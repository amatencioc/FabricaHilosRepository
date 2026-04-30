using FabricaHilos.Models.Sgc;
using FabricaHilos.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FabricaHilos.Controllers.Sistemas;

[Authorize]
[Route("Sistemas")]
public class SistemasController : Controller
{
    private readonly IMenuService _menuService;

    public SistemasController(IMenuService menuService)
    {
        _menuService = menuService;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public IActionResult Index()
    {
        var menus   = _menuService.GetMenusActuales();
        var modulos = new List<SgcModuloDto>();

        var moduloIndicadores = new SgcModuloDto
        {
            Nombre      = "Indicadores",
            Descripcion = "KPIs del área de Sistemas — seguimiento de requerimientos de desarrollo.",
            Icono       = "bi-graph-up-arrow",
            ColorClase  = "text-info"
        };

        if (menus.SistemasIndicadoresDesarrollo)
        {
            moduloIndicadores.SubModulos.Add(new SgcSubModuloDto
            {
                Nombre      = "KPI Desarrollo",
                Descripcion = "Requerimientos pendientes y entregados por área y año. Tasa de atención mensual.",
                Icono       = "bi-kanban-fill",
                Controller  = "Desarrollo",
                Action      = "Index"
            });
        }

        if (moduloIndicadores.SubModulos.Any())
            modulos.Add(moduloIndicadores);

        return View("~/Views/Sistemas/Index.cshtml", modulos);
    }
}
