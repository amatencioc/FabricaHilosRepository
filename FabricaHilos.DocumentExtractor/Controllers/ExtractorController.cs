using FabricaHilos.DocumentExtractor.Models;
using FabricaHilos.DocumentExtractor.Services;
using Microsoft.AspNetCore.Mvc;

namespace FabricaHilos.DocumentExtractor.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
[IgnoreAntiforgeryToken]
public class ExtractorController : ControllerBase
{
    private readonly IDocumentExtractorService _service;

    public ExtractorController(IDocumentExtractorService service)
    {
        _service = service;
    }

    /// <summary>
    /// Extrae datos de un archivo PDF o imagen y los retorna como JSON.
    /// Soporta PDFs nativos, PDFs escaneados (OCR), PNG, JPEG, TIFF, BMP y WebP. Límite: 30 MB.
    /// </summary>
    /// <param name="archivo">Archivo a procesar (PDF, PNG, JPEG, TIFF, BMP, WebP). Límite: 30 MB.</param>
    [HttpPost("extraer")]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> Extraer(IFormFile archivo)
    {
        if (archivo == null || archivo.Length == 0)
            return BadRequest(new { error = "No se proporcionó ningún archivo." });

        var tiposMimePermitidos = new[]
        {
            "application/pdf",
            "image/png",
            "image/jpeg",
            "image/tiff",
            "image/bmp",
            "image/webp"
        };
        if (!tiposMimePermitidos.Contains(archivo.ContentType, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { error = $"Tipo de archivo no permitido: {archivo.ContentType}. Solo se aceptan PDF, PNG, JPEG, TIFF, BMP y WebP." });

        if (archivo.Length > 30 * 1024 * 1024)
            return BadRequest(new { error = "El archivo supera el límite de 30 MB." });

        using var stream = archivo.OpenReadStream();
        var resultado = await _service.ExtraerAsync(stream, archivo.ContentType, archivo.FileName);
        return Ok(resultado);
    }

    /// <summary>
    /// Estado del servicio OCR: tessdata path, archivos encontrados y directorio base.
    /// No requiere archivo — útil para verificar la configuración del servidor.
    /// </summary>
    [HttpGet("diagnostico")]
    public IActionResult DiagnosticoGet()
    {
        var tessDataPath = PdfExtractorService.GetTessDataPathForDiagnostics();
        var tessFiles = tessDataPath != null && Directory.Exists(tessDataPath)
            ? Directory.GetFiles(tessDataPath).Select(Path.GetFileName).ToArray()
            : Array.Empty<string>();

        return Ok(new
        {
            tessDataPath,
            tessDataEncontrado = tessDataPath != null,
            tessDataArchivos = tessFiles,
            baseDirectory = AppContext.BaseDirectory,
            workingDirectory = Directory.GetCurrentDirectory()
        });
    }

    /// <summary>
    /// Endpoint de diagnóstico: muestra el texto crudo extraído y el estado del OCR.
    /// Útil para depurar documentos que no se procesan correctamente.
    /// </summary>
    /// <param name="archivo">Archivo a diagnosticar (PDF, PNG, JPEG, TIFF, BMP, WebP). Límite: 30 MB.</param>
    [HttpPost("diagnostico")]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> Diagnostico(IFormFile archivo)
    {
        if (archivo == null || archivo.Length == 0)
            return BadRequest(new { error = "No se proporcionó ningún archivo." });

        var tessDataPath = PdfExtractorService.GetTessDataPathForDiagnostics();
        var baseDir = AppContext.BaseDirectory;
        var workingDir = Directory.GetCurrentDirectory();

        var tessFiles = tessDataPath != null && Directory.Exists(tessDataPath)
            ? Directory.GetFiles(tessDataPath).Select(Path.GetFileName).ToArray()
            : Array.Empty<string>();

        using var buffer = new MemoryStream();
        await archivo.CopyToAsync(buffer);

        buffer.Position = 0;
        var (textoRaw, fuente) = await _service.ExtraerTextoRawAsync(buffer, archivo.ContentType);

        buffer.Position = 0;
        var resultado = await _service.ExtraerAsync(buffer, archivo.ContentType, archivo.FileName);

        return Ok(new
        {
            diagnostico = new
            {
                tessDataPath,
                tessDataEncontrado = tessDataPath != null,
                tessDataArchivos = tessFiles,
                baseDirectory = baseDir,
                workingDirectory = workingDir,
                fuenteExtraccion = fuente,
                caracteresExtraidos = textoRaw?.Length ?? 0,
                textoRaw = textoRaw != null
                    ? textoRaw.Substring(0, Math.Min(textoRaw.Length, 2000))
                    : null
            },
            resultado
        });
    }
}
