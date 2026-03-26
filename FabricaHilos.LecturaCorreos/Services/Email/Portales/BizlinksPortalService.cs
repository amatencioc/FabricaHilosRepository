namespace FabricaHilos.LecturaCorreos.Services.Email.Portales;

using System.IO.Compression;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using FabricaHilos.LecturaCorreos.Models;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;

/// <summary>
/// Descarga XML, PDF y CDR desde el portal web (Bizlinks u otro proveedor JSF).
/// Estrategia:
///   1. GET de la pagina -> intentar links directos (a[href]).
///   2. Si no hay links directos -> envio de formulario JSF con cookies de sesion.
/// Crea su propio HttpClient por sesion para aislar cookies entre operaciones.
/// </summary>
public class BizlinksPortalService : IPortalDescargaService
{
    private readonly ILogger<BizlinksPortalService> _logger;

    private static readonly string[] KeysXml = ["Descargar XML", "XML"];
    private static readonly string[] KeysPdf = ["Descargar PDF", "PDF"];
    private static readonly string[] KeysCdr = ["Descargar CDR", "CDR"];

    // Limite por entrada descomprimida: previene ZIP bomb y OutOfMemoryException.
    private const long MaxEntradaBytes = 25 * 1024 * 1024; // 25 MB

    public BizlinksPortalService(ILogger<BizlinksPortalService> logger)
        => _logger = logger;

    public async Task<List<AdjuntoCorreo>> DescargarAdjuntosAsync(
        EnlacePortal enlace, CancellationToken ct)
    {
        var resultado = new List<AdjuntoCorreo>();

        // ── Links directos en el correo (efacturacion.pe y similares) ────────
        // Si el correo ya trajo las URLs de descarga, no se necesita portal ni sesion JSF.
        if (enlace.TieneLinksDirectos)
        {
            _logger.LogInformation(
                "Portal: links directos detectados en el correo. XML={Xml} PDF={Pdf}",
                enlace.UrlXmlDirecto ?? "-", enlace.UrlPdfDirecto ?? "-");

            using var httpDirecto = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            httpDirecto.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            if (enlace.UrlXmlDirecto is not null)
                resultado.AddRange(await DescargarGetAsync(httpDirecto, enlace.UrlXmlDirecto, "XML", enlace, ct));
            if (enlace.UrlPdfDirecto is not null)
                resultado.AddRange(await DescargarGetAsync(httpDirecto, enlace.UrlPdfDirecto, "PDF", enlace, ct));

            return resultado;
        }

        _logger.LogInformation("Portal: accediendo a '{Url}'...", enlace.UrlConsultar);

        // Un handler propio por sesion: cada descarga tiene su propio CookieContainer.
        using var handler = new HttpClientHandler
        {
            UseCookies        = true,
            CookieContainer   = new CookieContainer(),
            AllowAutoRedirect = true,
        };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        http.DefaultRequestHeaders.Accept.ParseAdd(
            "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("es-PE,es;q=0.9,en;q=0.8");

        string html;
        try
        {
            html = await http.GetStringAsync(enlace.UrlConsultar, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Portal: no se pudo acceder a '{Url}'. Se omite '{Asunto}'.",
                enlace.UrlConsultar, enlace.Asunto);
            return resultado;
        }

        var baseUri  = new Uri(enlace.UrlConsultar);
        var config   = Configuration.Default;
        var context  = BrowsingContext.New(config);
        var document = await context.OpenAsync(req => req.Content(html), ct);

        // --- Intento 1: links directos <a href> ---------------------------------
        var urlXml = ExtraerHref(document, baseUri, KeysXml);
        var urlPdf = ExtraerHref(document, baseUri, KeysPdf);
        var urlCdr = ExtraerHref(document, baseUri, KeysCdr);

        if (urlXml is not null || urlPdf is not null || urlCdr is not null)
        {
            _logger.LogDebug("Portal: links directos encontrados. PDF={Pdf} XML={Xml} CDR={Cdr}",
                urlPdf ?? "-", urlXml ?? "-", urlCdr ?? "-");

            if (urlXml is not null) resultado.AddRange(await DescargarGetAsync(http, urlXml, "XML", enlace, ct));
            if (urlPdf is not null) resultado.AddRange(await DescargarGetAsync(http, urlPdf, "PDF", enlace, ct));
            if (urlCdr is not null) resultado.AddRange(await DescargarGetAsync(http, urlCdr, "CDR", enlace, ct));

            // CDR no encontrado como link directo: el portal puede requerir formulario/postback.
            // Se intenta via formulario y solo se toman los adjuntos tipo CDR para evitar duplicar XML/PDF.
            if (urlCdr is null)
            {
                _logger.LogDebug("Portal: CDR sin link directo; intentando via formulario...");
                var adjFormulario = await DescargarConJsfAsync(http, document, baseUri, enlace, ct);
                resultado.AddRange(adjFormulario.Where(a => a.TipoAdjunto == "CDR"));
            }

            return resultado;
        }

        // --- Intento 2: formulario JSF ------------------------------------------
        _logger.LogDebug("Portal: sin links directos, intentando envio de formulario JSF...");
        var adjJsf = await DescargarConJsfAsync(http, document, baseUri, enlace, ct);
        resultado.AddRange(adjJsf);

        if (resultado.Count == 0)
            _logger.LogWarning(
                "Portal: no se encontraron archivos en '{Url}'. " +
                "Si el portal requiere autenticacion considera usar Microsoft.Playwright.",
                enlace.UrlConsultar);

        return resultado;
    }

    // ── Descarga mediante envio de formulario JSF ────────────────────────────

    private async Task<List<AdjuntoCorreo>> DescargarConJsfAsync(
        HttpClient http, IDocument document, Uri baseUri,
        EnlacePortal enlace, CancellationToken ct)
    {
        var resultado = new List<AdjuntoCorreo>();

        var form = document.QuerySelector("form");
        if (form is null)
        {
            _logger.LogWarning("Portal JSF: no se encontro <form> en la pagina.");
            return resultado;
        }

        var formId     = form.GetAttribute("id") ?? string.Empty;
        var actionRaw  = form.GetAttribute("action") ?? baseUri.PathAndQuery;
        var formAction = actionRaw.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? actionRaw
            : new Uri(baseUri, actionRaw).AbsoluteUri;

        // Campos ocultos del formulario (incluye javax.faces.ViewState)
        var ocultos = form
            .QuerySelectorAll("input[type='hidden']")
            .Select(i => KeyValuePair.Create(
                i.GetAttribute("name") ?? string.Empty,
                i.GetAttribute("value") ?? string.Empty))
            .Where(kv => !string.IsNullOrEmpty(kv.Key))
            .ToList();

        _logger.LogDebug("Portal JSF: action={Action} | formId={Id} | campos ocultos={N}",
            formAction, formId, ocultos.Count);

        // Botones y links de acción del formulario (JSF submit buttons + ASPX __doPostBack links).
        var botones = form
            .QuerySelectorAll("input[type='submit'], button[type='submit'], button:not([type]), a[onclick*='doPostBack'], a[onclick*='submitForm']")
            .ToList();

        foreach (var (palabras, tipo) in new[]
        {
            (KeysXml, "XML"),
            (KeysPdf, "PDF"),
            (KeysCdr, "CDR"),
        })
        {
            if (ct.IsCancellationRequested) break;

            var boton = botones.FirstOrDefault(b =>
            {
                var texto = b.GetAttribute("value") ?? b.TextContent ?? string.Empty;
                if (string.IsNullOrWhiteSpace(texto))
                {
                    var img = b.QuerySelector("img");
                    if (img is not null)
                        texto = img.GetAttribute("alt")
                             ?? img.GetAttribute("title")
                             ?? Path.GetFileNameWithoutExtension(img.GetAttribute("src") ?? string.Empty);
                }
                return palabras.Any(k => texto.Contains(k, StringComparison.OrdinalIgnoreCase));
            });

            if (boton is null)
            {
                _logger.LogDebug("Portal JSF: boton para {Tipo} no encontrado.", tipo);
                continue;
            }

            var btnName  = boton.GetAttribute("name") ?? boton.Id ?? string.Empty;
            var btnValue = boton.GetAttribute("value") ?? boton.TextContent.Trim();
            var onclick  = boton.GetAttribute("onclick") ?? string.Empty;

            // Construir POST body: campos ocultos + boton clickeado + params extra
            var data = new Dictionary<string, string>();
            foreach (var kv in ocultos) data[kv.Key] = kv.Value;

            // ASPX WebForms: __doPostBack transmite el evento via __EVENTTARGET/__EVENTARGUMENT.
            if (onclick.Contains("__doPostBack", StringComparison.OrdinalIgnoreCase))
            {
                var dpb = Regex.Match(onclick, @"__doPostBack\('([^']*)'[,\s]*'([^']*)'\)");
                if (dpb.Success)
                {
                    data["__EVENTTARGET"]   = dpb.Groups[1].Value;
                    data["__EVENTARGUMENT"] = dpb.Groups[2].Value;
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(btnName)) data[btnName] = btnValue;
                // myfaces.oam.submitForm agrega parametros extra via JS antes de hacer submit.
                // Ej: [['parametros','20536252666-09-T001-00004178']] -> parametros=20536252666-09-T001-00004178
                foreach (var kv in ExtraerParametrosOam(onclick))
                    data[kv.Key] = kv.Value;
            }

            try
            {
                var content  = new FormUrlEncodedContent(data);
                var response = await http.PostAsync(formAction, content, ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Portal JSF: POST {Tipo} => HTTP {Status}.",
                        tipo, (int)response.StatusCode);
                    continue;
                }

                var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
                if (contentType.Contains("html", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning(
                        "Portal JSF: la descarga de {Tipo} devolvio HTML (requiere sesion autenticada).", tipo);
                    continue;
                }

                var bytes = await response.Content.ReadAsByteArrayAsync(ct);
                if (bytes.Length == 0) continue;

                var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                    ?? response.Content.Headers.ContentDisposition?.FileName
                    ?? NombrePorTipo(tipo);

                _logger.LogInformation("Portal JSF: {Tipo} '{Nombre}' descargado ({Size} KB).",
                    tipo, fileName, bytes.Length / 1024);

                // El portal devuelve ZIPs tanto para XML como para CDR.
                // Se extrae el contenido igual que LectorAdjuntoZip con adjuntos de correo.
                if (EsZip(bytes))
                {
                    var deZip = ExtraerDeZip(bytes, enlace);
                    _logger.LogDebug("Portal JSF: ZIP '{Nombre}' — {N} adjunto(s) extraídos.", fileName, deZip.Count);
                    resultado.AddRange(deZip);
                }
                else
                {
                    AgregarSiNoNulo(resultado, ConstruirAdjunto(tipo, fileName, bytes, enlace));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Portal JSF: error al descargar {Tipo}.", tipo);
            }
        }

        return resultado;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Detecta ZIP por magic bytes (PK\x03\x04).
    private static bool EsZip(byte[] bytes)
        => bytes.Length >= 4
           && bytes[0] == 0x50 && bytes[1] == 0x4B
           && bytes[2] == 0x03 && bytes[3] == 0x04;

    // Detecta si los bytes descargados son una pagina HTML en lugar del archivo esperado.
    // Cubre portales que devuelven HTML con Content-Type incorrecto (text/xml, octet-stream, etc.).
    private static bool EsHtml(byte[] bytes)
    {
        if (bytes.Length < 5) return false;
        var cabecera = Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 200)).TrimStart();
        return cabecera.StartsWith("<html",      StringComparison.OrdinalIgnoreCase)
            || cabecera.StartsWith("<!DOCTYPE",  StringComparison.OrdinalIgnoreCase);
    }

    // Extrae entradas XML y PDF de un ZIP devuelto por el portal.
    // Replica la logica de LectorAdjuntoZip para adjuntos de correo.
    private static List<AdjuntoCorreo> ExtraerDeZip(byte[] zipBytes, EnlacePortal enlace)
    {
        var lista = new List<AdjuntoCorreo>();
        using var ms  = new MemoryStream(zipBytes);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        foreach (var entry in zip.Entries)
        {
            if (entry.Length > MaxEntradaBytes) continue;

            if (entry.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                using var entryMs = new MemoryStream();
                using (var s = entry.Open()) s.CopyTo(entryMs);
                lista.Add(new AdjuntoCorreo
                {
                    TipoAdjunto   = "XML",
                    NombreArchivo = entry.Name,
                    ContenidoXml  = Encoding.UTF8.GetString(entryMs.ToArray()),
                    GrupoCorreo   = enlace.GrupoCorreo,
                    Asunto        = enlace.Asunto,
                    Remitente     = enlace.Remitente,
                    FechaCorreo   = enlace.FechaCorreo,
                });
            }
            else if (entry.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                using var entryMs = new MemoryStream();
                using (var s = entry.Open()) s.CopyTo(entryMs);
                lista.Add(new AdjuntoCorreo
                {
                    TipoAdjunto   = "PDF",
                    NombreArchivo = entry.Name,
                    ContenidoPdf  = entryMs.ToArray(),
                    GrupoCorreo   = enlace.GrupoCorreo,
                    Asunto        = enlace.Asunto,
                    Remitente     = enlace.Remitente,
                    FechaCorreo   = enlace.FechaCorreo,
                });
            }
        }
        return lista;
    }

    // Extrae los pares [name, value] del array de params extra de myfaces.oam.submitForm.
    // Patron del onclick: myfaces.oam.submitForm('form','btn',null,[['parametros','valor']])
    private static Dictionary<string, string> ExtraerParametrosOam(string onclick)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(onclick)) return result;
        var matches = Regex.Matches(onclick, @"\['([^']+)','([^']*)'\]");
        foreach (Match m in matches)
            result[m.Groups[1].Value] = m.Groups[2].Value;
        return result;
    }

    private static string? ExtraerHref(IDocument document, Uri baseUri, string[] palabras)
    {
        // Primera pasada: texto visible, title del link y atributos de la imagen hija.
        foreach (var link in document.QuerySelectorAll("a[href]"))
        {
            var texto = link.TextContent.Trim();

            // Fallback para links de solo imagen: title/aria-label del link, luego atributos del <img>.
            if (string.IsNullOrWhiteSpace(texto))
            {
                texto = link.GetAttribute("title") ?? link.GetAttribute("aria-label") ?? string.Empty;
                if (string.IsNullOrWhiteSpace(texto))
                {
                    var img = link.QuerySelector("img");
                    if (img is not null)
                        texto = img.GetAttribute("tooltip")
                             ?? img.GetAttribute("title")
                             ?? img.GetAttribute("alt")
                             ?? Path.GetFileNameWithoutExtension(img.GetAttribute("src") ?? string.Empty);
                }
            }

            if (!palabras.Any(k => texto.Contains(k, StringComparison.OrdinalIgnoreCase))) continue;
            var href = link.GetAttribute("href") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(href)
                || href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)) continue;
            return href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? href
                : new Uri(baseUri, href).AbsoluteUri;
        }

        // Segunda pasada: buscar en la URL misma (portales que codifican el tipo en el href).
        foreach (var link in document.QuerySelectorAll("a[href]"))
        {
            var href = link.GetAttribute("href") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(href)
                || href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)) continue;
            if (!palabras.Any(k => href.Contains(k, StringComparison.OrdinalIgnoreCase))) continue;
            return href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? href
                : new Uri(baseUri, href).AbsoluteUri;
        }

        return null;
    }

    private async Task<List<AdjuntoCorreo>> DescargarGetAsync(
        HttpClient http, string url, string tipo, EnlacePortal enlace, CancellationToken ct,
        bool intentoSeguimiento = false)
    {
        var lista = new List<AdjuntoCorreo>();
        try
        {
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseContentRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Portal GET: {Tipo} respondio {Status} desde '{Url}'.",
                    tipo, (int)response.StatusCode, url);
                return lista;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (contentType.Contains("html", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Portal GET: {Tipo} devolvio HTML en lugar de archivo desde '{Url}'.", tipo, url);
                return lista;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            if (bytes.Length == 0) return lista;

            // Detectar HTML aunque el Content-Type no lo indique (portales con configuracion incorrecta).
            if (EsHtml(bytes))
            {
                _logger.LogWarning(
                    "Portal GET: {Tipo} devolvio contenido HTML (Content-Type: {ContentType}) desde '{Url}'.",
                    tipo, contentType, url);

                // Para CDR: la URL puede ser una pagina de vista, no de descarga directa.
                // Intentar extraer el CDR real desde esa pagina HTML mediante multiples estrategias.
                // Se usa la URL efectiva (tras redirecciones) como base para resolver URLs relativas.
                if (tipo == "CDR" && !intentoSeguimiento)
                {
                    var efectivaUrl = response.RequestMessage?.RequestUri?.ToString() ?? url;
                    return await SeguirPaginaVistaCdrAsync(http, bytes, efectivaUrl, enlace, ct);
                }

                return lista;
            }

            var nombre = response.Content.Headers.ContentDisposition?.FileNameStar
                ?? response.Content.Headers.ContentDisposition?.FileName
                ?? ExtraerNombreDeUrl(url, tipo);

            _logger.LogInformation("Portal GET: {Tipo} '{Nombre}' descargado ({Size} KB).",
                tipo, nombre, bytes.Length / 1024);

            if (EsZip(bytes))
                lista.AddRange(ExtraerDeZip(bytes, enlace));
            else
                AgregarSiNoNulo(lista, ConstruirAdjunto(tipo, nombre, bytes, enlace));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Portal GET: error al descargar {Tipo} desde '{Url}'.", tipo, url);
        }
        return lista;
    }

    // Estrategias en cascada para extraer el CDR real desde una pagina vista (no es descarga directa).
    // Aplica cuando el portal devuelve HTML en lugar del archivo al hacer GET a la URL del CDR.
    private async Task<List<AdjuntoCorreo>> SeguirPaginaVistaCdrAsync(
        HttpClient http, byte[] htmlBytes, string url, EnlacePortal enlace, CancellationToken ct)
    {
        var lista       = new List<AdjuntoCorreo>();
        var htmlContent = Encoding.UTF8.GetString(htmlBytes);
        var baseUri     = new Uri(url);
        var innerDoc    = await BrowsingContext.New(Configuration.Default)
                               .OpenAsync(req => req.Content(htmlContent), ct);

        _logger.LogDebug("Portal CDR vista: primeros 800 chars del HTML: {Html}",
            htmlContent.Length > 800 ? htmlContent[..800] : htmlContent);

        // Estrategia 0: reintentar la URL original eliminando text/html del Accept y agregando Referer.
        // Algunos portales ASPX hacen content-negotiation: si el cliente acepta text/html sirven la
        // vista HTML; si el Accept es application/xml o */* sin text/html, sirven el archivo.
        try
        {
            http.DefaultRequestHeaders.Accept.Clear();
            http.DefaultRequestHeaders.Accept.ParseAdd(
                "application/xml,application/zip,application/octet-stream,*/*;q=0.1");
            if (!string.IsNullOrEmpty(enlace.UrlConsultar))
                http.DefaultRequestHeaders.Referrer = new Uri(enlace.UrlConsultar);

            using var resp0 = await http.GetAsync(url, HttpCompletionOption.ResponseContentRead, ct);
            if (resp0.IsSuccessStatusCode)
            {
                var b0 = await resp0.Content.ReadAsByteArrayAsync(ct);
                if (b0.Length > 0 && !EsHtml(b0))
                {
                    var n0 = resp0.Content.Headers.ContentDisposition?.FileNameStar
                        ?? resp0.Content.Headers.ContentDisposition?.FileName
                        ?? ExtraerNombreDeUrl(url, "CDR");
                    _logger.LogInformation(
                        "Portal CDR vista: CDR '{Nombre}' descargado ({Size} KB) via Accept=*/*.",
                        n0, b0.Length / 1024);
                    if (EsZip(b0)) lista.AddRange(ExtraerDeZip(b0, enlace));
                    else AgregarSiNoNulo(lista, ConstruirAdjunto("CDR", n0, b0, enlace));
                    if (lista.Count > 0) return lista;
                }
            }
        }
        catch (Exception ex) { _logger.LogDebug(ex, "Portal CDR vista: estrategia 0 fallida."); }
        finally
        {
            http.DefaultRequestHeaders.Accept.Clear();
            http.DefaultRequestHeaders.Accept.ParseAdd(
                "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            http.DefaultRequestHeaders.Referrer = null;
        }

        // Estrategia 1: iframe o embed con src directo al archivo CDR/XML.
        foreach (var selector in new[] { "iframe[src]", "embed[src]" })
        {
            foreach (var elem in innerDoc.QuerySelectorAll(selector))
            {
                var src = elem.GetAttribute("src") ?? string.Empty;
                if (string.IsNullOrWhiteSpace(src)
                    || src.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)) continue;
                var srcUrl = src.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? src : new Uri(baseUri, src).AbsoluteUri;
                _logger.LogDebug("Portal CDR vista: iframe/embed src='{Url}'.", srcUrl);
                var res = await DescargarGetAsync(http, srcUrl, "CDR", enlace, ct, intentoSeguimiento: true);
                if (res.Count > 0) return res;
            }
        }

        // Estrategia 2: buscar links con palabras clave amplias.
        string[] keywordsAmplio = ["Descargar CDR", "CDR", "Constancia", "Descargar", "Download"];
        var urlDescarga = ExtraerHref(innerDoc, baseUri, keywordsAmplio);
        if (urlDescarga is not null && !urlDescarga.Equals(url, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Portal CDR vista: link de descarga encontrado: '{Url}'.", urlDescarga);
            var res = await DescargarGetAsync(http, urlDescarga, "CDR", enlace, ct, intentoSeguimiento: true);
            if (res.Count > 0) return res;
        }

        // Estrategia 3: enviar formulario de la pagina vista con boton de descarga.
        var form = innerDoc.QuerySelector("form");
        if (form is not null)
        {
            var actionRaw  = form.GetAttribute("action") ?? baseUri.PathAndQuery;
            var formAction = actionRaw.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? actionRaw : new Uri(baseUri, actionRaw).AbsoluteUri;

            var ocultos = form
                .QuerySelectorAll("input[type='hidden']")
                .Select(i => KeyValuePair.Create(
                    i.GetAttribute("name") ?? string.Empty,
                    i.GetAttribute("value") ?? string.Empty))
                .Where(kv => !string.IsNullOrEmpty(kv.Key))
                .ToList();

            string[] keywordsBoton = ["Descargar CDR", "CDR", "Constancia", "Descargar", "Download", "XML"];
            var botones = form.QuerySelectorAll(
                "input[type='submit'], button[type='submit'], button:not([type]), a[onclick*='doPostBack'], a[onclick*='submitForm']");

            foreach (var boton in botones)
            {
                var texto = boton.GetAttribute("value") ?? boton.TextContent.Trim();
                if (string.IsNullOrWhiteSpace(texto))
                {
                    var img = boton.QuerySelector("img");
                    if (img is not null)
                        texto = img.GetAttribute("alt") ?? img.GetAttribute("title")
                             ?? Path.GetFileNameWithoutExtension(img.GetAttribute("src") ?? string.Empty);
                }
                if (!keywordsBoton.Any(k => texto.Contains(k, StringComparison.OrdinalIgnoreCase))) continue;

                var data    = new Dictionary<string, string>();
                foreach (var kv in ocultos) data[kv.Key] = kv.Value;

                var onclick = boton.GetAttribute("onclick") ?? string.Empty;
                if (onclick.Contains("__doPostBack", StringComparison.OrdinalIgnoreCase))
                {
                    var dpb = Regex.Match(onclick, @"__doPostBack\('([^']*)'[,\s]*'([^']*)'\)");
                    if (dpb.Success)
                    {
                        data["__EVENTTARGET"]   = dpb.Groups[1].Value;
                        data["__EVENTARGUMENT"] = dpb.Groups[2].Value;
                    }
                }
                else
                {
                    var btnName  = boton.GetAttribute("name") ?? boton.Id ?? string.Empty;
                    var btnValue = boton.GetAttribute("value") ?? boton.TextContent.Trim();
                    if (!string.IsNullOrEmpty(btnName)) data[btnName] = btnValue;
                    foreach (var kv in ExtraerParametrosOam(onclick)) data[kv.Key] = kv.Value;
                }

                try
                {
                    _logger.LogDebug("Portal CDR vista: POST formulario boton '{Texto}' a '{Action}'.",
                        texto.Trim(), formAction);
                    using var postResponse = await http.PostAsync(
                        formAction, new FormUrlEncodedContent(data), ct);
                    if (!postResponse.IsSuccessStatusCode) continue;

                    var postBytes = await postResponse.Content.ReadAsByteArrayAsync(ct);
                    if (postBytes.Length == 0 || EsHtml(postBytes)) continue;

                    var nombre = postResponse.Content.Headers.ContentDisposition?.FileNameStar
                        ?? postResponse.Content.Headers.ContentDisposition?.FileName
                        ?? NombrePorTipo("CDR");

                    _logger.LogInformation(
                        "Portal CDR vista: CDR '{Nombre}' descargado via formulario ({Size} KB).",
                        nombre, postBytes.Length / 1024);

                    if (EsZip(postBytes))
                        lista.AddRange(ExtraerDeZip(postBytes, enlace));
                    else
                        AgregarSiNoNulo(lista, ConstruirAdjunto("CDR", nombre, postBytes, enlace));

                    if (lista.Count > 0) return lista;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Portal CDR vista: error al enviar formulario.");
                }
            }
        }

        _logger.LogWarning(
            "Portal CDR vista: no se pudo extraer CDR desde la pagina vista '{Url}'.", url);
        return lista;
    }

    private static AdjuntoCorreo? ConstruirAdjunto(
        string tipo, string nombre, byte[] bytes, EnlacePortal enlace)
        => tipo switch
        {
            "XML" => new AdjuntoCorreo
            {
                TipoAdjunto   = "XML",
                NombreArchivo = nombre,
                ContenidoXml  = Encoding.UTF8.GetString(bytes),
                GrupoCorreo   = enlace.GrupoCorreo,
                Asunto        = enlace.Asunto,
                Remitente     = enlace.Remitente,
                FechaCorreo   = enlace.FechaCorreo,
            },
            "PDF" => new AdjuntoCorreo
            {
                TipoAdjunto   = "PDF",
                NombreArchivo = nombre,
                ContenidoPdf  = bytes,
                GrupoCorreo   = enlace.GrupoCorreo,
                Asunto        = enlace.Asunto,
                Remitente     = enlace.Remitente,
                FechaCorreo   = enlace.FechaCorreo,
            },
            "CDR" => new AdjuntoCorreo
            {
                TipoAdjunto   = "CDR",
                NombreArchivo = nombre,
                ContenidoXml  = Encoding.UTF8.GetString(bytes), // CDR es siempre ApplicationResponse XML
                GrupoCorreo   = enlace.GrupoCorreo,
                Asunto        = enlace.Asunto,
                Remitente     = enlace.Remitente,
                FechaCorreo   = enlace.FechaCorreo,
            },
            _ => null,
        };

    private static string ExtraerNombreDeUrl(string url, string tipo)
    {
        try
        {
            var seg = Uri.UnescapeDataString(new Uri(url).Segments.LastOrDefault() ?? "").Trim('/');
            var ext = Path.GetExtension(seg).ToLowerInvariant();
            if (!string.IsNullOrEmpty(ext) && ext is ".xml" or ".pdf" or ".zip") return seg;
        }
        catch { }
        return NombrePorTipo(tipo);
    }

    private static string NombrePorTipo(string tipo)
    {
        var ext = tipo switch { "PDF" => ".pdf", "CDR" => ".zip", _ => ".xml" };
        return $"portal_{tipo.ToLower()}_{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
    }

    private static void AgregarSiNoNulo(List<AdjuntoCorreo> lista, AdjuntoCorreo? adj)
    {
        if (adj is not null) lista.Add(adj);
    }
}
