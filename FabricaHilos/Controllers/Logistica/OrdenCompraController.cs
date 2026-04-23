using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FabricaHilos.Services.Logistica;
using FabricaHilos.Models.Logistica;
using FabricaHilos.Services;

namespace FabricaHilos.Controllers.Logistica;

[Authorize]
[Route("Logistica/OrdenCompra")]
public class OrdenCompraController : OracleBaseController
{
    private readonly IOrdenCompraService _service;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;
    private readonly ILogger<OrdenCompraController> _logger;
    private readonly IEmpresaTemaService _empresaTema;

    private static readonly HashSet<string> _extPermitidas =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx",
            ".jpg", ".jpeg", ".png", ".gif", ".bmp",
            ".txt", ".zip", ".rar"
        };

    public OrdenCompraController(
        IOrdenCompraService service,
        IWebHostEnvironment env,
        IConfiguration config,
        ILogger<OrdenCompraController> logger,
        IEmpresaTemaService empresaTema)
    {
        _service     = service;
        _env         = env;
        _config      = config;
        _logger      = logger;
        _empresaTema = empresaTema;
    }

    // ── LISTADO ────────────────────────────────────────────────────────────────

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(
        string? buscar,
        DateTime? fechaInicio,
        DateTime? fechaFin,
        string? estado,
        int page = 1)
    {
        if (fechaInicio is null && fechaFin is null && string.IsNullOrWhiteSpace(buscar))
        {
            var hoy     = DateTime.Today;
            fechaInicio = new DateTime(hoy.Year, hoy.Month, 1);
            fechaFin    = new DateTime(hoy.Year, hoy.Month, DateTime.DaysInMonth(hoy.Year, hoy.Month));
        }

        const int pageSize = 20;
        var (items, total) = await _service.ObtenerOrdenesAsync(
            buscar, fechaInicio, fechaFin, estado, page, pageSize);

        var codigos = items.Select(o => o.CodProveed)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()!;
        ViewBag.Proveedores  = await _service.ObtenerNombresProveedoresAsync(codigos);

        var codigosCc = items.Select(o => o.CCosto).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct()!;
        ViewBag.CentrosCosto = await _service.ObtenerDescripcionesCentroCostosAsync(codigosCc);

        ViewBag.Buscar      = buscar;
        ViewBag.FechaInicio = fechaInicio?.ToString("yyyy-MM-dd");
        ViewBag.FechaFin    = fechaFin?.ToString("yyyy-MM-dd");
        ViewBag.Estado      = estado;
        ViewBag.Page        = page;
        ViewBag.PageSize    = pageSize;
        ViewBag.TotalCount  = total;
        ViewBag.TotalPages  = (int)Math.Ceiling(total / (double)pageSize);

        return View("~/Views/Logistica/OrdenCompra/Index.cshtml", items);
    }

    // ── DETALLE ────────────────────────────────────────────────────────────────

    [HttpGet("Detalle/{tipoDocto}/{serie:int}/{numPed:long}")]
    public async Task<IActionResult> Detalle(
        string tipoDocto, int serie, long numPed,
        string? buscar, string? fechaInicio, string? fechaFin, string? estado, int page = 1)
    {
        var orden = await _service.ObtenerOrdenAsync(tipoDocto, serie, numPed);
        if (orden is null)
            return NotFound();

        var items = await _service.ObtenerItemsAsync(tipoDocto, serie, numPed);

        var codigos = new[] { orden.CodProveed }.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct()!;
        ViewBag.Proveedores  = await _service.ObtenerNombresProveedoresAsync(codigos);

        var codigosCc = new[] { orden.CCosto }.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct()!;
        ViewBag.CentrosCosto = await _service.ObtenerDescripcionesCentroCostosAsync(codigosCc);

        var codigosCondPag = new[] { orden.CondPag }.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct()!;
        ViewBag.DescripcionesCondPag = await _service.ObtenerDescripcionesCondPagAsync(codigosCondPag);

        var codigosArt = items.Select(i => i.CodArt).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct()!;
        ViewBag.DescripcionesArticulos = await _service.ObtenerDescripcionesArticulosAsync(codigosArt);

        // Nombres de usuarios de auditoría
        var usuariosAuditoria = new[] { orden.AAduser, orden.AMduser }
            .Where(c => !string.IsNullOrWhiteSpace(c)).Distinct()!;
        var tareasNombres = usuariosAuditoria.Select(u => _service.ObtenerNombreEmpleadoAsync(u!));
        var nombresResultado = await Task.WhenAll(tareasNombres);
        var nombresUsuarios = usuariosAuditoria.Zip(nombresResultado, (u, n) => (u!, n))
            .ToDictionary(x => x.Item1, x => x.n, StringComparer.OrdinalIgnoreCase);
        ViewBag.NombresUsuarios = nombresUsuarios;

        ViewBag.ReturnBuscar      = buscar;
        ViewBag.ReturnFechaInicio = fechaInicio;
        ViewBag.ReturnFechaFin    = fechaFin;
        ViewBag.ReturnEstado      = estado;
        ViewBag.ReturnPage        = page;

        EnsureNetworkShare(ObtenerCarpetaRaiz());
        ViewBag.ArchivosExistentes = ObtenerArchivosExistentes(items);

        return View("~/Views/Logistica/OrdenCompra/Detalle.cshtml", (orden, items));
    }

    // ── UPLOAD DE ARCHIVOS ─────────────────────────────────────────────────────

    [HttpPost("SubirArchivos")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubirArchivos(OrdenCompraUploadModel model)
    {
        var redir = new { tipoDocto = model.TipoDocto, serie = model.Serie, numPed = model.NumPed,
                          buscar = model.ReturnBuscar, fechaInicio = model.ReturnFechaInicio,
                          fechaFin = model.ReturnFechaFin, estado = model.ReturnEstado, page = model.ReturnPage };

        if (model.Archivos == null || model.Archivos.Count == 0)
        { TempData["Warning"] = "No se seleccionaron archivos."; return RedirectToAction(nameof(Detalle), redir); }

        if (model.SeleccionItems.Count == 0)
        { TempData["Warning"] = "Debe seleccionar al menos un ítem antes de adjuntar archivos."; return RedirectToAction(nameof(Detalle), redir); }

        long idGrupo = (model.ExistingIdGrupo.HasValue && model.ExistingIdGrupo.Value > 0)
            ? model.ExistingIdGrupo.Value
            : await _service.ObtenerSiguienteIdGrupoAsync();

        string carpeta = ObtenerCarpetaPorGrupo(idGrupo);
        EnsureNetworkShare(ObtenerCarpetaRaiz());
        Directory.CreateDirectory(carpeta);

        var errores = new List<string>();
        int exitosos = 0;

        foreach (var archivo in model.Archivos)
        {
            if (archivo.Length == 0) continue;
            var ext = Path.GetExtension(archivo.FileName);
            if (!_extPermitidas.Contains(ext)) { errores.Add($"Extensión no permitida: {archivo.FileName}"); continue; }

            var nombreSeguro = $"{Path.GetFileNameWithoutExtension(archivo.FileName)}_{model.NumPed}_{DateTime.Now:yyyyMMdd}{ext}";
            nombreSeguro = string.Concat(nombreSeguro.Split(Path.GetInvalidFileNameChars()));
            var rutaDestino = Path.Combine(carpeta, nombreSeguro);
            await using var stream = new FileStream(rutaDestino, FileMode.Create);
            await archivo.CopyToAsync(stream);
            exitosos++;
        }

        if (exitosos > 0)
            await _service.ActualizarIdGrupoItemsAsync(model.TipoDocto!, model.Serie, model.NumPed, model.SeleccionItems, idGrupo);

        TempData[errores.Count > 0 ? "Warning" : "Success"] = errores.Count > 0
            ? $"Se cargaron {exitosos} archivo(s). Errores: {string.Join("; ", errores)}"
            : $"Se cargaron {exitosos} archivo(s) en el grupo {idGrupo}. {model.SeleccionItems.Count} ítem(s) actualizados.";

        return RedirectToAction(nameof(Detalle), redir);
    }

    // ── VER ARCHIVO ────────────────────────────────────────────────────────────

    [HttpGet("Ver/{idGrupo:long}/{nombreArchivo}")]
    public IActionResult Ver(long idGrupo, string nombreArchivo)
    {
        nombreArchivo = Path.GetFileName(nombreArchivo);
        var ruta = Path.Combine(ObtenerCarpetaPorGrupo(idGrupo), nombreArchivo);
        EnsureNetworkShare(ruta);
        if (!System.IO.File.Exists(ruta)) return NotFound();
        return PhysicalFile(ruta, ObtenerContentType(Path.GetExtension(nombreArchivo)));
    }

    // ── APROBAR ARCHIVO ────────────────────────────────────────────────────────

    [HttpPost("AprobarArchivo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AprobarArchivo(
        long idGrupo, string nombreArchivo,
        string tipoDocto, int serie, long numPed,
        string? retBuscar, string? retFechaInicio, string? retFechaFin, string? retEstado, int retPage = 1)
    {
        try
        {
            nombreArchivo = Path.GetFileName(nombreArchivo);
            var carpeta = ObtenerCarpetaPorGrupo(idGrupo);
            EnsureNetworkShare(ObtenerCarpetaRaiz());
            bool ok = false;

            if (nombreArchivo.StartsWith("APROBADO_", StringComparison.OrdinalIgnoreCase))
                ok = System.IO.File.Exists(Path.Combine(carpeta, nombreArchivo));
            else
            {
                var rutaOrig = Path.Combine(carpeta, nombreArchivo);
                var rutaNueva = Path.Combine(carpeta, $"APROBADO_{nombreArchivo}");
                if (System.IO.File.Exists(rutaOrig)) { System.IO.File.Move(rutaOrig, rutaNueva); ok = true; }
            }

            if (ok) { await _service.AprobarGrupoAsync(idGrupo); TempData["Success"] = "Archivo aprobado correctamente."; }
            else TempData["Error"] = "No se encontró el archivo para aprobar.";
        }
        catch (Exception ex) { _logger.LogError(ex, "Error al aprobar archivo grupo {IdGrupo}", idGrupo); TempData["Error"] = ex.Message; }

        return RedirectToAction(nameof(Detalle), new { tipoDocto, serie, numPed, buscar = retBuscar, fechaInicio = retFechaInicio, fechaFin = retFechaFin, estado = retEstado, page = retPage });
    }

    // ── DESCARGAR ARCHIVO ──────────────────────────────────────────────────────

    [HttpGet("Descargar/{idGrupo:long}/{nombreArchivo}")]
    public IActionResult Descargar(long idGrupo, string nombreArchivo)
    {
        nombreArchivo = Path.GetFileName(nombreArchivo);
        var ruta = Path.Combine(ObtenerCarpetaPorGrupo(idGrupo), nombreArchivo);
        EnsureNetworkShare(ruta);
        if (!System.IO.File.Exists(ruta)) return NotFound();
        return PhysicalFile(ruta, ObtenerContentType(Path.GetExtension(nombreArchivo)), nombreArchivo);
    }

    // ── ELIMINAR ARCHIVO ───────────────────────────────────────────────────────

    [HttpPost("EliminarArchivo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarArchivo(long idGrupo, string nombreArchivo,
        string tipoDocto, int serie, long numPed,
        string? retBuscar = null, string? retFechaInicio = null, string? retFechaFin = null,
        string? retEstado = null, int retPage = 1)
    {
        nombreArchivo = Path.GetFileName(nombreArchivo);
        bool eraAprobado = nombreArchivo.StartsWith("APROBADO_", StringComparison.OrdinalIgnoreCase);
        var carpeta = ObtenerCarpetaPorGrupo(idGrupo);
        EnsureNetworkShare(ObtenerCarpetaRaiz());
        var ruta    = Path.Combine(carpeta, nombreArchivo);

        if (System.IO.File.Exists(ruta)) System.IO.File.Delete(ruta);
        if (eraAprobado) await _service.DesaprobarGrupoAsync(idGrupo);

        bool carpetaVacia = !Directory.Exists(carpeta) || !Directory.EnumerateFiles(carpeta).Any();
        if (carpetaVacia)
        {
            await _service.LimpiarIdGrupoAsync(idGrupo);
            if (Directory.Exists(carpeta)) Directory.Delete(carpeta, recursive: false);
        }

        TempData["Success"] = $"Archivo '{nombreArchivo}' eliminado.";
        return RedirectToAction(nameof(Detalle), new { tipoDocto, serie, numPed, buscar = retBuscar, fechaInicio = retFechaInicio, fechaFin = retFechaFin, estado = retEstado, page = retPage });
    }

    // ── HELPERS ────────────────────────────────────────────────────────────────

    private string ObtenerCarpetaRaiz()
    {
        var raiz = _config["RutaRequerimientos"]
                   ?? Path.Combine(_env.WebRootPath, "uploads", "requerimientos");
        var ruc  = _empresaTema.GetRucActual();
        return string.IsNullOrWhiteSpace(ruc)
            ? raiz
            : Path.Combine(raiz, ruc);
    }

    private string ObtenerCarpetaPorGrupo(long idGrupo)
        => Path.Combine(ObtenerCarpetaRaiz(), idGrupo.ToString());

    private List<ArchivoRequisicionDto> ObtenerArchivosExistentes(IEnumerable<ItemOrdDto> items)
    {
        var grupos = items.Where(i => i.IdGrupo.HasValue).GroupBy(i => i.IdGrupo!.Value).ToList();
        var resultado = new List<ArchivoRequisicionDto>();
        foreach (var grupo in grupos)
        {
            var carpeta = ObtenerCarpetaPorGrupo(grupo.Key);
            if (!Directory.Exists(carpeta)) continue;
            resultado.AddRange(Directory.GetFiles(carpeta)
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTime)
                .Select(f => new ArchivoRequisicionDto
                {
                    NombreArchivo = f.Name, RutaRelativa = f.FullName,
                    TamanioBytes  = f.Length, FechaCarga   = f.CreationTime,
                    IdGrupo       = grupo.Key, CarpetaGrupo = carpeta
                }));
        }
        return resultado.OrderByDescending(a => a.IdGrupo).ThenByDescending(a => a.FechaCarga).ToList();
    }

    private static string ObtenerContentType(string extension) => extension.ToLower() switch
    {
        ".pdf"  => "application/pdf",
        ".doc"  => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xls"  => "application/vnd.ms-excel",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png"  => "image/png",
        ".gif"  => "image/gif",
        ".bmp"  => "image/bmp",
        ".txt"  => "text/plain",
        ".zip"  => "application/zip",
        ".rar"  => "application/x-rar-compressed",
        _       => "application/octet-stream"
    };
}
