using FabricaHilos.Models.RecursosHumanos;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System.Data;

namespace FabricaHilos.Services.RecursosHumanos;

public interface IAuthHorasService
{
    Task<AuthHorasLoginResult>       LoginAsync(string codUsuario);
    Task<List<AuthHorasEmpleadoDto>> ObtenerEmpleadosAsync(string codUsuario, string codEmpresa);
    Task<List<AuthHorasTareoDto>>    ObtenerTareoAsync(string codUsuario, string codEmpresa, string codPersonal, string fechaInicio, string fechaFin);
    Task<AuthHorasGrabarResult>          GrabarAutorizacionAsync(string codUsuario, AuthHorasGrabarRequest req);
    Task<List<AuthHorasSupervisorDto>>   ObtenerSupervisoresAsync(string codUsuario, string codEmpresa);
    Task<List<AuthHorasResumenDto>>      ObtenerResumenHeAsync(string codUsuario, string codEmpresa, string fechaInicio, string fechaFin);
}

public class AuthHorasService : IAuthHorasService
{
    private readonly string _connStr;
    private readonly ILogger<AuthHorasService> _logger;
    private const string Paquete = "AQUARIUS.PKG_AUTH_HE_SUPERVISOR";

    public AuthHorasService(IConfiguration configuration, ILogger<AuthHorasService> logger)
    {
        _connStr = configuration.GetConnectionString("AquariusConnection")
            ?? throw new InvalidOperationException("AquariusConnection no configurada.");
        _logger = logger;
    }

    // =========================================================
    // 1. LOGIN
    // =========================================================
    public async Task<AuthHorasLoginResult> LoginAsync(string codUsuario)
    {
        var result = new AuthHorasLoginResult();
        try
        {
            await using var conn = new OracleConnection(_connStr);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"{Paquete}.sp_login_intranet";
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add("v_cod_usuario", OracleDbType.Varchar2).Value = codUsuario;
            var pCur = cmd.Parameters.Add("cv_1", OracleDbType.RefCursor);
            pCur.Direction = ParameterDirection.Output;

            await cmd.ExecuteNonQueryAsync();
            await using var reader = ((OracleRefCursor)pCur.Value).GetDataReader();
            if (await reader.ReadAsync())
            {
                var res = GetStr(reader, "resultado");
                if (res == "OK")
                {
                    result.Ok              = true;
                    result.CodUsuario      = GetStr(reader, "cod_usuario")      ?? codUsuario;
                    result.NomUsuario      = GetStr(reader, "nom_usuario")      ?? string.Empty;
                    result.CodPersonal     = GetStr(reader, "cod_personal");
                    result.IndAdmin        = GetStr(reader, "ind_admin")        ?? "N";
                    result.CntEmpresas     = GetInt(reader, "cnt_empresas");
                    result.CodEmpresaUnica = GetStr(reader, "cod_empresa_unica");
                    result.EsAdmAlguna     = GetStr(reader, "es_adm_alguna")   ?? "N";
                }
                else
                {
                    result.Ok      = false;
                    result.Mensaje = GetStr(reader, "mensaje") ?? res ?? "Error";
                }
            }
            else
            {
                result.Ok      = false;
                result.Mensaje = "CREDENCIAL_INVALIDA";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AuthHoras.LoginAsync error");
            result.Ok      = false;
            result.Mensaje = "Error de conexión. Intente más tarde.";
        }
        return result;
    }

    // =========================================================
    // 2. EMPLEADOS A CARGO
    // =========================================================
    public async Task<List<AuthHorasEmpleadoDto>> ObtenerEmpleadosAsync(string codUsuario, string codEmpresa)
    {
        var lista = new List<AuthHorasEmpleadoDto>();
        try
        {
            await using var conn = new OracleConnection(_connStr);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"{Paquete}.sp_read_empleados";
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add("v_cod_usuario", OracleDbType.Varchar2).Value = codUsuario;
            cmd.Parameters.Add("v_cod_empresa", OracleDbType.Varchar2).Value = codEmpresa;
            var pCur = cmd.Parameters.Add("cv_1", OracleDbType.RefCursor);
            pCur.Direction = ParameterDirection.Output;

            await cmd.ExecuteNonQueryAsync();
            await using var reader = ((OracleRefCursor)pCur.Value).GetDataReader();
            while (await reader.ReadAsync())
            {
                lista.Add(new AuthHorasEmpleadoDto
                {
                    CodPersonal     = GetStr(reader, "cod_personal")      ?? string.Empty,
                    NombreCompleto  = GetStr(reader, "nombre_completo")   ?? string.Empty,
                    NumFotocheck    = GetStr(reader, "num_fotocheck"),
                    CodEmpresa      = GetStr(reader, "cod_empresa")       ?? string.Empty,
                    CodSucursal     = GetStr(reader, "cod_sucursal")      ?? string.Empty,
                    CodCCostos      = GetStr(reader, "cod_c_costos")      ?? string.Empty,
                    DesCCostos      = GetStr(reader, "des_c_costos")      ?? string.Empty,
                    CodTipoPlanilla = GetStr(reader, "cod_tipo_planilla") ?? string.Empty,
                    DesTipoPlanilla = GetStr(reader, "des_tipo_planilla") ?? string.Empty,
                    TipEstado       = GetStr(reader, "tip_estado")        ?? string.Empty,
                    HorId           = GetStr(reader, "horid"),
                    HorDes          = GetStr(reader, "hordes"),
                    HorCla          = GetStr(reader, "horcla"),
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AuthHoras.ObtenerEmpleadosAsync error");
        }
        return lista;
    }

    // =========================================================
    // 3. TAREO DIARIO
    // =========================================================
    public async Task<List<AuthHorasTareoDto>> ObtenerTareoAsync(
        string codUsuario, string codEmpresa, string codPersonal,
        string fechaInicio, string fechaFin)
    {
        var lista = new List<AuthHorasTareoDto>();
        try
        {
            await using var conn = new OracleConnection(_connStr);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"{Paquete}.sp_read_tareo_he";
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add("v_cod_usuario",  OracleDbType.Varchar2).Value = codUsuario;
            cmd.Parameters.Add("v_cod_empresa",  OracleDbType.Varchar2).Value = codEmpresa;
            cmd.Parameters.Add("v_cod_personal", OracleDbType.Varchar2).Value = codPersonal;
            cmd.Parameters.Add("v_fecha_inicio", OracleDbType.Varchar2).Value = fechaInicio;
            cmd.Parameters.Add("v_fecha_final",  OracleDbType.Varchar2).Value = fechaFin;
            var pCur = cmd.Parameters.Add("cv_1", OracleDbType.RefCursor);
            pCur.Direction = ParameterDirection.Output;

            await cmd.ExecuteNonQueryAsync();
            await using var reader = ((OracleRefCursor)pCur.Value).GetDataReader();
            while (await reader.ReadAsync())
            {
                lista.Add(new AuthHorasTareoDto
                {
                    FechaMarStr     = GetDateStr(reader, "fechamar"),
                    DiaSemana       = GetStr(reader, "dia_semana")       ?? string.Empty,
                    Descanso        = GetStr(reader, "descanso")         ?? "N",
                    Feriado         = GetStr(reader, "feriado")          ?? "N",
                    Entrada         = GetTimeStr(reader, "entrada"),
                    Salida          = GetTimeStr(reader, "salida"),
                    HoraEfectiva    = GetTimeStr(reader, "horaefectiva"),
                    Alerta01        = GetStr(reader, "alerta01"),

                    HoraExtAntesOfi = GetTimeStr(reader, "horaextantesofi"),
                    HayHeaPorAut    = GetStr(reader, "hayhea_poraut")    ?? "N",
                    AuthHeaHoras    = GetStr(reader, "auth_hea_horas"),
                    AuthHeaObs      = GetStr(reader, "auth_hea_obs"),
                    AuthHeaUsr      = GetStr(reader, "auth_hea_usr"),
                    DesAuthHeaHoras = GetStr(reader, "desauth_hea_horas"),

                    HoraExtraOfi    = GetTimeStr(reader, "horaextraofi"),
                    HayHedPorAut    = GetStr(reader, "hayhed_poraut")    ?? "N",
                    AuthHedHoras    = GetStr(reader, "auth_hed_horas"),
                    AuthHedObs      = GetStr(reader, "auth_hed_obs"),
                    AuthHedUsr      = GetStr(reader, "auth_hed_usr"),
                    DesAuthHedHoras = GetStr(reader, "desauth_hed_horas"),

                    HoraDoblesOf    = GetTimeStr(reader, "horadoblesof"),
                    HayHeoPorAut    = GetStr(reader, "hayheo_poraut")    ?? "N",
                    AuthHeoHoras    = GetStr(reader, "auth_heo_horas"),
                    AuthHeoObs      = GetStr(reader, "auth_heo_obs"),
                    AuthHeoUsr      = GetStr(reader, "auth_heo_usr"),
                    DesAuthHeoHoras = GetStr(reader, "desauth_heo_horas"),
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AuthHoras.ObtenerTareoAsync error");
        }
        return lista;
    }

    // =========================================================
    // 4. GRABAR AUTORIZACIÓN
    // =========================================================
    public async Task<AuthHorasGrabarResult> GrabarAutorizacionAsync(string codUsuario, AuthHorasGrabarRequest req)
    {
        var result = new AuthHorasGrabarResult();
        try
        {
            await using var conn = new OracleConnection(_connStr);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"{Paquete}.sp_grabar_autorizacion";
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add("v_cod_usuario",   OracleDbType.Varchar2).Value = codUsuario;
            cmd.Parameters.Add("v_cod_empresa",   OracleDbType.Varchar2).Value = req.CodEmpresa;
            cmd.Parameters.Add("v_cod_personal",  OracleDbType.Varchar2).Value = req.CodPersonal;
            cmd.Parameters.Add("v_fecha",         OracleDbType.Varchar2).Value = req.Fecha;
            cmd.Parameters.Add("v_tipo",          OracleDbType.Varchar2).Value = req.Tipo;
            cmd.Parameters.Add("v_valor",         OracleDbType.Varchar2).Value = req.Valor;
            cmd.Parameters.Add("v_observaciones", OracleDbType.Varchar2).Value =
                string.IsNullOrEmpty(req.Observaciones) ? DBNull.Value : (object)req.Observaciones;
            var pCur = cmd.Parameters.Add("cv_1", OracleDbType.RefCursor);
            pCur.Direction = ParameterDirection.Output;

            await cmd.ExecuteNonQueryAsync();
            await using var reader = ((OracleRefCursor)pCur.Value).GetDataReader();
            if (await reader.ReadAsync())
            {
                result.Ok      = GetStr(reader, "resultado") == "OK";
                result.Mensaje = GetStr(reader, "mensaje")   ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AuthHoras.GrabarAutorizacionAsync error");
            result.Ok      = false;
            result.Mensaje = "Error al grabar la autorización.";
        }
        return result;
    }

    // =========================================================
    // 5. SUPERVISORES (solo para administradores)
    // =========================================================
    public async Task<List<AuthHorasSupervisorDto>> ObtenerSupervisoresAsync(string codUsuario, string codEmpresa)
    {
        var lista = new List<AuthHorasSupervisorDto>();
        try
        {
            await using var conn = new OracleConnection(_connStr);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"{Paquete}.sp_read_supervisores";
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add("v_cod_usuario", OracleDbType.Varchar2).Value = codUsuario;
            cmd.Parameters.Add("v_cod_empresa", OracleDbType.Varchar2).Value = codEmpresa;
            var pCur = cmd.Parameters.Add("cv_1", OracleDbType.RefCursor);
            pCur.Direction = ParameterDirection.Output;

            await cmd.ExecuteNonQueryAsync();
            await using var reader = ((OracleRefCursor)pCur.Value).GetDataReader();
            while (await reader.ReadAsync())
            {
                lista.Add(new AuthHorasSupervisorDto
                {
                    CodUsuario = GetStr(reader, "cod_usuario") ?? string.Empty,
                    NomUsuario = GetStr(reader, "nom_usuario") ?? string.Empty,
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AuthHoras.ObtenerSupervisoresAsync error");
        }
        return lista;
    }

    // =========================================================
    // 6. RESUMEN HE POR EMPLEADO
    // =========================================================
    public async Task<List<AuthHorasResumenDto>> ObtenerResumenHeAsync(
        string codUsuario, string codEmpresa, string fechaInicio, string fechaFin)
    {
        var lista = new List<AuthHorasResumenDto>();
        try
        {
            await using var conn = new OracleConnection(_connStr);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"{Paquete}.sp_read_resumen_he";
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add("v_cod_usuario",  OracleDbType.Varchar2).Value = codUsuario;
            cmd.Parameters.Add("v_cod_empresa",  OracleDbType.Varchar2).Value = codEmpresa;
            cmd.Parameters.Add("v_fecha_inicio", OracleDbType.Varchar2).Value = fechaInicio;
            cmd.Parameters.Add("v_fecha_final",  OracleDbType.Varchar2).Value = fechaFin;
            var pCur = cmd.Parameters.Add("cv_1", OracleDbType.RefCursor);
            pCur.Direction = ParameterDirection.Output;

            await cmd.ExecuteNonQueryAsync();
            await using var reader = ((OracleRefCursor)pCur.Value).GetDataReader();
            while (await reader.ReadAsync())
            {
                lista.Add(new AuthHorasResumenDto
                {
                    CodPersonal     = GetStr(reader, "cod_personal")    ?? string.Empty,
                    NombreCompleto  = GetStr(reader, "nombre_completo") ?? string.Empty,
                    NumFotocheck    = GetStr(reader, "num_fotocheck"),
                    CodCCostos      = GetStr(reader, "cod_c_costos")    ?? string.Empty,
                    DesCCostos      = GetStr(reader, "des_c_costos")    ?? string.Empty,
                    DiasConHe       = GetInt(reader, "dias_con_he"),
                    DiasPendientes  = GetInt(reader, "dias_pendientes"),
                    DiasAutorizados = GetInt(reader, "dias_autorizados"),
                    MinHed          = GetInt(reader, "min_hed"),
                    MinHea          = GetInt(reader, "min_hea"),
                    MinHeo          = GetInt(reader, "min_heo"),
                    MinHedAut       = GetInt(reader, "min_hed_aut"),
                    MinHeaAut       = GetInt(reader, "min_hea_aut"),
                    MinHeoAut       = GetInt(reader, "min_heo_aut"),
                    Estado          = GetStr(reader, "estado")          ?? string.Empty,
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AuthHoras.ObtenerResumenHeAsync error");
        }
        return lista;
    }

    // ── helpers ──────────────────────────────────────────────────────────────
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
    private static string GetDateStr(OracleDataReader r, string col)
    {
        try
        {
            if (r[col] == DBNull.Value) return string.Empty;
            return Convert.ToDateTime(r[col]).ToString("dd/MM/yyyy");
        }
        catch { return string.Empty; }
    }
    private static string? GetTimeStr(OracleDataReader r, string col)
    {
        try
        {
            if (r[col] == DBNull.Value) return null;
            return Convert.ToDateTime(r[col]).ToString("HH:mm");
        }
        catch { return null; }
    }
}
