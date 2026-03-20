namespace FabricaHilos.LecturaCorreos.Services.Signals;

/// <summary>
/// Señal de sincronización entre workers: <see cref="SunatCdrWorker"/> espera a que
/// <see cref="LecturaCorreosSunatCdrWorker"/> complete la limpieza de tablas antes
/// de iniciar su primer ciclo de consulta CDR.
/// </summary>
public interface ILimpiezaSignal
{
    /// <summary>Espera hasta que la limpieza haya finalizado (o si no aplica, retorna inmediatamente).</summary>
    Task EsperarAsync(CancellationToken ct = default);

    /// <summary>Indica que la limpieza terminó (o que no era necesaria).</summary>
    void Completar();
}

public sealed class LimpiezaSignal : ILimpiezaSignal
{
    private readonly TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task EsperarAsync(CancellationToken ct = default) =>
        _tcs.Task.WaitAsync(ct);

    public void Completar() =>
        _tcs.TrySetResult();
}
