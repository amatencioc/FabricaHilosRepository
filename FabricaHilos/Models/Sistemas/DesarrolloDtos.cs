namespace FabricaHilos.Models.Sistemas
{
    // ── Fila cruda devuelta por ind_desarrollo.sql ─────────────────────────────
    /// <summary>
    /// Una fila del query ind_desarrollo.sql.
    /// ESTADO '1' = Pendiente (sin F_TERMINO).
    /// ESTADO '2' = Entregado (F_TERMINO en el rango).
    /// </summary>
    public class DevFilaRawDto
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

        public bool EsPendiente => Estado == "1";
        public bool EsEntregado => Estado == "2";
    }

    /// <summary>Celda PENDIENTE/ENTREGADO de un año en una fila del pivot.</summary>
    public class DevCeldaAnoDto
    {
        public int Ano       { get; set; }
        public int Pendiente { get; set; }
        public int Entregado { get; set; }
    }

    /// <summary>Una fila del pivot por área.</summary>
    public class DevFilaAreaDto
    {
        public string             Area           { get; set; } = "";
        public List<DevCeldaAnoDto> Anos         { get; set; } = [];
        public int                TotalPendiente { get; set; }
        public int                TotalEntregado { get; set; }
    }

    /// <summary>DTO usado para ranking/gráficos por área.</summary>
    public class DevAreaTotalDto
    {
        public string Area           { get; set; } = "";
        public int    TotalPendiente { get; set; }
        public int    TotalEntregado { get; set; }
        public int    Abiertos       => TotalPendiente - TotalEntregado;
    }

    /// <summary>Distribución de items recibidos por año.</summary>
    public class DevAnoTotalDto
    {
        public int Ano       { get; set; }
        public int Pendiente { get; set; }
        public int Entregado { get; set; }
    }

    /// <summary>Atención mes a mes para el año más reciente del rango.</summary>
    public class DevAtencionMesDto
    {
        public int    Mes         { get; set; }
        public string Etiqueta    { get; set; } = "";
        public int    Recibidos   { get; set; }
        public int    AtMismoMes  { get; set; }
        public int    AtSigMes    { get; set; }
        public int    Pendientes  { get; set; }
        public double PctMismoMes { get; set; }
    }

    /// <summary>Respuesta compuesta del endpoint /DatosDashboard.</summary>
    public class DevDashboardDto
    {
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin    { get; set; }

        // KPI cards
        public int    TotalRecibidos  { get; set; }
        public int    TotalPendientes { get; set; }   // ESTADO='1' (abiertos)
        public int    TotalEntregados { get; set; }   // ESTADO='2'
        public int    AnoAtencion     { get; set; }
        public double PctAtencionAno  { get; set; }

        // Tabla pivot
        public List<int>            Anos        { get; set; } = [];
        public List<DevFilaAreaDto> Filas       { get; set; } = [];
        public List<DevCeldaAnoDto> TotalesAno  { get; set; } = [];
        public int                  GTPendiente { get; set; }
        public int                  GTEntregado { get; set; }

        // Datos para gráficos
        public List<DevAreaTotalDto>   PorArea     { get; set; } = [];
        public List<DevAnoTotalDto>    PorAno      { get; set; } = [];
        public List<DevAtencionMesDto> AtencionMes { get; set; } = [];
    }
}
