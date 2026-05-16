using ClosedXML.Excel;
using FabricaHilos.Models.Logistica;
using FabricaHilos.Services.Logistica;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FabricaHilos.Controllers.Logistica;

[Authorize]
[Route("Logistica/Indicadores")]
public class IndicadoresLogisticaController : OracleBaseController
{
    private readonly IIndLogisticaService _service;
    private readonly ILogger<IndicadoresLogisticaController> _logger;

    public IndicadoresLogisticaController(
        IIndLogisticaService service,
        ILogger<IndicadoresLogisticaController> logger)
    {
        _service = service;
        _logger  = logger;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public IActionResult Index()
    {
        var hoy = DateTime.Today;
        var ini = new DateTime(hoy.Year, hoy.Month, 1);
        ViewBag.FechaDesde = ini.ToString("yyyy-MM-dd");
        ViewBag.FechaHasta = hoy.ToString("yyyy-MM-dd");
        return View("~/Views/Logistica/Indicadores/Index.cshtml");
    }

    [HttpGet("Dashboard")]
    public async Task<IActionResult> Dashboard(DateTime fechaDesde, DateTime fechaHasta)
    {
        if (fechaDesde == default || fechaHasta == default)
            return BadRequest("Debe indicar un rango de fechas válido.");
        if (fechaDesde > fechaHasta)
            return BadRequest("La fecha inicial no puede ser mayor que la fecha final.");
        try
        {
            var vm = await _service.ObtenerDashboardAsync(fechaDesde, fechaHasta);
            return PartialView("~/Views/Logistica/Indicadores/_KpiDashboard.cshtml", vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener dashboard logistico ({Desde} - {Hasta})", fechaDesde, fechaHasta);
            return StatusCode(500, "Error al obtener los datos. Intente nuevamente.");
        }
    }

    [HttpGet("CicloVida")]
    public async Task<IActionResult> CicloVida(DateTime fechaDesde, DateTime fechaHasta)
    {
        if (fechaDesde == default || fechaHasta == default)
            return BadRequest("Debe indicar un rango de fechas válido.");
        if (fechaDesde > fechaHasta)
            return BadRequest("La fecha inicial no puede ser mayor que la fecha final.");
        try
        {
            var vm = await _service.ObtenerCicloVidaAsync(fechaDesde, fechaHasta);
            return PartialView("~/Views/Logistica/Indicadores/_CicloVida.cshtml", vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener ciclo de vida ({Desde} - {Hasta})", fechaDesde, fechaHasta);
            return StatusCode(500, "Error al obtener los datos. Intente nuevamente.");
        }
    }

    [HttpGet("TendenciaMensual")]
    public async Task<IActionResult> TendenciaMensual(int mesesAtras = 12)
    {
        if (mesesAtras <= 0 || mesesAtras > 60)
            return BadRequest("El parámetro mesesAtras debe estar entre 1 y 60.");
        try
        {
            var vm = await _service.ObtenerTendenciaMensualAsync(mesesAtras);
            return PartialView("~/Views/Logistica/Indicadores/_TendenciaMensual.cshtml", vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener tendencia mensual ({Meses} meses)", mesesAtras);
            return StatusCode(500, "Error al obtener los datos. Intente nuevamente.");
        }
    }

    [HttpGet("ExportarExcel")]
    public async Task<IActionResult> ExportarExcel(DateTime fechaDesde, DateTime fechaHasta)
    {
        if (fechaDesde == default || fechaHasta == default)
            return BadRequest("Debe indicar un rango de fechas válido.");
        if (fechaDesde > fechaHasta)
            return BadRequest("La fecha inicial no puede ser mayor que la fecha final.");
        try
        {
            var datos = await _service.ObtenerDetalleAsync(fechaDesde, fechaHasta);

            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Indicadores Logistica");

            string[] headers =
            [
                "Tipo", "Nro. Req.", "Fecha", "F. Autoriza", "F. Recibe",
                "Orden Compra", "Fch. Orden", "Destino", "Descripcion Destino",
                "Solicita", "Observacion", "Cod. Articulo", "Descripcion Art.",
                "Unidad", "Cantidad", "Cant. Desp.", "Saldo",
                "P. Unit.", "Sub Total", "IGV", "Total", "Estado"
            ];

            for (int i = 0; i < headers.Length; i++)
                ws.Cell(1, i + 1).Value = headers[i];

            var headerRange = ws.Range(1, 1, 1, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1e3a5f");
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            int row = 2;
            foreach (var d in datos)
            {
                ws.Cell(row, 1).Value  = d.Tipo         ?? "";
                ws.Cell(row, 2).Value  = (double)d.NumReq;
                ws.Cell(row, 3).Value  = d.Fecha?.ToString("dd/MM/yyyy")     ?? "";
                ws.Cell(row, 4).Value  = d.FAutoriza?.ToString("dd/MM/yyyy") ?? "";
                ws.Cell(row, 5).Value  = d.FRecibe?.ToString("dd/MM/yyyy")   ?? "";
                ws.Cell(row, 6).Value  = d.OrdenCompra  ?? "";
                ws.Cell(row, 7).Value  = d.FchOrden?.ToString("dd/MM/yyyy")  ?? "";
                ws.Cell(row, 8).Value  = d.Destino      ?? "";
                ws.Cell(row, 9).Value  = d.DescDestino  ?? "";
                ws.Cell(row, 10).Value = d.Solicita     ?? "";
                ws.Cell(row, 11).Value = d.Observacion  ?? "";
                ws.Cell(row, 12).Value = d.CodArt       ?? "";
                ws.Cell(row, 13).Value = d.DescArticulo ?? "";
                ws.Cell(row, 14).Value = d.Unidad       ?? "";
                ws.Cell(row, 15).Value = (double)d.Cantidad;
                ws.Cell(row, 16).Value = (double)d.CantDesp;
                ws.Cell(row, 17).Value = (double)d.Saldo;
                ws.Cell(row, 18).Value = (double)d.PUnit;
                ws.Cell(row, 19).Value = (double)d.SubTotal;
                ws.Cell(row, 20).Value = (double)d.Igv;
                ws.Cell(row, 21).Value = (double)d.Total;
                ws.Cell(row, 22).Value = d.Estado ?? "";

                var fillColor = d.Estado?.ToUpperInvariant() switch
                {
                    "ATENDIDO" => XLColor.FromHtml("#d4edda"),
                    "ANULADO"  => XLColor.FromHtml("#f8d7da"),
                    _ when string.Equals(d.Tipo, "SERVICIO", StringComparison.OrdinalIgnoreCase) => XLColor.FromHtml("#d1ecf1"),
                    _ => XLColor.NoColor
                };
                if (fillColor != XLColor.NoColor)
                    ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = fillColor;

                row++;
            }

            ws.Columns().AdjustToContents();
            ws.SheetView.FreezeRows(1);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            ms.Position = 0;

            string fileName = $"IndicadoresLogistica_{fechaDesde:yyyyMMdd}_{fechaHasta:yyyyMMdd}.xlsx";
            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar Excel de indicadores logisticos");
            return StatusCode(500, $"Error al generar el archivo: {ex.Message}");
        }
    }
}
