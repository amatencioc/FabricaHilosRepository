using FabricaHilos.Models.Produccion.Planeamiento;

namespace FabricaHilos.Services.Produccion.Planeamiento;

public interface IPlnSeguimientoService
{
    Task<IEnumerable<PlnSeguimiento>>    GetActivosAsync(string? codCliente = null, string? codPaso = null);
    Task<IEnumerable<PlnSeguimiento>>    GetPorPedidoAsync(long numPed, int serie);
    Task<IEnumerable<PlnEstadoCodigo>>   GetEstadosAsync();
    Task<IEnumerable<PlnLogEvento>>      GetEventosPorPedidoAsync(long numPed, int serie);
    Task<IEnumerable<PlnAlerta>>         GetAlertasPorPedidoAsync(long numPed, int serie);
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
                         string nuevoPaso, string? observacion = null, decimal? kgCantidad = null);

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
}
