namespace FabricaHilos.Models.Sire;

/// <summary>
/// Registro de un job de exportación asíncrona de propuesta SIRE.
/// El BackgroundService SireExportacionWorker procesa estos jobs de forma desacoplada.
/// </summary>
public class SireExportacionJob
{
    public int Id { get; set; }

    /// <summary>Identificador único del job (GUID sin guiones). Retornado al front.</summary>
    public string JobId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Tipo de registro: "ventas" (RVIE) o "compras" (RCE).</summary>
    public string TipoRegistro { get; set; } = string.Empty;

    /// <summary>Período tributario YYYYMM (ej: "202601").</summary>
    public string Periodo { get; set; } = string.Empty;

    /// <summary>Usuario ASP.NET Identity que solicitó la exportación.</summary>
    public string UsuarioId { get; set; } = string.Empty;

    /// <summary>
    /// Estado del job: Pendiente → EnProceso → Completado | Error.
    /// </summary>
    public string Estado { get; set; } = EstadoJob.Pendiente;

    /// <summary>Número de ticket retornado por SUNAT al exportar.</summary>
    public string? NumTicket { get; set; }

    /// <summary>Nombre del archivo ZIP generado por SUNAT.</summary>
    public string? NombreArchivo { get; set; }

    /// <summary>Ruta UNC completa donde se guardó el archivo en la red.</summary>
    public string? RutaArchivo { get; set; }

    /// <summary>Código de tipo de archivo devuelto por SUNAT (ej: "ZIP").</summary>
    public string? CodTipoArchivo { get; set; }

    /// <summary>Código de proceso devuelto por SUNAT (requerido para descarga 5.17).</summary>
    public string? CodProceso { get; set; }

    /// <summary>Número de registros del TXT parseados e insertados en Oracle SIRE_VALIDA.</summary>
    public int? RegistrosInsertados { get; set; }

    /// <summary>Número de registros que ya existían y fueron omitidos (MERGE - no duplicados).</summary>
    public int? RegistrosDuplicados { get; set; }

    /// <summary>Mensaje de error si Estado = Error.</summary>
    public string? MensajeError { get; set; }

    /// <summary>Timestamp UTC de creación del job.</summary>
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    /// <summary>Timestamp UTC de la última actualización de estado.</summary>
    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;

    /// <summary>Timestamp UTC de finalización (Completado o Error).</summary>
    public DateTime? FechaFinalizacion { get; set; }

    /// <summary>
    /// Fecha/hora mínima para la próxima consulta del ticket a SUNAT.
    /// Solo aplica cuando Estado = EsperandoTicket.
    /// Controlada por SireTicketWatcherWorker.
    /// </summary>
    public DateTime? ProximaConsulta { get; set; }
}

public static class EstadoJob
{
    public const string Pendiente        = "Pendiente";
    public const string EnProceso        = "EnProceso";
    /// <summary>Ticket obtenido; SUNAT aún procesando. SireTicketWatcherWorker vigila cada 15 min.</summary>
    public const string EsperandoTicket  = "EsperandoTicket";
    public const string Completado       = "Completado";
    public const string Error            = "Error";

    /// <summary>Estados que representan un job en curso (no terminal).</summary>
    public static readonly IReadOnlyList<string> Activos =
        [Pendiente, EnProceso, EsperandoTicket];
}
