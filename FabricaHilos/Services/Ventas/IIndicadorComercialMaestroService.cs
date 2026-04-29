using FabricaHilos.Models.Ventas;

namespace FabricaHilos.Services.Ventas
{
    public interface IIndicadorComercialMaestroService
    {
        /// <summary>
        /// Importe neto por Asesor / Mes usando CLIENTES.VENDEDOR (misma lógica que DashboardComercialMaestro).
        /// </summary>
        Task<List<IcmImporteAsesorMesDto>> ObtenerImportePorAsesorMesAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda);

        /// <summary>
        /// KG vendidos por Asesor / Mes usando CLIENTES.VENDEDOR.
        /// </summary>
        Task<List<IcmKgAsesorMesDto>> ObtenerKgPorAsesorMesAsync(
            DateTime fechaInicio, DateTime fechaFin);

        /// <summary>
        /// Nro. de clientes distintos por Asesor / Mes usando CLIENTES.VENDEDOR.
        /// </summary>
        Task<List<IcmClientesAsesorMesDto>> ObtenerClientesPorAsesorMesAsync(
            DateTime fechaInicio, DateTime fechaFin);
    }
}
