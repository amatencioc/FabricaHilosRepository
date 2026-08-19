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
    // v3.1 (2026-08-18) -- FIX DE RAÍZ para líneas huérfanas (ver casos reales 022230 y
    // 022375 en /memories/repo/orgatex-oracle-sync-diagnosis.md): antes este MERGE solo
    // insertaba/actualizaba, nunca borraba -- si una línea se quitaba de la receta en
    // OrgaTex (desaparecía de tmpProductionRecipe), quedaba huérfana PARA SIEMPRE en
    // RecipeSnapshot_Detalle y el worker de Oracle la seguía reenviando indefinidamente
    // (o, peor, dejaba de reenviarla tras el primer watermark exitoso pero SIN haberla
    // borrado de Oracle -- ver hallazgo "silent OK" en la memoria del repo). Ahora se
    // agrega `WHEN NOT MATCHED BY SOURCE THEN DELETE`, con una condición extra que
    // restringe el borrado SOLO a DyelotRefNo actualmente presentes en
    // tmpProductionRecipe (pantalla de receta abierta AHORA MISMO en esa máquina) --
    // así se obtiene un diff completo (insert+update+delete) exacto contra la fuente
    // viva, sin el riesgo de borrar el histórico de batches con la pantalla cerrada
    // (tmpProductionRecipe es transitoria y se vacía casi al instante al cerrar --
    // ver "Intento descartado" en la memoria del repo; esta vez el scope evita ese
    // falso positivo).
    private const string SqlMergeDetalle = """
        MERGE dbo.RecipeSnapshot_Detalle AS tgt
        USING (
            SELECT LEFT(DyelotRefNo COLLATE DATABASE_DEFAULT, 20) AS DyelotRefNo, CorrectionNumber, ISNULL(CallOff, -1) AS CallOff, RecipePos,
                   LEFT(Number COLLATE DATABASE_DEFAULT, 60)  AS ProductCode,
                   LEFT(Name   COLLATE DATABASE_DEFAULT, 200) AS Descripcion,
                   CASE RecipePosType WHEN 1 THEN 'CHEMICAL' WHEN 2 THEN 'DYESTUFF' ELSE 'OTRO' END AS Tipo,
                   TRY_CONVERT(float, Amount) * 1000 AS CantidadG,
                   'g' AS Unit,
                   CASE KindOfStation WHEN 2 THEN 'AUTO' WHEN 5 THEN 'MAN' ELSE '' END AS Modo,
                   TRY_CONVERT(float, RecipeAmount) AS RecipeAmount,
                   LEFT(RecipeUnit COLLATE DATABASE_DEFAULT, 10) AS RecipeUnit
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
            RecipeAmount = src.RecipeAmount,
            RecipeUnit   = src.RecipeUnit,
            FechaCaptura = GETDATE()
        WHEN NOT MATCHED THEN INSERT
            (DyelotRefNo, CorrectionNumber, CallOff, RecipePos, ProductCode, Descripcion, Tipo, CantidadG, Unit, Modo, RecipeAmount, RecipeUnit, FechaCaptura)
            VALUES
            (src.DyelotRefNo, src.CorrectionNumber, src.CallOff, src.RecipePos, src.ProductCode, src.Descripcion, src.Tipo, src.CantidadG, src.Unit, src.Modo, src.RecipeAmount, src.RecipeUnit, GETDATE())
        WHEN NOT MATCHED BY SOURCE
             AND tgt.DyelotRefNo IN (SELECT DISTINCT DyelotRefNo COLLATE DATABASE_DEFAULT FROM dbo.tmpProductionRecipe)
             THEN DELETE;
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
                bd.RecipeID                                          AS RecipeIdOrgatex,
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
            RecipeIdOrgatex = src.RecipeIdOrgatex,
            Queued = src.queued, Loaded = src.loaded, Started = src.started, Terminated = src.terminated,
            FuenteDetalle = 'SNAPSHOT', FechaCaptura = GETDATE()
        WHEN NOT MATCHED THEN INSERT
            (DyelotRefNo, Partida, Maquina, NombreMaquina, RecetaNo, RecetaDesc, ColorNo, ColorNombre,
             Cliente, Calidad, CalidadDescription, PesoLoteKg, RelacionBanioLxKg, CantidadAguaL,
             RecipeIdOrgatex, Queued, Loaded, Started, Terminated, FuenteDetalle, FechaCaptura)
            VALUES
            (src.DyelotRefNo, src.Partida, src.Maquina, src.NombreMaquina, src.RecetaNo, src.RecetaDesc, src.ColorNo, src.ColorNombre,
             src.Cliente, src.Calidad, src.CalidadDescription, src.PesoLoteKg, src.RelacionBanioLxKg, src.CantidadAguaL,
             src.RecipeIdOrgatex, src.queued, src.loaded, src.started, src.terminated, 'SNAPSHOT', GETDATE());
        """;

    // v3.2 (2026-08-19) -- HASTA 10 PARTIDAS POR RECETA (ver hallazgo completo en el
    // header de sqlserver/recipe_snapshot_oracle_sync.sql v3.2): antes este MERGE solo
    // capturaba Partida = bd.batch_text_01, así que una receta con 2+ partidas reales
    // (caso 022424, luego confirmados hasta 6 en datos históricos) perdía todas menos
    // la primera. No hay una columna fija "partida N" -- depende del tipo de
    // máquina/plantilla -- así que se detectan candidatas por FORMATO dentro de las 20
    // columnas genéricas batch_text_01..20: exactamente 5 dígitos + guion
    // ('[0-9][0-9][0-9][0-9][0-9]-%'), el mismo prefijo de toda partida real
    // (NRO_PEDIDO-SER_PARTIDA[-NROPART[-REPROCESO]]).
    // v3.3 (2026-08-19) -- FIX DE RAÍZ para ORA-06502 en producción (visto en vivo:
    // 49 filas atascadas, ~95.000 intentos fallidos acumulados en unas horas): varias
    // columnas batch_text_XX traen "Partida + ' ' + sufijo libre" concatenado (ej.
    // "89145-7-0-10 ORANGE", "89069-1-1 BLUSH PINK", o el ya conocido
    // "89276-15-0-00 13-0822" de batch_text_07) -- el filtro de arriba solo exige que
    // el string EMPIECE con 5 dígitos + guion, pero antes se guardaba el string
    // COMPLETO (incluido el sufijo) como Partida, y ese sufijo no numérico revienta el
    // parser de SP_MERGE_PARTIDA_MAS (ORA-06502) para siempre (Fase 2 reintenta cada
    // ciclo sin poder tener éxito jamás). Ahora se extrae SOLO el token limpio
    // (dígitos y guiones desde el inicio, cortando en el primer carácter que no sea
    // dígito ni guion vía PATINDEX) en vez del string completo, y se exige que haya
    // al menos un dígito después del primer guion. Efecto colateral bueno: esto
    // también vuelve redundante (pero inofensivo, se deja como red de seguridad) el
    // filtro self-join de abajo para el caso batch_text_07, porque el token
    // resultante de "89276-15-0-00 13-0822" pasa a ser exactamente "89276-15-0-00"
    // (idéntico a la partida real ya detectada en otra columna), no un duplicado
    // derivado distinto.
    private const string SqlMergePartidasDetectadas = """
        ;WITH candidatos AS (
            SELECT DISTINCT
                LEFT(bd.batch_ref_no COLLATE DATABASE_DEFAULT, 20) AS DyelotRefNo,
                LEFT(tk.token, 100)                                 AS Partida
            FROM (SELECT DISTINCT DyelotRefNo FROM dbo.tmpProductionRecipe) di
            JOIN dbo.BatchDetail bd ON bd.batch_ref_no = di.DyelotRefNo
            CROSS APPLY (VALUES
                (bd.batch_text_01 COLLATE DATABASE_DEFAULT),(bd.batch_text_02 COLLATE DATABASE_DEFAULT),(bd.batch_text_03 COLLATE DATABASE_DEFAULT),
                (bd.batch_text_04 COLLATE DATABASE_DEFAULT),(bd.batch_text_05 COLLATE DATABASE_DEFAULT),(bd.batch_text_06 COLLATE DATABASE_DEFAULT),
                (bd.batch_text_07 COLLATE DATABASE_DEFAULT),(bd.batch_text_08 COLLATE DATABASE_DEFAULT),(bd.batch_text_09 COLLATE DATABASE_DEFAULT),
                (bd.batch_text_10 COLLATE DATABASE_DEFAULT),(bd.batch_text_11 COLLATE DATABASE_DEFAULT),(bd.batch_text_12 COLLATE DATABASE_DEFAULT),
                (bd.batch_text_13 COLLATE DATABASE_DEFAULT),(bd.batch_text_14 COLLATE DATABASE_DEFAULT),(bd.batch_text_15 COLLATE DATABASE_DEFAULT),
                (bd.batch_text_16 COLLATE DATABASE_DEFAULT),(bd.batch_text_17 COLLATE DATABASE_DEFAULT),(bd.batch_text_18 COLLATE DATABASE_DEFAULT),
                (bd.batch_text_19 COLLATE DATABASE_DEFAULT),(bd.batch_text_20 COLLATE DATABASE_DEFAULT)
            ) v(txt)
            CROSS APPLY (SELECT LTRIM(RTRIM(v.txt)) AS trimmed) t
            CROSS APPLY (SELECT CASE WHEN PATINDEX('%[^0-9-]%', t.trimmed) = 0
                                      THEN t.trimmed
                                      ELSE LEFT(t.trimmed, PATINDEX('%[^0-9-]%', t.trimmed) - 1)
                                 END AS token) tk
            WHERE t.trimmed LIKE '[0-9][0-9][0-9][0-9][0-9]-%'
              AND tk.token LIKE '[0-9][0-9][0-9][0-9][0-9]-[0-9]%'
        )
        MERGE dbo.RecipeSnapshot_CabeceraPartida AS tgt
        USING (
            SELECT c1.DyelotRefNo, c1.Partida
            FROM candidatos c1
            WHERE NOT EXISTS (
                SELECT 1 FROM candidatos c2
                WHERE c2.DyelotRefNo = c1.DyelotRefNo
                  AND c2.Partida <> c1.Partida
                  AND LEN(c1.Partida) > LEN(c2.Partida)
                  AND LEFT(c1.Partida, LEN(c2.Partida) + 1) = c2.Partida + ' '
            )
        ) AS src
        ON tgt.DyelotRefNo = src.DyelotRefNo AND tgt.Partida = src.Partida
        WHEN NOT MATCHED THEN INSERT (DyelotRefNo, Partida, FechaCaptura)
            VALUES (src.DyelotRefNo, src.Partida, GETDATE());
        """;

    // Fix del hallazgo 2026-08: OrgaTex vacía tmpProductionRecipe esencialmente al mismo
    // tiempo que graba BatchDetail.terminated, así que ningún poll ve nunca ambas
    // condiciones juntas (fila en tmp + terminated ya seteado), y el MERGE de cabecera
    // de arriba (acotado a lo presente en tmpProductionRecipe) jamás alcanza a copiar
    // Terminated. Este UPDATE es independiente de tmpProductionRecipe -- corre SIEMPRE,
    // para cualquier cabecera YA existente en RecipeSnapshot_Cabecera (entró ahí en algún
    // ciclo previo por Queued/Loaded/Started) cuyo Terminated siga NULL, copiándolo
    // directo desde BatchDetail apenas esté disponible.
    private const string SqlCerrarCabecerasTerminadas = """
        UPDATE c
        SET c.Loaded       = bd.loaded,
            c.Started      = bd.started,
            c.Terminated   = bd.terminated,
            c.FechaCaptura = GETDATE()
        FROM dbo.RecipeSnapshot_Cabecera c
        JOIN dbo.BatchDetail bd ON bd.batch_ref_no = c.DyelotRefNo COLLATE DATABASE_DEFAULT
        WHERE c.Terminated IS NULL
          AND bd.terminated IS NOT NULL;
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

    public async Task<(int FilasDetalle, int FilasCabecera, int FilasCerradas, int FilasPartidas)> SincronizarAsync(CancellationToken ct)
    {
        await using var conn = new SqlConnection(_connStr);
        await conn.OpenAsync(ct);

        int filasDetalle = 0, filasCabecera = 0, filasPartidas = 0;

        if (await HayDatosPendientesAsync(conn, ct))
        {
            // Los 3 MERGE se ejecutan en la misma transacción: si alguno falla, no
            // queremos dejar los demás ya confirmados (evita snapshots
            // parciales/inconsistentes entre Detalle, Cabecera y CabeceraPartida).
            await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(ct);

            filasDetalle = await EjecutarMergeAsync(conn, tx, SqlMergeDetalle, ct);
            filasCabecera = await EjecutarMergeAsync(conn, tx, SqlMergeCabecera, ct);
            filasPartidas = await EjecutarMergeAsync(conn, tx, SqlMergePartidasDetectadas, ct);

            await tx.CommitAsync(ct);
        }

        // Independiente de si tmpProductionRecipe tenía algo o no: siempre se intenta
        // cerrar cabeceras ya existentes cuyo batch ya haya terminado en BatchDetail.
        int filasCerradas = await EjecutarMergeAsync(conn, null, SqlCerrarCabecerasTerminadas, ct);

        return (filasDetalle, filasCabecera, filasCerradas, filasPartidas);
    }

    private static async Task<bool> HayDatosPendientesAsync(SqlConnection conn, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(SqlExisteAlgo, conn) { CommandTimeout = 10 };
        var resultado = await cmd.ExecuteScalarAsync(ct);
        return resultado is not null;
    }

    private async Task<int> EjecutarMergeAsync(SqlConnection conn, SqlTransaction? tx, string sql, CancellationToken ct)
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
