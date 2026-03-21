using System.Reflection;
using System.Text.RegularExpressions;

namespace FabricaHilos.Notificaciones.Rendering;

/// <summary>
/// Motor de renderizado de templates HTML.
/// Carga el archivo .html embebido en el .dll y reemplaza
/// todos los {{placeholder}} con los valores del diccionario.
/// Los templates se cargan como EmbeddedResource — no dependen de rutas físicas.
///
/// Además, resuelve {{Asset:nombre.ext}} incrustando el archivo binario
/// (imagen, fuente, etc.) como data URI base64, por lo que los emails
/// son completamente autónomos sin dependencias externas.
/// </summary>
public static class TemplateRenderer
{
    private static readonly Assembly _assembly = Assembly.GetExecutingAssembly();

    private static readonly Dictionary<string, string> _mimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".png",  "image/png" },
        { ".jpg",  "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".gif",  "image/gif" },
        { ".svg",  "image/svg+xml" },
        { ".ico",  "image/x-icon" },
        { ".woff", "font/woff" },
        { ".woff2","font/woff2" },
        { ".ttf",  "font/truetype" },
    };

    /// <summary>
    /// Carga el template HTML y aplica los reemplazos del payload.
    /// </summary>
    /// <param name="nombreTemplate">
    /// Nombre del archivo sin extensión. Ej: "DocumentoLimbo"
    /// Debe existir como EmbeddedResource en Templates/{nombre}.html
    /// </param>
    /// <param name="reemplazos">Diccionario clave→valor para sustituir {{placeholders}} en el HTML</param>
    public static string Renderizar(string nombreTemplate, Dictionary<string, string> reemplazos)
    {
        var html = CargarTemplate(nombreTemplate);

        html = ResolverAssets(html);

        foreach (var (clave, valor) in reemplazos)
            html = html.Replace($"{{{{{clave}}}}}", valor ?? string.Empty);

        return html;
    }

    /// <summary>
    /// Reemplaza todas las ocurrencias de {{Asset:nombre.ext}} por su data URI base64.
    /// El archivo debe existir como EmbeddedResource en Templates/Assets/.
    /// </summary>
    private static string ResolverAssets(string html)
    {
        return Regex.Replace(html, @"\{\{Asset:([^}]+)\}\}", match =>
        {
            var nombreArchivo = match.Groups[1].Value.Trim();
            var resourceName = $"FabricaHilos.Notificaciones.Templates.Assets.{nombreArchivo}";

            using var stream = _assembly.GetManifestResourceStream(resourceName);
            if (stream is null) return match.Value; // deja el placeholder si no se encuentra

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var base64 = Convert.ToBase64String(ms.ToArray());
            var ext = Path.GetExtension(nombreArchivo);
            var mime = _mimeTypes.GetValueOrDefault(ext, "application/octet-stream");

            return $"data:{mime};base64,{base64}";
        });
    }

    private static string CargarTemplate(string nombre)
    {
        var resourceName = $"FabricaHilos.Notificaciones.Templates.{nombre}.html";

        using var stream = _assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException(
                $"Template '{nombre}.html' no encontrado como EmbeddedResource. " +
                $"Verifica que esté en Templates/ y marcado como EmbeddedResource en el .csproj.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
