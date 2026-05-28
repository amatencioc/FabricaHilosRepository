using FabricaHilos.Sire.Interfaces;
using FabricaHilos.Sire.Models;

namespace FabricaHilos.Sire.Services.Mock;

public sealed class SireAuthServiceMock : ISireAuthService
{
    public Task<AuthToken> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new AuthToken
        {
            AccessToken = "mock-token-sire",
            TokenType = "Bearer",
            ExpiresIn = 3600,
            Scope = "https://api.sunat.gob.pe/v1/contribuyente/gem",
            ExpiraEnUtc = DateTime.UtcNow.AddHours(1)
        });
    }
}
