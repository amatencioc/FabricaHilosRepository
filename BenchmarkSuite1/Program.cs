using BenchmarkDotNet.Running;
using FabricaHilos.Sire.Benchmarks;

namespace BenchmarkSuite1
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<SirePerformanceBenchmarks>();
        }
    }
}
