namespace FabricaHilos.Models.RecursosHumanos;

public class EmpleadoDto
{
    public string? Personal  { get; set; }
    public string? Fotocheck { get; set; }
    public string? Empleado  { get; set; }
    public string? TipEstado { get; set; }
}

public class MarcacionRangoDto
{
    public string?  CodEmpresa        { get; set; }
    public string?  CodPersonal       { get; set; }
    public string?  Fotocheck         { get; set; }
    public int?     NumMarcaciones    { get; set; }

    // Marcaciones actuales
    public string?  Entrada           { get; set; }
    public string?  IniRefri          { get; set; }
    public string?  FinRefri          { get; set; }
    public string?  Salida            { get; set; }

    // Horario teórico (SCA_HORARIO_DET)
    public string?  HorDescripcion    { get; set; }
    public string?  HorClase          { get; set; }
    public string?  HorEntrada        { get; set; }
    public string?  HorIniRefri       { get; set; }
    public string?  HorFinRefri       { get; set; }
    public string?  HorSalida         { get; set; }
    public string?  HorTotalHrs       { get; set; }
    public string?  HorDescanso       { get; set; }

    // Horario fijado en tareo
    public string?  EntradaTeorica    { get; set; }
    public string?  IniRefriTeorico   { get; set; }
    public string?  FinRefriTeorico   { get; set; }
    public string?  SalidaTeorica     { get; set; }

    // Horas calculadas
    public string?  HrsBrutas         { get; set; }
    public string?  HrsRefrigerio     { get; set; }
    public string?  HrsEfectivas      { get; set; }
    public string?  Tardanza          { get; set; }
    public string?  HrsNocturnas      { get; set; }
    public string?  HrsNocturnasOf    { get; set; }
    public string?  TotHoras          { get; set; }
    public string?  HoraDobles        { get; set; }

    // Indicadores
    public string?  Descanso          { get; set; }
    public string?  Cerrado           { get; set; }
    public string?  Obrero            { get; set; }
    public string?  TipoEntrada       { get; set; }
    public string?  TipoSalida        { get; set; }

    // Permisos/ausencias
    public string?  DescMedico        { get; set; }
    public string?  Subsidio          { get; set; }
    public string?  PermGoce          { get; set; }
    public string?  PermSgoce         { get; set; }
    public string?  Vacaciones        { get; set; }
    public string?  Suspension        { get; set; }
    public string?  LicPaternidad     { get; set; }
    public string?  LicFallecimiento  { get; set; }

    // Horas extras
    public string?  HoraExtra         { get; set; }
    public string?  TotalHorasExtras  { get; set; }
    public string?  HoraExtraAjus     { get; set; }
    public string?  He25Pct           { get; set; }
    public string?  He35Pct           { get; set; }
    public string?  He50Pct           { get; set; }

    // Auditoría de depuración
    public string?  CodDepuracion     { get; set; }
    public string?  DescDepuracion    { get; set; }

    // Alertas
    public string?  Alerta01          { get; set; }
    public string?  Alerta06          { get; set; }

    // Verificación cruzada con SCA_HISTORIAL
    public int?     MarcasHistorial   { get; set; }

    // Columna extra de fecha (añadida en la vista via JOIN o param)
    public string?  Fechamar          { get; set; }
}

public class DepuraRangoResultadoDto
{
    public string? Resultado           { get; set; }
    public string? FechaInicio         { get; set; }
    public string? FechaFin            { get; set; }
    public int     TotalDias           { get; set; }
    public int     DiasOk              { get; set; }
    public int     DiasError           { get; set; }
    public int     TurnosNocturnos     { get; set; }
    public int     Entradas            { get; set; }
    public int     Anticipadas         { get; set; }
    public int     Salidas             { get; set; }
    public int     Inirefris           { get; set; }
    public int     Finrefris           { get; set; }
    public int     Anomalas            { get; set; }
    public int     NocturnosSinRefri   { get; set; }
    public int     Recalculos          { get; set; }
    public int     TotalGeneradas      { get; set; }
    public int     TotalHistorial      { get; set; }
}
