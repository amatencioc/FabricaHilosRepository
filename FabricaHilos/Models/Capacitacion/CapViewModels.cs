namespace FabricaHilos.Models.Capacitacion;

// ── ViewModels compuestos para las vistas ─────────────────────────────

/// <summary>Mi Panel — dashboard del alumno</summary>
public class MiPanelVm
{
    public string       NombreUsuario         { get; set; } = "";
    public int          CursosEnCurso         { get; set; }
    public int          CursosCompletados     { get; set; }
    public int          Certificados          { get; set; }
    public int          HorasCapacitacion     { get; set; }
    public List<CapCurso> CursosActivos       { get; set; } = [];
    public List<CapCurso> CursosAprobados     { get; set; } = [];   // completados/aprobados
    public List<CapCurso> CursosRecomendados  { get; set; } = [];   // del catálogo, no inscritos
}

/// <summary>Catálogo de cursos con filtros y paginación</summary>
public class CatalogoVm
{
    public List<CapCategoria>   Categorias         { get; set; } = [];
    public List<CapCurso>       Cursos             { get; set; } = [];
    public int?                 FiltroCategoria    { get; set; }
    public string?              FiltroBusqueda     { get; set; }
    public string?              FiltroNivel        { get; set; }
    public bool                 SoloObligatorios   { get; set; }
    public bool                 SoloPendientes     { get; set; }
    // Paginación
    public int                  TotalCursos        { get; set; }
    public int                  Pagina             { get; set; } = 1;
    public int                  TamPag             { get; set; } = 12;
    public int                  TotalPaginas       => TamPag > 0 ? (int)Math.Ceiling((double)TotalCursos / TamPag) : 1;
    public bool                 TienePrevio        => Pagina > 1;
    public bool                 TieneSiguiente     => Pagina < TotalPaginas;
}

/// <summary>Detalle de un curso (landing page antes de inscribirse)</summary>
public class CursoDetalleVm
{
    public CapCurso              Curso              { get; set; } = new();
    public List<CapSeccion>      Secciones          { get; set; } = [];
    public List<CapContenido>    ContenidosSinSeccion { get; set; } = [];
    public CapCurso?             CursoRequisito     { get; set; }
    public bool                  RequisitoSatisfecho { get; set; } = true;
    public string?               MensajeRequisito   { get; set; }
    public List<CapIntentoExamen> Intentos          { get; set; } = [];
    public List<CapCurso>        CursosDependientes { get; set; } = [];
}

/// <summary>Player / reproductor de lección</summary>
public class CursoPlayerVm
{
    public CapCurso              Curso              { get; set; } = new();
    public long                  IdInscripcion      { get; set; }
    public int                   PctProgreso        { get; set; }
    public int                   LeccionesVistas    { get; set; }
    public int                   TotalLecciones     { get; set; }
    public CapContenido          Actual             { get; set; } = new();
    public CapContenido?         Anterior           { get; set; }
    public CapContenido?         Siguiente          { get; set; }
    public List<CapSeccion>      Secciones          { get; set; } = [];
    public List<CapContenido>    ContenidosSinSeccion { get; set; } = [];
    public bool                  TieneExamen        { get; set; }
    public int?                  IdExamen           { get; set; }
    public bool                  ExamenFinalAprobado  { get; set; }
    public bool                  ExamenFinalBloqueado { get; set; }
    public bool                  TieneCertificado   { get; set; }
    public bool                  CertificadoEmitido { get; set; }

    // Para sidebar: ID del contenido activo
    public long IdContenidoActual => Actual.IdContenido;
    public int  IdCurso           => Curso.IdCurso;
    public string TituloCurso     => Curso.Titulo;
}

/// <summary>Examen en progreso</summary>
public class ExamenRendirVm
{
    public long           IdIntento          { get; set; }
    public long           IdInscripcion      { get; set; }
    public int            IdExamen           { get; set; }
    public string         TituloCurso        { get; set; } = "";
    public string         TituloExamen       { get; set; } = "";
    public int            TiempoMin          { get; set; }
    public int            MinutosRestantes   { get; set; }
    public DateTime       FchVencimiento     { get; set; }
    public string         FchVencimientoISO  => FchVencimiento.ToString("yyyy-MM-ddTHH:mm:ss");
    public int            TotalPreguntas     { get; set; }
    public int            PregActual         { get; set; }   // 0-based
    public CapPregunta    PreguntaActual     { get; set; } = new();
    public List<bool>     Respondidas        { get; set; } = [];
    public bool           EsPrimeraPregunta  => PregActual == 0;
    public bool           EsUltimaPregunta   => PregActual == TotalPreguntas - 1;
    public int            PctPreguntaActual  => TotalPreguntas > 0
        ? (int)((PregActual + 1.0) / TotalPreguntas * 100)
        : 0;
}

/// <summary>Resultado del examen</summary>
public class ExamenResultadoVm
{
    public long    IdIntento          { get; set; }
    public int     IdExamen           { get; set; }
    public int     IdCurso            { get; set; }
    public long    IdInscripcion      { get; set; }
    public string  TituloCurso        { get; set; } = "";
    public string  TituloExamen       { get; set; } = "";
    public decimal PuntajeObt         { get; set; }
    public decimal NotaAprobacion     { get; set; }
    public bool    Aprobado           { get; set; }
    public int     NroIntento         { get; set; }
    public int     MaxIntentos        { get; set; }
    public bool    TieneCertificado   { get; set; }
    public bool    PuedeReintentar    => !Aprobado && NroIntento < MaxIntentos;
}
