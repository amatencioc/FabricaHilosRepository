using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using FabricaHilos.Models;
using FabricaHilos.Logica;
using FabricaHilos.Services;

namespace FabricaHilos.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<AccountController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IMenuService _menuService;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<AccountController> logger,
            IConfiguration configuration,
            IMenuService menuService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _logger = logger;
            _configuration = configuration;
            _menuService = menuService;
        }

        [HttpGet]
        public async Task<IActionResult> Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                // Si la sesión Oracle también está activa, redirigir a la app directamente
                if (!string.IsNullOrEmpty(HttpContext.Session.GetString("OracleUser")))
                {
                    var (ctrl, act, area) = _menuService.GetLanding();
                    return area != null 
                        ? RedirectToAction(act, ctrl, new { area }) 
                        : RedirectToAction(act, ctrl);
                }

                // Cookie web válida pero sesión Oracle expirada (ej: reinicio de la app)
                // → cerrar sesión web y redirigir a login limpio para evitar HTTP 400
                // (si caemos directo a View() el token anti-CSRF queda inválido)
                await _signInManager.SignOutAsync();
                HttpContext.Session.Clear();
                return RedirectToAction("Login", new { returnUrl });
            }
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string usuario, string password, bool recordarme, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (string.IsNullOrEmpty(usuario) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError(string.Empty, "Por favor ingrese el usuario y contraseña.");
                return View();
            }

            try
            {
                // 1. Validar contra Oracle Database
                var loginOracle = new Login(_configuration, _logger);
                var usuarioOracle = loginOracle.EncontrarUsuario(usuario, password);

                if (!string.IsNullOrEmpty(usuarioOracle.c_user))
                {
                    // Validar que las credenciales funcionen como login Oracle real.
                    // Si la cuenta Oracle del usuario tiene una contraseña distinta a
                    // PSW_SIG (o no existe como usuario Oracle), se permite el login
                    // pero los servicios usarán la conexión base del appsettings.
                    bool oracleCredencialesValidas = false;
                    var baseConnStr = _configuration.GetConnectionString("OracleConnection") ?? string.Empty;
                    var csb = new OracleConnectionStringBuilder(baseConnStr)
                    {
                        UserID = usuario,
                        Password = password
                    };
                    try
                    {
                        using var testConn = new OracleConnection(csb.ToString());
                        await testConn.OpenAsync();
                        oracleCredencialesValidas = true;
                    }
                    catch (OracleException oex) when (oex.Number == 1017 || oex.Number == 1004)
                    {
                        _logger.LogWarning(
                            "Usuario {Usuario} existe en CS_USER pero sus credenciales no son válidas como login Oracle (ORA-{Codigo}). Se usará la conexión base.",
                            usuario, oex.Number);
                    }

                    var adminUsers = _configuration.GetSection("AdminUsers").Get<string[]>()
                                    ?? [];
                    var esAdmin = adminUsers.Contains(usuario, StringComparer.OrdinalIgnoreCase);
                    var rolCorrecto = esAdmin ? "Admin" : "Trabajador";

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
                        var tieneAdmin      = await _userManager.IsInRoleAsync(userIdentity, "Admin");
                        var tieneTrabajador = await _userManager.IsInRoleAsync(userIdentity, "Trabajador");

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

                    await _signInManager.SignInAsync(userIdentity, recordarme);

                    // Siempre guardar el usuario Oracle en sesión (para auditoría).
                    // Solo guardar la contraseña si las credenciales Oracle son válidas;
                    // así GetOracleConnectionString() usará la conexión base cuando no
                    // exista OraclePass en sesión (fallback seguro).
                    HttpContext.Session.SetString("OracleUser", usuario);
                    if (oracleCredencialesValidas)
                        HttpContext.Session.SetString("OraclePass", password);

                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                        return Redirect(returnUrl);
                    var (ctrl, act, area) = _menuService.GetLanding();
                    return area != null 
                        ? RedirectToAction(act, ctrl, new { area }) 
                        : RedirectToAction(act, ctrl);
                }

                // 2. Validar contra Identity local
                var resultado = await _signInManager.PasswordSignInAsync(usuario, password, recordarme, lockoutOnFailure: true);
                if (resultado.Succeeded)
                {
                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                        return Redirect(returnUrl);
                    var (ctrl, act, area) = _menuService.GetLanding();
                    return area != null 
                        ? RedirectToAction(act, ctrl, new { area }) 
                        : RedirectToAction(act, ctrl);
                }

                if (resultado.IsLockedOut)
                {
                    _logger.LogWarning("Cuenta bloqueada tras múltiples intentos fallidos: {Usuario}", usuario);
                    ModelState.AddModelError(string.Empty, "Cuenta bloqueada temporalmente por múltiples intentos fallidos. Intente nuevamente en 10 minutos.");
                    return View();
                }

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
    }
}
