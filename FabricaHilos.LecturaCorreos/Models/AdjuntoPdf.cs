namespace FabricaHilos.LecturaCorreos.Models;

/// <summary>
/// Datos de un adjunto PDF extraído de un correo, listos para persistir
/// en la tabla FH_LECTCORREOS_PDF_ADJUNTOS vía PKG_LC_LOGISTICA.SP_GUARDAR_PDF_ADJUNTO.
/// </summary>
public class AdjuntoPdf
{
    public long      Id              { get; set; }
    public string    NombreArchivo   { get; set; } = string.Empty;
    public string    CuentaCorreo    { get; set; } = string.Empty;
    public string    AsuntoCorreo    { get; set; } = string.Empty;
    public string    RemitenteCorreo { get; set; } = string.Empty;
    public DateTime? FechaCorreo     { get; set; }
    public byte[]    Contenido       { get; set; } = [];
    /// <summary>PENDIENTE | REVISADO</summary>
    public string    Estado          { get; set; } = "PENDIENTE";
    public DateTime  FechaCreacion   { get; set; }
}
