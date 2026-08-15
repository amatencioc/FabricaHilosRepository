using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FabricaHilos.Models.Sgc;
using FabricaHilos.Services;

namespace FabricaHilos.Controllers.RecursosHumanos
{
    [Authorize]
    [Route("RecursosHumanos")]
    public class RecursosHumanosController : Controller
    {
        private readonly IMenuService _menuService;

        public RecursosHumanosController(IMenuService menuService)
        {
            _menuService = menuService;
        }

        [HttpGet("")]
        [HttpGet("Index")]
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

            if (menus.RhCompensacionDiaDia)
            {
                moduloAquarius.SubModulos.Add(new SgcSubModuloDto
                {
                    Nombre      = "Compensación Día por Día",
                    Descripcion = "Registra tiempo de días de descanso para compensar ausencias, tardanzas o faltas.",
                    Icono       = "bi-calendar2-check",
                    Controller  = "CompensacionDiaDia",
                    Action      = "Index"
                });
            }

            if (menus.RhCompensacionDdc)
            {
                moduloAquarius.SubModulos.Add(new SgcSubModuloDto
                {
                    Nombre      = "Día Libre por Compensar (DDC)",
                    Descripcion = "Compensa días DDC de empleados con horario rotativo usando sus HE simples del rango.",
                    Icono       = "bi-calendar2-x",
                    Controller  = "CompensacionDdc",
                    Action      = "Index"
                });
            }

            if (menus.RhAutorizacionHoras)
            {
                moduloAquarius.SubModulos.Add(new SgcSubModuloDto
                {
                    Nombre      = "Autorización de Horas",
                    Descripcion = "Portal de autorización de horas extras para supervisores.",
                    Icono       = "bi-pencil-square",
                    Controller  = "AuthHoras",
                    Action      = "Index"
                });
            }

            if (menus.RhPlanillaMensual)
            {
                moduloAquarius.SubModulos.Add(new SgcSubModuloDto
                {
                    Nombre      = "Planilla Mensual",
                    Descripcion = "Consulta y análisis de la planilla mensual por empresa, sucursal y centro de costos.",
                    Icono       = "bi-file-earmark-spreadsheet",
                    Controller  = "PlanillaMensual",
                    Action      = "Resumen"
                });
            }

            if (moduloAquarius.SubModulos.Any())
                modulos.Add(moduloAquarius);

            // Buscar Empleado (módulo independiente, usado por varias áreas)
            var moduloFindEmpleado = new SgcModuloDto
            {
                Nombre      = "Buscar Empleado",
                Descripcion = "Consulta rápida del estado actual de un empleado: asistencia, vigilancia y eventos vigentes.",
                Icono       = "bi-person-badge",
                ColorClase  = "text-info"
            };

            if (menus.RhFindEmpleado)
            {
                moduloFindEmpleado.SubModulos.Add(new SgcSubModuloDto
                {
                    Nombre      = "Buscar Empleado",
                    Descripcion = "Ingrese código, DNI o nombre para ver el estado actual del empleado en Aquarius y SIG.",
                    Icono       = "bi-person-badge",
                    Controller  = "FindEmpleado",
                    Action      = "Index"
                });
            }

            if (menus.RhProyeccionAsistencia)
            {
                moduloFindEmpleado.SubModulos.Add(new SgcSubModuloDto
                {
                    Nombre      = "Proyección de Asistencia",
                    Descripcion = "¿Cuántos y qué empleados vendrían a trabajar en una fecha específica? Según horario/turno vigente y eventos activos.",
                    Icono       = "bi-calendar-week",
                    Controller  = "ProyeccionAsistencia",
                    Action      = "Index"
                });
            }

            if (moduloFindEmpleado.SubModulos.Any())
                modulos.Add(moduloFindEmpleado);

            // Indicadores
            var moduloIndicadores = new SgcModuloDto
            {
                Nombre      = "Indicadores",
                Descripcion = "KPIs de Gestión de Personal — Sobretiempo, concentración de HE y masa salarial por área.",
                Icono       = "bi-graph-up-arrow",
                ColorClase  = "text-warning"
            };

            if (menus.RhIndicadoresHorasExtras)
            {
                moduloIndicadores.SubModulos.Add(new SgcSubModuloDto
                {
                    Nombre      = "KPI Sobretiempo por Área",
                    Descripcion = "Mide el monto total de sobretiempo pagado por área, el promedio por trabajador y la participación de cada área en el costo total de la empresa.",
                    Icono       = "bi-clock-fill",
                    Controller  = "HorasExtras",
                    Action      = "Index"
                });
            }

            if (menus.RhIndicadoresCostoSalarialHorasExtras)
            {
                moduloIndicadores.SubModulos.Add(new SgcSubModuloDto
                {
                    Nombre      = "Costo Salarial Horas Extraordinarias",
                    Descripcion = "Análisis detallado del costo de horas extraordinarias por área, categoría de costo y período.",
                    Icono       = "bi-coin",
                    Controller  = "CostoSalarialHorasExtras",
                    Action      = "Index"
                });
            }

            if (menus.RhIndicadoresComparativoCostoLaboral)
            {
                moduloIndicadores.SubModulos.Add(new SgcSubModuloDto
                {
                    Nombre      = "Comparativo Costo Laboral (Áño vs Áño)",
                    Descripcion = "Compara N° de trabajadores y costo laboral total (básico + cargas sociales por Ley, factor 1.4232) entre dos años, con desglose de beneficios sociales por área.",
                    Icono       = "bi-bar-chart-line-fill",
                    Controller  = "ComparativoCostoLaboral",
                    Action      = "Index"
                });
            }

            if (menus.RhIndicadoresEventosSobretiempo)
            {
                moduloIndicadores.SubModulos.Add(new SgcSubModuloDto
                {
                    Nombre      = "Eventos vs Sobretiempo por Área",
                    Descripcion = "Correlaciona ausentismo/eventos del personal con el sobretiempo generado por área y mes.",
                    Icono       = "bi-calendar2-x",
                    Controller  = "EventosSobretiempo",
                    Action      = "Index"
                });
            }

            if (moduloIndicadores.SubModulos.Any())
                modulos.Add(moduloIndicadores);

            // Reporte de Planilla
            var moduloReportePlanilla = new SgcModuloDto
            {
                Nombre      = "Reporte de Planilla",
                Descripcion = "Reportes detallados de planilla: ingresos, descuentos y aportes por periodo.",
                Icono       = "bi-file-earmark-spreadsheet",
                ColorClase  = "text-info"
            };

            if (menus.RhReportePlanillaIngDsctoAportes)
            {
                moduloReportePlanilla.SubModulos.Add(new SgcSubModuloDto
                {
                    Nombre      = "Ingreso y Descuento de Aportes",
                    Descripcion = "Detalle de ingresos, descuentos y aportes de una planilla semanal (Año y Semana).",
                    Icono       = "bi-file-earmark-spreadsheet",
                    Controller  = "PlanillaIngDsctoAportes",
                    Action      = "Index"
                });
            }

            if (moduloReportePlanilla.SubModulos.Any())
                modulos.Add(moduloReportePlanilla);

            // Capacitación
            var moduloCapacitacion = new SgcModuloDto
            {
                Nombre      = "Capacitación",
                Descripcion = "Plataforma de aprendizaje en línea: cursos, exámenes y certificados.",
                Icono       = "bi-mortarboard-fill",
                ColorClase  = "text-success"
            };

            if (menus.CapacitacionMisCursos)
                moduloCapacitacion.SubModulos.Add(new SgcSubModuloDto
                {
                    Nombre      = "Mi Panel",
                    Descripcion = "Mis cursos activos, progreso y certificados obtenidos.",
                    Icono       = "bi-mortarboard",
                    Controller  = "Capacitacion",
                    Action      = "MiPanel"
                });

            if (menus.CapacitacionCatalogo)
                moduloCapacitacion.SubModulos.Add(new SgcSubModuloDto
                {
                    Nombre      = "Catálogo",
                    Descripcion = "Explora y accede a todos los cursos disponibles.",
                    Icono       = "bi-grid",
                    Controller  = "Capacitacion",
                    Action      = "Catalogo"
                });

            if (menus.CapacitacionAdmin)
                moduloCapacitacion.SubModulos.Add(new SgcSubModuloDto
                {
                    Nombre      = "Administración",
                    Descripcion = "Gestión de cursos, inscripciones y reportes.",
                    Icono       = "bi-gear-fill",
                    Controller  = "CapacitacionAdmin",
                    Action      = "Index"
                });

            if (moduloCapacitacion.SubModulos.Any())
                modulos.Add(moduloCapacitacion);

            return View("~/Views/RecursosHumanos/Index.cshtml", modulos);
        }
    }
}
