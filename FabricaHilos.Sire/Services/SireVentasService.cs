using FabricaHilos.Sire.Constants;
using FabricaHilos.Sire.Interfaces;
using FabricaHilos.Sire.Models;
using FabricaHilos.Sire.Options;
using Microsoft.Extensions.Options;

namespace FabricaHilos.Sire.Services;

public sealed class SireVentasService : SireServiceBase, ISireVentasService
{
    protected override string LibroNombre => "RVIE";

    public SireVentasService(HttpClient httpClient, ISireAuthService authService, IOptions<SireOptions> options)
        : base(httpClient, authService, options) { }

    public Task<IReadOnlyList<PropuestaDto>> ObtenerPeriodosAsync(CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<PropuestaDto>>(HttpMethod.Get, SireEndpoints.RviePeriodos, null, cancellationToken);

    public Task<IReadOnlyList<RegistroVenta>> ObtenerPropuestaAsync(string periodo, CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<RegistroVenta>>(HttpMethod.Get, SireEndpoints.RviePropuesta(periodo), null, cancellationToken);

    public Task<TicketEstado> AceptarPropuestaAsync(string periodo, CancellationToken cancellationToken = default)
        => SendAsync<TicketEstado>(HttpMethod.Post, SireEndpoints.RvieAceptar(periodo), new { }, cancellationToken);

    public Task<TicketEstado> ReemplazarPropuestaAsync(string periodo, Stream contenidoArchivo, string nombreArchivo, CancellationToken cancellationToken = default)
        => SendMultipartAsync(SireEndpoints.RvieReemplazo(periodo), contenidoArchivo, nombreArchivo, cancellationToken);

    public Task<TicketEstado> CerrarPeriodoAsync(string periodo, CancellationToken cancellationToken = default)
        => SendAsync<TicketEstado>(HttpMethod.Post, SireEndpoints.RvieCierre(periodo), new { }, cancellationToken);

    public Task<ConstanciaCierre> DescargarConstanciaAsync(string periodo, CancellationToken cancellationToken = default)
        => DescargarConstanciaBaseAsync(SireEndpoints.RvieConstancia(periodo), $"RVIE_Constancia_{periodo}.pdf", cancellationToken);
}
