using System.Text.Json;
using FabricaHilos.Notificaciones.Abstractions;
using FabricaHilos.Notificaciones.Configuration;
using FabricaHilos.Notificaciones.Models;
using FabricaHilos.Notificaciones.Rendering;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace FabricaHilos.Notificaciones.Services;

public sealed class EmailNotificacionService : IEmailNotificacionService
{
    private readonly EmailSettings                     _settings;
    private readonly IHttpClientFactory                _httpClientFactory;
    private readonly ILogger<EmailNotificacionService> _logger;

    public EmailNotificacionService(
        IOptions<EmailSettings>           settings,
        IHttpClientFactory                httpClientFactory,
        ILogger<EmailNotificacionService> logger)
    {
        _settings          = settings.Value;
        _httpClientFactory = httpClientFactory;
        _logger            = logger;
    }

    public async Task<bool> EnviarAsync(INotificacionPayload payload, CancellationToken ct = default)
    {
        try
        {
            // 1. Renderizar el HTML con los datos del payload
            var htmlBody = TemplateRenderer.Renderizar(
                nombreTemplate: payload.Tipo.ToString(),
                reemplazos:     payload.ObtenerReemplazos()
            );

            // 2. Construir el mensaje con MimeKit
            var mensaje = new MimeMessage();
            mensaje.From.Add(new MailboxAddress(_settings.NombreEnvio, _settings.UsuarioEnvio));
            mensaje.To.Add(new MailboxAddress(payload.NombreDestinatario, payload.CorreoDestinatario));
            mensaje.Subject = ObtenerAsunto(payload);

            var builder = new BodyBuilder { HtmlBody = htmlBody };
            mensaje.Body = builder.ToMessageBody();

            // 3. Enviar con MailKit
            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(
                _settings.SmtpHost,
                _settings.SmtpPort,
                _settings.UsarSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
                ct);

            if (_settings.UsarOAuth2)
            {
                // Autenticación OAuth2 Client Credentials — obtiene el access token de Microsoft
                var token  = await ObtenerTokenOAuth2Async(ct);
                var oauth2 = new SaslMechanismOAuth2(_settings.UsuarioEnvio, token);
                await smtp.AuthenticateAsync(oauth2, ct);
            }
            else
            {
                await smtp.AuthenticateAsync(_settings.UsuarioEnvio, _settings.PasswordEnvio, ct);
            }

            await smtp.SendAsync(mensaje, ct);
            await smtp.DisconnectAsync(quit: true, ct);

            _logger.LogInformation(
                "[Notificaciones] Correo {Tipo} enviado correctamente a {Destinatario}",
                payload.Tipo, payload.CorreoDestinatario);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[Notificaciones] Error al enviar correo {Tipo} a {Destinatario}",
                payload.Tipo, payload.CorreoDestinatario);
            return false;
        }
    }

    // ── OAuth2 — Client Credentials Flow (mismo patrón que ImapConexionService) ─────

    private async Task<string> ObtenerTokenOAuth2Async(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_settings.TenantId))
            throw new InvalidOperationException("TenantId no configurado para OAuth2.");
        if (string.IsNullOrWhiteSpace(_settings.ClientId))
            throw new InvalidOperationException("ClientId no configurado para OAuth2.");
        if (string.IsNullOrWhiteSpace(_settings.ClientSecret))
            throw new InvalidOperationException("ClientSecret no configurado para OAuth2.");

        var url = $"https://login.microsoftonline.com/{_settings.TenantId}/oauth2/v2.0/token";

        _logger.LogDebug(
            "[Notificaciones] OAuth2: solicitando token — TenantId={Tenant}, ClientId={Client}",
            _settings.TenantId, _settings.ClientId);

        var body = new Dictionary<string, string>
        {
            ["grant_type"]    = "client_credentials",
            ["client_id"]     = _settings.ClientId,
            ["client_secret"] = _settings.ClientSecret,
            ["scope"]         = "https://outlook.office365.com/.default",
        };

        using var http = _httpClientFactory.CreateClient();
        using var req  = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(body),
        };

        using var tokenCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        tokenCts.CancelAfter(TimeSpan.FromSeconds(30));

        var resp = await http.SendAsync(req, tokenCts.Token);
        var json = await resp.Content.ReadAsStringAsync(tokenCts.Token);

        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogError(
                "[Notificaciones] OAuth2 HTTP {Status}. Respuesta: {Json}",
                (int)resp.StatusCode, json);
            throw new InvalidOperationException(
                $"OAuth2 falló con HTTP {(int)resp.StatusCode}.");
        }

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("access_token").GetString()
               ?? throw new InvalidOperationException("Token OAuth2 vacío.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Asunto del correo según el tipo de notificación.
    /// Al agregar un nuevo TipoNotificacion, agregar su asunto aquí.
    /// </summary>
    private static string ObtenerAsunto(INotificacionPayload payload) =>
        payload.Tipo switch
        {
            TipoNotificacion.DocumentoLimbo =>
                "⚠️ Documento pendiente de validación — Acción requerida",
            _ => "Notificación del Sistema — La Colonial Fábrica de Hilos"
        };
}
