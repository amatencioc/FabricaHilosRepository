using FabricaHilos.Models.RecursosHumanos;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace FabricaHilos.Services.RecursosHumanos;

/// <summary>
/// Genera el documento Word de Acuerdo de Compensación de HE
/// a partir de la plantilla AcuerdoCompHE.docx.
/// </summary>
public class AcuerdoCompHeDocxService
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AcuerdoCompHeDocxService> _logger;

    public AcuerdoCompHeDocxService(
        IWebHostEnvironment env,
        ILogger<AcuerdoCompHeDocxService> logger)
    {
        _env    = env;
        _logger = logger;
    }

    /// <summary>
    /// Genera el .docx rellenado con los datos del empleado.
    /// </summary>
    /// <param name="datos">Lista de compensaciones DDC del empleado.</param>
    /// <param name="fechaInicio">Fecha inicio del rango (yyyy-MM-dd).</param>
    /// <param name="fechaFin">Fecha fin del rango (yyyy-MM-dd).</param>
    /// <returns>Bytes del .docx generado.</returns>
    public async Task<byte[]> GenerarAsync(
        List<DdcRangoConsultaDto> datos,
        string fechaInicio,
        string fechaFin)
    {
        var templatePath = Path.Combine(
            _env.ContentRootPath,
            "Data", "RecursosHumanos", "Aquarius", "Compensacion",
            "DiaLibrePorCompensar", "AcuerdoCompHE.docx");

        var templateBytes = await File.ReadAllBytesAsync(templatePath);
        using var outputMs = new MemoryStream();

        // Leer el ZIP original en modo Read y escribir salida en un MemoryStream nuevo (Create).
        // Esto evita la corrupción que ocurre al usar ZipArchiveMode.Update sobre el mismo stream.
        using (var inputZip  = new ZipArchive(new MemoryStream(templateBytes), ZipArchiveMode.Read,  leaveOpen: false))
        using (var outputZip = new ZipArchive(outputMs,                        ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in inputZip.Entries)
            {
                var newEntry = outputZip.CreateEntry(entry.FullName, CompressionLevel.Optimal);

                if (entry.FullName == "word/document.xml")
                {
                    // Leer y modificar el XML
                    string xmlContent;
                    using (var sr = new StreamReader(entry.Open(), Encoding.UTF8))
                        xmlContent = await sr.ReadToEndAsync();

                    xmlContent = ModificarDocumentXml(xmlContent, datos, fechaInicio, fechaFin);

                    // Escribir bytes UTF-8 sin BOM directamente al entry del ZIP
                    var xmlBytes = new UTF8Encoding(false).GetBytes(xmlContent);
                    using var dst = newEntry.Open();
                    await dst.WriteAsync(xmlBytes);
                }
                else
                {
                    // Copiar entradas sin modificar
                    using var src = entry.Open();
                    using var dst = newEntry.Open();
                    await src.CopyToAsync(dst);
                }
            }
        }

        return outputMs.ToArray();
    }

    // ── Lógica de modificación del XML ────────────────────────────────────────

    private static string ModificarDocumentXml(
        string xml,
        List<DdcRangoConsultaDto> datos,
        string fechaInicio,
        string fechaFin)
    {
        var fechasHe  = datos
            .Where(d => !string.IsNullOrEmpty(d.FechaOrigenStr))
            .Select(d => DateTime.TryParse(d.FechaOrigenStr, out var f) ? (DateTime?)f : null)
            .Where(f => f.HasValue)
            .Select(f => f!.Value)
            .ToList();

        var fechasDdc = datos
            .Where(d => !string.IsNullOrEmpty(d.FechaDestinoStr))
            .Select(d => DateTime.TryParse(d.FechaDestinoStr, out var f) ? (DateTime?)f : null)
            .Where(f => f.HasValue)
            .Select(f => f!.Value)
            .ToList();

        var semanaHe  = fechasHe.Any()
            ? string.Join(", ", fechasHe.Select(f  => System.Globalization.ISOWeek.GetWeekOfYear(f)).Distinct().OrderBy(w => w))
            : "";
        var semanaDdc = fechasDdc.Any()
            ? string.Join(", ", fechasDdc.Select(f => System.Globalization.ISOWeek.GetWeekOfYear(f)).Distinct().OrderBy(w => w))
            : "";

        // Calcular el rango completo de semanas ISO de las HE
        // (lunes de la primera semana → domingo de la última semana)
        string rangoDocDesde, rangoDocHasta;
        if (fechasHe.Any())
        {
            static DateTime IsoLunes(DateTime d) =>
                System.Globalization.ISOWeek.ToDateTime(
                    System.Globalization.ISOWeek.GetYear(d),
                    System.Globalization.ISOWeek.GetWeekOfYear(d),
                    DayOfWeek.Monday);
            rangoDocDesde = IsoLunes(fechasHe.Min()).ToString("dd/MM/yyyy");
            rangoDocHasta = IsoLunes(fechasHe.Max()).AddDays(6).ToString("dd/MM/yyyy");
        }
        else
        {
            rangoDocDesde = fechaInicio;
            rangoDocHasta = fechaFin;
        }

        // ── Corregir ortografía y acentuación de la plantilla ─────────────────
        xml = CorregirTextos(xml);

        // ── Reemplazar marcadores ─────────────────────────────────────────────
        // ______________  → FECHA (inicio del rango DDC)
        xml = ReemplazarWt(xml, "______________", fechaInicio);
        // ____________    → ÁREA (sin datos disponibles, se deja en blanco)
        xml = ReemplazarWt(xml, "____________",   "");
        // _________       → SEMANA N° (semana de las horas extras)
        xml = ReemplazarWt(xml, "_________",      semanaHe);
        // DEL___________AL___________ → rango completo de la(s) semana(s) ISO de las HE
        // El run en la plantilla tiene un espacio inicial: " DEL___________AL___________"
        xml = ReemplazarWt(xml, " DEL___________AL___________", $" DEL {rangoDocDesde} AL {rangoDocHasta}");

        // ── Párrafo 2: sustituir los dos N°… con los números de semana ────────
        // Primer  N°… = semana del día inasistido (FechaDestinoStr)
        xml = ReemplazarWt(xml, " días inasistidos durante la semana N°…",
                                $" días inasistidos durante la semana N°{semanaDdc}");
        // Segundo N°… = semana de la hora extra (FechaOrigenStr) — run aislado " N°…"
        xml = ReemplazarWt(xml, " N°…", $" N°{semanaHe}");

        // ── Reemplazar filas de la tabla con los datos DDC ────────────────────
        xml = ReemplazarFilasTabla(xml, datos);

        return xml;
    }

    /// <summary>
    /// Corrige errores ortográficos, de acentuación y dobles espacios
    /// en el XML de la plantilla, actuando directamente sobre los nodos &lt;w:t&gt; conocidos.
    /// </summary>
    private static string CorregirTextos(string xml)
    {
        // Reemplaza el contenido exacto de un nodo <w:t>
        static string Rw(string x, string viejo, string nuevo) =>
            x.Replace($">{viejo}</w:t>", $">{nuevo}</w:t>", StringComparison.Ordinal);

        // ── Encabezado ────────────────────────────────────────────────────────
        xml = Rw(xml, "LA COLONIAL FABRICA DE HILOS S.A ",
                      "LA COLONIAL FÁBRICA DE HILOS S.A.");
        xml = Rw(xml, "ACUERDO DE COMPENSACION DE HORAS EXTRAS",
                      "ACUERDO DE COMPENSACIÓN DE HORAS EXTRAS");
        xml = Rw(xml, "AREA:", "ÁREA:");

        // ── Párrafo 1: correcciones por run exacto ────────────────────────────
        // Doble espacio: "durante la  semana,"
        xml = Rw(xml, "s por el trabajador durante la  semana,",
                      "s por el trabajador durante la semana,");
        // "laboro" → "laboró"
        xml = Rw(xml, " que no laboro", " que no laboró");
        // "pero si percibió" → "pero sí percibió"
        xml = Rw(xml, ", pero si percibió las remuneraciones",
                      ", pero sí percibió las remuneraciones");

        // ── Párrafo 2: correcciones por run exacto ────────────────────────────
        // "efectuara" → "efectuará"  (el N°… se sustituye dinámicamente en ModificarDocumentXml)
        xml = Rw(xml, "efectuara ", "efectuará ");

        // ── Párrafo 3: correcciones por run exacto ────────────────────────────
        // Doble espacio: "proceden  a" + coma faltante después de "sentido"
        xml = Rw(xml,
            "En tal sentido no existiendo afectación o renuncia a derecho de las partes, proceden  a",
            "En tal sentido, no existiendo afectación o renuncia a derecho de las partes, proceden a");

        // ── Cabeceras de tabla ────────────────────────────────────────────────
        xml = Rw(xml, "COMPENSACION DE HORAS EXTRAS", "COMPENSACIÓN DE HORAS EXTRAS");
        xml = Rw(xml, "FECHA QUE NO ASISTIO",          "FECHA QUE NO ASISTIÓ");

        return xml;
    }

    /// <summary>
    /// Reemplaza el texto de un nodo &lt;w:t&gt; que contiene exactamente <paramref name="buscar"/>.
    /// Conserva los atributos originales del tag (ej: xml:space="preserve").
    /// </summary>
    private static string ReemplazarWt(string xml, string buscar, string reemplazar)
    {
        var patron = $">{buscar}</w:t>";
        var idx = xml.IndexOf(patron, StringComparison.Ordinal);
        if (idx < 0) return xml;

        // Sustituir solo el texto entre '>' y '</w:t>', sin alterar los atributos del tag de apertura.
        return xml.Substring(0, idx + 1)        // incluye el '>'
             + reemplazar
             + "</w:t>"
             + xml.Substring(idx + patron.Length);
    }

    /// <summary>
    /// Reemplaza las filas de datos de la tabla DDC.
    /// Fila 0 = título merge, Fila 1 = cabecera, Filas 2..N = datos (vacías en plantilla).
    /// </summary>
    private static string ReemplazarFilasTabla(string xml, List<DdcRangoConsultaDto> datos)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        var nm = new XmlNamespaceManager(doc.NameTable);
        nm.AddNamespace("w",   "http://schemas.openxmlformats.org/wordprocessingml/2006/main");
        nm.AddNamespace("w14", "http://schemas.microsoft.com/office/word/2010/wordml");

        var tbl = doc.SelectSingleNode("//w:tbl", nm)
                  ?? throw new InvalidOperationException("No se encontró la tabla en la plantilla.");

        var allRows  = tbl.SelectNodes("w:tr", nm)!;
        var dataRows = new List<XmlNode>();
        for (int i = 2; i < allRows.Count; i++)
            dataRows.Add(allRows[i]!);

        if (dataRows.Count == 0)
            throw new InvalidOperationException("La plantilla no tiene filas de datos en la tabla.");

        // Clonar la primera fila de datos como plantilla
        var rowTemplate = (XmlNode)dataRows[0].CloneNode(true);

        // Eliminar todas las filas de datos existentes
        foreach (var r in dataRows)
            tbl.RemoveChild(r);

        if (!datos.Any())
        {
            // Si no hay datos dejar al menos una fila vacía
            tbl.AppendChild(rowTemplate.CloneNode(true));
        }
        else
        {
            // Ordenar por fecha que no asistió (FechaDestinoStr) ascendente
            var datosOrdenados = datos
                .OrderBy(d => d.FechaDestinoStr == null)
                .ThenBy(d => DateTime.TryParse(d.FechaDestinoStr, out var f) ? f : DateTime.MaxValue)
                .ToList();

            for (int idx = 0; idx < datosOrdenados.Count; idx++)
            {
                var item   = datosOrdenados[idx];
                var newRow = (XmlNode)rowTemplate.CloneNode(true);
                var cells  = newRow.SelectNodes("w:tc", nm)!;

                if (cells.Count >= 8)
                {
                    SetCellText(doc, nm, cells[0], (idx + 1).ToString());        // N°
                    SetCellText(doc, nm, cells[1], item.NombreCompleto ?? "");    // Apellidos y Nombres
                    SetCellText(doc, nm, cells[2], "");                            // DNI
                    SetCellText(doc, nm, cells[3], "");                            // Firma
                    SetCellText(doc, nm, cells[4], item.FechaDestinoStr ?? "");   // Fecha que no asistió
                    SetCellText(doc, nm, cells[5], "");                            // Motivo de ausencia (en blanco)
                    SetCellText(doc, nm, cells[6], item.FechaOrigenStr ?? "");    // Fecha HE
                    SetCellText(doc, nm, cells[7], item.TiempoHhMi ?? "");        // N° Horas
                }

                tbl.AppendChild(newRow);
            }
        }

        // Serializar con UTF-8 real para que coincida con la declaración XML del docx
        using var ms = new MemoryStream();
        using (var xw = XmlWriter.Create(ms, new XmlWriterSettings
        {
            Encoding           = new UTF8Encoding(false), // sin BOM
            OmitXmlDeclaration = false,
            Indent             = false
        }))
        {
            doc.Save(xw);
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void SetCellText(XmlDocument doc, XmlNamespaceManager nm, XmlNode? cell, string text)
    {
        if (cell == null) return;
        var runs = cell.SelectNodes(".//w:r", nm)!;

        // Eliminar runs adicionales (dejar solo el primero)
        for (int i = 1; i < runs.Count; i++)
            runs[i]!.ParentNode!.RemoveChild(runs[i]!);

        if (runs.Count > 0)
        {
            var tNodes = runs[0]!.SelectNodes("w:t", nm)!;
            for (int i = 1; i < tNodes.Count; i++)
                tNodes[i]!.ParentNode!.RemoveChild(tNodes[i]!);

            XmlNode tNode;
            if (tNodes.Count > 0)
            {
                tNode = tNodes[0]!;
            }
            else
            {
                tNode = doc.CreateElement("w", "t", "http://schemas.openxmlformats.org/wordprocessingml/2006/main");
                runs[0]!.AppendChild(tNode);
            }

            tNode.InnerText = text;

            if (text.Length > 0 && (text[0] == ' ' || text[^1] == ' '))
            {
                var attr = doc.CreateAttribute("xml:space", "http://www.w3.org/XML/1998/namespace");
                attr.Value = "preserve";
                ((XmlElement)tNode).SetAttributeNode(attr);
            }
        }
        else
        {
            var p = cell.SelectSingleNode(".//w:p", nm);
            if (p != null)
            {
                var r = doc.CreateElement("w", "r", "http://schemas.openxmlformats.org/wordprocessingml/2006/main");
                var t = doc.CreateElement("w", "t", "http://schemas.openxmlformats.org/wordprocessingml/2006/main");
                t.InnerText = text;
                r.AppendChild(t);
                p.AppendChild(r);
            }
        }
    }
}
