namespace FabricaHilos.Models.Produccion.Planeamiento;

// ── SP_PLN_PEND_REVISADO ────────────────────────────────────────────────────
/// <summary>Partida pendiente de revisado (Martín). Columnas de SP_PLN_PEND_REVISADO.</summary>
public class PlnPendienteRevisado
{
    public string    Partida      { get; set; } = "";   // PARTIDA_07
    public string    Material     { get; set; } = "";   // MATERIAL_07
    public string    ColorDet     { get; set; } = "";   // COLOR_DET_07 (ITEMPED.COLOR_DET)
    public decimal   CantidadPedido { get; set; }       // CANTIDAD_PEDIDO_07 (ITEMPED.CANTIDAD)
    public DateTime? FechaFin     { get; set; }         // FECHA_FIN_07
    public string    Cliente      { get; set; } = "";   // DESC_CLIENTE_07
    public string    CodCliente   { get; set; } = "";   // COD_CLIENTE_07
    public string    CodVende     { get; set; } = "";   // COD_VENDE_07
    public string    Maquina      { get; set; } = "";   // DESC_MAQ_07
    public decimal   NroRmc       { get; set; }         // NRO_RMC_07
    public decimal   Peso         { get; set; }         // PESO_PARTIDA_07
    public string    Lote         { get; set; } = "";   // LOTE_07
    public string    ColoSer      { get; set; } = "";   // COLO_SER_07
    public DateTime? FchEntrega   { get; set; }         // FCH_ENTREGA_07
    public string    Tipo         { get; set; } = "";   // TIPO_07 (G=Tintorería, H=Hilandería)
    public string    DescAsesor   { get; set; } = "";   // DESC_ASESOR_07
    public decimal   Guia         { get; set; }         // GUIA_07
    public int       Prioridad    { get; set; } = 99;   // PRIORIDAD_07 (99 = sin asignar)

    public int DiasRetraso => FchEntrega.HasValue
        ? (int)(DateTime.Today - FchEntrega.Value.Date).TotalDays
        : 0;
    public bool EstaVencido => DiasRetraso > 0;
}

// ── SP_PLN_PEND_EVAL_CALIDAD ────────────────────────────────────────────────
/// <summary>Partida pendiente de evaluación de calidad tintorería (Ivon).</summary>
public class PlnPendienteEvalCalidad
{
    public string    Partida      { get; set; } = "";   // PARTIDA_03
    public string    Material     { get; set; } = "";   // MATERIAL_03
    public string    ColorTecnico { get; set; } = "";   // COLOR_TECNICO_03
    public string    Cliente      { get; set; } = "";   // DESC_CLIENTE_03
    public string    CodCliente   { get; set; } = "";   // COD_CLIENTE_03
    public string    CodVende     { get; set; } = "";   // COD_VENDE_03
    public DateTime? FechaFin     { get; set; }         // FECHA_FIN_03
    public string    CodMaq       { get; set; } = "";   // COD_MAQ_03
    public string    Maquina      { get; set; } = "";   // DESC_MAQ_03
    public decimal   NroRmc       { get; set; }         // NRO_RMC_03
    public decimal   Peso         { get; set; }         // PESO_PARTIDA_03
    public string    Lote         { get; set; } = "";   // LOTE_03
    public string    ColoSer      { get; set; } = "";   // COLO_SER_03
    public DateTime? FchEntrega   { get; set; }         // FCH_ENTREGA_03

    public int DiasRetraso => FchEntrega.HasValue
        ? (int)(DateTime.Today - FchEntrega.Value.Date).TotalDays
        : 0;
    public bool EstaVencido => DiasRetraso > 0;
}

// ── SP_PLN_PEND_ENCONADO ────────────────────────────────────────────────────
/// <summary>
/// Partida aprobada pendiente de enconado/devanado (Guevara). Réplica literal de
/// PLNM/PEND_ENCONADO/LISTADO.sql: solo universo tintorería, sin p_tipo/p_asesor.
/// </summary>
public class PlnPendienteEnconado
{
    public string    Partida       { get; set; } = "";  // PARTIDA
    public string    Material      { get; set; } = "";  // SOLO_MATERIAL
    public string    ColorTecnico  { get; set; } = "";  // COLOR_TECNICO
    public string    ColorCli      { get; set; } = "";  // COLOR_CLI
    public string    Cliente       { get; set; } = "";  // DESC_CLIENTE
    public decimal   Peso          { get; set; }        // NETO_GUIA
    public string    CodMaq        { get; set; } = "";  // COD_MAQ
    public DateTime? FecTenido     { get; set; }        // FEC_TENIDO
    public DateTime? FecAprob     { get; set; }        // FEC_APROB
    public DateTime? FchEntrega    { get; set; }        // FCH_ENTREGA
    public string    Lote          { get; set; } = "";  // LOTE
    public string    Rmc           { get; set; } = "";  // RMC
    public decimal   NroRmc        { get; set; }        // NRO_RMC
    public decimal   Guia          { get; set; }        // GUIA
    public string    DescEstEvaluacion { get; set; } = ""; // DESC_EST_EVALUACION
    public string    ProdMoulinex  { get; set; } = "";  // PROD_MOULINEX
    public string    ProdMercerizado { get; set; } = ""; // PROD_MERCERIZADO
    public decimal   NumPed        { get; set; }        // NUM_PED
    public decimal   ItemPed       { get; set; }        // ITEM_PED
    public decimal   NroPart       { get; set; }        // NROPART

    public int DiasRetraso => FchEntrega.HasValue
        ? (int)(DateTime.Today - FchEntrega.Value.Date).TotalDays
        : 0;
    public bool EstaVencido => DiasRetraso > 0;
}

// ── SP_PLN_PEND_ENCONADO_CUADRO1 ────────────────────────────────────────────
/// <summary>Cuadro 1 (izquierda) de "Pendientes de Enconado": resumen por categoría de material.</summary>
public class PlnEnconadoCuadro1
{
    public string  Orden    { get; set; } = "";  // ORDEN_02
    public string  Texto    { get; set; } = "";  // TEXTO_02
    public decimal PesoKg   { get; set; }        // PESO_KG_02
    public decimal Cantidad { get; set; }        // CANT_02
}

// ── SP_PLN_PEND_ENCONADO_CUADRO2 ────────────────────────────────────────────
/// <summary>Cuadro 2 (derecha) de "Pendientes de Enconado": desglose de "MATERIAL POR APROBAR" por estatus de evaluación.</summary>
public class PlnEnconadoCuadro2
{
    public string  Estatus  { get; set; } = "";  // ESTATUS_03
    public decimal Cantidad { get; set; }        // PARTIDA_03
    public decimal Kg       { get; set; }        // KG_03
}

// ── SP_PLN_PEND_TENIDO ──────────────────────────────────────────────────────
/// <summary>Partida pendiente de teñido (Fredy/Malena). ORIGEN: PROGRAMADO | CON_PREVIO.</summary>
public class PlnPendienteTenido
{
    public string    Partida       { get; set; } = "";  // PARTIDA
    public string    Material      { get; set; } = "";  // MATERIAL
    public string    Cliente       { get; set; } = "";  // DESC_CLIENTE
    public string    CodCliente    { get; set; } = "";  // COD_CLIENTE
    public string    CodVende      { get; set; } = "";  // COD_VENDE
    public DateTime? FechaProg     { get; set; }        // FECHA_PROG (nullable en PROGRAMADO)
    public string    CodMaq        { get; set; } = "";  // COD_MAQ
    public string    Maquina       { get; set; } = "";  // DESC_MAQ
    public string    Proceso       { get; set; } = "";  // PROCESO
    public string    Rmc           { get; set; } = "";  // RMC
    public decimal   NroRmc        { get; set; }        // NRO_RMC
    public decimal   Peso          { get; set; }        // PESO
    public string    Lote          { get; set; } = "";  // LOTE
    public string    ColoSer       { get; set; } = "";  // COLO_SER
    public DateTime? FchEntrega    { get; set; }        // FCH_ENTREGA
    public string    Origen        { get; set; } = "";  // 'PROGRAMADO' | 'CON_PREVIO'
    public string    EstReceta      { get; set; } = "";  // 'Con receta' | 'Sin receta'
    public string    AlmIntermedio { get; set; } = "";  // 'CON DESPACHO' | 'SIN DESPACHO'

    public int DiasRetraso => FchEntrega.HasValue
        ? (int)(DateTime.Today - FchEntrega.Value.Date).TotalDays
        : 0;
    public bool EstaVencido => DiasRetraso > 0;
}

// ── SP_PLN_PEND_SECADO ──────────────────────────────────────────────────────
/// <summary>Partida terminada en tintorería pendiente de secado (Freddy/Malena).</summary>
public class PlnPendienteSecado
{
    public string    Partida     { get; set; } = "";  // PARTIDA_01
    public string    Material    { get; set; } = "";  // MATERIAL_01
    public string    ColorTecnico { get; set; } = ""; // COLOR_TECNICO_01
    public string    Cliente     { get; set; } = "";  // DESC_CLIENTE_01
    public string    CodCliente  { get; set; } = "";  // COD_CLIENTE_01
    public string    CodVende    { get; set; } = "";  // COD_VENDE_01
    public DateTime? Fecha       { get; set; }        // FECHA_01 (FECHA_FIN tintorería)
    public string    CodMaq      { get; set; } = "";  // COD_MAQ_01
    public string    Maquina     { get; set; } = "";  // DESC_MAQ_01
    public string    Proceso     { get; set; } = "";  // PROCESO_01
    public decimal   NroRmc      { get; set; }        // NRO_RMC_01
    public string    Rmc         { get; set; } = "";  // RMC
    public decimal   Peso        { get; set; }        // PESO_PARTIDA_01
    public string    Lote        { get; set; } = "";  // LOTE_01
    public string    ColoSer     { get; set; } = "";  // COLO_SER_01
    public DateTime? FchEntrega  { get; set; }        // FCH_ENTREGA_01

    public int DiasRetraso => FchEntrega.HasValue
        ? (int)(DateTime.Today - FchEntrega.Value.Date).TotalDays
        : 0;
    public bool EstaVencido => DiasRetraso > 0;
}

// ── SP_PLN_EN_SECADO ─────────────────────────────────────────────────────────
/// <summary>Partida actualmente en proceso de secado (V_RSECADO.ESTADO='1'). Pestaña "En Secado" de PendientesSecado.</summary>
public class PlnEnSecado
{
    public string    Partida      { get; set; } = "";  // PARTIDA_02
    public string    Material     { get; set; } = "";  // MATERIAL_02
    public string    ColorTecnico { get; set; } = "";  // COLOR_TECNICO_02
    public string    Cliente      { get; set; } = "";  // DESC_CLIENTE_02
    public string    CodCliente   { get; set; } = "";  // COD_CLIENTE_02
    public string    CodVende     { get; set; } = "";  // COD_VENDE_02
    public DateTime? FechaIni     { get; set; }         // FECHA_INI_02
    public string    CodMaq       { get; set; } = "";  // COD_MAQ_02
    public string    Maquina      { get; set; } = "";  // DESC_MAQ_02
    public decimal   NroRmc       { get; set; }         // NRO_RMC_02
    public string    Rmc          { get; set; } = "";  // RMC
    public decimal   Peso         { get; set; }         // PESO_PARTIDA_02
    public string    Lote         { get; set; } = "";  // LOTE_02
    public string    ColoSer      { get; set; } = "";  // COLO_SER_02
    public DateTime? FchEntrega   { get; set; }         // FCH_ENTREGA_02

    public int DiasRetraso => FchEntrega.HasValue
        ? (int)(DateTime.Today - FchEntrega.Value.Date).TotalDays
        : 0;
    public bool EstaVencido => DiasRetraso > 0;
}

// ── SP_PLN_PEND_MADEJA ───────────────────────────────────────────────────────────
/// <summary>Partida programada pendiente de acabado de madeja.</summary>
public class PlnPendienteMadeja
{
    public string    Partida    { get; set; } = "";  // PARTIDA_000
    public string    Material   { get; set; } = "";  // MATERIAL_000
    public string    ColorTecnico { get; set; } = ""; // COLOR_TECNICO_000
    public string    Cliente    { get; set; } = "";  // DESC_CLIENTE_000
    public string    CodCliente { get; set; } = "";  // COD_CLIENTE_000
    public string    CodVende   { get; set; } = "";  // COD_VENDE_000
    public DateTime? FchProg    { get; set; }        // FCH_PROG_000
    public string    CodMaq     { get; set; } = "";  // COD_MAQ_000
    public string    Maquina    { get; set; } = "";  // DESC_MAQUINA_000
    public string    Rmc        { get; set; } = "";  // RMC_000
    public decimal   NroRmc     { get; set; }        // NRO_RMC_000
    public decimal   Peso       { get; set; }        // NETO_GUIA_000
    public string    Lote       { get; set; } = "";  // LOTE_000
    public string    ColoSer    { get; set; } = "";  // COLO_SER_000
    public DateTime? FchEntrega { get; set; }        // FCH_ENTREGA_000

    public int DiasRetraso => FchEntrega.HasValue
        ? (int)(DateTime.Today - FchEntrega.Value.Date).TotalDays
        : 0;
    public bool EstaVencido => DiasRetraso > 0;
}

// ── SP_PLN_PEND_PARTIDAS_DEF ────────────────────────────────────────────────
/// <summary>Partida con evaluación de calidad pendiente de definición (Karen).</summary>
public class PlnPendientePartidaDef
{
    public DateTime? Fecha           { get; set; }         // FECHA_01
    public decimal   Guia            { get; set; }         // GUIA_01
    public string    Partida         { get; set; } = "";   // PARTIDA_01
    public string    Material        { get; set; } = "";   // MATERIAL_01
    public string    ColorTecnico    { get; set; } = "";   // COLOR_TECNICO_01
    public string    Color           { get; set; } = "";   // COLOR_01
    public string    DescIntensidad  { get; set; } = "";   // DESC_INTENSIDAD_01
    public decimal   NroRmc          { get; set; }         // NRO_RMC_01
    public decimal   PesoNeto        { get; set; }         // PESO_NETO_01
    public string    CodCliente      { get; set; } = "";   // COD_CLIENTE_01
    public string    Cliente         { get; set; } = "";   // DESCLIENTE_01
    public string    Consulta        { get; set; } = "";   // CONSULTA_01
    public string    DescDefecto     { get; set; } = "";   // DESC_DEFECTO_01
    public string    Observaciones   { get; set; } = "";   // OBSERVACIONES_01
    public string    DescEvaluacion  { get; set; } = "";   // DESC_EVALUACION_01
    public DateTime? FchEntrega      { get; set; }         // FCH_ENTREGA_01

    public int  DiasRetraso => FchEntrega.HasValue
        ? (int)(DateTime.Today - FchEntrega.Value.Date).TotalDays
        : 0;
    public bool EstaVencido => DiasRetraso > 0;
}

// ── SP_PLN_FILTRO_TIPO ──────────────────────────────────────────────────────
/// <summary>Tipos de programa para combo: H=Hilandería, G=Tintorería.</summary>
public class PlnFiltroTipo
{
    public string Tipo        { get; set; } = "";
    public string Descripcion { get; set; } = "";
}

// ── SP_PLN_OBS_REVISADO ──────────────────────────────────────────────────────────────────────────
/// <summary>Partida con observacion en el proceso de revisado. Columnas de SP_PLN_OBS_REVISADO.</summary>
public class PlnObservacionRevisado
{
    public string    Partida     { get; set; } = "";
    public string    Material    { get; set; } = "";
    public DateTime? FechaFin    { get; set; }    // REVISADO_D.FECHA
    public string    Cliente     { get; set; } = "";
    public string    CodCliente  { get; set; } = "";
    public string    CodVende    { get; set; } = "";
    public string    CodMaq      { get; set; } = "";
    public string    Maquina     { get; set; } = "";
    public decimal   NroRmc      { get; set; }
    public decimal   Peso        { get; set; }
    public string    Lote        { get; set; } = "";
    public string    ColoSer     { get; set; } = "";
    public decimal   Faltante    { get; set; }    // REVISADO_D.FALTANTE
    public decimal   Rechazado   { get; set; }    // REVISADO_D.RECHAZADO
    public decimal   Reenconado  { get; set; }    // REVISADO_D.REENCONADO
    public decimal   Evaluado    { get; set; }    // REVISADO_D.EVALUADO (CONSULTA)
    public DateTime? FchEntrega  { get; set; }
    public string    Observacion { get; set; } = "";
    public string    DescAsesor  { get; set; } = "";   // DESC_ASESOR_07

    public int DiasRetraso => FchEntrega.HasValue
        ? (int)(DateTime.Today - FchEntrega.Value.Date).TotalDays
        : 0;
    public bool EstaVencido => DiasRetraso > 0;
}

// ── PlnRevisadoViewModel ─────────────────────────────────────────────────────────────────────────
/// <summary>ViewModel para la vista PendientesRevisado con sus dos pestanas.</summary>
public class PlnRevisadoViewModel
{
    public IList<PlnPendienteRevisado>   Pendientes    { get; set; } = new List<PlnPendienteRevisado>();
    public IList<PlnObservacionRevisado> Observaciones { get; set; } = new List<PlnObservacionRevisado>();
}

// -- PlnPartidasDefViewModel
/// <summary>ViewModel para PartidasPorDefinir con dos pestanas.</summary>
public class PlnPartidasDefViewModel
{
    public IList<PlnPendientePartidaDef>  Partidas        { get; set; } = new List<PlnPendientePartidaDef>();
    public IList<PlnRectificacionReceta>  Rectificaciones { get; set; } = new List<PlnRectificacionReceta>();
}

// -- PlnEvalCalidadViewModel
/// <summary>ViewModel para la vista EvalCalidad con tres pestanas (EvalCalidad + PartidasDef + RectReceta).</summary>
public class PlnEvalCalidadViewModel
{
    public IList<PlnPendienteEvalCalidad> EvalCalidad { get; set; } = new List<PlnPendienteEvalCalidad>();
}

// -- PlnSecadoViewModel
/// <summary>ViewModel para la vista PendientesSecado con dos pestanas (Pend. Secado + En Secado).</summary>
public class PlnSecadoViewModel
{
    public IList<PlnPendienteSecado> Secado { get; set; } = new List<PlnPendienteSecado>();
}
