using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FabricaHilos.Filters
{
    /// <summary>
    /// Filtro de autorización personalizado para el módulo VENTAS.
    /// Solo permite acceso a:
    /// - Usuario específico: COSTOS2
    /// - Usuarios que comiencen con: VENT (ejemplo: VENT001, VENTADMIN, etc.)
    /// </summary>
    public class VentasAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            // Verificar si el usuario está autenticado
            if (!context.HttpContext.User.Identity?.IsAuthenticated ?? true)
            {
                context.Result = new RedirectToActionResult("Login", "Account", null);
                return;
            }

            // Obtener nombre de usuario desde la sesión de Oracle (más confiable)
            var oracleUser = context.HttpContext.Session.GetString("OracleUser");

            // Si no hay sesión Oracle, obtener desde Identity
            if (string.IsNullOrEmpty(oracleUser))
            {
                oracleUser = context.HttpContext.User.Identity?.Name;
            }

            // Si aún no hay usuario, denegar acceso
            if (string.IsNullOrEmpty(oracleUser))
            {
                context.Result = new ForbidResult();
                return;
            }

            // Convertir a mayúsculas para comparación uniforme
            var usuario = oracleUser.ToUpperInvariant();

            // Validar si el usuario tiene permiso de acceso
            bool tieneAcceso = usuario == "COSTOS2" || usuario.StartsWith("VENT");

            if (!tieneAcceso)
            {
                // Redirigir a página de acceso denegado
                context.Result = new RedirectToActionResult("AccesoDenegado", "Account", null);
            }
        }
    }
}
