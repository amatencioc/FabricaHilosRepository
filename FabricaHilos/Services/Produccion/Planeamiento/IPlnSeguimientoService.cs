using FabricaHilos.Models.Produccion.Planeamiento;

namespace FabricaHilos.Services.Produccion.Planeamiento;

public interface IPlnSeguimientoService
{
    Task<IEnumerable<PlnSeguimiento>>    GetActivosAsync(string? busquedaCliente = null, string? codPaso = null, string? numPed = null, bool incluyeCerrados = false);

    /// <summary>
    /// Devuelve una página de ítems de PLN_SEGUIMIENTO (10 pedidos por página).
    /// Totales globales (para KPIs y paginación) se calculan en la misma llamada
    /// con una sub-query COUNT para evitar dos round-trips.
    /// </summary>
    Task<PlnSeguimientoPagina> GetActivosPaginadoAsync(
        string? busquedaCliente = null,
        string? codPaso         = null,
        string? numPed          = null,
        string? asesor          = null,
        bool    incluyeCerrados = false,
        int     pagina          = 1,
        int     tamPagina       = 10);
    Task<IEnumerable<PlnSeguimiento>>    GetPorPedidoAsync(long numPed, int serie);

    /// <summary>Lee un único ítem por clave sustituta ID_SEGUIM. Devuelve null si no existe.</summary>
    Task<PlnSeguimiento?>                GetByIdAsync(long idSeguim);

    /// <summary>Lee un único ítem por clave natural (SERIE, NUM_PED, NRO, NUM_DET). Devuelve null si no existe.</summary>
    Task<PlnSeguimiento?>                GetByItemAsync(int serie, long numPed, int nro, int numDet);
    Task<IEnumerable<PlnEstadoCodigo>>   GetEstadosAsync();
    Task<IEnumerable<PlnLogEvento>>      GetEventosPorPedidoAsync(long numPed, int serie);
    Task<IEnumerable<PlnAlerta>>         GetAlertasPorPedidoAsync(long numPed, int serie);

    /// <summary>
    /// Devuelve el log de eventos de un ítem (PLN_LOG_EVENTOS) con filtros opcionales
    /// por tipo de evento y ciclo, con paginación del lado del servidor.
    /// </summary>
    Task<(IEnumerable<PlnLogEvento> Items, int TotalRegistros)> GetEventosPorSeguimAsync(
        long idSeguim,
        string? tipoEvento = null,
        int?    nroCiclo   = null,
        int     pagina     = 1,
        int     tamPagina  = 25);
    /// <summary>Devuelve filas de V_PLN_TRAZABILIDAD para el Timeline ApexCharts.</summary>
    Task<IEnumerable<PlnTrazabilidad>>   GetTrazabilidadAsync(long numPed, int serie);
    /// <summary>Devuelve historial de recálculos de PLN_FECHAS_ESTIMADAS de un ítem.</summary>
    Task<IEnumerable<PlnFechaEstimada>>  GetFechasEstimadasAsync(long idSeguim);

    // ── Llamadas a procedimientos PKG_PLN ─────────────────────────────────────
    /// <summary>
    /// Llama a PKG_PLN.SP_PLN_AVANZA_PASO para correcciones manuales autorizadas.
    /// No debe usarse en el flujo automático (eso lo hacen los triggers Oracle).
    /// </summary>
    Task AvanzaPasoAsync(int serie, long numPed, int nro, int numDet,
                         string nuevoPaso, string? observacion = null, decimal? kgCantidad = null,
                         string? proceso = null);

    /// <summary>Llama a PKG_PLN.SP_PLN_CIERRE_ITEM (cierre manual de ítem).</summary>
    Task CierreItemAsync(long idSeguim, string motivo, string usuario);

    /// <summary>Llama a PKG_PLN.SP_PLN_REPROGRAMAR (nueva fecha estimada de despacho).</summary>
    Task ReprogramarAsync(int serie, long numPed, int nro, int numDet,
                          DateTime nuevaFchDesp, string motivo, string usuario);

    /// <summary>
    /// Llama a PKG_PLN.SP_PLN_CALCULA_FECHAS para recalcular todas las fechas estimadas de un ítem.
    /// Motivos: 'PED'=pedido / 'PLA'=planificado / 'REP'=reprogramado / 'MAQ'=máquina
    /// </summary>
    Task CalcularFechasAsync(int serie, long numPed, int nro, int numDet, string motivo);

    /// <summary>
    /// Llama a PKG_PLN.SP_PLN_INIT_SEGUIMIENTO para inicializar el seguimiento de un ítem.
    /// Paso inicial: '01' (pedido registrado) o '13' (solo despacho).
    /// Idempotente: si ya existe el ítem, lo ignora silenciosamente.
    /// </summary>
    Task InitSeguimientoAsync(int serie, long numPed, int nro, int numDet = 0,
                              string pasoIni = "01");

    /// <summary>Obtiene descripción y material (FIBRA) de un artículo por COD_ART.</summary>
    Task<(string Descripcion, string Fibra)> GetArticuloInfoAsync(string codArt);

    /// <summary>
    /// Carga el detalle completo de Tintorería para una partida dada.
    /// Incluye: cálculo de recetas planificadas (ING_RECETAS_G), baños ejecutados
    /// (TT_RPRODUC), secado (TT_RSECADO), CC TT (CTCALIDAD_D) y validación de
    /// laboratorio (L_VALIDA_RECETA). Devuelve un objeto vacío si la partida no existe.
    /// </summary>
    Task<PlnDetalleTt> GetDetalleTtAsync(long numPartida);

    /// <summary>Estado de actividad de una máquina TT: baños activos en curso + carga desde PLN_CARGA_DIARIA (día más reciente disponible, máx 30 días atrás).</summary>
    Task<(int BanosActivos, bool EsLibre, decimal PctCargaHoy, bool HayCargaHoy, int DiasAntiguo)> GetMaquinaStatusAsync(string codMaq);
}
