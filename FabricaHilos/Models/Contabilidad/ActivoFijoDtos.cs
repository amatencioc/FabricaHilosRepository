namespace FabricaHilos.Models.Contabilidad;

// ── Activo Fijo completo (tabla ACTIVO_FIJO) ──────────────────────────────────
public class ActivoFijoDto
{
    // ── Clave ─────────────────────────────────────────────────────────────────
    public string  Clase   { get; set; } = "";
    public string  Codigo  { get; set; } = "";
    public int     Numero  { get; set; }

    // ── Descripción e identificación ──────────────────────────────────────────
    public string? TipoComp    { get; set; }
    public string? Descripcion { get; set; }
    public string? Modelo      { get; set; }
    public string? Marca       { get; set; }
    public string? Serie       { get; set; }
    public string? Color       { get; set; }

    // ── Fechas ────────────────────────────────────────────────────────────────
    public DateTime? FOpera    { get; set; }   // F_OPERA  — inicio de operaciones
    public DateTime? FBaja     { get; set; }   // F_BAJA
    public DateTime? FAdquisi  { get; set; }   // F_ADQUISI
    public DateTime? FFabrica  { get; set; }   // F_FABRICA
    public DateTime? FIngreso  { get; set; }   // F_INGRESO — fecha recepción
    public DateTime? FInventa  { get; set; }   // F_INVENTA

    // ── Ubicación / responsable ────────────────────────────────────────────────
    public string? CCosto      { get; set; }
    public string? Rescod      { get; set; }
    public string? Ubicacion   { get; set; }
    /// <summary>Nombre del responsable (Jefatura) obtenido via CENTRO_DE_COSTOS → TABLAS_AUXILIARES → CS_USER.</summary>
    public string? NombreResponsable { get; set; }
    /// <summary>Email del responsable obtenido de CS_ANEXO.</summary>
    public string? EmailResponsable  { get; set; }

    // ── Proveedor / comprobante ───────────────────────────────────────────────
    public string? CodProveed  { get; set; }
    public string? SerieCmp    { get; set; }
    public string? OrdenCmp    { get; set; }
    public string? TipoDoc     { get; set; }
    public string? SerieDoc    { get; set; }
    public string? NumDoc      { get; set; }   // N° comprobante de recepción
    public string? CondiTec    { get; set; }   // condición técnica
    public string? FormaAdq    { get; set; }   // forma de adquisición
    public string? MonedaAdq   { get; set; }

    // ── Valores económicos ────────────────────────────────────────────────────
    public decimal? ValorAdqS  { get; set; }   // VALOR_ADQ_S
    public decimal? ValorAdqD  { get; set; }   // VALOR_ADQ_D
    public decimal? TipcamAdq  { get; set; }   // TIPCAM_ADQ
    public decimal? ValorIniS  { get; set; }   // VALOR_INI_S
    public decimal? ValorIdxS  { get; set; }   // VALOR_IDX_S
    public decimal? ValorNetoS { get; set; }   // VALOR_NETO_S
    public decimal? ValResidS  { get; set; }   // VAL_RESID_S
    public decimal? ValResidD  { get; set; }   // VAL_RESID_D
    public decimal? DepreMesS  { get; set; }   // DEPRE_MES_S
    public decimal? DepreAcumS { get; set; }   // DEPRE_ACUM_S
    public decimal? RevalAcumS { get; set; }   // REVAL_ACUM_S
    public decimal? Mejora     { get; set; }   // MEJORA
    public decimal? Potencia   { get; set; }   // POTENCIA

    // ── Depreciación ─────────────────────────────────────────────────────────
    public int?     VidaUtil    { get; set; }  // VIDA_UTIL (años)
    public decimal? TasaDeprec  { get; set; }  // TASA_DEPREC (%)
    public int      MesesDep    { get; set; }  // MESES_DEP (NOT NULL)
    public int?     IndDeprec   { get; set; }  // IND_DEPREC

    // ── Cuentas contables ─────────────────────────────────────────────────────
    public string? Cuenta       { get; set; }
    public string? CuentaRev    { get; set; }
    public string? CuentaDep    { get; set; }
    public string? CuentaRdep   { get; set; }
    public string? CuentaIdx    { get; set; }
    public string? CuentaIdxDep { get; set; }

    // ── Caracterización ───────────────────────────────────────────────────────
    public string? Tangible         { get; set; }
    public string? Situacion        { get; set; }
    public string? TipoCompra       { get; set; }
    public string? Caracter         { get; set; }
    public string? Estado           { get; set; }
    public string? CSestado         { get; set; }
    public string? Arrendado        { get; set; }
    public string? IndMantenimiento { get; set; }

    // ── NIIF ──────────────────────────────────────────────────────────────────
    public int?     NiifVutil        { get; set; }
    public decimal? NiifTasa         { get; set; }
    public decimal? NiifVnetoant     { get; set; }
    public decimal? NiifVtasado      { get; set; }
    public decimal? NiifVrevaluado   { get; set; }
    public decimal? NiifVdeterioro   { get; set; }
    public decimal? NiifVresidual    { get; set; }
    public decimal? NiifVdepreciado  { get; set; }
    public decimal? HistTasa         { get; set; }

    // ── Auditoría ─────────────────────────────────────────────────────────────
    public string?   AAduser  { get; set; }
    public DateTime? AAdfecha { get; set; }
    public string?   AMduser  { get; set; }
    public DateTime? AMdfecha { get; set; }

    // ── Alta / Baja (responsables y firmas) ──────────────────────────────────
    /// <summary>Código del empleado responsable del Alta (→ RH_FIRMAS para firma).</summary>
    public string? UserAlta  { get; set; }
    public string? ObsAlta   { get; set; }
    /// <summary>Código del empleado responsable de la Baja (→ RH_FIRMAS para firma).</summary>
    public string? UserBaja  { get; set; }
    public string? ObsBaja   { get; set; }

    // ── Visado de Alta ────────────────────────────────────────────────────────
    /// <summary>N=Sin visar, P=Pendiente, A=Aprobado, R=Devuelto con observación.</summary>
    public string? VisadoAlta      { get; set; }
    public string? VisadoAltaPor   { get; set; }   // C_CODIGO del aprobador
    public DateTime? VisadoAltaFecha{ get; set; }
    public string? VisadoAltaObs   { get; set; }

    // ── Campos resueltos (joins, NO están en BD) ──────────────────────────────
    public string? ClaseDescripcion     { get; set; }
    public string? ProveedorNombre      { get; set; }
    public string? CCostoDescripcion    { get; set; }

    // ── Helpers de display ────────────────────────────────────────────────────
    public string EstadoTexto => Estado switch
    {
        "0" => "Activo",
        "5" => "En Proceso",
        "6" => "Baja Parcial",
        "7" => "Depreciado",
        "8" => "Val. Neto Cero",
        "9" => "Dado de Baja",
        _   => Estado ?? "—"
    };

    public string EstadoBadge => Estado switch
    {
        "0" => "success",
        "9" => "danger",
        "7" => "warning",
        "8" => "secondary",
        _   => "info"
    };

    /// <summary>
    /// Clave de carpeta para archivos: sanitiza el CODIGO reemplazando caracteres no válidos.
    /// Ej: "07-0281" → "07_0281"
    /// </summary>
    public string CarpetaKey => $"{Clase}_{Codigo.Replace('-', '_').Replace('/', '_')}_{Numero}";
}

// ── Clase de activo fijo (AF_CLASE) ───────────────────────────────────────────
public class AfClaseDto
{
    public string  Codigo       { get; set; } = "";
    public string  Descripcion  { get; set; } = "";
    public int     VUtil        { get; set; }
    public decimal Tasa         { get; set; }
}

// ── Firma de responsable para la Ficha ───────────────────────────────────────
public class FirmaAfDto
{
    public string    Codigo         { get; set; } = "";
    public string    NombreCompleto { get; set; } = "";
    public string    Cargo          { get; set; } = "";
    public string    RolEtiqueta    { get; set; } = "";
    public byte[]?   Firma          { get; set; }
}

// ── Archivo adjunto de alta/baja ──────────────────────────────────────────────
public class ArchivoAfDto
{
    public string   NombreArchivo { get; set; } = "";
    public string   Tipo          { get; set; } = "";   // "alta" | "baja"
    public long     TamanioBytes  { get; set; }
    public DateTime FechaCarga    { get; set; }
}

// ── Model para subida de archivos ─────────────────────────────────────────────
public class ActivoFijoUploadModel
{
    public string  Clase       { get; set; } = "";
    public string  Codigo      { get; set; } = "";
    public int     Numero      { get; set; }
    public string  Tipo        { get; set; } = "alta";   // "alta" | "baja"
    public List<IFormFile>? Archivos { get; set; }
    public string? ReturnToken { get; set; }
    // Observaciones a guardar junto con el upload
    public string?   ObsAlta     { get; set; }
    public string?   ObsBaja     { get; set; }
    /// <summary>Fecha de Inicio de Operaciones (F_OPERA) — sólo aplica al guardar Alta.</summary>
    public DateTime? FOpera       { get; set; }
    /// <summary>Indica que el campo FOpera fue enviado desde el form (true aunque esté vacío, para poder guardar NULL).</summary>
    public bool      FOperaEnviada { get; set; }
    // ── Campos adicionales de BAJA
    /// <summary>Estado de la baja: '0'=ALTA, '6'=B.VENTA, '7'=B.VENTA DEPR., '8'=B.DETERIORO, '9'=B.DESHUSO, '5'=OTROS</summary>
    public string? EstadoBaja  { get; set; }
    /// <summary>Fecha de baja del activo (F_BAJA).</summary>
    public DateTime? FBaja      { get; set; }
    /// <summary>Indica que el campo FBaja fue enviado desde el form (para poder guardar NULL).</summary>
    public bool      FBajaEnviada { get; set; }
    /// <summary>Estado SUNAT: '1'=Activos en Desuso, '2'=Activos Obsoletos, '9'=Resto de Activos</summary>
    public string? CSestado    { get; set; }
}

// -- Memorando de baja de Activo Fijo ---------------------------------------------------------

/// <summary>Formulario de entrada para generar el memorando.</summary>
public class MemorandoFormModel
{
    /// <summary>Claves "Clase|Codigo|Numero" separadas por coma (viene del Index via checkboxes).</summary>
    public string  Seleccion     { get; set; } = "";

    // Encabezado del memo
    public string  Ciudad        { get; set; } = "Callao";
    public string  NumMemo       { get; set; } = "";
    public int     Anio          { get; set; } = DateTime.Now.Year;
    public string  Area          { get; set; } = "";

    // Campos DE / A / REF
    public string  De            { get; set; } = "";
    public string  Para          { get; set; } = "";
    public string  CargoDestino  { get; set; } = "";
    public string  Referencia    { get; set; } = "";

    // Cuerpo
    public string  CuerpoTexto   { get; set; } = "";
    public string? MotivoEntre   { get; set; }
}

/// <summary>Un item de la tabla del memorando.</summary>
public class MemorandoItemDto
{
    public string    Codigo      { get; set; } = "";
    public string?   Descripcion { get; set; }
    public DateTime? FIngreso    { get; set; }
    public int       AniosAnt    { get; set; }
    public decimal   PrecioRef   { get; set; }
}

// ── Visado de Alta ────────────────────────────────────────────────────────────

/// <summary>Datos necesarios para enviar el email de visado al responsable del área.</summary>
public class VisadoAltaEmailData
{
    public required string CorreoAprobador  { get; set; }
    public required string NombreAprobador  { get; set; }
    public required string UrlAprobar       { get; set; }
    public required string UrlObservar      { get; set; }
    public required string UrlFicha         { get; set; }
    // Datos del activo para el payload del email
    public required string CodigoActivo     { get; set; }
    public required string ClaseActivo      { get; set; }
    public required string Descripcion      { get; set; }
    public required string CCosto           { get; set; }
    public required string NombreCC         { get; set; }
    public required string ValorAdquisicion { get; set; }
    public required string FechaRecepcion   { get; set; }
    public required string NombreRegistrador{ get; set; }
    public required string FechaRegistro    { get; set; }
    public string?  ObsAlta                 { get; set; }
    public string?  FechaOperacion          { get; set; }
    public required string FechaExpira      { get; set; }
}

/// <summary>Resultado del procesamiento de un token de visado.</summary>
public class VisadoResultado
{
    public bool    Ok           { get; set; }
    public string? Error        { get; set; }
    // Datos del activo para mostrar en la página de confirmación
    public string? CodigoActivo { get; set; }
    public string? Descripcion  { get; set; }
    public string? Accion       { get; set; }  // "APROBADO" | "OBSERVADO"
    public string? UrlFicha     { get; set; }
    // Datos adicionales para el correo de confirmacion de visado (a Llanet)
    public string?   CCosto          { get; set; }
    public string?   NombreCC        { get; set; }
    public string?   NombreAprobador { get; set; }
    public DateTime? FechaVisado     { get; set; }
}

/// <summary>Estado del visado leído desde la BD para mostrar en Editar.cshtml.</summary>
public class VisadoAltaEstado
{
    public string    Estado          { get; set; } = "N";  // N/P/A/R
    public string?   NombreAprobador { get; set; }
    public DateTime? FechaVisado     { get; set; }
    public string?   Observacion     { get; set; }

    public string EstadoTexto => Estado switch
    {
        "P" => "Pendiente de visado",
        "A" => "Aprobado",
        "R" => "Devuelto con observación",
        _   => "Sin visar"
    };
    public string EstadoCss => Estado switch
    {
        "P" => "warning",
        "A" => "success",
        "R" => "danger",
        _   => "secondary"
    };
}

/// <summary>DTO completo para renderizar la vista de impresion del memorando.</summary>
public class MemorandoDto
{
    // Encabezado
    public string   NumeroMemo   { get; set; } = "";
    public string   Ciudad       { get; set; } = "Callao";
    public DateTime Fecha        { get; set; } = DateTime.Now;

    // Campos memo
    public string   De           { get; set; } = "";
    public string   Para         { get; set; } = "";
    public string   CargoDestino { get; set; } = "";
    public string   Referencia   { get; set; } = "";
    public string   CuerpoTexto  { get; set; } = "";
    public string?  MotivoEntre  { get; set; }

    // Tabla de activos
    public List<MemorandoItemDto> Items { get; set; } = new();
    public decimal Total => Items.Sum(i => i.PrecioRef);

    // Firma del emisor (usuario activo)
    public FirmaAfDto? FirmaEmisor { get; set; }

    // Datos de la empresa para encabezado corporativo
    public string EmpresaNombre    { get; set; } = "";
    public string EmpresaRuc       { get; set; } = "";
    public string EmpresaDireccion { get; set; } = "";
    public string EmpresaTelefono  { get; set; } = "";
    public string EmpresaLogoPath  { get; set; } = "";
}
