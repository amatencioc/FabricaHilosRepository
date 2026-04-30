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

    // ══════════════════════════════════════════════════════════════════════════
    //  PASO 1 — DELETE DOCUVENT
    // ══════════════════════════════════════════════════════════════════════════
    public async Task<RestablecerPasoDto> Paso1DeleteDocumentAsync(
        string tipoDoc, string serie, string numero)
    {
        try
        {
            await using var con = new OracleConnection(GetOracleConnectionString());
            await con.OpenAsync();

            const string sql =
                @"DELETE FROM {0}DOCUVENT
                   WHERE TIPODOC = :tipoDoc
                     AND SERIE   = :serie
                     AND NUMERO  = :numero";

            await using var cmd = new OracleCommand(string.Format(sql, S), con);
            cmd.Parameters.Add("tipoDoc", OracleDbType.Varchar2).Value = tipoDoc;
            cmd.Parameters.Add("serie",   OracleDbType.Varchar2).Value = serie;
            cmd.Parameters.Add("numero",  OracleDbType.Varchar2).Value = numero;

            var filas = await cmd.ExecuteNonQueryAsync();
            return new RestablecerPasoDto
            {
                Ok     = filas > 0,
                Filas  = filas,
                Mensaje = filas > 0
                    ? $"DELETE en DOCUVENT ejecutado. Filas afectadas: {filas}."
                    : "No se encontró el registro en DOCUVENT para eliminar."
            };
        }
        catch (Exception ex)
        {
            return new RestablecerPasoDto { Ok = false, Error = ex.Message };
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  PASO 2 — ESPERAR MOVGLOS ESTADO=9, LUEGO DELETE MOVGLOS
    // ══════════════════════════════════════════════════════════════════════════
    public async Task<RestablecerPasoDto> Paso2EsperarYDeleteMovGlosAsync(
        string tipoDoc, string serie, string numero, int timeoutSegundos = 60)
    {
        try
        {
            await using var con = new OracleConnection(GetOracleConnectionString());
            await con.OpenAsync();

            const string sqlCheck =
                @"SELECT COUNT(1) FROM {0}MOVGLOS
                   WHERE TIPO_REFERENCIA = :tipoDoc
                     AND SERIE           = :serie
                     AND NRO_REFERENCIA  = :numero
                     AND ESTADO         = '9'";

            var deadline = DateTime.UtcNow.AddSeconds(timeoutSegundos);
            bool encontrado = false;

            while (DateTime.UtcNow < deadline)
            {
                await using var cmdCheck = new OracleCommand(string.Format(sqlCheck, S), con);
                cmdCheck.Parameters.Add("tipoDoc", OracleDbType.Varchar2).Value = tipoDoc;
                cmdCheck.Parameters.Add("serie",   OracleDbType.Varchar2).Value = serie;
                cmdCheck.Parameters.Add("numero",  OracleDbType.Varchar2).Value = numero;

                var cnt = Convert.ToInt32(await cmdCheck.ExecuteScalarAsync());
                if (cnt > 0) { encontrado = true; break; }

                await Task.Delay(2000);
            }

            if (!encontrado)
                return new RestablecerPasoDto
                {
                    Ok     = false,
                    Error  = $"Timeout ({timeoutSegundos}s): MOVGLOS nunca alcanzó ESTADO = 9."
                };

            const string sqlDelete =
                @"DELETE FROM {0}MOVGLOS
                   WHERE TIPO_REFERENCIA = :tipoDoc
                     AND SERIE           = :serie
                     AND NRO_REFERENCIA  = :numero";

            await using var cmdDel = new OracleCommand(string.Format(sqlDelete, S), con);
            cmdDel.Parameters.Add("tipoDoc", OracleDbType.Varchar2).Value = tipoDoc;
            cmdDel.Parameters.Add("serie",   OracleDbType.Varchar2).Value = serie;
            cmdDel.Parameters.Add("numero",  OracleDbType.Varchar2).Value = numero;

            var filas = await cmdDel.ExecuteNonQueryAsync();
            return new RestablecerPasoDto
            {
                Ok      = filas > 0,
                Filas   = filas,
                Mensaje = $"MOVGLOS alcanzó ESTADO=9. DELETE ejecutado. Filas afectadas: {filas}."
            };
        }
        catch (Exception ex)
        {
            return new RestablecerPasoDto { Ok = false, Error = ex.Message };
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  PASO 3 — UPDATE NRODOC
    // ══════════════════════════════════════════════════════════════════════════
    public async Task<RestablecerPasoDto> Paso3UpdateNroDocAsync(
        string tipoDoc, string serie, string numeroBusqueda)
    {
        try
        {
            await using var con = new OracleConnection(GetOracleConnectionString());
            await con.OpenAsync();

            const string sql =
                @"UPDATE {0}NRODOC
                     SET NUMERO  = :numeroBusqueda
                   WHERE TIPODOC = :tipoDoc
                     AND SERIE   = :serie";

            await using var cmd = new OracleCommand(string.Format(sql, S), con);
            cmd.Parameters.Add("numeroBusqueda", OracleDbType.Varchar2).Value = numeroBusqueda;
            cmd.Parameters.Add("tipoDoc",        OracleDbType.Varchar2).Value = tipoDoc;
            cmd.Parameters.Add("serie",          OracleDbType.Varchar2).Value = serie;

            var filas = await cmd.ExecuteNonQueryAsync();
            return new RestablecerPasoDto
            {
                Ok      = filas > 0,
                Filas   = filas,
                Mensaje = $"UPDATE NRODOC ejecutado. NUMERO = {numeroBusqueda}. Filas: {filas}."
            };
        }
        catch (Exception ex)
        {
            return new RestablecerPasoDto { Ok = false, Error = ex.Message };
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  PASO 4 — UPDATE NROLIBR
    // ══════════════════════════════════════════════════════════════════════════
    public async Task<RestablecerPasoDto> Paso4UpdateNroLibrAsync(
        string ano, string mes, string libro, string voucherBusqueda)
    {
        try
        {
            await using var con = new OracleConnection(GetOracleConnectionString());
            await con.OpenAsync();

            const string sql =
                @"UPDATE {0}NROLIBR
                     SET NUMERO = :voucherBusqueda
                   WHERE ANO    = :ano
                     AND MES    = :mes
                     AND LIBRO  = :libro";

            await using var cmd = new OracleCommand(string.Format(sql, S), con);
            cmd.Parameters.Add("voucherBusqueda", OracleDbType.Varchar2).Value = voucherBusqueda;
            cmd.Parameters.Add("ano",             OracleDbType.Varchar2).Value = ano;
            cmd.Parameters.Add("mes",             OracleDbType.Varchar2).Value = mes;
            cmd.Parameters.Add("libro",           OracleDbType.Varchar2).Value = libro;

            var filas = await cmd.ExecuteNonQueryAsync();
            return new RestablecerPasoDto
            {
                Ok      = filas > 0,
                Filas   = filas,
                Mensaje = $"UPDATE NROLIBR ejecutado. NUMERO = {voucherBusqueda}. Filas: {filas}."
            };
        }
        catch (Exception ex)
        {
            return new RestablecerPasoDto { Ok = false, Error = ex.Message };
        }
    }
}
