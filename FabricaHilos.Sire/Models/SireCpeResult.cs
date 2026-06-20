namespace FabricaHilos.Sire.Models;

/// <summary>
/// Resultado de la consulta de un comprobante electrónico en la API CPE de SUNAT.
/// </summary>
public sealed class SireCpeResult
{
    public byte[] Contenido { get; init; } = [];
    public string ContentType { get; init; } = "application/octet-stream";
    public string NombreArchivo { get; init; } = "comprobante";
}
