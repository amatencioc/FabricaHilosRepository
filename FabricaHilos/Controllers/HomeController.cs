using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FabricaHilos.Data;
using FabricaHilos.Models;
using FabricaHilos.Services;
using System.Diagnostics;

namespace FabricaHilos.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HomeController> _logger;
        private readonly IMenuService _menuService;

        public HomeController(ApplicationDbContext context, ILogger<HomeController> logger, IMenuService menuService)
        {
            _context = context;
            _logger = logger;
            _menuService = menuService;
        }

        // Punto de entrada por defecto: redirige al primer módulo disponible del usuario
        public IActionResult Landing()
        {
            var (ctrl, act) = _menuService.GetLanding();
            return RedirectToAction(act, ctrl);
        }

        public async Task<IActionResult> Index()
        {
            var totalProductos = await _context.ProductosTerminados.CountAsync();
            var ordenesActivas = await _context.OrdenesProduccion
                .Where(o => o.Estado == Models.Produccion.EstadoOrden.EnProceso)
                .CountAsync();
            var ventasMes = (decimal)(await _context.Pedidos
                .Where(p => p.Fecha.Month == DateTime.Now.Month && p.Fecha.Year == DateTime.Now.Year)
                .Select(p => (double)p.Total)
                .SumAsync());
            var totalEmpleados = await _context.Empleados.Where(e => e.Activo).CountAsync();
            var stockBajo = await _context.MateriasPrimas.Where(m => m.CantidadDisponible < m.StockMinimo).CountAsync();

            ViewBag.TotalProductos = totalProductos;
            ViewBag.OrdenesActivas = ordenesActivas;
            ViewBag.VentasMes = ventasMes;
            ViewBag.TotalEmpleados = totalEmpleados;
            ViewBag.StockBajo = stockBajo;

            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
