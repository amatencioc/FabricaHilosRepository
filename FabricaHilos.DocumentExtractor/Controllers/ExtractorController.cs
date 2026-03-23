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
    /// </summary>
    /// <param name="archivo">Archivo a procesar (PDF, PNG, JPEG). Límite: 10 MB.</param>
    [HttpPost("extraer")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Extraer(IFormFile archivo)
    {
        if (archivo == null || archivo.Length == 0)
            return BadRequest(new { error = "No se proporcionó ningún archivo." });

        var tiposMimePermitidos = new[] { "application/pdf", "image/png", "image/jpeg" };
        if (!tiposMimePermitidos.Contains(archivo.ContentType, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { error = $"Tipo de archivo no permitido: {archivo.ContentType}. Solo se aceptan PDF, PNG y JPEG." });

        if (archivo.Length > 10 * 1024 * 1024)
            return BadRequest(new { error = "El archivo supera el límite de 10 MB." });

        using var stream = archivo.OpenReadStream();
        var resultado = await _service.ExtraerAsync(stream, archivo.ContentType, archivo.FileName);
        return Ok(resultado);
    }
}
