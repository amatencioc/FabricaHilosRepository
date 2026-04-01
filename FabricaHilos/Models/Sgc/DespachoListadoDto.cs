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

        // Datos adicionales para descarga de PDF
        public string? FacturaTipo { get; set; }
        public string? FacturaSerie { get; set; }
        public string? GuiaCodAlm { get; set; }
        public string? GuiaTpTransac { get; set; }
        public int? GuiaSerie { get; set; }

        // Código de cliente para envío a TC
        public string? CodCliente { get; set; }

        // Información de factura ya enviada a TC
        public bool EnviadoATC { get; set; }
        public int? NumReqTC { get; set; }
        public string? NumCer { get; set; }
    }
}
