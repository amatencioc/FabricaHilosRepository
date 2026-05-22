namespace FabricaHilos.Models.Produccion.Planeamiento;

/// <summary>
/// Detalle completo de la producción en Tintorería para un sublote/partida.
/// Agrupa todos los datos visibles en el formulario "Análisis de Partida de
/// Tintorería" del sistema Logix:
///   · Cálculo de recetas planificadas (ING_RECETAS_G vía PARTIDA_MAS)
///   · Registro de producción TT — baños ejecutados (TT_RPRODUC)
///   · Registro de secado (TT_RSECADO)
///   · Control de Calidad TT (CTCALIDAD_D)
///   · Validación de receta de Laboratorio (L_VALIDA_RECETA)
/// </summary>
public class PlnDetalleTt
{
    /// <summary>Cálculo de recetas planificadas (ING_RECETAS_G vía PARTIDA_MAS).</summary>
    public IList<PlnCalculoReceta>    CalculoRecetas    { get; set; } = [];

    /// <summary>Baños de TT ejecutados (TT_RPRODUC TIPODOC='IR'). Uno por proceso: BQM, TEAC…</summary>
    public IList<PlnBanoTt>           Banos             { get; set; } = [];

    /// <summary>Todos los registros de secado (TT_RSECADO) — ordenados por FECHA_INI ASC.</summary>
    public IList<PlnSecadoTt>         Secados           { get; set; } = [];

    /// <summary>Último registro de secado (el más reciente, por FECHA_INI DESC).</summary>
    public PlnSecadoTt?               Secado            => Secados.Count > 0 ? Secados[^1] : null;

    /// <summary>Control de calidad de tintorería (CTCALIDAD_D).</summary>
    public PlnCalidadTt?              Calidad           { get; set; }

    /// <summary>Validación de receta por Laboratorio (L_VALIDA_RECETA vía NROPROG).</summary>
    public PlnValidacionReceta?       ValidacionReceta  { get; set; }

    // ── Secciones post-TT ────────────────────────────────────────────────────

    /// <summary>Programas de conera (H_PROGRAMACION). Ordenados por FECHA ASC.</summary>
    public IList<PlnProgramaConera>   ProgramasConera   { get; set; } = [];

    /// <summary>Registros de devanado en máquina REDINA (H_RPRODUC TP_MAQ='R'). ASC por FECHA_INI.</summary>
    public IList<PlnDevanado>         Devanados         { get; set; } = [];

    /// <summary>Registros de revisado de producto acabado (REVISADO_G+D). ASC por número.</summary>
    public IList<PlnRevisado>         Revisados         { get; set; } = [];

    /// <summary>Pesajes de ingreso a almacén PT (LOTES TP='16' + KARDEX_D). ASC por fecha.</summary>
    public IList<PlnPesajeAlmacen>    PesajesAlmacen    { get; set; } = [];

    /// <summary>Guías de despacho de PT (LOTES TP='21'/'23' + KARDEX_G). ASC por fecha.</summary>
    public IList<PlnDespachoProducto> Despachos         { get; set; } = [];

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>True si hay baños de TT registrados.</summary>
    public bool TieneBanos          => Banos.Count > 0;

    /// <summary>True si hay cálculo de recetas planificadas.</summary>
    public bool TieneCalculoRecetas => CalculoRecetas.Count > 0;

    /// <summary>Primer inicio de TT (menor FECHA_INI de todos los baños).</summary>
    public DateTime? InicioTt  => Banos.Count > 0 ? Banos.Min(b => b.FechaIni) : null;

    /// <summary>Último fin de TT (mayor FECHA_FIN de los baños completados).</summary>
    public DateTime? FinTt     => Banos.Any(b => b.FechaFin.HasValue)
                                    ? Banos.Where(b => b.FechaFin.HasValue).Max(b => b.FechaFin)
                                    : null;

    /// <summary>Fecha de creación de la PARTIDA (PARTIDA.FECHA). Corresponde al inicio real de PASO '03' En Hilandería.</summary>
    public DateTime? FechaPartida { get; set; }

    /// <summary>Máquina real donde se ejecutó la TT (del primer baño registrado).</summary>
    public string?   MaquinaRealTt  => Banos.FirstOrDefault(b => !string.IsNullOrEmpty(b.CodMaq))?.CodMaq;

    /// <summary>True cuando al menos un baño se ejecutó en una máquina diferente a la planificada.</summary>
    public bool HayCambioMaquina => Banos.Any(b => b.HayCambioMaquina);
}

// ─────────────────────────────────────────────────────────────────────────────
// Cálculo de Recetas planificadas (ING_RECETAS_G vía PARTIDA_MAS)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Una receta planificada para la partida (ING_RECETAS_G vía PARTIDA_MAS).
/// Representa un proceso/baño que fue CALCULADO y planificado (no necesariamente ejecutado).
/// </summary>
public class PlnCalculoReceta
{
    /// <summary>Número de guia (ING_RECETAS_G.NUMERO = PARTIDA_MAS.NUMERO).</summary>
    public long     Guia            { get; set; }

    /// <summary>Código de proceso (ej: TEAC, BQM).</summary>
    public string   Proceso         { get; set; } = "";

    /// <summary>Descripción legible del proceso (ej: "Teñido y Acabado").</summary>
    public string   DescProceso     { get; set; } = "";

    /// <summary>Código de máquina planificada (ej: R10).</summary>
    public string   CodMaqPlanif    { get; set; } = "";

    /// <summary>Nombre completo de la máquina planificada (ej: "THIES 4x4-1").</summary>
    public string   NombreMaqPlanif { get; set; } = "";

    /// <summary>Peso neto de la receta en kg.</summary>
    public decimal? PesoNeto        { get; set; }

    /// <summary>Estado numérico de la receta (ING_RECETAS_G.ESTADO).</summary>
    public int      EstadoReceta    { get; set; }

    /// <summary>Descripción del estado de la receta.</summary>
    public string   DescEstReceta   { get; set; } = "";
}

// ─────────────────────────────────────────────────────────────────────────────
// Baños de TT ejecutados (TT_RPRODUC)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Un baño de tintorería ejecutado (fila en TT_RPRODUC con TIPODOC='IR').
/// Corresponde a una fila del "Registro Producción Tintorería" del formulario Logix.
/// </summary>
public class PlnBanoTt
{
    /// <summary>Guia / número de receta (TT_RPRODUC.RECETA = ING_RECETAS_G.NUMERO).</summary>
    public long      Guia             { get; set; }

    /// <summary>Código de proceso ejecutado (ej: BQM, TEAC).</summary>
    public string    Proceso          { get; set; } = "";

    /// <summary>Descripción del proceso ejecutado.</summary>
    public string    DescProceso      { get; set; } = "";

    /// <summary>Código de máquina real donde se ejecutó el baño (ej: R04).</summary>
    public string    CodMaq           { get; set; } = "";

    /// <summary>Nombre completo de la máquina real (ej: "THIES 4").</summary>
    public string    NombreMaq        { get; set; } = "";

    /// <summary>Fecha y hora de inicio del baño.</summary>
    public DateTime  FechaIni         { get; set; }

    /// <summary>Fecha y hora de fin del baño (null si aún en proceso).</summary>
    public DateTime? FechaFin         { get; set; }

    /// <summary>Duración calculada en horas (FECHA_FIN - FECHA_INI).</summary>
    public decimal?  Horas            { get; set; }

    /// <summary>Calificación del baño (AP=Aprobado, RE=Rechazado, etc.).</summary>
    public string?   Calificacion     { get; set; }

    /// <summary>Descripción legible de la calificación.</summary>
    public string    DescCalif        { get; set; } = "";

    /// <summary>Estado del baño en TT_RPRODUC (3=Completado).</summary>
    public string    Estado           { get; set; } = "3";

    /// <summary>Código de máquina planificada según ING_RECETAS_G (puede diferir de la real).</summary>
    public string    CodMaqPlanif     { get; set; } = "";

    /// <summary>Nombre de la máquina planificada.</summary>
    public string    NombreMaqPlanif  { get; set; } = "";

    /// <summary>
    /// True cuando la máquina real difiere de la máquina planificada.
    /// Indica un cambio de máquina durante la ejecución TT.
    /// </summary>
    public bool HayCambioMaquina =>
        !string.IsNullOrEmpty(CodMaqPlanif) &&
        !string.IsNullOrEmpty(CodMaq) &&
        !string.Equals(CodMaq, CodMaqPlanif, StringComparison.OrdinalIgnoreCase);

    /// <summary>Duración formateada (ej: "12h 00min").</summary>
    public string HorasFormateado =>
        Horas.HasValue ? $"{(int)Horas.Value}h {(int)((Horas.Value - (int)Horas.Value) * 60):D2}min" : "—";
}

// ─────────────────────────────────────────────────────────────────────────────
// Secado (TT_RSECADO)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Registro de secado post-tintorería (TT_RSECADO).
/// La guia = PARTIDA.NUMERO (no es la guia de receta).
/// </summary>
public class PlnSecadoTt
{
    /// <summary>PARTIDA.NUMERO (clave de navegación TT_RSECADO.GUIA = PARTIDA.NUMERO).</summary>
    public long      GuiaPartida  { get; set; }

    /// <summary>Código de máquina de secado (ej: S01).</summary>
    public string    CodMaq       { get; set; } = "";

    /// <summary>Nombre completo de la secadora (ej: "SECADORA THIES").</summary>
    public string    NombreMaq    { get; set; } = "";

    /// <summary>Fecha/hora de inicio de secado.</summary>
    public DateTime  FechaIni     { get; set; }

    /// <summary>Fecha/hora de fin de secado.</summary>
    public DateTime? FechaFin     { get; set; }

    /// <summary>Peso neto después del secado (kg).</summary>
    public decimal?  PesoNeto     { get; set; }

    /// <summary>Minutos de secado.</summary>
    public decimal?  MinSecado    { get; set; }

    /// <summary>Minutos de re-secado (0 si no aplica).</summary>
    public decimal?  MinResecado  { get; set; }

    /// <summary>Estado numérico del secado.</summary>
    public string    Estado       { get; set; } = "";

    /// <summary>Descripción del estado del secado.</summary>
    public string    DescEstado   { get; set; } = "";

    /// <summary>Duración total calculada (FechaFin - FechaIni).</summary>
    public double? DuracionHoras =>
        FechaFin.HasValue ? (FechaFin.Value - FechaIni).TotalHours : null;
}

// ─────────────────────────────────────────────────────────────────────────────
// Control de Calidad TT (CTCALIDAD_D)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Control de Calidad de Tintorería (CTCALIDAD_D).
/// Guia = PARTIDA.NUMERO.
/// </summary>
public class PlnCalidadTt
{
    public long      Numero         { get; set; }

    /// <summary>PARTIDA.NUMERO (clave de navegación CTCALIDAD_D.GUIA = PARTIDA.NUMERO).</summary>
    public long      GuiaPartida    { get; set; }

    public DateTime  Fecha          { get; set; }

    /// <summary>Estado de evaluación (código: 31=Pendiente, 32=Evaluado, 33=Observado).</summary>
    public string    EstEvaluacion  { get; set; } = "";

    /// <summary>Descripción del estado de evaluación.</summary>
    public string    DescEstEval    { get; set; } = "";

    /// <summary>Resultado (01=Aprobado, 02=Rechazado, 03=Aprobado con obs.).</summary>
    public string    Resultado      { get; set; } = "";

    /// <summary>Descripción del resultado.</summary>
    public string    DescResultado  { get; set; } = "";

    /// <summary>Merma inicial (kg perdidos al entrar a CC).</summary>
    public decimal?  MermaInicio    { get; set; }

    /// <summary>Merma final (kg perdidos al salir de CC).</summary>
    public decimal?  MermaFin       { get; set; }

    /// <summary>Observaciones del analista de calidad.</summary>
    public string?   Observacion    { get; set; }

    /// <summary>Evaluación de tono (campo TONO de CTCALIDAD_D).</summary>
    public string?   Tono           { get; set; }

    /// <summary>Solidez al frote (campo SF).</summary>
    public string?   Solidez        { get; set; }

    /// <summary>Igualdad (campo SI).</summary>
    public string?   Igualdad       { get; set; }

    /// <summary>Defecto detectado (código).</summary>
    public string?   Defecto        { get; set; }

    /// <summary>Color Bootstrap para el badge de resultado.</summary>
    public string ResultadoBadge => Resultado switch
    {
        "01" => "success",
        "02" => "danger",
        "03" => "warning",
        _    => "secondary"
    };
}

// ─────────────────────────────────────────────────────────────────────────────
// Validación de Receta de Laboratorio (L_VALIDA_RECETA)
// ─────────────────────────────────────────────────────────────────────────────

// ─────────────────────────────────────────────────────────────────────────────
// Programa Conera (H_PROGRAMACION)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Entrada de programa de conera (H_PROGRAMACION).
/// GUIA = PARTIDA.NUMERO.  Puede haber varios si hubo reanudaciones.
/// </summary>
public class PlnProgramaConera
{
    public long      Numero     { get; set; }
    public DateTime  FechaIni   { get; set; }    // H_PROGRAMACION.FECHA
    public DateTime? FechaFin   { get; set; }    // H_PROGRAMACION.FECHA_FIN
    public string    CodMaq     { get; set; } = "";
    public string    NombreMaq  { get; set; } = "";
    public string    Estado     { get; set; } = "";
    public string    DescEstado { get; set; } = "";
}

// ─────────────────────────────────────────────────────────────────────────────
// Registro de Devanado (H_RPRODUC TP_MAQ = 'R')
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Registro de producción en máquina devanadora/conera (H_RPRODUC donde TP_MAQ='R').
/// GUIA = PARTIDA.NUMERO.  Puede haber varios turnos.
/// </summary>
public class PlnDevanado
{
    public string    CodMaq    { get; set; } = "";
    public string    NombreMaq { get; set; } = "";
    public DateTime  FechaIni  { get; set; }
    public DateTime? FechaFin  { get; set; }
    public decimal?  Unidades  { get; set; }    // conos producidos
    public decimal?  PesoNeto  { get; set; }
    public string    Estado    { get; set; } = "";

    public double? DuracionHoras =>
        FechaFin.HasValue ? (FechaFin.Value - FechaIni).TotalHours : null;
}

// ─────────────────────────────────────────────────────────────────────────────
// Revisado de Productos Acabados (REVISADO_G + REVISADO_D)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Cabecera de revisado (REVISADO_G) con sus líneas de detalle (REVISADO_D).
/// Un REVISADO_G puede tener varias líneas (varios ítems / turnos / revisadores).
/// </summary>
public class PlnRevisado
{
    public long                  Numero       { get; set; }
    public long                  Guia         { get; set; }
    public string                MaqProced    { get; set; } = "";
    public DateTime              FchRegistro  { get; set; }   // A_ADFECHA
    public DateTime?             FchFinRevisa { get; set; }
    public IList<PlnRevisadoDet> Detalle      { get; set; } = [];
}

/// <summary>Línea de detalle de revisado (REVISADO_D).</summary>
public class PlnRevisadoDet
{
    public int       Item        { get; set; }
    public DateTime  Fecha       { get; set; }
    public string    Revisador   { get; set; } = "";    // C_CODIGO
    public string    Turno       { get; set; } = "";
    public decimal   Aprobado    { get; set; }
    public decimal   Rechazado   { get; set; }
    public decimal   Faltante    { get; set; }
    public decimal   Merma       { get; set; }
    public string?   Observacion { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Pesaje Ingreso Almacén PT (LOTES TP_TRANSAC='16' → KARDEX_D)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Registro de pesaje de ingreso a almacén de producto terminado.
/// Un registro por guía de kardex (KARDEX_G); puede haber más de uno si
/// el ingreso se realizó en varias guías.
/// </summary>
public class PlnPesajeAlmacen
{
    public string    CodAlm      { get; set; } = "";
    public string    TpTransac   { get; set; } = "";
    public int       Serie       { get; set; }
    public long      Numero      { get; set; }
    public DateTime  Fecha       { get; set; }
    public int       LotesEtiq   { get; set; }   // COUNT de LOTES etiquetados
    public decimal   PesoPesado  { get; set; }   // KARDEX_D.CANTIDAD (kg neto)
}

// ─────────────────────────────────────────────────────────────────────────────
// Despachos de Producto Terminado (LOTES TP_TRANSAC='21'/'23' → KARDEX_G)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Guía de despacho de producto terminado (KARDEX_G TP_TRANSAC='21'|'23').
/// Puede no existir si el ítem aún no fue despachado.
/// </summary>
public class PlnDespachoProducto
{
    public string    CodAlm    { get; set; } = "";
    public string    TpTransac { get; set; } = "";
    public int       Serie     { get; set; }
    public long      Numero    { get; set; }
    public DateTime  Fecha     { get; set; }
    public int       Lotes     { get; set; }
    public decimal   Unidades  { get; set; }
    public decimal   PesoKg    { get; set; }
    public string?   Cliente   { get; set; }
    public string?   Glosa     { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Validación de Receta de Laboratorio (L_VALIDA_RECETA)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Validación de receta de Laboratorio (L_VALIDA_RECETA vía NROPROG de PARTIDA).
/// Indica si la receta fue validada por el laboratorista antes de iniciar TT.
/// </summary>
public class PlnValidacionReceta
{
    public long      Numero        { get; set; }

    /// <summary>NROPROG de la partida (vínculo L_VALIDA_RECETA.NROPROG = PARTIDA.NROPROG).</summary>
    public long      Nroprog       { get; set; }

    /// <summary>Tipo: 1=Normal, 2=Reproceso.</summary>
    public int       Tipo          { get; set; }

    /// <summary>Descripción del tipo.</summary>
    public string    DescTipo      { get; set; } = "";

    /// <summary>Código del laboratorista responsable (C_LABORATORISTA).</summary>
    public string    Laboratorista { get; set; } = "";

    /// <summary>Estado numérico: 1=En proceso, 2=Observado, 3=Validado, 4=Rechazado.</summary>
    public int       Estado        { get; set; }

    /// <summary>Descripción del estado.</summary>
    public string    DescEstado    { get; set; } = "";

    /// <summary>Fecha de registro de la solicitud de validación.</summary>
    public DateTime  FchRegistro   { get; set; }

    /// <summary>Fecha de validación efectiva (null si aún pendiente).</summary>
    public DateTime? FchValidacion { get; set; }

    /// <summary>Color Bootstrap para el badge de estado.</summary>
    public string EstadoBadge => Estado switch
    {
        3 => "success",
        4 => "danger",
        2 => "warning",
        _ => "secondary"
    };

    /// <summary>Días transcurridos entre registro y validación (null si aún no validado).</summary>
    public int? DiasValidacion =>
        FchValidacion.HasValue ? (int)(FchValidacion.Value - FchRegistro).TotalDays : null;
}
