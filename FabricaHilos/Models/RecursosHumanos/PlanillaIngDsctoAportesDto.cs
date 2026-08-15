namespace FabricaHilos.Models.RecursosHumanos;

/// <summary>
/// Fila del reporte "Planilla de ingreso y descuento de aportes" (PKG_RPT_PLANILLA.P_INGR_DESC_APORT).
/// La fila con CCodigo = null y Nombre = "TOTAL" representa el total general.
/// </summary>
public class PlanillaIngDsctoAportesDto
{
    public string? CCodigo { get; set; }
    public string? CCodper { get; set; }
    public string  Nombre  { get; set; } = "";

    public decimal Horas         { get; set; }
    public decimal Basico        { get; set; }
    public decimal BasicoTarifa  { get; set; }
    public decimal Dominical     { get; set; }
    public decimal Turno2        { get; set; }
    public decimal Turno3        { get; set; }
    public decimal He25          { get; set; }
    public decimal He100         { get; set; }
    public decimal PrimaTextil   { get; set; }
    public decimal Dl25981       { get; set; }
    public decimal AsigFam       { get; set; }
    public decimal AsigFamLey    { get; set; }
    public decimal Movilidad     { get; set; }
    public decimal Colacion      { get; set; }
    public decimal He35          { get; set; }
    public decimal DmEnfermedad  { get; set; }
    public decimal BonVac        { get; set; }
    public decimal DmAccidente   { get; set; }
    public decimal LicCh         { get; set; }
    public decimal TotIngreso    { get; set; }

    public decimal DsctoJudicial { get; set; }
    public decimal DsctoSindical { get; set; }
    public decimal Tardanza      { get; set; }
    public decimal DsctoMedico   { get; set; }
    public decimal CuotPrestamo  { get; set; }
    public decimal DsctoComedor  { get; set; }

    public decimal Snp           { get; set; }
    public decimal QuintaCat     { get; set; }
    public decimal Afp10         { get; set; }
    public decimal AfpCom        { get; set; }
    public decimal AfpSeg        { get; set; }

    public decimal TotDscto      { get; set; }
    public decimal Neto          { get; set; }

    public bool EsTotal => string.Equals(Nombre, "TOTAL", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(CCodigo);
}

public class PlanillaIngDsctoAportesFiltroDto
{
    public int Anio   { get; set; }
    public int Semana { get; set; }
}

/// <summary>
/// Fila del reporte "Resumen de pago por banco" (PKG_RPT_PLANILLA.P_RESUMEN_PAGO_BANCO).
/// </summary>
public class ResumenPagoBancoDto
{
    public string? CBanco       { get; set; }
    public string? DescBanco    { get; set; }
    public long    NumPla       { get; set; }
    public string? DescPlanilla { get; set; }
    public DateTime? FInicio    { get; set; }
    public DateTime? FFinal     { get; set; }
    public string? CCodigo      { get; set; }
    public string? CCodper      { get; set; }
    public string  Nombre       { get; set; } = "";
    public string? DescCargo    { get; set; }
    public DateTime? FIngreso   { get; set; }
    public DateTime? FVencto    { get; set; }
    public string? CEstado      { get; set; }
    public string? Situacion    { get; set; }
    public DateTime? FCese      { get; set; }
    public decimal Subtotal     { get; set; }
    public decimal Extra        { get; set; }
    public decimal ImpVacac     { get; set; }
    public decimal Importe      { get; set; }
}

/// <summary>
/// Monto de un mes (columna pivote) para un empleado en el reporte de resumen por banco:
/// "PLANILLA SEMANAL {MES}" (Subtotal) e "IMPORTE HORAS EXTRAS" (Extra).
/// </summary>
public class ResumenPagoBancoMesDto
{
    public decimal PlanillaSemanal { get; set; }
    public decimal ImporteExtra    { get; set; }
}

/// <summary>
/// Fila (empleado) del reporte pivote de resumen por banco, con un monto por cada
/// mes de <see cref="ResumenPagoBancoGrupoDto.Meses"/> (alineado por posición).
/// </summary>
public class ResumenPagoBancoFilaDto
{
    public int     Item     { get; set; }
    public string? CCodper  { get; set; }
    public string  Nombre   { get; set; } = "";
    public List<ResumenPagoBancoMesDto> Montos { get; set; } = new();
    public decimal TotalSemana { get; set; }

    /// <summary>Adelanto de vacaciones (RH_ADELANTOS concepto 1079) del empleado en este periodo.</summary>
    public decimal ImpVacac { get; set; }
}

/// <summary>
/// Grupo (banco) del reporte pivote de resumen por banco: encabezado de meses y filas de empleados.
/// </summary>
public class ResumenPagoBancoGrupoDto
{
    public string? CBanco    { get; set; }
    public string? DescBanco { get; set; }
    public List<string> Meses { get; set; } = new();
    public List<ResumenPagoBancoFilaDto> Filas { get; set; } = new();
    public List<ResumenPagoBancoMesDto> TotalesPorMes { get; set; } = new();
    public decimal TotalGeneral { get; set; }

    /// <summary>Suma de ImpVacac (pago de vacaciones incluido en la planilla) del banco.</summary>
    public decimal TotalImpVacac { get; set; }
}

/// <summary>
/// Reporte pivote completo de "Resumen de pago por banco": título con el periodo consultado
/// y un grupo por cada banco.
/// </summary>
public class ResumenPagoBancoReporteDto
{
    public string Titulo { get; set; } = "";
    public List<ResumenPagoBancoGrupoDto> Grupos { get; set; } = new();
}

/// <summary>
/// Fila del reporte "Resumen de pago por Gran Centro de Costo" (PKG_RPT_PLANILLA.P_RESUMEN_PAGO_CCOSTO).
/// </summary>
public class ResumenPagoCcostoDto
{
    public string? GranCcosto     { get; set; }
    public string? DescGranCcosto { get; set; }
    public decimal Cant           { get; set; }
    public decimal ImpDiaLab      { get; set; }
    public decimal HorExtra       { get; set; }
    public decimal ImpExtra       { get; set; }
    public decimal ImpVacac       { get; set; }
    public decimal ImpAsig        { get; set; }
    public decimal ImpTot         { get; set; }
    public decimal Subtotal       { get; set; }
}
