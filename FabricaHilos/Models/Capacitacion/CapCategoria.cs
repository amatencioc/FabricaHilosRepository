namespace FabricaHilos.Models.Capacitacion;

public class CapCategoria
{
    public int     IdCategoria  { get; set; }
    public string  Nombre       { get; set; } = "";
    public string? Descripcion  { get; set; }
    public string  IconoBs      { get; set; } = "bi-mortarboard";
    public string  ColorUi      { get; set; } = "#0d6efd";
    public int     Orden        { get; set; } = 1;
    public string  Activo       { get; set; } = "S";

    // Computed
    public int TotalCursos      { get; set; }  // para vista catálogo
}
