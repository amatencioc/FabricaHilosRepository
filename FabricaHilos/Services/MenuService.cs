using FabricaHilos.Config;
using Microsoft.Extensions.Options;

namespace FabricaHilos.Services;

public interface IMenuService
{
    MenuOptions GetMenusActuales();
    (string controller, string action) GetLanding();
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

        if (string.IsNullOrEmpty(username))
            return global;

        var allowed = _config.GetSection($"UserMenus:{username}").Get<string[]>();

        // Si el usuario tiene UserMenus configurados, esos tienen SIEMPRE precedencia,
        // incluso sobre el rol Admin (evita que un rol incorrecto en BD dé acceso total
        // a usuarios que deberían estar restringidos).
        if (allowed == null || allowed.Length == 0)
        {
            // Sin UserMenus: Admin ve todo; cualquier otro ve el global
            if (user!.IsInRole("Admin"))
                return MenuOptions.Todo();

            return global;
        }

        // Menú principal visible si el global lo habilita Y el usuario lo tiene en su lista
        bool Menu(bool globalEnabled, string key) =>
            globalEnabled && allowed.Contains(key, StringComparer.OrdinalIgnoreCase);

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
            Inventario      = Menu(global.Inventario,      "Inventario"),
            Produccion      = Menu(global.Produccion,      "Produccion"),
            Sgc             = Menu(global.Sgc,             "Sgc"),
            Facturacion     = Menu(global.Facturacion,     "Facturacion"),
            Ventas          = Menu(global.Ventas,          "Ventas"),
            RecursosHumanos = Menu(global.RecursosHumanos, "RecursosHumanos"),
            Administracion  = Menu(global.Administracion,  "Administracion"),

            // ── Submenús: Inventario ──────────────────────────────────────
            InventarioMateriaPrima      = SubMenu(global.InventarioMateriaPrima,      "Inventario", "Inventario.MateriaPrima"),
            InventarioProductoTerminado = SubMenu(global.InventarioProductoTerminado, "Inventario", "Inventario.ProductoTerminado"),

            // ── Submenús: Producción ──────────────────────────────────────
            ProduccionRegistroPreparatoria = SubMenu(global.ProduccionRegistroPreparatoria, "Produccion", "Produccion.RegistroPreparatoria"),
            ProduccionAutoconer            = SubMenu(global.ProduccionAutoconer,            "Produccion", "Produccion.Autoconer"),

            // ── Submenús: SGC ─────────────────────────────────────────────
            SgcDashboard = SubMenu(global.SgcDashboard, "Sgc", "Sgc.Dashboard"),
            SgcPedidos   = SubMenu(global.SgcPedidos,   "Sgc", "Sgc.Pedidos"),

            // ── Submenús: Facturación ─────────────────────────────────────
            FacturacionImportarFacturas = SubMenu(global.FacturacionImportarFacturas, "Facturacion", "Facturacion.ImportarFacturas"),
            FacturacionListaDocumentos  = SubMenu(global.FacturacionListaDocumentos,  "Facturacion", "Facturacion.ListaDocumentos"),

            // ── Submenús: Ventas ──────────────────────────────────────────
            VentasClientes = SubMenu(global.VentasClientes, "Ventas", "Ventas.Clientes"),
            VentasPedidos  = SubMenu(global.VentasPedidos,  "Ventas", "Ventas.Pedidos"),

            // ── Submenús: Recursos Humanos ────────────────────────────────
            RecursosHumanosEmpleados  = SubMenu(global.RecursosHumanosEmpleados,  "RecursosHumanos", "RecursosHumanos.Empleados"),
            RecursosHumanosAsistencia = SubMenu(global.RecursosHumanosAsistencia, "RecursosHumanos", "RecursosHumanos.Asistencia"),

            // ── Submenús: Administración ──────────────────────────────────
            AdministracionRegistrarUsuario = SubMenu(global.AdministracionRegistrarUsuario, "Administracion", "Administracion.RegistrarUsuario"),
        };
    }

    public (string controller, string action) GetLanding()
    {
        var menus = GetMenusActuales();

        if (menus.Dashboard)        return ("Home",       "Index");
        if (menus.Produccion)       return ("Produccion", "Index");
        if (menus.Sgc)              return ("Sgc",        "Index");
        if (menus.Facturacion)      return ("Facturacion",          "Index");
        if (menus.Ventas)           return ("Ventas",               "Index");
        if (menus.Inventario)       return ("Inventario",           "Index");
        if (menus.RecursosHumanos)  return ("RecursosHumanos",      "Index");
        return ("RegistroPreparatoria", "Index");
    }
}
