namespace FabricaHilos.Config;

/// <summary>
/// Controla qué submenús del sidebar están habilitados globalmente.
/// Se configura en appsettings.json → sección "Menus".
/// El acceso por módulo se determina desde el campo ACCESO_WEB de Oracle (CS_USER).
/// </summary>
public class MenuOptions
{
    public const string Seccion = "Menus";

    // ── Menús principales ─────────────────────────────────────────────────
    public bool Dashboard        { get; set; } = false;
    public bool Produccion       { get; set; } = true;
    public bool Sgc              { get; set; } = true;
    public bool Facturacion      { get; set; } = true;
    public bool SireFlag         { get; set; } = false;
    public bool Ventas           { get; set; } = false;
    public bool Seguridad        { get; set; } = false;

    // ── Submenús: Producción ──────────────────────────────────────────────
    public bool ProduccionRegistroPreparatoria { get; set; } = true;
    public bool ProduccionAutoconer            { get; set; } = true;
    public bool ProduccionAutoconerPorPartida  { get; set; } = true;
    public bool ProduccionAutoconerPorCanillas { get; set; } = true;

    // ── Submenús: SGC ─────────────────────────────────────────────────────
    public bool SgcPedidos   { get; set; } = true;
    public bool SgcDespachos { get; set; } = true;
    public bool SgcDespachosRelacionFacCli { get; set; } = true;
    public bool SgcDespachosCargarTC { get; set; } = true;
    public bool SgcAnalisisReclamo { get; set; } = true;

    // ── Submenús: Facturación ─────────────────────────────────────────────
    public bool FacturacionImportarFacturas { get; set; } = true;
    public bool FacturacionListaDocumentos  { get; set; } = true;

    // ── Submenús: Ventas ──────────────────────────────────────────────────
    public bool VentasConsultaTC { get; set; } = true;
    public bool VentasIndicadorComercialMaestro { get; set; } = false;
    public bool VentasDashboardComercialMaestro { get; set; } = true;
    public bool VentasDashboardGerencial { get; set; } = true;

    // ── Submenús: Seguridad ───────────────────────────────────────────────
    public bool SeguridadInspecciones { get; set; } = true;

    // ── Menús: Recursos Humanos ───────────────────────────────────────────
    public bool RecursosHumanos              { get; set; } = false;
    public bool RhMarcaciones                { get; set; } = true;
    public bool RhCompensacionDiaDia         { get; set; } = true;
    public bool RhCompensacionDdc            { get; set; } = true;
    public bool RhAutorizacionHoras           { get; set; } = true;

    // ── Submenús: RH → Indicadores
    public bool RhIndicadores                              { get; set; } = true;
    public bool RhIndicadoresHorasExtras                   { get; set; } = true;
    public bool RhIndicadoresConcentracionSobretiempo      { get; set; } = true;
    public bool RhIndicadoresEvolucionMasaSalarial         { get; set; } = true;

    // ── Menús: Logística
    public bool Logistica                { get; set; } = false;
    public bool LogisticaRequerimiento   { get; set; } = true;
    public bool LogisticaOrdenCompra     { get; set; } = true;
    public bool LogisticaIndicadores     { get; set; } = true;

    // ── Menús: Créditos y Cobranzas ───────────────────────────────────────
    public bool CreditosCobranza         { get; set; } = false;
    public bool CcNivelMorosidad         { get; set; } = true;
    public bool CcNivelTiempo            { get; set; } = true;

    // ── Menús: Contabilidad ──────────────────────────────────────────────
    public bool Contabilidad      { get; set; } = false;
    public bool ContabilidadSire  { get; set; } = true;

    // ── Menús: Sistemas ──────────────────────────────────────────────────
    public bool Sistemas                                    { get; set; } = false;
    public bool SistemasIndicadores                         { get; set; } = true;
    public bool SistemasIndicadoresDesarrollo               { get; set; } = true;
    public bool SistemasIndicadoresIncidencia               { get; set; } = true;
    public bool SistemasIndicadoresSeguimientoDev           { get; set; } = true;
    public bool SistemasRequerimientos                      { get; set; } = true;
    public bool SistemasRequerimientosAnularDocumento       { get; set; } = true;

    // ── Menús: Planeamiento ──────────────────────────────────────────────
    public bool Planeamiento              { get; set; } = false;
    public bool PlaneamientoDashboard     { get; set; } = true;
    public bool PlaneamientoPedido        { get; set; } = true;
    public bool PlaneamientoCargaMaquinas { get; set; } = true;
    public bool PlaneamientoAlertas            { get; set; } = true;
    public bool PlaneamientoProximosVencer      { get; set; } = true;
    public bool PlaneamientoKPIs                { get; set; } = false;
    public bool PlaneamientoPendientesDespacho   { get; set; } = true;

    /// <summary>
    /// Devuelve una instancia con todos los menús y submenús visibles.
    /// Se usa para usuarios Administrador, que no tienen restricciones.
    /// </summary>
    public static MenuOptions Todo() => new()
    {
        Dashboard        = true,
        Produccion       = true,
        Sgc              = true,
        Facturacion      = true,
        SireFlag         = true,
        Ventas           = true,
        Seguridad        = true,

        ProduccionRegistroPreparatoria = true,
        ProduccionAutoconer            = true,
        ProduccionAutoconerPorPartida  = true,
        ProduccionAutoconerPorCanillas = true,

        SgcPedidos   = true,
        SgcDespachos = true,
        SgcDespachosRelacionFacCli = true,
        SgcDespachosCargarTC = true,
        SgcAnalisisReclamo = true,

        FacturacionImportarFacturas = true,
        FacturacionListaDocumentos  = true,

        VentasConsultaTC = true,
        VentasIndicadorComercialMaestro = true,
        VentasDashboardComercialMaestro = true,
        VentasDashboardGerencial = true,

        SeguridadInspecciones = true,

        RecursosHumanos  = true,
        RhMarcaciones          = true,
        RhAutorizacionHoras    = true,

        RhIndicadores          = true,
        RhIndicadoresHorasExtras = true,

        Logistica              = true,
        LogisticaRequerimiento = true,
        LogisticaOrdenCompra   = true,
        LogisticaIndicadores   = true,

        CreditosCobranza = true,
        CcNivelMorosidad = true,
        CcNivelTiempo    = true,

        Contabilidad     = true,
        ContabilidadSire = true,

        Planeamiento              = true,
        PlaneamientoDashboard     = true,
        PlaneamientoPedido        = true,
        PlaneamientoCargaMaquinas = true,
        PlaneamientoAlertas            = true,
        PlaneamientoProximosVencer      = true,
        PlaneamientoKPIs                = true,
        PlaneamientoPendientesDespacho  = true,

        Sistemas                              = true,
        SistemasIndicadores                   = true,
        SistemasIndicadoresDesarrollo         = true,
        SistemasIndicadoresIncidencia         = true,
        SistemasIndicadoresSeguimientoDev     = true,
        SistemasRequerimientos                = true,
        SistemasRequerimientosAnularDocumento = true,
    };
}

