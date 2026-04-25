using FabricaHilos.Models.CreditosCobranza;

namespace FabricaHilos.Services.CreditosCobranza;

public interface INivelMorosidadService
{
    Task<List<NivelMorosidadDto>> ObtenerNivelMorosidadAsync(DateTime fechaInicio, DateTime fechaFin);
}
