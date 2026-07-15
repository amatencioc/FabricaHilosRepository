using FabricaHilos.Models.RecursosHumanos;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System.Data;

namespace FabricaHilos.Services.RecursosHumanos;

public interface IPlanillaMensualService
{
    Task<List<PlanillaEmpresaDto>>      ObtenerEmpresasAsync();
    Task<List<PlanillaSucursalDto>>     ObtenerSucursalesAsync(string codEmpresa);
    Task<List<PlanillaTipoPlanillaDto>> ObtenerTiposPlanillaAsync(string codEmpresa);
    Task<List<PlanillaCCostosDto>>      ObtenerCCostosAsync(string codEmpresa);
    Task<List<PlanillaPeriodoDto>>      ObtenerPeriodosAsync(string fechaInicio, string fechaFinal);
    Task<List<PlanillaResumenDto>>      ObtenerResumenAsync(PlanillaMensualFiltroDto filtro);
    Task<List<PlanillaDetalleFilaDto>>  ObtenerDetalleAsync(PlanillaMensualFiltroDto filtro);
}

public class PlanillaMensualService : IPlanillaMensualService
{
    private readonly string _connStr;
    private readonly ILogger<PlanillaMensualService> _logger;

    public PlanillaMensualService(IConfiguration configuration, ILogger<PlanillaMensualService> logger)
    {
        _connStr = configuration.GetConnectionString("AquariusConnection")
            ?? throw new InvalidOperationException("AquariusConnection no configurada.");
        _logger = logger;
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static string? Str(OracleDataReader r, string col)
    {
        try { return r[col] == DBNull.Value ? null : r[col]?.ToString()?.Trim(); }
        catch { return null; }
    }

    private static int Int(OracleDataReader r, string col)
    {
        try { return r[col] == DBNull.Value ? 0 : Convert.ToInt32(r[col]); }
        catch { return 0; }
    }

    // ── MAESTROS ──────────────────────────────────────────────────────────────

    public async Task<List<PlanillaEmpresaDto>> ObtenerEmpresasAsync()
    {
        var lista = new List<PlanillaEmpresaDto>();
        try
        {
            await using var conn = new OracleConnection(_connStr);
            await conn.OpenAsync();
            await using var cmd  = conn.CreateCommand();
            cmd.CommandText =
                @"SELECT DISTINCT p.cod_empresa,
                         NVL((SELECT des_razon_social FROM MAE_EMPRESAS WHERE cod_empresa = p.cod_empresa), p.cod_empresa) des_empresa
                  FROM PLA_PERSONAL p
                  ORDER BY cod_empresa";
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                lista.Add(new PlanillaEmpresaDto { CodEmpresa = Str(r,"cod_empresa") ?? "", DesEmpresa = Str(r,"des_empresa") ?? "" });
        }
        catch (Exception ex) { _logger.LogError(ex, "ObtenerEmpresasAsync"); }
        return lista;
    }

    public async Task<List<PlanillaSucursalDto>> ObtenerSucursalesAsync(string codEmpresa)
    {
        var lista = new List<PlanillaSucursalDto>();
        try
        {
            await using var conn = new OracleConnection(_connStr);
            await conn.OpenAsync();
            await using var cmd  = conn.CreateCommand();
            cmd.CommandText =
                @"SELECT DISTINCT p.cod_sucursal,
                         NVL(s.des_sucursal, p.cod_sucursal) des_sucursal
                  FROM PLA_PERSONAL p
                  LEFT JOIN MAE_SUCURSAL s ON s.cod_empresa = p.cod_empresa AND s.cod_sucursal = p.cod_sucursal
                  WHERE p.cod_empresa = :emp
                  ORDER BY p.cod_sucursal";
            cmd.Parameters.Add("emp", OracleDbType.Varchar2).Value = codEmpresa;
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                lista.Add(new PlanillaSucursalDto { CodSucursal = Str(r,"cod_sucursal") ?? "", DesSucursal = Str(r,"des_sucursal") ?? "" });
        }
        catch (Exception ex) { _logger.LogError(ex, "ObtenerSucursalesAsync"); }
        return lista;
    }

    public async Task<List<PlanillaTipoPlanillaDto>> ObtenerTiposPlanillaAsync(string codEmpresa)
    {
        var lista = new List<PlanillaTipoPlanillaDto>();
        try
        {
            await using var conn = new OracleConnection(_connStr);
            await conn.OpenAsync();
            await using var cmd  = conn.CreateCommand();
            cmd.CommandText =
                @"SELECT cod_tipo_planilla, des_tipo_planilla
                  FROM PLA_TIPO_PLANILLA
                  WHERE cod_empresa = :emp AND ind_asistencia = 'S'
                  ORDER BY cod_tipo_planilla";
            cmd.Parameters.Add("emp", OracleDbType.Varchar2).Value = codEmpresa;
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                lista.Add(new PlanillaTipoPlanillaDto { CodTipoPlanilla = Str(r,"cod_tipo_planilla") ?? "", DesTipoPlanilla = Str(r,"des_tipo_planilla") ?? "" });
        }
        catch (Exception ex) { _logger.LogError(ex, "ObtenerTiposPlanillaAsync"); }
        return lista;
    }

    public async Task<List<PlanillaCCostosDto>> ObtenerCCostosAsync(string codEmpresa)
    {
        var lista = new List<PlanillaCCostosDto>();
        try
        {
            await using var conn = new OracleConnection(_connStr);
            await conn.OpenAsync();
            await using var cmd  = conn.CreateCommand();
            cmd.CommandText =
                @"SELECT DISTINCT p.cod_c_costos,
                         NVL(c.des_c_costos, p.cod_c_costos) des_c_costos
                  FROM PLA_PERSONAL p
                  JOIN MAE_C_COSTOS c ON c.cod_empresa = p.cod_empresa
                      AND c.num_ver_c_costos = p.num_ver_c_costos
                      AND c.cod_c_costos     = p.cod_c_costos
                  WHERE p.cod_empresa = :emp
                  ORDER BY p.cod_c_costos";
            cmd.Parameters.Add("emp", OracleDbType.Varchar2).Value = codEmpresa;
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                lista.Add(new PlanillaCCostosDto { CodCCostos = Str(r,"cod_c_costos") ?? "", DesCCostos = Str(r,"des_c_costos") ?? "" });
        }
        catch (Exception ex) { _logger.LogError(ex, "ObtenerCCostosAsync"); }
        return lista;
    }

    public async Task<List<PlanillaPeriodoDto>> ObtenerPeriodosAsync(string fechaInicio, string fechaFinal)
    {
        var lista = new List<PlanillaPeriodoDto>();
        try
        {
            await using var conn = new OracleConnection(_connStr);
            await conn.OpenAsync();
            await using var cmd  = conn.CreateCommand();
            cmd.CommandText =
                @"SELECT sem_proceso,
                         to_char(fecini,'DD/MM/YYYY') fecini,
                         to_char(fecfin,'DD/MM/YYYY') fecfin
                  FROM SCA_PERIODOS
                  WHERE fecini >= to_date(:fi,'DD/MM/YYYY') - 7
                    AND fecfin <= to_date(:ff,'DD/MM/YYYY') + 7
                    AND fecini >= to_date(:fi,'DD/MM/YYYY') - 14
                  ORDER BY fecini";
            cmd.Parameters.Add("fi", OracleDbType.Varchar2).Value = fechaInicio;
            cmd.Parameters.Add("ff", OracleDbType.Varchar2).Value = fechaFinal;
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var sem  = Str(r,"sem_proceso") ?? "";
                var ini  = Str(r,"fecini") ?? "";
                var fin  = Str(r,"fecfin") ?? "";
                lista.Add(new PlanillaPeriodoDto
                {
                    SemProceso = sem,
                    FecIni     = ini,
                    FecFin     = fin,
                    Label      = $"Sem {sem}: {ini[..5]} – {fin}"
                });
            }
        }
        catch (Exception ex) { _logger.LogError(ex, "ObtenerPeriodosAsync"); }
        return lista;
    }

    // ── RESUMEN ───────────────────────────────────────────────────────────────

    public async Task<List<PlanillaResumenDto>> ObtenerResumenAsync(PlanillaMensualFiltroDto filtro)
    {
        var lista = new List<PlanillaResumenDto>();
        try
        {
            await using var conn = new OracleConnection(_connStr);
            await conn.OpenAsync();
            await using var cmd  = conn.CreateCommand();
            cmd.CommandText  = "AQUARIUS.SP_SCA_READ_RESUMENTAREO_V2";
            cmd.CommandType  = CommandType.StoredProcedure;
            cmd.Parameters.Add("v_cod_empresa",       OracleDbType.Varchar2).Value = filtro.CodEmpresa;
            cmd.Parameters.Add("v_cod_tipo_planilla", OracleDbType.Varchar2).Value = filtro.CodTipoPlanilla;
            cmd.Parameters.Add("v_fecha_inicio",      OracleDbType.Varchar2).Value = filtro.FechaInicio;
            cmd.Parameters.Add("v_fecha_final",       OracleDbType.Varchar2).Value = filtro.FechaFinal;
            var pCur = cmd.Parameters.Add("cv_1", OracleDbType.RefCursor);
            pCur.Direction = ParameterDirection.Output;

            await cmd.ExecuteNonQueryAsync();
            await using var r = ((OracleRefCursor)pCur.Value).GetDataReader();
            while (await r.ReadAsync())
            {
                lista.Add(new PlanillaResumenDto
                {
                    CodEmpresa           = Str(r,"cod_empresa"),
                    CodPersonal          = Str(r,"cod_spring"),
                    NomTrabajador        = Str(r,"nom_trabajador"),
                    DiasEfectivos        = Int(r,"DiasEfectivos"),
                    DiasTurnoDia         = Int(r,"DiasTurnoDia"),
                    DiasTurnoNoche       = Int(r,"DiasTurnoNoche"),
                    HorasEfectivas       = Str(r,"HorasEfectivas"),
                    HorasEfectivas1      = Str(r,"HorasEfectivas1"),
                    DiasT2               = Int(r,"HorasEfectivas2_Dias"),
                    DiasT3               = Int(r,"HorasEfectivas3_Dias"),
                    DiasFalta            = Int(r,"DiasFalta"),
                    Tardanzas            = Str(r,"Tardanzas"),
                    Vacaciones           = Int(r,"Vacaciones"),
                    DescansosMedicos     = Int(r,"DescansosMedicos"),
                    Subsidios            = Int(r,"Subsidios"),
                    LicenciasSindicales  = Int(r,"LicenciasSindicales"),
                    Suspensiones         = Int(r,"Suspensiones"),
                    PermisoGoceFisico    = Int(r,"PermisoGoceFisico"),
                    LicenciaPaternidad   = Int(r,"LicenciaPaternidad"),
                    LicenciaFallecimiento= Int(r,"LicenciaFallecimiento"),
                    PermisosConGoce      = Str(r,"PermisosConGoce"),
                    PermisosSinGoce      = Str(r,"PermisosSinGoce"),
                    Horas25              = Str(r,"Horas25"),
                    Horas25MinRnd        = Int(r,"Horas25_min_rnd"),
                    Horas35              = Str(r,"Horas35"),
                    Horas35MinRnd        = Int(r,"Horas35_min_rnd"),
                    Horas50              = Str(r,"Horas50"),
                    Horas100             = Str(r,"Horas100"),
                    Horas100MinRnd       = Int(r,"Horas100_min_rnd"),
                });
            }
        }
        catch (Exception ex) { _logger.LogError(ex, "ObtenerResumenAsync"); throw; }
        return lista;
    }

    // ── DETALLE ───────────────────────────────────────────────────────────────

    public async Task<List<PlanillaDetalleFilaDto>> ObtenerDetalleAsync(PlanillaMensualFiltroDto filtro)
    {
        var lista = new List<PlanillaDetalleFilaDto>();
        try
        {
            await using var conn = new OracleConnection(_connStr);
            await conn.OpenAsync();
            await using var cmd  = conn.CreateCommand();
            cmd.CommandText  = "AQUARIUS.SP_SCA_REPORT_ASIDETB_ADM";
            cmd.CommandType  = CommandType.StoredProcedure;
            cmd.Parameters.Add("v_cod_empresa",       OracleDbType.NVarchar2).Value = filtro.CodEmpresa;
            cmd.Parameters.Add("v_cod_sucursal",      OracleDbType.NVarchar2).Value = filtro.CodSucursal;
            cmd.Parameters.Add("v_cod_tipo_planilla", OracleDbType.NVarchar2).Value = filtro.CodTipoPlanilla;
            cmd.Parameters.Add("v_c_costos",          OracleDbType.NVarchar2).Value = filtro.CCostos;
            cmd.Parameters.Add("v_cod_personal",      OracleDbType.NVarchar2).Value = DBNull.Value;
            cmd.Parameters.Add("v_fecha_inicio",      OracleDbType.NVarchar2).Value = filtro.FechaInicio;
            cmd.Parameters.Add("v_fecha_final",       OracleDbType.NVarchar2).Value = filtro.FechaFinal;
            cmd.Parameters.Add("v_orden",             OracleDbType.Char).Value      = "0"; // por nombre
            cmd.Parameters.Add("v_tip_codigo",        OracleDbType.NVarchar2).Value = "0"; // fotocheck
            cmd.Parameters.Add("v_tipo",              OracleDbType.Char).Value      = "1"; // por planilla
            var pCur = cmd.Parameters.Add("cv_1", OracleDbType.RefCursor);
            pCur.Direction = ParameterDirection.Output;

            await cmd.ExecuteNonQueryAsync();
            await using var r = ((OracleRefCursor)pCur.Value).GetDataReader();
            while (await r.ReadAsync())
            {
                lista.Add(new PlanillaDetalleFilaDto
                {
                    TipoFila          = Str(r,"tipo_fila")           ?? "D",
                    SemProceso        = Str(r,"sem_proceso"),
                    FecProceso        = Str(r,"fec_proceso"),
                    CodPersonal       = Str(r,"cod_personal"),
                    NumDocIdentidad   = Str(r,"num_doc_identidad"),
                    CodTipo           = Str(r,"cod_tipo"),
                    DesTipo           = Str(r,"des_tipo"),
                    NomTrabajador     = Str(r,"nom_trabajador"),
                    Dia               = Str(r,"dia"),
                    Feriado           = Str(r,"feriado"),
                    DiLab             = Str(r,"dialab"),
                    HorarioTeorico    = Str(r,"horarioteorico"),
                    TotHorasTeom      = Int(r,"tothorasteom"),
                    HorarioJornada    = Str(r,"horariojornada"),
                    HorarioRefrigerio = Str(r,"horariorefrigerio"),
                    HoraRef           = Str(r,"horaref"),
                    HoraTardanza      = Str(r,"horatardanza"),
                    HoraTardanzaM     = Int(r,"horatardanzam"),
                    HoraAnteSalida    = Str(r,"horaantesalida"),
                    HoraAnteSalidaM   = Int(r,"horaantesalidam"),
                    HoraPermiso       = Str(r,"horapermiso"),
                    HoraPermisoM      = Int(r,"horapermisom"),
                    HoraEfectiva      = Str(r,"horaefectiva"),
                    HoraEfectivaM     = Int(r,"horaefectivam"),
                    HoraEfectivaT1    = Str(r,"horaefectivaT1"),
                    HoraEfectivaT1M   = Int(r,"horaefectivaT1m"),
                    HoraEfectivaT2    = Str(r,"horaefectivaT2"),
                    HoraEfectivaT2M   = Int(r,"horaefectivaT2m"),
                    HoraEfectivaT3    = Str(r,"horaefectivaT3"),
                    HoraEfectivaT3M   = Int(r,"horaefectivaT3m"),
                    HoraExofi1        = Str(r,"horaexofi1"),
                    HoraExofi1M       = Int(r,"horaexofi1m"),
                    HoraExofi2        = Str(r,"horaexofi2"),
                    HoraExofi2M       = Int(r,"horaexofi2m"),
                    HoraDobles        = Str(r,"horadoblesof"),
                    HoraDoblesM       = Int(r,"horadoblesofm"),
                    TotHoraNocturna   = Str(r,"tothoranocturna_of"),
                    TotHoraNocturnaM  = Int(r,"tothoranocturna_ofm"),
                    DiasT2            = Int(r,"horaefectivaT2_dias"),
                    DiasT3            = Int(r,"horaefectivaT3_dias"),
                    HoraExofi1MRnd    = Int(r,"horaexofi1m_rnd"),
                    HoraExofi2MRnd    = Int(r,"horaexofi2m_rnd"),
                    HoraDoblesRnd     = Int(r,"horadoblesofm_rnd"),
                });
            }
        }
        catch (Exception ex) { _logger.LogError(ex, "ObtenerDetalleAsync"); throw; }
        return lista;
    }
}
