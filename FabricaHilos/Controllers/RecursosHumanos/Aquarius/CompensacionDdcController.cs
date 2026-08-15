using FabricaHilos.Services;
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
        private readonly AcuerdoCompHeDocxService _acuerdoService;
        private readonly ILogger<CompensacionDdcController> _logger;

        // Empresas que comparten el paquete PKG_ARB_COMP_DDC y pueden alternarse
        // entre si mediante el ddl de seleccion de empresa en la vista.
        private static readonly HashSet<string> _empresasIntercambiables = new()
        {
            "ArbonaConnection", "SolsaConnection"
        };

        public CompensacionDdcController(
            ICompensacionDdcService service,
            AcuerdoCompHeDocxService acuerdoService,
            ILogger<CompensacionDdcController> logger)
        {
            _service        = service;
            _acuerdoService = acuerdoService;
            _logger         = logger;
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
            return View("~/Views/RecursosHumanos/Aquarius/Compensacion/DiaLibrePorCompensar/Index.cshtml");
        }

        // ── LISTAR DDC RANGO (GET, devuelve JSON) ─────────────────────────────

        [HttpGet("ListarDdcRango")]
        public async Task<IActionResult> ListarDdcRango(
            string fechaInicio,
            string fechaFin,
            string? nombre = null,
            string? fechaHeInicio = null,
            string? fechaHeFin = null,
            string? empresaSel = null)
        {
            try
            {
                var resultado = await _service.ListarDdcRangoAsync(
                    CodEmpresaAquariusEfectivo(empresaSel),
                    fechaInicio,
                    fechaFin,
                    string.IsNullOrWhiteSpace(nombre) ? null : nombre,
                    string.IsNullOrWhiteSpace(fechaHeInicio) ? null : fechaHeInicio,
                    string.IsNullOrWhiteSpace(fechaHeFin)    ? null : fechaHeFin,
                    soloDdc: true,
                    ResolverEmpresaSel(empresaSel));

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
            string fechaHeFin,
            string? empresaSel = null)
        {
            try
            {
                var resultado = await _service.ListarHePersonalAsync(
                    CodEmpresaAquariusEfectivo(empresaSel),
                    codPersonal,
                    fechaHeInicio,
                    fechaHeFin,
                    ResolverEmpresaSel(empresaSel));

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
            string? fechaHeFin = null,
            string? empresaSel = null)
        {
            try
            {
                var resultado = await _service.CalcularDdcAsync(
                    CodEmpresaAquariusEfectivo(empresaSel),
                    fechaInicio,
                    fechaFin,
                    listaPersonal,
                    string.IsNullOrWhiteSpace(fechaHeInicio) ? null : fechaHeInicio,
                    string.IsNullOrWhiteSpace(fechaHeFin)    ? null : fechaHeFin,
                    ResolverEmpresaSel(empresaSel));

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
            string? listaDdcFechas = null,
            string? fechaHeInicio = null,
            string? fechaHeFin = null,
            string? empresaSel = null)
        {
            try
            {
                var resultado = await _service.RegistrarDdcMasivoAsync(
                    CodEmpresaAquariusEfectivo(empresaSel),
                    fechaInicio,
                    fechaFin,
                    listaPersonal,
                    string.IsNullOrWhiteSpace(listaDdcFechas) ? null : listaDdcFechas,
                    string.IsNullOrWhiteSpace(fechaHeInicio)  ? null : fechaHeInicio,
                    string.IsNullOrWhiteSpace(fechaHeFin)     ? null : fechaHeFin,
                    ResolverEmpresaSel(empresaSel));

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
        public async Task<IActionResult> ConsultarEvento(long idEvento, string? empresaSel = null)
        {
            try
            {
                var resultado = await _service.ConsultarEventoDdcAsync(idEvento, ResolverEmpresaSel(empresaSel));
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
        public async Task<IActionResult> ConsultarComp(long idCompen, string? empresaSel = null)
        {
            try
            {
                var resultado = await _service.ConsultarCompDdcAsync(idCompen, ResolverEmpresaSel(empresaSel));
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
            string fechaFin,
            string? empresaSel = null)
        {
            try
            {
                var resultado = await _service.ConsultarRangoDdcAsync(
                    CodEmpresaAquariusEfectivo(empresaSel),
                    string.IsNullOrWhiteSpace(codPersonal) ? null : codPersonal,
                    fechaInicio,
                    fechaFin,
                    ResolverEmpresaSel(empresaSel));

                return Json(new { ok = true, data = resultado });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ConsultarRango DDC");
                return Json(new { ok = false, error = ex.Message });
            }
        }

        // ── EXPORTAR ACUERDO COMPENSACIÓN HE (GET, devuelve .docx) ───────────

        [HttpGet("ExportarAcuerdo")]
        public async Task<IActionResult> ExportarAcuerdo(
            string codPersonal,
            string fechaInicio,
            string fechaFin,
            string? empresaSel = null)
        {
            try
            {
                var datos = await _service.ConsultarRangoDdcAsync(
                    CodEmpresaAquariusEfectivo(empresaSel),
                    codPersonal,
                    fechaInicio,
                    fechaFin,
                    ResolverEmpresaSel(empresaSel));

                if (!datos.Any())
                    return Json(new { ok = false, error = "Sin registros para el empleado en el rango indicado." });

                var docBytes = await _acuerdoService.GenerarAsync(datos, fechaInicio, fechaFin);

                var f = fechaInicio.Replace("/", "").Replace("-", "");
                var nombreArchivo = $"AcuerdoCompHE_{codPersonal}_{f}.docx";
                return File(docBytes,
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    nombreArchivo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ExportarAcuerdo DDC cod={Cod}", codPersonal);
                return Json(new { ok = false, error = ex.Message });
            }
        }
    }
}
