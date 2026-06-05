namespace FabricaHilos.Models.RecursosHumanos;

/// <summary>
/// Resultado del BLOQUE 1: una fila por período (ANO, MES) con el resumen general.
/// </summary>
public class HorasExtrasResumenMesDto
{
    public int     Ano                    { get; set; }
    public int     Mes                    { get; set; }
    public decimal TotalHorasExtras       { get; set; }
    public int     TotalTrabajadores      { get; set; }
    public decimal PromHorasExtrasTrab    { get; set; }
    public int     TrabajadoresConHe      { get; set; }
    public decimal PctColaboradoresConHe  { get; set; }
}

/// <summary>
/// Resultado del BLOQUE 2: una fila por período + área con todos los indicadores calculados en Oracle.
/// </summary>
public class HorasExtrasAreaDto
{
    public int     Ano                   { get; set; }
    public int     Mes                   { get; set; }
    public string  Area                  { get; set; } = string.Empty;
    public int     TotalTrabajadores     { get; set; }
    public decimal TotalHorasExtras      { get; set; }
    public decimal PromHorasExtrasTrab   { get; set; }
    public decimal PctTotalHorasExtras   { get; set; }
    public int     TrabajadoresConHe     { get; set; }
    public decimal PctTrabajadoresConHe  { get; set; }
}

/// <summary>
/// Resultado del KPI ConcentraciónSobretiempoArea: una fila por período + área.
/// </summary>
public class ConcentracionSobretiempoAreaDto
{
    public int     Ano        { get; set; }
    public int     Mes        { get; set; }
    public string  Area       { get; set; } = string.Empty;
    public int     TotalTrab  { get; set; }
    public int     ConHe      { get; set; }
    public int     SinHe      { get; set; }
    public decimal PctConHe   { get; set; }
    public decimal PctSinHe   { get; set; }
}

/// <summary>
/// ViewModel para el dashboard KPI ConcentraciónSobretiempoArea.
/// </summary>
public class ConcentracionSobretiempoAreaViewModel
{
    public int AnoIni  { get; set; }
    public int MesIni  { get; set; }
    public int AnoFin  { get; set; }
    public int MesFin  { get; set; }

    public List<ConcentracionSobretiempoAreaDto> Filas { get; set; } = new();
}

// ─────────────────────────────────────────────────────────────────────────────
// KPI EvolucionMensualMasaSalarial
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// BLOQUE 1: Detalle por mes y área (masa salarial + sobretiempo + ratio).
/// </summary>
public class EvolucionMasaSalarialAreaDto
{
    public int     Ano           { get; set; }
    public int     Mes           { get; set; }
    public string  Area          { get; set; } = string.Empty;
    public int     NroTrab       { get; set; }
    public decimal MasaSalarial  { get; set; }
    public decimal Sobretiempo   { get; set; }
    public decimal RatioHePct    { get; set; }
}

/// <summary>
/// BLOQUE 2: Resumen mensual empresa (para gráfico de línea).
/// </summary>
public class EvolucionMasaSalarialResumenDto
{
    public int     Ano           { get; set; }
    public int     Mes           { get; set; }
    public decimal MasaSalarial  { get; set; }
    public decimal Sobretiempo   { get; set; }
    public decimal RatioHePct    { get; set; }
    public decimal VarVsMesAnt   { get; set; }
    public decimal VarPct        { get; set; }
}

/// <summary>
/// ViewModel para el dashboard KPI EvolucionMensualMasaSalarial.
/// </summary>
public class EvolucionMasaSalarialViewModel
{
    public int AnoIni  { get; set; }
    public int MesIni  { get; set; }
    public int AnoFin  { get; set; }
    public int MesFin  { get; set; }

    /// <summary>
    /// Filtro de tipo de empleado aplicado (campo Oracle <c>PARAMPLA.C_EO</c>):
    ///  <c>"T"</c> = Todos, <c>"O"</c> = Solo Obreros, <c>"E"</c> = Solo Empleados.
    /// </summary>
    public string Tipo { get; set; } = "T";

    public List<EvolucionMasaSalarialResumenDto> Resumen { get; set; } = new();
    public List<EvolucionMasaSalarialAreaDto>    Areas   { get; set; } = new();
}

/// <summary>
/// ViewModel completo para el dashboard KPI Horas Extras por Área.
/// </summary>
public class HorasExtrasKpiViewModel
{
    // ── Filtros aplicados ────────────────────────────────────────────────
    public int AnoIni  { get; set; }
    public int MesIni  { get; set; }
    public int AnoFin  { get; set; }
    public int MesFin  { get; set; }

    // ── Resumen por período (BLOQUE 1) ───────────────────────────────────
    public List<HorasExtrasResumenMesDto> Resumen { get; set; } = new();

    // ── Tabla de áreas por período (BLOQUE 2) ────────────────────────────
    public List<HorasExtrasAreaDto> Areas  { get; set; } = new();
}
