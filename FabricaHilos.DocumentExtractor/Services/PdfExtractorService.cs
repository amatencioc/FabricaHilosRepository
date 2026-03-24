using System.Globalization;
using System.Runtime.InteropServices;
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
    // Search tessdata in multiple locations; gracefully degrade if not found.
    private static readonly string? TessDataPath = FindTessData();

    // Static date format arrays shared across extraction methods.
    private static readonly string[] FormatosFecha = ["dd/MM/yyyy", "dd-MM-yyyy", "yyyy-MM-dd", "yyyy/MM/dd", "d/M/yyyy", "d-M-yyyy"];
    private static readonly string[] FormatosFechaEspanol = ["d 'de' MMMM 'de' yyyy", "dd 'de' MMMM 'de' yyyy"];

    public static string? GetTessDataPathForDiagnostics() => TessDataPath;

    private static string? FindTessData()
    {
        var candidates = new List<string?>
        {
            // Rutas relativas al ejecutable (producción y publicación)
            Path.Combine(AppContext.BaseDirectory, "tessdata"),
            Path.Combine(AppContext.BaseDirectory, "..", "tessdata"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "tessdata"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "tessdata"),

            // Directorio de trabajo actual (desde donde se corre dotnet run)
            Path.Combine(Directory.GetCurrentDirectory(), "tessdata"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "tessdata"),

            // Variable de entorno estándar de Tesseract
            Environment.GetEnvironmentVariable("TESSDATA_PREFIX"),

            // Instalaciones típicas de Tesseract en Windows
            @"C:\Program Files\Tesseract-OCR\tessdata",
            @"C:\Program Files (x86)\Tesseract-OCR\tessdata",
            @"C:\Tesseract-OCR\tessdata",
            @"C:\tessdata",

            // Rutas de usuario en Windows
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "tessdata"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "tessdata"),
        };

        return candidates
            .OfType<string>()
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => Path.GetFullPath(p))
            .FirstOrDefault(p => Directory.Exists(p) &&
                (File.Exists(Path.Combine(p, "spa.traineddata")) ||
                 File.Exists(Path.Combine(p, "eng.traineddata"))));
    }

    public Task<DocumentoExtraido> ExtraerAsync(Stream archivo, string tipoMime, string nombreArchivo)
    {
        var resultado = new DocumentoExtraido
        {
            NombreArchivo = nombreArchivo,
            FuenteExtraccion = "PdfPig"
        };

        bool esImagen = tipoMime is "image/png" or "image/jpeg" or "image/jpg" or "image/tiff" or "image/bmp" or "image/webp";
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
            using (var pdf = PdfDocument.Open(new MemoryStream(pdfBytes)))
            {
                foreach (UglyToad.PdfPig.Content.Page page in pdf.GetPages())
                    sb.AppendLine(page.Text);
            }

            string texto = sb.ToString();
            if (texto.Trim().Length < 50)
            {
                usedOcr = true;
                string textoOcr = ExtraerTextoOcrDesdePdf(pdfBytes);
                return string.IsNullOrWhiteSpace(textoOcr) ? texto : textoOcr;
            }
            return texto;
        }
        if (tipoMime is "image/png" or "image/jpeg" or "image/jpg" or "image/tiff" or "image/bmp" or "image/webp")
        {
            usedOcr = true;
            return ExtraerTextoOcr(archivo);
        }
        return string.Empty;
    }

    private static string ExtraerTextoOcrDesdePdf(byte[] pdfBytes)
    {
        var sb = new StringBuilder();
        var renderOptions = new RenderOptions { Dpi = 300, Grayscale = true };

        foreach (var bitmap in Conversion.ToImages(pdfBytes, options: renderOptions))
        {
            using (bitmap)
                sb.AppendLine(OcrBytes(PreprocesarBitmap(bitmap)));
        }
        return sb.ToString();
    }

    private static byte[] PreprocesarBitmap(SKBitmap original)
    {
        if (original == null) return Array.Empty<byte>();

        int w = original.Width, h = original.Height;
        const int minLongSide = 2400;
        float scale = (float)minLongSide / Math.Max(w, h);

        using var upscaled = scale > 1f
            ? original.Resize(new SKImageInfo((int)(w * scale), (int)(h * scale)),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None))
            : null;
        var source = upscaled ?? original;

        const float c = 1.4f;
        float t = (1f - c) / 2f * 255f;
        float[] cm = [c, 0, 0, 0, t, 0, c, 0, 0, t, 0, 0, c, 0, t, 0, 0, 0, 1, 0];

        using var output = new SKBitmap(source.Width, source.Height);
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

    public Task<(string texto, string fuente)> ExtraerTextoRawAsync(Stream archivo, string tipoMime)
    {
        using var ms = new MemoryStream();
        archivo.CopyTo(ms);
        var bytes = ms.ToArray();

        if (tipoMime == "application/pdf")
        {
            var sb = new StringBuilder();
            ms.Position = 0;
            using (var pdf = PdfDocument.Open(ms))
            {
                foreach (UglyToad.PdfPig.Content.Page page in pdf.GetPages())
                    sb.AppendLine(page.Text);
            }
            string textoNativo = sb.ToString();
            if (textoNativo.Trim().Length >= 50)
                return Task.FromResult((textoNativo, "PdfPig"));

            // Fallback a OCR
            if (TessDataPath != null)
            {
                string textoOcr = ExtraerTextoOcrDesdePdf(bytes);
                if (!string.IsNullOrWhiteSpace(textoOcr))
                    return Task.FromResult((textoOcr, "Tesseract-OCR (PDF)"));
            }
            return Task.FromResult((textoNativo, "PdfPig (sin OCR)"));
        }

        if (tipoMime is "image/png" or "image/jpeg" or "image/jpg" or "image/tiff" or "image/bmp" or "image/webp")
        {
            string textoOcr = OcrBytes(PreprocesarImagen(bytes));
            return Task.FromResult((textoOcr, "Tesseract-OCR"));
        }

        return Task.FromResult((string.Empty, "Ninguna"));
    }

    // LSTM OCR with dual-pass PSM: Auto first, SparseText fallback for forms/invoices.
    // Uses spa+eng for best coverage of Peruvian documents. Gracefully falls back if tessdata missing.
    private static string OcrBytes(byte[] imageBytes)
    {
        if (TessDataPath == null)
            return string.Empty; // Tessdata not found; caller falls back to PdfPig result.

        // Try spa+eng combined; if eng.traineddata is missing, fall back to spa only.
        string lang = File.Exists(Path.Combine(TessDataPath, "eng.traineddata")) ? "spa+eng" : "spa";

        using var engine = new TesseractEngine(TessDataPath, lang, EngineMode.LstmOnly);
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
            if (TessDataPath == null)
            {
                string installHint = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? @"Coloque los archivos en la carpeta 'tessdata' del proyecto o instale Tesseract-OCR en C:\Program Files\Tesseract-OCR\"
                    : "Coloque los archivos en la carpeta 'tessdata' del proyecto o instale Tesseract-OCR en el sistema.";
                doc.MensajeError = "OCR no disponible: No se encontraron archivos tessdata (spa.traineddata/eng.traineddata). " + installHint;
            }
            else
                doc.MensajeError = $"No se pudo extraer texto del documento. " +
                    $"Fuente: {doc.FuenteExtraccion}. " +
                    $"Tesseract activo en: {TessDataPath}";
            doc.Confianza = 0;
            return;
        }

        // ── Tipo de documento ──────────────────────────────────────────────────────
        if (Regex.IsMatch(texto, @"NOTA\s*DE?\s*CR[EÉ]DITO|NOTA\s*CR[EÉ]DITO", RegexOptions.IgnoreCase))
            doc.TipoDocumento = "NOTA DE CRÉDITO";
        else if (Regex.IsMatch(texto, @"NOTA\s*DE?\s*D[EÉ]BITO|NOTA\s*D[EÉ]BITO", RegexOptions.IgnoreCase))
            doc.TipoDocumento = "NOTA DE DÉBITO";
        else if (Regex.IsMatch(texto, @"GU[IÍ]A\s*DE\s*REMISI[OÓ]N|GUIA\s*REMISION", RegexOptions.IgnoreCase))
            doc.TipoDocumento = "GUÍA DE REMISIÓN";
        else if (Regex.IsMatch(texto, @"RECIBO\s*(?:POR\s*)?HONORARIOS|HONORARIOS", RegexOptions.IgnoreCase))
            doc.TipoDocumento = "RECIBO POR HONORARIOS";
        else if (Regex.IsMatch(texto, @"FACTURA", RegexOptions.IgnoreCase))
            doc.TipoDocumento = "FACTURA";
        else if (Regex.IsMatch(texto, @"BOLETA", RegexOptions.IgnoreCase))
            doc.TipoDocumento = "BOLETA";
        else if (Regex.IsMatch(texto, @"SEDAPAL|Servicio\s*de\s*Agua\s*Potable", RegexOptions.IgnoreCase))
            doc.TipoDocumento = "RECIBO DE AGUA";
        else if (Regex.IsMatch(texto, @"RECIBO\s*DE\s*AGUA|RECIBO\s*DE\s*LUZ|RECIBO\s*DE\s*SERVICIO", RegexOptions.IgnoreCase))
            doc.TipoDocumento = "RECIBO DE SERVICIO";
        else if (Regex.IsMatch(texto, @"RECIBO", RegexOptions.IgnoreCase))
            doc.TipoDocumento = "RECIBO";

        // ── N° comprobante: prefijos SUNAT ampliados + recibos SEDAPAL/servicios ────
        // Cubre: E001, F001, B001, T001, variantes multi-letra RC01, RH001, y SEDAPAL S107-XXXXXXXX
        var matchComp = Regex.Match(texto, @"\b([EFBTS]\d{3}|[A-Z]{1,4}\d{2,3})-(\d{4,10})\b");
        if (matchComp.Success)
        {
            doc.Serie = matchComp.Groups[1].Value;
            doc.Correlativo = matchComp.Groups[2].Value;
            doc.NumeroDocumento = $"{doc.Serie}-{doc.Correlativo}";
        }
        else
        {
            // Buscar patrones como "N° RECIBO: 12345", "RECIBO N°: 12345", "N° BOLETA: 12345"
            var matchNro = Regex.Match(texto, @"(?:N[°º]?\s*(?:RECIBO|BOLETA|FACTURA)|(?:RECIBO|BOLETA|FACTURA)\s*N[°º]?)\s*:?\s*(\d{4,10})", RegexOptions.IgnoreCase);
            if (matchNro.Success)
            {
                doc.Correlativo = matchNro.Groups[1].Value;
                doc.NumeroDocumento = doc.Correlativo;
            }
        }

        // ── RUC: personas jurídicas (20x) y personas naturales (10x) ──────────────
        // Prefer labeled matches; use unlabeled as fallback. If labeled gives only one,
        // try to find a second RUC from the unlabeled set.
        var rucConEtiqueta = Regex.Matches(texto, @"\bR\.?U\.?C\.?\s*:?\s*((20|10)\d{9})\b", RegexOptions.IgnoreCase);
        var rucsBare       = Regex.Matches(texto, @"\b((20|10)\d{9})\b");
        var etiquetados    = rucConEtiqueta.Cast<Match>().Select(m => m.Groups[1].Value).Distinct().ToList();
        var todosRucs      = rucsBare.Cast<Match>().Select(m => m.Groups[1].Value).Distinct().ToList();

        // Use labeled list when both emisor and receptor appear with labels;
        // otherwise, prefer labeled for emisor and fall back to bare list for receptor.
        if (etiquetados.Count >= 2)
        {
            doc.RucEmisor   = etiquetados[0];
            doc.RucReceptor = etiquetados[1];
        }
        else if (etiquetados.Count == 1)
        {
            doc.RucEmisor = etiquetados[0];
            doc.RucReceptor = todosRucs.FirstOrDefault(r => r != doc.RucEmisor);
        }
        else
        {
            if (todosRucs.Count >= 1) doc.RucEmisor   = todosRucs[0];
            if (todosRucs.Count >= 2) doc.RucReceptor = todosRucs[1];
        }

        // ── Razón Social Emisor ────────────────────────────────────────────────────
        // Buscar por etiqueta explícita
        var matchRsEm = Regex.Match(texto,
            @"(?:Raz[oó]n\s*Social\s*(?:del?\s*)?Emisor|Empresa|Proveedor)\s*:?\s*(.+?)(?=\s{2,}|\bRUC\b|[\r\n])",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (matchRsEm.Success)
        {
            doc.RazonSocialEmisor = matchRsEm.Groups[1].Value.Trim();
        }
        else if (doc.RucEmisor != null)
        {
            // Intentar extraer de las líneas previas al RUC del emisor
            var lineas = texto.Split('\n');
            int rucIdx = Array.FindIndex(lineas, l => l.Contains(doc.RucEmisor));
            if (rucIdx > 0)
            {
                // Primera línea en mayúsculas antes del RUC (nombre de empresa)
                for (int i = rucIdx - 1; i >= Math.Max(0, rucIdx - 5); i--)
                {
                    var linea = lineas[i].Trim();
                    if (linea.Length > 5 && linea == linea.ToUpperInvariant() && !Regex.IsMatch(linea, @"^\d"))
                    {
                        doc.RazonSocialEmisor = linea;
                        break;
                    }
                }
            }
        }

        // ── Dirección Emisor ───────────────────────────────────────────────────────
        var matchDirEm = Regex.Match(texto,
            @"(?:Direcci[oó]n\s*(?:del?\s*)?Emisor|Domicilio\s*Fiscal)\s*:?\s*(.+?)(?=\s{2,}|\bRUC\b|\bTel[eé]f|\bFax\b|[\r\n])",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (matchDirEm.Success)
        {
            doc.DireccionEmisor = matchDirEm.Groups[1].Value.Trim();
        }
        else
        {
            // Buscar líneas con indicadores de dirección (Av., Jr., Calle, Urb., etc.)
            var matchDirGeneral = Regex.Match(texto,
                @"((?:Av\.?|Jr\.?|Calle|Ca\.?|Urb\.?|Psje\.?|Mz\.?)\s+[^\n\r]{5,60}?)(?=[\r\n]|\bTel[eé]f|\bFax\b|\bRUC\b)",
                RegexOptions.IgnoreCase);
            if (matchDirGeneral.Success)
                doc.DireccionEmisor = matchDirGeneral.Groups[1].Value.Trim();
        }

        // ── Razón Social Receptor ──────────────────────────────────────────────────
        var matchRsRec = Regex.Match(texto,
            @"(?:(?:Nombre\s*/?\s*)?Raz[oó]n\s*Social|Se[nñ]or(?:es)?|Cliente|PARA)\s*:?\s*(.+?)(?=\s{3,}|\bRUC\b|\bDirecci[oó]n\b|[\r\n])",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (matchRsRec.Success) doc.RazonSocialReceptor = matchRsRec.Groups[1].Value.Trim();

        // ── Dirección Receptor ─────────────────────────────────────────────────────
        var matchDirRec = Regex.Match(texto,
            @"Direcci[oó]n\s*:?\s*(.+?)(?=\s{3,}|\bFecha\b|\bMoneda\b|\bRUC\b|[\r\n])",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (matchDirRec.Success) doc.DireccionReceptor = matchDirRec.Groups[1].Value.Trim();

        // ── Fecha Emisión ──────────────────────────────────────────────────────────
        doc.FechaEmision = ExtraerFecha(texto,
            new[]
            {
                @"Fecha\s*(?:de\s*)?(?:Emisi[oó]n|Expedici[oó]n)\b[^/\-\d]*(\d{2}[/\-]\d{2}[/\-]\d{4})",
                @"Fecha\s*(?:de\s*)?(?:Emisi[oó]n|Expedici[oó]n)\b[^/\-\d]*(\d{4}[/\-]\d{2}[/\-]\d{2})",
                @"\b(\d{2}/\d{2}/\d{4})\b",
                @"\b(\d{2}-\d{2}-\d{4})\b",
                @"\b(\d{4}-\d{2}-\d{2})\b",
                @"(\d{1,2}\s+de\s+\w+\s+de\s+\d{4})"
            });

        // ── Fecha Vencimiento ──────────────────────────────────────────────────────
        doc.FechaVencimiento = ExtraerFechaConEtiqueta(texto,
            @"Fecha\s*(?:de\s*)?(?:Vencimiento|Pago|Caducidad)\b[^/\-\d]*(\d{2}[/\-]\d{2}[/\-]\d{4})");

        // ── Hora ───────────────────────────────────────────────────────────────────
        var matchHora = Regex.Match(texto, @"\b(\d{2}:\d{2}:\d{2})\b");
        if (matchHora.Success && TimeSpan.TryParse(matchHora.Groups[1].Value, out var hora))
            doc.HoraEmision = hora;

        // ── Moneda ─────────────────────────────────────────────────────────────────
        doc.Moneda = Regex.IsMatch(texto, @"D[oó]lares?|USD|\$", RegexOptions.IgnoreCase) ? "USD" : "PEN";

        // ── Montos ─────────────────────────────────────────────────────────────────
        doc.BaseImponible = ExtraerMonto(texto,
            @"(?:Sub\s*Total|Op\.?\s*Gravad[ao]|Total\s*Gravad[ao]|Base\s*Imponible)[^\d]*([\d,\.]+)");

        doc.TotalIgv = ExtraerMonto(texto,
            @"(?:I\.?G\.?V\.?|Impuesto\s*(?:General\s*)?a\s*la\s*Venta)[^\d]*([\d,\.]+)");

        doc.TotalPagar = ExtraerMonto(texto,
            @"(?:Importe\s*Total|TOTAL\s*A\s*PAGAR|TOTAL\s*GENERAL|TOTAL\s*PAGAR|Total\s*a\s*Cobrar|IMPORTE\s*A\s*PAGAR)[^\d]*([\d,\.]+)");
        // Fallback 1: monto con asteriscos (formato SEDAPAL: S/ *****684.20)
        if (doc.TotalPagar == null)
            doc.TotalPagar = ExtraerMonto(texto, @"S/\.?\s*\*+([\d,\.]+)");
        // Fallback 2: linea que contenga solo "TOTAL" seguido de monto
        if (doc.TotalPagar == null)
            doc.TotalPagar = ExtraerMonto(texto, @"(?<!\w)TOTAL(?!\s*(?:GRAVAD|EXONER|INAFECT|DESCUENT|ANTICIP|GRATU))[^\d]*([\d,\.]+)");

        doc.TotalExonerado = ExtraerMonto(texto,
            @"(?:Total\s*Exonerado|Op\.?\s*Exonerada)[^\d]*([\d,\.]+)");

        doc.TotalInafecto = ExtraerMonto(texto,
            @"(?:Total\s*Inafecto|Op\.?\s*Inafecta)[^\d]*([\d,\.]+)");

        doc.TotalDescuento = ExtraerMonto(texto,
            @"(?:Descuento\s*Global|Total\s*Descuento|Descuento)[^\d]*([\d,\.]+)");

        doc.TotalAnticipos = ExtraerMonto(texto,
            @"(?:Anticipo)[^\d]*([\d,\.]+)");

        doc.TotalGratuito = ExtraerMonto(texto,
            @"(?:Gratuito|Op\.?\s*Gratuita)[^\d]*([\d,\.]+)");

        // ── Detracción ─────────────────────────────────────────────────────────────
        doc.MontoDetraccion = ExtraerMonto(texto,
            @"(?:DETRACCION|DETRACCIÓN|Detraccion|Detracción)[^\d]*([\d,\.]+)");
        if (doc.MontoDetraccion.HasValue && doc.MontoDetraccion > 0)
            doc.TieneDetraccion = true;

        var matchPctDet = Regex.Match(texto,
            @"(?:porcentaje|%)\s*(?:de\s*)?(?:DETRACCION|DETRACCIÓN)[^\d]*([\d\.]+)", RegexOptions.IgnoreCase);
        if (!matchPctDet.Success)
            matchPctDet = Regex.Match(texto,
                @"(?:DETRACCION|DETRACCIÓN)[^\d%]*(\d{1,3}(?:[.,]\d+)?)\s*%", RegexOptions.IgnoreCase);
        if (matchPctDet.Success)
            doc.PctDetraccion = ParseDecimal(matchPctDet.Groups[1].Value) ?? 0;

        // ── Forma de pago ──────────────────────────────────────────────────────────
        var matchPago = Regex.Match(texto, @"(Cr[eé]dito|Contado|CREDITO|CONTADO|\d+\s*D[IÍ]AS?)", RegexOptions.IgnoreCase);
        if (matchPago.Success) doc.FormaPago = matchPago.Value.Trim();

        // ── N° Pedido / Orden de Compra ────────────────────────────────────────────
        var matchPedido = Regex.Match(texto, @"(?:Orden\s*de\s*Compra|O\.C\.)\s*[:\-]?\s*(\d[\d\-]*)", RegexOptions.IgnoreCase);
        if (matchPedido.Success) doc.NumeroPedido = matchPedido.Groups[1].Value.Trim();

        // ── N° Guía de Remisión ────────────────────────────────────────────────────
        var matchGuia = Regex.Match(texto, @"(?:Gu[íi]a\s*de\s*Remis)[^\n]*?([A-Z]{1,4}\d{2,3}-\d{4,8})", RegexOptions.IgnoreCase);
        if (matchGuia.Success) doc.NumeroGuia = matchGuia.Groups[1].Value.Trim();

        // ── Observaciones ──────────────────────────────────────────────────────────
        var matchObs = Regex.Match(texto, @"FACTURA\s*A\s*(\d+\s*D[IÍ]AS?)", RegexOptions.IgnoreCase);
        if (matchObs.Success) doc.Observaciones = matchObs.Value.Trim();

        // ── Items ──────────────────────────────────────────────────────────────────
        doc.Items = ExtraerItems(texto);

        // ── Calcular confianza (10 campos clave) ───────────────────────────────────
        int total = 10;
        int encontrados = 0;
        if (doc.RucEmisor != null)           encontrados++;
        if (doc.NumeroDocumento != null)     encontrados++;
        if (doc.FechaEmision != null)        encontrados++;
        if (doc.TotalPagar != null)          encontrados++;
        if (doc.TipoDocumento != null)       encontrados++;
        if (doc.RazonSocialEmisor != null)   encontrados++;
        if (doc.RucReceptor != null)         encontrados++;
        if (doc.BaseImponible != null)       encontrados++;
        if (doc.TotalIgv != null)            encontrados++;
        if (doc.Items.Count > 0)             encontrados++;
        doc.Confianza = Math.Min(1.0, encontrados / (double)total);
    }

    // Extraer fecha probando múltiples patrones y formatos.
    private static DateTime? ExtraerFecha(string texto, string[] patrones)
    {
        var culturaEs = new CultureInfo("es-PE");

        foreach (var patron in patrones)
        {
            var m = Regex.Match(texto, patron, RegexOptions.IgnoreCase);
            if (!m.Success) continue;
            var val = m.Groups[1].Value.Trim();

            foreach (var fmt in FormatosFecha)
                if (DateTime.TryParseExact(val, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                    return d;

            foreach (var fmt in FormatosFechaEspanol)
                if (DateTime.TryParseExact(val, fmt, culturaEs, DateTimeStyles.None, out var d))
                    return d;
        }
        return null;
    }

    private static DateTime? ExtraerFechaConEtiqueta(string texto, string patron)
    {
        var m = Regex.Match(texto, patron, RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        var val = m.Groups[1].Value.Trim();
        foreach (var fmt in FormatosFecha)
            if (DateTime.TryParseExact(val, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return d;
        return null;
    }

    private static decimal? ExtraerMonto(string texto, string patron)
    {
        var match = Regex.Match(texto, patron, RegexOptions.IgnoreCase);
        if (!match.Success) return null;
        return ParseDecimal(match.Groups[1].Value);
    }

    // Maneja formato US (1,234.56) y europeo (1.234,56).
    private static decimal? ParseDecimal(string valor)
    {
        valor = valor.Trim();
        if (string.IsNullOrEmpty(valor)) return null;

        if (valor.Contains(',') && valor.Contains('.'))
        {
            int lastComma = valor.LastIndexOf(',');
            int lastDot   = valor.LastIndexOf('.');
            if (lastComma > lastDot) // formato europeo: 1.234,56
                valor = valor.Replace(".", "").Replace(",", ".");
            else // formato US: 1,234.56
                valor = valor.Replace(",", "");
        }
        else if (valor.Contains(','))
        {
            // Solo coma: si hay exactamente 2 dígitos después es separador decimal; si son 3, es miles.
            var partes = valor.Split(',');
            valor = partes.Length == 2 && partes[1].Length == 2
                ? valor.Replace(",", ".")
                : valor.Replace(",", "");
        }

        return decimal.TryParse(valor, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    private static List<ItemDocumento> ExtraerItems(string texto)
    {
        var items = new List<ItemDocumento>();
        var lineas = texto.Split('\n');

        foreach (var linea in lineas)
        {
            var l = linea.Trim();
            if (string.IsNullOrWhiteSpace(l)) continue;

            // Patrón SUNAT estándar ampliado:
            // CODIGO  DESCRIPCION  CANTIDAD  UNIDAD  P.UNIT  [DESCUENTO]  VALOR_VENTA
            var matchSunat = Regex.Match(l,
                @"^([A-Z0-9\-]{2,20})?\s{1,4}(.+?)\s{1,4}(\d+[\.,]\d*)\s{1,4}([A-Z]{2,3})?\s{0,4}([\d,\.]+)\s{0,4}([\d,\.]+)?\s{0,4}([\d,\.]+)\s*$",
                RegexOptions.IgnoreCase);
            if (matchSunat.Success)
            {
                var cant     = ParseDecimal(matchSunat.Groups[3].Value);
                var vUnitario = ParseDecimal(matchSunat.Groups[5].Value);
                var vVenta   = ParseDecimal(matchSunat.Groups[7].Value);

                if (cant.HasValue && vVenta.HasValue)
                {
                    items.Add(new ItemDocumento
                    {
                        Codigo       = matchSunat.Groups[1].Success && !string.IsNullOrWhiteSpace(matchSunat.Groups[1].Value) ? matchSunat.Groups[1].Value.Trim() : null,
                        Descripcion  = matchSunat.Groups[2].Value.Trim(),
                        Cantidad     = cant,
                        UnidadMedida = matchSunat.Groups[4].Success && !string.IsNullOrWhiteSpace(matchSunat.Groups[4].Value) ? matchSunat.Groups[4].Value.Trim() : null,
                        ValorUnitario = vUnitario,
                        ValorVenta   = vVenta
                    });
                    continue;
                }
            }

            // Patrón simple: cantidad + descripción + monto al final de la línea
            var matchSimple = Regex.Match(l,
                @"^(\d+[\.,]?\d*)\s+(.+?)\s+([\d,\.]+)\s*$");
            if (matchSimple.Success)
            {
                var cant   = ParseDecimal(matchSimple.Groups[1].Value);
                var vVenta = ParseDecimal(matchSimple.Groups[3].Value);
                if (cant.HasValue && vVenta.HasValue)
                {
                    items.Add(new ItemDocumento
                    {
                        Descripcion = matchSimple.Groups[2].Value.Trim(),
                        Cantidad    = cant,
                        ValorVenta  = vVenta
                    });
                }
            }
        }
        return items;
    }
}
