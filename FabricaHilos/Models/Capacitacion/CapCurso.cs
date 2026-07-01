namespace FabricaHilos.Models.Capacitacion;

public class CapCurso
{
    public int     IdCurso            { get; set; }
    public int     IdCategoria        { get; set; }
    public string  Titulo             { get; set; } = "";
    public string? Descripcion        { get; set; }
    public string? Objetivo           { get; set; }
    public string? ImagenPortada      { get; set; }
    public int?    DuracionMin        { get; set; }
    public string  Nivel              { get; set; } = "B";   // B/I/A
    public string  Obligatorio        { get; set; } = "N";
    public decimal NotaAprobacion     { get; set; } = 70;
    public int     MaxIntentos        { get; set; } = 3;
    public string  TieneExamen        { get; set; } = "S";
    public string  TieneCertificado   { get; set; } = "S";
    public string  TieneTareas        { get; set; } = "N";
    public int?    CertValidezDias    { get; set; }
    public int?    IdCursoRequisito   { get; set; }
    public string? TituloRequisito    { get; set; }
    public decimal NotaMinRequisito   { get; set; } = 70;
    public string? UsrCreador         { get; set; }
    public DateTime FchCreacion       { get; set; }
    public DateTime? FchModif         { get; set; }
    public string  Estado             { get; set; } = "A";   // A=Publicado  I=Borrador  B=Archivado

    // ── Computed helpers ──────────────────────────────────────────────
    public string NivelTexto   => Nivel switch { "B" => "Básico", "I" => "Intermedio", "A" => "Avanzado", _ => "" };
    public bool   EsObligatorio => Obligatorio == "S";
    public string EstadoTexto  => Estado switch { "A" => "Publicado", "I" => "Borrador", "B" => "Archivado", _ => "" };
    public string DuracionFormato => DuracionMin.HasValue
        ? DuracionMin.Value >= 60
            ? $"{DuracionMin.Value / 60}h {DuracionMin.Value % 60}min"
            : $"{DuracionMin.Value} min"
        : "";

    // ── Enriquecido desde JOIN con CAP_CATEGORIA ──────────────────────
    public string? NombreCategoria { get; set; }
    public string  ColorCategoria  { get; set; } = "#0d6efd";
    public string  IconoCategoria  { get; set; } = "bi-mortarboard";

    // ── Para el catálogo: estado del alumno (CAP_INSCRIPCION + CAP_PROGRESO) ──
    public bool    EstaInscrito             { get; set; }
    public int     PctProgreso              { get; set; }     // 0-100
    public string? EstadoInscripcion        { get; set; }     // P/C/V/X
    public bool    TieneCertificadoEmitido  { get; set; }
    public int     TotalLecciones           { get; set; }
    public int     LeccionesVistas          { get; set; }
    public int?    DiasParaVencer           { get; set; }     // días hasta FCH_VENCIMIENTO

    // ── Para la vista del player ──────────────────────────────────────
    public long? IdInscripcion { get; set; }
    public bool  ExamenAprobado { get; set; }
    public int?  IdExamen       { get; set; }
}
