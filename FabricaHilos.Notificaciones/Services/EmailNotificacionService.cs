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

            mensaje.Subject = ObtenerAsunto(payload);

            var builder = new BodyBuilder { HtmlBody = htmlBody };

            // CC, BCC y adjunto PDF (solo para EnvioCertificadoFacturacionPayload)
            if (payload is FabricaHilos.Notificaciones.Models.Payloads.EnvioCertificadoFacturacionPayload certPayload)
            {
                if (!string.IsNullOrEmpty(certPayload.CorreoCopia))
                    mensaje.Cc.Add(new MailboxAddress("Copia", certPayload.CorreoCopia));
                if (!string.IsNullOrEmpty(certPayload.CorreoCopiaOculta))
                    mensaje.Bcc.Add(new MailboxAddress("Copia Oculta", certPayload.CorreoCopiaOculta));

                if (certPayload.ArchivoCertificadoPdf is { Length: > 0 } pdfBytes)
                {
                    var nombreArchivo = string.IsNullOrWhiteSpace(certPayload.NombreArchivoCertificadoPdf)
                        ? "certificado.pdf"
                        : certPayload.NombreArchivoCertificadoPdf;
                    builder.Attachments.Add(nombreArchivo, pdfBytes, new MimeKit.ContentType("application", "pdf"));
                }
            }

            // CC al vendedor en correos de reclamo enviado a calidad
            if (payload is FabricaHilos.Notificaciones.Models.Payloads.ReclamoEnviadoCalidadPayload reclamoPayload
                && !string.IsNullOrEmpty(reclamoPayload.CorreoCopia))
            {
                mensaje.Cc.Add(new MailboxAddress(reclamoPayload.NombreVendedor, reclamoPayload.CorreoCopia));
            }

            // CC múltiple, To adicionales y adjunto Excel para el reporte SIRE Compras
            if (payload is FabricaHilos.Notificaciones.Models.Payloads.SireReporteComprasPayload sirePayload)
            {
                if (sirePayload.CorreosTo is { Count: > 0 })
                    foreach (var to in sirePayload.CorreosTo)
                        if (!string.IsNullOrWhiteSpace(to))
                            mensaje.To.Add(new MailboxAddress(to, to));

                if (sirePayload.CorreosCopia is { Count: > 0 })
                    foreach (var cc in sirePayload.CorreosCopia)
                        if (!string.IsNullOrWhiteSpace(cc))
                            mensaje.Cc.Add(new MailboxAddress(cc, cc));

                if (sirePayload.ArchivoExcel is { Length: > 0 } xlsBytes)
                    builder.Attachments.Add(
                        sirePayload.NombreArchivo,
                        xlsBytes,
                        new MimeKit.ContentType("application", "vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
            }

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
            TipoNotificacion.ReclamoEnviadoCalidad =>
                "🔍 Reclamo recibido para análisis de calidad",
            TipoNotificacion.ReclamoEvaluadoVendedor =>
                "✅ Su reclamo ha sido evaluado — Acción requerida",
            TipoNotificacion.SireReporteCompras =>
                $"📊 SIRE RCE — Documentos Solo SUNAT período {(payload as FabricaHilos.Notificaciones.Models.Payloads.SireReporteComprasPayload)?.Periodo ?? string.Empty}",
            _ => "Notificación del Sistema — La Colonial Fábrica de Hilos"
        };
}
