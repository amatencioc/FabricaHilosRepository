namespace FabricaHilos.Models.Capacitacion;

public class CapCertificado
{
    public int     IdCertificado  { get; set; }
    public long    IdInscripcion  { get; set; }
    public long    IdIntento      { get; set; }
    public string  CodUsuario     { get; set; } = "";
    public string  NombreUsuario  { get; set; } = "";   // snapshot del momento de emisión
    public string  TituloCurso    { get; set; } = "";   // snapshot del momento de emisión
    public decimal PuntajeObt     { get; set; }
    public DateTime FchEmision    { get; set; }
    public DateTime? FchVencimiento { get; set; }
    public string  CodigoVerif    { get; set; } = "";   // GUID UUID v4
    public string? UrlVerif       { get; set; }
    public string? RutaPdf        { get; set; }
    public string  Estado         { get; set; } = "V";  // V=Vigente  R=Renovado  X=Anulado

    // Computed
    public bool EsVigente =>
        Estado == "V" &&
        (!FchVencimiento.HasValue || FchVencimiento.Value >= DateTime.Today);

    public string EstadoTexto => Estado switch
    {
        "V" => "Vigente",
        "R" => "Renovado",
        "X" => "Anulado",
        _   => ""
    };
}
