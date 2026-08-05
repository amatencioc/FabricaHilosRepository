namespace FabricaHilos.Models.CreditosCobranza;

public class ValorizadoNoVendidoDto
{
    public int    Ano                  { get; set; }
    public int    Mes                  { get; set; }
    public string NombreMes            { get; set; } = string.Empty;
    public decimal KgVendidos          { get; set; }
    public decimal Valorizado          { get; set; }
    public decimal Promedio            { get; set; }
    public decimal CanToneladasKg      { get; set; }
    public decimal DiferenciaKg        { get; set; }
    public decimal ValorizadoNoVendido { get; set; }
}
