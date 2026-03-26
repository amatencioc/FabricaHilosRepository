namespace FabricaHilos.LecturaCorreos.Services.Email.Portales;

using FabricaHilos.LecturaCorreos.Models;

public interface IPortalDescargaService
{
    /// <summary>
    /// Navega a la URL del portal, descarga XML, PDF y CDR,
    /// y los devuelve como adjuntos listos para el pipeline normal.
    /// </summary>
    Task<List<AdjuntoCorreo>> DescargarAdjuntosAsync(EnlacePortal enlace, CancellationToken ct);
}
