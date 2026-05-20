namespace FabricaHilos.Models.Produccion.Planeamiento;

public class PlnAlerta
{
    public long     IdAlerta   { get; set; }
    public long?    IdSeguim   { get; set; }
    public string   TipAlerta  { get; set; } = "";
    public string   Nivel      { get; set; } = "B";
    public string   Titulo     { get; set; } = "";
    public string?  Detalle    { get; set; }
    public DateTime FchAlerta  { get; set; }
    public DateTime? FchLimite { get; set; }
    public int?     DiasRetraso { get; set; }
    public string?  CodMaq     { get; set; }
    public string   Estado     { get; set; } = "A";

    // Datos desnormalizados del seguimiento
    public long?    NumPed        { get; set; }
    public int?     Serie         { get; set; }
    public int?     Nro           { get; set; }  // BUG FIX: V_PLN_ALERTAS_ACTIVAS.nro no se mapeaba
    public string?  CodArt        { get; set; }
    public string?  CodCliente    { get; set; }
    public string?  NombreCliente { get; set; }
    public string?  CodPasoAct    { get; set; }
    public string?  ColorUiPaso   { get; set; }

    // BUG FIX: V_PLN_ALERTAS_ACTIVAS.horas_sin_resolver (en realidad son días decimales; se convierte a horas al leer)
    public double?  HorasSinResolver { get; set; }

    // Campos de resolución (solo presentes en historial: ESTADO='R'/'I')
    public DateTime? FchResolucion  { get; set; }
    public string?   UsuarioResuelve { get; set; }

    // Alias para la vista
    public DateTime FchGeneracion => FchAlerta;

    public string NivelColor => Nivel switch
    {
        "C" => "danger",
        "A" => "warning",
        "M" => "info",
        "B" => "secondary",
        _   => "secondary"
    };

    public string NivelTexto => Nivel switch
    {
        "C" => "Crítico",
        "A" => "Alto",
        "M" => "Medio",
        "B" => "Bajo",
        _   => "Bajo"
    };

    public string TipoTexto => TipAlerta switch
    {
        "RET1" => "Retraso crítico",
        "RET2" => "Retraso alto",
        "SMP"  => "Sin planificación",
        "STN"  => "Sin ingresar a TT",
        "QCF"  => "CC rechazado",
        _      => TipAlerta
    };
}
