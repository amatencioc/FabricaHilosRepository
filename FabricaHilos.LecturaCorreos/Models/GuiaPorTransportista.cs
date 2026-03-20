namespace FabricaHilos.LecturaCorreos.Models;

/// <summary>Resultado del cursor de <c>PKG_LC_LOGISTICA.SP_GUIAS_POR_TRANSPORTISTA</c>.</summary>
public class GuiaPorTransportista
{
    public long      Id                    { get; set; }
    public string    NumeroDocumento        { get; set; } = string.Empty;
    public DateTime? FechaEmision           { get; set; }
    public string    RucEmisor              { get; set; } = string.Empty;
    public string    RazonSocialEmisor      { get; set; } = string.Empty;
    public string    RucReceptor            { get; set; } = string.Empty;
    public string    RazonSocialReceptor    { get; set; } = string.Empty;
    public string    RucTransportista       { get; set; } = string.Empty;
    public string    RazonSocTransportista  { get; set; } = string.Empty;
    public string    NombreConductor        { get; set; } = string.Empty;
    public string    LicenciaConductor      { get; set; } = string.Empty;
    public string    NroDocConductor        { get; set; } = string.Empty;
    public string    PlacaVehiculo          { get; set; } = string.Empty;
    public string    MarcaVehiculo          { get; set; } = string.Empty;
    /// <summary>"01"=transporte público, "02"=privado.</summary>
    public string    ModalidadTraslado      { get; set; } = string.Empty;
    public string    MotivoTraslado         { get; set; } = string.Empty;
    /// <summary>"01"=carretera.</summary>
    public string    ModoTransporte         { get; set; } = string.Empty;
    public decimal   PesoBruto              { get; set; }
    public string    UnidadPeso             { get; set; } = string.Empty;
    public DateTime? FechaInicioTraslado    { get; set; }
    public DateTime? FechaFinTraslado       { get; set; }
    public string    UbigeoOrigen           { get; set; } = string.Empty;
    public string    DirOrigen              { get; set; } = string.Empty;
    public string    UbigeoDestino          { get; set; } = string.Empty;
    public string    DirDestino             { get; set; } = string.Empty;
    public string    NumeroPedido           { get; set; } = string.Empty;
    public string    Estado                 { get; set; } = string.Empty;
    public string    CuentaCorreo           { get; set; } = string.Empty;
    public int       CantItems              { get; set; }
}
