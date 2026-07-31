namespace FabricaHilos.Models.Capacitacion;

// ── Alcance/visibilidad de un curso (ver RRHH/LMS/Database/07_CAP_VISIBILIDAD_CURSO.sql) ──

/// <summary>Opción de área disponible para asignar a un curso (TABLAS_AUXILIARES TIPO=83).</summary>
public class CapAreaOption
{
    public string GranCcosto { get; set; } = "";
    public string DescArea   { get; set; } = "";
}

/// <summary>Área ya asignada a un curso (CAP_CURSO_AREA).</summary>
public class CapCursoArea
{
    public int    IdCurso    { get; set; }
    public string GranCcosto { get; set; } = "";
    public string? DescArea  { get; set; }
}

/// <summary>Personal específico asignado a un curso (CAP_CURSO_USUARIO). Clave real = CCodigo
/// (V_PERSONAL.C_CODIGO, existe para TODO el personal); CodUsuario es solo snapshot de
/// CS_USER.C_USER y puede ser NULL si la persona aún no tiene cuenta de acceso al LMS.</summary>
public class CapCursoUsuario
{
    public int     IdCurso        { get; set; }
    public string  CCodigo        { get; set; } = "";
    public string? CodUsuario     { get; set; }
    public string? NombreUsuario  { get; set; }
}

/// <summary>Resultado de búsqueda de personal para el selector "Personal específico" (select2 AJAX).
/// Fuente = CAP_V_HEADCOUNT_JEFATURA (objeto principal/universo completo, mismo que alimenta la
/// pestaña "Jefaturas" — ver 08/14_CAP_*.sql), NO se une con CS_USER para filtrar — CodUsuario
/// viene NULL si la persona todavía no tiene cuenta de acceso al LMS (igual se puede asignar).</summary>
public class CapEmpleadoBusqueda
{
    public string  CCodigo    { get; set; } = "";
    public string? CodUsuario { get; set; }
    public string? Nombre     { get; set; }
    public string? DescArea   { get; set; }
    public string? DescCargo  { get; set; }
}

// ── Jerarquía GRAN_CCOSTO (área) → CENTRO_COSTO (ver 12_CAP_JERARQUIA_CCOSTO.sql) ──
// Un GRAN_CCOSTO agrupa varios CENTRO_COSTO (ej. área "PREPARATORIA" contiene
// BATAN, CARDAS, MANUARES, PABILERA, PEINADORA, etc. — CENTRO_DE_COSTOS.GRAN_CCOSTO).

/// <summary>Opción de centro de costo (CENTRO_DE_COSTOS) con su área dueña, para
/// el selector "Centros de costo específicos" (CursoForm) y el filtro de Reportes.</summary>
public class CapCcostoOption
{
    public string CentroCosto  { get; set; } = "";
    public string NombreCcosto { get; set; } = "";
    public string GranCcosto   { get; set; } = "";
    public string DescArea     { get; set; } = "";
}

/// <summary>Centro de costo ya asignado a un curso (CAP_CURSO_CCOSTO) — refinamiento
/// puntual de CAP_CURSO_AREA cuando ALCANCE='AREA'.</summary>
public class CapCursoCcosto
{
    public int     IdCurso     { get; set; }
    public string  CentroCosto { get; set; } = "";
    public string? GranCcosto  { get; set; }
    public string? DescCcosto  { get; set; }
    public string? DescArea    { get; set; }
}

// ── Cargo (ver 15_CAP_CURSO_CARGO.sql) ──
// Dimensión de asignación adicional, independiente de Área/Centro de Costo (ej. "asignar
// este curso a todos los Supervisores de la empresa, sin importar su área"). Combina con
// ALCANCE='AREA' igual que CAP_CURSO_CCOSTO (todas se evalúan con OR en la visibilidad).

/// <summary>Opción de cargo disponible para asignar a un curso (T_CARGO), con el headcount
/// actual (universo completo — ver CAP_V_HEADCOUNT_JEFATURA) para orientar al admin.</summary>
public class CapCargoOption
{
    public string CodCargo  { get; set; } = "";
    public string DescCargo { get; set; } = "";
    public int    Cantidad  { get; set; }
}

/// <summary>Cargo ya asignado a un curso (CAP_CURSO_CARGO).</summary>
public class CapCursoCargo
{
    public int     IdCurso   { get; set; }
    public string  CodCargo  { get; set; } = "";
    public string? DescCargo { get; set; }
}

