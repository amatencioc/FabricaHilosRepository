using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FabricaHilos.Models.Sgc;
using FabricaHilos.Services;

namespace FabricaHilos.Controllers.Ventas
{
    [Authorize]
    public class VentasController : Controller
    {
        private readonly IMenuService _menuService;

        public VentasController(IMenuService menuService)
        {
            _menuService = menuService;
        }

        public IActionResult Index()
        {
            var menus = _menuService.GetMenusActuales();
            var modulos = new List<SgcModuloDto>();

            if (menus.VentasConsultaTC)
            {
                modulos.Add(new SgcModuloDto
                {
                    Nombre = "Consulta TC",
                    Descripcion = "Gestión y consulta de requerimientos de certificados de origen.",
                    Icono = "bi-file-earmark-text",
                    ColorClase = "text-primary",
                    Controller = "ConsultaTc",
                    Action = "Index"
                });
            }

            if (menus.VentasIndicadoresComerciales)
            {
                modulos.Add(new SgcModuloDto
                {
                    Nombre = "Indicadores Comerciales",
                    Descripcion = "Dashboard de indicadores: importe, KG y clientes por asesor y mes.",
                    Icono = "bi-bar-chart-line",
                    ColorClase = "text-success",
                    Controller = "IndicadoresComerciales",
                    Action = "Index"
                });
            }

            if (menus.VentasVentasPorMercado)
            {
                modulos.Add(new SgcModuloDto
                {
                    Nombre = "Ventas por Mercado",
                    Descripcion = "Distribución de ventas por mercado geográfico: Perú, LATAM y Global.",
                    Icono = "bi-globe-americas",
                    ColorClase = "text-info",
                    Controller = "VentasPorMercado",
                    Action = "Index"
                });
            }

            if (menus.VentasDashboardComercial)
            {
                modulos.Add(new SgcModuloDto
                {
                    Nombre = "Dashboard Comercial",
                    Descripcion = "Seguimiento de ventas y metas comerciales por asesor y período.",
                    Icono = "bi-graph-up-arrow",
                    ColorClase = "text-warning",
                    Controller = "DashboardComercial",
                    Action = "Index"
                });
            }

            if (menus.VentasDashboardGerencial)
            {
                modulos.Add(new SgcModuloDto
                {
                    Nombre = "Dashboard Gerencial",
                    Descripcion = "Visión ejecutiva consolidada de ventas, márgenes y tendencias.",
                    Icono = "bi-speedometer2",
                    ColorClase = "text-danger",
                    Controller = "DashboardGerencial",
                    Action = "Index"
                });
            }

            return View(modulos);
        }
    }
}
