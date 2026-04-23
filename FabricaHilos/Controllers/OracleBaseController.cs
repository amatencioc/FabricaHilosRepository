using FabricaHilos.Helpers;
using FabricaHilos.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FabricaHilos.Controllers
{
    /// <summary>
    /// Controlador base para todos los módulos que requieren sesión Oracle activa.
    /// Centraliza:
    ///   - Verificación de sesión Oracle y redirect al login si expiró.
    ///   - EnsureNetworkShare(): conexión a recurso de red UNC antes de leer PDFs.
    ///   - CodEmpresaAquarius: código de empresa para sistemas externos.
    /// </summary>
    public abstract class OracleBaseController : Controller
    {
        // Inyectado por el contenedor DI mediante property injection ([FromServices]).
        // Los controllers hijos NO necesitan declararlo ni pasarlo al constructor.
        [FromServices]
        public IConfiguration Configuration { get; set; } = null!;

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

        /// <summary>
        /// CodEmpresa para sistemas externos (Aquarius, etc.) según la empresa activa en sesión.
        /// Fuente única: OracleServiceBase.GetCodEmpresaAquarius — al agregar una empresa solo
        /// se actualiza el diccionario en OracleServiceBase.
        /// </summary>
        protected string CodEmpresaAquarius
        {
            get
            {
                var connKey = HttpContext.Session.GetString("EmpresaConexion") ?? "LaColonialConnection";
                return OracleServiceBase.GetCodEmpresaAquarius(connKey);
            }
        }

        /// <summary>
        /// Conecta al recurso de red UNC (si está configurado) antes de leer archivos PDF.
        /// Usa las credenciales definidas en appsettings.json → sección "NetworkShare".
        /// Disponible para todos los controllers que hereden de OracleBaseController.
        /// </summary>
        protected void EnsureNetworkShare(string filePath)
        {
            var username = Configuration["NetworkShare:Username"];
            if (string.IsNullOrEmpty(username)) return;
            try
            {
                NetworkShareHelper.Connect(
                    filePath,
                    username,
                    Configuration["NetworkShare:Password"],
                    Configuration["NetworkShare:Domain"]);
            }
            catch (Exception ex)
            {
                // Log no disponible aquí directamente; el controller hijo puede capturar si necesita
                _ = ex; // suprimir warning — el caller decide si loguea
            }
        }
    }
}
