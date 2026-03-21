namespace FabricaHilos.Notificaciones.Models;

/// <summary>
/// Catálogo de todos los tipos de notificación disponibles.
/// Convención: el nombre del valor coincide con el nombre del archivo .html
/// almacenado en Templates/ (sin extensión).
/// Para agregar un nuevo tipo: añadir el valor aquí y crear
/// Templates/{Nombre}.html con los mismos {{placeholders}} que el payload expone.
/// </summary>
public enum TipoNotificacion
{
    /// <summary>
    /// Notifica a un proveedor que su documento PDF no cuenta con XML válido
    /// o que el tipo de documento no está registrado en el sistema.
    /// Template: Templates/DocumentoLimbo.html
    /// </summary>
    DocumentoLimbo,

    // Ejemplos de futuros tipos — agregar aquí y crear el .html correspondiente:
    // DocumentoPorVencer,
    // ErrorProcesamiento,
    // AlertaAdministrativa,
    // ConfirmacionRecepcion,
}
