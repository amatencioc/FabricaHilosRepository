namespace FabricaHilos.Models.Capacitacion;

public class CapIntentoExamen
{
    public long    IdIntento       { get; set; }
    public long    IdInscripcion   { get; set; }
    public int     IdExamen        { get; set; }
    public int     NroIntento      { get; set; }
    public DateTime FchIni         { get; set; }    // CRÍTICO: registrado al iniciar (anti-trampa)
    public DateTime? FchFin        { get; set; }
    public decimal? PuntajeObt     { get; set; }
    public string  Aprobado        { get; set; } = "N";
    public string  Anulado         { get; set; } = "N";

    // Enriched
    public string? TituloExamen    { get; set; }
    public int?    TiempoMin       { get; set; }

    // Computed
    public bool  EstaAprobado      => Aprobado == "S";
    public bool  EstaEnProgreso    => FchFin == null && Anulado == "N";

    public int MinutosRestantes
    {
        get
        {
            if (!TiempoMin.HasValue) return 0;
            var elapsed = (DateTime.Now - FchIni).TotalMinutes;
            return Math.Max(0, (int)(TiempoMin.Value - elapsed));
        }
    }

    public DateTime FchVencimiento =>
        FchIni.AddMinutes(TiempoMin ?? 30).AddMinutes(1);  // +1 min de gracia
}

public class CapRespuesta
{
    public long   IdRespuesta  { get; set; }
    public long   IdIntento    { get; set; }
    public long   IdPregunta   { get; set; }
    public long?  IdOpcion     { get; set; }
    public string EsCorrecta   { get; set; } = "N";
}
