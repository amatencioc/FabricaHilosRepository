using System.Text;
using System.Text.RegularExpressions;
using FabricaHilos.DocumentExtractor.Models;
using PDFtoImage;
using SkiaSharp;
using Tesseract;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace FabricaHilos.DocumentExtractor.Services;

public class PdfExtractorService : IDocumentExtractorService
{
    private static readonly string TessDataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");
    public Task<DocumentoExtraido> ExtraerAsync(Stream archivo, string tipoMime, string nombreArchivo)
    {
        var resultado = new DocumentoExtraido
        {
            NombreArchivo = nombreArchivo,
            FuenteExtraccion = "PdfPig"
        };

        bool esImagen = tipoMime is "image/png" or "image/jpeg" or "image/jpg" or "image/tiff" or "image/bmp";
        if (esImagen) resultado.FuenteExtraccion = "Tesseract-OCR";

        try
        {
            string texto = ExtraerTexto(archivo, tipoMime, out bool usedOcr);
            if (usedOcr && tipoMime == "application/pdf")
                resultado.FuenteExtraccion = "Tesseract-OCR (PDF)";
            ProcesarTexto(texto, resultado);
        }
        catch (Exception ex)
        {
            resultado.MensajeError = ex.Message;
            resultado.Confianza = 0;
        }

        return Task.FromResult(resultado);
    }

    private static string ExtraerTexto(Stream archivo, string tipoMime, out bool usedOcr)
    {
        usedOcr = false;
        if (tipoMime == "application/pdf")
        {
            using var ms = new MemoryStream();
            archivo.CopyTo(ms);
            var pdfBytes = ms.ToArray();

            var sb = new StringBuilder();
            int pageCount;
            using (var pdf = PdfDocument.Open(new MemoryStream(pdfBytes)))
            {
                pageCount = pdf.NumberOfPages;
                foreach (UglyToad.PdfPig.Content.Page page in pdf.GetPages())
                    sb.AppendLine(page.Text);
            }

            string texto = sb.ToString();
            if (texto.Trim().Length < 50)
            {
                usedOcr = true;
                return ExtraerTextoOcrDesdePdf(pdfBytes, pageCount);
            }
            return texto;
        }
        if (tipoMime is "image/png" or "image/jpeg" or "image/jpg" or "image/tiff" or "image/bmp")
        {
            usedOcr = true;
            return ExtraerTextoOcr(archivo);
        }
        return string.Empty;
    }

    private static string ExtraerTextoOcrDesdePdf(byte[] pdfBytes, int pageCount)
    {
        var sb = new StringBuilder();
        var renderOptions = new RenderOptions { Dpi = 300 };

        for (int i = 0; i < pageCount; i++)
        {
            using var pageMs = new MemoryStream();
            Conversion.SavePng(pageMs, pdfBytes, page: i, options: renderOptions);
            sb.AppendLine(OcrBytes(PreprocesarImagen(pageMs.ToArray())));
        }
        return sb.ToString();
    }

    private static string ExtraerTextoOcr(Stream archivo)
    {
        using var ms = new MemoryStream();
        archivo.CopyTo(ms);
        return OcrBytes(PreprocesarImagen(ms.ToArray()));
    }

    // Upscale to >= 2400px longest side, convert to grayscale, boost contrast 1.4x.
    private static byte[] PreprocesarImagen(byte[] imageBytes)
    {
        using var original = SKBitmap.Decode(imageBytes);
        if (original == null) return imageBytes;

        int w = original.Width, h = original.Height;
        const int minLongSide = 2400;
        float scale = (float)minLongSide / Math.Max(w, h);
        int newW = scale > 1f ? (int)(w * scale) : w;
        int newH = scale > 1f ? (int)(h * scale) : h;

        // Grayscale + 1.4x contrast via 4x5 color matrix
        const float c = 1.4f;
        float t = (1f - c) / 2f * 255f;
        float[] cm =
        [
            c * 0.299f, c * 0.587f, c * 0.114f, 0, t,
            c * 0.299f, c * 0.587f, c * 0.114f, 0, t,
            c * 0.299f, c * 0.587f, c * 0.114f, 0, t,
            0,          0,          0,          1, 0
        ];

        // Resize first (if needed), then draw at (0,0) with color matrix
        using var upscaled = scale > 1f
            ? original.Resize(new SKImageInfo(newW, newH), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None))
            : null;
        var source = upscaled ?? original;

        using var output = new SKBitmap(newW, newH);
        using (var canvas = new SKCanvas(output))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { ColorFilter = SKColorFilter.CreateColorMatrix(cm) };
            canvas.DrawBitmap(source, 0f, 0f, paint);
        }

        using var image = SKImage.FromBitmap(output);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    // LSTM OCR with dual-pass PSM: Auto first, SparseText fallback for forms/invoices.
    private static string OcrBytes(byte[] imageBytes)
    {
        if (!Directory.Exists(TessDataPath))
            throw new InvalidOperationException($"Tessdata no encontrado: {TessDataPath}");

        using var engine = new TesseractEngine(TessDataPath, "spa", EngineMode.LstmOnly);
        engine.SetVariable("preserve_interword_spaces", "1");
        engine.SetVariable("user_defined_dpi", "300");

        using var pix = Pix.LoadFromMemory(imageBytes);

        string textAuto;
        using (var page = engine.Process(pix, PageSegMode.Auto))
            textAuto = page.GetText() ?? string.Empty;

        // If Auto produces few lines, also try SparseText (better for label/value forms)
        var lineCount = textAuto.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
        if (lineCount < 6)
        {
            string textSparse;
            using (var page = engine.Process(pix, PageSegMode.SparseText))
                textSparse = page.GetText() ?? string.Empty;
            if (textSparse.Length > textAuto.Length)
                return textSparse;
        }

        return textAuto;
    }

    private static void ProcesarTexto(string texto, DocumentoExtraido doc)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            doc.MensajeError = "No se pudo extraer texto del documento.";
            doc.Confianza = 0;
            return;
        }

        // Tipo de documento
        if (Regex.IsMatch(texto, @"FACTURA", RegexOptions.IgnoreCase))
            doc.TipoDocumento = "FACTURA";
        else if (Regex.IsMatch(texto, @"BOLETA", RegexOptions.IgnoreCase))
            doc.TipoDocumento = "BOLETA";
        else if (Regex.IsMatch(texto, @"RECIBO", RegexOptions.IgnoreCase))
            doc.TipoDocumento = "RECIBO";

        // N° comprobante: [EFBT] son los prefijos de serie SUNAT para facturas, boletas, recibos y notas
        var matchComp = Regex.Match(texto, @"\b([A-Z]\d{3})-(\d{4,8})\b");
        if (matchComp.Success)
        {
            doc.Serie = matchComp.Groups[1].Value;
            doc.Correlativo = matchComp.Groups[2].Value;
            doc.NumeroDocumento = $"{doc.Serie}-{doc.Correlativo}";
        }

        // RUCs: busca etiqueta "RUC:" primero (más fiable), fallback a patrón posicional
        var rucConEtiqueta = Regex.Matches(texto, @"\bRUC\s*:?\s*(20\d{9})\b", RegexOptions.IgnoreCase);
        var rucsBare       = Regex.Matches(texto, @"\b(20\d{9})\b");
        var todosRucs = (rucConEtiqueta.Count >= 2 ? rucConEtiqueta : rucsBare)
                       .Cast<Match>().Select(m => m.Groups[1].Value).Distinct().ToList();
        if (todosRucs.Count >= 1) doc.RucEmisor   = todosRucs[0];
        if (todosRucs.Count >= 2) doc.RucReceptor = todosRucs[1];

        // Razón Social Receptor ("Nombre/Razón Social: ...") — captura hasta RUC o espacio múltiple
        var matchRsRec = Regex.Match(texto,
            @"(?:Nombre\s*/?\s*)?Raz[oó]n\s*Social\s*:\s*(.+?)(?=\s{3,}|\bRUC\b|[\r\n])",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (matchRsRec.Success) doc.RazonSocialReceptor = matchRsRec.Groups[1].Value.Trim();

        // Dirección Receptor
        var matchDir = Regex.Match(texto,
            @"Direcci[oó]n\s*:\s*(.+?)(?=\s{3,}|\bFecha\b|\bMoneda\b|[\r\n])",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (matchDir.Success) doc.DireccionReceptor = matchDir.Groups[1].Value.Trim();

        // Fecha Emisión: etiqueta primero, fallback a primera fecha encontrada
        var matchFE = Regex.Match(texto, @"Fecha\s*(?:de\s*)?Emisi[oó]n\b[^/\d]*(\d{2}/\d{2}/\d{4})", RegexOptions.IgnoreCase);
        if (!matchFE.Success)
            matchFE = Regex.Match(texto, @"\b(\d{2}/\d{2}/\d{4})\b");
        if (matchFE.Success && DateTime.TryParseExact(matchFE.Groups[1].Value, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out var fe))
            doc.FechaEmision = fe;

        // Fecha Vencimiento: siempre por etiqueta (evita confusión con Fecha Emisión)
        var matchFV = Regex.Match(texto, @"Fecha\s*(?:de\s*)?Vencimiento\b[^/\d]*(\d{2}/\d{2}/\d{4})", RegexOptions.IgnoreCase);
        if (matchFV.Success && DateTime.TryParseExact(matchFV.Groups[1].Value, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out var fv))
            doc.FechaVencimiento = fv;

        // Hora
        var matchHora = Regex.Match(texto, @"\b(\d{2}:\d{2}:\d{2})\b");
        if (matchHora.Success && TimeSpan.TryParse(matchHora.Groups[1].Value, out var hora))
            doc.HoraEmision = hora;

        // Moneda
        doc.Moneda = Regex.IsMatch(texto, @"D[oó]lares?|USD|\$", RegexOptions.IgnoreCase) ? "USD" : "PEN";

        // Base imponible
        doc.BaseImponible = ExtraerMonto(texto, @"(?:Sub\s*Total|Op\.?\s*Gravada)[^\d]*(\d[\d,\.]+)");

        // IGV
        doc.TotalIgv = ExtraerMonto(texto, @"(?:IGV|I\.G\.V)[^\d]*(\d[\d,\.]+)");

        // Total a pagar
        doc.TotalPagar = ExtraerMonto(texto, @"(?:Importe\s*Total|TOTAL\s*A\s*PAGAR|TOTAL\s*PAGAR)[^\d]*(\d[\d,\.]+)");

        // Descuentos
        doc.TotalDescuento = ExtraerMonto(texto, @"(?:Descuento)[^\d]*(\d[\d,\.]+)");

        // Anticipos
        doc.TotalAnticipos = ExtraerMonto(texto, @"(?:Anticipo)[^\d]*(\d[\d,\.]+)");

        // Gratuito
        doc.TotalGratuito = ExtraerMonto(texto, @"(?:Gratuito|Op\.?\s*Gratuita)[^\d]*(\d[\d,\.]+)");

        // Forma de pago
        var matchPago = Regex.Match(texto, @"(Crédito|Contado|CREDITO|CONTADO|\d+\s*D[IÍ]AS?)", RegexOptions.IgnoreCase);
        if (matchPago.Success) doc.FormaPago = matchPago.Value.Trim();

        // N° Pedido / Orden de Compra (captura solo dígitos para evitar contaminación con texto adyacente)
        var matchPedido = Regex.Match(texto, @"(?:Orden\s*de\s*Compra|O\.C\.)\s*[:\-]?\s*(\d[\d\-]*)", RegexOptions.IgnoreCase);
        if (matchPedido.Success) doc.NumeroPedido = matchPedido.Groups[1].Value.Trim();

        // N° Guía de Remisión
        var matchGuia = Regex.Match(texto, @"(?:Gu[íi]a\s*de\s*Remis)[^\n]*?([A-Z]\d{3}-\d{4,8})", RegexOptions.IgnoreCase);
        if (matchGuia.Success) doc.NumeroGuia = matchGuia.Groups[1].Value.Trim();

        // Observaciones
        var matchObs = Regex.Match(texto, @"FACTURA\s*A\s*(\d+\s*D[IÍ]AS?)", RegexOptions.IgnoreCase);
        if (matchObs.Success) doc.Observaciones = matchObs.Value.Trim();

        // Items
        doc.Items = ExtraerItems(texto);

        // Calcular confianza según campos extraídos (7 campos clave)
        int camposExtraidos = 0;
        if (doc.TipoDocumento != null)       camposExtraidos++;
        if (doc.NumeroDocumento != null)     camposExtraidos++;
        if (doc.RucEmisor != null)           camposExtraidos++;
        if (doc.FechaEmision != null)        camposExtraidos++;
        if (doc.TotalPagar != null || doc.BaseImponible != null) camposExtraidos++;
        if (doc.RazonSocialReceptor != null) camposExtraidos++;
        if (doc.FechaVencimiento != null)    camposExtraidos++;
        doc.Confianza = Math.Min(1.0, camposExtraidos / 7.0);
    }

    private static decimal? ExtraerMonto(string texto, string patron)
    {
        var match = Regex.Match(texto, patron, RegexOptions.IgnoreCase);
        if (!match.Success) return null;
        var valor = match.Groups[1].Value.Replace(",", "");
        return decimal.TryParse(valor, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    private static List<ItemDocumento> ExtraerItems(string texto)
    {
        var items = new List<ItemDocumento>();
        // Patrón: cantidad + descripción + monto al final de la línea
        var lineas = texto.Split('\n');
        foreach (var linea in lineas)
        {
            // Busca líneas con patrón: número descripción número (tabla de detalles)
            var match = Regex.Match(linea.Trim(),
                @"^(\d+[\.,]?\d*)\s+(.+?)\s+(\d+[\.,]\d{2})\s*$");
            if (match.Success)
            {
                if (decimal.TryParse(match.Groups[1].Value.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var cant) &&
                    decimal.TryParse(match.Groups[3].Value.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var valor))
                {
                    items.Add(new ItemDocumento
                    {
                        Descripcion = match.Groups[2].Value.Trim(),
                        Cantidad = cant,
                        ValorVenta = valor
                    });
                }
            }
        }
        return items;
    }
}
