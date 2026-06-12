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
    public int TicketPollIntervalMs { get; set; } = 5000;
    public int TicketMaxRetries { get; set; } = 120;
    public bool UseMock { get; set; } = true;
}
