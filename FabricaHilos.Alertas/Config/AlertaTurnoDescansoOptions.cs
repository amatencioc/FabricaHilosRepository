namespace FabricaHilos.Alertas.Config;

/// <summary>
/// Configuraci�n del proceso semanal de alertas de tareo (turno/descanso).
/// Lee AQUARIUS.V_SCA_ALERTA_TAREO_DETALLE (NOTIFICADO='N'), generado por
/// PKG_SCA_ALERTAS_TAREO.GENERAR_ALERTAS (job Oracle JOB_SCA_ALERTAS_TAREO),
/// arma un Excel y lo env�a por correo. Al enviar con �xito, marca cada
/// alerta como notificada (PKG_SCA_ALERTAS_TAREO.MARCAR_NOTIFICADO).
/// </summary>
public class AlertaTurnoDescansoOptions
{
    public const string SeccionConfig = "AlertasTurnoDescanso";

    /// <summary>
    /// Habilita o deshabilita el worker. Cuando es false el servicio arranca
    /// pero no ejecuta ning�n ciclo.
    /// </summary>
    public bool WorkerActivo { get; set; } = true;

    /// <summary>
    /// D�a de la semana en que se ejecuta el env�o (default: jueves).
    /// </summary>
    public DayOfWeek DiaSemanaEjecucion { get; set; } = DayOfWeek.Thursday;

    /// <summary>
    /// Hora del d�a (0-23) en que se ejecuta el env�o (default: 8am).
    /// </summary>
    public int HoraEjecucion { get; set; } = 8;

    /// <summary>
    /// Correo (o lista separada por coma/punto y coma) que recibe el reporte
    /// semanal de alertas de turno/descanso.
    /// </summary>
    public string CorreoDestino { get; set; } = string.Empty;

    /// <summary>
    /// Si es true, ejecuta un ciclo inmediatamente al iniciar el servicio
    /// (�til para pruebas), adem�s de la programaci�n semanal normal.
    /// </summary>
    public bool EjecutarAlIniciar { get; set; } = false;
}
