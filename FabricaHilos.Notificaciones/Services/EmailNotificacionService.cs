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
    private readonly ILogger<EmailNotificacionService> _logger;

    public EmailNotificacionService(
        IOptions<EmailSettings> settings,
        ILogger<EmailNotificacionService> logger)
    {
        _settings = settings.Value;
        _logger   = logger;
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

            // Agregar CC si está disponible (solo para EnvioCertificadoFacturacionPayload)
            if (payload is FabricaHilos.Notificaciones.Models.Payloads.EnvioCertificadoFacturacionPayload certPayload 
                && !string.IsNullOrEmpty(certPayload.CorreoCopia))
            {
                mensaje.Cc.Add(new MailboxAddress("Copia", certPayload.CorreoCopia));
            }

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

            await smtp.AuthenticateAsync(_settings.UsuarioEnvio, _settings.PasswordEnvio, ct);
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

    /// <summary>
    /// Asunto del correo según el tipo de notificación.
    /// Al agregar un nuevo TipoNotificacion, agregar su asunto aquí.
    /// </summary>
    private static string ObtenerAsunto(INotificacionPayload payload) =>
        payload.Tipo switch
        {
            TipoNotificacion.DocumentoLimbo =>
                "⚠️ Documento pendiente de validación — Acción requerida",
            TipoNotificacion.EnvioCertificadoFacturacion =>
                "📄 Requerimiento de emisión de Factura — Certificado listo",
            _ => "Notificación del Sistema — La Colonial Fábrica de Hilos"
        };
}
