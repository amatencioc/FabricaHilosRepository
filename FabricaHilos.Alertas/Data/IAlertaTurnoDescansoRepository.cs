namespace FabricaHilos.Alertas.Data;

using FabricaHilos.Alertas.Models;

public interface IAlertaTurnoDescansoRepository
{
    /// <summary>
    /// Lee AQUARIUS.V_SCA_ALERTA_TAREO_DETALLE WHERE NOTIFICADO='N': alertas de
    /// tareo (TU=3 semanas mismo turno, SD=sin descanso) generadas por el job
    /// Oracle JOB_SCA_ALERTAS_TAREO / PKG_SCA_ALERTAS_TAREO.GENERAR_ALERTAS que
    /// todav�a no han sido notificadas por correo.
    ///
    /// Nota (PKG_SCA_ALERTAS_TAREO v2.2/v2.3): la generaci�n ya excluye del lado
    /// Oracle a empleados de �reas administrativas/oficina (ES_AREA_ADMINISTRATIVA)
    /// y con cargo Jefe/Jefatura/Supervisor (ES_CARGO_SUPERVISOR), por lo que estos
    /// nunca aparecer�n en la vista. El worker .NET no requiere l�gica adicional:
    /// solo consume lo que ya viene filtrado.
    /// </summary>
    Task<IReadOnlyList<AlertaTurnoDescansoDetalle>> ObtenerPendientesAsync(CancellationToken ct);

    /// <summary>
    /// Llama AQUARIUS.PKG_SCA_ALERTAS_TAREO.MARCAR_NOTIFICADO(p_id_alerta) para
    /// que la alerta ya enviada por correo no se vuelva a recoger en el pr�ximo ciclo.
    /// </summary>
    Task MarcarNotificadoAsync(long idAlerta, CancellationToken ct);
}
