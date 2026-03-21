using FabricaHilos.Notificaciones.Abstractions;
using FabricaHilos.Notificaciones.Configuration;
using FabricaHilos.Notificaciones.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FabricaHilos.Notificaciones.Extensions;

public static class NotificacionesServiceExtensions
{
    /// <summary>
    /// Registra el servicio de notificaciones en el contenedor de DI.
    ///
    /// Usar en el Program.cs del proyecto consumidor:
    ///   builder.Services.AddNotificaciones(builder.Configuration);
    ///
    /// Requiere la sección "Notificaciones:Email" en appsettings.json:
    /// {
    ///   "Notificaciones": {
    ///     "Email": {
    ///       "SmtpHost": "smtp.office365.com",
    ///       "SmtpPort": 587,
    ///       "UsarSsl": true,
    ///       "UsuarioEnvio": "notificaciones@colonial.com.pe",
    ///       "NombreEnvio": "Sistema La Colonial",
    ///       "PasswordEnvio": ""
    ///     }
    ///   }
    /// }
    /// </summary>
    public static IServiceCollection AddNotificaciones(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<EmailSettings>(
            configuration.GetSection("Notificaciones:Email"));

        services.AddScoped<IEmailNotificacionService, EmailNotificacionService>();

        return services;
    }
}
