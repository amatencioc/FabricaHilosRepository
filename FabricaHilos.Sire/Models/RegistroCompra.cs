namespace FabricaHilos.Sire.Models;

public sealed class RegistroCompra
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
    public string TipoDocIdentidadProveedor { get; set; } = string.Empty;
    public string NumeroDocIdentidadProveedor { get; set; } = string.Empty;
    public string RazonSocialProveedor { get; set; } = string.Empty;
    public decimal BaseImponibleGravadaDestinoGravadas { get; set; }
    public decimal IgvDestinoGravadas { get; set; }
    public decimal BaseImponibleGravadaDestinoMixtas { get; set; }
    public decimal IgvDestinoMixtas { get; set; }
    public decimal BaseImponibleGravadaDestinoNoGravadas { get; set; }
    public decimal IgvDestinoNoGravadas { get; set; }
    public decimal ValorAdquisicionesNoGravadas { get; set; }
    public decimal Isc { get; set; }
    public decimal Icbper { get; set; }
    public decimal OtrosTributosCargos { get; set; }
    public decimal ImporteTotal { get; set; }
    public decimal TipoCambio { get; set; }
    public string FechaEmisionDocModificado { get; set; } = string.Empty;
    public string TipoDocModificado { get; set; } = string.Empty;
    public string SerieDocModificado { get; set; } = string.Empty;
    public string CodigoDependenciaAduanera { get; set; } = string.Empty;
    public string NumeroDocModificado { get; set; } = string.Empty;
    public string NumeroConstanciaDetraccion { get; set; } = string.Empty;
    public string IndicadorSujetoRetencion { get; set; } = "0";
    public string ClasificacionBienesServicios { get; set; } = string.Empty;
    public string IdentificacionContrato { get; set; } = string.Empty;
    public string CodigoErrorTipo1 { get; set; } = string.Empty;
    public string Estado { get; set; } = "1";
}
