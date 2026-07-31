using FabricaHilos.Services.Sistemas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FabricaHilos.Controllers.Sistemas;

[Authorize]
[Route("Sistemas/UsuariosActivos/[action]")]
public sealed class UsuariosActivosController(UsuarioActivoStore store, InteraccionUsuarioLogger interaccionLogger) : Controller
{
    // GET /Sistemas/UsuariosActivos/Index
    [HttpGet]
    public IActionResult Index() => View();

    // GET /Sistemas/UsuariosActivos/Datos  <- polling JS cada 15 s
    [HttpGet]
    public IActionResult Datos()
    {
        var activos = store.ObtenerActivos().Select(u => new
        {
            u.Usuario,
            u.Nombre,
            Modulo          = u.Modulo.TrimStart('/'),
            Pagina          = u.Pagina,
            Ip              = u.Ip,
            UltimaActividad = u.UltimaActividad.ToString("HH:mm:ss"),
            HaceSegundos    = (int)(DateTime.Now - u.UltimaActividad).TotalSeconds,
            // Campos nuevos
            TipoAcceso      = u.TipoAcceso,
            Navegador       = u.Navegador,
            DispositivoOS   = u.DispositivoOS,
            TotalRequests   = u.TotalRequests,
            Duracion        = u.DuracionFormato,
            DuracionSeg     = (int)u.DuracionSesion.TotalSeconds,
            FechaIngreso    = u.FechaIngreso.ToString("HH:mm:ss"),
            Empresa         = u.Empresa
        });

        var (interno, externo, movil) = store.ContadorTipoAcceso();
        var distribucion = store.DistribucionPorModulo();

        return Json(new
        {
            total      = store.CantidadActivos,
            interno,
            externo,
            movil,
            picoDia    = store.PicoDia,
            picoDiaHora= store.PicoDiaHora,
            distribucion,
            usuarios   = activos
        });
    }

    // GET /Sistemas/UsuariosActivos/Historial?usuario=XXXXX
    [HttpGet]
    public IActionResult Historial(string usuario)
    {
        if (string.IsNullOrWhiteSpace(usuario))
            return Json(new { ok = false, msg = "Usuario requerido." });

        var info = store.ObtenerUsuario(usuario);
        if (info is null)
            return Json(new { ok = false, msg = "Usuario no encontrado en sesiones activas." });

        var historial = info.ObtenerHistorial().Select(h => new
        {
            Modulo    = h.Modulo.TrimStart('/'),
            Pagina    = h.Pagina,
            Hora      = h.Timestamp.ToString("HH:mm:ss"),
            HaceSeg   = (int)(DateTime.Now - h.Timestamp).TotalSeconds
        });

        return Json(new
        {
            ok         = true,
            usuario    = info.Usuario,
            nombre     = info.Nombre,
            historial
        });
    }

    // GET /Sistemas/UsuariosActivos/Resumen
    [HttpGet]
    public IActionResult Resumen()
    {
        var activos = store.ObtenerActivos();
        var (interno, externo, movil) = store.ContadorTipoAcceso();
        var distribucion = store.DistribucionPorModulo();

        // Top 5 paginas mas visitadas (suma de TotalRequests por pagina)
        var topPaginas = activos
            .GroupBy(u => u.Pagina)
            .Select(g => new { pagina = g.Key, visitas = g.Sum(u => u.TotalRequests) })
            .OrderByDescending(x => x.visitas)
            .Take(5);

        // Navegadores en uso
        var navegadores = activos
            .GroupBy(u => u.Navegador)
            .Select(g => new { navegador = g.Key, cantidad = g.Count() });

        return Json(new
        {
            totalActivos = store.CantidadActivos,
            interno, externo, movil,
            picoDia    = store.PicoDia,
            picoDiaHora= store.PicoDiaHora,
            distribucion,
            topPaginas,
            navegadores
        });
    }

    // POST /Sistemas/UsuariosActivos/Heartbeat  <- ping silencioso desde _Layout cada 60 s
    [HttpPost]
    public IActionResult Heartbeat()
    {
        var usuario = User.FindFirst("OracleUser")?.Value
                   ?? User.Identity?.Name
                   ?? "";
        if (!string.IsNullOrEmpty(usuario))
            store.RenovarActividad(usuario);

        return Ok();
    }

    // GET /Sistemas/UsuariosActivos/Historico?modulo=...&desde=...&hasta=...&usuario=...
    [HttpGet]
    public IActionResult Historico(string? modulo, DateOnly? desde, DateOnly? hasta, string? usuario)
    {
        var modulos   = interaccionLogger.ObtenerModulosDisponibles();
        var eventos   = interaccionLogger.Consultar(modulo, desde, hasta, usuario);

        var vm = new HistoricoUsuariosActivosViewModel
        {
            Modulos        = modulos,
            Eventos        = eventos,
            FiltroModulo   = modulo ?? "",
            FiltroDesde    = desde,
            FiltroHasta    = hasta,
            FiltroUsuario  = usuario ?? ""
        };

        return View(vm);
    }
}

/// <summary>ViewModel de la vista de historico acumulado (filtro de archivos JSONL).</summary>
public sealed class HistoricoUsuariosActivosViewModel
{
    public IReadOnlyList<string> Modulos { get; init; } = [];
    public IReadOnlyList<InteraccionUsuarioEvento> Eventos { get; init; } = [];
    public string FiltroModulo { get; init; } = "";
    public DateOnly? FiltroDesde { get; init; }
    public DateOnly? FiltroHasta { get; init; }
    public string FiltroUsuario { get; init; } = "";
}
