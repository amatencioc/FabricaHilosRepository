using FabricaHilos.Models.Sistemas;
using Oracle.ManagedDataAccess.Client;

namespace FabricaHilos.Services.Sistemas;

public class AnularDocumentoService : OracleServiceBase, IAnularDocumentoService
{
    public AnularDocumentoService(
        IConfiguration       configuration,
        IHttpContextAccessor httpContextAccessor)
        : base(configuration, httpContextAccessor) { }

    public async Task<AnularDocumentoResultDto> BuscarDocumentoAsync(
        string tipoDoc, string serie, string numero)
    {
        var result = new AnularDocumentoResultDto();

        await using var con = new OracleConnection(GetOracleConnectionString());
        await con.OpenAsync();

        // ── 1) DOCUVENT ───────────────────────────────────────────────────────
        const string sqlDocuvent =
            @"SELECT COUNT(1), MAX(ESTADO) FROM {0}DOCUVENT
               WHERE TIPODOC = :tipoDoc
                 AND SERIE   = :serie
                 AND NUMERO  = :numero";

        await using (var cmd = new OracleCommand(
            string.Format(sqlDocuvent, S), con))
        {
            cmd.Parameters.Add("tipoDoc", OracleDbType.Varchar2).Value = tipoDoc;
            cmd.Parameters.Add("serie",   OracleDbType.Varchar2).Value = serie;
            cmd.Parameters.Add("numero",  OracleDbType.Varchar2).Value = numero;

            await using var rdr = await cmd.ExecuteReaderAsync();
            if (await rdr.ReadAsync())
            {
                result.ExisteDocumento  = Convert.ToInt32(rdr[0]) > 0;
                result.EstadoDocumento  = rdr[1] == DBNull.Value ? null : rdr[1]?.ToString()?.Trim();
            }
        }

        if (!result.ExisteDocumento)
            return result;

        // ── 2) MOVGLOS ────────────────────────────────────────────────────────
        const string sqlMovGlos =
            @"SELECT ANO, MES, LIBRO, VOUCHER, ESTADO
                FROM {0}MOVGLOS
               WHERE TIPO_REFERENCIA = :tipoDoc
                 AND SERIE           = :serie
                 AND NRO_REFERENCIA  = :numero
                 AND ROWNUM = 1";

        await using (var cmd = new OracleCommand(
            string.Format(sqlMovGlos, S), con))
        {
            cmd.Parameters.Add("tipoDoc", OracleDbType.Varchar2).Value = tipoDoc;
            cmd.Parameters.Add("serie",   OracleDbType.Varchar2).Value = serie;
            cmd.Parameters.Add("numero",  OracleDbType.Varchar2).Value = numero;

            await using var rdr = await cmd.ExecuteReaderAsync();
            if (await rdr.ReadAsync())
            {
                result.ExisteMovGlos  = true;
                result.Ano           = rdr["ANO"]?.ToString();
                result.Mes           = rdr["MES"]?.ToString();
                result.Libro         = rdr["LIBRO"]?.ToString();
                result.Voucher       = rdr["VOUCHER"]?.ToString();
                result.EstadoMovGlos = rdr["ESTADO"] == DBNull.Value ? null : rdr["ESTADO"]?.ToString()?.Trim();
            }
        }

        if (!result.ExisteMovGlos)
            return result;

        // ── 3) NRODOC ─────────────────────────────────────────────────────────
        const string sqlNroDoc =
            @"SELECT NUMERO FROM {0}NRODOC
               WHERE TIPODOC = :tipoDoc
                 AND SERIE   = :serie
                 AND ROWNUM  = 1";

        await using (var cmd = new OracleCommand(
            string.Format(sqlNroDoc, S), con))
        {
            cmd.Parameters.Add("tipoDoc", OracleDbType.Varchar2).Value = tipoDoc;
            cmd.Parameters.Add("serie",   OracleDbType.Varchar2).Value = serie;

            var val = await cmd.ExecuteScalarAsync();
            if (val != null && val != DBNull.Value)
            {
                result.ExisteNroDoc = true;
                result.NroDoc       = val.ToString();
            }
        }

        // ── 4) NROLIBR ────────────────────────────────────────────────────────
        const string sqlNroLibr =
            @"SELECT NUMERO FROM {0}NROLIBR
               WHERE ANO   = :ano
                 AND MES   = :mes
                 AND LIBRO = :libro
                 AND ROWNUM = 1";

        await using (var cmd = new OracleCommand(
            string.Format(sqlNroLibr, S), con))
        {
            cmd.Parameters.Add("ano",   OracleDbType.Varchar2).Value = result.Ano;
            cmd.Parameters.Add("mes",   OracleDbType.Varchar2).Value = result.Mes;
            cmd.Parameters.Add("libro", OracleDbType.Varchar2).Value = result.Libro;

            var val = await cmd.ExecuteScalarAsync();
            if (val != null && val != DBNull.Value)
            {
                result.ExisteNroLibr = true;
                result.NroLibr       = val.ToString();
            }
        }

        return result;
    }

    public async Task<RestablecerResultDto> RestablecerFacturaAsync(
        string tipoDoc, string serie, string numeroAnterior)
    {
        var result = new RestablecerResultDto();

        await using var con = new OracleConnection(GetOracleConnectionString());
        await con.OpenAsync();

        // ── Guardar numeroActual de NRODOC antes de sobreescribir ──────────────
        const string sqlNroDocActual =
            @"SELECT NUMERO FROM {0}NRODOC
               WHERE TIPODOC = :tipoDoc
                 AND SERIE   = :serie
                 AND ROWNUM  = 1";

        string? numeroActual = null;
        await using (var cmd = new OracleCommand(string.Format(sqlNroDocActual, S), con))
        {
            cmd.Parameters.Add("tipoDoc", OracleDbType.Varchar2).Value = tipoDoc;
            cmd.Parameters.Add("serie",   OracleDbType.Varchar2).Value = serie;
            var val = await cmd.ExecuteScalarAsync();
            numeroActual = val == DBNull.Value ? null : val?.ToString();
        }

        // ── UPDATE NRODOC ────────────────────────────────────────────────────
        const string sqlUpdate =
            @"UPDATE {0}NRODOC
                 SET NUMERO  = :numeroAnterior
               WHERE TIPODOC = :tipoDoc
                 AND SERIE   = :serie";

        await using (var cmd = new OracleCommand(string.Format(sqlUpdate, S), con))
        {
            cmd.Parameters.Add("numeroAnterior", OracleDbType.Varchar2).Value = numeroAnterior;
            cmd.Parameters.Add("tipoDoc",        OracleDbType.Varchar2).Value = tipoDoc;
            cmd.Parameters.Add("serie",          OracleDbType.Varchar2).Value = serie;

            result.Filas = await cmd.ExecuteNonQueryAsync();
            result.Ok    = result.Filas > 0;
        }

        return result;
    }
}
