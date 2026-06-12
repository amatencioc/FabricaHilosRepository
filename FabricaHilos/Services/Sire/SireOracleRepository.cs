using FabricaHilos.Models.Sire;
using Oracle.ManagedDataAccess.Client;

namespace FabricaHilos.Services.Sire;

/// <summary>
/// Implementación ADO.NET del repositorio Oracle para SIRE.
/// Usa la conexión LaColonialConnection (SIG/STARK) — SIRE es siempre La Colonial por RUC.
/// Tablas: SIG.SIRE_JOB | SIG.SIRE_HEALTH | SIG.SIRE_LOG
/// </summary>
public sealed class SireOracleRepository : ISireOracleRepository
{
    private readonly string _connStr;
    private readonly ILogger<SireOracleRepository> _logger;

    public SireOracleRepository(IConfiguration configuration, ILogger<SireOracleRepository> logger)
    {
        _connStr = configuration.GetConnectionString("LaColonialConnection")
            ?? throw new InvalidOperationException("LaColonialConnection no encontrado en configuración.");
        _logger = logger;
    }

    // ── Jobs ──────────────────────────────────────────────────────────────────

    public async Task<int> InsertJobAsync(SireExportacionJob job, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO SIG.SIRE_JOB
                (ID, JOB_ID, TIPO_REGISTRO, PERIODO, USUARIO_ID, ESTADO,
                 FECHA_CREACION, FECHA_ACT)
            VALUES
                (SEQ_SIRE_JOB.NEXTVAL, :jobId, :tipo, :periodo, :usuario, :estado,
                 SYSDATE, SYSDATE)
            RETURNING ID INTO :newId";

        await using var conn = new OracleConnection(_connStr);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.Add(":jobId",   OracleDbType.Varchar2).Value = job.JobId;
        cmd.Parameters.Add(":tipo",    OracleDbType.Varchar2).Value = job.TipoRegistro;
        cmd.Parameters.Add(":periodo", OracleDbType.Varchar2).Value = job.Periodo;
        cmd.Parameters.Add(":usuario", OracleDbType.Varchar2).Value = (object?)job.UsuarioId ?? DBNull.Value;
        cmd.Parameters.Add(":estado",  OracleDbType.Varchar2).Value = job.Estado;
        var outId = new OracleParameter(":newId", OracleDbType.Int32)
            { Direction = System.Data.ParameterDirection.Output };
        cmd.Parameters.Add(outId);

        await cmd.ExecuteNonQueryAsync(ct);
        var id = Convert.ToInt32(outId.Value.ToString());
        job.Id = id;
        return id;
    }

    public async Task UpdateJobAsync(SireExportacionJob job, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE SIG.SIRE_JOB SET
                ESTADO          = :estado,
                NUM_TICKET      = :ticket,
                NOMBRE_ARCHIVO  = :nomArch,
                RUTA_ARCHIVO    = :rutaArch,
                COD_TIPO_ARCHIVO= :codTipo,
                COD_PROCESO     = :codProc,
                REG_INSERTADOS  = :regIns,
                REG_DUPLICADOS  = :regDup,
                MENSAJE_ERROR   = :msgErr,
                FECHA_ACT       = SYSDATE,
                FECHA_FIN       = :fechaFin
            WHERE ID = :id";

        await using var conn = new OracleConnection(_connStr);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.Add(":estado",   OracleDbType.Varchar2).Value = job.Estado;
        cmd.Parameters.Add(":ticket",   OracleDbType.Varchar2).Value = (object?)job.NumTicket        ?? DBNull.Value;
        cmd.Parameters.Add(":nomArch",  OracleDbType.Varchar2).Value = (object?)job.NombreArchivo    ?? DBNull.Value;
        cmd.Parameters.Add(":rutaArch", OracleDbType.Varchar2).Value = (object?)job.RutaArchivo      ?? DBNull.Value;
        cmd.Parameters.Add(":codTipo",  OracleDbType.Varchar2).Value = (object?)job.CodTipoArchivo   ?? DBNull.Value;
        cmd.Parameters.Add(":codProc",  OracleDbType.Varchar2).Value = (object?)job.CodProceso       ?? DBNull.Value;
        cmd.Parameters.Add(":regIns",   OracleDbType.Int32   ).Value = (object?)job.RegistrosInsertados  ?? DBNull.Value;
        cmd.Parameters.Add(":regDup",   OracleDbType.Int32   ).Value = (object?)job.RegistrosDuplicados  ?? DBNull.Value;
        cmd.Parameters.Add(":msgErr",   OracleDbType.Varchar2).Value = Trunc(job.MensajeError, 2000);
        cmd.Parameters.Add(":fechaFin", OracleDbType.Date    ).Value = (object?)job.FechaFinalizacion ?? DBNull.Value;
        cmd.Parameters.Add(":id",       OracleDbType.Int32   ).Value = job.Id;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<SireExportacionJob?> GetJobByIdAsync(int id, CancellationToken ct = default)
    {
        const string sql = "SELECT * FROM SIG.SIRE_JOB WHERE ID = :id";
        await using var conn = new OracleConnection(_connStr);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.Add(":id", OracleDbType.Int32).Value = id;
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapJob(reader) : null;
    }

    public async Task<SireExportacionJob?> GetJobByJobIdAsync(string jobId, CancellationToken ct = default)
    {
        const string sql = "SELECT * FROM SIG.SIRE_JOB WHERE JOB_ID = :jobId";
        await using var conn = new OracleConnection(_connStr);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.Add(":jobId", OracleDbType.Varchar2).Value = jobId;
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapJob(reader) : null;
    }

    public async Task<SireExportacionJob?> GetJobActivoAsync(string tipoRegistro, string periodo, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT * FROM (
                SELECT * FROM SIG.SIRE_JOB
                WHERE TIPO_REGISTRO = :tipo AND PERIODO = :periodo
                  AND ESTADO IN ('Pendiente','EnProceso')
                ORDER BY FECHA_CREACION DESC
            ) WHERE ROWNUM <= 1";

        await using var conn = new OracleConnection(_connStr);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.Add(":tipo",    OracleDbType.Varchar2).Value = tipoRegistro;
        cmd.Parameters.Add(":periodo", OracleDbType.Varchar2).Value = periodo;
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapJob(reader) : null;
    }

    public async Task<List<SireExportacionJob>> GetJobsInterrumpidosAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT * FROM SIG.SIRE_JOB
            WHERE ESTADO IN ('Pendiente','EnProceso')
            ORDER BY FECHA_CREACION ASC";

        await using var conn = new OracleConnection(_connStr);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var result = new List<SireExportacionJob>();
        while (await reader.ReadAsync(ct))
            result.Add(MapJob(reader));
        return result;
    }

    // ── Health logs ───────────────────────────────────────────────────────────

    public async Task InsertHealthLogAsync(SireHealthCheckLog log, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO SIG.SIRE_HEALTH
                (ID, FECHA, ESTADO, TOKEN_OK, RVIE_OK, RVIE_PERIODOS, RCE_OK, RCE_PERIODOS, MENSAJE_ERROR)
            VALUES
                (SEQ_SIRE_HEALTH.NEXTVAL, SYSDATE, :estado, :tokenOk,
                 :rvieOk, :rviePer, :rceOk, :rcePer, :msg)";

        await using var conn = new OracleConnection(_connStr);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.Add(":estado",  OracleDbType.Varchar2).Value = log.Status;
        cmd.Parameters.Add(":tokenOk", OracleDbType.Int32   ).Value = log.AuthOk ? 1 : 0;
        cmd.Parameters.Add(":rvieOk",  OracleDbType.Int32   ).Value = log.RvieOk ? 1 : 0;
        cmd.Parameters.Add(":rviePer", OracleDbType.Int32   ).Value = (object?)log.RviePeriodos ?? DBNull.Value;
        cmd.Parameters.Add(":rceOk",   OracleDbType.Int32   ).Value = log.RceOk  ? 1 : 0;
        cmd.Parameters.Add(":rcePer",  OracleDbType.Int32   ).Value = (object?)log.RcePeriodos  ?? DBNull.Value;
        cmd.Parameters.Add(":msg",     OracleDbType.Varchar2).Value = Trunc(log.Descripcion, 2000);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<SireHealthCheckLog>> GetHealthLogsAsync(int top = 50, CancellationToken ct = default)
    {
        var sql = $@"
            SELECT * FROM (
                SELECT * FROM SIG.SIRE_HEALTH
                ORDER BY FECHA DESC
            ) WHERE ROWNUM <= {top}";

        await using var conn = new OracleConnection(_connStr);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var result = new List<SireHealthCheckLog>();
        while (await reader.ReadAsync(ct))
            result.Add(MapHealth(reader));
        return result;
    }

    // ── API logs ──────────────────────────────────────────────────────────────

    public async Task InsertApiLogAsync(SireApiLog log, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO SIG.SIRE_LOG
                (ID, FECHA, JOB_ID, OPERACION, METODO_HTTP, URL, HTTP_STATUS, DURACION_MS, EXITO, MENSAJE)
            VALUES
                (SEQ_SIRE_LOG.NEXTVAL, SYSDATE, :jobId, :op, :metodo,
                 :url, :status, :dur, :exito, :msg)";

        try
        {
            await using var conn = new OracleConnection(_connStr);
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.Add(":jobId",  OracleDbType.Varchar2).Value = (object?)log.JobId      ?? DBNull.Value;
            cmd.Parameters.Add(":op",     OracleDbType.Varchar2).Value = log.Operacion;
            cmd.Parameters.Add(":metodo", OracleDbType.Varchar2).Value = (object?)log.MetodoHttp ?? DBNull.Value;
            cmd.Parameters.Add(":url",    OracleDbType.Varchar2).Value = Trunc(log.Url, 1000);
            cmd.Parameters.Add(":status", OracleDbType.Int32   ).Value = (object?)log.HttpStatus  ?? DBNull.Value;
            cmd.Parameters.Add(":dur",    OracleDbType.Int64   ).Value = (object?)log.DuracionMs  ?? DBNull.Value;
            cmd.Parameters.Add(":exito",  OracleDbType.Int32   ).Value = log.Exito ? 1 : 0;
            cmd.Parameters.Add(":msg",    OracleDbType.Varchar2).Value = Trunc(log.Mensaje, 2000);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex)
        {
            // El log no debe interrumpir el flujo principal
            _logger.LogWarning(ex, "[SIRE-LOG] No se pudo insertar log de auditoría en Oracle.");
        }
    }

    // ── Mappers ───────────────────────────────────────────────────────────────

    public async Task<List<SireApiLog>> GetApiLogsAsync(
        int top = 200,
        string? jobId = null,
        string? operacion = null,
        CancellationToken ct = default)
    {
        var where = new List<string>();
        if (!string.IsNullOrWhiteSpace(jobId))     where.Add("JOB_ID = :jobId");
        if (!string.IsNullOrWhiteSpace(operacion)) where.Add("OPERACION = :op");

        var whereClause = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : string.Empty;
        var sql = $@"
            SELECT * FROM (
                SELECT ID, FECHA, JOB_ID, OPERACION, METODO_HTTP, URL,
                       HTTP_STATUS, DURACION_MS, EXITO, MENSAJE
                FROM SIG.SIRE_LOG
                {whereClause}
                ORDER BY FECHA DESC
            ) WHERE ROWNUM <= :top";

        var list = new List<SireApiLog>();
        await using var conn = new OracleConnection(_connStr);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.Add(":top", OracleDbType.Int32).Value = top;
        if (!string.IsNullOrWhiteSpace(jobId))     cmd.Parameters.Add(":jobId", OracleDbType.Varchar2).Value = jobId;
        if (!string.IsNullOrWhiteSpace(operacion)) cmd.Parameters.Add(":op",    OracleDbType.Varchar2).Value = operacion;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(MapApiLog(reader));

        return list;
    }

    private static SireApiLog MapApiLog(System.Data.Common.DbDataReader r) => new()
    {
        Id         = r.GetInt64(r.GetOrdinal("ID")),
        Fecha      = r.GetDateTime(r.GetOrdinal("FECHA")),
        JobId      = NullStr(r, "JOB_ID"),
        Operacion  = r.GetString(r.GetOrdinal("OPERACION")),
        MetodoHttp = NullStr(r, "METODO_HTTP"),
        Url        = NullStr(r, "URL"),
        HttpStatus = NullInt(r, "HTTP_STATUS"),
        DuracionMs = r.IsDBNull(r.GetOrdinal("DURACION_MS")) ? null : r.GetInt64(r.GetOrdinal("DURACION_MS")),
        Exito      = r.GetInt32(r.GetOrdinal("EXITO")) == 1,
        Mensaje    = NullStr(r, "MENSAJE"),
    };

    private static SireExportacionJob MapJob(System.Data.Common.DbDataReader r) => new()
    {
        Id                  = r.GetInt32(r.GetOrdinal("ID")),
        JobId               = r.GetString(r.GetOrdinal("JOB_ID")),
        TipoRegistro        = r.GetString(r.GetOrdinal("TIPO_REGISTRO")),
        Periodo             = r.GetString(r.GetOrdinal("PERIODO")),
        UsuarioId           = NullStr(r, "USUARIO_ID"),
        Estado              = r.GetString(r.GetOrdinal("ESTADO")),
        NumTicket           = NullStr(r, "NUM_TICKET"),
        NombreArchivo       = NullStr(r, "NOMBRE_ARCHIVO"),
        RutaArchivo         = NullStr(r, "RUTA_ARCHIVO"),
        CodTipoArchivo      = NullStr(r, "COD_TIPO_ARCHIVO"),
        CodProceso          = NullStr(r, "COD_PROCESO"),
        RegistrosInsertados = NullInt(r, "REG_INSERTADOS"),
        RegistrosDuplicados = NullInt(r, "REG_DUPLICADOS"),
        MensajeError        = NullStr(r, "MENSAJE_ERROR"),
        FechaCreacion       = r.GetDateTime(r.GetOrdinal("FECHA_CREACION")),
        FechaActualizacion  = r.GetDateTime(r.GetOrdinal("FECHA_ACT")),
        FechaFinalizacion   = NullDate(r, "FECHA_FIN"),
    };

    private static SireHealthCheckLog MapHealth(System.Data.Common.DbDataReader r) => new()
    {
        Id           = r.GetInt32(r.GetOrdinal("ID")),
        FechaUtc     = r.GetDateTime(r.GetOrdinal("FECHA")),
        Status       = r.GetString(r.GetOrdinal("ESTADO")),
        AuthOk       = r.GetInt32(r.GetOrdinal("TOKEN_OK")) == 1,
        RvieOk       = r.GetInt32(r.GetOrdinal("RVIE_OK")) == 1,
        RviePeriodos = NullInt(r, "RVIE_PERIODOS"),
        RceOk        = r.GetInt32(r.GetOrdinal("RCE_OK")) == 1,
        RcePeriodos  = NullInt(r, "RCE_PERIODOS"),
        Descripcion  = NullStr(r, "MENSAJE_ERROR"),
    };

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string?   NullStr(System.Data.Common.DbDataReader r, string col)
        => r.IsDBNull(r.GetOrdinal(col)) ? null : r.GetString(r.GetOrdinal(col));

    private static int?      NullInt(System.Data.Common.DbDataReader r, string col)
        => r.IsDBNull(r.GetOrdinal(col)) ? null : r.GetInt32(r.GetOrdinal(col));

    private static DateTime? NullDate(System.Data.Common.DbDataReader r, string col)
        => r.IsDBNull(r.GetOrdinal(col)) ? null : r.GetDateTime(r.GetOrdinal(col));

    private static object Trunc(string? s, int max)
        => s is null ? DBNull.Value : (object)s[..Math.Min(s.Length, max)];
}
