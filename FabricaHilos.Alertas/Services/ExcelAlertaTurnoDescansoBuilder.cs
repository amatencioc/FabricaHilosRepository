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
            "ID Alerta", "Empresa", "Cod. Personal", "Empleado", "Tipo Alerta", "Descripción Alerta",
            "Inicio Semana", "Fin Semana", "Turno", "Turno Descripción", "Horario",
            "Hora Ingreso Teórica", "Hora Salida Teórica", "Centro de Costo", "Área",
            "Encargado", "Días Descanso", "Detalle", "Fecha Detección", "Estado",
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
            ws.Cell(row, 1).Value  = a.IdAlerta;
            ws.Cell(row, 2).Value  = a.CodEmpresa;
            ws.Cell(row, 3).Value  = a.CodPersonal;
            ws.Cell(row, 4).Value  = a.NombreEmpleado;
            ws.Cell(row, 5).Value  = a.TipAlerta;
            ws.Cell(row, 6).Value  = a.TipAlertaDesc;
            ws.Cell(row, 7).Value  = a.FecIniSemana;
            ws.Cell(row, 7).Style.DateFormat.Format = "dd/MM/yyyy";
            ws.Cell(row, 8).Value  = a.FecFinSemana;
            ws.Cell(row, 8).Style.DateFormat.Format = "dd/MM/yyyy";
            ws.Cell(row, 9).Value  = a.TurnoCod ?? string.Empty;
            ws.Cell(row, 10).Value = a.TurnoDescripcion ?? string.Empty;
            ws.Cell(row, 11).Value = a.HorarioDesc ?? string.Empty;
            ws.Cell(row, 12).Value = a.HoraIngresoTeorica ?? string.Empty;
            ws.Cell(row, 13).Value = a.HoraSalidaTeorica ?? string.Empty;
            ws.Cell(row, 14).Value = a.CentroCostoNombre ?? string.Empty;
            ws.Cell(row, 15).Value = a.AreaNombre ?? string.Empty;
            ws.Cell(row, 16).Value = a.EncargadoNombre ?? string.Empty;
            ws.Cell(row, 17).Value = a.DiasDescanso ?? 0;
            ws.Cell(row, 18).Value = a.Detalle ?? string.Empty;
            ws.Cell(row, 19).Value = a.FecDeteccion;
            ws.Cell(row, 19).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
            ws.Cell(row, 20).Value = a.Estado;

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
