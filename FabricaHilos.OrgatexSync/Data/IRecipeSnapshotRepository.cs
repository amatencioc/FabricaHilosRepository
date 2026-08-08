namespace FabricaHilos.OrgatexSync.Data;

public interface IRecipeSnapshotRepository
{
    /// <summary>
    /// Copia (MERGE, acumulativo) el contenido actual de dbo.tmpProductionRecipe hacia
    /// dbo.RecipeSnapshot_Detalle / dbo.RecipeSnapshot_Cabecera, en ORGATEX (SQL Server).
    /// Solo ejecuta SELECT sobre tmpProductionRecipe (nunca INSERT/UPDATE/DELETE ahí) --
    /// reemplaza al trigger trg_tmpProductionRecipe_Snapshot, deshabilitado permanentemente
    /// porque SQL Server prohíbe cualquier trigger sobre esa tabla mientras el cliente
    /// OrgaTex inserte con OUTPUT sin INTO (Msg 334).
    /// </summary>
    Task<(int FilasDetalle, int FilasCabecera)> SincronizarAsync(CancellationToken ct);
}
