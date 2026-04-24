namespace FabricaHilos.Models.Ventas
{
    // ── Fila cruda devuelta por el query (ya agrupada por cliente/asesor/moneda) ─
    /// <summary>
    /// Una fila del query DasboardComercial_MaestroAsesor.sql.
    /// El query devuelve datos ya agrupados: un registro por COD_CLIENTE + MONEDA.
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
        public string? Moneda     { get; set; }   // 'S' o 'D'
        public decimal Soles      { get; set; }
        public decimal Dolar      { get; set; }
        public decimal IgvSoles   { get; set; }
        public decimal IgvDolar   { get; set; }
    }

    // ── DTOs de salida por gráfico ──────────────────────────────────────────────

    /// <summary>Importe total por Asesor (Gráfico Cartera / Ranking).</summary>
    public class DcmImporteAsesorDto
    {
        public string? CodAsesor { get; set; }
        public string? Asesor    { get; set; }
        public decimal Importe   { get; set; }
        public decimal Kilos     { get; set; }
        public int     NroDoc    { get; set; }
    }

    /// <summary>Nro. de Clientes distintos por Asesor (Gráfico Pie / Bar clientes).</summary>
    public class DcmNroClientesAsesorDto
    {
        public string? Asesor      { get; set; }
        public int     NroClientes { get; set; }
    }

    /// <summary>
    /// Cliente con importe, KG, IGV e info completa — usado en tabla maestra,
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
        public decimal Igv         { get; set; }
        public decimal Total       { get; set; }   // Importe + IGV
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
        /// <summary>Importe + KG + NroDoc por asesor (para Cartera, Ranking y Participación).</summary>
        public List<DcmImporteAsesorDto>    Asesores      { get; set; } = [];

        /// <summary>Cantidad de clientes distintos por asesor (para Pie y Bar %).</summary>
        public List<DcmNroClientesAsesorDto> Clientes     { get; set; } = [];

        /// <summary>Todos los clientes con detalle completo (tabla, exportación y ranking).</summary>
        public List<DcmClienteMaestroDto>   ClientesTodos { get; set; } = [];

        /// <summary>Top N clientes por asesor (Importe y KG).</summary>
        public List<DcmTopClienteAsesorDto> TopClientes   { get; set; } = [];
    }
}
