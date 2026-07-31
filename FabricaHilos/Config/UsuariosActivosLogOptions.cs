namespace FabricaHilos.Config;

/// <summary>
/// Opciones para la persistencia del historial acumulado de interacciones de usuarios activos.
/// Se mapean desde la sección "Logs" en appsettings.json.
/// </summary>
public sealed class UsuariosActivosLogOptions
{
    public const string SectionName = "Logs";

    /// <summary>
    /// Ruta (relativa al directorio base de la aplicación, o absoluta) donde se
    /// guardan los archivos JSON Lines de interacciones de usuarios activos,
    /// uno por módulo y por día (ej. "contabilidad_2026-01-15.jsonl").
    /// </summary>
    public string UsuariosActivosPath { get; set; } = "App_Data/UsuariosActivos";
}
