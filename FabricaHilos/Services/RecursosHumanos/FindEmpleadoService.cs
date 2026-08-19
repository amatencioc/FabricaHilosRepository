using FabricaHilos.Models.RecursosHumanos;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System.Data;
using System.Linq;

namespace FabricaHilos.Services.RecursosHumanos;

public interface IFindEmpleadoService
{
    Task<(bool Ok, string? Mensaje, EmpleadoConEventoRealDto? Data)> BuscarAsync(
        string busqueda, string tipoBusqueda, DateTime? fechaDesde = null, DateTime? fechaHasta = null);

    Task<List<SugerenciaEmpleadoDto>> SugerirNombresAsync(string texto);

    /// <summary>
    /// v2.0 — Búsqueda masiva: sin nombre, solo por rango de fechas + categoría
    /// (EMPLEADO/OBRERO/TODOS). Trae TODOS los empleados/obreros que tengan al
    /// menos un registro en SIG.SI_REGPERS dentro del rango, y para cada uno
    /// reutiliza BuscarAsync (misma fuente de verdad que la búsqueda individual).
    /// </summary>
    Task<(bool Ok, string? Mensaje, List<EmpleadoConEventoRealDto> Data)> BuscarMasivoAsync(
        DateTime fechaDesde, DateTime fechaHasta, string categoria);
}

/// <summary>
/// Invoca AQUARIUS.SP_FIND_EMPLEADO_EVENTO_REAL: búsqueda flexible (CODIGO/DNI/NOMBRE)
/// + asistencia de HOY (AQUARIUS) + evento activo EN VIVO (SIG.RH_EVENTOS).
/// No modifica datos — 100% lectura.
/// </summary>
public class FindEmpleadoService : IFindEmpleadoService
{
    private readonly string _connectionString;
    private readonly ILogger<FindEmpleadoService> _logger;

    public FindEmpleadoService(IConfiguration configuration, ILogger<FindEmpleadoService> logger)
    {
        _connectionString = configuration.GetConnectionString("AquariusConnection")
            ?? throw new InvalidOperationException("AquariusConnection connection string not found.");
        _logger = logger;
    }

    public async Task<(bool Ok, string? Mensaje, EmpleadoConEventoRealDto? Data)> BuscarAsync(
        string busqueda, string tipoBusqueda, DateTime? fechaDesde = null, DateTime? fechaHasta = null)
    {
        try
        {
            await using var conn = new OracleConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = "SP_FIND_EMPLEADO_EVENTO_REAL";
            cmd.BindByName = true;

            cmd.Parameters.Add(new OracleParameter("p_busqueda",      OracleDbType.Varchar2) { Value = busqueda });
            cmd.Parameters.Add(new OracleParameter("p_tipo_busqueda", OracleDbType.Varchar2) { Value = tipoBusqueda });
            cmd.Parameters.Add(new OracleParameter("p_fecha_desde", OracleDbType.Date) { Value = (object?)fechaDesde ?? DBNull.Value });
            cmd.Parameters.Add(new OracleParameter("p_fecha_hasta", OracleDbType.Date) { Value = (object?)fechaHasta ?? DBNull.Value });

            cmd.Parameters.Add(new OracleParameter("p_cod_aquarius", OracleDbType.Varchar2, 10)  { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_cod_sig",      OracleDbType.Varchar2, 10)  { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_nombre",       OracleDbType.Varchar2, 200) { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_dni",          OracleDbType.Varchar2, 20)  { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_empresa",      OracleDbType.Varchar2, 10)  { Direction = ParameterDirection.Output });

            cmd.Parameters.Add(new OracleParameter("p_estado_hoy",   OracleDbType.Varchar2, 20)  { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_entrada",      OracleDbType.Varchar2, 10)  { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_salida",       OracleDbType.Varchar2, 10)  { Direction = ParameterDirection.Output });

            cmd.Parameters.Add(new OracleParameter("p_evento_desc",   OracleDbType.Varchar2, 500)  { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_evento_tipo",   OracleDbType.Varchar2, 5)    { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_evento_fechas", OracleDbType.Varchar2, 30)   { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_evento_obs",    OracleDbType.Varchar2, 500)  { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_evento_fuente", OracleDbType.Varchar2, 50)   { Direction = ParameterDirection.Output });

            cmd.Parameters.Add(new OracleParameter("p_vig_estado",  OracleDbType.Varchar2, 30)  { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_vig_entrada", OracleDbType.Varchar2, 10)  { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_vig_salida",  OracleDbType.Varchar2, 10)  { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_vig_alcohol", OracleDbType.Varchar2, 20)  { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_vig_celular", OracleDbType.Varchar2, 5)   { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_vig_fuente",  OracleDbType.Varchar2, 50)  { Direction = ParameterDirection.Output });

            cmd.Parameters.Add(new OracleParameter("p_rango_vig_desde", OracleDbType.Varchar2, 15) { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_rango_vig_hasta", OracleDbType.Varchar2, 15) { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_rango_evt_desde", OracleDbType.Varchar2, 15) { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_rango_evt_hasta", OracleDbType.Varchar2, 15) { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_cur_vigilancia", OracleDbType.RefCursor) { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_cur_eventos",    OracleDbType.RefCursor) { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_cur_compensaciones", OracleDbType.RefCursor) { Direction = ParameterDirection.Output });

            cmd.Parameters.Add(new OracleParameter("p_nota_sync", OracleDbType.Varchar2, 200) { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_error",     OracleDbType.Varchar2, 500) { Direction = ParameterDirection.Output });

            await cmd.ExecuteNonQueryAsync();

            string? GetOut(string name) =>
                cmd.Parameters[name].Value is OracleString os && !os.IsNull ? os.Value : null;

            static string? Col(OracleDataReader r, string name)
            {
                int idx;
                try
                {
                    idx = r.GetOrdinal(name);
                }
                catch (IndexOutOfRangeException)
                {
                    // Columna no existe en el resultset (ej. SP desplegado no coincide aún
                    // con la versión esperada por este código) — no debe tumbar la búsqueda.
                    return null;
                }
                return r.IsDBNull(idx) ? null : r.GetValue(idx).ToString();
            }

            var error = GetOut("p_error");
            if (!string.IsNullOrEmpty(error))
                return (false, error, null);

            var codAquarius = GetOut("p_cod_aquarius");
            if (string.IsNullOrEmpty(codAquarius))
                return (false, $"Empleado no encontrado: {busqueda}", null);

            var vigilanciaRegistros = new List<VigilanciaRegistroDto>();
            if (cmd.Parameters["p_cur_vigilancia"].Value is OracleRefCursor vigCursor)
            {
                using var vigReader = vigCursor.GetDataReader();
                while (vigReader.Read())
                {
                    vigilanciaRegistros.Add(new VigilanciaRegistroDto
                    {
                        Tipo             = Col(vigReader, "TIPO"),
                        CodSig           = Col(vigReader, "C_CODIGO"),
                        DocId            = Col(vigReader, "DOCID"),
                        Nombre           = Col(vigReader, "NOMBRE"),
                        DniRuc           = Col(vigReader, "DNI_RUC"),
                        CentroCosto      = Col(vigReader, "C_COSTO"),
                        TipoCp           = Col(vigReader, "TIPO_CP"),
                        FechaIngreso     = Col(vigReader, "FECHAI"),
                        FechaSalida      = Col(vigReader, "FECHAF"),
                        TraeCelular      = Col(vigReader, "TRAE_CELULAR"),
                        GuardaCelular    = Col(vigReader, "GUARDA_CELULAR"),
                        NroBlock         = Col(vigReader, "NRO_BLOCK"),
                        TestAlcohol      = Col(vigReader, "TEST_ALCOHOL"),
                        ResultadoAlcohol = Col(vigReader, "RESULTADO_ALCOHOL"),
                        Observacion      = Col(vigReader, "OBSERVACION"),
                    });
                }
            }

            var eventosHistorial = new List<EventoHistorialDto>();
            if (cmd.Parameters["p_cur_eventos"].Value is OracleRefCursor evtCursor)
            {
                using var evtReader = evtCursor.GetDataReader();
                while (evtReader.Read())
                {
                    eventosHistorial.Add(new EventoHistorialDto
                    {
                        TipoCodigo   = Col(evtReader, "EVENTO_TIPO_CODIGO"),
                        Descripcion  = Col(evtReader, "EVENTO_DESCRIPCION"),
                        FechaInicio  = Col(evtReader, "FECHA_INICIO"),
                        FechaFinal   = Col(evtReader, "FECHA_FINAL"),
                        Observacion  = Col(evtReader, "OBSERVACION"),
                        NoSincroniza = Col(evtReader, "NO_SINCRONIZA") == "S",
                    });
                }
            }

            // Compensaciones (AQUARIUS.SCA_COMPENSACION) deshabilitado por pedido de negocio:
            // ya no se procesa el cursor (se cierra sin iterar, se evita el costo de fetch/parseo).
            var compensaciones = new List<CompensacionDto>();
            if (cmd.Parameters["p_cur_compensaciones"].Value is OracleRefCursor compCursor)
            {
                compCursor.Dispose();
            }


            // v1.8 — Horario/Turno vigente. Consulta directa (no vía el SP grande, para no
            // depender de un nuevo redeploy) contra SCA_HORARIO_PERSONAL + SCA_HORARIO_CAB,
            // resolviendo el horario vigente a HOY mediante el patrón MAX(fec_vigencia).
            // v1.9 — Turno: mismo criterio que AQUARIUS.SP_AQ_PROYECCION_ASISTENCIA
            // (NVL(hd.hortur, hc.hortur) vía SCA_HORARIO_DET del día actual, ProcessDay),
            // porque en horarios rotativos el turno real puede variar por día respecto
            // al de la cabecera (SCA_HORARIO_CAB.hortur).
            string? horarioDesc = null, horarioTurno = null;
            var codEmpresaVal = GetOut("p_empresa");
            if (!string.IsNullOrEmpty(codEmpresaVal))
            {
                try
                {
                    await using var cmdHor = conn.CreateCommand();
                    cmdHor.BindByName = true;
                    cmdHor.CommandText =
                        "SELECT hc.hordes, NVL(hd.hortur, hc.hortur) AS hortur " +
                        "FROM SCA_HORARIO_PERSONAL hp " +
                        "JOIN SCA_HORARIO_CAB hc ON hc.horid = hp.horid " +
                        "LEFT JOIN SCA_HORARIO_DET hd ON hd.horid = hp.horid " +
                        "  AND hd.diaid = ProcessDay(SYSDATE) AND hd.aplica = 'S' " +
                        "WHERE hp.cod_empresa = :emp AND hp.cod_personal = :cod " +
                        "  AND hp.fec_vigencia = (SELECT MAX(fec_vigencia) FROM SCA_HORARIO_PERSONAL " +
                        "                          WHERE cod_empresa = :emp AND cod_personal = :cod AND fec_vigencia <= SYSDATE)";
                    cmdHor.Parameters.Add(new OracleParameter("emp", OracleDbType.Varchar2) { Value = codEmpresaVal });
                    cmdHor.Parameters.Add(new OracleParameter("cod", OracleDbType.Varchar2) { Value = codAquarius });

                    await using var horReader = await cmdHor.ExecuteReaderAsync();
                    if (await horReader.ReadAsync())
                    {
                        horarioDesc  = horReader["hordes"] is DBNull ? null : horReader["hordes"].ToString()?.Trim();
                        horarioTurno = horReader["hortur"] is DBNull ? null : horReader["hortur"].ToString()?.Trim();
                    }
                }
                catch (Exception exHor)
                {
                    // No debe tumbar la búsqueda principal si esta consulta adicional falla.
                    _logger.LogWarning(exHor, "No se pudo resolver horario/turno vigente para {Cod}", codAquarius);
                }

                // v2.1 — Turno POR DÍA para cada fila de vigilanciaRegistros. El personal con
                // horario rotativo cambia de turno día a día (SCA_HORARIO_DET); usar solo el
                // turno de HOY (arriba) para todas las filas de un rango histórico era incorrecto
                // (mostraba el mismo turno repetido para fechas donde el empleado tuvo otro turno).
                try
                {
                    var fechas = vigilanciaRegistros
                        .Select(v => v.FechaIngreso ?? v.FechaSalida)
                        .Where(f => !string.IsNullOrWhiteSpace(f))
                        .Select(f => f!.Trim().Split(' ')[0])
                        .Select(f => DateTime.TryParseExact(f, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out var d) ? d : (DateTime?)null)
                        .Where(d => d.HasValue)
                        .Select(d => d!.Value)
                        .ToList();

                    if (fechas.Count > 0)
                    {
                        var minFecha = fechas.Min();
                        var maxFecha = fechas.Max();

                        await using var cmdHorDia = conn.CreateCommand();
                        cmdHorDia.BindByName = true;
                        cmdHorDia.CommandText =
                            "SELECT TO_CHAR(f.fecha, 'DD/MM/YYYY') AS fecha_str, hc.hordes, NVL(hd.hortur, hc.hortur) AS hortur " +
                            "FROM (SELECT TRUNC(:desde) + LEVEL - 1 AS fecha FROM dual " +
                            "      CONNECT BY LEVEL <= (TRUNC(:hasta) - TRUNC(:desde) + 1)) f " +
                            "JOIN SCA_HORARIO_PERSONAL hp " +
                            "  ON hp.cod_empresa = :emp AND hp.cod_personal = :cod " +
                            " AND hp.fec_vigencia = (SELECT MAX(fec_vigencia) FROM SCA_HORARIO_PERSONAL " +
                            "                         WHERE cod_empresa = :emp AND cod_personal = :cod AND fec_vigencia <= f.fecha) " +
                            "JOIN SCA_HORARIO_CAB hc ON hc.horid = hp.horid " +
                            "LEFT JOIN SCA_HORARIO_DET hd ON hd.horid = hp.horid " +
                            "  AND hd.diaid = ProcessDay(f.fecha) AND hd.aplica = 'S'";
                        cmdHorDia.Parameters.Add(new OracleParameter("desde", OracleDbType.Date) { Value = minFecha });
                        cmdHorDia.Parameters.Add(new OracleParameter("hasta", OracleDbType.Date) { Value = maxFecha });
                        cmdHorDia.Parameters.Add(new OracleParameter("emp", OracleDbType.Varchar2) { Value = codEmpresaVal });
                        cmdHorDia.Parameters.Add(new OracleParameter("cod", OracleDbType.Varchar2) { Value = codAquarius });

                        var mapaTurnoPorFecha = new Dictionary<string, (string? Hordes, string? Hortur)>();
                        await using (var horDiaReader = await cmdHorDia.ExecuteReaderAsync())
                        {
                            while (await horDiaReader.ReadAsync())
                            {
                                var fechaStr = horDiaReader["fecha_str"] is DBNull ? null : horDiaReader["fecha_str"].ToString();
                                if (string.IsNullOrEmpty(fechaStr)) continue;
                                mapaTurnoPorFecha[fechaStr] = (
                                    horDiaReader["hordes"] is DBNull ? null : horDiaReader["hordes"].ToString()?.Trim(),
                                    horDiaReader["hortur"] is DBNull ? null : horDiaReader["hortur"].ToString()?.Trim());
                            }
                        }

                        foreach (var v in vigilanciaRegistros)
                        {
                            var fechaFila = (v.FechaIngreso ?? v.FechaSalida)?.Trim().Split(' ')[0];
                            if (fechaFila != null && mapaTurnoPorFecha.TryGetValue(fechaFila, out var turnoFila))
                            {
                                v.HorarioDia = turnoFila.Hordes;
                                v.TurnoDia   = turnoFila.Hortur;
                            }
                        }
                    }
                }
                catch (Exception exHorDia)
                {
                    _logger.LogWarning(exHorDia, "No se pudo resolver turno por día para {Cod}", codAquarius);
                }
            }

            var dto = new EmpleadoConEventoRealDto
            {
                CodAquarius          = codAquarius,
                CodSig               = GetOut("p_cod_sig"),
                NombreCompleto       = GetOut("p_nombre"),
                Dni                  = GetOut("p_dni"),
                Empresa              = codEmpresaVal,
                HorarioDescripcion   = horarioDesc,
                HorarioTurno         = horarioTurno,
                EstadoAsistenciaHoy  = GetOut("p_estado_hoy"),
                HoraEntrada          = GetOut("p_entrada"),
                HoraSalida           = GetOut("p_salida"),
                EventoDescripcion    = GetOut("p_evento_desc"),
                EventoTipoCodigo     = GetOut("p_evento_tipo"),
                EventoFechas         = GetOut("p_evento_fechas"),
                EventoObservacion    = GetOut("p_evento_obs"),
                FuenteEvento         = GetOut("p_evento_fuente") ?? "SIG.RH_EVENTOS (EN VIVO)",
                VigilanciaEstado     = GetOut("p_vig_estado"),
                VigilanciaEntrada    = GetOut("p_vig_entrada"),
                VigilanciaSalida     = GetOut("p_vig_salida"),
                VigilanciaAlcohol    = GetOut("p_vig_alcohol"),
                VigilanciaCelular    = GetOut("p_vig_celular"),
                VigilanciaFuente     = GetOut("p_vig_fuente") ?? "SIG.SI_REGPERS (VIGILANCIA - TIEMPO REAL)",
                RangoVigilanciaDesde = GetOut("p_rango_vig_desde"),
                RangoVigilanciaHasta = GetOut("p_rango_vig_hasta"),
                RangoEventosDesde    = GetOut("p_rango_evt_desde"),
                RangoEventosHasta    = GetOut("p_rango_evt_hasta"),
                VigilanciaRegistros  = vigilanciaRegistros,
                EventosHistorial     = eventosHistorial,
                Compensaciones       = compensaciones,
                NotaSincronizacion   = GetOut("p_nota_sync"),
            };

            return (true, null, dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al buscar empleado: {Busqueda} ({Tipo})", busqueda, tipoBusqueda);
            return (false, "Error al consultar el empleado en Aquarius.", null);
        }
    }

    /// <summary>
    /// v1.7 — Autocompletado de búsqueda por Nombre. Consulta directa (no vía el SP grande)
    /// contra PLA_PERSONAL, mismo criterio que AQUARIUS.sp_SCA_Read_Personal_AutCom:
    /// solo activos (tip_estado='AC'), LIKE sobre "APELLIDOS, Nombre", máx. 20 filas.
    /// </summary>
    public async Task<List<SugerenciaEmpleadoDto>> SugerirNombresAsync(string texto)
    {
        var resultado = new List<SugerenciaEmpleadoDto>();

        await using var conn = new OracleConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.BindByName = true;
        cmd.CommandText =
            "SELECT * FROM ( " +
            "  SELECT p.cod_personal, p.cod_spring, " +
            "         LTRIM(RTRIM(p.ape_paterno)) || ' ' || LTRIM(RTRIM(p.ape_materno)) || ', ' || LTRIM(RTRIM(p.nom_trabajador)) AS nombre " +
            "  FROM PLA_PERSONAL p " +
            "  WHERE UPPER(LTRIM(RTRIM(p.ape_paterno)) || ' ' || LTRIM(RTRIM(p.ape_materno)) || ', ' || LTRIM(RTRIM(p.nom_trabajador))) LIKE '%' || UPPER(:texto) || '%' " +
            "    AND p.tip_estado = 'AC' " +
            "  ORDER BY LTRIM(RTRIM(p.ape_paterno)) || ' ' || LTRIM(RTRIM(p.ape_materno)) || ', ' || LTRIM(RTRIM(p.nom_trabajador)) " +
            ") WHERE ROWNUM <= 20";
        cmd.Parameters.Add(new OracleParameter("texto", OracleDbType.Varchar2) { Value = texto });

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            resultado.Add(new SugerenciaEmpleadoDto
            {
                CodPersonal = reader["cod_personal"] is DBNull ? null : reader["cod_personal"].ToString(),
                CodSpring   = reader["cod_spring"]   is DBNull ? null : reader["cod_spring"].ToString(),
                Nombre      = reader["nombre"]       is DBNull ? null : reader["nombre"].ToString(),
            });
        }

        return resultado;
    }

    /// <summary>
    /// v2.0 — Búsqueda masiva por rango de fechas (sin nombre). Universo: solo
    /// empleados/obreros con al menos 1 registro en SIG.SI_REGPERS (TIPO='T')
    /// dentro del rango [fechaDesde, fechaHasta]. Filtro categoría vía
    /// AQUARIUS.PLA_TIPO_PLANILLA.DES_TIPO_PLANILLA (LIKE 'EMPLEADO%'/'OBRERO%').
    /// Sin límite de filas (decisión de negocio). Reutiliza BuscarAsync por cada
    /// código encontrado para no duplicar la lógica del SP.
    /// </summary>
    public async Task<(bool Ok, string? Mensaje, List<EmpleadoConEventoRealDto> Data)> BuscarMasivoAsync(
        DateTime fechaDesde, DateTime fechaHasta, string categoria)
    {
        var resultado = new List<EmpleadoConEventoRealDto>();
        try
        {
            var codigos = new List<string>();

            await using (var conn = new OracleConnection(_connectionString))
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.BindByName = true;
                cmd.CommandText =
                    "SELECT DISTINCT p.cod_personal " +
                    "FROM SIG.SI_REGPERS s " +
                    "JOIN PLA_PERSONAL p ON p.cod_spring = s.c_codigo " +
                    "JOIN PLA_TIPO_PLANILLA tp ON tp.cod_empresa = p.cod_empresa AND tp.cod_tipo_planilla = p.cod_tipo_planilla " +
                    "WHERE s.tipo = 'T' AND s.fechai IS NOT NULL " +
                    "  AND TRUNC(s.fechai) BETWEEN TRUNC(:desde) AND TRUNC(:hasta) " +
                    "  AND (:categoria = 'TODOS' OR UPPER(tp.des_tipo_planilla) LIKE UPPER(:categoria) || '%') " +
                    "ORDER BY p.cod_personal";
                cmd.Parameters.Add(new OracleParameter("desde", OracleDbType.Date) { Value = fechaDesde.Date });
                cmd.Parameters.Add(new OracleParameter("hasta", OracleDbType.Date) { Value = fechaHasta.Date });
                cmd.Parameters.Add(new OracleParameter("categoria", OracleDbType.Varchar2)
                {
                    Value = string.IsNullOrWhiteSpace(categoria) ? "TODOS" : categoria.Trim().ToUpperInvariant()
                });

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var cod = reader["cod_personal"] is DBNull ? null : reader["cod_personal"].ToString();
                    if (!string.IsNullOrEmpty(cod))
                        codigos.Add(cod);
                }
            }

            // Cada empleado se resuelve con la misma lógica probada de BuscarAsync.
            // v2.1 — Se ejecuta con concurrencia limitada (en vez de 100% secuencial)
            // para reducir el tiempo total de la búsqueda masiva sin saturar el pool
            // de conexiones Oracle (cada BuscarAsync abre su propia conexión).
            const int maxConcurrencia = 5;
            using var semaforo = new SemaphoreSlim(maxConcurrencia);
            var resultadosPorIndice = new EmpleadoConEventoRealDto?[codigos.Count];

            var tareas = codigos.Select(async (cod, i) =>
            {
                await semaforo.WaitAsync();
                try
                {
                    var (ok, _, data) = await BuscarAsync(cod, "CODIGO", fechaDesde, fechaHasta);
                    if (ok && data != null)
                        resultadosPorIndice[i] = data;
                }
                finally
                {
                    semaforo.Release();
                }
            });

            await Task.WhenAll(tareas);

            resultado.AddRange(resultadosPorIndice.Where(d => d != null)!);

            return (true, null, resultado);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en búsqueda masiva rango {Desde:d}-{Hasta:d} categoría {Cat}", fechaDesde, fechaHasta, categoria);
            return (false, "Error al consultar la búsqueda masiva en Aquarius.", resultado);
        }
    }
}
