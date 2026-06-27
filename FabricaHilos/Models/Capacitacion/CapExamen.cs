namespace FabricaHilos.Models.Capacitacion;

public class CapExamen
{
    public int     IdExamen          { get; set; }
    public int     IdCurso           { get; set; }
    public string  Titulo            { get; set; } = "Evaluación final";
    public string? Instrucciones     { get; set; }
    public int     TiempoMin         { get; set; } = 30;
    public string  MezclarPreg       { get; set; } = "S";
    public string  MezclarOpc        { get; set; } = "S";
    public string  MostrarResult     { get; set; } = "S";
    public string  ModoPreguntas     { get; set; } = "F";   // F=Fijas  R=Aleatorio banco
    public int?    NroPregAleatorias { get; set; }
    public int?    IdSeccion         { get; set; }
    public string  TipoExamen        { get; set; } = "F";   // F=Final  S=Sección
    public string  Activo            { get; set; } = "S";

    // Computed
    public bool EsModoAleatorio => ModoPreguntas == "R";
    public bool EsFinal         => TipoExamen == "F";
}

public class CapPregunta
{
    public long    IdPregunta  { get; set; }
    public int?    IdExamen    { get; set; }
    public long?   IdBanco     { get; set; }
    public string  Enunciado   { get; set; } = "";
    public string  TipoPreg    { get; set; } = "OM";
    // OM=Opción múltiple una  OV=Varias  VF=Verdadero/Falso
    // EMP=Emparejamiento  RC=Respuesta corta  ENS=Ensayo
    public decimal Puntaje     { get; set; } = 1;
    public string? ImagenUrl   { get; set; }
    public int?    Orden       { get; set; }
    public string  Activo      { get; set; } = "S";

    // Enriched
    public List<CapOpcion> Opciones { get; set; } = [];

    // Computed
    public bool RequiereCalificacionManual => TipoPreg is "RC" or "ENS";
    public bool TieneOpciones             => TipoPreg is "OM" or "OV" or "VF";
    public bool TienePares                => TipoPreg == "EMP";

    public string TipoPregIcono => TipoPreg switch
    {
        "OM"  => "bi-ui-radios",
        "OV"  => "bi-check2-square",
        "VF"  => "bi-toggle-on",
        "EMP" => "bi-arrow-left-right",
        "RC"  => "bi-input-cursor-text",
        "ENS" => "bi-journal-text",
        _     => "bi-question"
    };

    public string TipoPregTexto => TipoPreg switch
    {
        "OM"  => "Opción múltiple",
        "OV"  => "Varias correctas",
        "VF"  => "Verdadero/Falso",
        "EMP" => "Emparejamiento",
        "RC"  => "Respuesta corta",
        "ENS" => "Ensayo",
        _     => ""
    };
}

public class CapOpcion
{
    public long   IdOpcion    { get; set; }
    public long   IdPregunta  { get; set; }
    public string Texto       { get; set; } = "";
    public string EsCorrecta  { get; set; } = "N";
    public int?   Orden       { get; set; }

    // Para la vista del examen (estado de selección del alumno)
    public bool Seleccionada  { get; set; }
}
