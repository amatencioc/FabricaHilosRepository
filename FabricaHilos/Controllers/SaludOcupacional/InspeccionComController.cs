using FabricaHilos.Helpers;
using FabricaHilos.Models.SaludOcupacional;
using FabricaHilos.Services.Archivos;
using FabricaHilos.Services.SaludOcupacional;
using FabricaHilos.Notificaciones.Abstractions;
using FabricaHilos.Notificaciones.Models.Payloads;
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
    private readonly string _rutaSO;
    private readonly IProcesadorArchivoService _procesadorArchivo;
    private readonly IEmailNotificacionService _email;

    public InspeccionComController(
        ISoInspeccionComService  svc,
        ISoInspeccionPdfService  pdf,
        ILogger<InspeccionComController> logger,
        IWebHostEnvironment env,
        IConfiguration configuration,
        IProcesadorArchivoService procesadorArchivo,
        IEmailNotificacionService email)
    {
        _svc              = svc;
        _pdf              = pdf;
        _logger           = logger;
        _env              = env;
        _procesadorArchivo = procesadorArchivo;
        _email            = email;
        _rutaSO = configuration.GetValue<string>("RutaSaludOcupacional")
            ?? throw new InvalidOperationException(
                "La clave 'RutaSaludOcupacional' no está definida en appsettings.json.");
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
        try
        {
            var insp = await _svc.ObtenerPorIdAsync(id);
            if (insp is null) return NotFound();

            var detalles  = await _svc.ObtenerDetalleAsync(id);
            var rubros    = await _svc.ObtenerRUBROSConItemsAsync();
            var acciones  = await _svc.ObtenerAccionesInspeccionAsync(id);
            var evidencias= await _svc.ObtenerEvidenciasAsync(id);
            var hallazgos = await _svc.ObtenerHallazgosAsync(id);

            foreach (var h in hallazgos)
                foreach (var img in h.Imgs)
                    if (!string.IsNullOrEmpty(img.RutaArch))
                        img.RutaFisica = img.RutaArch;

            var vm = new SoDetalleInspeccionViewModel
            {
                Inspeccion = insp,
                Rubros     = BuildRubrosConDetalles(rubros, detalles),
                Acciones   = acciones,
                Evidencias = evidencias,
                Hallazgos  = hallazgos
            };
            return View("~/Views/SaludOcupacional/InspeccionCom/Detalle.cshtml", vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SO] Error al cargar Detalle de inspección {Id}", id);
            TempData["Error"] = "Error al cargar el detalle de la inspección. Intente nuevamente.";
            return RedirectToAction(nameof(Historial));
        }
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
            var hallazgos  = await _svc.ObtenerHallazgosAsync(id);
            var personalClasif = await _svc.ObtenerPersonalClasifAsync(soloActivos: true);

            // RutaArch ya es la ruta UNC física — se asigna directamente
            foreach (var ev in evidencias)
                if (!string.IsNullOrEmpty(ev.RutaArch))
                    ev.RutaFisica = ev.RutaArch;

            // Resolver ruta física de imágenes de hallazgos
            foreach (var h in hallazgos)
                foreach (var img in h.Imgs)
                    if (!string.IsNullOrEmpty(img.RutaArch))
                        img.RutaFisica = img.RutaArch;

            var vm = new SoDetalleInspeccionViewModel
            {
                Inspeccion = insp,
                Rubros     = BuildRubrosConDetalles(rubros, detalles),
                Acciones   = acciones,
                Evidencias = evidencias,
                Hallazgos  = hallazgos,
                PersonalClasif = personalClasif
            };

            var logoPath = Path.Combine(_env.WebRootPath, "img", "logo.png");
            var pdfBytes = _pdf.Generar(vm, logoPath);

            var nombreArchivo = $"InspeccionComedor_{insp.FechaInsp:yyyyMMdd}_{insp.NombreComedor?.Replace(" ","_")}.pdf";
            // Servir inline para que el browser lo muestre en una pestaña nueva;
            // el usuario decide si lo descarga o imprime desde el visor del PDF.
            Response.Headers["Content-Disposition"] = $"inline; filename=\"{nombreArchivo}\"";
            return File(pdfBytes, "application/pdf");
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
        // Siempre cargamos ambas fuentes para los KPIs globales
        var abiertas  = await _svc.ObtenerAccionesAbiertasAsync(idCom);
        var resueltas = await _svc.ObtenerAccionesResueltasAsync(idCom);

        IReadOnlyList<SoInspAccion> acciones = filtro switch
        {
            "P"  => abiertas.Where(a => a.Estado == "P").ToList(),
            "R"  => resueltas,
            "V"  => resueltas.Where(a => a.Estado == "V").ToList(),
            _    => abiertas.Concat(resueltas)
                            .OrderByDescending(a => a.FechaInsp)
                            .ThenBy(a => a.FchLimite)
                            .ToList()   // "PR" = Todas
        };

        // KPIs sobre el universo completo (no solo el filtro aplicado)
        var todas = abiertas.Concat(resueltas).ToList();
        var vm = new SoAccionesViewModel
        {
            Acciones       = acciones,
            TotalPendientes= todas.Count(a => a.Estado == "P"),
            TotalVencidas  = todas.Count(a => a.EsVencida),
            TotalResueltas = resueltas.Count,
            FiltroEstado   = filtro
        };

        // Cargar hallazgos con fotos para cada inspección referenciada
        var inspeccionIds = acciones
            .Where(a => a.IdInsp > 0)
            .Select(a => a.IdInsp)
            .Distinct();
        foreach (var idInsp in inspeccionIds)
        {
            var halls = await _svc.ObtenerHallazgosAsync(idInsp);
            foreach (var h in halls)
            {
                foreach (var img in h.Imgs)
                    img.RutaFisica = string.IsNullOrEmpty(img.RutaArch)
                        ? null
                        : Path.Combine(_rutaSO, "hallazgos", idInsp.ToString(), System.IO.Path.GetFileName(img.RutaArch));
                vm.HallazgosPorId.TryAdd(h.IdHallazgo, h);
            }
        }

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
                idInsp = vm.Inspeccion.IdInsp;

                // Al cerrar no actualizamos el encabezado: los datos ya están guardados en BD
                // y sobreescribirlos con el form dinámico del modal podría borrar campos.
                if (accion != "cerrar")
                    await _svc.ActualizarEncabezadoAsync(vm.Inspeccion, usuario);
            }

            // Guardar puntajes del checklist si vienen en el form
            if (vm.Rubros?.Any() == true)
            {
                var detalles = vm.Rubros.SelectMany(r => r.Items).ToList();
                // Asegurarse de que cada detalle tenga el IdInsp correcto
                foreach (var d in detalles)
                    if (d.IdInsp <= 0) d.IdInsp = idInsp;
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
            _logger.LogDebug("[SO] GuardarPuntaje → IdDetalle={IdDetalle} IdItem={IdItem} IdInsp={IdInsp} Pts={Pts}",
                req.IdDetalle, req.IdItem, req.IdInsp, req.Puntaje);
            await _svc.GuardarDetalleAsync(new SoInspDetalle
            {
                IdDetalle  = req.IdDetalle,
                IdItem     = req.IdItem,
                IdInsp     = req.IdInsp,
                Puntaje    = req.Puntaje,
                Hallazgo   = req.Hallazgo,
                Responsable= req.Responsable
            }, usuario);
            return Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SO] Error GuardarPuntaje detalle {Id} item {Item} insp {Insp}",
                req.IdDetalle, req.IdItem, req.IdInsp);
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

        var extOriginal = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        if (extOriginal is not (".jpg" or ".jpeg" or ".png" or ".webp"))
            return BadRequest(new { ok = false, error = "Solo se permiten imágenes (jpg, png, webp)." });

        try
        {
            // Carpeta en red: \\server\FabricaHilos\SaludOcupacional\{idInsp}
            var carpeta = Path.Combine(_rutaSO, idInsp.ToString());
            EnsureNetworkShare(_rutaSO);
            Directory.CreateDirectory(carpeta);

            var nombreBase = $"{idDetalle}_{DateTime.Now:yyyyMMddHHmmss}";

            // Copiar a MemoryStream primero (sin using: el Task.Run lo necesita vivo)
            var ms = new MemoryStream();
            await archivo.CopyToAsync(ms);
            ms.Position = 0;

            var procesador = new FabricaHilos.Services.Seguridad.Inspeccion
                .ProcesadorImagenSeguridad(_procesadorArchivo, carpeta, _logger);

            string nombreArch;
            var imgTask = Task.Run(async () =>
            {
                await using (ms)   // el Task.Run es dueño del stream: lo dispone al terminar
                {
                    EnsureNetworkShare(_rutaSO);
                    // Se conserva la extensión real del archivo subido para que la validación
                    // de firma binaria (magic bytes) coincida con el contenido real; el
                    // procesador siempre guarda el resultado final como .jpg.
                    return await procesador.GuardarYOptimizarImagenAsync(ms, $"{nombreBase}{extOriginal}");
                }
            });

            if (await Task.WhenAny(imgTask, Task.Delay(TimeSpan.FromSeconds(20))) == imgTask)
                nombreArch = await imgTask;   // nombre real en disco (con extensión .jpg)
            else
            {
                _logger.LogWarning("[SO] SubirEvidencia TIMEOUT 20s — detalle {Id}", idDetalle);
                nombreArch = $"{nombreBase}.jpg";  // fallback: ruta puede no existir
            }

            var rutaFisica = Path.Combine(carpeta, nombreArch);
            var usuario = HttpContext.Session.GetString("OracleUser") ?? "SISTEMA";

            var idEvi = await _svc.AgregarEvidenciaAsync(new SoInspEvidencia
            {
                IdDetalle   = idDetalle,
                IdInsp      = idInsp,
                NombreArch  = nombreArch,
                RutaArch    = rutaFisica,   // ruta UNC — usada por PDF y ServirImagen
                Descripcion = descripcion,
                Usuario     = usuario
            });

            // URL para visualización en browser
            var urlVista = Url.Action(nameof(ServirImagen),
                new { idInsp, nombreArch, tipo = "evi" });

            return Ok(new { ok = true, idEvidencia = idEvi, ruta = urlVista });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SO] Error al subir evidencia inspección {Id}", idInsp);
            return StatusCode(500, new { ok = false, error = "Error al guardar la imagen." });
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // POST /SaludOcupacional/InspeccionCom/EliminarEvidencia  (AJAX JSON)
    // ────────────────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarEvidencia([FromBody] long idEvidencia)
    {
        try
        {
            var ev = await _svc.ObtenerEvidenciaPorIdAsync(idEvidencia);
            if (ev is not null && !string.IsNullOrEmpty(ev.RutaArch)
                && System.IO.File.Exists(ev.RutaArch))
            {
                System.IO.File.Delete(ev.RutaArch);
            }
            await _svc.EliminarEvidenciaAsync(idEvidencia);
            return Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SO] Error al eliminar evidencia {Id}", idEvidencia);
            return BadRequest(new { ok = false, error = ex.Message });
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // GET /SaludOcupacional/InspeccionCom/ServirImagen
    // Sirve archivos de imagen desde la ruta UNC de red al browser.
    // tipo = "evi" (evidencias checklist) | "hal" (imágenes de hallazgos)
    // ────────────────────────────────────────────────────────────────────────
    [HttpGet]
    public IActionResult ServirImagen(long idInsp, string nombreArch, string tipo = "evi")
    {
        if (string.IsNullOrWhiteSpace(nombreArch) ||
            nombreArch.Contains("..") || nombreArch.Contains('/') || nombreArch.Contains('\\'))
            return BadRequest();

        string rutaFisica = tipo == "hal"
            ? Path.Combine(_rutaSO, "hallazgos", idInsp.ToString(), nombreArch)
            : Path.Combine(_rutaSO, idInsp.ToString(), nombreArch);

        EnsureNetworkShare(_rutaSO);

        if (!System.IO.File.Exists(rutaFisica))
            return NotFound();

        var contentType = Path.GetExtension(nombreArch).ToLowerInvariant() switch
        {
            ".png"  => "image/png",
            ".webp" => "image/webp",
            _       => "image/jpeg"
        };

        var stream = new FileStream(rutaFisica, FileMode.Open, FileAccess.Read,
                                    FileShare.Read, bufferSize: 65536, useAsync: true);
        return File(stream, contentType);
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
            if (req.IdDetalle == 0)
                // Viene de SO_INSP_HALLAZGO (IdAccion == ID_HALLAZGO en ese caso)
                await _svc.ActualizarEstadoHallazgoAsync(req.IdAccion, req.Estado, req.Observacion, usuario);
            else
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
    // POST /SaludOcupacional/InspeccionCom/GuardarObservacion  (AJAX)
    // ────────────────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarObservacion(
        [FromBody] GuardarObservacionRequest req)
    {
        try
        {
            var usuario = HttpContext.Session.GetString("OracleUser") ?? "SISTEMA";
            await _svc.GuardarObservacionAsync(req.IdInsp, req.Observacion, usuario);
            return Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SO] Error al guardar observación inspección {Id}", req.IdInsp);
            return BadRequest(new { ok = false, error = ex.Message });
        }
    }

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
    // GET /SaludOcupacional/InspeccionCom/Hallazgos/{id}
    // ────────────────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Hallazgos(long id)
    {
        var insp = await _svc.ObtenerPorIdAsync(id);
        if (insp is null) return NotFound();

        var hallazgos = await _svc.ObtenerHallazgosAsync(id);

        var vm = new SoHallazgosViewModel
        {
            Inspeccion = insp,
            Hallazgos  = hallazgos.ToList()
        };
        return View("~/Views/SaludOcupacional/InspeccionCom/Hallazgos.cshtml", vm);
    }

    // ────────────────────────────────────────────────────────────────────────
    // POST /SaludOcupacional/InspeccionCom/NuevoHallazgo  (AJAX JSON)
    // ────────────────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NuevoHallazgo([FromBody] NuevoHallazgoRequest req)
    {
        try
        {
            var usuario = HttpContext.Session.GetString("OracleUser") ?? "SISTEMA";
            if (string.IsNullOrWhiteSpace(req.Descripcion))
                return BadRequest(new { ok = false, error = "La descripción del hallazgo es obligatoria." });

            var id = await _svc.GuardarHallazgoAsync(new SoHallazgo
            {
                IdInsp      = req.IdInsp,
                Descripcion = req.Descripcion.Trim(),
                AccionCorr  = string.IsNullOrWhiteSpace(req.AccionCorr) ? null : req.AccionCorr.Trim(),
                Estado      = req.Estado ?? "P",
                FchLimite   = req.FchLimite,
                CodClasif   = string.IsNullOrWhiteSpace(req.CodClasif) ? null : req.CodClasif
            }, usuario);

            var hallazgos = await _svc.ObtenerHallazgosAsync(req.IdInsp);
            var nuevo     = hallazgos.FirstOrDefault(h => h.IdHallazgo == id);

            return Ok(new { ok = true, idHallazgo = id, correlativo = nuevo?.Correlativo ?? 0 });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SO] Error creando hallazgo para inspección {Id}", req.IdInsp);
            return BadRequest(new { ok = false, error = ex.Message });
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // POST /SaludOcupacional/InspeccionCom/ActualizarHallazgo  (AJAX JSON)
    // ────────────────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActualizarHallazgo([FromBody] ActualizarHallazgoRequest req)
    {
        try
        {
            var usuario = HttpContext.Session.GetString("OracleUser") ?? "SISTEMA";
            await _svc.ActualizarHallazgoAsync(new SoHallazgo
            {
                IdHallazgo  = req.IdHallazgo,
                Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null! : req.Descripcion.Trim(),
                AccionCorr  = req.AccionCorr,
                ObsSeguim   = req.ObsSeguim,
                Estado      = req.Estado ?? "P",
                FchLimite   = req.FchLimite,
                CodClasif   = string.IsNullOrWhiteSpace(req.CodClasif) ? null : req.CodClasif
            }, usuario);
            return Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SO] Error actualizando hallazgo {Id}", req.IdHallazgo);
            return BadRequest(new { ok = false, error = ex.Message });
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // POST /SaludOcupacional/InspeccionCom/EliminarHallazgo  (AJAX JSON)
    // ────────────────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarHallazgo([FromBody] long idHallazgo)
    {
        try
        {
            await _svc.EliminarHallazgoAsync(idHallazgo);
            return Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SO] Error eliminando hallazgo {Id}", idHallazgo);
            return BadRequest(new { ok = false, error = ex.Message });
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // POST /SaludOcupacional/InspeccionCom/SubirImgHallazgo  (AJAX multipart)
    // ────────────────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10_485_760)]
    public async Task<IActionResult> SubirImgHallazgo(
        long idHallazgo, long idInsp, string tipo, IFormFile archivo, string? descripcion)
    {
        if (archivo is null || archivo.Length == 0)
            return BadRequest(new { ok = false, error = "Archivo vacío." });

        var ext = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp"))
            return BadRequest(new { ok = false, error = "Solo imágenes jpg/png/webp." });

        try
        {
            // Carpeta en red: \\server\FabricaHilos\SaludOcupacional\hallazgos\{idInsp}
            var carpeta = Path.Combine(_rutaSO, "hallazgos", idInsp.ToString());
            EnsureNetworkShare(_rutaSO);
            Directory.CreateDirectory(carpeta);

            var tipoNorm   = tipo?.ToUpper() == "S" ? "S" : "H";
            var nombreBase = $"{idHallazgo}_{tipoNorm}_{DateTime.Now:yyyyMMddHHmmss}";

            // Copiar a MemoryStream (sin using: el Task.Run lo necesita vivo)
            var ms = new MemoryStream();
            await archivo.CopyToAsync(ms);
            ms.Position = 0;

            var procesador = new FabricaHilos.Services.Seguridad.Inspeccion
                .ProcesadorImagenSeguridad(_procesadorArchivo, carpeta, _logger);

            string nombreArch;
            var imgTask = Task.Run(async () =>
            {
                await using (ms)   // el Task.Run es dueño del stream: lo dispone al terminar
                {
                    EnsureNetworkShare(_rutaSO);
                    // Se conserva la extensión real del archivo subido para que la validación
                    // de firma binaria (magic bytes) coincida con el contenido real; el
                    // procesador siempre guarda el resultado final como .jpg.
                    return await procesador.GuardarYOptimizarImagenAsync(ms, $"{nombreBase}{ext}");
                }
            });

            if (await Task.WhenAny(imgTask, Task.Delay(TimeSpan.FromSeconds(20))) == imgTask)
                nombreArch = await imgTask;   // nombre real en disco
            else
            {
                _logger.LogWarning("[SO] SubirImgHallazgo TIMEOUT 20s — hallazgo {Id}", idHallazgo);
                nombreArch = $"{nombreBase}.jpg";  // fallback
            }

            var rutaFisica = Path.Combine(carpeta, nombreArch);
            var usuario = HttpContext.Session.GetString("OracleUser") ?? "SISTEMA";
            var idImg = await _svc.AgregarImgHallazgoAsync(new SoHallazgoImg
            {
                IdHallazgo  = idHallazgo,
                Tipo        = tipoNorm,
                RutaArch    = rutaFisica,   // ruta UNC física
                Descripcion = descripcion,
                UsrCrea     = usuario
            });

            var urlVista = Url.Action(nameof(ServirImagen),
                new { idInsp, nombreArch, tipo = "hal" });

            return Ok(new { ok = true, idImg, ruta = urlVista });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SO] Error subiendo imagen hallazgo {Id}", idHallazgo);
            return StatusCode(500, new { ok = false, error = "Error al guardar la imagen." });
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // POST /SaludOcupacional/InspeccionCom/EliminarImgHallazgo  (AJAX JSON)
    // ────────────────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarImgHallazgo([FromBody] long idImg)
    {
        try
        {
            var img = await _svc.ObtenerImgHallazgoPorIdAsync(idImg);
            if (img is not null && !string.IsNullOrEmpty(img.RutaArch)
                && System.IO.File.Exists(img.RutaArch))
            {
                System.IO.File.Delete(img.RutaArch);
            }
            await _svc.EliminarImgHallazgoAsync(idImg);
            return Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SO] Error eliminando imagen hallazgo {Id}", idImg);
            return BadRequest(new { ok = false, error = ex.Message });
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // POST /SaludOcupacional/InspeccionCom/EnviarCorreoClasif  (AJAX JSON)
    // Envía al personal asignado a una clasificación el PDF con SOLO los hallazgos
    // de esa clasificación para la inspección indicada.
    // ────────────────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnviarCorreoClasif([FromBody] EnviarCorreoClasifRequest req)
    {
        try
        {
            var insp = await _svc.ObtenerPorIdAsync(req.IdInsp);
            if (insp is null) return NotFound(new { ok = false, error = "Inspección no encontrada." });

            var codClasifReq = req.CodClasif?.Trim();
            if (string.IsNullOrWhiteSpace(codClasifReq))
                return BadRequest(new { ok = false, error = "Debe indicar la clasificación." });

            var hallazgos = (await _svc.ObtenerHallazgosAsync(req.IdInsp))
                .Where(h => string.Equals(h.CodClasif?.Trim(), codClasifReq, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (hallazgos.Count == 0)
                return BadRequest(new { ok = false, error = "No hay hallazgos de esta clasificación para esta inspección." });

            foreach (var h in hallazgos)
                foreach (var img in h.Imgs)
                    if (!string.IsNullOrEmpty(img.RutaArch))
                        img.RutaFisica = img.RutaArch;

            var personalTodos = (await _svc.ObtenerPersonalClasifAsync(req.CodClasif, soloActivos: true)).ToList();
            if (personalTodos.Count == 0)
                return BadRequest(new { ok = false, error = "No hay personal asignado a esta clasificación. Configúrelo en Mantenimiento de Personal." });

            // Solo se incluyen correos con formato válido; los inválidos/incompletos se
            // reportan al usuario en vez de intentar enviarlos silenciosamente (causa común
            // de "el correo no llega": direcciones vacías o mal escritas en Mantenimiento de Personal).
            bool esEmailValido(string? e) => !string.IsNullOrWhiteSpace(e) &&
                System.Text.RegularExpressions.Regex.IsMatch(e.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$");

            var personal      = personalTodos.Where(p => esEmailValido(p.Email)).ToList();
            var sinCorreo     = personalTodos.Where(p => !esEmailValido(p.Email)).Select(p => p.Nombre).ToList();

            if (personal.Count == 0)
                return BadRequest(new { ok = false, error = $"El personal asignado a esta clasificación no tiene correo válido configurado ({string.Join(", ", sinCorreo)}). Corríjalo en Mantenimiento de Personal." });

            var logoPath = Path.Combine(_env.WebRootPath, "img", "logo.png");
            var pdfBytes = _pdf.GenerarPorClasificacion(insp, hallazgos, codClasifReq, logoPath);
            var clasifLabel = SoClasificacion.Label(codClasifReq);
            var nombreArchivo = $"Hallazgos_{clasifLabel.Replace(" ", "_")}_{insp.FechaInsp:yyyyMMdd}.pdf";
            var correosDestino = personal.Select(p => p.Email!.Trim()).ToList();

            var payload = new InspeccionHallazgosClasifPayload
            {
                CorreoDestinatario = personal[0].Email,
                NombreDestinatario = personal[0].Nombre,
                CorreosTo          = personal.Skip(1).Select(p => p.Email).ToList(),
                NombreComedor      = insp.NombreComedor ?? "---",
                FechaInsp          = insp.FechaInsp.ToString("dd/MM/yyyy"),
                Clasificacion      = clasifLabel,
                CantHallazgos      = hallazgos.Count.ToString(),
                NombreArchivo      = nombreArchivo,
                ArchivoPdf         = pdfBytes
            };

            _logger.LogInformation(
                "[SO] Enviando correo de hallazgos {Clasif} (insp {IdInsp}) a: {Correos} | adjunto: {Archivo}",
                clasifLabel, req.IdInsp, string.Join(", ", correosDestino), nombreArchivo);

            var enviado = await _email.EnviarAsync(payload);
            if (!enviado)
                return StatusCode(500, new { ok = false, error = "No se pudo enviar el correo. Revise la configuración SMTP o intente nuevamente." });

            return Ok(new
            {
                ok            = true,
                destinatarios = personal.Count,
                correos       = correosDestino,
                clasificacion = clasifLabel,
                cantHallazgos = hallazgos.Count,
                archivo       = nombreArchivo,
                sinCorreo     = sinCorreo
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SO] Error enviando correo de hallazgos clasificados para inspección {Id}, clasif {Clasif}", req.IdInsp, req.CodClasif);
            return BadRequest(new { ok = false, error = ex.Message });
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // GET  /SaludOcupacional/InspeccionCom/PersonalClasif
    // Mantenimiento de personal notificado por clasificación (asignar/quitar)
    // ────────────────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> PersonalClasif()
    {
        var personal = await _svc.ObtenerPersonalClasifAsync(soloActivos: false);
        var vm = new SoPersonalClasifViewModel { Personal = personal.ToList() };
        return View("~/Views/SaludOcupacional/InspeccionCom/PersonalClasif.cshtml", vm);
    }

    // ────────────────────────────────────────────────────────────────────────
    // GET  /SaludOcupacional/InspeccionCom/BuscarPersonal?term=  (AJAX, select2)
    // ────────────────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> BuscarPersonal(string term)
    {
        if (string.IsNullOrWhiteSpace(term) || term.Trim().Length < 2)
            return Json(new { results = Array.Empty<object>() });

        var empleados = await _svc.BuscarEmpleadosAsync(term.Trim());
        return Json(new
        {
            results = empleados.Select(e => new
            {
                id     = e.CCodigo,
                nombre = e.Nombre,
                email  = e.Email,
                text   = $"{e.Nombre}{(string.IsNullOrEmpty(e.DescArea) ? "" : " · " + e.DescArea)}"
                       + (string.IsNullOrEmpty(e.Email) ? " (sin correo)" : "")
            })
        });
    }

    // ────────────────────────────────────────────────────────────────────────
    // POST /SaludOcupacional/InspeccionCom/AsignarPersonal  (AJAX JSON)
    // ────────────────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AsignarPersonal([FromBody] AsignarPersonalRequest req)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(req.Email))
                return BadRequest(new { ok = false, error = "El empleado seleccionado no tiene correo registrado." });

            var usuario = HttpContext.Session.GetString("OracleUser") ?? "SISTEMA";
            var id = await _svc.AsignarPersonalAsync(new SoPersonalClasif
            {
                CodClasif      = req.CodClasif,
                CCodigo        = req.CCodigo,
                Nombre         = req.Nombre,
                Email          = req.Email,
                IndResponsable = req.EsResponsable ? "S" : "N"
            }, usuario);

            return Ok(new { ok = true, idPersonal = id, esResponsable = req.EsResponsable });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SO] Error asignando personal {Ccodigo} a clasificación {Clasif}", req.CCodigo, req.CodClasif);
            return BadRequest(new { ok = false, error = ex.Message });
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // POST /SaludOcupacional/InspeccionCom/QuitarPersonal  (AJAX JSON)
    // ────────────────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuitarPersonal([FromBody] long idPersonal)
    {
        try
        {
            var usuario = HttpContext.Session.GetString("OracleUser") ?? "SISTEMA";
            await _svc.QuitarPersonalAsync(idPersonal, usuario);
            return Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SO] Error quitando personal {Id}", idPersonal);
            return BadRequest(new { ok = false, error = ex.Message });
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // POST /SaludOcupacional/InspeccionCom/MarcarResponsable  (AJAX JSON)
    // Alterna si la persona figura como "Responsable" en el PDF (S) o solo
    // recibe copia del correo por ser Inspector/Medico SSO (N).
    // ────────────────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarcarResponsable([FromBody] MarcarResponsableRequest req)
    {
        try
        {
            var usuario = HttpContext.Session.GetString("OracleUser") ?? "SISTEMA";
            await _svc.MarcarResponsableAsync(req.IdPersonal, req.EsResponsable, usuario);
            return Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SO] Error marcando responsable {Id}", req.IdPersonal);
            return BadRequest(new { ok = false, error = ex.Message });
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // Helpers privados
    // ────────────────────────────────────────────────────────────────────────

    private static List<SoRubroConDetalles> BuildRubrosConDetalles(
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
    string? Responsable,
    int     IdItem  = 0,
    long    IdInsp  = 0);

public record ActualizarAccionRequest(
    long    IdAccion,
    long    IdDetalle,
    string  Estado,
    string? Observacion);

public record GuardarObservacionRequest(
    long    IdInsp,
    string? Observacion);

public record NuevoHallazgoRequest(
    long      IdInsp,
    string?   Descripcion,
    string?   AccionCorr  = null,
    string?   Estado      = "P",
    DateTime? FchLimite   = null,
    string?   CodClasif   = null);

public record ActualizarHallazgoRequest(
    long      IdHallazgo,
    string?   Descripcion,
    string?   AccionCorr,
    string?   ObsSeguim,
    string?   Estado,
    DateTime? FchLimite,
    string?   CodClasif = null);

public record EnviarCorreoClasifRequest(long IdInsp, string CodClasif);

public record AsignarPersonalRequest(string CodClasif, string CCodigo, string Nombre, string Email, bool EsResponsable = true);

public record MarcarResponsableRequest(long IdPersonal, bool EsResponsable);
