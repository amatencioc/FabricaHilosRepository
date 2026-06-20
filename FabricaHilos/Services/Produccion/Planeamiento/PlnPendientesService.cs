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
                CodCliente = Str(r["COD_CLIENTE_07"]),
                CodVende   = Str(r["COD_VENDE_07"]),
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
                CodCliente = Str(r["COD_CLIENTE_03"]),
                CodVende   = Str(r["COD_VENDE_03"]),
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
                CodCliente = Str(r["COD_CLIENTE_05"]),
                CodVende   = Str(r["COD_VENDE_05"]),
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
                CodCliente = Str(r["COD_CLIENTE"]),
                CodVende   = Str(r["COD_VENDE"]),
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

    // ── SP_PLN_PEND_SECADO ────────────────────────────────────────────────────────────────
    public async Task<IEnumerable<PlnPendienteSecado>> GetPendientesSecadoAsync(
        string tipo = "%", string asesor = "%", string cliente = "%")
    {
        await using var conn = await AbrirConexionAsync();
        await using var cmd  = BuildSpCmd(conn, "SP_PLN_PEND_SECADO", tipo, asesor, cliente);

        var list = new List<PlnPendienteSecado>();
        await using var r = (OracleDataReader)await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new PlnPendienteSecado
            {
                Partida    = Str(r["PARTIDA_01"]),
                Material   = Str(r["MATERIAL_01"]),
                Cliente    = Str(r["DESC_CLIENTE_01"]),
                CodCliente = Str(r["COD_CLIENTE_01"]),
                CodVende   = Str(r["COD_VENDE_01"]),
                Fecha      = Dat(r["FECHA_01"]),
                CodMaq     = Str(r["COD_MAQ_01"]),
                Maquina    = Str(r["DESC_MAQ_01"]),
                Proceso    = Str(r["PROCESO_01"]),
                NroRmc     = Dec(r["NRO_RMC_01"]),
                Peso       = Dec(r["PESO_PARTIDA_01"]),
                Lote       = Str(r["LOTE_01"]),
                ColoSer    = Str(r["COLO_SER_01"]),
                FchEntrega = Dat(r["FCH_ENTREGA_01"]),
            });
        return list;
    }

    // ── SP_PLN_PEND_MADEJA ────────────────────────────────────────────────────────────────
    public async Task<IEnumerable<PlnPendienteMadeja>> GetPendientesMadejaAsync(
        string tipo = "%", string asesor = "%", string cliente = "%")
    {
        await using var conn = await AbrirConexionAsync();
        await using var cmd  = BuildSpCmd(conn, "SP_PLN_PEND_MADEJA", tipo, asesor, cliente);

        var list = new List<PlnPendienteMadeja>();
        await using var r = (OracleDataReader)await cmd.ExecuteReaderAsync();

        // ── DIAGNÓSTICO: volcar nombres reales de columna ────────────────────
        var colNames = Enumerable.Range(0, r.FieldCount).Select(i => $"{i}:{r.GetName(i)}").ToArray();
        System.Console.WriteLine("[SP_PLN_PEND_MADEJA] Columnas reales: " + string.Join(" | ", colNames));
        // ────────────────────────────────────────────────────────────────────

        // Índices seguros basados en posición hasta confirmar nombres reales
        bool HasCol(string name) { try { r.GetOrdinal(name); return true; } catch { return false; } }

        while (await r.ReadAsync())
        {
            // Detectar nombres reales en primera fila
            list.Add(new PlnPendienteMadeja
            {
                Partida    = HasCol("PARTIDA_000")       ? Str(r["PARTIDA_000"])       : Str(r[0]),
                Material   = HasCol("MATERIAL_000")      ? Str(r["MATERIAL_000"])      : (r.FieldCount > 1  ? Str(r[1])  : ""),
                Cliente    = HasCol("DESC_CLIENTE_000")  ? Str(r["DESC_CLIENTE_000"])  : (r.FieldCount > 2  ? Str(r[2])  : ""),
                CodCliente = HasCol("COD_CLIENTE_000")   ? Str(r["COD_CLIENTE_000"])   : (r.FieldCount > 3  ? Str(r[3])  : ""),
                CodVende   = HasCol("COD_VENDE_000")     ? Str(r["COD_VENDE_000"])     : (r.FieldCount > 4  ? Str(r[4])  : ""),
                FchProg    = HasCol("FCH_PROG_000")      ? Dat(r["FCH_PROG_000"])      : (r.FieldCount > 5  ? Dat(r[5])  : null),
                CodMaq     = HasCol("COD_MAQUINA_000")   ? Str(r["COD_MAQUINA_000"])   : (r.FieldCount > 6  ? Str(r[6])  : ""),
                Maquina    = HasCol("DESC_MAQUINA_000")  ? Str(r["DESC_MAQUINA_000"])  : (r.FieldCount > 7  ? Str(r[7])  : ""),
                NroRmc     = HasCol("NRO_RMC_000")       ? Dec(r["NRO_RMC_000"])       : (r.FieldCount > 8  ? Dec(r[8])  : 0m),
                Peso       = HasCol("NETO_GUIA_000")     ? Dec(r["NETO_GUIA_000"])     : (r.FieldCount > 9  ? Dec(r[9])  : 0m),
                Lote       = HasCol("LOTE_000")          ? Str(r["LOTE_000"])          : (r.FieldCount > 10 ? Str(r[10]) : ""),
                ColoSer    = HasCol("COLO_SER_000")      ? Str(r["COLO_SER_000"])      : (r.FieldCount > 11 ? Str(r[11]) : ""),
                FchEntrega = HasCol("FCH_ENTREGA_000")   ? Dat(r["FCH_ENTREGA_000"])   : (r.FieldCount > 12 ? Dat(r[12]) : null),
            });
        }
        return list;
    }

    // ── SP_PLN_PEND_PARTIDAS_DEF ──────────────────────────────────────────────────────────────
    public async Task<IEnumerable<PlnPendientePartidaDef>> GetPendientesPartidasDefAsync(
        string estEval = "%")
    {
        await using var conn = await AbrirConexionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText    = $"{S}PKG_PLN.SP_PLN_PEND_PARTIDAS_DEF";
        cmd.CommandType    = CommandType.StoredProcedure;
        cmd.BindByName     = true;
        cmd.CommandTimeout = TimeoutSeconds;
        cmd.Parameters.Add("p_est_eval", OracleDbType.Varchar2).Value =
            string.IsNullOrWhiteSpace(estEval) ? "%" : estEval;
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
