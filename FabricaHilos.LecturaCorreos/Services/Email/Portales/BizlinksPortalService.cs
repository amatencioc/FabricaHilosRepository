namespace FabricaHilos.LecturaCorreos.Services.Email.Portales;

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

    public BizlinksPortalService(ILogger<BizlinksPortalService> logger)
        => _logger = logger;

    public async Task<List<AdjuntoCorreo>> DescargarAdjuntosAsync(
        EnlacePortal enlace, CancellationToken ct)
    {
        var resultado = new List<AdjuntoCorreo>();

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

            if (urlXml is not null) AgregarSiNoNulo(resultado, await DescargarGetAsync(http, urlXml, "XML", enlace, ct));
            if (urlPdf is not null) AgregarSiNoNulo(resultado, await DescargarGetAsync(http, urlPdf, "PDF", enlace, ct));
            if (urlCdr is not null) AgregarSiNoNulo(resultado, await DescargarGetAsync(http, urlCdr, "CDR", enlace, ct));
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

        // Botones submit del formulario
        var botones = form
            .QuerySelectorAll("input[type='submit'], button[type='submit'], button:not([type])")
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
                return palabras.Any(k => texto.Contains(k, StringComparison.OrdinalIgnoreCase));
            });

            if (boton is null)
            {
                _logger.LogDebug("Portal JSF: boton para {Tipo} no encontrado.", tipo);
                continue;
            }

            var btnName  = boton.GetAttribute("name") ?? boton.Id ?? string.Empty;
            var btnValue = boton.GetAttribute("value") ?? boton.TextContent.Trim();

            // Construir POST body: campos ocultos + formulario ID + boton clickeado
            var data = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(formId)) data[formId] = formId;
            foreach (var kv in ocultos) data[kv.Key] = kv.Value;
            if (!string.IsNullOrEmpty(btnName)) data[btnName] = btnValue;

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

                AgregarSiNoNulo(resultado, ConstruirAdjunto(tipo, fileName, bytes, enlace));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Portal JSF: error al descargar {Tipo}.", tipo);
            }
        }

        return resultado;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string? ExtraerHref(IDocument document, Uri baseUri, string[] palabras)
    {
        foreach (var link in document.QuerySelectorAll("a[href]"))
        {
            var texto = link.TextContent.Trim();
            if (!palabras.Any(k => texto.Contains(k, StringComparison.OrdinalIgnoreCase))) continue;
            var href = link.GetAttribute("href") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(href)) continue;
            return href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? href
                : new Uri(baseUri, href).AbsoluteUri;
        }
        return null;
    }

    private async Task<AdjuntoCorreo?> DescargarGetAsync(
        HttpClient http, string url, string tipo, EnlacePortal enlace, CancellationToken ct)
    {
        try
        {
            var bytes = await http.GetByteArrayAsync(url, ct);
            if (bytes.Length == 0) return null;
            var nombre = ExtraerNombreDeUrl(url, tipo);
            _logger.LogInformation("Portal GET: {Tipo} '{Nombre}' descargado ({Size} KB).",
                tipo, nombre, bytes.Length / 1024);
            return ConstruirAdjunto(tipo, nombre, bytes, enlace);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Portal GET: error al descargar {Tipo} desde '{Url}'.", tipo, url);
            return null;
        }
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
                ContenidoPdf  = bytes,
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
            if (!string.IsNullOrEmpty(seg) && seg.Contains('.')) return seg;
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
