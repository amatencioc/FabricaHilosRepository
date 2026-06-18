namespace FabricaHilos.Models.Sire;

/// <summary>
/// ViewModel para la vista /Sire/Monitoreo.
/// Muestra el log de auditoría HTTP (SIRE_LOG) con filtros opcionales.
/// </summary>
public sealed class SireMonitoreoViewModel
{
    // ── Log de auditoría HTTP (SIRE_LOG) ──────────────────────────────────────
    public IReadOnlyList<SireApiLog> ApiLogs { get; init; } = [];

    // ── Filtros activos ───────────────────────────────────────────────────────
    /// <summary>JobId filtrado (puede ser null = todos).</summary>
    public string? FiltroJobId     { get; init; }

    /// <summary>Operación filtrada: AUTH|EXPORTAR|TICKET|DESCARGAR (null = todas).</summary>
    public string? FiltroOperacion { get; init; }

    /// <summary>Tab activo al cargar la página: "log" u otros futuros.</summary>
    public string TabActivo        { get; init; } = "log";

    // ── KPIs calculados de Log HTTP ───────────────────────────────────────────
    public int ApiTotal    => ApiLogs.Count;
    public int ApiExitosos => ApiLogs.Count(x => x.Exito);
    public int ApiFallidos => ApiLogs.Count(x => !x.Exito);
    public double? ApiDuracionPromMs => ApiLogs.Any(x => x.DuracionMs.HasValue)
        ? Math.Round(ApiLogs.Where(x => x.DuracionMs.HasValue).Average(x => x.DuracionMs!.Value), 0)
        : null;

    // ── Split por tipo para vista unificada RVIE / RCE ────────────────────────
    public IReadOnlyList<SireApiLog> ApiLogsVentas  => ApiLogs.Where(x => x.TipoRegistro == "ventas").ToList().AsReadOnly();
    public IReadOnlyList<SireApiLog> ApiLogsCompras => ApiLogs.Where(x => x.TipoRegistro == "compras").ToList().AsReadOnly();
    /// <summary>Logs sin job asociado: AUTH, llamadas sueltas.</summary>
    public IReadOnlyList<SireApiLog> ApiLogsSistema => ApiLogs.Where(x => x.TipoRegistro == null).ToList().AsReadOnly();
}
