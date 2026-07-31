using FabricaHilos.Services;
using FabricaHilos.Services.RecursosHumanos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

namespace FabricaHilos.Controllers.RecursosHumanos.Aquarius
{
    [Authorize]
    [Route("RecursosHumanos/Aquarius/CompensacionDiaDia")]
    public class CompensacionDiaDiaController : OracleBaseController
    {
        private readonly ICompensacionDiaDiaService _service;
        private readonly ILogger<CompensacionDiaDiaController> _logger;

        // Empresas que comparten el paquete PKG_ARB_COMP_DIA_DIA y pueden alternarse
        // entre si mediante el ddl de seleccion de empresa en la vista.
        private static readonly HashSet<string> _empresasIntercambiables = new()
        {
            "ArbonaConnection", "SolsaConnection"
        };

        public CompensacionDiaDiaController(
            ICompensacionDiaDiaService service,
            ILogger<CompensacionDiaDiaController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        // -- EMPRESAS DISPONIBLES (GET, devuelve JSON) --
        // Devuelve la lista de empresas seleccionables (Arbona/Solsa) cuando la
        // sesion activa es una de ellas, para poblar el ddl en la vista.

        [HttpGet("EmpresasDisponibles")]
        public IActionResult EmpresasDisponibles()
        {
            var empresaActual = HttpContext.Session.GetString("EmpresaConexion") ?? "LaColonialConnection";

            if (!_empresasIntercambiables.Contains(empresaActual))
            {
                return Json(new { ok = true, habilitado = false, empresas = Array.Empty<object>(), actual = empresaActual });
            }

            var empresas = _empresasIntercambiables
                .Select(connKey => new
                {
                    connKey,
                    codigo = OracleServiceBase.GetCodEmpresaAquarius(connKey),
                    descripcion = connKey == "ArbonaConnection" ? "ARBONA" : "SOLSA"
                })
                .OrderBy(e => e.descripcion)
                .ToList();

            return Json(new { ok = true, habilitado = true, empresas, actual = empresaActual });
        }

        // -- Helpers de resolucion de empresa seleccionada --

        /// <summary>
        /// Valida el parametro empresaSel enviado desde la vista: solo se acepta si
        /// la sesion activa es Arbona o Solsa y el valor es una de esas dos claves.
        /// En cualquier otro caso se ignora (se usa la empresa de sesion).
        /// </summary>
        private string? ResolverEmpresaSel(string? empresaSel)
        {
            var empresaActual = HttpContext.Session.GetString("EmpresaConexion") ?? "LaColonialConnection";

            if (!_empresasIntercambiables.Contains(empresaActual))
                return null;

            if (string.IsNullOrWhiteSpace(empresaSel) || !_empresasIntercambiables.Contains(empresaSel))
                return null;

            return empresaSel;
        }

        private string CodEmpresaAquariusEfectivo(string? empresaSel)
        {
            var resuelta = ResolverEmpresaSel(empresaSel);
            return resuelta != null ? OracleServiceBase.GetCodEmpresaAquarius(resuelta) : CodEmpresaAquarius;
        }

        // ── INDEX ──────────────────────────────────────────────────────────────

        [HttpGet("")]
        [HttpGet("Index")]
        public IActionResult Index()
        {
            return View("~/Views/RecursosHumanos/Aquarius/Compensacion/DiaPorDia/Index.cshtml");
        }

        // ── PASO 1: CALCULAR PREVIEW (POST, devuelve JSON) ─────────────────────

        [HttpPost("Calcular")]
        public async Task<IActionResult> Calcular(
            string fechaOrigen,
            string? fechaDestino,
            string tipoOrigen,
            string? listaPersonal,
            string? fechaHorasInicio = null,
            string? fechaHorasFin   = null,
            string? empresaSel      = null)
        {
            try
            {
                var resultado = await _service.CalcularHorasEventoAsync(
                    CodEmpresaAquariusEfectivo(empresaSel),
                    fechaOrigen,
                    string.IsNullOrWhiteSpace(fechaDestino) ? null : fechaDestino,
                    tipoOrigen,
                    string.IsNullOrWhiteSpace(listaPersonal) ? null : listaPersonal,
                    string.IsNullOrWhiteSpace(fechaHorasInicio) ? null : fechaHorasInicio,
                    string.IsNullOrWhiteSpace(fechaHorasFin)   ? null : fechaHorasFin,
                    ResolverEmpresaSel(empresaSel));

                return Json(new { ok = true, data = resultado });
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Oracle error en Calcular CompensacionDiaDia");
                return Json(new { ok = false, error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en Calcular CompensacionDiaDia");
                return Json(new { ok = false, error = ex.Message });
            }
        }

        // ── PASO 2: REGISTRAR MASIVO (POST, devuelve JSON) ─────────────────────

        [HttpPost("RegistrarMasivo")]
        public async Task<IActionResult> RegistrarMasivo(
            string fechaOrigen,
            string fechaDestino,
            string tipoOrigen,
            string tipoCompensacion,
            string listaPersonal,
            string? horasMax,
            string? fechaHorasInicio = null,
            string? fechaHorasFin   = null,
            string? empresaSel      = null)
        {
            try
            {
                var resultado = await _service.RegistrarEventoMasivoAsync(
                    CodEmpresaAquariusEfectivo(empresaSel),
                    fechaOrigen,
                    fechaDestino,
                    tipoOrigen,
                    tipoCompensacion,
                    listaPersonal,
                    string.IsNullOrWhiteSpace(horasMax) ? null : horasMax,
                    string.IsNullOrWhiteSpace(fechaHorasInicio) ? null : fechaHorasInicio,
                    string.IsNullOrWhiteSpace(fechaHorasFin)   ? null : fechaHorasFin,
                    ResolverEmpresaSel(empresaSel));

                return Json(new { ok = true, data = resultado });
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Oracle error en RegistrarMasivo CompensacionDiaDia");
                return Json(new { ok = false, error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en RegistrarMasivo CompensacionDiaDia");
                return Json(new { ok = false, error = ex.Message });
            }
        }

        // ── VER ESTADO de una compensación (GET, devuelve JSON) ────────────────

        [HttpGet("VerEstado/{idCompen:long}")]
        public async Task<IActionResult> VerEstado(long idCompen, string? empresaSel = null)
        {
            try
            {
                var dto = await _service.VerEstadoAsync(idCompen, ResolverEmpresaSel(empresaSel));
                if (dto == null)
                    return Json(new { ok = false, error = "Compensación no encontrada." });
                return Json(new { ok = true, data = dto });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en VerEstado id={Id}", idCompen);
                return Json(new { ok = false, error = ex.Message });
            }
        }

        // ── COMMIT (POST) ─────────────────────────────────────────────────────

        [HttpPost("Commit")]
        public async Task<IActionResult> Commit()
        {
            try
            {
                await _service.CommitAsync();
                return Json(new { ok = true });
            }
            catch (InvalidOperationException ex)
            {
                // Transacción no encontrada: servidor reiniciado o sesión expirada
                _logger.LogWarning(ex, "Commit sin transacción activa");
                return Json(new { ok = false, error = ex.Message, sinTransaccion = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en Commit CompensacionDiaDia");
                return Json(new { ok = false, error = ex.Message });
            }
        }

        // ── ROLLBACK (POST) ───────────────────────────────────────────────────

        [HttpPost("Rollback")]
        public async Task<IActionResult> Rollback()
        {
            try
            {
                await _service.RollbackAsync();
                return Json(new { ok = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en Rollback CompensacionDiaDia");
                return Json(new { ok = false, error = ex.Message });
            }
        }

        // ── LISTAR EMPLEADOS RANGO (GET, devuelve JSON) ───────────────────────

        [HttpGet("ListarEmpleadosRango")]
        public async Task<IActionResult> ListarEmpleadosRango(
            string fechaInicio,
            string fechaFin,
            string? codPersonal,
            string? nombre,
            int pagina    = 1,
            int tamPagina = 10,
            string? fechaHorasInicio = null,
            string? fechaHorasFin   = null,
            string? sortBy           = null,
            string? sortDir          = null,
            string? empresaSel       = null)
        {
            try
            {
                var resultado = await _service.ListarEmpleadosRangoAsync(
                    CodEmpresaAquariusEfectivo(empresaSel),
                    fechaInicio,
                    fechaFin,
                    string.IsNullOrWhiteSpace(codPersonal) ? null : codPersonal,
                    string.IsNullOrWhiteSpace(nombre)      ? null : nombre,
                    pagina,
                    tamPagina,
                    string.IsNullOrWhiteSpace(fechaHorasInicio) ? null : fechaHorasInicio,
                    string.IsNullOrWhiteSpace(fechaHorasFin)   ? null : fechaHorasFin,
                    string.IsNullOrWhiteSpace(sortBy)  ? null : sortBy,
                    string.IsNullOrWhiteSpace(sortDir) ? null : sortDir,
                    ResolverEmpresaSel(empresaSel));

                return Json(new { ok = true, data = resultado.Items, totalFilas = resultado.Total, pagina, tamPagina });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ListarEmpleadosRango CompensacionDiaDia");
                return Json(new { ok = false, error = ex.Message });
            }
        }

        // ── CONSULTAR EVENTO (GET, devuelve JSON) ─────────────────────────────

        [HttpGet("ConsultarEvento")]
        public async Task<IActionResult> ConsultarEvento(long idEvento, string? empresaSel = null)
        {
            try
            {
                var resultado = await _service.ConsultarEventoAsync(idEvento, ResolverEmpresaSel(empresaSel));
                return Json(new { ok = true, data = resultado });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ConsultarEvento id={Id}", idEvento);
                return Json(new { ok = false, error = ex.Message });
            }
        }

        // ── DETALLE HORAS EMPLEADO (GET, devuelve JSON) ───────────────────────

        [HttpGet("DetalleHorasEmpleado")]
        public async Task<IActionResult> DetalleHorasEmpleado(
            string codPersonal,
            string fechaHorasInicio,
            string fechaHorasFin,
            string? empresaSel = null)
        {
            try
            {
                var resultado = await _service.DetalleHorasEmpleadoAsync(
                    CodEmpresaAquariusEfectivo(empresaSel),
                    codPersonal,
                    fechaHorasInicio,
                    fechaHorasFin,
                    ResolverEmpresaSel(empresaSel));
                return Json(new { ok = true, data = resultado });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en DetalleHorasEmpleado cod={Cod}", codPersonal);
                return Json(new { ok = false, error = ex.Message });
            }
        }

        // ── CONSULTAR RANGO (GET, devuelve JSON) ──────────────────────────────

        [HttpGet("ConsultarRango")]
        public async Task<IActionResult> ConsultarRango(
            string? codPersonal,
            string fechaInicio,
            string fechaFin,
            string? empresaSel = null)
        {
            try
            {
                var resultado = await _service.ConsultarRangoAsync(
                    CodEmpresaAquariusEfectivo(empresaSel),
                    string.IsNullOrWhiteSpace(codPersonal) ? null : codPersonal,
                    fechaInicio,
                    fechaFin,
                    ResolverEmpresaSel(empresaSel));

                return Json(new { ok = true, data = resultado });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ConsultarRango CompensacionDiaDia");
                return Json(new { ok = false, error = ex.Message });
            }
        }
    }
}
