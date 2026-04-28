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

        public CompensacionDiaDiaController(
            ICompensacionDiaDiaService service,
            ILogger<CompensacionDiaDiaController> logger)
        {
            _service = service;
            _logger  = logger;
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
            string? listaPersonal)
        {
            try
            {
                var resultado = await _service.CalcularHorasEventoAsync(
                    CodEmpresaAquarius,
                    fechaOrigen,
                    string.IsNullOrWhiteSpace(fechaDestino) ? null : fechaDestino,
                    tipoOrigen,
                    string.IsNullOrWhiteSpace(listaPersonal) ? null : listaPersonal);

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
            string? horasMax)
        {
            try
            {
                var resultado = await _service.RegistrarEventoMasivoAsync(
                    CodEmpresaAquarius,
                    fechaOrigen,
                    fechaDestino,
                    tipoOrigen,
                    tipoCompensacion,
                    listaPersonal,
                    string.IsNullOrWhiteSpace(horasMax) ? null : horasMax);

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
        public async Task<IActionResult> VerEstado(long idCompen)
        {
            try
            {
                var dto = await _service.VerEstadoAsync(idCompen);
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
            int tamPagina = 10)
        {
            try
            {
                var resultado = await _service.ListarEmpleadosRangoAsync(
                    CodEmpresaAquarius,
                    fechaInicio,
                    fechaFin,
                    string.IsNullOrWhiteSpace(codPersonal) ? null : codPersonal,
                    string.IsNullOrWhiteSpace(nombre)      ? null : nombre,
                    pagina,
                    tamPagina);

                return Json(new { ok = true, data = resultado.Items, totalFilas = resultado.Total, pagina, tamPagina });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ListarEmpleadosRango CompensacionDiaDia");
                return Json(new { ok = false, error = ex.Message });
            }
        }

        // ── CONSULTAR RANGO (GET, devuelve JSON) ──────────────────────────────

        [HttpGet("ConsultarRango")]
        public async Task<IActionResult> ConsultarRango(
            string? codPersonal,
            string fechaInicio,
            string fechaFin)
        {
            try
            {
                var resultado = await _service.ConsultarRangoAsync(
                    CodEmpresaAquarius,
                    string.IsNullOrWhiteSpace(codPersonal) ? null : codPersonal,
                    fechaInicio,
                    fechaFin);

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
