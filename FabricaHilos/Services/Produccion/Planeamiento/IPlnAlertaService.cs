using FabricaHilos.Models.Produccion.Planeamiento;

namespace FabricaHilos.Services.Produccion.Planeamiento;

public interface IPlnAlertaService
{
    Task<IEnumerable<PlnAlerta>> GetActivasAsync();

    /// <summary>
    /// Historial de alertas resueltas o ignoradas de los últimos <paramref name="ultDias"/> días.
    /// Lee directamente de PLN_ALERTA (ESTADO IN ('R','I')) porque V_PLN_ALERTAS_ACTIVAS solo expone ESTADO='A'.
    /// </summary>
    Task<IEnumerable<PlnAlerta>> GetHistorialAsync(int ultDias = 30);

    Task ResolverAsync(long idAlerta, string usuario);
    Task IgnorarAsync(long idAlerta, string usuario);

    /// <summary>
    /// Llama a PKG_PLN.SP_PLN_GENERA_ALERTAS para forzar la generación de alertas sin esperar al JOB.
    /// Normalmente ejecutado cada hora por JOB_PLN_ALERTAS.
    /// </summary>
    Task GenerarAlertasAsync();
}
