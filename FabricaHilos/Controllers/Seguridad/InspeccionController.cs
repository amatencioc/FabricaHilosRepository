using FabricaHilos.Models.Seguridad.Inspeccion;
using FabricaHilos.Services.Seguridad.Inspeccion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FabricaHilos.Controllers.Seguridad
{
    [Authorize]
    [Area("Seguridad")]
    [Route("Seguridad/Inspeccion")]
    public class InspeccionController : Controller
    {
        private readonly ProcesadorImagenSeguridad _procesadorImagen;
        private readonly IInspeccionService _inspeccionService;
        private readonly ILogger<InspeccionController> _logger;
        private readonly string _rutaSeguridad;

        public InspeccionController(
            IConfiguration configuration, 
            IInspeccionService inspeccionService,
            ILogger<InspeccionController> logger)
        {
            _rutaSeguridad = configuration.GetValue<string>("RutaSeguridad")
                ?? throw new InvalidOperationException(
                    "La clave 'RutaSeguridad' no está definida en appsettings.json.");

            _procesadorImagen = new ProcesadorImagenSeguridad(_rutaSeguridad);
            _inspeccionService = inspeccionService;
            _logger = logger;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            base.OnActionExecuting(context);

            if (string.IsNullOrEmpty(HttpContext.Session.GetString("OracleUser")))
            {
                _logger.LogWarning("Sesión Oracle expirada en Inspección. Redirigiendo al login.");
                TempData["Warning"] = "Su sesión Oracle ha expirado. Por favor, inicie sesión nuevamente.";
                context.Result = RedirectToAction("Login", "Account",
                    new { returnUrl = Request.Path + Request.QueryString });
            }
        }

        // ========== LISTADO DE INSPECCIONES ==========

        [HttpGet]
        public async Task<IActionResult> Index(string? buscar, string? tipo)
        {
            try
            {
                var inspecciones = await _inspeccionService.ObtenerInspeccionesAsync(buscar, tipo);

                ViewBag.Buscar = buscar;
                ViewBag.TipoFiltro = tipo;

                return View(inspecciones);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar listado de inspecciones");
                TempData["Error"] = "Error al cargar el listado de inspecciones.";
                return View(new List<InspeccionListDto>());
            }
        }

        // ========== CREAR HALLAZGO (H) ==========

        [HttpGet]
        public async Task<IActionResult> Crear()
        {
            await CargarDatosFormularioAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(FotoSeguridadViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await CargarDatosFormularioAsync();
                return View(model);
            }

            try
            {
                // 1. Obtener el siguiente número de inspección
                var numeroInspeccion = await _inspeccionService.ObtenerSiguienteNumeroInspeccionAsync();

                // 2. Guardar y optimizar la imagen del hallazgo con nombre específico
                var nombreArchivo = $"{numeroInspeccion}-H.jpg";
                await _procesadorImagen.GuardarYOptimizarImagenAsync(model.Foto, nombreArchivo);
                _logger.LogInformation("Imagen de hallazgo guardada: {NombreArchivo}", nombreArchivo);

                // 3. Obtener usuario de la sesión
                var usuario = HttpContext.Session.GetString("OracleUser") ?? User.Identity?.Name ?? "DESCONOCIDO";

                // 4. Registrar hallazgo en SI_INSPECCION
                var inspeccionDto = new InspeccionRegistroDto
                {
                    NumeroInspeccion = numeroInspeccion,
                    CentroCosto = model.CentroCosto,
                    TipoInspeccion = model.TipoInspeccion,
                    ResponsableInspeccion = model.ResponsableInspeccion,
                    ResponsableArea = model.ResponsableArea,
                    RutaFoto = _rutaSeguridad,
                    UbicaFoto = model.UbicacionFoto
                };

                await _inspeccionService.RegistrarHallazgoAsync(inspeccionDto, usuario);

                TempData["Success"] = $"Hallazgo N° {numeroInspeccion} registrado exitosamente.";
                _logger.LogInformation("Hallazgo {Numero} registrado por usuario {Usuario}", numeroInspeccion, usuario);

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar hallazgo de seguridad");
                TempData["Error"] = $"Error al registrar hallazgo: {ex.Message}";
                await CargarDatosFormularioAsync();
                return View(model);
            }
        }

        // ========== AGREGAR ACCIÓN CORRECTIVA (AC) ==========

        [HttpGet]
        public async Task<IActionResult> AccionCorrectiva(int numero)
        {
            try
            {
                var inspeccion = await _inspeccionService.ObtenerInspeccionPorNumeroAsync(numero);

                if (inspeccion == null)
                {
                    TempData["Error"] = "No se encontró la inspección especificada.";
                    return RedirectToAction(nameof(Index));
                }

                if (inspeccion.TieneAccionCorrectiva)
                {
                    TempData["Warning"] = "Esta inspección ya tiene una acción correctiva registrada.";
                    return RedirectToAction(nameof(Index));
                }

                ViewBag.Inspeccion = inspeccion;
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar formulario de acción correctiva para inspección {Numero}", numero);
                TempData["Error"] = "Error al cargar el formulario.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AccionCorrectiva(int numero, AccionCorrectivaViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var inspeccion = await _inspeccionService.ObtenerInspeccionPorNumeroAsync(numero);
                ViewBag.Inspeccion = inspeccion;
                return View(model);
            }

            try
            {
                // 1. Verificar que la inspección existe y no tiene AC
                var inspeccion = await _inspeccionService.ObtenerInspeccionPorNumeroAsync(numero);

                if (inspeccion == null)
                {
                    TempData["Error"] = "No se encontró la inspección especificada.";
                    return RedirectToAction(nameof(Index));
                }

                if (inspeccion.TieneAccionCorrectiva)
                {
                    TempData["Warning"] = "Esta inspección ya tiene una acción correctiva registrada.";
                    return RedirectToAction(nameof(Index));
                }

                // 2. Guardar y optimizar la imagen de acción correctiva con nombre específico
                var nombreArchivo = $"{numero}-AC.jpg";
                await _procesadorImagen.GuardarYOptimizarImagenAsync(model.Foto, nombreArchivo);
                _logger.LogInformation("Imagen de acción correctiva guardada: {NombreArchivo}", nombreArchivo);

                // 3. Obtener usuario de la sesión
                var usuario = HttpContext.Session.GetString("OracleUser") ?? User.Identity?.Name ?? "DESCONOCIDO";

                // 4. Registrar acción correctiva
                await _inspeccionService.RegistrarAccionCorrectivaAsync(numero, _rutaSeguridad, model.UbicacionFoto, usuario);

                TempData["Success"] = $"Acción correctiva registrada para inspección N° {numero}.";
                _logger.LogInformation("Acción correctiva registrada para inspección {Numero} por usuario {Usuario}", numero, usuario);

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar acción correctiva para inspección {Numero}", numero);
                TempData["Error"] = $"Error al registrar acción correctiva: {ex.Message}";
                return RedirectToAction(nameof(AccionCorrectiva), new { numero });
            }
        }

        // ========== MÉTODOS AUXILIARES ==========

        private async Task CargarDatosFormularioAsync()
        {
            try
            {
                var responsablesArea = await _inspeccionService.ObtenerResponsablesAreaAsync();
                var responsablesInspeccion = await _inspeccionService.ObtenerResponsablesInspeccionAsync();
                var centrosCosto = await _inspeccionService.ObtenerCentrosCostoAsync();

                ViewBag.ResponsablesArea = new SelectList(responsablesArea, "Codigo", "TextoCompleto");
                ViewBag.ResponsablesInspeccion = new SelectList(responsablesInspeccion, "Codigo", "TextoCompleto");
                ViewBag.CentrosCosto = new SelectList(centrosCosto, "CentroCosto", "TextoCompleto");

                ViewBag.TiposInspeccion = new SelectList(new[]
                {
                    new { Value = "P", Text = "Planeada" },
                    new { Value = "N", Text = "No Planeada" }
                }, "Value", "Text");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar datos del formulario");
                ViewBag.Error = "Error al cargar datos del formulario. Por favor, recargue la página.";
            }
        }

        // ========== COMPATIBILIDAD CON MENÚ ANTIGUO ==========

        [HttpGet]
        public async Task<IActionResult> SubirFoto()
        {
            // Redirigir a Crear para mantener compatibilidad
            return RedirectToAction(nameof(Crear));
        }
    }
}
