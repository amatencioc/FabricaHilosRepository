using Oracle.ManagedDataAccess.Client;
using System.Data;
using Microsoft.AspNetCore.Http;

namespace FabricaHilos.Services.Produccion
{
    public class MotivoDto
    {
        public string Codigo      { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string TextoCompleto => $"{Codigo} - {Descripcion}";
    }

    public class ParoListDto
    {
        public DateTime FechaTurno              { get; set; }
        public string   TipoMaquina             { get; set; } = string.Empty;
        public string   DescripcionTipoMaquina  { get; set; } = string.Empty;
        public string   CodigoMaquina           { get; set; } = string.Empty;
        public string   DescripcionMaquina { get; set; } = string.Empty;
        public string   Motivo             { get; set; } = string.Empty;
        public string   DescripcionMotivo  { get; set; } = string.Empty;
        public DateTime FechaInicio        { get; set; }
        public DateTime? FechaFin          { get; set; }
        public string   Estado             { get; set; } = string.Empty;
        public string   Turno              { get; set; } = string.Empty;
        public string   AdUser             { get; set; } = string.Empty;
    }

    public class ParoItemDto
    {
        public string  FechaIni { get; set; } = string.Empty;
        public string  Motivo   { get; set; } = string.Empty;
        public string? FechaFin { get; set; }
    }

    public class GuardarParosRequest
    {
        public string         TpMaq            { get; set; } = string.Empty;
        public string         CodMaq           { get; set; } = string.Empty;
        public string         Turno            { get; set; } = string.Empty;
        public List<ParoItemDto>       Paros           { get; set; } = new();
        public List<ParoUpdateItemDto> ParosActualizar { get; set; } = new();
    }

    public class ParoUpdateItemDto
    {
        public string  OriginalFechaIni { get; set; } = string.Empty;
        public string? NuevaFechaIni    { get; set; }
        public string  Motivo           { get; set; } = string.Empty;
        public string? FechaFin         { get; set; }
    }

    public class EliminarParoBDRequest
    {
        public string TpMaq    { get; set; } = string.Empty;
        public string CodMaq   { get; set; } = string.Empty;
        public string FechaIni { get; set; } = string.Empty;
    }

    public class AgregarRolloRequest
    {
        public decimal PesoBruto  { get; set; }
        public string  FechaTurno { get; set; } = string.Empty;
        public string  Turno      { get; set; } = string.Empty;
        public string  TpMaq      { get; set; } = string.Empty;
        public string  CodMaq     { get; set; } = string.Empty;
    }

    public class CerrarBatanRequest
    {
        public int Id { get; set; }
    }

    public class ParoGuardadoDto
    {
        public string  FechaIni   { get; set; } = string.Empty;  // yyyy-MM-ddTHH:mm
        public string  Motivo     { get; set; } = string.Empty;
        public string  MotivoDesc { get; set; } = string.Empty;
        public string? FechaFin   { get; set; }                  // yyyy-MM-ddTHH:mm or null
        public bool    EsAbierto  { get; set; }
        public string  Turno      { get; set; } = string.Empty;
    }

    public class ParoPagedResult
    {
        public List<ParoListDto> Items { get; set; } = new();
        public int TotalCount          { get; set; }
    }

    public interface IParoService
    {
        Task<List<MotivoDto>>  ObtenerMotivosAsync();
        Task<ParoPagedResult>  ObtenerParosAsync(string? filtroTipoMaquina = null, string? filtroMaquina = null, List<string>? filtroEstados = null, int page = 1, int pageSize = 10);
        Task<ParoListDto?>     ObtenerParoPorClaveAsync(string tpMaq, string codMaq, DateTime fechaIni);
        Task<bool>             InsertarParoAsync(string tpMaq, string codMaq, string motivo, DateTime fechaIni, DateTime? fechaFin, string turno, string? adUser, DateTime? fechaTurno = null, string estado = "1");
        Task<bool>             ActualizarParoAsync(string oldTpMaq, string oldCodMaq, DateTime oldFechaIni, string motivo, DateTime? fechaFin, string turno, string? mdUser, DateTime? fechaTurno = null, string estado = "1");
        Task<bool>             AnularParoAsync(string tpMaq, string codMaq, DateTime fechaIni);
        Task<bool>             EliminarParoAsync(string tpMaq, string codMaq, DateTime fechaIni, string? adUser = null);
        Task<bool>             InsertarParosBatchAsync(string tpMaq, string codMaq, string turno, string? adUser, List<ParoItemDto> paros);
        Task<bool>             ActualizarParosBatchAsync(string tpMaq, string codMaq, string? adUser, List<ParoUpdateItemDto> paros);
        Task<List<ParoGuardadoDto>> ObtenerParosPorMaquinaAsync(string tpMaq, string codMaq, DateTime fechaTurno, List<string> turnos);
        Task<bool>             TieneParosAbiertosAsync(string tpMaq, string codMaq);
        Task<HashSet<string>>  ObtenerMaquinasConParosAsync(IEnumerable<(string tpMaq, string codMaq)> maquinas);
    }

    public class ParoService : OracleServiceBase, IParoService
    {
        private readonly ILogger<ParoService> _logger;

        public ParoService(IConfiguration configuration, ILogger<ParoService> logger, IHttpContextAccessor httpContextAccessor)
            : base(configuration, httpContextAccessor)
        {
            _logger = logger;
        }

        public async Task<List<MotivoDto>> ObtenerMotivosAsync()
        {
            var connectionString = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connectionString)) return new List<MotivoDto>();

            var query = $@"
                SELECT CODIGO, DESCRIPCION
                FROM {S}H_TPROD
                WHERE TABLA = '33'
                  AND CODIGO <> '....'
                ORDER BY CODIGO";

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();
                using var command = new OracleCommand(query, connection);
                using var reader  = await command.ExecuteReaderAsync();

                var motivos = new List<MotivoDto>();
                while (await reader.ReadAsync())
                {
                    motivos.Add(new MotivoDto
                    {
                        Codigo      = reader["CODIGO"]?.ToString()?.Trim()      ?? string.Empty,
                        Descripcion = reader["DESCRIPCION"]?.ToString()?.Trim() ?? string.Empty
                    });
                }
                return motivos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener motivos de paro");
                return new List<MotivoDto>();
            }
        }

        public async Task<ParoPagedResult> ObtenerParosAsync(string? filtroTipoMaquina = null, string? filtroMaquina = null, List<string>? filtroEstados = null, int page = 1, int pageSize = 10)
        {
            var connectionString = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connectionString)) return new ParoPagedResult();

            var filterSuffix = string.Empty;
            if (!string.IsNullOrEmpty(filtroTipoMaquina))
                filterSuffix += " AND R.TP_MAQ = :filtroTipoMaquina";
            if (!string.IsNullOrEmpty(filtroMaquina))
                filterSuffix += " AND R.COD_MAQ LIKE :filtroMaquina || '%'";
            if (filtroEstados != null && filtroEstados.Count > 0)
            {
                var paramNames = string.Join(", ", filtroEstados.Select((_, i) => $":filtroEstado{i}"));
                filterSuffix += $" AND R.ESTADO IN ({paramNames})";
            }

            var countQuery = $"SELECT COUNT(*) FROM {S}H_RPARADA R WHERE TRUNC(R.FECHA_TURNO) = TRUNC(SYSDATE)" + filterSuffix;

            var innerQuery = $@"
                SELECT R.TP_MAQ, R.COD_MAQ, R.MOTIVO, R.FECHA_INI, R.FECHA_FIN,
                       R.ESTADO, R.TURNO, R.FECHA_TURNO, R.A_ADUSER,
                       M.DESC_MAQ,
                       M.DESC_TPMAQ,
                       MO.DESCRIPCION AS DESC_MOTIVO
                FROM {S}H_RPARADA R
                LEFT JOIN {S}V_MAQUINA M  ON M.COD_MAQ  = R.COD_MAQ AND M.AREA = '01'
                LEFT JOIN {S}H_TPROD MO   ON MO.TABLA = '33' AND MO.CODIGO = R.MOTIVO AND MO.CODIGO <> '....'
                WHERE TRUNC(R.FECHA_TURNO) = TRUNC(SYSDATE)";
            innerQuery += filterSuffix;
            innerQuery += " ORDER BY R.FECHA_INI DESC";

            int startRow = (page - 1) * pageSize + 1;
            int endRow   = page * pageSize;

            var dataQuery = $@"
                SELECT *
                FROM (
                    SELECT t_.*, ROWNUM AS RN_
                    FROM ({innerQuery}) t_
                    WHERE ROWNUM <= :pEndRow
                ) p_
                WHERE p_.RN_ >= :pStartRow";

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();

                int totalCount = 0;
                using (var countCmd = new OracleCommand(countQuery, connection))
                {
                    countCmd.BindByName = true;
                    AddFilterParams(countCmd, filtroTipoMaquina, filtroMaquina, filtroEstados);
                    var r = await countCmd.ExecuteScalarAsync();
                    if (r != null && r != DBNull.Value) totalCount = Convert.ToInt32(r);
                }

                var items = new List<ParoListDto>();
                using (var dataCmd = new OracleCommand(dataQuery, connection))
                {
                    dataCmd.BindByName = true;
                    AddFilterParams(dataCmd, filtroTipoMaquina, filtroMaquina, filtroEstados);
                    dataCmd.Parameters.Add(new OracleParameter(":pEndRow",   OracleDbType.Int32, endRow,   ParameterDirection.Input));
                    dataCmd.Parameters.Add(new OracleParameter(":pStartRow", OracleDbType.Int32, startRow, ParameterDirection.Input));

                    using var reader = await dataCmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        items.Add(new ParoListDto
                        {
                            TipoMaquina             = reader["TP_MAQ"]?.ToString()?.Trim()       ?? string.Empty,
                            DescripcionTipoMaquina  = reader["DESC_TPMAQ"]?.ToString()?.Trim()   ?? string.Empty,
                            CodigoMaquina           = reader["COD_MAQ"]?.ToString()?.Trim()      ?? string.Empty,
                            DescripcionMaquina = reader["DESC_MAQ"]?.ToString()?.Trim()     ?? string.Empty,
                            Motivo             = reader["MOTIVO"]?.ToString()?.Trim()        ?? string.Empty,
                            DescripcionMotivo  = reader["DESC_MOTIVO"]?.ToString()?.Trim()  ?? string.Empty,
                            FechaInicio        = reader["FECHA_INI"]  != DBNull.Value ? Convert.ToDateTime(reader["FECHA_INI"])  : DateTime.MinValue,
                            FechaFin           = reader["FECHA_FIN"]  != DBNull.Value ? Convert.ToDateTime(reader["FECHA_FIN"])  : null,
                            Estado             = reader["ESTADO"]?.ToString()?.Trim()        ?? string.Empty,
                            Turno              = reader["TURNO"]?.ToString()?.Trim()         ?? string.Empty,
                            FechaTurno         = reader["FECHA_TURNO"] != DBNull.Value ? Convert.ToDateTime(reader["FECHA_TURNO"]) : DateTime.Today,
                            AdUser             = reader["A_ADUSER"]?.ToString()?.Trim()      ?? string.Empty
                        });
                    }
                }

                _logger.LogInformation("Se obtuvieron {Count} paros de {Total} totales", items.Count, totalCount);
                return new ParoPagedResult { Items = items, TotalCount = totalCount };
            }
            catch (OracleException oEx)
            {
                _logger.LogError(oEx, "Error de Oracle al obtener paros: {Msg}", oEx.Message);
                return new ParoPagedResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general al obtener paros");
                return new ParoPagedResult();
            }
        }

        public async Task<ParoListDto?> ObtenerParoPorClaveAsync(string tpMaq, string codMaq, DateTime fechaIni)
        {
            var connectionString = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connectionString)) return null;

            var query = $@"
                SELECT R.TP_MAQ, R.COD_MAQ, R.MOTIVO, R.FECHA_INI, R.FECHA_FIN,
                       R.ESTADO, R.TURNO, R.FECHA_TURNO, R.A_ADUSER,
                       M.DESC_MAQ,
                       M.DESC_TPMAQ,
                       MO.DESCRIPCION AS DESC_MOTIVO
                FROM {S}H_RPARADA R
                LEFT JOIN {S}V_MAQUINA M  ON M.COD_MAQ  = R.COD_MAQ AND M.AREA = '01'
                LEFT JOIN {S}H_TPROD MO   ON MO.TABLA = '33' AND MO.CODIGO = R.MOTIVO AND MO.CODIGO <> '....'
                WHERE TRIM(R.TP_MAQ)  = TRIM(:tpMaq)
                  AND TRIM(R.COD_MAQ) = TRIM(:codMaq)
                  AND TO_CHAR(R.FECHA_INI, 'YYYY-MM-DD HH24:MI:SS') = :fechaIni
                  AND ROWNUM = 1";

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();
                using var command = new OracleCommand(query, connection);
                command.BindByName = true;
                command.Parameters.Add(new OracleParameter(":tpMaq",    OracleDbType.Varchar2) { Value = tpMaq });
                command.Parameters.Add(new OracleParameter(":codMaq",   OracleDbType.Varchar2) { Value = codMaq });
                command.Parameters.Add(new OracleParameter(":fechaIni", OracleDbType.Varchar2) { Value = fechaIni.ToString("yyyy-MM-dd HH:mm:ss") });

                using var reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync()) return null;

                return new ParoListDto
                {
                    TipoMaquina            = reader["TP_MAQ"]?.ToString()?.Trim()       ?? string.Empty,
                    DescripcionTipoMaquina = reader["DESC_TPMAQ"]?.ToString()?.Trim()   ?? string.Empty,
                    CodigoMaquina          = reader["COD_MAQ"]?.ToString()?.Trim()      ?? string.Empty,
                    DescripcionMaquina     = reader["DESC_MAQ"]?.ToString()?.Trim()     ?? string.Empty,
                    Motivo                 = reader["MOTIVO"]?.ToString()?.Trim()        ?? string.Empty,
                    DescripcionMotivo      = reader["DESC_MOTIVO"]?.ToString()?.Trim()  ?? string.Empty,
                    FechaInicio            = reader["FECHA_INI"]  != DBNull.Value ? Convert.ToDateTime(reader["FECHA_INI"])  : DateTime.MinValue,
                    FechaFin               = reader["FECHA_FIN"]  != DBNull.Value ? Convert.ToDateTime(reader["FECHA_FIN"])  : null,
                    Estado                 = reader["ESTADO"]?.ToString()?.Trim()        ?? string.Empty,
                    Turno                  = reader["TURNO"]?.ToString()?.Trim()         ?? string.Empty,
                    FechaTurno             = reader["FECHA_TURNO"] != DBNull.Value ? Convert.ToDateTime(reader["FECHA_TURNO"]) : DateTime.Today,
                    AdUser                 = reader["A_ADUSER"]?.ToString()?.Trim()      ?? string.Empty
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener paro por clave");
                return null;
            }
        }

        public async Task<bool> InsertarParoAsync(string tpMaq, string codMaq, string motivo, DateTime fechaIni, DateTime? fechaFin, string turno, string? adUser, DateTime? fechaTurno = null, string estado = "1")
        {
            var connectionString = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connectionString)) return false;

            // FECHA_TURNO: usar fecha manual si se proporcionó; si no, calcular por regla de hora
            var fechaTurnoEfectiva = fechaTurno?.Date ?? (fechaIni.Hour < 7 ? fechaIni.Date.AddDays(-1) : fechaIni.Date);

            var query = $@"
                INSERT INTO {S}H_RPARADA (
                    FECHA_TURNO, TP_MAQ, COD_MAQ, MOTIVO,
                    FECHA_INI, FECHA_FIN, ESTADO,
                    A_ADUSER, A_ADFECHA, TURNO
                ) VALUES (
                    :fechaTurno, :tpMaq, :codMaq, :motivo,
                    :fechaIni, :fechaFin, :estado,
                    :adUser, SYSDATE, :turno
                )";

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();
                using var command = new OracleCommand(query, connection);
                command.BindByName = true;

                command.Parameters.Add(new OracleParameter(":fechaTurno", OracleDbType.Date)    { Value = fechaTurnoEfectiva });
                command.Parameters.Add(new OracleParameter(":tpMaq",      OracleDbType.Varchar2) { Value = tpMaq });
                command.Parameters.Add(new OracleParameter(":codMaq",     OracleDbType.Varchar2) { Value = codMaq });
                command.Parameters.Add(new OracleParameter(":motivo",     OracleDbType.Varchar2) { Value = motivo });
                command.Parameters.Add(new OracleParameter(":fechaIni",   OracleDbType.Date)     { Value = fechaIni });
                command.Parameters.Add(new OracleParameter(":fechaFin",   OracleDbType.Date)     { Value = fechaFin.HasValue ? (object)fechaFin.Value : DBNull.Value });
                command.Parameters.Add(new OracleParameter(":adUser",     OracleDbType.Varchar2) { Value = adUser ?? string.Empty });
                command.Parameters.Add(new OracleParameter(":turno",      OracleDbType.Varchar2) { Value = turno });
                command.Parameters.Add(new OracleParameter(":estado",     OracleDbType.Varchar2) { Value = estado });

                var rows = await command.ExecuteNonQueryAsync();
                _logger.LogInformation("Paro insertado en H_RPARADA. Filas: {Rows}", rows);
                return rows > 0;
            }
            catch (OracleException oEx)
            {
                _logger.LogError(oEx, "Error de Oracle al insertar paro: {Msg}", oEx.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general al insertar paro");
                return false;
            }
        }

        public async Task<bool> ActualizarParoAsync(string oldTpMaq, string oldCodMaq, DateTime oldFechaIni, string motivo, DateTime? fechaFin, string turno, string? mdUser, DateTime? fechaTurno = null, string estado = "1")
        {
            var connectionString = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connectionString)) return false;

            var query = $@"
                UPDATE {S}H_RPARADA
                SET MOTIVO      = :motivo,
                    FECHA_FIN   = :fechaFin,
                    TURNO       = :turno,
                    FECHA_TURNO = NVL(:fechaTurno, FECHA_TURNO),
                    ESTADO      = :estado,
                    A_MDUSER    = :mdUser,
                    A_MDFECHA   = SYSDATE
                WHERE TRIM(TP_MAQ)  = TRIM(:oldTpMaq)
                  AND TRIM(COD_MAQ) = TRIM(:oldCodMaq)
                  AND TO_CHAR(FECHA_INI, 'YYYY-MM-DD HH24:MI:SS') = :oldFechaIni
                  AND ESTADO = '1'";

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();
                using var command = new OracleCommand(query, connection);
                command.BindByName = true;

                command.Parameters.Add(new OracleParameter(":motivo",      OracleDbType.Varchar2) { Value = motivo });
                command.Parameters.Add(new OracleParameter(":fechaFin",    OracleDbType.Date)     { Value = fechaFin.HasValue ? (object)fechaFin.Value : DBNull.Value });
                command.Parameters.Add(new OracleParameter(":turno",       OracleDbType.Varchar2) { Value = turno });
                command.Parameters.Add(new OracleParameter(":fechaTurno",  OracleDbType.Date)     { Value = fechaTurno.HasValue ? (object)fechaTurno.Value.Date : DBNull.Value });
                command.Parameters.Add(new OracleParameter(":estado",      OracleDbType.Varchar2) { Value = estado });
                command.Parameters.Add(new OracleParameter(":mdUser",      OracleDbType.Varchar2) { Value = mdUser ?? string.Empty });
                command.Parameters.Add(new OracleParameter(":oldTpMaq",    OracleDbType.Varchar2) { Value = oldTpMaq });
                command.Parameters.Add(new OracleParameter(":oldCodMaq",   OracleDbType.Varchar2) { Value = oldCodMaq });
                command.Parameters.Add(new OracleParameter(":oldFechaIni", OracleDbType.Varchar2) { Value = oldFechaIni.ToString("yyyy-MM-dd HH:mm:ss") });

                var rows = await command.ExecuteNonQueryAsync();
                _logger.LogInformation("Paro actualizado en H_RPARADA. Filas: {Rows}", rows);
                return rows > 0;
            }
            catch (OracleException oEx)
            {
                _logger.LogError(oEx, "Error de Oracle al actualizar paro: {Msg}", oEx.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general al actualizar paro");
                return false;
            }
        }

        public async Task<bool> AnularParoAsync(string tpMaq, string codMaq, DateTime fechaIni)
        {
            var connectionString = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connectionString)) return false;

            var query = $@"
                UPDATE {S}H_RPARADA
                SET ESTADO = '9'
                WHERE TRIM(TP_MAQ)  = TRIM(:tpMaq)
                  AND TRIM(COD_MAQ) = TRIM(:codMaq)
                  AND TO_CHAR(FECHA_INI, 'YYYY-MM-DD HH24:MI:SS') = :fechaIni
                  AND ESTADO = '1'";

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();
                using var command = new OracleCommand(query, connection);
                command.BindByName = true;

                command.Parameters.Add(new OracleParameter(":tpMaq",    OracleDbType.Varchar2) { Value = tpMaq });
                command.Parameters.Add(new OracleParameter(":codMaq",   OracleDbType.Varchar2) { Value = codMaq });
                command.Parameters.Add(new OracleParameter(":fechaIni", OracleDbType.Varchar2) { Value = fechaIni.ToString("yyyy-MM-dd HH:mm:ss") });

                var rows = await command.ExecuteNonQueryAsync();
                _logger.LogInformation("Paro anulado en H_RPARADA. Filas: {Rows}", rows);
                return rows > 0;
            }
            catch (OracleException oEx)
            {
                _logger.LogError(oEx, "Error de Oracle al anular paro: {Msg}", oEx.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general al anular paro");
                return false;
            }
        }

        public async Task<bool> InsertarParosBatchAsync(string tpMaq, string codMaq, string turno, string? adUser, List<ParoItemDto> paros)
        {
            var connectionString = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connectionString) || paros == null || paros.Count == 0) return false;

            var query = $@"
                INSERT INTO {S}H_RPARADA (
                    FECHA_TURNO, TP_MAQ, COD_MAQ, MOTIVO,
                    FECHA_INI, FECHA_FIN, ESTADO,
                    A_ADUSER, A_ADFECHA, TURNO
                ) VALUES (
                    :fechaTurno, :tpMaq, :codMaq, :motivo,
                    :fechaIni, :fechaFin, :estado,
                    :adUser, SYSDATE, :turno
                )";

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();
                using var transaction = connection.BeginTransaction();

                foreach (var paro in paros)
                {
                    if (!DateTime.TryParse(paro.FechaIni, out var fechaIni)) continue;

                    DateTime? fechaFin = null;
                    if (!string.IsNullOrEmpty(paro.FechaFin) && DateTime.TryParse(paro.FechaFin, out var fin))
                        fechaFin = fin;

                    var fechaTurno = fechaIni.Hour < 7 ? fechaIni.Date.AddDays(-1) : fechaIni.Date;
                    var estado     = fechaFin.HasValue ? "3" : "1";

                    using var cmd = new OracleCommand(query, connection);
                    cmd.Transaction = transaction;
                    cmd.BindByName  = true;
                    cmd.Parameters.Add(new OracleParameter(":fechaTurno", OracleDbType.Date)     { Value = fechaTurno });
                    cmd.Parameters.Add(new OracleParameter(":tpMaq",      OracleDbType.Varchar2)  { Value = tpMaq });
                    cmd.Parameters.Add(new OracleParameter(":codMaq",     OracleDbType.Varchar2)  { Value = codMaq });
                    cmd.Parameters.Add(new OracleParameter(":motivo",     OracleDbType.Varchar2)  { Value = paro.Motivo });
                    cmd.Parameters.Add(new OracleParameter(":fechaIni",   OracleDbType.Date)      { Value = fechaIni });
                    cmd.Parameters.Add(new OracleParameter(":fechaFin",   OracleDbType.Date)      { Value = fechaFin.HasValue ? (object)fechaFin.Value : DBNull.Value });
                    cmd.Parameters.Add(new OracleParameter(":adUser",     OracleDbType.Varchar2)  { Value = adUser ?? string.Empty });
                    cmd.Parameters.Add(new OracleParameter(":turno",      OracleDbType.Varchar2)  { Value = turno });
                    cmd.Parameters.Add(new OracleParameter(":estado",     OracleDbType.Varchar2)  { Value = estado });

                    await cmd.ExecuteNonQueryAsync();
                }

                transaction.Commit();
                _logger.LogInformation("Lote de {Count} paros insertado en H_RPARADA", paros.Count);
                return true;
            }
            catch (OracleException oEx)
            {
                _logger.LogError(oEx, "Error de Oracle al insertar lote de paros: {Msg}", oEx.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general al insertar lote de paros");
                return false;
            }
        }

        public async Task<bool> ActualizarParosBatchAsync(string tpMaq, string codMaq, string? adUser, List<ParoUpdateItemDto> paros)
        {
            var connectionString = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connectionString) || paros == null || paros.Count == 0) return false;

            var query = $@"
                UPDATE {S}H_RPARADA
                SET MOTIVO    = :motivo,
                    FECHA_FIN = :fechaFin,
                    FECHA_INI = :nuevaFechaIni,
                    ESTADO    = :estado,
                    A_MDUSER  = :mdUser,
                    A_MDFECHA = SYSDATE
                WHERE TRIM(TP_MAQ)  = TRIM(:tpMaq)
                  AND TRIM(COD_MAQ) = TRIM(:codMaq)
                  AND TO_CHAR(FECHA_INI, 'YYYY-MM-DD HH24:MI:SS') = :fechaIni";

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();
                using var transaction = connection.BeginTransaction();

                foreach (var paro in paros)
                {
                    if (string.IsNullOrEmpty(paro.OriginalFechaIni)) continue;
                    if (!DateTime.TryParse(paro.OriginalFechaIni, out var originalFechaIni)) continue;

                    DateTime? fechaFin = null;
                    if (!string.IsNullOrEmpty(paro.FechaFin) && DateTime.TryParse(paro.FechaFin, out var fin))
                        fechaFin = fin;

                    var estado = fechaFin.HasValue ? "3" : "1";

                    var nuevaFechaIni = originalFechaIni;
                    if (!string.IsNullOrEmpty(paro.NuevaFechaIni) && DateTime.TryParse(paro.NuevaFechaIni, out var nfi))
                        nuevaFechaIni = nfi;

                    using var cmd = new OracleCommand(query, connection);
                    cmd.Transaction = transaction;
                    cmd.BindByName  = true;
                    cmd.Parameters.Add(new OracleParameter(":motivo",       OracleDbType.Varchar2) { Value = paro.Motivo });
                    cmd.Parameters.Add(new OracleParameter(":fechaFin",     OracleDbType.Date)     { Value = fechaFin.HasValue ? (object)fechaFin.Value : DBNull.Value });
                    cmd.Parameters.Add(new OracleParameter(":nuevaFechaIni",OracleDbType.Date)     { Value = nuevaFechaIni });
                    cmd.Parameters.Add(new OracleParameter(":estado",       OracleDbType.Varchar2) { Value = estado });
                    cmd.Parameters.Add(new OracleParameter(":mdUser",       OracleDbType.Varchar2) { Value = adUser ?? string.Empty });
                    cmd.Parameters.Add(new OracleParameter(":tpMaq",        OracleDbType.Varchar2) { Value = tpMaq });
                    cmd.Parameters.Add(new OracleParameter(":codMaq",       OracleDbType.Varchar2) { Value = codMaq });
                    cmd.Parameters.Add(new OracleParameter(":fechaIni",     OracleDbType.Varchar2) { Value = originalFechaIni.ToString("yyyy-MM-dd HH:mm:ss") });

                    await cmd.ExecuteNonQueryAsync();
                }

                transaction.Commit();
                _logger.LogInformation("Lote de {Count} paros actualizados en H_RPARADA", paros.Count);
                return true;
            }
            catch (OracleException oEx)
            {
                _logger.LogError(oEx, "Error de Oracle al actualizar lote de paros: {Msg}", oEx.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general al actualizar lote de paros");
                return false;
            }
        }

        public async Task<List<ParoGuardadoDto>> ObtenerParosPorMaquinaAsync(string tpMaq, string codMaq, DateTime fechaTurno, List<string> turnos)
        {
            var connectionString = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connectionString)) return new List<ParoGuardadoDto>();
            if (turnos == null || turnos.Count == 0) return new List<ParoGuardadoDto>();

            var turnoParams = string.Join(", ", Enumerable.Range(0, turnos.Count).Select(i => $":turno{i}"));
            var query = $@"
                SELECT R.MOTIVO, R.FECHA_INI, R.FECHA_FIN, R.ESTADO, R.TURNO,
                       MO.DESCRIPCION AS DESC_MOTIVO
                FROM {S}H_RPARADA R
                LEFT JOIN {S}H_TPROD MO ON MO.TABLA = '33' AND MO.CODIGO = R.MOTIVO AND MO.CODIGO <> '....'
                WHERE TRIM(R.TP_MAQ) = TRIM(:tpMaq)
                  AND TRIM(R.COD_MAQ) = TRIM(:codMaq)
                  AND R.ESTADO <> '9'
                  AND TRUNC(R.FECHA_TURNO) = TRUNC(:fechaTurno)
                  AND R.TURNO IN ({turnoParams})
                ORDER BY R.FECHA_INI DESC";

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();
                using var command = new OracleCommand(query, connection);
                command.BindByName = true;
                command.Parameters.Add(new OracleParameter(":tpMaq",      OracleDbType.Varchar2) { Value = tpMaq });
                command.Parameters.Add(new OracleParameter(":codMaq",     OracleDbType.Varchar2) { Value = codMaq });
                command.Parameters.Add(new OracleParameter(":fechaTurno", OracleDbType.Date)     { Value = fechaTurno.Date });
                for (int i = 0; i < turnos.Count; i++)
                    command.Parameters.Add(new OracleParameter($":turno{i}", OracleDbType.Varchar2) { Value = turnos[i] });

                var result = new List<ParoGuardadoDto>();
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var fechaIni = reader["FECHA_INI"] != DBNull.Value ? Convert.ToDateTime(reader["FECHA_INI"]) : (DateTime?)null;
                    var fechaFin = reader["FECHA_FIN"] != DBNull.Value ? Convert.ToDateTime(reader["FECHA_FIN"]) : (DateTime?)null;
                    var estado   = reader["ESTADO"]?.ToString()?.Trim() ?? string.Empty;
                    var motivo   = reader["MOTIVO"]?.ToString()?.Trim() ?? string.Empty;
                    var desc     = reader["DESC_MOTIVO"]?.ToString()?.Trim() ?? motivo;

                    result.Add(new ParoGuardadoDto
                    {
                        FechaIni   = fechaIni.HasValue ? fechaIni.Value.ToString("yyyy-MM-ddTHH:mm") : string.Empty,
                        Motivo     = motivo,
                        MotivoDesc = desc,
                        FechaFin   = fechaFin.HasValue ? fechaFin.Value.ToString("yyyy-MM-ddTHH:mm") : null,
                        EsAbierto  = estado == "1",
                        Turno      = reader["TURNO"]?.ToString()?.Trim() ?? string.Empty
                    });
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener paros por máquina: {TpMaq}/{CodMaq}", tpMaq, codMaq);
                return new List<ParoGuardadoDto>();
            }
        }

        public async Task<bool> EliminarParoAsync(string tpMaq, string codMaq, DateTime fechaIni, string? adUser = null)
        {
            var connectionString = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connectionString)) return false;

            var query = $@"
                UPDATE {S}H_RPARADA
                SET ESTADO    = '9',
                    A_MDUSER  = :adUser,
                    A_MDFECHA = SYSDATE
                WHERE TRIM(TP_MAQ)  = TRIM(:tpMaq)
                  AND TRIM(COD_MAQ) = TRIM(:codMaq)
                  AND TO_CHAR(FECHA_INI, 'YYYY-MM-DD HH24:MI:SS') = :fechaIni";

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();
                using var command = new OracleCommand(query, connection);
                command.BindByName = true;
                command.Parameters.Add(new OracleParameter(":adUser",   OracleDbType.Varchar2) { Value = adUser ?? string.Empty });
                command.Parameters.Add(new OracleParameter(":tpMaq",    OracleDbType.Varchar2) { Value = tpMaq });
                command.Parameters.Add(new OracleParameter(":codMaq",   OracleDbType.Varchar2) { Value = codMaq });
                command.Parameters.Add(new OracleParameter(":fechaIni", OracleDbType.Varchar2) { Value = fechaIni.ToString("yyyy-MM-dd HH:mm:ss") });
                var rows = await command.ExecuteNonQueryAsync();
                _logger.LogInformation("Paro eliminado (anulado) en H_RPARADA. Filas: {Rows}", rows);
                return rows > 0;
            }
            catch (OracleException oEx)
            {
                _logger.LogError(oEx, "Error de Oracle al eliminar paro: {Msg}", oEx.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general al eliminar paro");
                return false;
            }
        }

        public async Task<bool> TieneParosAbiertosAsync(string tpMaq, string codMaq)
        {
            var connectionString = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connectionString)) return false;

            var query = $@"
                SELECT COUNT(*) FROM {S}H_RPARADA
                WHERE TRIM(TP_MAQ) = TRIM(:tpMaq)
                  AND TRIM(COD_MAQ) = TRIM(:codMaq)
                  AND ESTADO = '1'";

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();
                using var command = new OracleCommand(query, connection);
                command.BindByName = true;
                command.Parameters.Add(new OracleParameter(":tpMaq",  OracleDbType.Varchar2) { Value = tpMaq });
                command.Parameters.Add(new OracleParameter(":codMaq", OracleDbType.Varchar2) { Value = codMaq });
                var result = await command.ExecuteScalarAsync();
                return result != null && result != DBNull.Value && Convert.ToInt32(result) > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar paros abiertos: {TpMaq}/{CodMaq}", tpMaq, codMaq);
                return false;
            }
        }

        public async Task<HashSet<string>> ObtenerMaquinasConParosAsync(IEnumerable<(string tpMaq, string codMaq)> maquinas)
        {
            var maquinaList = maquinas.ToList();
            if (maquinaList.Count == 0) return new HashSet<string>();

            var connectionString = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connectionString)) return new HashSet<string>();

            var conditions = new List<string>();
            for (int i = 0; i < maquinaList.Count; i++)
                conditions.Add($"(TRIM(TP_MAQ) = :tp{i} AND TRIM(COD_MAQ) = :cod{i})");

            var query = $@"
                SELECT DISTINCT TRIM(TP_MAQ) AS TP_MAQ, TRIM(COD_MAQ) AS COD_MAQ
                FROM {S}H_RPARADA
                WHERE ({string.Join(" OR ", conditions)})
                  AND ESTADO <> '9'
                  AND TRUNC(FECHA_TURNO) = TRUNC(SYSDATE)";

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();
                using var command = new OracleCommand(query, connection);
                command.BindByName = true;
                for (int i = 0; i < maquinaList.Count; i++)
                {
                    command.Parameters.Add(new OracleParameter($":tp{i}",  OracleDbType.Varchar2) { Value = maquinaList[i].tpMaq });
                    command.Parameters.Add(new OracleParameter($":cod{i}", OracleDbType.Varchar2) { Value = maquinaList[i].codMaq });
                }
                var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var tp  = reader["TP_MAQ"]?.ToString()?.Trim() ?? string.Empty;
                    var cod = reader["COD_MAQ"]?.ToString()?.Trim() ?? string.Empty;
                    result.Add($"{tp}|{cod}");
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener máquinas con paros");
                return new HashSet<string>();
            }
        }

        private static void AddFilterParams(OracleCommand cmd, string? filtroTipoMaquina, string? filtroMaquina, List<string>? filtroEstados)
        {
            if (!string.IsNullOrEmpty(filtroTipoMaquina))
                cmd.Parameters.Add(new OracleParameter(":filtroTipoMaquina", OracleDbType.Varchar2, filtroTipoMaquina, ParameterDirection.Input));
            if (!string.IsNullOrEmpty(filtroMaquina))
                cmd.Parameters.Add(new OracleParameter(":filtroMaquina", OracleDbType.Varchar2, filtroMaquina, ParameterDirection.Input));
            if (filtroEstados != null && filtroEstados.Count > 0)
            {
                for (int i = 0; i < filtroEstados.Count; i++)
                    cmd.Parameters.Add(new OracleParameter($":filtroEstado{i}", OracleDbType.Varchar2, filtroEstados[i], ParameterDirection.Input));
            }
        }
    }
}
