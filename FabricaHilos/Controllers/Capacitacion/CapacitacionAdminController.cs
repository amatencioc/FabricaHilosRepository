using Dapper;
using FabricaHilos.Models.Capacitacion;
using FabricaHilos.Services.Capacitacion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Oracle.ManagedDataAccess.Client;

namespace FabricaHilos.Controllers.Capacitacion;

[Authorize]
[Route("RecursosHumanos/CapacitacionAdmin/[action]")]
public class CapacitacionAdminController : OracleBaseController
{
    private readonly ICapacitacionService _capSvc;
    private readonly IExamenService       _examSvc;
    private readonly ContenidoMediaService _media;

    public CapacitacionAdminController(
        ICapacitacionService capSvc,
        IExamenService       examSvc,
        ContenidoMediaService media)
    {
        _capSvc  = capSvc;
        _examSvc = examSvc;
        _media   = media;
    }

    private string UsuarioActual => HttpContext.Session.GetString("OracleUser") ?? "";

    // ── Verificación de administrador ────────────────────────────────────────
    // Cachea el resultado en sesión para no ir a BD en cada request.
    // La clave "EsCapAdmin" puede ser "S" o "N" (string para compatibilidad
    // con el patrón de sesión existente en la app).
    public override async Task OnActionExecutionAsync(
        ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // 1) Ejecutar la verificación de sesión Oracle del base (síncrona)
        base.OnActionExecuting(context);
        if (context.Result != null)
        {
            // base redirigió → no continuar (ejecutar next para cerrar el pipeline)
            await next();
            return;
        }

        // 2) Leer cache de sesión
        var cacheAdmin = HttpContext.Session.GetString("EsCapAdmin");

        if (cacheAdmin == null)
        {
            // Primera vez: consultar BD y guardar en sesión
            var esAdmin = await _capSvc.IsCapAdminAsync(UsuarioActual);
            cacheAdmin  = esAdmin ? "S" : "N";
            HttpContext.Session.SetString("EsCapAdmin", cacheAdmin);
        }

        if (cacheAdmin != "S")
        {
            TempData["Error"] = "No tiene permisos para acceder al área de administración de Capacitación.";
            context.Result = RedirectToAction("Index", "Capacitacion",
                new { area = "" });
            await next();
            return;
        }

        await next();
    }


    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var categorias = await _capSvc.GetCategoriasAsync();
        var cursos     = await _capSvc.GetCatalogoAsync(UsuarioActual);
        ViewBag.Categorias = categorias;
        return View("~/Views/RecursosHumanos/Capacitacion/Admin/Index.cshtml", cursos);
    }

    // GET /RecursosHumanos/CapacitacionAdmin/CursoForm?id=5  (0=nuevo)
    [HttpGet]
    public async Task<IActionResult> CursoForm(int id = 0)
    {
        var categorias = await _capSvc.GetCategoriasAsync();
        ViewBag.Categorias = categorias;

        CapCurso curso = new();
        if (id > 0)
        {
            var existente = await _capSvc.GetCursoDetalleAsync(id, UsuarioActual);
            if (existente != null) curso = existente;
        }

        return View("~/Views/RecursosHumanos/Capacitacion/Admin/CursoForm.cshtml", curso);
    }

    // POST /RecursosHumanos/CapacitacionAdmin/GuardarContenido
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarContenido(int idCurso, string titulo, string tipo,
        int orden, IFormFile? archivo, string? urlExterna, string? contenidoHtml,
        int duracionSeg = 0, string obligatorio = "S", int? idSeccion = null)
    {
        string rutaArchivo = "";
        string mimeType    = "";
        string nombreOri   = "";
        long   tamanio     = 0;

        if (archivo != null && archivo.Length > 0 && tipo is "VID" or "PDF" or "ARC")
        {
            var (ok, msg, ruta, mime) = await _media.GuardarArchivoAsync(archivo, idCurso);
            if (!ok) return Json(new { ok = false, msg });
            rutaArchivo = ruta;
            mimeType    = mime;
            nombreOri   = Path.GetFileName(archivo.FileName);
            tamanio     = archivo.Length;
        }

        // Guardar en BD usando Dapper directo (sin servicio específico por simplicidad)
        await using var db = new OracleConnection(Configuration.GetConnectionString(
            HttpContext.Session.GetString("EmpresaConexion") ?? "LaColonialConnection") ?? "");

        await db.ExecuteAsync(
            $@"INSERT INTO {S()}CAP_CONTENIDO
               (ID_CONTENIDO, ID_CURSO, TITULO, TIPO, ORDEN, RUTA_ARCHIVO, NOMBRE_ARCH_ORI,
                TAMANIO_BYTES, MIME_TYPE, URL_EXTERNA, CONTENIDO_HTML, DURACION_SEG,
                OBLIGATORIO, ID_SECCION, ACTIVO)
               VALUES ({S()}CAP_SEQ_CONTENIDO.NEXTVAL, :cur, :tit, :tipo, :ord, :ruta, :nom,
                       :tam, :mime, :url, :html, :dur, :oblig, :sec, 'S')",
            new {
                cur = idCurso, tit = titulo, tipo, ord = orden,
                ruta = (object?)rutaArchivo.NullIfEmpty() ?? DBNull.Value,
                nom  = (object?)nombreOri.NullIfEmpty()  ?? DBNull.Value,
                tam  = tamanio > 0 ? (object)tamanio : DBNull.Value,
                mime = (object?)mimeType.NullIfEmpty()   ?? DBNull.Value,
                url  = (object?)urlExterna               ?? DBNull.Value,
                html = (object?)contenidoHtml            ?? DBNull.Value,
                dur  = duracionSeg > 0 ? (object)duracionSeg : DBNull.Value,
                oblig = obligatorio,
                sec  = (object?)idSeccion ?? DBNull.Value
            });

        return Json(new { ok = true, msg = "Contenido guardado." });
    }

    // GET /RecursosHumanos/CapacitacionAdmin/Reportes
    [HttpGet]
    public async Task<IActionResult> Reportes(int? idCurso)
    {
        var cursos = await _capSvc.GetCatalogoAsync(UsuarioActual);
        ViewBag.Cursos = cursos;
        ViewBag.IdCursoActivo = idCurso;

        if (idCurso.HasValue)
        {
            var inscritos = await _capSvc.GetInscripcionesAsync(idCurso.Value);
            return View("~/Views/RecursosHumanos/Capacitacion/Admin/Reportes.cshtml", inscritos);
        }

        return View("~/Views/RecursosHumanos/Capacitacion/Admin/Reportes.cshtml",
            new List<CapInscripcion>());
    }

    // ── Helper ──────────────────────────────────────────────────────────────
    // Necesario porque este controller no hereda directamente de OracleServiceBase
    private string S()
    {
        return HttpContext.Session.GetString("EmpresaConexion") switch
        {
            "ArbonaConnection" => "ARBONA.",
            "SolsaConnection"  => "SOLSA.",
            _                  => "SIG."
        };
    }
}

internal static class StringExt
{
    public static string? NullIfEmpty(this string? s) =>
        string.IsNullOrEmpty(s) ? null : s;
}
