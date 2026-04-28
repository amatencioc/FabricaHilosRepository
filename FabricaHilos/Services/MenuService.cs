using FabricaHilos.Config;
using Microsoft.Extensions.Options;

namespace FabricaHilos.Services;

public interface IMenuService
{
    MenuOptions GetMenusActuales();
    (string? controller, string? action, string? area, string? url) GetLanding();
}

public class MenuService : IMenuService
{
    private readonly IOptions<MenuOptions> _globalMenus;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MenuService(
        IOptions<MenuOptions> globalMenus,
        IHttpContextAccessor httpContextAccessor)
    {
        _globalMenus = globalMenus;
        _httpContextAccessor = httpContextAccessor;
    }

    public MenuOptions GetMenusActuales()
    {
        var global    = _globalMenus.Value;
        var session   = _httpContextAccessor.HttpContext?.Session;
        var accesoWeb = session?.GetString("AccesoWeb") ?? string.Empty;

        var modulos = accesoWeb
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Admin tiene acceso a todo el menú según la configuración global
        if (modulos.Contains("Admin", StringComparer.OrdinalIgnoreCase))
            return global;

        bool Tiene(string modulo) => modulos.Contains(modulo);

        return new MenuOptions
        {
            // Menus principales
            Dashboard       = global.Dashboard,
            Produccion      = Tiene("Produccion"),
            Sgc             = Tiene("Sgc"),
            Facturacion     = Tiene("Facturacion"),
            Ventas          = Tiene("Ventas"),
            Seguridad       = Tiene("Seguridad"),
            RecursosHumanos = Tiene("RecursosHumanos"),

            // Submenus: Produccion
            ProduccionRegistroPreparatoria = global.ProduccionRegistroPreparatoria && Tiene("Produccion"),
            ProduccionAutoconer            = global.ProduccionAutoconer            && Tiene("Produccion"),
            ProduccionAutoconerPorPartida  = global.ProduccionAutoconerPorPartida  && Tiene("Produccion"),
            ProduccionAutoconerPorCanillas = global.ProduccionAutoconerPorCanillas && Tiene("Produccion"),

            // Submenus: Sgc
            SgcPedidos                 = global.SgcPedidos                 && Tiene("Sgc"),
            SgcDespachos               = global.SgcDespachos               && Tiene("Sgc"),
            SgcDespachosRelacionFacCli = global.SgcDespachosRelacionFacCli && Tiene("Sgc"),
            SgcDespachosCargarTC       = global.SgcDespachosCargarTC       && Tiene("Sgc"),

            // Submenus: Facturacion
            FacturacionImportarFacturas = global.FacturacionImportarFacturas && Tiene("Facturacion"),
            FacturacionListaDocumentos  = global.FacturacionListaDocumentos  && Tiene("Facturacion"),

            // Submenus: Ventas
            VentasConsultaTC             = global.VentasConsultaTC             && Tiene("Ventas"),
            VentasIndicadoresComerciales = global.VentasIndicadoresComerciales && Tiene("Ventas"),
            VentasVentasPorMercado       = global.VentasVentasPorMercado       && Tiene("Ventas"),
            VentasDashboardComercial        = global.VentasDashboardComercial        && Tiene("Ventas"),
            VentasDashboardComercialMaestro = global.VentasDashboardComercialMaestro && Tiene("Ventas"),
            VentasDashboardGerencial        = global.VentasDashboardGerencial        && Tiene("Ventas"),

            // Submenus: Seguridad
            SeguridadInspecciones = global.SeguridadInspecciones && Tiene("Seguridad"),

            // Submenus: Recursos Humanos
            RhMarcaciones         = global.RhMarcaciones         && Tiene("RecursosHumanos"),
            RhCompensacionDiaDia  = global.RhCompensacionDiaDia  && Tiene("RecursosHumanos"),

            // Menú principal: Logística
            Logistica              = Tiene("Logistica"),
            LogisticaRequerimiento = global.LogisticaRequerimiento && Tiene("Logistica"),
            LogisticaOrdenCompra   = global.LogisticaOrdenCompra   && Tiene("Logistica"),

            // Menú principal: Créditos y Cobranzas
            CreditosCobranza = Tiene("CreditosCobranza"),
            CcNivelMorosidad = global.CcNivelMorosidad && Tiene("CreditosCobranza"),
            CcNivelTiempo    = global.CcNivelTiempo    && Tiene("CreditosCobranza"),
        };
    }

    public (string? controller, string? action, string? area, string? url) GetLanding()
    {
        var menus = GetMenusActuales();

        if (menus.Dashboard)        return ("Home",        "Index", null, null);
        if (menus.Produccion)       return ("Produccion",  "Index", null, null);
        if (menus.Sgc)              return ("Sgc",         "Index", null, null);
        if (menus.Facturacion)      return ("Facturacion", "Index", null, null);
        if (menus.Ventas)           return ("Ventas",      "Index", null, null);
        if (menus.Seguridad)        return ("Inspeccion",  "Index", null, null);
        if (menus.RecursosHumanos)  return ("RecursosHumanos", "Index", null, null);
        if (menus.Logistica)        return (null, null, null, "/Logistica/Requerimiento");
        return ("RegistroPreparatoria", "Index", null, null);
    }
}