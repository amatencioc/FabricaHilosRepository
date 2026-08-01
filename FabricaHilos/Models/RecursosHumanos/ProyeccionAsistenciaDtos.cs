namespace FabricaHilos.Models.RecursosHumanos;

/// <summary>
/// Filtro de búsqueda para la Proyección de Asistencia por fecha.
/// </summary>
public class ProyeccionAsistenciaFiltroDto
{
    public DateTime Fecha       { get; set; } = DateTime.Today.AddDays(1);
    public string?  CodEmpresa  { get; set; }   // null/"" = todas las empresas
}

/// <summary>
/// Resumen agregado: cuántos empleados activos caerían en cada estado para la fecha
/// consultada (TRABAJARIA | DESCANSO | EVENTO | SIN_HORARIO).
/// Fuente: AQUARIUS.SP_AQ_PROYECCION_ASISTENCIA (cursor p_cur_resumen).
/// </summary>
public class ProyeccionResumenDto
{
    public string? Estado     { get; set; }   // TRABAJARIA | DESCANSO | EVENTO | SIN_HORARIO
    public int     Cantidad   { get; set; }
}

/// <summary>
/// Detalle por empleado del pronóstico de asistencia para una fecha específica.
/// Fuente: AQUARIUS.SP_AQ_PROYECCION_ASISTENCIA (cursor p_cur_detalle).
/// </summary>
public class ProyeccionEmpleadoDto
{
    public string? CodPersonal        { get; set; }
    public string? CodSpring          { get; set; }
    public string? NombreCompleto     { get; set; }
    public string? Empresa            { get; set; }
    public string? HorarioDescripcion { get; set; }   // HORDES
    public string? Turno              { get; set; }   // HORTUR del día
    public string? HoraIngresoTeorica { get; set; }   // HH:MM (SCA_HORARIO_DET.HORING)
    public string? HoraSalidaTeorica  { get; set; }   // HH:MM (SCA_HORARIO_DET.HORSAL)
    public string? HorasTrabajo       { get; set; }   // HH:MM (SCA_HORARIO_DET.TOTHORAS, jornada neta sin refrigerio)
    public decimal? HorasTrabajoNum   { get; set; }   // Decimal, para sumar/filtrar (ej. 8.75)
    public string? HoraIngresoReal    { get; set; }   // HH:MM real marcado (SCA_ASISTENCIA_TAREO.ENTRADA); NULL si el día aún no ocurrió
    public string? HoraSalidaReal     { get; set; }   // HH:MM real marcado (SCA_ASISTENCIA_TAREO.SALIDA); NULL si el día aún no ocurrió
    public string? HorasTrabajadasReal    { get; set; } // HH:MM real = salida - entrada; NULL si falta marcación
    public decimal? HorasTrabajadasRealNum { get; set; } // Decimal, para sumar/filtrar
    public string? Ccosto             { get; set; }   // PLA_PERSONAL.COD_C_COSTOS
    public string? CcostoNombre       { get; set; }   // SIG.CENTRO_DE_COSTOS.NOMBRE (fallback AQUARIUS.MAE_C_COSTOS.DES_C_COSTOS)
    public string? GranCcosto         { get; set; }   // SIG.CENTRO_DE_COSTOS.GRAN_CCOSTO
    public string? GranCcostoNombre   { get; set; }   // SIG.TABLAS_AUXILIARES(TIPO=83).DESCRIPCION
    public string? EncargadoNombre    { get; set; }   // Encargado de área del centro de costo (mismo mecanismo que PKG_MA_PROGRAMA)
    public string? Estado             { get; set; }   // TRABAJARIA | DESCANSO | EVENTO | SIN_HORARIO
    public string? EventoDescripcion  { get; set; }    // sólo cuando Estado = EVENTO
    public string? Feriado            { get; set; }    // 'F' si la fecha es feriado para ese empleado, si no null
}
