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

        var carpeta = ObtenerRutaCarpeta();
        var nombre  = $"{documento.RucEmisor}-{documento.TipoDocumento}-{documento.Serie}-{documento.Correlativo}.xml";
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
    /// Resuelve el nombre del PDF.
    /// Si el nombre del archivo sigue el patrón SUNAT, lo normaliza al formato
    /// ruc-tipo-serie-correlativo.pdf; de lo contrario devuelve el nombre original.
    /// </summary>
    private static string ResolverNombrePdf(string nombreOriginal)
    {
        var soloNombre = Path.GetFileName(nombreOriginal);
        var m          = _patronSunat.Match(soloNombre);

        return m.Success
            ? $"{m.Groups[1].Value}-{m.Groups[2].Value}-{m.Groups[3].Value}-{m.Groups[4].Value}.pdf"
            : soloNombre;
    }

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
