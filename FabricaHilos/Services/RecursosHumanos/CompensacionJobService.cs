using FabricaHilos.Models.RecursosHumanos;
using Oracle.ManagedDataAccess.Client;
using System.Collections.Concurrent;
using System.Data;
using System.Threading.Channels;

namespace FabricaHilos.Services.RecursosHumanos;

// ── Estado de un job de compensación ─────────────────────────────────────────

public enum CompensacionEstado { Pendiente, EnProceso, Completado, Error }

public class CompensacionJob
{
    public string JobId          { get; init; } = Guid.NewGuid().ToString("N");
    public string CodEmpresa     { get; init; } = string.Empty;
    public string CodPersonal    { get; init; } = string.Empty;
    public DateTime FechaInicio  { get; init; }
    public DateTime FechaFin     { get; init; }
    public string ConnectionString { get; init; } = string.Empty;

    public CompensacionEstado Estado   { get; set; } = CompensacionEstado.Pendiente;
    public DateTime CreadoEn           { get; set; } = DateTime.Now;
    public DateTime? IniciadoEn        { get; set; }
    public DateTime? FinalizadoEn      { get; set; }
    public AplicarRangoResultadoDto? Resultado { get; set; }
    public string? MensajeError        { get; set; }
}

// ── Interfaz pública ──────────────────────────────────────────────────────────

public interface ICompensacionJobService
{
    string Encolar(string codEmpresa, string codPersonal, DateTime fechaInicio, DateTime fechaFin, string connectionString);
    CompensacionJob? ObtenerEstado(string jobId);
}

// ── Implementación: Singleton + BackgroundService ─────────────────────────────

public class CompensacionJobService : BackgroundService, ICompensacionJobService
{
    private readonly Channel<CompensacionJob> _canal;
    private readonly ConcurrentDictionary<string, CompensacionJob> _jobs = new();
    private readonly ILogger<CompensacionJobService> _logger;
    private const string Paquete = "AQUARIUS.PKG_SCA_COMPENSACIONES";

    public CompensacionJobService(ILogger<CompensacionJobService> logger)
    {
        _logger = logger;
        _canal = Channel.CreateBounded<CompensacionJob>(new BoundedChannelOptions(50)
        {
            FullMode     = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    // ── Encolar ───────────────────────────────────────────────────────────────

    public string Encolar(string codEmpresa, string codPersonal, DateTime fechaInicio, DateTime fechaFin, string connectionString)
    {
        var job = new CompensacionJob
        {
            CodEmpresa       = codEmpresa,
            CodPersonal      = codPersonal,
            FechaInicio      = fechaInicio,
            FechaFin         = fechaFin,
            ConnectionString = connectionString,
            CreadoEn         = DateTime.Now,
            Estado           = CompensacionEstado.Pendiente
        };

        _jobs[job.JobId] = job;
        _canal.Writer.TryWrite(job);

        _logger.LogInformation(
            "Compensación encolada: JobId={JobId}, Personal={Personal}, Rango={Inicio}→{Fin}",
            job.JobId, codPersonal,
            fechaInicio.ToString("dd/MM/yyyy"), fechaFin.ToString("dd/MM/yyyy"));

        return job.JobId;
    }

    // ── Consultar estado ──────────────────────────────────────────────────────

    public CompensacionJob? ObtenerEstado(string jobId) =>
        _jobs.TryGetValue(jobId, out var job) ? job : null;

    // ── BackgroundService: loop principal ─────────────────────────────────────

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CompensacionJobService iniciado.");

        await foreach (var job in _canal.Reader.ReadAllAsync(stoppingToken))
        {
            _ = Task.Run(() => EjecutarJobAsync(job), CancellationToken.None);
        }

        _logger.LogInformation("CompensacionJobService detenido.");
    }

    // ── Ejecución del procedure ───────────────────────────────────────────────

    private async Task EjecutarJobAsync(CompensacionJob job)
    {
        job.Estado     = CompensacionEstado.EnProceso;
        job.IniciadoEn = DateTime.Now;

        _logger.LogInformation(
            "Iniciando aplicación de compensaciones: JobId={JobId}, Personal={Personal}",
            job.JobId, job.CodPersonal);

        try
        {
            await using var conn = new OracleConnection(job.ConnectionString);
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandType    = CommandType.StoredProcedure;
            cmd.CommandText    = $"{Paquete}.APLICAR_RANGO";
            cmd.CommandTimeout = 300;

            cmd.Parameters.Add(new OracleParameter("p_cod_empresa",        OracleDbType.Varchar2) { Value = job.CodEmpresa });
            cmd.Parameters.Add(new OracleParameter("p_cod_personal",       OracleDbType.Varchar2) { Value = job.CodPersonal });
            cmd.Parameters.Add(new OracleParameter("p_fecha_inicio",       OracleDbType.Varchar2) { Value = job.FechaInicio.ToString("dd/MM/yyyy") });
            cmd.Parameters.Add(new OracleParameter("p_fecha_fin",          OracleDbType.Varchar2) { Value = job.FechaFin.ToString("dd/MM/yyyy") });
            cmd.Parameters.Add(new OracleParameter("p_eliminar_no_cuadra", OracleDbType.Varchar2) { Value = "S" });
            cmd.Parameters.Add(new OracleParameter("cv_resultado",         OracleDbType.RefCursor) { Direction = ParameterDirection.Output });

            await using var reader = await cmd.ExecuteReaderAsync();
            AplicarRangoResultadoDto resultado;
            if (await reader.ReadAsync())
            {
                var r = (OracleDataReader)reader;
                static string? S(OracleDataReader rd, string col) { try { return rd[col] == DBNull.Value ? null : rd[col]?.ToString(); } catch { return null; } }
                static int? I(OracleDataReader rd, string col) { try { return rd[col] == DBNull.Value ? null : Convert.ToInt32(rd[col]); } catch { return null; } }

                resultado = new AplicarRangoResultadoDto
                {
                    FechaInicio           = S(r, "fecha_inicio"),
                    FechaFin              = S(r, "fecha_fin"),
                    CodEmpresa            = S(r, "cod_empresa"),
                    CodPersonal           = S(r, "cod_personal"),
                    DiasProcesados        = I(r, "dias_procesados"),
                    TotalAplicadasDestino = I(r, "total_aplicadas_destino"),
                    TotalAplicadasOrigen  = I(r, "total_aplicadas_origen"),
                    TotalEliminadas       = I(r, "total_eliminadas"),
                    TotalErrores          = I(r, "total_errores"),
                };
            }
            else
            {
                resultado = new AplicarRangoResultadoDto { TotalErrores = 0 };
            }

            job.Resultado     = resultado;
            job.Estado        = CompensacionEstado.Completado;
            job.FinalizadoEn  = DateTime.Now;

            _logger.LogInformation(
                "Compensaciones completadas: JobId={JobId}, Aplicadas={Apl}, Eliminadas={El}, Errores={Er}",
                job.JobId,
                resultado.TotalAplicadasDestino + resultado.TotalAplicadasOrigen,
                resultado.TotalEliminadas,
                resultado.TotalErrores);
        }
        catch (Exception ex)
        {
            job.Estado       = CompensacionEstado.Error;
            job.MensajeError = ex.Message;
            job.FinalizadoEn = DateTime.Now;
            _logger.LogError(ex, "Error en CompensacionJobService: JobId={JobId}", job.JobId);
        }
    }
}
