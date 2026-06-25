using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using FabricaHilos.Models.Produccion.Planeamiento;
using Microsoft.VSDiagnostics;

[ShortRunJob]
[CPUUsageDiagnoser]
public class RegistroPedidoKpiBenchmark
{
    private IReadOnlyList<RegistroPedidoItem> _items = null!;
    [Params(200, 1000)]
    public int ItemCount;
    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(42);
        var servs = new[]
        {
            "C",
            "CT",
            "ST",
            "STD",
            "STR",
            "SRT",
            "SR"
        };
        var list = new List<RegistroPedidoItem>(ItemCount);
        for (int i = 0; i < ItemCount; i++)
        {
            list.Add(new RegistroPedidoItem { NumPed = 100000 + (i / 3), Cantidad = (decimal)(rng.NextDouble() * 500 + 10), CodServ = servs[rng.Next(servs.Length)], FchPedido = DateTime.Today.AddDays(-rng.Next(30)), FMaxPed = rng.Next(2) == 0 ? DateTime.Today.AddDays(rng.Next(-5, 30)) : null, });
        }

        _items = list.AsReadOnly();
    }

    // ── Baseline: propiedades calculadas independientes (patrón actual) ──────
    [Benchmark(Baseline = true)]
    public (int, int, decimal, decimal, decimal, int, int) KpisMultiPass()
    {
        var vm = new RegistroPedidosViewModel
        {
            Items = _items
        };
        var p1 = vm.TotalPedidos;
        var p2 = vm.TotalItems;
        var p3 = vm.TotalKg;
        var p4 = vm.KgHilanderia;
        var p5 = vm.KgTintoreria;
        var p6 = vm.PedidosHoy;
        var p7 = vm.ItemsConVencido;
        return (p1, p2, (decimal)p3, (decimal)p4, (decimal)p5, p6, p7);
    }

    // ── Propuesta: un único bucle acumulador ──────────────────────────────────
    [Benchmark]
    public (int, int, decimal, decimal, decimal, int, int) KpisSinglePass()
    {
        var items = _items;
        var pedidos = new HashSet<long>(items.Count);
        var pedHoy = new HashSet<long>();
        decimal totalKg = 0, kgHil = 0, kgTint = 0;
        int vencido = 0;
        var hoy = DateTime.Today;
        foreach (var x in items)
        {
            pedidos.Add(x.NumPed);
            totalKg += x.Cantidad;
            if (x.CodServ == "C" || x.CodServ == "CT")
                kgHil += x.Cantidad;
            if (x.CodServ == "CT" || x.CodServ == "ST" || x.CodServ == "STD" || x.CodServ == "STR" || x.CodServ == "SRT")
                kgTint += x.Cantidad;
            if (x.FchPedido.Date == hoy)
                pedHoy.Add(x.NumPed);
            if (x.FMaxPed.HasValue && x.FMaxPed.Value < hoy)
                vencido++;
        }

        return (pedidos.Count, items.Count, totalKg, kgHil, kgTint, pedHoy.Count, vencido);
    }
}