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
        Task<int> RegistrarVFactautAsync(int numReq, string codArt, string usuario);
        Task<bool> ActualizarEstadoReqCertAsync(int numReq, int estado, string usuario);
        Task<List<ReqCertPartidaDto>> ObtenerPartidasPorRequerimientoAsync(int numReq);
        Task<List<ReqCertOrdenCompraDto>> ObtenerOrdenesCompraPorRequerimientoAsync(int numReq);
    }

    public class CargaTcService : OracleServiceBase, ICargaTcService
    {
        private readonly ILogger<CargaTcService> _logger;

        public CargaTcService(IConfiguration configuration, ILogger<CargaTcService> logger, IHttpContextAccessor httpContextAccessor)
            : base(configuration, httpContextAccessor)
        {
            _logger = logger;
        }

        private static int? SafeGetInt32(OracleDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);

            if (reader.IsDBNull(ordinal))
                return null;

            try
            {
                // Intenta obtenerlo como decimal primero (el tipo más común en Oracle para NUMBER)
                return Convert.ToInt32(reader.GetDecimal(ordinal));
            }
            catch
            {
                try
                {
                    // Si falla, intenta obtenerlo directamente como int
                    return reader.GetInt32(ordinal);
                }
                catch
                {
                    try
                    {
                        // Como último recurso, intenta convertir desde string
                        var value = reader.GetValue(ordinal);
                        return Convert.ToInt32(value);
                    }
                    catch
                    {
                        return null;
                    }
                }
            }
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
                           TIPODOC, SERIE, NUMERO, ESTADO,
                           A_ADUSER, A_ADFECHA, A_MDUSER, A_MDFECHA,
                           RAZON_SOCIAL, RUC
                    FROM (
                        SELECT ROW_NUMBER() OVER (ORDER BY Q.NUM_REQ ASC) AS RN,
                               COUNT(*) OVER() AS TOTAL_COUNT,
                               Q.NUM_REQ, Q.FECHA, Q.NUM_CER, Q.COD_CLIENTE, Q.COD_ART, Q.COD_VENDE,
                               Q.TIPODOC, Q.SERIE, Q.NUMERO, Q.ESTADO,
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
                                rc.ESTADO,
                                rc.A_ADUSER,
                                rc.A_ADFECHA,
                                rc.A_MDUSER,
                                rc.A_MDFECHA,
                                c.NOMBRE AS RAZON_SOCIAL,
                                c.RUC
                            FROM {S}REQ_CERT rc
                            LEFT JOIN {S}CLIENTES c ON rc.COD_CLIENTE = c.COD_CLIENTE
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
                        Estado = SafeGetInt32(reader, "ESTADO"),
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

                var sql = $@"
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
                        rc.ESTADO,
                        rc.A_ADUSER,
                        rc.A_ADFECHA,
                        rc.A_MDUSER,
                        rc.A_MDFECHA,
                        c.NOMBRE AS RAZON_SOCIAL,
                        c.RUC
                    FROM {S}REQ_CERT rc
                    LEFT JOIN {S}CLIENTES c ON rc.COD_CLIENTE = c.COD_CLIENTE
                    WHERE rc.NUM_REQ = :NumReq";

                using var cmd = new OracleCommand(sql, conn);
                cmd.Parameters.Add(new OracleParameter("NumReq", numReq));

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var oracleReader = reader as OracleDataReader ?? throw new InvalidOperationException("OracleDataReader expected");

                    return new ReqCertDto
                    {
                        NumReq = oracleReader.GetInt32("NUM_REQ"),
                        Fecha = oracleReader.IsDBNull("FECHA") ? null : oracleReader.GetDateTime("FECHA"),
                        NumCer = oracleReader.IsDBNull("NUM_CER") ? null : oracleReader.GetString("NUM_CER"),
                        CodCliente = oracleReader.IsDBNull("COD_CLIENTE") ? null : oracleReader.GetString("COD_CLIENTE"),
                        CodArt = oracleReader.IsDBNull("COD_ART") ? null : oracleReader.GetString("COD_ART"),
                        CodVende = oracleReader.IsDBNull("COD_VENDE") ? null : oracleReader.GetString("COD_VENDE"),
                        TipoDoc = oracleReader.IsDBNull("TIPODOC") ? null : oracleReader.GetString("TIPODOC"),
                        Serie = oracleReader.IsDBNull("SERIE") ? null : oracleReader.GetString("SERIE"),
                        Numero = oracleReader.IsDBNull("NUMERO") ? null : oracleReader.GetString("NUMERO"),
                        Estado = SafeGetInt32(oracleReader, "ESTADO"),
                        AAduser = oracleReader.IsDBNull("A_ADUSER") ? null : oracleReader.GetString("A_ADUSER"),
                        AAdfecha = oracleReader.IsDBNull("A_ADFECHA") ? null : oracleReader.GetDateTime("A_ADFECHA"),
                        AMduser = oracleReader.IsDBNull("A_MDUSER") ? null : oracleReader.GetString("A_MDUSER"),
                        AMdfecha = oracleReader.IsDBNull("A_MDFECHA") ? null : oracleReader.GetDateTime("A_MDFECHA"),
                        RazonSocial = oracleReader.IsDBNull("RAZON_SOCIAL") ? null : oracleReader.GetString("RAZON_SOCIAL"),
                        Ruc = oracleReader.IsDBNull("RUC") ? null : oracleReader.GetString("RUC")
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

                var sql = $@"
                    SELECT 
                        NUM_REQ,
                        TIPODOC,
                        SERIE,
                        NUMERO,
                        A_ADUSER,
                        A_ADFECHA,
                        A_MDUSER,
                        A_MDFECHA
                    FROM {S}REQ_CERT_D
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

                var sql = $"SELECT COD_CLIENTE, RUC, NOMBRE AS RAZON_SOCIAL FROM {S}CLIENTES WHERE COD_CLIENTE = :CodCliente";

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
                using var transaction = conn.BeginTransaction();

                try
                {
                    // 1. Bloquear el registro antes de actualizar (FOR UPDATE NOWAIT)
                    var querySel = $"SELECT NUM_CER FROM {S}REQ_CERT WHERE NUM_REQ = :NumReq FOR UPDATE NOWAIT";

                    using (var cmdSel = new OracleCommand(querySel, conn))
                    {
                        cmdSel.Transaction = transaction;
                        cmdSel.Parameters.Add(new OracleParameter("NumReq", modelo.NumReq));

                        using var reader = await cmdSel.ExecuteReaderAsync();
                        if (!await reader.ReadAsync())
                        {
                            _logger.LogWarning("No se encontró el registro NUM_REQ={NumReq} a actualizar", modelo.NumReq);
                            await transaction.RollbackAsync();
                            return false;
                        }
                    }

                    _logger.LogDebug("Registro NUM_REQ={NumReq} bloqueado correctamente", modelo.NumReq);

                    // 2. Ejecutar el UPDATE
                    var sql = $@"
                        UPDATE {S}REQ_CERT 
                        SET 
                            NUM_CER = :NumCer,
                            A_MDUSER = :Usuario,
                            A_MDFECHA = SYSDATE
                        WHERE NUM_REQ = :NumReq";

                    using var cmd = new OracleCommand(sql, conn);
                    cmd.Transaction = transaction;
                    cmd.Parameters.Add(new OracleParameter("NumCer", modelo.NumCer));
                    cmd.Parameters.Add(new OracleParameter("Usuario", usuario));
                    cmd.Parameters.Add(new OracleParameter("NumReq", modelo.NumReq));

                    var rowsAffected = await cmd.ExecuteNonQueryAsync();

                    // 3. COMMIT de la transacción
                    await transaction.CommitAsync();
                    _logger.LogInformation("✅ Certificado actualizado correctamente. NUM_REQ={NumReq}, Filas={Rows}", modelo.NumReq, rowsAffected);
                    return rowsAffected > 0;
                }
                catch (OracleException oraEx) when (oraEx.Number == 54) // ORA-00054: resource busy
                {
                    _logger.LogWarning("El certificado NUM_REQ={NumReq} está siendo modificado por otro usuario. Intente nuevamente.", modelo.NumReq);
                    await transaction.RollbackAsync();
                    return false;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
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

            // Autenticarse en el recurso de red antes de crear directorios
            var username = _configuration["NetworkShare:Username"];
            var password = _configuration["NetworkShare:Password"];
            var domain = _configuration["NetworkShare:Domain"];

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    FabricaHilos.Helpers.NetworkShareHelper.Connect(rutaBase, username, password, domain);
                    _logger.LogInformation("Autenticación exitosa en el recurso de red para crear carpetas");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al autenticarse en el recurso de red");
                throw new InvalidOperationException($"No se pudo conectar al recurso de red: {ex.Message}", ex);
            }

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

                var sql = $@"
                    SELECT NRO_LISTA, IMPORTE
                    FROM {S}LISPRED
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

                var sql = $@"
                    SELECT C_NOMBRE, C_EMAIL
                    FROM {S}CS_USER
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

        public async Task<int> RegistrarVFactautAsync(int numReq, string codArt, string usuario)
        {
            try
            {
                using var conn = new OracleConnection(GetOracleConnectionString());
                await conn.OpenAsync();

                using var transaction = conn.BeginTransaction();

                try
                {
                    int nuevoNumero;

                    var sqlMax = $@"
                        SELECT NVL(MAX(NUMERO), 0) + 1 AS NUEVO_NUMERO
                        FROM {S}V_FACTAUT
                        WHERE TIPO = 'FA' AND SERIE = 1";

                    using (var cmdMax = new OracleCommand(sqlMax, conn))
                    {
                        cmdMax.Transaction = transaction;
                        var result = await cmdMax.ExecuteScalarAsync();
                        nuevoNumero = Convert.ToInt32(result);
                    }

                    string tipoCertificado = codArt.Length > 4 ? codArt.Substring(4) : codArt;
                    string concepto = $"REQ. EMISION. FACT. {tipoCertificado}";

                    var sqlInsert = $@"
                        INSERT INTO {S}V_FACTAUT 
                        (TIPO, SERIE, NUMERO, CONCEPTO, TIP_DIREF, NRO_DIREF, ESTADO, A_ADUSER, A_ADFECHA)
                        VALUES 
                        ('FA', 1, :Numero, :Concepto, 'GC', :NumReq, 0, :Usuario, SYSDATE)";

                    using (var cmdInsert = new OracleCommand(sqlInsert, conn))
                    {
                        cmdInsert.Transaction = transaction;
                        cmdInsert.Parameters.Add(new OracleParameter("Numero", nuevoNumero));
                        cmdInsert.Parameters.Add(new OracleParameter("Concepto", concepto));
                        cmdInsert.Parameters.Add(new OracleParameter("NumReq", numReq));
                        cmdInsert.Parameters.Add(new OracleParameter("Usuario", usuario));

                        await cmdInsert.ExecuteNonQueryAsync();
                    }

                    transaction.Commit();

                    _logger.LogInformation("Registro V_FACTAUT creado exitosamente: TIPO=FA, SERIE=1, NUMERO={Numero}, NRO_REF={NumReq}, TIP_DIREF=GC, ESTADO=0", nuevoNumero, numReq);

                    return nuevoNumero;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar V_FACTAUT para NUM_REQ {NumReq}", numReq);
                throw;
            }
        }

        public async Task<List<ReqCertPartidaDto>> ObtenerPartidasPorRequerimientoAsync(int numReq)
        {
            var partidas = new List<ReqCertPartidaDto>();

            try
            {
                using var conn = new OracleConnection(GetOracleConnectionString());
                await conn.OpenAsync();

                var sql = $@"
                    SELECT DISTINCT 
                        CASE 
                            WHEN P.NUM_PED IS NOT NULL AND I.NRO IS NOT NULL 
                            THEN TO_CHAR(P.NUM_PED) || '-' || TO_CHAR(I.NRO)
                            ELSE NULL 
                        END AS PEDIDO
                    FROM {S}REQ_CERT_D rcd
                    INNER JOIN {S}DOCUVENT F
                        ON F.TIPODOC = rcd.TIPODOC
                        AND TRIM(F.SERIE) = TRIM(rcd.SERIE)
                        AND TRIM(F.NUMERO) = TRIM(rcd.NUMERO)
                    LEFT JOIN {S}KARDEX_G G
                        ON G.TIP_REF = F.TIPODOC
                        AND TRIM(G.SER_REF) = TRIM(F.SERIE)
                        AND TRIM(G.NRO_REF) = TRIM(F.NUMERO)
                    LEFT JOIN {S}PEDIDO P
                        ON TRIM(G.NRO_DOC_REF) = TO_CHAR(P.NUM_PED)
                        AND TRIM(G.SER_DOC_REF) = TO_CHAR(P.SERIE)
                        AND G.TIP_DOC_REF = P.TIPO_DOCTO
                        AND P.ESTADO <> '9'
                    LEFT JOIN {S}ITEMDOCU ID
                        ON ID.TIPODOC = F.TIPODOC
                        AND TRIM(ID.SERIE) = TRIM(F.SERIE)
                        AND TRIM(ID.NUMERO) = TRIM(F.NUMERO)
                    LEFT JOIN {S}ITEMPED I
                        ON I.NUM_PED = P.NUM_PED
                        AND I.SERIE = P.SERIE
                        AND I.COD_ART = ID.COD_ART
                    WHERE rcd.NUM_REQ = :NumReq
                        AND P.NUM_PED IS NOT NULL
                        AND I.NRO IS NOT NULL
                    ORDER BY P.NUM_PED, I.NRO";

                using var cmd = new OracleCommand(sql, conn);
                cmd.Parameters.Add(new OracleParameter("NumReq", numReq));

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var pedido = reader.IsDBNull("PEDIDO") ? null : reader.GetString("PEDIDO");
                    if (!string.IsNullOrWhiteSpace(pedido))
                    {
                        partidas.Add(new ReqCertPartidaDto
                        {
                            Partida = pedido,
                            Item = null
                        });
                    }
                }

                _logger.LogInformation("Se obtuvieron {Count} partidas (pedidos) para NUM_REQ={NumReq}", partidas.Count, numReq);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener partidas (pedidos) para NUM_REQ {NumReq}", numReq);
            }

            return partidas;
        }

        public async Task<List<ReqCertOrdenCompraDto>> ObtenerOrdenesCompraPorRequerimientoAsync(int numReq)
        {
            var ordenesCompra = new List<ReqCertOrdenCompraDto>();

            try
            {
                using var conn = new OracleConnection(GetOracleConnectionString());
                await conn.OpenAsync();

                var sql = $@"
                    SELECT DISTINCT 
                        P.NUMERO_REF AS ORDEN_COMPRA
                    FROM {S}REQ_CERT_D rcd
                    INNER JOIN {S}DOCUVENT F
                        ON F.TIPODOC = rcd.TIPODOC
                        AND TRIM(F.SERIE) = TRIM(rcd.SERIE)
                        AND TRIM(F.NUMERO) = TRIM(rcd.NUMERO)
                    LEFT JOIN {S}KARDEX_G G
                        ON G.TIP_REF = F.TIPODOC
                        AND TRIM(G.SER_REF) = TRIM(F.SERIE)
                        AND TRIM(G.NRO_REF) = TRIM(F.NUMERO)
                    LEFT JOIN {S}PEDIDO P
                        ON TRIM(G.NRO_DOC_REF) = TO_CHAR(P.NUM_PED)
                        AND TRIM(G.SER_DOC_REF) = TO_CHAR(P.SERIE)
                        AND G.TIP_DOC_REF = P.TIPO_DOCTO
                        AND P.ESTADO <> '9'
                    WHERE rcd.NUM_REQ = :NumReq
                        AND P.NUMERO_REF IS NOT NULL
                    ORDER BY P.NUMERO_REF";

                using var cmd = new OracleCommand(sql, conn);
                cmd.Parameters.Add(new OracleParameter("NumReq", numReq));

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var oc = reader.IsDBNull("ORDEN_COMPRA") ? null : reader.GetString("ORDEN_COMPRA");
                    if (!string.IsNullOrWhiteSpace(oc))
                    {
                        ordenesCompra.Add(new ReqCertOrdenCompraDto { OrdenCompra = oc });
                    }
                }

                _logger.LogInformation("Se obtuvieron {Count} órdenes de compra para NUM_REQ={NumReq}", ordenesCompra.Count, numReq);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener órdenes de compra para NUM_REQ {NumReq}", numReq);
            }

            return ordenesCompra;
        }

        public async Task<bool> ActualizarEstadoReqCertAsync(int numReq, int estado, string usuario)
        {
            try
            {
                using var conn = new OracleConnection(GetOracleConnectionString());
                await conn.OpenAsync();

                var sql = $@"
                    UPDATE {S}REQ_CERT
                    SET ESTADO = :Estado,
                        A_MDUSER = :Usuario,
                        A_MDFECHA = SYSDATE
                    WHERE NUM_REQ = :NumReq";

                using var cmd = new OracleCommand(sql, conn);
                cmd.Parameters.Add(new OracleParameter("Estado", estado));
                cmd.Parameters.Add(new OracleParameter("Usuario", usuario));
                cmd.Parameters.Add(new OracleParameter("NumReq", numReq));

                int rowsAffected = await cmd.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Estado de REQ_CERT actualizado: NUM_REQ={NumReq}, ESTADO={Estado}", numReq, estado);
                    return true;
                }

                _logger.LogWarning("No se encontró requerimiento REQ_CERT con NUM_REQ={NumReq}", numReq);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar estado de REQ_CERT para NUM_REQ {NumReq}", numReq);
                return false;
            }
        }
    }
}
