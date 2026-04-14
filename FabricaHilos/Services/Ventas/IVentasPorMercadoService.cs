using FabricaHilos.Models.Ventas;

namespace FabricaHilos.Services.Ventas
{
    public interface IVentasPorMercadoService
    {
        /// <summary>Ventas agrupadas por mercado (Perú, Latam, Global).</summary>
        Task<List<VentaMercadoDto>> ObtenerVentasPorMercadoAsync(DateTime fechaInicio, DateTime fechaFin, string moneda);

        /// <summary>Detalle por país dentro de un mercado específico.</summary>
        Task<List<VentaMercadoPaisDto>> ObtenerDetallePorPaisAsync(DateTime fechaInicio, DateTime fechaFin, string moneda, string? mercado);

        /// <summary>Detalle por departamento (solo para mercado Perú).</summary>
        Task<List<VentaMercadoDepartamentoDto>> ObtenerDetallePorDepartamentoAsync(DateTime fechaInicio, DateTime fechaFin, string moneda);
    }
}
