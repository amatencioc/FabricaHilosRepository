namespace FabricaHilos.Alertas.Services;

using ClosedXML.Excel;
using FabricaHilos.Alertas.Models;

/// <summary>
/// Arma el Excel adjunto (ClosedXML) con el detalle de alertas pendientes de
/// AQUARIUS.V_SCA_ALERTA_TAREO_DETALLE, para el reporte semanal enviado a RRHH.
/// </summary>
public static class ExcelAlertaTurnoDescansoBuilder
{
    public static byte[] Construir(IReadOnlyList<AlertaTurnoDescansoDetalle> alertas)
    {
        using var workbook  = new XLWorkbook();
        var ws              = workbook.Worksheets.Add("Alertas Turno-Descanso");

        string[] headers =
        [
            "Empleado", "Inicio Semana", "Fin Semana", "Horario", "Área",
            "Encargado", "Detalle", "Fecha Detección",
        ];

        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        var headerRow = ws.Range(1, 1, 1, headers.Length);
        headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#1B4D3E");
        headerRow.Style.Font.FontColor       = XLColor.White;
        headerRow.Style.Font.Bold            = true;

        int row = 2;
        foreach (var a in alertas)
        {
            ws.Cell(row, 1).Value = a.NombreEmpleado;
            ws.Cell(row, 2).Value = a.FecIniSemana;
            ws.Cell(row, 2).Style.DateFormat.Format = "dd/MM/yyyy";
            ws.Cell(row, 3).Value = a.FecFinSemana;
            ws.Cell(row, 3).Style.DateFormat.Format = "dd/MM/yyyy";
            ws.Cell(row, 4).Value = a.HorarioDesc ?? string.Empty;
            ws.Cell(row, 5).Value = a.AreaNombre ?? string.Empty;
            ws.Cell(row, 6).Value = a.EncargadoNombre ?? string.Empty;
            ws.Cell(row, 7).Value = a.Detalle ?? string.Empty;
            ws.Cell(row, 8).Value = a.FecDeteccion;
            ws.Cell(row, 8).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";

            if (row % 2 == 0)
                ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EDEB");

            row++;
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
