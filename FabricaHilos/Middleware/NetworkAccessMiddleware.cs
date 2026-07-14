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

        // 2. Red interna: usar RemoteIpAddress (conexión TCP real).
        //    IMPORTANTE: NO usar X-Forwarded-For aquí — un atacante externo puede falsificarlo
        //    para suplantar una IP interna y saltarse el bloqueo.
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
            <title>Acceso Restringido</title>
            <style>
                * { margin: 0; padding: 0; box-sizing: border-box; }
                body {
                    background: #0f1923;
                    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
                    display: flex; align-items: center; justify-content: center;
                    min-height: 100vh; padding: 16px;
                }
                .card {
                    background: #1a2535;
                    border: 1px solid #2d3a4a;
                    border-radius: 12px;
                    max-width: 420px; width: 100%;
                    box-shadow: 0 12px 40px rgba(0,0,0,0.5);
                    overflow: hidden;
                }
                .card-top {
                    background: linear-gradient(135deg, #b91c1c, #991b1b);
                    padding: 20px 24px;
                    display: flex; align-items: center; gap: 12px;
                }
                .lock-icon {
                    width: 40px; height: 40px;
                    background: rgba(255,255,255,0.15);
                    border-radius: 50%;
                    display: flex; align-items: center; justify-content: center;
                    font-size: 20px; flex-shrink: 0;
                }
                .card-top-text h1 {
                    color: #fff; font-size: 1.1rem; font-weight: 700; margin: 0;
                }
                .card-top-text p {
                    color: rgba(255,255,255,0.75); font-size: 0.78rem; margin: 2px 0 0;
                }
                .card-body { padding: 24px; }
                .info-row {
                    display: flex; gap: 10px; align-items: flex-start;
                    background: #243040; border-radius: 8px;
                    padding: 14px 16px; margin-bottom: 16px;
                }
                .info-row .icon { font-size: 1.1rem; flex-shrink: 0; margin-top: 1px; }
                .info-row p { color: #a0aec0; font-size: 0.83rem; line-height: 1.55; margin: 0; }
                .info-row p strong { color: #e2e8f0; }
                .btn-primary {
                    display: block; width: 100%; padding: 13px;
                    background: #2563eb; color: #fff;
                    text-align: center; text-decoration: none;
                    border: none; border-radius: 8px;
                    font-size: 0.9rem; font-weight: 600; cursor: pointer;
                    transition: background .15s;
                }
                .btn-primary:hover { background: #1d4ed8; }
            </style>
        </head>
        <body>
            <div class="card">
                <div class="card-top">
                    <div class="lock-icon">🔒</div>
                    <div class="card-top-text">
                        <h1>Acceso Restringido</h1>
                        <p>Este módulo no está disponible externamente</p>
                    </div>
                </div>
                <div class="card-body">
                    <div class="info-row">
                        <span class="icon">ℹ️</span>
                        <p>
                            El módulo al que intentas acceder está disponible
                            <strong>únicamente dentro de la red interna</strong>
                            de la empresa.<br><br>
                            Desde acceso externo solo puedes utilizar los
                            <strong>módulos habilitados para tu perfil</strong>.
                        </p>
                    </div>
                    <a href="/Account/LogoutExterno" class="btn-primary">Ir al inicio de sesión</a>
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
            rutasExternas:  RouteGroups.Expandir(opts.RutasExternasPermitidas).ToArray(),
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
