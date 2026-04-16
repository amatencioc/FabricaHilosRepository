using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FabricaHilos.Controllers
{
    /// <summary>
    /// Controlador base para todos los módulos que requieren sesión Oracle activa.
    /// Centraliza la verificación de sesión y el redirect al login, evitando
    /// código duplicado en cada controlador hijo.
    /// </summary>
    public abstract class OracleBaseController : Controller
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            base.OnActionExecuting(context);

            if (string.IsNullOrEmpty(HttpContext.Session.GetString("OracleUser")))
            {
                // Limpiar sesión para evitar estado inconsistente
                // (cookie Identity válida pero sesión Oracle perdida por reinicio del servidor)
                HttpContext.Session.Clear();

                TempData["Warning"] = "Su sesión ha expirado. Por favor, inicie sesión nuevamente.";

                // En POST no usar la URL actual para evitar reenvío accidental del formulario
                string? returnUrl = null;
                if (HttpContext.Request.Method == HttpMethods.Get)
                    returnUrl = Request.Path + Request.QueryString;

                context.Result = RedirectToAction("Login", "Account",
                    returnUrl != null ? new { returnUrl } : null);
            }
        }
    }
}
