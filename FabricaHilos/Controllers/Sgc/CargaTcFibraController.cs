using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FabricaHilos.Models.Sgc;
using FabricaHilos.Services;
using FabricaHilos.Services.Sgc;

namespace FabricaHilos.Controllers.Sgc
{
    [Authorize]
    public class CargaTcFibraController : OracleBaseController
    {
        private readonly ICargaTcFibraService _cargaTcFibraService;
        private readonly ILogger<CargaTcFibraController> _logger;
        private readonly INavTokenService _navToken;

        public CargaTcFibraController(
            ICargaTcFibraService cargaTcFibraService,
            ILogger<CargaTcFibraController> logger,
            INavTokenService navToken)
        {
            _cargaTcFibraService = cargaTcFibraService;
            _logger              = logger;
            _navToken            = navToken;
        }

        // ========== LISTADO DE TRAZABILIDAD ==========

        [HttpGet]
        public async Task<IActionResult> Index(string? t = null, DateTime? fechaInicio = null,
            DateTime? fechaFin = null, string? tipoCert = null, bool soloPendientesTc = false, bool misRequerimientos = false)
        {
            // Si hay filtros nuevos sin token, crear token y redirigir
            if (string.IsNullOrEmpty(t) && (fechaInicio.HasValue || fechaFin.HasValue || tipoCert != null || soloPendientesTc || misRequerimientos))
            {
                var token = _navToken.Protect(new Dictionary<string, string?> {
                    ["fechaInicio"]        = fechaInicio?.ToString("yyyy-MM-dd"),
                    ["fechaFin"]           = fechaFin?.ToString("yyyy-MM-dd"),
                    ["tipoCert"]           = tipoCert,
                    ["soloPendientesTc"]   = soloPendientesTc.ToString(),
                    ["misRequerimientos"]  = misRequerimientos.ToString()
                });
                return RedirectToAction(nameof(Index), new { t = token });
            }

            // Desempaquetar token
            if (!string.IsNullOrEmpty(t) && _navToken.TryUnprotect(t, out var nav))
            {
                if (DateTime.TryParse(nav.GetValueOrDefault("fechaInicio"), out var fi)) fechaInicio = fi;
                if (DateTime.TryParse(nav.GetValueOrDefault("fechaFin"),    out var ff)) fechaFin    = ff;
                tipoCert = nav.GetValueOrDefault("tipoCert") ?? tipoCert;
                if (bool.TryParse(nav.GetValueOrDefault("soloPendientesTc"),  out var spt)) soloPendientesTc  = spt;
                if (bool.TryParse(nav.GetValueOrDefault("misRequerimientos"), out var mrq)) misRequerimientos = mrq;
            }

            var usuarioActual = HttpContext.Session.GetString("OracleUser") ?? User.FindFirst("OracleUser")?.Value;
            var codUsuario    = misRequerimientos ? usuarioActual : null;
            var items = await _cargaTcFibraService.ObtenerTrazabilidadAsync(fechaInicio, fechaFin, tipoCert, soloPendientesTc, codUsuario);

            var navToken = _navToken.Protect(new Dictionary<string, string?> {
                ["fechaInicio"]        = fechaInicio?.ToString("yyyy-MM-dd"),
                ["fechaFin"]           = fechaFin?.ToString("yyyy-MM-dd"),
                ["tipoCert"]           = tipoCert,
                ["soloPendientesTc"]   = soloPendientesTc.ToString(),
                ["misRequerimientos"]  = misRequerimientos.ToString()
            });

            ViewBag.FechaInicio        = fechaInicio?.ToString("yyyy-MM-dd");
            ViewBag.FechaFin           = fechaFin?.ToString("yyyy-MM-dd");
            ViewBag.TipoCert           = tipoCert;
            ViewBag.SoloPendientesTc   = soloPendientesTc;
            ViewBag.MisRequerimientos  = misRequerimientos;
            ViewBag.NavToken           = navToken;
            ViewBag.TotalCount         = items.Count;

            return View("~/Views/Sgc/Despachos/CargaTcFibra/Index.cshtml", items);
        }

        // ========== DETALLE DE UN INGRESO ==========

        [HttpGet]
        public async Task<IActionResult> Detalle(string codAlm, string tpTransac, decimal serie, decimal numero, string? t = null)
        {
            var ingreso = await _cargaTcFibraService.ObtenerTrazabilidadIngresoAsync(codAlm, tpTransac, serie, numero);
            if (ingreso == null)
            {
                TempData["Error"] = "No se encontró el ingreso especificado.";
                return RedirectToAction(nameof(Index), new { t });
            }

            ViewBag.NavToken = t;

            return View("~/Views/Sgc/Despachos/CargaTcFibra/Detalle.cshtml", ingreso);
        }

        // ========== REGISTRAR CERTIFICADO ==========

        [HttpPost]
        public async Task<IActionResult> RegistrarCertificado([FromBody] RegistrarCertificadoTcAlgodonDto modelo)
        {
            try
            {
                var usuario = HttpContext.Session.GetString("OracleUser") ?? "SYSTEM";
                var resultado = await _cargaTcFibraService.RegistrarCertificadoAsync(modelo, usuario);

                if (resultado.Exito)
                    return Json(new { tipo = "Exito", mensaje = "Certificado registrado correctamente.", idCert = resultado.IdCert });

                return Json(new { tipo = "Advertencia", mensaje = resultado.MensajeError ?? "No se pudo registrar el certificado." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar certificado TC de algodón para {CodAlm}/{TpTransac}/{Serie}/{Numero}",
                    modelo.CodAlm, modelo.TpTransac, modelo.Serie, modelo.Numero);
                return Json(new { tipo = "Error", mensaje = $"Error al registrar: {ex.Message}" });
            }
        }

        // ========== ANULAR CERTIFICADO ==========

        [HttpPost]
        public async Task<IActionResult> AnularCertificado(int idCert)
        {
            try
            {
                var usuario = HttpContext.Session.GetString("OracleUser") ?? "SYSTEM";
                var resultado = await _cargaTcFibraService.AnularCertificadoAsync(idCert, usuario);

                if (resultado.Exito)
                    return Json(new { tipo = "Exito", mensaje = "Certificado anulado correctamente." });

                return Json(new { tipo = "Advertencia", mensaje = resultado.MensajeError ?? "No se pudo anular el certificado." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al anular certificado TC de algodón {IdCert}", idCert);
                return Json(new { tipo = "Error", mensaje = $"Error al anular: {ex.Message}" });
            }
        }

        // ========== PENDIENTES DE TC (bandeja de alertas) ==========

        [HttpGet]
        public async Task<IActionResult> Pendientes(int diasAntiguedad = 0)
        {
            var items = await _cargaTcFibraService.ObtenerPendientesTcAsync(diasAntiguedad);
            return Json(items);
        }
    }
}
