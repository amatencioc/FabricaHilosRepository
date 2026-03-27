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
/// Clase base abstracta con utilidades HTTP, parseo HTML y ZIP compartidas
/// por todos los servicios de descarga de portales.
/// Implementa el flujo común: cargar portal → extraer links → descargar → formulario fallback.
/// </summary>
public abstract class PortalDescargaBase : IPortalDescargaService
{
    protected static readonly string[] KeysXml = ["Descargar XML", "XML"];
    protected static readonly string[] KeysPdf = ["Descargar PDF", "PDF"];
    protected static readonly string[] KeysCdr = ["Descargar CDR", "CDR"];

    protected const long MaxEntradaBytes = 25 * 1024 * 1024; // 25 MB

    protected readonly ILogger _logger;

    protected PortalDescargaBase(ILogger logger) => _logger = logger;

    public abstract Task<List<AdjuntoCorreo>> DescargarAdjuntosAsync(
        EnlacePortal enlace, CancellationToken ct);

    // ── Flujo portal comun ────────────────────────────────────────────────────

    /// <summary>
    /// Flujo estándar de portal:
    ///   1. GET de la URL de consulta (con autenticación por URL).
    ///   2. Extraer links directos (a[href]).
    ///   3. Si no hay o faltan → formulario JSF/ASPX.
    /// </summary>
    protected async Task<List<AdjuntoCorreo>> DescargarDesdePortalAsync(
        EnlacePortal enlace, string nombrePortal, CancellationToken ct)
    {
        var resultado = new List<AdjuntoCorreo>();

        _logger.LogInformation("Portal {Portal}: accediendo a '{Url}'...",
            nombrePortal, enlace.UrlConsultar);

        using var http = CrearHttpClientConSesion();

        string html;
        Uri    baseUri;
        try
        {
            using var paginaResp = await http.GetAsync(
                enlace.UrlConsultar, HttpCompletionOption.ResponseContentRead, ct);
            paginaResp.EnsureSuccessStatusCode();
            html    = await paginaResp.Content.ReadAsStringAsync(ct);
            baseUri = paginaResp.RequestMessage?.RequestUri ?? new Uri(enlace.UrlConsultar);
            http.DefaultRequestHeaders.Referrer = baseUri;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Portal {Portal}: no se pudo acceder a '{Url}'. Se omite '{Asunto}'.",
                nombrePortal, enlace.UrlConsultar, enlace.Asunto);
            return resultado;
        }

        var document = await BrowsingContext.New(Configuration.Default)
                            .OpenAsync(req => req.Content(html), ct);

        var urlXml = ExtraerHref(document, baseUri, KeysXml);
        var urlPdf = ExtraerHref(document, baseUri, KeysPdf);
        var urlCdr = ExtraerHref(document, baseUri, KeysCdr);

        if (urlXml is not null || urlPdf is not null || urlCdr is not null)
        {
            _logger.LogDebug("Portal {Portal}: links directos. PDF={Pdf} XML={Xml} CDR={Cdr}",
                nombrePortal, urlPdf ?? "-", urlXml ?? "-", urlCdr ?? "-");

            if (urlXml is not null) resultado.AddRange(await DescargarGetAsync(http, urlXml, "XML", enlace, ct));
            if (urlPdf is not null) resultado.AddRange(await DescargarGetAsync(http, urlPdf, "PDF", enlace, ct));
            if (urlCdr is not null) resultado.AddRange(await DescargarGetAsync(http, urlCdr, "CDR", enlace, ct));

            // CDR: si no fue descargado exitosamente (link no encontrado o link directo devolvio
            // vista HTML en lugar del archivo), intentar via formulario JSF/ASPX de la pagina de listado.
            // Portales ASPX WebForms usan __doPostBack en el icono CDR — el href es solo el viewer
            // de fallback; el download real ocurre via POST al formulario con __EVENTTARGET del icono.
            if (!resultado.Any(a => a.TipoAdjunto == "CDR"))
            {
                _logger.LogDebug(
                    "Portal {Portal}: CDR no descargado via link directo; intentando via formulario...",
                    nombrePortal);
                // Restaurar Referer a la pagina de listado: OnCdrVistaDetectadaAsync puede haberlo
                // cambiado a la URL del viewer durante las estrategias de extraccion.
                http.DefaultRequestHeaders.Referrer = baseUri;
                var adjFormulario = await DescargarConJsfAsync(http, document, baseUri, enlace, ct);
                resultado.AddRange(adjFormulario.Where(a => a.TipoAdjunto == "CDR"));
            }

            return resultado;
        }

        _logger.LogDebug("Portal {Portal}: sin links directos, intentando formulario...", nombrePortal);
        var adjJsf = await DescargarConJsfAsync(http, document, baseUri, enlace, ct);
        resultado.AddRange(adjJsf);

        if (resultado.Count == 0)
            _logger.LogWarning(
                "Portal {Portal}: no se encontraron archivos en '{Url}'. " +
                "Si el portal requiere autenticacion considera usar Microsoft.Playwright.",
                nombrePortal, enlace.UrlConsultar);

        return resultado;
    }

    // ── Descarga GET ──────────────────────────────────────────────────────────

    protected async Task<List<AdjuntoCorreo>> DescargarGetAsync(
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
                // CDR viewer: la pagina HTML es el viewer del CDR, no el archivo en si.
                // Pasar a la subclase para que intente las estrategias de extraccion.
                if (tipo == "CDR" && !intentoSeguimiento)
                {
                    var htmlBytesViewer = await response.Content.ReadAsByteArrayAsync(ct);
                    if (htmlBytesViewer.Length > 0)
                    {
                        _logger.LogWarning(
                            "Portal GET: CDR devolvio HTML (text/html) desde '{Url}'. Intentando extraccion desde viewer.", url);
                        var efectivaUrl = response.RequestMessage?.RequestUri?.ToString() ?? url;
                        return await OnCdrVistaDetectadaAsync(http, htmlBytesViewer, efectivaUrl, enlace, ct);
                    }
                }
                _logger.LogWarning(
                    "Portal GET: {Tipo} devolvio HTML en lugar de archivo desde '{Url}'.", tipo, url);
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
                // La subclase decide cómo extraer el CDR desde el viewer (virtual).
                if (tipo == "CDR" && !intentoSeguimiento)
                {
                    var efectivaUrl = response.RequestMessage?.RequestUri?.ToString() ?? url;
                    return await OnCdrVistaDetectadaAsync(http, bytes, efectivaUrl, enlace, ct);
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

    /// <summary>
    /// Invocado cuando el GET al CDR devuelve una página HTML (CDR viewer).
    /// Implementación por defecto: devuelve lista vacía.
    /// Las subclases sobreescriben para implementar extracción del CDR desde el viewer.
    /// </summary>
    protected virtual Task<List<AdjuntoCorreo>> OnCdrVistaDetectadaAsync(
        HttpClient http, byte[] htmlBytes, string url, EnlacePortal enlace, CancellationToken ct)
        => Task.FromResult(new List<AdjuntoCorreo>());

    // ── Formulario JSF / ASPX WebForms ────────────────────────────────────────

    protected async Task<List<AdjuntoCorreo>> DescargarConJsfAsync(
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

        var formId    = form.GetAttribute("id") ?? string.Empty;
        var actionRaw = form.GetAttribute("action") ?? baseUri.PathAndQuery;
        var formAction = actionRaw.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? actionRaw
            : new Uri(baseUri, actionRaw).AbsoluteUri;

        var ocultos = form
            .QuerySelectorAll("input[type='hidden']")
            .Select(i => KeyValuePair.Create(
                i.GetAttribute("name") ?? string.Empty,
                i.GetAttribute("value") ?? string.Empty))
            .Where(kv => !string.IsNullOrEmpty(kv.Key))
            .ToList();

        _logger.LogDebug("Portal JSF: action={Action} | formId={Id} | campos ocultos={N}",
            formAction, formId, ocultos.Count);

        var botones = form
            .QuerySelectorAll("input[type='submit'], input[type='image'], button[type='submit'], button:not([type]), a[onclick*='doPostBack'], a[href*='__doPostBack'], a[onclick*='submitForm']")
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
                    // input[type='image']: src/alt/title estan en el propio elemento, no tiene hijos <img>.
                    var bSrc = b.GetAttribute("src") ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(bSrc))
                        texto = b.GetAttribute("alt") ?? b.GetAttribute("title")
                             ?? Path.GetFileNameWithoutExtension(bSrc);
                }
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
            var btnHref  = boton.GetAttribute("href") ?? string.Empty;

            var data = new Dictionary<string, string>();
            foreach (var kv in ocultos) data[kv.Key] = kv.Value;

            // ASPX WebForms: __doPostBack puede estar en onclick (Button con JS inline)
            // o en href (asp:LinkButton renderizado como <a href="javascript:__doPostBack(...)">).
            var doPostBackSrc = onclick.Contains("__doPostBack", StringComparison.OrdinalIgnoreCase)
                ? onclick
                : btnHref.Contains("__doPostBack", StringComparison.OrdinalIgnoreCase) ? btnHref : string.Empty;

            if (!string.IsNullOrEmpty(doPostBackSrc))
            {
                var dpb = Regex.Match(doPostBackSrc, @"__doPostBack\('([^']*)'[,\s]*'([^']*)'\)");
                if (dpb.Success)
                {
                    data["__EVENTTARGET"]   = dpb.Groups[1].Value;
                    data["__EVENTARGUMENT"] = dpb.Groups[2].Value;
                }
            }
            else if (boton.TagName.Equals("INPUT", StringComparison.OrdinalIgnoreCase)
                  && (boton.GetAttribute("type") ?? string.Empty).Equals("image", StringComparison.OrdinalIgnoreCase))
            {
                // asp:ImageButton: envia {name}.x y {name}.y como coordenadas del click en la imagen.
                if (!string.IsNullOrEmpty(btnName))
                {
                    data[$"{btnName}.x"] = "0";
                    data[$"{btnName}.y"] = "0";
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(btnName)) data[btnName] = btnValue;
                // myfaces.oam.submitForm agrega parametros extra via JS antes de hacer submit.
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

    // ── HttpClient factory ────────────────────────────────────────────────────

    /// <summary>
    /// Crea un HttpClient con CookieContainer propio para aislar cookies entre sesiones.
    /// El caller debe disponer el cliente (using).
    /// </summary>
    protected static HttpClient CrearHttpClientConSesion()
    {
        var handler = new HttpClientHandler
        {
            UseCookies        = true,
            CookieContainer   = new CookieContainer(),
            AllowAutoRedirect = true,
        };
        var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        http.DefaultRequestHeaders.Accept.ParseAdd(
            "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("es-PE,es;q=0.9,en;q=0.8");
        return http;
    }

    // ── Helpers estáticos ─────────────────────────────────────────────────────

    protected static bool EsZip(byte[] bytes)
        => bytes.Length >= 4
           && bytes[0] == 0x50 && bytes[1] == 0x4B
           && bytes[2] == 0x03 && bytes[3] == 0x04;

    protected static bool EsHtml(byte[] bytes)
    {
        if (bytes.Length < 5) return false;
        var cabecera = Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 200)).TrimStart();
        return cabecera.StartsWith("<html",     StringComparison.OrdinalIgnoreCase)
            || cabecera.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase);
    }

    protected static List<AdjuntoCorreo> ExtraerDeZip(byte[] zipBytes, EnlacePortal enlace)
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

    protected static Dictionary<string, string> ExtraerParametrosOam(string onclick)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(onclick)) return result;
        var matches = Regex.Matches(onclick, @"\['([^']+)','([^']*)'\]");
        foreach (Match m in matches)
            result[m.Groups[1].Value] = m.Groups[2].Value;
        return result;
    }

    protected static string? ExtraerHref(IDocument document, Uri baseUri, string[] palabras)
    {
        // Primera pasada: texto visible, title del link y atributos de la imagen hija.
        foreach (var link in document.QuerySelectorAll("a[href]"))
        {
            var texto = link.TextContent.Trim();

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

    protected static AdjuntoCorreo? ConstruirAdjunto(
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

    protected static string ExtraerNombreDeUrl(string url, string tipo)
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

    protected static string NombrePorTipo(string tipo)
    {
        var ext = tipo switch { "PDF" => ".pdf", "CDR" => ".zip", _ => ".xml" };
        return $"portal_{tipo.ToLower()}_{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
    }

    protected static void AgregarSiNoNulo(List<AdjuntoCorreo> lista, AdjuntoCorreo? adj)
    {
        if (adj is not null) lista.Add(adj);
    }
}
