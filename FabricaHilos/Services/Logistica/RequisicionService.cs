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

    Task ActualizarIdGrupoItemsAsync(string tipDoc, int serie, long numReq, IEnumerable<int> ordenes, long idGrupo);

    Task<long> ObtenerSiguienteIdGrupoAsync();
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

        // Excluir estados cerrado/anulado solo cuando no hay búsqueda activa
        string baseEstadoFilter = hasBuscar ? string.Empty : " AND ESTADO NOT IN ('6','9')";

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
                   PAGED.A_ADUSER, PAGED.A_ADFECHA, PAGED.A_MDUSER, PAGED.A_MDFECHA
            FROM (
                SELECT ROW_NUMBER() OVER (ORDER BY FECHA DESC, NUMREQ DESC) AS RN,
                       COUNT(*) OVER() AS TOTAL_COUNT,
                       TIPDOC, SERIE, NUMREQ, CENTRO_COSTO, PROVEEDORES,
                       FECHA, F_ENTREGA, RESPONSABLE, PRIORIDAD, OBSERVACION,
                       ESTADO, DESTINO, IND_SERV, IMPSTO, AFECTO_IGV, AFECTO_IRENTA,
                       TIP_REF, SER_REF, NRO_REF,
                       F_AUTORIZA, AUTORIZA, USER_AUTORIZA, IP_AUTORIZA,
                       F_RECIBE, RECIBE,
                       FCH_ENTREGA_LOGIST, NOTA_ANULACION,
                       A_ADUSER, A_ADFECHA, A_MDUSER, A_MDFECHA
                FROM REQUISICION
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
                items.Add(MapRequisicion(r));
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
}