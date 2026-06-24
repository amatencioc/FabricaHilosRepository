using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using FabricaHilos.Models;
using FabricaHilos.Logica;
using FabricaHilos.Services;

namespace FabricaHilos.Controllers.Account
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<AccountController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IMenuService _menuService;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<AccountController> logger,
            IConfiguration configuration,
            IMenuService menuService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _configuration = configuration;
            _menuService = menuService;
        }

        [HttpGet]
        public async Task<IActionResult> Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var oracleUser = HttpContext.Session.GetString("OracleUser");
                var oraclePass = HttpContext.Session.GetString("OraclePass");

                // Si la sesión Oracle está activa, refrescar AccesoWeb desde Oracle antes de
                // redirigir para evitar que una sesión obsoleta mande al usuario a un módulo
                // incorrecto (ej: tablet compartida donde el AccesoWeb anterior era "Seguridad"
                // y el usuario actual tiene "Produccion").
                if (!string.IsNullOrEmpty(oracleUser) && !string.IsNullOrEmpty(oraclePass))
                {
                    try
                    {
                        var loginOracle   = new Login(_configuration, _logger);
                        var usuarioOracle = await loginOracle.EncontrarUsuarioAsync(oracleUser, oraclePass);
                        if (!string.IsNullOrEmpty(usuarioOracle.c_user)
                            && !string.IsNullOrWhiteSpace(usuarioOracle.acceso_web))
                        {
                            HttpContext.Session.SetString("AccesoWeb", usuarioOracle.acceso_web);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "No se pudo refrescar AccesoWeb desde Oracle para {Usuario}; se usará el valor de sesión.", oracleUser);
                    }
                    // Verificar que AccesoWeb esté en sesión antes de redirigir.
                    // Si Oracle falló y la sesión fue limpiada por OracleBaseController,
                    // AccesoWeb estará vacío → forzar re-login para evitar aterrizar en
                    // un módulo incorrecto (fallback de GetLanding).
                    var accesoActual = HttpContext.Session.GetString("AccesoWeb");
                    if (string.IsNullOrWhiteSpace(accesoActual))
                    {
                        _logger.LogWarning(
                            "Sesión Identity válida para {Usuario} pero AccesoWeb vacío tras refresh Oracle; forzando re-login.",
                            oracleUser);
                        // SignOut + View() directo para evitar ERR_TOO_MANY_REDIRECTS
                        await _signInManager.SignOutAsync();
                        HttpContext.Session.Clear();
                        ViewData["ReturnUrl"] = returnUrl;
                        ViewData["InfoMsg"]   = "Tu sesión expiró. Por favor vuelve a iniciar sesión.";
                        return View();
                    }
                    return RedirectToLanding();
                }

                // Cookie web válida pero sesión Oracle expirada (ej: reinicio de la app)
                // → cerrar sesión web y mostrar login directamente (sin redirect para evitar
                // ERR_TOO_MANY_REDIRECTS: el browser necesita recibir el Set-Cookie de logout
                // en la misma respuesta antes de procesar otro redirect a Login).
                await _signInManager.SignOutAsync();
                HttpContext.Session.Clear();
                ViewData["ReturnUrl"] = returnUrl;
                ViewData["InfoMsg"]   = "Tu sesión expiró. Por favor vuelve a iniciar sesión.";
                return View();
            }
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> Login(string usuario, string password, bool recordarme, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (string.IsNullOrEmpty(usuario) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError(string.Empty, "Por favor ingrese el usuario y contraseña.");
                return View();
            }

            // Normalizar: eliminar espacios del usuario y contraseña que el browser/autocompletar
            // puede añadir silenciosamente, y convertir el usuario a mayúsculas (Oracle CS_USER
            // almacena siempre en mayúsculas: PERSARB2, no persarb2).
            usuario  = usuario.Trim().ToUpperInvariant();
            password = password.Trim();

            try
            {
                // 1. Validar contra Oracle Database (async con timeout interno)
                var loginOracle = new Login(_configuration, _logger);
                var usuarioOracle = await loginOracle.EncontrarUsuarioAsync(usuario, password);

                if (!string.IsNullOrEmpty(usuarioOracle.c_user))
                {
                    // Determinar connection string según la empresa del usuario
                    var connKeyEmpresa = usuarioOracle.Empresa switch
                    {
                        "ARBONA" => "ArbonaConnection",
                        "SOLSA"  => "SolsaConnection",
                        _        => "LaColonialConnection"
                    };

                    var adminUsers  = _configuration.GetSection("AdminUsers").Get<string[]>() ?? [];
                    var esAdmin     = adminUsers.Contains(usuario, StringComparer.OrdinalIgnoreCase);
                    var rolCorrecto = esAdmin ? "Admin" : "Trabajador";

                    // La autenticación es EXCLUSIVAMENTE Oracle (CS_USER).
                    // Si EncontrarUsuarioAsync tuvo éxito, las credenciales Oracle son válidas
                    // y se almacenan en sesión para que los servicios conecten como el usuario.
                    // Identity/SQLite solo gestiona la cookie de sesión web, nunca valida contraseñas.
                    var userIdentity = await _userManager.FindByNameAsync(usuario);

                    if (userIdentity == null)
                    {
                        userIdentity = new ApplicationUser
                        {
                            UserName = usuario,
                            Email = $"{usuario}@fabricahilos.com",
                            NombreCompleto = usuarioOracle.c_nombre ?? usuarioOracle.c_user,
                            Cargo = usuarioOracle.c_costo ?? "Usuario",
                            EmailConfirmed = true
                        };

                        // Los usuarios de Oracle se autentican contra Oracle, no necesitan
                        // contraseña en Identity. Se crea sin contraseña para evitar que
                        // las reglas de complejidad impidan guardar el usuario en la BD.
                        var createResult = await _userManager.CreateAsync(userIdentity);
                        if (createResult.Succeeded)
                            await _userManager.AddToRoleAsync(userIdentity, rolCorrecto);
                        else
                        {
                            _logger.LogWarning("No se pudo crear usuario Identity para {Usuario}: {Errores}",
                                usuario, string.Join(", ", createResult.Errors.Select(e => e.Description)));
                            ModelState.AddModelError(string.Empty, "Error al registrar el acceso. Por favor intente nuevamente.");
                            return View();
                        }
                    }
                    else
                    {
                        // Garantizar que UserName siempre coincida exactamente con el código
                        // Oracle ingresado. Corrige A_ADUSER/A_MDUSER que guardaban un valor
                        // incorrecto cuando el registro en SQLite tenía un UserName distinto.
                        bool needsUpdate = false;
                        if (!string.Equals(userIdentity.UserName, usuario, StringComparison.Ordinal))
                        {
                            _logger.LogInformation(
                                "Corrigiendo UserName Identity: '{Old}' → '{New}'",
                                userIdentity.UserName, usuario);
                            userIdentity.UserName = usuario;
                            needsUpdate = true;
                        }
                        var nombreOracle = usuarioOracle.c_nombre ?? usuarioOracle.c_user;
                        if (userIdentity.NombreCompleto != nombreOracle)
                        {
                            userIdentity.NombreCompleto = nombreOracle;
                            needsUpdate = true;
                        }
                        if (needsUpdate)
                            await _userManager.UpdateAsync(userIdentity);

                        // Corregir rol si no coincide con lo esperado (ej: usuario que tenía
                        // Admin por error y ahora debe ser Trabajador, o viceversa)
                        var rolesCheck      = await Task.WhenAll(
                            _userManager.IsInRoleAsync(userIdentity, "Admin"),
                            _userManager.IsInRoleAsync(userIdentity, "Trabajador"));
                        var tieneAdmin      = rolesCheck[0];
                        var tieneTrabajador = rolesCheck[1];

                        if (esAdmin && !tieneAdmin)
                        {
                            if (tieneTrabajador) await _userManager.RemoveFromRoleAsync(userIdentity, "Trabajador");
                            await _userManager.AddToRoleAsync(userIdentity, "Admin");
                        }
                        else if (!esAdmin && tieneAdmin)
                        {
                            await _userManager.RemoveFromRoleAsync(userIdentity, "Admin");
                            if (!tieneTrabajador) await _userManager.AddToRoleAsync(userIdentity, "Trabajador");
                        }
                        else if (!esAdmin && !tieneTrabajador)
                        {
                            await _userManager.AddToRoleAsync(userIdentity, "Trabajador");
                        }
                    }

                    // Validar que el usuario tenga al menos un módulo asignado en ACCESO_WEB
                    if (string.IsNullOrWhiteSpace(usuarioOracle.acceso_web))
                    {
                        _logger.LogWarning("Usuario '{Usuario}' no tiene módulo/área asignada en ACCESO_WEB.", usuario);
                        ModelState.AddModelError(string.Empty, "Su usuario no tiene ningún módulo o área asignada. Contacte al administrador del sistema.");
                        return View();
                    }

                    // isPersistent: true → cookie duradera, no se pierde al cerrar el navegador móvil
                    await _signInManager.SignInAsync(userIdentity, isPersistent: true);

                    // Guardar datos de sesión Oracle.
                    // EmpresaConexion indica qué ConnectionString deben usar los servicios.
                    HttpContext.Session.SetString("OracleUser",       usuario);
                    HttpContext.Session.SetString("OracleUserCodigo", usuarioOracle.c_codigo ?? string.Empty);
                    HttpContext.Session.SetString("AccesoWeb",        usuarioOracle.acceso_web ?? string.Empty);
                    HttpContext.Session.SetString("EmpresaConexion",  connKeyEmpresa);
                    HttpContext.Session.SetString("OraclePass",       password);

                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                        return Redirect(returnUrl);
                    return RedirectToLanding();
                }

                // Oracle es la única fuente de autenticación válida.
                // No se permite fallback a Identity local para evitar accesos con
                // contraseñas desactualizadas almacenadas en SQLite.
                _logger.LogWarning("Usuario '{Usuario}' no autenticado: no existe en Oracle o contraseña incorrecta.", usuario);
                ModelState.AddModelError(string.Empty, "Usuario o contraseña incorrectos.");
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado durante el inicio de sesión para {Usuario}", usuario);
                ModelState.AddModelError(string.Empty, "Ocurrió un error inesperado. Por favor intente nuevamente.");
                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Clear();
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login");
        }

        public IActionResult AccesoDenegado()
        {
            return View();
        }

        /// <summary>
        /// Redirige al landing apropiado según los permisos del usuario.
        /// El middleware se encarga de controlar el acceso por red.
        /// </summary>
        private IActionResult RedirectToLanding()
        {
            var (ctrl, act, area, url) = _menuService.GetLanding();
            if (url != null) return Redirect(url);
            return area != null
                ? RedirectToAction(act!, ctrl!, new { area })
                : RedirectToAction(act!, ctrl!);
        }
    }
}
