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

    public PlaneamientoController(
        IMenuService           menuService,
        IPlnSeguimientoService seguimiento,
        IPlnAlertaService      alerta,
        IPlnKpiService         kpi)
    {
        _menuService = menuService;
        _seguimiento = seguimiento;
        _alerta      = alerta;
        _kpi         = kpi;
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

        return View(modulos);
    }

    // GET /Planeamiento/Dashboard
    public async Task<IActionResult> Dashboard(string? codCliente, string? codPaso)
    {
        var tItems   = _seguimiento.GetActivosAsync(codCliente, codPaso);
        var tEstados = _seguimiento.GetEstadosAsync();
        await Task.WhenAll(tItems, tEstados);
        ViewBag.Estados       = tEstados.Result;
        ViewBag.FiltroCliente = codCliente;
        ViewBag.FiltroPaso    = codPaso;
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
}
