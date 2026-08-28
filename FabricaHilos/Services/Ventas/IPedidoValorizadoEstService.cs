using FabricaHilos.Models.Ventas;

namespace FabricaHilos.Services.Ventas
{
    public interface IPedidoValorizadoEstService
    {
        Task<List<PedidoValorizadoEstDto>> ListarAsync(PedidoValorizadoEstFiltroDto filtro);
        Task<List<VendedorDto>> ObtenerVendedoresAsync();
        Task<List<Select2ItemDto>> BuscarClientesAsync(string term);
        Task<List<Select2ItemDto>> BuscarArticulosAsync(string term);
    }
}
