namespace FabricaHilos.Models.RecursosHumanos;

// ── Login ──────────────────────────────────────────────────────────────────

public class AuthHorasLoginRequest
{
    public string CodUsuario { get; set; } = string.Empty;
}

public class AuthHorasLoginResult
{
    public bool   Ok              { get; set; }
    public string Mensaje         { get; set; } = string.Empty;
    public string CodUsuario      { get; set; } = string.Empty;
    public string NomUsuario      { get; set; } = string.Empty;
    public string? CodPersonal    { get; set; }
    public string IndAdmin        { get; set; } = "N";
    public int    CntEmpresas     { get; set; }
    public string? CodEmpresaUnica { get; set; }
    public string EsAdmAlguna     { get; set; } = "N";
}

// ── Empleados ─────────────────────────────────────────────────────────────

public class AuthHorasEmpleadoDto
{
    public string  CodPersonal      { get; set; } = string.Empty;
    public string  NombreCompleto   { get; set; } = string.Empty;
    public string? NumFotocheck     { get; set; }
    public string  CodEmpresa       { get; set; } = string.Empty;
    public string  CodSucursal      { get; set; } = string.Empty;
    public string  CodCCostos       { get; set; } = string.Empty;
    public string  DesCCostos       { get; set; } = string.Empty;
    public string  CodTipoPlanilla  { get; set; } = string.Empty;
    public string  DesTipoPlanilla  { get; set; } = string.Empty;
    public string  TipEstado        { get; set; } = string.Empty;
    public string? HorId            { get; set; }
    public string? HorDes           { get; set; }
    public string? HorCla           { get; set; }
}

// ── Tareo / HE / Autorizaciones ───────────────────────────────────────────

public class AuthHorasTareoDto
{
    // Identificación del día
    public string  FechaMarStr      { get; set; } = string.Empty;
    public string  DiaSemana        { get; set; } = string.Empty;
    public string  Descanso         { get; set; } = string.Empty;
    public string  Feriado          { get; set; } = string.Empty;

    // Marcaciones reales
    public string? Entrada          { get; set; }
    public string? Salida           { get; set; }
    public string? HoraEfectiva     { get; set; }

    // Alerta marca impar
    public string? Alerta01         { get; set; }   // "MI" = marca impar

    // HEA — horas extras antes de entrada (versión oficial)
    public string? HoraExtAntesOfi  { get; set; }
    public string  HayHeaPorAut     { get; set; } = "N";
    public string? AuthHeaHoras     { get; set; }
    public string? AuthHeaObs       { get; set; }
    public string? AuthHeaUsr       { get; set; }
    public string? DesAuthHeaHoras  { get; set; }

    // HED — horas extras después de salida (versión oficial)
    public string? HoraExtraOfi     { get; set; }
    public string  HayHedPorAut     { get; set; } = "N";
    public string? AuthHedHoras     { get; set; }
    public string? AuthHedObs       { get; set; }
    public string? AuthHedUsr       { get; set; }
    public string? DesAuthHedHoras  { get; set; }

    // HEO — horas dobles / descanso trabajado (versión oficial)
    public string? HoraDoblesOf     { get; set; }
    public string  HayHeoPorAut     { get; set; } = "N";
    public string? AuthHeoHoras     { get; set; }
    public string? AuthHeoObs       { get; set; }
    public string? AuthHeoUsr       { get; set; }
    public string? DesAuthHeoHoras  { get; set; }
}

// ── Grabar Autorización ───────────────────────────────────────────────────

// ── Resumen HE por empleado ───────────────────────────────────────────────

public class AuthHorasSupervisorDto
{
    public string CodUsuario  { get; set; } = string.Empty;
    public string NomUsuario  { get; set; } = string.Empty;
}

public class AuthHorasResumenDto
{
    public string CodPersonal     { get; set; } = string.Empty;
    public string NombreCompleto  { get; set; } = string.Empty;
    public string? NumFotocheck   { get; set; }
    public string CodCCostos      { get; set; } = string.Empty;
    public string DesCCostos      { get; set; } = string.Empty;
    public int    DiasConHe       { get; set; }
    public int    DiasPendientes  { get; set; }
    public int    DiasAutorizados { get; set; }
    public int    MinHed          { get; set; }
    public int    MinHea          { get; set; }
    public int    MinHeo          { get; set; }
    public int    MinHedAut       { get; set; }
    public int    MinHeaAut       { get; set; }
    public int    MinHeoAut       { get; set; }
    public string Estado          { get; set; } = string.Empty;  // SIN_HE | PENDIENTE | PARCIAL | COMPLETO
    public string? Obs            { get; set; }  // Campo único de observación (incluye prefijos [COLONIAL] [AQUARIUS])
    // Visto bueno del jefe de planta
    public string? IndVisado      { get; set; }   // 'S'=con visto bueno / 'N'=sin visto bueno / null=sin registrar
    public string? ObsVisado      { get; set; }
    public string? CodUsuVisado   { get; set; }
    public string? FecVisado      { get; set; }   // 'DD/MM/YYYY HH24:MI'
}

// ── Visado HE (visto bueno del jefe de planta) ────────────────────────────

public class AuthHorasVisadoItemRequest
{
    public string  CodPersonal { get; set; } = string.Empty;
    public string  IndVisado   { get; set; } = "S";   // "S" o "N"
    public string? ObsVisado   { get; set; }
}

public class AuthHorasGrabarVisadoRequest
{
    public string CodEmpresa  { get; set; } = string.Empty;
    public string Desde       { get; set; } = string.Empty;   // dd/MM/yyyy
    public string Hasta       { get; set; } = string.Empty;   // dd/MM/yyyy
    public List<AuthHorasVisadoItemRequest> Visados { get; set; } = new();
}

public class AuthHorasGrabarVisadoResult
{
    public bool   Ok        { get; set; }
    public string Mensaje   { get; set; } = string.Empty;
    public int    Guardados { get; set; }
}

// ── Grabar Autorización ───────────────────────────────────────────────────

public class AuthHorasGrabarRequest
{
    public string  CodEmpresa      { get; set; } = string.Empty;
    public string  CodPersonal     { get; set; } = string.Empty;
    public string  Fecha           { get; set; } = string.Empty;   // dd/MM/yyyy
    public string  Tipo            { get; set; } = string.Empty;   // 1..6
    public string  Valor           { get; set; } = string.Empty;   // HH:MI
    public string? Observaciones   { get; set; }
}

public class AuthHorasGrabarResult
{
    public bool   Ok      { get; set; }
    public string Mensaje { get; set; } = string.Empty;
}
