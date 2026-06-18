namespace FabricaHilos.Services.Sire;

/// <summary>
/// Configuración para el reporte de documentos "Solo SUNAT" en Compras.
/// Se mapea desde la sección "SireReporteCompras" del appsettings.json.
/// </summary>
public sealed class SireReporteComprasOptions
{
    /// <summary>Destinatario principal del correo (To).</summary>
    public string DestinatarioA { get; set; } = string.Empty;

    /// <summary>Destinatarios en copia (Cc). Puede ser vacío.</summary>
    public List<string> DestinatarosCc { get; set; } = [];

    /// <summary>
    /// RUCs que se excluyen del reporte (documentos de estos proveedores
    /// no aparecerán en el Excel enviado por correo).
    /// </summary>
    public List<string> RucsExcluidos { get; set; } = [];
}
