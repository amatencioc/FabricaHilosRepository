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

        if (menus.SistemasIndicadoresDesarrolloComplejidad)
        {
            moduloIndicadores.SubModulos.Add(new SgcSubModuloDto
            {
                Nombre      = "KPI Desarrollo - Complejidad",
                Descripcion = "Requerimientos ponderados por nivel BAJA / MEDIA / ALTA. Distribución y avance por complejidad.",
                Icono       = "bi-bar-chart-steps",
                Controller  = "DesarrolloComplejidad",
                Action      = "Index"
            });
        }

        if (menus.SistemasIndicadoresIncidencia)
        {
            moduloIndicadores.SubModulos.Add(new SgcSubModuloDto
            {
                Nombre      = "KPI Incidencias",
                Descripcion = "Incidencias pendientes y resueltas por área y año. Promedio de minutos de atención mensual.",
                Icono       = "bi-bell-fill",
                Controller  = "Incidencia",
                Action      = "Index"
            });
        }

        if (menus.SistemasIndicadoresSeguimientoDev)
        {
            moduloIndicadores.SubModulos.Add(new SgcSubModuloDto
            {
                Nombre      = "Seguimiento Dev",
                Descripcion = "Requerimientos entregados por responsable y área. Seguimiento mensual del equipo de desarrollo.",
                Icono       = "bi-clipboard2-check-fill",
                Controller  = "SeguimientoDev",
                Action      = "Index"
            });
        }

        if (moduloIndicadores.SubModulos.Any())
            modulos.Add(moduloIndicadores);

        var moduloReq = new SgcModuloDto
        {
            Nombre      = "Requerimientos",
            Descripcion = "Herramientas de gestión de documentos y requerimientos del área de Sistemas.",
            Icono       = "bi-file-earmark-text",
            ColorClase  = "text-warning"
        };

        if (menus.SistemasRequerimientosAnularDocumento)
        {
            moduloReq.SubModulos.Add(new SgcSubModuloDto
            {
                Nombre      = "Anular Documento",
                Descripcion = "Consulta y verificación de documentos (Boleta/Factura) para proceso de anulación.",
                Icono       = "bi-file-earmark-x-fill",
                Controller  = "AnularDocumento",
                Action      = "Index"
            });
        }

        if (moduloReq.SubModulos.Any())
            modulos.Add(moduloReq);

        return View("~/Views/Sistemas/Index.cshtml", modulos);
    }
}
