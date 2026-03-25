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
}
