using FabricaHilos.Models.RecursosHumanos;
using Oracle.ManagedDataAccess.Client;
using System.Collections.Concurrent;
using System.Data;
using System.Threading.Channels;

namespace FabricaHilos.Services.RecursosHumanos;

// ── Estado de un job de depuración ────────────────────────────────────────────

public enum DepuracionEstado { Pendiente, EnProceso, Completado, Error }

public class DepuracionJob
{
    public string JobId        { get; init; } = Guid.NewGuid().ToString("N");
    public string CodEmpresa   { get; init; } = string.Empty;
    public string CodPersonal  { get; init; } = string.Empty;
    public DateTime FechaInicio { get; init; }
    public DateTime FechaFin    { get; init; }
    public string ConnectionString { get; init; } = string.Empty;

    public DepuracionEstado Estado  { get; set; } = DepuracionEstado.Pendiente;
    public DateTime CreadoEn        { get; set; } = DateTime.Now;
    public DateTime? IniciadoEn     { get; set; }
    public DateTime? FinalizadoEn   { get; set; }
    public DepuraRangoResultadoDto? Resultado { get; set; }
    public string? MensajeError     { get; set; }
}

// ── Interfaz pública ──────────────────────────────────────────────────────────

public interface IDepuracionJobService
{
    string Encolar(string codEmpresa, string codPersonal, DateTime fechaInicio, DateTime fechaFin, string connectionString);
    DepuracionJob? ObtenerEstado(string jobId);
}

// ── Implementación: Singleton + BackgroundService ─────────────────────────────

public class DepuracionJobService : BackgroundService, IDepuracionJobService
{
    private readonly Channel<DepuracionJob> _canal;
    private readonly ConcurrentDictionary<string, DepuracionJob> _jobs = new();
    private readonly ILogger<DepuracionJobService> _logger;
    private const string Paquete = "AQUARIUS.PKG_SCA_DEPURA_TAREO";

    public DepuracionJobService(ILogger<DepuracionJobService> logger)
    {
        _logger = logger;
        // Canal con capacidad limitada para evitar acumulación descontrolada
        _canal = Channel.CreateBounded<DepuracionJob>(new BoundedChannelOptions(50)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    // ── Encolar ───────────────────────────────────────────────────────────────

    public string Encolar(string codEmpresa, string codPersonal, DateTime fechaInicio, DateTime fechaFin, string connectionString)
    {
        var job = new DepuracionJob
        {
            CodEmpresa      = codEmpresa,
            CodPersonal     = codPersonal,
            FechaInicio     = fechaInicio,
            FechaFin        = fechaFin,
            ConnectionString = connectionString,
            CreadoEn        = DateTime.Now,
            Estado          = DepuracionEstado.Pendiente
        };

        _jobs[job.JobId] = job;
        _canal.Writer.TryWrite(job);

        _logger.LogInformation(
            "Depuración encolada: JobId={JobId}, Personal={Personal}, Rango={Inicio}→{Fin}",
            job.JobId, codPersonal,
            fechaInicio.ToString("dd/MM/yyyy"), fechaFin.ToString("dd/MM/yyyy"));

        return job.JobId;
    }

    // ── Consultar estado ──────────────────────────────────────────────────────

    public DepuracionJob? ObtenerEstado(string jobId) =>
        _jobs.TryGetValue(jobId, out var job) ? job : null;

    // ── BackgroundService: loop principal ─────────────────────────────────────

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DepuracionJobService iniciado.");

        await foreach (var job in _canal.Reader.ReadAllAsync(stoppingToken))
        {
            // Ejecutar el job en Task.Run para no bloquear el canal
            // CancellationToken.None: el Oracle procedure NO debe cortarse aunque la app se detenga
            _ = Task.Run(() => EjecutarJobAsync(job), CancellationToken.None);
        }

        _logger.LogInformation("DepuracionJobService detenido.");
    }

    // ── Ejecución del procedure ───────────────────────────────────────────────

    private async Task EjecutarJobAsync(DepuracionJob job)
    {
        job.Estado     = DepuracionEstado.EnProceso;
        job.IniciadoEn = DateTime.Now;

        _logger.LogInformation(
            "Iniciando depuración: JobId={JobId}, Personal={Personal}",
            job.JobId, job.CodPersonal);

        try
        {
            await using var conn = new OracleConnection(job.ConnectionString);

            // Retry de conexión: hasta 3 intentos con backoff
            for (int intento = 1; intento <= 3; intento++)
            {
                try
                {
                    await conn.OpenAsync(CancellationToken.None);
                    break;
                }
                catch (OracleException oex) when (intento < 3)
                {
                    _logger.LogWarning(
                        "Intento {Intento}/3 de conexión Oracle fallido (ORA-{Codigo}): JobId={JobId}. Reintentando en {Seg}s…",
                        intento, oex.Number, job.JobId, intento * 3);
                    await Task.Delay(TimeSpan.FromSeconds(intento * 3), CancellationToken.None);
                }
            }

            await using var cmd = conn.CreateCommand();
            cmd.CommandType    = CommandType.StoredProcedure;
            cmd.CommandText    = $"{Paquete}.DEPURA_RANGO";
            cmd.CommandTimeout = 0; // Sin límite: el procedure puede tardar minutos

            cmd.Parameters.Add(new OracleParameter("p_cod_empresa",  OracleDbType.Varchar2) { Value = job.CodEmpresa });
            cmd.Parameters.Add(new OracleParameter("p_cod_personal", OracleDbType.Varchar2) { Value = job.CodPersonal });
            cmd.Parameters.Add(new OracleParameter("p_fecha_inicio", OracleDbType.Varchar2) { Value = job.FechaInicio.ToString("dd/MM/yyyy") });
            cmd.Parameters.Add(new OracleParameter("p_fecha_fin",    OracleDbType.Varchar2) { Value = job.FechaFin.ToString("dd/MM/yyyy") });
            cmd.Parameters.Add(new OracleParameter("p_solo_obreros", OracleDbType.Varchar2) { Value = "N" });
            cmd.Parameters.Add(new OracleParameter("cv_resultado",   OracleDbType.RefCursor) { Direction = ParameterDirection.Output });

            await using var reader = await cmd.ExecuteReaderAsync(CancellationToken.None);
            DepuraRangoResultadoDto resultado;

            if (await reader.ReadAsync(CancellationToken.None))
            {
                var r = (OracleDataReader)reader;
                resultado = new DepuraRangoResultadoDto
                {
                    Resultado         = GetStr(r, "resultado"),
                    FechaInicio       = GetStr(r, "fecha_inicio"),
                    FechaFin          = GetStr(r, "fecha_fin"),
                    TotalDias         = GetInt(r, "total_dias"),
                    DiasOk            = GetInt(r, "dias_ok"),
                    DiasError         = GetInt(r, "dias_error"),
                    TurnosNocturnos   = GetInt(r, "turnos_nocturnos"),
                    Entradas          = GetInt(r, "entradas"),
                    Anticipadas       = GetInt(r, "anticipadas"),
                    Salidas           = GetInt(r, "salidas"),
                    Inirefris         = GetInt(r, "inirefris"),
                    Finrefris         = GetInt(r, "finrefris"),
                    Anomalas          = GetInt(r, "anomalas"),
                    NocturnosSinRefri = GetInt(r, "nocturnos_sin_refri"),
                    Recalculos        = GetInt(r, "recalculos"),
                    TotalGeneradas    = GetInt(r, "total_generadas"),
                    TotalHistorial    = GetInt(r, "total_historial"),
                };
            }
            else
            {
                resultado = new DepuraRangoResultadoDto { Resultado = "SIN_DATOS" };
            }

            job.Resultado     = resultado;
            job.Estado        = resultado.Resultado?.StartsWith("ERROR") == true
                                    ? DepuracionEstado.Error
                                    : DepuracionEstado.Completado;
            job.FinalizadoEn  = DateTime.Now;

            var duracion = (job.FinalizadoEn - job.IniciadoEn)?.TotalSeconds ?? 0;
            _logger.LogInformation(
                "Depuración finalizada: JobId={JobId}, Estado={Estado}, Duración={Dur:F1}s, Resultado={Resultado}",
                job.JobId, job.Estado, duracion, resultado.Resultado);
        }
        catch (Exception ex)
        {
            job.Estado       = DepuracionEstado.Error;
            job.MensajeError = ex.Message;
            job.FinalizadoEn = DateTime.Now;

            _logger.LogError(ex,
                "Error en depuración: JobId={JobId}, Personal={Personal}",
                job.JobId, job.CodPersonal);
        }
    }

    // ── Helpers de lectura ────────────────────────────────────────────────────

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
}
