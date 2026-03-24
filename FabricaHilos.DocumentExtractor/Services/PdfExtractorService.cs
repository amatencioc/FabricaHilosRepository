using System.Text;
using System.Text.RegularExpressions;
using FabricaHilos.DocumentExtractor.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace FabricaHilos.DocumentExtractor.Services;

public class PdfExtractorService : IDocumentExtractorService
{
    public Task<DocumentoExtraido> ExtraerAsync(Stream archivo, string tipoMime, string nombreArchivo)
    {
        var resultado = new DocumentoExtraido
        {
            NombreArchivo = nombreArchivo,
            FuenteExtraccion = "PdfPig"
        };

        try
        {
            string texto = ExtraerTexto(archivo, tipoMime);
            ProcesarTexto(texto, resultado);
        }
        catch (Exception ex)
        {
            resultado.MensajeError = ex.Message;
            resultado.Confianza = 0;
        }

        return Task.FromResult(resultado);
    }

    private static string ExtraerTexto(Stream archivo, string tipoMime)
    {
        if (tipoMime == "application/pdf")
        {
            var sb = new StringBuilder();
            using var pdf = PdfDocument.Open(archivo);
            foreach (Page page in pdf.GetPages())
                sb.AppendLine(page.Text);
            return sb.ToString();
        }
        // For images: return empty (no OCR support without additional library)
        return string.Empty;
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
        var matchComp = Regex.Match(texto, @"\b([EFBT]\d{3})-(\d{4,8})\b");
        if (matchComp.Success)
        {
            doc.Serie = matchComp.Groups[1].Value;
            doc.Correlativo = matchComp.Groups[2].Value;
            doc.NumeroDocumento = $"{doc.Serie}-{doc.Correlativo}";
        }

        // RUCs: en Perú los RUC de personas jurídicas comienzan con 20; primer RUC = emisor, segundo = receptor
        var rucs = Regex.Matches(texto, @"\b(20\d{9})\b");
        if (rucs.Count >= 1) doc.RucEmisor = rucs[0].Groups[1].Value;
        if (rucs.Count >= 2) doc.RucReceptor = rucs[1].Groups[1].Value;

        // Fechas: primera = emisión, segunda = vencimiento
        var fechas = Regex.Matches(texto, @"\b(\d{2}/\d{2}/\d{4})\b");
        if (fechas.Count >= 1 && DateTime.TryParseExact(fechas[0].Groups[1].Value, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out var fe))
            doc.FechaEmision = fe;
        if (fechas.Count >= 2 && DateTime.TryParseExact(fechas[1].Groups[1].Value, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out var fv))
            doc.FechaVencimiento = fv;

        // Hora
        var matchHora = Regex.Match(texto, @"\b(\d{2}:\d{2}:\d{2})\b");
        if (matchHora.Success && TimeSpan.TryParse(matchHora.Groups[1].Value, out var hora))
            doc.HoraEmision = hora;

        // Moneda
        doc.Moneda = Regex.IsMatch(texto, @"DOLAR|USD|\$", RegexOptions.IgnoreCase) ? "USD" : "PEN";

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

        // N° Pedido / Orden de Compra
        var matchPedido = Regex.Match(texto, @"(?:Orden\s*de\s*Compra|O\.C\.)[^\w]*([A-Z0-9\-]+)", RegexOptions.IgnoreCase);
        if (matchPedido.Success) doc.NumeroPedido = matchPedido.Groups[1].Value.Trim();

        // N° Guía de Remisión
        var matchGuia = Regex.Match(texto, @"(?:Guía\s*de\s*Remis|GUIA)[^\w]*([A-Z0-9\-]+)", RegexOptions.IgnoreCase);
        if (matchGuia.Success) doc.NumeroGuia = matchGuia.Groups[1].Value.Trim();

        // Observaciones
        var matchObs = Regex.Match(texto, @"FACTURA\s*A\s*(\d+\s*D[IÍ]AS?)", RegexOptions.IgnoreCase);
        if (matchObs.Success) doc.Observaciones = matchObs.Value.Trim();

        // Items
        doc.Items = ExtraerItems(texto);

        // Calcular confianza según campos extraídos
        int camposExtraidos = 0;
        if (doc.RucEmisor != null) camposExtraidos++;
        if (doc.NumeroDocumento != null) camposExtraidos++;
        if (doc.FechaEmision != null) camposExtraidos++;
        if (doc.TotalPagar != null) camposExtraidos++;
        if (doc.TipoDocumento != null) camposExtraidos++;
        doc.Confianza = Math.Min(1.0, camposExtraidos / 5.0);
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
