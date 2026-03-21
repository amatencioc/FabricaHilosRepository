using FabricaHilos.Notificaciones.Abstractions;
using FabricaHilos.Notificaciones.Models;

namespace FabricaHilos.Notificaciones.Models.Payloads;

/// <summary>
/// Datos necesarios para construir el correo de "documento en limbo".
/// Se usa cuando un adjunto PDF recibido en FH_LECTCORREOS_PDF_ADJUNTOS
/// no tiene archivo XML válido asociado o su tipo de documento no está registrado.
/// Template correspondiente: Templates/DocumentoLimbo.html
/// </summary>
public class DocumentoLimboPayload : INotificacionPayload
{
    // --- Routing del correo ---
    public TipoNotificacion Tipo               => TipoNotificacion.DocumentoLimbo;
    public required string  CorreoDestinatario { get; set; }
    public required string  NombreDestinatario { get; set; }

    // --- Datos específicos del documento en limbo ---
    public required string CorreoRemitente  { get; set; }
    public required string FechaRecepcion   { get; set; }
    public required string NombreArchivo    { get; set; }
    public required string MotivoError      { get; set; }

    /// <summary>
    /// Genera el diccionario de reemplazos para el template DocumentoLimbo.html.
    /// Cada clave corresponde a un {{placeholder}} en el HTML.
    /// </summary>
    public Dictionary<string, string> ObtenerReemplazos() => new()
    {
        ["NombreRemitente"] = NombreDestinatario,
        ["CorreoRemitente"] = CorreoRemitente,
        ["FechaRecepcion"]  = FechaRecepcion,
        ["NombreArchivo"]   = NombreArchivo,
        ["MotivoError"]     = MotivoError,
    };
}
