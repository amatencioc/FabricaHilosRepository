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
                Tipo       = Str(r["TIPO_07"]),
                DescAsesor = Str(r["DESC_ASESOR_07"]),
                Guia       = Dec(r["GUIA_07"]),
                Prioridad  = r["PRIORIDAD_07"] == DBNull.Value ? 99 : Convert.ToInt32(r["PRIORIDAD_07"]),
            });
        return list;
    }

    // ── PLN_PRIOR_REVISADO ─────────────────────────────────────────────────────────
    public async Task GuardarPrioridadRevisadoAsync(decimal guia, int prioridad)
    {
        await using var conn = await AbrirConexionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.BindByName  = true;
        cmd.CommandText =
            $"MERGE INTO {S}PLN_PRIOR_REVISADO PR " +
            "USING (SELECT :p_guia AS GUIA FROM DUAL) SRC " +
            "ON (PR.GUIA = SRC.GUIA) " +
            "WHEN MATCHED THEN UPDATE SET PR.PRIORIDAD = :p_prio " +
            "WHEN NOT MATCHED THEN INSERT (GUIA, PRIORIDAD) VALUES (:p_guia, :p_prio)";
        cmd.Parameters.Add("p_guia", OracleDbType.Decimal).Value = guia;
        cmd.Parameters.Add("p_prio", OracleDbType.Int32).Value   = prioridad;
        await cmd.ExecuteNonQueryAsync();
    }

    // ── SP_PLN_OBS_REVISADO ─────────────────────────────────────────────────────────────────────
    public async Task<IEnumerable<PlnObservacionRevisado>> GetObservacionesRevisadoAsync(
        string tipo = "%", string asesor = "%", string cliente = "%",
        DateTime? fechaI = null, DateTime? fechaF = null)
    {
        try
        {
            await using var conn = await AbrirConexionAsync();
            await using var cmd  = conn.CreateCommand();
            cmd.CommandText    = $"{S}PKG_PLN.SP_PLN_OBS_REVISADO";
            cmd.CommandType    = CommandType.StoredProcedure;
            cmd.BindByName     = true;
            cmd.CommandTimeout = TimeoutSeconds;
            cmd.Parameters.Add("p_tipo",    OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(tipo)    ? "%" : tipo;
            cmd.Parameters.Add("p_asesor",  OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(asesor)  ? "%" : asesor;
            cmd.Parameters.Add("p_cliente", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(cliente) ? "%" : cliente;
            cmd.Parameters.Add("p_fechai",  OracleDbType.Date).Value     = fechaI ?? DateTime.Today.AddDays(-30);
            cmd.Parameters.Add("p_fechaf",  OracleDbType.Date).Value     = fechaF ?? DateTime.Today;
            var pCursor = cmd.Parameters.Add("p_cursor", OracleDbType.RefCursor);
            pCursor.Direction = ParameterDirection.Output;

            var list = new List<PlnObservacionRevisado>();
            await using var r = (OracleDataReader)await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new PlnObservacionRevisado
                {
                    FechaFin    = Dat(r["FECHA_FIN_07"]),
                    Partida     = Str(r["PARTIDA_07"]),
                    Material    = Str(r["MATERIAL_07"]),
                    Peso        = Dec(r["PESO_PARTIDA_07"]),
                    Cliente     = Str(r["DESC_CLIENTE_07"]),
                    CodCliente  = Str(r["COD_CLIENTE_07"]),
                    CodVende    = Str(r["COD_VENDE_07"]),
                    CodMaq      = Str(r["COD_MAQ_07"]),
                    Maquina     = Str(r["DESC_MAQ_07"]),
                    NroRmc      = Dec(r["NRO_RMC_07"]),
                    Lote        = Str(r["LOTE_07"]),
                    ColoSer     = Str(r["COLO_SER_07"]),
                    Faltante    = Dec(r["FALTANTE_07"]),
                    Rechazado   = Dec(r["RECHAZADO_07"]),
                    Reenconado  = Dec(r["REENCONADO_07"]),
                    Evaluado    = Dec(r["EVALUADO_07"]),
                    FchEntrega  = Dat(r["FCH_ENTREGA_07"]),
                    Observacion = Str(r["OBSERVACION_07"]),
                    DescAsesor  = Str(r["DESC_ASESOR_07"]),
                });
            return list;
        }
        catch
        {
            return Enumerable.Empty<PlnObservacionRevisado>();
        }
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
                Rmc        = Str(r["RMC"]),
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
        while (await r.ReadAsync())
            list.Add(new PlnPendienteMadeja
            {
                Partida    = Str(r["PARTIDA_000"]),
                Material   = Str(r["MATERIAL_000"]),
                Cliente    = Str(r["DESC_CLIENTE_000"]),
                CodCliente = Str(r["COD_CLIENTE_000"]),
                CodVende   = Str(r["COD_VENDE_000"]),
                FchProg    = Dat(r["FCH_PROG_000"]),
                Maquina    = Str(r["DESC_MAQUINA_000"]),
                CodMaq     = Str(r["RMC_000"]),
                NroRmc     = Dec(r["NRO_RMC_000"]),
                Peso       = Dec(r["NETO_GUIA_000"]),
                Lote       = Str(r["LOTE_000"]),
                ColoSer    = Str(r["COLO_SER_000"]),
                FchEntrega = Dat(r["FCH_ENTREGA_000"]),
            });
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

    // ── SP_PLN_RECT_RECETA ───────────────────────────────────────────────────
    public async Task<IEnumerable<PlnRectificacionReceta>> GetRectificacionesRecetaAsync(
        string estado = "%")
    {
        try
        {
            await using var conn = await AbrirConexionAsync();
            await using var cmd  = conn.CreateCommand();
            cmd.CommandText    = $"{S}PKG_PLN.SP_PLN_RECT_RECETA";
            cmd.CommandType    = CommandType.StoredProcedure;
            cmd.BindByName     = true;
            cmd.CommandTimeout = TimeoutSeconds;
            cmd.Parameters.Add("p_estado", OracleDbType.Varchar2).Value =
                string.IsNullOrWhiteSpace(estado) ? "%" : estado;
            var pCursor = cmd.Parameters.Add("p_cursor", OracleDbType.RefCursor);
            pCursor.Direction = ParameterDirection.Output;

            var list = new List<PlnRectificacionReceta>();
            await using var r = (OracleDataReader)await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new PlnRectificacionReceta
                {
                    Fecha        = Dat(r["FECHA"]),
                    Area         = Str(r["AREA"]),
                    UserRegistro = Str(r["USER_REGISTRO"]),
                    DescDefecto  = Str(r["DESC_DEFECTO"]),
                    Partida      = Str(r["PARTIDA"]),
                    Material     = Str(r["MATERIAL"]),
                    DescCliente  = Str(r["DESC_CLIENTE"]),
                    DescLabo     = Str(r["DESC_LABO"]),
                    FentregaPed  = Dat(r["FENTREGA_PED"]),
                    Estado       = Str(r["ESTADO"]),
                });
            return list;
        }
        catch
        {
            return Enumerable.Empty<PlnRectificacionReceta>();
        }
    }
}
