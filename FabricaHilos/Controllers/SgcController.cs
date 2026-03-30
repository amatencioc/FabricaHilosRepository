using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using FabricaHilos.Helpers;
using FabricaHilos.Models.Sgc;
using FabricaHilos.Services;
using FabricaHilos.Services.Sgc;

namespace FabricaHilos.Controllers
{
    [Authorize]
    public class SgcController : Controller
    {
        private readonly ISgcService _sgcService;
            private readonly ILogger<SgcController> _logger;
            private readonly IConfiguration _configuration;
            private readonly ISalidaInternaPdfService _salidaInternaPdf;
            private readonly IWebHostEnvironment _env;
            private readonly INavTokenService _navToken;

            public SgcController(ISgcService sgcService, ILogger<SgcController> logger,
                IConfiguration configuration, ISalidaInternaPdfService salidaInternaPdf,
                IWebHostEnvironment env, INavTokenService navToken)
            {
                _sgcService       = sgcService;
                _logger           = logger;
                _salidaInternaPdf = salidaInternaPdf;
                _env              = env;
                _configuration    = configuration;
                _navToken         = navToken;
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

        public async Task<IActionResult> Pedidos(string? t = null, string? buscar = null, DateTime? fechaInicio = null, DateTime? fechaFin = null, int page = 1)
        {
            if (string.IsNullOrEmpty(t) && (buscar != null || fechaInicio.HasValue || fechaFin.HasValue))
            {
                var token = _navToken.Protect(new Dictionary<string, string?> {
                    ["buscar"]      = buscar,
                    ["fechaInicio"] = fechaInicio?.ToString("yyyy-MM-dd"),
                    ["fechaFin"]    = fechaFin?.ToString("yyyy-MM-dd")
                });
                return RedirectToAction(nameof(Pedidos), new { t = token, page });
            }
            if (!string.IsNullOrEmpty(t) && _navToken.TryUnprotect(t, out var nav))
            {
                buscar = nav.GetValueOrDefault("buscar");
                if (DateTime.TryParse(nav.GetValueOrDefault("fechaInicio"), out var fi)) fechaInicio = fi;
                if (DateTime.TryParse(nav.GetValueOrDefault("fechaFin"),    out var ff)) fechaFin    = ff;
            }
            var navToken = _navToken.Protect(new Dictionary<string, string?> {
                ["buscar"]      = buscar,
                ["fechaInicio"] = fechaInicio?.ToString("yyyy-MM-dd"),
                ["fechaFin"]    = fechaFin?.ToString("yyyy-MM-dd")
            });
            ViewBag.NavToken = navToken;

            const int pageSize = 10;
            var resultado = await _sgcService.ObtenerPedidosAsync(buscar, fechaInicio, fechaFin, page, pageSize);
            if (!resultado.Items.Any() && page > 1)
                return RedirectToAction(nameof(Pedidos), new { t, page = 1 });

            bool tieneFiltroPedido = !string.IsNullOrWhiteSpace(buscar) || fechaInicio.HasValue || fechaFin.HasValue;
            ViewBag.Buscar            = buscar;
            ViewBag.FechaInicio       = fechaInicio?.ToString("yyyy-MM-dd");
            ViewBag.FechaFin          = fechaFin?.ToString("yyyy-MM-dd");
            ViewBag.Page              = page;
            ViewBag.PageSize          = pageSize;
            ViewBag.TotalCount        = resultado.TotalCount;
            ViewBag.TotalPages        = resultado.TotalCount == 0 ? 1 : (int)Math.Ceiling((double)resultado.TotalCount / pageSize);
            ViewBag.TieneFiltroPedido = tieneFiltroPedido;
            ViewBag.SumTotalPedido   = resultado.SumTotalPedido;
            ViewBag.SumTotalDespacho = resultado.SumTotalDespacho;
            return View("Pedidos/Pedidos", resultado.Items);
        }

        // ========== DETALLE DE PEDIDO (ITEMPED) ==========

        public async Task<IActionResult> DetallePedido(string? t = null, int serie = 0, int numPed = 0, string? buscar = null, string? fechaInicio = null, string? fechaFin = null, int page = 1)
        {
            if (!string.IsNullOrEmpty(t) && _navToken.TryUnprotect(t, out var nav))
            {
                if (int.TryParse(nav.GetValueOrDefault("serie"),  out var s))  serie  = s;
                if (int.TryParse(nav.GetValueOrDefault("numPed"), out var np)) numPed = np;
                buscar      = nav.GetValueOrDefault("buscar")      ?? buscar;
                fechaInicio = nav.GetValueOrDefault("fechaInicio") ?? fechaInicio;
                fechaFin    = nav.GetValueOrDefault("fechaFin")    ?? fechaFin;
            }
            var navToken = _navToken.Protect(new Dictionary<string, string?> {
                ["serie"]       = serie.ToString(),
                ["numPed"]      = numPed.ToString(),
                ["buscar"]      = buscar,
                ["fechaInicio"] = fechaInicio,
                ["fechaFin"]    = fechaFin
            });
            ViewBag.NavToken = navToken;

            var pedido = await _sgcService.ObtenerPedidoAsync(serie, numPed);
            if (pedido == null) return NotFound();

            const int pageSize = 10;
            var resultado = await _sgcService.ObtenerDetallePedidoAsync(serie, numPed, page, pageSize);
            if (!resultado.Items.Any() && page > 1)
                return RedirectToAction(nameof(DetallePedido), new { t, page = 1 });

            ViewBag.Pedido          = pedido;
            ViewBag.Buscar          = buscar;
            ViewBag.FechaInicio     = fechaInicio;
            ViewBag.FechaFin        = fechaFin;
            ViewBag.Page            = page;
            ViewBag.PageSize        = pageSize;
            ViewBag.TotalCount      = resultado.TotalCount;
            ViewBag.TotalPages      = resultado.TotalCount == 0 ? 1 : (int)Math.Ceiling((double)resultado.TotalCount / pageSize);
            ViewBag.TieneGuias      = await _sgcService.TieneGuiasAsync(serie, numPed);
            ViewBag.SumCantidad     = resultado.SumCantidad;
            ViewBag.SumPrecio       = resultado.SumPrecio;
            ViewBag.SumCantDespacho = resultado.SumCantDespacho;
            ViewBag.SumDifDespacho  = resultado.SumDifDespacho;
            return View("Pedidos/DetallePedido", resultado.Items);
        }

        // ========== GUÍAS (KARDEX_G) ==========

        public async Task<IActionResult> Guias(string? t = null, int pedSerie = 0, int numPed = 0, int nro = 0, string? buscar = null, int page = 1)
        {
            if (!string.IsNullOrEmpty(t) && _navToken.TryUnprotect(t, out var nav))
            {
                if (int.TryParse(nav.GetValueOrDefault("pedSerie"), out var ps)) pedSerie = ps;
                if (int.TryParse(nav.GetValueOrDefault("numPed"),   out var np)) numPed   = np;
                if (int.TryParse(nav.GetValueOrDefault("nro"),      out var n))  nro      = n;
                buscar = nav.GetValueOrDefault("buscar") ?? buscar;
            }
            var navToken = _navToken.Protect(new Dictionary<string, string?> {
                ["pedSerie"] = pedSerie.ToString(),
                ["numPed"]   = numPed.ToString(),
                ["nro"]      = nro.ToString(),
                ["buscar"]   = buscar
            });
            ViewBag.NavToken = navToken;

            const int pageSize = 10;
            var resultado = await _sgcService.ObtenerGuiasAsync(pedSerie, numPed, page, pageSize);
            if (!resultado.Items.Any() && page > 1)
                return RedirectToAction(nameof(Guias), new { t, page = 1 });

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
            return View("Pedidos/Guias", resultado.Items);
        }

        // ========== DETALLE DE GUÍA (KARDEX_D) ==========

        public async Task<IActionResult> DetalleGuia(string? t = null, string? codAlm = null, string? tpTransac = null,
            int serie = 0, int numero = 0, int pedSerie = 0, int numPed = 0, int nro = 0, string? codArt = null, string? buscar = null, int page = 1)
        {
            if (!string.IsNullOrEmpty(t) && _navToken.TryUnprotect(t, out var nav))
            {
                codAlm    = nav.GetValueOrDefault("codAlm")    ?? codAlm;
                tpTransac = nav.GetValueOrDefault("tpTransac") ?? tpTransac;
                codArt    = nav.GetValueOrDefault("codArt")    ?? codArt;
                buscar    = nav.GetValueOrDefault("buscar")    ?? buscar;
                if (int.TryParse(nav.GetValueOrDefault("serie"),    out var sr)) serie    = sr;
                if (int.TryParse(nav.GetValueOrDefault("numero"),   out var nm)) numero   = nm;
                if (int.TryParse(nav.GetValueOrDefault("pedSerie"), out var ps)) pedSerie = ps;
                if (int.TryParse(nav.GetValueOrDefault("numPed"),   out var np)) numPed   = np;
                if (int.TryParse(nav.GetValueOrDefault("nro"),      out var n))  nro      = n;
            }
            var navToken = _navToken.Protect(new Dictionary<string, string?> {
                ["codAlm"]    = codAlm,
                ["tpTransac"] = tpTransac,
                ["serie"]     = serie.ToString(),
                ["numero"]    = numero.ToString(),
                ["pedSerie"]  = pedSerie.ToString(),
                ["numPed"]    = numPed.ToString(),
                ["nro"]       = nro.ToString(),
                ["codArt"]    = codArt,
                ["buscar"]    = buscar
            });
            ViewBag.NavToken = navToken;

            var guia = await _sgcService.ObtenerGuiaAsync(codAlm ?? string.Empty, tpTransac ?? string.Empty, serie, numero);
            if (guia == null) return NotFound();

            const int pageSize = 10;
            var resultado = await _sgcService.ObtenerDetalleGuiaAsync(codAlm ?? string.Empty, tpTransac ?? string.Empty, serie, numero, page, pageSize);
            if (!resultado.Items.Any() && page > 1)
                return RedirectToAction(nameof(DetalleGuia), new { t, page = 1 });

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
            return View("Pedidos/DetalleGuia", resultado.Items);
        }

        // ========== FACTURAS (DOCUVENT) ==========

        public async Task<IActionResult> Facturas(string? t = null,
            string? cTipo = null, string? cSerie = null, string? cNumero = null,
            string? codAlm = null, string? tpTransac = null, int guiaSerie = 0, int guiaNumero = 0,
            int pedSerie = 0, int numPed = 0, int itemNro = 0, int kdNro = 0,
            string? codArt = null, int? packingSerie = null, int? packingNumero = null,
            string? buscar = null, int page = 1)
        {
            if (!string.IsNullOrEmpty(t) && _navToken.TryUnprotect(t, out var nav))
            {
                cTipo     = nav.GetValueOrDefault("cTipo")     ?? cTipo;
                cSerie    = nav.GetValueOrDefault("cSerie")    ?? cSerie;
                cNumero   = nav.GetValueOrDefault("cNumero")   ?? cNumero;
                codAlm    = nav.GetValueOrDefault("codAlm")    ?? codAlm;
                tpTransac = nav.GetValueOrDefault("tpTransac") ?? tpTransac;
                buscar    = nav.GetValueOrDefault("buscar")    ?? buscar;
                codArt    = nav.GetValueOrDefault("codArt")    ?? codArt;
                if (int.TryParse(nav.GetValueOrDefault("guiaSerie"),    out var gs))  guiaSerie    = gs;
                if (int.TryParse(nav.GetValueOrDefault("guiaNumero"),   out var gn))  guiaNumero   = gn;
                if (int.TryParse(nav.GetValueOrDefault("pedSerie"),     out var ps))  pedSerie     = ps;
                if (int.TryParse(nav.GetValueOrDefault("numPed"),       out var np))  numPed       = np;
                if (int.TryParse(nav.GetValueOrDefault("itemNro"),      out var it))  itemNro      = it;
                if (int.TryParse(nav.GetValueOrDefault("kdNro"),        out var kd))  kdNro        = kd;
                if (int.TryParse(nav.GetValueOrDefault("packingSerie"),  out var pks)) packingSerie  = pks;
                if (int.TryParse(nav.GetValueOrDefault("packingNumero"), out var pkn)) packingNumero = pkn;
            }

            var navToken = _navToken.Protect(new Dictionary<string, string?> {
                ["cTipo"]         = cTipo,
                ["cSerie"]        = cSerie,
                ["cNumero"]       = cNumero,
                ["codAlm"]        = codAlm,
                ["tpTransac"]     = tpTransac,
                ["guiaSerie"]     = guiaSerie.ToString(),
                ["guiaNumero"]    = guiaNumero.ToString(),
                ["pedSerie"]      = pedSerie.ToString(),
                ["numPed"]        = numPed.ToString(),
                ["itemNro"]       = itemNro.ToString(),
                ["kdNro"]         = kdNro.ToString(),
                ["codArt"]        = codArt,
                ["buscar"]        = buscar,
                ["packingSerie"]  = packingSerie?.ToString(),
                ["packingNumero"] = packingNumero?.ToString()
            });

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
                return RedirectToAction(nameof(Facturas), new { t = navToken, page = 1 });

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
            ViewBag.NavToken      = navToken;
            ViewBag.Page          = page;
            ViewBag.PageSize      = pageSize;
            ViewBag.TotalCount    = resultado.TotalCount;
            ViewBag.TotalPages    = resultado.TotalCount == 0 ? 1 : (int)Math.Ceiling((double)resultado.TotalCount / pageSize);
            return View("Pedidos/Facturas", resultado.Items);
        }

        // ========== DETALLE DE FACTURA (ITEMDOCU) ==========

        public async Task<IActionResult> DetalleFactura(string? t = null,
            string tipo = "", string serie = "", string numero = "",
            string codAlm = "", string tpTransac = "", int guiaSerie = 0, int guiaNumero = 0,
            int pedSerie = 0, int numPed = 0, int itemNro = 0, string? buscar = null,
            bool fromPacking = false, int? packingSerie = null, int? packingNumero = null,
            string? cTipo = null, int page = 1)
        {
            if (!string.IsNullOrEmpty(t) && _navToken.TryUnprotect(t, out var nav))
            {
                tipo      = nav.GetValueOrDefault("tipo")      ?? tipo;
                serie     = nav.GetValueOrDefault("serie")     ?? serie;
                numero    = nav.GetValueOrDefault("numero")    ?? numero;
                codAlm    = nav.GetValueOrDefault("codAlm")    ?? codAlm;
                tpTransac = nav.GetValueOrDefault("tpTransac") ?? tpTransac;
                buscar    = nav.GetValueOrDefault("buscar")    ?? buscar;
                cTipo     = nav.GetValueOrDefault("cTipo")     ?? cTipo;
                if (int.TryParse(nav.GetValueOrDefault("guiaSerie"),    out var gs))  guiaSerie    = gs;
                if (int.TryParse(nav.GetValueOrDefault("guiaNumero"),   out var gn))  guiaNumero   = gn;
                if (int.TryParse(nav.GetValueOrDefault("pedSerie"),     out var ps))  pedSerie     = ps;
                if (int.TryParse(nav.GetValueOrDefault("numPed"),       out var np))  numPed       = np;
                if (int.TryParse(nav.GetValueOrDefault("itemNro"),      out var it))  itemNro      = it;
                if (bool.TryParse(nav.GetValueOrDefault("fromPacking"), out var fp))  fromPacking  = fp;
                if (int.TryParse(nav.GetValueOrDefault("packingSerie"),  out var pks)) packingSerie  = pks;
                if (int.TryParse(nav.GetValueOrDefault("packingNumero"), out var pkn)) packingNumero = pkn;
            }

            var factura = await _sgcService.ObtenerFacturaAsync(tipo, serie, numero);
            if (factura == null) return NotFound();

            const int pageSize = 10;
            var resultado = await _sgcService.ObtenerDetalleFacturaAsync(tipo, serie, numero, page, pageSize);
            if (!resultado.Items.Any() && page > 1)
                return RedirectToAction(nameof(DetalleFactura), new { t, page = 1 });

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
            ViewBag.NavToken      = t;
            ViewBag.Page          = page;
            ViewBag.PageSize      = pageSize;
            ViewBag.TotalCount    = resultado.TotalCount;
            ViewBag.TotalPages    = resultado.TotalCount == 0 ? 1 : (int)Math.Ceiling((double)resultado.TotalCount / pageSize);
            return View("Pedidos/DetalleFactura", resultado.Items);
        }

        // ========== PACKING (PACKING_G) ==========

        public async Task<IActionResult> Packing(string? t = null, int pedSerie = 0, int numPed = 0, string? buscar = null, string? fechaInicio = null, string? fechaFin = null, int page = 1)
        {
            if (!string.IsNullOrEmpty(t) && _navToken.TryUnprotect(t, out var nav))
            {
                if (int.TryParse(nav.GetValueOrDefault("pedSerie"), out var ps)) pedSerie = ps;
                if (int.TryParse(nav.GetValueOrDefault("numPed"),   out var np)) numPed   = np;
                buscar      = nav.GetValueOrDefault("buscar")      ?? buscar;
                fechaInicio = nav.GetValueOrDefault("fechaInicio") ?? fechaInicio;
                fechaFin    = nav.GetValueOrDefault("fechaFin")    ?? fechaFin;
            }
            var navToken = _navToken.Protect(new Dictionary<string, string?> {
                ["pedSerie"]    = pedSerie.ToString(),
                ["numPed"]      = numPed.ToString(),
                ["buscar"]      = buscar,
                ["fechaInicio"] = fechaInicio,
                ["fechaFin"]    = fechaFin
            });
            ViewBag.NavToken = navToken;

            var pedido = await _sgcService.ObtenerPedidoAsync(pedSerie, numPed);
            if (pedido == null) return NotFound();

            const int pageSize = 10;
            var resultado = await _sgcService.ObtenerPackingsAsync(numPed, page, pageSize);
            if (!resultado.Items.Any() && page > 1)
                return RedirectToAction(nameof(Packing), new { t, page = 1 });

            ViewBag.Pedido       = pedido;
            ViewBag.PedSerie     = pedSerie;
            ViewBag.NumPed       = numPed;
            ViewBag.NumOrdcompra = resultado.Items.FirstOrDefault()?.NumOrdcompra;
            ViewBag.Buscar       = buscar;
            ViewBag.FechaInicio  = fechaInicio;
            ViewBag.FechaFin     = fechaFin;
            ViewBag.Page         = page;
            ViewBag.PageSize     = pageSize;
            ViewBag.TotalCount   = resultado.TotalCount;
            ViewBag.TotalPages   = resultado.TotalCount == 0 ? 1 : (int)Math.Ceiling((double)resultado.TotalCount / pageSize);
            return View("Pedidos/Packing", resultado.Items);
        }

        // ========== PDF DOWNLOADS ==========

        public async Task<IActionResult> DescargarGuiaPdf(string codAlm, string tpTransac, int serie, int numero,
            int pedSerie, int numPed, int nro, string codArt)
        {
            var guia = await _sgcService.ObtenerGuiaAsync(codAlm, tpTransac, serie, numero);
            if (guia == null)
                return Json(new { tipo = "Error", mensaje = "No se encontró la guía." });

            // Para TP_TRANSAC = 23 (Salida Interna) no existe PDF físico: se genera on-the-fly
            if (tpTransac == "23" || string.IsNullOrEmpty(guia.SerieSunat))
            {
                try
                {
                    var datos = await _sgcService.ObtenerSalidaInternaAsync(codAlm, tpTransac, serie, numero);
                    if (datos == null)
                        return Json(new { tipo = "Error", mensaje = "No se encontraron datos para generar el reporte." });

                    var rucEmpresa = _configuration["RucEmpresa"] ?? string.Empty;
                    var logoPath   = Path.Combine(_env.WebRootPath, "images", "logo-colonial.png");
                    var pdfBytes   = _salidaInternaPdf.Generar(datos, rucEmpresa, logoPath);
                    var fileName   = $"SalidaInterna-{datos.Serie:D3}-{datos.Numero:D8}.pdf";
                    return File(pdfBytes, "application/pdf", fileName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al generar Salida Interna PDF {CodAlm}/{TpTransac}/{Serie}/{Numero}", codAlm, tpTransac, serie, numero);
                    return Json(new { tipo = "Error", mensaje = $"Error al generar el reporte: {ex.Message}" });
                }
            }

            var fecha         = guia.FchTransac!.Value;
            var rutaProv      = _configuration["RutaProv"] ?? string.Empty;
            var rucEmpresaPdf = _configuration["RucEmpresa"] ?? string.Empty;
            var nroFormato    = guia.Numero.ToString("D8");
            var nombreArchivo = $"{rucEmpresaPdf}-09-{guia.SerieSunat}-{nroFormato}.pdf";

            var rutaPdf = !string.IsNullOrEmpty(rutaProv)
                ? Path.Combine(rutaProv, fecha.ToString("yyyyMMdd"), nombreArchivo)
                : string.Empty;

            if (!string.IsNullOrEmpty(rutaPdf))
            {
                EnsureNetworkShare(rutaPdf);
                try
                {
                    var pdfBytes = await System.IO.File.ReadAllBytesAsync(rutaPdf);
                    return File(pdfBytes, "application/pdf", nombreArchivo);
                }
                catch (FileNotFoundException)
                {
                    return Json(new { tipo = "Advertencia", mensaje = $"No se encontró el PDF. Fecha: {fecha:dd/MM/yyyy}\nRuta: {rutaPdf}" });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al leer PDF de guía: {Ruta}", rutaPdf);
                    return Json(new { tipo = "Advertencia", mensaje = $"Error al acceder al PDF.\nRuta: {rutaPdf}\nDetalle: {ex.Message}" });
                }
            }

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

            if (!string.IsNullOrEmpty(rutaPdf))
            {
                EnsureNetworkShare(rutaPdf);
                try
                {
                    var pdfBytes = await System.IO.File.ReadAllBytesAsync(rutaPdf);
                    return File(pdfBytes, "application/pdf", nombreArchivo);
                }
                catch (FileNotFoundException)
                {
                    return Json(new { tipo = "Advertencia", mensaje = $"No se encontró el PDF. Fecha: {fecha:dd/MM/yyyy}\nRuta: {rutaPdf}" });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al leer PDF de factura: {Ruta}", rutaPdf);
                    return Json(new { tipo = "Advertencia", mensaje = $"Error al acceder al PDF.\nRuta: {rutaPdf}\nDetalle: {ex.Message}" });
                }
            }

            return Json(new { tipo = "Advertencia", mensaje = $"No se encontró el PDF. Fecha: {fecha:dd/MM/yyyy}\nRuta: {rutaPdf}" });
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        // ========== DESPACHOS: LISTADO ==========

        [HttpGet]
        public async Task<IActionResult> ListadoDespachos(string? t = null, string? guia = null, string? pedido = null, string? factura = null, string? razonSocial = null, DateTime? fechaInicio = null, DateTime? fechaFin = null, int page = 1)
        {
            // Valor por defecto para Razón Social
            if (string.IsNullOrWhiteSpace(razonSocial) && string.IsNullOrEmpty(t))
                razonSocial = "ART ATLAS S.R.L.";

            if (string.IsNullOrEmpty(t) && (guia != null || pedido != null || factura != null || razonSocial != null || fechaInicio.HasValue || fechaFin.HasValue))
            {
                var token = _navToken.Protect(new Dictionary<string, string?> {
                    ["guia"]        = guia,
                    ["pedido"]      = pedido,
                    ["factura"]     = factura,
                    ["razonSocial"] = razonSocial,
                    ["fechaInicio"] = fechaInicio?.ToString("yyyy-MM-dd"),
                    ["fechaFin"]    = fechaFin?.ToString("yyyy-MM-dd")
                });
                return RedirectToAction(nameof(ListadoDespachos), new { t = token, page });
            }
            if (!string.IsNullOrEmpty(t) && _navToken.TryUnprotect(t, out var nav))
            {
                guia        = nav.GetValueOrDefault("guia")        ?? guia;
                pedido      = nav.GetValueOrDefault("pedido")      ?? pedido;
                factura     = nav.GetValueOrDefault("factura")     ?? factura;
                razonSocial = nav.GetValueOrDefault("razonSocial") ?? razonSocial;
                if (DateTime.TryParse(nav.GetValueOrDefault("fechaInicio"), out var fi)) fechaInicio = fi;
                if (DateTime.TryParse(nav.GetValueOrDefault("fechaFin"),    out var ff)) fechaFin    = ff;
            }
            var navToken = _navToken.Protect(new Dictionary<string, string?> {
                ["guia"]        = guia,
                ["pedido"]      = pedido,
                ["factura"]     = factura,
                ["razonSocial"] = razonSocial,
                ["fechaInicio"] = fechaInicio?.ToString("yyyy-MM-dd"),
                ["fechaFin"]    = fechaFin?.ToString("yyyy-MM-dd")
            });
            ViewBag.NavToken = navToken;

            const int pageSize = 10;
            var resultado = await _sgcService.ObtenerListadoDespachosAsync(guia, pedido, factura, razonSocial, fechaInicio, fechaFin, page, pageSize);
            if (!resultado.Items.Any() && page > 1)
                return RedirectToAction(nameof(ListadoDespachos), new { t, page = 1 });

            ViewBag.Guia        = guia;
            ViewBag.Pedido      = pedido;
            ViewBag.Factura     = factura;
            ViewBag.RazonSocial = razonSocial;
            ViewBag.FechaInicio = fechaInicio?.ToString("yyyy-MM-dd");
            ViewBag.FechaFin    = fechaFin?.ToString("yyyy-MM-dd");
            ViewBag.Page        = page;
            ViewBag.PageSize    = pageSize;
            ViewBag.TotalCount  = resultado.TotalCount;
            ViewBag.TotalPages  = resultado.TotalCount == 0 ? 1 : (int)Math.Ceiling((double)resultado.TotalCount / pageSize);
            return View("Despachos/ListadoDespachos", resultado.Items);
        }

        [HttpGet]
        public async Task<IActionResult> ExportarDespachosExcel(string? guia = null, string? pedido = null, string? factura = null, string? razonSocial = null, DateTime? fechaInicio = null, DateTime? fechaFin = null)
        {
            // Exportar todos los registros sin paginación (pageSize muy grande)
            var resultado = await _sgcService.ObtenerListadoDespachosAsync(guia, pedido, factura, razonSocial, fechaInicio, fechaFin, 1, int.MaxValue);
            var items = resultado.Items;

            using var workbook  = new ClosedXML.Excel.XLWorkbook();
            var ws = workbook.Worksheets.Add("Listado de Despachos");

            // Headers
            string[] headers = { "#", "RAZON SOCIAL", "OC", "PEDIDO", "FACTURA", "FECHA.DOC", "ARTICULO", "CANTIDAD", "CANT_FACTURADA", "PRECIO", "GUIA", "OBS" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
            }

            // Data rows
            int row = 2;
            foreach (var d in items)
            {
                ws.Cell(row, 1).Value  = d.Correlativo;
                ws.Cell(row, 2).Value  = d.RazonSocial ?? string.Empty;
                ws.Cell(row, 3).Value  = d.Oc          ?? string.Empty;
                ws.Cell(row, 4).Value  = d.Pedido      ?? string.Empty;
                ws.Cell(row, 5).Value  = d.Factura     ?? string.Empty;
                ws.Cell(row, 6).Value  = d.FechaDoc.HasValue ? d.FechaDoc.Value.ToString("dd/MM/yyyy") : string.Empty;
                ws.Cell(row, 7).Value  = d.Articulo    ?? string.Empty;
                ws.Cell(row, 8).Value  = (double)(d.Cantidad      ?? 0m);
                ws.Cell(row, 9).Value  = (double)(d.CantFacturada ?? 0m);
                ws.Cell(row, 10).Value = (double)(d.Precio        ?? 0m);
                ws.Cell(row, 11).Value = d.Guia;
                ws.Cell(row, 12).Value = d.Obs          ?? string.Empty;

                // Highlight FACTURA column in yellow
                ws.Cell(row, 5).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.Yellow;
                row++;
            }

            ws.Columns().AdjustToContents();

            using var ms = new System.IO.MemoryStream();
            workbook.SaveAs(ms);
            var fileName = $"ListadoDespachos_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        private void EnsureNetworkShare(string filePath)
        {
            var username = _configuration["NetworkShare:Username"];
            if (string.IsNullOrEmpty(username)) return;
            try
            {
                NetworkShareHelper.Connect(
                    filePath,
                    username,
                    _configuration["NetworkShare:Password"],
                    _configuration["NetworkShare:Domain"]);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo establecer conexión al recurso de red: {Path}", filePath);
            }
        }
    }
}
