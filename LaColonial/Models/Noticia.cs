namespace LaColonial.Models;

public class Noticia
{
    public string Slug        { get; set; } = "";
    public string Titulo      { get; set; } = "";
    public string Categoria   { get; set; } = "";
    public string Fecha       { get; set; } = "";
    public string FechaHtml   { get; set; } = "";   // datetime attribute
    public string Imagen      { get; set; } = "";   // filename in /images/noticias/
    public string Extracto    { get; set; } = "";
    public string Contenido   { get; set; } = "";   // HTML completo del artículo
    public bool   EsEvento    { get; set; }
    public bool   Destacada   { get; set; }
}
