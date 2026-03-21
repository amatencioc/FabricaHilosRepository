namespace FabricaHilos.Notificaciones.Configuration;

/// <summary>
/// Configuración del servidor SMTP/OAuth2 para el envío de correos.
/// Se mapea desde la sección "Notificaciones:Email" del appsettings.json
/// del proyecto consumidor.
/// </summary>
public class EmailSettings
{
    /// <summary>Host del servidor SMTP (e.g. smtp.office365.com).</summary>
    public string SmtpHost { get; set; } = "smtp.office365.com";

    /// <summary>Puerto SMTP (generalmente 587 con STARTTLS o 993 con SSL).</summary>
    public int SmtpPort { get; set; } = 587;

    /// <summary>Indica si se usa SSL/TLS al conectar.</summary>
    public bool UsarSsl { get; set; } = true;

    /// <summary>Dirección de correo que aparece como remitente.</summary>
    public string UsuarioEnvio { get; set; } = string.Empty;

    /// <summary>Nombre visible del remitente en el correo.</summary>
    public string NombreEnvio { get; set; } = "Sistema La Colonial";

    // --- OAuth2 (Office 365) ---

    /// <summary>Tenant ID del directorio Azure AD.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Client ID (Application ID) del registro de aplicación en Azure.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Client Secret del registro de aplicación en Azure.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Contraseña para autenticación SMTP básica (usuario/contraseña).
    /// Solo se usa cuando <see cref="ClientSecret"/> está vacío (sin OAuth2).
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// URL del token OAuth2.
    /// Por defecto apunta al endpoint de Office 365 / Azure AD.
    /// </summary>
    public string AuthUrl { get; set; } =
        "https://login.microsoftonline.com/{TenantId}/oauth2/v2.0/token";

    /// <summary>Scope OAuth2 requerido por Office 365 SMTP.</summary>
    public string Scope { get; set; } = "https://outlook.office365.com/.default";
}
