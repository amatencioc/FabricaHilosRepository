using FabricaHilos.Models.RecursosHumanos;
using Microsoft.Extensions.Caching.Memory;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace FabricaHilos.Services.RecursosHumanos;

public interface IMarcacionesService
{
    Task<List<EmpleadoDto>> BuscarEmpleadoAsync(string codEmpresa, string? nombre);
    Task<(List<EmpleadoDto> Items, int Total)> ListarEmpleadosAsync(string codEmpresa, string? buscar, int page, int pageSize);
    Task<List<MarcacionRangoDto>> ConsultarRangoAsync(string codEmpresa, string codPersonal, DateTime fechaInicio, DateTime fechaFin);
    Task<DepuraRangoResultadoDto> DepurarRangoAsync(string codEmpresa, string codPersonal, DateTime fechaInicio, DateTime fechaFin);
    void InvalidarCacheEmpleados(string codEmpresa);
}

public class MarcacionesService : IMarcacionesService
{
    private readonly string _baseConnectionString;
    private readonly ILogger<MarcacionesService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMemoryCache _cache;

    private const string Paquete    = "AQUARIUS.PKG_SCA_DEPURA_TAREO";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private static string CacheKeyEmpleados(string emp) => $"marc_empleados_{emp}";

    public MarcacionesService(IConfiguration configuration, ILogger<MarcacionesService> logger, IHttpContextAccessor httpContextAccessor, IMemoryCache cache)
    {
        _baseConnectionString = configuration.GetConnectionString("AquariusConnection")
            ?? throw new InvalidOperationException("Aquarius connection string not found.");
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _cache = cache;
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

    // ── BUSCAR_EMPLEADO

    public async Task<List<EmpleadoDto>> BuscarEmpleadoAsync(string codEmpresa, string? nombre)
    {
        // Si hay filtro por nombre, filtrar desde la caché del listado completo
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
                    result.Add(new EmpleadoDto
                    {
                        Personal  = GetStr((OracleDataReader)reader, "personal"),
                        Fotocheck = GetStr((OracleDataReader)reader, "fotocheck"),
                        Empleado  = GetStr((OracleDataReader)reader, "empleado"),
                        TipEstado = GetStr((OracleDataReader)reader, "tip_estado"),
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

    public async Task<List<MarcacionRangoDto>> ConsultarRangoAsync(string codEmpresa, string codPersonal, DateTime fechaInicio, DateTime fechaFin)
    {
        try
        {
            return await WithOracleRetryAsync(async () =>
            {
                var result = new List<MarcacionRangoDto>();
                await using var conn = new OracleConnection(GetOracleConnectionString());
                await conn.OpenAsync();

                // Iteramos día a día porque CONSULTAR_RANGO recibe un solo empleado
                // pero acepta rango directamente (p_fecha_inicio / p_fecha_fin).
                await using var cmd = conn.CreateCommand();
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = $"{Paquete}.CONSULTAR_RANGO";

                cmd.Parameters.Add(new OracleParameter("p_cod_empresa",  OracleDbType.Varchar2) { Value = codEmpresa });
                cmd.Parameters.Add(new OracleParameter("p_cod_personal", OracleDbType.Varchar2) { Value = codPersonal });
                cmd.Parameters.Add(new OracleParameter("p_fecha_inicio", OracleDbType.Varchar2) { Value = fechaInicio.ToString("dd/MM/yyyy") });
                cmd.Parameters.Add(new OracleParameter("p_fecha_fin",    OracleDbType.Varchar2) { Value = fechaFin.ToString("dd/MM/yyyy") });
                cmd.Parameters.Add(new OracleParameter("cv_resultado",   OracleDbType.RefCursor) { Direction = ParameterDirection.Output });

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var r = (OracleDataReader)reader;
                    result.Add(new MarcacionRangoDto
                    {
                        Fechamar         = GetStr(r, "fechamar"),
                        CodEmpresa       = GetStr(r, "emp"),
                        CodPersonal      = GetStr(r, "personal"),
                        Fotocheck        = GetStr(r, "fotocheck"),
                        HorEntrada       = GetStr(r, "hor_entrada"),
                        HorIniRefri      = GetStr(r, "hor_ini_ref"),
                        HorFinRefri      = GetStr(r, "hor_fin_ref"),
                        HorSalida        = GetStr(r, "hor_salida"),
                        NumMarcaciones   = GetNullInt(r, "n_marcas"),
                        Entrada          = GetStr(r, "entrada"),
                        IniRefri         = GetStr(r, "ini_refri"),
                        FinRefri         = GetStr(r, "fin_refri"),
                        Salida           = GetStr(r, "salida"),
                        CodDepuracion    = GetStr(r, "cod_dep"),
                        DescDepuracion   = GetStr(r, "desc_dep"),
                        Pendiente        = GetStr(r, "pendiente"),
                        CasoAplica       = GetStr(r, "caso_aplica"),
                        Problema         = GetStr(r, "problema"),
                    });
                }
                return result;
            }, "CONSULTAR_RANGO");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en CONSULTAR_RANGO: empresa={Empresa}, personal={Personal}", codEmpresa, codPersonal);
            throw;
        }
    }

    // ── DEPURA_RANGO ───────────────────────────────────────────────────────

    public async Task<DepuraRangoResultadoDto> DepurarRangoAsync(string codEmpresa, string codPersonal, DateTime fechaInicio, DateTime fechaFin)
    {
        try
        {
            return await WithOracleRetryAsync(async () =>
            {
                await using var conn = new OracleConnection(GetOracleConnectionString());
                await conn.OpenAsync();

                await using var cmd = conn.CreateCommand();
                cmd.CommandType    = CommandType.StoredProcedure;
                cmd.CommandText    = $"{Paquete}.DEPURA_RANGO";
                cmd.CommandTimeout = 120;

                cmd.Parameters.Add(new OracleParameter("p_cod_empresa",    OracleDbType.Varchar2) { Value = codEmpresa });
                cmd.Parameters.Add(new OracleParameter("p_cod_personal",   OracleDbType.Varchar2) { Value = codPersonal });
                cmd.Parameters.Add(new OracleParameter("p_fecha_inicio",   OracleDbType.Varchar2) { Value = fechaInicio.ToString("dd/MM/yyyy") });
                cmd.Parameters.Add(new OracleParameter("p_fecha_fin",      OracleDbType.Varchar2) { Value = fechaFin.ToString("dd/MM/yyyy") });
                cmd.Parameters.Add(new OracleParameter("p_solo_obreros",   OracleDbType.Varchar2) { Value = "N" });
                cmd.Parameters.Add(new OracleParameter("cv_resultado",     OracleDbType.RefCursor) { Direction = ParameterDirection.Output });

                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var r = (OracleDataReader)reader;
                    return new DepuraRangoResultadoDto
                    {
                        Resultado         = GetStr(r, "resultado"),
                        FechaInicio       = GetStr(r, "fecha_inicio"),
                        FechaFin          = GetStr(r, "fecha_fin"),
                        TotalDias         = GetInt(r, "total_dias"),
                        DiasOk            = GetInt(r, "dias_ok"),
                        DiasError         = GetInt(r, "dias_error"),
                        TurnosNocturnos   = GetInt(r, "turnos_nocturnos"),
                        Entradas          = GetInt(r, "entradas"),
                        Anticipadas       = GetInt(r, "anticipadas"),
                        Salidas           = GetInt(r, "salidas"),
                        Inirefris         = GetInt(r, "inirefris"),
                        Finrefris         = GetInt(r, "finrefris"),
                        Anomalas          = GetInt(r, "anomalas"),
                        NocturnosSinRefri = GetInt(r, "nocturnos_sin_refri"),
                        Recalculos        = GetInt(r, "recalculos"),
                        TotalGeneradas    = GetInt(r, "total_generadas"),
                        TotalHistorial    = GetInt(r, "total_historial"),
                    };
                }

                return new DepuraRangoResultadoDto { Resultado = "SIN_DATOS" };
            }, "DEPURA_RANGO");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en DEPURA_RANGO: empresa={Empresa}, personal={Personal}", codEmpresa, codPersonal);
            return new DepuraRangoResultadoDto { Resultado = $"ERROR: {ex.Message}" };
        }
    }
}
