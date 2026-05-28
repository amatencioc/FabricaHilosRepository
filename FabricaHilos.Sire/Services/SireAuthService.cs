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
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _memoryCache;
    private readonly string _authUrl;
    private readonly string _username;
    private readonly string _password;
    private readonly string _scope;
    private readonly string _clientId;
    private readonly string _clientSecret;

    public SireAuthService(HttpClient httpClient, IOptions<SireOptions> options, IMemoryCache memoryCache)
    {
        var o = options.Value;
        _httpClient = httpClient;
        _memoryCache = memoryCache;
        _authUrl = $"{o.AuthUrl.TrimEnd('/')}/{o.ClientId}/oauth2/token/";
        _username = $"{o.Ruc}{o.UsuarioSol}";
        _password = o.ClaveSol;
        _scope = o.Scope;
        _clientId = o.ClientId;
        _clientSecret = o.ClientSecret;
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
            ["scope"] = _scope,
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
            ["username"] = _username,
            ["password"] = _password
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, _authUrl)
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

        var token = JsonSerializer.Deserialize<AuthToken>(content, JsonOptions)
               ?? throw new SireApiException("No se pudo deserializar el token de SUNAT.");

        token.ExpiraEnUtc = DateTime.UtcNow.AddSeconds(token.ExpiresIn);
        _memoryCache.Set(CacheKey, token, token.ExpiraEnUtc);
        return token;
    }

    }
