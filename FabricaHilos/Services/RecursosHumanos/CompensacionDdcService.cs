using FabricaHilos.Models.RecursosHumanos;
using Oracle.ManagedDataAccess.Client;
using System.Collections.Concurrent;
using System.Data;

namespace FabricaHilos.Services.RecursosHumanos;

public interface ICompensacionDdcService
{
    Task<List<DdcRangoFilaDto>> ListarDdcRangoAsync(
        string codEmpresa,
        string fechaInicio,
        string fechaFin,
        string? nombre = null,
        string? fechaHeInicio = null,
        string? fechaHeFin = null,
        bool soloDdc = true,
        string? empresaConexion = null);

    Task<List<DdcRangoFilaDto>> ListarHePersonalAsync(
        string codEmpresa,
        string codPersonal,
        string fechaHeInicio,
        string fechaHeFin,
        string? empresaConexion = null);

    Task<List<DdcCalculoFilaDto>> CalcularDdcAsync(
        string codEmpresa,
        string fechaInicio,
        string fechaFin,
        string listaPersonal,
        string? fechaHeInicio = null,
        string? fechaHeFin = null,
        string? empresaConexion = null);

    Task<List<DdcRegistroFilaDto>> RegistrarDdcMasivoAsync(
        string codEmpresa,
        string fechaInicio,
        string fechaFin,
        string listaPersonal,
        string? listaDdcFechas = null,
        string? fechaHeInicio = null,
        string? fechaHeFin = null,
        string? empresaConexion = null);

    Task<List<DdcEventoFilaDto>> ConsultarEventoDdcAsync(long idEvento, string? empresaConexion = null);

    Task<List<DdcCompFilaDto>> ConsultarCompDdcAsync(long idCompen, string? empresaConexion = null);

    Task<List<DdcRangoConsultaDto>> ConsultarRangoDdcAsync(
        string? codEmpresa,
        string? codPersonal,
        string fechaInicio,
        string fechaFin,
        string? empresaConexion = null);

    Task CommitAsync();
    Task RollbackAsync();
}

public class CompensacionDdcService : ICompensacionDdcService
{
    // Paquete Oracle de DDC (Día Libre por Compensar) según la empresa activa en sesión.
    // ARBONA usa PKG_ARB_COMP_DDC (redondeo a cuarto de hora, floor puro, sin split HEA/HED,
    // sin integración LOGIX — ver ARBONA/COMPENSACIONES/PKG_ARB_Comp_DDC.sql) y vive en una
    // base de datos distinta (ArbonaConnection); el resto de empresas usa PKG_SCA_COMP_DDC
    // (La Colonial) sobre AquariusConnection. Mismo patrón que CompensacionDiaDiaService.
    private const string PaqueteColonial = "AQUARIUS.PKG_SCA_COMP_DDC";
    private const string PaqueteArbona   = "AQUARIUS.PKG_ARB_COMP_DDC";

    private readonly string _baseConnectionString;
    private readonly string _arbonaConnectionString;
    private readonly string _solsaConnectionString;
    private readonly ILogger<CompensacionDdcService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal record ActiveTxEntry(OracleConnection Conn, OracleTransaction Txn, DateTime CreatedAt, string CodEmpresa);
    internal static readonly ConcurrentDictionary<string, ActiveTxEntry> _activeTx = new();

    public CompensacionDdcService(
        IConfiguration configuration,
        ILogger<CompensacionDdcService> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _baseConnectionString = configuration.GetConnectionString("AquariusConnection")
            ?? throw new InvalidOperationException("Aquarius connection string not found.");
        _arbonaConnectionString = configuration.GetConnectionString("ArbonaConnection")
            ?? _baseConnectionString;
        _solsaConnectionString = configuration.GetConnectionString("SolsaConnection")
            ?? _baseConnectionString;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    // Empresa activa en sesión (mismo patrón que CompensacionDiaDiaService.GetEmpresaConexion).
    // Si se recibe "empresaConexion" (override explícito desde el combo de empresa del
    // módulo, habilitado cuando la sesión es ARBONA o SOLSA) y es una clave válida,
    // tiene prioridad sobre el valor de sesión.
    private string GetEmpresaConexion(string? empresaConexion = null)
    {
        if (EsEmpresaValida(empresaConexion))
            return empresaConexion!;
        return _httpContextAccessor.HttpContext?.Session.GetString("EmpresaConexion") ?? "LaColonialConnection";
    }

    private static bool EsEmpresaValida(string? empresaConexion) =>
        empresaConexion is "ArbonaConnection" or "SolsaConnection" or "LaColonialConnection";

    // ARBONA y SOLSA comparten el mismo paquete Oracle (PKG_ARB_COMP_DDC); cada
    // una vive en su propia base de datos (ver GetOracleConnectionString).
    private string GetPaquete(string? empresaConexion = null)
    {
        var empresa = GetEmpresaConexion(empresaConexion);
        return empresa is "ArbonaConnection" or "SolsaConnection" ? PaqueteArbona : PaqueteColonial;
    }

    // ARBONA y SOLSA viven cada una en su propia base de datos distinta de
    // AquariusConnection (usada por el resto de empresas, ej. La Colonial).
    private string GetOracleConnectionString(string? empresaConexion = null)
    {
        var empresa = GetEmpresaConexion(empresaConexion);
        return empresa switch
        {
            "ArbonaConnection" => _arbonaConnectionString,
            "SolsaConnection"  => _solsaConnectionString,
            _                    => _baseConnectionString
        };
    }

    private string GetSessionId()
    {
        var ctx = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("HttpContext no disponible.");
        return ctx.Session.Id;
    }

    private static string? GetStr(OracleDataReader r, string col)
    {
        try { return r[col] == DBNull.Value ? null : r[col]?.ToString(); }
        catch { return null; }
    }

    private static int GetInt(OracleDataReader r, string col)
    {
        try { return r[col] == DBNull.Value ? 0 : Convert.ToInt32(r[col]); }
        catch { return 0; }
    }

    private static long? GetNullLong(OracleDataReader r, string col)
    {
        try { return r[col] == DBNull.Value ? null : Convert.ToInt64(r[col]); }
        catch { return null; }
    }

    private static bool IsOra04068(OracleException ex) =>
        ex.Number == 4068 || ex.Number == 4061 || ex.Number == 4065 || ex.Number == 6508;

    private async Task<T> WithOracleRetryAsync<T>(Func<Task<T>> operation, string contexto)
    {
        try
        {
            return await operation();
        }
        catch (OracleException ex) when (IsOra04068(ex))
        {
            _logger.LogWarning("ORA-04068 en {Contexto}, reintentando...", contexto);
            return await operation();
        }
    }

    // ── LISTAR_DDC_RANGO ─────────────────────────────────────────────────────

    public async Task<List<DdcRangoFilaDto>> ListarDdcRangoAsync(
        string codEmpresa,
        string fechaInicio,
        string fechaFin,
        string? nombre = null,
        string? fechaHeInicio = null,
        string? fechaHeFin = null,
        bool soloDdc = true,
        string? empresaConexion = null)
    {
        return await WithOracleRetryAsync(async () =>
        {
            var result = new List<DdcRangoFilaDto>();
            await using var conn = new OracleConnection(GetOracleConnectionString(empresaConexion));
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandType    = CommandType.StoredProcedure;
            cmd.CommandTimeout = 120;
            cmd.CommandText    = $"{GetPaquete(empresaConexion)}.LISTAR_DDC_RANGO";

            cmd.Parameters.Add(new OracleParameter("p_cod_empresa",     OracleDbType.Varchar2) { Value = codEmpresa });
            cmd.Parameters.Add(new OracleParameter("p_fecha_inicio",    OracleDbType.Varchar2) { Value = fechaInicio });
            cmd.Parameters.Add(new OracleParameter("p_fecha_fin",       OracleDbType.Varchar2) { Value = fechaFin });
            cmd.Parameters.Add(new OracleParameter("p_nombre",          OracleDbType.Varchar2) { Value = (object?)nombre       ?? DBNull.Value });
            cmd.Parameters.Add(new OracleParameter("p_fecha_he_inicio", OracleDbType.Varchar2) { Value = (object?)fechaHeInicio ?? DBNull.Value });
            cmd.Parameters.Add(new OracleParameter("p_fecha_he_fin",    OracleDbType.Varchar2) { Value = (object?)fechaHeFin    ?? DBNull.Value });
            cmd.Parameters.Add(new OracleParameter("p_solo_ddc",        OracleDbType.Varchar2) { Value = soloDdc ? "S" : "N" });
            cmd.Parameters.Add(new OracleParameter("cv_resultado",      OracleDbType.RefCursor){ Direction = ParameterDirection.Output });

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var r = (OracleDataReader)reader;
                result.Add(new DdcRangoFilaDto
                {
                    CodPersonal    = GetStr(r, "cod_personal"),
                    NombreCompleto = GetStr(r, "nombre_completo"),
                    NumFotocheck   = GetStr(r, "num_fotocheck"),
                    FechamarStr    = GetStr(r, "fechamar_str"),
                    DiaSemana      = GetStr(r, "dia_semana"),
                    TipoDia        = GetStr(r, "tipo_dia"),
                    MinHe          = GetInt(r, "min_he"),
                    HorasHe        = GetStr(r, "horas_he"),
                    MinFalta       = GetInt(r, "min_falta"),
                    HorasFalta     = GetStr(r, "horas_falta"),
                    Alerta02       = GetStr(r, "alerta02"),
                    Alerta06       = GetStr(r, "alerta06"),
                    Descanso       = GetStr(r, "descanso"),
                    NumMarcaciones = GetInt(r, "nummarcaciones"),
                    YaCompensado    = GetStr(r, "ya_compensado"),
                    LogixCmotivo    = GetStr(r, "logix_cmotivo"),
                    LogixDinicio    = GetStr(r, "logix_dinicio"),
                    LogixDfinal     = GetStr(r, "logix_dfinal"),
                    LogixDescMotivo = GetStr(r, "logix_desc_motivo"),
                    DescAlerta06    = GetStr(r, "desc_alerta06"),
                });
            }
            return result;
        }, "LISTAR_DDC_RANGO");
    }

    // ── LISTAR_HE_PERSONAL ────────────────────────────────────────────────────

    public async Task<List<DdcRangoFilaDto>> ListarHePersonalAsync(
        string codEmpresa,
        string codPersonal,
        string fechaHeInicio,
        string fechaHeFin,
        string? empresaConexion = null)
    {
        return await WithOracleRetryAsync(async () =>
        {
            var result = new List<DdcRangoFilaDto>();
            await using var conn = new OracleConnection(GetOracleConnectionString(empresaConexion));
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandType    = CommandType.StoredProcedure;
            cmd.CommandTimeout = 120;
            cmd.CommandText    = $"{GetPaquete(empresaConexion)}.LISTAR_HE_PERSONAL";

            cmd.Parameters.Add(new OracleParameter("p_cod_empresa",     OracleDbType.Varchar2) { Value = codEmpresa });
            cmd.Parameters.Add(new OracleParameter("p_cod_personal",    OracleDbType.Varchar2) { Value = codPersonal });
            cmd.Parameters.Add(new OracleParameter("p_fecha_he_inicio", OracleDbType.Varchar2) { Value = fechaHeInicio });
            cmd.Parameters.Add(new OracleParameter("p_fecha_he_fin",    OracleDbType.Varchar2) { Value = fechaHeFin });
            cmd.Parameters.Add(new OracleParameter("cv_resultado",      OracleDbType.RefCursor){ Direction = ParameterDirection.Output });

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var r = (OracleDataReader)reader;
                result.Add(new DdcRangoFilaDto
                {
                    CodPersonal     = GetStr(r, "cod_personal"),
                    NombreCompleto  = GetStr(r, "nombre_completo"),
                    NumFotocheck    = GetStr(r, "num_fotocheck"),
                    FechamarStr     = GetStr(r, "fechamar_str"),
                    DiaSemana       = GetStr(r, "dia_semana"),
                    TipoDia         = GetStr(r, "tipo_dia"),
                    MinHe           = GetInt(r, "min_he"),
                    HorasHe         = GetStr(r, "horas_he"),
                    MinFalta        = 0,
                    HorasFalta      = null,
                    Alerta02        = GetStr(r, "alerta02"),
                    Alerta06        = GetStr(r, "alerta06"),
                    Descanso        = GetStr(r, "descanso"),
                    NumMarcaciones  = GetInt(r, "nummarcaciones"),
                    YaCompensado    = GetStr(r, "ya_compensado"),
                    DescAlerta06    = GetStr(r, "desc_alerta06"),
                });
            }
            return result;
        }, "LISTAR_HE_PERSONAL");
    }

    // ── CALCULAR_DDC ─────────────────────────────────────────────────────────

    public async Task<List<DdcCalculoFilaDto>> CalcularDdcAsync(
        string codEmpresa,
        string fechaInicio,
        string fechaFin,
        string listaPersonal,
        string? fechaHeInicio = null,
        string? fechaHeFin = null,
        string? empresaConexion = null)
    {
        return await WithOracleRetryAsync(async () =>
        {
            var result = new List<DdcCalculoFilaDto>();
            await using var conn = new OracleConnection(GetOracleConnectionString(empresaConexion));
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandType    = CommandType.StoredProcedure;
            cmd.CommandTimeout = 120;
            cmd.CommandText    = $"{GetPaquete(empresaConexion)}.CALCULAR_DDC";

            cmd.Parameters.Add(new OracleParameter("p_cod_empresa",     OracleDbType.Varchar2) { Value = codEmpresa });
            cmd.Parameters.Add(new OracleParameter("p_fecha_inicio",    OracleDbType.Varchar2) { Value = fechaInicio });
            cmd.Parameters.Add(new OracleParameter("p_fecha_fin",       OracleDbType.Varchar2) { Value = fechaFin });
            cmd.Parameters.Add(new OracleParameter("p_lista_personal",  OracleDbType.Varchar2) { Value = listaPersonal });
            cmd.Parameters.Add(new OracleParameter("p_fecha_he_inicio", OracleDbType.Varchar2) { Value = (object?)fechaHeInicio ?? DBNull.Value });
            cmd.Parameters.Add(new OracleParameter("p_fecha_he_fin",    OracleDbType.Varchar2) { Value = (object?)fechaHeFin    ?? DBNull.Value });
            cmd.Parameters.Add(new OracleParameter("cv_resultado",      OracleDbType.RefCursor){ Direction = ParameterDirection.Output });

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var r = (OracleDataReader)reader;
                result.Add(new DdcCalculoFilaDto
                {
                    CodPersonal           = GetStr(r, "cod_personal"),
                    NombreCompleto        = GetStr(r, "nombre_completo"),
                    FechaDdcStr           = GetStr(r, "fecha_ddc_str"),
                    DiaSemana             = GetStr(r, "dia_semana"),
                    MinFalta              = GetInt(r, "min_falta_total"),
                    HorasFalta            = GetStr(r, "horas_falta_total"),
                    MinHeAsignadasSim     = GetInt(r, "min_he_asignadas"),
                    HorasHeAsignadasSim   = GetStr(r, "horas_he_asignadas"),
                    MinFaltaRestanteSim   = GetInt(r, "min_falta_restante"),
                    HorasFaltaRestanteSim = GetStr(r, "horas_falta_restante"),
                    TotalHeRangoSim       = GetInt(r, "total_he_rango_sim"),
                    HorasTotalHeRangoSim  = GetStr(r, "horas_total_he_rango_sim"),
                    Estado                = GetStr(r, "estado"),
                });
            }
            return result;
        }, "CALCULAR_DDC");
    }

    // ── REGISTRAR_DDC_MASIVO ─────────────────────────────────────────────────
    // Mantiene la conexión abierta con transacción hasta Commit / Rollback.

    public async Task<List<DdcRegistroFilaDto>> RegistrarDdcMasivoAsync(
        string codEmpresa,
        string fechaInicio,
        string fechaFin,
        string listaPersonal,
        string? listaDdcFechas = null,
        string? fechaHeInicio = null,
        string? fechaHeFin = null,
        string? empresaConexion = null)
    {
        var sessionId = GetSessionId();
        await DisposeTransactionAsync(sessionId);

        var txConn = new OracleConnection(GetOracleConnectionString(empresaConexion));
        try
        {
            await txConn.OpenAsync();
            var txn = txConn.BeginTransaction();
            _activeTx[sessionId] = new ActiveTxEntry(txConn, txn, DateTime.UtcNow, codEmpresa);
        }
        catch
        {
            await txConn.DisposeAsync();
            throw;
        }

        try
        {
            var result = new List<DdcRegistroFilaDto>();
            var entry = _activeTx[sessionId];

            await using var cmd = entry.Conn.CreateCommand();
            cmd.Transaction = entry.Txn;
            cmd.CommandTimeout = 120;
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"{GetPaquete(empresaConexion)}.REGISTRAR_DDC_MASIVO";

            cmd.Parameters.Add(new OracleParameter("p_cod_empresa",      OracleDbType.Varchar2) { Value = codEmpresa });
            cmd.Parameters.Add(new OracleParameter("p_fecha_inicio",     OracleDbType.Varchar2) { Value = fechaInicio });
            cmd.Parameters.Add(new OracleParameter("p_fecha_fin",        OracleDbType.Varchar2) { Value = fechaFin });
            cmd.Parameters.Add(new OracleParameter("p_lista_personal",   OracleDbType.Varchar2) { Value = listaPersonal });
            cmd.Parameters.Add(new OracleParameter("p_lista_ddc_fechas", OracleDbType.Varchar2) { Value = (object?)listaDdcFechas ?? DBNull.Value });
            cmd.Parameters.Add(new OracleParameter("p_fecha_he_inicio",  OracleDbType.Varchar2) { Value = (object?)fechaHeInicio  ?? DBNull.Value });
            cmd.Parameters.Add(new OracleParameter("p_fecha_he_fin",     OracleDbType.Varchar2) { Value = (object?)fechaHeFin     ?? DBNull.Value });
            cmd.Parameters.Add(new OracleParameter("cv_resultado",       OracleDbType.RefCursor){ Direction = ParameterDirection.Output });

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var r = (OracleDataReader)reader;
                result.Add(new DdcRegistroFilaDto
                {
                    IdEvento           = GetNullLong(r, "id_evento"),
                    CodPersonal        = GetStr(r, "cod_personal"),
                    NombreCompleto     = GetStr(r, "nombre_completo"),
                    FechaDdcStr        = GetStr(r, "fecha_ddc_str"),
                    DiaSemana          = GetStr(r, "dia_semana"),
                    MinFaltaTotal      = GetInt(r, "min_falta_total"),
                    HorasFaltaTotal    = GetStr(r, "horas_falta_total"),
                    MinHeAsignadas     = GetInt(r, "min_he_asignadas"),
                    HorasHeAsignadas   = GetStr(r, "horas_he_asignadas"),
                    MinFaltaRestante   = GetInt(r, "min_falta_restante"),
                    HorasFaltaRestante = GetStr(r, "horas_falta_restante"),
                    Estado             = GetStr(r, "estado"),
                    Motivo             = GetStr(r, "motivo"),
                });
            }
            return result;
        }
        catch
        {
            await DisposeTransactionAsync(sessionId);
            throw;
        }
    }

    // ── COMMIT ────────────────────────────────────────────────────────────────

    public async Task CommitAsync()
    {
        var sessionId = GetSessionId();
        if (!_activeTx.TryGetValue(sessionId, out var entry))
            throw new InvalidOperationException("No hay transacción activa para esta sesión. Es posible que el servidor se haya reiniciado.");
        try
        {
            await entry.Txn.CommitAsync();
            _logger.LogInformation("COMMIT DDC exitoso para sesión {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en COMMIT DDC para sesión {SessionId}", sessionId);
            throw;
        }
        finally
        {
            await DisposeTransactionAsync(sessionId);
        }
    }

    // ── ROLLBACK ──────────────────────────────────────────────────────────────

    public async Task RollbackAsync()
    {
        var sessionId = GetSessionId();
        if (!_activeTx.TryGetValue(sessionId, out _))
            return;
        try
        {
            if (_activeTx.TryGetValue(sessionId, out var entry))
                await entry.Txn.RollbackAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error en ROLLBACK DDC para sesión {SessionId}", sessionId);
        }
        finally
        {
            await DisposeTransactionAsync(sessionId);
        }
    }

    internal static async Task DisposeTransactionAsync(string sessionId)
    {
        if (_activeTx.TryRemove(sessionId, out var entry))
        {
            try { await entry.Txn.DisposeAsync(); } catch { /* ignorar */ }
            try { await entry.Conn.DisposeAsync(); } catch { /* ignorar */ }
        }
    }

    // ── CONSULTAR_EVENTO_DDC ─────────────────────────────────────────────────

    public async Task<List<DdcEventoFilaDto>> ConsultarEventoDdcAsync(long idEvento, string? empresaConexion = null)
    {
        return await WithOracleRetryAsync(async () =>
        {
            var result = new List<DdcEventoFilaDto>();
            await using var conn = new OracleConnection(GetOracleConnectionString(empresaConexion));
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"{GetPaquete(empresaConexion)}.CONSULTAR_EVENTO_DDC";

            cmd.Parameters.Add(new OracleParameter("p_id_evento",  OracleDbType.Decimal)   { Value = idEvento });
            cmd.Parameters.Add(new OracleParameter("cv_resultado", OracleDbType.RefCursor) { Direction = ParameterDirection.Output });

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var r = (OracleDataReader)reader;
                result.Add(new DdcEventoFilaDto
                {
                    IdCompen         = GetNullLong(r, "id_compen"),
                    CodEmpresa       = GetStr(r, "cod_empresa"),
                    CodPersonal      = GetStr(r, "cod_personal"),
                    NombreCompleto   = GetStr(r, "nombre_completo"),
                    FechaOrigenStr   = GetStr(r, "fechaorigen_str"),
                    FechaDestinoStr  = GetStr(r, "fechadestino_str"),
                    TipoCompensacion = GetStr(r, "tipocompensacion"),
                    TiempoMin        = GetInt(r, "tiempo_min"),
                    TiempoHhMi       = GetStr(r, "tiempo_hhmi"),
                    OriAlerta06      = GetStr(r, "ori_alerta06"),
                    OriHeActual      = GetStr(r, "ori_he_actual"),
                    DestAlerta02     = GetStr(r, "dest_alerta02"),
                    DestFaltaActual  = GetStr(r, "dest_falta_actual"),
                    DestHefecActual  = GetStr(r, "dest_hefec_actual"),
                });
            }
            return result;
        }, "CONSULTAR_EVENTO_DDC");
    }

    // ── CONSULTAR_COMP_DDC ─────────────────────────────────────────────────────

    public async Task<List<DdcCompFilaDto>> ConsultarCompDdcAsync(long idCompen, string? empresaConexion = null)
    {
        return await WithOracleRetryAsync(async () =>
        {
            var result = new List<DdcCompFilaDto>();
            await using var conn = new OracleConnection(GetOracleConnectionString(empresaConexion));
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"{GetPaquete(empresaConexion)}.CONSULTAR_COMP_DDC";

            cmd.Parameters.Add(new OracleParameter("p_id_compen",  OracleDbType.Decimal)  { Value = idCompen });
            cmd.Parameters.Add(new OracleParameter("cv_resultado", OracleDbType.RefCursor){ Direction = ParameterDirection.Output });

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var r = (OracleDataReader)reader;
                result.Add(new DdcCompFilaDto
                {
                    IdCompen        = GetNullLong(r, "id_compen"),
                    CodEmpresa      = GetStr(r, "cod_empresa"),
                    CodPersonal     = GetStr(r, "cod_personal"),
                    NombreCompleto  = GetStr(r, "nombre_completo"),
                    FechaOrigenStr  = GetStr(r, "fechaorigen_str"),
                    FechaDestinoStr = GetStr(r, "fechadestino_str"),
                    TipoCompensacion= GetStr(r, "tipocompensacion"),
                    TiempoMin       = GetInt(r, "tiempo_min"),
                    TiempoHhMi      = GetStr(r, "tiempo_hhmi"),
                    IdEvento        = GetNullLong(r, "id_evento"),
                    OriAlerta06     = GetStr(r, "ori_alerta06"),
                    OriHeActual     = GetStr(r, "ori_he_actual"),
                    DestAlerta02    = GetStr(r, "dest_alerta02"),
                    DestFaltaActual = GetStr(r, "dest_falta_actual"),
                    DestHefecActual = GetStr(r, "dest_hefec_actual"),
                });
            }
            return result;
        }, "CONSULTAR_COMP_DDC");
    }

    // ── CONSULTAR_RANGO_DDC

    public async Task<List<DdcRangoConsultaDto>> ConsultarRangoDdcAsync(
        string? codEmpresa,
        string? codPersonal,
        string fechaInicio,
        string fechaFin,
        string? empresaConexion = null)
    {
        return await WithOracleRetryAsync(async () =>
        {
            var result = new List<DdcRangoConsultaDto>();
            await using var conn = new OracleConnection(GetOracleConnectionString(empresaConexion));
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"{GetPaquete(empresaConexion)}.CONSULTAR_RANGO_DDC";

            cmd.Parameters.Add(new OracleParameter("p_cod_empresa",  OracleDbType.Varchar2) { Value = (object?)codEmpresa  ?? DBNull.Value });
            cmd.Parameters.Add(new OracleParameter("p_cod_personal", OracleDbType.Varchar2) { Value = (object?)codPersonal ?? DBNull.Value });
            cmd.Parameters.Add(new OracleParameter("p_fecha_inicio", OracleDbType.Varchar2) { Value = fechaInicio });
            cmd.Parameters.Add(new OracleParameter("p_fecha_fin",    OracleDbType.Varchar2) { Value = fechaFin });
            cmd.Parameters.Add(new OracleParameter("cv_resultado",   OracleDbType.RefCursor){ Direction = ParameterDirection.Output });

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var r = (OracleDataReader)reader;
                var destAlerta02 = GetStr(r, "dest_alerta02");
                result.Add(new DdcRangoConsultaDto
                {
                    IdCompen         = GetNullLong(r, "id_compen"),
                    CodEmpresa       = GetStr(r, "cod_empresa"),
                    CodPersonal      = GetStr(r, "cod_personal"),
                    NumFotocheck     = GetStr(r, "num_fotocheck"),
                    NombreCompleto   = GetStr(r, "nombre_completo"),
                    FechaOrigenStr   = GetStr(r, "fechaorigen_str"),
                    FechaDestinoStr  = GetStr(r, "fechadestino_str"),
                    TipoOrigen       = GetStr(r, "tipoorigen"),
                    TipoCompensacion = GetStr(r, "tipocompensacion"),
                    TiempoMin        = GetInt(r, "tiempo_min"),
                    TiempoHhMi       = GetStr(r, "tiempo_hhmi"),
                    Evento           = GetStr(r, "evento"),
                    OriAlerta06      = GetStr(r, "ori_alerta06"),
                    OriHeActual      = GetStr(r, "ori_he_actual"),
                    DestAlerta02     = destAlerta02,
                    DestFaltaActual  = GetStr(r, "dest_falta_actual"),
                    DestHefecActual  = GetStr(r, "dest_hefec_actual"),
                    EstadoAplicacion = destAlerta02 == "FC" ? "APLICADA" : "PENDIENTE",
                });
            }
            return result;
        }, "CONSULTAR_RANGO_DDC");
    }
}
