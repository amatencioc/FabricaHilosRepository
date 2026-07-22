namespace FabricaHilos.OrgatexSync.Data;

using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;

/// <summary>
/// Reintenta operaciones Oracle ante errores transitorios de red/conexión
/// con backoff escalonado: 500 ms → 1,5 s → 3 s.
/// Copia del helper usado en FabricaHilos.LecturaCorreos/Data/OracleRetry.cs
/// (cada Worker Service es autocontenido; no se referencia entre proyectos).
/// </summary>
internal static class OracleRetry
{
    private static readonly HashSet<int> ErroresTransitorios =
    [
        28,     // Session killed
        1033,   // ORACLE initialization or shutdown in progress
        1089,   // Immediate shutdown in progress
        3113,   // End-of-file on communication channel
        3114,   // Not connected to ORACLE
        12150, 12152, 12153, 12157, 12170,
        12203, 12224, 12500, 12535, 12537,
        12541, 12543,
    ];

    private static readonly TimeSpan[] Backoff =
    [
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromMilliseconds(1_500),
        TimeSpan.FromMilliseconds(3_000),
    ];

    internal static async Task<T> EjecutarAsync<T>(
        Func<Task<T>> operacion,
        ILogger        logger,
        string         nombreOperacion,
        CancellationToken ct = default,
        Func<Task>?    reconectar = null)
    {
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

                // Errores como 3113/3114 dejan la conexión inutilizable: si no se
                // reabre, todos los reintentos (y las filas siguientes del lote)
                // fallarán en cascada aunque el error original era transitorio.
                if (reconectar is not null)
                {
                    try
                    {
                        await reconectar();
                    }
                    catch (Exception exReconexion)
                    {
                        logger.LogWarning(exReconexion,
                            "No se pudo reconectar tras error transitorio en '{Op}'.", nombreOperacion);
                    }
                }
            }
        }

        ct.ThrowIfCancellationRequested();
        return await operacion();
    }
}
