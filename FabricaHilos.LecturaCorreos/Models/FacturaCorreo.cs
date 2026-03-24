namespace FabricaHilos.LecturaCorreos.Models;

public class FacturaCorreo
{
    public long Id { get; set; }

    // Datos del comprobante
    public string Ruc { get; set; } = string.Empty;
    public string TipoComprobante { get; set; } = string.Empty; // "01"=Factura, "03"=Boleta
    public string Serie { get; set; } = string.Empty;
    public int Correlativo { get; set; }

    // Estado general del registro
    public string Estado { get; set; } = "PENDIENTE_CDR"; // PENDIENTE_CDR | ACEPTADO | RECHAZADO | ERROR

    // Datos retornados por SUNAT
    public string? CodigoRespuestaSunat { get; set; }
    public string? MensajeSunat { get; set; }
    public byte[]? CdrContenido { get; set; }  // ZIP del CDR en binario
    public string? MensajeError { get; set; }

    // Fechas y control
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaConsultaSunat { get; set; }
    public int Intentos { get; set; }

    // Referencia al documento/factura que originó este registro (enlace al documento)
    public long? DocumentoId { get; set; }           // FK hacia tabla de documentos/facturas emitidas
    public string? DocumentoReferencia { get; set; } // Ej: número de pedido, guía, etc.

    /// <summary>
    /// RUC de la empresa receptora (LaColonial, etc.) obtenido de FH_LC_DOCUMENTO.
    /// Es el RUC que se usa para autenticarse en SUNAT al consultar el CDR.
    /// Distinto de <see cref="Ruc"/> que es el RUC del EMISOR del comprobante.
    /// </summary>
    public string? RucReceptor { get; set; }
}
