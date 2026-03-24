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
        await _limpiezaSignal.EsperarAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Iniciando ciclo de consulta de CDR — {Hora}", DateTimeOffset.Now);

            try
            {
                await ProcesarFacturasPendientesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general en el ciclo de consulta de CDR.");
            }

            await Task.Delay(_intervalo, stoppingToken);
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
                await repositorio.IncrementarIntentosAsync(factura.Id);

                _logger.LogInformation(
                    "Consultando CDR en SUNAT para factura ID {Id} — {Tipo}/{Serie}/{Correlativo}",
                    factura.Id, factura.TipoComprobante, factura.Serie, factura.Correlativo);

                var respuesta = await sunatService.ConsultarCdrAsync(
                    factura.Ruc,
                    factura.TipoComprobante,
                    factura.Serie,
                    factura.Correlativo,
                    empresa);

                if (!respuesta.Exitoso)
                {
                    _logger.LogWarning(
                        "La consulta a SUNAT falló para factura ID {Id}: {Error}",
                        factura.Id, respuesta.ErrorDetalle);
                    await repositorio.GuardarErrorAsync(factura.Id, respuesta.ErrorDetalle ?? "Error desconocido");
                    continue;
                }

                if (respuesta.EstaAceptado)
                {
                    _logger.LogInformation(
                        "Factura ID {Id} ACEPTADA_SUNAT — Código: {Codigo}, Mensaje: {Mensaje}",
                        factura.Id, respuesta.CodigoRespuesta, respuesta.MensajeRespuesta);

                    await repositorio.ActualizarEstadoAsync(
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

                    await repositorio.ActualizarEstadoAsync(
                        factura.Id,
                        "RECHAZADO_SUNAT",
                        respuesta.CodigoRespuesta,
                        respuesta.MensajeRespuesta,
                        respuesta.CdrZip);
                }
                else
                {
                    // En proceso / sin CDR aún disponible
                    _logger.LogInformation(
                        "Factura ID {Id} aún en proceso en SUNAT — Código: {Codigo}, Mensaje: {Mensaje}",
                        factura.Id, respuesta.CodigoRespuesta, respuesta.MensajeRespuesta);

                    if (!string.IsNullOrWhiteSpace(respuesta.MensajeRespuesta))
                        await repositorio.GuardarErrorAsync(factura.Id, respuesta.MensajeRespuesta);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar factura ID {Id}", factura.Id);
                await repositorio.GuardarErrorAsync(factura.Id, ex.Message);
            }
        }
    }
}
