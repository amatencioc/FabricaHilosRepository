namespace FabricaHilos.Sire.Models;

/// <summary>
/// Resultado de una operación de subida TUS a SUNAT SIRE.
/// </summary>
public sealed class TusUploadResult
{
    /// <summary>Indica si la subida fue completada exitosamente.</summary>
    public bool Exitoso { get; set; }

    /// <summary>
    /// Número de ticket emitido por SUNAT tras la subida exitosa.
    /// Se usa para consultar el estado del procesamiento.
    /// </summary>
    public string NumTicket { get; set; } = string.Empty;

    /// <summary>URL de upload TUS creada en el servidor (Upload-Location).</summary>
    public string UploadUrl { get; set; } = string.Empty;

    /// <summary>Bytes enviados confirmados por el servidor.</summary>
    public long BytesSubidos { get; set; }

    /// <summary>Mensaje de error o descripción del resultado.</summary>
    public string Mensaje { get; set; } = string.Empty;

    public static TusUploadResult Error(string mensaje) =>
        new() { Exitoso = false, Mensaje = mensaje };

    public static TusUploadResult Ok(string numTicket, long bytesSubidos, string mensaje = "Subida completada") =>
        new() { Exitoso = true, NumTicket = numTicket, BytesSubidos = bytesSubidos, Mensaje = mensaje };
}
