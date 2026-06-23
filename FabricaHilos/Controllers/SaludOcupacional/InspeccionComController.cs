using FabricaHilos.Models.SaludOcupacional;
using FabricaHilos.Services.SaludOcupacional;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FabricaHilos.Controllers.SaludOcupacional;

[Authorize]
[Route("SaludOcupacional/InspeccionCom/[action]")]
public class InspeccionComController : OracleBaseController
{
    private readonly ISoInspeccionComService  _svc;
    private readonly ISoInspeccionPdfService  _pdf;
    private readonly ILogger<InspeccionComController> _logger;
    private readonly IWebHostEnvironment _env;

    public InspeccionComController(
        ISoInspeccionComService  svc,
        ISoInspeccionPdfService  pdf,
        ILogger<InspeccionComController> logger,
        IWebHostEnvironment env)
    {
        _svc    = svc;
        _pdf    = pdf;
        _logger = logger;
        _env    = env;
    }

    // ────────────────────────────────────────────────────────────────────────
    // GET  /SaludOcupacional/InspeccionCom/Dashboard
    // ────────────────────────────────────────────────────────────────────────
    [HttpGet("/SaludOcupacional/InspeccionCom")]
    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        try
        {
            var vm = await _svc.ObtenerDashboardAsync();
            return View("~/Views/SaludOcupacional/InspeccionCom/Dashboard.cshtml", vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SO] Error al cargar Dashboard de inspecciones");
            TempData["Error"] = "Error al cargar el dashboard. Intente nuevamente.";
            return View("~/Views/SaludOcupacional/InspeccionCom/Dashboard.cshtml",
                new SoDashboardViewModel());
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // GET  /SaludOcupacional/InspeccionCom/Historial
    // ────────────────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Historial(int? idCom, string? estado)
    {
        var inspecciones = await _svc.ListarInspeccionesAsync(idCom, estado, top: 100);
        var comedores    = await _svc.ObtenerCOMEDORESAsync();
        ViewBag.Comedores   = comedores;
        ViewBag.FiltroIdCom = idCom;
        ViewBag.FiltroEst   = estado;
        return View("~/Views/SaludOcupacional/InspeccionCom/Historial.cshtml", inspecciones);
    }

    // ────────────────────────────────────────────────────────────────────────
    // GET  /SaludOcupacional/InspeccionCom/Nueva
    // ────────────────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Nueva()
    {
        var vm = new SoNuevaInspeccionViewModel
        {
            Inspeccion = new SoInspeccion
            {
                FechaInsp = DateTime.Today,
                Inspector = HttpContext.Session.GetString("OracleUser") ?? string.Empty
            },
            Comedores = await _svc.ObtenerCOMEDORESAsync(),
            Rubros    = BuildRubrosConDetalles(await _svc.ObtenerRUBROSConItemsAsync(), null)
        };
        return View("~/Views/SaludOcupacional/InspeccionCom/Nueva.cshtml", vm);
    }

    // ────────────────────────────────────────────────────────────────────────
    // GET  /SaludOcupacional/InspeccionCom/Editar/{id}
    // ────────────────────────────────────────────────────────────────────────
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Editar(long id)
    {
        var insp = await _svc.ObtenerPorIdAsync(id);
        if (insp is null) return NotFound();
        if (!insp.EsBorrador)
        {
            TempData["Error"] = "Solo se pueden editar inspecciones en estado Borrador.";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        var detalles = await _svc.ObtenerDetalleAsync(id);
        var rubros   = await _svc.ObtenerRUBROSConItemsAsync();

        var vm = new SoNuevaInspeccionViewModel
        {
            Inspeccion = insp,
            Comedores  = await _svc.ObtenerCOMEDORESAsync(),
            Rubros     = BuildRubrosConDetalles(rubros, detalles)
        };
        return View("~/Views/SaludOcupacional/InspeccionCom/Nueva.cshtml", vm);
    }

    // ────────────────────────────────────────────────────────────────────────
    // GET  /SaludOcupacional/InspeccionCom/Detalle/{id}
    // ────────────────────────────────────────────────────────────────────────
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detalle(long id)
    {
        var insp = await _svc.ObtenerPorIdAsync(id);
        if (insp is null) return NotFound();

        var detalles  = await _svc.ObtenerDetalleAsync(id);
        var rubros    = await _svc.ObtenerRUBROSConItemsAsync();
        var acciones  = await _svc.ObtenerAccionesInspeccionAsync(id);
        var evidencias= await _svc.ObtenerEvidenciasAsync(id);

        var vm = new SoDetalleInspeccionViewModel
        {
            Inspeccion = insp,
            Rubros     = BuildRubrosConDetalles(rubros, detalles),
            Acciones   = acciones,
            Evidencias = evidencias
        };
        return View("~/Views/SaludOcupacional/InspeccionCom/Detalle.cshtml", vm);
    }

    // ────────────────────────────────────────────────────────────────────────
    // GET  /SaludOcupacional/InspeccionCom/ExportarPdf/{id}
    // ────────────────────────────────────────────────────────────────────────
    [HttpGet("{id:long}")]
    public async Task<IActionResult> ExportarPdf(long id)
    {
        var insp = await _svc.ObtenerPorIdAsync(id);
        if (insp is null) return NotFound();

        try
        {
            var detalles   = await _svc.ObtenerDetalleAsync(id);
            var rubros     = await _svc.ObtenerRUBROSConItemsAsync();
            var acciones   = await _svc.ObtenerAccionesInspeccionAsync(id);
            var evidencias = await _svc.ObtenerEvidenciasAsync(id);

            // Resolver ruta física de cada evidencia
            foreach (var ev in evidencias)
                if (!string.IsNullOrEmpty(ev.RutaArch))
                    ev.RutaFisica = Path.Combine(_env.WebRootPath,
                                                 ev.RutaArch.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

            var vm = new SoDetalleInspeccionViewModel
            {
                Inspeccion = insp,
                Rubros     = BuildRubrosConDetalles(rubros, detalles),
                Acciones   = acciones,
                Evidencias = evidencias
            };

            var logoPath = Path.Combine(_env.WebRootPath, "img", "logo.png");
            var pdfBytes = _pdf.Generar(vm, logoPath);

            var nombreArchivo = $"InspeccionComedor_{insp.FechaInsp:yyyyMMdd}_{insp.NombreComedor?.Replace(" ","_")}.pdf";
            return File(pdfBytes, "application/pdf", nombreArchivo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar PDF inspección {Id}", id);
            TempData["Error"] = "Error al generar el PDF. Por favor intente nuevamente.";
            return RedirectToAction(nameof(Detalle), new { id });
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // GET  /SaludOcupacional/InspeccionCom/Acciones
    // ────────────────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Acciones(string filtro = "PR", int? idCom = null)
    {
        IReadOnlyList<SoInspAccion> acciones;
        if (filtro == "R")
        {
            acciones = await _svc.ObtenerAccionesResueltasAsync(idCom);
        }
        else
        {
            var abiertas = await _svc.ObtenerAccionesAbiertasAsync(idCom);
            acciones = filtro == "P" ? abiertas.Where(a => a.Estado == "P").ToList()
                     : filtro == "E" ? abiertas.Where(a => a.Estado == "E").ToList()
                     : abiertas;
        }

        var vm = new SoAccionesViewModel
        {
            Acciones       = acciones,
            TotalPendientes= acciones.Count(a => a.Estado == "P"),
            TotalEnProceso = acciones.Count(a => a.Estado == "E"),
            TotalVencidas  = acciones.Count(a => a.EsVencida),
            TotalResueltas = acciones.Count(a => a.Estado == "R"),
            FiltroEstado   = filtro
        };
        return View("~/Views/SaludOcupacional/InspeccionCom/Acciones.cshtml", vm);
    }

    // ────────────────────────────────────────────────────────────────────────
    // POST /SaludOcupacional/InspeccionCom/GuardarInspeccion
    // ────────────────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarInspeccion(SoNuevaInspeccionViewModel vm, string accion)
    {
        var usuario = HttpContext.Session.GetString("OracleUser") ?? "SISTEMA";
        long idInsp;

        // Validar que se haya seleccionado un comedor antes de tocar la BD
        if (vm.Inspeccion.IdCom <= 0)
        {
            TempData["Error"] = "Debe seleccionar un comedor antes de guardar.";
            return vm.Inspeccion.IdInsp == 0
                ? RedirectToAction(nameof(Nueva))
                : RedirectToAction(nameof(Editar), new { id = vm.Inspeccion.IdInsp });
        }

        try
        {
            if (vm.Inspeccion.IdInsp == 0)
            {
                idInsp = await _svc.CrearBorradorAsync(vm.Inspeccion, usuario);
            }
            else
            {
                await _svc.ActualizarEncabezadoAsync(vm.Inspeccion, usuario);
                idInsp = vm.Inspeccion.IdInsp;
            }

            // Guardar puntajes del checklist si vienen en el form
            if (vm.Rubros?.Any() == true)
            {
                var detalles = vm.Rubros.SelectMany(r => r.Items).ToList();
                if (detalles.Any())
                    await _svc.GuardarDetallesLoteAsync(detalles, usuario);
            }

            if (accion == "cerrar")
            {
                await _svc.CerrarInspeccionAsync(idInsp, usuario);
                TempData["Success"] = "Inspección cerrada correctamente.";
                return RedirectToAction(nameof(Detalle), new { id = idInsp });
            }

            TempData["Success"] = "Inspección guardada como borrador.";
            return RedirectToAction(nameof(Editar), new { id = idInsp });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return vm.Inspeccion.IdInsp == 0
                ? RedirectToAction(nameof(Nueva))
                : RedirectToAction(nameof(Editar), new { id = vm.Inspeccion.IdInsp });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SO] Error al guardar inspección");
            TempData["Error"] = "Error al guardar la inspección. Intente nuevamente.";
            return vm.Inspeccion.IdInsp == 0
                ? RedirectToAction(nameof(Nueva))
                : RedirectToAction(nameof(Editar), new { id = vm.Inspeccion.IdInsp });
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // POST /SaludOcupacional/InspeccionCom/GuardarPuntaje   (AJAX)
    // ────────────────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarPuntaje(
        [FromBody] GuardarPuntajeRequest req)
    {
        try
        {
            var usuario = HttpContext.Session.GetString("OracleUser") ?? "SISTEMA";
            await _svc.GuardarDetalleAsync(new SoInspDetalle
            {
                IdDetalle  = req.IdDetalle,
                Puntaje    = req.Puntaje,
                Hallazgo   = req.Hallazgo,
                Responsable= req.Responsable
            }, usuario);
            return Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SO] Error GuardarPuntaje detalle {Id}", req.IdDetalle);
            return BadRequest(new { ok = false, error = ex.Message });
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // POST /SaludOcupacional/InspeccionCom/SubirEvidencia   (AJAX multipart)
    // ────────────────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10_485_760)]  // 10 MB por foto
    public async Task<IActionResult> SubirEvidencia(
        long idInsp, long idDetalle, IFormFile archivo, string? descripcion)
    {
        if (archivo is null || archivo.Length == 0)
            return BadRequest(new { ok = false, error = "Archivo vacío." });

        var ext = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp"))
            return BadRequest(new { ok = false, error = "Solo se permiten imágenes (jpg, png, webp)." });

        try
        {
            var carpeta = Path.Combine(_env.WebRootPath, "uploads", "so", idInsp.ToString());
            Directory.CreateDirectory(carpeta);

            var nombreArch = $"{idDetalle}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
            var ruta       = Path.Combine(carpeta, nombreArch);
            await using var fs = System.IO.File.Create(ruta);
            await archivo.CopyToAsync(fs);

            var rutaRelativa = $"/uploads/so/{idInsp}/{nombreArch}";
            var usuario      = HttpContext.Session.GetString("OracleUser") ?? "SISTEMA";

            var idEvi = await _svc.AgregarEvidenciaAsync(new SoInspEvidencia
            {
                IdDetalle  = idDetalle,
                IdInsp     = idInsp,
                NombreArch = nombreArch,
                RutaArch   = rutaRelativa,
                Descripcion= descripcion,
                Usuario    = usuario
            });

            return Ok(new { ok = true, idEvidencia = idEvi, ruta = rutaRelativa });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SO] Error al subir evidencia inspección {Id}", idInsp);
            return StatusCode(500, new { ok = false, error = "Error al guardar la imagen." });
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // POST /SaludOcupacional/InspeccionCom/CrearAccion
    // ────────────────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearAccion(SoInspAccion accion)
    {
        var usuario = HttpContext.Session.GetString("OracleUser") ?? "SISTEMA";
        await _svc.CrearAccionAsync(accion, usuario);
        TempData["Success"] = "Acción correctiva registrada.";
        return RedirectToAction(nameof(Editar), new { id = accion.IdInsp });
    }

    // ────────────────────────────────────────────────────────────────────────
    // POST /SaludOcupacional/InspeccionCom/ActualizarAccion  (AJAX)
    // ────────────────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActualizarAccion(
        [FromBody] ActualizarAccionRequest req)
    {
        try
        {
            var usuario = HttpContext.Session.GetString("OracleUser") ?? "SISTEMA";
            await _svc.ActualizarEstadoAccionAsync(req.IdAccion, req.Estado, req.Observacion, usuario);
            return Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SO] Error al actualizar acción {Id}", req.IdAccion);
            return BadRequest(new { ok = false, error = ex.Message });
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // POST /SaludOcupacional/InspeccionCom/Anular
    // ────────────────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Anular(long id)
    {
        var usuario = HttpContext.Session.GetString("OracleUser") ?? "SISTEMA";
        try
        {
            await _svc.AnularInspeccionAsync(id, usuario);
            TempData["Success"] = "Inspección anulada.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SO] Error al anular inspección {Id}", id);
            TempData["Error"] = "Error al anular la inspección. Intente nuevamente.";
        }
        return RedirectToAction(nameof(Historial));
    }

    // ────────────────────────────────────────────────────────────────────────
    // Helpers privados
    // ────────────────────────────────────────────────────────────────────────

    private static IReadOnlyList<SoRubroConDetalles> BuildRubrosConDetalles(
        IReadOnlyList<SoInspRubro>   rubros,
        IReadOnlyList<SoInspDetalle>? detallesExistentes)
    {
        return rubros.Select(r => new SoRubroConDetalles
        {
            Rubro = r,
            Items = r.Items.Select(item =>
            {
                var det = detallesExistentes?.FirstOrDefault(d => d.IdItem == item.IdItem);
                return new SoInspDetalle
                {
                    IdItem      = item.IdItem,
                    IdDetalle   = det?.IdDetalle ?? 0,
                    IdInsp      = det?.IdInsp    ?? 0,
                    CodItem     = item.CodItem,
                    Descripcion = item.Descripcion,
                    PtsMax      = item.PtsMax,
                    Puntaje     = det?.Puntaje    ?? 0,
                    Hallazgo    = det?.Hallazgo,
                    Responsable = det?.Responsable,
                    TieneAccion = det?.TieneAccion ?? "N",
                    IdRubro     = r.IdRubro,
                    CodRubro    = r.CodRubro
                };
            }).ToList()
        }).ToList();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Request DTOs (AJAX)
// ─────────────────────────────────────────────────────────────────────────────

public record GuardarPuntajeRequest(
    long    IdDetalle,
    int     Puntaje,
    string? Hallazgo,
    string? Responsable);

public record ActualizarAccionRequest(
    long    IdAccion,
    string  Estado,
    string? Observacion);
