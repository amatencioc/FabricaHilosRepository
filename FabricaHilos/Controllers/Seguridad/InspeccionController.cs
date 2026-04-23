using FabricaHilos.Filters;
using FabricaHilos.Helpers;
using FabricaHilos.Models.Seguridad.Inspeccion;
using FabricaHilos.Services.Seguridad.Inspeccion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FabricaHilos.Controllers.Seguridad
{
    [Authorize]
    [AccesoExternoPermitido]
    [Route("Seguridad/Inspeccion")]
    public class InspeccionController : OracleBaseController
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

            _procesadorImagen = new ProcesadorImagenSeguridad(_rutaSeguridad, logger);
            _inspeccionService = inspeccionService;
            _logger = logger;
        }

        // ========== LISTADO DE INSPECCIONES ==========

        [HttpGet]
        [Route("")]
        [Route("Index")]
        public async Task<IActionResult> Index(string? tipo, string? estado, DateTime? fechaInicio, DateTime? fechaFin)
        {
            try
            {
                var inspecciones = await _inspeccionService.ObtenerInspeccionesAsync(tipo, estado, fechaInicio, fechaFin);

                ViewBag.TipoFiltro = tipo;
                ViewBag.EstadoFiltro = estado;
                ViewBag.FechaInicio = fechaInicio?.ToString("yyyy-MM-dd");
                ViewBag.FechaFin = fechaFin?.ToString("yyyy-MM-dd");

                return View("~/Views/Seguridad/Inspeccion/Index.cshtml", inspecciones);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar listado de inspecciones");
                TempData["Error"] = "Error al cargar el listado de inspecciones.";
                return View("~/Views/Seguridad/Inspeccion/Index.cshtml", new List<InspeccionListDto>());
            }
        }

        // ========== CREAR INSPECCIÓN ==========

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
            if (!ModelState.IsValid)
            {
                await CargarDatosFormularioAsync();
                return View("~/Views/Seguridad/Inspeccion/Crear.cshtml", model);
            }

            try
            {
                var usuario = HttpContext.Session.GetString("OracleUser") ?? User.Identity?.Name ?? "DESCONOCIDO";

                var inspeccionDto = new InspeccionRegistroDto
                {
                    CentroCosto = model.CentroCosto,
                    TipoInspeccion = model.TipoInspeccion,
                    ResponsableInspeccion = model.ResponsableInspeccion,
                    ResponsableArea = model.ResponsableArea,
                    ObjetivoHallazgo = model.ObjetivoHallazgo
                };

                var numeroInspeccion = await _inspeccionService.RegistrarHallazgoAsync(inspeccionDto, usuario);
                TempData["Success"] = $"Inspección N° {numeroInspeccion} registrada exitosamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar inspección");
                TempData["Error"] = $"Error al registrar inspección: {ex.Message}";
                await CargarDatosFormularioAsync();
                return View("~/Views/Seguridad/Inspeccion/Crear.cshtml", model);
            }
        }

        // ========== AGREGAR ACCIÓN CORRECTIVA (AC) ==========

        [HttpGet("AccionCorrectiva/{numero}/{item}")]
        public async Task<IActionResult> AccionCorrectiva(int numero, int item)
        {
            try
            {
                var inspeccion = await _inspeccionService.ObtenerInspeccionPorNumeroAsync(numero);

                if (inspeccion == null)
                {
                    TempData["Error"] = "No se encontró la inspección especificada.";
                    return RedirectToAction(nameof(Index));
                }

                var foto = inspeccion.Fotos.FirstOrDefault(f => f.Item == item);
                if (foto == null)
                {
                    TempData["Error"] = $"No se encontró el hallazgo ítem {item}.";
                    return RedirectToAction(nameof(Index));
                }

                if (foto.TieneAccionCorrectiva)
                {
                    TempData["Warning"] = $"El hallazgo ítem {item} ya tiene una acción correctiva registrada.";
                    return RedirectToAction(nameof(Index));
                }

                ViewBag.Inspeccion = inspeccion;
                ViewBag.Item = item;
                ViewBag.FotoHallazgo = foto;
                return View("~/Views/Seguridad/Inspeccion/AccionCorrectiva.cshtml");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar formulario de acción correctiva para inspección {Numero}/{Item}", numero, item);
                TempData["Error"] = "Error al cargar el formulario.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost("AccionCorrectiva/{numero}/{item}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AccionCorrectiva(int numero, int item, AccionCorrectivaViewModel model)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _logger.LogWarning("▶▶ POST AccionCorrectiva({Numero}/{Item}) — INICIO ({Ms}ms)", numero, item, sw.ElapsedMilliseconds);

            if (!ModelState.IsValid)
            {
                var errores = string.Join(" | ", ModelState.Values
                    .SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                _logger.LogWarning("▶▶ AC ModelState INVÁLIDO: {Errores} ({Ms}ms)", errores, sw.ElapsedMilliseconds);
                var inspeccion = await _inspeccionService.ObtenerInspeccionPorNumeroAsync(numero);
                ViewBag.Inspeccion = inspeccion;
                ViewBag.Item = item;
                ViewBag.FotoHallazgo = inspeccion?.Fotos.FirstOrDefault(f => f.Item == item);
                return View("~/Views/Seguridad/Inspeccion/AccionCorrectiva.cshtml", model);
            }
            _logger.LogWarning("▶▶ AC ModelState OK ({Ms}ms)", sw.ElapsedMilliseconds);

            try
            {
                // 1. Usuario
                var usuario = HttpContext.Session.GetString("OracleUser") ?? User.Identity?.Name ?? "DESCONOCIDO";

                // 2. Registrar acción correctiva en BD
                await _inspeccionService.RegistrarAccionCorrectivaAsync(numero, item, _rutaSeguridad, model.UbicacionFoto, usuario);

                // 3. Imagen con timeout
                var nombreArchivo = $"{numero}-{item}-AC.jpg";
                try
                {
                    using var msImg = new MemoryStream();
                    await model.Foto.CopyToAsync(msImg);
                    var imgBytes = msImg.ToArray();

                    var imgTask = Task.Run(async () =>
                    {
                        EnsureNetworkShare(_rutaSeguridad);
                        using var imgStream = new MemoryStream(imgBytes, writable: false);
                        await _procesadorImagen.GuardarYOptimizarImagenAsync(imgStream, nombreArchivo);
                    });

                    if (await Task.WhenAny(imgTask, Task.Delay(TimeSpan.FromSeconds(15))) == imgTask)
                        await imgTask;
                    else
                        _logger.LogWarning("▶▶ AC: TIMEOUT 15s guardando imagen en red ({Ms}ms)", sw.ElapsedMilliseconds);

                    TempData["Success"] = $"Acción correctiva registrada para inspección N° {numero}, ítem {item}.";
                }
                catch (Exception exImg)
                {
                    _logger.LogError(exImg, "▶▶ AC: ERROR imagen ({Ms}ms)", sw.ElapsedMilliseconds);
                    TempData["Warning"] = $"Acción correctiva N° {numero} ítem {item} registrada, pero no se pudo guardar la imagen: {exImg.Message}";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ioEx)
            {
                TempData["Warning"] = ioEx.Message;
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "▶▶ POST AccionCorrectiva — ERROR GENERAL ({Ms}ms)", sw.ElapsedMilliseconds);
                TempData["Error"] = $"Error al registrar acción correctiva: {ex.Message}";
                return RedirectToAction(nameof(AccionCorrectiva), new { numero, item });
            }
        }

        // ========== ANULAR INSPECCIÓN ==========

        [HttpPost("Anular/{numero}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Anular(int numero)
        {
            try
            {
                var usuario = HttpContext.Session.GetString("OracleUser") ?? User.Identity?.Name ?? "DESCONOCIDO";
                await _inspeccionService.AnularInspeccionAsync(numero, usuario);
                TempData["Success"] = $"Inspección N° {numero} anulada correctamente.";
            }
            catch (InvalidOperationException ioEx)
            {
                _logger.LogWarning(ioEx, "Anulación rechazada para inspección {Numero}", numero);
                TempData["Warning"] = ioEx.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al anular inspección {Numero}", numero);
                TempData["Error"] = $"Error al anular la inspección: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // ========== ACTUALIZAR FOTO (H o AC) ==========

        [HttpPost("ActualizarFoto")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarFoto(int numero, int item, string tipoFoto, string ubicacion, IFormFile? foto)
        {
            _logger.LogWarning("▶▶ POST ActualizarFoto Num={Num}, Item={Item}, Tipo={Tipo}", numero, item, tipoFoto);
            try
            {
                var usuario = HttpContext.Session.GetString("OracleUser") ?? User.Identity?.Name ?? "DESCONOCIDO";
                string? rutaFotoCompleta = null;

                if (foto != null && foto.Length > 0)
                {
                    var nombreArchivo = $"{numero}-{item}-{tipoFoto}.jpg";
                    try
                    {
                        using var msImg = new MemoryStream();
                        await foto.CopyToAsync(msImg);
                        var imgBytes = msImg.ToArray();

                        var imgTask = Task.Run(async () =>
                        {
                            EnsureNetworkShare(_rutaSeguridad);
                            using var imgStream = new MemoryStream(imgBytes, writable: false);
                            await _procesadorImagen.GuardarYOptimizarImagenAsync(imgStream, nombreArchivo);
                        });

                        if (await Task.WhenAny(imgTask, Task.Delay(TimeSpan.FromSeconds(15))) == imgTask)
                            await imgTask;
                        else
                            _logger.LogWarning("▶▶ ActualizarFoto: TIMEOUT guardando imagen");

                        rutaFotoCompleta = Path.Combine(_rutaSeguridad, nombreArchivo);
                    }
                    catch (Exception exImg)
                    {
                        _logger.LogError(exImg, "▶▶ ActualizarFoto: ERROR guardando imagen");
                        TempData["Warning"] = "Se actualizó la ubicación pero no se pudo guardar la nueva imagen.";
                    }
                }

                await _inspeccionService.ActualizarFotoAsync(numero, item, tipoFoto, ubicacion, rutaFotoCompleta, usuario);

                if (TempData["Warning"] == null)
                    TempData["Success"] = $"Foto de {(tipoFoto == "H" ? "hallazgo" : "acción correctiva")} actualizada para inspección N° {numero}, ítem {item}.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "▶▶ ActualizarFoto: ERROR GENERAL");
                TempData["Error"] = $"Error al actualizar: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // ========== EDITAR INSPECCIÓN ==========

        [HttpGet("Editar/{numero}")]
        public async Task<IActionResult> Editar(int numero)
        {
            try
            {
                var inspeccion = await _inspeccionService.ObtenerInspeccionPorNumeroAsync(numero);
                if (inspeccion == null)
                {
                    TempData["Error"] = "No se encontró la inspección.";
                    return RedirectToAction(nameof(Index));
                }
                if (inspeccion.Estado == "9")
                {
                    TempData["Warning"] = "No se puede editar una inspección anulada.";
                    return RedirectToAction(nameof(Index));
                }

                await CargarDatosFormularioAsync();

                var model = new EditarHallazgoViewModel
                {
                    ResponsableArea = inspeccion.ResponsableArea,
                    ResponsableInspeccion = inspeccion.ResponsableInspeccion,
                    CentroCosto = inspeccion.CentroCosto,
                    TipoInspeccion = inspeccion.Tipo,
                    ObjetivoHallazgo = inspeccion.Objetivo
                };

                ViewBag.Numero = numero;
                return View("~/Views/Seguridad/Inspeccion/EditarHallazgo.cshtml", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar edición de inspección {Numero}", numero);
                TempData["Error"] = "Error al cargar el formulario de edición.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost("Editar/{numero}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(int numero, EditarHallazgoViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await CargarDatosFormularioAsync();
                ViewBag.Numero = numero;
                return View("~/Views/Seguridad/Inspeccion/EditarHallazgo.cshtml", model);
            }

            try
            {
                var usuario = HttpContext.Session.GetString("OracleUser") ?? User.Identity?.Name ?? "DESCONOCIDO";
                await _inspeccionService.ActualizarHallazgoAsync(
                    numero, model.CentroCosto, model.TipoInspeccion,
                    model.ResponsableInspeccion, model.ResponsableArea, usuario);
                TempData["Success"] = $"Inspección N° {numero} actualizada correctamente.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al editar inspección {Numero}", numero);
                TempData["Error"] = $"Error al editar: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // ========== VER DETALLE (JSON) ==========

        [HttpGet("Detalle/{numero}")]
        public async Task<IActionResult> Detalle(int numero)
        {
            try
            {
                var item = await _inspeccionService.ObtenerInspeccionPorNumeroAsync(numero);
                if (item == null) return NotFound();

                return Json(new
                {
                    item.Numero,
                    Fecha = item.Fecha.ToString("dd/MM/yyyy HH:mm"),
                    item.Tipo,
                    item.CentroCosto,
                    item.NombreCentroCosto,
                    item.ResponsableInspeccion,
                    item.NombreRespInspeccion,
                    item.ResponsableArea,
                    item.NombreRespArea,
                    item.Estado,
                    item.Objetivo,
                    Fotos = item.Fotos.Select(f => new
                    {
                        f.Item,
                        f.UbicaFotoH,
                        FechaFotoH = f.FechaFotoH?.ToString("dd/MM/yyyy"),
                        f.UbicaFotoAc,
                        FechaFotoAc = f.FechaFotoAc?.ToString("dd/MM/yyyy"),
                        f.TieneAccionCorrectiva
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener detalle de inspección {Numero}", numero);
                return StatusCode(500);
            }
        }

        // ========== SERVIR IMAGEN ==========

        [HttpGet("Imagen/{numero}/{item}/{tipo}")]
        public IActionResult Imagen(int numero, int item, string tipo)
        {
            tipo = tipo.ToUpperInvariant();
            if (tipo != "H" && tipo != "AC") return BadRequest();

            var nombreArchivo = $"{numero}-{item}-{tipo}.jpg";
            var rutaCompleta = Path.Combine(_rutaSeguridad, nombreArchivo);

            try
            {
                EnsureNetworkShare(rutaCompleta);

                if (!System.IO.File.Exists(rutaCompleta)) return NotFound();

                // Usar FileStream en lugar de ReadAllBytes para no cargar toda la imagen en RAM
                var stream = new FileStream(rutaCompleta, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
                return File(stream, "image/jpeg");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al servir imagen {Archivo}", nombreArchivo);
                return NotFound();
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

        // ========== GESTIÓN H / AC ==========

        [HttpGet("HallazgosAC/{numero}")]
        public async Task<IActionResult> HallazgosAC(int numero)
        {
            try
            {
                var inspeccion = await _inspeccionService.ObtenerInspeccionPorNumeroAsync(numero);
                if (inspeccion == null)
                {
                    TempData["Error"] = "No se encontró la inspección.";
                    return RedirectToAction(nameof(Index));
                }
                if (inspeccion.Estado == "9")
                {
                    TempData["Warning"] = "No se puede gestionar una inspección anulada.";
                    return RedirectToAction(nameof(Index));
                }

                ViewBag.Inspeccion = inspeccion;
                return View("~/Views/Seguridad/Inspeccion/AgregarHallazgo.cshtml");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar gestión H/AC para inspección {Numero}", numero);
                TempData["Error"] = "Error al cargar la gestión de hallazgos.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost("AgregarHallazgo/{numero}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AgregarHallazgo(int numero, AgregarHallazgoFotoViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errores = string.Join(" | ", ModelState.Values
                    .SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                _logger.LogWarning("▶▶ AgregarHallazgo ModelState INVÁLIDO #{Num}: {Errores}", numero, errores);
                var inspeccion = await _inspeccionService.ObtenerInspeccionPorNumeroAsync(numero);
                ViewBag.Inspeccion = inspeccion;
                return View("~/Views/Seguridad/Inspeccion/AgregarHallazgo.cshtml", model);
            }

            try
            {
                var usuario = HttpContext.Session.GetString("OracleUser") ?? User.Identity?.Name ?? "DESCONOCIDO";

                var nuevoItem = await _inspeccionService.AgregarFotoHallazgoAsync(
                    numero, _rutaSeguridad, model.UbicacionFoto, usuario);

                var nombreArchivo = $"{numero}-{nuevoItem}-H.jpg";
                try
                {
                    using var msImg = new MemoryStream();
                    await model.Foto.CopyToAsync(msImg);
                    var imgBytes = msImg.ToArray();

                    var imgTask = Task.Run(async () =>
                    {
                        EnsureNetworkShare(_rutaSeguridad);
                        using var imgStream = new MemoryStream(imgBytes, writable: false);
                        await _procesadorImagen.GuardarYOptimizarImagenAsync(imgStream, nombreArchivo);
                    });

                    if (await Task.WhenAny(imgTask, Task.Delay(TimeSpan.FromSeconds(15))) == imgTask)
                        await imgTask;
                    else
                        _logger.LogWarning("▶▶ AgregarHallazgo: TIMEOUT guardando imagen");

                    TempData["Success"] = $"Hallazgo ítem {nuevoItem} agregado a inspección N° {numero}.";
                }
                catch (Exception exImg)
                {
                    _logger.LogError(exImg, "▶▶ AgregarHallazgo: ERROR imagen");
                    TempData["Warning"] = $"Hallazgo ítem {nuevoItem} registrado, pero no se pudo guardar la imagen: {exImg.Message}";
                }

                return RedirectToAction(nameof(HallazgosAC), new { numero });
            }
            catch (InvalidOperationException ioEx)
            {
                TempData["Warning"] = ioEx.Message;
                return RedirectToAction(nameof(HallazgosAC), new { numero });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al agregar hallazgo a inspección {Numero}", numero);
                TempData["Error"] = $"Error al agregar hallazgo: {ex.Message}";
                return RedirectToAction(nameof(HallazgosAC), new { numero });
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
        public IActionResult SubirFoto()
        {
            return RedirectToAction(nameof(Crear));
        }
    }
}
