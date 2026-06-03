using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FabricaHilos.Models.Sgc;
using FabricaHilos.Services;
using FabricaHilos.Services.Sgc.AnalisisReclamo;

namespace FabricaHilos.Controllers.Sgc;

[Authorize]
[Route("Sgc/Reclamos")]
public class AnalisisReclamoController : OracleBaseController
{
    private readonly IAnalisisReclamoService _service;
    private readonly IWebHostEnvironment     _env;
    private readonly IConfiguration          _config;
    private readonly ILogger<AnalisisReclamoController> _logger;
    private readonly IMenuService            _menuService;

    // ── Extensiones de archivo permitidas (sin restricción de tipo) ──────────
    //    Se aceptan todos los tipos: foto, video, audio, PDF, correo (.eml),
    //    documentos de Office, ZIP, etc.
    private static readonly HashSet<string> _extPermitidas =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Documentos
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
            ".txt", ".csv", ".rtf",
            // Imágenes
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".heic", ".heif",
            // Video
            ".mp4", ".mov", ".avi", ".mkv", ".webm", ".3gp",
            // Audio
            ".mp3", ".wav", ".ogg", ".m4a", ".aac",
            // Correo
            ".eml", ".msg",
            // Comprimidos
            ".zip", ".rar", ".7z"
        };

    // Tamaño máximo por archivo: 100 MB
    private const long MaxBytesPerFile = 100 * 1024 * 1024;

    public AnalisisReclamoController(
        IAnalisisReclamoService service,
        IWebHostEnvironment     env,
        IConfiguration          config,
        ILogger<AnalisisReclamoController> logger,
        IMenuService            menuService)
    {
        _service     = service;
        _env         = env;
        _config      = config;
        _logger      = logger;
        _menuService = menuService;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  LISTADO
    // ════════════════════════════════════════════════════════════════════════

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(string? buscar, string? estado)
    {
        var reclamos = await _service.ObtenerReclamosAsync(buscar, estado);

        // El analista no ve reclamos en estado '01' (aún no enviados a Calidad)
        var accesoModulo = _menuService.ObtenerAccesoModulo("SgcAnalisisReclamo");
        bool esAnalista = accesoModulo.TieneModificador("AC")
                       || (!accesoModulo.TieneModificador("VD")
                           && !accesoModulo.TieneModificador("GE")
                           && !accesoModulo.TieneModificador("OB")
                           && _menuService.ObtenerAccesoModulo("Sgc").TieneModificador("AC"));
        if (esAnalista)
            reclamos = reclamos.Where(r => r.Estado != "01").ToList();

        ViewBag.Buscar = buscar;
        ViewBag.Estado = estado;

        return View("~/Views/Sgc/AnalisisReclamo/Index.cshtml", reclamos);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  NUEVO RECLAMO
    // ════════════════════════════════════════════════════════════════════════

    [HttpGet("Nuevo")]
    public async Task<IActionResult> Nuevo()
    {
        ViewBag.Clientes = await _service.ObtenerClientesAsync();
        return View("~/Views/Sgc/AnalisisReclamo/Nuevo.cshtml", new CrearReclamoRequest());
    }

    [HttpPost("Nuevo")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(524_288_000)]           // 500 MB máximo por request
    [RequestFormLimits(MultipartBodyLengthLimit = 524_288_000)]
    public async Task<IActionResult> Nuevo(CrearReclamoRequest model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Clientes = await _service.ObtenerClientesAsync();
            return View("~/Views/Sgc/AnalisisReclamo/Nuevo.cshtml", model);
        }

        var usuario = User.Identity?.Name ?? "SYS";

        // Crear reclamo en BD
        var (idReclamo, error) = await _service.CrearReclamoAsync(model, usuario);
        if (error != null)
        {
            ModelState.AddModelError("", error);
            ViewBag.Clientes = await _service.ObtenerClientesAsync();
            return View("~/Views/Sgc/AnalisisReclamo/Nuevo.cshtml", model);
        }

        // Subir archivos del vendedor (si hay)
        if (model.Archivos != null && model.Archivos.Count > 0)
        {
            var (exitosos, errores) = await GuardarArchivosAsync(model.Archivos, idReclamo, "VD", usuario);
            if (errores.Count > 0)
                TempData["Warning"] = $"Reclamo creado. {exitosos} archivo(s) subido(s). Errores: {string.Join("; ", errores)}";
        }

        TempData["Success"] = $"Reclamo #{idReclamo} creado correctamente.";
        return RedirectToAction(nameof(Detalle), new { id = idReclamo });
    }

    // ════════════════════════════════════════════════════════════════════════
    //  DETALLE
    // ════════════════════════════════════════════════════════════════════════

    [HttpGet("Detalle/{id:long}")]
    public async Task<IActionResult> Detalle(long id)
    {
        var reclamo = await _service.ObtenerReclamoAsync(id);
        if (reclamo is null)
        {
            TempData["Error"] = $"El reclamo #{id} no existe.";
            return RedirectToAction(nameof(Index));
        }

        var descargos = await _service.ObtenerDescargosAsync(id);
        var archivos  = await _service.ObtenerArchivosAsync(id);

        var usuario = User.Identity?.Name ?? "";

        // Determinar rol del usuario según su token de ACCESO_WEB:
        //   Prioridad: SgcAnalisisReclamo[VD/AC/GE/OB] > Sgc[OB] > fallback OB
        //   GE (Gerencia) = solo lectura + puede aprobar/rechazar cuando estado=03
        //   Si el reclamo ya está finalizado → siempre OB (solo lectura)
        string rolUsuario;
        if (reclamo.EsFinalizado)
        {
            rolUsuario = "OB";
        }
        else
        {
            // Leer el modificador del token específico SgcAnalisisReclamo[XX]
            var accesoModulo = _menuService.ObtenerAccesoModulo("SgcAnalisisReclamo");
            if (accesoModulo.TieneModificador("VD"))
                rolUsuario = "VD";
            else if (accesoModulo.TieneModificador("AC"))
                rolUsuario = "AC";
            else if (accesoModulo.TieneModificador("GE"))
                rolUsuario = "GE";
            else if (accesoModulo.TieneModificador("OB"))
                rolUsuario = "OB";
            else
            {
                // No tiene token específico: leer modificador del token padre Sgc
                var accesoSgc = _menuService.ObtenerAccesoModulo("Sgc");
                if (accesoSgc.TieneModificador("VD"))
                    rolUsuario = "VD";
                else if (accesoSgc.TieneModificador("AC"))
                    rolUsuario = "AC";
                else if (accesoSgc.TieneModificador("GE"))
                    rolUsuario = "GE";
                // Sin modificador explícito: si el usuario es el vendedor que creó el
                // reclamo lo tratamos como VD (puede editar su propio reclamo).
                // Si no, solo lectura.
                else if (string.Equals(usuario, reclamo.UsuVendedor, StringComparison.OrdinalIgnoreCase))
                    rolUsuario = "VD";
                else
                    rolUsuario = "OB";
            }
        }

        var vm = new ReclamoDetalleVm
        {
            Reclamo     = reclamo,
            Descargos   = descargos,
            Archivos    = archivos,
            RolUsuario  = rolUsuario
        };

        // El analista solo puede ver reclamos que el vendedor ya envió a Calidad (estado >= '02')
        if (rolUsuario == "AC" && reclamo.Estado == "01")
        {
            TempData["Error"] = $"El reclamo #{id} aún no ha sido enviado a Calidad.";
            return RedirectToAction(nameof(Index));
        }

        return View("~/Views/Sgc/AnalisisReclamo/Detalle.cshtml", vm);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  AGREGAR DESCARGO
    // ════════════════════════════════════════════════════════════════════════

    [HttpPost("AgregarDescargo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AgregarDescargo(AgregarDescargoRequest model)
    {
        var usuario = User.Identity?.Name ?? "SYS";

        if (string.IsNullOrWhiteSpace(model.Descripcion))
        {
            TempData["Error"] = "El descargo no puede estar vacío.";
            return RedirectToAction(nameof(Detalle), new { id = model.IdReclamo });
        }

        var (_, error) = await _service.AgregarDescargoAsync(
            model.IdReclamo, model.Rol, model.Descripcion, usuario);

        if (error != null)
            TempData["Error"] = error;
        else
            TempData["Success"] = "Descargo agregado correctamente.";

        return RedirectToAction(nameof(Detalle), new { id = model.IdReclamo });
    }

    // ════════════════════════════════════════════════════════════════════════
    //  SUBIR ARCHIVOS
    // ════════════════════════════════════════════════════════════════════════

    [HttpPost("SubirArchivos")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(524_288_000)]           // 500 MB máximo por request
    [RequestFormLimits(MultipartBodyLengthLimit = 524_288_000)]
    public async Task<IActionResult> SubirArchivos(SubirArchivosReclamoRequest model)
    {
        var usuario = User.Identity?.Name ?? "SYS";

        if (model.Archivos == null || model.Archivos.Count == 0)
        {
            TempData["Warning"] = "No se seleccionaron archivos.";
            return RedirectToAction(nameof(Detalle), new { id = model.IdReclamo });
        }

        var (exitosos, errores) = await GuardarArchivosAsync(model.Archivos, model.IdReclamo, model.Rol, usuario);

        if (errores.Count > 0)
            TempData["Warning"] = $"{exitosos} archivo(s) subido(s). Errores: {string.Join("; ", errores)}";
        else
            TempData["Success"] = $"{exitosos} archivo(s) subido(s) correctamente.";

        return RedirectToAction(nameof(Detalle), new { id = model.IdReclamo });
    }

    // ════════════════════════════════════════════════════════════════════════
    //  VER ARCHIVO (inline / descarga)
    // ════════════════════════════════════════════════════════════════════════

    [HttpGet("VerArchivo/{idArchivo:long}")]
    public async Task<IActionResult> VerArchivo(long idArchivo, bool descargar = false)
    {
        var archivo = await _service.ObtenerArchivoAsync(idArchivo);
        if (archivo is null) return NotFound();

        var ruta = ObtenerRutaFisica(archivo.IdReclamo, archivo.NombreServer);
        EnsureNetworkShare(ObtenerCarpetaBase());
        if (!System.IO.File.Exists(ruta)) return NotFound("El archivo no está disponible en el servidor.");

        var mime  = string.IsNullOrWhiteSpace(archivo.MimeType) ? "application/octet-stream" : archivo.MimeType;
        var disp  = descargar ? "attachment" : "inline";

        // RFC 6266: filename* con UTF-8 percent-encoding para nombres con caracteres no-ASCII
        // filename= (ASCII fallback) + filename*= (UTF-8 completo)
        var nombreAscii  = RemoverNoAscii(archivo.NombreOrig);
        var nombreEnc    = Uri.EscapeDataString(archivo.NombreOrig);
        Response.Headers["Content-Disposition"] =
            $"{disp}; filename=\"{nombreAscii}\"; filename*=UTF-8''{nombreEnc}";
        return PhysicalFile(ruta, mime);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  ELIMINAR ARCHIVO
    // ════════════════════════════════════════════════════════════════════════

    [HttpPost("EliminarArchivo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarArchivo(long idArchivo, long idReclamo)
    {
        var usuario = User.Identity?.Name ?? "SYS";

        // Obtener datos del archivo antes de eliminar de BD
        var archivo = await _service.ObtenerArchivoAsync(idArchivo);
        if (archivo is null)
        {
            TempData["Error"] = "El archivo no existe.";
            return RedirectToAction(nameof(Detalle), new { id = idReclamo });
        }

        // Eliminar registro en BD
        var error = await _service.EliminarArchivoAsync(idArchivo, usuario);
        if (error != null)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Detalle), new { id = idReclamo });
        }

        // Eliminar archivo físico
        var ruta = ObtenerRutaFisica(archivo.IdReclamo, archivo.NombreServer);
        EnsureNetworkShare(ObtenerCarpetaBase());
        if (System.IO.File.Exists(ruta))
        {
            try { System.IO.File.Delete(ruta); }
            catch (Exception ex) { _logger.LogWarning(ex, "No se pudo eliminar el archivo físico: {Ruta}", ruta); }
        }

        TempData["Success"] = $"Archivo '{archivo.NombreOrig}' eliminado.";
        return RedirectToAction(nameof(Detalle), new { id = idReclamo });
    }

    // ════════════════════════════════════════════════════════════════════════
    //  CAMBIAR ESTADO
    // ════════════════════════════════════════════════════════════════════════

    [HttpPost("CambiarEstado")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(long idReclamo, string estado)
    {
        var usuario = User.Identity?.Name ?? "SYS";
        var error   = await _service.CambiarEstadoAsync(idReclamo, estado, usuario);

        if (error != null)
            TempData["Error"] = error;
        else
            TempData["Success"] = "Estado actualizado correctamente.";

        return RedirectToAction(nameof(Detalle), new { id = idReclamo });
    }

    // ════════════════════════════════════════════════════════════════════════
    //  ESCALAR A GERENCIA  (Analista → '03')
    // ════════════════════════════════════════════════════════════════════════

    [HttpPost("EscalarGerencia")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EscalarGerencia(EscalarGerenciaRequest model)
    {
        var usuario = User.Identity?.Name ?? "SYS";
        var error   = await _service.EscalarGerenciaAsync(model.IdReclamo, usuario);

        if (error != null)
            TempData["Error"] = error;
        else
            TempData["Success"] = "Reclamo escalado a Gerencia para aprobación.";

        return RedirectToAction(nameof(Detalle), new { id = model.IdReclamo });
    }

    // ════════════════════════════════════════════════════════════════════════
    //  APROBAR RECLAMO  (Gerente → '04')
    // ════════════════════════════════════════════════════════════════════════

    [HttpPost("Aprobar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Aprobar(AprobarReclamoRequest model)
    {
        var usuario = User.Identity?.Name ?? "SYS";
        var error   = await _service.AprobarReclamoAsync(model.IdReclamo, model.Observacion, usuario);

        if (error != null)
            TempData["Error"] = error;
        else
            TempData["Success"] = "Reclamo APROBADO correctamente.";

        return RedirectToAction(nameof(Detalle), new { id = model.IdReclamo });
    }

    // ════════════════════════════════════════════════════════════════════════
    //  RECHAZAR RECLAMO  (Gerente → '05')
    // ════════════════════════════════════════════════════════════════════════

    [HttpPost("Rechazar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rechazar(RechazarReclamoRequest model)
    {
        var usuario = User.Identity?.Name ?? "SYS";

        if (string.IsNullOrWhiteSpace(model.Motivo))
        {
            TempData["Error"] = "Debe ingresar el motivo del rechazo.";
            return RedirectToAction(nameof(Detalle), new { id = model.IdReclamo });
        }

        var error = await _service.RechazarReclamoAsync(model.IdReclamo, model.Motivo, usuario);

        if (error != null)
            TempData["Error"] = error;
        else
            TempData["Success"] = "Reclamo RECHAZADO.";

        return RedirectToAction(nameof(Detalle), new { id = model.IdReclamo });
    }

    // ════════════════════════════════════════════════════════════════════════
    //  ELIMINAR RECLAMO (solo para pruebas)
    // ════════════════════════════════════════════════════════════════════════

    [HttpPost("EliminarReclamo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarReclamo(long idReclamo)
    {
        var usuario = User.Identity?.Name ?? "SYS";

        var (_, error) = await _service.EliminarReclamoAsync(idReclamo, usuario);

        if (error != null)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Index));
        }

        // Borrar carpeta física del reclamo (ignorar si no existe)
        try
        {
            var carpeta = ObtenerCarpetaReclamo(idReclamo);
            if (Directory.Exists(carpeta))
                Directory.Delete(carpeta, recursive: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo borrar la carpeta física del reclamo {Id}", idReclamo);
        }

        TempData["Success"] = $"Reclamo #{idReclamo} eliminado correctamente.";
        return RedirectToAction(nameof(Index));
    }

    // ════════════════════════════════════════════════════════════════════════
    //  BÚSQUEDA DE CLIENTES (AJAX)
    // ════════════════════════════════════════════════════════════════════════

    [HttpGet("BuscarClientes")]
    public async Task<IActionResult> BuscarClientes(string? q)
    {
        var clientes = await _service.ObtenerClientesAsync(q);
        return Json(clientes);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  HELPERS
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Guarda los archivos en disco y registra cada uno en BD.
    /// Retorna (exitosos, listaDeMensajesDeError).
    /// </summary>
    private async Task<(int Exitosos, List<string> Errores)> GuardarArchivosAsync(
        IEnumerable<IFormFile> archivos, long idReclamo, string rol, string usuario)
    {
        var carpeta = ObtenerCarpetaReclamo(idReclamo);
        EnsureNetworkShare(ObtenerCarpetaBase());
        Directory.CreateDirectory(carpeta);

        int         exitosos = 0;
        var         errores  = new List<string>();

        foreach (var archivo in archivos)
        {
            if (archivo.Length == 0) continue;

            // Validar tamaño
            if (archivo.Length > MaxBytesPerFile)
            {
                errores.Add($"{archivo.FileName}: supera el límite de 100 MB.");
                continue;
            }

            // Validar extensión
            var ext = Path.GetExtension(archivo.FileName);
            if (!_extPermitidas.Contains(ext))
            {
                errores.Add($"{archivo.FileName}: extensión '{ext}' no permitida.");
                continue;
            }

            // Nombre seguro en servidor: GUID + extensión (evita colisiones y traversal)
            var nombreServer = $"{Guid.NewGuid():N}{ext}";
            var rutaDest     = Path.Combine(carpeta, nombreServer);

            try
            {
                await using var stream = new FileStream(rutaDest, FileMode.Create, FileAccess.Write, FileShare.None);
                await archivo.CopyToAsync(stream);

                var (_, errBD) = await _service.RegistrarArchivoAsync(
                    idReclamo, rol,
                    archivo.FileName,
                    nombreServer,
                    archivo.ContentType,
                    archivo.Length,
                    usuario);

                if (errBD != null)
                {
                    // Limpiar el archivo físico si falló el registro en BD
                    System.IO.File.Delete(rutaDest);
                    errores.Add($"{archivo.FileName}: {errBD}");
                }
                else
                {
                    exitosos++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar archivo {Nombre}", archivo.FileName);
                errores.Add($"{archivo.FileName}: error interno al guardar.");
            }
        }

        return (exitosos, errores);
    }

    /// <summary>Carpeta base configurada o por defecto en wwwroot/uploads/reclamoscalidad.</summary>
    private string ObtenerCarpetaBase()
    {
        return _config["RutaAnalisisReclamo"]
               ?? Path.Combine(_env.WebRootPath, "uploads", "reclamoscalidad");
    }

    /// <summary>Carpeta específica del reclamo: {base}/{idReclamo}/</summary>
    private string ObtenerCarpetaReclamo(long idReclamo)
        => Path.Combine(ObtenerCarpetaBase(), idReclamo.ToString());

    /// <summary>Ruta física completa de un archivo.</summary>
    private string ObtenerRutaFisica(long idReclamo, string nombreServer)
        => Path.Combine(ObtenerCarpetaReclamo(idReclamo), nombreServer);

    /// <summary>
    /// Fallback ASCII para el parámetro filename= del header Content-Disposition.
    /// Reemplaza caracteres no imprimibles / no-ASCII por '_' y elimina comillas.
    /// </summary>
    private static string RemoverNoAscii(string nombre)
    {
        var sb = new System.Text.StringBuilder(nombre.Length);
        foreach (var c in nombre)
        {
            if (c == '"' || c == '\\') continue;
            sb.Append(c < 0x20 || c > 0x7E ? '_' : c);
        }
        return sb.Length == 0 ? "archivo" : sb.ToString();
    }
}
