using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FabricaHilos.Data;
using FabricaHilos.Models.RecursosHumanos;

namespace FabricaHilos.Controllers
{
    [Authorize]
    public class RecursosHumanosController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RecursosHumanosController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        // ========== EMPLEADOS ==========

        public async Task<IActionResult> Empleados(string? buscar)
        {
            var query = _context.Empleados.AsQueryable();
            if (!string.IsNullOrEmpty(buscar))
                query = query.Where(e => e.NombreCompleto.Contains(buscar) || e.Dni.Contains(buscar) || (e.Cargo != null && e.Cargo.Contains(buscar)));
            ViewBag.Buscar = buscar;
            return View(await query.OrderBy(e => e.NombreCompleto).ToListAsync());
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Gerencia")]
        public IActionResult CrearEmpleado()
        {
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Gerencia")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearEmpleado(Empleado model)
        {
            if (ModelState.IsValid)
            {
                _context.Empleados.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Empleado creado exitosamente.";
                return RedirectToAction(nameof(Empleados));
            }
            return View(model);
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Gerencia")]
        public async Task<IActionResult> EditarEmpleado(int id)
        {
            var empleado = await _context.Empleados.FindAsync(id);
            if (empleado == null) return NotFound();
            return View(empleado);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Gerencia")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarEmpleado(int id, Empleado model)
        {
            if (id != model.Id) return BadRequest();
            if (ModelState.IsValid)
            {
                _context.Empleados.Update(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Empleado actualizado.";
                return RedirectToAction(nameof(Empleados));
            }
            return View(model);
        }

        // ========== ASISTENCIA ==========

        public async Task<IActionResult> Asistencia(int? empleadoId, DateTime? fecha)
        {
            var query = _context.Asistencias.Include(a => a.Empleado).AsQueryable();
            if (empleadoId.HasValue)
                query = query.Where(a => a.EmpleadoId == empleadoId.Value);
            if (fecha.HasValue)
                query = query.Where(a => a.Fecha.Date == fecha.Value.Date);
            else
                query = query.Where(a => a.Fecha.Date == DateTime.Today);

            ViewBag.Empleados = await _context.Empleados.Where(e => e.Activo).OrderBy(e => e.NombreCompleto).ToListAsync();
            ViewBag.EmpleadoId = empleadoId;
            ViewBag.Fecha = fecha ?? DateTime.Today;
            return View(await query.OrderByDescending(a => a.Fecha).ToListAsync());
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        public async Task<IActionResult> RegistrarAsistencia()
        {
            ViewBag.Empleados = await _context.Empleados.Where(e => e.Activo).OrderBy(e => e.NombreCompleto).ToListAsync();
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistrarAsistencia(Asistencia model)
        {
            if (ModelState.IsValid)
            {
                _context.Asistencias.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Asistencia registrada exitosamente.";
                return RedirectToAction(nameof(Asistencia));
            }
            ViewBag.Empleados = await _context.Empleados.Where(e => e.Activo).OrderBy(e => e.NombreCompleto).ToListAsync();
            return View(model);
        }
    }
}
