namespace FabricaHilos.Models.Sire;

/// <summary>
/// Registro de auditoría de una llamada HTTP al API SUNAT-SIRE.
/// Se persiste en la tabla Oracle SIRE_LOG para investigación de incidencias.
/// </summary>
public class SireApiLog
{
    public long      Id          { get; set; }
    public DateTime  Fecha       { get; set; } = DateTime.UtcNow;

    /// <summary>JobId del job asociado. Puede ser null si la llamada no pertenece a un job (p.ej. health check).</summary>
    public string?   JobId       { get; set; }

    /// <summary>Tipo de operación: AUTH | EXPORTAR | TICKET | DESCARGAR | HEALTH</summary>
    public string    Operacion   { get; set; } = string.Empty;

    public string?   MetodoHttp  { get; set; }
    public string?   Url         { get; set; }
    public int?      HttpStatus  { get; set; }
    public long?     DuracionMs  { get; set; }
    public bool      Exito       { get; set; }

    /// <summary>Resumen del resultado o del error (máx 2000 chars).</summary>
    public string?   Mensaje     { get; set; }
}

public static class SireOperacion
{
    public const string Auth      = "AUTH";
    public const string Exportar  = "EXPORTAR";
    public const string Ticket    = "TICKET";
    public const string Descargar = "DESCARGAR";
    public const string Health    = "HEALTH";
    public const string Guardar   = "GUARDAR";    // Guardar ZIP en ruta de red
    public const string Cargar    = "CARGAR";     // Cargar propuesta en Oracle SIRE_VALIDA
    public const string Completar = "COMPLETAR";  // Finalización del job
}
