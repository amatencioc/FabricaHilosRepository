namespace FabricaHilos.LecturaCorreos.Models;

public class LineaDocumento
{
    public int      NumeroLinea    { get; set; }
    public string   Descripcion    { get; set; } = string.Empty;
    public string   NombreItem     { get; set; } = string.Empty;
    public string   CodigoProducto { get; set; } = string.Empty;
    public string   CodigoUNSPSC   { get; set; } = string.Empty;
    public decimal  Cantidad       { get; set; }
    public string   UnidadMedida   { get; set; } = string.Empty;
    public decimal  PrecioUnitario { get; set; }
    public decimal  PrecioConIgv   { get; set; }
    public bool     EsGratuito     { get; set; }
    public decimal  SubTotal       { get; set; }
    public decimal  Igv            { get; set; }
    public decimal  TotalLinea     { get; set; }
    public string   AfectacionIgv  { get; set; } = string.Empty;
    public decimal  PorcentajeIgv  { get; set; } = 18;
    public string   Lote           { get; set; } = string.Empty;
    public DateTime? FechaVencLote { get; set; }
}
