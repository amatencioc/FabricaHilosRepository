namespace FabricaHilos.Models.Produccion.Planeamiento;

/// <summary>Cliente con pedidos activos. SP_PLN_FILTRO_CLIENTES → (COD_CLIENTE, NOMBRE).</summary>
public class PlnFiltroCliente
{
    public string? CodCliente { get; set; }
    public string? Nombre     { get; set; }
}

/// <summary>Asesor/vendedor con pedidos activos. SP_PLN_FILTRO_ASESORES → (COD_VENDE, ABREVIADA, NOMBRE).</summary>
public class PlnFiltroAsesor
{
    public string? CodVende   { get; set; }
    public string? Abreviada  { get; set; }
    public string? Nombre     { get; set; }
}

/// <summary>Título de hilo usado en ítems activos. SP_PLN_FILTRO_TITULOS → (TITULO, DESCRIPCION).</summary>
public class PlnFiltroTitulo
{
    public string? Titulo      { get; set; }
    public string? Descripcion { get; set; }
}

/// <summary>Fibra usada en ítems activos. SP_PLN_FILTRO_FIBRAS → (TIPO_FIBRA, ABREVIADO, DESCRIPCION).</summary>
public class PlnFiltroFibra
{
    public string? TipoFibra   { get; set; }
    public string? Abreviado   { get; set; }
    public string? Descripcion { get; set; }
}

/// <summary>Proceso de producción usado en ítems activos. SP_PLN_FILTRO_PROCESOS → (PROCESO, DESCRIPCION).</summary>
public class PlnFiltroProceso
{
    public string? Proceso     { get; set; }
    public string? Descripcion { get; set; }
}
