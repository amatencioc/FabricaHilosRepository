using System.Net;

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
        private readonly IConfiguration _configuration;

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

        public NetworkAccessMiddleware(
            RequestDelegate next,
            ILogger<NetworkAccessMiddleware> logger,
            IConfiguration configuration)
        {
            _next = next;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var remoteIp = context.Connection.RemoteIpAddress;
            var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

            // 1. Siempre permitir archivos estáticos
            if (_rutasEstaticasPermitidas.Any(p => path.StartsWith(p)))
            {
                await _next(context);
                return;
            }

            // 2. Obtener las redes internas configuradas en appsettings.json
            var redesInternas = _configuration
                .GetSection("RedInterna:Subnets")
                .Get<string[]>() ?? Array.Empty<string>();

            bool esRedInterna = EsIpInterna(remoteIp, redesInternas);

            if (esRedInterna)
            {
                // Red interna → acceso TOTAL
                await _next(context);
                return;
            }

            // 3. Fuera de la red interna → solo rutas de Seguridad y Producción permitidas
            bool rutaPermitida = _rutasPermitidas.Any(r =>
                path == r || path.StartsWith(r + "/"));

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

        private static bool EsIpInterna(IPAddress? remoteIp, string[] subnets)
        {
            if (remoteIp == null) return false;

            // Siempre permitir loopback (localhost / desarrollo)
            if (IPAddress.IsLoopback(remoteIp)) return true;

            // Si viene como IPv4 mapeado en IPv6 (::ffff:10.x.x.x), extraer IPv4
            if (remoteIp.IsIPv4MappedToIPv6)
                remoteIp = remoteIp.MapToIPv4();

            foreach (var subnet in subnets)
            {
                if (EstaEnSubnet(remoteIp, subnet))
                    return true;
            }

            return false;
        }

        private static bool EstaEnSubnet(IPAddress ip, string subnet)
        {
            try
            {
                // Formato esperado: "10.0.7.0/24"
                var partes = subnet.Split('/');
                if (partes.Length != 2) return false;

                var redBase = IPAddress.Parse(partes[0]);
                int prefixLen = int.Parse(partes[1]);

                var ipBytes = ip.GetAddressBytes();
                var redBytes = redBase.GetAddressBytes();

                if (ipBytes.Length != redBytes.Length) return false;

                int bytesCompletos = prefixLen / 8;
                int bitsSobrantes = prefixLen % 8;

                for (int i = 0; i < bytesCompletos; i++)
                    if (ipBytes[i] != redBytes[i]) return false;

                if (bitsSobrantes > 0)
                {
                    int mascara = 0xFF << (8 - bitsSobrantes);
                    if ((ipBytes[bytesCompletos] & mascara) != (redBytes[bytesCompletos] & mascara))
                        return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
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
