using System.Text.RegularExpressions;
using FabricaHilos.LecturaCorreos.Config;
using FabricaHilos.LecturaCorreos.Data;
using FabricaHilos.Notificaciones.Abstractions;
using FabricaHilos.Notificaciones.Models.Payloads;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FabricaHilos.LecturaCorreos.Workers;

/// <summary>
/// Recorre FH_LECTCORREOS_PDF_ADJUNTOS (ESTADO = 'PENDIENTE'), envía un correo
/// de notificación al remitente a través de FabricaHilos.Notificaciones y actualiza:
///   ENVIO_CORREO_OK → correo enviado con éxito.
///   ERROR_CORREO    → fallo en el envío.
/// </summary>
public class NotificacionPdfLimboWorker : BackgroundService
{
    private readonly IServiceScopeFactory               _scopeFactory;
    private readonly ILogger<NotificacionPdfLimboWorker> _logger;
    private readonly TimeSpan                           _intervalo;
    private readonly bool                               _activo;

    public NotificacionPdfLimboWorker(
        IServiceScopeFactory                  scopeFactory,
        ILogger<NotificacionPdfLimboWorker>   logger,
        IOptions<LecturaCorreosOptions>       opciones)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
        _activo       = opciones.Value.WorkerNotificacionPdfActivo;
        _intervalo    = TimeSpan.FromMinutes(opciones.Value.IntervaloNotificacionPdfMinutos);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_activo)
        {
            _logger.LogWarning(
                "NotificacionPdfLimboWorker está DESHABILITADO (WorkerNotificacionPdfActivo = false). " +
                "Actívalo en appsettings.json → LecturaCorreos:WorkerNotificacionPdfActivo.");
            return;
        }

        _logger.LogInformation(
            "NotificacionPdfLimboWorker iniciado. Intervalo: {Minutos} min.", _intervalo.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcesarPendientesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general en el ciclo de NotificacionPdfLimboWorker.");
            }

            await Task.Delay(_intervalo, stoppingToken);
        }

        _logger.LogInformation("NotificacionPdfLimboWorker detenido.");
    }

    private async Task ProcesarPendientesAsync(CancellationToken ct)
    {
        using var scope      = _scopeFactory.CreateScope();
        var repo             = scope.ServiceProvider.GetRequiredService<IPdfLimboRepository>();
        var emailService     = scope.ServiceProvider.GetRequiredService<IEmailNotificacionService>();

        var pendientes = await repo.ObtenerPendientesNotificacionAsync();

        if (pendientes.Count == 0)
        {
            _logger.LogInformation("No hay PDFs pendientes de notificación.");
            return;
        }

        _logger.LogInformation(
            "─── Ciclo NotificacionPdfLimboWorker: {Cantidad} PDF(s) pendientes ───",
            pendientes.Count);

        // Imprimir la lista completa antes de procesar
        for (int i = 0; i < pendientes.Count; i++)
        {
            var p = pendientes[i];
            _logger.LogInformation(
                "  [{Num}/{Total}] ID={Id} | Remitente={Remitente} | Archivo='{Archivo}'",
                i + 1, pendientes.Count, p.Id, p.RemitenteCorreo, p.NombreArchivo);
        }

        int enviados = 0;
        int fallidos = 0;

        foreach (var pdf in pendientes)
        {
            if (ct.IsCancellationRequested) break;

            var (nombre, email) = ParsearRemitente(pdf.RemitenteCorreo);

            _logger.LogInformation(
                ">>> Enviando correo → ID={Id} | Para: {Email} | Archivo: '{Archivo}'",
                pdf.Id, email, pdf.NombreArchivo);

            try
            {
                var payload = new DocumentoLimboPayload
                {
                    CorreoDestinatario = email,
                    NombreDestinatario = nombre,
                    NombreRemitente    = nombre,
                    CorreoRemitente    = email,
                    FechaRecepcion     = (pdf.FechaCorreo ?? pdf.FechaCreacion)
                                            .ToString("dd/MM/yyyy HH:mm"),
                    NombreArchivo      = pdf.NombreArchivo,
                    MotivoError        = "El documento PDF recibido no tiene un archivo XML válido " +
                                        "asociado, o el tipo de documento no está registrado en el sistema.",
                };

                var enviado = await emailService.EnviarAsync(payload, ct);

                if (enviado)
                {
                    await repo.MarcarCorreoEnviadoAsync(pdf.Id);
                    enviados++;
                    _logger.LogInformation(
                        "<<< OK  → ID={Id} | Para: {Email} | Archivo: '{Archivo}' → ENVIO_CORREO_OK",
                        pdf.Id, email, pdf.NombreArchivo);
                }
                else
                {
                    await repo.MarcarErrorNotificacionAsync(
                        pdf.Id, "El servicio de correo retornó false al intentar enviar.");
                    fallidos++;
                    _logger.LogWarning(
                        "<<< FAIL → ID={Id} | Para: {Email} | Archivo: '{Archivo}' → ERROR_CORREO",
                        pdf.Id, email, pdf.NombreArchivo);
                }
            }
            catch (Exception ex)
            {
                fallidos++;
                _logger.LogError(ex,
                    "<<< ERR  → ID={Id} | Para: {Email} | Archivo: '{Archivo}'",
                    pdf.Id, email, pdf.NombreArchivo);
                try
                {
                    using var errorScope = _scopeFactory.CreateScope();
                    var errorRepo = errorScope.ServiceProvider.GetRequiredService<IPdfLimboRepository>();
                    await errorRepo.MarcarErrorNotificacionAsync(pdf.Id, ex.Message);
                }
                catch (Exception dbEx)
                {
                    _logger.LogError(dbEx,
                        "No se pudo persistir el estado ERROR_CORREO para PDF ID={Id}.", pdf.Id);
                }
            }
        }

        _logger.LogInformation(
            "─── Ciclo finalizado: {Enviados} enviado(s), {Fallidos} fallido(s) de {Total} ───",
            enviados, fallidos, pendientes.Count);
    }

    /// <summary>
    /// Extrae nombre y correo de formatos como "Juan Pérez &lt;juan@empresa.com&gt;" o "juan@empresa.com".
    /// </summary>
    private static (string Nombre, string Email) ParsearRemitente(string remitente)
    {
        if (string.IsNullOrWhiteSpace(remitente))
            return ("Remitente", string.Empty);

        var match = Regex.Match(remitente, @"^(.+?)\s*<([^>]+)>$");
        if (match.Success)
            return (match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim());

        return (remitente.Trim(), remitente.Trim());
    }
}
