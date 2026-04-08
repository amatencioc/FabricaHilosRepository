using FabricaHilos.Helpers;
using FabricaHilos.Models.Seguridad.Inspeccion;
using FabricaHilos.Services.Seguridad.Inspeccion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FabricaHilos.Controllers.Seguridad
{
    [Authorize]
    [Route("Seguridad/Inspeccion")]
    public class InspeccionController : Controller
    {
        private readonly ProcesadorImagenSeguridad _procesadorImagen;
        private readonly IInspeccionService _inspeccionService;
        private readonly ILogger<InspeccionController> _logger;
        private readonly string _rutaSeguridad;
        private readonly string? _networkUsername;
        private readonly string? _networkPassword;
        private readonly string? _networkDomain;

        public InspeccionController(
            IConfiguration configuration, 
            IInspeccionService inspeccionService,
            ILogger<InspeccionController> logger)
        {
            _rutaSeguridad = configuration.GetValue<string>("RutaSeguridad")
                ?? throw new InvalidOperationException(
                    "La clave 'RutaSeguridad' no está definida en appsettings.json.");

            _networkUsername = configuration["NetworkShare:Username"];
            _networkPassword = configuration["NetworkShare:Password"];
            _networkDomain   = configuration["NetworkShare:Domain"];

            _procesadorImagen = new ProcesadorImagenSeguridad(_rutaSeguridad, logger);
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
        [Route("")]
        [Route("Index")]
        public async Task<IActionResult> Index(string? buscar, string? tipo)
        {
            try
            {
                var inspecciones = await _inspeccionService.ObtenerInspeccionesAsync(buscar, tipo);

                ViewBag.Buscar = buscar;
                ViewBag.TipoFiltro = tipo;

                return View("~/Views/Seguridad/Inspeccion/Index.cshtml", inspecciones);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar listado de inspecciones");
                TempData["Error"] = "Error al cargar el listado de inspecciones.";
                return View("~/Views/Seguridad/Inspeccion/Index.cshtml", new List<InspeccionListDto>());
            }
        }

        // ========== CREAR HALLAZGO (H) ==========

        [HttpGet("Crear")]
        public async Task<IActionResult> Crear()
        {
            await CargarDatosFormularioAsync();
            return View("~/Views/Seguridad/Inspeccion/Crear.cshtml");
        }

        [HttpPost("Crear")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(FotoSeguridadViewModel model)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _logger.LogWarning("▶▶ POST Crear — INICIO ({Ms}ms)", sw.ElapsedMilliseconds);

            if (!ModelState.IsValid)
            {
                var errores = string.Join(" | ", ModelState.Values
                    .SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                _logger.LogWarning("▶▶ ModelState INVÁLIDO: {Errores} ({Ms}ms)", errores, sw.ElapsedMilliseconds);
                await CargarDatosFormularioAsync();
                _logger.LogWarning("▶▶ CargarDatos terminó ({Ms}ms)", sw.ElapsedMilliseconds);
                return View("~/Views/Seguridad/Inspeccion/Crear.cshtml", model);
            }
            _logger.LogWarning("▶▶ ModelState OK ({Ms}ms)", sw.ElapsedMilliseconds);

            try
            {
                // 1. Usuario
                var usuario = HttpContext.Session.GetString("OracleUser") ?? User.Identity?.Name ?? "DESCONOCIDO";
                _logger.LogWarning("▶▶ Paso 1: Usuario={Usuario} ({Ms}ms)", usuario, sw.ElapsedMilliseconds);

                // 2. Registrar en BD (obtiene número, inserta y actualiza correlativo en una sola transacción)
                _logger.LogWarning("▶▶ Paso 2: RegistrarHallazgoAsync (transacción atómica)...");
                var inspeccionDto = new InspeccionRegistroDto
                {
                    CentroCosto = model.CentroCosto,
                    TipoInspeccion = model.TipoInspeccion,
                    ResponsableInspeccion = model.ResponsableInspeccion,
                    ResponsableArea = model.ResponsableArea,
                    RutaFoto = _rutaSeguridad,
                    UbicaFoto = model.UbicacionFoto
                };

                var numeroInspeccion = await _inspeccionService.RegistrarHallazgoAsync(inspeccionDto, usuario);
                _logger.LogWarning("▶▶ Paso 2: BD OK — Número asignado={Numero} ({Ms}ms)", numeroInspeccion, sw.ElapsedMilliseconds);

                // 3. Imagen con timeout
                var nombreArchivo = $"{numeroInspeccion}-H.jpg";
                try
                {
                    _logger.LogWarning("▶▶ Paso 4a: Leyendo bytes de imagen en memoria...");
                    using var msImg = new MemoryStream();
                    await model.Foto.CopyToAsync(msImg);
                    var imgBytes = msImg.ToArray();
                    _logger.LogWarning("▶▶ Paso 4a: {Size} bytes leídos ({Ms}ms)", imgBytes.Length, sw.ElapsedMilliseconds);

                    _logger.LogWarning("▶▶ Paso 4b: Iniciando Task.Run para guardar en red (timeout 15s)...");
                    var imgTask = Task.Run(async () =>
                    {
                        if (OperatingSystem.IsWindows())
                            NetworkShareHelper.Connect(_rutaSeguridad, _networkUsername, _networkPassword, _networkDomain);
                        using var imgStream = new MemoryStream(imgBytes, writable: false);
                        await _procesadorImagen.GuardarYOptimizarImagenAsync(imgStream, nombreArchivo);
                    });

                    if (await Task.WhenAny(imgTask, Task.Delay(TimeSpan.FromSeconds(15))) == imgTask)
                    {
                        await imgTask; // re-throw si hubo error
                        _logger.LogWarning("▶▶ Paso 4b: Imagen guardada OK ({Ms}ms)", sw.ElapsedMilliseconds);
                    }
                    else
                    {
                        _logger.LogWarning("▶▶ Paso 4b: TIMEOUT 15s guardando imagen en red ({Ms}ms)", sw.ElapsedMilliseconds);
                    }

                    TempData["Success"] = $"Hallazgo N° {numeroInspeccion} registrado exitosamente.";
                }
                catch (Exception exImg)
                {
                    _logger.LogError(exImg, "▶▶ Paso 4: ERROR imagen ({Ms}ms)", sw.ElapsedMilliseconds);
                    TempData["Warning"] = $"Hallazgo N° {numeroInspeccion} registrado, pero no se pudo guardar la imagen: {exImg.Message}";
                }

                _logger.LogWarning("▶▶ POST Crear — FIN. Redirigiendo a Index ({Ms}ms)", sw.ElapsedMilliseconds);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "▶▶ POST Crear — ERROR GENERAL ({Ms}ms)", sw.ElapsedMilliseconds);
                TempData["Error"] = $"Error al registrar hallazgo: {ex.Message}";
                await CargarDatosFormularioAsync();
                return View("~/Views/Seguridad/Inspeccion/Crear.cshtml", model);
            }
        }

        // ========== AGREGAR ACCIÓN CORRECTIVA (AC) ==========

        [HttpGet("AccionCorrectiva/{numero}")]
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
                return View("~/Views/Seguridad/Inspeccion/AccionCorrectiva.cshtml");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar formulario de acción correctiva para inspección {Numero}", numero);
                TempData["Error"] = "Error al cargar el formulario.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost("AccionCorrectiva/{numero}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AccionCorrectiva(int numero, AccionCorrectivaViewModel model)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _logger.LogWarning("▶▶ POST AccionCorrectiva({Numero}) — INICIO ({Ms}ms)", numero, sw.ElapsedMilliseconds);

            if (!ModelState.IsValid)
            {
                var errores = string.Join(" | ", ModelState.Values
                    .SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                _logger.LogWarning("▶▶ AC ModelState INVÁLIDO: {Errores} ({Ms}ms)", errores, sw.ElapsedMilliseconds);
                var inspeccion = await _inspeccionService.ObtenerInspeccionPorNumeroAsync(numero);
                ViewBag.Inspeccion = inspeccion;
                return View("~/Views/Seguridad/Inspeccion/AccionCorrectiva.cshtml", model);
            }
            _logger.LogWarning("▶▶ AC ModelState OK ({Ms}ms)", sw.ElapsedMilliseconds);

            try
            {
                // 1. Verificar que la inspección existe y no tiene AC
                _logger.LogWarning("▶▶ AC Paso 1: ObtenerInspeccionPorNumero...");
                var inspeccion = await _inspeccionService.ObtenerInspeccionPorNumeroAsync(numero);
                _logger.LogWarning("▶▶ AC Paso 1: Existe={Existe}, TieneAC={TieneAC} ({Ms}ms)",
                    inspeccion != null, inspeccion?.TieneAccionCorrectiva, sw.ElapsedMilliseconds);

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

                // 2. Usuario
                var usuario = HttpContext.Session.GetString("OracleUser") ?? User.Identity?.Name ?? "DESCONOCIDO";
                _logger.LogWarning("▶▶ AC Paso 2: Usuario={Usuario} ({Ms}ms)", usuario, sw.ElapsedMilliseconds);

                // 3. Registrar acción correctiva en BD
                _logger.LogWarning("▶▶ AC Paso 3: RegistrarAccionCorrectivaAsync...");
                await _inspeccionService.RegistrarAccionCorrectivaAsync(numero, _rutaSeguridad, model.UbicacionFoto, usuario);
                _logger.LogWarning("▶▶ AC Paso 3: BD OK ({Ms}ms)", sw.ElapsedMilliseconds);

                // 4. Imagen con timeout
                var nombreArchivo = $"{numero}-AC.jpg";
                try
                {
                    _logger.LogWarning("▶▶ AC Paso 4a: Leyendo bytes de imagen...");
                    using var msImg = new MemoryStream();
                    await model.Foto.CopyToAsync(msImg);
                    var imgBytes = msImg.ToArray();
                    _logger.LogWarning("▶▶ AC Paso 4a: {Size} bytes leídos ({Ms}ms)", imgBytes.Length, sw.ElapsedMilliseconds);

                    _logger.LogWarning("▶▶ AC Paso 4b: Task.Run guardar en red (timeout 15s)...");
                    var imgTask = Task.Run(async () =>
                    {
                        if (OperatingSystem.IsWindows())
                            NetworkShareHelper.Connect(_rutaSeguridad, _networkUsername, _networkPassword, _networkDomain);
                        using var imgStream = new MemoryStream(imgBytes, writable: false);
                        await _procesadorImagen.GuardarYOptimizarImagenAsync(imgStream, nombreArchivo);
                    });

                    if (await Task.WhenAny(imgTask, Task.Delay(TimeSpan.FromSeconds(15))) == imgTask)
                    {
                        await imgTask;
                        _logger.LogWarning("▶▶ AC Paso 4b: Imagen guardada OK ({Ms}ms)", sw.ElapsedMilliseconds);
                    }
                    else
                    {
                        _logger.LogWarning("▶▶ AC Paso 4b: TIMEOUT 15s guardando imagen en red ({Ms}ms)", sw.ElapsedMilliseconds);
                    }

                    TempData["Success"] = $"Acción correctiva registrada para inspección N° {numero}.";
                }
                catch (Exception exImg)
                {
                    _logger.LogError(exImg, "▶▶ AC Paso 4: ERROR imagen ({Ms}ms)", sw.ElapsedMilliseconds);
                    TempData["Warning"] = $"Acción correctiva N° {numero} registrada, pero no se pudo guardar la imagen: {exImg.Message}";
                }

                _logger.LogWarning("▶▶ POST AccionCorrectiva — FIN. Redirigiendo a Index ({Ms}ms)", sw.ElapsedMilliseconds);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "▶▶ POST AccionCorrectiva — ERROR GENERAL ({Ms}ms)", sw.ElapsedMilliseconds);
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

                // Lookups para pre-poblar texto en campos de búsqueda al volver con errores
                ViewBag.ResponsablesAreaList    = responsablesArea;
                ViewBag.CentrosCostoList        = centrosCosto;

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

        // ========== BÚSQUEDA AJAX ==========

        [HttpGet("BuscarResponsable")]
        public async Task<IActionResult> BuscarResponsable(string? q)
        {
            var todos = await _inspeccionService.ObtenerResponsablesAreaAsync();

            IEnumerable<ResponsableDto> filtrados = string.IsNullOrWhiteSpace(q)
                ? todos.Take(15)
                : todos.Where(r => r.Codigo.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                                   r.NombreCorto.Contains(q, StringComparison.OrdinalIgnoreCase))
                       .Take(10);

            return Json(filtrados
                .Select(r => new { codigo = r.Codigo, nombre = r.NombreCorto, texto = r.TextoCompleto })
                .ToList());
        }

        [HttpGet("BuscarCentroCosto")]
        public async Task<IActionResult> BuscarCentroCosto(string? q)
        {
            var todos = await _inspeccionService.ObtenerCentrosCostoAsync();

            IEnumerable<CentroCostoDto> filtrados = string.IsNullOrWhiteSpace(q)
                ? todos.Take(15)
                : todos.Where(c => c.CentroCosto.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                                   c.Nombre.Contains(q, StringComparison.OrdinalIgnoreCase))
                       .Take(10);

            return Json(filtrados
                .Select(c => new { codigo = c.CentroCosto, nombre = c.Nombre, texto = c.TextoCompleto })
                .ToList());
        }

        // ========== COMPATIBILIDAD CON MENÚ ANTIGUO ==========

        [HttpGet("SubirFoto")]
        public async Task<IActionResult> SubirFoto()
        {
            // Redirigir a Crear para mantener compatibilidad
            return RedirectToAction(nameof(Crear));
        }
    }
}
