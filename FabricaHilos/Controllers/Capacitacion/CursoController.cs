using FabricaHilos.Models.Capacitacion;
using FabricaHilos.Services.Capacitacion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FabricaHilos.Controllers.Capacitacion;

[Authorize]
[Route("RecursosHumanos/Curso/[action]")]
public class CursoController : OracleBaseController
{
    private readonly ICapacitacionService _svc;
    private readonly ContenidoMediaService _media;

    public CursoController(ICapacitacionService svc, ContenidoMediaService media)
    {
        _svc   = svc;
        _media = media;
    }

    private string UsuarioActual => HttpContext.Session.GetString("OracleUser") ?? "";

    // GET /RecursosHumanos/Curso/Detalle/5
    [HttpGet("{idCurso:int}")]
    public async Task<IActionResult> Detalle(int idCurso)
    {
        var curso = await _svc.GetCursoDetalleAsync(idCurso, UsuarioActual);
        if (curso == null) return NotFound();

        CapCurso? requisito = null;
        bool requisitoCumplido = true;
        string? mensajeReq = null;

        if (curso.IdCursoRequisito.HasValue)
        {
            requisito = await _svc.GetCursoDetalleAsync(curso.IdCursoRequisito.Value, UsuarioActual);
            requisitoCumplido = await _svc.ValidarRequisitoAsync(idCurso, UsuarioActual);
            if (!requisitoCumplido)
                mensajeReq = $"Debe completar \"{requisito?.Titulo}\" y aprobar el examen antes de inscribirse.";
        }

        // Obtener secciones con contenidos para el currículum
        var player = curso.EstaInscrito
            ? await _svc.GetPlayerAsync(idCurso, 0, UsuarioActual)
            : null;

        var vm = new CursoDetalleVm
        {
            Curso                = curso,
            Secciones            = player?.Secciones ?? [],
            ContenidosSinSeccion = player?.ContenidosSinSeccion ?? [],
            CursoRequisito       = requisito,
            RequisitoSatisfecho  = requisitoCumplido,
            MensajeRequisito     = mensajeReq,
        };

        return View("~/Views/RecursosHumanos/Capacitacion/Curso/Detalle.cshtml", vm);
    }

    // GET /RecursosHumanos/Curso/Ver/5?contenido=123
    [HttpGet("{idCurso:int}")]
    public async Task<IActionResult> Ver(int idCurso, long? contenido)
    {
        var player = await _svc.GetPlayerAsync(idCurso, contenido ?? 0, UsuarioActual);
        if (player == null)
            return RedirectToAction("Detalle", new { idCurso });

        return View("~/Views/RecursosHumanos/Capacitacion/Curso/Ver.cshtml", player);
    }

    // POST /RecursosHumanos/Curso/MarcarCompletado  (AJAX)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarcarCompletado(long idInscripcion, long idContenido, int segReproducido)
    {
        var ok = await _svc.MarcarCompletadoAsync(idInscripcion, idContenido, segReproducido);
        return Json(new { ok });
    }

    // GET /RecursosHumanos/Curso/ServirPortada/5
    // Sirve la imagen de portada del curso (pública dentro de la sesión autenticada)
    [HttpGet("{idCurso:int}")]
    public async Task<IActionResult> ServirPortada(int idCurso)
    {
        var curso = await _svc.GetCursoDetalleAsync(idCurso, UsuarioActual);
        if (curso == null || string.IsNullOrEmpty(curso.ImagenPortada))
            return NotFound();

        var (ok, fs, mime, _) = _media.ObtenerArchivo(curso.ImagenPortada, "");
        if (!ok || fs == null)
            return NotFound();

        Response.Headers.Append("Cache-Control", "private, max-age=3600");
        return File(fs, mime);
    }

    // GET /RecursosHumanos/Curso/ServirMedia/123
    // Sirve archivos multimedia de forma segura (verifica inscripción activa)
    [HttpGet("{idContenido:long}")]
    public async Task<IActionResult> ServirMedia(long idContenido)
    {
        // Verificar que el usuario tiene acceso a este contenido
        var player = await _svc.GetPlayerAsync(0, idContenido, UsuarioActual);
        if (player == null) return Forbid();

        var (ok, fs, mime, descarga) = _media.ObtenerArchivo(
            player.Actual.RutaArchivo ?? "",
            player.Actual.NombreArchOri ?? player.Actual.Titulo);

        if (!ok || fs == null) return NotFound();

        // Para video: soportar Range requests
        if (mime.StartsWith("video/"))
            return File(fs, mime, enableRangeProcessing: true);

        // Para PDF: inline en el browser
        Response.Headers.Append("Content-Disposition", $"inline; filename=\"{descarga}\"");
        return File(fs, mime);
    }
}
