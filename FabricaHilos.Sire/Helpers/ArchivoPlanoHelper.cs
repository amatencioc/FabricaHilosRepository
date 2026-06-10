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
        // Pre-allocate StringBuilder: ~1.2 KB por registro es conservador; usar 1KB mínimo
        const int capacidadInicialPorRegistro = 1024;
        var sb = new StringBuilder(capacidadInicialPorRegistro);

        // Buffer reutilizable para formateo decimal (línea a línea)
        var primero = true;
        foreach (var r in registros)
        {
            if (!primero)
                sb.AppendLine();

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
              .Append(r.BaseImponibleGravada.ToString("0.00", CultureInfo.InvariantCulture)).Append('|')
              .Append(r.BaseImponibleGravadaTasaDiferenciada.ToString("0.00", CultureInfo.InvariantCulture)).Append('|')
              .Append(r.IgvTasaDiferenciada.ToString("0.00", CultureInfo.InvariantCulture)).Append('|')
              .Append(r.BaseImponibleIsc.ToString("0.00", CultureInfo.InvariantCulture)).Append('|')
              .Append(r.Isc.ToString("0.00", CultureInfo.InvariantCulture)).Append('|')
              .Append(r.BaseImponibleIvap.ToString("0.00", CultureInfo.InvariantCulture)).Append('|')
              .Append(r.Ivap.ToString("0.00", CultureInfo.InvariantCulture)).Append('|')
              .Append(r.OperacionesExoneradas.ToString("0.00", CultureInfo.InvariantCulture)).Append('|')
              .Append(r.OperacionesInafectas.ToString("0.00", CultureInfo.InvariantCulture)).Append('|')
              .Append(r.Igv.ToString("0.00", CultureInfo.InvariantCulture)).Append('|')
              .Append(r.Icbper.ToString("0.00", CultureInfo.InvariantCulture)).Append('|')
              .Append(r.OtrosTributosCargos.ToString("0.00", CultureInfo.InvariantCulture)).Append('|')
              .Append(r.ImporteTotal.ToString("0.00", CultureInfo.InvariantCulture)).Append('|')
              .Append(r.TipoCambio.ToString("0.00", CultureInfo.InvariantCulture)).Append('|')
              .Append(r.FechaEmisionDocModificado).Append('|')
              .Append(r.TipoDocModificado).Append('|')
              .Append(r.SerieDocModificado).Append('|')
              .Append(r.NumeroDocModificado).Append('|')
              .Append(r.CodigoErrorTipo1).Append('|')
              .Append(r.IndicadorComprobanteCancelado).Append('|')
              .Append(r.Estado);

            primero = false;
        }

        return sb.ToString();
    }

    public static string GenerarComprasTxt(IEnumerable<RegistroCompra> registros)
    {
        const int capacidadInicialPorRegistro = 1024;
        var sb = new StringBuilder(capacidadInicialPorRegistro);

        var primero = true;
        foreach (var r in registros)
        {
            if (!primero)
                sb.AppendLine();

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
              .Append(r.BaseImponibleGravadaDestinoGravadas.ToString("0.00", CultureInfo.InvariantCulture)).Append('|')
              .Append(r.IgvDestinoGravadas.ToString("0.00", CultureInfo.InvariantCulture)).Append('|')
              .Append(r.BaseImponibleGravadaDestinoMixtas.ToString("0.00", CultureInfo.InvariantCulture)).Append('|')
              .Append(r.IgvDestinoMixtas.ToString("0.00", CultureInfo.InvariantCulture)).Append('|')
              .Append(r.BaseImponibleGravadaDestinoNoGravadas.ToString("0.00", CultureInfo.InvariantCulture)).Append('|')
              .Append(r.IgvDestinoNoGravadas.ToString("0.00", CultureInfo.InvariantCulture)).Append('|')
              .Append(r.ValorAdquisicionesNoGravadas.ToString("0.00", CultureInfo.InvariantCulture)).Append('|')
              .Append(r.Isc.ToString("0.00", CultureInfo.InvariantCulture)).Append('|')
              .Append(r.Icbper.ToString("0.00", CultureInfo.InvariantCulture)).Append('|')
              .Append(r.OtrosTributosCargos.ToString("0.00", CultureInfo.InvariantCulture)).Append('|')
              .Append(r.ImporteTotal.ToString("0.00", CultureInfo.InvariantCulture)).Append('|')
              .Append(r.TipoCambio.ToString("0.00", CultureInfo.InvariantCulture)).Append('|')
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
              .Append(r.Estado);

            primero = false;
        }

        return sb.ToString();
    }

    private static string DecimalStr(decimal valor)
        => valor.ToString("0.00", CultureInfo.InvariantCulture);
}
