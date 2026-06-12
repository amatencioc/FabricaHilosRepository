using System.Threading.Channels;

namespace FabricaHilos.Services.Sire;

/// <summary>
/// Cola de jobs de exportación SIRE. Permite al controlador encolar un job
/// para que el BackgroundService SireExportacionWorker lo procese de forma asíncrona.
/// </summary>
public interface ISireExportacionQueue
{
    /// <summary>Encola un jobId para ser procesado por el worker.</summary>
    void Encolar(int jobId);

    /// <summary>Espera y retorna el siguiente jobId pendiente de procesar.</summary>
    ValueTask<int> DequeueAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Implementación basada en System.Threading.Channels.Channel para comunicación
/// thread-safe entre el controlador (productor) y el BackgroundService (consumidor).
/// </summary>
public sealed class SireExportacionQueue : ISireExportacionQueue
{
    private readonly Channel<int> _channel = Channel.CreateUnbounded<int>(
        new UnboundedChannelOptions { SingleReader = true });

    public void Encolar(int jobId) =>
        _channel.Writer.TryWrite(jobId);

    public ValueTask<int> DequeueAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAsync(cancellationToken);
}
