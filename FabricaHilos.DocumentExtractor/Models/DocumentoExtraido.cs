namespace FabricaHilos.DocumentExtractor.Models;

public class DocumentoExtraido
{
    public string NombreArchivo { get; set; } = string.Empty;
    public string? TipoDocumento { get; set; }
    public string? Serie { get; set; }
    public string? Correlativo { get; set; }
    public string? NumeroDocumento { get; set; }
    public DateTime? FechaEmision { get; set; }
    public TimeSpan? HoraEmision { get; set; }
    public DateTime? FechaVencimiento { get; set; }

    // Emisor
    public string? RucEmisor { get; set; }
    public string? RazonSocialEmisor { get; set; }
    public string? NombreComercialEmisor { get; set; }
    public string? DireccionEmisor { get; set; }

    // Receptor
    public string? RucReceptor { get; set; }
    public string? RazonSocialReceptor { get; set; }
    public string? DireccionReceptor { get; set; }

    // Montos
    public string Moneda { get; set; } = "PEN";
    public decimal? BaseImponible { get; set; }
    public decimal? TotalIgv { get; set; }
    public decimal? TotalExonerado { get; set; }
    public decimal? TotalInafecto { get; set; }
    public decimal? TotalGratuito { get; set; }
    public decimal? TotalDescuento { get; set; }
    public decimal? TotalCargo { get; set; }
    public decimal? TotalAnticipos { get; set; }
    public decimal? TotalPagar { get; set; }

    // Pago
    public string? FormaPago { get; set; }
    public decimal? MontoNetoPendiente { get; set; }

    // Detracción
    public bool TieneDetraccion { get; set; }
    public decimal? PctDetraccion { get; set; }
    public decimal? MontoDetraccion { get; set; }

    // Referencias
    public string? NumeroPedido { get; set; }
    public string? NumeroGuia { get; set; }
    public string? NumeroDocRef { get; set; }

    // Traslado
    public string? ModalidadTraslado { get; set; }
    public string? MotivoTraslado { get; set; }
    public string? ModoTransporte { get; set; }
    public decimal? PesoBruto { get; set; }
    public string? UnidadPeso { get; set; }
    public DateTime? FechaInicioTraslado { get; set; }
    public DateTime? FechaFinTraslado { get; set; }

    // Transporte
    public string? RucTransportista { get; set; }
    public string? RazonSocTransportista { get; set; }
    public string? NombreConductor { get; set; }
    public string? LicenciaConductor { get; set; }
    public string? PlacaVehiculo { get; set; }
    public string? MarcaVehiculo { get; set; }
    public string? NroDocConductor { get; set; }

    // Origen/Destino
    public string? UbigeoOrigen { get; set; }
    public string? DirOrigen { get; set; }
    public string? UbigeoDestino { get; set; }
    public string? DirDestino { get; set; }

    // Misc
    public string? Vendedor { get; set; }
    public string Estado { get; set; } = "PENDIENTE";
    public DateTime FechaProcesamiento { get; set; } = DateTime.Now;
    public string? Observaciones { get; set; }

    // Metadatos de extracción
    public string FuenteExtraccion { get; set; } = "PdfPig";
    public double Confianza { get; set; } = 1.0;
    public string? MensajeError { get; set; }

    public List<ItemDocumento> Items { get; set; } = new();
}

public class ItemDocumento
{
    public string? Codigo { get; set; }
    public string? Descripcion { get; set; }
    public string? UnidadMedida { get; set; }
    public decimal? Cantidad { get; set; }
    public decimal? ValorUnitario { get; set; }
    public decimal? Descuento { get; set; }
    public decimal? ValorVenta { get; set; }
}
