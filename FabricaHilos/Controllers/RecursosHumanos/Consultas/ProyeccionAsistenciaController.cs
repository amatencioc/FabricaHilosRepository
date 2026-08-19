using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FabricaHilos.Models.RecursosHumanos;
using FabricaHilos.Services.RecursosHumanos;

namespace FabricaHilos.Controllers.RecursosHumanos.Consultas
{
    [Authorize]
    [Route("RecursosHumanos/ProyeccionAsistencia")]
    public class ProyeccionAsistenciaController : OracleBaseController
    {
        private readonly IProyeccionAsistenciaService _proyeccionAsistenciaService;
        private readonly ILogger<ProyeccionAsistenciaController> _logger;

        public ProyeccionAsistenciaController(
            IProyeccionAsistenciaService proyeccionAsistenciaService,
            ILogger<ProyeccionAsistenciaController> logger)
        {
            _proyeccionAsistenciaService = proyeccionAsistenciaService;
            _logger                      = logger;
        }

        // ========== INDEX — Vista de consulta ==========

        [HttpGet("")]
        [HttpGet("Index")]
        public IActionResult Index()
        {
            return View("~/Views/RecursosHumanos/Consultas/ProyeccionAsistencia/Index.cshtml");
        }

        // ========== CONSULTAR (AJAX) ==========

        [HttpGet("Consultar")]
        public async Task<IActionResult> Consultar(string? fecha, string? empresa = null)
        {
            if (!DateTime.TryParse(fecha, out var fechaConsulta))
                return Json(new { ok = false, mensaje = "Fecha inválida." });

            try
            {
                var (ok, mensaje, resumen, detalle) = await _proyeccionAsistenciaService.ConsultarAsync(
                    fechaConsulta.Date, string.IsNullOrWhiteSpace(empresa) ? null : empresa.Trim());

                if (!ok)
                    return Json(new { ok = false, mensaje });

                return Json(new { ok = true, resumen, detalle });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ProyeccionAsistencia.Consultar: {Fecha} ({Empresa})", fecha, empresa);
                return Json(new { ok = false, mensaje = "Error al consultar la proyección de asistencia." });
            }
        }

        // ========== EXPORTAR EXCEL ==========
        // Recibe exactamente las filas ya filtradas del lado del cliente (mismo criterio de
        // búsqueda visible en pantalla) y genera un Excel con las mismas columnas de la tabla.

        [HttpPost("ExportarExcel")]
        public IActionResult ExportarExcel([FromBody] ProyeccionAsistenciaExportarExcelRequest request)
        {
            try
            {
                var filas = request?.Filas ?? new List<ProyeccionAsistenciaExportarFilaDto>();

                using var workbook  = new XLWorkbook();
                var ws = workbook.Worksheets.Add("Proyección Asistencia");

                var encabezados = new[]
                {
                    "Nombre", "Gran C. Costo", "Centro de Costo", "Encargado", "Turno - Horario",
                    "Ingreso Asignado", "Salida Asignada", "Horas Asignadas",
                    "Entrada Trabajada", "Salida Trabajada", "Horas Trabajadas",
                    "Estado", "Detalle Evento", "Feriado"
                };

                for (int i = 0; i < encabezados.Length; i++)
                {
                    var cell = ws.Cell(1, i + 1);
                    cell.Value = encabezados[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                }

                int row = 2;
                foreach (var f in filas)
                {
                    var turnoHorario = $"{f.Turno} - {f.HorarioDescripcion}".Trim(' ', '-');

                    ws.Cell(row, 1).Value  = f.NombreCompleto;
                    ws.Cell(row, 2).Value  = f.GranCcostoNombre;
                    ws.Cell(row, 3).Value  = f.CcostoNombre;
                    ws.Cell(row, 4).Value  = f.EncargadoNombre;
                    ws.Cell(row, 5).Value  = turnoHorario;
                    ws.Cell(row, 6).Value  = f.HoraIngresoTeorica;
                    ws.Cell(row, 7).Value  = f.HoraSalidaTeorica;
                    ws.Cell(row, 8).Value  = f.HorasTrabajo;
                    ws.Cell(row, 9).Value  = f.HoraIngresoReal;
                    ws.Cell(row, 10).Value = f.HoraSalidaReal;
                    ws.Cell(row, 11).Value = f.HorasTrabajadasReal;
                    ws.Cell(row, 12).Value = f.Estado;
                    ws.Cell(row, 13).Value = f.EventoDescripcion;
                    ws.Cell(row, 14).Value = f.Feriado == "F" ? "Feriado" : "";
                    row++;
                }

                ws.Columns().AdjustToContents();
                ws.SheetView.FreezeRows(1);

                using var ms = new MemoryStream();
                workbook.SaveAs(ms);
                ms.Position = 0;

                var fecha    = request?.Fecha;
                var fileName = $"ProyeccionAsistencia_{fecha}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
                return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al exportar Excel de ProyeccionAsistencia");
                return StatusCode(500, "Error al generar el archivo Excel.");
            }
        }
    }
}
