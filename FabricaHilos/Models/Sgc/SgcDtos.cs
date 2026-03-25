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
        public decimal? TotalDespacho { get; set; }
        public string? UnidadDespacho { get; set; }
        public bool TieneDetalle { get; set; }
        public bool TienePacking  { get; set; }
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
        public decimal? SaldoR { get; set; }
        public string? Descripcion { get; set; }
        public string? Unidad { get; set; }
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
        public string? SerieSunat { get; set; }
        public bool TieneDetalle { get; set; }
    }

    // NOTE: SIG.KARDEX_D
    // Adjust column names in SgcService if they differ in your schema.
    public class KardexDDto
    {
        public string CodAlm { get; set; } = string.Empty;
        public string TpTransac { get; set; } = string.Empty;
        public int Serie { get; set; }
        public int Numero { get; set; }
        public int Nro { get; set; }
        public int? IpNro { get; set; }
        public string? CodArt { get; set; }
        public string? Titulo { get; set; }
        public decimal? Cantidad { get; set; }
        public decimal? Precio { get; set; }
        public decimal? Importe { get; set; }
        public string? Estado { get; set; }
        public string? Detalle { get; set; }
        public string? Descripcion { get; set; }
        public string? Unidad { get; set; }
        public string? ColorDet { get; set; }
    }

    public class SalidaInternaDto
    {
        public string CodAlm { get; set; } = string.Empty;
        public string TpTransac { get; set; } = string.Empty;
        public int Serie { get; set; }
        public int Numero { get; set; }
        public DateTime? FchTransac { get; set; }
        public string? Nombre { get; set; }
        public string? Ruc { get; set; }
        public string? Glosa { get; set; }
        public decimal? PesoTotal { get; set; }
        public string? TipRef { get; set; }
        public string? SerRef { get; set; }
        public string? NroRef { get; set; }
        public string? NroDocRef { get; set; }
        public string? Motivo { get; set; }
        public int? NroBultos { get; set; }
        public string? NomTranspor { get; set; }
        public string? NroTranspor { get; set; }
        public string? NomVehiculo { get; set; }
        public string? DirPartida { get; set; }
        public string? DirLlegada { get; set; }
        public DateTime? FchEntrega { get; set; }
        public string? ModTraslado { get; set; }
        public List<SalidaInternaItemDto> Items { get; set; } = [];
    }

    public class SalidaInternaItemDto
    {
        public string? CodArt { get; set; }
        public string? Descripcion { get; set; }
        public string? Unidad { get; set; }
        public decimal? Cantidad { get; set; }
    }

    // NOTE: SIG.DOCUVENT
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
        public decimal? ValVenta { get; set; }
        public decimal? ImpIgv { get; set; }
        public decimal? PrecioVta { get; set; }
        public bool TieneDetalle { get; set; }
    }

    public class PackingGDto
    {
        public string? Tipo         { get; set; }
        public int     Serie        { get; set; }
        public int     Numero       { get; set; }
        public string? Observacion  { get; set; }
        public string? SerRef       { get; set; }
        public string? NroRef       { get; set; }
        public int     NumPed       { get; set; }
        public string? NumOrdcompra { get; set; }
    }

    // NOTE: SIG.ITEMDOCU
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
        public string? Descripcion { get; set; }
    }
}
