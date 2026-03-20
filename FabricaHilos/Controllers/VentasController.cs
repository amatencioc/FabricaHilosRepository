using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FabricaHilos.Data;
using FabricaHilos.Models.Ventas;

namespace FabricaHilos.Controllers
{
    [Authorize]
    public class VentasController : Controller
    {
        private readonly ApplicationDbContext _context;

        public VentasController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        // ========== CLIENTES ==========

        public async Task<IActionResult> Clientes(string? buscar)
        {
            var query = _context.Clientes.AsQueryable();
            if (!string.IsNullOrEmpty(buscar))
                query = query.Where(c => c.Nombre.Contains(buscar) || (c.RucDni != null && c.RucDni.Contains(buscar)));
            ViewBag.Buscar = buscar;
            return View(await query.OrderBy(c => c.Nombre).ToListAsync());
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        public IActionResult CrearCliente()
        {
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearCliente(Cliente model)
        {
            if (ModelState.IsValid)
            {
                _context.Clientes.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Cliente creado exitosamente.";
                return RedirectToAction(nameof(Clientes));
            }
            return View(model);
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        public async Task<IActionResult> EditarCliente(int id)
        {
            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null) return NotFound();
            return View(cliente);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarCliente(int id, Cliente model)
        {
            if (id != model.Id) return BadRequest();
            if (ModelState.IsValid)
            {
                _context.Clientes.Update(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Cliente actualizado.";
                return RedirectToAction(nameof(Clientes));
            }
            return View(model);
        }

        // ========== PEDIDOS ==========

        public async Task<IActionResult> Pedidos(string? buscar, EstadoPedido? estado)
        {
            var query = _context.Pedidos.Include(p => p.Cliente).AsQueryable();
            if (!string.IsNullOrEmpty(buscar))
                query = query.Where(p => p.NumeroPedido.Contains(buscar) || (p.Cliente != null && p.Cliente.Nombre.Contains(buscar)));
            if (estado.HasValue)
                query = query.Where(p => p.Estado == estado.Value);

            ViewBag.Buscar = buscar;
            ViewBag.EstadoFiltro = estado;
            return View(await query.OrderByDescending(p => p.Fecha).ToListAsync());
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        public async Task<IActionResult> CrearPedido()
        {
            ViewBag.Clientes = await _context.Clientes.Where(c => c.Activo).OrderBy(c => c.Nombre).ToListAsync();
            var ultimo = await _context.Pedidos.OrderByDescending(p => p.Id).FirstOrDefaultAsync();
            ViewBag.NumeroPedido = $"PED-{DateTime.Now.Year}-{(ultimo?.Id ?? 0) + 1:D4}";
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearPedido(Pedido model)
        {
            if (ModelState.IsValid)
            {
                _context.Pedidos.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Pedido creado exitosamente.";
                return RedirectToAction(nameof(Pedidos));
            }
            ViewBag.Clientes = await _context.Clientes.Where(c => c.Activo).OrderBy(c => c.Nombre).ToListAsync();
            return View(model);
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        public async Task<IActionResult> EditarPedido(int id)
        {
            var pedido = await _context.Pedidos.FindAsync(id);
            if (pedido == null) return NotFound();
            ViewBag.Clientes = await _context.Clientes.Where(c => c.Activo).OrderBy(c => c.Nombre).ToListAsync();
            return View(pedido);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarPedido(int id, Pedido model)
        {
            if (id != model.Id) return BadRequest();
            if (ModelState.IsValid)
            {
                _context.Pedidos.Update(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Pedido actualizado.";
                return RedirectToAction(nameof(Pedidos));
            }
            ViewBag.Clientes = await _context.Clientes.Where(c => c.Activo).OrderBy(c => c.Nombre).ToListAsync();
            return View(model);
        }
    }
}
