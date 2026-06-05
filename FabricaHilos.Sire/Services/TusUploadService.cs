using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FabricaHilos.Sire.Interfaces;
using FabricaHilos.Sire.Models;
using FabricaHilos.Sire.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FabricaHilos.Sire.Services;

/// <summary>
/// Implementa la subida de archivos a SUNAT SIRE usando el protocolo TUS.io 1.0.0.
///
/// Flujo TUS:
///   1. POST (creación)  → el servidor responde con Upload-Location (URL del recurso)
///   2. PATCH (subida)   → se envían chunks binarios hasta que Offset == FileSize
///   3. El servidor devuelve el numTicket al completar la subida
/// </summary>
public sealed class TusUploadService : ITusUploadService
{
    private const string TusVersion = "1.0.0";
    private const string TusUploadPath = "/libros/rvierce/receptorpropuesta/web/propuesta/upload";

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly ISireAuthService _authService;
    private readonly SireOptions _options;
    private readonly ILogger<TusUploadService> _logger;

    public TusUploadService(
        HttpClient httpClient,
        ISireAuthService authService,
        IOptions<SireOptions> options,
        ILogger<TusUploadService> logger)
    {
        _httpClient = httpClient;
        _authService = authService;
        _options = options.Value;
        _logger = logger;
    }

    // ──────────────────────────────────────────────
    // Métodos de conveniencia
    // ──────────────────────────────────────────────

    public Task<TusUploadResult> ReemplazarPropuestaRceAsync(
        Stream archivoZip, string periodo, string nombreArchivo,
        CancellationToken cancellationToken = default)
    {
        return SubirArchivoAsync(archivoZip, new TusUploadOptions
        {
            NumRuc                  = _options.Ruc,
            PerTributario           = periodo,
            CodProceso              = "61",
            CodLibro                = "080000",
            NombreArchivoImportacion = nombreArchivo
        }, cancellationToken);
    }

    public Task<TusUploadResult> ReemplazarPropuestaRvieAsync(
        Stream archivoZip, string periodo, string nombreArchivo,
        CancellationToken cancellationToken = default)
    {
        return SubirArchivoAsync(archivoZip, new TusUploadOptions
        {
            NumRuc                  = _options.Ruc,
            PerTributario           = periodo,
            CodProceso              = "61",
            CodLibro                = "140100",
            NombreArchivoImportacion = nombreArchivo
        }, cancellationToken);
    }

    // ──────────────────────────────────────────────
    // Implementación principal del protocolo TUS
    // ──────────────────────────────────────────────

    public async Task<TusUploadResult> SubirArchivoAsync(
        Stream archivoStream,
        TusUploadOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(archivoStream);
        ArgumentNullException.ThrowIfNull(options);

        var fileSize = archivoStream.Length;
        _logger.LogInformation("TUS SUNAT: iniciando subida. Proceso={CodProceso} Periodo={Periodo} Archivo={Archivo} Tamaño={Size}",
            options.CodProceso, options.PerTributario, options.NombreArchivoImportacion, fileSize);

        try
        {
            // PASO 1 — Creación del recurso TUS
            var uploadUrl = await CrearRecursoTusAsync(fileSize, options, cancellationToken);
            _logger.LogDebug("TUS SUNAT: recurso creado → {Url}", uploadUrl);

            // PASO 2 — Subida por chunks
            var bytesSubidos = await SubirChunksAsync(uploadUrl, archivoStream, options.ChunkSizeBytes, cancellationToken);
            _logger.LogInformation("TUS SUNAT: subida completada. Bytes={Bytes}", bytesSubidos);

            // PASO 3 — Leer ticket de la respuesta del último PATCH
            var ticket = await ObtenerTicketAsync(uploadUrl, cancellationToken);

            return TusUploadResult.Ok(ticket, bytesSubidos);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "TUS SUNAT: error durante la subida");
            return TusUploadResult.Error(ex.Message);
        }
    }

    // ──────────────────────────────────────────────
    // PASO 1: POST de creación → retorna Upload-Location
    // ──────────────────────────────────────────────
    private async Task<string> CrearRecursoTusAsync(
        long fileSize,
        TusUploadOptions options,
        CancellationToken cancellationToken)
    {
        var createUrl = BuildTusUrl();
        using var request = new HttpRequestMessage(HttpMethod.Post, createUrl);

        await ApplyBearerAsync(request, cancellationToken);

        request.Headers.Add("Tus-Resumable", TusVersion);
        request.Headers.Add("Upload-Length", fileSize.ToString());
        request.Headers.Add("Upload-Metadata", BuildTusMetadata(options));
        request.Content = new ByteArrayContent(Array.Empty<byte>());
        request.Content.Headers.ContentLength = 0;
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new SireApiException(
                $"TUS SUNAT: error al crear recurso. HTTP {(int)response.StatusCode} → {body}",
                response.StatusCode);
        }

        // SUNAT devuelve la URL del recurso en el header Upload-Location (o Location)
        var location = response.Headers.Location?.ToString()
            ?? (response.Headers.TryGetValues("Upload-Location", out var vals) ? vals.FirstOrDefault() : null);

        if (string.IsNullOrWhiteSpace(location))
        {
            throw new SireApiException("TUS SUNAT: el servidor no retornó Upload-Location en la creación del recurso.");
        }

        return location;
    }

    // ──────────────────────────────────────────────
    // PASO 2: PATCHes con chunks binarios
    // ──────────────────────────────────────────────
    private async Task<long> SubirChunksAsync(
        string uploadUrl,
        Stream archivoStream,
        int chunkSize,
        CancellationToken cancellationToken)
    {
        archivoStream.Seek(0, SeekOrigin.Begin);
        long offset = 0;
        var buffer = new byte[chunkSize];

        while (offset < archivoStream.Length)
        {
            var bytesLeidos = await archivoStream.ReadAsync(buffer, cancellationToken);
            if (bytesLeidos == 0) break;

            var chunk = new ReadOnlyMemory<byte>(buffer, 0, bytesLeidos);
            using var request = new HttpRequestMessage(HttpMethod.Patch, uploadUrl);

            await ApplyBearerAsync(request, cancellationToken);
            request.Headers.Add("Tus-Resumable", TusVersion);
            request.Headers.Add("Upload-Offset", offset.ToString());

            request.Content = new ByteArrayContent(chunk.ToArray());
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/offset+octet-stream");
            request.Content.Headers.ContentLength = bytesLeidos;

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new SireApiException(
                    $"TUS SUNAT: error al enviar chunk en offset {offset}. HTTP {(int)response.StatusCode} → {body}",
                    response.StatusCode);
            }

            // El servidor confirma el offset recibido
            if (response.Headers.TryGetValues("Upload-Offset", out var offsets)
                && long.TryParse(offsets.FirstOrDefault(), out var confirmedOffset))
            {
                offset = confirmedOffset;
            }
            else
            {
                offset += bytesLeidos;
            }

            _logger.LogDebug("TUS SUNAT: chunk enviado. Offset={Offset}/{Total}", offset, archivoStream.Length);
        }

        return offset;
    }

    // ──────────────────────────────────────────────
    // PASO 3: GET al upload URL para recuperar el ticket
    // ──────────────────────────────────────────────
    private async Task<string> ObtenerTicketAsync(string uploadUrl, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uploadUrl);
        await ApplyBearerAsync(request, cancellationToken);
        request.Headers.Add("Tus-Resumable", TusVersion);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("TUS SUNAT: GET post-upload retornó {Status}. Body={Body}", response.StatusCode, body);
            return string.Empty;
        }

        // SUNAT puede devolver el ticket en el body JSON o en un header
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("numTicket", out var ticketProp))
                return ticketProp.GetString() ?? string.Empty;
        }
        catch (JsonException)
        {
            // no es JSON; podría venir en un header
        }

        if (response.Headers.TryGetValues("X-Ticket", out var tickets))
            return tickets.FirstOrDefault() ?? string.Empty;

        return string.Empty;
    }

    // ──────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────

    private string BuildTusUrl()
    {
        var baseUrl = _options.ApiBaseUrl.TrimEnd('/');
        return $"{baseUrl}{TusUploadPath}";
    }

    /// <summary>
    /// Construye el header Upload-Metadata codificando cada valor en Base64.
    /// Formato TUS: "key base64value,key base64value,..."
    /// </summary>
    private static string BuildTusMetadata(TusUploadOptions opts)
    {
        var fields = new Dictionary<string, string>
        {
            ["filename"]               = opts.NombreArchivoImportacion,
            ["filetype"]               = "application/zip",
            ["numRuc"]                 = opts.NumRuc,
            ["perTributario"]          = opts.PerTributario,
            ["codOrigenEnvio"]         = opts.CodOrigenEnvio,
            ["codProceso"]             = opts.CodProceso,
            ["codTipoCorrelativo"]     = opts.CodTipoCorrelativo,
            ["nomArchivoImportacion"]  = opts.NombreArchivoImportacion,
            ["codLibro"]               = opts.CodLibro
        };

        return string.Join(",", fields.Select(kv =>
            $"{kv.Key} {Convert.ToBase64String(Encoding.UTF8.GetBytes(kv.Value))}"));
    }

    private async Task ApplyBearerAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _authService.GetTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue(token.TokenType, token.AccessToken);
    }
}
