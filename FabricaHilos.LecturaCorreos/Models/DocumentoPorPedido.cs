namespace FabricaHilos.LecturaCorreos.Models;

/// <summary>Resultado del cursor de <c>PKG_LC_LOGISTICA.SP_BUSCAR_POR_PEDIDO</c>.</summary>
public class DocumentoPorPedido
{
    public long      Id                  { get; set; }
    public string    TipoXml             { get; set; } = string.Empty;
    public string    DescTipoDocumento   { get; set; } = string.Empty;
    public string    NumeroDocumento     { get; set; } = string.Empty;
    public DateTime? FechaEmision        { get; set; }
    public string    RucEmisor           { get; set; } = string.Empty;
    public string    RazonSocialEmisor   { get; set; } = string.Empty;
    public string    Moneda              { get; set; } = string.Empty;
    public decimal   TotalPagar          { get; set; }
    public string    FormaPago           { get; set; } = string.Empty;
    public DateTime? FechaVencimiento    { get; set; }
    /// <summary>"S" o "N".</summary>
    public string    TieneDetraccion     { get; set; } = "N";
    public decimal   MontoDetraccion     { get; set; }
    public string    NumeroGuia          { get; set; } = string.Empty;
    public string    PlacaVehiculo       { get; set; } = string.Empty;
    public string    NombreConductor     { get; set; } = string.Empty;
    public DateTime? FechaInicioTraslado { get; set; }
    public string    DirOrigen           { get; set; } = string.Empty;
    public string    DirDestino          { get; set; } = string.Empty;
    public string    Estado              { get; set; } = string.Empty;
    public string    CuentaCorreo        { get; set; } = string.Empty;
    public string    NombreArchivo       { get; set; } = string.Empty;
    public int       CantLineas          { get; set; }
}
