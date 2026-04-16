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

    /// <summary>Mapeo de país BD → ISO para el mapa interactivo.</summary>
    public class DgPaisIsoDto
    {
        public string? CodigoBD { get; set; }
        public string? CodigoISO { get; set; }
        public string? Descripcion { get; set; }
    }

    /// <summary>Cantidad KG mensual (sin filtro de asesor).</summary>
    public class DgKgMensualDto
    {
        public string? Periodo { get; set; }
        public decimal CantidadKg { get; set; }
    }

    /// <summary>Top hilados (familia) por importe facturado.</summary>
    public class DgTopHiladoImporteDto
    {
        public string? Familia { get; set; }
        public decimal Importe { get; set; }
    }

    /// <summary>Ventas agrupadas por giro de cliente.</summary>
    public class DgVentaPorGiroDto
    {
        public string? CodigoGiro { get; set; }
        public string? DescGiro { get; set; }
        public decimal Importe { get; set; }
    }

    /// <summary>Top hilados (familia) por kilogramos vendidos.</summary>
    public class DgTopHiladoKgDto
    {
        public string? Familia { get; set; }
        public decimal Kilos { get; set; }
    }
}
