namespace FabricaHilos.LecturaCorreos.Services.Email.Conexion;

using System.Net.Http;
using System.Text.Json;
using FabricaHilos.LecturaCorreos.Config;
using MailKit.Net.Imap;
using MailKit.Security;
using Microsoft.Extensions.Logging;

/// <summary>
/// Gestiona la conexión IMAP: timeout de socket, connect y autenticación
/// (usuario/contraseña o OAuth2 Client Credentials para Office 365).
/// </summary>
public class ImapConexionService : IImapConexionService
{
    private static readonly TimeSpan TimeoutSocket = TimeSpan.FromMinutes(5);

    private readonly IHttpClientFactory             _httpClientFactory;
    private readonly ILogger<ImapConexionService>   _logger;

    public ImapConexionService(
        IHttpClientFactory           httpClientFactory,
        ILogger<ImapConexionService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger            = logger;
    }

    public async Task<ImapClient> ConectarAsync(CuentaCorreoOptions cuenta, CancellationToken ct)
    {
        var client = new ImapClient();
        try
        {
            // Timeout de socket: si la conexión se queda colgada más de 5 min, falla rápido.
            client.Timeout = (int)TimeoutSocket.TotalMilliseconds;

            await client.ConnectAsync(cuenta.ImapHost, cuenta.ImapPort, cuenta.UsarSsl, ct);

            if (!string.IsNullOrEmpty(cuenta.ClientId)     &&
                !string.IsNullOrEmpty(cuenta.ClientSecret) &&
                !string.IsNullOrEmpty(cuenta.TenantId))
            {
                var token  = await ObtenerTokenOAuth2Async(cuenta, ct);
                var oauth2 = new SaslMechanismOAuth2(cuenta.Usuario, token);
                await client.AuthenticateAsync(oauth2, ct);
                _logger.LogInformation("Cuenta {Nombre}: autenticado con OAuth2.", cuenta.Nombre);
            }
            else
            {
                await client.AuthenticateAsync(cuenta.Usuario, cuenta.Contrasena, ct);
                _logger.LogInformation("Cuenta {Nombre}: autenticado con usuario/contraseña.", cuenta.Nombre);
            }

            return client;
        }
        catch
        {
            // Si algo falla durante la conexión/auth, liberar el cliente antes de propagar.
            await DesconectarSeguroAsync(client);
            client.Dispose();
            throw;
        }
    }

    // ── OAuth2 — Client Credentials Flow ─────────────────────────────────────

    private async Task<string> ObtenerTokenOAuth2Async(CuentaCorreoOptions cuenta, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cuenta.ClientSecret))
            throw new InvalidOperationException($"ClientSecret vacío para cuenta '{cuenta.Nombre}'.");

        var url = $"https://login.microsoftonline.com/{cuenta.TenantId}/oauth2/v2.0/token";

        _logger.LogDebug(
            "OAuth2: solicitando token para '{Nombre}' — TenantId={Tenant}, ClientId={Client}",
            cuenta.Nombre, cuenta.TenantId, cuenta.ClientId);

        var body = new Dictionary<string, string>
        {
            ["grant_type"]    = "client_credentials",
            ["client_id"]     = cuenta.ClientId!,
            ["client_secret"] = cuenta.ClientSecret!,
            ["scope"]         = "https://outlook.office365.com/.default",
        };

        using var http = _httpClientFactory.CreateClient();
        using var req  = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(body),
        };

        // Timeout propio de 30 s para no bloquear el ciclo si el endpoint de Microsoft no responde.
        using var tokenCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        tokenCts.CancelAfter(TimeSpan.FromSeconds(30));

        var resp = await http.SendAsync(req, tokenCts.Token);
        var json = await resp.Content.ReadAsStringAsync(tokenCts.Token);

        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogError(
                "OAuth2 HTTP {Status} para cuenta '{Nombre}'. Respuesta (truncada): {Json}",
                (int)resp.StatusCode, cuenta.Nombre,
                json.Length > 500 ? json[..500] + "…" : json);
            throw new InvalidOperationException(
                $"OAuth2 falló con HTTP {(int)resp.StatusCode} para cuenta '{cuenta.Nombre}'.");
        }

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("access_token").GetString()
               ?? throw new InvalidOperationException("Token OAuth2 vacío.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task DesconectarSeguroAsync(ImapClient client)
    {
        try
        {
            if (client.IsConnected)
                await client.DisconnectAsync(quit: false, CancellationToken.None);
        }
        catch { /* ignorar — ya estamos manejando otra excepción */ }
    }
}
