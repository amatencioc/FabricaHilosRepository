using FabricaHilos.Models.RecursosHumanos;
using Microsoft.Extensions.Caching.Memory;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace FabricaHilos.Services.RecursosHumanos;

public interface ICompensacionesService
{
    Task<List<EmpleadoDto>> BuscarEmpleadoAsync(string codEmpresa, string? nombre);
    Task<(List<EmpleadoDto> Items, int Total)> ListarEmpleadosAsync(string codEmpresa, string? buscar, int page, int pageSize);
    Task<List<CompensacionRangoDto>> ConsultarRangoAsync(string codEmpresa, string codPersonal, DateTime fechaInicio, DateTime fechaFin);
    Task<List<SaldoBancoDto>> ConsultarSaldoBancoAsync(string codEmpresa, string codPersonal, int? anio, int? mes);
    Task<CompensacionValidarDto> ValidarAsync(string codEmpresa, string codPersonal, string? fechaDestino, string fechaOrigen, char tipoOrigen, char tipoCompensacion, string horas);
    Task<CompensacionRegistrarDto> RegistrarAsync(string codEmpresa, string codPersonal, string? fechaDestino, string fechaOrigen, char tipoOrigen, char tipoCompensacion, string horas, string? perid, string? tipoBanco, string? proceso, string validar);
    Task<CompensacionEliminarDto> EliminarAsync(int idCompen, string revertirTareo);
    Task<AplicarRangoResultadoDto> AplicarRangoAsync(string codEmpresa, string codPersonal, DateTime fechaInicio, DateTime fechaFin);
    Task<List<DiagnosticoDiaDto>> DiagnosticoRangoAsync(string codEmpresa, string codPersonal, DateTime fechaInicio, DateTime fechaFin);
    Task<(List<EmpleadoDisponibleDto> Items, int Total)> ListarEmpleadosDisponiblesAsync(string codEmpresa, DateTime fechaOrigen, string tipoOrigen, int page, int pageSize);
    void InvalidarCacheEmpleados(string codEmpresa);
}

public class CompensacionesService : ICompensacionesService
{
    private readonly string _baseConnectionString;
    private readonly ILogger<CompensacionesService> _logger;
    private readonly IMemoryCache _cache;

    private const string Paquete = "AQUARIUS.PKG_SCA_COMPENSACIONES";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private static string CacheKeyEmpleados(string emp) => $"comp_empleados_{emp}";

    public CompensacionesService(IConfiguration configuration, ILogger<CompensacionesService> logger, IMemoryCache cache)
    {
        _baseConnectionString = configuration.GetConnectionString("AquariusConnection")
            ?? throw new InvalidOperationException("Aquarius connection string not found.");
        _logger = logger;
        _cache  = cache;
    }

    public void InvalidarCacheEmpleados(string codEmpresa) =>
        _cache.Remove(CacheKeyEmpleados(codEmpresa));

    private string GetOracleConnectionString() => _baseConnectionString;

    private static string? GetStr(OracleDataReader r, string col)
    {
        try { return r[col] == DBNull.Value ? null : r[col]?.ToString(); }
        catch { return null; }
    }

    private static int GetInt(OracleDataReader r, string col)
    {
        try { return r[col] == DBNull.Value ? 0 : Convert.ToInt32(r[col]); }
        catch { return 0; }
    }

    private static int? GetNullInt(OracleDataReader r, string col)
    {
        try { return r[col] == DBNull.Value ? null : Convert.ToInt32(r[col]); }
        catch { return null; }
    }

    private static bool IsOra04068(OracleException ex) =>
        ex.Number == 4068 || ex.Number == 4061 || ex.Number == 4065 || ex.Number == 6508;

    private async Task<T> WithOracleRetryAsync<T>(Func<Task<T>> operation, string contexto)
    {
        try
        {
            return await operation();
        }
        catch (OracleException ex) when (IsOra04068(ex))
        {
            _logger.LogWarning("ORA-04068 en {Contexto}, reintentando con nueva conexión...", contexto);
            return await operation();
        }
    }

    // ── BUSCAR_EMPLEADO ────────────────────────────────────────────────────

    public async Task<List<EmpleadoDto>> BuscarEmpleadoAsync(string codEmpresa, string? nombre)
    {
        // Filtrar desde caché para no ir a Oracle en cada tecla del autocomplete
        if (!string.IsNullOrWhiteSpace(nombre))
        {
            var todos = await ObtenerTodosEmpleadosAsync(codEmpresa);
            var q = nombre.Trim().ToUpperInvariant();
            return todos
                .Where(e => (e.Empleado ?? string.Empty).ToUpperInvariant().Contains(q)
                         || (e.Personal ?? string.Empty).ToUpperInvariant().Contains(q))
                .ToList();
        }
        return await ObtenerTodosEmpleadosAsync(codEmpresa);
    }

    private async Task<List<EmpleadoDto>> ObtenerTodosEmpleadosAsync(string codEmpresa)
    {
        var key = CacheKeyEmpleados(codEmpresa);
        if (_cache.TryGetValue(key, out List<EmpleadoDto>? cached) && cached != null)
            return cached;

        var result = await CargarEmpleadosOracle(codEmpresa);
        _cache.Set(key, result, CacheDuration);
        return result;
    }

    private async Task<List<EmpleadoDto>> CargarEmpleadosOracle(string codEmpresa)
    {
        try
        {
            return await WithOracleRetryAsync(async () =>
            {
                var result = new List<EmpleadoDto>();
                await using var conn = new OracleConnection(GetOracleConnectionString());
                await conn.OpenAsync();

                await using var cmd = conn.CreateCommand();
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = $"{Paquete}.BUSCAR_EMPLEADO";

                cmd.Parameters.Add(new OracleParameter("p_cod_empresa", OracleDbType.Varchar2) { Value = codEmpresa });
                cmd.Parameters.Add(new OracleParameter("p_nombre",      OracleDbType.Varchar2) { Value = DBNull.Value });
                cmd.Parameters.Add(new OracleParameter("cv_resultado",  OracleDbType.RefCursor) { Direction = ParameterDirection.Output });

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var r = (OracleDataReader)reader;
                    result.Add(new EmpleadoDto
                    {
                        Personal  = GetStr(r, "cod_personal"),
                        Fotocheck = GetStr(r, "num_fotocheck"),
                        Empleado  = GetStr(r, "nombre_completo"),
                        TipEstado = GetStr(r, "tip_estado"),
                    });
                }
                return result;
            }, "BUSCAR_EMPLEADO");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en BUSCAR_EMPLEADO (Oracle): empresa={Empresa}", codEmpresa);
            throw;
        }
    }

    // ── LISTAR EMPLEADOS PAGINADO ──────────────────────────────────────────

    public async Task<(List<EmpleadoDto> Items, int Total)> ListarEmpleadosAsync(string codEmpresa, string? buscar, int page, int pageSize)
    {
        var todos = await BuscarEmpleadoAsync(codEmpresa, string.IsNullOrWhiteSpace(buscar) ? null : buscar);
        var total = todos.Count;
        var items = todos
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        return (items, total);
    }

    // ── CONSULTAR_RANGO ────────────────────────────────────────────────────

    public async Task<List<CompensacionRangoDto>> ConsultarRangoAsync(string codEmpresa, string codPersonal, DateTime fechaInicio, DateTime fechaFin)
    {
        try
        {
            return await WithOracleRetryAsync(async () =>
            {
                var result = new List<CompensacionRangoDto>();
                await using var conn = new OracleConnection(GetOracleConnectionString());
                await conn.OpenAsync();

                await using var cmd = conn.CreateCommand();
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = $"{Paquete}.CONSULTAR_RANGO";

                cmd.Parameters.Add(new OracleParameter("p_cod_empresa",  OracleDbType.Varchar2) { Value = codEmpresa });
                cmd.Parameters.Add(new OracleParameter("p_cod_personal", OracleDbType.Varchar2) { Value = string.IsNullOrEmpty(codPersonal) ? (object)DBNull.Value : codPersonal });
                cmd.Parameters.Add(new OracleParameter("p_fecha_inicio", OracleDbType.Varchar2) { Value = fechaInicio.ToString("dd/MM/yyyy") });
                cmd.Parameters.Add(new OracleParameter("p_fecha_fin",    OracleDbType.Varchar2) { Value = fechaFin.ToString("dd/MM/yyyy") });
                cmd.Parameters.Add(new OracleParameter("cv_resultado",   OracleDbType.RefCursor) { Direction = ParameterDirection.Output });

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var r = (OracleDataReader)reader;
                    result.Add(new CompensacionRangoDto
                    {
                        IdCompen         = GetNullInt(r, "id_compen"),
                        CodEmpresa       = GetStr(r, "cod_empresa"),
                        CodPersonal      = GetStr(r, "cod_personal"),
                        FechaOrigen      = GetStr(r, "fechaorigen"),
                        FechaDestino     = GetStr(r, "fechadestino"),
                        TipoOrigen       = GetStr(r, "tipoorigen"),
                        TipoCompensacion = GetStr(r, "tipocompensacion"),
                        TiempoMin        = GetNullInt(r, "tiempo_min"),
                        TiempoHhmi       = GetStr(r, "tiempo_hhmi"),
                        Periodo          = GetStr(r, "periodo"),
                        DestAlerta02     = GetStr(r, "dest_alerta02"),
                        DestAlerta03     = GetStr(r, "dest_alerta03"),
                        DestAlerta04     = GetStr(r, "dest_alerta04"),
                        DestAlerta07     = GetStr(r, "dest_alerta07"),
                        DestAlerta09     = GetStr(r, "dest_alerta09"),
                        OriAlerta06      = GetStr(r, "ori_alerta06"),
                        OriAlerta08      = GetStr(r, "ori_alerta08"),
                        EstadoAplicacion = GetStr(r, "estado_aplicacion"),
                    });
                }
                return result;
            }, "CONSULTAR_RANGO");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en CONSULTAR_RANGO Compensaciones: empresa={Empresa}, personal={Personal}", codEmpresa, codPersonal);
            throw;
        }
    }

    // ── CONSULTAR_SALDO_BANCO ──────────────────────────────────────────────

    public async Task<List<SaldoBancoDto>> ConsultarSaldoBancoAsync(string codEmpresa, string codPersonal, int? anio, int? mes)
    {
        try
        {
            return await WithOracleRetryAsync(async () =>
            {
                var result = new List<SaldoBancoDto>();
                await using var conn = new OracleConnection(GetOracleConnectionString());
                await conn.OpenAsync();

                await using var cmd = conn.CreateCommand();
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = $"{Paquete}.CONSULTAR_SALDO_BANCO";

                cmd.Parameters.Add(new OracleParameter("p_cod_empresa",  OracleDbType.Varchar2) { Value = codEmpresa });
                cmd.Parameters.Add(new OracleParameter("p_cod_personal", OracleDbType.Varchar2) { Value = codPersonal });
                cmd.Parameters.Add(new OracleParameter("p_anio",         OracleDbType.Int32)    { Value = (object?)anio ?? DBNull.Value });
                cmd.Parameters.Add(new OracleParameter("p_mes",          OracleDbType.Int32)    { Value = (object?)mes  ?? DBNull.Value });
                cmd.Parameters.Add(new OracleParameter("cv_resultado",   OracleDbType.RefCursor) { Direction = ParameterDirection.Output });

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var r = (OracleDataReader)reader;
                    result.Add(new SaldoBancoDto
                    {
                        TipoBanco  = GetStr(r, "tipo_banco"),
                        AnoProceso = GetStr(r, "ano_proceso"),
                        MesProceso = GetStr(r, "mes_proceso"),
                        SemProceso = GetStr(r, "sem_proceso"),
                        SaldoMin   = GetNullInt(r, "saldo_min"),
                        SaldoHhmi  = GetStr(r, "saldo_hhmi"),
                    });
                }
                return result;
            }, "CONSULTAR_SALDO_BANCO");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en CONSULTAR_SALDO_BANCO: empresa={Empresa}, personal={Personal}", codEmpresa, codPersonal);
            throw;
        }
    }

    // ── VALIDAR ────────────────────────────────────────────────────────────

    public async Task<CompensacionValidarDto> ValidarAsync(string codEmpresa, string codPersonal, string? fechaDestino, string fechaOrigen, char tipoOrigen, char tipoCompensacion, string horas)
    {
        try
        {
            return await WithOracleRetryAsync(async () =>
            {
                await using var conn = new OracleConnection(GetOracleConnectionString());
                await conn.OpenAsync();

                await using var cmd = conn.CreateCommand();
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = $"{Paquete}.VALIDAR";

                cmd.Parameters.Add(new OracleParameter("p_cod_empresa",       OracleDbType.Varchar2) { Value = codEmpresa });
                cmd.Parameters.Add(new OracleParameter("p_cod_personal",      OracleDbType.Varchar2) { Value = codPersonal });
                cmd.Parameters.Add(new OracleParameter("p_fecha_destino",     OracleDbType.Varchar2) { Value = (object?)fechaDestino ?? DBNull.Value });
                cmd.Parameters.Add(new OracleParameter("p_fecha_origen",      OracleDbType.Varchar2) { Value = fechaOrigen });
                cmd.Parameters.Add(new OracleParameter("p_tipo_origen",       OracleDbType.Char)     { Value = tipoOrigen.ToString() });
                cmd.Parameters.Add(new OracleParameter("p_tipo_compensacion", OracleDbType.Char)     { Value = tipoCompensacion.ToString() });
                cmd.Parameters.Add(new OracleParameter("p_horas",             OracleDbType.Varchar2) { Value = horas });
                cmd.Parameters.Add(new OracleParameter("cv_resultado",        OracleDbType.RefCursor) { Direction = ParameterDirection.Output });

                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var r = (OracleDataReader)reader;
                    return new CompensacionValidarDto
                    {
                        PuedeAplicar              = GetStr(r, "puede_aplicar"),
                        Motivo                    = GetStr(r, "motivo"),
                        TiempoSolicitadoMin       = GetNullInt(r, "tiempo_solicitado_min"),
                        TiempoDisponibleOrigenMin = GetNullInt(r, "tiempo_disponible_origen_min"),
                        TiempoDeficitDestinoMin   = GetNullInt(r, "tiempo_deficit_destino_min"),
                        TipoValidacion            = GetStr(r, "tipo_validacion"),
                    };
                }

                return new CompensacionValidarDto { PuedeAplicar = "N", Motivo = "SIN_DATOS" };
            }, "VALIDAR");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en VALIDAR Compensaciones: empresa={Empresa}, personal={Personal}", codEmpresa, codPersonal);
            return new CompensacionValidarDto { PuedeAplicar = "N", Motivo = $"ERROR: {ex.Message}" };
        }
    }

    // ── REGISTRAR ─────────────────────────────────────────────────────────

    public async Task<CompensacionRegistrarDto> RegistrarAsync(string codEmpresa, string codPersonal, string? fechaDestino, string fechaOrigen, char tipoOrigen, char tipoCompensacion, string horas, string? perid, string? tipoBanco, string? proceso, string validar)
    {
        try
        {
            return await WithOracleRetryAsync(async () =>
            {
                await using var conn = new OracleConnection(GetOracleConnectionString());
                await conn.OpenAsync();

                await using var cmd = conn.CreateCommand();
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = $"{Paquete}.REGISTRAR";

                cmd.Parameters.Add(new OracleParameter("p_cod_empresa",       OracleDbType.Varchar2) { Value = codEmpresa });
                cmd.Parameters.Add(new OracleParameter("p_cod_personal",      OracleDbType.Varchar2) { Value = codPersonal });
                cmd.Parameters.Add(new OracleParameter("p_fecha_destino",     OracleDbType.Varchar2) { Value = (object?)fechaDestino ?? DBNull.Value });
                cmd.Parameters.Add(new OracleParameter("p_fecha_origen",      OracleDbType.Varchar2) { Value = fechaOrigen });
                cmd.Parameters.Add(new OracleParameter("p_tipo_origen",       OracleDbType.Char)     { Value = tipoOrigen.ToString() });
                cmd.Parameters.Add(new OracleParameter("p_tipo_compensacion", OracleDbType.Char)     { Value = tipoCompensacion.ToString() });
                cmd.Parameters.Add(new OracleParameter("p_horas",             OracleDbType.Varchar2) { Value = horas });
                cmd.Parameters.Add(new OracleParameter("p_perid",             OracleDbType.Varchar2) { Value = (object?)perid    ?? DBNull.Value });
                cmd.Parameters.Add(new OracleParameter("p_tipo_banco",        OracleDbType.Varchar2) { Value = (object?)tipoBanco ?? DBNull.Value });
                cmd.Parameters.Add(new OracleParameter("p_proceso",           OracleDbType.Varchar2) { Value = (object?)proceso   ?? DBNull.Value });
                cmd.Parameters.Add(new OracleParameter("p_validar",           OracleDbType.Varchar2) { Value = validar });
                cmd.Parameters.Add(new OracleParameter("cv_resultado",        OracleDbType.RefCursor) { Direction = ParameterDirection.Output });

                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var r = (OracleDataReader)reader;
                    return new CompensacionRegistrarDto
                    {
                        IdCompen      = GetNullInt(r, "id_compen"),
                        Estado        = GetStr(r, "estado"),
                        Motivo        = GetStr(r, "motivo"),
                        TiempoMinutos = GetNullInt(r, "tiempo_minutos"),
                    };
                }

                return new CompensacionRegistrarDto { Estado = "ERR", Motivo = "SIN_DATOS" };
            }, "REGISTRAR");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en REGISTRAR Compensaciones: empresa={Empresa}, personal={Personal}", codEmpresa, codPersonal);
            return new CompensacionRegistrarDto { Estado = "ERR", Motivo = $"ERROR: {ex.Message}" };
        }
    }

    // ── ELIMINAR ──────────────────────────────────────────────────────────

    public async Task<CompensacionEliminarDto> EliminarAsync(int idCompen, string revertirTareo)
    {
        try
        {
            return await WithOracleRetryAsync(async () =>
            {
                await using var conn = new OracleConnection(GetOracleConnectionString());
                await conn.OpenAsync();

                await using var cmd = conn.CreateCommand();
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = $"{Paquete}.ELIMINAR";

                cmd.Parameters.Add(new OracleParameter("p_id_compen",      OracleDbType.Int32)    { Value = idCompen });
                cmd.Parameters.Add(new OracleParameter("p_revertir_tareo", OracleDbType.Varchar2) { Value = revertirTareo });
                cmd.Parameters.Add(new OracleParameter("cv_resultado",     OracleDbType.RefCursor) { Direction = ParameterDirection.Output });

                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var r = (OracleDataReader)reader;
                    return new CompensacionEliminarDto
                    {
                        Estado          = GetStr(r, "estado"),
                        Motivo          = GetStr(r, "motivo"),
                        FilasEliminadas = GetNullInt(r, "filas_eliminadas"),
                    };
                }

                return new CompensacionEliminarDto { Estado = "ERR", Motivo = "SIN_DATOS" };
            }, "ELIMINAR");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en ELIMINAR Compensaciones: idCompen={IdCompen}", idCompen);
            return new CompensacionEliminarDto { Estado = "ERR", Motivo = $"ERROR: {ex.Message}" };
        }
    }

    // ── DIAGNOSTICO_RANGO ──────────────────────────────────────────────────

    public async Task<List<DiagnosticoDiaDto>> DiagnosticoRangoAsync(string codEmpresa, string codPersonal, DateTime fechaInicio, DateTime fechaFin)
    {
        try
        {
            return await WithOracleRetryAsync(async () =>
            {
                var result = new List<DiagnosticoDiaDto>();
                await using var conn = new OracleConnection(GetOracleConnectionString());
                await conn.OpenAsync();

                await using var cmd = conn.CreateCommand();
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = $"{Paquete}.DIAGNOSTICO_RANGO";

                cmd.Parameters.Add(new OracleParameter("p_cod_empresa",  OracleDbType.Varchar2) { Value = codEmpresa });
                cmd.Parameters.Add(new OracleParameter("p_cod_personal", OracleDbType.Varchar2) { Value = codPersonal });
                cmd.Parameters.Add(new OracleParameter("p_fecha_inicio", OracleDbType.Varchar2) { Value = fechaInicio.ToString("dd/MM/yyyy") });
                cmd.Parameters.Add(new OracleParameter("p_fecha_fin",    OracleDbType.Varchar2) { Value = fechaFin.ToString("dd/MM/yyyy") });
                cmd.Parameters.Add(new OracleParameter("cv_resultado",   OracleDbType.RefCursor) { Direction = ParameterDirection.Output });

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var r = (OracleDataReader)reader;
                    result.Add(new DiagnosticoDiaDto
                    {
                        Fecha             = GetStr(r, "fecha"),
                        CodEmpresa        = GetStr(r, "cod_empresa"),
                        CodPersonal       = GetStr(r, "cod_personal"),
                        HeMin             = GetInt(r, "he_min"),
                        HeHhmi            = GetStr(r, "he_hhmi"),
                        DoblesMin         = GetInt(r, "dobles_min"),
                        DoblesHhmi        = GetStr(r, "dobles_hhmi"),
                        BancoMin          = GetInt(r, "banco_min"),
                        BancoHhmi         = GetStr(r, "banco_hhmi"),
                        TardMin           = GetInt(r, "tard_min"),
                        TardHhmi          = GetStr(r, "tard_hhmi"),
                        AntesMin          = GetInt(r, "antes_min"),
                        AntesHhmi         = GetStr(r, "antes_hhmi"),
                        FaltaMin          = GetInt(r, "falta_min"),
                        FaltaHhmi         = GetStr(r, "falta_hhmi"),
                        NotrabMin         = GetInt(r, "notrab_min"),
                        NotrabHhmi        = GetStr(r, "notrab_hhmi"),
                        PermisoMin        = GetInt(r, "permiso_min"),
                        PermisoHhmi       = GetStr(r, "permiso_hhmi"),
                        CompenRegistradas = GetInt(r, "compen_registradas"),
                        CompenAplicadas   = GetInt(r, "compen_aplicadas"),
                        TieneOrigen       = GetStr(r, "tiene_origen"),
                        TieneDeficit      = GetStr(r, "tiene_deficit"),
                        EsDescanso        = GetStr(r, "es_descanso"),
                        EsFeriado         = GetStr(r, "es_feriado"),
                        Alerta01          = GetStr(r, "alerta01"),
                        Sugerencia        = GetStr(r, "sugerencia"),
                    });
                }
                return result;
            }, "DIAGNOSTICO_RANGO");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en DIAGNOSTICO_RANGO: empresa={Empresa}, personal={Personal}", codEmpresa, codPersonal);
            throw;
        }
    }

    // ── APLICAR_RANGO ──────────────────────────────────────────────────────

    public async Task<AplicarRangoResultadoDto> AplicarRangoAsync(string codEmpresa, string codPersonal, DateTime fechaInicio, DateTime fechaFin)
    {
        try
        {
            return await WithOracleRetryAsync(async () =>
            {
                await using var conn = new OracleConnection(GetOracleConnectionString());
                await conn.OpenAsync();

                await using var cmd = conn.CreateCommand();
                cmd.CommandType    = CommandType.StoredProcedure;
                cmd.CommandText    = $"{Paquete}.APLICAR_RANGO";
                cmd.CommandTimeout = 120;

                cmd.Parameters.Add(new OracleParameter("p_cod_empresa",          OracleDbType.Varchar2) { Value = codEmpresa });
                cmd.Parameters.Add(new OracleParameter("p_cod_personal",         OracleDbType.Varchar2) { Value = codPersonal });
                cmd.Parameters.Add(new OracleParameter("p_fecha_inicio",         OracleDbType.Varchar2) { Value = fechaInicio.ToString("dd/MM/yyyy") });
                cmd.Parameters.Add(new OracleParameter("p_fecha_fin",            OracleDbType.Varchar2) { Value = fechaFin.ToString("dd/MM/yyyy") });
                cmd.Parameters.Add(new OracleParameter("p_eliminar_no_cuadra",   OracleDbType.Varchar2) { Value = "S" });
                cmd.Parameters.Add(new OracleParameter("cv_resultado",           OracleDbType.RefCursor) { Direction = ParameterDirection.Output });

                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var r = (OracleDataReader)reader;
                    return new AplicarRangoResultadoDto
                    {
                        FechaInicio           = GetStr(r, "fecha_inicio"),
                        FechaFin              = GetStr(r, "fecha_fin"),
                        CodEmpresa            = GetStr(r, "cod_empresa"),
                        CodPersonal           = GetStr(r, "cod_personal"),
                        DiasProcesados        = GetNullInt(r, "dias_procesados"),
                        TotalAplicadasDestino = GetNullInt(r, "total_aplicadas_destino"),
                        TotalAplicadasOrigen  = GetNullInt(r, "total_aplicadas_origen"),
                        TotalEliminadas       = GetNullInt(r, "total_eliminadas"),
                        TotalErrores          = GetNullInt(r, "total_errores"),
                    };
                }

                return new AplicarRangoResultadoDto { TotalErrores = 1 };
            }, "APLICAR_RANGO");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en APLICAR_RANGO Compensaciones: empresa={Empresa}, personal={Personal}", codEmpresa, codPersonal);
            return new AplicarRangoResultadoDto { TotalErrores = 1 };
        }
    }

    // ── LISTAR EMPLEADOS DISPONIBLES PAGINADO ─────────────────────────────────

    public async Task<(List<EmpleadoDisponibleDto> Items, int Total)> ListarEmpleadosDisponiblesAsync(
        string codEmpresa, DateTime fechaOrigen, string tipoOrigen, int page, int pageSize)
    {
        try
        {
            return await WithOracleRetryAsync(async () =>
            {
                var result   = new List<EmpleadoDisponibleDto>();
                string fecha = fechaOrigen.ToString("yyyy-MM-dd");   // ANSI DATE literal — no bind needed

                string colOrigen = tipoOrigen switch
                {
                    "E" => "horaextra_ajus",
                    "D" => "horadoblesof",
                    "B" => "horabancoh",
                    _   => "horaextra_ajus"
                };

                await using var conn = new OracleConnection(GetOracleConnectionString());
                await conn.OpenAsync();

                // ── COUNT ──────────────────────────────────────────────────
                // horaextra_ajus/horadoblesof/horabancoh son tipo DATE (BASE_DATE + min/1440)
                string sqlCount =
                    $"SELECT COUNT(*) FROM ( " +
                    $"SELECT DISTINCT t.cod_personal FROM AQUARIUS.SCA_ASISTENCIA_TAREO t " +
                    $"WHERE t.cod_empresa = :p_empresa " +
                    $"  AND t.fechamar    = DATE '{fecha}' " +
                    $"  AND t.{colOrigen} > DATE '1900-01-01' )";

                int total = 0;
                await using (var cmdCount = conn.CreateCommand())
                {
                    cmdCount.CommandText = sqlCount;
                    cmdCount.BindByName  = true;
                    cmdCount.Parameters.Add(new OracleParameter("p_empresa", OracleDbType.Varchar2) { Value = codEmpresa });
                    var cnt = await cmdCount.ExecuteScalarAsync();
                    total = cnt == DBNull.Value || cnt == null ? 0 : Convert.ToInt32(cnt);
                }

                if (total == 0) return (result, 0);

                int rowStart = (page - 1) * pageSize + 1;
                int rowEnd   = page * pageSize;

                // ── PAGE ───────────────────────────────────────────────────
                // Minutos = (col - DATE '1900-01-01') * 1440  (col es tipo DATE)
                string sqlPage =
                    $"SELECT rn.*, p.num_fotocheck, TRIM(p.ape_paterno||' '||p.ape_materno||', '||p.nom_trabajador) AS nombre_completo " +
                    $"FROM ( " +
                    $"  SELECT ROWNUM AS rn, q.* FROM ( " +
                    $"    SELECT t.cod_personal, " +
                    $"           ROUND(SUM((t.horaextra_ajus - DATE '1900-01-01') * 1440)) AS min_he, " +
                    $"           ROUND(SUM((t.horadoblesof   - DATE '1900-01-01') * 1440)) AS min_doble, " +
                    $"           ROUND(SUM((t.horabancoh     - DATE '1900-01-01') * 1440)) AS min_banco " +
                    $"    FROM AQUARIUS.SCA_ASISTENCIA_TAREO t " +
                    $"    WHERE t.cod_empresa = :p_empresa " +
                    $"      AND t.fechamar    = DATE '{fecha}' " +
                    $"      AND t.{colOrigen} > DATE '1900-01-01' " +
                    $"    GROUP BY t.cod_personal " +
                    $"    ORDER BY t.cod_personal " +
                    $"  ) q WHERE ROWNUM <= :p_rowend " +
                    $") rn " +
                    $"LEFT JOIN PLA_PERSONAL p ON p.cod_empresa = :p_empresa2 AND p.cod_personal = rn.cod_personal " +
                    $"WHERE rn.rn >= :p_rowstart";

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = sqlPage;
                cmd.BindByName  = true;
                cmd.Parameters.Add(new OracleParameter("p_empresa",  OracleDbType.Varchar2) { Value = codEmpresa });
                cmd.Parameters.Add(new OracleParameter("p_rowend",   OracleDbType.Int32)    { Value = rowEnd });
                cmd.Parameters.Add(new OracleParameter("p_empresa2", OracleDbType.Varchar2) { Value = codEmpresa });
                cmd.Parameters.Add(new OracleParameter("p_rowstart", OracleDbType.Int32)    { Value = rowStart });

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var r = (OracleDataReader)reader;
                    int minHe    = GetInt(r, "min_he");
                    int minDoble = GetInt(r, "min_doble");
                    int minBanco = GetInt(r, "min_banco");

                    static string MinAHhmi(int m) { int h = m / 60; int mm = m % 60; return $"{h:D2}:{mm:D2}"; }

                    result.Add(new EmpleadoDisponibleDto
                    {
                        CodPersonal  = GetStr(r, "cod_personal"),
                        Fotocheck    = GetStr(r, "num_fotocheck"),
                        Nombre       = GetStr(r, "nombre_completo"),
                        MinutosHe    = minHe,
                        MinutosDoble = minDoble,
                        MinutosBanco = minBanco,
                        HhmiHe       = MinAHhmi(minHe),
                        HhmiDoble    = MinAHhmi(minDoble),
                        HhmiBanco    = MinAHhmi(minBanco),
                    });
                }

                return (result, total);
            }, "LISTAR_EMPLEADOS_DISPONIBLES");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en LISTAR_EMPLEADOS_DISPONIBLES: empresa={Empresa}, fecha={Fecha}", codEmpresa, fechaOrigen);
            throw;
        }
    }
}
