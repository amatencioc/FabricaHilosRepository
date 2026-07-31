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
/// Ficha de empleado (FchIngreso/Sexo/FchNacimiento/EstadoCivil/
/// NivelEducativo/Afp) agregada en 14_CAP_HEADCOUNT_FICHA_EMPLEADO.sql
/// (24/07/2026), fuente V_PERSONAL + RH_RTPS (mismo patrón que
/// RRHH/LMS/QUERY_EMP_SUPERVISOR.TXT).
/// </summary>
public class CapHeadcountDetalle
{
    public string?  CodJefatura     { get; set; }
    public string?  NombreJefatura  { get; set; }
    /// Usuario (CS_USER.C_USER) del jefe, cuando existe login. "-" si no tiene (ver 14_CAP_HEADCOUNT_FICHA_EMPLEADO.sql).
    public string?  CodUsuarioJefe  { get; set; }
    public string   GranCcosto      { get; set; } = "";
    public string   DescArea        { get; set; } = "";
    public string   CentroCosto     { get; set; } = "";
    public string   DescCcosto      { get; set; } = "";
    public string   CCodigo         { get; set; } = "";
    /// Usuario (CS_USER.C_USER) del propio trabajador, cuando existe login. Null si no tiene.
    public string?  CodUsuario      { get; set; }
    public string   NombreTrabajador { get; set; } = "";
    public string?  DocId           { get; set; }
    public string?  CodCargo        { get; set; }
    public string?  DescCargo       { get; set; }
    public DateTime? FchIngreso     { get; set; }
    public string?  Sexo            { get; set; }
    public DateTime? FchNacimiento  { get; set; }
    public string?  EstadoCivil     { get; set; }
    public string?  NivelEducativo  { get; set; }
    public string?  Afp             { get; set; }
}
