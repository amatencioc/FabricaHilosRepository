using FabricaHilos.Sire.Constants;
using FabricaHilos.Sire.Interfaces;
using FabricaHilos.Sire.Models;
using FabricaHilos.Sire.Options;
using Microsoft.Extensions.Options;

namespace FabricaHilos.Sire.Services;

public sealed class SireComprasService : SireServiceBase, ISireComprasService
{
    protected override string LibroNombre => "RCE";

    public SireComprasService(HttpClient httpClient, ISireAuthService authService, IOptions<SireOptions> options)
        : base(httpClient, authService, options) { }

    public Task<IReadOnlyList<PropuestaDto>> ObtenerPeriodosAsync(CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<PropuestaDto>>(HttpMethod.Get, SireEndpoints.RcePeriodos, null, cancellationToken);

    public Task<IReadOnlyList<RegistroCompra>> ObtenerPropuestaAsync(string periodo, CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<RegistroCompra>>(HttpMethod.Get, SireEndpoints.RcePropuesta(periodo), null, cancellationToken);

    public Task<TicketEstado> AceptarPropuestaAsync(string periodo, CancellationToken cancellationToken = default)
        => SendAsync<TicketEstado>(HttpMethod.Post, SireEndpoints.RceAceptar(periodo), new { }, cancellationToken);

    public Task<TicketEstado> ReemplazarPropuestaAsync(string periodo, Stream contenidoArchivo, string nombreArchivo, CancellationToken cancellationToken = default)
        => SendMultipartAsync(SireEndpoints.RceReemplazo(periodo), contenidoArchivo, nombreArchivo, cancellationToken);

    public Task<TicketEstado> CerrarPeriodoAsync(string periodo, CancellationToken cancellationToken = default)
        => SendAsync<TicketEstado>(HttpMethod.Post, SireEndpoints.RceCierre(periodo), new { }, cancellationToken);

    public Task<ConstanciaCierre> DescargarConstanciaAsync(string periodo, CancellationToken cancellationToken = default)
        => DescargarConstanciaBaseAsync(SireEndpoints.RceConstancia(periodo), $"RCE_Constancia_{periodo}.pdf", cancellationToken);
}
