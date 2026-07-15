namespace FabricaHilos.Services.Sire;

/// <summary>
/// Configuración para el reporte de documentos "Solo SUNAT" en Compras.
/// Se mapea desde la sección "SireReporteCompras" del appsettings.json.
/// </summary>
public sealed class SireReporteComprasOptions
{
    /// <summary>Destinatarios principales del correo (To). Puede ser uno o varios.</summary>
    public List<string> DestinatarioA { get; set; } = [];

    /// <summary>Destinatarios en copia (Cc). Puede ser vacío.</summary>
    public List<string> DestinatarosCc { get; set; } = [];

    /// <summary>
    /// RUCs que se excluyen del reporte (documentos de estos proveedores
    /// no aparecerán en el Excel enviado por correo).
    /// </summary>
    public List<string> RucsExcluidos { get; set; } = [];
}
