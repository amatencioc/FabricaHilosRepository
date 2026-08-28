using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FabricaHilos.Models.Sgc;
using FabricaHilos.Services;
using FabricaHilos.Services.Archivos;
using FabricaHilos.Services.Sgc;

namespace FabricaHilos.Controllers.Sgc
{
    /// <summary>
    /// Módulo "Cargar TC Fibras" — certificados de compra de algodón orgánico
    /// (GOTS / OCS). Misma dinámica que <see cref="CargaTcController"/> (listado,
    /// detalle, carga/visualización/descarga de PDF), pero sobre SIG.REQ_CERT /
    /// REQ_CERT_D con TIPO='C' (Compra) en vez de TIPO='V' (Venta), y resolviendo
    /// el Proveedor en vez del Cliente. No incluye Enviar a Facturación.
    /// </summary>
    [Authorize]
    public class CargaTcFibraController : OracleBaseController
    {
        private readonly ICargaTcFibraService _cargaTcFibraService;
        private readonly ILogger<CargaTcFibraController> _logger;
        private readonly INavTokenService _navToken;
        private readonly IProcesadorArchivoService _procesador;

        public CargaTcFibraController(
            ICargaTcFibraService cargaTcFibraService,
            ILogger<CargaTcFibraController> logger,
            INavTokenService navToken,
            IProcesadorArchivoService procesador)
        {
            _cargaTcFibraService = cargaTcFibraService;
            _logger              = logger;
            _navToken            = navToken;
            _procesador          = procesador;
        }

        // ========== LISTADO DE REQUERIMIENTOS (REQ_CERT, TIPO='C') ==========

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
            var resultado = await _cargaTcFibraService.ObtenerRequerimientosAsync(buscar, fechaInicio, fechaFin, page, pageSize);

            if (!resultado.Items.Any() && page > 1)
                return RedirectToAction(nameof(Index), new { t, page = 1 });

            var navToken = _navToken.Protect(new Dictionary<string, string?> {
                ["buscar"]      = buscar,
                ["fechaInicio"] = fechaInicio?.ToString("yyyy-MM-dd"),
                ["fechaFin"]    = fechaFin?.ToString("yyyy-MM-dd")
            });

            ViewBag.Buscar      = buscar;
            ViewBag.FechaInicio = fechaInicio?.ToString("yyyy-MM-dd");
            ViewBag.FechaFin    = fechaFin?.ToString("yyyy-MM-dd");
            ViewBag.NavToken    = navToken;
            ViewBag.Page        = page;
            ViewBag.PageSize    = pageSize;
            ViewBag.TotalCount  = resultado.TotalCount;
            ViewBag.TotalPages  = resultado.TotalCount == 0 ? 1 : (int)Math.Ceiling((double)resultado.TotalCount / pageSize);
            ViewBag.Proveedores = await _cargaTcFibraService.ObtenerTodosProveedoresAsync();

            return View("~/Views/Sgc/Despachos/CargaTcFibra/Index.cshtml", resultado.Items);
        }

        // ========== DETALLE DE REQUERIMIENTO (REQ_CERT_D, TIPO='C') ==========

        [HttpGet]
        public async Task<IActionResult> Detalle(int numReq, string? t = null)
        {
            var requerimiento = await _cargaTcFibraService.ObtenerRequerimientoAsync(numReq);
            if (requerimiento == null)
            {
                TempData["Error"] = "No se encontró el requerimiento especificado.";
                return RedirectToAction(nameof(Index), new { t });
            }

            var detalles = await _cargaTcFibraService.ObtenerDetalleRequerimientoAsync(numReq);

            ViewBag.Requerimiento = requerimiento;
            ViewBag.NavToken = t;

            return View("~/Views/Sgc/Despachos/CargaTcFibra/Detalle.cshtml", detalles);
        }

        // ========== REGISTRAR REQUERIMIENTO DE CERTIFICADO DIGITAL (un-click) ==========

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistrarRequerimiento([FromBody] RegistrarRequerimientoCertDto modelo)
        {
            if (!ModelState.IsValid)
            {
                var error = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Datos inválidos.";
                return Json(new { tipo = "Advertencia", mensaje = error });
            }

            try
            {
                var usuario = HttpContext.Session.GetString("OracleUser") ?? "SYSTEM";
                var codResponsable = HttpContext.Session.GetString("OracleUserCodigo");
                if (string.IsNullOrWhiteSpace(codResponsable))
                    return Json(new { tipo = "Advertencia", mensaje = "No se pudo determinar el código de empleado del usuario actual. Vuelva a iniciar sesión." });

                var numReq = await _cargaTcFibraService.RegistrarRequerimientoCertificadoAsync(modelo, usuario, codResponsable);
                if (numReq == null)
                    return Json(new { tipo = "Advertencia", mensaje = $"No se encontró el requerimiento de certificado Nº {modelo.NumReq}." });

                return Json(new { tipo = "Exito", mensaje = "Requerimiento registrado correctamente.", numReq });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar requerimiento de Certificado Digital");
                return Json(new { tipo = "Error", mensaje = "Ocurrió un error al registrar el requerimiento. Contacte al administrador." });
            }
        }

        // ========== ACTUALIZAR CERTIFICADO ==========

        [HttpPost]
        public async Task<IActionResult> ActualizarCertificado([FromBody] ActualizarCertificadoDto modelo)
        {
            try
            {
                var usuario = HttpContext.Session.GetString("OracleUser") ?? "SYSTEM";
                var resultado = await _cargaTcFibraService.ActualizarCertificadoAsync(modelo, usuario);

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
                _logger.LogError(ex, "Error al actualizar certificado de fibra para NUM_REQ {NumReq}", modelo.NumReq);
                return Json(new { tipo = "Error", mensaje = "Ocurrió un error al actualizar los datos. Contacte al administrador." });
            }
        }

        // ========== CARGAR PDF ==========

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CargarPdf(int numReq, IFormFile archivo, string? numCer, string? observacion)
        {
            try
            {
                if (archivo == null || archivo.Length == 0)
                    return Json(new { tipo = "Advertencia", mensaje = "El Archivo PDF del certificado es obligatorio." });

                // Validar por extensión (no por ContentType del cliente, que puede falsificarse)
                var extArchivo = Path.GetExtension(archivo.FileName).ToLowerInvariant();
                if (extArchivo != ".pdf")
                    return Json(new { tipo = "Advertencia", mensaje = "Solo se permiten archivos PDF." });

                // Obtener el requerimiento para resolver el RUC del proveedor
                var requerimiento = await _cargaTcFibraService.ObtenerRequerimientoAsync(numReq);
                if (requerimiento == null || string.IsNullOrEmpty(requerimiento.Ruc))
                {
                    return Json(new { tipo = "Error", mensaje = "No se encontró el RUC del proveedor asociado a este requerimiento." });
                }

                // Validar que al menos NumCer esté presente para generar la ruta
                if (string.IsNullOrWhiteSpace(numCer))
                {
                    return Json(new { tipo = "Advertencia", mensaje = "El Nº Certificado es obligatorio." });
                }

                // Validar longitud máxima permitida por Oracle (columna NUM_CER VARCHAR2(30))
                if (numCer.Length > 30)
                {
                    return Json(new { tipo = "Advertencia", mensaje = $"El Nº Certificado no puede superar los 30 caracteres (ingresó {numCer.Length})." });
                }

                // Observación es opcional (columna OBSERVACION VARCHAR2(200))
                if (!string.IsNullOrEmpty(observacion) && observacion.Length > 200)
                {
                    return Json(new { tipo = "Advertencia", mensaje = $"La Observación no puede superar los 200 caracteres (ingresó {observacion.Length})." });
                }

                // Generar ruta del PDF (carpeta por RUC del proveedor). El servicio ya se
                // autentica en el recurso de red compartido internamente.
                var rutaPdf = await _cargaTcFibraService.GenerarRutaPdfCertificado(requerimiento.Ruc, numCer);

                // Guardar el archivo usando el servicio centralizado (magic bytes + validación)
                var carpetaPdf = Path.GetDirectoryName(rutaPdf) ?? "";
                var nombreBase = Path.GetFileNameWithoutExtension(rutaPdf);
                var resPdf = await _procesador.GuardarAsync(archivo, carpetaPdf, nombreBase, PerfilArchivo.SoloPdf);
                if (!resPdf.Ok)
                    return Json(new { tipo = "Error", mensaje = resPdf.Error });

                _logger.LogInformation("PDF de fibra guardado en: {RutaPdf}", rutaPdf);

                // Actualizar los datos en la base de datos
                var modelo = new ActualizarCertificadoDto
                {
                    NumReq = numReq,
                    NumCer = numCer,
                    Observacion = observacion
                };

                var usuario = HttpContext.Session.GetString("OracleUser") ?? "SYSTEM";
                var actualizado = await _cargaTcFibraService.ActualizarCertificadoAsync(modelo, usuario);

                if (actualizado)
                {
                    TempData["Success"] = "Certificado y PDF cargados correctamente.";
                    return Json(new
                    {
                        tipo = "Exito",
                        redirectUrl = Url.Action("Detalle", new { numReq, t = Request.Query["t"].ToString() })
                    });
                }
                else
                {
                    return Json(new { tipo = "Advertencia", mensaje = "PDF guardado pero no se pudo actualizar la base de datos." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar PDF de fibra para NUM_REQ {NumReq}", numReq);
                return Json(new { tipo = "Error", mensaje = "Ocurrió un error al cargar el PDF. Contacte al administrador." });
            }
        }

        // ========== VISUALIZAR PDF ==========

        [HttpGet]
        public async Task<IActionResult> VisualizarPdf(int numReq)
        {
            try
            {
                var requerimiento = await _cargaTcFibraService.ObtenerRequerimientoAsync(numReq);
                if (requerimiento == null)
                {
                    _logger.LogWarning("No se encontró el requerimiento de fibra NUM_REQ={NumReq}", numReq);
                    return Content("<html><body><h3>No se encontró el requerimiento.</h3></body></html>", "text/html");
                }

                if (string.IsNullOrEmpty(requerimiento.NumCer))
                {
                    _logger.LogWarning("El requerimiento de fibra NUM_REQ={NumReq} no tiene certificado cargado", numReq);
                    return Content("<html><body><h3>El requerimiento no tiene certificado cargado.</h3></body></html>", "text/html");
                }

                if (string.IsNullOrEmpty(requerimiento.Ruc))
                {
                    _logger.LogWarning("No se encontró el RUC del proveedor para NUM_REQ={NumReq}", numReq);
                    return Content("<html><body><h3>No se encontró el RUC del proveedor.</h3></body></html>", "text/html");
                }

                // Generar ruta del PDF. El servicio ya se autentica en el recurso de red
                // compartido internamente.
                var rutaPdf = await _cargaTcFibraService.GenerarRutaPdfCertificado(requerimiento.Ruc, requerimiento.NumCer);

                _logger.LogInformation("Intentando cargar PDF de fibra desde: {RutaPdf}", rutaPdf);

                if (!System.IO.File.Exists(rutaPdf))
                {
                    _logger.LogWarning("No se encontró el archivo PDF en: {RutaPdf}", rutaPdf);
                    return Content($"<html><body><h3>No se encontró el PDF</h3><p>Ruta: {rutaPdf}</p></body></html>", "text/html");
                }

                var pdfBytes = await System.IO.File.ReadAllBytesAsync(rutaPdf);
                var nombreArchivo = Path.GetFileName(rutaPdf);

                _logger.LogInformation("PDF de fibra cargado exitosamente: {NombreArchivo}, Tamaño: {Size} bytes", nombreArchivo, pdfBytes.Length);

                // Content-Disposition: inline para visualizar en el navegador
                Response.Headers.Append("Content-Disposition", $"inline; filename=\"{nombreArchivo}\"");
                return File(pdfBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al visualizar PDF de fibra para NUM_REQ {NumReq}", numReq);
                return Content("<html><body><h3>Error al cargar el PDF</h3><p>Ocurrió un error al procesar el archivo. Contacte al administrador.</p></body></html>", "text/html");
            }
        }

        // ========== DESCARGAR PDF ==========

        [HttpGet]
        public async Task<IActionResult> DescargarPdf(int numReq)
        {
            try
            {
                var requerimiento = await _cargaTcFibraService.ObtenerRequerimientoAsync(numReq);
                if (requerimiento == null)
                {
                    return Json(new { tipo = "Error", mensaje = "No se encontró el requerimiento." });
                }

                if (string.IsNullOrEmpty(requerimiento.NumCer))
                {
                    return Json(new { tipo = "Advertencia", mensaje = "El requerimiento no tiene certificado cargado." });
                }

                if (string.IsNullOrEmpty(requerimiento.Ruc))
                {
                    return Json(new { tipo = "Error", mensaje = "No se encontró el RUC del proveedor." });
                }

                // Generar ruta del PDF. El servicio ya se autentica en el recurso de red
                // compartido internamente.
                var rutaPdf = await _cargaTcFibraService.GenerarRutaPdfCertificado(requerimiento.Ruc, requerimiento.NumCer);

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
                _logger.LogError(ex, "Error al descargar PDF de fibra para NUM_REQ {NumReq}", numReq);
                return Json(new { tipo = "Error", mensaje = $"Error al descargar PDF: {ex.Message}" });
            }
        }
    }
}
