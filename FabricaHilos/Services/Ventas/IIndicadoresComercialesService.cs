using FabricaHilos.Models.Ventas;

namespace FabricaHilos.Services.Ventas
{
    public interface IIndicadoresComercialesService
    {
        /// <summary>Query 1: Importe por Asesor / Mes (sin conceptos inafectos a comisiones)</summary>
        Task<List<ImporteAsesorMesDto>> ObtenerImportePorAsesorAsync(DateTime fechaInicio, DateTime fechaFin, string moneda);

        /// <summary>Query 1.1: Detalle de Importe por Cliente por Asesor / Mes</summary>
        Task<List<DetalleImporteAsesorMesDto>> ObtenerDetalleImportePorAsesorAsync(DateTime fechaInicio, DateTime fechaFin, string moneda, string asesor, string mes);

        /// <summary>Query 2: Cantidad KG por Asesor / Mes</summary>
        Task<List<CantidadKgAsesorMesDto>> ObtenerCantidadKgPorAsesorAsync(DateTime fechaInicio, DateTime fechaFin);

        /// <summary>Query 3: Nro. de Clientes por Asesor / Mes</summary>
        Task<List<NroClientesAsesorMesDto>> ObtenerNroClientesPorAsesorAsync(DateTime fechaInicio, DateTime fechaFin);

        /// <summary>Query 3.1: Detalle de Clientes por Asesor / Mes</summary>
        Task<List<DetalleClienteAsesorMesDto>> ObtenerDetalleClientesPorAsesorAsync(DateTime fechaInicio, DateTime fechaFin, string moneda, string asesor, string mes);
    }
}
