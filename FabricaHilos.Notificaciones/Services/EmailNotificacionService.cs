using FabricaHilos.Notificaciones.Abstractions;
using FabricaHilos.Notificaciones.Configuration;
using FabricaHilos.Notificaciones.Rendering;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace FabricaHilos.Notificaciones.Services;

/// <summary>
/// Implementación del servicio de notificaciones por correo electrónico.
/// Usa MailKit + MimeKit para construir y enviar el mensaje.
/// Soporta autenticación básica (usuario/contraseña) y OAuth2 (Office 365).
/// </summary>
public class EmailNotificacionService : IEmailNotificacionService
{
    private readonly EmailSettings                      _settings;
    private readonly ILogger<EmailNotificacionService>  _logger;

    public EmailNotificacionService(
        IOptions<EmailSettings>                    settings,
        ILogger<EmailNotificacionService>          logger)
    {
        _settings = settings.Value;
        _logger   = logger;
    }

    /// <inheritdoc />
    public async Task<bool> EnviarAsync(INotificacionPayload payload, CancellationToken ct = default)
    {
        try
        {
            var cuerpoHtml = TemplateRenderer.Renderizar(payload);
            var mensaje    = ConstruirMensaje(payload, cuerpoHtml);

            await EnviarMensajeAsync(mensaje, ct);

            _logger.LogInformation(
                "Notificación [{Tipo}] enviada a {Destinatario}.",
                payload.Tipo, payload.CorreoDestinatario);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error al enviar notificación [{Tipo}] a {Destinatario}.",
                payload.Tipo, payload.CorreoDestinatario);
            return false;
        }
    }

    // --- Construcción del mensaje ---

    private MimeMessage ConstruirMensaje(INotificacionPayload payload, string cuerpoHtml)
    {
        var mensaje = new MimeMessage();
        mensaje.From.Add(new MailboxAddress(_settings.NombreEnvio, _settings.UsuarioEnvio));
        mensaje.To.Add(new MailboxAddress(payload.NombreDestinatario, payload.CorreoDestinatario));
        mensaje.Subject = ObtenerAsunto(payload);

        var builder = new BodyBuilder { HtmlBody = cuerpoHtml };
        mensaje.Body = builder.ToMessageBody();

        return mensaje;
    }

    private static string ObtenerAsunto(INotificacionPayload payload) => payload.Tipo switch
    {
        Models.TipoNotificacion.DocumentoLimbo =>
            "⚠️ Documento Pendiente de Validación — La Colonial Fábrica de Hilos",
        _ => $"Notificación del Sistema — {payload.Tipo}"
    };

    // --- Envío SMTP ---

    private async Task EnviarMensajeAsync(MimeMessage mensaje, CancellationToken ct)
    {
        using var cliente = new SmtpClient();

        var sslOpciones = _settings.UsarSsl
            ? SecureSocketOptions.StartTls
            : SecureSocketOptions.None;

        await cliente.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, sslOpciones, ct);

        if (!string.IsNullOrWhiteSpace(_settings.ClientSecret))
        {
            // OAuth2 (Office 365)
            var token = await ObtenerTokenOAuth2Async(ct);
            var oauth2 = new SaslMechanismOAuth2(_settings.UsuarioEnvio, token);
            await cliente.AuthenticateAsync(oauth2, ct);
        }
        else if (!string.IsNullOrWhiteSpace(_settings.UsuarioEnvio) &&
                 !string.IsNullOrWhiteSpace(_settings.Password))
        {
            // Autenticación básica (usuario / contraseña)
            await cliente.AuthenticateAsync(_settings.UsuarioEnvio, _settings.Password, ct);
        }

        await cliente.SendAsync(mensaje, ct);
        await cliente.DisconnectAsync(quit: true, ct);
    }

    // --- OAuth2 ---

    private async Task<string> ObtenerTokenOAuth2Async(CancellationToken ct)
    {
        var authUrl = _settings.AuthUrl.Replace("{TenantId}", _settings.TenantId,
            StringComparison.OrdinalIgnoreCase);

        using var http = new HttpClient();
        var parametros = new Dictionary<string, string>
        {
            ["grant_type"]    = "client_credentials",
            ["client_id"]     = _settings.ClientId,
            ["client_secret"] = _settings.ClientSecret,
            ["scope"]         = _settings.Scope,
        };

        var respuesta = await http.PostAsync(authUrl, new FormUrlEncodedContent(parametros), ct);
        respuesta.EnsureSuccessStatusCode();

        using var documento = await System.Text.Json.JsonDocument.ParseAsync(
            await respuesta.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

        return documento.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("El token OAuth2 devuelto está vacío.");
    }
}
