using System.Reflection;
using FabricaHilos.Notificaciones.Abstractions;
using FabricaHilos.Notificaciones.Models;

namespace FabricaHilos.Notificaciones.Rendering;

/// <summary>
/// Carga templates HTML embebidos en el ensamblado y reemplaza
/// los {{placeholders}} con los datos del payload.
/// Los templates se almacenan como <c>EmbeddedResource</c> en la carpeta Templates/.
/// Convención de nombre: <c>FabricaHilos.Notificaciones.Templates.{TipoNotificacion}.html</c>
/// </summary>
public static class TemplateRenderer
{
    private static readonly Assembly _assembly = typeof(TemplateRenderer).Assembly;

    /// <summary>
    /// Carga el template correspondiente al tipo de notificación del payload,
    /// aplica los reemplazos y devuelve el HTML listo para enviar.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Si no existe un template embebido para el tipo de notificación indicado.
    /// </exception>
    public static string Renderizar(INotificacionPayload payload)
    {
        var nombreRecurso = ObtenerNombreRecurso(payload.Tipo);

        using var stream = _assembly.GetManifestResourceStream(nombreRecurso)
            ?? throw new InvalidOperationException(
                $"No se encontró el template embebido '{nombreRecurso}'. " +
                $"Verifique que el archivo Templates/{payload.Tipo}.html existe " +
                $"y está marcado como EmbeddedResource en el .csproj.");

        using var reader = new StreamReader(stream);
        var html = reader.ReadToEnd();

        return AplicarReemplazos(html, payload.ObtenerReemplazos());
    }

    // --- Helpers privados ---

    private static string ObtenerNombreRecurso(TipoNotificacion tipo) =>
        $"FabricaHilos.Notificaciones.Templates.{tipo}.html";

    private static string AplicarReemplazos(
        string html, Dictionary<string, string> reemplazos)
    {
        foreach (var (clave, valor) in reemplazos)
            html = html.Replace($"{{{{{clave}}}}}", valor, StringComparison.Ordinal);

        return html;
    }
}
