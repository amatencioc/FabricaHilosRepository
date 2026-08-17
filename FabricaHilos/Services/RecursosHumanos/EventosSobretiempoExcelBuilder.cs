using ClosedXML.Excel;
using FabricaHilos.Models.RecursosHumanos;
using System.Globalization;

namespace FabricaHilos.Services.RecursosHumanos;

/// <summary>
/// Genera el libro Excel de exportación de "Eventos vs Sobretiempo por Área"
/// (/RecursosHumanos/EventosSobretiempo), replicando en hojas separadas las mismas
/// tablas que se muestran en el dashboard (_KpiDashboard.cshtml): Resumen Comparativo,
/// Detalle por Área, Detalle por Centro de Costo, Detalle por Empleado, Consolidado
/// de Eventos y las 2 Proyecciones de Bolsa de HE. Mismo criterio de columnas que la
/// vista (sin HE Banco, ya oculta en la UI).
/// </summary>
public static class EventosSobretiempoExcelBuilder
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("es-PE");

    private static string NombreMes(int m) => Culture.DateTimeFormat.GetMonthName(m);

    private static readonly XLColor HeaderBg  = XLColor.FromHtml("#1B4D3E");
    private static readonly XLColor HeaderFg  = XLColor.White;
    private static readonly XLColor GroupBg   = XLColor.FromHtml("#e8f0e9");
    private static readonly XLColor TotalBg   = XLColor.FromHtml("#dcebe1");

    public static XLWorkbook Construir(EventosSobretiempoKpiViewModel vm)
    {
        var wb = new XLWorkbook();

        var periodos = vm.Resumen.OrderBy(r => r.Ano).ThenBy(r => r.Mes).ToList();

        static bool TieneDatosArea(EventosSobretiempoAreaMesDto a) =>
            a.TotalTrabajadores != 0 || a.HorasProduccion != 0 || a.MontoProduccion != 0 ||
            a.TotalHorasExtras != 0 || a.HorasHe != 0 || a.He25 != 0 || a.He35 != 0 || a.He100 != 0 ||
            a.TrabajadoresConEvento != 0 || a.DiasEvento != 0 ||
            a.HorasHeEvento != 0 || a.HorasHeNecesidad != 0 || a.MontoHeEvento != 0 || a.MontoHeNecesidad != 0;

        static bool TieneDatosCc(EventosSobretiempoCentroCostoMesDto c) =>
            c.TotalTrabajadores != 0 || c.HorasProduccion != 0 || c.MontoProduccion != 0 ||
            c.TotalHorasExtras != 0 || c.HorasHe != 0 || c.He25 != 0 || c.He35 != 0 || c.He100 != 0 ||
            c.TrabajadoresConEvento != 0 || c.DiasEvento != 0 ||
            c.HorasHeEvento != 0 || c.HorasHeNecesidad != 0 || c.MontoHeEvento != 0 || c.MontoHeNecesidad != 0;

        var areas = vm.Areas.GroupBy(a => a.Area).Where(g => g.Any(TieneDatosArea)).OrderBy(g => g.Key).Select(g => g.Key).ToList();
        var centrosConDatos = vm.CentrosCosto.GroupBy(c => (c.GranCcosto, c.CentroCosto)).Where(g => g.Any(TieneDatosCc)).Select(g => g.Key).ToHashSet();

        HojaCaratula(wb, vm, periodos);
        if (periodos.Count > 1) HojaResumenComparativo(wb, vm, periodos);
        HojaDetalleArea(wb, vm, periodos, areas);
        HojaDetalleCentroCosto(wb, vm, periodos, areas);
        HojaDetalleEmpleado(wb, vm, periodos, areas);
        HojaConsolidadoEventos(wb, vm);
        HojaProyeccionArea(wb, vm, areas);
        HojaProyeccionCentroCosto(wb, vm, areas, centrosConDatos);

        return wb;
    }

    private static void EstilizarEncabezado(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = HeaderBg;
        range.Style.Font.FontColor = HeaderFg;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
    }

    private static void Finalizar(IXLWorksheet ws, int firstDataRow, int lastRow, int lastCol)
    {
        if (lastRow >= firstDataRow)
        {
            ws.Range(firstDataRow, 1, lastRow, lastCol).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(firstDataRow, 1, lastRow, lastCol).Style.Border.InsideBorder = XLBorderStyleValues.Hair;
        }
        ws.SheetView.FreezeRows(firstDataRow - 1);
        ws.Columns().AdjustToContents();
    }

    // ── Carátula ──────────────────────────────────────────────────────
    private static void HojaCaratula(XLWorkbook wb, EventosSobretiempoKpiViewModel vm, List<EventosSobretiempoResumenMesDto> periodos)
    {
        var ws = wb.Worksheets.Add("Resumen");
        ws.Cell(1, 1).Value = "Eventos vs Sobretiempo por Área";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        string periodo = vm.AnoIni == vm.AnoFin && vm.MesIni == vm.MesFin
            ? $"{NombreMes(vm.MesFin)} de {vm.AnoFin}"
            : $"{NombreMes(vm.MesIni)} {vm.AnoIni} — {NombreMes(vm.MesFin)} {vm.AnoFin}";
        ws.Cell(2, 1).Value = "Periodo: " + periodo;

        int row = 3;
        if (!string.IsNullOrEmpty(vm.GranCcostoLabel))  { ws.Cell(row, 1).Value = "Gran Centro de Costo: " + vm.GranCcostoLabel; row++; }
        if (!string.IsNullOrEmpty(vm.CentroCostoLabel)) { ws.Cell(row, 1).Value = "Centro de Costo: " + vm.CentroCostoLabel; row++; }

        if (vm.Advertencias.Count > 0)
        {
            row++;
            ws.Cell(row, 1).Value = "Advertencias:";
            ws.Cell(row, 1).Style.Font.Bold = true;
            row++;
            foreach (var adv in vm.Advertencias)
            {
                ws.Cell(row, 1).Value = "• " + adv;
                row++;
            }
        }

        row += 2;
        decimal gTotalHE       = periodos.Sum(r => r.TotalHorasExtras);
        int     gTrabConEvento = periodos.Sum(r => r.TrabajadoresConEvento);
        int     gDiasEvento    = periodos.Sum(r => r.DiasEvento);
        int     gTotalTrabSum  = periodos.Sum(r => r.TotalTrabajadores);
        decimal gPctConHE      = gTotalTrabSum > 0 ? Math.Round((decimal)vm.Areas.Sum(a => a.TrabajadoresConHe) / gTotalTrabSum * 100, 1) : 0m;

        ws.Cell(row, 1).Value = "Total Sobretiempo (S/.)";
        ws.Cell(row, 2).Value = (double)gTotalHE;
        row++;
        ws.Cell(row, 1).Value = "% Trabajadores con HE";
        ws.Cell(row, 2).Value = (double)gPctConHE / 100.0;
        ws.Cell(row, 2).Style.NumberFormat.Format = "0.0%";
        row++;
        ws.Cell(row, 1).Value = "Trabajadores con Evento";
        ws.Cell(row, 2).Value = gTrabConEvento;
        row++;
        ws.Cell(row, 1).Value = "Días Evento";
        ws.Cell(row, 2).Value = gDiasEvento;

        ws.Columns().AdjustToContents();
    }

    // ── Resumen Comparativo por Período ──────────────────────────────
    private static void HojaResumenComparativo(XLWorkbook wb, EventosSobretiempoKpiViewModel vm, List<EventosSobretiempoResumenMesDto> periodos)
    {
        var ws = wb.Worksheets.Add("Resumen Comparativo");
        string[] headers = { "Periodo", "Horas Producción (h)", "Horas Producción (S/.)", "Sobretiempo (h)", "Sobretiempo (S/.)", "Trabajadores", "Trab. con Evento", "Días Evento" };
        for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
        EstilizarEncabezado(ws.Range(1, 1, 1, headers.Length));

        int row = 2;
        foreach (var p in periodos)
        {
            ws.Cell(row, 1).Value = $"{NombreMes(p.Mes)} {p.Ano}";
            ws.Cell(row, 2).Value = (double)p.HorasProduccion;
            ws.Cell(row, 3).Value = (double)p.MontoProduccion;
            ws.Cell(row, 4).Value = (double)p.HorasHe;
            ws.Cell(row, 5).Value = (double)p.TotalHorasExtras;
            ws.Cell(row, 6).Value = p.TotalTrabajadores;
            ws.Cell(row, 7).Value = p.TrabajadoresConEvento;
            ws.Cell(row, 8).Value = p.DiasEvento;
            row++;
        }

        ws.Cell(row, 1).Value = "Total acumulado";
        ws.Cell(row, 2).Value = (double)periodos.Sum(p => p.HorasProduccion);
        ws.Cell(row, 3).Value = (double)periodos.Sum(p => p.MontoProduccion);
        ws.Cell(row, 4).Value = (double)periodos.Sum(p => p.HorasHe);
        ws.Cell(row, 5).Value = (double)periodos.Sum(p => p.TotalHorasExtras);
        ws.Cell(row, 6).Value = periodos.Sum(p => p.TotalTrabajadores);
        ws.Cell(row, 7).Value = periodos.Sum(p => p.TrabajadoresConEvento);
        ws.Cell(row, 8).Value = periodos.Sum(p => p.DiasEvento);
        ws.Range(row, 1, row, headers.Length).Style.Font.Bold = true;
        ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = TotalBg;

        Finalizar(ws, 2, row, headers.Length);
    }

    // Encabezados de métricas repetidos por período (sin Banco, igual que la vista).
    private static readonly string[] MetricasArea = {
        "Trabajadores", "Horas Producción (h)", "Horas Producción (S/.)", "HE (h)", "HE (S/.)",
        "HE 25% (S/.)", "HE 35% (S/.)", "HE 100% (S/.)",
        "HE Evento (h)", "HE Evento (S/.)", "HE Necesidad (h)", "HE Necesidad (S/.)",
        "Trab. c/Evento", "Días Evento"
    };
    private static readonly string[] MetricasCentroCosto = {
        "Trabajadores", "Horas Producción (h)", "Horas Producción (S/.)", "HE (h)", "HE (S/.)",
        "HE 25% (S/.)", "HE 35% (S/.)", "HE 100% (S/.)",
        "HE Evento (h)", "HE Evento (S/.)", "HE Necesidad (h)", "HE Necesidad (S/.)",
        "Días Evento"
    };
    private static readonly string[] MetricasEmpleado = {
        "Horas Producción (h)", "Horas Producción (S/.)", "HE (h)", "HE (S/.)",
        "HE 25% (S/.)", "HE 35% (S/.)", "HE 100% (S/.)",
        "HE Evento (h)", "HE Evento (S/.)", "HE Necesidad (h)", "HE Necesidad (S/.)",
        "Días Evento"
    };

    private static int EscribirEncabezadoPeriodos(IXLWorksheet ws, List<(int Ano, int Mes)> periodos, string[] metricas, string primeraColLabel)
    {
        ws.Cell(1, 1).Value = primeraColLabel;
        ws.Range(1, 1, 2, 1).Merge();

        int col = 2;
        foreach (var p in periodos)
        {
            ws.Cell(1, col).Value = $"{NombreMes(p.Mes)} {p.Ano}";
            ws.Range(1, col, 1, col + metricas.Length - 1).Merge();
            for (int i = 0; i < metricas.Length; i++)
            {
                ws.Cell(2, col + i).Value = metricas[i];
            }
            col += metricas.Length;
        }
        EstilizarEncabezado(ws.Range(1, 1, 2, col - 1));
        return col; // primera columna libre después de los períodos
    }

    // ── Detalle por Área ──────────────────────────────────────────────
    private static void HojaDetalleArea(XLWorkbook wb, EventosSobretiempoKpiViewModel vm, List<EventosSobretiempoResumenMesDto> periodos, List<string> areas)
    {
        var ws = wb.Worksheets.Add("Detalle por Área");
        var periodosKey = periodos.Select(p => (p.Ano, p.Mes)).ToList();
        int totalCols = EscribirEncabezadoPeriodos(ws, periodosKey, MetricasArea, "Área") - 1;

        EventosSobretiempoAreaMesDto? Get(int ano, int mes, string area) =>
            vm.Areas.FirstOrDefault(a => a.Ano == ano && a.Mes == mes && a.Area == area);

        int row = 3;
        foreach (var areaName in areas)
        {
            ws.Cell(row, 1).Value = areaName;
            int col = 2;
            foreach (var (ano, mes) in periodosKey)
            {
                var d = Get(ano, mes, areaName);
                ws.Cell(row, col + 0).Value  = d?.TotalTrabajadores ?? 0;
                ws.Cell(row, col + 1).Value  = (double)(d?.HorasProduccion ?? 0);
                ws.Cell(row, col + 2).Value  = (double)(d?.MontoProduccion ?? 0);
                ws.Cell(row, col + 3).Value  = (double)(d?.HorasHe ?? 0);
                ws.Cell(row, col + 4).Value  = (double)(d?.TotalHorasExtras ?? 0);
                ws.Cell(row, col + 5).Value  = (double)(d?.He25 ?? 0);
                ws.Cell(row, col + 6).Value  = (double)(d?.He35 ?? 0);
                ws.Cell(row, col + 7).Value  = (double)(d?.He100 ?? 0);
                ws.Cell(row, col + 8).Value  = (double)(d?.HorasHeEvento ?? 0);
                ws.Cell(row, col + 9).Value  = (double)(d?.MontoHeEvento ?? 0);
                ws.Cell(row, col + 10).Value = (double)(d?.HorasHeNecesidad ?? 0);
                ws.Cell(row, col + 11).Value = (double)(d?.MontoHeNecesidad ?? 0);
                ws.Cell(row, col + 12).Value = d?.TrabajadoresConEvento ?? 0;
                ws.Cell(row, col + 13).Value = d?.DiasEvento ?? 0;
                col += MetricasArea.Length;
            }
            row++;
        }

        // Totales
        ws.Cell(row, 1).Value = "Total";
        int c = 2;
        foreach (var (ano, mes) in periodosKey)
        {
            var rowTotal = vm.Areas.Where(a => a.Ano == ano && a.Mes == mes).ToList();
            ws.Cell(row, c + 0).Value  = rowTotal.Sum(a => a.TotalTrabajadores);
            ws.Cell(row, c + 1).Value  = (double)rowTotal.Sum(a => a.HorasProduccion);
            ws.Cell(row, c + 2).Value  = (double)rowTotal.Sum(a => a.MontoProduccion);
            ws.Cell(row, c + 3).Value  = (double)rowTotal.Sum(a => a.HorasHe);
            ws.Cell(row, c + 4).Value  = (double)rowTotal.Sum(a => a.TotalHorasExtras);
            ws.Cell(row, c + 5).Value  = (double)rowTotal.Sum(a => a.He25);
            ws.Cell(row, c + 6).Value  = (double)rowTotal.Sum(a => a.He35);
            ws.Cell(row, c + 7).Value  = (double)rowTotal.Sum(a => a.He100);
            ws.Cell(row, c + 8).Value  = (double)rowTotal.Sum(a => a.HorasHeEvento);
            ws.Cell(row, c + 9).Value  = (double)rowTotal.Sum(a => a.MontoHeEvento);
            ws.Cell(row, c + 10).Value = (double)rowTotal.Sum(a => a.HorasHeNecesidad);
            ws.Cell(row, c + 11).Value = (double)rowTotal.Sum(a => a.MontoHeNecesidad);
            ws.Cell(row, c + 12).Value = rowTotal.Sum(a => a.TrabajadoresConEvento);
            ws.Cell(row, c + 13).Value = rowTotal.Sum(a => a.DiasEvento);
            c += MetricasArea.Length;
        }
        ws.Range(row, 1, row, totalCols).Style.Font.Bold = true;
        ws.Range(row, 1, row, totalCols).Style.Fill.BackgroundColor = TotalBg;

        Finalizar(ws, 3, row, totalCols);
    }

    // ── Detalle por Centro de Costo (todas las áreas, plano) ─────────
    private static void HojaDetalleCentroCosto(XLWorkbook wb, EventosSobretiempoKpiViewModel vm, List<EventosSobretiempoResumenMesDto> periodos, List<string> areas)
    {
        var ws = wb.Worksheets.Add("Detalle por Centro Costo");
        var periodosKey = periodos.Select(p => (p.Ano, p.Mes)).ToList();

        ws.Cell(1, 1).Value = "Área";
        ws.Cell(1, 2).Value = "Centro de Costo";
        ws.Range(1, 1, 2, 1).Merge();
        ws.Range(1, 2, 2, 2).Merge();
        int col = 3;
        foreach (var (ano, mes) in periodosKey)
        {
            ws.Cell(1, col).Value = $"{NombreMes(mes)} {ano}";
            ws.Range(1, col, 1, col + MetricasCentroCosto.Length - 1).Merge();
            for (int i = 0; i < MetricasCentroCosto.Length; i++) ws.Cell(2, col + i).Value = MetricasCentroCosto[i];
            col += MetricasCentroCosto.Length;
        }
        int totalCols = col - 1;
        EstilizarEncabezado(ws.Range(1, 1, 2, totalCols));

        var grupos = vm.CentrosCosto
            .Where(c => areas.Contains(c.GranCcosto))
            .GroupBy(c => (c.GranCcosto, c.CentroCosto))
            .OrderBy(g => g.Key.GranCcosto).ThenBy(g => g.Key.CentroCosto)
            .ToList();

        int row = 3;
        foreach (var grupo in grupos)
        {
            ws.Cell(row, 1).Value = grupo.Key.GranCcosto;
            ws.Cell(row, 2).Value = grupo.Key.CentroCosto;
            int c = 3;
            foreach (var (ano, mes) in periodosKey)
            {
                var d = grupo.FirstOrDefault(x => x.Ano == ano && x.Mes == mes);
                ws.Cell(row, c + 0).Value  = d?.TotalTrabajadores ?? 0;
                ws.Cell(row, c + 1).Value  = (double)(d?.HorasProduccion ?? 0);
                ws.Cell(row, c + 2).Value  = (double)(d?.MontoProduccion ?? 0);
                ws.Cell(row, c + 3).Value  = (double)(d?.HorasHe ?? 0);
                ws.Cell(row, c + 4).Value  = (double)(d?.TotalHorasExtras ?? 0);
                ws.Cell(row, c + 5).Value  = (double)(d?.He25 ?? 0);
                ws.Cell(row, c + 6).Value  = (double)(d?.He35 ?? 0);
                ws.Cell(row, c + 7).Value  = (double)(d?.He100 ?? 0);
                ws.Cell(row, c + 8).Value  = (double)(d?.HorasHeEvento ?? 0);
                ws.Cell(row, c + 9).Value  = (double)(d?.MontoHeEvento ?? 0);
                ws.Cell(row, c + 10).Value = (double)(d?.HorasHeNecesidad ?? 0);
                ws.Cell(row, c + 11).Value = (double)(d?.MontoHeNecesidad ?? 0);
                ws.Cell(row, c + 12).Value = d?.DiasEvento ?? 0;
                c += MetricasCentroCosto.Length;
            }
            row++;
        }

        Finalizar(ws, 3, row - 1, totalCols);
    }

    // ── Detalle por Empleado (todas las áreas, plano) ────────────────
    private static void HojaDetalleEmpleado(XLWorkbook wb, EventosSobretiempoKpiViewModel vm, List<EventosSobretiempoResumenMesDto> periodos, List<string> areas)
    {
        var ws = wb.Worksheets.Add("Detalle por Empleado");
        var periodosKey = periodos.Select(p => (p.Ano, p.Mes)).ToList();

        string[] fijas = { "Área", "Código", "Empleado", "Puesto", "Centro de Costo" };
        for (int i = 0; i < fijas.Length; i++)
        {
            ws.Cell(1, i + 1).Value = fijas[i];
            ws.Range(1, i + 1, 2, i + 1).Merge();
        }
        int col = fijas.Length + 1;
        foreach (var (ano, mes) in periodosKey)
        {
            ws.Cell(1, col).Value = $"{NombreMes(mes)} {ano}";
            ws.Range(1, col, 1, col + MetricasEmpleado.Length - 1).Merge();
            for (int i = 0; i < MetricasEmpleado.Length; i++) ws.Cell(2, col + i).Value = MetricasEmpleado[i];
            col += MetricasEmpleado.Length;
        }
        int totalCols = col - 1;
        EstilizarEncabezado(ws.Range(1, 1, 2, totalCols));

        var empleados = vm.Empleados
            .Where(e => areas.Contains(e.Area))
            .GroupBy(e => (e.Area, e.CodEmpleado))
            .OrderBy(g => g.Key.Area).ThenBy(g => g.First().NomEmpleado)
            .ToList();

        int row = 3;
        foreach (var grupo in empleados)
        {
            var primero = grupo.First();
            ws.Cell(row, 1).Value = primero.Area;
            ws.Cell(row, 2).Value = primero.CodEmpleado;
            ws.Cell(row, 3).Value = primero.NomEmpleado;
            ws.Cell(row, 4).Value = primero.Puesto ?? "";
            ws.Cell(row, 5).Value = primero.CentroCosto ?? "";

            int c = fijas.Length + 1;
            foreach (var (ano, mes) in periodosKey)
            {
                var d = grupo.FirstOrDefault(x => x.Ano == ano && x.Mes == mes);
                ws.Cell(row, c + 0).Value  = (double)(d?.HorasProduccion ?? 0);
                ws.Cell(row, c + 1).Value  = (double)(d?.MontoProduccion ?? 0);
                ws.Cell(row, c + 2).Value  = (double)(d?.HorasHe ?? 0);
                ws.Cell(row, c + 3).Value  = (double)(d?.TotalHorasExtras ?? 0);
                ws.Cell(row, c + 4).Value  = (double)(d?.He25 ?? 0);
                ws.Cell(row, c + 5).Value  = (double)(d?.He35 ?? 0);
                ws.Cell(row, c + 6).Value  = (double)(d?.He100 ?? 0);
                ws.Cell(row, c + 7).Value  = (double)(d?.HorasHeEvento ?? 0);
                ws.Cell(row, c + 8).Value  = (double)(d?.MontoHeEvento ?? 0);
                ws.Cell(row, c + 9).Value  = (double)(d?.HorasHeNecesidad ?? 0);
                ws.Cell(row, c + 10).Value = (double)(d?.MontoHeNecesidad ?? 0);
                ws.Cell(row, c + 11).Value = d?.DiasEvento ?? 0;
                c += MetricasEmpleado.Length;
            }
            row++;
        }

        Finalizar(ws, 3, row - 1, totalCols);
    }

    // ── Consolidado de Eventos ────────────────────────────────────────
    private static void HojaConsolidadoEventos(XLWorkbook wb, EventosSobretiempoKpiViewModel vm)
    {
        var ws = wb.Worksheets.Add("Consolidado de Eventos");
        string[] headers = { "Tipo de Evento", "Cantidad de Empleados", "Total Días" };
        for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
        EstilizarEncabezado(ws.Range(1, 1, 1, headers.Length));

        int row = 2;
        foreach (var c in vm.ConsolidadoEventos)
        {
            ws.Cell(row, 1).Value = c.TipoEvento;
            ws.Cell(row, 2).Value = c.CantidadEmpleados;
            ws.Cell(row, 3).Value = c.TotalDias;
            row++;
        }

        if (vm.ConsolidadoEventos.Count > 0)
        {
            ws.Cell(row, 1).Value = "Total";
            ws.Cell(row, 2).Value = vm.ConsolidadoEventos.Sum(c => c.CantidadEmpleados);
            ws.Cell(row, 3).Value = vm.ConsolidadoEventos.Sum(c => c.TotalDias);
            ws.Range(row, 1, row, headers.Length).Style.Font.Bold = true;
            ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = TotalBg;
        }
        else
        {
            ws.Cell(row, 1).Value = "Sin eventos en el rango consultado";
            row++;
        }

        Finalizar(ws, 2, row, headers.Length);
    }

    // ── Proyección de Bolsa de HE por Área ────────────────────────────
    private static void HojaProyeccionArea(XLWorkbook wb, EventosSobretiempoKpiViewModel vm, List<string> areas)
    {
        var ws = wb.Worksheets.Add("Proyección Bolsa HE Área");
        string[] headers = { "Área", "Meses considerados", "HE Necesidad prom. (h/mes)", "HE Necesidad prom. (S/./mes)",
            "HE Evento prom. (h/mes)", "HE Evento prom. (S/./mes)", "Bolsa Mensual Sugerida (h)", "Bolsa Mensual Sugerida (S/.)" };
        for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
        EstilizarEncabezado(ws.Range(1, 1, 1, headers.Length));

        var proyeccion = vm.ProyeccionBolsaHe.Where(pb => areas.Contains(pb.Area)).ToList();
        int row = 2;
        foreach (var pb in proyeccion)
        {
            ws.Cell(row, 1).Value = pb.Area;
            ws.Cell(row, 2).Value = pb.MesesConsiderados;
            ws.Cell(row, 3).Value = (double)pb.HorasHeNecesidadProm;
            ws.Cell(row, 4).Value = (double)pb.MontoHeNecesidadProm;
            ws.Cell(row, 5).Value = (double)pb.HorasHeEventoProm;
            ws.Cell(row, 6).Value = (double)pb.MontoHeEventoProm;
            ws.Cell(row, 7).Value = (double)pb.HorasHeNecesidadProm;
            ws.Cell(row, 8).Value = (double)pb.MontoHeNecesidadProm;
            row++;
        }
        if (proyeccion.Count == 0)
        {
            ws.Cell(row, 1).Value = "Sin datos suficientes (AQUARIUS) para proyectar";
            row++;
        }

        Finalizar(ws, 2, row - 1, headers.Length);
    }

    // ── Proyección de Bolsa de HE por Centro de Costo ─────────────────
    private static void HojaProyeccionCentroCosto(XLWorkbook wb, EventosSobretiempoKpiViewModel vm, List<string> areas, HashSet<(string GranCcosto, string CentroCosto)> centrosConDatos)
    {
        var ws = wb.Worksheets.Add("Proyección Bolsa HE CC");
        string[] headers = { "Gran Centro de Costo", "Centro de Costo", "Meses considerados", "HE Necesidad prom. (h/mes)", "HE Necesidad prom. (S/./mes)",
            "HE Evento prom. (h/mes)", "HE Evento prom. (S/./mes)", "Bolsa Mensual Sugerida (h)", "Bolsa Mensual Sugerida (S/.)" };
        for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
        EstilizarEncabezado(ws.Range(1, 1, 1, headers.Length));

        var proyeccionCc = vm.ProyeccionBolsaHeCentroCosto
            .Where(pb => areas.Contains(pb.GranCcosto) && centrosConDatos.Contains((pb.GranCcosto, pb.CentroCosto)))
            .OrderByDescending(pb => pb.HorasHeNecesidadProm)
            .ToList();

        int row = 2;
        foreach (var pb in proyeccionCc)
        {
            ws.Cell(row, 1).Value = pb.GranCcosto;
            ws.Cell(row, 2).Value = pb.CentroCosto;
            ws.Cell(row, 3).Value = pb.MesesConsiderados;
            ws.Cell(row, 4).Value = (double)pb.HorasHeNecesidadProm;
            ws.Cell(row, 5).Value = (double)pb.MontoHeNecesidadProm;
            ws.Cell(row, 6).Value = (double)pb.HorasHeEventoProm;
            ws.Cell(row, 7).Value = (double)pb.MontoHeEventoProm;
            ws.Cell(row, 8).Value = (double)pb.HorasHeNecesidadProm;
            ws.Cell(row, 9).Value = (double)pb.MontoHeNecesidadProm;
            row++;
        }
        if (proyeccionCc.Count == 0)
        {
            ws.Cell(row, 1).Value = "Sin datos suficientes (AQUARIUS) para proyectar";
            row++;
        }

        Finalizar(ws, 2, row - 1, headers.Length);
    }
}
