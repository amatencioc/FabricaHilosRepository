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
}
