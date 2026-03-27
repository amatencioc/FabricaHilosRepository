namespace FabricaHilos.LecturaCorreos.Services.Email.Portales;

using FabricaHilos.LecturaCorreos.Models;
using Microsoft.Extensions.Logging;

/// <summary>
/// Descarga XML, PDF y CDR desde el portal JSF de Bizlinks (bizlinks.la).
/// Estrategia:
///   1. GET de la pagina -> intentar links directos (a[href]).
///   2. Si no hay links directos -> envio de formulario JSF con cookies de sesion.
/// </summary>
public class BizlinksPortalService : PortalDescargaBase
{
    public BizlinksPortalService(ILogger<BizlinksPortalService> logger)
        : base(logger) { }

    public override Task<List<AdjuntoCorreo>> DescargarAdjuntosAsync(
        EnlacePortal enlace, CancellationToken ct)
        => DescargarDesdePortalAsync(enlace, "Bizlinks", ct);
}
