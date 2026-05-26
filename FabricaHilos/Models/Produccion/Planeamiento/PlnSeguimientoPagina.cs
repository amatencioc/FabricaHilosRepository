namespace FabricaHilos.Models.Produccion.Planeamiento;

/// <summary>
/// Resultado de <c>GetActivosPaginadoAsync</c>: página de ítems + totales globales
/// usados por los KPIs del Dashboard y los controles de paginación.
/// </summary>
public sealed class PlnSeguimientoPagina
{
    /// <summary>Ítems de la página actual (todos los ítems de los pedidos incluidos).</summary>
    public IReadOnlyList<PlnSeguimiento> Items { get; init; } = [];

    // ── Totales globales (sobre el conjunto completo filtrado, no solo la página) ──
    public int TotalItems       { get; init; }
    public int TotalRetrasados  { get; init; }
    public int TotalUrgentes    { get; init; }
    public int TotalReprocesos  { get; init; }
    public int TotalSinPlanif   { get; init; }

    // ── Metadatos de paginación ────────────────────────────────────────────────
    public int TotalPedidos     { get; init; }
    public int Pagina           { get; init; }
    public int TamPagina        { get; init; }
    public int TotalPaginas     => TamPagina > 0 ? (int)Math.Ceiling((double)TotalPedidos / TamPagina) : 1;
}
