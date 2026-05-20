using System.Data;
using Oracle.ManagedDataAccess.Client;
using FabricaHilos.Models.Produccion.Planeamiento;

namespace FabricaHilos.Services.Produccion.Planeamiento;

/// <summary>
/// Servicio de lectura y actualización de PLN_PARAM (§2.1 PKG_PLN).
/// Tabla de configuración clave-valor de 9 filas que controla umbrales de alertas,
/// horas de turno y días de buffer. No requiere recompilar PL/SQL para ajustar valores.
/// </summary>
public class PlnParamService : OracleServiceBase, IPlnParamService
{
    public PlnParamService(
        IConfiguration       configuration,
        IHttpContextAccessor httpContextAccessor)
        : base(configuration, httpContextAccessor) { }

    private static string SafeStr(object? v) =>
        v == null || v == DBNull.Value ? "" : v.ToString()!;

    private static T SafeVal<T>(object? v, T def = default!)
    {
        if (v == null || v == DBNull.Value) return def;
        var underlying = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        return (T)Convert.ChangeType(v, underlying);
    }

    private static PlnParam MapRow(System.Data.Common.DbDataReader r) => new()
    {
        CodParam    = SafeStr(r["cod_param"]),
        Descripcion = SafeStr(r["descripcion"]),
        ValorNum    = r["valor_num"]  == DBNull.Value ? null : SafeVal<decimal?>(r["valor_num"]),
        ValorText   = r["valor_text"] == DBNull.Value ? null : SafeStr(r["valor_text"]),
        ValorDate   = r["valor_date"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(r["valor_date"]),
        AMduser     = r["a_mduser"]   == DBNull.Value ? null : SafeStr(r["a_mduser"]),
        AMdfecha    = r["a_mdfecha"]  == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(r["a_mdfecha"]),
    };

    public async Task<IEnumerable<PlnParam>> GetAllAsync()
    {
        var sql = $@"
            SELECT cod_param, descripcion, valor_num, valor_text, valor_date,
                   a_mduser, a_mdfecha
            FROM   {S}PLN_PARAM
            ORDER  BY cod_param";

        var list = new List<PlnParam>();
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd = new OracleCommand(sql, conn);
        await using var r   = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(MapRow(r));
        return list;
    }

    public async Task<PlnParam?> GetAsync(string codParam)
    {
        var sql = $@"
            SELECT cod_param, descripcion, valor_num, valor_text, valor_date,
                   a_mduser, a_mdfecha
            FROM   {S}PLN_PARAM
            WHERE  cod_param = :codParam";

        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        cmd.Parameters.Add(new OracleParameter("codParam", OracleDbType.Varchar2, 20)
            { Value = codParam });
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? MapRow(r) : null;
    }

    public Task UpdateAsync(string codParam, decimal valorNum, string usuario)
        => UpdateAsync(codParam, valorNum, null, null, usuario);

    public async Task UpdateAsync(
        string codParam, decimal? valorNum, string? valorText, DateTime? valorDate, string usuario)
    {
        // Actualiza solo los campos que se pasan; los NULL conservan el valor actual.
        // A_MDUSER y A_MDFECHA registran auditoría de última modificación.
        var setParts = new List<string>
        {
            "A_MDUSER  = :usuario",
            "A_MDFECHA = SYSDATE",
        };
        if (valorNum.HasValue)  setParts.Add("VALOR_NUM  = :valorNum");
        if (valorText != null)  setParts.Add("VALOR_TEXT = :valorText");
        if (valorDate.HasValue) setParts.Add("VALOR_DATE = :valorDate");

        var sql = $@"UPDATE {S}PLN_PARAM
                     SET    {string.Join(", ", setParts)}
                     WHERE  COD_PARAM = :codParam";

        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        cmd.Parameters.Add(new OracleParameter("usuario",  OracleDbType.Varchar2, 15) { Value = (object)usuario });
        cmd.Parameters.Add(new OracleParameter("codParam", OracleDbType.Varchar2, 20) { Value = (object)codParam });
        if (valorNum.HasValue)
            cmd.Parameters.Add(new OracleParameter("valorNum", OracleDbType.Decimal)    { Value = valorNum.Value });
        if (valorText != null)
            cmd.Parameters.Add(new OracleParameter("valorText", OracleDbType.Varchar2, 100) { Value = (object)valorText });
        if (valorDate.HasValue)
            cmd.Parameters.Add(new OracleParameter("valorDate", OracleDbType.Date)      { Value = valorDate.Value });

        await cmd.ExecuteNonQueryAsync();
    }
}
