namespace FabricaHilos.Models.Capacitacion;

public class CapContenido
{
    public long    IdContenido   { get; set; }
    public int     IdCurso       { get; set; }
    public string  Titulo        { get; set; } = "";
    public string  Tipo          { get; set; } = "VID";  // VID/PDF/TXT/URL/ARC
    public int     Orden         { get; set; }
    public string? RutaArchivo   { get; set; }    // path relativo al MediaBasePath
    public string? NombreArchOri { get; set; }    // nombre original para Content-Disposition
    public long?   TamanioBytes  { get; set; }    // para mostrar "12.4 MB" en sidebar
    public string? MimeType      { get; set; }    // video/mp4, application/pdf, etc.
    public string? UrlExterna    { get; set; }    // para TIPO='URL'
    public string? ContenidoHtml { get; set; }    // para TIPO='TXT'
    public int?    DuracionSeg   { get; set; }    // solo VID
    public string  Obligatorio   { get; set; } = "S";   // N=opcional
    public int?    IdSeccion     { get; set; }    // FK a CAP_SECCION
    public string  Activo        { get; set; } = "S";

    // ── Para la vista: progreso del alumno (CAP_PROGRESO) ────────────
    public bool   Completado       { get; set; }
    public int    SegReproducido   { get; set; }
    public bool   Bloqueado        { get; set; }   // true si el contenido anterior no está completado

    // ── Computed helpers ──────────────────────────────────────────────
    public string TipoIcono => Tipo switch
    {
        "VID" => "bi-play-circle-fill",
        "PDF" => "bi-file-earmark-pdf-fill",
        "TXT" => "bi-file-text-fill",
        "URL" => "bi-link-45deg",
        "ARC" => "bi-file-earmark-arrow-down-fill",
        _     => "bi-file"
    };

    public string DuracionFormato => DuracionSeg.HasValue
        ? $"{DuracionSeg.Value / 60}:{DuracionSeg.Value % 60:D2}"
        : "";

    public string TamanioFormato => TamanioBytes.HasValue ? TamanioBytes.Value switch
    {
        >= 1_048_576 => $"{TamanioBytes.Value / 1_048_576.0:F1} MB",
        >= 1_024     => $"{TamanioBytes.Value / 1_024.0:F0} KB",
        _            => $"{TamanioBytes.Value} B"
    } : "";
}
