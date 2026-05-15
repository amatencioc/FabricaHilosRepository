using FabricaHilos.Services.RecursosHumanos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

namespace FabricaHilos.Controllers.RecursosHumanos.Aquarius
{
    [Authorize]
    [Route("RecursosHumanos/Aquarius/CompensacionDdc")]
    public class CompensacionDdcController : OracleBaseController
    {
        private readonly ICompensacionDdcService _service;
        private readonly ILogger<CompensacionDdcController> _logger;

        public CompensacionDdcController(
            ICompensacionDdcService service,
            ILogger<CompensacionDdcController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        // ── INDEX ──────────────────────────────────────────────────────────────

        [HttpGet("")]
        [HttpGet("Index")]
        public IActionResult Index()
        {
            return View("~/Views/RecursosHumanos/Aquarius/Compensacion/DiaLibrePorCompensar/Index.cshtml");
        }

        // ── LISTAR DDC RANGO (GET, devuelve JSON) ─────────────────────────────

        [HttpGet("ListarDdcRango")]
        public async Task<IActionResult> ListarDdcRango(
            string fechaInicio,
            string fechaFin,
            string? nombre = null,
            string? fechaHeInicio = null,
            string? fechaHeFin = null)
        {
            try
            {
                var resultado = await _service.ListarDdcRangoAsync(
                    CodEmpresaAquarius,
                    fechaInicio,
                    fechaFin,
                    string.IsNullOrWhiteSpace(nombre) ? null : nombre,
                    string.IsNullOrWhiteSpace(fechaHeInicio) ? null : fechaHeInicio,
                    string.IsNullOrWhiteSpace(fechaHeFin)    ? null : fechaHeFin,
                    soloDdc: true);

                return Json(new { ok = true, data = resultado });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ListarDdcRango");
                return Json(new { ok = false, error = ex.Message });
            }
        }

        // ── LISTAR HE DE UN EMPLEADO (GET, devuelve JSON) ─────────────────────

        [HttpGet("ListarHe")]
        public async Task<IActionResult> ListarHe(
            string codPersonal,
            string fechaHeInicio,
            string fechaHeFin)
        {
            try
            {
                var resultado = await _service.ListarHePersonalAsync(
                    CodEmpresaAquarius,
                    codPersonal,
                    fechaHeInicio,
                    fechaHeFin);

                return Json(new { ok = true, data = resultado });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ListarHe cod={Cod}", codPersonal);
                return Json(new { ok = false, error = ex.Message });
            }
        }

        // ── CALCULAR DDC PREVIEW (POST, devuelve JSON) ────────────────────────

        [HttpPost("Calcular")]
        public async Task<IActionResult> Calcular(
            string fechaInicio,
            string fechaFin,
            string listaPersonal,
            string? fechaHeInicio = null,
            string? fechaHeFin = null)
        {
            try
            {
                var resultado = await _service.CalcularDdcAsync(
                    CodEmpresaAquarius,
                    fechaInicio,
                    fechaFin,
                    listaPersonal,
                    string.IsNullOrWhiteSpace(fechaHeInicio) ? null : fechaHeInicio,
                    string.IsNullOrWhiteSpace(fechaHeFin)    ? null : fechaHeFin);

                return Json(new { ok = true, data = resultado });
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Oracle error en Calcular DDC");
                return Json(new { ok = false, error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en Calcular DDC");
                return Json(new { ok = false, error = ex.Message });
            }
        }

        // ── REGISTRAR MASIVO (POST, devuelve JSON) ────────────────────────────

        [HttpPost("RegistrarMasivo")]
        public async Task<IActionResult> RegistrarMasivo(
            string fechaInicio,
            string fechaFin,
            string listaPersonal,
            string? fechaHeInicio = null,
            string? fechaHeFin = null)
        {
            try
            {
                var resultado = await _service.RegistrarDdcMasivoAsync(
                    CodEmpresaAquarius,
                    fechaInicio,
                    fechaFin,
                    listaPersonal,
                    string.IsNullOrWhiteSpace(fechaHeInicio) ? null : fechaHeInicio,
                    string.IsNullOrWhiteSpace(fechaHeFin)    ? null : fechaHeFin);

                return Json(new { ok = true, data = resultado });
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Oracle error en RegistrarMasivo DDC");
                return Json(new { ok = false, error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en RegistrarMasivo DDC");
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
                _logger.LogWarning(ex, "Commit DDC sin transacción activa");
                return Json(new { ok = false, error = ex.Message, sinTransaccion = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en Commit DDC");
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
                _logger.LogError(ex, "Error en Rollback DDC");
                return Json(new { ok = false, error = ex.Message });
            }
        }

        // ── CONSULTAR EVENTO (GET, devuelve JSON) ─────────────────────────────

        [HttpGet("ConsultarEvento")]
        public async Task<IActionResult> ConsultarEvento(long idEvento)
        {
            try
            {
                var resultado = await _service.ConsultarEventoDdcAsync(idEvento);
                return Json(new { ok = true, data = resultado });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ConsultarEvento DDC id={Id}", idEvento);
                return Json(new { ok = false, error = ex.Message });
            }
        }

        // ── CONSULTAR COMP (GET, devuelve JSON) ───────────────────────────────

        [HttpGet("ConsultarComp")]
        public async Task<IActionResult> ConsultarComp(long idCompen)
        {
            try
            {
                var resultado = await _service.ConsultarCompDdcAsync(idCompen);
                return Json(new { ok = true, data = resultado });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ConsultarComp DDC id={Id}", idCompen);
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
                var resultado = await _service.ConsultarRangoDdcAsync(
                    CodEmpresaAquarius,
                    string.IsNullOrWhiteSpace(codPersonal) ? null : codPersonal,
                    fechaInicio,
                    fechaFin);

                return Json(new { ok = true, data = resultado });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ConsultarRango DDC");
                return Json(new { ok = false, error = ex.Message });
            }
        }
    }
}
