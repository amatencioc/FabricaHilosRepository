namespace FabricaHilos.Notificaciones.Models;

/// <summary>
/// Enum de todos los tipos de notificación disponibles.
/// Convención: el nombre del valor = nombre del archivo .html en Templates/
/// Ejemplo: DocumentoLimbo → Templates/DocumentoLimbo.html
/// Para agregar un nuevo tipo: agregar el valor aquí y crear su .html en Templates/
/// </summary>
public enum TipoNotificacion
{
    /// <summary>Notifica al remitente que su PDF no tiene XML válido o tipo no registrado.</summary>
    DocumentoLimbo,

    /// <summary>Notifica a facturación sobre un requerimiento de certificado listo para facturar.</summary>
    EnvioCertificadoFacturacion,

    // Futuros casos (agregar aquí y crear Templates/{Nombre}.html):
    // DocumentoPorVencer,
    // ErrorProcesamiento,
    // AlertaAdministrativa,
    // ConfirmacionRecepcion,
}
