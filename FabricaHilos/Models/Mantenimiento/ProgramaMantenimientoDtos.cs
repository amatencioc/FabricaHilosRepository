namespace FabricaHilos.Models.Mantenimiento;

/// <summary>
/// Fila de listado — programas de mantenimiento (MA_PROGRAMA) asignados a un mecánico.
/// Fuente: PKG_MA_PROGRAMA.P_LISTA_ASIGNADOS (esquema SIG, tablas legacy MA_PROGRAMA*).
/// </summary>
public class ProgramaMantenimientoListItemDto
{
    public long      NumProg    { get; set; }
    public string?   CCodigo    { get; set; }
    public string?   CodMaq     { get; set; }
    public string?   CodAct     { get; set; }
    public string?   ActivoDesc { get; set; }
    public string?   Tipo       { get; set; }
    public string?   Clase      { get; set; }
    public string?   Estado     { get; set; }
    public string?   EstadoDesc { get; set; }
    public string?   Detalle    { get; set; }
    public string?   Informe    { get; set; }
    public DateTime? FechaIni   { get; set; }
    public DateTime? FechaFin   { get; set; }
    public DateTime? FchProg    { get; set; }
    public DateTime? FchFirma   { get; set; }
    public string?   RespFirma  { get; set; }
    public bool      Validado   { get; set; }

    /// <summary>Resuelto en BD por PKG_MA_PROGRAMA.F_ES_JEFE_PROGRAMA para el responsable de firma logueado (v1.6).</summary>
    public bool      PuedeValidar { get; set; }

    public string TipoDesc  => Tipo  switch { "M" => "Mecánico",    "E" => "Eléctrico",  _ => "-" };
    public string ClaseDesc => Clase switch { "P" => "Preventivo",  "C" => "Correctivo", _ => "-" };

    public string EstadoBadgeClass => Estado switch
    {
        "1" => "secondary",
        "2" => "info",
        "3" => "success",
        "8" => "warning",
        "9" => "danger",
        _   => "secondary"
    };
}

/// <summary>Cabecera de detalle — incluye nombres resueltos (mecánico / quien validó / jefe de área).</summary>
public class ProgramaMantenimientoCabeceraDto : ProgramaMantenimientoListItemDto
{
    public string? Mecanico        { get; set; }
    public string? RespFirmaNombre { get; set; }

    // Centro de costo de la máquina/activo y su jefe de área vigente (v1.1).
    public string? Ccosto          { get; set; }
    public string? CcostoNombre    { get; set; }
    public string? JefeCCodigo     { get; set; }
    public string? JefeNombre      { get; set; }

    /// <summary>Resuelto en BD por PKG_MA_PROGRAMA.F_ES_JEFE_PROGRAMA para el usuario que pidió el detalle.</summary>
    public bool PuedeValidarJefe   { get; set; }
}

/// <summary>
/// Fila de listado — programas Ejecutados y sin validar que le corresponde revisar a un
/// jefe de área. Fuente: PKG_MA_PROGRAMA.P_LISTA_PARA_VALIDAR.
/// </summary>
public class ProgramaPendienteValidarDto
{
    public long      NumProg      { get; set; }
    public string?   CCodigo      { get; set; }
    public string?   Mecanico     { get; set; }
    public string?   CodMaq       { get; set; }
    public string?   CodAct       { get; set; }
    public string?   ActivoDesc   { get; set; }
    public string?   Ccosto       { get; set; }
    public string?   CcostoNombre { get; set; }
    public string?   EncargadoCCodigo { get; set; }
    public string?   EncargadoNombre  { get; set; }
    public string?   Tipo         { get; set; }
    public string?   Clase        { get; set; }
    public string?   Detalle      { get; set; }
    public string?   Informe      { get; set; }
    public DateTime? FechaIni     { get; set; }
    public DateTime? FechaFin     { get; set; }
    public DateTime? FchProg      { get; set; }
    public string?   Estado       { get; set; }
    public string?   EstadoDesc   { get; set; }

    public string TipoDesc  => Tipo  switch { "M" => "Mecánico",   "E" => "Eléctrico",  _ => "-" };
    public string ClaseDesc => Clase switch { "P" => "Preventivo", "C" => "Correctivo", _ => "-" };

    /// <summary>El botón "Firmar / Validar" solo debe verse cuando el programa está Ejecutado.</summary>
    public bool PuedeFirmar => Estado == "3";

    public string EstadoBadgeClass => Estado switch
    {
        "1" => "secondary",
        "2" => "info",
        "3" => "success",
        "8" => "warning",
        "9" => "danger",
        _   => "secondary"
    };
}

/// <summary>
/// Fila de listado — TODOS los programas Ejecutados (validados y pendientes) de los
/// centros de costo donde el usuario es el JEFE (autoridad de escalamiento). Vista de
/// solo lectura, el jefe no valida (v1.11). Fuente: PKG_MA_PROGRAMA.P_LISTA_JEFE.
/// </summary>
public class ProgramaJefeVistaDto
{
    public long      NumProg          { get; set; }
    public string?   CCodigo          { get; set; }
    public string?   Mecanico         { get; set; }
    public string?   CodMaq           { get; set; }
    public string?   CodAct           { get; set; }
    public string?   ActivoDesc       { get; set; }
    public string?   Ccosto           { get; set; }
    public string?   CcostoNombre     { get; set; }
    public string?   EncargadoCCodigo { get; set; }
    public string?   EncargadoNombre  { get; set; }
    public string?   Tipo             { get; set; }
    public string?   Clase            { get; set; }
    public string?   Detalle          { get; set; }
    public string?   Informe          { get; set; }
    public DateTime? FechaIni         { get; set; }
    public DateTime? FechaFin         { get; set; }
    public DateTime? FchProg          { get; set; }
    public DateTime? FchFirma         { get; set; }
    public string?   RespFirma        { get; set; }
    public string?   RespFirmaNombre  { get; set; }
    public bool      Validado         { get; set; }

    public string TipoDesc  => Tipo  switch { "M" => "Mecánico",   "E" => "Eléctrico",  _ => "-" };
    public string ClaseDesc => Clase switch { "P" => "Preventivo", "C" => "Correctivo", _ => "-" };
}

/// <summary>Tarea planificada del programa (MA_PROGRAMA_D + catálogo MA_TAREA).</summary>
public class ProgramaMantenimientoTareaDto
{
    public int?    ItemActiv { get; set; }
    public long?   CodTarea  { get; set; }
    public string? TareaDesc { get; set; }
    public string? Detalle   { get; set; }
    public string? Estado    { get; set; }
}

/// <summary>Sesión de ejecución real registrada (MA_PROGRAMA_T) — puede haber varias por programa.</summary>
public class ProgramaMantenimientoTiempoDto
{
    public string?   CCodigo     { get; set; }
    public string?   Mecanico    { get; set; }
    public DateTime? FechaIni    { get; set; }
    public DateTime? FechaFin    { get; set; }
    public string?   Estado      { get; set; }
    public string?   Observacion { get; set; }
    public decimal   Horas       { get; set; }
}

/// <summary>Material/artículo consumido (MA_PROGRAMA_A + catálogo ARTICUL).</summary>
public class ProgramaMantenimientoMaterialDto
{
    public string? TipoDoc  { get; set; }
    public int?    Serie    { get; set; }
    public long?   NroDoc   { get; set; }
    public string? CodArt   { get; set; }
    public string? ArtDesc  { get; set; }
    public decimal Cantidad { get; set; }
}

/// <summary>ViewModel compuesto para la pantalla de detalle de un programa.</summary>
public class ProgramaMantenimientoDetalleViewModel
{
    public ProgramaMantenimientoCabeceraDto?         Cabecera   { get; set; }
    public List<ProgramaMantenimientoTareaDto>       Tareas     { get; set; } = new();
    public List<ProgramaMantenimientoTiempoDto>      Tiempos    { get; set; } = new();
    public List<ProgramaMantenimientoMaterialDto>    Materiales { get; set; } = new();

    /// <summary>Solo el jefe de área del centro de costo de la máquina puede validar, y solo si el programa está Ejecutado ('3') y sin validar (v1.1 — ya no es el mecánico asignado).</summary>
    public bool PuedeValidar =>
        Cabecera != null
        && Cabecera.Estado == "3"
        && !Cabecera.Validado
        && Cabecera.PuedeValidarJefe;
}
