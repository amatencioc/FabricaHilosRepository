using FabricaHilos.Services.RecursosHumanos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FabricaHilos.Controllers.RecursosHumanos.Aquarius
{
    [Authorize]
    [Route("RecursosHumanos/Aquarius/Marcaciones")]
    public class MarcacionesController : OracleBaseController
    {
        private readonly IMarcacionesService _marcacionesService;
        private readonly ILogger<MarcacionesController> _logger;
        private readonly string _codEmpresa;
        private const int PageSize = 10;

        public MarcacionesController(
            IMarcacionesService marcacionesService,
            ILogger<MarcacionesController> logger,
            IConfiguration configuration)
        {
            _marcacionesService = marcacionesService;
            _logger = logger;
            _codEmpresa = configuration["Aquarius:CodEmpresa"] ?? "0003";
        }

        // ========== INDEX — Lista paginada de empleados ==========

        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index(string? buscar, int page = 1)
        {
            page = Math.Max(1, page);
            var (items, total) = await _marcacionesService.ListarEmpleadosAsync(_codEmpresa, buscar, page, PageSize);
            ViewBag.Buscar     = buscar ?? string.Empty;
            ViewBag.Page       = page;
            ViewBag.PageSize   = PageSize;
            ViewBag.Total      = total;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)PageSize);
            return View("~/Views/RecursosHumanos/Aquarius/Marcaciones/Index.cshtml", items);
        }

        // ========== HORARIOS — Vista de horarios por rango ==========

        [HttpGet("Horarios")]
        public IActionResult Horarios(string codPersonal, string nombre)
        {
            ViewBag.CodPersonal = codPersonal;
            ViewBag.Nombre      = nombre;
            return View("~/Views/RecursosHumanos/Aquarius/Horarios/Index.cshtml");
        }

        // ========== BUSCAR EMPLEADO (AJAX — autocompletado) ==========

        [HttpGet("BuscarEmpleado")]
        public async Task<IActionResult> BuscarEmpleado(string? nombre)
        {
            try
            {
                var empleados = await _marcacionesService.BuscarEmpleadoAsync(_codEmpresa, nombre);
                return Json(new { ok = true, data = empleados });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al buscar empleado: {Nombre}", nombre);
                return Json(new { ok = false, mensaje = "Error al buscar empleados." });
            }
        }

        // ========== CONSULTAR RANGO (AJAX) ==========

        [HttpGet("ConsultarRango")]
        public async Task<IActionResult> ConsultarRango(string codPersonal, string fechaInicio, string fechaFin)
        {
            try
            {
                if (!DateTime.TryParseExact(fechaInicio, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var dtInicio)
                 || !DateTime.TryParseExact(fechaFin,    "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var dtFin))
                    return Json(new { ok = false, mensaje = "Formato de fecha inválido." });

                var marcaciones = await _marcacionesService.ConsultarRangoAsync(_codEmpresa, codPersonal, dtInicio, dtFin);
                return Json(new { ok = true, data = marcaciones });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar rango: personal={Personal}", codPersonal);
                return Json(new { ok = false, mensaje = "Error al consultar marcaciones." });
            }
        }

        // ========== DEPURAR RANGO (AJAX POST) ==========

        [HttpPost("DepurarRango")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DepurarRango([FromForm] string codPersonal, [FromForm] string fechaInicio, [FromForm] string fechaFin)
        {
            try
            {
                if (!DateTime.TryParseExact(fechaInicio, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var dtInicio)
                 || !DateTime.TryParseExact(fechaFin,    "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var dtFin))
                    return Json(new { ok = false, mensaje = "Formato de fecha inválido." });

                var resultado = await _marcacionesService.DepurarRangoAsync(_codEmpresa, codPersonal, dtInicio, dtFin);
                bool esError  = resultado.Resultado?.StartsWith("ERROR") == true;
                return Json(new { ok = !esError, data = resultado });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al depurar rango: personal={Personal}", codPersonal);
                return Json(new { ok = false, mensaje = "Error al ejecutar depuración." });
            }
        }
    }
}
