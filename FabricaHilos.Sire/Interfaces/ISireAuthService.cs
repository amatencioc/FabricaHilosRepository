using FabricaHilos.Sire.Models;

namespace FabricaHilos.Sire.Interfaces;

public interface ISireAuthService
{
    Task<AuthToken> GetTokenAsync(CancellationToken cancellationToken = default);
}
