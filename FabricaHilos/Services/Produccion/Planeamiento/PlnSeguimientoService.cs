using System.Data;
using Microsoft.Extensions.Caching.Memory;
using Oracle.ManagedDataAccess.Client;
using FabricaHilos.Models.Produccion.Planeamiento;

namespace FabricaHilos.Services.Produccion.Planeamiento;

public class PlnSeguimientoService : OracleServiceBase, IPlnSeguimientoService
{
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan EstadosCacheTtl = TimeSpan.FromMinutes(30);
    private const string EstadosCacheKey = "PlnEstadoCodigo";

    public PlnSeguimientoService(
        IConfiguration       configuration,
        IHttpContextAccessor httpContextAccessor,
        IMemoryCache         cache)
        : base(configuration, httpContextAccessor)
    {
        _cache = cache;
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static DateTime? SafeDate(object? val) =>
        val == null || val == DBNull.Value ? null : Convert.ToDateTime(val);

    private static string SafeStr(object? val) =>
        val == null || val == DBNull.Value ? "" : val.ToString()!;

    private static T SafeVal<T>(object? val, T def = default!) =>
        val == null || val == DBNull.Value ? def : (T)Convert.ChangeType(val, typeof(T));

    // ── V_PLN_ESTADO_ITEM — vista autorizada por PKG_PLN §8.2 ──────────────────
    // Reemplaza el SQL ad-hoc SeguimientoSelect. Alias clave:
    //   kg_pedido → CantidadOrig  |  nom_cliente → NombreCliente
    //   estado_seguim → Estado    |  situ_part disponible (ignorado aquí)
    // Nota: la vista NO incluye fechas estimadas/reales completas; GetPorPedidoAsync
    // hace JOIN adicional con PLN_SEGUIMIENTO para obtenerlas.

    // Mapper con ordinals pre-calculados (para bucles de múltiples filas)
    private static PlnSeguimiento MapSeguimientoOrd(
        OracleDataReader r,
        int oIdSeguim, int oSerie, int oNumPed, int oNro, int oNumDet,
        int oCodCliente, int oNombreCliente, int oCodArt, int oColor, int oTitulo,
        int oProceso, int oCantidadOrig, int oSoloDespacho,
        int oCodPasoAct, int oNombrePaso, int oColorUi, int oNroCiclo,
        int oFchPedido, int oFchEntregaComp,
        int oFchEstHilanderia, int oFchEstPartida, int oFchEstTinIni, int oFchEstTinFin,
        int oFchEstSecado, int oFchEstCalidad, int oFchEstDespacho,
        int oFchRealProgramado, int oFchRealProduccion, int oFchRealPartida,
        int oFchRealTinIni, int oFchRealTinFin, int oFchRealSecado,
        int oFchRealCcTinto, int oFchRealCcRechazo, int oFchRealDevanado,
        int oFchRealCalidad, int oFchRealAlmPt, int oFchRealDespacho,
        int oKgProducidos, int oKgEnTin, int oKgEnAlmPt, int oKgDespachados, int oKgPendientes,
        int oIndRetraso, int oDiasRetraso, int oIndUrgente, int oIndReproceso, int oEstado)
        => new()
        {
            IdSeguim          = SafeVal<long>(r[oIdSeguim]),
            Serie             = SafeVal<int>(r[oSerie]),
            NumPed            = SafeVal<long>(r[oNumPed]),
            Nro               = SafeVal<int>(r[oNro]),
            NumDet            = SafeVal<int>(r[oNumDet]),
            CodCliente        = SafeStr(r[oCodCliente]),
            NombreCliente     = SafeStr(r[oNombreCliente]),
            CodArt            = SafeStr(r[oCodArt]),
            Color             = SafeStr(r[oColor]),
            Titulo            = SafeStr(r[oTitulo]),
            Proceso           = SafeStr(r[oProceso]),
            CantidadOrig      = SafeVal<decimal>(r[oCantidadOrig]),
            SoloDespacho      = SafeStr(r[oSoloDespacho]),
            CodPasoAct        = SafeStr(r[oCodPasoAct]),
            NombrePaso        = SafeStr(r[oNombrePaso]),
            ColorUi           = SafeStr(r[oColorUi]),
            NroCiclo          = SafeVal<int>(r[oNroCiclo]),
            FchPedido         = SafeVal<DateTime>(r[oFchPedido]),
            FchEntregaComp    = SafeDate(r[oFchEntregaComp]),
            FchEstHilanderia  = SafeDate(r[oFchEstHilanderia]),
            FchEstPartida     = SafeDate(r[oFchEstPartida]),
            FchEstTinIni      = SafeDate(r[oFchEstTinIni]),
            FchEstTinFin      = SafeDate(r[oFchEstTinFin]),
            FchEstSecado      = SafeDate(r[oFchEstSecado]),
            FchEstCalidad     = SafeDate(r[oFchEstCalidad]),
            FchEstDespacho    = SafeDate(r[oFchEstDespacho]),
            FchRealProgramado = SafeDate(r[oFchRealProgramado]),
            FchRealProduccion = SafeDate(r[oFchRealProduccion]),
            FchRealPartida    = SafeDate(r[oFchRealPartida]),
            FchRealTinIni     = SafeDate(r[oFchRealTinIni]),
            FchRealTinFin     = SafeDate(r[oFchRealTinFin]),
            FchRealSecado     = SafeDate(r[oFchRealSecado]),
            FchRealCcTinto    = SafeDate(r[oFchRealCcTinto]),
            FchRealCcRechazo  = SafeDate(r[oFchRealCcRechazo]),
            FchRealDevanado   = SafeDate(r[oFchRealDevanado]),
            FchRealCalidad    = SafeDate(r[oFchRealCalidad]),
            FchRealAlmPt      = SafeDate(r[oFchRealAlmPt]),
            FchRealDespacho   = SafeDate(r[oFchRealDespacho]),
            KgProducidos      = SafeVal<decimal>(r[oKgProducidos]),
            KgEnTin           = SafeVal<decimal>(r[oKgEnTin]),
            KgEnAlmPt         = SafeVal<decimal>(r[oKgEnAlmPt]),
            KgDespachados     = SafeVal<decimal>(r[oKgDespachados]),
            KgPendientes      = SafeVal<decimal>(r[oKgPendientes]),
            IndRetraso        = SafeStr(r[oIndRetraso]),
            DiasRetraso       = SafeVal<int>(r[oDiasRetraso]),
            IndUrgente        = SafeStr(r[oIndUrgente]),
            IndReproceso      = SafeStr(r[oIndReproceso]),
            Estado            = SafeStr(r[oEstado]),
        };

    private string EstadoItemSelect => $@"
        SELECT v.id_seguim, v.serie, v.num_ped, v.nro, v.num_det,
               v.cod_cliente, v.nom_cliente   AS NOMBRE_CLIENTE,
               v.cod_art, v.color, v.titulo, v.proceso,
               v.kg_pedido                    AS CANTIDAD_ORIG,
               v.kg_producidos, v.kg_en_tin, v.kg_en_alm_pt,
               v.kg_despachados, v.kg_pendientes,
               v.cod_paso_act, v.nombre_paso, v.color_ui,
               v.nro_ciclo,
               v.fch_pedido, v.fch_entrega_comp,
               v.fch_est_despacho,
               v.fch_real_despacho,
               v.dias_retraso, v.ind_retraso, v.ind_urgente,
               v.ind_reproceso,
               v.estado_seguim                AS ESTADO,
               v.semaforo,
               NULL                           AS SOLO_DESPACHO,
               NULL AS FCH_EST_HILANDERIA, NULL AS FCH_EST_PARTIDA,
               NULL AS FCH_EST_TIN_INI,   NULL AS FCH_EST_TIN_FIN,
               NULL AS FCH_EST_SECADO,    NULL AS FCH_EST_CALIDAD,
               NULL AS FCH_REAL_PROGRAMADO, NULL AS FCH_REAL_PRODUCCION,
               NULL AS FCH_REAL_PARTIDA,
               NULL AS FCH_REAL_TIN_INI,  NULL AS FCH_REAL_TIN_FIN,
               NULL AS FCH_REAL_SECADO,
               NULL AS FCH_REAL_CC_TINTO, NULL AS FCH_REAL_CC_RECHAZO,
               NULL AS FCH_REAL_DEVANADO, NULL AS FCH_REAL_CALIDAD,
               NULL AS FCH_REAL_ALM_PT
        FROM   {S}V_PLN_ESTADO_ITEM v";

    public async Task<IEnumerable<PlnSeguimiento>> GetActivosAsync(
        string? codCliente = null, string? codPaso = null)
    {
        // Usa V_PLN_ESTADO_ITEM (§8.2 PKG_PLN): ya incluye JOINs a CLIENTES,
        // ARTICUL y PLN_ESTADO_CODIGO. Filtro ESTADO='A' equivale a estado_seguim='A'.
        var sql = EstadoItemSelect
            + " WHERE v.estado_seguim = 'A'"
            + (!string.IsNullOrWhiteSpace(codCliente) ? " AND v.cod_cliente = :codCliente" : "")
            + (!string.IsNullOrWhiteSpace(codPaso)    ? " AND v.cod_paso_act = :codPaso"   : "")
            + " ORDER BY v.ind_urgente DESC, v.dias_retraso DESC, v.fch_entrega_comp";

        var list = new List<PlnSeguimiento>();
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        if (!string.IsNullOrWhiteSpace(codCliente))
            cmd.Parameters.Add("codCliente", codCliente);
        if (!string.IsNullOrWhiteSpace(codPaso))
            cmd.Parameters.Add("codPaso", codPaso);

        await using var r = (OracleDataReader)await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return list;

        int oIdSeguim = r.GetOrdinal("ID_SEGUIM"), oSerie = r.GetOrdinal("SERIE"),
            oNumPed = r.GetOrdinal("NUM_PED"), oNro = r.GetOrdinal("NRO"),
            oNumDet = r.GetOrdinal("NUM_DET"), oCodCliente2 = r.GetOrdinal("COD_CLIENTE"),
            oNombreCliente = r.GetOrdinal("NOMBRE_CLIENTE"), oCodArt = r.GetOrdinal("COD_ART"),
            oColor = r.GetOrdinal("COLOR"), oTitulo = r.GetOrdinal("TITULO"),
            oProceso = r.GetOrdinal("PROCESO"), oCantidadOrig = r.GetOrdinal("CANTIDAD_ORIG"),
            oSoloDespacho = r.GetOrdinal("SOLO_DESPACHO"), oCodPasoAct = r.GetOrdinal("COD_PASO_ACT"),
            oNombrePaso = r.GetOrdinal("NOMBRE_PASO"), oColorUi = r.GetOrdinal("COLOR_UI"),
            oNroCiclo = r.GetOrdinal("NRO_CICLO"), oFchPedido = r.GetOrdinal("FCH_PEDIDO"),
            oFchEntregaComp = r.GetOrdinal("FCH_ENTREGA_COMP"),
            oFchEstHilanderia = r.GetOrdinal("FCH_EST_HILANDERIA"),
            oFchEstPartida = r.GetOrdinal("FCH_EST_PARTIDA"),
            oFchEstTinIni = r.GetOrdinal("FCH_EST_TIN_INI"),
            oFchEstTinFin = r.GetOrdinal("FCH_EST_TIN_FIN"),
            oFchEstSecado = r.GetOrdinal("FCH_EST_SECADO"),
            oFchEstCalidad = r.GetOrdinal("FCH_EST_CALIDAD"),
            oFchEstDespacho = r.GetOrdinal("FCH_EST_DESPACHO"),
            oFchRealProgramado = r.GetOrdinal("FCH_REAL_PROGRAMADO"),
            oFchRealProduccion = r.GetOrdinal("FCH_REAL_PRODUCCION"),
            oFchRealPartida = r.GetOrdinal("FCH_REAL_PARTIDA"),
            oFchRealTinIni = r.GetOrdinal("FCH_REAL_TIN_INI"),
            oFchRealTinFin = r.GetOrdinal("FCH_REAL_TIN_FIN"),
            oFchRealSecado = r.GetOrdinal("FCH_REAL_SECADO"),
            oFchRealCcTinto = r.GetOrdinal("FCH_REAL_CC_TINTO"),
            oFchRealCcRechazo = r.GetOrdinal("FCH_REAL_CC_RECHAZO"),
            oFchRealDevanado = r.GetOrdinal("FCH_REAL_DEVANADO"),
            oFchRealCalidad = r.GetOrdinal("FCH_REAL_CALIDAD"),
            oFchRealAlmPt = r.GetOrdinal("FCH_REAL_ALM_PT"),
            oFchRealDespacho = r.GetOrdinal("FCH_REAL_DESPACHO"),
            oKgProducidos = r.GetOrdinal("KG_PRODUCIDOS"),
            oKgEnTin = r.GetOrdinal("KG_EN_TIN"),
            oKgEnAlmPt = r.GetOrdinal("KG_EN_ALM_PT"),
            oKgDespachados = r.GetOrdinal("KG_DESPACHADOS"),
            oKgPendientes = r.GetOrdinal("KG_PENDIENTES"),
            oIndRetraso = r.GetOrdinal("IND_RETRASO"),
            oDiasRetraso = r.GetOrdinal("DIAS_RETRASO"),
            oIndUrgente = r.GetOrdinal("IND_URGENTE"),
            oIndReproceso = r.GetOrdinal("IND_REPROCESO"),
            oEstado = r.GetOrdinal("ESTADO");

        do
        {
            list.Add(MapSeguimientoOrd(r,
                oIdSeguim, oSerie, oNumPed, oNro, oNumDet,
                oCodCliente2, oNombreCliente, oCodArt, oColor, oTitulo,
                oProceso, oCantidadOrig, oSoloDespacho,
                oCodPasoAct, oNombrePaso, oColorUi, oNroCiclo,
                oFchPedido, oFchEntregaComp,
                oFchEstHilanderia, oFchEstPartida, oFchEstTinIni, oFchEstTinFin,
                oFchEstSecado, oFchEstCalidad, oFchEstDespacho,
                oFchRealProgramado, oFchRealProduccion, oFchRealPartida,
                oFchRealTinIni, oFchRealTinFin, oFchRealSecado,
                oFchRealCcTinto, oFchRealCcRechazo, oFchRealDevanado,
                oFchRealCalidad, oFchRealAlmPt, oFchRealDespacho,
                oKgProducidos, oKgEnTin, oKgEnAlmPt, oKgDespachados, oKgPendientes,
                oIndRetraso, oDiasRetraso, oIndUrgente, oIndReproceso, oEstado));
        } while (await r.ReadAsync());

        return list;
    }

    public async Task<IEnumerable<PlnSeguimiento>> GetPorPedidoAsync(long numPed, int serie)
    {
        // V_PLN_TRAZABILIDAD (§8.3): todas las fechas estimadas y reales.
        // JOIN a V_PLN_ESTADO_ITEM (§8.2) para KGs, nombre_paso, color_ui, semáforo, etc.
        // JOIN a PLN_SEGUIMIENTO directamente para campos no expuestos por la trazabilidad
        // (FCH_EST_*, FCH_REAL_CC_*, SOLO_DESPACHO, IND_REPROCESO, NRO_CICLO, IND_URGENTE).
        var sql = $@"
            SELECT s.ID_SEGUIM,
                   s.SERIE, s.NUM_PED, s.NRO, s.NUM_DET,
                   s.COD_CLIENTE,
                   v.NOM_CLIENTE          AS NOMBRE_CLIENTE,
                   s.COD_ART,
                   s.COLOR, s.TITULO, s.PROCESO,
                   s.CANTIDAD_ORIG,
                   s.SOLO_DESPACHO,
                   s.COD_PASO_ACT,
                   v.NOMBRE_PASO, v.COLOR_UI,
                   s.NRO_CICLO,
                   s.FCH_PEDIDO,
                   s.FCH_ENTREGA_COMP,
                   s.FCH_EST_HILANDERIA, s.FCH_EST_PARTIDA,
                   s.FCH_EST_TIN_INI,   s.FCH_EST_TIN_FIN,
                   s.FCH_EST_SECADO,    s.FCH_EST_CALIDAD, s.FCH_EST_DESPACHO,
                   s.FCH_REAL_PROGRAMADO, s.FCH_REAL_PRODUCCION,
                   s.FCH_REAL_PARTIDA,
                   s.FCH_REAL_TIN_INI,  s.FCH_REAL_TIN_FIN,
                   s.FCH_REAL_SECADO,
                   s.FCH_REAL_CC_TINTO, s.FCH_REAL_CC_RECHAZO,
                   s.FCH_REAL_DEVANADO, s.FCH_REAL_CALIDAD,
                   s.FCH_REAL_ALM_PT,   s.FCH_REAL_DESPACHO,
                   s.KG_PRODUCIDOS, s.KG_EN_TIN, s.KG_EN_ALM_PT,
                   s.KG_DESPACHADOS,    s.KG_PENDIENTES,
                   s.IND_RETRASO, s.DIAS_RETRASO,
                   s.IND_URGENTE, s.IND_REPROCESO,
                   s.ESTADO
            FROM   {S}PLN_SEGUIMIENTO s
            JOIN   {S}V_PLN_ESTADO_ITEM v
                ON  v.ID_SEGUIM = s.ID_SEGUIM
            WHERE  s.NUM_PED = :numPed AND s.SERIE = :serie
            ORDER  BY s.NRO, s.NUM_DET";

        var list = new List<PlnSeguimiento>();
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        cmd.Parameters.Add("numPed", numPed);
        cmd.Parameters.Add("serie", serie);

        await using var r = (OracleDataReader)await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return list;

        int oIdSeguim = r.GetOrdinal("ID_SEGUIM"), oSerie = r.GetOrdinal("SERIE"),
            oNumPed = r.GetOrdinal("NUM_PED"), oNro = r.GetOrdinal("NRO"),
            oNumDet = r.GetOrdinal("NUM_DET"), oCodCliente = r.GetOrdinal("COD_CLIENTE"),
            oNombreCliente = r.GetOrdinal("NOMBRE_CLIENTE"), oCodArt = r.GetOrdinal("COD_ART"),
            oColor = r.GetOrdinal("COLOR"), oTitulo = r.GetOrdinal("TITULO"),
            oProceso = r.GetOrdinal("PROCESO"), oCantidadOrig = r.GetOrdinal("CANTIDAD_ORIG"),
            oSoloDespacho = r.GetOrdinal("SOLO_DESPACHO"), oCodPasoAct = r.GetOrdinal("COD_PASO_ACT"),
            oNombrePaso = r.GetOrdinal("NOMBRE_PASO"), oColorUi = r.GetOrdinal("COLOR_UI"),
            oNroCiclo = r.GetOrdinal("NRO_CICLO"), oFchPedido = r.GetOrdinal("FCH_PEDIDO"),
            oFchEntregaComp = r.GetOrdinal("FCH_ENTREGA_COMP"),
            oFchEstHilanderia = r.GetOrdinal("FCH_EST_HILANDERIA"),
            oFchEstPartida = r.GetOrdinal("FCH_EST_PARTIDA"),
            oFchEstTinIni = r.GetOrdinal("FCH_EST_TIN_INI"),
            oFchEstTinFin = r.GetOrdinal("FCH_EST_TIN_FIN"),
            oFchEstSecado = r.GetOrdinal("FCH_EST_SECADO"),
            oFchEstCalidad = r.GetOrdinal("FCH_EST_CALIDAD"),
            oFchEstDespacho = r.GetOrdinal("FCH_EST_DESPACHO"),
            oFchRealProgramado = r.GetOrdinal("FCH_REAL_PROGRAMADO"),
            oFchRealProduccion = r.GetOrdinal("FCH_REAL_PRODUCCION"),
            oFchRealPartida = r.GetOrdinal("FCH_REAL_PARTIDA"),
            oFchRealTinIni = r.GetOrdinal("FCH_REAL_TIN_INI"),
            oFchRealTinFin = r.GetOrdinal("FCH_REAL_TIN_FIN"),
            oFchRealSecado = r.GetOrdinal("FCH_REAL_SECADO"),
            oFchRealCcTinto = r.GetOrdinal("FCH_REAL_CC_TINTO"),
            oFchRealCcRechazo = r.GetOrdinal("FCH_REAL_CC_RECHAZO"),
            oFchRealDevanado = r.GetOrdinal("FCH_REAL_DEVANADO"),
            oFchRealCalidad = r.GetOrdinal("FCH_REAL_CALIDAD"),
            oFchRealAlmPt = r.GetOrdinal("FCH_REAL_ALM_PT"),
            oFchRealDespacho = r.GetOrdinal("FCH_REAL_DESPACHO"),
            oKgProducidos = r.GetOrdinal("KG_PRODUCIDOS"),
            oKgEnTin = r.GetOrdinal("KG_EN_TIN"),
            oKgEnAlmPt = r.GetOrdinal("KG_EN_ALM_PT"),
            oKgDespachados = r.GetOrdinal("KG_DESPACHADOS"),
            oKgPendientes = r.GetOrdinal("KG_PENDIENTES"),
            oIndRetraso = r.GetOrdinal("IND_RETRASO"),
            oDiasRetraso = r.GetOrdinal("DIAS_RETRASO"),
            oIndUrgente = r.GetOrdinal("IND_URGENTE"),
            oIndReproceso = r.GetOrdinal("IND_REPROCESO"),
            oEstado = r.GetOrdinal("ESTADO");

        do
        {
            list.Add(MapSeguimientoOrd(r,
                oIdSeguim, oSerie, oNumPed, oNro, oNumDet,
                oCodCliente, oNombreCliente, oCodArt, oColor, oTitulo,
                oProceso, oCantidadOrig, oSoloDespacho,
                oCodPasoAct, oNombrePaso, oColorUi, oNroCiclo,
                oFchPedido, oFchEntregaComp,
                oFchEstHilanderia, oFchEstPartida, oFchEstTinIni, oFchEstTinFin,
                oFchEstSecado, oFchEstCalidad, oFchEstDespacho,
                oFchRealProgramado, oFchRealProduccion, oFchRealPartida,
                oFchRealTinIni, oFchRealTinFin, oFchRealSecado,
                oFchRealCcTinto, oFchRealCcRechazo, oFchRealDevanado,
                oFchRealCalidad, oFchRealAlmPt, oFchRealDespacho,
                oKgProducidos, oKgEnTin, oKgEnAlmPt, oKgDespachados, oKgPendientes,
                oIndRetraso, oDiasRetraso, oIndUrgente, oIndReproceso, oEstado));
        } while (await r.ReadAsync());

        return list;
    }

    public async Task<IEnumerable<PlnEstadoCodigo>> GetEstadosAsync()
    {
        // B4: GetOrCreateAsync es atómico — evita race condition TryGetValue+Set
        return await _cache.GetOrCreateAsync(EstadosCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = EstadosCacheTtl;

            // ORA-00904: PLN_ESTADO_CODIGO NO tiene columna AREA (§2.2 PKG_PLN.sql).
            // Area se deriva en el modelo (PlnEstadoCodigo.Area) a partir de CodPaso.
            var sql = $@"
                SELECT COD_PASO, NOMBRE_PASO, DESCRIPCION, ORDEN_PASO, COLOR_UI, ES_FINAL
                FROM   {S}PLN_ESTADO_CODIGO
                ORDER  BY ORDEN_PASO";

            var list = new List<PlnEstadoCodigo>();
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn);
            await using var r   = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new PlnEstadoCodigo
                {
                    CodPaso     = SafeStr(r["COD_PASO"]),
                    NombrePaso  = SafeStr(r["NOMBRE_PASO"]),
                    Descripcion = SafeStr(r["DESCRIPCION"]),
                    OrdenPaso   = SafeVal<int>(r["ORDEN_PASO"]),
                    ColorUi     = SafeStr(r["COLOR_UI"]),
                    EsFinal     = SafeStr(r["ES_FINAL"]),
                });
            }
            return (IReadOnlyList<PlnEstadoCodigo>)list.AsReadOnly();
        }) ?? [];
    }

    public async Task<IEnumerable<PlnLogEvento>> GetEventosPorPedidoAsync(long numPed, int serie)
    {
        // PLN_LOG_EVENTOS: tabla de trazabilidad de eventos del paquete (§2.3 PKG_PLN).
        // El JOIN a PLN_ESTADO_CODIGO se elimina: nombre_paso se resuelve en C#
        // usando el caché de GetEstadosAsync (30 min TTL).
        var estados = (await GetEstadosAsync()).ToDictionary(e => e.CodPaso, e => e.NombrePaso);

        var sql = $@"
            SELECT ev.ID_EVENTO, ev.ID_SEGUIM, ev.NUM_PED, ev.SERIE, ev.NRO, ev.NUM_DET,
                   ev.COD_PASO, ev.TIPO_EVENTO, ev.FCH_EVENTO,
                   ev.TABLA_ORIGEN, ev.KG_CANTIDAD, ev.OBSERVACION, ev.USUARIO, ev.NRO_CICLO
            FROM   {S}PLN_LOG_EVENTOS ev
            WHERE  ev.NUM_PED = :numPed AND ev.SERIE = :serie
            ORDER  BY ev.FCH_EVENTO DESC";

        var list = new List<PlnLogEvento>();
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        cmd.Parameters.Add("numPed", numPed);
        cmd.Parameters.Add("serie", serie);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var codPaso = SafeStr(r["COD_PASO"]);
            list.Add(new PlnLogEvento
            {
                IdEvento    = SafeVal<long>(r["ID_EVENTO"]),
                IdSeguim    = SafeVal<long>(r["ID_SEGUIM"]),
                NumPed      = SafeVal<long>(r["NUM_PED"]),
                Serie       = SafeVal<int>(r["SERIE"]),
                Nro         = SafeVal<int>(r["NRO"]),
                NumDet      = SafeVal<int>(r["NUM_DET"]),
                CodPaso     = codPaso,
                NombrePaso  = estados.TryGetValue(codPaso, out var nom) ? nom : codPaso,
                TipoEvento  = SafeStr(r["TIPO_EVENTO"]),
                FchEvento   = SafeVal<DateTime>(r["FCH_EVENTO"]),
                TablaOrigen = SafeStr(r["TABLA_ORIGEN"]),
                KgCantidad  = r["KG_CANTIDAD"] == DBNull.Value ? null : SafeVal<decimal?>(r["KG_CANTIDAD"]),
                Observacion = SafeStr(r["OBSERVACION"]),
                Usuario     = SafeStr(r["USUARIO"]),
                NroCiclo    = SafeVal<int>(r["NRO_CICLO"]),
            });
        }
        return list;
    }

    public async Task<IEnumerable<PlnAlerta>> GetAlertasPorPedidoAsync(long numPed, int serie)
    {
        // V_PLN_ALERTAS_ACTIVAS (§8.4 PKG_PLN): ya incluye nom_cliente y horas_sin_resolver.
        // JOIN a PLN_SEGUIMIENTO para filtrar por num_ped/serie y obtener cod_paso_act/color_ui.
        var sql = $@"
            SELECT a.id_alerta, a.id_alerta AS ID_SEGUIM_NULL,
                   a.tip_alerta, a.nivel, a.titulo, a.detalle,
                   a.fch_alerta, a.fch_limite, a.dias_retraso, a.num_ped, a.nro,
                   a.cod_cliente, a.nom_cliente AS NOMBRE_CLIENTE,
                   a.cod_maq, a.estado,
                   a.horas_sin_resolver * 24 AS horas_sin_resolver,
                   s.serie,
                   s.cod_paso_act,
                   ec.color_ui AS color_ui_paso
            FROM   {S}V_PLN_ALERTAS_ACTIVAS a
            JOIN   {S}PLN_SEGUIMIENTO s
                ON  s.num_ped = a.num_ped AND s.nro = a.nro AND s.serie = :serie
            LEFT   JOIN {S}PLN_ESTADO_CODIGO ec ON ec.cod_paso = s.cod_paso_act
            WHERE  a.num_ped = :numPed
            ORDER  BY DECODE(a.nivel,'C',1,'A',2,'M',3,'B',4), a.fch_limite";

        var list = new List<PlnAlerta>();
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        cmd.Parameters.Add("numPed", numPed);
        cmd.Parameters.Add("serie", serie);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new PlnAlerta
            {
                IdAlerta      = SafeVal<long>(r["id_alerta"]),
                TipAlerta     = SafeStr(r["tip_alerta"]),
                Nivel         = SafeStr(r["nivel"]),
                Titulo        = SafeStr(r["titulo"]),
                Detalle       = SafeStr(r["detalle"]),
                FchAlerta     = SafeVal<DateTime>(r["fch_alerta"]),
                FchLimite     = SafeDate(r["fch_limite"]),
                DiasRetraso   = r["dias_retraso"] == DBNull.Value ? null : SafeVal<int?>(r["dias_retraso"]),
                CodMaq        = SafeStr(r["cod_maq"]),
                Estado        = SafeStr(r["estado"]),
                NumPed        = r["num_ped"] == DBNull.Value ? null : SafeVal<long?>(r["num_ped"]),
                Nro           = r["nro"] == DBNull.Value ? null : SafeVal<int?>(r["nro"]),
                Serie         = r["serie"] == DBNull.Value ? null : SafeVal<int?>(r["serie"]),
                CodCliente    = SafeStr(r["cod_cliente"]),
                NombreCliente = SafeStr(r["NOMBRE_CLIENTE"]),
                CodPasoAct    = SafeStr(r["cod_paso_act"]),
                ColorUiPaso   = SafeStr(r["color_ui_paso"]),
                HorasSinResolver = r["horas_sin_resolver"] == DBNull.Value
                    ? null : SafeVal<double?>(r["horas_sin_resolver"]),
            });
        }
        return list;
    }

    public async Task<IEnumerable<PlnTrazabilidad>> GetTrazabilidadAsync(long numPed, int serie)
    {
        // V_PLN_TRAZABILIDAD (§8.3 PKG_PLN): timeline completo de fechas est. vs. reales.
        var sql = $@"
            SELECT t.num_ped, t.nro, t.num_det,
                   t.cod_cliente, t.cod_art,
                   t.fch_pedido, t.fch_aprob_pedido,
                   t.fch_planeada, t.fch_entrega_plan,
                   t.fch_est_cono1, t.fch_est_tenido,
                   t.fch_real_programado, t.fch_real_produccion,
                   t.fch_real_partida, t.fch_real_tin_ini,
                   t.fch_prog_tin, t.fch_real_tin_fin,
                   t.fch_real_secado, t.fch_real_calidad,
                   t.fch_real_alm_pt, t.fch_real_despacho,
                   t.fch_compromiso_cliente,
                   t.dias_pedido_a_partida, t.dias_en_tintoreria,
                   t.dias_partida_a_almpt,  t.dias_almpt_a_despacho,
                   t.dias_total_ciclo,      t.dias_desvio_cliente,
                   t.cod_paso_act, t.dias_retraso, t.nro_ciclo
            FROM   {S}V_PLN_TRAZABILIDAD t
            WHERE  t.num_ped = :numPed
            ORDER  BY t.nro, t.num_det";

        var list = new List<PlnTrazabilidad>();
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        cmd.Parameters.Add("numPed", numPed);
        cmd.Parameters.Add("serie", serie);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new PlnTrazabilidad
            {
                NumPed                = SafeVal<long>(r["num_ped"]),
                Nro                   = SafeVal<int>(r["nro"]),
                NumDet                = SafeVal<int>(r["num_det"]),
                CodCliente            = SafeStr(r["cod_cliente"]),
                CodArt                = SafeStr(r["cod_art"]),
                FchPedido             = SafeVal<DateTime>(r["fch_pedido"]),
                FchAprobPedido        = SafeDate(r["fch_aprob_pedido"]),
                FchPlaneada           = SafeDate(r["fch_planeada"]),
                FchEntregaPlan        = SafeDate(r["fch_entrega_plan"]),
                FchEstCono1           = SafeDate(r["fch_est_cono1"]),
                FchEstTenido          = SafeDate(r["fch_est_tenido"]),
                FchRealProgramado     = SafeDate(r["fch_real_programado"]),
                FchRealProduccion     = SafeDate(r["fch_real_produccion"]),
                FchRealPartida        = SafeDate(r["fch_real_partida"]),
                FchRealTinIni         = SafeDate(r["fch_real_tin_ini"]),
                FchProgTin            = SafeDate(r["fch_prog_tin"]),
                FchRealTinFin         = SafeDate(r["fch_real_tin_fin"]),
                FchRealSecado         = SafeDate(r["fch_real_secado"]),
                FchRealCalidad        = SafeDate(r["fch_real_calidad"]),
                FchRealAlmPt          = SafeDate(r["fch_real_alm_pt"]),
                FchRealDespacho       = SafeDate(r["fch_real_despacho"]),
                FchCompromisoCliente  = SafeDate(r["fch_compromiso_cliente"]),
                DiasPedidoAPartida    = r["dias_pedido_a_partida"]  == DBNull.Value ? null : SafeVal<double?>(r["dias_pedido_a_partida"]),
                DiasEnTintoreria      = r["dias_en_tintoreria"]     == DBNull.Value ? null : SafeVal<double?>(r["dias_en_tintoreria"]),
                DiasPartidaAAlmPt     = r["dias_partida_a_almpt"]   == DBNull.Value ? null : SafeVal<double?>(r["dias_partida_a_almpt"]),
                DiasAlmPtADespacho    = r["dias_almpt_a_despacho"]  == DBNull.Value ? null : SafeVal<double?>(r["dias_almpt_a_despacho"]),
                DiasTotalCiclo        = r["dias_total_ciclo"]        == DBNull.Value ? null : SafeVal<double?>(r["dias_total_ciclo"]),
                DiasDesvioCliente     = r["dias_desvio_cliente"]     == DBNull.Value ? null : SafeVal<double?>(r["dias_desvio_cliente"]),
                CodPasoAct            = SafeStr(r["cod_paso_act"]),
                DiasRetraso           = SafeVal<int>(r["dias_retraso"]),
                NroCiclo              = SafeVal<int>(r["nro_ciclo"]),
            });
        }
        return list;
    }

    public async Task<IEnumerable<PlnFechaEstimada>> GetFechasEstimadasAsync(long idSeguim)
    {
        // PLN_FECHAS_ESTIMADAS (§2.7 PKG_PLN): historial de recálculos de fechas.
        var sql = $@"
            SELECT f.id_fech, f.id_seguim, f.fch_calculo, f.motivo_recalculo,
                   f.fch_est_hilanderia, f.fch_est_partida,
                   f.fch_est_tin_ini,    f.fch_est_tin_fin,
                   f.fch_est_secado,     f.fch_est_calidad, f.fch_est_despacho,
                   f.difer_dias, f.usuario
            FROM   {S}PLN_FECHAS_ESTIMADAS f
            WHERE  f.id_seguim = :idSeguim
            ORDER  BY f.fch_calculo DESC";

        var list = new List<PlnFechaEstimada>();
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        cmd.Parameters.Add("idSeguim", idSeguim);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new PlnFechaEstimada
            {
                IdFech           = SafeVal<long>(r["id_fech"]),
                IdSeguim         = SafeVal<long>(r["id_seguim"]),
                FchCalculo       = SafeVal<DateTime>(r["fch_calculo"]),
                MotivoRecalculo  = SafeStr(r["motivo_recalculo"]),
                FchEstHilanderia = SafeDate(r["fch_est_hilanderia"]),
                FchEstPartida    = SafeDate(r["fch_est_partida"]),
                FchEstTinIni     = SafeDate(r["fch_est_tin_ini"]),
                FchEstTinFin     = SafeDate(r["fch_est_tin_fin"]),
                FchEstSecado     = SafeDate(r["fch_est_secado"]),
                FchEstCalidad    = SafeDate(r["fch_est_calidad"]),
                FchEstDespacho   = SafeDate(r["fch_est_despacho"]),
                DiferDias        = r["difer_dias"] == DBNull.Value ? null : SafeVal<int?>(r["difer_dias"]),
                Usuario          = SafeStr(r["usuario"]),
            });
        }
        return list;
    }

    // ── Wrappers de procedimientos PKG_PLN ──────────────────────────────────────

    public async Task AvanzaPasoAsync(
        int serie, long numPed, int nro, int numDet,
        string nuevoPaso, string? observacion = null, decimal? kgCantidad = null)
    {
        // PKG_PLN.SP_PLN_AVANZA_PASO (§6 PKG_PLN): correcciones manuales autorizadas.
        // Los triggers de planta llaman al mismo SP de forma automática.
        // COMMIT interno NO debe usarse en trigger (ORA-04092); pero en llamada directa el SP
        // devuelve sin COMMIT, por eso se hace COMMIT explícito desde aquí.
        const string sql = "BEGIN PKG_PLN.SP_PLN_AVANZA_PASO(:serie,:numPed,:nro,:numDet,:paso,'MANUAL',NULL,:kg,:obs); COMMIT; END;";
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        cmd.Parameters.Add("serie",  serie);
        cmd.Parameters.Add("numPed", numPed);
        cmd.Parameters.Add("nro",    nro);
        cmd.Parameters.Add("numDet", numDet);
        cmd.Parameters.Add("paso",   nuevoPaso);
        cmd.Parameters.Add(new OracleParameter("kg",  OracleDbType.Decimal) { Value = kgCantidad.HasValue ? (object)kgCantidad.Value : DBNull.Value });
        cmd.Parameters.Add(new OracleParameter("obs", OracleDbType.Varchar2, 300) { Value = (object?)observacion ?? DBNull.Value });
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task CierreItemAsync(long idSeguim, string motivo, string usuario)
    {
        // PKG_PLN.SP_PLN_CIERRE_ITEM (§6 PKG_PLN): cierre manual de ítem.
        // El SP hace COMMIT internamente.
        const string sql = "BEGIN PKG_PLN.SP_PLN_CIERRE_ITEM(:id,:motivo,:usuario); END;";
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        cmd.Parameters.Add("id",      idSeguim);
        cmd.Parameters.Add(new OracleParameter("motivo",  OracleDbType.Varchar2, 200) { Value = (object)motivo });
        cmd.Parameters.Add(new OracleParameter("usuario", OracleDbType.Varchar2, 15)  { Value = (object)usuario });
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ReprogramarAsync(
        int serie, long numPed, int nro, int numDet,
        DateTime nuevaFchDesp, string motivo, string usuario)
    {
        // PKG_PLN.SP_PLN_REPROGRAMAR (§6 PKG_PLN): nueva FCH_EST_DESPACHO.
        // El SP hace COMMIT internamente.
        const string sql =
            "BEGIN PKG_PLN.SP_PLN_REPROGRAMAR(:serie,:numPed,:nro,:numDet,:fch,:motivo,:usuario); END;";
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        cmd.Parameters.Add("serie",   serie);
        cmd.Parameters.Add("numPed",  numPed);
        cmd.Parameters.Add("nro",     nro);
        cmd.Parameters.Add("numDet",  numDet);
        cmd.Parameters.Add(new OracleParameter("fch",     OracleDbType.Date) { Value = nuevaFchDesp });
        cmd.Parameters.Add(new OracleParameter("motivo",  OracleDbType.Varchar2, 200) { Value = (object)motivo });
        cmd.Parameters.Add(new OracleParameter("usuario", OracleDbType.Varchar2, 15)  { Value = (object)usuario });
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task InitSeguimientoAsync(int serie, long numPed, int nro, int numDet = 0,
                                             string pasoIni = "01")
    {
        // PKG_PLN.SP_PLN_INIT_SEGUIMIENTO (§6 PKG_PLN): crea la fila inicial en PLN_SEGUIMIENTO.
        // Idempotente: DUP_VAL_ON_INDEX es ignorado silenciosamente por el paquete.
        // Uso directo desde C# (correcciones manuales): el SP no hace COMMIT en trigger,
        // por eso se añade COMMIT explícito aquí.
        const string sql =
            "BEGIN PKG_PLN.SP_PLN_INIT_SEGUIMIENTO(:serie,:numPed,:nro,:numDet,:pasoIni); COMMIT; END;";
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        cmd.Parameters.Add("serie",   serie);
        cmd.Parameters.Add("numPed",  numPed);
        cmd.Parameters.Add("nro",     nro);
        cmd.Parameters.Add("numDet",  numDet);
        cmd.Parameters.Add(new OracleParameter("pasoIni", OracleDbType.Varchar2, 2) { Value = (object)pasoIni });
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task CalcularFechasAsync(int serie, long numPed, int nro, int numDet, string motivo)
    {
        // PKG_PLN.SP_PLN_CALCULA_FECHAS (§6 PKG_PLN): recalcula todas las fechas estimadas del ítem.
        // Motivos válidos: 'PED' / 'PLA' / 'REP' / 'MAQ'
        // El SP guarda historial en PLN_FECHAS_ESTIMADAS y sincroniza ITEMPED_DET.
        const string sql =
            "BEGIN PKG_PLN.SP_PLN_CALCULA_FECHAS(:serie,:numPed,:nro,:numDet,:motivo); END;";
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        cmd.Parameters.Add("serie",   serie);
        cmd.Parameters.Add("numPed",  numPed);
        cmd.Parameters.Add("nro",     nro);
        cmd.Parameters.Add("numDet",  numDet);
        cmd.Parameters.Add(new OracleParameter("motivo", OracleDbType.Varchar2, 3) { Value = (object)motivo });
        await cmd.ExecuteNonQueryAsync();
    }
}
