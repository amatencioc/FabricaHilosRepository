using FabricaHilos.Models.RecursosHumanos;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System.Data;

namespace FabricaHilos.Services.RecursosHumanos;

public interface IReporteEventosSobretiempoService
{
    Task<EventosSobretiempoKpiViewModel> ObtenerKpiAsync(
        string codEmpresaAquarius, int anoIni, int mesIni, int anoFin, int mesFin, string tipo = "T",
        List<string>? granCcosto = null, string? centroCosto = null);

    // Catálogos Gran Centro de Costo / Centro de Costo (filtro jerárquico, mismo patrón
    // que Capacitacion/Admin/Reportes) — fuente: SIG.V_CENTRO_DE_COSTOS.
    Task<List<GranCcostoOptionDto>>  GetGranCcostoOptionsAsync();
    Task<List<CentroCostoOptionDto>> GetCentroCostoOptionsAsync();
}

/// <summary>
/// Orquesta el reporte "Eventos vs Sobretiempo": combina el sobretiempo por área
/// (SIG, PKG_RPT_EVENTOS_SOBRETIEMPO) con los eventos/ausencias por área (AQUARIUS,
/// vía IPlanillaMensualService.ObtenerResumenAsync — sin modificarlo), agrupando
/// ambos por (Área, Año, Mes). Sin vínculo empleado-a-empleado entre ambos lados.
/// </summary>
public class ReporteEventosSobretiempoService : IReporteEventosSobretiempoService
{
    private readonly string _connStrSig;
    private readonly IPlanillaMensualService _planillaMensualService;
    private readonly ILogger<ReporteEventosSobretiempoService> _logger;

    public ReporteEventosSobretiempoService(
        IConfiguration configuration,
        IPlanillaMensualService planillaMensualService,
        ILogger<ReporteEventosSobretiempoService> logger)
    {
        _connStrSig = configuration.GetConnectionString("LaColonialConnection")
            ?? throw new InvalidOperationException("LaColonialConnection no configurada.");
        _planillaMensualService = planillaMensualService;
        _logger = logger;
    }

    public async Task<EventosSobretiempoKpiViewModel> ObtenerKpiAsync(
        string codEmpresaAquarius, int anoIni, int mesIni, int anoFin, int mesFin, string tipo = "T",
        List<string>? granCcosto = null, string? centroCosto = null)
    {
        // Filtro múltiple de Áreas (Gran Centro de Costo, checkboxes tipo HorasExtras) →
        // Centro de Costo (single-select, sin cambios). ""/vacíos del query string se
        // descartan, igual que el resto de filtros opcionales del reporte. Los códigos
        // seleccionados se envían al PL/SQL como una sola cadena separada por comas
        // (PKG_RPT_EVENTOS_SOBRETIEMPO v2.1 — P_GRAN_CCOSTO ahora hace match tipo
        // IN-list vía LIKE) para no tener que cambiar la firma del procedure ni crear un
        // tipo TABLE nuevo solo para esto.
        var granCcostoList = granCcosto?.Where(g => !string.IsNullOrWhiteSpace(g)).Select(g => g.Trim()).Distinct().ToList();
        if (granCcostoList != null && granCcostoList.Count == 0) granCcostoList = null;
        var granCcostoParam = granCcostoList != null ? string.Join(",", granCcostoList) : null;
        centroCosto = string.IsNullOrWhiteSpace(centroCosto) ? null : centroCosto.Trim();

        var vm = new EventosSobretiempoKpiViewModel { AnoIni = anoIni, MesIni = mesIni, AnoFin = anoFin, MesFin = mesFin };
        if (granCcostoList != null || centroCosto != null)
            (vm.GranCcostoLabel, vm.CentroCostoLabel) = await ResolverLabelsCcostoAsync(granCcostoParam, centroCosto);
        var filas     = new Dictionary<(int Ano, int Mes, string Area), EventosSobretiempoAreaMesDto>();
        var empleados = new Dictionary<(int Ano, int Mes, string Cod), EventosSobretiempoEmpleadoDto>();
        // Crudo día+empleado (AQUARIUS.SCA_ASISTENCIA_TAREO) para clasificar HE por Evento
        // vs HE por Necesidad — ver PKG_RPT_EVENTOS_SOBRETIEMPO.SP_HE_DIARIO_AQUARIUS.
        // v2.2 (19/08/2026): TieneEvento se partió en TieneEventoPer (permisos formales
        // AQUARIUS, confiables) + TieneFaltaRaw (HORAS_FALTA crudo, sin corroborar — se
        // calcula de marcaciones biométricas y queda en "falta" TODOS los días para
        // personal exceptuado de marcar). TieneFaltaRaw se corrobora día a día contra
        // SIG.RH_EVENTOS (Logix) en el bloque "2b" antes de usarse — ver
        // ResolverFaltasLogixDiarioAsync más abajo.
        var heDiario  = new List<(int Ano, int Mes, DateTime Fecha, string CodPersonal, decimal HorasHe, decimal HorasBanco, bool TieneEventoPer, bool TieneFaltaRaw)>();
        // Consolidado final por tipo de evento (Label -> empleados distintos con ese evento + total días),
        // acumulado a lo largo de TODO el rango de meses consultado (no por área/mes).
        var consolidado = new Dictionary<string, (HashSet<string> Empleados, int TotalDias)>();

        // Trabajador excluido de todo el reporte (área, detalle y eventos) a pedido del usuario.
        const string CodExcluido = "034001";

        // ── 1) Sobretiempo por Área/Año/Mes (SIG) ───────────────────────────
        // Envuelto en reintentos: el pool de LaColonialConnection es pequeño (Max Pool
        // Size=10, ver appsettings.json) y bajo carga concurrente puede haber contención
        // transitoria (ORA-12170/ORA-3135/timeout) que antes tumbaba todo el reporte con
        // un solo intento fallido — coincide con el síntoma "intermitente, al reintentar
        // manualmente ya funciona".
        await EjecutarConReintentosAsync(async () =>
        {
        await using var conn = new OracleConnection(_connStrSig);
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "PKG_RPT_EVENTOS_SOBRETIEMPO.SP_RESUMEN_AREA";
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.BindByName  = true;
            cmd.Parameters.Add(new OracleParameter("P_ANO_INI",      OracleDbType.Int32)    { Value = anoIni });
            cmd.Parameters.Add(new OracleParameter("P_MES_INI",      OracleDbType.Int32)    { Value = mesIni });
            cmd.Parameters.Add(new OracleParameter("P_ANO_FIN",      OracleDbType.Int32)    { Value = anoFin });
            cmd.Parameters.Add(new OracleParameter("P_MES_FIN",      OracleDbType.Int32)    { Value = mesFin });
            cmd.Parameters.Add(new OracleParameter("P_TIPO",         OracleDbType.Varchar2) { Value = string.IsNullOrEmpty(tipo) ? "T" : tipo });
            cmd.Parameters.Add(new OracleParameter("P_GRAN_CCOSTO",  OracleDbType.Varchar2) { Value = (object?)granCcostoParam ?? DBNull.Value });
            cmd.Parameters.Add(new OracleParameter("P_CENTRO_COSTO", OracleDbType.Varchar2) { Value = (object?)centroCosto ?? DBNull.Value });
            var pCur = cmd.Parameters.Add("CV_1", OracleDbType.RefCursor);
            pCur.Direction = ParameterDirection.Output;

            await cmd.ExecuteNonQueryAsync();
            await using var r = ((OracleRefCursor)pCur.Value).GetDataReader();
            // Limpia resultados de un intento previo fallido a mitad de lectura (el
            // cursor se reabre desde cero en cada reintento).
            filas.Clear();
            while (await r.ReadAsync())
            {
                var ano  = GetInt(r, "ANO");
                var mes  = GetInt(r, "MES");
                // El label de esta fila es siempre el Gran Centro de Costo (aunque ya se haya
                // filtrado por uno específico), para que el drill-down Área → Centro de Costo →
                // Empleado siempre muestre los 3 niveles, sin importar los filtros aplicados.
                var area = r["DESC_GRAN_CCOSTO"]?.ToString()?.Trim() ?? "SIN ÁREA";
                // SP_RESUMEN_AREA agrupa por (GRAN_CCOSTO, CENTRO_COSTO) desde v1.8: puede
                // devolver VARIAS filas para la misma Área (una por Centro de Costo). Antes se
                // sobrescribía filas[(ano,mes,area)] con cada fila nueva, dejando el 1er nivel
                // "Detalle por Área" con los números de solo el ÚLTIMO Centro de Costo leído en
                // vez del total del Área — se acumula en vez de reemplazar (PCT_DEL_TOTAL_HE es
                // sumable: cada fila ya es % del gran total, no del área).
                if (!filas.TryGetValue((ano, mes, area), out var fila))
                {
                    fila = new EventosSobretiempoAreaMesDto { Ano = ano, Mes = mes, Area = area };
                    filas[(ano, mes, area)] = fila;
                }
                fila.TotalTrabajadores   += GetInt(r, "NRO_TRABAJADORES");
                fila.TotalHorasExtras    += GetDecimal(r, "TOTAL_SOBRETIEMPO");
                fila.He25                += GetDecimal(r, "HE_25");
                fila.He35                += GetDecimal(r, "HE_35");
                fila.He100               += GetDecimal(r, "HE_100");
                fila.HorasHe             += GetDecimal(r, "HORAS_SOBRETIEMPO");
                fila.MontoProduccion     += GetDecimal(r, "MONTO_PRODUCCION");
                fila.PctTotalHorasExtras += GetDecimal(r, "PCT_DEL_TOTAL_HE");
                fila.TrabajadoresConHe   += GetInt(r, "TRAB_CON_HE");
            }
        }
        }, nameof(EventosSobretiempoAreaMesDto));

        // ── 1b) Sobretiempo por Empleado/Área/Año/Mes (SIG) — drill-down ────
        await EjecutarConReintentosAsync(async () =>
        {
        await using var conn = new OracleConnection(_connStrSig);
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "PKG_RPT_EVENTOS_SOBRETIEMPO.SP_DETALLE_SOBRETIEMPO";
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.BindByName  = true;
            cmd.Parameters.Add(new OracleParameter("P_ANO_INI",      OracleDbType.Int32)    { Value = anoIni });
            cmd.Parameters.Add(new OracleParameter("P_MES_INI",      OracleDbType.Int32)    { Value = mesIni });
            cmd.Parameters.Add(new OracleParameter("P_ANO_FIN",      OracleDbType.Int32)    { Value = anoFin });
            cmd.Parameters.Add(new OracleParameter("P_MES_FIN",      OracleDbType.Int32)    { Value = mesFin });
            cmd.Parameters.Add(new OracleParameter("P_TIPO",         OracleDbType.Varchar2) { Value = string.IsNullOrEmpty(tipo) ? "T" : tipo });
            cmd.Parameters.Add(new OracleParameter("P_GRAN_CCOSTO",  OracleDbType.Varchar2) { Value = (object?)granCcostoParam ?? DBNull.Value });
            cmd.Parameters.Add(new OracleParameter("P_CENTRO_COSTO", OracleDbType.Varchar2) { Value = (object?)centroCosto ?? DBNull.Value });
            var pCur = cmd.Parameters.Add("CV_1", OracleDbType.RefCursor);
            pCur.Direction = ParameterDirection.Output;

            await cmd.ExecuteNonQueryAsync();
            await using var r = ((OracleRefCursor)pCur.Value).GetDataReader();
            // Limpia resultados de un intento previo fallido a mitad de lectura.
            empleados.Clear();
            while (await r.ReadAsync())
            {
                var ano = GetInt(r, "ANO");
                var mes = GetInt(r, "MES");
                var cod = r["C_CODIGO"]?.ToString()?.Trim() ?? "";
                if (cod == "") continue;
                empleados[(ano, mes, cod)] = new EventosSobretiempoEmpleadoDto
                {
                    Ano             = ano,
                    Mes             = mes,
                    // Igual que en SP_RESUMEN_AREA: siempre el Gran Centro de Costo, para
                    // mantener los 3 niveles del drill-down aunque ya se haya filtrado.
                    Area            = r["DESC_GRAN_CCOSTO"]?.ToString()?.Trim() ?? "SIN ÁREA",
                    // Gran Centro de Costo real de ESTA fila (Ano/Mes), sin importar el filtro.
                    GranCcostoDesc  = r["DESC_GRAN_CCOSTO"]?.ToString()?.Trim(),
                    CodEmpleado     = cod,
                    NomEmpleado     = r["NOMBRE_CORTO"]?.ToString()?.Trim() ?? cod,
                    TotalHorasExtras = GetDecimal(r, "TOTAL_SOBRETIEMPO"),
                    He25             = GetDecimal(r, "SOBRETIEMPO_25"),
                    He35             = GetDecimal(r, "SOBRETIEMPO_35"),
                    He100            = GetDecimal(r, "SOBRETIEMPO_100"),
                    HorasHe          = GetDecimal(r, "HORAS_SOBRETIEMPO"),
                    MontoProduccion  = GetDecimal(r, "MONTO_PRODUCCION"),
                    HorasHeOtroCc    = GetDecimal(r, "HORAS_HE_OTRO_CC"),
                    DescHeOtroCc     = r["DESC_HE_OTRO_CC"]?.ToString()?.Trim(),
                };
            }
        }
        }, nameof(EventosSobretiempoEmpleadoDto));

        // ── 1c) Crudo día+empleado HE/Evento (AQUARIUS.SCA_ASISTENCIA_TAREO) ─
        // Sin resolución de área en SQL (ver comentario del SP): se resuelve abajo con
        // ccostoPorEmpleado, el mismo diccionario ya usado para el resto del reporte.
        await EjecutarConReintentosAsync(async () =>
        {
        await using var conn = new OracleConnection(_connStrSig);
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "PKG_RPT_EVENTOS_SOBRETIEMPO.SP_HE_DIARIO_AQUARIUS";
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.BindByName  = true;
            cmd.Parameters.Add(new OracleParameter("P_ANO_INI", OracleDbType.Int32)    { Value = anoIni });
            cmd.Parameters.Add(new OracleParameter("P_MES_INI", OracleDbType.Int32)    { Value = mesIni });
            cmd.Parameters.Add(new OracleParameter("P_ANO_FIN", OracleDbType.Int32)    { Value = anoFin });
            cmd.Parameters.Add(new OracleParameter("P_MES_FIN", OracleDbType.Int32)    { Value = mesFin });
            cmd.Parameters.Add(new OracleParameter("P_TIPO",    OracleDbType.Varchar2) { Value = string.IsNullOrEmpty(tipo) ? "T" : tipo });
            var pCur = cmd.Parameters.Add("CV_1", OracleDbType.RefCursor);
            pCur.Direction = ParameterDirection.Output;

            await cmd.ExecuteNonQueryAsync();
            await using var r = ((OracleRefCursor)pCur.Value).GetDataReader();
            heDiario.Clear();
            while (await r.ReadAsync())
            {
                var cod = r["COD_PERSONAL"]?.ToString()?.Trim();
                if (string.IsNullOrEmpty(cod) || cod == CodExcluido) continue;
                heDiario.Add((
                    GetInt(r, "ANO"),
                    GetInt(r, "MES"),
                    Convert.ToDateTime(r["FECHAMAR"]),
                    cod,
                    GetDecimal(r, "HORAS_HE_DIA"),
                    GetDecimal(r, "HORAS_BANCO_DIA"),
                    GetInt(r, "TIENE_EVENTO_PER") == 1,
                    GetInt(r, "TIENE_FALTA_RAW") == 1));
            }
        }
        }, "SP_HE_DIARIO_AQUARIUS");

        // ── 2) Resolución Área/Centro de Costo/situación laboral/contrato vigente por empleado ─
        var ccostoPorEmpleado       = await ResolverCcostoPorEmpleadoAsync(codEmpresaAquarius);
        var estadoPorEmpleado       = await ResolverEstadoPorEmpleadoAsync();
        var codPersonalActivoPorSpring = await ResolverCodPersonalActivoAsync(codEmpresaAquarius);
        // Traduce COD_PERSONAL (identifica el CONTRATO en AQUARIUS.SCA_ASISTENCIA_TAREO,
        // fuente de heDiario) -> COD_SPRING (identifica a la PERSONA, clave de
        // ccostoPorEmpleado). Confirmado con MCP (14/08/2026): COD_PERSONAL nunca es igual
        // a COD_SPRING (0 de 615 coincidencias directas), así que sin esta traducción el
        // TryGetValue de más abajo fallaba siempre y el 100% del HE clasificado (Evento y
        // Necesidad) caía en el bucket "SIN ÁREA" — areas reales quedaban todas en cero en
        // "Proyección de Bolsa de HE por Área" mientras "SIN ÁREA" acumulaba todo (ej. HE
        // Evento prom. = 14,370.2 h).
        var codSpringPorCodPersonal = await ResolverCodSpringPorCodPersonalAsync(codEmpresaAquarius);
        // Días exactos (no el total mensual de ResolverFaltasLogixAsync) con Falta formal
        // Logix (SIG.RH_EVENTOS, C_TIPO='07'), para corroborar TieneFaltaRaw día a día
        // antes de contarlo como evento real en la clasificación de abajo (v2.2, FIX
        // incoherencia "grupo sin eventos pero 100% HE por Evento").
        var faltasLogixDiario = await ResolverFaltasLogixDiarioAsync(
            new DateTime(anoIni, mesIni, 1),
            new DateTime(anoFin, mesFin, 1).AddMonths(1).AddDays(-1));

        // heDiario (SP_HE_DIARIO_AQUARIUS) no acepta P_GRAN_CCOSTO/P_CENTRO_COSTO — trae
        // SIEMPRE la empresa completa. FIX 21/08/2026 (reportado por el usuario: filtrando
        // por Gran Centro de Costo=ADMINISTRACION igual aparecían SIN ÁREA/SISTEMAS/
        // TINTORERIA con HE Banco de otras áreas): mismo criterio de filtro que ya se aplica
        // a "resumen" más abajo, para que heAreaAcc/heEmpAcc/heBancoEmpAcc (y sus filas
        // "fantasma") nunca incluyan empleados fuera del Gran Centro de Costo/Centro de
        // Costo activo.
        bool PasaFiltroCcosto(string codSpring) =>
            (granCcostoList is null || (ccostoPorEmpleado.TryGetValue(codSpring, out var ccFiltroG)
                && !string.IsNullOrEmpty(ccFiltroG.GranCcosto) && granCcostoList.Contains(ccFiltroG.GranCcosto)))
            && (centroCosto is null || (ccostoPorEmpleado.TryGetValue(codSpring, out var ccFiltroC)
                && ccFiltroC.CentroCosto == centroCosto));

        // ── 2a) HE Banco/Compensación por (Ano, Mes, Empleado) — v2.4 (21/08/2026) ──
        // Eje independiente de Evento/Necesidad (ese es "por qué" se hizo HE; esto es "cómo
        // se liquida": planilla/dinero vs banco de horas/descanso). No requiere agrupar por
        // Área/Centro de Costo para clasificar pool de evento — es un simple total por
        // empleado/mes (HORAS_BANCO_DIA, AQUARIUS.SCA_ASISTENCIA_TAREO.HORABANCOH), fuente
        // de PKG_RPT_EVENTOS_SOBRETIEMPO.SP_HE_DIARIO_AQUARIUS v2.4.
        var heBancoEmpAcc = heDiario
            .Select(h => new
            {
                h.Ano, h.Mes, h.HorasBanco,
                CodSpring = codSpringPorCodPersonal.TryGetValue(h.CodPersonal, out var cs) ? cs : h.CodPersonal,
            })
            .Where(h => PasaFiltroCcosto(h.CodSpring))
            .GroupBy(h => (h.Ano, h.Mes, h.CodSpring))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.HorasBanco));

        // ── 2b) Clasificación HE por Evento vs HE por Necesidad (día a día) ──
        // Regla: si en (día, área) hubo al menos 1 empleado con evento, TODO el HE de ese
        // día en esa área es "por Evento" (cobertura); si no, es "por Necesidad". Se agrupa
        // primero por (Ano, Mes, Fecha, Área[, CentroCosto]) para saber si "hubo evento" ese
        // día en esa área, y recién ahí se clasifica el total de HE de ese día.
        // v2.1 (14/08/2026): se agrega heEmpAcc (por Empleado) a este mismo cálculo, y ya NO
        // se acumula heCcAcc de forma independiente — Centro de Costo se arma más abajo
        // SUMANDO el detalle por Empleado (vm.CentrosCosto), igual que ya se hacía para que
        // Área = Σ Centro de Costo. Todo se deriva de la MISMA partición por (día, Área,
        // Centro de Costo, Empleado) para que Empleado sea la fuente de verdad y los 3
        // niveles (Área/Centro de Costo/Empleado) siempre sumen exacto entre sí.
        //
        // v2.3 (20/08/2026) — FIX incoherencia "electricistas de MANTENIMIENTO HILANDERIA
        // sin ningún evento propio, pero con HE 100% clasificado por Evento": el Centro de
        // Costo "MANTENIMIENTO HILANDERIA" (y en general el Gran Centro de Costo
        // MANTENIMIENTO) agrupa ~25 empleados activos de ~14 oficios distintos
        // (electricista, ayudante de almacén, mecánico, auxiliar de oficina, etc. —
        // confirmado vía MCP AQUARIUS.PLA_PERSONAL). Prácticamente todos los días del mes
        // ALGUIEN de ese Centro de Costo tiene un permiso formal (vacaciones/descanso
        // médico/etc.), pero un electricista NO cubre el trabajo de un auxiliar de oficina
        // ausente — la "cobertura" solo tiene sentido entre compañeros del MISMO oficio.
        // Fix: para el Gran Centro de Costo MANTENIMIENTO, "hubo evento" se calcula por
        // (día, Área, CentroCosto, Especialidad) en vez de (día, Área, CentroCosto) —
        // reutiliza ResolverSubCentroCosto(Puesto) (mismo agrupador ya usado para el nivel
        // presentacional "Sub Centro de Costo"). Validado con datos reales: jul/2026, CC=
        // MANTENIMIENTO HILANDERIA (P740), los 3 electricistas (034664/034253/034566) no
        // tienen NINGÚN PER_*/falta-Logix propio en todo el mes, mientras que 7 compañeros
        // de OTROS oficios sí (cargos 216/304/033/299/237/016) — de ahí que antes de este
        // fix su HE saliera ~100% "por Evento" pese a DiasEvento=0. Fuera de MANTENIMIENTO
        // el agrupador sigue siendo (día, Área, CentroCosto), sin cambios.
        var heAreaAcc = new Dictionary<(int Ano, int Mes, string Area), (decimal Evento, decimal Necesidad)>();
        var heEmpAcc  = new Dictionary<(int Ano, int Mes, string CodSpring), (decimal Evento, decimal Necesidad)>();
        // v2.5 (20/08/2026) — detalle día a día por empleado, a pedido del usuario para poder
        // auditar/desagregar el HE Evento/Necesidad mensual (ver EventosSobretiempoDiaEmpleadoDto).
        var detalleDiarioHe = new List<EventosSobretiempoDiaEmpleadoDto>();
        {
            var conArea = heDiario
                .Select(h => new
                {
                    h.Ano, h.Mes, h.Fecha, h.HorasHe,
                    // h.CodPersonal viene de SCA_ASISTENCIA_TAREO (identifica el contrato);
                    // se traduce a COD_SPRING antes de buscar en ccostoPorEmpleado (ver
                    // comentario de codSpringPorCodPersonal más arriba) y antes de
                    // corroborar TieneFaltaRaw contra faltasLogixDiario (misma clave).
                    CodSpring = codSpringPorCodPersonal.TryGetValue(h.CodPersonal, out var cs) ? cs : h.CodPersonal,
                    h.TieneEventoPer, h.TieneFaltaRaw,
                })
                // Mismo FIX de heBancoEmpAcc (ver PasaFiltroCcosto arriba): sin esto, un
                // empleado de OTRO Gran Centro de Costo/Centro de Costo con HE paga igual
                // aparecía como fila "fantasma" al filtrar (raro antes porque casi siempre
                // ya existía en el detalle SIG filtrado, pero posible).
                .Where(h => PasaFiltroCcosto(h.CodSpring))
                .Select(h => new
                {
                    h.Ano, h.Mes, h.Fecha, h.HorasHe, h.CodSpring,
                    // TieneEvento final = permiso formal confiable OR falta cruda
                    // corroborada día a día contra Logix (RH_EVENTOS). Descarta el
                    // "falso positivo" de HORAS_FALTA para personal exceptuado de marcar
                    // (ver v2.2 en PKG_RPT_EVENTOS_SOBRETIEMPO.sql).
                    TieneEvento = h.TieneEventoPer || (h.TieneFaltaRaw && faltasLogixDiario.Contains((h.CodSpring, h.Fecha.Date))),
                    Area        = ccostoPorEmpleado.TryGetValue(h.CodSpring, out var cc) ? cc.Area : "SIN ÁREA",
                    CentroCosto = ccostoPorEmpleado.TryGetValue(h.CodSpring, out var cc2) ? (cc2.CentroCostoDesc ?? "SIN CENTRO DE COSTO") : "SIN CENTRO DE COSTO",
                    // Especialidad (v2.3): solo se usa para MANTENIMIENTO, ver comentario arriba.
                    // "" para todo lo demás, así el GroupBy no fragmenta el resto de Áreas.
                    Especialidad = (ccostoPorEmpleado.TryGetValue(h.CodSpring, out var cc3) ? cc3.Area : "SIN ÁREA") == "MANTENIMIENTO"
                        ? (ResolverSubCentroCosto(ccostoPorEmpleado.TryGetValue(h.CodSpring, out var cc4) ? cc4.Puesto : null) ?? "SIN PUESTO")
                        : "",
                })
                .ToList();

            // "Hubo evento" es una propiedad de (día, Área, Centro de Costo[, Especialidad]):
            // cualquier empleado de ese Centro de Costo (del mismo oficio, si es MANTENIMIENTO)
            // con evento ese día contamina el HE trabajado ahí ese día (regla de negocio, ver
            // _MEMORIA_EVENTOS_SOBRETIEMPO.md).
            var huboEventoPorDia = conArea
                .GroupBy(x => (x.Ano, x.Mes, x.Fecha, x.Area, x.CentroCosto, x.Especialidad))
                .ToDictionary(g => g.Key, g => g.Any(x => x.TieneEvento));

            foreach (var g in conArea.GroupBy(x => (x.Ano, x.Mes, x.Fecha, x.Area, x.CentroCosto, x.Especialidad, x.CodSpring)))
            {
                var horas      = g.Sum(x => x.HorasHe);
                var huboEvento = huboEventoPorDia[(g.Key.Ano, g.Key.Mes, g.Key.Fecha, g.Key.Area, g.Key.CentroCosto, g.Key.Especialidad)];

                var keyArea = (g.Key.Ano, g.Key.Mes, g.Key.Area);
                heAreaAcc.TryGetValue(keyArea, out var accArea);
                heAreaAcc[keyArea] = huboEvento
                    ? (accArea.Evento + horas, accArea.Necesidad)
                    : (accArea.Evento, accArea.Necesidad + horas);

                var keyEmp = (g.Key.Ano, g.Key.Mes, g.Key.CodSpring);
                heEmpAcc.TryGetValue(keyEmp, out var accEmp);
                heEmpAcc[keyEmp] = huboEvento
                    ? (accEmp.Evento + horas, accEmp.Necesidad)
                    : (accEmp.Evento, accEmp.Necesidad + horas);

                detalleDiarioHe.Add(new EventosSobretiempoDiaEmpleadoDto
                {
                    Ano = g.Key.Ano, Mes = g.Key.Mes, Fecha = g.Key.Fecha, CodEmpleado = g.Key.CodSpring,
                    HorasHe = horas, HuboEventoPool = huboEvento, TieneEventoPropio = g.Any(x => x.TieneEvento),
                });
            }
        }

        // Reparte TotalHorasExtras (soles, SIG, mensual — sin fecha por día) proporcional a
        // las horas AQUARIUS de cada bucket, único criterio posible al no haber fecha diaria
        // del lado SIG. Sin datos AQUARIUS ese mes/área (totalHoras=0 — ej. el empleado tiene
        // filas de tareo pero cero HE registrada en AQUARIUS ese mes), TODO el monto va a
        // Necesidad (v2.4, 20/08/2026) — antes devolvía (0,0) y ese monto desaparecía del
        // desglose aunque seguía contando en el total mostrado (HE S/.).
        static (decimal MontoEvento, decimal MontoNecesidad) RepartirMontoHe(decimal totalSoles, decimal horasEvento, decimal horasNecesidad)
        {
            var totalHoras = horasEvento + horasNecesidad;
            if (totalHoras <= 0) return (0m, totalSoles);
            var montoEvento = Math.Round(totalSoles * (horasEvento / totalHoras), 2);
            return (montoEvento, Math.Round(totalSoles - montoEvento, 2));
        }

        // Igual que RepartirMontoHe pero para horas: heDiario (AQUARIUS.SCA_ASISTENCIA_TAREO)
        // es un universo/corte distinto al HE oficial de SIG (INGRE_PLA), así que la suma cruda
        // de horasEvento/horasNecesidad de AQUARIUS NO cuadra con HorasHe de SIG — se usa esa
        // suma solo como PROPORCIÓN y se reparte sobre el total oficial, para que Evento+Necesidad
        // siempre sumen exacto el HE (h) mostrado en pantalla (mismo criterio ya usado en soles).
        // totalAquarius=0 (v2.4, 20/08/2026): mismo criterio que RepartirMontoHe — TODO el
        // HE va a Necesidad en vez de (0,0), para que Evento+Necesidad siempre sume el HE(h).
        static (decimal HorasEvento, decimal HorasNecesidad) RepartirHorasHe(decimal totalHorasHe, decimal horasEventoAquarius, decimal horasNecesidadAquarius)
        {
            var totalAquarius = horasEventoAquarius + horasNecesidadAquarius;
            if (totalAquarius <= 0) return (0m, totalHorasHe);
            var horasEvento = Math.Round(totalHorasHe * (horasEventoAquarius / totalAquarius), 2);
            return (horasEvento, Math.Round(totalHorasHe - horasEvento, 2));
        }

        // SP_SCA_RESUMENTAREO_SIGLIVE exige un cod_tipo_planilla real (no admite "0"/"todos",
        // ver PlanillaMensualController.ApiResumen que rechaza "0" con BadRequest) — hay que
        // iterar sobre los tipos reales de la empresa y combinar los resultados.
        var tiposPlanilla = (await _planillaMensualService.ObtenerTiposPlanillaAsync(codEmpresaAquarius))
            .Where(tp => tipo switch
            {
                "O" => tp.DesTipoPlanilla.StartsWith("OBRERO", StringComparison.OrdinalIgnoreCase),
                "E" => tp.DesTipoPlanilla.StartsWith("EMPLEADO", StringComparison.OrdinalIgnoreCase),
                _   => true,
            })
            .ToList();

        // Optimización (back-end): antes ResolverFaltasLogixAsync se llamaba UNA VEZ POR
        // MES dentro del foreach de abajo, abriendo una conexión/round-trip a Oracle SIG
        // (RH_EVENTOS) por cada mes del rango consultado (ej. 12 round-trips para un año
        // completo, todos con la misma forma de query, solo cambiando el rango de fechas).
        // Se reemplaza por UNA sola consulta con los eventos crudos (C_CODIGO/D_INICIO/
        // D_FINAL) que se solapen con TODO el rango [P_ANO_INI/MES_INI .. P_ANO_FIN/MES_FIN],
        // y el corte por mes (SUM de días que solapan ese mes puntual) se calcula en
        // memoria (RecortarFaltasLogixPorMes) — mismo resultado, 1 sola ida a Oracle en vez
        // de N.
        var fechaInicioRango = new DateTime(anoIni, mesIni, 1);
        var fechaFinalRango  = new DateTime(anoFin, mesFin, 1).AddMonths(1).AddDays(-1);
        var eventosFaltaLogix = await ResolverEventosFaltaLogixAsync(fechaInicioRango, fechaFinalRango);

        // ── 3) Eventos por mes (AQUARIUS, reusa IPlanillaMensualService) ────
        foreach (var (ano, mes) in MesesDelRango(anoIni, mesIni, anoFin, mesFin))
        {
            var fechaInicio = new DateTime(ano, mes, 1);
            var fechaFinal  = fechaInicio.AddMonths(1).AddDays(-1);

            var resumen = new List<PlanillaResumenDto>();
            foreach (var tp in tiposPlanilla)
            {
                var filtro = new PlanillaMensualFiltroDto
                {
                    CodEmpresa      = codEmpresaAquarius,
                    CodSucursal     = "0",
                    CodTipoPlanilla = tp.CodTipoPlanilla,
                    CCostos         = "TODOS",
                    FechaInicio     = fechaInicio.ToString("dd/MM/yyyy"),
                    FechaFinal      = fechaFinal.ToString("dd/MM/yyyy"),
                };
                // Reintenta hasta 3 veces con backoff ante fallas transitorias (timeout de
                // Oracle, pool de conexiones agotado, hiccup de red) — antes un solo intento
                // fallido descartaba en SILENCIO ese Año/Mes/Tipo (solo quedaba en el log),
                // lo que explicaba el síntoma reportado: "se pierde data al cargar y al
                // volver a consultar trae todo correctamente" (era justamente el reintento
                // manual del usuario el que "arreglaba" la falla transitoria).
                const int maxIntentos = 3;
                var exitoso = false;
                Exception? ultimoError = null;
                for (var intento = 1; intento <= maxIntentos && !exitoso; intento++)
                {
                    try
                    {
                        resumen.AddRange(await _planillaMensualService.ObtenerResumenAsync(filtro));
                        exitoso = true;
                    }
                    catch (Exception ex)
                    {
                        ultimoError = ex;
                        _logger.LogWarning(ex, "Intento {Intento}/{Max} fallido al obtener eventos AQUARIUS para {Ano}-{Mes} Tipo:{Tipo}",
                            intento, maxIntentos, ano, mes, tp.CodTipoPlanilla);
                        if (intento < maxIntentos)
                            await Task.Delay(TimeSpan.FromMilliseconds(300 * intento));
                    }
                }
                if (!exitoso)
                {
                    _logger.LogError(ultimoError, "Error al obtener eventos AQUARIUS para {Ano}-{Mes} Tipo:{Tipo} tras {Max} intentos",
                        ano, mes, tp.CodTipoPlanilla, maxIntentos);
                    vm.Advertencias.Add($"No se pudo obtener el detalle de eventos AQUARIUS de {tp.DesTipoPlanilla} para {mes:00}/{ano}. Los totales de esa combinación pueden estar incompletos — reintente la consulta.");
                }
            }

            // Descarta filas "fantasma": SCA_ASISTENCIA_TAREO puede tener tareo huérfano
            // enganchado a un COD_PERSONAL (contrato) antiguo/cerrado de un empleado que
            // reingresó con otro contrato — mismo COD_SPRING, pero ya no es su registro
            // vigente (patrón ya documentado dentro de SP_SCA_RESUMENTAREO_SIGLIVE, caso
            // YUTCA LOPEZ 034630). Se compara contra el COD_PERSONAL más reciente por
            // COD_SPRING para no arrastrar ausencias de un contrato que ya no aplica.
            //
            // También se descarta aquí (y no solo al armar vm.Empleados) al personal ya
            // cesado ANTES de este (ano, mes): antes este filtro de situación laboral se
            // aplicaba recién al final sobre "empleados" (vm.Empleados), así que sus días
            // de evento igual quedaban sumados en "Detalle por Área" y en "Consolidado de
            // Eventos" aunque el empleado ya no apareciera en el drill-down — los filtros
            // de período/tipo de la vista no se reflejaban igual en las 3 tablas. Al
            // filtrar acá, Áreas/Consolidado/Empleados quedan consistentes entre sí.
            resumen = resumen
                .Where(e => e.CodPersonal != CodExcluido)
                .Where(e => e.CodPersonal is null || e.CodPersonalAquarius is null
                    || !codPersonalActivoPorSpring.TryGetValue(e.CodPersonal, out var activo)
                    || e.CodPersonalAquarius == activo)
                .Where(e => e.CodPersonal is null
                    || !estadoPorEmpleado.TryGetValue(e.CodPersonal, out var est)
                    || est.FecCese is null || est.FecCese >= fechaInicio)
                // Filtro jerárquico Gran Centro de Costo / Centro de Costo (opcional): se aplica
                // acá, ANTES de armar porArea/empleados/consolidado, para que las 3 tablas del
                // dashboard queden consistentes entre sí (mismo patrón que los 3 filtros de
                // arriba — ver comentario de "Áreas/Consolidado/Empleados quedan consistentes").
                .Where(e => granCcostoList is null || (e.CodPersonal != null
                    && ccostoPorEmpleado.TryGetValue(e.CodPersonal, out var ccG)
                    && !string.IsNullOrEmpty(ccG.GranCcosto) && granCcostoList.Contains(ccG.GranCcosto)))
                .Where(e => centroCosto is null || (e.CodPersonal != null
                    && ccostoPorEmpleado.TryGetValue(e.CodPersonal, out var ccC) && ccC.CentroCosto == centroCosto))
                .ToList();

            // DiasFalta de AQUARIUS sale de marcaciones biométricas (SCA_ASISTENCIA_TAREO):
            // hay trabajadores activos que jamás marcan (ej. personal exceptuado) y quedan
            // con "Falta" el 100% de sus días trabajados sin que exista ausencia real. La
            // fuente confiable es el evento formal registrado en Logix (SIG.RH_EVENTOS,
            // C_TIPO='07'), igual que ya se hace para Vacaciones/D.Médico/etc. dentro de la
            // propia SP_SCA_RESUMENTAREO_SIGLIVE — se reemplaza el valor de AQUARIUS por éste.
            var faltasLogix = RecortarFaltasLogixPorMes(eventosFaltaLogix, fechaInicio, fechaFinal);
            foreach (var e in resumen)
                if (!string.IsNullOrEmpty(e.CodPersonal))
                    e.DiasFalta = faltasLogix.TryGetValue(e.CodPersonal, out var df) ? df : 0;

            // Área del empleado para eventos: prioriza el área SIG (SP_DETALLE_SOBRETIEMPO,
            // la misma fuente de fila.TotalHorasExtras) y solo cae al mapeo AQUARIUS si el
            // empleado no tiene sobretiempo SIG ese mes — así el total de DiasEvento por área
            // cuadra siempre con la suma del detalle por empleado (antes usaban áreas distintas).
            // Siempre usa el Gran Centro de Costo (nunca el Centro de Costo puntual) para
            // mantener los 3 niveles del drill-down (Área → Centro de Costo → Empleado) sin
            // importar los filtros aplicados.
            string ResolverAreaEvento(string cod) =>
                empleados.TryGetValue((ano, mes, cod), out var e) && !string.IsNullOrEmpty(e.Area)
                    ? e.Area
                    : (ccostoPorEmpleado.TryGetValue(cod, out var a) ? a.Area : "SIN ÁREA");

            // Descarta filas sin CodPersonal (mismo criterio que el bucle de abajo que puebla
            // "empleados"/"consolidado": if (string.IsNullOrEmpty(e.CodPersonal)) continue;).
            // Antes estas filas SÍ se contaban acá bajo "SIN ÁREA" (TrabajadoresConEvento/
            // DiasEvento > 0) pero nunca llegaban a empleados/vm.CentrosCosto/vm.Empleados
            // (por el "continue" de abajo) — la fila "SIN ÁREA" de la tabla por Área mostraba
            // Trab. c/Evento y Días Evento con datos, pero el drill-down (Centro de Costo →
            // Empleado) quedaba vacío para esa misma Área, sin explicación aparente.
            var porArea = resumen
                .Where(e => e.CodPersonal is not null)
                .Select(e => new
                {
                    Area = ResolverAreaEvento(e.CodPersonal!),
                    Dias = TotalDiasEvento(e),
                    HorasProduccion = ParseHorasHHMM(e.HorasEfectivas),
                })
                .GroupBy(x => x.Area);

            // Días de evento por empleado — mismo criterio que TotalDiasEvento, para el
            // drill-down de la tabla "Detalle por Área" (SP_DETALLE_SOBRETIEMPO no trae
            // eventos, así que se completan/crean aquí por (Ano, Mes, CodEmpleado)). Se
            // acumula (+=) en vez de asignar por si el empleado aparece en más de un tipo
            // de planilla, y se arma el desglose por tipo de evento ("Vacaciones: 3, ...").
            foreach (var e in resumen)
            {
                if (string.IsNullOrEmpty(e.CodPersonal)) continue;
                var keyEmp = (ano, mes, e.CodPersonal);
                if (!empleados.TryGetValue(keyEmp, out var emp))
                {
                    emp = new EventosSobretiempoEmpleadoDto
                    {
                        Ano = ano, Mes = mes, Area = ResolverAreaEvento(e.CodPersonal),
                        GranCcostoDesc = ccostoPorEmpleado.TryGetValue(e.CodPersonal, out var ccArea) ? ccArea.Area : null,
                        CodEmpleado = e.CodPersonal, NomEmpleado = e.NomTrabajador ?? e.CodPersonal,
                    };
                    empleados[keyEmp] = emp;
                }
                emp.DiasEvento += TotalDiasEvento(e);
                emp.HorasProduccion += ParseHorasHHMM(e.HorasEfectivas);

                var categorias = CategoriasEvento(e).Where(c => c.Dias > 0).ToList();
                if (categorias.Count > 0)
                {
                    var partes = emp.DescEventos.Length > 0 ? new List<string> { emp.DescEventos } : new List<string>();
                    partes.AddRange(categorias.Select(c => $"{c.Label}: {c.Dias}"));
                    emp.DescEventos = string.Join(", ", partes);

                    foreach (var c in categorias)
                    {
                        if (!consolidado.TryGetValue(c.Label, out var acc))
                            consolidado[c.Label] = acc = (new HashSet<string>(), 0);
                        acc.Empleados.Add(e.CodPersonal!);
                        consolidado[c.Label] = (acc.Empleados, acc.TotalDias + c.Dias);
                    }
                }
            }

            foreach (var g in porArea)
            {
                var key = (ano, mes, g.Key);
                if (!filas.TryGetValue(key, out var fila))
                {
                    fila = new EventosSobretiempoAreaMesDto { Ano = ano, Mes = mes, Area = g.Key };
                    filas[key] = fila;
                }
                fila.TrabajadoresConEvento = g.Count(x => x.Dias > 0);
                fila.DiasEvento            = g.Sum(x => x.Dias);
                fila.HorasProduccion      += g.Sum(x => x.HorasProduccion);
            }
        }

        // TotalTrabajadores (SIG) solo cuenta a quien tiene registrado al menos un concepto
        // de HE ese mes — un área puede tener empleados con eventos (AQUARIUS) pero CERO
        // horas extra, y antes esa fila mostraba "0 Trabajadores" con "1 Trab. c/Evento" (
        // confuso: parecía que no había nadie). Se reemplaza por el headcount real: cantidad
        // de empleados distintos que aparecen en el drill-down de esa Área/Año/Mes (unión de
        // ambas fuentes), siempre >= el conteo original de SIG.
        var headcountPorArea = empleados.Values
            .GroupBy(e => (e.Ano, e.Mes, e.Area))
            .ToDictionary(g => g.Key, g => g.Select(e => e.CodEmpleado).Distinct().Count());
        foreach (var fila in filas.Values)
            if (headcountPorArea.TryGetValue((fila.Ano, fila.Mes, fila.Area), out var headcount) && headcount > fila.TotalTrabajadores)
                fila.TotalTrabajadores = headcount;

        // Aplica la clasificación HE Evento/Necesidad (2b) sobre cada fila de Área,
        // creando la fila si el área solo tiene HE en AQUARIUS pero no en SIG ese mes.
        foreach (var (key, acc) in heAreaAcc)
        {
            if (!filas.TryGetValue(key, out var fila))
            {
                fila = new EventosSobretiempoAreaMesDto { Ano = key.Ano, Mes = key.Mes, Area = key.Area };
                filas[key] = fila;
            }
            (fila.HorasHeEvento, fila.HorasHeNecesidad) = RepartirHorasHe(fila.HorasHe, acc.Evento, acc.Necesidad);
            (fila.MontoHeEvento, fila.MontoHeNecesidad) = RepartirMontoHe(fila.TotalHorasExtras, acc.Evento, acc.Necesidad);
        }

        vm.Areas = filas.Values.OrderBy(f => f.Ano).ThenBy(f => f.Mes).ThenBy(f => f.Area).ToList();

        // Igual que con Área (heAreaAcc, arriba): crea filas de Empleado "fantasma" (sin
        // datos SIG, solo clasificación HE Evento/Necesidad) para códigos que existen
        // ÚNICAMENTE en AQUARIUS (heEmpAcc) — así Centro de Costo (que se arma más abajo
        // SUMANDO el detalle por Empleado) no pierde estas horas.
        foreach (var key in heEmpAcc.Keys)
        {
            var keyEmp = (key.Ano, key.Mes, key.CodSpring);
            if (!empleados.ContainsKey(keyEmp))
            {
                var areaResuelta = ccostoPorEmpleado.TryGetValue(key.CodSpring, out var ccFant) ? ccFant.Area : "SIN ÁREA";
                empleados[keyEmp] = new EventosSobretiempoEmpleadoDto
                {
                    Ano = key.Ano, Mes = key.Mes, Area = areaResuelta, GranCcostoDesc = areaResuelta,
                    CodEmpleado = key.CodSpring, NomEmpleado = key.CodSpring,
                };
            }
        }

        // Mismo criterio, para HE Banco (heBancoEmpAcc, 2a): un empleado puede tener horas
        // de banco un mes sin tener NADA de HE Evento/Necesidad ese mismo mes (no comparten
        // las mismas llaves necesariamente) — sin este bloque esas filas "fantasma" nunca
        // se crearían y las horas de banco quedarían invisibles en el drill-down.
        foreach (var key in heBancoEmpAcc.Keys)
        {
            var keyEmp = (key.Ano, key.Mes, key.CodSpring);
            if (!empleados.ContainsKey(keyEmp))
            {
                var areaResuelta = ccostoPorEmpleado.TryGetValue(key.CodSpring, out var ccFant) ? ccFant.Area : "SIN ÁREA";
                empleados[keyEmp] = new EventosSobretiempoEmpleadoDto
                {
                    Ano = key.Ano, Mes = key.Mes, Area = areaResuelta, GranCcostoDesc = areaResuelta,
                    CodEmpleado = key.CodSpring, NomEmpleado = key.CodSpring,
                };
            }
        }

        // Situación laboral por empleado: solo para mostrar FecIngreso/FecCese/badge
        // "Cesado" en el drill-down — la exclusión de cesados ANTES del (ano, mes) ya
        // ocurre arriba, al filtrar "resumen" dentro del foreach por período (así
        // Áreas/Consolidado/Empleados quedan consistentes). El .Where de abajo queda
        // como resguardo defensivo (no debería filtrar nada adicional).
        foreach (var emp in empleados.Values)
        {
            // Puesto se muestra siempre (cualquier Gran Centro de Costo); el algoritmo de
            // Sub Centro de Costo (agrupar por especialidad del Puesto) sigue restringido
            // al Gran Centro de Costo real MANTENIMIENTO de ESTA fila (emp.GranCcostoDesc,
            // Ano/Mes), no al Gran Centro de Costo ACTUAL del empleado (cc.GranCcosto) —
            // evita que un empleado que HOY pertenece a Mantenimiento pero ese período
            // apareció en otra Área (ej. por evento) desagregue por puesto esa otra Área.
            if (ccostoPorEmpleado.TryGetValue(emp.CodEmpleado, out var cc))
            {
                emp.Puesto      = cc.Puesto;
                // cc existe (está en AQUARIUS.PLA_PERSONAL, de ahí el "SIN ÁREA" vía COALESCE)
                // pero puede no tener NINGÚN centro de costo (cc.CentroCostoDesc null) — mismo
                // fallback que el "else" de abajo, si no vm.CentrosCosto agrupa bajo "SIN CENTRO
                // DE COSTO" pero emp.CentroCosto queda null y el filtro e.centroCosto === valor
                // nunca matchea (mostraba "SIN ÁREA" con HE > 0 pero 0 empleados al hacer clic).
                emp.CentroCosto = cc.CentroCostoDesc ?? "SIN CENTRO DE COSTO";
                // Normaliza GranCcostoDesc con el MISMO fallback que usa el GroupBy de
                // vm.CentrosCosto más abajo (antes solo vivía ahí): el drill-down Centro de
                // Costo → Empleado filtraba en el cliente SOLO por centroCosto (sin Área), así
                // que un empleado con la misma descripción de Centro de Costo pero contado ese
                // período bajo OTRA Área (ej. por evento) se sumaba igual en "Detalle por
                // Empleado" aunque vm.CentrosCosto no lo contara en esa fila — el Total de
                // Centro de Costo no cuadraba con el Total de Detalle por Empleado.
                if (string.IsNullOrEmpty(emp.GranCcostoDesc))
                    emp.GranCcostoDesc = cc.Area;
            }
            else
            {
                // Sin mapeo AQUARIUS.PLA_PERSONAL/CENTRO_DE_COSTOS para este empleado (cae en
                // "SIN ÁREA", ver ResolverAreaEvento): se fija el mismo fallback textual que
                // usa vm.CentrosCosto más abajo, para que el drill-down Centro de Costo →
                // Empleado encuentre coincidencia (antes emp.CentroCosto quedaba null y el
                // filtro e.centroCosto === valor nunca matcheaba, mostrando "SIN ÁREA" con
                // HE Evento > 0 pero sin empleados al hacer clic).
                emp.CentroCosto = "SIN CENTRO DE COSTO";
                if (string.IsNullOrEmpty(emp.GranCcostoDesc))
                    emp.GranCcostoDesc = emp.Area;
            }
            if (string.Equals(emp.GranCcostoDesc, "MANTENIMIENTO", StringComparison.OrdinalIgnoreCase))
                emp.SubCentroCosto = ResolverSubCentroCosto(emp.Puesto);

            // Clasificación HE Evento/Necesidad (v2.1, 14/08/2026) — mismo criterio que Área/
            // Centro de Costo (heEmpAcc ya viene de la MISMA partición por día que heAreaAcc,
            // ver bloque "2b"), aplicado sobre HorasHe/TotalHorasExtras propios del empleado.
            // v2.4 (20/08/2026) — FIX "HE Evento + HE Necesidad no cuadra con HE total": si el
            // empleado tiene HorasHe/TotalHorasExtras (SIG) pero AQUARIUS no tiene NINGÚN
            // registro de asistencia ese mes (heEmpAcc sin entrada — ej. empleado migró de
            // centro de costo entre SIG/AQUARIUS, o su tareo no llegó ese mes), antes el "if"
            // simplemente no clasificaba nada y esas horas/soles quedaban fuera de Evento Y de
            // Necesidad (desaparecían del desglose aunque sí contaban en el total HE mostrado
            // — confirmado con COD_SPRING 034645/P740 jul-2026: 16h SIG, 0 registros AQUARIUS).
            // Sin evidencia de evento, el default seguro es "por Necesidad" (nunca se asume
            // cobertura sin evidencia), igual que ya hace RepartirHorasHe/RepartirMontoHe
            // cuando SÍ hay heEmpAcc pero su suma es 0.
            // v2.5 (14/08/2026) — INTERFACE_ASSITIME (AQUARIUS) empuja HORAEXOFI1/2/HORADOBLESOF
            // día a día hacia SIG.INGRE_PLA.VALOR_ORI como un TOTAL de período sin fecha (ver
            // memoria repo). Si no hay ningún día en AQUARIUS (heEmpAcc ausente o su suma es 0),
            // ese HorasHe SIG ya se pagó en planilla pero no se puede ubicar en un día real — se
            // marca como informativo (HorasHeSinEvidencia) sin alterar el reparto Evento/Necesidad
            // ya existente (sigue yendo 100% a Necesidad, como hasta ahora).
            if (heEmpAcc.TryGetValue((emp.Ano, emp.Mes, emp.CodEmpleado), out var accEmp))
            {
                (emp.HorasHeEvento, emp.HorasHeNecesidad) = RepartirHorasHe(emp.HorasHe, accEmp.Evento, accEmp.Necesidad);
                (emp.MontoHeEvento, emp.MontoHeNecesidad) = RepartirMontoHe(emp.TotalHorasExtras, accEmp.Evento, accEmp.Necesidad);
                if (accEmp.Evento + accEmp.Necesidad <= 0) emp.HorasHeSinEvidencia = emp.HorasHe;
            }
            else
            {
                emp.HorasHeNecesidad = emp.HorasHe;
                emp.MontoHeNecesidad = emp.TotalHorasExtras;
                emp.HorasHeSinEvidencia = emp.HorasHe;
            }

            // HE Banco/Compensación (2a) — puramente aditivo, independiente de la
            // clasificación Evento/Necesidad de arriba (no la altera ni la reemplaza).
            if (heBancoEmpAcc.TryGetValue((emp.Ano, emp.Mes, emp.CodEmpleado), out var horasBanco))
                emp.HorasHeBanco = horasBanco;

            if (!estadoPorEmpleado.TryGetValue(emp.CodEmpleado, out var est)) continue;
            emp.FecIngreso = est.FecIngreso;
            emp.FecCese    = est.FecCese;
            if (est.FecCese is DateTime fc)
            {
                var inicioMes = new DateTime(emp.Ano, emp.Mes, 1);
                emp.CesadoEnPeriodo = fc >= inicioMes && fc < inicioMes.AddMonths(1);
            }
        }

        vm.Empleados = empleados.Values
            .Where(e => e.FecCese is null || e.CesadoEnPeriodo || e.FecCese >= new DateTime(e.Ano, e.Mes, 1).AddMonths(1))
            .OrderBy(e => e.Ano).ThenBy(e => e.Mes).ThenBy(e => e.Area)
            .ThenByDescending(e => e.TotalHorasExtras).ToList();

        // Nivel intermedio Gran Centro de Costo → Centro de Costo: se arma siempre (aún si
        // ya se filtró por un Gran Centro de Costo puntual) agrupando el detalle por
        // empleado (vm.Empleados) según el Gran Centro de Costo/Centro de Costo real de
        // cada empleado (ccostoPorEmpleado), usado por el drill-down interactivo en el
        // cliente cuando el filtro Gran Centro de Costo = "Todos" (ver _KpiDashboard.cshtml).
        vm.CentrosCosto = vm.Empleados
            // Si el usuario seleccionó un Centro de Costo puntual (código real, no texto),
            // este nivel intermedio debe restringirse a ESE Centro de Costo únicamente —
            // antes se ignoraba este filtro para poder mostrar siempre los 3 niveles cuando
            // Gran Centro de Costo = "Todos", pero eso hacía que, al filtrar por un Centro de
            // Costo específico (ej. MANTENIMIENTO TINTORERIA), igual aparecieran los demás
            // Centros de Costo del mismo Gran Centro de Costo (ej. MANTENIMIENTO HILANDERIA).
            .Where(e => centroCosto is null
                || (ccostoPorEmpleado.TryGetValue(e.CodEmpleado, out var ccSel) && ccSel.CentroCosto == centroCosto))
            .GroupBy(e =>
            {
                // No se excluye ni se indexa directamente ccostoPorEmpleado[e.CodEmpleado]:
                // empleados sin mapeo ("SIN ÁREA") deben seguir apareciendo acá también,
                // agrupados bajo (SIN ÁREA, SIN CENTRO DE COSTO), igual que emp.CentroCosto
                // (ver fallback arriba) — antes se filtraban con .Where(ContainsKey) y un
                // indexer directo que además hubiera lanzado KeyNotFoundException.
                var tieneCc = ccostoPorEmpleado.TryGetValue(e.CodEmpleado, out var cc);
                // Usa el Gran Centro de Costo REAL de esta fila (e.GranCcostoDesc) y no el
                // ACTUAL del empleado (cc.Area) para no desalinear el desglose cuando el
                // empleado cambió de Gran Centro de Costo entre el período consultado y hoy.
                var granCcostoDesc = !string.IsNullOrEmpty(e.GranCcostoDesc) ? e.GranCcostoDesc : (tieneCc ? cc.Area : e.Area);
                var centroCostoDesc = tieneCc ? (cc.CentroCostoDesc ?? "SIN CENTRO DE COSTO") : "SIN CENTRO DE COSTO";
                return (e.Ano, e.Mes, GranCcosto: granCcostoDesc, CentroCosto: centroCostoDesc);
            })
            .Select(g => new EventosSobretiempoCentroCostoMesDto
            {
                Ano                   = g.Key.Ano,
                Mes                   = g.Key.Mes,
                GranCcosto            = g.Key.GranCcosto,
                CentroCosto           = g.Key.CentroCosto,
                TotalTrabajadores     = g.Select(e => e.CodEmpleado).Distinct().Count(),
                HorasProduccion       = g.Sum(e => e.HorasProduccion),
                MontoProduccion       = g.Sum(e => e.MontoProduccion),
                TotalHorasExtras      = g.Sum(e => e.TotalHorasExtras),
                HorasHe               = g.Sum(e => e.HorasHe),
                He25                  = g.Sum(e => e.He25),
                He35                  = g.Sum(e => e.He35),
                He100                 = g.Sum(e => e.He100),
                TrabajadoresConEvento = g.Count(e => e.DiasEvento > 0),
                DiasEvento            = g.Sum(e => e.DiasEvento),
                // v2.1 (14/08/2026): ya NO se calcula independientemente vía heCcAcc — se
                // SUMA directo del detalle por Empleado (fuente de verdad, ver heEmpAcc más
                // arriba) para que Centro de Costo cuadre siempre exacto con Empleado.
                HorasHeEvento         = g.Sum(e => e.HorasHeEvento),
                HorasHeNecesidad      = g.Sum(e => e.HorasHeNecesidad),
                MontoHeEvento         = g.Sum(e => e.MontoHeEvento),
                MontoHeNecesidad      = g.Sum(e => e.MontoHeNecesidad),
                HorasHeBanco          = g.Sum(e => e.HorasHeBanco),
            })
            .OrderBy(c => c.Ano).ThenBy(c => c.Mes).ThenBy(c => c.GranCcosto).ThenBy(c => c.CentroCosto)
            .ToList();

        // El reparto Evento/Necesidad de Área (heAreaAcc, arriba) usa un ratio "pooled" sobre
        // TODOS los Centro de Costo de esa Área, mientras el de Centro de Costo (heCcAcc) usa
        // el ratio propio de cada uno aplicado a su propio HorasHe/TotalHorasExtras oficial —
        // son 2 repartos matemáticamente independientes del MISMO total y casi nunca coinciden
        // (promedio ponderado no es asociativo entre niveles), por eso el Total de Centro de
        // Costo no cuadraba con la fila de Área (ver captura: Área HE Evento=566.8/Necesidad=93.2
        // vs suma de sus Centro de Costo=534.8/125.2, aunque el HE(h) total sí coincidía).
        // Se sobreescribe el valor de Área con la SUMA real de sus Centro de Costo para que el
        // drill-down Área → Centro de Costo cuadre siempre exacto (Centro de Costo es el nivel
        // más fino que tiene este desglose, así que es la fuente de verdad).
        foreach (var grupo in vm.CentrosCosto.GroupBy(c => (c.Ano, c.Mes, c.GranCcosto)))
        {
            if (filas.TryGetValue((grupo.Key.Ano, grupo.Key.Mes, grupo.Key.GranCcosto), out var filaArea))
            {
                filaArea.HorasHeEvento    = grupo.Sum(c => c.HorasHeEvento);
                filaArea.HorasHeNecesidad = grupo.Sum(c => c.HorasHeNecesidad);
                filaArea.MontoHeEvento    = grupo.Sum(c => c.MontoHeEvento);
                filaArea.MontoHeNecesidad = grupo.Sum(c => c.MontoHeNecesidad);
                filaArea.HorasHeBanco     = grupo.Sum(c => c.HorasHeBanco);
            }
        }

        // ── 4) Resumen global por (Año, Mes) ─────────────────────────────────
        vm.Resumen = vm.Areas
            .GroupBy(f => (f.Ano, f.Mes))
            .Select(g => new EventosSobretiempoResumenMesDto
            {
                Ano                   = g.Key.Ano,
                Mes                   = g.Key.Mes,
                HorasProduccion       = g.Sum(x => x.HorasProduccion),
                MontoProduccion       = g.Sum(x => x.MontoProduccion),
                TotalHorasExtras      = g.Sum(x => x.TotalHorasExtras),
                HorasHe               = g.Sum(x => x.HorasHe),
                He25                  = g.Sum(x => x.He25),
                He35                  = g.Sum(x => x.He35),
                He100                 = g.Sum(x => x.He100),
                TotalTrabajadores     = g.Sum(x => x.TotalTrabajadores),
                TrabajadoresConEvento = g.Sum(x => x.TrabajadoresConEvento),
                DiasEvento            = g.Sum(x => x.DiasEvento),
                HorasHeEvento         = g.Sum(x => x.HorasHeEvento),
                HorasHeNecesidad      = g.Sum(x => x.HorasHeNecesidad),
                MontoHeEvento         = g.Sum(x => x.MontoHeEvento),
                MontoHeNecesidad      = g.Sum(x => x.MontoHeNecesidad),
                HorasHeBanco          = g.Sum(x => x.HorasHeBanco),
            })
            .OrderBy(r => r.Ano).ThenBy(r => r.Mes)
            .ToList();

        // ── 5) Consolidado de eventos (tabla final, cantidad de empleados por tipo) ──
        vm.ConsolidadoEventos = consolidado
            .Select(kv => new EventosSobretiempoConsolidadoDto
            {
                TipoEvento        = kv.Key,
                CantidadEmpleados = kv.Value.Empleados.Count,
                TotalDias         = kv.Value.TotalDias,
            })
            .OrderByDescending(c => c.TotalDias)
            .ToList();

        // ── 6) Proyección de Bolsa de HE por Área — promedio mensual de HE "por
        // Necesidad" en el rango consultado (referencia de bolsa mensual asignable por el
        // encargado de área, separada del HE "por Evento" que depende de cuántas
        // ausencias haya cada mes y no es proyectable de la misma forma).
        vm.ProyeccionBolsaHe = vm.Areas
            .GroupBy(f => f.Area)
            .Select(g =>
            {
                var meses = g.Select(f => (f.Ano, f.Mes)).Distinct().Count();
                return new ProyeccionBolsaHeDto
                {
                    Area                 = g.Key,
                    MesesConsiderados    = meses,
                    HorasHeNecesidadProm = Math.Round(g.Sum(f => f.HorasHeNecesidad) / meses, 2),
                    MontoHeNecesidadProm = Math.Round(g.Sum(f => f.MontoHeNecesidad) / meses, 2),
                    HorasHeEventoProm    = Math.Round(g.Sum(f => f.HorasHeEvento) / meses, 2),
                    MontoHeEventoProm    = Math.Round(g.Sum(f => f.MontoHeEvento) / meses, 2),
                };
            })
            .OrderByDescending(p => p.HorasHeNecesidadProm)
            .ToList();

        // v2.5 (20/08/2026) — misma proyección pero por (Gran Centro de Costo, Centro de
        // Costo), a pedido del usuario (le faltaba este nivel además de Área). Se arma
        // sobre vm.CentrosCosto (ya cuadra exacto con Empleado, ver bloque 4).
        vm.ProyeccionBolsaHeCentroCosto = vm.CentrosCosto
            .GroupBy(c => (c.GranCcosto, c.CentroCosto))
            .Select(g =>
            {
                var meses = g.Select(c => (c.Ano, c.Mes)).Distinct().Count();
                return new ProyeccionBolsaHeCentroCostoDto
                {
                    GranCcosto           = g.Key.GranCcosto,
                    CentroCosto          = g.Key.CentroCosto,
                    MesesConsiderados    = meses,
                    HorasHeNecesidadProm = Math.Round(g.Sum(c => c.HorasHeNecesidad) / meses, 2),
                    MontoHeNecesidadProm = Math.Round(g.Sum(c => c.MontoHeNecesidad) / meses, 2),
                    HorasHeEventoProm    = Math.Round(g.Sum(c => c.HorasHeEvento) / meses, 2),
                    MontoHeEventoProm    = Math.Round(g.Sum(c => c.MontoHeEvento) / meses, 2),
                };
            })
            .OrderByDescending(p => p.HorasHeNecesidadProm)
            .ToList();

        // v2.5 (20/08/2026) — detalle día a día por empleado (auditoría/desagregación),
        // solo para empleados que quedaron en el resultado final (vm.Empleados).
        var codsEnResultado = vm.Empleados.Select(e => e.CodEmpleado).ToHashSet();
        vm.DetalleDiarioHe = detalleDiarioHe
            .Where(d => codsEnResultado.Contains(d.CodEmpleado))
            .OrderBy(d => d.CodEmpleado).ThenBy(d => d.Fecha)
            .ToList();

        return vm;
    }

    // Descripciones del Gran Centro de Costo / Centro de Costo filtrados, para el título
    // del dashboard ("Gran Centro de Costo: X / Centro de Costo: Y") — SIG.V_CENTRO_DE_COSTOS.
    // granCcosto acepta una lista de códigos separados por coma (filtro múltiple de
    // Áreas, checkboxes tipo HorasExtras) — arma un label conjunto ("MANTENIMIENTO,
    // TINTORERÍA") con las descripciones de TODOS los códigos seleccionados.
    private async Task<(string? GranLabel, string? CentroLabel)> ResolverLabelsCcostoAsync(string? granCcosto, string? centroCosto)
    {
        var granLabels = new List<string>();
        string? centroLabel = null;
        await using var conn = new OracleConnection(_connStrSig);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.BindByName  = true;
        cmd.CommandText = @"
SELECT DISTINCT desc_gran_ccosto, desc_ccosto_det, gran_ccosto, ccosto_det
FROM   V_CENTRO_DE_COSTOS
WHERE  (:granCcosto  IS NOT NULL AND ','||:granCcosto||',' LIKE '%,'||gran_ccosto||',%')
    OR (:centroCosto IS NOT NULL AND ccosto_det  = :centroCosto)";
        cmd.Parameters.Add("granCcosto",  OracleDbType.Varchar2).Value = (object?)granCcosto  ?? DBNull.Value;
        cmd.Parameters.Add("centroCosto", OracleDbType.Varchar2).Value = (object?)centroCosto ?? DBNull.Value;
        var codigosGran = granCcosto?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet() ?? new HashSet<string>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var codGran = r["gran_ccosto"]?.ToString()?.Trim();
            if (codGran != null && codigosGran.Contains(codGran))
            {
                var desc = r["desc_gran_ccosto"]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(desc)) granLabels.Add(desc);
            }
            if (centroCosto != null && r["ccosto_det"]?.ToString()?.Trim() == centroCosto) centroLabel = r["desc_ccosto_det"]?.ToString()?.Trim();
        }
        return (granLabels.Count > 0 ? string.Join(", ", granLabels.Distinct().OrderBy(x => x)) : null, centroLabel);
    }

    // Patrón ya validado en SP_AQ_PROYECCION_ASISTENCIA (ver
    // /memories/repo/proyeccion-asistencia-horas-ccosto-31072026.md): PLA_PERSONAL.
    // COD_C_COSTOS = SIG.CENTRO_DE_COSTOS.CENTRO_COSTO (ESTADO<>'9') -> GRAN_CCOSTO
    // -> SIG.TABLAS_AUXILIARES(TIPO=83) -> DESCRIPCION. Dedup por ROW_NUMBER porque
    // PLA_PERSONAL puede tener 2 COD_PERSONAL distintos para el mismo COD_SPRING.
    // Fallback: si COD_C_COSTOS no resuelve (ej. '000'/placeholder, no existe en
    // SIG.CENTRO_DE_COSTOS), se usa el centro de costo más reciente de SIG.PLA_COSTO
    // (mismo origen que SP_RESUMEN_AREA/SP_DETALLE_SOBRETIEMPO) — validado con MCP para
    // COD_SPRING='038155' (AQUARIUS devolvía cod_c_costos='000' inexistente en SIG, pero
    // SIG.PLA_COSTO sí tiene su centro de costo real). Corre en conexión SIG porque ya
    // está probado que SIG puede leer AQUARIUS.PLA_PERSONAL (grants existentes).
    // Además del nombre descriptivo del área (Gran Centro de Costo), expone también los
    // CÓDIGOS de Gran Centro de Costo y Centro de Costo (V_CENTRO_DE_COSTOS.GRAN_CCOSTO/
    // CCOSTO_DET) para poder filtrar el lado AQUARIUS ("resumen") por el mismo criterio
    // jerárquico que ya filtra el lado SIG (P_GRAN_CCOSTO/P_CENTRO_COSTO del package).
    private async Task<Dictionary<string, (string Area, string? GranCcosto, string? CentroCosto, string? CentroCostoDesc, string? Puesto)>> ResolverCcostoPorEmpleadoAsync(string codEmpresa)
    {
        var dict = new Dictionary<string, (string, string?, string?, string?, string?)>();
        await using var conn = new OracleConnection(_connStrSig);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.BindByName  = true;
        cmd.CommandText = @"
SELECT cod_spring, area, gran_ccosto, centro_costo, centro_costo_desc, desc_cargo FROM (
  SELECT p.cod_spring,
         COALESCE(ta.descripcion, fb.desc_gran_ccosto, 'SIN ÁREA') AS area,
         COALESCE(cc.gran_ccosto, fb.gran_ccosto)                  AS gran_ccosto,
         COALESCE(cc.centro_costo, fb.ccosto_det)                  AS centro_costo,
         COALESCE(cc.nombre, fb.desc_ccosto_det)                   AS centro_costo_desc,
         tc.descripcion                                            AS desc_cargo,
         ROW_NUMBER() OVER (PARTITION BY p.cod_spring ORDER BY p.cod_personal DESC) rn
  FROM AQUARIUS.PLA_PERSONAL p
  LEFT JOIN CENTRO_DE_COSTOS cc ON cc.centro_costo = p.cod_c_costos AND cc.estado <> '9'
  LEFT JOIN TABLAS_AUXILIARES ta ON ta.tipo = 83 AND ta.codigo = cc.gran_ccosto
  LEFT JOIN V_PERSONAL vp ON vp.c_codigo = p.cod_spring
  LEFT JOIN T_CARGO tc ON tc.c_cargo = vp.c_cargo
  LEFT JOIN (
    SELECT c.c_codigo, y.desc_gran_ccosto, y.gran_ccosto, y.ccosto_det, y.desc_ccosto_det,
           ROW_NUMBER() OVER (PARTITION BY c.c_codigo ORDER BY c.num_pla DESC) rn
    FROM PLA_COSTO c
    JOIN V_CENTRO_DE_COSTOS y ON y.ccosto_det = c.c_costo
  ) fb ON fb.c_codigo = p.cod_spring AND fb.rn = 1
  WHERE p.cod_empresa = :codEmpresa AND p.cod_spring IS NOT NULL
) WHERE rn = 1";
        cmd.Parameters.Add("codEmpresa", OracleDbType.Varchar2).Value = codEmpresa;
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var cod = r["cod_spring"]?.ToString()?.Trim();
            if (!string.IsNullOrEmpty(cod))
                dict[cod] = (
                    r["area"]?.ToString()?.Trim() ?? "SIN ÁREA",
                    r["gran_ccosto"]?.ToString()?.Trim(),
                    r["centro_costo"]?.ToString()?.Trim(),
                    r["centro_costo_desc"]?.ToString()?.Trim(),
                    r["desc_cargo"]?.ToString()?.Trim());
        }
        return dict;
    }

    private static readonly HashSet<string> _stopWordsPuesto = new(StringComparer.OrdinalIgnoreCase) { "DE", "DEL", "LA", "EL", "Y" };

    // Sinónimos/variantes que deben agruparse bajo un mismo Sub Centro de Costo, ej.
    // "TECNICO DE PROYECTO" y "MECANICO PROYECTOS" -> PROYECTO (singular/plural), o
    // términos equivalentes con distinta redacción. Clave y valor ya normalizados
    // (sin tildes, mayúsculas) — ver NormalizarPalabra.
    private static readonly Dictionary<string, string> _sinonimosSubCentroCosto = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PROYECTOS"] = "PROYECTO",
    };

    // Sub Centro de Costo presentacional (solo vistas, no se persiste): última palabra
    // significativa del Puesto (ignora preposiciones), normalizada (sin tildes, singular,
    // sinónimos) para agrupar variantes equivalentes, ej. "MECANICO PREPARATORIA" y
    // "PREPARATORIA" -> PREPARATORIA; "TECNICO DE PROYECTO" y "MECANICO PROYECTOS" ->
    // PROYECTO; "AYUDANTE DE MECANICO AUTOCONER" -> AUTOCONER; "ELECTRICISTA" y "TECNICO
    // ELECTRICISTA" -> ELECTRICISTA.
    private static string? ResolverSubCentroCosto(string? puesto)
    {
        if (string.IsNullOrWhiteSpace(puesto)) return null;
        var palabras = puesto.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !_stopWordsPuesto.Contains(w)).ToList();
        if (palabras.Count == 0) return "VARIOS";
        return NormalizarPalabraSubCentroCosto(palabras[^1]);
    }

    // Quita tildes/diacríticos, pasa a may. y singulariza (quita "S" final salvo palabras
    // cortas, para no romper términos de 3 letras o menos), aplicando luego el diccionario
    // de sinónimos explícitos.
    private static string NormalizarPalabraSubCentroCosto(string palabra)
    {
        var sinTildes = new string(palabra.Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray()).ToUpperInvariant();

        var singular = sinTildes.Length > 3 && sinTildes.EndsWith('S') ? sinTildes[..^1] : sinTildes;

        return _sinonimosSubCentroCosto.TryGetValue(singular, out var canonico) ? canonico : singular;
    }


    // Catálogo Gran Centro de Costo (filtro del reporte) — SIG.V_CENTRO_DE_COSTOS.
    public async Task<List<GranCcostoOptionDto>> GetGranCcostoOptionsAsync()
    {
        var lista = new List<GranCcostoOptionDto>();
        await using var conn = new OracleConnection(_connStrSig);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        // Solo Gran Centro de Costo con actividad reciente en PLA_COSTO (últimos 24 meses):
        // evita listar grupos cuyo único Centro de Costo hijo con personal dejó de operar
        // hace años (ej. ALMACEN SUMINISTROS, sin ningún registro desde enero/2014).
        cmd.CommandText = @"
SELECT DISTINCT y.gran_ccosto, y.desc_gran_ccosto
FROM   V_CENTRO_DE_COSTOS y
WHERE  EXISTS (
         SELECT 1 FROM PLA_COSTO c JOIN PARAMPLA x ON x.num_pla = c.num_pla
         WHERE  c.c_costo = y.ccosto_det AND x.tipo_pla = 'N'
           AND  (x.ano*100+x.mes) >= (EXTRACT(YEAR FROM ADD_MONTHS(SYSDATE,-24))*100
                                       + EXTRACT(MONTH FROM ADD_MONTHS(SYSDATE,-24)))
       )
ORDER  BY y.desc_gran_ccosto";
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            lista.Add(new GranCcostoOptionDto
            {
                Codigo      = r["gran_ccosto"]?.ToString()?.Trim() ?? "",
                Descripcion = r["desc_gran_ccosto"]?.ToString()?.Trim() ?? "",
            });
        }
        return lista;
    }

    // Catálogo Centro de Costo (filtro del reporte, agrupado por Gran Centro de Costo
    // en el <select> vía DescGranCcosto) — SIG.V_CENTRO_DE_COSTOS.
    public async Task<List<CentroCostoOptionDto>> GetCentroCostoOptionsAsync()
    {
        var lista = new List<CentroCostoOptionDto>();
        await using var conn = new OracleConnection(_connStrSig);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        // Solo Centro de Costo con actividad reciente en PLA_COSTO (últimos 24 meses) — hay
        // códigos en V_CENTRO_DE_COSTOS (ej. ALMACEN SUMINISTROS) que tuvieron personal hace
        // más de una década (último registro ene/2014) pero ninguno en el rango de fechas que
        // realmente puede consultar el reporte, y por eso nunca aparecen en la tabla aunque
        // sigan en el filtro.
        cmd.CommandText = @"
SELECT DISTINCT y.ccosto_det, y.desc_ccosto_det, y.gran_ccosto, y.desc_gran_ccosto
FROM   V_CENTRO_DE_COSTOS y
WHERE  EXISTS (
         SELECT 1 FROM PLA_COSTO c JOIN PARAMPLA x ON x.num_pla = c.num_pla
         WHERE  c.c_costo = y.ccosto_det AND x.tipo_pla = 'N'
           AND  (x.ano*100+x.mes) >= (EXTRACT(YEAR FROM ADD_MONTHS(SYSDATE,-24))*100
                                       + EXTRACT(MONTH FROM ADD_MONTHS(SYSDATE,-24)))
       )
ORDER  BY y.desc_gran_ccosto, y.desc_ccosto_det";
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            lista.Add(new CentroCostoOptionDto
            {
                Codigo         = r["ccosto_det"]?.ToString()?.Trim() ?? "",
                Descripcion    = r["desc_ccosto_det"]?.ToString()?.Trim() ?? "",
                GranCcosto     = r["gran_ccosto"]?.ToString()?.Trim() ?? "",
                DescGranCcosto = r["desc_gran_ccosto"]?.ToString()?.Trim() ?? "",
            });
        }
        return lista;
    }

    // SIG.V_PERSONAL (SITUACION=1 => activo) — fuente autoritativa de vigencia: a
    // diferencia de AQUARIUS.PLA_PERSONAL (que puede tener 2+ COD_PERSONAL/reingresos
    // por COD_SPRING), V_PERSONAL ya viene 1 fila por C_CODIGO/COD_SPRING, sin dedup.
    private async Task<Dictionary<string, (DateTime? FecIngreso, DateTime? FecCese)>> ResolverEstadoPorEmpleadoAsync()
    {
        var dict = new Dictionary<string, (DateTime?, DateTime?)>();
        await using var conn = new OracleConnection(_connStrSig);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT c_codigo, situacion, f_ingreso, f_cese FROM V_PERSONAL";
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var cod = r["c_codigo"]?.ToString()?.Trim();
            if (string.IsNullOrEmpty(cod)) continue;
            var vigente    = r["situacion"]?.ToString()?.Trim() == "1";
            var fecIngreso = r["f_ingreso"] is DateTime fi ? fi : (DateTime?)null;
            var fecCese    = r["f_cese"] is DateTime fc ? fc : (DateTime?)null;
            // Si SITUACION indica cesado pero no hay F_CESE (hueco de dato), se ignora
            // el registro para no filtrar de más — se prefiere mostrar antes que ocultar.
            dict[cod] = (fecIngreso, vigente ? null : fecCese);
        }
        return dict;
    }

    // AQUARIUS.PLA_PERSONAL.cod_personal más reciente por COD_SPRING (contrato/registro
    // vigente) — se usa para descartar tareo huérfano enganchado a un contrato antiguo
    // (ver comentario en el foreach de "resumen" dentro de ObtenerKpiAsync).
    private async Task<Dictionary<string, string>> ResolverCodPersonalActivoAsync(string codEmpresa)
    {
        var dict = new Dictionary<string, string>();
        await using var conn = new OracleConnection(_connStrSig);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.BindByName  = true;
        cmd.CommandText = @"
SELECT cod_spring, cod_personal FROM (
  SELECT p.cod_spring, p.cod_personal,
         ROW_NUMBER() OVER (PARTITION BY p.cod_spring ORDER BY p.fec_ingreso DESC NULLS LAST, p.cod_personal DESC) rn
  FROM AQUARIUS.PLA_PERSONAL p
  WHERE p.cod_empresa = :codEmpresa AND p.cod_spring IS NOT NULL
) WHERE rn = 1";
        cmd.Parameters.Add("codEmpresa", OracleDbType.Varchar2).Value = codEmpresa;
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var cod = r["cod_spring"]?.ToString()?.Trim();
            if (!string.IsNullOrEmpty(cod))
                dict[cod] = r["cod_personal"]?.ToString()?.Trim() ?? "";
        }
        return dict;
    }

    // Traduce COD_PERSONAL (AQUARIUS.SCA_ASISTENCIA_TAREO, 1 fila por contrato/reingreso)
    // -> COD_SPRING (identificador de PERSONA, clave de ccostoPorEmpleado y del resto del
    // reporte). A diferencia de ResolverCodPersonalActivoAsync (que solo guarda el
    // cod_personal MÁS RECIENTE por cod_spring, para descartar tareo huérfano), acá se
    // incluyen TODOS los cod_personal históricos de PLA_PERSONAL, porque SCA_ASISTENCIA_TAREO
    // puede traer marcaciones de un contrato antiguo y aun así deben poder resolverse a su
    // área/centro de costo real. Confirmado con MCP (14/08/2026): COD_PERSONAL nunca es igual
    // a COD_SPRING (0 de 615 coincidencias), por eso hacía falta esta traducción explícita.
    private async Task<Dictionary<string, string>> ResolverCodSpringPorCodPersonalAsync(string codEmpresa)
    {
        var dict = new Dictionary<string, string>();
        await using var conn = new OracleConnection(_connStrSig);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.BindByName  = true;
        cmd.CommandText = @"
SELECT cod_personal, cod_spring
FROM   AQUARIUS.PLA_PERSONAL
WHERE  cod_empresa = :codEmpresa AND cod_personal IS NOT NULL AND cod_spring IS NOT NULL";
        cmd.Parameters.Add("codEmpresa", OracleDbType.Varchar2).Value = codEmpresa;
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var codPersonal = r["cod_personal"]?.ToString()?.Trim();
            if (!string.IsNullOrEmpty(codPersonal))
                dict[codPersonal] = r["cod_spring"]?.ToString()?.Trim() ?? "";
        }
        return dict;
    }

    // Eventos crudos de Falta Logix (SIG.RH_EVENTOS, C_TIPO='07') que se solapan con TODO
    // el rango consultado — UNA sola ida a Oracle para todo el reporte (optimización: antes
    // ResolverFaltasLogixAsync se llamaba una vez POR MES dentro del foreach de
    // ObtenerKpiAsync, un round-trip por mes con la misma forma de query). El corte por mes
    // puntual se hace en memoria vía RecortarFaltasLogixPorMes. D_FINAL puede ser NULL
    // (evento abierto): se usa D_INICIO como fin también en ese caso.
    private async Task<List<(string Cod, DateTime DIni, DateTime DFin)>> ResolverEventosFaltaLogixAsync(DateTime fechaInicio, DateTime fechaFinal)
    {
        var eventos = new List<(string, DateTime, DateTime)>();
        await using var conn = new OracleConnection(_connStrSig);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.BindByName  = true;
        cmd.CommandText = @"
SELECT c_codigo, d_inicio, d_final
FROM   RH_EVENTOS
WHERE  c_tipo = '07'
  AND  d_inicio <= :fechaFinal AND NVL(d_final, d_inicio) >= :fechaInicio";
        cmd.Parameters.Add("fechaFinal",  OracleDbType.Date).Value = fechaFinal;
        cmd.Parameters.Add("fechaInicio", OracleDbType.Date).Value = fechaInicio;
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var cod = r["c_codigo"]?.ToString()?.Trim();
            if (string.IsNullOrEmpty(cod)) continue;
            var dIni = Convert.ToDateTime(r["d_inicio"]).Date;
            var dFin = r["d_final"] is DBNull ? dIni : Convert.ToDateTime(r["d_final"]).Date;
            eventos.Add((cod, dIni, dFin));
        }
        return eventos;
    }

    // Recorta (en memoria, sin ida a Oracle) los eventos de Falta Logix de todo el rango al
    // solape con un mes puntual [fechaInicio, fechaFinal] — mismo cálculo LEAST/GREATEST que
    // antes hacía el SUM en SQL dentro de ResolverFaltasLogixAsync (ya no se llama por mes).
    private static Dictionary<string, int> RecortarFaltasLogixPorMes(
        List<(string Cod, DateTime DIni, DateTime DFin)> eventos, DateTime fechaInicio, DateTime fechaFinal)
    {
        var dict = new Dictionary<string, int>();
        foreach (var (cod, dIni, dFin) in eventos)
        {
            if (dIni > fechaFinal || dFin < fechaInicio) continue;
            var desde = dIni < fechaInicio ? fechaInicio : dIni;
            var hasta = dFin > fechaFinal ? fechaFinal : dFin;
            var dias  = (hasta - desde).Days + 1;
            if (dias <= 0) continue;
            dict[cod] = dict.TryGetValue(cod, out var acc) ? acc + dias : dias;
        }
        return dict;
    }

    // D\u00edas de "Falta" del mes por empleado, tomados del evento formal Logix (SIG.RH_EVENTOS,
    // C_TIPO='07') en vez de las marcaciones AQUARIUS \u2014 mismo c\u00e1lculo de solape de d\u00edas
    // (LEAST/GREATEST) que usa SP_SCA_RESUMENTAREO_SIGLIVE para sus dem\u00e1s tipos de evento.
    // NOTA: ya no se llama desde ObtenerKpiAsync (ver ResolverEventosFaltaLogixAsync +
    // RecortarFaltasLogixPorMes arriba); se conserva como utilidad standalone equivalente
    // por si algún otro flujo necesita el total de un rango puntual sin pre-cargar eventos.
    private async Task<Dictionary<string, int>> ResolverFaltasLogixAsync(DateTime fechaInicio, DateTime fechaFinal)
    {
        var dict = new Dictionary<string, int>();
        await using var conn = new OracleConnection(_connStrSig);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.BindByName  = true;
        cmd.CommandText = @"
SELECT c_codigo,
       SUM(LEAST(NVL(d_final, d_inicio), :fechaFinal) - GREATEST(d_inicio, :fechaInicio) + 1) dias
FROM   RH_EVENTOS
WHERE  c_tipo = '07'
  AND  d_inicio <= :fechaFinal AND NVL(d_final, d_inicio) >= :fechaInicio
GROUP  BY c_codigo";
        cmd.Parameters.Add("fechaFinal",  OracleDbType.Date).Value = fechaFinal;
        cmd.Parameters.Add("fechaInicio", OracleDbType.Date).Value = fechaInicio;
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var cod = r["c_codigo"]?.ToString()?.Trim();
            if (!string.IsNullOrEmpty(cod))
                dict[cod] = Convert.ToInt32(r["dias"]);
        }
        return dict;
    }

    // Días EXACTOS (no el total mensual de ResolverFaltasLogixAsync) con Falta formal
    // Logix (SIG.RH_EVENTOS, C_TIPO='07'), por (COD_SPRING, Fecha) — usado en el bloque
    // "2b" de ObtenerKpiAsync para corroborar día a día el TIENE_FALTA_RAW crudo de
    // SP_HE_DIARIO_AQUARIUS antes de contarlo como evento real. HORAS_FALTA
    // (AQUARIUS.SCA_ASISTENCIA_TAREO) se calcula de marcaciones biométricas y queda en
    // "falta" TODOS los días para personal exceptuado de marcar — sin esta corroboración,
    // cualquier Centro de Costo con al menos 1 de esos empleados quedaba con
    // HuboEventoDia=TRUE prácticamente todo el mes, clasificando el 100% de su HE como
    // "por Evento" con 0% "por Necesidad" real (confirmado con datos reales vía MCP,
    // ver PKG_RPT_EVENTOS_SOBRETIEMPO.sql v2.2). El rango [d_inicio, d_final] de cada
    // evento se expande a días individuales acá (no en SQL) porque el volumen es
    // pequeño (eventos de Falta, no todos los tipos) y evita reconsultar Oracle por día.
    private async Task<HashSet<(string CodSpring, DateTime Fecha)>> ResolverFaltasLogixDiarioAsync(DateTime fechaInicio, DateTime fechaFinal)
    {
        var dias = new HashSet<(string, DateTime)>();
        await using var conn = new OracleConnection(_connStrSig);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.BindByName  = true;
        cmd.CommandText = @"
SELECT c_codigo, d_inicio, d_final
FROM   RH_EVENTOS
WHERE  c_tipo = '07'
  AND  d_inicio <= :fechaFinal AND NVL(d_final, d_inicio) >= :fechaInicio";
        cmd.Parameters.Add("fechaFinal",  OracleDbType.Date).Value = fechaFinal;
        cmd.Parameters.Add("fechaInicio", OracleDbType.Date).Value = fechaInicio;
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var cod = r["c_codigo"]?.ToString()?.Trim();
            if (string.IsNullOrEmpty(cod)) continue;
            var dIni = Convert.ToDateTime(r["d_inicio"]).Date;
            var dFin = r["d_final"] is DBNull ? dIni : Convert.ToDateTime(r["d_final"]).Date;
            var desde = dIni < fechaInicio.Date ? fechaInicio.Date : dIni;
            var hasta = dFin > fechaFinal.Date ? fechaFinal.Date : dFin;
            for (var f = desde; f <= hasta; f = f.AddDays(1))
                dias.Add((cod, f));
        }
        return dias;
    }

    // Suma de todos los días de ausencia/evento del resumen mensual de AQUARIUS
    // (SP_SCA_RESUMENTAREO_SIGLIVE) — define qué cuenta como "evento" en este reporte.
    private static int TotalDiasEvento(PlanillaResumenDto e) =>
        e.DiasFalta + e.Vacaciones + e.DescansosMedicos + e.Subsidios + e.AccidenteTrabajo +
        e.SubsidioIncapacidadAccidente + e.SubsidioMaternidad + e.LicenciasSindicales +
        e.Suspensiones + e.PermisoGoceFisico + e.LicenciaPaternidad + e.LicenciaFallecimiento +
        e.DiasPermisoSinGoce + e.DiasPermisoConGoce;

    // "HH:MM" (PlanillaResumenDto.HorasEfectivas, AQUARIUS) -> horas decimales.
    private static decimal ParseHorasHHMM(string? hhmm)
    {
        if (string.IsNullOrWhiteSpace(hhmm)) return 0m;
        var partes = hhmm.Split(':');
        if (partes.Length != 2 || !int.TryParse(partes[0], out var horas) || !int.TryParse(partes[1], out var minutos))
            return 0m;
        return horas + minutos / 60m;
    }

    // Mismas categorías que TotalDiasEvento, mostradas como etiquetas legibles para el
    // drill-down "Detalle por Área" (columna "Detalle Eventos").
    private static IEnumerable<(string Label, int Dias)> CategoriasEvento(PlanillaResumenDto e)
    {
        yield return ("Falta", e.DiasFalta);
        yield return ("Vacaciones", e.Vacaciones);
        yield return ("D. Médico", e.DescansosMedicos);
        yield return ("Subsidio", e.Subsidios);
        yield return ("Acc. Trabajo", e.AccidenteTrabajo);
        yield return ("Sub. Incap. Acc.", e.SubsidioIncapacidadAccidente);
        yield return ("Sub. Maternidad", e.SubsidioMaternidad);
        yield return ("Lic. Sindical", e.LicenciasSindicales);
        yield return ("Suspensión", e.Suspensiones);
        yield return ("Permiso c/Goce Físico", e.PermisoGoceFisico);
        yield return ("Lic. Paternidad", e.LicenciaPaternidad);
        yield return ("Lic. Fallecimiento", e.LicenciaFallecimiento);
        yield return ("Permiso s/Goce", e.DiasPermisoSinGoce);
        yield return ("Permiso c/Goce", e.DiasPermisoConGoce);
    }

    private static IEnumerable<(int Ano, int Mes)> MesesDelRango(int anoIni, int mesIni, int anoFin, int mesFin)
    {
        var cur = new DateTime(anoIni, mesIni, 1);
        var fin = new DateTime(anoFin, mesFin, 1);
        while (cur <= fin)
        {
            yield return (cur.Year, cur.Month);
            cur = cur.AddMonths(1);
        }
    }

    // Reintenta hasta 3 veces con backoff ante fallas transitorias de Oracle (timeout,
    // pool agotado, ORA-12170/ORA-3135) al ejecutar los procedures SIG del reporte —
    // antes un solo intento fallido dejaba el diccionario destino vacío/parcial SIN
    // avisar al usuario, lo que coincide con el síntoma reportado: "se pierde data al
    // cargar y al volver a llamar trae todo correctamente" (era el reintento MANUAL del
    // usuario el que "arreglaba" la falla transitoria). Si los 3 intentos fallan, se
    // relanza la excepción para que el controller devuelva 500 (mejor fallar visible
    // que mostrar un dashboard incompleto sin avisar).
    private async Task EjecutarConReintentosAsync(Func<Task> accion, string contexto)
    {
        const int maxIntentos = 3;
        for (var intento = 1; intento <= maxIntentos; intento++)
        {
            try
            {
                await accion();
                return;
            }
            catch (Exception ex) when (intento < maxIntentos)
            {
                _logger.LogWarning(ex, "Intento {Intento}/{Max} fallido ejecutando {Contexto}", intento, maxIntentos, contexto);
                await Task.Delay(TimeSpan.FromMilliseconds(300 * intento));
            }
        }
        // Último intento: si falla, se deja propagar (no hay catch aquí).
        await accion();
    }


    private static decimal GetDecimal(OracleDataReader r, string col) => r[col] == DBNull.Value ? 0m : Convert.ToDecimal(r[col]);
    private static int GetInt(OracleDataReader r, string col) => r[col] == DBNull.Value ? 0 : Convert.ToInt32(r[col]);
}
