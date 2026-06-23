using FabricaHilos.Config;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Sockets;

namespace FabricaHilos.Middleware;

/// <summary>
/// Middleware que restringe el acceso por red.
/// - Red interna (subnets configuradas): acceso TOTAL.
/// - Internet / acceso externo: solo las rutas listadas en
///   RedInterna:RutasExternasPermitidas son accesibles.
///
/// Soporta hot-reload: editar appsettings.json aplica los cambios
/// sin reiniciar la aplicación.
/// </summary>
public class NetworkAccessMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<NetworkAccessMiddleware> _logger;

    // Estado mutable recalculado al detectar cambio en la configuración
    private volatile ComputedState _state;

    public NetworkAccessMiddleware(
        RequestDelegate next,
        ILogger<NetworkAccessMiddleware> logger,
        IOptionsMonitor<RedInternaOptions> optionsMonitor)
    {
        _next   = next;
        _logger = logger;
        _state  = ComputedState.Build(optionsMonitor.CurrentValue);

        // Recalcular estado cuando appsettings.json cambie en caliente
        optionsMonitor.OnChange(opts =>
        {
            _state = ComputedState.Build(opts);
            _logger.LogInformation(
                "[NetworkAccessMiddleware] Configuración recargada: {SubnetCount} subnet(s), " +
                "{ExtCount} ruta(s) externas, {StaticCount} ruta(s) estáticas.",
                _state.Subnets.Length,
                _state.RutasExternas.Length,
                _state.RutasEstaticas.Length);
        });
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path  = context.Request.Path.Value ?? "";
        var state = _state; // snapshot thread-safe

        // 1. Archivos estáticos: siempre permitidos
        foreach (var prefijo in state.RutasEstaticas)
        {
            if (path.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }
        }

        // 2. Red interna: acceso total
        if (EsIpInterna(context.Connection.RemoteIpAddress, state.Subnets))
        {
            await _next(context);
            return;
        }

        // 3. Internet: solo rutas externas permitidas
        foreach (var ruta in state.RutasExternas)
        {
            if (path.Equals(ruta, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(ruta + "/", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation(
                    "Acceso externo permitido: {Path} desde {IP}",
                    path, context.Connection.RemoteIpAddress);
                await _next(context);
                return;
            }
        }

        // 4. Bloqueado
        _logger.LogWarning(
            "Acceso BLOQUEADO desde IP externa {IP} → {Path}",
            context.Connection.RemoteIpAddress, path);

        context.Response.StatusCode  = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(Pagina403Html());
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static bool EsIpInterna(IPAddress? ip, (uint Network, uint Mask)[] subnets)
    {
        if (ip is null) return false;
        if (IPAddress.IsLoopback(ip)) return true;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (ip.AddressFamily != AddressFamily.InterNetwork) return false;

        uint ipInt = IpAUint(ip);
        foreach (var (network, mask) in subnets)
            if ((ipInt & mask) == network) return true;

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

    private static string Pagina403Html() => """
        <!DOCTYPE html>
        <html lang="es">
        <head>
            <meta charset="UTF-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1.0" />
            <title>Acceso Restringido – La Colonial</title>
            <style>
                * { margin: 0; padding: 0; box-sizing: border-box; }
                body {
                    background-color: #1a3a2e;
                    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
                    display: flex; align-items: center; justify-content: center;
                    min-height: 100vh;
                }
                .overlay { position: fixed; inset: 0; background: rgba(0,0,0,0.5); }
                .modal-box {
                    position: relative; z-index: 10; background: #fff;
                    border-radius: 8px; max-width: 460px; width: 90%;
                    box-shadow: 0 8px 32px rgba(0,0,0,0.3); overflow: hidden;
                }
                .modal-header {
                    background-color: #dc3545; color: #fff;
                    padding: 16px 20px; font-size: 1.15rem; font-weight: 600;
                }
                .modal-body { padding: 20px; color: #333; line-height: 1.6; }
                .modal-body strong { color: #000; }
                .modal-footer { padding: 12px 20px 20px; }
                .btn-success {
                    display: block; width: 100%; padding: 12px;
                    background-color: #198754; color: #fff;
                    text-align: center; text-decoration: none;
                    border: none; border-radius: 6px;
                    font-size: 1rem; font-weight: 500; cursor: pointer;
                }
                .btn-success:hover { background-color: #157347; }
            </style>
        </head>
        <body>
            <div class="overlay"></div>
            <div class="modal-box">
                <div class="modal-header">🔒 Acceso Restringido</div>
                <div class="modal-body">
                    <p>
                        El módulo que intentas acceder <strong>solo está disponible dentro de la
                        red interna</strong> de La Colonial - Fábrica de Hilos S.A.
                    </p>
                    <p style="margin-top:12px;">
                        Si necesitas acceder a un módulo habilitado para acceso externo,
                        utiliza los enlaces disponibles.
                    </p>
                </div>
                <div class="modal-footer">
                    <a href="/Account/Login" class="btn-success">Ir al inicio de sesión</a>
                </div>
            </div>
        </body>
        </html>
        """;

    // ── Estado pre-computado (inmutable, intercambiado atómicamente) ──────────

    private sealed class ComputedState
    {
        internal readonly (uint Network, uint Mask)[] Subnets;
        internal readonly string[] RutasExternas;
        internal readonly string[] RutasEstaticas;

        private ComputedState(
            (uint Network, uint Mask)[] subnets,
            string[] rutasExternas,
            string[] rutasEstaticas)
        {
            Subnets       = subnets;
            RutasExternas = rutasExternas;
            RutasEstaticas = rutasEstaticas;
        }

        internal static ComputedState Build(RedInternaOptions opts) => new(
            subnets: opts.Subnets
                .Select(ParsearSubnet)
                .Where(s => s.HasValue)
                .Select(s => s!.Value)
                .ToArray(),
            rutasExternas:  opts.RutasExternasPermitidas,
            rutasEstaticas: opts.RutasEstaticasPermitidas);

        // Referencia local al helper estático del middleware padre
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
}
