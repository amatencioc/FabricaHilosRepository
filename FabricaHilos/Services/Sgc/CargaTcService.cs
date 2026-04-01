using Oracle.ManagedDataAccess.Client;
using System.Data;
using FabricaHilos.Models.Sgc;

namespace FabricaHilos.Services.Sgc
{
    public interface ICargaTcService
    {
        Task<(List<ReqCertDto> Items, int TotalCount)> ObtenerRequerimientosAsync(string? buscar, DateTime? fechaInicio, DateTime? fechaFin, int page = 1, int pageSize = 10);
        Task<ReqCertDto?> ObtenerRequerimientoAsync(int numReq);
        Task<List<ReqCertDDto>> ObtenerDetalleRequerimientoAsync(int numReq);
        Task<ClienteDto?> ObtenerClientePorCodigoAsync(string codCliente);
        Task<bool> ActualizarCertificadoAsync(ActualizarCertificadoDto modelo, string usuario);
        Task<string> GenerarRutaPdfCertificado(string ruc, string numCer);
        Task<(string? NroLista, decimal? Importe)> ObtenerDatosListaPreciosAsync(string codArt);
        Task<(string? Nombre, string? Email)> ObtenerDatosVendedorAsync(string codVende);
    }

    public class CargaTcService : ICargaTcService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<CargaTcService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CargaTcService(IConfiguration configuration, ILogger<CargaTcService> logger, IHttpContextAccessor httpContextAccessor)
        {
            _configuration = configuration;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        private string GetOracleConnectionString()
        {
            var oraUser = _httpContextAccessor.HttpContext?.Session.GetString("OracleUser");
            var oraPass = _httpContextAccessor.HttpContext?.Session.GetString("OraclePass");
            var baseConnStr = _configuration.GetConnectionString("OracleConnection") ?? string.Empty;

            if (!string.IsNullOrEmpty(oraUser) && !string.IsNullOrEmpty(oraPass))
            {
                var csBuilder = new OracleConnectionStringBuilder(baseConnStr)
                {
                    UserID = oraUser,
                    Password = oraPass
                };
                return csBuilder.ConnectionString;
            }
            return baseConnStr;
        }

        public async Task<(List<ReqCertDto> Items, int TotalCount)> ObtenerRequerimientosAsync(
            string? buscar, DateTime? fechaInicio, DateTime? fechaFin, int page = 1, int pageSize = 10)
        {
            var items = new List<ReqCertDto>();
            int totalCount = 0;

            try
            {
                using var conn = new OracleConnection(GetOracleConnectionString());
                await conn.OpenAsync();

                int startRow = (page - 1) * pageSize + 1;
                int endRow   = page * pageSize;

                bool hasBuscar   = !string.IsNullOrWhiteSpace(buscar);
                bool hasFechaIni = fechaInicio.HasValue;
                bool hasFechaFin = fechaFin.HasValue;

                string buscarFilter = hasBuscar
                    ? "\n                          AND (rc.NUM_CER LIKE '%' || :buscar || '%' OR c.NOMBRE LIKE '%' || :buscar || '%' OR c.RUC LIKE '%' || :buscar || '%')"
                    : string.Empty;

                string fechaFilter = string.Empty;
                if (hasFechaIni && hasFechaFin)
                    fechaFilter = "\n                          AND TRUNC(rc.FECHA) BETWEEN TRUNC(:fechaInicio) AND TRUNC(:fechaFin)";
                else if (hasFechaIni)
                    fechaFilter = "\n                          AND TRUNC(rc.FECHA) >= TRUNC(:fechaInicio)";
                else if (hasFechaFin)
                    fechaFilter = "\n                          AND TRUNC(rc.FECHA) <= TRUNC(:fechaFin)";

                string sql = $@"
                    SELECT RN, TOTAL_COUNT,
                           NUM_REQ, FECHA, NUM_CER, COD_CLIENTE, COD_ART, COD_VENDE,
                           TIPODOC, SERIE, NUMERO,
                           A_ADUSER, A_ADFECHA, A_MDUSER, A_MDFECHA,
                           RAZON_SOCIAL, RUC
                    FROM (
                        SELECT ROW_NUMBER() OVER (ORDER BY Q.NUM_REQ ASC) AS RN,
                               COUNT(*) OVER() AS TOTAL_COUNT,
                               Q.NUM_REQ, Q.FECHA, Q.NUM_CER, Q.COD_CLIENTE, Q.COD_ART, Q.COD_VENDE,
                               Q.TIPODOC, Q.SERIE, Q.NUMERO,
                               Q.A_ADUSER, Q.A_ADFECHA, Q.A_MDUSER, Q.A_MDFECHA,
                               Q.RAZON_SOCIAL, Q.RUC
                        FROM (
                            SELECT
                                rc.NUM_REQ,
                                rc.FECHA,
                                rc.NUM_CER,
                                rc.COD_CLIENTE,
                                rc.COD_ART,
                                rc.COD_VENDE,
                                rc.TIPODOC,
                                rc.SERIE,
                                rc.NUMERO,
                                rc.A_ADUSER,
                                rc.A_ADFECHA,
                                rc.A_MDUSER,
                                rc.A_MDFECHA,
                                c.NOMBRE AS RAZON_SOCIAL,
                                c.RUC
                            FROM SIG.REQ_CERT rc
                            LEFT JOIN SIG.CLIENTES c ON rc.COD_CLIENTE = c.COD_CLIENTE
                            WHERE 1=1{buscarFilter}{fechaFilter}
                        ) Q
                    )
                    WHERE RN BETWEEN :startRow AND :endRow";

                using var cmd = new OracleCommand(sql, conn);
                cmd.BindByName = true;

                if (hasBuscar)
                    cmd.Parameters.Add(new OracleParameter(":buscar", OracleDbType.Varchar2, buscar!.Trim(), ParameterDirection.Input));
                if (hasFechaIni)
                    cmd.Parameters.Add(new OracleParameter(":fechaInicio", OracleDbType.Date, fechaInicio!.Value.Date, ParameterDirection.Input));
                if (hasFechaFin)
                    cmd.Parameters.Add(new OracleParameter(":fechaFin", OracleDbType.Date, fechaFin!.Value.Date, ParameterDirection.Input));

                cmd.Parameters.Add(new OracleParameter(":startRow", OracleDbType.Int32, startRow, ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":endRow", OracleDbType.Int32, endRow, ParameterDirection.Input));

                using var reader = await cmd.ExecuteReaderAsync() as OracleDataReader
                    ?? throw new InvalidOperationException("OracleDataReader expected");

                while (await reader.ReadAsync())
                {
                    if (items.Count == 0)
                        totalCount = reader["TOTAL_COUNT"] == DBNull.Value ? 0 : Convert.ToInt32(reader["TOTAL_COUNT"]);

                    items.Add(new ReqCertDto
                    {
                        NumReq = Convert.ToInt32(reader["NUM_REQ"]),
                        Fecha = reader["FECHA"] == DBNull.Value ? null : Convert.ToDateTime(reader["FECHA"]),
                        NumCer = reader["NUM_CER"] == DBNull.Value ? null : reader["NUM_CER"]?.ToString(),
                        CodCliente = reader["COD_CLIENTE"] == DBNull.Value ? null : reader["COD_CLIENTE"]?.ToString(),
                        CodArt = reader["COD_ART"] == DBNull.Value ? null : reader["COD_ART"]?.ToString(),
                        CodVende = reader["COD_VENDE"] == DBNull.Value ? null : reader["COD_VENDE"]?.ToString(),
                        TipoDoc = reader["TIPODOC"] == DBNull.Value ? null : reader["TIPODOC"]?.ToString(),
                        Serie = reader["SERIE"] == DBNull.Value ? null : reader["SERIE"]?.ToString(),
                        Numero = reader["NUMERO"] == DBNull.Value ? null : reader["NUMERO"]?.ToString(),
                        AAduser = reader["A_ADUSER"] == DBNull.Value ? null : reader["A_ADUSER"]?.ToString(),
                        AAdfecha = reader["A_ADFECHA"] == DBNull.Value ? null : Convert.ToDateTime(reader["A_ADFECHA"]),
                        AMduser = reader["A_MDUSER"] == DBNull.Value ? null : reader["A_MDUSER"]?.ToString(),
                        AMdfecha = reader["A_MDFECHA"] == DBNull.Value ? null : Convert.ToDateTime(reader["A_MDFECHA"]),
                        RazonSocial = reader["RAZON_SOCIAL"] == DBNull.Value ? null : reader["RAZON_SOCIAL"]?.ToString(),
                        Ruc = reader["RUC"] == DBNull.Value ? null : reader["RUC"]?.ToString()
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener requerimientos de certificados");
                throw;
            }

            return (items, totalCount);
        }

        public async Task<ReqCertDto?> ObtenerRequerimientoAsync(int numReq)
        {
            try
            {
                using var conn = new OracleConnection(GetOracleConnectionString());
                await conn.OpenAsync();

                var sql = @"
                    SELECT 
                        rc.NUM_REQ,
                        rc.FECHA,
                        rc.NUM_CER,
                        rc.COD_CLIENTE,
                        rc.COD_ART,
                        rc.COD_VENDE,
                        rc.TIPODOC,
                        rc.SERIE,
                        rc.NUMERO,
                        rc.A_ADUSER,
                        rc.A_ADFECHA,
                        rc.A_MDUSER,
                        rc.A_MDFECHA,
                        c.NOMBRE AS RAZON_SOCIAL,
                        c.RUC
                    FROM SIG.REQ_CERT rc
                    LEFT JOIN SIG.CLIENTES c ON rc.COD_CLIENTE = c.COD_CLIENTE
                    WHERE rc.NUM_REQ = :NumReq";

                using var cmd = new OracleCommand(sql, conn);
                cmd.Parameters.Add(new OracleParameter("NumReq", numReq));

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new ReqCertDto
                    {
                        NumReq = reader.GetInt32("NUM_REQ"),
                        Fecha = reader.IsDBNull("FECHA") ? null : reader.GetDateTime("FECHA"),
                        NumCer = reader.IsDBNull("NUM_CER") ? null : reader.GetString("NUM_CER"),
                        CodCliente = reader.IsDBNull("COD_CLIENTE") ? null : reader.GetString("COD_CLIENTE"),
                        CodArt = reader.IsDBNull("COD_ART") ? null : reader.GetString("COD_ART"),
                        CodVende = reader.IsDBNull("COD_VENDE") ? null : reader.GetString("COD_VENDE"),
                        TipoDoc = reader.IsDBNull("TIPODOC") ? null : reader.GetString("TIPODOC"),
                        Serie = reader.IsDBNull("SERIE") ? null : reader.GetString("SERIE"),
                        Numero = reader.IsDBNull("NUMERO") ? null : reader.GetString("NUMERO"),
                        AAduser = reader.IsDBNull("A_ADUSER") ? null : reader.GetString("A_ADUSER"),
                        AAdfecha = reader.IsDBNull("A_ADFECHA") ? null : reader.GetDateTime("A_ADFECHA"),
                        AMduser = reader.IsDBNull("A_MDUSER") ? null : reader.GetString("A_MDUSER"),
                        AMdfecha = reader.IsDBNull("A_MDFECHA") ? null : reader.GetDateTime("A_MDFECHA"),
                        RazonSocial = reader.IsDBNull("RAZON_SOCIAL") ? null : reader.GetString("RAZON_SOCIAL"),
                        Ruc = reader.IsDBNull("RUC") ? null : reader.GetString("RUC")
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener requerimiento {NumReq}", numReq);
                throw;
            }

            return null;
        }

        public async Task<List<ReqCertDDto>> ObtenerDetalleRequerimientoAsync(int numReq)
        {
            var items = new List<ReqCertDDto>();

            try
            {
                using var conn = new OracleConnection(GetOracleConnectionString());
                await conn.OpenAsync();

                var sql = @"
                    SELECT 
                        NUM_REQ,
                        TIPODOC,
                        SERIE,
                        NUMERO,
                        A_ADUSER,
                        A_ADFECHA,
                        A_MDUSER,
                        A_MDFECHA
                    FROM SIG.REQ_CERT_D
                    WHERE NUM_REQ = :NumReq
                    ORDER BY TIPODOC, SERIE, NUMERO";

                using var cmd = new OracleCommand(sql, conn);
                cmd.Parameters.Add(new OracleParameter("NumReq", numReq));

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    items.Add(new ReqCertDDto
                    {
                        NumReq = reader.GetInt32("NUM_REQ"),
                        TipoDoc = reader.IsDBNull("TIPODOC") ? null : reader.GetString("TIPODOC"),
                        Serie = reader.IsDBNull("SERIE") ? null : reader.GetString("SERIE"),
                        Numero = reader.IsDBNull("NUMERO") ? null : reader.GetString("NUMERO"),
                        AAduser = reader.IsDBNull("A_ADUSER") ? null : reader.GetString("A_ADUSER"),
                        AAdfecha = reader.IsDBNull("A_ADFECHA") ? null : reader.GetDateTime("A_ADFECHA"),
                        AMduser = reader.IsDBNull("A_MDUSER") ? null : reader.GetString("A_MDUSER"),
                        AMdfecha = reader.IsDBNull("A_MDFECHA") ? null : reader.GetDateTime("A_MDFECHA")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener detalle del requerimiento {NumReq}", numReq);
                throw;
            }

            return items;
        }

        public async Task<ClienteDto?> ObtenerClientePorCodigoAsync(string codCliente)
        {
            try
            {
                using var conn = new OracleConnection(GetOracleConnectionString());
                await conn.OpenAsync();

                var sql = "SELECT COD_CLIENTE, RUC, NOMBRE AS RAZON_SOCIAL FROM SIG.CLIENTES WHERE COD_CLIENTE = :CodCliente";

                using var cmd = new OracleCommand(sql, conn);
                cmd.Parameters.Add(new OracleParameter("CodCliente", codCliente));

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new ClienteDto
                    {
                        CodCliente = reader.GetString("COD_CLIENTE"),
                        Ruc = reader.IsDBNull("RUC") ? null : reader.GetString("RUC"),
                        RazonSocial = reader.IsDBNull("RAZON_SOCIAL") ? null : reader.GetString("RAZON_SOCIAL")
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener cliente {CodCliente}", codCliente);
                throw;
            }

            return null;
        }

        public async Task<bool> ActualizarCertificadoAsync(ActualizarCertificadoDto modelo, string usuario)
        {
            try
            {
                using var conn = new OracleConnection(GetOracleConnectionString());
                await conn.OpenAsync();

                var sql = @"
                    UPDATE SIG.REQ_CERT 
                    SET 
                        NUM_CER = :NumCer,
                        A_MDUSER = :Usuario,
                        A_MDFECHA = SYSDATE
                    WHERE NUM_REQ = :NumReq";

                using var cmd = new OracleCommand(sql, conn);
                cmd.Parameters.Add(new OracleParameter("NumCer", modelo.NumCer));
                cmd.Parameters.Add(new OracleParameter("Usuario", usuario));
                cmd.Parameters.Add(new OracleParameter("NumReq", modelo.NumReq));

                var rowsAffected = await cmd.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar certificado para NUM_REQ {NumReq}", modelo.NumReq);
                throw;
            }
        }

        public async Task<string> GenerarRutaPdfCertificado(string ruc, string numCer)
        {
            var rutaBase = _configuration["RutaCertificados"] ?? @"\\10.0.7.14\6-20100096260\Certificados";
            var año = DateTime.Now.Year.ToString();

            // Crear estructura de carpetas: RutaBase\RUC\AÑO
            var rutaCarpeta = Path.Combine(rutaBase, ruc, año);

            // Crear carpetas si no existen
            await Task.Run(() =>
            {
                if (!Directory.Exists(rutaCarpeta))
                {
                    Directory.CreateDirectory(rutaCarpeta);
                    _logger.LogInformation("Carpeta creada: {RutaCarpeta}", rutaCarpeta);
                }
            });

            // Nombre del archivo: NUM_CER.pdf
            var nombreArchivo = $"{numCer}.pdf";
            var rutaCompleta = Path.Combine(rutaCarpeta, nombreArchivo);

            return rutaCompleta;
        }

        public async Task<(string? NroLista, decimal? Importe)> ObtenerDatosListaPreciosAsync(string codArt)
        {
            try
            {
                using var conn = new OracleConnection(GetOracleConnectionString());
                await conn.OpenAsync();

                var sql = @"
                    SELECT NRO_LISTA, IMPORTE
                    FROM SIG.LISPRED
                    WHERE COD_ART = :CodArt
                    AND ROWNUM = 1";

                using var cmd = new OracleCommand(sql, conn);
                cmd.Parameters.Add(new OracleParameter("CodArt", codArt));

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var nroLista = reader.IsDBNull(reader.GetOrdinal("NRO_LISTA")) 
                        ? null 
                        : reader.GetString(reader.GetOrdinal("NRO_LISTA"));

                    var importe = reader.IsDBNull(reader.GetOrdinal("IMPORTE")) 
                        ? (decimal?)null 
                        : reader.GetDecimal(reader.GetOrdinal("IMPORTE"));

                    return (nroLista, importe);
                }

                return (null, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener datos de lista de precios para COD_ART {CodArt}", codArt);
                return (null, null);
            }
        }

        public async Task<(string? Nombre, string? Email)> ObtenerDatosVendedorAsync(string codVende)
        {
            try
            {
                using var conn = new OracleConnection(GetOracleConnectionString());
                await conn.OpenAsync();

                var sql = @"
                    SELECT C_NOMBRE, C_EMAIL
                    FROM SIG.CS_USER
                    WHERE COD_ASESOR = :CodVende";

                using var cmd = new OracleCommand(sql, conn);
                cmd.Parameters.Add(new OracleParameter("CodVende", codVende));

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var nombre = reader.IsDBNull(reader.GetOrdinal("C_NOMBRE")) 
                        ? null 
                        : reader.GetString(reader.GetOrdinal("C_NOMBRE"));

                    var email = reader.IsDBNull(reader.GetOrdinal("C_EMAIL")) 
                        ? null 
                        : reader.GetString(reader.GetOrdinal("C_EMAIL"));

                    return (nombre, email);
                }

                return (null, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener datos del vendedor con COD_VENDE {CodVende}", codVende);
                return (null, null);
            }
        }
    }
}
