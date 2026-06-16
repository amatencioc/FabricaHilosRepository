using System.Data;
using Oracle.ManagedDataAccess.Client;
using FabricaHilos.Models.Produccion.Planeamiento;

namespace FabricaHilos.Services.Produccion.Planeamiento;

public class PlnPendientesService : OracleServiceBase, IPlnPendientesService
{
    private const int TimeoutSeconds = 60;

    public PlnPendientesService(
        IConfiguration       configuration,
        IHttpContextAccessor httpContextAccessor)
        : base(configuration, httpContextAccessor) { }

    // ── Helpers ─────────────────────────────────────────────────────────────
    private static string     Str(object? v) =>
        v == null || v == DBNull.Value ? "" : v.ToString()?.Trim() ?? "";

    private static decimal    Dec(object? v) =>
        v == null || v == DBNull.Value ? 0m : Convert.ToDecimal(v);

    private static DateTime?  Dat(object? v) =>
        v == null || v == DBNull.Value ? null : Convert.ToDateTime(v);

    private OracleCommand BuildSpCmd(OracleConnection conn, string spName,
        string tipo, string asesor, string cliente)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText    = $"{S}PKG_PLN.{spName}";
        cmd.CommandType    = CommandType.StoredProcedure;
        cmd.BindByName     = true;
        cmd.CommandTimeout = TimeoutSeconds;
        cmd.Parameters.Add("p_tipo",    OracleDbType.Varchar2).Value =
            string.IsNullOrWhiteSpace(tipo)    ? "%" : tipo;
        cmd.Parameters.Add("p_asesor",  OracleDbType.Varchar2).Value =
            string.IsNullOrWhiteSpace(asesor)  ? "%" : asesor;
        cmd.Parameters.Add("p_cliente", OracleDbType.Varchar2).Value =
            string.IsNullOrWhiteSpace(cliente) ? "%" : cliente;
        var pCursor = cmd.Parameters.Add("p_cursor", OracleDbType.RefCursor);
        pCursor.Direction = ParameterDirection.Output;
        return cmd;
    }

    // ── SP_PLN_FILTRO_TIPO ───────────────────────────────────────────────────
    public async Task<IEnumerable<PlnFiltroTipo>> GetFiltroTipoAsync()
    {
        await using var conn = await AbrirConexionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText    = $"{S}PKG_PLN.SP_PLN_FILTRO_TIPO";
        cmd.CommandType    = CommandType.StoredProcedure;
        cmd.BindByName     = true;
        cmd.CommandTimeout = TimeoutSeconds;
        var pCursor = cmd.Parameters.Add("p_cursor", OracleDbType.RefCursor);
        pCursor.Direction = ParameterDirection.Output;

        var list = new List<PlnFiltroTipo>();
        await using var r = (OracleDataReader)await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new PlnFiltroTipo { Tipo = Str(r["TIPO"]), Descripcion = Str(r["DESCRIPCION"]) });
        return list;
    }

    // ── SP_PLN_PEND_REVISADO ─────────────────────────────────────────────────
    public async Task<IEnumerable<PlnPendienteRevisado>> GetPendientesRevisadoAsync(
        string tipo = "%", string asesor = "%", string cliente = "%")
    {
        await using var conn = await AbrirConexionAsync();
        await using var cmd  = BuildSpCmd(conn, "SP_PLN_PEND_REVISADO", tipo, asesor, cliente);

        var list = new List<PlnPendienteRevisado>();
        await using var r = (OracleDataReader)await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new PlnPendienteRevisado
            {
                Partida    = Str(r["PARTIDA_07"]),
                Material   = Str(r["MATERIAL_07"]),
                FechaFin   = Dat(r["FECHA_FIN_07"]),
                Cliente    = Str(r["DESC_CLIENTE_07"]),
                Maquina    = Str(r["DESC_MAQ_07"]),
                NroRmc     = Dec(r["NRO_RMC_07"]),
                Peso       = Dec(r["PESO_PARTIDA_07"]),
                Lote       = Str(r["LOTE_07"]),
                ColoSer    = Str(r["COLO_SER_07"]),
                FchEntrega = Dat(r["FCH_ENTREGA_07"]),
            });
        return list;
    }

    // ── SP_PLN_PEND_EVAL_CALIDAD ─────────────────────────────────────────────
    public async Task<IEnumerable<PlnPendienteEvalCalidad>> GetPendientesEvalCalidadAsync(
        string tipo = "%", string asesor = "%", string cliente = "%")
    {
        await using var conn = await AbrirConexionAsync();
        await using var cmd  = BuildSpCmd(conn, "SP_PLN_PEND_EVAL_CALIDAD", tipo, asesor, cliente);

        var list = new List<PlnPendienteEvalCalidad>();
        await using var r = (OracleDataReader)await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new PlnPendienteEvalCalidad
            {
                Partida    = Str(r["PARTIDA_03"]),
                Material   = Str(r["MATERIAL_03"]),
                Cliente    = Str(r["DESC_CLIENTE_03"]),
                FechaFin   = Dat(r["FECHA_FIN_03"]),
                CodMaq     = Str(r["COD_MAQ_03"]),
                Maquina    = Str(r["DESC_MAQ_03"]),
                NroRmc     = Dec(r["NRO_RMC_03"]),
                Peso       = Dec(r["PESO_PARTIDA_03"]),
                Lote       = Str(r["LOTE_03"]),
                ColoSer    = Str(r["COLO_SER_03"]),
                FchEntrega = Dat(r["FCH_ENTREGA_03"]),
            });
        return list;
    }

    // ── SP_PLN_PEND_ENCONADO ─────────────────────────────────────────────────
    public async Task<IEnumerable<PlnPendienteEnconado>> GetPendientesEnconadoAsync(
        string tipo = "%", string asesor = "%", string cliente = "%")
    {
        await using var conn = await AbrirConexionAsync();
        await using var cmd  = BuildSpCmd(conn, "SP_PLN_PEND_ENCONADO", tipo, asesor, cliente);

        var list = new List<PlnPendienteEnconado>();
        await using var r = (OracleDataReader)await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new PlnPendienteEnconado
            {
                Partida    = Str(r["PARTIDA_05"]),
                Material   = Str(r["MATERIAL_05"]),
                Cliente    = Str(r["DESC_CLIENTE_05"]),
                Fecha      = Dat(r["FECHA_05"]),
                EstEval    = Str(r["DESC_EST_EVAL_05"]),
                Resultado  = Str(r["DESC_RESULTADO_05"]),
                NroRmc     = Dec(r["NRO_RMC_05"]),
                Peso       = Dec(r["PESO_PARTIDA_05"]),
                Lote       = Str(r["LOTE_05"]),
                ColoSer    = Str(r["COLO_SER_05"]),
                FchEntrega = Dat(r["FCH_ENTREGA_05"]),
                Origen     = Str(r["ORIGEN"]),
            });
        return list;
    }

    // ── SP_PLN_PEND_TENIDO ───────────────────────────────────────────────────
    public async Task<IEnumerable<PlnPendienteTenido>> GetPendientesTenidoAsync(
        string tipo = "%", string asesor = "%", string cliente = "%")
    {
        await using var conn = await AbrirConexionAsync();
        await using var cmd  = BuildSpCmd(conn, "SP_PLN_PEND_TENIDO", tipo, asesor, cliente);

        var list = new List<PlnPendienteTenido>();
        await using var r = (OracleDataReader)await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new PlnPendienteTenido
            {
                Partida    = Str(r["PARTIDA"]),
                Material   = Str(r["MATERIAL"]),
                Cliente    = Str(r["DESC_CLIENTE"]),
                FechaProg  = Dat(r["FECHA_PROG"]),
                CodMaq     = Str(r["COD_MAQ"]),
                Maquina    = Str(r["DESC_MAQ"]),
                Proceso    = Str(r["PROCESO"]),
                Rmc        = Str(r["RMC"]),
                NroRmc     = Dec(r["NRO_RMC"]),
                Peso       = Dec(r["PESO"]),
                Lote       = Str(r["LOTE"]),
                ColoSer    = Str(r["COLO_SER"]),
                FchEntrega = Dat(r["FCH_ENTREGA"]),
                Origen     = Str(r["ORIGEN"]),
            });
        return list;
    }

    // ── SP_PLN_PEND_PARTIDAS_DEF ─────────────────────────────────────────────
    public async Task<IEnumerable<PlnPendientePartidaDef>> GetPendientesPartidasDefAsync()
    {
        await using var conn = await AbrirConexionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText    = $"{S}PKG_PLN.SP_PLN_PEND_PARTIDAS_DEF";
        cmd.CommandType    = CommandType.StoredProcedure;
        cmd.BindByName     = true;
        cmd.CommandTimeout = TimeoutSeconds;
        var pCursor = cmd.Parameters.Add("p_cursor", OracleDbType.RefCursor);
        pCursor.Direction = ParameterDirection.Output;

        var list = new List<PlnPendientePartidaDef>();
        await using var r = (OracleDataReader)await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new PlnPendientePartidaDef
            {
                Fecha          = Dat(r["FECHA_01"]),
                Guia           = Dec(r["GUIA_01"]),
                Partida        = Str(r["PARTIDA_01"]),
                Material       = Str(r["MATERIAL_01"]),
                Color          = Str(r["COLOR_01"]),
                DescIntensidad = Str(r["DESC_INTENSIDAD_01"]),
                NroRmc         = Dec(r["NRO_RMC_01"]),
                PesoNeto       = Dec(r["PESO_NETO_01"]),
                CodCliente     = Str(r["COD_CLIENTE_01"]),
                Cliente        = Str(r["DESCLIENTE_01"]),
                Consulta       = Str(r["CONSULTA_01"]),
                DescDefecto    = Str(r["DESC_DEFECTO_01"]),
                Observaciones  = Str(r["OBSERVACIONES_01"]),
                DescEvaluacion = Str(r["DESC_EVALUACION_01"]),
                FchEntrega     = Dat(r["FCH_ENTREGA_01"]),
            });
        return list;
    }
}
