namespace FabricaHilos.Config;

/// <summary>
/// Opciones para el control de acceso por red.
/// Se mapean desde la sección "RedInterna" en appsettings.json.
/// Soporta hot-reload: los cambios en appsettings.json se aplican
/// sin reiniciar la aplicación.
/// </summary>
public sealed class RedInternaOptions
{
    public const string SectionName = "RedInterna";

    /// <summary>
    /// Subnets internas en notación CIDR (p. ej. "10.0.7.0/24").
    /// Las IPs dentro de estas subnets tienen acceso total.
    /// </summary>
    public string[] Subnets { get; set; } = [];

    /// <summary>
    /// Prefijos de ruta accesibles desde internet (acceso externo).
    /// La comparación es insensible a mayúsculas y cubre el prefijo completo,
    /// por lo que "/saludocupacional" cubre también "/saludocupacional/inspeccioncom/...".
    /// </summary>
    public string[] RutasExternasPermitidas { get; set; } =
    [
        "/account/login",
        "/account/logout",
        "/account/accesodenegado",
        "/seguridad",
        "/produccion",
        "/registropreparatoria",
        "/autoconer",
    ];

    /// <summary>
    /// Prefijos de rutas estáticas que siempre se permiten (CSS, JS, imágenes, etc.).
    /// </summary>
    public string[] RutasEstaticasPermitidas { get; set; } =
    [
        "/css/",
        "/js/",
        "/lib/",
        "/images/",
        "/favicon.ico",
        "/_framework/",
    ];
}
