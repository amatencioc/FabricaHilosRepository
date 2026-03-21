namespace FabricaHilos.Notificaciones.Configuration;

/// <summary>
/// Configuración del servidor de correo saliente.
/// Se mapea desde la sección "Notificaciones:Email" del appsettings.json
/// del proyecto consumidor.
/// </summary>
public class EmailSettings
{
    public string SmtpHost      { get; set; } = string.Empty;
    public int    SmtpPort      { get; set; } = 587;
    public bool   UsarSsl       { get; set; } = true;
    public string UsuarioEnvio  { get; set; } = string.Empty;
    public string NombreEnvio   { get; set; } = "Sistema La Colonial";
    public string PasswordEnvio { get; set; } = string.Empty;

    // OAuth2 Office365 (opcional — usar cuando UsarOAuth2 = true)
    public string TenantId     { get; set; } = string.Empty;
    public string ClientId     { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public bool   UsarOAuth2   { get; set; } = false;
}
