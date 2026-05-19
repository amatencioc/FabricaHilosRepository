namespace FabricaHilos.Models.Produccion.Planeamiento;

/// <summary>
/// DTO de V_PLN_KPI_CUMPLIMIENTO (§8.7 PKG_PLN) — una fila por mes.
/// Solo ítems cerrados (ESTADO='C', COD_PASO_ACT='14') con FCH_REAL_DESPACHO.
/// </summary>
public class PlnKpi
{
    public DateTime Periodo                { get; set; }
    public string   PeriodoStr            => Periodo.ToString("MM/yyyy");
    public int      TotalItemsCerrados    { get; set; }
    public int      EntregadosATiempo     { get; set; }
    public int      EntregadosTarde       { get; set; }
    public double   PctOtif               { get; set; }
    public double   CicloPromedioDias     { get; set; }
    public double   DiasPromTintoreria    { get; set; }
    public double   DiasPromPedidoPartida { get; set; }
    public decimal  KgTotalDespachados    { get; set; }
    public double   RetrasoPromedioDias   { get; set; }

    // Alias de compatibilidad con vistas existentes
    public int    TotalPedidos      => TotalItemsCerrados;
    public int    PedidosATiempo    => EntregadosATiempo;
    public int    PedidosRetrasados => EntregadosTarde;
    public decimal KgDespachados   => KgTotalDespachados;
}

public class PlnKpiResumen
{
    // B7: List<> evita doble enumeración al llamar Any() + Average() sobre IEnumerable
    public IReadOnlyList<PlnKpi>         OtifMensual        { get; set; } = [];
    public double                        TasaReproceso       { get; set; }
    public int                           RetrasosCriticos    { get; set; }
    public int                           RetrasosAltos       { get; set; }
    public double                        CicloPromedioTotal  { get; set; }
    public IReadOnlyList<PlnRetrasoArea> RetrasosPorArea    { get; set; } = [];

    // Propiedades derivadas para la vista
    public double OtifPromedio      => OtifMensual.Count > 0 ? OtifMensual.Average(k => k.PctOtif) : 0;
    public double PctReproceso      => TasaReproceso;
    public int    TotalCriticos     => RetrasosCriticos;
    public double CicloPromedioDias => CicloPromedioTotal;
    public IReadOnlyList<PlnKpi> Kpis => OtifMensual;
}

public class PlnRetrasoArea
{
    public string Area         { get; set; } = "";
    public int    CantRetrasos { get; set; }
    public double DiasPromedio { get; set; }
}

