namespace FabricaHilos.Config;

/// <summary>
/// Opciones de una empresa cargadas desde appsettings.json → sección "EmpresaTema".
/// </summary>
public class EmpresaTemaOptions
{
    public const string SectionName = "EmpresaTema";

    /// <summary>Clave de la empresa activa. Debe coincidir con una clave en "Empresas".</summary>
    public string EmpresaActiva { get; set; } = "LaColonial";

    /// <summary>Mapa de empresas disponibles.</summary>
    public Dictionary<string, EmpresaConfig> Empresas { get; set; } = new();
}

/// <summary>
/// Configuración visual y de identidad de una empresa.
/// </summary>
public class EmpresaConfig
{
    /// <summary>Nombre corto para mostrar en sidebar/login (ej: "La Colonial").</summary>
    public string NombreCorto { get; set; } = string.Empty;

    /// <summary>Nombre completo para mostrar en subtítulo (ej: "Fábrica de Hilos S.A.").</summary>
    public string NombreCompleto { get; set; } = string.Empty;

    /// <summary>Ruta relativa al CSS de variables dentro de wwwroot (ej: "themes/LaColonial/variables.css").</summary>
    public string VariablesCssPath { get; set; } = string.Empty;

    /// <summary>Ruta relativa al logo icono dentro de wwwroot (ej: "themes/LaColonial/logo-icon.svg").</summary>
    public string LogoIconPath { get; set; } = string.Empty;

    /// <summary>Ruta relativa al logo completo dentro de wwwroot (ej: "themes/LaColonial/logo.png"). Opcional.</summary>
    public string LogoFullPath { get; set; } = string.Empty;

    /// <summary>Texto alternativo del logo para accesibilidad.</summary>
    public string LogoAlt { get; set; } = string.Empty;

    /// <summary>Sufijo que aparece en el &lt;title&gt; de cada página.</summary>
    public string TituloSistema { get; set; } = "Sistema de Gestión";

    /// <summary>Texto del footer.</summary>
    public string TextoFooter { get; set; } = string.Empty;

    /// <summary>RUC de la empresa (ej: "20100096260").</summary>
    public string Ruc { get; set; } = string.Empty;
}
