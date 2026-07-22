namespace FabricaHilos.OrgatexSync.Logging;

using Microsoft.Extensions.Logging;

/// <summary>
/// Logger dedicado a registrar cada llamada individual a PKG_ORGATEX.SP_MERGE_FILA
/// (una fila de ORGATEX migrada a CARGA_ORGATEX), sea exitosa o con error.
/// Escribe en su propia carpeta/archivo (Logs/OrgatexCalls/orgatex-calls-.log),
/// separado del log general del servicio (Logs/orgatexSync-.log). Ver Program.cs,
/// sub-logger de Serilog filtrado por <see cref="NombreCategoria"/>.
/// </summary>
public static class OrgatexCallLogger
{
    /// <summary>
    /// Categoría/SourceContext usada para filtrar en el sub-logger de Serilog (Program.cs)
    /// y para crear la instancia de <see cref="ILogger"/> vía <see cref="ILoggerFactory"/>.
    /// </summary>
    public const string NombreCategoria = "OrgatexCallLog";

    /// <summary>
    /// Crea el logger dedicado a partir de la fábrica de loggers registrada en DI.
    /// </summary>
    public static ILogger Crear(ILoggerFactory loggerFactory) =>
        loggerFactory.CreateLogger(NombreCategoria);
}
