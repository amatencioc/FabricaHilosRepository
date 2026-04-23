using FabricaHilos.Models.Ventas;

namespace FabricaHilos.Services.Ventas
{
    public interface IDashboardComercialService
    {
        /// <summary>
        /// Ejecuta el QueryPrincipal y devuelve todos los datos agregados del dashboard en un solo objeto.
        /// </summary>
        Task<DcDashboardDto> ObtenerDashboardAsync(DateTime fechaInicio, DateTime fechaFin, string moneda, int top = 3);

        /// <summary>
        /// Lista de clientes de un asesor específico con importe y KG (para detalle desde pie chart).
        /// Derivado de los datos ya cargados, pero también disponible como endpoint independiente.
        /// </summary>
        Task<List<DcClienteImporteAsesorDto>> ObtenerClientesPorAsesorAsync(DateTime fechaInicio, DateTime fechaFin, string moneda, string asesor);

        /// <summary>
        /// Diagnóstico: devuelve el número de filas que retorna el QueryPrincipal para un rango de fechas.
        /// </summary>
        Task<int> DiagnosticoFilasAsync(DateTime fechaInicio, DateTime fechaFin);
    }
}
