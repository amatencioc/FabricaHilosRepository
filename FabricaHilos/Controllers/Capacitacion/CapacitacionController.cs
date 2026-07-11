using FabricaHilos.Models.Capacitacion;
using FabricaHilos.Services;
using FabricaHilos.Services.Capacitacion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace FabricaHilos.Controllers.Capacitacion;

[Authorize]
[Route("RecursosHumanos/Capacitacion/[action]")]
public class CapacitacionController : OracleBaseController
{
    private readonly ICapacitacionService _svc;
    private readonly IMenuService         _menuService;
    private readonly ILogger<CapacitacionController> _log;

    public CapacitacionController(
        ICapacitacionService svc,
        IMenuService menuService,
        ILogger<CapacitacionController> log)
    {
        _svc         = svc;
        _menuService = menuService;
        _log         = log;
    }

    private string UsuarioActual => HttpContext.Session.GetString("OracleUser") ?? "";

    private bool EsAdmin => _menuService.GetMenusActuales().CapacitacionAdmin;

    // GET /RecursosHumanos/Capacitacion/MiPanel
    [HttpGet]
    public async Task<IActionResult> MiPanel()
    {
        try
        {
            var vm = await _svc.GetMiPanelAsync(UsuarioActual);
            vm.NombreUsuario = UsuarioActual;
            ViewBag.EsAdmin  = EsAdmin;
            return View("~/Views/RecursosHumanos/Capacitacion/MiPanel.cshtml", vm);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "[Capacitacion] Error al cargar MiPanel para usuario {Usuario}", UsuarioActual);
            TempData["Error"] = "Ocurrió un error al cargar el panel. Por favor intente nuevamente.";
            return RedirectToAction("Index", "Home");
        }
    }

    // GET /RecursosHumanos/Capacitacion/Catalogo
    [HttpGet]
    public async Task<IActionResult> Catalogo(
        int? categoria, string? busqueda, string? nivel,
        bool soloObligatorios = false, bool soloPendientes = false, int pagina = 1)
    {
        try
        {
            const int tamPag = 12;
            pagina = pagina < 1 ? 1 : pagina;

            var categoriasTask = _svc.GetCategoriasAsync();
            var cursosTask     = _svc.GetCatalogoAsync(
                UsuarioActual, categoria, busqueda, nivel, soloObligatorios, soloPendientes,
                pagina: pagina, tamPag: tamPag);
            var totalTask      = _svc.GetCatalogoTotalAsync(
                UsuarioActual, categoria, busqueda, nivel, soloObligatorios, soloPendientes);

            await Task.WhenAll(categoriasTask, cursosTask, totalTask);

            var vm = new CatalogoVm
            {
                Categorias       = categoriasTask.Result,
                Cursos           = cursosTask.Result,
                FiltroCategoria  = categoria,
                FiltroBusqueda   = busqueda,
                FiltroNivel      = nivel,
                SoloObligatorios = soloObligatorios,
                SoloPendientes   = soloPendientes,
                TotalCursos      = totalTask.Result,
                Pagina           = pagina,
                TamPag           = tamPag,
            };

            ViewBag.EsAdmin = EsAdmin;
            return View("~/Views/RecursosHumanos/Capacitacion/Catalogo.cshtml", vm);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "[Capacitacion] Error al cargar Catalogo para usuario {Usuario}", UsuarioActual);
            TempData["Error"] = "Ocurrió un error al cargar el catálogo. Por favor intente nuevamente.";
            return RedirectToAction(nameof(MiPanel));
        }
    }

    // POST /RecursosHumanos/Capacitacion/Inscribirse
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Inscribirse(int idCurso)
    {
        var (ok, msg, idInscripcion) = await _svc.InscribirseAsync(idCurso, UsuarioActual);
        if (!ok)
            return Json(new { ok = false, msg });

        // Obtener primer contenido del curso para redirigir al player
        var player = await _svc.GetPlayerAsync(idCurso, 0, UsuarioActual);
        long primerContenido = player?.Actual.IdContenido ?? 0;

        return Json(new { ok = true, msg, idCurso, primerContenido });
    }
}
