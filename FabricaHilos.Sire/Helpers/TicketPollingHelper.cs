using System.Net;
using FabricaHilos.Sire.Models;
using FabricaHilos.Sire.Options;
using FabricaHilos.Sire.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FabricaHilos.Sire.Helpers;

public sealed class TicketPollingHelper
{
    private readonly SireOptions _options;
    private readonly ILogger<TicketPollingHelper> _logger;

    public TicketPollingHelper(IOptions<SireOptions> options, ILogger<TicketPollingHelper> logger)
    {
        _options = options.Value;
        _logger  = logger;
    }

    public async Task<TicketEstado> EsperarEstadoFinalAsync(
        Func<CancellationToken, Task<TicketEstado>> consultaEstadoAsync,
        CancellationToken cancellationToken = default)
    {
        TicketEstado? ultimoEstado = null;

        for (var intento = 0; intento < _options.TicketMaxRetries; intento++)
        {
            try
            {
                ultimoEstado = await consultaEstadoAsync(cancellationToken);
                if (ultimoEstado.EsFinal)
                {
                    return ultimoEstado;
                }
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Timeout del HttpClient (no es un shutdown): SUNAT tardó más de lo esperado.
                // Se continúa el ciclo de polling en el siguiente intento.
                _logger.LogWarning("[SIRE-POLLING] Timeout de red en intento {Intento}/{Max}. Reintentando...",
                    intento + 1, _options.TicketMaxRetries);
            }
            catch (HttpRequestException ex)
            {
                // Error de red transitorio (conexión caída, DNS, etc.).
                _logger.LogWarning(ex, "[SIRE-POLLING] Error de red en intento {Intento}/{Max}. Reintentando...",
                    intento + 1, _options.TicketMaxRetries);
            }
            catch (SireApiException ex) when (ex.StatusCode is
                HttpStatusCode.BadGateway or
                HttpStatusCode.ServiceUnavailable or
                HttpStatusCode.GatewayTimeout)
            {
                // SUNAT devuelve 502/503/504 cuando su infraestructura está saturada.
                // Es un error transitorio: se espera el intervalo normal y se reintenta.
                _logger.LogWarning("[SIRE-POLLING] SUNAT respondió {StatusCode} en intento {Intento}/{Max}. Reintentando...",
                    (int?)ex.StatusCode, intento + 1, _options.TicketMaxRetries);
            }

            await Task.Delay(_options.TicketPollIntervalMs, cancellationToken);
        }

        // Se agotaron los reintentos sin llegar a un estado final (SUNAT sigue procesando).
        // Se retorna un estado especial TIMEOUT para que el worker lo maneje de forma explícita.
        return new TicketEstado
        {
            NumTicket = ultimoEstado?.NumTicket ?? string.Empty,
            Estado    = "TIMEOUT",
            Mensaje   = $"El ticket no alcanzó un estado final tras {_options.TicketMaxRetries} intentos "
                      + $"({_options.TicketMaxRetries * _options.TicketPollIntervalMs / 1000} s). "
                      + $"Último estado SUNAT: {ultimoEstado?.Estado ?? "desconocido"}."
        };
    }
}
