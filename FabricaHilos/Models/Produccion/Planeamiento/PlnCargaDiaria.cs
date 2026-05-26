namespace FabricaHilos.Models.Produccion.Planeamiento;

public class PlnCargaDiaria
{
    public DateTime Fecha           { get; set; }
    public string   CodMaq          { get; set; } = "";
    public string?  NombreMaq       { get; set; }
    public string   TpMaq           { get; set; } = "";  // 'H'=Hilandería (H_PRODUCCION_D), 'W'=Tintorería (TT_RPRODUC TIPODOC='PA', activo 2021+). 'T' = alias legado.
    public double   HorasCapacidad  { get; set; }
    public decimal  KgCapacidad     { get; set; }
    public double   HorasAsignadas  { get; set; }
    public decimal  KgAsignados     { get; set; }
    public int      NroPedidos      { get; set; }
    public double   HorasReal       { get; set; }   // horas_real (V_PLN_CARGA_MAQUINAS §8.5)
    public decimal  KgReal          { get; set; }   // kg_real    (V_PLN_CARGA_MAQUINAS §8.5)
    public double   PctUtilizacion  { get; set; }
    public double   PctCarga        { get; set; }
    public string   IndSobrecargada { get; set; } = "N";
    public string   EstadoCarga     { get; set; } = "";  // SOBRECARGADA/CARGA_ALTA/CARGA_MEDIA/DISPONIBLE

    // TP_MAQ: 'H'=Hilandería (grupos C/R/T/U de H_PRODUCCION_D), 'W'=Tintorería (TT_RPRODUC TIPODOC='PA').
    // FIX: SP_PLN_CARGA_DIARIA_REFRESH §6 inserta TP_MAQ='W' (no 'T') para tintorería desde 2021.
    public string Area => TpMaq switch
    {
        "H"        => "Hilandería",
        "T" or "W" => "Tintorería",
        _          => "Otras"
    };

    // Nombre legible — misma lógica que maqNombre() en Pedido.cshtml para coherencia visual.
    // R03→"Rodete Nº3 · THIES", M01→"Madejas Nº1 · HANK", MR2→"Mad.Rodete Nº2", S01→"Secadora Nº1"
    public string NombreMaqDisplay
    {
        get
        {
            if (!string.IsNullOrEmpty(NombreMaq)) return NombreMaq;
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

    public bool    EstaSobrecargada => IndSobrecargada == "S";

    public string ColorSemaforo => PctCarga switch
    {
        > 95 => "#dc3545",
        > 80 => "#fd7e14",
        > 50 => "#ffc107",
        _    => "#198754"
    };
}
