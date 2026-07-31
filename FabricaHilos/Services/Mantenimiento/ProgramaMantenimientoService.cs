using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System.Data;
using System.Data.Common;
using FabricaHilos.Models.Mantenimiento;

namespace FabricaHilos.Services.Mantenimiento;

public interface IProgramaMantenimientoService
{
    Task<List<ProgramaMantenimientoListItemDto>>  ListarAsignadosAsync(string cCodigo);
    Task<List<ProgramaPendienteValidarDto>>       ListarPendientesValidarAsync(string cCodigo);
    Task<List<ProgramaJefeVistaDto>>               ListarJefeAsync(string cCodigo);
    Task<ProgramaMantenimientoDetalleViewModel?>  ObtenerDetalleAsync(long numProg, string? cCodigoUsuario = null);
    Task<(bool Ok, string? Mensaje)>               ValidarAsync(long numProg, string cCodigo);
}

/// <summary>
/// Consume PKG_MA_PROGRAMA (esquema SIG) — listado/detalle de programas de
/// mantenimiento legacy (MA_PROGRAMA*) y la acción de validación del mecánico.
/// </summary>
public class ProgramaMantenimientoService : OracleServiceBase, IProgramaMantenimientoService
{
    public ProgramaMantenimientoService(
        IConfiguration       configuration,
        IHttpContextAccessor httpContextAccessor)
        : base(configuration, httpContextAccessor) { }

    // ── Helpers de lectura (DBNull-safe) ──────────────────────────────────────
    private static DateTime? ReadDate(DbDataReader r, string col) => r[col] is DBNull ? null : Convert.ToDateTime(r[col]);
    private static decimal   ReadDec(DbDataReader r, string col)  => r[col] is DBNull ? 0m   : Convert.ToDecimal(r[col]);
    private static string?   ReadStr(DbDataReader r, string col)  => r[col] is DBNull ? null  : r[col].ToString()?.Trim();
    private static long?     ReadLong(DbDataReader r, string col) => r[col] is DBNull ? (long?)null : Convert.ToInt64(r[col]);
    private static int?      ReadInt(DbDataReader r, string col)  => r[col] is DBNull ? (int?)null  : Convert.ToInt32(r[col]);

    private static OracleParameter AddOutCursor(OracleCommand cmd, string name)
    {
        var p = cmd.Parameters.Add(name, OracleDbType.RefCursor);
        p.Direction = ParameterDirection.Output;
        return p;
    }

    private static ProgramaMantenimientoListItemDto LeerListItem(DbDataReader r) => new()
    {
        NumProg    = ReadLong(r, "NROPROG") ?? 0,
        CCodigo    = ReadStr(r, "C_CODIGO"),
        CodMaq     = ReadStr(r, "COD_MAQ"),
        CodAct     = ReadStr(r, "COD_ACT"),
        ActivoDesc = ReadStr(r, "ACTIVO_DESC"),
        Tipo       = ReadStr(r, "TIPO"),
        Clase      = ReadStr(r, "CLASE"),
        Estado     = ReadStr(r, "ESTADO"),
        EstadoDesc = ReadStr(r, "ESTADO_DESC"),
        Detalle    = ReadStr(r, "DETALLE"),
        Informe    = ReadStr(r, "INFORME"),
        FechaIni   = ReadDate(r, "FECHA_INI"),
        FechaFin   = ReadDate(r, "FECHA_FIN"),
        FchProg    = ReadDate(r, "FCH_PROG"),
        FchFirma   = ReadDate(r, "FCH_FIRMA"),
        RespFirma  = ReadStr(r, "RESP_FIRMA"),
        Validado   = ReadStr(r, "VALIDADO") == "S",
        PuedeValidar = ReadStr(r, "PUEDE_VALIDAR") == "S",
    };

    public async Task<List<ProgramaMantenimientoListItemDto>> ListarAsignadosAsync(string cCodigo)
    {
        var result = new List<ProgramaMantenimientoListItemDto>();

        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"{S}PKG_MA_PROGRAMA.P_LISTA_ASIGNADOS";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName  = true;
        cmd.Parameters.Add("P_C_CODIGO", OracleDbType.Varchar2).Value = cCodigo;
        AddOutCursor(cmd, "P_CURSOR");

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result.Add(LeerListItem(reader));

        return result;
    }

    public async Task<ProgramaMantenimientoDetalleViewModel?> ObtenerDetalleAsync(long numProg, string? cCodigoUsuario = null)
    {
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"{S}PKG_MA_PROGRAMA.P_DETALLE";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName  = true;
        cmd.Parameters.Add("P_NROPROG",  OracleDbType.Decimal).Value  = numProg;
        cmd.Parameters.Add("P_C_CODIGO", OracleDbType.Varchar2).Value = (object?)cCodigoUsuario ?? DBNull.Value;
        var pCab       = AddOutCursor(cmd, "P_CUR_CAB");
        var pTareas    = AddOutCursor(cmd, "P_CUR_TAREAS");
        var pTiempos   = AddOutCursor(cmd, "P_CUR_TIEMPOS");
        var pMateriales = AddOutCursor(cmd, "P_CUR_MATERIALES");

        await cmd.ExecuteNonQueryAsync();

        var vm = new ProgramaMantenimientoDetalleViewModel();

        await using (var r = ((OracleRefCursor)pCab.Value).GetDataReader())
        {
            if (await r.ReadAsync())
            {
                var baseDto = LeerListItem(r);
                vm.Cabecera = new ProgramaMantenimientoCabeceraDto
                {
                    NumProg = baseDto.NumProg, CCodigo = baseDto.CCodigo, CodMaq = baseDto.CodMaq,
                    CodAct  = baseDto.CodAct, ActivoDesc = baseDto.ActivoDesc, Tipo = baseDto.Tipo,
                    Clase   = baseDto.Clase, Estado = baseDto.Estado, EstadoDesc = baseDto.EstadoDesc,
                    Detalle = baseDto.Detalle, Informe = baseDto.Informe, FechaIni = baseDto.FechaIni,
                    FechaFin = baseDto.FechaFin, FchProg = baseDto.FchProg, FchFirma = baseDto.FchFirma,
                    RespFirma = baseDto.RespFirma, Validado = baseDto.Validado,
                    Mecanico        = ReadStr(r, "MECANICO"),
                    RespFirmaNombre = ReadStr(r, "RESP_FIRMA_NOMBRE"),
                    Ccosto          = ReadStr(r, "CCOSTO"),
                    CcostoNombre    = ReadStr(r, "CCOSTO_NOMBRE"),
                    JefeCCodigo     = ReadStr(r, "JEFE_C_CODIGO"),
                    JefeNombre      = ReadStr(r, "JEFE_NOMBRE"),
                    PuedeValidarJefe = ReadStr(r, "PUEDE_VALIDAR") == "S",
                };
            }
        }
        if (vm.Cabecera == null)
            return null;

        await using (var r = ((OracleRefCursor)pTareas.Value).GetDataReader())
        {
            while (await r.ReadAsync())
                vm.Tareas.Add(new ProgramaMantenimientoTareaDto
                {
                    ItemActiv = ReadInt(r, "ITEM_ACTIV"),
                    CodTarea  = ReadLong(r, "COD_TAREA"),
                    TareaDesc = ReadStr(r, "TAREA_DESC"),
                    Detalle   = ReadStr(r, "DETALLE"),
                    Estado    = ReadStr(r, "ESTADO"),
                });
        }

        await using (var r = ((OracleRefCursor)pTiempos.Value).GetDataReader())
        {
            while (await r.ReadAsync())
                vm.Tiempos.Add(new ProgramaMantenimientoTiempoDto
                {
                    CCodigo     = ReadStr(r, "C_CODIGO"),
                    Mecanico    = ReadStr(r, "MECANICO"),
                    FechaIni    = ReadDate(r, "FECHA_INI"),
                    FechaFin    = ReadDate(r, "FECHA_FIN"),
                    Estado      = ReadStr(r, "ESTADO"),
                    Observacion = ReadStr(r, "OBSERVACION"),
                    Horas       = ReadDec(r, "HORAS"),
                });
        }

        await using (var r = ((OracleRefCursor)pMateriales.Value).GetDataReader())
        {
            while (await r.ReadAsync())
                vm.Materiales.Add(new ProgramaMantenimientoMaterialDto
                {
                    TipoDoc  = ReadStr(r, "TIPODOC"),
                    Serie    = ReadInt(r, "SERIE"),
                    NroDoc   = ReadLong(r, "NRODOC"),
                    CodArt   = ReadStr(r, "COD_ART"),
                    ArtDesc  = ReadStr(r, "ART_DESC"),
                    Cantidad = ReadDec(r, "CANTIDAD"),
                });
        }

        return vm;
    }

    public async Task<List<ProgramaPendienteValidarDto>> ListarPendientesValidarAsync(string cCodigo)
    {
        var result = new List<ProgramaPendienteValidarDto>();

        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"{S}PKG_MA_PROGRAMA.P_LISTA_PARA_VALIDAR";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName  = true;
        cmd.Parameters.Add("P_C_CODIGO", OracleDbType.Varchar2).Value = cCodigo;
        AddOutCursor(cmd, "P_CURSOR");

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new ProgramaPendienteValidarDto
            {
                NumProg      = ReadLong(reader, "NROPROG") ?? 0,
                CCodigo      = ReadStr(reader, "C_CODIGO"),
                Mecanico     = ReadStr(reader, "MECANICO"),
                CodMaq       = ReadStr(reader, "COD_MAQ"),
                CodAct       = ReadStr(reader, "COD_ACT"),
                ActivoDesc   = ReadStr(reader, "ACTIVO_DESC"),
                Ccosto       = ReadStr(reader, "CCOSTO"),
                CcostoNombre = ReadStr(reader, "CCOSTO_NOMBRE"),
                EncargadoCCodigo = ReadStr(reader, "ENCARGADO_C_CODIGO"),
                EncargadoNombre  = ReadStr(reader, "ENCARGADO_NOMBRE"),
                Tipo         = ReadStr(reader, "TIPO"),
                Clase        = ReadStr(reader, "CLASE"),
                Detalle      = ReadStr(reader, "DETALLE"),
                Informe      = ReadStr(reader, "INFORME"),
                FechaIni     = ReadDate(reader, "FECHA_INI"),
                FechaFin     = ReadDate(reader, "FECHA_FIN"),
                FchProg      = ReadDate(reader, "FCH_PROG"),
                Estado       = ReadStr(reader, "ESTADO"),
                EstadoDesc   = ReadStr(reader, "ESTADO_DESC"),
            });
        }

        return result;
    }

    public async Task<List<ProgramaJefeVistaDto>> ListarJefeAsync(string cCodigo)
    {
        var result = new List<ProgramaJefeVistaDto>();

        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"{S}PKG_MA_PROGRAMA.P_LISTA_JEFE";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName  = true;
        cmd.Parameters.Add("P_C_CODIGO", OracleDbType.Varchar2).Value = cCodigo;
        AddOutCursor(cmd, "P_CURSOR");

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new ProgramaJefeVistaDto
            {
                NumProg      = ReadLong(reader, "NROPROG") ?? 0,
                CCodigo      = ReadStr(reader, "C_CODIGO"),
                Mecanico     = ReadStr(reader, "MECANICO"),
                CodMaq       = ReadStr(reader, "COD_MAQ"),
                CodAct       = ReadStr(reader, "COD_ACT"),
                ActivoDesc   = ReadStr(reader, "ACTIVO_DESC"),
                Ccosto       = ReadStr(reader, "CCOSTO"),
                CcostoNombre = ReadStr(reader, "CCOSTO_NOMBRE"),
                EncargadoCCodigo = ReadStr(reader, "ENCARGADO_C_CODIGO"),
                EncargadoNombre  = ReadStr(reader, "ENCARGADO_NOMBRE"),
                Tipo         = ReadStr(reader, "TIPO"),
                Clase        = ReadStr(reader, "CLASE"),
                Detalle      = ReadStr(reader, "DETALLE"),
                Informe      = ReadStr(reader, "INFORME"),
                FechaIni     = ReadDate(reader, "FECHA_INI"),
                FechaFin     = ReadDate(reader, "FECHA_FIN"),
                FchProg      = ReadDate(reader, "FCH_PROG"),
                FchFirma     = ReadDate(reader, "FCH_FIRMA"),
                RespFirma    = ReadStr(reader, "RESP_FIRMA"),
                RespFirmaNombre = ReadStr(reader, "RESP_FIRMA_NOMBRE"),
                Validado     = ReadStr(reader, "VALIDADO") == "S",
            });
        }

        return result;
    }

    public async Task<(bool Ok, string? Mensaje)> ValidarAsync(long numProg, string cCodigo)
    {
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"{S}PKG_MA_PROGRAMA.P_VALIDAR";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName  = true;
        cmd.Parameters.Add("P_NROPROG",  OracleDbType.Decimal).Value  = numProg;
        cmd.Parameters.Add("P_C_CODIGO", OracleDbType.Varchar2).Value = cCodigo;
        var pMsg = cmd.Parameters.Add("P_MSG", OracleDbType.Varchar2, 500);
        pMsg.Direction = ParameterDirection.Output;

        await cmd.ExecuteNonQueryAsync();

        string? msg = null;
        if (pMsg.Value is OracleString os && !os.IsNull)
            msg = os.Value;

        return string.IsNullOrEmpty(msg) ? (true, null) : (false, msg);
    }
}
