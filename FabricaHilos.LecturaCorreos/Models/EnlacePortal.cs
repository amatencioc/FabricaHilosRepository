namespace FabricaHilos.LecturaCorreos.Models;

/// <summary>
/// Representa la URL "CONSULTAR" extraida del cuerpo HTML de un correo
/// que no contiene adjuntos directos (XML/PDF/ZIP).
/// </summary>
public class EnlacePortal
{
    public string   UrlConsultar { get; set; } = string.Empty;
    public string   GrupoCorreo  { get; set; } = string.Empty;
    public string   Asunto       { get; set; } = string.Empty;
    public string   Remitente    { get; set; } = string.Empty;
    public DateTime FechaCorreo  { get; set; }
}
