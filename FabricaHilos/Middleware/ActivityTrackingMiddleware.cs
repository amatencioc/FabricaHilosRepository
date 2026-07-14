using System.Security.Claims;
using FabricaHilos.Services.Sistemas;

namespace FabricaHilos.Middleware;

/// <summary>
/// Registra en UsuarioActivoStore la actividad de cada request autenticado.
/// Captura tipo de acceso (Interno/Externo/Movil), navegador, OS y historial de paginas.
/// </summary>
public sealed class ActivityTrackingMiddleware(RequestDelegate next)
{
    private static readonly string[] _ignorar =
        ["/favicon", "/_vs", "/_framework", "/health", "/lib/", "/css/", "/js/", "/images/",
         "/sistemas/usuariosactivos/datos",
         "/sistemas/usuariosactivos/heartbeat",
         "/sistemas/usuariosactivos/historial",
         "/sistemas/usuariosactivos/resumen",
         "/.well-known", "/browserlink", "/signin-", "/signout-", "/__browser"];

    private static readonly HashSet<string> _modulosValidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "sistemas", "contabilidad", "logistica", "recursoshumanos",
        "produccion", "comercial", "finanzas", "seguridad", "home", "account",
        "ventas", "saludocupacional", "registropreparatoria", "autoconer",
        "sgc", "planeamiento", "creditoscobranza", "facturacion"
    };

    // Prefijos de red interna (LAN)
    private static readonly string[] _redInterna =
        ["10.", "192.168.", "172.16.", "172.17.", "172.18.", "172.19.",
         "172.20.", "172.21.", "172.22.", "172.23.", "172.24.", "172.25.",
         "172.26.", "172.27.", "172.28.", "172.29.", "172.30.", "172.31.",
         "127.", "::1", "localhost"];

    public async Task InvokeAsync(HttpContext ctx, UsuarioActivoStore store)
    {
        var path = ctx.Request.Path.Value ?? "";

        if (!_ignorar.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase))
            && ctx.User.Identity?.IsAuthenticated == true)
        {
            var usuario = ctx.User.FindFirst("OracleUser")?.Value;
            var nombre  = ctx.User.FindFirst("OracleNombre")?.Value;

            if (string.IsNullOrEmpty(usuario))
            {
                await ctx.Session.LoadAsync();
                usuario = ctx.Session.GetString("OracleUser");
                nombre  = ctx.Session.GetString("OracleNombre");
            }

            usuario ??= ctx.User.Identity?.Name ?? "";
            nombre  ??= usuario;

            if (!string.IsNullOrEmpty(usuario))
            {
                var modulo = ExtraerModulo(path);
                if (_modulosValidos.Contains(modulo.TrimStart('/')))
                {
                    // IP para mostrar en el dashboard (puede incluir X-Forwarded-For de proxy legítimo)
                    var ipDisplay    = ObtenerIp(ctx);
                    // IP real de la conexión TCP (no falsificable) para clasificar Interno/Externo
                    var ipTcp        = ctx.Connection.RemoteIpAddress?.ToString() ?? "";
                    var ua           = ctx.Request.Headers.UserAgent.ToString();
                    var tipoAcceso   = ClasificarAcceso(ipTcp, ua);
                    var navegador    = ExtraerNavegador(ua);
                    var dispositivoOs = ExtraerOS(ua);

                    store.Registrar(usuario, nombre, modulo, path.ToLowerInvariant(),
                                    ipDisplay, tipoAcceso, navegador, dispositivoOs);
                }
            }
        }

        await next(ctx);
    }

    private static string ObtenerIp(HttpContext ctx)
    {
        // Respetar X-Forwarded-For si viene de proxy/IIS ARR
        var forwarded = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwarded))
            return forwarded.Split(',')[0].Trim();
        return ctx.Connection.RemoteIpAddress?.ToString() ?? "";
    }

    private static string ClasificarAcceso(string ip, string ua)
    {
        var esMovil = ua.Contains("Mobile", StringComparison.OrdinalIgnoreCase)
                   || ua.Contains("Android", StringComparison.OrdinalIgnoreCase)
                   || ua.Contains("iPhone", StringComparison.OrdinalIgnoreCase);
        if (esMovil) return "Movil";

        var esInterno = _redInterna.Any(p => ip.StartsWith(p, StringComparison.OrdinalIgnoreCase));
        return esInterno ? "Interno" : "Externo";
    }

    private static string ExtraerNavegador(string ua)
    {
        if (ua.Contains("Edg/",     StringComparison.OrdinalIgnoreCase)) return "Edge";
        if (ua.Contains("Chrome/",  StringComparison.OrdinalIgnoreCase)) return "Chrome";
        if (ua.Contains("Firefox/", StringComparison.OrdinalIgnoreCase)) return "Firefox";
        if (ua.Contains("Safari/",  StringComparison.OrdinalIgnoreCase)) return "Safari";
        if (ua.Contains("MSIE",     StringComparison.OrdinalIgnoreCase)
         || ua.Contains("Trident/", StringComparison.OrdinalIgnoreCase)) return "IE";
        return "Otro";
    }

    private static string ExtraerOS(string ua)
    {
        if (ua.Contains("Android",  StringComparison.OrdinalIgnoreCase)) return "Android";
        if (ua.Contains("iPhone",   StringComparison.OrdinalIgnoreCase)) return "iOS";
        if (ua.Contains("iPad",     StringComparison.OrdinalIgnoreCase)) return "iOS";
        if (ua.Contains("Windows",  StringComparison.OrdinalIgnoreCase)) return "Windows";
        if (ua.Contains("Macintosh",StringComparison.OrdinalIgnoreCase)) return "Mac";
        if (ua.Contains("Linux",    StringComparison.OrdinalIgnoreCase)) return "Linux";
        return "Otro";
    }

    private static string ExtraerModulo(string path)
    {
        var partes = path.Trim('/').Split('/');
        return partes.Length > 0 ? $"/{partes[0].ToLowerInvariant()}" : "/";
    }
}
