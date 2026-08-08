namespace FabricaHilos.OrgatexSync.Config;

public class RecipeSnapshotOptions
{
    public const string SeccionConfig = "RecipeSnapshotSync";

    /// <summary>
    /// Habilita o deshabilita el worker de polling (RecipeSnapshotWorker).
    /// Cuando es false el servicio arranca pero no ejecuta ningún ciclo.
    /// </summary>
    public bool WorkerActivo { get; set; } = true;

    /// <summary>
    /// Intervalo entre ciclos de polling, en milisegundos. dbo.tmpProductionRecipe
    /// (ORGATEX) se vacía en cuestión de segundos tras terminar el batch -- un
    /// intervalo corto es indispensable para no perder filas. Default 1500 ms.
    /// </summary>
    public int IntervaloMs { get; set; } = 1500;
}
