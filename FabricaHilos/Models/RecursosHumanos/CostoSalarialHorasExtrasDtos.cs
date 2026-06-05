namespace FabricaHilos.Models.RecursosHumanos;

// ════════════════════════════════════════════════════════════════════════
//  ARCHIVO: CostoSalarialHorasExtrasDtos.cs
//  PROPÓSITO: Define los objetos de transferencia de datos (DTOs) que
//             viajan entre el servicio Oracle y la vista Razor.
//
//  ESTRUCTURA:
//   • CostoSalarialHorasExtrasDto      → Una fila por (Año + Mes + Área)
//   • CostoSalarialHorasExtrasViewModel → Contenedor con todas las filas
//                                         + parámetros del filtro elegido
//
//  ORIGEN DE LOS DATOS:
//   Los valores provienen de Oracle desde las tablas:
//   PARAMPLA, PLANILLA, INGRE_PLA, T_CONCEPTO, PLA_COSTO, V_CENTRO_DE_COSTOS.
//   Conceptos RTPS usados:
//     0105 = HE 25% | 0106 = HE 35% | 0107 = HE 100% feriado | 0121 = Básico
// ════════════════════════════════════════════════════════════════════════

/// <summary>
/// Una fila del reporte: representa el costo de horas extras y básico
/// de UN ÁREA en UN MES específico.
/// </summary>
/// <remarks>
/// Ejemplo: { Ano=2026, Mes=5, Area="CONERAS", NroTrab=39, MontoHeTotal=15000 }
/// significa "En mayo 2026, el área Coneras tuvo 39 trabajadores y pagó S/ 15,000 en HE".
/// </remarks>
public class CostoSalarialHorasExtrasDto
{
    /// <summary>Año de la planilla (ej: 2026).</summary>
    public int Ano { get; set; }

    /// <summary>Mes de la planilla (1-12).</summary>
    public int Mes { get; set; }

    /// <summary>
    /// Nombre del área agrupadora (ej: "HILANDERIA", "TINTORERIA", "CONERAS").
    /// Proviene de <c>V_CENTRO_DE_COSTOS.DESC_GRAN_CCOSTO</c>.
    /// </summary>
    public string Area { get; set; } = string.Empty;

    /// <summary>
    /// Número de trabajadores del área en ese mes (en planilla normal).
    /// </summary>
    public int NroTrab { get; set; }

    /// <summary>
    /// Número de trabajadores que cobraron al menos S/ 1 de horas extras.
    /// Es siempre <c>≤ NroTrab</c>.
    /// </summary>
    public int NroTrabConHe { get; set; }

    /// <summary>
    /// Monto pagado por HE realizadas en día laborable (recargo 25%).
    /// Concepto RTPS: <c>0105</c>.
    /// </summary>
    public decimal MontoHe25 { get; set; }

    /// <summary>
    /// Monto pagado por HE realizadas en horario nocturno (recargo 35%).
    /// Concepto RTPS: <c>0106</c>.
    /// </summary>
    public decimal MontoHe35 { get; set; }

    /// <summary>
    /// Monto pagado por HE en feriados / domingos (recargo 100%).
    /// Concepto RTPS: <c>0107</c>.
    /// </summary>
    public decimal MontoHe100 { get; set; }

    /// <summary>
    /// Suma de MontoHe25 + MontoHe35 + MontoHe100.
    /// Es el costo TOTAL de horas extras del área en el mes.
    /// </summary>
    public decimal MontoHeTotal { get; set; }

    /// <summary>
    /// Masa salarial básica del área en el mes (concepto <c>0121</c>).
    /// Es la base sobre la cual se calcula el Ratio HE/Básico.
    /// </summary>
    public decimal MasaSalarial { get; set; }

    /// <summary>
    /// Porcentaje del básico que representa el costo de HE.
    /// Fórmula: <c>(MontoHeTotal / MasaSalarial) × 100</c>.
    /// </summary>
    /// <remarks>
    /// Interpretación semafórica:
    ///  • ≤ 15% saludable
    ///  • 15-30% atención
    ///  • &gt; 30% crítico
    /// </remarks>
    public decimal RatioHePct { get; set; }
}

/// <summary>
/// Modelo que la vista Razor recibe en cada consulta.
/// Contiene los parámetros del filtro usados y la lista completa de filas.
/// </summary>
/// <remarks>
/// La propiedad <see cref="Filas"/> contiene una fila por cada combinación
/// (Año, Mes, Área) presente en el período seleccionado.
/// </remarks>
public class CostoSalarialHorasExtrasViewModel
{
    /// <summary>Año de inicio del filtro (ej: 2025).</summary>
    public int AnoIni { get; set; }

    /// <summary>Mes de inicio del filtro (1-12).</summary>
    public int MesIni { get; set; }

    /// <summary>Año fin del filtro (ej: 2026).</summary>
    public int AnoFin { get; set; }

    /// <summary>Mes fin del filtro (1-12).</summary>
    public int MesFin { get; set; }

    /// <summary>
    /// Tipo de empleado a incluir en el cálculo:
    ///  • <c>"T"</c> = Todos (default)
    ///  • <c>"O"</c> = Solo Obreros   (PARAMPLA.C_EO = 'O')
    ///  • <c>"E"</c> = Solo Empleados (PARAMPLA.C_EO = 'E')
    /// </summary>
    public string Tipo { get; set; } = "T";

    /// <summary>
    /// Lista de filas del reporte (una por cada combinación Año+Mes+Área).
    /// Si está vacía, no hay datos para el período seleccionado.
    /// </summary>
    public List<CostoSalarialHorasExtrasDto> Filas { get; set; } = new();
}
