using FabricaHilos.Models.RecursosHumanos;
using FabricaHilos.Services.RecursosHumanos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FabricaHilos.Controllers.RecursosHumanos.Aquarius
{
    [Authorize]
    [Route("RecursosHumanos/Aquarius/AuthHoras")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class AuthHorasController : Controller
    {
        private readonly IAuthHorasService _service;
        private readonly ILogger<AuthHorasController> _logger;

        private const string SessUsuario  = "AuthHoras_CodUsuario";
        private const string SessNombre   = "AuthHoras_NomUsuario";
        private const string SessEmpresa  = "AuthHoras_CodEmpresa";
        private const string SessEsAdmin  = "AuthHoras_EsAdm";
        private const string SessCntEmp   = "AuthHoras_CntEmpresas";
        private const string SessSsoFallo = "AuthHoras_SsoFallo";

        public AuthHorasController(IAuthHorasService service, ILogger<AuthHorasController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        // ── INDEX — SSO automático (Logix→Aquarius); fallback a login manual ──
        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString(SessUsuario)))
                return View("~/Views/RecursosHumanos/Aquarius/AuthHoras/Index.cshtml");

            // Intento de login automático (SSO) con el usuario Logix/SIG ya autenticado.
            // Se cachea en sesión el resultado negativo para no repetir el round-trip a
            // Oracle en cada visita a Index() dentro de la misma sesión (el usuario Logix
            // no cambia mientras la sesión esté activa).
            var oracleUser = HttpContext.Session.GetString("OracleUser");
            var ssoYaFallo = HttpContext.Session.GetString(SessSsoFallo) == oracleUser;
            if (!string.IsNullOrEmpty(oracleUser) && !ssoYaFallo)
            {
                var ssoResult = await _service.LoginPorLogixAsync(oracleUser);
                if (ssoResult.Ok)
                {
                    SetSesionSupervisor(ssoResult);
                    return RedirectToAction("Dashboard");
                }

                HttpContext.Session.SetString(SessSsoFallo, oracleUser);

                _logger.LogInformation(
                    "SSO Logix→Aquarius no disponible para {OracleUser}: {Mensaje}. Se muestra login manual.",
                    oracleUser, ssoResult.Mensaje);

                // Mensaje informativo (no es un error del usuario): explica por qué
                // debe ingresar manualmente pese a estar autenticado en el portal.
                TempData["LoginInfo"] = ssoResult.Mensaje switch
                {
                    "USUARIO_LOGIX_NO_ENCONTRADO"     => null, // usuario Logix no registrado en Aquarius; sin pista útil, se omite
                    "EMPLEADO_NO_ENCONTRADO_AQUARIUS" => "No se encontró su registro de empleado en Aquarius. Ingrese manualmente o contacte a Sistemas.",
                    "SIN_USUARIO_AQUARIUS"            => "Su usuario de Aquarius aún no ha sido creado. Ingrese manualmente o contacte a Sistemas.",
                    "USUARIO_BAJA"                     => "Su usuario de Aquarius está dado de baja. Contacte a Sistemas.",
                    _                                  => null
                };
            }

            return View("~/Views/RecursosHumanos/Aquarius/AuthHoras/Login.cshtml");
        }

        // ── POST LOGIN (manual, fallback si el SSO no resolvió el usuario) ──
        [HttpPost("Login")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login([FromForm] AuthHorasLoginRequest req)
        {
            var result = await _service.LoginAsync(req.CodUsuario);

            if (!result.Ok)
            {
                TempData["LoginError"] = result.Mensaje switch
                {
                    "CREDENCIAL_INVALIDA" => "Usuario o contraseña incorrectos.",
                    "USUARIO_BAJA"        => "El usuario está dado de baja.",
                    _                     => result.Mensaje
                };
                return View("~/Views/RecursosHumanos/Aquarius/AuthHoras/Login.cshtml");
            }

            SetSesionSupervisor(result);
            return RedirectToAction("Dashboard");
        }

        // ── Helper: puebla la sesión del supervisor a partir del resultado de login ──
        private void SetSesionSupervisor(AuthHorasLoginResult result)
        {
            HttpContext.Session.SetString(SessUsuario, result.CodUsuario);
            HttpContext.Session.SetString(SessNombre,  result.NomUsuario);
            HttpContext.Session.SetString(SessEsAdmin, result.EsAdmAlguna);
            HttpContext.Session.SetString(SessCntEmp,  result.CntEmpresas.ToString());

            if (!string.IsNullOrEmpty(result.CodEmpresaUnica))
                HttpContext.Session.SetString(SessEmpresa, result.CodEmpresaUnica);
            else
                HttpContext.Session.Remove(SessEmpresa);
        }

        // ── LOGOUT SUPERVISOR ──────────────────────────────────────────────
        [HttpPost("Logout")]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Remove(SessUsuario);
            HttpContext.Session.Remove(SessNombre);
            HttpContext.Session.Remove(SessEmpresa);
            HttpContext.Session.Remove(SessEsAdmin);
            HttpContext.Session.Remove(SessCntEmp);
            HttpContext.Session.Remove(SessSsoFallo);
            return RedirectToAction("Index");
        }

        // ── DASHBOARD (2 tarjetas) ───────────────────────────────────
        [HttpGet("Dashboard")]
        public IActionResult Dashboard()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString(SessUsuario)))
                return RedirectToAction("Index");

            return View("~/Views/RecursosHumanos/Aquarius/AuthHoras/Dashboard.cshtml");
        }

        // ── AUTORIZACIÓN DE HORAS (vista principal) ────────────────────
        [HttpGet("Autorizar")]
        public IActionResult Autorizar()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString(SessUsuario)))
                return RedirectToAction("Index");

            return View("~/Views/RecursosHumanos/Aquarius/AuthHoras/Index.cshtml");
        }

        // ── CONSULTA RESUMEN HE ──────────────────────────────────
        [HttpGet("ConsultaResumen")]
        public IActionResult ConsultaResumen()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString(SessUsuario)))
                return RedirectToAction("Index");

            return View("~/Views/RecursosHumanos/Aquarius/AuthHoras/Resumen.cshtml");
        }

        // ── API: supervisores (solo admin) ────────────────────────────
        [HttpGet("Supervisores")]
        public async Task<IActionResult> Supervisores([FromQuery] string empresa)
        {
            var usuario = HttpContext.Session.GetString(SessUsuario);
            if (string.IsNullOrEmpty(usuario)) return Unauthorized();

            var lista = await _service.ObtenerSupervisoresAsync(usuario, empresa);
            return Ok(lista);
        }

        // ── API: resumen HE por empleado ─────────────────────────────
        [HttpGet("ResumenHe")]
        public async Task<IActionResult> ResumenHe(
            [FromQuery] string empresa,
            [FromQuery] string desde,
            [FromQuery] string hasta,
            [FromQuery] string? supervisor)
        {
            var usuario = HttpContext.Session.GetString(SessUsuario);
            if (string.IsNullOrEmpty(usuario)) return Unauthorized();

            // Solo admin puede consultar en nombre de otro supervisor
            string codConsulta;
            if (!string.IsNullOrEmpty(supervisor))
            {
                var esAdmin = HttpContext.Session.GetString(SessEsAdmin);
                if (esAdmin != "S") return Forbid();
                codConsulta = supervisor;
            }
            else
            {
                codConsulta = usuario;
            }

            var lista = await _service.ObtenerResumenHeAsync(codConsulta, empresa, desde, hasta);
            return Ok(lista);
        }

        // ── API: grabar visto bueno (todos los supervisores) ───────────────────
        [HttpPost("GrabarVisado")]
        public async Task<IActionResult> GrabarVisado([FromBody] AuthHorasGrabarVisadoRequest req)
        {
            var usuario = HttpContext.Session.GetString(SessUsuario);
            if (string.IsNullOrEmpty(usuario)) return Unauthorized();

            var result = await _service.GrabarVisadoAsync(usuario, req);
            return Ok(result);
        }

        // ── API: lista de empleados ─────────────────────────────────
        [HttpGet("Empleados")]
        public async Task<IActionResult> Empleados([FromQuery] string empresa)
        {
            var usuario = HttpContext.Session.GetString(SessUsuario);
            if (string.IsNullOrEmpty(usuario))
                return Unauthorized();

            var lista = await _service.ObtenerEmpleadosAsync(usuario, empresa);
            return Ok(lista);
        }

        // ── API: tareo de un empleado ──────────────────────────────────────
        [HttpGet("Tareo")]
        public async Task<IActionResult> Tareo(
            [FromQuery] string empresa,
            [FromQuery] string personal,
            [FromQuery] string desde,
            [FromQuery] string hasta)
        {
            var usuario = HttpContext.Session.GetString(SessUsuario);
            if (string.IsNullOrEmpty(usuario))
                return Unauthorized();

            var lista = await _service.ObtenerTareoAsync(usuario, empresa, personal, desde, hasta);
            return Ok(lista);
        }

        // ── API: grabar autorización ───────────────────────────────────────
        [HttpPost("Grabar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Grabar([FromBody] AuthHorasGrabarRequest req)
        {
            var usuario = HttpContext.Session.GetString(SessUsuario);
            if (string.IsNullOrEmpty(usuario))
                return Unauthorized();

            // Validar tipo permitido (1=AuthHEA, 2=AuthHED, 3=DesAuthHEA, 4=DesAuthHED, 5=AuthHEO, 6=DesAuthHEO)
            var tiposValidos = new[] { "1", "2", "3", "4", "5", "6" };
            if (!tiposValidos.Contains(req.Tipo))
                return BadRequest(new { ok = false, mensaje = "Tipo de operación no válido." });

            // Validar formato fecha dd/MM/yyyy
            if (!System.Text.RegularExpressions.Regex.IsMatch(req.Fecha, @"^\d{2}/\d{2}/\d{4}$"))
                return BadRequest(new { ok = false, mensaje = "Formato de fecha inválido." });

            // Validar formato valor HH:MM
            if (!System.Text.RegularExpressions.Regex.IsMatch(req.Valor, @"^\d{2}:\d{2}$"))
                return BadRequest(new { ok = false, mensaje = "Formato de horas inválido." });

            // Prefijo de trazabilidad: identifica qué usuario del sistema principal autorizó
            var oracleUser   = HttpContext.Session.GetString("OracleUser") ?? string.Empty;
            var connKey      = HttpContext.Session.GetString("EmpresaConexion") ?? string.Empty;
            var empresaLabel = connKey switch
            {
                "ArbonaConnection"     => "ARBONA",
                "SolsaConnection"      => "SOLSA",
                "LaColonialConnection" => "COLONIAL",
                _                     => "COLONIAL"
            };
            const int maxObs = 100;
            var prefijo = $"[{empresaLabel}-{oracleUser}] [AQUARIUS-{usuario}]";
            var obsCompleta = string.IsNullOrWhiteSpace(req.Observaciones)
                ? prefijo
                : $"{prefijo} {req.Observaciones}";
            req.Observaciones = obsCompleta.Length > maxObs
                ? obsCompleta[..maxObs]
                : obsCompleta;

            var result = await _service.GrabarAutorizacionAsync(usuario, req);
            return Ok(result);
        }

        // ── API: datos de sesión (para inicializar la vista) ──────────────
        [HttpGet("SesionInfo")]
        public IActionResult SesionInfo()
        {
            var usuario = HttpContext.Session.GetString(SessUsuario);
            if (string.IsNullOrEmpty(usuario))
                return Unauthorized();

            // Empresa que el usuario tiene en el sistema principal (login Oracle)
            var conexionSistema = HttpContext.Session.GetString("EmpresaConexion");
            var codEmpresaSistema = conexionSistema != null
                ? FabricaHilos.Services.OracleServiceBase.GetCodEmpresaAquarius(conexionSistema)
                : null;

            // Largo del prefijo fijo para que el frontend calcule el maxlength disponible
            var oracleUserSes   = HttpContext.Session.GetString("OracleUser") ?? string.Empty;
            var connKeySes      = HttpContext.Session.GetString("EmpresaConexion") ?? string.Empty;
            var empresaLabelSes = connKeySes switch
            {
                "ArbonaConnection"     => "ARBONA",
                "SolsaConnection"      => "SOLSA",
                "LaColonialConnection" => "COLONIAL",
                _                     => "COLONIAL"
            };
            var prefijoSes    = $"[{empresaLabelSes}-{oracleUserSes}] [AQUARIUS-{usuario}] ";
            var maxObsUsuario = Math.Max(10, 100 - prefijoSes.Length);

            return Ok(new
            {
                codUsuario        = usuario,
                nomUsuario        = HttpContext.Session.GetString(SessNombre),
                codEmpresa        = HttpContext.Session.GetString(SessEmpresa),
                esAdmin           = HttpContext.Session.GetString(SessEsAdmin),
                cntEmpresas       = HttpContext.Session.GetString(SessCntEmp),
                codEmpresaSistema = codEmpresaSistema,
                maxObsUsuario     = maxObsUsuario,
            });
        }
    }
}
