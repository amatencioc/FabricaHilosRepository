using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FabricaHilos.Models.Sgc;
using FabricaHilos.Services;
using FabricaHilos.Services.Sgc;

namespace FabricaHilos.Controllers
{
    [Authorize]
    public class RelacionFacCliController : OracleBaseController
    {
        private readonly ISgcService _sgcService;
        private readonly ILogger<RelacionFacCliController> _logger;
        private readonly INavTokenService _navToken;

        public RelacionFacCliController(ISgcService sgcService, ILogger<RelacionFacCliController> logger, INavTokenService navToken)
        {
            _sgcService = sgcService;
            _logger = logger;
            _navToken = navToken;
        }

        // ========== LISTADO DE DESPACHOS

        [HttpGet]
        public async Task<IActionResult> ListadoDespachos(string? t = null, string? guia = null, string? pedido = null, 
            string? factura = null, string? razonSocial = null, DateTime? fechaInicio = null, DateTime? fechaFin = null, 
            bool? gots = null, bool? ocs = null, int page = 1)
        {
            // Cuando no hay ningún filtro (primera carga), aplicar rango de fechas por defecto (último mes)
            bool sinFiltros = string.IsNullOrEmpty(t)
                && guia == null && pedido == null && factura == null && razonSocial == null
                && !fechaInicio.HasValue && !fechaFin.HasValue && !gots.HasValue && !ocs.HasValue;

            if (sinFiltros)
            {
                fechaFin    = DateTime.Today;
                fechaInicio = DateTime.Today.AddDays(-30);
                gots        = true;
            }

            // Si hay filtros nuevos sin token, crear token y redirigir
            if (string.IsNullOrEmpty(t) && (guia != null || pedido != null || factura != null || razonSocial != null || fechaInicio.HasValue || fechaFin.HasValue || gots.HasValue || ocs.HasValue))
            {
                var token = _navToken.Protect(new Dictionary<string, string?> {
                    ["guia"]        = guia,
                    ["pedido"]      = pedido,
                    ["factura"]     = factura,
                    ["razonSocial"] = razonSocial,
                    ["fechaInicio"] = fechaInicio?.ToString("yyyy-MM-dd"),
                    ["fechaFin"]    = fechaFin?.ToString("yyyy-MM-dd"),
                    ["gots"]        = gots?.ToString(),
                    ["ocs"]         = ocs?.ToString()
                });
                return RedirectToAction(nameof(ListadoDespachos), new { t = token, page });
            }

            // Desempaquetar token
            if (!string.IsNullOrEmpty(t) && _navToken.TryUnprotect(t, out var nav))
            {
                guia        = nav.GetValueOrDefault("guia")        ?? guia;
                pedido      = nav.GetValueOrDefault("pedido")      ?? pedido;
                factura     = nav.GetValueOrDefault("factura")     ?? factura;
                razonSocial = nav.GetValueOrDefault("razonSocial") ?? razonSocial;
                if (DateTime.TryParse(nav.GetValueOrDefault("fechaInicio"), out var fi)) fechaInicio = fi;
                if (DateTime.TryParse(nav.GetValueOrDefault("fechaFin"),    out var ff)) fechaFin    = ff;
                if (bool.TryParse(nav.GetValueOrDefault("gots"), out var g)) gots = g;
                if (bool.TryParse(nav.GetValueOrDefault("ocs"),  out var o)) ocs  = o;
            }

            const int pageSize = 10;

            var resultado = await _sgcService.ObtenerListadoDespachosAsync(guia, pedido, factura, razonSocial, fechaInicio, fechaFin, gots, ocs, page, pageSize);

            if (!resultado.Items.Any() && page > 1)
                return RedirectToAction(nameof(ListadoDespachos), new { t, page = 1 });

            ViewBag.Guia        = guia;
            ViewBag.Pedido      = pedido;
            ViewBag.Factura     = factura;
            ViewBag.RazonSocial = razonSocial;
            ViewBag.FechaInicio = fechaInicio?.ToString("yyyy-MM-dd");
            ViewBag.FechaFin    = fechaFin?.ToString("yyyy-MM-dd");
            ViewBag.Gots        = gots;
            ViewBag.Ocs         = ocs;
            ViewBag.NavToken    = t;
            ViewBag.Page        = page;
            ViewBag.PageSize    = pageSize;
            ViewBag.TotalCount  = resultado.TotalCount;
            ViewBag.TotalPages  = resultado.TotalCount == 0 ? 1 : (int)Math.Ceiling((double)resultado.TotalCount / pageSize);

            return View("~/Views/Sgc/Despachos/RelacionFacCli/ListadoDespachos.cshtml", resultado.Items);
        }

        [HttpGet]
        public async Task<IActionResult> ExportarDespachosExcel(string? guia = null, string? pedido = null, 
            string? factura = null, string? razonSocial = null, DateTime? fechaInicio = null, DateTime? fechaFin = null, 
            bool? gots = null, bool? ocs = null)
        {
            // Decidir qué método llamar según si hay filtro de certificados
            var resultado = await _sgcService.ObtenerListadoDespachosAsync(guia, pedido, factura, razonSocial, fechaInicio, fechaFin, gots, ocs, 1, int.MaxValue);
            var items = resultado.Items;

            using var workbook  = new ClosedXML.Excel.XLWorkbook();
            var ws = workbook.Worksheets.Add("Listado de Despachos");

            // Headers
            string[] headers = { "#", "RAZON SOCIAL", "OC", "PEDIDO", "FACTURA", "FECHA.DOC", "ARTICULO", "CANTIDAD", "CANT_FACTURADA", "PRECIO", "GUIA", "OBS" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style.Font.Bold = true;
                ws.Cell(1, i + 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
            }

            // Datos
            int row = 2;
            foreach (var item in items)
            {
                ws.Cell(row, 1).Value  = row - 1;
                ws.Cell(row, 2).Value  = item.RazonSocial ?? "";
                ws.Cell(row, 3).Value  = item.Oc ?? "";
                ws.Cell(row, 4).Value  = item.Pedido ?? "";
                ws.Cell(row, 5).Value  = item.Factura ?? "";
                ws.Cell(row, 6).Value  = item.FechaDoc?.ToString("dd/MM/yyyy") ?? "";
                ws.Cell(row, 7).Value  = item.Articulo ?? "";
                ws.Cell(row, 8).Value  = item.Cantidad ?? 0;
                ws.Cell(row, 9).Value  = item.CantFacturada ?? 0;
                ws.Cell(row, 10).Value = item.Precio ?? 0;
                ws.Cell(row, 11).Value = item.Guia?.ToString() ?? "";
                ws.Cell(row, 12).Value = item.Obs ?? "";
                row++;
            }

            ws.Columns().AdjustToContents();

            using var ms = new System.IO.MemoryStream();
            workbook.SaveAs(ms);
            var fileName = $"ListadoDespachos_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        [HttpPost]
        public async Task<IActionResult> EnviarFacturasTC([FromBody] List<FacturaTcDto> facturas)
        {
            try
            {
                if (facturas == null || !facturas.Any())
                    return Json(new { tipo = "Advertencia", mensaje = "No se recibieron facturas para enviar." });

                // DEBUG: Log de facturas recibidas
                _logger.LogInformation("===== FACTURAS RECIBIDAS PARA TC =====");
                _logger.LogInformation("Total facturas: {Count}", facturas.Count);
                foreach (var f in facturas)
                {
                    _logger.LogInformation("  TIPO='{Tipo}' SERIE='{Serie}' NUMERO='{Numero}' COD_CLIENTE='{CodCliente}' COD_ART='{CodArt}'", 
                        f.Tipo, f.Serie, f.Numero, f.CodCliente, f.CodArt);
                }

                var numReq = await _sgcService.GuardarRequerimientoCertificadoAsync(facturas);

                return Json(new
                {
                    tipo = "Exito",
                    mensaje = $"Se creó el requerimiento #{numReq} con {facturas.Count} factura(s).",
                    numReq
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar facturas a TC");
                return Json(new { tipo = "Error", mensaje = $"Error al enviar facturas: {ex.Message}" });
            }
        }
    }
}
