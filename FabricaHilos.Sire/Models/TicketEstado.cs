using System.Text.Json.Serialization;

namespace FabricaHilos.Sire.Models;

/// <summary>
/// Respuesta de operaciones que generan tickets (aceptar propuesta, registrar preliminar, etc.).
/// Según manual v25 pág 34 (servicio 5.8).
/// </summary>
public sealed class TicketEstado
{
    [JsonPropertyName("numTicket")]
    public string NumTicket { get; set; } = string.Empty;

    /// <summary>Estado del ticket (EN_PROCESO, COMPLETADO, ERROR, RECHAZADO). Usado en consultas de estado.</summary>
    public string Estado { get; set; } = string.Empty;

    /// <summary>Mensaje descriptivo del estado. Usado en consultas de estado.</summary>
    public string Mensaje { get; set; } = string.Empty;

    /// <summary>Alias de NumTicket para compatibilidad con código existente.</summary>
    [JsonIgnore]
    public string Ticket => NumTicket;

    public bool EsFinal =>
        string.Equals(Estado, "COMPLETADO", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Estado, "ERROR", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Estado, "RECHAZADO", StringComparison.OrdinalIgnoreCase);
}
