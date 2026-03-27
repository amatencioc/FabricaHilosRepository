namespace FabricaHilos.LecturaCorreos.Services.Email.Portales;

using FabricaHilos.LecturaCorreos.Models;

/// <summary>
/// Orquestador que enruta cada <see cref="EnlacePortal"/> al servicio de descarga correcto
/// según el origen del correo detectado:
///   - efacturacion.pe : links directos XML/PDF en el cuerpo del correo.
///   - bizlinks.la     : portal JSF (botón "Consultar" en el correo).
///   - asaduanas.com   : portal ASPX WebForms → softpad.com.pe (link "Ver documento").
/// </summary>
public class PortalDescargaOrquestador : IPortalDescargaService
{
    private readonly EfacturacionPortalService _efacturacion;
    private readonly BizlinksPortalService     _bizlinks;
    private readonly AsaduanasPortalService    _asaduanas;

    public PortalDescargaOrquestador(
        EfacturacionPortalService efacturacion,
        BizlinksPortalService     bizlinks,
        AsaduanasPortalService    asaduanas)
    {
        _efacturacion = efacturacion;
        _bizlinks     = bizlinks;
        _asaduanas    = asaduanas;
    }

    public Task<List<AdjuntoCorreo>> DescargarAdjuntosAsync(
        EnlacePortal enlace, CancellationToken ct)
    {
        if (enlace.TieneLinksDirectos)
            return _efacturacion.DescargarAdjuntosAsync(enlace, ct);

        if (enlace.UrlConsultar.Contains("bizlinks", StringComparison.OrdinalIgnoreCase))
            return _bizlinks.DescargarAdjuntosAsync(enlace, ct);

        // asaduanas.com → softpad.com.pe y cualquier otro portal ASPX WebForms
        return _asaduanas.DescargarAdjuntosAsync(enlace, ct);
    }
}
