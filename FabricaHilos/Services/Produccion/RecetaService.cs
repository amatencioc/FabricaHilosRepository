using Oracle.ManagedDataAccess.Client;
using System.Data;
using FabricaHilos.Models.Produccion;
using Microsoft.AspNetCore.Http;

namespace FabricaHilos.Services.Produccion
{
    public class RecetaDto
    {
        public string Numero { get; set; } = string.Empty;
        public string Material { get; set; } = string.Empty;
        public string Lote { get; set; } = string.Empty;
    }

    public class LoteDto
    {
        public string Lote { get; set; } = string.Empty;
        public string Receta { get; set; } = string.Empty;
        public string Material { get; set; } = string.Empty;
    }

    public class MaquinaDto
    {
        public string TipoMaquina { get; set; } = string.Empty;
        public string DescripcionTipoMaquina { get; set; } = string.Empty;
        public string TextoCompleto => $"{TipoMaquina} - {DescripcionTipoMaquina}";
    }

    public class MaquinaIndividualDto
    {
        public string CodigoMaquina { get; set; } = string.Empty;
        public string DescripcionMaquina { get; set; } = string.Empty;
        public string TextoCompleto => DescripcionMaquina;
    }

    public class TituloDto
    {
        public string Titulo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string TextoCompleto => Descripcion;
    }

    public class EmpleadoOracleDto
    {
        public string Codigo { get; set; } = string.Empty;
        public string NombreCorto { get; set; } = string.Empty;
        public string TextoCompleto => NombreCorto;
    }

    public class DestinoDto
    {
        public string Codigo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
    }

    public class PartidaDto
    {
        public string Guia { get; set; } = string.Empty;
        public string Partida { get; set; } = string.Empty;
        public string Material { get; set; } = string.Empty;
        public string Lote { get; set; } = string.Empty;
        public string DescCliente { get; set; } = string.Empty;
        public string Titulo { get; set; } = string.Empty;
    }

    public class PreparatoriaListDto
    {
        public int? LocalId { get; set; }
        public string Receta { get; set; } = string.Empty;
        public string Lote { get; set; } = string.Empty;
        public string Material { get; set; } = string.Empty;
        public string TipoMaquina { get; set; } = string.Empty;
        public string CodigoMaquina { get; set; } = string.Empty;
        public string DescripcionMaquina { get; set; } = string.Empty;
        public string Titulo { get; set; } = string.Empty;
        public string DescripcionTitulo { get; set; } = string.Empty;
        public DateTime FechaInicio { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string CodigoOperario { get; set; } = string.Empty;
        public string NombreOperario { get; set; } = string.Empty;
        public string Turno { get; set; } = string.Empty;
        public string PasoManual { get; set; } = string.Empty;
        public bool TieneParos { get; set; }
        public int     CantRollos { get; set; }
        public decimal TotalNeto  { get; set; }
    }

    public class DetalleProductivoOracleDto
    {
        public decimal? Velocidad { get; set; }
        public decimal? Metraje { get; set; }
        public int? Unidades { get; set; }
        public decimal? PesoNeto { get; set; }
        public DateTime? FechaFin { get; set; }
        public decimal? ProducTeorico { get; set; }
        public decimal? ProdEsperado { get; set; }
        public decimal? HusosInac { get; set; }
        public int? NroParada { get; set; }
        public decimal? ContadorIni { get; set; }
        public decimal? ContadorFin { get; set; }
        public string?  EstadoOracle { get; set; }
    }

    public class GuardarCerrarResultado
    {
        public bool UpdateExitoso { get; set; }
        public string Codigo { get; set; } = "0";
        public string Mensaje { get; set; } = string.Empty;
    }

    public class PreparatoriaPagedResult
    {
        public List<PreparatoriaListDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
    }

    public class RolloDto
    {
        public int Item { get; set; }
        public decimal Neto { get; set; }
        public DateTime? FechaRegistro { get; set; }
    }

    public interface IRecetaService
    {
        Task<List<RecetaDto>> BuscarRecetaPorCodigoAsync(string codigo);
        Task<List<LoteDto>> BuscarLotePorCodigoAsync(string codigo);
        Task<List<PartidaDto>> BuscarPartidaPorGuiaAsync(string guia);
        Task<List<MaquinaDto>> ObtenerTiposMaquinasAsync();
        Task<List<MaquinaIndividualDto>> ObtenerMaquinasPorTipoAsync(string tipoMaquina);
        Task<List<TituloDto>> ObtenerTitulosAsync();
        Task<List<TituloDto>> ObtenerTitulosAutoconerAsync();
        Task<List<EmpleadoOracleDto>> ObtenerEmpleadosAsync();
        Task<List<EmpleadoOracleDto>> BuscarOperarioAsync(string codigo);
        Task<string> ObtenerNombreEmpleadoAsync(string codigo);
        Task<List<DestinoDto>> ObtenerDestinosAutoconerAsync();
        Task<bool> InsertarPreparatoriaAsync(OrdenProduccion orden, string? adUser = null);
        Task<bool> InsertarPreparatoriaAutoconerAsync(RegistroAutoconer registro, string? adUser = null);
        Task<decimal> ObtenerPesoTituloAsync(string titulo);
        Task<int> ObtenerHusosMaquinaAsync(string tpMaq, string codMaq);
        Task<PreparatoriaPagedResult> ObtenerPreparatoriasAsync(string? filtroLote = null, string? filtroMaquina = null, string? filtroTipoMaquina = null, List<string>? filtroEstados = null, int page = 1, int pageSize = 10);
        Task<bool> TieneMaquinaEnProcesoAsync(string tpMaq, string codMaq);
        Task<bool> CerrarPreparatoriaOracleAsync(string? receta, string? lote, string? tpMaq, string? codMaq, string? titulo, DateTime fechaIni, string? mdUser = null);
        Task<bool> AnularPreparatoriaOracleAsync(string? receta, string? lote, string? tpMaq, string? codMaq, string? titulo, DateTime fechaIni);
        Task<bool> ActualizarPreparatoriaOracleAsync(
            string? oldReceta, string? oldLote, string? oldTpMaq, string? oldCodMaq, string? oldTitulo, DateTime fechaIni,
            string? newReceta, string? newLote, string? newTpMaq, string? newCodMaq, string? newTitulo,
            string? cCodigo, string? turno, string? pasoManuar, DateTime newFechaIni,
            decimal? contadorInicial = null, decimal? husosInactivas = null, string? mdUser = null, decimal? velocidad = null, decimal? metraje = null);
        Task<GuardarCerrarResultado> GuardarYCerrarDetalleProduccionAsync(
            string? receta, string? lote, string? tpMaq, string? codMaq, string? titulo, DateTime fechaIni,
            decimal? velocidad, int? rolloTacho, decimal? kgNeto,
            int? nroParada = null, decimal? contadorFinal = null, DateTime? fechaFin = null);
        Task<DetalleProductivoOracleDto?> ObtenerDetalleProductivoOracleAsync(
            string? receta, string? lote, string? tpMaq, string? codMaq, string? titulo, DateTime fechaIni);
        Task<bool> AgregarRolloAsync(DateTime fechaTurno, string turno, string tpMaq, string codMaq, decimal neto, string? adUser);
        Task<List<RolloDto>> ObtenerRollosPorMaquinaAsync(DateTime fechaTurno, string turno, string tpMaq, string codMaq, DateTime? fechaIni = null, DateTime? fechaFin = null);
        Task<bool> ActualizarUltimoRolloBatanAsync(DateTime fechaTurno, string turno, string tpMaq, string codMaq, DateTime fechaIni, string? mdUser);
    }

    public class RecetaService : IRecetaService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<RecetaService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public RecetaService(IConfiguration configuration, ILogger<RecetaService> logger, IHttpContextAccessor httpContextAccessor)
        {
            _configuration = configuration;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Construye la cadena de conexión Oracle usando las credenciales del usuario
        /// actualmente logueado (guardadas en sesión). Si no hay sesión activa,
        /// usa la cadena de conexión por defecto del appsettings.json.
        /// </summary>
        private string GetOracleConnectionString()
        {
            var oraUser = _httpContextAccessor.HttpContext?.Session.GetString("OracleUser");
            var oraPass = _httpContextAccessor.HttpContext?.Session.GetString("OraclePass");
            var baseConnStr = _configuration.GetConnectionString("OracleConnection") ?? string.Empty;

            if (!string.IsNullOrEmpty(oraUser) && !string.IsNullOrEmpty(oraPass))
            {
                var csBuilder = new OracleConnectionStringBuilder(baseConnStr)
                {
                    UserID   = oraUser,
                    Password = oraPass
                };
                _logger.LogDebug("Usando conexión Oracle como usuario: {OraUser}", oraUser);
                return csBuilder.ToString();
            }

            return baseConnStr;
        }

        public async Task<List<RecetaDto>> BuscarRecetaPorCodigoAsync(string codigo)
        {
            var connectionString = GetOracleConnectionString();

            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogWarning("Oracle connection string not configured");
                return null;
            }

            _logger.LogInformation("Buscando receta con código: {Codigo}", codigo);

            const string query = @"
                SELECT 
                    G.NUMERO AS NUMERO_RECETA,
                    F.ABREVIADO||' '||P.ABREVIADO||' '||V.ABREVIADO||' ('||I.COLOR_DET||')' AS DESCRIPCION_MATERIAL,
                    G.LOTE AS CODIGO_LOTE
                FROM H_RECETA_G G,
                     H_FIBRA F,
                     H_PROCESOS P,
                     ITEMPED I,
                     V_TFIBRA T,
                     V_VALPF V,
                     CLIENTES C
                WHERE G.TIPO = 'R'
                  AND NVL(G.ESTADO,'1') <> '9'
                  AND G.FECHA BETWEEN ADD_MONTHS(TRUNC(SYSDATE),-4) AND TRUNC(SYSDATE)
                  AND F.FIBRA = G.FIBRA
                  AND P.PROCESO = G.PROCESO
                  AND I.NUM_PED = G.NUM_PED
                  AND I.NRO = G.ITEM_PED
                  AND T.FIBRA = I.TFIBRA
                  AND V.TIPO = T.INDPF
                  AND V.CODIGO = I.VALPF
                  AND C.COD_CLIENTE = G.COD_CLIENTE
                  AND TO_CHAR(G.NUMERO) LIKE :codigo || '%'
                ORDER BY G.NUMERO DESC";

            try
            {
                _logger.LogDebug("Conectando a Oracle...");
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();
                _logger.LogDebug("Conexión establecida");

                using var command = new OracleCommand(query, connection);
                command.Parameters.Add(new OracleParameter(":codigo", OracleDbType.Varchar2, codigo, ParameterDirection.Input));

                _logger.LogDebug("Ejecutando consulta...");
                using var reader = await command.ExecuteReaderAsync();

                var resultados = new List<RecetaDto>();
                while (await reader.ReadAsync())
                {
                    resultados.Add(new RecetaDto
                    {
                        Numero = reader["NUMERO_RECETA"]?.ToString() ?? string.Empty,
                        Material = reader["DESCRIPCION_MATERIAL"]?.ToString() ?? string.Empty,
                        Lote = reader["CODIGO_LOTE"]?.ToString() ?? string.Empty
                    });
                }

                _logger.LogInformation("Se encontraron {Count} receta(s) para el código: {Codigo}", resultados.Count, codigo);
                return resultados;
            }
            catch (OracleException oEx)
            {
                _logger.LogError(oEx, "Error de Oracle al buscar receta: {Codigo}. OracleError: {OracleError}", 
                    codigo, oEx.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general al buscar receta en Oracle: {Codigo}", codigo);
                throw;
            }
        }

        public async Task<List<LoteDto>> BuscarLotePorCodigoAsync(string codigo)
        {
            var connectionString = GetOracleConnectionString();

            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogWarning("Oracle connection string not configured");
                return null;
            }

            _logger.LogInformation("Buscando lote con código: {Codigo}", codigo);

            const string query = @"
                SELECT 
                    R.LOTE AS CODIGO_LOTE,
                    NULL AS NUMERO_RECETA,
                    F.ABREVIADO||' '||P.ABREVIADO||' '||V.ABREVIADO AS DESCRIPCION_MATERIAL
                FROM H_RUTA_LOTE_G R,
                     H_FIBRA F,
                     H_PROCESOS P,
                     V_VALPF V
                WHERE R.ESTADO = '0'
                  AND F.FIBRA = R.FIBRA
                  AND P.PROCESO = R.PROCESO
                  AND V.TIPO = F.INDPF
                  AND V.CODIGO = R.VALPF
                  AND R.LOTE LIKE :codigo || '%'
                ORDER BY R.LOTE DESC";

            try
            {
                _logger.LogDebug("Conectando a Oracle para buscar lote...");
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();
                _logger.LogDebug("Conexión establecida");

                using var command = new OracleCommand(query, connection);
                command.Parameters.Add(new OracleParameter(":codigo", OracleDbType.Varchar2, codigo, ParameterDirection.Input));

                _logger.LogDebug("Ejecutando consulta de lote...");
                using var reader = await command.ExecuteReaderAsync();

                var resultados = new List<LoteDto>();
                while (await reader.ReadAsync())
                {
                    resultados.Add(new LoteDto
                    {
                        Lote = reader["CODIGO_LOTE"]?.ToString() ?? string.Empty,
                        Receta = reader["NUMERO_RECETA"]?.ToString() ?? string.Empty,
                        Material = reader["DESCRIPCION_MATERIAL"]?.ToString() ?? string.Empty
                    });
                }

                _logger.LogInformation("Se encontraron {Count} lote(s) para el código: {Codigo}", resultados.Count, codigo);
                return resultados;
            }
            catch (OracleException oEx)
            {
                _logger.LogError(oEx, "Error de Oracle al buscar lote: {Codigo}. OracleError: {OracleError}", 
                    codigo, oEx.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general al buscar lote en Oracle: {Codigo}", codigo);
                throw;
            }
        }

        public async Task<List<PartidaDto>> BuscarPartidaPorGuiaAsync(string guia)
        {
            var connectionString = GetOracleConnectionString();

            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogWarning("Oracle connection string not configured");
                return new List<PartidaDto>();
            }

            _logger.LogInformation("Buscando partida con guia: {Guia}", guia);

            const string query = @"
                SELECT V.GUIA,
                       V.PARTIDA,
                       V.SOLO_MATERIAL||' ('||V.COLOR_CLI||')' MATERIAL,
                       V.LOTE,
                       C.NOMBRE DESC_CLIENTE,
                       I.TITULO
                FROM V_PARTIDA V,
                     CLIENTES C,
                     ITEMPED I
                WHERE V.ESTADO = '0'
                  AND ((V.ESTADO_PED = '5') OR (V.ESTADO_PED = '6' AND TRUNC(I.F_CIERRE) >= ADD_MONTHS(TRUNC(SYSDATE),-1)))
                  AND C.COD_CLIENTE = V.COD_CLIENTE
                  AND I.NUM_PED = V.NUM_PED
                  AND I.NRO = V.NRO
                  AND V.GUIA LIKE :guia1 || '%'
                UNION
                SELECT V.GUIA,
                       V.PARTIDA,
                       V.SOLO_MATERIAL||' ('||V.COLOR_CLI||')' MATERIAL,
                       V.LOTE,
                       C.NOMBRE DESC_CLIENTE,
                       I.TITULO
                FROM V_PARTIDA V,
                     CLIENTES C,
                     ITEMPED I
                WHERE NVL(V.ESTADO,'0') = '7'
                  AND V.ESTADO_PED IN ('5','6')
                  AND TRUNC(I.F_CIERRE) >= ADD_MONTHS(TRUNC(SYSDATE),-2)
                  AND C.COD_CLIENTE = V.COD_CLIENTE
                  AND I.NUM_PED = V.NUM_PED
                  AND I.NRO = V.NRO
                  AND V.GUIA LIKE :guia2 || '%'
                ORDER BY 2";

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();

                using var command = new OracleCommand(query, connection);
                command.Parameters.Add(new OracleParameter(":guia1", OracleDbType.Varchar2, guia, ParameterDirection.Input));
                command.Parameters.Add(new OracleParameter(":guia2", OracleDbType.Varchar2, guia, ParameterDirection.Input));

                using var reader = await command.ExecuteReaderAsync();

                var resultados = new List<PartidaDto>();
                while (await reader.ReadAsync())
                {
                    resultados.Add(new PartidaDto
                    {
                        Guia       = reader["GUIA"]?.ToString() ?? string.Empty,
                        Partida    = reader["PARTIDA"]?.ToString() ?? string.Empty,
                        Material   = reader["MATERIAL"]?.ToString() ?? string.Empty,
                        Lote       = reader["LOTE"]?.ToString() ?? string.Empty,
                        DescCliente = reader["DESC_CLIENTE"]?.ToString() ?? string.Empty,
                        Titulo     = reader["TITULO"]?.ToString() ?? string.Empty
                    });
                }

                _logger.LogInformation("Se encontraron {Count} partida(s) para la guia: {Guia}", resultados.Count, guia);
                return resultados;
            }
            catch (OracleException oEx)
            {
                _logger.LogError(oEx, "Error de Oracle al buscar partida: {Guia}. OracleError: {OracleError}", guia, oEx.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general al buscar partida en Oracle: {Guia}", guia);
                throw;
            }
        }

        public async Task<List<MaquinaDto>> ObtenerTiposMaquinasAsync()
        {
            var connectionString = GetOracleConnectionString();

            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogWarning("Oracle connection string not configured");
                return new List<MaquinaDto>();
            }

            _logger.LogInformation("Obteniendo tipos de máquinas desde V_MAQUINA");

            const string query = @"
                SELECT 
                    TP_MAQ,
                    MAX(DESC_TPMAQ) AS DESC_TPMAQ
                FROM V_MAQUINA
                WHERE AREA = '01'
                GROUP BY TP_MAQ
                ORDER BY TP_MAQ";

            try
            {
                _logger.LogDebug("Conectando a Oracle para obtener tipos de máquinas...");
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();
                _logger.LogDebug("Conexión establecida");

                using var command = new OracleCommand(query, connection);
                _logger.LogDebug("Ejecutando consulta de tipos de máquinas...");
                using var reader = await command.ExecuteReaderAsync();

                var maquinas = new List<MaquinaDto>();
                while (await reader.ReadAsync())
                {
                    maquinas.Add(new MaquinaDto
                    {
                        TipoMaquina = reader["TP_MAQ"]?.ToString() ?? string.Empty,
                        DescripcionTipoMaquina = reader["DESC_TPMAQ"]?.ToString() ?? string.Empty
                    });
                }

                _logger.LogInformation("Se obtuvieron {Count} tipos de máquinas", maquinas.Count);
                return maquinas;
            }
            catch (OracleException oEx)
            {
                _logger.LogError(oEx, "Error de Oracle al obtener tipos de máquinas. OracleError: {OracleError}", 
                    oEx.Message);
                return new List<MaquinaDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general al obtener tipos de máquinas en Oracle");
                return new List<MaquinaDto>();
            }
        }

        public async Task<List<MaquinaIndividualDto>> ObtenerMaquinasPorTipoAsync(string tipoMaquina)
        {
            var connectionString = GetOracleConnectionString();

            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogWarning("Oracle connection string not configured");
                return new List<MaquinaIndividualDto>();
            }

            if (string.IsNullOrEmpty(tipoMaquina))
            {
                _logger.LogWarning("Tipo de máquina no especificado");
                return new List<MaquinaIndividualDto>();
            }

            _logger.LogInformation("Obteniendo máquinas para el tipo: {TipoMaquina}", tipoMaquina);

            const string query = @"
                SELECT 
                    COD_MAQ,
                    DESC_MAQ
                FROM V_MAQUINA
                WHERE TP_MAQ = :tipoMaquina
                  AND AREA = '01'
                ORDER BY COD_MAQ";

            try
            {
                _logger.LogDebug("Conectando a Oracle para obtener máquinas individuales...");
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();
                _logger.LogDebug("Conexión establecida");

                using var command = new OracleCommand(query, connection);
                command.Parameters.Add(new OracleParameter(":tipoMaquina", OracleDbType.Varchar2, tipoMaquina, ParameterDirection.Input));

                _logger.LogDebug("Ejecutando consulta de máquinas individuales...");
                using var reader = await command.ExecuteReaderAsync();

                var maquinas = new List<MaquinaIndividualDto>();
                while (await reader.ReadAsync())
                {
                    maquinas.Add(new MaquinaIndividualDto
                    {
                        CodigoMaquina = reader["COD_MAQ"]?.ToString() ?? string.Empty,
                        DescripcionMaquina = reader["DESC_MAQ"]?.ToString() ?? string.Empty
                    });
                }

                _logger.LogInformation("Se obtuvieron {Count} máquinas para el tipo {TipoMaquina}", maquinas.Count, tipoMaquina);
                return maquinas;
            }
            catch (OracleException oEx)
            {
                _logger.LogError(oEx, "Error de Oracle al obtener máquinas. OracleError: {OracleError}", 
                    oEx.Message);
                return new List<MaquinaIndividualDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general al obtener máquinas en Oracle");
                return new List<MaquinaIndividualDto>();
            }
        }

        public async Task<List<TituloDto>> ObtenerTitulosAsync()
        {
            var connectionString = GetOracleConnectionString();

            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogWarning("Oracle connection string not configured");
                return new List<TituloDto>();
            }

            _logger.LogInformation("Obteniendo títulos desde H_TITULOS");

            const string query = @"
                SELECT T.TITULO, T.DESCRIPCION AS DESC_TITULO
                FROM H_TITULOS T
                WHERE T.TITULO BETWEEN '400' AND '499'
                UNION
                SELECT T.TITULO, T.DESCRIPCION AS DESC_TITULO
                FROM H_TITULOS T
                WHERE T.TITULO = '200'
                ORDER BY 1";

            try
            {
                _logger.LogDebug("Conectando a Oracle para obtener títulos...");
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();
                _logger.LogDebug("Conexión establecida");

                using var command = new OracleCommand(query, connection);
                _logger.LogDebug("Ejecutando consulta de títulos...");
                using var reader = await command.ExecuteReaderAsync();

                var titulos = new List<TituloDto>();
                while (await reader.ReadAsync())
                {
                    titulos.Add(new TituloDto
                    {
                        Titulo = reader["TITULO"]?.ToString() ?? string.Empty,
                        Descripcion = reader["DESC_TITULO"]?.ToString() ?? string.Empty
                    });
                }

                _logger.LogInformation("Se obtuvieron {Count} títulos", titulos.Count);
                return titulos;
            }
            catch (OracleException oEx)
            {
                _logger.LogError(oEx, "Error de Oracle al obtener títulos. OracleError: {OracleError}", 
                    oEx.Message);
                return new List<TituloDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general al obtener títulos en Oracle");
                return new List<TituloDto>();
            }
        }

        public async Task<List<TituloDto>> ObtenerTitulosAutoconerAsync()
        {
            var connectionString = GetOracleConnectionString();

            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogWarning("Oracle connection string not configured");
                return new List<TituloDto>();
            }

            _logger.LogInformation("Obteniendo títulos Autoconer desde H_TITULOS");

            const string query = @"
                SELECT T.TITULO, T.DESCRIPCION AS DESC_TITULO
                FROM H_TITULOS T
                ORDER BY 1";

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();

                using var command = new OracleCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                var titulos = new List<TituloDto>();
                while (await reader.ReadAsync())
                {
                    titulos.Add(new TituloDto
                    {
                        Titulo = reader["TITULO"]?.ToString() ?? string.Empty,
                        Descripcion = reader["DESC_TITULO"]?.ToString() ?? string.Empty
                    });
                }

                _logger.LogInformation("Se obtuvieron {Count} títulos Autoconer", titulos.Count);
                return titulos;
            }
            catch (OracleException oEx)
            {
                _logger.LogError(oEx, "Error de Oracle al obtener títulos Autoconer. OracleError: {OracleError}",
                    oEx.Message);
                return new List<TituloDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general al obtener títulos Autoconer en Oracle");
                return new List<TituloDto>();
            }
        }

        public async Task<List<EmpleadoOracleDto>> ObtenerEmpleadosAsync()
        {
            var connectionString = GetOracleConnectionString();

            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogWarning("Oracle connection string not configured");
                return new List<EmpleadoOracleDto>();
            }

            _logger.LogInformation("Obteniendo empleados desde V_PERSONAL");

            const string query = @"
                SELECT V.C_CODIGO, V.NOMBRE_CORTO
                FROM V_PERSONAL V,
                     V_GRAN_CCOSTO C
                WHERE V.SITUACION = '1'
                  AND C.GRAN_CCOSTO = '01'
                  AND C.C_COSTO = 'P140'
                  AND C.C_CODIGO = V.C_CODIGO
                ORDER BY 2";

            try
            {
                _logger.LogDebug("Conectando a Oracle para obtener empleados...");
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();
                _logger.LogDebug("Conexión establecida");

                using var command = new OracleCommand(query, connection);
                _logger.LogDebug("Ejecutando consulta de empleados...");
                using var reader = await command.ExecuteReaderAsync();

                var empleados = new List<EmpleadoOracleDto>();
                while (await reader.ReadAsync())
                {
                    empleados.Add(new EmpleadoOracleDto
                    {
                        Codigo = reader["C_CODIGO"]?.ToString() ?? string.Empty,
                        NombreCorto = reader["NOMBRE_CORTO"]?.ToString() ?? string.Empty
                    });
                }

                _logger.LogInformation("Se obtuvieron {Count} empleados", empleados.Count);
                return empleados;
            }
            catch (OracleException oEx)
            {
                _logger.LogError(oEx, "Error de Oracle al obtener empleados. OracleError: {OracleError}", 
                    oEx.Message);
                return new List<EmpleadoOracleDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general al obtener empleados en Oracle");
                return new List<EmpleadoOracleDto>();
            }
        }

        public async Task<List<EmpleadoOracleDto>> BuscarOperarioAsync(string codigo)
        {
            var connectionString = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connectionString)) return new List<EmpleadoOracleDto>();

            const string query = @"
                SELECT C_CODIGO, NOMBRE_CORTO
                FROM V_PERSONAL
                WHERE SITUACION = '1'
                  AND (C_CODIGO LIKE :codigo || '%' OR UPPER(NOMBRE_CORTO) LIKE '%' || UPPER(:codigo) || '%')
                  AND ROWNUM <= 20
                ORDER BY NOMBRE_CORTO";

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();
                using var command = new OracleCommand(query, connection);
                command.BindByName = true;
                command.Parameters.Add(new OracleParameter(":codigo", OracleDbType.Varchar2) { Value = codigo });
                var result = new List<EmpleadoOracleDto>();
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new EmpleadoOracleDto
                    {
                        Codigo      = reader["C_CODIGO"]?.ToString()?.Trim()     ?? string.Empty,
                        NombreCorto = reader["NOMBRE_CORTO"]?.ToString()?.Trim() ?? string.Empty
                    });
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al buscar operario por código: {Codigo}", codigo);
                return new List<EmpleadoOracleDto>();
            }
        }

        public async Task<string> ObtenerNombreEmpleadoAsync(string codigo)
        {
            if (string.IsNullOrEmpty(codigo)) return codigo;
            var connectionString = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connectionString)) return codigo;

            const string query = "SELECT NOMBRE_CORTO FROM V_PERSONAL WHERE C_CODIGO = :codigo AND ROWNUM = 1";
            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();
                using var command = new OracleCommand(query, connection);
                command.BindByName = true;
                command.Parameters.Add(new OracleParameter(":codigo", OracleDbType.Varchar2) { Value = codigo });
                var result = await command.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                {
                    var nombre = result.ToString()?.Trim();
                    return string.IsNullOrEmpty(nombre) ? codigo : nombre;
                }
                return codigo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener nombre de empleado por código: {Codigo}", codigo);
                return codigo;
            }
        }

        public async Task<decimal> ObtenerPesoTituloAsync(string titulo)
        {
            var connectionString = GetOracleConnectionString();

            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogWarning("Oracle connection string not configured");
                return 0m;
            }

            const string query = "SELECT PESO FROM H_TITULOS WHERE TITULO = :titulo";

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();

                using var command = new OracleCommand(query, connection);
                command.Parameters.Add(new OracleParameter(":titulo", OracleDbType.Varchar2, titulo, ParameterDirection.Input));

                var result = await command.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                    return Convert.ToDecimal(result);

                return 0m;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener PESO para título: {Titulo}", titulo);
                return 0m;
            }
        }

        public async Task<bool> TieneMaquinaEnProcesoAsync(string tpMaq, string codMaq)
        {
            var connectionString = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connectionString)) return false;

            const string query = @"
                SELECT COUNT(*) FROM H_RPRODUC
                WHERE TRIM(TP_MAQ) = TRIM(:tpMaq)
                  AND TRIM(COD_MAQ) = TRIM(:codMaq)
                  AND ESTADO = '1'";

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();
                using var command = new OracleCommand(query, connection);
                command.Parameters.Add(new OracleParameter(":tpMaq",  OracleDbType.Varchar2) { Value = tpMaq });
                command.Parameters.Add(new OracleParameter(":codMaq", OracleDbType.Varchar2) { Value = codMaq });
                var result = await command.ExecuteScalarAsync();
                return result != null && result != DBNull.Value && Convert.ToInt32(result) > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar máquina en proceso: {TpMaq}/{CodMaq}", tpMaq, codMaq);
                return false;
            }
        }

        public async Task<int> ObtenerHusosMaquinaAsync(string tpMaq, string codMaq)
        {
            var connectionString = GetOracleConnectionString();

            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogWarning("Oracle connection string not configured");
                return 0;
            }

            const string query = "SELECT HUSOS FROM H_MAQUINAS WHERE TP_MAQ = :tpMaq AND COD_MAQ = :codMaq";

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();

                using var command = new OracleCommand(query, connection);
                command.Parameters.Add(new OracleParameter(":tpMaq",  OracleDbType.Varchar2, tpMaq,  ParameterDirection.Input));
                command.Parameters.Add(new OracleParameter(":codMaq", OracleDbType.Varchar2, codMaq, ParameterDirection.Input));

                var result = await command.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                    return Convert.ToInt32(result);

                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener HUSOS para máquina: {TpMaq}/{CodMaq}", tpMaq, codMaq);
                return 0;
            }
        }

        public async Task<bool> InsertarPreparatoriaAsync(OrdenProduccion orden, string? adUser = null)
        {
            var connectionString = GetOracleConnectionString();

            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogWarning("Oracle connection string not configured");
                return false;
            }

            _logger.LogInformation("Insertando preparatoria en H_RPRODUC: Receta={Receta}, Lote={Lote}", 
                orden.CodigoReceta, orden.Lote);

            const string query = @"
                INSERT INTO H_RPRODUC (
                    RECETA,
                    LOTE,
                    TP_MAQ,
                    COD_MAQ,
                    TITULO,
                    FECHA_INI,
                    ESTADO,
                    C_CODIGO,
                    TURNO,
                    PASO_MANUAR,
                    HUSOS_INAC,
                    CONTADOR_INI,
                    FECHA_TURNO,
                         VELOCIDAD,
                         METRAJE,
                         A_ADUSER,
                         A_MDUSER
                    ) VALUES (
                        :receta,
                        :lote,
                        :tp_maq,
                        :cod_maq,
                        :titulo,
                        :fecha_ini,
                        :estado,
                        :c_codigo,
                        :turno,
                        :paso_manuar,
                        :husos_inac,
                        :contador_ini,
                        :fecha_turno,
                        :velocidad,
                        :metraje,
                        :a_aduser,
                         :a_mduser
                    )";

            try
            {
                _logger.LogDebug("Conectando a Oracle para insertar preparatoria...");
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();
                _logger.LogDebug("Conexión establecida");

                using var command = new OracleCommand(query, connection);
                command.BindByName = true;

                // Agregar parámetros
                command.Parameters.Add(new OracleParameter(":receta",       OracleDbType.Varchar2, orden.CodigoReceta ?? string.Empty, ParameterDirection.Input));
                command.Parameters.Add(new OracleParameter(":lote",         OracleDbType.Varchar2, orden.Lote ?? string.Empty, ParameterDirection.Input));
                command.Parameters.Add(new OracleParameter(":tp_maq",       OracleDbType.Varchar2, orden.CodigoMaquina ?? string.Empty, ParameterDirection.Input));
                command.Parameters.Add(new OracleParameter(":cod_maq",      OracleDbType.Varchar2, orden.Maquina ?? string.Empty, ParameterDirection.Input));
                command.Parameters.Add(new OracleParameter(":titulo",       OracleDbType.Varchar2, orden.Titulo ?? string.Empty, ParameterDirection.Input));
                command.Parameters.Add(new OracleParameter(":fecha_ini",    OracleDbType.Date, orden.FechaInicio, ParameterDirection.Input));
                command.Parameters.Add(new OracleParameter(":estado",       OracleDbType.Varchar2, "1", ParameterDirection.Input));
                command.Parameters.Add(new OracleParameter(":c_codigo",     OracleDbType.Varchar2, orden.EmpleadoId ?? string.Empty, ParameterDirection.Input));
                command.Parameters.Add(new OracleParameter(":turno",        OracleDbType.Varchar2, orden.Turno ?? string.Empty, ParameterDirection.Input));
                command.Parameters.Add(new OracleParameter(":paso_manuar",  OracleDbType.Varchar2, orden.PasoManuar ?? string.Empty, ParameterDirection.Input));
                command.Parameters.Add(new OracleParameter(":husos_inac",   OracleDbType.Decimal) { Value = orden.CodigoMaquina == "P" && orden.HorasInactivas.HasValue  ? (object)orden.HorasInactivas.Value  : (object)0 });
                command.Parameters.Add(new OracleParameter(":contador_ini", OracleDbType.Decimal) { Value = orden.CodigoMaquina == "P" && orden.ContadorInicial.HasValue ? (object)orden.ContadorInicial.Value : DBNull.Value });
                // FECHA_TURNO en formato 24h: hora < 7 → día anterior; hora >= 7 → misma fecha
                // Ej: 18/03/2026 02:00 → hora=2 < 7 → FECHA_TURNO = 17/03/2026
                // Ej: 18/03/2026 08:00 → hora=8 >= 7 → FECHA_TURNO = 18/03/2026
                var fechaTurno = orden.FechaInicio.Hour < 7
                    ? orden.FechaInicio.Date.AddDays(-1)
                    : orden.FechaInicio.Date;
                command.Parameters.Add(new OracleParameter(":fecha_turno",  OracleDbType.Varchar2, fechaTurno.ToString("dd/MM/yyyy"), ParameterDirection.Input));
                command.Parameters.Add(new OracleParameter(":velocidad",    OracleDbType.Decimal) { Value = orden.Velocidad.HasValue ? (object)orden.Velocidad.Value : DBNull.Value });
                command.Parameters.Add(new OracleParameter(":metraje",     OracleDbType.Decimal) { Value = orden.Metraje.HasValue  ? (object)orden.Metraje.Value  : DBNull.Value });
                command.Parameters.Add(new OracleParameter(":a_aduser",     OracleDbType.Varchar2, adUser ?? string.Empty, ParameterDirection.Input));
                command.Parameters.Add(new OracleParameter(":a_mduser",     OracleDbType.Varchar2, adUser ?? string.Empty, ParameterDirection.Input));

                _logger.LogDebug("Ejecutando INSERT en H_RPRODUC...");
                var rowsAffected = await command.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Preparatoria insertada exitosamente en H_RPRODUC. Filas afectadas: {RowsAffected}", rowsAffected);
                    return true;
                }
                else
                {
                    _logger.LogWarning("No se insertó ningún registro en H_RPRODUC");
                    return false;
                }
            }
            catch (OracleException oEx)
            {
                _logger.LogError(oEx, "Error de Oracle al insertar preparatoria. OracleError: {OracleError}", 
                    oEx.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general al insertar preparatoria en Oracle");
                return false;
            }
        }

        public async Task<bool> InsertarPreparatoriaAutoconerAsync(RegistroAutoconer registro, string? adUser = null)
        {
            var connectionString = GetOracleConnectionString();

            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogWarning("Oracle connection string not configured");
                return false;
            }

            _logger.LogInformation("Insertando preparatoria Autoconer en H_RPRODUC: Lote={Lote}, Maquina={Maquina}",
                registro.Lote, registro.NumeroAutoconer);

            // TP_MAQ='A', ESTADO='1', HUSOS_INAC=0 y A_ADFECHA/A_MDFECHA=SYSDATE son literales en el SQL.
            // HUSOS y HUSOS_ACT se consultan en H_MAQUINAS por la máquina seleccionada.
            // KG_UNIDAD se calcula como PESO_NETO / UNIDADES cuando ambos están disponibles.
            // GUIA = Nº Partida del formulario. PROCESO, VELOCIDAD2 y PROD_TEORICO → NULL.
            const string query = @"
                INSERT INTO H_RPRODUC (
                    RECETA, LOTE, TP_MAQ, COD_MAQ, TITULO,
                    FECHA_INI, FECHA_FIN,
                    ESTADO,
                    A_ADUSER, A_ADFECHA, A_MDUSER, A_MDFECHA,
                    TURNO,
                    PESO_NETO, UNIDADES,
                    HUSOS, HUSOS_INAC, HUSOS_ACT, KG_UNIDAD,
                    VELOCIDAD,
                    GUIA, DESTINO, PROCESO,
                    C_CODIGO, FECHA_TURNO,
                    HUSOS_ACT_T1, HUSOS_ACT_T2, HUSOS_ACT_T3,
                    HUSOS_ACT_T4, HUSOS_ACT_T5, HUSOS_ACT_T6,
                    VELOCIDAD2, PROD_TEORICO
                ) VALUES (
                    :receta, :lote, 'A', :cod_maq, :titulo,
                    :fecha_ini, :fecha_fin,
                    '1',
                    :a_aduser, SYSDATE, :a_mduser, SYSDATE,
                    :turno,
                    :peso_neto, :unidades,
                    :husos, 0, :husos_act, :kg_unidad,
                    :velocidad,
                    :guia, :destino, NULL,
                    :c_codigo, :fecha_turno,
                    :husos_act_t1, :husos_act_t2, :husos_act_t3,
                    :husos_act_t4, :husos_act_t5, :husos_act_t6,
                    NULL, NULL
                )";

            try
            {
                _logger.LogDebug("Conectando a Oracle para insertar preparatoria Autoconer...");
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();

                // ── HUSOS totales de la máquina (H_MAQUINAS) ─────────────────────────
                // HUSOS_INAC = 0 por definición en AUTOCONER → HUSOS_ACT = HUSOS
                int? husosMaquina = null;
                const string queryHusos = "SELECT HUSOS FROM H_MAQUINAS WHERE TP_MAQ = 'A' AND COD_MAQ = :cod";
                using (var cmdHusos = new OracleCommand(queryHusos, connection))
                {
                    cmdHusos.Parameters.Add(new OracleParameter(":cod", OracleDbType.Varchar2, registro.NumeroAutoconer, ParameterDirection.Input));
                    var resHusos = await cmdHusos.ExecuteScalarAsync();
                    if (resHusos != null && resHusos != DBNull.Value)
                        husosMaquina = Convert.ToInt32(resHusos);
                }

                // ── KG_UNIDAD = PesoBruto (Kg por unidad, directo del formulario) ─────────────────────
                decimal? kgUnidad = registro.PesoBruto;
                // ── PESO_NETO = UNIDADES × KG_UNIDAD ─────────────────────────────────
                decimal? pesoNeto = (registro.Cantidad.HasValue && kgUnidad.HasValue)
                    ? Math.Round(registro.Cantidad.Value * kgUnidad.Value, 4)
                    : (decimal?)null;

                using var command = new OracleCommand(query, connection);
                command.BindByName = true;

                // ── Identificación ────────────────────────────────────────────────────
                command.Parameters.Add(new OracleParameter(":receta",       OracleDbType.Varchar2) { Value = (object?)registro.CodigoReceta ?? DBNull.Value });
                command.Parameters.Add(new OracleParameter(":lote",         OracleDbType.Varchar2,  registro.Lote,            ParameterDirection.Input));
                command.Parameters.Add(new OracleParameter(":cod_maq",      OracleDbType.Varchar2,  registro.NumeroAutoconer, ParameterDirection.Input));
                command.Parameters.Add(new OracleParameter(":titulo",       OracleDbType.Varchar2,  registro.Titulo,          ParameterDirection.Input));

                // ── Fechas ────────────────────────────────────────────────────────────
                command.Parameters.Add(new OracleParameter(":fecha_ini",    OracleDbType.Date,      registro.Fecha,           ParameterDirection.Input));
                command.Parameters.Add(new OracleParameter(":fecha_fin",    OracleDbType.Date)      { Value = registro.HoraFinal.HasValue ? (object)registro.HoraFinal.Value : DBNull.Value });

                // ── Auditoría ─────────────────────────────────────────────────────────
                command.Parameters.Add(new OracleParameter(":a_aduser",     OracleDbType.Varchar2,  adUser ?? string.Empty,   ParameterDirection.Input));
                command.Parameters.Add(new OracleParameter(":a_mduser",     OracleDbType.Varchar2,  adUser ?? string.Empty,   ParameterDirection.Input));

                // ── Turno / Operario ──────────────────────────────────────────────────
                command.Parameters.Add(new OracleParameter(":turno",        OracleDbType.Varchar2,  registro.Turno,           ParameterDirection.Input));
                command.Parameters.Add(new OracleParameter(":c_codigo",     OracleDbType.Varchar2,  registro.CodigoOperador,  ParameterDirection.Input));

                // ── Producción ────────────────────────────────────────────────────────
                command.Parameters.Add(new OracleParameter(":peso_neto",    OracleDbType.Decimal)   { Value = pesoNeto.HasValue              ? (object)pesoNeto.Value              : DBNull.Value });
                command.Parameters.Add(new OracleParameter(":unidades",     OracleDbType.Int32)     { Value = registro.Cantidad.HasValue     ? (object)registro.Cantidad.Value     : DBNull.Value });
                command.Parameters.Add(new OracleParameter(":husos",        OracleDbType.Int32)     { Value = husosMaquina.HasValue          ? (object)husosMaquina.Value          : DBNull.Value });
                command.Parameters.Add(new OracleParameter(":husos_act",    OracleDbType.Int32)     { Value = husosMaquina.HasValue          ? (object)husosMaquina.Value          : DBNull.Value });
                command.Parameters.Add(new OracleParameter(":kg_unidad",    OracleDbType.Decimal)   { Value = kgUnidad.HasValue              ? (object)kgUnidad.Value              : DBNull.Value });
                command.Parameters.Add(new OracleParameter(":velocidad",    OracleDbType.Decimal)   { Value = registro.VelocidadMMin.HasValue ? (object)registro.VelocidadMMin.Value : DBNull.Value });

                // ── Guia / Destino ────────────────────────────────────────────────────
                command.Parameters.Add(new OracleParameter(":guia",         OracleDbType.Varchar2)  { Value = (object?)registro.Guia ?? DBNull.Value });
                command.Parameters.Add(new OracleParameter(":destino",      OracleDbType.Varchar2)  { Value = (object?)registro.Destino ?? DBNull.Value });

                // ── FECHA_TURNO (DATE): hora < 7 → día anterior; hora >= 7 → misma fecha ──
                var fechaTurno = registro.Fecha.Hour < 7
                    ? registro.Fecha.Date.AddDays(-1)
                    : registro.Fecha.Date;
                command.Parameters.Add(new OracleParameter(":fecha_turno",  OracleDbType.Date,      fechaTurno,               ParameterDirection.Input));

                // ── Tramos → HUSOS_ACT_T1..T6 (bool → 1/0) ──────────────────────────
                command.Parameters.Add(new OracleParameter(":husos_act_t1", OracleDbType.Int32,     registro.Tramo1 ? 10 : 0, ParameterDirection.Input));
                command.Parameters.Add(new OracleParameter(":husos_act_t2", OracleDbType.Int32,     registro.Tramo2 ? 10 : 0, ParameterDirection.Input));
                command.Parameters.Add(new OracleParameter(":husos_act_t3", OracleDbType.Int32,     registro.Tramo3 ? 10 : 0, ParameterDirection.Input));
                command.Parameters.Add(new OracleParameter(":husos_act_t4", OracleDbType.Int32,     registro.Tramo4 ? 10 : 0, ParameterDirection.Input));
                command.Parameters.Add(new OracleParameter(":husos_act_t5", OracleDbType.Int32,     registro.Tramo5 ? 10 : 0, ParameterDirection.Input));
                command.Parameters.Add(new OracleParameter(":husos_act_t6", OracleDbType.Int32,     registro.Tramo6 ? 10 : 0, ParameterDirection.Input));

                _logger.LogDebug("Ejecutando INSERT Autoconer en H_RPRODUC...");
                var rowsAffected = await command.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Preparatoria Autoconer insertada exitosamente en H_RPRODUC. Filas afectadas: {RowsAffected}", rowsAffected);
                    return true;
                }

                _logger.LogWarning("No se insertó ningún registro Autoconer en H_RPRODUC");
                return false;
            }
            catch (OracleException oEx)
            {
                _logger.LogError(oEx, "Error de Oracle al insertar preparatoria Autoconer. OracleError: {OracleError}", oEx.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general al insertar preparatoria Autoconer en Oracle");
                return false;
            }
        }

        public async Task<PreparatoriaPagedResult> ObtenerPreparatoriasAsync(string? filtroLote = null, string? filtroMaquina = null, string? filtroTipoMaquina = null, List<string>? filtroEstados = null, int page = 1, int pageSize = 10)
        {
            var connectionString = GetOracleConnectionString();

            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogWarning("Oracle connection string not configured");
                return new PreparatoriaPagedResult();
            }

            _logger.LogInformation("Obteniendo preparatorias desde H_RPRODUC por FECHA_TURNO del día de hoy");

            // Construir filtros dinámicos (compartidos entre COUNT y consulta de datos)
            var filterSuffix = string.Empty;
            if (!string.IsNullOrEmpty(filtroLote))
                filterSuffix += " AND R.LOTE LIKE :filtroLote || '%'";
            if (!string.IsNullOrEmpty(filtroTipoMaquina))
                filterSuffix += " AND R.TP_MAQ = :filtroTipoMaquina";
            if (!string.IsNullOrEmpty(filtroMaquina))
                filterSuffix += " AND R.COD_MAQ LIKE :filtroMaquina || '%'";
            if (filtroEstados != null && filtroEstados.Count > 0)
            {
                var paramNames = string.Join(", ", filtroEstados.Select((_, i) => $":filtroEstado{i}"));
                filterSuffix += $" AND R.ESTADO IN ({paramNames})";
            }

            // ── CONTEO: query liviano, solo H_RPRODUC ────────────────────────────
            var countQuery = "SELECT COUNT(*) FROM H_RPRODUC R WHERE R.FECHA_TURNO = TO_CHAR(SYSDATE, 'DD/MM/YYYY') AND R.TP_MAQ IN ('M', 'P', 'E', 'L', 'B')" + filterSuffix;

            // ── DATOS: paginación en dos fases ────────────────────────────────────
            // Fase 1: query ligero (sin subqueries correlacionadas).
            // Ordena y pagina usando solo columnas simples y JOINs directos.
            var innerLightQuery = @"
                SELECT R.RECETA, R.LOTE, R.TP_MAQ, R.COD_MAQ, R.TITULO,
                       R.FECHA_INI, R.ESTADO, R.C_CODIGO, R.TURNO, R.PASO_MANUAR,
                       M.DESC_MAQ,
                       T.DESCRIPCION  AS DESC_TITULO,
                       P.NOMBRE_CORTO AS NOMBRE_OPERARIO
                FROM H_RPRODUC R
                LEFT JOIN V_MAQUINA  M ON M.COD_MAQ  = R.COD_MAQ  AND M.AREA = '01'
                LEFT JOIN H_TITULOS  T ON T.TITULO   = R.TITULO
                LEFT JOIN V_PERSONAL P ON P.C_CODIGO = R.C_CODIGO
                WHERE R.FECHA_TURNO = TO_CHAR(SYSDATE, 'DD/MM/YYYY') AND R.TP_MAQ IN ('M', 'P', 'E', 'L', 'B')";
            innerLightQuery += filterSuffix;
            innerLightQuery += " ORDER BY R.FECHA_INI DESC";

            int startRow = (page - 1) * pageSize + 1;
            int endRow   = page * pageSize;

            // Fase 2: CTE materializado con las N filas ya paginadas.
            // Las subqueries costosas de MATERIAL se ejecutan únicamente sobre esas filas.
            var dataQuery = $@"
                WITH paged AS (
                    SELECT /*+ MATERIALIZE */
                           p_.RECETA, p_.LOTE, p_.TP_MAQ, p_.COD_MAQ, p_.TITULO,
                           p_.FECHA_INI, p_.ESTADO, p_.C_CODIGO, p_.TURNO, p_.PASO_MANUAR,
                           p_.DESC_MAQ, p_.DESC_TITULO, p_.NOMBRE_OPERARIO
                    FROM (
                        SELECT t_.*, ROWNUM AS RN_
                        FROM ({innerLightQuery}) t_
                        WHERE ROWNUM <= :pEndRow
                    ) p_
                    WHERE p_.RN_ >= :pStartRow
                )
                SELECT
                    p.RECETA, p.LOTE, p.TP_MAQ, p.COD_MAQ, p.TITULO,
                    p.FECHA_INI, p.ESTADO, p.C_CODIGO, p.TURNO, p.PASO_MANUAR,
                    p.DESC_MAQ, p.DESC_TITULO, p.NOMBRE_OPERARIO,
                    CASE
                        WHEN p.RECETA IS NOT NULL THEN
                            (SELECT F2.ABREVIADO||' '||P2.ABREVIADO||' '||V2.ABREVIADO||' ('||I2.COLOR_DET||')'
                             FROM H_RECETA_G G2, H_FIBRA F2, H_PROCESOS P2, ITEMPED I2,
                                  V_TFIBRA T2, V_VALPF V2, CLIENTES C2
                             WHERE TO_CHAR(G2.NUMERO) = TRIM(TO_CHAR(p.RECETA))
                               AND G2.TIPO = 'R'
                               AND NVL(G2.ESTADO,'1') <> '9'
                               AND F2.FIBRA          = G2.FIBRA
                               AND P2.PROCESO        = G2.PROCESO
                               AND I2.NUM_PED        = G2.NUM_PED
                               AND I2.NRO            = G2.ITEM_PED
                               AND T2.FIBRA          = I2.TFIBRA
                               AND V2.TIPO           = T2.INDPF
                               AND V2.CODIGO         = I2.VALPF
                               AND C2.COD_CLIENTE    = G2.COD_CLIENTE
                               AND ROWNUM = 1)
                        ELSE
                            (SELECT F3.ABREVIADO||' '||PR3.ABREVIADO||' '||V3.ABREVIADO
                             FROM H_RUTA_LOTE_G RL3, H_FIBRA F3, H_PROCESOS PR3, V_VALPF V3
                             WHERE RL3.LOTE    = p.LOTE
                               AND RL3.ESTADO  = '0'
                               AND F3.FIBRA    = RL3.FIBRA
                               AND PR3.PROCESO = RL3.PROCESO
                               AND V3.TIPO     = F3.INDPF
                               AND V3.CODIGO   = RL3.VALPF
                               AND ROWNUM = 1)
                    END AS MATERIAL
                FROM paged p";

            try
            {
                _logger.LogDebug("Conectando a Oracle para obtener preparatorias...");
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();
                _logger.LogDebug("Conexión establecida");

                // 1. Total de registros
                int totalCount = 0;
                using (var countCmd = new OracleCommand(countQuery, connection))
                {
                    countCmd.BindByName = true;
                    if (!string.IsNullOrEmpty(filtroLote))
                        countCmd.Parameters.Add(new OracleParameter(":filtroLote", OracleDbType.Varchar2, filtroLote, ParameterDirection.Input));
                    if (!string.IsNullOrEmpty(filtroTipoMaquina))
                        countCmd.Parameters.Add(new OracleParameter(":filtroTipoMaquina", OracleDbType.Varchar2, filtroTipoMaquina, ParameterDirection.Input));
                    if (!string.IsNullOrEmpty(filtroMaquina))
                        countCmd.Parameters.Add(new OracleParameter(":filtroMaquina", OracleDbType.Varchar2, filtroMaquina, ParameterDirection.Input));
                    if (filtroEstados != null && filtroEstados.Count > 0)
                    {
                        for (int i = 0; i < filtroEstados.Count; i++)
                            countCmd.Parameters.Add(new OracleParameter($":filtroEstado{i}", OracleDbType.Varchar2, filtroEstados[i], ParameterDirection.Input));
                    }
                    var countResult = await countCmd.ExecuteScalarAsync();
                    if (countResult != null && countResult != DBNull.Value)
                        totalCount = Convert.ToInt32(countResult);
                }

                // 2. Datos paginados
                _logger.LogDebug("Ejecutando consulta paginada (página {Page}, filas {Start}–{End})...", page, startRow, endRow);
                var preparatorias = new List<PreparatoriaListDto>();
                using (var dataCmd = new OracleCommand(dataQuery, connection))
                {
                    dataCmd.BindByName = true;
                    if (!string.IsNullOrEmpty(filtroLote))
                        dataCmd.Parameters.Add(new OracleParameter(":filtroLote", OracleDbType.Varchar2, filtroLote, ParameterDirection.Input));
                    if (!string.IsNullOrEmpty(filtroTipoMaquina))
                        dataCmd.Parameters.Add(new OracleParameter(":filtroTipoMaquina", OracleDbType.Varchar2, filtroTipoMaquina, ParameterDirection.Input));
                    if (!string.IsNullOrEmpty(filtroMaquina))
                        dataCmd.Parameters.Add(new OracleParameter(":filtroMaquina", OracleDbType.Varchar2, filtroMaquina, ParameterDirection.Input));
                    if (filtroEstados != null && filtroEstados.Count > 0)
                    {
                        for (int i = 0; i < filtroEstados.Count; i++)
                            dataCmd.Parameters.Add(new OracleParameter($":filtroEstado{i}", OracleDbType.Varchar2, filtroEstados[i], ParameterDirection.Input));
                    }
                    dataCmd.Parameters.Add(new OracleParameter(":pEndRow",   OracleDbType.Int32, endRow,   ParameterDirection.Input));
                    dataCmd.Parameters.Add(new OracleParameter(":pStartRow", OracleDbType.Int32, startRow, ParameterDirection.Input));

                    using var reader = await dataCmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var fechaIni = reader["FECHA_INI"] != DBNull.Value
                            ? Convert.ToDateTime(reader["FECHA_INI"])
                            : DateTime.MinValue;

                        preparatorias.Add(new PreparatoriaListDto
                        {
                            Receta             = reader["RECETA"]?.ToString()?.Trim()          ?? string.Empty,
                            Lote               = reader["LOTE"]?.ToString()?.Trim()             ?? string.Empty,
                            Material           = reader["MATERIAL"]?.ToString()?.Trim()         ?? string.Empty,
                            TipoMaquina        = reader["TP_MAQ"]?.ToString()?.Trim()           ?? string.Empty,
                            CodigoMaquina      = reader["COD_MAQ"]?.ToString()?.Trim()          ?? string.Empty,
                            DescripcionMaquina = reader["DESC_MAQ"]?.ToString()?.Trim()         ?? string.Empty,
                            Titulo             = reader["TITULO"]?.ToString()?.Trim()            ?? string.Empty,
                            DescripcionTitulo  = reader["DESC_TITULO"]?.ToString()?.Trim()      ?? string.Empty,
                            FechaInicio        = fechaIni,
                            Estado             = reader["ESTADO"]?.ToString()?.Trim()            ?? string.Empty,
                            CodigoOperario     = reader["C_CODIGO"]?.ToString()?.Trim()         ?? string.Empty,
                            NombreOperario     = reader["NOMBRE_OPERARIO"]?.ToString()?.Trim()  ?? string.Empty,
                            Turno              = reader["TURNO"]?.ToString()?.Trim()             ?? string.Empty,
                            PasoManual         = reader["PASO_MANUAR"]?.ToString()?.Trim()      ?? string.Empty
                        });
                    }
                }

                _logger.LogInformation("Se obtuvieron {Count} preparatorias de {Total} totales", preparatorias.Count, totalCount);
                return new PreparatoriaPagedResult { Items = preparatorias, TotalCount = totalCount };
            }
            catch (OracleException oEx)
            {
                _logger.LogError(oEx, "Error de Oracle al obtener preparatorias. OracleError: {OracleError}",
                    oEx.Message);
                return new PreparatoriaPagedResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general al obtener preparatorias en Oracle");
                return new PreparatoriaPagedResult();
            }
        }

        public async Task<bool> CerrarPreparatoriaOracleAsync(string? receta, string? lote, string? tpMaq, string? codMaq, string? titulo, DateTime fechaIni, string? mdUser = null)
        {
            var connectionString = GetOracleConnectionString();

            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogWarning("Oracle connection string not configured");
                return false;
            }

            _logger.LogInformation("Cerrando preparatoria en Oracle: Receta={Receta}, Lote={Lote}, TpMaq={TpMaq}, CodMaq={CodMaq}, Titulo={Titulo}, FechaIni={FechaIni}",
                receta, lote, tpMaq, codMaq, titulo, fechaIni);

            const string query = @"
                UPDATE H_RPRODUC SET ESTADO = '3',
                                    FECHA_FIN = :fechaFin,
                                    A_MDUSER  = :mdUser,
                                    A_MDFECHA = :mdFecha
                WHERE NVL(TRIM(TO_CHAR(RECETA)), ' ')              = NVL(TRIM(:receta), ' ')
                  AND TRIM(LOTE)                                    = TRIM(:lote)
                  AND TRIM(TP_MAQ)                                  = TRIM(:tpMaq)
                  AND TRIM(COD_MAQ)                                 = TRIM(:codMaq)
                  AND TRIM(TITULO)                                  = TRIM(:titulo)
                  AND TO_CHAR(FECHA_INI, 'YYYY-MM-DD HH24:MI:SS') = :fechaIni
                  AND ESTADO = '1'";

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();

                using var command = new OracleCommand(query, connection);

                static object Str(string? v) => string.IsNullOrEmpty(v) ? DBNull.Value : (object)v;

                command.Parameters.Add(new OracleParameter(":fechaFin", OracleDbType.Date)    { Value = DateTime.Now });
                command.Parameters.Add(new OracleParameter(":mdUser",   OracleDbType.Varchar2) { Value = Str(mdUser) });
                command.Parameters.Add(new OracleParameter(":mdFecha",  OracleDbType.Date)    { Value = DateTime.Now });
                command.Parameters.Add(new OracleParameter(":receta",   OracleDbType.Varchar2) { Value = Str(receta) });
                command.Parameters.Add(new OracleParameter(":lote",     OracleDbType.Varchar2) { Value = Str(lote) });
                command.Parameters.Add(new OracleParameter(":tpMaq",    OracleDbType.Varchar2) { Value = Str(tpMaq) });
                command.Parameters.Add(new OracleParameter(":codMaq",   OracleDbType.Varchar2) { Value = Str(codMaq) });
                command.Parameters.Add(new OracleParameter(":titulo",   OracleDbType.Varchar2) { Value = Str(titulo) });
                command.Parameters.Add(new OracleParameter(":fechaIni", OracleDbType.Varchar2) { Value = fechaIni.ToString("yyyy-MM-dd HH:mm:ss") });

                var rowsAffected = await command.ExecuteNonQueryAsync();
                _logger.LogInformation("Preparatoria cerrada en Oracle. Filas afectadas: {Rows}", rowsAffected);
                return rowsAffected > 0;
            }
            catch (OracleException oEx)
            {
                _logger.LogError(oEx, "Error de Oracle al cerrar preparatoria: {Receta}", receta);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general al cerrar preparatoria: {Receta}", receta);
                return false;
            }
        }

        public async Task<bool> AnularPreparatoriaOracleAsync(string? receta, string? lote, string? tpMaq, string? codMaq, string? titulo, DateTime fechaIni)
        {
            var connectionString = GetOracleConnectionString();

            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogWarning("Oracle connection string not configured");
                return false;
            }

            _logger.LogInformation("Anulando preparatoria en Oracle: Receta={Receta}, Lote={Lote}, TpMaq={TpMaq}, CodMaq={CodMaq}, Titulo={Titulo}, FechaIni={FechaIni}",
                receta, lote, tpMaq, codMaq, titulo, fechaIni);

            // TO_CHAR a nivel de segundos: FECHA_INI fue insertada con el mismo DateTime.Now
            // de la app (ya no usa SYSDATE), por lo que los valores coincidirán exactamente.
            const string query = @"
                UPDATE H_RPRODUC SET ESTADO = '9',
                                    FECHA_FIN = :fechaFin
                WHERE NVL(TRIM(TO_CHAR(RECETA)), ' ')              = NVL(TRIM(:receta), ' ')
                  AND TRIM(LOTE)                                    = TRIM(:lote)
                  AND TRIM(TP_MAQ)                                  = TRIM(:tpMaq)
                  AND TRIM(COD_MAQ)                                 = TRIM(:codMaq)
                  AND TRIM(TITULO)                                  = TRIM(:titulo)
                  AND TO_CHAR(FECHA_INI, 'YYYY-MM-DD HH24:MI:SS') = :fechaIni
                  AND ESTADO = '1'";

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();

                using var command = new OracleCommand(query, connection);

                static object Str(string? v) => string.IsNullOrEmpty(v) ? DBNull.Value : (object)v;

                command.Parameters.Add(new OracleParameter(":fechaFin", OracleDbType.Date) { Value = DateTime.Now });
                command.Parameters.Add(new OracleParameter(":receta",  OracleDbType.Varchar2) { Value = Str(receta) });
                command.Parameters.Add(new OracleParameter(":lote",    OracleDbType.Varchar2) { Value = Str(lote) });
                command.Parameters.Add(new OracleParameter(":tpMaq",   OracleDbType.Varchar2) { Value = Str(tpMaq) });
                command.Parameters.Add(new OracleParameter(":codMaq",  OracleDbType.Varchar2) { Value = Str(codMaq) });
                command.Parameters.Add(new OracleParameter(":titulo",  OracleDbType.Varchar2) { Value = Str(titulo) });
                command.Parameters.Add(new OracleParameter(":fechaIni", OracleDbType.Varchar2) { Value = fechaIni.ToString("yyyy-MM-dd HH:mm:ss") });

                var rowsAffected = await command.ExecuteNonQueryAsync();
                _logger.LogInformation("Preparatoria anulada en Oracle. Filas afectadas: {Rows}", rowsAffected);
                return rowsAffected > 0;
            }
            catch (OracleException oEx)
            {
                _logger.LogError(oEx, "Error de Oracle al anular preparatoria: {Receta}", receta);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general al anular preparatoria: {Receta}", receta);
                return false;
            }
        }

        public async Task<bool> ActualizarPreparatoriaOracleAsync(
            string? oldReceta, string? oldLote, string? oldTpMaq, string? oldCodMaq, string? oldTitulo, DateTime fechaIni,
            string? newReceta, string? newLote, string? newTpMaq, string? newCodMaq, string? newTitulo,
            string? cCodigo, string? turno, string? pasoManuar, DateTime newFechaIni,
            decimal? contadorInicial = null, decimal? husosInactivas = null, string? mdUser = null, decimal? velocidad = null, decimal? metraje = null)
        {
            var connectionString = GetOracleConnectionString();

            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogWarning("Oracle connection string not configured");
                return false;
            }

            _logger.LogInformation(
                "Actualizando preparatoria en Oracle. WHERE: Receta={OldReceta}, Lote={OldLote}, TpMaq={OldTpMaq}, CodMaq={OldCodMaq}, Titulo={OldTitulo}, FechaIni={FechaIni}, MdUser={MdUser}",
                oldReceta, oldLote, oldTpMaq, oldCodMaq, oldTitulo, fechaIni, mdUser);

            // SET: campos editables incluyendo FECHA_INI.
            // WHERE: usa TO_CHAR a segundos — FECHA_INI fue insertada con DateTime.Now de la app
            // (no SYSDATE), así ambos valores son idénticos.
            var extraSetActualizar = newTpMaq == "P"
                ? ",\n                    CONTADOR_INI = :contadorIni,\n                    HUSOS_INAC   = :husosInac"
                : string.Empty;
            var query = $@"
                UPDATE H_RPRODUC
                SET
                    RECETA      = :newReceta,
                    LOTE        = :newLote,
                    TP_MAQ      = :newTpMaq,
                    COD_MAQ     = :newCodMaq,
                    TITULO      = :newTitulo,
                    C_CODIGO    = :cCodigo,
                    TURNO       = :turno,
                    PASO_MANUAR  = :pasoManuar,
                    FECHA_TURNO  = :fechaTurno,
                    A_MDUSER     = :mdUser,
                    VELOCIDAD    = :velocidad,
                    METRAJE      = :metraje,
                    FECHA_INI    = TO_DATE(:newFechaIni, 'YYYY-MM-DD HH24:MI:SS'){extraSetActualizar}
                WHERE NVL(TRIM(TO_CHAR(RECETA)), ' ')              = NVL(TRIM(:oldReceta), ' ')
                  AND TRIM(LOTE)                                    = TRIM(:oldLote)
                  AND TRIM(TP_MAQ)                                  = TRIM(:oldTpMaq)
                  AND TRIM(COD_MAQ)                                 = TRIM(:oldCodMaq)
                  AND TRIM(TITULO)                                  = TRIM(:oldTitulo)
                  AND TO_CHAR(FECHA_INI, 'YYYY-MM-DD HH24:MI:SS') = :fechaIni
                         AND ESTADO  = '1'";

                  try
                  {
                      using var connection = new OracleConnection(connectionString);
                      await connection.OpenAsync();

                      using var command = new OracleCommand(query, connection);
                      command.BindByName = true;

                      static object Str(string? v) => string.IsNullOrEmpty(v) ? DBNull.Value : (object)v;

                // SET
                command.Parameters.Add(new OracleParameter(":newReceta",   OracleDbType.Varchar2) { Value = Str(newReceta) });
                command.Parameters.Add(new OracleParameter(":newLote",     OracleDbType.Varchar2) { Value = Str(newLote) });
                command.Parameters.Add(new OracleParameter(":newTpMaq",    OracleDbType.Varchar2) { Value = Str(newTpMaq) });
                command.Parameters.Add(new OracleParameter(":newCodMaq",   OracleDbType.Varchar2) { Value = Str(newCodMaq) });
                command.Parameters.Add(new OracleParameter(":newTitulo",   OracleDbType.Varchar2) { Value = Str(newTitulo) });
                command.Parameters.Add(new OracleParameter(":cCodigo",     OracleDbType.Varchar2) { Value = Str(cCodigo) });
                command.Parameters.Add(new OracleParameter(":turno",       OracleDbType.Varchar2) { Value = Str(turno) });
                command.Parameters.Add(new OracleParameter(":pasoManuar",  OracleDbType.Varchar2) { Value = Str(pasoManuar) });
                command.Parameters.Add(new OracleParameter(":mdUser",      OracleDbType.Varchar2) { Value = Str(mdUser) });
                command.Parameters.Add(new OracleParameter(":velocidad",   OracleDbType.Decimal)  { Value = velocidad.HasValue ? (object)velocidad.Value : DBNull.Value });
                command.Parameters.Add(new OracleParameter(":metraje",     OracleDbType.Decimal)  { Value = metraje.HasValue   ? (object)metraje.Value   : DBNull.Value });
                command.Parameters.Add(new OracleParameter(":newFechaIni", OracleDbType.Varchar2) { Value = newFechaIni.ToString("yyyy-MM-dd HH:mm:ss") });
                // FECHA_TURNO
                var fechaTurno = newFechaIni.Hour < 7
                    ? newFechaIni.Date.AddDays(-1)
                    : newFechaIni.Date;
                command.Parameters.Add(new OracleParameter(":fechaTurno", OracleDbType.Varchar2) { Value = fechaTurno.ToString("dd/MM/yyyy") });
                if (newTpMaq == "P")
                {
                    command.Parameters.Add(new OracleParameter(":contadorIni", OracleDbType.Decimal) { Value = contadorInicial.HasValue ? (object)contadorInicial.Value : DBNull.Value });
                    command.Parameters.Add(new OracleParameter(":husosInac",   OracleDbType.Decimal) { Value = husosInactivas.HasValue  ? (object)husosInactivas.Value  : DBNull.Value });
                }
                // WHERE
                command.Parameters.Add(new OracleParameter(":oldReceta",   OracleDbType.Varchar2) { Value = Str(oldReceta) });
                command.Parameters.Add(new OracleParameter(":oldLote",     OracleDbType.Varchar2) { Value = Str(oldLote) });
                command.Parameters.Add(new OracleParameter(":oldTpMaq",    OracleDbType.Varchar2) { Value = Str(oldTpMaq) });
                command.Parameters.Add(new OracleParameter(":oldCodMaq",   OracleDbType.Varchar2) { Value = Str(oldCodMaq) });
                command.Parameters.Add(new OracleParameter(":oldTitulo",   OracleDbType.Varchar2) { Value = Str(oldTitulo) });
                command.Parameters.Add(new OracleParameter(":fechaIni",    OracleDbType.Varchar2) { Value = fechaIni.ToString("yyyy-MM-dd HH:mm:ss") });

                var rowsAffected = await command.ExecuteNonQueryAsync();
                _logger.LogInformation("Preparatoria actualizada en Oracle. Filas afectadas: {Rows}", rowsAffected);
                return rowsAffected > 0;
            }
            catch (OracleException oEx)
            {
                _logger.LogError(oEx, "Error de Oracle al actualizar preparatoria. OracleError: {OracleError}", oEx.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general al actualizar preparatoria en Oracle");
                return false;
            }
        }

        public async Task<GuardarCerrarResultado> GuardarYCerrarDetalleProduccionAsync(
            string? receta, string? lote, string? tpMaq, string? codMaq, string? titulo, DateTime fechaIni,
            decimal? velocidad, int? rolloTacho, decimal? kgNeto,
            int? nroParada = null, decimal? contadorFinal = null, DateTime? fechaFin = null)
        {
            var connectionString = GetOracleConnectionString();

            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogWarning("Oracle connection string not configured");
                return new GuardarCerrarResultado { UpdateExitoso = false };
            }

            var extraSetDetalle = tpMaq == "P"
                ? ",\n                    NRO_PARADA   = :parada,\n                    CONTADOR_FIN = :contadorFin"
                : string.Empty;
            var query = $@"
                UPDATE H_RPRODUC
                SET VELOCIDAD = :velocidad,
                    UNIDADES  = :unidades,
                    PESO_NETO = :kgPeso,
                    ESTADO    = '3',
                    FECHA_FIN = :fechaFin{extraSetDetalle}
                WHERE NVL(TRIM(TO_CHAR(RECETA)), ' ')              = NVL(TRIM(:receta), ' ')
                  AND TRIM(LOTE)                                    = TRIM(:lote)
                  AND TRIM(TP_MAQ)                                  = TRIM(:tpMaq)
                  AND TRIM(COD_MAQ)                                 = TRIM(:codMaq)
                  AND TRIM(TITULO)                                  = TRIM(:titulo)
                  AND TO_CHAR(FECHA_INI, 'YYYY-MM-DD HH24:MI:SS') = :fechaIni
                  AND ESTADO = '1'";

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();

                using var command = new OracleCommand(query, connection);
                command.BindByName = true;

                static object Str(string? v) => string.IsNullOrEmpty(v) ? DBNull.Value : (object)v;
                static object Dec(decimal? v) => v.HasValue ? (object)v.Value : DBNull.Value;
                static object Int(int? v) => v.HasValue ? (object)v.Value : DBNull.Value;

                command.Parameters.Add(new OracleParameter(":velocidad", OracleDbType.Decimal)    { Value = Dec(velocidad) });
                command.Parameters.Add(new OracleParameter(":unidades",  OracleDbType.Int32)      { Value = Int(rolloTacho) });
                command.Parameters.Add(new OracleParameter(":kgPeso",    OracleDbType.Decimal)    { Value = Dec(kgNeto) });
                command.Parameters.Add(new OracleParameter(":fechaFin",  OracleDbType.Date)       { Value = fechaFin ?? DateTime.Now });
                command.Parameters.Add(new OracleParameter(":receta",    OracleDbType.Varchar2)   { Value = Str(receta) });
                command.Parameters.Add(new OracleParameter(":lote",      OracleDbType.Varchar2)   { Value = Str(lote) });
                command.Parameters.Add(new OracleParameter(":tpMaq",     OracleDbType.Varchar2)   { Value = Str(tpMaq) });
                command.Parameters.Add(new OracleParameter(":codMaq",    OracleDbType.Varchar2)   { Value = Str(codMaq) });
                command.Parameters.Add(new OracleParameter(":titulo",    OracleDbType.Varchar2)   { Value = Str(titulo) });
                command.Parameters.Add(new OracleParameter(":fechaIni",  OracleDbType.Varchar2)   { Value = fechaIni.ToString("yyyy-MM-dd HH:mm:ss") });
                if (tpMaq == "P")
                {
                    command.Parameters.Add(new OracleParameter(":parada",      OracleDbType.Int32)   { Value = nroParada.HasValue     ? (object)nroParada.Value     : DBNull.Value });
                    command.Parameters.Add(new OracleParameter(":contadorFin", OracleDbType.Decimal) { Value = contadorFinal.HasValue ? (object)contadorFinal.Value : DBNull.Value });
                }

                _logger.LogInformation(
                    "GuardarYCerrar WHERE params: receta=[{Receta}] lote=[{Lote}] tpMaq=[{TpMaq}] codMaq=[{CodMaq}] titulo=[{Titulo}] fechaIni=[{FechaIni}]",
                    receta, lote, tpMaq, codMaq, titulo, fechaIni.ToString("yyyy-MM-dd HH:mm:ss"));

                // Diagnóstico: obtener FECHA_INI y ESTADO reales en Oracle antes del UPDATE
                const string diagSql = @"
                    SELECT fecha_ini_str, estado
                    FROM (
                        SELECT TO_CHAR(FECHA_INI, 'YYYY-MM-DD HH24:MI:SS') AS fecha_ini_str,
                               ESTADO
                        FROM H_RPRODUC
                        WHERE NVL(TRIM(TO_CHAR(RECETA)), ' ') = NVL(TRIM(:dReceta), ' ')
                          AND TRIM(LOTE)    = TRIM(:dLote)
                          AND TRIM(TP_MAQ)  = TRIM(:dTpMaq)
                          AND TRIM(COD_MAQ) = TRIM(:dCodMaq)
                          AND TRIM(TITULO)  = TRIM(:dTitulo)
                        ORDER BY FECHA_INI DESC
                    )
                    WHERE ROWNUM <= 5";
                using (var diagCmd = new OracleCommand(diagSql, connection))
                {
                    diagCmd.Parameters.Add(new OracleParameter(":dReceta",  OracleDbType.Varchar2) { Value = Str(receta) });
                    diagCmd.Parameters.Add(new OracleParameter(":dLote",    OracleDbType.Varchar2) { Value = Str(lote) });
                    diagCmd.Parameters.Add(new OracleParameter(":dTpMaq",   OracleDbType.Varchar2) { Value = Str(tpMaq) });
                    diagCmd.Parameters.Add(new OracleParameter(":dCodMaq",  OracleDbType.Varchar2) { Value = Str(codMaq) });
                    diagCmd.Parameters.Add(new OracleParameter(":dTitulo",  OracleDbType.Varchar2) { Value = Str(titulo) });
                    using var diagReader = await diagCmd.ExecuteReaderAsync();
                    var diagFound = false;
                    while (await diagReader.ReadAsync())
                    {
                        diagFound = true;
                        var fechaOra  = diagReader.IsDBNull(diagReader.GetOrdinal("fecha_ini_str")) ? "(null)" : diagReader.GetString(diagReader.GetOrdinal("fecha_ini_str"));
                        var estadoOra = diagReader.IsDBNull(diagReader.GetOrdinal("estado"))        ? "(null)" : diagReader.GetString(diagReader.GetOrdinal("estado"));
                        _logger.LogInformation(
                            "DIAG H_RPRODUC: FECHA_INI_Oracle=[{FechaOra}] ESTADO=[{EstadoOra}] | SQLite envía FechaIni=[{FechaSQLite}] | ¿Coinciden? {Match}",
                            fechaOra,
                            estadoOra,
                            fechaIni.ToString("yyyy-MM-dd HH:mm:ss"),
                            (fechaOra == fechaIni.ToString("yyyy-MM-dd HH:mm:ss")).ToString());
                    }
                    if (!diagFound)
                        _logger.LogWarning("DIAG H_RPRODUC: ninguna fila encontrada para lote=[{Lote}] tpMaq=[{TpMaq}] codMaq=[{CodMaq}] titulo=[{Titulo}] (sin filtro de fecha/estado)",
                            lote, tpMaq, codMaq, titulo);
                }

                var rowsAffected = await command.ExecuteNonQueryAsync();
                _logger.LogInformation("GuardarYCerrar: filas afectadas en H_RPRODUC = {Rows}", rowsAffected);

                if (rowsAffected <= 0)
                    return new GuardarCerrarResultado { UpdateExitoso = false };

                // Ejecutar SP_CALCULAR_PROD_ESP_TEO tras el UPDATE exitoso
                using var procCommand = new OracleCommand("SIG.PKG_PROD_RUTINAS.SP_CALCULAR_PROD_ESP_TEO", connection);
                procCommand.CommandType = CommandType.StoredProcedure;
                procCommand.BindByName  = true;

                procCommand.Parameters.Add(new OracleParameter("pi_receta",    OracleDbType.Varchar2, 200) { Direction = ParameterDirection.Input,  Value = Str(receta) });
                procCommand.Parameters.Add(new OracleParameter("pi_lote",      OracleDbType.Varchar2, 200) { Direction = ParameterDirection.Input,  Value = Str(lote) });
                procCommand.Parameters.Add(new OracleParameter("pi_tp_maq",    OracleDbType.Varchar2, 10)  { Direction = ParameterDirection.Input,  Value = Str(tpMaq) });
                procCommand.Parameters.Add(new OracleParameter("pi_cod_maq",   OracleDbType.Varchar2, 20)  { Direction = ParameterDirection.Input,  Value = Str(codMaq) });
                procCommand.Parameters.Add(new OracleParameter("pi_titulo",    OracleDbType.Varchar2, 20)  { Direction = ParameterDirection.Input,  Value = Str(titulo) });
                procCommand.Parameters.Add(new OracleParameter("pi_fecha_ini", OracleDbType.Varchar2, 30)  { Direction = ParameterDirection.Input,  Value = fechaIni.ToString("yyyy-MM-dd HH:mm:ss") });
                var poResultado = new OracleParameter("po_resultado", OracleDbType.Varchar2, 4000) { Direction = ParameterDirection.Output };
                procCommand.Parameters.Add(poResultado);

                await procCommand.ExecuteNonQueryAsync();

                var resultadoStr = poResultado.Value?.ToString() ?? "0|";
                var sepIdx  = resultadoStr.IndexOf('|');
                var codigo  = sepIdx > 0  ? resultadoStr[..sepIdx]       : "0";
                var mensaje = sepIdx >= 0 ? resultadoStr[(sepIdx + 1)..] : string.Empty;

                _logger.LogInformation("SP_CALCULAR_PROD_ESP_TEO resultado: Codigo={Codigo}, Mensaje={Mensaje}", codigo, mensaje);

                return new GuardarCerrarResultado { UpdateExitoso = true, Codigo = codigo, Mensaje = mensaje };
            }
            catch (OracleException oEx)
            {
                _logger.LogError(oEx, "Error de Oracle en GuardarYCerrarDetalleProduccion. OracleError: {OracleError}", oEx.Message);
                return new GuardarCerrarResultado { UpdateExitoso = false };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general en GuardarYCerrarDetalleProduccion");
                return new GuardarCerrarResultado { UpdateExitoso = false };
            }
        }

        public async Task<DetalleProductivoOracleDto?> ObtenerDetalleProductivoOracleAsync(
            string? receta, string? lote, string? tpMaq, string? codMaq, string? titulo, DateTime fechaIni)
        {
            var connectionString = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogWarning("Oracle connection string not configured");
                return null;
            }

            const string query = @"
                SELECT VELOCIDAD, METRAJE, UNIDADES, PESO_NETO,
                       FECHA_FIN, PROD_TEORICO, PROD_ESPERADO,
                       HUSOS_INAC, NRO_PARADA, CONTADOR_INI, CONTADOR_FIN, ESTADO
                FROM H_RPRODUC
                WHERE NVL(TRIM(TO_CHAR(RECETA)), ' ')              = NVL(TRIM(:receta), ' ')
                  AND TRIM(LOTE)                                    = TRIM(:lote)
                  AND TRIM(TP_MAQ)                                  = TRIM(:tpMaq)
                  AND TRIM(COD_MAQ)                                 = TRIM(:codMaq)
                  AND TRIM(TITULO)                                  = TRIM(:titulo)
                  AND TO_CHAR(FECHA_INI, 'YYYY-MM-DD HH24:MI:SS') = :fechaIni
                  AND ROWNUM = 1";

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();

                using var command = new OracleCommand(query, connection);
                command.BindByName = true;

                static object Str(string? v) => string.IsNullOrEmpty(v) ? DBNull.Value : (object)v;
                command.Parameters.Add(new OracleParameter(":receta",   OracleDbType.Varchar2) { Value = Str(receta) });
                command.Parameters.Add(new OracleParameter(":lote",     OracleDbType.Varchar2) { Value = Str(lote) });
                command.Parameters.Add(new OracleParameter(":tpMaq",    OracleDbType.Varchar2) { Value = Str(tpMaq) });
                command.Parameters.Add(new OracleParameter(":codMaq",   OracleDbType.Varchar2) { Value = Str(codMaq) });
                command.Parameters.Add(new OracleParameter(":titulo",   OracleDbType.Varchar2) { Value = Str(titulo) });
                command.Parameters.Add(new OracleParameter(":fechaIni", OracleDbType.Varchar2) { Value = fechaIni.ToString("yyyy-MM-dd HH:mm:ss") });

                using var reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync()) return null;

                static decimal? Dec(IDataReader r, string col)
                {
                    var ord = r.GetOrdinal(col);
                    return r.IsDBNull(ord) ? null : Convert.ToDecimal(r.GetValue(ord));
                }
                static int? Int(IDataReader r, string col)
                {
                    var ord = r.GetOrdinal(col);
                    return r.IsDBNull(ord) ? null : Convert.ToInt32(r.GetValue(ord));
                }
                static DateTime? Dt(IDataReader r, string col)
                {
                    var ord = r.GetOrdinal(col);
                    return r.IsDBNull(ord) ? null : Convert.ToDateTime(r.GetValue(ord));
                }

                return new DetalleProductivoOracleDto
                {
                    Velocidad     = Dec(reader, "VELOCIDAD"),
                    Metraje       = Dec(reader, "METRAJE"),
                    Unidades      = Int(reader, "UNIDADES"),
                    PesoNeto      = Dec(reader, "PESO_NETO"),
                    FechaFin      = Dt(reader,  "FECHA_FIN"),
                    ProducTeorico = Dec(reader, "PROD_TEORICO"),
                    ProdEsperado  = Dec(reader, "PROD_ESPERADO"),
                    HusosInac     = Dec(reader, "HUSOS_INAC"),
                    NroParada     = Int(reader, "NRO_PARADA"),
                    ContadorIni   = Dec(reader, "CONTADOR_INI"),
                    ContadorFin   = Dec(reader, "CONTADOR_FIN"),
                    EstadoOracle  = reader.IsDBNull(reader.GetOrdinal("ESTADO")) ? null : reader["ESTADO"]?.ToString()?.Trim()
                };
            }
            catch (OracleException oEx)
            {
                _logger.LogError(oEx, "Error de Oracle al obtener detalle productivo. OracleError: {OracleError}", oEx.Message);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general al obtener detalle productivo en Oracle");
                return null;
            }
        }

        public async Task<bool> AgregarRolloAsync(DateTime fechaTurno, string turno, string tpMaq, string codMaq, decimal neto, string? adUser)
        {
            var connectionString = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogWarning("Oracle connection string not configured");
                return false;
            }

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();

                const string queryMaxItem = @"
                    SELECT NVL(MAX(ITEM), 0) + 1 AS NEXT_ITEM
                    FROM SIG.H_RPRODUC_ROLLO
                    WHERE FECHA_TURNO = :fechaTurno
                      AND TURNO       = :turno
                      AND TP_MAQ      = :tpMaq
                      AND COD_MAQ     = :codMaq";

                int nextItem;
                using (var cmdMax = new OracleCommand(queryMaxItem, connection))
                {
                    cmdMax.BindByName = true;
                    cmdMax.Parameters.Add(new OracleParameter(":fechaTurno", OracleDbType.Date)     { Value = fechaTurno.Date });
                    cmdMax.Parameters.Add(new OracleParameter(":turno",      OracleDbType.Varchar2) { Value = turno });
                    cmdMax.Parameters.Add(new OracleParameter(":tpMaq",      OracleDbType.Varchar2) { Value = tpMaq });
                    cmdMax.Parameters.Add(new OracleParameter(":codMaq",     OracleDbType.Varchar2) { Value = codMaq });
                    var result = await cmdMax.ExecuteScalarAsync();
                    nextItem = result != null && result != DBNull.Value ? Convert.ToInt32(result) : 1;
                }

                const string queryInsert = @"
                    INSERT INTO SIG.H_RPRODUC_ROLLO (FECHA_TURNO, TURNO, TP_MAQ, COD_MAQ, ITEM, NETO, A_ADUSER, A_ADFECHA)
                    VALUES (:fechaTurno, :turno, :tpMaq, :codMaq, :item, :neto, :adUser, :adFecha)";

                using var cmdInsert = new OracleCommand(queryInsert, connection);
                cmdInsert.BindByName = true;
                cmdInsert.Parameters.Add(new OracleParameter(":fechaTurno", OracleDbType.Date)     { Value = fechaTurno.Date });
                cmdInsert.Parameters.Add(new OracleParameter(":turno",      OracleDbType.Varchar2) { Value = turno });
                cmdInsert.Parameters.Add(new OracleParameter(":tpMaq",      OracleDbType.Varchar2) { Value = tpMaq });
                cmdInsert.Parameters.Add(new OracleParameter(":codMaq",     OracleDbType.Varchar2) { Value = codMaq });
                cmdInsert.Parameters.Add(new OracleParameter(":item",       OracleDbType.Int32)    { Value = nextItem });
                cmdInsert.Parameters.Add(new OracleParameter(":neto",       OracleDbType.Decimal)  { Value = neto });
                cmdInsert.Parameters.Add(new OracleParameter(":adUser",     OracleDbType.Varchar2) { Value = adUser ?? string.Empty });
                cmdInsert.Parameters.Add(new OracleParameter(":adFecha",    OracleDbType.Date)     { Value = DateTime.Now });

                var rows = await cmdInsert.ExecuteNonQueryAsync();
                _logger.LogInformation("Rollo insertado en H_RPRODUC_ROLLOS. Item={Item}, TpMaq={TpMaq}, CodMaq={CodMaq}, Neto={Neto}", nextItem, tpMaq, codMaq, neto);
                return rows > 0;
            }
            catch (OracleException oEx)
            {
                _logger.LogError(oEx, "Error de Oracle al agregar rollo. TpMaq={TpMaq}, CodMaq={CodMaq}", tpMaq, codMaq);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general al agregar rollo. TpMaq={TpMaq}, CodMaq={CodMaq}", tpMaq, codMaq);
                return false;
            }
        }

        public async Task<List<RolloDto>> ObtenerRollosPorMaquinaAsync(DateTime fechaTurno, string turno, string tpMaq, string codMaq, DateTime? fechaIni = null, DateTime? fechaFin = null)
        {
            var connectionString = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connectionString)) return new List<RolloDto>();

            var query = @"
                SELECT ITEM, NETO, A_ADFECHA
                FROM SIG.H_RPRODUC_ROLLO
                WHERE FECHA_TURNO = :fechaTurno
                  AND TURNO       = :turno
                  AND TP_MAQ      = :tpMaq
                  AND COD_MAQ     = :codMaq";

            if (fechaIni.HasValue)
                query += "\n                  AND A_ADFECHA >= :fechaIni";
            if (fechaFin.HasValue)
                query += "\n                  AND A_ADFECHA <= :fechaFin";

            query += "\n                ORDER BY ITEM";

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();
                using var command = new OracleCommand(query, connection);
                command.BindByName = true;
                command.Parameters.Add(new OracleParameter(":fechaTurno", OracleDbType.Date)     { Value = fechaTurno.Date });
                command.Parameters.Add(new OracleParameter(":turno",      OracleDbType.Varchar2) { Value = turno });
                command.Parameters.Add(new OracleParameter(":tpMaq",      OracleDbType.Varchar2) { Value = tpMaq });
                command.Parameters.Add(new OracleParameter(":codMaq",     OracleDbType.Varchar2) { Value = codMaq });
                if (fechaIni.HasValue)
                    command.Parameters.Add(new OracleParameter(":fechaIni", OracleDbType.TimeStamp) { Value = fechaIni.Value });
                if (fechaFin.HasValue)
                    command.Parameters.Add(new OracleParameter(":fechaFin", OracleDbType.TimeStamp) { Value = fechaFin.Value });

                var result = new List<RolloDto>();
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new RolloDto
                    {
                        Item = reader.IsDBNull(reader.GetOrdinal("ITEM")) ? 0 : Convert.ToInt32(reader["ITEM"]),
                        Neto = reader.IsDBNull(reader.GetOrdinal("NETO")) ? 0m : Convert.ToDecimal(reader["NETO"]),
                        FechaRegistro = reader.IsDBNull(reader.GetOrdinal("A_ADFECHA")) ? null : Convert.ToDateTime(reader["A_ADFECHA"])
                    });
                }
                return result;
            }
            catch (OracleException oEx)
            {
                _logger.LogError(oEx, "Error de Oracle al obtener rollos BATAN. TpMaq={TpMaq}, CodMaq={CodMaq}", tpMaq, codMaq);
                return new List<RolloDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general al obtener rollos BATAN. TpMaq={TpMaq}, CodMaq={CodMaq}", tpMaq, codMaq);
                return new List<RolloDto>();
            }
        }

        public async Task<List<DestinoDto>> ObtenerDestinosAutoconerAsync()
        {
            var connectionString = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connectionString)) return new List<DestinoDto>();

            const string query = "SELECT CODIGO, DESCRIPCION FROM H_TPROD WHERE TABLA = '63' AND ESTADO <> '9' ORDER BY CODIGO";
            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();
                using var command = new OracleCommand(query, connection);
                var result = new List<DestinoDto>();
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new DestinoDto
                    {
                        Codigo      = reader["CODIGO"]?.ToString()?.Trim()      ?? string.Empty,
                        Descripcion = reader["DESCRIPCION"]?.ToString()?.Trim() ?? string.Empty
                    });
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener destinos Autoconer desde H_TPROD");
                return new List<DestinoDto>();
            }
        }

        public async Task<bool> ActualizarUltimoRolloBatanAsync(DateTime fechaTurno, string turno, string tpMaq, string codMaq, DateTime fechaIni, string? mdUser)
        {
            var connectionString = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connectionString)) return false;

            const string query = @"
                UPDATE SIG.H_RPRODUC_ROLLO
                SET    A_MDUSER  = :mdUser,
                       A_MDFECHA = :mdFecha
                WHERE  FECHA_TURNO = :fechaTurno
                  AND  TURNO       = :turno
                  AND  TP_MAQ      = :tpMaq
                  AND  COD_MAQ     = :codMaq
                  AND  A_ADFECHA   >= :fechaIni
                  AND  ITEM        = (
                           SELECT MAX(ITEM)
                           FROM   SIG.H_RPRODUC_ROLLO
                           WHERE  FECHA_TURNO = :fechaTurno
                             AND  TURNO       = :turno
                             AND  TP_MAQ      = :tpMaq
                             AND  COD_MAQ     = :codMaq
                             AND  A_ADFECHA   >= :fechaIni
                       )";

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();
                using var command = new OracleCommand(query, connection);
                command.BindByName = true;
                command.Parameters.Add(new OracleParameter(":mdUser",     OracleDbType.Varchar2)  { Value = mdUser ?? string.Empty });
                command.Parameters.Add(new OracleParameter(":mdFecha",    OracleDbType.TimeStamp) { Value = DateTime.Now });
                command.Parameters.Add(new OracleParameter(":fechaTurno", OracleDbType.Date)      { Value = fechaTurno.Date });
                command.Parameters.Add(new OracleParameter(":turno",      OracleDbType.Varchar2)  { Value = turno });
                command.Parameters.Add(new OracleParameter(":tpMaq",      OracleDbType.Varchar2)  { Value = tpMaq });
                command.Parameters.Add(new OracleParameter(":codMaq",     OracleDbType.Varchar2)  { Value = codMaq });
                command.Parameters.Add(new OracleParameter(":fechaIni",   OracleDbType.TimeStamp) { Value = fechaIni });

                var rows = await command.ExecuteNonQueryAsync();
                _logger.LogInformation("Último rollo BATAN actualizado (A_MDUSER/A_MDFECHA). TpMaq={TpMaq}, CodMaq={CodMaq}, Filas={Rows}", tpMaq, codMaq, rows);
                return rows > 0;
            }
            catch (OracleException oEx)
            {
                _logger.LogError(oEx, "Error de Oracle al actualizar último rollo BATAN. TpMaq={TpMaq}, CodMaq={CodMaq}", tpMaq, codMaq);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general al actualizar último rollo BATAN. TpMaq={TpMaq}, CodMaq={CodMaq}", tpMaq, codMaq);
                return false;
            }
        }
    }
}
