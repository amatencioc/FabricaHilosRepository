namespace FabricaHilos.LecturaCorreos.Models;

/// <summary>
/// Representa un adjunto extraído de un correo electrónico.
/// <see cref="TipoAdjunto"/> discrimina entre "XML" y "PDF".
/// Solo uno de <see cref="ContenidoXml"/> o <see cref="ContenidoPdf"/> tendrá valor.
/// </summary>
public class AdjuntoCorreo
{
    /// <summary>"XML" | "PDF"</summary>
    public string   TipoAdjunto   { get; set; } = string.Empty;
    public string   NombreArchivo { get; set; } = string.Empty;

    /// <summary>Contenido de texto del XML. Solo presente cuando <see cref="TipoAdjunto"/> es "XML".</summary>
    public string?  ContenidoXml  { get; set; }

    /// <summary>Bytes del PDF. Solo presente cuando <see cref="TipoAdjunto"/> es "PDF".</summary>
    public byte[]?  ContenidoPdf  { get; set; }

    public string   Asunto        { get; set; } = string.Empty;
    public string   Remitente     { get; set; } = string.Empty;
    public DateTime FechaCorreo   { get; set; }
}
