using FabricaHilos.Models.Produccion.Planeamiento;

namespace FabricaHilos.Services.Produccion.Planeamiento;

public interface IPlnKpiService
{
    Task<PlnKpiResumen>                      GetResumenAsync();
    Task<IEnumerable<PlnCargaDiaria>>        GetCargaMaquinasAsync();
    /// <summary>V_PLN_ESTADO_PEDIDO §8.1: resumen por pedido (estado, avance, retrasos).</summary>
    Task<IEnumerable<PlnEstadoPedido>>       GetEstadoPedidosAsync();
    /// <summary>V_PLN_PENDIENTES_DESP §8.6: ítems listos para despachar priorizados.</summary>
    Task<IEnumerable<PlnPendienteDespacho>>  GetPendientesDespachoAsync();
    /// <summary>V_PLN_KPI_PRODUCCION §8.8: eficiencia por máquina últimos 12 meses.</summary>
    Task<IEnumerable<PlnKpiProduccion>>      GetKpiProduccionAsync();

    /// <summary>
    /// Llama a PKG_PLN.SP_PLN_CARGA_DIARIA_REFRESH para recalcular PLN_CARGA_DIARIA en el rango dado.
    /// Normalmente ejecutado por JOB_PLN_CARGA; disponible aquí para refresco manual.
    /// </summary>
    Task RefreshCargaDiariaAsync(DateTime fchIni, DateTime fchFin);
}
