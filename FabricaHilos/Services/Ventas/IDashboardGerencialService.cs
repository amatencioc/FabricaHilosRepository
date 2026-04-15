using FabricaHilos.Models.Ventas;

namespace FabricaHilos.Services.Ventas
{
    public interface IDashboardGerencialService
    {
        /// <summary>Ventas agrupadas por mercado (Perú, Latam, Global).</summary>
        Task<List<DgVentaMercadoDto>> ObtenerVentasPorMercadoAsync(DateTime fechaInicio, DateTime fechaFin, string moneda);

        /// <summary>Detalle por país dentro de un mercado específico.</summary>
        Task<List<DgVentaMercadoPaisDto>> ObtenerDetallePorPaisAsync(DateTime fechaInicio, DateTime fechaFin, string moneda, string? mercado);

        /// <summary>Detalle por departamento (solo para mercado Perú).</summary>
        Task<List<DgVentaMercadoDepartamentoDto>> ObtenerDetallePorDepartamentoAsync(DateTime fechaInicio, DateTime fechaFin, string moneda);

        /// <summary>Detalle por distrito dentro de un departamento peruano (UBIGEO.PAIS='01').</summary>
        Task<List<DgVentaMercadoDistritoDto>> ObtenerDetallePorDistritoAsync(DateTime fechaInicio, DateTime fechaFin, string moneda, string departamento);

        /// <summary>Detalle de ciudades/distritos de un país extranjero (UBIGEO.PAIS≠'01').</summary>
        Task<List<DgVentaMercadoCiudadPaisDto>> ObtenerCiudadesPorPaisAsync(DateTime fechaInicio, DateTime fechaFin, string moneda, string codigoPais);

        /// <summary>Evolución mensual de ventas por mercado.</summary>
        Task<List<DgVentaMercadoEvolucionDto>> ObtenerEvolucionMensualAsync(DateTime fechaInicio, DateTime fechaFin, string moneda);

        /// <summary>Top clientes por importe de ventas.</summary>
        Task<List<DgVentaMercadoTopClienteDto>> ObtenerTopClientesAsync(DateTime fechaInicio, DateTime fechaFin, string moneda, string? mercado, int top);

        /// <summary>Detalle completo de documentos de venta (nivel transaccional).</summary>
        Task<List<DgVentaMercadoDocumentoDto>> ObtenerDetalleDocumentosAsync(DateTime fechaInicio, DateTime fechaFin, string moneda, string? mercado);

        /// <summary>Mapeo de países BD (TABLAS_AUXILIARES TIPO=25) con código ISO (INDICADOR2).</summary>
        Task<List<DgPaisIsoDto>> ObtenerPaisesIsoAsync();
    }
}
