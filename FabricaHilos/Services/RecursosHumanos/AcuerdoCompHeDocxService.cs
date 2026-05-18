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
        var outputMs = new MemoryStream();

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
        var nombreEmpleado = datos.FirstOrDefault()?.NombreCompleto ?? "";

        // Corregir ortografía y acentuación de la plantilla
        xml = CorregirTextos(xml);

        // Reemplazar marcadores de texto plano (están en <w:t> sin fragmentar)
        // ______________  → FECHA
        xml = ReemplazarWt(xml, "______________", fechaInicio);
        // ____________    → ÁREA / Nombre empleado
        xml = ReemplazarWt(xml, "____________",   nombreEmpleado.ToUpper());
        // _________       → SEMANA N°
        xml = ReemplazarWt(xml, "_________",      "");
        // DEL___________AL___________ → rango de fechas
        xml = ReemplazarWt(xml, "DEL___________AL___________", $"DEL {fechaInicio} AL {fechaFin}");

        // Reemplazar filas de la tabla con los datos DDC
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
        // Doble espacio: "el día y/o  los días"
        xml = Rw(xml, "el día y/o  los días inasistidos durante la semana N°…",
                      "el día y/o los días inasistidos durante la semana N°...");
        // "efectuara" → "efectuará"
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
    /// Reemplaza el texto de un nodo <w:t> que contiene exactamente <paramref name="buscar"/>.
    /// </summary>
    private static string ReemplazarWt(string xml, string buscar, string reemplazar)
    {
        // Buscar tanto sin atributos como con atributos (ej: xml:space="preserve")
        // Patrón: <w:t>TEXTO</w:t>  o  <w:t atributo="...">TEXTO</w:t>
        var idx = xml.IndexOf($">{buscar}</w:t>", StringComparison.Ordinal);
        if (idx < 0) return xml;

        // Preservar espacios si el reemplazo empieza/termina con espacio
        var needSpace = reemplazar.StartsWith(' ') || reemplazar.EndsWith(' ');
        string newContent = needSpace
            ? $" xml:space=\"preserve\">{reemplazar}</w:t>"
            : $">{reemplazar}</w:t>";

        // El idx apunta al '>' antes del texto; buscar el inicio del tag <w:t para reemplazar atributos si es necesario
        // Como el atributo xml:space puede ya estar, simplemente reemplazamos desde '>' hasta '</w:t>'
        int closeIdx = idx + 1 + buscar.Length + "</w:t>".Length; // fin del </w:t>
        return xml.Substring(0, idx) + newContent + xml.Substring(closeIdx);
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
            dataRows.Add(allRows[i]);

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
                    SetCellText(doc, nm, cells[5], item.TipoCompensacion ?? "");  // Motivo
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

    private static void SetCellText(XmlDocument doc, XmlNamespaceManager nm, XmlNode cell, string text)
    {
        var runs = cell.SelectNodes(".//w:r", nm)!;

        // Eliminar runs adicionales (dejar solo el primero)
        for (int i = 1; i < runs.Count; i++)
            runs[i].ParentNode!.RemoveChild(runs[i]);

        if (runs.Count > 0)
        {
            var tNodes = runs[0].SelectNodes("w:t", nm)!;
            for (int i = 1; i < tNodes.Count; i++)
                tNodes[i].ParentNode!.RemoveChild(tNodes[i]);

            XmlNode tNode;
            if (tNodes.Count > 0)
            {
                tNode = tNodes[0];
            }
            else
            {
                tNode = doc.CreateElement("w", "t", "http://schemas.openxmlformats.org/wordprocessingml/2006/main");
                runs[0].AppendChild(tNode);
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
