namespace FabricaHilos.Sire.Models;

/// <summary>
/// DTO para una propuesta RVIE (Registro de Ventas e Ingresos)
/// Contiene los detalles de la propuesta desde SUNAT
/// </summary>
public class SirePropuestaVentasDto
{
    /// <summary>Período de la propuesta (YYYYMM)</summary>
    public string Periodo { get; set; }

    /// <summary>Estado actual de la propuesta</summary>
    public string Estado { get; set; }

    /// <summary>Descripción del estado</summary>
    public string EstadoDescripcion { get; set; }

    /// <summary>Ticket identificador de la propuesta</summary>
    public string Ticket { get; set; }

    /// <summary>Hash de validación de la propuesta</summary>
    public string Hash { get; set; }

    /// <summary>Fecha de generación de la propuesta</summary>
    public DateTime FechaGeneracion { get; set; }

    /// <summary>Total de comprobantes en la propuesta</summary>
    public int TotalComprobantes { get; set; }

    /// <summary>Total de monto en la propuesta (en soles)</summary>
    public decimal TotalMonto { get; set; }

    /// <summary>Errores encontrados en la propuesta (si los hay)</summary>
    public List<string> Errores { get; set; } = new();

    /// <summary>Advertencias encontradas en la propuesta (si las hay)</summary>
    public List<string> Advertencias { get; set; } = new();

    /// <summary>Detalle de líneas/registros (si aplica)</summary>
    public string DetalleJSON { get; set; }
}
