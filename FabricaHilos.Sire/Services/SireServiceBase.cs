using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FabricaHilos.Sire.Interfaces;
using FabricaHilos.Sire.Models;
using FabricaHilos.Sire.Options;
using Microsoft.Extensions.Options;

namespace FabricaHilos.Sire.Services;

public abstract class SireServiceBase
{
    protected static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly ISireAuthService _authService;
    private readonly string _apiBaseUrl;

    protected SireServiceBase(HttpClient httpClient, ISireAuthService authService, IOptions<SireOptions> options)
    {
        _httpClient = httpClient;
        _authService = authService;
        _apiBaseUrl = options.Value.ApiBaseUrl.TrimEnd('/');
    }

    protected async Task<T> SendAsync<T>(HttpMethod method, string path, object? payload, CancellationToken cancellationToken)
    {
        var url = BuildUrl(path);
        using var request = new HttpRequestMessage(method, url);
        await ApplyBearerAsync(request, cancellationToken);

        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new SireApiException(
                $"Error SUNAT {LibroNombre}: {(int)response.StatusCode} [{method} {url}] - {content}",
                response.StatusCode);
        }

        if (typeof(T) == typeof(string))
        {
            return (T)(object)content;
        }

        return JsonSerializer.Deserialize<T>(content, JsonOptions)
               ?? throw new SireApiException($"Respuesta SUNAT {LibroNombre} inválida.");
    }

    protected async Task<TicketEstado> SendMultipartAsync(string path, Stream contenidoArchivo, string nombreArchivo, CancellationToken cancellationToken)
    {
        using var formContent = new MultipartFormDataContent();
        formContent.Add(new StreamContent(contenidoArchivo), "archivo", nombreArchivo);

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUrl(path)) { Content = formContent };
        await ApplyBearerAsync(request, cancellationToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new SireApiException($"Error SUNAT {LibroNombre} reemplazo: {(int)response.StatusCode} - {body}", response.StatusCode);
        }

        return JsonSerializer.Deserialize<TicketEstado>(body, JsonOptions)
               ?? new TicketEstado { Estado = "COMPLETADO", Mensaje = "Operación ejecutada" };
    }

    protected async Task<ConstanciaCierre> DescargarConstanciaBaseAsync(string path, string nombreArchivo, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildUrl(path));
        await ApplyBearerAsync(request, cancellationToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new SireApiException($"Error SUNAT {LibroNombre} constancia: {(int)response.StatusCode} - {error}", response.StatusCode);
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return new ConstanciaCierre
        {
            NombreArchivo = nombreArchivo,
            ContentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream",
            Contenido = bytes
        };
    }

    protected string BuildUrl(string path) => $"{_apiBaseUrl}{path}";

    private async Task ApplyBearerAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        AuthToken token;
        try
        {
            token = await _authService.GetTokenAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not SireApiException)
        {
            // SUNAT inaccesible (red/timeout): se propaga como SireApiException para que
            // las acciones que ya manejan este tipo puedan degradar a modo local/offline
            // (propuestas ya descargadas, reportes en caché, etc.) en vez de fallar con 500.
            throw new SireApiException($"No se pudo autenticar con SUNAT ({LibroNombre}): {ex.Message}", statusCode: null, innerException: ex);
        }
        // IMPORTANTE: Siempre usar "Bearer" como tipo, incluso si SUNAT devuelve "JWT" en token_type
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
    }

    /// <summary>Nombre del libro SUNAT para mensajes de error (ej. "RVIE" o "RCE").</summary>
    protected abstract string LibroNombre { get; }
}
