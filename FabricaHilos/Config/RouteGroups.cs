using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace FabricaHilos.Config;

/// <summary>
/// Expansión dinámica de grupos de rutas para acceso externo.
///
/// Descubre automáticamente qué controladores forman parte del mismo módulo lógico
/// usando reflexión sobre los atributos [Route] del ensamblado.
///
/// PATRÓN SOPORTADO (sin configuración adicional):
///   Controllers/{Modulo}/ModuloController        ← controlador canónico
///   Controllers/{Modulo}/SubControladorController ← controladores de soporte
///
/// Cuando appsettings contiene la ruta del controlador canónico
/// (ej. "/recursoshumanos/capacitacion"), automáticamente se permiten
/// también todos los sub-controladores del mismo namespace/carpeta.
///
/// REGLA CLAVE:
///   El controlador canónico es aquel cuyo nombre coincide exactamente con
///   el último segmento del sub-namespace + "Controller"
///   (ej. namespace "Capacitacion" → "CapacitacionController").
///   Si no existe ese controlador en el grupo, no se aplica expansión.
/// </summary>
public static class RouteGroups
{
    private static readonly Lazy<IReadOnlyDictionary<string, string[]>> _expansion =
        new(BuildExpansion, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Expande una colección de rutas canónicas incluyendo todos los prefijos URL
    /// reales de los módulos descubiertos. Las rutas sin grupo definido se devuelven tal cual.
    /// </summary>
    public static IEnumerable<string> Expandir(IEnumerable<string> rutas)
    {
        var expansion = _expansion.Value;
        var resultado = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var ruta in rutas)
        {
            resultado.Add(ruta);
            if (expansion.TryGetValue(ruta, out var adicionales))
                foreach (var extra in adicionales)
                    resultado.Add(extra);
        }

        return resultado;
    }

    /// <summary>
    /// Devuelve el mapa de expansión descubierto (útil para diagnóstico/logging al inicio).
    /// Clave = ruta canónica. Valor = todas las rutas del módulo.
    /// </summary>
    public static IReadOnlyDictionary<string, string[]> GetExpansionMap()
        => _expansion.Value;

    // ── Descubrimiento por reflexión ──────────────────────────────────────────

    private static IReadOnlyDictionary<string, string[]> BuildExpansion()
    {
        var result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        // Solo controllers del propio ensamblado que tengan al menos un [Route] explícito
        var controllerTypes = typeof(RouteGroups).Assembly.GetTypes()
            .Where(t => !t.IsAbstract
                     && !t.IsGenericType
                     && typeof(ControllerBase).IsAssignableFrom(t)
                     && t.GetCustomAttributes<RouteAttribute>().Any());

        // Agrupar por sub-namespace después de ".Controllers."
        // Ej: "FabricaHilos.Controllers.Capacitacion" → grupo "Capacitacion"
        //     "FabricaHilos.Controllers.RecursosHumanos.Aquarius" → grupo "RecursosHumanos.Aquarius"
        var grupos = controllerTypes
            .GroupBy(ExtractSubNamespace)
            .Where(g => !string.IsNullOrEmpty(g.Key) && g.Count() > 1);

        foreach (var grupo in grupos)
        {
            // Buscar el controlador canónico: nombre = último segmento del namespace + "Controller"
            // Ej: namespace "Capacitacion" → busca "CapacitacionController"
            var ultimoSegmento = grupo.Key.Split('.').Last();
            var canonico = grupo.FirstOrDefault(t =>
                t.Name.Equals(ultimoSegmento + "Controller", StringComparison.OrdinalIgnoreCase));

            if (canonico is null) continue; // Sin canónico = sin expansión automática

            var rutaCanonica = GetMainRoutePrefix(canonico);
            if (rutaCanonica is null) continue;

            // Filtro de segmento: solo incluir rutas del grupo que compartan el primer segmento
            // con la ruta canónica. Esto excluye rutas alternativas/alias sin prefijo coherente.
            // Ej: canónica="/recursoshumanos/capacitacion" → primer segmento="recursoshumanos"
            //     Se excluye "/anulardocumento" (primer segmento="anulardocumento") ✓
            var primerSegmento = GetFirstSegment(rutaCanonica);

            var todasLasRutas = grupo
                .SelectMany(GetAllRoutePrefixes)
                .Where(r => GetFirstSegment(r)
                    .Equals(primerSegmento, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(r => r)
                .ToArray();

            if (todasLasRutas.Length > 1)
                result[rutaCanonica] = todasLasRutas;
        }

        return result;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Extrae el sub-namespace después de ".Controllers."
    /// Ej: "FabricaHilos.Controllers.Capacitacion" → "Capacitacion"
    ///     "FabricaHilos.Controllers.RecursosHumanos.Aquarius" → "RecursosHumanos.Aquarius"
    /// </summary>
    private static string ExtractSubNamespace(Type t)
    {
        const string marker = ".Controllers.";
        var ns = t.Namespace ?? string.Empty;
        var idx = ns.IndexOf(marker, StringComparison.Ordinal);
        return idx >= 0 ? ns[(idx + marker.Length)..] : string.Empty;
    }

    /// <summary>Primer prefijo de ruta del controlador (primer atributo [Route], sin token [action]).</summary>
    private static string? GetMainRoutePrefix(Type t)
    {
        var template = t.GetCustomAttributes<RouteAttribute>()
            .Select(a => a.Template ?? string.Empty)
            .FirstOrDefault(tmpl => !string.IsNullOrEmpty(tmpl));

        return template is null ? null : NormalizeTemplate(template);
    }

    /// <summary>
    /// Todos los prefijos de ruta del controlador.
    /// Un controlador puede tener múltiples atributos [Route].
    /// </summary>
    private static IEnumerable<string> GetAllRoutePrefixes(Type t)
        => t.GetCustomAttributes<RouteAttribute>()
            .Select(a => a.Template ?? string.Empty)
            .Where(tmpl => !string.IsNullOrEmpty(tmpl))
            .Select(NormalizeTemplate)
            .Distinct(StringComparer.OrdinalIgnoreCase);

    /// <summary>Convierte un template de ruta en prefijo URL normalizado en minúsculas.</summary>
    private static string NormalizeTemplate(string template)
        => "/" + template
            .Replace("/[action]", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("[action]", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim('/')
            .ToLowerInvariant();

    /// <summary>
    /// Primer segmento de una ruta URL.
    /// Ej: "/recursoshumanos/capacitacion" → "recursoshumanos"
    ///     "/sistemas" → "sistemas"
    /// </summary>
    private static string GetFirstSegment(string route)
    {
        var trimmed = route.TrimStart('/');
        var slash = trimmed.IndexOf('/');
        return slash >= 0 ? trimmed[..slash] : trimmed;
    }
}
