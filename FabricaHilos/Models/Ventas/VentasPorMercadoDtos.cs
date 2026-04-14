namespace FabricaHilos.Models.Ventas
{
    /// <summary>Venta agrupada por mercado geográfico (Perú / Latam / Global).</summary>
    public class VentaMercadoDto
    {
        public string? Mercado { get; set; }
        public decimal Importe { get; set; }
    }

    /// <summary>Detalle de venta por país dentro de un mercado.</summary>
    public class VentaMercadoPaisDto
    {
        public string? Mercado { get; set; }
        public string? CodigoPais { get; set; }
        public string? Pais { get; set; }
        public decimal Importe { get; set; }
    }

    /// <summary>Detalle de venta por departamento (solo Perú).</summary>
    public class VentaMercadoDepartamentoDto
    {
        public string? Departamento { get; set; }
        public decimal Importe { get; set; }
    }
}
