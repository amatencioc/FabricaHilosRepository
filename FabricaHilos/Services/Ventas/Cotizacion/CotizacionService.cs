using System.Data;
using System.Globalization;
using System.Text.Json;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using FabricaHilos.Models.Ventas.Cotizacion;

namespace FabricaHilos.Services.Ventas.Cotizacion;

public interface ICotizacionService
{
    /// <summary>Camino A: calcula una línea de tiempo de costeo a partir de parámetros libres (simulación).</summary>
    Task<CotizacionTimelineDto> CalcularSimulacionAsync(CotizacionParametros parametros);

    /// <summary>Detalle/trazabilidad por componente (PKG_COT.F_COTIZAR_DETALLE): de qué tabla/COT_KB
    /// sale cada valor, qué claves se buscaron y la fórmula aplicada. Para el panel "Ver detalle del cálculo".</summary>
    Task<List<CotizarDetalleDto>> ObtenerDetalleCalculoAsync(CotizacionParametros parametros);

    /// <summary>Recalcula la línea de tiempo a partir de una edición de ítem (real o simulación) aún no guardada.</summary>
    Task<CotizacionTimelineDto> RecalcularItemAsync(CotizacionItemEdicionDto edicion);

    /// <summary>Comparativo por tonalidad (Camino A): evalúa F_COTIZAR para las 6 tonalidades
    /// (CRUDO/BLANCO/CLARO/MEDIO/OSCURO/INTENSO) con los demás parámetros fijos, replicando
    /// la hoja "Resumen" del Excel manual de costeo para que la contadora compare de un vistazo.</summary>
    Task<CotizacionComparativoDto> CompararTonalidadesAsync(CotizacionParametros parametros);

    /// <summary>Igual que <see cref="CompararTonalidadesAsync"/> pero derivando los parámetros
    /// desde una edición de ítem (real o simulación) aún no guardada (Camino B, Detalle.cshtml).</summary>
    Task<CotizacionComparativoDto> CompararTonalidadesItemAsync(CotizacionItemEdicionDto edicion);

    /// <summary>Catálogo de referencia (COT_KB + PARAMCOS) que alimenta las fórmulas de F_COTIZAR
    /// — equivalente a las hojas "Auxiliares"/"Gas Natural" del Excel manual. Dato GLOBAL (no
    /// depende de los parámetros de la cotización), para que el usuario audite/revise los montos base.</summary>
    Task<CotizacionAuxiliaresDto> ObtenerAuxiliaresAsync();

    /// <summary>Camino B: recalcula la línea de tiempo de un ítem real ya existente en COTIZACION_D/P.</summary>
    Task<CotizacionTimelineDto> CalcularItemExistenteAsync(string tipoDoc, int serie, long numero, int item);

    /// <summary>Guarda (crea o versiona) una simulación completa en COT_HISTORIAL (TIPODOC='SM'). Devuelve el NUMERO.</summary>
    Task<long> GuardarSimulacionAsync(long? numero, CotizacionParametros parametros, string usuario, string accion, string? observacion, long? numeroOrigen);

    /// <summary>Aplica una edición a un ítem real: guarda snapshot en COT_HISTORIAL y sobrescribe COTIZACION_D/P.</summary>
    Task GuardarEdicionItemRealAsync(string tipoDoc, int serie, long numero, int item, CotizacionItemEdicionDto edicion, string usuario, string? observacion);

    /// <summary>Edita (nueva versión en COT_HISTORIAL) una simulación existente a partir del formulario por sección.</summary>
    Task GuardarEdicionSimulacionAsync(long numero, CotizacionItemEdicionDto edicion, string usuario, string? observacion);

    /// <summary>Listado combinado de cotizaciones reales (CT) y simulaciones (SM) para el Index.</summary>
    Task<(List<CotizacionResumenDto> Items, int TotalCount)> ListarAsync(string? buscar, bool incluirEliminadas, int page, int pageSize);

    /// <summary>Detalle completo (cabecera + ítems + línea de tiempo + historial) de una cotización real o simulación.</summary>
    Task<CotizacionDetalleViewModel?> ObtenerDetalleCompletoAsync(string tipoDoc, int serie, long numero);

    /// <summary>Historial de versiones de una cotización/ítem (ITEM=null trae todos los ítems incl. el header ITEM=0).</summary>
    Task<List<CotizacionHistorialDto>> ObtenerHistorialAsync(string tipoDoc, int serie, long numero, int? item);

    /// <summary>Marca (app-level, no toca el ERP) una cotización/simulación como eliminada.</summary>
    Task EliminarAsync(string tipoDoc, int serie, long numero, string usuario, string? observacion);

    /// <summary>Revierte una eliminación app-level.</summary>
    Task RestaurarAsync(string tipoDoc, int serie, long numero, string usuario);

    /// <summary>Duplica un ítem (real o de simulación) como una NUEVA simulación independiente. Devuelve el NUMERO nuevo.</summary>
    Task<long> DuplicarItemComoSimulacionAsync(string tipoDoc, int serie, long numero, int item, string usuario);

    /// <summary>Busca códigos de título (H_TITULOS) por código o descripción, para el autocomplete de Simular/Detalle.</summary>
    Task<List<CotizacionLookupDto>> BuscarTitulosAsync(string? texto);

    /// <summary>Busca artículos de materia prima (ARTICUL, TP_ART='M') por código o descripción.</summary>
    Task<List<CotizacionLookupDto>> BuscarMateriaPrimaAsync(string? texto);

    /// <summary>Ficha técnica de ruta VIGENTE (mantenida por Preparatoria) para un título+intensidad —
    /// usada en Simular.cshtml mientras la simulación aún no se ha guardado (muestra siempre la última).</summary>
    Task<RutaTecnicaCabDto?> ObtenerRutaTecnicaVigenteAsync(string? tituloCod, string? intensidadCod);
}

/// <summary>
/// Servicio del motor de costeo/cotización (Camino A: simulación libre vía PKG_COT.F_COTIZAR;
/// Camino B: recálculo/edición de ítems reales de COTIZACION_G/D/P), con historial y
/// versionado propio en COT_HISTORIAL. Ver copilot-instructions.md sección PKG_COT para el
/// detalle de las reglas de negocio replicadas aquí (PV_PARSE_PCT, mapeo de presentación, etc).
///
/// Diseño de seguridad (decidido tras auditar COTIZACION_G/D/P):
///   - NUNCA se inserta en COTIZACION_G (su mecanismo real de generación de NUMERO no coincide
///     con ninguna secuencia visible en USER_SEQUENCES).
///   - NUNCA se modifica COTIZACION_G/D/P.ESTADO (dominio de valores no documentado).
///   - "Eliminar"/"Restaurar" son banderas 100% app-level (fila COT_HISTORIAL ITEM=0).
///   - "Duplicar" jamás crea una cotización real: siempre crea una simulación (TIPODOC='SM')
///     con NUMERO propio (COT_SIMULACION_SEQ), enlazada por NUMERO_ORIGEN.
/// </summary>
public class CotizacionService : OracleServiceBase, ICotizacionService
{
    private readonly ILogger<CotizacionService> _logger;
    private readonly IRutaTecnicaService _rutaTecnicaService;

    public CotizacionService(
        IConfiguration configuration,
        ILogger<CotizacionService> logger,
        IHttpContextAccessor httpContextAccessor,
        IRutaTecnicaService rutaTecnicaService)
        : base(configuration, httpContextAccessor)
    {
        _logger = logger;
        _rutaTecnicaService = rutaTecnicaService;
    }

    // ── helpers de lectura de OracleDataReader ──────────────────────────────────

    private static string?   GetStr(OracleDataReader r, string col)      => r[col] == DBNull.Value ? null : r[col]?.ToString();
    private static decimal    GetDec(OracleDataReader r, string col)     => r[col] == DBNull.Value ? 0m   : Convert.ToDecimal(r[col]);
    private static decimal?   GetNullDec(OracleDataReader r, string col) => r[col] == DBNull.Value ? null : Convert.ToDecimal(r[col]);
    private static DateTime?  GetDt(OracleDataReader r, string col)      => r[col] == DBNull.Value ? null : Convert.ToDateTime(r[col]);
    private static int        GetInt(OracleDataReader r, string col)     => r[col] == DBNull.Value ? 0    : Convert.ToInt32(r[col]);
    private static int?       GetNullInt(OracleDataReader r, string col) => r[col] == DBNull.Value ? null : Convert.ToInt32(r[col]);
    private static long       GetLong(OracleDataReader r, string col)    => r[col] == DBNull.Value ? 0L   : Convert.ToInt64(r[col]);
    private static long?      GetNullLong(OracleDataReader r, string col)=> r[col] == DBNull.Value ? null : Convert.ToInt64(r[col]);

    private static string? GetClobStr(OracleDataReader r, string col)
    {
        var val = r[col];
        if (val == DBNull.Value) return null;
        return val is OracleClob clob ? clob.Value : val.ToString();
    }

    // ── metadata de pasos para ordenar/etiquetar la línea de tiempo ─────────────
    // El cursor de F_COTIZAR usa "ORDER BY 1" (alfabético) por lo que las filas de
    // resumen ('---...') salen primero. Se reordena aquí según el orden lógico real.
    private static readonly Dictionary<string, (int Orden, string Grupo, string Etiqueta, string Icono, string Color)> _stepMeta = new()
    {
        ["MP1_BRUTO"]       = (1,  "componente", "Materia Prima 1",            "bi-flower1",               "#8d6e63"),
        ["MP2_BRUTO"]       = (2,  "componente", "Materia Prima 2",            "bi-flower2",               "#a1887f"),
        ["MERMA_DELTA"]     = (3,  "componente", "Merma (incremento)",         "bi-arrow-down-up",         "#ff9800"),
        ["MP_CON_MERMA"]    = (4,  "componente", "MP neto con merma",          "bi-basket-fill",           "#795548"),
        ["HILATURA"]        = (5,  "componente", "Hilatura (Spinning)",        "bi-gear-fill",             "#0d6efd"),
        ["CABLE_PLYING"]    = (6,  "componente", "Cable / Retorcido",          "bi-link-45deg",            "#6f42c1"),
        ["TINTURA_TT"]      = (7,  "componente", "Tintorería (TT)",            "bi-droplet-fill",          "#6610f2"),
        ["DEVANADO"]        = (8,  "componente", "Devanado",                   "bi-arrow-repeat",          "#20c997"),
        ["EMPAQUE"]         = (9,  "componente", "Empaque",                    "bi-box-seam-fill",         "#fd7e14"),
        ["FIJADO"]          = (10, "componente", "Fijado / Torsión",           "bi-lightning-charge-fill", "#dc3545"),
        ["OVERHEAD"]        = (11, "componente", "Overhead (MOI+CIF+GOF)",     "bi-building",              "#6c757d"),
        ["---TOTAL_COSTO"]  = (12, "resumen",    "Costo Total",                "bi-flag-fill",             "#198754"),
        ["---MERMA_ESCALA"] = (13, "resumen",    "% Merma por escala de lote", "bi-percent",               "#0dcaf0"),
        ["---ESCALA_KG"]    = (14, "resumen",    "Factor de escala por kg",    "bi-rulers",                "#0dcaf0"),
        ["---PRECIO_25PCT"] = (15, "precio",     "Precio sugerido 25%",        "bi-tag",                   "#adb5bd"),
        ["---PRECIO_30PCT"] = (16, "precio",     "Precio sugerido 30%",        "bi-tag-fill",              "#0d6efd"),
        ["---PRECIO_35PCT"] = (17, "precio",     "Precio sugerido 35%",        "bi-tag-fill",              "#198754"),
        ["---PRECIO_40PCT"] = (18, "precio",     "Precio sugerido 40%",        "bi-tag-fill",              "#fd7e14"),
    };

    // ══════════════════════════════════════════════════════════════════════════
    // CÁLCULO (llamada a PKG_COT.F_COTIZAR vía REF CURSOR)
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<CotizacionTimelineDto> CalcularSimulacionAsync(CotizacionParametros parametros)
    {
        using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        var pasos = await EjecutarFCotizarAsync(conn, parametros);
        return ConstruirTimeline(parametros, pasos);
    }

    public async Task<List<CotizarDetalleDto>> ObtenerDetalleCalculoAsync(CotizacionParametros p)
    {
        using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();

        var sql = $@"
            BEGIN
                :ret := {S}PKG_COT.F_COTIZAR_DETALLE(
                    p_titulo_cod     => :p_titulo_cod,
                    p_cod_art_mp1    => :p_cod_art_mp1,
                    p_cod_art_mp2    => :p_cod_art_mp2,
                    p_pct_mp1        => :p_pct_mp1,
                    p_proceso        => :p_proceso,
                    p_intensidad_cod => :p_intensidad_cod,
                    p_cantidad_kg    => :p_cantidad_kg,
                    p_presentacion   => :p_presentacion,
                    p_nplies         => :p_nplies
                );
            END;";

        using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        cmd.Parameters.Add(new OracleParameter(":ret", OracleDbType.RefCursor) { Direction = ParameterDirection.Output });
        cmd.Parameters.Add(new OracleParameter(":p_titulo_cod",     OracleDbType.Varchar2, (object?)p.TituloCod ?? DBNull.Value, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":p_cod_art_mp1",    OracleDbType.Varchar2, (object?)p.CodArtMp1 ?? DBNull.Value, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":p_cod_art_mp2",    OracleDbType.Varchar2, (object?)p.CodArtMp2 ?? DBNull.Value, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":p_pct_mp1",        OracleDbType.Decimal,  p.PctMp1,        ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":p_proceso",        OracleDbType.Varchar2, p.Proceso,       ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":p_intensidad_cod", OracleDbType.Varchar2, p.IntensidadCod, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":p_cantidad_kg",    OracleDbType.Decimal,  p.CantidadKg,    ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":p_presentacion",   OracleDbType.Varchar2, p.Presentacion,  ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":p_nplies",         OracleDbType.Int32,    p.Nplies,        ParameterDirection.Input));

        await cmd.ExecuteNonQueryAsync();

        var filas = new List<CotizarDetalleDto>();
        if (cmd.Parameters[":ret"].Value is OracleRefCursor refCursor)
        {
            using var reader = refCursor.GetDataReader();
            var ordValorRef = reader.GetOrdinal("VALOR_REF");
            while (reader.Read())
            {
                filas.Add(new CotizarDetalleDto
                {
                    Componente = reader["COMPONENTE"] == DBNull.Value ? "" : reader["COMPONENTE"].ToString()!,
                    Fuente     = reader["FUENTE"] == DBNull.Value ? null : reader["FUENTE"].ToString(),
                    Detalle    = reader["DETALLE"] == DBNull.Value ? null : reader["DETALLE"].ToString(),
                    // Lectura defensiva: aunque el SQL ya redondea a NUMBER(18,6), se evita aquí
                    // que un futuro valor con demasiados dígitos (fuera del rango de System.Decimal)
                    // tumbe todo el panel de detalle — se omite ese valor puntual en vez de fallar.
                    ValorRef   = reader.IsDBNull(ordValorRef) ? null : SafeGetDecimal(reader, ordValorRef),
                });
            }
        }
        return filas;
    }

    private static decimal? SafeGetDecimal(OracleDataReader reader, int ordinal)
    {
        try
        {
            return reader.GetDecimal(ordinal);
        }
        catch (Exception ex) when (ex is OverflowException or InvalidCastException)
        {
            return null;
        }
    }

    public async Task<CotizacionTimelineDto> RecalcularItemAsync(CotizacionItemEdicionDto edicion)
    {
        using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        var parametros = await DerivarParametrosAsync(conn, edicion);
        var pasos = await EjecutarFCotizarAsync(conn, parametros);
        return ConstruirTimeline(parametros, pasos);
    }

    // ── Comparativo por tonalidad (hoja "Resumen" del Excel) ────────────────────
    // Orden fijo = orden de proceso de intensidad de tintorería (mismo orden que los
    // <select> de Simular.cshtml/Detalle.cshtml): CRUDO, BLANCO, CLARO, MEDIO, OSCURO, INTENSO.
    private static readonly (string Cod, string Etiqueta)[] _tonalidadesOrden =
    [
        ("0", "Crudo"),
        ("5", "Blanco"),
        ("1", "Claro"),
        ("2", "Medio"),
        ("3", "Oscuro"),
        ("4", "Intenso"),
    ];

    public async Task<CotizacionComparativoDto> CompararTonalidadesAsync(CotizacionParametros parametros)
    {
        using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        return await CompararTonalidadesCoreAsync(conn, parametros);
    }

    public async Task<CotizacionComparativoDto> CompararTonalidadesItemAsync(CotizacionItemEdicionDto edicion)
    {
        using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        var parametros = await DerivarParametrosAsync(conn, edicion);
        return await CompararTonalidadesCoreAsync(conn, parametros);
    }

    private async Task<CotizacionComparativoDto> CompararTonalidadesCoreAsync(OracleConnection conn, CotizacionParametros baseParams)
    {
        var columnas = new List<CotizacionComparativoColumnaDto>();
        var pasosPorColumna = new List<List<CotizarPasoDto>>();

        foreach (var (cod, etiqueta) in _tonalidadesOrden)
        {
            var p = new CotizacionParametros
            {
                TituloCod = baseParams.TituloCod,
                CodArtMp1 = baseParams.CodArtMp1,
                CodArtMp2 = baseParams.CodArtMp2,
                PctMp1 = baseParams.PctMp1,
                Proceso = baseParams.Proceso,
                IntensidadCod = cod,
                CantidadKg = baseParams.CantidadKg,
                Presentacion = baseParams.Presentacion,
                Nplies = baseParams.Nplies,
                MargenPct = baseParams.MargenPct,
            };
            var pasos = await EjecutarFCotizarAsync(conn, p);
            columnas.Add(new CotizacionComparativoColumnaDto
            {
                IntensidadCod = cod,
                Etiqueta = etiqueta,
                EsActual = cod == baseParams.IntensidadCod,
            });
            pasosPorColumna.Add(pasos);
        }

        // Universo de TIPO en el orden lógico de proceso (ya viene ordenado por EnriquecerYOrdenar).
        var meta = pasosPorColumna
            .SelectMany(pasos => pasos)
            .GroupBy(x => x.Tipo)
            .Select(g => g.First())
            .OrderBy(x => x.Orden)
            .ToList();

        var filas = meta.Select(m => new CotizacionComparativoFilaDto
        {
            Tipo = m.Tipo,
            Etiqueta = m.EtiquetaCorta,
            Grupo = m.Grupo,
            Icono = m.Icono,
            Color = m.Color,
            Valores = pasosPorColumna
                .Select(pasos => pasos.FirstOrDefault(x => x.Tipo == m.Tipo)?.CostoUsdKg ?? 0m)
                .ToList(),
        }).ToList();

        return new CotizacionComparativoDto { Columnas = columnas, Filas = filas };
    }

    // ── Auxiliares y Servicios (COT_KB + PARAMCOS) — hojas "Auxiliares"/"Gas Natural" del Excel ─
    // Dato GLOBAL: no depende de los parámetros de la cotización, se lee tal cual está en BD.
    public async Task<CotizacionAuxiliaresDto> ObtenerAuxiliaresAsync()
    {
        using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();

        var resultado = new CotizacionAuxiliaresDto();

        // PARAMCOS: una sola fila vigente, con la corrección de las columnas desfasadas
        // (ver cabecera de PKG_COT.sql: COSTO_MOI real=MOD, COSTO_CIF real=MOI, COSTO_GOF real=CIF).
        using (var cmdParam = new OracleCommand(
            "SELECT ICAMBIO, COSTO_ENEL, COSTO_MOI, COSTO_CIF, COSTO_GOF FROM PARAMCOS WHERE ROWNUM = 1", conn))
        using (var reader = await cmdParam.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                decimal? Num(string col) => reader[col] == DBNull.Value ? null : Convert.ToDecimal(reader[col]);
                resultado.Parametros.Add(new CotizacionParametroGlobalDto { Nombre = "Tipo de Cambio", Valor = Num("ICAMBIO"), Unidad = "S//USD", Nota = "PARAMCOS.ICAMBIO" });
                resultado.Parametros.Add(new CotizacionParametroGlobalDto { Nombre = "Energía Eléctrica", Valor = Num("COSTO_ENEL"), Unidad = "S//kWh", Nota = "PARAMCOS.COSTO_ENEL" });
                resultado.Parametros.Add(new CotizacionParametroGlobalDto { Nombre = "Mano de Obra Directa (MOD)", Valor = Num("COSTO_MOI"), Unidad = "USD/HH", Nota = "PARAMCOS.COSTO_MOI (columna desfasada: guarda el MOD real)" });
                resultado.Parametros.Add(new CotizacionParametroGlobalDto { Nombre = "MOI (Mano de Obra Indirecta)", Valor = Num("COSTO_CIF"), Unidad = "USD/kg", Nota = "PARAMCOS.COSTO_CIF (columna desfasada: guarda el MOI real)" });
                resultado.Parametros.Add(new CotizacionParametroGlobalDto { Nombre = "CIF (Costos Indirectos de Fabricación)", Valor = Num("COSTO_GOF"), Unidad = "USD/kg", Nota = "PARAMCOS.COSTO_GOF (columna desfasada: guarda el CIF real)" });
            }
        }

        // GOF (Gastos Operativos/Financieros) no vive en PARAMCOS — sale de COT_KB.OVERHEAD_GOF.
        using (var cmdGof = new OracleCommand($"SELECT {S}PKG_COT.F_OVERHEAD_USD_KG FROM DUAL", conn))
        {
            try
            {
                var total = Convert.ToDecimal(await cmdGof.ExecuteScalarAsync());
                resultado.Parametros.Add(new CotizacionParametroGlobalDto { Nombre = "Overhead total (MOI+CIF+GOF)", Valor = total, Unidad = "USD/kg", Nota = "PKG_COT.F_OVERHEAD_USD_KG (valor combinado que usa F_COTIZAR)" });
            }
            catch { /* función privada del package body en algunas versiones: se omite si no es visible */ }
        }

        // COT_KB completo (activo), agrupado por categoría — catálogo de insumos/tarifas/factores.
        var sql = $@"
            SELECT CATEGORIA, CLAVE1, CLAVE2, CLAVE3, CLAVE4, VALOR_NUM, VALOR_TEXT, UNIDAD,
                   FUENTE, CONFIANZA, OBSERVACION, FCH_ACTUALIZ
            FROM {S}COT_KB
            WHERE ESTADO = 'A'
            ORDER BY CATEGORIA, CLAVE1, CLAVE2, CLAVE3";
        using (var cmd = new OracleCommand(sql, conn))
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                resultado.Auxiliares.Add(new CotizacionAuxiliarDto
                {
                    Categoria = reader["CATEGORIA"] == DBNull.Value ? "" : reader["CATEGORIA"].ToString()!,
                    Clave1 = reader["CLAVE1"] == DBNull.Value ? null : reader["CLAVE1"].ToString(),
                    Clave2 = reader["CLAVE2"] == DBNull.Value ? null : reader["CLAVE2"].ToString(),
                    Clave3 = reader["CLAVE3"] == DBNull.Value ? null : reader["CLAVE3"].ToString(),
                    Clave4 = reader["CLAVE4"] == DBNull.Value ? null : reader["CLAVE4"].ToString(),
                    ValorNum = reader["VALOR_NUM"] == DBNull.Value ? null : Convert.ToDecimal(reader["VALOR_NUM"]),
                    ValorText = reader["VALOR_TEXT"] == DBNull.Value ? null : reader["VALOR_TEXT"].ToString(),
                    Unidad = reader["UNIDAD"] == DBNull.Value ? null : reader["UNIDAD"].ToString(),
                    Fuente = reader["FUENTE"] == DBNull.Value ? null : reader["FUENTE"].ToString(),
                    Confianza = reader["CONFIANZA"] == DBNull.Value ? null : reader["CONFIANZA"].ToString(),
                    Observacion = reader["OBSERVACION"] == DBNull.Value ? null : reader["OBSERVACION"].ToString(),
                    FchActualiz = reader["FCH_ACTUALIZ"] == DBNull.Value ? null : Convert.ToDateTime(reader["FCH_ACTUALIZ"]),
                });
            }
        }

        return resultado;
    }

    public async Task<CotizacionTimelineDto> CalcularItemExistenteAsync(string tipoDoc, int serie, long numero, int item)
    {
        using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();

        var edicion = await LeerEdicionDesdeBdAsync(conn, tipoDoc, serie, numero, item)
            ?? throw new InvalidOperationException($"No se encontró el ítem {item} de la cotización {tipoDoc}-{serie}-{numero}.");

        var parametros = await DerivarParametrosAsync(conn, edicion);
        var pasos = await EjecutarFCotizarAsync(conn, parametros);
        return ConstruirTimeline(parametros, pasos);
    }

    private async Task<List<CotizarPasoDto>> EjecutarFCotizarAsync(OracleConnection conn, CotizacionParametros p)
    {
        var sql = $@"
            BEGIN
                :ret := {S}PKG_COT.F_COTIZAR(
                    p_titulo_cod     => :p_titulo_cod,
                    p_cod_art_mp1    => :p_cod_art_mp1,
                    p_cod_art_mp2    => :p_cod_art_mp2,
                    p_pct_mp1        => :p_pct_mp1,
                    p_proceso        => :p_proceso,
                    p_intensidad_cod => :p_intensidad_cod,
                    p_cantidad_kg    => :p_cantidad_kg,
                    p_presentacion   => :p_presentacion,
                    p_nplies         => :p_nplies,
                    p_margen_pct     => :p_margen_pct
                );
            END;";

        using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        cmd.Parameters.Add(new OracleParameter(":ret", OracleDbType.RefCursor) { Direction = ParameterDirection.Output });
        cmd.Parameters.Add(new OracleParameter(":p_titulo_cod",     OracleDbType.Varchar2, (object?)p.TituloCod  ?? DBNull.Value, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":p_cod_art_mp1",    OracleDbType.Varchar2, (object?)p.CodArtMp1 ?? DBNull.Value, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":p_cod_art_mp2",    OracleDbType.Varchar2, (object?)p.CodArtMp2 ?? DBNull.Value, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":p_pct_mp1",        OracleDbType.Decimal,  p.PctMp1,        ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":p_proceso",        OracleDbType.Varchar2, p.Proceso,       ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":p_intensidad_cod", OracleDbType.Varchar2, p.IntensidadCod, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":p_cantidad_kg",    OracleDbType.Decimal,  p.CantidadKg,    ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":p_presentacion",   OracleDbType.Varchar2, p.Presentacion,  ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":p_nplies",         OracleDbType.Int32,    p.Nplies,        ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":p_margen_pct",     OracleDbType.Decimal,  p.MargenPct,     ParameterDirection.Input));

        await cmd.ExecuteNonQueryAsync();

        var pasos = new List<CotizarPasoDto>();
        if (cmd.Parameters[":ret"].Value is OracleRefCursor refCursor)
        {
            using var reader = refCursor.GetDataReader();
            while (reader.Read())
            {
                pasos.Add(new CotizarPasoDto
                {
                    Tipo        = reader["TIPO"] == DBNull.Value ? "" : reader["TIPO"].ToString()!,
                    Descripcion = reader["DESCRIPCION"] == DBNull.Value ? null : reader["DESCRIPCION"].ToString(),
                    CostoUsdKg  = reader["COSTO_USD_KG"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["COSTO_USD_KG"]),
                    Notas       = reader["NOTAS"] == DBNull.Value ? null : reader["NOTAS"].ToString(),
                });
            }
        }
        return EnriquecerYOrdenar(pasos);
    }

    private static List<CotizarPasoDto> EnriquecerYOrdenar(List<CotizarPasoDto> pasos)
    {
        foreach (var paso in pasos)
        {
            if (_stepMeta.TryGetValue(paso.Tipo, out var meta))
            {
                paso.Orden = meta.Orden;
                paso.Grupo = meta.Grupo;
                paso.EtiquetaCorta = meta.Etiqueta;
                paso.Icono = meta.Icono;
                paso.Color = meta.Color;
            }
            else
            {
                paso.Orden = 99;
                paso.Grupo = "componente";
                paso.EtiquetaCorta = paso.Tipo;
            }
        }
        return pasos.OrderBy(x => x.Orden).ToList();
    }

    private static CotizacionTimelineDto ConstruirTimeline(CotizacionParametros p, List<CotizarPasoDto> pasos)
    {
        decimal Buscar(string tipo) => pasos.FirstOrDefault(x => x.Tipo == tipo)?.CostoUsdKg ?? 0m;
        return new CotizacionTimelineDto
        {
            Parametros = p,
            Pasos = pasos,
            CostoTotal = Buscar("---TOTAL_COSTO"),
            Precio25 = Buscar("---PRECIO_25PCT"),
            Precio30 = Buscar("---PRECIO_30PCT"),
            Precio35 = Buscar("---PRECIO_35PCT"),
            Precio40 = Buscar("---PRECIO_40PCT"),
        };
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Derivación de parámetros (replica la lógica interna de F_COTIZAR_NRO / PV_PARSE_PCT)
    // ══════════════════════════════════════════════════════════════════════════

    private async Task<CotizacionParametros> DerivarParametrosAsync(OracleConnection conn, CotizacionItemEdicionDto e)
    {
        var pct1 = CalcularPctMp1(e.Fibra2, e.Valpf);
        string? fibra1Art = string.IsNullOrWhiteSpace(e.Fibra1) ? null : await ResolverFibraArtAsync(conn, e.Fibra1);
        string? fibra2Art = (pct1 < 100 && !string.IsNullOrWhiteSpace(e.Fibra2)) ? await ResolverFibraArtAsync(conn, e.Fibra2) : null;

        var titulo = e.Titulo?.Trim();
        int nplies = (titulo == "151" || titulo == "152") ? 3 : 1;

        return new CotizacionParametros
        {
            TituloCod = string.IsNullOrWhiteSpace(titulo) ? null : titulo,
            CodArtMp1 = fibra1Art,
            CodArtMp2 = fibra2Art,
            PctMp1 = pct1,
            Proceso = string.IsNullOrWhiteSpace(e.Proceso) ? "01" : e.Proceso,
            IntensidadCod = string.IsNullOrWhiteSpace(e.IntensidadCod) ? "3" : e.IntensidadCod,
            CantidadKg = e.CantidadKg <= 0 ? 500 : e.CantidadKg,
            Presentacion = MapearPresentacion(e.Presentacion),
            Nplies = nplies,
            MargenPct = e.MargenPct <= 0 ? 30 : e.MargenPct,
        };
    }

    /// <summary>Replica PV_PARSE_PCT (privada en PKG_COT, no expuesta en el spec).</summary>
    private static decimal CalcularPctMp1(string? fibra2, string? valpf)
    {
        if (string.IsNullOrWhiteSpace(fibra2) || fibra2 == "0") return 100;
        // Parseo invariante de cultura: replica TO_NUMBER de Oracle (PV_PARSE_PCT), que no
        // depende de la configuración regional del servidor .NET (evita bug silencioso en
        // culturas con coma decimal como es-PE, donde "65.5" fallaría el parseo).
        if (!decimal.TryParse(valpf, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) || v == 0) return 100;
        return v;
    }

    /// <summary>Replica el mapeo de presentación de PKG_COT (bug intencional: solo mira la 1ra letra).</summary>
    private static string MapearPresentacion(string? codigoCorto)
    {
        var p = (codigoCorto ?? "").Trim().ToUpperInvariant();
        if (p.StartsWith('C')) return "CONO";
        if (p.StartsWith('R')) return "RODETE";
        return "MADEJA";
    }

    private async Task<string?> ResolverFibraArtAsync(OracleConnection conn, string fibraCod)
    {
        using var cmd = new OracleCommand($"SELECT {S}PKG_COT.F_FIBRA_TO_ART(:f) FROM DUAL", conn);
        cmd.BindByName = true;
        cmd.Parameters.Add(new OracleParameter(":f", OracleDbType.Varchar2, fibraCod, ParameterDirection.Input));
        var result = await cmd.ExecuteScalarAsync();
        return (result == null || result == DBNull.Value) ? null : result.ToString();
    }

    private async Task<decimal?> ObtenerUltimoMargenAsync(OracleConnection conn, string tipoDoc, int serie, long numero, int item)
    {
        const string sql = @"
            SELECT MARGEN_PCT FROM (
                SELECT MARGEN_PCT FROM {0}COT_HISTORIAL
                WHERE TIPODOC=:tipodoc AND SERIE=:serie AND NUMERO=:numero AND ITEM=:item AND MARGEN_PCT IS NOT NULL
                ORDER BY ID_HIST DESC
            ) WHERE ROWNUM = 1";
        using var cmd = new OracleCommand(string.Format(sql, S), conn);
        cmd.BindByName = true;
        cmd.Parameters.Add(new OracleParameter(":tipodoc", OracleDbType.Varchar2, tipoDoc, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":serie",   OracleDbType.Int32,    serie,   ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":numero",  OracleDbType.Int64,    numero,  ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":item",    OracleDbType.Int32,    item,    ParameterDirection.Input));
        var result = await cmd.ExecuteScalarAsync();
        return (result == null || result == DBNull.Value) ? null : Convert.ToDecimal(result);
    }

    private async Task<CotizacionItemEdicionDto?> LeerEdicionDesdeBdAsync(OracleConnection conn, string tipoDoc, int serie, long numero, int item)
    {
        var sql = $@"
            SELECT D.TITULO, D.PROCESO, D.INTENSIDAD, D.FIBRA1, D.FIBRA2, D.VALPF,
                   D.PRESENTACION, D.CANTIDAD,
                   (SELECT NVL(P.RANGO, D.CANTIDAD) FROM {S}COTIZACION_P P
                     WHERE P.TIPODOC=D.TIPODOC AND P.SERIE=D.SERIE AND P.NUMERO=D.NUMERO
                       AND P.ITEM=D.ITEM AND P.PRECIO_ELEGIDO=1 AND ROWNUM=1) AS RANGO_ELEGIDO
            FROM {S}COTIZACION_D D
            WHERE D.TIPODOC=:tipodoc AND D.SERIE=:serie AND D.NUMERO=:numero AND D.ITEM=:item";

        using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        cmd.Parameters.Add(new OracleParameter(":tipodoc", OracleDbType.Varchar2, tipoDoc, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":serie",   OracleDbType.Int32,    serie,   ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":numero",  OracleDbType.Int64,    numero,  ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":item",    OracleDbType.Int32,    item,    ParameterDirection.Input));

        using var r = await cmd.ExecuteReaderAsync() as OracleDataReader ?? throw new InvalidOperationException("OracleDataReader esperado.");
        if (!await r.ReadAsync()) return null;

        var edicion = new CotizacionItemEdicionDto
        {
            Titulo = GetStr(r, "TITULO"),
            Proceso = GetStr(r, "PROCESO") ?? "01",
            IntensidadCod = GetStr(r, "INTENSIDAD") ?? "3",
            Fibra1 = GetStr(r, "FIBRA1"),
            Fibra2 = GetStr(r, "FIBRA2"),
            Valpf = GetStr(r, "VALPF"),
            Presentacion = GetStr(r, "PRESENTACION") ?? "M",
            CantidadKg = GetNullDec(r, "RANGO_ELEGIDO") ?? GetNullDec(r, "CANTIDAD") ?? 500,
        };

        edicion.MargenPct = await ObtenerUltimoMargenAsync(conn, tipoDoc, serie, numero, item) ?? 30;
        return edicion;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // GUARDADO — historial (COT_HISTORIAL)
    // ══════════════════════════════════════════════════════════════════════════

    private async Task InsertarHistorialAsync(
        OracleConnection conn, string tipoDoc, int serie, long numero, int item, string accion,
        string? titulo, string? proceso, string? intensidad, string? fibra1, string? fibra2, string? valpf,
        string? presentacion, decimal? cantidadKg, int? nplies, decimal? margenPct,
        CotizacionTimelineDto? timeline, string? usuario, string? observacion, long? numeroOrigen,
        RutaTecnicaCabDto? rutaTecnica = null)
    {
        int siguienteVersion;
        using (var cmdVer = new OracleCommand(
            $"SELECT NVL(MAX(NRO_VERSION),0)+1 FROM {S}COT_HISTORIAL WHERE TIPODOC=:tipodoc AND SERIE=:serie AND NUMERO=:numero AND ITEM=:item", conn))
        {
            cmdVer.BindByName = true;
            cmdVer.Parameters.Add(new OracleParameter(":tipodoc", OracleDbType.Varchar2, tipoDoc, ParameterDirection.Input));
            cmdVer.Parameters.Add(new OracleParameter(":serie",   OracleDbType.Int32,    serie,   ParameterDirection.Input));
            cmdVer.Parameters.Add(new OracleParameter(":numero",  OracleDbType.Int64,    numero,  ParameterDirection.Input));
            cmdVer.Parameters.Add(new OracleParameter(":item",    OracleDbType.Int32,    item,    ParameterDirection.Input));
            siguienteVersion = Convert.ToInt32(await cmdVer.ExecuteScalarAsync());
        }

        string? detalleJson = timeline != null ? JsonSerializer.Serialize(timeline.Pasos) : null;
        // Snapshot congelado de la ficha técnica de Preparatoria vigente en este momento — así,
        // aunque luego se edite COT_RUTA_TECNICA_CAB/DET, esta versión guardada no cambia
        // (requisito: "debe guardar todo, desde lo que ingresó preparatoria hasta el final").
        string? rutaTecnicaJson = rutaTecnica != null ? JsonSerializer.Serialize(rutaTecnica) : null;

        var sql = $@"
            INSERT INTO {S}COT_HISTORIAL(
                ID_HIST, TIPODOC, SERIE, NUMERO, ITEM, NRO_VERSION, ACCION, FCH_HIST, USUARIO,
                TITULO, PROCESO, INTENSIDAD, FIBRA1, FIBRA2, VALPF, PRESENTACION, CANTIDAD_KG, NPLIES, MARGEN_PCT,
                COSTO_TOTAL, PRECIO_25, PRECIO_30, PRECIO_35, PRECIO_40, DETALLE_JSON, RUTA_TECNICA_JSON, OBSERVACION, NUMERO_ORIGEN)
            VALUES(
                {S}COT_HISTORIAL_SEQ.NEXTVAL, :tipodoc, :serie, :numero, :item, :nroVersion, :accion, SYSDATE, :usuario,
                :titulo, :proceso, :intensidad, :fibra1, :fibra2, :valpf, :presentacion, :cantidadKg, :nplies, :margenPct,
                :costoTotal, :precio25, :precio30, :precio35, :precio40, :detalleJson, :rutaTecnicaJson, :observacion, :numeroOrigen)";

        using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        cmd.Parameters.Add(new OracleParameter(":tipodoc",     OracleDbType.Varchar2, tipoDoc,           ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":serie",       OracleDbType.Int32,    serie,             ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":numero",      OracleDbType.Int64,    numero,            ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":item",        OracleDbType.Int32,    item,              ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":nroVersion",  OracleDbType.Int32,    siguienteVersion,  ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":accion",      OracleDbType.Varchar2, accion,            ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":usuario",     OracleDbType.Varchar2, (object?)usuario      ?? DBNull.Value, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":titulo",      OracleDbType.Varchar2, (object?)titulo       ?? DBNull.Value, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":proceso",     OracleDbType.Varchar2, (object?)proceso      ?? DBNull.Value, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":intensidad",  OracleDbType.Varchar2, (object?)intensidad   ?? DBNull.Value, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":fibra1",      OracleDbType.Varchar2, (object?)fibra1       ?? DBNull.Value, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":fibra2",      OracleDbType.Varchar2, (object?)fibra2       ?? DBNull.Value, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":valpf",       OracleDbType.Varchar2, (object?)valpf        ?? DBNull.Value, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":presentacion",OracleDbType.Varchar2, (object?)presentacion ?? DBNull.Value, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":cantidadKg",  OracleDbType.Decimal,  (object?)cantidadKg   ?? DBNull.Value, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":nplies",      OracleDbType.Int32,    (object?)nplies       ?? DBNull.Value, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":margenPct",   OracleDbType.Decimal,  (object?)margenPct    ?? DBNull.Value, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":costoTotal",  OracleDbType.Decimal,  (object?)timeline?.CostoTotal ?? DBNull.Value, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":precio25",    OracleDbType.Decimal,  (object?)timeline?.Precio25  ?? DBNull.Value, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":precio30",    OracleDbType.Decimal,  (object?)timeline?.Precio30  ?? DBNull.Value, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":precio35",    OracleDbType.Decimal,  (object?)timeline?.Precio35  ?? DBNull.Value, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":precio40",    OracleDbType.Decimal,  (object?)timeline?.Precio40  ?? DBNull.Value, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":detalleJson", OracleDbType.Clob,     (object?)detalleJson  ?? DBNull.Value, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":rutaTecnicaJson", OracleDbType.Clob, (object?)rutaTecnicaJson ?? DBNull.Value, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":observacion", OracleDbType.Varchar2, (object?)observacion  ?? DBNull.Value, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":numeroOrigen",OracleDbType.Int64,    (object?)numeroOrigen ?? DBNull.Value, ParameterDirection.Input));

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<long> GuardarSimulacionAsync(long? numero, CotizacionParametros parametros, string usuario, string accion, string? observacion, long? numeroOrigen)
    {
        using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();

        long numeroFinal;
        if (numero is null or 0)
        {
            using var cmdSeq = new OracleCommand($"SELECT {S}COT_SIMULACION_SEQ.NEXTVAL FROM DUAL", conn);
            numeroFinal = Convert.ToInt64(await cmdSeq.ExecuteScalarAsync());
        }
        else numeroFinal = numero.Value;

        var pasos = await EjecutarFCotizarAsync(conn, parametros);
        var timeline = ConstruirTimeline(parametros, pasos);
        var rutaTecnica = await _rutaTecnicaService.ObtenerVigenteAsync(parametros.TituloCod, parametros.IntensidadCod);

        await InsertarHistorialAsync(conn, "SM", 0, numeroFinal, 1, accion,
            parametros.TituloCod, parametros.Proceso, parametros.IntensidadCod, parametros.CodArtMp1, parametros.CodArtMp2, parametros.PctMp1.ToString(CultureInfo.InvariantCulture),
            parametros.Presentacion, parametros.CantidadKg, parametros.Nplies, parametros.MargenPct,
            timeline, usuario, observacion, numeroOrigen, rutaTecnica);

        return numeroFinal;
    }

    public async Task GuardarEdicionItemRealAsync(string tipoDoc, int serie, long numero, int item, CotizacionItemEdicionDto edicion, string usuario, string? observacion)
    {
        using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();

        var parametros = await DerivarParametrosAsync(conn, edicion);
        var pasos = await EjecutarFCotizarAsync(conn, parametros);
        var timeline = ConstruirTimeline(parametros, pasos);
        var rutaTecnica = await _rutaTecnicaService.ObtenerVigenteAsync(edicion.Titulo, edicion.IntensidadCod);

        // 1) snapshot ANTES de sobrescribir COTIZACION_D/P (preserva los valores anteriores en versiones previas)
        await InsertarHistorialAsync(conn, tipoDoc, serie, numero, item, "EDICION",
            edicion.Titulo, edicion.Proceso, edicion.IntensidadCod, edicion.Fibra1, edicion.Fibra2, edicion.Valpf,
            edicion.Presentacion, edicion.CantidadKg, parametros.Nplies, edicion.MargenPct,
            timeline, usuario, observacion, null, rutaTecnica);

        // 2) actualizar COTIZACION_D (solo columnas consumidas por PKG_COT)
        using (var cmd = new OracleCommand($@"
            UPDATE {S}COTIZACION_D SET
                TITULO=:titulo, PROCESO=:proceso, INTENSIDAD=:intensidad,
                FIBRA1=:fibra1, FIBRA2=:fibra2, VALPF=:valpf,
                PRESENTACION=:presentacion, CANTIDAD=:cantidad,
                A_MDUSER=:usuario, A_MDFECHA=SYSDATE
            WHERE TIPODOC=:tipodoc AND SERIE=:serie AND NUMERO=:numero AND ITEM=:item", conn))
        {
            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter(":titulo",       OracleDbType.Varchar2, (object?)edicion.Titulo ?? DBNull.Value, ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter(":proceso",      OracleDbType.Varchar2, edicion.Proceso,       ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter(":intensidad",   OracleDbType.Varchar2, edicion.IntensidadCod, ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter(":fibra1",       OracleDbType.Varchar2, (object?)edicion.Fibra1 ?? DBNull.Value, ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter(":fibra2",       OracleDbType.Varchar2, (object?)edicion.Fibra2 ?? DBNull.Value, ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter(":valpf",        OracleDbType.Varchar2, (object?)edicion.Valpf ?? DBNull.Value, ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter(":presentacion", OracleDbType.Varchar2, edicion.Presentacion,  ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter(":cantidad",     OracleDbType.Decimal,  edicion.CantidadKg,    ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter(":usuario",      OracleDbType.Varchar2, (object?)usuario ?? DBNull.Value, ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter(":tipodoc",      OracleDbType.Varchar2, tipoDoc, ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter(":serie",        OracleDbType.Int32,    serie,   ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter(":numero",       OracleDbType.Int64,    numero,  ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter(":item",         OracleDbType.Int32,    item,    ParameterDirection.Input));
            await cmd.ExecuteNonQueryAsync();
        }

        // 3) sincronizar el RANGO del precio elegido con la cantidad editada
        using (var cmd = new OracleCommand($@"
            UPDATE {S}COTIZACION_P SET RANGO=:cantidad
            WHERE TIPODOC=:tipodoc AND SERIE=:serie AND NUMERO=:numero AND ITEM=:item AND PRECIO_ELEGIDO=1", conn))
        {
            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter(":cantidad", OracleDbType.Decimal,  edicion.CantidadKg, ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter(":tipodoc",  OracleDbType.Varchar2, tipoDoc, ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter(":serie",    OracleDbType.Int32,    serie,   ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter(":numero",   OracleDbType.Int64,    numero,  ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter(":item",     OracleDbType.Int32,    item,    ParameterDirection.Input));
            await cmd.ExecuteNonQueryAsync();
        }

        // 4) refrescar costo/rentabilidad oficiales del ERP vía el SP existente (mantiene consistencia)
        using (var cmd = new OracleCommand($"BEGIN {S}PKG_COT.SP_ACTUALIZAR_COSTO_COTIZACION(:numero, :msg); END;", conn))
        {
            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter(":numero", OracleDbType.Int64, numero, ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter(":msg", OracleDbType.Varchar2, 4000) { Direction = ParameterDirection.Output });
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task GuardarEdicionSimulacionAsync(long numero, CotizacionItemEdicionDto edicion, string usuario, string? observacion)
    {
        using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();

        var parametros = await DerivarParametrosAsync(conn, edicion);
        var pasos = await EjecutarFCotizarAsync(conn, parametros);
        var timeline = ConstruirTimeline(parametros, pasos);
        var rutaTecnica = await _rutaTecnicaService.ObtenerVigenteAsync(parametros.TituloCod, parametros.IntensidadCod);

        await InsertarHistorialAsync(conn, "SM", 0, numero, 1, "EDICION",
            parametros.TituloCod, parametros.Proceso, parametros.IntensidadCod, parametros.CodArtMp1, parametros.CodArtMp2, parametros.PctMp1.ToString(CultureInfo.InvariantCulture),
            parametros.Presentacion, parametros.CantidadKg, parametros.Nplies, parametros.MargenPct,
            timeline, usuario, observacion, null, rutaTecnica);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // LECTURA — COTIZACION_G / COTIZACION_D / COTIZACION_P
    // ══════════════════════════════════════════════════════════════════════════

    private async Task<CotizacionHeaderDto?> LeerHeaderAsync(OracleConnection conn, string tipoDoc, int serie, long numero)
    {
        var sql = $@"
            SELECT g.TIPODOC, g.SERIE, g.NUMERO, g.FECHA, g.COD_CLIENTE, c.NOMBRE AS NOMBRE_CLIENTE,
                   g.COD_VENDE, g.ESTADO, g.MONEDA, g.OBSERVACIONES
            FROM {S}COTIZACION_G g
            LEFT JOIN {S}CLIENTES c ON c.COD_CLIENTE = g.COD_CLIENTE
            WHERE g.TIPODOC=:tipodoc AND g.SERIE=:serie AND g.NUMERO=:numero";
        using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        cmd.Parameters.Add(new OracleParameter(":tipodoc", OracleDbType.Varchar2, tipoDoc, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":serie",   OracleDbType.Int32,    serie,   ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":numero",  OracleDbType.Int64,    numero,  ParameterDirection.Input));

        using var r = await cmd.ExecuteReaderAsync() as OracleDataReader ?? throw new InvalidOperationException("OracleDataReader esperado.");
        if (!await r.ReadAsync()) return null;

        return new CotizacionHeaderDto
        {
            TipoDoc = GetStr(r, "TIPODOC") ?? tipoDoc,
            Serie = GetInt(r, "SERIE"),
            Numero = GetLong(r, "NUMERO"),
            Fecha = GetDt(r, "FECHA"),
            CodCliente = GetStr(r, "COD_CLIENTE"),
            NombreCliente = GetStr(r, "NOMBRE_CLIENTE"),
            CodVende = GetStr(r, "COD_VENDE"),
            Estado = GetStr(r, "ESTADO"),
            Moneda = GetStr(r, "MONEDA"),
            Observaciones = GetStr(r, "OBSERVACIONES"),
        };
    }

    private async Task<List<CotizacionItemDto>> LeerItemsAsync(OracleConnection conn, string tipoDoc, int serie, long numero, int? item = null)
    {
        var sql = $@"
            SELECT TIPODOC, SERIE, NUMERO, ITEM, TITULO, PROCESO, INTENSIDAD, FIBRA1, FIBRA2, VALPF,
                   PRESENTACION, CANTIDAD, PRECIO_SUGERIDO, PRECIO_MAX, COLOR_DET, ESTADO
            FROM {S}COTIZACION_D
            WHERE TIPODOC=:tipodoc AND SERIE=:serie AND NUMERO=:numero {(item.HasValue ? "AND ITEM=:item" : "")}
            ORDER BY ITEM";
        using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        cmd.Parameters.Add(new OracleParameter(":tipodoc", OracleDbType.Varchar2, tipoDoc, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":serie",   OracleDbType.Int32,    serie,   ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":numero",  OracleDbType.Int64,    numero,  ParameterDirection.Input));
        if (item.HasValue)
            cmd.Parameters.Add(new OracleParameter(":item", OracleDbType.Int32, item.Value, ParameterDirection.Input));

        var lista = new List<CotizacionItemDto>();
        using var r = await cmd.ExecuteReaderAsync() as OracleDataReader ?? throw new InvalidOperationException("OracleDataReader esperado.");
        while (await r.ReadAsync())
        {
            lista.Add(new CotizacionItemDto
            {
                TipoDoc = GetStr(r, "TIPODOC") ?? tipoDoc,
                Serie = GetInt(r, "SERIE"),
                Numero = GetLong(r, "NUMERO"),
                Item = GetInt(r, "ITEM"),
                Titulo = GetStr(r, "TITULO"),
                Proceso = GetStr(r, "PROCESO"),
                Intensidad = GetStr(r, "INTENSIDAD"),
                Fibra1 = GetStr(r, "FIBRA1"),
                Fibra2 = GetStr(r, "FIBRA2"),
                Valpf = GetStr(r, "VALPF"),
                Presentacion = GetStr(r, "PRESENTACION"),
                Cantidad = GetNullDec(r, "CANTIDAD"),
                PrecioSugerido = GetNullDec(r, "PRECIO_SUGERIDO"),
                PrecioMax = GetNullDec(r, "PRECIO_MAX"),
                ColorDet = GetStr(r, "COLOR_DET"),
                Estado = GetStr(r, "ESTADO"),
            });
        }
        return lista;
    }

    private async Task<CotizacionItemDto?> LeerItemAsync(OracleConnection conn, string tipoDoc, int serie, long numero, int item)
        => (await LeerItemsAsync(conn, tipoDoc, serie, numero, item)).FirstOrDefault();

    private async Task<List<CotizacionPrecioDto>> LeerPreciosAsync(OracleConnection conn, string tipoDoc, int serie, long numero, int item)
    {
        var sql = $@"
            SELECT TIPODOC, SERIE, NUMERO, ITEM, RANGO, PRECIO, PRECIO_MAX, COSTO, PORC_RENT, PORC_RENT_MAX, PRECIO_ELEGIDO
            FROM {S}COTIZACION_P
            WHERE TIPODOC=:tipodoc AND SERIE=:serie AND NUMERO=:numero AND ITEM=:item
            ORDER BY RANGO DESC";
        using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        cmd.Parameters.Add(new OracleParameter(":tipodoc", OracleDbType.Varchar2, tipoDoc, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":serie",   OracleDbType.Int32,    serie,   ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":numero",  OracleDbType.Int64,    numero,  ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":item",    OracleDbType.Int32,    item,    ParameterDirection.Input));

        var lista = new List<CotizacionPrecioDto>();
        using var r = await cmd.ExecuteReaderAsync() as OracleDataReader ?? throw new InvalidOperationException("OracleDataReader esperado.");
        while (await r.ReadAsync())
        {
            lista.Add(new CotizacionPrecioDto
            {
                TipoDoc = GetStr(r, "TIPODOC") ?? tipoDoc,
                Serie = GetInt(r, "SERIE"),
                Numero = GetLong(r, "NUMERO"),
                Item = GetNullInt(r, "ITEM"),
                Rango = GetDec(r, "RANGO"),
                Precio = GetNullDec(r, "PRECIO"),
                PrecioMax = GetNullDec(r, "PRECIO_MAX"),
                Costo = GetNullDec(r, "COSTO"),
                PorcRent = GetNullDec(r, "PORC_RENT"),
                PorcRentMax = GetNullDec(r, "PORC_RENT_MAX"),
                PrecioElegido = GetNullInt(r, "PRECIO_ELEGIDO"),
            });
        }
        return lista;
    }

    private static CotizacionItemEdicionDto MapItemToEdicion(CotizacionItemDto item)
    {
        var elegido = item.Precios.FirstOrDefault(p => p.PrecioElegido == 1);
        return new CotizacionItemEdicionDto
        {
            Titulo = item.Titulo,
            Proceso = item.Proceso ?? "01",
            IntensidadCod = item.Intensidad ?? "3",
            Fibra1 = item.Fibra1,
            Fibra2 = item.Fibra2,
            Valpf = item.Valpf,
            Presentacion = item.Presentacion ?? "M",
            CantidadKg = elegido?.Rango ?? item.Cantidad ?? 500,
            MargenPct = 30,
        };
    }

    // ══════════════════════════════════════════════════════════════════════════
    // HISTORIAL (lectura)
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<List<CotizacionHistorialDto>> ObtenerHistorialAsync(string tipoDoc, int serie, long numero, int? item)
    {
        using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        return await LeerHistorialAsync(conn, tipoDoc, serie, numero, item);
    }

    private async Task<List<CotizacionHistorialDto>> LeerHistorialAsync(OracleConnection conn, string tipoDoc, int serie, long numero, int? item)
    {
        var sql = $@"
            SELECT ID_HIST, TIPODOC, SERIE, NUMERO, ITEM, NRO_VERSION, ACCION, FCH_HIST, USUARIO,
                   TITULO, PROCESO, INTENSIDAD, FIBRA1, FIBRA2, VALPF, PRESENTACION, CANTIDAD_KG, NPLIES, MARGEN_PCT,
                   COSTO_TOTAL, PRECIO_25, PRECIO_30, PRECIO_35, PRECIO_40, DETALLE_JSON, RUTA_TECNICA_JSON, OBSERVACION, NUMERO_ORIGEN
            FROM {S}COT_HISTORIAL
            WHERE TIPODOC=:tipodoc AND SERIE=:serie AND NUMERO=:numero {(item.HasValue ? "AND ITEM=:item" : "")}
            ORDER BY ITEM, NRO_VERSION DESC";
        using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        cmd.Parameters.Add(new OracleParameter(":tipodoc", OracleDbType.Varchar2, tipoDoc, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":serie",   OracleDbType.Int32,    serie,   ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":numero",  OracleDbType.Int64,    numero,  ParameterDirection.Input));
        if (item.HasValue)
            cmd.Parameters.Add(new OracleParameter(":item", OracleDbType.Int32, item.Value, ParameterDirection.Input));

        var lista = new List<CotizacionHistorialDto>();
        using var r = await cmd.ExecuteReaderAsync() as OracleDataReader ?? throw new InvalidOperationException("OracleDataReader esperado.");
        while (await r.ReadAsync())
        {
            lista.Add(new CotizacionHistorialDto
            {
                IdHist = GetLong(r, "ID_HIST"),
                TipoDoc = GetStr(r, "TIPODOC") ?? tipoDoc,
                Serie = GetInt(r, "SERIE"),
                Numero = GetLong(r, "NUMERO"),
                Item = GetInt(r, "ITEM"),
                NroVersion = GetInt(r, "NRO_VERSION"),
                Accion = GetStr(r, "ACCION") ?? "",
                FchHist = GetDt(r, "FCH_HIST") ?? DateTime.MinValue,
                Usuario = GetStr(r, "USUARIO"),
                Titulo = GetStr(r, "TITULO"),
                Proceso = GetStr(r, "PROCESO"),
                Intensidad = GetStr(r, "INTENSIDAD"),
                Fibra1 = GetStr(r, "FIBRA1"),
                Fibra2 = GetStr(r, "FIBRA2"),
                Valpf = GetStr(r, "VALPF"),
                Presentacion = GetStr(r, "PRESENTACION"),
                CantidadKg = GetNullDec(r, "CANTIDAD_KG"),
                Nplies = GetNullInt(r, "NPLIES"),
                MargenPct = GetNullDec(r, "MARGEN_PCT"),
                CostoTotal = GetNullDec(r, "COSTO_TOTAL"),
                Precio25 = GetNullDec(r, "PRECIO_25"),
                Precio30 = GetNullDec(r, "PRECIO_30"),
                Precio35 = GetNullDec(r, "PRECIO_35"),
                Precio40 = GetNullDec(r, "PRECIO_40"),
                DetalleJson = GetClobStr(r, "DETALLE_JSON"),
                RutaTecnicaJson = GetClobStr(r, "RUTA_TECNICA_JSON"),
                Observacion = GetStr(r, "OBSERVACION"),
                NumeroOrigen = GetNullLong(r, "NUMERO_ORIGEN"),
            });
        }
        return lista;
    }

    private async Task<bool> EstaEliminadaInternalAsync(OracleConnection conn, string tipoDoc, int serie, long numero)
    {
        var sql = $@"
            SELECT ACCION FROM (
                SELECT ACCION FROM {S}COT_HISTORIAL
                WHERE TIPODOC=:tipodoc AND SERIE=:serie AND NUMERO=:numero AND ITEM=0
                ORDER BY ID_HIST DESC
            ) WHERE ROWNUM=1";
        using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        cmd.Parameters.Add(new OracleParameter(":tipodoc", OracleDbType.Varchar2, tipoDoc, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":serie",   OracleDbType.Int32,    serie,   ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":numero",  OracleDbType.Int64,    numero,  ParameterDirection.Input));
        var result = await cmd.ExecuteScalarAsync();
        return result != null && result.ToString() == "ELIMINACION";
    }

    // ══════════════════════════════════════════════════════════════════════════
    // LISTADO (Index) — mezcla CT (COTIZACION_G) y SM (COT_HISTORIAL)
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<(List<CotizacionResumenDto> Items, int TotalCount)> ListarAsync(string? buscar, bool incluirEliminadas, int page, int pageSize)
    {
        buscar = string.IsNullOrWhiteSpace(buscar) ? null : buscar.Trim();

        using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();

        // 1) claves marcadas como eliminadas (a nivel de cabecera, ITEM=0), tanto CT como SM
        var eliminadas = new HashSet<(string TipoDoc, int Serie, long Numero)>();
        using (var cmd = new OracleCommand($@"
            SELECT TIPODOC, SERIE, NUMERO FROM (
                SELECT TIPODOC, SERIE, NUMERO, ACCION,
                       ROW_NUMBER() OVER (PARTITION BY TIPODOC, SERIE, NUMERO ORDER BY ID_HIST DESC) RN
                FROM {S}COT_HISTORIAL WHERE ITEM = 0
            ) WHERE RN = 1 AND ACCION = 'ELIMINACION'", conn))
        {
            using var r = await cmd.ExecuteReaderAsync() as OracleDataReader ?? throw new InvalidOperationException("OracleDataReader esperado.");
            while (await r.ReadAsync())
                eliminadas.Add((GetStr(r, "TIPODOC") ?? "", GetInt(r, "SERIE"), GetLong(r, "NUMERO")));
        }

        var resultado = new List<CotizacionResumenDto>();

        // 2) cotizaciones reales (CT) — se limita a las 300 más recientes / coincidentes por performance
        using (var cmd = new OracleCommand($@"
            SELECT * FROM (
                SELECT g.TIPODOC, g.SERIE, g.NUMERO, g.FECHA, g.COD_CLIENTE, c.NOMBRE AS NOMBRE_CLIENTE,
                       (SELECT COUNT(*) FROM {S}COTIZACION_D d
                         WHERE d.TIPODOC=g.TIPODOC AND d.SERIE=g.SERIE AND d.NUMERO=g.NUMERO) AS TOTAL_ITEMS
                FROM {S}COTIZACION_G g
                LEFT JOIN {S}CLIENTES c ON c.COD_CLIENTE = g.COD_CLIENTE
                WHERE (:buscar IS NULL
                       OR TO_CHAR(g.NUMERO) LIKE '%'||:buscar||'%'
                       OR UPPER(NVL(c.NOMBRE,'-')) LIKE '%'||UPPER(:buscar)||'%')
                ORDER BY g.NUMERO DESC
            ) WHERE ROWNUM <= 300", conn))
        {
            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter(":buscar", OracleDbType.Varchar2, (object?)buscar ?? DBNull.Value, ParameterDirection.Input));
            using var r = await cmd.ExecuteReaderAsync() as OracleDataReader ?? throw new InvalidOperationException("OracleDataReader esperado.");
            while (await r.ReadAsync())
            {
                var dto = new CotizacionResumenDto
                {
                    TipoDoc = GetStr(r, "TIPODOC") ?? "CT",
                    Serie = GetInt(r, "SERIE"),
                    Numero = GetLong(r, "NUMERO"),
                    Fecha = GetDt(r, "FECHA"),
                    CodCliente = GetStr(r, "COD_CLIENTE"),
                    NombreCliente = GetStr(r, "NOMBRE_CLIENTE"),
                    TotalItems = GetInt(r, "TOTAL_ITEMS"),
                };
                dto.Eliminada = eliminadas.Contains((dto.TipoDoc, dto.Serie, dto.Numero));
                if (dto.Eliminada && !incluirEliminadas) continue;
                resultado.Add(dto);
            }
        }

        // 3) simulaciones (SM) — agrupadas en memoria desde COT_HISTORIAL (bajo volumen esperado)
        using (var cmd = new OracleCommand($@"
            SELECT TIPODOC, SERIE, NUMERO, ITEM, NRO_VERSION, FCH_HIST, TITULO, CANTIDAD_KG
            FROM {S}COT_HISTORIAL WHERE TIPODOC='SM' ORDER BY NUMERO, ITEM, NRO_VERSION", conn))
        {
            using var r = await cmd.ExecuteReaderAsync() as OracleDataReader ?? throw new InvalidOperationException("OracleDataReader esperado.");
            var grupos = new Dictionary<long, CotizacionResumenDto>();
            while (await r.ReadAsync())
            {
                var numero = GetLong(r, "NUMERO");
                var item = GetInt(r, "ITEM");
                var fch = GetDt(r, "FCH_HIST");

                if (!grupos.TryGetValue(numero, out var dto))
                {
                    dto = new CotizacionResumenDto { TipoDoc = "SM", Serie = 0, Numero = numero, TotalItems = 1, Fecha = fch };
                    grupos[numero] = dto;
                }
                if (dto.UltimaModificacion is null || fch > dto.UltimaModificacion) dto.UltimaModificacion = fch;
                if (item == 1) dto.Titulo = GetStr(r, "TITULO");
            }
            foreach (var dto in grupos.Values)
            {
                dto.Eliminada = eliminadas.Contains((dto.TipoDoc, dto.Serie, dto.Numero));
                if (dto.Eliminada && !incluirEliminadas) continue;
                if (buscar != null &&
                    !dto.Numero.ToString().Contains(buscar, StringComparison.OrdinalIgnoreCase) &&
                    !(dto.Titulo?.Contains(buscar, StringComparison.OrdinalIgnoreCase) ?? false))
                    continue;
                resultado.Add(dto);
            }
        }

        var ordenado = resultado.OrderByDescending(x => x.UltimaModificacion ?? x.Fecha ?? DateTime.MinValue).ToList();
        var total = ordenado.Count;
        var paginado = ordenado.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return (paginado, total);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // DETALLE COMPLETO (cabecera + ítems + línea de tiempo + historial)
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<CotizacionDetalleViewModel?> ObtenerDetalleCompletoAsync(string tipoDoc, int serie, long numero)
    {
        using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();

        var vm = new CotizacionDetalleViewModel
        {
            Eliminada = await EstaEliminadaInternalAsync(conn, tipoDoc, serie, numero),
        };

        if (tipoDoc == "CT")
        {
            var header = await LeerHeaderAsync(conn, tipoDoc, serie, numero);
            if (header is null) return null;
            vm.Header = header;

            var items = await LeerItemsAsync(conn, tipoDoc, serie, numero);

            // El c\u00e1lculo por \u00edtem (precios + margen + F_COTIZAR + historial) es costoso (F_COTIZAR es una
            // funci\u00f3n PL/SQL pesada, ver _MEMORIA_COTIZACION.md) y era 100% secuencial en una sola conexi\u00f3n.
            // Se paraleliza con una conexi\u00f3n independiente por \u00edtem (OracleConnection no soporta comandos
            // concurrentes) y un l\u00edmite de concurrencia para no saturar el pool de Oracle.
            using var throttle = new SemaphoreSlim(Math.Min(items.Count, 5));
            var tareas = items.Select(async item =>
            {
                await throttle.WaitAsync();
                try
                {
                    using var itemConn = new OracleConnection(GetOracleConnectionString());
                    await itemConn.OpenAsync();

                    item.Precios = await LeerPreciosAsync(itemConn, tipoDoc, serie, numero, item.Item);
                    var edicion = MapItemToEdicion(item);
                    edicion.MargenPct = await ObtenerUltimoMargenAsync(itemConn, tipoDoc, serie, numero, item.Item) ?? 30;

                    var parametros = await DerivarParametrosAsync(itemConn, edicion);
                    var pasos = await EjecutarFCotizarAsync(itemConn, parametros);
                    var timeline = ConstruirTimeline(parametros, pasos);
                    var historial = await LeerHistorialAsync(itemConn, tipoDoc, serie, numero, item.Item);

                    return new CotizacionItemDetalleViewModel { Item = item, Timeline = timeline, Historial = historial };
                }
                finally
                {
                    throttle.Release();
                }
            }).ToList();

            var resultados = await Task.WhenAll(tareas);
            vm.Items.AddRange(resultados.OrderBy(x => x.Item.Item));
        }
        else
        {
            var historialTodos = await LeerHistorialAsync(conn, "SM", 0, numero, 1);
            if (historialTodos.Count == 0) return null;
            var ultima = historialTodos.OrderByDescending(h => h.NroVersion).First();

            vm.Header = new CotizacionHeaderDto
            {
                TipoDoc = "SM",
                Serie = 0,
                Numero = numero,
                Fecha = historialTodos.Min(h => h.FchHist),
                NombreCliente = "Simulación (sin cliente asociado)",
            };

            var pasos = string.IsNullOrEmpty(ultima.DetalleJson)
                ? new List<CotizarPasoDto>()
                : JsonSerializer.Deserialize<List<CotizarPasoDto>>(ultima.DetalleJson!) ?? new List<CotizarPasoDto>();

            var timeline = new CotizacionTimelineDto
            {
                Parametros = new CotizacionParametros
                {
                    TituloCod = ultima.Titulo,
                    CodArtMp1 = ultima.Fibra1,
                    CodArtMp2 = ultima.Fibra2,
                    PctMp1 = decimal.TryParse(ultima.Valpf, NumberStyles.Number, CultureInfo.InvariantCulture, out var pct) ? pct : 100,
                    Proceso = ultima.Proceso ?? "01",
                    IntensidadCod = ultima.Intensidad ?? "3",
                    CantidadKg = ultima.CantidadKg ?? 500,
                    Presentacion = ultima.Presentacion ?? "MADEJA",
                    Nplies = ultima.Nplies ?? 1,
                    MargenPct = ultima.MargenPct ?? 30,
                },
                Pasos = pasos,
                CostoTotal = ultima.CostoTotal ?? 0,
                Precio25 = ultima.Precio25 ?? 0,
                Precio30 = ultima.Precio30 ?? 0,
                Precio35 = ultima.Precio35 ?? 0,
                Precio40 = ultima.Precio40 ?? 0,
            };

            var itemDto = new CotizacionItemDto
            {
                TipoDoc = "SM",
                Serie = 0,
                Numero = numero,
                Item = 1,
                Titulo = ultima.Titulo,
                Proceso = ultima.Proceso,
                Intensidad = ultima.Intensidad,
                Fibra1 = ultima.Fibra1,
                Fibra2 = ultima.Fibra2,
                Valpf = ultima.Valpf,
                Presentacion = ultima.Presentacion,
                Cantidad = ultima.CantidadKg,
            };

            vm.Items.Add(new CotizacionItemDetalleViewModel { Item = itemDto, Timeline = timeline, Historial = historialTodos });
        }

        return vm;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ELIMINAR / RESTAURAR / DUPLICAR (app-level, no tocan COTIZACION_G/D/P.ESTADO)
    // ══════════════════════════════════════════════════════════════════════════

    public async Task EliminarAsync(string tipoDoc, int serie, long numero, string usuario, string? observacion)
    {
        using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await InsertarHistorialAsync(conn, tipoDoc, serie, numero, 0, "ELIMINACION",
            null, null, null, null, null, null, null, null, null, null,
            null, usuario, observacion, null);
    }

    public async Task RestaurarAsync(string tipoDoc, int serie, long numero, string usuario)
    {
        using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await InsertarHistorialAsync(conn, tipoDoc, serie, numero, 0, "RESTAURACION",
            null, null, null, null, null, null, null, null, null, null,
            null, usuario, null, null);
    }

    public async Task<long> DuplicarItemComoSimulacionAsync(string tipoDoc, int serie, long numero, int item, string usuario)
    {
        using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();

        CotizacionParametros parametros;
        if (tipoDoc == "CT")
        {
            var itemDto = await LeerItemAsync(conn, tipoDoc, serie, numero, item)
                ?? throw new InvalidOperationException("Ítem no encontrado.");
            itemDto.Precios = await LeerPreciosAsync(conn, tipoDoc, serie, numero, item);
            var edicion = MapItemToEdicion(itemDto);
            parametros = await DerivarParametrosAsync(conn, edicion);
        }
        else
        {
            var historial = await LeerHistorialAsync(conn, tipoDoc, serie, numero, item);
            var ultima = historial.OrderByDescending(h => h.NroVersion).FirstOrDefault()
                ?? throw new InvalidOperationException("Simulación no encontrada.");
            parametros = new CotizacionParametros
            {
                TituloCod = ultima.Titulo,
                CodArtMp1 = ultima.Fibra1,
                CodArtMp2 = ultima.Fibra2,
                PctMp1 = decimal.TryParse(ultima.Valpf, NumberStyles.Number, CultureInfo.InvariantCulture, out var pct) ? pct : 100,
                Proceso = ultima.Proceso ?? "01",
                IntensidadCod = ultima.Intensidad ?? "3",
                CantidadKg = ultima.CantidadKg ?? 500,
                Presentacion = ultima.Presentacion ?? "MADEJA",
                Nplies = ultima.Nplies ?? 1,
                MargenPct = ultima.MargenPct ?? 30,
            };
        }

        long nuevoNumero;
        using (var cmdSeq = new OracleCommand($"SELECT {S}COT_SIMULACION_SEQ.NEXTVAL FROM DUAL", conn))
            nuevoNumero = Convert.ToInt64(await cmdSeq.ExecuteScalarAsync());

        var pasos = await EjecutarFCotizarAsync(conn, parametros);
        var timeline = ConstruirTimeline(parametros, pasos);

        await InsertarHistorialAsync(conn, "SM", 0, nuevoNumero, 1, "DUPLICADO_DESTINO",
            parametros.TituloCod, parametros.Proceso, parametros.IntensidadCod, parametros.CodArtMp1, parametros.CodArtMp2, parametros.PctMp1.ToString(CultureInfo.InvariantCulture),
            parametros.Presentacion, parametros.CantidadKg, parametros.Nplies, parametros.MargenPct,
            timeline, usuario, $"Duplicado desde {tipoDoc}-{serie}-{numero} ítem {item}", numero);

        await InsertarHistorialAsync(conn, tipoDoc, serie, numero, 0, "DUPLICADO_ORIGEN",
            null, null, null, null, null, null, null, null, null, null,
            null, usuario, $"Duplicado hacia simulación SM-{nuevoNumero}", nuevoNumero);

        return nuevoNumero;
    }

    // ── Buscadores (autocomplete) ───────────────────────────────────────────────

    public async Task<List<CotizacionLookupDto>> BuscarTitulosAsync(string? texto)
    {
        using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();

        var filtro = (texto ?? "").Trim().ToUpperInvariant();
        // Trae todos los datos informativos de H_TITULOS (no solo TITULO/DESCRIPCION) para
        // que el usuario vea de un vistazo el título/torsión, cabos, peso, categoría, etc.
        // sin tener que consultar la tabla por su cuenta.
        // Orden por relevancia: primero coincidencias que EMPIEZAN con lo escrito (código o
        // descripción), luego el resto alfabético — así una búsqueda por descripción (ej. el
        // usuario recuerda el nombre pero no el código) no queda enterrada detrás de otros
        // títulos que solo la contienen en medio del texto.
        var sql = $@"
            SELECT * FROM (
                SELECT T.TITULO, T.DESCRIPCION, T.ABREVIADO, T.CATEGORIA, T.ESTADO, T.TIPO,
                       T.PESO, T.EQUINUM, T.TIT, T.CABO, T.TIT_ORIG
                FROM {S}H_TITULOS T
                WHERE (:filtro IS NULL OR UPPER(T.TITULO) LIKE '%'||:filtro||'%' OR UPPER(T.DESCRIPCION) LIKE '%'||:filtro||'%')
                ORDER BY CASE WHEN :filtro IS NULL THEN 0
                              WHEN UPPER(T.TITULO) LIKE :filtro||'%' THEN 0
                              WHEN UPPER(T.DESCRIPCION) LIKE :filtro||'%' THEN 1
                              WHEN UPPER(T.ABREVIADO) LIKE :filtro||'%' THEN 2
                              ELSE 3 END,
                         T.DESCRIPCION
            ) WHERE ROWNUM <= 40";
        using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        cmd.Parameters.Add(new OracleParameter(":filtro", OracleDbType.Varchar2, string.IsNullOrEmpty(filtro) ? null : filtro, ParameterDirection.Input));

        var lista = new List<CotizacionLookupDto>();
        using var r = await cmd.ExecuteReaderAsync() as OracleDataReader ?? throw new InvalidOperationException("OracleDataReader esperado.");
        while (await r.ReadAsync())
        {
            // Resumen informativo con todos los datos de H_TITULOS: categoría/tipo (dominio no
            // documentado, se muestra tal cual viene de BD), título numérico (TIT), cabos (CABO,
            // nro de hebras retorcidas), peso y equivalencia (EQUINUM), y el título "original"
            // (TIT_ORIG) cuando el título vigente es un reemplazo de otro código.
            var categoria = GetStr(r, "CATEGORIA");
            var tipo = GetStr(r, "TIPO");
            var estado = GetStr(r, "ESTADO");
            var abreviado = GetStr(r, "ABREVIADO");
            var peso = GetNullDec(r, "PESO");
            var equinum = GetNullDec(r, "EQUINUM");
            var tit = GetNullDec(r, "TIT");
            var cabo = GetNullDec(r, "CABO");
            var titOrig = GetStr(r, "TIT_ORIG");

            var partes = new List<string>();
            if (!string.IsNullOrWhiteSpace(abreviado)) partes.Add($"Abrev.: {abreviado}");
            if (!string.IsNullOrWhiteSpace(categoria)) partes.Add($"Categoría: {categoria}");
            if (!string.IsNullOrWhiteSpace(tipo)) partes.Add($"Tipo: {tipo}");
            if (tit is not null) partes.Add($"Título num.: {tit}");
            if (cabo is not null) partes.Add($"Cabos: {cabo}");
            if (peso is not null) partes.Add($"Peso: {peso}");
            if (equinum is not null) partes.Add($"Equinum: {equinum}");
            if (!string.IsNullOrWhiteSpace(titOrig)) partes.Add($"Título original: {titOrig}");
            if (!string.IsNullOrWhiteSpace(estado) && estado != "A") partes.Add($"Estado: {estado}");

            lista.Add(new CotizacionLookupDto
            {
                Codigo = GetStr(r, "TITULO") ?? "",
                Descripcion = GetStr(r, "DESCRIPCION") ?? "",
                Extra = partes.Count > 0 ? string.Join(" | ", partes) : null,
            });
        }
        return lista;
    }

    public async Task<List<CotizacionLookupDto>> BuscarMateriaPrimaAsync(string? texto)
    {
        using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();

        var filtro = (texto ?? "").Trim().ToUpperInvariant();
        // Igual que en BuscarTitulosAsync: prioriza coincidencias que EMPIEZAN con el texto
        // escrito (código o descripción) antes que las que solo lo contienen en medio — para
        // que buscar por descripción (ej. "TANGUIS", "PIMA") encuentre el artículo correcto
        // aunque existan muchas variantes con nombres parecidos (orgánico, en conversión, etc.).
        var sql = $@"
            SELECT * FROM (
                SELECT A.COD_ART, A.DESCRIPCION, A.UNIDAD
                FROM {S}ARTICUL A
                WHERE A.TP_ART = 'M' AND A.ESTADO = '0'
                  AND (:filtro IS NULL OR UPPER(A.COD_ART) LIKE '%'||:filtro||'%' OR UPPER(A.DESCRIPCION) LIKE '%'||:filtro||'%')
                ORDER BY CASE WHEN :filtro IS NULL THEN 0
                              WHEN UPPER(A.COD_ART) LIKE :filtro||'%' THEN 0
                              WHEN UPPER(A.DESCRIPCION) LIKE :filtro||'%' THEN 1
                              ELSE 2 END,
                         LENGTH(A.DESCRIPCION),
                         A.DESCRIPCION
            ) WHERE ROWNUM <= 40";
        using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        cmd.Parameters.Add(new OracleParameter(":filtro", OracleDbType.Varchar2, string.IsNullOrEmpty(filtro) ? null : filtro, ParameterDirection.Input));

        var lista = new List<CotizacionLookupDto>();
        using var r = await cmd.ExecuteReaderAsync() as OracleDataReader ?? throw new InvalidOperationException("OracleDataReader esperado.");
        while (await r.ReadAsync())
        {
            lista.Add(new CotizacionLookupDto
            {
                Codigo = GetStr(r, "COD_ART") ?? "",
                Descripcion = GetStr(r, "DESCRIPCION") ?? "",
                Extra = GetStr(r, "UNIDAD"),
            });
        }
        return lista;
    }

    public Task<RutaTecnicaCabDto?> ObtenerRutaTecnicaVigenteAsync(string? tituloCod, string? intensidadCod)
        => _rutaTecnicaService.ObtenerVigenteAsync(tituloCod, intensidadCod);
}
