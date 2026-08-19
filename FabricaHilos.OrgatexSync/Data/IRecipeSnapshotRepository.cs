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
    ///
    /// v3.2: también detecta y guarda en dbo.RecipeSnapshot_CabeceraPartida hasta N
    /// partidas por receta (el negocio indicó hasta 10), encontradas por patrón dentro
    /// de las 20 columnas genéricas BatchDetail.batch_text_01..20 -- no hay una columna
    /// fija "partida N" (varía según tipo de máquina/plantilla en OrgaTex).
    /// </summary>
    Task<(int FilasDetalle, int FilasCabecera, int FilasCerradas, int FilasPartidas)> SincronizarAsync(CancellationToken ct);
}
