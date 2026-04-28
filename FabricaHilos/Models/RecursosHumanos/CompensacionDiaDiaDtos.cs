namespace FabricaHilos.Models.RecursosHumanos;

// ── Resultado de CALCULAR_HORAS_EVENTO (preview, solo lectura) ────────────────

public class CompensacionPreviewDto
{
    public string? CodPersonal          { get; set; }
    public string? NombreCompleto       { get; set; }
    public int     MinDisponibles       { get; set; }
    public string? HorasDisponibles     { get; set; }
    public int     MinJornadaDestino    { get; set; }
    public string? HorasJornadaDestino  { get; set; }
    public int     MinACompensar        { get; set; }
    public string? HorasACompensar      { get; set; }
    public int     MinSobrante          { get; set; }
    public string? HorasSobrante        { get; set; }
}

// ── Resultado de REGISTRAR_EVENTO_MASIVO ─────────────────────────────────────

public class CompensacionMasivoResultDto
{
    public string? CodPersonal          { get; set; }
    public string? NombreCompleto       { get; set; }
    public int     MinDisponibles       { get; set; }
    public string? HorasDisponibles     { get; set; }
    public int     MinJornadaDestino    { get; set; }
    public string? HorasJornadaDestino  { get; set; }
    public int     MinACompensar        { get; set; }
    public string? HorasACompensar      { get; set; }
    public int     MinSobrante          { get; set; }
    public string? HorasSobrante        { get; set; }
    public long?   IdCompen             { get; set; }
    public string? Estado               { get; set; }
    public string? Motivo               { get; set; }
    public int     SaldoBancoSemMin     { get; set; }
    public long    IdEvento             { get; set; }
}

// ── Resultado de CONSULTAR_RANGO ─────────────────────────────────────────────

public class CompensacionRangoDto
{
    public long?   IdCompen            { get; set; }
    public string? CodEmpresa          { get; set; }
    public string? CodPersonal         { get; set; }
    public string? FechaOrigen         { get; set; }
    public string? FechaDestino        { get; set; }
    public string? TipoOrigen          { get; set; }
    public string? TipoCompensacion    { get; set; }
    public int     TiempoMin           { get; set; }
    public string? TiempoHhMi          { get; set; }
    public string? Periodo             { get; set; }
    public string? DestAlerta02        { get; set; }
    public string? DestAlerta03        { get; set; }
    public string? DestAlerta04        { get; set; }
    public string? DestAlerta07        { get; set; }
    public string? DestAlerta09        { get; set; }
    public string? OriAlerta06         { get; set; }
    public string? OriAlerta08         { get; set; }
    public string? EstadoAplicacion    { get; set; }
}

// ── Resultado de LISTAR_EMPLEADOS_RANGO ──────────────────────────────────────

public class EmpleadoRangoDto
{
    public string? CodPersonal      { get; set; }
    public string? NombreCompleto   { get; set; }
    public string? FechamarStr      { get; set; }
    public int     MinTrabajadas    { get; set; }
    public string? HorasTrabajadas  { get; set; }
    public int     MinHe            { get; set; }
    public string? HorasHe        { get; set; }
    public int     MinDobles      { get; set; }
    public string? HorasDobles    { get; set; }
    public int     MinBanco       { get; set; }
    public string? HorasBanco     { get; set; }
    public int     MinTotal       { get; set; }
    public string? HorasTotal     { get; set; }
}

// ── Resultado de VER_ESTADO ───────────────────────────────────────────────────

public class CompensacionEstadoDto
{
    public long?   IdCompen            { get; set; }
    public string? CodEmpresa          { get; set; }
    public string? CodPersonal         { get; set; }
    public string? NomTrabajador       { get; set; }
    public string? ApePaterno          { get; set; }
    public string? ApeMaterno          { get; set; }
    public string? FechaOrigen         { get; set; }
    public string? FechaDestino        { get; set; }
    public string? TipoOrigen          { get; set; }
    public string? TipoCompensacion    { get; set; }
    public int     TiempoMin           { get; set; }
    public string? TiempoHhMi          { get; set; }
    public string? Periodo             { get; set; }

    // Tareo origen
    public string? OriHeAjus           { get; set; }
    public string? OriDobles           { get; set; }
    public string? OriBanco            { get; set; }
    public string? OriAlerta06         { get; set; }
    public string? OriAlerta08         { get; set; }

    // Tareo destino
    public string? DesTardanza         { get; set; }
    public string? DesAnteSalida       { get; set; }
    public string? DesNoTrab           { get; set; }
    public string? DesFalta            { get; set; }
    public string? DesPermiso          { get; set; }
    public string? DesAlerta02         { get; set; }
    public string? DesAlerta03         { get; set; }
    public string? DesAlerta04         { get; set; }
    public string? DesAlerta07         { get; set; }
    public string? DesAlerta09         { get; set; }
}
