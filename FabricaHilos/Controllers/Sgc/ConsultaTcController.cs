using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FabricaHilos.Services;
using FabricaHilos.Services.Sgc;

namespace FabricaHilos.Controllers.Sgc
{
    [Authorize]
    public class ConsultaTcController : OracleBaseController
    {
        private readonly ICargaTcService _cargaTcService;
        private readonly ILogger<ConsultaTcController> _logger;
        private readonly INavTokenService _navToken;

        public ConsultaTcController(
            ICargaTcService cargaTcService, 
            ILogger<ConsultaTcController> logger, 
            INavTokenService navToken)
        {
            _cargaTcService = cargaTcService;
            _logger = logger;
            _navToken = navToken;
        }

        // ========== LISTADO DE REQUERIMIENTOS (SOLO LECTURA) ==========

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

            return View("~/Views/Ventas/ConsultaTc/Index.cshtml", resultado.Items);
        }

        // ========== DETALLE DE REQUERIMIENTO (SOLO LECTURA) ==========

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

            return View("~/Views/Ventas/ConsultaTc/Detalle.cshtml", detalles);
        }

        // ========== VISUALIZAR PDF (READONLY) ==========

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

                // Obtener la ruta del PDF
                var cliente = await _cargaTcService.ObtenerClientePorCodigoAsync(requerimiento.CodCliente);
                if (cliente == null || string.IsNullOrEmpty(cliente.Ruc))
                {
                    _logger.LogError("No se encontró el RUC para el cliente {CodCliente}", requerimiento.CodCliente);
                    return Content("<html><body><h3>No se pudo obtener el RUC del cliente.</h3></body></html>", "text/html");
                }

                var rutaPdf = await _cargaTcService.GenerarRutaPdfCertificado(cliente.Ruc, requerimiento.NumCer);

                // Autenticarse en el recurso de red compartido antes de leer
                EnsureNetworkShare(rutaPdf);

                if (!System.IO.File.Exists(rutaPdf))
                {
                    _logger.LogWarning("El archivo PDF no existe en la ruta: {RutaPdf}", rutaPdf);
                    return Content("<html><body><h3>El archivo PDF del certificado no existe.</h3></body></html>", "text/html");
                }

                var pdfBytes = await System.IO.File.ReadAllBytesAsync(rutaPdf);
                return File(pdfBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al visualizar PDF para NUM_REQ {NumReq}", numReq);
                return Content($"<html><body><h3>Error al cargar el PDF: {ex.Message}</h3></body></html>", "text/html");
            }
        }
    }
}
