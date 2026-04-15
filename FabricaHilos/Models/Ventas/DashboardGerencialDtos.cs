namespace FabricaHilos.Models.Ventas
{
    /// <summary>Venta agrupada por mercado geográfico (Perú / Latam / Global).</summary>
    public class DgVentaMercadoDto
    {
        public string? Mercado { get; set; }
        public decimal Importe { get; set; }
    }

    /// <summary>Detalle de venta por país dentro de un mercado.</summary>
    public class DgVentaMercadoPaisDto
    {
        public string? Mercado { get; set; }
        public string? CodigoPais { get; set; }
        public string? Pais { get; set; }
        public decimal Importe { get; set; }
    }

    /// <summary>Detalle de venta por departamento (solo Perú).</summary>
    public class DgVentaMercadoDepartamentoDto
    {
        public string? Departamento { get; set; }
        public decimal Importe { get; set; }
    }

    /// <summary>Detalle de venta por distrito dentro de un departamento (solo Perú, UBIGEO.PAIS='01').</summary>
    public class DgVentaMercadoDistritoDto
    {
        public string? Departamento { get; set; }
        public string? Distrito { get; set; }
        public decimal Importe { get; set; }
    }

    /// <summary>Detalle de ciudades/distritos de un país extranjero (UBIGEO.PAIS≠'01').</summary>
    public class DgVentaMercadoCiudadPaisDto
    {
        public string? Pais { get; set; }
        public string? Ciudad { get; set; }
        public decimal Importe { get; set; }
    }

    /// <summary>Evolución mensual de ventas por mercado.</summary>
    public class DgVentaMercadoEvolucionDto
    {
        public string? Periodo { get; set; }
        public string? Mercado { get; set; }
        public decimal Importe { get; set; }
    }

    /// <summary>Top clientes por importe de ventas.</summary>
    public class DgVentaMercadoTopClienteDto
    {
        public string? CodCliente { get; set; }
        public string? NomCliente { get; set; }
        public string? Pais { get; set; }
        public string? Mercado { get; set; }
        public decimal Importe { get; set; }
        public int CantDocumentos { get; set; }
    }

    /// <summary>Detalle completo de documentos de venta (nivel transaccional).</summary>
    public class DgVentaMercadoDocumentoDto
    {
        public string? TipoDoc { get; set; }
        public string? Serie { get; set; }
        public string? Numero { get; set; }
        public DateTime? Fecha { get; set; }
        public string? Moneda { get; set; }
        public string? CodCliente { get; set; }
        public string? NomCliente { get; set; }
        public string? Pais { get; set; }
        public string? Exportacion { get; set; }
        public string? Mercado { get; set; }
        public decimal ImportCam { get; set; }
        public decimal ValVenta { get; set; }
        public decimal ImpDescto { get; set; }
        public decimal ImpAnticipo { get; set; }
        public decimal ImpInteres { get; set; }
        public decimal ImpNeto { get; set; }
        public decimal ImpIgv { get; set; }
        public decimal PrecioVta { get; set; }
        public string? Departamento { get; set; }
        public string? Distrito { get; set; }
        public string? UbigeoPais { get; set; }
    }

    /// <summary>Mapeo de país BD → ISO para el mapa interactivo.</summary>
    public class DgPaisIsoDto
    {
        public string? CodigoBD { get; set; }
        public string? CodigoISO { get; set; }
        public string? Descripcion { get; set; }
    }
}
