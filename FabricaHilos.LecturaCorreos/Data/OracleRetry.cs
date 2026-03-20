namespace FabricaHilos.LecturaCorreos.Data;

using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;

/// <summary>
/// Reintenta operaciones Oracle ante errores transitorios de red/conexión
/// con backoff escalonado: 500 ms → 1,5 s → 3 s.
/// </summary>
internal static class OracleRetry
{
    // Errores Oracle que indican problemas transitorios de red o disponibilidad.
    private static readonly HashSet<int> ErroresTransitorios =
    [
        28,     // Session killed
        1033,   // ORACLE initialization or shutdown in progress
        1089,   // Immediate shutdown in progress
        3113,   // End-of-file on communication channel
        3114,   // Not connected to ORACLE
        12150, 12152, 12153, 12157, 12170,  // TNS communication errors
        12203, 12224, 12500, 12535, 12537,  // TNS listener/connection errors
        12541, 12543,                        // TNS: no listener / host unreachable
    ];

    private static readonly TimeSpan[] Backoff =
    [
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromMilliseconds(1_500),
        TimeSpan.FromMilliseconds(3_000),
    ];

    /// <summary>Ejecuta <paramref name="operacion"/> con reintentos para errores transitorios Oracle.</summary>
    internal static async Task<T> EjecutarAsync<T>(
        Func<Task<T>> operacion,
        ILogger        logger,
        string         nombreOperacion,
        CancellationToken ct = default)
    {
        // Los primeros Backoff.Length intentos atrapan errores transitorios y esperan.
        for (int intento = 0; intento < Backoff.Length; intento++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                return await operacion();
            }
            catch (OracleException ex) when (ErroresTransitorios.Contains(ex.Number))
            {
                logger.LogWarning(
                    "Oracle error transitorio {Num} en '{Op}' — intento {N}/{Max}. Reintentando en {Ms} ms.",
                    ex.Number, nombreOperacion, intento + 1, Backoff.Length + 1,
                    (int)Backoff[intento].TotalMilliseconds);

                await Task.Delay(Backoff[intento], ct);
            }
        }

        // Último intento: deja propagar cualquier excepción sin atrapar.
        ct.ThrowIfCancellationRequested();
        return await operacion();
    }

    /// <summary>Sobrecarga para operaciones sin valor de retorno.</summary>
    internal static Task EjecutarAsync(
        Func<Task> operacion, ILogger logger, string nombreOperacion, CancellationToken ct = default)
        => EjecutarAsync<int>(async () => { await operacion(); return 0; }, logger, nombreOperacion, ct);
}
