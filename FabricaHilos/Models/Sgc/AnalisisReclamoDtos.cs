namespace FabricaHilos.Models.Sgc;

// ────────────────────────────────────────────────────────────────────────────
//  ANÁLISIS DE RECLAMOS — DTOs y ViewModels
// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Cabecera del reclamo (listado y detalle).
/// </summary>
public class ReclamoDto
{
    public long      IdReclamo       { get; set; }
    public string    CodCliente      { get; set; } = "";
    public string?   NomCliente      { get; set; }
    public string    Contacto        { get; set; } = "";
    public string    Telefono        { get; set; } = "";
    public string    Asunto          { get; set; } = "";
    public string    Estado          { get; set; } = "01";
    public string    UsuVendedor     { get; set; } = "";
    public DateTime  FchCreacion     { get; set; }
    public string?   UsuAnalista     { get; set; }
    public DateTime? FchAnalisis     { get; set; }
    public string?   UsuGerente      { get; set; }
    public DateTime? FchAprobacion   { get; set; }
    public string?   MotRechazo      { get; set; }
    public int       TotalDescargos  { get; set; }
    public int       TotalArchivos   { get; set; }

    // ── Helpers de presentación ──────────────────────────────────────────────
    // Estados: 01=Abierto  02=En Revisión  03=Pend.Aprobación  04=Aprobado  05=Rechazado
    public string EstadoDescripcion => Estado switch
    {
        "01" => "Abierto",
        "02" => "En Revisión",
        "03" => "Pend. Aprobación",
        "04" => "Aprobado",
        "05" => "Rechazado",
        _    => Estado
    };

    public string EstadoBadge => Estado switch
    {
        "01" => "warning",
        "02" => "info",
        "03" => "primary",
        "04" => "success",
        "05" => "danger",
        _    => "secondary"
    };

    public string EstadoIcono => Estado switch
    {
        "01" => "bi-folder2-open",
        "02" => "bi-hourglass-split",
        "03" => "bi-send",
        "04" => "bi-check-circle-fill",
        "05" => "bi-x-circle-fill",
        _    => "bi-circle"
    };

    /// <summary>True si el reclamo ya fue resuelto (aprobado o rechazado) por la gerencia.</summary>
    public bool EsFinalizado => Estado is "04" or "05";
}

/// <summary>
/// Descargo de un participante (Vendedor o Analista de Calidad).
/// </summary>
public class ReclamoDescargoDto
{
    public long     IdDescargo   { get; set; }
    public long     IdReclamo    { get; set; }
    public string   Rol          { get; set; } = "";   // 'VD' o 'AC'
    public string   Descripcion  { get; set; } = "";
    public string   Usuario      { get; set; } = "";
    public DateTime FchRegistro  { get; set; }

    public bool   EsVendedor  => Rol == "VD";
    public bool   EsAnalista  => Rol == "AC";
    public bool   EsGerente   => Rol == "GE";
    public string RolTexto    => Rol switch
    {
        "VD" => "Vendedor",
        "AC" => "Analista de Calidad",
        "GE" => "Gerente",
        _    => Rol
    };
    public string RolIcono    => Rol switch
    {
        "VD" => "bi-person-badge",
        "AC" => "bi-shield-check",
        "GE" => "bi-person-check-fill",
        _    => "bi-person"
    };
    public string RolColorClase => Rol switch
    {
        "VD" => "primary",
        "AC" => "warning",
        "GE" => "success",
        _    => "secondary"
    };
}

/// <summary>
/// Archivo adjunto (referencia en BD; el físico está en disco).
/// </summary>
public class ReclamoArchivoDto
{
    public long     IdArchivo     { get; set; }
    public long     IdReclamo     { get; set; }
    public string   Rol           { get; set; } = "";
    public string   NombreOrig    { get; set; } = "";
    public string   NombreServer  { get; set; } = "";
    public string?  MimeType      { get; set; }
    public long     TamanioBytes  { get; set; }
    public string   Usuario       { get; set; } = "";
    public DateTime FchCarga      { get; set; }

    public bool   EsVendedor    => Rol == "VD";
    public bool   EsAnalista    => Rol == "AC";
    public bool   EsGerente     => Rol == "GE";
    public string RolTexto      => Rol switch { "VD" => "Vendedor", "AC" => "Analista de Calidad", "GE" => "Gerente", _ => Rol };

    public string TamanioTexto => TamanioBytes switch
    {
        < 1_024               => $"{TamanioBytes} B",
        < 1_048_576           => $"{TamanioBytes / 1_024.0:F1} KB",
        _                     => $"{TamanioBytes / 1_048_576.0:F2} MB"
    };

    /// <summary>Ícono Bootstrap según el tipo MIME o extensión.</summary>
    public string Icono
    {
        get
        {
            var mime = (MimeType ?? "").ToLower();
            var ext  = System.IO.Path.GetExtension(NombreOrig).ToLower();
            if (mime.StartsWith("image/"))                return "bi-file-image";
            if (mime.StartsWith("video/"))                return "bi-file-play";
            if (mime.StartsWith("audio/"))                return "bi-file-music";
            if (mime == "application/pdf")                return "bi-file-pdf";
            if (mime.Contains("word")   || ext == ".doc" || ext == ".docx") return "bi-file-word";
            if (mime.Contains("excel")  || ext == ".xls" || ext == ".xlsx") return "bi-file-excel";
            if (mime == "message/rfc822" || ext == ".eml") return "bi-envelope";
            if (mime.Contains("zip")    || ext == ".zip")  return "bi-file-zip";
            return "bi-file-earmark";
        }
    }

    /// <summary>True si el archivo puede mostrarse en línea en el navegador.</summary>
    public bool EsVisualizableInline
    {
        get
        {
            var mime = (MimeType ?? "").ToLower();
            return mime.StartsWith("image/")
                || mime.StartsWith("video/")
                || mime.StartsWith("audio/")
                || mime == "application/pdf";
        }
    }
}

// ────────────────────────────────────────────────────────────────────────────
//  ViewModels para las vistas
// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// ViewModel para la vista Detalle del reclamo.
/// </summary>
public class ReclamoDetalleVm
{
    public ReclamoDto               Reclamo    { get; set; } = new();
    public List<ReclamoDescargoDto> Descargos  { get; set; } = new();
    public List<ReclamoArchivoDto>  Archivos   { get; set; } = new();

    /// <summary>
    /// Rol del usuario actual respecto a este reclamo, derivado del token ACCESO_WEB de Oracle:
    /// "VD" = Vendedor       → SgcAnalisisReclamo[VD] — agrega descargos y evidencia (sección vendedor)
    /// "AC" = Analista       → SgcAnalisisReclamo[AC] — agrega descargos y evidencia + gestiona estados
    /// "GE" = Gerencia       → SgcAnalisisReclamo[GE] — solo lectura + aprueba/rechaza cuando estado=03
    /// "OB" = Observador     → SgcAnalisisReclamo[OB] o Sgc[OB] — solo lectura total
    /// </summary>
    public string RolUsuario { get; set; } = "OB";

    public bool PuedeEscribirVD => RolUsuario == "VD";
    public bool PuedeEscribirAC => RolUsuario == "AC";
    public bool PuedeAprobarGE  => RolUsuario == "GE";
    public bool EsSoloLectura   => RolUsuario == "OB";
}

// ────────────────────────────────────────────────────────────────────────────
//  Request / Form models
// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Datos del formulario para crear un nuevo reclamo.
/// El vendedor selecciona cliente, ingresa contacto/teléfono/asunto/descargo
/// y opcionalmente adjunta archivos.
/// </summary>
public class CrearReclamoRequest
{
    public string              CodCliente { get; set; } = "";
    public string              NomCliente { get; set; } = "";  // se resuelve desde el combo
    public string              Contacto   { get; set; } = "";
    public string              Telefono   { get; set; } = "";
    public string              Asunto     { get; set; } = "";
    public string              Descargo   { get; set; } = "";
    public List<IFormFile>?    Archivos   { get; set; }
}

/// <summary>
/// Datos para agregar un descargo desde la vista Detalle.
/// </summary>
public class AgregarDescargoRequest
{
    public long   IdReclamo   { get; set; }
    public string Rol         { get; set; } = "";   // 'VD', 'AC' o 'GE'
    public string Descripcion { get; set; } = "";
}

/// <summary>
/// Datos para subir archivos desde la vista Detalle.
/// </summary>
public class SubirArchivosReclamoRequest
{
    public long             IdReclamo { get; set; }
    public string           Rol       { get; set; } = "";   // 'VD', 'AC' o 'GE'
    public List<IFormFile>? Archivos  { get; set; }
}

/// <summary>Analista escala el reclamo a Gerencia para aprobación.</summary>
public class EscalarGerenciaRequest
{
    public long IdReclamo { get; set; }
}

/// <summary>Gerente aprueba el reclamo (SÍ es un reclamo válido).</summary>
public class AprobarReclamoRequest
{
    public long    IdReclamo   { get; set; }
    public string? Observacion { get; set; }
}

/// <summary>Gerente rechaza el reclamo (NO es un reclamo válido).</summary>
public class RechazarReclamoRequest
{
    public long   IdReclamo { get; set; }
    public string Motivo    { get; set; } = "";
}

/// <summary>
/// DTO de cliente para el combo del formulario Nuevo.
/// </summary>
public class ClienteComboDto
{
    public string CodCliente { get; set; } = "";
    public string NomCliente { get; set; } = "";
}

/// <summary>
/// ViewModel para la partial _ListaArchivos.cshtml.
/// </summary>
public class ListaArchivosVm
{
    public List<ReclamoArchivoDto> Archivos      { get; set; } = new();
    public long                    IdReclamo     { get; set; }
    /// <summary>Color Bootstrap: "primary", "warning", "success", etc.</summary>
    public string                  CssClase      { get; set; } = "primary";
    /// <summary>Mostrar botón Eliminar. False cuando la sección está bloqueada.</summary>
    public bool                    PuedeEliminar { get; set; } = false;
}

/// <summary>
/// ViewModel para la partial _FormSubirArchivos.cshtml.
/// </summary>
public class FormSubirArchivosVm
{
    public long   IdReclamo     { get; set; }
    public string Rol           { get; set; } = "";   // "VD" o "AC"
    public string BtnColorClase { get; set; } = "primary";
    public string LabelTexto    { get; set; } = "Adjuntar archivos";
}
