namespace FabricaHilos.Models.CreditosCobranza;

public class NivelMorosidadDto
{
    public int    Ano        { get; set; }
    public int    Mes        { get; set; }
    public decimal SaldoSoles  { get; set; }
    public decimal VencSoles   { get; set; }
    public decimal IndSoles    { get; set; }
    public decimal SaldoDolar  { get; set; }
    public decimal VencDolar   { get; set; }
    public decimal IndDolar    { get; set; }
}
