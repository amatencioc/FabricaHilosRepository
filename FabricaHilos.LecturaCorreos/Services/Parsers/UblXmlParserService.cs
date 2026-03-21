namespace FabricaHilos.LecturaCorreos.Services.Parsers;

using System.Xml.Linq;
using FabricaHilos.LecturaCorreos.Models;
using Microsoft.Extensions.Logging;

public class UblXmlParserService : IXmlParserService
{
    // ── Namespaces UBL 2.1 peruano ────────────────────────────────────────────
    static readonly XNamespace NsInvoice  = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    static readonly XNamespace NsDespatch = "urn:oasis:names:specification:ubl:schema:xsd:DespatchAdvice-2";
    static readonly XNamespace NsCredit   = "urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2";
    static readonly XNamespace NsDebit    = "urn:oasis:names:specification:ubl:schema:xsd:DebitNote-2";
    static readonly XNamespace NsCac      = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    static readonly XNamespace NsCbc      = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    static readonly XNamespace NsSac      = "urn:sunat:names:specification:ubl:peru:schema:xsd:SunatAggregateComponents-1";
    static readonly XNamespace NsFac      = "urn:facele:names:specification:ubl:peru:schema:xsd:FaceleAggregateComponents-1";

    private readonly ILogger<UblXmlParserService> _logger;

    public UblXmlParserService(ILogger<UblXmlParserService> logger)
    {
        _logger = logger;
    }

    public ResultadoParseo Parsear(string xmlContenido, string nombreArchivo, string cuentaCorreo,
                                   string asunto, string remitente, DateTime fechaCorreo)
    {
        try
        {
            var doc = XDocument.Parse(xmlContenido);
            var root = doc.Root;
            if (root is null)
                return new ResultadoParseo(EstadoParseo.XmlInvalido,
                    Descripcion: "El documento XML no tiene elemento raíz.");

            var localName = root.Name.LocalName;

            var documento = new DocumentoXml
            {
                NombreArchivo   = nombreArchivo,
                CuentaCorreo    = cuentaCorreo,
                AsuntoCorreo    = asunto,
                RemitenteCorreo = remitente,
                FechaCorreo     = fechaCorreo,
                XmlContenido    = xmlContenido,
            };

            switch (localName)
            {
                case "Invoice":
                    documento.TipoXml = "INVOICE";
                    ParsearFactura(root, documento);
                    break;
                case "CreditNote":
                    documento.TipoXml = "CREDIT_NOTE";
                    ParsearFactura(root, documento);
                    break;
                case "DebitNote":
                    documento.TipoXml = "DEBIT_NOTE";
                    ParsearFactura(root, documento);
                    break;
                case "DespatchAdvice":
                    documento.TipoXml = "DESPATCH_ADVICE";
                    ParsearGuiaRemision(root, documento);
                    break;
                case "ApplicationResponse":
                    documento.TipoXml = "APPLICATION_RESPONSE";
                    documento.EsCdr   = true;
                    ParsearApplicationResponse(root, documento);
                    _logger.LogDebug("CDR (ApplicationResponse) '{Archivo}' parseado correctamente.",
                        nombreArchivo);
                    // Serie y Correlativo ya establecidos por ParsearApplicationResponse.
                    // No se aplica SepararNumeroDoc (NumeroDocumento lleva prefijo "CDR-").
                    return new ResultadoParseo(EstadoParseo.Exito, documento);
                default:
                    _logger.LogWarning("XML '{Archivo}' no reconocido: elemento raíz '{Root}'.",
                        nombreArchivo, localName);
                    return new ResultadoParseo(EstadoParseo.TipoNoReconocido,
                        Descripcion: $"Elemento raíz no soportado: <{localName}>.");
            }

            var partes = SepararNumeroDoc(documento.NumeroDocumento);
            documento.Serie       = partes[0];
            documento.Correlativo = partes[1];

            return new ResultadoParseo(EstadoParseo.Exito, documento);
        }
        catch (System.Xml.XmlException ex)
        {
            _logger.LogError(ex, "XML mal formado en '{Archivo}' (línea {Linea}, pos {Pos}).",
                nombreArchivo, ex.LineNumber, ex.LinePosition);
            return new ResultadoParseo(EstadoParseo.XmlInvalido,
                Descripcion: $"XML mal formado en línea {ex.LineNumber}, pos {ex.LinePosition}: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al parsear '{Archivo}'.", nombreArchivo);
            return new ResultadoParseo(EstadoParseo.XmlInvalido,
                Descripcion: $"Error inesperado: {ex.Message}");
        }
    }

    // ── Invoice / CreditNote / DebitNote ──────────────────────────────────────
    private void ParsearFactura(XElement root, DocumentoXml doc)
    {
        doc.NumeroDocumento = Elem(root, NsCbc, "ID") ?? string.Empty;
        doc.FechaEmision    = Fecha(Elem(root, NsCbc, "IssueDate"));
        doc.HoraEmision     = Elem(root, NsCbc, "IssueTime") ?? string.Empty;
        doc.FechaVencimiento = Fecha(Elem(root, NsCbc, "DueDate"));
        doc.Moneda          = Elem(root, NsCbc, "DocumentCurrencyCode") ?? string.Empty;

        // Facturas/Boletas incluyen InvoiceTypeCode ("01","03").
        // Notas de Crédito/Débito no incluyen ese nodo: se deriva del elemento raíz.
        doc.TipoDocumento = Elem(root, NsCbc, "InvoiceTypeCode")
                         ?? Elem(root, NsCbc, "CreditNoteTypeCode")
                         ?? Elem(root, NsCbc, "DebitNoteTypeCode")
                         ?? doc.TipoXml switch
                            {
                                "CREDIT_NOTE" => "07",
                                "DEBIT_NOTE"  => "08",
                                _             => string.Empty,
                            };

        // ── Emisor ────────────────────────────────────────────────────────────
        var supplier = root.Element(NsCac + "AccountingSupplierParty")
                           ?.Element(NsCac + "Party");
        if (supplier is not null)
        {
            doc.RucEmisor             = Elem(supplier?.Element(NsCac + "PartyIdentification"), NsCbc, "ID") ?? string.Empty;
            doc.NombreComercialEmisor = Elem(supplier?.Element(NsCac + "PartyName"), NsCbc, "Name") ?? string.Empty;
            doc.RazonSocialEmisor     = Elem(supplier?.Element(NsCac + "PartyLegalEntity"), NsCbc, "RegistrationName") ?? string.Empty;

            var regAddr = supplier?.Element(NsCac + "PartyLegalEntity")
                                   ?.Element(NsCac + "RegistrationAddress");
            if (regAddr is null)
                regAddr = supplier?.Element(NsCac + "PostalAddress");

            doc.UbigeoEmisor    = Elem(regAddr, NsCbc, "ID") ?? string.Empty;
            doc.DireccionEmisor = Elem(regAddr?.Element(NsCac + "AddressLine"), NsCbc, "Line") ?? string.Empty;
        }

        // ── Receptor ──────────────────────────────────────────────────────────
        var customer = root.Element(NsCac + "AccountingCustomerParty")
                           ?.Element(NsCac + "Party");
        if (customer is not null)
        {
            doc.RucReceptor         = Elem(customer?.Element(NsCac + "PartyIdentification"), NsCbc, "ID") ?? string.Empty;
            doc.RazonSocialReceptor = Elem(customer?.Element(NsCac + "PartyLegalEntity"), NsCbc, "RegistrationName") ?? string.Empty;

            var custAddr = customer?.Element(NsCac + "PartyLegalEntity")
                                    ?.Element(NsCac + "RegistrationAddress");
            if (custAddr is null)
                custAddr = customer?.Element(NsCac + "PostalAddress");

            doc.UbigeoReceptor    = Elem(custAddr, NsCbc, "ID") ?? string.Empty;
            doc.DireccionReceptor = Elem(custAddr?.Element(NsCac + "AddressLine"), NsCbc, "Line") ?? string.Empty;
        }

        // ── Importes ──────────────────────────────────────────────────────────
        var taxTotal = root.Element(NsCac + "TaxTotal");
        doc.TotalIgv = Dec(taxTotal, NsCbc, "TaxAmount");

        foreach (var sub in root.Elements(NsCac + "TaxTotal")
                                 .SelectMany(t => t.Elements(NsCac + "TaxSubtotal")))
        {
            var taxCat  = sub.Element(NsCac + "TaxCategory");
            var taxCode = Elem(taxCat?.Element(NsCac + "TaxScheme"), NsCbc, "ID");
            var monto   = Dec(sub, NsCbc, "TaxableAmount");

            switch (taxCode)
            {
                case "1000": doc.BaseImponible  += monto; break;
                case "9997": doc.TotalExonerado += monto; break;
                case "9998": doc.TotalInafecto  += monto; break;
                case "9996": doc.TotalGratuito  += monto; break;
            }
        }

        var lmt = root.Element(NsCac + "LegalMonetaryTotal");
        if (lmt is not null)
        {
            if (doc.BaseImponible == 0)
                doc.BaseImponible = Dec(lmt, NsCbc, "LineExtensionAmount");
            doc.TotalDescuento = Dec(lmt, NsCbc, "AllowanceTotalAmount");
            doc.TotalCargo     = Dec(lmt, NsCbc, "ChargeTotalAmount");
            doc.TotalAnticipos = Dec(lmt, NsCbc, "PrepaidAmount");
            doc.TotalPagar     = Dec(lmt, NsCbc, "PayableAmount");
        }

        // ── Forma de pago y cuotas ─────────────────────────────────────────────
        foreach (var pt in root.Elements(NsCac + "PaymentTerms"))
        {
            var ptId = Elem(pt, NsCbc, "ID");
            if (ptId?.Equals("FormaPago", StringComparison.OrdinalIgnoreCase) == true)
            {
                doc.FormaPago          = Elem(pt, NsCbc, "PaymentMeansID") ?? string.Empty;
                doc.MontoNetoPendiente = Dec(pt, NsCbc, "Amount");
            }
            else if (ptId?.StartsWith("Cuota", StringComparison.OrdinalIgnoreCase) == true)
            {
                doc.Cuotas.Add(new CuotaPago
                {
                    NumeroCuota      = ptId,
                    FechaVencimiento = Fecha(Elem(pt, NsCbc, "PaymentDueDate")),
                    Monto            = Dec(pt, NsCbc, "Amount"),
                    Moneda           = doc.Moneda,
                });
            }
        }

        // ── Detracción ────────────────────────────────────────────────────────
        var detraccion = root.Elements(NsCac + "PaymentMeans")
                             .FirstOrDefault(pm =>
                                 Elem(pm, NsCbc, "ID")?.Equals("Detraccion",
                                     StringComparison.OrdinalIgnoreCase) == true);
        if (detraccion is not null)
        {
            doc.TieneDetraccion     = true;
            doc.CodBienDetraccion   = Elem(detraccion, NsCbc, "PaymentMeansCode") ?? string.Empty;
            doc.NroCuentaDetraccion = Elem(
                detraccion.Element(NsCac + "PayeeFinancialAccount"), NsCbc, "ID") ?? string.Empty;
            doc.PctDetraccion   = Dec(detraccion, NsCbc, "PaymentPercent");
            doc.MontoDetraccion = Dec(detraccion, NsCbc, "PaymentAmount");
        }

        if (!doc.TieneDetraccion)
        {
            var sunatExt = BuscarExtensionSunat(root);
            if (sunatExt is not null)
            {
                var detSunat = sunatExt.Element(NsSac + "AdditionalInformation")
                                        ?.Elements(NsSac + "SunatTransaction")
                                        .FirstOrDefault(t =>
                                            Elem(t, NsCbc, "ID")?.Equals("Detraccion",
                                                StringComparison.OrdinalIgnoreCase) == true);
                if (detSunat is not null)
                {
                    doc.TieneDetraccion     = true;
                    doc.CodBienDetraccion   = Elem(detSunat, NsSac, "SunatPaymentCode") ?? string.Empty;
                    doc.NroCuentaDetraccion = Elem(detSunat, NsSac, "SunatBankAccount") ?? string.Empty;
                    doc.PctDetraccion       = Dec(detSunat, NsSac, "SunatPaymentPercent");
                    doc.MontoDetraccion     = Dec(detSunat, NsSac, "SunatPaymentAmount");
                }
            }
        }

        // ── Referencias ───────────────────────────────────────────────────────
        doc.NumeroPedido = Elem(root.Element(NsCac + "OrderReference"), NsCbc, "ID") ?? string.Empty;
        doc.NumeroGuia   = Elem(root.Element(NsCac + "DespatchDocumentReference"), NsCbc, "ID") ?? string.Empty;
        doc.NumeroDocRef = Elem(root.Element(NsCac + "BillingReference")
                                    ?.Element(NsCac + "InvoiceDocumentReference"), NsCbc, "ID") ?? string.Empty;

        foreach (var prop in root.Descendants(NsFac + "AdditionalPrintedProperty"))
        {
            var propId  = Elem(prop, NsCbc, "ID")?.ToLowerInvariant();
            var propVal = Elem(prop, NsCbc, "Value") ?? string.Empty;
            switch (propId)
            {
                case "vendedor":
                    doc.Vendedor = propVal;
                    break;
                case "fechavencimiento":
                    if (doc.FechaVencimiento is null)
                        doc.FechaVencimiento = Fecha(propVal);
                    break;
            }
        }

        // ── Líneas ────────────────────────────────────────────────────────────
        var lineaTag = doc.TipoXml switch
        {
            "CREDIT_NOTE" => "CreditNoteLine",
            "DEBIT_NOTE"  => "DebitNoteLine",
            _             => "InvoiceLine",
        };

        foreach (var linea in root.Elements(NsCac + lineaTag))
            doc.Lineas.Add(ParsearLineaFactura(linea));
    }

    private static LineaDocumento ParsearLineaFactura(XElement linea)
    {
        var item      = linea.Element(NsCac + "Item");
        var taxCat    = linea.Element(NsCac + "TaxTotal")
                             ?.Element(NsCac + "TaxSubtotal")
                             ?.Element(NsCac + "TaxCategory");
        var pricingRef = linea.Element(NsCac + "PricingReference")
                               ?.Elements(NsCac + "AlternativeConditionPrice")
                               .FirstOrDefault();
        var qty = linea.Element(NsCbc + "InvoicedQuantity")
               ?? linea.Element(NsCbc + "CreditedQuantity")
               ?? linea.Element(NsCbc + "DebitedQuantity");

        var pctIgv = Dec(taxCat, NsCbc, "Percent");

        return new LineaDocumento
        {
            NumeroLinea    = int.TryParse(Elem(linea, NsCbc, "ID"), out var nl) ? nl : 0,
            Cantidad       = decimal.TryParse(qty?.Value?.Trim(),
                                System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out var q) ? q : 0,
            UnidadMedida   = qty?.Attribute("unitCode")?.Value ?? string.Empty,
            SubTotal       = Dec(linea, NsCbc, "LineExtensionAmount"),
            EsGratuito     = Elem(linea, NsCbc, "FreeOfChargeIndicator")
                                 ?.Equals("true", StringComparison.OrdinalIgnoreCase) == true,
            PrecioConIgv   = Dec(pricingRef, NsCbc, "PriceAmount"),
            PrecioUnitario = Dec(linea.Element(NsCac + "Price"), NsCbc, "PriceAmount"),
            Igv            = Dec(linea.Element(NsCac + "TaxTotal"), NsCbc, "TaxAmount"),
            PorcentajeIgv  = pctIgv == 0 ? 18 : pctIgv,
            AfectacionIgv  = Elem(taxCat, NsCbc, "TaxExemptionReasonCode") ?? string.Empty,
            TotalLinea     = Dec(linea, NsCbc, "LineExtensionAmount")
                           + Dec(linea.Element(NsCac + "TaxTotal"), NsCbc, "TaxAmount"),
            Descripcion    = Elem(item, NsCbc, "Description") ?? string.Empty,
            NombreItem     = Elem(item, NsCbc, "Name") ?? string.Empty,
            CodigoProducto = Elem(item?.Element(NsCac + "SellersItemIdentification"), NsCbc, "ID") ?? string.Empty,
            CodigoUNSPSC   = item?.Element(NsCac + "CommodityClassification")
                                   ?.Element(NsCbc + "ItemClassificationCode")?.Value?.Trim() ?? string.Empty,
        };
    }

    // ── DespatchAdvice ────────────────────────────────────────────────────────
    private static void ParsearGuiaRemision(XElement root, DocumentoXml doc)
    {
        doc.NumeroDocumento = Elem(root, NsCbc, "ID") ?? string.Empty;
        doc.FechaEmision    = Fecha(Elem(root, NsCbc, "IssueDate"));
        doc.TipoDocumento   = Elem(root, NsCbc, "DespatchAdviceTypeCode") ?? "09";

        var dsp = root.Element(NsCac + "DespatchSupplierParty");
        doc.RucEmisor             = Elem(dsp, NsCbc, "CustomerAssignedAccountID") ?? string.Empty;
        doc.NombreComercialEmisor = Elem(dsp?.Element(NsCac + "Party")?.Element(NsCac + "PartyName"),
                                         NsCbc, "Name") ?? string.Empty;
        doc.RazonSocialEmisor     = Elem(dsp?.Element(NsCac + "Party")
                                              ?.Element(NsCac + "PartyLegalEntity"),
                                         NsCbc, "RegistrationName") ?? string.Empty;

        var dlv = root.Element(NsCac + "DeliveryCustomerParty");
        doc.RucReceptor         = Elem(dlv, NsCbc, "CustomerAssignedAccountID") ?? string.Empty;
        doc.RazonSocialReceptor = Elem(dlv?.Element(NsCac + "Party")?.Element(NsCac + "PartyName"),
                                       NsCbc, "Name") ?? string.Empty;

        var shipment = root.Element(NsCac + "Shipment");
        if (shipment is not null)
        {
            doc.ModalidadTraslado = Elem(shipment, NsCbc, "HandlingCode") ?? string.Empty;
            doc.MotivoTraslado    = Elem(shipment, NsCbc, "HandlingInstructions") ?? string.Empty;

            var grossWeight = shipment.Element(NsCbc + "GrossWeightMeasure");
            doc.PesoBruto  = decimal.TryParse(grossWeight?.Value?.Trim(),
                                 System.Globalization.NumberStyles.Any,
                                 System.Globalization.CultureInfo.InvariantCulture, out var pb) ? pb : 0;
            doc.UnidadPeso = grossWeight?.Attribute("unitCode")?.Value ?? string.Empty;

            var stage = shipment.Element(NsCac + "ShipmentStage");
            if (stage is not null)
            {
                doc.ModoTransporte      = Elem(stage, NsCbc, "TransportModeCode") ?? string.Empty;
                var transit             = stage.Element(NsCac + "TransitPeriod");
                doc.FechaInicioTraslado = Fecha(Elem(transit, NsCbc, "StartDate"));
                doc.FechaFinTraslado    = Fecha(Elem(transit, NsCbc, "EndDate"));

                var carrier = stage.Element(NsCac + "CarrierParty");
                doc.RucTransportista      = Elem(carrier?.Element(NsCac + "PartyIdentification"),
                                                 NsCbc, "ID") ?? string.Empty;
                doc.RazonSocTransportista = Elem(carrier?.Element(NsCac + "PartyLegalEntity"),
                                                 NsCbc, "RegistrationName") ?? string.Empty;
            }

            var delivery = shipment.Element(NsCac + "Delivery");
            var despacho = delivery?.Element(NsCac + "Despatch");
            var despAddr = despacho?.Element(NsCac + "DespatchAddress");
            doc.UbigeoOrigen = Elem(despAddr, NsCbc, "ID") ?? string.Empty;
            doc.DirOrigen    = Elem(despAddr?.Element(NsCac + "AddressLine"), NsCbc, "Line") ?? string.Empty;

            var dlvAddr = delivery?.Element(NsCac + "DeliveryAddress");
            doc.UbigeoDestino = Elem(dlvAddr, NsCbc, "ID") ?? string.Empty;
            doc.DirDestino    = Elem(dlvAddr?.Element(NsCac + "AddressLine"), NsCbc, "Line") ?? string.Empty;
        }

        foreach (var prop in root.Descendants(NsFac + "AdditionalPrintedProperty"))
        {
            var propIdLower = Elem(prop, NsCbc, "ID")?.Trim()?.ToLowerInvariant();
            var propVal     = Elem(prop, NsCbc, "Value") ?? string.Empty;
            switch (propIdLower)
            {
                case "nombreconductor":   doc.NombreConductor   = propVal; break;
                case "licenciaconductor": doc.LicenciaConductor = propVal; break;
                case "placa":             doc.PlacaVehiculo     = propVal; break;
                case "marca":             doc.MarcaVehiculo     = propVal; break;
                case "numerodocumento":   doc.NroDocConductor   = propVal; break;
                case "orden de compra":   doc.NumeroPedido      = propVal; break;
            }
        }

        foreach (var linea in root.Elements(NsCac + "DespatchLine"))
            doc.Lineas.Add(ParsearLineaGuia(linea));
    }

    private static LineaDocumento ParsearLineaGuia(XElement linea)
    {
        var item   = linea.Element(NsCac + "Item");
        var qty    = linea.Element(NsCbc + "DeliveredQuantity");
        var loteId = item?.Element(NsCac + "ItemInstance")
                         ?.Element(NsCac + "LotIdentification");

        return new LineaDocumento
        {
            NumeroLinea    = int.TryParse(Elem(linea, NsCbc, "ID"), out var nl) ? nl : 0,
            Cantidad       = decimal.TryParse(qty?.Value?.Trim(),
                                System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out var q) ? q : 0,
            UnidadMedida   = qty?.Attribute("unitCode")?.Value ?? string.Empty,
            Descripcion    = Elem(item, NsCbc, "Description") ?? string.Empty,
            NombreItem     = Elem(item, NsCbc, "Name") ?? string.Empty,
            CodigoProducto = Elem(item?.Element(NsCac + "SellersItemIdentification"), NsCbc, "ID") ?? string.Empty,
            Lote           = Elem(loteId, NsCbc, "LotNumberID") ?? string.Empty,
            FechaVencLote  = Fecha(Elem(loteId, NsCbc, "ExpiryDate")),
        };
    }

    // ── ApplicationResponse (CDR / Constancia de Recepción) ──────────────────

    /// <summary>
    /// Parsea una Constancia de Recepción (CDR) emitida por SUNAT o un OSE.
    /// Extrae los datos del comprobante referenciado para construir el nombre de
    /// archivo con el sufijo <c>_CDR</c> y registrar el CDR vinculado a ese documento.
    /// </summary>
    private static void ParsearApplicationResponse(XElement root, DocumentoXml doc)
    {
        doc.FechaEmision = Fecha(Elem(root, NsCbc, "IssueDate"));
        doc.HoraEmision  = Elem(root, NsCbc, "IssueTime") ?? string.Empty;

        // ReceiverParty = empresa que recibe el CDR = emisor del comprobante original.
        var receiver = root.Element(NsCac + "ReceiverParty");
        doc.RucEmisor         = Elem(receiver?.Element(NsCac + "PartyIdentification"), NsCbc, "ID") ?? string.Empty;
        doc.RazonSocialEmisor = Elem(receiver?.Element(NsCac + "PartyLegalEntity"),    NsCbc, "RegistrationName")
                             ?? Elem(receiver?.Element(NsCac + "PartyName"),            NsCbc, "Name")
                             ?? string.Empty;

        // Fallback: algunos OSEs no incluyen el RUC en ReceiverParty; se extrae del nombre del archivo.
        // Patrón habitual: R-{RUC}-{TIPO}-{SERIE}-{CORRELATIVO}.xml
        if (string.IsNullOrWhiteSpace(doc.RucEmisor) && !string.IsNullOrWhiteSpace(doc.NombreArchivo))
        {
            var partesFn = Path.GetFileNameWithoutExtension(doc.NombreArchivo).Split('-');
            if (partesFn.Length >= 5
                && partesFn[0].Equals("R", StringComparison.OrdinalIgnoreCase)
                && partesFn[1].Length == 11
                && long.TryParse(partesFn[1], out _))
                doc.RucEmisor = partesFn[1];
        }

        // SenderParty = OSE o SUNAT que emite el CDR.
        var sender = root.Element(NsCac + "SenderParty");
        doc.RucReceptor         = Elem(sender?.Element(NsCac + "PartyIdentification"), NsCbc, "ID") ?? string.Empty;
        doc.RazonSocialReceptor = Elem(sender?.Element(NsCac + "PartyLegalEntity"),    NsCbc, "RegistrationName")
                               ?? Elem(sender?.Element(NsCac + "PartyName"),            NsCbc, "Name")
                               ?? string.Empty;

        // ── Comprobante original referenciado ─────────────────────────────────
        var docResponse  = root.Element(NsCac + "DocumentResponse");
        var docRef       = docResponse?.Element(NsCac + "DocumentReference");
        var referencedId = Elem(docRef, NsCbc, "ID") ?? string.Empty;          // ej. "FF05-74833"
        doc.TipoDocumento = Elem(docRef, NsCbc, "DocumentTypeCode") ?? string.Empty;  // ej. "01"

        // Separar serie y correlativo del comprobante referenciado (para nombrado de archivo).
        var partes      = referencedId.Split('-', 2);
        doc.Serie       = partes.Length >= 1 ? partes[0] : string.Empty;
        doc.Correlativo = partes.Length >= 2 ? partes[1] : string.Empty;

        // NumeroDocumento único en BD: el prefijo "CDR-" evita colisión con el comprobante original.
        doc.NumeroDocumento = string.IsNullOrWhiteSpace(referencedId)
            ? (Elem(root, NsCbc, "ID") ?? doc.NombreArchivo)
            : $"CDR-{referencedId}";

        // ── Código de respuesta (para diagnóstico) ────────────────────────────
        var response     = docResponse?.Element(NsCac + "Response");
        var responseCode = Elem(response, NsCbc, "ResponseCode") ?? string.Empty;
        var description  = Elem(response, NsCbc, "Description")  ?? string.Empty;
        doc.NumeroDocRef = string.IsNullOrWhiteSpace(description)
            ? responseCode
            : $"{responseCode}: {description}".TrimStart(':').Trim();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static XElement? BuscarExtensionSunat(XElement root)
    {
        XNamespace nsExtUbl = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";
        var ublExtensions = root.Element(nsExtUbl + "UBLExtensions");
        if (ublExtensions is null) return null;

        var extensionContents = ublExtensions
            .Elements(nsExtUbl + "UBLExtension")
            .Select(e => e.Element(nsExtUbl + "ExtensionContent"))
            .Where(ec => ec is not null)
            .SelectMany(ec => ec!.Elements());

        return extensionContents
            .FirstOrDefault(e => e.Name.NamespaceName == NsSac.NamespaceName);
    }

    private static string? Elem(XElement? parent, XNamespace ns, string localName)
        => parent?.Element(ns + localName)?.Value?.Trim();

    private static decimal Dec(XElement? parent, XNamespace ns, string localName)
        => decimal.TryParse(Elem(parent, ns, localName),
               System.Globalization.NumberStyles.Any,
               System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;

    private static DateTime? Fecha(string? s)
        => DateTime.TryParse(s, out var d) ? d : null;

    private static string[] SepararNumeroDoc(string id)
    {
        var parts = id.Split('-', 2);
        return parts.Length == 2 ? parts : [id, ""];
    }
}
