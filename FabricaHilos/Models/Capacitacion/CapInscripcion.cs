namespace FabricaHilos.Models.Capacitacion;

public class CapInscripcion
{
    public long    IdInscripcion  { get; set; }
    public string  CodUsuario     { get; set; } = "";
    public int     IdCurso        { get; set; }
    public DateTime FchInscripcion { get; set; }
    public DateTime? FchVencimiento { get; set; }
    public string? InscritoPor    { get; set; }
    public string  Obligatorio    { get; set; } = "N";
    public string  Estado         { get; set; } = "P";  // P/C/V/X

    // Enriched
    public string? TituloCurso    { get; set; }
    public string? NombreUsuario  { get; set; }
    public int     PctProgreso    { get; set; }
    public int?    DiasParaVencer => FchVencimiento.HasValue
        ? (int)(FchVencimiento.Value - DateTime.Today).TotalDays
        : null;

    // Examen info
    public int     TotalIntentos    { get; set; }
    public decimal? MejorNota       { get; set; }
    public string? ExamenAprobado   { get; set; }  // S/N/null
    public int?    IntentoAprobado  { get; set; }  // nro_intento en que aprobó
    public string? TieneExamen      { get; set; }  // S/N del curso
}

public class CapProgreso
{
    public long    IdProgreso      { get; set; }
    public long    IdInscripcion   { get; set; }
    public long    IdContenido     { get; set; }
    public DateTime FchInicio      { get; set; }
    public DateTime? FchFin        { get; set; }
    public int     SegReproducido  { get; set; }
    public string  Completado      { get; set; } = "N";
}
