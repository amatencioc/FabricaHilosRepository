using System.Reflection;

namespace FabricaHilos.Notificaciones.Rendering;

/// <summary>
/// Motor de renderizado de templates HTML.
/// Carga el archivo .html embebido en el .dll y reemplaza
/// todos los {{placeholder}} con los valores del diccionario.
/// Los templates se cargan como EmbeddedResource — no dependen de rutas físicas.
/// </summary>
public static class TemplateRenderer
{
    private static readonly Assembly _assembly = Assembly.GetExecutingAssembly();

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

        foreach (var (clave, valor) in reemplazos)
            html = html.Replace($"{{{{{clave}}}}}", valor ?? string.Empty);

        return html;
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
