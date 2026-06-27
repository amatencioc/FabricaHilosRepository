using FabricaHilos.Services.Capacitacion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FabricaHilos.Controllers.Capacitacion;

[Authorize]
[Route("RecursosHumanos/Certificado/[action]")]
public class CertificadoController : OracleBaseController
{
    private readonly ICertificadoService _svc;

    public CertificadoController(ICertificadoService svc) => _svc = svc;

    private string UsuarioActual => HttpContext.Session.GetString("OracleUser") ?? "";

    // GET /RecursosHumanos/Certificado/MiCertificado/5
    [HttpGet("{id:int}")]
    public async Task<IActionResult> MiCertificado(int id)
    {
        var cert = await _svc.GetAsync(id, UsuarioActual);
        if (cert == null) return NotFound();
        return View("~/Views/RecursosHumanos/Capacitacion/Certificado/MiCertificado.cshtml", cert);
    }

    // GET /RecursosHumanos/Certificado/Descargar/5
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Descargar(int id)
    {
        var cert = await _svc.GetAsync(id, UsuarioActual);
        if (cert == null) return NotFound();

        var pdf = await _svc.GenerarPdfAsync(id);
        if (pdf == null)
            return BadRequest("No se pudo generar el certificado. Intente más tarde.");

        var nombreArchivo = $"Certificado_{cert.TituloCurso.Replace(" ", "_")}_{cert.FchEmision:yyyyMMdd}.pdf";
        return File(pdf, "application/pdf", nombreArchivo);
    }

    // GET /RecursosHumanos/Certificado/Verificar?c=GUID  (público, sin autenticación)
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Verificar(string c)
    {
        if (string.IsNullOrWhiteSpace(c))
            return View("~/Views/RecursosHumanos/Capacitacion/Certificado/Verificar.cshtml", null);

        // Validar formato GUID para prevenir inyección
        if (!Guid.TryParse(c, out _))
            return View("~/Views/RecursosHumanos/Capacitacion/Certificado/Verificar.cshtml", null);

        var cert = await _svc.GetByCodigoAsync(c);
        return View("~/Views/RecursosHumanos/Capacitacion/Certificado/Verificar.cshtml", cert);
    }
}
