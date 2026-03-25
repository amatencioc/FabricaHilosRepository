namespace FabricaHilos.Models.Sgc
{
    public class DashKpiDto
    {
        public int TotalPedidos { get; set; }
        public decimal TotalPedido { get; set; }
        public decimal TotalPendiente { get; set; }
    }

    public class DashEstadoDto
    {
        public string? Estado { get; set; }
        public string? DescEstado { get; set; }
        public int Cantidad { get; set; }
        public decimal Total { get; set; }
    }

    public class DashEvolucionDto
    {
        public string? Mes { get; set; }
        public string? Moneda { get; set; }
        public int NumPedidos { get; set; }
        public decimal TotalPedido { get; set; }
    }

    public class DashTopClienteDto
    {
        public string? CodCliente { get; set; }
        public string? Nombre { get; set; }
        public int NumPedidos { get; set; }
        public decimal TotalPedido { get; set; }
        public decimal PesoTotal { get; set; }
    }

    public class DashTopArticuloDto
    {
        public string? CodArt { get; set; }
        public string? Descripcion { get; set; }
        public decimal CantPedida { get; set; }
        public decimal CantDespachada { get; set; }
        public decimal CantPendiente { get; set; }
        public decimal ValorTotal { get; set; }
    }

    public class DashVendedorDto
    {
        public string? CodVende { get; set; }
        public string? NombreVendedor { get; set; }
        public string? Moneda { get; set; }
        public int NumPedidos { get; set; }
        public decimal TotalPedido { get; set; }
    }

    public class DashMonedaDto
    {
        public string? Moneda { get; set; }
        public int NumPedidos { get; set; }
        public decimal TotalPedido { get; set; }
    }

    public class DashSucursalClienteDto
    {
        public string? NroSucur { get; set; }
        public string? NombreSucursal { get; set; }
        public string? Ciudad { get; set; }
        public string? Distrito { get; set; }
        public int NumPedidos { get; set; }
        public decimal TotalPedido { get; set; }
    }

    public class DashDespachoDto
    {
        public string? Mes { get; set; }
        public int NumGuias { get; set; }
        public decimal PesoTotal { get; set; }
    }

    public class DashPedidoRiesgoDto
    {
        public int NumPed { get; set; }
        public int Serie { get; set; }
        public string? Nombre { get; set; }
        public DateTime? Fecha { get; set; }
        public int DiasEmitido { get; set; }
        public decimal TotalPedido { get; set; }
        public string? CodVende { get; set; }
    }

    // ── Nuevos reportes BI ─────────────────────────────────────

    public class DashTicketClienteDto
    {
        public string? CodCliente { get; set; }
        public string? Nombre { get; set; }
        public int NumPedidos { get; set; }
        public decimal TotalPedido { get; set; }
        public decimal TicketPromedio { get; set; }
    }

    public class DashCicloDto
    {
        public string? Mes { get; set; }
        public int NumPedidos { get; set; }
        public decimal DiasPromCierre { get; set; }
    }

    public class DashRecompraDto
    {
        public string? Frecuencia { get; set; }
        public int NumClientes { get; set; }
        public int NumPedidosTotal { get; set; }
        public decimal TotalPedido { get; set; }
    }

    public class DashConcentracionDto
    {
        public string? Segmento { get; set; }
        public int NumClientes { get; set; }
        public decimal TotalPedido { get; set; }
    }

    public class DashZonaDto
    {
        public string? Ciudad { get; set; }
        public int NumPedidos { get; set; }
        public decimal TotalPedido { get; set; }
    }

    public class DashMixDto
    {
        public string? Categoria { get; set; }
        public decimal CantPedida { get; set; }
        public decimal ValorTotal { get; set; }
    }

    public class DashMixProductoResultDto
    {
        public List<DashMixDto> Fibra        { get; set; } = [];
        public List<DashMixDto> Color        { get; set; } = [];
        public List<DashMixDto> Presentacion { get; set; } = [];
    }
}
