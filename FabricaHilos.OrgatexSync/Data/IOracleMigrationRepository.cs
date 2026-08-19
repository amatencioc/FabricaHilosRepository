namespace FabricaHilos.OrgatexSync.Data;

using FabricaHilos.OrgatexSync.Models;

/// <summary>
/// Migra RecipeSnapshot_Cabecera/Detalle (ORGATEX) hacia Oracle SIG
/// (ING_RECETAS_G/D + PARTIDA_MAS vía PKG_ORGATEX). Ver OracleMigrationRepository.
/// </summary>
public interface IOracleMigrationRepository
{
    /// <summary>
    /// Cabeceras con al menos 1 línea en RecipeSnapshot_Detalle pendientes de
    /// (re)sincronizar hacia ING_RECETAS_G/D (v3.0: gate por ROWVERSION, ya NO por
    /// IngRecetaMigrado). Dispara si nunca se sincronizó con éxito, o si Cabecera/
    /// Detalle cambiaron después del último watermark exitoso -- sin importar si ya
    /// estaba "cerrada" (Terminated + ventana de gracia). Excluye las cabeceras
    /// marcadas EliminadoEnOracle=1 (borrado/anulación intencional en Oracle, regla de
    /// negocio: no se recrean). Devuelve también el watermark (MIN_ACTIVE_ROWVERSION())
    /// capturado ANTES de leer los datos, a pasar tal cual a
    /// <see cref="MigrarIngRecetaAsync"/> para no perder escrituras concurrentes.
    /// </summary>
    Task<(IReadOnlyList<RecipeCabeceraPendiente> Cabeceras, byte[] Watermark)> ObtenerCabecerasPendientesIngRecetaAsync(CancellationToken ct);

    /// <summary>
    /// Trae, en UNA sola consulta (WHERE DyelotRefNo IN (...)), el detalle de todas las
    /// cabeceras indicadas. Reemplaza N consultas individuales (una por cabecera) por
    /// una sola ida a SQL Server por ciclo, evitando abrir hasta MaxGradoParalelismo
    /// conexiones SQL Server concurrentes solo para leer el detalle.
    /// </summary>
    Task<IReadOnlyDictionary<string, IReadOnlyList<RecipeDetalleLinea>>> ObtenerDetallesBatchAsync(
        IReadOnlyList<string> dyelotRefNos, CancellationToken ct);

    /// <summary>
    /// Migra 1 cabecera completa (todas sus líneas de detalle, ya cargadas por
    /// <see cref="ObtenerDetallesBatchAsync"/>) a ING_RECETAS_G/D vía SP_MERGE_ING_RECETA,
    /// y actualiza dbo.RecipeSnapshot_OracleSync con el resultado. El MERGE es idempotente
    /// y se re-ejecuta en cada ciclo mientras haya cambios pendientes de sincronizar.
    /// v3.0: si <paramref name="cabecera"/> ya se había sincronizado antes
    /// (<see cref="RecipeCabeceraPendiente.UltimoRowVerSincronizado"/> no nulo), primero
    /// verifica que el header siga existiendo en Oracle -- si ya no existe, se asume
    /// borrado/anulación intencional y se marca EliminadoEnOracle=1 sin recrear nada
    /// (regla de negocio del usuario). <paramref name="watermark"/> solo se persiste como
    /// nuevo <c>UltimoRowVerSincronizado</c> cuando TODAS las líneas salieron OK
    /// (fail==0); si algo falló, se deja el watermark anterior para reintentar en el
    /// próximo ciclo. <c>IngRecetaMigrado</c> queda solo como flag informativo de
    /// "cierre" (Terminated + <paramref name="ventanaGraciaSegundos"/>), ya no gatea la
    /// re-sincronización.
    /// </summary>
    Task<(int Ok, int Fail)> MigrarIngRecetaAsync(
        RecipeCabeceraPendiente cabecera, IReadOnlyList<RecipeDetalleLinea> detalle, byte[] watermark, int ventanaGraciaSegundos, CancellationToken ct);

    /// <summary>
    /// Partidas candidatas (dbo.RecipeSnapshot_CabeceraPartida) pendientes de vincular
    /// hacia PARTIDA_MAS (Vinculada=0). Desde v3.2: 1 fila por partida detectada, no 1
    /// fila por cabecera -- una misma receta puede aportar hasta N filas aquí.
    /// </summary>
    Task<IReadOnlyList<PartidaCandidata>> ObtenerCabecerasPendientesPartidaAsync(CancellationToken ct);

    /// <summary>
    /// Vincula la PARTIDA real del ERP vía SP_MERGE_PARTIDA_MAS y actualiza
    /// dbo.RecipeSnapshot_CabeceraPartida. Devuelve false (sin lanzar) si la PARTIDA aún
    /// no existe en el ERP -- se reintenta en ciclos siguientes.
    /// </summary>
    Task<bool> VincularPartidaAsync(PartidaCandidata candidata, CancellationToken ct);
}
