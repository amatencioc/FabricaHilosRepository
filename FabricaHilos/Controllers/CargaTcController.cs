using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using FabricaHilos.Models.Sgc;
using FabricaHilos.Services;
using FabricaHilos.Services.Sgc;

namespace FabricaHilos.Controllers
{
    [Authorize]
    public class CargaTcController : Controller
    {
        private readonly ICargaTcService _cargaTcService;
        private readonly ILogger<CargaTcController> _logger;
        private readonly INavTokenService _navToken;

        public CargaTcController(ICargaTcService cargaTcService, ILogger<CargaTcController> logger, INavTokenService navToken)
        {
            _cargaTcService = cargaTcService;
            _logger = logger;
            _navToken = navToken;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            base.OnActionExecuting(context);
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("OracleUser")))
            {
                _logger.LogWarning("Sesión Oracle expirada en Carga TC. Redirigiendo al login.");
                TempData["Warning"] = "Su sesión Oracle ha expirado. Por favor, inicie sesión nuevamente.";
                context.Result = RedirectToAction("Login", "Account",
                    new { returnUrl = Request.Path + Request.QueryString });
            }
        }

        // ========== LISTADO DE REQUERIMIENTOS (REQ_CERT) ==========

        [HttpGet]
        public async Task<IActionResult> Index(string? t = null, string? buscar = null, 
            DateTime? fechaInicio = null, DateTime? fechaFin = null, int page = 1)
        {
            // Si hay filtros nuevos sin token, crear token y redirigir
            if (string.IsNullOrEmpty(t) && (buscar != null || fechaInicio.HasValue || fechaFin.HasValue))
            {
                var token = _navToken.Protect(new Dictionary<string, string?> {
                    ["buscar"]      = buscar,
                    ["fechaInicio"] = fechaInicio?.ToString("yyyy-MM-dd"),
                    ["fechaFin"]    = fechaFin?.ToString("yyyy-MM-dd")
                });
                return RedirectToAction(nameof(Index), new { t = token, page });
            }

            // Desempaquetar token
            if (!string.IsNullOrEmpty(t) && _navToken.TryUnprotect(t, out var nav))
            {
                buscar = nav.GetValueOrDefault("buscar") ?? buscar;
                if (DateTime.TryParse(nav.GetValueOrDefault("fechaInicio"), out var fi)) fechaInicio = fi;
                if (DateTime.TryParse(nav.GetValueOrDefault("fechaFin"),    out var ff)) fechaFin    = ff;
            }

            const int pageSize = 10;
            var resultado = await _cargaTcService.ObtenerRequerimientosAsync(buscar, fechaInicio, fechaFin, page, pageSize);
            
            if (!resultado.Items.Any() && page > 1)
                return RedirectToAction(nameof(Index), new { t, page = 1 });

            ViewBag.Buscar      = buscar;
            ViewBag.FechaInicio = fechaInicio?.ToString("yyyy-MM-dd");
            ViewBag.FechaFin    = fechaFin?.ToString("yyyy-MM-dd");
            ViewBag.NavToken    = t;
            ViewBag.Page        = page;
            ViewBag.PageSize    = pageSize;
            ViewBag.TotalCount  = resultado.TotalCount;
            ViewBag.TotalPages  = resultado.TotalCount == 0 ? 1 : (int)Math.Ceiling((double)resultado.TotalCount / pageSize);

            return View("~/Views/Sgc/Despachos/CargaTc/Index.cshtml", resultado.Items);
        }

        // ========== DETALLE DE REQUERIMIENTO (REQ_CERT_D) ==========

        [HttpGet]
        public async Task<IActionResult> Detalle(int numReq, string? t = null)
        {
            var requerimiento = await _cargaTcService.ObtenerRequerimientoAsync(numReq);
            if (requerimiento == null)
            {
                TempData["Error"] = "No se encontró el requerimiento especificado.";
                return RedirectToAction(nameof(Index), new { t });
            }

            var detalles = await _cargaTcService.ObtenerDetalleRequerimientoAsync(numReq);

            ViewBag.Requerimiento = requerimiento;
            ViewBag.NavToken = t;

            return View("~/Views/Sgc/Despachos/CargaTc/Detalle.cshtml", detalles);
        }

        // ========== ACTUALIZAR CERTIFICADO ==========

        [HttpPost]
        public async Task<IActionResult> ActualizarCertificado([FromBody] ActualizarCertificadoDto modelo)
        {
            try
            {
                var usuario = HttpContext.Session.GetString("OracleUser") ?? "SYSTEM";
                var resultado = await _cargaTcService.ActualizarCertificadoAsync(modelo, usuario);

                if (resultado)
                {
                    return Json(new { tipo = "Exito", mensaje = "Datos actualizados correctamente." });
                }
                else
                {
                    return Json(new { tipo = "Advertencia", mensaje = "No se pudo actualizar los datos." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar certificado para NUM_REQ {NumReq}", modelo.NumReq);
                return Json(new { tipo = "Error", mensaje = $"Error al actualizar: {ex.Message}" });
            }
        }

        // ========== CARGAR PDF ==========

        [HttpPost]
        public async Task<IActionResult> CargarPdf(int numReq, IFormFile archivo, string? numCer)
        {
            try
            {
                if (archivo == null || archivo.Length == 0)
                    return Json(new { tipo = "Advertencia", mensaje = "Debe seleccionar un archivo PDF." });

                if (!archivo.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
                    return Json(new { tipo = "Advertencia", mensaje = "Solo se permiten archivos PDF." });

                // Obtener el requerimiento para obtener el código de cliente
                var requerimiento = await _cargaTcService.ObtenerRequerimientoAsync(numReq);
                if (requerimiento == null || string.IsNullOrEmpty(requerimiento.CodCliente))
                {
                    return Json(new { tipo = "Error", mensaje = "No se encontró el código de cliente para este requerimiento." });
                }

                // Obtener el RUC del cliente
                var cliente = await _cargaTcService.ObtenerClientePorCodigoAsync(requerimiento.CodCliente);
                if (cliente == null || string.IsNullOrEmpty(cliente.Ruc))
                {
                    return Json(new { tipo = "Error", mensaje = $"No se encontró el RUC para el cliente {requerimiento.CodCliente}." });
                }

                // Validar que al menos NumCer esté presente para generar la ruta
                if (string.IsNullOrWhiteSpace(numCer))
                {
                    return Json(new { tipo = "Advertencia", mensaje = "Debe ingresar el Nº Certificado para cargar el PDF." });
                }

                // Generar ruta del PDF
                var rutaPdf = await _cargaTcService.GenerarRutaPdfCertificado(cliente.Ruc, numCer);

                // Guardar el archivo
                using (var stream = new FileStream(rutaPdf, FileMode.Create))
                {
                    await archivo.CopyToAsync(stream);
                }

                _logger.LogInformation("PDF guardado en: {RutaPdf}", rutaPdf);

                // Actualizar los datos en la base de datos
                var modelo = new ActualizarCertificadoDto
                {
                    NumReq = numReq,
                    NumCer = numCer
                };

                var usuario = HttpContext.Session.GetString("OracleUser") ?? "SYSTEM";
                var actualizado = await _cargaTcService.ActualizarCertificadoAsync(modelo, usuario);

                if (actualizado)
                {
                    return Json(new
                    {
                        tipo = "Exito",
                        mensaje = $"PDF guardado correctamente en: {rutaPdf}",
                        ruta = rutaPdf
                    });
                }
                else
                {
                    return Json(new { tipo = "Advertencia", mensaje = "PDF guardado pero no se pudo actualizar la base de datos." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar PDF para NUM_REQ {NumReq}", numReq);
                return Json(new { tipo = "Error", mensaje = $"Error al cargar PDF: {ex.Message}" });
            }
        }

        // ========== VISUALIZAR PDF ==========

        [HttpGet]
        public async Task<IActionResult> VisualizarPdf(int numReq)
        {
            try
            {
                var requerimiento = await _cargaTcService.ObtenerRequerimientoAsync(numReq);
                if (requerimiento == null)
                {
                    return NotFound("No se encontró el requerimiento.");
                }

                if (string.IsNullOrEmpty(requerimiento.NumCer) || string.IsNullOrEmpty(requerimiento.CodCliente))
                {
                    return NotFound("El requerimiento no tiene certificado cargado.");
                }

                // Obtener el RUC del cliente
                var cliente = await _cargaTcService.ObtenerClientePorCodigoAsync(requerimiento.CodCliente);
                if (cliente == null || string.IsNullOrEmpty(cliente.Ruc))
                {
                    return NotFound("No se encontró el RUC del cliente.");
                }

                // Generar ruta del PDF
                var rutaPdf = await _cargaTcService.GenerarRutaPdfCertificado(cliente.Ruc, requerimiento.NumCer);

                if (!System.IO.File.Exists(rutaPdf))
                {
                    return NotFound($"No se encontró el PDF en: {rutaPdf}");
                }

                var pdfBytes = await System.IO.File.ReadAllBytesAsync(rutaPdf);
                var nombreArchivo = Path.GetFileName(rutaPdf);

                // Content-Disposition: inline para visualizar en el navegador
                Response.Headers.Add("Content-Disposition", $"inline; filename=\"{nombreArchivo}\"");
                return File(pdfBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al visualizar PDF para NUM_REQ {NumReq}", numReq);
                return StatusCode(500, "Error al cargar el PDF");
            }
        }

        // ========== DESCARGAR PDF ==========

        [HttpGet]
        public async Task<IActionResult> DescargarPdf(int numReq)
        {
            try
            {
                var requerimiento = await _cargaTcService.ObtenerRequerimientoAsync(numReq);
                if (requerimiento == null)
                {
                    return Json(new { tipo = "Error", mensaje = "No se encontró el requerimiento." });
                }

                if (string.IsNullOrEmpty(requerimiento.NumCer) || string.IsNullOrEmpty(requerimiento.CodCliente))
                {
                    return Json(new { tipo = "Advertencia", mensaje = "El requerimiento no tiene certificado cargado." });
                }

                // Obtener el RUC del cliente
                var cliente = await _cargaTcService.ObtenerClientePorCodigoAsync(requerimiento.CodCliente);
                if (cliente == null || string.IsNullOrEmpty(cliente.Ruc))
                {
                    return Json(new { tipo = "Error", mensaje = "No se encontró el RUC del cliente." });
                }

                // Generar ruta del PDF
                var rutaPdf = await _cargaTcService.GenerarRutaPdfCertificado(cliente.Ruc, requerimiento.NumCer);

                if (!System.IO.File.Exists(rutaPdf))
                {
                    return Json(new { tipo = "Advertencia", mensaje = $"No se encontró el PDF en: {rutaPdf}" });
                }

                var pdfBytes = await System.IO.File.ReadAllBytesAsync(rutaPdf);
                var nombreArchivo = Path.GetFileName(rutaPdf);

                return File(pdfBytes, "application/pdf", nombreArchivo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al descargar PDF para NUM_REQ {NumReq}", numReq);
                return Json(new { tipo = "Error", mensaje = $"Error al descargar PDF: {ex.Message}" });
            }
        }
    }
}
