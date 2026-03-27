namespace FabricaHilos.LecturaCorreos.Services.Email.Portales;

using FabricaHilos.LecturaCorreos.Models;
using Microsoft.Extensions.Logging;

/// <summary>
/// Descarga XML, PDF y CDR desde el portal ASPX WebForms de AS ADUANAS (asaduanas.com),
/// que redirige al portal softpad.com.pe.
/// Estrategia:
///   1. GET de la pagina → intentar links directos (a[href]) para los 3 documentos (XML, CDR, PDF).
///   2. Si el CDR link devuelve HTML (CDR aún no disponible en portal) → intentar via formulario ASPX (__doPostBack).
///   3. Si tampoco se obtiene, el CDR se verificará luego via SUNAT.
/// Los 3 documentos se descargan de la misma manera (GET directo); no se aplican
/// estrategias de extracción en cascada desde páginas vista.
/// </summary>
public class AsaduanasPortalService : PortalDescargaBase
{
    public AsaduanasPortalService(ILogger<AsaduanasPortalService> logger)
        : base(logger) { }

    public override Task<List<AdjuntoCorreo>> DescargarAdjuntosAsync(
        EnlacePortal enlace, CancellationToken ct)
        => DescargarDesdePortalAsync(enlace, "Asaduanas/softpad", ct);
}
