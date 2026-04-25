using FabricaHilos.Models.CreditosCobranza;

namespace FabricaHilos.Services.CreditosCobranza;

public interface INivelTiempoService
{
    Task<List<NivelTiempoDto>> ObtenerNivelTiempoAsync(DateTime fechaInicio, DateTime fechaFin);
}
