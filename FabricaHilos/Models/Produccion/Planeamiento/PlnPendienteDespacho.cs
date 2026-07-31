namespace FabricaHilos.Models.Produccion.Planeamiento;

/// <summary>
/// DTO enriquecido para PendientesDespacho y ProximosDespacho.
/// Cubre ítems en PASO '12'/'13' (listos) y '08'-'11' (próximos a llegar).
/// </summary>
public class PlnPendienteDespacho
{
    // — Claves —
    public int      Serie             { get; set; }
    public long     NumPed            { get; set; }
    public int      Nro               { get; set; }
    public int      NumDet            { get; set; }

    // — Artículo / cliente —
    public string   CodCliente        { get; set; } = "";
    public string   NomCliente        { get; set; } = "";
    public string   CodArt            { get; set; } = "";
    public string   DescArt           { get; set; } = "";
    public string   Color             { get; set; } = "";
    public string   ColorDet          { get; set; } = "";   // ITEMPED.COLOR_DET
    public string   Titulo            { get; set; } = "";
    public string   Proceso           { get; set; } = "";   // '01'=Cardado, '20'=Peinado, '24'=Gaseado

    // — Kilogramos —
    public decimal  CantidadPedido    { get; set; }         // ITEMPED.CANTIDAD
    public decimal  KgPendientes      { get; set; }
    public decimal  KgProducidos      { get; set; }         // producidos totales (vs. pedidos)
    public decimal  StockDisponible   { get; set; }         // stock almacén PT (01 paso'12/'13')
    public decimal  KgADespachar      { get; set; }         // MIN(kg_pendientes, stock_disponible)

    // — Fechas —
    public DateTime? FchEntregaComp   { get; set; }         // comprometida al cliente
    public DateTime? FchEstDespacho   { get; set; }         // estimada por PLN_ (útil en próximos)

    // — Indicadores de tiempo —
    public int      DiasVencido       { get; set; }         // > 0 = ya vencida, < 0 = días restantes
    public int      DiasRetraso       { get; set; }
    public int      DiasEnPaso        { get; set; }         // días acumulados en el paso actual

    // — Flags —
    public string   IndUrgente        { get; set; } = "N";
    public string   IndRetraso        { get; set; } = "N";

    // — Paso —
    public string   CodPasoAct        { get; set; } = "";
    public string   NombrePaso        { get; set; } = "";
    public string   ColorUi           { get; set; } = "#6c757d";  // color del paso (PLN_ESTADO_CODIGO)

    // — Máquinas (coherencia con CargaMaquinas) —
    public string   CodMaqSecado      { get; set; } = "";   // secadora que procesó el lote
    public string   CodMaqDevan       { get; set; } = "";   // devanadora que procesó el lote

    // — Conos programados —
    public decimal  KgPorCono         { get; set; }         // H_PROGRAMACION.KG_UNIDAD
    public int      NumConos          { get; set; }         // ROUND(kg_pendientes / kg_por_cono)

    // — Conos reales (PARTIDA.NRO_RMC / RMC) —
    public int      NroRmc            { get; set; }         // PARTIDA.NRO_RMC — cantidad de conos/madejas
    public string   Rmc               { get; set; } = "";   // PARTIDA.RMC — 'R'=Rollo, 'M'=Madeja

    // — Pedido —
    public string   PrioridadPedido   { get; set; } = "";

    // ── Helpers UI ──────────────────────────────────────────────────────────
    public bool EsUrgente   => IndUrgente == "S";
    public bool EstaVencido => DiasVencido > 0;

    /// Semáforo Bootstrap basado en días vencidos / restantes.
    public string SemaforoCss => DiasVencido >= 5  ? "danger"
                               : DiasVencido >= 2  ? "warning"
                               : DiasVencido >= 1  ? "warning"
                               : DiasVencido == 0  ? "secondary"
                               : DiasVencido >= -3 ? "info"      // ≤ 3 días restantes
                               : "success";

    /// Texto del semáforo para tooltip.
    public string SemaforoTexto => DiasVencido > 0  ? $"+{DiasVencido} d vencido"
                                 : DiasVencido == 0 ? "Vence hoy"
                                 : $"{-DiasVencido} d restantes";

    /// Porcentaje de kgs producidos sobre kg pendientes (sobreproducción > 100 es normal).
    public int PctCumplimiento => KgPendientes > 0
        ? (int)Math.Round(KgProducidos / KgPendientes * 100)
        : 0;

    /// Nombre legible del proceso.
    public string NombreProceso => Proceso switch
    {
        "01" => "Cardado",
        "20" => "Peinado",
        "24" => "Gaseado",
        _    => Proceso
    };
}
