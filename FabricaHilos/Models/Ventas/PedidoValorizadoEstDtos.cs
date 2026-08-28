namespace FabricaHilos.Models.Ventas
{
    /// <summary>Una fila devuelta por PKG_PED_VAL_EST.SP_LISTADO_PEDIDOS.</summary>
    public class PedidoValorizadoEstDto
    {
        public string?  CodCliente        { get; set; }
        public string?  Nombre            { get; set; }
        public DateTime? Fecha            { get; set; }
        public DateTime? Entrega          { get; set; }
        public int      Nro               { get; set; }
        public long     NumPed            { get; set; }
        public string?  NumeroRef         { get; set; }
        public string?  CodArt            { get; set; }
        public string?  Unidad            { get; set; }
        public string?  Descripcion       { get; set; }
        public decimal  StockAct          { get; set; }
        public decimal  StockLote         { get; set; }
        public string?  Clieref           { get; set; }
        public string?  Estado            { get; set; }
        public string?  TipoDocto         { get; set; }
        public int      Serie             { get; set; }
        public string?  Moneda            { get; set; }
        public decimal  Impsol            { get; set; }
        public decimal  Impdol            { get; set; }
        public decimal  Soles             { get; set; }
        public decimal  Cantidad          { get; set; }
        public int      Dias              { get; set; }
        public decimal  Despachado        { get; set; }
        public decimal  Saldo             { get; set; }
        public string?  Detalle           { get; set; }
        public DateTime? FentregaMod      { get; set; }
        public string?  FchEntregaMinmax { get; set; }
        public string?  EstatusDescripcion { get; set; }
        public string?  CPago             { get; set; }
        public decimal  AnticipoSaldo     { get; set; }
        /// <summary>'S' = contraentrega (COND_PAG='AA') sin anticipo -> resaltar fila en rosado.</summary>
        public string   IndSinAnticipo    { get; set; } = "N";
    }

    /// <summary>Filtros del listado, todos opcionales (default = sin restricción, igual que el procedure).</summary>
    public class PedidoValorizadoEstFiltroDto
    {
        public string? Cliente       { get; set; }
        public bool    ExcluirAlmacen{ get; set; }
        public string? OpcFPedido    { get; set; } = "A LA FECHA";
        public DateTime? FechaI      { get; set; }
        public DateTime? FechaF      { get; set; }
        public string? OpcFEntrega   { get; set; } = "TODOS";
        public DateTime? FecEntIni   { get; set; }
        public DateTime? FecEntFin   { get; set; }
        public string? OpcPais       { get; set; } = "TODOS";
        public string? Vendedor      { get; set; }
        public string? Articulo      { get; set; }
        public string? NumPed        { get; set; }
        public string? Nro           { get; set; }
        public string? OCompra       { get; set; }
        public string? Material      { get; set; }
        public decimal Cambio        { get; set; } = 3.75m;
    }

    /// <summary>Item genérico para combos select2 (código/vendedor/artículo).</summary>
    public class Select2ItemDto
    {
        public string Id   { get; set; } = "";
        public string Text { get; set; } = "";
    }

    public class VendedorDto
    {
        public string CodVendedor { get; set; } = "";
        public string Nombre      { get; set; } = "";
    }
}
