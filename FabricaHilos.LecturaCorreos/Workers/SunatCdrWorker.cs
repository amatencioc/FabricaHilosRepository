using FabricaHilos.LecturaCorreos.Config;
using FabricaHilos.LecturaCorreos.Data;
using FabricaHilos.LecturaCorreos.Services.Signals;
using FabricaHilos.LecturaCorreos.Services.Sunat;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FabricaHilos.LecturaCorreos.Workers;

public class SunatCdrWorker : BackgroundService
{
    private readonly IServiceScopeFactory       _scopeFactory;
    private readonly ILimpiezaSignal            _limpiezaSignal;
    private readonly ILogger<SunatCdrWorker>    _logger;
    private readonly TimeSpan                   _intervalo;
    private readonly bool                       _activo;
    private readonly LecturaCorreosOptions      _opciones;

    public SunatCdrWorker(
        IServiceScopeFactory            scopeFactory,
        ILimpiezaSignal                 limpiezaSignal,
        ILogger<SunatCdrWorker>         logger,
        IOptions<LecturaCorreosOptions> opciones)
    {
        _scopeFactory   = scopeFactory;
        _limpiezaSignal = limpiezaSignal;
        _logger         = logger;
        _opciones       = opciones.Value;
        _activo         = _opciones.WorkerSunatActivo;
        _intervalo      = TimeSpan.FromMinutes(_opciones.IntervaloConsultaMinutos);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_activo)
        {
            _logger.LogWarning("SunatCdrWorker está DESHABILITADO (WorkerSunatActivo = false). " +
                               "Actívalo en appsettings.json → LecturaCorreos:WorkerSunatActivo.");
            return;
        }

        _logger.LogInformation("SunatCdrWorker iniciado. Intervalo: {Intervalo} minutos.", _intervalo.TotalMinutes);

        // Espera a que LecturaCorreosSunatCdrWorker complete la limpieza de tablas
        // antes de iniciar el primer ciclo de consulta CDR.
        // Timeout de 10 min: si el worker de correos falla antes de señalizar, CDR arranca de todas formas.
        using var esperaCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        esperaCts.CancelAfter(TimeSpan.FromMinutes(10));
        try
        {
            await _limpiezaSignal.EsperarAsync(esperaCts.Token);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("SunatCdrWorker: parada solicitada durante la espera de señal de limpieza.");
            return;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("SunatCdrWorker: timeout (10 min) esperando señal de limpieza. Iniciando ciclos de CDR de todas formas.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Iniciando ciclo de consulta de CDR — {Hora}", DateTimeOffset.Now);

                try
                {
                    await ProcesarFacturasPendientesAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error general en el ciclo de consulta de CDR.");
                }

                try
                {
                    await Task.Delay(_intervalo, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Error crítico inesperado en SunatCdrWorker. El worker reintentará en 60s.");
                try { await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        _logger.LogInformation("SunatCdrWorker detenido.");
    }

    private async Task ProcesarFacturasPendientesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var repositorio = scope.ServiceProvider.GetRequiredService<ILecturaCorreosRepository>();
        var sunatService = scope.ServiceProvider.GetRequiredService<ISunatService>();

        var pendientes = await repositorio.ObtenerFacturasPendientesCdrAsync();
        var lista = pendientes.ToList();

        if (!lista.Any())
        {
            _logger.LogInformation("No hay facturas pendientes de consulta de CDR.");
            return;
        }

        _logger.LogInformation("Se encontraron {Cantidad} facturas pendientes de CDR.", lista.Count);

        foreach (var factura in lista)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var empresa = _opciones.Empresas.FirstOrDefault(e => e.Ruc == factura.RucReceptor);
            if (empresa is null)
            {
                _logger.LogWarning(
                    "No se encontró empresa configurada para el RUC receptor '{RucReceptor}' " +
                    "(factura ID {Id}, emisor {RucEmisor}). Se omite.",
                    factura.RucReceptor ?? "null", factura.Id, factura.Ruc);
                continue;
            }

            if (empresa.Sunat is null)
            {
                _logger.LogWarning(
                    "La empresa '{Nombre}' ({Ruc}) no tiene configuración SUNAT. Se omite factura ID {Id}.",
                    empresa.Nombre, empresa.Ruc, factura.Id);
                continue;
            }

            try
            {
                _logger.LogInformation(
                    "Consultando CDR en SUNAT para factura ID {Id} — {Tipo}/{Serie}/{Correlativo}",
                    factura.Id, factura.TipoComprobante, factura.Serie, factura.Correlativo);

                var respuesta = await sunatService.ConsultarCdrAsync(
                    factura.Ruc,
                    factura.TipoComprobante,
                    factura.Serie,
                    factura.Correlativo,
                    empresa,
                    cancellationToken);

                if (!respuesta.Exitoso)
                {
                    _logger.LogWarning(
                        "La consulta a SUNAT falló para factura ID {Id}: {Error}",
                        factura.Id, respuesta.ErrorDetalle);
                    await GuardarErrorSeguroAsync(repositorio, factura.Id, respuesta.ErrorDetalle ?? "Error desconocido");
                    continue;
                }

                if (respuesta.EstaAceptado)
                {
                    _logger.LogInformation(
                        "Factura ID {Id} ACEPTADA_SUNAT — Código: {Codigo}, Mensaje: {Mensaje}",
                        factura.Id, respuesta.CodigoRespuesta, respuesta.MensajeRespuesta);

                    await repositorio.ActualizarEstadoConIncrementoAsync(
                        factura.Id,
                        "ACEPTADO_SUNAT",
                        respuesta.CodigoRespuesta,
                        respuesta.MensajeRespuesta,
                        respuesta.CdrZip);
                }
                else if (respuesta.EstaRechazado)
                {
                    _logger.LogWarning(
                        "Factura ID {Id} RECHAZADA_SUNAT — Código: {Codigo}, Mensaje: {Mensaje}",
                        factura.Id, respuesta.CodigoRespuesta, respuesta.MensajeRespuesta);

                    await repositorio.ActualizarEstadoConIncrementoAsync(
                        factura.Id,
                        "RECHAZADO_SUNAT",
                        respuesta.CodigoRespuesta,
                        respuesta.MensajeRespuesta,
                        respuesta.CdrZip);
                }
                else
                {
                    // En proceso / sin CDR aún disponible.
                    // GuardarErrorSeguroAsync usa GuardarErrorConIncrementoAsync, por lo que
                    // INTENTOS siempre se incrementa atómicamente con el guardado del mensaje.
                    _logger.LogInformation(
                        "Factura ID {Id} aún en proceso en SUNAT — Código: {Codigo}, Mensaje: {Mensaje}",
                        factura.Id, respuesta.CodigoRespuesta, respuesta.MensajeRespuesta);

                    var mensajeEnProceso = string.IsNullOrWhiteSpace(respuesta.MensajeRespuesta)
                        ? $"En proceso — código: {respuesta.CodigoRespuesta}"
                        : respuesta.MensajeRespuesta;
                    await GuardarErrorSeguroAsync(repositorio, factura.Id, mensajeEnProceso);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar factura ID {Id}", factura.Id);
                await GuardarErrorSeguroAsync(repositorio, factura.Id, ex.Message);
            }
        }
    }

    /// <summary>
    /// Wrapper defensivo: si Oracle falla al guardar el error, loguea y continúa
    /// en lugar de matar el foreach de facturas pendientes.
    /// Usa <see cref="ILecturaCorreosRepository.GuardarErrorConIncrementoAsync"/> para que
    /// el incremento de INTENTOS y el guardado del error sean atómicos.
    /// </summary>
    private async Task GuardarErrorSeguroAsync(ILecturaCorreosRepository repositorio, long facturaId, string mensaje)
    {
        try
        {
            await repositorio.GuardarErrorConIncrementoAsync(facturaId, mensaje);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "No se pudo registrar el error en BD para factura ID {Id}. Se continúa con la siguiente.",
                facturaId);
        }
    }
}
