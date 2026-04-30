using FabricaHilos.Models.Sistemas;

namespace FabricaHilos.Services.Sistemas
{
    public interface IDesarrolloService
    {
        /// <summary>
        /// Ejecuta ind_desarrollo.sql y devuelve todos los datos agregados
        /// del dashboard de Desarrollo en un solo objeto.
        /// </summary>
        Task<DevDashboardDto> ObtenerDashboardAsync(DateTime fechaInicio, DateTime fechaFin);
    }
}
