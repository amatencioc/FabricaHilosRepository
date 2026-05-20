using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FabricaHilos.Models.Sgc;
using FabricaHilos.Services;

namespace FabricaHilos.Controllers.Produccion
{
    [Authorize]
    public class ProduccionController : OracleBaseController
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

            if (menus.Planeamiento)
            {
                var planeamientoModulo = new SgcModuloDto
                {
                    Nombre = "Planeamiento",
                    Descripcion = "Seguimiento de pedidos, carga de máquinas, alertas y KPIs de producción.",
                    Icono = "bi-kanban",
                    ColorClase = "text-info",
                    Controller = "Planeamiento",
                    Action = "Index"
                };

                if (menus.PlaneamientoDashboard)
                {
                    planeamientoModulo.SubModulos.Add(new SgcSubModuloDto
                    {
                        Nombre = "Dashboard",
                        Descripcion = "Vista general de pedidos activos",
                        Icono = "bi-speedometer2",
                        Controller = "Planeamiento",
                        Action = "Dashboard"
                    });
                }

                if (menus.PlaneamientoCargaMaquinas)
                {
                    planeamientoModulo.SubModulos.Add(new SgcSubModuloDto
                    {
                        Nombre = "Carga de Máquinas",
                        Descripcion = "Planificación y carga por máquina",
                        Icono = "bi-gear-wide-connected",
                        Controller = "Planeamiento",
                        Action = "CargaMaquinas"
                    });
                }

                if (menus.PlaneamientoAlertas)
                {
                    planeamientoModulo.SubModulos.Add(new SgcSubModuloDto
                    {
                        Nombre = "Alertas",
                        Descripcion = "Alertas activas por pedido e ítem",
                        Icono = "bi-bell",
                        Controller = "Planeamiento",
                        Action = "Alertas"
                    });
                }

                if (menus.PlaneamientoKPIs)
                {
                    planeamientoModulo.SubModulos.Add(new SgcSubModuloDto
                    {
                        Nombre = "KPIs",
                        Descripcion = "Indicadores de cumplimiento y desempeño",
                        Icono = "bi-graph-up-arrow",
                        Controller = "Planeamiento",
                        Action = "KPIs"
                    });
                }

                modulos.Add(planeamientoModulo);
            }

            return View(modulos);
        }
    }
}
