using FabricaHilos.Sire.Interfaces;
using FabricaHilos.Sire.Models;
using Microsoft.Extensions.Logging;

namespace FabricaHilos.Sire.Services.Mock;

/// <summary>
/// Mock de <see cref="ITusUploadService"/> para entornos sin conectividad a SUNAT.
/// Simula el flujo TUS sin realizar llamadas HTTP reales.
/// </summary>
public sealed class TusUploadServiceMock : ITusUploadService
{
    private readonly ILogger<TusUploadServiceMock> _logger;
    private static int _ticketCounter = 900000;

    public TusUploadServiceMock(ILogger<TusUploadServiceMock> logger)
    {
        _logger = logger;
    }

    public Task<TusUploadResult> ReemplazarPropuestaRceAsync(
        Stream archivoZip, string periodo, string nombreArchivo,
        CancellationToken cancellationToken = default)
        => SimularSubidaAsync(archivoZip, periodo, nombreArchivo, "RCE", cancellationToken);

    public Task<TusUploadResult> ReemplazarPropuestaRvieAsync(
        Stream archivoZip, string periodo, string nombreArchivo,
        CancellationToken cancellationToken = default)
        => SimularSubidaAsync(archivoZip, periodo, nombreArchivo, "RVIE", cancellationToken);

    public Task<TusUploadResult> SubirArchivoAsync(
        Stream archivoStream,
        TusUploadOptions options,
        CancellationToken cancellationToken = default)
        => SimularSubidaAsync(archivoStream, options.PerTributario, options.NombreArchivoImportacion,
            $"Proceso-{options.CodProceso}", cancellationToken);

    // ──────────────────────────────────────────────
    private async Task<TusUploadResult> SimularSubidaAsync(
        Stream stream, string periodo, string nombreArchivo, string tipo,
        CancellationToken cancellationToken)
    {
        // Simula latencia de red
        await Task.Delay(400, cancellationToken);

        var ticketNum = Interlocked.Increment(ref _ticketCounter);
        var ticket = $"2024{ticketNum}";
        var bytes = stream.CanSeek ? stream.Length : 0;

        _logger.LogInformation("[MOCK] TUS Upload {Tipo}: Archivo={Archivo} Periodo={Periodo} → Ticket={Ticket}",
            tipo, nombreArchivo, periodo, ticket);

        return TusUploadResult.Ok(ticket, bytes, $"[MOCK] Subida simulada exitosa. Ticket: {ticket}");
    }
}
