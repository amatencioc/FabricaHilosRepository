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
        public async Task<IActionResult> Consultar(int anio, int semana = 0, string ceo = "O", int? mes = null)
        {
            var esEmpleado = string.Equals(ceo, "E", StringComparison.OrdinalIgnoreCase);
            if (anio <= 0 || (esEmpleado ? (!mes.HasValue || mes <= 0 || mes > 12) : semana <= 0))
                return Json(new { ok = false, mensaje = esEmpleado ? "Debe indicar Año y Mes válidos." : "Debe indicar Año y Semana válidos." });

            try
            {
                var datos = await _service.ObtenerAsync(anio, semana, ceo, mes);
                return Json(new { ok = true, datos });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en PlanillaIngDsctoAportes.Consultar: Anio={Anio} Semana={Semana} Ceo={Ceo} Mes={Mes}", anio, semana, ceo, mes);
                return Json(new { ok = false, mensaje = "Error al consultar el reporte." });
            }
        }

        // ========== RESUMEN POR BANCO (AJAX) — pestaña "Resumen" ==========

        [HttpGet("ConsultarResumenBanco")]
        public async Task<IActionResult> ConsultarResumenBanco(int anio, int semana = 0, string ceo = "O", int? mes = null)
        {
            var esEmpleado = string.Equals(ceo, "E", StringComparison.OrdinalIgnoreCase);
            if (anio <= 0 || (esEmpleado ? (!mes.HasValue || mes <= 0 || mes > 12) : semana <= 0))
                return Json(new { ok = false, mensaje = esEmpleado ? "Debe indicar Año y Mes válidos." : "Debe indicar Año y Semana válidos." });

            try
            {
                var datos = await _service.ObtenerResumenBancoReporteAsync(anio, semana, ceo, mes);
                return Json(new { ok = true, datos });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en PlanillaIngDsctoAportes.ConsultarResumenBanco: Anio={Anio} Semana={Semana} Ceo={Ceo} Mes={Mes}", anio, semana, ceo, mes);
                return Json(new { ok = false, mensaje = "Error al consultar el resumen por banco." });
            }
        }

        // ========== RESUMEN POR CENTRO DE COSTO (AJAX) — pestaña "Detalle" ==========

        [HttpGet("ConsultarResumenCcosto")]
        public async Task<IActionResult> ConsultarResumenCcosto(int anio, int semana = 0, string ceo = "O", int? mes = null)
        {
            var esEmpleado = string.Equals(ceo, "E", StringComparison.OrdinalIgnoreCase);
            if (anio <= 0 || (esEmpleado ? (!mes.HasValue || mes <= 0 || mes > 12) : semana <= 0))
                return Json(new { ok = false, mensaje = esEmpleado ? "Debe indicar Año y Mes válidos." : "Debe indicar Año y Semana válidos." });

            try
            {
                var datos = await _service.ObtenerResumenCcostoAsync(anio, semana, ceo, mes);
                return Json(new { ok = true, datos });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en PlanillaIngDsctoAportes.ConsultarResumenCcosto: Anio={Anio} Semana={Semana} Ceo={Ceo} Mes={Mes}", anio, semana, ceo, mes);
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
        public async Task<IActionResult> ExportarExcel(int anio, int semana = 0, string ceo = "O", int? mes = null, DateTime? fechaLiquidacion = null)
        {
            var esEmpleado = string.Equals(ceo, "E", StringComparison.OrdinalIgnoreCase);
            if (anio <= 0 || (esEmpleado ? (!mes.HasValue || mes <= 0 || mes > 12) : semana <= 0))
                return BadRequest(esEmpleado ? "Debe indicar Año y Mes válidos." : "Debe indicar Año y Semana válidos.");

            try
            {
                var datos          = await _service.ObtenerAsync(anio, semana, ceo, mes);
                var resumenBanco   = await _service.ObtenerResumenBancoReporteAsync(anio, semana, ceo, mes);
                var resumenCcosto  = await _service.ObtenerResumenCcostoAsync(anio, semana, ceo, mes);
                
                LiquidacionesReporteDto? liquidaciones = null;
                if (fechaLiquidacion.HasValue)
                {
                    liquidaciones = await _service.ObtenerLiquidacionesBancoAsync(fechaLiquidacion.Value);
                }

                var bytes = _excelService.GenerarExcel(datos, resumenBanco, resumenCcosto, liquidaciones, anio, semana, ceo, mes);
                var sufijo = esEmpleado ? $"M{mes:00}" : $"S{semana}";
                var nombreArchivo = $"PlanillaIngDsctoAportes_{anio}_{sufijo}.xlsx";
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombreArchivo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en PlanillaIngDsctoAportes.ExportarExcel: Anio={Anio} Semana={Semana} Ceo={Ceo} Mes={Mes}", anio, semana, ceo, mes);
                return StatusCode(500, "Error al generar el Excel.");
            }
        }
    }
}
