namespace FabricaHilos.LecturaCorreos.Services.Archivos;

using FabricaHilos.LecturaCorreos.Models;

/// <summary>
/// Guarda en disco los documentos obtenidos de los correos,
/// organizados en la estructura: RucEmpresa / año / mes / día.
/// La fecha de la carpeta es siempre la del día en que se procesan los archivos (DateTime.Today).
/// El nombre de cada archivo sigue el formato SUNAT:
///   ruc_emisor-tipo_doc-serie-correlativo.ext
/// </summary>
public interface IArchivoDocumentoService
{
    /// <summary>
    /// Guarda el contenido XML de un documento UBL con el nombre normalizado.
    /// La carpeta de destino se calcula con la fecha actual de procesamiento.
    /// Devuelve la ruta completa donde se guardó el archivo, o <see langword="null"/> si no hay ruta configurada.
    /// </summary>
    Task<string?> GuardarXmlAsync(DocumentoXml documento, string contenidoXml, string rucEmpresa, DocumentoXml? facturaRef = null, CancellationToken ct = default);

    /// <summary>
    /// Guarda los bytes de un PDF.
    /// Si <paramref name="documentoXml"/> está disponible (mismo correo), sus campos RUC/tipo/serie/correlativo
    /// se usan para construir el nombre; de lo contrario se intenta extraer del nombre original del archivo.
    /// <paramref name="facturaRef"/> se usa para añadir el correlativo de la factura al nombre de las guías (tipo 09).
    /// La carpeta de destino se calcula con la fecha actual de procesamiento.
    /// Devuelve la ruta completa donde se guardó el archivo, o <see langword="null"/> si no hay ruta configurada.
    /// </summary>
    Task<string?> GuardarPdfAsync(string nombreArchivoOriginal, byte[] contenido, string rucEmpresa, DocumentoXml? documentoXml = null, DocumentoXml? facturaRef = null, CancellationToken ct = default);
}
