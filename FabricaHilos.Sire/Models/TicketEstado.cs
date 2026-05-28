namespace FabricaHilos.Sire.Models;

public sealed class TicketEstado
{
    public string Ticket { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;

    public bool EsFinal =>
        string.Equals(Estado, "COMPLETADO", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Estado, "ERROR", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Estado, "RECHAZADO", StringComparison.OrdinalIgnoreCase);
}
