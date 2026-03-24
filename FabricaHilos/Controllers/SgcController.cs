using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using FabricaHilos.Models.Sgc;
using FabricaHilos.Services.Sgc;

namespace FabricaHilos.Controllers
{
    [Authorize]
    public class SgcController : Controller
    {
        private readonly ISgcService _sgcService;
            private readonly ILogger<SgcController> _logger;
            private readonly IConfiguration _configuration;

            public SgcController(ISgcService sgcService, ILogger<SgcController> logger, IConfiguration configuration)
            {
                _sgcService    = sgcService;
                _logger        = logger;
                _configuration = configuration;
            }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            base.OnActionExecuting(context);
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("OracleUser")))
            {
                _logger.LogWarning("Sesión Oracle expirada en SGC. Redirigiendo al login.");
                TempData["Warning"] = "Su sesión Oracle ha expirado. Por favor, inicie sesión nuevamente.";
                context.Result = RedirectToAction("Login", "Account",
                    new { returnUrl = Request.Path + Request.QueryString });
            }
        }

        // ========== INDEX ==========

        public IActionResult Index()
        {
            return View();
        }

        // ========== PEDIDOS (ESTADO <> '9') ==========

        public async Task<IActionResult> Pedidos(string? buscar, int page = 1)
        {
            const int pageSize = 10;
            var resultado       = await _sgcService.ObtenerPedidosAsync(buscar, page, pageSize);
            if (!resultado.Items.Any() && page > 1)
                return RedirectToAction(nameof(Pedidos), new { buscar, page = 1 });

            ViewBag.Buscar     = buscar;
            ViewBag.Page       = page;
            ViewBag.PageSize   = pageSize;
            ViewBag.TotalCount = resultado.TotalCount;
            ViewBag.TotalPages = resultado.TotalCount == 0 ? 1 : (int)Math.Ceiling((double)resultado.TotalCount / pageSize);
            return View(resultado.Items);
        }

        // ========== DETALLE DE PEDIDO (ITEMPED) ==========

        public async Task<IActionResult> DetallePedido(int serie, int numPed, string? buscar = null, int page = 1)
        {
            var pedido = await _sgcService.ObtenerPedidoAsync(serie, numPed);
            if (pedido == null) return NotFound();

            const int pageSize = 10;
            var resultado = await _sgcService.ObtenerDetallePedidoAsync(serie, numPed, page, pageSize);
            if (!resultado.Items.Any() && page > 1)
                return RedirectToAction(nameof(DetallePedido), new { serie, numPed, buscar, page = 1 });

            ViewBag.Pedido     = pedido;
            ViewBag.Buscar     = buscar;
            ViewBag.Page       = page;
            ViewBag.PageSize   = pageSize;
            ViewBag.TotalCount = resultado.TotalCount;
            ViewBag.TotalPages = resultado.TotalCount == 0 ? 1 : (int)Math.Ceiling((double)resultado.TotalCount / pageSize);
            ViewBag.TieneGuias = await _sgcService.TieneGuiasAsync(serie, numPed);
            return View(resultado.Items);
        }

        // ========== GUÍAS (KARDEX_G) ==========

        public async Task<IActionResult> Guias(int pedSerie, int numPed, int nro, string? buscar = null, int page = 1)
        {
            const int pageSize = 10;
            var resultado = await _sgcService.ObtenerGuiasAsync(pedSerie, numPed, page, pageSize);
            if (!resultado.Items.Any() && page > 1)
                return RedirectToAction(nameof(Guias), new { pedSerie, numPed, nro, buscar, page = 1 });

            var pedido  = await _sgcService.ObtenerPedidoAsync(pedSerie, numPed);
            var itemPed = await _sgcService.ObtenerItemPedAsync(numPed, nro);

            ViewBag.Pedido     = pedido;
            ViewBag.Buscar     = buscar;
            ViewBag.ItemPed    = itemPed;
            ViewBag.PedSerie   = pedSerie;
            ViewBag.NumPed     = numPed;
            ViewBag.ItemNro    = nro;
            ViewBag.Page       = page;
            ViewBag.PageSize   = pageSize;
            ViewBag.TotalCount = resultado.TotalCount;
            ViewBag.TotalPages = resultado.TotalCount == 0 ? 1 : (int)Math.Ceiling((double)resultado.TotalCount / pageSize);
            return View(resultado.Items);
        }

        // ========== DETALLE DE GUÍA (KARDEX_D) ==========

        public async Task<IActionResult> DetalleGuia(string codAlm, string tpTransac, int serie, int numero,
            int pedSerie, int numPed, int nro, string codArt, string? buscar = null, int page = 1)
        {
            var guia = await _sgcService.ObtenerGuiaAsync(codAlm, tpTransac, serie, numero);
            if (guia == null) return NotFound();

            const int pageSize = 10;
            var resultado = await _sgcService.ObtenerDetalleGuiaAsync(codAlm, tpTransac, serie, numero, codArt, page, pageSize);
            if (!resultado.Items.Any() && page > 1)
                return RedirectToAction(nameof(DetalleGuia), new { codAlm, tpTransac, serie, numero, pedSerie, numPed, nro, codArt, buscar, page = 1 });

            ViewBag.Guia       = guia;
            ViewBag.Buscar     = buscar;
            ViewBag.PedSerie   = pedSerie;
            ViewBag.NumPed     = numPed;
            ViewBag.ItemNro    = nro;
            ViewBag.CodArt     = codArt;
            ViewBag.Page       = page;
            ViewBag.PageSize   = pageSize;
            ViewBag.TotalCount = resultado.TotalCount;
            ViewBag.TotalPages = resultado.TotalCount == 0 ? 1 : (int)Math.Ceiling((double)resultado.TotalCount / pageSize);
            return View(resultado.Items);
        }

        // ========== FACTURAS (DOCUVENT) ==========

        public async Task<IActionResult> Facturas(string? cTipo, string? cSerie, string? cNumero,
            string? codAlm, string? tpTransac, int guiaSerie, int guiaNumero,
            int pedSerie, int numPed, int itemNro, int kdNro,
            string? codArt = null, int? packingSerie = null, int? packingNumero = null,
            string? buscar = null, int page = 1)
        {
            const int pageSize = 10;
            bool fromPacking = packingSerie.HasValue && packingNumero.HasValue;

            KardexGDto?  guia    = null;
            PackingGDto? packing = null;
            (List<DocuVentDto> Items, int TotalCount) resultado;

            if (fromPacking)
            {
                packing   = await _sgcService.ObtenerPackingAsync(cTipo ?? string.Empty, packingSerie!.Value, packingNumero!.Value);
                resultado = await _sgcService.ObtenerFacturasPorPackingAsync(cTipo ?? string.Empty, packingSerie.Value, packingNumero.Value, page, pageSize);
            }
            else
            {
                guia      = await _sgcService.ObtenerGuiaAsync(codAlm ?? string.Empty, tpTransac ?? string.Empty, guiaSerie, guiaNumero);
                resultado = await _sgcService.ObtenerFacturasAsync(cTipo, cSerie, cNumero, page, pageSize);
            }

            if (!resultado.Items.Any() && page > 1)
                return RedirectToAction(nameof(Facturas), new { cTipo, cSerie, cNumero, codAlm, tpTransac, guiaSerie, guiaNumero, pedSerie, numPed, itemNro, kdNro, codArt, packingSerie, packingNumero, buscar, page = 1 });

            ViewBag.Guia          = guia;
            ViewBag.Packing       = packing;
            ViewBag.FromPacking   = fromPacking;
            ViewBag.PackingSerie  = packingSerie;
            ViewBag.PackingNumero = packingNumero;
            ViewBag.Buscar        = buscar;
            ViewBag.CTipo         = cTipo;
            ViewBag.CSerie        = cSerie;
            ViewBag.CNumero       = cNumero;
            ViewBag.CodAlm        = codAlm ?? string.Empty;
            ViewBag.TpTransac     = tpTransac ?? string.Empty;
            ViewBag.GuiaSerie     = guiaSerie;
            ViewBag.GuiaNumero    = guiaNumero;
            ViewBag.PedSerie      = pedSerie;
            ViewBag.NumPed        = numPed;
            ViewBag.ItemNro       = itemNro;
            ViewBag.KdNro         = kdNro;
            ViewBag.CodArt        = codArt;
            ViewBag.SinFactura    = !fromPacking && string.IsNullOrEmpty(cTipo);
            ViewBag.Page          = page;
            ViewBag.PageSize      = pageSize;
            ViewBag.TotalCount    = resultado.TotalCount;
            ViewBag.TotalPages    = resultado.TotalCount == 0 ? 1 : (int)Math.Ceiling((double)resultado.TotalCount / pageSize);
            return View(resultado.Items);
        }

        // ========== DETALLE DE FACTURA (ITEMDOCU) ==========

        public async Task<IActionResult> DetalleFactura(string tipo, string serie, string numero,
            string codAlm, string tpTransac, int guiaSerie, int guiaNumero,
            int pedSerie, int numPed, int itemNro, string? buscar = null,
            bool fromPacking = false, int? packingSerie = null, int? packingNumero = null,
            string? cTipo = null, int page = 1)
        {
            var factura = await _sgcService.ObtenerFacturaAsync(tipo, serie, numero);
            if (factura == null) return NotFound();

            const int pageSize = 10;
            var resultado = await _sgcService.ObtenerDetalleFacturaAsync(tipo, serie, numero, page, pageSize);
            if (!resultado.Items.Any() && page > 1)
                return RedirectToAction(nameof(DetalleFactura), new { tipo, serie, numero, codAlm, tpTransac, guiaSerie, guiaNumero, pedSerie, numPed, itemNro, buscar, fromPacking, packingSerie, packingNumero, cTipo, page = 1 });

            ViewBag.Factura       = factura;
            ViewBag.Buscar        = buscar;
            ViewBag.CodAlm        = codAlm;
            ViewBag.TpTransac     = tpTransac;
            ViewBag.GuiaSerie     = guiaSerie;
            ViewBag.GuiaNumero    = guiaNumero;
            ViewBag.PedSerie      = pedSerie;
            ViewBag.NumPed        = numPed;
            ViewBag.ItemNro       = itemNro;
            ViewBag.FromPacking   = fromPacking;
            ViewBag.PackingSerie  = packingSerie;
            ViewBag.PackingNumero = packingNumero;
            ViewBag.CTipo         = cTipo;
            ViewBag.Page          = page;
            ViewBag.PageSize      = pageSize;
            ViewBag.TotalCount    = resultado.TotalCount;
            ViewBag.TotalPages    = resultado.TotalCount == 0 ? 1 : (int)Math.Ceiling((double)resultado.TotalCount / pageSize);
            return View(resultado.Items);
        }

        // ========== PACKING (PACKING_G) ==========

        public async Task<IActionResult> Packing(int pedSerie, int numPed, string? buscar = null, int page = 1)
        {
            var pedido = await _sgcService.ObtenerPedidoAsync(pedSerie, numPed);
            if (pedido == null) return NotFound();

            const int pageSize = 10;
            var resultado = await _sgcService.ObtenerPackingsAsync(numPed, page, pageSize);
            if (!resultado.Items.Any() && page > 1)
                return RedirectToAction(nameof(Packing), new { pedSerie, numPed, buscar, page = 1 });

            ViewBag.Pedido       = pedido;
            ViewBag.PedSerie     = pedSerie;
            ViewBag.NumPed       = numPed;
            ViewBag.NumOrdcompra = resultado.Items.FirstOrDefault()?.NumOrdcompra;
            ViewBag.Buscar       = buscar;
            ViewBag.Page         = page;
            ViewBag.PageSize     = pageSize;
            ViewBag.TotalCount   = resultado.TotalCount;
            ViewBag.TotalPages   = resultado.TotalCount == 0 ? 1 : (int)Math.Ceiling((double)resultado.TotalCount / pageSize);
            return View(resultado.Items);
        }

        // ========== PDF DOWNLOADS ==========

        public async Task<IActionResult> DescargarGuiaPdf(string codAlm, string tpTransac, int serie, int numero,
            int pedSerie, int numPed, int nro, string codArt)
        {
            var guia = await _sgcService.ObtenerGuiaAsync(codAlm, tpTransac, serie, numero);
            if (guia == null)
                return Json(new { tipo = "Error", mensaje = "No se encontró la guía." });

            var faltantesGuia = new List<string>();
            if (guia.FchTransac == null)              faltantesGuia.Add("Fecha de Transacción");
            if (string.IsNullOrEmpty(guia.Ruc))        faltantesGuia.Add("RUC");
            if (string.IsNullOrEmpty(guia.SerieSunat)) faltantesGuia.Add("Serie SUNAT");
            if (faltantesGuia.Count > 0)
            {
                var msg = $"La guía no tiene los datos necesarios para construir la ruta del PDF.\n\n"
                    + $"- Fecha de Transacción: {guia.FchTransac?.ToString("dd/MM/yyyy") ?? "[nula]"}\n"
                    + $"- RUC: {(string.IsNullOrEmpty(guia.Ruc) ? "[nulo]" : guia.Ruc)}\n"
                    + $"- Serie SUNAT: {(string.IsNullOrEmpty(guia.SerieSunat) ? "[nula]" : guia.SerieSunat)}.\n\n"
                    + $"Falta: {string.Join(", ", faltantesGuia)}.";
                return Json(new { tipo = "Error", mensaje = msg });
            }

            var fecha         = guia.FchTransac!.Value;
            var rutaProv      = _configuration["RutaProv"] ?? string.Empty;
            var rucEmpresa    = _configuration["RucEmpresa"] ?? string.Empty;
            var nroFormato    = guia.Numero.ToString("D8");
            var nombreArchivo = $"{rucEmpresa}-09-{guia.SerieSunat}-{nroFormato}.pdf";

            var rutaPdf = !string.IsNullOrEmpty(rutaProv)
                ? Path.Combine(rutaProv, fecha.ToString("yyyyMMdd"), nombreArchivo)
                : string.Empty;

            if (!string.IsNullOrEmpty(rutaPdf) && System.IO.File.Exists(rutaPdf))
                return File(await System.IO.File.ReadAllBytesAsync(rutaPdf), "application/pdf", nombreArchivo);

            return Json(new { tipo = "Advertencia", mensaje = $"No se encontró el PDF. Fecha: {fecha:dd/MM/yyyy}\nRuta: {rutaPdf}" });
        }

        public async Task<IActionResult> DescargarFacturaPdf(string tipo, string serie, string numero,
            string codAlm, string tpTransac, int guiaSerie, int guiaNumero,
            int pedSerie, int numPed, int itemNro)
        {
            var factura = await _sgcService.ObtenerFacturaAsync(tipo, serie, numero);
            if (factura == null)
                return Json(new { tipo = "Error", mensaje = "No se encontró la factura." });

            var faltantesFactura = new List<string>();
            if (factura.Fecha == null)               faltantesFactura.Add("Fecha de Facturación");
            if (string.IsNullOrEmpty(factura.Ruc))   faltantesFactura.Add("RUC");
            if (string.IsNullOrEmpty(factura.Serie))  faltantesFactura.Add("Serie SUNAT");
            if (faltantesFactura.Count > 0)
            {
                var errorMsg = $"La factura no tiene los datos necesarios para construir la ruta del PDF. "
                    + $"Fecha de Facturación: {factura.Fecha?.ToString("dd/MM/yyyy") ?? "[nula]"} — "
                    + $"RUC: {(string.IsNullOrEmpty(factura.Ruc) ? "[nulo]" : factura.Ruc)} — "
                    + $"Serie SUNAT: {(string.IsNullOrEmpty(factura.Serie) ? "[nula]" : factura.Serie.Trim())}. "
                    + $"Falta: {string.Join(", ", faltantesFactura)}. ";
                return Json(new { tipo = "Error", mensaje = errorMsg });
            }

            var fecha         = factura.Fecha!.Value;
            var rutaProv      = _configuration["RutaProv"] ?? string.Empty;
            var rucEmpresa    = _configuration["RucEmpresa"] ?? string.Empty;
            var nroFormato    = (factura.Numero ?? string.Empty).Trim().PadLeft(8, '0');
            var nombreArchivo = $"{rucEmpresa}-01-{factura.Serie!.Trim()}-{nroFormato}.pdf";

            var rutaPdf = !string.IsNullOrEmpty(rutaProv)
                ? Path.Combine(rutaProv, fecha.ToString("yyyyMMdd"), nombreArchivo)
                : string.Empty;

            if (!string.IsNullOrEmpty(rutaPdf) && System.IO.File.Exists(rutaPdf))
                return File(await System.IO.File.ReadAllBytesAsync(rutaPdf), "application/pdf", nombreArchivo);

            return Json(new { tipo = "Advertencia", mensaje = $"No se encontró el PDF. Fecha: {fecha:dd/MM/yyyy}\nRuta: {rutaPdf}" });
        }
    }
}
