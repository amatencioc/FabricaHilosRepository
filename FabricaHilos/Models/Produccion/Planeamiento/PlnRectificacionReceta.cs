namespace FabricaHilos.Models.Produccion.Planeamiento;

public class PlnRectificacionReceta
{
    public long      Numero         { get; set; }
    public DateTime  FchRegistro    { get; set; }
    public DateTime? Fecha          { get; set; }
    public string    Area           { get; set; } = "";
    public string    Situacion      { get; set; } = "";
    public string    UserRegistro   { get; set; } = "";
    public string?   DescDefecto    { get; set; }
    public string    Partida        { get; set; } = "";
    public string    Material       { get; set; } = "";
    public string    ColorTecnico   { get; set; } = "";
    public string    DescCliente    { get; set; } = "";
    public string    DescLabo       { get; set; } = "";
    public DateTime? FentregaPed    { get; set; }
    public string    Estado         { get; set; } = "1";
    public string?   Laboratorista  { get; set; }
    public string?   Supervisor     { get; set; }
    public string    Proceso        { get; set; } = "";
    public string?   DefectoOrig    { get; set; }
    public string    CodCausa       { get; set; } = "";
    public string?   Observacion    { get; set; }
    public string?   MaqProd        { get; set; }
    public string?   DescMaqProd    { get; set; }
    public string    MarcaEnproc    { get; set; } = "";
    public DateTime? FchEnProceso   { get; set; }
    public string    MarcaRectif    { get; set; } = "";
    public DateTime? FchRectificado { get; set; }
    public string    MarcaAprob     { get; set; } = "";
    public DateTime? FchAprobado    { get; set; }

    public bool   EstaAprobada  => Estado == "6";
    public bool   EstaPendiente => Estado == "1";
    public bool   EstaEnProceso => Estado == "3";
    public bool   EstaActiva    => Estado == "1" || Estado == "3";
    public bool   EstaVencido   => FentregaPed.HasValue && FentregaPed.Value.Date < DateTime.Today;
    public int    DiasRetraso   => FentregaPed.HasValue
                                   ? (int)(DateTime.Today - FentregaPed.Value.Date).TotalDays : 0;

    public string EstadoLabel => Estado switch
    {
        "1" => "Pendiente",
        "3" => "En Proceso",
        "6" => "Aprobada",
        _   => "Anulada"
    };

    public string EstadoBadge => Estado switch
    {
        "1" => "warning",
        "3" => "info",
        "6" => "success",
        _   => "secondary"
    };
}
