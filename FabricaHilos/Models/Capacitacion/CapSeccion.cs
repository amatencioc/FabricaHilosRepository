namespace FabricaHilos.Models.Capacitacion;

public class CapSeccion
{
    public int     IdSeccion    { get; set; }
    public int     IdCurso      { get; set; }
    public string  Titulo       { get; set; } = "";
    public string? Descripcion  { get; set; }
    public int     Orden        { get; set; }
    public string  Activo       { get; set; } = "S";

    // ── Enriched (para el player) ─────────────────────────────────────
    public List<CapContenido> Contenidos    { get; set; } = [];
    public int   Total                      { get; set; }     // total de contenidos obligatorios
    public int   Completados                { get; set; }     // completados por el alumno
    public bool  TieneExamen                { get; set; }
    public int?  IdExamen                   { get; set; }
    public bool  ExamenAprobado             { get; set; }
    public bool  ExamenBloqueado            { get; set; }     // true si aún hay contenidos pendientes

    public bool EstaCompleta => Total > 0 && Completados >= Total;
}
