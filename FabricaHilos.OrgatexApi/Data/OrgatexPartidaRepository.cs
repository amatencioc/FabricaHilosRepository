namespace FabricaHilos.OrgatexApi.Data;

using System.Data;
using System.Globalization;
using FabricaHilos.OrgatexApi.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

/// <summary>
/// Lee la partida (cabecera + detalle) desde ORGATEX (SQL Server) ejecutando
/// directamente los queries (sin procedimiento almacenado) y registra los datos
/// en Oracle (SIG.ING_RECETAS_G/D y SIG.PARTIDA_MAS) vía PKG_ORGATEX.
/// </summary>
public sealed class OrgatexPartidaRepository : IOrgatexPartidaRepository
{
    private readonly string _orgatexConnStr;
    private readonly string _oracleConnStr;
    private readonly ILogger<OrgatexPartidaRepository> _logger;

    public OrgatexPartidaRepository(IConfiguration configuration, ILogger<OrgatexPartidaRepository> logger)
    {
        _orgatexConnStr = configuration.GetConnectionString("OrgatexConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:OrgatexConnection no configurada.");
        _oracleConnStr = configuration.GetConnectionString("LaColonialConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:LaColonialConnection no configurada.");
        _logger = logger;
    }

    public async Task<(PartidaCabecera? Cabecera, IReadOnlyList<PartidaDetalle> Detalle)> ObtenerDatosPartidaAsync(
        string batchRefNo, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_orgatexConnStr);
        await conn.OpenAsync(ct);

        // Cascada de 4 niveles (ver consultas_partida.sql v5.0 para el diseño completo):
        //   1) SNAPSHOT       -> RecipeSnapshot_Cabecera/_Detalle (capturado por
        //      dbo.trg_tmpProductionRecipe_Snapshot mientras el batch corría -- inmediato y completo).
        //   2) COMPLETO       -> tmpProductionRecipe en vivo (edge case, trigger aún no lo capturó).
        //   3) LEGACY_DYELOTS -> [ORGATEX-INTEG].dbo.Dyelots/Dyelot_Recipe (cross-db, retraso ETL,
        //      solo para batches viejos anteriores al despliegue del trigger).
        //   4) PARCIAL_SOLO_COLOR -> otx.Item_Attribute 'Recipe Part Lines' (último recurso, solo color).
        var hasSnapshot = await ExisteAsync(conn, "SELECT 1 FROM dbo.RecipeSnapshot_Cabecera WHERE DyelotRefNo = @BatchRefNo", batchRefNo, ct);
        var hasTmp = await ExisteAsync(conn, "SELECT 1 FROM dbo.tmpProductionRecipe WHERE DyelotRefNo = @BatchRefNo", batchRefNo, ct);
        var hasDyelots = await ExisteAsync(conn, "SELECT 1 FROM [ORGATEX-INTEG].dbo.Dyelots WHERE DyelotRefNo COLLATE DATABASE_DEFAULT = @BatchRefNo COLLATE DATABASE_DEFAULT", batchRefNo, ct);
        var hasBatchDetail = await ExisteAsync(conn, "SELECT 1 FROM dbo.BatchDetail WHERE batch_ref_no = @BatchRefNo", batchRefNo, ct);

        var fuente = hasSnapshot ? "SNAPSHOT"
                   : hasTmp      ? "COMPLETO"
                   : hasDyelots  ? "LEGACY_DYELOTS"
                                 : "PARCIAL_SOLO_COLOR";

        // 1) Cabecera ---------------------------------------------------------------
        PartidaCabecera? cabecera = null;

        if (hasSnapshot)
        {
            const string cabeceraSnapshotSQL = @"
                SELECT
                    DyelotRefNo         AS NoRefPartida,
                    Partida, Maquina, NombreMaquina, RecetaNo, RecetaDesc,
                    ColorNo, ColorNombre, Cliente, Calidad, CalidadDescription,
                    PesoLoteKg, RelacionBanioLxKg, CantidadAguaL,
                    Queued AS queued, Loaded AS loaded, Started AS started, Terminated AS terminated,
                    'SNAPSHOT' AS FuenteDetalle
                FROM dbo.RecipeSnapshot_Cabecera
                WHERE DyelotRefNo = @BatchRefNo";
            cabecera = await LeerCabeceraAsync(conn, cabeceraSnapshotSQL, batchRefNo, ct);
        }
        else if (hasBatchDetail)
        {
            var cabeceraSQL = @"
                SELECT
                    bd.batch_ref_no                                AS NoRefPartida,
                    bd.batch_text_01                                AS Partida,
                    bd.machine_no                                   AS Maquina,
                    m.name                                          AS NombreMaquina,
                    bd.RecipeID                                     AS RecipeIdOrgatex,
                    ri.Name                                         AS RecetaNo,
                    ri.Description                                  AS RecetaDesc,
                    ct.Name                                         AS ColorNo,
                    ct.Description                                  AS ColorNombre,
                    cu.Name                                         AS Cliente,
                    ql.Name                                         AS Calidad,
                    ql.Description                                  AS CalidadDescription,
                    bd.batch_parameter_01                           AS PesoLoteKg,
                    bd.batch_parameter_03                           AS RelacionBanioLxKg,
                    bd.batch_parameter_01 * bd.batch_parameter_03   AS CantidadAguaL,
                    bd.queued, bd.loaded, bd.started, bd.terminated,
                    @Fuente                                         AS FuenteDetalle
                FROM BatchDetail bd
                LEFT JOIN Machine m ON m.machine_no = bd.machine_no
                OUTER APPLY (SELECT TOP 1 RowGuid FROM otx.Item WHERE Id = bd.RecipeID AND Category_Id = 'PROCESS_RECIPE') ri_g
                LEFT JOIN otx.Item ri ON ri.RowGuid = ri_g.RowGuid
                OUTER APPLY (SELECT TOP 1 ia.Link_Item_RowGuid AS g FROM otx.Item_Attribute ia JOIN otx.Attribute a ON a.Id = ia.Attribute_Id WHERE ia.Item_RowGuid = ri.RowGuid AND a.Name = 'ColorType') ct_g
                LEFT JOIN otx.Item ct ON ct.RowGuid = ct_g.g
                OUTER APPLY (SELECT TOP 1 ia.Link_Item_RowGuid AS g FROM otx.Item_Attribute ia JOIN otx.Attribute a ON a.Id = ia.Attribute_Id WHERE ia.Item_RowGuid = ri.RowGuid AND a.Name = 'Customer') cu_g
                LEFT JOIN otx.Item cu ON cu.RowGuid = cu_g.g
                OUTER APPLY (SELECT TOP 1 ia.Link_Item_RowGuid AS g FROM otx.Item_Attribute ia JOIN otx.Attribute a ON a.Id = ia.Attribute_Id WHERE ia.Item_RowGuid = ri.RowGuid AND a.Name = 'Quality') ql_g
                LEFT JOIN otx.Item ql ON ql.RowGuid = ql_g.g
                WHERE bd.batch_ref_no = @BatchRefNo
            ";
            cabecera = await LeerCabeceraAsync(conn, cabeceraSQL, batchRefNo, ct, fuente);
        }
        else if (hasDyelots)
        {
            const string cabeceraDyelotsSQL = @"
                SELECT
                    dy.DyelotRefNo COLLATE DATABASE_DEFAULT   AS NoRefPartida,
                    dy.Dyelot                                  AS Partida,
                    dy.machine                                 AS Maquina,
                    m.name                                     AS NombreMaquina,
                    CAST(NULL AS int)                          AS RecipeIdOrgatex,
                    dy.recipeno                                AS RecetaNo,
                    CAST(NULL AS nvarchar(255))                AS RecetaDesc,
                    dy.ColourNo                                 AS ColorNo,
                    CAST(NULL AS nvarchar(255))                AS ColorNombre,
                    CAST(NULL AS nvarchar(255))                AS Cliente,
                    CAST(NULL AS nvarchar(255))                AS Calidad,
                    CAST(NULL AS nvarchar(255))                AS CalidadDescription,
                    dy.Weight                                   AS PesoLoteKg,
                    CAST(NULL AS float)                        AS RelacionBanioLxKg,
                    CAST(NULL AS float)                        AS CantidadAguaL,
                    CAST(NULL AS datetime)                     AS queued,
                    CAST(NULL AS datetime)                     AS loaded,
                    CAST(NULL AS datetime)                     AS started,
                    dy.Endtime                                  AS terminated,
                    'LEGACY_DYELOTS'                            AS FuenteDetalle
                FROM [ORGATEX-INTEG].dbo.Dyelots dy
                LEFT JOIN Machine m ON m.machine_no = dy.machine COLLATE DATABASE_DEFAULT
                WHERE dy.DyelotRefNo COLLATE DATABASE_DEFAULT = @BatchRefNo COLLATE DATABASE_DEFAULT";
            cabecera = await LeerCabeceraAsync(conn, cabeceraDyelotsSQL, batchRefNo, ct);
        }
        // si ninguna de las 3 fuentes tiene el batch, cabecera queda null (comportamiento previo).

        // RecipeIdOrgatex (usado como P_COD_RECETA en Oracle) no viene poblado en las ramas
        // SNAPSHOT/LEGACY_DYELOTS -- se completa desde BatchDetail si el batch aún está ahí.
        if (cabecera is not null && cabecera.RecipeIdOrgatex is null && hasBatchDetail)
        {
            const string recipeIdSQL = "SELECT RecipeID FROM BatchDetail WHERE batch_ref_no = @BatchRefNo";
            await using var recipeIdCmd = new SqlCommand(recipeIdSQL, conn) { CommandTimeout = 60 };
            recipeIdCmd.Parameters.Add(new SqlParameter("@BatchRefNo", SqlDbType.NVarChar, 20) { Value = batchRefNo });
            var recipeIdValue = await recipeIdCmd.ExecuteScalarAsync(ct);
            if (recipeIdValue is not null and not DBNull)
            {
                cabecera.RecipeIdOrgatex = Convert.ToInt32(recipeIdValue);
            }
        }

        // 2) Detalle real (producto + dosis) -----------------------------------------
        var detalle = new List<PartidaDetalle>();
        if (cabecera is not null)
        {
            string detalleSQL;

            if (hasSnapshot)
            {
                detalleSQL = @"
                    SELECT
                        CallOff        AS Llamada,
                        RecipePos      AS Pos,
                        ProductCode,
                        Descripcion,
                        Tipo,
                        CantidadG,
                        Unit,
                        Modo,
                        'SNAPSHOT'     AS Fuente
                    FROM dbo.RecipeSnapshot_Detalle
                    WHERE DyelotRefNo = @BatchRefNo
                    ORDER BY CallOff, RecipePos
                ";
            }
            else if (hasTmp)
            {
                detalleSQL = @"
                    SELECT
                        t.CallOff                                        AS Llamada,
                        t.RecipePos                                      AS Pos,
                        t.Number                                         AS ProductCode,
                        t.Name                                           AS Descripcion,
                        ISNULL(i.Category_Id, 'OTRO')                    AS Tipo,
                        t.Amount * 1000                                  AS CantidadG,
                        'g'                                              AS Unit,
                        CASE t.KindOfStation WHEN 2 THEN 'AUTO' WHEN 5 THEN 'MAN' ELSE '' END AS Modo,
                        'COMPLETO'                                       AS Fuente
                    FROM tmpProductionRecipe t
                    LEFT JOIN otx.Item i ON i.Name COLLATE DATABASE_DEFAULT = t.Number COLLATE DATABASE_DEFAULT
                                         AND i.Category_Id IN ('CHEMICAL','DYESTUFF')
                    WHERE t.DyelotRefNo = @BatchRefNo
                      AND t.RecipePosType IN (1,2)
                    ORDER BY t.RecipePos, t.[LineNo]
                ";
            }
            else if (hasDyelots)
            {
                detalleSQL = @"
                    SELECT
                        dr.CallOff                                       AS Llamada,
                        dr.Counter                                       AS Pos,
                        dr.ProductShortName                              AS ProductCode,
                        dr.ProductName                                    AS Descripcion,
                        ISNULL(i.Category_Id, 'OTRO')                     AS Tipo,
                        CASE WHEN LOWER(dr.unit) = 'kg' THEN dr.Amount * 1000 ELSE dr.Amount END AS CantidadG,
                        'g'                                                AS Unit,
                        CAST(NULL AS nvarchar(10))                        AS Modo,
                        'LEGACY_DYELOTS'                                   AS Fuente
                    FROM [ORGATEX-INTEG].dbo.Dyelots dy
                    JOIN [ORGATEX-INTEG].dbo.Dyelot_Recipe dr ON dr.Dyelot = dy.Dyelot AND dr.Redye = dy.Redye
                    LEFT JOIN otx.Item i ON i.Name COLLATE DATABASE_DEFAULT = dr.ProductShortName COLLATE DATABASE_DEFAULT
                                         AND i.Category_Id IN ('CHEMICAL','DYESTUFF')
                    WHERE dy.DyelotRefNo COLLATE DATABASE_DEFAULT = @BatchRefNo COLLATE DATABASE_DEFAULT
                    ORDER BY dr.CallOff, dr.Counter
                ";
            }
            else
            {
                // Rama 4 (último recurso): Detalle color-específico persistente (via otx.Item_Attribute 'Recipe Part Lines')
                detalleSQL = @"
                    DECLARE @Weight float, @Ratio float, @RecipeID2 int;
                    SELECT @Weight = batch_parameter_01, @Ratio = batch_parameter_03, @RecipeID2 = RecipeID
                    FROM BatchDetail WHERE batch_ref_no = @BatchRefNo;

                    DECLARE @LiquorL float = @Weight * @Ratio;
                    DECLARE @recRG uniqueidentifier = (SELECT TOP 1 RowGuid FROM otx.Item WHERE Id = @RecipeID2 AND Category_Id = 'PROCESS_RECIPE');

                    SELECT
                        CAST(NULL AS int)                                AS Llamada,
                        CAST(NULL AS int)                                AS Pos,
                        prod.Name                                        AS ProductCode,
                        prod.Description                                  AS Descripcion,
                        prod.Category_Id                                  AS Tipo,
                        CASE prod.Category_Id
                            WHEN 'DYESTUFF' THEN CAST(pl.Value AS float) / 100.0 * @Weight * 1000
                            ELSE                 CAST(pl.Value AS float) * @LiquorL
                        END                                                AS CantidadG,
                        'g'                                                AS Unit,
                        CAST(NULL AS nvarchar(10))                        AS Modo,
                        'PARCIAL_SOLO_COLOR'                              AS Fuente
                    FROM otx.Item_Attribute rpl
                    JOIN otx.Attribute aRecipePart ON aRecipePart.Id = rpl.Attribute_Id AND aRecipePart.Name = 'Recipe Part Lines'
                    JOIN otx.Item recipePart ON recipePart.RowGuid = rpl.Link_Item_RowGuid
                    JOIN otx.Item_Attribute pl ON pl.Item_RowGuid = recipePart.RowGuid
                    JOIN otx.Attribute aQuantity ON aQuantity.Id = pl.Attribute_Id AND aQuantity.Name = 'Recipe Part Lines'
                    JOIN otx.Item prod ON prod.RowGuid = pl.Link_Item_RowGuid
                    WHERE rpl.Item_RowGuid = @recRG
                    ORDER BY prod.Category_Id, prod.Name
                ";
            }

            await using var detalleCmd = new SqlCommand(detalleSQL, conn) { CommandTimeout = 60 };
            detalleCmd.Parameters.Add(new SqlParameter("@BatchRefNo", SqlDbType.NVarChar, 20) { Value = batchRefNo });

            await using var reader = await detalleCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                detalle.Add(new PartidaDetalle
                {
                    Llamada     = ReadIntN(reader, "Llamada"),
                    Pos         = ReadIntN(reader, "Pos"),
                    ProductCode = ReadStringN(reader, "ProductCode"),
                    Descripcion = ReadStringN(reader, "Descripcion"),
                    Tipo        = ReadStringN(reader, "Tipo"),
                    CantidadG   = ReadDecimalN(reader, "CantidadG"),
                    Unit        = ReadStringN(reader, "Unit"),
                    Modo        = ReadStringN(reader, "Modo"),
                    Fuente      = ReadStringN(reader, "Fuente"),
                });
            }
        }

        return (cabecera, detalle);
    }

    private static async Task<bool> ExisteAsync(SqlConnection conn, string existsSelectSql, string batchRefNo, CancellationToken ct)
    {
        var sql = $"SELECT CASE WHEN EXISTS ({existsSelectSql}) THEN 1 ELSE 0 END";
        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
        cmd.Parameters.Add(new SqlParameter("@BatchRefNo", SqlDbType.NVarChar, 20) { Value = batchRefNo });
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int i && i == 1;
    }

    private static async Task<PartidaCabecera?> LeerCabeceraAsync(
        SqlConnection conn, string cabeceraSQL, string batchRefNo, CancellationToken ct, string? fuenteParam = null)
    {
        await using var cabeceraCmd = new SqlCommand(cabeceraSQL, conn) { CommandTimeout = 60 };
        cabeceraCmd.Parameters.Add(new SqlParameter("@BatchRefNo", SqlDbType.NVarChar, 20) { Value = batchRefNo });
        if (fuenteParam is not null)
        {
            cabeceraCmd.Parameters.Add(new SqlParameter("@Fuente", SqlDbType.NVarChar, 30) { Value = fuenteParam });
        }

        await using var reader = await cabeceraCmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return new PartidaCabecera
        {
            NoRefPartida       = ReadStringN(reader, "NoRefPartida"),
            Partida            = ReadStringN(reader, "Partida"),
            Maquina            = ReadStringN(reader, "Maquina"),
            NombreMaquina      = ReadStringN(reader, "NombreMaquina"),
            RecipeIdOrgatex    = ReadIntN(reader, "RecipeIdOrgatex"),
            RecetaNo           = ReadStringN(reader, "RecetaNo"),
            RecetaDesc         = ReadStringN(reader, "RecetaDesc"),
            ColorNo            = ReadStringN(reader, "ColorNo"),
            ColorNombre        = ReadStringN(reader, "ColorNombre"),
            Cliente            = ReadStringN(reader, "Cliente"),
            Calidad            = ReadStringN(reader, "Calidad"),
            CalidadDescription = ReadStringN(reader, "CalidadDescription"),
            PesoLoteKg         = ReadDecimalN(reader, "PesoLoteKg"),
            RelacionBanioLxKg  = ReadDecimalN(reader, "RelacionBanioLxKg"),
            CantidadAguaL      = ReadDecimalN(reader, "CantidadAguaL"),
            Queued             = ReadDateTimeN(reader, "queued"),
            Loaded             = ReadDateTimeN(reader, "loaded"),
            Started            = ReadDateTimeN(reader, "started"),
            Terminated         = ReadDateTimeN(reader, "terminated"),
            FuenteDetalle      = ReadStringN(reader, "FuenteDetalle"),
        };
    }

    public async Task<(int Codigo, string? Mensaje)> MergeIngRecetaAsync(
        string numero, PartidaCabecera cabecera, PartidaDetalle detalle, int proceso, int item, CancellationToken ct)
    {
        var conn = new OracleConnection(_oracleConnStr);
        await using (conn.ConfigureAwait(false))
        {
            await conn.OpenAsync(ct);

            using var cmd = new OracleCommand("PKG_ORGATEX.SP_MERGE_ING_RECETA", conn)
            {
                CommandType = CommandType.StoredProcedure,
            };

            // Fecha/hora del batch: terminated, o started si terminated es NULL (ver pkg_orgatex.sql).
            var fechaHora = cabecera.Terminated ?? cabecera.Started;

            // Truncar a los tamaños reales de columna (VARCHAR2) en SIG.ING_RECETAS_G/D
            // para evitar ORA-12899 cuando el dato de OrgaTex viene más largo.
            // COD_RECETA usa RecipeID (numérico, estable) y NO RecetaNo/Item.Name: ese
            // campo puede venir con texto libre pegado por el operador de OrgaTex (ej.
            // batch 020006/RecipeID 848: Name='88987-6 SECONDAIRE FOUNE ZAM', 28 chars).
            AddParam(cmd, "P_NUMERO",      OracleDbType.Varchar2, numero);
            AddParam(cmd, "P_MAQUINA",     OracleDbType.Varchar2, Trunc(cabecera.Maquina, 4));
            AddParam(cmd, "P_COD_RECETA",  OracleDbType.Varchar2, Trunc(cabecera.RecipeIdOrgatex?.ToString(CultureInfo.InvariantCulture), 8));
            AddParam(cmd, "P_PESO_NETO",   OracleDbType.Decimal,  cabecera.PesoLoteKg);
            AddParam(cmd, "P_OBSERVACION", OracleDbType.Varchar2, Trunc(cabecera.Partida, 120));
            AddParam(cmd, "P_FECHA",       OracleDbType.Date,     fechaHora);
            AddParam(cmd, "P_HORA",        OracleDbType.Varchar2, Trunc(fechaHora?.ToString("HHmmss", CultureInfo.InvariantCulture), 6));
            AddParam(cmd, "P_PROCESO",     OracleDbType.Int32,    proceso);
            AddParam(cmd, "P_ITEM",        OracleDbType.Int32,    item);
            AddParam(cmd, "P_COD_ART",     OracleDbType.Varchar2, Trunc(detalle.ProductCode, 25));
            AddParam(cmd, "P_CANTIDAD",    OracleDbType.Decimal,  detalle.CantidadG);
            AddParam(cmd, "P_UNIDAD",      OracleDbType.Varchar2, Trunc(detalle.Unit, 6));

            cmd.Parameters.Add(new OracleParameter("P_CODIGO_RESULTADO", OracleDbType.Int32) { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("P_MENSAJE_RESULTADO", OracleDbType.Varchar2, 500) { Direction = ParameterDirection.Output });

            return await OracleRetry.EjecutarAsync(async () =>
            {
                await cmd.ExecuteNonQueryAsync(ct);
                return LeerResultado(cmd);
            }, _logger, nameof(MergeIngRecetaAsync), ct);
        }
    }

    public async Task<(int Codigo, string? Mensaje)> MergePartidaMasAsync(
        string numero, string partidaOrgatex, CancellationToken ct)
    {
        var conn = new OracleConnection(_oracleConnStr);
        await using (conn.ConfigureAwait(false))
        {
            await conn.OpenAsync(ct);

            using var cmd = new OracleCommand("PKG_ORGATEX.SP_MERGE_PARTIDA_MAS", conn)
            {
                CommandType = CommandType.StoredProcedure,
            };

            AddParam(cmd, "P_NUMERO",          OracleDbType.Varchar2, numero);
            AddParam(cmd, "P_PARTIDA_ORGATEX", OracleDbType.Varchar2, partidaOrgatex);

            cmd.Parameters.Add(new OracleParameter("P_CODIGO_RESULTADO", OracleDbType.Int32) { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("P_MENSAJE_RESULTADO", OracleDbType.Varchar2, 500) { Direction = ParameterDirection.Output });

            return await OracleRetry.EjecutarAsync(async () =>
            {
                await cmd.ExecuteNonQueryAsync(ct);
                return LeerResultado(cmd);
            }, _logger, nameof(MergePartidaMasAsync), ct);
        }
    }

    private static (int Codigo, string? Mensaje) LeerResultado(OracleCommand cmd)
    {
        var pCod = cmd.Parameters["P_CODIGO_RESULTADO"];
        var pMsg = cmd.Parameters["P_MENSAJE_RESULTADO"];

        int codigo = pCod.Value switch
        {
            OracleDecimal od => (int)od.Value,
            int i            => i,
            _                => -1,
        };
        string? mensaje = pMsg.Value?.ToString();
        return (codigo, mensaje);
    }

    private static void AddParam(OracleCommand cmd, string name, OracleDbType type, object? value)
    {
        cmd.Parameters.Add(new OracleParameter(name, type)
        {
            Direction = ParameterDirection.Input,
            Value     = value ?? DBNull.Value,
        });
    }

    private static string? Trunc(string? value, int maxLen) =>
        value is null || value.Length <= maxLen ? value : value[..maxLen];

    private static string? ReadStringN(SqlDataReader r, string col)
    {
        var i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? null : r.GetString(i);
    }

    private static decimal? ReadDecimalN(SqlDataReader r, string col)
    {
        var i = r.GetOrdinal(col);
        if (r.IsDBNull(i)) return null;

        var valor = r.GetValue(i);
        return valor switch
        {
            decimal d => d,
            string s  => decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
                             ? parsed
                             : null,
            _ => Convert.ToDecimal(valor, CultureInfo.InvariantCulture),
        };
    }

    private static int? ReadIntN(SqlDataReader r, string col)
    {
        var i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? null : Convert.ToInt32(r.GetValue(i));
    }

    private static DateTime? ReadDateTimeN(SqlDataReader r, string col)
    {
        var i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? null : r.GetDateTime(i);
    }
}
