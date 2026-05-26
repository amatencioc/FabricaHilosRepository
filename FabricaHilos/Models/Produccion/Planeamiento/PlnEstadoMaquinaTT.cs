namespace FabricaHilos.Models.Produccion.Planeamiento;

/// <summary>
/// Estado en tiempo real de una máquina de tintorería (Rodetes Thies y Hanks).
/// Fuente: TT_RPRODUC (estado IN ('1','2')) + catálogo de máquinas conocidas.
/// EstadoMaq = "ACTIVA" si hay proceso activo, "LIBRE" si no hay ninguno.
/// </summary>
public class PlnEstadoMaquinaTT
{
    public string    EstadoMaq      { get; set; } = "LIBRE";  // ACTIVA / LIBRE
    public string    CodMaq         { get; set; } = "";
    public string    TipDoc         { get; set; } = "";       // PA / IR / -
    public string    Proceso        { get; set; } = "";       // TE, TEAC, BQM, MA, IN
    public long      NumPed         { get; set; }             // 0 = sin enlace pedido
    public int       Nro            { get; set; }
    public int       NumDet         { get; set; }
    public int       Serie          { get; set; }
    public string?   NombreCliente  { get; set; }
    public string?   CodArt         { get; set; }
    public string?   Titulo         { get; set; }
    public string    CodPasoAct     { get; set; } = "";
    public string    NombrePaso     { get; set; } = "";
    public string    ColorUi        { get; set; } = "#6c757d";
    public DateTime? FchEntregaComp { get; set; }
    public int       DiasRetraso    { get; set; }
    public string    IndRetraso     { get; set; } = "N";
    public string    IndUrgente     { get; set; } = "N";
    public decimal   Kg             { get; set; }
    /// Descripción real de la máquina (de TT_MAQUINA.DESCRIPCION). Null cuando la fuente no la provee.
    public string?   Descripcion    { get; set; }

    // ── Helpers ──────────────────────────────────────────────────────────────

    public bool EsActiva      => EstadoMaq == "ACTIVA";
    public bool EstaRetrasado => IndRetraso == "S";
    public bool EsUrgente     => IndUrgente == "S";
    public bool TienePedido   => NumPed > 0;

    /// Grupo de máquina: Thies (R), HANK (M), Mad.Rodete (MR), Secadora (S), Centrífuga (C), Prensadora (P), Caldero (Q)
    public string Grupo =>
        CodMaq.Length >= 2 && CodMaq.StartsWith("MR", StringComparison.OrdinalIgnoreCase)
            ? "Mad.Rodete"
            : CodMaq.StartsWith("R", StringComparison.OrdinalIgnoreCase)
                ? "Thies"
                : CodMaq.StartsWith("M", StringComparison.OrdinalIgnoreCase)
                    ? "HANK"
                    : CodMaq.StartsWith("S", StringComparison.OrdinalIgnoreCase)
                        ? "Secadora"
                        : CodMaq.StartsWith("C", StringComparison.OrdinalIgnoreCase)
                            ? "Centrífuga"
                            : CodMaq.StartsWith("P", StringComparison.OrdinalIgnoreCase)
                                ? "Prensadora"
                                : CodMaq.StartsWith("Q", StringComparison.OrdinalIgnoreCase)
                                    ? "Caldero"
                                    : "Otro";

    /// Nombre legible: R03→"Thies Nº3", M01→"Hank Nº1", MR2→"Mad.Rodete Nº2", C01→"Centrífuga Nº1"
    /// Si Descripcion está disponible (desde TT_MAQUINA), la usa directamente.
    public string NombreMaqDisplay
    {
        get
        {
            if (!string.IsNullOrEmpty(Descripcion)) return Descripcion;
            if (string.IsNullOrEmpty(CodMaq)) return "";
            int i = 0;
            while (i < CodMaq.Length && char.IsLetter(CodMaq[i])) i++;
            var pref = CodMaq[..i].ToUpper();
            var numS = CodMaq[i..].Trim();
            if (!int.TryParse(numS, out var num)) return CodMaq;
            return pref switch
            {
                "R"  => $"Thies Nº{num}",
                "M"  => $"Hank Nº{num}",
                "MR" => $"Mad.Rodete Nº{num}",
                "S"  => $"Secadora Nº{num}",
                "C"  => $"Centrífuga Nº{num}",
                "P"  => $"Prensadora Nº{num}",
                "Q"  => $"Caldero Nº{num}",
                _    => CodMaq
            };
        }
    }

    /// Nombre del proceso de tintorería o secado
    public string NombreProceso => Proceso switch
    {
        "TE"   => "Teñido",
        "TEAC" => "Teñido + Acabado",
        "BQM"  => "Blanqueo Químico",
        "MA"   => "Matizado",
        "IN"   => "Intensificado",
        "SE"   => "Secado",
        "RE"   => "Resecado",
        _      => Proceso
    };

    /// Color del badge de estado
    public string BadgeClase => EstadoMaq switch
    {
        "ACTIVA" => "bg-success",
        "LIBRE"  => "bg-secondary",
        _        => "bg-light text-dark border"
    };

    /// Color del borde lateral de la tarjeta
    public string ColorBorde => EstadoMaq == "ACTIVA"
        ? (EstaRetrasado ? "#e53e3e" : "#198754")
        : "#adb5bd";
}
