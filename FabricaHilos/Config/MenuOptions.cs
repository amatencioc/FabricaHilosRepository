namespace FabricaHilos.Config;

/// <summary>
/// Controla qué menús y submenús del sidebar son visibles.
/// Se configura en appsettings.json → sección "Menus".
/// false = oculto, true = visible.
/// Por usuario se usa "UserMenus:{username}" como lista de claves permitidas.
/// Clave de submenú usa notación punteada: "Produccion.Autoconer".
/// </summary>
public class MenuOptions
{
    public const string Seccion = "Menus";

    // ── Menús principales ─────────────────────────────────────────────────
    public bool Dashboard        { get; set; } = false;
    public bool Inventario       { get; set; } = false;
    public bool Produccion       { get; set; } = true;
    public bool Sgc              { get; set; } = true;
    public bool Facturacion      { get; set; } = true;
    public bool Ventas           { get; set; } = false;
    public bool RecursosHumanos  { get; set; } = false;
    public bool Administracion   { get; set; } = false;
    public bool Seguridad        { get; set; } = false;

    // ── Submenús: Inventario ──────────────────────────────────────────────
    public bool InventarioMateriaPrima      { get; set; } = true;
    public bool InventarioProductoTerminado { get; set; } = true;

    // ── Submenús: Producción ──────────────────────────────────────────────
    public bool ProduccionRegistroPreparatoria { get; set; } = true;
    public bool ProduccionAutoconer            { get; set; } = true;
    public bool ProduccionAutoconerPorPartida  { get; set; } = true;
    public bool ProduccionAutoconerPorCanillas { get; set; } = true;

    // ── Submenús: SGC ─────────────────────────────────────────────────────
    public bool SgcDashboard { get; set; } = true;
    public bool SgcPedidos   { get; set; } = true;
    public bool SgcDespachos { get; set; } = true;
    public bool SgcDespachosRelacionFacCli { get; set; } = true;
    public bool SgcDespachosCargarTC { get; set; } = true;

    // ── Submenús: Facturación ─────────────────────────────────────────────
    public bool FacturacionImportarFacturas { get; set; } = true;
    public bool FacturacionListaDocumentos  { get; set; } = true;

    // ── Submenús: Ventas ──────────────────────────────────────────────────
    public bool VentasConsultaTC { get; set; } = true;

    // ── Submenús: Recursos Humanos ────────────────────────────────────────
    public bool RecursosHumanosEmpleados  { get; set; } = true;
    public bool RecursosHumanosAsistencia { get; set; } = true;

    // ── Submenús: Administración ──────────────────────────────────────────
    public bool AdministracionRegistrarUsuario { get; set; } = true;

    // ── Submenús: Seguridad ───────────────────────────────────────────────
    public bool SeguridadInspecciones { get; set; } = true;

    /// <summary>
    /// Devuelve una instancia con todos los menús y submenús visibles.
    /// Se usa para usuarios Administrador, que no tienen restricciones.
    /// </summary>
    public static MenuOptions Todo() => new()
    {
        Dashboard        = true,
        Inventario       = true,
        Produccion       = true,
        Sgc              = true,
        Facturacion      = true,
        Ventas           = true,
        RecursosHumanos  = true,
        Administracion   = true,
        Seguridad        = true,

        InventarioMateriaPrima         = true,
        InventarioProductoTerminado    = true,

        ProduccionRegistroPreparatoria = true,
        ProduccionAutoconer            = true,
        ProduccionAutoconerPorPartida  = true,
        ProduccionAutoconerPorCanillas = true,

        SgcDashboard = true,
        SgcPedidos   = true,
        SgcDespachos = true,
        SgcDespachosRelacionFacCli = true,
        SgcDespachosCargarTC = true,

        FacturacionImportarFacturas = true,
        FacturacionListaDocumentos  = true,

        VentasConsultaTC = true,

        RecursosHumanosEmpleados  = true,
        RecursosHumanosAsistencia = true,

        AdministracionRegistrarUsuario = true,

        SeguridadInspecciones = true,
    };
}

