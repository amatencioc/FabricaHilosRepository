using System.Data;
using Oracle.ManagedDataAccess.Client;
using FabricaHilos.Models.Produccion.Planeamiento;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace FabricaHilos.Services.Produccion.Planeamiento;

public class PlnPendientesService : OracleServiceBase, IPlnPendientesService
{
    private const int TimeoutSeconds = 60;
    // TTL corto: solo evita repetir, dentro de la misma ventana de refresco de pantalla,
    // la consulta "universo" (sin filtros) que se dispara en paralelo a la consulta filtrada
    // en cada request de PendientesEnconado, sin comprometer la frescura de los datos.
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(20);
    private readonly ILogger<PlnPendientesService> _logger;
    private readonly IMemoryCache _cache;

    public PlnPendientesService(
        IConfiguration       configuration,
        IHttpContextAccessor httpContextAccessor,
        ILogger<PlnPendientesService> logger,
        IMemoryCache cache)
        : base(configuration, httpContextAccessor)
    {
        _logger = logger;
        _cache  = cache;
    }

    private async Task<List<T>> GetCachedAsync<T>(string cacheKey, Func<Task<IEnumerable<T>>> factory)
    {
        if (_cache.TryGetValue(cacheKey, out List<T>? cached) && cached is not null)
            return cached;

        var list = (await factory()).ToList();
        _cache.Set(cacheKey, list, CacheTtl);
        return list;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────
    private static string     Str(object? v) =>
        v == null || v == DBNull.Value ? "" : v.ToString()?.Trim() ?? "";

    /// <summary>
    /// Corrige textos con caracteres especiales mal interpretados (mojibake) que llegan
    /// desde Oracle cuando el charset del cliente no coincide con el de la BD
    /// (ej: "TintorerÃ­a" en vez de "Tintorería"). Reinterpreta los bytes como UTF-8 real;
    /// si el resultado no es válido, se devuelve el texto original sin cambios.
    /// </summary>
    private static string FixEncoding(string v)
    {
        if (string.IsNullOrEmpty(v)) return v;
        try
        {
            var bytes = System.Text.Encoding.Latin1.GetBytes(v);
            var fixedValue = System.Text.Encoding.UTF8.GetString(bytes);
            return fixedValue.Contains('\uFFFD') ? v : fixedValue;
        }
        catch
        {
            return v;
        }
    }

    private static decimal    Dec(object? v) =>
        v == null || v == DBNull.Value ? 0m : Convert.ToDecimal(v);

    private static DateTime?  Dat(object? v) =>
        v == null || v == DBNull.Value ? null : Convert.ToDateTime(v);

    private OracleCommand BuildSpCmd(OracleConnection conn, string spName,
        string tipo, string asesor, string cliente, string? rmc = null)
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
        if (rmc != null)
            cmd.Parameters.Add("p_rmc", OracleDbType.Varchar2).Value =
                string.IsNullOrWhiteSpace(rmc) ? "%" : rmc;
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
            list.Add(new PlnFiltroTipo { Tipo = Str(r["TIPO"]), Descripcion = FixEncoding(Str(r["DESCRIPCION"])) });
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
                ColorDet   = Str(r["COLOR_DET_07"]),
                CantidadPedido = Dec(r["CANTIDAD_PEDIDO_07"]),
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
        catch (OracleException ex) when (ex.Number == 942)
        {
            _logger.LogWarning("[PlnPendientesService] SP_PLN_OBS_REVISADO o una de sus tablas no existe en el esquema {Esquema}.", S);
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
                ColorTecnico = Str(r["COLOR_TECNICO_03"]),
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
        string tipo = "%", string asesor = "%", string cliente = "%", string rmc = "%")
    {
        // El controlador siempre dispara, ademas de la consulta filtrada, una consulta
        // "universo" (tipo=asesor=cliente=rmc="%") solo para obtener los CodVende/CodCliente
        // distintos usados en los combos. Esa combinacion es identica para todos los
        // usuarios, asi que se cachea brevemente para no duplicar el costo del SP.
        if (tipo == "%" && asesor == "%" && cliente == "%" && rmc == "%")
            return await GetCachedAsync(
                $"PlnPendEnconado:Universo:{S}",
                () => ExecuteGetPendientesEnconadoAsync(tipo, asesor, cliente, rmc));

        return await ExecuteGetPendientesEnconadoAsync(tipo, asesor, cliente, rmc);
    }

    private async Task<IEnumerable<PlnPendienteEnconado>> ExecuteGetPendientesEnconadoAsync(
        string tipo, string asesor, string cliente, string rmc)
    {
        await using var conn = await AbrirConexionAsync();
        await using var cmd  = BuildSpCmd(conn, "SP_PLN_PEND_ENCONADO", tipo, asesor, cliente, rmc);

        var list = new List<PlnPendienteEnconado>();
        await using var r = (OracleDataReader)await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new PlnPendienteEnconado
            {
                Partida       = Str(r["PARTIDA"]),
                Material      = Str(r["SOLO_MATERIAL"]),
                ColorTecnico  = Str(r["COLOR_TECNICO"]),
                ColorCli      = Str(r["COLOR_CLI"]),
                Cliente       = FixEncoding(Str(r["DESC_CLIENTE"])),
                Peso          = Dec(r["NETO_GUIA"]),
                CodMaq        = Str(r["COD_MAQ"]),
                FecTenido     = Dat(r["FEC_TENIDO"]),
                FecAprob      = Dat(r["FEC_APROB"]),
                FchEntrega    = Dat(r["FCH_ENTREGA"]),
                Lote          = Str(r["LOTE"]),
                Rmc           = Str(r["RMC"]),
                NroRmc        = Dec(r["NRO_RMC"]),
                Guia          = Dec(r["GUIA"]),
                DescEstEvaluacion = FixEncoding(Str(r["DESC_EST_EVALUACION"])),
                ProdMoulinex  = Str(r["PROD_MOULINEX"]),
                ProdMercerizado = Str(r["PROD_MERCERIZADO"]),
                NumPed        = Dec(r["NUM_PED"]),
                ItemPed       = Dec(r["ITEM_PED"]),
                NroPart       = Dec(r["NROPART"]),
            });
        return list;
    }

    // ── SP_PLN_PEND_ENCONADO_CUADRO1 ─────────────────────────────────────────
    public async Task<IEnumerable<PlnEnconadoCuadro1>> GetEnconadoCuadro1Async(
        string tipo = "%", string asesor = "%", string cliente = "%", string rmc = "%", string estado = "%") =>
        await GetCachedAsync(
            $"PlnPendEnconado:Cuadro1:{S}{tipo}:{asesor}:{cliente}:{rmc}:{estado}",
            () => ExecuteGetEnconadoCuadro1Async(tipo, asesor, cliente, rmc, estado));

    private async Task<IEnumerable<PlnEnconadoCuadro1>> ExecuteGetEnconadoCuadro1Async(
        string tipo, string asesor, string cliente, string rmc, string estado)
    {
        await using var conn = await AbrirConexionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText    = $"{S}PKG_PLN.SP_PLN_PEND_ENCONADO_CUADRO1";
        cmd.CommandType    = CommandType.StoredProcedure;
        cmd.BindByName     = true;
        cmd.CommandTimeout = TimeoutSeconds;
        cmd.Parameters.Add("p_tipo",    OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(tipo)    ? "%" : tipo;
        cmd.Parameters.Add("p_asesor",  OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(asesor)  ? "%" : asesor;
        cmd.Parameters.Add("p_cliente", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(cliente) ? "%" : cliente;
        cmd.Parameters.Add("p_rmc",     OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(rmc)     ? "%" : rmc;
        cmd.Parameters.Add("p_estado",  OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(estado)  ? "%" : estado;
        var pCursor = cmd.Parameters.Add("p_cursor", OracleDbType.RefCursor);
        pCursor.Direction = ParameterDirection.Output;

        var list = new List<PlnEnconadoCuadro1>();
        await using var r = (OracleDataReader)await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new PlnEnconadoCuadro1
            {
                Orden    = Str(r["ORDEN_02"]),
                Texto    = FixEncoding(Str(r["TEXTO_02"])),
                PesoKg   = Dec(r["PESO_KG_02"]),
                Cantidad = Dec(r["CANT_02"]),
            });
        return list;
    }

    // ── SP_PLN_PEND_ENCONADO_CUADRO2 ─────────────────────────────────────────
    public async Task<IEnumerable<PlnEnconadoCuadro2>> GetEnconadoCuadro2Async(
        string tipo = "%", string asesor = "%", string cliente = "%", string rmc = "%", string estado = "%") =>
        await GetCachedAsync(
            $"PlnPendEnconado:Cuadro2:{S}{tipo}:{asesor}:{cliente}:{rmc}:{estado}",
            () => ExecuteGetEnconadoCuadro2Async(tipo, asesor, cliente, rmc, estado));

    private async Task<IEnumerable<PlnEnconadoCuadro2>> ExecuteGetEnconadoCuadro2Async(
        string tipo, string asesor, string cliente, string rmc, string estado)
    {
        await using var conn = await AbrirConexionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText    = $"{S}PKG_PLN.SP_PLN_PEND_ENCONADO_CUADRO2";
        cmd.CommandType    = CommandType.StoredProcedure;
        cmd.BindByName     = true;
        cmd.CommandTimeout = TimeoutSeconds;
        cmd.Parameters.Add("p_tipo",    OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(tipo)    ? "%" : tipo;
        cmd.Parameters.Add("p_asesor",  OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(asesor)  ? "%" : asesor;
        cmd.Parameters.Add("p_cliente", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(cliente) ? "%" : cliente;
        cmd.Parameters.Add("p_rmc",     OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(rmc)     ? "%" : rmc;
        cmd.Parameters.Add("p_estado",  OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(estado)  ? "%" : estado;
        var pCursor = cmd.Parameters.Add("p_cursor", OracleDbType.RefCursor);
        pCursor.Direction = ParameterDirection.Output;

        var list = new List<PlnEnconadoCuadro2>();
        await using var r = (OracleDataReader)await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new PlnEnconadoCuadro2
            {
                Estatus  = FixEncoding(Str(r["ESTATUS_03"])),
                Cantidad = Dec(r["PARTIDA_03"]),
                Kg       = Dec(r["KG_03"]),
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
                EstReceta     = Str(r["EST_RECETA"]),
                AlmIntermedio = Str(r["ALM_INTERMEDIOS"]),
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
                ColorTecnico = Str(r["COLOR_TECNICO_01"]),
                Cliente    = Str(r["DESC_CLIENTE_01"]),
                CodCliente = Str(r["COD_CLIENTE_01"]),
                CodVende   = Str(r["COD_VENDE_01"]),
                Fecha      = Dat(r["FECHA_01"]),
                CodMaq     = Str(r["COD_MAQ_01"]),
                Maquina    = Str(r["DESC_MAQ_01"]),
                Proceso    = Str(r["PROCESO_01"]),
                NroRmc     = Dec(r["NRO_RMC_01"]),
                Rmc        = Str(r["RMC"]),
                Peso       = Dec(r["PESO_PARTIDA_01"]),
                Lote       = Str(r["LOTE_01"]),
                ColoSer    = Str(r["COLO_SER_01"]),
                FchEntrega = Dat(r["FCH_ENTREGA_01"]),
            });
        return list;
    }

    // ── SP_PLN_EN_SECADO ──────────────────────────────────────────────────────────────────
    public async Task<IEnumerable<PlnEnSecado>> GetEnSecadoAsync(
        string tipo = "%", string asesor = "%", string cliente = "%")
    {
        await using var conn = await AbrirConexionAsync();
        await using var cmd  = BuildSpCmd(conn, "SP_PLN_EN_SECADO", tipo, asesor, cliente);

        var list = new List<PlnEnSecado>();
        await using var r = (OracleDataReader)await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new PlnEnSecado
            {
                Partida      = Str(r["PARTIDA_02"]),
                Material     = Str(r["MATERIAL_02"]),
                ColorTecnico = Str(r["COLOR_TECNICO_02"]),
                Cliente      = Str(r["DESC_CLIENTE_02"]),
                CodCliente   = Str(r["COD_CLIENTE_02"]),
                CodVende     = Str(r["COD_VENDE_02"]),
                FechaIni     = Dat(r["FECHA_INI_02"]),
                CodMaq       = Str(r["COD_MAQ_02"]),
                Maquina      = Str(r["DESC_MAQ_02"]),
                NroRmc       = Dec(r["NRO_RMC_02"]),
                Rmc          = Str(r["RMC"]),
                Peso         = Dec(r["PESO_PARTIDA_02"]),
                Lote         = Str(r["LOTE_02"]),
                ColoSer      = Str(r["COLO_SER_02"]),
                FchEntrega   = Dat(r["FCH_ENTREGA_02"]),
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
                ColorTecnico = Str(r["COLOR_TECNICO_000"]),
                Cliente    = Str(r["DESC_CLIENTE_000"]),
                CodCliente = Str(r["COD_CLIENTE_000"]),
                CodVende   = Str(r["COD_VENDE_000"]),
                FchProg    = Dat(r["FCH_PROG_000"]),
                Maquina    = Str(r["DESC_MAQUINA_000"]),
                CodMaq     = Str(r["COD_MAQ_000"]),
                Rmc        = Str(r["RMC_000"]),
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
                ColorTecnico   = Str(r["COLOR_TECNICO_01"]),
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
                    ColorTecnico = Str(r["COLOR_TECNICO"]),
                    DescCliente  = Str(r["DESC_CLIENTE"]),
                    DescLabo     = Str(r["DESC_LABO"]),
                    MaqProd      = Str(r["MAQ_PROD"]),
                    DescMaqProd  = Str(r["DESC_MAQ_PROD"]),
                    FentregaPed  = Dat(r["FENTREGA_PED"]),
                    Estado       = Str(r["ESTADO"]),
                });
            return list;
        }
        catch (OracleException ex) when (ex.Number == 942)
        {
            _logger.LogWarning("[PlnPendientesService] SP_PLN_RECT_RECETA o una de sus tablas no existe en el esquema {Esquema}.", S);
            return Enumerable.Empty<PlnRectificacionReceta>();
        }
    }
}
