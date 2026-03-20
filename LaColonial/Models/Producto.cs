namespace LaColonial.Models;

public class Producto
{
    public string Slug        { get; set; } = "";
    public string Nombre      { get; set; } = "";
    public string NombreCorto { get; set; } = "";
    public string ImagenPortada { get; set; } = "";   // path relativo desde /images/
    public string Extracto    { get; set; } = "";
    public string ContenidoHtml { get; set; } = "";
    public string[] Galeria   { get; set; } = Array.Empty<string>(); // paths relativos desde /images/
}
