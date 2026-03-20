namespace FabricaHilos.LecturaCorreos.Models;

/// <summary>
/// Resultado de SP_LISTAR_DOCUMENTOS y cabecera de SP_OBTENER_DOCUMENTO.
/// Mapea las columnas seleccionadas de FH_LC_DOCUMENTO más los conteos relacionados.
/// </summary>
public class DocumentoResumen
{
    // ── Identificación ────────────────────────────────────────
    public long      Id                    { get; set; }

    // ── Origen correo ─────────────────────────────────────────
    public string    NombreArchivo         { get; set; } = string.Empty;
    public string    CuentaCorreo          { get; set; } = string.Empty;
    public string    AsuntoCorreo          { get; set; } = string.Empty;
    public string    RemitenteCorreo       { get; set; } = string.Empty;
    public DateTime? FechaCorreo           { get; set; }

    // ── Clasificación ─────────────────────────────────────────
    public string    TipoXml               { get; set; } = string.Empty;
    public string    TipoDocumento         { get; set; } = string.Empty;
    public string    DescTipoDocumento     { get; set; } = string.Empty;
    public string    Serie                 { get; set; } = string.Empty;
    public string    Correlativo           { get; set; } = string.Empty;
    public string    NumeroDocumento       { get; set; } = string.Empty;
    public DateTime? FechaEmision          { get; set; }
    public string    HoraEmision           { get; set; } = string.Empty;

    // ── Emisor ────────────────────────────────────────────────
    public string    RucEmisor             { get; set; } = string.Empty;
    public string    RazonSocialEmisor     { get; set; } = string.Empty;
    public string    NombreComercialEmisor { get; set; } = string.Empty;
    public string    DireccionEmisor       { get; set; } = string.Empty;

    // ── Receptor ──────────────────────────────────────────────
    public string    RucReceptor           { get; set; } = string.Empty;
    public string    RazonSocialReceptor   { get; set; } = string.Empty;
    public string    DireccionReceptor     { get; set; } = string.Empty;

    // ── Importes ──────────────────────────────────────────────
    public string    Moneda                { get; set; } = string.Empty;
    public decimal   BaseImponible         { get; set; }
    public decimal   TotalIgv              { get; set; }
    public decimal   TotalExonerado        { get; set; }
    public decimal   TotalInafecto         { get; set; }
    public decimal   TotalGratuito         { get; set; }
    public decimal   TotalPagar            { get; set; }

    // ── Pago ──────────────────────────────────────────────────
    public string    FormaPago             { get; set; } = string.Empty;
    public DateTime? FechaVencimiento      { get; set; }
    public decimal   MontoNetoPendiente    { get; set; }

    // ── Detracción ────────────────────────────────────────────
    public string    TieneDetraccion       { get; set; } = "N";
    public decimal   PctDetraccion         { get; set; }
    public decimal   MontoDetraccion       { get; set; }

    // ── Referencias cruzadas ──────────────────────────────────
    public string    NumeroPedido          { get; set; } = string.Empty;
    public string    NumeroGuia            { get; set; } = string.Empty;
    public string    NumeroDocRef          { get; set; } = string.Empty;

    // ── Transporte ────────────────────────────────────────────
    public string    ModalidadTraslado     { get; set; } = string.Empty;
    public string    MotivoTraslado        { get; set; } = string.Empty;
    public decimal   PesoBruto             { get; set; }
    public string    UnidadPeso            { get; set; } = string.Empty;
    public DateTime? FechaInicioTraslado   { get; set; }
    public string    RucTransportista      { get; set; } = string.Empty;
    public string    RazonSocTransportista { get; set; } = string.Empty;
    public string    PlacaVehiculo         { get; set; } = string.Empty;
    public string    NombreConductor       { get; set; } = string.Empty;

    // ── Control ───────────────────────────────────────────────
    public string    Estado                { get; set; } = string.Empty;
    public DateTime  FechaProcesamiento    { get; set; }
    public string    Observaciones         { get; set; } = string.Empty;

    // ── Conteos relacionados (SP_LISTAR_DOCUMENTOS) ───────────
    public int       CantLineas            { get; set; }
    public int       CantCuotas            { get; set; }
}
