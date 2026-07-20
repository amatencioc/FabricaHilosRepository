namespace FabricaHilos.Models.Capacitacion;

/// <summary>
/// Opción de supervisor/jefe de área para el filtro de Reportes
/// (distinct sobre CAP_V_EMPLEADO.COD_SUPERVISOR/NOMBRE_SUPERVISOR).
/// </summary>
public class CapSupervisorOption
{
    public string CodSupervisor    { get; set; } = "";
    public string NombreSupervisor { get; set; } = "";
}

/// <summary>
/// Fila de headcount para el "Dashboard por Jefaturas" — 1 fila por
/// trabajador de la nómina vigente (ver vista CAP_V_HEADCOUNT_JEFATURA
/// en 08_CAP_REPORTES_ORG.sql). El agrupamiento Jefatura → Área →
/// Centro de Costo se arma en el cliente (JS), igual patrón que el
/// "Panel interactivo" de Reportes.cshtml.
/// </summary>
public class CapHeadcountDetalle
{
    public string? CodJefatura    { get; set; }
    public string? NombreJefatura { get; set; }
    public string  GranCcosto     { get; set; } = "";
    public string  DescArea       { get; set; } = "";
    public string  CentroCosto    { get; set; } = "";
    public string  DescCcosto     { get; set; } = "";
    public string  CCodigo        { get; set; } = "";
    public string  NombreTrabajador { get; set; } = "";
    public string? DocId          { get; set; }
    public string? CodCargo       { get; set; }
    public string? DescCargo      { get; set; }
}
