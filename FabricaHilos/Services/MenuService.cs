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
        var username = _httpContextAccessor.HttpContext?.User?.Identity?.Name;

        if (string.IsNullOrEmpty(username))
            return global;

        var allowed = _config.GetSection($"UserMenus:{username}").Get<string[]>();

        // Sin restricción de usuario → menú global completo
        if (allowed == null || allowed.Length == 0)
            return global;

        return new MenuOptions
        {
            Dashboard       = global.Dashboard       && allowed.Contains("Dashboard",       StringComparer.OrdinalIgnoreCase),
            Inventario      = global.Inventario      && allowed.Contains("Inventario",      StringComparer.OrdinalIgnoreCase),
            Produccion      = global.Produccion      && allowed.Contains("Produccion",      StringComparer.OrdinalIgnoreCase),
            Sgc             = global.Sgc             && allowed.Contains("Sgc",             StringComparer.OrdinalIgnoreCase),
            Facturacion     = global.Facturacion     && allowed.Contains("Facturacion",     StringComparer.OrdinalIgnoreCase),
            Ventas          = global.Ventas          && allowed.Contains("Ventas",          StringComparer.OrdinalIgnoreCase),
            RecursosHumanos = global.RecursosHumanos && allowed.Contains("RecursosHumanos", StringComparer.OrdinalIgnoreCase),
            Administracion  = global.Administracion  && allowed.Contains("Administracion",  StringComparer.OrdinalIgnoreCase),
        };
    }

    public (string controller, string action) GetLanding()
    {
        var menus = GetMenusActuales();

        if (menus.Dashboard)        return ("Home",                 "Index");
        if (menus.Produccion)       return ("RegistroPreparatoria", "Index");
        if (menus.Sgc)              return ("Sgc",                  "Index");
        if (menus.Facturacion)      return ("Facturacion",          "Index");
        if (menus.Ventas)           return ("Ventas",               "Index");
        if (menus.Inventario)       return ("Inventario",           "Index");
        if (menus.RecursosHumanos)  return ("RecursosHumanos",      "Index");
        return ("RegistroPreparatoria", "Index");
    }
}
