using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FabricaHilos.Services.Ventas;

namespace FabricaHilos.Controllers.Ventas
{
    [Authorize]
    public class DashboardComercialMaestroController : OracleBaseController
    {
        private readonly IDashboardComercialMaestroService _service;

        public DashboardComercialMaestroController(IDashboardComercialMaestroService service)
        {
            _service = service;
        }

        public IActionResult Index() => View();

        // ── Endpoint principal: devuelve todo el dashboard en una sola llamada ──
        [HttpGet]
        public async Task<IActionResult> DatosDashboard(
            DateTime? fechaInicio, DateTime? fechaFin, string? moneda, int top = 3)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _service.ObtenerDashboardAsync(fi, ff, moneda ?? "D", top);
            return Json(data);
        }

        // ── Endpoint de detalle: clientes de un asesor (desde clic en pie chart) ──
        [HttpGet]
        public async Task<IActionResult> DatosClientesPorAsesor(
            DateTime? fechaInicio, DateTime? fechaFin, string? moneda, string? asesor)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            if (string.IsNullOrEmpty(asesor))
                return Json(Array.Empty<object>());

            var data = await _service.ObtenerClientesPorAsesorAsync(fi, ff, moneda ?? "D", asesor);
            return Json(data);
        }

        // ── Diagnóstico: contar filas del QueryPrincipal ──────────────────────
        [HttpGet]
        public async Task<IActionResult> DiagnosticoFilas(DateTime? fechaInicio, DateTime? fechaFin)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var n = await _service.DiagnosticoFilasAsync(fi, ff);
            return Json(new
            {
                fechaInicio = fi.ToString("dd/MM/yyyy"),
                fechaFin    = ff.ToString("dd/MM/yyyy"),
                filasOracle = n,
                nota        = n < 0 ? "Error - revisar logs del servidor" : $"{n} filas devueltas por COUNT(*) directo en Oracle"
            });
        }

        private static (DateTime fi, DateTime ff) ResolverFechas(DateTime? fi, DateTime? ff)
        {
            var f2 = ff ?? DateTime.Today;
            var f1 = fi ?? new DateTime(f2.Year, 1, 1);
            return (f1, f2);
        }
    }
}
