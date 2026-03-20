namespace FabricaHilos.LecturaCorreos.Models;

/// <summary>Resultado del cursor de <c>PKG_LC_LOGISTICA.SP_LISTAR_ERRORES</c>.</summary>
public class ErrorProcesamiento
{
    public long      Id                 { get; set; }
    public string    NombreArchivo      { get; set; } = string.Empty;
    public string    ExtensionArchivo   { get; set; } = string.Empty;
    public string    CuentaCorreo       { get; set; } = string.Empty;
    public string    AsuntoCorreo       { get; set; } = string.Empty;
    public string    RemitenteCorreo    { get; set; } = string.Empty;
    /// <summary>PARSE_XML | IMAP | ORACLE | VALIDACION | TIPO_NO_SOPORTADO</summary>
    public string    TipoError          { get; set; } = string.Empty;
    public string    MensajeError       { get; set; } = string.Empty;
    public DateTime  FechaError         { get; set; }
    /// <summary>"S" o "N".</summary>
    public string    Procesado          { get; set; } = "N";
    public DateTime? FechaRevision      { get; set; }
    public string    Observaciones      { get; set; } = string.Empty;
}
