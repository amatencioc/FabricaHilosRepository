using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FabricaHilos.Models.Sgc;
using FabricaHilos.Services;
using FabricaHilos.Filters;

namespace FabricaHilos.Controllers.Seguridad
{
    [Authorize]
    [AccesoExternoPermitido]
    public class SeguridadController : Controller
    {
        private readonly IMenuService _menuService;

        public SeguridadController(IMenuService menuService)
        {
            _menuService = menuService;
        }

        public IActionResult Index()
        {
            var menus = _menuService.GetMenusActuales();
            var modulos = new List<SgcModuloDto>();

            if (menus.SeguridadInspecciones)
            {
                modulos.Add(new SgcModuloDto
                {
                    Nombre = "Inspecciones",
                    Descripcion = "Registro y seguimiento de inspecciones de seguridad, hallazgos y acciones correctivas.",
                    Icono = "bi-clipboard-check",
                    ColorClase = "text-warning",
                    Controller = "Inspeccion",
                    Action = "Index"
                });
            }

            return View("~/Views/Seguridad/Index.cshtml", modulos);
        }
    }
}
