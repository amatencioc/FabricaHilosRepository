using System.Net.Http.Headers;
using System.Text.Json;
using FabricaHilos.Models.Facturacion;

namespace FabricaHilos.Services;

public class DocumentExtractorClient
{
    private readonly HttpClient _http;

    public DocumentExtractorClient(HttpClient http) => _http = http;

    public async Task<DocumentoExtraido?> ExtraerAsync(IFormFile archivo)
    {
        using var content = new MultipartFormDataContent();
        using var stream = archivo.OpenReadStream();
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(archivo.ContentType);
        content.Add(fileContent, "archivo", archivo.FileName);

        var response = await _http.PostAsync("api/v1/extractor/extraer", content);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<DocumentoExtraido>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }
}
