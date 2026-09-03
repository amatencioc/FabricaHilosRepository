namespace FabricaHilos.Models.Produccion.Planeamiento;

/// <summary>
/// Fila de una de las 3 secciones del reporte "Ingreso de Pedidos Aprobados por Grupo de Fibra"
/// (PKG_PLN.SP_PLN_INGRESO_PED_APROB_FIBRA). Representa el KG aprobado agrupado por
/// cliente/grupo de fibra/tipo (producción, solo despacho o servicios).
/// </summary>
public class PlnIngresoFibraItem
{
    public string Orden  { get; set; } = "";
    public string Cliente { get; set; } = "";
    public string CodCliente { get; set; } = "";
    public string NomCliente { get; set; } = "";
    public string Grupo  { get; set; } = "";
    public string Tipo   { get; set; } = "";
    public string CargaTrabajoEnconado { get; set; } = "";
    public decimal Kg    { get; set; }
}

/// <summary>
/// Contenedor con las 3 secciones devueltas por SP_PLN_INGRESO_PED_APROB_FIBRA para un rango de fechas.
/// </summary>
public class PlnIngresoPedidoAprobadoFibraViewModel
{
    public DateTime FchIni { get; set; }
    public DateTime FchFin { get; set; }
    public string TipoCliente { get; set; } = "TODOS";

    public List<PlnIngresoFibraItem> Produccion { get; set; } = [];
    public List<PlnIngresoFibraItem> SoloDespacho { get; set; } = [];
    public List<PlnIngresoFibraItem> Servicios { get; set; } = [];

    public decimal TotalProduccion   => Produccion.Sum(x => x.Kg);
    public decimal TotalSoloDespacho => SoloDespacho.Sum(x => x.Kg);
    public decimal TotalServicios    => Servicios.Sum(x => x.Kg);
    public decimal TotalGeneral      => TotalProduccion + TotalSoloDespacho + TotalServicios;
}

/// <summary>
/// Contenedor con las 3 variantes (TODOS/CLIENTE/ALMACEN) del reporte, usado para permitir
/// que cada gráfico de la vista cambie de filtro Cliente sin recargar la página.
/// </summary>
public class PlnIngresoPedidoAprobadoFibraVariantesViewModel
{
    public PlnIngresoPedidoAprobadoFibraViewModel Todos   { get; set; } = new();
    public PlnIngresoPedidoAprobadoFibraViewModel Cliente { get; set; } = new();
    public PlnIngresoPedidoAprobadoFibraViewModel Almacen { get; set; } = new();
}
