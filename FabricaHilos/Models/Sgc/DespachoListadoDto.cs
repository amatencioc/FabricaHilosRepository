namespace FabricaHilos.Models.Sgc
{
    public class DespachoListadoDto
    {
        public int Correlativo { get; set; }
        public string? RazonSocial { get; set; }
        public string? Oc { get; set; }
        public string? Pedido { get; set; }
        public string? Factura { get; set; }
        public DateTime? FechaDoc { get; set; }
        public string? Articulo { get; set; }
        public decimal? Cantidad { get; set; }
        public decimal? CantFacturada { get; set; }
        public decimal? Precio { get; set; }
        public int? Guia { get; set; }
        public string? Obs { get; set; }
    }
}
