namespace FabricaHilos.LecturaCorreos.Models;

/// <summary>Resultado del cursor de <c>PKG_LC_LOGISTICA.SP_RESUMEN_POR_PROVEEDOR</c>.</summary>
public class ResumenPorProveedor
{
    public string    RucEmisor          { get; set; } = string.Empty;
    public string    RazonSocialEmisor  { get; set; } = string.Empty;
    public int       TotalDocumentos    { get; set; }
    public int       FacturasBoletas    { get; set; }
    public int       GuiasRemision      { get; set; }
    public int       NotasCredito       { get; set; }
    public int       NotasDebito        { get; set; }
    public decimal   TotalSoles         { get; set; }
    public decimal   TotalDolares       { get; set; }
    public decimal   TotalDetracciones  { get; set; }
    public DateTime? PrimeraFactura     { get; set; }
    public DateTime? UltimaFactura      { get; set; }
    public decimal   PromedioImporte    { get; set; }
}
