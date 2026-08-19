namespace FabricaHilos.OrgatexSync.Data;

using System.Data;
using System.Globalization;
using FabricaHilos.OrgatexSync.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

/// <summary>
/// Migra en tiempo real dbo.RecipeSnapshot_Cabecera/Detalle (ORGATEX, SQL Server)
/// hacia Oracle SIG: ING_RECETAS_G/D vía PKG_ORGATEX.SP_MERGE_ING_RECETA, y
/// PARTIDA_MAS vía PKG_ORGATEX.SP_MERGE_PARTIDA_MAS. El estado de la migración
/// (qué cabecera ya se migró, cuántas líneas OK/error, si ya se vinculó la PARTIDA)
/// se persiste en dbo.RecipeSnapshot_OracleSync -- tabla de control separada del
/// snapshot puro (ver recipe_snapshot_oracle_sync.sql). Ambos pasos son idempotentes
/// (los procedures Oracle hacen MERGE): reintentar un ciclo fallido no duplica nada.
/// Convenciones de mapeo (P_HORA "HH.mm", P_PROCESO="0" cuando no hay llamada real,
/// P_COD_RECETA = RecipeIdOrgatex numérico en vez del texto RecetaNo) tomadas de
/// sync_partida_a_oracle.ps1, la referencia real ya validada en producción -- con la
/// corrección de P_COD_RECETA documentada en pkg_orgatex.sql v3.2.
/// </summary>
public sealed class OracleMigrationRepository : IOracleMigrationRepository
{
    // Re-sincronización continua por ROWVERSION (v3.0, reemplaza el gate por
    // IngRecetaMigrado=0): dispara si la cabecera nunca se sincronizó con éxito
    // (ctrl.DyelotRefNo IS NULL o UltimoRowVerSincronizado IS NULL), o si cambió algo
    // en Cabecera/Detalle DESPUÉS del último watermark exitoso -- sin importar si ya
    // estaba "cerrada" (IngRecetaMigrado ahora es solo informativo, ver
    // MigrarIngRecetaAsync). EliminadoEnOracle=1 excluye para siempre las cabeceras
    // cuyo header ya no existe en Oracle (borrado intencional, regla de negocio del
    // usuario: no se recrea).
    private const string SqlCabecerasPendientesIngReceta = """
        SELECT c.DyelotRefNo, c.Partida, c.Maquina, c.RecipeIdOrgatex, c.PesoLoteKg,
               c.Queued, c.Loaded, c.Started, c.Terminated, ctrl.UltimoRowVerSincronizado
        FROM dbo.RecipeSnapshot_Cabecera c
        LEFT JOIN dbo.RecipeSnapshot_OracleSync ctrl ON ctrl.DyelotRefNo = c.DyelotRefNo
        WHERE EXISTS (SELECT 1 FROM dbo.RecipeSnapshot_Detalle d WHERE d.DyelotRefNo = c.DyelotRefNo)
          AND ISNULL(ctrl.EliminadoEnOracle, 0) = 0
          AND (
                ctrl.UltimoRowVerSincronizado IS NULL
             OR c.RowVer > ctrl.UltimoRowVerSincronizado
             OR EXISTS (
                    SELECT 1 FROM dbo.RecipeSnapshot_Detalle d
                    WHERE d.DyelotRefNo = c.DyelotRefNo AND d.RowVer > ctrl.UltimoRowVerSincronizado
                )
          )
        ORDER BY c.Queued;
        """;

    // Base de la consulta batch: el IN (...) se arma dinámicamente con un parámetro
    // por cada DyelotRefNo (evita SQL injection y listas de longitud variable). Trae
    // el detalle de TODAS las cabeceras pendientes del ciclo en una sola ida a SQL
    // Server, en vez de una consulta por cabecera (N conexiones/round-trips).
    private const string SqlDetalleBatchBase = """
        SELECT DyelotRefNo, CallOff, RecipePos, ProductCode, CantidadG, Unit, RecipeAmount, RecipeUnit
        FROM dbo.RecipeSnapshot_Detalle
        WHERE DyelotRefNo IN ({0})
        ORDER BY DyelotRefNo, CallOff, RecipePos;
        """;

    // v3.2: PARTIDA_MAS no tiene FK hacia ING_RECETAS_G (verificado en ALL_CONSTRAINTS),
    // así que vincular partidas no depende de que el header ya esté cerrado ni de
    // ING_RECETA. Fuente = dbo.RecipeSnapshot_CabeceraPartida (1 fila por partida
    // detectada por patrón en BatchDetail.batch_text_01..20, ver
    // RecipeSnapshotRepository.SqlMergePartidasDetectadas) -- una misma receta puede
    // aportar hasta N filas (el negocio indicó hasta 10), cada una vinculada de forma
    // independiente e idempotente.
    private const string SqlCabecerasPendientesPartida = """
        SELECT DyelotRefNo, Partida
        FROM dbo.RecipeSnapshot_CabeceraPartida
        WHERE Vinculada = 0
        ORDER BY FechaCaptura;
        """;

    private const string SqlMarcarIngReceta = """
        MERGE dbo.RecipeSnapshot_OracleSync AS tgt
        USING (SELECT @DyelotRefNo AS DyelotRefNo) AS src
        ON tgt.DyelotRefNo = src.DyelotRefNo
        WHEN MATCHED THEN UPDATE SET
            IngRecetaMigrado         = @IngRecetaMigrado,
            FechaIngRecetaMigrado    = CASE WHEN @IngRecetaMigrado = 1 THEN GETDATE() ELSE FechaIngRecetaMigrado END,
            LineasOk                 = @LineasOk,
            LineasError              = @LineasError,
            IntentosIngReceta        = IntentosIngReceta + 1,
            UltimoError              = @UltimoError,
            UltimoRowVerSincronizado = ISNULL(@UltimoRowVerSincronizado, UltimoRowVerSincronizado),
            FechaActualizacion       = GETDATE()
        WHEN NOT MATCHED THEN INSERT
            (DyelotRefNo, IngRecetaMigrado, FechaIngRecetaMigrado, LineasOk, LineasError, IntentosIngReceta, UltimoError, UltimoRowVerSincronizado, FechaActualizacion)
            VALUES
            (@DyelotRefNo, @IngRecetaMigrado, CASE WHEN @IngRecetaMigrado = 1 THEN GETDATE() ELSE NULL END, @LineasOk, @LineasError, 1, @UltimoError, @UltimoRowVerSincronizado, GETDATE());
        """;

    // v3.0: se ejecuta cuando una cabecera YA sincronizada antes (UltimoRowVerSincronizado
    // no nulo) deja de existir en Oracle -- se asume borrado/anulación intencional del
    // usuario en Oracle (regla de negocio), se marca para no volver a intentarla jamás.
    private const string SqlMarcarEliminadoEnOracle = """
        UPDATE dbo.RecipeSnapshot_OracleSync SET
            EliminadoEnOracle  = 1,
            UltimoError        = @UltimoError,
            FechaActualizacion = GETDATE()
        WHERE DyelotRefNo = @DyelotRefNo;
        """;

    private const string SqlMarcarPartida = """
        UPDATE dbo.RecipeSnapshot_CabeceraPartida SET
            Vinculada      = @Vinculada,
            FechaVinculada = CASE WHEN @Vinculada = 1 THEN GETDATE() ELSE FechaVinculada END,
            Intentos       = Intentos + 1,
            UltimoError    = @UltimoError
        WHERE DyelotRefNo = @DyelotRefNo AND Partida = @Partida;
        """;

    private readonly string _sqlServerConnStr;
    private readonly string _oracleConnStr;
    private readonly ILogger<OracleMigrationRepository> _logger;

    public OracleMigrationRepository(IConfiguration configuration, ILogger<OracleMigrationRepository> logger)
    {
        _sqlServerConnStr = configuration.GetConnectionString("OrgatexLiveConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:OrgatexLiveConnection no configurada.");
        _oracleConnStr = configuration.GetConnectionString("LaColonialConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:LaColonialConnection no configurada.");
        _logger = logger;
    }

    public async Task<(IReadOnlyList<RecipeCabeceraPendiente> Cabeceras, byte[] Watermark)> ObtenerCabecerasPendientesIngRecetaAsync(CancellationToken ct)
    {
        var lista = new List<RecipeCabeceraPendiente>();

        await using var conn = new SqlConnection(_sqlServerConnStr);
        await conn.OpenAsync(ct);

        // Watermark capturado ANTES de leer los datos a migrar (patrón estándar de
        // rowversion): cualquier escritura concurrente que ocurra DESPUÉS de este punto
        // queda con un RowVer > watermark, así que el próximo ciclo la vuelve a ver como
        // pendiente aunque este ciclo ya haya "cerrado" con éxito -- no se pierde nada.
        byte[] watermark;
        await using (var cmdWatermark = new SqlCommand("SELECT MIN_ACTIVE_ROWVERSION();", conn) { CommandTimeout = 10 })
        {
            watermark = (byte[])(await cmdWatermark.ExecuteScalarAsync(ct))!;
        }

        await using var cmd = new SqlCommand(SqlCabecerasPendientesIngReceta, conn) { CommandTimeout = 30 };

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            lista.Add(LeerCabecera(reader));
        }

        return (lista, watermark);
    }

    public async Task<IReadOnlyList<PartidaCandidata>> ObtenerCabecerasPendientesPartidaAsync(CancellationToken ct)
    {
        var lista = new List<PartidaCandidata>();

        await using var conn = new SqlConnection(_sqlServerConnStr);
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SqlCabecerasPendientesPartida, conn) { CommandTimeout = 30 };

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            lista.Add(new PartidaCandidata
            {
                DyelotRefNo = reader.GetString(reader.GetOrdinal("DyelotRefNo")),
                Partida     = reader.GetString(reader.GetOrdinal("Partida")),
            });
        }

        return lista;
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<RecipeDetalleLinea>>> ObtenerDetallesBatchAsync(
        IReadOnlyList<string> dyelotRefNos, CancellationToken ct)
    {
        var resultado = new Dictionary<string, IReadOnlyList<RecipeDetalleLinea>>();
        if (dyelotRefNos.Count == 0)
        {
            return resultado;
        }

        await using var conn = new SqlConnection(_sqlServerConnStr);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 30;

        var nombresParam = new string[dyelotRefNos.Count];
        for (int i = 0; i < dyelotRefNos.Count; i++)
        {
            var nombre = $"@d{i}";
            nombresParam[i] = nombre;
            cmd.Parameters.AddWithValue(nombre, dyelotRefNos[i]);
        }
        cmd.CommandText = string.Format(SqlDetalleBatchBase, string.Join(", ", nombresParam));

        var agrupado = new Dictionary<string, List<RecipeDetalleLinea>>();
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var dyelot = reader.GetString(reader.GetOrdinal("DyelotRefNo"));
                var linea = new RecipeDetalleLinea
                {
                    CallOff      = reader.GetInt16(reader.GetOrdinal("CallOff")),
                    RecipePos    = reader.GetInt16(reader.GetOrdinal("RecipePos")),
                    ProductCode  = ReadStringN(reader, "ProductCode"),
                    CantidadG    = ReadDoubleN(reader, "CantidadG"),
                    Unit         = ReadStringN(reader, "Unit"),
                    RecipeAmount = ReadDoubleN(reader, "RecipeAmount"),
                    RecipeUnit   = ReadStringN(reader, "RecipeUnit"),
                };

                if (!agrupado.TryGetValue(dyelot, out var lineas))
                {
                    lineas = [];
                    agrupado[dyelot] = lineas;
                }
                lineas.Add(linea);
            }
        }

        foreach (var (dyelot, lineas) in agrupado)
        {
            resultado[dyelot] = lineas;
        }

        return resultado;
    }

    public async Task<(int Ok, int Fail)> MigrarIngRecetaAsync(
        RecipeCabeceraPendiente cabecera, IReadOnlyList<RecipeDetalleLinea> detalle, byte[] watermark, int ventanaGraciaSegundos, CancellationToken ct)
    {
        if (detalle.Count == 0)
        {
            // Aún no llegó ninguna línea a RecipeSnapshot_Detalle para este DyelotRefNo
            // (posible carrera entre RecipeSnapshotWorker y este worker); no se marca
            // como migrado para que el próximo ciclo lo vuelva a intentar.
            _logger.LogWarning(
                "[ORACLE-MIGRATION] {Dyelot}: sin líneas en RecipeSnapshot_Detalle todavía, se reintenta en el próximo ciclo.",
                cabecera.DyelotRefNo);
            return (0, 0);
        }

        int ok = 0, fail = 0;
        string? ultimoError = null;
        int numero = int.Parse(cabecera.DyelotRefNo, CultureInfo.InvariantCulture);

        var conn = new OracleConnection(_oracleConnStr);
        await using (conn.ConfigureAwait(false))
        {
            await conn.OpenAsync(ct);
            conn.AutoCommit = true;

            // v3.0 -- regla de negocio: si esta cabecera YA se había sincronizado con
            // éxito antes (UltimoRowVerSincronizado no nulo) y ahora vuelve a aparecer
            // como pendiente (rowversion avanzó), verificar PRIMERO que el header siga
            // existiendo en Oracle antes de reintentar el MERGE. Si ya no existe, se
            // asume borrado/anulación intencional por un usuario en Oracle: no se
            // recrea, se marca EliminadoEnOracle=1 y se deja de intentar para siempre.
            if (cabecera.UltimoRowVerSincronizado is not null && !await ExisteEnOracleAsync(conn, numero, ct))
            {
                _logger.LogInformation(
                    "[ORACLE-MIGRATION] {Dyelot}: ya no existe en SIG.ING_RECETAS_G (NUMERO={Numero}) pese a haberse sincronizado antes -- " +
                    "se asume borrado/anulación intencional en Oracle, no se recrea. Se marca EliminadoEnOracle=1.",
                    cabecera.DyelotRefNo, numero);
                await MarcarEliminadoEnOracleAsync(cabecera.DyelotRefNo, ct);
                return (0, 0);
            }

            using var cmd = CrearComandoIngReceta(conn);

            async Task Reconectar()
            {
                if (conn.State != ConnectionState.Open)
                {
                    try { conn.Close(); } catch { /* ya estaba rota */ }
                    await conn.OpenAsync(ct);
                    conn.AutoCommit = true;
                }
            }

            foreach (var linea in detalle)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var (codigo, mensaje) = await OracleRetry.EjecutarAsync(
                        () => MergeIngRecetaAsyncCore(cmd, numero, cabecera, linea, ct),
                        _logger, nameof(MigrarIngRecetaAsync), ct, Reconectar);

                    if (codigo == 0)
                    {
                        ok++;
                    }
                    else
                    {
                        fail++;
                        ultimoError = $"{codigo} - {mensaje}";
                        _logger.LogWarning(
                            "[ORACLE-MIGRATION] SP_MERGE_ING_RECETA falló para {Dyelot} CallOff={CallOff} RecipePos={Pos}: {Cod} - {Msg}",
                            cabecera.DyelotRefNo, linea.CallOff, linea.RecipePos, codigo, mensaje);
                    }
                }
                catch (Exception ex)
                {
                    fail++;
                    ultimoError = ex.Message;
                    _logger.LogError(ex,
                        "[ORACLE-MIGRATION] Excepción en SP_MERGE_ING_RECETA para {Dyelot} CallOff={CallOff} RecipePos={Pos}.",
                        cabecera.DyelotRefNo, linea.CallOff, linea.RecipePos);
                }
            }
        }

        // IngRecetaMigrado ahora es solo INFORMATIVO (v3.0) -- "el ciclo de esta receta
        // ya cerró" para reporting, no gatea nada. El gate real de re-sincronización es
        // el watermark (UltimoRowVerSincronizado), que solo avanza cuando fail==0 (igual
        // criterio que antes) -- si algo falló, se deja el watermark anterior para que
        // el próximo ciclo la reintente de nuevo sin necesidad de una edición nueva.
        bool puedeCerrar = fail == 0
            && cabecera.Terminated is DateTime terminado
            && terminado <= DateTime.Now.AddSeconds(-ventanaGraciaSegundos);

        byte[]? watermarkAGuardar = fail == 0 ? watermark : null;

        await MarcarIngRecetaAsync(cabecera.DyelotRefNo, puedeCerrar, ok, fail, ultimoError, watermarkAGuardar, ct);

        return (ok, fail);
    }


    public async Task<bool> VincularPartidaAsync(PartidaCandidata candidata, CancellationToken ct)
    {
        var conn = new OracleConnection(_oracleConnStr);
        await using (conn.ConfigureAwait(false))
        {
            await conn.OpenAsync(ct);
            conn.AutoCommit = true;

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PKG_ORGATEX.SP_MERGE_PARTIDA_MAS";
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.BindByName = true;
            cmd.CommandTimeout = 60;

            AddParam(cmd, "P_NUMERO", OracleDbType.Int32, int.Parse(candidata.DyelotRefNo, CultureInfo.InvariantCulture));
            AddParam(cmd, "P_PARTIDA_ORGATEX", OracleDbType.Varchar2, candidata.Partida);
            cmd.Parameters.Add(new OracleParameter("P_CODIGO_RESULTADO", OracleDbType.Int32) { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("P_MENSAJE_RESULTADO", OracleDbType.Varchar2, 500) { Direction = ParameterDirection.Output });

            try
            {
                var (codigo, mensaje) = await OracleRetry.EjecutarAsync(
                    async () =>
                    {
                        await cmd.ExecuteNonQueryAsync(ct);
                        return LeerResultado(cmd);
                    },
                    _logger, nameof(VincularPartidaAsync), ct);

                bool exito = codigo == 0;
                await MarcarPartidaAsync(candidata.DyelotRefNo, candidata.Partida, exito, exito ? null : $"{codigo} - {mensaje}", ct);

                if (!exito)
                {
                    _logger.LogWarning(
                        "[ORACLE-MIGRATION] SP_MERGE_PARTIDA_MAS falló para {Dyelot} Partida='{Partida}': {Cod} - {Msg}",
                        candidata.DyelotRefNo, candidata.Partida, codigo, mensaje);
                }

                return exito;
            }
            catch (Exception ex)
            {
                await MarcarPartidaAsync(candidata.DyelotRefNo, candidata.Partida, false, ex.Message, ct);
                _logger.LogError(ex, "[ORACLE-MIGRATION] Excepción en SP_MERGE_PARTIDA_MAS para {Dyelot} Partida='{Partida}'.", candidata.DyelotRefNo, candidata.Partida);
                return false;
            }
        }
    }

    private async Task MarcarIngRecetaAsync(string dyelotRefNo, bool migrado, int ok, int fail, string? ultimoError, byte[]? watermark, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_sqlServerConnStr);
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SqlMarcarIngReceta, conn) { CommandTimeout = 15 };
        cmd.Parameters.AddWithValue("@DyelotRefNo", dyelotRefNo);
        cmd.Parameters.AddWithValue("@IngRecetaMigrado", migrado);
        cmd.Parameters.AddWithValue("@LineasOk", ok);
        cmd.Parameters.AddWithValue("@LineasError", fail);
        cmd.Parameters.AddWithValue("@UltimoError", (object?)ultimoError ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@UltimoRowVerSincronizado", (object?)watermark ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    // v3.0: chequeo de existencia en Oracle antes de reintentar una cabecera que ya se
    // había sincronizado con éxito antes -- ver MigrarIngRecetaAsync.
    private static async Task<bool> ExisteEnOracleAsync(OracleConnection conn, int numero, CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM SIG.ING_RECETAS_G WHERE TP_TRANSAC='IR' AND SERIE=2 AND NUMERO=:numero";
        cmd.Parameters.Add(new OracleParameter("numero", OracleDbType.Int32) { Value = numero });
        var resultado = await cmd.ExecuteScalarAsync(ct);
        int count = resultado switch
        {
            OracleDecimal od => (int)od.Value,
            decimal d        => (int)d,
            int i            => i,
            _                => 0,
        };
        return count > 0;
    }

    private async Task MarcarEliminadoEnOracleAsync(string dyelotRefNo, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_sqlServerConnStr);
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SqlMarcarEliminadoEnOracle, conn) { CommandTimeout = 15 };
        cmd.Parameters.AddWithValue("@DyelotRefNo", dyelotRefNo);
        cmd.Parameters.AddWithValue("@UltimoError", "Header ya no existe en SIG.ING_RECETAS_G -- asumido borrado/anulado intencionalmente en Oracle.");

        await cmd.ExecuteNonQueryAsync(ct);
    }


    private async Task MarcarPartidaAsync(string dyelotRefNo, string partida, bool vinculada, string? ultimoError, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_sqlServerConnStr);
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SqlMarcarPartida, conn) { CommandTimeout = 15 };
        cmd.Parameters.AddWithValue("@DyelotRefNo", dyelotRefNo);
        cmd.Parameters.AddWithValue("@Partida", partida);
        cmd.Parameters.AddWithValue("@Vinculada", vinculada);
        cmd.Parameters.AddWithValue("@UltimoError", (object?)ultimoError ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static RecipeCabeceraPendiente LeerCabecera(SqlDataReader r) => new()
    {
        DyelotRefNo     = r.GetString(r.GetOrdinal("DyelotRefNo")),
        Partida         = ReadStringN(r, "Partida"),
        Maquina         = ReadStringN(r, "Maquina"),
        RecipeIdOrgatex = ReadIntN(r, "RecipeIdOrgatex"),
        PesoLoteKg      = ReadDoubleN(r, "PesoLoteKg"),
        Queued          = r.GetDateTime(r.GetOrdinal("Queued")),
        Loaded          = ReadDateTimeN(r, "Loaded"),
        Started         = ReadDateTimeN(r, "Started"),
        Terminated      = ReadDateTimeN(r, "Terminated"),
        UltimoRowVerSincronizado = ReadBytesN(r, "UltimoRowVerSincronizado"),
    };

    private static OracleCommand CrearComandoIngReceta(OracleConnection conn)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText    = "PKG_ORGATEX.SP_MERGE_ING_RECETA";
        cmd.CommandType    = CommandType.StoredProcedure;
        cmd.BindByName     = true;
        cmd.CommandTimeout = 60;

        AddParam(cmd, "P_NUMERO",      OracleDbType.Int32,    null);
        AddParam(cmd, "P_MAQUINA",     OracleDbType.Varchar2, null);
        AddParam(cmd, "P_COD_RECETA",  OracleDbType.Int32,    null);
        AddParam(cmd, "P_PESO_NETO",   OracleDbType.Decimal,  null);
        AddParam(cmd, "P_OBSERVACION", OracleDbType.Varchar2, null);
        AddParam(cmd, "P_FECHA",       OracleDbType.Date,     null);
        AddParam(cmd, "P_HORA",        OracleDbType.Varchar2, null);
        AddParam(cmd, "P_PROCESO",     OracleDbType.Varchar2, null);
        AddParam(cmd, "P_ITEM",        OracleDbType.Int32,    null);
        AddParam(cmd, "P_COD_ART",     OracleDbType.Varchar2, null);
        AddParam(cmd, "P_CANTIDAD",    OracleDbType.Decimal,  null);
        AddParam(cmd, "P_UNIDAD",      OracleDbType.Varchar2, null);
        AddParam(cmd, "P_TOTAL",       OracleDbType.Decimal,  null);

        cmd.Parameters.Add(new OracleParameter("P_CODIGO_RESULTADO", OracleDbType.Int32)
        {
            Direction = ParameterDirection.Output,
        });
        cmd.Parameters.Add(new OracleParameter("P_MENSAJE_RESULTADO", OracleDbType.Varchar2, 500)
        {
            Direction = ParameterDirection.Output,
        });

        return cmd;
    }

    private static async Task<(int Codigo, string Mensaje)> MergeIngRecetaAsyncCore(
        OracleCommand cmd, int numero, RecipeCabeceraPendiente cabecera, RecipeDetalleLinea linea, CancellationToken ct)
    {
        // P_FECHA/P_HORA: antes tomaban directo cabecera.Terminated (asumía que la
        // migración sólo corría después de terminado). Con el sync continuo, la
        // primera migración típicamente ocurre en estado Queued (Terminated todavía
        // NULL) -- se usa el hito más avanzado disponible como "fecha del evento":
        // Terminated > Started > Loaded > Queued (Queued siempre está presente desde
        // el registro). Esto hace que P_FECHA/P_HORA en Oracle avancen junto con el
        // batch y queden fijos en el momento de Terminated recién en el cierre final.
        DateTime fechaEvento = cabecera.Terminated ?? cabecera.Started ?? cabecera.Loaded ?? cabecera.Queued;

        // P_PROCESO: -1 es el centinela usado por RecipeSnapshotRepository/el trigger
        // para CallOff NULL (paso de proceso genérico sin llamada real) -- se traduce
        // a "0", igual que sync_partida_a_oracle.ps1 (constante "sin llamada real").
        SetParam(cmd, "P_NUMERO",      numero);
        SetParam(cmd, "P_MAQUINA",     cabecera.Maquina);
        SetParam(cmd, "P_COD_RECETA",  cabecera.RecipeIdOrgatex);
        SetParam(cmd, "P_PESO_NETO",   ADecimal(cabecera.PesoLoteKg));
        SetParam(cmd, "P_OBSERVACION", cabecera.Partida);
        SetParam(cmd, "P_FECHA",       fechaEvento.Date);
        SetParam(cmd, "P_HORA",        fechaEvento.ToString("HH.mm", CultureInfo.InvariantCulture));
        SetParam(cmd, "P_PROCESO",     linea.CallOff == -1 ? "0" : linea.CallOff.ToString(CultureInfo.InvariantCulture));
        SetParam(cmd, "P_ITEM",        linea.RecipePos);
        SetParam(cmd, "P_COD_ART",     linea.ProductCode);
        SetParam(cmd, "P_CANTIDAD",    ADecimal(linea.RecipeAmount));
        SetParam(cmd, "P_UNIDAD",      linea.RecipeUnit);
        SetParam(cmd, "P_TOTAL",       ADecimal(linea.CantidadG));

        await cmd.ExecuteNonQueryAsync(ct);

        return LeerResultado(cmd);
    }

    private static decimal? ADecimal(double? valor) => valor.HasValue ? (decimal)valor.Value : null;

    private static (int Codigo, string Mensaje) LeerResultado(OracleCommand cmd)
    {
        var pCod = cmd.Parameters["P_CODIGO_RESULTADO"];
        var pMsg = cmd.Parameters["P_MENSAJE_RESULTADO"];

        int codigo = pCod.Value switch
        {
            OracleDecimal od => (int)od.Value,
            int i            => i,
            _                => -1,
        };
        string mensaje = pMsg.Value?.ToString() ?? string.Empty;
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

    private static void SetParam(OracleCommand cmd, string name, object? value)
    {
        cmd.Parameters[name].Value = value ?? DBNull.Value;
    }

    private static string? ReadStringN(SqlDataReader r, string col)
    {
        var i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? null : r.GetString(i);
    }

    private static int? ReadIntN(SqlDataReader r, string col)
    {
        var i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? null : r.GetInt32(i);
    }

    private static double? ReadDoubleN(SqlDataReader r, string col)
    {
        var i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? null : r.GetDouble(i);
    }

    private static DateTime? ReadDateTimeN(SqlDataReader r, string col)
    {
        var i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? null : r.GetDateTime(i);
    }

    private static byte[]? ReadBytesN(SqlDataReader r, string col)
    {
        var i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? null : (byte[])r[i];
    }
}
