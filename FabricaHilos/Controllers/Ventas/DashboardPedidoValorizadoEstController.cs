using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FabricaHilos.Models.Ventas;
using FabricaHilos.Services.Ventas;

namespace FabricaHilos.Controllers.Ventas
{
    [Authorize]
    public class DashboardPedidoValorizadoEstController : OracleBaseController
    {
        private readonly IPedidoValorizadoEstService _service;
        private readonly ILogger<DashboardPedidoValorizadoEstController> _logger;

        public DashboardPedidoValorizadoEstController(IPedidoValorizadoEstService service, ILogger<DashboardPedidoValorizadoEstController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.Vendedores = await _service.ObtenerVendedoresAsync();
            ViewBag.VendedorLogueado = await _service.ObtenerVendedorLogueadoAsync();
            return View();
        }

        // ── Listado principal (grilla) ──────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> DatosListado([FromQuery] PedidoValorizadoEstFiltroDto filtro)
        {
            var data = await _service.ListarAsync(filtro);
            return Json(data);
        }

        // ── Exportar a Excel (mismos filtros del listado en pantalla) ───────
        [HttpGet]
        public async Task<IActionResult> ExportarExcel([FromQuery] PedidoValorizadoEstFiltroDto filtro)
        {
            try
            {
                var data = await _service.ListarAsync(filtro);

                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Pedidos Valorizados");

                string[] encabezados =
                {
                    "Cliente", "Nombre", "F. Pedido", "Pedido", "Ítem", "O/Compra",
                    "Artículo", "Descripción", "Kg Stock", "Kg Pedido", "Kg Despachado", "Kg Saldo",
                    "Importe $", "F. Entrega", "Área", "Estatus", "Cond. Pago", "Anticipo Saldo $", "Sin anticipo"
                };
                for (int c = 0; c < encabezados.Length; c++)
                    ws.Cell(1, c + 1).Value = encabezados[c];
                ws.Row(1).Style.Font.Bold = true;
                ws.Row(1).Style.Fill.BackgroundColor = XLColor.FromArgb(0x1B, 0x4D, 0x3E);
                ws.Row(1).Style.Font.FontColor = XLColor.White;

                int fila = 2;
                foreach (var d in data)
                {
                    var estatusParts = (d.EstatusDescripcion ?? "").Split('|');
                    var area = estatusParts.Length > 0 ? estatusParts[0].Trim() : "";
                    var estatus = estatusParts.Length > 1 ? estatusParts[1].Trim() : "";

                    ws.Cell(fila, 1).Value  = d.CodCliente;
                    ws.Cell(fila, 2).Value  = d.Nombre;
                    ws.Cell(fila, 3).Value  = d.Fecha;
                    ws.Cell(fila, 4).Value  = d.NumPed;
                    ws.Cell(fila, 5).Value  = d.Nro;
                    ws.Cell(fila, 6).Value  = d.NumeroRef;
                    ws.Cell(fila, 7).Value  = d.CodArt;
                    ws.Cell(fila, 8).Value  = d.Descripcion;
                    ws.Cell(fila, 9).Value  = d.StockLote;
                    ws.Cell(fila, 10).Value = d.Cantidad;
                    ws.Cell(fila, 11).Value = d.Despachado;
                    ws.Cell(fila, 12).Value = d.Saldo;
                    ws.Cell(fila, 13).Value = d.Soles;
                    ws.Cell(fila, 14).Value = d.Entrega;
                    ws.Cell(fila, 15).Value = area;
                    ws.Cell(fila, 16).Value = estatus;
                    ws.Cell(fila, 17).Value = d.CPago;
                    ws.Cell(fila, 18).Value = d.AnticipoSaldo;
                    ws.Cell(fila, 19).Value = d.IndSinAnticipo == "S" ? "Sin anticipo" : "";

                    ws.Cell(fila, 3).Style.DateFormat.Format  = "dd/MM/yyyy";
                    ws.Cell(fila, 14).Style.DateFormat.Format = "dd/MM/yyyy";
                    ws.Cell(fila, 9).Style.NumberFormat.Format  = "#,##0.00";
                    ws.Cell(fila, 10).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(fila, 11).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(fila, 12).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(fila, 13).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(fila, 18).Style.NumberFormat.Format = "#,##0.00";

                    if (d.IndSinAnticipo == "S")
                        ws.Row(fila).Style.Fill.BackgroundColor = XLColor.FromArgb(0xFD, 0xE3, 0xEF);

                    fila++;
                }

                ws.Columns().AdjustToContents();
                ws.SheetView.FreezeRows(1);

                using var ms = new MemoryStream();
                wb.SaveAs(ms);
                ms.Position = 0;

                var fileName = $"PedidosValorizadosEstado_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
                return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al exportar a Excel el listado de Pedidos Valorizados/Estado");
                return StatusCode(500, "Error al generar el archivo Excel.");
            }
        }

        // ── Autocompletado select2: clientes ────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> BuscarClientes(string term)
        {
            var data = await _service.BuscarClientesAsync(term ?? "");
            return Json(new { results = data });
        }

        // ── Autocompletado select2: artículos ───────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> BuscarArticulos(string term)
        {
            var data = await _service.BuscarArticulosAsync(term ?? "");
            return Json(new { results = data });
        }
    }
}
