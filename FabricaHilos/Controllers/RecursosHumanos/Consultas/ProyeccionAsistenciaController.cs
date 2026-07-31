using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FabricaHilos.Services.RecursosHumanos;

namespace FabricaHilos.Controllers.RecursosHumanos.Consultas
{
    [Authorize]
    [Route("RecursosHumanos/ProyeccionAsistencia")]
    public class ProyeccionAsistenciaController : OracleBaseController
    {
        private readonly IProyeccionAsistenciaService _proyeccionAsistenciaService;
        private readonly ILogger<ProyeccionAsistenciaController> _logger;

        public ProyeccionAsistenciaController(
            IProyeccionAsistenciaService proyeccionAsistenciaService,
            ILogger<ProyeccionAsistenciaController> logger)
        {
            _proyeccionAsistenciaService = proyeccionAsistenciaService;
            _logger                      = logger;
        }

        // ========== INDEX — Vista de consulta ==========

        [HttpGet("")]
        [HttpGet("Index")]
        public IActionResult Index()
        {
            return View("~/Views/RecursosHumanos/Consultas/ProyeccionAsistencia/Index.cshtml");
        }

        // ========== CONSULTAR (AJAX) ==========

        [HttpGet("Consultar")]
        public async Task<IActionResult> Consultar(string? fecha, string? empresa = null)
        {
            if (!DateTime.TryParse(fecha, out var fechaConsulta))
                return Json(new { ok = false, mensaje = "Fecha inválida." });

            try
            {
                var (ok, mensaje, resumen, detalle) = await _proyeccionAsistenciaService.ConsultarAsync(
                    fechaConsulta.Date, string.IsNullOrWhiteSpace(empresa) ? null : empresa.Trim());

                if (!ok)
                    return Json(new { ok = false, mensaje });

                return Json(new { ok = true, resumen, detalle });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ProyeccionAsistencia.Consultar: {Fecha} ({Empresa})", fecha, empresa);
                return Json(new { ok = false, mensaje = "Error al consultar la proyección de asistencia." });
            }
        }
    }
}
