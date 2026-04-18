using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FabricaHilos.Models.Sgc;
using FabricaHilos.Services;
using FabricaHilos.Services.Sgc;
using FabricaHilos.Notificaciones.Abstractions;
using FabricaHilos.Notificaciones.Models.Payloads;

namespace FabricaHilos.Controllers.Sgc
{
    [Authorize]
    public class CargaTcController : OracleBaseController
    {
        private readonly ICargaTcService _cargaTcService;
        private readonly ILogger<CargaTcController> _logger;
        private readonly INavTokenService _navToken;
        private readonly IEmailNotificacionService _emailService;
        private readonly IConfiguration _configuration;

        public CargaTcController(
            ICargaTcService cargaTcService, 
            ILogger<CargaTcController> logger, 
            INavTokenService navToken,
            IEmailNotificacionService emailService,
            IConfiguration configuration)
        {
            _cargaTcService = cargaTcService;
            _logger = logger;
            _navToken = navToken;
            _emailService = emailService;
            _configuration = configuration;
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
                    return Json(new { tipo = "Advertencia", mensaje = "El Archivo PDF del certificado es obligatorio." });

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
                    return Json(new { tipo = "Advertencia", mensaje = "El Nº Certificado es obligatorio." });
                }

                // Autenticarse en el recurso de red antes de cualquier operación
                var rutaBase = _configuration["RutaCertificados"] ?? @"\\10.0.7.14\6-20100096260\Certificados";
                var username = _configuration["NetworkShare:Username"];
                var password = _configuration["NetworkShare:Password"];
                var domain = _configuration["NetworkShare:Domain"];

                try
                {
                    if (OperatingSystem.IsWindows())
                    {
                        FabricaHilos.Helpers.NetworkShareHelper.Connect(rutaBase, username, password, domain);
                        _logger.LogInformation("Autenticación exitosa en el recurso de red: {RutaBase}", rutaBase);
                    }
                }
                catch (Exception exAuth)
                {
                    _logger.LogError(exAuth, "Error al autenticarse en el recurso de red");
                    return Json(new { tipo = "Error", mensaje = $"Error al conectar con el recurso de red: {exAuth.Message}" });
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
                    _logger.LogWarning("No se encontró el requerimiento NUM_REQ={NumReq}", numReq);
                    return Content("<html><body><h3>No se encontró el requerimiento.</h3></body></html>", "text/html");
                }

                if (string.IsNullOrEmpty(requerimiento.NumCer))
                {
                    _logger.LogWarning("El requerimiento NUM_REQ={NumReq} no tiene certificado cargado", numReq);
                    return Content("<html><body><h3>El requerimiento no tiene certificado cargado.</h3></body></html>", "text/html");
                }

                // Intentar obtener el RUC directamente del requerimiento primero
                string? ruc = requerimiento.Ruc;

                // Si no tiene RUC directo, intentar obtenerlo del cliente
                if (string.IsNullOrEmpty(ruc) && !string.IsNullOrEmpty(requerimiento.CodCliente))
                {
                    var cliente = await _cargaTcService.ObtenerClientePorCodigoAsync(requerimiento.CodCliente);
                    ruc = cliente?.Ruc;
                }

                if (string.IsNullOrEmpty(ruc))
                {
                    _logger.LogWarning("No se encontró el RUC para NUM_REQ={NumReq}, CodCliente={CodCliente}", 
                        numReq, requerimiento.CodCliente);
                    return Content("<html><body><h3>No se encontró el RUC del cliente.</h3></body></html>", "text/html");
                }

                // Generar ruta del PDF
                var rutaPdf = await _cargaTcService.GenerarRutaPdfCertificado(ruc, requerimiento.NumCer);

                _logger.LogInformation("Intentando cargar PDF desde: {RutaPdf}", rutaPdf);

                if (!System.IO.File.Exists(rutaPdf))
                {
                    _logger.LogWarning("No se encontró el archivo PDF en: {RutaPdf}", rutaPdf);
                    return Content($"<html><body><h3>No se encontró el PDF</h3><p>Ruta: {rutaPdf}</p></body></html>", "text/html");
                }

                var pdfBytes = await System.IO.File.ReadAllBytesAsync(rutaPdf);
                var nombreArchivo = Path.GetFileName(rutaPdf);

                _logger.LogInformation("PDF cargado exitosamente: {NombreArchivo}, Tamaño: {Size} bytes", nombreArchivo, pdfBytes.Length);

                // Content-Disposition: inline para visualizar en el navegador
                Response.Headers.Append("Content-Disposition", $"inline; filename=\"{nombreArchivo}\"");
                return File(pdfBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al visualizar PDF para NUM_REQ {NumReq}", numReq);
                return Content($"<html><body><h3>Error al cargar el PDF</h3><p>{ex.Message}</p><pre>{ex.StackTrace}</pre></body></html>", "text/html");
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

                if (string.IsNullOrEmpty(requerimiento.NumCer))
                {
                    return Json(new { tipo = "Advertencia", mensaje = "El requerimiento no tiene certificado cargado." });
                }

                // Intentar obtener el RUC directamente del requerimiento primero
                string? ruc = requerimiento.Ruc;

                // Si no tiene RUC directo, intentar obtenerlo del cliente
                if (string.IsNullOrEmpty(ruc) && !string.IsNullOrEmpty(requerimiento.CodCliente))
                {
                    var cliente = await _cargaTcService.ObtenerClientePorCodigoAsync(requerimiento.CodCliente);
                    ruc = cliente?.Ruc;
                }

                if (string.IsNullOrEmpty(ruc))
                {
                    return Json(new { tipo = "Error", mensaje = "No se encontró el RUC del cliente." });
                }

                // Generar ruta del PDF
                var rutaPdf = await _cargaTcService.GenerarRutaPdfCertificado(ruc, requerimiento.NumCer);

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

        // ========== ENVIAR A FACTURACIÓN ==========

        [HttpPost]
        public async Task<IActionResult> EnviarAFacturacion(int numReq)
        {
            int? numeroVFactaut = null;
            var usuario = HttpContext.Session.GetString("OracleUser") ?? "SYSTEM";

            try
            {
                var requerimiento = await _cargaTcService.ObtenerRequerimientoAsync(numReq);
                if (requerimiento == null)
                {
                    return Json(new { success = false, message = "No se encontró el requerimiento especificado." });
                }

                if (string.IsNullOrEmpty(requerimiento.NumCer))
                {
                    return Json(new { success = false, message = "El requerimiento no tiene certificado asignado." });
                }

                if (string.IsNullOrEmpty(requerimiento.CodArt))
                {
                    return Json(new { success = false, message = "El requerimiento no tiene código de artículo." });
                }

                if (string.IsNullOrEmpty(requerimiento.CodVende))
                {
                    return Json(new { success = false, message = "El requerimiento no tiene código de vendedor." });
                }

                _logger.LogInformation("Iniciando proceso de envío a Facturación - REQ {NumReq}, Certificado {NumCer}", 
                    numReq, requerimiento.NumCer);

                // PASO 1: Registrar en V_FACTAUT antes de enviar el correo
                try
                {
                    numeroVFactaut = await _cargaTcService.RegistrarVFactautAsync(numReq, requerimiento.CodArt, usuario);
                    _logger.LogInformation("Registro V_FACTAUT creado exitosamente con NUMERO={Numero}", numeroVFactaut);
                }
                catch (Exception exVFactaut)
                {
                    _logger.LogError(exVFactaut, "Error al registrar en V_FACTAUT para REQ {NumReq}", numReq);
                    return Json(new { success = false, message = $"Error al registrar documento: {exVFactaut.Message}" });
                }

                // PASO 2: Preparar y enviar el correo
                var (nroLista, importe) = await _cargaTcService.ObtenerDatosListaPreciosAsync(requerimiento.CodArt);
                var (nombreVendedor, emailVendedor) = await _cargaTcService.ObtenerDatosVendedorAsync(requerimiento.CodVende);

                var detalles = await _cargaTcService.ObtenerDetalleRequerimientoAsync(numReq);
                var totalFacturas = detalles.Count;

                // Obtener partidas y órdenes de compra
                var partidas = await _cargaTcService.ObtenerPartidasPorRequerimientoAsync(numReq);
                var ordenesCompra = await _cargaTcService.ObtenerOrdenesCompraPorRequerimientoAsync(numReq);

                // Formatear partidas y OC para el correo
                string partidasTexto = partidas.Any() 
                    ? string.Join("\n", partidas.Select(p => p.PartidaItem)) 
                    : "No disponible";

                string ordenesCompraTexto = ordenesCompra.Any() 
                    ? string.Join("\n", ordenesCompra.Select(oc => oc.OrdenCompra)) 
                    : "No disponible";

                var tipoCertificado = requerimiento.CodArt.Replace("CERT", "");
                string monedaTexto = nroLista == "2" ? "DOLARES" : (nroLista ?? "N/A");

                var destinatarioFacturacion = _configuration["CorreoFacturacion"] ?? "iramirez@colonial.com.pe";
                var copiaFacturacion = _configuration["CorreoFacturacionCopia"];

                var payload = new EnvioCertificadoFacturacionPayload
                {
                    CorreoDestinatario = destinatarioFacturacion,
                    NombreDestinatario = "Facturación",
                    CorreoCopia = copiaFacturacion,
                    NumRequerimiento = requerimiento.NumReq.ToString(),
                    FechaRequerimiento = requerimiento.Fecha?.ToString("dd/MM/yyyy") ?? "N/A",
                    TipoCertificado = tipoCertificado,
                    NumCertificado = requerimiento.NumCer,
                    CodCliente = requerimiento.Ruc ?? requerimiento.CodCliente ?? "N/A",
                    NombreCliente = requerimiento.RazonSocial ?? "N/A",
                    CodVendedor = requerimiento.CodVende ?? "N/A",
                    NombreVendedor = nombreVendedor ?? "N/A",
                    Moneda = monedaTexto,
                    Importe = importe?.ToString("N2") ?? "0.00",
                    TotalFacturas = totalFacturas.ToString(),
                    Partidas = partidasTexto,
                    OrdenesCompra = ordenesCompraTexto
                };

                try
                {
                    await _emailService.EnviarAsync(payload);
                    _logger.LogInformation("Correo enviado exitosamente a {Email} para REQ {NumReq}", 
                        destinatarioFacturacion, numReq);
                }
                catch (Exception exEmail)
                {
                    _logger.LogError(exEmail, "Error al enviar correo para REQ {NumReq}", numReq);
                    return Json(new { success = false, message = $"Error al enviar correo de notificación: {exEmail.Message}" });
                }

                // PASO 3: Actualizar ESTADO en REQ_CERT después de envío exitoso del correo
                var estadoActualizado = await _cargaTcService.ActualizarEstadoReqCertAsync(numReq, 2, usuario);

                if (estadoActualizado)
                {
                    _logger.LogInformation("Estado de REQ_CERT actualizado a 2 para NUM_REQ={NumReq}", numReq);
                }
                else
                {
                    _logger.LogWarning("No se pudo actualizar el estado de REQ_CERT para NUM_REQ={NumReq}", numReq);
                }

                TempData["Success"] = $"El certificado {requerimiento.NumCer} ha sido enviado a Facturación correctamente.";

                return Json(new 
                { 
                    success = true, 
                    redirectUrl = Url.Action("Index", new { t = Request.Query["t"].ToString() })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general al enviar certificado a Facturación para NUM_REQ {NumReq}", numReq);
                return Json(new { success = false, message = $"Error al enviar a Facturación: {ex.Message}" });
            }
        }
    }
}
