namespace FabricaHilos.Models.SaludOcupacional;

// ─────────────────────────────────────────────────────────────────────────────
// Catálogo
// ─────────────────────────────────────────────────────────────────────────────

public class SoConcesionaria
{
    public int    IdConc   { get; set; }
    public string Nombre   { get; set; } = string.Empty;
    public string? Ruc     { get; set; }
    public string? Contacto{ get; set; }
    public string  Estado  { get; set; } = "A";
}

public class SoComedor
{
    public int    IdCom      { get; set; }
    public string Nombre     { get; set; } = string.Empty;
    public string? Ubicacion { get; set; }
    public int?   IdConc     { get; set; }
    public string? NombreConc{ get; set; }  // desnormalizado para UI
    public int?   Capacidad  { get; set; }
    public string Tipo       { get; set; } = "INDUSTRIAL";
    public string Estado     { get; set; } = "A";
}

public class SoInspRubro
{
    public int    IdRubro   { get; set; }
    public string CodRubro  { get; set; } = string.Empty;
    public string Nombre    { get; set; } = string.Empty;
    public int    Orden     { get; set; }
    public string? IconoBi  { get; set; }
    public List<SoInspItem> Items { get; set; } = new();
}

public class SoInspItem
{
    public int    IdItem      { get; set; }
    public int    IdRubro     { get; set; }
    public string CodItem     { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public int    PtsMax      { get; set; }
    public int    Orden       { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Inspección
// ─────────────────────────────────────────────────────────────────────────────

public class SoInspeccion
{
    public long      IdInsp       { get; set; }
    public int       IdCom        { get; set; }
    public string?   NombreComedor{ get; set; }
    public string?   NombreConc   { get; set; }
    public DateTime  FechaInsp    { get; set; }
    public string?   HoraInsp     { get; set; }
    public string?   Encargada    { get; set; }
    public string?   Inspector    { get; set; }
    public string?   Medico       { get; set; }
    public decimal   PtsObtenidos { get; set; }
    public decimal?  PtsMaximo    { get; set; }
    public decimal?  PctCumpl     { get; set; }
    public string?   Calificacion { get; set; }
    public string?   Observaciones{ get; set; }
    public string    Estado       { get; set; } = "B";
    public DateTime  FchCrea      { get; set; }
    public string?   UsrCrea      { get; set; }
    public DateTime? FchCierre    { get; set; }

    // Helpers UI
    public string EstadoLabel => Estado switch
    {
        "B" => "Borrador",
        "C" => "Cerrada",
        "A" => "Anulada",
        _   => Estado
    };
    public string EstadoBadgeCss => Estado switch
    {
        "B" => "so-badge-proc",
        "C" => "so-badge-ok",
        "A" => "so-badge-danger",
        _   => "so-badge-secondary"
    };
    public string CalifBadgeCss => Calificacion switch
    {
        "ACEPTABLE"       => "so-semaforo-ok",
        "CON OBSERVACION" => "so-semaforo-warn",
        "NO ACEPTABLE"    => "so-semaforo-danger",
        _                 => ""
    };
    public string CalifDotCss => Calificacion switch
    {
        "ACEPTABLE"       => "so-dot-ok",
        "CON OBSERVACION" => "so-dot-warn",
        "NO ACEPTABLE"    => "so-dot-danger",
        _                 => ""
    };
    public bool EsBorrador => Estado == "B";
    public bool EstaCerrada => Estado == "C";
}

public class SoInspDetalle
{
    public long   IdDetalle   { get; set; }
    public long   IdInsp      { get; set; }
    public int    IdItem      { get; set; }
    public string CodItem     { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public int    PtsMax      { get; set; }
    public int    Puntaje     { get; set; }
    public string? Hallazgo   { get; set; }
    public string? Responsable{ get; set; }
    public string  TieneAccion{ get; set; } = "N";
    public int     IdRubro    { get; set; }
    public string  CodRubro   { get; set; } = string.Empty;

    // Helpers
    public bool   NoCumple    => Puntaje == 0;
    public bool   TieneHallazgo => !string.IsNullOrWhiteSpace(Hallazgo);
    public string PuntajeBtnCss0 => Puntaje == 0 ? "selected-0" : "";
    public string PuntajeBtnCss2 => Puntaje == 2 ? "selected-2" : "";
    public string PuntajeBtnCss4 => Puntaje == 4 ? "selected-4" : "";
}

public class SoInspEvidencia
{
    public long      IdEvidencia { get; set; }
    public long      IdDetalle   { get; set; }
    public long      IdInsp      { get; set; }
    public string    NombreArch  { get; set; } = string.Empty;
    public string?   RutaArch    { get; set; }
    public string?   Descripcion { get; set; }
    public DateTime  FchCarga    { get; set; }
    public string?   Usuario     { get; set; }

    /// Ruta física en disco — se rellena en el controller antes de generar el PDF
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string?   RutaFisica  { get; set; }
}

public class SoInspAccion
{
    public long      IdAccion       { get; set; }
    public long      IdDetalle      { get; set; }
    public long      IdInsp         { get; set; }
    public string    Descripcion    { get; set; } = string.Empty;
    public string?   Responsable    { get; set; }
    public DateTime? FchLimite      { get; set; }
    public DateTime? FchCierre      { get; set; }
    public string    Estado         { get; set; } = "P";
    public string?   Observacion    { get; set; }
    public string?   UsuarioCierre  { get; set; }
    public DateTime  FchCrea        { get; set; }

    // Datos desnormalizados para UI
    public string?   CodItem        { get; set; }
    public string?   DescItem       { get; set; }
    public string?   NombreComedor  { get; set; }
    public DateTime? FechaInsp      { get; set; }

    public bool EsVencida   => FchLimite.HasValue && FchLimite.Value < DateTime.Today && Estado != "R";

    public string EstadoLabel => Estado switch
    {
        "P" => "Pendiente",
        "E" => "En Proceso",
        "R" => "Resuelta",
        _   => Estado
    };
    public string EstadoBadgeCss => Estado switch
    {
        "P" => "so-badge-pend",
        "E" => "so-badge-proc",
        "R" => "so-badge-done",
        _   => "so-badge-secondary"
    };
}

// ─────────────────────────────────────────────────────────────────────────────
// ViewModels
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>ViewModel para el Dashboard de inspecciones</summary>
public class SoDashboardViewModel
{
    public IReadOnlyList<SoInspeccion> UltimasInspecciones { get; set; } = Array.Empty<SoInspeccion>();
    public IReadOnlyList<SoInspAccion> AccionesPendientes  { get; set; } = Array.Empty<SoInspAccion>();
    public IReadOnlyList<SoComedor>    Comedores           { get; set; } = Array.Empty<SoComedor>();

    // KPIs
    public int    TotalInspecciones    { get; set; }
    public int    InspeccionesEsteAno  { get; set; }
    public int    AccionesPend         { get; set; }
    public int    AccionesVencidas     { get; set; }
    public decimal? UltimoPctCumpl     { get; set; }
    public string?  UltimaCalificacion { get; set; }
}

/// <summary>ViewModel para crear/editar inspección (checklist interactivo)</summary>
public class SoNuevaInspeccionViewModel
{
    public SoInspeccion              Inspeccion { get; set; } = new();
    public IReadOnlyList<SoComedor>  Comedores  { get; set; } = Array.Empty<SoComedor>();

    /// <summary>Rubros con sus ítems agrupados (para renderizar el checklist)</summary>
    public IReadOnlyList<SoRubroConDetalles> Rubros { get; set; } = Array.Empty<SoRubroConDetalles>();
}

/// <summary>Rubro del checklist con los detalles ya resueltos</summary>
public class SoRubroConDetalles
{
    public SoInspRubro              Rubro    { get; set; } = new();
    public IReadOnlyList<SoInspDetalle> Items { get; set; } = Array.Empty<SoInspDetalle>();
    public int PtsObtenidosRubro => Items.Sum(i => i.Puntaje);
    public int PtsMaximoRubro    => Items.Sum(i => i.PtsMax);
    public decimal PctRubro      => PtsMaximoRubro == 0 ? 0
        : Math.Round((decimal)PtsObtenidosRubro / PtsMaximoRubro * 100, 1);
    public string PctBarCss => PctRubro >= 75 ? "ok" : PctRubro >= 51 ? "warn" : "danger";
}

/// <summary>ViewModel para la vista de detalle (inspección cerrada)</summary>
public class SoDetalleInspeccionViewModel
{
    public SoInspeccion                       Inspeccion  { get; set; } = new();
    public IReadOnlyList<SoRubroConDetalles>  Rubros      { get; set; } = Array.Empty<SoRubroConDetalles>();
    public IReadOnlyList<SoInspAccion>        Acciones    { get; set; } = Array.Empty<SoInspAccion>();
    public IReadOnlyList<SoInspEvidencia>     Evidencias  { get; set; } = Array.Empty<SoInspEvidencia>();
}

/// <summary>ViewModel para la bandeja de acciones correctivas</summary>
public class SoAccionesViewModel
{
    public IReadOnlyList<SoInspAccion> Acciones    { get; set; } = Array.Empty<SoInspAccion>();
    public int TotalPendientes  { get; set; }
    public int TotalEnProceso   { get; set; }
    public int TotalVencidas    { get; set; }
    public int TotalResueltas   { get; set; }
    public string FiltroEstado  { get; set; } = "PR";  // 'P','E','R','PR'=todos abiertos
}
