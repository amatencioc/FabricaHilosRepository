namespace FabricaHilos.Models.Ventas
{
    /// <summary>Query 1: Importe por Asesor / Mes</summary>
    public class ImporteAsesorMesDto
    {
        public string? CodAsesor { get; set; }
        public string? Asesor { get; set; }
        public string? Mes { get; set; }
        public decimal Importe { get; set; }
    }

    /// <summary>Query 2: Cantidad KG por Asesor / Mes</summary>
    public class CantidadKgAsesorMesDto
    {
        public string? Asesor { get; set; }
        public string? Mes { get; set; }
        public decimal CantidadKg { get; set; }
    }

    /// <summary>Query 3: Nro. de Clientes por Asesor / Mes</summary>
    public class NroClientesAsesorMesDto
    {
        public string? Asesor { get; set; }
        public string? Mes { get; set; }
        public int NroClientes { get; set; }
    }

    /// <summary>Query 1.1: Detalle de Importe por Cliente por Asesor / Mes</summary>
    public class DetalleImporteAsesorMesDto
    {
        public string? Asesor { get; set; }
        public string? Mes { get; set; }
        public string? CodCliente { get; set; }
        public string? Ruc { get; set; }
        public string? RazonSocial { get; set; }
        public decimal Importe { get; set; }
    }

    /// <summary>Query 3.1: Detalle de Clientes por Asesor / Mes</summary>
    public class DetalleClienteAsesorMesDto
    {
        public string? Asesor { get; set; }
        public string? Mes { get; set; }
        public string? CodCliente { get; set; }
        public string? Ruc { get; set; }
        public string? RazonSocial { get; set; }
        public decimal CantidadKg { get; set; }
        public decimal Importe { get; set; }
    }
}
