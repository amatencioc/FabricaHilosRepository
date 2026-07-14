using FabricaHilos.Config;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Sockets;

namespace FabricaHilos.Services;

/// <summary>
/// Expone información sobre el tipo de acceso de la petición actual
/// (interna vs. externa) y la lista de rutas permitidas externamente.
/// Inyectable en controllers, views (via @inject) y otros servicios.
/// </summary>
public interface IRedInternaService
{
    /// <summary>True cuando la petición llega desde fuera de las subnets internas.</summary>
    bool EsAccesoExterno { get; }

    /// <summary>Rutas raíz accesibles desde internet según appsettings.</summary>
    IReadOnlyList<string> RutasExternasPermitidas { get; }

    /// <summary>True si la ruta dada está accesible externamente.</summary>
    bool RutaEsAccesibleExternamente(string ruta);
}

public class RedInternaService : IRedInternaService
{
    private readonly IHttpContextAccessor _ctx;
    private readonly RedInternaOptions    _opts;

    public RedInternaService(
        IHttpContextAccessor httpContextAccessor,
        IOptionsSnapshot<RedInternaOptions> opts)
    {
        _ctx  = httpContextAccessor;
        _opts = opts.Value;
    }

    public bool EsAccesoExterno =>
        !EsIpInterna(_ctx.HttpContext?.Connection.RemoteIpAddress);

    public IReadOnlyList<string> RutasExternasPermitidas =>
        _opts.RutasExternasPermitidas;

    public bool RutaEsAccesibleExternamente(string ruta) =>
        RouteGroups.Expandir(_opts.RutasExternasPermitidas).Any(r =>
            ruta.Equals(r, StringComparison.OrdinalIgnoreCase) ||
            ruta.StartsWith(r + "/", StringComparison.OrdinalIgnoreCase));

    // ── IP helpers ────────────────────────────────────────────────────────────

    private bool EsIpInterna(IPAddress? ip)
    {
        if (ip is null) return false;
        if (IPAddress.IsLoopback(ip)) return true;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (ip.AddressFamily != AddressFamily.InterNetwork) return false;

        uint ipInt = IpAUint(ip);
        foreach (var subnet in _opts.Subnets)
        {
            var parsed = ParsearSubnet(subnet);
            if (parsed.HasValue && (ipInt & parsed.Value.Mask) == parsed.Value.Network)
                return true;
        }
        return false;
    }

    private static (uint Network, uint Mask)? ParsearSubnet(string subnet)
    {
        try
        {
            var partes = subnet.Split('/');
            if (partes.Length != 2) return null;
            if (!IPAddress.TryParse(partes[0], out var redBase) ||
                redBase.AddressFamily != AddressFamily.InterNetwork) return null;
            if (!int.TryParse(partes[1], out int prefix) || prefix < 0 || prefix > 32) return null;
            uint mask    = prefix == 0 ? 0u : ~((1u << (32 - prefix)) - 1);
            uint network = IpAUint(redBase) & mask;
            return (network, mask);
        }
        catch { return null; }
    }

    private static uint IpAUint(IPAddress ip)
    {
        var b = ip.GetAddressBytes();
        return ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];
    }
}
