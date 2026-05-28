using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FabricaHilos.Sire.Constants;
using FabricaHilos.Sire.Interfaces;
using FabricaHilos.Sire.Models;
using FabricaHilos.Sire.Options;
using Microsoft.Extensions.Options;

namespace FabricaHilos.Sire.Services;

public sealed class SireVentasService : ISireVentasService
{
    private readonly HttpClient _httpClient;
    private readonly ISireAuthService _authService;
    private readonly SireOptions _options;

    public SireVentasService(HttpClient httpClient, ISireAuthService authService, IOptions<SireOptions> options)
    {
        _httpClient = httpClient;
        _authService = authService;
        _options = options.Value;
    }

    public Task<IReadOnlyList<PropuestaDto>> ObtenerPeriodosAsync(CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<PropuestaDto>>(HttpMethod.Get, SireEndpoints.RviePeriodos, null, cancellationToken);

    public Task<IReadOnlyList<RegistroVenta>> ObtenerPropuestaAsync(string periodo, CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<RegistroVenta>>(HttpMethod.Get, SireEndpoints.RviePropuesta(periodo), null, cancellationToken);

    public Task<TicketEstado> AceptarPropuestaAsync(string periodo, CancellationToken cancellationToken = default)
        => SendAsync<TicketEstado>(HttpMethod.Post, SireEndpoints.RvieAceptar(periodo), new { }, cancellationToken);

    public Task<TicketEstado> ReemplazarPropuestaAsync(string periodo, Stream contenidoArchivo, string nombreArchivo, CancellationToken cancellationToken = default)
        => SendMultipartAsync(SireEndpoints.RvieReemplazo(periodo), contenidoArchivo, nombreArchivo, cancellationToken);

    public Task<TicketEstado> CerrarPeriodoAsync(string periodo, CancellationToken cancellationToken = default)
        => SendAsync<TicketEstado>(HttpMethod.Post, SireEndpoints.RvieCierre(periodo), new { }, cancellationToken);

    public async Task<ConstanciaCierre> DescargarConstanciaAsync(string periodo, CancellationToken cancellationToken = default)
    {
        await SetBearerAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildUrl(SireEndpoints.RvieConstancia(periodo)));
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new SireApiException($"Error SUNAT RVIE constancia: {(int)response.StatusCode} - {error}", response.StatusCode);
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return new ConstanciaCierre
        {
            NombreArchivo = $"RVIE_Constancia_{periodo}.pdf",
            ContentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream",
            Contenido = bytes
        };
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? payload, CancellationToken cancellationToken)
    {
        await SetBearerAsync(cancellationToken);

        using var request = new HttpRequestMessage(method, BuildUrl(path));
        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new SireApiException($"Error SUNAT RVIE: {(int)response.StatusCode} - {content}", response.StatusCode);
        }

        if (typeof(T) == typeof(string))
        {
            return (T)(object)content;
        }

        return JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? throw new SireApiException("Respuesta SUNAT RVIE inválida.");
    }

    private async Task<TicketEstado> SendMultipartAsync(string path, Stream contenidoArchivo, string nombreArchivo, CancellationToken cancellationToken)
    {
        await SetBearerAsync(cancellationToken);
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(contenidoArchivo), "archivo", nombreArchivo);

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUrl(path))
        {
            Content = content
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new SireApiException($"Error SUNAT RVIE reemplazo: {(int)response.StatusCode} - {body}", response.StatusCode);
        }

        return JsonSerializer.Deserialize<TicketEstado>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? new TicketEstado { Estado = "COMPLETADO", Mensaje = "Operación ejecutada" };
    }

    private async Task SetBearerAsync(CancellationToken cancellationToken)
    {
        var token = await _authService.GetTokenAsync(cancellationToken);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(token.TokenType, token.AccessToken);
    }

    private string BuildUrl(string path)
    {
        var baseUrl = _options.ApiBaseUrl.TrimEnd('/');
        return $"{baseUrl}{path}";
    }
}
