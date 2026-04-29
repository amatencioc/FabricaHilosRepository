using FabricaHilos.Models.Ventas;

namespace FabricaHilos.Services.Ventas
{
    public interface IIndicadorComercialMaestroService
    {
        /// <summary>
        /// Carga todos los datos en un único viaje Oracle y devuelve importe, KG y clientes por asesor/mes.
        /// </summary>
        Task<IcmTodosDto> ObtenerTodosAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda);

        Task<List<IcmImporteAsesorMesDto>> ObtenerImportePorAsesorMesAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda);

        Task<List<IcmKgAsesorMesDto>> ObtenerKgPorAsesorMesAsync(
            DateTime fechaInicio, DateTime fechaFin);

        Task<List<IcmClientesAsesorMesDto>> ObtenerClientesPorAsesorMesAsync(
            DateTime fechaInicio, DateTime fechaFin);
    }
}
