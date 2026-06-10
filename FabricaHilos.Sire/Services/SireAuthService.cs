using System.Net.Http.Headers;
using System.Text.Json;
using FabricaHilos.Sire.Interfaces;
using FabricaHilos.Sire.Models;
using FabricaHilos.Sire.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FabricaHilos.Sire.Services;

public sealed class SireAuthService : ISireAuthService
{
    private const string CacheKey = "SIRE_AUTH_TOKEN";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<SireAuthService> _logger;
    private readonly string _authUrl;
    private readonly string _username;
    private readonly string _password;
    private readonly string _scope;
    private readonly string _clientId;
    private readonly string _clientSecret;

    public SireAuthService(
        HttpClient httpClient,
        IOptions<SireOptions> options,
        IMemoryCache memoryCache,
        ILogger<SireAuthService> logger)
    {
        var o = options.Value;
        _httpClient   = httpClient;
        _memoryCache  = memoryCache;
        _logger       = logger;
        _authUrl      = $"{o.AuthUrl.TrimEnd('/')}/{o.ClientId}/oauth2/token/";
        _username     = $"{o.Ruc}{o.UsuarioSol}";  // SIN espacio: RUC+Usuario (formato SUNAT confirmado)
        _password     = o.ClaveSol?.Trim() ?? "";  // Eliminar espacios en blanco
        _scope        = o.Scope;
        _clientId     = o.ClientId;
        _clientSecret = o.ClientSecret?.Trim() ?? "";

        _logger.LogDebug("[SIRE-AUTH] Configuración cargada: AuthUrl={AuthUrl} | Username={Username} | Scope={Scope} | ClientId={ClientId}",
            _authUrl, _username, _scope, _clientId);

        // Validación de credenciales en construcción
        if (string.IsNullOrWhiteSpace(_clientId) || string.IsNullOrWhiteSpace(_clientSecret) 
            || string.IsNullOrWhiteSpace(_username) || string.IsNullOrWhiteSpace(_password))
        {
            _logger.LogWarning("[SIRE-AUTH] ⚠️ ADVERTENCIA: Credenciales incompletas detectadas en appsettings.json");
        }
    }

    public async Task<AuthToken> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_memoryCache.TryGetValue<AuthToken>(CacheKey, out var cached)
            && cached is not null
            && cached.ExpiraEnUtc > DateTime.UtcNow.AddMinutes(5))
        {
            var minutosRestantes = (cached.ExpiraEnUtc - DateTime.UtcNow).TotalMinutes;
            _logger.LogDebug("[SIRE-AUTH] Token en caché válido, expira en {Minutos:F1} minutos ({Expira:u})", 
                minutosRestantes, cached.ExpiraEnUtc);

            // ⚠️ MONITOREO: Advertir si el token expira pronto (última renovación antes de expirar)
            if (minutosRestantes < 10)
            {
                _logger.LogWarning("[SIRE-AUTH] ⚠️ Token próximo a expirar en {Minutos:F1} minutos. Renovación inminente en próxima petición.",
                    minutosRestantes);
            }

            return cached;
        }

        _logger.LogInformation("[SIRE-AUTH] Solicitando nuevo token a SUNAT (caché vacío o expirado)...");
        _logger.LogDebug("[SIRE-AUTH] POST {Url}", _authUrl);
        _logger.LogDebug("[SIRE-AUTH] Parámetros: grant_type=password | scope={Scope} | client_id={ClientId} | username={Username} | password=*** ({PwdLen} chars)",
            _scope, _clientId, _username, _password?.Length ?? 0);

        var form = new Dictionary<string, string>
        {
            ["grant_type"]    = "password",
            ["scope"]         = _scope,
            ["client_id"]     = _clientId,
            ["client_secret"] = _clientSecret,
            ["username"]      = _username,
            ["password"]      = _password
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, _authUrl)
        {
            Content = new FormUrlEncodedContent(form)
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogDebug("[SIRE-AUTH] Respuesta HTTP {StatusCode}: {Body}", (int)response.StatusCode, content);

        if (!response.IsSuccessStatusCode)
        {
            // ❌ MONITOREO: Clasificar errores comunes para facilitar diagnóstico
            var statusCode = (int)response.StatusCode;
            var errorMsg = content.Length > 200 ? content.Substring(0, 200) + "..." : content;

            if (statusCode == 400)
            {
                _logger.LogError("[SIRE-AUTH] ❌ ERROR 400 Bad Request: Credenciales rechazadas por SUNAT. " +
                    "Verificar RUC ({Ruc}), UsuarioSOL ({Usuario}), y ClaveSol en appsettings.json. " +
                    "Body: {Body}", _username.Substring(0, Math.Min(11, _username.Length)), _username.Length > 11 ? _username.Substring(11) : "?", errorMsg);
            }
            else if (statusCode == 401)
            {
                _logger.LogError("[SIRE-AUTH] ❌ ERROR 401 Unauthorized: ClientId/ClientSecret inválidos o aplicación deshabilitada en SUNAT. " +
                    "Verificar ClientId ({ClientId}) en portal SUNAT → Aplicaciones registradas. " +
                    "Body: {Body}", _clientId, errorMsg);
            }
            else if (statusCode == 403)
            {
                _logger.LogError("[SIRE-AUTH] ❌ ERROR 403 Forbidden: Scope ({Scope}) no autorizado para esta aplicación. " +
                    "Verificar permisos en portal SUNAT. Body: {Body}", _scope, errorMsg);
            }
            else if (statusCode >= 500)
            {
                _logger.LogError("[SIRE-AUTH] ❌ ERROR {Status} Server Error: SUNAT no disponible temporalmente. " +
                    "Reintentar en unos minutos. Body: {Body}", statusCode, errorMsg);
            }
            else
            {
                _logger.LogError("[SIRE-AUTH] ❌ ERROR {Status}: {Body}", statusCode, errorMsg);
            }

            throw new SireApiException($"Error de autenticación SUNAT: {statusCode} - {content}", response.StatusCode);
        }

        var token = JsonSerializer.Deserialize<AuthToken>(content, JsonOptions)
               ?? throw new SireApiException("No se pudo deserializar el token de SUNAT.");

        token.ExpiraEnUtc = DateTime.UtcNow.AddSeconds(token.ExpiresIn);
        _memoryCache.Set(CacheKey, token, token.ExpiraEnUtc);

        _logger.LogInformation("[SIRE-AUTH] ✅ Token obtenido correctamente. Tipo={Tipo} | ExpiresIn={Segundos}s | Expira={Expira:u}",
            token.TokenType, token.ExpiresIn, token.ExpiraEnUtc);

        return token;
    }
}
