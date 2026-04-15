using FabricaHilos.Models.Ventas;

namespace FabricaHilos.Services.Ventas
{
    public interface IDashboardComercialService
    {
        /// <summary>Query 1: Importe por Asesor / Mes (sin conceptos inafectos a comisiones)</summary>
        Task<List<DcImporteAsesorMesDto>> ObtenerImportePorAsesorAsync(DateTime fechaInicio, DateTime fechaFin, string moneda);

        /// <summary>Query 1.1: Detalle de Importe por Cliente por Asesor / Mes</summary>
        Task<List<DcDetalleImporteAsesorMesDto>> ObtenerDetalleImportePorAsesorAsync(DateTime fechaInicio, DateTime fechaFin, string moneda, string asesor, string mes);

        /// <summary>Query 2: Cantidad KG por Asesor / Mes</summary>
        Task<List<DcCantidadKgAsesorMesDto>> ObtenerCantidadKgPorAsesorAsync(DateTime fechaInicio, DateTime fechaFin);

        /// <summary>Query 3: Nro. de Clientes por Asesor / Mes</summary>
        Task<List<DcNroClientesAsesorMesDto>> ObtenerNroClientesPorAsesorAsync(DateTime fechaInicio, DateTime fechaFin);

        /// <summary>Query 3.1: Detalle de Clientes por Asesor / Mes</summary>
        Task<List<DcDetalleClienteAsesorMesDto>> ObtenerDetalleClientesPorAsesorAsync(DateTime fechaInicio, DateTime fechaFin, string moneda, string asesor, string mes);

        /// <summary>Query 4: Top N clientes por Asesor (Kilos e Importe acumulado)</summary>
        Task<List<DcTopClienteAsesorDto>> ObtenerTopClientesPorAsesorAsync(DateTime fechaInicio, DateTime fechaFin, string moneda, int top);
    }
}
