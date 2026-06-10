using FabricaHilos.Models.Produccion.Planeamiento;

namespace FabricaHilos.Services.Produccion.Planeamiento;

public interface IPlnReporteService
{
    /// <summary>
    /// Llama a PKG_PLN.SP_PLN_SEG_PROG_TINTORERIA y devuelve la lista de ítems.
    /// </summary>
    /// <param name="opc">
    ///   'POR FECHA DE ENTREGA' | 'POR PEDIDO' | 'POR FECHA DE PROGRAMA' |
    ///   'POR FECHA DE TEÑIDO'  | 'POR FECHA APROB PEDIDO'
    /// </param>
    Task<IEnumerable<PlnReporteProduccion>> GetReporteProduccionAsync(
        string            opc,
        DateTime?         fechaIni          = null,
        DateTime?         fechaFin          = null,
        long?             numPed            = null,
        string            cliente           = "%",
        string            asesor            = "%",
        string            titulo            = "%",
        string            fibra             = "%",
        string            proceso           = "%",
        CancellationToken ct                = default);

    /// <summary>Clientes con pedidos activos para combo. SP_PLN_FILTRO_CLIENTES.</summary>
    Task<IEnumerable<PlnFiltroCliente>> GetFiltroClientesAsync();

    /// <summary>Asesores/vendedores con pedidos activos para combo. SP_PLN_FILTRO_ASESORES.</summary>
    Task<IEnumerable<PlnFiltroAsesor>> GetFiltroAsesoresAsync();

    /// <summary>Títulos usados en ítems activos para combo. SP_PLN_FILTRO_TITULOS.</summary>
    Task<IEnumerable<PlnFiltroTitulo>> GetFiltroTitulosAsync();

    /// <summary>Fibras usadas en ítems activos para combo. SP_PLN_FILTRO_FIBRAS.</summary>
    Task<IEnumerable<PlnFiltroFibra>> GetFiltroFibrasAsync();

    /// <summary>Procesos de producción usados en ítems activos para combo. SP_PLN_FILTRO_PROCESOS.</summary>
    Task<IEnumerable<PlnFiltroProceso>> GetFiltroProcesosAsync();
}

