namespace FabricaHilos.Models.Produccion.Planeamiento;

/// <summary>
/// Resultado de PKG_PLN.SP_PLN_REPORTE_PRODUCCION.
/// Representa el estado de producción (tintorería) de un ítem de pedido.
/// </summary>
public class PlnReporteProduccion
{
    /// <summary>NUM_PED-NRO-NUM_DET-REPROCESO</summary>
    public string? Partida          { get; set; }
    public string? EstadoProg       { get; set; }
    public string? Cliente          { get; set; }
    public string? Material         { get; set; }

    public DateTime? FchPedido      { get; set; }
    public DateTime? FchEntrega     { get; set; }
    public string?   FchPartida     { get; set; }   // TO_CHAR(Q.FECHA,'DD/MM/YY') || ' ' || Q.HORA

    public decimal?  PesoNeto       { get; set; }
    public string?   Rmc            { get; set; }
    public string?   NroRmc         { get; set; }
    public string?   Referencia     { get; set; }
    public string?   Proceso        { get; set; }

    public DateTime? FechaTenido    { get; set; }
    public DateTime? FechaCcalid    { get; set; }
    public DateTime? FechaEncon     { get; set; }
    public DateTime? FechaSecado    { get; set; }
    public DateTime? FechaReceta    { get; set; }
    public DateTime? FchRevisado    { get; set; }
    public DateTime? FechaIng       { get; set; }

    public decimal?  CantDesp       { get; set; }
    public string?   Titulo         { get; set; }
    public decimal?  CantProg       { get; set; }
    public string?   Lote           { get; set; }
    public string?   TituloTexto    { get; set; }
    public DateTime? FchProg        { get; set; }
    public string?   PartMatiz      { get; set; }

    public string?   EstEvaluacion  { get; set; }
    public string?   Defecto        { get; set; }
    public string?   Resultado      { get; set; }
    public decimal?  DiasRetraso    { get; set; }

    public DateTime? FchEntregaConoUno   { get; set; }
    public DateTime? FchValRec           { get; set; }
    public DateTime? FchEstimaConoUno    { get; set; }
    public DateTime? FchEntTin           { get; set; }
    public DateTime? FchEstimaTenido     { get; set; }
    public DateTime? FchProgval          { get; set; }
    public string?   LaboVal             { get; set; }
    public DateTime? FchUltIngAlmpi      { get; set; }
    public string?   MaqProg             { get; set; }
    public string?   AcaMad              { get; set; }
    public DateTime? FechaSecadoMad      { get; set; }

    // ── Helpers ──────────────────────────────────────────────────────────────
    /// <summary>Indicador de retraso según DIAS_RETRASO > 0.</summary>
    public bool EstaRetrasado => DiasRetraso.HasValue && DiasRetraso.Value < 0;

    /// <summary>Color semáforo para días de retraso.</summary>
    public string SemaforoClass => EstaRetrasado ? "text-danger fw-semibold" : "text-success";
}
