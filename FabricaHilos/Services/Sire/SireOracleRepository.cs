using FabricaHilos.Models.Sire;
using Oracle.ManagedDataAccess.Client;

namespace FabricaHilos.Services.Sire;

/// <summary>
/// Implementación ADO.NET del repositorio Oracle para SIRE.
/// Usa la conexión LaColonialConnection (SIG/STARK) — SIRE es siempre La Colonial por RUC.
/// Tablas: SIG.SIRE_JOB | SIG.SIRE_LOG | SIG.SIRE_PROPUESTA | SIG.SIRE_LEGACY | SIG.SIRE_CONCIL
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

    /// <summary>
    /// Abre la conexión y fija la zona horaria de la sesión Oracle a Lima (UTC-5).
    /// El servidor Oracle tiene OS en UTC → SYSDATE devuelve UTC.
    /// Con esta sesión, CURRENT_DATE devuelve hora Lima correcta en todas las tablas SIRE.
    /// Reintenta una vez si la conexión del pool estaba obsoleta (ORA-12570 y similares).
    /// </summary>
    private async Task<OracleConnection> OpenConnAsync(CancellationToken ct)
    {
        const int maxAttempts = 2;
        OracleException? lastEx = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var conn = new OracleConnection(_connStr);
            try
            {
                await conn.OpenAsync(ct);
                // AutoCommit=true: cada DML se commitea automáticamente al ejecutarse.
                // ODP.NET Managed tiene AutoCommit=false por defecto, lo que hace que
                // los UPDATE/INSERT queden en transacciones implícitas sin commit hasta
                // que la conexión se recicle, causando inconsistencia entre el log .NET
                // y el estado real en la BD (especialmente en el WatcherWorker).
                conn.AutoCommit = true;
                await using var tzCmd = conn.CreateCommand();
                tzCmd.CommandText = "ALTER SESSION SET TIME_ZONE = 'America/Lima'";
                await tzCmd.ExecuteNonQueryAsync(ct);
                return conn;
            }
            catch (OracleException ex) when (attempt < maxAttempts && IsStalePoolError(ex.Number))
            {
                // Conexión del pool obsoleta (servidor Oracle cortó la sesión por inactividad).
                // Se descarta el pool completo y se reintenta con una conexión fresca.
                _logger.LogWarning(
                    "[SIRE-ORACLE] Conexión Oracle obsoleta (ORA-{Nr}) en intento {A}/{M}. Descartando pool...",
                    ex.Number, attempt, maxAttempts);
                await conn.DisposeAsync();
                OracleConnection.ClearAllPools();
                lastEx = ex;
            }
            catch
            {
                await conn.DisposeAsync();
                throw;
            }
        }

        throw lastEx!;
    }

    /// <summary>Códigos de error Oracle que indican una conexión de pool expirada o caída.</summary>
    private static bool IsStalePoolError(int oraNumber) => oraNumber is
        12570 or // TNS:packet reader failure — paquete inesperado (inactividad larga)
        12571 or // TNS:packet write failure
        12537 or // TNS:connection closed
        12547 or // TNS:lost contact
        3135  or // connection lost contact
        28    or // session killed
        1012;    // not logged on

    // ── Jobs ──────────────────────────────────────────────────────────────────

    public async Task<int> InsertJobAsync(SireExportacionJob job, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO SIG.SIRE_JOB
                (ID, JOB_ID, TIPO_REGISTRO, PERIODO, USUARIO_ID, ESTADO,
                 FECHA_CREACION, FECHA_ACT)
            VALUES
                (SEQ_SIRE_JOB.NEXTVAL, :jobId, :tipo, :periodo, :usuario, :estado,
                 CURRENT_DATE, CURRENT_DATE)
            RETURNING ID INTO :newId";

        await using var conn = await OpenConnAsync(ct);
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
                FECHA_ACT       = CURRENT_DATE,
                FECHA_FIN       = :fechaFin,
                PROXIMA_CONSULTA= :proxCons
            WHERE ID = :id";

        await using var conn = await OpenConnAsync(ct);
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
        cmd.Parameters.Add(":proxCons", OracleDbType.Date    ).Value = (object?)job.ProximaConsulta   ?? DBNull.Value;
        cmd.Parameters.Add(":id",       OracleDbType.Int32   ).Value = job.Id;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<SireExportacionJob?> GetJobByIdAsync(int id, CancellationToken ct = default)
    {
        const string sql = "SELECT * FROM SIG.SIRE_JOB WHERE ID = :id";
        await using var conn = await OpenConnAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.Add(":id", OracleDbType.Int32).Value = id;
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapJob(reader) : null;
    }

    public async Task<SireExportacionJob?> GetJobByJobIdAsync(string jobId, CancellationToken ct = default)
    {
        const string sql = "SELECT * FROM SIG.SIRE_JOB WHERE JOB_ID = :jobId";
        await using var conn = await OpenConnAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.Add(":jobId", OracleDbType.Varchar2).Value = jobId;
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapJob(reader) : null;
    }

    public async Task<SireExportacionJob?> GetJobActivoAsync(string tipoRegistro, CancellationToken ct = default)
    {
        // Un job activo es cualquiera que aún no ha terminado (no terminal).
        // Incluye EsperandoTicket para evitar crear duplicados mientras el watcher vigila.
        const string sql = @"
            SELECT * FROM (
                SELECT * FROM SIG.SIRE_JOB
                WHERE TIPO_REGISTRO = :tipo
                  AND ESTADO IN ('Pendiente','EnProceso','EsperandoTicket')
                ORDER BY FECHA_CREACION DESC
            ) WHERE ROWNUM <= 1";

        await using var conn = await OpenConnAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.Add(":tipo", OracleDbType.Varchar2).Value = tipoRegistro;
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapJob(reader) : null;
    }

    public async Task<List<SireExportacionJob>> GetJobsInterrumpidosAsync(CancellationToken ct = default)
    {
        // Reencola el job más reciente por tipo que esté en estado no-terminal.
        // EsperandoTicket: el watcher lo recoge; Pendiente/EnProceso: el worker lo procesa.
        const string sql = @"
            SELECT * FROM (
                SELECT j.*,
                       ROW_NUMBER() OVER (PARTITION BY TIPO_REGISTRO ORDER BY FECHA_CREACION DESC) AS rn
                FROM SIG.SIRE_JOB j
                WHERE ESTADO IN ('Pendiente','EnProceso','EsperandoTicket')
            ) WHERE rn = 1
            ORDER BY FECHA_CREACION ASC";

        await using var conn = await OpenConnAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var result = new List<SireExportacionJob>();
        while (await reader.ReadAsync(ct))
            result.Add(MapJob(reader));
        return result;
    }

    public async Task<List<SireExportacionJob>> GetJobsEsperandoTicketAsync(CancellationToken ct = default)
    {
        // Obtiene jobs en EsperandoTicket cuya PROXIMA_CONSULTA ya venció (o es NULL).
        // Se añade 1 minuto de margen para cubrir desfases entre el reloj .NET y Oracle.
        // Ordenados por PROXIMA_CONSULTA para procesar primero los más viejos.
        const string sql = @"
            SELECT * FROM SIG.SIRE_JOB
            WHERE ESTADO = 'EsperandoTicket'
              AND (PROXIMA_CONSULTA IS NULL OR PROXIMA_CONSULTA <= CURRENT_DATE + 1/1440)
            ORDER BY PROXIMA_CONSULTA ASC, FECHA_CREACION ASC";

        await using var conn = await OpenConnAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var result = new List<SireExportacionJob>();
        while (await reader.ReadAsync(ct))
            result.Add(MapJob(reader));
        return result;
    }

    public async Task<List<SireExportacionJob>> GetJobsRecientesAsync(int top = 20, CancellationToken ct = default)
    {
        top = Math.Max(1, Math.Min(top, 200));
        var sql = $@"
            SELECT * FROM (
                SELECT * FROM SIG.SIRE_JOB
                ORDER BY FECHA_CREACION DESC
            ) WHERE ROWNUM <= {top}";

        await using var conn = await OpenConnAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var result = new List<SireExportacionJob>();
        while (await reader.ReadAsync(ct))
            result.Add(MapJob(reader));
        return result;
    }

    // ── API logs ──────────────────────────────────────────────────────────────

    public async Task InsertApiLogAsync(SireApiLog log, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO SIG.SIRE_LOG
                (ID, FECHA, JOB_ID, OPERACION, METODO_HTTP, URL, HTTP_STATUS, DURACION_MS, EXITO, MENSAJE)
            VALUES
                (SEQ_SIRE_LOG.NEXTVAL, :fecha, :jobId, :op, :metodo,
                 :url, :status, :dur, :exito, :msg)";

        try
        {
            await using var conn = await OpenConnAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.Add(":fecha",  OracleDbType.Date    ).Value = log.Fecha;  // hora Lima desde .NET
            cmd.Parameters.Add(":jobId",  OracleDbType.Varchar2).Value = (object?)log.JobId      ?? DBNull.Value;
            cmd.Parameters.Add(":op",     OracleDbType.Varchar2).Value = log.Operacion;
            cmd.Parameters.Add(":metodo", OracleDbType.Varchar2).Value = (object?)log.MetodoHttp ?? DBNull.Value;
            cmd.Parameters.Add(":url",    OracleDbType.Varchar2).Value = Trunc(log.Url, 1000);
            cmd.Parameters.Add(":status", OracleDbType.Int32   ).Value = (object?)log.HttpStatus  ?? DBNull.Value;
            cmd.Parameters.Add(":dur",    OracleDbType.Int64   ).Value = (object?)log.DuracionMs  ?? DBNull.Value;
            cmd.Parameters.Add(":exito",  OracleDbType.Int32   ).Value = log.Exito ? 1 : 0;
            cmd.Parameters.Add(":msg",    OracleDbType.Varchar2).Value = Trunc(log.Mensaje, 2000);
            await cmd.ExecuteNonQueryAsync(ct);
            _logger.LogDebug("[SIRE-LOG] Inserción exitosa: {Op} - {HttpStatus}", log.Operacion, log.HttpStatus);
        }
        catch (Exception ex)
        {
            // El log no debe interrumpir el flujo principal
            _logger.LogError(ex, "[SIRE-LOG] No se pudo insertar log de auditoría: {Op}", log.Operacion);
        }
    }

    // ── Mappers ───────────────────────────────────────────────────────────────

    public async Task<List<SireApiLog>> GetApiLogsAsync(
        int top = 200,
        string? jobId = null,
        string? operacion = null,
        CancellationToken ct = default,
        bool ordenAscendente = false)
    {
        top = Math.Max(1, Math.Min(top, 5000)); // Sanitize range

        // Build dynamic WHERE clause with proper filtering
        var whereConditions = new List<string>();
        if (!string.IsNullOrWhiteSpace(jobId))     whereConditions.Add("l.JOB_ID = :jobId");
        if (!string.IsNullOrWhiteSpace(operacion)) whereConditions.Add("l.OPERACION = :op");

        var whereClause = whereConditions.Count > 0 
            ? "WHERE " + string.Join(" AND ", whereConditions)
            : "";

        // ORDER BY ID garantiza orden de inserción correcto aunque FECHA tenga misma precisión de segundo.
        // DESC = más reciente primero (Monitoreo, actividad general).
        // ASC  = cronológico (modal de progreso del job).
        var orderDir = ordenAscendente ? "ASC" : "DESC";

        // Oracle 10g compatible: use simple inline view with ROWNUM
        var sql = $@"
            SELECT * FROM (
                SELECT l.ID, l.FECHA, l.JOB_ID, l.OPERACION, l.METODO_HTTP, l.URL,
                       l.HTTP_STATUS, l.DURACION_MS, l.EXITO, l.MENSAJE,
                       j.TIPO_REGISTRO
                FROM SIG.SIRE_LOG l
                LEFT JOIN SIG.SIRE_JOB j ON j.JOB_ID = l.JOB_ID
                {whereClause}
                ORDER BY l.ID {orderDir}
            )
            WHERE ROWNUM <= {top}";

        var list = new List<SireApiLog>();
        await using var conn = await OpenConnAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        if (!string.IsNullOrWhiteSpace(jobId))     cmd.Parameters.Add(":jobId", OracleDbType.Varchar2).Value = jobId;
        if (!string.IsNullOrWhiteSpace(operacion)) cmd.Parameters.Add(":op",    OracleDbType.Varchar2).Value = operacion;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(MapApiLog(reader));

        return list;
    }

    private static SireApiLog MapApiLog(System.Data.Common.DbDataReader r) => new()
    {
        Id           = r.GetInt64(r.GetOrdinal("ID")),
        Fecha        = r.GetDateTime(r.GetOrdinal("FECHA")),
        JobId        = NullStr(r, "JOB_ID"),
        Operacion    = r.GetString(r.GetOrdinal("OPERACION")),
        MetodoHttp   = NullStr(r, "METODO_HTTP"),
        Url          = NullStr(r, "URL"),
        HttpStatus   = NullInt(r, "HTTP_STATUS"),
        DuracionMs   = r.IsDBNull(r.GetOrdinal("DURACION_MS")) ? null : r.GetInt64(r.GetOrdinal("DURACION_MS")),
        Exito        = r.GetInt32(r.GetOrdinal("EXITO")) == 1,
        Mensaje      = NullStr(r, "MENSAJE"),
        TipoRegistro = NullStr(r, "TIPO_REGISTRO"),
    };

    private static SireExportacionJob MapJob(System.Data.Common.DbDataReader r) => new()
    {
        Id                  = r.GetInt32(r.GetOrdinal("ID")),
        JobId               = r.GetString(r.GetOrdinal("JOB_ID")),
        TipoRegistro        = r.GetString(r.GetOrdinal("TIPO_REGISTRO")),
        Periodo             = r.GetString(r.GetOrdinal("PERIODO")),
        UsuarioId           = NullStr(r, "USUARIO_ID") ?? string.Empty,
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
        ProximaConsulta     = NullDate(r, "PROXIMA_CONSULTA"),
    };

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string?   NullStr(System.Data.Common.DbDataReader r, string col)
        => r.IsDBNull(r.GetOrdinal(col)) ? null : r.GetString(r.GetOrdinal(col));

    /// <summary>
    /// Lee un VARCHAR2 y corrige el encoding cuando Oracle 10g (WE8MSWIN1252/WE8ISO8859P1)
    /// devuelve bytes Latin-1 que ODP.NET interpreta mal como UTF-8.
    /// Si la cadena contiene caracteres de reemplazo (U+FFFD) o secuencias típicas de
    /// double-encoding (Ã, ñ→Ã±, etc.) se re-decodifica vía ISO-8859-1 → UTF-8.
    /// </summary>
    private static string? FixStr(System.Data.Common.DbDataReader r, string col)
    {
        if (r.IsDBNull(r.GetOrdinal(col))) return null;
        var s = r.GetString(r.GetOrdinal(col));
        if (string.IsNullOrEmpty(s)) return s;
        // Detectar double-encoding: bytes UTF-8 leídos como Latin-1 generan Ã seguida de otro carácter.
        if (!s.Contains('\uFFFD') && !s.Contains('Ã')) return s;
        try
        {
            var latin1 = System.Text.Encoding.GetEncoding("ISO-8859-1");
            var utf8   = System.Text.Encoding.UTF8;
            var bytes  = latin1.GetBytes(s);
            var fixed_ = utf8.GetString(bytes);
            // Solo usar el valor re-codificado si no generó más caracteres de reemplazo
            return fixed_.Contains('\uFFFD') ? s : fixed_;
        }
        catch
        {
            return s;
        }
    }

    private static int?      NullInt(System.Data.Common.DbDataReader r, string col)
        => r.IsDBNull(r.GetOrdinal(col)) ? null : r.GetInt32(r.GetOrdinal(col));

    private static DateTime? NullDate(System.Data.Common.DbDataReader r, string col)
        => r.IsDBNull(r.GetOrdinal(col)) ? null : r.GetDateTime(r.GetOrdinal(col));

    private static decimal NullDec(System.Data.Common.DbDataReader r, string col)
        => r.IsDBNull(r.GetOrdinal(col)) ? 0m : r.GetDecimal(r.GetOrdinal(col));

    private static object Trunc(string? s, int max)
        => s is null ? DBNull.Value : (object)s[..Math.Min(s.Length, max)];

    // ── SIRE_VALIDA ───────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<List<SireValidaRegistro>> GetRegistrosPropuestaAsync(
        string tipo, string periodo, CancellationToken ct = default)
    {
        var tipoDb    = tipo.Equals("ventas", StringComparison.OrdinalIgnoreCase) ? "1" : "2";
        if (!int.TryParse(periodo, out var periodoNr))
            return [];

        const string sql = @"
            SELECT ID_PROP, CAR_SUNAT, TIPO, PERIODO,
                   F_EMISION, F_VENCTO, TIPDOC, SERIE, NUMERO,
                   ANIO_DAM, NROFIN, TDOCID,
                   RUC, NOMBRE,
                   NVL(BI_GRAV_DG,0)    BI_GRAV_DG,
                   NVL(IGV_IPM_DG,0)    IGV_IPM_DG,
                   NVL(BI_GRAV_DGNG,0)  BI_GRAV_DGNG,
                   NVL(IGV_IPM_DGNG,0)  IGV_IPM_DGNG,
                   NVL(BI_GRAV_DNG,0)   BI_GRAV_DNG,
                   NVL(IGV_IPM_DNG,0)   IGV_IPM_DNG,
                   NVL(VAL_ADQ_NG,0)    VAL_ADQ_NG,
                   NVL(ISC,0)           ISC,
                   NVL(ICBPER,0)        ICBPER,
                   NVL(OTROS_TRIB,0)    OTROS_TRIB,
                   NVL(TOTAL_CP,0)      TOTAL_CP,
                   MONEDA, NVL(CAMBIO,0) CAMBIO,
                   F_DOCREF, TIP_DOCREF, SER_DOCREF, NRO_DOCREF,
                   FLAG_DETRAC, TIPO_NOTA,
                   EST_COMP, INCONSIST,
                   CONCIL_ESTADO, CONCIL_DIFFS,
                   FCH_CARGA, FCH_CONCIL
            FROM   SIG.SIRE_PROPUESTA
            WHERE  TIPO    = :tipo
              AND  PERIODO = :periodo
            ORDER  BY CAR_SUNAT";

        await using var conn = await OpenConnAsync(ct);
        using var cmd = new OracleCommand(sql, conn);
        cmd.Parameters.Add(new OracleParameter("tipo",    OracleDbType.Varchar2) { Value = tipoDb    });
        cmd.Parameters.Add(new OracleParameter("periodo", OracleDbType.Int32)    { Value = periodoNr });

        var list = new List<SireValidaRegistro>();
        await using var rdr = await cmd.ExecuteReaderAsync(ct);
        while (await rdr.ReadAsync(ct))
        {
            list.Add(new SireValidaRegistro
            {
                IdProp       = rdr.GetInt64(rdr.GetOrdinal("ID_PROP")),
                CarSunat     = rdr.GetString(rdr.GetOrdinal("CAR_SUNAT")),
                Tipo         = rdr.GetString(rdr.GetOrdinal("TIPO")),
                Periodo      = rdr.GetInt32(rdr.GetOrdinal("PERIODO")),
                FEmision     = NullDate(rdr, "F_EMISION"),
                FVencto      = NullDate(rdr, "F_VENCTO"),
                Tipdoc       = NullStr(rdr,  "TIPDOC"),
                Serie        = NullStr(rdr,  "SERIE"),
                Numero       = NullStr(rdr,  "NUMERO"),
                AnioDam      = NullStr(rdr,  "ANIO_DAM"),
                Nrofin       = NullStr(rdr,  "NROFIN"),
                Tdocid       = NullStr(rdr,  "TDOCID"),
                Ruc          = NullStr(rdr,  "RUC"),
                Nombre       = FixStr(rdr,   "NOMBRE"),
                BiGravDg     = NullDec(rdr,  "BI_GRAV_DG"),
                IgvIpmDg     = NullDec(rdr,  "IGV_IPM_DG"),
                BiGravDgng   = NullDec(rdr,  "BI_GRAV_DGNG"),
                IgvIpmDgng   = NullDec(rdr,  "IGV_IPM_DGNG"),
                BiGravDng    = NullDec(rdr,  "BI_GRAV_DNG"),
                IgvIpmDng    = NullDec(rdr,  "IGV_IPM_DNG"),
                ValAdqNg     = NullDec(rdr,  "VAL_ADQ_NG"),
                Isc          = NullDec(rdr,  "ISC"),
                Icbper       = NullDec(rdr,  "ICBPER"),
                OtrosTrib    = NullDec(rdr,  "OTROS_TRIB"),
                TotalCp      = NullDec(rdr,  "TOTAL_CP"),
                Moneda       = NullStr(rdr,  "MONEDA"),
                Cambio       = NullDec(rdr,  "CAMBIO"),
                FDocref      = NullDate(rdr, "F_DOCREF"),
                TipDocref    = NullStr(rdr,  "TIP_DOCREF"),
                SerDocref    = NullStr(rdr,  "SER_DOCREF"),
                NroDocref    = NullStr(rdr,  "NRO_DOCREF"),
                FlagDetrac   = NullStr(rdr,  "FLAG_DETRAC"),
                TipoNota     = NullStr(rdr,  "TIPO_NOTA"),
                EstComp      = NullStr(rdr,  "EST_COMP"),
                Inconsist    = NullStr(rdr,  "INCONSIST"),
                ConcilEstado = NullStr(rdr,  "CONCIL_ESTADO"),
                ConcilDiffs  = NullStr(rdr,  "CONCIL_DIFFS"),
                FchCarga     = NullDate(rdr, "FCH_CARGA"),
                FchConcil    = NullDate(rdr, "FCH_CONCIL"),
            });
        }
        return list;
    }

    /// <inheritdoc/>
    public async Task<List<PropuestaPeriodoResumen>> GetPropuestasResumenAsync(
        string tipo, CancellationToken ct = default)
    {
        var tipoDb = tipo.Equals("ventas", StringComparison.OrdinalIgnoreCase) ? "1" : "2";

        const string sql = @"
            SELECT TIPO, PERIODO, MAX(JOB_ID) JOB_ID,
                   COUNT(*)                        TOTAL_REGISTROS,
                   MAX(FCH_CARGA)                  FCH_CARGA,
                   SUM(NVL(BI_GRAV_DG,0))          TOTAL_BASE,
                   SUM(NVL(IGV_IPM_DG,0))          TOTAL_IGV,
                   SUM(NVL(TOTAL_CP,0))            TOTAL_IMPORTE,
                   MIN(CONCIL_ESTADO)              CONCIL_ESTADO
            FROM   SIG.SIRE_PROPUESTA
            WHERE  TIPO = :tipo
            GROUP  BY TIPO, PERIODO
            ORDER  BY PERIODO ASC";

        await using var conn = await OpenConnAsync(ct);
        using var cmd = new OracleCommand(sql, conn);
        cmd.Parameters.Add(new OracleParameter("tipo", OracleDbType.Varchar2) { Value = tipoDb });

        var list = new List<PropuestaPeriodoResumen>();
        await using var rdr = await cmd.ExecuteReaderAsync(ct);
        while (await rdr.ReadAsync(ct))
        {
            list.Add(new PropuestaPeriodoResumen
            {
                Tipo           = rdr.GetString(rdr.GetOrdinal("TIPO")),
                Periodo        = rdr.GetInt32(rdr.GetOrdinal("PERIODO")),
                JobId          = NullStr(rdr, "JOB_ID"),
                TotalRegistros = rdr.GetInt32(rdr.GetOrdinal("TOTAL_REGISTROS")),
                FchCarga       = NullDate(rdr, "FCH_CARGA"),
                TotalBase      = NullDec(rdr, "TOTAL_BASE"),
                TotalIgv       = NullDec(rdr, "TOTAL_IGV"),
                TotalImporte   = NullDec(rdr, "TOTAL_IMPORTE"),
                ConcilEstado   = NullStr(rdr, "CONCIL_ESTADO") ?? "0",
            });
        }
        return list;
    }

    /// <inheritdoc/>
    public async Task<int> EliminarPropuestaAsync(
        string tipo, int periodo, CancellationToken ct = default)
    {
        var tipoDb = tipo.Equals("ventas", StringComparison.OrdinalIgnoreCase) ? "1" : "2";

        await using var conn = await OpenConnAsync(ct);

        // Las 4 tablas deben limpiarse atómicamente: SIRE_CONCIL, SIRE_CONCIL_RESUMEN,
        // SIRE_LEGACY y SIRE_PROPUESTA. Si alguno falla, se revierte todo.
        conn.AutoCommit = false;
        await using var tx = conn.BeginTransaction();
        try
        {
            // 1. Cruzado / Diferencias
            await using var cmd1 = new OracleCommand(
                "DELETE FROM SIG.SIRE_CONCIL WHERE TIPO = :tipo AND PERIODO = :periodo", conn);
            cmd1.Transaction = tx;
            cmd1.Parameters.Add(new OracleParameter("tipo",    OracleDbType.Varchar2) { Value = tipoDb  });
            cmd1.Parameters.Add(new OracleParameter("periodo", OracleDbType.Int32)    { Value = periodo });
            await cmd1.ExecuteNonQueryAsync(ct);

            // 1b. Todos los excluidos del período: NC_AUTO, MANUAL y cualquier otro motivo.
            //     EliminarPropuesta = limpieza total. No se preserva nada.
            try
            {
                await using var cmdExcl = new OracleCommand(
                    "DELETE FROM SIG.SIRE_EXCLUIDOS_LOGIX WHERE TIPO = :tipo AND PERIODO = :periodo", conn);
                cmdExcl.Transaction = tx;
                cmdExcl.Parameters.Add(new OracleParameter("tipo",    OracleDbType.Varchar2) { Value = tipoDb  });
                cmdExcl.Parameters.Add(new OracleParameter("periodo", OracleDbType.Int32)    { Value = periodo });
                await cmdExcl.ExecuteNonQueryAsync(ct);
            }
            catch (OracleException exExcl) when (exExcl.Number == 942) // ORA-00942: table or view does not exist
            {
                // Tabla aún no creada — se omite sin interrumpir la operación
            }

            // 2. Resumen de conciliación
            await using var cmd2 = new OracleCommand(
                "DELETE FROM SIG.SIRE_CONCIL_RESUMEN WHERE TIPO = :tipo AND PERIODO = :periodo", conn);
            cmd2.Transaction = tx;
            cmd2.Parameters.Add(new OracleParameter("tipo",    OracleDbType.Varchar2) { Value = tipoDb  });
            cmd2.Parameters.Add(new OracleParameter("periodo", OracleDbType.Int32)    { Value = periodo });
            await cmd2.ExecuteNonQueryAsync(ct);

            // 3. Legacy (datos ERP)
            await using var cmd3 = new OracleCommand(
                "DELETE FROM SIG.SIRE_LEGACY WHERE TIPO = :tipo AND PERIODO = :periodo", conn);
            cmd3.Transaction = tx;
            cmd3.Parameters.Add(new OracleParameter("tipo",    OracleDbType.Varchar2) { Value = tipoDb  });
            cmd3.Parameters.Add(new OracleParameter("periodo", OracleDbType.Int32)    { Value = periodo });
            await cmd3.ExecuteNonQueryAsync(ct);

            // 4. Propuesta SUNAT (tabla principal — retornamos su conteo)
            await using var cmd4 = new OracleCommand(
                "DELETE FROM SIG.SIRE_PROPUESTA WHERE TIPO = :tipo AND PERIODO = :periodo", conn);
            cmd4.Transaction = tx;
            cmd4.Parameters.Add(new OracleParameter("tipo",    OracleDbType.Varchar2) { Value = tipoDb  });
            cmd4.Parameters.Add(new OracleParameter("periodo", OracleDbType.Int32)    { Value = periodo });
            var borrados = await cmd4.ExecuteNonQueryAsync(ct);

            tx.Commit();
            return borrados;
        }
        catch
        {
            try { tx.Rollback(); } catch { /* no ocultar la excepción original */ }
            throw;
        }
        finally
        {
            conn.AutoCommit = true;
        }
    }

    /// <inheritdoc/>
    public async Task<string> ConciliarPropuestaAsync(
        string tipo, int periodo, CancellationToken ct = default)
    {
        var tipoDb = tipo.Equals("ventas", StringComparison.OrdinalIgnoreCase) ? "1" : "2";

        await using var conn = await OpenConnAsync(ct);

        // SP1 + SP2 deben ser atómicos.
        // Paso 0 (dentro de la TX): borrar NC_AUTO previas del período para que
        // SP_SIRE_AUTO_EXCLUIR_NC (Paso 4) las regenere con los nuevos ID_CONCIL.
        // Las exclusiones MANUAL no se tocan — son decisiones del usuario.
        conn.AutoCommit = false;
        await using var tx = conn.BeginTransaction();
        try
        {
            // Paso 0: limpiar NC_AUTO — se regeneran en Paso 4
            try
            {
                using var cmd0 = new OracleCommand(
                    "DELETE FROM SIG.SIRE_EXCLUIDOS_LOGIX WHERE TIPO=:tipo AND PERIODO=:periodo AND MOTIVO='NC_AUTO'",
                    conn);
                cmd0.Transaction = tx;
                cmd0.Parameters.Add(new OracleParameter("tipo",    OracleDbType.Varchar2) { Value = tipoDb  });
                cmd0.Parameters.Add(new OracleParameter("periodo", OracleDbType.Int32)    { Value = periodo });
                await cmd0.ExecuteNonQueryAsync(ct);
            }
            catch (OracleException exExcl) when (exExcl.Number == 942) { /* tabla aún no creada */ }

            // Paso 1: carga legacy
            using (var cmd1 = new OracleCommand(
                "BEGIN SIG.SP_SIRE_CARGA_LEGACY(:tipo, :periodo, :usuario); END;", conn))
            {
                cmd1.Transaction = tx;
                cmd1.Parameters.Add(new OracleParameter("tipo",    OracleDbType.Varchar2) { Value = tipoDb  });
                cmd1.Parameters.Add(new OracleParameter("periodo", OracleDbType.Int32)    { Value = periodo });
                cmd1.Parameters.Add(new OracleParameter("usuario", OracleDbType.Varchar2) { Value = "WEB"   });
                await cmd1.ExecuteNonQueryAsync(ct);
            }

            // Paso 2: conciliar
            using (var cmd2 = new OracleCommand(
                "BEGIN SIG.SP_SIRE_CONCILIAR(:tipo, :periodo, :usuario); END;", conn))
            {
                cmd2.Transaction = tx;
                cmd2.Parameters.Add(new OracleParameter("tipo",    OracleDbType.Varchar2) { Value = tipoDb  });
                cmd2.Parameters.Add(new OracleParameter("periodo", OracleDbType.Int32)    { Value = periodo });
                cmd2.Parameters.Add(new OracleParameter("usuario", OracleDbType.Varchar2) { Value = "WEB"   });
                await cmd2.ExecuteNonQueryAsync(ct);
            }

            tx.Commit();
        }
        catch
        {
            try { tx.Rollback(); } catch { /* no ocultar excepción original */ }
            throw;
        }
        finally
        {
            conn.AutoCommit = true;
        }

        // ── Paso 3: re-aplicar exclusiones MANUAL ────────────────────────────
        // SP_SIRE_CONCILIAR recreó SIRE_CONCIL con nuevos ID_CONCIL.
        // Actualizamos SIRE_EXCLUIDOS_LOGIX.ID_CONCIL (el viejo ya no existe)
        // y marcamos SIRE_CONCIL.ESTADO='EXCLUIDO'.
        // Se aplica siempre, sin importar si ID_CONCIL era NULL o tenía valor antiguo.
        try
        {
            const string sqlRevinc = @"
                UPDATE SIG.SIRE_EXCLUIDOS_LOGIX EX
                SET    EX.ID_CONCIL = (
                           SELECT C.ID_CONCIL FROM SIG.SIRE_CONCIL C
                           WHERE  C.ID_PROP  = EX.ID_PROP
                             AND  C.TIPO     = EX.TIPO
                             AND  C.PERIODO  = EX.PERIODO
                             AND  ROWNUM = 1
                       )
                WHERE  EX.TIPO    = :tipo
                  AND  EX.PERIODO = :periodo
                  AND  EX.MOTIVO  = 'MANUAL'
                  AND  EX.ESTADO  = 'A'
                  AND  EX.ID_PROP IS NOT NULL";

            using var cmdRevinc = new OracleCommand(sqlRevinc, conn);
            cmdRevinc.Parameters.Add(new OracleParameter("tipo",    OracleDbType.Varchar2) { Value = tipoDb  });
            cmdRevinc.Parameters.Add(new OracleParameter("periodo", OracleDbType.Int32)    { Value = periodo });
            var nRevinc = await cmdRevinc.ExecuteNonQueryAsync(ct);

            // Marcar SIRE_CONCIL como EXCLUIDO para todas las exclusiones manuales activas
            const string sqlMarcaExcl = @"
                UPDATE SIG.SIRE_CONCIL
                SET    ESTADO = 'EXCLUIDO', DIFF_CAMPOS = 'MANUAL'
                WHERE  ID_CONCIL IN (
                           SELECT ID_CONCIL FROM SIG.SIRE_EXCLUIDOS_LOGIX
                           WHERE  TIPO    = :tipo
                             AND  PERIODO = :periodo
                             AND  MOTIVO  = 'MANUAL'
                             AND  ESTADO  = 'A'
                             AND  ID_CONCIL IS NOT NULL
                       )";
            using var cmdMarca = new OracleCommand(sqlMarcaExcl, conn);
            cmdMarca.Parameters.Add(new OracleParameter("tipo",    OracleDbType.Varchar2) { Value = tipoDb  });
            cmdMarca.Parameters.Add(new OracleParameter("periodo", OracleDbType.Int32)    { Value = periodo });
            await cmdMarca.ExecuteNonQueryAsync(ct);

            if (nRevinc > 0)
                _logger.LogInformation("[SIRE-CONCIL] {N} exclusiones manuales re-aplicadas tipo={T} periodo={P}",
                    nRevinc, tipoDb, periodo);
        }
        catch (Exception exRev)
        {
            _logger.LogWarning("[SIRE-CONCIL] Error al re-vincular exclusiones manuales: {Msg}", exRev.Message);
        }

        // ── Paso 4: auto-excluir N/C sin match en ERP ────────────────────────
        // Las NC_AUTO se borraron en el Paso 0 → el SP las regenera sin problemas.
        try
        {
            using var cmdNc = new OracleCommand(
                "BEGIN SIG.SP_SIRE_AUTO_EXCLUIR_NC(:tipo, :periodo, :usuario); END;", conn);
            cmdNc.Parameters.Add(new OracleParameter("tipo",    OracleDbType.Varchar2) { Value = tipoDb  });
            cmdNc.Parameters.Add(new OracleParameter("periodo", OracleDbType.Int32)    { Value = periodo });
            cmdNc.Parameters.Add(new OracleParameter("usuario", OracleDbType.Varchar2) { Value = "WEB"   });
            await cmdNc.ExecuteNonQueryAsync(ct);
            _logger.LogInformation("[SIRE-CONCIL] NC automáticas procesadas tipo={T} periodo={P}", tipoDb, periodo);
        }
        catch (Exception exNc)
        {
            _logger.LogWarning("[SIRE-CONCIL] SP_SIRE_AUTO_EXCLUIR_NC falló (no bloquea): {Msg}", exNc.Message);
        }

        // ── Paso 5: sincronizar SIRE_PROPUESTA.CONCIL_ESTADO='5' ─────────────
        // SP_SIRE_CONCILIAR asigna '3' (SOLO_SUNAT) a los excluidos.
        // Aquí corregimos a '5' (Excluido) para MANUAL + NC_AUTO,
        // para que la pestaña "Propuesta SUNAT" los muestre correctamente.
        try
        {
            const string sqlSyncEstado = @"
                UPDATE SIG.SIRE_PROPUESTA P
                SET    P.CONCIL_ESTADO = '5',
                       P.FCH_CONCIL    = SYSDATE
                WHERE  P.TIPO    = :tipo
                  AND  P.PERIODO = :periodo
                  AND  EXISTS (
                           SELECT 1 FROM SIG.SIRE_CONCIL C
                           WHERE  C.ID_PROP = P.ID_PROP
                             AND  C.ESTADO  = 'EXCLUIDO'
                       )";
            using var cmdSync = new OracleCommand(sqlSyncEstado, conn);
            cmdSync.Parameters.Add(new OracleParameter("tipo",    OracleDbType.Varchar2) { Value = tipoDb  });
            cmdSync.Parameters.Add(new OracleParameter("periodo", OracleDbType.Int32)    { Value = periodo });
            await cmdSync.ExecuteNonQueryAsync(ct);
        }
        catch (Exception exSync)
        {
            _logger.LogWarning("[SIRE-CONCIL] Sync CONCIL_ESTADO='5' falló (no bloquea): {Msg}", exSync.Message);
        }

        // ── Paso 6: actualizar TOTAL_EXCL final (MANUAL + NC_AUTO) ───────────
        try
        {
            // ODP.NET positional binding: cada ocurrencia del mismo nombre bind requiere
            // un parámetro independiente. Se usan aliases tipo2/periodo2 para el WHERE externo.
            const string sqlTotalExcl = @"
                UPDATE SIG.SIRE_CONCIL_RESUMEN
                SET    TOTAL_EXCL = (
                           SELECT COUNT(*) FROM SIG.SIRE_CONCIL
                           WHERE  TIPO=:tipo AND PERIODO=:periodo AND ESTADO='EXCLUIDO'
                       )
                WHERE  TIPO=:tipo2 AND PERIODO=:periodo2";
            using var cmdTotalExcl = new OracleCommand(sqlTotalExcl, conn);
            cmdTotalExcl.Parameters.Add(new OracleParameter("tipo",     OracleDbType.Varchar2) { Value = tipoDb  });
            cmdTotalExcl.Parameters.Add(new OracleParameter("periodo",  OracleDbType.Int32)    { Value = periodo });
            cmdTotalExcl.Parameters.Add(new OracleParameter("tipo2",    OracleDbType.Varchar2) { Value = tipoDb  });
            cmdTotalExcl.Parameters.Add(new OracleParameter("periodo2", OracleDbType.Int32)    { Value = periodo });
            await cmdTotalExcl.ExecuteNonQueryAsync(ct);
        }
        catch (Exception exTot)
        {
            _logger.LogWarning("[SIRE-CONCIL] TOTAL_EXCL final falló (no bloquea): {Msg}", exTot.Message);
        }

        // Paso 3: leer resumen
        const string sqlRes = @"
            SELECT TOTAL_OK, TOTAL_DIFER, TOTAL_SOLO_SUNAT, TOTAL_SOLO_LEG,
                   TOTAL_SUNAT, TOTAL_LEGACY, TOTAL_EXCL,
                   ROUND(NVL(DIFF_TOTAL,0),2) DIFF_TOTAL
            FROM   SIG.SIRE_CONCIL_RESUMEN
            WHERE  TIPO = :tipo AND PERIODO = :periodo";

        using var cmdRes = new OracleCommand(sqlRes, conn);
        cmdRes.Parameters.Add(new OracleParameter("tipo",    OracleDbType.Varchar2) { Value = tipoDb  });
        cmdRes.Parameters.Add(new OracleParameter("periodo", OracleDbType.Int32)    { Value = periodo });
        await using var rdr = await cmdRes.ExecuteReaderAsync(ct);
        if (await rdr.ReadAsync(ct))
        {
            var ok         = rdr.IsDBNull(0) ? 0  : rdr.GetInt32(0);
            var dif        = rdr.IsDBNull(1) ? 0  : rdr.GetInt32(1);
            var soloSunat  = rdr.IsDBNull(2) ? 0  : rdr.GetInt32(2);
            var soloLegacy = rdr.IsDBNull(3) ? 0  : rdr.GetInt32(3);
            var totSunat   = rdr.IsDBNull(4) ? 0  : rdr.GetInt32(4);
            var totLegacy  = rdr.IsDBNull(5) ? 0  : rdr.GetInt32(5);
            var totExcl    = rdr.IsDBNull(6) ? 0  : rdr.GetInt32(6);
            var diffTotal  = rdr.IsDBNull(7) ? 0m : rdr.GetDecimal(7);
            return $"SUNAT:{totSunat} Legacy:{totLegacy} | OK:{ok} Diferencias:{dif} Solo-SUNAT:{soloSunat} Solo-Legacy:{soloLegacy} Excluidos:{totExcl} | Diff.Total:S/{diffTotal:N2}";
        }
        return "Conciliación completada (sin resumen disponible).";
    }

    /// <inheritdoc/>
    public async Task<SireConcilResumen?> GetConcilResumenAsync(
        string tipo, string periodo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(periodo) || !int.TryParse(periodo, out var periodoNr))
            return null;

        var tipoDb = tipo.Equals("ventas", StringComparison.OrdinalIgnoreCase) ? "1" : "2";

        const string sql = @"
            SELECT TOTAL_SUNAT, TOTAL_LEGACY,
                   TOTAL_OK, TOTAL_DIFER, TOTAL_SOLO_SUNAT, TOTAL_SOLO_LEG, TOTAL_EXCL,
                   ROUND(NVL(SUMA_SUNAT_BASE,0),2)  SUMA_SUNAT_BASE,
                   ROUND(NVL(SUMA_SUNAT_IGV,0),2)   SUMA_SUNAT_IGV,
                   ROUND(NVL(SUMA_SUNAT_TOTAL,0),2) SUMA_SUNAT_TOTAL,
                   ROUND(NVL(SUMA_LEG_BASE,0),2)    SUMA_LEG_BASE,
                   ROUND(NVL(SUMA_LEG_IGV,0),2)     SUMA_LEG_IGV,
                   ROUND(NVL(SUMA_LEG_TOTAL,0),2)   SUMA_LEG_TOTAL,
                   ROUND(NVL(DIFF_BASE,0),2)         DIFF_BASE,
                   ROUND(NVL(DIFF_IGV,0),2)          DIFF_IGV,
                   ROUND(NVL(DIFF_TOTAL,0),2)        DIFF_TOTAL,
                   FCH_CONCIL, CONCIL_POR, ESTADO_CIERRE
            FROM   SIG.SIRE_CONCIL_RESUMEN
            WHERE  TIPO = :tipo AND PERIODO = :periodo";

        await using var conn = await OpenConnAsync(ct);
        using var cmd = new OracleCommand(sql, conn);
        cmd.Parameters.Add(new OracleParameter("tipo",    OracleDbType.Varchar2) { Value = tipoDb   });
        cmd.Parameters.Add(new OracleParameter("periodo", OracleDbType.Int32)    { Value = periodoNr });

        await using var rdr = await cmd.ExecuteReaderAsync(ct);
        if (await rdr.ReadAsync(ct))
        {
            return new SireConcilResumen
            {
                TotalSunat     = rdr.IsDBNull(rdr.GetOrdinal("TOTAL_SUNAT"))     ? 0  : rdr.GetInt32(rdr.GetOrdinal("TOTAL_SUNAT")),
                TotalLegacy    = rdr.IsDBNull(rdr.GetOrdinal("TOTAL_LEGACY"))    ? 0  : rdr.GetInt32(rdr.GetOrdinal("TOTAL_LEGACY")),
                TotalOk        = rdr.IsDBNull(rdr.GetOrdinal("TOTAL_OK"))        ? 0  : rdr.GetInt32(rdr.GetOrdinal("TOTAL_OK")),
                TotalDifer     = rdr.IsDBNull(rdr.GetOrdinal("TOTAL_DIFER"))     ? 0  : rdr.GetInt32(rdr.GetOrdinal("TOTAL_DIFER")),
                TotalSoloSunat = rdr.IsDBNull(rdr.GetOrdinal("TOTAL_SOLO_SUNAT"))? 0  : rdr.GetInt32(rdr.GetOrdinal("TOTAL_SOLO_SUNAT")),
                TotalSoloLeg   = rdr.IsDBNull(rdr.GetOrdinal("TOTAL_SOLO_LEG"))  ? 0  : rdr.GetInt32(rdr.GetOrdinal("TOTAL_SOLO_LEG")),
                TotalExcl      = rdr.IsDBNull(rdr.GetOrdinal("TOTAL_EXCL"))      ? 0  : rdr.GetInt32(rdr.GetOrdinal("TOTAL_EXCL")),
                SumaSunatBase  = NullDec(rdr, "SUMA_SUNAT_BASE"),
                SumaSunatIgv   = NullDec(rdr, "SUMA_SUNAT_IGV"),
                SumaSunatTotal = NullDec(rdr, "SUMA_SUNAT_TOTAL"),
                SumaLegBase    = NullDec(rdr, "SUMA_LEG_BASE"),
                SumaLegIgv     = NullDec(rdr, "SUMA_LEG_IGV"),
                SumaLegTotal   = NullDec(rdr, "SUMA_LEG_TOTAL"),
                DiffBase       = NullDec(rdr, "DIFF_BASE"),
                DiffIgv        = NullDec(rdr, "DIFF_IGV"),
                DiffTotal      = NullDec(rdr, "DIFF_TOTAL"),
                FchConcil      = NullDate(rdr, "FCH_CONCIL"),
                ConcilPor      = NullStr(rdr, "CONCIL_POR"),
                EstadoCierre   = NullStr(rdr, "ESTADO_CIERRE") ?? "ABIERTO",
            };
        }
        return null;
    }

    /// <inheritdoc/>
    public async Task<List<SireLegacyRegistro>> GetLegacyAsync(
        string tipo, string periodo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(periodo) || !int.TryParse(periodo, out var periodoNr))
            return [];

        var tipoDb = tipo.Equals("ventas", StringComparison.OrdinalIgnoreCase) ? "1" : "2";

        // SELECT completo (incluye columnas del patch 04_SIRE_PATCH_LEGACY_CAMPOS.sql)
        const string sqlCompleto = @"
            SELECT ID_LEGACY, TIPO, PERIODO, TABLA_ORIGEN, ID_ORIGEN,
                   F_EMISION, F_VENCTO, TIPDOC, SERIE, NUMERO, TDOCID,
                   RUC, NOMBRE,
                   NVL(BASE_IMPONIBLE,0) BASE_IMPONIBLE,
                   NVL(IGV,0)            IGV,
                   NVL(OTROS,0)          OTROS,
                   NVL(TOTAL,0)          TOTAL,
                   NVL(ISC,0)            ISC,
                   NVL(VAL_ADQ_NG,0)     VAL_ADQ_NG,
                   NVL(VAL_FACT_GRAT,0)  VAL_FACT_GRAT,
                   TIPO_NOTA, FLAG_DETRAC, ANIO_DAM,
                   TIP_DOCREF, SER_DOCREF, NRO_DOCREF, F_DOCREF,
                   MONEDA, NVL(CAMBIO,1) CAMBIO,
                   DOC_REF, EST_ERP, ANULADO, ID_PROP_MATCH
            FROM   SIG.SIRE_LEGACY
            WHERE  TIPO = :tipo AND PERIODO = :periodo
            ORDER  BY TIPDOC, SERIE, NUMERO";

        // SELECT reducido para BD sin patch (sin columnas del ALTER TABLE)
        const string sqlBase = @"
            SELECT ID_LEGACY, TIPO, PERIODO, TABLA_ORIGEN, ID_ORIGEN,
                   F_EMISION, F_VENCTO, TIPDOC, SERIE, NUMERO, TDOCID,
                   RUC, NOMBRE,
                   NVL(BASE_IMPONIBLE,0) BASE_IMPONIBLE,
                   NVL(IGV,0)            IGV,
                   NVL(OTROS,0)          OTROS,
                   NVL(TOTAL,0)          TOTAL,
                   MONEDA, NVL(CAMBIO,1) CAMBIO,
                   DOC_REF, EST_ERP, ANULADO, ID_PROP_MATCH
            FROM   SIG.SIRE_LEGACY
            WHERE  TIPO = :tipo AND PERIODO = :periodo
            ORDER  BY TIPDOC, SERIE, NUMERO";

        await using var conn = await OpenConnAsync(ct);

        // Intentar primero con SELECT completo; si ORA-00904 (columna inexistente),
        // la BD no tiene el patch aplicado → reintentar con SELECT base.
        try
        {
            return await EjecutarGetLegacyAsync(conn, sqlCompleto, tipoDb, periodoNr, patchAplicado: true, ct);
        }
        catch (OracleException ex) when (ex.Number == 904)
        {
            _logger.LogWarning(
                "[SIRE-LEGACY] ORA-00904 detectado — patch 04_SIRE_PATCH_LEGACY_CAMPOS.sql no aplicado. " +
                "Usando SELECT reducido. Aplique el patch en Oracle para habilitar todos los campos.");
            return await EjecutarGetLegacyAsync(conn, sqlBase, tipoDb, periodoNr, patchAplicado: false, ct);
        }
    }

    private static async Task<List<SireLegacyRegistro>> EjecutarGetLegacyAsync(
        OracleConnection conn, string sql, string tipoDb, int periodoNr,
        bool patchAplicado, CancellationToken ct)
    {
        using var cmd = new OracleCommand(sql, conn);
        cmd.Parameters.Add(new OracleParameter("tipo",    OracleDbType.Varchar2) { Value = tipoDb   });
        cmd.Parameters.Add(new OracleParameter("periodo", OracleDbType.Int32)    { Value = periodoNr });

        var list = new List<SireLegacyRegistro>();
        await using var rdr = await cmd.ExecuteReaderAsync(ct);
        while (await rdr.ReadAsync(ct))
        {
            list.Add(new SireLegacyRegistro
            {
                IdLegacy      = rdr.IsDBNull(rdr.GetOrdinal("ID_LEGACY"))    ? 0  : Convert.ToInt64(rdr["ID_LEGACY"]),
                Tipo          = rdr.GetString(rdr.GetOrdinal("TIPO")),
                Periodo       = rdr.GetInt32(rdr.GetOrdinal("PERIODO")),
                TablaOrigen   = NullStr(rdr, "TABLA_ORIGEN"),
                IdOrigen      = NullStr(rdr, "ID_ORIGEN"),
                FEmision      = NullDate(rdr, "F_EMISION"),
                FVencto       = NullDate(rdr, "F_VENCTO"),
                Tipdoc        = NullStr(rdr, "TIPDOC"),
                Serie         = NullStr(rdr, "SERIE"),
                Numero        = NullStr(rdr, "NUMERO"),
                Tdocid        = NullStr(rdr, "TDOCID"),
                Ruc           = NullStr(rdr, "RUC"),
                Nombre        = NullStr(rdr, "NOMBRE"),
                BaseImponible = NullDec(rdr, "BASE_IMPONIBLE"),
                Igv           = NullDec(rdr, "IGV"),
                Otros         = NullDec(rdr, "OTROS"),
                Total         = NullDec(rdr, "TOTAL"),
                Isc           = patchAplicado ? NullDec(rdr, "ISC")          : 0m,
                ValAdqNg      = patchAplicado ? NullDec(rdr, "VAL_ADQ_NG")   : 0m,
                ValFactGrat   = patchAplicado ? NullDec(rdr, "VAL_FACT_GRAT"): 0m,
                TipoNota      = patchAplicado ? NullStr(rdr, "TIPO_NOTA")    : null,
                FlagDetrac    = patchAplicado ? NullStr(rdr, "FLAG_DETRAC")  : null,
                AnioDam       = patchAplicado ? NullStr(rdr, "ANIO_DAM")     : null,
                TipDocref     = patchAplicado ? NullStr(rdr, "TIP_DOCREF")   : null,
                SerDocref     = patchAplicado ? NullStr(rdr, "SER_DOCREF")   : null,
                NroDocref     = patchAplicado ? NullStr(rdr, "NRO_DOCREF")   : null,
                FDocref       = patchAplicado ? NullDate(rdr, "F_DOCREF")    : null,
                Moneda        = NullStr(rdr, "MONEDA"),
                Cambio        = NullDec(rdr, "CAMBIO"),
                DocRef        = NullStr(rdr, "DOC_REF"),
                EstErp        = NullStr(rdr, "EST_ERP"),
                Anulado       = NullStr(rdr, "ANULADO") ?? "N",
                IdPropMatch   = rdr.IsDBNull(rdr.GetOrdinal("ID_PROP_MATCH")) ? null : Convert.ToInt64(rdr["ID_PROP_MATCH"]),
            });
        }
        return list;
    }

    /// <inheritdoc/>
    public async Task<List<SireConcilDetalle>> GetConcilDetalleAsync(
        string tipo, string periodo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(periodo) || !int.TryParse(periodo, out var periodoNr))
            return [];

        var tipoDb = tipo.Equals("ventas", StringComparison.OrdinalIgnoreCase) ? "1" : "2";

        const string sql = @"
            SELECT C.ID_CONCIL, C.TIPO, C.PERIODO, C.ID_PROP, C.ID_LEGACY,
                   C.TIPDOC, C.SERIE, C.NUMERO, C.F_EMISION, C.RUC, C.NOMBRE,
                   C.ESTADO,
                   NVL(C.SUNAT_BASE,0)      SUNAT_BASE,
                   NVL(C.SUNAT_IGV,0)       SUNAT_IGV,
                   NVL(C.SUNAT_TOTAL,0)     SUNAT_TOTAL,
                   C.SUNAT_MONEDA,          C.SUNAT_EST,
                   NVL(C.LEG_BASE,0)        LEG_BASE,
                   NVL(C.LEG_IGV,0)         LEG_IGV,
                   NVL(C.LEG_TOTAL,0)       LEG_TOTAL,
                   C.LEG_MONEDA,            C.LEG_EST,
                   NVL(C.DIFF_TOTAL_CP,0)   DIFF_TOTAL_CP,
                   NVL(C.DIFF_BASE,0)       DIFF_BASE,
                   NVL(C.DIFF_IGV,0)        DIFF_IGV,
                   C.DIFF_FECHA,            C.DIFF_CAMPOS,
                   NVL(C.SUNAT_VALADQNG,0)  SUNAT_VALADQNG,
                   NVL(C.LEG_VALADQNG,0)    LEG_VALADQNG,
                   NVL(C.SUNAT_ISC,0)       SUNAT_ISC,
                   NVL(C.LEG_ISC,0)         LEG_ISC,
                   NVL(C.SUNAT_OTROS,0)     SUNAT_OTROS,
                   NVL(C.LEG_OTROS,0)       LEG_OTROS,
                   C.SUNAT_CAMBIO,          C.LEG_CAMBIO,
                   C.REVISADO,              C.OBS_MANUAL,
                   NVL(L.TIP_DOCREF, P.TIP_DOCREF)  TIP_DOCREF,
                   NVL(L.SER_DOCREF, P.SER_DOCREF)  SER_DOCREF,
                   NVL(L.NRO_DOCREF, P.NRO_DOCREF)  NRO_DOCREF,
                   NVL(L.F_DOCREF,   P.F_DOCREF)     F_DOCREF,
                   NVL(L.TIPO_NOTA,  P.TIPO_NOTA)    TIPO_NOTA,
                   EX.MOTIVO        EXCL_MOTIVO,
                   EX.OBS           EXCL_OBS,
                   EX.USUARIO       EXCL_USUARIO,
                   EX.FCH_EXCLUSION EXCL_FCH,
                   C.VALIDEZ_CP,    C.VALIDEZ_RUC,
                   C.VALIDEZ_DOM,   C.FCH_VALIDEZ
            FROM   SIG.SIRE_CONCIL C
            LEFT JOIN SIG.SIRE_LEGACY          L  ON L.ID_LEGACY = C.ID_LEGACY
            LEFT JOIN SIG.SIRE_PROPUESTA       P  ON P.ID_PROP   = C.ID_PROP
            -- Subquery con ROWNUM=1 para garantizar una sola fila de exclusión por concil
            -- y evitar multiplicación de filas si existieran varios registros activos.
            LEFT JOIN (
                SELECT ID_CONCIL, MOTIVO, OBS, USUARIO, FCH_EXCLUSION
                FROM   SIG.SIRE_EXCLUIDOS_LOGIX
                WHERE  ESTADO = 'A'
            ) EX ON EX.ID_CONCIL = C.ID_CONCIL
            WHERE  C.TIPO = :tipo AND C.PERIODO = :periodo
            ORDER  BY C.ESTADO, C.TIPDOC, C.SERIE, C.NUMERO";

        await using var conn = await OpenConnAsync(ct);
        using var cmd = new OracleCommand(sql, conn);
        cmd.Parameters.Add(new OracleParameter("tipo",    OracleDbType.Varchar2) { Value = tipoDb   });
        cmd.Parameters.Add(new OracleParameter("periodo", OracleDbType.Int32)    { Value = periodoNr });

        var list = new List<SireConcilDetalle>();
        await using var rdr = await cmd.ExecuteReaderAsync(ct);
        while (await rdr.ReadAsync(ct))
        {
            list.Add(new SireConcilDetalle
            {
                IdConcil    = rdr.IsDBNull(rdr.GetOrdinal("ID_CONCIL"))  ? 0  : Convert.ToInt64(rdr["ID_CONCIL"]),
                Tipo        = rdr.GetString(rdr.GetOrdinal("TIPO")),
                Periodo     = rdr.GetInt32(rdr.GetOrdinal("PERIODO")),
                IdProp      = rdr.IsDBNull(rdr.GetOrdinal("ID_PROP"))    ? null : Convert.ToInt64(rdr["ID_PROP"]),
                IdLegacy    = rdr.IsDBNull(rdr.GetOrdinal("ID_LEGACY"))  ? null : Convert.ToInt64(rdr["ID_LEGACY"]),
                Tipdoc      = NullStr(rdr, "TIPDOC"),
                Serie       = NullStr(rdr, "SERIE"),
                Numero      = NullStr(rdr, "NUMERO"),
                FEmision    = NullDate(rdr, "F_EMISION"),
                Ruc         = NullStr(rdr, "RUC"),
                Nombre      = FixStr(rdr,  "NOMBRE"),
                Estado      = NullStr(rdr, "ESTADO") ?? "PENDIENTE",
                SunatBase   = NullDec(rdr, "SUNAT_BASE"),
                SunatIgv    = NullDec(rdr, "SUNAT_IGV"),
                SunatTotal  = NullDec(rdr, "SUNAT_TOTAL"),
                SunatMoneda = NullStr(rdr, "SUNAT_MONEDA"),
                SunatEst    = NullStr(rdr, "SUNAT_EST"),
                LegBase     = NullDec(rdr, "LEG_BASE"),
                LegIgv      = NullDec(rdr, "LEG_IGV"),
                LegTotal    = NullDec(rdr, "LEG_TOTAL"),
                LegMoneda   = NullStr(rdr, "LEG_MONEDA"),
                LegEst      = NullStr(rdr, "LEG_EST"),
                DiffTotalCp  = NullDec(rdr, "DIFF_TOTAL_CP"),
                DiffBase     = NullDec(rdr, "DIFF_BASE"),
                DiffIgv      = NullDec(rdr, "DIFF_IGV"),
                DiffFecha    = rdr.IsDBNull(rdr.GetOrdinal("DIFF_FECHA")) ? null : Convert.ToInt32(rdr["DIFF_FECHA"]),
                DiffCampos   = NullStr(rdr, "DIFF_CAMPOS"),
                SunatValAdqNg = NullDec(rdr, "SUNAT_VALADQNG"),
                LegValAdqNg   = NullDec(rdr, "LEG_VALADQNG"),
                SunatIsc      = NullDec(rdr, "SUNAT_ISC"),
                LegIsc        = NullDec(rdr, "LEG_ISC"),
                SunatOtros    = NullDec(rdr, "SUNAT_OTROS"),
                LegOtros      = NullDec(rdr, "LEG_OTROS"),
                SunatCambio   = rdr.IsDBNull(rdr.GetOrdinal("SUNAT_CAMBIO")) ? null : NullDec(rdr, "SUNAT_CAMBIO"),
                LegCambio     = rdr.IsDBNull(rdr.GetOrdinal("LEG_CAMBIO"))   ? null : NullDec(rdr, "LEG_CAMBIO"),
                Revisado     = NullStr(rdr, "REVISADO") ?? "N",
                ObsManual    = NullStr(rdr, "OBS_MANUAL"),
                TipDocref    = NullStr(rdr, "TIP_DOCREF"),
                SerDocref    = NullStr(rdr, "SER_DOCREF"),
                NroDocref    = NullStr(rdr, "NRO_DOCREF"),
                FDocref      = NullDate(rdr, "F_DOCREF"),
                TipoNota     = NullStr(rdr, "TIPO_NOTA"),
                ExclMotivo   = NullStr(rdr, "EXCL_MOTIVO"),
                ExclObs      = NullStr(rdr, "EXCL_OBS"),
                ExclUsuario  = NullStr(rdr, "EXCL_USUARIO"),
                ExclFch      = NullDate(rdr, "EXCL_FCH"),
                ValidezCp    = NullStr(rdr, "VALIDEZ_CP"),
                ValidezRuc   = NullStr(rdr, "VALIDEZ_RUC"),
                ValidezDom   = NullStr(rdr, "VALIDEZ_DOM"),
                FchValidez   = NullDate(rdr, "FCH_VALIDEZ"),
            });
        }
        return list;
    }

    public async Task InvalidarConciliacionAsync(string tipo, int periodo, CancellationToken ct = default)
    {
        var tipoDb = tipo.Equals("ventas", StringComparison.OrdinalIgnoreCase) ? "1" : "2";
        await using var conn = await OpenConnAsync(ct);

        // Los 3 DELETEs deben ser atómicos: si uno falla, ninguno debe quedar aplicado.
        // Con AutoCommit=true (fijado en OpenConnAsync) necesitamos una transacción explícita
        // que sobreescriba ese modo para este bloque.
        conn.AutoCommit = false;
        await using var tx = conn.BeginTransaction();
        try
        {
            // 1. Borrar cruce (Cruzado/Diferencias)
            await using var cmd1 = new OracleCommand(
                "DELETE FROM SIG.SIRE_CONCIL WHERE TIPO = :tipo AND PERIODO = :periodo", conn);
            cmd1.Transaction = tx;
            cmd1.Parameters.Add(new OracleParameter("tipo",    OracleDbType.Varchar2) { Value = tipoDb  });
            cmd1.Parameters.Add(new OracleParameter("periodo", OracleDbType.Int32)    { Value = periodo });
            await cmd1.ExecuteNonQueryAsync(ct);

            // 2. Borrar resumen de conciliación
            await using var cmd2 = new OracleCommand(
                "DELETE FROM SIG.SIRE_CONCIL_RESUMEN WHERE TIPO = :tipo AND PERIODO = :periodo", conn);
            cmd2.Transaction = tx;
            cmd2.Parameters.Add(new OracleParameter("tipo",    OracleDbType.Varchar2) { Value = tipoDb  });
            cmd2.Parameters.Add(new OracleParameter("periodo", OracleDbType.Int32)    { Value = periodo });
            await cmd2.ExecuteNonQueryAsync(ct);

            // 3. Borrar Legacy (datos ERP) — se regeneran al ejecutar Conciliar
            await using var cmd3 = new OracleCommand(
                "DELETE FROM SIG.SIRE_LEGACY WHERE TIPO = :tipo AND PERIODO = :periodo", conn);
            cmd3.Transaction = tx;
            cmd3.Parameters.Add(new OracleParameter("tipo",    OracleDbType.Varchar2) { Value = tipoDb  });
            cmd3.Parameters.Add(new OracleParameter("periodo", OracleDbType.Int32)    { Value = periodo });
            await cmd3.ExecuteNonQueryAsync(ct);

            tx.Commit();
        }
        catch
        {
            try { tx.Rollback(); } catch { /* no ocultar la excepción original */ }
            throw;
        }
        finally
        {
            conn.AutoCommit = true;
        }
    }

    // =========================================================================
    // Exclusiones — SIRE_EXCLUIDOS_LOGIX
    // =========================================================================

    public async Task<List<SireExcluidoLogix>> GetExcluidosAsync(
        string tipo, string periodo, CancellationToken ct = default)
    {
        if (!int.TryParse(periodo, out var periodoNr)) return [];
        var tipoDb = tipo.Equals("ventas", StringComparison.OrdinalIgnoreCase) ? "1" : "2";

        const string sql = @"
            SELECT ID_EXCLUIDO, TIPO, PERIODO, MOTIVO,
                   ID_PROP, ID_CONCIL, TIPDOC, SERIE, NUMERO, F_EMISION,
                   RUC, NOMBRE, TOTAL_CP, MONEDA,
                   TIP_DOCREF, SER_DOCREF, NRO_DOCREF,
                   ID_EXCLUIDO_REL,
                   USUARIO, FCH_EXCLUSION, OBS, ESTADO
            FROM   SIG.SIRE_EXCLUIDOS_LOGIX
            WHERE  TIPO = :tipo AND PERIODO = :periodo AND ESTADO = 'A'
            ORDER  BY FCH_EXCLUSION DESC, ID_EXCLUIDO";

        await using var conn = await OpenConnAsync(ct);
        using var cmd = new OracleCommand(sql, conn);
        cmd.Parameters.Add(new OracleParameter("tipo",    OracleDbType.Char)  { Value = tipoDb   });
        cmd.Parameters.Add(new OracleParameter("periodo", OracleDbType.Int32) { Value = periodoNr });

        var list = new List<SireExcluidoLogix>();
        await using var rdr = await cmd.ExecuteReaderAsync(ct);
        while (await rdr.ReadAsync(ct))
        {
            list.Add(new SireExcluidoLogix
            {
                IdExcluido    = Convert.ToInt64(rdr["ID_EXCLUIDO"]),
                Tipo          = rdr.GetString(rdr.GetOrdinal("TIPO")),
                Periodo       = rdr.GetInt32(rdr.GetOrdinal("PERIODO")),
                Motivo        = rdr.GetString(rdr.GetOrdinal("MOTIVO")),
                IdProp        = rdr.IsDBNull(rdr.GetOrdinal("ID_PROP"))         ? null : Convert.ToInt64(rdr["ID_PROP"]),
                IdConcil      = rdr.IsDBNull(rdr.GetOrdinal("ID_CONCIL"))       ? null : Convert.ToInt64(rdr["ID_CONCIL"]),
                Tipdoc        = NullStr(rdr, "TIPDOC"),
                Serie         = NullStr(rdr, "SERIE"),
                Numero        = NullStr(rdr, "NUMERO"),
                FEmision      = NullDate(rdr, "F_EMISION"),
                Ruc           = NullStr(rdr, "RUC"),
                Nombre        = NullStr(rdr, "NOMBRE"),
                TotalCp       = NullDec(rdr, "TOTAL_CP"),
                Moneda        = NullStr(rdr, "MONEDA"),
                TipDocref     = NullStr(rdr, "TIP_DOCREF"),
                SerDocref     = NullStr(rdr, "SER_DOCREF"),
                NroDocref     = NullStr(rdr, "NRO_DOCREF"),
                IdExcluidoRel = rdr.IsDBNull(rdr.GetOrdinal("ID_EXCLUIDO_REL")) ? null : Convert.ToInt64(rdr["ID_EXCLUIDO_REL"]),
                Usuario       = NullStr(rdr, "USUARIO"),
                FchExclusion  = rdr.GetDateTime(rdr.GetOrdinal("FCH_EXCLUSION")),
                Obs           = NullStr(rdr, "OBS"),
                Estado        = rdr.GetString(rdr.GetOrdinal("ESTADO")),
            });
        }
        return list;
    }

    public async Task<int> ExcluirManualAsync(
        string tipo, int periodo, IEnumerable<long> idsConcil,
        string usuario, string? obs, CancellationToken ct = default)
    {
        var tipoDb = tipo.Equals("ventas", StringComparison.OrdinalIgnoreCase) ? "1" : "2";
        var ids    = idsConcil.ToList();
        if (ids.Count == 0) return 0;

        int excluidos = 0;
        await using var conn = await OpenConnAsync(ct);

        // Todos los INSERT + UPDATE deben ser atómicos: si uno falla no deben quedar
        // registros EXCLUIDO en SIRE_CONCIL sin su entrada en SIRE_EXCLUIDOS_LOGIX.
        conn.AutoCommit = false;
        await using var tx = conn.BeginTransaction();
        try
        {

        const string sqlLeer = @"
            SELECT C.ID_CONCIL, C.ID_PROP, C.TIPDOC, C.SERIE, C.NUMERO,
                   C.F_EMISION, C.RUC, C.NOMBRE,
                   NVL(C.SUNAT_TOTAL,0) AS TOTAL_CP, C.SUNAT_MONEDA,
                   P.TIP_DOCREF, P.SER_DOCREF, P.NRO_DOCREF
            FROM   SIG.SIRE_CONCIL C
            JOIN   SIG.SIRE_PROPUESTA P ON P.ID_PROP = C.ID_PROP
            WHERE  C.ID_CONCIL = :id AND C.TIPO = :tipo
              AND  C.PERIODO = :periodo AND C.ESTADO = 'SOLO_SUNAT'";

        foreach (var idConcil in ids)
        {
            using var cmdLeer = new OracleCommand(sqlLeer, conn);
            cmdLeer.Transaction = tx;
            cmdLeer.Parameters.Add(new OracleParameter("id",      OracleDbType.Int64)   { Value = idConcil });
            cmdLeer.Parameters.Add(new OracleParameter("tipo",    OracleDbType.Char)    { Value = tipoDb   });
            cmdLeer.Parameters.Add(new OracleParameter("periodo", OracleDbType.Int32)   { Value = periodo  });

            await using var rdr = await cmdLeer.ExecuteReaderAsync(ct);
            if (!await rdr.ReadAsync(ct)) continue;

            var idProp   = Convert.ToInt64(rdr["ID_PROP"]);
            var tipdoc   = NullStr(rdr, "TIPDOC");
            var serie    = NullStr(rdr, "SERIE");
            var numero   = NullStr(rdr, "NUMERO");
            var fem      = NullDate(rdr, "F_EMISION");
            var ruc      = NullStr(rdr, "RUC");
            var nombre   = NullStr(rdr, "NOMBRE");
            var total    = NullDec(rdr, "TOTAL_CP");
            var moneda   = NullStr(rdr, "SUNAT_MONEDA");
            var tipRef   = NullStr(rdr, "TIP_DOCREF");
            var serRef   = NullStr(rdr, "SER_DOCREF");
            var nroRef   = NullStr(rdr, "NRO_DOCREF");
            await rdr.DisposeAsync();

            // UPSERT: si ya existe una exclusión MANUAL activa para este ID_CONCIL,
            // actualizarla en lugar de insertar un duplicado.
            long? existingId = null;
            using (var cmdChk = new OracleCommand(
                "SELECT ID_EXCLUIDO FROM SIG.SIRE_EXCLUIDOS_LOGIX WHERE ID_CONCIL = :idConcil AND MOTIVO = 'MANUAL' AND ESTADO = 'A' AND ROWNUM = 1",
                conn))
            {
                cmdChk.Transaction = tx;
                cmdChk.Parameters.Add(new OracleParameter("idConcil", OracleDbType.Int64) { Value = idConcil });
                var scalar = await cmdChk.ExecuteScalarAsync(ct);
                if (scalar != null && scalar != DBNull.Value)
                    existingId = Convert.ToInt64(scalar);
            }

            if (existingId.HasValue)
            {
                // Ya existe — actualizar los campos dinámicos sin generar duplicado
                using var cmdUpdExcl = new OracleCommand(
                    @"UPDATE SIG.SIRE_EXCLUIDOS_LOGIX
                         SET ID_PROP = :idProp, FCH_EXCLUSION = SYSDATE,
                             USUARIO = :usuario, OBS = :obs
                       WHERE ID_EXCLUIDO = :id",
                    conn);
                cmdUpdExcl.Transaction = tx;
                cmdUpdExcl.Parameters.Add(new OracleParameter("idProp",  OracleDbType.Int64)   { Value = idProp   });
                cmdUpdExcl.Parameters.Add(new OracleParameter("usuario", OracleDbType.Varchar2) { Value = (object?)usuario ?? DBNull.Value });
                cmdUpdExcl.Parameters.Add(new OracleParameter("obs",     OracleDbType.Varchar2) { Value = (object?)(obs ?? "Excluido manualmente") ?? DBNull.Value });
                cmdUpdExcl.Parameters.Add(new OracleParameter("id",      OracleDbType.Int64)   { Value = existingId.Value });
                await cmdUpdExcl.ExecuteNonQueryAsync(ct);
            }
            else
            {
            // Obtener siguiente ID de secuencia solo cuando se va a insertar
            long idExcluido;
            using (var cmdSeq = new OracleCommand("SELECT SIG.SEQ_SIRE_EXCL.NEXTVAL FROM DUAL", conn))
                idExcluido = Convert.ToInt64(await cmdSeq.ExecuteScalarAsync(ct));

            const string sqlIns = @"
                INSERT INTO SIG.SIRE_EXCLUIDOS_LOGIX (
                    ID_EXCLUIDO, TIPO, PERIODO, MOTIVO,
                    ID_PROP, ID_CONCIL, TIPDOC, SERIE, NUMERO, F_EMISION,
                    RUC, NOMBRE, TOTAL_CP, MONEDA,
                    TIP_DOCREF, SER_DOCREF, NRO_DOCREF,
                    USUARIO, FCH_EXCLUSION, OBS, ESTADO
                ) VALUES (
                    :id, :tipo, :periodo, 'MANUAL',
                    :idProp, :idConcil, :tipdoc, :serie, :numero, :fem,
                    :ruc, :nombre, :total, :moneda,
                    :tipRef, :serRef, :nroRef,
                    :usuario, SYSDATE, :obs, 'A'
                )";

            using var cmdIns = new OracleCommand(sqlIns, conn);
            cmdIns.Transaction = tx;
            cmdIns.Parameters.Add(new OracleParameter("id",       OracleDbType.Int64)   { Value = idExcluido });
            cmdIns.Parameters.Add(new OracleParameter("tipo",     OracleDbType.Char)    { Value = tipoDb     });
            cmdIns.Parameters.Add(new OracleParameter("periodo",  OracleDbType.Int32)   { Value = periodo    });
            cmdIns.Parameters.Add(new OracleParameter("idProp",   OracleDbType.Int64)   { Value = idProp     });
            cmdIns.Parameters.Add(new OracleParameter("idConcil", OracleDbType.Int64)   { Value = idConcil   });
            cmdIns.Parameters.Add(new OracleParameter("tipdoc",   OracleDbType.Varchar2){ Value = (object?)tipdoc   ?? DBNull.Value });
            cmdIns.Parameters.Add(new OracleParameter("serie",    OracleDbType.Varchar2){ Value = (object?)serie    ?? DBNull.Value });
            cmdIns.Parameters.Add(new OracleParameter("numero",   OracleDbType.Varchar2){ Value = (object?)numero   ?? DBNull.Value });
            cmdIns.Parameters.Add(new OracleParameter("fem",      OracleDbType.Date)    { Value = (object?)fem      ?? DBNull.Value });
            cmdIns.Parameters.Add(new OracleParameter("ruc",      OracleDbType.Varchar2){ Value = (object?)ruc      ?? DBNull.Value });
            cmdIns.Parameters.Add(new OracleParameter("nombre",   OracleDbType.Varchar2){ Value = (object?)nombre   ?? DBNull.Value });
            cmdIns.Parameters.Add(new OracleParameter("total",    OracleDbType.Decimal) { Value = total      });
            cmdIns.Parameters.Add(new OracleParameter("moneda",   OracleDbType.Varchar2){ Value = (object?)moneda   ?? DBNull.Value });
            cmdIns.Parameters.Add(new OracleParameter("tipRef",   OracleDbType.Varchar2){ Value = (object?)tipRef   ?? DBNull.Value });
            cmdIns.Parameters.Add(new OracleParameter("serRef",   OracleDbType.Varchar2){ Value = (object?)serRef   ?? DBNull.Value });
            cmdIns.Parameters.Add(new OracleParameter("nroRef",   OracleDbType.Varchar2){ Value = (object?)nroRef   ?? DBNull.Value });
            cmdIns.Parameters.Add(new OracleParameter("usuario",  OracleDbType.Varchar2){ Value = (object?)usuario  ?? DBNull.Value });
            cmdIns.Parameters.Add(new OracleParameter("obs",      OracleDbType.Varchar2){ Value = (object?)(obs ?? "Excluido manualmente") ?? DBNull.Value });
            await cmdIns.ExecuteNonQueryAsync(ct);
            } // end else (INSERT nuevo)

            using var cmdUpd = new OracleCommand(
                "UPDATE SIG.SIRE_CONCIL SET ESTADO='EXCLUIDO', DIFF_CAMPOS='MANUAL' WHERE ID_CONCIL=:id", conn);
            cmdUpd.Transaction = tx;
            cmdUpd.Parameters.Add(new OracleParameter("id", OracleDbType.Int64) { Value = idConcil });
            await cmdUpd.ExecuteNonQueryAsync(ct);

            excluidos++;
        }

        if (excluidos > 0)
        {
            // ODP.NET: cuando la misma variable bind aparece más de una vez en la SQL,
            // se deben registrar parámetros independientes (uno por ocurrencia) para evitar
            // binding incorrecto. Se usan alias tipo2/periodo2 para la cláusula WHERE externa.
            using var cmdRes = new OracleCommand(
                @"UPDATE SIG.SIRE_CONCIL_RESUMEN
                  SET    TOTAL_EXCL = (SELECT COUNT(*) FROM SIG.SIRE_CONCIL
                                       WHERE TIPO=:tipo AND PERIODO=:periodo AND ESTADO='EXCLUIDO')
                  WHERE  TIPO=:tipo2 AND PERIODO=:periodo2", conn);
            cmdRes.Transaction = tx;
            cmdRes.Parameters.Add(new OracleParameter("tipo",    OracleDbType.Char)  { Value = tipoDb  });
            cmdRes.Parameters.Add(new OracleParameter("periodo", OracleDbType.Int32) { Value = periodo });
            cmdRes.Parameters.Add(new OracleParameter("tipo2",   OracleDbType.Char)  { Value = tipoDb  });
            cmdRes.Parameters.Add(new OracleParameter("periodo2",OracleDbType.Int32) { Value = periodo });
            await cmdRes.ExecuteNonQueryAsync(ct);
        }

        tx.Commit();
        return excluidos;
        }
        catch
        {
            try { tx.Rollback(); } catch { /* no ocultar la excepción original */ }
            throw;
        }
        finally
        {
            conn.AutoCommit = true;
        }
    }

    public async Task RestaurarExcluidoAsync(
        long idConcil, string usuario, CancellationToken ct = default)
    {
        await using var conn = await OpenConnAsync(ct);

        // Buscar el excluido activo por ID_CONCIL (fuera de TX — solo lectura)
        long  idExcluido = 0;
        long?  idRel    = null;
        long?  idConcilValue = idConcil;
        string tipoDb   = "";
        int    periodo  = 0;

        using (var cmd = new OracleCommand(
            "SELECT ID_EXCLUIDO, ID_EXCLUIDO_REL, TIPO, PERIODO FROM SIG.SIRE_EXCLUIDOS_LOGIX WHERE ID_CONCIL=:id AND ESTADO='A' AND ROWNUM=1",
            conn))
        {
            cmd.Parameters.Add(new OracleParameter("id", OracleDbType.Int64) { Value = idConcil });
            await using var rdr = await cmd.ExecuteReaderAsync(ct);
            if (!await rdr.ReadAsync(ct)) return;  // ya restaurado o no existe
            idExcluido = Convert.ToInt64(rdr[0]);
            idRel      = rdr.IsDBNull(1) ? null : Convert.ToInt64(rdr[1]);
            tipoDb     = rdr.GetString(2);
            periodo    = rdr.GetInt32(3);
        }

        // Envolver todos los UPDATE en una TX atómica: si el resumen o el par vinculado
        // falla, se revierte todo y el estado de Oracle queda consistente.
        conn.AutoCommit = false;
        await using var tx = conn.BeginTransaction();
        try
        {
            // Restaurar el registro principal
            await RestaurarUnExcluidoAsync(conn, tx, idExcluido, idConcilValue, usuario, ct);

            // Restaurar el par vinculado (si existe)
            if (idRel.HasValue)
            {
                long?  idConcilRel = null;
                using (var cmd2 = new OracleCommand(
                    "SELECT ID_CONCIL FROM SIG.SIRE_EXCLUIDOS_LOGIX WHERE ID_EXCLUIDO=:id AND ESTADO='A'", conn))
                {
                    cmd2.Parameters.Add(new OracleParameter("id", OracleDbType.Int64) { Value = idRel.Value });
                    var res = await cmd2.ExecuteScalarAsync(ct);
                    if (res is not null and not DBNull) idConcilRel = Convert.ToInt64(res);
                }
                await RestaurarUnExcluidoAsync(conn, tx, idRel.Value, idConcilRel, usuario, ct);
            }

            // Actualizar resumen
            // ODP.NET positional binding: cada ocurrencia del mismo nombre bind requiere un
            // parámetro independiente. Se usan aliases tipo2/periodo2 para el WHERE externo
            // para evitar que Oracle mapee incorrectamente y el UPDATE no actualice la fila.
            using var cmdRes = new OracleCommand(
                @"UPDATE SIG.SIRE_CONCIL_RESUMEN
                  SET    TOTAL_EXCL = (SELECT COUNT(*) FROM SIG.SIRE_CONCIL
                                       WHERE TIPO=:tipo AND PERIODO=:periodo AND ESTADO='EXCLUIDO')
                  WHERE  TIPO=:tipo2 AND PERIODO=:periodo2", conn);
            cmdRes.Transaction = tx;
            cmdRes.Parameters.Add(new OracleParameter("tipo",    OracleDbType.Char)  { Value = tipoDb  });
            cmdRes.Parameters.Add(new OracleParameter("periodo", OracleDbType.Int32) { Value = periodo });
            cmdRes.Parameters.Add(new OracleParameter("tipo2",   OracleDbType.Char)  { Value = tipoDb  });
            cmdRes.Parameters.Add(new OracleParameter("periodo2",OracleDbType.Int32) { Value = periodo });
            await cmdRes.ExecuteNonQueryAsync(ct);

            tx.Commit();
        }
        catch
        {
            try { tx.Rollback(); } catch { /* no ocultar la excepción original */ }
            throw;
        }
        finally
        {
            conn.AutoCommit = true;
        }
    }

    private static async Task RestaurarUnExcluidoAsync(
        OracleConnection conn, OracleTransaction tx, long idExcluido, long? idConcil, string usuario, CancellationToken ct)
    {
        using var cmd1 = new OracleCommand(
            "UPDATE SIG.SIRE_EXCLUIDOS_LOGIX SET ESTADO='R' WHERE ID_EXCLUIDO=:id", conn);
        cmd1.Transaction = tx;
        cmd1.Parameters.Add(new OracleParameter("id", OracleDbType.Int64) { Value = idExcluido });
        await cmd1.ExecuteNonQueryAsync(ct);

        if (idConcil.HasValue)
        {
            using var cmd2 = new OracleCommand(
                "UPDATE SIG.SIRE_CONCIL SET ESTADO='SOLO_SUNAT', DIFF_CAMPOS=NULL WHERE ID_CONCIL=:id", conn);
            cmd2.Transaction = tx;
            cmd2.Parameters.Add(new OracleParameter("id", OracleDbType.Int64) { Value = idConcil.Value });
            await cmd2.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task AutoExcluirNcAsync(
        string tipo, int periodo, string usuario, CancellationToken ct = default)
    {
        var tipoDb = tipo.Equals("ventas", StringComparison.OrdinalIgnoreCase) ? "1" : "2";
        await using var conn = await OpenConnAsync(ct);
        using var cmd = new OracleCommand(
            "BEGIN SIG.SP_SIRE_AUTO_EXCLUIR_NC(:tipo, :periodo, :usuario); END;", conn);
        cmd.Parameters.Add(new OracleParameter("tipo",    OracleDbType.Char)    { Value = tipoDb  });
        cmd.Parameters.Add(new OracleParameter("periodo", OracleDbType.Int32)   { Value = periodo });
        cmd.Parameters.Add(new OracleParameter("usuario", OracleDbType.Varchar2){ Value = usuario });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Validez de comprobante (API Consulta Integrada SUNAT)
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<List<SireConcilDetalle>> GetConcilPendientesValidezAsync(
        string tipo, string periodo, CancellationToken ct = default)
    {
        if (!int.TryParse(periodo, out var periodoNr)) return [];
        var tipoDb = tipo.Equals("ventas", StringComparison.OrdinalIgnoreCase) ? "1" : "2";

        // Incluye filas sin validar (VALIDEZ_CP IS NULL) y las que ya tienen '0' (NO EXISTE)
        // para permitir re-validación cuando el resultado previo pudo ser incorrecto.
        // Se trae SUNAT_MONEDA y CAMBIO de SIRE_PROPUESTA para convertir el monto a PEN
        // cuando el comprobante está en moneda extranjera (SUNAT exige el monto en soles).
        const string sql = @"
            SELECT C.ID_CONCIL, C.TIPO, C.PERIODO,
                   C.TIPDOC, C.SERIE, C.NUMERO, C.F_EMISION, C.RUC, C.NOMBRE,
                   C.ESTADO, NVL(C.SUNAT_TOTAL,0) SUNAT_TOTAL,
                   NVL(C.SUNAT_MONEDA,'PEN') SUNAT_MONEDA,
                   NVL(P.CAMBIO,1)           CAMBIO
            FROM   SIG.SIRE_CONCIL C
            LEFT JOIN SIG.SIRE_PROPUESTA P ON P.ID_PROP = C.ID_PROP
            WHERE  C.TIPO    = :tipo
              AND  C.PERIODO = :periodo
              AND  C.ESTADO NOT IN ('EXCLUIDO','SOLO_LEGACY')
              AND  (C.VALIDEZ_CP IS NULL OR C.VALIDEZ_CP = '0')
            ORDER BY C.TIPDOC, C.SERIE, C.NUMERO";

        await using var conn = await OpenConnAsync(ct);
        using var cmd = new OracleCommand(sql, conn);
        cmd.Parameters.Add(new OracleParameter("tipo",    OracleDbType.Varchar2) { Value = tipoDb });
        cmd.Parameters.Add(new OracleParameter("periodo", OracleDbType.Int32)    { Value = periodoNr });

        var list = new List<SireConcilDetalle>();
        await using var rdr = await cmd.ExecuteReaderAsync(ct);
        while (await rdr.ReadAsync(ct))
        {
            list.Add(new SireConcilDetalle
            {
                IdConcil     = Convert.ToInt64(rdr["ID_CONCIL"]),
                Tipo         = rdr.GetString(rdr.GetOrdinal("TIPO")),
                Periodo      = rdr.GetInt32(rdr.GetOrdinal("PERIODO")),
                Tipdoc       = NullStr(rdr, "TIPDOC"),
                Serie        = NullStr(rdr, "SERIE"),
                Numero       = NullStr(rdr, "NUMERO"),
                FEmision     = NullDate(rdr, "F_EMISION"),
                Ruc          = NullStr(rdr, "RUC"),
                Nombre       = FixStr(rdr,  "NOMBRE"),
                Estado       = NullStr(rdr, "ESTADO") ?? "",
                SunatTotal   = NullDec(rdr, "SUNAT_TOTAL"),
                SunatMoneda  = NullStr(rdr, "SUNAT_MONEDA") ?? "PEN",
                CambioMoneda = NullDec(rdr, "CAMBIO") is decimal c && c > 0 ? c : 1m,
            });
        }
        return list;
    }

    public async Task<List<SireConcilDetalle>> GetConcilTodosParaValidarAsync(
        string tipo, string periodo, CancellationToken ct = default)
    {
        if (!int.TryParse(periodo, out var periodoNr)) return [];
        var tipoDb = tipo.Equals("ventas", StringComparison.OrdinalIgnoreCase) ? "1" : "2";

        // Igual que GetConcilPendientesValidezAsync pero SIN filtro VALIDEZ_CP
        // y SIN excluir SOLO_LEGACY (también se valida).
        const string sql = @"
            SELECT C.ID_CONCIL, C.TIPO, C.PERIODO,
                   C.TIPDOC, C.SERIE, C.NUMERO, C.F_EMISION, C.RUC, C.NOMBRE,
                   C.ESTADO, NVL(C.SUNAT_TOTAL,0) SUNAT_TOTAL,
                   NVL(C.SUNAT_MONEDA,'PEN') SUNAT_MONEDA,
                   NVL(P.CAMBIO,1)           CAMBIO
            FROM   SIG.SIRE_CONCIL C
            LEFT JOIN SIG.SIRE_PROPUESTA P ON P.ID_PROP = C.ID_PROP
            WHERE  C.TIPO    = :tipo
              AND  C.PERIODO = :periodo
              AND  C.ESTADO != 'EXCLUIDO'
            ORDER BY C.TIPDOC, C.SERIE, C.NUMERO";

        await using var conn = await OpenConnAsync(ct);
        using var cmd = new OracleCommand(sql, conn);
        cmd.Parameters.Add(new OracleParameter("tipo",    OracleDbType.Varchar2) { Value = tipoDb });
        cmd.Parameters.Add(new OracleParameter("periodo", OracleDbType.Int32)    { Value = periodoNr });

        var list = new List<SireConcilDetalle>();
        await using var rdr = await cmd.ExecuteReaderAsync(ct);
        while (await rdr.ReadAsync(ct))
        {
            list.Add(new SireConcilDetalle
            {
                IdConcil     = Convert.ToInt64(rdr["ID_CONCIL"]),
                Tipo         = rdr.GetString(rdr.GetOrdinal("TIPO")),
                Periodo      = rdr.GetInt32(rdr.GetOrdinal("PERIODO")),
                Tipdoc       = NullStr(rdr, "TIPDOC"),
                Serie        = NullStr(rdr, "SERIE"),
                Numero       = NullStr(rdr, "NUMERO"),
                FEmision     = NullDate(rdr, "F_EMISION"),
                Ruc          = NullStr(rdr, "RUC"),
                Nombre       = FixStr(rdr,  "NOMBRE"),
                Estado       = NullStr(rdr, "ESTADO") ?? "",
                SunatTotal   = NullDec(rdr, "SUNAT_TOTAL"),
                SunatMoneda  = NullStr(rdr, "SUNAT_MONEDA") ?? "PEN",
                CambioMoneda = NullDec(rdr, "CAMBIO") is decimal c && c > 0 ? c : 1m,
            });
        }
        return list;
    }

    public async Task<SireConcilDetalle?> GetConcilFilaParaValidezAsync(
        long idConcil, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT C.ID_CONCIL, C.TIPO, C.PERIODO,
                   C.TIPDOC, C.SERIE, C.NUMERO, C.F_EMISION, C.RUC, C.NOMBRE,
                   C.ESTADO, NVL(C.SUNAT_TOTAL,0) SUNAT_TOTAL,
                   NVL(C.SUNAT_MONEDA,'PEN') SUNAT_MONEDA,
                   NVL(P.CAMBIO,1)           CAMBIO
            FROM   SIG.SIRE_CONCIL C
            LEFT JOIN SIG.SIRE_PROPUESTA P ON P.ID_PROP = C.ID_PROP
            WHERE  C.ID_CONCIL = :id";

        await using var conn = await OpenConnAsync(ct);
        using var cmd = new OracleCommand(sql, conn);
        cmd.Parameters.Add(new OracleParameter("id", OracleDbType.Int64) { Value = idConcil });

        await using var rdr = await cmd.ExecuteReaderAsync(ct);
        if (!await rdr.ReadAsync(ct)) return null;

        return new SireConcilDetalle
        {
            IdConcil     = Convert.ToInt64(rdr["ID_CONCIL"]),
            Tipo         = rdr.GetString(rdr.GetOrdinal("TIPO")),
            Periodo      = rdr.GetInt32(rdr.GetOrdinal("PERIODO")),
            Tipdoc       = NullStr(rdr, "TIPDOC"),
            Serie        = NullStr(rdr, "SERIE"),
            Numero       = NullStr(rdr, "NUMERO"),
            FEmision     = NullDate(rdr, "F_EMISION"),
            Ruc          = NullStr(rdr, "RUC"),
            Nombre       = FixStr(rdr,  "NOMBRE"),
            Estado       = NullStr(rdr, "ESTADO") ?? "",
            SunatTotal   = NullDec(rdr, "SUNAT_TOTAL"),
            SunatMoneda  = NullStr(rdr, "SUNAT_MONEDA") ?? "PEN",
            CambioMoneda = NullDec(rdr, "CAMBIO") is decimal c && c > 0 ? c : 1m,
        };
    }

    public async Task GuardarValidezAsync(
        long idConcil, string estadoCp, string estadoRuc, string condDomiRuc,
        CancellationToken ct = default)
    {
        await using var conn = await OpenConnAsync(ct);
        using var cmd = new OracleCommand(
            "UPDATE SIG.SIRE_CONCIL " +
            "SET VALIDEZ_CP=:cp, VALIDEZ_RUC=:ruc, VALIDEZ_DOM=:dom, FCH_VALIDEZ=SYSDATE " +
            "WHERE ID_CONCIL=:id", conn);
        cmd.Parameters.Add(new OracleParameter("cp",  OracleDbType.Varchar2, 2) { Value = estadoCp });
        cmd.Parameters.Add(new OracleParameter("ruc", OracleDbType.Varchar2, 2) { Value = estadoRuc });
        cmd.Parameters.Add(new OracleParameter("dom", OracleDbType.Varchar2, 2) { Value = condDomiRuc });
        cmd.Parameters.Add(new OracleParameter("id",  OracleDbType.Int64)       { Value = idConcil });
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

