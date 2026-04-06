using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FabricaHilos.Models.Sgc;
using FabricaHilos.Services;

namespace FabricaHilos.Controllers
{
    [Authorize]
    public class ProduccionController : Controller
    {
        private readonly IMenuService _menuService;

        public ProduccionController(IMenuService menuService)
        {
            _menuService = menuService;
        }

        public IActionResult Index()
        {
            var menus = _menuService.GetMenusActuales();
            var modulos = new List<SgcModuloDto>();

            if (menus.ProduccionRegistroPreparatoria)
            {
                modulos.Add(new SgcModuloDto
                {
                    Nombre = "Registro de Preparatoria",
                    Descripcion = "Gestión de órdenes de producción, seguimiento de procesos y control de cantidades.",
                    Icono = "bi-clipboard-data",
                    ColorClase = "text-primary",
                    Controller = "RegistroPreparatoria",
                    Action = "Index"
                });
            }

            if (menus.ProduccionAutoconer)
            {
                var autoconerModulo = new SgcModuloDto
                {
                    Nombre = "Control Autoconer",
                    Descripcion = "Control de producción de máquinas Autoconer, registro de tramos, destinos y reprocesos.",
                    Icono = "bi-speedometer2",
                    ColorClase = "text-success",
                    Controller = "Autoconer",
                    Action = "Index"
                };

                // Agregar submenús si están habilitados
                if (menus.ProduccionAutoconerPorPartida)
                {
                    autoconerModulo.SubModulos.Add(new SgcSubModuloDto
                    {
                        Nombre = "Por Partida",
                        Descripcion = "Registro individual por partida",
                        Icono = "bi-folder",
                        Controller = "Autoconer",
                        Action = "Index"
                    });
                }

                if (menus.ProduccionAutoconerPorCanillas)
                {
                    autoconerModulo.SubModulos.Add(new SgcSubModuloDto
                    {
                        Nombre = "Por Canillas",
                        Descripcion = "Registro agrupado por canillas",
                        Icono = "bi-list-ul",
                        Controller = "Autoconer",
                        Action = "PorCanillas"
                    });
                }

                modulos.Add(autoconerModulo);
            }

            return View(modulos);
        }
    }
}
