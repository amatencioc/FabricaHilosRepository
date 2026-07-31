namespace FabricaHilos.OrgatexSync.Data;

using System.Data;
using FabricaHilos.OrgatexSync.Logging;
using FabricaHilos.OrgatexSync.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

/// <summary>
/// Lee recetas de tintura de ORGATEX (SQL Server, solo lectura) y las migra (MERGE)
/// a Oracle SIG.CARGA_ORGATEX vía PKG_ORGATEX.SP_MERGE_FILA.
/// No crea ni modifica ningún objeto en ORGATEX — solo ejecuta SELECT.
/// </summary>
public sealed class OrgatexRepository : IOrgatexRepository
{
    // Alias de columnas ya mapeados 1:1 a CARGA_ORGATEX (ver SQL/PKG_ORGATEX.sql).
    private const string SqlSelect = """
        SELECT da.DyelotRefNo RECETA_ORGATEX, da.Dyelot PARTIDA, da.ColourNo COD_COLOR, da.recipeno DESC_COLOR, da.machine MAQUINA,
               da.Weight PESO, dr.CallOff LLAMADA, dr.Counter CONTADOR, dr.ProductShortName COD_PRODUCTO, dr.ProductName DESCRIPCION,
               dr.Amount CANT_ORGATEX, dr.ActualAmount CANT_REAL_ORGATEX, dr.unit UNIDAD, Endtime FECHA
        FROM Dyelots da
        INNER JOIN Dyelot_Recipe dr ON dr.Dyelot = da.Dyelot AND dr.Redye = da.Redye
        WHERE Endtime BETWEEN @Desde AND @Hasta
        ORDER BY Endtime
        """;

    private readonly string _sqlServerConnStr;
    private readonly string _oracleConnStr;
    private readonly ILogger<OrgatexRepository> _logger;
    private readonly ILogger _callLogger;

    public OrgatexRepository(IConfiguration configuration, ILogger<OrgatexRepository> logger, ILoggerFactory loggerFactory)
    {
        _sqlServerConnStr = configuration.GetConnectionString("OrgatexConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:OrgatexConnection no configurada.");
        _oracleConnStr = configuration.GetConnectionString("LaColonialConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:LaColonialConnection no configurada.");
        _logger = logger;
        _callLogger = OrgatexCallLogger.Crear(loggerFactory);
    }

    public async Task<IReadOnlyList<OrgatexRow>> ObtenerRecetasAsync(DateTime desde, DateTime hasta, CancellationToken ct)
    {
        // Capacidad inicial razonable para evitar redimensionamientos repetidos del
        // List<T> (copias completas del array interno) durante lotes de miles de filas.
        var lista = new List<OrgatexRow>(1024);

        await using var conn = new SqlConnection(_sqlServerConnStr);
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SqlSelect, conn) { CommandTimeout = 120 };
        cmd.Parameters.AddWithValue("@Desde", desde);
        cmd.Parameters.AddWithValue("@Hasta", hasta);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            lista.Add(new OrgatexRow
            {
                RecetaOrgatex   = ReadDecimalN(reader, "RECETA_ORGATEX"),
                Partida         = ReadStringN(reader, "PARTIDA"),
                CodColor        = ReadStringN(reader, "COD_COLOR"),
                DescColor       = ReadStringN(reader, "DESC_COLOR"),
                Maquina         = ReadStringN(reader, "MAQUINA"),
                Peso            = ReadDecimalN(reader, "PESO"),
                Llamada         = ReadIntN(reader, "LLAMADA"),
                Contador        = ReadIntN(reader, "CONTADOR"),
                CodProducto     = ReadStringN(reader, "COD_PRODUCTO"),
                Descripcion     = ReadStringN(reader, "DESCRIPCION"),
                CantOrgatex     = ReadDecimalN(reader, "CANT_ORGATEX"),
                CantRealOrgatex = ReadDecimalN(reader, "CANT_REAL_ORGATEX"),
                Unidad          = ReadStringN(reader, "UNIDAD"),
                Fecha           = reader.GetDateTime(reader.GetOrdinal("FECHA")),
            });
        }

        return lista;
    }

    public async Task<(int Ok, int Fail)> MergeCargaOrgatexAsync(IReadOnlyList<OrgatexRow> filas, CancellationToken ct)
    {
        int ok = 0, fail = 0;

        var conn = new OracleConnection(_oracleConnStr);
        await using (conn.ConfigureAwait(false))
        {
            await conn.OpenAsync(ct);
            conn.AutoCommit = true;

            // El comando y sus parámetros se crean una sola vez y se reutilizan
            // para todas las filas del lote (evita allocations/binding repetido
            // en cada uno de los miles de round-trips diarios).
            using var cmd = CrearComandoMerge(conn);

            async Task Reconectar()
            {
                if (conn.State != ConnectionState.Open)
                {
                    try { conn.Close(); } catch { /* ya estaba rota */ }
                    await conn.OpenAsync(ct);
                    conn.AutoCommit = true;
                }
            }

            foreach (var fila in filas)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var (codigo, mensaje) = await OracleRetry.EjecutarAsync(
                        () => MergeFilaAsyncCore(cmd, fila, ct),
                        _logger, nameof(MergeCargaOrgatexAsync), ct, Reconectar);

                    if (codigo == 0)
                    {
                        ok++;
                        _callLogger.LogInformation(
                            "OK PARTIDA={Partida} LLAMADA={Llamada} CONTADOR={Contador} COD_PRODUCTO={Prod} FECHA={Fecha:yyyy-MM-dd HH:mm:ss}",
                            fila.Partida, fila.Llamada, fila.Contador, fila.CodProducto, fila.Fecha);
                    }
                    else
                    {
                        fail++;
                        _logger.LogWarning(
                            "MERGE falló para PARTIDA={Partida} LLAMADA={Llamada} CONTADOR={Contador} COD_PRODUCTO={Prod}: {Cod} - {Msg}",
                            fila.Partida, fila.Llamada, fila.Contador, fila.CodProducto, codigo, mensaje);
                        _callLogger.LogWarning(
                            "ERROR PARTIDA={Partida} LLAMADA={Llamada} CONTADOR={Contador} COD_PRODUCTO={Prod} FECHA={Fecha:yyyy-MM-dd HH:mm:ss} CODIGO={Cod} MENSAJE={Msg}",
                            fila.Partida, fila.Llamada, fila.Contador, fila.CodProducto, fila.Fecha, codigo, mensaje);
                    }
                }
                catch (Exception ex)
                {
                    fail++;
                    _logger.LogError(ex,
                        "Excepción al procesar fila PARTIDA={Partida} LLAMADA={Llamada} CONTADOR={Contador}.",
                        fila.Partida, fila.Llamada, fila.Contador);
                    _callLogger.LogError(ex,
                        "EXCEPCION PARTIDA={Partida} LLAMADA={Llamada} CONTADOR={Contador} COD_PRODUCTO={Prod} FECHA={Fecha:yyyy-MM-dd HH:mm:ss}",
                        fila.Partida, fila.Llamada, fila.Contador, fila.CodProducto, fila.Fecha);
                }
            }
        }

        return (ok, fail);
    }

    private static OracleCommand CrearComandoMerge(OracleConnection conn)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText    = "PKG_ORGATEX.SP_MERGE_FILA";
        cmd.CommandType    = CommandType.StoredProcedure;
        cmd.BindByName     = true;
        cmd.CommandTimeout = 60;

        AddParam(cmd, "P_RECETA_ORGATEX",    OracleDbType.Decimal,  null);
        AddParam(cmd, "P_PARTIDA",           OracleDbType.Varchar2, null);
        AddParam(cmd, "P_COD_COLOR",         OracleDbType.Varchar2, null);
        AddParam(cmd, "P_DESC_COLOR",        OracleDbType.Varchar2, null);
        AddParam(cmd, "P_MAQUINA",           OracleDbType.Varchar2, null);
        AddParam(cmd, "P_PESO",              OracleDbType.Decimal,  null);
        AddParam(cmd, "P_LLAMADA",           OracleDbType.Int32,    null);
        AddParam(cmd, "P_CONTADOR",          OracleDbType.Int32,    null);
        AddParam(cmd, "P_COD_PRODUCTO",      OracleDbType.Varchar2, null);
        AddParam(cmd, "P_DESCRIPCION",       OracleDbType.Varchar2, null);
        AddParam(cmd, "P_CANT_ORGATEX",      OracleDbType.Decimal,  null);
        AddParam(cmd, "P_CANT_REAL_ORGATEX", OracleDbType.Decimal,  null);
        AddParam(cmd, "P_UNIDAD",            OracleDbType.Varchar2, null);
        AddParam(cmd, "P_FECHA",             OracleDbType.Date,     null);

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

    private static async Task<(int Codigo, string Mensaje)> MergeFilaAsyncCore(OracleCommand cmd, OrgatexRow fila, CancellationToken ct)
    {
        SetParam(cmd, "P_RECETA_ORGATEX",    fila.RecetaOrgatex);
        SetParam(cmd, "P_PARTIDA",           fila.Partida);
        SetParam(cmd, "P_COD_COLOR",         fila.CodColor);
        SetParam(cmd, "P_DESC_COLOR",        fila.DescColor);
        SetParam(cmd, "P_MAQUINA",           fila.Maquina);
        SetParam(cmd, "P_PESO",              fila.Peso);
        SetParam(cmd, "P_LLAMADA",           fila.Llamada);
        SetParam(cmd, "P_CONTADOR",          fila.Contador);
        SetParam(cmd, "P_COD_PRODUCTO",      fila.CodProducto);
        SetParam(cmd, "P_DESCRIPCION",       fila.Descripcion);
        SetParam(cmd, "P_CANT_ORGATEX",      fila.CantOrgatex);
        SetParam(cmd, "P_CANT_REAL_ORGATEX", fila.CantRealOrgatex);
        SetParam(cmd, "P_UNIDAD",            fila.Unidad);
        SetParam(cmd, "P_FECHA",             fila.Fecha);

        await cmd.ExecuteNonQueryAsync(ct);

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

    private static decimal? ReadDecimalN(SqlDataReader r, string col)
    {
        var i = r.GetOrdinal(col);
        if (r.IsDBNull(i)) return null;

        // Algunas columnas de ORGATEX (p. ej. recipeno) llegan tipadas como
        // varchar/nvarchar en vez de numeric, aunque semánticamente sean números.
        // GetDecimal falla con InvalidCastException en ese caso; se usa Convert
        // con cultura invariante para soportar ambos casos de forma segura.
        var valor = r.GetValue(i);
        return valor switch
        {
            decimal d => d,
            string s  => decimal.TryParse(s, System.Globalization.NumberStyles.Number,
                             System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                             ? parsed
                             : null,
            _ => Convert.ToDecimal(valor, System.Globalization.CultureInfo.InvariantCulture),
        };
    }

    private static int? ReadIntN(SqlDataReader r, string col)
    {
        var i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? null : Convert.ToInt32(r.GetValue(i));
    }
}
