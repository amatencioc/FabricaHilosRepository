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

    public PlaneamientoController(
        IMenuService           menuService,
        IPlnSeguimientoService seguimiento,
        IPlnAlertaService      alerta,
        IPlnKpiService         kpi,
        IPlnParamService       param)
    {
        _menuService = menuService;
        _seguimiento = seguimiento;
        _alerta      = alerta;
        _kpi         = kpi;
        _param       = param;
    }

    // GET /Planeamiento
    public IActionResult Index()
    {
        var menus = _menuService.GetMenusActuales();
        var modulos = new List<SgcModuloDto>();

        if (menus.PlaneamientoDashboard)
            modulos.Add(new SgcModuloDto
            {
                Nombre      = "Dashboard de Seguimiento",
                Descripcion = "Tablero en tiempo real de todos los pedidos activos por etapa de producción.",
                Icono       = "bi-kanban",
                ColorClase  = "text-primary",
                Controller  = "Planeamiento",
                Action      = "Dashboard"
            });

        if (menus.PlaneamientoCargaMaquinas)
            modulos.Add(new SgcModuloDto
            {
                Nombre      = "Carga de Máquinas",
                Descripcion = "Capacidad vs. carga asignada por máquina en los próximos 30 días.",
                Icono       = "bi-speedometer",
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

        if (menus.PlaneamientoKPIs)
            modulos.Add(new SgcModuloDto
            {
                Nombre      = "KPIs",
                Descripcion = "Indicadores de gestión: OTIF, ciclo de producción, tasa de reproceso.",
                Icono       = "bi-graph-up-arrow",
                ColorClase  = "text-success",
                Controller  = "Planeamiento",
                Action      = "KPIs"
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
    public async Task<IActionResult> Dashboard(string? busquedaCliente, string? codPaso, string? numPed, bool incluyeCerrados = false)
    {
        var tItems   = _seguimiento.GetActivosAsync(busquedaCliente, codPaso, numPed, incluyeCerrados);
        var tEstados = _seguimiento.GetEstadosAsync();
        var tAlertas = _alerta.GetActivasAsync();
        await Task.WhenAll(tItems, tEstados, tAlertas);
        ViewBag.Estados          = tEstados.Result;
        ViewBag.FiltroCliente    = busquedaCliente;
        ViewBag.FiltroPaso       = codPaso;
        ViewBag.FiltroNumPed     = numPed;
        // Diccionario Serie|NumPed -> cantidad de alertas activas, para mostrar badge en fila de pedido
        ViewBag.AlertasPorPedido = tAlertas.Result
            .GroupBy(a => $"{a.Serie}|{a.NumPed}")
            .ToDictionary(g => g.Key, g => g.Count());
        ViewBag.IncluyeCerrados  = incluyeCerrados;
        return View(tItems.Result);
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
        // Se deduplica por NumPartida para no consultar dos veces la misma partida.
        var items     = tItems.Result.ToList();
        var detalleTt = new Dictionary<long, PlnDetalleTt>();
        foreach (var item in items.Where(x => x.NumPartida > 0))
        {
            if (!detalleTt.ContainsKey(item.NumPartida))
                detalleTt[item.NumPartida] = await _seguimiento.GetDetalleTtAsync(item.NumPartida);
        }

        var vm = new PlnPedidoViewModel
        {
            NumPed    = numPed,
            Serie     = serie,
            Items     = items,
            Eventos   = tEventos.Result,
            Alertas   = tAlertas.Result,
            Pasos     = tPasos.Result,
            DetalleTt = detalleTt,
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
        var carga = await _kpi.GetCargaMaquinasAsync();
        return View(carga);
    }

    // GET /Planeamiento/Alertas
    public async Task<IActionResult> Alertas()
    {
        var alertas = await _alerta.GetActivasAsync();
        return View(alertas);
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
        var tItems   = _seguimiento.GetPorPedidoAsync(numPed, serie);
        var tEventos = _seguimiento.GetEventosPorPedidoAsync(numPed, serie);
        var tAlertas = _seguimiento.GetAlertasPorPedidoAsync(numPed, serie);
        var tPasos   = _seguimiento.GetEstadosAsync();
        await Task.WhenAll(tItems, tEventos, tAlertas, tPasos);

        var vm = new PlnPedidoViewModel
        {
            NumPed  = numPed,
            Serie   = serie,
            Items   = tItems.Result,
            Eventos = tEventos.Result,
            Alertas = tAlertas.Result,
            Pasos   = tPasos.Result
        };
        return View(vm);
    }

    // GET /Planeamiento/KPIs
    public async Task<IActionResult> KPIs()
    {
        var tResumen = _kpi.GetResumenAsync();
        var tProd    = _kpi.GetKpiProduccionAsync();
        await Task.WhenAll(tResumen, tProd);
        ViewBag.KpiProduccion = tProd.Result;
        return View(tResumen.Result);
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
        // Obtener ciclos distintos para el filtro
        var todosEventos = await _seguimiento.GetEventosPorPedidoAsync(numPed, serie);
        ViewBag.CiclosDisponibles = todosEventos
            .Where(e => e.IdSeguim == idSeguim)
            .Select(e => e.NroCiclo)
            .Distinct()
            .OrderBy(c => c)
            .ToList();
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
        var pendientes = await _kpi.GetPendientesDespachoAsync();
        return View(pendientes);
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
