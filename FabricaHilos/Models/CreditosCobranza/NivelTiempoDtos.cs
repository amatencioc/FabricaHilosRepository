namespace FabricaHilos.Models.CreditosCobranza;

public class NivelTiempoDto
{
    public int     Ano        { get; set; }
    public int     Mes        { get; set; }
    public decimal SaldoSoles { get; set; }
    public decimal VtaSoles   { get; set; }
    public decimal IndSoles   { get; set; }
    public decimal SaldoDolar { get; set; }
    public decimal VtaDolar   { get; set; }
    public decimal IndDolar   { get; set; }
}
