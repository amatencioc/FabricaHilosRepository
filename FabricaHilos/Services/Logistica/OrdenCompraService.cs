using Oracle.ManagedDataAccess.Client;
using FabricaHilos.Models.Logistica;
using System.Data;

namespace FabricaHilos.Services.Logistica;

public interface IOrdenCompraService
{
    Task<(List<OrdenCompraDto> Items, int TotalCount)> ObtenerOrdenesAsync(
        string? buscar, DateTime? fechaInicio, DateTime? fechaFin,
        string? estado, int page = 1, int pageSize = 20);

    Task<OrdenCompraDto?> ObtenerOrdenAsync(string tipoDocto, int serie, long numPed);

    Task<List<ItemOrdDto>> ObtenerItemsAsync(string tipoDocto, int serie, long numPed);

    Task<Dictionary<string, string>> ObtenerNombresProveedoresAsync(IEnumerable<string> codigos);

    Task<Dictionary<string, string>> ObtenerDescripcionesCentroCostosAsync(IEnumerable<string> codigos);

    Task<Dictionary<string, string>> ObtenerDescripcionesArticulosAsync(IEnumerable<string> codigos);

    Task<Dictionary<string, string>> ObtenerDescripcionesCondPagAsync(IEnumerable<string> codigos);

    Task<string> ObtenerNombreEmpleadoAsync(string codigo);

    Task ActualizarIdGrupoItemsAsync(string tipoDocto, int serie, long numPed, IEnumerable<string> seleccionItems, long idGrupo);
    Task<long>   ObtenerSiguienteIdGrupoAsync();
    Task AprobarGrupoAsync(long idGrupo);
    Task DesaprobarGrupoAsync(long idGrupo);
    Task LimpiarIdGrupoAsync(long idGrupo);
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
                   PAGED.TIPO_DOCTO, PAGED.SERIE, PAGED.NUM_PED, PAGED.FECHA,
                   PAGED.COD_PROVEED, PAGED.COND_PAG, PAGED.MONEDA, PAGED.COD_VENDE,
                   PAGED.PLAZO_ENTREGA, PAGED.DETALLE, PAGED.C_COSTO, PAGED.F_ENTREGA,
                   PAGED.VAL_VENTA, PAGED.IMP_DESCTO, PAGED.IMP_NETO, PAGED.IMP_IGV,
                   PAGED.PRECIO_VTA, PAGED.TOTAL_FACTURADO,
                   PAGED.APROB_GERENCIA, PAGED.F_APROB_GER,
                   PAGED.A_ADUSER, PAGED.A_ADFECHA, PAGED.A_MDUSER, PAGED.A_MDFECHA
            FROM (
                SELECT ROW_NUMBER() OVER (ORDER BY O.FECHA DESC, O.NUM_PED DESC) AS RN,
                       COUNT(*) OVER() AS TOTAL_COUNT,
                       O.TIPO_DOCTO, O.SERIE, O.NUM_PED, O.FECHA,
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
        string sql  = $@"SELECT TIPO_DOCTO, SERIE, NUM_PED, FECHA, COD_PROVEED,
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
            cmd.Parameters.Add("numPed",    OracleDbType.Int64).Value    = numPed;
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
                               I.CANTIDAD, I.SALDO, I.PRECIO, I.IMP_VVTA, I.ESTADO,
                               I.ID_GRUPO, I.F_GRUPO,
                               D.NUMREQ, D.ORDEN_REQ
                        FROM {S}ITEMORD I
                        LEFT JOIN (SELECT COD_ART, ORDEN AS ORDEN_REQ, MAX(NUMREQ) AS NUMREQ
                                   FROM {S}DESP_ITEMREQ
                                   WHERE NRO_DOC_REF = TO_CHAR(:numPed)
                                   GROUP BY COD_ART, ORDEN) D
                               ON D.COD_ART = I.COD_ART AND D.ORDEN_REQ = I.ORDEN
                        WHERE I.TIPO_DOCTO = :tipoDocto AND I.SERIE = :serie AND I.NUM_PED = :numPed
                        ORDER BY D.NUMREQ NULLS LAST, D.ORDEN_REQ NULLS LAST, I.ORDEN";
        try
        {
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add("tipoDocto", OracleDbType.Varchar2).Value = tipoDocto;
            cmd.Parameters.Add("serie",     OracleDbType.Int32).Value    = serie;
            cmd.Parameters.Add("numPed",    OracleDbType.Int64).Value    = numPed;
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
                    ImpVvta     = GetDec(r, "IMP_VVTA"),
                    Estado      = GetStr(r, "ESTADO"),
                    IdGrupo     = r["ID_GRUPO"] == DBNull.Value ? null : Convert.ToInt64(r["ID_GRUPO"]),
                    FAprobado   = r["F_GRUPO"]  == DBNull.Value ? null : Convert.ToDateTime(r["F_GRUPO"]),
                    NumReq      = r["NUMREQ"]    == DBNull.Value ? null : Convert.ToInt64(r["NUMREQ"]),
                    OrdenReq    = r["ORDEN_REQ"] == DBNull.Value ? null : Convert.ToInt32(r["ORDEN_REQ"]),
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

    // ── Mapeo ──────────────────────────────────────────────────────────────────

    private static OrdenCompraDto MapOrden(OracleDataReader r) => new()
    {
        TipoDocto      = GetStr(r, "TIPO_DOCTO"),
        Serie          = GetInt(r, "SERIE"),
        NumPed         = GetLong(r, "NUM_PED"),
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
                cmd1.Parameters.Add(new OracleParameter(":idGrupo",   OracleDbType.Int64)    { Value = idGrupo   });
                cmd1.Parameters.Add(new OracleParameter(":tipoDocto", OracleDbType.Varchar2) { Value = tipoDocto });
                cmd1.Parameters.Add(new OracleParameter(":serie",     OracleDbType.Int32)    { Value = serie     });
                cmd1.Parameters.Add(new OracleParameter(":numPed",    OracleDbType.Int64)    { Value = numPed    });
                for (int i = 0; i < pares.Count; i++)
                {
                    cmd1.Parameters.Add(new OracleParameter($":ca{i}", OracleDbType.Varchar2) { Value = pares[i].CodArt });
                    cmd1.Parameters.Add(new OracleParameter($":or{i}", OracleDbType.Int32)    { Value = pares[i].Orden  });
                }
                await cmd1.ExecuteNonQueryAsync();

                await using var cmd2 = new OracleCommand(sqlItemreq, con) { BindByName = true, Transaction = trx };
                cmd2.Parameters.Add(new OracleParameter(":idGrupo", OracleDbType.Int64) { Value = idGrupo });
                cmd2.Parameters.Add(new OracleParameter(":numPed",  OracleDbType.Int64) { Value = numPed  });
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
                cmd1.Parameters.Add(new OracleParameter(":idGrupo", OracleDbType.Int64) { Value = idGrupo });
                await cmd1.ExecuteNonQueryAsync();

                await using var cmd2 = new OracleCommand(sqlItemreq, con) { BindByName = true, Transaction = trx };
                cmd2.Parameters.Add(new OracleParameter(":idGrupo", OracleDbType.Int64) { Value = idGrupo });
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
                cmd1.Parameters.Add(new OracleParameter(":idGrupo", OracleDbType.Int64) { Value = idGrupo });
                await cmd1.ExecuteNonQueryAsync();

                await using var cmd2 = new OracleCommand(sqlItemreq, con) { BindByName = true, Transaction = trx };
                cmd2.Parameters.Add(new OracleParameter(":idGrupo", OracleDbType.Int64) { Value = idGrupo });
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
                cmd1.Parameters.Add(new OracleParameter(":idGrupo", OracleDbType.Int64) { Value = idGrupo });
                await cmd1.ExecuteNonQueryAsync();

                // Luego ITEMORD
                await using var cmd2 = new OracleCommand(sqlItemord, con) { BindByName = true, Transaction = trx };
                cmd2.Parameters.Add(new OracleParameter(":idGrupo", OracleDbType.Int64) { Value = idGrupo });
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
}
