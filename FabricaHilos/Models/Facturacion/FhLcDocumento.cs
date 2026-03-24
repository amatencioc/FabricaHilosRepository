using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FabricaHilos.Models.Facturacion;

[Table("FH_LC_DOCUMENTO")]
public class FhLcDocumento
{
    [Key]
    public int Id { get; set; }

    [MaxLength(260)]
    public string NombreArchivo { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? TipoDocumento { get; set; }

    [MaxLength(10)]
    public string? Serie { get; set; }

    [MaxLength(20)]
    public string? Correlativo { get; set; }

    [MaxLength(30)]
    public string? NumeroDocumento { get; set; }

    public DateTime? FechaEmision { get; set; }
    public TimeSpan? HoraEmision { get; set; }
    public DateTime? FechaVencimiento { get; set; }

    // Emisor
    [MaxLength(20)]
    public string? RucEmisor { get; set; }

    [MaxLength(200)]
    public string? RazonSocialEmisor { get; set; }

    [MaxLength(200)]
    public string? NombreComercialEmisor { get; set; }

    [MaxLength(400)]
    public string? DireccionEmisor { get; set; }

    // Receptor
    [MaxLength(20)]
    public string? RucReceptor { get; set; }

    [MaxLength(200)]
    public string? RazonSocialReceptor { get; set; }

    [MaxLength(400)]
    public string? DireccionReceptor { get; set; }

    // Montos
    [MaxLength(10)]
    public string Moneda { get; set; } = "PEN";

    [Column(TypeName = "decimal(18,2)")]
    public decimal? BaseImponible { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? TotalIgv { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? TotalExonerado { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? TotalInafecto { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? TotalGratuito { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? TotalDescuento { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? TotalCargo { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? TotalAnticipos { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? TotalPagar { get; set; }

    // Pago
    [MaxLength(100)]
    public string? FormaPago { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? MontoNetoPendiente { get; set; }

    // Detracción
    public bool TieneDetraccion { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? PctDetraccion { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? MontoDetraccion { get; set; }

    // Referencias
    [MaxLength(100)]
    public string? NumeroPedido { get; set; }

    [MaxLength(50)]
    public string? NumeroGuia { get; set; }

    [MaxLength(50)]
    public string? NumeroDocRef { get; set; }

    // Traslado
    [MaxLength(50)]
    public string? ModalidadTraslado { get; set; }

    [MaxLength(100)]
    public string? MotivoTraslado { get; set; }

    [MaxLength(50)]
    public string? ModoTransporte { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? PesoBruto { get; set; }

    [MaxLength(10)]
    public string? UnidadPeso { get; set; }

    public DateTime? FechaInicioTraslado { get; set; }
    public DateTime? FechaFinTraslado { get; set; }

    // Transporte
    [MaxLength(20)]
    public string? RucTransportista { get; set; }

    [MaxLength(200)]
    public string? RazonSocTransportista { get; set; }

    [MaxLength(200)]
    public string? NombreConductor { get; set; }

    [MaxLength(30)]
    public string? LicenciaConductor { get; set; }

    [MaxLength(20)]
    public string? PlacaVehiculo { get; set; }

    [MaxLength(100)]
    public string? MarcaVehiculo { get; set; }

    [MaxLength(20)]
    public string? NroDocConductor { get; set; }

    // Origen/Destino
    [MaxLength(10)]
    public string? UbigeoOrigen { get; set; }

    [MaxLength(400)]
    public string? DirOrigen { get; set; }

    [MaxLength(10)]
    public string? UbigeoDestino { get; set; }

    [MaxLength(400)]
    public string? DirDestino { get; set; }

    // Misc
    [MaxLength(200)]
    public string? Vendedor { get; set; }

    [MaxLength(30)]
    public string Estado { get; set; } = "PENDIENTE";

    public DateTime FechaProcesamiento { get; set; } = DateTime.Now;

    [MaxLength(1000)]
    public string? Observaciones { get; set; }

    // Metadatos de extracción
    [MaxLength(50)]
    public string FuenteExtraccion { get; set; } = "PdfPig";

    public double Confianza { get; set; } = 1.0;

    [MaxLength(500)]
    public string? MensajeError { get; set; }
}
