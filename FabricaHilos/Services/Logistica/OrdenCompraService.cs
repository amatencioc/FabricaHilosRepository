using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using FabricaHilos.Models.Logistica;
using System.Data;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace FabricaHilos.Services.Logistica;

public interface IOrdenCompraService
{
    Task<(List<OrdenCompraDto> Items, int TotalCount)> ObtenerOrdenesAsync(
        string? buscar, DateTime? fechaInicio, DateTime? fechaFin,
        string? estado, int page = 1, int pageSize = 20);

    Task<OrdenCompraDto?> ObtenerOrdenAsync(string tipoDocto, int serie, long numPed);

    Task<List<ItemOrdDto>> ObtenerItemsAsync(string tipoDocto, int serie, long numPed);

    Task<Dictionary<string, string>> ObtenerNombresProveedoresAsync(IEnumerable<string> codigos);

    Task<ProveedorDetalleDto?> ObtenerDetalleProveedorAsync(string codProveedor);

    Task<Dictionary<string, string>> ObtenerDescripcionesCentroCostosAsync(IEnumerable<string> codigos);

    Task<Dictionary<string, string>> ObtenerDescripcionesArticulosAsync(IEnumerable<string> codigos);

    Task<Dictionary<string, string>> ObtenerDescripcionesCondPagAsync(IEnumerable<string> codigos);

    Task<string> ObtenerNombreEmpleadoAsync(string codigo);

    Task ActualizarIdGrupoItemsAsync(string tipoDocto, int serie, long numPed, IEnumerable<string> seleccionItems, long idGrupo);
    Task<long>   ObtenerSiguienteIdGrupoAsync();
    Task AprobarGrupoAsync(long idGrupo);
    Task DesaprobarGrupoAsync(long idGrupo);
    Task LimpiarIdGrupoAsync(long idGrupo);

    // ── Registro Nueva OC ──────────────────────────────────────────────────────
    Task<List<RequisicionPendienteDto>> ObtenerRequisicionesPendientesAsync();
    Task<List<ItemReqPendienteDto>> ObtenerItemsReqPendientesAsync(string tipDoc, int serie, long numReq);
    Task<Dictionary<string, string>> ObtenerTodosProveedoresAsync(string? buscar = null);
    Task<Dictionary<string, string>> ObtenerTodasCondPagAsync();
    Task<List<OpcEntregaDto>> ObtenerOpcEntregaAsync();
    Task<List<DestinoDto>> ObtenerDestinosAsync(string? tipo = null, string? buscar = null);
    Task<List<IgvDto>> ObtenerIgvAsync();
    Task<(long NumPed, string? Error)> RegistrarOcAsync(RegistrarOcRequest req, string usuario);
    Task<string?> AnularOcAsync(string tipoDocto, long numPed, string usuario);

    /// <summary>
    /// Devuelve los ID_GRUPO que están en ITEMREQ vinculados a los ítems de esta O/C
    /// a través de DESP_ITEMREQ, para mostrar los archivos del requerimiento original.
    /// </summary>
    Task<List<long>> ObtenerGruposDeRequisicionesVinculadasAsync(long numPed);

    /// <summary>
    /// Propaga los ID_GRUPO de ITEMREQ a los ITEMORD correspondientes cuando la O/C
    /// se creó después de que se subieron documentos al requerimiento.
    /// Devuelve true si se actualizó al menos un ítem.
    /// </summary>
    Task<bool> PropagateGruposReqToItemOrdAsync(long numPed);

    Task<(FirmaOcDto? Generado, FirmaOcDto? Aprobado)> ObtenerFirmasOcAsync(string tipoDocto, int serie, long numPed);
}

public class OrdenCompraService : OracleServiceBase, IOrdenCompraService
{
    private readonly ILogger<OrdenCompraService> _logger;

    public OrdenCompraService(
        IConfiguration configuration,
        ILogger<OrdenCompraService> logger,
        IHttpContextAccessor httpContextAccessor)
        : base(configuration, httpContextAccessor)
    {
        _logger = logger;
    }

    private static string?  GetStr(OracleDataReader r, string col)      => r[col] == DBNull.Value ? null : r[col]?.ToString();
    private static decimal   GetDec(OracleDataReader r, string col)     => r[col] == DBNull.Value ? 0m   : Convert.ToDecimal(r[col]);
    private static DateTime? GetDt(OracleDataReader r, string col)      => r[col] == DBNull.Value ? null : Convert.ToDateTime(r[col]);
    private static int       GetInt(OracleDataReader r, string col)     => r[col] == DBNull.Value ? 0    : Convert.ToInt32(r[col]);
    private static int?      GetNullInt(OracleDataReader r, string col) => r[col] == DBNull.Value ? null : Convert.ToInt32(r[col]);
    private static long      GetLong(OracleDataReader r, string col)    => r[col] == DBNull.Value ? 0L   : Convert.ToInt64(r[col]);

    // ── LISTADO ────────────────────────────────────────────────────────────────

    public async Task<(List<OrdenCompraDto> Items, int TotalCount)> ObtenerOrdenesAsync(
        string? buscar, DateTime? fechaInicio, DateTime? fechaFin,
        string? estado, int page = 1, int pageSize = 20)
    {
        var items   = new List<OrdenCompraDto>();
        int total   = 0;

        bool hasBuscar   = !string.IsNullOrWhiteSpace(buscar);
        bool hasFechaIni = fechaInicio.HasValue;
        bool hasFechaFin = fechaFin.HasValue;
        bool hasEstado   = !string.IsNullOrWhiteSpace(estado);

        int startRow = (page - 1) * pageSize + 1;
        int endRow   = page * pageSize;

        // Si hay búsqueda de texto libre, no se aplican filtros de fecha
        bool aplicarFechas = !hasBuscar;

        string buscarFilter   = hasBuscar
            ? " AND (UPPER(COD_PROVEED) LIKE '%' || UPPER(:buscar) || '%'" +
              "   OR TO_CHAR(NUM_PED) LIKE '%' || :buscar || '%')"
            : string.Empty;
        string fechaIniFilter = (aplicarFechas && hasFechaIni) ? " AND TRUNC(FECHA) >= TRUNC(:fechaIni)" : string.Empty;
        string fechaFinFilter = (aplicarFechas && hasFechaFin) ? " AND TRUNC(FECHA) <= TRUNC(:fechaFin)" : string.Empty;
        string estadoFilter   = hasEstado ? " AND ESTADO = :estado" : string.Empty;

        string whereClause = $"WHERE 1=1{buscarFilter}{fechaIniFilter}{fechaFinFilter}{estadoFilter}";

        string sql = $@"
            SELECT PAGED.TOTAL_COUNT,
                   PAGED.TIPO_DOCTO, PAGED.SERIE, PAGED.NUM_PED, PAGED.ESTADO, PAGED.FECHA,
                   PAGED.COD_PROVEED, PAGED.COND_PAG, PAGED.MONEDA, PAGED.COD_VENDE,
                   PAGED.PLAZO_ENTREGA, PAGED.DETALLE, PAGED.C_COSTO, PAGED.F_ENTREGA,
                   PAGED.VAL_VENTA, PAGED.IMP_DESCTO, PAGED.IMP_NETO, PAGED.IMP_IGV,
                   PAGED.PRECIO_VTA, PAGED.TOTAL_FACTURADO,
                   PAGED.APROB_GERENCIA, PAGED.F_APROB_GER,
                   PAGED.A_ADUSER, PAGED.A_ADFECHA, PAGED.A_MDUSER, PAGED.A_MDFECHA
            FROM (
                SELECT ROW_NUMBER() OVER (ORDER BY O.FECHA DESC, O.NUM_PED DESC) AS RN,
                       COUNT(*) OVER() AS TOTAL_COUNT,
                       O.TIPO_DOCTO, O.SERIE, O.NUM_PED, O.ESTADO, O.FECHA,
                       O.COD_PROVEED, O.COND_PAG, O.MONEDA, O.COD_VENDE,
                       O.PLAZO_ENTREGA, O.DETALLE, O.C_COSTO, O.F_ENTREGA,
                       O.VAL_VENTA, O.IMP_DESCTO, O.IMP_NETO, O.IMP_IGV,
                       O.PRECIO_VTA, O.TOTAL_FACTURADO,
                       O.APROB_GERENCIA, O.F_APROB_GER,
                       O.A_ADUSER, O.A_ADFECHA, O.A_MDUSER, O.A_MDFECHA
                FROM {S}ORDEN_DE_COMPRA O
                {whereClause}
            ) PAGED
            WHERE PAGED.RN BETWEEN :startRow AND :endRow";

        try
        {
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn);
            cmd.BindByName = true;

            if (hasBuscar)
                cmd.Parameters.Add(new OracleParameter(":buscar",   OracleDbType.Varchar2, buscar,                  ParameterDirection.Input));
            if (aplicarFechas && hasFechaIni)
                cmd.Parameters.Add(new OracleParameter(":fechaIni", OracleDbType.Date,     fechaInicio!.Value.Date, ParameterDirection.Input));
            if (aplicarFechas && hasFechaFin)
                cmd.Parameters.Add(new OracleParameter(":fechaFin", OracleDbType.Date,     fechaFin!.Value.Date,    ParameterDirection.Input));
            if (hasEstado)
                cmd.Parameters.Add(new OracleParameter(":estado",   OracleDbType.Varchar2, estado,                  ParameterDirection.Input));

            cmd.Parameters.Add(new OracleParameter(":startRow", OracleDbType.Int32, startRow, ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter(":endRow",   OracleDbType.Int32, endRow,   ParameterDirection.Input));

            using var reader = await cmd.ExecuteReaderAsync() as OracleDataReader
                ?? throw new InvalidOperationException("OracleDataReader expected");

            bool firstRow = true;
            while (await reader.ReadAsync())
            {
                if (firstRow)
                {
                    total    = GetInt(reader, "TOTAL_COUNT");
                    firstRow = false;
                }
                items.Add(MapOrden(reader));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener órdenes de compra");
        }

        return (items, total);
    }

    // ── CABECERA ───────────────────────────────────────────────────────────────

    public async Task<OrdenCompraDto?> ObtenerOrdenAsync(string tipoDocto, int serie, long numPed)
    {
        string sql  = $@"SELECT TIPO_DOCTO, SERIE, NUM_PED, ESTADO, FECHA, COD_PROVEED,
                               COND_PAG, MONEDA, COD_VENDE, PLAZO_ENTREGA, DETALLE, C_COSTO, F_ENTREGA,
                               VAL_VENTA, IMP_DESCTO, IMP_NETO, IMP_IGV, PRECIO_VTA, TOTAL_FACTURADO,
                               APROB_GERENCIA, F_APROB_GER,
                               A_ADUSER, A_ADFECHA, A_MDUSER, A_MDFECHA
                        FROM {S}ORDEN_DE_COMPRA
                        WHERE TIPO_DOCTO = :tipoDocto AND SERIE = :serie AND NUM_PED = :numPed";
        try
        {
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add("tipoDocto", OracleDbType.Varchar2).Value = tipoDocto;
            cmd.Parameters.Add("serie",     OracleDbType.Int32).Value    = serie;
            cmd.Parameters.Add("numPed",    OracleDbType.Decimal).Value    = numPed;
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
                return MapOrden((OracleDataReader)reader);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener orden de compra {TipoDocto}-{Serie}-{NumPed}", tipoDocto, serie, numPed);
        }
        return null;
    }

    // ── ITEMS ──────────────────────────────────────────────────────────────────

    public async Task<List<ItemOrdDto>> ObtenerItemsAsync(string tipoDocto, int serie, long numPed)
    {
        var items   = new List<ItemOrdDto>();
        string sql  = $@"SELECT I.TIPO_DOCTO, I.SERIE, I.NUM_PED, I.ORDEN,
                               I.COD_ART, I.COD_ORIG, I.UNIDAD, I.DESCRIPCION,
                               I.CANTIDAD, I.SALDO, I.PRECIO, I.POR_DESC1, I.POR_DESC2, I.IMP_VVTA, I.ESTADO,
                               I.ID_GRUPO, I.F_GRUPO,
                               D.NUMREQ, D.ORDEN_REQ,
                               R.CENTRO_COSTO
                        FROM {S}ITEMORD I
                        LEFT JOIN (SELECT COD_ART, ORDEN AS ORDEN_REQ, MAX(NUMREQ) AS NUMREQ
                                   FROM {S}DESP_ITEMREQ
                                   WHERE NRO_DOC_REF = TO_CHAR(:numPed)
                                   GROUP BY COD_ART, ORDEN) D
                               ON D.COD_ART = I.COD_ART AND D.ORDEN_REQ = I.ORDEN
                        LEFT JOIN {S}REQUISICION R ON R.NUMREQ = D.NUMREQ
                        WHERE I.TIPO_DOCTO = :tipoDocto AND I.SERIE = :serie AND I.NUM_PED = :numPed
                        ORDER BY D.NUMREQ NULLS LAST, D.ORDEN_REQ NULLS LAST, I.ORDEN";
        try
        {
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add("tipoDocto", OracleDbType.Varchar2).Value = tipoDocto;
            cmd.Parameters.Add("serie",     OracleDbType.Int32).Value    = serie;
            cmd.Parameters.Add("numPed",    OracleDbType.Decimal).Value    = numPed;
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var r = (OracleDataReader)reader;
                items.Add(new ItemOrdDto
                {
                    TipoDocto   = GetStr(r, "TIPO_DOCTO"),
                    Serie       = GetInt(r, "SERIE"),
                    NumPed      = GetLong(r, "NUM_PED"),
                    Orden       = GetInt(r, "ORDEN"),
                    CodArt      = GetStr(r, "COD_ART"),
                    CodOrig     = GetStr(r, "COD_ORIG"),
                    Unidad      = GetStr(r, "UNIDAD"),
                    Descripcion = GetStr(r, "DESCRIPCION"),
                    Cantidad    = GetDec(r, "CANTIDAD"),
                    Saldo       = GetDec(r, "SALDO"),
                    Precio      = GetDec(r, "PRECIO"),
                    PorDesc1    = GetDec(r, "POR_DESC1"),
                    PorDesc2    = GetDec(r, "POR_DESC2"),
                    ImpVvta     = GetDec(r, "IMP_VVTA"),
                    Estado      = GetStr(r, "ESTADO"),
                    IdGrupo     = r["ID_GRUPO"] == DBNull.Value ? null : Convert.ToInt64(r["ID_GRUPO"]),
                    FAprobado   = r["F_GRUPO"]  == DBNull.Value ? null : Convert.ToDateTime(r["F_GRUPO"]),
                    NumReq      = r["NUMREQ"]    == DBNull.Value ? null : Convert.ToInt64(r["NUMREQ"]),
                    OrdenReq    = r["ORDEN_REQ"] == DBNull.Value ? null : Convert.ToInt32(r["ORDEN_REQ"]),
                    CCosto      = GetStr(r, "CENTRO_COSTO"),
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener ítems de orden de compra {TipoDocto}-{Serie}-{NumPed}", tipoDocto, serie, numPed);
        }
        return items;
    }

    // ── PROVEEDORES ────────────────────────────────────────────────────────────

    public async Task<Dictionary<string, string>> ObtenerNombresProveedoresAsync(IEnumerable<string> codigos)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lista  = codigos.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
        if (lista.Count == 0) return result;

        var inParams = string.Join(",", lista.Select((_, i) => $":p{i}"));
        string sql   = $"SELECT COD_PROVEED, NOMBRE FROM {S}PROVEED WHERE COD_PROVEED IN ({inParams})";
        try
        {
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            for (int i = 0; i < lista.Count; i++)
                cmd.Parameters.Add($"p{i}", OracleDbType.Varchar2).Value = lista[i];
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var cod    = reader[0]?.ToString() ?? "";
                var nombre = reader[1]?.ToString() ?? "";
                if (!string.IsNullOrEmpty(cod))
                    result[cod] = nombre;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener nombres de proveedores");
        }
        return result;
    }

    public async Task<ProveedorDetalleDto?> ObtenerDetalleProveedorAsync(string codProveedor)
    {
        if (string.IsNullOrWhiteSpace(codProveedor)) return null;
        string sql = $"SELECT COD_PROVEED, NOMBRE, RUC, DIRECCION, TELEFONO FROM {S}PROVEED WHERE COD_PROVEED = :cod AND ROWNUM = 1";
        try
        {
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add("cod", OracleDbType.Varchar2).Value = codProveedor;
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new ProveedorDetalleDto
                {
                    Codigo    = GetStr(reader, "COD_PROVEED") ?? "",
                    Nombre    = GetStr(reader, "NOMBRE")      ?? "",
                    Ruc       = GetStr(reader, "RUC")         ?? "",
                    Direccion = GetStr(reader, "DIRECCION")   ?? "",
                    Telefono  = GetStr(reader, "TELEFONO")    ?? ""
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener detalle del proveedor {Cod}", codProveedor);
        }
        return null;
    }

    // ── Mapeo ──────────────────────────────────────────────────────────────────

    private static OrdenCompraDto MapOrden(OracleDataReader r) => new()
    {
        TipoDocto      = GetStr(r, "TIPO_DOCTO"),
        Serie          = GetInt(r, "SERIE"),
        NumPed         = GetLong(r, "NUM_PED"),
        Estado         = GetStr(r, "ESTADO"),
        Fecha          = GetDt(r, "FECHA"),
        CodProveed     = GetStr(r, "COD_PROVEED"),
        CondPag        = GetStr(r, "COND_PAG"),
        Moneda         = GetStr(r, "MONEDA"),
        CodVende       = GetStr(r, "COD_VENDE"),
        PlazoEntrega   = GetNullInt(r, "PLAZO_ENTREGA"),
        Detalle        = GetStr(r, "DETALLE"),
        CCosto         = GetStr(r, "C_COSTO"),
        FEntrega       = GetDt(r, "F_ENTREGA"),
        ValVenta       = GetDec(r, "VAL_VENTA"),
        ImpDescto      = GetDec(r, "IMP_DESCTO"),
        ImpNeto        = GetDec(r, "IMP_NETO"),
        ImpIgv         = GetDec(r, "IMP_IGV"),
        PrecioVta      = GetDec(r, "PRECIO_VTA"),
        TotalFacturado = GetDec(r, "TOTAL_FACTURADO"),
        AprobGerencia  = GetStr(r, "APROB_GERENCIA"),
        FAprobGer      = GetDt(r, "F_APROB_GER"),
        AAduser        = GetStr(r, "A_ADUSER"),
        AAdfecha       = GetDt(r, "A_ADFECHA"),
        AMduser        = GetStr(r, "A_MDUSER"),
        AMdfecha       = GetDt(r, "A_MDFECHA"),
    };

    // ── CENTRO DE COSTOS ───────────────────────────────────────────────────────

    public async Task<Dictionary<string, string>> ObtenerDescripcionesCentroCostosAsync(IEnumerable<string> codigos)
    {
        var lista  = codigos.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (lista.Count == 0) return result;

        var paramNames = lista.Select((_, i) => $":c{i}").ToList();
        var sql = $"SELECT CENTRO_COSTO, NOMBRE FROM {S}CENTRO_DE_COSTOS WHERE CENTRO_COSTO IN ({string.Join(",", paramNames)})";
        try
        {
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            for (int i = 0; i < lista.Count; i++)
                cmd.Parameters.Add(new OracleParameter($":c{i}", OracleDbType.Varchar2) { Value = lista[i] });
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var codigo = reader["CENTRO_COSTO"]?.ToString()?.Trim() ?? "";
                var desc   = reader["NOMBRE"]?.ToString()?.Trim() ?? "";
                if (!string.IsNullOrEmpty(codigo))
                    result[codigo] = desc;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener descripciones de CENTRO_DE_COSTOS");
        }
        return result;
    }

    // ── CONDICIÓN DE PAGO ──────────────────────────────────────────────────

    public async Task<Dictionary<string, string>> ObtenerDescripcionesCondPagAsync(IEnumerable<string> codigos)
    {
        var lista  = codigos.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (lista.Count == 0) return result;

        var paramNames = lista.Select((_, i) => $":c{i}").ToList();
        var sql = $"SELECT COND_PAG, DESCRIPCION FROM {S}CONDPAG WHERE COND_PAG IN ({string.Join(",", paramNames)})";
        try
        {
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            for (int i = 0; i < lista.Count; i++)
                cmd.Parameters.Add(new OracleParameter($":c{i}", OracleDbType.Varchar2) { Value = lista[i] });
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var cod  = reader["COND_PAG"]?.ToString()?.Trim() ?? "";
                var desc = reader["DESCRIPCION"]?.ToString()?.Trim() ?? "";
                if (!string.IsNullOrEmpty(cod))
                    result[cod] = desc;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener descripciones de CONDPAG");
        }
        return result;
    }

    // ── ARTÍCULOS ─────────────────────────────────────────────────────────

    public async Task<Dictionary<string, string>> ObtenerDescripcionesArticulosAsync(IEnumerable<string> codigos)
    {
        var lista  = codigos.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (lista.Count == 0) return result;

        var paramNames = lista.Select((_, i) => $":c{i}").ToList();
        var sql = $"SELECT COD_ART, DESCRIPCION FROM {S}ARTICUL WHERE COD_ART IN ({string.Join(",", paramNames)})";
        try
        {
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            for (int i = 0; i < lista.Count; i++)
                cmd.Parameters.Add(new OracleParameter($":c{i}", OracleDbType.Varchar2) { Value = lista[i] });
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var codArt = reader["COD_ART"]?.ToString()?.Trim() ?? "";
                var desc   = reader["DESCRIPCION"]?.ToString()?.Trim() ?? "";
                if (!string.IsNullOrEmpty(codArt))
                    result[codArt] = desc;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener descripciones de artículos");
        }
        return result;
    }

    // ── EMPLEADOS ─────────────────────────────────────────────────────────

    public async Task<string> ObtenerNombreEmpleadoAsync(string codigo)
    {
        if (string.IsNullOrEmpty(codigo)) return codigo;
        var connStr = GetOracleConnectionString();
        if (string.IsNullOrEmpty(connStr)) return codigo;

        var sql = $"SELECT NOMBRE_CORTO FROM {S}V_PERSONAL WHERE C_CODIGO = :codigo AND ROWNUM = 1";
        try
        {
            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter(":codigo", OracleDbType.Varchar2) { Value = codigo });
            var result = await cmd.ExecuteScalarAsync();
            if (result != null && result != DBNull.Value)
            {
                var nombre = result.ToString()?.Trim();
                return string.IsNullOrEmpty(nombre) ? codigo : nombre;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener nombre de empleado: {Codigo}", codigo);
        }
        return codigo;
    }

    // ── GRUPO / ADJUNTOS ───────────────────────────────────────────────────────

    public async Task ActualizarIdGrupoItemsAsync(string tipoDocto, int serie, long numPed, IEnumerable<string> seleccionItems, long idGrupo)
    {
        // Cada elemento viene como "COD_ART|ORDEN"
        var pares = seleccionItems
            .Where(s => !string.IsNullOrWhiteSpace(s) && s.Contains('|'))
            .Select(s => { var p = s.Split('|'); return (CodArt: p[0].Trim(), Orden: int.Parse(p[1].Trim())); })
            .Distinct()
            .ToList();

        if (pares.Count == 0) return;

        // ITEMORD: WHERE TIPO_DOCTO+SERIE+NUM_PED AND (COD_ART=x AND ORDEN=y OR ...)
        var condItemord = string.Join(" OR ", pares.Select((_, i) => $"(COD_ART = :ca{i} AND ORDEN = :or{i})"));
        var sqlItemord  = $"UPDATE {S}ITEMORD SET ID_GRUPO = :idGrupo" +
                          $" WHERE TIPO_DOCTO = :tipoDocto AND SERIE = :serie AND NUM_PED = :numPed" +
                          $" AND ({condItemord})";

        // ITEMREQ: sincronizar via DESP_ITEMREQ (NRO_DOC_REF = NUM_PED, COD_ART = seleccionados)
        var condCodArt = string.Join(" OR ", pares.Select((_, i) => $"D.COD_ART = :ca{i}"));
        var sqlItemreq  = $@"UPDATE {S}ITEMREQ IR SET ID_GRUPO = :idGrupo
            WHERE EXISTS (
                SELECT 1 FROM {S}DESP_ITEMREQ D
                WHERE D.NUMREQ = IR.NUMREQ AND D.ORDEN = IR.ORDEN
                AND D.NRO_DOC_REF = TO_CHAR(:numPed)
                AND ({condCodArt})
            )";

        try
        {
            await using var con = new OracleConnection(GetOracleConnectionString());
            await con.OpenAsync();
            using var trx = con.BeginTransaction();
            try
            {
                await using var cmd1 = new OracleCommand(sqlItemord, con) { BindByName = true, Transaction = trx };
                cmd1.Parameters.Add(new OracleParameter(":idGrupo",   OracleDbType.Decimal)  { Value = idGrupo   });
                cmd1.Parameters.Add(new OracleParameter(":tipoDocto", OracleDbType.Varchar2) { Value = tipoDocto });
                cmd1.Parameters.Add(new OracleParameter(":serie",     OracleDbType.Int32)    { Value = serie     });
                cmd1.Parameters.Add(new OracleParameter(":numPed",    OracleDbType.Decimal)  { Value = numPed    });
                for (int i = 0; i < pares.Count; i++)
                {
                    cmd1.Parameters.Add(new OracleParameter($":ca{i}", OracleDbType.Varchar2) { Value = pares[i].CodArt });
                    cmd1.Parameters.Add(new OracleParameter($":or{i}", OracleDbType.Int32)    { Value = pares[i].Orden  });
                }
                await cmd1.ExecuteNonQueryAsync();

                await using var cmd2 = new OracleCommand(sqlItemreq, con) { BindByName = true, Transaction = trx };
                cmd2.Parameters.Add(new OracleParameter(":idGrupo", OracleDbType.Decimal) { Value = idGrupo });
                cmd2.Parameters.Add(new OracleParameter(":numPed",  OracleDbType.Decimal) { Value = numPed  });
                for (int i = 0; i < pares.Count; i++)
                    cmd2.Parameters.Add(new OracleParameter($":ca{i}", OracleDbType.Varchar2) { Value = pares[i].CodArt });
                await cmd2.ExecuteNonQueryAsync();

                trx.Commit();
            }
            catch
            {
                trx.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar ID_GRUPO de ítems de O/C");
            throw;
        }
    }

    public async Task<List<long>> ObtenerGruposDeRequisicionesVinculadasAsync(long numPed)
    {
        // Busca ID_GRUPO de los ITEMREQ que están enlazados con esta O/C via DESP_ITEMREQ
        // Solo trae grupos que aún no están en ITEMORD (para no duplicar los ya propagados)
        var sql = $@"SELECT DISTINCT IR.ID_GRUPO
                     FROM {S}ITEMREQ IR
                     JOIN {S}DESP_ITEMREQ D ON D.NUMREQ = IR.NUMREQ AND D.ORDEN = IR.ORDEN
                     WHERE D.NRO_DOC_REF = TO_CHAR(:numPed)
                     AND IR.ID_GRUPO IS NOT NULL
                     AND NOT EXISTS (
                         SELECT 1 FROM {S}ITEMORD O
                         WHERE O.NUM_PED = :numPed AND O.ID_GRUPO = IR.ID_GRUPO
                     )";
        var result = new List<long>();
        try
        {
            await using var con = new OracleConnection(GetOracleConnectionString());
            await con.OpenAsync();
            await using var cmd = new OracleCommand(sql, con) { BindByName = true };
            cmd.Parameters.Add("numPed", OracleDbType.Decimal).Value = numPed;
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                result.Add(Convert.ToInt64(reader[0]));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener grupos de requerimientos vinculados a O/C {NumPed}", numPed);
        }
        return result;
    }

    public async Task<bool> PropagateGruposReqToItemOrdAsync(long numPed)
    {
        // Obtener los pares (COD_ART, ORDEN, ID_GRUPO, F_APROBADO) de ITEMREQ vinculados
        // a esta O/C que aún no tienen ID_GRUPO en ITEMORD
        var sqlSelect = $@"SELECT I.COD_ART, I.ORDEN, IR.ID_GRUPO, IR.F_APROBADO
                           FROM {S}ITEMORD I
                           JOIN {S}DESP_ITEMREQ D ON D.NRO_DOC_REF = TO_CHAR(:numPed)
                               AND D.COD_ART = I.COD_ART AND D.ORDEN = I.ORDEN
                           JOIN {S}ITEMREQ IR ON IR.NUMREQ = D.NUMREQ AND IR.ORDEN = D.ORDEN
                           WHERE I.NUM_PED = :numPed
                             AND I.ID_GRUPO IS NULL
                             AND IR.ID_GRUPO IS NOT NULL";

        var rows = new List<(string CodArt, int Orden, long IdGrupo, DateTime? FAprobado)>();

        await using var con = new OracleConnection(GetOracleConnectionString());
        await con.OpenAsync();

        await using (var cmdSel = new OracleCommand(sqlSelect, con) { BindByName = true })
        {
            cmdSel.Parameters.Add("numPed", OracleDbType.Decimal).Value = numPed;
            await using var reader = await cmdSel.ExecuteReaderAsync() as OracleDataReader;
            while (reader != null && await reader.ReadAsync())
            {
                rows.Add((
                    GetStr(reader, "COD_ART") ?? "",
                    GetInt(reader, "ORDEN"),
                    Convert.ToInt64(reader["ID_GRUPO"]),
                    reader["F_APROBADO"] == DBNull.Value ? null : Convert.ToDateTime(reader["F_APROBADO"])
                ));
            }
        }

        if (rows.Count == 0) return false;

        using var trx = con.BeginTransaction();
        try
        {
            foreach (var row in rows)
            {
                var sqlUpd = $@"UPDATE {S}ITEMORD
                                SET ID_GRUPO = :idGrupo,
                                    F_GRUPO  = :fGrupo
                                WHERE NUM_PED = :numPed
                                  AND COD_ART = :codArt
                                  AND ORDEN   = :orden";
                await using var cmdUpd = new OracleCommand(sqlUpd, con) { BindByName = true, Transaction = trx };
                cmdUpd.Parameters.Add("idGrupo", OracleDbType.Decimal).Value  = row.IdGrupo;
                cmdUpd.Parameters.Add("fGrupo",  OracleDbType.Date).Value     = row.FAprobado.HasValue ? (object)row.FAprobado.Value : DBNull.Value;
                cmdUpd.Parameters.Add("numPed",  OracleDbType.Decimal).Value  = numPed;
                cmdUpd.Parameters.Add("codArt",  OracleDbType.Varchar2).Value = row.CodArt;
                cmdUpd.Parameters.Add("orden",   OracleDbType.Int32).Value    = row.Orden;
                await cmdUpd.ExecuteNonQueryAsync();
            }
            trx.Commit();
        }
        catch
        {
            trx.Rollback();
            throw;
        }

        _logger.LogInformation("PropagateGruposReqToItemOrd: {Count} ítem(s) de ITEMORD actualizados para O/C {NumPed}", rows.Count, numPed);
        return true;
    }

    public async Task<long> ObtenerSiguienteIdGrupoAsync()
    {
        var sql = $"SELECT {S}LG_GRUPO_SEQ.NEXTVAL FROM DUAL";
        try
        {
            await using var con = new OracleConnection(GetOracleConnectionString());
            await con.OpenAsync();
            await using var cmd = new OracleCommand(sql, con);
            var valor = await cmd.ExecuteScalarAsync();
            return Convert.ToInt64(valor);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener siguiente valor de LG_GRUPO_SEQ");
            throw;
        }
    }

    public async Task AprobarGrupoAsync(long idGrupo)
    {
        // ITEMORD: actualiza F_GRUPO
        var sqlItemord  = $"UPDATE {S}ITEMORD SET F_GRUPO = SYSDATE WHERE ID_GRUPO = :idGrupo";
        // ITEMREQ: actualiza F_APROBADO via DESP_ITEMREQ (NRO_DOC_REF enlaza NUM_PED de ITEMORD)
        var sqlItemreq  = $@"UPDATE {S}ITEMREQ IR SET F_APROBADO = SYSDATE
            WHERE IR.ID_GRUPO = :idGrupo
            AND EXISTS (
                SELECT 1 FROM {S}DESP_ITEMREQ D
                JOIN   {S}ITEMORD O ON TO_CHAR(O.NUM_PED) = D.NRO_DOC_REF
                WHERE  D.NUMREQ = IR.NUMREQ AND D.ORDEN = IR.ORDEN
                AND    O.ID_GRUPO = :idGrupo
            )";
        try
        {
            await using var con = new OracleConnection(GetOracleConnectionString());
            await con.OpenAsync();
            using var trx = con.BeginTransaction();
            try
            {
                await using var cmd1 = new OracleCommand(sqlItemord, con) { BindByName = true, Transaction = trx };
                cmd1.Parameters.Add(new OracleParameter(":idGrupo", OracleDbType.Decimal) { Value = idGrupo });
                await cmd1.ExecuteNonQueryAsync();

                await using var cmd2 = new OracleCommand(sqlItemreq, con) { BindByName = true, Transaction = trx };
                cmd2.Parameters.Add(new OracleParameter(":idGrupo", OracleDbType.Decimal) { Value = idGrupo });
                await cmd2.ExecuteNonQueryAsync();

                trx.Commit();
            }
            catch
            {
                trx.Rollback();
                throw;
            }
        }
        catch (Exception ex) { _logger.LogError(ex, "Error al aprobar grupo {IdGrupo}", idGrupo); throw; }
    }

    public async Task DesaprobarGrupoAsync(long idGrupo)
    {
        var sqlItemord  = $"UPDATE {S}ITEMORD SET F_GRUPO = NULL WHERE ID_GRUPO = :idGrupo";
        var sqlItemreq  = $@"UPDATE {S}ITEMREQ IR SET F_APROBADO = NULL
            WHERE IR.ID_GRUPO = :idGrupo
            AND EXISTS (
                SELECT 1 FROM {S}DESP_ITEMREQ D
                JOIN   {S}ITEMORD O ON TO_CHAR(O.NUM_PED) = D.NRO_DOC_REF
                WHERE  D.NUMREQ = IR.NUMREQ AND D.ORDEN = IR.ORDEN
                AND    O.ID_GRUPO = :idGrupo
            )";
        try
        {
            await using var con = new OracleConnection(GetOracleConnectionString());
            await con.OpenAsync();
            using var trx = con.BeginTransaction();
            try
            {
                await using var cmd1 = new OracleCommand(sqlItemord, con) { BindByName = true, Transaction = trx };
                cmd1.Parameters.Add(new OracleParameter(":idGrupo", OracleDbType.Decimal) { Value = idGrupo });
                await cmd1.ExecuteNonQueryAsync();

                await using var cmd2 = new OracleCommand(sqlItemreq, con) { BindByName = true, Transaction = trx };
                cmd2.Parameters.Add(new OracleParameter(":idGrupo", OracleDbType.Decimal) { Value = idGrupo });
                await cmd2.ExecuteNonQueryAsync();

                trx.Commit();
            }
            catch
            {
                trx.Rollback();
                throw;
            }
        }
        catch (Exception ex) { _logger.LogError(ex, "Error al desaprobar grupo {IdGrupo}", idGrupo); throw; }
    }

    public async Task LimpiarIdGrupoAsync(long idGrupo)
    {
        var sqlItemord  = $"UPDATE {S}ITEMORD SET ID_GRUPO = NULL, F_GRUPO = NULL WHERE ID_GRUPO = :idGrupo";
        var sqlItemreq  = $@"UPDATE {S}ITEMREQ IR SET ID_GRUPO = NULL, F_APROBADO = NULL
            WHERE IR.ID_GRUPO = :idGrupo
            AND EXISTS (
                SELECT 1 FROM {S}DESP_ITEMREQ D
                JOIN   {S}ITEMORD O ON TO_CHAR(O.NUM_PED) = D.NRO_DOC_REF
                WHERE  D.NUMREQ = IR.NUMREQ AND D.ORDEN = IR.ORDEN
                AND    O.ID_GRUPO = :idGrupo
            )";
        try
        {
            await using var con = new OracleConnection(GetOracleConnectionString());
            await con.OpenAsync();
            using var trx = con.BeginTransaction();
            try
            {
                // Primero ITEMREQ (mientras ITEMORD.ID_GRUPO aún existe para el JOIN)
                await using var cmd1 = new OracleCommand(sqlItemreq, con) { BindByName = true, Transaction = trx };
                cmd1.Parameters.Add(new OracleParameter(":idGrupo", OracleDbType.Decimal) { Value = idGrupo });
                await cmd1.ExecuteNonQueryAsync();

                // Luego ITEMORD
                await using var cmd2 = new OracleCommand(sqlItemord, con) { BindByName = true, Transaction = trx };
                cmd2.Parameters.Add(new OracleParameter(":idGrupo", OracleDbType.Decimal) { Value = idGrupo });
                await cmd2.ExecuteNonQueryAsync();

                trx.Commit();
            }
            catch
            {
                trx.Rollback();
                throw;
            }
        }
        catch (Exception ex) { _logger.LogError(ex, "Error al limpiar ID_GRUPO {IdGrupo}", idGrupo); throw; }
    }

    // ── ObtenerDestinos (P_OBTENER_DESTINOS) ────────────────────────────────────

    public async Task<List<DestinoDto>> ObtenerDestinosAsync(string? tipo = null, string? buscar = null)
    {
        var result = new List<DestinoDto>();
        try
        {
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"{S}PKG_REG_ORDEN_COMPRA.P_OBTENER_DESTINOS";
            cmd.BindByName  = true;
            cmd.Parameters.Add(new OracleParameter("P_TIPO",   OracleDbType.Varchar2)
                { Value = string.IsNullOrWhiteSpace(tipo)   ? (object)DBNull.Value : tipo.Trim().ToUpper() });
            cmd.Parameters.Add(new OracleParameter("P_BUSCAR", OracleDbType.Varchar2)
                { Value = string.IsNullOrWhiteSpace(buscar) ? (object)DBNull.Value : buscar.Trim() });
            cmd.Parameters.Add(new OracleParameter("P_CURSOR", OracleDbType.RefCursor)
                { Direction = ParameterDirection.Output });
            await using var reader = await cmd.ExecuteReaderAsync() as OracleDataReader
                ?? throw new InvalidOperationException();
            while (await reader.ReadAsync())
            {
                result.Add(new DestinoDto
                {
                    TpDestino   = GetStr(reader, "TP_DESTINO") ?? "",
                    Codigo      = GetStr(reader, "CODIGO")      ?? "",
                    Descripcion = GetStr(reader, "DESCRIPCION") ?? "",
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener destinos tipo={Tipo} buscar={Buscar}", tipo, buscar);
        }
        return result.OrderBy(d => d.Codigo).ToList();
    }

    // ── ObtenerIgvAsync ────────────────────────────────────────────────────────

    public async Task<List<IgvDto>> ObtenerIgvAsync()
    {
        var result = new List<IgvDto>();
        try
        {
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"{S}PKG_REG_ORDEN_COMPRA.P_OBTENER_IGV";
            cmd.BindByName  = true;
            cmd.Parameters.Add(new OracleParameter("P_CURSOR", OracleDbType.RefCursor)
                { Direction = ParameterDirection.Output });
            await using var reader = await cmd.ExecuteReaderAsync() as OracleDataReader
                ?? throw new InvalidOperationException();
            while (await reader.ReadAsync())
            {
                result.Add(new IgvDto
                {
                    Codigo      = GetStr(reader, "CODIGO")      ?? "",
                    Descripcion = GetStr(reader, "DESCRIPCION") ?? "",
                    Valor       = reader.IsDBNull(reader.GetOrdinal("VALOR"))
                                    ? 0m
                                    : Convert.ToDecimal(reader["VALOR"])
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener lista de IGV: {Msg}", ex.Message);
            // Retornar fallback con IGV 18% para que el combo no quede vacío
            return new List<IgvDto>
            {
                new IgvDto { Codigo = "18", Descripcion = "IGV 18%", Valor = 0.18m }
            };
        }
        return result;
    }

    // ── ObtenerRequisicionesPendientes ─────────────────────────────────────────

    public async Task<List<RequisicionPendienteDto>> ObtenerRequisicionesPendientesAsync()
    {
        var result = new List<RequisicionPendienteDto>();
        try
        {
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandType = System.Data.CommandType.StoredProcedure;
            cmd.CommandText = $"{S}PKG_REG_ORDEN_COMPRA.P_OBTENER_REQUISICIONES";
            cmd.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("P_CURSOR", Oracle.ManagedDataAccess.Client.OracleDbType.RefCursor)
                { Direction = System.Data.ParameterDirection.Output });
            await using var reader = await cmd.ExecuteReaderAsync() as OracleDataReader
                ?? throw new InvalidOperationException();
            while (await reader.ReadAsync())
            {
                result.Add(new RequisicionPendienteDto
                {
                    TipDoc          = GetStr(reader, "TIPDOC"),
                    Serie           = GetInt(reader, "SERIE"),
                    NumReq          = GetLong(reader, "NUMREQ"),
                    CentroCosto     = GetStr(reader, "CENTRO_COSTO"),
                    Proveedores     = GetStr(reader, "PROVEEDORES"),
                    Fecha           = GetDt(reader, "FECHA"),
                    FEntrega        = GetDt(reader, "F_ENTREGA"),
                    Responsable     = GetStr(reader, "RESPONSABLE"),
                    Prioridad       = GetStr(reader, "PRIORIDAD"),
                    Observacion     = GetStr(reader, "OBSERVACION"),
                    Estado          = GetStr(reader, "ESTADO"),
                    Destino         = GetStr(reader, "DESTINO"),
                    IndServ         = GetStr(reader, "IND_SERV"),
                    Autoriza        = GetStr(reader, "AUTORIZA"),
                    TotalItems      = GetInt(reader, "TOTAL_ITEMS"),
                    ItemsPendientes = GetInt(reader, "ITEMS_PENDIENTES"),
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener requisiciones pendientes para OC");
        }
        return result;
    }

    // ── ObtenerItemsReqPendientes ──────────────────────────────────────────────

    public async Task<List<ItemReqPendienteDto>> ObtenerItemsReqPendientesAsync(string tipDoc, int serie, long numReq)
    {
        var result = new List<ItemReqPendienteDto>();
        try
        {
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandType = System.Data.CommandType.StoredProcedure;
            cmd.CommandText = $"{S}PKG_REG_ORDEN_COMPRA.P_OBTENER_ITEMS_REQ";
            cmd.BindByName  = true;
            cmd.Parameters.Add(new OracleParameter("P_TIPDOC", OracleDbType.Varchar2)  { Value = tipDoc });
            cmd.Parameters.Add(new OracleParameter("P_SERIE",  OracleDbType.Varchar2)  { Value = serie.ToString() });
            cmd.Parameters.Add(new OracleParameter("P_NUMREQ", OracleDbType.Decimal)   { Value = numReq });
            cmd.Parameters.Add(new OracleParameter("P_CURSOR", OracleDbType.RefCursor) { Direction = System.Data.ParameterDirection.Output });
            await using var reader = await cmd.ExecuteReaderAsync() as OracleDataReader
                ?? throw new InvalidOperationException();
            while (await reader.ReadAsync())
            {
                result.Add(new ItemReqPendienteDto
                {
                    TipDoc        = GetStr(reader, "TIPDOC"),
                    Serie         = GetInt(reader, "SERIE"),
                    NumReq        = GetLong(reader, "NUMREQ"),
                    Orden         = GetInt(reader, "ORDEN"),
                    CodArt        = GetStr(reader, "COD_ART"),
                    Detalle       = GetStr(reader, "DETALLE"),
                    Unidad        = GetStr(reader, "UNIDAD"),
                    Cantidad      = GetDec(reader, "CANTIDAD"),
                    Saldo         = GetDec(reader, "SALDO"),
                    Moneda        = GetStr(reader, "MONEDA"),
                    Precio        = GetDec(reader, "PRECIO"),
                    TpDestino     = GetStr(reader, "TP_DESTINO"),
                    Destino       = GetStr(reader, "DESTINO"),
                    CodSolicita   = GetStr(reader, "COD_SOLICITA"),
                    Marca         = GetStr(reader, "MARCA"),
                    Observaciones = GetStr(reader, "OBSERVACIONES"),
                    DescArticulo  = GetStr(reader, "DESC_ARTICULO"),
                    NumOcPrevio   = GetLong(reader, "NUM_OC_PREVIO"),
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener ítems de requisición pendiente {TipDoc}-{Serie}-{NumReq}", tipDoc, serie, numReq);
        }
        return result;
    }

    // ── ObtenerTodosProveedores ────────────────────────────────────────────────

    public async Task<Dictionary<string, string>> ObtenerTodosProveedoresAsync(string? buscar = null)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"{S}PKG_REG_ORDEN_COMPRA.P_OBTENER_PROVEEDORES";
            cmd.BindByName  = true;
            cmd.Parameters.Add(new OracleParameter("P_BUSCAR", OracleDbType.Varchar2)
                { Value = string.IsNullOrWhiteSpace(buscar) ? (object)DBNull.Value : buscar });
            cmd.Parameters.Add(new OracleParameter("P_CURSOR", OracleDbType.RefCursor)
                { Direction = ParameterDirection.Output });
            await using var reader = await cmd.ExecuteReaderAsync() as OracleDataReader
                ?? throw new InvalidOperationException("OracleDataReader expected");
            while (await reader.ReadAsync())
            {
                var cod    = GetStr(reader, "COD_PROVEED") ?? "";
                var nombre = GetStr(reader, "NOMBRE")      ?? "";
                if (!string.IsNullOrEmpty(cod))
                    result[cod] = nombre;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener lista de proveedores");
        }
        return result;
    }

    // ── ObtenerTodasCondPag ────────────────────────────────────────────────────

    public async Task<Dictionary<string, string>> ObtenerTodasCondPagAsync()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"{S}PKG_REG_ORDEN_COMPRA.P_OBTENER_CONDPAG";
            cmd.Parameters.Add(new OracleParameter("P_CURSOR", OracleDbType.RefCursor)
                { Direction = ParameterDirection.Output });
            await using var reader = await cmd.ExecuteReaderAsync() as OracleDataReader
                ?? throw new InvalidOperationException("OracleDataReader expected");
            while (await reader.ReadAsync())
            {
                var cod  = GetStr(reader, "COND_PAG")    ?? "";
                var desc = GetStr(reader, "DESCRIPCION") ?? "";
                if (!string.IsNullOrEmpty(cod))
                    result[cod] = desc;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener condiciones de pago");
        }
        return result;
    }

    // ── ObtenerOpcEntrega ──────────────────────────────────────────────────────

    public async Task<List<OpcEntregaDto>> ObtenerOpcEntregaAsync()
    {
        var result = new List<OpcEntregaDto>();
        try
        {
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"{S}PKG_REG_ORDEN_COMPRA.P_OBTENER_OPC_ENTREGA";
            cmd.Parameters.Add(new OracleParameter("P_CURSOR", OracleDbType.RefCursor)
                { Direction = ParameterDirection.Output });
            await using var reader = await cmd.ExecuteReaderAsync() as OracleDataReader
                ?? throw new InvalidOperationException("OracleDataReader expected");
            while (await reader.ReadAsync())
            {
                var cod  = GetStr(reader, "OPC_LENTR")   ?? "";
                var desc = GetStr(reader, "DESCRIPCION") ?? "";
                var lref = GetStr(reader, "L_ENTREGA_REF");
                if (!string.IsNullOrEmpty(cod))
                    result.Add(new OpcEntregaDto { OpcLEntrega = cod, Descripcion = desc, LEntregaRef = lref });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener opciones de lugar de entrega");
        }
        return result;
    }

    // ── RegistrarOcAsync ───────────────────────────────────────────────────────

    public async Task<(long NumPed, string? Error)> RegistrarOcAsync(RegistrarOcRequest req, string usuario)
    {
        try
        {
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandType = System.Data.CommandType.StoredProcedure;
            cmd.CommandText = $"{S}PKG_REG_ORDEN_COMPRA.P_REGISTRAR_OC";
            cmd.BindByName  = true;

            cmd.Parameters.Add(new OracleParameter("P_TIPO_DOCTO",  OracleDbType.Varchar2) { Value = req.TipoDocto });
            cmd.Parameters.Add(new OracleParameter("P_FECHA",       OracleDbType.Date)     { Value = req.Fecha.Date });
            cmd.Parameters.Add(new OracleParameter("P_F_ENTREGA",   OracleDbType.Date)     { Value = req.FEntrega.Date });
            cmd.Parameters.Add(new OracleParameter("P_COD_PROVEED", OracleDbType.Varchar2) { Value = req.CodProveed });
            cmd.Parameters.Add(new OracleParameter("P_COND_PAG",    OracleDbType.Varchar2) { Value = req.CondPag });
            cmd.Parameters.Add(new OracleParameter("P_MONEDA",      OracleDbType.Varchar2) { Value = req.Moneda });
            cmd.Parameters.Add(new OracleParameter("P_IMPSTO",      OracleDbType.Decimal)  { Value = req.Impsto });
            cmd.Parameters.Add(new OracleParameter("P_C_COSTO",     OracleDbType.Varchar2) { Value = (object?)req.CCosto ?? DBNull.Value });
            cmd.Parameters.Add(new OracleParameter("P_DETALLE",     OracleDbType.Varchar2) { Value = (object?)req.Detalle ?? DBNull.Value });
            cmd.Parameters.Add(new OracleParameter("P_L_ENTREGA",   OracleDbType.Varchar2) { Value = (object?)req.LEntrega ?? DBNull.Value });
            cmd.Parameters.Add(new OracleParameter("P_C_CODIGO",    OracleDbType.Varchar2) { Value = (object?)req.CCodigo ?? DBNull.Value });
            cmd.Parameters.Add(new OracleParameter("P_USUARIO",     OracleDbType.Varchar2) { Value = usuario });

            // P_ITEMS: Oracle array — usamos el paquete con colección de registros.
            // Como ODP.NET no admite T_ITEMS (tipo de registro anidado de PL/SQL) como parámetro
            // directo, construimos un bloque anónimo PL/SQL que llama al paquete.
            // Se rearma el comando como bloque anónimo.
            cmd.CommandType = System.Data.CommandType.Text;

            // Construir el bloque PL/SQL dinámico
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("DECLARE");
            sb.AppendLine($"  v_items {S}PKG_REG_ORDEN_COMPRA.T_ITEMS;");
            sb.AppendLine("  v_numped NUMBER;");
            sb.AppendLine("  v_error  VARCHAR2(500);");
            sb.AppendLine("BEGIN");
            for (int i = 0; i < req.Items.Count; i++)
            {
                sb.AppendLine($"  v_items({i + 1}).TIPDOC     := :tipdoc{i};");
                sb.AppendLine($"  v_items({i + 1}).SERIE      := :serie{i};");
                sb.AppendLine($"  v_items({i + 1}).NUMREQ     := :numreq{i};");
                sb.AppendLine($"  v_items({i + 1}).ORDEN      := :orden{i};");
                sb.AppendLine($"  v_items({i + 1}).COD_ART    := :codart{i};");
                sb.AppendLine($"  v_items({i + 1}).DETALLE    := :detalle{i};");
                sb.AppendLine($"  v_items({i + 1}).UNIDAD     := :unidad{i};");
                sb.AppendLine($"  v_items({i + 1}).COD_ORIG   := :codorig{i};");
                sb.AppendLine($"  v_items({i + 1}).CANTIDAD   := :cantidad{i};");
                sb.AppendLine($"  v_items({i + 1}).PRECIO     := :precio{i};");
                sb.AppendLine($"  v_items({i + 1}).POR_DESC1  := :pordesc1{i};");
                sb.AppendLine($"  v_items({i + 1}).POR_DESC2  := :pordesc2{i};");
                sb.AppendLine($"  v_items({i + 1}).TP_DESTINO := :tpdestino{i};");
                sb.AppendLine($"  v_items({i + 1}).DESTINO    := :destino{i};");
                sb.AppendLine($"  v_items({i + 1}).C_CODIGO   := :ccodigo{i};");
            }
            sb.AppendLine($"  {S}PKG_REG_ORDEN_COMPRA.P_REGISTRAR_OC(");
            sb.AppendLine("    P_TIPO_DOCTO  => :p_tipo_docto,");
            sb.AppendLine("    P_FECHA       => :p_fecha,");
            sb.AppendLine("    P_F_ENTREGA   => :p_f_entrega,");
            sb.AppendLine("    P_COD_PROVEED => :p_cod_proveed,");
            sb.AppendLine("    P_COND_PAG    => :p_cond_pag,");
            sb.AppendLine("    P_MONEDA      => :p_moneda,");
            sb.AppendLine("    P_IMPSTO      => :p_impsto,");
            sb.AppendLine("    P_C_COSTO     => :p_c_costo,");
            sb.AppendLine("    P_DETALLE     => :p_detalle,");
            sb.AppendLine("    P_OPC_LENTR   => :p_opc_lentr,");
            sb.AppendLine("    P_L_ENTREGA   => :p_l_entrega,");
            sb.AppendLine("    P_C_CODIGO    => :p_c_codigo,");
            sb.AppendLine("    P_USUARIO     => :p_usuario,");
            sb.AppendLine("    P_ITEMS       => v_items,");
            sb.AppendLine("    P_NUM_PED     => v_numped,");
            sb.AppendLine("    P_MSGERROR    => v_error");
            sb.AppendLine("  );");
            sb.AppendLine("  :p_num_ped  := v_numped;");
            sb.AppendLine("  :p_msgerror := v_error;");
            sb.AppendLine("END;");

            cmd.CommandText = sb.ToString();
            cmd.Parameters.Clear();
            cmd.BindByName = true;

            // Parámetros de cabecera
            cmd.Parameters.Add(new OracleParameter("p_tipo_docto",  OracleDbType.Varchar2) { Value = req.TipoDocto });
            cmd.Parameters.Add(new OracleParameter("p_fecha",       OracleDbType.Date)     { Value = req.Fecha.Date });
            cmd.Parameters.Add(new OracleParameter("p_f_entrega",   OracleDbType.Date)     { Value = req.FEntrega.Date });
            cmd.Parameters.Add(new OracleParameter("p_cod_proveed", OracleDbType.Varchar2) { Value = req.CodProveed });
            cmd.Parameters.Add(new OracleParameter("p_cond_pag",    OracleDbType.Varchar2) { Value = req.CondPag });
            cmd.Parameters.Add(new OracleParameter("p_moneda",      OracleDbType.Varchar2) { Value = req.Moneda });
            cmd.Parameters.Add(new OracleParameter("p_impsto",      OracleDbType.Decimal)  { Value = req.Impsto });
            cmd.Parameters.Add(new OracleParameter("p_c_costo",     OracleDbType.Varchar2) { Value = (object?)req.CCosto  ?? DBNull.Value });
            cmd.Parameters.Add(new OracleParameter("p_detalle",     OracleDbType.Varchar2) { Value = (object?)req.Detalle ?? DBNull.Value });
            cmd.Parameters.Add(new OracleParameter("p_opc_lentr",   OracleDbType.Varchar2) { Value = (object?)req.OpcLEntrega ?? DBNull.Value });
            cmd.Parameters.Add(new OracleParameter("p_l_entrega",   OracleDbType.Varchar2) { Value = (object?)req.LEntrega ?? DBNull.Value });
            cmd.Parameters.Add(new OracleParameter("p_c_codigo",    OracleDbType.Varchar2) { Value = (object?)req.CCodigo ?? DBNull.Value });
            cmd.Parameters.Add(new OracleParameter("p_usuario",     OracleDbType.Varchar2) { Value = usuario });

            // Parámetros de ítems
            for (int i = 0; i < req.Items.Count; i++)
            {
                var it = req.Items[i];
                cmd.Parameters.Add(new OracleParameter($"tipdoc{i}",    OracleDbType.Varchar2) { Value = it.TipDoc });
                cmd.Parameters.Add(new OracleParameter($"serie{i}",     OracleDbType.Int32)    { Value = it.Serie });
                cmd.Parameters.Add(new OracleParameter($"numreq{i}",    OracleDbType.Decimal)  { Value = it.NumReq });
                cmd.Parameters.Add(new OracleParameter($"orden{i}",     OracleDbType.Int32)    { Value = it.Orden });
                cmd.Parameters.Add(new OracleParameter($"codart{i}",    OracleDbType.Varchar2) { Value = it.CodArt });
                cmd.Parameters.Add(new OracleParameter($"detalle{i}",   OracleDbType.Varchar2) { Value = (object?)it.Detalle ?? DBNull.Value });
                cmd.Parameters.Add(new OracleParameter($"unidad{i}",    OracleDbType.Varchar2) { Value = (object?)it.Unidad ?? DBNull.Value });
                cmd.Parameters.Add(new OracleParameter($"codorig{i}",   OracleDbType.Varchar2) { Value = (object?)it.CodOrig ?? DBNull.Value });
                cmd.Parameters.Add(new OracleParameter($"cantidad{i}",  OracleDbType.Decimal)  { Value = it.Cantidad });
                cmd.Parameters.Add(new OracleParameter($"precio{i}",    OracleDbType.Decimal)  { Value = it.Precio });
                cmd.Parameters.Add(new OracleParameter($"pordesc1{i}",  OracleDbType.Decimal)  { Value = it.PorDesc1 });
                cmd.Parameters.Add(new OracleParameter($"pordesc2{i}",  OracleDbType.Decimal)  { Value = it.PorDesc2 });
                cmd.Parameters.Add(new OracleParameter($"tpdestino{i}", OracleDbType.Varchar2) { Value = (object?)it.TpDestino ?? DBNull.Value });
                cmd.Parameters.Add(new OracleParameter($"destino{i}",   OracleDbType.Varchar2) { Value = (object?)it.Destino ?? DBNull.Value });
                cmd.Parameters.Add(new OracleParameter($"ccodigo{i}",   OracleDbType.Varchar2) { Value = (object?)it.CCodigo ?? DBNull.Value });
            }

            // Parámetros de salida
            cmd.Parameters.Add(new OracleParameter("p_num_ped",  OracleDbType.Decimal)   { Direction = System.Data.ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_msgerror", OracleDbType.Varchar2, 500, "") { Direction = System.Data.ParameterDirection.Output });

            await cmd.ExecuteNonQueryAsync();

            var rawNum  = cmd.Parameters["p_num_ped"].Value;
            var rawErr  = cmd.Parameters["p_msgerror"].Value;
            string? err = rawErr == DBNull.Value || rawErr is Oracle.ManagedDataAccess.Types.OracleDecimal od && od.IsNull ? null : rawErr?.ToString();
            if (string.IsNullOrWhiteSpace(err)) err = null;

            long numPed = 0;
            if (rawNum != null && rawNum != DBNull.Value)
            {
                if (rawNum is Oracle.ManagedDataAccess.Types.OracleDecimal odc)
                    numPed = odc.IsNull ? 0 : Convert.ToInt64(odc.Value);
                else
                    numPed = Convert.ToInt64(rawNum);
            }

            return (numPed, err);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al registrar OC");
            return (0, $"Error interno: {ex.Message}");
        }
    }

    // ── AnularOcAsync ──────────────────────────────────────────────────────────

    public async Task<string?> AnularOcAsync(string tipoDocto, long numPed, string usuario)
    {
        try
        {
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandType = System.Data.CommandType.Text;
            cmd.BindByName  = true;
            cmd.CommandText = $@"DECLARE
  v_error VARCHAR2(500);
BEGIN
  {S}PKG_REG_ORDEN_COMPRA.P_ANULAR_OC(
    P_TIPO_DOCTO => :p_tipo_docto,
    P_SERIE      => :p_serie,
    P_NUM_PED    => :p_num_ped,
    P_USUARIO    => :p_usuario,
    P_MSGERROR   => v_error
  );
  :p_msgerror := v_error;
END;";
            cmd.Parameters.Add(new OracleParameter("p_tipo_docto", OracleDbType.Varchar2) { Value = tipoDocto });
            cmd.Parameters.Add(new OracleParameter("p_serie",      OracleDbType.Int32)    { Value = 1 });
            cmd.Parameters.Add(new OracleParameter("p_num_ped",    OracleDbType.Decimal)  { Value = numPed });
            cmd.Parameters.Add(new OracleParameter("p_usuario",    OracleDbType.Varchar2) { Value = usuario });
            cmd.Parameters.Add(new OracleParameter("p_msgerror",   OracleDbType.Varchar2, 500, "") { Direction = System.Data.ParameterDirection.Output });

            await cmd.ExecuteNonQueryAsync();

            var rawErr = cmd.Parameters["p_msgerror"].Value;
            string? err = rawErr == DBNull.Value ? null : rawErr?.ToString();
            return string.IsNullOrWhiteSpace(err) ? null : err;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al anular OC {TipoDocto}-1-{NumPed}", tipoDocto, numPed);
            return $"Error interno: {ex.Message}";
        }
    }

    // ── FIRMAS OC ─────────────────────────────────────────────────────────────

    public async Task<(FirmaOcDto? Generado, FirmaOcDto? Aprobado)> ObtenerFirmasOcAsync(
        string tipoDocto, int serie, long numPed)
    {
        var connStr = GetOracleConnectionString();
        if (string.IsNullOrEmpty(connStr)) return (null, null);

        FirmaOcDto? generado = null;
        FirmaOcDto? aprobado = null;

        try
        {
            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();

            // ── Paso 1: obtener A_ADUSER (generó) y APROB_GERENCIA (aprobó) de la O/C ──
            string? codGenerado  = null;
            string? codAprobado  = null;
            DateTime? fechaDoc   = null;
            string?   aprobGer   = null;
            DateTime? fAprobGer  = null;

            var sqlOc = $@"SELECT C_CODIGO, FECHA, APROB_GERENCIA, F_APROB_GER
                           FROM {S}ORDEN_DE_COMPRA
                           WHERE TIPO_DOCTO = :tipoDocto AND SERIE = :serie AND NUM_PED = :numPed
                           AND ROWNUM = 1";
            await using (var cmdOc = new OracleCommand(sqlOc, conn) { BindByName = true })
            {
                cmdOc.Parameters.Add("tipoDocto", OracleDbType.Varchar2).Value = tipoDocto;
                cmdOc.Parameters.Add("serie",     OracleDbType.Decimal).Value  = serie;
                cmdOc.Parameters.Add("numPed",    OracleDbType.Decimal).Value  = numPed;
                await using var r = (OracleDataReader)await cmdOc.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    codGenerado = GetStr(r, "C_CODIGO");
                    codAprobado = "034001"; // C_GERENTE — aprobador fijo definido en PKG_REG_ORDEN_COMPRA
                    fechaDoc    = GetDt(r, "FECHA");
                    aprobGer    = GetStr(r, "APROB_GERENCIA");
                    fAprobGer   = GetDt(r, "F_APROB_GER");
                }
            }

            // ── Paso 2: resolver nombre, cargo de cada usuario ──
            async Task<FirmaOcDto?> BuildFirma(string? cod, string rolEtiqueta)
            {
                if (string.IsNullOrWhiteSpace(cod)) return null;

                var sqlP = $@"SELECT ps.C_CODIGO,
                                     ps.APELLIDO_PATERNO || ' ' || ps.APELLIDO_MATERNO
                                     || ', ' || ps.NOMBRES AS NOMBRE_COMPLETO,
                                     NVL(tc.DESCRIPCION, '') AS CARGO
                              FROM {S}RH_PERSONAS ps
                              JOIN {S}RH_PERSONAL pr ON pr.C_CODIGO = ps.C_CODIGO
                              LEFT JOIN {S}T_CARGO tc ON tc.C_CARGO = pr.C_CARGO
                              WHERE ps.C_CODIGO = :cod AND ROWNUM = 1";
                await using var cmdP = new OracleCommand(sqlP, conn) { BindByName = true };
                cmdP.Parameters.Add("cod", OracleDbType.Varchar2).Value = cod;
                await using var rp = (OracleDataReader)await cmdP.ExecuteReaderAsync();
                if (!await rp.ReadAsync()) return null;

                return new FirmaOcDto
                {
                    Codigo         = GetStr(rp, "C_CODIGO")        ?? cod,
                    NombreCompleto = GetStr(rp, "NOMBRE_COMPLETO") ?? "",
                    Cargo          = GetStr(rp, "CARGO")           ?? "",
                    RolEtiqueta    = rolEtiqueta,
                    Firma          = null,
                    FechaDoc       = fechaDoc,
                    AprobGerencia  = aprobGer,
                    FAprobGer      = fAprobGer
                };
            }

            generado = await BuildFirma(codGenerado, "GENERADO POR");
            aprobado = await BuildFirma(codAprobado, "APROBADO POR");

            // ── Paso 3: leer LONG RAW de firma desde RH_FIRMAS ──
            async Task CargarFirma(FirmaOcDto? dto)
            {
                if (dto == null || string.IsNullOrEmpty(dto.Codigo)) return;
                try
                {
                    await using var cmdF = new OracleCommand(
                        $"SELECT FIRMA FROM {S}RH_FIRMAS WHERE C_CODIGO = :cod", conn)
                    {
                        InitialLONGFetchSize = -1
                    };
                    cmdF.Parameters.Add("cod", OracleDbType.Varchar2, 20).Value = dto.Codigo;
                    await using var rdr = (OracleDataReader)await cmdF.ExecuteReaderAsync();
                    if (await rdr.ReadAsync() && !rdr.IsDBNull(0))
                    {
                        byte[]? bytes = null;
                        var val = rdr.GetValue(0);
                        var typeName = val?.GetType().FullName ?? "null";

                        if (val is byte[] b && b.Length > 0)
                            bytes = b;
                        else if (val is OracleBinary ob && !ob.IsNull)
                            bytes = ob.Value;

                        _logger.LogInformation(
                            "RH_FIRMAS [{Codigo}]: tipo={Type} longitud={Len} primeros={Hex}",
                            dto.Codigo, typeName,
                            bytes?.Length ?? 0,
                            bytes != null ? BitConverter.ToString(bytes, 0, Math.Min(16, bytes.Length)) : "—");

                        if (bytes != null && bytes.Length > 0)
                        {
                            var mime = DetectImageMimeType(bytes);
                            if (mime == "image/tiff")
                            {
                                // TIFF no es soportado por navegadores → convertir a PNG
                                bytes = ConvertirTiffAPng(bytes);
                                mime  = "image/png";
                                _logger.LogInformation("Firma [{Codigo}]: TIFF convertido a PNG ({Len} bytes)", dto.Codigo, bytes.Length);
                            }
                            if (mime != null)
                            {
                                dto.Firma = bytes;
                            }
                            else
                            {
                                _logger.LogWarning(
                                    "Firma de {Codigo}: formato no soportado por navegador. Primeros 16 bytes: {Bytes}",
                                    dto.Codigo, BitConverter.ToString(bytes, 0, Math.Min(16, bytes.Length)));
                            }
                        }
                    }
                    else
                    {
                        _logger.LogInformation("RH_FIRMAS [{Codigo}]: sin registro o columna nula", dto.Codigo);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo leer firma de RH_FIRMAS para {Codigo}", dto.Codigo);
                }
            }

            await CargarFirma(generado);
            await CargarFirma(aprobado);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudieron obtener firmas OC {TipoDocto}-{Serie}-{NumPed}",
                tipoDocto, serie, numPed);
        }

        return (generado, aprobado);
    }

    /// <summary>Devuelve el ordinal de la columna si existe en el reader, -1 si no.</summary>
    private static int TryGetOrdinal(OracleDataReader reader, string columnName)
    {
        try { return reader.GetOrdinal(columnName); }
        catch (IndexOutOfRangeException) { return -1; }
    }

    /// <summary>
    /// Detecta el MIME type de una imagen por sus magic bytes.
    /// Soporta PNG, JPEG, BMP, GIF y TIFF.
    /// Devuelve null si el formato no es soportado por navegadores (ej: WMF, EMF).
    /// </summary>
    public static string? DetectImageMimeType(byte[] data)
    {
        if (data == null || data.Length < 4) return null;
        if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47) return "image/png";
        if (data[0] == 0xFF && data[1] == 0xD8)                                         return "image/jpeg";
        if (data[0] == 0x42 && data[1] == 0x4D)                                         return "image/bmp";
        if (data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46)                     return "image/gif";
        if ((data[0] == 0x49 && data[1] == 0x49) || (data[0] == 0x4D && data[1] == 0x4D)) return "image/tiff";
        // WMF: D7 CD C6 9A  /  EMF: 01 00 00 00 — no soportados por navegadores
        return null;
    }

    /// <summary>
    /// Convierte bytes de imagen TIFF a PNG usando ImageSharp, para que el navegador pueda mostrarla.
    /// Devuelve los bytes PNG, o el array original si la conversión falla.
    /// </summary>
    private byte[] ConvertirTiffAPng(byte[] tiffBytes)
    {
        try
        {
            using var input  = new System.IO.MemoryStream(tiffBytes);
            using var image  = Image.Load(input);
            using var output = new System.IO.MemoryStream();
            image.Save(output, new PngEncoder());
            return output.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo convertir TIFF a PNG (longitud={Len})", tiffBytes.Length);
            return tiffBytes;
        }
    }
}
