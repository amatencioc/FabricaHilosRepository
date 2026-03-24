namespace FabricaHilos.Config;

/// <summary>
/// Controla qué menús del sidebar son visibles.
/// Se configura en appsettings.json → sección "Menus".
/// false = oculto (en desarrollo), true = visible (terminado).
/// </summary>
public class MenuOptions
{
    public const string Seccion = "Menus";

    public bool Dashboard        { get; set; } = false;
    public bool Inventario       { get; set; } = false;
    public bool Produccion       { get; set; } = true;
    public bool Sgc              { get; set; } = true;
    public bool Facturacion      { get; set; } = true;
    public bool Ventas           { get; set; } = false;
    public bool RecursosHumanos  { get; set; } = false;
    public bool Administracion   { get; set; } = false;
}
