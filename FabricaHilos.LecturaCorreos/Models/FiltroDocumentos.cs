namespace FabricaHilos.LecturaCorreos.Models;

/// <summary>Filtros opcionales para SP_LISTAR_DOCUMENTOS.</summary>
public class FiltroDocumentos
{
    public string?   RucEmisor     { get; set; }
    public string?   RucReceptor   { get; set; }
    /// <summary>INVOICE | DESPATCH_ADVICE | CREDIT_NOTE | DEBIT_NOTE | UNKNOWN</summary>
    public string?   TipoXml       { get; set; }
    /// <summary>01=Factura, 03=Boleta, 07=NC, 08=ND, 09=Guía</summary>
    public string?   TipoDocumento { get; set; }
    /// <summary>PROCESADO | ERROR | DUPLICADO | IGNORADO</summary>
    public string?   Estado        { get; set; }
    public DateTime? FechaDesde    { get; set; }
    public DateTime? FechaHasta    { get; set; }
    public string?   NumeroPedido  { get; set; }
    public string?   NumeroGuia    { get; set; }
    public string?   CuentaCorreo  { get; set; }
}
