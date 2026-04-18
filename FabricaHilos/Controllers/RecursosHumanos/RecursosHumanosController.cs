using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FabricaHilos.Models.Sgc;
using FabricaHilos.Services;

namespace FabricaHilos.Controllers.RecursosHumanos
{
    [Authorize]
    public class RecursosHumanosController : Controller
    {
        private readonly IMenuService _menuService;

        public RecursosHumanosController(IMenuService menuService)
        {
            _menuService = menuService;
        }

        public IActionResult Index()
        {
            var menus = _menuService.GetMenusActuales();
            var modulos = new List<SgcModuloDto>();

            // Aquarius (módulo padre con sub-módulos)
            var moduloAquarius = new SgcModuloDto
            {
                Nombre      = "Aquarius",
                Descripcion = "Sistema de control de asistencia y gestión del personal.",
                Icono       = "bi-people-fill",
                ColorClase  = "text-primary"
            };

            if (menus.RhMarcaciones)
            {
                moduloAquarius.SubModulos.Add(new SgcSubModuloDto
                {
                    Nombre      = "Marcaciones",
                    Descripcion = "Control de asistencia y marcaciones del personal por empresa y período.",
                    Icono       = "bi-clock-history",
                    Controller  = "Marcaciones",
                    Action      = "Index"
                });
            }

            if (moduloAquarius.SubModulos.Any())
                modulos.Add(moduloAquarius);

            return View("~/Views/RecursosHumanos/Index.cshtml", modulos);
        }
    }
}
