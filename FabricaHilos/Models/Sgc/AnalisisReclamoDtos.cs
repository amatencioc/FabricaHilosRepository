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
    public string?   RucCliente      { get; set; }     // RUC del cliente (para mostrar en combo)
    public string?   NomCliente      { get; set; }
    public string    Contacto        { get; set; } = "";
    public string    Telefono        { get; set; } = "";
    public string    Asunto          { get; set; } = "";
    public string?   Descripcion     { get; set; }     // Descripción/detalle del reclamo
    public string    Estado          { get; set; } = "01";
    public string    UsuVendedor     { get; set; } = "";
    public DateTime  FchCreacion     { get; set; }
    public string?   UsuAnalista     { get; set; }
    public DateTime? FchAnalisis     { get; set; }
    public string?   UsuGerente      { get; set; }
    public DateTime? FchAprobacion   { get; set; }
    public string?   MotRechazo      { get; set; }
    public string?   AnalisisCausa   { get; set; }     // Análisis de causa del analista
    public string?   DecisionFinal   { get; set; }     // Decisión final del analista (solo cuando aprobado)
    public DateTime? FchDecision     { get; set; }     // Fecha de la decisión
    public string?   UsuDecision     { get; set; }     // Usuario que registró la decisión
    public DateTime? FchNotiCalidad  { get; set; }     // Última notificación a calidad
    public DateTime? FchNotiVend     { get; set; }     // Última notificación al vendedor
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
    public string RolColorClase => Rol switch { "VD" => "primary", "AC" => "warning", "GE" => "success", _ => "secondary" };

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
            if (mime.Contains("word")   || ext == ".doc"  || ext == ".docx") return "bi-file-word";
            if (mime.Contains("excel")  || ext == ".xls"  || ext == ".xlsx") return "bi-file-excel";
            if (mime.Contains("powerpoint") || ext == ".ppt" || ext == ".pptx") return "bi-file-ppt";
            if (mime == "message/rfc822" || ext == ".eml" || ext == ".msg") return "bi-envelope";
            if (mime.Contains("zip")    || ext == ".zip"  || ext == ".rar" || ext == ".7z") return "bi-file-zip";
            if (mime.StartsWith("text/") || ext == ".txt" || ext == ".csv" || ext == ".rtf") return "bi-file-text";
            return "bi-file-earmark";
        }
    }

    /// <summary>Clase de color Bootstrap para el ícono del archivo (ej. "text-danger").</summary>
    public string IconoColor
    {
        get
        {
            var mime = (MimeType ?? "").ToLower();
            var ext  = System.IO.Path.GetExtension(NombreOrig).ToLower();
            if (mime.StartsWith("image/"))                return "text-info";
            if (mime.StartsWith("video/"))                return "text-danger";
            if (mime.StartsWith("audio/"))                return "text-secondary";
            if (mime == "application/pdf")                return "text-danger";
            if (mime.Contains("word")   || ext == ".doc"  || ext == ".docx") return "text-primary";
            if (mime.Contains("excel")  || ext == ".xls"  || ext == ".xlsx") return "text-success";
            if (mime.Contains("powerpoint") || ext == ".ppt" || ext == ".pptx") return "text-warning";
            if (mime == "message/rfc822" || ext == ".eml" || ext == ".msg") return "text-secondary";
            if (mime.Contains("zip")    || ext == ".zip"  || ext == ".rar" || ext == ".7z") return "text-secondary";
            return "text-secondary";
        }
    }

    /// <summary>True si el archivo puede mostrarse en línea en el navegador.</summary>
    public bool EsVisualizableInline
    {
        get
        {
            var mime = (MimeType ?? "").ToLower();
            var ext  = System.IO.Path.GetExtension(NombreOrig).ToLower();
            return mime.StartsWith("image/")
                || mime.StartsWith("video/")
                || mime.StartsWith("audio/")
                || mime == "application/pdf"
                || mime.StartsWith("text/")
                || ext == ".txt" || ext == ".csv" || ext == ".rtf"
                || ext == ".docx";
        }
    }

    /// <summary>
    /// Tipo de visor a usar en el modal:
    /// "imagen" | "video" | "audio" | "pdf" | "texto" | "ninguno"
    /// </summary>
    public string TipoVisor
    {
        get
        {
            var mime = (MimeType ?? "").ToLower();
            var ext  = System.IO.Path.GetExtension(NombreOrig).ToLower();
            if (mime.StartsWith("image/"))                              return "imagen";
            if (mime.StartsWith("video/"))                              return "video";
            if (mime.StartsWith("audio/"))                              return "audio";
            if (mime == "application/pdf")                              return "pdf";
            if (mime.StartsWith("text/") || ext == ".txt"
                || ext == ".csv" || ext == ".rtf")                     return "texto";
            if (ext == ".docx")                                         return "word";
            return "ninguno";
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
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Debe seleccionar un cliente.")]
    public string              CodCliente { get; set; } = "";
    public string              NomCliente { get; set; } = "";  // se resuelve desde el combo
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "El contacto es obligatorio.")]
    [System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string              Contacto   { get; set; } = "";
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "El teléfono es obligatorio.")]
    [System.ComponentModel.DataAnnotations.MaxLength(30)]
    public string              Telefono   { get; set; } = "";
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "El asunto es obligatorio.")]
    [System.ComponentModel.DataAnnotations.MaxLength(400)]
    public string              Asunto     { get; set; } = "";
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "El descargo del vendedor es obligatorio.")]
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
/// Guardar el Análisis de Causa (Analista de Calidad).
/// Permitido en estados '01'..'04'. No permitido en '05' (Rechazado).
/// </summary>
public class GuardarAnalisisCausaRequest
{
    public long   IdReclamo { get; set; }
    public string Texto     { get; set; } = "";
}

/// <summary>
/// Guardar la Decisión (Analista de Calidad).
/// Sólo permitido cuando el reclamo está APROBADO (ESTADO='04').
/// </summary>
public class GuardarDecisionRequest
{
    public long   IdReclamo { get; set; }
    public string Texto     { get; set; } = "";
}

/// <summary>
/// Notificar a calidad que el vendedor ha enviado un reclamo.
/// </summary>
public class NotificarCalidadRequest
{
    public long IdReclamo { get; set; }
}

/// <summary>
/// Notificar al vendedor que el reclamo ha sido aprobado.
/// </summary>
public class NotificarVendedorAprobadoRequest
{
    public long IdReclamo { get; set; }
}

/// <summary>
/// DTO de cliente para el combo del formulario Nuevo.
/// </summary>
public class ClienteComboDto
{
    public string CodCliente { get; set; } = "";
    public string NomCliente { get; set; } = "";
    public string RucCliente { get; set; } = "";
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
/// DTO para la impresión completa de un reclamo aprobado.
/// Contiene toda la información del reclamo, descargos, archivos y datos de firma.
/// </summary>
public class ReclamoImpresionDto
{
    public ReclamoDto               Reclamo    { get; set; } = new();
    public List<ReclamoDescargoDto> Descargos  { get; set; } = new();
    public List<ReclamoArchivoDto>  Archivos   { get; set; } = new();

    /// <summary>Usuario Oracle que aprobó (para fallback de firma).</summary>
    public string?                  NombreGerenteAprobador { get; set; }

    /// <summary>Nombre completo obtenido de RH_PERSONAS (APELLIDO P. APELLIDO M., NOMBRES).</summary>
    public string?                  NombreCompletoGerente  { get; set; }

    /// <summary>Cargo del firmante obtenido de T_CARGO.</summary>
    public string?                  CargoGerente           { get; set; }

    /// <summary>
    /// Imagen de la firma (LONG RAW de RH_FIRMAS), ya convertida a PNG/JPEG si era TIFF.
    /// Null si no existe firma registrada.
    /// </summary>
    public byte[]?                  FirmaGerente           { get; set; }
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
