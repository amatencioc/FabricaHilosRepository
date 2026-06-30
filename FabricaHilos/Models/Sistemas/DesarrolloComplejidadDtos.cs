namespace FabricaHilos.Models.Sistemas
{
    // ── Fila cruda devuelta por ind_desarrollo_complejidad.sql ─────────────────
    public class DevCompFilaRawDto
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
        public string?   Complejidad     { get; set; }
        /// <summary>Código fuente de PRIORIDAD: '01'=BAJA · '02'=MEDIA · '03'=ALTA.</summary>
        public string?   CodComplejidad  { get; set; }

        public bool EsPendiente => Estado == "1";
        public bool EsEntregado => Estado == "2";
    }

    /// <summary>Celda PENDIENTE/ENTREGADO de un año en el pivot de complejidad.</summary>
    public class DevCompCeldaAnoDto
    {
        public int Ano       { get; set; }
        public int Pendiente { get; set; }
        public int Entregado { get; set; }
    }

    /// <summary>Una fila del pivot agrupada por COMPLEJIDAD.</summary>
    public class DevCompFilaComplejidadDto
    {
        public string                    Complejidad    { get; set; } = "";
        public List<DevCompCeldaAnoDto>  Anos           { get; set; } = [];
        public int                       TotalPendiente { get; set; }
        public int                       TotalEntregado { get; set; }
    }

    /// <summary>Totales por complejidad para gráficos.</summary>
    public class DevCompTotalDto
    {
        public string Complejidad     { get; set; } = "";
        public int    TotalPendiente  { get; set; }
        public int    TotalEntregado  { get; set; }
        public int    Abiertos        => TotalPendiente - TotalEntregado;
    }

    /// <summary>Distribución anual (totales de todos los ítems).</summary>
    public class DevCompAnoTotalDto
    {
        public int Ano       { get; set; }
        public int Pendiente { get; set; }
        public int Entregado { get; set; }
    }

    /// <summary>Atención mes a mes ponderada por peso de complejidad.</summary>
    public class DevCompAtencionMesDto
    {
        public int    Mes          { get; set; }
        public string Etiqueta     { get; set; } = "";
        public int    Recibidos    { get; set; }
        public int    AtMismoMes   { get; set; }
        public int    AtSigMes     { get; set; }
        public int    Pendientes   { get; set; }
        public double PctMismoMes  { get; set; }
        /// <summary>Peso acumulado por complejidad de los ítems del mes.</summary>
        public double PesoTotal    { get; set; }
    }

    /// <summary>Distribución de cantidad e ítems por complejidad en un mes.</summary>
    public class DevCompMesDetalleDto
    {
        public int    Mes          { get; set; }
        public string Etiqueta     { get; set; } = "";
        /// <summary>
        /// Diccionario: Complejidad → cantidad de ítems recibidos ese mes con esa complejidad.
        /// Permite pintar barras apiladas por complejidad en el gráfico mensual.
        /// </summary>
        public Dictionary<string, int> PorComplejidad { get; set; } = [];
    }

    /// <summary>Respuesta compuesta del endpoint /DatosDashboard de Complejidad.</summary>
    public class DevCompDashboardDto
    {
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin    { get; set; }

        // KPI cards
        public int    TotalRecibidos  { get; set; }
        public int    TotalPendientes { get; set; }
        public int    TotalEntregados { get; set; }
        public int    TotalComplejos  { get; set; }  // ítems de alta complejidad (ALTA / ALTO)
        public int    AnoAtencion     { get; set; }
        public double PctAtencionAno  { get; set; }

        // Pivot: complejidad × año
        public List<int>                     Anos       { get; set; } = [];
        public List<DevCompFilaComplejidadDto> Filas     { get; set; } = [];
        public List<DevCompCeldaAnoDto>       TotalesAno { get; set; } = [];
        public int                            GTPendiente { get; set; }
        public int                            GTEntregado { get; set; }

        // Datasets para gráficos
        public List<DevCompTotalDto>      PorComplejidad { get; set; } = [];
        public List<DevCompAnoTotalDto>   PorAno         { get; set; } = [];
        public List<DevCompAtencionMesDto> AtencionMes   { get; set; } = [];

        /// <summary>
        /// Lista ordenada de todas las complejidades presentes (para datasets de barras apiladas).
        /// </summary>
        public List<string>               Complejidades  { get; set; } = [];

        /// <summary>
        /// Detalle por mes × complejidad para el gráfico de barras apiladas mensual.
        /// </summary>
        public List<DevCompMesDetalleDto> MesDetalle     { get; set; } = [];
    }
}
