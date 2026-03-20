namespace FabricaHilos.LecturaCorreos.Models;

/// <summary>Resultado del cursor de <c>PKG_LC_LOGISTICA.SP_RESUMEN_POR_CUENTA</c>.</summary>
public class ResumenPorCuenta
{
    public string  CuentaCorreo       { get; set; } = string.Empty;
    public int     TotalDocumentos    { get; set; }
    public int     FacturasBoletas    { get; set; }
    public int     GuiasRemision      { get; set; }
    public int     NotasCredito       { get; set; }
    public int     NotasDebito        { get; set; }
    public int     Desconocidos       { get; set; }
    public int     Procesados         { get; set; }
    public int     ConError           { get; set; }
    public int     Duplicados         { get; set; }
    public int     Ignorados          { get; set; }
    public decimal TotalSoles         { get; set; }
    public decimal TotalDolares       { get; set; }
    public decimal TotalDetracciones  { get; set; }
}
