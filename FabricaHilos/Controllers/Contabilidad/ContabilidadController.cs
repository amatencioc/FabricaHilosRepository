using FabricaHilos.Models.Sgc;
using FabricaHilos.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FabricaHilos.Controllers.Contabilidad;

[Authorize]
public class ContabilidadController : OracleBaseController
{
    private readonly IMenuService _menuService;

    public ContabilidadController(IMenuService menuService)
    {
        _menuService = menuService;
    }

    public IActionResult Index()
    {
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

        return View(tarjetas);
    }
}
