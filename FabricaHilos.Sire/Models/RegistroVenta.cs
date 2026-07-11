namespace FabricaHilos.Sire.Models;

public sealed class RegistroVenta
{
    public string PeriodoTributario { get; set; } = string.Empty;
    public string Cuo { get; set; } = string.Empty;
    public string CorrelativoAsiento { get; set; } = string.Empty;
    public string FechaEmision { get; set; } = string.Empty;
    public string FechaVencimientoPago { get; set; } = string.Empty;
    public string TipoComprobante { get; set; } = string.Empty;
    public string SerieComprobante { get; set; } = string.Empty;
    public string AnioDuaDsi { get; set; } = string.Empty;
    public string NumeroComprobante { get; set; } = string.Empty;
    public string NumeroFinalComprobante { get; set; } = string.Empty;
    public string TipoDocIdentidadCliente { get; set; } = string.Empty;
    public string NumeroDocIdentidadCliente { get; set; } = string.Empty;
    public string RazonSocialCliente { get; set; } = string.Empty;
    public decimal BaseImponibleGravada { get; set; }
    public decimal BaseImponibleGravadaTasaDiferenciada { get; set; }
    public decimal IgvTasaDiferenciada { get; set; }
    public decimal BaseImponibleIsc { get; set; }
    public decimal Isc { get; set; }
    public decimal BaseImponibleIvap { get; set; }
    public decimal Ivap { get; set; }
    public decimal OperacionesExoneradas { get; set; }
    public decimal OperacionesInafectas { get; set; }
    public decimal Igv { get; set; }
    public decimal Icbper { get; set; }
    public decimal OtrosTributosCargos { get; set; }
    public decimal ImporteTotal { get; set; }
    public string CodigoMoneda { get; set; } = "PEN";
    public decimal TipoCambio { get; set; }
    public string FechaEmisionDocModificado { get; set; } = string.Empty;
    public string TipoDocModificado { get; set; } = string.Empty;
    public string SerieDocModificado { get; set; } = string.Empty;
    public string NumeroDocModificado { get; set; } = string.Empty;
    public string CodigoErrorTipo1 { get; set; } = string.Empty;
    public string IndicadorComprobanteCancelado { get; set; } = "0";
    public string Estado { get; set; } = "1";
}
