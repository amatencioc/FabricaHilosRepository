using FabricaHilos.Config;
using Microsoft.Extensions.Options;

namespace FabricaHilos.Services;

public interface IMenuService
{
    MenuOptions GetMenusActuales();
    (string controller, string action, string? area) GetLanding();
}

public class MenuService : IMenuService
{
    private readonly IOptions<MenuOptions> _globalMenus;
    private readonly IConfiguration _config;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MenuService(
        IOptions<MenuOptions> globalMenus,
        IConfiguration config,
        IHttpContextAccessor httpContextAccessor)
    {
        _globalMenus = globalMenus;
        _config = config;
        _httpContextAccessor = httpContextAccessor;
    }

    public MenuOptions GetMenusActuales()
    {
        var global = _globalMenus.Value;
        var user   = _httpContextAccessor.HttpContext?.User;
        var username = user?.Identity?.Name;
        var oracleUser = _httpContextAccessor.HttpContext?.Session.GetString("OracleUser");

        if (string.IsNullOrEmpty(username))
            return global;

        // Verificar si el usuario tiene acceso al módulo VENTAS
        bool tieneAccesoVentas = TieneAccesoVentas(oracleUser ?? username);

        var allowed = _config.GetSection($"UserMenus:{username}").Get<string[]>();

        // Si el usuario tiene UserMenus configurados, esos tienen SIEMPRE precedencia,
        // incluso sobre el rol Admin (evita que un rol incorrecto en BD dé acceso total
        // a usuarios que deberían estar restringidos).
        if (allowed == null || allowed.Length == 0)
        {
            // Caso especial: Usuarios de VENTAS (COSTOS2 o VENT...)
            // Solo deben ver el módulo VENTAS, independientemente de su rol
            if (tieneAccesoVentas)
            {
                return new MenuOptions
                {
                    Dashboard = false,
                    Produccion = false,
                    Sgc = false,
                    Facturacion = false,
                    Ventas = true,
                    Seguridad = false,
                    ProduccionRegistroPreparatoria = false,
                    ProduccionAutoconer = false,
                    ProduccionAutoconerPorPartida = false,
                    ProduccionAutoconerPorCanillas = false,
                    SgcDashboard = false,
                    SgcPedidos = false,
                    SgcDespachos = false,
                    SgcDespachosRelacionFacCli = false,
                    SgcDespachosCargarTC = false,
                    FacturacionImportarFacturas = false,
                    FacturacionListaDocumentos = false,
                    VentasConsultaTC = true,
                    SeguridadInspecciones = false
                };
            }

            // Sin UserMenus: Admin respeta la configuración global
            if (user!.IsInRole("Admin"))
            {
                return global;
            }

            // Otros usuarios sin UserMenus ven el menú global (sin Ventas)
            var menusGlobal = new MenuOptions
            {
                Dashboard = global.Dashboard,
                Produccion = global.Produccion,
                Sgc = global.Sgc,
                Facturacion = global.Facturacion,
                Ventas = false,
                Seguridad = global.Seguridad,
                ProduccionRegistroPreparatoria = global.ProduccionRegistroPreparatoria,
                ProduccionAutoconer = global.ProduccionAutoconer,
                ProduccionAutoconerPorPartida = global.ProduccionAutoconerPorPartida,
                ProduccionAutoconerPorCanillas = global.ProduccionAutoconerPorCanillas,
                SgcDashboard = global.SgcDashboard,
                SgcPedidos = global.SgcPedidos,
                SgcDespachos = global.SgcDespachos,
                SgcDespachosRelacionFacCli = global.SgcDespachosRelacionFacCli,
                SgcDespachosCargarTC = global.SgcDespachosCargarTC,
                FacturacionImportarFacturas = global.FacturacionImportarFacturas,
                FacturacionListaDocumentos = global.FacturacionListaDocumentos,
                VentasConsultaTC = false,
                SeguridadInspecciones = global.SeguridadInspecciones
            };
            return menusGlobal;
        }

        // Menú principal visible si el global lo habilita Y el usuario lo tiene en su lista
        bool Menu(bool globalEnabled, string key)
        {
            // Aplicar restricción especial para VENTAS
            if (key == "Ventas" && !tieneAccesoVentas)
                return false;

            return globalEnabled && allowed.Contains(key, StringComparer.OrdinalIgnoreCase);
        }

        // Submenú visible si:
        //   1. El global lo habilita
        //   2. El menú padre es accesible para el usuario
        //   3. Si hay entradas "Padre.X" en la lista → solo los explícitos; si no hay → todos
        bool SubMenu(bool globalEnabled, string parentKey, string subKey)
        {
            if (!globalEnabled) return false;
            if (!allowed.Contains(parentKey, StringComparer.OrdinalIgnoreCase)) return false;
            var prefix = parentKey + ".";
            var hasSubRestrictions = allowed.Any(a => a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            if (!hasSubRestrictions) return true;
            return allowed.Contains(subKey, StringComparer.OrdinalIgnoreCase);
        }

        return new MenuOptions
        {
            // ── Menús principales ─────────────────────────────────────────
            Dashboard       = Menu(global.Dashboard,       "Dashboard"),
            Produccion      = Menu(global.Produccion,      "Produccion"),
            Sgc             = Menu(global.Sgc,             "Sgc"),
            Facturacion     = Menu(global.Facturacion,     "Facturacion"),
            Ventas          = Menu(global.Ventas,          "Ventas"),
            Seguridad       = Menu(global.Seguridad,       "Seguridad"),

            // ── Submenús: Producción ──────────────────────────────────────
            ProduccionRegistroPreparatoria = SubMenu(global.ProduccionRegistroPreparatoria, "Produccion", "Produccion.RegistroPreparatoria"),
            ProduccionAutoconer            = SubMenu(global.ProduccionAutoconer,            "Produccion", "Produccion.Autoconer"),
            ProduccionAutoconerPorPartida  = SubMenu(global.ProduccionAutoconerPorPartida,  "Produccion", "Produccion.Autoconer.PorPartida"),
            ProduccionAutoconerPorCanillas = SubMenu(global.ProduccionAutoconerPorCanillas, "Produccion", "Produccion.Autoconer.PorCanillas"),

            // ── Submenús: SGC ─────────────────────────────────────────────
            SgcDashboard = SubMenu(global.SgcDashboard, "Sgc", "Sgc.Dashboard"),
            SgcPedidos   = SubMenu(global.SgcPedidos,   "Sgc", "Sgc.Pedidos"),
            SgcDespachos = SubMenu(global.SgcDespachos, "Sgc", "Sgc.Despachos"),
            SgcDespachosRelacionFacCli = SubMenu(global.SgcDespachosRelacionFacCli, "Sgc", "Sgc.Despachos.RelacionFacCli"),
            SgcDespachosCargarTC = SubMenu(global.SgcDespachosCargarTC, "Sgc", "Sgc.Despachos.CargarTC"),

            // ── Submenús: Facturación ─────────────────────────────────────
            FacturacionImportarFacturas = SubMenu(global.FacturacionImportarFacturas, "Facturacion", "Facturacion.ImportarFacturas"),
            FacturacionListaDocumentos  = SubMenu(global.FacturacionListaDocumentos,  "Facturacion", "Facturacion.ListaDocumentos"),

            // ── Submenús: Ventas ──────────────────────────────────────────
            VentasConsultaTC = SubMenu(global.VentasConsultaTC, "Ventas", "Ventas.ConsultaTC"),

            // ── Submenús: Seguridad ───────────────────────────────────────
            SeguridadInspecciones = SubMenu(global.SeguridadInspecciones, "Seguridad", "Seguridad.Inspeccion"),
        };
    }

    public (string controller, string action, string? area) GetLanding()
    {
        var menus = GetMenusActuales();

        if (menus.Dashboard)        return ("Home",       "Index", null);
        if (menus.Produccion)       return ("Produccion", "Index", null);
        if (menus.Sgc)              return ("Sgc",        "Index", null);
        if (menus.Facturacion)      return ("Facturacion",          "Index", null);
        if (menus.Ventas)           return ("Ventas",               "Index", null);
        if (menus.Seguridad)        return ("Inspeccion",           "Index", null);
        return ("RegistroPreparatoria", "Index", null);
    }

    /// <summary>
    /// Verifica si el usuario tiene acceso al módulo VENTAS.
    /// Solo permite acceso a:
    /// - Usuario específico: COSTOS2
    /// - Usuarios que comiencen con: VENT (ejemplo: VENT001, VENTADMIN, etc.)
    /// </summary>
    private bool TieneAccesoVentas(string usuario)
    {
        if (string.IsNullOrEmpty(usuario))
            return false;

        var usuarioUpper = usuario.ToUpperInvariant();
        return usuarioUpper == "COSTOS2" || usuarioUpper.StartsWith("VENT");
    }
}
