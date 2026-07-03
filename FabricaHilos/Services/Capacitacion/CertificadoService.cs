using Dapper;
using FabricaHilos.Models.Capacitacion;
using Oracle.ManagedDataAccess.Client;

namespace FabricaHilos.Services.Capacitacion;

public class CertificadoService : OracleServiceBase, ICertificadoService
{
    public CertificadoService(IConfiguration cfg, IHttpContextAccessor http)
        : base(cfg, http) { }

    public async Task<CapCertificado?> GetAsync(int idCertificado, string codUsuario)
    {
        await using var db = new OracleConnection(GetOracleConnectionString());
        return await db.QueryFirstOrDefaultAsync<CapCertificado>(
            $@"SELECT * FROM {S}CAP_CERTIFICADO
               WHERE ID_CERTIFICADO = :id AND COD_USUARIO = :usr AND ESTADO <> 'X'",
            new { id = idCertificado, usr = codUsuario });
    }

    public async Task<CapCertificado?> GetPrimeroAsync(string codUsuario)
    {
        await using var db = new OracleConnection(GetOracleConnectionString());
        return await db.QueryFirstOrDefaultAsync<CapCertificado>(
            $@"SELECT * FROM {S}CAP_CERTIFICADO
               WHERE COD_USUARIO = :usr AND ESTADO <> 'X'
               ORDER BY FCH_EMISION DESC",
            new { usr = codUsuario });
    }

    public async Task<CapCertificado?> GetByCodigoAsync(string codigoVerif)
    {
        await using var db = new OracleConnection(GetOracleConnectionString());
        return await db.QueryFirstOrDefaultAsync<CapCertificado>(
            $"SELECT * FROM {S}CAP_CERTIFICADO WHERE CODIGO_VERIF = :cod",
            new { cod = codigoVerif });
    }

    public async Task<CapCertificado?> EmitirAsync(long idIntento, long idInscripcion, string codUsuario)
    {
        await using var db = new OracleConnection(GetOracleConnectionString());
        await db.OpenAsync();

        await using var cmd = db.CreateCommand();
        cmd.CommandText =
            $"BEGIN {S}PKG_CAP.SP_CAP_EMITIR_CERT(:p_int, :p_insc, :p_usr, :p_id, :p_cod, :p_res); END;";

        cmd.Parameters.Add(new OracleParameter("p_int",  OracleDbType.Decimal)        { Value     = idIntento     });
        cmd.Parameters.Add(new OracleParameter("p_insc", OracleDbType.Decimal)        { Value     = idInscripcion });
        cmd.Parameters.Add(new OracleParameter("p_usr",  OracleDbType.Varchar2, 50)   { Value     = codUsuario    });
        cmd.Parameters.Add(new OracleParameter("p_id",   OracleDbType.Decimal)         { Direction = System.Data.ParameterDirection.Output });
        cmd.Parameters.Add(new OracleParameter("p_cod",  OracleDbType.Varchar2, 40)   { Direction = System.Data.ParameterDirection.Output });
        cmd.Parameters.Add(new OracleParameter("p_res",  OracleDbType.Varchar2, 20)   { Direction = System.Data.ParameterDirection.Output });

        await cmd.ExecuteNonQueryAsync();

        var resultado = cmd.Parameters["p_res"].Value?.ToString() ?? "ERROR";
        if (resultado is "ERROR" or "NO_APROBADO" or "NO_ENCONTRADO")
            return null;

        var idOut  = cmd.Parameters["p_id"].Value is Oracle.ManagedDataAccess.Types.OracleDecimal od && !od.IsNull
                     ? Convert.ToInt32(od.Value) : 0;
        var codigo = cmd.Parameters["p_cod"].Value?.ToString() ?? "";

        return await GetAsync(idOut, codUsuario)
            ?? new CapCertificado { IdCertificado = idOut, CodigoVerif = codigo, Estado = "V" };
    }
}

