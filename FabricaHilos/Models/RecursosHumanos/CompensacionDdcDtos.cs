namespace FabricaHilos.Models.RecursosHumanos;

// ── Resultado de LISTAR_DDC_RANGO (solo lectura) ──────────────────────────────

public class DdcRangoFilaDto
{
    public string? CodPersonal      { get; set; }
    public string? NombreCompleto   { get; set; }
    public string? FechamarStr      { get; set; }
    public string? DiaSemana        { get; set; }
    public string? TipoDia          { get; set; }   // HE | DDC | DESCANSO
    public int     MinHe            { get; set; }
    public string? HorasHe          { get; set; }
    public int     MinFalta         { get; set; }
    public string? HorasFalta       { get; set; }
    public string? Alerta02         { get; set; }
    public string? Alerta06         { get; set; }
    public string? Descanso         { get; set; }
    public string? YaCompensado     { get; set; }
    public string? NumFotocheck     { get; set; }
}

// ── Resultado de CALCULAR_DDC (preview por DDC) ───────────────────────────────

public class DdcCalculoFilaDto
{
    public string? CodPersonal          { get; set; }
    public string? NombreCompleto       { get; set; }
    public string? FechaDdcStr          { get; set; }
    public string? DiaSemana            { get; set; }
    public int     MinFalta             { get; set; }
    public string? HorasFalta          { get; set; }
    public int     MinHeAsignadasSim    { get; set; }
    public string? HorasHeAsignadasSim  { get; set; }
    public int     MinFaltaRestanteSim  { get; set; }
    public string? HorasFaltaRestanteSim{ get; set; }
    public int     TotalHeRangoSim      { get; set; }
    public string? HorasTotalHeRangoSim { get; set; }
    public string? Estado               { get; set; }  // OK|PARCIAL|SIN_HE|ADVERTENCIA_REDONDEO
}

// ── Resultado de REGISTRAR_DDC_MASIVO (por DDC) ───────────────────────────────

public class DdcRegistroFilaDto
{
    public long?   IdEvento             { get; set; }
    public string? CodPersonal          { get; set; }
    public string? NombreCompleto       { get; set; }
    public string? FechaDdcStr          { get; set; }
    public string? DiaSemana            { get; set; }
    public int     MinFaltaTotal        { get; set; }
    public string? HorasFaltaTotal      { get; set; }
    public int     MinHeAsignadas       { get; set; }
    public string? HorasHeAsignadas     { get; set; }
    public int     MinFaltaRestante     { get; set; }
    public string? HorasFaltaRestante   { get; set; }
    public string? Estado               { get; set; }  // OK|PARCIAL|SIN_HE|ERR|ADVERTENCIA_REDONDEO
    public string? Motivo               { get; set; }
}

// ── Resultado de CONSULTAR_RANGO_DDC ─────────────────────────────────────────

public class DdcRangoConsultaDto
{
    public long?   IdCompen         { get; set; }
    public string? CodEmpresa       { get; set; }
    public string? CodPersonal      { get; set; }
    public string? FechaOrigenStr   { get; set; }
    public string? FechaDestinoStr  { get; set; }
    public string? TipoOrigen       { get; set; }
    public string? TipoCompensacion { get; set; }
    public int     TiempoMin        { get; set; }
    public string? TiempoHhMi       { get; set; }
    public string? Aux1             { get; set; }
    public string? OriAlerta06      { get; set; }
    public string? DestAlerta02     { get; set; }
}

// ── Resultado de CONSULTAR_EVENTO_DDC ────────────────────────────────────────

public class DdcEventoFilaDto
{
    public long?   IdCompen         { get; set; }
    public string? CodEmpresa       { get; set; }
    public string? CodPersonal      { get; set; }
    public string? NombreCompleto   { get; set; }
    public string? FechaOrigenStr   { get; set; }
    public string? FechaDestinoStr  { get; set; }
    public string? TipoOrigen       { get; set; }
    public string? TipoCompensacion { get; set; }
    public int     TiempoMin        { get; set; }
    public string? TiempoHhMi       { get; set; }
    public string? EstadoAplicacion { get; set; }
    public string? OriAlerta06      { get; set; }
    public string? DestAlerta02     { get; set; }
}
