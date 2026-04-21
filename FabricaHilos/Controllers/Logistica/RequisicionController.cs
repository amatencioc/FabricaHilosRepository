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
        // Aplicar mes actual como rango por defecto cuando no se envían fechas
        if (fechaInicio is null && fechaFin is null && string.IsNullOrWhiteSpace(buscar))
        {
            var hoy     = DateTime.Today;
            fechaInicio = new DateTime(hoy.Year, hoy.Month, 1);           // primer día del mes
            fechaFin    = new DateTime(hoy.Year, hoy.Month,
                              DateTime.DaysInMonth(hoy.Year, hoy.Month)); // último día del mes
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

        var codigosCc = items.Select(r => r.CentroCosto).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct()!;
        ViewBag.CentrosCosto = await _service.ObtenerDescripcionesCentroCostosAsync(codigosCc);

        // Progreso general: 4 etapas del flujo logístico
        var clavesReq = items.Select(r => (r.TipDoc!, r.Serie, r.NumReq));
        var progresoMap = await _service.ObtenerProgresoGeneralAsync(clavesReq);
        foreach (var item in items)
        {
            var key = $"{item.TipDoc}|{item.Serie}|{item.NumReq}";
            if (progresoMap.TryGetValue(key, out var pg))
            {
                item.ProgresoGeneral = pg;
                item.OrdenesCompra   = pg.OrdenesCompra;
            }
        }

        return View("~/Views/Logistica/Requerimiento/Index.cshtml", items);
    }

    // ── DETALLE (cabecera + ítems + upload) ────────────────────────────────────

    [HttpGet("Detalle/{tipDoc}/{serie:int}/{numReq:long}")]
    public async Task<IActionResult> Detalle(string tipDoc, int serie, long numReq,
        string? buscar = null, string? fechaInicio = null, string? fechaFin = null,
        string? estado = null, int page = 1)
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
                TipDoc           = tipDoc,
                Serie            = serie,
                NumReq           = numReq,
                ReturnBuscar      = buscar,
                ReturnFechaInicio = fechaInicio,
                ReturnFechaFin    = fechaFin,
                ReturnEstado      = estado,
                ReturnPage        = page,
            }
        };

        // Preservar filtros para "Volver al listado" y breadcrumb
        ViewBag.ReturnBuscar      = buscar;
        ViewBag.ReturnFechaInicio = fechaInicio;
        ViewBag.ReturnFechaFin    = fechaFin;
        ViewBag.ReturnEstado      = estado;
        ViewBag.ReturnPage        = page;

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

        var codigosCabCc = new[] { cabecera.CentroCosto }.Where(c => !string.IsNullOrWhiteSpace(c))!;
        ViewBag.CentrosCostoCab = await _service.ObtenerDescripcionesCentroCostosAsync(codigosCabCc);

        // Órdenes de compra distintas para mostrar en el detalle
        cabecera.OrdenesCompra = items
            .Where(i => !string.IsNullOrWhiteSpace(i.NroDocRef))
            .Select(i => i.NroDocRef!)
            .Distinct()
            .OrderBy(n => n)
            .ToList();

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
            return RedirectToAction(nameof(Detalle), new
            {
                tipDoc      = model.TipDoc,  serie  = model.Serie,  numReq = model.NumReq,
                buscar      = model.ReturnBuscar,      fechaInicio = model.ReturnFechaInicio,
                fechaFin    = model.ReturnFechaFin,    estado      = model.ReturnEstado,
                page        = model.ReturnPage
            });
        }

        if (model.OrdenesItems.Count == 0)
        {
            TempData["Warning"] = "Debe seleccionar al menos un ítem antes de adjuntar archivos.";
            return RedirectToAction(nameof(Detalle), new
            {
                tipDoc      = model.TipDoc,  serie  = model.Serie,  numReq = model.NumReq,
                buscar      = model.ReturnBuscar,      fechaInicio = model.ReturnFechaInicio,
                fechaFin    = model.ReturnFechaFin,    estado      = model.ReturnEstado,
                page        = model.ReturnPage
            });
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

            // Formato: {nombre_original}_{numReq}_{yyyyMMdd}{ext}
            var nombreSeguro = $"{Path.GetFileNameWithoutExtension(archivo.FileName)}_{model.NumReq}_{DateTime.Now:yyyyMMdd}{ext}";
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

        return RedirectToAction(nameof(Detalle), new
        {
            tipDoc      = model.TipDoc,  serie  = model.Serie,  numReq = model.NumReq,
            buscar      = model.ReturnBuscar,      fechaInicio = model.ReturnFechaInicio,
            fechaFin    = model.ReturnFechaFin,    estado      = model.ReturnEstado,
            page        = model.ReturnPage
        });
    }

    // ── VER ARCHIVO INLINE (visor) ─────────────────────────────────────────────

    [HttpGet("Ver/{idGrupo:long}/{nombreArchivo}")]
    public IActionResult Ver(long idGrupo, string nombreArchivo)
    {
        nombreArchivo = Path.GetFileName(nombreArchivo);
        var ruta = Path.Combine(ObtenerCarpetaPorGrupo(idGrupo), nombreArchivo);

        if (!System.IO.File.Exists(ruta))
            return NotFound();

        var contentType = ObtenerContentType(Path.GetExtension(nombreArchivo));
        // Sin fileDownloadName → Content-Disposition: inline (el navegador lo muestra)
        return PhysicalFile(ruta, contentType);
    }

    // ── APROBAR ARCHIVO ────────────────────────────────────────────────────────

    [HttpPost("AprobarArchivo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AprobarArchivo(
        long idGrupo, string nombreArchivo,
        string tipDoc, int serie, long numReq,
        string? retBuscar, string? retFechaInicio, string? retFechaFin,
        string? retEstado, int retPage = 1)
    {
        try
        {
            nombreArchivo = Path.GetFileName(nombreArchivo);
            var carpeta = ObtenerCarpetaPorGrupo(idGrupo);
            bool aprobacionValida = false;

            if (nombreArchivo.StartsWith("APROBADO_", StringComparison.OrdinalIgnoreCase))
            {
                // Ya tiene el prefijo, verificar que el archivo existe en disco
                aprobacionValida = System.IO.File.Exists(Path.Combine(carpeta, nombreArchivo));
            }
            else
            {
                var rutaOriginal = Path.Combine(carpeta, nombreArchivo);
                var nuevoNombre  = $"APROBADO_{nombreArchivo}";
                var rutaNueva    = Path.Combine(carpeta, nuevoNombre);

                if (System.IO.File.Exists(rutaOriginal))
                {
                    System.IO.File.Move(rutaOriginal, rutaNueva);
                    aprobacionValida = true;
                }
            }

            if (aprobacionValida)
            {
                await _service.AprobarGrupoAsync(idGrupo);
                TempData["Success"] = "Archivo aprobado correctamente.";
            }
            else
            {
                TempData["Error"] = "No se encontró el archivo para aprobar. Verifique que el archivo exista.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al aprobar archivo del grupo {IdGrupo}", idGrupo);
            TempData["Error"] = $"Error al aprobar el archivo: {ex.Message}";
        }

        return RedirectToAction(nameof(Detalle), new
        {
            tipDoc,
            serie,
            numReq,
            buscar      = retBuscar,
            fechaInicio = retFechaInicio,
            fechaFin    = retFechaFin,
            estado      = retEstado,
            page        = retPage
        });
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
    public async Task<IActionResult> EliminarArchivo(long idGrupo, string nombreArchivo,
        string tipDoc, int serie, long numReq,
        string? retBuscar = null, string? retFechaInicio = null, string? retFechaFin = null,
        string? retEstado = null, int retPage = 1)
    {
        nombreArchivo = Path.GetFileName(nombreArchivo);
        bool eraAprobado = nombreArchivo.StartsWith("APROBADO_", StringComparison.OrdinalIgnoreCase);
        var carpeta = ObtenerCarpetaPorGrupo(idGrupo);
        var ruta    = Path.Combine(carpeta, nombreArchivo);

        if (System.IO.File.Exists(ruta))
            System.IO.File.Delete(ruta);

        // Si se eliminó el archivo de aprobación → limpiar F_APROBADO en BD
        if (eraAprobado)
            await _service.DesaprobarGrupoAsync(idGrupo);

        // Si la carpeta de este grupo quedó sin archivos → limpiar ID_GRUPO + F_APROBADO y eliminar carpeta
        bool carpetaVacia = !Directory.Exists(carpeta) || !Directory.EnumerateFiles(carpeta).Any();
        if (carpetaVacia)
        {
            await _service.LimpiarIdGrupoAsync(idGrupo);
            if (Directory.Exists(carpeta))
                Directory.Delete(carpeta, recursive: false);
        }

        TempData["Success"] = $"Archivo '{nombreArchivo}' eliminado.";
        return RedirectToAction(nameof(Detalle), new
        {
            tipDoc, serie, numReq,
            buscar      = retBuscar,
            fechaInicio = retFechaInicio,
            fechaFin    = retFechaFin,
            estado      = retEstado,
            page        = retPage
        });
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
