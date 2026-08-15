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
    ///
    /// Además, SIEMPRE (incluso cuando tmpProductionRecipe está vacía) intenta cerrar
    /// cabeceras ya existentes cuyo Terminated todavía esté NULL, copiándolo directo
    /// desde dbo.BatchDetail. Esto corrige el hallazgo de que OrgaTex vacía
    /// tmpProductionRecipe prácticamente al mismo tiempo que graba Terminated, por lo
    /// que el MERGE de cabecera (acotado a filas presentes en tmpProductionRecipe)
    /// nunca alcanza a capturarlo.
    /// </summary>
    Task<(int FilasDetalle, int FilasCabecera, int FilasCerradas)> SincronizarAsync(CancellationToken ct);
}
