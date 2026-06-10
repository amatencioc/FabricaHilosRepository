```

BenchmarkDotNet v0.15.2, Windows 11 (10.0.26200.8457)
12th Gen Intel Core i5-12400 2.50GHz, 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.300
  [Host]     : .NET 8.0.27 (8.0.2726.22922), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.27 (8.0.2726.22922), X64 RyuJIT AVX2


```
| Method                  | Mean        | Error     | StdDev     | Median      | Gen0     | Gen1     | Gen2     | Allocated  |
|------------------------ |------------:|----------:|-----------:|------------:|---------:|---------:|---------:|-----------:|
| GenerarVentasTxt_Small  |    75.00 μs |  1.271 μs |   1.127 μs |    74.76 μs |  13.9160 |   1.8311 |        - |  128.95 KB |
| GenerarVentasTxt_Large  | 5,585.14 μs | 99.756 μs | 166.670 μs | 5,562.43 μs | 328.1250 | 328.1250 | 328.1250 | 5902.95 KB |
| GenerarComprasTxt_Small |    67.37 μs |  1.045 μs |   1.910 μs |    66.65 μs |  12.9395 |   1.5869 |        - |  119.97 KB |
| GenerarComprasTxt_Large | 5,066.63 μs | 98.504 μs | 282.626 μs | 4,988.91 μs | 328.1250 | 328.1250 | 328.1250 | 5312.47 KB |
