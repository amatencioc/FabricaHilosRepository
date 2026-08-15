namespace FabricaHilos.OrgatexSync.Config;

public class OracleMigrationOptions
{
    public const string SeccionConfig = "OracleMigrationSync";

    /// <summary>
    /// Habilita o deshabilita el worker de migración a Oracle (OracleMigrationWorker).
    /// Cuando es false el servicio arranca pero no ejecuta ningún ciclo.
    /// </summary>
    public bool WorkerActivo { get; set; } = true;

    /// <summary>
    /// Intervalo entre ciclos de migración, en milisegundos. Default 5000 ms -- más
    /// holgado que RecipeSnapshotSync porque cada ciclo puede hacer varias llamadas
    /// a Oracle (una por línea de detalle + una por PARTIDA_MAS).
    /// </summary>
    public int IntervaloMs { get; set; } = 5000;

    /// <summary>
    /// Ventana de gracia, en segundos, desde que Terminated se marca hasta que la
    /// cabecera se considera lista para el CIERRE FINAL (IngRecetaMigrado=1, deja de
    /// re-sincronizarse). Ya NO controla cuándo empieza a migrar -- eso pasa desde que
    /// existe la cabecera con detalle (normalmente en estado Queued). Evita cerrar una
    /// receta cuyo detalle todavía se está terminando de escribir en
    /// RecipeSnapshot_Detalle (Terminated puede quedar grabado un instante antes que
    /// la última línea de detalle). Default 30s.
    /// </summary>
    public int VentanaGraciaSegundos { get; set; } = 30;

    /// <summary>
    /// Máximo de cabeceras que se procesan en paralelo dentro de cada fase del ciclo
    /// (migración a ING_RECETAS_G/D y vinculación de PARTIDA_MAS). Cada cabecera usa
    /// su propia conexión SQL Server/Oracle, por lo que procesarlas en paralelo reduce
    /// la duración del ciclo cuando varias recetas terminan casi al mismo tiempo, sin
    /// afectar la idempotencia (cada una hace su propio MERGE independiente). Default 4.
    /// </summary>
    public int MaxGradoParalelismo { get; set; } = 4;
}
