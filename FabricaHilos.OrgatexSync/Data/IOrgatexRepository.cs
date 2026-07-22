namespace FabricaHilos.OrgatexSync.Data;

using FabricaHilos.OrgatexSync.Models;

public interface IOrgatexRepository
{
    /// <summary>Lee de ORGATEX (SQL Server) las recetas de tintura con Endtime en [desde, hasta].</summary>
    Task<IReadOnlyList<OrgatexRow>> ObtenerRecetasAsync(DateTime desde, DateTime hasta, CancellationToken ct);

    /// <summary>
    /// Migra (MERGE) las filas a Oracle SIG.CARGA_ORGATEX vía PKG_ORGATEX.SP_MERGE_FILA.
    /// Devuelve cuántas filas se procesaron correctamente y cuántas fallaron.
    /// </summary>
    Task<(int Ok, int Fail)> MergeCargaOrgatexAsync(IReadOnlyList<OrgatexRow> filas, CancellationToken ct);
}
