namespace FabricaHilos.LecturaCorreos.Services.Email;

using FabricaHilos.LecturaCorreos.Config;
using FabricaHilos.LecturaCorreos.Services.Email.Conexion;
using FabricaHilos.LecturaCorreos.Services.Email.Lectores;
using FabricaHilos.LecturaCorreos.Services.Email.Portales;
using FabricaHilos.LecturaCorreos.Models;
using MailKit;
using MailKit.Search;
using Microsoft.Extensions.Logging;
using MimeKit;
using System.Net;
using System.Text.RegularExpressions;

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
    private readonly IPortalDescargaService         _portalService;
    private readonly ILogger<ImapEmailReaderService> _logger;

    public ImapEmailReaderService(
        IImapConexionService            conexion,
        ILectorAdjuntoXml               lectorXml,
        ILectorAdjuntoPdf               lectorPdf,
        ILectorAdjuntoZip               lectorZip,
        IPortalDescargaService          portalService,
        ILogger<ImapEmailReaderService> logger)
    {
        _conexion       = conexion;
        _lectorXml      = lectorXml;
        _lectorPdf      = lectorPdf;
        _lectorZip      = lectorZip;
        _portalService  = portalService;
        _logger         = logger;
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
                    "Cuenta '{Nombre}': error de conexión IMAP a {Host}:{Port} tras {Max} intento(s). Se propaga al circuit breaker.",
                    cuenta.Nombre, cuenta.ImapHost, cuenta.ImapPort, MaxIntentosImap);
                throw;
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
                        // No se marca como leído aquí: se difiere a MarcarProcesadosAsync,
                        // que se invoca DESPUÉS de persistir en BD y disco.
                        var grupoId = uid.Id.ToString();
                        adjuntosMensaje.ForEach(a => a.GrupoCorreo = grupoId);
                        resultado.AddRange(adjuntosMensaje);
                        procesados++;
                    }
                    else
                    {
                        // Sin adjuntos: no hay datos que perder, se puede marcar ahora.
                        if (cuenta.MarcarLeido)
                            await carpeta.AddFlagsAsync(uid, MessageFlags.Seen, true, ct);
                        _logger.LogInformation(
                            "Cuenta {Nombre}: UID {Uid} ('{Asunto}') sin adjuntos XML/PDF/ZIP.{Accion}",
                            cuenta.Nombre, uid, asunto,
                            cuenta.MarcarLeido ? " Marcado como leído." : " No se marca (MarcarLeido=false).");
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

        // Detectar message/rfc822 cuyo Message no pudo ser parseado por MimeKit:
        // en ese caso IterarPartes los silencia y sus adjuntos serían invisibles.
        AdvertirMessagePartsVacios(mensaje.Body, cuentaNombre, asunto);

        // IterarPartes desciende recursivamente en correos reenviados (RV:/FW:).
        // Un correo reenviado embebe el original como MessagePart (message/rfc822),
        // que BodyParts.OfType<MimePart>() no traversa, dejando XML/PDF invisibles.
        foreach (var parte in IterarPartes(mensaje.Body))
        {
            if (ct.IsCancellationRequested) break;

            var nombre = parte.FileName ?? string.Empty;
            if (string.IsNullOrEmpty(nombre) || parte.Content is null) continue;

            try
            {
                if (nombre.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    adjuntos.Add(await _lectorXml.ExtraerAsync(parte, asunto, remitente, fecha, ct));
                    _logger.LogInformation("Cuenta {Nombre}: XML '{Archivo}' leído.", cuentaNombre, nombre);
                }
                else if (nombre.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    adjuntos.Add(await _lectorPdf.ExtraerAsync(parte, asunto, remitente, fecha, ct));
                    _logger.LogInformation("Cuenta {Nombre}: PDF '{Archivo}' leído.", cuentaNombre, nombre);
                }
                else if (nombre.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    var deZip = await _lectorZip.ExtraerAsync(parte, asunto, remitente, fecha, ct);
                    adjuntos.AddRange(deZip);
                    _logger.LogInformation("Cuenta {Nombre}: ZIP '{Archivo}' — {N} adjunto(s) extraídos.",
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

        // Si el correo no tiene adjuntos directos, buscar links en el cuerpo HTML.
        // Soporta dos patrones:
        //   1. Links directos XML/PDF por contexto (efacturacion.pe: "formato XML ... aquí")
        //   2. Link CONSULTAR hacia portal JSF (BizLinks: consultarDocumento.jsf)
        if (adjuntos.Count == 0)
        {
            var enlace = new EnlacePortal
            {
                Asunto      = asunto,
                Remitente   = remitente,
                FechaCorreo = fecha,
            };

            ExtraerLinksDirectosDeMensaje(mensaje, enlace);

            if (!enlace.TieneLinksDirectos)
                enlace.UrlConsultar = ExtraerUrlConsultarDeMensaje(mensaje) ?? string.Empty;

            if (enlace.TieneLinksDirectos || !string.IsNullOrEmpty(enlace.UrlConsultar))
            {
                _logger.LogInformation(
                    "Cuenta {Nombre}: sin adjuntos directos — {Patron} detectado. Accediendo al portal...",
                    cuentaNombre,
                    enlace.TieneLinksDirectos ? "links directos XML/PDF" : "link CONSULTAR");

                var dePortal = await _portalService.DescargarAdjuntosAsync(enlace, ct);
                adjuntos.AddRange(dePortal);
            }
        }

        return adjuntos;
    }

    /// <summary>
    /// Detecta el patrón "links directos XML/PDF" en el cuerpo del correo.
    /// Usado por proveedores como efacturacion.pe que incluyen links de descarga
    /// directa en el HTML: "...en formato XML lo puede obtener accediendo aquí..."
    /// Rellena <see cref="EnlacePortal.UrlXmlDirecto"/> y/o <see cref="EnlacePortal.UrlPdfDirecto"/>.
    /// </summary>
    private static void ExtraerLinksDirectosDeMensaje(MimeMessage mensaje, EnlacePortal enlace)
    {
        IEnumerable<string> ObtenerHtmls()
        {
            if (!string.IsNullOrWhiteSpace(mensaje.HtmlBody))
                yield return mensaje.HtmlBody;
            foreach (var parte in IterarPartes(mensaje.Body))
            {
                if (parte is TextPart tp
                    && tp.ContentType.IsMimeType("text", "html")
                    && !string.IsNullOrWhiteSpace(tp.Text))
                    yield return tp.Text;
            }
        }

        foreach (var html in ObtenerHtmls())
        {
            enlace.UrlXmlDirecto ??= ExtraerUrlPorContexto(html, "XML");
            enlace.UrlPdfDirecto ??= ExtraerUrlPorContexto(html, "PDF");
            if (enlace.TieneLinksDirectos) break;
        }
    }

    /// <summary>
    /// Busca en el HTML un href cuya frase de contexto cercana contiene "formato {tipo}".
    /// Cubre: "formato XML[hasta 300 chars]&lt;a href='URL'&gt;"
    /// </summary>
    private static string? ExtraerUrlPorContexto(string html, string tipo)
    {
        var m = Regex.Match(
            html,
            @"formato\s+" + tipo + @"[^<]{0,300}<a\s[^>]*href\s*=\s*[""'](https?://[^""']+)[""']",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.NonBacktracking);
        return m.Success ? m.Groups[1].Value : null;
    }

    /// <summary>
    /// Busca el link "CONSULTAR" en TODAS las partes HTML del mensaje,
    /// incluyendo mensajes embebidos en correos reenviados (RV:/FW:).
    /// <c>mensaje.HtmlBody</c> solo devuelve el HTML del nivel superior;
    /// para correos reenviados el HTML de Bizlinks está dentro del MessagePart.
    /// </summary>
    private static string? ExtraerUrlConsultarDeMensaje(MimeMessage mensaje)
    {
        // Nivel superior: funciona para correos directos de Bizlinks.
        if (!string.IsNullOrWhiteSpace(mensaje.HtmlBody))
        {
            var url = ExtraerUrlConsultar(mensaje.HtmlBody);
            if (url is not null) return url;
        }

        // Traversal completo: cubre correos reenviados (RV:/FW:) donde el HTML
        // de Bizlinks está dentro de un MessagePart (message/rfc822).
        foreach (var parte in IterarPartes(mensaje.Body))
        {
            if (parte is not TextPart textPart) continue;
            if (!textPart.ContentType.IsMimeType("text", "html")) continue;
            if (string.IsNullOrWhiteSpace(textPart.Text)) continue;

            var url = ExtraerUrlConsultar(textPart.Text);
            if (url is not null) return url;
        }

        return null;
    }

    /// <summary>
    /// Extrae la URL del botón/link de consulta del cuerpo HTML del correo.
    /// Cubre: "CONSULTAR", "Consultar", "consultar" (BizLinks) y "Ver documento" (softpad.com.pe).
    /// Devuelve null si no encuentra ningún link. Decodifica entidades HTML (&amp; → &).
    /// </summary>
    private static string? ExtraerUrlConsultar(string? htmlBody)
    {
        if (string.IsNullOrWhiteSpace(htmlBody)) return null;

        // Patrón 1: <a href="...">...Consultar...</a>  o  <a href="...">...Ver documento...</a>
        var m = Regex.Match(
            htmlBody,
            @"<a\s[^>]*href\s*=\s*[""']([^""']+)[""'][^>]*>\s*(?:<[^>]+>)*\s*(?:Consultar|Ver\s+documento)\s*(?:</[^>]+>)*\s*</a>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.NonBacktracking);

        if (m.Success) return WebUtility.HtmlDecode(m.Groups[1].Value);

        // Patrón 2: href cuya URL contiene la palabra "consultar"
        // Cubre consultarDocumento.jsf, consultarComprobante, etc.
        var m2 = Regex.Match(
            htmlBody,
            @"href\s*=\s*[""'](https?://[^""']*consultar[^""']*)[""']",
            RegexOptions.IgnoreCase | RegexOptions.NonBacktracking);

        return m2.Success ? WebUtility.HtmlDecode(m2.Groups[1].Value) : null;
    }

    /// <summary>
    /// Recorre el árbol MIME y emite un Warning por cada <see cref="MessagePart"/>
    /// cuyo <c>Message</c> es null (MimeKit no pudo parsear el correo embebido).
    /// En ese caso <see cref="IterarPartes"/> omite silenciosamente esa rama.
    /// </summary>
    private void AdvertirMessagePartsVacios(MimeEntity? entidad, string cuentaNombre, string asunto)
    {
        switch (entidad)
        {
            case MessagePart mp:
                if (mp.Message is null)
                    _logger.LogWarning(
                        "Cuenta {Nombre}: '{Asunto}' contiene un message/rfc822 con cuerpo nulo — " +
                        "MimeKit no pudo parsear el correo embebido; sus adjuntos no serán procesados.",
                        cuentaNombre, asunto);
                else
                    AdvertirMessagePartsVacios(mp.Message.Body, cuentaNombre, asunto);
                break;
            case Multipart multi:
                foreach (var hijo in multi)
                    AdvertirMessagePartsVacios(hijo, cuentaNombre, asunto);
                break;
        }
    }

    /// <summary>
    /// Traversal recursivo del árbol MIME que descende dentro de correos
    /// reenviados (<c>message/rfc822</c> / <see cref="MessagePart"/>),
    /// invisibles para <c>BodyParts.OfType&lt;MimePart&gt;()</c>.
    /// </summary>
    private static IEnumerable<MimePart> IterarPartes(MimeEntity? entidad)
    {
        switch (entidad)
        {
            case null:
                break;

            case MimePart parte:
                yield return parte;
                break;

            case Multipart multipart:
                foreach (var hijo in multipart)
                foreach (var p in IterarPartes(hijo))
                    yield return p;
                break;

            case MessagePart msgParte:
                // Correo original embebido (RV:/FW:): descender en su cuerpo.
                if (msgParte.Message?.Body is not null)
                    foreach (var p in IterarPartes(msgParte.Message.Body))
                        yield return p;
                break;
        }
    }

    // ── Marcar mensajes como leídos post-persistencia ────────────────────────

    /// <inheritdoc/>
    public async Task MarcarProcesadosAsync(
        CuentaCorreoOptions cuenta, IReadOnlySet<string> grupoIds, CancellationToken ct)
    {
        if (grupoIds.Count == 0) return;

        for (int intento = 1; intento <= MaxIntentosImap; intento++)
        {
            try
            {
                await MarcarEnImapAsync(cuenta, grupoIds, ct);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "Cuenta {Nombre}: marcado de leídos cancelado por parada del servicio.", cuenta.Nombre);
                return;
            }
            catch (Exception ex) when (intento < MaxIntentosImap)
            {
                var espera = BackoffImap[intento - 1];
                _logger.LogWarning(ex,
                    "Cuenta '{Nombre}': fallo al marcar mensajes en intento {N}/{Max}. Reintentando en {Seg} s...",
                    cuenta.Nombre, intento, MaxIntentosImap, (int)espera.TotalSeconds);
                try   { await Task.Delay(espera, ct); }
                catch (OperationCanceledException) { return; }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Cuenta '{Nombre}': error al marcar mensajes como leídos tras {Max} intento(s). Los correos se reprocesarán en el próximo ciclo.",
                    cuenta.Nombre, MaxIntentosImap);
                return;
            }
        }
    }

    private async Task MarcarEnImapAsync(
        CuentaCorreoOptions cuenta, IReadOnlySet<string> grupoIds, CancellationToken ct)
    {
        using var client = await _conexion.ConectarAsync(cuenta, ct);

        var carpeta = await client.GetFolderAsync(cuenta.Carpeta, ct);
        await carpeta.OpenAsync(FolderAccess.ReadWrite, ct);

        var uids = grupoIds
            .Select(g => new UniqueId(uint.Parse(g)))
            .ToList();

        if (cuenta.MarcarLeido)
            await carpeta.AddFlagsAsync(uids, MessageFlags.Seen, true, ct);

        if (cuenta.MoverProcesado && !string.IsNullOrWhiteSpace(cuenta.CarpetaProcesados))
        {
            IMailFolder? carpetaProcesados = null;
            try
            {
                carpetaProcesados = await client.GetFolderAsync(cuenta.CarpetaProcesados, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Cuenta {Nombre}: no se pudo acceder a la carpeta '{Carpeta}'. Se omite el movimiento.",
                    cuenta.Nombre, cuenta.CarpetaProcesados);
            }

            if (carpetaProcesados is not null)
            {
                foreach (var uid in uids)
                {
                    try
                    {
                        await carpeta.MoveToAsync(uid, carpetaProcesados, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Cuenta {Nombre}: no se pudo mover el UID {Uid} a '{Carpeta}'.",
                            cuenta.Nombre, uid, cuenta.CarpetaProcesados);
                    }
                }
            }
        }

        _logger.LogInformation(
            "Cuenta {Nombre}: {Count} mensaje(s) marcado(s) como leído(s).",
            cuenta.Nombre, uids.Count);

        await client.DisconnectAsync(quit: true, CancellationToken.None);
    }
}
