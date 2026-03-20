namespace FabricaHilos.LecturaCorreos.Services.Email;

using FabricaHilos.LecturaCorreos.Config;
using FabricaHilos.LecturaCorreos.Services.Email.Conexion;
using FabricaHilos.LecturaCorreos.Services.Email.Lectores;
using FabricaHilos.LecturaCorreos.Models;
using MailKit;
using MailKit.Search;
using Microsoft.Extensions.Logging;
using MimeKit;

/// <summary>
/// Orquesta la lectura de correos IMAP delegando cada responsabilidad
/// a un servicio especializado: conexión, lectura XML, PDF y ZIP.
/// </summary>
public class ImapEmailReaderService : IEmailReaderService
{
    private readonly IImapConexionService           _conexion;
    private readonly ILectorAdjuntoXml              _lectorXml;
    private readonly ILectorAdjuntoPdf              _lectorPdf;
    private readonly ILectorAdjuntoZip              _lectorZip;
    private readonly ILogger<ImapEmailReaderService> _logger;

    public ImapEmailReaderService(
        IImapConexionService            conexion,
        ILectorAdjuntoXml               lectorXml,
        ILectorAdjuntoPdf               lectorPdf,
        ILectorAdjuntoZip               lectorZip,
        ILogger<ImapEmailReaderService> logger)
    {
        _conexion   = conexion;
        _lectorXml  = lectorXml;
        _lectorPdf  = lectorPdf;
        _lectorZip  = lectorZip;
        _logger     = logger;
    }

    // Reintentos en caso de error de conexión transitorio: 5 s → 15 s → 30 s.
    private static readonly TimeSpan[] BackoffImap =
        [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30)];
    private const int MaxIntentosImap = 3;

    public async Task<List<AdjuntoCorreo>> ObtenerAdjuntosAsync(
        CuentaCorreoOptions cuenta, int maxCorreos, CancellationToken ct)
    {
        for (int intento = 1; intento <= MaxIntentosImap; intento++)
        {
            try
            {
                return await EscanearCuentaAsync(cuenta, maxCorreos, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _logger.LogInformation("Cuenta {Nombre}: lectura cancelada por parada del servicio.", cuenta.Nombre);
                return [];
            }
            catch (Exception ex) when (intento < MaxIntentosImap)
            {
                var espera = BackoffImap[intento - 1];
                _logger.LogWarning(ex,
                    "Cuenta '{Nombre}': fallo IMAP en intento {N}/{Max}. Reintentando en {Seg} s...",
                    cuenta.Nombre, intento, MaxIntentosImap, (int)espera.TotalSeconds);
                try   { await Task.Delay(espera, ct); }
                catch (OperationCanceledException) { return []; }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Cuenta '{Nombre}': error de conexión IMAP a {Host}:{Port} tras {Max} intento(s). Se omite esta cuenta.",
                    cuenta.Nombre, cuenta.ImapHost, cuenta.ImapPort, MaxIntentosImap);
                return [];
            }
        }

        return [];
    }

    private async Task<List<AdjuntoCorreo>> EscanearCuentaAsync(
        CuentaCorreoOptions cuenta, int maxCorreos, CancellationToken ct)
    {
        var resultado = new List<AdjuntoCorreo>();

        using var client = await _conexion.ConectarAsync(cuenta, ct);

            var carpeta = await client.GetFolderAsync(cuenta.Carpeta, ct);
            await carpeta.OpenAsync(FolderAccess.ReadWrite, ct);

            var uids = await carpeta.SearchAsync(SearchQuery.NotSeen, ct);
            _logger.LogInformation("Cuenta {Nombre}: {Count} correos no leídos encontrados.",
                cuenta.Nombre, uids.Count);

            IMailFolder? carpetaProcesados = null;
            if (cuenta.MoverProcesado && !string.IsNullOrWhiteSpace(cuenta.CarpetaProcesados))
            {
                try
                {
                    carpetaProcesados = await client.GetFolderAsync(cuenta.CarpetaProcesados, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Cuenta {Nombre}: no se pudo acceder a la carpeta '{Carpeta}'. Se continuará sin mover mensajes.",
                        cuenta.Nombre, cuenta.CarpetaProcesados);
                }
            }

            var candidatos    = uids.Take(maxCorreos).ToList();
            int procesados    = 0;
            bool conexionOk   = true;   // se pone en false tras un timeout de mensaje

            for (int i = 0; i < candidatos.Count && conexionOk; i++)
            {
                if (ct.IsCancellationRequested) break;

                var uid = candidatos[i];

                // Timeout individual: si supera 5 min, el correo queda NO LEÍDO para el próximo ciclo.
                using var msgCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                msgCts.CancelAfter(TimeSpan.FromMinutes(5));

                try
                {
                    _logger.LogDebug("Cuenta {Nombre}: descargando mensaje {Num}/{Total} (UID {Uid})...",
                        cuenta.Nombre, i + 1, candidatos.Count, uid);

                    var mensaje   = await carpeta.GetMessageAsync(uid, msgCts.Token);
                    var asunto    = mensaje.Subject ?? string.Empty;
                    var remitente = mensaje.From?.Mailboxes.FirstOrDefault()?.Address ?? string.Empty;
                    var fecha     = mensaje.Date.UtcDateTime;

                    var adjuntosMensaje = await ExtraerAdjuntosAsync(
                        mensaje, cuenta.Nombre, asunto, remitente, fecha, ct);

                    if (adjuntosMensaje.Count > 0)
                    {
                        var grupoId = uid.Id.ToString();
                        adjuntosMensaje.ForEach(a => a.GrupoCorreo = grupoId);
                        resultado.AddRange(adjuntosMensaje);

                        if (cuenta.MarcarLeido)
                            await carpeta.AddFlagsAsync(uid, MessageFlags.Seen, true, ct);
                        if (cuenta.MoverProcesado && carpetaProcesados is not null)
                            await carpeta.MoveToAsync(uid, carpetaProcesados, ct);

                        procesados++;
                    }
                    else
                    {
                        _logger.LogDebug(
                            "Cuenta {Nombre}: UID {Uid} ('{Asunto}') sin adjuntos XML/PDF/ZIP. Queda no leído.",
                            cuenta.Nombre, uid, asunto);
                    }
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    // Timeout de 5 min superado. La conexión TCP puede haber quedado en estado
                    // indeterminado; se interrumpe el escaneo de esta cuenta para esta vuelta.
                    _logger.LogWarning(
                        "Cuenta {Nombre}: timeout (5 min) descargando UID {Uid}. Se interrumpe el escaneo; el correo queda pendiente para el próximo ciclo.",
                        cuenta.Nombre, uid);
                    conexionOk = false;
                }
                catch (Exception ex)
                {
                    // Error aislado en este mensaje: se continúa con el siguiente.
                    _logger.LogWarning(ex,
                        "Cuenta {Nombre}: error al procesar UID {Uid}. El correo queda pendiente para el próximo ciclo.",
                        cuenta.Nombre, uid);
                }
            }

                _logger.LogInformation(
                    "Cuenta {Nombre}: {Procesados}/{Candidatos} mensajes con adjuntos procesados.",
                    cuenta.Nombre, procesados, candidatos.Count);

                // CancellationToken.None: desconectar siempre aunque el token externo esté cancelado.
                await client.DisconnectAsync(quit: conexionOk, CancellationToken.None);

                return resultado;
            }

    // ── Extracción de adjuntos por tipo ──────────────────────────────────────

    private async Task<List<AdjuntoCorreo>> ExtraerAdjuntosAsync(
        MimeMessage mensaje, string cuentaNombre,
        string asunto, string remitente, DateTime fecha, CancellationToken ct)
    {
        var adjuntos = new List<AdjuntoCorreo>();

        foreach (var parte in mensaje.BodyParts.OfType<MimePart>())
        {
            if (ct.IsCancellationRequested) break;

            var nombre = parte.FileName ?? string.Empty;
            if (string.IsNullOrEmpty(nombre) || parte.Content is null) continue;

            try
            {
                if (nombre.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    adjuntos.Add(await _lectorXml.ExtraerAsync(parte, asunto, remitente, fecha, ct));
                    _logger.LogDebug("Cuenta {Nombre}: XML '{Archivo}' leído.", cuentaNombre, nombre);
                }
                else if (nombre.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    adjuntos.Add(await _lectorPdf.ExtraerAsync(parte, asunto, remitente, fecha, ct));
                    _logger.LogDebug("Cuenta {Nombre}: PDF '{Archivo}' leído.", cuentaNombre, nombre);
                }
                else if (nombre.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    var deZip = await _lectorZip.ExtraerAsync(parte, asunto, remitente, fecha, ct);
                    adjuntos.AddRange(deZip);
                    _logger.LogDebug("Cuenta {Nombre}: ZIP '{Archivo}' — {N} adjunto(s) extraídos.",
                        cuentaNombre, nombre, deZip.Count);
                }
            }
            catch (Exception ex)
            {
                // Error en un adjunto puntual: se omite y se continúa con el resto del mensaje.
                _logger.LogWarning(ex,
                    "Cuenta {Nombre}: error al leer adjunto '{Archivo}'. Se omite este adjunto.",
                    cuentaNombre, nombre);
            }
        }

        return adjuntos;
    }
}
