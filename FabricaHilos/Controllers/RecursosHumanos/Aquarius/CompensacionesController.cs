using FabricaHilos.Services;
using FabricaHilos.Services.RecursosHumanos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FabricaHilos.Controllers.RecursosHumanos.Aquarius
{
    [Authorize]
    [Route("RecursosHumanos/Aquarius/Compensaciones")]
    public class CompensacionesController : OracleBaseController
    {
        private readonly ICompensacionesService _compensacionesService;
        private readonly ICompensacionJobService _compensacionJobService;
        private readonly IMasivaEventoJobService _masivaJobService;
        private readonly ILogger<CompensacionesController> _logger;
        private readonly string _baseConnStr;
        private readonly INavTokenService _navToken;
        private const int PageSize = 10;

        public CompensacionesController(
            ICompensacionesService compensacionesService,
            ICompensacionJobService compensacionJobService,
            IMasivaEventoJobService masivaJobService,
            ILogger<CompensacionesController> logger,
            IConfiguration configuration,
            INavTokenService navToken)
        {
            _compensacionesService = compensacionesService;
            _compensacionJobService = compensacionJobService;
            _masivaJobService      = masivaJobService;
            _logger                = logger;
            _baseConnStr           = configuration.GetConnectionString("AquariusConnection") ?? string.Empty;
            _navToken              = navToken;
        }

        // ========== INDEX — Lista paginada de empleados ==========

        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index(string? t = null, string? buscar = null, int page = 1)
        {
            if (string.IsNullOrEmpty(t) && buscar != null)
            {
                var token = _navToken.Protect(new Dictionary<string, string?> { ["buscar"] = buscar });
                return RedirectToAction(nameof(Index), new { t = token, page });
            }

            if (!string.IsNullOrEmpty(t) && _navToken.TryUnprotect(t, out var nav))
                buscar = nav.GetValueOrDefault("buscar");

            page = Math.Max(1, page);
            var (items, total) = await _compensacionesService.ListarEmpleadosAsync(CodEmpresaAquarius, buscar, page, PageSize);

            var navToken = _navToken.Protect(new Dictionary<string, string?> { ["buscar"] = buscar });

            ViewBag.NavToken   = navToken;
            ViewBag.Buscar     = buscar ?? string.Empty;
            ViewBag.Page       = page;
            ViewBag.PageSize   = PageSize;
            ViewBag.Total      = total;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)PageSize);
            return View("~/Views/RecursosHumanos/Aquarius/Compensaciones/Individual/Index.cshtml", items);
        }

        // ========== DETALLE — Vista de compensaciones por rango ==========

        [HttpGet("Detalle")]
        public IActionResult Detalle(string codPersonal, string nombre)
        {
            ViewBag.CodPersonal = codPersonal;
            ViewBag.Nombre      = nombre;
            return View("~/Views/RecursosHumanos/Aquarius/Compensaciones/Individual/DetalleEmpleado.cshtml");
        }

        // ========== BUSCAR EMPLEADO (AJAX — autocompletado) ==========

        [HttpGet("BuscarEmpleado")]
        public async Task<IActionResult> BuscarEmpleado(string? nombre)
        {
            try
            {
                var empleados = await _compensacionesService.BuscarEmpleadoAsync(CodEmpresaAquarius, nombre);
                return Json(new { ok = true, data = empleados });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al buscar empleado Compensaciones: {Nombre}", nombre);
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

                var compensaciones = await _compensacionesService.ConsultarRangoAsync(CodEmpresaAquarius, codPersonal, dtInicio, dtFin);
                return Json(new { ok = true, data = compensaciones });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar rango compensaciones: personal={Personal}", codPersonal);
                return Json(new { ok = false, mensaje = "Error al consultar compensaciones." });
            }
        }

        // ========== CONSULTAR SALDO BANCO (AJAX) ==========

        [HttpGet("ConsultarSaldoBanco")]
        public async Task<IActionResult> ConsultarSaldoBanco(string codPersonal, int? anio, int? mes)
        {
            try
            {
                var saldos = await _compensacionesService.ConsultarSaldoBancoAsync(CodEmpresaAquarius, codPersonal, anio, mes);
                return Json(new { ok = true, data = saldos });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar saldo banco: personal={Personal}", codPersonal);
                return Json(new { ok = false, mensaje = "Error al consultar saldo del banco de horas." });
            }
        }

        // ========== VALIDAR (AJAX) ==========

        [HttpGet("Validar")]
        public async Task<IActionResult> Validar(string codPersonal, string? fechaDestino, string fechaOrigen,
            char tipoOrigen, char tipoCompensacion, string horas)
        {
            try
            {
                var resultado = await _compensacionesService.ValidarAsync(
                    CodEmpresaAquarius, codPersonal, fechaDestino, fechaOrigen, tipoOrigen, tipoCompensacion, horas);
                return Json(new { ok = true, data = resultado });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al validar compensación: personal={Personal}", codPersonal);
                return Json(new { ok = false, mensaje = "Error al validar compensación." });
            }
        }

        // ========== REGISTRAR (AJAX POST) ==========

        [HttpPost("Registrar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registrar([FromForm] string codPersonal,
            [FromForm] string? fechaDestino, [FromForm] string fechaOrigen,
            [FromForm] char tipoOrigen, [FromForm] char tipoCompensacion,
            [FromForm] string horas, [FromForm] string? perid,
            [FromForm] string? tipoBanco, [FromForm] string? proceso,
            [FromForm] string validar = "S")
        {
            try
            {
                var resultado = await _compensacionesService.RegistrarAsync(
                    CodEmpresaAquarius, codPersonal, fechaDestino, fechaOrigen,
                    tipoOrigen, tipoCompensacion, horas, perid, tipoBanco, proceso, validar);
                return Json(new { ok = resultado.Estado == "OK", data = resultado });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar compensación: personal={Personal}", codPersonal);
                return Json(new { ok = false, mensaje = "Error al registrar compensación." });
            }
        }

        // ========== ELIMINAR (AJAX POST) ==========

        [HttpPost("Eliminar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Eliminar([FromForm] int idCompen, [FromForm] string revertirTareo = "S")
        {
            try
            {
                var resultado = await _compensacionesService.EliminarAsync(idCompen, revertirTareo);
                return Json(new { ok = resultado.Estado == "OK", data = resultado });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar compensación: idCompen={IdCompen}", idCompen);
                return Json(new { ok = false, mensaje = "Error al eliminar compensación." });
            }
        }

        // ========== APLICAR RANGO — Encola el job y retorna jobId ==========

        [HttpPost("AplicarRango")]
        [ValidateAntiForgeryToken]
        public IActionResult AplicarRango([FromForm] string codPersonal, [FromForm] string fechaInicio, [FromForm] string fechaFin)
        {
            if (!DateTime.TryParseExact(fechaInicio, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var dtInicio)
             || !DateTime.TryParseExact(fechaFin,    "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var dtFin))
                return Json(new { ok = false, mensaje = "Formato de fecha inválido." });

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

            var jobId = _compensacionJobService.Encolar(CodEmpresaAquarius, codPersonal, dtInicio, dtFin, connStr);
            return Json(new { ok = true, jobId });
        }

        // ========== DIAGNOSTICO RANGO (AJAX GET) ==========

        [HttpGet("DiagnosticoRango")]
        public async Task<IActionResult> DiagnosticoRango(string codPersonal, string fechaInicio, string fechaFin)
        {
            if (!DateTime.TryParseExact(fechaInicio, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var dtInicio)
             || !DateTime.TryParseExact(fechaFin,    "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var dtFin))
                return Json(new { ok = false, mensaje = "Formato de fecha inválido." });

            try
            {
                var data = await _compensacionesService.DiagnosticoRangoAsync(CodEmpresaAquarius, codPersonal, dtInicio, dtFin);
                return Json(new { ok = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en DiagnosticoRango: personal={Personal}", codPersonal);
                return Json(new { ok = false, mensaje = "Error al obtener diagnóstico." });
            }
        }

        // ========== CONSULTAR ESTADO DE APLICACIÓN (polling) ==========

        [HttpGet("ConsultarAplicacion")]
        public IActionResult ConsultarAplicacion(string jobId)
        {
            var job = _compensacionJobService.ObtenerEstado(jobId);
            if (job == null)
                return Json(new { ok = false, mensaje = "Job no encontrado." });

            return Json(new
            {
                ok         = true,
                estado     = job.Estado.ToString(),
                completado = job.Estado is CompensacionEstado.Completado or CompensacionEstado.Error,
                exito      = job.Estado == CompensacionEstado.Completado,
                mensaje    = job.Estado switch
                {
                    CompensacionEstado.Pendiente  => "En cola, esperando procesamiento…",
                    CompensacionEstado.EnProceso  => "Aplicación en proceso…",
                    CompensacionEstado.Completado => "Compensaciones aplicadas correctamente.",
                    CompensacionEstado.Error      => $"Error: {job.MensajeError}",
                    _                             => "Estado desconocido"
                },
                resultado     = job.Resultado,
                duracionSeg   = job.FinalizadoEn.HasValue && job.IniciadoEn.HasValue
                    ? (int)(job.FinalizadoEn.Value - job.IniciadoEn.Value).TotalSeconds
                    : (int?)null
            });
        }

        // ========== MASIVA — Vista principal ==========

        [HttpGet("Masiva")]
        public IActionResult Masiva()
        {
            return View("~/Views/RecursosHumanos/Aquarius/Compensaciones/Masiva/Index.cshtml");
        }

        // ========== MASIVA — Empleados disponibles (paginado) ==========

        [HttpGet("Masiva/EmpleadosDisponibles")]
        public async Task<IActionResult> EmpleadosDisponibles(string fechaOrigen, string tipoOrigen, int page = 1)
        {
            if (!DateTime.TryParseExact(fechaOrigen, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var dtOrigen))
                return Json(new { ok = false, mensaje = "Formato de fecha inválido." });

            if (string.IsNullOrWhiteSpace(tipoOrigen) || !"EDB".Contains(tipoOrigen))
                return Json(new { ok = false, mensaje = "Tipo de origen inválido. Use E, D o B." });

            page = Math.Max(1, page);

            try
            {
                var (items, total) = await _compensacionesService.ListarEmpleadosDisponiblesAsync(
                    CodEmpresaAquarius, dtOrigen, tipoOrigen, page, PageSize);

                return Json(new
                {
                    ok         = true,
                    data       = items,
                    total,
                    page,
                    pageSize   = PageSize,
                    totalPages = (int)Math.Ceiling(total / (double)PageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en EmpleadosDisponibles: tipoOrigen={TipoOrigen}", tipoOrigen);
                return Json(new { ok = false, mensaje = "Error al obtener empleados." });
            }
        }

        // ========== MASIVA — Ejecutar evento (encola job) ==========

        [HttpPost("Masiva/EjecutarEvento")]
        [ValidateAntiForgeryToken]
        public IActionResult EjecutarEventoMasivo(
            [FromForm] string fechaOrigen, [FromForm] string fechaDestino,
            [FromForm] string tipoOrigen, [FromForm] string tipoCompensacion,
            [FromForm] string? listaPersonal)
        {
            if (!DateTime.TryParseExact(fechaOrigen, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var dtOrigen)
             || !DateTime.TryParseExact(fechaDestino, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var dtDestino))
                return Json(new { ok = false, mensaje = "Formato de fecha inválido." });

            if (string.IsNullOrWhiteSpace(tipoOrigen) || !"EDB".Contains(tipoOrigen))
                return Json(new { ok = false, mensaje = "Tipo de origen inválido." });

            if (string.IsNullOrWhiteSpace(tipoCompensacion) || !"TFANPI".Contains(tipoCompensacion))
                return Json(new { ok = false, mensaje = "Tipo de compensación inválido." });

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

            var jobId = _masivaJobService.Encolar(
                CodEmpresaAquarius, dtOrigen, dtDestino,
                tipoOrigen[0], tipoCompensacion[0],
                listaPersonal ?? string.Empty, connStr);

            return Json(new { ok = true, jobId });
        }

        // ========== MASIVA — Estado del job (polling) ==========

        [HttpGet("Masiva/EstadoEvento")]
        public IActionResult EstadoEventoMasivo(string jobId)
        {
            var job = _masivaJobService.ObtenerEstado(jobId);
            if (job == null)
                return Json(new { ok = false, mensaje = "Job no encontrado." });

            return Json(new
            {
                ok         = true,
                estado     = job.Estado.ToString(),
                completado = job.Estado is MasivaEventoEstado.Completado or MasivaEventoEstado.Error,
                exito      = job.Estado == MasivaEventoEstado.Completado,
                mensaje    = job.Estado switch
                {
                    MasivaEventoEstado.Pendiente  => "En cola, esperando procesamiento…",
                    MasivaEventoEstado.EnProceso  => "Procesando compensaciones…",
                    MasivaEventoEstado.Completado => "Compensaciones aplicadas correctamente.",
                    MasivaEventoEstado.Error      => $"Error: {job.MensajeError}",
                    _                             => "Estado desconocido"
                },
                resultado   = job.Resultado,
                duracionSeg = job.FinalizadoEn.HasValue && job.IniciadoEn.HasValue
                    ? (int)(job.FinalizadoEn.Value - job.IniciadoEn.Value).TotalSeconds
                    : (int?)null
            });
        }
    }
}
