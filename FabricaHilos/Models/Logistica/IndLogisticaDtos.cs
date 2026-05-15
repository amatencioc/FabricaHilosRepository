namespace FabricaHilos.Models.Logistica;

// ── P_DETALLE ─────────────────────────────────────────────────────────────────
// Una fila por ítem/despacho. Incluye todos los estados.
// Fuente principal para exportar a Excel.

public class IndLogisticaDetalleDto
{
    public string?   Tipo          { get; set; }
    public long      NumReq        { get; set; }
    public DateTime? Fecha         { get; set; }
    public DateTime? FAutoriza     { get; set; }
    public DateTime? FRecibe       { get; set; }
    public string?   OrdenCompra   { get; set; }
    public DateTime? FchOrden      { get; set; }
    public string?   Destino       { get; set; }
    public string?   DescDestino   { get; set; }
    public string?   Solicita      { get; set; }
    public string?   Observacion   { get; set; }
    public string?   CodArt        { get; set; }
    public string?   DescArticulo  { get; set; }
    public string?   Unidad        { get; set; }
    public decimal   Cantidad      { get; set; }
    public decimal   CantDesp      { get; set; }
    public decimal   Saldo         { get; set; }
    public decimal   PUnit         { get; set; }
    public decimal   SubTotal      { get; set; }
    public decimal   Igv           { get; set; }
    public decimal   Total         { get; set; }
    public string?   Estado        { get; set; }
}

// ── P_DASHBOARD: RESUMEN ──────────────────────────────────────────────────────
// PCT_ATENDIDO es window function OVER(PARTITION BY TIPO) → mismo valor
// en todas las filas del mismo TIPO: % de no-anuladas que ya están ATENDIDAS.

public class IndLogisticaResumenDto
{
    public string?  Tipo         { get; set; }
    public string?  Estado       { get; set; }
    public int      CantReqs     { get; set; }
    public int      CantItems    { get; set; }
    public decimal  MontoTotal   { get; set; }
    public decimal  PctAtendido  { get; set; }  // misma cifra por TIPO
}

// ── P_DASHBOARD: TIEMPOS ──────────────────────────────────────────────────────
// Una sola fila. Cada tramo calculado solo sobre reqs con ambas fechas.

public class IndLogisticaTiemposDto
{
    public int     TotalReqs             { get; set; }
    public decimal DiasRegAutorizacion   { get; set; }
    public decimal DiasAutRecibo         { get; set; }
    public decimal DiasReciboOc          { get; set; }
    public decimal DiasCicloTotal        { get; set; }
}

// ── P_DASHBOARD: TOP CENTRO DE COSTO ─────────────────────────────────────────
// Top 10 destinos por monto. CANT_REQS permite colorear CC vs Activo Fijo.

public class IndLogisticaTopCcostoDto
{
    public string?  Destino      { get; set; }
    public string?  DescDestino  { get; set; }
    public string?  TpDestino    { get; set; }   // 'U'=CC, 'A'=Activo Fijo
    public int      CantItems    { get; set; }
    public int      CantReqs     { get; set; }   // agregado en nueva versión SQL
    public decimal  MontoTotal   { get; set; }
}

// ── P_DASHBOARD: PENDIENTES ───────────────────────────────────────────────────
// DIAS_EN_ESPERA nunca es NULL: usa F_RECIBE si existe, sino FECHA.
// Semáforo del SQL: verde <3d, amarillo 3-7d, rojo >7d.

public class IndLogisticaPendienteDto
{
    public long      NumReq           { get; set; }
    public DateTime? Fecha            { get; set; }
    public string?   Tipo             { get; set; }
    public string?   Estado           { get; set; }   // columna nueva en SQL
    public string?   Solicita         { get; set; }
    public string?   CodArt           { get; set; }
    public string?   DescArticulo     { get; set; }
    public decimal   Saldo            { get; set; }
    public decimal   MontoPendiente   { get; set; }
    public int       DiasEnEspera     { get; set; }   // nunca NULL (lógica en SQL)
}

// ── P_CICLO_VIDA ──────────────────────────────────────────────────────────────
// Una fila por requisición ATENDIDA (ESTADO='6') con las 4 fechas hito
// y los días de cada tramo. Solo reqs con los 3 tramos calculables.
// Varias reqs pueden compartir la misma NRO_OC (hasta 5 en datos reales).

public class IndLogisticaCicloVidaDto
{
    public long      NumReq        { get; set; }
    public string?   Tipo          { get; set; }
    public string?   NroOc         { get; set; }
    public DateTime? FchRegistro   { get; set; }
    public DateTime? FchAutoriza   { get; set; }
    public DateTime? FchReciboLog  { get; set; }
    public DateTime? FchOc         { get; set; }
    public int       T1RegAut      { get; set; }   // días tramo 1
    public int       T2AutRec      { get; set; }   // días tramo 2
    public int       T3RecOc       { get; set; }   // días tramo 3
    public int       TCicloTotal   { get; set; }   // días ciclo completo
}

// ── P_TENDENCIA_MENSUAL ───────────────────────────────────────────────────────
// Una fila por mes. Promedios de tramos + % cumplimiento SLA.
// SLA sugerido: PCT_HASTA_5DIAS. Si baja de 70% el proceso se rezaga.

public class IndLogisticaTendenciaMensualDto
{
    public string?  Mes            { get; set; }   // 'YYYY-MM'
    public int      CantReqs       { get; set; }
    public decimal  T1Avg          { get; set; }   // Registro → Autorización
    public decimal  T2Avg          { get; set; }   // Autorización → Recibo Log
    public decimal  T3Avg          { get; set; }   // Recibo Log → OC
    public decimal  CicloAvg       { get; set; }   // ciclo total promedio
    public decimal  PctMismoDia    { get; set; }   // % atendidas el mismo día
    public decimal  PctHasta5Dias  { get; set; }   // % ciclo ≤ 5 días (SLA)
}

// ── ViewModels ────────────────────────────────────────────────────────────────

public class IndLogisticaDashboardViewModel
{
    public DateTime                        FechaDesde  { get; set; }
    public DateTime                        FechaHasta  { get; set; }
    public List<IndLogisticaResumenDto>    Resumen     { get; set; } = [];
    public IndLogisticaTiemposDto?         Tiempos     { get; set; }
    public List<IndLogisticaTopCcostoDto>  TopCcosto   { get; set; } = [];
    public List<IndLogisticaPendienteDto>  Pendientes  { get; set; } = [];
}

public class IndLogisticaCicloVidaViewModel
{
    public DateTime                       FechaDesde  { get; set; }
    public DateTime                       FechaHasta  { get; set; }
    public List<IndLogisticaCicloVidaDto> Items       { get; set; } = [];
}

public class IndLogisticaTendenciaMensualViewModel
{
    public int                                    MesesAtras { get; set; }
    public List<IndLogisticaTendenciaMensualDto>  Items      { get; set; } = [];
}
