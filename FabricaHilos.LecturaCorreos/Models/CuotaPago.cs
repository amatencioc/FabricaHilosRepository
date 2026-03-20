namespace FabricaHilos.LecturaCorreos.Models;

public class CuotaPago
{
    public string    NumeroCuota      { get; set; } = string.Empty;
    public DateTime? FechaVencimiento { get; set; }
    public decimal   Monto            { get; set; }
    public string    Moneda           { get; set; } = string.Empty;
}
