namespace FabricaHilos.Models.Ventas
{
    /// <summary>Query 1: Importe por Asesor / Mes</summary>
    public class DcImporteAsesorMesDto
    {
        public string? CodAsesor { get; set; }
        public string? Asesor { get; set; }
        public string? Mes { get; set; }
        public decimal Importe { get; set; }
    }

    /// <summary>Query 2: Cantidad KG por Asesor / Mes</summary>
    public class DcCantidadKgAsesorMesDto
    {
        public string? Asesor { get; set; }
        public string? Mes { get; set; }
        public decimal CantidadKg { get; set; }
    }

    /// <summary>Query 3: Nro. de Clientes por Asesor / Mes</summary>
    public class DcNroClientesAsesorMesDto
    {
        public string? Asesor { get; set; }
        public string? Mes { get; set; }
        public int NroClientes { get; set; }
    }

    /// <summary>Query 1.1: Detalle de Importe por Cliente por Asesor / Mes</summary>
    public class DcDetalleImporteAsesorMesDto
    {
        public string? Asesor { get; set; }
        public string? Mes { get; set; }
        public string? CodCliente { get; set; }
        public string? Ruc { get; set; }
        public string? RazonSocial { get; set; }
        public decimal Importe { get; set; }
    }

    /// <summary>Query 3.1: Detalle de Clientes por Asesor / Mes</summary>
    public class DcDetalleClienteAsesorMesDto
    {
        public string? Asesor { get; set; }
        public string? Mes { get; set; }
        public string? CodCliente { get; set; }
        public string? Ruc { get; set; }
        public string? RazonSocial { get; set; }
        public decimal CantidadKg { get; set; }
        public decimal Importe { get; set; }
    }

    /// <summary>Query 5: Clientes del Asesor — Importe + Giro (período completo)</summary>
    public class DcClienteImporteAsesorDto
    {
        public string? CodCliente { get; set; }
        public string? Ruc { get; set; }
        public string? RazonSocial { get; set; }
        public string? Giro { get; set; }
        public decimal Importe { get; set; }
    }

    /// <summary>Query 5b: Todos los Clientes por Asesor — Importe + Giro (período completo, todos los asesores)</summary>
    public class DcClienteImporteTodosDto
    {
        public string? Asesor { get; set; }
        public string? CodCliente { get; set; }
        public string? Ruc { get; set; }
        public string? RazonSocial { get; set; }
        public string? Giro { get; set; }
        public decimal Importe { get; set; }
    }

    /// <summary>Query 4: Top N clientes por Asesor (Kilos e Importe acumulado en el período)</summary>
    public class DcTopClienteAsesorDto
    {
        public string? Asesor { get; set; }
        public string? RazonSocial { get; set; }
        public decimal CantidadKg { get; set; }
        public decimal Importe { get; set; }
        public int Anio { get; set; }
        /// <summary>"importe" | "kg" | "both" — indica en qué ranking top-N aparece este cliente</summary>
        public string TopType { get; set; } = "both";
    }
}
