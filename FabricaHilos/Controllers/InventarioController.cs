using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FabricaHilos.Data;
using FabricaHilos.Models.Inventario;

namespace FabricaHilos.Controllers
{
    [Authorize]
    public class InventarioController : Controller
    {
        private readonly ApplicationDbContext _context;

        public InventarioController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        // ========== MATERIA PRIMA ==========

        public async Task<IActionResult> MateriaPrima(string? buscar)
        {
            var query = _context.MateriasPrimas.AsQueryable();
            if (!string.IsNullOrEmpty(buscar))
                query = query.Where(m => m.Nombre.Contains(buscar) || m.Tipo.Contains(buscar));

            ViewBag.Buscar = buscar;
            return View(await query.OrderBy(m => m.Nombre).ToListAsync());
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        public IActionResult CrearMateriaPrima()
        {
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearMateriaPrima(MateriaPrima model)
        {
            if (ModelState.IsValid)
            {
                _context.MateriasPrimas.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Materia prima creada exitosamente.";
                return RedirectToAction(nameof(MateriaPrima));
            }
            return View(model);
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        public async Task<IActionResult> EditarMateriaPrima(int id)
        {
            var item = await _context.MateriasPrimas.FindAsync(id);
            if (item == null) return NotFound();
            return View(item);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarMateriaPrima(int id, MateriaPrima model)
        {
            if (id != model.Id) return BadRequest();
            if (ModelState.IsValid)
            {
                _context.MateriasPrimas.Update(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Materia prima actualizada exitosamente.";
                return RedirectToAction(nameof(MateriaPrima));
            }
            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Gerencia")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarMateriaPrima(int id)
        {
            var item = await _context.MateriasPrimas.FindAsync(id);
            if (item != null)
            {
                _context.MateriasPrimas.Remove(item);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Materia prima eliminada.";
            }
            return RedirectToAction(nameof(MateriaPrima));
        }

        // ========== PRODUCTO TERMINADO ==========

        public async Task<IActionResult> ProductoTerminado(string? buscar)
        {
            var query = _context.ProductosTerminados.AsQueryable();
            if (!string.IsNullOrEmpty(buscar))
                query = query.Where(p => p.Nombre.Contains(buscar) || p.Tipo.Contains(buscar));

            ViewBag.Buscar = buscar;
            return View(await query.OrderBy(p => p.Nombre).ToListAsync());
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        public IActionResult CrearProductoTerminado()
        {
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearProductoTerminado(ProductoTerminado model)
        {
            if (ModelState.IsValid)
            {
                _context.ProductosTerminados.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Producto terminado creado exitosamente.";
                return RedirectToAction(nameof(ProductoTerminado));
            }
            return View(model);
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        public async Task<IActionResult> EditarProductoTerminado(int id)
        {
            var item = await _context.ProductosTerminados.FindAsync(id);
            if (item == null) return NotFound();
            return View(item);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarProductoTerminado(int id, ProductoTerminado model)
        {
            if (id != model.Id) return BadRequest();
            if (ModelState.IsValid)
            {
                _context.ProductosTerminados.Update(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Producto terminado actualizado.";
                return RedirectToAction(nameof(ProductoTerminado));
            }
            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Gerencia")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarProductoTerminado(int id)
        {
            var item = await _context.ProductosTerminados.FindAsync(id);
            if (item != null)
            {
                _context.ProductosTerminados.Remove(item);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Producto terminado eliminado.";
            }
            return RedirectToAction(nameof(ProductoTerminado));
        }
    }
}
