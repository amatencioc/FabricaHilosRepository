using FabricaHilos.LecturaCorreos.Models;

namespace FabricaHilos.LecturaCorreos.Data;

/// <summary>
/// Operaciones sobre FH_LECTCORREOS_ARCHIVOS para el ciclo de notificación por correo.
/// </summary>
public interface IPdfLimboRepository
{
    /// <summary>
    /// Devuelve los registros con ESTADO = 'PENDIENTE' que aún no han recibido notificación.
    /// El contenido binario (PDF) se excluye de la consulta para evitar cargas innecesarias.
    /// </summary>
    Task<IReadOnlyList<AdjuntoPdf>> ObtenerPendientesNotificacionAsync();

    /// <summary>Actualiza el ESTADO a 'ENVIO_CORREO_OK' cuando el correo se envió con éxito.</summary>
    Task MarcarCorreoEnviadoAsync(long id);

    /// <summary>Actualiza el ESTADO a 'ERROR_CORREO' y loguea el mensaje de error.</summary>
    Task MarcarErrorNotificacionAsync(long id, string mensajeError);
}
