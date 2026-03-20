namespace FabricaHilos.LecturaCorreos.Models;

/// <summary>Resultado del cursor de <c>PKG_LC_LOGISTICA.SP_DOC_POR_VENCER</c>.</summary>
public class DocumentoPorVencer
{
    public long      Id                 { get; set; }
    public string    NumeroDocumento    { get; set; } = string.Empty;
    public string    DescTipoDocumento  { get; set; } = string.Empty;
    public string    RucEmisor          { get; set; } = string.Empty;
    public string    RazonSocialEmisor  { get; set; } = string.Empty;
    public string    CuentaCorreo       { get; set; } = string.Empty;
    public DateTime? FechaEmision       { get; set; }
    public DateTime? FechaVencimiento   { get; set; }
    /// <summary>FECHA_VENCIMIENTO - TRUNC(SYSDATE) — calculado por Oracle.</summary>
    public int       DiasParaVencer     { get; set; }
    public string    Moneda             { get; set; } = string.Empty;
    public decimal   TotalPagar         { get; set; }
    public decimal   MontoNetoPendiente { get; set; }
    /// <summary>"S" o "N".</summary>
    public string    TieneDetraccion    { get; set; } = "N";
    public decimal   MontoDetraccion    { get; set; }
    public string    NumeroPedido       { get; set; } = string.Empty;
    public string    NumeroGuia         { get; set; } = string.Empty;
    public string    FormaPago          { get; set; } = string.Empty;
    /// <summary>CRITICO (≤7 días) | URGENTE (≤15 días) | NORMAL.</summary>
    public string    Prioridad          { get; set; } = string.Empty;
}
