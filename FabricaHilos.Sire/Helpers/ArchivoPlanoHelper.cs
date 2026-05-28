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
            sb.Append(r.PeriodoTributario).Append('|')
              .Append(r.Cuo).Append('|')
              .Append(r.CorrelativoAsiento).Append('|')
              .Append(r.FechaEmision).Append('|')
              .Append(r.FechaVencimientoPago).Append('|')
              .Append(r.TipoComprobante).Append('|')
              .Append(r.SerieComprobante).Append('|')
              .Append(r.AnioDuaDsi).Append('|')
              .Append(r.NumeroComprobante).Append('|')
              .Append(r.NumeroFinalComprobante).Append('|')
              .Append(r.TipoDocIdentidadCliente).Append('|')
              .Append(r.NumeroDocIdentidadCliente).Append('|')
              .Append(r.RazonSocialCliente).Append('|')
              .Append(DecimalStr(r.BaseImponibleGravada)).Append('|')
              .Append(DecimalStr(r.BaseImponibleGravadaTasaDiferenciada)).Append('|')
              .Append(DecimalStr(r.IgvTasaDiferenciada)).Append('|')
              .Append(DecimalStr(r.BaseImponibleIsc)).Append('|')
              .Append(DecimalStr(r.Isc)).Append('|')
              .Append(DecimalStr(r.BaseImponibleIvap)).Append('|')
              .Append(DecimalStr(r.Ivap)).Append('|')
              .Append(DecimalStr(r.OperacionesExoneradas)).Append('|')
              .Append(DecimalStr(r.OperacionesInafectas)).Append('|')
              .Append(DecimalStr(r.Igv)).Append('|')
              .Append(DecimalStr(r.Icbper)).Append('|')
              .Append(DecimalStr(r.OtrosTributosCargos)).Append('|')
              .Append(DecimalStr(r.ImporteTotal)).Append('|')
              .Append(DecimalStr(r.TipoCambio)).Append('|')
              .Append(r.FechaEmisionDocModificado).Append('|')
              .Append(r.TipoDocModificado).Append('|')
              .Append(r.SerieDocModificado).Append('|')
              .Append(r.NumeroDocModificado).Append('|')
              .Append(r.CodigoErrorTipo1).Append('|')
              .Append(r.IndicadorComprobanteCancelado).Append('|')
              .Append(r.Estado)
              .AppendLine();
        }

        return sb.ToString();
    }

    public static string GenerarComprasTxt(IEnumerable<RegistroCompra> registros)
    {
        var sb = new StringBuilder();
        foreach (var r in registros)
        {
            sb.Append(r.PeriodoTributario).Append('|')
              .Append(r.Cuo).Append('|')
              .Append(r.CorrelativoAsiento).Append('|')
              .Append(r.FechaEmision).Append('|')
              .Append(r.FechaVencimientoPago).Append('|')
              .Append(r.TipoComprobante).Append('|')
              .Append(r.SerieComprobante).Append('|')
              .Append(r.AnioDuaDsi).Append('|')
              .Append(r.NumeroComprobante).Append('|')
              .Append(r.TipoDocIdentidadProveedor).Append('|')
              .Append(r.NumeroDocIdentidadProveedor).Append('|')
              .Append(r.RazonSocialProveedor).Append('|')
              .Append(DecimalStr(r.BaseImponibleGravadaDestinoGravadas)).Append('|')
              .Append(DecimalStr(r.IgvDestinoGravadas)).Append('|')
              .Append(DecimalStr(r.BaseImponibleGravadaDestinoMixtas)).Append('|')
              .Append(DecimalStr(r.IgvDestinoMixtas)).Append('|')
              .Append(DecimalStr(r.BaseImponibleGravadaDestinoNoGravadas)).Append('|')
              .Append(DecimalStr(r.IgvDestinoNoGravadas)).Append('|')
              .Append(DecimalStr(r.ValorAdquisicionesNoGravadas)).Append('|')
              .Append(DecimalStr(r.Isc)).Append('|')
              .Append(DecimalStr(r.Icbper)).Append('|')
              .Append(DecimalStr(r.OtrosTributosCargos)).Append('|')
              .Append(DecimalStr(r.ImporteTotal)).Append('|')
              .Append(DecimalStr(r.TipoCambio)).Append('|')
              .Append(r.FechaEmisionDocModificado).Append('|')
              .Append(r.TipoDocModificado).Append('|')
              .Append(r.SerieDocModificado).Append('|')
              .Append(r.CodigoDependenciaAduanera).Append('|')
              .Append(r.NumeroDocModificado).Append('|')
              .Append(r.NumeroConstanciaDetraccion).Append('|')
              .Append(r.IndicadorSujetoRetencion).Append('|')
              .Append(r.ClasificacionBienesServicios).Append('|')
              .Append(r.IdentificacionContrato).Append('|')
              .Append(r.CodigoErrorTipo1).Append('|')
              .Append(r.Estado)
              .AppendLine();
        }

        return sb.ToString();
    }

    private static string DecimalStr(decimal valor)
        => valor.ToString("0.00", CultureInfo.InvariantCulture);
}
