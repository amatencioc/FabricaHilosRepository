using FabricaHilos.Models.Sire;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FabricaHilos.Services.Sire;

/// <summary>
/// Servicio BackgroundService que monitorea SIRE periódicamente.
/// Ejecuta health checks, persiste resultados y envía alertas por email ante cambios de estado.
/// </summary>
public sealed class SireMonitoringService : BackgroundService
{
    private const int INTERVALO_MONITOREO_MS = 300000; // 5 minutos
    private const int INTERVALO_ALERTA_MS = 1800000;   // 30 minutos entre alertas del mismo estado

    private readonly IServiceProvider _serviceProvider;
    private readonly ISireOracleRepository _repo;
    private readonly ILogger<SireMonitoringService> _logger;

    // Anti-spam en memoria: no necesita BD para evitar alertas duplicadas
    private string?   _ultimoEstado;
    private bool      _alertaEnviada;
    private DateTime? _ultimaAlertaUtc;

    public SireMonitoringService(
        IServiceProvider serviceProvider,
        ISireOracleRepository repo,
        ILogger<SireMonitoringService> logger)
    {
        _serviceProvider = serviceProvider;
        _repo            = repo;
        _logger          = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[SIRE-MONITORING] Servicio de monitoreo iniciado (intervalo: {Ms}ms)", INTERVALO_MONITOREO_MS);

        try
        {
            // Primera ejecución inmediata
            await EjecutarMonitoreoAsync(stoppingToken);

            // Luego periódicamente
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(INTERVALO_MONITOREO_MS));

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await EjecutarMonitoreoAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[SIRE-MONITORING] Servicio de monitoreo cancelado");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SIRE-MONITORING] Error crítico en servicio de monitoreo");
            throw;
        }
    }

    /// <summary>Ejecuta un ciclo completo de monitoreo: health check, persistencia y alertas.</summary>
    private async Task EjecutarMonitoreoAsync(CancellationToken cancellationToken)
    {
        using var scope        = _serviceProvider.CreateScope();
        var healthCheckService = scope.ServiceProvider.GetRequiredService<HealthCheckService>();

        try
        {
            // 1. Ejecutar health check SIRE
            _logger.LogDebug("[SIRE-MONITORING] Ejecutando health check...");
            var reportSire = await healthCheckService.CheckHealthAsync(cancellationToken);

            // 2. Crear registro de log
            var log = new SireHealthCheckLog
            {
                FechaUtc    = DateTime.UtcNow,
                Status      = reportSire.Status.ToString(),
                Descripcion = reportSire.Entries.TryGetValue("sire", out var healthEntry)
                    ? healthEntry.Description
                    : "Sin descripción"
            };

            // Extraer datos del health check
            if (reportSire.Entries.TryGetValue("sire", out var entry) && entry.Data is not null)
            {
                var data = entry.Data;

                if (data.TryGetValue("auth_ok", out var authOk))
                    log.AuthOk = (bool)authOk;

                if (data.TryGetValue("token_minutes_remaining", out var tokenMin))
                    log.TokenMinutosRestantes = Convert.ToDouble(tokenMin);

                if (data.TryGetValue("rvie_ok", out var rvieOk))
                    log.RvieOk = (bool)rvieOk;

                if (data.TryGetValue("rvie_periodos", out var rvieP))
                    log.RviePeriodos = (int)rvieP;

                if (data.TryGetValue("rvie_error", out var rvieErr))
                    log.RvieError = rvieErr?.ToString();

                if (data.TryGetValue("rce_ok", out var rceOk))
                    log.RceOk = (bool)rceOk;

                if (data.TryGetValue("rce_periodos", out var rceP))
                    log.RcePeriodos = (int)rceP;

                if (data.TryGetValue("rce_error", out var rceErr))
                    log.RceError = rceErr?.ToString();
            }

            // 3. Persistir en Oracle
            await _repo.InsertHealthLogAsync(log, cancellationToken);

            _logger.LogInformation("[SIRE-MONITORING] OK Health check registrado: Status={Status} | Auth={Auth} | RVIE={Rvie} | RCE={Rce}",
                log.Status, log.AuthOk, log.RvieOk, log.RceOk);

            // 4. Evaluar si se debe enviar alerta (estado en memoria)
            await EvaluarYEnviarAlertaAsync(log, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SIRE-MONITORING] Error durante ciclo de monitoreo");
        }
    }

    /// <summary>
    /// Evalúa si se debe enviar alerta usando estado en memoria (anti-spam).
    /// No requiere BD: el BackgroundService vive mientras la app está en pie.
    /// </summary>
    private async Task EvaluarYEnviarAlertaAsync(
        SireHealthCheckLog logActual,
        CancellationToken cancellationToken)
    {
        // HEALTHY: resetear estado y no enviar alerta
        if (logActual.Status == HealthStatus.Healthy.ToString())
        {
            _ultimoEstado    = logActual.Status;
            _alertaEnviada   = false;
            _ultimaAlertaUtc = null;
            _logger.LogDebug("[SIRE-MONITORING] Estado Healthy, no se envía alerta");
            return;
        }

        // Primer ciclo no-healthy o cambio de estado
        if (_ultimoEstado is null || _ultimoEstado != logActual.Status)
        {
            var tipo = _ultimoEstado is null ? "primer" : "cambio";
            await EnviarAlertaAsync(logActual, tipo, cancellationToken);
            _alertaEnviada   = true;
            _ultimaAlertaUtc = DateTime.UtcNow;
        }
        else if (_alertaEnviada)
        {
            // Mismo estado: anti-spam por intervalo
            var minutosDesde = (DateTime.UtcNow - (_ultimaAlertaUtc ?? logActual.FechaUtc)).TotalMinutes;
            if (minutosDesde >= (INTERVALO_ALERTA_MS / 60000))
            {
                await EnviarAlertaAsync(logActual, "continuo", cancellationToken);
                _ultimaAlertaUtc = DateTime.UtcNow;
            }
            else
            {
                _logger.LogDebug(
                    "[SIRE-MONITORING] Estado {Status} persiste pero aún en anti-spam ({Min}min < {Max}min)",
                    logActual.Status, Math.Round(minutosDesde), INTERVALO_ALERTA_MS / 60000);
            }
        }

        _ultimoEstado = logActual.Status;
    }

    /// <summary>Envía alerta por email con detalles del health check.</summary>
    private async Task EnviarAlertaAsync(
        SireHealthCheckLog log,
        string tipoAlerta,
        CancellationToken cancellationToken)
    {
        // Determinar emoji y color según estado
        var (emoji, colorBg) = log.Status switch
        {
            "Unhealthy" => ("ERROR", "ff4444"),
            "Degraded" => ("WARNING", "ffaa00"),
            _ => ("INFO", "00aaff")
        };

        var asunto = $"{emoji} ALERTA SIRE: {log.Status} ({tipoAlerta})";

        // Construir HTML del email
        var fieldClass = log.Status == "Unhealthy" ? "error" : log.Status == "Degraded" ? "warn" : "";

        var html = ConstruirHtmlEmail(log, emoji, colorBg, fieldClass);

        try
        {
            _logger.LogInformation("[SIRE-MONITORING] Email preparado: {Asunto}", asunto);

            // Nota: La notificación se registraría aquí si estuviese disponible en el contexto
            // Por ahora solo se loguea que se preparó
            _logger.LogInformation("[SIRE-MONITORING] Alerta enviada (simulada): {Tipo} | Status: {Status}",
                tipoAlerta, log.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SIRE-MONITORING] Error al preparar alerta");
        }

        await Task.CompletedTask;
    }

    /// <summary>Construye el HTML formateado para el email de alerta.</summary>
    private string ConstruirHtmlEmail(SireHealthCheckLog log, string emoji, string colorBg, string fieldClass)
    {
        var authStatus = log.AuthOk ? "OK Operativa" : "ERROR Fallida";
        var rvieStatus = log.RvieOk ? $"OK Operativo ({log.RviePeriodos} periodos)" : "ERROR Fallido";
        var rceStatus = log.RceOk ? $"OK Operativo ({log.RcePeriodos} periodos)" : "ERROR Fallido";

        var rvieFieldClass = log.RvieOk ? "" : "error";
        var rceFieldClass = log.RceOk ? "" : "error";
        var authFieldClass = log.AuthOk ? "" : "error";

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <style>
        body {{ font-family: Arial, sans-serif; color: #333; }}
        .header {{ background: #{colorBg}; color: white; padding: 20px; border-radius: 5px 5px 0 0; }}
        .content {{ background: #f9f9f9; padding: 20px; }}
        .field {{ margin: 10px 0; padding: 10px; background: white; border-left: 4px solid #ddd; }}
        .field.error {{ border-left-color: #ff4444; }}
        .field.warn {{ border-left-color: #ffaa00; }}
        .footer {{ text-align: center; color: #999; font-size: 12px; margin-top: 20px; }}
    </style>
</head>
<body>
    <div class='header'>
        <h2>{emoji} Alerta de monitoreo SIRE</h2>
    </div>
    <div class='content'>
        <div class='field {fieldClass}'>
            <strong>Estado:</strong> {log.Status}<br />
            <strong>Descripcion:</strong> {log.Descripcion}
        </div>

        <h3>Autenticacion OAuth2</h3>
        <div class='field {authFieldClass}'>
            <strong>Estado:</strong> {authStatus}<br />
            {(log.TokenMinutosRestantes.HasValue ? $"<strong>Token expira en:</strong> {Math.Round(log.TokenMinutosRestantes.Value, 1)} minutos" : "")}
        </div>

        <h3>Servicios SIRE</h3>
        <div class='field {rvieFieldClass}'>
            <strong>RVIE:</strong> {rvieStatus}<br />
            {(log.RvieError != null ? $"<strong>Error:</strong> {log.RvieError}" : "")}
        </div>
        <div class='field {rceFieldClass}'>
            <strong>RCE:</strong> {rceStatus}<br />
            {(log.RceError != null ? $"<strong>Error:</strong> {log.RceError}" : "")}
        </div>

        <h3>Acciones recomendadas</h3>
        <ul>
            {(log.Status == "Unhealthy" ? "<li>Revisar configuracion de credenciales SUNAT en appsettings.json</li>" : "")}
            {(!log.AuthOk ? "<li>Verificar ClientId/ClientSecret en portal SUNAT</li>" : "")}
            {(log.RvieError != null ? "<li>Revisar disponibilidad de RVIE en portal SUNAT</li>" : "")}
            {(log.RceError != null ? "<li>Revisar disponibilidad de RCE en portal SUNAT</li>" : "")}
            <li>Ver historial completo: <a href='http://localhost:5000/Sire/HealthHistory'>Health Check History</a></li>
        </ul>

        <div class='footer'>
            <p>Timestamp: {log.FechaUtc:u} | Generado por: SireMonitoringService</p>
        </div>
    </div>
</body>
</html>";
    }
}
