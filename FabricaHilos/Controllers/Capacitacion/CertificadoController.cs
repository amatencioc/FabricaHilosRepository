using FabricaHilos.Services;
using FabricaHilos.Services.Capacitacion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FabricaHilos.Controllers.Capacitacion;

[Authorize]
[Route("RecursosHumanos/Certificado/[action]")]
public class CertificadoController : OracleBaseController
{
    private readonly ICertificadoService  _svc;
    private readonly IEmpresaTemaService  _tema;
    private readonly IWebHostEnvironment  _env;

    public CertificadoController(
        ICertificadoService svc,
        IEmpresaTemaService tema,
        IWebHostEnvironment env)
    {
        _svc  = svc;
        _tema = tema;
        _env  = env;
    }

    private string UsuarioActual => HttpContext.Session.GetString("OracleUser") ?? "";

    // GET /RecursosHumanos/Certificado/Ver/5  — abre la vista HTML imprimible
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Ver(int id)
    {
        var cert = await _svc.GetAsync(id, UsuarioActual);
        if (cert == null) return NotFound();

        var empresa = _tema.GetTemaActual();
        ViewBag.EmpresaNombre   = empresa.NombreCompleto;
        ViewBag.EmpresaNombreC  = empresa.NombreCorto;
        ViewBag.EmpresaRuc      = empresa.Ruc;
        ViewBag.LogoUrl         = string.IsNullOrWhiteSpace(empresa.LogoFullPath)
                                ? null
                                : $"/{empresa.LogoFullPath.TrimStart('/')}";
        ViewBag.LogoIconUrl     = string.IsNullOrWhiteSpace(empresa.LogoIconPath)
                                ? null
                                : $"/{empresa.LogoIconPath.TrimStart('/')}";

        return View("~/Views/RecursosHumanos/Capacitacion/Certificado/Ver.cshtml", cert);
    }

    // GET /RecursosHumanos/Certificado/VerPrimero — abre el último certificado del usuario
    [HttpGet]
    public async Task<IActionResult> VerPrimero()
    {
        var cert = await _svc.GetPrimeroAsync(UsuarioActual);
        if (cert == null)
            return RedirectToAction("MiPanel", "Capacitacion");

        return RedirectToAction(nameof(Ver), new { id = cert.IdCertificado });
    }

    // GET /RecursosHumanos/Certificado/Verificar?c=GUID  (público, sin autenticación)
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Verificar(string c)
    {
        if (string.IsNullOrWhiteSpace(c))
            return View("~/Views/RecursosHumanos/Capacitacion/Certificado/Verificar.cshtml", null);

        if (!Guid.TryParse(c, out _))
            return View("~/Views/RecursosHumanos/Capacitacion/Certificado/Verificar.cshtml", null);

        var cert = await _svc.GetByCodigoAsync(c);
        return View("~/Views/RecursosHumanos/Capacitacion/Certificado/Verificar.cshtml", cert);
    }

    // GET /RecursosHumanos/Certificado/VerificarJson?c=GUID  — para AJAX desde el panel
    [HttpGet]
    public async Task<IActionResult> VerificarJson(string c)
    {
        if (string.IsNullOrWhiteSpace(c) || !Guid.TryParse(c, out _))
            return Json(new { encontrado = false });

        var cert = await _svc.GetByCodigoAsync(c);
        if (cert == null)
            return Json(new { encontrado = false });

        return Json(new
        {
            encontrado     = true,
            idCertificado  = cert.IdCertificado,
            nombreUsuario  = cert.NombreUsuario,
            tituloCurso    = cert.TituloCurso,
            puntajeObt     = cert.PuntajeObt.ToString("F0"),
            fchEmision     = cert.FchEmision.ToString("dd/MM/yyyy"),
            fchVencimiento = cert.FchVencimiento.HasValue ? cert.FchVencimiento.Value.ToString("dd/MM/yyyy") : (string?)null,
            esVigente      = cert.EsVigente,
            estadoTexto    = cert.EstadoTexto,
            codigoVerif    = cert.CodigoVerif
        });
    }
}
