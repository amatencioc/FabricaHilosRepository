using FabricaHilos.Models.Sistemas;

namespace FabricaHilos.Services.Sistemas
{
    public interface ISeguimientoDevService
    {
        Task<SdDashboardDto> ObtenerDashboardAsync(DateTime fechaInicio, DateTime fechaFin, string? responsable = null, string? tipoMotivo = null);
    }
}
