namespace FabricaHilos.Models.Ventas
{
    public class ReclamoMesDto
    {
        public string? Periodo { get; set; }
        public int Cantidad { get; set; }
    }

    public class ReclamoFamiliaDto
    {
        public string? CodFamilia { get; set; }
        public int Cantidad { get; set; }
    }

    public class ReclamoClienteDto
    {
        public string? CodCliente { get; set; }
        public string? NombreCliente { get; set; }
        public int Cantidad { get; set; }
        public decimal KgReclamados { get; set; }
    }

    public class ReclamoIndicadoresDto
    {
        public int TotalReclamos { get; set; }
        public decimal TotalKgReclamados { get; set; }
        public decimal LeadTimePromedio { get; set; }
        public decimal PctReclamos { get; set; }
        public decimal PctReposicion { get; set; }
        public decimal PctReproceso { get; set; }
        public int ReclamosPendientes { get; set; }
        public int ReclamosEnProceso { get; set; }
    }

    public class ReclamoMotivoDto
    {
        public string? Motivo { get; set; }
        public int Cantidad { get; set; }
        public decimal Porcentaje { get; set; }
    }

    public class ReclamoListadoDto
    {
        public int Nrorec { get; set; }
        public DateTime? Fecrec { get; set; }
        public string? Codcli { get; set; }
        public string? Descli { get; set; }
        public string? Desven { get; set; }
        public string? Codart { get; set; }
        public string? Desart { get; set; }
        public decimal Cantidad { get; set; }
        public string? Motivo { get; set; }
        public string? Procede { get; set; }
        public string? EstadoDesc { get; set; }
        public int? LeadTime { get; set; }
    }

    public class ReclamoComboItemDto
    {
        public string? Codigo { get; set; }
        public string? Descripcion { get; set; }
    }
}
