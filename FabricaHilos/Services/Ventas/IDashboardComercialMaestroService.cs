using FabricaHilos.Models.Ventas;

namespace FabricaHilos.Services.Ventas
{
    public interface IDashboardComercialMaestroService
    {
        /// <summary>
        /// Ejecuta el query MaestroAsesor y devuelve todos los datos agregados del dashboard en un solo objeto.
        /// </summary>
        Task<DcmDashboardDto> ObtenerDashboardAsync(DateTime fechaInicio, DateTime fechaFin, string moneda, int top = 3);

        /// <summary>
        /// Lista de clientes de un asesor específico (filtrado en memoria desde los datos ya cargados).
        /// </summary>
        Task<List<DcmClienteMaestroDto>> ObtenerClientesPorAsesorAsync(DateTime fechaInicio, DateTime fechaFin, string moneda, string asesor);

        /// <summary>
        /// Diagnóstico: devuelve el número de filas que retorna el query para un rango de fechas.
        /// </summary>
        Task<int> DiagnosticoFilasAsync(DateTime fechaInicio, DateTime fechaFin);
    }
}
