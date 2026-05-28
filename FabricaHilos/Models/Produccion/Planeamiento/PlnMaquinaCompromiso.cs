namespace FabricaHilos.Models.Produccion.Planeamiento;

/// <summary>
/// Compromiso de una máquina con un pedido en producción.
/// Fuentes: PLN_SEGUIMIENTO (COD_MAQ_SECADO / COD_MAQ_DEVAN)
///          y tablas legacy activas (TT_RSECADO / TT_RPRODUC) para ítems sin PLN tracking.
/// </summary>
public class PlnMaquinaCompromiso
{
    public string   Area           { get; set; } = "";   // "Secado" / "Devanado" / "Tintorería"
    public string   CodMaq         { get; set; } = "";
    public long     NumPed         { get; set; }          // 0 = sin PLN tracking
    public int      Nro            { get; set; }
    public int      NumDet         { get; set; }
    public int      Serie          { get; set; }
    public string   CodPasoAct     { get; set; } = "";
    public string   NombrePaso     { get; set; } = "";
    public string   ColorUi        { get; set; } = "#6c757d";
    public DateTime? FchEntregaComp { get; set; }
    public decimal  Kg             { get; set; }
    public string   IndRetraso     { get; set; } = "N";
    public int      DiasRetraso    { get; set; }
    /// <summary>EN_PROCESO = usando la máquina ahora | COMPROMETIDA = llegará pronto | ASIGNADA = ya la usó pero aún activo</summary>
    public string    EstadoMaq      { get; set; } = "";
    /// <summary>Fecha en que el proceso físico finalizó (ej. TT_RSECADO.FECHA_FIN).
    /// Cuando está poblado en PASO '08' indica que el secado físico terminó pero PLN aún no avanzó
    /// (espera inserción en CTCALIDAD_D para pasar a PASO '09').</summary>
    public DateTime? FechaFinFisico { get; set; }
    /// <summary>Fuente del dato: PLN (PLN_SEGUIMIENTO), TT_RSECADO, TT_RPRODUC</summary>
    public string    Fuente         { get; set; } = "";

    /// <summary>Nota contextual cuando el estado físico y el paso PLN divergen.
    /// Solo aplica en PASO '08': secado completado físicamente pero PLN espera CC TT.</summary>
    public string? Nota => (Area == "Secado" && CodPasoAct == "08" && FechaFinFisico.HasValue)
        ? $"Sec. completado el {FechaFinFisico.Value:dd/MM/yy} · Esp. CC TT"
        : null;

    // ── Helpers ────────────────────────────────────────────────────────────────
    public bool EstaRetrasado  => IndRetraso == "S";
    public bool SinSeguimiento => NumPed == 0;

    /// Badge Bootstrap por estado de máquina
    public string BadgeEstado => EstadoMaq switch
    {
        "EN_PROCESO"   => "bg-success",
        "COMPROMETIDA" => "bg-warning text-dark",
        "ASIGNADA"     => "bg-secondary",
        _              => "bg-light text-dark border"
    };

    /// Icono Bootstrap Icons por estado
    public string IconoEstado => EstadoMaq switch
    {
        "EN_PROCESO"   => "bi-play-circle-fill",
        "COMPROMETIDA" => "bi-clock-history",
        "ASIGNADA"     => "bi-check-circle",
        _              => "bi-question-circle"
    };

    /// Nombre legible — misma lógica que maqNombre() en Pedido.cshtml
    /// R03→"Rodete Nº3 · THIES", M01→"Madejas Nº1 · HANK", MR2→"Mad.Rodete Nº2", S01→"Secadora Nº1"
    public string NombreMaqDisplay
    {
        get
        {
            if (string.IsNullOrEmpty(CodMaq)) return "";
            int i = 0;
            while (i < CodMaq.Length && char.IsLetter(CodMaq[i])) i++;
            var pref = CodMaq[..i].ToUpper();
            var numS = CodMaq[i..].Trim();
            if (!int.TryParse(numS, out var num)) return CodMaq;
            return pref switch
            {
                "R"  => $"Rodete Nº{num} · THIES",
                "M"  => $"Madejas Nº{num} · HANK",
                "MR" => $"Mad.Rodete Nº{num}",
                "S"  => $"Secadora Nº{num}",
                _    => CodMaq
            };
        }
    }
}
