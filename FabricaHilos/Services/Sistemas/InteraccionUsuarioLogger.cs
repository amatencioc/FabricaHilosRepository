using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Options;
using FabricaHilos.Config;

namespace FabricaHilos.Services.Sistemas;

/// <summary>
/// Evento de interaccion persistido en disco (una linea JSON por evento).
/// </summary>
public sealed class InteraccionUsuarioEvento
{
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
    public DateTime Timestamp         { get; init; }
}

/// <summary>
/// Persiste de forma acumulada (append-only) el historial de interacciones de
/// usuarios activos en archivos JSON Lines, uno por modulo y por dia
/// (ej. "contabilidad_2026-01-15.jsonl"), dentro de la carpeta configurada en
/// Logs:UsuariosActivosPath. No usa base de datos: cada linea es un objeto JSON
/// independiente, lo que permite escritura por append sin reescribir el archivo
/// completo y evita perder todo el historial si el proceso se interrumpe a mitad
/// de una escritura.
/// </summary>
public sealed class InteraccionUsuarioLogger
{
    private readonly string _basePath;
    private readonly ILogger<InteraccionUsuarioLogger> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false
    };

    public InteraccionUsuarioLogger(IOptions<UsuariosActivosLogOptions> options, ILogger<InteraccionUsuarioLogger> logger)
    {
        _logger = logger;
        var configuredPath = options.Value.UsuariosActivosPath;
        _basePath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(AppContext.BaseDirectory, configuredPath);

        try
        {
            Directory.CreateDirectory(_basePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo crear la carpeta de historial de usuarios activos: {Path}", _basePath);
        }
    }

    public async Task RegistrarAsync(InteraccionUsuarioEvento evento)
    {
        var moduloArchivo = SanearNombreArchivo(evento.Modulo.TrimStart('/'));
        if (string.IsNullOrEmpty(moduloArchivo)) moduloArchivo = "general";

        var nombreArchivo = $"{moduloArchivo}_{evento.Timestamp:yyyy-MM-dd}.jsonl";
        var rutaCompleta  = Path.Combine(_basePath, nombreArchivo);

        var semaforo = _locks.GetOrAdd(rutaCompleta, _ => new SemaphoreSlim(1, 1));
        await semaforo.WaitAsync();
        try
        {
            var linea = JsonSerializer.Serialize(evento, _jsonOptions);
            await File.AppendAllTextAsync(rutaCompleta, linea + Environment.NewLine);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al escribir historial de usuarios activos en {Ruta}", rutaCompleta);
        }
        finally
        {
            semaforo.Release();
        }
    }

    /// <summary>Modulos disponibles (segun archivos existentes en disco), ordenados alfabeticamente.</summary>
    public IReadOnlyList<string> ObtenerModulosDisponibles()
    {
        try
        {
            if (!Directory.Exists(_basePath)) return [];

            return Directory.EnumerateFiles(_basePath, "*.jsonl")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .Select(nombre =>
                {
                    var idx = nombre.LastIndexOf('_');
                    return idx > 0 ? nombre[..idx] : nombre;
                })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(m => m, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al listar modulos disponibles en {Path}", _basePath);
            return [];
        }
    }

    /// <summary>
    /// Consulta el historial acumulado aplicando filtros opcionales de modulo, rango de fechas y usuario.
    /// Lee unicamente los archivos que correspondan al modulo/rango solicitado (no carga todo a memoria de una vez).
    /// </summary>
    public IReadOnlyList<InteraccionUsuarioEvento> Consultar(
        string? modulo, DateOnly? desde, DateOnly? hasta, string? usuario, int maxResultados = 500)
    {
        var resultados = new List<InteraccionUsuarioEvento>();
        if (!Directory.Exists(_basePath)) return resultados;

        try
        {
            var patron = string.IsNullOrWhiteSpace(modulo) ? "*.jsonl" : $"{SanearNombreArchivo(modulo)}_*.jsonl";
            var archivos = Directory.EnumerateFiles(_basePath, patron)
                .OrderByDescending(f => f); // mas recientes primero (nombre incluye fecha)

            foreach (var archivo in archivos)
            {
                if (resultados.Count >= maxResultados) break;

                var fecha = ExtraerFechaDeArchivo(archivo);
                if (desde.HasValue && fecha.HasValue && fecha.Value < desde.Value) continue;
                if (hasta.HasValue && fecha.HasValue && fecha.Value > hasta.Value) continue;

                foreach (var linea in File.ReadLines(archivo))
                {
                    if (resultados.Count >= maxResultados) break;
                    if (string.IsNullOrWhiteSpace(linea)) continue;

                    InteraccionUsuarioEvento? evento;
                    try
                    {
                        evento = JsonSerializer.Deserialize<InteraccionUsuarioEvento>(linea);
                    }
                    catch (JsonException)
                    {
                        continue; // linea corrupta/incompleta, se omite
                    }

                    if (evento is null) continue;
                    if (!string.IsNullOrWhiteSpace(usuario)
                        && !evento.Usuario.Contains(usuario, StringComparison.OrdinalIgnoreCase)
                        && !evento.Nombre.Contains(usuario, StringComparison.OrdinalIgnoreCase))
                        continue;

                    resultados.Add(evento);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al consultar historial de usuarios activos en {Path}", _basePath);
        }

        return resultados.OrderByDescending(e => e.Timestamp).ToList();
    }

    private static DateOnly? ExtraerFechaDeArchivo(string ruta)
    {
        var nombre = Path.GetFileNameWithoutExtension(ruta);
        var idx = nombre.LastIndexOf('_');
        if (idx < 0 || idx + 1 >= nombre.Length) return null;
        return DateOnly.TryParseExact(nombre[(idx + 1)..], "yyyy-MM-dd", out var fecha) ? fecha : null;
    }

    private static string SanearNombreArchivo(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return "";
        var invalidos = Path.GetInvalidFileNameChars();
        var limpio = new string(valor.Where(c => !invalidos.Contains(c)).ToArray());
        return limpio.ToLowerInvariant();
    }
}
