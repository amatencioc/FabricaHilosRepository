using FabricaHilos.Config;
using FabricaHilos.Models;
using Microsoft.Extensions.Options;

namespace FabricaHilos.Services;

public interface IMenuService
{
    MenuOptions GetMenusActuales();
    /// <summary>
    /// Devuelve los menÃºs filtrados segÃºn tipo de acceso:
    /// si esExterno=true, solo incluye mÃ³dulos cuya ruta raÃ­z estÃ¡ en las rutas externas permitidas.
    /// </summary>
    MenuOptions GetMenusFiltradosPorRed(bool esExterno, IEnumerable<string> rutasExternasPermitidas);
    (string? controller, string? action, string? area, string? url) GetLanding();
    /// <summary>
    /// Landing inteligente: si esExterno=true, solo redirige a mÃ³dulos accesibles externamente.
    /// </summary>
    (string? controller, string? action, string? area, string? url) GetLandingParaRed(bool esExterno, IEnumerable<string> rutasExternasPermitidas);
    /// <summary>
    /// Devuelve los modificadores/parÃ¡metros asociados a un mÃ³dulo especÃ­fico
    /// segÃºn el token de acceso almacenado en sesiÃ³n.
    /// Ejemplo token Oracle: LogisticaOrdenCompra[noNuevaOC,estado=2]
    /// </summary>
    ModuloAcceso ObtenerAccesoModulo(string nombreModulo);
    /// <summary>
    /// Devuelve la ruta del primer mÃ³dulo disponible segÃºn los permisos del usuario.
    /// Retorna una tupla (controller, action, area, url) o la ruta de acceso denegado si no hay mÃ³dulos disponibles.
    /// </summary>
    (string? controller, string? action, string? area, string? url) GetFirstAvailableModule();
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

        // Admin tiene acceso a todo el menï¿½ segï¿½n la configuraciï¿½n global
        if (modulos.Contains("Admin", StringComparer.OrdinalIgnoreCase))
            return global;

        // Tiene: token exacto del mï¿½dulo o sub-mï¿½dulo
        bool Tiene(string modulo) => modulos.Contains(modulo);

        // TieneAlguno: padre visible si tiene acceso al mï¿½dulo completo O a cualquier sub-mï¿½dulo especï¿½fico
        bool TieneAlguno(params string[] tkns) => tkns.Any(Tiene);

        return new MenuOptions
        {
            // ?? Menï¿½s principales ?????????????????????????????????????????????
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
                "SgcDespachosCargarTC",
                "SgcAnalisisReclamo"),

            Facturacion = TieneAlguno(
                "Facturacion",
                "FacturacionImportarFacturas",
                "FacturacionListaDocumentos"),

            SireFlag = global.SireFlag && TieneAlguno("Sire", "SireFlag"),

            Ventas = TieneAlguno(
                "Ventas",
                "VentasConsultaTC",
                "VentasCotizacion",
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
                "RhPlanillaMensual",
                "RhIndicadores",
                "RhIndicadoresHorasExtras",
                "RhIndicadoresCostoSalarialHorasExtras",
                "RhIndicadoresComparativoCostoLaboral",
                // CapacitaciÃ³n es sub-mÃ³dulo de Recursos Humanos
                "Capacitacion", "CapacitacionCatalogo", "CapacitacionMisCursos", "CapacitacionAdmin"),

            Logistica = TieneAlguno(
                "Logistica",
                "LogisticaRequerimiento",
                "LogisticaOrdenCompra",
                "LogisticaIndicadores"),

            CreditosCobranza = TieneAlguno(
                "CreditosCobranza",
                "CcNivelMorosidad",
                "CcNivelTiempo"),

            SaludOcupacional = TieneAlguno(
                "SaludOcupacional",
                "SoInspeccionComedor"),

            SoInspeccionComedor = global.SoInspeccionComedor
                && TieneAlguno("SaludOcupacional", "SoInspeccionComedor"),

            Contabilidad = TieneAlguno(
                "Contabilidad",
                "ContabilidadSire",
                "ContabilidadActivoFijo"),

            Sistemas = TieneAlguno(
                "Sistemas",
                "SistemasIndicadores",
                "SistemasIndicadoresDesarrollo",
                "SistemasIndicadoresIncidencia",
                "SistemasIndicadoresSeguimientoDev",
                "SistemasRequerimientos",
                "SistemasRequerimientosAnularDocumento",
                "SistemasMonitorUsuarios"),

            // ?? Sub-mï¿½dulos: Producciï¿½n ???????????????????????????????????????
            ProduccionRegistroPreparatoria = global.ProduccionRegistroPreparatoria
                && TieneAlguno("Produccion", "ProduccionRegistroPreparatoria"),

            // Sub-padre Autoconer visible si tiene tambiï¿½n cualquier hijo suyo
            ProduccionAutoconer = global.ProduccionAutoconer
                && TieneAlguno("Produccion", "ProduccionAutoconer",
                               "ProduccionAutoconerPorPartida", "ProduccionAutoconerPorCanillas"),

            ProduccionAutoconerPorPartida = global.ProduccionAutoconerPorPartida
                && TieneAlguno("Produccion", "ProduccionAutoconer", "ProduccionAutoconerPorPartida"),

            ProduccionAutoconerPorCanillas = global.ProduccionAutoconerPorCanillas
                && TieneAlguno("Produccion", "ProduccionAutoconer", "ProduccionAutoconerPorCanillas"),

            // ?? Sub-mï¿½dulos: SGC ??????????????????????????????????????????????
            SgcPedidos = global.SgcPedidos
                && TieneAlguno("Sgc", "SgcPedidos"),

            // Sub-padre Despachos visible si tiene tambiï¿½n cualquier hijo suyo
            SgcDespachos = global.SgcDespachos
                && TieneAlguno("Sgc", "SgcDespachos",
                               "SgcDespachosRelacionFacCli", "SgcDespachosCargarTC"),

            SgcDespachosRelacionFacCli = global.SgcDespachosRelacionFacCli
                && TieneAlguno("Sgc", "SgcDespachos", "SgcDespachosRelacionFacCli"),

            SgcDespachosCargarTC = global.SgcDespachosCargarTC
                && TieneAlguno("Sgc", "SgcDespachos", "SgcDespachosCargarTC"),

            SgcAnalisisReclamo = global.SgcAnalisisReclamo
                && TieneAlguno("Sgc", "SgcAnalisisReclamo"),

            // ?? Sub-mï¿½dulos: Facturaciï¿½n ??????????????????????????????????????
            FacturacionImportarFacturas = global.FacturacionImportarFacturas
                && TieneAlguno("Facturacion", "FacturacionImportarFacturas"),

            FacturacionListaDocumentos = global.FacturacionListaDocumentos
                && TieneAlguno("Facturacion", "FacturacionListaDocumentos"),

            // ?? Sub-mï¿½dulos: Ventas ???????????????????????????????????????????
            VentasConsultaTC = global.VentasConsultaTC
                && TieneAlguno("Ventas", "VentasConsultaTC"),

            VentasCotizacion = global.VentasCotizacion
                && TieneAlguno("Ventas", "VentasCotizacion"),

            VentasIndicadorComercialMaestro = global.VentasIndicadorComercialMaestro
                && TieneAlguno("Ventas", "VentasIndicadorComercialMaestro"),

            VentasDashboardComercialMaestro = global.VentasDashboardComercialMaestro
                && TieneAlguno("Ventas", "VentasDashboardComercialMaestro"),

            VentasDashboardGerencial = global.VentasDashboardGerencial
                && TieneAlguno("Ventas", "VentasDashboardGerencial"),

            // ?? Sub-mï¿½dulos: Seguridad ????????????????????????????????????????
            SeguridadInspecciones = global.SeguridadInspecciones
                && TieneAlguno("Seguridad", "SeguridadInspecciones"),

            // ?? Sub-mï¿½dulos: Recursos Humanos ?????????????????????????????????
            RhMarcaciones = global.RhMarcaciones
                && TieneAlguno("RecursosHumanos", "RhMarcaciones"),

            RhCompensacionDiaDia = global.RhCompensacionDiaDia
                && TieneAlguno("RecursosHumanos", "RhCompensacionDiaDia"),

            RhCompensacionDdc = global.RhCompensacionDdc
                && TieneAlguno("RecursosHumanos", "RhCompensacionDdc"),

            RhAutorizacionHoras = global.RhAutorizacionHoras
                && TieneAlguno("RecursosHumanos", "RhAutorizacionHoras"),

            RhPlanillaMensual = global.RhPlanillaMensual
                && TieneAlguno("RecursosHumanos", "RhPlanillaMensual"),

            // Sub-padre RhIndicadores
            RhIndicadores = global.RhIndicadores
                && TieneAlguno("RecursosHumanos", "RhIndicadores",
                               "RhIndicadoresHorasExtras",
                               "RhIndicadoresCostoSalarialHorasExtras",
                               "RhIndicadoresComparativoCostoLaboral"),

            RhIndicadoresHorasExtras = global.RhIndicadoresHorasExtras
                && TieneAlguno("RecursosHumanos", "RhIndicadores", "RhIndicadoresHorasExtras"),

            RhIndicadoresCostoSalarialHorasExtras = global.RhIndicadoresCostoSalarialHorasExtras
                && TieneAlguno("RecursosHumanos", "RhIndicadores", "RhIndicadoresCostoSalarialHorasExtras"),

            RhIndicadoresComparativoCostoLaboral = global.RhIndicadoresComparativoCostoLaboral
                && TieneAlguno("RecursosHumanos", "RhIndicadores", "RhIndicadoresComparativoCostoLaboral"),

            // ?? Sub-mï¿½dulos: Logï¿½stica ????????????????????????????????????????
            LogisticaRequerimiento = global.LogisticaRequerimiento
                && TieneAlguno("Logistica", "LogisticaRequerimiento"),

            LogisticaOrdenCompra = global.LogisticaOrdenCompra
                && TieneAlguno("Logistica", "LogisticaOrdenCompra"),

            LogisticaIndicadores = global.LogisticaIndicadores
                && TieneAlguno("Logistica", "LogisticaIndicadores"),

            // ?? Sub-mï¿½dulos: Crï¿½ditos y Cobranzas ????????????????????????????
            CcNivelMorosidad = global.CcNivelMorosidad
                && TieneAlguno("CreditosCobranza", "CcNivelMorosidad"),

            CcNivelTiempo = global.CcNivelTiempo
                && TieneAlguno("CreditosCobranza", "CcNivelTiempo"),

            // ?? Sub-mï¿½dulos: Contabilidad ??????????????????????????????????????????
            ContabilidadSire = global.ContabilidadSire
                && TieneAlguno("Contabilidad", "ContabilidadSire"),

            ContabilidadActivoFijo = global.ContabilidadActivoFijo
                && TieneAlguno("Contabilidad", "ContabilidadActivoFijo"),

            // ?? Planeamiento
            Planeamiento = TieneAlguno(
                "Produccion",
                "Planeamiento",
                "PlaneamientoRegistroPedidos",
                "PlaneamientoDashboard",
                "PlaneamientoPedido",
                "PlaneamientoCargaMaquinas",
                "PlaneamientoAlertas",
                "PlaneamientoKPIs",
                "PlaneamientoPendientesDespacho",
                "PlaneamientoSeguimientoTintoreria",
                "PlaneamientoPendRevisado",
                "PlaneamientoPendEvalCalidad",
                "PlaneamientoPendEnconado",
                "PlaneamientoPendTenido",
                "PlaneamientoPendSecado",
                "PlaneamientoPendMadeja"),

            PlaneamientoRegistroPedidos = global.PlaneamientoRegistroPedidos
                && TieneAlguno("Produccion", "Planeamiento", "PlaneamientoRegistroPedidos"),

            PlaneamientoDashboard = global.PlaneamientoDashboard
                && TieneAlguno("Produccion", "Planeamiento", "PlaneamientoDashboard"),

            PlaneamientoPedido = global.PlaneamientoPedido
                && TieneAlguno("Produccion", "Planeamiento", "PlaneamientoPedido"),

            PlaneamientoCargaMaquinas = global.PlaneamientoCargaMaquinas
                && TieneAlguno("Produccion", "Planeamiento", "PlaneamientoCargaMaquinas"),

            PlaneamientoAlertas = global.PlaneamientoAlertas
                && TieneAlguno("Produccion", "Planeamiento", "PlaneamientoAlertas"),

            PlaneamientoProximosVencer = global.PlaneamientoProximosVencer
                && TieneAlguno("Produccion", "Planeamiento", "PlaneamientoProximosVencer"),

            PlaneamientoSeguimientoTintoreria = global.PlaneamientoSeguimientoTintoreria
                && TieneAlguno("Produccion", "Planeamiento", "PlaneamientoSeguimientoTintoreria"),

            PlaneamientoKPIs = global.PlaneamientoKPIs
                && TieneAlguno("Produccion", "Planeamiento", "PlaneamientoKPIs"),

            PlaneamientoPendientesDespacho = global.PlaneamientoPendientesDespacho
                && TieneAlguno("Produccion", "Planeamiento", "PlaneamientoPendientesDespacho"),

            PlaneamientoPendRevisado = global.PlaneamientoPendRevisado
                && TieneAlguno("Produccion", "Planeamiento", "PlaneamientoPendRevisado"),

            PlaneamientoPendEvalCalidad = global.PlaneamientoPendEvalCalidad
                && TieneAlguno("Produccion", "Planeamiento", "PlaneamientoPendEvalCalidad"),

            PlaneamientoPendEnconado = global.PlaneamientoPendEnconado
                && TieneAlguno("Produccion", "Planeamiento", "PlaneamientoPendEnconado"),

            PlaneamientoPendTenido = global.PlaneamientoPendTenido
                && TieneAlguno("Produccion", "Planeamiento", "PlaneamientoPendTenido"),

            PlaneamientoPendSecado = global.PlaneamientoPendSecado
                && TieneAlguno("Produccion", "Planeamiento", "PlaneamientoPendSecado"),

            PlaneamientoPendMadeja = global.PlaneamientoPendMadeja
                && TieneAlguno("Produccion", "Planeamiento", "PlaneamientoPendMadeja"),

            // ?? Sub-m
            // Sub-padre SistemasIndicadores visible si tiene tambiï¿½n cualquier hijo suyo
            SistemasIndicadores = global.SistemasIndicadores
                && TieneAlguno("Sistemas", "SistemasIndicadores",
                               "SistemasIndicadoresDesarrollo",
                               "SistemasIndicadoresIncidencia",
                               "SistemasIndicadoresSeguimientoDev"),

            SistemasIndicadoresDesarrollo = global.SistemasIndicadoresDesarrollo
                && TieneAlguno("Sistemas", "SistemasIndicadores", "SistemasIndicadoresDesarrollo"),

            SistemasIndicadoresDesarrolloComplejidad = global.SistemasIndicadoresDesarrolloComplejidad
                && TieneAlguno("Sistemas", "SistemasIndicadores", "SistemasIndicadoresDesarrolloComplejidad"),

            SistemasIndicadoresIncidencia = global.SistemasIndicadoresIncidencia
                && TieneAlguno("Sistemas", "SistemasIndicadores", "SistemasIndicadoresIncidencia"),

            SistemasIndicadoresSeguimientoDev = global.SistemasIndicadoresSeguimientoDev
                && TieneAlguno("Sistemas", "SistemasIndicadores", "SistemasIndicadoresSeguimientoDev"),

            // Sub-padre SistemasRequerimientos visible si tiene tambiï¿½n cualquier hijo suyo
            SistemasRequerimientos = global.SistemasRequerimientos
                && TieneAlguno("Sistemas", "SistemasRequerimientos",
                               "SistemasRequerimientosAnularDocumento"),

            SistemasRequerimientosAnularDocumento = global.SistemasRequerimientosAnularDocumento
                && TieneAlguno("Sistemas", "SistemasRequerimientos", "SistemasRequerimientosAnularDocumento"),


            SistemasMonitorUsuarios = global.SistemasMonitorUsuarios
                && TieneAlguno("Sistemas", "SistemasMonitorUsuarios"),
            // â”€â”€ CapacitaciÃ³n (LMS) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            Capacitacion = global.Capacitacion
                && TieneAlguno("Capacitacion", "CapacitacionCatalogo",
                               "CapacitacionMisCursos", "CapacitacionAdmin"),

            CapacitacionCatalogo = global.CapacitacionCatalogo
                && TieneAlguno("Capacitacion", "CapacitacionCatalogo", "CapacitacionAdmin"),

            CapacitacionMisCursos = global.CapacitacionMisCursos
                && TieneAlguno("Capacitacion", "CapacitacionMisCursos", "CapacitacionAdmin"),

            CapacitacionAdmin = global.CapacitacionAdmin
                && TieneAlguno("CapacitacionAdmin"),
        };
    }

    public (string? controller, string? action, string? area, string? url) GetLanding()
    {
        var menus = GetMenusActuales();

        if (menus.Dashboard)        return ("Home",             "Index", null, null);

        // Si no tiene Dashboard, retorna el primer mÃ³dulo disponible segÃºn permiso
        return GetFirstAvailableModule();
    }

    // â”€â”€ Mapa mÃ³dulo â†’ ruta raÃ­z (debe coincidir con RedInterna:RutasExternasPermitidas) â”€â”€â”€â”€â”€â”€
    // Clave: ruta raÃ­z en minÃºsculas.  Valor: (controller, action, parent-flag-getter)
    private static readonly (string ruta, Func<MenuOptions, bool> tieneAcceso,
                              string ctrl, string action)[] _moduloRutaMap =
    [
        ("/produccion",        m => m.Produccion,       "Produccion",       "Index"),
        ("/registropreparatoria", m => m.ProduccionRegistroPreparatoria, "RegistroPreparatoria", "Index"),
        ("/autoconer",         m => m.ProduccionAutoconer, "Autoconer",     "Index"),
        ("/seguridad",         m => m.Seguridad,         "Inspeccion",      "Index"),
        ("/saludocupacional",  m => m.SaludOcupacional,  "InspeccionCom",   "Dashboard"),
        ("/logistica/ordencompra", m => m.LogisticaOrdenCompra, "OrdenCompra", "Index"),
        ("/account/login",     _ => true,                "Account",         "Login"),
        ("/account/logout",    _ => true,                "Account",         "Logout"),
    ];

    public MenuOptions GetMenusFiltradosPorRed(bool esExterno, IEnumerable<string> rutasExternasPermitidas)
    {
        if (!esExterno) return GetMenusActuales();

        var menus  = GetMenusActuales();
        var rutas  = rutasExternasPermitidas
            .Select(r => r.ToLowerInvariant())
            .ToHashSet();

        // ModuloVisible O(1): precalcula prefijos una sola vez para todas las llamadas.
        // "/a/b/c" genera prefijos ["/a", "/a/b", "/a/b/c"].
        // Visible si la ruta esta exactamente en ese set (hoja o padre con ruta propia).
        // Padre sin ruta exacta en appsettings => no esta en prefijos => no visible.
        var prefijos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var ruta in rutas)
        {
            var acum = string.Empty;
            foreach (var seg in ruta.Split("/", StringSplitOptions.RemoveEmptyEntries))
            {
                acum += "/" + seg;
                prefijos.Add(acum);
            }
        }

        bool ModuloVisible(string rutaRaiz, bool flagActual)
            => flagActual && prefijos.Contains(rutaRaiz);



        return new MenuOptions
        {
            Dashboard = false,   // /Home/Index no estÃ¡ en rutas externas

            Produccion                    = ModuloVisible("/produccion",           menus.Produccion),
            ProduccionRegistroPreparatoria= ModuloVisible("/registropreparatoria", menus.ProduccionRegistroPreparatoria),
            ProduccionAutoconer           = ModuloVisible("/autoconer",            menus.ProduccionAutoconer),
            ProduccionAutoconerPorPartida = ModuloVisible("/autoconer",            menus.ProduccionAutoconerPorPartida),
            ProduccionAutoconerPorCanillas= ModuloVisible("/autoconer",            menus.ProduccionAutoconerPorCanillas),

            Sgc                        = ModuloVisible("/sgc",              menus.Sgc),
            SgcPedidos                 = ModuloVisible("/sgc",              menus.SgcPedidos),
            SgcDespachos               = ModuloVisible("/sgc",              menus.SgcDespachos),
            SgcDespachosRelacionFacCli = ModuloVisible("/sgc",              menus.SgcDespachosRelacionFacCli),
            SgcDespachosCargarTC       = ModuloVisible("/sgc",              menus.SgcDespachosCargarTC),
            SgcAnalisisReclamo         = ModuloVisible("/sgc",              menus.SgcAnalisisReclamo),

            Facturacion                = ModuloVisible("/facturacion",      menus.Facturacion),
            FacturacionImportarFacturas= ModuloVisible("/facturacion",      menus.FacturacionImportarFacturas),
            FacturacionListaDocumentos = ModuloVisible("/facturacion",      menus.FacturacionListaDocumentos),

            Ventas                         = ModuloVisible("/ventas",       menus.Ventas),
            VentasConsultaTC               = ModuloVisible("/ventas",       menus.VentasConsultaTC),
            VentasIndicadorComercialMaestro= ModuloVisible("/ventas",       menus.VentasIndicadorComercialMaestro),
            VentasDashboardComercialMaestro= ModuloVisible("/ventas",       menus.VentasDashboardComercialMaestro),
            VentasDashboardGerencial       = ModuloVisible("/ventas",       menus.VentasDashboardGerencial),

            Seguridad             = ModuloVisible("/seguridad",             menus.Seguridad),
            SeguridadInspecciones = ModuloVisible("/seguridad",             menus.SeguridadInspecciones),

            RecursosHumanos                       = ModuloVisible("/recursoshumanos", menus.RecursosHumanos),
            RhMarcaciones                         = ModuloVisible("/recursoshumanos/aquarius/marcaciones",       menus.RhMarcaciones),
            RhCompensacionDiaDia                  = ModuloVisible("/recursoshumanos/aquarius/compensaciondiadia", menus.RhCompensacionDiaDia),
            RhCompensacionDdc                     = ModuloVisible("/recursoshumanos/aquarius/compensacionddc",    menus.RhCompensacionDdc),
            RhAutorizacionHoras                   = ModuloVisible("/recursoshumanos/aquarius/authhoras",          menus.RhAutorizacionHoras),
            RhPlanillaMensual                     = ModuloVisible("/recursoshumanos/aquarius/planillamensual",    menus.RhPlanillaMensual),
            RhIndicadores                         = menus.RhIndicadores && (
                ModuloVisible("/recursoshumanos/horasextras",                true) ||
                ModuloVisible("/recursoshumanos/costosalarialhorasextras",    true) ||
                ModuloVisible("/recursoshumanos/comparativocostolaboral",     true)),
            RhIndicadoresHorasExtras              = ModuloVisible("/recursoshumanos/horasextras",                menus.RhIndicadoresHorasExtras),
            RhIndicadoresCostoSalarialHorasExtras = ModuloVisible("/recursoshumanos/costosalarialhorasextras",   menus.RhIndicadoresCostoSalarialHorasExtras),
            RhIndicadoresComparativoCostoLaboral  = ModuloVisible("/recursoshumanos/comparativocostolaboral",    menus.RhIndicadoresComparativoCostoLaboral),

            Logistica             = ModuloVisible("/logistica",             menus.Logistica),
            LogisticaRequerimiento= ModuloVisible("/logistica/requerimiento", menus.LogisticaRequerimiento),
            LogisticaOrdenCompra  = ModuloVisible("/logistica/ordencompra",  menus.LogisticaOrdenCompra),
            LogisticaIndicadores  = ModuloVisible("/logistica/indicadores",  menus.LogisticaIndicadores),

            CreditosCobranza = ModuloVisible("/creditoscobranza",           menus.CreditosCobranza),
            CcNivelMorosidad = ModuloVisible("/creditoscobranza",           menus.CcNivelMorosidad),
            CcNivelTiempo    = ModuloVisible("/creditoscobranza",           menus.CcNivelTiempo),

            SaludOcupacional  = ModuloVisible("/saludocupacional",          menus.SaludOcupacional),
            SoInspeccionComedor = ModuloVisible("/saludocupacional",        menus.SoInspeccionComedor),

            Contabilidad     = ModuloVisible("/contabilidad",               menus.Contabilidad),
            ContabilidadSire       = ModuloVisible("/sire",                    menus.ContabilidadSire),
            ContabilidadActivoFijo = ModuloVisible("/contabilidad/activofijo", menus.ContabilidadActivoFijo),
            SireFlag         = ModuloVisible("/sire",                       menus.SireFlag),

            Planeamiento                    = ModuloVisible("/planeamiento", menus.Planeamiento),
            PlaneamientoRegistroPedidos     = ModuloVisible("/planeamiento", menus.PlaneamientoRegistroPedidos),
            PlaneamientoDashboard           = ModuloVisible("/planeamiento", menus.PlaneamientoDashboard),
            PlaneamientoPedido              = ModuloVisible("/planeamiento", menus.PlaneamientoPedido),
            PlaneamientoCargaMaquinas       = ModuloVisible("/planeamiento", menus.PlaneamientoCargaMaquinas),
            PlaneamientoAlertas             = ModuloVisible("/planeamiento", menus.PlaneamientoAlertas),
            PlaneamientoProximosVencer      = ModuloVisible("/planeamiento", menus.PlaneamientoProximosVencer),
            PlaneamientoKPIs                = ModuloVisible("/planeamiento", menus.PlaneamientoKPIs),
            PlaneamientoSeguimientoTintoreria= ModuloVisible("/planeamiento", menus.PlaneamientoSeguimientoTintoreria),
            PlaneamientoPendientesDespacho  = ModuloVisible("/planeamiento", menus.PlaneamientoPendientesDespacho),
            PlaneamientoPendRevisado        = ModuloVisible("/planeamiento", menus.PlaneamientoPendRevisado),
            PlaneamientoPendEvalCalidad     = ModuloVisible("/planeamiento", menus.PlaneamientoPendEvalCalidad),
            PlaneamientoPendEnconado        = ModuloVisible("/planeamiento", menus.PlaneamientoPendEnconado),
            PlaneamientoPendTenido          = ModuloVisible("/planeamiento", menus.PlaneamientoPendTenido),
            PlaneamientoPendSecado          = ModuloVisible("/planeamiento", menus.PlaneamientoPendSecado),
            PlaneamientoPendMadeja          = ModuloVisible("/planeamiento", menus.PlaneamientoPendMadeja),

            Sistemas                              = ModuloVisible("/sistemas", menus.Sistemas),
            SistemasIndicadores                   = ModuloVisible("/sistemas", menus.SistemasIndicadores),
            SistemasIndicadoresDesarrollo         = ModuloVisible("/sistemas", menus.SistemasIndicadoresDesarrollo),
            SistemasIndicadoresDesarrolloComplejidad = ModuloVisible("/sistemas", menus.SistemasIndicadoresDesarrolloComplejidad),
            SistemasIndicadoresIncidencia         = ModuloVisible("/sistemas", menus.SistemasIndicadoresIncidencia),
            SistemasIndicadoresSeguimientoDev     = ModuloVisible("/sistemas", menus.SistemasIndicadoresSeguimientoDev),
            SistemasRequerimientos                = ModuloVisible("/sistemas", menus.SistemasRequerimientos),
            SistemasRequerimientosAnularDocumento = ModuloVisible("/sistemas", menus.SistemasRequerimientosAnularDocumento),

            Capacitacion          = ModuloVisible("/recursoshumanos/capacitacion", menus.Capacitacion),
            CapacitacionCatalogo  = ModuloVisible("/recursoshumanos/capacitacion", menus.CapacitacionCatalogo),
            CapacitacionMisCursos = ModuloVisible("/recursoshumanos/capacitacion", menus.CapacitacionMisCursos),
            CapacitacionAdmin     = ModuloVisible("/recursoshumanos/capacitacion", menus.CapacitacionAdmin),
        };
    }

    public (string? controller, string? action, string? area, string? url) GetLandingParaRed(
        bool esExterno, IEnumerable<string> rutasExternasPermitidas)
    {
        if (!esExterno) return GetLanding();

        var menus = GetMenusFiltradosPorRed(esExterno, rutasExternasPermitidas);

        // Acceso externo/movil: ir al primer SUBMODULO disponible, no al Index del modulo padre.
        // El Index de un modulo padre solo muestra cards de navegacion (un tap extra innecesario en movil).

        // Produccion
        if (menus.ProduccionRegistroPreparatoria) return ("RegistroPreparatoria", "Index",      null, null);
        if (menus.ProduccionAutoconerPorPartida)  return ("Autoconer",            "PorPartida", null, null);
        if (menus.ProduccionAutoconerPorCanillas) return ("Autoconer",            "PorCanillas",null, null);
        if (menus.ProduccionAutoconer)            return ("Autoconer",            "Index",      null, null);
        if (menus.Produccion)                     return ("Produccion",           "Index",      null, null);

        // SGC
        if (menus.Sgc) return ("Sgc", "Index", null, null);

        // Facturacion
        if (menus.FacturacionListaDocumentos)  return ("Facturacion", "ListaDocumentos",  null, null);
        if (menus.FacturacionImportarFacturas) return ("Facturacion", "ImportarFacturas", null, null);
        if (menus.Facturacion)                 return ("Facturacion", "Index",            null, null);

        // SIRE
        if (menus.SireFlag) return ("Sire", "Index", null, null);

        // Ventas
        if (menus.VentasDashboardGerencial)        return ("Ventas", "DashboardGerencial",        null, null);
        if (menus.VentasDashboardComercialMaestro) return ("Ventas", "DashboardComercialMaestro", null, null);
        if (menus.VentasIndicadorComercialMaestro) return ("Ventas", "IndicadorComercialMaestro", null, null);
        if (menus.VentasConsultaTC)                return ("Ventas", "ConsultaTC",                null, null);
        if (menus.Ventas)                          return ("Ventas", "Index",                     null, null);

        // Seguridad
        if (menus.SeguridadInspecciones) return ("Inspeccion", "Index", null, null);
        if (menus.Seguridad)             return ("Inspeccion", "Index", null, null);

        // Recursos Humanos
        if (menus.RhMarcaciones)                         return ("Aquarius",                 "Marcaciones",        null, null);
        if (menus.RhCompensacionDiaDia)                  return ("Aquarius",                 "CompensacionDiaDia", null, null);
        if (menus.RhCompensacionDdc)                     return ("Aquarius",                 "CompensacionDdc",    null, null);
        if (menus.RhAutorizacionHoras)                   return ("Aquarius",                 "AuthHoras",          null, null);
        if (menus.RhPlanillaMensual)                      return ("PlanillaMensual",            "Dashboard",            null, null);
        if (menus.RhIndicadoresHorasExtras)
        if (menus.RhIndicadoresCostoSalarialHorasExtras) return ("CostoSalarialHorasExtras", "Index",              null, null);
        if (menus.RhIndicadoresComparativoCostoLaboral)  return ("ComparativoCostoLaboral",  "Index",              null, null);
        if (menus.CapacitacionCatalogo)                  return ("Capacitacion",             "Catalogo",           null, null);
        if (menus.CapacitacionMisCursos)                 return ("Capacitacion",             "MisCursos",          null, null);
        if (menus.CapacitacionAdmin)                     return ("Capacitacion",             "Admin",              null, null);
        if (menus.RecursosHumanos)                       return ("RecursosHumanos",          "Index",              null, null);

        // Logistica
        if (menus.LogisticaOrdenCompra)   return ("Logistica", "ItemsReq",    null, null);
        if (menus.LogisticaRequerimiento) return ("Logistica", "Index",       null, null);
        if (menus.LogisticaIndicadores)   return ("Logistica", "Indicadores", null, null);
        if (menus.Logistica)              return ("Logistica", "Index",       null, null);

        // Creditos y Cobranza
        if (menus.CcNivelMorosidad) return ("CreditosCobranza", "NivelMorosidad", null, null);
        if (menus.CcNivelTiempo)    return ("CreditosCobranza", "NivelTiempo",    null, null);
        if (menus.CreditosCobranza) return ("CreditosCobranza", "Index",          null, null);

        // Salud Ocupacional
        if (menus.SoInspeccionComedor) return ("InspeccionCom", "Dashboard", null, null);
        if (menus.SaludOcupacional)    return ("InspeccionCom", "Dashboard", null, null);

        // Contabilidad
        if (menus.ContabilidadActivoFijo) return ("ActivoFijo",   "Index", null, null);
        if (menus.ContabilidadSire)       return ("Sire",         "Index", null, null);
        if (menus.Contabilidad)           return ("Contabilidad", "Index", null, null);

        // Planeamiento
        if (menus.Planeamiento)
        {
            if (menus.PlaneamientoPendTenido)           return ("Planeamiento", "PendientesTenido",      null, null);
            if (menus.PlaneamientoPendSecado)           return ("Planeamiento", "PendientesSecado",      null, null);
            if (menus.PlaneamientoPendMadeja)           return ("Planeamiento", "PendientesMadeja",      null, null);
            if (menus.PlaneamientoPendEnconado)         return ("Planeamiento", "PendientesEnconado",    null, null);
            if (menus.PlaneamientoPendEvalCalidad)      return ("Planeamiento", "PendientesEvalCalidad", null, null);
            if (menus.PlaneamientoPendRevisado)         return ("Planeamiento", "PendientesRevisado",    null, null);
            if (menus.PlaneamientoPendientesDespacho)   return ("Planeamiento", "PendientesDespacho",    null, null);
            if (menus.PlaneamientoDashboard)            return ("Planeamiento", "Dashboard",             null, null);
            if (menus.PlaneamientoSeguimientoTintoreria) return ("Planeamiento", "SeguimientoTintoreria", null, null);
            if (menus.PlaneamientoAlertas)              return ("Planeamiento", "Alertas",               null, null);
            if (menus.PlaneamientoProximosVencer)       return ("Planeamiento", "ProximosVencer",        null, null);
            if (menus.PlaneamientoCargaMaquinas)        return ("Planeamiento", "CargaMaquinas",         null, null);
            if (menus.PlaneamientoRegistroPedidos)      return ("Planeamiento", "RegistroPedido",        null, null);
            return ("Planeamiento", "Dashboard", null, null);
        }

        // Sistemas
        if (menus.Sistemas) return ("Sistemas", "Index", null, null);

        return ("Account", "AccesoDenegado", null, null);
    }

    public (string? controller, string? action, string? area, string? url) GetFirstAvailableModule()
    {
        var menus = GetMenusActuales();

        // EvalÃºa los mÃ³dulos en el mismo orden que GetLanding()
        // Dashboard no se incluye aquÃ­ ya que es un mÃ³dulo especial que puede estar deshabilitado
        if (menus.Produccion)       return ("Produccion",       "Index", null, null);
        if (menus.Sgc)              return ("Sgc",              "Index", null, null);
        if (menus.Facturacion)      return ("Facturacion",      "Index", null, null);
        if (menus.SireFlag)         return ("Sire",             "Index", null, null);
        if (menus.Ventas)           return ("Ventas",           "Index", null, null);
        if (menus.Seguridad)        return ("Inspeccion",       "Index", null, null);
        if (menus.RecursosHumanos)  return ("RecursosHumanos",  "Index", null, null);
        if (menus.Logistica)        return ("Logistica",        "Index", null, null);
        if (menus.CreditosCobranza) return ("CreditosCobranza", "Index", null, null);
        if (menus.SaludOcupacional) return ("InspeccionCom",    "Dashboard", null, null);
        if (menus.Contabilidad)     return ("Contabilidad",     "Index", null, null);

        // Para Planeamiento: verificar sub-vistas disponibles en lugar de Dashboard
        if (menus.Planeamiento)
        {
            // Retornar la primera sub-vista disponible
            if (menus.PlaneamientoPendTenido)        return ("Planeamiento", "PendientesTenido", null, null);
            if (menus.PlaneamientoPendSecado)        return ("Planeamiento", "PendientesSecado", null, null);
            if (menus.PlaneamientoPendMadeja)        return ("Planeamiento", "PendientesMadeja", null, null);
            if (menus.PlaneamientoPendEnconado)      return ("Planeamiento", "PendientesEnconado", null, null);
            if (menus.PlaneamientoPendEvalCalidad)   return ("Planeamiento", "PendientesEvalCalidad", null, null);
            if (menus.PlaneamientoPendRevisado)      return ("Planeamiento", "PendientesRevisado", null, null);
            if (menus.PlaneamientoPendientesDespacho) return ("Planeamiento", "PendientesDespacho", null, null);
            if (menus.PlaneamientoDashboard)         return ("Planeamiento", "Dashboard", null, null);
            if (menus.PlaneamientoSeguimientoTintoreria) return ("Planeamiento", "SeguimientoTintoreria", null, null);
            if (menus.PlaneamientoAlertas)           return ("Planeamiento", "Alertas", null, null);
            if (menus.PlaneamientoProximosVencer)    return ("Planeamiento", "ProximosVencer", null, null);
            if (menus.PlaneamientoCargaMaquinas)     return ("Planeamiento", "CargaMaquinas", null, null);
            if (menus.PlaneamientoRegistroPedidos)   return ("Planeamiento", "RegistroPedido", null, null);
            // Si solo tiene acceso a Planeamiento sin sub-vistas especÃ­ficas, ir al Dashboard
            return ("Planeamiento", "Dashboard", null, null);
        }
        if (menus.Sistemas)         return ("Sistemas",         "Index",   null, null);

        // Si no tiene otros mÃ³dulos, intenta Dashboard como Ãºltimo recurso
        if (menus.Dashboard)        return ("Home",             "Index", null, null);

        // Sin mÃ³dulos asignados: AccesoDenegado
        return ("Account", "AccesoDenegado", null, null);
    }
}

