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
}
