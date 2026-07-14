using FabricaHilos.Services.Archivos;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace FabricaHilos.Services.Contabilidad;

/// <summary>
/// Adaptador de Activo Fijo sobre el servicio centralizado de archivos.
/// Mantiene la misma API pública para no romper el controlador existente.
/// Internamente delega toda la lógica de validación y guardado a
/// <see cref="IProcesadorArchivoService"/>.
/// </summary>
public class ProcesadorImagenActivoFijo
{
    private readonly IProcesadorArchivoService _procesador;
    private readonly ILogger<ProcesadorImagenActivoFijo> _logger;

    /// <summary>Perfil específico de Activo Fijo: imágenes + PDF, máx 20 MB</summary>
    public static readonly PerfilArchivo Perfil = PerfilArchivo.ImagenYPdf;

    public ProcesadorImagenActivoFijo(
        IProcesadorArchivoService            procesador,
        ILogger<ProcesadorImagenActivoFijo>  logger)
    {
        _procesador = procesador;
        _logger     = logger;
    }

    // Mantiene el mismo record que usaba el controlador
    public record ResultadoProcesamiento(
        string NombreArchivo,
        long   BytesOriginales,
        long   BytesFinales,
        int    AnchoFinal,
        int    AltoFinal);

    /// <summary>
    /// Valida, procesa (si es imagen) y guarda el archivo en <paramref name="carpetaDestino"/>
    /// con el nombre base <paramref name="nombreBase"/> (sin extensión).
    /// </summary>
    public async Task<ResultadoProcesamiento> ProcesarYGuardarAsync(
        IFormFile archivo,
        string    carpetaDestino,
        string    nombreBase)
    {
        var resultado = await _procesador.GuardarAsync(archivo, carpetaDestino, nombreBase, Perfil);

        if (!resultado.Ok)
            throw new InvalidOperationException(resultado.Error ?? "Error al procesar el archivo.");

        return new ResultadoProcesamiento(
            resultado.NombreArchivo,
            resultado.BytesOriginales,
            resultado.BytesFinales,
            AnchoFinal: 0,
            AltoFinal:  0);
    }
}
