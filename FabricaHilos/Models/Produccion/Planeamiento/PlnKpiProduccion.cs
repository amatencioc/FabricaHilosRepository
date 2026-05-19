namespace FabricaHilos.Models.Produccion.Planeamiento;

/// <summary>
/// DTO de V_PLN_KPI_PRODUCCION (§8.8 PKG_PLN).
/// KPIs de eficiencia de producción por máquina y mes.
/// Ventana: últimos 12 meses desde H_PRODUCCION_D.
/// </summary>
public class PlnKpiProduccion
{
    public DateTime Periodo           { get; set; }
    public string   TpMaq             { get; set; } = "";   // H=Hilandería, T=Tintorería
    public string   CodMaq            { get; set; } = "";
    public decimal  KgProducidos      { get; set; }
    public double   HorasPromTurno    { get; set; }
    public double   HorasPromParada   { get; set; }
    public double   KgPorHora         { get; set; }
    public int      DiasActivos       { get; set; }

    // Helpers UI
    public string PeriodoStr   => Periodo.ToString("MM/yyyy");
    public string Area         => TpMaq == "H" ? "Hilandería" : "Tintorería";
    public string EficienciaCss => KgPorHora switch
    {
        >= 15 => "success",
        >= 10 => "info",
        >= 5  => "warning",
        _     => "danger"
    };
}
