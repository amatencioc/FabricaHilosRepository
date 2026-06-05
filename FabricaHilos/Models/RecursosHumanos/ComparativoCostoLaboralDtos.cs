namespace FabricaHilos.Models.RecursosHumanos;

// ════════════════════════════════════════════════════════════════════════════
//  KPI ComparativoCostoLaboral
//  ─────────────────────────────────────────────────────────────────────────
//  Compara el costo laboral total (sueldo + cargas sociales calculadas por
//  Ley) entre dos años (Año1 vs Año2), por área de costo.
//
//  EL "COSTO LABORAL" DE ESTE KPI = Básico × 1.4232 (Opción C confirmada).
//  Detalle del factor:
//      Sueldo Básico  ........  100.00 %  ← concepto RTPS '0121' en planilla
//      EsSalud (Ley 26790)         9.00 %  (carga social patronal)
//      CTS                         8.33 %  (1 / 12)
//      Gratificaciones            16.66 %  (2 / 12 → Julio + Diciembre)
//      Vacaciones                  8.33 %  (1 / 12)
//      ─────────────────────────────────
//      TOTAL                     142.32 %  → Factor 1.4232 sobre el básico
//
//  Por qué se calcula y no se lee de planilla:
//   • Vacaciones (0118), Gratificaciones (0406), EsSalud Vida (0604) y CTS
//     NO se pagan como concepto en cada planilla mensual.
//   • Se devengan mes a mes y se desembolsan en planillas especiales
//     (Julio/Diciembre para gratificaciones, mayo/noviembre para CTS, etc.).
//   • Por eso el costo laboral "real" mensual se estima multiplicando el
//     básico por el factor de ley 1.4232 (esta es la práctica estándar en
//     manufactura textil peruana, alineada con el PDF de referencia donde
//     S/ 1,243.11 × 1.4233 ≈ S/ 1,769.19).
// ════════════════════════════════════════════════════════════════════════════

/// <summary>Fila base (una por ANO + AREA) entregada por el query Oracle.</summary>
public class ComparativoCostoLaboralFilaDto
{
    public int     Ano               { get; set; }
    public string  Area              { get; set; } = string.Empty;
    public int     NumMeses          { get; set; }   // meses con planilla en el año
    public decimal NroTrabProm       { get; set; }   // promedio mensual de empleados
    public decimal BasicoTotal       { get; set; }   // suma del año
    public decimal HeTotal           { get; set; }   // suma del año
    public decimal BasicoPromMes     { get; set; }   // promedio mensual del básico
    public decimal HePromMes         { get; set; }   // promedio mensual de HE
    public decimal BasicoPromXTrab   { get; set; }   // promedio mensual por trabajador

    // ── Derivados (calculados en C#, NO en SQL) ──
    public decimal CostoLaboralPromXTrab => Math.Round(BasicoPromXTrab * ComparativoCostoLaboralConstants.FactorTotal, 2);
    public decimal CostoLaboralPromMes   => Math.Round(BasicoPromMes   * ComparativoCostoLaboralConstants.FactorTotal, 2);
    public decimal CostoLaboralTotal     => Math.Round(BasicoTotal     * ComparativoCostoLaboralConstants.FactorTotal, 2);
}

/// <summary>Cuadro 1 — comparativo de costo laboral total Año1 vs Año2 por área.</summary>
public class ComparativoCuadro1Dto
{
    public string  Area                   { get; set; } = string.Empty;
    public decimal NroTrabPromAno1        { get; set; }
    public decimal NroTrabPromAno2        { get; set; }
    public decimal CostoLaboralPromMesAno1 { get; set; }   // S/ promedio mensual con beneficios
    public decimal CostoLaboralPromMesAno2 { get; set; }
    public decimal DiferenciaNroTrab      { get; set; }   // A2 − A1
    public decimal DiferenciaCostoMes     { get; set; }   // A2 − A1
    public decimal VariacionCostoPct      { get; set; }   // % cambio
}

/// <summary>Cuadro 2 — promedio mensual de NTrab con impacto monetario.</summary>
public class ComparativoCuadro2Dto
{
    public string  Area                  { get; set; } = string.Empty;
    public decimal NroTrabPromAno1       { get; set; }
    public decimal NroTrabPromAno2       { get; set; }
    public decimal DiferenciaNroTrab     { get; set; }     // A2 − A1
    public decimal CostoXTrabAno2        { get; set; }     // costo laboral promedio por trabajador (con beneficios) Año2
    public decimal ImpactoMensual        { get; set; }     // DIF × CostoXTrabAno2
    public decimal ImpactoAnualEstimado  { get; set; }     // ImpactoMensual × 12
}

/// <summary>Cuadro 3 — desglose del costo total mensual por trabajador.</summary>
public class ComparativoCuadro3Dto
{
    public string  Area              { get; set; } = string.Empty;
    public decimal SueldoBasico      { get; set; }   // S/ — promedio por trabajador (concepto 0121)
    public decimal MontoEsSalud      { get; set; }   // 9.00 %
    public decimal MontoCts          { get; set; }   // 8.33 %
    public decimal MontoGratif       { get; set; }   // 16.66 %
    public decimal MontoVacaciones   { get; set; }   // 8.33 %
    public decimal CostoTotalMes     { get; set; }   // 142.32 % = factor 1.4232
}

/// <summary>ViewModel completo del dashboard.</summary>
public class ComparativoCostoLaboralViewModel
{
    public int Ano1 { get; set; }   // año "antes"  (ej. 2025)
    public int Ano2 { get; set; }   // año "después" (ej. 2026)
    public int MesIniAno1 { get; set; } = 1;
    public int MesFinAno1 { get; set; } = 12;
    public int MesIniAno2 { get; set; } = 1;
    public int MesFinAno2 { get; set; } = 12;

    /// <summary>
    /// Sueldo básico promedio (S/.) ingresado manualmente por el usuario.
    /// Si es &gt; 0, se usa para recalcular Cuadros 1/2/3 con un básico único global
    /// (mismo valor para Año 1 y Año 2). Si es 0, se mantiene el cálculo desde
    /// planilla (concepto RTPS '0121' promediado por área y año).
    /// </summary>
    public decimal BasicoManual { get; set; }

    /// <summary>
    /// Filtro de tipo de empleado (campo Oracle <c>PARAMPLA.C_EO</c>):
    ///   <c>"T"</c> = Todos (default),
    ///   <c>"O"</c> = sólo Obreros (<c>C_EO='O'</c>),
    ///   <c>"E"</c> = sólo Empleados (<c>C_EO='E'</c>).
    /// </summary>
    public string Tipo { get; set; } = "T";

    public List<ComparativoCostoLaboralFilaDto> FilasCrudo { get; set; } = new();
    public List<ComparativoCuadro1Dto> Cuadro1 { get; set; } = new();
    public List<ComparativoCuadro2Dto> Cuadro2 { get; set; } = new();
    public List<ComparativoCuadro3Dto> Cuadro3 { get; set; } = new();

    // ── Totales generales ──
    public decimal TotalNroTrabAno1       { get; set; }
    public decimal TotalNroTrabAno2       { get; set; }
    public decimal TotalCostoLaboralAno1  { get; set; }   // promedio mensual × 12 meses estimado
    public decimal TotalCostoLaboralAno2  { get; set; }
    public decimal TotalImpactoMensual    { get; set; }
    public decimal TotalImpactoAnual      { get; set; }
}

/// <summary>Constantes del factor de ley aplicado al básico.</summary>
public static class ComparativoCostoLaboralConstants
{
    /// <summary>EsSalud — Ley 26790 (9% del básico).</summary>
    public const decimal FactorEsSalud = 0.0900m;

    /// <summary>CTS — 1/12 del básico (8.33%).</summary>
    public const decimal FactorCts = 0.0833m;

    /// <summary>Gratificaciones — 2/12 (Julio + Diciembre = 16.66%).</summary>
    public const decimal FactorGratif = 0.1666m;

    /// <summary>Vacaciones — 1/12 (8.33%).</summary>
    public const decimal FactorVacaciones = 0.0833m;

    /// <summary>Factor total a aplicar sobre el básico = 1.4232 (142.32%).</summary>
    public const decimal FactorTotal = 1.0000m + FactorEsSalud + FactorCts + FactorGratif + FactorVacaciones;
}
