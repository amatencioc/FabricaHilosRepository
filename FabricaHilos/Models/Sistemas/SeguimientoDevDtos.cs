namespace FabricaHilos.Models.Sistemas
{
    // ── Fila cruda devuelta por ind_seguimientoDev.sql ────────────────────────
    /// <summary>
    /// Una fila del query ind_seguimientoDev.sql.
    /// Solo incluye ESTADO='2' (entregados) en el rango de fechas.
    /// </summary>
    public class SdFilaRawDto
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
        public string?   UserSistema     { get; set; }  // T.DESCRIPCION → analista/responsable
        public string?   Motivo          { get; set; }  // '11','16' = Desarrollo; resto = Incidencia
    }

    /// <summary>Celda de un año en la tabla pivot por responsable.</summary>
    public class SdCeldaAnoDto
    {
        public int Ano       { get; set; }
        public int Entregado { get; set; }
    }

    /// <summary>Una fila del pivot por responsable (USER_SISTEMA).</summary>
    public class SdFilaResponsableDto
    {
        public string             Responsable    { get; set; } = "";
        public List<SdCeldaAnoDto> Anos          { get; set; } = [];
        public int                TotalEntregado { get; set; }
    }

    /// <summary>DTO usado para gráficos por área.</summary>
    public class SdAreaTotalDto
    {
        public string Area          { get; set; } = "";
        public int    TotalEntregado { get; set; }
    }

    /// <summary>DTO usado para gráficos por responsable.</summary>
    public class SdResponsableTotalDto
    {
        public string Responsable   { get; set; } = "";
        public int    TotalEntregado { get; set; }
    }

    /// <summary>Entregados mes a mes para el año más reciente del rango.</summary>
    public class SdEntregaMesDto
    {
        public int    Mes        { get; set; }
        public string Etiqueta   { get; set; } = "";
        public int    Entregados { get; set; }
    }

    /// <summary>Respuesta compuesta del endpoint /DatosDashboard.</summary>
    public class SdDashboardDto
    {
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin    { get; set; }

        // KPI cards
        public int TotalEntregados   { get; set; }
        public int TotalDesarrollo   { get; set; }  // MOTIVO 11 o 16
        public int TotalIncidencia   { get; set; }  // demás MOTIVOs
        public int TotalResponsables { get; set; }
        public int TotalAreas        { get; set; }
        public int AnoAtencion       { get; set; }

        // Tabla pivot por responsable
        public List<int>                   Anos             { get; set; } = [];
        public List<SdFilaResponsableDto>  Filas            { get; set; } = [];
        public List<SdCeldaAnoDto>         TotalesAno       { get; set; } = [];
        public int                         GTEntregado      { get; set; }

        // Lista de responsables para el filtro del cliente
        public List<string>                Responsables     { get; set; } = [];

        // Datos para gráficos (totales)
        public List<SdAreaTotalDto>        PorArea                   { get; set; } = [];
        public List<SdResponsableTotalDto> PorResponsable            { get; set; } = [];
        public List<SdEntregaMesDto>       EntregaMes                { get; set; } = [];

        // Datos split Desarrollo / Incidencia (para gráficos en modo Todos)
        public List<SdAreaTotalDto>        PorAreaDesarrollo         { get; set; } = [];
        public List<SdAreaTotalDto>        PorAreaIncidencia         { get; set; } = [];
        public List<SdResponsableTotalDto> PorResponsableDesarrollo  { get; set; } = [];
        public List<SdResponsableTotalDto> PorResponsableIncidencia  { get; set; } = [];
        public List<SdEntregaMesDto>       EntregaMesDesarrollo      { get; set; } = [];
        public List<SdEntregaMesDto>       EntregaMesIncidencia      { get; set; } = [];
        public List<SdCeldaAnoDto>         TotalesAnoDesarrollo      { get; set; } = [];
        public List<SdCeldaAnoDto>         TotalesAnoIncidencia      { get; set; } = [];
    }
}
