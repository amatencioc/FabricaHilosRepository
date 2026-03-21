using FabricaHilos.LecturaCorreos.Models;

namespace FabricaHilos.LecturaCorreos.Data;

public interface ILecturaCorreosRepository
{
    Task<IEnumerable<FacturaCorreo>> ObtenerFacturasPendientesCdrAsync();

    Task ActualizarEstadoAsync(
        long id,
        string estado,
        string codigoSunat,
        string mensajeSunat,
        byte[]? cdrZip);

    Task IncrementarIntentosAsync(long id);

    Task GuardarErrorAsync(long id, string mensajeError);

    Task InsertarFacturaPendienteCdrAsync(FacturaCorreo factura);

    /// <summary>
    /// Devuelve el ID del registro en FH_LECTCORREOS_FACTURAS que esté en estado PENDIENTE_CDR
    /// para el comprobante identificado por RUC, serie y correlativo.
    /// Retorna null si no existe o ya fue procesado.
    /// </summary>
    Task<long?> ObtenerIdFacturaPorSerieAsync(string ruc, string serie, int correlativo);

    /// <summary>
    /// SOLO PRUEBAS. Elimina todos los registros de las tablas del proceso
    /// en el orden correcto respetando las FK.
    /// Retorna el total de filas eliminadas por tabla.
    /// </summary>
    Task<LimpiezaResultado> LimpiarTablasAsync(CancellationToken ct = default);
}
