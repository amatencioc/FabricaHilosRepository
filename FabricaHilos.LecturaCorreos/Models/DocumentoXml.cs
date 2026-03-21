namespace FabricaHilos.LecturaCorreos.Models;

public class DocumentoXml
{
    // ── Origen ────────────────────────────────────────────────
    public string    NombreArchivo         { get; set; } = string.Empty;
    public string    CuentaCorreo          { get; set; } = string.Empty;
    public string    AsuntoCorreo          { get; set; } = string.Empty;
    public string    RemitenteCorreo       { get; set; } = string.Empty;
    public DateTime? FechaCorreo           { get; set; }

    // ── Clasificación ─────────────────────────────────────────
    public string    TipoXml              { get; set; } = string.Empty; // INVOICE, DESPATCH_ADVICE, CREDIT_NOTE, DEBIT_NOTE, APPLICATION_RESPONSE
    public string    TipoDocumento        { get; set; } = string.Empty; // 01, 03, 07, 08, 09
    public string    Serie                { get; set; } = string.Empty;
    public string    Correlativo          { get; set; } = string.Empty;
    public string    NumeroDocumento      { get; set; } = string.Empty;
    /// <summary>Indica que este documento es una Constancia de Recepción (CDR / ApplicationResponse).</summary>
    public bool      EsCdr                { get; set; }
    public DateTime? FechaEmision         { get; set; }
    public string    HoraEmision          { get; set; } = string.Empty;
    public DateTime? FechaVencimiento     { get; set; }

    // ── Emisor ────────────────────────────────────────────────
    public string RucEmisor              { get; set; } = string.Empty;
    public string NombreComercialEmisor  { get; set; } = string.Empty;
    public string RazonSocialEmisor      { get; set; } = string.Empty;
    public string DireccionEmisor        { get; set; } = string.Empty;
    public string UbigeoEmisor           { get; set; } = string.Empty;

    // ── Receptor ──────────────────────────────────────────────
    public string RucReceptor            { get; set; } = string.Empty;
    public string RazonSocialReceptor    { get; set; } = string.Empty;
    public string DireccionReceptor      { get; set; } = string.Empty;
    public string UbigeoReceptor         { get; set; } = string.Empty;

    // ── Importes ──────────────────────────────────────────────
    public string  Moneda                { get; set; } = string.Empty;
    public decimal BaseImponible         { get; set; }
    public decimal TotalIgv              { get; set; }
    public decimal TotalExonerado        { get; set; }
    public decimal TotalInafecto         { get; set; }
    public decimal TotalGratuito         { get; set; }
    public decimal TotalDescuento        { get; set; }
    public decimal TotalCargo            { get; set; }
    public decimal TotalAnticipos        { get; set; }
    public decimal TotalPagar            { get; set; }

    // ── Forma de pago ─────────────────────────────────────────
    public string  FormaPago             { get; set; } = string.Empty; // Contado / Credito
    public decimal MontoNetoPendiente    { get; set; }

    // ── Detracción ────────────────────────────────────────────
    public bool    TieneDetraccion       { get; set; }
    public string  CodBienDetraccion     { get; set; } = string.Empty;
    public string  NroCuentaDetraccion   { get; set; } = string.Empty;
    public decimal PctDetraccion         { get; set; }
    public decimal MontoDetraccion       { get; set; }

    // ── Referencias cruzadas ──────────────────────────────────
    public string NumeroPedido           { get; set; } = string.Empty;
    public string NumeroGuia             { get; set; } = string.Empty;
    public string NumeroDocRef           { get; set; } = string.Empty;

    // ── Transporte (DespatchAdvice) ───────────────────────────
    public string    ModalidadTraslado     { get; set; } = string.Empty;
    public string    MotivoTraslado        { get; set; } = string.Empty;
    public string    ModoTransporte        { get; set; } = string.Empty;
    public decimal   PesoBruto             { get; set; }
    public string    UnidadPeso            { get; set; } = string.Empty;
    public DateTime? FechaInicioTraslado   { get; set; }
    public DateTime? FechaFinTraslado      { get; set; }
    public string    RucTransportista      { get; set; } = string.Empty;
    public string    RazonSocTransportista { get; set; } = string.Empty;
    public string    NombreConductor       { get; set; } = string.Empty;
    public string    LicenciaConductor     { get; set; } = string.Empty;
    public string    PlacaVehiculo         { get; set; } = string.Empty;
    public string    MarcaVehiculo         { get; set; } = string.Empty;
    public string    NroDocConductor       { get; set; } = string.Empty;
    public string    UbigeoOrigen          { get; set; } = string.Empty;
    public string    DirOrigen             { get; set; } = string.Empty;
    public string    UbigeoDestino         { get; set; } = string.Empty;
    public string    DirDestino            { get; set; } = string.Empty;

    // ── Extra ─────────────────────────────────────────────────
    public string Vendedor                 { get; set; } = string.Empty;
    public string XmlContenido             { get; set; } = string.Empty;

    // ── Colecciones ───────────────────────────────────────────
    public List<LineaDocumento> Lineas     { get; set; } = [];
    public List<CuotaPago>     Cuotas     { get; set; } = [];
}
