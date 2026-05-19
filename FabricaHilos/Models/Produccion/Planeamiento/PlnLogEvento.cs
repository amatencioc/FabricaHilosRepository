namespace FabricaHilos.Models.Produccion.Planeamiento;

/// <summary>
/// DTO de PLN_LOG_EVENTOS (§2.4 PKG_PLN).
/// Historial inmutable de eventos del ciclo de vida de un ítem.
/// TIPO_EVENTO: 'AV'=Avance | 'RE'=Reprogramación | 'AL'=Alerta | 'CI'=Cierre manual.
/// </summary>
public class PlnLogEvento
{
    public long     IdEvento         { get; set; }
    public long     IdSeguim         { get; set; }
    public long     NumPed           { get; set; }
    public int      Serie            { get; set; }
    public int      Nro              { get; set; }
    public int      NumDet           { get; set; }
    public string   CodPaso          { get; set; } = "";
    public string?  DescPaso         { get; set; }
    public string?  NombrePaso       { get; set; }    // resuelto desde caché de PLN_ESTADO_CODIGO
    public string   TipoEvento       { get; set; } = "AV";
    public DateTime FchEvento        { get; set; }
    public string?  TablaOrigen      { get; set; }
    public long?    IdObjetoOrigen   { get; set; }
    public decimal? KgCantidad       { get; set; }
    public DateTime? FchEstimadaAnt  { get; set; }   // solo TIPO_EVENTO='RE'
    public DateTime? FchEstimadaNue  { get; set; }   // solo TIPO_EVENTO='RE'
    public string?  Observacion      { get; set; }
    public string?  Usuario          { get; set; }
    public int      NroCiclo         { get; set; } = 1;

    // Helpers UI
    public string TipoEventoDesc => TipoEvento switch
    {
        "AV" => "Avance",
        "RE" => "Reprogramación",
        "AL" => "Alerta",
        "CI" => "Cierre manual",
        _    => TipoEvento
    };
    public string TipoEventoCss => TipoEvento switch
    {
        "AV" => "primary",
        "RE" => "warning",
        "AL" => "danger",
        "CI" => "secondary",
        _    => "light"
    };
}
