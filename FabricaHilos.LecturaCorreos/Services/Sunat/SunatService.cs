namespace FabricaHilos.LecturaCorreos.Services.Sunat;

using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using FabricaHilos.LecturaCorreos.Config;
using Microsoft.Extensions.Logging;

public class SunatService : ISunatService
{
    private static readonly TimeSpan TimeoutSoap = TimeSpan.FromSeconds(30);

    private readonly HttpClient            _httpClient;
    private readonly ILogger<SunatService> _logger;

    public SunatService(HttpClient httpClient, ILogger<SunatService> logger)
    {
        _httpClient = httpClient;
        _logger     = logger;
    }

    public async Task<RespuestaCdrSunat> ConsultarCdrAsync(
        string ruc, string tipoComprobante, string serie, int correlativo,
        EmpresaOptions empresa, CancellationToken ct = default)
    {
        var sunat = empresa.Sunat
            ?? throw new InvalidOperationException(
                $"La empresa '{empresa.Nombre}' ({empresa.Ruc}) no tiene configuración SUNAT.");

        var soapEnvelope = ConstruirSoapEnvelope(
            empresa.Ruc, sunat.UsuarioSol, sunat.ClaveSol,
            ruc, tipoComprobante, serie, correlativo);

        var endpoint = string.IsNullOrWhiteSpace(sunat.EndpointConsultaCdr)
            ? "https://e-factura.sunat.gob.pe/ol-it-wsconscpegem/billConsultService"
            : sunat.EndpointConsultaCdr;

        try
        {
            var contenido = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
            contenido.Headers.ContentType = new MediaTypeHeaderValue("text/xml") { CharSet = "utf-8" };
            contenido.Headers.Add("SOAPAction", "\"\"");

            _logger.LogInformation(
                "Consultando CDR para {Ruc}/{Tipo}/{Serie}/{Correlativo} → {Endpoint}",
                ruc, tipoComprobante, serie, correlativo, endpoint);

            // Timeout propio de 30 s para no bloquear el ciclo si SUNAT no responde.
            using var soapCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            soapCts.CancelAfter(TimeoutSoap);

            var respuestaHttp   = await _httpClient.PostAsync(endpoint, contenido, soapCts.Token);
            var cuerpoRespuesta = await respuestaHttp.Content.ReadAsStringAsync(soapCts.Token);

            if (!respuestaHttp.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "HTTP {StatusCode} para {Ruc}/{Serie}/{Correlativo}. Respuesta: {Body}",
                    respuestaHttp.StatusCode, ruc, serie, correlativo, cuerpoRespuesta);

                return new RespuestaCdrSunat
                {
                    Exitoso      = false,
                    ErrorDetalle = $"HTTP {(int)respuestaHttp.StatusCode}: {cuerpoRespuesta}"
                };
            }

            _logger.LogDebug("Respuesta XML recibida para {Ruc}/{Serie}/{Correlativo}.", ruc, serie, correlativo);

            return ParsarRespuestaSoap(cuerpoRespuesta);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Timeout ({Seg}s) en consulta CDR para {Ruc}/{Serie}/{Correlativo}.",
                (int)TimeoutSoap.TotalSeconds, ruc, serie, correlativo);
            return new RespuestaCdrSunat
            {
                Exitoso      = false,
                ErrorDetalle = $"Timeout de {(int)TimeoutSoap.TotalSeconds}s en llamada SOAP a SUNAT."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error al llamar al servicio SOAP para {Ruc}/{Serie}/{Correlativo}.",
                ruc, serie, correlativo);

            return new RespuestaCdrSunat { Exitoso = false, ErrorDetalle = ex.Message };
        }
    }

    // ── Construcción del envelope SOAP ───────────────────────────────────────

    private string ConstruirSoapEnvelope(
        string authRuc, string usuarioSol, string claveSol,
        string ruc, string tipoComprobante, string serie, int correlativo)
    {
        var usuarioCompleto = $"{authRuc}{usuarioSol}";

        return $@"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/""
                  xmlns:ser=""http://service.sunat.gob.pe""
                  xmlns:wsse=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd"">
  <soapenv:Header>
    <wsse:Security>
      <wsse:UsernameToken>
        <wsse:Username>{EscaparXml(usuarioCompleto)}</wsse:Username>
        <wsse:Password>{EscaparXml(claveSol)}</wsse:Password>
      </wsse:UsernameToken>
    </wsse:Security>
  </soapenv:Header>
  <soapenv:Body>
    <ser:getStatusCdr>
      <rucComprobante>{EscaparXml(ruc)}</rucComprobante>
      <tipoComprobante>{EscaparXml(tipoComprobante)}</tipoComprobante>
      <serieComprobante>{EscaparXml(serie)}</serieComprobante>
      <numeroComprobante>{correlativo}</numeroComprobante>
    </ser:getStatusCdr>
  </soapenv:Body>
</soapenv:Envelope>";
    }

    private static string EscaparXml(string valor) =>
        valor
            .Replace("&",  "&amp;")
            .Replace("<",  "&lt;")
            .Replace(">",  "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'",  "&apos;");

    // ── Parseo de la respuesta SOAP ──────────────────────────────────────────

    private RespuestaCdrSunat ParsarRespuestaSoap(string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            XNamespace soapNs = "http://schemas.xmlsoap.org/soap/envelope/";

            var fault = doc.Descendants(soapNs + "Fault").FirstOrDefault();
            if (fault is not null)
            {
                var faultString = fault.Element("faultstring")?.Value ?? "Error desconocido en SOAP";
                _logger.LogWarning("SOAP Fault de SUNAT: {Fault}", faultString);
                return new RespuestaCdrSunat { Exitoso = false, ErrorDetalle = faultString };
            }

            var codigo  = doc.Descendants("statusCode").FirstOrDefault()?.Value
                       ?? doc.Descendants("codRespuesta").FirstOrDefault()?.Value
                       ?? string.Empty;

            var mensaje = doc.Descendants("statusMessage").FirstOrDefault()?.Value
                       ?? doc.Descendants("desMensaje").FirstOrDefault()?.Value
                       ?? string.Empty;

            var contenidoBase64 = doc.Descendants("content").FirstOrDefault()?.Value
                               ?? doc.Descendants("arcCdr").FirstOrDefault()?.Value;

            byte[]? cdrZip = null;
            if (!string.IsNullOrWhiteSpace(contenidoBase64))
            {
                try   { cdrZip = Convert.FromBase64String(contenidoBase64); }
                catch (Exception ex) { _logger.LogWarning(ex, "No se pudo decodificar el CDR en Base64."); }
            }

            return new RespuestaCdrSunat
            {
                Exitoso          = true,
                CodigoRespuesta  = codigo,
                MensajeRespuesta = mensaje,
                CdrZip           = cdrZip
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al parsear la respuesta XML de SUNAT.");
            return new RespuestaCdrSunat
            {
                Exitoso      = false,
                ErrorDetalle = $"Error al parsear respuesta XML: {ex.Message}"
            };
        }
    }
}
