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

    Task DesaprobarGrupoAsync(long idGrupo);

    Task LimpiarIdGrupoAsync(long idGrupo);

    /// <summary>
    /// Devuelve el progreso general por las 4 etapas del flujo logístico
    /// para un conjunto de requerimientos (tipDoc+serie+numReq).
    /// Clave del diccionario: "TIPDOC|SERIE|NUMREQ"
    /// </summary>
    Task<Dictionary<string, ProgresoGeneralDto>> ObtenerProgresoGeneralAsync(
        IEnumerable<(string TipDoc, int Serie, long NumReq)> claves);

    Task<Dictionary<string, ProgresoGeneralDto>> ObtenerProgresoGeneralAsync_Backup(
        IEnumerable<(string TipDoc, int Serie, long NumReq)> claves);

    Task CambiarEstadoAsync(
        IEnumerable<(string TipDoc, int Serie, long NumReq)> claves, string nuevoEstado);

    /// <summary>
    /// Activa requerimientos cancelados (ESTADO=9):
    /// Si tiene AUTORIZA → ESTADO='1' (Autorizada), si no → ESTADO='2' (Recibida).
    /// </summary>
    Task ActivarRequisicionesAsync(
        IEnumerable<(string TipDoc, int Serie, long NumReq)> claves);

    /// <summary>
    /// Retorna todos los requerimientos pendientes (ESTADO='1' o '2') con sus ítems,
    /// para exportar a Excel.
    /// </summary>
    Task<List<(RequisicionDto Cabecera, List<ItemReqDto> Items)>> ObtenerPendientesConItemsAsync();
}

public class RequisicionService : OracleServiceBase, IRequisicionService
{
    private readonly ILogger<RequisicionService> _logger;

    public RequisicionService(IConfiguration configuration,
        ILogger<RequisicionService> logger,
        IHttpContextAccessor httpContextAccessor)
        : base(configuration, httpContextAccessor)
    {
        _logger = logger;
    }

    private static string?   GetStr(OracleDataReader r, string col)      => r[col] == DBNull.Value ? null : r[col]?.ToString();
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

        bool esFiltroEspecial = hasEstado && (estado == "6" || estado == "9");

        // Para cerrado/anulado: respetar búsqueda y fechas igual que otros estados
        bool aplicarBuscar = hasBuscar;
        bool aplicarFechas = !hasBuscar;

        string fechaIniFilter = (aplicarFechas && hasFechaIni) ? " AND TRUNC(FECHA) >= TRUNC(:fechaIni)" : string.Empty;
        string fechaFinFilter = (aplicarFechas && hasFechaFin) ? " AND TRUNC(FECHA) <= TRUNC(:fechaFin)" : string.Empty;
        string estadoFilter   = hasEstado ? " AND ESTADO = :estado" : string.Empty;

        // Por defecto (sin filtro de estado) excluir cerrado(6) y anulado(9)
        // Cuando se elige explícitamente 6 o 9 no se aplica este filtro base
        string baseEstadoFilter = esFiltroEspecial ? string.Empty : " AND ESTADO NOT IN ('6','9')";

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
                              (SELECT COUNT(*)   FROM {S}ITEMREQ I WHERE I.TIPDOC=R.TIPDOC AND I.SERIE=R.SERIE AND I.NUMREQ=R.NUMREQ) AS TOTAL_ITEMS,
                              (SELECT COUNT(*)   FROM {S}ITEMREQ I WHERE I.TIPDOC=R.TIPDOC AND I.SERIE=R.SERIE AND I.NUMREQ=R.NUMREQ AND I.ID_GRUPO IS NOT NULL) AS ITEMS_CON_GRUPO
                       FROM {S}REQUISICION R
                {whereClause}
            ) PAGED
            WHERE PAGED.RN BETWEEN :startRow AND :endRow";

        try
        {
            using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();
            using var cmd = new OracleCommand(sql, conn);
            cmd.BindByName = true;

            if (aplicarBuscar)
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
        var sql = $@"
            SELECT TIPDOC, SERIE, NUMREQ, CENTRO_COSTO, PROVEEDORES,
                   FECHA, F_ENTREGA, RESPONSABLE, PRIORIDAD, OBSERVACION,
                   ESTADO, DESTINO, IND_SERV, IMPSTO, AFECTO_IGV, AFECTO_IRENTA,
                   TIP_REF, SER_REF, NRO_REF,
                   F_AUTORIZA, AUTORIZA, USER_AUTORIZA, IP_AUTORIZA,
                   F_RECIBE, RECIBE,
                   FCH_ENTREGA_LOGIST, NOTA_ANULACION,
                   A_ADUSER, A_ADFECHA, A_MDUSER, A_MDFECHA
            FROM {S}REQUISICION
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
        var sql = $@"
            SELECT I.TIPDOC, I.SERIE, I.NUMREQ, I.ORDEN, I.COD_ART, I.DETALLE, I.UNIDAD, I.MARCA, I.CTACTBLE,
                   I.CANTIDAD, I.SALDO, I.STK_MIN, I.STK_HIST,
                   I.MONEDA, I.PRECIO,
                   I.TP_DESTINO, I.DESTINO, I.COD_SOLICITA,
                   I.ID_GRUPO, I.F_APROBADO, I.OBSERVACIONES,
                   I.A_ADUSER, I.A_ADFECHA, I.A_MDUSER, I.A_MDFECHA,
                   MAX(D.NRO_DOC_REF) AS NRO_DOC_REF
            FROM {S}ITEMREQ I
            LEFT JOIN {S}DESP_ITEMREQ D ON D.NUMREQ = I.NUMREQ AND D.ORDEN = I.ORDEN
            WHERE I.TIPDOC = :tipDoc AND I.SERIE = :serie AND I.NUMREQ = :numReq
            GROUP BY I.TIPDOC, I.SERIE, I.NUMREQ, I.ORDEN, I.COD_ART, I.DETALLE, I.UNIDAD, I.MARCA, I.CTACTBLE,
                   I.CANTIDAD, I.SALDO, I.STK_MIN, I.STK_HIST,
                   I.MONEDA, I.PRECIO,
                   I.TP_DESTINO, I.DESTINO, I.COD_SOLICITA,
                   I.ID_GRUPO, I.F_APROBADO, I.OBSERVACIONES,
                   I.A_ADUSER, I.A_ADFECHA, I.A_MDUSER, I.A_MDFECHA
            ORDER BY I.ORDEN";

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
        NroDocRef     = GetStr(r, "NRO_DOC_REF"),
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
    var sql = $"SELECT C_CODIGO, NOMBRE_CORTO FROM {S}V_PERSONAL WHERE SITUACION = '1' AND C_CODIGO IN ({string.Join(",", paramNames)})";

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
    var sql = $"SELECT COD_ART, DESCRIPCION FROM {S}ARTICUL WHERE COD_ART IN ({string.Join(",", paramNames)})";

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
    var sql = $"SELECT CODIGO, DESCRIPCION FROM {S}TABLAS_AUXILIARES WHERE TIPO = :tipo AND CODIGO IN ({string.Join(",", paramNames)})";

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
    var sql = $"SELECT CENTRO_COSTO, NOMBRE FROM {S}CENTRO_DE_COSTOS WHERE CENTRO_COSTO IN ({string.Join(",", paramNames)})";

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
    var sql = $"UPDATE {S}ITEMREQ SET ID_GRUPO = :idGrupo" +
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
    var sqlSeq = $"SELECT {S}LG_GRUPO_SEQ.NEXTVAL FROM DUAL";
    try
    {
        await using var con = new OracleConnection(GetOracleConnectionString());
        await con.OpenAsync();
        await using var cmd = new OracleCommand(sqlSeq, con);
        var valor = await cmd.ExecuteScalarAsync();
        return Convert.ToInt64(valor);
    }
    catch (OracleException ex) when (ex.Number == 2289)
    {
        _logger.LogError(ex, "La secuencia LG_GRUPO_SEQ no existe en el esquema {Schema}. Ejecutar: FabricaHilos/Data/Logistica/CREATE_LG_GRUPO_SEQ.sql", S);
        throw new InvalidOperationException(
            $"La secuencia {S}LG_GRUPO_SEQ no existe en la base de datos. " +
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
    var sql = $"UPDATE {S}ITEMREQ SET F_APROBADO = SYSDATE WHERE ID_GRUPO = :idGrupo";
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

public async Task DesaprobarGrupoAsync(long idGrupo)
{
    var sql = $"UPDATE {S}ITEMREQ SET F_APROBADO = NULL WHERE ID_GRUPO = :idGrupo";
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
        _logger.LogError(ex, "Error al desaprobar grupo {IdGrupo}", idGrupo);
        throw;
    }
}

public async Task LimpiarIdGrupoAsync(long idGrupo)
{
    var sql = $"UPDATE {S}ITEMREQ SET ID_GRUPO = NULL, F_APROBADO = NULL WHERE ID_GRUPO = :idGrupo";
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

    foreach (var k in lista)
        result[$"{k.TipDoc}|{k.Serie}|{k.NumReq}"] = new ProgresoGeneralDto();

    var paramPairs = lista.Select((k, i) =>
        $"(TIPDOC = :td{i} AND SERIE = :sr{i} AND NUMREQ = :nr{i})").ToList();
    string whereIn = string.Join(" OR ", paramPairs);

    // ── Etapa 1: grupos distintos por NUMREQ y cuántos tienen F_APROBADO ────────
    string sqlEtapa1 = $@"
        SELECT TIPDOC, SERIE, NUMREQ,
               COUNT(*)                                               AS GRUPOS_TOTAL,
               SUM(CASE WHEN APROBADO = 1 THEN 1 ELSE 0 END)         AS GRUPOS_APROBADOS
        FROM (
            SELECT TIPDOC, SERIE, NUMREQ, ID_GRUPO,
                   MAX(CASE WHEN F_APROBADO IS NOT NULL THEN 1 ELSE 0 END) AS APROBADO
            FROM   {S}ITEMREQ
            WHERE  ({whereIn})
              AND  ID_GRUPO IS NOT NULL
            GROUP BY TIPDOC, SERIE, NUMREQ, ID_GRUPO
        )
        GROUP BY TIPDOC, SERIE, NUMREQ";

    // ── Etapa 2: ítems aprobados con/sin O/C por NUMREQ ────────────────────────
    // Base = ítems con F_APROBADO (aprobados en Etapa1); de esos cuántos tienen O/C.

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
                var key = $"{GetStr(r,"TIPDOC")}|{GetInt(r,"SERIE")}|{GetLong(r,"NUMREQ")}";
                if (result.TryGetValue(key, out var pg))
                {
                    pg.Etapa1GruposTotal     = GetInt(r, "GRUPOS_TOTAL");
                    pg.Etapa1GruposAprobados = GetInt(r, "GRUPOS_APROBADOS");
                }
            }
        }

        // — Etapa 2: ítems con O/C (total) vs ítems con O/C y aprobados (parcial) ──
        // Total     = ítems que tienen NRO_DOC_REF en DESP_ITEMREQ (tienen O/C asignada)
        // Aprobados = de esos, cuántos tienen además F_APROBADO IS NOT NULL
        if (lista.Count > 0)
        {
            var paramPairs2 = lista.Select((k, i) =>
                $"(I.TIPDOC = :e2td{i} AND I.SERIE = :e2sr{i} AND I.NUMREQ = :e2nr{i})").ToList();
            string sqlEtapa2 = $@"
                SELECT I.TIPDOC, I.SERIE, I.NUMREQ,
                       COUNT(*)                                                            AS ITEMS_CON_OC,
                       SUM(CASE WHEN I.F_APROBADO IS NOT NULL THEN 1 ELSE 0 END)          AS ITEMS_APROBADOS
                FROM   {S}ITEMREQ I
                JOIN   {S}DESP_ITEMREQ D ON D.NUMREQ = I.NUMREQ AND D.ORDEN = I.ORDEN
                                     AND D.NRO_DOC_REF IS NOT NULL
                WHERE  ({string.Join(" OR ", paramPairs2)})
                GROUP BY I.TIPDOC, I.SERIE, I.NUMREQ";

            await using var cmdE2 = new OracleCommand(sqlEtapa2, con) { BindByName = true };
            for (int i = 0; i < lista.Count; i++)
            {
                cmdE2.Parameters.Add(new OracleParameter($":e2td{i}", OracleDbType.Varchar2) { Value = lista[i].TipDoc });
                cmdE2.Parameters.Add(new OracleParameter($":e2sr{i}", OracleDbType.Int32)    { Value = lista[i].Serie  });
                cmdE2.Parameters.Add(new OracleParameter($":e2nr{i}", OracleDbType.Int64)    { Value = lista[i].NumReq });
            }
            await using var rE2 = (OracleDataReader)await cmdE2.ExecuteReaderAsync();
            while (await rE2.ReadAsync())
            {
                var key = $"{GetStr(rE2,"TIPDOC")}|{GetInt(rE2,"SERIE")}|{GetLong(rE2,"NUMREQ")}";
                if (!result.TryGetValue(key, out var pg)) continue;
                pg.Etapa2ItemsTotal = GetInt(rE2, "ITEMS_CON_OC");
                pg.Etapa2ItemsConOC = GetInt(rE2, "ITEMS_APROBADOS");
            }
        }

        // — Recolectar O/C distintas (columna O/C + base para Etapa3) ──────────
        // Se toman de DESP_ITEMREQ sin filtro de aprobación: la O/C pertenece al
        // requerimiento completo, no a un ítem individual aprobado.
        // WM_CONCAT compatible con Oracle 10g; se itera fila a fila (sin concatenar).
        if (lista.Count > 0)
        {
            var paramPairsOC = lista.Select((k, i) =>
                $"(TIPDOC = :octd{i} AND SERIE = :ocsr{i} AND NUMREQ = :ocnr{i})").ToList();
            string sqlOC = $@"
                SELECT DISTINCT TIPDOC, SERIE, NUMREQ, NRO_DOC_REF
                FROM   {S}DESP_ITEMREQ
                WHERE  NRO_DOC_REF IS NOT NULL
                  AND  ({string.Join(" OR ", paramPairsOC)})
                ORDER BY TIPDOC, SERIE, NUMREQ, NRO_DOC_REF";

            await using var cmdOC = new OracleCommand(sqlOC, con) { BindByName = true };
            for (int i = 0; i < lista.Count; i++)
            {
                cmdOC.Parameters.Add(new OracleParameter($":octd{i}", OracleDbType.Varchar2) { Value = lista[i].TipDoc });
                cmdOC.Parameters.Add(new OracleParameter($":ocsr{i}", OracleDbType.Int32)    { Value = lista[i].Serie  });
                cmdOC.Parameters.Add(new OracleParameter($":ocnr{i}", OracleDbType.Int64)    { Value = lista[i].NumReq });
            }
            await using var rOC = (OracleDataReader)await cmdOC.ExecuteReaderAsync();
            while (await rOC.ReadAsync())
            {
                var key = $"{GetStr(rOC,"TIPDOC")}|{GetInt(rOC,"SERIE")}|{GetLong(rOC,"NUMREQ")}";
                if (!result.TryGetValue(key, out var pg)) continue;
                string oc = GetStr(rOC, "NRO_DOC_REF") ?? "";
                if (!string.IsNullOrWhiteSpace(oc) && !pg.OrdenesCompra.Contains(oc))
                    pg.OrdenesCompra.Add(oc);
            }
        }

        // — Etapa 3: facturado ─────────────────────────────────────────────────
        // Por cada NUMREQ: cuántas O/C distintas hay (DESP_ITEMREQ.NRO_DOC_REF)
        // y de esas cuántas tienen registro en REGISTRO_DIARIO TIPO='RS' (NUM_REF=NRO_DOC_REF).
        // Mismo patrón que Etapa1 y Etapa2: una sola SQL con GROUP BY TIPDOC,SERIE,NUMREQ.
        if (lista.Count > 0)
        {
            var paramPairs3 = lista.Select((k, i) =>
                $"(D.TIPDOC = :e3td{i} AND D.SERIE = :e3sr{i} AND D.NUMREQ = :e3nr{i})").ToList();
            string sqlEtapa3 = $@"
                SELECT D.TIPDOC, D.SERIE, D.NUMREQ,
                       COUNT(DISTINCT D.NRO_DOC_REF)                                  AS OC_TOTAL,
                       COUNT(DISTINCT CASE WHEN RD.NUM_REF IS NOT NULL
                                           THEN D.NRO_DOC_REF END)                   AS OC_FACTURADAS
                FROM   {S}DESP_ITEMREQ D
                LEFT JOIN {S}REGISTRO_DIARIO RD
                       ON RD.NUM_REF = D.NRO_DOC_REF
                      AND RD.TIPO = 'RS'
                WHERE  D.NRO_DOC_REF IS NOT NULL
                  AND  ({string.Join(" OR ", paramPairs3)})
                GROUP BY D.TIPDOC, D.SERIE, D.NUMREQ";

            await using var cmd3 = new OracleCommand(sqlEtapa3, con) { BindByName = true };
            for (int i = 0; i < lista.Count; i++)
            {
                cmd3.Parameters.Add(new OracleParameter($":e3td{i}", OracleDbType.Varchar2) { Value = lista[i].TipDoc });
                cmd3.Parameters.Add(new OracleParameter($":e3sr{i}", OracleDbType.Int32)    { Value = lista[i].Serie  });
                cmd3.Parameters.Add(new OracleParameter($":e3nr{i}", OracleDbType.Int64)    { Value = lista[i].NumReq });
            }
            await using var r3 = (OracleDataReader)await cmd3.ExecuteReaderAsync();
            while (await r3.ReadAsync())
            {
                var key = $"{GetStr(r3,"TIPDOC")}|{GetInt(r3,"SERIE")}|{GetLong(r3,"NUMREQ")}";
                if (result.TryGetValue(key, out var pg))
                {
                    pg.Etapa3OcTotal      = GetInt(r3, "OC_TOTAL");
                    pg.Etapa3OcFacturadas = GetInt(r3, "OC_FACTURADAS");
                }
            }
        }

        // — Etapa 4: pago ──────────────────────────────────────────────────────
        // Por cada NUMREQ: cuántas facturas distintas hay en REGISTRO_DIARIO (via O/C)
        // y de esas cuántas tienen SALDO=0 en FACTPAG.
        // Mismo patrón: una sola SQL con GROUP BY TIPDOC,SERIE,NUMREQ.
        if (lista.Count > 0)
        {
            var paramPairs4 = lista.Select((k, i) =>
                $"(D.TIPDOC = :e4td{i} AND D.SERIE = :e4sr{i} AND D.NUMREQ = :e4nr{i})").ToList();
            string sqlEtapa4 = $@"
                SELECT D.TIPDOC, D.SERIE, D.NUMREQ,
                       COUNT(DISTINCT RD.NUMERO)                                       AS FACT_TOTAL,
                       COUNT(DISTINCT CASE WHEN FP.SALDO = 0 THEN RD.NUMERO END)       AS FACT_PAGADAS
                FROM   {S}DESP_ITEMREQ D
                JOIN   {S}REGISTRO_DIARIO RD
                       ON RD.NUM_REF = D.NRO_DOC_REF
                      AND RD.TIPO = 'RS'
                LEFT JOIN {S}FACTPAG FP
                       ON FP.TIPDOC      = RD.TIPDOC
                      AND FP.SERIE_NUM   = RD.SERIE
                      AND FP.NUMERO      = RD.NUMERO
                      AND FP.COD_PROVEEDOR = RD.RELACION
                WHERE  D.NRO_DOC_REF IS NOT NULL
                  AND  ({string.Join(" OR ", paramPairs4)})
                GROUP BY D.TIPDOC, D.SERIE, D.NUMREQ";

            await using var cmd4 = new OracleCommand(sqlEtapa4, con) { BindByName = true };
            for (int i = 0; i < lista.Count; i++)
            {
                cmd4.Parameters.Add(new OracleParameter($":e4td{i}", OracleDbType.Varchar2) { Value = lista[i].TipDoc });
                cmd4.Parameters.Add(new OracleParameter($":e4sr{i}", OracleDbType.Int32)    { Value = lista[i].Serie  });
                cmd4.Parameters.Add(new OracleParameter($":e4nr{i}", OracleDbType.Int64)    { Value = lista[i].NumReq });
            }
            await using var r4 = (OracleDataReader)await cmd4.ExecuteReaderAsync();
            while (await r4.ReadAsync())
            {
                var key = $"{GetStr(r4,"TIPDOC")}|{GetInt(r4,"SERIE")}|{GetLong(r4,"NUMREQ")}";
                if (result.TryGetValue(key, out var pg))
                {
                    pg.Etapa4FacturasTotal   = GetInt(r4, "FACT_TOTAL");
                    pg.Etapa4FacturasPagadas = GetInt(r4, "FACT_PAGADAS");
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

public async Task CambiarEstadoAsync(
    IEnumerable<(string TipDoc, int Serie, long NumReq)> claves, string nuevoEstado)
{
    var lista = claves.Distinct().ToList();
    if (lista.Count == 0) return;

    var paramPairs = lista.Select((k, i) =>
        $"(TIPDOC = :td{i} AND SERIE = :sr{i} AND NUMREQ = :nr{i})").ToList();
    var setClause = nuevoEstado == "9"
        ? "ESTADO = :estado, FCH_ENTREGA_LOGIST = SYSDATE, NOTA_ANULACION = 'Anulacion Masiva'"
        : "ESTADO = :estado";
    var sql = $"UPDATE {S}REQUISICION SET {setClause} WHERE " + string.Join(" OR ", paramPairs);

    try
    {
        await using var con = new OracleConnection(GetOracleConnectionString());
        await con.OpenAsync();
        await using var cmd = new OracleCommand(sql, con) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter(":estado", OracleDbType.Varchar2) { Value = nuevoEstado });
        for (int i = 0; i < lista.Count; i++)
        {
            cmd.Parameters.Add(new OracleParameter($":td{i}", OracleDbType.Varchar2) { Value = lista[i].TipDoc });
            cmd.Parameters.Add(new OracleParameter($":sr{i}", OracleDbType.Int32)    { Value = lista[i].Serie  });
            cmd.Parameters.Add(new OracleParameter($":nr{i}", OracleDbType.Int64)    { Value = lista[i].NumReq });
        }
        await cmd.ExecuteNonQueryAsync();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error al cambiar estado de requerimientos a {Estado}", nuevoEstado);
        throw;
    }
}

public async Task ActivarRequisicionesAsync(
    IEnumerable<(string TipDoc, int Serie, long NumReq)> claves)
{
    var lista = claves.Distinct().ToList();
    if (lista.Count == 0) return;

    var paramPairs = lista.Select((k, i) =>
        $"(TIPDOC = :td{i} AND SERIE = :sr{i} AND NUMREQ = :nr{i})").ToList();

    // Si tiene AUTORIZA → Autorizada (1), si no → Recibida (2)
    var sql = $"UPDATE {S}REQUISICION" +
              " SET ESTADO = CASE" +
              " WHEN AUTORIZA IS NOT NULL AND TRIM(AUTORIZA) != '' THEN '1'" +
              " ELSE '2'" +
              " END," +
              " FCH_ENTREGA_LOGIST = SYSDATE," +
              " NOTA_ANULACION = NOTA_ANULACION || ' - Activacion Masiva'" +
              $" WHERE ({string.Join(" OR ", paramPairs)})";

    try
    {
        await using var con = new OracleConnection(GetOracleConnectionString());
        await con.OpenAsync();
        await using var cmd = new OracleCommand(sql, con) { BindByName = true };
        for (int i = 0; i < lista.Count; i++)
        {
            cmd.Parameters.Add(new OracleParameter($":td{i}", OracleDbType.Varchar2) { Value = lista[i].TipDoc });
            cmd.Parameters.Add(new OracleParameter($":sr{i}", OracleDbType.Int32)    { Value = lista[i].Serie  });
            cmd.Parameters.Add(new OracleParameter($":nr{i}", OracleDbType.Int64)    { Value = lista[i].NumReq });
        }
        await cmd.ExecuteNonQueryAsync();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error al activar requerimientos");
        throw;
    }
}

public async Task<Dictionary<string, ProgresoGeneralDto>> ObtenerProgresoGeneralAsync_Backup(
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

    // ── Etapa 1: grupos distintos por NUMREQ y cuántos tienen F_APROBADO ────────
    // Se agrupa primero por NUMREQ+ID_GRUPO para determinar si ese grupo está
    // aprobado (al menos un ítem con F_APROBADO), luego se cuenta por NUMREQ.
    string sqlEtapa1 = $@"
        SELECT TIPDOC, SERIE, NUMREQ,
               COUNT(*)                              AS GRUPOS_TOTAL,
               SUM(CASE WHEN APROBADO = 1 THEN 1 ELSE 0 END) AS GRUPOS_APROBADOS
        FROM (
            SELECT TIPDOC, SERIE, NUMREQ, ID_GRUPO,
                   MAX(CASE WHEN F_APROBADO IS NOT NULL THEN 1 ELSE 0 END) AS APROBADO
            FROM   {S}ITEMREQ
            WHERE  ({whereIn})
              AND  ID_GRUPO IS NOT NULL
            GROUP BY TIPDOC, SERIE, NUMREQ, ID_GRUPO
        )
        GROUP BY TIPDOC, SERIE, NUMREQ";

    // ── Etapa 2: ítems aprobados con/sin O/C por NUMREQ ──────────────────────
    // Base = ítems con F_APROBADO IS NOT NULL; de esos cuántos tienen O/C.

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
                var key = $"{GetStr(r,"TIPDOC")}|{GetInt(r,"SERIE")}|{GetLong(r,"NUMREQ")}";
                if (result.TryGetValue(key, out var pg))
                {
                    pg.Etapa1GruposTotal     = GetInt(r, "GRUPOS_TOTAL");
                    pg.Etapa1GruposAprobados = GetInt(r, "GRUPOS_APROBADOS");
                }
            }
        }

        // — Etapa 2: ítems con O/C (total) vs ítems con O/C y aprobados (parcial) ──
        // Total     = ítems que tienen NRO_DOC_REF en DESP_ITEMREQ (tienen O/C asignada)
        // Aprobados = de esos, cuántos tienen además F_APROBADO IS NOT NULL
        if (lista.Count > 0)
        {
            var paramPairs2b = lista.Select((k, i) =>
                $"(I.TIPDOC = :e2td{i} AND I.SERIE = :e2sr{i} AND I.NUMREQ = :e2nr{i})").ToList();
            string sqlEtapa2b = $@"
                SELECT I.TIPDOC, I.SERIE, I.NUMREQ,
                       COUNT(*)                                                            AS ITEMS_CON_OC,
                       SUM(CASE WHEN I.F_APROBADO IS NOT NULL THEN 1 ELSE 0 END)          AS ITEMS_APROBADOS
                FROM   {S}ITEMREQ I
                JOIN   {S}DESP_ITEMREQ D ON D.NUMREQ = I.NUMREQ AND D.ORDEN = I.ORDEN
                                     AND D.NRO_DOC_REF IS NOT NULL
                WHERE  ({string.Join(" OR ", paramPairs2b)})
                GROUP BY I.TIPDOC, I.SERIE, I.NUMREQ";

            await using var cmdE2b = new OracleCommand(sqlEtapa2b, con) { BindByName = true };
            for (int i = 0; i < lista.Count; i++)
            {
                cmdE2b.Parameters.Add(new OracleParameter($":e2td{i}", OracleDbType.Varchar2) { Value = lista[i].TipDoc });
                cmdE2b.Parameters.Add(new OracleParameter($":e2sr{i}", OracleDbType.Int32)    { Value = lista[i].Serie  });
                cmdE2b.Parameters.Add(new OracleParameter($":e2nr{i}", OracleDbType.Int64)    { Value = lista[i].NumReq });
            }
            await using var rE2b = (OracleDataReader)await cmdE2b.ExecuteReaderAsync();
            while (await rE2b.ReadAsync())
            {
                var key = $"{GetStr(rE2b,"TIPDOC")}|{GetInt(rE2b,"SERIE")}|{GetLong(rE2b,"NUMREQ")}";
                if (!result.TryGetValue(key, out var pg)) continue;
                pg.Etapa2ItemsTotal = GetInt(rE2b, "ITEMS_CON_OC");
                pg.Etapa2ItemsConOC = GetInt(rE2b, "ITEMS_APROBADOS");
            }
        }

        // — Recolectar O/C distintas (columna O/C + base para Etapa3) ──────────
        // Iteración fila a fila compatible con Oracle 10g (sin LISTAGG).
        if (lista.Count > 0)
        {
            var paramPairsOCb = lista.Select((k, i) =>
                $"(TIPDOC = :octd{i} AND SERIE = :ocsr{i} AND NUMREQ = :ocnr{i})").ToList();
            string sqlOCb = $@"
                SELECT DISTINCT TIPDOC, SERIE, NUMREQ, NRO_DOC_REF
                FROM   {S}DESP_ITEMREQ
                WHERE  NRO_DOC_REF IS NOT NULL
                  AND  ({string.Join(" OR ", paramPairsOCb)})
                ORDER BY TIPDOC, SERIE, NUMREQ, NRO_DOC_REF";

            await using var cmdOCb = new OracleCommand(sqlOCb, con) { BindByName = true };
            for (int i = 0; i < lista.Count; i++)
            {
                cmdOCb.Parameters.Add(new OracleParameter($":octd{i}", OracleDbType.Varchar2) { Value = lista[i].TipDoc });
                cmdOCb.Parameters.Add(new OracleParameter($":ocsr{i}", OracleDbType.Int32)    { Value = lista[i].Serie  });
                cmdOCb.Parameters.Add(new OracleParameter($":ocnr{i}", OracleDbType.Int64)    { Value = lista[i].NumReq });
            }
            await using var rOCb = (OracleDataReader)await cmdOCb.ExecuteReaderAsync();
            while (await rOCb.ReadAsync())
            {
                var key = $"{GetStr(rOCb,"TIPDOC")}|{GetInt(rOCb,"SERIE")}|{GetLong(rOCb,"NUMREQ")}";
                if (!result.TryGetValue(key, out var pg)) continue;
                string oc = GetStr(rOCb, "NRO_DOC_REF") ?? "";
                if (!string.IsNullOrWhiteSpace(oc) && !pg.OrdenesCompra.Contains(oc))
                    pg.OrdenesCompra.Add(oc);
            }
        }

        // — Etapa 3: facturado —
        // Misma lógica que el método principal: una SQL con GROUP BY TIPDOC,SERIE,NUMREQ
        // que cuenta O/C distintas y cuántas tienen registro en REGISTRO_DIARIO TIPO='RS'.
        if (lista.Count > 0)
        {
            var paramPairs3b = lista.Select((k, i) =>
                $"(D.TIPDOC = :e3td{i} AND D.SERIE = :e3sr{i} AND D.NUMREQ = :e3nr{i})").ToList();
            string sqlEtapa3b = $@"
                SELECT D.TIPDOC, D.SERIE, D.NUMREQ,
                       COUNT(DISTINCT D.NRO_DOC_REF)                                  AS OC_TOTAL,
                       COUNT(DISTINCT CASE WHEN RD.NUM_REF IS NOT NULL
                                           THEN D.NRO_DOC_REF END)                   AS OC_FACTURADAS
                FROM   {S}DESP_ITEMREQ D
                LEFT JOIN {S}REGISTRO_DIARIO RD
                       ON RD.NUM_REF = D.NRO_DOC_REF
                      AND RD.TIPO = 'RS'
                WHERE  D.NRO_DOC_REF IS NOT NULL
                  AND  ({string.Join(" OR ", paramPairs3b)})
                GROUP BY D.TIPDOC, D.SERIE, D.NUMREQ";

            await using var cmd3 = new OracleCommand(sqlEtapa3b, con) { BindByName = true };
            for (int i = 0; i < lista.Count; i++)
            {
                cmd3.Parameters.Add(new OracleParameter($":e3td{i}", OracleDbType.Varchar2) { Value = lista[i].TipDoc });
                cmd3.Parameters.Add(new OracleParameter($":e3sr{i}", OracleDbType.Int32)    { Value = lista[i].Serie  });
                cmd3.Parameters.Add(new OracleParameter($":e3nr{i}", OracleDbType.Int64)    { Value = lista[i].NumReq });
            }
            await using var r3 = (OracleDataReader)await cmd3.ExecuteReaderAsync();
            while (await r3.ReadAsync())
            {
                var key = $"{GetStr(r3,"TIPDOC")}|{GetInt(r3,"SERIE")}|{GetLong(r3,"NUMREQ")}";
                if (result.TryGetValue(key, out var pg))
                {
                    pg.Etapa3OcTotal      = GetInt(r3, "OC_TOTAL");
                    pg.Etapa3OcFacturadas = GetInt(r3, "OC_FACTURADAS");
                }
            }
        }

        // — Etapa 4: pago —
        // Misma lógica que el método principal: una SQL con GROUP BY TIPDOC,SERIE,NUMREQ
        // que cuenta facturas distintas en REGISTRO_DIARIO y cuántas tienen SALDO=0 en FACTPAG.
        if (lista.Count > 0)
        {
            var paramPairs4b = lista.Select((k, i) =>
                $"(D.TIPDOC = :e4td{i} AND D.SERIE = :e4sr{i} AND D.NUMREQ = :e4nr{i})").ToList();
            string sqlEtapa4b = $@"
                SELECT D.TIPDOC, D.SERIE, D.NUMREQ,
                       COUNT(DISTINCT RD.NUMERO)                                       AS FACT_TOTAL,
                       COUNT(DISTINCT CASE WHEN FP.SALDO = 0 THEN RD.NUMERO END)       AS FACT_PAGADAS
                FROM   {S}DESP_ITEMREQ D
                JOIN   {S}REGISTRO_DIARIO RD
                       ON RD.NUM_REF = D.NRO_DOC_REF
                      AND RD.TIPO = 'RS'
                LEFT JOIN {S}FACTPAG FP
                       ON FP.TIPDOC      = RD.TIPDOC
                      AND FP.SERIE_NUM   = RD.SERIE
                      AND FP.NUMERO      = RD.NUMERO
                      AND FP.COD_PROVEEDOR = RD.RELACION
                WHERE  D.NRO_DOC_REF IS NOT NULL
                  AND  ({string.Join(" OR ", paramPairs4b)})
                GROUP BY D.TIPDOC, D.SERIE, D.NUMREQ";

            await using var cmd4 = new OracleCommand(sqlEtapa4b, con) { BindByName = true };
            for (int i = 0; i < lista.Count; i++)
            {
                cmd4.Parameters.Add(new OracleParameter($":e4td{i}", OracleDbType.Varchar2) { Value = lista[i].TipDoc });
                cmd4.Parameters.Add(new OracleParameter($":e4sr{i}", OracleDbType.Int32)    { Value = lista[i].Serie  });
                cmd4.Parameters.Add(new OracleParameter($":e4nr{i}", OracleDbType.Int64)    { Value = lista[i].NumReq });
            }
            await using var r4 = (OracleDataReader)await cmd4.ExecuteReaderAsync();
            while (await r4.ReadAsync())
            {
                var key = $"{GetStr(r4,"TIPDOC")}|{GetInt(r4,"SERIE")}|{GetLong(r4,"NUMREQ")}";
                if (result.TryGetValue(key, out var pg))
                {
                    pg.Etapa4FacturasTotal   = GetInt(r4, "FACT_TOTAL");
                    pg.Etapa4FacturasPagadas = GetInt(r4, "FACT_PAGADAS");
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

public async Task<List<(RequisicionDto Cabecera, List<ItemReqDto> Items)>> ObtenerPendientesConItemsAsync()
{
    var resultado = new List<(RequisicionDto, List<ItemReqDto>)>();

    var sqlCab = $@"
        SELECT TIPDOC, SERIE, NUMREQ, CENTRO_COSTO, PROVEEDORES,
               FECHA, F_ENTREGA, RESPONSABLE, PRIORIDAD, OBSERVACION,
               ESTADO, DESTINO, IND_SERV, IMPSTO, AFECTO_IGV, AFECTO_IRENTA,
               TIP_REF, SER_REF, NRO_REF,
               F_AUTORIZA, AUTORIZA, USER_AUTORIZA, IP_AUTORIZA,
               F_RECIBE, RECIBE,
               FCH_ENTREGA_LOGIST, NOTA_ANULACION,
               A_ADUSER, A_ADFECHA, A_MDUSER, A_MDFECHA
        FROM {S}REQUISICION
        WHERE ESTADO IN ('1','2')
        ORDER BY FECHA DESC, NUMREQ DESC";

    try
    {
        await using var con = new OracleConnection(GetOracleConnectionString());
        await con.OpenAsync();

        var cabeceras = new List<RequisicionDto>();
        await using (var cmd = new OracleCommand(sqlCab, con))
        await using (var r = (OracleDataReader)await cmd.ExecuteReaderAsync())
            while (await r.ReadAsync())
                cabeceras.Add(MapRequisicion(r));

        if (cabeceras.Count == 0) return resultado;

        var numReqs = string.Join(",", cabeceras.Select(c => c.NumReq).Distinct());

        var sqlItems = $@"
            SELECT I.TIPDOC, I.SERIE, I.NUMREQ, I.ORDEN, I.COD_ART, I.DETALLE, I.UNIDAD, I.MARCA, I.CTACTBLE,
                   I.CANTIDAD, I.SALDO, I.STK_MIN, I.STK_HIST,
                   I.MONEDA, I.PRECIO,
                   I.TP_DESTINO, I.DESTINO, I.COD_SOLICITA,
                   I.ID_GRUPO, I.F_APROBADO, I.OBSERVACIONES,
                   I.A_ADUSER, I.A_ADFECHA, I.A_MDUSER, I.A_MDFECHA,
                   MAX(D.NRO_DOC_REF) AS NRO_DOC_REF
            FROM {S}ITEMREQ I
            LEFT JOIN {S}DESP_ITEMREQ D ON D.NUMREQ = I.NUMREQ AND D.ORDEN = I.ORDEN
            WHERE I.NUMREQ IN ({numReqs})
              AND I.ID_GRUPO IS NULL
              AND I.F_APROBADO IS NULL
            GROUP BY I.TIPDOC, I.SERIE, I.NUMREQ, I.ORDEN, I.COD_ART, I.DETALLE, I.UNIDAD, I.MARCA, I.CTACTBLE,
                     I.CANTIDAD, I.SALDO, I.STK_MIN, I.STK_HIST,
                     I.MONEDA, I.PRECIO,
                     I.TP_DESTINO, I.DESTINO, I.COD_SOLICITA,
                     I.ID_GRUPO, I.F_APROBADO, I.OBSERVACIONES,
                     I.A_ADUSER, I.A_ADFECHA, I.A_MDUSER, I.A_MDFECHA
            ORDER BY I.NUMREQ, I.ORDEN";

        var itemsMap = new Dictionary<long, List<ItemReqDto>>();
        await using (var cmd2 = new OracleCommand(sqlItems, con))
        await using (var r2 = (OracleDataReader)await cmd2.ExecuteReaderAsync())
        {
            while (await r2.ReadAsync())
            {
                var item = MapItem(r2);
                if (!itemsMap.TryGetValue(item.NumReq, out var lista))
                {
                    lista = new List<ItemReqDto>();
                    itemsMap[item.NumReq] = lista;
                }
                lista.Add(item);
            }
        }

        foreach (var cab in cabeceras)
            resultado.Add((cab, itemsMap.TryGetValue(cab.NumReq, out var its) ? its : new List<ItemReqDto>()));
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error al obtener requerimientos pendientes con ítems");
    }

    return resultado;
}
}

