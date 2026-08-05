using FabricaHilos.Models.Sgc;
using FabricaHilos.Services;
using FabricaHilos.Services.Sire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FabricaHilos.Controllers.Contabilidad;

[Authorize]
public class ContabilidadController : OracleBaseController
{
    private readonly IMenuService _menuService;
    private readonly ILazySireInitializer _lazySireInitializer;
    private readonly ILogger<ContabilidadController> _logger;

    public ContabilidadController(
        IMenuService menuService,
        ILazySireInitializer lazySireInitializer,
        ILogger<ContabilidadController> logger)
    {
        _menuService = menuService;
        _lazySireInitializer = lazySireInitializer;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        // Iniciar lazy loading de servicios SIRE cuando se accede a Contabilidad
        if (!_lazySireInitializer.IsInitialized)
        {
            try
            {
                _logger.LogInformation("[CONTABILIDAD] Iniciando servicios SIRE...");
                await _lazySireInitializer.InitializeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CONTABILIDAD] Error al inicializar servicios SIRE");
                TempData["Error"] = "⚠️ SUNAT no responde en este momento (falla externa, no del sistema). Podrá seguir trabajando con datos ya descargados dentro de SIRE.";
            }
        }

        var menus = _menuService.GetMenusActuales();

        var tarjetas = new List<SgcModuloDto>();

        if (menus.ContabilidadSire)
            tarjetas.Add(new SgcModuloDto
            {
                Nombre      = "SIRE",
                Descripcion = "Registro de Ventas e Ingresos / Registro de Compras Electrónico",
                Icono       = "bi-journal-text",
                ColorClase  = "text-purple",
                Controller  = "Sire",
                Action      = "Index"
            });

        if (menus.ContabilidadActivoFijo)
            tarjetas.Add(new SgcModuloDto
            {
                Nombre      = "Activos Fijos",
                Descripcion = "Gestión, edición y ficha de activos fijos de la empresa",
                Icono       = "bi-buildings",
                ColorClase  = "text-success",
                Controller  = "ActivoFijo",
                Action      = "Index"
            });

        return View(tarjetas);
    }
}
