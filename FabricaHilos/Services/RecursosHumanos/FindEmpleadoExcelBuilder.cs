using ClosedXML.Excel;
using FabricaHilos.Models.RecursosHumanos;

namespace FabricaHilos.Services.RecursosHumanos;

/// <summary>
/// Genera los archivos Excel (ClosedXML) para el botón "Exportar Excel" de
/// FindEmpleado/Index.cshtml, tanto para la búsqueda individual (detalle completo
/// de un empleado) como para la búsqueda masiva por rango de fechas.
/// </summary>
public static class FindEmpleadoExcelBuilder
{
    private static void EstilizarEncabezado(IXLRange headerRange)
    {
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#C0622B");
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    private static IXLWorksheet CrearHojaConEncabezados(XLWorkbook wb, string nombre, params string[] headers)
    {
        var ws = wb.AddWorksheet(nombre);
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        EstilizarEncabezado(ws.Range(1, 1, 1, headers.Length));
        ws.SheetView.FreezeRows(1);
        return ws;
    }

    /// <summary>
    /// Extrae solo la parte de fecha (DD/MM/YYYY) de un valor tipo "DD/MM/YYYY HH24:MI"
    /// proveniente de SIG.SI_REGPERS (FechaIngreso/FechaSalida).
    /// </summary>
    private static string? ExtraerFecha(string? fechaHora)
    {
        if (string.IsNullOrWhiteSpace(fechaHora))
            return null;

        var partes = fechaHora.Trim().Split(' ', 2);
        return partes.Length > 0 ? partes[0] : null;
    }

    public static byte[] GenerarExcelIndividual(EmpleadoConEventoRealDto emp)
    {
        using var wb = new XLWorkbook();

        // ── Hoja 1: Datos Generales ─────────────────────────────────────────
        var wsGen = wb.AddWorksheet("Datos Generales");
        (string, object?)[] datos =
        [
            ("Nombre completo", emp.NombreCompleto),
            ("Cód. Aquarius", emp.CodAquarius),
            ("Cód. SIG", emp.CodSig),
            ("DNI", emp.Dni),
            ("Empresa", emp.Empresa),
            ("Horario", emp.HorarioDescripcion),
            ("Turno", emp.HorarioTurno),
            ("Estado asistencia hoy", emp.EstadoAsistenciaHoy),
            ("Hora entrada", emp.HoraEntrada),
            ("Hora salida", emp.HoraSalida),
            ("Evento (hoy)", emp.EventoDescripcion),
            ("Tipo evento", emp.EventoTipoCodigo),
            ("Fechas evento", emp.EventoFechas),
            ("Observación evento", emp.EventoObservacion),
            ("Vigilancia — estado", emp.VigilanciaEstado),
            ("Vigilancia — entrada", emp.VigilanciaEntrada),
            ("Vigilancia — salida", emp.VigilanciaSalida),
            ("Vigilancia — alcohol", emp.VigilanciaAlcohol),
            ("Vigilancia — celular", emp.VigilanciaCelular),
            ("Rango Vigilancia", $"{emp.RangoVigilanciaDesde} — {emp.RangoVigilanciaHasta}"),
            ("Rango Eventos", $"{emp.RangoEventosDesde} — {emp.RangoEventosHasta}"),
            ("Nota de sincronización", emp.NotaSincronizacion),
        ];

        for (int i = 0; i < datos.Length; i++)
        {
            wsGen.Cell(i + 1, 1).Value = datos[i].Item1;
            wsGen.Cell(i + 1, 2).Value = datos[i].Item2?.ToString() ?? "—";
            wsGen.Cell(i + 1, 1).Style.Font.Bold = true;
        }
        wsGen.Columns().AdjustToContents();

        // ── Hoja 2: Detalle Vigilancia ───────────────────────────────────────
        var wsVig = CrearHojaConEncabezados(wb, "Vigilancia",
            "Tipo", "Cód. SIG", "DocId", "Nombre", "DNI/RUC", "C. Costo", "Tipo CP",
            "Fecha Ingreso", "Fecha Salida", "Celular", "Guarda Cel.", "Bloque",
            "Test Alcohol", "Result. Alcohol", "Observación");

        int row = 2;
        foreach (var v in emp.VigilanciaRegistros)
        {
            wsVig.Cell(row, 1).Value = v.Tipo;
            wsVig.Cell(row, 2).Value = v.CodSig;
            wsVig.Cell(row, 3).Value = v.DocId;
            wsVig.Cell(row, 4).Value = v.Nombre;
            wsVig.Cell(row, 5).Value = v.DniRuc;
            wsVig.Cell(row, 6).Value = v.CentroCosto;
            wsVig.Cell(row, 7).Value = v.TipoCp;
            wsVig.Cell(row, 8).Value = v.FechaIngreso;
            wsVig.Cell(row, 9).Value = v.FechaSalida;
            wsVig.Cell(row, 10).Value = v.TraeCelular;
            wsVig.Cell(row, 11).Value = v.GuardaCelular;
            wsVig.Cell(row, 12).Value = v.NroBlock;
            wsVig.Cell(row, 13).Value = v.TestAlcohol;
            wsVig.Cell(row, 14).Value = v.ResultadoAlcohol;
            wsVig.Cell(row, 15).Value = v.Observacion;
            row++;
        }
        wsVig.Columns().AdjustToContents();

        // ── Hoja 3: Eventos SIG ─────────────────────────────────────────────
        var wsEvt = CrearHojaConEncabezados(wb, "Eventos",
            "Tipo", "Descripción", "Fecha Inicio", "Fecha Final", "Observación", "No sincroniza");

        row = 2;
        foreach (var e in emp.EventosHistorial)
        {
            wsEvt.Cell(row, 1).Value = e.TipoCodigo;
            wsEvt.Cell(row, 2).Value = e.Descripcion;
            wsEvt.Cell(row, 3).Value = e.FechaInicio;
            wsEvt.Cell(row, 4).Value = e.FechaFinal;
            wsEvt.Cell(row, 5).Value = e.Observacion;
            wsEvt.Cell(row, 6).Value = e.NoSincroniza ? "SÍ" : "NO";
            row++;
        }
        wsEvt.Columns().AdjustToContents();

        // ── Hoja 4: Compensaciones ───────────────────────────────────────────
        var wsComp = CrearHojaConEncabezados(wb, "Compensaciones",
            "Fecha Origen", "Tipo Origen", "Fecha Destino", "Tipo Compensación",
            "Tiempo (HH:MM)", "Descripción");

        row = 2;
        foreach (var c in emp.Compensaciones)
        {
            wsComp.Cell(row, 1).Value = c.FechaOrigen;
            wsComp.Cell(row, 2).Value = c.TipoOrigenDesc ?? c.TipoOrigen;
            wsComp.Cell(row, 3).Value = c.FechaDestino;
            wsComp.Cell(row, 4).Value = c.TipoCompensacionDesc ?? c.TipoCompensacion;
            wsComp.Cell(row, 5).Value = c.TiempoHhMm;
            wsComp.Cell(row, 6).Value = c.Descripcion;
            row++;
        }
        wsComp.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public static byte[] GenerarExcelMasivo(List<EmpleadoConEventoRealDto> lista, DateTime fechaDesde, DateTime fechaHasta)
    {
        using var wb = new XLWorkbook();

        // ── Hoja 1: Detalle diario (una fila por cada registro de Vigilancia,
        //    mostrando el día, ingreso, salida, si se tomó prueba de alcohol y
        //    su resultado, para cada empleado) ──────────────────────────────
        var wsDetalle = CrearHojaConEncabezados(wb, "Detalle Diario",
            "Cód. Aquarius", "Cód. SIG", "Nombre completo", "DNI", "Empresa",
            "Horario", "Turno", "Fecha", "Ingreso", "Salida",
            "Prueba Alcohol", "Resultado Alcohol", "Observación");

        int rowDet = 2;
        foreach (var emp in lista)
        {
            if (emp.VigilanciaRegistros.Count == 0)
            {
                wsDetalle.Cell(rowDet, 1).Value = emp.CodAquarius;
                wsDetalle.Cell(rowDet, 2).Value = emp.CodSig;
                wsDetalle.Cell(rowDet, 3).Value = emp.NombreCompleto;
                wsDetalle.Cell(rowDet, 4).Value = emp.Dni;
                wsDetalle.Cell(rowDet, 5).Value = emp.Empresa;
                wsDetalle.Cell(rowDet, 6).Value = emp.HorarioDescripcion;
                wsDetalle.Cell(rowDet, 7).Value = emp.HorarioTurno;
                wsDetalle.Cell(rowDet, 8).Value = "—";
                wsDetalle.Cell(rowDet, 9).Value = "—";
                wsDetalle.Cell(rowDet, 10).Value = "—";
                wsDetalle.Cell(rowDet, 11).Value = "—";
                wsDetalle.Cell(rowDet, 12).Value = "—";
                wsDetalle.Cell(rowDet, 13).Value = "Sin registros de vigilancia en el rango";
                rowDet++;
                continue;
            }

            foreach (var v in emp.VigilanciaRegistros)
            {
                bool esPositivo = string.Equals(v.TestAlcohol?.Trim(), "S", StringComparison.OrdinalIgnoreCase) &&
                                   string.Equals(v.ResultadoAlcohol?.Trim(), "P", StringComparison.OrdinalIgnoreCase);

                // Turno/Horario de ESE día (rotativo), no el fijo de HOY del empleado.
                wsDetalle.Cell(rowDet, 1).Value = emp.CodAquarius;
                wsDetalle.Cell(rowDet, 2).Value = emp.CodSig;
                wsDetalle.Cell(rowDet, 3).Value = emp.NombreCompleto;
                wsDetalle.Cell(rowDet, 4).Value = emp.Dni;
                wsDetalle.Cell(rowDet, 5).Value = emp.Empresa;
                wsDetalle.Cell(rowDet, 6).Value = v.HorarioDia ?? emp.HorarioDescripcion;
                wsDetalle.Cell(rowDet, 7).Value = v.TurnoDia ?? emp.HorarioTurno;
                wsDetalle.Cell(rowDet, 8).Value = ExtraerFecha(v.FechaIngreso) ?? ExtraerFecha(v.FechaSalida);
                wsDetalle.Cell(rowDet, 9).Value = v.FechaIngreso;
                wsDetalle.Cell(rowDet, 10).Value = v.FechaSalida;
                wsDetalle.Cell(rowDet, 11).Value = v.TestAlcohol;
                wsDetalle.Cell(rowDet, 12).Value = v.ResultadoAlcohol;
                wsDetalle.Cell(rowDet, 13).Value = v.Observacion;

                if (esPositivo)
                    wsDetalle.Range(rowDet, 1, rowDet, 13).Style.Fill.BackgroundColor = XLColor.FromHtml("#FDECEA");

                rowDet++;
            }
        }
        wsDetalle.Columns().AdjustToContents();

        // ── Hoja 2: Resumen (conteos por empleado, un empleado por fila) ────
        var wsResumen = CrearHojaConEncabezados(wb, "Resumen",
            "Cód. Aquarius", "Cód. SIG", "Nombre completo", "DNI", "Empresa",
            "Horario (hoy)", "Turno (hoy)", "Estado hoy", "Registros Vigilancia",
            "Pruebas Alcohol Positivas", "Eventos en rango");

        int row = 2;
        foreach (var emp in lista)
        {
            var vig = emp.VigilanciaRegistros;
            int positivos = vig.Count(v =>
                string.Equals(v.TestAlcohol?.Trim(), "S", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(v.ResultadoAlcohol?.Trim(), "P", StringComparison.OrdinalIgnoreCase));

            wsResumen.Cell(row, 1).Value = emp.CodAquarius;
            wsResumen.Cell(row, 2).Value = emp.CodSig;
            wsResumen.Cell(row, 3).Value = emp.NombreCompleto;
            wsResumen.Cell(row, 4).Value = emp.Dni;
            wsResumen.Cell(row, 5).Value = emp.Empresa;
            wsResumen.Cell(row, 6).Value = emp.HorarioDescripcion;
            wsResumen.Cell(row, 7).Value = emp.HorarioTurno;
            wsResumen.Cell(row, 8).Value = emp.EstadoAsistenciaHoy;
            wsResumen.Cell(row, 9).Value = vig.Count;
            wsResumen.Cell(row, 10).Value = positivos;
            wsResumen.Cell(row, 11).Value = emp.EventosHistorial.Count;

            if (positivos > 0)
                wsResumen.Range(row, 1, row, 11).Style.Fill.BackgroundColor = XLColor.FromHtml("#FDECEA");

            row++;
        }
        wsResumen.Columns().AdjustToContents();

        // ── Hoja 2: Detalle Vigilancia (todos los empleados) ─────────────────
        var wsVig = CrearHojaConEncabezados(wb, "Vigilancia",
            "Cód. Aquarius", "Nombre", "Tipo", "DocId", "DNI/RUC", "C. Costo",
            "Fecha Ingreso", "Fecha Salida", "Test Alcohol", "Result. Alcohol", "Observación");

        row = 2;
        foreach (var emp in lista)
        {
            foreach (var v in emp.VigilanciaRegistros)
            {
                wsVig.Cell(row, 1).Value = emp.CodAquarius;
                wsVig.Cell(row, 2).Value = emp.NombreCompleto;
                wsVig.Cell(row, 3).Value = v.Tipo;
                wsVig.Cell(row, 4).Value = v.DocId;
                wsVig.Cell(row, 5).Value = v.DniRuc;
                wsVig.Cell(row, 6).Value = v.CentroCosto;
                wsVig.Cell(row, 7).Value = v.FechaIngreso;
                wsVig.Cell(row, 8).Value = v.FechaSalida;
                wsVig.Cell(row, 9).Value = v.TestAlcohol;
                wsVig.Cell(row, 10).Value = v.ResultadoAlcohol;
                wsVig.Cell(row, 11).Value = v.Observacion;
                row++;
            }
        }
        wsVig.Columns().AdjustToContents();

        // ── Hoja 3: Eventos SIG (todos los empleados) ────────────────────────
        var wsEvt = CrearHojaConEncabezados(wb, "Eventos",
            "Cód. Aquarius", "Nombre", "Tipo", "Descripción", "Fecha Inicio", "Fecha Final", "Observación");

        row = 2;
        foreach (var emp in lista)
        {
            foreach (var e in emp.EventosHistorial)
            {
                wsEvt.Cell(row, 1).Value = emp.CodAquarius;
                wsEvt.Cell(row, 2).Value = emp.NombreCompleto;
                wsEvt.Cell(row, 3).Value = e.TipoCodigo;
                wsEvt.Cell(row, 4).Value = e.Descripcion;
                wsEvt.Cell(row, 5).Value = e.FechaInicio;
                wsEvt.Cell(row, 6).Value = e.FechaFinal;
                wsEvt.Cell(row, 7).Value = e.Observacion;
                row++;
            }
        }
        wsEvt.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
