namespace FabricaHilos.OrgatexSync.Workers;

using FabricaHilos.OrgatexSync.Config;
using FabricaHilos.OrgatexSync.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// BackgroundService que corre una vez al día (hora configurable, <see cref="OrgatexOptions.HoraEjecucion"/>)
/// y migra el día anterior completo de recetas de tintura desde ORGATEX (SQL Server) hacia
/// Oracle SIG.CARGA_ORGATEX. Solo lee de ORGATEX (no crea/modifica nada ahí).
/// </summary>
public sealed class OrgatexSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory       _scopeFactory;
    private readonly ILogger<OrgatexSyncWorker> _logger;
    private readonly OrgatexOptions             _opciones;

    public OrgatexSyncWorker(
        IServiceScopeFactory        scopeFactory,
        ILogger<OrgatexSyncWorker>  logger,
        IOptions<OrgatexOptions>    opciones)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
        _opciones     = opciones.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opciones.WorkerActivo)
        {
            _logger.LogWarning("[ORGATEX-SYNC] Worker DESHABILITADO (OrgatexSync:WorkerActivo = false).");
            return;
        }

        _logger.LogInformation(
            "[ORGATEX-SYNC] Worker iniciado. Ejecución diaria a las {Hora}:00 (migra el día anterior completo).",
            _opciones.HoraEjecucion);

        if (_opciones.EjecutarAlIniciar)
        {
            try
            {
                await EjecutarSincronizacionAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ORGATEX-SYNC] Error en ejecución inicial (EjecutarAlIniciar).");
            }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var espera = TiempoHastaProximaEjecucion(DateTime.Now, _opciones.HoraEjecucion);
            _logger.LogInformation(
                "[ORGATEX-SYNC] Próxima sincronización en {Horas:F1} h ({Fecha:yyyy-MM-dd HH:mm}).",
                espera.TotalHours, DateTime.Now.Add(espera));

            try
            {
                await Task.Delay(espera, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await EjecutarSincronizacionAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ORGATEX-SYNC] Error inesperado en ciclo de sincronización.");
            }
        }

        _logger.LogInformation("[ORGATEX-SYNC] Worker detenido.");
    }

    private async Task EjecutarSincronizacionAsync(CancellationToken ct)
    {
        var (desde, hasta) = DeterminarVentana();

        _logger.LogInformation(
            "[ORGATEX-SYNC] Iniciando sincronización — ventana {Desde:yyyy-MM-dd HH:mm:ss} a {Hasta:yyyy-MM-dd HH:mm:ss}.",
            desde, hasta);

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IOrgatexRepository>();

        var filas = await repo.ObtenerRecetasAsync(desde, hasta, ct);
        _logger.LogInformation("[ORGATEX-SYNC] {Cantidad} fila(s) leídas de ORGATEX.", filas.Count);

        if (filas.Count == 0)
        {
            _logger.LogInformation(
                "[ORGATEX-SYNC] Sin datos para la ventana {Desde:yyyy-MM-dd} a {Hasta:yyyy-MM-dd}. Nada que migrar.",
                desde, hasta);
            return;
        }

        var (ok, fail) = await repo.MergeCargaOrgatexAsync(filas, ct);
        _logger.LogInformation(
            "[ORGATEX-SYNC] Migración completada — {Ok} OK, {Fail} con error (total {Total}).",
            ok, fail, filas.Count);

        // Verificación de consistencia: registros leídos de ORGATEX vs. registros
        // efectivamente insertados/actualizados (OK) en Oracle CARGA_ORGATEX.
        if (ok == filas.Count)
        {
            _logger.LogInformation(
                "[ORGATEX-SYNC] Verificación OK: coinciden los registros leídos de ORGATEX ({Leidos}) con los insertados en Oracle ({Insertados}).",
                filas.Count, ok);
        }
        else
        {
            _logger.LogWarning(
                "[ORGATEX-SYNC] Verificación FALLIDA: NO coinciden los registros — leídos de ORGATEX: {Leidos}, insertados en Oracle: {Insertados}, diferencia: {Diferencia} (con error: {Fail}).",
                filas.Count, ok, filas.Count - ok, fail);
        }
    }

    private static TimeSpan TiempoHastaProximaEjecucion(DateTime ahora, int horaEjecucion)
    {
        var proxima = ahora.Date.AddHours(horaEjecucion);
        if (proxima <= ahora) proxima = proxima.AddDays(1);
        return proxima - ahora;
    }

    /// <summary>
    /// Determina la ventana de fechas a migrar:
    /// - Si <see cref="OrgatexOptions.FechaDesde"/> y <see cref="OrgatexOptions.FechaHasta"/> están
    ///   configurados (formato exacto "yyyy-MM-dd"), se usa ese rango completo para regularizar/reprocesar.
    /// - Si están vacíos/null, se usa el comportamiento diario normal: el día anterior completo
    ///   (00:00:00.000 a 23:59:59.999).
    /// </summary>
    private (DateTime Desde, DateTime Hasta) DeterminarVentana()
    {
        const string formatoFecha = "yyyy-MM-dd";

        if (!string.IsNullOrWhiteSpace(_opciones.FechaDesde) && !string.IsNullOrWhiteSpace(_opciones.FechaHasta))
        {
            var desdeOk = DateTime.TryParseExact(
                _opciones.FechaDesde, formatoFecha, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var fechaDesde);
            var hastaOk = DateTime.TryParseExact(
                _opciones.FechaHasta, formatoFecha, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var fechaHasta);

            if (desdeOk && hastaOk && fechaDesde <= fechaHasta)
            {
                _logger.LogInformation(
                    "[ORGATEX-SYNC] Rango de fechas configurado en appsettings (FechaDesde/FechaHasta) — " +
                    "se usará para regularizar en vez de la ventana diaria por defecto: {Desde:yyyy-MM-dd} a {Hasta:yyyy-MM-dd}.",
                    fechaDesde, fechaHasta);

                return (fechaDesde, fechaHasta.AddDays(1).AddMilliseconds(-1));
            }

            _logger.LogWarning(
                "[ORGATEX-SYNC] OrgatexSync:FechaDesde/FechaHasta configurados pero inválidos " +
                "(formato esperado \"{Formato}\": FechaDesde='{FechaDesde}', FechaHasta='{FechaHasta}'). " +
                "Se ignora el rango y se usa la ventana diaria por defecto.",
                formatoFecha, _opciones.FechaDesde, _opciones.FechaHasta);
        }

        // Comportamiento por defecto: todo el día anterior completo.
        var ayer = DateTime.Today.AddDays(-1);
        return (ayer, ayer.AddDays(1).AddMilliseconds(-1));
    }
}
