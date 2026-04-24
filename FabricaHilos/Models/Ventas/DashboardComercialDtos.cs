namespace FabricaHilos.Models.Ventas
{
    // ── Fila cruda devuelta por QueryPrincipal ──────────────────────────────────
    /// <summary>Una fila del QueryPrincipal.sql (un ítem de documento).</summary>
    public class DcFilaRawDto
    {
        public string? CodCliente  { get; set; }
        public string? Nombre      { get; set; }
        public string? DescGiro    { get; set; }
        public string? CodAsesor   { get; set; }
        public string? Asesor      { get; set; }
        public string? TipoDoc     { get; set; }
        public string? Serie       { get; set; }
        public string? NumDoc      { get; set; }
        public string? CodAlm      { get; set; }
        public string? TpArt       { get; set; }
        public string? CodFam      { get; set; }
        public string? CodLin      { get; set; }
        public string? CodArt      { get; set; }
        public string? Descripcion { get; set; }
        public string? Unidad      { get; set; }
        public decimal Cantidad    { get; set; }
        public decimal ImpVvta     { get; set; }
        public string? Mon         { get; set; }
        public string? Numero1     { get; set; }
        public string? Fec         { get; set; }   // "YYYY/MM"
        public int     Anio        { get; set; }
        public decimal Vvtu        { get; set; }
        public decimal PorDesc1    { get; set; }
        public decimal PorDesc2    { get; set; }
        public decimal Kilos       { get; set; }
        public decimal Soles       { get; set; }
        public decimal Dolar       { get; set; }
        public decimal IgvSoles    { get; set; }
        public decimal IgvDolar    { get; set; }
        public string? Ruc         { get; set; }
    }

    // ── DTOs de salida por gráfico ──────────────────────────────────────────────

    /// <summary>Importe por Asesor / Mes (Gráfico Cartera).</summary>
    public class DcImporteAsesorMesDto
    {
        public string? CodAsesor { get; set; }
        public string? Asesor    { get; set; }
        public string? Mes       { get; set; }
        public decimal Importe   { get; set; }
    }

    /// <summary>Cantidad KG por Asesor / Mes (Gráfico Cartera).</summary>
    public class DcCantidadKgAsesorMesDto
    {
        public string? Asesor     { get; set; }
        public string? Mes        { get; set; }
        public decimal CantidadKg { get; set; }
    }

    /// <summary>Nro. de Clientes distintos por Asesor (Gráfico Pie / Bar clientes).</summary>
    public class DcNroClientesAsesorMesDto
    {
        public string? Asesor     { get; set; }
        public string? Mes        { get; set; }
        public int     NroClientes { get; set; }
    }

    /// <summary>Cliente con importe y KG, para cualquier asesor (lista y exportación).</summary>
    public class DcClienteImporteTodosDto
    {
        public string? Asesor      { get; set; }
        public string? CodCliente  { get; set; }
        public string? Ruc         { get; set; }
        public string? RazonSocial { get; set; }
        public string? Giro        { get; set; }
        public string? Moneda      { get; set; }
        public decimal Importe     { get; set; }
        public decimal Igv         { get; set; }
        public decimal Total       { get; set; }
        public decimal CantidadKg  { get; set; }
    }

    /// <summary>Cliente con importe y KG para un asesor específico (detalle desde pie).</summary>
    public class DcClienteImporteAsesorDto
    {
        public string? CodCliente  { get; set; }
        public string? RazonSocial { get; set; }
        public string? Giro        { get; set; }
        public string? Unidad      { get; set; }
        public decimal CantidadKg  { get; set; }
        public decimal Importe     { get; set; }
    }

    /// <summary>Top N clientes por Asesor / Año (Kilos e Importe).</summary>
    public class DcTopClienteAsesorDto
    {
        public string? Asesor      { get; set; }
        public string? RazonSocial { get; set; }
        public decimal CantidadKg  { get; set; }
        public decimal Importe     { get; set; }
        public int     Anio        { get; set; }
        /// <summary>"importe" | "kg" | "both"</summary>
        public string  TopType     { get; set; } = "both";
    }

    /// <summary>Respuesta compuesta que retorna el endpoint único /DatosDashboard.</summary>
    public class DcDashboardDto
    {
        public List<DcImporteAsesorMesDto>    Importe       { get; set; } = [];
        public List<DcCantidadKgAsesorMesDto> Kg            { get; set; } = [];
        public List<DcNroClientesAsesorMesDto> Clientes     { get; set; } = [];
        public List<DcClienteImporteTodosDto> ClientesTodos { get; set; } = [];
        public List<DcTopClienteAsesorDto>    TopClientes   { get; set; } = [];
    }
}
