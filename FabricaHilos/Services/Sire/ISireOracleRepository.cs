using FabricaHilos.Models.Sire;

namespace FabricaHilos.Services.Sire;

/// <summary>
/// Repositorio Oracle para todas las operaciones de persistencia SIRE.
/// Encapsula las tres tablas: SIRE_JOB, SIRE_HEALTH, SIRE_LOG.
/// </summary>
public interface ISireOracleRepository
{
    // ── Jobs ──────────────────────────────────────────────────────────────────

    /// <summary>Inserta un nuevo job y retorna el ID asignado por la secuencia Oracle.</summary>
    Task<int> InsertJobAsync(SireExportacionJob job, CancellationToken ct = default);

    /// <summary>Actualiza todos los campos mutables de un job existente.</summary>
    Task UpdateJobAsync(SireExportacionJob job, CancellationToken ct = default);

    /// <summary>Obtiene un job por su clave interna (ID numérico).</summary>
    Task<SireExportacionJob?> GetJobByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Obtiene un job por su JobId (GUID string).</summary>
    Task<SireExportacionJob?> GetJobByJobIdAsync(string jobId, CancellationToken ct = default);

    /// <summary>
    /// Busca el job más reciente en estado Pendiente o EnProceso para el tipo dado.
    /// Solo puede existir UN job activo por tipo (compras|ventas) a la vez.
    /// Usado para detectar si ya existe un proceso activo antes de crear uno nuevo.
    /// </summary>
    Task<SireExportacionJob?> GetJobActivoAsync(string tipoRegistro, CancellationToken ct = default);

    /// <summary>Obtiene todos los jobs en estado Pendiente o EnProceso (para reencolar al reiniciar).</summary>
    Task<List<SireExportacionJob>> GetJobsInterrumpidosAsync(CancellationToken ct = default);

    /// <summary>
    /// Obtiene todos los jobs en estado EsperandoTicket cuya PROXIMA_CONSULTA ya venció.
    /// Usado por SireTicketWatcherWorker para consultar el estado del ticket en SUNAT.
    /// </summary>
    Task<List<SireExportacionJob>> GetJobsEsperandoTicketAsync(CancellationToken ct = default);

    /// <summary>
    /// Obtiene los N jobs más recientes (cualquier estado), ordenados por fecha creación desc.
    /// Usado en el dashboard de Index para mostrar historial de operaciones.
    /// </summary>
    Task<List<SireExportacionJob>> GetJobsRecientesAsync(int top = 20, CancellationToken ct = default);

    // ── Health logs ───────────────────────────────────────────────────────────

    Task InsertHealthLogAsync(SireHealthCheckLog log, CancellationToken ct = default);

    /// <summary>Obtiene los últimos N registros de health check, ordenados por fecha desc.</summary>
    Task<List<SireHealthCheckLog>> GetHealthLogsAsync(int top = 50, CancellationToken ct = default);

    // ── API logs ──────────────────────────────────────────────────────────────

    /// <summary>Inserta un registro de auditoría de llamada HTTP. Fire-and-forget seguro.</summary>
    Task InsertApiLogAsync(SireApiLog log, CancellationToken ct = default);

    /// <summary>
    /// Obtiene los últimos N registros de SIRE_LOG, con filtros opcionales.
    /// </summary>
    /// <param name="top">Máximo de filas a retornar.</param>
    /// <param name="jobId">Filtrar por JobId específico (null = todos).</param>
    /// <param name="operacion">Filtrar por operación: AUTH|EXPORTAR|TICKET|DESCARGAR|HEALTH (null = todas).</param>
    /// <param name="ordenAscendente">
    /// true  = ORDER BY ID ASC  (cronológico, para el modal de progreso del job).
    /// false = ORDER BY ID DESC (más reciente primero, para Monitoreo y actividad).
    /// </param>
    Task<List<SireApiLog>> GetApiLogsAsync(int top = 200, string? jobId = null, string? operacion = null, CancellationToken ct = default, bool ordenAscendente = false);
}
