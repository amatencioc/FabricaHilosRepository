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
    /// </summary>
    Task GuardarXmlAsync(DocumentoXml documento, string contenidoXml, CancellationToken ct = default);

    /// <summary>
    /// Guarda los bytes de un PDF.
    /// Intenta extraer ruc/tipo/serie/correlativo del nombre del archivo;
    /// si no coincide con el patrón SUNAT, conserva el nombre original.
    /// La carpeta de destino se calcula con la fecha actual de procesamiento.
    /// </summary>
    Task GuardarPdfAsync(string nombreArchivoOriginal, byte[] contenido, CancellationToken ct = default);
}
