namespace FabricaHilos.Models.Sire;

/// <summary>
/// ViewModel unificado para la vista /Sire/Monitoreo.
/// Contiene los health checks y el log de auditoría HTTP en un único objeto
/// para evitar dos acciones/vistas separadas.
/// </summary>
public sealed class SireMonitoreoViewModel
{
    // ── Health checks ─────────────────────────────────────────────────────────
    public IReadOnlyList<SireHealthCheckLog> HealthLogs { get; init; } = [];

    // ── Log de auditoría HTTP (SIRE_LOG) ──────────────────────────────────────
    public IReadOnlyList<SireApiLog> ApiLogs { get; init; } = [];

    // ── Filtros activos ───────────────────────────────────────────────────────
    /// <summary>JobId filtrado (puede ser null = todos).</summary>
    public string? FiltroJobId     { get; init; }

    /// <summary>Operación filtrada: AUTH|EXPORTAR|TICKET|DESCARGAR|HEALTH (null = todas).</summary>
    public string? FiltroOperacion { get; init; }

    /// <summary>Tab activo al cargar la página: "health" | "log".</summary>
    public string TabActivo        { get; init; } = "health";

    // ── KPIs calculados de Health ─────────────────────────────────────────────
    public int    HealthTotal     => HealthLogs.Count;
    public int    HealthOk        => HealthLogs.Count(x => x.Status == "Healthy");
    public int    HealthDegraded  => HealthLogs.Count(x => x.Status == "Degraded");
    public int    HealthError     => HealthLogs.Count(x => x.Status == "Unhealthy");
    public double UptimePercent   => HealthTotal > 0
        ? Math.Round((double)HealthOk / HealthTotal * 100, 1)
        : 0;

    // ── KPIs calculados de Log HTTP ───────────────────────────────────────────
    public int ApiTotal    => ApiLogs.Count;
    public int ApiExitosos => ApiLogs.Count(x => x.Exito);
    public int ApiFallidos => ApiLogs.Count(x => !x.Exito);
    public double? ApiDuracionPromMs => ApiLogs.Any(x => x.DuracionMs.HasValue)
        ? Math.Round(ApiLogs.Where(x => x.DuracionMs.HasValue).Average(x => x.DuracionMs!.Value), 0)
        : null;
}
