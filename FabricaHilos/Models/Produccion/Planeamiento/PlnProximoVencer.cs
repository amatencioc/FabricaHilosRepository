namespace FabricaHilos.Models.Produccion.Planeamiento;

/// <summary>
/// Ítem de pedido activo cuya fecha de entrega comprometida cae dentro de un rango indicado.
/// Usado por la sección "Próximos a Vencer" de /Planeamiento/Alertas.
/// </summary>
public class PlnProximoVencer
{
    public int     Serie          { get; set; }
    public long    NumPed         { get; set; }
    public int     Nro            { get; set; }
    public int     NumDet         { get; set; }

    public string? CodCliente     { get; set; }
    public string? NombreCliente  { get; set; }
    public string? CodArt         { get; set; }
    public string? Color          { get; set; }
    public string? Titulo         { get; set; }
    public string? Proceso        { get; set; }

    public string  CodPasoAct     { get; set; } = "01";
    public string? NombrePaso     { get; set; }
    public string? ColorUiPaso    { get; set; }

    public DateTime  FchPedido       { get; set; }
    public DateTime? FchEntregaComp  { get; set; }
    /// <summary>TRUNC(FCH_ENTREGA_COMP) - TRUNC(SYSDATE). Negativo = ya retrasado.</summary>
    public int       DiasHastaVencer { get; set; }

    public int     DiasRetraso    { get; set; }
    public string  IndRetraso     { get; set; } = "N";
    public string  IndUrgente     { get; set; } = "N";
    public string  IndReproceso   { get; set; } = "N";

    public decimal  CantidadOrig  { get; set; }
    public decimal  KgPendientes  { get; set; }
    public int      NroCiclo      { get; set; } = 1;

    // ── Helpers ──────────────────────────────────────────────────────────────
    public bool EstaRetrasado   => IndRetraso   == "S";
    public bool EsUrgente       => IndUrgente   == "S";
    public bool EstaEnReproceso => IndReproceso == "S";

    /// Semáforo visual según días restantes
    public string SemaforoClass => DiasHastaVencer switch
    {
        < 0  => "text-danger fw-bold",   // ya vencido
        <= 3 => "text-danger",           // ≤3 d
        <= 7 => "text-warning fw-semibold", // ≤7 d
        _    => "text-success"           // holgado
    };

    public string SemaforoIcon => DiasHastaVencer switch
    {
        < 0  => "bi-exclamation-octagon-fill",
        <= 3 => "bi-alarm-fill",
        <= 7 => "bi-clock-history",
        _    => "bi-calendar-check"
    };

    public string ProcesoTexto => Proceso switch
    {
        "01" => "Cardado",
        "20" => "Peinado",
        "24" => "P.Gaseado",
        _    => Proceso ?? ""
    };

    public double? KgPorcentajePendiente =>
        CantidadOrig > 0
            ? Math.Round((double)KgPendientes / (double)CantidadOrig * 100, 1)
            : (double?)null;
}
