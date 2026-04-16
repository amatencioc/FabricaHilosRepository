using FabricaHilos.Models.RecursosHumanos;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace FabricaHilos.Services.RecursosHumanos;

public interface IMarcacionesService
{
    Task<List<EmpleadoDto>> BuscarEmpleadoAsync(string codEmpresa, string? nombre);
    Task<(List<EmpleadoDto> Items, int Total)> ListarEmpleadosAsync(string codEmpresa, string? buscar, int page, int pageSize);
    Task<List<MarcacionRangoDto>> ConsultarRangoAsync(string codEmpresa, string codPersonal, DateTime fechaInicio, DateTime fechaFin);
    Task<DepuraRangoResultadoDto> DepurarRangoAsync(string codEmpresa, string codPersonal, DateTime fechaInicio, DateTime fechaFin);
}

public class MarcacionesService : IMarcacionesService
{
    private readonly string _baseConnectionString;
    private readonly ILogger<MarcacionesService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    private const string Paquete = "AQUARIUS.PKG_SCA_DEPURA_TAREO";

    public MarcacionesService(IConfiguration configuration, ILogger<MarcacionesService> logger, IHttpContextAccessor httpContextAccessor)
    {
        _baseConnectionString = configuration.GetConnectionString("AquariusConnection")
            ?? throw new InvalidOperationException("Aquarius connection string not found.");
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
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

    private static int? GetNullInt(OracleDataReader r, string col)
    {
        try { return r[col] == DBNull.Value ? null : Convert.ToInt32(r[col]); }
        catch { return null; }
    }

    // ── BUSCAR_EMPLEADO ────────────────────────────────────────────────────

    public async Task<List<EmpleadoDto>> BuscarEmpleadoAsync(string codEmpresa, string? nombre)
    {
        var result = new List<EmpleadoDto>();
        try
        {
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"{Paquete}.BUSCAR_EMPLEADO";

            cmd.Parameters.Add(new OracleParameter("p_cod_empresa", OracleDbType.Varchar2) { Value = codEmpresa });
            cmd.Parameters.Add(new OracleParameter("p_nombre", OracleDbType.Varchar2) { Value = (object?)nombre ?? DBNull.Value });
            cmd.Parameters.Add(new OracleParameter("cv_resultado", OracleDbType.RefCursor) { Direction = ParameterDirection.Output });

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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en BUSCAR_EMPLEADO: empresa={Empresa}, nombre={Nombre}", codEmpresa, nombre);
            throw;
        }
        return result;
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
        var result = new List<MarcacionRangoDto>();
        try
        {
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
                    CodEmpresa       = GetStr(r, "cod_empresa"),
                    CodPersonal      = GetStr(r, "cod_personal"),
                    Fotocheck        = GetStr(r, "fotocheck"),
                    NumMarcaciones   = GetNullInt(r, "nummarcaciones"),
                    Entrada          = GetStr(r, "entrada"),
                    IniRefri         = GetStr(r, "inirefri"),
                    FinRefri         = GetStr(r, "finrefri"),
                    Salida           = GetStr(r, "salida"),
                    HorDescripcion   = GetStr(r, "hor_descripcion"),
                    HorClase         = GetStr(r, "hor_clase"),
                    HorEntrada       = GetStr(r, "hor_entrada"),
                    HorIniRefri      = GetStr(r, "hor_inirefri"),
                    HorFinRefri      = GetStr(r, "hor_finrefri"),
                    HorSalida        = GetStr(r, "hor_salida"),
                    HorTotalHrs      = GetStr(r, "hor_total_hrs"),
                    HorDescanso      = GetStr(r, "hor_descanso"),
                    EntradaTeorica   = GetStr(r, "entrada_teorica"),
                    IniRefriTeorico  = GetStr(r, "inirefri_teorico"),
                    FinRefriTeorico  = GetStr(r, "finrefri_teorico"),
                    SalidaTeorica    = GetStr(r, "salida_teorica"),
                    HrsBrutas        = GetStr(r, "hrs_brutas"),
                    HrsRefrigerio    = GetStr(r, "hrs_refrigerio"),
                    HrsEfectivas     = GetStr(r, "hrs_efectivas"),
                    Tardanza         = GetStr(r, "tardanza"),
                    HrsNocturnas     = GetStr(r, "hrs_nocturnas"),
                    HrsNocturnasOf   = GetStr(r, "hrs_nocturnas_of"),
                    TotHoras         = GetStr(r, "tothoras"),
                    HoraDobles       = GetStr(r, "horadobles"),
                    Descanso         = GetStr(r, "descanso"),
                    Cerrado          = GetStr(r, "cerrado"),
                    Obrero           = GetStr(r, "obrero"),
                    TipoEntrada      = GetStr(r, "tipo_entrada"),
                    TipoSalida       = GetStr(r, "tipo_salida"),
                    DescMedico       = GetStr(r, "desc_medico"),
                    Subsidio         = GetStr(r, "subsidio"),
                    PermGoce         = GetStr(r, "perm_goce"),
                    PermSgoce        = GetStr(r, "perm_sgoce"),
                    Vacaciones       = GetStr(r, "vacaciones"),
                    Suspension       = GetStr(r, "suspension"),
                    LicPaternidad    = GetStr(r, "lic_paternidad"),
                    LicFallecimiento = GetStr(r, "lic_fallecimiento"),
                    HoraExtra        = GetStr(r, "hora_extra"),
                    TotalHorasExtras = GetStr(r, "total_horas_extras"),
                    HoraExtraAjus    = GetStr(r, "hora_extra_ajus"),
                    He25Pct          = GetStr(r, "he_25pct"),
                    He35Pct          = GetStr(r, "he_35pct"),
                    He50Pct          = GetStr(r, "he_50pct"),
                    CodDepuracion    = GetStr(r, "cod_depuracion"),
                    DescDepuracion   = GetStr(r, "desc_depuracion"),
                    Alerta01         = GetStr(r, "alerta01"),
                    Alerta06         = GetStr(r, "alerta06"),
                    MarcasHistorial  = GetNullInt(r, "marcas_historial"),
                    Fechamar         = GetStr(r, "fechamar"),
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en CONSULTAR_RANGO: empresa={Empresa}, personal={Personal}", codEmpresa, codPersonal);
            throw;
        }
        return result;
    }

    // ── DEPURA_RANGO ───────────────────────────────────────────────────────

    public async Task<DepuraRangoResultadoDto> DepurarRangoAsync(string codEmpresa, string codPersonal, DateTime fechaInicio, DateTime fechaFin)
    {
        try
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en DEPURA_RANGO: empresa={Empresa}, personal={Personal}", codEmpresa, codPersonal);
            return new DepuraRangoResultadoDto { Resultado = $"ERROR: {ex.Message}" };
        }
    }
}
