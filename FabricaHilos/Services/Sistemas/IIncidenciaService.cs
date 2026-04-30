using FabricaHilos.Models.Sistemas;

namespace FabricaHilos.Services.Sistemas
{
    public interface IIncidenciaService
    {
        /// <summary>
        /// Ejecuta ind_incidencias.sql y devuelve todos los datos agregados
        /// del dashboard de Incidencias en un solo objeto.
        /// </summary>
        Task<IncDashboardDto> ObtenerDashboardAsync(DateTime fechaInicio, DateTime fechaFin);
    }
}
