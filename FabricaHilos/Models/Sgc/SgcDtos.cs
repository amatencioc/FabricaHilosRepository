namespace FabricaHilos.Models.Sgc
{
    public class PedidoSgcDto
    {
        public int Serie { get; set; }
        public int NumPed { get; set; }
        public string? TipoDocto { get; set; }
        public string? Estado { get; set; }
        public DateTime? Fecha { get; set; }
        public string? CodCliente { get; set; }
        public string? Nombre { get; set; }
        public string? Ruc { get; set; }
        public string? Detalle { get; set; }
        public decimal? TotalPedido { get; set; }
        public decimal? TotalFacturado { get; set; }
        public string? CodVende { get; set; }
        public string? Moneda { get; set; }
        public string? NroSucur { get; set; }
    }

    public class ItemPedDto
    {
        public int Serie { get; set; }
        public int NumPed { get; set; }
        public int Nro { get; set; }
        public string? CodArt { get; set; }
        public string? Titulo { get; set; }
        public string? TipoFibra { get; set; }
        public string? Valpf { get; set; }
        public string? Color { get; set; }
        public decimal? Cantidad { get; set; }
        public decimal? Precio { get; set; }
        public decimal? Saldo { get; set; }
        public decimal? ImpVvb { get; set; }
        public string? Estado { get; set; }
        public string? Detalle { get; set; }
        public string? ColorDet { get; set; }
        public string? HiloDet { get; set; }
        public string? Proceso { get; set; }
        public string? Presentacion { get; set; }
        public string? RTipo { get; set; }
        public int? RSerie { get; set; }
        public int? RNumero { get; set; }
    }

    public class KardexGDto
    {
        public string CodAlm { get; set; } = string.Empty;
        public string TpTransac { get; set; } = string.Empty;
        public int Serie { get; set; }
        public int Numero { get; set; }
        public DateTime? FchTransac { get; set; }
        public string? Nombre { get; set; }
        public string? Ruc { get; set; }
        public string? Glosa { get; set; }
        public string? Estado { get; set; }
        public string? IndFact { get; set; }
        public decimal? PesoTotal { get; set; }
        public string? TipDocRef { get; set; }
        public string? SerDocRef { get; set; }
        public string? NroDocRef { get; set; }
        public string? TipRef { get; set; }
        public string? SerRef { get; set; }
        public string? NroRef { get; set; }
        public string? Motivo { get; set; }
        public string? Moneda { get; set; }
    }

    // NOTE: SIG.KARDEX_D columns are assumed based on ERP conventions.
    // Adjust column names in SgcService if they differ in your schema.
    public class KardexDDto
    {
        public string CodAlm { get; set; } = string.Empty;
        public string TpTransac { get; set; } = string.Empty;
        public int Serie { get; set; }
        public int Numero { get; set; }
        public int Nro { get; set; }
        public string? CodArt { get; set; }
        public string? Titulo { get; set; }
        public decimal? Cantidad { get; set; }
        public decimal? Precio { get; set; }
        public decimal? Importe { get; set; }
        public string? Estado { get; set; }
        public string? Detalle { get; set; }
    }

    // NOTE: SIG.DOCUVENT columns are assumed based on ERP conventions.
    // Adjust column names in SgcService if they differ in your schema.
    public class DocuVentDto
    {
        public string? Tipodoc { get; set; }
        public string? Serie { get; set; }
        public string? Numero { get; set; }
        public DateTime? Fecha { get; set; }
        public string? CodCliente { get; set; }
        public string? Nombre { get; set; }
        public string? Ruc { get; set; }
        public decimal? Total { get; set; }
        public string? Estado { get; set; }
        public string? Glosa { get; set; }
        public string? Moneda { get; set; }
    }

    // NOTE: SIG.ITEMDOCU columns are assumed based on ERP conventions.
    // Adjust column names in SgcService if they differ in your schema.
    public class ItemDocuDto
    {
        public string? Tipodoc { get; set; }
        public string? Serie { get; set; }
        public string? Numero { get; set; }
        public int Nro { get; set; }
        public string? CodArt { get; set; }
        public string? Titulo { get; set; }
        public decimal? Cantidad { get; set; }
        public decimal? Precio { get; set; }
        public decimal? Importe { get; set; }
        public string? Detalle { get; set; }
    }
}
