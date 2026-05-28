namespace FabricaHilos.Sire.Models;

public sealed class ConstanciaCierre
{
    public string NombreArchivo { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public byte[] Contenido { get; set; } = [];
}
