namespace FabricaHilos.Notificaciones.Abstractions;

public interface IEmailNotificacionService
{
    /// <summary>
    /// Envía una notificación de correo.
    /// El payload determina el template a usar y los datos a inyectar.
    /// Retorna true si el envío fue exitoso, false en caso de error.
    /// </summary>
    Task<bool> EnviarAsync(INotificacionPayload payload, CancellationToken ct = default);
}
