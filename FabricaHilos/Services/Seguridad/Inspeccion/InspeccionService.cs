using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace FabricaHilos.Services.Seguridad.Inspeccion
{
    public class ResponsableDto
    {
        public string Codigo { get; set; } = string.Empty;
        public string NombreCorto { get; set; } = string.Empty;
        public string TextoCompleto => $"{Codigo} - {NombreCorto}";
    }

    public class CentroCostoDto
    {
        public string CentroCosto { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string TextoCompleto => $"{CentroCosto} - {Nombre}";
    }

    public class InspeccionListDto
    {
        public int Numero { get; set; }
        public string CentroCosto { get; set; } = string.Empty;
        public string NombreCentroCosto { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string ResponsableInspeccion { get; set; } = string.Empty;
        public string NombreRespInspeccion { get; set; } = string.Empty;
        public string ResponsableArea { get; set; } = string.Empty;
        public string NombreRespArea { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public string? RutaFotoH { get; set; }
        public DateTime? FechaFotoH { get; set; }
        public string? UbicaFotoH { get; set; }
        public string? RutaFotoAc { get; set; }
        public DateTime? FechaFotoAc { get; set; }
        public string? UbicaFotoAc { get; set; }
        public bool TieneAccionCorrectiva => !string.IsNullOrEmpty(UbicaFotoAc);
    }

    public interface IInspeccionService
    {
        Task<List<ResponsableDto>> ObtenerResponsablesAreaAsync();
        Task<List<ResponsableDto>> ObtenerResponsablesInspeccionAsync();
        Task<List<CentroCostoDto>> ObtenerCentrosCostoAsync();
        Task<int> ObtenerSiguienteNumeroInspeccionAsync();
        /// <summary>
        /// Registra un hallazgo en una sola transacción: obtiene el número de NRODOC (FOR UPDATE NOWAIT),
        /// inserta en SI_INSPECCION y actualiza el correlativo. Retorna el número asignado.
        /// Lanza InvalidOperationException si NRODOC está bloqueada por otra sesión.
        /// </summary>
        Task<int> RegistrarHallazgoAsync(InspeccionRegistroDto inspeccion, string usuario);
        Task<List<InspeccionListDto>> ObtenerInspeccionesAsync(string? tipo = null, string? estado = null);
        Task<InspeccionListDto?> ObtenerInspeccionPorNumeroAsync(int numero);
        Task RegistrarAccionCorrectivaAsync(int numero, string rutaFoto, string ubicaFoto, string usuario);
        Task AnularInspeccionAsync(int numero, string usuario);
        Task ActualizarFotoAsync(int numero, string tipoFoto, string ubicaFoto, string? rutaFotoCompleta, string usuario);
        Task ActualizarHallazgoAsync(int numero, string ccosto, string tipo, string respInspeccion, string respArea, string usuario);
    }

    public class InspeccionRegistroDto
    {
        public int NumeroInspeccion { get; set; }
        public string CentroCosto { get; set; } = string.Empty;
        public string TipoInspeccion { get; set; } = string.Empty;
        public string ResponsableInspeccion { get; set; } = string.Empty;
        public string ResponsableArea { get; set; } = string.Empty;
        public string RutaFoto { get; set; } = string.Empty;
        public string UbicaFoto { get; set; } = string.Empty;
    }

    public class InspeccionService : IInspeccionService
    {
        private readonly string _connectionString;
        private readonly ILogger<InspeccionService> _logger;
        private const int CmdTimeoutSec = 15; // Timeout para comandos Oracle (evita bloqueo infinito por locks)

        public InspeccionService(IConfiguration configuration, ILogger<InspeccionService> logger)
        {
            _connectionString = configuration.GetConnectionString("OracleConnection")
                ?? throw new InvalidOperationException("Oracle connection string not found.");
            _logger = logger;
        }

        public async Task<List<ResponsableDto>> ObtenerResponsablesAreaAsync()
        {
            var resultado = new List<ResponsableDto>();

            const string query = @"
                SELECT C_CODIGO, NOMBRE_CORTO
                FROM V_PERSONAL
                WHERE SITUACION = '1'
                ORDER BY 2";

            try
            {
                using var connection = new OracleConnection(_connectionString);
                await connection.OpenAsync();

                using var command = new OracleCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    resultado.Add(new ResponsableDto
                    {
                        Codigo = reader.GetString(0),
                        NombreCorto = reader.GetString(1)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener responsables de área");
                throw;
            }

            return resultado;
        }

        public async Task<List<ResponsableDto>> ObtenerResponsablesInspeccionAsync()
        {
            var resultado = new List<ResponsableDto>();

            const string query = @"
                SELECT C_CODIGO, NOMBRE_CORTO
                FROM V_PERSONAL
                WHERE SITUACION = '1'
                  AND C_CARGO IN (SELECT C_CARGO FROM T_CARGO WHERE CCOSTO = '280' AND ESTADO <> '9')
                ORDER BY 2";

            try
            {
                using var connection = new OracleConnection(_connectionString);
                await connection.OpenAsync();

                using var command = new OracleCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    resultado.Add(new ResponsableDto
                    {
                        Codigo = reader.GetString(0),
                        NombreCorto = reader.GetString(1)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener responsables de inspección");
                throw;
            }

            return resultado;
        }

        public async Task<List<CentroCostoDto>> ObtenerCentrosCostoAsync()
        {
            var resultado = new List<CentroCostoDto>();

            const string query = @"
                SELECT CENTRO_COSTO, SUBSTR(NOMBRE, 1, 30) NOMBRE
                FROM CENTRO_DE_COSTOS
                WHERE TIPO = 'D'
                  AND ESTADO <> '9'
                ORDER BY 2";

            try
            {
                using var connection = new OracleConnection(_connectionString);
                await connection.OpenAsync();

                using var command = new OracleCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    resultado.Add(new CentroCostoDto
                    {
                        CentroCosto = reader.GetString(0),
                        Nombre = reader.GetString(1)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener centros de costo");
                throw;
            }

            return resultado;
        }

        public async Task<List<InspeccionListDto>> ObtenerInspeccionesAsync(string? tipo = null, string? estado = null)
        {
            var resultado = new List<InspeccionListDto>();

            var query = @"
                SELECT 
                    i.NUMERO,
                    i.CCOSTO,
                    NVL(c.NOMBRE, '') AS NOMBRE_CCOSTO,
                    i.FECHA,
                    i.TIPO,
                    i.RESP_INSPECCION,
                    NVL(vp1.NOMBRE_CORTO, '') AS NOMBRE_RESP_INSPECCION,
                    i.RESP_AREA,
                    NVL(vp2.NOMBRE_CORTO, '') AS NOMBRE_RESP_AREA,
                    i.ESTADO,
                    i.RUTA_FOTO_H,
                    i.FCH_FOTO_H,
                    i.UBICA_FOTO_H,
                    i.RUTA_FOTO_AC,
                    i.FCH_FOTO_AC,
                    i.UBICA_FOTO_AC
                FROM SI_INSPECCION i
                LEFT JOIN CENTRO_DE_COSTOS c ON i.CCOSTO = c.CENTRO_COSTO
                LEFT JOIN V_PERSONAL vp1 ON i.RESP_INSPECCION = vp1.C_CODIGO
                LEFT JOIN V_PERSONAL vp2 ON i.RESP_AREA = vp2.C_CODIGO
                WHERE 1=1";

            if (!string.IsNullOrWhiteSpace(tipo))
            {
                query += " AND i.TIPO = :tipo";
            }

            if (!string.IsNullOrWhiteSpace(estado))
            {
                query += " AND i.ESTADO = :estado";
            }

            query += " ORDER BY i.NUMERO DESC";

            try
            {
                using var connection = new OracleConnection(_connectionString);
                await connection.OpenAsync();

                using var command = new OracleCommand(query, connection);

                if (!string.IsNullOrWhiteSpace(tipo))
                {
                    command.Parameters.Add("tipo", OracleDbType.Varchar2).Value = tipo;
                }

                if (!string.IsNullOrWhiteSpace(estado))
                {
                    command.Parameters.Add("estado", OracleDbType.Varchar2).Value = estado;
                }

                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    resultado.Add(new InspeccionListDto
                    {
                        Numero = reader.GetInt32(0),
                        CentroCosto = reader.GetString(1),
                        NombreCentroCosto = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        Fecha = reader.GetDateTime(3),
                        Tipo = reader.GetString(4),
                        ResponsableInspeccion = reader.GetString(5),
                        NombreRespInspeccion = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                        ResponsableArea = reader.GetString(7),
                        NombreRespArea = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                        Estado = reader.GetString(9),
                        RutaFotoH = reader.IsDBNull(10) ? null : reader.GetString(10),
                        FechaFotoH = reader.IsDBNull(11) ? null : reader.GetDateTime(11),
                        UbicaFotoH = reader.IsDBNull(12) ? null : reader.GetString(12),
                        RutaFotoAc = reader.IsDBNull(13) ? null : reader.GetString(13),
                        FechaFotoAc = reader.IsDBNull(14) ? null : reader.GetDateTime(14),
                        UbicaFotoAc = reader.IsDBNull(15) ? null : reader.GetString(15)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener listado de inspecciones");
                throw;
            }

            return resultado;
        }

        public async Task<InspeccionListDto?> ObtenerInspeccionPorNumeroAsync(int numero)
        {
            const string query = @"
                SELECT 
                    i.NUMERO,
                    i.CCOSTO,
                    NVL(c.NOMBRE, '') AS NOMBRE_CCOSTO,
                    i.FECHA,
                    i.TIPO,
                    i.RESP_INSPECCION,
                    NVL(vp1.NOMBRE_CORTO, '') AS NOMBRE_RESP_INSPECCION,
                    i.RESP_AREA,
                    NVL(vp2.NOMBRE_CORTO, '') AS NOMBRE_RESP_AREA,
                    i.ESTADO,
                    i.RUTA_FOTO_H,
                    i.FCH_FOTO_H,
                    i.UBICA_FOTO_H,
                    i.RUTA_FOTO_AC,
                    i.FCH_FOTO_AC,
                    i.UBICA_FOTO_AC
                FROM SI_INSPECCION i
                LEFT JOIN CENTRO_DE_COSTOS c ON i.CCOSTO = c.CENTRO_COSTO
                LEFT JOIN V_PERSONAL vp1 ON i.RESP_INSPECCION = vp1.C_CODIGO
                LEFT JOIN V_PERSONAL vp2 ON i.RESP_AREA = vp2.C_CODIGO
                WHERE i.NUMERO = :numero";

            try
            {
                using var connection = new OracleConnection(_connectionString);
                await connection.OpenAsync();

                using var command = new OracleCommand(query, connection);
                command.Parameters.Add("numero", OracleDbType.Int32).Value = numero;

                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new InspeccionListDto
                    {
                        Numero = reader.GetInt32(0),
                        CentroCosto = reader.GetString(1),
                        NombreCentroCosto = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        Fecha = reader.GetDateTime(3),
                        Tipo = reader.GetString(4),
                        ResponsableInspeccion = reader.GetString(5),
                        NombreRespInspeccion = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                        ResponsableArea = reader.GetString(7),
                        NombreRespArea = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                        Estado = reader.GetString(9),
                        RutaFotoH = reader.IsDBNull(10) ? null : reader.GetString(10),
                        FechaFotoH = reader.IsDBNull(11) ? null : reader.GetDateTime(11),
                        UbicaFotoH = reader.IsDBNull(12) ? null : reader.GetString(12),
                        RutaFotoAc = reader.IsDBNull(13) ? null : reader.GetString(13),
                        FechaFotoAc = reader.IsDBNull(14) ? null : reader.GetDateTime(14),
                        UbicaFotoAc = reader.IsDBNull(15) ? null : reader.GetString(15)
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener inspección {Numero}", numero);
                throw;
            }

            return null;
        }

        public async Task<int> ObtenerSiguienteNumeroInspeccionAsync()
        {
            const string query = "SELECT NUMERO FROM NRODOC WHERE TIPODOC = 'IN'";
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                _logger.LogWarning("▶▶ SVC ObtenerSiguienteNumero: Abriendo conexión Oracle...");
                using var connection = new OracleConnection(_connectionString);
                await connection.OpenAsync();
                _logger.LogWarning("▶▶ SVC ObtenerSiguienteNumero: Conexión OK ({Ms}ms)", sw.ElapsedMilliseconds);

                using var command = new OracleCommand(query, connection);
                command.CommandTimeout = CmdTimeoutSec;
                var result = await command.ExecuteScalarAsync();
                var numero = result != null ? Convert.ToInt32(result) : 0;
                _logger.LogWarning("▶▶ SVC ObtenerSiguienteNumero: Resultado={Numero} ({Ms}ms)", numero, sw.ElapsedMilliseconds);
                return numero;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "▶▶ SVC ObtenerSiguienteNumero: ERROR ({Ms}ms)", sw.ElapsedMilliseconds);
                throw;
            }
        }

        public async Task<int> RegistrarHallazgoAsync(InspeccionRegistroDto inspeccion, string usuario)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            _logger.LogWarning("▶▶ SVC RegistrarHallazgo: Abriendo conexión Oracle...");
            using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();
            _logger.LogWarning("▶▶ SVC RegistrarHallazgo: Conexión OK ({Ms}ms)", sw.ElapsedMilliseconds);

            using var transaction = connection.BeginTransaction();
            _logger.LogWarning("▶▶ SVC RegistrarHallazgo: Transacción iniciada ({Ms}ms)", sw.ElapsedMilliseconds);

            try
            {
                // 1. Obtener número Y bloquear NRODOC atómicamente (NOWAIT = falla inmediato si hay lock zombie)
                const string queryNumero = "SELECT NUMERO FROM NRODOC WHERE TIPODOC = 'IN' FOR UPDATE NOWAIT";
                int numero;

                _logger.LogWarning("▶▶ SVC RegistrarHallazgo: SELECT NRODOC FOR UPDATE NOWAIT...");
                using (var cmdNumero = new OracleCommand(queryNumero, connection))
                {
                    cmdNumero.Transaction = transaction;
                    cmdNumero.CommandTimeout = CmdTimeoutSec;
                    var result = await cmdNumero.ExecuteScalarAsync();

                    if (result == null || result == DBNull.Value)
                        throw new InvalidOperationException(
                            "No se encontró el correlativo en NRODOC para TIPODOC='IN'.");

                    numero = Convert.ToInt32(result);
                }
                _logger.LogWarning("▶▶ SVC RegistrarHallazgo: Número obtenido de NRODOC={Numero}, fila bloqueada ({Ms}ms)", numero, sw.ElapsedMilliseconds);

                // 2. Construir ruta completa con nombre de archivo y extensión
                var nombreArchivo = $"{numero}-H.jpg";
                var rutaFotoCompleta = Path.Combine(inspeccion.RutaFoto, nombreArchivo);
                _logger.LogWarning("▶▶ SVC RegistrarHallazgo: Ruta foto completa={Ruta}", rutaFotoCompleta);

                // 3. Insertar en SI_INSPECCION con el número obtenido
                const string queryInsertar = @"
                    INSERT INTO SI_INSPECCION 
                    (NUMERO, CCOSTO, FECHA, TIPO, RESP_INSPECCION, RESP_AREA, ESTADO, 
                     RUTA_FOTO_H, FCH_FOTO_H, UBICA_FOTO_H, A_ADUSER, A_ADFECHA)
                    VALUES 
                    (:pNumero, :pCcosto, SYSDATE, :pTipo, :pRespInspeccion, :pRespArea, '1',
                     :pRutaFoto, SYSDATE, :pUbicaFoto, :pUsuario, SYSDATE)";

                _logger.LogWarning("▶▶ SVC RegistrarHallazgo: Ejecutando INSERT SI_INSPECCION con Numero={Num}...", numero);
                using (var cmdInsertar = new OracleCommand(queryInsertar, connection))
                {
                    cmdInsertar.BindByName = true;
                    cmdInsertar.Transaction = transaction;
                    cmdInsertar.CommandTimeout = CmdTimeoutSec;
                    cmdInsertar.Parameters.Add("pNumero", OracleDbType.Int32).Value = numero;
                    cmdInsertar.Parameters.Add("pCcosto", OracleDbType.Varchar2).Value = inspeccion.CentroCosto;
                    cmdInsertar.Parameters.Add("pTipo", OracleDbType.Varchar2).Value = inspeccion.TipoInspeccion;
                    cmdInsertar.Parameters.Add("pRespInspeccion", OracleDbType.Varchar2).Value = inspeccion.ResponsableInspeccion;
                    cmdInsertar.Parameters.Add("pRespArea", OracleDbType.Varchar2).Value = inspeccion.ResponsableArea;
                    cmdInsertar.Parameters.Add("pRutaFoto", OracleDbType.Varchar2).Value = rutaFotoCompleta;
                    cmdInsertar.Parameters.Add("pUbicaFoto", OracleDbType.Varchar2).Value = inspeccion.UbicaFoto;
                    cmdInsertar.Parameters.Add("pUsuario", OracleDbType.Varchar2).Value = usuario;

                    var filasInsertadas = await cmdInsertar.ExecuteNonQueryAsync();
                    _logger.LogWarning("▶▶ SVC RegistrarHallazgo: INSERT OK, filas={Filas} ({Ms}ms)", filasInsertadas, sw.ElapsedMilliseconds);
                }

                // 4. Actualizar el correlativo en NRODOC (+1) — ya tenemos el lock, no puede bloquearse
                const string queryActualizarCorrelativo = @"
                    UPDATE NRODOC 
                    SET NUMERO = NUMERO + 1 
                    WHERE TIPODOC = 'IN'";

                _logger.LogWarning("▶▶ SVC RegistrarHallazgo: Ejecutando UPDATE NRODOC (NUMERO+1)...");
                using (var cmdActualizar = new OracleCommand(queryActualizarCorrelativo, connection))
                {
                    cmdActualizar.Transaction = transaction;
                    cmdActualizar.CommandTimeout = CmdTimeoutSec;
                    var filasActualizadas = await cmdActualizar.ExecuteNonQueryAsync();
                    _logger.LogWarning("▶▶ SVC RegistrarHallazgo: UPDATE NRODOC OK, filas={Filas} ({Ms}ms)", filasActualizadas, sw.ElapsedMilliseconds);
                }

                _logger.LogWarning("▶▶ SVC RegistrarHallazgo: Commit...");
                await transaction.CommitAsync();
                _logger.LogWarning("▶▶ SVC RegistrarHallazgo: ✅ Commit OK. Número asignado={Numero}, NRODOC ahora={Sig} ({Ms}ms)", numero, numero + 1, sw.ElapsedMilliseconds);

                return numero;
            }
            catch (OracleException oraEx) when (oraEx.Number == 54) // ORA-00054: resource busy (NOWAIT)
            {
                _logger.LogWarning("▶▶ SVC RegistrarHallazgo: NRODOC BLOQUEADA por otra sesión ({Ms}ms)", sw.ElapsedMilliseconds);
                try { await transaction.RollbackAsync(); }
                catch (Exception exRb) { _logger.LogError(exRb, "▶▶ SVC RegistrarHallazgo: ERROR en Rollback (lock)"); }
                throw new InvalidOperationException(
                    "La tabla de correlativos (NRODOC) está bloqueada por otra operación que no finalizó. " +
                    "Esto puede deberse a un intento anterior que se colgó. " +
                    "Contacte al administrador de la base de datos para liberar el lock.", oraEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "▶▶ SVC RegistrarHallazgo: ERROR — Rollback ({Ms}ms)", sw.ElapsedMilliseconds);
                try { await transaction.RollbackAsync(); }
                catch (Exception exRb) { _logger.LogError(exRb, "▶▶ SVC RegistrarHallazgo: ERROR en Rollback"); }
                throw;
            }
        }

        public async Task RegistrarAccionCorrectivaAsync(int numero, string rutaFoto, string ubicaFoto, string usuario)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            _logger.LogWarning("▶▶ SVC RegistrarAC: Abriendo conexión Oracle...");
            using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();
            _logger.LogWarning("▶▶ SVC RegistrarAC: Conexión OK ({Ms}ms)", sw.ElapsedMilliseconds);

            using var transaction = connection.BeginTransaction();
            _logger.LogWarning("▶▶ SVC RegistrarAC: Transacción iniciada ({Ms}ms)", sw.ElapsedMilliseconds);

            try
            {
                // 1. Bloquear el registro de inspección y verificar estado y acción correctiva
                const string querySelect = @"
                    SELECT RUTA_FOTO_AC, ESTADO 
                    FROM SI_INSPECCION 
                    WHERE NUMERO = :pNumero 
                    FOR UPDATE NOWAIT";

                string? rutaFotoActual;
                string? estadoActual;

                _logger.LogWarning("▶▶ SVC RegistrarAC: SELECT FOR UPDATE NOWAIT Num={Num}...", numero);
                using (var cmdSelect = new OracleCommand(querySelect, connection))
                {
                    cmdSelect.Transaction = transaction;
                    cmdSelect.CommandTimeout = CmdTimeoutSec;
                    cmdSelect.BindByName = true;
                    cmdSelect.Parameters.Add("pNumero", OracleDbType.Int32).Value = numero;

                    using var reader = await cmdSelect.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                    {
                        throw new InvalidOperationException($"No se encontró el hallazgo con número {numero}.");
                    }

                    rutaFotoActual = reader.IsDBNull(0) ? null : reader.GetString(0);
                    estadoActual = reader.IsDBNull(1) ? null : reader.GetString(1);
                }

                _logger.LogWarning("▶▶ SVC RegistrarAC: Registro bloqueado. RUTA_FOTO_AC actual={Ruta}, ESTADO={Estado} ({Ms}ms)", 
                    rutaFotoActual ?? "NULL", estadoActual ?? "NULL", sw.ElapsedMilliseconds);

                // 2. Verificar que no esté anulado
                if (estadoActual == "9")
                {
                    throw new InvalidOperationException(
                        $"El hallazgo #{numero} está anulado y no se puede registrar una acción correctiva.");
                }

                // 3. Verificar que no tenga ya una acción correctiva registrada
                if (!string.IsNullOrEmpty(rutaFotoActual))
                {
                    _logger.LogWarning("▶▶ SVC RegistrarAC: El hallazgo {Num} YA TIENE acción correctiva registrada ({Ms}ms)", 
                        numero, sw.ElapsedMilliseconds);
                    throw new InvalidOperationException(
                        $"El hallazgo #{numero} ya tiene una acción correctiva registrada. " +
                        "No se puede sobrescribir.");
                }

                // 4. Construir ruta completa con nombre de archivo y extensión
                var nombreArchivo = $"{numero}-AC.jpg";
                var rutaFotoCompleta = Path.Combine(rutaFoto, nombreArchivo);
                _logger.LogWarning("▶▶ SVC RegistrarAC: Ruta foto completa={Ruta}", rutaFotoCompleta);

                // 5. Actualizar con la acción correctiva
                const string queryUpdate = @"
                    UPDATE SI_INSPECCION
                    SET RUTA_FOTO_AC = :pRutaFoto,
                        FCH_FOTO_AC = SYSDATE,
                        UBICA_FOTO_AC = :pUbicaFoto,
                        ESTADO = '6',
                        A_MDUSER = :pUsuario,
                        A_MDFECHA = SYSDATE
                    WHERE NUMERO = :pNumero";

                _logger.LogWarning("▶▶ SVC RegistrarAC: Ejecutando UPDATE SI_INSPECCION Num={Num}...", numero);
                using (var cmdUpdate = new OracleCommand(queryUpdate, connection))
                {
                    cmdUpdate.Transaction = transaction;
                    cmdUpdate.CommandTimeout = CmdTimeoutSec;
                    cmdUpdate.BindByName = true;
                    cmdUpdate.Parameters.Add("pRutaFoto", OracleDbType.Varchar2).Value = rutaFotoCompleta;
                    cmdUpdate.Parameters.Add("pUbicaFoto", OracleDbType.Varchar2).Value = ubicaFoto;
                    cmdUpdate.Parameters.Add("pUsuario", OracleDbType.Varchar2).Value = usuario;
                    cmdUpdate.Parameters.Add("pNumero", OracleDbType.Int32).Value = numero;

                    var filasActualizadas = await cmdUpdate.ExecuteNonQueryAsync();
                    _logger.LogWarning("▶▶ SVC RegistrarAC: UPDATE OK, filas={Filas} ({Ms}ms)", 
                        filasActualizadas, sw.ElapsedMilliseconds);
                }

                // 6. Commit
                _logger.LogWarning("▶▶ SVC RegistrarAC: Commit...");
                await transaction.CommitAsync();
                _logger.LogWarning("▶▶ SVC RegistrarAC: ✅ Commit OK. Acción correctiva registrada para hallazgo #{Num} ({Ms}ms)", 
                    numero, sw.ElapsedMilliseconds);
            }
            catch (OracleException oraEx) when (oraEx.Number == 54) // ORA-00054: resource busy (NOWAIT)
            {
                _logger.LogWarning("▶▶ SVC RegistrarAC: Registro BLOQUEADO por otra sesión ({Ms}ms)", sw.ElapsedMilliseconds);
                try { await transaction.RollbackAsync(); }
                catch (Exception exRb) { _logger.LogError(exRb, "▶▶ SVC RegistrarAC: ERROR en Rollback (lock)"); }
                throw new InvalidOperationException(
                    $"El hallazgo #{numero} está siendo actualizado por otro usuario en este momento. " +
                    "Por favor, intente nuevamente en unos segundos.", oraEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "▶▶ SVC RegistrarAC: ERROR — Rollback ({Ms}ms)", sw.ElapsedMilliseconds);
                try { await transaction.RollbackAsync(); }
                catch (Exception exRb) { _logger.LogError(exRb, "▶▶ SVC RegistrarAC: ERROR en Rollback"); }
                throw;
            }
        }

        public async Task AnularInspeccionAsync(int numero, string usuario)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            _logger.LogWarning("▶▶ SVC Anular: Abriendo conexión Oracle...");
            using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();
            _logger.LogWarning("▶▶ SVC Anular: Conexión OK ({Ms}ms)", sw.ElapsedMilliseconds);

            using var transaction = connection.BeginTransaction();
            _logger.LogWarning("▶▶ SVC Anular: Transacción iniciada ({Ms}ms)", sw.ElapsedMilliseconds);

            try
            {
                // 1. Bloquear el registro y verificar estado actual
                const string querySelect = @"
                    SELECT ESTADO 
                    FROM SI_INSPECCION 
                    WHERE NUMERO = :pNumero 
                    FOR UPDATE NOWAIT";

                string? estadoActual;

                _logger.LogWarning("▶▶ SVC Anular: SELECT FOR UPDATE NOWAIT Num={Num}...", numero);
                using (var cmdSelect = new OracleCommand(querySelect, connection))
                {
                    cmdSelect.Transaction = transaction;
                    cmdSelect.CommandTimeout = CmdTimeoutSec;
                    cmdSelect.BindByName = true;
                    cmdSelect.Parameters.Add("pNumero", OracleDbType.Int32).Value = numero;

                    using var reader = await cmdSelect.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                    {
                        throw new InvalidOperationException($"No se encontró la inspección con número {numero}.");
                    }

                    estadoActual = reader.IsDBNull(0) ? null : reader.GetString(0);
                }

                _logger.LogWarning("▶▶ SVC Anular: Estado actual={Estado} ({Ms}ms)", estadoActual ?? "NULL", sw.ElapsedMilliseconds);

                if (estadoActual == "9")
                {
                    throw new InvalidOperationException($"La inspección #{numero} ya se encuentra anulada.");
                }

                // 2. Actualizar estado a 9 (Anulado)
                const string queryUpdate = @"
                    UPDATE SI_INSPECCION
                    SET ESTADO = '9',
                        A_MDUSER = :pUsuario,
                        A_MDFECHA = SYSDATE
                    WHERE NUMERO = :pNumero";

                _logger.LogWarning("▶▶ SVC Anular: Ejecutando UPDATE ESTADO='9' Num={Num}...", numero);
                using (var cmdUpdate = new OracleCommand(queryUpdate, connection))
                {
                    cmdUpdate.Transaction = transaction;
                    cmdUpdate.CommandTimeout = CmdTimeoutSec;
                    cmdUpdate.BindByName = true;
                    cmdUpdate.Parameters.Add("pUsuario", OracleDbType.Varchar2).Value = usuario;
                    cmdUpdate.Parameters.Add("pNumero", OracleDbType.Int32).Value = numero;

                    var filasActualizadas = await cmdUpdate.ExecuteNonQueryAsync();
                    _logger.LogWarning("▶▶ SVC Anular: UPDATE OK, filas={Filas} ({Ms}ms)", filasActualizadas, sw.ElapsedMilliseconds);
                }

                // 3. Commit
                _logger.LogWarning("▶▶ SVC Anular: Commit...");
                await transaction.CommitAsync();
                _logger.LogWarning("▶▶ SVC Anular: ✅ Commit OK. Inspección #{Num} anulada ({Ms}ms)", numero, sw.ElapsedMilliseconds);
            }
            catch (OracleException oraEx) when (oraEx.Number == 54)
            {
                _logger.LogWarning("▶▶ SVC Anular: Registro BLOQUEADO por otra sesión ({Ms}ms)", sw.ElapsedMilliseconds);
                try { await transaction.RollbackAsync(); }
                catch (Exception exRb) { _logger.LogError(exRb, "▶▶ SVC Anular: ERROR en Rollback (lock)"); }
                throw new InvalidOperationException(
                    $"La inspección #{numero} está siendo modificada por otro usuario. Intente nuevamente.", oraEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "▶▶ SVC Anular: ERROR — Rollback ({Ms}ms)", sw.ElapsedMilliseconds);
                try { await transaction.RollbackAsync(); }
                catch (Exception exRb) { _logger.LogError(exRb, "▶▶ SVC Anular: ERROR en Rollback"); }
                throw;
            }
        }

        public async Task ActualizarFotoAsync(int numero, string tipoFoto, string ubicaFoto, string? rutaFotoCompleta, string usuario)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _logger.LogWarning("▶▶ SVC ActualizarFoto: tipo={Tipo}, Num={Num}...", tipoFoto, numero);

            using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                const string queryLock = "SELECT ESTADO FROM SI_INSPECCION WHERE NUMERO = :pNumero FOR UPDATE NOWAIT";
                string? estado;
                using (var cmdLock = new OracleCommand(queryLock, connection))
                {
                    cmdLock.Transaction = transaction;
                    cmdLock.CommandTimeout = CmdTimeoutSec;
                    cmdLock.BindByName = true;
                    cmdLock.Parameters.Add("pNumero", OracleDbType.Int32).Value = numero;
                    var result = await cmdLock.ExecuteScalarAsync();
                    if (result == null || result == DBNull.Value)
                        throw new InvalidOperationException($"No se encontró la inspección #{numero}.");
                    estado = result.ToString();
                }

                if (estado == "9")
                    throw new InvalidOperationException($"La inspección #{numero} está anulada y no se pueden editar sus fotos.");

                string query;
                if (tipoFoto == "H")
                {
                    query = rutaFotoCompleta != null
                        ? "UPDATE SI_INSPECCION SET UBICA_FOTO_H = :pUbica, RUTA_FOTO_H = :pRuta, FCH_FOTO_H = SYSDATE, A_MDUSER = :pUsuario, A_MDFECHA = SYSDATE WHERE NUMERO = :pNumero"
                        : "UPDATE SI_INSPECCION SET UBICA_FOTO_H = :pUbica, A_MDUSER = :pUsuario, A_MDFECHA = SYSDATE WHERE NUMERO = :pNumero";
                }
                else
                {
                    query = rutaFotoCompleta != null
                        ? "UPDATE SI_INSPECCION SET UBICA_FOTO_AC = :pUbica, RUTA_FOTO_AC = :pRuta, FCH_FOTO_AC = SYSDATE, A_MDUSER = :pUsuario, A_MDFECHA = SYSDATE WHERE NUMERO = :pNumero"
                        : "UPDATE SI_INSPECCION SET UBICA_FOTO_AC = :pUbica, A_MDUSER = :pUsuario, A_MDFECHA = SYSDATE WHERE NUMERO = :pNumero";
                }

                using (var cmdUpdate = new OracleCommand(query, connection))
                {
                    cmdUpdate.Transaction = transaction;
                    cmdUpdate.CommandTimeout = CmdTimeoutSec;
                    cmdUpdate.BindByName = true;
                    cmdUpdate.Parameters.Add("pUbica", OracleDbType.Varchar2).Value = ubicaFoto;
                    if (rutaFotoCompleta != null)
                        cmdUpdate.Parameters.Add("pRuta", OracleDbType.Varchar2).Value = rutaFotoCompleta;
                    cmdUpdate.Parameters.Add("pUsuario", OracleDbType.Varchar2).Value = usuario;
                    cmdUpdate.Parameters.Add("pNumero", OracleDbType.Int32).Value = numero;
                    await cmdUpdate.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                _logger.LogWarning("▶▶ SVC ActualizarFoto: ✅ OK ({Ms}ms)", sw.ElapsedMilliseconds);
            }
            catch (OracleException oraEx) when (oraEx.Number == 54)
            {
                try { await transaction.RollbackAsync(); } catch { }
                throw new InvalidOperationException($"La inspección #{numero} está siendo modificada por otro usuario.", oraEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "▶▶ SVC ActualizarFoto: ERROR");
                try { await transaction.RollbackAsync(); } catch { }
                throw;
            }
        }

        public async Task ActualizarHallazgoAsync(int numero, string ccosto, string tipo, string respInspeccion, string respArea, string usuario)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _logger.LogWarning("▶▶ SVC ActualizarHallazgo: Num={Num}...", numero);

            using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                const string queryLock = "SELECT ESTADO FROM SI_INSPECCION WHERE NUMERO = :pNumero FOR UPDATE NOWAIT";
                string? estado;
                using (var cmdLock = new OracleCommand(queryLock, connection))
                {
                    cmdLock.Transaction = transaction;
                    cmdLock.CommandTimeout = CmdTimeoutSec;
                    cmdLock.BindByName = true;
                    cmdLock.Parameters.Add("pNumero", OracleDbType.Int32).Value = numero;
                    using var reader = await cmdLock.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                        throw new InvalidOperationException($"No se encontró la inspección #{numero}.");
                    estado = reader.IsDBNull(0) ? null : reader.GetString(0);
                }

                if (estado == "9")
                    throw new InvalidOperationException($"La inspección #{numero} está anulada y no se puede editar.");

                const string queryUpdate = @"
                    UPDATE SI_INSPECCION
                    SET CCOSTO = :pCcosto, TIPO = :pTipo,
                        RESP_INSPECCION = :pRespInspeccion, RESP_AREA = :pRespArea,
                        A_MDUSER = :pUsuario, A_MDFECHA = SYSDATE
                    WHERE NUMERO = :pNumero";

                using (var cmdUpdate = new OracleCommand(queryUpdate, connection))
                {
                    cmdUpdate.Transaction = transaction;
                    cmdUpdate.CommandTimeout = CmdTimeoutSec;
                    cmdUpdate.BindByName = true;
                    cmdUpdate.Parameters.Add("pCcosto", OracleDbType.Varchar2).Value = ccosto;
                    cmdUpdate.Parameters.Add("pTipo", OracleDbType.Varchar2).Value = tipo;
                    cmdUpdate.Parameters.Add("pRespInspeccion", OracleDbType.Varchar2).Value = respInspeccion;
                    cmdUpdate.Parameters.Add("pRespArea", OracleDbType.Varchar2).Value = respArea;
                    cmdUpdate.Parameters.Add("pUsuario", OracleDbType.Varchar2).Value = usuario;
                    cmdUpdate.Parameters.Add("pNumero", OracleDbType.Int32).Value = numero;
                    await cmdUpdate.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                _logger.LogWarning("▶▶ SVC ActualizarHallazgo: ✅ OK #{Num} ({Ms}ms)", numero, sw.ElapsedMilliseconds);
            }
            catch (OracleException oraEx) when (oraEx.Number == 54)
            {
                try { await transaction.RollbackAsync(); } catch { }
                throw new InvalidOperationException($"La inspección #{numero} está siendo modificada por otro usuario.", oraEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "▶▶ SVC ActualizarHallazgo: ERROR");
                try { await transaction.RollbackAsync(); } catch { }
                throw;
            }
        }
    }
}
