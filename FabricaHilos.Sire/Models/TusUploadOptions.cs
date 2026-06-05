namespace FabricaHilos.Sire.Models;

/// <summary>
/// Metadata requerida por el endpoint TUS de SUNAT SIRE para la subida de archivos.
/// </summary>
public sealed class TusUploadOptions
{
    /// <summary>Número de RUC del contribuyente.</summary>
    public string NumRuc { get; set; } = string.Empty;

    /// <summary>Periodo tributario en formato yyyymm (ej: 202501).</summary>
    public string PerTributario { get; set; } = string.Empty;

    /// <summary>
    /// Código del proceso TUS según SUNAT:
    /// 61 = Reemplazar propuesta RCE
    /// 54 = Complementar propuesta
    ///  4 = Importar comprobantes al preliminar
    /// 56 = Cargar No Domiciliados
    /// </summary>
    public string CodProceso { get; set; } = string.Empty;

    /// <summary>Código de origen de envío. SUNAT usa "2" para reemplazo web.</summary>
    public string CodOrigenEnvio { get; set; } = "2";

    /// <summary>Código tipo correlativo. SUNAT usa "01".</summary>
    public string CodTipoCorrelativo { get; set; } = "01";

    /// <summary>Código del libro contable. RCE = "080000".</summary>
    public string CodLibro { get; set; } = "080000";

    /// <summary>Nombre del archivo tal como lo recibe SUNAT (ej: 20100096260_RCE_202501.zip).</summary>
    public string NombreArchivoImportacion { get; set; } = string.Empty;

    /// <summary>Tamaño del chunk en bytes para la subida TUS (default: 5 MB).</summary>
    public int ChunkSizeBytes { get; set; } = 5 * 1024 * 1024;
}
