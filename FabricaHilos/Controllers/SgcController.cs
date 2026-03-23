using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using FabricaHilos.Services.Sgc;

namespace FabricaHilos.Controllers
{
    [Authorize]
    public class SgcController : Controller
    {
        private readonly ISgcService _sgcService;
        private readonly ILogger<SgcController> _logger;

        public SgcController(ISgcService sgcService, ILogger<SgcController> logger)
        {
            _sgcService = sgcService;
            _logger     = logger;
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

        public async Task<IActionResult> DetallePedido(int serie, int numPed, int page = 1)
        {
            var pedido = await _sgcService.ObtenerPedidoAsync(serie, numPed);
            if (pedido == null) return NotFound();

            const int pageSize = 10;
            var resultado = await _sgcService.ObtenerDetallePedidoAsync(serie, numPed, page, pageSize);
            if (!resultado.Items.Any() && page > 1)
                return RedirectToAction(nameof(DetallePedido), new { serie, numPed, page = 1 });

            ViewBag.Pedido     = pedido;
            ViewBag.Page       = page;
            ViewBag.PageSize   = pageSize;
            ViewBag.TotalCount = resultado.TotalCount;
            ViewBag.TotalPages = resultado.TotalCount == 0 ? 1 : (int)Math.Ceiling((double)resultado.TotalCount / pageSize);
            return View(resultado.Items);
        }

        // ========== GUÍAS (KARDEX_G) ==========

        public async Task<IActionResult> Guias(int pedSerie, int numPed, int nro, int page = 1)
        {
            const int pageSize = 10;
            var resultado = await _sgcService.ObtenerGuiasAsync(pedSerie, numPed, page, pageSize);
            if (!resultado.Items.Any() && page > 1)
                return RedirectToAction(nameof(Guias), new { pedSerie, numPed, nro, page = 1 });

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
            int pedSerie, int numPed, int nro, int page = 1)
        {
            var guia = await _sgcService.ObtenerGuiaAsync(codAlm, tpTransac, serie, numero);
            if (guia == null) return NotFound();

            const int pageSize = 10;
            var resultado = await _sgcService.ObtenerDetalleGuiaAsync(codAlm, tpTransac, serie, numero, page, pageSize);
            if (!resultado.Items.Any() && page > 1)
                return RedirectToAction(nameof(DetalleGuia), new { codAlm, tpTransac, serie, numero, pedSerie, numPed, nro, page = 1 });

            ViewBag.Guia       = guia;
            ViewBag.PedSerie   = pedSerie;
            ViewBag.NumPed     = numPed;
            ViewBag.ItemNro    = nro;
            ViewBag.Page       = page;
            ViewBag.PageSize   = pageSize;
            ViewBag.TotalCount = resultado.TotalCount;
            ViewBag.TotalPages = resultado.TotalCount == 0 ? 1 : (int)Math.Ceiling((double)resultado.TotalCount / pageSize);
            return View(resultado.Items);
        }

        // ========== FACTURAS (DOCUVENT) ==========

        public async Task<IActionResult> Facturas(string? cTipo, string? cSerie, string? cNumero,
            string codAlm, string tpTransac, int guiaSerie, int guiaNumero,
            int pedSerie, int numPed, int itemNro, int kdNro, int page = 1)
        {
            const int pageSize = 10;
            var resultado = await _sgcService.ObtenerFacturasAsync(cTipo, cSerie, cNumero, page, pageSize);
            if (!resultado.Items.Any() && page > 1)
                return RedirectToAction(nameof(Facturas), new { cTipo, cSerie, cNumero, codAlm, tpTransac, guiaSerie, guiaNumero, pedSerie, numPed, itemNro, kdNro, page = 1 });

            ViewBag.CTipo       = cTipo;
            ViewBag.CSerie      = cSerie;
            ViewBag.CNumero     = cNumero;
            ViewBag.CodAlm      = codAlm;
            ViewBag.TpTransac   = tpTransac;
            ViewBag.GuiaSerie   = guiaSerie;
            ViewBag.GuiaNumero  = guiaNumero;
            ViewBag.PedSerie    = pedSerie;
            ViewBag.NumPed      = numPed;
            ViewBag.ItemNro     = itemNro;
            ViewBag.KdNro       = kdNro;
            ViewBag.SinFactura  = string.IsNullOrEmpty(cTipo);
            ViewBag.Page        = page;
            ViewBag.PageSize    = pageSize;
            ViewBag.TotalCount  = resultado.TotalCount;
            ViewBag.TotalPages  = resultado.TotalCount == 0 ? 1 : (int)Math.Ceiling((double)resultado.TotalCount / pageSize);
            return View(resultado.Items);
        }

        // ========== DETALLE DE FACTURA (ITEMDOCU) ==========

        public async Task<IActionResult> DetalleFactura(string tipo, string serie, string numero,
            string codAlm, string tpTransac, int guiaSerie, int guiaNumero,
            int pedSerie, int numPed, int itemNro, int page = 1)
        {
            var factura = await _sgcService.ObtenerFacturaAsync(tipo, serie, numero);
            if (factura == null) return NotFound();

            const int pageSize = 10;
            var resultado = await _sgcService.ObtenerDetalleFacturaAsync(tipo, serie, numero, page, pageSize);
            if (!resultado.Items.Any() && page > 1)
                return RedirectToAction(nameof(DetalleFactura), new { tipo, serie, numero, codAlm, tpTransac, guiaSerie, guiaNumero, pedSerie, numPed, itemNro, page = 1 });

            ViewBag.Factura     = factura;
            ViewBag.CodAlm      = codAlm;
            ViewBag.TpTransac   = tpTransac;
            ViewBag.GuiaSerie   = guiaSerie;
            ViewBag.GuiaNumero  = guiaNumero;
            ViewBag.PedSerie    = pedSerie;
            ViewBag.NumPed      = numPed;
            ViewBag.ItemNro     = itemNro;
            ViewBag.Page        = page;
            ViewBag.PageSize    = pageSize;
            ViewBag.TotalCount  = resultado.TotalCount;
            ViewBag.TotalPages  = resultado.TotalCount == 0 ? 1 : (int)Math.Ceiling((double)resultado.TotalCount / pageSize);
            return View(resultado.Items);
        }
    }
}
