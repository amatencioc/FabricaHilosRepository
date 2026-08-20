namespace FabricaHilos.Alertas.Workers;

using FabricaHilos.Alertas.Config;
using FabricaHilos.Alertas.Data;
using FabricaHilos.Alertas.Services;
using FabricaHilos.Notificaciones.Abstractions;
using FabricaHilos.Notificaciones.Models.Payloads;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// BackgroundService que, todos los jueves a las 8:00am, lee
/// AQUARIUS.V_SCA_ALERTA_TAREO_DETALLE (NOTIFICADO='N') -- alertas de tareo
/// (TU=3 semanas mismo turno, SD=3 semanas sin descanso) generadas por el job
/// Oracle JOB_SCA_ALERTAS_TAREO / PKG_SCA_ALERTAS_TAREO.GENERAR_ALERTAS -- arma
/// un Excel con el detalle y lo envía por correo a RRHH. Tras el envío exitoso,
/// marca cada alerta como notificada (PKG_SCA_ALERTAS_TAREO.MARCAR_NOTIFICADO)
/// para no reenviarla en la siguiente semana.
///
/// Nota: desde PKG_SCA_ALERTAS_TAREO v2.2/v2.3, la generación en Oracle ya excluye
/// empleados de áreas administrativas/oficina y con cargo Jefe/Jefatura/Supervisor
/// (siempre tienen horario fijo, no es una anomalía); ese filtrado es transparente
/// para este worker, que solo consume lo que ya llega filtrado en la vista.
/// </summary>
public sealed class AlertaTurnoDescansoWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AlertaTurnoDescansoWorker> _logger;
    private readonly AlertaTurnoDescansoOptions _opciones;

    public AlertaTurnoDescansoWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<AlertaTurnoDescansoWorker> logger,
        IOptions<AlertaTurnoDescansoOptions> opciones)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _opciones = opciones.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opciones.WorkerActivo)
        {
            _logger.LogWarning("[ALERTAS-TAREO] Worker DESHABILITADO (AlertasTurnoDescanso:WorkerActivo = false).");
            return;
        }

        if (string.IsNullOrWhiteSpace(_opciones.CorreoDestino))
        {
            _logger.LogError(
                "[ALERTAS-TAREO] Worker DETENIDO: AlertasTurnoDescanso:CorreoDestino no está configurado.");
            return;
        }

        _logger.LogInformation(
            "[ALERTAS-TAREO] Worker iniciado. Envío programado: {Dia} a las {Hora}:00, destino {Correo}.",
            _opciones.DiaSemanaEjecucion, _opciones.HoraEjecucion, _opciones.CorreoDestino);

        if (_opciones.EjecutarAlIniciar)
        {
            await EjecutarCicloAsync(stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var espera = CalcularEsperaHastaProximaEjecucion(DateTime.Now);

            _logger.LogInformation(
                "[ALERTAS-TAREO] Próxima ejecución en {Espera} (a las {Fecha:dd/MM/yyyy HH:mm}).",
                espera, DateTime.Now.Add(espera));

            try
            {
                await Task.Delay(espera, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await EjecutarCicloAsync(stoppingToken);
        }

        _logger.LogInformation("[ALERTAS-TAREO] Worker detenido.");
    }

    /// <summary>
    /// Calcula el TimeSpan hasta el próximo DiaSemanaEjecucion a las HoraEjecucion:00.
    /// Si hoy es el día configurado pero la hora ya pasó, calcula la semana siguiente.
    /// </summary>
    internal TimeSpan CalcularEsperaHastaProximaEjecucion(DateTime ahora)
    {
        var hoy = ahora.Date;
        var horaObjetivoHoy = hoy.AddHours(_opciones.HoraEjecucion);

        int diasHastaObjetivo = ((int)_opciones.DiaSemanaEjecucion - (int)ahora.DayOfWeek + 7) % 7;

        DateTime proximaEjecucion;
        if (diasHastaObjetivo == 0)
        {
            proximaEjecucion = ahora <= horaObjetivoHoy ? horaObjetivoHoy : horaObjetivoHoy.AddDays(7);
        }
        else
        {
            proximaEjecucion = hoy.AddDays(diasHastaObjetivo).AddHours(_opciones.HoraEjecucion);
        }

        return proximaEjecucion - ahora;
    }

    private async Task EjecutarCicloAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IAlertaTurnoDescansoRepository>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailNotificacionService>();

            var pendientes = await repo.ObtenerPendientesAsync(ct);

            if (pendientes.Count == 0)
            {
                _logger.LogInformation("[ALERTAS-TAREO] No hay alertas pendientes de notificar esta semana.");
                return;
            }

            var cantidadTurno = pendientes.Count(a => a.TipAlerta == "TU");
            var cantidadDescanso = pendientes.Count(a => a.TipAlerta == "SD");
            var fechaCorte = pendientes.Max(a => a.FecFinSemana).ToString("dd/MM/yyyy");
            var nombreArchivo = $"AlertasTareo_{DateTime.Now:yyyyMMdd}.xlsx";

            var archivoExcel = ExcelAlertaTurnoDescansoBuilder.Construir(pendientes);

            var payload = new AlertaTurnoDescansoSemanalPayload
            {
                CorreoDestinatario = _opciones.CorreoDestino,
                NombreDestinatario = "Recursos Humanos",
                FechaCorte = fechaCorte,
                CantidadAlertas = pendientes.Count.ToString(),
                CantidadTurno = cantidadTurno.ToString(),
                CantidadDescanso = cantidadDescanso.ToString(),
                NombreArchivo = nombreArchivo,
                ArchivoExcel = archivoExcel,
            };

            _logger.LogInformation(
                "[ALERTAS-TAREO] Enviando reporte semanal -> {Cantidad} alerta(s) (TU={Turno}, SD={Descanso}) a {Correo}.",
                pendientes.Count, cantidadTurno, cantidadDescanso, _opciones.CorreoDestino);

            var enviado = await emailService.EnviarAsync(payload, ct);

            if (!enviado)
            {
                _logger.LogError(
                    "[ALERTAS-TAREO] Fallo el envío del reporte semanal a {Correo}. No se marcarán las alertas como notificadas; se reintentará en el próximo ciclo.",
                    _opciones.CorreoDestino);
                return;
            }

            int marcadas = 0;
            foreach (var alerta in pendientes)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    await repo.MarcarNotificadoAsync(alerta.IdAlerta, ct);
                    marcadas++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "[ALERTAS-TAREO] No se pudo marcar como notificada la alerta ID={Id}. Podría reenviarse en el próximo ciclo.",
                        alerta.IdAlerta);
                }
            }

            _logger.LogInformation(
                "[ALERTAS-TAREO] Reporte semanal enviado OK. {Marcadas}/{Total} alerta(s) marcadas como notificadas.",
                marcadas, pendientes.Count);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ALERTAS-TAREO] Error en el ciclo semanal de alertas de tareo.");
        }
    }
}
