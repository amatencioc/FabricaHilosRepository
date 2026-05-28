namespace FabricaHilos.Models.Produccion.Planeamiento;

public class PlnSeguimiento
{
    public long   IdSeguim      { get; set; }
    public int    Serie         { get; set; }
    public long   NumPed        { get; set; }
    public int    Nro           { get; set; }
    public int    NumDet        { get; set; }

    public string? CodCliente   { get; set; }
    public string? NombreCliente { get; set; }
    public string? CodArt       { get; set; }
    public string? Color        { get; set; }
    public string? Titulo       { get; set; }
    public string? Proceso      { get; set; }
    public decimal CantidadOrig { get; set; }
    public string  SoloDespacho { get; set; } = "N";

    public string  CodPasoAct   { get; set; } = "01";
    public string? NombrePaso   { get; set; }
    public string? ColorUi      { get; set; }
    public string? CodPasoAnt   { get; set; }
    public int     NroCiclo     { get; set; } = 1;

    public DateTime  FchPedido        { get; set; }
    public DateTime? FchEntregaComp   { get; set; }
    /// <summary>ITEMPED_DET.FCH_REG_ENTREGA — fecha de registro del ítem (interna, menor prioridad).</summary>
    public DateTime? FchRegEntrega    { get; set; }
    /// <summary>ITEMPED_DET.FCH_ENTREGA_ORI — fecha ORIGINAL de compromiso del artículo (primera promesa formal).</summary>
    public DateTime? FchEntregaOri    { get; set; }

    // Fechas estimadas
    public DateTime? FchEstHilanderia { get; set; }
    public DateTime? FchEstPartida    { get; set; }
    public DateTime? FchEstTinIni     { get; set; }
    public DateTime? FchEstTinFin     { get; set; }
    public DateTime? FchEstSecado     { get; set; }
    public DateTime? FchEstCalidad    { get; set; }
    public DateTime? FchEstDespacho   { get; set; }

    // Fechas reales
    public DateTime? FchRealProgramado { get; set; }
    public DateTime? FchRealProduccion { get; set; }
    public DateTime? FchRealPartida    { get; set; }
    public DateTime? FchRealTinIni     { get; set; }
    public DateTime? FchRealTinFin     { get; set; }
    public DateTime? FchRealSecado     { get; set; }
    public DateTime? FchRealCcTinto    { get; set; }
    public DateTime? FchRealCcRechazo  { get; set; }
    public DateTime? FchRealGaseado    { get; set; }    // v2.1: PASO '09B', solo PROCESO='24'
    public DateTime? FchRealDevanado   { get; set; }
    public DateTime? FchRealCalidad    { get; set; }
    public DateTime? FchRealAlmPt      { get; set; }
    public DateTime? FchRealDespacho   { get; set; }

    // KGs
    public decimal KgProducidos  { get; set; }
    public decimal KgEnTin       { get; set; }
    public decimal KgEnAlmPt     { get; set; }
    public decimal KgDespachados { get; set; }
    public decimal KgPendientes  { get; set; }

    // Indicadores
    public string IndRetraso   { get; set; } = "N";
    public int    DiasRetraso  { get; set; }
    public string IndUrgente   { get; set; } = "N";
    public string IndReproceso { get; set; } = "N";
    // v2.3: flujo dual Lab/Hilandería — PASO '03' y '04' son concurrentes
    // 'L'=Lab aprobó ANTES de crear PARTIDA (~81%)  'H'=PARTIDA creada ANTES de Lab (~3%)  'N'=Sin Lab
    public string IndFlujo     { get; set; } = "L";
    public string Estado       { get; set; } = "A";

    // ── Referencias a objetos del flujo ─────────────────────────────────────────
    /// <summary>PARTIDA.NUMERO vinculada a este sublote (via PARTIDA.NROPROG = ITEMPED_DET.NROPROG).</summary>
    public long NumPartida { get; set; }
    /// <summary>PARTIDA.NUMERO del ciclo anterior (rechazado). Poblado solo cuando NRO_CICLO > 1.</summary>
    public long NumPartidaAnt { get; set; }

    // ── Máquinas asignadas / usadas ──────────────────────────────────────────
    public string? CodMaqTt      { get; set; }   // PLN_SEGUIMIENTO.COD_MAQ_TT (trigger al PASO '06')
    public string? CodMaqSecado  { get; set; }   // PLN_SEGUIMIENTO.COD_MAQ_SECADO (trigger TIA_PLN_FROM_TT_RSECADO)
    public string? CodMaqDevan   { get; set; }   // PLN_SEGUIMIENTO.COD_MAQ_DEVAN (trigger TIA_PLN_FROM_REVISADO_G)
    public string? MaqProgramada { get; set; }   // ITEMPED_DET.MAQUINA (máquina planificada)
    public string? MaqPartida    { get; set; }   // PARTIDA.COD_MAQ (real; más confiable post-TT)
    public string? MaqRealTt     { get; set; }   // TT_RPRODUC.COD_MAQ WHERE estado='1' (en curso ahora)

    /// <summary>Máquina TT efectiva: trigger → partida → programada (cascada de confiabilidad).</summary>
    // ── Actores del ciclo de vida (v2.6) ────────────────────────────────────────
    /// <summary>PEDIDO.F_APROBACION — fecha en que ventas aprobó el pedido.</summary>
    public DateTime? FchAprobacion    { get; set; }
    /// <summary>ITEMPED_DET.FHC_PROG — fecha programada por el planificador al asignar NROPROG.</summary>
    public DateTime? FchPlanif        { get; set; }
    /// <summary>PEDIDO.A_ADUSER — login de quien registró el pedido.</summary>
    public string?   UsrRegistro      { get; set; }
    /// <summary>CS_USER.C_NOMBRE para PEDIDO.A_ADUSER.</summary>
    public string?   NombreRegistro   { get; set; }
    /// <summary>PEDIDO.A_USAPROB — login de quien aprobó el pedido.</summary>
    public string?   UsrAprobacion    { get; set; }
    /// <summary>CS_USER.C_NOMBRE para PEDIDO.A_USAPROB.</summary>
    public string?   NombreAprobacion { get; set; }
    /// <summary>PLN_SEGUIMIENTO.USR_PLANIF — login de quien asignó NROPROG (planificador).</summary>
    public string?   UsrPlanif        { get; set; }
    /// <summary>CS_USER.C_NOMBRE para PLN_SEGUIMIENTO.USR_PLANIF.</summary>
    public string?   NombrePlanif     { get; set; }

    public string? MaqTtEfectiva =>
        !string.IsNullOrEmpty(CodMaqTt)   ? CodMaqTt   :
        !string.IsNullOrEmpty(MaqPartida) ? MaqPartida :
        MaqProgramada;

    /// <summary>True cuando hay un baño activo en una máquina distinta a la efectiva (desviación de planta).</summary>
    public bool HayConflictoMaquina =>
        !string.IsNullOrEmpty(MaqRealTt) &&
        !string.IsNullOrEmpty(MaqTtEfectiva) &&
        !string.Equals(MaqRealTt, MaqTtEfectiva, StringComparison.OrdinalIgnoreCase);

    // Helpers
    public bool EstaRetrasado    => IndRetraso   == "S";
    public bool EsUrgente        => IndUrgente   == "S";
    public bool EstaEnReproceso  => IndReproceso == "S";
    public bool EstaCerrado      => Estado        == "C";
    public bool EsStock          => SoloDespacho  == "S";
    public bool LabFuePrimero        => IndFlujo == "L";  // Lab aprobó antes (normal ~81%)
    public bool HilanderiaFuePrimero => IndFlujo == "H";  // PARTIDA creada antes (especial ~3%)

    /// <summary>Porcentaje de avance en el flujo (0–100).</summary>
    public int PctAvance => CodPasoAct switch
    {
        "01"  =>  6,
        "02"  => 13,
        "03"  => 19,
        "04"  => 25,
        "05"  => 31,
        "06"  => 38,
        "07"  => 44,
        "08"  => 50,
        "09"  => 56,
        "09B" => 62,
        "10"  => 69,
        "11"  => 75,
        "12"  => 81,
        "13"  => 88,
        "14"  => 100,
        "9R"  => 50,
        _     =>  0
    };
}
