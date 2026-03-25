using FabricaHilos.Models.Sgc;

namespace FabricaHilos.Services.Sgc
{
    public interface IDashboardSgcService
    {
        Task<DashKpiDto> ObtenerKpiAsync(DateTime fechaInicio, DateTime fechaFin);
        Task<List<DashEstadoDto>> ObtenerPorEstadoAsync(DateTime fechaInicio, DateTime fechaFin);
        Task<List<DashEvolucionDto>> ObtenerEvolucionMensualAsync(DateTime fechaInicio, DateTime fechaFin);
        Task<List<DashTopClienteDto>> ObtenerTopClientesAsync(DateTime fechaInicio, DateTime fechaFin, int top);
        Task<List<DashTopArticuloDto>> ObtenerTopArticulosAsync(DateTime fechaInicio, DateTime fechaFin, int top);
        Task<List<DashVendedorDto>> ObtenerPorVendedorAsync(DateTime fechaInicio, DateTime fechaFin);
        Task<List<DashMonedaDto>> ObtenerPorMonedaAsync(DateTime fechaInicio, DateTime fechaFin);
        Task<List<DashSucursalClienteDto>> ObtenerSucursalClienteAsync(DateTime fechaInicio, DateTime fechaFin, int top);
        Task<List<DashDespachoDto>> ObtenerDespachosAsync(DateTime fechaInicio, DateTime fechaFin);
        Task<List<DashPedidoRiesgoDto>> ObtenerPedidosEnRiesgoAsync(int diasMinimos = 30);
        Task<List<DashTicketClienteDto>> ObtenerTicketPorClienteAsync(DateTime fechaInicio, DateTime fechaFin, int top = 15);
        Task<List<DashCicloDto>> ObtenerCicloCierreAsync(DateTime fechaInicio, DateTime fechaFin);
        Task<List<DashRecompraDto>> ObtenerRecompraAsync(DateTime fechaInicio, DateTime fechaFin);
        Task<List<DashConcentracionDto>> ObtenerConcentracionRiesgoAsync(DateTime fechaInicio, DateTime fechaFin);
        Task<List<DashZonaDto>> ObtenerPorZonaAsync(DateTime fechaInicio, DateTime fechaFin);
        Task<DashMixProductoResultDto> ObtenerMixProductoAsync(DateTime fechaInicio, DateTime fechaFin);
    }
}
