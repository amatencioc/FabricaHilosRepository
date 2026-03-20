namespace FabricaHilos.LecturaCorreos.Workers;

using FabricaHilos.LecturaCorreos.Config;
using FabricaHilos.LecturaCorreos.Data;
using FabricaHilos.LecturaCorreos.Services;
using FabricaHilos.LecturaCorreos.Services.Email;
using FabricaHilos.LecturaCorreos.Models;
using FabricaHilos.LecturaCorreos.Services.Parsers;
using FabricaHilos.LecturaCorreos.Services.Signals;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

public class LecturaCorreosSunatCdrWorker : BackgroundService
{
    private readonly LecturaCorreosOptions   _opciones;
    private readonly IEmailReaderService     _emailReader;
    private readonly IXmlParserService       _xmlParser;
    private readonly ILogisticaRepository            _repositorio;
    private readonly ILecturaCorreosRepository       _lecturaCorreosRepository;
    private readonly ILimpiezaSignal                 _limpiezaSignal;
    private readonly ICuentaCircuitBreaker            _circuitBreaker;
    private readonly ILogger<LecturaCorreosSunatCdrWorker> _logger;

    // Garantiza que la limpieza/señal inicial se ejecute solo una vez,
    // independientemente de cuántos ciclos complete el worker.
    private bool _limpiezaRealizada;

    public LecturaCorreosSunatCdrWorker(
        IOptions<LecturaCorreosOptions>        opciones,
        IEmailReaderService                    emailReader,
        IXmlParserService                      xmlParser,
        ILogisticaRepository                   repositorio,
        ILecturaCorreosRepository              lecturaCorreosRepository,
        ILimpiezaSignal                        limpiezaSignal,
        ICuentaCircuitBreaker                  circuitBreaker,
        ILogger<LecturaCorreosSunatCdrWorker>  logger)
    {
        _opciones                 = opciones.Value;
        _emailReader              = emailReader;
        _xmlParser                = xmlParser;
        _repositorio              = repositorio;
        _lecturaCorreosRepository = lecturaCorreosRepository;
        _limpiezaSignal           = limpiezaSignal;
        _circuitBreaker           = circuitBreaker;
        _logger                   = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LecturaCorreosSunatCdrWorker iniciado. Intervalo: {Minutos} min.",
            _opciones.IntervaloMinutos);

        while (!stoppingToken.IsCancellationRequested)
        {
            // Solo en el primer ciclo: limpiar BD (si aplica) y desbloquear SunatCdrWorker.
            if (!_limpiezaRealizada)
            {
                if (_opciones.LimpiarBdAlIniciar)
                    await LimpiarBdParaPruebasAsync(stoppingToken);
                else
                    _limpiezaSignal.Completar();
                _limpiezaRealizada = true;
            }

            _logger.LogInformation("Iniciando ciclo de lectura de correos...");

            var sw = Stopwatch.StartNew();

            // Las cuentas activas y no suspendidas se procesan en paralelo.
            var cuentasActivas = _opciones.Cuentas
                .Where(c => c.Activa && !_circuitBreaker.EstaSuspendida(c.Nombre))
                .ToList();

            if (cuentasActivas.Count > 0)
                await Task.WhenAll(cuentasActivas.Select(c => ProcesarCuentaAsync(c, stoppingToken)));
            else
                _logger.LogWarning("Ninguna cuenta activa disponible (todas suspendidas o desactivadas).");

            sw.Stop();

            var intervalo = TimeSpan.FromMinutes(_opciones.IntervaloMinutos);
            if (sw.Elapsed > intervalo)
                _logger.LogWarning(
                    "⚠️ Ciclo tardó {Elapsed:F1} min — supera el intervalo configurado de {Intervalo} min. Considera reducir MaxCorreosPorCiclo o aumentar el intervalo.",
                    sw.Elapsed.TotalMinutes, _opciones.IntervaloMinutos);
            else
                _logger.LogInformation(
                    "Ciclo completado en {Elapsed:F1} min. Próximo ciclo en {Intervalo} min.",
                    sw.Elapsed.TotalMinutes, _opciones.IntervaloMinutos);

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(_opciones.IntervaloMinutos), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    // ── Procesamiento por cuenta ──────────────────────────────────────────────

    /// <summary>
    /// Procesa una cuenta de correo completa: conexión IMAP, descarga y persistencia de adjuntos.
    /// Integra el circuit breaker: registra éxito o fallo al final de cada intento.
    /// </summary>
    private async Task ProcesarCuentaAsync(CuentaCorreoOptions cuenta, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return;

        _logger.LogInformation("Procesando cuenta: {Nombre} ({Proveedor})", cuenta.Nombre, cuenta.Proveedor);

        try
        {
            var adjuntos = await _emailReader.ObtenerAdjuntosAsync(
                cuenta, _opciones.MaxCorreosPorCiclo, ct);

            // Conexión exitosa (aunque no haya adjuntos) → resetear contador de fallos.
            _circuitBreaker.RegistrarExito(cuenta.Nombre);

            _logger.LogInformation("Cuenta {Nombre}: {Count} adjunto(s) encontrado(s) (XML + PDF).",
                cuenta.Nombre, adjuntos.Count);

            foreach (var adjunto in adjuntos)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    if (adjunto.TipoAdjunto == "PDF")
                        await ProcesarAdjuntoPdfAsync(cuenta.Nombre, adjunto);
                    else
                        await ProcesarAdjuntoAsync(
                            cuenta.Nombre,
                            adjunto.NombreArchivo,
                            adjunto.ContenidoXml ?? string.Empty,
                            adjunto.Asunto,
                            adjunto.Remitente,
                            adjunto.FechaCorreo);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error al procesar adjunto '{Archivo}' de cuenta '{Cuenta}'. Se continúa con el siguiente.",
                        adjunto.NombreArchivo, cuenta.Nombre);
                }
            }
        }
        catch (Exception ex)
        {
            // Fallo de conexión IMAP (ya agotó reintentos internos) → abrir circuito si se repite.
            _circuitBreaker.RegistrarFallo(cuenta.Nombre);
            _logger.LogError(ex, "Error inesperado al procesar la cuenta {Nombre}.", cuenta.Nombre);
        }
    }

    // ── SOLO PRUEBAS ──────────────────────────────────────────────────────────
    private async Task LimpiarBdParaPruebasAsync(CancellationToken ct)
    {
        _logger.LogWarning("⚠️  LimpiarBdAlIniciar=true — eliminando datos de todas las tablas del proceso...");
        try
        {
            var r = await _lecturaCorreosRepository.LimpiarTablasAsync(ct);
            _logger.LogWarning(
                "Limpieza completada: {D} documentos, {L} líneas, {C} cuotas, {F} facturas CDR, {E} errores. Total: {T} filas.",
                r.FilasDocumentos, r.FilasLineas, r.FilasCuotas, r.FilasFacturas, r.FilasErrores, r.TotalFilas);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al limpiar las tablas para pruebas. Se continuará de todas formas.");
        }
        finally
        {
            // Siempre desbloquear SunatCdrWorker, haya o no error en la limpieza.
            _limpiezaSignal.Completar();
        }
    }

    private async Task ProcesarAdjuntoAsync(
        string cuentaNombre, string nombreArchivo, string contenidoXml,
        string asunto, string remitente, DateTime fechaCorreo)
    {
        _logger.LogDebug("Procesando adjunto '{Archivo}' de cuenta '{Cuenta}'.",
            nombreArchivo, cuentaNombre);

        var ext = System.IO.Path.GetExtension(nombreArchivo);

        ResultadoParseo resultado;
        try
        {
            resultado = _xmlParser.Parsear(
                contenidoXml, nombreArchivo, cuentaNombre, asunto, remitente, fechaCorreo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Excepción en el parser XML para '{Archivo}'. Se registrará como error.",
                nombreArchivo);
            await RegistrarErrorSeguroAsync(
                nombreArchivo, ext, cuentaNombre, asunto, remitente,
                "EXCEPCION_PARSER", ex.Message, contenidoXml);
            return;
        }

        switch (resultado.Estado)
        {
            case EstadoParseo.CdrOmitido:
                _logger.LogDebug("Adjunto '{Archivo}' es CDR de SUNAT. Se omite.", nombreArchivo);
                return;

            case EstadoParseo.XmlInvalido:
                _logger.LogWarning(
                    "Adjunto '{Archivo}' contiene XML inválido. Se registrará el error. Detalle: {Desc}",
                    nombreArchivo, resultado.Descripcion);
                await RegistrarErrorSeguroAsync(
                    nombreArchivo, ext, cuentaNombre, asunto, remitente,
                    "XML_INVALIDO", resultado.Descripcion ?? string.Empty, contenidoXml);
                return;

            case EstadoParseo.TipoNoReconocido:
                _logger.LogWarning(
                    "Adjunto '{Archivo}' no reconocido como XML UBL. Se registrará el error. Detalle: {Desc}",
                    nombreArchivo, resultado.Descripcion);
                await RegistrarErrorSeguroAsync(
                    nombreArchivo, ext, cuentaNombre, asunto, remitente,
                    "XML_NO_RECONOCIDO", resultado.Descripcion ?? string.Empty, contenidoXml);
                return;

            case EstadoParseo.Exito:
                break;

            default:
                _logger.LogError("Estado de parseo inesperado '{Estado}' para '{Archivo}'.",
                    resultado.Estado, nombreArchivo);
                return;
        }

        var documento = resultado.Documento!;

        try
        {
            // Insertar cabecera
            var (idGenerado, codigo, mensaje) = await _repositorio.InsertarDocumentoAsync(documento);

            if (codigo == -2)
            {
                _logger.LogWarning(
                    "Documento '{Numero}' ya existe en BD (duplicado). Se omite. Mensaje: {Msg}",
                    documento.NumeroDocumento, mensaje);
                return;
            }

            if (codigo != 0)
            {
                _logger.LogError(
                    "Error al insertar documento '{Numero}'. Código: {Cod}. Mensaje: {Msg}",
                    documento.NumeroDocumento, codigo, mensaje);

                await RegistrarErrorSeguroAsync(
                    nombreArchivo, ext, cuentaNombre, asunto, remitente,
                    "ERROR_INSERT_DOCUMENTO", mensaje, contenidoXml);
                return;
            }

            _logger.LogInformation(
                "Documento '{Numero}' insertado con ID={Id}. Líneas: {L}, Cuotas: {C}.",
                documento.NumeroDocumento, idGenerado,
                documento.Lineas.Count, documento.Cuotas.Count);

            // Insertar líneas
            foreach (var linea in documento.Lineas)
            {
                var (codLinea, msgLinea) = await _repositorio.InsertarLineaAsync(idGenerado, linea);
                if (codLinea != 0)
                    _logger.LogWarning(
                        "Línea {Num} del documento {Doc}: código {Cod} - {Msg}",
                        linea.NumeroLinea, documento.NumeroDocumento, codLinea, msgLinea);
            }

            // Insertar cuotas
            foreach (var cuota in documento.Cuotas)
            {
                var (codCuota, msgCuota) = await _repositorio.InsertarCuotaAsync(idGenerado, cuota);
                if (codCuota != 0)
                    _logger.LogWarning(
                        "Cuota {Num} del documento {Doc}: código {Cod} - {Msg}",
                        cuota.NumeroCuota, documento.NumeroDocumento, codCuota, msgCuota);
            }

            // Encolar en FH_LECTCORREOS_FACTURAS para verificación CDR en SUNAT
            if (documento.TipoDocumento is "01" or "03"
                && int.TryParse(documento.Correlativo, out var correlativoInt))
            {
                try
                {
                    await _lecturaCorreosRepository.InsertarFacturaPendienteCdrAsync(new FacturaCorreo
                    {
                        Ruc                 = documento.RucEmisor,
                        TipoComprobante     = documento.TipoDocumento,
                        Serie               = documento.Serie,
                        Correlativo         = correlativoInt,
                        Estado              = "PENDIENTE_CDR",
                        DocumentoId         = idGenerado,
                        DocumentoReferencia = documento.NumeroDocumento
                    });

                    _logger.LogInformation(
                        "Documento '{Numero}' encolado en FH_LECTCORREOS_FACTURAS para verificación CDR en SUNAT.",
                        documento.NumeroDocumento);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "No se pudo registrar el documento '{Numero}' para verificación CDR. Se continuará sin bloquear el flujo.",
                        documento.NumeroDocumento);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Excepción al persistir el documento '{Numero}'.", documento.NumeroDocumento);

            await RegistrarErrorSeguroAsync(
                nombreArchivo, ext, cuentaNombre, asunto, remitente,
                "EXCEPCION_PERSISTENCIA", $"{ex.Message} | {ex.StackTrace}", contenidoXml);
        }
    }

    /// <summary>
    /// Persiste un adjunto PDF en la tabla FH_LECTCORREOS_PDF_ADJUNTOS
    /// vía PKG_LC_LOGISTICA.SP_GUARDAR_PDF_ADJUNTO.
    /// </summary>
    private async Task ProcesarAdjuntoPdfAsync(string cuentaNombre, AdjuntoCorreo adjunto)
    {
        _logger.LogDebug("Guardando PDF '{Archivo}' de cuenta '{Cuenta}'.",
            adjunto.NombreArchivo, cuentaNombre);

        var pdf = new AdjuntoPdf
        {
            NombreArchivo   = adjunto.NombreArchivo,
            CuentaCorreo    = cuentaNombre,
            AsuntoCorreo    = adjunto.Asunto,
            RemitenteCorreo = adjunto.Remitente,
            FechaCorreo     = adjunto.FechaCorreo,
            Contenido       = adjunto.ContenidoPdf ?? [],
        };

        try
        {
            var (idGenerado, codigo, mensaje) = await _repositorio.GuardarAdjuntoPdfAsync(pdf);

            if (codigo == -2)
            {
                _logger.LogWarning(
                    "PDF '{Archivo}' ya existe en BD (duplicado). Se omite. Mensaje: {Msg}",
                    adjunto.NombreArchivo, mensaje);
                return;
            }

            if (codigo != 0)
            {
                _logger.LogError(
                    "Error al guardar PDF '{Archivo}'. Código: {Cod}. Mensaje: {Msg}",
                    adjunto.NombreArchivo, codigo, mensaje);
                return;
            }

            _logger.LogInformation(
                "PDF '{Archivo}' guardado con ID={Id}.",
                adjunto.NombreArchivo, idGenerado);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Excepción al persistir el PDF '{Archivo}'.", adjunto.NombreArchivo);
        }
    }

    /// <summary>
    /// Registra un error en el repositorio sin propagar excepciones para no interrumpir el ciclo.
    /// </summary>
    private async Task RegistrarErrorSeguroAsync(
        string nombreArchivo, string extension, string cuentaNombre,
        string asunto, string remitente, string tipoError,
        string descripcion, string contenidoXml)
    {
        try
        {
            await _repositorio.RegistrarErrorAsync(
                nombreArchivo, extension, cuentaNombre, asunto, remitente,
                tipoError, descripcion, string.Empty, contenidoXml);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "No se pudo registrar el error '{Tipo}' para '{Archivo}' en la base de datos.",
                tipoError, nombreArchivo);
        }
    }
}
