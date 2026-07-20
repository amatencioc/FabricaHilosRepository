namespace FabricaHilos.Models.RecursosHumanos;

// ── FILTROS ──────────────────────────────────────────────────────────────────

public class PlanillaMensualFiltroDto
{
    public string CodEmpresa      { get; set; } = "0";
    public string CodSucursal     { get; set; } = "0";
    public string CodTipoPlanilla { get; set; } = "0";
    public string CCostos         { get; set; } = "TODOS"; // CSV o 'TODOS'
    public string FechaInicio     { get; set; } = string.Empty; // DD/MM/YYYY
    public string FechaFinal      { get; set; } = string.Empty;
}

// ── MAESTROS (combos) ─────────────────────────────────────────────────────────

public class PlanillaEmpresaDto
{
    public string CodEmpresa  { get; set; } = string.Empty;
    public string DesEmpresa  { get; set; } = string.Empty;
}

public class PlanillaSucursalDto
{
    public string CodSucursal { get; set; } = string.Empty;
    public string DesSucursal { get; set; } = string.Empty;
}

public class PlanillaTipoPlanillaDto
{
    public string CodTipoPlanilla { get; set; } = string.Empty;
    public string DesTipoPlanilla { get; set; } = string.Empty;
}

public class PlanillaCCostosDto
{
    public string CodCCostos { get; set; } = string.Empty;
    public string DesCCostos { get; set; } = string.Empty;
}

public class PlanillaPeriodoDto
{
    public string SemProceso { get; set; } = string.Empty;
    public string FecIni     { get; set; } = string.Empty; // DD/MM/YYYY
    public string FecFin     { get; set; } = string.Empty;
    public string Label      { get; set; } = string.Empty; // "Sem 022: 25/05 – 31/05/2026"
}

// ── RESUMEN ───────────────────────────────────────────────────────────────────

public class PlanillaResumenDto
{
    public string? CodEmpresa       { get; set; }
    public string? CodPersonal      { get; set; }  // fotocheck o cod interno
    public string? NomTrabajador    { get; set; }

    // Días
    public int     DiasEfectivos    { get; set; }
    public int     DiasTurnoDia     { get; set; }
    public int     DiasTurnoNoche   { get; set; }

    // Horas efectivas
    public string? HorasEfectivas   { get; set; }  // HH:MM
    public string? HorasEfectivas1  { get; set; }  // HH:MM T1
    public int     DiasT2           { get; set; }  // días T2 (V2: ROUND(min/480))
    public int     DiasT3           { get; set; }  // días T3
    public string? HorasT2          { get; set; }  // HH:MM T2 (usado para OBRERO)
    public string? HorasT3          { get; set; }  // HH:MM T3 (usado para OBRERO)

    // Ausencias
    public int     DiasFalta        { get; set; }
    public string? Tardanzas        { get; set; }  // HH:MM

    // Permisos (días)
    public int     Vacaciones        { get; set; }
    public int     VentaVacaciones   { get; set; }
    public int     GVaca             { get; set; }  // días de vacaciones gozados (Vacaciones - VentaVacaciones)
    public int     DescansosMedicos  { get; set; }
    public int     Subsidios         { get; set; }
    public int     AccidenteTrabajo  { get; set; }
    public int     SubsidioMaternidad { get; set; }
    public int     LicenciasSindicales { get; set; }
    public int     Suspensiones      { get; set; }
    public int     PermisoGoceFisico { get; set; }
    public int     LicenciaPaternidad { get; set; }
    public int     LicenciaFallecimiento { get; set; }
    public int     DiasPermisoSinGoce { get; set; }
    public int     DiasPermisoConGoce { get; set; }

    // Permisos (horas)
    public string? PermisosConGoce   { get; set; }  // HH:MM
    public string? PermisosSinGoce   { get; set; }  // HH:MM

    // HE (redondeadas 30-min)
    public string? Horas25           { get; set; }  // H25% HH:MM
    public int     Horas25MinRnd     { get; set; }  // minutos redondeados
    public string? Horas35           { get; set; }  // H35% HH:MM
    public int     Horas35MinRnd     { get; set; }
    public string? Horas50           { get; set; }  // H50% HH:MM (no visible en reporte)
    public string? Horas100          { get; set; }  // HOR.D HH:MM
    public int     Horas100MinRnd    { get; set; }
}

// ── DETALLE ───────────────────────────────────────────────────────────────────

public class PlanillaDetalleFilaDto
{
    public string  TipoFila          { get; set; } = "D"; // 'D'=detalle, 'T'=total

    // Identificación
    public string? SemProceso        { get; set; }
    public string? FecProceso        { get; set; }  // DD/MM/YYYY
    public string? CodPersonal       { get; set; }
    public string? NumDocIdentidad   { get; set; }
    public string? CodTipo           { get; set; }
    public string? DesTipo           { get; set; }
    public string? NomTrabajador     { get; set; }
    public string? Dia               { get; set; }  // LU, MA…
    public string? Feriado           { get; set; }
    public string? DiLab             { get; set; }  // 'SIN Horario' o ''

    // Horarios
    public string? HorarioTeorico    { get; set; }  // HH:MI-HH:MI
    public int     TotHorasTeom      { get; set; }  // min teóricos
    public string? HorarioJornada    { get; set; }
    public string? HorarioRefrigerio { get; set; }
    public string? HoraRef           { get; set; }

    // Tardanza / antes salida
    public string? HoraTardanza      { get; set; }
    public int     HoraTardanzaM     { get; set; }
    public string? HoraAnteSalida    { get; set; }
    public int     HoraAnteSalidaM   { get; set; }

    // Permisos
    public string? HoraPermiso       { get; set; }
    public int     HoraPermisoM      { get; set; }

    // Efectivas
    public string? HoraEfectiva      { get; set; }
    public int     HoraEfectivaM     { get; set; }
    public string? HoraEfectivaT1    { get; set; }
    public int     HoraEfectivaT1M   { get; set; }
    public string? HoraEfectivaT2    { get; set; }
    public int     HoraEfectivaT2M   { get; set; }
    public string? HoraEfectivaT3    { get; set; }
    public int     HoraEfectivaT3M   { get; set; }

    // HE (por fila 'D' en HH:MM; en fila 'T' en minutos + campos corregidos)
    public string? HoraExofi1        { get; set; }  // H25%
    public int     HoraExofi1M       { get; set; }
    public string? HoraExofi2        { get; set; }  // H35%
    public int     HoraExofi2M       { get; set; }
    public string? HoraDobles        { get; set; }  // HOR.D
    public int     HoraDoblesM       { get; set; }
    public string? TotHoraNocturna   { get; set; }  // H.Noc
    public int     TotHoraNocturnaM  { get; set; }

    // Campos corregidos (solo meaningful en tipo_fila='T')
    public int     DiasT2            { get; set; }  // DIAS T2
    public int     DiasT3            { get; set; }  // DIAS T3
    public int     HoraExofi1MRnd    { get; set; }  // H25% min redondeados
    public int     HoraExofi2MRnd    { get; set; }  // H35% min redondeados
    public int     HoraDoblesRnd     { get; set; }  // HOR.D min redondeados (min 60 o 0)

    // Helpers de visualización (calculados en cliente)
    public string  H25Rnd   => HoraExofi1MRnd  > 0 ? $"{HoraExofi1MRnd/60}:{HoraExofi1MRnd%60:D2}"  : "0:00";
    public string  H35Rnd   => HoraExofi2MRnd  > 0 ? $"{HoraExofi2MRnd/60}:{HoraExofi2MRnd%60:D2}"  : "0:00";
    public string  DobRnd   => HoraDoblesRnd    > 0 ? $"{HoraDoblesRnd/60}:{HoraDoblesRnd%60:D2}"    : "0:00";
}
