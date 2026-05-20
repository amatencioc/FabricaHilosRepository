using FabricaHilos.Config;
using FabricaHilos.Models;
using Microsoft.Extensions.Options;

namespace FabricaHilos.Services;

public interface IMenuService
{
    MenuOptions GetMenusActuales();
    (string? controller, string? action, string? area, string? url) GetLanding();

    /// <summary>
    /// Devuelve los modificadores/parámetros asociados a un módulo específico
    /// según el token de acceso almacenado en sesión.
    /// Ejemplo token Oracle: LogisticaOrdenCompra[noNuevaOC,estado=2]
    /// </summary>
    ModuloAcceso ObtenerAccesoModulo(string nombreModulo);
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

    // ?? Parser de tokens de acceso ?????????????????????????????????????????
    // Soporta la forma: NombreModulo[mod1,clave=valor,...]
    // Los tokens sin corchetes se tratan como nombre puro (sin modificadores).

    private static (string nombre, ModuloAcceso acceso) ParseToken(string raw)
    {
        var bracketStart = raw.IndexOf('[');
        if (bracketStart < 0)
            return (raw.Trim(), ModuloAcceso.SinRestricciones);

        var nombre  = raw[..bracketStart].Trim();
        var content = raw[(bracketStart + 1)..].TrimEnd(']').Trim();

        var modificadores = new List<string>();
        var parametros    = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var parte in content.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = parte.IndexOf('=');
            if (eq >= 0)
                parametros[parte[..eq].Trim()] = parte[(eq + 1)..].Trim();
            else
                modificadores.Add(parte);
        }

        return (nombre, new ModuloAcceso(modificadores, parametros));
    }

    private IReadOnlyList<(string nombre, ModuloAcceso acceso)> ObtenerTokens()
    {
        var session   = _httpContextAccessor.HttpContext?.Session;
        var accesoWeb = session?.GetString("AccesoWeb") ?? string.Empty;

        // Separar por coma respetando los corchetes: A[x=1,y],B ? ["A[x=1,y]", "B"]
        var result  = new List<(string, ModuloAcceso)>();
        int depth   = 0;
        var current = new System.Text.StringBuilder();
        foreach (var ch in accesoWeb)
        {
            if      (ch == '[') { depth++; current.Append(ch); }
            else if (ch == ']') { depth--; current.Append(ch); }
            else if (ch == ',' && depth == 0)
            {
                var raw = current.ToString().Trim();
                if (raw.Length > 0) result.Add(ParseToken(raw));
                current.Clear();
            }
            else current.Append(ch);
        }
        var last = current.ToString().Trim();
        if (last.Length > 0) result.Add(ParseToken(last));
        return result;
    }

    public ModuloAcceso ObtenerAccesoModulo(string nombreModulo)
    {
        foreach (var (nombre, acceso) in ObtenerTokens())
            if (string.Equals(nombre, nombreModulo, StringComparison.OrdinalIgnoreCase))
                return acceso;
        return ModuloAcceso.SinRestricciones;
    }

    public MenuOptions GetMenusActuales()
    {
        var global  = _globalMenus.Value;
        var tokens  = ObtenerTokens();
        var modulos = tokens.Select(t => t.nombre).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Admin tiene acceso a todo el menú según la configuración global
        if (modulos.Contains("Admin", StringComparer.OrdinalIgnoreCase))
            return global;

        // Tiene: token exacto del módulo o sub-módulo
        bool Tiene(string modulo) => modulos.Contains(modulo);

        // TieneAlguno: padre visible si tiene acceso al módulo completo O a cualquier sub-módulo específico
        bool TieneAlguno(params string[] tkns) => tkns.Any(Tiene);

        return new MenuOptions
        {
            // ?? Menús principales ?????????????????????????????????????????????
            Dashboard = global.Dashboard && TieneAlguno("Dashboard"),

            Produccion = TieneAlguno(
                "Produccion",
                "ProduccionRegistroPreparatoria",
                "ProduccionAutoconer",
                "ProduccionAutoconerPorPartida",
                "ProduccionAutoconerPorCanillas"),

            Sgc = TieneAlguno(
                "Sgc",
                "SgcPedidos",
                "SgcDespachos",
                "SgcDespachosRelacionFacCli",
                "SgcDespachosCargarTC"),

            Facturacion = TieneAlguno(
                "Facturacion",
                "FacturacionImportarFacturas",
                "FacturacionListaDocumentos"),

            Ventas = TieneAlguno(
                "Ventas",
                "VentasConsultaTC",
                "VentasIndicadorComercialMaestro",
                "VentasDashboardComercialMaestro",
                "VentasDashboardGerencial"),

            Seguridad = TieneAlguno(
                "Seguridad",
                "SeguridadInspecciones"),

            RecursosHumanos = TieneAlguno(
                "RecursosHumanos",
                "RhMarcaciones",
                "RhCompensacionDiaDia",
                "RhCompensacionDdc",
                "RhAutorizacionHoras",
                "RhIndicadores",
                "RhIndicadoresHorasExtras",
                "RhIndicadoresConcentracionSobretiempo",
                "RhIndicadoresEvolucionMasaSalarial"),

            Logistica = TieneAlguno(
                "Logistica",
                "LogisticaRequerimiento",
                "LogisticaOrdenCompra",
                "LogisticaIndicadores"),

            CreditosCobranza = TieneAlguno(
                "CreditosCobranza",
                "CcNivelMorosidad",
                "CcNivelTiempo"),

            Sistemas = TieneAlguno(
                "Sistemas",
                "SistemasIndicadores",
                "SistemasIndicadoresDesarrollo",
                "SistemasIndicadoresIncidencia",
                "SistemasIndicadoresSeguimientoDev",
                "SistemasRequerimientos",
                "SistemasRequerimientosAnularDocumento"),

            // ?? Sub-módulos: Producción ???????????????????????????????????????
            ProduccionRegistroPreparatoria = global.ProduccionRegistroPreparatoria
                && TieneAlguno("Produccion", "ProduccionRegistroPreparatoria"),

            // Sub-padre Autoconer visible si tiene también cualquier hijo suyo
            ProduccionAutoconer = global.ProduccionAutoconer
                && TieneAlguno("Produccion", "ProduccionAutoconer",
                               "ProduccionAutoconerPorPartida", "ProduccionAutoconerPorCanillas"),

            ProduccionAutoconerPorPartida = global.ProduccionAutoconerPorPartida
                && TieneAlguno("Produccion", "ProduccionAutoconer", "ProduccionAutoconerPorPartida"),

            ProduccionAutoconerPorCanillas = global.ProduccionAutoconerPorCanillas
                && TieneAlguno("Produccion", "ProduccionAutoconer", "ProduccionAutoconerPorCanillas"),

            // ?? Sub-módulos: SGC ??????????????????????????????????????????????
            SgcPedidos = global.SgcPedidos
                && TieneAlguno("Sgc", "SgcPedidos"),

            // Sub-padre Despachos visible si tiene también cualquier hijo suyo
            SgcDespachos = global.SgcDespachos
                && TieneAlguno("Sgc", "SgcDespachos",
                               "SgcDespachosRelacionFacCli", "SgcDespachosCargarTC"),

            SgcDespachosRelacionFacCli = global.SgcDespachosRelacionFacCli
                && TieneAlguno("Sgc", "SgcDespachos", "SgcDespachosRelacionFacCli"),

            SgcDespachosCargarTC = global.SgcDespachosCargarTC
                && TieneAlguno("Sgc", "SgcDespachos", "SgcDespachosCargarTC"),

            // ?? Sub-módulos: Facturación ??????????????????????????????????????
            FacturacionImportarFacturas = global.FacturacionImportarFacturas
                && TieneAlguno("Facturacion", "FacturacionImportarFacturas"),

            FacturacionListaDocumentos = global.FacturacionListaDocumentos
                && TieneAlguno("Facturacion", "FacturacionListaDocumentos"),

            // ?? Sub-módulos: Ventas ???????????????????????????????????????????
            VentasConsultaTC = global.VentasConsultaTC
                && TieneAlguno("Ventas", "VentasConsultaTC"),

            VentasIndicadorComercialMaestro = global.VentasIndicadorComercialMaestro
                && TieneAlguno("Ventas", "VentasIndicadorComercialMaestro"),

            VentasDashboardComercialMaestro = global.VentasDashboardComercialMaestro
                && TieneAlguno("Ventas", "VentasDashboardComercialMaestro"),

            VentasDashboardGerencial = global.VentasDashboardGerencial
                && TieneAlguno("Ventas", "VentasDashboardGerencial"),

            // ?? Sub-módulos: Seguridad ????????????????????????????????????????
            SeguridadInspecciones = global.SeguridadInspecciones
                && TieneAlguno("Seguridad", "SeguridadInspecciones"),

            // ?? Sub-módulos: Recursos Humanos ?????????????????????????????????
            RhMarcaciones = global.RhMarcaciones
                && TieneAlguno("RecursosHumanos", "RhMarcaciones"),

            RhCompensacionDiaDia = global.RhCompensacionDiaDia
                && TieneAlguno("RecursosHumanos", "RhCompensacionDiaDia"),

            RhCompensacionDdc = global.RhCompensacionDdc
                && TieneAlguno("RecursosHumanos", "RhCompensacionDdc"),

            RhAutorizacionHoras = global.RhAutorizacionHoras
                && TieneAlguno("RecursosHumanos", "RhAutorizacionHoras"),

            // Sub-padre RhIndicadores visible si tiene también cualquier hijo suyo
            RhIndicadores = global.RhIndicadores
                && TieneAlguno("RecursosHumanos", "RhIndicadores",
                               "RhIndicadoresHorasExtras",
                               "RhIndicadoresConcentracionSobretiempo",
                               "RhIndicadoresEvolucionMasaSalarial"),

            RhIndicadoresHorasExtras = global.RhIndicadoresHorasExtras
                && TieneAlguno("RecursosHumanos", "RhIndicadores", "RhIndicadoresHorasExtras"),

            RhIndicadoresConcentracionSobretiempo = global.RhIndicadoresConcentracionSobretiempo
                && TieneAlguno("RecursosHumanos", "RhIndicadores", "RhIndicadoresConcentracionSobretiempo"),

            RhIndicadoresEvolucionMasaSalarial = global.RhIndicadoresEvolucionMasaSalarial
                && TieneAlguno("RecursosHumanos", "RhIndicadores", "RhIndicadoresEvolucionMasaSalarial"),

            // ?? Sub-módulos: Logística ????????????????????????????????????????
            LogisticaRequerimiento = global.LogisticaRequerimiento
                && TieneAlguno("Logistica", "LogisticaRequerimiento"),

            LogisticaOrdenCompra = global.LogisticaOrdenCompra
                && TieneAlguno("Logistica", "LogisticaOrdenCompra"),

            LogisticaIndicadores = global.LogisticaIndicadores
                && TieneAlguno("Logistica", "LogisticaIndicadores"),

            // ?? Sub-módulos: Créditos y Cobranzas ????????????????????????????
            CcNivelMorosidad = global.CcNivelMorosidad
                && TieneAlguno("CreditosCobranza", "CcNivelMorosidad"),

            CcNivelTiempo = global.CcNivelTiempo
                && TieneAlguno("CreditosCobranza", "CcNivelTiempo"),

            // ?? Planeamiento ???????????????????????????????????????????????????
            Planeamiento = TieneAlguno(
                "Planeamiento",
                "PlaneamientoDashboard",
                "PlaneamientoPedido",
                "PlaneamientoCargaMaquinas",
                "PlaneamientoAlertas",
                "PlaneamientoKPIs",
                "PlaneamientoPendientesDespacho"),

            PlaneamientoDashboard = global.PlaneamientoDashboard
                && TieneAlguno("Planeamiento", "PlaneamientoDashboard"),

            PlaneamientoPedido = global.PlaneamientoPedido
                && TieneAlguno("Planeamiento", "PlaneamientoPedido"),

            PlaneamientoCargaMaquinas = global.PlaneamientoCargaMaquinas
                && TieneAlguno("Planeamiento", "PlaneamientoCargaMaquinas"),

            PlaneamientoAlertas = global.PlaneamientoAlertas
                && TieneAlguno("Planeamiento", "PlaneamientoAlertas"),

            PlaneamientoKPIs = global.PlaneamientoKPIs
                && TieneAlguno("Planeamiento", "PlaneamientoKPIs"),

            PlaneamientoPendientesDespacho = global.PlaneamientoPendientesDespacho
                && TieneAlguno("Planeamiento", "PlaneamientoPendientesDespacho"),

            // ?? Sub-m
            // Sub-padre SistemasIndicadores visible si tiene también cualquier hijo suyo
            SistemasIndicadores = global.SistemasIndicadores
                && TieneAlguno("Sistemas", "SistemasIndicadores",
                               "SistemasIndicadoresDesarrollo",
                               "SistemasIndicadoresIncidencia",
                               "SistemasIndicadoresSeguimientoDev"),

            SistemasIndicadoresDesarrollo = global.SistemasIndicadoresDesarrollo
                && TieneAlguno("Sistemas", "SistemasIndicadores", "SistemasIndicadoresDesarrollo"),

            SistemasIndicadoresIncidencia = global.SistemasIndicadoresIncidencia
                && TieneAlguno("Sistemas", "SistemasIndicadores", "SistemasIndicadoresIncidencia"),

            SistemasIndicadoresSeguimientoDev = global.SistemasIndicadoresSeguimientoDev
                && TieneAlguno("Sistemas", "SistemasIndicadores", "SistemasIndicadoresSeguimientoDev"),

            // Sub-padre SistemasRequerimientos visible si tiene también cualquier hijo suyo
            SistemasRequerimientos = global.SistemasRequerimientos
                && TieneAlguno("Sistemas", "SistemasRequerimientos",
                               "SistemasRequerimientosAnularDocumento"),

            SistemasRequerimientosAnularDocumento = global.SistemasRequerimientosAnularDocumento
                && TieneAlguno("Sistemas", "SistemasRequerimientos", "SistemasRequerimientosAnularDocumento"),
        };
    }

    public (string? controller, string? action, string? area, string? url) GetLanding()
    {
        var menus = GetMenusActuales();

        if (menus.Dashboard)        return ("Home",             "Index", null, null);
        if (menus.Produccion)       return ("Produccion",       "Index", null, null);
        if (menus.Sgc)              return ("Sgc",              "Index", null, null);
        if (menus.Facturacion)      return ("Facturacion",      "Index", null, null);
        if (menus.Ventas)           return ("Ventas",           "Index", null, null);
        if (menus.Seguridad)        return ("Inspeccion",       "Index", null, null);
        if (menus.RecursosHumanos)  return ("RecursosHumanos",  "Index", null, null);
        if (menus.Logistica)    return ("Logistica",        "Index", null, null);
        if (menus.CreditosCobranza) return ("CreditosCobranza", "Index", null, null);
        if (menus.Planeamiento)  return ("Planeamiento",     "Index", null, null);
        if (menus.Sistemas)         return ("Sistemas",          "Index", null, null);
        // Sin módulos asignados o AccesoWeb vacío: redirigir a login para evitar
        // aterrizar en un módulo al que el usuario no tiene acceso.
        return ("Account", "Login", null, null);
    }
}