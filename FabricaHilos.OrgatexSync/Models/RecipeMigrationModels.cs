namespace FabricaHilos.OrgatexSync.Models;

/// <summary>
/// Cabecera candidata a migrar hacia Oracle (leída de dbo.RecipeSnapshot_Cabecera,
/// combinada con el estado de dbo.RecipeSnapshot_OracleSync). Sirve tanto para el
/// paso de ING_RECETA como para el de PARTIDA_MAS -- ver OracleMigrationRepository.
/// Desde el cambio "sync continuo": ya NO se exige Terminated para migrar (se migra
/// desde que existe cabecera+detalle, típicamente en estado Queued) -- Terminated,
/// Loaded y Started ahora son opcionales/progresivos; Queued es el único que siempre
/// está presente desde el registro.
/// </summary>
public sealed class RecipeCabeceraPendiente
{
    public string    DyelotRefNo     { get; set; } = "";
    public string?   Partida         { get; set; }
    public string?   Maquina         { get; set; }
    public int?      RecipeIdOrgatex { get; set; }
    public double?   PesoLoteKg      { get; set; }
    public DateTime  Queued          { get; set; }
    public DateTime? Loaded          { get; set; }
    public DateTime? Started         { get; set; }
    public DateTime? Terminated      { get; set; }

    /// <summary>
    /// Watermark de la última sincronización SIN ERROR de esta cabecera (v3.0,
    /// dbo.RecipeSnapshot_OracleSync.UltimoRowVerSincronizado). Null = nunca se
    /// sincronizó con éxito todavía (primera vez, no hay nada que "resucitar" en
    /// Oracle). Distinto de null = ya se sincronizó antes -- antes de reintentar
    /// hay que verificar que el header siga existiendo en Oracle (ver
    /// OracleMigrationRepository.MigrarIngRecetaAsync).
    /// </summary>
    public byte[]?   UltimoRowVerSincronizado { get; set; }
}

/// <summary>
/// Partida candidata (dbo.RecipeSnapshot_CabeceraPartida) pendiente de vincular vía
/// PKG_ORGATEX.SP_MERGE_PARTIDA_MAS. Desde v3.2: una receta puede tener hasta N
/// partidas (el negocio indicó hasta 10), detectadas por patrón dentro de las 20
/// columnas genéricas BatchDetail.batch_text_01..20 (ver
/// RecipeSnapshotRepository.SqlMergePartidasDetectadas) -- ya NO hay columnas fijas
/// Partida/Partida2 en RecipeCabeceraPendiente para esto; cada partida detectada es
/// una fila independiente, vinculada de forma individual e idempotente.
/// </summary>
public sealed class PartidaCandidata
{
    public string DyelotRefNo { get; set; } = "";
    public string Partida     { get; set; } = "";
}

/// <summary>
/// Línea de detalle (dbo.RecipeSnapshot_Detalle) lista para pasar a
/// PKG_ORGATEX.SP_MERGE_ING_RECETA. Desde v3.3 del package: RecipeAmount/RecipeUnit
/// (dosis original de la fórmula) van a P_CANTIDAD/P_UNIDAD; CantidadG (cantidad real
/// calculada) va al nuevo P_TOTAL.
/// </summary>
public sealed class RecipeDetalleLinea
{
    public int     CallOff      { get; set; }
    public int     RecipePos    { get; set; }
    public string? ProductCode  { get; set; }
    public double? CantidadG    { get; set; }
    public string? Unit         { get; set; }
    public double? RecipeAmount { get; set; }
    public string? RecipeUnit   { get; set; }
}
