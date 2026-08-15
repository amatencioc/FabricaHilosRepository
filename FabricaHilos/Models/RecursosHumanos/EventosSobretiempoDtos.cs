namespace FabricaHilos.Models.RecursosHumanos;

// ── FILTRO ───────────────────────────────────────────────────────────────────

public class EventosSobretiempoFiltroDto
{
    public int     AnoIni       { get; set; }
    public int     MesIni       { get; set; }
    public int     AnoFin       { get; set; }
    public int     MesFin       { get; set; }
    public string  Tipo         { get; set; } = "T"; // E=Empleados, O=Obreros, T=Todos
    public string? GranCcosto   { get; set; }        // V_CENTRO_DE_COSTOS.GRAN_CCOSTO (opcional)
    public string? CentroCosto  { get; set; }        // V_CENTRO_DE_COSTOS.CCOSTO_DET (opcional)
}

// ── CATÁLOGOS Gran Centro de Costo / Centro de Costo (filtro jerárquico,
//    mismo patrón usado en Capacitacion/Admin/Reportes) — fuente: SIG.V_CENTRO_DE_COSTOS ─

public class GranCcostoOptionDto
{
    public string Codigo      { get; set; } = string.Empty; // GRAN_CCOSTO
    public string Descripcion { get; set; } = string.Empty; // DESC_GRAN_CCOSTO
}

public class CentroCostoOptionDto
{
    public string Codigo          { get; set; } = string.Empty; // CCOSTO_DET
    public string Descripcion     { get; set; } = string.Empty; // DESC_CCOSTO_DET
    public string GranCcosto      { get; set; } = string.Empty; // GRAN_CCOSTO (para agrupar/filtrar en cascada)
    public string DescGranCcosto  { get; set; } = string.Empty; // DESC_GRAN_CCOSTO (label del <optgroup>)
}

// ── FILA POR (ÁREA, AÑO, MES) — combina sobretiempo (SIG) + eventos (AQUARIUS) ─

public class EventosSobretiempoAreaMesDto
{
    public int    Ano  { get; set; }
    public int    Mes  { get; set; }
    public string Area { get; set; } = string.Empty;

    // Sobretiempo — PKG_RPT_EVENTOS_SOBRETIEMPO.SP_RESUMEN_AREA (SIG).
    // "HorasExtras" es SOLES (SUM(INGRE_PLA.VALOR_CAL)), no horas reales — mismo
    // criterio ya usado en producción por /RecursosHumanos/HorasExtras.
    public int     TotalTrabajadores    { get; set; }

    // Horas de producción (turno regular) — AQUARIUS PlanillaResumenDto.HorasEfectivas,
    // suma de HorasEfectivas de todos los empleados del área/mes. Todo lo que exceda esta
    // cantidad de horas contractuales ya se paga como HE (sobretiempo).
    public decimal HorasProduccion      { get; set; }

    // Monto en soles de las horas de producción (SIG — SUM(INGRE_PLA.VALOR_CAL) del
    // concepto 1001/BASICO), mismo criterio que TotalHorasExtras para la HE.
    public decimal MontoProduccion      { get; set; }
    public decimal TotalHorasExtras     { get; set; }

    // Horas reales de sobretiempo trabajadas (SIG — SUM(INGRE_PLA.VALOR_ORI)), a
    // diferencia de TotalHorasExtras que es el monto pagado en soles.
    public decimal HorasHe              { get; set; }
    public decimal He25                 { get; set; }
    public decimal He35                 { get; set; }
    public decimal He100                { get; set; }
    public decimal PctTotalHorasExtras  { get; set; }
    public int     TrabajadoresConHe    { get; set; }

    // Eventos — AQUARIUS.SP_SCA_RESUMENTAREO_SIGLIVE (vía IPlanillaMensualService,
    // sin modificar) + resolución de área AQUARIUS→SIG.
    public int TrabajadoresConEvento { get; set; }
    public int DiasEvento            { get; set; }

    // HE por Evento vs HE por Necesidad — AQUARIUS.SCA_ASISTENCIA_TAREO día a día
    // (PKG_RPT_EVENTOS_SOBRETIEMPO.SP_HE_DIARIO_AQUARIUS, clasificado en
    // ReporteEventosSobretiempoService): si un día hubo algún evento en el área,
    // TODO el HE de ese día en esa área se considera "por Evento" (cobertura de un
    // ausente); si no hubo evento ese día, el HE es "por Necesidad" (producción pura).
    // MontoHeEvento/MontoHeNecesidad reparten TotalHorasExtras (soles, mensual, SIG)
    // proporcionalmente a las horas AQUARIUS de cada bucket, porque el monto en soles
    // no tiene fecha por día.
    public decimal HorasHeEvento      { get; set; }
    public decimal HorasHeNecesidad   { get; set; }
    public decimal MontoHeEvento      { get; set; }
    public decimal MontoHeNecesidad   { get; set; }
}

// ── FILA POR (GRAN CENTRO DE COSTO, CENTRO DE COSTO, AÑO, MES) — nivel intermedio
//    del drill-down cuando el filtro Gran Centro de Costo = "Todos" ─────────────

public class EventosSobretiempoCentroCostoMesDto
{
    public int    Ano          { get; set; }
    public int    Mes          { get; set; }
    public string GranCcosto   { get; set; } = string.Empty; // Gran Centro de Costo (desc.)
    public string CentroCosto  { get; set; } = string.Empty; // Centro de Costo (desc.)

    public int     TotalTrabajadores    { get; set; }
    public decimal HorasProduccion      { get; set; }
    public decimal MontoProduccion      { get; set; }
    public decimal TotalHorasExtras     { get; set; }
    public decimal HorasHe              { get; set; }
    public decimal He25                 { get; set; }
    public decimal He35                 { get; set; }
    public decimal He100                { get; set; }
    public int     TrabajadoresConEvento { get; set; }
    public int     DiasEvento            { get; set; }
    // HE por Evento vs HE por Necesidad — ver comentario en EventosSobretiempoAreaMesDto.
    public decimal HorasHeEvento    { get; set; }
    public decimal HorasHeNecesidad { get; set; }
    public decimal MontoHeEvento    { get; set; }
    public decimal MontoHeNecesidad { get; set; }}

// ── FILA POR (EMPLEADO, ÁREA, AÑO, MES) — detalle para el drill-down por área ──

public class EventosSobretiempoEmpleadoDto
{
    public int    Ano          { get; set; }
    public int    Mes          { get; set; }
    public string Area         { get; set; } = string.Empty;
    public string CodEmpleado  { get; set; } = string.Empty; // SIG.PLANILLA.C_CODIGO = AQUARIUS.PLA_PERSONAL.COD_SPRING
    public string NomEmpleado  { get; set; } = string.Empty;

    // Gran Centro de Costo REAL de esta fila (Ano/Mes/Empleado), independiente del
    // criterio de "Area" (que puede mostrar el Centro de Costo cuando ya se filtró por
    // Gran Centro de Costo) — usado para restringir el Sub Centro de Costo (ver abajo)
    // al Gran Centro de Costo MANTENIMIENTO real de ESTA fila, evitando que un empleado
    // que perteneció a Mantenimiento en otra fecha "contamine" el agrupamiento de otra
    // Área en la que aparece (ej. por evento) en el período consultado.
    public string? GranCcostoDesc { get; set; }

    // Horas de producción (turno regular) — AQUARIUS PlanillaResumenDto.HorasEfectivas.
    public decimal HorasProduccion  { get; set; }

    // Monto en soles de las horas de producción (SIG — PKG_RPT_EVENTOS_SOBRETIEMPO.
    // SP_DETALLE_SOBRETIEMPO.MONTO_PRODUCCION, concepto 1001/BASICO).
    public decimal MontoProduccion  { get; set; }

    // Sobretiempo — PKG_RPT_EVENTOS_SOBRETIEMPO.SP_DETALLE_SOBRETIEMPO (SIG).
    public decimal TotalHorasExtras { get; set; }
    public decimal He25             { get; set; }
    public decimal He35             { get; set; }
    public decimal He100            { get; set; }

    // Horas reales de sobretiempo trabajadas (SUM(INGRE_PLA.VALOR_ORI)).
    public decimal HorasHe          { get; set; }

    // HE por Evento vs HE por Necesidad — ver comentario en EventosSobretiempoAreaMesDto.
    // v2.1 (14/08/2026): a diferencia de la nota de diseño original (NO se agregaba a nivel
    // Empleado porque "cualquier evento en el Área contamina todo el HE del día"), el usuario
    // pidió exponerlo también acá. Se calcula por Empleado (heEmpAcc, en el Service) usando la
    // MISMA regla de contaminación por día+Área+Centro de Costo, y Centro de Costo se arma
    // sumando estos 4 campos desde el detalle por Empleado (no de forma independiente), para
    // que Empleado sea la fuente de verdad y siempre cuadre con los niveles superiores.
    public decimal HorasHeEvento    { get; set; }
    public decimal HorasHeNecesidad { get; set; }
    public decimal MontoHeEvento    { get; set; }
    public decimal MontoHeNecesidad { get; set; }

    // Eventos — AQUARIUS.SP_SCA_RESUMENTAREO_SIGLIVE (vía IPlanillaMensualService).
    public int    DiasEvento  { get; set; }
    public string DescEventos { get; set; } = string.Empty; // ej. "Vacaciones: 3, D. Médico: 1"

    // Situación laboral — AQUARIUS.PLA_PERSONAL (TIP_ESTADO/FEC_INGRESO/FEC_CESADO).
    // CesadoEnPeriodo = true cuando FecCese cae dentro de (Ano, Mes) de esta fila.
    public DateTime? FecIngreso     { get; set; }
    public DateTime? FecCese        { get; set; }
    public bool      CesadoEnPeriodo { get; set; }

    // Puesto (SIG.T_CARGO.DESCRIPCION) y su Sub Centro de Costo derivado (presentacional,
    // solo vistas — ver ResolverSubCentroCosto). Por ahora solo se llena para Gran Centro
    // de Costo MANTENIMIENTO (alcance inicial pedido por el usuario, 13/08/2026).
    public string? Puesto         { get; set; }
    public string? SubCentroCosto { get; set; }

    // Centro de Costo (desc.) del empleado — siempre poblado (independiente del filtro
    // aplicado), para el nivel intermedio de drill-down Gran Centro de Costo → Centro de
    // Costo → Empleado cuando el filtro Gran Centro de Costo = "Todos".
    public string? CentroCosto { get; set; }

    // HE trabajada en OTRO Centro de Costo distinto al de PLA_COSTO (SIG.HORAS_PLA) —
    // solo informativo (20/08/2026), no reasigna Área/Centro de Costo del reporte.
    public decimal HorasHeOtroCc { get; set; }
    public string? DescHeOtroCc  { get; set; } // ej. "VIGILANCIA (270): 12h; MANTENIMIENTO HILANDERIA (P740): 10h"

    // HE ya pagada en planilla (SIG.INGRE_PLA.VALOR_ORI vía INTERFACE_ASSITIME) sin día
    // identificable en AQUARIUS.SCA_ASISTENCIA_TAREO ese mes — solo informativo (14/08/2026),
    // ya está incluida en HorasHeNecesidad (no se resta ni se reclasifica).
    public decimal HorasHeSinEvidencia { get; set; }
}

// ── RESUMEN GLOBAL POR (AÑO, MES) ────────────────────────────────────────────

public class EventosSobretiempoResumenMesDto
{
    public int     Ano  { get; set; }
    public int     Mes  { get; set; }
    public decimal HorasProduccion       { get; set; }
    public decimal MontoProduccion       { get; set; }
    public decimal TotalHorasExtras      { get; set; }
    public decimal HorasHe               { get; set; }
    public decimal He25                  { get; set; }
    public decimal He35                  { get; set; }
    public decimal He100                 { get; set; }
    public int     TotalTrabajadores     { get; set; }
    public int     TrabajadoresConEvento { get; set; }
    public int     DiasEvento            { get; set; }

    // HE por Evento vs HE por Necesidad — ver comentario en EventosSobretiempoAreaMesDto.
    public decimal HorasHeEvento    { get; set; }
    public decimal HorasHeNecesidad { get; set; }
    public decimal MontoHeEvento    { get; set; }
    public decimal MontoHeNecesidad { get; set; }
}

// ── PROYECCIÓN DE BOLSA DE HE POR ÁREA — promedio mensual de HE "por Necesidad"
//    (producción pura, sin eventos) en el rango consultado: es la referencia que
//    un encargado de área puede usar para presupuestar su bolsa mensual de HE,
//    separada del HE "por Evento" (cobertura de ausencias, no proyectable igual
//    porque depende de cuántas ausencias haya cada mes) ─────────────────────────

public class ProyeccionBolsaHeDto
{
    public string  Area                 { get; set; } = string.Empty;
    public int     MesesConsiderados    { get; set; }
    public decimal HorasHeNecesidadProm { get; set; }
    public decimal MontoHeNecesidadProm { get; set; }
    public decimal HorasHeEventoProm    { get; set; }
    public decimal MontoHeEventoProm    { get; set; }
}

// Igual que ProyeccionBolsaHeDto pero por (Gran Centro de Costo, Centro de Costo) —
// v2.5 (20/08/2026), a pedido del usuario: la misma proyección de bolsa mensual pero
// a un nivel más fino que Área (ej. distinguir MANTENIMIENTO HILANDERIA de MANTENIMIENTO
// TINTORERIA dentro del Gran Centro de Costo MANTENIMIENTO). Se agrega sobre vm.CentrosCosto,
// mismo criterio (promedio mensual de HE Necesidad/Evento en el rango consultado).
public class ProyeccionBolsaHeCentroCostoDto
{
    public string  GranCcosto           { get; set; } = string.Empty;
    public string  CentroCosto          { get; set; } = string.Empty;
    public int     MesesConsiderados    { get; set; }
    public decimal HorasHeNecesidadProm { get; set; }
    public decimal MontoHeNecesidadProm { get; set; }
    public decimal HorasHeEventoProm    { get; set; }
    public decimal MontoHeEventoProm    { get; set; }
}

// ── DETALLE DÍA A DÍA POR EMPLEADO (v2.5, 20/08/2026) — desagregación del HE Evento/
//    Necesidad mensual del empleado: expone, día por día, las horas HE que AQUARIUS
//    registró y si ese día contaminó el pool como "Evento" (HuboEventoPool) y si el
//    evento fue del propio empleado (TieneEventoPropio) o de un compañero (cobertura).
//    Referencial (universo AQUARIUS, no necesariamente cuadra 1:1 con el HE oficial SIG
//    de ese día puntual — ver comentario en RepartirHorasHe) — sirve para VALIDAR/auditar
//    de dónde sale la clasificación mensual, no reemplaza el total oficial mostrado arriba.
public class EventosSobretiempoDiaEmpleadoDto
{
    public int      Ano              { get; set; }
    public int      Mes              { get; set; }
    public DateTime Fecha            { get; set; }
    public string   CodEmpleado      { get; set; } = string.Empty; // COD_SPRING
    public decimal  HorasHe          { get; set; } // horas HE ese día según AQUARIUS (crudo)
    public bool     HuboEventoPool   { get; set; } // true = ese día se clasificó "por Evento" (pool día+Área+CC[+Especialidad])
    public bool     TieneEventoPropio { get; set; } // true = el evento/falta de ESE día es del propio empleado, no de un compañero
}

// ── CONSOLIDADO DE EVENTOS (tabla final: cantidad de empleados por tipo) ──────

public class EventosSobretiempoConsolidadoDto
{
    public string TipoEvento        { get; set; } = string.Empty;
    public int    CantidadEmpleados { get; set; } // empleados distintos con >=1 día de este evento en el rango
    public int    TotalDias         { get; set; }
}

// ── VIEWMODEL ────────────────────────────────────────────────────────────────

public class EventosSobretiempoKpiViewModel
{
    public int AnoIni { get; set; }
    public int MesIni { get; set; }
    public int AnoFin { get; set; }
    public int MesFin { get; set; }

    // Descripciones del filtro Gran Centro de Costo / Centro de Costo aplicado (null =
    // "Todos"), para que el título del dashboard muestre qué se está filtrando.
    public string? GranCcostoLabel  { get; set; }
    public string? CentroCostoLabel { get; set; }

    // Advertencias no bloqueantes (ej. AQUARIUS no respondió para algún Año/Mes/Tipo de
    // planilla tras los reintentos automáticos): antes esto se descartaba en silencio
    // (solo quedaba en el log del servidor) y el usuario veía datos incompletos sin
    // ninguna señal — ahora se muestra explícitamente en el dashboard para que el
    // usuario sepa que debe reintentar la consulta en vez de asumir que "no hay eventos".
    public List<string> Advertencias { get; set; } = new();

    public List<EventosSobretiempoResumenMesDto>      Resumen            { get; set; } = new();
    public List<EventosSobretiempoAreaMesDto>         Areas              { get; set; } = new();
    public List<EventosSobretiempoCentroCostoMesDto>  CentrosCosto       { get; set; } = new();
    public List<EventosSobretiempoEmpleadoDto>        Empleados          { get; set; } = new();
    public List<EventosSobretiempoConsolidadoDto>     ConsolidadoEventos { get; set; } = new();
    public List<ProyeccionBolsaHeDto>                 ProyeccionBolsaHe  { get; set; } = new();
    public List<ProyeccionBolsaHeCentroCostoDto>      ProyeccionBolsaHeCentroCosto { get; set; } = new();
    public List<EventosSobretiempoDiaEmpleadoDto>     DetalleDiarioHe    { get; set; } = new();
}
