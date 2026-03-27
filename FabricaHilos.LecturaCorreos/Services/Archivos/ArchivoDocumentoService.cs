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

    /// <summary>
    /// Patrón para nombres separados por guión bajo que incluyen el RUC de 11 dígitos:
    ///   {RUC 11 dígitos}_{tipo}_{serie}-{correlativo}[_{extra}]
    ///   Ej: 20385817836_0198_0004-253542_78350808
    /// </summary>
    private static readonly Regex _patronSubraya = new(
        @"^(\d{11})_([A-Za-z0-9]+)_([A-Za-z0-9]+)-(\d+)",
        RegexOptions.Compiled);

    /// <summary>
    /// Patrón mínimo para nombres del tipo serie-correlativo sin RUC ni tipo:
    ///   {SERIE}-{CORRELATIVO}
    ///   Ej: F010-00043673
    /// RUC y tipo se rellenarán con "0".
    /// </summary>
    private static readonly Regex _patronSerieCorrel = new(
        @"^([A-Za-z][A-Za-z0-9]{0,5})-(\d+)",
        RegexOptions.Compiled);

    /// <summary>
    /// Patrón para CDR (Constancia de Recepción) emitido por SUNAT u OSE:
    ///   R-{RUC 11 dígitos}-{tipo 2 dígitos}-{serie}-{correlativo}
    ///   Ej: R-20347646891-01-FF05-74833.xml
    ///   Ej: R-20607958221-01-F001-00000019-2026-03-20-354.00.xml (softpad — tiene segmentos extra)
    /// </summary>
    private static readonly Regex _patronCdr = new(
        @"^R-(\d{11})-(\d{2})-([A-Za-z0-9]{1,4})-(\d+)",
        RegexOptions.Compiled);

    // ── Valores por defecto
    private const string CerosRuc    = "00000000000"; // 11 dígitos
    private const string CerosTipo   = "00";           //  2 dígitos
    private const string CerosSerie  = "0000";         //  4 dígitos
    private const string CerosCorrel = "00000000";     //  8 dígitos

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

    public async Task<string?> GuardarXmlAsync(
        DocumentoXml documento, string contenidoXml, string rucEmpresa, DocumentoXml? facturaRef = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opciones.RutaArchivos)) return null;

        // Si algún campo del objeto parseado está vacío, se intenta extraerlo
        // del nombre original del archivo adjunto (suele seguir el patrón SUNAT).
        var fb      = ExtraerCamposSunat(documento.NombreArchivo);
        var ruc     = Campo(documento.RucEmisor,     fb.Length > 0 ? fb[0] : null, CerosRuc);
        var tipo    = Campo(documento.TipoDocumento, fb.Length > 1 ? fb[1] : null, CerosTipo);
        var serie   = Campo(documento.Serie,         fb.Length > 2 ? fb[2] : null, CerosSerie);
        var correl  = PadCorrel(Campo(documento.Correlativo, fb.Length > 3 ? fb[3] : null, CerosCorrel));
        var sufijo  = ExtraerSufijo(documento.NombreArchivo);

        // Solo guardar si el documento tiene al menos un campo SUNAT identificatorio.
        if (!TieneIdentificacion(ruc, tipo, serie, correl))
        {
            _logger.LogDebug(
                "XML '{Archivo}' omitido del disco: sin RUC, tipo, serie ni correlativo identificable.",
                documento.NombreArchivo);
            return null;
        }

        var carpeta   = ObtenerRutaCarpeta(rucEmpresa);
        var sufijoCdr = documento.EsCdr ? "_CDR" : string.Empty;
        var nombre    = $"{ruc}-{tipo}-{serie}-{correl}{ObtenerSufijoFacturaRef(documento, facturaRef)}{sufijoCdr}{sufijo}.xml";
        var ruta      = Path.Combine(carpeta, nombre);

        return await EscribirArchivoAsync(ruta, carpeta, Encoding.UTF8.GetBytes(contenidoXml), ct);
    }

    public async Task<string?> GuardarPdfAsync(
        string nombreArchivoOriginal, byte[] contenido, string rucEmpresa, DocumentoXml? documentoXml = null, DocumentoXml? facturaRef = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opciones.RutaArchivos)) return null;

        var carpeta = ObtenerRutaCarpeta(rucEmpresa);
        string nombre;

        if (documentoXml is not null)
        {
            // Usar los datos del XML del mismo correo para construir el nombre normalizado.
            var fb     = ExtraerCamposSunat(documentoXml.NombreArchivo);
            var ruc    = Campo(documentoXml.RucEmisor,     fb.Length > 0 ? fb[0] : null, CerosRuc);
            var tipo   = Campo(documentoXml.TipoDocumento, fb.Length > 1 ? fb[1] : null, CerosTipo);
            var serie  = Campo(documentoXml.Serie,         fb.Length > 2 ? fb[2] : null, CerosSerie);
            var correl = PadCorrel(Campo(documentoXml.Correlativo, fb.Length > 3 ? fb[3] : null, CerosCorrel));
            var sufijo = ExtraerSufijo(nombreArchivoOriginal);

            // Solo guardar si el XML asociado tiene al menos un campo SUNAT identificatorio.
            if (!TieneIdentificacion(ruc, tipo, serie, correl))
            {
                _logger.LogDebug(
                    "PDF '{Archivo}' omitido del disco: el XML asociado no tiene RUC, tipo, serie ni correlativo identificable.",
                    nombreArchivoOriginal);
                return null;
            }

            nombre = $"{ruc}-{tipo}-{serie}-{correl}{ObtenerSufijoFacturaRef(documentoXml, facturaRef)}{sufijo}.pdf";
        }
        else
        {
            // Sin datos XML: solo guardar si el nombre original coincide con un patrón SUNAT conocido.
            if (!NombreOriginalTieneIdentificacion(nombreArchivoOriginal))
            {
                _logger.LogDebug(
                    "PDF '{Archivo}' omitido del disco: nombre sin patrón SUNAT reconocible y sin XML asociado.",
                    nombreArchivoOriginal);
                return null;
            }

            nombre = ResolverNombrePdf(nombreArchivoOriginal);
        }

        var ruta = Path.Combine(carpeta, nombre);
        return await EscribirArchivoAsync(ruta, carpeta, contenido, ct);
    }

    // ── Helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Retorna "_CORRELATIVO" de la factura de referencia cuando el documento es una guía
    /// de remisión (tipo "09") y se proporcionó una factura del mismo correo.
    /// Vacío en cualquier otro caso.
    /// </summary>
    private static string ObtenerSufijoFacturaRef(DocumentoXml documento, DocumentoXml? facturaRef)
    {
        if (facturaRef is null) return string.Empty;
        if (documento.TipoDocumento != "09") return string.Empty;
        return $"_{PadCorrel(Campo(facturaRef.Correlativo, null, CerosCorrel))}";
    }

    /// <summary>
    /// Devuelve <see langword="true"/> si al menos uno de los cuatro campos SUNAT
    /// contiene datos reales (no es el valor cero por defecto).
    /// </summary>
    private static bool TieneIdentificacion(string ruc, string tipo, string serie, string correl) =>
        ruc != CerosRuc || tipo != CerosTipo || serie != CerosSerie || correl != CerosCorrel;

    /// <summary>
    /// Devuelve <see langword="true"/> si el nombre de archivo original coincide con
    /// alguno de los cuatro patrones SUNAT conocidos (excluye el fallback de ceros).
    /// </summary>
    private static bool NombreOriginalTieneIdentificacion(string nombreOriginal)
    {
        var soloNombre = Path.GetFileName(nombreOriginal);
        return _patronSunat.IsMatch(soloNombre)
            || _patronSubraya.IsMatch(soloNombre)
            || _patronAlternativo.IsMatch(soloNombre)
            || _patronSerieCorrel.IsMatch(soloNombre)
            || _patronCdr.IsMatch(soloNombre);
    }

    /// <summary>
    /// Construye la ruta de carpeta usando la fecha de hoy:
    ///   {RutaArchivos}/{rucEmpresa}/{yyyy}/{MM}/{dd}
    /// </summary>
    private string ObtenerRutaCarpeta(string rucEmpresa)
    {
        var hoy = DateTime.Today;
        return Path.Combine(
            _opciones.RutaArchivos,
            rucEmpresa,
            hoy.Year.ToString(),
            hoy.Month.ToString("D2"),
            hoy.Day.ToString("D2"));
    }

    /// <summary>
    /// Resuelve el nombre del PDF aplicando cuatro intentos en orden:
    ///   1. Patrón SUNAT estándar     → ruc-tipo-serie-correlativo.pdf
    ///      (ej. 20551234567-01-F001-00000001)
    ///   2. Patrón guión bajo con RUC → ruc-tipo-serie-correlativo.pdf
    ///      (ej. 20385817836_0198_0004-253542_78350808 → 20385817836-0198-0004-253542.pdf)
    ///   3. Patrón alternativo        → 0-tipo-serie-correlativo.pdf
    ///      (ej. PDF-DOC-E001-622720537870614 → 0-DOC-E001-622720537870614.pdf)
    ///   4. Patrón serie-correlativo  → 0-0-serie-correlativo.pdf
    ///      (ej. F010-00043673 → 0-0-F010-00043673.pdf)
    ///   5. Sin coincidencia          → 0-0-0-{nombre_sin_ext}.pdf
    /// Los campos vacíos se rellenan con "0". El sufijo -INFORME se añade cuando corresponde.
    /// </summary>
    private static string ResolverNombrePdf(string nombreOriginal)
    {
        var soloNombre = Path.GetFileName(nombreOriginal);
        var sufijo     = ExtraerSufijo(soloNombre);

        // ── Intento 1: patrón SUNAT (RUC-tipo-serie-correlativo) ─────────────
        var m = _patronSunat.Match(soloNombre);
        if (m.Success)
            return $"{Campo(m.Groups[1].Value, null, CerosRuc)}-{Campo(m.Groups[2].Value, null, CerosTipo)}-{Campo(m.Groups[3].Value, null, CerosSerie)}-{PadCorrel(Campo(m.Groups[4].Value, null, CerosCorrel))}{sufijo}.pdf";

        // ── Intento 2: guión bajo con RUC (RUC_tipo_serie-correlativo) ────────
        var ms = _patronSubraya.Match(soloNombre);
        if (ms.Success)
            return $"{Campo(ms.Groups[1].Value, null, CerosRuc)}-{Campo(ms.Groups[2].Value, null, CerosTipo)}-{Campo(ms.Groups[3].Value, null, CerosSerie)}-{PadCorrel(Campo(ms.Groups[4].Value, null, CerosCorrel))}{sufijo}.pdf";

        // ── Intento 3: patrón alternativo (PREFIJO-tipo-serie-correlativo) ───
        var ma = _patronAlternativo.Match(soloNombre);
        if (ma.Success)
        {
            var tipo   = Campo(ma.Groups[1].Value, null, CerosTipo);
            var serie  = Campo(ma.Groups[2].Value, null, CerosSerie);
            var correl = PadCorrel(Campo(ma.Groups[3].Value, null, CerosCorrel));
            return $"{CerosRuc}-{tipo}-{serie}-{correl}{sufijo}.pdf";
        }

        // ── Intento 4: patrón mínimo (serie-correlativo) ─────────────────────
        var msc = _patronSerieCorrel.Match(soloNombre);
        if (msc.Success)
            return $"{CerosRuc}-{CerosTipo}-{Campo(msc.Groups[1].Value, null, CerosSerie)}-{PadCorrel(Campo(msc.Groups[2].Value, null, CerosCorrel))}{sufijo}.pdf";

        // ── Sin coincidencia: formato con ceros, nombre original sanitizado como correlativo
        var sinExt = SanitizarNombreArchivo(Path.GetFileNameWithoutExtension(soloNombre));
        return $"{CerosRuc}-{CerosTipo}-{CerosSerie}-{sinExt}{sufijo}.pdf";
    }

    /// <summary>
    /// Devuelve <paramref name="valor"/> si no está vacío;
    /// si lo está, prueba <paramref name="fallback"/>;
    /// si también está vacío, devuelve "0".
    /// </summary>
    private static string Campo(string? valor, string? fallback = null, string valorDefault = "0") =>
        !string.IsNullOrWhiteSpace(valor)    ? valor    :
        !string.IsNullOrWhiteSpace(fallback) ? fallback : valorDefault;

    /// <summary>
    /// Elimina caracteres no válidos en nombres de archivo y trunca a 100 caracteres
    /// para evitar path traversal y rutas demasiado largas.
    /// </summary>
    private static string SanitizarNombreArchivo(string nombre)
    {
        var invalidos = Path.GetInvalidFileNameChars();
        var limpio = new string(nombre.Where(c => Array.IndexOf(invalidos, c) < 0).ToArray());
        if (limpio.Length > 100) limpio = limpio[..100];
        return limpio.Length > 0 ? limpio : "_";
    }

    /// <summary>
    /// Intenta extraer los cuatro segmentos [ruc, tipo, serie, correlativo] del nombre de archivo.
    /// Prueba primero el patrón SUNAT estándar y luego el patrón con guión bajo.
    /// Devuelve un array vacío si ninguno coincide.
    /// </summary>
    private static string[] ExtraerCamposSunat(string nombreArchivo)
    {
        var nombre = Path.GetFileName(nombreArchivo);
        var m = _patronSunat.Match(nombre);
        if (m.Success)
            return [m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value];
        var ms = _patronSubraya.Match(nombre);
        if (ms.Success)
            return [ms.Groups[1].Value, ms.Groups[2].Value, ms.Groups[3].Value, ms.Groups[4].Value];
        var mc = _patronCdr.Match(nombre);
        if (mc.Success)
            return [mc.Groups[1].Value, mc.Groups[2].Value, mc.Groups[3].Value, mc.Groups[4].Value];
        return [];
    }

    /// <summary>
    /// Extrae el sufijo de tipo de documento según palabras clave en el nombre del archivo.
    ///   "REGLAMENTO" → "-REGLAMENTO" | "INFORME" → "-INFORME" | "REGLA" → "-REGLA"
    ///   "HOJA"       → "-HOJA"       | "SERVICIO" → "-SERVICIO" | "ACTA" → "-ACTA"
    /// El orden importa: REGLAMENTO antes de REGLA para evitar coincidencia parcial.
    /// Comparación insensible a mayúsculas/minúsculas.
    /// </summary>
    private static string ExtraerSufijo(string nombreArchivo)
    {
        var sinExt = Path.GetFileNameWithoutExtension(nombreArchivo);
        if (sinExt.Contains("REGLAMENTO", StringComparison.OrdinalIgnoreCase)) return "-REGLAMENTO";
        if (sinExt.Contains("INFORME",    StringComparison.OrdinalIgnoreCase)) return "-INFORME";
        if (sinExt.Contains("REGLA",      StringComparison.OrdinalIgnoreCase)) return "-REGLA";
        if (sinExt.Contains("HOJA",       StringComparison.OrdinalIgnoreCase)) return "-HOJA";
        if (sinExt.Contains("SERVICIO",   StringComparison.OrdinalIgnoreCase)) return "-SERVICIO";
        if (sinExt.Contains("ACTA",       StringComparison.OrdinalIgnoreCase)) return "-ACTA";
        return string.Empty;
    }

    /// <summary>
    /// Rellena el correlativo con ceros a la izquierda hasta completar 8 dígitos.
    /// </summary>
    private static string PadCorrel(string correl) => correl.PadLeft(8, '0');

    /// <summary>
    /// Crea la carpeta si no existe y escribe el archivo.
    /// Si el nombre ya existe en disco, busca un nombre único añadiendo un contador (-1, -2, …).
    /// Retorna la ruta definitivamente escrita, o <see langword="null"/> si hubo error.
    /// </summary>
    private async Task<string?> EscribirArchivoAsync(
        string ruta, string carpeta, byte[] contenido, CancellationToken ct)
    {
        try
        {
            Directory.CreateDirectory(carpeta); // no hace nada si la carpeta ya existe

            // Si el nombre ya existe, buscar el primer nombre libre con sufijo numérico.
            var rutaFinal = ruta;
            if (File.Exists(rutaFinal))
            {
                var sinExt  = Path.GetFileNameWithoutExtension(ruta);
                var ext     = Path.GetExtension(ruta);
                int contador = 1;
                const int maxIntentos = 999;
                do
                {
                    rutaFinal = Path.Combine(carpeta, $"{sinExt}-{contador}{ext}");
                    contador++;
                }
                while (File.Exists(rutaFinal) && contador <= maxIntentos);

                if (contador > maxIntentos)
                {
                    _logger.LogWarning(
                        "Se alcanzó el límite de {Max} nombres en disco para '{Nombre}'. Se omite el archivo.",
                        maxIntentos, Path.GetFileName(ruta));
                    return null;
                }

                _logger.LogDebug(
                    "Nombre '{Nombre}' ya existente en disco; se usa: {RutaFinal}",
                    Path.GetFileName(ruta), rutaFinal);
            }

            await File.WriteAllBytesAsync(rutaFinal, contenido, ct);
            _logger.LogInformation("Archivo guardado en disco: {Ruta}", rutaFinal);
            return rutaFinal;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar archivo en '{Ruta}'.", ruta);
            return null;
        }
    }
}
