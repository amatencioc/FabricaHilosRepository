namespace FabricaHilos.Models.Produccion.Planeamiento;

// ── SP_PLN_PEND_REVISADO ────────────────────────────────────────────────────
/// <summary>Partida pendiente de revisado (Martín). Columnas de SP_PLN_PEND_REVISADO.</summary>
public class PlnPendienteRevisado
{
    public string    Partida      { get; set; } = "";   // PARTIDA_07
    public string    Material     { get; set; } = "";   // MATERIAL_07
    public DateTime? FechaFin     { get; set; }         // FECHA_FIN_07
    public string    Cliente      { get; set; } = "";   // DESC_CLIENTE_07
    public string    Maquina      { get; set; } = "";   // DESC_MAQ_07
    public decimal   NroRmc       { get; set; }         // NRO_RMC_07
    public decimal   Peso         { get; set; }         // PESO_PARTIDA_07
    public string    Lote         { get; set; } = "";   // LOTE_07
    public string    ColoSer      { get; set; } = "";   // COLO_SER_07
    public DateTime? FchEntrega   { get; set; }         // FCH_ENTREGA_07

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
    public string    Cliente      { get; set; } = "";   // DESC_CLIENTE_03
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
/// <summary>Partida aprobada pendiente de enconado/devanado (Guevara). ORIGEN: TINTORERIA | HILANDERIA.</summary>
public class PlnPendienteEnconado
{
    public string    Partida       { get; set; } = "";  // PARTIDA_05
    public string    Material      { get; set; } = "";  // MATERIAL_05
    public string    Cliente       { get; set; } = "";  // DESC_CLIENTE_05
    public DateTime? Fecha         { get; set; }        // FECHA_05 (nullable en parte Hilandería)
    public string    EstEval       { get; set; } = "";  // DESC_EST_EVAL_05
    public string    Resultado     { get; set; } = "";  // DESC_RESULTADO_05
    public decimal   NroRmc        { get; set; }        // NRO_RMC_05
    public decimal   Peso          { get; set; }        // PESO_PARTIDA_05
    public string    Lote          { get; set; } = "";  // LOTE_05
    public string    ColoSer       { get; set; } = "";  // COLO_SER_05
    public DateTime? FchEntrega    { get; set; }        // FCH_ENTREGA_05
    public string    Origen        { get; set; } = "";  // 'TINTORERIA' | 'HILANDERIA'

    public int DiasRetraso => FchEntrega.HasValue
        ? (int)(DateTime.Today - FchEntrega.Value.Date).TotalDays
        : 0;
    public bool EstaVencido => DiasRetraso > 0;
}

// ── SP_PLN_PEND_TENIDO ──────────────────────────────────────────────────────
/// <summary>Partida pendiente de teñido (Fredy/Malena). ORIGEN: PROGRAMADO | CON_PREVIO.</summary>
public class PlnPendienteTenido
{
    public string    Partida       { get; set; } = "";  // PARTIDA
    public string    Material      { get; set; } = "";  // MATERIAL
    public string    Cliente       { get; set; } = "";  // DESC_CLIENTE
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
