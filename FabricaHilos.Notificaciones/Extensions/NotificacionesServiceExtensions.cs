using FabricaHilos.Notificaciones.Abstractions;
using FabricaHilos.Notificaciones.Configuration;
using FabricaHilos.Notificaciones.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FabricaHilos.Notificaciones.Extensions;

/// <summary>
/// Métodos de extensión para registrar FabricaHilos.Notificaciones en el contenedor DI.
/// </summary>
public static class NotificacionesServiceExtensions
{
    /// <summary>
    /// Registra todos los servicios de notificaciones en el contenedor DI.
    /// Uso en el Program.cs del proyecto consumidor:
    /// <code>
    /// builder.Services.AddNotificaciones(builder.Configuration);
    /// </code>
    /// Requiere la sección "Notificaciones:Email" en appsettings.json.
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
