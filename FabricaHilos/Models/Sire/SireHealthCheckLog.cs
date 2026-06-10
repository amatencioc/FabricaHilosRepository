namespace FabricaHilos.Models.Sire;

/// <summary>
/// Registro de auditoría para health checks de SIRE.
/// Se utiliza para historial, análisis de disponibilidad y alertas.
/// </summary>
public class SireHealthCheckLog
{
    public int Id { get; set; }

    /// <summary>Timestamp UTC del health check.</summary>
    public DateTime FechaUtc { get; set; }

    /// <summary>Estado del health check: Healthy, Degraded, Unhealthy.</summary>
    public string Status { get; set; } = "Unhealthy";

    /// <summary>¿Autenticación OAuth2 exitosa?</summary>
    public bool AuthOk { get; set; }

    /// <summary>Minutos restantes hasta que el token expire (si AuthOk=true).</summary>
    public double? TokenMinutosRestantes { get; set; }

    /// <summary>¿Servicio RVIE disponible?</summary>
    public bool RvieOk { get; set; }

    /// <summary>Número de periodos RVIE disponibles (si RvieOk=true).</summary>
    public int? RviePeriodos { get; set; }

    /// <summary>Descripción del error RVIE (si RvieOk=false).</summary>
    public string? RvieError { get; set; }

    /// <summary>¿Servicio RCE disponible?</summary>
    public bool RceOk { get; set; }

    /// <summary>Número de periodos RCE disponibles (si RceOk=true).</summary>
    public int? RcePeriodos { get; set; }

    /// <summary>Descripción del error RCE (si RceOk=false).</summary>
    public string? RceError { get; set; }

    /// <summary>Descripción del error general (si Status != Healthy).</summary>
    public string? Descripcion { get; set; }

    /// <summary>¿Se envió alerta por email por este estado?</summary>
    public bool AlertaEnviada { get; set; }

    /// <summary>Timestamp de la última alerta enviada (para evitar spam).</summary>
    public DateTime? UltimaAlertaUtc { get; set; }
}
