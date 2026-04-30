namespace FabricaHilos.Models.Sistemas
{
    // ── Fila cruda devuelta por ind_incidencias.sql (query 1) ──────────────────
    /// <summary>
    /// Una fila del query ind_incidencias.sql.
    /// ESTADO '1' = Pendiente (sin F_TERMINO).
    /// ESTADO '2' = Resuelto (F_TERMINO en el rango).
    /// ESTADO '9' = Anulado  (F_TERMINO en el rango).
    /// </summary>
    public class IncFilaRawDto
    {
        public string?   Numero          { get; set; }
        public string?   ClienteNombre   { get; set; }
        public string?   CCosto          { get; set; }
        public string?   Area            { get; set; }
        public DateTime? Fecha           { get; set; }
        public DateTime? FechaAprobacion { get; set; }
        public string?   Requerimiento   { get; set; }
        public string?   Solucion        { get; set; }
        public DateTime? FechaInicio     { get; set; }
        public DateTime? FechaTermino    { get; set; }
        public string?   Estado          { get; set; }

        public bool EsPendiente  => Estado == "1";
        public bool EsEntregado  => Estado == "2" || Estado == "9";
    }

    /// <summary>Celda PENDIENTE/ENTREGADO de un año en una fila del pivot.</summary>
    public class IncCeldaAnoDto
    {
        public int Ano       { get; set; }
        public int Pendiente { get; set; }
        public int Entregado { get; set; }
    }

    /// <summary>Una fila del pivot por área.</summary>
    public class IncFilaAreaDto
    {
        public string               Area           { get; set; } = "";
        public List<IncCeldaAnoDto> Anos           { get; set; } = [];
        public int                  TotalPendiente { get; set; }
        public int                  TotalEntregado { get; set; }
    }

    /// <summary>DTO usado para ranking/gráficos por área.</summary>
    public class IncAreaTotalDto
    {
        public string Area           { get; set; } = "";
        public int    TotalPendiente { get; set; }
        public int    TotalEntregado { get; set; }
        public int    Abiertos       => TotalPendiente - TotalEntregado;
    }

    /// <summary>Distribución de incidencias recibidas por año.</summary>
    public class IncAnoTotalDto
    {
        public int Ano       { get; set; }
        public int Pendiente { get; set; }
        public int Entregado { get; set; }
    }

    /// <summary>Atención mes a mes (query 1).</summary>
    public class IncAtencionMesDto
    {
        public int    Mes         { get; set; }
        public string Etiqueta    { get; set; } = "";
        public int    Recibidos   { get; set; }
        public int    AtMismoMes  { get; set; }
        public int    AtSigMes    { get; set; }
        public int    Pendientes  { get; set; }
        public double PctMismoMes { get; set; }
    }

    /// <summary>Promedio de minutos de atención por mes (query 2).</summary>
    public class IncMinutosMesDto
    {
        public int    Mes             { get; set; }
        public string Etiqueta        { get; set; } = "";
        public double PromedioMinutos { get; set; }
    }

    /// <summary>Respuesta compuesta del endpoint /DatosDashboard.</summary>
    public class IncDashboardDto
    {
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin    { get; set; }

        // KPI cards
        public int    TotalRecibidos  { get; set; }
        public int    TotalPendientes { get; set; }   // ESTADO='1' (abiertos)
        public int    TotalEntregados { get; set; }   // ESTADO='2' o '9'
        public int    AnoAtencion     { get; set; }
        public double PctAtencionAno  { get; set; }

        // Tabla pivot
        public List<int>            Anos        { get; set; } = [];
        public List<IncFilaAreaDto> Filas       { get; set; } = [];
        public List<IncCeldaAnoDto> TotalesAno  { get; set; } = [];
        public int                  GTPendiente { get; set; }
        public int                  GTEntregado { get; set; }

        // Datos para gráficos
        public List<IncAreaTotalDto>   PorArea     { get; set; } = [];
        public List<IncAnoTotalDto>    PorAno      { get; set; } = [];
        public List<IncAtencionMesDto> AtencionMes { get; set; } = [];
        public List<IncMinutosMesDto>  MinutosMes  { get; set; } = [];
    }
}
