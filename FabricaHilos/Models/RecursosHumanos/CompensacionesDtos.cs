namespace FabricaHilos.Models.RecursosHumanos;

public class EmpleadoDisponibleDto
{
    public string? CodPersonal  { get; set; }
    public string? Fotocheck    { get; set; }
    public string? Nombre       { get; set; }
    public int     MinutosHe    { get; set; }
    public int     MinutosDoble { get; set; }
    public int     MinutosBanco { get; set; }
    public string? HhmiHe       { get; set; }
    public string? HhmiDoble    { get; set; }
    public string? HhmiBanco    { get; set; }
}

public class CompensacionRangoDto
{
    public int?    IdCompen          { get; set; }
    public string? CodEmpresa        { get; set; }
    public string? CodPersonal       { get; set; }
    public string? FechaOrigen       { get; set; }
    public string? FechaDestino      { get; set; }
    public string? TipoOrigen        { get; set; }
    public string? TipoCompensacion  { get; set; }
    public int?    TiempoMin         { get; set; }
    public string? TiempoHhmi        { get; set; }
    public string? Periodo           { get; set; }

    // Alertas destino
    public string? DestAlerta02      { get; set; }
    public string? DestAlerta03      { get; set; }
    public string? DestAlerta04      { get; set; }
    public string? DestAlerta07      { get; set; }
    public string? DestAlerta09      { get; set; }

    // Alertas origen
    public string? OriAlerta06       { get; set; }
    public string? OriAlerta08       { get; set; }

    public string? EstadoAplicacion  { get; set; }
}

public class CompensacionRegistrarDto
{
    public string? Estado        { get; set; }
    public string? Motivo        { get; set; }
    public int?    IdCompen      { get; set; }
    public int?    TiempoMinutos { get; set; }
}

public class CompensacionEliminarDto
{
    public string? Estado          { get; set; }
    public string? Motivo          { get; set; }
    public int?    FilasEliminadas { get; set; }
}

public class CompensacionValidarDto
{
    public string? PuedeAplicar               { get; set; }
    public string? Motivo                     { get; set; }
    public int?    TiempoSolicitadoMin        { get; set; }
    public int?    TiempoDisponibleOrigenMin  { get; set; }
    public int?    TiempoDeficitDestinoMin    { get; set; }
    public string? TipoValidacion             { get; set; }
}

public class AplicarDiaResultadoDto
{
    public string? Fecha              { get; set; }
    public string? CodEmpresa         { get; set; }
    public string? CodPersonal        { get; set; }
    public int?    AplicadasDestino   { get; set; }
    public int?    AplicadasOrigen    { get; set; }
    public int?    Eliminadas         { get; set; }
    public int?    Errores            { get; set; }
}

public class AplicarRangoResultadoDto
{
    public string? FechaInicio            { get; set; }
    public string? FechaFin               { get; set; }
    public string? CodEmpresa             { get; set; }
    public string? CodPersonal            { get; set; }
    public int?    DiasProcesados         { get; set; }
    public int?    TotalAplicadasDestino  { get; set; }
    public int?    TotalAplicadasOrigen   { get; set; }
    public int?    TotalEliminadas        { get; set; }
    public int?    TotalErrores           { get; set; }
}

public class SaldoBancoDto
{
    public string? TipoBanco   { get; set; }
    public string? AnoProceso  { get; set; }
    public string? MesProceso  { get; set; }
    public string? SemProceso  { get; set; }
    public int?    SaldoMin    { get; set; }
    public string? SaldoHhmi   { get; set; }
}

public class CompensacionJobResultadoDto
{
    public string? Resultado   { get; set; }
    public string? FechaInicio { get; set; }
    public string? FechaFin    { get; set; }
    public int?    Aplicadas   { get; set; }
    public int?    Eliminadas  { get; set; }
    public int?    Errores     { get; set; }
}

public class DiagnosticoDiaDto
{
    public string? Fecha               { get; set; }
    public string? CodEmpresa          { get; set; }
    public string? CodPersonal         { get; set; }

    // Origen (disponible para ceder)
    public int     HeMin               { get; set; }
    public string? HeHhmi              { get; set; }
    public int     DoblesMin           { get; set; }
    public string? DoblesHhmi          { get; set; }
    public int     BancoMin            { get; set; }
    public string? BancoHhmi           { get; set; }

    // Destino (déficit a cubrir)
    public int     TardMin             { get; set; }
    public string? TardHhmi            { get; set; }
    public int     AntesMin            { get; set; }
    public string? AntesHhmi           { get; set; }
    public int     FaltaMin            { get; set; }
    public string? FaltaHhmi           { get; set; }
    public int     NotrabMin           { get; set; }
    public string? NotrabHhmi          { get; set; }
    public int     PermisoMin          { get; set; }
    public string? PermisoHhmi         { get; set; }

    // Compensaciones
    public int     CompenRegistradas   { get; set; }
    public int     CompenAplicadas     { get; set; }

    // Flags
    public string? TieneOrigen         { get; set; }
    public string? TieneDeficit        { get; set; }
    public string? EsDescanso          { get; set; }
    public string? EsFeriado           { get; set; }
    public string? Alerta01            { get; set; }

    // Sugerencia automática
    public string? Sugerencia          { get; set; }
}

public class RegistrarEventoResultadoDto
{
    public string? FechaOrigen          { get; set; }
    public string? FechaDestino         { get; set; }
    public int     EmpleadosEncontrados { get; set; }
    public int     RegistradasOk        { get; set; }
    public int     AplicadasOk          { get; set; }
    public int     Errores              { get; set; }
    public string? Estado               { get; set; }
}

public class AplicarRangoMasivoResultadoDto
{
    public string? FechaInicio              { get; set; }
    public string? FechaFin                 { get; set; }
    public int     EmpleadosProcesados      { get; set; }
    public int     TotalAplicadasDestino    { get; set; }
    public int     TotalAplicadasOrigen     { get; set; }
    public int     TotalEliminadas          { get; set; }
    public int     TotalErrores             { get; set; }
}
