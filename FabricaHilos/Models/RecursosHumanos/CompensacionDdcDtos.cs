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
    public int     NumMarcaciones   { get; set; }
    // Solo para tipo_dia = 'BLOQ_LOGIX'
    public string? LogixCmotivo     { get; set; }  // Código de motivo LOGIX (C_TIPO='07')
    public string? LogixDinicio     { get; set; }  // Fecha inicio evento LOGIX (dd/MM/yyyy)
    public string? LogixDfinal      { get; set; }  // Fecha fin evento LOGIX   (dd/MM/yyyy)
    public string? LogixDescMotivo  { get; set; }  // Descripción del motivo LOGIX
    // Disponible en LISTAR_DDC_RANGO y LISTAR_HE_PERSONAL (no nulo solo para filas tipo 'HE')
    public string? DescAlerta06     { get; set; }  // Descripción legible de alerta06
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
    public long?   IdCompen          { get; set; }
    public string? CodEmpresa        { get; set; }
    public string? CodPersonal       { get; set; }
    public string? NumFotocheck      { get; set; }
    public string? NombreCompleto    { get; set; }
    public string? FechaOrigenStr    { get; set; }
    public string? FechaDestinoStr   { get; set; }
    public string? TipoOrigen        { get; set; }
    public string? TipoCompensacion  { get; set; }
    public int     TiempoMin         { get; set; }
    public string? TiempoHhMi        { get; set; }
    public string? Evento            { get; set; }   // aux1 LIKE 'D%'
    public string? OriAlerta06       { get; set; }   // EC=HE consumidas, EE=HE existentes
    public string? OriHeActual       { get; set; }   // HH:MI actual en tareo origen
    public string? DestAlerta02      { get; set; }   // FC=compensado, FT=pendiente
    public string? DestFaltaActual   { get; set; }   // horas_falta actual en tareo destino
    public string? DestHefecActual   { get; set; }   // horaefectiva actual en tareo destino
    /// <summary>Derivado de dest_alerta02: FC → APLICADA, otro → PENDIENTE.</summary>
    public string? EstadoAplicacion  { get; set; }
}

// ── Resultado de CONSULTAR_EVENTO_DDC ────────────────────────────────────────

public class DdcEventoFilaDto
{
    public long?   IdCompen          { get; set; }
    public string? CodEmpresa        { get; set; }
    public string? CodPersonal       { get; set; }
    public string? NombreCompleto    { get; set; }
    public string? FechaOrigenStr    { get; set; }
    public string? FechaDestinoStr   { get; set; }
    public string? TipoCompensacion  { get; set; }
    public int     TiempoMin         { get; set; }
    public string? TiempoHhMi        { get; set; }
    public string? OriAlerta06       { get; set; }   // EC=HE consumidas, EE=HE existentes
    public string? OriHeActual       { get; set; }   // HH:MI actual en tareo origen
    public string? DestAlerta02      { get; set; }   // FC=compensado, FT=pendiente
    public string? DestFaltaActual   { get; set; }   // horas_falta actual en tareo destino
    public string? DestHefecActual   { get; set; }   // horaefectiva actual en tareo destino
}

// ── Resultado de CONSULTAR_COMP_DDC (fila individual por id_compen) ───────────

public class DdcCompFilaDto
{
    public long?   IdCompen          { get; set; }
    public string? CodEmpresa        { get; set; }
    public string? CodPersonal       { get; set; }
    public string? NombreCompleto    { get; set; }
    public string? FechaOrigenStr    { get; set; }
    public string? FechaDestinoStr   { get; set; }
    public string? TipoCompensacion  { get; set; }
    public int     TiempoMin         { get; set; }
    public string? TiempoHhMi        { get; set; }
    public long?   IdEvento          { get; set; }   // extraído de aux1 (SUBSTR(aux1,2))
    public string? OriAlerta06       { get; set; }
    public string? OriHeActual       { get; set; }
    public string? DestAlerta02      { get; set; }
    public string? DestFaltaActual   { get; set; }
    public string? DestHefecActual   { get; set; }
}
