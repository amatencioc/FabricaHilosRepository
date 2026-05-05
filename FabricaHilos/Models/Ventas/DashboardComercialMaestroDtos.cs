namespace FabricaHilos.Models.Ventas
{
    // ── Fila cruda devuelta por el query (ya agrupada por cliente/asesor) ────────
    /// <summary>
    /// Una fila del query QueryComercialMaestroGrupo.sql.
    /// El query devuelve un registro por COD_CLIENTE + COD_ASESOR.
    /// SOLES y DOLAR son montos netos en su moneda original (sin conversión cruzada).
    /// </summary>
    public class DcmFilaRawDto
    {
        public string? CodCliente { get; set; }
        public string? Ruc        { get; set; }
        public string? Nombre     { get; set; }
        public string? Giro       { get; set; }
        public string? DescGiro   { get; set; }
        public string? CodAsesor  { get; set; }
        public string? Asesor     { get; set; }
        public int     NroDoc     { get; set; }
        public decimal TotUnid    { get; set; }   // kilos
        public decimal Soles      { get; set; }   // importe neto en soles
        public decimal Dolar      { get; set; }   // importe neto en dólares
    }

    // ── DTOs de salida por gráfico ──────────────────────────────────────────────

    /// <summary>
    /// Cliente con importe, KG e info completa — usado en tabla maestra,
    /// en ranking de clientes y en exportación Excel.
    /// </summary>
    public class DcmClienteMaestroDto
    {
        public string? Asesor      { get; set; }
        public string? CodAsesor   { get; set; }
        public string? CodCliente  { get; set; }
        public string? Ruc         { get; set; }
        public string? RazonSocial { get; set; }
        public string? Giro        { get; set; }
        public int     NroDoc      { get; set; }
        public decimal CantidadKg  { get; set; }
        public decimal Importe     { get; set; }
        public decimal Total       { get; set; }
    }

    /// <summary>Top N clientes por Asesor (Kilos e Importe).</summary>
    public class DcmTopClienteAsesorDto
    {
        public string? Asesor      { get; set; }
        public string? CodCliente  { get; set; }
        public string? RazonSocial { get; set; }
        public decimal CantidadKg  { get; set; }
        public decimal Importe     { get; set; }
        public int     NroDoc      { get; set; }
        /// <summary>"importe" | "kg" | "both"</summary>
        public string  TopType     { get; set; } = "both";
    }

    /// <summary>Respuesta compuesta que retorna el endpoint único /DatosDashboard.</summary>
    public class DcmDashboardDto
    {
        /// <summary>Todos los clientes con detalle completo (tabla, exportación y ranking).</summary>
        public List<DcmClienteMaestroDto>   ClientesTodos     { get; set; } = [];

        /// <summary>Top N clientes por asesor (Importe y KG).</summary>
        public List<DcmTopClienteAsesorDto> TopClientes       { get; set; } = [];

        /// <summary>Conteo de clientes distintos por asesor (para el pie chart).</summary>
        public List<DcmClientesCountDto>    ClientesPorAsesor { get; set; } = [];

        /// <summary>Totales de venta e importe por asesor derivados del detalle (ranking y participación).</summary>
        public List<DcmVentaAsesorDto>      VentasPorAsesor   { get; set; } = [];
    }

    /// <summary>Cantidad de clientes distintos por asesor.</summary>
    public class DcmClientesCountDto
    {
        public string? Asesor        { get; set; }
        public int     TotalClientes { get; set; }
    }

    /// <summary>Totales de importe y KG por asesor (del detalle P_TIPO='D').</summary>
    public class DcmVentaAsesorDto
    {
        public string? Asesor     { get; set; }
        public decimal Importe    { get; set; }
        public decimal CantidadKg { get; set; }
    }
}
