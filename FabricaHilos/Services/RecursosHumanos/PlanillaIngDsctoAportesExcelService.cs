using ClosedXML.Excel;
using FabricaHilos.Models.RecursosHumanos;

namespace FabricaHilos.Services.RecursosHumanos;

public interface IPlanillaIngDsctoAportesExcelService
{
    byte[] GenerarExcel(
        List<PlanillaIngDsctoAportesDto> datos,
        ResumenPagoBancoReporteDto resumenBanco,
        List<ResumenPagoCcostoDto> resumenCcosto,
        LiquidacionesReporteDto? liquidaciones,
        int anio, int semana, string ceo = "O", int? mes = null);
}

public class PlanillaIngDsctoAportesExcelService : IPlanillaIngDsctoAportesExcelService
{
    public byte[] GenerarExcel(
        List<PlanillaIngDsctoAportesDto> datos,
        ResumenPagoBancoReporteDto resumenBanco,
        List<ResumenPagoCcostoDto> resumenCcosto,
        LiquidacionesReporteDto? liquidaciones,
        int anio, int semana, string ceo = "O", int? mes = null)
    {
        using var wb = new XLWorkbook();

        // ── Pestaña 1: Resumen (P_RESUMEN_PAGO_BANCO) ──────────────────────
        GenerarHojaResumenBanco(wb, resumenBanco, liquidaciones, anio, semana);

        // ── Pestaña 2: Detalle (P_INGR_DESC_APORT + P_RESUMEN_PAGO_CCOSTO) ─
        GenerarHojaDetalle(wb, datos, resumenCcosto, anio, semana, ceo, mes);

        // ── Pestaña 3: Liquidaciones (opcional, si se proporcionan) ─────────
        if (liquidaciones != null && liquidaciones.Grupos.Count > 0)
        {
            GenerarHojaLiquidaciones(wb, liquidaciones);
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void GenerarHojaResumenBanco(XLWorkbook wb, ResumenPagoBancoReporteDto reporte, LiquidacionesReporteDto? liquidaciones, int anio, int semana)
    {
        var ws = wb.AddWorksheet("Resumen");

        const int colItem = 1;
        const int colCod = 2;
        const int colNombre = 3;
        const int primeraColMes = 4; // 2 columnas por mes: Planilla semanal / Importe horas extras

        int filaActual = 1;

        ws.Cell(filaActual, 1).Value = "COLONIAL FABRICA DE HILOS S.A.";
        ws.Range(filaActual, 1, filaActual, 3).Merge();
        ws.Cell(filaActual, 1).Style.Font.Bold = true;
        filaActual += 2;

        ws.Cell(filaActual, 1).Value = reporte.Titulo;
        ws.Range(filaActual, 1, filaActual, 4).Merge();
        ws.Cell(filaActual, 1).Style.Font.Bold = true;
        ws.Cell(filaActual, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        filaActual += 2;

        if (reporte.Grupos.Count == 0)
        {
            ws.Cell(filaActual, 1).Value = "No se encontró información para el periodo consultado.";
            ws.Columns(1, 6).AdjustToContents();
            return;
        }

        var resumenPorBanco = new List<(string DescBanco, decimal TotalPlanilla, decimal Vaca, decimal Subtotal, decimal Lbs, decimal TotalPagar)>();

        foreach (var grupo in reporte.Grupos)
        {
            int totalColumnas = primeraColMes + (grupo.Meses.Count * 2) - 1 + 4;

            ws.Cell(filaActual, 1).Value = grupo.DescBanco ?? "";
            ws.Range(filaActual, 1, filaActual, totalColumnas).Merge();
            ws.Cell(filaActual, 1).Style.Font.Bold = true;
            ws.Cell(filaActual, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#dbe5f1");
            filaActual++;

            int filaHeader1 = filaActual;
            int filaHeader2 = filaActual + 1;

            ws.Cell(filaHeader1, colItem).Value = "N°";
            ws.Cell(filaHeader1, colCod).Value = "Cod.";
            ws.Cell(filaHeader1, colNombre).Value = "Nombres";
            ws.Range(filaHeader1, colItem, filaHeader2, colItem).Merge();
            ws.Range(filaHeader1, colCod, filaHeader2, colCod).Merge();
            ws.Range(filaHeader1, colNombre, filaHeader2, colNombre).Merge();

            int col = primeraColMes;
            foreach (var mes in grupo.Meses)
            {
                ws.Cell(filaHeader1, col).Value = $"PLANILLA SEMANAL {mes}";
                ws.Range(filaHeader1, col, filaHeader1, col + 1).Merge();
                ws.Cell(filaHeader2, col).Value = "IMPORTE";
                ws.Cell(filaHeader2, col + 1).Value = "IMPORTE HORAS EXTRAS";
                col += 2;
            }

            ws.Cell(filaHeader1, col).Value = "TOTAL";
            ws.Cell(filaHeader2, col).Value = "SEMANA TOTAL";
            ws.Range(filaHeader1, col, filaHeader2, col).Merge();
            int colTotal = col;

            int colPagoVaca = colTotal + 1;
            int colLbs = colTotal + 2;
            int colDepositar = colTotal + 3;

            ws.Cell(filaHeader1, colPagoVaca).Value = "PAGO VACA";
            ws.Range(filaHeader1, colPagoVaca, filaHeader2, colPagoVaca).Merge();
            ws.Cell(filaHeader1, colLbs).Value = "LBS";
            ws.Range(filaHeader1, colLbs, filaHeader2, colLbs).Merge();
            ws.Cell(filaHeader1, colDepositar).Value = "TOTAL A DEPOSITAR";
            ws.Range(filaHeader1, colDepositar, filaHeader2, colDepositar).Merge();

            var headerRange = ws.Range(filaHeader1, colItem, filaHeader2, colDepositar);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1e3a5f");
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            headerRange.Style.Alignment.WrapText = true;
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            filaActual = filaHeader2 + 1;

            foreach (var fila in grupo.Filas)
            {
                ws.Cell(filaActual, colItem).Value = fila.Item;
                if (long.TryParse(fila.CCodper, out var codPer))
                    ws.Cell(filaActual, colCod).Value = codPer;
                else
                    ws.Cell(filaActual, colCod).Value = fila.CCodper ?? "";
                ws.Cell(filaActual, colCod).Style.NumberFormat.Format = "0";
                ws.Cell(filaActual, colNombre).Value = fila.Nombre;

                int c = primeraColMes;
                foreach (var monto in fila.Montos)
                {
                    ws.Cell(filaActual, c).Value = (double)monto.PlanillaSemanal;
                    ws.Cell(filaActual, c + 1).Value = (double)monto.ImporteExtra;
                    c += 2;
                }

                ws.Cell(filaActual, colTotal).Value = (double)fila.TotalSemana;
                ws.Cell(filaActual, colPagoVaca).Value = (double)fila.ImpVacac;
                ws.Cell(filaActual, colDepositar).Value = (double)(fila.TotalSemana + fila.ImpVacac);

                var numRange = ws.Range(filaActual, primeraColMes, filaActual, colDepositar);
                numRange.Style.NumberFormat.Format = "#,##0.00;;\"-\"";
                var bordeFila = ws.Range(filaActual, colItem, filaActual, colDepositar);
                bordeFila.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                bordeFila.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                filaActual++;
            }

            // Fila de totales de planilla del banco
            ws.Cell(filaActual, colNombre).Value = "TOTAL PLANILLA";
            ws.Cell(filaActual, colNombre).Style.Font.Bold = true;
            int ct = primeraColMes;
            foreach (var totMes in grupo.TotalesPorMes)
            {
                ws.Cell(filaActual, ct).Value = (double)totMes.PlanillaSemanal;
                ws.Cell(filaActual, ct + 1).Value = (double)totMes.ImporteExtra;
                ct += 2;
            }
            ws.Cell(filaActual, colTotal).Value = (double)grupo.TotalGeneral;
            ws.Cell(filaActual, colPagoVaca).Value = (double)grupo.TotalImpVacac;

            var grupoLiqui = liquidaciones?.Grupos.FirstOrDefault(gl =>
                (!string.IsNullOrEmpty(gl.CBanco) && gl.CBanco == grupo.CBanco) ||
                (!string.IsNullOrEmpty(gl.DescBanco) && gl.DescBanco == grupo.DescBanco));

            var totalLbs = grupoLiqui?.TotalGeneral ?? 0m;
            var totalDepositarPlanilla = grupo.TotalGeneral + grupo.TotalImpVacac;
            ws.Cell(filaActual, colDepositar).Value = (double)totalDepositarPlanilla;

            var totalRange = ws.Range(filaActual, primeraColMes, filaActual, colDepositar);
            totalRange.Style.NumberFormat.Format = "#,##0.00;;\"-\"";
            var totalRow = ws.Range(filaActual, colItem, filaActual, colDepositar);
            totalRow.Style.Font.Bold = true;
            totalRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#dbe5f1");
            totalRow.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            totalRow.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            filaActual += 2;

            // ── Bloque LIQUIDACIONES del mismo banco (continúa la misma tabla) ──
            if (grupoLiqui != null && grupoLiqui.Filas.Count > 0)
            {
                ws.Cell(filaActual, 1).Value = "LIQUIDACIONES";
                ws.Cell(filaActual, 1).Style.Font.Bold = true;
                ws.Cell(filaActual, 1).Style.Font.FontColor = XLColor.Red;
                filaActual++;

                foreach (var fl in grupoLiqui.Filas)
                {
                    ws.Cell(filaActual, colItem).Value = fl.Item;
                    if (long.TryParse(fl.CCodper, out var codPerLiq))
                        ws.Cell(filaActual, colCod).Value = codPerLiq;
                    else
                        ws.Cell(filaActual, colCod).Value = fl.CCodper ?? "";
                    ws.Cell(filaActual, colCod).Style.NumberFormat.Format = "0";
                    ws.Cell(filaActual, colNombre).Value = fl.Nombre;
                    ws.Cell(filaActual, colLbs).Value = (double)fl.Total;
                    ws.Cell(filaActual, colDepositar).Value = (double)fl.Total;

                    var liqNumRange = ws.Range(filaActual, colLbs, filaActual, colDepositar);
                    liqNumRange.Style.NumberFormat.Format = "#,##0.00;;\"-\"";
                    var liqBorde = ws.Range(filaActual, colItem, filaActual, colDepositar);
                    liqBorde.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    liqBorde.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                    filaActual++;
                }

                ws.Cell(filaActual, colTotal).Value = "-";
                ws.Cell(filaActual, colPagoVaca).Value = "-";
                ws.Cell(filaActual, colLbs).Value = (double)totalLbs;
                ws.Cell(filaActual, colDepositar).Value = (double)totalLbs;
                var liqTotalRange = ws.Range(filaActual, colLbs, filaActual, colDepositar);
                liqTotalRange.Style.NumberFormat.Format = "#,##0.00;;\"-\"";
                var liqTotalRow = ws.Range(filaActual, colItem, filaActual, colDepositar);
                liqTotalRow.Style.Font.Bold = true;
                liqTotalRow.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                liqTotalRow.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                filaActual += 2;
            }

            var totalDepositarFinal = grupo.TotalGeneral + grupo.TotalImpVacac + totalLbs;

            ws.Cell(filaActual, colNombre).Value = $"TOTAL A PAGAR {grupo.DescBanco}";
            ws.Cell(filaActual, colTotal).Value = (double)grupo.TotalGeneral;
            ws.Cell(filaActual, colPagoVaca).Value = (double)grupo.TotalImpVacac;
            ws.Cell(filaActual, colLbs).Value = (double)totalLbs;
            ws.Cell(filaActual, colDepositar).Value = (double)totalDepositarFinal;
            var totalPagarRange = ws.Range(filaActual, colTotal, filaActual, colDepositar);
            totalPagarRange.Style.NumberFormat.Format = "#,##0.00;;\"-\"";
            var totalPagarRow = ws.Range(filaActual, colItem, filaActual, colDepositar);
            totalPagarRow.Style.Font.Bold = true;
            totalPagarRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#92d050");
            totalPagarRow.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            totalPagarRow.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            resumenPorBanco.Add((
                grupo.DescBanco ?? $"BANCO {grupo.CBanco}",
                grupo.TotalGeneral,
                grupo.TotalImpVacac,
                grupo.TotalGeneral + grupo.TotalImpVacac,
                totalLbs,
                totalDepositarFinal));

            filaActual += 3; // espacio entre bancos
        }

        if (resumenPorBanco.Count > 0)
        {
            filaActual += 1;

            ws.Cell(filaActual, 1).Value = "RESUMEN POR BANCO";
            ws.Cell(filaActual, 1).Style.Font.Bold = true;
            filaActual++;

            int filaHeaderResumen = filaActual;
            ws.Cell(filaHeaderResumen, 1).Value = "BANCOS";
            ws.Cell(filaHeaderResumen, 2).Value = "TOTAL PLANILLA";
            ws.Cell(filaHeaderResumen, 3).Value = "VACA";
            ws.Cell(filaHeaderResumen, 4).Value = "TOTAL PLANILLA";
            ws.Cell(filaHeaderResumen, 5).Value = "LBS";
            ws.Cell(filaHeaderResumen, 6).Value = "TOTAL A PAGAR";

            var headerResumenRange = ws.Range(filaHeaderResumen, 1, filaHeaderResumen, 6);
            headerResumenRange.Style.Font.Bold = true;
            headerResumenRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#e2efda");
            headerResumenRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerResumenRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerResumenRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            filaActual++;

            decimal tTotalPlanilla = 0, tVaca = 0, tSubtotal = 0, tLbs = 0, tTotalPagar = 0;

            foreach (var r in resumenPorBanco)
            {
                ws.Cell(filaActual, 1).Value = r.DescBanco;
                ws.Cell(filaActual, 2).Value = (double)r.TotalPlanilla;
                ws.Cell(filaActual, 3).Value = (double)r.Vaca;
                ws.Cell(filaActual, 4).Value = (double)r.Subtotal;
                ws.Cell(filaActual, 4).Style.Fill.BackgroundColor = XLColor.FromHtml("#f2f2f2");
                ws.Cell(filaActual, 5).Value = (double)r.Lbs;
                ws.Cell(filaActual, 6).Value = (double)r.TotalPagar;

                var numRangeResumen = ws.Range(filaActual, 2, filaActual, 6);
                numRangeResumen.Style.NumberFormat.Format = "#,##0.00;;\"-\"";
                var bordeResumen = ws.Range(filaActual, 1, filaActual, 6);
                bordeResumen.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                bordeResumen.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                tTotalPlanilla += r.TotalPlanilla;
                tVaca += r.Vaca;
                tSubtotal += r.Subtotal;
                tLbs += r.Lbs;
                tTotalPagar += r.TotalPagar;

                filaActual++;
            }

            ws.Cell(filaActual, 1).Value = "TOTAL";
            ws.Cell(filaActual, 2).Value = (double)tTotalPlanilla;
            ws.Cell(filaActual, 3).Value = (double)tVaca;
            ws.Cell(filaActual, 4).Value = (double)tSubtotal;
            ws.Cell(filaActual, 5).Value = (double)tLbs;
            ws.Cell(filaActual, 6).Value = (double)tTotalPagar;

            var totalResumenRange = ws.Range(filaActual, 2, filaActual, 6);
            totalResumenRange.Style.NumberFormat.Format = "#,##0.00;;\"-\"";
            var totalResumenRow = ws.Range(filaActual, 1, filaActual, 6);
            totalResumenRow.Style.Font.Bold = true;
            totalResumenRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#92d050");
            totalResumenRow.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            totalResumenRow.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            filaActual++;
        }

        ws.Columns(1, 16).AdjustToContents();
        ws.SheetView.FreezeRows(0);
    }

    private static void GenerarHojaDetalle(
        XLWorkbook wb,
        List<PlanillaIngDsctoAportesDto> datos,
        List<ResumenPagoCcostoDto> resumenCcosto,
        int anio, int semana, string ceo = "O", int? mes = null)
    {
        var ws = wb.AddWorksheet("Detalle");

        var esEmpleado = string.Equals(ceo, "E", StringComparison.OrdinalIgnoreCase);
        ws.Cell(1, 1).Value = esEmpleado
            ? $"Planilla de Ingreso y Descuento de Aportes - Año {anio} / Mes {mes:00}"
            : $"Planilla de Ingreso y Descuento de Aportes - Año {anio} / Semana {semana}";
        ws.Range(1, 1, 1, 3).Merge();
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 13;

        string[] headers =
        [
            "N°", "Cod. Per.", "Nombre",
            "Horas", "Basico Tarifa", "Basico", "Dominical", "2do Turno", "3er Turno",
            "H.E.25%", "H.E.100", "Prima Textil", "DL.2598(1)", "Asig.Fam", "Asig.Fam Ley",
            "Movilidad", "Colacion", "H/Extr(35%)", "D.M.Enf", "Bon.Vac", "D.M.Acc", "Lic.C/H",
            "Tot.Ingreso",
            "Dscto.Judicial", "Dscto.Sindical", "Tardanza", "Dscto.Medico", "Cuot.Prestamo", "Dscto.Comedor",
            "S.N.P", "5ta.Cat", "AFP 10", "AFP Com", "AFP Seg",
            "Tot.Dscto", "Neto"
        ];

        int headerRow = 3;
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(headerRow, i + 1).Value = headers[i];

        var headerRange = ws.Range(headerRow, 1, headerRow, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1e3a5f");
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRange.Style.Alignment.WrapText = true;

        int row = headerRow + 1;
        int nro = 1;
        foreach (var d in datos)
        {
            int c = 1;
            ws.Cell(row, c++).Value = d.EsTotal ? "" : (nro++).ToString();
            int colCodPerDetalle = c;
            if (!d.EsTotal && long.TryParse(d.CCodper, out var codPerDetalle))
                ws.Cell(row, c++).Value = codPerDetalle;
            else
            {
                ws.Cell(row, c++).Value = d.CCodper ?? "";
            }
            ws.Cell(row, c++).Value = d.Nombre;
            if (!d.EsTotal)
                ws.Cell(row, c).Value = (double)d.Horas;
            c++;
            ws.Cell(row, c++).Value = (double)d.BasicoTarifa;
            ws.Cell(row, c++).Value = (double)d.Basico;
            ws.Cell(row, c++).Value = (double)d.Dominical;
            ws.Cell(row, c++).Value = (double)d.Turno2;
            ws.Cell(row, c++).Value = (double)d.Turno3;
            ws.Cell(row, c++).Value = (double)d.He25;
            ws.Cell(row, c++).Value = (double)d.He100;
            ws.Cell(row, c++).Value = (double)d.PrimaTextil;
            ws.Cell(row, c++).Value = (double)d.Dl25981;
            ws.Cell(row, c++).Value = (double)d.AsigFam;
            ws.Cell(row, c++).Value = (double)d.AsigFamLey;
            ws.Cell(row, c++).Value = (double)d.Movilidad;
            ws.Cell(row, c++).Value = (double)d.Colacion;
            ws.Cell(row, c++).Value = (double)d.He35;
            ws.Cell(row, c++).Value = (double)d.DmEnfermedad;
            ws.Cell(row, c++).Value = (double)d.BonVac;
            ws.Cell(row, c++).Value = (double)d.DmAccidente;
            ws.Cell(row, c++).Value = (double)d.LicCh;
            ws.Cell(row, c++).Value = (double)d.TotIngreso;
            ws.Cell(row, c++).Value = (double)d.DsctoJudicial;
            ws.Cell(row, c++).Value = (double)d.DsctoSindical;
            ws.Cell(row, c++).Value = (double)d.Tardanza;
            ws.Cell(row, c++).Value = (double)d.DsctoMedico;
            ws.Cell(row, c++).Value = (double)d.CuotPrestamo;
            ws.Cell(row, c++).Value = (double)d.DsctoComedor;
            ws.Cell(row, c++).Value = (double)d.Snp;
            ws.Cell(row, c++).Value = (double)d.QuintaCat;
            ws.Cell(row, c++).Value = (double)d.Afp10;
            ws.Cell(row, c++).Value = (double)d.AfpCom;
            ws.Cell(row, c++).Value = (double)d.AfpSeg;
            ws.Cell(row, c++).Value = (double)d.TotDscto;
            ws.Cell(row, c++).Value = (double)d.Neto;

            ws.Cell(row, colCodPerDetalle).Style.NumberFormat.Format = "0";

            if (d.EsTotal)
            {
                var totalRange = ws.Range(row, 1, row, headers.Length);
                totalRange.Style.Font.Bold = true;
                totalRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#d9e1f2");
            }

            row++;
        }

        if (row > headerRow + 1)
        {
            var numRange = ws.Range(headerRow + 1, 4, row - 1, headers.Length);
            numRange.Style.NumberFormat.Format = "#,##0.00";
        }

        ws.Columns(1, headers.Length).AdjustToContents();
        ws.SheetView.FreezeRows(headerRow);

        // ── Bloque adicional: Resumen por Gran Centro de Costo ─────────────
        row += 2;
        int ccostoTituloRow = row;
        ws.Cell(ccostoTituloRow, 1).Value = "Resumen de Pago por Gran Centro de Costo";
        ws.Range(ccostoTituloRow, 1, ccostoTituloRow, 9).Merge();
        ws.Cell(ccostoTituloRow, 1).Style.Font.Bold = true;
        ws.Cell(ccostoTituloRow, 1).Style.Font.FontSize = 12;

        string[] headersCcosto =
        [
            "Gran Centro de Costo", "Cant.", "Imp. Día Lab.", "Imp. Asig.", "Subtotal",
            "Hor. Extra", "Imp. Extra", "Imp. Total", "%HE"
        ];

        int ccostoHeaderRow = ccostoTituloRow + 2;
        for (int i = 0; i < headersCcosto.Length; i++)
            ws.Cell(ccostoHeaderRow, i + 1).Value = headersCcosto[i];

        var ccostoHeaderRange = ws.Range(ccostoHeaderRow, 1, ccostoHeaderRow, headersCcosto.Length);
        ccostoHeaderRange.Style.Font.Bold = true;
        ccostoHeaderRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1e3a5f");
        ccostoHeaderRange.Style.Font.FontColor = XLColor.White;
        ccostoHeaderRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ccostoHeaderRange.Style.Alignment.WrapText = true;

        int ccostoRow = ccostoHeaderRow + 1;
        decimal totCant = 0, totImpDiaLab = 0, totImpAsig = 0, totSubtotal = 0, totHorExtra = 0, totImpExtra = 0, totImpTot = 0;

        // %HE = Imp. Extra de la fila / Imp. Extra TOTAL de todos los centros de costo.
        totImpExtra = resumenCcosto.Sum(d => d.ImpExtra);

        foreach (var d in resumenCcosto)
        {
            var pctHe = totImpExtra != 0 ? (d.ImpExtra / totImpExtra) : 0;

            int c = 1;
            ws.Cell(ccostoRow, c++).Value = d.DescGranCcosto ?? "";
            ws.Cell(ccostoRow, c++).Value = (double)d.Cant;
            ws.Cell(ccostoRow, c++).Value = (double)d.ImpDiaLab;
            ws.Cell(ccostoRow, c++).Value = (double)d.ImpAsig;
            ws.Cell(ccostoRow, c++).Value = (double)d.Subtotal;
            ws.Cell(ccostoRow, c++).Value = (double)d.HorExtra;
            ws.Cell(ccostoRow, c++).Value = (double)d.ImpExtra;
            ws.Cell(ccostoRow, c++).Value = (double)d.ImpTot;
            ws.Cell(ccostoRow, c++).Value = (double)pctHe;
            ws.Cell(ccostoRow, c - 1).Style.NumberFormat.Format = "0.00%";

            totCant += d.Cant;
            totImpDiaLab += d.ImpDiaLab;
            totImpAsig += d.ImpAsig;
            totSubtotal += d.Subtotal;
            totHorExtra += d.HorExtra;
            totImpTot += d.ImpTot;

            ccostoRow++;
        }

        if (ccostoRow > ccostoHeaderRow + 1)
        {
            var numRangeCcosto = ws.Range(ccostoHeaderRow + 1, 2, ccostoRow - 1, 8);
            numRangeCcosto.Style.NumberFormat.Format = "#,##0.00";

            var pctTotal = totImpExtra != 0 ? 1m : 0;

            int c = 1;
            ws.Cell(ccostoRow, c++).Value = "";
            ws.Cell(ccostoRow, c++).Value = (double)totCant;
            ws.Cell(ccostoRow, c++).Value = (double)totImpDiaLab;
            ws.Cell(ccostoRow, c++).Value = (double)totImpAsig;
            ws.Cell(ccostoRow, c++).Value = (double)totSubtotal;
            ws.Cell(ccostoRow, c++).Value = (double)totHorExtra;
            ws.Cell(ccostoRow, c++).Value = (double)totImpExtra;
            ws.Cell(ccostoRow, c++).Value = (double)totImpTot;
            ws.Cell(ccostoRow, c).Value = (double)pctTotal;
            ws.Cell(ccostoRow, c).Style.NumberFormat.Format = "0.00%";

            var totalRowRange = ws.Range(ccostoRow, 1, ccostoRow, headersCcosto.Length);
            totalRowRange.Style.Font.Bold = true;
            totalRowRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#92d050");
            totalRowRange.Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(ccostoRow, headersCcosto.Length).Style.NumberFormat.Format = "0.00%";
        }

        ws.Columns(1, headersCcosto.Length).AdjustToContents();
    }

    private static void GenerarHojaLiquidaciones(XLWorkbook wb, LiquidacionesReporteDto reporte)
    {
        var ws = wb.AddWorksheet("Liquidaciones");

        const int colItem = 1;
        const int colCod = 2;
        const int colNombre = 3;
        const int colPagoVaca = 4;
        const int colLbs = 5;
        const int colTotal = 6;

        int filaActual = 1;

        // Encabezado principal
        ws.Cell(filaActual, 1).Value = "COLONIAL FABRICA DE HILOS S.A.";
        ws.Range(filaActual, 1, filaActual, colTotal).Merge();
        ws.Cell(filaActual, 1).Style.Font.Bold = true;
        filaActual += 2;

        ws.Cell(filaActual, 1).Value = reporte.Titulo;
        ws.Range(filaActual, 1, filaActual, colTotal).Merge();
        ws.Cell(filaActual, 1).Style.Font.Bold = true;
        ws.Cell(filaActual, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        filaActual += 2;

        if (reporte.Grupos.Count == 0)
        {
            ws.Cell(filaActual, 1).Value = "No se encontró información de liquidaciones.";
            ws.Columns(1, colTotal).AdjustToContents();
            return;
        }

        // Procesar cada grupo (banco)
        foreach (var grupo in reporte.Grupos)
        {
            // Encabezado del banco
            ws.Cell(filaActual, 1).Value = $"BANCO: {grupo.DescBanco ?? grupo.CBanco}";
            ws.Range(filaActual, 1, filaActual, colTotal).Merge();
            ws.Cell(filaActual, 1).Style.Font.Bold = true;
            ws.Cell(filaActual, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#dbe5f1");
            filaActual++;

            // Encabezados de columnas
            ws.Cell(filaActual, colItem).Value = "N°";
            ws.Cell(filaActual, colCod).Value = "Cod.";
            ws.Cell(filaActual, colNombre).Value = "Nombres";
            ws.Cell(filaActual, colPagoVaca).Value = "Pago Vaca";
            ws.Cell(filaActual, colLbs).Value = "Lbs";
            ws.Cell(filaActual, colTotal).Value = "Total";

            var headerRange = ws.Range(filaActual, colItem, filaActual, colTotal);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1e3a5f");
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            filaActual++;

            // Filas de datos
            foreach (var fila in grupo.Filas)
            {
                ws.Cell(filaActual, colItem).Value = fila.Item;
                ws.Cell(filaActual, colCod).Value = fila.CCodper ?? "";
                ws.Cell(filaActual, colCod).Style.NumberFormat.Format = "0";
                ws.Cell(filaActual, colNombre).Value = fila.Nombre;
                ws.Cell(filaActual, colPagoVaca).Value = (double)fila.PagoVacac;
                ws.Cell(filaActual, colLbs).Value = (double)fila.PagoCts;
                ws.Cell(filaActual, colTotal).Value = (double)fila.Total;

                var numRange = ws.Range(filaActual, colPagoVaca, filaActual, colTotal);
                numRange.Style.NumberFormat.Format = "#,##0.00";

                var bordeFila = ws.Range(filaActual, colItem, filaActual, colTotal);
                bordeFila.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                bordeFila.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                filaActual++;
            }

            // Fila de totales del banco
            ws.Cell(filaActual, colNombre).Value = "TOTAL";
            ws.Cell(filaActual, colNombre).Style.Font.Bold = true;
            ws.Cell(filaActual, colPagoVaca).Value = (double)grupo.TotalPagoVacac;
            ws.Cell(filaActual, colLbs).Value = (double)grupo.TotalPagoCts;
            ws.Cell(filaActual, colTotal).Value = (double)grupo.TotalGeneral;

            var totalRange = ws.Range(filaActual, colPagoVaca, filaActual, colTotal);
            totalRange.Style.NumberFormat.Format = "#,##0.00";

            var totalRow = ws.Range(filaActual, colItem, filaActual, colTotal);
            totalRow.Style.Font.Bold = true;
            totalRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#92d050"); // Verde
            totalRow.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            totalRow.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            filaActual += 2; // Espacio entre bancos
        }

        ws.Columns(1, colTotal).AdjustToContents();
    }
}
