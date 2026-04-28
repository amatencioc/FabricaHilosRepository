using FabricaHilos.Models.RecursosHumanos;
using Microsoft.Extensions.Caching.Memory;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace FabricaHilos.Services.RecursosHumanos;

public interface ICompensacionDiaDiaService
{
    Task<List<CompensacionPreviewDto>> CalcularHorasEventoAsync(
        string codEmpresa,
        string fechaOrigen,
        string? fechaDestino,
        string tipoOrigen,
        string? listaPersonal);

    Task<List<CompensacionMasivoResultDto>> RegistrarEventoMasivoAsync(
        string codEmpresa,
        string fechaOrigen,
        string fechaDestino,
        string tipoOrigen,
        string tipoCompensacion,
        string listaPersonal,
        string? horasMax);

    Task<CompensacionEstadoDto?> VerEstadoAsync(long idCompen);

    Task<List<CompensacionRangoDto>> ConsultarRangoAsync(
        string? codEmpresa,
        string? codPersonal,
        string fechaInicio,
        string fechaFin);

    Task<(List<EmpleadoRangoDto> Items, int Total)> ListarEmpleadosRangoAsync(
        string codEmpresa,
        string fechaInicio,
        string fechaFin,
        string? codPersonal,
        string? nombre,
        int pagina,
        int tamPagina);

    /// <summary>Confirma la última transacción de registro masivo (COMMIT).</summary>
    Task CommitAsync();

    /// <summary>Revierte la última transacción de registro masivo (ROLLBACK).</summary>
    Task RollbackAsync();
}

public class CompensacionDiaDiaService : ICompensacionDiaDiaService, IAsyncDisposable
{
    private const string Paquete = "AQUARIUS.PKG_SCA_COMP_DIA_DIA";

    private readonly string _baseConnectionString;
    private readonly ILogger<CompensacionDiaDiaService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMemoryCache _cache;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(3);
    private static string CacheKeyRango(string emp, string ini, string fin) =>
        $"comp_rango_{emp}_{ini}_{fin}";

    // Conexión abierta mantenida entre RegistrarEventoMasivo y Commit/Rollback
    private OracleConnection?    _txConn;
    private OracleTransaction?   _txn;

    public CompensacionDiaDiaService(
        IConfiguration configuration,
        ILogger<CompensacionDiaDiaService> logger,
        IHttpContextAccessor httpContextAccessor,
        IMemoryCache cache)
    {
        _baseConnectionString = configuration.GetConnectionString("AquariusConnection")
            ?? throw new InvalidOperationException("Aquarius connection string not found.");
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _cache = cache;
    }

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

    private static long? GetNullLong(OracleDataReader r, string col)
    {
        try { return r[col] == DBNull.Value ? null : Convert.ToInt64(r[col]); }
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
            _logger.LogWarning("ORA-04068 en {Contexto}, reintentando...", contexto);
            return await operation();
        }
    }

    // ── CALCULAR_HORAS_EVENTO ─────────────────────────────────────────────────

    public async Task<List<CompensacionPreviewDto>> CalcularHorasEventoAsync(
        string codEmpresa,
        string fechaOrigen,
        string? fechaDestino,
        string tipoOrigen,
        string? listaPersonal)
    {
        return await WithOracleRetryAsync(async () =>
        {
            var result = new List<CompensacionPreviewDto>();
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"{Paquete}.CALCULAR_HORAS_EVENTO";

            cmd.Parameters.Add(new OracleParameter("p_cod_empresa",    OracleDbType.Varchar2) { Value = codEmpresa });
            cmd.Parameters.Add(new OracleParameter("p_fecha_origen",   OracleDbType.Varchar2) { Value = fechaOrigen });
            cmd.Parameters.Add(new OracleParameter("p_fecha_destino",  OracleDbType.Varchar2) { Value = (object?)fechaDestino ?? DBNull.Value });
            cmd.Parameters.Add(new OracleParameter("p_tipo_origen",    OracleDbType.Char)     { Value = tipoOrigen });
            cmd.Parameters.Add(new OracleParameter("p_lista_personal", OracleDbType.Varchar2) { Value = (object?)listaPersonal ?? DBNull.Value });
            cmd.Parameters.Add(new OracleParameter("cv_resultado",     OracleDbType.RefCursor){ Direction = ParameterDirection.Output });

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new CompensacionPreviewDto
                {
                    CodPersonal         = GetStr((OracleDataReader)reader, "cod_personal"),
                    NombreCompleto      = GetStr((OracleDataReader)reader, "nombre_completo"),
                    MinDisponibles      = GetInt((OracleDataReader)reader, "min_disponibles"),
                    HorasDisponibles    = GetStr((OracleDataReader)reader, "horas_disponibles"),
                    MinJornadaDestino   = GetInt((OracleDataReader)reader, "min_jornada_destino"),
                    HorasJornadaDestino = GetStr((OracleDataReader)reader, "horas_jornada_destino"),
                    MinACompensar       = GetInt((OracleDataReader)reader, "min_a_compensar"),
                    HorasACompensar     = GetStr((OracleDataReader)reader, "horas_a_compensar"),
                    MinSobrante         = GetInt((OracleDataReader)reader, "min_sobrante"),
                    HorasSobrante       = GetStr((OracleDataReader)reader, "horas_sobrante"),
                });
            }
            return result;
        }, "CALCULAR_HORAS_EVENTO");
    }

    // ── REGISTRAR_EVENTO_MASIVO ───────────────────────────────────────────────
    // Mantiene la conexión abierta con transacción hasta que se llame CommitAsync / RollbackAsync.

    public async Task<List<CompensacionMasivoResultDto>> RegistrarEventoMasivoAsync(
        string codEmpresa,
        string fechaOrigen,
        string fechaDestino,
        string tipoOrigen,
        string tipoCompensacion,
        string listaPersonal,
        string? horasMax)
    {
        // Liberar transacción anterior si quedó abierta
        await DisposeTransactionAsync();

        _txConn = new OracleConnection(GetOracleConnectionString());
        await _txConn.OpenAsync();
        _txn = _txConn.BeginTransaction();

        try
        {
            var result = new List<CompensacionMasivoResultDto>();

            await using var cmd = _txConn.CreateCommand();
            cmd.Transaction = _txn;
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"{Paquete}.REGISTRAR_EVENTO_MASIVO";

            cmd.Parameters.Add(new OracleParameter("p_cod_empresa",        OracleDbType.Varchar2) { Value = codEmpresa });
            cmd.Parameters.Add(new OracleParameter("p_fecha_origen",       OracleDbType.Varchar2) { Value = fechaOrigen });
            cmd.Parameters.Add(new OracleParameter("p_fecha_destino",      OracleDbType.Varchar2) { Value = fechaDestino });
            cmd.Parameters.Add(new OracleParameter("p_tipo_origen",        OracleDbType.Char)     { Value = tipoOrigen });
            cmd.Parameters.Add(new OracleParameter("p_tipo_compensacion",  OracleDbType.Char)     { Value = tipoCompensacion });
            cmd.Parameters.Add(new OracleParameter("p_lista_personal",     OracleDbType.Varchar2) { Value = listaPersonal });
            cmd.Parameters.Add(new OracleParameter("p_horas_max",          OracleDbType.Varchar2) { Value = (object?)horasMax ?? DBNull.Value });
            cmd.Parameters.Add(new OracleParameter("cv_resultado",         OracleDbType.RefCursor){ Direction = ParameterDirection.Output });

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new CompensacionMasivoResultDto
                {
                    CodPersonal         = GetStr((OracleDataReader)reader, "cod_personal"),
                    NombreCompleto      = GetStr((OracleDataReader)reader, "nombre_completo"),
                    MinDisponibles      = GetInt((OracleDataReader)reader, "min_disponibles"),
                    HorasDisponibles    = GetStr((OracleDataReader)reader, "horas_disponibles"),
                    MinJornadaDestino   = GetInt((OracleDataReader)reader, "min_jornada_destino"),
                    HorasJornadaDestino = GetStr((OracleDataReader)reader, "horas_jornada_destino"),
                    MinACompensar       = GetInt((OracleDataReader)reader, "min_a_compensar"),
                    HorasACompensar     = GetStr((OracleDataReader)reader, "horas_a_compensar"),
                    MinSobrante         = GetInt((OracleDataReader)reader, "min_sobrante"),
                    HorasSobrante       = GetStr((OracleDataReader)reader, "horas_sobrante"),
                    IdCompen            = GetNullLong((OracleDataReader)reader, "id_compen"),
                    Estado              = GetStr((OracleDataReader)reader, "estado"),
                    Motivo              = GetStr((OracleDataReader)reader, "motivo"),
                    SaldoBancoSemMin    = GetInt((OracleDataReader)reader, "saldo_banco_sem_min"),
                    IdEvento            = GetNullLong((OracleDataReader)reader, "id_evento") ?? 0,
                });
            }
            return result;
        }
        catch
        {
            await DisposeTransactionAsync();
            throw;
        }
    }

    // ── COMMIT ────────────────────────────────────────────────────────────────

    public async Task CommitAsync()
    {
        if (_txn != null)
        {
            await _txn.CommitAsync();
            await DisposeTransactionAsync();
        }
    }

    // ── ROLLBACK ──────────────────────────────────────────────────────────────

    public async Task RollbackAsync()
    {
        if (_txn != null)
        {
            await _txn.RollbackAsync();
            await DisposeTransactionAsync();
        }
    }

    private async Task DisposeTransactionAsync()
    {
        if (_txn    != null) { await _txn.DisposeAsync();    _txn    = null; }
        if (_txConn != null) { await _txConn.DisposeAsync(); _txConn = null; }
    }

    public async Task<(List<EmpleadoRangoDto> Items, int Total)> ListarEmpleadosRangoAsync(
        string codEmpresa,
        string fechaInicio,
        string fechaFin,
        string? codPersonal,
        string? nombre,
        int pagina,
        int tamPagina)
    {
        // Clave de caché por empresa+rango
        var cacheKey = CacheKeyRango(codEmpresa, fechaInicio, fechaFin);
        if (!_cache.TryGetValue(cacheKey, out List<EmpleadoRangoDto>? todos) || todos == null)
        {
            todos = await CargarEmpleadosRangoOracleAsync(codEmpresa, fechaInicio, fechaFin);
            _cache.Set(cacheKey, todos, CacheDuration);
        }

        IEnumerable<EmpleadoRangoDto> filtrados = todos;

        if (!string.IsNullOrWhiteSpace(codPersonal))
            filtrados = filtrados.Where(e =>
                string.Equals(e.CodPersonal, codPersonal.Trim(), StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(nombre))
        {
            var q = nombre.Trim().ToUpperInvariant();
            filtrados = filtrados.Where(e =>
                (e.NombreCompleto ?? string.Empty).ToUpperInvariant().Contains(q) ||
                (e.CodPersonal   ?? string.Empty).ToUpperInvariant().Contains(q));
        }

        var lista = filtrados.ToList();
        var total = lista.Count;
        var items = lista.Skip((pagina - 1) * tamPagina).Take(tamPagina).ToList();
        return (items, total);
    }

    private async Task<List<EmpleadoRangoDto>> CargarEmpleadosRangoOracleAsync(
        string codEmpresa, string fechaInicio, string fechaFin)
    {
        return await WithOracleRetryAsync(async () =>
        {
            var result = new List<EmpleadoRangoDto>();
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"{Paquete}.LISTAR_EMPLEADOS_RANGO";

            cmd.Parameters.Add(new OracleParameter("p_cod_empresa",  OracleDbType.Varchar2) { Value = codEmpresa });
            cmd.Parameters.Add(new OracleParameter("p_fecha_inicio", OracleDbType.Varchar2) { Value = fechaInicio });
            cmd.Parameters.Add(new OracleParameter("p_fecha_fin",    OracleDbType.Varchar2) { Value = fechaFin });
            cmd.Parameters.Add(new OracleParameter("p_nombre",       OracleDbType.Varchar2) { Value = DBNull.Value });
            cmd.Parameters.Add(new OracleParameter("cv_resultado",   OracleDbType.RefCursor){ Direction = ParameterDirection.Output });

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var r = (OracleDataReader)reader;
                result.Add(new EmpleadoRangoDto
                {
                    CodPersonal     = GetStr(r, "cod_personal"),
                    NombreCompleto  = GetStr(r, "nombre_completo"),
                    FechamarStr     = GetStr(r, "fechamar_str"),
                    MinTrabajadas   = GetInt(r, "min_trabajadas"),
                    HorasTrabajadas = GetStr(r, "horas_trabajadas"),
                    MinHe           = GetInt(r, "min_he"),
                    HorasHe        = GetStr(r, "horas_he"),
                    MinDobles      = GetInt(r, "min_dobles"),
                    HorasDobles    = GetStr(r, "horas_dobles"),
                    MinBanco       = GetInt(r, "min_banco"),
                    HorasBanco     = GetStr(r, "horas_banco"),
                    MinTotal       = GetInt(r, "min_total"),
                    HorasTotal     = GetStr(r, "horas_total"),
                });
            }
            return result;
        }, "LISTAR_EMPLEADOS_RANGO");
    }

    public async ValueTask DisposeAsync() => await DisposeTransactionAsync();

    // ── VER_ESTADO ────────────────────────────────────────────────────────────

    public async Task<CompensacionEstadoDto?> VerEstadoAsync(long idCompen)
    {
        return await WithOracleRetryAsync(async () =>
        {
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"{Paquete}.VER_ESTADO";

            cmd.Parameters.Add(new OracleParameter("p_id_compen",  OracleDbType.Decimal)   { Value = idCompen });
            cmd.Parameters.Add(new OracleParameter("cv_resultado", OracleDbType.RefCursor) { Direction = ParameterDirection.Output });

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var r = (OracleDataReader)reader;
                return new CompensacionEstadoDto
                {
                    IdCompen         = GetNullLong(r, "id_compen"),
                    CodEmpresa       = GetStr(r, "cod_empresa"),
                    CodPersonal      = GetStr(r, "cod_personal"),
                    NomTrabajador    = GetStr(r, "nom_trabajador"),
                    ApePaterno       = GetStr(r, "ape_paterno"),
                    ApeMaterno       = GetStr(r, "ape_materno"),
                    FechaOrigen      = GetStr(r, "fechaorigen"),
                    FechaDestino     = GetStr(r, "fechadestino"),
                    TipoOrigen       = GetStr(r, "tipoorigen"),
                    TipoCompensacion = GetStr(r, "tipocompensacion"),
                    TiempoMin        = GetInt(r, "tiempo_min"),
                    TiempoHhMi       = GetStr(r, "tiempo_hhmi"),
                    Periodo          = GetStr(r, "periodo"),
                    OriHeAjus        = GetStr(r, "ori_he_ajus"),
                    OriDobles        = GetStr(r, "ori_dobles"),
                    OriBanco         = GetStr(r, "ori_banco"),
                    OriAlerta06      = GetStr(r, "ori_alerta06"),
                    OriAlerta08      = GetStr(r, "ori_alerta08"),
                    DesTardanza      = GetStr(r, "des_tardanza"),
                    DesAnteSalida    = GetStr(r, "des_antesalida"),
                    DesNoTrab        = GetStr(r, "des_no_trab"),
                    DesFalta         = GetStr(r, "des_falta"),
                    DesPermiso       = GetStr(r, "des_permiso"),
                    DesAlerta02      = GetStr(r, "des_alerta02"),
                    DesAlerta03      = GetStr(r, "des_alerta03"),
                    DesAlerta04      = GetStr(r, "des_alerta04"),
                    DesAlerta07      = GetStr(r, "des_alerta07"),
                    DesAlerta09      = GetStr(r, "des_alerta09"),
                };
            }
            return null;
        }, "VER_ESTADO");
    }

    // ── CONSULTAR_RANGO ───────────────────────────────────────────────────────

    public async Task<List<CompensacionRangoDto>> ConsultarRangoAsync(
        string? codEmpresa,
        string? codPersonal,
        string fechaInicio,
        string fechaFin)
    {
        return await WithOracleRetryAsync(async () =>
        {
            var result = new List<CompensacionRangoDto>();
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"{Paquete}.CONSULTAR_RANGO";

            cmd.Parameters.Add(new OracleParameter("p_cod_empresa",  OracleDbType.Varchar2) { Value = (object?)codEmpresa  ?? DBNull.Value });
            cmd.Parameters.Add(new OracleParameter("p_cod_personal", OracleDbType.Varchar2) { Value = (object?)codPersonal ?? DBNull.Value });
            cmd.Parameters.Add(new OracleParameter("p_fecha_inicio", OracleDbType.Varchar2) { Value = fechaInicio });
            cmd.Parameters.Add(new OracleParameter("p_fecha_fin",    OracleDbType.Varchar2) { Value = fechaFin });
            cmd.Parameters.Add(new OracleParameter("cv_resultado",   OracleDbType.RefCursor){ Direction = ParameterDirection.Output });

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var r = (OracleDataReader)reader;
                result.Add(new CompensacionRangoDto
                {
                    IdCompen         = GetNullLong(r, "id_compen"),
                    CodEmpresa       = GetStr(r, "cod_empresa"),
                    CodPersonal      = GetStr(r, "cod_personal"),
                    FechaOrigen      = GetStr(r, "fechaorigen"),
                    FechaDestino     = GetStr(r, "fechadestino"),
                    TipoOrigen       = GetStr(r, "tipoorigen"),
                    TipoCompensacion = GetStr(r, "tipocompensacion"),
                    TiempoMin        = GetInt(r, "tiempo_min"),
                    TiempoHhMi       = GetStr(r, "tiempo_hhmi"),
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
}
