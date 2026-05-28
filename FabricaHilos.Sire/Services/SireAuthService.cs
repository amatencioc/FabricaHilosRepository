using System.Net.Http.Headers;
using System.Text.Json;
using FabricaHilos.Sire.Interfaces;
using FabricaHilos.Sire.Models;
using FabricaHilos.Sire.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace FabricaHilos.Sire.Services;

public sealed class SireAuthService : ISireAuthService
{
    private const string CacheKey = "SIRE_AUTH_TOKEN";

    private readonly HttpClient _httpClient;
    private readonly SireOptions _options;
    private readonly IMemoryCache _memoryCache;

    public SireAuthService(HttpClient httpClient, IOptions<SireOptions> options, IMemoryCache memoryCache)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _memoryCache = memoryCache;
    }

    public async Task<AuthToken> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_memoryCache.TryGetValue<AuthToken>(CacheKey, out var cached)
            && cached is not null
            && cached.ExpiraEnUtc > DateTime.UtcNow.AddMinutes(5))
        {
            return cached;
        }

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["scope"] = _options.Scope,
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["username"] = $"{_options.Ruc}{_options.UsuarioSol}",
            ["password"] = _options.ClaveSol
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildAuthUrl())
        {
            Content = new FormUrlEncodedContent(form)
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new SireApiException($"Error de autenticación SUNAT: {(int)response.StatusCode} - {content}", response.StatusCode);
        }

        var token = JsonSerializer.Deserialize<AuthToken>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new SireApiException("No se pudo deserializar el token de SUNAT.");

        token.ExpiraEnUtc = DateTime.UtcNow.AddSeconds(token.ExpiresIn);
        _memoryCache.Set(CacheKey, token, token.ExpiraEnUtc);
        return token;
    }

    private string BuildAuthUrl()
    {
        var baseUrl = _options.AuthUrl.TrimEnd('/');
        return $"{baseUrl}/{_options.ClientId}/oauth2/token/";
    }
}
