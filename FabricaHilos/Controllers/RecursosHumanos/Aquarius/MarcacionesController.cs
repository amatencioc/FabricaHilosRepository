using FabricaHilos.Services;
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
        private readonly IDepuracionJobService _depuracionJobService;
        private readonly ILogger<MarcacionesController> _logger;
        private readonly string _baseConnStr;
        private readonly INavTokenService _navToken;
        private const int PageSize = 10;

        public MarcacionesController(
            IMarcacionesService marcacionesService,
            IDepuracionJobService depuracionJobService,
            ILogger<MarcacionesController> logger,
            IConfiguration configuration,
            INavTokenService navToken)
        {
            _marcacionesService    = marcacionesService;
            _depuracionJobService  = depuracionJobService;
            _logger                = logger;
            _baseConnStr           = configuration.GetConnectionString("AquariusConnection") ?? string.Empty;
            _navToken              = navToken;
        }

        // ========== INDEX — Lista paginada de empleados ==========

        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index(string? t = null, string? buscar = null, int page = 1)
        {
            // Si hay filtro sin token → crear token y redirigir
            if (string.IsNullOrEmpty(t) && buscar != null)
            {
                var token = _navToken.Protect(new Dictionary<string, string?> { ["buscar"] = buscar });
                return RedirectToAction(nameof(Index), new { t = token, page });
            }

            // Desempaquetar token
            if (!string.IsNullOrEmpty(t) && _navToken.TryUnprotect(t, out var nav))
                buscar = nav.GetValueOrDefault("buscar");

            page = Math.Max(1, page);
            var (items, total) = await _marcacionesService.ListarEmpleadosAsync(CodEmpresaAquarius, buscar, page, PageSize);

            var navToken = _navToken.Protect(new Dictionary<string, string?> { ["buscar"] = buscar });

            ViewBag.NavToken   = navToken;
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
            return View("~/Views/RecursosHumanos/Aquarius/Marcaciones/HorarioPorEmpleado.cshtml");
        }

        // ========== BUSCAR EMPLEADO (AJAX — autocompletado) ==========

        [HttpGet("BuscarEmpleado")]
        public async Task<IActionResult> BuscarEmpleado(string? nombre)
        {
            try
            {
                var empleados = await _marcacionesService.BuscarEmpleadoAsync(CodEmpresaAquarius, nombre);
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

                var marcaciones = await _marcacionesService.ConsultarRangoAsync(CodEmpresaAquarius, codPersonal, dtInicio, dtFin);
                return Json(new { ok = true, data = marcaciones });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar rango: personal={Personal}", codPersonal);
                return Json(new { ok = false, mensaje = "Error al consultar marcaciones." });
            }
        }

        // ========== DEPURAR RANGO — Encola el job y retorna jobId inmediatamente ==========

        [HttpPost("DepurarRango")]
        [ValidateAntiForgeryToken]
        public IActionResult DepurarRango([FromForm] string codPersonal, [FromForm] string fechaInicio, [FromForm] string fechaFin)
        {
            if (!DateTime.TryParseExact(fechaInicio, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var dtInicio)
             || !DateTime.TryParseExact(fechaFin,    "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var dtFin))
                return Json(new { ok = false, mensaje = "Formato de fecha inválido." });

            // Resolver la connection string con credenciales de sesión del usuario logueado
            var oracleUser = HttpContext.Session.GetString("OracleUser");
            var oraclePass = HttpContext.Session.GetString("OraclePass");
            string connStr = _baseConnStr;
            if (!string.IsNullOrEmpty(oracleUser) && !string.IsNullOrEmpty(oraclePass))
            {
                var csb = new Oracle.ManagedDataAccess.Client.OracleConnectionStringBuilder(_baseConnStr)
                {
                    UserID   = oracleUser,
                    Password = oraclePass
                };
                connStr = csb.ToString();
            }

            var jobId = _depuracionJobService.Encolar(CodEmpresaAquarius, codPersonal, dtInicio, dtFin, connStr);
            return Json(new { ok = true, jobId });
        }

        // ========== CONSULTAR ESTADO DE DEPURACIÓN (polling) ==========

        [HttpGet("ConsultarDepuracion")]
        public IActionResult ConsultarDepuracion(string jobId)
        {
            var job = _depuracionJobService.ObtenerEstado(jobId);
            if (job == null)
                return Json(new { ok = false, mensaje = "Job no encontrado." });

            return Json(new
            {
                ok       = true,
                estado   = job.Estado.ToString(),     // Pendiente | EnProceso | Completado | Error
                completado = job.Estado is DepuracionEstado.Completado or DepuracionEstado.Error,
                exito    = job.Estado == DepuracionEstado.Completado
                           && job.Resultado?.Resultado?.StartsWith("ERROR") != true,
                mensaje  = job.Estado switch
                {
                    DepuracionEstado.Pendiente   => "En cola, esperando procesamiento…",
                    DepuracionEstado.EnProceso   => "Depuración en proceso…",
                    DepuracionEstado.Completado  => "Depuración completada correctamente.",
                    DepuracionEstado.Error       => $"Error: {job.MensajeError ?? job.Resultado?.Resultado}",
                    _                            => "Estado desconocido"
                },
                duracionSeg = job.FinalizadoEn.HasValue && job.IniciadoEn.HasValue
                    ? (int)(job.FinalizadoEn.Value - job.IniciadoEn.Value).TotalSeconds
                    : (int?)null
            });
        }
    }
}
