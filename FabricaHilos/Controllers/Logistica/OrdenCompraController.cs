using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FabricaHilos.Services.Logistica;
using FabricaHilos.Models.Logistica;
using FabricaHilos.Services;
using FabricaHilos.Data;

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
    private readonly ApplicationDbContext _db;
    private readonly IMenuService _menuService;

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
        INavTokenService navToken,
        ApplicationDbContext db,
        IMenuService menuService)
    {
        _service      = service;
        _env          = env;
        _config       = config;
        _logger       = logger;
        _empresaTema  = empresaTema;
        _navToken     = navToken;
        _db           = db;
        _menuService  = menuService;
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
        // ── Restricciones dinámicas según AccesoWeb ────────────────────────────
        var accesoOc = _menuService.ObtenerAccesoModulo("LogisticaOrdenCompra");

        // Si el token indica estado fijo (ej: estado=2), se impone sin posibilidad de cambiarlo
        var estadoForzado = accesoOc.ObtenerParametro("estado");
        if (!string.IsNullOrEmpty(estadoForzado))
            estado = estadoForzado;

        // Exponer modificadores a la vista
        ViewBag.NoNuevaOC    = accesoOc.TieneModificador("noNuevaOC");
        ViewBag.EstadoForzado = estadoForzado;

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
            .Select(c => c!)
            .Distinct();
        ViewBag.Proveedores  = await _service.ObtenerNombresProveedoresAsync(codigos);

        var codigosCc = items.Select(o => o.CCosto).Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).Distinct();
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

        // ── Fase 1: obtener cabecera e ítems en paralelo ──────────────────────
        var tOrden = _service.ObtenerOrdenAsync(tipoDocto, serie, numPed);
        var tItems = _service.ObtenerItemsAsync(tipoDocto, serie, numPed);
        await Task.WhenAll(tOrden, tItems);

        var orden = tOrden.Result;
        var items = tItems.Result;

        if (orden is null)
            return NotFound();

        // ── Fase 2: todos los lookups en paralelo ─────────────────────────────
        var codigos        = new[] { orden.CodProveed }.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).Distinct();
        var codigosCc      = new[] { orden.CCosto     }.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).Distinct();
        var codigosCondPag = new[] { orden.CondPag    }.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).Distinct();
        var codigosArt     = items.Select(i => i.CodArt).Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).Distinct();
        var usuariosAuditoria = new[] { orden.AAduser, orden.AMduser }
            .Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).Distinct().ToList();

        var tProveedores    = _service.ObtenerNombresProveedoresAsync(codigos);
        var tCentrosCosto   = _service.ObtenerDescripcionesCentroCostosAsync(codigosCc);
        var tCondPag        = _service.ObtenerDescripcionesCondPagAsync(codigosCondPag);
        var tArticulos      = _service.ObtenerDescripcionesArticulosAsync(codigosArt);
        var tNombres        = Task.WhenAll(usuariosAuditoria.Select(u => _service.ObtenerNombreEmpleadoAsync(u)));
        var tProvDetalle    = _service.ObtenerDetalleProveedorAsync(orden.CodProveed ?? "");
        var tPropagate      = _service.PropagateGruposReqToItemOrdAsync(numPed);
        var tGruposReq      = _service.ObtenerGruposDeRequisicionesVinculadasAsync(numPed);

        await Task.WhenAll(tProveedores, tCentrosCosto, tCondPag, tArticulos, tNombres, tProvDetalle, tPropagate, tGruposReq);

        // Si la propagación añadió grupos nuevos, recargar ítems
        if (tPropagate.Result)
            items = await _service.ObtenerItemsAsync(tipoDocto, serie, numPed);

        var nombresUsuarios = usuariosAuditoria
            .Zip(tNombres.Result, (u, n) => (u, n))
            .ToDictionary(x => x.u, x => x.n, StringComparer.OrdinalIgnoreCase);

        ViewBag.Proveedores            = tProveedores.Result;
        ViewBag.CentrosCosto           = tCentrosCosto.Result;
        ViewBag.DescripcionesCondPag   = tCondPag.Result;
        ViewBag.DescripcionesArticulos = tArticulos.Result;
        ViewBag.NombresUsuarios        = nombresUsuarios;
        ViewBag.ProveedorDetalle       = tProvDetalle.Result;

        ViewBag.NavToken   = t;
        ViewBag.Dt         = dt;
        ViewBag.ReturnPage = page;

        var accesoOcDetalle       = _menuService.ObtenerAccesoModulo("LogisticaOrdenCompra");
        var accesoLogistica       = _menuService.ObtenerAccesoModulo("Logistica");
        ViewBag.NoAprobarOC          = accesoOcDetalle.TieneModificador("noAprobarOC")
                                    || accesoLogistica.TieneModificador("noAprobarOC");
        ViewBag.MostrarAnularOC      = accesoOcDetalle.TieneModificador("AnularOC")
                                    || accesoLogistica.TieneModificador("AnularOC");
        ViewBag.MostrarEnviarGerenciaOC = accesoOcDetalle.TieneModificador("EnviarGerenciaOC")
                                       || accesoLogistica.TieneModificador("EnviarGerenciaOC");

        EnsureNetworkShare(ObtenerCarpetaRaiz());

        var archivosOc = ObtenerArchivosExistentes(items);

        var archivosReq = ObtenerArchivosExistentes(
            tGruposReq.Result.Select(g => new ItemOrdDto { IdGrupo = g }));
        foreach (var a in archivosReq)
            a.EsDeRequerimiento = true;

        ViewBag.ArchivosExistentes = archivosOc.Concat(archivosReq)
            .OrderByDescending(a => a.IdGrupo).ThenByDescending(a => a.FechaCarga).ToList();

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

    // ── NUEVA ORDEN DE COMPRA ──────────────────────────────────────────────────

    [HttpGet("Nueva")]
    public async Task<IActionResult> Nueva(string? t = null)
    {
        var requisiciones = (await _service.ObtenerRequisicionesPendientesAsync())
                            .OrderByDescending(r => r.NumReq)
                            .ToList();
        var proveedores      = await _service.ObtenerTodosProveedoresAsync();
        var condPag          = await _service.ObtenerTodasCondPagAsync();
        var proveedoresConPag = await _service.ObtenerCondPagPorProveedorAsync();
        var opcEntrega    = await _service.ObtenerOpcEntregaAsync();
        var igvList       = await _service.ObtenerIgvAsync();
        var centrosCosto  = await _service.ObtenerDescripcionesCentroCostosAsync(
            requisiciones.Select(r => r.CentroCosto).Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).Distinct());

        ViewBag.NavToken    = t;
        ViewBag.Requisiciones = requisiciones;
        ViewBag.Proveedores   = proveedores;
        ViewBag.ProveedoresCondPag = proveedoresConPag;
        ViewBag.CondPag       = condPag;
        ViewBag.OpcEntrega    = opcEntrega;   // List<OpcEntregaDto>
        ViewBag.IgvList       = igvList;      // List<IgvDto>
        ViewBag.CentrosCosto  = centrosCosto;
        ViewBag.Usuario       = HttpContext.Session.GetString("OracleUser") ?? string.Empty;

        var empresa = _empresaTema.GetTemaActual();
        ViewBag.EmpresaNombre = empresa.NombreCompleto;
        ViewBag.EmpresaRuc    = _empresaTema.GetRucActual();

        return View("~/Views/Logistica/OrdenCompra/Nueva.cshtml");
    }

    // ── AJAX: ítems de un requerimiento ───────────────────────────────────────

    [HttpGet("ItemsReq")]
    public async Task<IActionResult> ItemsReq(string tipDoc, int serie, long numReq)
    {
        var items = await _service.ObtenerItemsReqPendientesAsync(tipDoc, serie, numReq);

        // Resolver descripciones de destino agrupando por tipo para minimizar llamadas
        var codigosU = items.Where(i => i.TpDestino == "U" && !string.IsNullOrWhiteSpace(i.Destino))
                            .Select(i => i.Destino!).Distinct().ToList();
        var codigosA = items.Where(i => i.TpDestino == "A" && !string.IsNullOrWhiteSpace(i.Destino))
                            .Select(i => i.Destino!).Distinct().ToList();

        var descMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (codigosU.Count > 0)
        {
            foreach (var cod in codigosU)
            {
                var res = await _service.ObtenerDestinosAsync("U", cod);
                var match = res.FirstOrDefault(d => string.Equals(d.Codigo, cod, StringComparison.OrdinalIgnoreCase));
                if (match != null) descMap[cod] = match.Descripcion;
            }
        }
        if (codigosA.Count > 0)
        {
            foreach (var cod in codigosA)
            {
                var res = await _service.ObtenerDestinosAsync("A", cod);
                var match = res.FirstOrDefault(d => string.Equals(d.Codigo, cod, StringComparison.OrdinalIgnoreCase));
                if (match != null) descMap[cod] = match.Descripcion;
            }
        }

        foreach (var it in items)
            if (!string.IsNullOrWhiteSpace(it.Destino) && descMap.TryGetValue(it.Destino!, out var desc))
                it.DestinoDesc = desc;

        return Json(items);
    }

    [HttpGet("BuscarDestinos")]
    public async Task<IActionResult> BuscarDestinos(string? tipo = null, string? buscar = null)
    {
        var destinos = await _service.ObtenerDestinosAsync(tipo, buscar);
        return Json(destinos.Select(d => new { val = d.Codigo, txt = d.Descripcion, tp = d.TpDestino }));
    }

    // ── REGISTRAR ORDEN ────────────────────────────────────────────────────────

    [HttpPost("Registrar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Registrar([FromBody] RegistrarOcRequest? request)
    {
        // ── Guardia: payload nulo o malformado (corte de red, cierre de navegador) ──
        if (request is null)
            return Json(new { ok = false, error = "No se recibieron datos. Intente nuevamente." });

        // ── Validación de cabecera ────────────────────────────────────────────────
        var errs = new List<string>();

        if (string.IsNullOrWhiteSpace(request.TipoDocto))
            errs.Add("Tipo de documento es requerido.");
        if (request.Fecha == default)
            errs.Add("Fecha es requerida.");
        if (request.FEntrega == default)
            errs.Add("Fecha de entrega es requerida.");
        if (string.IsNullOrWhiteSpace(request.CodProveed))
            errs.Add("Proveedor es requerido.");
        if (string.IsNullOrWhiteSpace(request.CondPag))
            errs.Add("Condición de pago es requerida.");
        if (string.IsNullOrWhiteSpace(request.Moneda))
            errs.Add("Moneda es requerida.");
        if (string.IsNullOrWhiteSpace(request.OpcLEntrega))
            errs.Add("Lugar de entrega es requerido.");

        // ── Validación de ítems ───────────────────────────────────────────────────
        if (request.Items is null || request.Items.Count == 0)
            errs.Add("Debe incluir al menos un ítem.");
        else
        {
            for (int i = 0; i < request.Items.Count; i++)
            {
                var it  = request.Items[i];
                var idx = $"Ítem {i + 1} ({it.CodArt ?? "?"})";

                if (string.IsNullOrWhiteSpace(it.TipDoc))
                    errs.Add($"{idx}: TipDoc es requerido.");
                if (string.IsNullOrWhiteSpace(it.CodArt))
                    errs.Add($"{idx}: Código de artículo es requerido.");
                if (it.NumReq <= 0)
                    errs.Add($"{idx}: Número de requerimiento inválido.");
                if (it.Orden <= 0)
                    errs.Add($"{idx}: Orden inválida.");
                if (it.Cantidad <= 0)
                    errs.Add($"{idx}: Cantidad debe ser mayor a 0.");
                if (it.Precio <= 0)
                    errs.Add($"{idx}: Precio debe ser mayor a 0.");
                if (it.PorDesc1 < 0 || it.PorDesc1 >= 100)
                    errs.Add($"{idx}: Descuento 1 fuera de rango (0-99.99).");
                if (it.PorDesc2 < 0 || it.PorDesc2 >= 100)
                    errs.Add($"{idx}: Descuento 2 fuera de rango (0-99.99).");
            }
        }

        if (errs.Count > 0)
        {
            _logger.LogWarning("Registrar OC rechazado — {Count} error(es): {Errors}",
                errs.Count, string.Join(" | ", errs));
            return Json(new { ok = false, error = string.Join("\n", errs) });
        }

        // ── Llamada al servicio ───────────────────────────────────────────────────
        var usuario   = HttpContext.Session.GetString("OracleUser")       ?? string.Empty;
        var cCodigo   = HttpContext.Session.GetString("OracleUserCodigo") ?? string.Empty;

        // P_C_CODIGO siempre debe ser el C_CODIGO del usuario logueado (no viene del cliente)
        if (string.IsNullOrWhiteSpace(request.CCodigo))
            request.CCodigo = cCodigo;

        try
        {
            var (numPed, error) = await _service.RegistrarOcAsync(request, usuario);

            if (!string.IsNullOrEmpty(error))
                return Json(new { ok = false, error });

            // ── Persistir en SQL Server ANTES de responder al cliente ─────────────
            // Si la red cae después del COMMIT de Oracle, el registro queda aquí
            // y el usuario puede recuperarlo la próxima vez que abra "Nueva Orden".
            var logEntry = new LogRegistroOc
            {
                Usuario    = usuario,
                TipoDocto  = request.TipoDocto!,
                NumPed     = numPed,
                Serie      = 1,
                CodProveed = request.CodProveed!,
                Moneda     = request.Moneda!,
                Impsto     = request.Impsto,
                Fecha      = request.Fecha,
                FEntrega   = request.FEntrega,
                CantItems  = request.Items?.Count ?? 0,
                Detalle    = request.Detalle,
                FechaLog   = DateTime.UtcNow,
                Notificado = false
            };
            _db.LogRegistrosOc.Add(logEntry);
            await _db.SaveChangesAsync();

            // Generar token de detalle para navegar directamente a la OC creada
            var dtToken = _navToken.Protect(new Dictionary<string, string?> {
                ["tipoDocto"] = request.TipoDocto,
                ["serie"]     = "1",
                ["numPed"]    = numPed.ToString()
            });
            var detailUrl = Url.Action(nameof(Detalle), "OrdenCompra",
                new { dt = dtToken, t = HttpContext.Request.Query["t"].ToString() });
            var printUrl = Url.Action(nameof(Imprimir), "OrdenCompra",
                new { dt = dtToken, t = HttpContext.Request.Query["t"].ToString() });

            return Json(new { ok = true, numPed, detailUrl, printUrl, logId = logEntry.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al registrar OC. Usuario={Usuario}", usuario);
            return Json(new { ok = false, error = "Error interno al registrar la orden. Contacte al administrador." });
        }
    }

    // ── ACUSAR RECEPCIÓN ──────────────────────────────────────────────────────
    // El front llama a este endpoint una vez que recibió y mostró el resultado.
    // Marca el log como notificado para que no vuelva a aparecer como pendiente.

    [HttpPost("AcusarRecepcion")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcusarRecepcion([FromBody] long logId)
    {
        var usuario = HttpContext.Session.GetString("OracleUser") ?? string.Empty;
        var log = await _db.LogRegistrosOc.FindAsync(logId);
        if (log != null && log.Usuario == usuario)
        {
            log.Notificado = true;
            await _db.SaveChangesAsync();
        }
        return Json(new { ok = true });
    }

    // ── ÓRDENES PENDIENTES DE NOTIFICAR ───────────────────────────────────────
    // Devuelve OC registradas en Oracle pero cuya respuesta no llegó al cliente.
    // Se consulta al entrar a "Nueva Orden" para alertar al usuario.

    [HttpGet("PendientesNotificacion")]
    public async Task<IActionResult> PendientesNotificacion()
    {
        var usuario = HttpContext.Session.GetString("OracleUser") ?? string.Empty;
        var pendientes = _db.LogRegistrosOc
            .Where(l => l.Usuario == usuario && !l.Notificado
                     && l.FechaLog >= DateTime.UtcNow.AddHours(-8)) // solo del turno actual
            .OrderByDescending(l => l.FechaLog)
            .Select(l => new {
                l.Id, l.NumPed, l.TipoDocto, l.Serie,
                l.CodProveed, l.Moneda, l.CantItems,
                l.Fecha, l.Detalle, l.FechaLog
            })
            .ToList();

        return Json(pendientes);
    }

    // ── IMPRIMIR ORDEN ─────────────────────────────────────────────────────────

    [HttpGet("Imprimir")]
    public async Task<IActionResult> Imprimir(string? dt = null, string? t = null)
    {
        if (string.IsNullOrEmpty(dt) || !_navToken.TryUnprotect(dt, out var dtNav))
            return NotFound();

        var tipoDocto = dtNav.GetValueOrDefault("tipoDocto") ?? string.Empty;
        if (!int.TryParse(dtNav.GetValueOrDefault("serie"), out var serie)) serie = 0;
        if (!long.TryParse(dtNav.GetValueOrDefault("numPed"), out var numPed)) numPed = 0;

        var orden = await _service.ObtenerOrdenAsync(tipoDocto, serie, numPed);
        if (orden is null) return NotFound();

        var items = await _service.ObtenerItemsAsync(tipoDocto, serie, numPed);

        var codigos = new[] { orden.CodProveed }.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).Distinct();
        ViewBag.Proveedores = await _service.ObtenerNombresProveedoresAsync(codigos);

        var codigosCc = new[] { orden.CCosto }.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).Distinct();
        ViewBag.CentrosCosto = await _service.ObtenerDescripcionesCentroCostosAsync(codigosCc);

        var codigosCondPag = new[] { orden.CondPag }.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).Distinct();
        ViewBag.DescripcionesCondPag = await _service.ObtenerDescripcionesCondPagAsync(codigosCondPag);

        var codigosArt = items.Select(i => i.CodArt).Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).Distinct();
        ViewBag.DescripcionesArticulos = await _service.ObtenerDescripcionesArticulosAsync(codigosArt);

        var usuariosAuditoria = new[] { orden.AAduser, orden.AMduser }
            .Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).Distinct();
        var tareasNombres = usuariosAuditoria.Select(u => _service.ObtenerNombreEmpleadoAsync(u));
        var nombresResultado = await Task.WhenAll(tareasNombres);
        var nombresUsuarios = usuariosAuditoria.Zip(nombresResultado, (u, n) => (u, n))
            .ToDictionary(x => x.u, x => x.n, StringComparer.OrdinalIgnoreCase);
        ViewBag.NombresUsuarios = nombresUsuarios;

        var empresa = _empresaTema.GetTemaActual();
        ViewBag.EmpresaNombre      = empresa.NombreCompleto;
        ViewBag.EmpresaNombreCorto = empresa.NombreCorto;
        ViewBag.EmpresaRuc         = _empresaTema.GetRucActual();
        ViewBag.EmpresaLogoPath    = empresa.LogoFullPath;
        ViewBag.EmpresaLogoAlt     = empresa.LogoAlt;
        ViewBag.EmpresaDireccion   = empresa.Direccion;
        ViewBag.EmpresaTelefono    = empresa.Telefono;

        var provDetalle = await _service.ObtenerDetalleProveedorAsync(orden.CodProveed ?? "");
        ViewBag.ProveedorDetalle = provDetalle;

        ViewBag.NavToken        = t;
        ViewBag.Dt              = dt;

        var (firmaGenerado, firmaAprobado) = await _service.ObtenerFirmasOcAsync(tipoDocto, serie, numPed);
        ViewBag.FirmaGenerado = firmaGenerado;
        ViewBag.FirmaAprobado = firmaAprobado;

        return View("~/Views/Logistica/OrdenCompra/Imprimir.cshtml", (orden, items));
    }

    // ── FIRMA IMAGEN ───────────────────────────────────────────────────────────

    [HttpGet("FirmaImg")]
    public async Task<IActionResult> FirmaImg(string dt, string rol)
    {
        if (string.IsNullOrEmpty(dt) || !_navToken.TryUnprotect(dt, out var dtNav))
            return NotFound();

        var tipoDocto = dtNav.GetValueOrDefault("tipoDocto") ?? string.Empty;
        if (!int.TryParse(dtNav.GetValueOrDefault("serie"),  out var serie))  serie  = 0;
        if (!long.TryParse(dtNav.GetValueOrDefault("numPed"), out var numPed)) numPed = 0;

        var (firmaGenerado, firmaAprobado) = await _service.ObtenerFirmasOcAsync(tipoDocto, serie, numPed);
        var firma = rol == "aprobado" ? firmaAprobado : firmaGenerado;

        if (firma?.Firma == null || firma.Firma.Length == 0)
            return NotFound();

        var mime = FabricaHilos.Services.Logistica.OrdenCompraService.DetectImageMimeType(firma.Firma)
                   ?? "application/octet-stream";
        return File(firma.Firma, mime);
    }

    // ── IMPRIMIR CONTABILIDAD ──────────────────────────────────────────────────

    [HttpGet("ImprimirContabilidad")]
    public async Task<IActionResult> ImprimirContabilidad(string? dt = null, string? t = null)
    {
        if (string.IsNullOrEmpty(dt) || !_navToken.TryUnprotect(dt, out var dtNav))
            return NotFound();

        var tipoDocto = dtNav.GetValueOrDefault("tipoDocto") ?? string.Empty;
        if (!int.TryParse(dtNav.GetValueOrDefault("serie"), out var serie)) serie = 0;
        if (!long.TryParse(dtNav.GetValueOrDefault("numPed"), out var numPed)) numPed = 0;

        var orden = await _service.ObtenerOrdenAsync(tipoDocto, serie, numPed);
        if (orden is null) return NotFound();

        var items = await _service.ObtenerItemsAsync(tipoDocto, serie, numPed);

        var codigos = new[] { orden.CodProveed }.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).Distinct();
        ViewBag.Proveedores = await _service.ObtenerNombresProveedoresAsync(codigos);

        // Todos los centros de costo que aparecen en cabecera e ítems
        var codigosCc = items.Select(i => i.CCosto)
            .Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!)
            .Concat(new[] { orden.CCosto }.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!))
            .Distinct();
        ViewBag.CentrosCosto = await _service.ObtenerDescripcionesCentroCostosAsync(codigosCc);

        var codigosCondPag = new[] { orden.CondPag }.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).Distinct();
        ViewBag.DescripcionesCondPag = await _service.ObtenerDescripcionesCondPagAsync(codigosCondPag);

        var codigosArt = items.Select(i => i.CodArt).Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).Distinct();
        ViewBag.DescripcionesArticulos = await _service.ObtenerDescripcionesArticulosAsync(codigosArt);

        var usuariosAuditoria = new[] { orden.AAduser, orden.AMduser }
            .Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).Distinct();
        var tareasNombres = usuariosAuditoria.Select(u => _service.ObtenerNombreEmpleadoAsync(u));
        var nombresResultado = await Task.WhenAll(tareasNombres);
        ViewBag.NombresUsuarios = usuariosAuditoria.Zip(nombresResultado, (u, n) => (u, n))
            .ToDictionary(x => x.u, x => x.n, StringComparer.OrdinalIgnoreCase);

        var empresa = _empresaTema.GetTemaActual();
        ViewBag.EmpresaNombre      = empresa.NombreCompleto;
        ViewBag.EmpresaNombreCorto = empresa.NombreCorto;
        ViewBag.EmpresaRuc         = _empresaTema.GetRucActual();
        ViewBag.EmpresaLogoPath    = empresa.LogoFullPath;
        ViewBag.EmpresaLogoAlt     = empresa.LogoAlt;
        ViewBag.EmpresaDireccion   = empresa.Direccion;
        ViewBag.EmpresaTelefono    = empresa.Telefono;

        ViewBag.ProveedorDetalle = await _service.ObtenerDetalleProveedorAsync(orden.CodProveed ?? "");
        ViewBag.NavToken = t;
        ViewBag.Dt       = dt;

        var (firmaGenerado, firmaAprobado) = await _service.ObtenerFirmasOcAsync(tipoDocto, serie, numPed);
        ViewBag.FirmaGenerado = firmaGenerado;
        ViewBag.FirmaAprobado = firmaAprobado;

        // Desglose por destino / centro de costo para Contabilidad
        var destinos = await _service.ObtenerDestinosPorOcAsync(tipoDocto, serie, numPed);
        // Resolver descripciones de destino usando el catálogo
        var catalogoDestinos = await _service.ObtenerDestinosAsync();
        var dictDestinos = catalogoDestinos.ToDictionary(
            d => $"{d.TpDestino}|{d.Codigo}", d => d.Descripcion, StringComparer.OrdinalIgnoreCase);
        foreach (var d in destinos)
        {
            var key = $"{d.TpDestino}|{d.Destino}";
            d.DestinoDesc = dictDestinos.TryGetValue(key, out var desc) ? desc : d.Destino;
        }
        ViewBag.DestinosOc = destinos;

        return View("~/Views/Logistica/OrdenCompra/ImprimirContabilidad.cshtml", (orden, items));
    }

    // ── ENVIAR A GERENCIA ──────────────────────────────────────────────────────

    [HttpPost("EnviarGerencia")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnviarGerencia([FromBody] AnularOcRequest request)
    {
        var error = await _service.EnviarGerenciaAsync(
            request.TipoDocto ?? string.Empty, request.NumPed);

        if (!string.IsNullOrEmpty(error))
            return Json(new { ok = false, error });

        TempData["Success"] = $"O/C N° {request.NumPed} enviada a aprobación de Gerencia.";
        return Json(new { ok = true, nuevoEstado = "2" });
    }

    // ── APROBAR ORDEN DE COMPRA ────────────────────────────────────────────────

    [HttpPost("AprobarOc")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AprobarOc([FromBody] AnularOcRequest request)
    {
        var codAprob = HttpContext.Session.GetString("OracleUserCodigo") ?? string.Empty;
        var error = await _service.AprobarOcAsync(
            request.TipoDocto ?? string.Empty, request.NumPed, codAprob);

        if (!string.IsNullOrEmpty(error))
            return Json(new { ok = false, error });

        TempData["Success"] = $"O/C N° {request.NumPed} aprobada correctamente.";
        return Json(new { ok = true, nuevoEstado = "2", aprobGerencia = "S" });
    }

    // ── NO APROBAR ORDEN DE COMPRA ────────────────────────────────────────────

    [HttpPost("NoAprobarOc")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NoAprobarOc([FromBody] AnularOcRequest request)
    {
        var codAprob = HttpContext.Session.GetString("OracleUserCodigo") ?? string.Empty;
        var error = await _service.NoAprobarOcAsync(
            request.TipoDocto ?? string.Empty, request.NumPed, codAprob);

        if (!string.IsNullOrEmpty(error))
            return Json(new { ok = false, error });

        TempData["Success"] = $"O/C N° {request.NumPed} marcada como No Aprobada.";
        return Json(new { ok = true, nuevoEstado = "3", aprobGerencia = "N" });
    }

    // ── CERRAR ORDEN DE COMPRA ─────────────────────────────────────────────────

    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CerrarOc([FromBody] AnularOcRequest request)
    {
        var error = await _service.CerrarOcAsync(
            request.TipoDocto ?? string.Empty, request.NumPed);

        if (!string.IsNullOrEmpty(error))
            return Json(new { ok = false, error });

        TempData["Success"] = $"O/C N° {request.NumPed} cerrada correctamente.";
        return Json(new { ok = true });
    }

    // ── ANULAR ORDEN ───────────────────────────────────────────────────────────

    [HttpPost("Anular")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Anular([FromBody] AnularOcRequest request)
    {
        var usuario = HttpContext.Session.GetString("OracleUser") ?? string.Empty;
        var error   = await _service.AnularOcAsync(
            request.TipoDocto ?? string.Empty, request.NumPed, usuario);

        if (!string.IsNullOrEmpty(error))
            return Json(new { ok = false, error });

        TempData["Success"] = $"O/C N° {request.NumPed} anulada correctamente.";
        return Json(new { ok = true, nuevoEstado = "9" });
    }

    // ── PREVIEW BORRADOR (Paso 2 → Imprimir.cshtml sin guardar) ──────────────

    [HttpPost("PreviewBorrador")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PreviewBorrador([FromBody] PreviewBorradorRequest? req)
    {
        if (req is null) return BadRequest();

        // Construir OrdenCompraDto desde los datos del formulario
        var oc = new OrdenCompraDto
        {
            TipoDocto  = req.TipoDocto,
            Serie      = 0,
            NumPed     = 0,
            Estado     = "0",                  // EMITIDA (borrador)
            Fecha      = req.Fecha == default ? DateTime.Today : req.Fecha,
            FEntrega   = req.FEntrega == default ? DateTime.Today : req.FEntrega,
            CodProveed = req.CodProveed,
            CondPag    = req.CondPag,
            Moneda     = req.Moneda,
            Detalle    = req.Detalle,
            CCosto     = req.CCosto,
            PrecioVta  = 0
        };

        // Construir ítems
        int orden = 0;
        var items = req.ItemsConDesc.Select(it => {
            orden++;
            var precio = it.Precio;
            var cant   = it.Cantidad;
            var d1     = it.PorDesc1 / 100m;
            var d2     = it.PorDesc2 / 100m;
            var imp    = Math.Round(cant * precio * (1 - d1) * (1 - d2), 2);
            return new ItemOrdDto
            {
                TipoDocto   = req.TipoDocto,
                Serie       = 0,
                NumPed      = 0,
                Orden       = orden,
                CodArt      = it.CodArt,
                CodOrig     = it.CodOrig,
                Unidad      = it.Unidad,
                Descripcion = it.Desc ?? it.Detalle ?? it.CodArt,
                Cantidad    = cant,
                Precio      = precio,
                PorDesc1    = it.PorDesc1,
                PorDesc2    = it.PorDesc2,
                ImpVvta     = imp,
                Estado      = "0",
                NumReq      = it.NumReq > 0 ? it.NumReq : null,
                OrdenReq    = it.Orden > 0 ? it.Orden : null,
            };
        }).ToList();

        // Calcular totales
        var impsto    = req.Impsto;
        var valVenta  = items.Sum(i => i.ImpVvta);
        var igvAmt    = Math.Round(valVenta * impsto, 2);
        oc.ValVenta   = valVenta;
        oc.ImpIgv     = igvAmt;
        oc.PrecioVta  = valVenta + igvAmt;

        // ViewBag: mismos lookups que usa Imprimir.cshtml
        ViewBag.Proveedores = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            { [req.CodProveed ?? ""] = req.ProveedorNombre ?? req.CodProveed ?? "" };
        ViewBag.CentrosCosto = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            { [req.CCosto ?? ""] = req.CCostoNombre ?? req.CCosto ?? "" };
        ViewBag.DescripcionesCondPag = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            { [req.CondPag ?? ""] = req.CondPagNombre ?? req.CondPag ?? "" };
        ViewBag.DescripcionesArticulos = req.ItemsConDesc
            .Where(i => !string.IsNullOrEmpty(i.CodArt))
            .GroupBy(i => i.CodArt!)
            .ToDictionary(g => g.Key, g => g.First().Desc ?? g.Key, StringComparer.OrdinalIgnoreCase);
        ViewBag.NombresUsuarios = new Dictionary<string, string>();

        var empresa = _empresaTema.GetTemaActual();
        ViewBag.EmpresaNombre    = empresa.NombreCompleto;
        ViewBag.EmpresaRuc       = _empresaTema.GetRucActual();
        ViewBag.EmpresaLogoPath  = empresa.LogoFullPath;
        ViewBag.EmpresaLogoAlt   = empresa.LogoAlt;
        ViewBag.EmpresaDireccion = empresa.Direccion;
        ViewBag.EmpresaTelefono  = empresa.Telefono;

        var provDetallePrev = await _service.ObtenerDetalleProveedorAsync(req.CodProveed ?? "");
        // Para preview, si no hay datos de Oracle usamos el nombre que ya viene en el payload
        if (provDetallePrev == null && !string.IsNullOrWhiteSpace(req.ProveedorNombre))
            provDetallePrev = new ProveedorDetalleDto { Codigo = req.CodProveed ?? "", Nombre = req.ProveedorNombre };
        ViewBag.ProveedorDetalle = provDetallePrev;

        // Para borrador no hay numPed aún; firmas se muestran en blanco
        ViewBag.FirmaGenerado = (FirmaOcDto?)null;
        ViewBag.FirmaAprobado = (FirmaOcDto?)null;

        return View("~/Views/Logistica/OrdenCompra/Imprimir.cshtml", (oc, items));
    }
}
