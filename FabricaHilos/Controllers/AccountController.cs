using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
                var loginOracle = new Login(_configuration, null);
                var usuarioOracle = loginOracle.EncontrarUsuario(usuario, password);

                if (!string.IsNullOrEmpty(usuarioOracle.c_user))
                {
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
                            NombreCompleto = usuarioOracle.c_user,
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
                        if (userIdentity.NombreCompleto != usuarioOracle.c_user)
                        {
                            userIdentity.NombreCompleto = usuarioOracle.c_user;
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

                    // Guardar credenciales Oracle del usuario en sesión para que
                    // los servicios conecten a Oracle con el usuario propio.
                    HttpContext.Session.SetString("OracleUser", usuario);
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

        [HttpGet]
        [Authorize(Roles = "Admin,Gerencia")]
        public async Task<IActionResult> Register()
        {
            ViewBag.Roles = await _roleManager.Roles.ToListAsync();
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Gerencia")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(string nombreCompleto, string email, string password, string rol)
        {
            if (string.IsNullOrEmpty(nombreCompleto) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError(string.Empty, "Todos los campos son obligatorios.");
                ViewBag.Roles = await _roleManager.Roles.ToListAsync();
                return View();
            }

            var usuario = new ApplicationUser
            {
                UserName = email,
                Email = email,
                NombreCompleto = nombreCompleto,
                EmailConfirmed = true
            };

            var resultado = await _userManager.CreateAsync(usuario, password);
            if (resultado.Succeeded)
            {
                if (!string.IsNullOrEmpty(rol) && await _roleManager.RoleExistsAsync(rol))
                    await _userManager.AddToRoleAsync(usuario, rol);

                TempData["Success"] = $"Usuario {email} creado exitosamente.";
                return RedirectToAction("Index", "Home");
            }

            foreach (var error in resultado.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            ViewBag.Roles = await _roleManager.Roles.ToListAsync();
            return View();
        }

        public IActionResult AccesoDenegado()
        {
            return View();
        }
    }
}
