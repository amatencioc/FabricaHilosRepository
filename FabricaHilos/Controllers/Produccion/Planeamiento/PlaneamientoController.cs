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
    private readonly IPlnSeguimientoService  _seguimiento;
    private readonly IPlnAlertaService       _alerta;
    private readonly IPlnKpiService          _kpi;
    private readonly IPlnParamService        _param;
    private readonly IPlnReporteService      _reporte;

    public PlaneamientoController(
        IMenuService           menuService,
        IPlnSeguimientoService seguimiento,
        IPlnAlertaService      alerta,
        IPlnKpiService         kpi,
        IPlnParamService       param,
        IPlnReporteService     reporte)
    {
        _menuService = menuService;
        _seguimiento = seguimiento;
        _alerta      = alerta;
        _kpi         = kpi;
        _param       = param;
        _reporte     = reporte;
    }

    // GET /Planeamiento  — redirige directamente al Dashboard (Index es redundante)
    public IActionResult Index()
    {
        return RedirectToAction(nameof(Dashboard));
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

        if (menus.PlaneamientoSeguimientoTintoreria)
            modulos.Add(new SgcModuloDto
            {
                Nombre      = "Seguimiento de Programación de Tintorería",
                Descripcion = "Reporte de producción de tintorería por fecha de entrega, programa, teñido, pedido o aprobación.",
                Icono       = "bi-table",
                ColorClase  = "text-primary",
                Controller  = "Planeamiento",
                Action      = "SeguimientoTintoreria"
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
    public async Task<IActionResult> Dashboard(string? busquedaCliente, string? codPaso, string? numPed, bool incluyeCerrados = false, int pagina = 1, int tamPagina = 50)
    {
        if (tamPagina != 50 && tamPagina != 100 && tamPagina != 200) tamPagina = 50;
        var tPagina  = _seguimiento.GetActivosPaginadoAsync(busquedaCliente, codPaso, numPed, incluyeCerrados, pagina, tamPagina);
        var tEstados = _seguimiento.GetEstadosAsync();
        var tAlertas = _alerta.GetActivasAsync();
        await Task.WhenAll(tPagina, tEstados, tAlertas);

        var resultado = tPagina.Result;
        ViewBag.Estados          = tEstados.Result;
        ViewBag.FiltroCliente    = busquedaCliente;
        ViewBag.FiltroPaso       = codPaso;
        ViewBag.FiltroNumPed     = numPed;
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
        var tItems   = _seguimiento.GetPorPedidoAsync(numPed, serie);
        var tEventos = _seguimiento.GetEventosPorPedidoAsync(numPed, serie);
        var tAlertas = _seguimiento.GetAlertasPorPedidoAsync(numPed, serie);
        var tPasos   = _seguimiento.GetEstadosAsync();
        await Task.WhenAll(tItems, tEventos, tAlertas, tPasos);

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
        await Task.WhenAll(tClientes, tAsesores, tTitulos, tFibras, tProcesos);

        ViewBag.FiltroClientes = tClientes.Result.ToList();
        ViewBag.FiltroAsesores = tAsesores.Result.ToList();
        ViewBag.FiltroTitulos  = tTitulos.Result.ToList();
        ViewBag.FiltroFibras   = tFibras.Result.ToList();
        ViewBag.FiltroProcesos = tProcesos.Result.ToList();

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
        await _kpi.RefreshCargaDiariaAsync(ini, fin);
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
}
