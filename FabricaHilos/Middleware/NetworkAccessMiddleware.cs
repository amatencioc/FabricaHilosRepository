using System.Net;

namespace FabricaHilos.Middleware
{
    /// <summary>
    /// Middleware que restringe el acceso por red.
    /// - Peticiones desde la red interna (LAN): acceso TOTAL a todos los módulos.
    /// - Peticiones desde internet (externa): acceso SOLO al módulo Seguridad/Inspecciones.
    /// </summary>
    public class NetworkAccessMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<NetworkAccessMiddleware> _logger;
        private readonly IConfiguration _configuration;

        // Rutas que SÍ son accesibles desde internet (módulo Seguridad)
        private static readonly string[] _rutasPermitidas = new[]
        {
            "/account/login",
            "/account/logout",
            "/account/accesodenegado",
            "/seguridad",
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

            // 3. Fuera de la red interna → solo rutas de Seguridad permitidas
            bool rutaPermitida = _rutasPermitidas.Any(r =>
                path == r || path.StartsWith(r + "/"));

            if (rutaPermitida)
            {
                _logger.LogInformation(
                    "Acceso externo permitido a ruta de Seguridad: {Path} desde IP: {IP}",
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
                <link rel="stylesheet"
                      href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css"
                      integrity="sha384-QWTKZyjpPEjISv5WaRU9OFeRpok6YctnYmDr5pNlyT2bRjXh0JMhjY6hW+ALEwIH"
                      crossorigin="anonymous" />
                <style>
                    body { background-color: #1a3a2e; }
                </style>
            </head>
            <body>
                <!-- Modal -->
                <div class="modal fade" id="modalAccesoRestringido" tabindex="-1"
                     aria-labelledby="modalAccesoRestringidoLabel" aria-modal="true" role="dialog"
                     data-bs-backdrop="static" data-bs-keyboard="false">
                    <div class="modal-dialog modal-dialog-centered">
                        <div class="modal-content">
                            <div class="modal-header bg-danger text-white">
                                <h5 class="modal-title" id="modalAccesoRestringidoLabel">
                                    🔒 Acceso Restringido
                                </h5>
                            </div>
                            <div class="modal-body">
                                <p>
                                    El módulo que intentas acceder <strong>solo está disponible dentro de la
                                    red interna</strong> de La Colonial - Fábrica de Hilos S.A.
                                </p>
                                <p>
                                    Si necesitas registrar una <strong>Inspección de Seguridad</strong>,
                                    puedes hacerlo desde el módulo habilitado para acceso externo.
                                </p>
                            </div>
                            <div class="modal-footer">
                                <a href="/Account/Login" class="btn btn-success w-100">
                                    Ir a Seguridad / Inspecciones
                                </a>
                            </div>
                        </div>
                    </div>
                </div>

                <script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/js/bootstrap.bundle.min.js"
                        integrity="sha384-YvpcrYf0tY3lHB60NNkmXc4s9bIOgUxi8T/jzmRZ5+fKxQ2bBT5rz5sT9bqcjxE"
                        crossorigin="anonymous"></script>
                <script>
                    document.addEventListener('DOMContentLoaded', function () {
                        var modal = new bootstrap.Modal(
                            document.getElementById('modalAccesoRestringido'));
                        modal.show();
                    });
                </script>
            </body>
            </html>
            """;
    }
}
