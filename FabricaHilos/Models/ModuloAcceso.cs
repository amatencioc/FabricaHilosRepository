namespace FabricaHilos.Models;

/// <summary>
/// Representa los modificadores y parámetros de acceso para un módulo específico,
/// extraídos del token de la forma: NombreModulo[modificador1,clave=valor,...].
/// Ejemplo: LogisticaOrdenCompra[noNuevaOC,estado=2]
/// </summary>
public class ModuloAcceso
{
    /// <summary>Instancia sin restricciones adicionales (acceso completo al módulo).</summary>
    public static readonly ModuloAcceso SinRestricciones = new();

    private readonly HashSet<string>            _modificadores;
    private readonly Dictionary<string, string> _parametros;

    public ModuloAcceso()
    {
        _modificadores = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _parametros    = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    internal ModuloAcceso(IEnumerable<string> modificadores, IDictionary<string, string> parametros)
    {
        _modificadores = new HashSet<string>(modificadores, StringComparer.OrdinalIgnoreCase);
        _parametros    = new Dictionary<string, string>(parametros, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Indica si el modificador (flag) está presente. Ej: "noNuevaOC".</summary>
    public bool TieneModificador(string modificador) =>
        _modificadores.Contains(modificador);

    /// <summary>Devuelve el valor de un parámetro o null si no existe. Ej: ObtenerParametro("estado") → "2".</summary>
    public string? ObtenerParametro(string clave) =>
        _parametros.TryGetValue(clave, out var v) ? v : null;
}
