using System.Collections.Concurrent;

namespace FabricaHilos.Services.Sistemas;

/// <summary>Entrada de historial: pagina visitada + timestamp.</summary>
public sealed class PaginaVisitada
{
    public string   Pagina    { get; init; } = "";
    public string   Modulo    { get; init; } = "";
    public DateTime Timestamp { get; init; }
}

public sealed class UsuarioActivoInfo
{
    // ── Identidad ──────────────────────────────────────────────────────────
    public string   Usuario         { get; init; } = "";
    public string   Nombre          { get; set;  } = "";
    public string   Ip              { get; init; } = "";

    // ── Sesion actual ──────────────────────────────────────────────────────
    public string   Modulo          { get; set;  } = "";
    public string   Pagina          { get; set;  } = "";
    public string   PaginaAnterior  { get; set;  } = "";
    public DateTime UltimaActividad { get; set;  }
    public DateTime FechaIngreso    { get; init; } = DateTime.Now;

    // ── Estadisticas ───────────────────────────────────────────────────────
    public int      TotalRequests   { get; set;  }
    public string   TipoAcceso      { get; set;  } = "Interno";   // Interno | Externo | Movil
    public string   Navegador       { get; set;  } = "";          // Chrome | Firefox | Safari | Edge | Otro
    public string   DispositivoOS   { get; set;  } = "";          // Windows | Android | iOS | Mac | Linux
    public string   Empresa         { get; set;  } = "";          // La Colonial | Arbona | Solsa

    // ── Historial circular de ultimas 15 paginas ───────────────────────────
    private readonly object _lock = new();
    private readonly Queue<PaginaVisitada> _historial = new();
    private const int MaxHistorial = 15;

    public void AgregarHistorial(string modulo, string pagina)
    {
        lock (_lock)
        {
            // No duplicar la misma pagina consecutiva
            if (_historial.Count > 0 && _historial.Last().Pagina == pagina) return;
            if (_historial.Count >= MaxHistorial) _historial.Dequeue();
            _historial.Enqueue(new PaginaVisitada { Modulo = modulo, Pagina = pagina, Timestamp = DateTime.Now });
        }
    }

    public IReadOnlyList<PaginaVisitada> ObtenerHistorial()
    {
        lock (_lock) { return _historial.Reverse().ToList(); }
    }

    // ── Helpers calculados ─────────────────────────────────────────────────
    public TimeSpan DuracionSesion => DateTime.Now - FechaIngreso;

    public string DuracionFormato
    {
        get
        {
            var d = DuracionSesion;
            if (d.TotalHours >= 1) return $"{(int)d.TotalHours}h {d.Minutes:D2}m";
            if (d.TotalMinutes >= 1) return $"{(int)d.TotalMinutes}m {d.Seconds:D2}s";
            return $"{(int)d.TotalSeconds}s";
        }
    }
}

/// <summary>Snapshot del resultado de un Registrar(), usado para persistir el evento sin releer bajo lock.</summary>
public sealed class RegistroResultado
{
    public bool     CambioPagina      { get; init; }
    public string   Usuario           { get; init; } = "";
    public string   Nombre            { get; init; } = "";
    public string   Empresa           { get; init; } = "";
    public string   Modulo            { get; init; } = "";
    public string   Pagina            { get; init; } = "";
    public string   PaginaAnterior    { get; init; } = "";
    public string   Ip                { get; init; } = "";
    public string   TipoAcceso        { get; init; } = "";
    public string   Navegador         { get; init; } = "";
    public string   DispositivoOS     { get; init; } = "";
    public int      TotalRequests     { get; init; }
    public int      DuracionSesionSeg { get; init; }
}

/// <summary>
/// Registro singleton en memoria de usuarios activos y el modulo/pagina que estan usando.
/// Tambien mantiene estadisticas de pico diario y distribucion por modulo.
/// </summary>
public sealed class UsuarioActivoStore
{
    private readonly ConcurrentDictionary<string, UsuarioActivoInfo> _activos
        = new(StringComparer.OrdinalIgnoreCase);

    // ── Estadisticas de pico diario ────────────────────────────────────────
    private int    _picoDia;
    private string _picoDiaHora = "";
    private readonly object _picoLock = new();

    public RegistroResultado Registrar(string usuario, string nombre, string modulo, string pagina,
                          string ip, string tipoAcceso, string navegador, string dispositivoOs,
                          string empresa = "")
    {
        var entry = _activos.GetOrAdd(usuario, _ => new UsuarioActivoInfo
        {
            Usuario       = usuario,
            Nombre        = nombre,
            Ip            = ip,
            TipoAcceso    = tipoAcceso,
            Navegador     = navegador,
            DispositivoOS = dispositivoOs,
            Empresa       = empresa,
            UltimaActividad = DateTime.Now,
            TotalRequests = 0
        });

        RegistroResultado resultado;

        // Mutar el entry con lock propio del objeto: evita data race entre threads
        // que leen TotalRequests, Pagina, etc. al mismo tiempo que se actualiza.
        lock (entry)
        {
            var cambioPagina = entry.Pagina != pagina;

            entry.PaginaAnterior  = entry.Pagina;
            entry.Modulo          = modulo;
            entry.Pagina          = pagina;
            entry.UltimaActividad = DateTime.Now;
            entry.TotalRequests++;
            if (!string.IsNullOrEmpty(nombre) && (entry.Nombre == entry.Usuario || string.IsNullOrEmpty(entry.Nombre)))
                entry.Nombre = nombre;
            if (!string.IsNullOrEmpty(tipoAcceso))   entry.TipoAcceso    = tipoAcceso;
            if (!string.IsNullOrEmpty(navegador))      entry.Navegador     = navegador;
            if (!string.IsNullOrEmpty(dispositivoOs))  entry.DispositivoOS = dispositivoOs;
            if (!string.IsNullOrEmpty(empresa))        entry.Empresa       = empresa;
            entry.AgregarHistorial(modulo, pagina);

            resultado = new RegistroResultado
            {
                CambioPagina      = cambioPagina,
                Usuario           = entry.Usuario,
                Nombre            = entry.Nombre,
                Empresa           = entry.Empresa,
                Modulo            = entry.Modulo,
                Pagina            = entry.Pagina,
                PaginaAnterior    = entry.PaginaAnterior,
                Ip                = entry.Ip,
                TipoAcceso        = entry.TipoAcceso,
                Navegador         = entry.Navegador,
                DispositivoOS     = entry.DispositivoOS,
                TotalRequests     = entry.TotalRequests,
                DuracionSesionSeg = (int)entry.DuracionSesion.TotalSeconds
            };
        }

        ActualizarPico();
        return resultado;
    }

    public void Remover(string usuario) => _activos.TryRemove(usuario, out _);

    /// <summary>Renueva la marca de actividad sin cambiar modulo ni pagina (heartbeat).</summary>
    public void RenovarActividad(string usuario)
    {
        if (_activos.TryGetValue(usuario, out var entry))
        {
            lock (entry)
            {
                entry.UltimaActividad = DateTime.Now;
                entry.TotalRequests++;
            }
        }
    }

    public void LimpiarInactivos(TimeSpan limite)
    {
        var corte = DateTime.Now - limite;
        foreach (var kv in _activos)
            if (kv.Value.UltimaActividad < corte)
                _activos.TryRemove(kv.Key, out _);
    }

    public UsuarioActivoInfo? ObtenerUsuario(string usuario) =>
        _activos.TryGetValue(usuario, out var e) ? e : null;

    public IReadOnlyList<UsuarioActivoInfo> ObtenerActivos()
        => _activos.Values
                   .OrderBy(u => u.Modulo)
                   .ThenBy(u => u.Nombre)
                   .ToList();

    public int CantidadActivos => _activos.Count;

    /// <summary>Distribucion de usuarios por modulo (para el grafico de barras).</summary>
    public IReadOnlyDictionary<string, int> DistribucionPorModulo()
        => _activos.Values
                   .GroupBy(u => u.Modulo.TrimStart('/'), StringComparer.OrdinalIgnoreCase)
                   .ToDictionary(g => g.Key, g => g.Count());

    /// <summary>Contadores de tipo de acceso: Interno / Externo / Movil.</summary>
    public (int interno, int externo, int movil) ContadorTipoAcceso()
    {
        int interno = 0, externo = 0, movil = 0;
        foreach (var u in _activos.Values)
        {
            if (u.TipoAcceso == "Movil")   movil++;
            else if (u.TipoAcceso == "Externo") externo++;
            else interno++;
        }
        return (interno, externo, movil);
    }

    public int  PicoDia     => _picoDia;
    public string PicoDiaHora => _picoDiaHora;

    private void ActualizarPico()
    {
        var actual = _activos.Count;
        lock (_picoLock)
        {
            if (actual > _picoDia)
            {
                _picoDia     = actual;
                _picoDiaHora = DateTime.Now.ToString("HH:mm");
            }
        }
    }
}
