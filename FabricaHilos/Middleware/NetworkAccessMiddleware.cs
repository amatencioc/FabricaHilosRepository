using System.Net;
using System.Net.Sockets;

namespace FabricaHilos.Middleware
{
    /// <summary>
    /// Middleware que restringe el acceso por red.
    /// - Peticiones desde la red interna (LAN): acceso TOTAL a todos los módulos.
    /// - Peticiones desde internet (externa): acceso SOLO a los módulos Seguridad/Inspecciones y Producción.
    /// </summary>
    public class NetworkAccessMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<NetworkAccessMiddleware> _logger;

        // Rutas que SÍ son accesibles desde internet (módulos Seguridad y Producción)
        private static readonly string[] _rutasPermitidas = new[]
        {
            "/account/login",
            "/account/logout",
            "/account/accesodenegado",
            "/seguridad",
            "/produccion",
            "/registropreparatoria",
            "/autoconer",
        };

        // Prefijos de rutas estáticas siempre permitidos
        private static readonly string[] _rutasEstaticasPermitidas = new[]
        {
            "/css/", "/js/", "/lib/", "/images/", "/favicon.ico",
            "/_framework/",
        };

        // Subnets pre-parseadas como (networkUint, maskUint) para comparación de enteros
        private readonly (uint Network, uint Mask)[] _subnets;

        public NetworkAccessMiddleware(
            RequestDelegate next,
            ILogger<NetworkAccessMiddleware> logger,
            IConfiguration configuration)
        {
            _next = next;
            _logger = logger;

            var subnetsConfig = configuration
                .GetSection("RedInterna:Subnets")
                .Get<string[]>() ?? Array.Empty<string>();

            _subnets = subnetsConfig
                .Select(ParsearSubnet)
                .Where(s => s.HasValue)
                .Select(s => s!.Value)
                .ToArray();
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var remoteIp = context.Connection.RemoteIpAddress;
            var path = context.Request.Path.Value ?? "";

            // 1. Siempre permitir archivos estáticos (comparación OrdinalIgnoreCase sin allocations)
            foreach (var prefijo in _rutasEstaticasPermitidas)
            {
                if (path.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase))
                {
                    await _next(context);
                    return;
                }
            }

            // 2. Verificar si es red interna usando subnets pre-parseadas
            bool esRedInterna = EsIpInterna(remoteIp, _subnets);

            if (esRedInterna)
            {
                // Red interna → acceso TOTAL
                await _next(context);
                return;
            }

            // 3. Fuera de la red interna → solo rutas de Seguridad y Producción permitidas
            bool rutaPermitida = false;
            foreach (var ruta in _rutasPermitidas)
            {
                if (path.Equals(ruta, StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith(ruta + "/", StringComparison.OrdinalIgnoreCase))
                {
                    rutaPermitida = true;
                    break;
                }
            }

            if (rutaPermitida)
            {
                _logger.LogInformation(
                    "Acceso externo permitido a ruta permitida: {Path} desde IP: {IP}",
                    path, remoteIp);
                await _next(context);
                return;
            }

            // 4. Acceso denegado para todo lo demás desde internet
            _logger.LogWarning(
                "Acceso BLOQUEADO desde IP externa {IP} intentando acceder a: {Path}",
                remoteIp, path);

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(Pagina403Html());
        }

        private static bool EsIpInterna(IPAddress? remoteIp, (uint Network, uint Mask)[] subnets)
        {
            if (remoteIp == null) return false;

            // Siempre permitir loopback (localhost / desarrollo)
            if (IPAddress.IsLoopback(remoteIp)) return true;


            if (remoteIp.IsIPv4MappedToIPv6)
                remoteIp = remoteIp.MapToIPv4();

            // Solo soportamos IPv4 para subnets internas
            if (remoteIp.AddressFamily != AddressFamily.InterNetwork)
                return false;

            uint ipInt = IpAUint(remoteIp);
            foreach (var (network, mask) in subnets)
            {
                if ((ipInt & mask) == network)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Parsea "10.0.7.0/24" a (networkUint, maskUint). Retorna null si es inválido.
        /// Se llama una sola vez en el constructor.
        /// </summary>
        private static (uint Network, uint Mask)? ParsearSubnet(string subnet)
        {
            try
            {
                var partes = subnet.Split('/');
                if (partes.Length != 2) return null;

                if (!IPAddress.TryParse(partes[0], out var redBase) ||
                    redBase.AddressFamily != AddressFamily.InterNetwork)
                    return null;

                if (!int.TryParse(partes[1], out int prefixLen) || prefixLen < 0 || prefixLen > 32)
                    return null;

                uint mask = prefixLen == 0 ? 0u : ~((1u << (32 - prefixLen)) - 1);
                uint network = IpAUint(redBase) & mask;
                return (network, mask);
            }
            catch
            {
                return null;
            }
        }

        private static uint IpAUint(IPAddress ip)
        {
            var bytes = ip.GetAddressBytes();
            return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
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
                        display: flex;
                        align-items: center;
                        justify-content: center;
                        min-height: 100vh;
                    }
                    .overlay {
                        position: fixed; inset: 0;
                        background: rgba(0,0,0,0.5);
                    }
                    .modal-box {
                        position: relative; z-index: 10;
                        background: #fff;
                        border-radius: 8px;
                        max-width: 460px;
                        width: 90%;
                        box-shadow: 0 8px 32px rgba(0,0,0,0.3);
                        overflow: hidden;
                    }
                    .modal-header {
                        background-color: #dc3545;
                        color: #fff;
                        padding: 16px 20px;
                        font-size: 1.15rem;
                        font-weight: 600;
                    }
                    .modal-body {
                        padding: 20px;
                        color: #333;
                        line-height: 1.6;
                    }
                    .modal-body strong { color: #000; }
                    .modal-footer {
                        padding: 12px 20px 20px;
                    }
                    .btn-success {
                        display: block;
                        width: 100%;
                        padding: 12px;
                        background-color: #198754;
                        color: #fff;
                        text-align: center;
                        text-decoration: none;
                        border: none;
                        border-radius: 6px;
                        font-size: 1rem;
                        font-weight: 500;
                        cursor: pointer;
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
                            Si necesitas registrar una <strong>Inspección de Seguridad</strong> o acceder al módulo de <strong>Producción</strong>,
                                puedes hacerlo desde los módulos habilitados para acceso externo.
                        </p>
                    </div>
                    <div class="modal-footer">
                        <a href="/Seguridad/Inspeccion" class="btn-success">
                            Ir a Seguridad / Inspecciones
                        </a>
                    </div>
                </div>
            </body>
            </html>
            """;
    }
}
