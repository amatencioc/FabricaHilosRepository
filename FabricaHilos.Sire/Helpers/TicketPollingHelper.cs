using FabricaHilos.Sire.Models;
using FabricaHilos.Sire.Options;
using Microsoft.Extensions.Options;

namespace FabricaHilos.Sire.Helpers;

public sealed class TicketPollingHelper
{
    private readonly SireOptions _options;

    public TicketPollingHelper(IOptions<SireOptions> options)
    {
        _options = options.Value;
    }

    public async Task<TicketEstado> EsperarEstadoFinalAsync(
        Func<CancellationToken, Task<TicketEstado>> consultaEstadoAsync,
        CancellationToken cancellationToken = default)
    {
        TicketEstado? ultimoEstado = null;

        for (var intento = 0; intento < _options.TicketMaxRetries; intento++)
        {
            ultimoEstado = await consultaEstadoAsync(cancellationToken);
            if (ultimoEstado.EsFinal)
            {
                return ultimoEstado;
            }

            await Task.Delay(_options.TicketPollIntervalMs, cancellationToken);
        }

        return ultimoEstado ?? new TicketEstado
        {
            Estado = "ERROR",
            Mensaje = "No se pudo obtener estado final del ticket en el número máximo de intentos."
        };
    }
}
