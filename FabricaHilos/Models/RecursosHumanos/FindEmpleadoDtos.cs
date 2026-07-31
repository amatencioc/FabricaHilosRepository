namespace FabricaHilos.Models.RecursosHumanos;

/// <summary>
/// v1.7 — Sugerencia de empleado para el autocompletado de búsqueda por Nombre
/// (mismo patrón que AQUARIUS.sp_SCA_Read_Personal_AutCom: PLA_PERSONAL activos,
/// LIKE sobre "APELLIDOS, Nombre"). Se usa para listar coincidencias mientras el
/// usuario escribe, antes de disparar la búsqueda completa por CODIGO (exacta).
/// </summary>
public class SugerenciaEmpleadoDto
{
    public string? CodPersonal { get; set; }
    public string? CodSpring   { get; set; }
    public string? Nombre      { get; set; }   // "APELLIDOS, Nombre"
}

/// <summary>
/// Resultado de búsqueda unificada de empleado: identificación + asistencia de HOY
/// (AQUARIUS.SCA_ASISTENCIA_TAREO) + evento activo EN VIVO (SIG.RH_EVENTOS).
/// Fuente: AQUARIUS.SP_FIND_EMPLEADO_EVENTO_REAL
/// </summary>
public class EmpleadoConEventoRealDto
{
    // Identificación
    public string? CodAquarius     { get; set; }
    public string? CodSig          { get; set; }
    public string? NombreCompleto  { get; set; }
    public string? Dni             { get; set; }
    public string? Empresa         { get; set; }

    // v1.8 — Horario/Turno vigente (AQUARIUS.SCA_HORARIO_PERSONAL + SCA_HORARIO_CAB)
    public string? HorarioDescripcion { get; set; }   // HORDES, ej. "TERCER TURNO 23:00-07:00"
    public string? HorarioTurno       { get; set; }   // HORTUR, ej. "T1" | "T2" | "T3"

    // Asistencia HOY (AQUARIUS.SCA_ASISTENCIA_TAREO)
    public string? EstadoAsistenciaHoy { get; set; }   // PRESENTE | FALTA | NO MARCÓ | null
    public string? HoraEntrada         { get; set; }
    public string? HoraSalida          { get; set; }

    // Evento HOY (SIG.RH_EVENTOS EN VIVO)
    public string? EventoDescripcion  { get; set; }    // SIN EVENTO | VENTA VACACIONES | ...
    public string? EventoTipoCodigo   { get; set; }    // 54 | 56 | 57 | 23 | ...
    public string? EventoFechas       { get; set; }    // "DD/MM/YYYY - DD/MM/YYYY"
    public string? EventoObservacion  { get; set; }

    // Marcación Vigilancia HOY (SIG.SI_REGPERS EN VIVO) — v1.2
    // Fuente inmediata mientras AQUARIUS.SCA_ASISTENCIA_TAREO se sincroniza (batch diferido).
    public string? VigilanciaEstado    { get; set; }   // AÚN EN PLANTA | YA SALIÓ | SIN REGISTRO VIGILANCIA HOY
    public string? VigilanciaEntrada   { get; set; }
    public string? VigilanciaSalida    { get; set; }
    public string? VigilanciaAlcohol   { get; set; }    // NEGATIVO | POSITIVO | NO REALIZADA
    public string? VigilanciaCelular   { get; set; }    // SÍ | NO
    public string? VigilanciaFuente    { get; set; }

    // v1.3 — Rango efectivo aplicado (echo del rango usado por el SP: explícito o default)
    public string? RangoVigilanciaDesde { get; set; }   // default = HOY si no se envía rango
    public string? RangoVigilanciaHasta { get; set; }
    public string? RangoEventosDesde    { get; set; }   // default = mes anterior si no se envía rango
    public string? RangoEventosHasta    { get; set; }   // default = mes siguiente si no se envía rango

    // v1.3 — Detalle completo: TODOS los registros de SIG.SI_REGPERS en el rango
    public List<VigilanciaRegistroDto> VigilanciaRegistros { get; set; } = new();

    // v1.3 — Historial de eventos SIG.RH_EVENTOS en el rango (mes ant/actual/sig por defecto)
    public List<EventoHistorialDto> EventosHistorial { get; set; } = new();

    // v1.5 — TODAS las compensaciones (AQUARIUS.SCA_COMPENSACION) en el rango efectivo de Eventos
    public List<CompensacionDto> Compensaciones { get; set; } = new();

    // Auditoría (trazabilidad de origen de datos)
    public string? FuenteAsistencia    { get; set; } = "AQUARIUS.SCA_ASISTENCIA_TAREO";
    public string? FuenteEvento        { get; set; }
    public string? NotaSincronizacion  { get; set; }
}

/// <summary>
/// Fila de detalle de SIG.SI_REGPERS — tabla donde Vigilancia graba EN VIVO el
/// ingreso/salida de personal (biométrico/portería).
/// v1.4: se retiraron columnas de auditoría/técnicas sin valor para RRHH
/// (ActivoFijo, Datec1-5, Detalle, UsuarioRegistro/FechaRegistro/UsuarioModifico/
/// FechaModifico, SalidaFlag) y se reordenó para coincidir con el cursor del SP.
/// </summary>
public class VigilanciaRegistroDto
{
    public string? Tipo              { get; set; }   // 'T'=Trabajador | 'V'=Visita
    public string? CodSig            { get; set; }   // C_CODIGO
    public string? DocId             { get; set; }
    public string? Nombre            { get; set; }
    public string? DniRuc            { get; set; }
    public string? CentroCosto       { get; set; }   // C_COSTO
    public string? TipoCp            { get; set; }
    public string? FechaIngreso      { get; set; }   // FECHAI (DD/MM/YYYY HH24:MI)
    public string? FechaSalida       { get; set; }   // FECHAF (DD/MM/YYYY HH24:MI)
    public string? TraeCelular       { get; set; }   // S/N
    public string? GuardaCelular     { get; set; }   // S/N
    public string? NroBlock          { get; set; }
    public string? TestAlcohol       { get; set; }   // S/N
    public string? ResultadoAlcohol  { get; set; }   // S/N
    public string? Observacion       { get; set; }
}

/// <summary>
/// Fila de historial de SIG.RH_EVENTOS (permisos, vacaciones, descansos, etc.)
/// dentro del rango efectivo (mes anterior/actual/siguiente por defecto).
/// </summary>
public class EventoHistorialDto
{
    public string? TipoCodigo    { get; set; }
    public string? Descripcion   { get; set; }
    public string? FechaInicio   { get; set; }
    public string? FechaFinal    { get; set; }
    public string? Observacion   { get; set; }
    public bool    NoSincroniza  { get; set; }   // true si el tipo NO sincroniza a AQUARIUS (54/56/57)
}

/// <summary>
/// Fila de AQUARIUS.SCA_COMPENSACION — traslado de tiempo (minutos) desde un día/concepto
/// ORIGEN (donde sobra: HE/dobles/banco) hacia un día/concepto DESTINO (tardanza/falta/
/// permiso/etc.) o al banco de horas. v1.5. v1.6: + Descripcion en lenguaje natural.
/// </summary>
public class CompensacionDto
{
    public string? IdCompen              { get; set; }
    public string? FechaOrigen           { get; set; }
    public string? TipoOrigen            { get; set; }        // E/D/B/I
    public string? TipoOrigenDesc        { get; set; }        // HORAS EXTRAS | HORAS DOBLES | BANCO DE HORAS | INTERCAMBIO (BANCO)
    public string? FechaDestino          { get; set; }        // null cuando tipocompensacion='I'
    public string? TipoCompensacion      { get; set; }        // A/T/N/F/P/I
    public string? TipoCompensacionDesc  { get; set; }        // HORAS ANTES DE SALIDA | TARDANZA | ...
    public string? TiempoHhMm            { get; set; }        // HH:MM
    public string? Aux1                  { get; set; }        // 'M'+id_evento (ref. evento masivo) O periodo banco si tipocompensacion='I'
    public string? Descripcion           { get; set; }        // v1.6: frase legible que explica la compensación completa
}

