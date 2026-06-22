using FabricaHilos.Models.Sire;

namespace FabricaHilos.Services.Sire;

/// <summary>
/// Repositorio Oracle para todas las operaciones de persistencia SIRE.
/// Encapsula las tablas: SIRE_JOB, SIRE_LOG, SIRE_PROPUESTA, SIRE_LEGACY, SIRE_CONCIL.
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

    /// <summary>
    /// Obtiene todos los jobs para un tipo+período específico, ordenados por fecha creación desc.
    /// Garantiza encontrar el job correcto sin límite arbitrario de filas.
    /// </summary>
    Task<List<SireExportacionJob>> GetJobsPorTipoPeriodoAsync(string tipo, string periodo, CancellationToken ct = default);

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

    // ── SIRE_PROPUESTA (propuestas descargadas) ───────────────────────────────

    /// <summary>
    /// Retorna los registros de propuesta SUNAT para un período almacenados en SIRE_PROPUESTA.
    /// </summary>
    Task<List<SireValidaRegistro>> GetRegistrosPropuestaAsync(string tipo, string periodo, CancellationToken ct = default);

    /// <summary>
    /// Retorna un resumen de todas las propuestas descargadas (agrupado por TIPO+PERIODO).
    /// Usado en las vistas Compras/Ventas para mostrar la lista de archivos disponibles.
    /// </summary>
    Task<List<PropuestaPeriodoResumen>> GetPropuestasResumenAsync(string tipo, CancellationToken ct = default);

    /// <summary>
    /// Elimina todos los registros de SIRE_PROPUESTA de un período. Devuelve filas borradas.
    /// </summary>
    Task<int> EliminarPropuestaAsync(string tipo, int periodo, CancellationToken ct = default);

    /// <summary>
    /// Ejecuta SP_SIRE_CARGA_LEGACY + SP_SIRE_CONCILIAR para el período dado.
    /// Devuelve un mensaje de resultado.
    /// </summary>
    Task<string> ConciliarPropuestaAsync(string tipo, int periodo, CancellationToken ct = default);

    /// <summary>
    /// Lee el resumen de la última conciliación desde SIRE_CONCIL_RESUMEN.
    /// Devuelve null si nunca se concilió el período.
    /// </summary>
    Task<SireConcilResumen?> GetConcilResumenAsync(string tipo, string periodo, CancellationToken ct = default);

    // ── SIRE_LEGACY (datos ERP cargados por SP_SIRE_CARGA_LEGACY) ─────────

    /// <summary>
    /// Retorna los registros del ERP almacenados en SIRE_LEGACY para un período.
    /// </summary>
    Task<List<SireLegacyRegistro>> GetLegacyAsync(string tipo, string periodo, CancellationToken ct = default);

    // ── SIRE_CONCIL (resultado cruzado SUNAT vs Legacy) ───────────────────

    /// <summary>
    /// Retorna el detalle fila a fila de SIRE_CONCIL para un período.
    /// Incluye registros OK, con diferencia, solo-SUNAT y solo-Legacy.
    /// </summary>
    Task<List<SireConcilDetalle>> GetConcilDetalleAsync(string tipo, string periodo, CancellationToken ct = default);

    /// <summary>
    /// Invalida la conciliación de un período: borra SIRE_CONCIL y resetea
    /// SIRE_LEGACY.ID_PROP_MATCH = NULL. Debe llamarse después de re-procesar el ZIP
    /// para que el usuario deba volver a ejecutar Conciliar.
    /// </summary>
    Task InvalidarConciliacionAsync(string tipo, int periodo, CancellationToken ct = default);

    // =========================================================================
    // Exclusiones (SIRE_EXCLUIDOS_LOGIX)
    // =========================================================================

    /// <summary>
    /// Devuelve todos los excluidos activos de un período (ESTADO='A').
    /// </summary>
    Task<List<SireExcluidoLogix>> GetExcluidosAsync(string tipo, string periodo, CancellationToken ct = default);

    /// <summary>
    /// Excluye manualmente una lista de registros de SIRE_CONCIL (SOLO_SUNAT).
    /// Inserta en SIRE_EXCLUIDOS_LOGIX con MOTIVO='MANUAL' y cambia ESTADO a 'EXCLUIDO'.
    /// Retorna cuántos registros fueron excluidos.
    /// </summary>
    Task<int> ExcluirManualAsync(string tipo, int periodo, IEnumerable<long> idsConcil,
        string usuario, string? obs, CancellationToken ct = default);

    /// <summary>
    /// Restaura un excluido por ID_CONCIL: pone ESTADO='R' en SIRE_EXCLUIDOS_LOGIX y
    /// devuelve el registro SIRE_CONCIL a ESTADO='SOLO_SUNAT'.
    /// Si tiene un par vinculado (ID_EXCLUIDO_REL) lo restaura también.
    /// </summary>
    Task RestaurarExcluidoAsync(long idConcil, string usuario, CancellationToken ct = default);

    /// <summary>
    /// Llama a SP_SIRE_AUTO_EXCLUIR_NC: busca N/C en SOLO_SUNAT, las excluye
    /// junto a su doc. de referencia si éste también está en SOLO_SUNAT.
    /// </summary>
    Task AutoExcluirNcAsync(string tipo, int periodo, string usuario, CancellationToken ct = default);

    // =========================================================================
    // Validez de comprobante (API Consulta Integrada SUNAT)
    // =========================================================================

    /// <summary>
    /// Devuelve los registros SIRE_CONCIL pendientes de validar (VALIDEZ_CP IS NULL o '0')
    /// del tipo y período indicados, incluyendo los campos necesarios para llamar a la API.
    /// </summary>
    Task<List<SireConcilDetalle>> GetConcilPendientesValidezAsync(string tipo, string periodo, CancellationToken ct = default);

    /// <summary>
    /// Devuelve TODOS los registros SIRE_CONCIL del tipo y período indicados
    /// que pueden validarse contra SUNAT (excluye EXCLUIDO y SOLO_LEGACY).
    /// No filtra por VALIDEZ_CP: permite revalidar registros ya validados.
    /// </summary>
    Task<List<SireConcilDetalle>> GetConcilTodosParaValidarAsync(string tipo, string periodo, CancellationToken ct = default);

    /// <summary>
    /// Devuelve los datos de una sola fila de SIRE_CONCIL para validarla contra SUNAT.
    /// Incluye SUNAT_MONEDA y CAMBIO para conversión de moneda cuando corresponda.
    /// </summary>
    Task<SireConcilDetalle?> GetConcilFilaParaValidezAsync(long idConcil, CancellationToken ct = default);

    /// <summary>
    /// Guarda el resultado de la validación SUNAT en SIRE_CONCIL
    /// (VALIDEZ_CP, VALIDEZ_RUC, VALIDEZ_DOM, FCH_VALIDEZ = SYSDATE).
    /// </summary>
    Task GuardarValidezAsync(long idConcil, string estadoCp, string estadoRuc, string condDomiRuc, CancellationToken ct = default);

    // =========================================================================
    // SSCO — Sujetos Sin Capacidad Operativa
    // =========================================================================

    /// <summary>
    /// Devuelve en una sola query todos los RUCs de SIG.SSCO_LISTA más los metadatos
    /// de la última carga (fecha y período YYYYMM). Reemplaza GetSscoRucsAsync + GetSscoMetaAsync.
    /// </summary>
    Task<(HashSet<string> Rucs, DateTime? FchCarga, int? Periodo)> GetSscoDataAsync(CancellationToken ct = default);

    /// <summary>
    /// Devuelve el padrón completo de SIG.SSCO_LISTA ordenado por RUC.
    /// </summary>
    Task<List<SscoListaEntry>> GetSscoListaAsync(CancellationToken ct = default);

    /// <summary>
    /// Carga (MERGE) el padrón SSCO recibido en SIG.SSCO_LISTA.
    /// Actualiza las filas existentes y añade las nuevas. No borra RUCs que ya no estén en la lista.
    /// Devuelve el número de filas afectadas (INSERT + UPDATE).
    /// </summary>
    Task<int> CargarSscoLoteAsync(IEnumerable<SscoListaEntry> entries, int periodoCarga, string usuario, CancellationToken ct = default);
}
