using FabricaHilos.DocumentExtractor.Models;

namespace FabricaHilos.DocumentExtractor.Services;

public interface IDocumentExtractorService
{
    Task<DocumentoExtraido> ExtraerAsync(Stream archivo, string tipoMime, string nombreArchivo);
}
