using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FabricaHilos.Services.Sire;

/// <summary>
/// Implementación de IConsultaValidezService.
/// Llama a la API oficial de SUNAT "Consulta Integrada de Validez de CPE":
///   Auth : POST https://api-seguridad.sunat.gob.pe/v1/clientesextranet/{clientId}/oauth2/token/
///   API  : POST https://api.sunat.gob.pe/v1/contribuyente/contribuyentes/{ruc}/validarcomprobante
/// Credenciales: appsettings.json → ConsultaValidez.ClientId / ClientSecret
/// El token dura 3600 s — se reutiliza mediante caché en memoria.
/// </summary>
public sealed class ConsultaValidezService : IConsultaValidezService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<ConsultaValidezService> _logger;
    private readonly string _authUrl;
    private readonly string _apiUrl;
    private readonly string _scope;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _rucEmpresa;

    // Caché del token (no se usa IMemoryCache para no añadir dependencia)
    private string? _cachedToken;
    private DateTime _tokenExpiry = DateTime.MinValue;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    public ConsultaValidezService(
        IHttpClientFactory httpFactory,
        IConfiguration configuration,
        ILogger<ConsultaValidezService> logger)
    {
        _httpFactory  = httpFactory;
        _logger       = logger;

        var cfg = configuration.GetSection("ConsultaValidez");
        _authUrl      = cfg["AuthUrl"]      ?? "https://api-seguridad.sunat.gob.pe/v1/clientesextranet";
        _apiUrl       = cfg["ApiUrl"]       ?? "https://api.sunat.gob.pe/v1/contribuyente/contribuyentes";
        _scope        = cfg["Scope"]        ?? "https://api.sunat.gob.pe/v1/contribuyente/contribuyentes";
        _clientId     = cfg["ClientId"]     ?? throw new InvalidOperationException("ConsultaValidez:ClientId no configurado.");
        _clientSecret = cfg["ClientSecret"] ?? throw new InvalidOperationException("ConsultaValidez:ClientSecret no configurado.");
        _rucEmpresa   = cfg["Ruc"]          ?? throw new InvalidOperationException("ConsultaValidez:Ruc no configurado.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    public async Task<ValidezResult?> ValidarAsync(
        string rucEmisor, string tipdoc, string serie, string numero,
        DateTime fechaEmision, decimal monto, CancellationToken ct = default)
    {
        try
        {
            var token = await GetTokenAsync(ct);
            if (token is null) return null;

            // El campo "numero" en el body es entero sin ceros a la izquierda
            if (!int.TryParse(numero.TrimStart('0'), out var numeroInt))
                numeroInt = 0;

            var body = new
            {
                numRuc       = rucEmisor.Trim(),
                codComp      = tipdoc.Trim().PadLeft(2, '0'),
                numeroSerie  = serie.Trim(),
                numero       = numeroInt,
                fechaEmision = fechaEmision.ToString("dd/MM/yyyy"),
                monto        = monto.ToString("F2")
            };

            var http    = _httpFactory.CreateClient("sunat-validez");
            var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_apiUrl.TrimEnd('/')}/{_rucEmpresa}/validarcomprobante");

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json");

            var resp = await http.SendAsync(request, ct);

            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // Token expirado inesperadamente → forzar renovación y reintentar 1 vez
                InvalidarToken();
                token = await GetTokenAsync(ct);
                if (token is null) return null;
                request = new HttpRequestMessage(HttpMethod.Post,
                    $"{_apiUrl.TrimEnd('/')}/{_rucEmpresa}/validarcomprobante");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = new StringContent(
                    JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
                resp = await http.SendAsync(request, ct);
            }

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("[ValidezSUNAT] HTTP {Status} para {Ruc}/{Tipo}/{Serie}/{Num}",
                    (int)resp.StatusCode, rucEmisor, tipdoc, serie, numero);
                return null;
            }

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("success", out var succ) || !succ.GetBoolean())
            {
                _logger.LogWarning("[ValidezSUNAT] success=false para {Ruc}/{Serie}/{Num}: {Json}",
                    rucEmisor, serie, numero, json);
                return null;
            }

            var data        = root.GetProperty("data");
            var estadoCp    = data.TryGetProperty("estadoCp",    out var cp)  ? cp.GetString()  ?? "?" : "?";
            var estadoRuc   = data.TryGetProperty("estadoRuc",   out var ruc) ? ruc.GetString() ?? ""  : "";
            var condDomiRuc = data.TryGetProperty("condDomiRuc", out var dom) ? dom.GetString() ?? ""  : "";

            return new ValidezResult(estadoCp, estadoRuc, condDomiRuc);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ValidezSUNAT] Error validando {Ruc}/{Serie}/{Num}",
                rucEmisor, serie, numero);
            return null;
        }
    }

    public void InvalidarToken()
    {
        _cachedToken = null;
        _tokenExpiry = DateTime.MinValue;
    }

    // ─────────────────────────────────────────────────────────────────────────
    private async Task<string?> GetTokenAsync(CancellationToken ct)
    {
        if (_cachedToken is not null && DateTime.UtcNow < _tokenExpiry)
            return _cachedToken;

        await _tokenLock.WaitAsync(ct);
        try
        {
            // Double-check dentro del lock
            if (_cachedToken is not null && DateTime.UtcNow < _tokenExpiry)
                return _cachedToken;

            var http = _httpFactory.CreateClient("sunat-validez");
            var url  = $"{_authUrl.TrimEnd('/')}/{_clientId}/oauth2/token/";

            var form = new Dictionary<string, string>
            {
                ["grant_type"]    = "client_credentials",
                ["scope"]         = _scope,
                ["client_id"]     = _clientId,
                ["client_secret"] = _clientSecret,
            };

            var resp = await http.PostAsync(url, new FormUrlEncodedContent(form), ct);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogError("[ValidezSUNAT] Error obteniendo token HTTP {Status}: {Body}",
                    (int)resp.StatusCode, body);
                return null;
            }

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            _cachedToken = doc.RootElement.GetProperty("access_token").GetString();
            var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var exp)
                ? exp.GetInt32() : 3600;

            _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 60); // margen de 60 s
            _logger.LogInformation("[ValidezSUNAT] Token obtenido. Expira en {Seg} s.", expiresIn);
            return _cachedToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }
}
