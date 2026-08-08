namespace FabricaHilos.OrgatexSync.Data;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Reemplazo del trigger trg_tmpProductionRecipe_Snapshot (ver
/// ORGATEX/snapshot_recipe_trigger.sql), deshabilitado permanentemente por una
/// restricción dura de SQL Server: ningún trigger (AFTER ni INSTEAD OF) puede
/// convivir con el INSERT que hace el cliente OrgaTex sobre tmpProductionRecipe
/// (usa OUTPUT INSERTED.AutoKey sin INTO -- Msg 334). Este repositorio ejecuta,
/// desde una conexión propia y separada (fuera de la transacción del cliente),
/// exactamente la misma lógica MERGE que tenía el trigger, leyendo
/// dbo.tmpProductionRecipe directamente en vez de la pseudo-tabla "inserted".
/// Solo lee de tmpProductionRecipe -- jamás la modifica.
/// </summary>
public sealed class RecipeSnapshotRepository : IRecipeSnapshotRepository
{
    // Igual al MERGE de detalle del trigger (v1.2): ISNULL(CallOff,-1) evita el NULL
    // en la PK de RecipeSnapshot_Detalle sin descartar la fila (ver hallazgo crítico
    // 2026-08-07 en /memories/repo/orgatex-sqlserver.md).
    private const string SqlMergeDetalle = """
        MERGE dbo.RecipeSnapshot_Detalle AS tgt
        USING (
            SELECT LEFT(DyelotRefNo COLLATE DATABASE_DEFAULT, 20) AS DyelotRefNo, CorrectionNumber, ISNULL(CallOff, -1) AS CallOff, RecipePos,
                   LEFT(Number COLLATE DATABASE_DEFAULT, 60)  AS ProductCode,
                   LEFT(Name   COLLATE DATABASE_DEFAULT, 200) AS Descripcion,
                   CASE RecipePosType WHEN 1 THEN 'CHEMICAL' WHEN 2 THEN 'DYESTUFF' ELSE 'OTRO' END AS Tipo,
                   TRY_CONVERT(float, Amount) * 1000 AS CantidadG,
                   'g' AS Unit,
                   CASE KindOfStation WHEN 2 THEN 'AUTO' WHEN 5 THEN 'MAN' ELSE '' END AS Modo
            FROM dbo.tmpProductionRecipe
            WHERE RecipePosType IN (1, 2)
        ) AS src
        ON  tgt.DyelotRefNo      = src.DyelotRefNo
        AND tgt.CorrectionNumber = src.CorrectionNumber
        AND tgt.CallOff          = src.CallOff
        AND tgt.RecipePos        = src.RecipePos
        WHEN MATCHED THEN UPDATE SET
            ProductCode  = src.ProductCode,
            Descripcion  = src.Descripcion,
            Tipo         = src.Tipo,
            CantidadG    = src.CantidadG,
            Unit         = src.Unit,
            Modo         = src.Modo,
            FechaCaptura = GETDATE()
        WHEN NOT MATCHED THEN INSERT
            (DyelotRefNo, CorrectionNumber, CallOff, RecipePos, ProductCode, Descripcion, Tipo, CantidadG, Unit, Modo, FechaCaptura)
            VALUES
            (src.DyelotRefNo, src.CorrectionNumber, src.CallOff, src.RecipePos, src.ProductCode, src.Descripcion, src.Tipo, src.CantidadG, src.Unit, src.Modo, GETDATE());
        """;

    // Igual al MERGE de cabecera del trigger; única diferencia real: la fuente de
    // DyelotRefNo a procesar es la tabla completa (leída por este proceso), no la
    // pseudo-tabla "inserted" de un trigger.
    private const string SqlMergeCabecera = """
        MERGE dbo.RecipeSnapshot_Cabecera AS tgt
        USING (
            SELECT
                LEFT(bd.batch_ref_no COLLATE DATABASE_DEFAULT, 20)   AS DyelotRefNo,
                LEFT(bd.batch_text_01, 200)                          AS Partida,
                LEFT(bd.machine_no, 8)                               AS Maquina,
                LEFT(m.name, 200)                                    AS NombreMaquina,
                LEFT(ri.Name, 255)                                   AS RecetaNo,
                LEFT(ri.Description, 255)                            AS RecetaDesc,
                LEFT(ct.Name, 255)                                   AS ColorNo,
                LEFT(ct.Description, 255)                            AS ColorNombre,
                LEFT(cu.Name, 255)                                   AS Cliente,
                LEFT(ql.Name, 255)                                   AS Calidad,
                LEFT(ql.Description, 255)                            AS CalidadDescription,
                TRY_CONVERT(float, bd.batch_parameter_01)                                  AS PesoLoteKg,
                TRY_CONVERT(float, bd.batch_parameter_03)                                  AS RelacionBanioLxKg,
                TRY_CONVERT(float, bd.batch_parameter_01) * TRY_CONVERT(float, bd.batch_parameter_03) AS CantidadAguaL,
                bd.queued, bd.loaded, bd.started, bd.terminated
            FROM (SELECT DISTINCT DyelotRefNo FROM dbo.tmpProductionRecipe) di
            JOIN dbo.BatchDetail bd ON bd.batch_ref_no = di.DyelotRefNo
            LEFT JOIN dbo.Machine m ON m.machine_no = bd.machine_no
            OUTER APPLY (SELECT TOP 1 RowGuid FROM otx.Item WHERE Id = bd.RecipeID AND Category_Id = 'PROCESS_RECIPE') ri_g
            LEFT JOIN otx.Item ri ON ri.RowGuid = ri_g.RowGuid
            OUTER APPLY (SELECT TOP 1 ia.Link_Item_RowGuid AS g FROM otx.Item_Attribute ia JOIN otx.Attribute a ON a.Id = ia.Attribute_Id WHERE ia.Item_RowGuid = ri.RowGuid AND a.Name = 'ColorType') ct_g
            LEFT JOIN otx.Item ct ON ct.RowGuid = ct_g.g
            OUTER APPLY (SELECT TOP 1 ia.Link_Item_RowGuid AS g FROM otx.Item_Attribute ia JOIN otx.Attribute a ON a.Id = ia.Attribute_Id WHERE ia.Item_RowGuid = ri.RowGuid AND a.Name = 'Customer') cu_g
            LEFT JOIN otx.Item cu ON cu.RowGuid = cu_g.g
            OUTER APPLY (SELECT TOP 1 ia.Link_Item_RowGuid AS g FROM otx.Item_Attribute ia JOIN otx.Attribute a ON a.Id = ia.Attribute_Id WHERE ia.Item_RowGuid = ri.RowGuid AND a.Name = 'Quality') ql_g
            LEFT JOIN otx.Item ql ON ql.RowGuid = ql_g.g
        ) AS src
        ON tgt.DyelotRefNo = src.DyelotRefNo
        WHEN MATCHED THEN UPDATE SET
            Partida = src.Partida, Maquina = src.Maquina, NombreMaquina = src.NombreMaquina,
            RecetaNo = src.RecetaNo, RecetaDesc = src.RecetaDesc, ColorNo = src.ColorNo, ColorNombre = src.ColorNombre,
            Cliente = src.Cliente, Calidad = src.Calidad, CalidadDescription = src.CalidadDescription,
            PesoLoteKg = src.PesoLoteKg, RelacionBanioLxKg = src.RelacionBanioLxKg, CantidadAguaL = src.CantidadAguaL,
            Queued = src.queued, Loaded = src.loaded, Started = src.started, Terminated = src.terminated,
            FuenteDetalle = 'SNAPSHOT', FechaCaptura = GETDATE()
        WHEN NOT MATCHED THEN INSERT
            (DyelotRefNo, Partida, Maquina, NombreMaquina, RecetaNo, RecetaDesc, ColorNo, ColorNombre,
             Cliente, Calidad, CalidadDescription, PesoLoteKg, RelacionBanioLxKg, CantidadAguaL,
             Queued, Loaded, Started, Terminated, FuenteDetalle, FechaCaptura)
            VALUES
            (src.DyelotRefNo, src.Partida, src.Maquina, src.NombreMaquina, src.RecetaNo, src.RecetaDesc, src.ColorNo, src.ColorNombre,
             src.Cliente, src.Calidad, src.CalidadDescription, src.PesoLoteKg, src.RelacionBanioLxKg, src.CantidadAguaL,
             src.queued, src.loaded, src.started, src.terminated, 'SNAPSHOT', GETDATE());
        """;

    private readonly string _connStr;
    private readonly ILogger<RecipeSnapshotRepository> _logger;

    public RecipeSnapshotRepository(IConfiguration configuration, ILogger<RecipeSnapshotRepository> logger)
    {
        _connStr = configuration.GetConnectionString("OrgatexLiveConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:OrgatexLiveConnection no configurada.");
        _logger = logger;
    }

    // Chequeo liviano previo al MERGE: en la mayoría de los ciclos (cada
    // IntervaloMs) tmpProductionRecipe está vacía porque el batch aún no
    // terminó. Evita pagar el costo de los OUTER APPLY/JOIN de SqlMergeCabecera
    // en cada polling cuando no hay absolutamente nada que sincronizar.
    private const string SqlExisteAlgo = "SELECT TOP (1) 1 FROM dbo.tmpProductionRecipe";

    public async Task<(int FilasDetalle, int FilasCabecera)> SincronizarAsync(CancellationToken ct)
    {
        await using var conn = new SqlConnection(_connStr);
        await conn.OpenAsync(ct);

        if (!await HayDatosPendientesAsync(conn, ct))
        {
            return (0, 0);
        }

        // Ambos MERGE se ejecutan en la misma transacción: si el de cabecera
        // falla, no queremos dejar el detalle ya confirmado (evita snapshots
        // parciales/inconsistentes entre RecipeSnapshot_Detalle y _Cabecera).
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(ct);

        int filasDetalle = await EjecutarMergeAsync(conn, tx, SqlMergeDetalle, ct);
        int filasCabecera = await EjecutarMergeAsync(conn, tx, SqlMergeCabecera, ct);

        await tx.CommitAsync(ct);

        return (filasDetalle, filasCabecera);
    }

    private static async Task<bool> HayDatosPendientesAsync(SqlConnection conn, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(SqlExisteAlgo, conn) { CommandTimeout = 10 };
        var resultado = await cmd.ExecuteScalarAsync(ct);
        return resultado is not null;
    }

    private async Task<int> EjecutarMergeAsync(SqlConnection conn, SqlTransaction tx, string sql, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(sql, conn, tx) { CommandTimeout = 30 };
        try
        {
            return await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "[RECIPE-SNAPSHOT] Error ejecutando MERGE contra ORGATEX (tmpProductionRecipe).");
            throw;
        }
    }
}
