using FabricaHilos.Models.Sistemas;

namespace FabricaHilos.Services.Sistemas
{
    public interface IDesarrolloComplejidadService
    {
        /// <summary>
        /// Ejecuta ind_desarrollo_complejidad.sql y devuelve todos los datos del
        /// dashboard de Complejidad agrupados por nivel BAJA/MEDIA/ALTA (PRIORIDAD).
        /// </summary>
        Task<DevCompDashboardDto> ObtenerDashboardAsync(DateTime fechaInicio, DateTime fechaFin);
    }
}
