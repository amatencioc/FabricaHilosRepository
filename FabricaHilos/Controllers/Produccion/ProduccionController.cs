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
                        Nombre = "Seguimiento de Pedidos",
                        Descripcion = "Tablero en tiempo real de todos los pedidos activos por etapa de producción.",
                        Icono = "bi-kanban",
                        Controller = "Planeamiento",
                        Action = "Dashboard"
                    });
                }

                if (menus.PlaneamientoSeguimientoTintoreria)
                {
                    planeamientoModulo.SubModulos.Add(new SgcSubModuloDto
                    {
                        Nombre = "Seg. Programación Tintorería",
                        Descripcion = "Reporte de producción de tintorería por fecha de entrega, programa, teñido, pedido o aprobación.",
                        Icono = "bi-table",
                        Controller = "Planeamiento",
                        Action = "SeguimientoTintoreria"
                    });
                }

                if (menus.PlaneamientoProximosVencer)
                {
                    planeamientoModulo.SubModulos.Add(new SgcSubModuloDto
                    {
                        Nombre = "Próximos a Vencer",
                        Descripcion = "Ítems activos cuya fecha de entrega comprometida se aproxima.",
                        Icono = "bi-calendar-event-fill",
                        Controller = "Planeamiento",
                        Action = "ProximosVencer"
                    });
                }

                if (menus.PlaneamientoPendTenido)
                {
                    planeamientoModulo.SubModulos.Add(new SgcSubModuloDto
                    {
                        Nombre = "Pendientes de Teñido",
                        Descripcion = "Partidas programadas o con previo (receta IR) aún sin producción activa de teñido. Responsables: Fredy / Malena.",
                        Icono = "bi-droplet-half",
                        Controller = "Planeamiento",
                        Action = "PendientesTenido"
                    });
                }

                if (menus.PlaneamientoPendEvalCalidad)
                {
                    planeamientoModulo.SubModulos.Add(new SgcSubModuloDto
                    {
                        Nombre = "Pendientes Eval. Calidad",
                        Descripcion = "Partidas secadas sin evaluación de calidad tintorería registrada. Responsable: Ivon.",
                        Icono = "bi-patch-check",
                        Controller = "Planeamiento",
                        Action = "PendientesEvalCalidad"
                    });
                }
                if (menus.PlaneamientoPendPartidasDef)
                {
                    planeamientoModulo.SubModulos.Add(new SgcSubModuloDto
                    {
                        Nombre = "Partidas por Definir",
                        Descripcion = "Partidas con evaluación de calidad pendiente de definición (resultado no aprobado). Responsable: Karen.",
                        Icono = "bi-question-circle",
                        Controller = "Planeamiento",
                        Action = "PartidasPorDefinir"
                    });
                }
                if (menus.PlaneamientoPendEnconado)
                {
                    planeamientoModulo.SubModulos.Add(new SgcSubModuloDto
                    {
                        Nombre = "Pendientes de Enconado",
                        Descripcion = "Partidas aprobadas en CC pendientes de enconado o devanado (Tintorería + Hilandería). Responsable: Guevara.",
                        Icono = "bi-arrow-repeat",
                        Controller = "Planeamiento",
                        Action = "PendientesEnconado"
                    });
                }

                if (menus.PlaneamientoPendRevisado)
                {
                    planeamientoModulo.SubModulos.Add(new SgcSubModuloDto
                    {
                        Nombre = "Pendientes de Revisado",
                        Descripcion = "Partidas en programa estado 6 (revisado) sin revisado aprobado. Responsable: Martín.",
                        Icono = "bi-clipboard2-check",
                        Controller = "Planeamiento",
                        Action = "PendientesRevisado"
                    });
                }

                if (menus.PlaneamientoPendientesDespacho)
                {
                    planeamientoModulo.SubModulos.Add(new SgcSubModuloDto
                    {
                        Nombre = "Pendientes de Despacho",
                        Descripcion = "Ítems listos en almacén PT pendientes de ser despachados al cliente.",
                        Icono = "bi-truck",
                        Controller = "Planeamiento",
                        Action = "PendientesDespacho"
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
                        Descripcion = "Bandeja de alertas activas: retrasos, reprocesos y sobrecargas.",
                        Icono = "bi-bell-fill",
                        Controller = "Planeamiento",
                        Action = "Alertas"
                    });
                }



                modulos.Add(planeamientoModulo);
            }

            // Parámetros PLN — visible siempre pero deshabilitado (acceso solo por ruta directa)
            if (menus.Planeamiento)
            {
                var plnParams = modulos.FirstOrDefault(m => m.Controller == "Planeamiento");
                plnParams?.SubModulos.Add(new SgcSubModuloDto
                {
                    Nombre        = "Parámetros",
                    Descripcion   = "Configuración de umbrales, horas de turno y buffers del módulo PLN_.",
                    Icono         = "bi-sliders",
                    Controller    = "Planeamiento",
                    Action        = "Parametros",
                    Deshabilitado = true
                });
            }

            return View(modulos);
        }
    }
}
