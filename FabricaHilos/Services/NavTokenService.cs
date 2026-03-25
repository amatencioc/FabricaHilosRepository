using Microsoft.AspNetCore.DataProtection;
using System.Text.Json;

namespace FabricaHilos.Services;

public interface INavTokenService
{
    string Protect(Dictionary<string, string?> values);
    bool TryUnprotect(string token, out Dictionary<string, string?> values);
}

public class NavTokenService : INavTokenService
{
    private readonly IDataProtector _protector;

    public NavTokenService(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("SgcNavigation.v1");
    }

    public string Protect(Dictionary<string, string?> values)
    {
        var json = JsonSerializer.Serialize(values);
        return _protector.Protect(json);
    }

    public bool TryUnprotect(string token, out Dictionary<string, string?> values)
    {
        try
        {
            var json = _protector.Unprotect(token);
            values   = JsonSerializer.Deserialize<Dictionary<string, string?>>(json)
                       ?? new Dictionary<string, string?>();
            return true;
        }
        catch
        {
            values = new Dictionary<string, string?>();
            return false;
        }
    }
}
