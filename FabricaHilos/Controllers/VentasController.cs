using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FabricaHilos.Filters;
using FabricaHilos.Models.Sgc;
using FabricaHilos.Services;

namespace FabricaHilos.Controllers
{
    [Authorize]
    [VentasAuthorize]
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

            return View(modulos);
        }
    }
}
