namespace FabricaHilos.Models.Sire;

/// <summary>
/// ViewModel para el dashboard principal de SIRE
/// Agrupa datos de ambas operaciones (RVIE y RCE) para presentación en Index
/// </summary>
public class SireDashboardViewModel
{
    /// <summary>Estado general de la conexión a SIRE</summary>
    public bool EstadoConexion { get; set; }

    /// <summary>Mensaje descriptivo del estado</summary>
    public string MensajeEstado { get; set; }

    /// <summary>Indica si el sistema está en modo Mock</summary>
    public bool EsMock { get; set; }

    // ─────────────────────────────────────────────────────────────
    // RVIE (Registro de Ventas e Ingresos)
    // ─────────────────────────────────────────────────────────────

    /// <summary>Total de períodos RVIE con propuestas disponibles</summary>
    public int RviePropuestasDisponibles { get; set; }

    /// <summary>Total de períodos RVIE en proceso</summary>
    public int RvieEnProceso { get; set; }

    /// <summary>Total de períodos RVIE cerrados</summary>
    public int RvieCerrados { get; set; }

    /// <summary>Próximo período RVIE con acción pendiente</summary>
    public string RvieProximaAccion { get; set; }

    /// <summary>Últimos 5 períodos RVIE ordenados descendentemente (objetos anónimos)</summary>
    public List<dynamic> RvieUltimosPeriodos { get; set; } = new();

    // ─────────────────────────────────────────────────────────────
    // RCE (Registro de Compras y Gastos)
    // ─────────────────────────────────────────────────────────────

    /// <summary>Total de períodos RCE con propuestas disponibles</summary>
    public int RcePropuestasDisponibles { get; set; }

    /// <summary>Total de períodos RCE en proceso</summary>
    public int RceEnProceso { get; set; }

    /// <summary>Total de períodos RCE cerrados</summary>
    public int RceCerrados { get; set; }

    /// <summary>Próximo período RCE con acción pendiente</summary>
    public string RceProximaAccion { get; set; }

    /// <summary>Últimos 5 períodos RCE ordenados descendentemente (objetos anónimos)</summary>
    public List<dynamic> RceUltimosPeriodos { get; set; } = new();

    // ─────────────────────────────────────────────────────────────
    // Monitoreo General
    // ─────────────────────────────────────────────────────────────

    /// <summary>Última vez que se ejecutó health check</summary>
    public DateTime? UltimaRevisionSalud { get; set; }

    /// <summary>Estado del último health check</summary>
    public string EstadoUltimaRevision { get; set; }

    /// <summary>Advertencias o problemas detectados recientemente</summary>
    public List<string> Advertencias { get; set; } = new();
}
