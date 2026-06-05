using FabricaHilos.Sire.Models;

namespace FabricaHilos.Sire.Interfaces;

/// <summary>
/// Servicio de subida de archivos ZIP/TXT a SUNAT SIRE mediante protocolo TUS.io.
/// </summary>
public interface ITusUploadService
{
    /// <summary>
    /// Sube un archivo ZIP al endpoint TUS de SUNAT y retorna el ticket de procesamiento.
    /// </summary>
    /// <param name="archivoStream">Stream del archivo ZIP/TXT a subir.</param>
    /// <param name="options">Metadata requerida por SUNAT (periodo, código proceso, etc.).</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<TusUploadResult> SubirArchivoAsync(
        Stream archivoStream,
        TusUploadOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sube el archivo ZIP de reemplazo de propuesta RCE (codProceso=61, codLibro=080000).
    /// </summary>
    Task<TusUploadResult> ReemplazarPropuestaRceAsync(
        Stream archivoZip,
        string periodo,
        string nombreArchivo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sube el archivo de reemplazo de propuesta RVIE (codProceso=61, codLibro=140100).
    /// </summary>
    Task<TusUploadResult> ReemplazarPropuestaRvieAsync(
        Stream archivoZip,
        string periodo,
        string nombreArchivo,
        CancellationToken cancellationToken = default);
}
