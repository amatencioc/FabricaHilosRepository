using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FabricaHilos.Filters
{
    /// <summary>
    /// Filtro de autorización para el módulo VENTAS.
    /// Los usuarios y prefijos permitidos se configuran en appsettings.json → VentasAcceso.
    /// </summary>
    public class VentasAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            if (!context.HttpContext.User.Identity?.IsAuthenticated ?? true)
            {
                context.Result = new RedirectToActionResult("Login", "Account", null);
                return;
            }

            if (context.HttpContext.User.IsInRole("Admin"))
                return;

            var oracleUser = context.HttpContext.Session.GetString("OracleUser")
                             ?? context.HttpContext.User.Identity?.Name;

            if (string.IsNullOrEmpty(oracleUser))
            {
                context.Result = new ForbidResult();
                return;
            }

            var usuario = oracleUser.ToUpperInvariant();

            // Leer reglas de acceso desde configuración (sin hardcodear usuarios en código)
            var config = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var usuariosExactos = config.GetSection("VentasAcceso:UsuariosExactos").Get<string[]>() ?? [];
            var prefijos        = config.GetSection("VentasAcceso:Prefijos").Get<string[]>() ?? [];

            bool tieneAcceso = usuariosExactos.Any(u => string.Equals(u, usuario, StringComparison.OrdinalIgnoreCase))
                            || prefijos.Any(p => usuario.StartsWith(p, StringComparison.OrdinalIgnoreCase));

            if (!tieneAcceso)
                context.Result = new RedirectToActionResult("AccesoDenegado", "Account", null);
        }
    }
}
