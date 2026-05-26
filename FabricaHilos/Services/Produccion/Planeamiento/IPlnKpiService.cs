using FabricaHilos.Models.Produccion.Planeamiento;

namespace FabricaHilos.Services.Produccion.Planeamiento;

public interface IPlnKpiService
{
    Task<PlnKpiResumen>                      GetResumenAsync();

    /// <summary>
    /// Carga de máquinas para la ventana de los próximos 30 días (fuente: V_PLN_CARGA_MAQUINAS).
    /// Incluye la fecha de hoy hasta hoy+30. Usar para el Gantt/Heatmap en CargaMaquinas.cshtml.
    /// </summary>
    Task<IEnumerable<PlnCargaDiaria>>        GetCargaMaquinasAsync();

    /// <summary>
    /// Carga de máquinas para un rango de fechas arbitrario (fuente: PLN_CARGA_DIARIA directamente).
    /// Usar cuando el rango supera la ventana de 30 días de V_PLN_CARGA_MAQUINAS.
    /// </summary>
    Task<IEnumerable<PlnCargaDiaria>>        GetCargaMaquinasRangoAsync(DateTime fchIni, DateTime fchFin);
    /// <summary>V_PLN_ESTADO_PEDIDO §8.1: resumen por pedido (estado, avance, retrasos).</summary>
    Task<IEnumerable<PlnEstadoPedido>>       GetEstadoPedidosAsync();
    /// <summary>V_PLN_PENDIENTES_DESP §8.6: ítems listos para despachar priorizados.</summary>
    Task<IEnumerable<PlnPendienteDespacho>>  GetPendientesDespachoAsync();
    /// <summary>Ítems en pasos 08–11 (próximos a llegar a Alm PT), ordenados por FCH_ENTREGA_COMP.</summary>
    Task<IEnumerable<PlnPendienteDespacho>>  GetProximosDespachoAsync();
    /// <summary>V_PLN_KPI_PRODUCCION §8.8: eficiencia por máquina últimos 12 meses.</summary>
    Task<IEnumerable<PlnKpiProduccion>>      GetKpiProduccionAsync();

    /// <summary>
    /// Llama a PKG_PLN.SP_PLN_CARGA_DIARIA_REFRESH para recalcular PLN_CARGA_DIARIA en el rango dado.
    /// Normalmente ejecutado por JOB_PLN_CARGA; disponible aquí para refresco manual.
    /// </summary>
    Task RefreshCargaDiariaAsync(DateTime fchIni, DateTime fchFin);

    /// <summary>
    /// Compromisos de máquinas activos: qué pedidos usan / usarán cada máquina.
    /// Fuentes: PLN_SEGUIMIENTO (COD_MAQ_SECADO, COD_MAQ_DEVAN) +
    ///          TT_RSECADO / TT_RPRODUC activos para ítems sin PLN tracking.
    /// </summary>
    Task<IEnumerable<PlnMaquinaCompromiso>> GetMaquinasCompromisoAsync();

    /// <summary>
    /// Estado en tiempo real de TODAS las máquinas de tintorería (Thies, HANK, Mad.Rodete).
    /// Fuente: T_MAQUINAS (catálogo tipo T/M) + TT_RPRODUC — ACTIVA si estado IN ('1','2'), LIBRE si no tiene proceso activo.
    /// Incluye pedido, cliente, proceso, kg, retraso cuando el enlace PARTIDA→ITEMPED_DET está disponible.
    /// </summary>
    Task<IEnumerable<PlnEstadoMaquinaTT>> GetEstadoMaquinasTintoreriaAsync();

    /// <summary>
    /// Estado en tiempo real de TODAS las máquinas de secado (S01–S04 de T_MAQUINAS tipo S).
    /// Fuente: T_MAQUINAS TIPO_MAQ='S' (catálogo) + TT_RSECADO — ACTIVA si estado IN ('1','2'), LIBRE si no tiene proceso activo.
    /// Incluye pedido, cliente, proceso, kg, retraso cuando el enlace PARTIDA→ITEMPED_DET está disponible.
    /// </summary>
    Task<IEnumerable<PlnEstadoMaquinaTT>> GetEstadoMaquinasSecadoAsync();

    /// <summary>
    /// Estado en tiempo real de TODAS las máquinas de soporte TT (Centrífugas C01-C02, Mercerizadora MR2,
    /// Prensadora P01, Calderos Q01-Q02). Fuente: TT_MAQUINA TIPO_MAQ IN ('C','M','P','Q') ESTADO='0'.
    /// No tienen tabla de actividad propia → siempre retornan LIBRE con Descripcion real de TT_MAQUINA.
    /// </summary>
    Task<IEnumerable<PlnEstadoMaquinaTT>> GetEstadoMaquinasOtrasAsync();

    /// <summary>
    /// Resumen de actividad de máquinas de hilandería agrupado por tipo + máquina.
    /// Fuente: H_RPRODUC (últimas 24h). Incluye lote, título, proceso, kg, husos, velocidad.
    /// </summary>
    Task<IEnumerable<PlnResumenHilanderia>> GetResumenHilanderiaAsync();
}
