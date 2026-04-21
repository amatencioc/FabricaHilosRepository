using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System.Data;
using FabricaHilos.Models.Logistica;

namespace FabricaHilos.Services.Logistica;

public interface IRequisicionService
{
    Task<(List<RequisicionDto> Items, int TotalCount)> ObtenerRequisicionesAsync(
        string? buscar, DateTime? fechaInicio, DateTime? fechaFin,
        string? estado, int page = 1, int pageSize = 20);

    Task<RequisicionDto?> ObtenerRequisicionAsync(string tipDoc, int serie, long numReq);

    Task<List<ItemReqDto>> ObtenerItemsAsync(string tipDoc, int serie, long numReq);

    Task<Dictionary<string, string>> ObtenerNombresPersonalAsync(IEnumerable<string> codigos);

    Task<Dictionary<string, string>> ObtenerDescripcionesArticulosAsync(IEnumerable<string> codigos);

    Task<Dictionary<string, string>> ObtenerDescripcionesTablaAuxiliarAsync(string tipo, IEnumerable<string> codigos);

    Task<Dictionary<string, string>> ObtenerDescripcionesCentroCostosAsync(IEnumerable<string> codigos);

    Task ActualizarIdGrupoItemsAsync(string tipDoc, int serie, long numReq, IEnumerable<int> ordenes, long idGrupo);

    Task<long> ObtenerSiguienteIdGrupoAsync();

    Task AprobarGrupoAsync(long idGrupo);

    Task LimpiarIdGrupoAsync(long idGrupo);

    /// <summary>
    /// Devuelve el progreso general por las 4 etapas del flujo logístico
    /// para un conjunto de requerimientos (tipDoc+serie+numReq).
    /// Clave del diccionario: "TIPDOC|SERIE|NUMREQ"
    /// </summary>
    Task<Dictionary<string, ProgresoGeneralDto>> ObtenerProgresoGeneralAsync(
        IEnumerable<(string TipDoc, int Serie, long NumReq)> claves);
}

public class RequisicionService : IRequisicionService
{
    private readonly string _baseConnectionString;
    private readonly ILogger<RequisicionService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RequisicionService(IConfiguration configuration,
        ILogger<RequisicionService> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _baseConnectionString = configuration.GetConnectionString("OracleConnection")
            ?? throw new InvalidOperationException("Oracle connection string not found.");
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    private string GetOracleConnectionString()
    {
        var oraUser = _httpContextAccessor.HttpContext?.Session.GetString("OracleUser");
        var oraPass = _httpContextAccessor.HttpContext?.Session.GetString("OraclePass");

        if (!string.IsNullOrEmpty(oraUser) && !string.IsNullOrEmpty(oraPass))
        {
            var csb = new OracleConnectionStringBuilder(_baseConnectionString)
            {
                UserID   = oraUser,
                Password = oraPass
            };
            return csb.ToString();
        }
        return _baseConnectionString;
    }

    private static string?   GetStr(OracleDataReader r, string col)     => r[col] == DBNull.Value ? null : r[col]?.ToString();
    private static decimal    GetDec(OracleDataReader r, string col)     => r[col] == DBNull.Value ? 0m   : Convert.ToDecimal(r[col]);
    private static decimal?   GetNullDec(OracleDataReader r, string col) => r[col] == DBNull.Value ? null : Convert.ToDecimal(r[col]);
    private static DateTime?  GetDt(OracleDataReader r, string col)      => r[col] == DBNull.Value ? null : Convert.ToDateTime(r[col]);
    private static int        GetInt(OracleDataReader r, string col)      => r[col] == DBNull.Value ? 0    : Convert.ToInt32(r[col]);
    private static int?       GetNullInt(OracleDataReader r, string col)  => r[col] == DBNull.Value ? null : Convert.ToInt32(r[col]);
    private static long       GetLong(OracleDataReader r, string col)     => r[col] == DBNull.Value ? 0L   : Convert.ToInt64(r[col]);
    private static long?      GetNullLong(OracleDataReader r, string col) => r[col] == DBNull.Value ? null : Convert.ToInt64(r[col]);

    // ── LISTADO ────────────────────────────────────────────────────────────────

    public async Task<(List<RequisicionDto> Items, int TotalCount)> ObtenerRequisicionesAsync(
        string? buscar, DateTime? fechaInicio, DateTime? fechaFin,
        string? estado, int page = 1, int pageSize = 20)
    {
        var connStr   = GetOracleConnectionString();
        var items     = new List<RequisicionDto>();
        int total     = 0;

        bool hasBuscar   = !string.IsNullOrWhiteSpace(buscar);
        bool hasFechaIni = fechaInicio.HasValue;
        bool hasFechaFin = fechaFin.HasValue;
        bool hasEstado   = !string.IsNullOrWhiteSpace(estado);

        int startRow = (page - 1) * pageSize + 1;
        int endRow   = page * pageSize;

        string buscarFilter = hasBuscar
            ? " AND (UPPER(PROVEEDORES) LIKE '%' || UPPER(:buscar) || '%'" +
              "   OR TO_CHAR(NUMREQ)    LIKE '%' || :buscar || '%'" +
              "   OR UPPER(CENTRO_COSTO) LIKE '%' || UPPER(:buscar) || '%')"
            : string.Empty;

        // Si hay búsqueda de texto, no aplicar filtro de fechas (se busca por cualquier fecha)
        bool aplicarFechas = !hasBuscar;
        string fechaIniFilter = (aplicarFechas && hasFechaIni) ? " AND TRUNC(FECHA) >= TRUNC(:fechaIni)" : string.Empty;
        string fechaFinFilter = (aplicarFechas && hasFechaFin) ? " AND TRUNC(FECHA) <= TRUNC(:fechaFin)" : string.Empty;
        string estadoFilter   = hasEstado ? " AND ESTADO = :estado" : string.Empty;

        // Excluir estados cerrado/anulado salvo cuando: hay búsqueda de texto,
        // o cuando el filtro de estado es explícitamente '6' (cerrada).
        bool filtraCerrada = hasEstado && estado == "6";
        string baseEstadoFilter = (hasBuscar || filtraCerrada) ? string.Empty : " AND ESTADO NOT IN ('6','9')";

        string whereClause = $"WHERE 1=1{baseEstadoFilter}{buscarFilter}{fechaIniFilter}{fechaFinFilter}{estadoFilter}";

        string sql = $@"
            SELECT PAGED.TOTAL_COUNT,
                   PAGED.TIPDOC, PAGED.SERIE, PAGED.NUMREQ, PAGED.CENTRO_COSTO,
                   PAGED.PROVEEDORES, PAGED.FECHA, PAGED.F_ENTREGA, PAGED.RESPONSABLE,
                   PAGED.PRIORIDAD, PAGED.OBSERVACION, PAGED.ESTADO, PAGED.DESTINO,
                   PAGED.IND_SERV, PAGED.IMPSTO, PAGED.AFECTO_IGV, PAGED.AFECTO_IRENTA,
                   PAGED.TIP_REF, PAGED.SER_REF, PAGED.NRO_REF,
                   PAGED.F_AUTORIZA, PAGED.AUTORIZA, PAGED.USER_AUTORIZA, PAGED.IP_AUTORIZA,
                   PAGED.F_RECIBE, PAGED.RECIBE,
                   PAGED.FCH_ENTREGA_LOGIST, PAGED.NOTA_ANULACION,
                   PAGED.A_ADUSER, PAGED.A_ADFECHA, PAGED.A_MDUSER, PAGED.A_MDFECHA,
                   PAGED.TOTAL_ITEMS, PAGED.ITEMS_CON_GRUPO
            FROM (
                SELECT ROW_NUMBER() OVER (ORDER BY R.FECHA DESC, R.NUMREQ DESC) AS RN,
                       COUNT(*) OVER() AS TOTAL_COUNT,
                       R.TIPDOC, R.SERIE, R.NUMREQ, R.CENTRO_COSTO, R.PROVEEDORES,
                       R.FECHA, R.F_ENTREGA, R.RESPONSABLE, R.PRIORIDAD, R.OBSERVACION,
                       R.ESTADO, R.DESTINO, R.IND_SERV, R.IMPSTO, R.AFECTO_IGV, R.AFECTO_IRENTA,
                       R.TIP_REF, R.SER_REF, R.NRO_REF,
                       R.F_AUTORIZA, R.AUTORIZA, R.USER_AUTORIZA, R.IP_AUTORIZA,
                       R.F_RECIBE, R.RECIBE,
                       R.FCH_ENTREGA_LOGIST, R.NOTA_ANULACION,
                       R.A_ADUSER, R.A_ADFECHA, R.A_MDUSER, R.A_MDFECHA,
                       (SELECT COUNT(*)   FROM ITEMREQ I WHERE I.TIPDOC=R.TIPDOC AND I.SERIE=R.SERIE AND I.NUMREQ=R.NUMREQ) AS TOTAL_ITEMS,
                       (SELECT COUNT(*)   FROM ITEMREQ I WHERE I.TIPDOC=R.TIPDOC AND I.SERIE=R.SERIE AND I.NUMREQ=R.NUMREQ AND I.ID_GRUPO IS NOT NULL) AS ITEMS_CON_GRUPO
                FROM REQUISICION R
                {whereClause}
            ) PAGED
            WHERE PAGED.RN BETWEEN :startRow AND :endRow";

        try
        {
            using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();
            using var cmd = new OracleCommand(sql, conn);
            cmd.BindByName = true;

            if (hasBuscar)
                cmd.Parameters.Add(new OracleParameter(":buscar",    OracleDbType.Varchar2, buscar,                  ParameterDirection.Input));
            if (aplicarFechas && hasFechaIni)
                cmd.Parameters.Add(new OracleParameter(":fechaIni",  OracleDbType.Date,     fechaInicio!.Value.Date, ParameterDirection.Input));
            if (aplicarFechas && hasFechaFin)
                cmd.Parameters.Add(new OracleParameter(":fechaFin",  OracleDbType.Date,     fechaFin!.Value.Date,    ParameterDirection.Input));
            if (hasEstado)
                cmd.Parameters.Add(new OracleParameter(":estado",    OracleDbType.Varchar2, estado,                  ParameterDirection.Input));

            cmd.Parameters.Add(new OracleParameter(":startRow", OracleDbType.Int32, startRow, ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter(":endRow",   OracleDbType.Int32, endRow,   ParameterDirection.Input));

            _logger.LogDebug("ObtenerRequisicionesAsync SQL: {Sql} | buscar={Buscar} fIni={FIni} fFin={FFin} estado={Estado} startRow={S} endRow={E}",
                sql, buscar, fechaInicio, fechaFin, estado, startRow, endRow);

            using var r = await cmd.ExecuteReaderAsync() as OracleDataReader
                ?? throw new InvalidOperationException("OracleDataReader expected");

            while (await r.ReadAsync())
            {
                if (total == 0)
                    total = GetInt(r, "TOTAL_COUNT");
                var dto = MapRequisicion(r);
                dto.TotalItems    = GetInt(r, "TOTAL_ITEMS");
                dto.ItemsConGrupo = GetInt(r, "ITEMS_CON_GRUPO");
                items.Add(dto);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener requisiciones");
        }

        return (items, total);
    }

    // ── CABECERA ───────────────────────────────────────────────────────────────

    public async Task<RequisicionDto?> ObtenerRequisicionAsync(string tipDoc, int serie, long numReq)
    {
        const string sql = @"
            SELECT TIPDOC, SERIE, NUMREQ, CENTRO_COSTO, PROVEEDORES,
                   FECHA, F_ENTREGA, RESPONSABLE, PRIORIDAD, OBSERVACION,
                   ESTADO, DESTINO, IND_SERV, IMPSTO, AFECTO_IGV, AFECTO_IRENTA,
                   TIP_REF, SER_REF, NRO_REF,
                   F_AUTORIZA, AUTORIZA, USER_AUTORIZA, IP_AUTORIZA,
                   F_RECIBE, RECIBE,
                   FCH_ENTREGA_LOGIST, NOTA_ANULACION,
                   A_ADUSER, A_ADFECHA, A_MDUSER, A_MDFECHA
            FROM REQUISICION
            WHERE TIPDOC = :tipDoc AND SERIE = :serie AND NUMREQ = :numReq";

        try
        {
            using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();
            using var cmd = new OracleCommand(sql, conn);
            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter(":tipDoc", OracleDbType.Varchar2, tipDoc, ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter(":serie",  OracleDbType.Int32,    serie,  ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter(":numReq", OracleDbType.Int64,    numReq, ParameterDirection.Input));

            using var r = await cmd.ExecuteReaderAsync() as OracleDataReader
                ?? throw new InvalidOperationException("OracleDataReader expected");
            if (await r.ReadAsync())
                return MapRequisicion(r);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener cabecera requisición {TipDoc}/{Serie}/{NumReq}", tipDoc, serie, numReq);
        }
        return null;
    }

    // ── ITEMS ──────────────────────────────────────────────────────────────────

    public async Task<List<ItemReqDto>> ObtenerItemsAsync(string tipDoc, int serie, long numReq)
    {
        const string sql = @"
            SELECT TIPDOC, SERIE, NUMREQ, ORDEN, COD_ART, DETALLE, UNIDAD, MARCA, CTACTBLE,
                   CANTIDAD, SALDO, STK_MIN, STK_HIST,
                   MONEDA, PRECIO,
                   TP_DESTINO, DESTINO, COD_SOLICITA,
                   ID_GRUPO, F_APROBADO, OBSERVACIONES,
                   A_ADUSER, A_ADFECHA, A_MDUSER, A_MDFECHA
            FROM ITEMREQ
            WHERE TIPDOC = :tipDoc AND SERIE = :serie AND NUMREQ = :numReq
            ORDER BY ORDEN";

        var items = new List<ItemReqDto>();
        try
        {
            using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();
            using var cmd = new OracleCommand(sql, conn);
            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter(":tipDoc", OracleDbType.Varchar2, tipDoc, ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter(":serie",  OracleDbType.Int32,    serie,  ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter(":numReq", OracleDbType.Int64,    numReq, ParameterDirection.Input));

            using var r = await cmd.ExecuteReaderAsync() as OracleDataReader
                ?? throw new InvalidOperationException("OracleDataReader expected");
            while (await r.ReadAsync())
                items.Add(MapItem(r));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener ítems de requisición {TipDoc}/{Serie}/{NumReq}", tipDoc, serie, numReq);
        }
        return items;
    }

    // ── MAPPERS ────────────────────────────────────────────────────────────────

    private static RequisicionDto MapRequisicion(OracleDataReader r) => new()
    {
        TipDoc           = GetStr(r, "TIPDOC"),
        Serie            = GetInt(r, "SERIE"),
        NumReq           = GetLong(r, "NUMREQ"),
        CentroCosto      = GetStr(r, "CENTRO_COSTO"),
        Proveedores      = GetStr(r, "PROVEEDORES"),
        Fecha            = GetDt(r, "FECHA"),
        FEntrega         = GetDt(r, "F_ENTREGA"),
        Responsable      = GetStr(r, "RESPONSABLE"),
        Prioridad        = GetStr(r, "PRIORIDAD"),
        Observacion      = GetStr(r, "OBSERVACION"),
        Estado           = GetStr(r, "ESTADO"),
        Destino          = GetStr(r, "DESTINO"),
        IndServ          = GetStr(r, "IND_SERV"),
        Impsto           = GetDec(r, "IMPSTO"),
        AfectoIgv        = GetStr(r, "AFECTO_IGV"),
        AfectoIrenta     = GetStr(r, "AFECTO_IRENTA"),
        TipRef           = GetStr(r, "TIP_REF"),
        SerRef           = GetNullInt(r, "SER_REF"),
        NroRef           = GetNullLong(r, "NRO_REF"),
        FAutoriza        = GetDt(r, "F_AUTORIZA"),
        Autoriza         = GetStr(r, "AUTORIZA"),
        UserAutoriza     = GetStr(r, "USER_AUTORIZA"),
        IpAutoriza       = GetStr(r, "IP_AUTORIZA"),
        FRecibe          = GetDt(r, "F_RECIBE"),
        Recibe           = GetStr(r, "RECIBE"),
        FchEntregaLogist = GetDt(r, "FCH_ENTREGA_LOGIST"),
        NotaAnulacion    = GetStr(r, "NOTA_ANULACION"),
        AAduser          = GetStr(r, "A_ADUSER"),
        AAdfecha         = GetDt(r, "A_ADFECHA"),
        AMduser          = GetStr(r, "A_MDUSER"),
        AMdfecha         = GetDt(r, "A_MDFECHA"),
    };

    private static ItemReqDto MapItem(OracleDataReader r) => new()
    {
        TipDoc        = GetStr(r, "TIPDOC"),
        Serie         = GetInt(r, "SERIE"),
        NumReq        = GetLong(r, "NUMREQ"),
        Orden         = GetInt(r, "ORDEN"),
        CodArt        = GetStr(r, "COD_ART"),
        Detalle       = GetStr(r, "DETALLE"),
        Unidad        = GetStr(r, "UNIDAD"),
        Marca         = GetStr(r, "MARCA"),
        CtaCtble      = GetStr(r, "CTACTBLE"),
        Cantidad      = GetDec(r, "CANTIDAD"),
        Saldo         = GetDec(r, "SALDO"),
        StkMin        = GetNullDec(r, "STK_MIN"),
        StkHist       = GetNullDec(r, "STK_HIST"),
        Moneda        = GetStr(r, "MONEDA"),
        Precio        = GetDec(r, "PRECIO"),
        TpDestino     = GetStr(r, "TP_DESTINO"),
        Destino       = GetStr(r, "DESTINO"),
        CodSolicita   = GetStr(r, "COD_SOLICITA"),
        IdGrupo       = GetNullLong(r, "ID_GRUPO"),
        FAprobado     = GetDt(r, "F_APROBADO"),
        Observaciones = GetStr(r, "OBSERVACIONES"),
        AAduser       = GetStr(r, "A_ADUSER"),
        AAdfecha      = GetDt(r, "A_ADFECHA"),
        AMduser       = GetStr(r, "A_MDUSER"),
        AMdfecha      = GetDt(r, "A_MDFECHA"),
    };

public async Task<Dictionary<string, string>> ObtenerNombresPersonalAsync(IEnumerable<string> codigos)
{
    var lista = codigos.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    if (lista.Count == 0) return result;

    var paramNames = lista.Select((_, i) => $":c{i}").ToList();
    var sql = $"SELECT C_CODIGO, NOMBRE_CORTO FROM V_PERSONAL WHERE SITUACION = '1' AND C_CODIGO IN ({string.Join(",", paramNames)})";

    try
    {
        await using var con = new OracleConnection(GetOracleConnectionString());
        await con.OpenAsync();
        await using var cmd = new OracleCommand(sql, con) { BindByName = true };
        for (int i = 0; i < lista.Count; i++)
            cmd.Parameters.Add(new OracleParameter($":c{i}", OracleDbType.Varchar2) { Value = lista[i] });

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var codigo = reader["C_CODIGO"]?.ToString()?.Trim() ?? "";
            var nombre = reader["NOMBRE_CORTO"]?.ToString()?.Trim() ?? "";
            if (!string.IsNullOrEmpty(codigo))
                result[codigo] = nombre;
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error al obtener nombres de personal");
    }

    return result;
}

public async Task<Dictionary<string, string>> ObtenerDescripcionesArticulosAsync(IEnumerable<string> codigos)
{
    var lista = codigos.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    if (lista.Count == 0) return result;

    var paramNames = lista.Select((_, i) => $":c{i}").ToList();
    var sql = $"SELECT COD_ART, DESCRIPCION FROM SIG.ARTICUL WHERE COD_ART IN ({string.Join(",", paramNames)})";

    try
    {
        await using var con = new OracleConnection(GetOracleConnectionString());
        await con.OpenAsync();
        await using var cmd = new OracleCommand(sql, con) { BindByName = true };
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

public async Task<Dictionary<string, string>> ObtenerDescripcionesTablaAuxiliarAsync(string tipo, IEnumerable<string> codigos)
{
    var lista = codigos.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    if (lista.Count == 0) return result;

    var paramNames = lista.Select((_, i) => $":c{i}").ToList();
    var sql = $"SELECT CODIGO, DESCRIPCION FROM SIG.TABLAS_AUXILIARES WHERE TIPO = :tipo AND CODIGO IN ({string.Join(",", paramNames)})";

    try
    {
        await using var con = new OracleConnection(GetOracleConnectionString());
        await con.OpenAsync();
        await using var cmd = new OracleCommand(sql, con) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter(":tipo", OracleDbType.Varchar2) { Value = tipo });
        for (int i = 0; i < lista.Count; i++)
            cmd.Parameters.Add(new OracleParameter($":c{i}", OracleDbType.Varchar2) { Value = lista[i] });

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var codigo = reader["CODIGO"]?.ToString()?.Trim() ?? "";
            var desc   = reader["DESCRIPCION"]?.ToString()?.Trim() ?? "";
            if (!string.IsNullOrEmpty(codigo))
                result[codigo] = desc;
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error al obtener descripciones de TABLAS_AUXILIARES tipo={Tipo}", tipo);
    }

    return result;
}

public async Task<Dictionary<string, string>> ObtenerDescripcionesCentroCostosAsync(IEnumerable<string> codigos)
{
    var lista = codigos.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    if (lista.Count == 0) return result;

    var paramNames = lista.Select((_, i) => $":c{i}").ToList();
    var sql = $"SELECT CENTRO_COSTO, NOMBRE FROM SIG.CENTRO_DE_COSTOS WHERE CENTRO_COSTO IN ({string.Join(",", paramNames)})";

    try
    {
        await using var con = new OracleConnection(GetOracleConnectionString());
        await con.OpenAsync();
        await using var cmd = new OracleCommand(sql, con) { BindByName = true };
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

public async Task ActualizarIdGrupoItemsAsync(
    string tipDoc, int serie, long numReq,
    IEnumerable<int> ordenes, long idGrupo)
{
    var lista = ordenes.Distinct().ToList();
    if (lista.Count == 0) return;

    var paramNames = lista.Select((_, i) => $":ord{i}").ToList();
    var sql = $"UPDATE ITEMREQ SET ID_GRUPO = :idGrupo" +
              $" WHERE TIPDOC = :tipDoc AND SERIE = :serie AND NUMREQ = :numReq" +
              $" AND ORDEN IN ({string.Join(",", paramNames)})";

    try
    {
        await using var con = new OracleConnection(GetOracleConnectionString());
        await con.OpenAsync();
        await using var cmd = new OracleCommand(sql, con) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter(":idGrupo", OracleDbType.Int64)   { Value = idGrupo });
        cmd.Parameters.Add(new OracleParameter(":tipDoc",  OracleDbType.Varchar2) { Value = tipDoc  });
        cmd.Parameters.Add(new OracleParameter(":serie",   OracleDbType.Int32)    { Value = serie   });
        cmd.Parameters.Add(new OracleParameter(":numReq",  OracleDbType.Int64)    { Value = numReq  });
        for (int i = 0; i < lista.Count; i++)
            cmd.Parameters.Add(new OracleParameter($":ord{i}", OracleDbType.Int32) { Value = lista[i] });

        await cmd.ExecuteNonQueryAsync();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error al actualizar ID_GRUPO de ítems");
        throw;
    }
}

public async Task<long> ObtenerSiguienteIdGrupoAsync()
{
    const string sql = "SELECT SIG.LG_GRUPO_SEQ.NEXTVAL FROM DUAL";
    try
    {
        await using var con = new OracleConnection(GetOracleConnectionString());
        await con.OpenAsync();
        await using var cmd = new OracleCommand(sql, con);
        var valor = await cmd.ExecuteScalarAsync();
        return Convert.ToInt64(valor);
    }
    catch (OracleException ex) when (ex.Number == 2289)
    {
        _logger.LogError(ex, "La secuencia SIG.LG_GRUPO_SEQ no existe en Oracle. Ejecutar: FabricaHilos/Data/Logistica/CREATE_LG_GRUPO_SEQ.sql");
        throw new InvalidOperationException(
            "La secuencia SIG.LG_GRUPO_SEQ no existe en la base de datos. " +
            "Solicite al DBA ejecutar el script: Data/Logistica/CREATE_LG_GRUPO_SEQ.sql", ex);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error al obtener siguiente valor de LG_GRUPO_SEQ");
        throw;
    }
}

public async Task AprobarGrupoAsync(long idGrupo)
{
    const string sql = "UPDATE ITEMREQ SET F_APROBADO = SYSDATE WHERE ID_GRUPO = :idGrupo";
    try
    {
        await using var con = new OracleConnection(GetOracleConnectionString());
        await con.OpenAsync();
        await using var cmd = new OracleCommand(sql, con) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter(":idGrupo", OracleDbType.Int64) { Value = idGrupo });
        await cmd.ExecuteNonQueryAsync();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error al aprobar grupo {IdGrupo}", idGrupo);
        throw;
    }
}

public async Task LimpiarIdGrupoAsync(long idGrupo)
{
    const string sql = "UPDATE ITEMREQ SET ID_GRUPO = NULL, F_APROBADO = NULL WHERE ID_GRUPO = :idGrupo";
    try
    {
        await using var con = new OracleConnection(GetOracleConnectionString());
        await con.OpenAsync();
        await using var cmd = new OracleCommand(sql, con) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter(":idGrupo", OracleDbType.Int64) { Value = idGrupo });
        await cmd.ExecuteNonQueryAsync();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error al limpiar ID_GRUPO {IdGrupo}", idGrupo);
        throw;
    }
}

// ── PROGRESO GENERAL (4 etapas) ────────────────────────────────────────────

public async Task<Dictionary<string, ProgresoGeneralDto>> ObtenerProgresoGeneralAsync(
    IEnumerable<(string TipDoc, int Serie, long NumReq)> claves)
{
    var result = new Dictionary<string, ProgresoGeneralDto>(StringComparer.OrdinalIgnoreCase);
    var lista  = claves.Distinct().ToList();
    if (lista.Count == 0) return result;

    // Inicializar entradas
    foreach (var k in lista)
        result[$"{k.TipDoc}|{k.Serie}|{k.NumReq}"] = new ProgresoGeneralDto();

    // Construir cláusula IN de claves compuestas
    var paramPairs = lista.Select((k, i) =>
        $"(TIPDOC = :td{i} AND SERIE = :sr{i} AND NUMREQ = :nr{i})").ToList();
    string whereIn = string.Join(" OR ", paramPairs);

    // ── Etapa 1: ítems con ID_GRUPO y F_APROBADO ──────────────────────────────
    string sqlEtapa1 = $@"
        SELECT TIPDOC, SERIE, NUMREQ,
               COUNT(*) AS ITEMS_APROBADOS
        FROM   ITEMREQ
        WHERE  ({whereIn})
          AND  ID_GRUPO   IS NOT NULL
          AND  F_APROBADO IS NOT NULL
        GROUP BY TIPDOC, SERIE, NUMREQ";

    // ── Etapa 2: orden de compra (DESP_ITEMREQ con NRO_DOC_REF) ──────────────
    // Un NUMREQ tiene varios ítems con el mismo NRO_DOC_REF → MAX para obtener 1 valor por NUMREQ
    var paramNumreqs = lista.Select((k, i) => $":nr2_{i}").ToList();
    string whereNumreqs = string.Join(",", paramNumreqs);
    string sqlEtapa2 = $@"
        SELECT D.NUMREQ, MAX(D.NRO_DOC_REF) AS NRO_DOC_REF
        FROM   DESP_ITEMREQ D
        WHERE  D.NUMREQ IN ({whereNumreqs})
          AND  D.NRO_DOC_REF IS NOT NULL
        GROUP BY D.NUMREQ";

    // ── Etapa 4: pendiente de pago ────────────────────────────────────────────
    // TODO: Implementar cuando se disponga del campo/tabla de contabilidad/pago

    try
    {
        await using var con = new OracleConnection(GetOracleConnectionString());
        await con.OpenAsync();

        // — Etapa 1 —
        await using (var cmd = new OracleCommand(sqlEtapa1, con) { BindByName = true })
        {
            for (int i = 0; i < lista.Count; i++)
            {
                cmd.Parameters.Add(new OracleParameter($":td{i}", OracleDbType.Varchar2) { Value = lista[i].TipDoc });
                cmd.Parameters.Add(new OracleParameter($":sr{i}", OracleDbType.Int32)    { Value = lista[i].Serie  });
                cmd.Parameters.Add(new OracleParameter($":nr{i}", OracleDbType.Int64)    { Value = lista[i].NumReq });
            }
            await using var r = (OracleDataReader)await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var key   = $"{GetStr(r,"TIPDOC")}|{GetInt(r,"SERIE")}|{GetLong(r,"NUMREQ")}";
                var items = GetInt(r, "ITEMS_APROBADOS");
                if (result.TryGetValue(key, out var pg))
                {
                    pg.Etapa1Items    = items;
                    pg.Etapa1Aprobada = items > 0;
                }
            }
        }

        // — Etapa 2: orden de compra —
        // 1 NRO_DOC_REF por NUMREQ (GROUP BY NUMREQ con MAX)
        var nroDocRefANumReqs = new Dictionary<string, List<long>>(StringComparer.OrdinalIgnoreCase);

        await using (var cmd = new OracleCommand(sqlEtapa2, con) { BindByName = true })
        {
            for (int i = 0; i < lista.Count; i++)
                cmd.Parameters.Add(new OracleParameter($":nr2_{i}", OracleDbType.Int64) { Value = lista[i].NumReq });

            await using var r = (OracleDataReader)await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                long   numReq    = GetLong(r, "NUMREQ");
                string nroDocRef = GetStr(r,  "NRO_DOC_REF") ?? "";
                if (string.IsNullOrWhiteSpace(nroDocRef)) continue;

                _logger.LogDebug("Etapa2 → NUMREQ={NumReq} NRO_DOC_REF={NroDocRef}", numReq, nroDocRef);

                if (!nroDocRefANumReqs.TryGetValue(nroDocRef, out var numReqs))
                {
                    numReqs = new List<long>();
                    nroDocRefANumReqs[nroDocRef] = numReqs;
                }
                if (!numReqs.Contains(numReq)) numReqs.Add(numReq);

                var match = result.FirstOrDefault(kv =>
                    kv.Key.EndsWith($"|{numReq}", StringComparison.OrdinalIgnoreCase));
                if (match.Value != null)
                {
                    match.Value.Etapa2Items    = 1;
                    match.Value.Etapa2Aprobada = true;
                }
            }
        }

        _logger.LogDebug("Etapa2 completada. NRO_DOC_REF recolectados: [{Refs}]",
            string.Join(", ", nroDocRefANumReqs.Keys));

        // — Etapa 3: facturado —
        // REGISTRO_DIARIO WHERE NUM_REF = NRO_DOC_REF AND TIPO = 'RS'
        if (nroDocRefANumReqs.Count > 0)
        {
            var todosNroDocRef = nroDocRefANumReqs.Keys.ToList();
            var paramE3 = todosNroDocRef.Select((_, i) => $":e3_{i}").ToList();
            string sqlEtapa3 = $@"
                SELECT RD.NUM_REF, COUNT(*) AS ITEMS_FACTURADOS,
                       MAX(RD.TIPDOC)   AS TIPDOC,
                       MAX(RD.SERIE)    AS SERIE,
                       MAX(RD.NUMERO)   AS NUMERO,
                       MAX(RD.RELACION) AS RELACION
                FROM   REGISTRO_DIARIO RD
                WHERE  RD.NUM_REF IN ({string.Join(",", paramE3)})
                  AND  RD.TIPO = 'RS'
                GROUP BY RD.NUM_REF";

            _logger.LogDebug("Etapa3 SQL: {Sql} | Params: [{Refs}]",
                sqlEtapa3, string.Join(", ", todosNroDocRef));

            await using var cmd3 = new OracleCommand(sqlEtapa3, con) { BindByName = true };
            for (int i = 0; i < todosNroDocRef.Count; i++)
                cmd3.Parameters.Add(new OracleParameter($":e3_{i}", OracleDbType.Varchar2) { Value = todosNroDocRef[i] });

            await using var r3 = (OracleDataReader)await cmd3.ExecuteReaderAsync();
            bool hayResultados = false;
            while (await r3.ReadAsync())
            {
                hayResultados = true;
                string numRef = GetStr(r3, "NUM_REF") ?? "";
                _logger.LogDebug("Etapa3 → NUM_REF={NumRef} TIPDOC={TipDoc} NUMERO={Numero}", numRef, GetStr(r3, "TIPDOC"), GetStr(r3, "NUMERO"));

                if (!nroDocRefANumReqs.TryGetValue(numRef, out var numReqsE3)) continue;

                // Aplica a TODOS los NUMREQs que comparten este NRO_DOC_REF
                foreach (long numReq in numReqsE3)
                {
                    var match = result.FirstOrDefault(kv =>
                        kv.Key.EndsWith($"|{numReq}", StringComparison.OrdinalIgnoreCase));
                    if (match.Value != null)
                    {
                        match.Value.Etapa3Items++;
                        match.Value.Etapa3Aprobada = true;
                        match.Value.Etapa3TipDoc   = GetStr(r3, "TIPDOC");
                        match.Value.Etapa3Serie    = GetStr(r3, "SERIE");
                        match.Value.Etapa3Numero   = GetStr(r3, "NUMERO");
                        match.Value.Etapa3Relacion = GetStr(r3, "RELACION");
                    }
                }
            }
            if (!hayResultados)
                _logger.LogDebug("Etapa3 → REGISTRO_DIARIO no devolvió filas para los NUM_REF buscados.");
        }

        // — Etapa 4: pago en FACTPAG —
        // Recolectar los NUMREQs con Etapa3 aprobada y sus datos de comprobante
        var etapa4Keys = result.Values
            .Where(pg => pg.Etapa3Aprobada
                      && pg.Etapa3TipDoc   != null
                      && pg.Etapa3Serie    != null
                      && pg.Etapa3Numero   != null
                      && pg.Etapa3Relacion  != null)
            .Select(pg => pg)
            .ToList();

        if (etapa4Keys.Count > 0)
        {
            var wherePairs = etapa4Keys.Select((_, i) =>
                $"(TIPDOC = :fp_td{i} AND SERIE_NUM = :fp_sr{i} AND NUMERO = :fp_nm{i} AND COD_PROVEEDOR = :fp_rv{i})"
            ).ToList();

            string sqlEtapa4 = $@"
                SELECT TIPDOC, SERIE_NUM, NUMERO, COD_PROVEEDOR, SALDO
                FROM   FACTPAG
                WHERE  {string.Join(" OR ", wherePairs)}";

            await using var cmd4 = new OracleCommand(sqlEtapa4, con) { BindByName = true };
            for (int i = 0; i < etapa4Keys.Count; i++)
            {
                cmd4.Parameters.Add(new OracleParameter($":fp_td{i}", OracleDbType.Varchar2) { Value = etapa4Keys[i].Etapa3TipDoc });
                cmd4.Parameters.Add(new OracleParameter($":fp_sr{i}", OracleDbType.Varchar2) { Value = etapa4Keys[i].Etapa3Serie });
                cmd4.Parameters.Add(new OracleParameter($":fp_nm{i}", OracleDbType.Varchar2) { Value = etapa4Keys[i].Etapa3Numero });
                cmd4.Parameters.Add(new OracleParameter($":fp_rv{i}", OracleDbType.Varchar2) { Value = etapa4Keys[i].Etapa3Relacion });
            }

            await using var r4 = (OracleDataReader)await cmd4.ExecuteReaderAsync();
            // Indexar resultados de FACTPAG por clave compuesta
            var factpagResults = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            while (await r4.ReadAsync())
            {
                string fpKey = $"{GetStr(r4,"TIPDOC")}|{GetStr(r4,"SERIE_NUM")}|{GetStr(r4,"NUMERO")}|{GetStr(r4,"COD_PROVEEDOR")}";
                decimal saldo = r4.IsDBNull(r4.GetOrdinal("SALDO")) ? 0m : r4.GetDecimal(r4.GetOrdinal("SALDO"));
                factpagResults[fpKey] = saldo;
            }

            // Aplicar Etapa4 a cada requisición con Etapa3 aprobada
            foreach (var pg in etapa4Keys)
            {
                string fpKey = $"{pg.Etapa3TipDoc}|{pg.Etapa3Serie}|{pg.Etapa3Numero}|{pg.Etapa3Relacion}";
                if (factpagResults.TryGetValue(fpKey, out decimal saldo))
                {
                    // Etapa4 concluida solo si SALDO = 0 (sin pendiente de pago)
                    pg.Etapa4Aprobada = saldo == 0m;
                    pg.Etapa4Items    = 1;
                    pg.Etapa4Saldo    = saldo;
                }
            }
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error al obtener progreso general de requerimientos");
    }

    return result;
}
}