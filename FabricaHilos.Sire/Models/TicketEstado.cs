using System.Text.Json.Serialization;

namespace FabricaHilos.Sire.Models;

/// <summary>
/// Datos del archivo generado por un proceso asíncrono SIRE.
/// Devuelto en el campo archivoReporte del servicio 5.16 (consulta de estado de ticket).
/// Según manual SIRE v25 pág 43-46.
/// </summary>
public sealed class ArchivoReporteDto
{
    /// <summary>Nombre del archivo generado (ej: PP20100096260202601140000001111110.ZIP).</summary>
    [JsonPropertyName("nomArchivoReporte")]
    public string? NomArchivoReporte { get; set; }

    /// <summary>Código de tipo de archivo (ej: "ZIP", "PDF"). Puede ser null.</summary>
    [JsonPropertyName("codTipoArchivoReporte")]
    public string? CodTipoArchivoReporte { get; set; }
}

/// <summary>
/// Modelo normalizado de estado de ticket. Se popula a partir del wrapper paginado
/// devuelto por el servicio 5.16 (consultaestadotickets) vía <see cref="TicketConsultaResponse.ToTicketEstado"/>.
/// Para operaciones que solo devuelven numTicket (exportar, aceptar, etc.) solo se llena NumTicket.
/// Según manual SIRE Ventas v25 pág 43-46.
/// </summary>
public sealed class TicketEstado
{
    [JsonPropertyName("numTicket")]
    public string NumTicket { get; set; } = string.Empty;

    /// <summary>Estado textual del ticket: EN_PROCESO, COMPLETADO, ERROR, RECHAZADO. Mapeado de desEstadoProceso.</summary>
    public string Estado { get; set; } = string.Empty;

    /// <summary>Código numérico de estado de envío (codEstadoProceso). 3=completado, 4=completado con errores.</summary>
    public string CodEstadoProceso { get; set; } = string.Empty;

    /// <summary>Código del indicador de carga masiva (codProceso). Requerido por servicio 5.17 para descarga.</summary>
    public string CodProceso { get; set; } = string.Empty;

    /// <summary>Período tributario del ticket (ej: "202601"). Requerido por servicio 5.17.</summary>
    public string PerTributario { get; set; } = string.Empty;

    /// <summary>Nombre del archivo de importación original. Requerido por servicio 5.17.</summary>
    public string? NomArchivoImportacion { get; set; }

    /// <summary>Mensaje descriptivo del estado (desEstadoEnvio del detalleTicket).</summary>
    public string Mensaje { get; set; } = string.Empty;

    /// <summary>
    /// Datos del archivo generado. Disponible cuando Estado=COMPLETADO.
    /// Mapeado desde registros[0].archivoReporte[0] o registros[0].detalleTicket.nomArchivoReporte.
    /// </summary>
    [JsonPropertyName("archivoReporte")]
    public ArchivoReporteDto? ArchivoReporte { get; set; }

    /// <summary>Alias de NumTicket para compatibilidad con código existente.</summary>
    [JsonIgnore]
    public string Ticket => NumTicket;

    /// <summary>
    /// True cuando SUNAT ha terminado de procesar el ticket (exitoso o con error).
    /// RVIE devuelve "COMPLETADO"; RCE devuelve "Terminado" (misma semántica, distinto literal).
    /// Los códigos 3 y 4 son la representación numérica del estado finalizado.
    /// </summary>
    public bool EsFinal =>
        string.Equals(Estado, "COMPLETADO", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Estado, "TERMINADO", StringComparison.OrdinalIgnoreCase)   // RCE devuelve "Terminado"
        || string.Equals(Estado, "ERROR", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Estado, "RECHAZADO", StringComparison.OrdinalIgnoreCase)
        || CodEstadoProceso == "3"
        || CodEstadoProceso == "4";

    /// <summary>True cuando el ticket finalizó con éxito (archivo generado esperado).</summary>
    public bool EsExito =>
        string.Equals(Estado, "COMPLETADO", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Estado, "TERMINADO", StringComparison.OrdinalIgnoreCase)
        || CodEstadoProceso == "3";
}
