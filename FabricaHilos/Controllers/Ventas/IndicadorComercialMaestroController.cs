using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FabricaHilos.Services.Ventas;

namespace FabricaHilos.Controllers.Ventas
{
    [Authorize]
    public class IndicadorComercialMaestroController : OracleBaseController
    {
        private readonly IIndicadorComercialMaestroService _service;

        public IndicadorComercialMaestroController(IIndicadorComercialMaestroService service)
        {
            _service = service;
        }

        public IActionResult Index() => View();

        // Endpoint único: devuelve importe, kg y clientes en una sola respuesta
        // para evitar múltiples viajes a Oracle (reutiliza la misma carga interna).
        [HttpGet]
        public async Task<IActionResult> DatosTodos(
            DateTime? fechaInicio, DateTime? fechaFin, string? moneda)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var mon = moneda ?? "D";
            var importe  = await _service.ObtenerImportePorAsesorMesAsync(fi, ff, mon);
            var kg       = await _service.ObtenerKgPorAsesorMesAsync(fi, ff);
            var clientes = await _service.ObtenerClientesPorAsesorMesAsync(fi, ff);
            return Json(new { importe, kg, clientes });
        }

        // Endpoints individuales mantenidos por compatibilidad
        [HttpGet]
        public async Task<IActionResult> DatosImporte(
            DateTime? fechaInicio, DateTime? fechaFin, string? moneda)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _service.ObtenerImportePorAsesorMesAsync(fi, ff, moneda ?? "D");
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosKg(
            DateTime? fechaInicio, DateTime? fechaFin)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _service.ObtenerKgPorAsesorMesAsync(fi, ff);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosClientes(
            DateTime? fechaInicio, DateTime? fechaFin)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _service.ObtenerClientesPorAsesorMesAsync(fi, ff);
            return Json(data);
        }

        private static (DateTime fi, DateTime ff) ResolverFechas(DateTime? fi, DateTime? ff)
        {
            var f2 = ff ?? DateTime.Today;
            var f1 = fi ?? new DateTime(f2.Year, 1, 1);
            return (f1, f2);
        }
    }
}
