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
    private readonly INavTokenService _navToken;

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
        IEmpresaTemaService empresaTema,
        INavTokenService navToken)
    {
        _service     = service;
        _env         = env;
        _config      = config;
        _logger      = logger;
        _empresaTema = empresaTema;
        _navToken    = navToken;
    }

    // ── LISTADO ────────────────────────────────────────────────────────────────

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(
        string? t = null,
        string? buscar = null,
        DateTime? fechaInicio = null,
        DateTime? fechaFin = null,
        string? estado = null,
        int page = 1)
    {
        // Si hay filtros nuevos sin token, crear token y redirigir
        if (string.IsNullOrEmpty(t) && (buscar != null || fechaInicio.HasValue || fechaFin.HasValue || estado != null))
        {
            var token = _navToken.Protect(new Dictionary<string, string?> {
                ["buscar"]      = buscar,
                ["fechaInicio"] = fechaInicio?.ToString("yyyy-MM-dd"),
                ["fechaFin"]    = fechaFin?.ToString("yyyy-MM-dd"),
                ["estado"]      = estado
            });
            return RedirectToAction(nameof(Index), new { t = token, page });
        }

        // Desempaquetar token
        if (!string.IsNullOrEmpty(t) && _navToken.TryUnprotect(t, out var nav))
        {
            buscar = nav.GetValueOrDefault("buscar");
            if (DateTime.TryParse(nav.GetValueOrDefault("fechaInicio"), out var fi)) fechaInicio = fi;
            if (DateTime.TryParse(nav.GetValueOrDefault("fechaFin"),    out var ff)) fechaFin    = ff;
            estado = nav.GetValueOrDefault("estado");
        }

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

        // Generar token final con fechas normalizadas para paginación y Detalle
        var navToken = _navToken.Protect(new Dictionary<string, string?> {
            ["buscar"]      = buscar,
            ["fechaInicio"] = fechaInicio?.ToString("yyyy-MM-dd"),
            ["fechaFin"]    = fechaFin?.ToString("yyyy-MM-dd"),
            ["estado"]      = estado
        });

        ViewBag.Buscar      = buscar;
        ViewBag.FechaInicio = fechaInicio?.ToString("yyyy-MM-dd");
        ViewBag.FechaFin    = fechaFin?.ToString("yyyy-MM-dd");
        ViewBag.Estado      = estado;
        ViewBag.NavToken    = navToken;
        ViewBag.Page        = page;
        ViewBag.PageSize    = pageSize;
        ViewBag.TotalCount  = total;
        ViewBag.TotalPages  = (int)Math.Ceiling(total / (double)pageSize);

        return View("~/Views/Logistica/OrdenCompra/Index.cshtml", items);
    }

    // ── DETALLE ────────────────────────────────────────────────────────────────

    [HttpGet("Detalle")]
    public async Task<IActionResult> Detalle(
        string? dt = null,
        string? t = null, int page = 1)
    {
        if (string.IsNullOrEmpty(dt) || !_navToken.TryUnprotect(dt, out var dtNav))
        {
            TempData["Error"] = "Parámetros de detalle inválidos o expirados.";
            return RedirectToAction(nameof(Index), new { t });
        }
        var tipoDocto = dtNav.GetValueOrDefault("tipoDocto") ?? string.Empty;
        if (!int.TryParse(dtNav.GetValueOrDefault("serie"), out var serie)) serie = 0;
        if (!long.TryParse(dtNav.GetValueOrDefault("numPed"), out var numPed)) numPed = 0;

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

        ViewBag.NavToken  = t;
        ViewBag.Dt        = dt;
        ViewBag.ReturnPage = page;

        EnsureNetworkShare(ObtenerCarpetaRaiz());
        ViewBag.ArchivosExistentes = ObtenerArchivosExistentes(items);

        return View("~/Views/Logistica/OrdenCompra/Detalle.cshtml", (orden, items));
    }

    // ── UPLOAD DE ARCHIVOS ─────────────────────────────────────────────────────

    [HttpPost("SubirArchivos")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubirArchivos(OrdenCompraUploadModel model)
    {
        // Desempaquetar dt para obtener tipoDocto/serie/numPed
        string tipoDocto = string.Empty; int serie = 0; long numPed = 0;
        if (!string.IsNullOrEmpty(model.Dt) && _navToken.TryUnprotect(model.Dt, out var dtNav))
        {
            tipoDocto = dtNav.GetValueOrDefault("tipoDocto") ?? string.Empty;
            int.TryParse(dtNav.GetValueOrDefault("serie"), out serie);
            long.TryParse(dtNav.GetValueOrDefault("numPed"), out numPed);
        }
        var redir = new { dt = model.Dt, t = model.ReturnBuscar, page = model.ReturnPage };

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

            var nombreSeguro = $"{Path.GetFileNameWithoutExtension(archivo.FileName)}_{numPed}_{DateTime.Now:yyyyMMdd}{ext}";
            nombreSeguro = string.Concat(nombreSeguro.Split(Path.GetInvalidFileNameChars()));
            var rutaDestino = Path.Combine(carpeta, nombreSeguro);
            await using var stream = new FileStream(rutaDestino, FileMode.Create);
            await archivo.CopyToAsync(stream);
            exitosos++;
        }

        if (exitosos > 0)
            await _service.ActualizarIdGrupoItemsAsync(tipoDocto, serie, numPed, model.SeleccionItems, idGrupo);

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
        string? dt = null,
        string? retNavToken = null, int retPage = 1)
    {
        string tipoDocto = string.Empty; int serie = 0; long numPed = 0;
        if (!string.IsNullOrEmpty(dt) && _navToken.TryUnprotect(dt, out var dtNav))
        {
            tipoDocto = dtNav.GetValueOrDefault("tipoDocto") ?? string.Empty;
            int.TryParse(dtNav.GetValueOrDefault("serie"), out serie);
            long.TryParse(dtNav.GetValueOrDefault("numPed"), out numPed);
        }
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

        return RedirectToAction(nameof(Detalle), new { dt, t = retNavToken, page = retPage });
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
        string? dt = null,
        string? retNavToken = null, int retPage = 1)
    {
        string tipoDocto = string.Empty; int serie = 0; long numPed = 0;
        if (!string.IsNullOrEmpty(dt) && _navToken.TryUnprotect(dt, out var dtNav))
        {
            tipoDocto = dtNav.GetValueOrDefault("tipoDocto") ?? string.Empty;
            int.TryParse(dtNav.GetValueOrDefault("serie"), out serie);
            long.TryParse(dtNav.GetValueOrDefault("numPed"), out numPed);
        }
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
        return RedirectToAction(nameof(Detalle), new { dt, t = retNavToken, page = retPage });
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
