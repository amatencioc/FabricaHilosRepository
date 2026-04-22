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

    public class InspeccionFotoDto
    {
        public int Numero { get; set; }
        public int Item { get; set; }
        public string? RutaFotoH { get; set; }
        public DateTime? FechaFotoH { get; set; }
        public string? UbicaFotoH { get; set; }
        public string? RutaFotoAc { get; set; }
        public DateTime? FechaFotoAc { get; set; }
        public string? UbicaFotoAc { get; set; }
        public string? Estado { get; set; }
        public bool TieneAccionCorrectiva => !string.IsNullOrEmpty(UbicaFotoAc);
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
        public string Objetivo { get; set; } = string.Empty;
        public List<InspeccionFotoDto> Fotos { get; set; } = new();
        public int CantFotos => Fotos.Count;
        public int CantAccionesCorrectivas => Fotos.Count(f => f.TieneAccionCorrectiva);
        public bool TieneHallazgos => Fotos.Any();
        public bool TieneAccionCorrectivaPendiente => Fotos.Any(f => !f.TieneAccionCorrectiva);
        public bool TodasTienenAccionCorrectiva => Fotos.Any() && Fotos.All(f => f.TieneAccionCorrectiva);
        public bool PuedeAgregarHallazgo => Fotos.Count < 10 && Estado != "9";
    }

    public interface IInspeccionService
    {
        Task<List<ResponsableDto>> ObtenerResponsablesAreaAsync();
        Task<List<ResponsableDto>> ObtenerResponsablesInspeccionAsync();
        Task<List<CentroCostoDto>> ObtenerCentrosCostoAsync();
        Task<int> ObtenerSiguienteNumeroInspeccionAsync();
        Task<int> RegistrarHallazgoAsync(InspeccionRegistroDto inspeccion, string usuario);
        Task<List<InspeccionListDto>> ObtenerInspeccionesAsync(string? tipo = null, string? estado = null, DateTime? fechaInicio = null, DateTime? fechaFin = null);
        Task<InspeccionListDto?> ObtenerInspeccionPorNumeroAsync(int numero);
        Task<List<InspeccionFotoDto>> ObtenerFotosInspeccionAsync(int numero);
        Task<int> AgregarFotoHallazgoAsync(int numero, string rutaFoto, string ubicaFoto, string usuario);
        Task RegistrarAccionCorrectivaAsync(int numero, int item, string rutaFoto, string ubicaFoto, string usuario);
        Task AnularInspeccionAsync(int numero, string usuario);
        Task ActualizarFotoAsync(int numero, int item, string tipoFoto, string ubicaFoto, string? rutaFotoCompleta, string usuario);
        Task ActualizarHallazgoAsync(int numero, string ccosto, string tipo, string respInspeccion, string respArea, string usuario);
    }

    public class InspeccionRegistroDto
    {
        public int NumeroInspeccion { get; set; }
        public string CentroCosto { get; set; } = string.Empty;
        public string TipoInspeccion { get; set; } = string.Empty;
        public string ResponsableInspeccion { get; set; } = string.Empty;
        public string ResponsableArea { get; set; } = string.Empty;
        public string ObjetivoHallazgo { get; set; } = string.Empty;
    }

    public class InspeccionService : OracleServiceBase, IInspeccionService
    {
        private readonly ILogger<InspeccionService> _logger;
        private const int CmdTimeoutSec = 15;

        public InspeccionService(IConfiguration configuration, ILogger<InspeccionService> logger, IHttpContextAccessor httpContextAccessor)
            : base(configuration, httpContextAccessor)
        {
            _logger = logger;
        }

        public async Task<List<ResponsableDto>> ObtenerResponsablesAreaAsync()
        {
            var resultado = new List<ResponsableDto>();

            var query = $@"
                SELECT C_CODIGO, NOMBRE_CORTO
                FROM {S}V_PERSONAL
                WHERE SITUACION = '1'
                ORDER BY 2";

            try
            {
                using var connection = new OracleConnection(GetOracleConnectionString());
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

            var query = $@"
                SELECT C_CODIGO, NOMBRE_CORTO
                FROM {S}V_PERSONAL
                WHERE SITUACION = '1'
                  AND C_CARGO IN (SELECT C_CARGO FROM {S}T_CARGO WHERE CCOSTO = '280' AND ESTADO <> '9')
                ORDER BY 2";

            try
            {
                using var connection = new OracleConnection(GetOracleConnectionString());
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

            var query = $@"
                SELECT CENTRO_COSTO, SUBSTR(NOMBRE, 1, 30) NOMBRE
                FROM {S}CENTRO_DE_COSTOS
                WHERE TIPO = 'D'
                  AND ESTADO <> '9'
                ORDER BY 2";

            try
            {
                using var connection = new OracleConnection(GetOracleConnectionString());
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

        public async Task<List<InspeccionListDto>> ObtenerInspeccionesAsync(string? tipo = null, string? estado = null, DateTime? fechaInicio = null, DateTime? fechaFin = null)
        {
            var resultado = new List<InspeccionListDto>();

            var query = $@"
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
                    i.ESTADO
                FROM {S}SI_INSPECCION i
                LEFT JOIN {S}CENTRO_DE_COSTOS c ON i.CCOSTO = c.CENTRO_COSTO
                LEFT JOIN {S}V_PERSONAL vp1 ON i.RESP_INSPECCION = vp1.C_CODIGO
                LEFT JOIN {S}V_PERSONAL vp2 ON i.RESP_AREA = vp2.C_CODIGO
                WHERE 1=1";

            if (!string.IsNullOrWhiteSpace(tipo))
            {
                query += " AND i.TIPO = :tipo";
            }

            if (!string.IsNullOrWhiteSpace(estado))
            {
                query += " AND i.ESTADO = :estado";
            }

            if (fechaInicio.HasValue)
            {
                query += " AND i.FECHA >= :fechaInicio";
            }

            if (fechaFin.HasValue)
            {
                query += " AND i.FECHA < :fechaFin";
            }

            query += " ORDER BY i.NUMERO DESC";

            try
            {
                using var connection = new OracleConnection(GetOracleConnectionString());
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

                if (fechaInicio.HasValue)
                {
                    command.Parameters.Add("fechaInicio", OracleDbType.Date).Value = fechaInicio.Value.Date;
                }

                if (fechaFin.HasValue)
                {
                    command.Parameters.Add("fechaFin", OracleDbType.Date).Value = fechaFin.Value.Date.AddDays(1);
                }

                using (var reader = await command.ExecuteReaderAsync())
                {
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
                            Objetivo = string.Empty
                        });
                    }
                }

                if (resultado.Any())
                {
                    await CargarFotosAsync(connection, resultado);
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
            var query = $@"
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
                    i.ESTADO
                FROM {S}SI_INSPECCION i
                LEFT JOIN {S}CENTRO_DE_COSTOS c ON i.CCOSTO = c.CENTRO_COSTO
                LEFT JOIN {S}V_PERSONAL vp1 ON i.RESP_INSPECCION = vp1.C_CODIGO
                LEFT JOIN {S}V_PERSONAL vp2 ON i.RESP_AREA = vp2.C_CODIGO
                WHERE i.NUMERO = :numero";

            try
            {
                using var connection = new OracleConnection(GetOracleConnectionString());
                await connection.OpenAsync();

                InspeccionListDto? dto = null;

                using var command = new OracleCommand(query, connection);
                command.Parameters.Add("numero", OracleDbType.Int32).Value = numero;

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        dto = new InspeccionListDto
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
                            Objetivo = string.Empty
                        };
                    }
                }

                if (dto != null)
                {
                    var lista = new List<InspeccionListDto> { dto };
                    await CargarFotosAsync(connection, lista);
                }

                return dto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener inspección {Numero}", numero);
                throw;
            }
        }

        public async Task<int> ObtenerSiguienteNumeroInspeccionAsync()
        {
            var query = $"SELECT NUMERO FROM {S}NRODOC WHERE TIPODOC = 'IN'";
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                _logger.LogWarning("▶▶ SVC ObtenerSiguienteNumero: Abriendo conexión Oracle...");
                using var connection = new OracleConnection(GetOracleConnectionString());
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
            using var connection = new OracleConnection(GetOracleConnectionString());
            await connection.OpenAsync();
            _logger.LogWarning("▶▶ SVC RegistrarHallazgo: Conexión OK ({Ms}ms)", sw.ElapsedMilliseconds);

            using var transaction = connection.BeginTransaction();
            _logger.LogWarning("▶▶ SVC RegistrarHallazgo: Transacción iniciada ({Ms}ms)", sw.ElapsedMilliseconds);

            try
            {
                // 1. Obtener número Y bloquear NRODOC atómicamente (NOWAIT = falla inmediato si hay lock zombie)
                var queryNumero = $"SELECT NUMERO FROM {S}NRODOC WHERE TIPODOC = 'IN' FOR UPDATE NOWAIT";
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

                // 2. Construir ruta completa — ya no se guarda foto en el registro
                // La foto se agrega después desde la ventana H / AC

                // 3. Obtener cantidad de trabajadores activos
                var queryNroTrab = $@"
                    SELECT COUNT(*)
                    FROM {S}V_PERSONAL
                    WHERE SITUACION = '1'
                      AND C_ESTADO IN ('CO','ES')";
                int nroTrab;

                using (var cmdNroTrab = new OracleCommand(queryNroTrab, connection))
                {
                    cmdNroTrab.Transaction = transaction;
                    cmdNroTrab.CommandTimeout = CmdTimeoutSec;
                    var resultNroTrab = await cmdNroTrab.ExecuteScalarAsync();
                    nroTrab = resultNroTrab != null ? Convert.ToInt32(resultNroTrab) : 0;
                }
                _logger.LogWarning("▶▶ SVC RegistrarHallazgo: NRO_TRAB={NroTrab} ({Ms}ms)", nroTrab, sw.ElapsedMilliseconds);

                // 4. Insertar cabecera en SI_INSPECCION (sin datos de foto)
                var queryInsertar = $@"
                    INSERT INTO {S}SI_INSPECCION 
                    (NUMERO, CCOSTO, FECHA, TIPO, RESP_INSPECCION, RESP_AREA, ESTADO, NRO_TRAB, OBJETIVO, A_ADUSER, A_ADFECHA)
                    VALUES 
                    (:pNumero, :pCcosto, SYSDATE, :pTipo, :pRespInspeccion, :pRespArea, '1', :pNroTrab, :pObjetivo, :pUsuario, SYSDATE)";

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
                    cmdInsertar.Parameters.Add("pNroTrab", OracleDbType.Int32).Value = nroTrab;
                    cmdInsertar.Parameters.Add("pObjetivo", OracleDbType.Varchar2).Value =
                        string.IsNullOrWhiteSpace(inspeccion.ObjetivoHallazgo) ? (object)DBNull.Value : inspeccion.ObjetivoHallazgo;
                    cmdInsertar.Parameters.Add("pUsuario", OracleDbType.Varchar2).Value = usuario;
                    await cmdInsertar.ExecuteNonQueryAsync();
                }

                // 5. Actualizar el correlativo en NRODOC (+1)
                var queryActualizarCorrelativo = $@"
                    UPDATE {S}NRODOC 
                    SET NUMERO = NUMERO + 1 
                    WHERE TIPODOC = 'IN'";

                _logger.LogWarning("▶▶ SVC RegistrarHallazgo: Ejecutando UPDATE {S}NRODOC (NUMERO+1)...");
                using (var cmdActualizar = new OracleCommand(queryActualizarCorrelativo, connection))
                {
                    cmdActualizar.Transaction = transaction;
                    cmdActualizar.CommandTimeout = CmdTimeoutSec;
                    var filasActualizadas = await cmdActualizar.ExecuteNonQueryAsync();
                    _logger.LogWarning("▶▶ SVC RegistrarHallazgo: UPDATE {S}NRODOC OK, filas={Filas} ({Ms}ms)", filasActualizadas, sw.ElapsedMilliseconds);
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

        public async Task RegistrarAccionCorrectivaAsync(int numero, int item, string rutaFoto, string ubicaFoto, string usuario)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            using var connection = new OracleConnection(GetOracleConnectionString());
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                // 1. Verificar estado de la cabecera
                var queryEstado = $@"
                    SELECT ESTADO FROM {S}SI_INSPECCION WHERE NUMERO = :pNumero FOR UPDATE NOWAIT";
                string? estadoActual;
                using (var cmdEstado = new OracleCommand(queryEstado, connection))
                {
                    cmdEstado.Transaction = transaction;
                    cmdEstado.CommandTimeout = CmdTimeoutSec;
                    cmdEstado.BindByName = true;
                    cmdEstado.Parameters.Add("pNumero", OracleDbType.Int32).Value = numero;
                    var result = await cmdEstado.ExecuteScalarAsync();
                    if (result == null || result == DBNull.Value)
                        throw new InvalidOperationException($"No se encontró la inspección #{numero}.");
                    estadoActual = result.ToString();
                }

                if (estadoActual == "9")
                    throw new InvalidOperationException($"La inspección #{numero} está anulada.");

                // 2. Bloquear el item de detalle y verificar que no tenga AC
                var querySelectF = $@"
                    SELECT RUTA_FOTO_AC FROM {S}SI_INSPECCION_F
                    WHERE NUMERO = :pNumero AND ITEM = :pItem FOR UPDATE NOWAIT";
                string? rutaAcActual;
                using (var cmdF = new OracleCommand(querySelectF, connection))
                {
                    cmdF.Transaction = transaction;
                    cmdF.CommandTimeout = CmdTimeoutSec;
                    cmdF.BindByName = true;
                    cmdF.Parameters.Add("pNumero", OracleDbType.Int32).Value = numero;
                    cmdF.Parameters.Add("pItem", OracleDbType.Int32).Value = item;
                    using var reader = await cmdF.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                        throw new InvalidOperationException($"No se encontró el hallazgo #{numero} ítem {item}.");
                    rutaAcActual = reader.IsDBNull(0) ? null : reader.GetString(0);
                }

                if (!string.IsNullOrEmpty(rutaAcActual))
                    throw new InvalidOperationException($"El hallazgo #{numero} ítem {item} ya tiene acción correctiva.");

                // 3. Actualizar SI_INSPECCION_F con la acción correctiva
                var nombreArchivo = $"{numero}-{item}-AC.jpg";
                var rutaFotoCompleta = Path.Combine(rutaFoto, nombreArchivo);

                var queryUpdateF = $@"
                    UPDATE {S}SI_INSPECCION_F
                    SET RUTA_FOTO_AC = :pRutaFoto,
                        FCH_FOTO_AC = SYSDATE,
                        UBICA_FOTO_AC = :pUbicaFoto,
                        A_MDUSER = :pUsuario,
                        A_MDFECHA = SYSDATE
                    WHERE NUMERO = :pNumero AND ITEM = :pItem";
                using (var cmdUpd = new OracleCommand(queryUpdateF, connection))
                {
                    cmdUpd.Transaction = transaction;
                    cmdUpd.CommandTimeout = CmdTimeoutSec;
                    cmdUpd.BindByName = true;
                    cmdUpd.Parameters.Add("pRutaFoto", OracleDbType.Varchar2).Value = rutaFotoCompleta;
                    cmdUpd.Parameters.Add("pUbicaFoto", OracleDbType.Varchar2).Value = ubicaFoto;
                    cmdUpd.Parameters.Add("pUsuario", OracleDbType.Varchar2).Value = usuario;
                    cmdUpd.Parameters.Add("pNumero", OracleDbType.Int32).Value = numero;
                    cmdUpd.Parameters.Add("pItem", OracleDbType.Int32).Value = item;
                    await cmdUpd.ExecuteNonQueryAsync();
                }

                // 4. Verificar si todos los items tienen AC → cerrar inspección (ESTADO='6')
                var querySinAC = $@"
                    SELECT COUNT(*) FROM {S}SI_INSPECCION_F
                    WHERE NUMERO = :pNumero AND ESTADO <> '9' AND RUTA_FOTO_AC IS NULL";
                using (var cmdCheck = new OracleCommand(querySinAC, connection))
                {
                    cmdCheck.Transaction = transaction;
                    cmdCheck.CommandTimeout = CmdTimeoutSec;
                    cmdCheck.BindByName = true;
                    cmdCheck.Parameters.Add("pNumero", OracleDbType.Int32).Value = numero;
                    var pendientes = Convert.ToInt32(await cmdCheck.ExecuteScalarAsync());
                    if (pendientes == 0)
                    {
                        var queryCerrar = $@"
                            UPDATE {S}SI_INSPECCION SET ESTADO = '6', A_MDUSER = :pUsuario, A_MDFECHA = SYSDATE WHERE NUMERO = :pNumero";
                        using var cmdCerrar = new OracleCommand(queryCerrar, connection);
                        cmdCerrar.Transaction = transaction;
                        cmdCerrar.CommandTimeout = CmdTimeoutSec;
                        cmdCerrar.BindByName = true;
                        cmdCerrar.Parameters.Add("pUsuario", OracleDbType.Varchar2).Value = usuario;
                        cmdCerrar.Parameters.Add("pNumero", OracleDbType.Int32).Value = numero;
                        await cmdCerrar.ExecuteNonQueryAsync();
                    }
                }

                await transaction.CommitAsync();
                _logger.LogInformation("Acción correctiva registrada: inspección #{Num} ítem {Item}", numero, item);
            }
            catch (OracleException oraEx) when (oraEx.Number == 54)
            {
                try { await transaction.RollbackAsync(); } catch { }
                throw new InvalidOperationException(
                    $"El registro está siendo modificado por otro usuario. Intente nuevamente.", oraEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar AC inspección #{Num} ítem {Item}", numero, item);
                try { await transaction.RollbackAsync(); } catch { }
                throw;
            }
        }

        public async Task AnularInspeccionAsync(int numero, string usuario)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            _logger.LogWarning("▶▶ SVC Anular: Abriendo conexión Oracle...");
            using var connection = new OracleConnection(GetOracleConnectionString());
            await connection.OpenAsync();
            _logger.LogWarning("▶▶ SVC Anular: Conexión OK ({Ms}ms)", sw.ElapsedMilliseconds);

            using var transaction = connection.BeginTransaction();
            _logger.LogWarning("▶▶ SVC Anular: Transacción iniciada ({Ms}ms)", sw.ElapsedMilliseconds);

            try
            {
                // 1. Bloquear el registro y verificar estado actual
                var querySelect = $@"
                    SELECT ESTADO 
                    FROM {S}SI_INSPECCION 
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

                // 2. Anular cabecera
                var queryUpdate = $@"
                    UPDATE {S}SI_INSPECCION
                    SET ESTADO = '9',
                        A_MDUSER = :pUsuario,
                        A_MDFECHA = SYSDATE
                    WHERE NUMERO = :pNumero";

                using (var cmdUpdate = new OracleCommand(queryUpdate, connection))
                {
                    cmdUpdate.Transaction = transaction;
                    cmdUpdate.CommandTimeout = CmdTimeoutSec;
                    cmdUpdate.BindByName = true;
                    cmdUpdate.Parameters.Add("pUsuario", OracleDbType.Varchar2).Value = usuario;
                    cmdUpdate.Parameters.Add("pNumero", OracleDbType.Int32).Value = numero;
                    await cmdUpdate.ExecuteNonQueryAsync();
                }

                // 3. Anular detalle (SI_INSPECCION_F)
                var queryUpdateF = $@"
                    UPDATE {S}SI_INSPECCION_F
                    SET ESTADO = '9',
                        A_MDUSER = :pUsuario,
                        A_MDFECHA = SYSDATE
                    WHERE NUMERO = :pNumero";

                using (var cmdUpdateF = new OracleCommand(queryUpdateF, connection))
                {
                    cmdUpdateF.Transaction = transaction;
                    cmdUpdateF.CommandTimeout = CmdTimeoutSec;
                    cmdUpdateF.BindByName = true;
                    cmdUpdateF.Parameters.Add("pUsuario", OracleDbType.Varchar2).Value = usuario;
                    cmdUpdateF.Parameters.Add("pNumero", OracleDbType.Int32).Value = numero;
                    await cmdUpdateF.ExecuteNonQueryAsync();
                }

                // 4. Commit
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

        public async Task ActualizarFotoAsync(int numero, int item, string tipoFoto, string ubicaFoto, string? rutaFotoCompleta, string usuario)
        {
            using var connection = new OracleConnection(GetOracleConnectionString());
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                // Verificar estado cabecera
                var queryLock = $"SELECT ESTADO FROM {S}SI_INSPECCION WHERE NUMERO = :pNumero FOR UPDATE NOWAIT";
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
                    throw new InvalidOperationException($"La inspección #{numero} está anulada.");

                // Actualizar en SI_INSPECCION_F
                string query;
                if (tipoFoto == "H")
                {
                    query = rutaFotoCompleta != null
                        ? $"UPDATE {S}SI_INSPECCION_F SET UBICA_FOTO_H = :pUbica, RUTA_FOTO_H = :pRuta, FCH_FOTO_H = SYSDATE, A_MDUSER = :pUsuario, A_MDFECHA = SYSDATE WHERE NUMERO = :pNumero AND ITEM = :pItem"
                        : $"UPDATE {S}SI_INSPECCION_F SET UBICA_FOTO_H = :pUbica, A_MDUSER = :pUsuario, A_MDFECHA = SYSDATE WHERE NUMERO = :pNumero AND ITEM = :pItem";
                }
                else
                {
                    query = rutaFotoCompleta != null
                        ? $"UPDATE {S}SI_INSPECCION_F SET UBICA_FOTO_AC = :pUbica, RUTA_FOTO_AC = :pRuta, FCH_FOTO_AC = SYSDATE, A_MDUSER = :pUsuario, A_MDFECHA = SYSDATE WHERE NUMERO = :pNumero AND ITEM = :pItem"
                        : $"UPDATE {S}SI_INSPECCION_F SET UBICA_FOTO_AC = :pUbica, A_MDUSER = :pUsuario, A_MDFECHA = SYSDATE WHERE NUMERO = :pNumero AND ITEM = :pItem";
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
                    cmdUpdate.Parameters.Add("pItem", OracleDbType.Int32).Value = item;
                    await cmdUpdate.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
            }
            catch (OracleException oraEx) when (oraEx.Number == 54)
            {
                try { await transaction.RollbackAsync(); } catch { }
                throw new InvalidOperationException($"La inspección #{numero} está siendo modificada por otro usuario.", oraEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar foto inspección #{Num} ítem {Item}", numero, item);
                try { await transaction.RollbackAsync(); } catch { }
                throw;
            }
        }

        public async Task ActualizarHallazgoAsync(int numero, string ccosto, string tipo, string respInspeccion, string respArea, string usuario)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _logger.LogWarning("▶▶ SVC ActualizarHallazgo: Num={Num}...", numero);

            using var connection = new OracleConnection(GetOracleConnectionString());
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                var queryLock = $"SELECT ESTADO FROM {S}SI_INSPECCION WHERE NUMERO = :pNumero FOR UPDATE NOWAIT";
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

                var queryUpdate = $@"
                    UPDATE {S}SI_INSPECCION
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

        // ========== FOTOS (SI_INSPECCION_F) ==========

        private async Task CargarFotosAsync(OracleConnection connection, List<InspeccionListDto> inspecciones)
        {
            if (!inspecciones.Any()) return;

            var numeros = inspecciones.Select(i => i.Numero).Distinct().ToList();

            // Parámetros nombrados para el IN (:p0, :p1, ...) — evita interpolación directa
            var paramNames = numeros.Select((_, i) => $":p{i}").ToList();
            var inClause   = string.Join(",", paramNames);

            var query = $@"
                SELECT NUMERO, ITEM, RUTA_FOTO_H, FCH_FOTO_H, UBICA_FOTO_H,
                       RUTA_FOTO_AC, FCH_FOTO_AC, UBICA_FOTO_AC, ESTADO
                FROM {S}SI_INSPECCION_F
                WHERE NUMERO IN ({inClause}) AND ESTADO <> '9'
                ORDER BY NUMERO, ITEM";

            var fotos = new List<InspeccionFotoDto>();
            using (var cmd = new OracleCommand(query, connection))
            {
                for (int i = 0; i < numeros.Count; i++)
                    cmd.Parameters.Add(new OracleParameter($":p{i}", numeros[i]));

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    fotos.Add(new InspeccionFotoDto
                    {
                        Numero = reader.GetInt32(0),
                        Item = reader.GetInt32(1),
                        RutaFotoH = reader.IsDBNull(2) ? null : reader.GetString(2),
                        FechaFotoH = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                        UbicaFotoH = reader.IsDBNull(4) ? null : reader.GetString(4),
                        RutaFotoAc = reader.IsDBNull(5) ? null : reader.GetString(5),
                        FechaFotoAc = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                        UbicaFotoAc = reader.IsDBNull(7) ? null : reader.GetString(7),
                        Estado = reader.IsDBNull(8) ? null : reader.GetString(8)
                    });
                }
            }

            var fotosPorNumero = fotos.GroupBy(f => f.Numero).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var insp in inspecciones)
            {
                if (fotosPorNumero.TryGetValue(insp.Numero, out var lista))
                    insp.Fotos = lista;
            }
        }

        public async Task<List<InspeccionFotoDto>> ObtenerFotosInspeccionAsync(int numero)
        {
            var resultado = new List<InspeccionFotoDto>();

            var query = $@"
                SELECT NUMERO, ITEM, RUTA_FOTO_H, FCH_FOTO_H, UBICA_FOTO_H,
                       RUTA_FOTO_AC, FCH_FOTO_AC, UBICA_FOTO_AC, ESTADO
                FROM {S}SI_INSPECCION_F
                WHERE NUMERO = :pNumero AND ESTADO <> '9'
                ORDER BY ITEM";

            try
            {
                using var connection = new OracleConnection(GetOracleConnectionString());
                await connection.OpenAsync();
                using var cmd = new OracleCommand(query, connection);
                cmd.BindByName = true;
                cmd.Parameters.Add("pNumero", OracleDbType.Int32).Value = numero;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    resultado.Add(new InspeccionFotoDto
                    {
                        Numero = reader.GetInt32(0),
                        Item = reader.GetInt32(1),
                        RutaFotoH = reader.IsDBNull(2) ? null : reader.GetString(2),
                        FechaFotoH = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                        UbicaFotoH = reader.IsDBNull(4) ? null : reader.GetString(4),
                        RutaFotoAc = reader.IsDBNull(5) ? null : reader.GetString(5),
                        FechaFotoAc = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                        UbicaFotoAc = reader.IsDBNull(7) ? null : reader.GetString(7),
                        Estado = reader.IsDBNull(8) ? null : reader.GetString(8)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener fotos de inspección {Numero}", numero);
                throw;
            }

            return resultado;
        }

        public async Task<int> AgregarFotoHallazgoAsync(int numero, string rutaFoto, string ubicaFoto, string usuario)
        {
            using var connection = new OracleConnection(GetOracleConnectionString());
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                // 1. Verificar estado cabecera
                var queryLock = $"SELECT ESTADO FROM {S}SI_INSPECCION WHERE NUMERO = :pNumero FOR UPDATE NOWAIT";
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
                    throw new InvalidOperationException($"La inspección #{numero} está anulada.");

                // 2. Obtener siguiente ITEM
                var queryMaxItem = $@"
                    SELECT NVL(MAX(ITEM), 0) FROM {S}SI_INSPECCION_F WHERE NUMERO = :pNumero";
                int maxItem;
                using (var cmdMax = new OracleCommand(queryMaxItem, connection))
                {
                    cmdMax.Transaction = transaction;
                    cmdMax.CommandTimeout = CmdTimeoutSec;
                    cmdMax.BindByName = true;
                    cmdMax.Parameters.Add("pNumero", OracleDbType.Int32).Value = numero;
                    maxItem = Convert.ToInt32(await cmdMax.ExecuteScalarAsync());
                }

                int nuevoItem = maxItem + 1;
                if (nuevoItem > 10)
                    throw new InvalidOperationException($"La inspección #{numero} ya tiene el máximo de 10 hallazgos.");

                // 3. Construir ruta
                var nombreArchivo = $"{numero}-{nuevoItem}-H.jpg";
                var rutaFotoCompleta = Path.Combine(rutaFoto, nombreArchivo);

                // 4. Insertar en SI_INSPECCION_F
                var queryInsert = $@"
                    INSERT INTO {S}SI_INSPECCION_F
                    (NUMERO, ITEM, RUTA_FOTO_H, FCH_FOTO_H, UBICA_FOTO_H, ESTADO, A_ADUSER, A_ADFECHA)
                    VALUES
                    (:pNumero, :pItem, :pRutaFoto, SYSDATE, :pUbicaFoto, '1', :pUsuario, SYSDATE)";
                using (var cmdIns = new OracleCommand(queryInsert, connection))
                {
                    cmdIns.Transaction = transaction;
                    cmdIns.CommandTimeout = CmdTimeoutSec;
                    cmdIns.BindByName = true;
                    cmdIns.Parameters.Add("pNumero", OracleDbType.Int32).Value = numero;
                    cmdIns.Parameters.Add("pItem", OracleDbType.Int32).Value = nuevoItem;
                    cmdIns.Parameters.Add("pRutaFoto", OracleDbType.Varchar2).Value = rutaFotoCompleta;
                    cmdIns.Parameters.Add("pUbicaFoto", OracleDbType.Varchar2).Value = ubicaFoto;
                    cmdIns.Parameters.Add("pUsuario", OracleDbType.Varchar2).Value = usuario;
                    await cmdIns.ExecuteNonQueryAsync();
                }

                // 5. Reabrir inspección si estaba cerrada
                if (estado == "6")
                {
                    var queryReabrir = $@"
                        UPDATE {S}SI_INSPECCION SET ESTADO = '1', A_MDUSER = :pUsuario, A_MDFECHA = SYSDATE WHERE NUMERO = :pNumero";
                    using var cmdReabrir = new OracleCommand(queryReabrir, connection);
                    cmdReabrir.Transaction = transaction;
                    cmdReabrir.CommandTimeout = CmdTimeoutSec;
                    cmdReabrir.BindByName = true;
                    cmdReabrir.Parameters.Add("pUsuario", OracleDbType.Varchar2).Value = usuario;
                    cmdReabrir.Parameters.Add("pNumero", OracleDbType.Int32).Value = numero;
                    await cmdReabrir.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                _logger.LogInformation("Hallazgo agregado: inspección #{Num} ítem {Item}", numero, nuevoItem);
                return nuevoItem;
            }
            catch (OracleException oraEx) when (oraEx.Number == 54)
            {
                try { await transaction.RollbackAsync(); } catch { }
                throw new InvalidOperationException("El registro está siendo modificado por otro usuario.", oraEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al agregar hallazgo a inspección #{Num}", numero);
                try { await transaction.RollbackAsync(); } catch { }
                throw;
            }
        }
    }
}
