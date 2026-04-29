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

            if (moduloAquarius.SubModulos.Any())
                modulos.Add(moduloAquarius);

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

            if (menus.RhIndicadoresConcentracionSobretiempo)
            {
                moduloIndicadores.SubModulos.Add(new SgcSubModuloDto
                {
                    Nombre      = "KPI Concentración de Sobretiempo",
                    Descripcion = "Mide qué proporción de trabajadores generó HE en el período — identifica si la carga está distribuida o concentrada en pocos colaboradores.",
                    Icono       = "bi-people-fill",
                    Controller  = "ConcentracionSobretiempoArea",
                    Action      = "Index"
                });
            }

            if (menus.RhIndicadoresEvolucionMasaSalarial)
            {
                moduloIndicadores.SubModulos.Add(new SgcSubModuloDto
                {
                    Nombre      = "KPI Masa Salarial y Sobretiempo",
                    Descripcion = "Evolución mensual del gasto en remuneraciones por área, con ratio sobretiempo/masa y variación vs mes anterior.",
                    Icono       = "bi-currency-dollar",
                    Controller  = "EvolucionMasaSalarial",
                    Action      = "Index"
                });
            }

            if (moduloIndicadores.SubModulos.Any())
                modulos.Add(moduloIndicadores);

            return View("~/Views/RecursosHumanos/Index.cshtml", modulos);
        }
    }
}
