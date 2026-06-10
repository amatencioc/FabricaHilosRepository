using BenchmarkDotNet.Attributes;
using FabricaHilos.Sire.Helpers;
using FabricaHilos.Sire.Models;
using System.Collections.Generic;
using Microsoft.VSDiagnostics;

namespace FabricaHilos.Sire.Benchmarks;
[MemoryDiagnoser]
public class SirePerformanceBenchmarks
{
    private List<RegistroVenta> _ventasSmall;
    private List<RegistroVenta> _ventasLarge;
    private List<RegistroCompra> _comprasSmall;
    private List<RegistroCompra> _comprasLarge;
    [GlobalSetup]
    public void Setup()
    {
        // Dataset pequeño (100 registros)
        _ventasSmall = GenerarVentasRegistros(100);
        _comprasSmall = GenerarComprasRegistros(100);
        // Dataset grande (5000 registros)
        _ventasLarge = GenerarVentasRegistros(5000);
        _comprasLarge = GenerarComprasRegistros(5000);
    }

    /// <summary>
    /// Benchmark: Generación de archivo plano de VENTAS (pequeño 100 registros)
    /// Métrica: Asignaciones GC y tiempo de ejecución
    /// </summary>
    [Benchmark]
    public string GenerarVentasTxt_Small() => ArchivoPlanoHelper.GenerarVentasTxt(_ventasSmall);
    /// <summary>
    /// Benchmark: Generación de archivo plano de VENTAS (grande 5000 registros)
    /// Métrica: Estrés de memoria y eficiencia StringBuilder
    /// </summary>
    [Benchmark]
    public string GenerarVentasTxt_Large() => ArchivoPlanoHelper.GenerarVentasTxt(_ventasLarge);
    /// <summary>
    /// Benchmark: Generación de archivo plano de COMPRAS (pequeño 100 registros)
    /// </summary>
    [Benchmark]
    public string GenerarComprasTxt_Small() => ArchivoPlanoHelper.GenerarComprasTxt(_comprasSmall);
    /// <summary>
    /// Benchmark: Generación de archivo plano de COMPRAS (grande 5000 registros)
    /// </summary>
    [Benchmark]
    public string GenerarComprasTxt_Large() => ArchivoPlanoHelper.GenerarComprasTxt(_comprasLarge);
    // ─── Helpers para generar datos de prueba ───────────────────────────────
    private static List<RegistroVenta> GenerarVentasRegistros(int cantidad)
    {
        var registros = new List<RegistroVenta>(cantidad);
        for (int i = 1; i <= cantidad; i++)
        {
            registros.Add(new RegistroVenta { PeriodoTributario = "202401", Cuo = i.ToString().PadLeft(4, '0'), CorrelativoAsiento = i.ToString(), FechaEmision = "01/01/2024", FechaVencimientoPago = "01/02/2024", TipoComprobante = "01", SerieComprobante = "F001", AnioDuaDsi = "0", NumeroComprobante = i.ToString().PadLeft(8, '0'), NumeroFinalComprobante = i.ToString().PadLeft(8, '0'), TipoDocIdentidadCliente = "01", NumeroDocIdentidadCliente = "12345678901", RazonSocialCliente = $"Cliente {i}", BaseImponibleGravada = 1000.00m, BaseImponibleGravadaTasaDiferenciada = 0.00m, IgvTasaDiferenciada = 0.00m, BaseImponibleIsc = 0.00m, Isc = 0.00m, BaseImponibleIvap = 0.00m, Ivap = 0.00m, OperacionesExoneradas = 0.00m, OperacionesInafectas = 0.00m, Igv = 180.00m, Icbper = 0.00m, OtrosTributosCargos = 0.00m, ImporteTotal = 1180.00m, TipoCambio = 1.00m, FechaEmisionDocModificado = "", TipoDocModificado = "", SerieDocModificado = "", NumeroDocModificado = "", CodigoErrorTipo1 = "0", IndicadorComprobanteCancelado = "0", Estado = "1" });
        }

        return registros;
    }

    private static List<RegistroCompra> GenerarComprasRegistros(int cantidad)
    {
        var registros = new List<RegistroCompra>(cantidad);
        for (int i = 1; i <= cantidad; i++)
        {
            registros.Add(new RegistroCompra { PeriodoTributario = "202401", Cuo = i.ToString().PadLeft(4, '0'), CorrelativoAsiento = i.ToString(), FechaEmision = "01/01/2024", FechaVencimientoPago = "01/02/2024", TipoComprobante = "01", SerieComprobante = "F001", AnioDuaDsi = "0", NumeroComprobante = i.ToString().PadLeft(8, '0'), TipoDocIdentidadProveedor = "01", NumeroDocIdentidadProveedor = "20123456789", RazonSocialProveedor = $"Proveedor {i}", BaseImponibleGravadaDestinoGravadas = 1000.00m, IgvDestinoGravadas = 180.00m, BaseImponibleGravadaDestinoMixtas = 0.00m, IgvDestinoMixtas = 0.00m, BaseImponibleGravadaDestinoNoGravadas = 0.00m, IgvDestinoNoGravadas = 0.00m, ValorAdquisicionesNoGravadas = 0.00m, Isc = 0.00m, Icbper = 0.00m, OtrosTributosCargos = 0.00m, ImporteTotal = 1180.00m, TipoCambio = 1.00m, FechaEmisionDocModificado = "", TipoDocModificado = "", SerieDocModificado = "", CodigoDependenciaAduanera = "", NumeroDocModificado = "", NumeroConstanciaDetraccion = "" });
        }

        return registros;
    }
}