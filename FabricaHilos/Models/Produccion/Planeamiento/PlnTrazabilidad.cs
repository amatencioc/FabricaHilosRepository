namespace FabricaHilos.Models.Produccion.Planeamiento;

/// <summary>
/// DTO de V_PLN_TRAZABILIDAD (§8.3 PKG_PLN).
/// Provee todos los datos para el gráfico Timeline Horizontal (ApexCharts rangeBar)
/// en PedidoGantt.cshtml / Pedido.cshtml.
/// Incluye fechas reales, planificadas y diferencias de días entre hitos.
/// </summary>
public class PlnTrazabilidad
{
    public long     NumPed               { get; set; }
    public int      Nro                  { get; set; }
    public int      NumDet               { get; set; }
    public string   CodCliente           { get; set; } = "";
    public string   CodArt               { get; set; } = "";
    public string   Color                { get; set; } = "";
    public string   Titulo               { get; set; } = "";

    // Fechas del pedido y planificación
    public DateTime  FchPedido           { get; set; }
    public DateTime? FchAprobPedido      { get; set; }
    public DateTime? FchPlaneada         { get; set; }
    public DateTime? FchEntregaPlan      { get; set; }
    public DateTime? FchEstCono1         { get; set; }
    public DateTime? FchEstTenido        { get; set; }

    // Fechas estimadas calculadas por SP_PLN_CALCULA_FECHAS (PLN_SEGUIMIENTO.FCH_EST_*)
    public DateTime? FchEstHilanderia    { get; set; }
    public DateTime? FchEstPartida       { get; set; }
    public DateTime? FchEstTinIni        { get; set; }
    public DateTime? FchEstTinFin        { get; set; }
    public DateTime? FchEstSecado        { get; set; }
    public DateTime? FchEstCalidad       { get; set; }
    public DateTime? FchEstDespacho      { get; set; }

    // Fechas reales de producción (FCH_REAL_*)
    public DateTime? FchRealProgramado   { get; set; }
    public DateTime? FchRealProduccion   { get; set; }
    public DateTime? FchRealPartida      { get; set; }
    public DateTime? FchRealTinIni       { get; set; }
    public DateTime? FchProgTin          { get; set; }   // TT_PROGPART.FENTREGA
    public DateTime? FchRealTinFin       { get; set; }
    public DateTime? FchRealSecado       { get; set; }
    /// <summary>PASO 09 — CC Tintorería aprobado (CTCALIDAD_D.FECHA / FCH_REAL_CC_TINTO). Mismo campo que Pedido/Gantt usan para la etapa Calidad.</summary>
    public DateTime? FchRealCcTinto      { get; set; }
    /// <summary>PASO 11 — Revisado final (REVISADO_D.FECHA / FCH_REAL_CALIDAD).</summary>
    public DateTime? FchRealCalidad      { get; set; }
    public DateTime? FchRealAlmPt        { get; set; }
    public DateTime? FchRealDespacho     { get; set; }
    public DateTime? FchCompromisoCliente { get; set; }

    // Diferencias calculadas (en días; null si alguna fecha es null)
    public double? DiasPedidoAPartida    { get; set; }
    public double? DiasEnTintoreria      { get; set; }
    public double? DiasPartidaAAlmPt     { get; set; }
    public double? DiasAlmPtADespacho    { get; set; }
    public double? DiasTotalCiclo        { get; set; }
    public double? DiasDesvioCliente     { get; set; }   // positivo = tardío

    // Paso actual y estado de retraso
    public string   CodPasoAct           { get; set; } = "";
    public int      DiasRetraso          { get; set; }
    public int      NroCiclo             { get; set; }

    // Helpers UI para ApexCharts Timeline
    /// <summary>CSS del desvío respecto al cliente (+ = tarde / - = adelantado).</summary>
    public string DesvioClienteCss => DiasDesvioCliente switch
    {
        null             => "secondary",
        > 7              => "danger",
        > 3              => "warning",
        > 0              => "info",
        _                => "success"
    };
    public bool DespachadoATiempo  => DiasDesvioCliente <= 0;
    public bool DespachadoTardio   => DiasDesvioCliente > 0;

    // Métricas estimadas (calculadas en C# cuando los reales son null)
    public double? EstDiasPedidoAPartida =>
        FchEstPartida.HasValue ? (FchEstPartida.Value - FchPedido).TotalDays : null;
    public double? EstDiasEnTintoreria =>
        (FchEstTinIni.HasValue && FchEstTinFin.HasValue)
            ? (FchEstTinFin.Value - FchEstTinIni.Value).TotalDays : null;
    public double? EstDiasPartidaADespacho =>
        (FchEstPartida.HasValue && FchEstDespacho.HasValue)
            ? (FchEstDespacho.Value - FchEstPartida.Value).TotalDays : null;
    public double? EstDiasTotalCiclo =>
        FchEstDespacho.HasValue ? (FchEstDespacho.Value - FchPedido).TotalDays : null;
    public double? EstDiasDesvioCliente =>
        (FchEstDespacho.HasValue && FchCompromisoCliente.HasValue)
            ? (FchEstDespacho.Value - FchCompromisoCliente.Value).TotalDays : null;
    public string EstDesvioClienteCss => EstDiasDesvioCliente switch
    {
        null   => "secondary",
        > 7    => "danger",
        > 3    => "warning",
        > 0    => "info",
        _      => "success"
    };

    // ── Actores del ciclo de vida (v2.6) ────────────────────────────────────────
    /// <summary>ITEMPED_DET.FHC_PROG — fecha en que el planificador asignó el NROPROG.</summary>
    public DateTime? FchPlanif        { get; set; }
    /// <summary>PEDIDO.A_ADUSER — login de quien registró el pedido.</summary>
    public string?   UsrRegistro      { get; set; }
    /// <summary>CS_USER.C_NOMBRE para PEDIDO.A_ADUSER.</summary>
    public string?   NombreRegistro   { get; set; }
    /// <summary>PEDIDO.A_USAPROB — login de quien aprobó el pedido.</summary>
    public string?   UsrAprobacion    { get; set; }
    /// <summary>CS_USER.C_NOMBRE para PEDIDO.A_USAPROB.</summary>
    public string?   NombreAprobacion { get; set; }
    /// <summary>PLN_SEGUIMIENTO.USR_PLANIF — login del planificador.</summary>
    public string?   UsrPlanif        { get; set; }
    /// <summary>CS_USER.C_NOMBRE para PLN_SEGUIMIENTO.USR_PLANIF.</summary>
    public string?   NombrePlanif     { get; set; }
}
