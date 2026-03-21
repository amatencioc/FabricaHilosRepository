using FabricaHilos.Notificaciones.Abstractions;

namespace FabricaHilos.Notificaciones.Models.Payloads;

/// <summary>
/// Payload para notificar a un remitente que su documento adjunto
/// está en limbo en FH_LECTCORREOS_PDF_ADJUNTOS:
/// no tiene archivo XML válido o el tipo de documento no está registrado.
/// Corresponde al template: Templates/DocumentoLimbo.html
/// </summary>
public class DocumentoLimboPayload : INotificacionPayload
{
    // --- Routing del correo ---
    public TipoNotificacion Tipo               => TipoNotificacion.DocumentoLimbo;
    public required string  CorreoDestinatario { get; set; }
    public required string  NombreDestinatario { get; set; }

    // --- Datos del caso ---
    public required string NombreRemitente { get; set; }
    public required string CorreoRemitente { get; set; }
    public required string FechaRecepcion  { get; set; }
    public required string NombreArchivo   { get; set; }
    public required string MotivoError     { get; set; }

    /// <summary>
    /// Mapea las propiedades a los {{placeholders}} del template HTML.
    /// </summary>
    public Dictionary<string, string> ObtenerReemplazos() => new()
    {
        { "NombreRemitente", NombreRemitente },
        { "CorreoRemitente", CorreoRemitente },
        { "FechaRecepcion",  FechaRecepcion  },
        { "NombreArchivo",   NombreArchivo   },
        { "MotivoError",     MotivoError     },
    };
}
