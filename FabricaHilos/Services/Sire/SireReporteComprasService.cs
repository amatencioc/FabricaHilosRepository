using ClosedXML.Excel;
using FabricaHilos.Models.Sire;
using FabricaHilos.Notificaciones.Abstractions;
using FabricaHilos.Notificaciones.Models.Payloads;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FabricaHilos.Services.Sire;

/// <summary>
/// Genera un Excel con los documentos "Solo SUNAT" del período (excluyendo proveedores
/// configurados) y lo envía por correo usando el sistema de templates del proyecto Notificaciones.
/// </summary>
public sealed class SireReporteComprasService
{
    private readonly SireReporteComprasOptions          _opts;
    private readonly IEmailNotificacionService          _email;
    private readonly ILogger<SireReporteComprasService> _logger;

    public SireReporteComprasService(
        IOptions<SireReporteComprasOptions>  opts,
        IEmailNotificacionService            email,
        ILogger<SireReporteComprasService>   logger)
    {
        _opts   = opts.Value;
        _email  = email;
        _logger = logger;
    }

    /// <summary>
    /// Genera el Excel con los registros SOLO_SUNAT filtrados y envía el correo.
    /// Devuelve un mensaje de resultado para mostrar al usuario.
    /// </summary>
    public async Task<(bool Ok, string Mensaje)> EnviarReporteAsync(
        string                         periodo,
        IEnumerable<SireConcilDetalle>  registros,
        string                         usuarioActual,
        CancellationToken              ct = default)
    {
        var rucsExcluidos = new HashSet<string>(
            _opts.RucsExcluidos, StringComparer.OrdinalIgnoreCase);

        var soloSunat = registros
            .Where(r => r.Estado == "SOLO_SUNAT"
                        && !rucsExcluidos.Contains(r.Ruc ?? string.Empty))
            .OrderBy(r => r.FEmision)
            .ThenBy(r => r.Serie)
            .ThenBy(r => r.Numero)
            .ToList();

        if (soloSunat.Count == 0)
            return (false, "No hay documentos 'Solo SUNAT' para incluir en el reporte (o todos pertenecen a proveedores excluidos).");

        using var wb     = GenerarExcel(soloSunat, periodo);
        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        var excelBytes = stream.ToArray();

        var periodoLabel = periodo.Length == 6
            ? $"{periodo[..4]}/{periodo[4..]}"
            : periodo;

        var nombreArchivo = $"SIRE_RCE_SoloSUNAT_{periodoLabel.Replace("/", "")}.xlsx";

        var totalBase    = soloSunat.Sum(r => r.SunatBase);
        var totalIgv     = soloSunat.Sum(r => r.SunatIgv);
        var totalImporte = soloSunat.Sum(r => r.SunatTotal);

        var provExcl = _opts.RucsExcluidos.Count > 0
            ? string.Join(", ", _opts.RucsExcluidos)
            : "Ninguno";

        var payload = new SireReporteComprasPayload
        {
            CorreoDestinatario  = _opts.DestinatarioA.FirstOrDefault() ?? string.Empty,
            NombreDestinatario  = "Equipo de Contabilidad",
            CorreosCopia        = _opts.DestinatarosCc.ToList(),
            CorreosTo           = _opts.DestinatarioA.Skip(1).ToList(),
            Periodo             = periodoLabel,
            CantDocumentos      = soloSunat.Count.ToString("N0"),
            TotalBase           = totalBase.ToString("N2"),
            TotalIgv            = totalIgv.ToString("N2"),
            TotalImporte        = totalImporte.ToString("N2"),
            ProveedoresExcluidos= provExcl,
            GeneradoPor         = usuarioActual,
            NombreArchivo       = nombreArchivo,
            ArchivoExcel        = excelBytes,
        };

        var ok = await _email.EnviarAsync(payload, ct);

        if (ok)
        {
            _logger.LogInformation(
                "[SIRE] Reporte Solo SUNAT enviado: período={Periodo}, filas={Filas}, destinatario={Dest}",
                periodo, soloSunat.Count, _opts.DestinatarioA);
            var todosDestinatarios = string.Join(", ", _opts.DestinatarioA);
            return (true, $"Reporte enviado a {todosDestinatarios} con {soloSunat.Count} documentos.");
        }

        return (false, "Error al enviar el correo. Revise los logs del sistema.");
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Genera los bytes del Excel Solo SUNAT <b>incluyendo</b> los proveedores que normalmente
    /// se excluyen del correo (RucsExcluidos del appsettings), para descarga directa.
    /// Devuelve los bytes y el nombre de archivo sugerido.
    /// </summary>
    public (byte[] Bytes, string NombreArchivo) GenerarBytesParaDescarga(
        string                         periodo,
        IEnumerable<SireConcilDetalle> registros)
    {
        var soloSunat = registros
            .Where(r => r.Estado == "SOLO_SUNAT")
            .OrderBy(r => r.FEmision)
            .ThenBy(r => r.Serie)
            .ThenBy(r => r.Numero)
            .ToList();

        var periodoLabel  = periodo.Length == 6
            ? $"{periodo[..4]}/{periodo[4..]}"
            : periodo;
        var nombreArchivo = $"SIRE_RCE_SoloSUNAT_Completo_{periodoLabel.Replace("/", "")}.xlsx";

        using var wb     = GenerarExcel(soloSunat, periodo);
        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return (stream.ToArray(), nombreArchivo);
    }

    private static XLWorkbook GenerarExcel(IList<SireConcilDetalle> rows, string periodo)
    {
        var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Solo SUNAT");

        // Cabecera
        var headers = new[]
        {
            "Tipo", "Serie", "Número", "F. Emisión", "RUC", "Proveedor",
            "Moneda", "Base SUNAT", "IGV SUNAT", "Total SUNAT"
        };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1C3A5E");
            cell.Style.Font.FontColor       = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // Datos
        int row = 2;
        foreach (var r in rows)
        {
            ws.Cell(row, 1).Value = r.Tipdoc ?? "-";
            ws.Cell(row, 2).Value = r.Serie   ?? "-";
            ws.Cell(row, 3).Value = r.Numero  ?? "-";
            ws.Cell(row, 4).Value = r.FEmision.HasValue
                ? r.FEmision.Value.ToString("dd/MM/yyyy")
                : "-";
            ws.Cell(row, 5).Value = r.Ruc    ?? "-";
            ws.Cell(row, 6).Value = r.Nombre ?? "-";
            ws.Cell(row, 7).Value = r.SunatMoneda ?? r.LegMoneda ?? "PEN";
            ws.Cell(row, 8).Value = r.SunatBase;
            ws.Cell(row, 9).Value = r.SunatIgv;
            ws.Cell(row, 10).Value = r.SunatTotal;

            ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 9).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 10).Style.NumberFormat.Format = "#,##0.00";

            // Alternancia de filas
            if (row % 2 == 0)
            {
                ws.Range(row, 1, row, 10)
                  .Style.Fill.BackgroundColor = XLColor.FromHtml("#F0F4F8");
            }
            row++;
        }

        // Totales
        ws.Cell(row, 5).Value = "TOTAL";
        ws.Cell(row, 5).Style.Font.Bold = true;
        ws.Cell(row, 8).FormulaA1 = $"=SUM(H2:H{row - 1})";
        ws.Cell(row, 9).FormulaA1 = $"=SUM(I2:I{row - 1})";
        ws.Cell(row, 10).FormulaA1 = $"=SUM(J2:J{row - 1})";
        ws.Range(row, 1, row, 10).Style.Font.Bold = true;
        ws.Range(row, 8, row, 10).Style.NumberFormat.Format = "#,##0.00";

        ws.Columns().AdjustToContents(minWidth: 8, maxWidth: 50);
        ws.SheetView.FreezeRows(1);

        return wb;
    }
}

