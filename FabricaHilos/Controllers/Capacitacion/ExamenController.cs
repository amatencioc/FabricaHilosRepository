using FabricaHilos.Services.Capacitacion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FabricaHilos.Controllers.Capacitacion;

[Authorize]
[Route("RecursosHumanos/Examen/[action]")]
public class ExamenController : OracleBaseController
{
    private readonly IExamenService       _examSvc;
    private readonly ICertificadoService  _certSvc;
    private readonly ICapacitacionService _capSvc;

    public ExamenController(IExamenService examSvc, ICertificadoService certSvc, ICapacitacionService capSvc)
    {
        _examSvc = examSvc;
        _certSvc = certSvc;
        _capSvc  = capSvc;
    }

    private string UsuarioActual => HttpContext.Session.GetString("OracleUser") ?? "";

    // GET /RecursosHumanos/Examen/IniciarExamen/5?idInscripcion=77
    [HttpGet("{idExamen:int}")]
    public async Task<IActionResult> IniciarExamen(int idExamen, long idInscripcion)
    {
        var (ok, msg, idIntento) = await _examSvc.IniciarIntentoAsync(idExamen, idInscripcion, UsuarioActual);
        if (!ok)
        {
            TempData["Error"] = msg;
            return RedirectToAction("MiPanel", "Capacitacion");
        }

        return RedirectToAction("Rendir", new { idIntento, nro = 0 });
    }

    // GET /RecursosHumanos/Examen/Rendir?idIntento=11&nro=0
    [HttpGet]
    public async Task<IActionResult> Rendir(long idIntento, int nro = 0)
    {
        var vm = await _examSvc.GetRendirVmAsync(idIntento, nro);
        if (vm == null) return RedirectToAction("MiPanel", "Capacitacion");

        // Anti-trampa: verificar tiempo servidor
        if (!await _examSvc.ValidarTiempoAsync(idIntento))
        {
            await _examSvc.ProcesarYCerrarAsync(idIntento, UsuarioActual);
            TempData["Warning"] = "El tiempo del examen ha finalizado.";
            return RedirectToAction("Resultado", new { idIntento });
        }

        return View("~/Views/RecursosHumanos/Capacitacion/Examen/Rendir.cshtml", vm);
    }

    // POST /RecursosHumanos/Examen/GuardarRespuesta  (AJAX)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarRespuesta(long idIntento, long idPregunta, long idOpcion)
    {
        var ok = await _examSvc.GuardarRespuestaAsync(idIntento, idPregunta, idOpcion);
        return Json(new { ok });
    }

    // POST /RecursosHumanos/Examen/GuardarTexto  (AJAX para RC/ENS)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarTexto(long idIntento, long idPregunta, string texto)
    {
        var ok = await _examSvc.GuardarRespuestaTextoAsync(idIntento, idPregunta, texto);
        return Json(new { ok });
    }

    // POST /RecursosHumanos/Examen/Enviar
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enviar(long idIntento, long idInscripcion)
    {
        var resultado = await _examSvc.ProcesarYCerrarAsync(idIntento, UsuarioActual);
        if (resultado == null)
            return RedirectToAction("MiPanel", "Capacitacion");

        // Si aprobó y el curso tiene certificado, emitirlo automáticamente
        if (resultado.Aprobado && resultado.TieneCertificado)
            await _certSvc.EmitirAsync(idIntento, idInscripcion, UsuarioActual);

        return RedirectToAction("Resultado", new { idIntento });
    }

    // GET /RecursosHumanos/Examen/Resultado?idIntento=11
    [HttpGet]
    public async Task<IActionResult> Resultado(long idIntento)
    {
        var vm = await _examSvc.GetResultadoAsync(idIntento, UsuarioActual);
        if (vm == null) return RedirectToAction("MiPanel", "Capacitacion");

        return View("~/Views/RecursosHumanos/Capacitacion/Examen/Resultado.cshtml", vm);
    }
}
