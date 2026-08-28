using FabricaHilos.Models.Produccion.Planeamiento;
using FabricaHilos.Models.Sgc;
using FabricaHilos.Services;
using FabricaHilos.Services.Produccion.Planeamiento;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FabricaHilos.Controllers.Produccion.Planeamiento;

[Authorize]
public class PlaneamientoController : OracleBaseController
{
    private readonly IMenuService            _menuService;
    private readonly IPlnRegistroService     _registro;
    private readonly IPlnSeguimientoService  _seguimiento;
    private readonly IPlnAlertaService       _alerta;
    private readonly IPlnKpiService          _kpi;
    private readonly IPlnParamService        _param;
    private readonly IPlnReporteService      _reporte;
    private readonly IPlnPendientesService   _pendientes;

    public PlaneamientoController(
        IMenuService           menuService,
        IPlnRegistroService    registro,
        IPlnSeguimientoService seguimiento,
        IPlnAlertaService      alerta,
        IPlnKpiService         kpi,
        IPlnParamService       param,
        IPlnReporteService     reporte,
        IPlnPendientesService  pendientes)
    {
        _menuService = menuService;
        _registro    = registro;
        _seguimiento = seguimiento;
        _alerta      = alerta;
        _kpi         = kpi;
        _param       = param;
        _reporte     = reporte;
        _pendientes  = pendientes;
    }

    // GET /Planeamiento/RegistroPedido  — Registro de Pedidos: vista principal del módulo
    public async Task<IActionResult> RegistroPedido(
        string? fchDesde       = null,
        string? fchHasta       = null,
        string? cod_serv       = null,
        string? cod_cliente    = null,
        string? proceso        = null,
        string? estado         = null,
        string? tfibra         = null,
        string? paso_actual    = null,
        string? grupo          = null,
        string? fchEntDesde    = null,
        string? fchEntHasta    = null)
    {
        // Detectar carga inicial: sin parámetros en la query o petición de "reset" desde el cliente
        // El botón "Limpiar" establecerá el parámetro reset=1 para indicar que debe
        // comportarse igual que la carga inicial (traer 1 mes por defecto) pero manteniendo
        // los inputs visibles en blanco.
        string? reset = HttpContext.Request.Query["reset"].FirstOrDefault();
        var isInitialLoad = HttpContext.Request.Query.Count == 0 || (!string.IsNullOrWhiteSpace(reset) && (reset == "1" || reset.Equals("true", StringComparison.OrdinalIgnoreCase)));

        // Convertir parámetros entrantes; si vienen vacíos permanecen null
        var desdeParam = string.IsNullOrWhiteSpace(fchDesde)    ? (DateTime?)null : ParseFecha(fchDesde,    DateTime.Today);
        var hastaParam = string.IsNullOrWhiteSpace(fchHasta)    ? (DateTime?)null : ParseFecha(fchHasta,    DateTime.Today);
        var entDesde   = string.IsNullOrWhiteSpace(fchEntDesde) ? (DateTime?)null : ParseFecha(fchEntDesde, DateTime.Today);
        var entHasta   = string.IsNullOrWhiteSpace(fchEntHasta) ? (DateTime?)null : ParseFecha(fchEntHasta, DateTime.Today);

        // Para la carga inicial queremos traer un mes por defecto, pero
        // dejar los campos de fecha en la vista en blanco. Por eso
        // llamamos al service con un rango por defecto solo cuando es
        // la primera carga (isInitialLoad == true).
        DateTime? desdeForQuery = desdeParam;
        DateTime? hastaForQuery = hastaParam;
        if (isInitialLoad && desdeParam == null && hastaParam == null)
        {
            desdeForQuery = DateTime.Today.AddMonths(-1);
            hastaForQuery = DateTime.Today;
        }

        var items = await _registro.GetRegistroDiarioAsync(
            desdeForQuery, hastaForQuery,
            cod_serv    ?? "",
            cod_cliente ?? "",
            proceso     ?? "",
            estado      ?? "",
            tfibra      ?? "",
            paso_actual ?? "",
            entDesde,
            entHasta);

        var vm = new RegistroPedidosViewModel
        {
            Items            = items,
            // Mantener nulos los valores mostrados para que los inputs queden en blanco
            FchDesde         = isInitialLoad ? (DateTime?)null : desdeParam,
            FchHasta         = isInitialLoad ? (DateTime?)null : hastaParam,
            FiltroServ       = cod_serv    ?? "",
            FiltroCliente    = cod_cliente ?? "",
            FiltroProceso    = proceso     ?? "",
            FiltroEstado     = estado      ?? "",
            FiltroTfibra     = tfibra      ?? "",
            FiltroPasoActual = paso_actual ?? "",
            FiltroGrupo      = grupo ?? "dia",
            FchEntDesde      = entDesde,
            FchEntHasta      = entHasta,
        };

        return View("RegistroPedido", vm);
    }

    private static DateTime ParseFecha(string? s, DateTime fallback)
    {
        if (string.IsNullOrWhiteSpace(s)) return fallback;
        return DateTime.TryParse(s, out var d) ? d : fallback;
    }

    // GET /Planeamiento/IndexCards  — listado de tarjetas (mantenido por compatibilidad)
    public IActionResult IndexCards()
    {
        var menus = _menuService.GetMenusActuales();
        var modulos = new List<SgcModuloDto>();

        if (menus.PlaneamientoDashboard)
            modulos.Add(new SgcModuloDto
            {
                Nombre      = "Seguimiento de Pedidos",
                Descripcion = "Tablero en tiempo real de todos los pedidos activos por etapa de producción.",
                Icono       = "bi-kanban",
                ColorClase  = "text-primary",
                Controller  = "Planeamiento",
                Action      = "Dashboard"
            });

        if (menus.PlaneamientoSeguimientoTintoreria)
            modulos.Add(new SgcModuloDto
            {
                Nombre      = "Seguimiento Prog. Tintorería",
                Descripcion = "Reporte de producción de tintorería por fecha de entrega, programa, teñido, pedido o aprobación.",
                Icono       = "bi-table",
                ColorClase  = "text-primary",
                Controller  = "Planeamiento",
                Action      = "SeguimientoTintoreria"
            });

        if (menus.PlaneamientoProximosVencer)
            modulos.Add(new SgcModuloDto
            {
                Nombre      = "Próximos a Vencer",
                Descripcion = "Ítems activos cuya fecha de entrega comprometida se aproxima.",
                Icono       = "bi-calendar-event-fill",
                ColorClase  = "text-warning",
                Controller  = "Planeamiento",
                Action      = "ProximosVencer"
            });

        if (menus.PlaneamientoPendTenido)
            modulos.Add(new SgcModuloDto
            {
                Nombre      = "Pendientes de Teñido",
                Descripcion = "Partidas programadas o con previo (receta IR) aún sin producción activa de teñido. Responsables: Fredy / Malena.",
                Icono       = "bi-droplet-half",
                ColorClase  = "text-info",
                Controller  = "Planeamiento",
                Action      = "PendientesTenido"
            });

        if (menus.PlaneamientoPendSecado)
            modulos.Add(new SgcModuloDto
            {
                Nombre      = "Pendientes de Secado",
                Descripcion = "Partidas terminadas en tintorería pendientes de ingresar a secado. Responsables: Freddy / Malena.",
                Icono       = "bi-thermometer-half",
                ColorClase  = "text-warning",
                Controller  = "Planeamiento",
                Action      = "PendientesSecado"
            });

        if (menus.PlaneamientoPendMadeja)
            modulos.Add(new SgcModuloDto
            {
                Nombre      = "Pendientes de Acabado Madeja",
                Descripcion = "Partidas programadas con producción pendiente de acabado de madeja (sin V_RPRODUC activo).",
                Icono       = "bi-wind",
                ColorClase  = "text-info",
                Controller  = "Planeamiento",
                Action      = "PendientesMadeja"
            });

        if (menus.PlaneamientoPendEvalCalidad)
            modulos.Add(new SgcModuloDto
            {
                Nombre      = "Eval. Calidad",
                Descripcion = "Partidas secadas sin evaluación de calidad tintorería registrada. Responsable: Ivon.",
                Icono       = "bi-patch-check",
                ColorClase  = "text-success",
                Controller  = "Planeamiento",
                Action      = "PendientesEvalCalidad"
            });

        if (menus.PlaneamientoPendEnconado)
            modulos.Add(new SgcModuloDto
            {
                Nombre      = "Pendientes de Enconado",
                Descripcion = "Partidas aprobadas en CC pendientes de enconado o devanado (Tintorería + Hilandería). Responsable: Guevara.",
                Icono       = "bi-arrow-repeat",
                ColorClase  = "text-warning",
                Controller  = "Planeamiento",
                Action      = "PendientesEnconado"
            });

        if (menus.PlaneamientoPendRevisado)
            modulos.Add(new SgcModuloDto
            {
                Nombre      = "Pendientes de Revisado",
                Descripcion = "Partidas en programa estado 6 (revisado) sin revisado aprobado. Responsable: Martín.",
                Icono       = "bi-clipboard2-check",
                ColorClase  = "text-primary",
                Controller  = "Planeamiento",
                Action      = "PendientesRevisado"
            });

        if (menus.PlaneamientoPendientesDespacho)
            modulos.Add(new SgcModuloDto
            {
                Nombre      = "Pendientes de Despacho",
                Descripcion = "Ítems listos en almacén PT pendientes de ser despachados al cliente.",
                Icono       = "bi-truck",
                ColorClase  = "text-info",
                Controller  = "Planeamiento",
                Action      = "PendientesDespacho"
            });

        if (menus.PlaneamientoCargaMaquinas)
            modulos.Add(new SgcModuloDto
            {
                Nombre      = "Carga de Máquinas",
                Descripcion = "Planificación y carga por máquina",
                Icono       = "bi-gear-wide-connected",
                ColorClase  = "text-warning",
                Controller  = "Planeamiento",
                Action      = "CargaMaquinas"
            });

        if (menus.PlaneamientoAlertas)
            modulos.Add(new SgcModuloDto
            {
                Nombre      = "Alertas",
                Descripcion = "Bandeja de alertas activas: retrasos, reprocesos y sobrecargas.",
                Icono       = "bi-bell-fill",
                ColorClase  = "text-danger",
                Controller  = "Planeamiento",
                Action      = "Alertas"
            });

        if (menus.PlaneamientoIngresoPedApAprobFibra)
            modulos.Add(new SgcModuloDto
            {
                Nombre      = "Ingreso de Pedidos Aprobados por Grupo de Fibra",
                Descripcion = "KG de pedidos aprobados agrupados por grupo de fibra: producción, solo despacho y servicios.",
                Icono       = "bi-diagram-3-fill",
                ColorClase  = "text-primary",
                Controller  = "Planeamiento",
                Action      = "IngresoPedApAprobFibra"
            });

        modulos.Add(new SgcModuloDto
        {
            Nombre      = "Parámetros",
            Descripcion = "Configuración de umbrales, horas de turno y buffers del módulo PLN_.",
            Icono       = "bi-sliders",
            ColorClase  = "text-secondary",
            Controller  = "Planeamiento",
            Action      = "Parametros"
        });

        return View(modulos);
    }

    // GET /Planeamiento/Dashboard
    public async Task<IActionResult> Dashboard(string? busquedaCliente, string? codPaso, string? numPed, string? asesor, bool incluyeCerrados = false, int pagina = 1, int tamPagina = 50)
    {
        if (tamPagina != 50 && tamPagina != 100 && tamPagina != 200) tamPagina = 50;
        var tPagina  = _seguimiento.GetActivosPaginadoAsync(busquedaCliente, codPaso, numPed, asesor, incluyeCerrados, pagina, tamPagina);
        var tEstados = _seguimiento.GetEstadosAsync();
        var tAlertas = _alerta.GetActivasAsync();
        await Task.WhenAll(tPagina, tEstados, tAlertas);

        var resultado = tPagina.Result;
        ViewBag.Estados          = tEstados.Result;
        ViewBag.FiltroCliente    = busquedaCliente;
        ViewBag.FiltroPaso       = codPaso;
        ViewBag.FiltroNumPed     = numPed;
        ViewBag.FiltroAsesor     = asesor;
        ViewBag.IncluyeCerrados  = incluyeCerrados;
        ViewBag.AlertasPorPedido = tAlertas.Result
            .GroupBy(a => $"{a.Serie}|{a.NumPed}")
            .ToDictionary(g => g.Key, g => g.Count());
        // Metadatos de paginación
        ViewBag.Pagina         = resultado.Pagina;
        ViewBag.TotalPaginas   = resultado.TotalPaginas;
        ViewBag.TotalPedidos   = resultado.TotalPedidos;
        ViewBag.TamPagina      = tamPagina;
        // Totales globales para KPIs
        ViewBag.TotalItems       = resultado.TotalItems;
        ViewBag.TotalRetrasados  = resultado.TotalRetrasados;
        ViewBag.TotalUrgentes    = resultado.TotalUrgentes;
        ViewBag.TotalReprocesos  = resultado.TotalReprocesos;
        ViewBag.TotalSinPlanif   = resultado.TotalSinPlanif;
        return View(resultado.Items);
    }

    // GET /Planeamiento/Pedido?numPed=&serie=
    public async Task<IActionResult> Pedido(long numPed, int serie)
    {
        var tItems        = _seguimiento.GetPorPedidoAsync(numPed, serie);
        var tEventos      = _seguimiento.GetEventosPorPedidoAsync(numPed, serie);
        var tAlertas      = _seguimiento.GetAlertasPorPedidoAsync(numPed, serie);
        var tPasos        = _seguimiento.GetEstadosAsync();
        var tValidacionLab = _seguimiento.GetValidacionLabPorPedidoAsync(numPed, serie);
        await Task.WhenAll(tItems, tEventos, tAlertas, tPasos, tValidacionLab);

        // Cargar detalle TT por cada sublote que tenga partida asignada.
        // Paralelizar todas las partidas únicas (actual + anterior) en una sola ronda de Tasks.
        var items         = tItems.Result.ToList();
        var partidas      = items.Where(x => x.NumPartida    > 0).Select(x => x.NumPartida).Distinct();
        var partidasAnt   = items.Where(x => x.NumPartidaAnt > 0).Select(x => x.NumPartidaAnt).Distinct();
        var tareasTt      = partidas.Union(partidasAnt).Distinct()
                               .ToDictionary(p => p, p => _seguimiento.GetDetalleTtAsync(p));
        await Task.WhenAll(tareasTt.Values);
        var detalleTt    = partidas.Where(p    => tareasTt.ContainsKey(p))
                               .ToDictionary(p => p, p => tareasTt[p].Result);
        var detalleTtAnt = partidasAnt.Where(p => tareasTt.ContainsKey(p))
                               .ToDictionary(p => p, p => tareasTt[p].Result);

        var vm = new PlnPedidoViewModel
        {
            NumPed            = numPed,
            Serie             = serie,
            Items             = items,
            Eventos           = tEventos.Result,
            Alertas           = tAlertas.Result,
            Pasos             = tPasos.Result,
            DetalleTt         = detalleTt,
            DetalleTtAnterior = detalleTtAnt,
            ValidacionLab     = tValidacionLab.Result,
        };
        return View(vm);
    }

    // GET /Planeamiento/Pedido2?numPed=&serie=
    public async Task<IActionResult> Pedido2(long numPed, int serie)
    {
        var tItems   = _seguimiento.GetPorPedidoAsync(numPed, serie);
        var tEventos = _seguimiento.GetEventosPorPedidoAsync(numPed, serie);
        var tAlertas = _seguimiento.GetAlertasPorPedidoAsync(numPed, serie);
        var tPasos   = _seguimiento.GetEstadosAsync();
        await Task.WhenAll(tItems, tEventos, tAlertas, tPasos);

        var items         = tItems.Result.ToList();
        var partidas      = items.Where(x => x.NumPartida    > 0).Select(x => x.NumPartida).Distinct();
        var partidasAnt   = items.Where(x => x.NumPartidaAnt > 0).Select(x => x.NumPartidaAnt).Distinct();
        var tareasTt      = partidas.Union(partidasAnt).Distinct()
                               .ToDictionary(p => p, p => _seguimiento.GetDetalleTtAsync(p));
        await Task.WhenAll(tareasTt.Values);
        var detalleTt    = partidas.Where(p    => tareasTt.ContainsKey(p))
                               .ToDictionary(p => p, p => tareasTt[p].Result);
        var detalleTtAnt = partidasAnt.Where(p => tareasTt.ContainsKey(p))
                               .ToDictionary(p => p, p => tareasTt[p].Result);

        var vm = new PlnPedidoViewModel
        {
            NumPed            = numPed,
            Serie             = serie,
            Items             = items,
            Eventos           = tEventos.Result,
            Alertas           = tAlertas.Result,
            Pasos             = tPasos.Result,
            DetalleTt         = detalleTt,
            DetalleTtAnterior = detalleTtAnt,
        };
        return View(vm);
    }

    // GET /Planeamiento/GetArticuloInfo?codArt=xxx  — JSON: { descripcion, fibra }
    [HttpGet]
    public async Task<IActionResult> GetArticuloInfo(string codArt)
    {
        if (string.IsNullOrWhiteSpace(codArt))
            return Json(new { descripcion = "", fibra = "" });
        var (desc, fibra) = await _seguimiento.GetArticuloInfoAsync(codArt);
        return Json(new { descripcion = desc, fibra });
    }

    // GET /Planeamiento/GetMaquinaStatus?codMaq=R05  — JSON: { banosActivos, esLibre, pctCargaHoy, hayCargaHoy }
    [HttpGet]
    public async Task<IActionResult> GetMaquinaStatus(string codMaq)
    {
        if (string.IsNullOrWhiteSpace(codMaq))
            return Json(new { banosActivos = 0, esLibre = true, pctCargaHoy = 0.0, hayCargaHoy = false, diasAntiguo = -1 });
        var (banosActivos, esLibre, pctCargaHoy, hayCargaHoy, diasAntiguo) = await _seguimiento.GetMaquinaStatusAsync(codMaq);
        return Json(new { banosActivos, esLibre, pctCargaHoy = (double)pctCargaHoy, hayCargaHoy, diasAntiguo });
    }

    // GET /Planeamiento/CargaMaquinas
    public async Task<IActionResult> CargaMaquinas()
    {
        var tCompromisos  = _kpi.GetMaquinasCompromisoAsync();
        var tTt           = _kpi.GetEstadoMaquinasTintoreriaAsync();
        var tSec          = _kpi.GetEstadoMaquinasSecadoAsync();
        var tOtras        = _kpi.GetEstadoMaquinasOtrasAsync();
        var tHil          = _kpi.GetResumenHilanderiaAsync();
        var tCargaDiaria  = _kpi.GetCargaMaquinasAsync();
        await Task.WhenAll(tCompromisos, tTt, tSec, tOtras, tHil, tCargaDiaria);

        ViewBag.Compromisos       = tCompromisos.Result.ToList();
        ViewBag.EstadoTintoreria  = tTt.Result.ToList();
        ViewBag.EstadoSecado      = tSec.Result.ToList();
        ViewBag.EstadoOtras       = tOtras.Result.ToList();
        ViewBag.EstadoHilanderia  = tHil.Result.ToList();
        ViewBag.CargaDiaria       = tCargaDiaria.Result.ToList();
        return View();
    }

    // GET /Planeamiento/Alertas[?fchIni=DD/MM/YYYY&fchFin=DD/MM/YYYY&diasAtras=N]
    public async Task<IActionResult> Alertas(
        string? fchIni   = null,
        string? fchFin   = null,
        int     diasAtras = 0)
    {
        // input type="date" envía yyyy-MM-dd; también aceptamos dd/MM/yyyy por compatibilidad.
        var hoy      = DateTime.Today;
        var fmts     = new[] { "yyyy-MM-dd", "dd/MM/yyyy" };
        var culture  = System.Globalization.CultureInfo.InvariantCulture;
        var ini      = DateTime.TryParseExact(fchIni, fmts, culture,
                           System.Globalization.DateTimeStyles.None, out var d1) ? d1 : hoy;
        var fin      = DateTime.TryParseExact(fchFin, fmts, culture,
                           System.Globalization.DateTimeStyles.None, out var d2) ? d2 : hoy.AddDays(30);

        var tAlertas  = _alerta.GetActivasAsync();
        var tProximos = _alerta.GetProximosVencerAsync(ini, fin, diasAtras);
        await Task.WhenAll(tAlertas, tProximos);

        ViewBag.ProximosVencer = tProximos.Result;
        ViewBag.PvFchIni       = ini.ToString("dd/MM/yyyy");
        ViewBag.PvFchFin       = fin.ToString("dd/MM/yyyy");
        ViewBag.PvDiasAtras    = diasAtras;

        return View(tAlertas.Result);
    }

    // GET /Planeamiento/ProximosVencer[?fchIni=DD/MM/YYYY&fchFin=DD/MM/YYYY&diasAtras=N]
    public async Task<IActionResult> ProximosVencer(
        string? fchIni    = null,
        string? fchFin    = null,
        int     diasAtras = 0)
    {
        var hoy     = DateTime.Today;
        var fmts    = new[] { "yyyy-MM-dd", "dd/MM/yyyy" };
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        var ini     = DateTime.TryParseExact(fchIni, fmts, culture,
                          System.Globalization.DateTimeStyles.None, out var d1) ? d1 : hoy;
        var fin     = DateTime.TryParseExact(fchFin, fmts, culture,
                          System.Globalization.DateTimeStyles.None, out var d2) ? d2 : hoy.AddDays(30);

        var proximos = await _alerta.GetProximosVencerAsync(ini, fin, diasAtras);

        ViewBag.PvFchIni   = ini.ToString("dd/MM/yyyy");
        ViewBag.PvFchFin   = fin.ToString("dd/MM/yyyy");
        ViewBag.PvDiasAtras = diasAtras;

        return View(proximos);
    }

    // GET /Planeamiento/IngresoPedApAprobFibra[?fchIni=DD/MM/YYYY&fchFin=DD/MM/YYYY]
    // Ingreso de Pedidos Aprobados por Grupo de Fibra (ex QUERY RENZO).
    public async Task<IActionResult> IngresoPedApAprobFibra(
        string? fchIni = null,
        string? fchFin = null)
    {
        var hoy         = DateTime.Today;
        var inicioMes   = new DateTime(hoy.Year, hoy.Month, 1);
        var finMes      = inicioMes.AddMonths(1).AddDays(-1);
        var fmts        = new[] { "yyyy-MM-dd", "dd/MM/yyyy" };
        var culture     = System.Globalization.CultureInfo.InvariantCulture;
        var ini         = DateTime.TryParseExact(fchIni, fmts, culture,
                              System.Globalization.DateTimeStyles.None, out var d1) ? d1 : inicioMes;
        var fin         = DateTime.TryParseExact(fchFin, fmts, culture,
                              System.Globalization.DateTimeStyles.None, out var d2) ? d2 : finMes;

        var vm = await _reporte.GetIngresoPedidosAprobadosFibraAsync(ini, fin);

        return View(vm);
    }

    // GET /Planeamiento/SeguimientoTintoreria
    public async Task<IActionResult> SeguimientoTintoreria(
        string? opc      = null,
        string? fchIni   = null,
        string? fchFin   = null,
        long?   numPed   = null,
        string? cliente  = null,
        string? asesor   = null,
        string? titulo   = null,
        string? fibra    = null,
        string? proceso  = null,
        int?    mes      = null,
        int?    ano      = null)
    {
        var ct      = HttpContext.RequestAborted;
        var hoy     = DateTime.Today;
        var fmts    = new[] { "yyyy-MM-dd", "dd/MM/yyyy" };
        var culture = System.Globalization.CultureInfo.InvariantCulture;

        opc ??= "POR FECHA DE ENTREGA";

        DateTime ini, fin;

        // Filtro principal: si se especifica mes/año, calcula el rango completo del mes
        if (mes is >= 1 and <= 12 && ano is > 2000)
        {
            var anoVal = ano.Value;
            var mesVal = mes.Value;
            ini = new DateTime(anoVal, mesVal, 1);
            fin = new DateTime(anoVal, mesVal, DateTime.DaysInMonth(anoVal, mesVal));
            // El filtro de mes siempre usa POR FECHA DE ENTREGA para el rango
            if (opc == "POR PEDIDO") opc = "POR FECHA DE ENTREGA";
        }
        else
        {
            mes = null;
            ano = null;
            ini = DateTime.TryParseExact(fchIni, fmts, culture,
                          System.Globalization.DateTimeStyles.None, out var d1) ? d1 : hoy;
            fin = DateTime.TryParseExact(fchFin, fmts, culture,
                          System.Globalization.DateTimeStyles.None, out var d2) ? d2 : hoy.AddDays(30);
        }

        // Cargar combos en paralelo (servidos desde caché tras la primera llamada)
        var tClientes = _reporte.GetFiltroClientesAsync();
        var tAsesores = _reporte.GetFiltroAsesoresAsync();
        var tTitulos  = _reporte.GetFiltroTitulosAsync();
        var tFibras   = _reporte.GetFiltroFibrasAsync();
        var tProcesos = _reporte.GetFiltroProcesosAsync();
        var tCatMotivo = _reporte.GetCatalogoMotivoAsync();
        await Task.WhenAll(tClientes, tAsesores, tTitulos, tFibras, tProcesos, tCatMotivo);

        ViewBag.FiltroClientes = tClientes.Result.ToList();
        ViewBag.FiltroAsesores = tAsesores.Result.ToList();
        ViewBag.FiltroTitulos  = tTitulos.Result.ToList();
        ViewBag.FiltroFibras   = tFibras.Result.ToList();
        ViewBag.FiltroProcesos = tProcesos.Result.ToList();
        ViewBag.CatMotivo      = tCatMotivo.Result.ToList();

        IEnumerable<FabricaHilos.Models.Produccion.Planeamiento.PlnReporteProduccion> items
            = Enumerable.Empty<FabricaHilos.Models.Produccion.Planeamiento.PlnReporteProduccion>();

        if (HttpContext.Request.Query.Count > 0 || HttpContext.Request.Method == "POST")
        {
            items = await _reporte.GetReporteProduccionAsync(
                opc,
                opc == "POR PEDIDO" ? null   : ini,
                opc == "POR PEDIDO" ? null   : fin,
                opc == "POR PEDIDO" ? numPed : null,
                string.IsNullOrWhiteSpace(cliente) ? "%" : cliente,
                string.IsNullOrWhiteSpace(asesor)  ? "%" : asesor,
                string.IsNullOrWhiteSpace(titulo)  ? "%" : titulo,
                string.IsNullOrWhiteSpace(fibra)   ? "%" : fibra,
                string.IsNullOrWhiteSpace(proceso) ? "%" : proceso,
                ct);
        }

        ViewBag.StOpc     = opc;
        ViewBag.StFchIni  = ini.ToString("dd/MM/yyyy");
        ViewBag.StFchFin  = fin.ToString("dd/MM/yyyy");
        ViewBag.StIniHtml = ini.ToString("yyyy-MM-dd");
        ViewBag.StFinHtml = fin.ToString("yyyy-MM-dd");
        ViewBag.StNumPed  = numPed;
        ViewBag.StCliente = cliente;
        ViewBag.StAsesor  = asesor;
        ViewBag.StTitulo  = titulo;
        ViewBag.StFibra   = fibra;
        ViewBag.StProceso = proceso;
        ViewBag.StMes     = mes;
        ViewBag.StAno     = ano;

        return View(items);
    }

    // GET /Planeamiento/HistorialAlertas?ultDias=30
    public async Task<IActionResult> HistorialAlertas(int ultDias = 30)
    {
        var historial = await _alerta.GetHistorialAsync(ultDias);
        ViewBag.UltDias = ultDias;
        return View(historial);
    }

    // POST /Planeamiento/ResolverAlerta
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolverAlerta(long idAlerta)
    {
        var usuario = User.Identity?.Name ?? "sistema";
        await _alerta.ResolverAsync(idAlerta, usuario);
        return RedirectToAction(nameof(Alertas));
    }

    // POST /Planeamiento/IgnorarAlerta
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> IgnorarAlerta(long idAlerta)
    {
        var usuario = User.Identity?.Name ?? "sistema";
        await _alerta.IgnorarAsync(idAlerta, usuario);
        return RedirectToAction(nameof(Alertas));
    }

    // GET /Planeamiento/PedidoGantt?numPed=&serie=
    public async Task<IActionResult> PedidoGantt(long numPed, int serie)
    {
        var tEventos = _seguimiento.GetEventosPorPedidoAsync(numPed, serie);
        var tAlertas = _seguimiento.GetAlertasPorPedidoAsync(numPed, serie);
        var tPasos   = _seguimiento.GetEstadosAsync();
        var items    = (await _seguimiento.GetPorPedidoAsync(numPed, serie)).ToList();
        await Task.WhenAll(tEventos, tAlertas, tPasos);

        // Auto-planificación silenciosa: calcula FCH_EST_* para cualquier ítem
        // en paso '01' que todavía no tenga fechas estimadas, sin requerir acción del usuario.
        var sinPlanificar = items
            .Where(x => x.CodPasoAct == "01" && !x.FchEstHilanderia.HasValue)
            .ToList();

        var erroresPlanif = new List<string>();
        if (sinPlanificar.Any())
        {
            foreach (var it in sinPlanificar)
            {
                try
                {
                    await _seguimiento.CalcularFechasAsync(it.Serie, it.NumPed, it.Nro, it.NumDet, "PLA");
                }
                catch (InvalidOperationException ex)
                {
                    // SP_PLN_CALCULA_FECHAS puede fallar si faltan parámetros de proceso.
                    // Se registra el error por ítem y se continúa con los demás.
                    erroresPlanif.Add($"Ítem {it.Nro}: {ex.Message}");
                }
            }
            // Recargar ítems para que la vista reciba las fechas ya calculadas.
            items = (await _seguimiento.GetPorPedidoAsync(numPed, serie)).ToList();
        }

        if (erroresPlanif.Any())
            ViewBag.ErroresPlanif = erroresPlanif;

        // Cargar DetalleTt por sublotes con partida asignada (igual que Pedido/Pedido2).
        var partidas    = items.Where(x => x.NumPartida    > 0).Select(x => x.NumPartida).Distinct();
        var partidasAnt = items.Where(x => x.NumPartidaAnt > 0).Select(x => x.NumPartidaAnt).Distinct();
        var tareasTt    = partidas.Union(partidasAnt).Distinct()
                             .ToDictionary(p => p, p => _seguimiento.GetDetalleTtAsync(p));
        await Task.WhenAll(tareasTt.Values);
        var detalleTt    = partidas.Where(p    => tareasTt.ContainsKey(p))
                             .ToDictionary(p => p, p => tareasTt[p].Result);
        var detalleTtAnt = partidasAnt.Where(p => tareasTt.ContainsKey(p))
                             .ToDictionary(p => p, p => tareasTt[p].Result);

        var vm = new PlnPedidoViewModel
        {
            NumPed            = numPed,
            Serie             = serie,
            Items             = items,
            Eventos           = tEventos.Result,
            Alertas           = tAlertas.Result,
            Pasos             = tPasos.Result,
            DetalleTt         = detalleTt,
            DetalleTtAnterior = detalleTtAnt,
        };
        return View(vm);
    }

    // GET /Planeamiento/ItemTimeline?numPed=&serie=&nro=&numDet= — PartialView para modal
    [HttpGet]
    public async Task<IActionResult> ItemTimeline(long numPed, int serie, int nro, int numDet)
    {
        var tItem  = _seguimiento.GetByItemAsync(serie, numPed, nro, numDet);
        var tPasos = _seguimiento.GetEstadosAsync();
        var tEvt   = _seguimiento.GetEventosPorPedidoAsync(numPed, serie);
        await Task.WhenAll(tItem, tPasos, tEvt);

        var item = tItem.Result;
        if (item == null) return NotFound();

        var vm = new PlnPedidoViewModel
        {
            NumPed  = numPed,
            Serie   = serie,
            Items   = [item],
            Eventos = tEvt.Result.Where(e => e.Nro == nro).ToList(),
            Pasos   = tPasos.Result
        };
        return PartialView("_ItemTimeline", vm);
    }

    // GET /Planeamiento/ItemGantt?numPed=&serie=&nro=&numDet= — PartialView para modal
    [HttpGet]
    public async Task<IActionResult> ItemGantt(long numPed, int serie, int nro, int numDet)
    {
        var tItem  = _seguimiento.GetByItemAsync(serie, numPed, nro, numDet);
        var tPasos = _seguimiento.GetEstadosAsync();
        await Task.WhenAll(tItem, tPasos);

        var item = tItem.Result;
        if (item == null) return NotFound();

        var vm = new PlnPedidoViewModel
        {
            NumPed  = numPed,
            Serie   = serie,
            Items   = [item],
            Pasos   = tPasos.Result
        };
        return PartialView("_ItemGantt", vm);
    }

    // GET /Planeamiento/Trazabilidad?numPed=&serie=
    public async Task<IActionResult> Trazabilidad(long numPed, int serie)
    {
        var traza = await _seguimiento.GetTrazabilidadAsync(numPed, serie);
        ViewBag.NumPed = numPed;
        ViewBag.Serie  = serie;
        return View(traza);
    }

    // GET /Planeamiento/Historial?idSeguim=
    public async Task<IActionResult> Historial(long idSeguim)
    {
        var hist = await _seguimiento.GetFechasEstimadasAsync(idSeguim);
        ViewBag.IdSeguim = idSeguim;
        return View(hist);
    }

    // GET /Planeamiento/HistorialEventos?idSeguim=&numPed=&serie=&tipoEvento=&nroCiclo=&pagina=
    public async Task<IActionResult> HistorialEventos(long idSeguim, long numPed, int serie,
                                                       string? tipoEvento = null,
                                                       int?    nroCiclo   = null,
                                                       int     pagina     = 1,
                                                       int     tamPagina  = 25)
    {
        var (items, total) = await _seguimiento.GetEventosPorSeguimAsync(
                                idSeguim, tipoEvento, nroCiclo, pagina, tamPagina);
        ViewBag.IdSeguim   = idSeguim;
        ViewBag.NumPed     = numPed;
        ViewBag.Serie      = serie;
        ViewBag.TipoEvento = tipoEvento;
        ViewBag.NroCiclo   = nroCiclo;
        ViewBag.Pagina     = pagina;
        ViewBag.TamPagina  = tamPagina;
        ViewBag.Total      = total;
        ViewBag.TotalPags  = (int)Math.Ceiling(total / (double)tamPagina);
        // Ciclos disponibles: PLN_LOG_EVENTOS no almacena NRO_CICLO; derivar desde PLN_SEGUIMIENTO.
        var seguimCiclo = await _seguimiento.GetByIdAsync(idSeguim);
        ViewBag.CiclosDisponibles = seguimCiclo is null
            ? new System.Collections.Generic.List<int>()
            : Enumerable.Range(1, seguimCiclo.NroCiclo).ToList();
        return View(items);
    }

    // POST /Planeamiento/Reprogramar
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reprogramar(int serie, long numPed, int nro, int numDet,
                                                  DateTime nuevaFchDesp, string motivo)
    {
        var usuario = User.Identity?.Name ?? "sistema";
        await _seguimiento.ReprogramarAsync(serie, numPed, nro, numDet, nuevaFchDesp, motivo, usuario);
        return RedirectToAction(nameof(Pedido), new { numPed, serie });
    }

    // POST /Planeamiento/RecalcularFechas
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecalcularFechas(int serie, long numPed, int nro, int numDet,
                                                       string motivo = "REP")
    {
        await _seguimiento.CalcularFechasAsync(serie, numPed, nro, numDet, motivo);
        return RedirectToAction(nameof(Pedido), new { numPed, serie });
    }

    // POST /Planeamiento/PlanificarItem
    // ─────────────────────────────────────────────────────────────────────────
    // Propósito : Lanzar el PRIMER cálculo de fechas estimadas para un ítem que
    //             aún está en el paso '01 — Pedido Registrado', es decir, que
    //             Planeamiento no le ha asignado aún número de programación (NROPROG).
    //
    // Por qué existe este endpoint y no usa RecalcularFechas:
    //   RecalcularFechas usa motivo 'REP' (reprogramación), que en PLN_FECHAS_ESTIMADAS
    //   queda registrado como un ajuste posterior. El motivo 'PLA' (planificado) indica
    //   que es el cálculo inicial que autoriza el avance al paso '02', y es el que
    //   SP_PLN_AVANZA_PASO espera encontrar en PLN_FECHAS_ESTIMADAS para validar que
    //   el ítem tiene fechas antes de avanzar a producción.
    //
    // Flujo esperado:
    //   Gantt (paso 01) → click "Planificar" → POST aquí
    //   → SP_PLN_CALCULA_FECHAS(motivo='PLA') rellena FCH_EST_* en PLN_SEGUIMIENTO
    //   → Se inserta fila en PLN_FECHAS_ESTIMADAS
    //   → Redirect al Gantt, que ahora mostrará barras estimadas en todas las etapas
    //
    // Parámetros:
    //   serie/numPed/nro/numDet → identifican unívocamente el ítem en PLN_SEGUIMIENTO
    //   (clave compuesta, no hay columna ID_SEGUIM en el formulario del Gantt)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PlanificarItem(int serie, long numPed, int nro, int numDet)
    {
        try
        {
            await _seguimiento.CalcularFechasAsync(serie, numPed, nro, numDet, "PLA");
            TempData["Exito"] = $"Fechas estimadas calculadas correctamente para el ítem {serie}-{numPed}/{nro}/{numDet}. " +
                                 "Ya puede ver el Gantt con el cronograma estimado completo.";
        }
        catch (InvalidOperationException ex)
        {
            // SP_PLN_CALCULA_FECHAS puede lanzar una excepción descriptiva si el ítem
            // no cumple las condiciones previas (ej. estado inválido). Se captura aquí
            // para mostrarlo en un modal de error en lugar de dejar que suba como HTTP 500.
            TempData["Error09B"] = ex.Message;
            return RedirectToAction(nameof(Dashboard));
        }
        return RedirectToAction(nameof(PedidoGantt), new { numPed, serie });
    }

    // POST /Planeamiento/AvanzarPaso
    // ─────────────────────────────────────────────────────────────────────────
    // Propósito : Avance manual autorizado de un ítem de seguimiento a un paso
    //             específico. Llama a PKG_PLN.SP_PLN_AVANZA_PASO con origen 'MANUAL'.
    //
    // Cuándo se usa:
    //   - Cuando un operario o planificador necesita corregir manualmente el paso
    //     actual de un ítem (ej. el trigger automático de planta no disparó).
    //   - Para avanzar a '09B — Gaseado' solo si el ítem tiene PROCESO='24'.
    //     Cualquier otro intento lanza InvalidOperationException (BUG#35 de PKG_PLN)
    //     y se captura aquí para mostrar un modal explicativo sin error HTTP 500.
    //
    // Parámetros:
    //   proceso → se pasa desde la vista para que el service pueda validar la
    //             restricción 09B antes de llegar al paquete Oracle.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AvanzarPaso(int serie, long numPed, int nro, int numDet,
                                                  string nuevoPaso, string? proceso = null,
                                                  string? observacion = null, decimal? kgCantidad = null)
    {
        try
        {
            await _seguimiento.AvanzaPasoAsync(serie, numPed, nro, numDet,
                                               nuevoPaso, observacion, kgCantidad, proceso);
            TempData["Exito"] = $"Ítem {serie}-{numPed}/{nro}/{numDet} avanzado correctamente al paso '{nuevoPaso}'.";
        }
        catch (InvalidOperationException ex)
        {
            // La excepción descriptiva es lanzada por PlnSeguimientoService.AvanzaPasoAsync
            // cuando el intento de avanzar a '09B' no cumple la restricción PROCESO='24'
            // (BUG#35 de PKG_PLN). Se guarda en TempData["Error09B"] para que el Dashboard
            // abra automáticamente el modal de error al cargarse, evitando el error HTTP 500.
            TempData["Error09B"] = ex.Message;
        }
        return RedirectToAction(nameof(Dashboard));
    }

    // POST /Planeamiento/RefreshCargaDiaria
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RefreshCargaDiaria(DateTime? fchIni, DateTime? fchFin)
    {
        var ini = fchIni ?? DateTime.Today;
        var fin = fchFin ?? DateTime.Today.AddDays(30);
        try
        {
            await _kpi.RefreshCargaDiariaAsync(ini, fin);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error al refrescar carga diaria: {ex.Message}";
        }
        return RedirectToAction(nameof(CargaMaquinas));
    }

    // POST /Planeamiento/GenerarAlertas
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerarAlertas()
    {
        await _alerta.GenerarAlertasAsync();
        return RedirectToAction(nameof(Alertas));
    }

    // POST /Planeamiento/CerrarItem
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CerrarItem(long idSeguim, long numPed, int serie,
                                                 string motivo = "CIERRE_MANUAL")
    {
        var usuario = User.Identity?.Name ?? "sistema";
        await _seguimiento.CierreItemAsync(idSeguim, motivo, usuario);
        return RedirectToAction(nameof(Pedido), new { numPed, serie });
    }

    // POST /Planeamiento/IniciarSeguimiento
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> IniciarSeguimiento(int serie, long numPed, int nro,
                                                         int numDet = 0, string pasoIni = "01")
    {
        await _seguimiento.InitSeguimientoAsync(serie, numPed, nro, numDet, pasoIni);
        return RedirectToAction(nameof(Pedido), new { numPed, serie });
    }

    // GET /Planeamiento/PendientesDespacho
    public async Task<IActionResult> PendientesDespacho()
    {
        var pendientesTask = _kpi.GetPendientesDespachoAsync();
        var proximosTask   = _kpi.GetProximosDespachoAsync();
        await Task.WhenAll(pendientesTask, proximosTask);
        ViewBag.Proximos = proximosTask.Result.ToList();
        return View(pendientesTask.Result);
    }

    // GET /Planeamiento/Parametros
    public async Task<IActionResult> Parametros()
    {
        var parametros = await _param.GetAllAsync();
        return View(parametros);
    }

    // POST /Planeamiento/ActualizarParam
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActualizarParam(string codParam, decimal valorNum)
    {
        var usuario = User.Identity?.Name ?? "sistema";
        await _param.UpdateAsync(codParam, valorNum, usuario);
        TempData["Exito"] = $"Parámetro '{codParam}' actualizado a {valorNum}.";
        return RedirectToAction(nameof(Parametros));
    }

    // POST /Planeamiento/GuardarColorHexa  — AJAX JSON
    [HttpPost]
    public async Task<IActionResult> GuardarColorHexa([FromBody] List<PlnSaveColorDto> items)
    {
        if (items == null || items.Count == 0)
            return BadRequest(new { ok = false, msg = "Sin datos." });
        await _reporte.SaveColorHexaAsync(items, HttpContext.RequestAborted);
        return Ok(new { ok = true, n = items.Count });
    }

    // POST /Planeamiento/GuardarObservaciones  — AJAX JSON
    [HttpPost]
    public async Task<IActionResult> GuardarObservaciones([FromBody] List<PlnSaveObsDto> items)
    {
        if (items == null || items.Count == 0)
            return BadRequest(new { ok = false, msg = "Sin datos." });
        await _reporte.SaveObservacionAsync(items, HttpContext.RequestAborted);
        return Ok(new { ok = true, n = items.Count });
    }

    // POST /Planeamiento/GuardarMotivo  — AJAX JSON (Área Resp. / Motivo / Descripción)
    [HttpPost]
    public async Task<IActionResult> GuardarMotivo([FromBody] List<PlnSaveMotivoDto> items)
    {
        if (items == null || items.Count == 0)
            return BadRequest(new { ok = false, msg = "Sin datos." });
        await _reporte.SaveMotivoAsync(items, HttpContext.RequestAborted);
        return Ok(new { ok = true, n = items.Count });
    }

    // ── Partidas pendientes de revisado ─────────────────────────────────────
    // GET /Planeamiento/PendientesRevisado
    public async Task<IActionResult> PendientesRevisado(
        string? tipo = null, string? asesor = null, string? cliente = null,
        string? fchIni = null, string? fchFin = null)
    {
        var fmts    = new[] { "yyyy-MM-dd", "dd/MM/yyyy" };
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        var fechaI  = DateTime.TryParseExact(fchIni, fmts, culture,
                          System.Globalization.DateTimeStyles.None, out var d1) ? d1 : DateTime.Today;
        var fechaF  = DateTime.TryParseExact(fchFin, fmts, culture,
                          System.Globalization.DateTimeStyles.None, out var d2) ? d2 : DateTime.Today;
        ViewBag.ObsFchIni  = fechaI.ToString("yyyy-MM-dd");
        ViewBag.ObsFchFin  = fechaF.ToString("yyyy-MM-dd");

        // Una sola llamada DB al universo completo + las otras 3 en paralelo (P1 + P2)
        var tFiltroTipo = _pendientes.GetFiltroTipoAsync();
        var tUniverso   = _pendientes.GetPendientesRevisadoAsync("%", "%", "%");
        var tAsesores   = _reporte.GetFiltroAsesoresAsync();
        var tClientes   = _reporte.GetFiltroClientesAsync();
        await Task.WhenAll(tFiltroTipo, tUniverso, tAsesores, tClientes);

        // Filtrado en memoria — elimina la segunda round-trip a Oracle
        var universo = tUniverso.Result.ToList();
        IEnumerable<PlnPendienteRevisado> datos = universo;
        if (!string.IsNullOrEmpty(tipo)    && tipo    != "%") datos = datos.Where(x => x.Tipo       == tipo);
        if (!string.IsNullOrEmpty(asesor)  && asesor  != "%") datos = datos.Where(x => x.CodVende   == asesor);
        if (!string.IsNullOrEmpty(cliente) && cliente != "%") datos = datos.Where(x => x.CodCliente == cliente);
        // Cliente ALMACEN: solo mostrar partidas con 8+ rodetes (nro_rmc)
        datos = datos.Where(x => !string.Equals(x.Cliente, "ALMACEN", StringComparison.OrdinalIgnoreCase) || x.NroRmc >= 8);

        var codVende = universo.Select(d => d.CodVende).Where(s => s.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var codCli   = universo.Select(d => d.CodCliente).Where(s => s.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        ViewBag.FiltroTipo     = tFiltroTipo.Result.ToList();
        ViewBag.FiltroAsesores = tAsesores.Result.Where(a => codVende.Contains(a.CodVende ?? "")).ToList();
        ViewBag.FiltroClientes = tClientes.Result.Where(c => codCli.Contains(c.CodCliente ?? "")).ToList();
        ViewBag.FiltroTipoSel  = tipo;
        ViewBag.FiltroAsesor   = asesor;
        ViewBag.FiltroCliente  = cliente;
        var vm = new PlnRevisadoViewModel
        {
            Pendientes    = datos
                .OrderBy(x => x.Prioridad < 99 ? x.Prioridad : int.MaxValue)
                .ThenBy(x => x.FechaFin ?? DateTime.MaxValue)
                .ToList(),
            Observaciones = [],
        };
        return View(vm);
    }

    // POST /Planeamiento/GuardarPrioridadRevisado
    [HttpPost]
    public async Task<IActionResult> GuardarPrioridadRevisado(decimal guia, int prioridad)
    {
        await _pendientes.GuardarPrioridadRevisadoAsync(guia, prioridad);
        return Json(new { ok = true });
    }

    // GET /Planeamiento/ObservacionesRevisadoPartial  (lazy load – tab 2)
    public async Task<IActionResult> ObservacionesRevisadoPartial(
        string? tipo = null, string? asesor = null, string? cliente = null,
        string? fchIni = null, string? fchFin = null)
    {
        var fmts    = new[] { "yyyy-MM-dd", "dd/MM/yyyy" };
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        var fechaI  = DateTime.TryParseExact(fchIni, fmts, culture,
                          System.Globalization.DateTimeStyles.None, out var d1) ? d1 : DateTime.Today;
        var fechaF  = DateTime.TryParseExact(fchFin, fmts, culture,
                          System.Globalization.DateTimeStyles.None, out var d2) ? d2 : DateTime.Today;
        ViewBag.ObsFchIni     = fechaI.ToString("yyyy-MM-dd");
        ViewBag.ObsFchFin     = fechaF.ToString("yyyy-MM-dd");
        ViewBag.FiltroTipoSel = tipo;
        ViewBag.FiltroAsesor  = asesor;
        ViewBag.FiltroCliente = cliente;
        var obs = await _pendientes.GetObservacionesRevisadoAsync(
            tipo ?? "%", asesor ?? "%", cliente ?? "%", fechaI, fechaF);
        return PartialView("_ObsRevisadoTabContent", obs);
    }


    // -- Evaluacion de Calidad (3 tabs: EvalCalidad + PartidasDef + RectReceta)
    // GET /Planeamiento/PendientesEvalCalidad
    public async Task<IActionResult> PendientesEvalCalidad(
        string? tipo = null, string? asesor = null, string? cliente = null)
    {
        var tFiltroTipo  = _pendientes.GetFiltroTipoAsync();
        var tDatos       = _pendientes.GetPendientesEvalCalidadAsync(
            tipo ?? "%", asesor ?? "%", cliente ?? "%");
        var tUniverso    = _pendientes.GetPendientesEvalCalidadAsync("%", "%", "%");
        await Task.WhenAll(tFiltroTipo, tDatos, tUniverso);
        var universo = tUniverso.Result.ToList();
        var codVende = universo.Select(d => d.CodVende).Where(s => s.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var codCli   = universo.Select(d => d.CodCliente).Where(s => s.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var asesores = (await _reporte.GetFiltroAsesoresAsync()).Where(a => codVende.Contains(a.CodVende ?? "")).ToList();
        var clientes = (await _reporte.GetFiltroClientesAsync()).Where(c => codCli.Contains(c.CodCliente ?? "")).ToList();
        ViewBag.FiltroTipo     = tFiltroTipo.Result.Where(t => t.Tipo == "G").ToList();
        ViewBag.FiltroAsesores = asesores;
        ViewBag.FiltroClientes = clientes;
        ViewBag.FiltroTipoSel  = tipo;
        ViewBag.FiltroAsesor   = asesor;
        ViewBag.FiltroCliente  = cliente;
        var vm = new PlnEvalCalidadViewModel
        {
            EvalCalidad = tDatos.Result.OrderBy(x => x.FechaFin ?? DateTime.MaxValue).ToList(),
        };
        return View(vm);
    }

    // GET /Planeamiento/PartidasDefPartial  (lazy load - tab 2)
    public async Task<IActionResult> PartidasDefPartial(string? estEval = null)
    {
        var todos = (await _pendientes.GetPendientesPartidasDefAsync("%")).ToList();
        var tiposEval = todos
            .Where(x => !string.IsNullOrWhiteSpace(x.DescEvaluacion))
            .Select(x => x.DescEvaluacion!)
            .Distinct().OrderBy(e => e).ToList();
        var datos = string.IsNullOrEmpty(estEval)
            ? todos
            : todos.Where(x => x.DescEvaluacion == estEval).ToList();
        ViewBag.FiltroEstEval = estEval;
        ViewBag.TiposEvalAll  = tiposEval;
        return PartialView("_PartDefTabContent", datos.OrderBy(x => x.Fecha ?? DateTime.MaxValue).ToList());
    }

    // ── Partidas con evaluación de calidad pendiente de definición
    // GET /Planeamiento/PartidasPorDefinir
    public async Task<IActionResult> PartidasPorDefinir(string? estEval = null)
    {
        // Carga TODOS los datos (sin filtro en SP) para que el dropdown de tipos
        // siempre muestre todas las opciones disponibles, independientemente del filtro activo.
        var todos = (await _pendientes.GetPendientesPartidasDefAsync("%")).ToList();
        var tiposEval = todos
            .Where(x => !string.IsNullOrWhiteSpace(x.DescEvaluacion))
            .Select(x => x.DescEvaluacion!)
            .Distinct().OrderBy(e => e).ToList();
        // Filtro aplicado en C# sobre la descripción devuelta por el SP
        var datos = string.IsNullOrEmpty(estEval)
            ? todos
            : todos.Where(x => x.DescEvaluacion == estEval).ToList();
        ViewBag.FiltroEstEval = estEval;
        ViewBag.TiposEvalAll  = tiposEval;
        var vm = new PlnPartidasDefViewModel
        {
            Partidas        = datos.OrderBy(x => x.Fecha ?? DateTime.MaxValue).ToList(),
            Rectificaciones = [],
        };
        return View(vm);
    }

    // GET /Planeamiento/RectificacionRecetaPartial  (lazy load – tab 2)
    public async Task<IActionResult> RectificacionRecetaPartial(string? estado = null)
    {
        // null = carga inicial → pendientes por defecto | "" = "Todos"
        var filtro = estado switch { null => "1", "" => "%", _ => estado };
        ViewBag.FiltroEstadoRec = estado ?? "1";
        var rects = await _pendientes.GetRectificacionesRecetaAsync(filtro);
        return PartialView("_RectRecetaTabContent", rects);
    }

    // ── Partidas terminadas en tintorería pendientes de secado ───────────────
    // GET /Planeamiento/PendientesSecado
    public async Task<IActionResult> PendientesSecado(
        string? tipo = null, string? asesor = null, string? cliente = null)
    {
        var tFiltroTipo  = _pendientes.GetFiltroTipoAsync();
        var tDatos       = _pendientes.GetPendientesSecadoAsync(
            tipo ?? "%", asesor ?? "%", cliente ?? "%");
        var tUniverso    = _pendientes.GetPendientesSecadoAsync("%", "%", "%");
        await Task.WhenAll(tFiltroTipo, tDatos, tUniverso);
        var universo = tUniverso.Result.ToList();
        var codVende = universo.Select(d => d.CodVende).Where(s => s.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var codCli   = universo.Select(d => d.CodCliente).Where(s => s.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var asesores = (await _reporte.GetFiltroAsesoresAsync()).Where(a => codVende.Contains(a.CodVende ?? "")).ToList();
        var clientes = (await _reporte.GetFiltroClientesAsync()).Where(c => codCli.Contains(c.CodCliente ?? "")).ToList();
        ViewBag.FiltroTipo     = tFiltroTipo.Result.Where(t => t.Tipo == "G").ToList(); // solo Tintorería
        ViewBag.FiltroAsesores = asesores;
        ViewBag.FiltroClientes = clientes;
        ViewBag.FiltroTipoSel  = tipo;
        ViewBag.FiltroAsesor   = asesor;
        ViewBag.FiltroCliente  = cliente;
        var vm = new PlnSecadoViewModel
        {
            Secado = tDatos.Result.OrderBy(x => x.Fecha ?? DateTime.MaxValue).ToList(),
        };
        return View(vm);
    }

    // GET /Planeamiento/EnSecadoPartial  (lazy load - tab 2, usa los mismos filtros tipo/asesor/cliente)
    public async Task<IActionResult> EnSecadoPartial(
        string? tipo = null, string? asesor = null, string? cliente = null)
    {
        var tDatos    = _pendientes.GetEnSecadoAsync(tipo ?? "%", asesor ?? "%", cliente ?? "%");
        var tAsesores = _reporte.GetFiltroAsesoresAsync();
        await Task.WhenAll(tDatos, tAsesores);
        var datos    = tDatos.Result;
        var codVende = datos.Select(d => d.CodVende).Where(s => s.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        ViewBag.FiltroAsesores = tAsesores.Result.Where(a => codVende.Contains(a.CodVende ?? "")).ToList();
        return PartialView("_EnSecadoTabContent", datos.OrderBy(x => x.FechaIni ?? DateTime.MaxValue).ToList());
    }

    // ── Partidas programadas pendientes de acabado de madeja
    // GET /Planeamiento/PendientesMadeja
    public async Task<IActionResult> PendientesMadeja(
        string? tipo = null, string? asesor = null, string? cliente = null)
    {
        var tFiltroTipo  = _pendientes.GetFiltroTipoAsync();
        var tDatos       = _pendientes.GetPendientesMadejaAsync(
            tipo ?? "%", asesor ?? "%", cliente ?? "%");
        var tUniverso    = _pendientes.GetPendientesMadejaAsync("%", "%", "%");
        await Task.WhenAll(tFiltroTipo, tDatos, tUniverso);
        var universo = tUniverso.Result.ToList();
        var codVende = universo.Select(d => d.CodVende).Where(s => s.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var codCli   = universo.Select(d => d.CodCliente).Where(s => s.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var asesores = (await _reporte.GetFiltroAsesoresAsync()).Where(a => codVende.Contains(a.CodVende ?? "")).ToList();
        var clientes = (await _reporte.GetFiltroClientesAsync()).Where(c => codCli.Contains(c.CodCliente ?? "")).ToList();
        ViewBag.FiltroTipo     = tFiltroTipo.Result.Where(t => t.Tipo == "G").ToList(); // solo Tintorería
        ViewBag.FiltroAsesores = asesores;
        ViewBag.FiltroClientes = clientes;
        ViewBag.FiltroTipoSel  = tipo;
        ViewBag.FiltroAsesor   = asesor;
        ViewBag.FiltroCliente  = cliente;
        return View(tDatos.Result.OrderBy(x => x.FchProg ?? DateTime.MaxValue).ToList());
    }

    // ── Partidas aprobadas pendientes de enconado/devanado
    // GET /Planeamiento/PendientesEnconado
    public async Task<IActionResult> PendientesEnconado(
        string? tipo = null, string? asesor = null, string? cliente = null, string? rmc = null, string? estado = null)
    {
        var estadoParam  = string.IsNullOrWhiteSpace(estado) ? "%" : estado.ToUpperInvariant();
        var tFiltroTipo  = _pendientes.GetFiltroTipoAsync();
        var tDatos       = _pendientes.GetPendientesEnconadoAsync(
            tipo ?? "%", asesor ?? "%", cliente ?? "%", rmc ?? "%");
        var tCuadro1     = _pendientes.GetEnconadoCuadro1Async(tipo ?? "%", asesor ?? "%", cliente ?? "%", rmc ?? "%", estadoParam);
        var tCuadro2     = _pendientes.GetEnconadoCuadro2Async(tipo ?? "%", asesor ?? "%", cliente ?? "%", rmc ?? "%", estadoParam);
        var tAsesores    = _reporte.GetFiltroAsesoresAsync();
        var tClientes    = _reporte.GetFiltroClientesAsync();
        await Task.WhenAll(tFiltroTipo, tDatos, tCuadro1, tCuadro2, tAsesores, tClientes);
        // SP_PLN_PEND_ENCONADO ya no devuelve COD_VENDE/COD_CLIENTE (réplica literal de
        // LISTADO.sql, sin el join a CLIENTES); se listan todos los asesores/clientes,
        // el filtro p_cliente sigue funcionando en el SP aunque el combo no se acote.
        ViewBag.FiltroTipo     = tFiltroTipo.Result.ToList();
        ViewBag.FiltroAsesores = tAsesores.Result.ToList();
        ViewBag.FiltroClientes = tClientes.Result.ToList();
        ViewBag.FiltroTipoSel  = tipo;
        ViewBag.FiltroAsesor   = asesor;
        ViewBag.FiltroCliente  = cliente;
        ViewBag.FiltroRmc      = rmc ?? "%";
        ViewBag.FiltroEstado   = estado ?? "";
        ViewBag.Cuadro1        = tCuadro1.Result.ToList();
        ViewBag.Cuadro2        = tCuadro2.Result.ToList();
        return View(tDatos.Result.OrderBy(x => x.FchEntrega ?? DateTime.MaxValue).ToList());
    }

    // GET /Planeamiento/GetEnconadoCuadros — refresco AJAX de los 2 cuadros al cambiar Estado/RMC (filtros client-side de la grilla)
    // p_rmc: 'R'|'M'|'%' (el SP agrupa 'L' dentro de 'M'); p_estado: 'VENCIDO'|'PORVENCER'|'ATIEMPO'|'SINFECHA'|'%'
    [HttpGet]
    public async Task<IActionResult> GetEnconadoCuadros(
        string? tipo = null, string? asesor = null, string? cliente = null, string? rmc = null, string? estado = null)
    {
        var tCuadro1 = _pendientes.GetEnconadoCuadro1Async(tipo ?? "%", asesor ?? "%", cliente ?? "%", rmc ?? "%", estado ?? "%");
        var tCuadro2 = _pendientes.GetEnconadoCuadro2Async(tipo ?? "%", asesor ?? "%", cliente ?? "%", rmc ?? "%", estado ?? "%");
        await Task.WhenAll(tCuadro1, tCuadro2);
        return Json(new { cuadro1 = tCuadro1.Result, cuadro2 = tCuadro2.Result });
    }

    // ── Partidas pendientes de teñido
    // GET /Planeamiento/PendientesTenido
    public async Task<IActionResult> PendientesTenido(
        string? tipo = null, string? asesor = null, string? cliente = null)
    {
        var tFiltroTipo  = _pendientes.GetFiltroTipoAsync();
        var tDatos       = _pendientes.GetPendientesTenidoAsync(
            tipo ?? "%", asesor ?? "%", cliente ?? "%");
        var tUniverso    = _pendientes.GetPendientesTenidoAsync("%", "%", "%");
        await Task.WhenAll(tFiltroTipo, tDatos, tUniverso);
        var universo = tUniverso.Result.ToList();
        var codVende = universo.Select(d => d.CodVende).Where(s => s.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var codCli   = universo.Select(d => d.CodCliente).Where(s => s.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var asesores = (await _reporte.GetFiltroAsesoresAsync()).Where(a => codVende.Contains(a.CodVende ?? "")).ToList();
        var clientes = (await _reporte.GetFiltroClientesAsync()).Where(c => codCli.Contains(c.CodCliente ?? "")).ToList();
        ViewBag.FiltroTipo     = tFiltroTipo.Result.Where(t => t.Tipo == "G").ToList(); // solo Tintorería
        ViewBag.FiltroAsesores = asesores;
        ViewBag.FiltroClientes = clientes;
        ViewBag.FiltroTipoSel  = tipo;
        ViewBag.FiltroAsesor   = asesor;
        ViewBag.FiltroCliente  = cliente;
        return View(tDatos.Result.OrderBy(x => x.FechaProg ?? DateTime.MaxValue).ToList());
    }
}
