namespace FabricaHilos.LecturaCorreos.Services.Email.Portales;

using FabricaHilos.LecturaCorreos.Models;
using Microsoft.Extensions.Logging;

/// <summary>
/// Descarga XML y PDF desde links directos incluidos en el cuerpo del correo.
/// Correos de efacturacion.pe incluyen las URLs de descarga directamente en el HTML:
///   "El comprobante en formato XML lo puede obtener accediendo aquí."
/// No se necesita navegar ningún portal ni enviar formularios.
/// </summary>
public class EfacturacionPortalService : PortalDescargaBase
{
    public EfacturacionPortalService(ILogger<EfacturacionPortalService> logger)
        : base(logger) { }

    public override async Task<List<AdjuntoCorreo>> DescargarAdjuntosAsync(
        EnlacePortal enlace, CancellationToken ct)
    {
        var resultado = new List<AdjuntoCorreo>();

        _logger.LogInformation(
            "Portal efacturacion.pe: links directos. XML={Xml} PDF={Pdf}",
            enlace.UrlXmlDirecto ?? "-", enlace.UrlPdfDirecto ?? "-");

        using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

        if (enlace.UrlXmlDirecto is not null)
            resultado.AddRange(await DescargarGetAsync(http, enlace.UrlXmlDirecto, "XML", enlace, ct));
        if (enlace.UrlPdfDirecto is not null)
            resultado.AddRange(await DescargarGetAsync(http, enlace.UrlPdfDirecto, "PDF", enlace, ct));

        return resultado;
    }
}
