namespace FabricaHilos.Models.Produccion.Planeamiento;

/// <summary>
/// DTO de PLN_FECHAS_ESTIMADAS (§2.7 PKG_PLN).
/// Snapshot inmutable de cada recálculo de fechas por SP_PLN_CALCULA_FECHAS
/// o SP_PLN_REPROGRAMAR. Permite auditar la precisión de la planificación.
/// MOTIVO_RECALCULO: 'PED'=pedido | 'PLA'=planificado | 'REP'=reprogramado | 'MAQ'=máquina
/// DIFER_DIAS positivo = se demoró más; negativo = se adelantó respecto al cálculo anterior.
/// </summary>
public class PlnFechaEstimada
{
    public long      IdFech              { get; set; }
    public long      IdSeguim            { get; set; }
    public DateTime  FchCalculo          { get; set; }
    public string    MotivoRecalculo     { get; set; } = "";
    public DateTime? FchEstHilanderia    { get; set; }
    public DateTime? FchEstPartida       { get; set; }
    public DateTime? FchEstTinIni        { get; set; }
    public DateTime? FchEstTinFin        { get; set; }
    public DateTime? FchEstSecado        { get; set; }
    public DateTime? FchEstCalidad       { get; set; }
    public DateTime? FchEstDespacho      { get; set; }
    public int?      DiferDias           { get; set; }
    public string?   Usuario             { get; set; }

    // Helpers UI
    public string MotivoDesc => MotivoRecalculo switch
    {
        "AV"  => "Avance de paso",
        "PED" => "Registro de pedido",
        "PLA" => "Planificación",
        "REP" => "Reprogramación manual",
        "MAQ" => "Cambio de máquina",
        _     => MotivoRecalculo
    };
    public string DiferCss => DiferDias switch
    {
        null       => "secondary",
        0          => "success",
        > 0        => "danger",
        _          => "info"
    };
}
