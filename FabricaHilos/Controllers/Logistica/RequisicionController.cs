using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FabricaHilos.Models.Logistica;
using FabricaHilos.Services.Logistica;

namespace FabricaHilos.Controllers.Logistica;

[Authorize]
[Route("Logistica/Requerimiento")]
public class RequisicionController : OracleBaseController
{
    private readonly IRequisicionService _service;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;
    private readonly ILogger<RequisicionController> _logger;

    // Extensiones permitidas para la carga de archivos
    private static readonly HashSet<string> _extPermitidas =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx",
            ".jpg", ".jpeg", ".png", ".gif", ".bmp",
            ".txt", ".zip", ".rar"
        };

    public RequisicionController(
        IRequisicionService service,
        IWebHostEnvironment env,
        IConfiguration config,
        ILogger<RequisicionController> logger)
    {
        _service = service;
        _env     = env;
        _config  = config;
        _logger  = logger;
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
        // Aplicar semana actual como rango por defecto cuando no se envían fechas
        if (fechaInicio is null && fechaFin is null && string.IsNullOrWhiteSpace(buscar))
        {
            var hoy       = DateTime.Today;
            int diasDesde = hoy.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)hoy.DayOfWeek - 1;
            fechaInicio   = hoy.AddDays(-diasDesde);          // lunes de la semana actual
            fechaFin      = fechaInicio.Value.AddDays(6);     // domingo de la semana actual
        }
        const int pageSize = 10;
        var (items, total) = await _service.ObtenerRequisicionesAsync(
            buscar, fechaInicio, fechaFin, estado, page, pageSize);

        ViewBag.Buscar      = buscar;
        ViewBag.FechaInicio = fechaInicio?.ToString("yyyy-MM-dd");
        ViewBag.FechaFin    = fechaFin?.ToString("yyyy-MM-dd");
        ViewBag.Estado      = estado;
        ViewBag.Page        = page;
        ViewBag.PageSize    = pageSize;
        ViewBag.TotalCount  = total;
        ViewBag.TotalPages  = (int)Math.Ceiling(total / (double)pageSize);

        var codigos = items
            .SelectMany(r => new[] { r.Responsable, r.Autoriza })
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()!;
        ViewBag.Nombres = await _service.ObtenerNombresPersonalAsync(codigos);

        var codigosPrio = items.Select(r => r.Prioridad).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct()!;
        ViewBag.Prioridades = await _service.ObtenerDescripcionesTablaAuxiliarAsync("70", codigosPrio);

        return View("~/Views/Logistica/Requerimiento/Index.cshtml", items);
    }

    // ── DETALLE (cabecera + ítems + upload) ────────────────────────────────────

    [HttpGet("Detalle/{tipDoc}/{serie:int}/{numReq:long}")]
    public async Task<IActionResult> Detalle(string tipDoc, int serie, long numReq)
    {
        var cabecera = await _service.ObtenerRequisicionAsync(tipDoc, serie, numReq);
        if (cabecera is null)
        {
            TempData["Warning"] = $"No se encontró el requerimiento {tipDoc}-{serie:D3}-{numReq}.";
            return RedirectToAction(nameof(Index));
        }

        var items = await _service.ObtenerItemsAsync(tipDoc, serie, numReq);

        var vm = new RequisicionDetalleViewModel
        {
            Cabecera = cabecera,
            Items    = items,
            Upload   = new RequisicionUploadModel
            {
                TipDoc = tipDoc,
                Serie  = serie,
                NumReq = numReq,
            }
        };

        // Archivos ya cargados para este requerimiento (por idGrupo de los ítems)
        ViewBag.ArchivosExistentes = ObtenerArchivosExistentes(items);

        var codigosPersonal = new[] { cabecera.Responsable, cabecera.Autoriza }
            .Concat(items.Select(i => i.CodSolicita))
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()!;
        ViewBag.Nombres = await _service.ObtenerNombresPersonalAsync(codigosPersonal);

        var codigosArt = items.Select(i => i.CodArt).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct()!;
        ViewBag.DescripcionesArticulos = await _service.ObtenerDescripcionesArticulosAsync(codigosArt);

        ViewBag.Destinos   = await _service.ObtenerDescripcionesTablaAuxiliarAsync("85",
            new[] { cabecera.Destino }.Where(c => !string.IsNullOrWhiteSpace(c))!);
        ViewBag.Prioridades = await _service.ObtenerDescripcionesTablaAuxiliarAsync("70",
            new[] { cabecera.Prioridad }.Where(c => !string.IsNullOrWhiteSpace(c))!);

        var codigosCc = items.Select(i => i.Destino).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct()!;
        ViewBag.DestinosItem = await _service.ObtenerDescripcionesCentroCostosAsync(codigosCc);

        return View("~/Views/Logistica/Requerimiento/Detalle.cshtml", vm);
    }

    // ── UPLOAD DE ARCHIVOS ─────────────────────────────────────────────────────

    [HttpPost("SubirArchivos")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubirArchivos(RequisicionUploadModel model)
    {
        if (model.Archivos == null || model.Archivos.Count == 0)
        {
            TempData["Warning"] = "No se seleccionaron archivos.";
            return RedirectToAction(nameof(Detalle), new { tipDoc = model.TipDoc, serie = model.Serie, numReq = model.NumReq });
        }

        if (model.OrdenesItems.Count == 0)
        {
            TempData["Warning"] = "Debe seleccionar al menos un ítem antes de adjuntar archivos.";
            return RedirectToAction(nameof(Detalle), new { tipDoc = model.TipDoc, serie = model.Serie, numReq = model.NumReq });
        }

        // Si los ítems seleccionados ya comparten un grupo, reutilizarlo; si no, generar uno nuevo
        long idGrupo = (model.ExistingIdGrupo.HasValue && model.ExistingIdGrupo.Value > 0)
            ? model.ExistingIdGrupo.Value
            : await _service.ObtenerSiguienteIdGrupoAsync();
        string carpeta = ObtenerCarpetaPorGrupo(idGrupo);
        Directory.CreateDirectory(carpeta);

        var errores  = new List<string>();
        var exitosos = 0;

        foreach (var archivo in model.Archivos)
        {
            if (archivo.Length == 0) continue;

            var ext = Path.GetExtension(archivo.FileName);
            if (!_extPermitidas.Contains(ext))
            {
                errores.Add($"Extensión no permitida: {archivo.FileName}");
                continue;
            }

            var nombreSeguro = $"{Path.GetFileNameWithoutExtension(archivo.FileName)}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
            nombreSeguro = string.Concat(nombreSeguro.Split(Path.GetInvalidFileNameChars()));

            var rutaDestino = Path.Combine(carpeta, nombreSeguro);
            await using var stream = new FileStream(rutaDestino, FileMode.Create);
            await archivo.CopyToAsync(stream);
            exitosos++;
        }

        if (exitosos > 0)
            await _service.ActualizarIdGrupoItemsAsync(model.TipDoc!, model.Serie, model.NumReq, model.OrdenesItems, idGrupo);

        if (errores.Count > 0)
            TempData["Warning"] = $"Se cargaron {exitosos} archivo(s). Errores: {string.Join("; ", errores)}";
        else
            TempData["Success"] = $"Se cargaron {exitosos} archivo(s) en el grupo {idGrupo}. {model.OrdenesItems.Count} ítem(s) actualizados.";

        return RedirectToAction(nameof(Detalle), new { tipDoc = model.TipDoc, serie = model.Serie, numReq = model.NumReq });
    }

    // ── DESCARGAR ARCHIVO ──────────────────────────────────────────────────────

    [HttpGet("Descargar/{idGrupo:long}/{nombreArchivo}")]
    public IActionResult Descargar(long idGrupo, string nombreArchivo)
    {
        nombreArchivo = Path.GetFileName(nombreArchivo);
        var ruta = Path.Combine(ObtenerCarpetaPorGrupo(idGrupo), nombreArchivo);

        if (!System.IO.File.Exists(ruta))
            return NotFound();

        var contentType = ObtenerContentType(Path.GetExtension(nombreArchivo));
        return PhysicalFile(ruta, contentType, nombreArchivo);
    }

    // ── ELIMINAR ARCHIVO ───────────────────────────────────────────────────────

    [HttpPost("EliminarArchivo")]
    [ValidateAntiForgeryToken]
    public IActionResult EliminarArchivo(long idGrupo, string nombreArchivo)
    {
        nombreArchivo = Path.GetFileName(nombreArchivo);
        var ruta = Path.Combine(ObtenerCarpetaPorGrupo(idGrupo), nombreArchivo);

        if (System.IO.File.Exists(ruta))
            System.IO.File.Delete(ruta);

        TempData["Success"] = $"Archivo '{nombreArchivo}' eliminado.";
        return Redirect(Request.Headers["Referer"].ToString());
    }

    // ── HELPERS ────────────────────────────────────────────────────────────────

    private string ObtenerCarpetaRaiz()
    {
        return _config["RutaRequerimientos"]
               ?? Path.Combine(_env.WebRootPath, "uploads", "requerimientos");
    }

    private string ObtenerCarpetaPorGrupo(long idGrupo)
        => Path.Combine(ObtenerCarpetaRaiz(), idGrupo.ToString());

    private List<ArchivoRequisicionDto> ObtenerArchivosExistentes(IEnumerable<ItemReqDto> items)
    {
        var grupos = items
            .Where(i => i.IdGrupo.HasValue)
            .GroupBy(i => i.IdGrupo!.Value)
            .ToList();

        var resultado = new List<ArchivoRequisicionDto>();

        foreach (var grupo in grupos)
        {
            var carpeta = ObtenerCarpetaPorGrupo(grupo.Key);
            if (!Directory.Exists(carpeta)) continue;

            var archivos = Directory.GetFiles(carpeta)
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTime)
                .Select(f => new ArchivoRequisicionDto
                {
                    NombreArchivo = f.Name,
                    RutaRelativa  = f.FullName,
                    TamanioBytes  = f.Length,
                    FechaCarga    = f.CreationTime,
                    IdGrupo       = grupo.Key,
                    CarpetaGrupo  = carpeta
                });

            resultado.AddRange(archivos);
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
