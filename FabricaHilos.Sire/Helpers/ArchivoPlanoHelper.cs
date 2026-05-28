using System.Globalization;
using System.Text;
using FabricaHilos.Sire.Models;

namespace FabricaHilos.Sire.Helpers;

public static class ArchivoPlanoHelper
{
    public static string GenerarNombreArchivo(string ruc, string periodo, string codigoLibro)
    {
        if (periodo.Length != 6)
        {
            throw new ArgumentException("El periodo debe tener formato AAAAMM.", nameof(periodo));
        }

        var anio = periodo[..4];
        var mes = periodo[4..6];
        return $"LE{ruc}{anio}{mes}{codigoLibro}0111.txt";
    }

    public static string GenerarVentasTxt(IEnumerable<RegistroVenta> registros)
    {
        var sb = new StringBuilder();
        foreach (var r in registros)
        {
            var campos = new[]
            {
                r.PeriodoTributario, r.Cuo, r.CorrelativoAsiento, r.FechaEmision, r.FechaVencimientoPago,
                r.TipoComprobante, r.SerieComprobante, r.AnioDuaDsi, r.NumeroComprobante, r.NumeroFinalComprobante,
                r.TipoDocIdentidadCliente, r.NumeroDocIdentidadCliente, r.RazonSocialCliente,
                DecimalStr(r.BaseImponibleGravada), DecimalStr(r.BaseImponibleGravadaTasaDiferenciada), DecimalStr(r.IgvTasaDiferenciada),
                DecimalStr(r.BaseImponibleIsc), DecimalStr(r.Isc), DecimalStr(r.BaseImponibleIvap), DecimalStr(r.Ivap),
                DecimalStr(r.OperacionesExoneradas), DecimalStr(r.OperacionesInafectas), DecimalStr(r.Igv), DecimalStr(r.Icbper),
                DecimalStr(r.OtrosTributosCargos), DecimalStr(r.ImporteTotal), DecimalStr(r.TipoCambio),
                r.FechaEmisionDocModificado, r.TipoDocModificado, r.SerieDocModificado, r.NumeroDocModificado,
                r.CodigoErrorTipo1, r.IndicadorComprobanteCancelado, r.Estado
            };

            sb.AppendJoin('|', campos).AppendLine();
        }

        return sb.ToString();
    }

    public static string GenerarComprasTxt(IEnumerable<RegistroCompra> registros)
    {
        var sb = new StringBuilder();
        foreach (var r in registros)
        {
            var campos = new[]
            {
                r.PeriodoTributario, r.Cuo, r.CorrelativoAsiento, r.FechaEmision, r.FechaVencimientoPago,
                r.TipoComprobante, r.SerieComprobante, r.AnioDuaDsi, r.NumeroComprobante, r.TipoDocIdentidadProveedor,
                r.NumeroDocIdentidadProveedor, r.RazonSocialProveedor,
                DecimalStr(r.BaseImponibleGravadaDestinoGravadas), DecimalStr(r.IgvDestinoGravadas),
                DecimalStr(r.BaseImponibleGravadaDestinoMixtas), DecimalStr(r.IgvDestinoMixtas),
                DecimalStr(r.BaseImponibleGravadaDestinoNoGravadas), DecimalStr(r.IgvDestinoNoGravadas),
                DecimalStr(r.ValorAdquisicionesNoGravadas), DecimalStr(r.Isc), DecimalStr(r.Icbper),
                DecimalStr(r.OtrosTributosCargos), DecimalStr(r.ImporteTotal), DecimalStr(r.TipoCambio),
                r.FechaEmisionDocModificado, r.TipoDocModificado, r.SerieDocModificado,
                r.CodigoDependenciaAduanera, r.NumeroDocModificado, r.NumeroConstanciaDetraccion,
                r.IndicadorSujetoRetencion, r.ClasificacionBienesServicios, r.IdentificacionContrato,
                r.CodigoErrorTipo1, r.Estado
            };

            sb.AppendJoin('|', campos).AppendLine();
        }

        return sb.ToString();
    }

    private static string DecimalStr(decimal valor)
        => valor.ToString("0.00", CultureInfo.InvariantCulture);
}
