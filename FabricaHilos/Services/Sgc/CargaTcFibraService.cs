using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System.Data;
using FabricaHilos.Models.Sgc;

namespace FabricaHilos.Services.Sgc
{
    public interface ICargaTcFibraService
    {
        Task<List<TcAlgodonDto>> ObtenerTrazabilidadAsync(DateTime? fechaDesde, DateTime? fechaHasta, string? tipoCert, bool soloPendientesTc, string? codUsuario = null);
        Task<TcAlgodonDto?> ObtenerTrazabilidadIngresoAsync(string codAlm, string tpTransac, decimal serie, decimal numero);
        Task<List<TcAlgodonDto>> ObtenerPendientesTcAsync(int diasAntiguedad);
        Task<ResultadoTcAlgodonDto> RegistrarCertificadoAsync(RegistrarCertificadoTcAlgodonDto modelo, string usuario);
        Task<ResultadoTcAlgodonDto> AnularCertificadoAsync(int idCert, string usuario);
    }

    public class CargaTcFibraService : OracleServiceBase, ICargaTcFibraService
    {
        private readonly ILogger<CargaTcFibraService> _logger;

        public CargaTcFibraService(IConfiguration configuration, ILogger<CargaTcFibraService> logger, IHttpContextAccessor httpContextAccessor)
            : base(configuration, httpContextAccessor)
        {
            _logger = logger;
        }

        private static string? GetStr(OracleDataReader r, string col)
        {
            var ordinal = r.GetOrdinal(col);
            return r.IsDBNull(ordinal) ? null : r.GetValue(ordinal).ToString();
        }

        private static int? GetInt(OracleDataReader r, string col)
        {
            var ordinal = r.GetOrdinal(col);
            if (r.IsDBNull(ordinal)) return null;
            return Convert.ToInt32(r.GetValue(ordinal));
        }

        private static decimal? GetDecimal(OracleDataReader r, string col)
        {
            var ordinal = r.GetOrdinal(col);
            if (r.IsDBNull(ordinal)) return null;
            return Convert.ToDecimal(r.GetValue(ordinal));
        }

        private static DateTime? GetDt(OracleDataReader r, string col)
        {
            var ordinal = r.GetOrdinal(col);
            if (r.IsDBNull(ordinal)) return null;
            return Convert.ToDateTime(r.GetValue(ordinal));
        }

        private static TcAlgodonDto MapRow(OracleDataReader r) => new()
        {
            CodAlm          = GetStr(r, "COD_ALM"),
            TpTransac       = GetStr(r, "TP_TRANSAC"),
            Serie           = GetDecimal(r, "SERIE"),
            Numero          = GetDecimal(r, "NUMERO"),
            Algodon         = GetStr(r, "ALGODON"),
            Req             = GetInt(r, "REQ"),
            Oc              = GetDecimal(r, "OC"),
            FchReq          = GetDt(r, "FCH_REQ"),
            FchOc           = GetDt(r, "FCH_OC"),
            CantidadQq      = GetDecimal(r, "CANTIDAD_QQ"),
            CantidadKgAprox = GetDecimal(r, "CANTIDAD_KG_APROX"),
            Factura         = GetStr(r, "FACTURA"),
            Guia            = GetStr(r, "GUIA"),
            FchAtencion     = GetDt(r, "FCH_ATENCION"),
            Tc              = GetStr(r, "TC"),
            Tipo            = GetStr(r, "TIPO"),
            CodProveed      = GetStr(r, "COD_PROVEED"),
            Proveedor       = GetStr(r, "PROVEEDOR"),
            DetalleOc       = GetStr(r, "DETALLE_OC"),
            PendRegistroTc  = GetStr(r, "PEND_REGISTRO_TC"),
            IdCert          = GetInt(r, "ID_CERT"),
            UsuarioResponsable = GetStr(r, "USUARIO_RESPONSABLE"),
        };

        public async Task<List<TcAlgodonDto>> ObtenerTrazabilidadAsync(DateTime? fechaDesde, DateTime? fechaHasta, string? tipoCert, bool soloPendientesTc, string? codUsuario = null)
        {
            var result = new List<TcAlgodonDto>();
            try
            {
                await using var conn = new OracleConnection(GetOracleConnectionString());
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = $"{S}PKG_SGC_TC_ALGODON.P_OBTENER_TRAZABILIDAD";
                cmd.BindByName  = true;
                cmd.Parameters.Add(new OracleParameter("P_FCH_DESDE", OracleDbType.Date)
                    { Value = fechaDesde.HasValue ? fechaDesde.Value.Date : (object)DBNull.Value });
                cmd.Parameters.Add(new OracleParameter("P_FCH_HASTA", OracleDbType.Date)
                    { Value = fechaHasta.HasValue ? fechaHasta.Value.Date : (object)DBNull.Value });
                cmd.Parameters.Add(new OracleParameter("P_TIPO_CERT", OracleDbType.Varchar2)
                    { Value = string.IsNullOrWhiteSpace(tipoCert) ? (object)DBNull.Value : tipoCert.Trim() });
                cmd.Parameters.Add(new OracleParameter("P_SOLO_PEND_TC", OracleDbType.Varchar2, soloPendientesTc ? "S" : "N", ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter("P_COD_USUARIO", OracleDbType.Varchar2)
                    { Value = string.IsNullOrWhiteSpace(codUsuario) ? (object)DBNull.Value : codUsuario.Trim() });
                cmd.Parameters.Add(new OracleParameter("P_CURSOR", OracleDbType.RefCursor) { Direction = ParameterDirection.Output });

                await using var reader = await cmd.ExecuteReaderAsync() as OracleDataReader
                    ?? throw new InvalidOperationException("OracleDataReader expected");
                while (await reader.ReadAsync())
                    result.Add(MapRow(reader));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener trazabilidad TC algodón");
                throw;
            }
            return result;
        }

        public async Task<TcAlgodonDto?> ObtenerTrazabilidadIngresoAsync(string codAlm, string tpTransac, decimal serie, decimal numero)
        {
            try
            {
                await using var conn = new OracleConnection(GetOracleConnectionString());
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = $"{S}PKG_SGC_TC_ALGODON.P_OBTENER_TRAZABILIDAD_INGRESO";
                cmd.BindByName  = true;
                cmd.Parameters.Add(new OracleParameter("P_COD_ALM", OracleDbType.Varchar2, codAlm, ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter("P_TP_TRANSAC", OracleDbType.Varchar2, tpTransac, ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter("P_SERIE", OracleDbType.Decimal, serie, ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter("P_NUMERO", OracleDbType.Decimal, numero, ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter("P_CURSOR", OracleDbType.RefCursor) { Direction = ParameterDirection.Output });

                await using var reader = await cmd.ExecuteReaderAsync() as OracleDataReader
                    ?? throw new InvalidOperationException("OracleDataReader expected");
                if (await reader.ReadAsync())
                    return MapRow(reader);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener trazabilidad del ingreso {CodAlm}/{TpTransac}/{Serie}/{Numero}", codAlm, tpTransac, serie, numero);
                throw;
            }
        }

        public async Task<List<TcAlgodonDto>> ObtenerPendientesTcAsync(int diasAntiguedad)
        {
            var result = new List<TcAlgodonDto>();
            try
            {
                await using var conn = new OracleConnection(GetOracleConnectionString());
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = $"{S}PKG_SGC_TC_ALGODON.P_OBTENER_PENDIENTES_TC";
                cmd.BindByName  = true;
                cmd.Parameters.Add(new OracleParameter("P_DIAS_ANTIGUEDAD", OracleDbType.Int32, diasAntiguedad, ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter("P_CURSOR", OracleDbType.RefCursor) { Direction = ParameterDirection.Output });

                await using var reader = await cmd.ExecuteReaderAsync() as OracleDataReader
                    ?? throw new InvalidOperationException("OracleDataReader expected");
                while (await reader.ReadAsync())
                    result.Add(MapRow(reader));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener pendientes de TC de algodón");
                throw;
            }
            return result;
        }

        public async Task<ResultadoTcAlgodonDto> RegistrarCertificadoAsync(RegistrarCertificadoTcAlgodonDto modelo, string usuario)
        {
            try
            {
                await using var conn = new OracleConnection(GetOracleConnectionString());
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = $"{S}PKG_SGC_TC_ALGODON.P_REGISTRAR_CERTIFICADO";
                cmd.BindByName  = true;
                cmd.Parameters.Add(new OracleParameter("P_COD_ALM", OracleDbType.Varchar2, modelo.CodAlm, ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter("P_TP_TRANSAC", OracleDbType.Varchar2, modelo.TpTransac, ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter("P_SERIE", OracleDbType.Decimal, modelo.Serie, ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter("P_NUMERO", OracleDbType.Decimal, modelo.Numero, ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter("P_NUM_TC", OracleDbType.Varchar2, modelo.NumTc?.Trim(), ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter("P_TIPO_CERT", OracleDbType.Varchar2)
                    { Value = string.IsNullOrWhiteSpace(modelo.TipoCert) ? (object)DBNull.Value : modelo.TipoCert.Trim() });
                cmd.Parameters.Add(new OracleParameter("P_OBSERVACION", OracleDbType.Varchar2)
                    { Value = string.IsNullOrWhiteSpace(modelo.Observacion) ? (object)DBNull.Value : modelo.Observacion.Trim() });
                cmd.Parameters.Add(new OracleParameter("P_USUARIO", OracleDbType.Varchar2, usuario, ParameterDirection.Input));
                var pIdCert = cmd.Parameters.Add(new OracleParameter("P_ID_CERT", OracleDbType.Decimal) { Direction = ParameterDirection.Output });
                var pMsgError = cmd.Parameters.Add(new OracleParameter("P_MSGERROR", OracleDbType.Varchar2, 4000) { Direction = ParameterDirection.Output });

                await cmd.ExecuteNonQueryAsync();

                var msgError = pMsgError.Value == DBNull.Value ? null : ((OracleString)pMsgError.Value).Value;
                if (!string.IsNullOrWhiteSpace(msgError))
                    return new ResultadoTcAlgodonDto { Exito = false, MensajeError = msgError };

                int? idCert = pIdCert.Value == DBNull.Value ? null : Convert.ToInt32(((OracleDecimal)pIdCert.Value).Value);
                return new ResultadoTcAlgodonDto { Exito = true, IdCert = idCert };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar certificado TC de algodón para {CodAlm}/{TpTransac}/{Serie}/{Numero}",
                    modelo.CodAlm, modelo.TpTransac, modelo.Serie, modelo.Numero);
                return new ResultadoTcAlgodonDto { Exito = false, MensajeError = $"Error al registrar: {ex.Message}" };
            }
        }

        public async Task<ResultadoTcAlgodonDto> AnularCertificadoAsync(int idCert, string usuario)
        {
            try
            {
                await using var conn = new OracleConnection(GetOracleConnectionString());
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = $"{S}PKG_SGC_TC_ALGODON.P_ANULAR_CERTIFICADO";
                cmd.BindByName  = true;
                cmd.Parameters.Add(new OracleParameter("P_ID_CERT", OracleDbType.Decimal, idCert, ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter("P_USUARIO", OracleDbType.Varchar2, usuario, ParameterDirection.Input));
                var pMsgError = cmd.Parameters.Add(new OracleParameter("P_MSGERROR", OracleDbType.Varchar2, 4000) { Direction = ParameterDirection.Output });

                await cmd.ExecuteNonQueryAsync();

                var msgError = pMsgError.Value == DBNull.Value ? null : ((OracleString)pMsgError.Value).Value;
                if (!string.IsNullOrWhiteSpace(msgError))
                    return new ResultadoTcAlgodonDto { Exito = false, MensajeError = msgError };

                return new ResultadoTcAlgodonDto { Exito = true, IdCert = idCert };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al anular certificado TC de algodón {IdCert}", idCert);
                return new ResultadoTcAlgodonDto { Exito = false, MensajeError = $"Error al anular: {ex.Message}" };
            }
        }
    }
}
