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

            if (menus.VentasIndicadorComercialMaestro)
            {
                modulos.Add(new SgcModuloDto
                {
                    Nombre = "Indicador Comercial Maestro",
                    Descripcion = "Análisis de importe y KG por asesor con gráficos de barras y cuadros comparativos mensuales.",
                    Icono = "bi-graph-up",
                    ColorClase = "text-success",
                    Controller = "IndicadorComercialMaestro",
                    Action = "Index"
                });
            }

            if (menus.VentasDashboardComercialMaestro)
            {
                modulos.Add(new SgcModuloDto
                {
                    Nombre = "Dashboard Comercial Maestro",
                    Descripcion = "Seguimiento de ventas y metas comerciales por asesor y período (maestro).",
                    Icono = "bi-graph-up-arrow",
                    ColorClase = "text-primary",
                    Controller = "DashboardComercialMaestro",
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

            if (menus.VentasReporteReclamos)
            {
                modulos.Add(new SgcModuloDto
                {
                    Nombre = "Reporte de Reclamos",
                    Descripcion = "Dashboard analítico de reclamos de clientes: KPIs, tendencias por mes, familia, motivo y cliente.",
                    Icono = "bi-exclamation-triangle",
                    ColorClase = "text-warning",
                    Controller = "ReporteReclamos",
                    Action = "Index"
                });
            }

            if (menus.VentasDashboardPedidoValorizadoEst)
            {
                modulos.Add(new SgcModuloDto
                {
                    Nombre = "Pedidos Valorizados / Estado",
                    Descripcion = "Listado de pedidos pendientes con filtros por cliente, O/Compra, material y fecha de entrega, resaltando contraentregas sin anticipo.",
                    Icono = "bi-list-check",
                    ColorClase = "text-info",
                    Controller = "DashboardPedidoValorizadoEst",
                    Action = "Index"
                });
            }

            return View(modulos);
        }
    }
}
