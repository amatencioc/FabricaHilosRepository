using FabricaHilos.Config;
using FabricaHilos.Models;
using Microsoft.Extensions.Options;

namespace FabricaHilos.Services;

public interface IMenuService
{
    MenuOptions GetMenusActuales();
    /// <summary>
    /// Devuelve los menús filtrados según tipo de acceso:
    /// si esExterno=true, solo incluye módulos cuya ruta raíz está en las rutas externas permitidas.
    /// </summary>
    MenuOptions GetMenusFiltradosPorRed(bool esExterno, IEnumerable<string> rutasExternasPermitidas);
    (string? controller, string? action, string? area, string? url) GetLanding();
    /// <summary>
    /// Landing inteligente: si esExterno=true, solo redirige a módulos accesibles externamente.
    /// </summary>
    (string? controller, string? action, string? area, string? url) GetLandingParaRed(bool esExterno, IEnumerable<string> rutasExternasPermitidas);
    /// <summary>
    /// Devuelve los modificadores/parámetros asociados a un módulo específico
    /// según el token de acceso almacenado en sesión.
    /// Ejemplo token Oracle: LogisticaOrdenCompra[noNuevaOC,estado=2]
    /// </summary>
    ModuloAcceso ObtenerAccesoModulo(string nombreModulo);
    /// <summary>
    /// Devuelve la ruta del primer módulo disponible según los permisos del usuario.
    /// Retorna una tupla (controller, action, area, url) o la ruta de acceso denegado si no hay módulos disponibles.
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

        // Admin tiene acceso a todo el men� seg�n la configuraci�n global
        if (modulos.Contains("Admin", StringComparer.OrdinalIgnoreCase))
            return global;

        // Tiene: token exacto del m�dulo o sub-m�dulo
        bool Tiene(string modulo) => modulos.Contains(modulo);

        // TieneAlguno: padre visible si tiene acceso al m�dulo completo O a cualquier sub-m�dulo espec�fico
        bool TieneAlguno(params string[] tkns) => tkns.Any(Tiene);

        return new MenuOptions
        {
            // ?? Men�s principales ?????????????????????????????????????????????
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
                "RhIndicadoresCostoSalarialHorasExtras",
                "RhIndicadoresComparativoCostoLaboral"),

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
                "ContabilidadSire"),

            Sistemas = TieneAlguno(
                "Sistemas",
                "SistemasIndicadores",
                "SistemasIndicadoresDesarrollo",
                "SistemasIndicadoresIncidencia",
                "SistemasIndicadoresSeguimientoDev",
                "SistemasRequerimientos",
                "SistemasRequerimientosAnularDocumento"),

            // ?? Sub-m�dulos: Producci�n ???????????????????????????????????????
            ProduccionRegistroPreparatoria = global.ProduccionRegistroPreparatoria
                && TieneAlguno("Produccion", "ProduccionRegistroPreparatoria"),

            // Sub-padre Autoconer visible si tiene tambi�n cualquier hijo suyo
            ProduccionAutoconer = global.ProduccionAutoconer
                && TieneAlguno("Produccion", "ProduccionAutoconer",
                               "ProduccionAutoconerPorPartida", "ProduccionAutoconerPorCanillas"),

            ProduccionAutoconerPorPartida = global.ProduccionAutoconerPorPartida
                && TieneAlguno("Produccion", "ProduccionAutoconer", "ProduccionAutoconerPorPartida"),

            ProduccionAutoconerPorCanillas = global.ProduccionAutoconerPorCanillas
                && TieneAlguno("Produccion", "ProduccionAutoconer", "ProduccionAutoconerPorCanillas"),

            // ?? Sub-m�dulos: SGC ??????????????????????????????????????????????
            SgcPedidos = global.SgcPedidos
                && TieneAlguno("Sgc", "SgcPedidos"),

            // Sub-padre Despachos visible si tiene tambi�n cualquier hijo suyo
            SgcDespachos = global.SgcDespachos
                && TieneAlguno("Sgc", "SgcDespachos",
                               "SgcDespachosRelacionFacCli", "SgcDespachosCargarTC"),

            SgcDespachosRelacionFacCli = global.SgcDespachosRelacionFacCli
                && TieneAlguno("Sgc", "SgcDespachos", "SgcDespachosRelacionFacCli"),

            SgcDespachosCargarTC = global.SgcDespachosCargarTC
                && TieneAlguno("Sgc", "SgcDespachos", "SgcDespachosCargarTC"),

            SgcAnalisisReclamo = global.SgcAnalisisReclamo
                && TieneAlguno("Sgc", "SgcAnalisisReclamo"),

            // ?? Sub-m�dulos: Facturaci�n ??????????????????????????????????????
            FacturacionImportarFacturas = global.FacturacionImportarFacturas
                && TieneAlguno("Facturacion", "FacturacionImportarFacturas"),

            FacturacionListaDocumentos = global.FacturacionListaDocumentos
                && TieneAlguno("Facturacion", "FacturacionListaDocumentos"),

            // ?? Sub-m�dulos: Ventas ???????????????????????????????????????????
            VentasConsultaTC = global.VentasConsultaTC
                && TieneAlguno("Ventas", "VentasConsultaTC"),

            VentasIndicadorComercialMaestro = global.VentasIndicadorComercialMaestro
                && TieneAlguno("Ventas", "VentasIndicadorComercialMaestro"),

            VentasDashboardComercialMaestro = global.VentasDashboardComercialMaestro
                && TieneAlguno("Ventas", "VentasDashboardComercialMaestro"),

            VentasDashboardGerencial = global.VentasDashboardGerencial
                && TieneAlguno("Ventas", "VentasDashboardGerencial"),

            // ?? Sub-m�dulos: Seguridad ????????????????????????????????????????
            SeguridadInspecciones = global.SeguridadInspecciones
                && TieneAlguno("Seguridad", "SeguridadInspecciones"),

            // ?? Sub-m�dulos: Recursos Humanos ?????????????????????????????????
            RhMarcaciones = global.RhMarcaciones
                && TieneAlguno("RecursosHumanos", "RhMarcaciones"),

            RhCompensacionDiaDia = global.RhCompensacionDiaDia
                && TieneAlguno("RecursosHumanos", "RhCompensacionDiaDia"),

            RhCompensacionDdc = global.RhCompensacionDdc
                && TieneAlguno("RecursosHumanos", "RhCompensacionDdc"),

            RhAutorizacionHoras = global.RhAutorizacionHoras
                && TieneAlguno("RecursosHumanos", "RhAutorizacionHoras"),

            // Sub-padre RhIndicadores visible si tiene tambi�n cualquier hijo suyo
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

            // ?? Sub-m�dulos: Log�stica ????????????????????????????????????????
            LogisticaRequerimiento = global.LogisticaRequerimiento
                && TieneAlguno("Logistica", "LogisticaRequerimiento"),

            LogisticaOrdenCompra = global.LogisticaOrdenCompra
                && TieneAlguno("Logistica", "LogisticaOrdenCompra"),

            LogisticaIndicadores = global.LogisticaIndicadores
                && TieneAlguno("Logistica", "LogisticaIndicadores"),

            // ?? Sub-m�dulos: Cr�ditos y Cobranzas ????????????????????????????
            CcNivelMorosidad = global.CcNivelMorosidad
                && TieneAlguno("CreditosCobranza", "CcNivelMorosidad"),

            CcNivelTiempo = global.CcNivelTiempo
                && TieneAlguno("CreditosCobranza", "CcNivelTiempo"),

            // ?? Sub-m�dulos: Contabilidad ??????????????????????????????????????????
            ContabilidadSire = global.ContabilidadSire
                && TieneAlguno("Contabilidad", "ContabilidadSire"),

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
            // Sub-padre SistemasIndicadores visible si tiene tambi�n cualquier hijo suyo
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

            // Sub-padre SistemasRequerimientos visible si tiene tambi�n cualquier hijo suyo
            SistemasRequerimientos = global.SistemasRequerimientos
                && TieneAlguno("Sistemas", "SistemasRequerimientos",
                               "SistemasRequerimientosAnularDocumento"),

            SistemasRequerimientosAnularDocumento = global.SistemasRequerimientosAnularDocumento
                && TieneAlguno("Sistemas", "SistemasRequerimientos", "SistemasRequerimientosAnularDocumento"),

            // ── Capacitación (LMS) ───────────────────────────────────────────
            Capacitacion = global.Capacitacion
                && TieneAlguno("Capacitacion", "CapacitacionCatalogo",
                               "CapacitacionMisCursos", "CapacitacionAdmin"),

            CapacitacionCatalogo = global.CapacitacionCatalogo
                && TieneAlguno("Capacitacion", "CapacitacionCatalogo"),

            CapacitacionMisCursos = global.CapacitacionMisCursos
                && TieneAlguno("Capacitacion", "CapacitacionMisCursos"),

            CapacitacionAdmin = global.CapacitacionAdmin
                && TieneAlguno("Capacitacion", "CapacitacionAdmin"),
        };
    }

    public (string? controller, string? action, string? area, string? url) GetLanding()
    {
        var menus = GetMenusActuales();

        if (menus.Dashboard)        return ("Home",             "Index", null, null);

        // Si no tiene Dashboard, retorna el primer módulo disponible según permiso
        return GetFirstAvailableModule();
    }

    // ── Mapa módulo → ruta raíz (debe coincidir con RedInterna:RutasExternasPermitidas) ──────
    // Clave: ruta raíz en minúsculas.  Valor: (controller, action, parent-flag-getter)
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

        // Devuelve el MenuOptions con los módulos que tienen ruta externa habilitada
        // Para cada propiedad, verifica si la ruta raíz del módulo está en las rutas externas.
        bool ModuloVisible(string rutaRaiz, bool flagActual)
            => flagActual && rutas.Any(r => r == rutaRaiz || r.StartsWith(rutaRaiz + "/"));

        return new MenuOptions
        {
            Dashboard = false,   // /Home/Index no está en rutas externas

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
            RhMarcaciones                         = ModuloVisible("/recursoshumanos", menus.RhMarcaciones),
            RhCompensacionDiaDia                  = ModuloVisible("/recursoshumanos", menus.RhCompensacionDiaDia),
            RhCompensacionDdc                     = ModuloVisible("/recursoshumanos", menus.RhCompensacionDdc),
            RhAutorizacionHoras                   = ModuloVisible("/recursoshumanos", menus.RhAutorizacionHoras),
            RhIndicadores                         = ModuloVisible("/recursoshumanos", menus.RhIndicadores),
            RhIndicadoresHorasExtras              = ModuloVisible("/recursoshumanos", menus.RhIndicadoresHorasExtras),
            RhIndicadoresCostoSalarialHorasExtras = ModuloVisible("/recursoshumanos", menus.RhIndicadoresCostoSalarialHorasExtras),
            RhIndicadoresComparativoCostoLaboral  = ModuloVisible("/recursoshumanos", menus.RhIndicadoresComparativoCostoLaboral),

            Logistica             = ModuloVisible("/logistica",             menus.Logistica),
            LogisticaRequerimiento= ModuloVisible("/logistica",             menus.LogisticaRequerimiento),
            LogisticaOrdenCompra  = ModuloVisible("/logistica/ordencompra", menus.LogisticaOrdenCompra),
            LogisticaIndicadores  = ModuloVisible("/logistica",             menus.LogisticaIndicadores),

            CreditosCobranza = ModuloVisible("/creditoscobranza",           menus.CreditosCobranza),
            CcNivelMorosidad = ModuloVisible("/creditoscobranza",           menus.CcNivelMorosidad),
            CcNivelTiempo    = ModuloVisible("/creditoscobranza",           menus.CcNivelTiempo),

            SaludOcupacional  = ModuloVisible("/saludocupacional",          menus.SaludOcupacional),
            SoInspeccionComedor = ModuloVisible("/saludocupacional",        menus.SoInspeccionComedor),

            Contabilidad     = ModuloVisible("/contabilidad",               menus.Contabilidad),
            ContabilidadSire = ModuloVisible("/contabilidad",               menus.ContabilidadSire),
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

        // Misma cadena de prioridad que GetLanding(), pero solo llega aquí si el módulo
        // tiene ruta externa habilitada (ya filtrado en GetMenusFiltradosPorRed).
        if (menus.Produccion)       return ("Produccion",       "Index",     null, null);
        if (menus.Sgc)              return ("Sgc",              "Index",     null, null);
        if (menus.Facturacion)      return ("Facturacion",      "Index",     null, null);
        if (menus.SireFlag)         return ("Sire",             "Index",     null, null);
        if (menus.Ventas)           return ("Ventas",           "Index",     null, null);
        if (menus.Seguridad)        return ("Inspeccion",       "Index",     null, null);
        if (menus.RecursosHumanos)  return ("RecursosHumanos",  "Index",     null, null);
        if (menus.Logistica)        return ("Logistica",        "Index",     null, null);
        if (menus.CreditosCobranza) return ("CreditosCobranza", "Index",     null, null);
        if (menus.SaludOcupacional) return ("InspeccionCom",    "Dashboard", null, null);
        if (menus.Contabilidad)     return ("Contabilidad",     "Index",     null, null);
        if (menus.Planeamiento)     return ("Planeamiento",     "Dashboard", null, null);
        if (menus.Sistemas)         return ("Sistemas",         "Index",     null, null);
        return ("Account", "AccesoDenegado", null, null);
    }

    public (string? controller, string? action, string? area, string? url) GetFirstAvailableModule()
    {
        var menus = GetMenusActuales();

        // Evalúa los módulos en el mismo orden que GetLanding()
        // Dashboard no se incluye aquí ya que es un módulo especial que puede estar deshabilitado
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
            // Si solo tiene acceso a Planeamiento sin sub-vistas específicas, ir al Dashboard
            return ("Planeamiento", "Dashboard", null, null);
        }
        if (menus.Sistemas)         return ("Sistemas",         "Index", null, null);

        // Si no tiene otros módulos, intenta Dashboard como último recurso
        if (menus.Dashboard)        return ("Home",             "Index", null, null);

        // Sin módulos asignados: AccesoDenegado
        return ("Account", "AccesoDenegado", null, null);
    }
}

