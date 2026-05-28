using FabricaHilos.Sire.Models;

namespace FabricaHilos.Sire.Interfaces;

public interface ISireVentasService
{
    Task<IReadOnlyList<PropuestaDto>> ObtenerPeriodosAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RegistroVenta>> ObtenerPropuestaAsync(string periodo, CancellationToken cancellationToken = default);
    Task<TicketEstado> AceptarPropuestaAsync(string periodo, CancellationToken cancellationToken = default);
    Task<TicketEstado> ReemplazarPropuestaAsync(string periodo, Stream contenidoArchivo, string nombreArchivo, CancellationToken cancellationToken = default);
    Task<TicketEstado> CerrarPeriodoAsync(string periodo, CancellationToken cancellationToken = default);
    Task<ConstanciaCierre> DescargarConstanciaAsync(string periodo, CancellationToken cancellationToken = default);
}
