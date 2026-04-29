namespace FabricaHilos.Models.Ventas
{
    /// <summary>Resultado combinado de una sola carga Oracle (IndicadorComercialMaestro).</summary>
    public class IcmTodosDto
    {
        public List<IcmImporteAsesorMesDto> Importe   { get; set; } = new();
        public List<IcmKgAsesorMesDto>      Kg        { get; set; } = new();
        public List<IcmClientesAsesorMesDto> Clientes { get; set; } = new();
    }

    /// <summary>Importe neto por Asesor / Mes (IndicadorComercialMaestro).</summary>
    public class IcmImporteAsesorMesDto
    {
        public string? CodAsesor { get; set; }
        public string? Asesor    { get; set; }
        public string? Mes       { get; set; }   // "YYYY/MM"
        public decimal Importe   { get; set; }
    }

    /// <summary>KG vendidos por Asesor / Mes (IndicadorComercialMaestro).</summary>
    public class IcmKgAsesorMesDto
    {
        public string? Asesor     { get; set; }
        public string? Mes        { get; set; }
        public decimal CantidadKg { get; set; }
    }

    /// <summary>Nro. de clientes distintos por Asesor / Mes (IndicadorComercialMaestro).</summary>
    public class IcmClientesAsesorMesDto
    {
        public string? Asesor      { get; set; }
        public string? Mes         { get; set; }
        public int     NroClientes { get; set; }
    }
}
