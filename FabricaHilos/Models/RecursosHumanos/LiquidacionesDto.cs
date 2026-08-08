namespace FabricaHilos.Models.RecursosHumanos;

/// <summary>
/// Fila del reporte "Liquidaciones por banco" (PKG_RPT_PLANILLA.P_LIQUIDACIONES_BANCO).
/// Representa una liquidación (vacaciones, CTS, etc.) de un trabajador.
/// </summary>
public class LiquidacionesDto
{
    public int     ItemSeq      { get; set; }
    public string? CBanco       { get; set; }
    public string? DescBanco    { get; set; }
    public string? CCodigo      { get; set; }
    public string? CCodper      { get; set; }
    public string  Nombre       { get; set; } = "";
    public decimal PagoVacac    { get; set; }
    public decimal PagoCts      { get; set; }
    public decimal TotalLiqui   { get; set; }

    public bool EsTotal => string.Equals(Nombre, "TOTAL", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(CCodigo);
}

/// <summary>
/// Monto para un mes específico en el reporte pivote de liquidaciones.
/// </summary>
public class LiquidacionesMesDto
{
    public decimal PagoVacac  { get; set; }
    public decimal PagoCts    { get; set; }
    public decimal Subtotal   { get; set; } // PagoVacac + PagoCts
}

/// <summary>
/// Fila (trabajador) en el reporte pivote de liquidaciones.
/// </summary>
public class LiquidacionesFilaDto
{
    public int     Item      { get; set; }
    public string? CCodper   { get; set; }
    public string  Nombre    { get; set; } = "";
    public decimal PagoVacac { get; set; }
    public decimal PagoCts   { get; set; }
    public decimal Total     { get; set; }
}

/// <summary>
/// Grupo (banco) en el reporte de liquidaciones por banco.
/// </summary>
public class LiquidacionesGrupoDto
{
    public string? CBanco     { get; set; }
    public string? DescBanco  { get; set; }
    public List<LiquidacionesFilaDto> Filas { get; set; } = new();
    public decimal TotalPagoVacac { get; set; }
    public decimal TotalPagoCts   { get; set; }
    public decimal TotalGeneral   { get; set; }
}

/// <summary>
/// Reporte completo de liquidaciones por banco.
/// </summary>
public class LiquidacionesReporteDto
{
    public string Titulo { get; set; } = "";
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public List<LiquidacionesGrupoDto> Grupos { get; set; } = new();
}

/// <summary>
/// Filtro para la consulta de liquidaciones.
/// </summary>
public class LiquidacionesFiltroDto
{
    public DateTime FechaLiquidacion { get; set; }
}
