using FabricaHilos.Models.RecursosHumanos;
using Oracle.ManagedDataAccess.Client;
using System.Collections.Concurrent;
using System.Data;
using System.Threading.Channels;

namespace FabricaHilos.Services.RecursosHumanos;

// ── Estado de un job de evento masivo ─────────────────────────────────────────

public enum MasivaEventoEstado { Pendiente, EnProceso, Completado, Error }

public class MasivaEventoJob
{
    public string JobId             { get; init; } = Guid.NewGuid().ToString("N");
    public string CodEmpresa        { get; init; } = string.Empty;
    public DateTime FechaOrigen     { get; init; }
    public DateTime FechaDestino    { get; init; }
    public char TipoOrigen          { get; init; }
    public char TipoCompensacion    { get; init; }
    public string ListaPersonal     { get; init; } = string.Empty;
    public string ConnectionString  { get; init; } = string.Empty;

    public MasivaEventoEstado Estado { get; set; } = MasivaEventoEstado.Pendiente;
    public DateTime CreadoEn         { get; set; } = DateTime.Now;
    public DateTime? IniciadoEn      { get; set; }
    public DateTime? FinalizadoEn    { get; set; }
    public RegistrarEventoResultadoDto? Resultado { get; set; }
    public string? MensajeError      { get; set; }
}

// ── Interfaz pública ──────────────────────────────────────────────────────────

public interface IMasivaEventoJobService
{
    string Encolar(string codEmpresa, DateTime fechaOrigen, DateTime fechaDestino,
        char tipoOrigen, char tipoCompensacion, string listaPersonal, string connectionString);
    MasivaEventoJob? ObtenerEstado(string jobId);
}

// ── Implementación: Singleton + BackgroundService ─────────────────────────────

public class MasivaEventoJobService : BackgroundService, IMasivaEventoJobService
{
    private readonly Channel<MasivaEventoJob> _canal;
    private readonly ConcurrentDictionary<string, MasivaEventoJob> _jobs = new();
    private readonly ILogger<MasivaEventoJobService> _logger;
    private const string Paquete = "AQUARIUS.PKG_SCA_COMPENSACIONES";

    public MasivaEventoJobService(ILogger<MasivaEventoJobService> logger)
    {
        _logger = logger;
        _canal = Channel.CreateBounded<MasivaEventoJob>(new BoundedChannelOptions(20)
        {
            FullMode     = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    // ── Encolar ───────────────────────────────────────────────────────────────

    public string Encolar(string codEmpresa, DateTime fechaOrigen, DateTime fechaDestino,
        char tipoOrigen, char tipoCompensacion, string listaPersonal, string connectionString)
    {
        var job = new MasivaEventoJob
        {
            CodEmpresa       = codEmpresa,
            FechaOrigen      = fechaOrigen,
            FechaDestino     = fechaDestino,
            TipoOrigen       = tipoOrigen,
            TipoCompensacion = tipoCompensacion,
            ListaPersonal    = listaPersonal,
            ConnectionString = connectionString,
            CreadoEn         = DateTime.Now,
            Estado           = MasivaEventoEstado.Pendiente
        };

        _jobs[job.JobId] = job;
        _canal.Writer.TryWrite(job);

        _logger.LogInformation(
            "Evento masivo encolado: JobId={JobId}, Origen={Origen}, Destino={Destino}, Empleados={N}",
            job.JobId,
            fechaOrigen.ToString("dd/MM/yyyy"),
            fechaDestino.ToString("dd/MM/yyyy"),
            string.IsNullOrEmpty(listaPersonal) ? "TODOS" : listaPersonal.Split(',').Length.ToString());

        return job.JobId;
    }

    // ── Consultar estado ──────────────────────────────────────────────────────

    public MasivaEventoJob? ObtenerEstado(string jobId) =>
        _jobs.TryGetValue(jobId, out var job) ? job : null;

    // ── BackgroundService: loop principal ─────────────────────────────────────

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MasivaEventoJobService iniciado.");

        await foreach (var job in _canal.Reader.ReadAllAsync(stoppingToken))
        {
            _ = Task.Run(() => EjecutarJobAsync(job), CancellationToken.None);
        }

        _logger.LogInformation("MasivaEventoJobService detenido.");
    }

    // ── Ejecución del procedure ───────────────────────────────────────────────

    private async Task EjecutarJobAsync(MasivaEventoJob job)
    {
        job.Estado     = MasivaEventoEstado.EnProceso;
        job.IniciadoEn = DateTime.Now;

        _logger.LogInformation(
            "Iniciando evento masivo: JobId={JobId}, Origen={Origen}, Empleados={Lista}",
            job.JobId, job.FechaOrigen.ToString("dd/MM/yyyy"), job.ListaPersonal);

        try
        {
            await using var conn = new OracleConnection(job.ConnectionString);
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandType    = CommandType.StoredProcedure;
            cmd.CommandText    = $"{Paquete}.REGISTRAR_EVENTO";
            cmd.CommandTimeout = 300;

            cmd.Parameters.Add(new OracleParameter("p_cod_empresa",        OracleDbType.Varchar2) { Value = job.CodEmpresa });
            cmd.Parameters.Add(new OracleParameter("p_fecha_origen",       OracleDbType.Varchar2) { Value = job.FechaOrigen.ToString("dd/MM/yyyy") });
            cmd.Parameters.Add(new OracleParameter("p_fecha_destino",      OracleDbType.Varchar2) { Value = job.FechaDestino.ToString("dd/MM/yyyy") });
            cmd.Parameters.Add(new OracleParameter("p_tipo_origen",        OracleDbType.Varchar2) { Value = job.TipoOrigen.ToString() });
            cmd.Parameters.Add(new OracleParameter("p_tipo_compensacion",  OracleDbType.Varchar2) { Value = job.TipoCompensacion.ToString() });
            cmd.Parameters.Add(new OracleParameter("p_lista_personal",     OracleDbType.Varchar2) { Value = string.IsNullOrEmpty(job.ListaPersonal) ? (object)DBNull.Value : job.ListaPersonal });
            cmd.Parameters.Add(new OracleParameter("p_horas_override",     OracleDbType.Varchar2) { Value = DBNull.Value });
            cmd.Parameters.Add(new OracleParameter("p_aplicar",            OracleDbType.Varchar2) { Value = "S" });
            cmd.Parameters.Add(new OracleParameter("p_eliminar_no_cuadra", OracleDbType.Varchar2) { Value = "S" });
            cmd.Parameters.Add(new OracleParameter("p_validar",            OracleDbType.Varchar2) { Value = "S" });
            cmd.Parameters.Add(new OracleParameter("p_solo_obreros",       OracleDbType.Varchar2) { Value = "N" });
            cmd.Parameters.Add(new OracleParameter("cv_resultado",         OracleDbType.RefCursor) { Direction = ParameterDirection.Output });

            static string? S(OracleDataReader r, string c) { try { return r[c] == DBNull.Value ? null : r[c]?.ToString(); } catch { return null; } }
            static int I(OracleDataReader r, string c) { try { return r[c] == DBNull.Value ? 0 : Convert.ToInt32(r[c]); } catch { return 0; } }

            await using var reader = await cmd.ExecuteReaderAsync();
            RegistrarEventoResultadoDto resultado;
            if (await reader.ReadAsync())
            {
                var r = (OracleDataReader)reader;
                resultado = new RegistrarEventoResultadoDto
                {
                    FechaOrigen          = S(r, "fecha_origen"),
                    FechaDestino         = S(r, "fecha_destino"),
                    EmpleadosEncontrados = I(r, "empleados_encontrados"),
                    RegistradasOk        = I(r, "registradas_ok"),
                    AplicadasOk          = I(r, "aplicadas_ok"),
                    Errores              = I(r, "errores"),
                    Estado               = S(r, "estado"),
                };
            }
            else
            {
                resultado = new RegistrarEventoResultadoDto { Estado = "SIN_DATOS" };
            }

            job.Resultado    = resultado;
            job.Estado       = MasivaEventoEstado.Completado;
            job.FinalizadoEn = DateTime.Now;

            _logger.LogInformation(
                "Evento masivo completado: JobId={JobId}, Empleados={Emp}, RegistradasOk={Reg}, AplicadasOk={Apl}, Errores={Err}",
                job.JobId, resultado.EmpleadosEncontrados, resultado.RegistradasOk, resultado.AplicadasOk, resultado.Errores);
        }
        catch (Exception ex)
        {
            job.Estado       = MasivaEventoEstado.Error;
            job.MensajeError = ex.Message;
            job.FinalizadoEn = DateTime.Now;
            _logger.LogError(ex, "Error en MasivaEventoJobService: JobId={JobId}", job.JobId);
        }
    }
}
