using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FabricaHilos.Services.RecursosHumanos;
using FabricaHilos.Models.RecursosHumanos;

namespace FabricaHilos.Controllers.RecursosHumanos.ReportePlanilla
{
    [Authorize]
    [Route("RecursosHumanos/ReportePlanilla/PlanillaIngDsctoAportes")]
    public class PlanillaIngDsctoAportesController : Controller
    {
        private readonly IPlanillaIngDsctoAportesService      _service;
        private readonly IPlanillaIngDsctoAportesExcelService _excelService;
        private readonly ILogger<PlanillaIngDsctoAportesController> _logger;

        public PlanillaIngDsctoAportesController(
            IPlanillaIngDsctoAportesService service,
            IPlanillaIngDsctoAportesExcelService excelService,
            ILogger<PlanillaIngDsctoAportesController> logger)
        {
            _service      = service;
            _excelService = excelService;
            _logger       = logger;
        }

        // ========== INDEX — Vista de consulta ==========

        [HttpGet("")]
        [HttpGet("Index")]
        public IActionResult Index()
        {
            return View("~/Views/RecursosHumanos/ReportePlanilla/PlanillaIngDsctoAportes/Index.cshtml");
        }

        // ========== CONSULTAR (AJAX) ==========

        [HttpGet("Consultar")]
        public async Task<IActionResult> Consultar(int anio, int semana)
        {
            if (anio <= 0 || semana <= 0)
                return Json(new { ok = false, mensaje = "Debe indicar Año y Semana válidos." });

            try
            {
                var datos = await _service.ObtenerAsync(anio, semana);
                return Json(new { ok = true, datos });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en PlanillaIngDsctoAportes.Consultar: Anio={Anio} Semana={Semana}", anio, semana);
                return Json(new { ok = false, mensaje = "Error al consultar el reporte." });
            }
        }

        // ========== RESUMEN POR BANCO (AJAX) — pestaña "Resumen" ==========

        [HttpGet("ConsultarResumenBanco")]
        public async Task<IActionResult> ConsultarResumenBanco(int anio, int semana)
        {
            if (anio <= 0 || semana <= 0)
                return Json(new { ok = false, mensaje = "Debe indicar Año y Semana válidos." });

            try
            {
                var datos = await _service.ObtenerResumenBancoReporteAsync(anio, semana);
                return Json(new { ok = true, datos });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en PlanillaIngDsctoAportes.ConsultarResumenBanco: Anio={Anio} Semana={Semana}", anio, semana);
                return Json(new { ok = false, mensaje = "Error al consultar el resumen por banco." });
            }
        }

        // ========== RESUMEN POR CENTRO DE COSTO (AJAX) — pestaña "Detalle" ==========

        [HttpGet("ConsultarResumenCcosto")]
        public async Task<IActionResult> ConsultarResumenCcosto(int anio, int semana)
        {
            if (anio <= 0 || semana <= 0)
                return Json(new { ok = false, mensaje = "Debe indicar Año y Semana válidos." });

            try
            {
                var datos = await _service.ObtenerResumenCcostoAsync(anio, semana);
                return Json(new { ok = true, datos });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en PlanillaIngDsctoAportes.ConsultarResumenCcosto: Anio={Anio} Semana={Semana}", anio, semana);
                return Json(new { ok = false, mensaje = "Error al consultar el resumen por centro de costo." });
            }
        }

        // ========== CONSULTAR LIQUIDACIONES (AJAX) ==========

        [HttpGet("ConsultarLiquidaciones")]
        public async Task<IActionResult> ConsultarLiquidaciones(DateTime fechaLiquidacion)
        {
            if (fechaLiquidacion == default)
                return Json(new { ok = false, mensaje = "Debe indicar una fecha de liquidación." });

            try
            {
                var datos = await _service.ObtenerLiquidacionesBancoAsync(fechaLiquidacion);
                return Json(new { ok = true, datos });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en PlanillaIngDsctoAportes.ConsultarLiquidaciones: Fecha={Fecha}", fechaLiquidacion);
                return Json(new { ok = false, mensaje = "Error al consultar las liquidaciones." });
            }
        }

        // ========== EXPORTAR EXCEL ==========

        [HttpGet("ExportarExcel")]
        public async Task<IActionResult> ExportarExcel(int anio, int semana, DateTime? fechaLiquidacion = null)
        {
            if (anio <= 0 || semana <= 0)
                return BadRequest("Debe indicar Año y Semana válidos.");

            try
            {
                var datos          = await _service.ObtenerAsync(anio, semana);
                var resumenBanco   = await _service.ObtenerResumenBancoReporteAsync(anio, semana);
                var resumenCcosto  = await _service.ObtenerResumenCcostoAsync(anio, semana);
                
                LiquidacionesReporteDto? liquidaciones = null;
                if (fechaLiquidacion.HasValue)
                {
                    liquidaciones = await _service.ObtenerLiquidacionesBancoAsync(fechaLiquidacion.Value);
                }

                var bytes = _excelService.GenerarExcel(datos, resumenBanco, resumenCcosto, liquidaciones, anio, semana);
                var nombreArchivo = $"PlanillaIngDsctoAportes_{anio}_S{semana}.xlsx";
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombreArchivo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en PlanillaIngDsctoAportes.ExportarExcel: Anio={Anio} Semana={Semana}", anio, semana);
                return StatusCode(500, "Error al generar el Excel.");
            }
        }
    }
}
