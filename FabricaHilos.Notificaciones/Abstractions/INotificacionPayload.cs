using FabricaHilos.Notificaciones.Models;

namespace FabricaHilos.Notificaciones.Abstractions;

/// <summary>
/// Contrato que todo payload de notificación debe cumplir.
/// Permite que el servicio sea genérico y adaptable a cualquier escenario.
/// </summary>
public interface INotificacionPayload
{
    /// <summary>Tipo de notificación → determina qué template HTML se carga.</summary>
    TipoNotificacion Tipo { get; }

    /// <summary>Dirección de correo del destinatario.</summary>
    string CorreoDestinatario { get; }

    /// <summary>Nombre visible del destinatario.</summary>
    string NombreDestinatario { get; }

    /// <summary>
    /// Diccionario de reemplazos: clave = nombre del {{placeholder}} en el template,
    /// valor = dato real.
    /// Ejemplo: { "NombreRemitente", "Juan Pérez" }, { "MotivoError", "Sin XML" }
    /// </summary>
    Dictionary<string, string> ObtenerReemplazos();
}
