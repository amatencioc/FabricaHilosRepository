namespace FabricaHilos.Services.Produccion.Planeamiento;

using FabricaHilos.Models.Produccion.Planeamiento;

public interface IPlnRegistroService
{
    /// <summary>
    /// Devuelve todos los ítems de pedidos registrados en el rango de fechas indicado,
    /// con todos los datos de cabecera, artículo, familia y línea textil.
    /// </summary>
    Task<IReadOnlyList<RegistroPedidoItem>> GetRegistroDiarioAsync(
        DateTime fchDesde,
        DateTime fchHasta,
        string   filtroServ         = "",
        string   filtroCliente      = "",
        string   filtroProceso      = "",
        string   filtroEstado       = "",
        string   filtroTfibra       = "",
        string   filtroPasoActual   = "");
}
