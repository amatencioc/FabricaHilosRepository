namespace FabricaHilos.Sire.Models;

/// <summary>
/// DTO normalizado para un período RCE (Registro de Compras y Gastos)
/// Agrupa información del estado del período desde la perspectiva de SUNAT
/// </summary>
public class SirePeriodoComprasDto
{
    /// <summary>Período en formato YYYYMM (ej: 202601 para enero 2026)</summary>
    public string Periodo { get; set; }

    /// <summary>Estado SUNAT del período (PROPUESTA_DISPONIBLE, CERRADO, EN_PROCESO, SIN_INFORMACION)</summary>
    public string Estado { get; set; }

    /// <summary>Descripción legible del estado</summary>
    public string EstadoDescripcion { get; set; }

    /// <summary>Indica si hay una propuesta disponible que se puede aceptar/rechazar</summary>
    public bool TienePropuestaDisponible { get; set; }

    /// <summary>Indica si el período ya está cerrado</summary>
    public bool EstaCerrado { get; set; }

    /// <summary>Último ticket de operación (si existe)</summary>
    public string UltimoTicket { get; set; }

    /// <summary>Fecha de la última operación</summary>
    public DateTime? FechaUltimaOperacion { get; set; }

    /// <summary>Número de registros en el período</summary>
    public int NumeroRegistros { get; set; }

    /// <summary>Observaciones o notas sobre el período</summary>
    public string Observaciones { get; set; }
}
