using FabricaHilos.Models.CreditosCobranza;

namespace FabricaHilos.Services.CreditosCobranza;

public interface IValorizadoNoVendidoService
{
    Task<List<ValorizadoNoVendidoDto>> ObtenerValorizadoNoVendidoAsync(DateTime fechaInicio, DateTime fechaFin);
}
