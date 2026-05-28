namespace FabricaHilos.Sire.Constants;

public static class TipoComprobante
{
    public static readonly IReadOnlyDictionary<string, string> Tabla10 =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["01"] = "Factura",
            ["03"] = "Boleta de Venta",
            ["07"] = "Nota de Crédito",
            ["08"] = "Nota de Débito",
            ["09"] = "Guía de Remisión",
            ["12"] = "Ticket",
            ["31"] = "Guía de Remisión Transportista",
            ["56"] = "Comprobante de Retención"
        };
}
