using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FabricaHilos.Models.Sgc;
using FabricaHilos.Services;

namespace FabricaHilos.Controllers.Logistica;

[Authorize]
public class LogisticaController : Controller
{
    private readonly IMenuService _menuService;

    public LogisticaController(IMenuService menuService)
    {
        _menuService = menuService;
    }

    public IActionResult Landing() => RedirectToAction(nameof(Index));

    public IActionResult Index()
    {
        var menus   = _menuService.GetMenusActuales();
        var modulos = new List<SgcModuloDto>();

        if (menus.LogisticaRequerimiento)
        {
            modulos.Add(new SgcModuloDto
            {
                Nombre      = "Requerimientos",
                Descripcion = "Gestión de requerimientos y requisiciones de compra, seguimiento de ítems y adjuntos.",
                Icono       = "bi-clipboard-check",
                ColorClase  = "text-warning",
                Controller  = "Requisicion",
                Action      = "Index"
            });
        }

        if (menus.LogisticaOrdenCompra)
        {
            modulos.Add(new SgcModuloDto
            {
                Nombre      = "Órdenes de Compra",
                Descripcion = "Listado y detalle de órdenes de compra emitidas a proveedores.",
                Icono       = "bi-cart-check",
                ColorClase  = "text-success",
                Controller  = "OrdenCompra",
                Action      = "Index"
            });
        }

        if (menus.LogisticaIndicadores)
        {
            modulos.Add(new SgcModuloDto
            {
                Nombre      = "Indicadores",
                Descripcion = "KPIs y dashboard de requisiciones: resumen por estado, tiempos del ciclo logístico, top destinos y pendientes.",
                Icono       = "bi-bar-chart-line",
                ColorClase  = "text-danger",
                Controller  = "IndicadoresLogistica",
                Action      = "Index"
            });
        }

        return View("~/Views/Logistica/Index.cshtml", modulos);
    }
}
