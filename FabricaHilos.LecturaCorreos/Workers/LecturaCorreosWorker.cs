namespace FabricaHilos.LecturaCorreos.Workers;

using FabricaHilos.LecturaCorreos.Config;
using FabricaHilos.LecturaCorreos.Data;
using FabricaHilos.LecturaCorreos.Services;
using FabricaHilos.LecturaCorreos.Services.Archivos;
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
    private readonly IArchivoDocumentoService        _archivoService;
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
        IArchivoDocumentoService               archivoService,
        ILogger<LecturaCorreosSunatCdrWorker>  logger)
    {
        _opciones                 = opciones.Value;
        _emailReader              = emailReader;
        _xmlParser                = xmlParser;
        _repositorio              = repositorio;
        _lecturaCorreosRepository = lecturaCorreosRepository;
        _limpiezaSignal           = limpiezaSignal;
        _circuitBreaker           = circuitBreaker;
        _archivoService           = archivoService;
        _logger                   = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opciones.WorkerCorreosActivo)
        {
            _logger.LogWarning("LecturaCorreosSunatCdrWorker está DESHABILITADO (WorkerCorreosActivo = false). " +
                               "Actívalo en appsettings.json → LecturaCorreos:WorkerCorreosActivo.");
            // Libera la señal para que SunatCdrWorker no quede bloqueado esperando.
            _limpiezaSignal.Completar();
            return;
        }

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
            {
                try
                {
                    await Task.WhenAll(cuentasActivas.Select(c => ProcesarCuentaAsync(c, stoppingToken)));
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Error inesperado en el procesamiento paralelo de cuentas.");
                }
            }
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

            // Agrupar adjuntos por correo (GrupoCorreo = UID IMAP del mensaje).
            // Para cada grupo se parsea el XML primero y se pasa el DocumentoXml al PDF,
            // de modo que ambos archivos compartan el mismo nombre normalizado SUNAT.
            var grupos = adjuntos
                .GroupBy(a => a.GrupoCorreo)
                .OrderBy(g => g.Key);

            var gruposProcesados = new HashSet<string>();

            foreach (var grupo in grupos)
            {
                if (ct.IsCancellationRequested) break;

                // Parseo previo del XML para extraer los datos de nombrado del grupo.
                DocumentoXml? docXmlGrupo = null;
                var xmlDelGrupo = grupo.FirstOrDefault(a => a.TipoAdjunto == "XML");
                if (xmlDelGrupo is not null)
                {
                    try
                    {
                        var r = _xmlParser.Parsear(
                            xmlDelGrupo.ContenidoXml ?? string.Empty,
                            xmlDelGrupo.NombreArchivo,
                            cuenta.Nombre,
                            xmlDelGrupo.Asunto,
                            xmlDelGrupo.Remitente,
                            xmlDelGrupo.FechaCorreo);
                        if (r.Estado == EstadoParseo.Exito)
                            docXmlGrupo = r.Documento;
                    }
                    catch (Exception exParseo)
                    {
                        _logger.LogDebug(exParseo,
                            "Pre-parseo del XML '{Archivo}' falló; el PDF del grupo se nombrará sin datos del XML.",
                            xmlDelGrupo.NombreArchivo);
                    }
                }

                // ── Paso 1: XMLs (y otros no-PDF) → capturar el documentoId generado ──
                long? documentoIdGrupo = null;
                foreach (var adjunto in grupo.Where(a => a.TipoAdjunto != "PDF"))
                {
                    if (ct.IsCancellationRequested) break;

                    try
                    {
                        var docId = await ProcesarAdjuntoAsync(
                            cuenta.Nombre,
                            adjunto.NombreArchivo,
                            adjunto.ContenidoXml ?? string.Empty,
                            adjunto.Asunto,
                            adjunto.Remitente,
                            adjunto.FechaCorreo,
                            ct);
                        documentoIdGrupo ??= docId;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Error al procesar adjunto '{Archivo}' de cuenta '{Cuenta}'. Se continúa con el siguiente.",
                            adjunto.NombreArchivo, cuenta.Nombre);
                    }
                }

                // Si había XML en el grupo pero documentoIdGrupo sigue null, significa que el XML
                // fue encontrado pero no pudo insertarse en BD (inválido, duplicado, error SP, etc.).
                // El Warning ya fue emitido por ProcesarAdjuntoAsync; aquí se refuerza el contexto.
                if (documentoIdGrupo is null && grupo.Any(a => a.TipoAdjunto == "XML"))
                    _logger.LogWarning(
                        "Cuenta {Cuenta}: el XML '{Archivo}' (grupo {Grupo}) no generó documentoId. " +
                        "El PDF del grupo se registrará como huérfano en FH_LECTCORREOS_PDF_ADJUNTOS.",
                        cuenta.Nombre,
                        grupo.FirstOrDefault(a => a.TipoAdjunto == "XML")?.NombreArchivo ?? "?",
                        grupo.Key);

                // ── Paso 2: PDFs → vinculados al documentoId del XML del mismo correo ──
                foreach (var adjunto in grupo.Where(a => a.TipoAdjunto == "PDF"))
                {
                    if (ct.IsCancellationRequested) break;

                    try
                    {
                        await ProcesarAdjuntoPdfAsync(cuenta.Nombre, adjunto, documentoIdGrupo, docXmlGrupo, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Error al procesar adjunto '{Archivo}' de cuenta '{Cuenta}'. Se continúa con el siguiente.",
                            adjunto.NombreArchivo, cuenta.Nombre);
                    }
                }

                // Registrar el grupo como completado solo si no hubo cancelación durante su procesamiento.
                if (!ct.IsCancellationRequested)
                    gruposProcesados.Add(grupo.Key);
            }

            // Marcar como leídos (y mover si aplica) los correos ya persistidos en BD y disco.
            if (gruposProcesados.Count > 0)
                await _emailReader.MarcarProcesadosAsync(cuenta, gruposProcesados, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Parada limpia del servicio: no contar como fallo de circuito.
            _logger.LogInformation("Cuenta {Nombre}: procesamiento cancelado por parada del servicio.", cuenta.Nombre);
        }
        catch (Exception ex)
        {
            // Fallo IMAP tras agotar reintentos → abrir circuito si se repite.
            _circuitBreaker.RegistrarFallo(cuenta.Nombre);
            _logger.LogError(ex, "Error al procesar la cuenta '{Nombre}'. Circuit breaker registra el fallo.", cuenta.Nombre);
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
                "Limpieza completada: {D} documentos, {L} líneas, {C} cuotas, {F} facturas CDR, {E} errores, {P} PDF adjuntos, {A} archivos. Total: {T} filas.",
                r.FilasDocumentos, r.FilasLineas, r.FilasCuotas, r.FilasFacturas, r.FilasErrores, r.FilasPdfAdjuntos, r.FilasArchivos, r.TotalFilas);
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

    /// <returns>ID del documento insertado, o <see langword="null"/> si fue omitido o hubo error.</returns>
    private async Task<long?> ProcesarAdjuntoAsync(
        string cuentaNombre, string nombreArchivo, string contenidoXml,
        string asunto, string remitente, DateTime fechaCorreo,
        CancellationToken ct = default)
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
                "EXCEPCION_PARSER", ex.Message, contenidoXml, ct);
            return null;
        }

        switch (resultado.Estado)
        {
            case EstadoParseo.CdrOmitido:
                _logger.LogDebug("Adjunto '{Archivo}' es CDR de SUNAT. Se omite.", nombreArchivo);
                return null;

            case EstadoParseo.XmlInvalido:
                _logger.LogWarning(
                    "Adjunto '{Archivo}' contiene XML inválido. Se registrará el error. Detalle: {Desc}",
                    nombreArchivo, resultado.Descripcion);
                await RegistrarErrorSeguroAsync(
                    nombreArchivo, ext, cuentaNombre, asunto, remitente,
                    "XML_INVALIDO", resultado.Descripcion ?? string.Empty, contenidoXml, ct);
                return null;

            case EstadoParseo.TipoNoReconocido:
                _logger.LogWarning(
                    "Adjunto '{Archivo}' no reconocido como XML UBL. Se registrará el error. Detalle: {Desc}",
                    nombreArchivo, resultado.Descripcion);
                await RegistrarErrorSeguroAsync(
                    nombreArchivo, ext, cuentaNombre, asunto, remitente,
                    "XML_NO_RECONOCIDO", resultado.Descripcion ?? string.Empty, contenidoXml, ct);
                return null;

            case EstadoParseo.Exito:
                break;

            default:
                _logger.LogError("Estado de parseo inesperado '{Estado}' para '{Archivo}'.",
                    resultado.Estado, nombreArchivo);
                return null;
        }

        var documento = resultado.Documento!;

        try
        {
            // Insertar cabecera
            var (idGenerado, codigo, mensaje) = await _repositorio.InsertarDocumentoAsync(documento);

            if (codigo == -2)
            {
                // El SP detectó duplicado. Intentamos reusar el ID que ya devolvió el SP;
                // si no lo trae (idGenerado == 0), consultamos directamente la BD.
                var idExistente = idGenerado > 0
                    ? idGenerado
                    : await _repositorio.ObtenerDocumentoIdAsync(documento.NumeroDocumento);

                _logger.LogWarning(
                    "Documento '{Numero}' ya existe en BD (duplicado). " +
                    "Se reutiliza ID={Id} para vincular el PDF del correo. Mensaje SP: {Msg}",
                    documento.NumeroDocumento, idExistente, mensaje);
                return idExistente;
            }

            if (codigo != 0)
            {
                _logger.LogError(
                    "Error al insertar documento '{Numero}'. Código: {Cod}. Mensaje: {Msg}",
                    documento.NumeroDocumento, codigo, mensaje);

                await RegistrarErrorSeguroAsync(
                    nombreArchivo, ext, cuentaNombre, asunto, remitente,
                    "ERROR_INSERT_DOCUMENTO", mensaje, contenidoXml, ct);
                return null;
            }

            _logger.LogInformation(
                "Documento '{Numero}' insertado con ID={Id}. Líneas: {L}, Cuotas: {C}.",
                documento.NumeroDocumento, idGenerado,
                documento.Lineas.Count, documento.Cuotas.Count);

            // Guardar XML en disco solo si la inserción en BD fue exitosa.
            var rutaXml = await _archivoService.GuardarXmlAsync(documento, contenidoXml, ct);
            if (!string.IsNullOrEmpty(rutaXml))
                await _repositorio.RegistrarArchivoAsync(idGenerado, "XML", nombreArchivo, Path.GetFileName(rutaXml), rutaXml);

            // Insertar líneas — aislado para que un fallo de BD no active el catch del documento cabecera.
            try
            {
                foreach (var linea in documento.Lineas)
                {
                    var (codLinea, msgLinea) = await _repositorio.InsertarLineaAsync(idGenerado, linea);
                    if (codLinea != 0)
                        _logger.LogWarning(
                            "Línea {Num} del documento {Doc}: código {Cod} - {Msg}",
                            linea.NumeroLinea, documento.NumeroDocumento, codLinea, msgLinea);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Error al insertar líneas del documento '{Numero}'. El cabecera ya está en BD.",
                    documento.NumeroDocumento);
            }

            // Insertar cuotas — aislado por la misma razón.
            try
            {
                foreach (var cuota in documento.Cuotas)
                {
                    var (codCuota, msgCuota) = await _repositorio.InsertarCuotaAsync(idGenerado, cuota);
                    if (codCuota != 0)
                        _logger.LogWarning(
                            "Cuota {Num} del documento {Doc}: código {Cod} - {Msg}",
                            cuota.NumeroCuota, documento.NumeroDocumento, codCuota, msgCuota);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Error al insertar cuotas del documento '{Numero}'. El cabecera ya está en BD.",
                    documento.NumeroDocumento);
            }

            // Encolar en FH_LECTCORREOS_FACTURAS para verificación CDR en SUNAT.
            // Tipos válidos: 01=Factura, 03=Boleta, 07=Nota de Crédito, 08=Nota de Débito.
            if (documento.TipoDocumento is "01" or "03" or "07" or "08"
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
            return idGenerado;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Excepción al persistir el documento '{Numero}'.", documento.NumeroDocumento);

            await RegistrarErrorSeguroAsync(
                nombreArchivo, ext, cuentaNombre, asunto, remitente,
                "EXCEPCION_PERSISTENCIA", $"{ex.Message} | {ex.StackTrace}", contenidoXml, ct);
            return null;
        }
    }

    /// <summary>
    /// Procesa un adjunto PDF según si está vinculado a un documento válido o es huérfano:
    /// - Con documento: guarda en disco y registra en FH_LECTCORREOS_ARCHIVOS.
    /// - Sin documento: inserta en FH_LECTCORREOS_PDF_ADJUNTOS para notificación al cliente.
    /// </summary>
    private async Task ProcesarAdjuntoPdfAsync(string cuentaNombre, AdjuntoCorreo adjunto, long? documentoId = null, DocumentoXml? documentoXml = null, CancellationToken ct = default)
    {
        _logger.LogDebug("Procesando PDF '{Archivo}' de cuenta '{Cuenta}'.",
            adjunto.NombreArchivo, cuentaNombre);

        try
        {
            // Rescate: si el PDF llegó en un correo separado al XML, intentar vincularlo
            // buscando el documento en BD por RUC+Serie+Correlativo extraídos del nombre del archivo.
            if (documentoId is null)
            {
                var (ruc, serie, corr) = TryExtraerRefDocDePdfNombre(adjunto.NombreArchivo);
                if (ruc is not null && serie is not null && corr is not null)
                {
                    var idRescatado = await _repositorio.ObtenerDocumentoIdPorRucYSerieAsync(ruc, serie, corr.Value);
                    if (idRescatado is not null)
                    {
                        _logger.LogInformation(
                            "PDF '{Archivo}' rescatado: vinculado al documento ID={Id} " +
                            "(RUC={Ruc} Serie={Serie} Correlativo={Corr}). No se registrará como huérfano.",
                            adjunto.NombreArchivo, idRescatado, ruc, serie, corr);
                        documentoId = idRescatado;
                    }
                }
            }

            if (documentoId is not null)
            {
                // PDF vinculado a un documento válido: disco + FH_LECTCORREOS_ARCHIVOS.
                // NO insertar en FH_LECTCORREOS_PDF_ADJUNTOS (tabla reservada para PDFs huérfanos).
                var rutaPdf = await _archivoService.GuardarPdfAsync(
                    adjunto.NombreArchivo, adjunto.ContenidoPdf ?? [], documentoXml, ct);
                if (!string.IsNullOrEmpty(rutaPdf))
                {
                    await _repositorio.RegistrarArchivoAsync(documentoId, "PDF", adjunto.NombreArchivo, Path.GetFileName(rutaPdf), rutaPdf);
                    _logger.LogInformation(
                        "PDF '{Archivo}' vinculado al documento ID={Id}: guardado en '{Ruta}'.",
                        adjunto.NombreArchivo, documentoId, rutaPdf);
                }
                else
                {
                    _logger.LogDebug(
                        "PDF '{Archivo}' vinculado al documento ID={Id}: omitido del disco (sin patrón SUNAT reconocible o error de escritura).",
                        adjunto.NombreArchivo, documentoId);
                }
                return;
            }

            // PDF huérfano (sin documento asociado): insertar en FH_LECTCORREOS_PDF_ADJUNTOS
            // para notificar al cliente que no envió su comprobante válido.
            var pdf = new AdjuntoPdf
            {
                NombreArchivo   = adjunto.NombreArchivo,
                CuentaCorreo    = cuentaNombre,
                AsuntoCorreo    = adjunto.Asunto,
                RemitenteCorreo = adjunto.Remitente,
                FechaCorreo     = adjunto.FechaCorreo,
                Contenido       = adjunto.ContenidoPdf ?? [],
            };

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
                    "Error al guardar PDF huérfano '{Archivo}'. Código: {Cod}. Mensaje: {Msg}",
                    adjunto.NombreArchivo, codigo, mensaje);
                return;
            }

            _logger.LogInformation(
                "PDF huérfano '{Archivo}' guardado en FH_LECTCORREOS_PDF_ADJUNTOS con ID={Id}.",
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
        string descripcion, string contenidoXml,
        CancellationToken ct = default)
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

    /// <summary>
    /// Extrae RUC emisor, serie y correlativo desde el nombre de un archivo PDF.
    /// Soporta dos patrones habituales:
    /// <list type="bullet">
    ///   <item>PDF-DOC-{SERIE}-{CORRELATIVO}{RUC}  (p.ej. PDF-DOC-E001-622720537870614.pdf)</item>
    ///   <item>{RUC}-{TIPO}-{SERIE}-{CORRELATIVO}  (p.ej. 20268214527-01-F001-90757.pdf)</item>
    /// </list>
    /// Devuelve (null, null, null) si el nombre no coincide con ningún patrón.
    /// </summary>
    private static (string? Ruc, string? Serie, long? Correlativo)
        TryExtraerRefDocDePdfNombre(string nombreArchivo)
    {
        var nombre = Path.GetFileNameWithoutExtension(nombreArchivo);

        // Patrón 1: PDF-DOC-{SERIE}-{CORRELATIVO}{RUC}
        // El RUC (11 dígitos) está concatenado al final del correlativo sin separador.
        if (nombre.StartsWith("PDF-DOC-", StringComparison.OrdinalIgnoreCase))
        {
            var resto = nombre[8..];                       // quita "PDF-DOC-"
            var guion = resto.IndexOf('-');
            if (guion > 0)
            {
                var serie    = resto[..guion];
                var combined = resto[(guion + 1)..];       // {CORRELATIVO}{RUC}
                if (combined.Length > 11)
                {
                    var rucStr  = combined[^11..];         // últimos 11 dígitos = RUC
                    var corrStr = combined[..^11];         // resto = correlativo
                    if (long.TryParse(rucStr, out _) && long.TryParse(corrStr, out var corr))
                        return (rucStr, serie, corr);
                }
            }
            return (null, null, null);
        }

        // Patrón 2: {RUC}-{TIPO}-{SERIE}-{CORRELATIVO}
        var partes = nombre.Split('-');
        if (partes.Length >= 4
            && partes[0].Length == 11
            && long.TryParse(partes[0], out _)
            && long.TryParse(partes[3], out var correlativo))
        {
            return (partes[0], partes[2], correlativo);
        }

        return (null, null, null);
    }
}
