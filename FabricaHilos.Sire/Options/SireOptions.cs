namespace FabricaHilos.Sire.Options;

public sealed class SireOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Ruc { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
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
    /// <summary>
    /// Cuando es true, AceptarPropuesta y CerrarPeriodo simulan las llamadas SUNAT
    /// (ticket, polling y descarga) sin hacer ningún request real.
    /// Cambiar a false en producción para activar el flujo real.
    /// Se controla desde appsettings.json: Sire:UsarStub
    /// </summary>
    public bool UsarStub { get; set; } = true;
    /// <summary>URL de la página pública SUNAT donde se publica el padrón SSCO (.xlsx).</summary>
    public string SscoPageUrl { get; set; } = "https://www.sunat.gob.pe/padronesnotificaciones/sujeSinCapacidadOperativa.html";
}
