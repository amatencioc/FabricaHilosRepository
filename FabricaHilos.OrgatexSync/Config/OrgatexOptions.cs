namespace FabricaHilos.OrgatexSync.Config;

public class OrgatexOptions
{
    public const string SeccionConfig = "OrgatexSync";

    /// <summary>
    /// Habilita o deshabilita el worker de sincronización (OrgatexSyncWorker).
    /// Cuando es false el servicio arranca pero no ejecuta ningún ciclo.
    /// </summary>
    public bool WorkerActivo { get; set; } = true;

    /// <summary>
    /// Hora del día (0-23, hora local del servidor) en que corre la sincronización diaria.
    /// Siempre migra el día anterior completo (00:00:00 a 23:59:59.999).
    /// </summary>
    public int HoraEjecucion { get; set; } = 6;

    /// <summary>
    /// Si es true, ejecuta un ciclo inmediato al arrancar el servicio, además del ciclo diario
    /// programado. Útil en desarrollo/pruebas; en producción debe quedar en false.
    /// </summary>
    public bool EjecutarAlIniciar { get; set; } = false;

    /// <summary>
    /// Fecha de inicio (inclusive) para regularizar/reprocesar un rango específico en vez del
    /// día anterior por defecto. FORMATO EXACTO: "yyyy-MM-dd" (ej. "2026-07-20").
    /// Debe usarse en conjunto con <see cref="FechaHasta"/>. Si queda vacío/null, se ignora
    /// el rango y se usa la ventana diaria normal (día anterior completo).
    /// </summary>
    public string? FechaDesde { get; set; }

    /// <summary>
    /// Fecha de fin (inclusive, se migra el día completo hasta 23:59:59.999) para
    /// regularizar/reprocesar un rango específico. FORMATO EXACTO: "yyyy-MM-dd" (ej. "2026-07-22").
    /// Debe usarse en conjunto con <see cref="FechaDesde"/>. Si queda vacío/null, se ignora
    /// el rango y se usa la ventana diaria normal (día anterior completo).
    /// </summary>
    public string? FechaHasta { get; set; }
}
