using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FabricaHilos.Services.Contabilidad;
using FabricaHilos.Models.Contabilidad;
using FabricaHilos.Services;

namespace FabricaHilos.Controllers.Contabilidad;

[Authorize]
[Route("Contabilidad/ActivoFijo")]
public class ActivoFijoController : OracleBaseController
{
    private readonly IActivoFijoService              _service;
    private readonly IWebHostEnvironment             _env;
    private readonly IConfiguration                 _config;
    private readonly ILogger<ActivoFijoController>  _logger;
    private readonly IEmpresaTemaService             _empresaTema;
    private readonly INavTokenService                _navToken;
    private readonly ProcesadorImagenActivoFijo      _procesadorImagen;

    public ActivoFijoController(
        IActivoFijoService              service,
        IWebHostEnvironment             env,
        IConfiguration                 config,
        ILogger<ActivoFijoController>  logger,
        IEmpresaTemaService             empresaTema,
        INavTokenService                navToken,
        ProcesadorImagenActivoFijo      procesadorImagen)
    {
        _service          = service;
        _env              = env;
        _config           = config;
        _logger           = logger;
        _empresaTema      = empresaTema;
        _navToken         = navToken;
        _procesadorImagen = procesadorImagen;
    }

    // ── LISTADO ────────────────────────────────────────────────────────────────

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(
        string? t       = null,
        string? buscar  = null,
        string? clase   = null,
        string? estado  = null,
        int     page    = 1)
    {
        // Si hay filtros nuevos sin token, crear token y redirigir
        if (string.IsNullOrEmpty(t) && (buscar != null || clase != null || estado != null))
        {
            var token = _navToken.Protect(new Dictionary<string, string?>
            {
                ["buscar"] = buscar,
                ["clase"]  = clase,
                ["estado"] = estado
            });
            return RedirectToAction(nameof(Index), new { t = token, page });
        }

        // Desempaquetar token
        if (!string.IsNullOrEmpty(t) && _navToken.TryUnprotect(t, out var nav))
        {
            buscar = nav.GetValueOrDefault("buscar");
            clase  = nav.GetValueOrDefault("clase");
            estado = nav.GetValueOrDefault("estado");
        }

        // Carga inicial sin token → mostrar activos por defecto (estado "0").
        // Si el usuario elige "— Todos los estados —" (value=""), el form envía estado="",
        // que el controller recibe como string.Empty; lo normalizamos a null para que
        // el WHERE dinámico no incluya filtro de estado.
        if (estado is null && string.IsNullOrEmpty(t))
            estado = "0";                          // default visual + funcional: activos
        else if (estado == string.Empty)
            estado = null;                         // "Todos" → sin filtro en el WHERE

        const int pageSize = 25;

        // Cuando el usuario escribe en el buscador, se ignoran clase y estado
        // para que la búsqueda por código/descripción/marca/serie sea global.
        string? claseQuery  = string.IsNullOrWhiteSpace(buscar) ? clase  : null;
        string? estadoQuery = string.IsNullOrWhiteSpace(buscar) ? estado : null;

        var (items, total) = await _service.ObtenerActivosAsync(buscar, claseQuery, estadoQuery, page, pageSize);
        var clases = await _service.ObtenerClasesAsync();

        // Resolver proveedores
        var codProv = items.Select(a => a.CodProveed)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!)
            .Distinct();
        var proveedores = await _service.ObtenerNombresProveedoresAsync(codProv);

        // Resolver descripciones de centros de costo
        var codCC = items.Select(a => a.CCosto)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!)
            .Distinct();
        var ccostos = await _service.ObtenerDescripcionesCCostosAsync(codCC);

        // Generar NavToken final para paginación
        var navToken = _navToken.Protect(new Dictionary<string, string?>
        {
            ["buscar"] = buscar,
            ["clase"]  = clase,
            ["estado"] = estado
        });

        ViewBag.Buscar       = buscar;
        ViewBag.Clase        = clase;
        ViewBag.Estado       = estado;
        ViewBag.NavToken     = navToken;
        ViewBag.Page         = page;
        ViewBag.PageSize     = pageSize;
        ViewBag.TotalCount   = total;
        ViewBag.TotalPages   = (int)Math.Ceiling(total / (double)pageSize);
        ViewBag.Clases       = clases;
        ViewBag.Proveedores  = proveedores;
        ViewBag.CCostos      = ccostos;

        return View("~/Views/Contabilidad/ActivoFijo/Index.cshtml", items);
    }

    // ── EDITAR ─────────────────────────────────────────────────────────────────

    [HttpGet("Editar")]
    public async Task<IActionResult> Editar(
        string clase, string codigo, int numero = 0,
        string? t = null)
    {
        var activo = await _service.ObtenerActivoAsync(clase, codigo, numero);
        if (activo == null)
        {
            TempData["Error"] = $"Activo {codigo} no encontrado.";
            return RedirectToAction(nameof(Index), new { t });
        }

        // Obtener nombre de empleados USER_ALTA / USER_BAJA para mostrar
        var tNombreAlta = _service.ObtenerNombreEmpleadoAsync(activo.UserAlta ?? "");
        var tNombreBaja = _service.ObtenerNombreEmpleadoAsync(activo.UserBaja ?? "");
        await Task.WhenAll(tNombreAlta, tNombreBaja);

        // Archivos adjuntos
        var archivos = ObtenerArchivos(activo.CarpetaKey, "alta")
            .Concat(ObtenerArchivos(activo.CarpetaKey, "baja"))
            .ToList();

        ViewBag.NombreUserAlta  = tNombreAlta.Result;
        ViewBag.NombreUserBaja  = tNombreBaja.Result;
        ViewBag.Archivos        = archivos;
        ViewBag.NavToken        = t;

        return View("~/Views/Contabilidad/ActivoFijo/Editar.cshtml", activo);
    }

    [HttpPost("Editar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(ActivoFijoDto dto, string? t = null)
    {
        var usuario = HttpContext.Session.GetString("OracleUser") ?? User.Identity?.Name ?? "APP";

        try
        {
            await _service.ActualizarActivoAsync(dto, usuario);
            TempData["Success"] = $"Activo {dto.Codigo} actualizado correctamente.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar activo {Codigo}", dto.Codigo);
            TempData["Error"] = $"Error al guardar: {ex.Message}";
        }

        return RedirectToAction(nameof(Editar), new { clase = dto.Clase, codigo = dto.Codigo, numero = dto.Numero, t });
    }

    // ── FICHA (impresión) ──────────────────────────────────────────────────────

    [HttpGet("Ficha")]
    public async Task<IActionResult> Ficha(string clase, string codigo, int numero = 0)
    {
        var activo = await _service.ObtenerActivoAsync(clase, codigo, numero);
        if (activo == null) return NotFound();

        // Cargar lookups y firmas en paralelo
        var tProveedores  = _service.ObtenerNombresProveedoresAsync(
            new[] { activo.CodProveed }.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!));
        var tCCostos      = _service.ObtenerDescripcionesCCostosAsync(
            new[] { activo.CCosto }.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!));
        var tFirmas       = _service.ObtenerFirmasAsync(activo.UserAlta, activo.UserBaja);
        var tNombreAlta   = _service.ObtenerNombreEmpleadoAsync(activo.UserAlta ?? "");
        var tNombreBaja   = _service.ObtenerNombreEmpleadoAsync(activo.UserBaja ?? "");

        await Task.WhenAll(tProveedores, tCCostos, tFirmas, tNombreAlta, tNombreBaja);

        var (firmaAlta, firmaBaja) = tFirmas.Result;

        // Archivos adjuntos para la ficha
        var archivosAlta = ObtenerArchivos(activo.CarpetaKey, "alta");
        var archivosBaja = ObtenerArchivos(activo.CarpetaKey, "baja");

        var tema = _empresaTema.GetTemaActual();
        ViewBag.Proveedores     = tProveedores.Result;
        ViewBag.CCostos         = tCCostos.Result;
        ViewBag.FirmaAlta       = firmaAlta;
        ViewBag.FirmaBaja       = firmaBaja;
        ViewBag.ArchivosAlta    = archivosAlta;
        ViewBag.ArchivosBaja    = archivosBaja;
        ViewBag.NombreUserAlta  = tNombreAlta.Result;
        ViewBag.NombreUserBaja  = tNombreBaja.Result;
        ViewBag.EmpresaNombre   = tema.NombreCompleto;
        ViewBag.EmpresaRuc      = tema.Ruc;

        return View("~/Views/Contabilidad/ActivoFijo/Ficha.cshtml", activo);
    }

    // ── SUBIR IMÁGENES ALTA ────────────────────────────────────────────────────

    [HttpPost("SubirImagenesAlta")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubirImagenesAlta(ActivoFijoUploadModel model)
    {
        model.Tipo = "alta";
        return await SubirArchivos(model);
    }

    // ── SUBIR IMÁGENES BAJA ────────────────────────────────────────────────────

    [HttpPost("SubirImagenesBaja")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubirImagenesBaja(ActivoFijoUploadModel model)
    {
        model.Tipo = "baja";
        return await SubirArchivos(model);
    }

    // ── ACCIÓN UNIFICADA: guardar observaciones + subir archivos de ALTA ──────────────────────

    [HttpPost("GuardarYSubirAlta")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarYSubirAlta(ActivoFijoUploadModel model)
    {
        model.Tipo = "alta";
        return await GuardarYSubir(model);
    }

    // ── ACCIÓN UNIFICADA: guardar observaciones + subir archivos de BAJA ──────────────────────

    [HttpPost("GuardarYSubirBaja")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarYSubirBaja(ActivoFijoUploadModel model)
    {
        model.Tipo = "baja";
        return await GuardarYSubir(model);
    }

    private async Task<IActionResult> GuardarYSubir(ActivoFijoUploadModel model)
    {
        var redir  = new { clase = model.Clase, codigo = model.Codigo, numero = model.Numero, t = model.ReturnToken };
        var usuario = HttpContext.Session.GetString("OracleUser") ?? User.Identity?.Name ?? "APP";

        // 1 — Guardar observaciones del activo
        try
        {
            var dto = await _service.ObtenerActivoAsync(model.Clase, model.Codigo, model.Numero);
            if (dto == null)
            {
                TempData["Error"] = $"Activo {model.Codigo} no encontrado.";
                return RedirectToAction(nameof(Editar), redir);
            }
            // Aplicar solo la observación del tipo correspondiente
            if (model.Tipo == "alta" && model.ObsAlta != null) dto.ObsAlta = model.ObsAlta;
            if (model.Tipo == "baja" && model.ObsBaja != null) dto.ObsBaja = model.ObsBaja;
            await _service.ActualizarActivoAsync(dto, usuario);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar observaciones en GuardarYSubir {Codigo}", model.Codigo);
            TempData["Error"] = $"Error al guardar: {ex.Message}";
            return RedirectToAction(nameof(Editar), redir);
        }

        // 2 — Subir archivos (si se seleccionaron)
        if (model.Archivos != null && model.Archivos.Count > 0)
            return await SubirArchivos(model);

        TempData["Success"] = "Observaciones guardadas.";
        return RedirectToAction(nameof(Editar), redir);
    }

    private async Task<IActionResult> SubirArchivos(ActivoFijoUploadModel model)
    {
        var redir = new { clase = model.Clase, codigo = model.Codigo, numero = model.Numero, t = model.ReturnToken };

        if (string.IsNullOrWhiteSpace(model.Clase) || string.IsNullOrWhiteSpace(model.Codigo))
        {
            TempData["Error"] = "Parámetros inválidos.";
            return RedirectToAction(nameof(Editar), redir);
        }

        if (model.Archivos == null || model.Archivos.Count == 0)
        {
            TempData["Warning"] = "No se seleccionaron archivos.";
            return RedirectToAction(nameof(Editar), redir);
        }

        var activo = await _service.ObtenerActivoAsync(model.Clase, model.Codigo, model.Numero);
        if (activo == null) return NotFound();

        var carpeta = ObtenerCarpeta(activo.CarpetaKey, model.Tipo);
        EnsureNetworkShare(ObtenerCarpetaRaiz());
        Directory.CreateDirectory(carpeta);

        var errores  = new List<string>();
        int exitosos = 0;

        // Prefijo determinístico: {CarpetaKey}_{TIPO en mayúsculas}
        // El índice se calcula contando los archivos ya existentes en la carpeta.
        var tipoTag   = model.Tipo.ToUpperInvariant(); // ALTA | BAJA
        var prefijoFijo = $"{activo.CarpetaKey}_{tipoTag}";
        int indiceBase  = Directory.Exists(carpeta)
            ? Directory.GetFiles(carpeta).Length
            : 0;

        int fileIdx = 0;
        foreach (var archivo in model.Archivos)
        {
            if (archivo.Length == 0) continue;

            // Nombre base: {CarpetaKey}_{TIPO}_{índice:D2}
            // El procesador agrega la extensión (.jpg para imágenes, .pdf para PDF).
            var idx        = indiceBase + fileIdx + 1;
            var nombreBase = $"{prefijoFijo}_{idx:D2}";
            fileIdx++;

            try
            {
                // ProcesadorImagenActivoFijo:
                //  • Valida tamaño (máx 20 MB) y extensión
                //  • AutoOrient EXIF → fotos de celular no salen giradas en Oracle Forms
                //  • Redimensiona a máx. 1600 px por lado
                //  • Convierte y comprime a JPEG 75 %
                //  • PDF pasa sin modificar
                var resultado = await _procesadorImagen.ProcesarYGuardarAsync(archivo, carpeta, nombreBase);
                _logger.LogInformation(
                    "ActivoFijo IMG guardada: {Nombre} ({Orig}KB → {Final}KB) {W}x{H}",
                    resultado.NombreArchivo,
                    resultado.BytesOriginales / 1024,
                    resultado.BytesFinales    / 1024,
                    resultado.AnchoFinal,
                    resultado.AltoFinal);
                exitosos++;
            }
            catch (InvalidOperationException ex)
            {
                errores.Add($"{archivo.FileName}: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando imagen {Nombre}", archivo.FileName);
                errores.Add($"Error al procesar {archivo.FileName}.");
            }
        }

        if (exitosos > 0)
        {
            var usuario = HttpContext.Session.GetString("OracleUser") ?? User.Identity?.Name ?? "APP";
            await _service.ActualizarUsuarioAltaBajaAsync(model.Clase, model.Codigo, model.Numero, model.Tipo, usuario);
        }

        TempData[errores.Count > 0 ? "Warning" : "Success"] = errores.Count > 0
            ? $"Se cargaron {exitosos} archivo(s). Errores: {string.Join("; ", errores)}"
            : $"Se cargaron {exitosos} imagen(es) de {model.Tipo}.";

        return RedirectToAction(nameof(Editar), redir);
    }

    // ── VER ARCHIVO ────────────────────────────────────────────────────────────

    [HttpGet("Ver/{carpetaKey}/{tipo}/{nombreArchivo}")]
    public IActionResult Ver(string carpetaKey, string tipo, string nombreArchivo)
    {
        nombreArchivo = Path.GetFileName(nombreArchivo);
        var ruta = Path.Combine(ObtenerCarpeta(carpetaKey, tipo), nombreArchivo);
        EnsureNetworkShare(ObtenerCarpetaRaiz());
        if (!System.IO.File.Exists(ruta)) return NotFound();
        return PhysicalFile(ruta, ObtenerContentType(Path.GetExtension(nombreArchivo)));
    }

    // ── DESCARGAR ARCHIVO ──────────────────────────────────────────────────────

    [HttpGet("Descargar/{carpetaKey}/{tipo}/{nombreArchivo}")]
    public IActionResult Descargar(string carpetaKey, string tipo, string nombreArchivo)
    {
        nombreArchivo = Path.GetFileName(nombreArchivo);
        var ruta = Path.Combine(ObtenerCarpeta(carpetaKey, tipo), nombreArchivo);
        EnsureNetworkShare(ObtenerCarpetaRaiz());
        if (!System.IO.File.Exists(ruta)) return NotFound();
        return PhysicalFile(ruta, ObtenerContentType(Path.GetExtension(nombreArchivo)), nombreArchivo);
    }

    // ── ELIMINAR ARCHIVO ───────────────────────────────────────────────────────

    [HttpPost("EliminarArchivo")]
    [ValidateAntiForgeryToken]
    public IActionResult EliminarArchivo(
        string carpetaKey, string tipo, string nombreArchivo,
        string? clase = null, string? codigo = null, int numero = 0, string? t = null)
    {
        nombreArchivo = Path.GetFileName(nombreArchivo);
        var carpeta   = ObtenerCarpeta(carpetaKey, tipo);
        EnsureNetworkShare(ObtenerCarpetaRaiz());
        var ruta = Path.Combine(carpeta, nombreArchivo);
        try
        {
            if (System.IO.File.Exists(ruta)) System.IO.File.Delete(ruta);
            TempData["Success"] = $"Archivo '{nombreArchivo}' eliminado.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo eliminar el archivo {Ruta}", ruta);
            TempData["Error"] = $"No se pudo eliminar '{nombreArchivo}'. El archivo puede estar en uso.";
        }
        return RedirectToAction(nameof(Editar), new { clase, codigo, numero, t });
    }


    // -- MEMORANDO -----------------------------------------------------------------------

    [HttpGet("Memorando")]
    public IActionResult Memorando(string? seleccion)
    {
        if (string.IsNullOrWhiteSpace(seleccion))
        {
            TempData["Warning"] = "Seleccione al menos un activo para generar el memorando.";
            return RedirectToAction(nameof(Index));
        }
        var form = new MemorandoFormModel { Seleccion = seleccion, Anio = DateTime.Now.Year };
        return View("~/Views/Contabilidad/ActivoFijo/Memorando.cshtml", form);
    }

    [HttpPost("Memorando")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Memorando(MemorandoFormModel form)
    {
        if (!ModelState.IsValid) return View("~/Views/Contabilidad/ActivoFijo/Memorando.cshtml", form);
        var claves = (form.Seleccion ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => { var p = s.Trim().Split('|'); return p.Length == 3 && int.TryParse(p[2], out var n) ? (Clase: p[0], Codigo: p[1], Numero: n) : (Clase: "", Codigo: "", Numero: 0); })
            .Where(c => !string.IsNullOrEmpty(c.Clase)).ToList();
        if (claves.Count == 0) { ModelState.AddModelError("Seleccion", "No se encontraron activos validos."); return View("~/Views/Contabilidad/ActivoFijo/Memorando.cshtml", form); }
        var items   = await _service.ObtenerActivosParaMemoAsync(claves);
        var usuario = HttpContext.Session.GetString("OracleUser") ?? User.Identity?.Name ?? "";
        var firma   = await _service.ObtenerFirmaUsuarioAsync(usuario);
        var tema    = _empresaTema.GetTemaActual();
        var dto = new MemorandoDto {
            NumeroMemo      = $"{form.NumMemo} - {form.Anio} - {form.Area}".Trim(' ', '-'),
            Ciudad          = form.Ciudad, Fecha = DateTime.Now,
            De              = form.De, Para = form.Para, CargoDestino = form.CargoDestino,
            Referencia      = form.Referencia, CuerpoTexto = form.CuerpoTexto,
            MotivoEntre     = form.MotivoEntre, Items = items, FirmaEmisor = firma,
            EmpresaNombre   = tema.NombreCompleto,
            EmpresaRuc      = tema.Ruc,
            EmpresaDireccion = tema.Direccion,
            EmpresaTelefono = tema.Telefono,
            EmpresaLogoPath = tema.LogoFullPath
        };
        return View("~/Views/Contabilidad/ActivoFijo/ImprimirMemo.cshtml", dto);
    }

    // ── HELPERS ────────────────────────────────────────────────────────────────

    private string ObtenerCarpetaRaiz()
    {
        var raiz = _config["RutaActivosFijos"]
            ?? Path.Combine(_env.WebRootPath, "uploads", "activos");
        var ruc = _empresaTema.GetRucActual();
        return string.IsNullOrWhiteSpace(ruc) ? raiz : Path.Combine(raiz, ruc);
    }

    private string ObtenerCarpeta(string carpetaKey, string tipo)
        => Path.Combine(ObtenerCarpetaRaiz(), carpetaKey, tipo);

    private List<ArchivoAfDto> ObtenerArchivos(string carpetaKey, string tipo)
    {
        var carpeta = ObtenerCarpeta(carpetaKey, tipo);
        if (!Directory.Exists(carpeta)) return new();
        return Directory.GetFiles(carpeta)
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTime)
            .Select(f => new ArchivoAfDto
            {
                NombreArchivo = f.Name,
                Tipo          = tipo,
                TamanioBytes  = f.Length,
                FechaCarga    = f.CreationTime
            })
            .ToList();
    }

    private new static void EnsureNetworkShare(string path)
    {
        try { if (!string.IsNullOrEmpty(path) && !Directory.Exists(path)) Directory.CreateDirectory(path); }
        catch { /* network share may already exist */ }
    }

    private static string ObtenerContentType(string extension) => extension.ToLower() switch
    {
        ".pdf"  => "application/pdf",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png"  => "image/png",
        _       => "application/octet-stream"
    };
}
