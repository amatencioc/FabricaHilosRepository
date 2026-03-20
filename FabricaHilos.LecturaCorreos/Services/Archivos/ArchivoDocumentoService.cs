namespace FabricaHilos.LecturaCorreos.Services.Archivos;

using System.Text;
using System.Text.RegularExpressions;
using FabricaHilos.LecturaCorreos.Config;
using FabricaHilos.LecturaCorreos.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Almacena en disco los documentos descargados de los correos.
///
/// Estructura de carpetas:
///   {RutaArchivos}/{RucEmpresa}/{yyyy}/{MM}/{dd}/
///   La fecha es siempre <see cref="DateTime.Today"/> (día en que se copian los archivos).
///
/// Nombre de archivo:
///   {ruc_emisor}-{tipo_doc}-{serie}-{correlativo}.{ext}
///
/// Si <see cref="LecturaCorreosOptions.RutaArchivos"/> está vacío, el servicio
/// no hace nada (configurable desde appsettings.json).
/// </summary>
public sealed class ArchivoDocumentoService : IArchivoDocumentoService
{
    /// <summary>
    /// Patrón del nombre de archivo estándar SUNAT:
    ///   {RUC 11 dígitos}-{tipo 2 dígitos}-{serie}-{correlativo}.{ext}
    ///   Ej: 20551234567-01-F001-00000001.pdf
    /// </summary>
    private static readonly Regex _patronSunat = new(
        @"^(\d{11})-(\d{2})-([A-Za-z0-9]{1,4})-(\d+)\.",
        RegexOptions.Compiled);

    /// <summary>
    /// Patrón alternativo para nombres que no incluyen el RUC al inicio:
    ///   {PREFIJO}-{TIPO}-{SERIE}-{CORRELATIVO}.{ext}
    ///   Ej: PDF-DOC-E001-622720537870614.pdf
    /// El RUC se rellenará con "0".
    /// </summary>
    private static readonly Regex _patronAlternativo = new(
        @"^[A-Za-z]+-([A-Za-z0-9]+)-([A-Za-z0-9]{1,6})-(\d+)\.",
        RegexOptions.Compiled);

    private readonly LecturaCorreosOptions           _opciones;
    private readonly ILogger<ArchivoDocumentoService> _logger;

    public ArchivoDocumentoService(
        IOptions<LecturaCorreosOptions>            opciones,
        ILogger<ArchivoDocumentoService>           logger)
    {
        _opciones = opciones.Value;
        _logger   = logger;
    }

    // ── IArchivoDocumentoService ──────────────────────────────────────────────

    public async Task GuardarXmlAsync(
        DocumentoXml documento, string contenidoXml, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opciones.RutaArchivos)) return;

        // Si algún campo del objeto parseado está vacío, se intenta extraerlo
        // del nombre original del archivo adjunto (suele seguir el patrón SUNAT).
        var fb      = ExtraerCamposSunat(documento.NombreArchivo);
        var ruc     = Campo(documento.RucEmisor,    fb.Length > 0 ? fb[0] : null);
        var tipo    = Campo(documento.TipoDocumento, fb.Length > 1 ? fb[1] : null);
        var serie   = Campo(documento.Serie,         fb.Length > 2 ? fb[2] : null);
        var correl  = Campo(documento.Correlativo,   fb.Length > 3 ? fb[3] : null);
        var sufijo  = EsInforme(documento.NombreArchivo) ? "-INFORME" : string.Empty;

        var carpeta = ObtenerRutaCarpeta();
        var nombre  = $"{ruc}-{tipo}-{serie}-{correl}{sufijo}.xml";
        var ruta    = Path.Combine(carpeta, nombre);

        await EscribirArchivoAsync(ruta, carpeta, Encoding.UTF8.GetBytes(contenidoXml), ct);
    }

    public async Task GuardarPdfAsync(
        string nombreArchivoOriginal, byte[] contenido, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opciones.RutaArchivos)) return;

        var carpeta = ObtenerRutaCarpeta();
        var nombre  = ResolverNombrePdf(nombreArchivoOriginal);
        var ruta    = Path.Combine(carpeta, nombre);

        await EscribirArchivoAsync(ruta, carpeta, contenido, ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Construye la ruta de carpeta usando la fecha de hoy:
    ///   {RutaArchivos}/{RucEmpresa}/{yyyy}/{MM}/{dd}
    /// </summary>
    private string ObtenerRutaCarpeta()
    {
        var hoy = DateTime.Today;
        return Path.Combine(
            _opciones.RutaArchivos,
            _opciones.RucEmpresa,
            hoy.Year.ToString(),
            hoy.Month.ToString("D2"),
            hoy.Day.ToString("D2"));
    }

    /// <summary>
    /// Resuelve el nombre del PDF aplicando tres intentos en orden:
    ///   1. Patrón SUNAT estándar   → ruc-tipo-serie-correlativo.pdf
    ///   2. Patrón alternativo      → 0-tipo-serie-correlativo.pdf
    ///      (ej. PDF-DOC-E001-622720537870614 → 0-DOC-E001-622720537870614.pdf)
    ///   3. Sin coincidencia        → nombre original sin cambios
    /// Los campos vacíos se rellenan con "0". El sufijo -INFORME se añade cuando corresponde.
    /// </summary>
    private static string ResolverNombrePdf(string nombreOriginal)
    {
        var soloNombre = Path.GetFileName(nombreOriginal);
        var sufijo     = EsInforme(soloNombre) ? "-INFORME" : string.Empty;

        // ── Intento 1: patrón SUNAT (RUC-tipo-serie-correlativo) ─────────────
        var m = _patronSunat.Match(soloNombre);
        if (m.Success)
            return $"{Campo(m.Groups[1].Value)}-{Campo(m.Groups[2].Value)}-{Campo(m.Groups[3].Value)}-{Campo(m.Groups[4].Value)}{sufijo}.pdf";

        // ── Intento 2: patrón alternativo (PREFIJO-tipo-serie-correlativo) ───
        var ma = _patronAlternativo.Match(soloNombre);
        if (ma.Success)
        {
            var tipo   = Campo(ma.Groups[1].Value);
            var serie  = Campo(ma.Groups[2].Value);
            var correl = Campo(ma.Groups[3].Value);
            return $"0-{tipo}-{serie}-{correl}{sufijo}.pdf";
        }

        // ── Sin coincidencia: conservar el nombre original ────────────────────
        return soloNombre;
    }

    /// <summary>
    /// Devuelve <paramref name="valor"/> si no está vacío;
    /// si lo está, prueba <paramref name="fallback"/>;
    /// si también está vacío, devuelve "0".
    /// </summary>
    private static string Campo(string? valor, string? fallback = null) =>
        !string.IsNullOrWhiteSpace(valor)    ? valor    :
        !string.IsNullOrWhiteSpace(fallback) ? fallback : "0";

    /// <summary>
    /// Intenta extraer los cuatro segmentos del nombre de archivo SUNAT:
    ///   [ruc, tipo, serie, correlativo]
    /// Devuelve un array vacío si el nombre no sigue el patrón.
    /// </summary>
    private static string[] ExtraerCamposSunat(string nombreArchivo)
    {
        var m = _patronSunat.Match(Path.GetFileName(nombreArchivo));
        return m.Success
            ? [m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value]
            : [];
    }

    /// <summary>
    /// Devuelve true si el nombre del archivo (sin extensión) contiene la palabra "INFORME"
    /// (sin distinguir mayúsculas/minúsculas).
    /// </summary>
    private static bool EsInforme(string nombreArchivo) =>
        Path.GetFileNameWithoutExtension(nombreArchivo)
            .Contains("INFORME", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Crea la carpeta si no existe y escribe el archivo.
    /// Si el archivo ya existe lo omite para no sobreescribir.
    /// </summary>
    private async Task EscribirArchivoAsync(
        string ruta, string carpeta, byte[] contenido, CancellationToken ct)
    {
        try
        {
            Directory.CreateDirectory(carpeta); // no hace nada si la carpeta ya existe

            if (File.Exists(ruta))
            {
                _logger.LogDebug("Archivo ya existe, se omite: {Ruta}", ruta);
                return;
            }

            await File.WriteAllBytesAsync(ruta, contenido, ct);
            _logger.LogInformation("Archivo guardado en disco: {Ruta}", ruta);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar archivo en '{Ruta}'.", ruta);
        }
    }
}
