namespace FabricaHilos.Sire.Options;

public sealed class SireOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Ruc { get; set; } = string.Empty;
    public string UsuarioSol { get; set; } = string.Empty;
    public string ClaveSol { get; set; } = string.Empty;
    public string AuthUrl { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    /// <summary>Intervalo entre intentos de polling del ticket en Fase 1 (ms). Default: 30 seg.</summary>
    public int TicketPollIntervalMs { get; set; } = 30_000;
    /// <summary>Máximo de intentos en Fase 1 antes de pasar a EsperandoTicket. Default: 20 (10 min).</summary>
    public int TicketMaxRetries { get; set; } = 20;
    /// <summary>Intervalo en minutos del SireTicketWatcherWorker (Fase 2). Default: 15 min.</summary>
    public int WatcherIntervalMin { get; set; } = 15;
    public bool UseMock { get; set; } = true;
}
