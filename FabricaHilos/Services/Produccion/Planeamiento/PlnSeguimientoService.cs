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

    private static T SafeVal<T>(object? val, T def = default!)
    {
        if (val == null || val == DBNull.Value) return def;
        var underlying = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        return (T)Convert.ChangeType(val, underlying);
    }

    // Oracle NUMBER puede desbordarse al convertirlo a decimal .NET con GetValue().
    // Este helper usa GetOracleDecimal para evitar el OverflowException y devuelve double?.
    private static double? SafeOracleDouble(Oracle.ManagedDataAccess.Client.OracleDataReader r, int ordinal)
    {
        if (r.IsDBNull(ordinal)) return null;
        try
        {
            var od = r.GetOracleDecimal(ordinal);
            od = Oracle.ManagedDataAccess.Types.OracleDecimal.SetPrecision(od, 15);
            return (double)od;
        }
        catch
        {
            return null;
        }
    }

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
        int oIndRetraso, int oDiasRetraso, int oIndUrgente, int oIndReproceso, int oEstado,
        int oFchRealGaseado = -1,   // v2.1: opcional — solo '09B' PROCESO='24'
        int oMaqTt = -1, int oMaqProgramada = -1, int oMaqPartida = -1,   // v2.2: campos máquina
        int oMaqRealTt = -1,                                               // v2.3: TT_RPRODUC real-time
        int oNumPartida = -1,                                              // v2.4: PARTIDA.NUMERO
        int oNumPartidaAnt = -1,                                          // v2.7: ciclo anterior (reproceso)
        int oMaqSecado = -1, int oMaqDevan = -1,                          // v2.5: secado/devanado
        int oFchRegEntrega2 = -1, int oFchEntregaOri2 = -1,                 // v2.3: fechas compromiso por artículo
        int oFchAprobacion = -1, int oFchPlanif = -1,                      // v2.6: fechas actor
        int oUsrRegistro = -1, int oNombreRegistro = -1,                   // v2.6: quien registró
        int oUsrAprobacion = -1, int oNombreAprobacion = -1,               // v2.6: quien aprobó
        int oUsrPlanif = -1, int oNombrePlanif = -1,                       // v2.6: planificador
        int oIndFlujo = -1)                                                    // v2.3: flujo dual Lab/Hilandería
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
            FchRealGaseado    = oFchRealGaseado >= 0 ? SafeDate(r[oFchRealGaseado]) : null,
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
            CodMaqTt          = oMaqTt         >= 0 ? SafeStr(r[oMaqTt])         : null,
            CodMaqSecado      = oMaqSecado     >= 0 ? SafeStr(r[oMaqSecado])     : null,
            CodMaqDevan       = oMaqDevan      >= 0 ? SafeStr(r[oMaqDevan])      : null,
            MaqProgramada     = oMaqProgramada >= 0 ? SafeStr(r[oMaqProgramada]) : null,
            MaqPartida        = oMaqPartida    >= 0 ? SafeStr(r[oMaqPartida])    : null,
            MaqRealTt         = oMaqRealTt     >= 0 ? SafeStr(r[oMaqRealTt])     : null,
            NumPartida        = oNumPartida    >= 0 ? SafeVal<long>(r[oNumPartida]) : 0,
            NumPartidaAnt     = oNumPartidaAnt >= 0 ? SafeVal<long>(r[oNumPartidaAnt]) : 0,
            FchRegEntrega     = oFchRegEntrega2   >= 0 ? SafeDate(r[oFchRegEntrega2])   : null,
            FchEntregaOri     = oFchEntregaOri2   >= 0 ? SafeDate(r[oFchEntregaOri2])   : null,
            FchAprobacion     = oFchAprobacion    >= 0 ? SafeDate(r[oFchAprobacion])    : null,
            FchPlanif         = oFchPlanif        >= 0 ? SafeDate(r[oFchPlanif])        : null,
            UsrRegistro       = oUsrRegistro      >= 0 ? SafeStr(r[oUsrRegistro])       : null,
            NombreRegistro    = oNombreRegistro   >= 0 ? SafeStr(r[oNombreRegistro])    : null,
            UsrAprobacion     = oUsrAprobacion    >= 0 ? SafeStr(r[oUsrAprobacion])     : null,
            NombreAprobacion  = oNombreAprobacion >= 0 ? SafeStr(r[oNombreAprobacion])  : null,
            UsrPlanif         = oUsrPlanif        >= 0 ? SafeStr(r[oUsrPlanif])         : null,
            NombrePlanif      = oNombrePlanif     >= 0 ? SafeStr(r[oNombrePlanif])      : null,
            IndFlujo          = oIndFlujo         >= 0 ? SafeStr(r[oIndFlujo])          : "L",
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
               NULL AS FCH_REAL_ALM_PT,
               NULL AS FCH_REAL_GASEADO
        FROM   {S}V_PLN_ESTADO_ITEM v";

    public async Task<IEnumerable<PlnSeguimiento>> GetActivosAsync(
        string? busquedaCliente = null, string? codPaso = null, string? numPed = null, bool incluyeCerrados = false)
    {
        // Usa V_PLN_ESTADO_ITEM (§8.2 PKG_PLN): ya incluye JOINs a CLIENTES,
        // ARTICUL y PLN_ESTADO_CODIGO. Filtro ESTADO='A' equivale a estado_seguim='A'.
        var sql = EstadoItemSelect
            + (incluyeCerrados ? " WHERE v.estado_seguim IN ('A','C')" : " WHERE v.estado_seguim = 'A'")
            + (!string.IsNullOrWhiteSpace(busquedaCliente)
                ? " AND (UPPER(v.nom_cliente) LIKE UPPER('%'||:busquedaCliente||'%') OR UPPER(v.cod_cliente) LIKE UPPER('%'||:busquedaCliente||'%'))"
                : "")
            + (!string.IsNullOrWhiteSpace(codPaso)         ? " AND v.cod_paso_act = :codPaso"   : "")
            + (!string.IsNullOrWhiteSpace(numPed)          ? " AND v.num_ped = :numPed"          : "")
            + " ORDER BY v.ind_urgente DESC, v.dias_retraso DESC, v.fch_entrega_comp";

        var list = new List<PlnSeguimiento>();
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        if (!string.IsNullOrWhiteSpace(busquedaCliente))
            cmd.Parameters.Add("busquedaCliente", busquedaCliente);
        if (!string.IsNullOrWhiteSpace(codPaso))
            cmd.Parameters.Add("codPaso", codPaso);
        if (!string.IsNullOrWhiteSpace(numPed))
            cmd.Parameters.Add("numPed", numPed);

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

    public async Task<PlnSeguimientoPagina> GetActivosPaginadoAsync(
        string? busquedaCliente = null,
        string? codPaso         = null,
        string? numPed          = null,
        bool    incluyeCerrados = false,
        int     pagina          = 1,
        int     tamPagina       = 10)
    {
        if (pagina < 1) pagina = 1;
        if (tamPagina < 1) tamPagina = 10;

        // ── Cláusula WHERE compartida ─────────────────────────────────────────
        var whereClause =
              (incluyeCerrados ? " WHERE v.estado_seguim IN ('A','C')" : " WHERE v.estado_seguim = 'A'")
            + (!string.IsNullOrWhiteSpace(busquedaCliente)
                ? " AND (UPPER(v.nom_cliente) LIKE UPPER('%'||:busquedaCliente||'%') OR UPPER(v.cod_cliente) LIKE UPPER('%'||:busquedaCliente||'%'))"
                : "")
            + (!string.IsNullOrWhiteSpace(codPaso) ? " AND v.cod_paso_act = :codPaso" : "")
            + (!string.IsNullOrWhiteSpace(numPed)  ? " AND v.num_ped = :numPed"  : "");

        // ── 1. Totales globales (KPIs + total pedidos para paginación) ─────────
        // Se cuenta a nivel de ítem para los KPIs y a nivel de pedido para la paginación.
        var sqlCount = $@"
            SELECT
                COUNT(*)                                          AS total_items,
                SUM(CASE WHEN v.ind_retraso  = 'S' THEN 1 ELSE 0 END) AS total_retraso,
                SUM(CASE WHEN v.ind_urgente  = 'S' THEN 1 ELSE 0 END) AS total_urgente,
                SUM(CASE WHEN v.ind_reproceso= 'S' THEN 1 ELSE 0 END) AS total_reproceso,
                SUM(CASE WHEN v.cod_paso_act = '01' THEN 1 ELSE 0 END) AS total_sin_planif,
                COUNT(DISTINCT v.serie || '|' || v.num_ped)      AS total_pedidos
            FROM {S}V_PLN_ESTADO_ITEM v
            {whereClause}";

        int totalItems = 0, totalRetraso = 0, totalUrgente = 0,
            totalReproceso = 0, totalSinPlanif = 0, totalPedidos = 0;

        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();

        await using (var cmdCount = new OracleCommand(sqlCount, conn))
        {
            cmdCount.BindByName = true;
            if (!string.IsNullOrWhiteSpace(busquedaCliente)) cmdCount.Parameters.Add("busquedaCliente", busquedaCliente);
            if (!string.IsNullOrWhiteSpace(codPaso))         cmdCount.Parameters.Add("codPaso", codPaso);
            if (!string.IsNullOrWhiteSpace(numPed))          cmdCount.Parameters.Add("numPed", numPed);

            await using var rc = (OracleDataReader)await cmdCount.ExecuteReaderAsync();
            if (await rc.ReadAsync())
            {
                totalItems      = rc.IsDBNull(0) ? 0 : Convert.ToInt32(rc[0]);
                totalRetraso    = rc.IsDBNull(1) ? 0 : Convert.ToInt32(rc[1]);
                totalUrgente    = rc.IsDBNull(2) ? 0 : Convert.ToInt32(rc[2]);
                totalReproceso  = rc.IsDBNull(3) ? 0 : Convert.ToInt32(rc[3]);
                totalSinPlanif  = rc.IsDBNull(4) ? 0 : Convert.ToInt32(rc[4]);
                totalPedidos    = rc.IsDBNull(5) ? 0 : Convert.ToInt32(rc[5]);
            }
        }

        // ── 2. Pedidos de la página (ROWNUM sobre lista ordenada de pedidos) ──
        // Oracle 10g no soporta OFFSET/FETCH; se usa la técnica clásica de doble ROWNUM.
        // Primero se obtiene la lista ordenada de pares (SERIE, NUM_PED) con rownum,
        // luego se hace JOIN con los ítems de esos pedidos.
        int filaDesde = (pagina - 1) * tamPagina + 1;
        int filaHasta = pagina * tamPagina;

        var sqlPage = $@"
            SELECT t.*
            FROM (
                SELECT t2.*, ROWNUM AS rn
                FROM (
                    SELECT DISTINCT v.serie, v.num_ped
                    FROM   {S}V_PLN_ESTADO_ITEM v
                    {whereClause}
                    ORDER BY v.serie DESC, v.num_ped DESC
                ) t2
                WHERE ROWNUM <= :filaHasta
            ) t
            WHERE t.rn >= :filaDesde";

        var pedidosPagina = new List<(int Serie, long NumPed)>();
        await using (var cmdPed = new OracleCommand(sqlPage, conn))
        {
            cmdPed.BindByName = true;
            if (!string.IsNullOrWhiteSpace(busquedaCliente)) cmdPed.Parameters.Add("busquedaCliente", busquedaCliente);
            if (!string.IsNullOrWhiteSpace(codPaso))         cmdPed.Parameters.Add("codPaso", codPaso);
            if (!string.IsNullOrWhiteSpace(numPed))          cmdPed.Parameters.Add("numPed", numPed);
            cmdPed.Parameters.Add("filaHasta", filaHasta);
            cmdPed.Parameters.Add("filaDesde", filaDesde);

            await using var rp = (OracleDataReader)await cmdPed.ExecuteReaderAsync();
            while (await rp.ReadAsync())
                pedidosPagina.Add((Convert.ToInt32(rp["SERIE"]), Convert.ToInt64(rp["NUM_PED"])));
        }

        if (pedidosPagina.Count == 0)
        {
            return new PlnSeguimientoPagina
            {
                Items          = [],
                TotalItems     = totalItems,
                TotalRetrasados= totalRetraso,
                TotalUrgentes  = totalUrgente,
                TotalReprocesos= totalReproceso,
                TotalSinPlanif = totalSinPlanif,
                TotalPedidos   = totalPedidos,
                Pagina         = pagina,
                TamPagina      = tamPagina,
            };
        }

        // ── 3. Ítems de los pedidos de la página ──────────────────────────────
        // Se construye IN (...) con los pares SERIE+NUM_PED de la página.
        var inPairs = string.Join(" OR ", pedidosPagina.Select((p, i) =>
            $"(v.serie = :pSerie{i} AND v.num_ped = :pNumPed{i})"));

        // Siempre recuperar todos los sub-lotes del pedido (A+C) para mostrar la imagen completa
        // en el sub-table del Dashboard. La selección de PEDIDOS en la página sí filtra por 'A'.
        var sqlItems = EstadoItemSelect
            + " WHERE v.estado_seguim IN ('A','C')"
            + $" AND ({inPairs})"
            + " ORDER BY v.ind_urgente DESC, v.dias_retraso DESC, v.num_ped DESC, v.nro";

        var list = new List<PlnSeguimiento>();
        await using (var cmdItems = new OracleCommand(sqlItems, conn))
        {
            cmdItems.BindByName = true;
            for (int i = 0; i < pedidosPagina.Count; i++)
            {
                cmdItems.Parameters.Add($"pSerie{i}",  pedidosPagina[i].Serie);
                cmdItems.Parameters.Add($"pNumPed{i}", pedidosPagina[i].NumPed);
            }

            await using var r = (OracleDataReader)await cmdItems.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
            int oIdSeguim = r.GetOrdinal("ID_SEGUIM"), oSerie = r.GetOrdinal("SERIE"),
                oNumPed2 = r.GetOrdinal("NUM_PED"), oNro = r.GetOrdinal("NRO"),
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
                    oIdSeguim, oSerie, oNumPed2, oNro, oNumDet,
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
            } // end if (await r.ReadAsync())
        }

        return new PlnSeguimientoPagina
        {
            Items          = list,
            TotalItems     = totalItems,
            TotalRetrasados= totalRetraso,
            TotalUrgentes  = totalUrgente,
            TotalReprocesos= totalReproceso,
            TotalSinPlanif = totalSinPlanif,
            TotalPedidos   = totalPedidos,
            Pagina         = pagina,
            TamPagina      = tamPagina,
        };
    }


    public async Task<IEnumerable<PlnSeguimiento>> GetPorPedidoAsync(long numPed, int serie)
    {
        // V_PLN_TRAZABILIDAD (§8.3): todas las fechas estimadas y reales.
        // JOIN a V_PLN_ESTADO_ITEM (§8.2) para KGs, nombre_paso, color_ui, semáforo, etc.
        // JOIN a PLN_SEGUIMIENTO directamente para campos no expuestos por la trazabilidad
        // (FCH_EST_*, FCH_REAL_CC_*, SOLO_DESPACHO, IND_REPROCESO, NRO_CICLO, IND_URGENTE, IND_FLUJO).
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
                   s.FCH_REG_ENTREGA, s.FCH_ENTREGA_ORI,
                   s.FCH_EST_HILANDERIA, s.FCH_EST_PARTIDA,
                   s.FCH_EST_TIN_INI,   s.FCH_EST_TIN_FIN,
                   s.FCH_EST_SECADO,    s.FCH_EST_CALIDAD, s.FCH_EST_DESPACHO,
                   s.FCH_REAL_PROGRAMADO, s.FCH_REAL_PRODUCCION,
                   s.FCH_REAL_PARTIDA,
                   s.FCH_REAL_TIN_INI,  s.FCH_REAL_TIN_FIN,
                   s.FCH_REAL_SECADO,
                   s.FCH_REAL_CC_TINTO, s.FCH_REAL_CC_RECHAZO,
                   s.FCH_REAL_GASEADO,
                   s.FCH_REAL_DEVANADO, s.FCH_REAL_CALIDAD,
                   s.FCH_REAL_ALM_PT,   s.FCH_REAL_DESPACHO,
                   s.KG_PRODUCIDOS, s.KG_EN_TIN, s.KG_EN_ALM_PT,
                   s.KG_DESPACHADOS,    s.KG_PENDIENTES,
                   s.IND_RETRASO, s.DIAS_RETRASO,
                   s.IND_URGENTE, s.IND_REPROCESO,
                   s.IND_FLUJO,
                   s.ESTADO,
                   s.COD_MAQ_TT,
                   s.COD_MAQ_SECADO,
                   s.COD_MAQ_DEVAN,
                   s.NUM_PARTIDA,
                   (SELECT MAX(p_ant.NUMERO)
                    FROM   {S}ITEMPED_DET id_ant
                    JOIN   {S}PARTIDA p_ant ON p_ant.NROPROG = id_ant.NROPROG
                                           AND p_ant.ESTADO   = '8'
                    WHERE  id_ant.SERIE   = s.SERIE
                      AND  id_ant.NUM_PED = s.NUM_PED
                      AND  id_ant.NRO     = s.NRO
                      AND  id_ant.NUM_DET = s.NUM_DET)  AS NUM_PARTIDA_ANT,
                   id.MAQUINA        AS MAQ_PROGRAMADA,
                   p.COD_MAQ         AS MAQ_PARTIDA,
                   (SELECT tt.cod_maq
                    FROM   {S}TT_RPRODUC tt
                    JOIN   {S}PARTIDA_MAS pm ON pm.numero = tt.receta AND pm.tp_transac = 'IR'
                    WHERE  pm.partida      = s.NUM_PARTIDA
                      AND  tt.estado       IN ('0','1','2')
                      AND  s.NUM_PARTIDA   IS NOT NULL
                      AND  ROWNUM          = 1)  AS MAQ_REAL_TT,
                   s.FCH_APROBACION, s.FCH_PLANIF, s.USR_PLANIF,
                   pe2.A_ADUSER      AS USR_REGISTRO,
                   cu_reg.C_NOMBRE   AS NOMBRE_REGISTRO,
                   pe2.A_USAPROB     AS USR_APROBACION,
                   cu_apr.C_NOMBRE   AS NOMBRE_APROBACION,
                   cu_pln.C_NOMBRE   AS NOMBRE_PLANIF
            FROM   {S}PLN_SEGUIMIENTO s
            JOIN   {S}V_PLN_ESTADO_ITEM v
                ON  v.ID_SEGUIM = s.ID_SEGUIM
            -- Subquery garantiza 1 fila por item: toma el NROPROG más reciente
            -- (cuando reproceso asigna un nuevo programa, ITEMPED_DET queda con 2 filas
            --  para la misma PK lógica — esta agrupación evita la duplicación en el resultado)
            LEFT JOIN (
                SELECT serie, num_ped, nro, num_det,
                       MAX(nroprog) AS nroprog,
                       MAX(maquina) KEEP (DENSE_RANK LAST ORDER BY nroprog) AS maquina
                FROM   {S}ITEMPED_DET
                WHERE  nroprog > 0
                GROUP  BY serie, num_ped, nro, num_det
            ) id ON id.SERIE   = s.SERIE
                AND id.NUM_PED = s.NUM_PED
                AND id.NRO     = s.NRO
                AND id.NUM_DET = s.NUM_DET
            -- JOIN por PK directa evita duplicados cuando NROPROG tiene N partidas (sublotes)
            LEFT JOIN {S}PARTIDA p ON p.NUMERO = s.NUM_PARTIDA
            -- v2.6: actores del ciclo de vida (registro, aprobación, planificación)
            -- Inline views en CS_USER: evita multiplicación cuando el mismo C_USER
            -- tiene más de 1 fila (p.ej. usuarios migrados o con doble empresa).
            LEFT JOIN {S}PEDIDO pe2 ON pe2.SERIE = s.SERIE AND pe2.NUM_PED = s.NUM_PED
            LEFT JOIN (SELECT C_USER, MAX(C_NOMBRE) AS C_NOMBRE FROM {S}CS_USER GROUP BY C_USER) cu_reg ON cu_reg.C_USER = pe2.A_ADUSER
            LEFT JOIN (SELECT C_USER, MAX(C_NOMBRE) AS C_NOMBRE FROM {S}CS_USER GROUP BY C_USER) cu_apr ON cu_apr.C_USER = pe2.A_USAPROB
            LEFT JOIN (SELECT C_USER, MAX(C_NOMBRE) AS C_NOMBRE FROM {S}CS_USER GROUP BY C_USER) cu_pln ON cu_pln.C_USER = s.USR_PLANIF
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
            oFchRealGaseado = r.GetOrdinal("FCH_REAL_GASEADO"),
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
            oIndFlujo     = r.GetOrdinal("IND_FLUJO"),
            oEstado = r.GetOrdinal("ESTADO"),
            oMaqTt = r.GetOrdinal("COD_MAQ_TT"),
            oMaqSecado = r.GetOrdinal("COD_MAQ_SECADO"),
            oMaqDevan = r.GetOrdinal("COD_MAQ_DEVAN"),
            oMaqProgramada = r.GetOrdinal("MAQ_PROGRAMADA"),
            oMaqPartida = r.GetOrdinal("MAQ_PARTIDA"),
            oMaqRealTt = r.GetOrdinal("MAQ_REAL_TT"),
            oNumPartida = r.GetOrdinal("NUM_PARTIDA"),
            oNumPartidaAnt = r.GetOrdinal("NUM_PARTIDA_ANT"),
            oFchAprobacion    = r.GetOrdinal("FCH_APROBACION"),
            oFchPlanif        = r.GetOrdinal("FCH_PLANIF"),
            oFchRegEntrega    = r.GetOrdinal("FCH_REG_ENTREGA"),
            oFchEntregaOri    = r.GetOrdinal("FCH_ENTREGA_ORI"),
            oUsrRegistro      = r.GetOrdinal("USR_REGISTRO"),
            oNombreRegistro   = r.GetOrdinal("NOMBRE_REGISTRO"),
            oUsrAprobacion    = r.GetOrdinal("USR_APROBACION"),
            oNombreAprobacion = r.GetOrdinal("NOMBRE_APROBACION"),
            oUsrPlanif        = r.GetOrdinal("USR_PLANIF"),
            oNombrePlanif     = r.GetOrdinal("NOMBRE_PLANIF");

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
                oIndRetraso, oDiasRetraso, oIndUrgente, oIndReproceso, oEstado,
                oFchRealGaseado, oMaqTt, oMaqProgramada, oMaqPartida, oMaqRealTt,
                oNumPartida, oNumPartidaAnt, oMaqSecado, oMaqDevan,
                oFchRegEntrega2: oFchRegEntrega, oFchEntregaOri2: oFchEntregaOri,
                oFchAprobacion: oFchAprobacion, oFchPlanif: oFchPlanif,
                oUsrRegistro: oUsrRegistro, oNombreRegistro: oNombreRegistro,
                oUsrAprobacion: oUsrAprobacion, oNombreAprobacion: oNombreAprobacion,
                oUsrPlanif: oUsrPlanif, oNombrePlanif: oNombrePlanif,
                oIndFlujo: oIndFlujo));
        } while (await r.ReadAsync());

        return list;
    }

    public Task<PlnSeguimiento?> GetByIdAsync(long idSeguim)
        => GetSingleByWhereAsync("s.ID_SEGUIM = :p1", cmd => cmd.Parameters.Add("p1", idSeguim));

    public Task<PlnSeguimiento?> GetByItemAsync(int serie, long numPed, int nro, int numDet)
        => GetSingleByWhereAsync(
            "s.SERIE = :serie AND s.NUM_PED = :numPed AND s.NRO = :nro AND s.NUM_DET = :numDet",
            cmd =>
            {
                cmd.Parameters.Add("serie",  serie);
                cmd.Parameters.Add("numPed", numPed);
                cmd.Parameters.Add("nro",    nro);
                cmd.Parameters.Add("numDet", numDet);
            });

    /// <summary>
    /// Helper compartido por GetByIdAsync / GetByItemAsync.
    /// Ejecuta el mismo SELECT completo que GetPorPedidoAsync pero con WHERE dinámico;
    /// devuelve el primer resultado o null.
    /// </summary>
    private async Task<PlnSeguimiento?> 
    GetSingleByWhereAsync(
        string whereClause, Action<OracleCommand> bindParams)
    {
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
                   s.FCH_REAL_GASEADO,
                   s.FCH_REAL_DEVANADO, s.FCH_REAL_CALIDAD,
                   s.FCH_REAL_ALM_PT,   s.FCH_REAL_DESPACHO,
                   s.KG_PRODUCIDOS, s.KG_EN_TIN, s.KG_EN_ALM_PT,
                   s.KG_DESPACHADOS,    s.KG_PENDIENTES,
                   s.IND_RETRASO, s.DIAS_RETRASO,
                   s.IND_URGENTE, s.IND_REPROCESO,
                   s.IND_FLUJO,
                   s.ESTADO,
                   s.COD_MAQ_TT,
                   s.COD_MAQ_SECADO,
                   s.COD_MAQ_DEVAN,
                   s.NUM_PARTIDA,
                   (SELECT MAX(p_ant.NUMERO)
                    FROM   {S}ITEMPED_DET id_ant
                    JOIN   {S}PARTIDA p_ant ON p_ant.NROPROG = id_ant.NROPROG
                                           AND p_ant.ESTADO   = '8'
                    WHERE  id_ant.SERIE   = s.SERIE
                      AND  id_ant.NUM_PED = s.NUM_PED
                      AND  id_ant.NRO     = s.NRO
                      AND  id_ant.NUM_DET = s.NUM_DET)  AS NUM_PARTIDA_ANT,
                   id.MAQUINA        AS MAQ_PROGRAMADA,
                   p.COD_MAQ         AS MAQ_PARTIDA,
                   (SELECT tt.cod_maq
                    FROM   {S}TT_RPRODUC tt
                    JOIN   {S}PARTIDA_MAS pm ON pm.numero = tt.receta AND pm.tp_transac = 'IR'
                    WHERE  pm.partida      = s.NUM_PARTIDA
                      AND  tt.estado       IN ('0','1','2')
                      AND  s.NUM_PARTIDA   IS NOT NULL
                      AND  ROWNUM          = 1)  AS MAQ_REAL_TT,
                   s.FCH_APROBACION, s.FCH_PLANIF, s.USR_PLANIF,
                   s.FCH_REG_ENTREGA, s.FCH_ENTREGA_ORI,
                   pe2.A_ADUSER      AS USR_REGISTRO,
                   cu_reg.C_NOMBRE   AS NOMBRE_REGISTRO,
                   pe2.A_USAPROB     AS USR_APROBACION,
                   cu_apr.C_NOMBRE   AS NOMBRE_APROBACION,
                   cu_pln.C_NOMBRE   AS NOMBRE_PLANIF
            FROM   {S}PLN_SEGUIMIENTO s
            JOIN   {S}V_PLN_ESTADO_ITEM v ON v.ID_SEGUIM = s.ID_SEGUIM
            -- Subquery garantiza 1 fila por item: toma el NROPROG más reciente
            -- (reproceso puede generar una segunda fila en ITEMPED_DET para la misma PK lógica)
            LEFT JOIN (
                SELECT serie, num_ped, nro, num_det,
                       MAX(nroprog) AS nroprog,
                       MAX(maquina) KEEP (DENSE_RANK LAST ORDER BY nroprog) AS maquina
                FROM   {S}ITEMPED_DET
                WHERE  nroprog > 0
                GROUP  BY serie, num_ped, nro, num_det
            ) id ON id.SERIE   = s.SERIE
                AND id.NUM_PED = s.NUM_PED
                AND id.NRO     = s.NRO
                AND id.NUM_DET = s.NUM_DET
            LEFT JOIN {S}PARTIDA p ON p.NUMERO = s.NUM_PARTIDA
            -- v2.6: actores del ciclo de vida
            -- Inline views en CS_USER: evita duplicados si C_USER no es único.
            LEFT JOIN {S}PEDIDO pe2 ON pe2.SERIE = s.SERIE AND pe2.NUM_PED = s.NUM_PED
            LEFT JOIN (SELECT C_USER, MAX(C_NOMBRE) AS C_NOMBRE FROM {S}CS_USER GROUP BY C_USER) cu_reg ON cu_reg.C_USER = pe2.A_ADUSER
            LEFT JOIN (SELECT C_USER, MAX(C_NOMBRE) AS C_NOMBRE FROM {S}CS_USER GROUP BY C_USER) cu_apr ON cu_apr.C_USER = pe2.A_USAPROB
            LEFT JOIN (SELECT C_USER, MAX(C_NOMBRE) AS C_NOMBRE FROM {S}CS_USER GROUP BY C_USER) cu_pln ON cu_pln.C_USER = s.USR_PLANIF
            WHERE  {whereClause}";

        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        bindParams(cmd);

        await using var r = (OracleDataReader)await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        return MapSeguimientoOrd(r,
            r.GetOrdinal("ID_SEGUIM"), r.GetOrdinal("SERIE"),
            r.GetOrdinal("NUM_PED"), r.GetOrdinal("NRO"), r.GetOrdinal("NUM_DET"),
            r.GetOrdinal("COD_CLIENTE"), r.GetOrdinal("NOMBRE_CLIENTE"),
            r.GetOrdinal("COD_ART"), r.GetOrdinal("COLOR"), r.GetOrdinal("TITULO"),
            r.GetOrdinal("PROCESO"), r.GetOrdinal("CANTIDAD_ORIG"),
            r.GetOrdinal("SOLO_DESPACHO"), r.GetOrdinal("COD_PASO_ACT"),
            r.GetOrdinal("NOMBRE_PASO"), r.GetOrdinal("COLOR_UI"),
            r.GetOrdinal("NRO_CICLO"), r.GetOrdinal("FCH_PEDIDO"),
            r.GetOrdinal("FCH_ENTREGA_COMP"),
            r.GetOrdinal("FCH_EST_HILANDERIA"), r.GetOrdinal("FCH_EST_PARTIDA"),
            r.GetOrdinal("FCH_EST_TIN_INI"),   r.GetOrdinal("FCH_EST_TIN_FIN"),
            r.GetOrdinal("FCH_EST_SECADO"),    r.GetOrdinal("FCH_EST_CALIDAD"),
            r.GetOrdinal("FCH_EST_DESPACHO"),
            r.GetOrdinal("FCH_REAL_PROGRAMADO"), r.GetOrdinal("FCH_REAL_PRODUCCION"),
            r.GetOrdinal("FCH_REAL_PARTIDA"),
            r.GetOrdinal("FCH_REAL_TIN_INI"),  r.GetOrdinal("FCH_REAL_TIN_FIN"),
            r.GetOrdinal("FCH_REAL_SECADO"),
            r.GetOrdinal("FCH_REAL_CC_TINTO"), r.GetOrdinal("FCH_REAL_CC_RECHAZO"),
            r.GetOrdinal("FCH_REAL_DEVANADO"), r.GetOrdinal("FCH_REAL_CALIDAD"),
            r.GetOrdinal("FCH_REAL_ALM_PT"),   r.GetOrdinal("FCH_REAL_DESPACHO"),
            r.GetOrdinal("KG_PRODUCIDOS"), r.GetOrdinal("KG_EN_TIN"),
            r.GetOrdinal("KG_EN_ALM_PT"),  r.GetOrdinal("KG_DESPACHADOS"),
            r.GetOrdinal("KG_PENDIENTES"),
            r.GetOrdinal("IND_RETRASO"),   r.GetOrdinal("DIAS_RETRASO"),
            r.GetOrdinal("IND_URGENTE"),   r.GetOrdinal("IND_REPROCESO"),
            r.GetOrdinal("ESTADO"),
            r.GetOrdinal("FCH_REAL_GASEADO"),
            r.GetOrdinal("COD_MAQ_TT"), r.GetOrdinal("MAQ_PROGRAMADA"),
            r.GetOrdinal("MAQ_PARTIDA"), r.GetOrdinal("MAQ_REAL_TT"),
            r.GetOrdinal("NUM_PARTIDA"),
            r.GetOrdinal("NUM_PARTIDA_ANT"),
            r.GetOrdinal("COD_MAQ_SECADO"), r.GetOrdinal("COD_MAQ_DEVAN"),
            oFchRegEntrega2: r.GetOrdinal("FCH_REG_ENTREGA"),
            oFchEntregaOri2: r.GetOrdinal("FCH_ENTREGA_ORI"),
            oFchAprobacion: r.GetOrdinal("FCH_APROBACION"),
            oFchPlanif: r.GetOrdinal("FCH_PLANIF"),
            oUsrRegistro:  r.GetOrdinal("USR_REGISTRO"),
            oNombreRegistro: r.GetOrdinal("NOMBRE_REGISTRO"),
            oUsrAprobacion: r.GetOrdinal("USR_APROBACION"),
            oNombreAprobacion: r.GetOrdinal("NOMBRE_APROBACION"),
            oUsrPlanif:    r.GetOrdinal("USR_PLANIF"),
            oNombrePlanif: r.GetOrdinal("NOMBRE_PLANIF"),
            oIndFlujo: r.GetOrdinal("IND_FLUJO"));
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
                   ev.TABLA_ORIGEN, ev.ID_OBJETO_ORIGEN, ev.KG_CANTIDAD,
                   ev.FCH_ESTIMADA_ANT, ev.FCH_ESTIMADA_NUE,
                   ev.OBSERVACION, ev.USUARIO
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
                IdEvento        = SafeVal<long>(r["ID_EVENTO"]),
                IdSeguim        = SafeVal<long>(r["ID_SEGUIM"]),
                NumPed          = SafeVal<long>(r["NUM_PED"]),
                Serie           = SafeVal<int>(r["SERIE"]),
                Nro             = SafeVal<int>(r["NRO"]),
                NumDet          = SafeVal<int>(r["NUM_DET"]),
                CodPaso         = codPaso,
                NombrePaso      = estados.TryGetValue(codPaso, out var nom) ? nom : codPaso,
                TipoEvento      = SafeStr(r["TIPO_EVENTO"]),
                FchEvento       = SafeVal<DateTime>(r["FCH_EVENTO"]),
                TablaOrigen     = SafeStr(r["TABLA_ORIGEN"]),
                IdObjetoOrigen  = r["ID_OBJETO_ORIGEN"] == DBNull.Value ? null : SafeVal<long?>(r["ID_OBJETO_ORIGEN"]),
                KgCantidad      = r["KG_CANTIDAD"] == DBNull.Value ? null : SafeVal<decimal?>(r["KG_CANTIDAD"]),
                FchEstimadaAnt  = SafeDate(r["FCH_ESTIMADA_ANT"]),
                FchEstimadaNue  = SafeDate(r["FCH_ESTIMADA_NUE"]),
                Observacion     = SafeStr(r["OBSERVACION"]),
                Usuario         = SafeStr(r["USUARIO"]),
                NroCiclo        = 0,
            });
        }
        return list;
    }

    public async Task<(IEnumerable<PlnLogEvento> Items, int TotalRegistros)> GetEventosPorSeguimAsync(
        long idSeguim, string? tipoEvento = null, int? nroCiclo = null, int pagina = 1, int tamPagina = 25)
    {
        var estados = (await GetEstadosAsync()).ToDictionary(e => e.CodPaso, e => e.NombrePaso);

        // Cláusulas de filtro opcionales
        var where = new System.Text.StringBuilder("WHERE ev.ID_SEGUIM = :idSeguim");
        if (!string.IsNullOrEmpty(tipoEvento)) where.Append(" AND ev.TIPO_EVENTO = :tipoEvento");
        // NRO_CICLO no existe en PLN_LOG_EVENTOS (§2.4 PKG_PLN.sql); filtro ignorado.

        var sqlCount = $"SELECT COUNT(*) FROM {S}PLN_LOG_EVENTOS ev {where}";
        var sqlData  = $@"
            SELECT * FROM (
                SELECT sub.*, ROWNUM AS RN FROM (
                    SELECT ev.ID_EVENTO, ev.ID_SEGUIM, ev.NUM_PED, ev.SERIE, ev.NRO, ev.NUM_DET,
                           ev.COD_PASO, ev.TIPO_EVENTO, ev.FCH_EVENTO,
                           ev.TABLA_ORIGEN, ev.ID_OBJETO_ORIGEN, ev.KG_CANTIDAD,
                           ev.FCH_ESTIMADA_ANT, ev.FCH_ESTIMADA_NUE,
                           ev.OBSERVACION, ev.USUARIO
                    FROM   {S}PLN_LOG_EVENTOS ev
                    {where}
                    ORDER  BY ev.FCH_EVENTO DESC
                ) sub
                WHERE ROWNUM <= :offsetFin
            ) WHERE RN > :offset";

        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();

        // Contar total
        await using var cmdCnt = new OracleCommand(sqlCount, conn);
        cmdCnt.BindByName = true;
        cmdCnt.Parameters.Add("idSeguim", idSeguim);
        if (!string.IsNullOrEmpty(tipoEvento)) cmdCnt.Parameters.Add("tipoEvento", tipoEvento);
        var total = Convert.ToInt32(await cmdCnt.ExecuteScalarAsync() ?? 0);

        // Página de datos
        await using var cmdData = new OracleCommand(sqlData, conn);
        cmdData.BindByName = true;
        cmdData.Parameters.Add("idSeguim",  idSeguim);
        if (!string.IsNullOrEmpty(tipoEvento)) cmdData.Parameters.Add("tipoEvento", tipoEvento);
        cmdData.Parameters.Add("offset",    (pagina - 1) * tamPagina);
        cmdData.Parameters.Add("offsetFin", pagina * tamPagina);

        var list = new List<PlnLogEvento>();
        await using var r = await cmdData.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var codPaso = SafeStr(r["COD_PASO"]);
            list.Add(new PlnLogEvento
            {
                IdEvento        = SafeVal<long>(r["ID_EVENTO"]),
                IdSeguim        = SafeVal<long>(r["ID_SEGUIM"]),
                NumPed          = SafeVal<long>(r["NUM_PED"]),
                Serie           = SafeVal<int>(r["SERIE"]),
                Nro             = SafeVal<int>(r["NRO"]),
                NumDet          = SafeVal<int>(r["NUM_DET"]),
                CodPaso         = codPaso,
                NombrePaso      = estados.TryGetValue(codPaso, out var nom) ? nom : codPaso,
                TipoEvento      = SafeStr(r["TIPO_EVENTO"]),
                FchEvento       = SafeVal<DateTime>(r["FCH_EVENTO"]),
                TablaOrigen     = SafeStr(r["TABLA_ORIGEN"]),
                IdObjetoOrigen  = r["ID_OBJETO_ORIGEN"] == DBNull.Value ? null : SafeVal<long?>(r["ID_OBJETO_ORIGEN"]),
                KgCantidad      = r["KG_CANTIDAD"] == DBNull.Value ? null : SafeVal<decimal?>(r["KG_CANTIDAD"]),
                FchEstimadaAnt  = SafeDate(r["FCH_ESTIMADA_ANT"]),
                FchEstimadaNue  = SafeDate(r["FCH_ESTIMADA_NUE"]),
                Observacion     = SafeStr(r["OBSERVACION"]),
                Usuario         = SafeStr(r["USUARIO"]),
                NroCiclo        = 0,
            });
        }
        return (list, total);
    }

    public async Task<IEnumerable<PlnAlerta>> GetAlertasPorPedidoAsync(long numPed, int serie)
    {
        // V_PLN_ALERTAS_ACTIVAS (§8.4 v2.3): mismos campos enriquecidos que GetActivasAsync,
        // filtrado por num_ped y serie para mostrar en el modal de Pedido con igual detalle.
        var sql = $@"
            SELECT a.id_alerta, a.serie, a.tip_alerta, a.nivel, a.titulo, a.detalle,
                   a.fch_alerta, a.fch_limite, a.dias_retraso, a.num_ped, a.nro,
                   a.cod_cliente, a.nom_cliente, a.cod_maq, a.estado, a.horas_sin_resolver,
                   a.cod_art, a.titulo_art, a.proceso, a.cod_paso_act, a.nombre_paso, a.color_ui,
                   a.fch_entrega_comp, a.dias_retraso_ent, a.cantidad_orig, a.kg_pendientes,
                   a.nro_ciclo, a.ind_urgente
            FROM   {S}V_PLN_ALERTAS_ACTIVAS a
            WHERE  a.num_ped = :numPed
              AND  a.serie   = :serie
            ORDER  BY DECODE(a.nivel,'C',1,'A',2,'M',3,'B',4), a.fch_limite";

        var list = new List<PlnAlerta>();
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        cmd.Parameters.Add("numPed", numPed);
        cmd.Parameters.Add("serie", serie);
        await using var r = await cmd.ExecuteReaderAsync();
        int oHoras = r.GetOrdinal("horas_sin_resolver");
        while (await r.ReadAsync())
        {
            double? horasSinResolver = r.IsDBNull(oHoras) ? null : (double?)r.GetDouble(oHoras);
            list.Add(new PlnAlerta
            {
                IdAlerta         = SafeVal<long>(r["id_alerta"]),
                Serie            = r["serie"]            == DBNull.Value ? null : SafeVal<int?>(r["serie"]),
                TipAlerta        = SafeStr(r["tip_alerta"]),
                Nivel            = SafeStr(r["nivel"]),
                Titulo           = SafeStr(r["titulo"]),
                Detalle          = SafeStr(r["detalle"]),
                FchAlerta        = SafeVal<DateTime>(r["fch_alerta"]),
                FchLimite        = SafeDate(r["fch_limite"]),
                DiasRetraso      = r["dias_retraso"]     == DBNull.Value ? null : SafeVal<int?>(r["dias_retraso"]),
                CodMaq           = SafeStr(r["cod_maq"]),
                Estado           = SafeStr(r["estado"]),
                NumPed           = r["num_ped"]          == DBNull.Value ? null : SafeVal<long?>(r["num_ped"]),
                Nro              = r["nro"]              == DBNull.Value ? null : SafeVal<int?>(r["nro"]),
                CodCliente       = SafeStr(r["cod_cliente"]),
                NombreCliente    = SafeStr(r["nom_cliente"]),
                CodArt           = r["cod_art"]          == DBNull.Value ? null : SafeStr(r["cod_art"]),
                TituloArt        = r["titulo_art"]       == DBNull.Value ? null : SafeStr(r["titulo_art"]),
                Proceso          = SafeStr(r["proceso"]),
                CodPasoAct       = SafeStr(r["cod_paso_act"]),
                NombrePaso       = SafeStr(r["nombre_paso"]),
                ColorUiPaso      = SafeStr(r["color_ui"]),
                FchEntregaComp   = SafeDate(r["fch_entrega_comp"]),
                DiasRetrasoEnt   = r["dias_retraso_ent"] == DBNull.Value ? null : SafeVal<int?>(r["dias_retraso_ent"]),
                CantidadOrig     = r["cantidad_orig"]    == DBNull.Value ? null : SafeVal<decimal?>(r["cantidad_orig"]),
                KgPendientes     = r["kg_pendientes"]    == DBNull.Value ? null : SafeVal<decimal?>(r["kg_pendientes"]),
                NroCiclo         = r["nro_ciclo"]        == DBNull.Value ? null : SafeVal<int?>(r["nro_ciclo"]),
                IndUrgente       = SafeStr(r["ind_urgente"]),
                HorasSinResolver = horasSinResolver,
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
                   t.color, t.titulo,
                   t.fch_pedido,
                   COALESCE(t.fch_aprobacion, t.fch_aprob_pedido_raw) AS fch_aprob_pedido,
                   t.fch_planeada, t.fch_entrega_plan,
                   t.fch_est_cono1, t.fch_est_tenido,
                   t.fch_est_hilanderia, t.fch_est_partida,
                   t.fch_est_tin_ini,   t.fch_est_tin_fin,
                   t.fch_est_secado,    t.fch_est_calidad, t.fch_est_despacho,
                   t.fch_real_programado, t.fch_real_produccion,
                   t.fch_real_partida, t.fch_real_tin_ini,
                   t.fch_prog_tin, t.fch_real_tin_fin,
                   t.fch_real_secado, t.fch_real_cc_tinto, t.fch_real_calidad,
                   t.fch_real_alm_pt, t.fch_real_despacho,
                   t.fch_compromiso_cliente,
                   t.dias_pedido_a_partida, t.dias_en_tintoreria,
                   t.dias_partida_a_almpt,  t.dias_almpt_a_despacho,
                   t.dias_total_ciclo,      t.dias_desvio_cliente,
                   t.cod_paso_act, t.dias_retraso, t.nro_ciclo,
                   t.fch_planif,
                   t.usr_registro,   t.nombre_registro,
                   t.usr_aprobacion, t.nombre_aprobacion,
                   t.usr_planif,     t.nombre_planif
            FROM   {S}V_PLN_TRAZABILIDAD t
            WHERE  t.num_ped = :numPed
              AND  t.serie   = :serie
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
                Color                 = SafeStr(r["color"]),
                Titulo                = SafeStr(r["titulo"]),
                FchPedido             = SafeVal<DateTime>(r["fch_pedido"]),
                FchAprobPedido        = SafeDate(r["fch_aprob_pedido"]),
                FchPlaneada           = SafeDate(r["fch_planeada"]),
                FchEntregaPlan        = SafeDate(r["fch_entrega_plan"]),
                FchEstCono1           = SafeDate(r["fch_est_cono1"]),
                FchEstTenido          = SafeDate(r["fch_est_tenido"]),
                FchEstHilanderia      = SafeDate(r["fch_est_hilanderia"]),
                FchEstPartida         = SafeDate(r["fch_est_partida"]),
                FchEstTinIni          = SafeDate(r["fch_est_tin_ini"]),
                FchEstTinFin          = SafeDate(r["fch_est_tin_fin"]),
                FchEstSecado          = SafeDate(r["fch_est_secado"]),
                FchEstCalidad         = SafeDate(r["fch_est_calidad"]),
                FchEstDespacho        = SafeDate(r["fch_est_despacho"]),
                FchRealProgramado     = SafeDate(r["fch_real_programado"]),
                FchRealProduccion     = SafeDate(r["fch_real_produccion"]),
                FchRealPartida        = SafeDate(r["fch_real_partida"]),
                FchRealTinIni         = SafeDate(r["fch_real_tin_ini"]),
                FchProgTin            = SafeDate(r["fch_prog_tin"]),
                FchRealTinFin         = SafeDate(r["fch_real_tin_fin"]),
                FchRealSecado         = SafeDate(r["fch_real_secado"]),
                FchRealCcTinto        = SafeDate(r["fch_real_cc_tinto"]),
                FchRealCalidad        = SafeDate(r["fch_real_calidad"]),
                FchRealAlmPt          = SafeDate(r["fch_real_alm_pt"]),
                FchRealDespacho       = SafeDate(r["fch_real_despacho"]),
                FchCompromisoCliente  = SafeDate(r["fch_compromiso_cliente"]),
                DiasPedidoAPartida    = SafeOracleDouble((Oracle.ManagedDataAccess.Client.OracleDataReader)r, r.GetOrdinal("dias_pedido_a_partida")),
                DiasEnTintoreria      = SafeOracleDouble((Oracle.ManagedDataAccess.Client.OracleDataReader)r, r.GetOrdinal("dias_en_tintoreria")),
                DiasPartidaAAlmPt     = SafeOracleDouble((Oracle.ManagedDataAccess.Client.OracleDataReader)r, r.GetOrdinal("dias_partida_a_almpt")),
                DiasAlmPtADespacho    = SafeOracleDouble((Oracle.ManagedDataAccess.Client.OracleDataReader)r, r.GetOrdinal("dias_almpt_a_despacho")),
                DiasTotalCiclo        = SafeOracleDouble((Oracle.ManagedDataAccess.Client.OracleDataReader)r, r.GetOrdinal("dias_total_ciclo")),
                DiasDesvioCliente     = SafeOracleDouble((Oracle.ManagedDataAccess.Client.OracleDataReader)r, r.GetOrdinal("dias_desvio_cliente")),
                CodPasoAct            = SafeStr(r["cod_paso_act"]),
                DiasRetraso           = SafeVal<int>(r["dias_retraso"]),
                NroCiclo              = SafeVal<int>(r["nro_ciclo"]),
                FchPlanif             = SafeDate(r["fch_planif"]),
                UsrRegistro           = SafeStr(r["usr_registro"]),
                NombreRegistro        = SafeStr(r["nombre_registro"]),
                UsrAprobacion         = SafeStr(r["usr_aprobacion"]),
                NombreAprobacion      = SafeStr(r["nombre_aprobacion"]),
                UsrPlanif             = SafeStr(r["usr_planif"]),
                NombrePlanif          = SafeStr(r["nombre_planif"]),
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
        string nuevoPaso, string? observacion = null, decimal? kgCantidad = null,
        string? proceso = null)
    {
        // PKG_PLN.SP_PLN_AVANZA_PASO (§6 PKG_PLN): correcciones manuales autorizadas.
        // Los triggers de planta llaman al mismo SP de forma automática.
        // COMMIT interno NO debe usarse en trigger (ORA-04092); pero en llamada directa el SP
        // devuelve sin COMMIT, por eso se hace COMMIT explícito desde aquí.
        //
        // ── Regla BUG#35 del paquete PKG_PLN ────────────────────────────────────
        // SP_PLN_AVANZA_PASO en Oracle verifica PROCESO='24' antes de permitir el avance a
        // PASO '09B' (Gaseado). Esta etapa solo existe para hilos especiales (ej. merino)
        // porque el proceso de gaseado quema las fibras superficiales para dar brillo y
        // suavidad, y no aplica a hilados estándar.
        //
        // Si se intenta avanzar a '09B' con un ítem de otro proceso, Oracle lo descarta
        // silenciosamente. Aquí lanzamos una excepción descriptiva para que el usuario
        // (o el código llamador) sepa exactamente por qué no se puede avanzar, en lugar
        // de recibir un resultado vacío confuso.
        if (nuevoPaso == "09B" && !string.Equals(proceso, "24", StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"El paso '09B — Gaseado' solo está habilitado para ítems con PROCESO='24' " +
                $"(hilo especial / merino). El ítem actual tiene PROCESO='{proceso ?? "desconocido"}'. " +
                $"Verifique que el artículo corresponda a un proceso de gaseado antes de avanzar. " +
                $"Restricción definida en PKG_PLN.SP_PLN_AVANZA_PASO (BUG#35).");

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
        cmd.Parameters.Add(new OracleParameter("pasoIni", OracleDbType.Varchar2, 3) { Value = (object)pasoIni });
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

    public async Task<(string Descripcion, string Fibra)> GetArticuloInfoAsync(string codArt)
    {
        const string sql = "SELECT DESCRIPCION, FIBRA FROM ARTICUL WHERE COD_ART = :codArt";
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        cmd.Parameters.Add(new OracleParameter("codArt", OracleDbType.Varchar2, 25) { Value = (object)(codArt ?? "") });
        await using var r = await cmd.ExecuteReaderAsync();
        if (await r.ReadAsync())
            return (SafeStr(r["DESCRIPCION"]), SafeStr(r["FIBRA"]));
        return ("", "");
    }

    public async Task<(int BanosActivos, bool EsLibre, decimal PctCargaHoy, bool HayCargaHoy, int DiasAntiguo)> GetMaquinaStatusAsync(string codMaq)
    {
        var sqlBanos = $@"
            SELECT NVL(SUM(CASE WHEN estado IN ('1','2') THEN 1 ELSE 0 END), 0) AS banos_activos
            FROM   {S}TT_RPRODUC
            WHERE  cod_maq   = :codMaq
              AND  fecha_ini >= TRUNC(SYSDATE) - 7";

        // Usa el día más reciente disponible en los últimos 30 días
        // (el JOB_PLN_CARGA corre a las 23:30, por lo que durante el día solo hay datos de ayer)
        var sqlCarga = $@"
            SELECT NVL(c.PCT_UTILIZACION, 0)          AS pct_hoy,
                   NVL(c.IND_SOBRECARGADA,'N')        AS ind_sob,
                   TRUNC(SYSDATE) - TRUNC(c.FECHA)   AS dias_antiguo
            FROM   {S}PLN_CARGA_DIARIA c
            WHERE  c.COD_MAQ = :codMaq
              AND  c.FECHA   = (SELECT MAX(c2.FECHA)
                                FROM   {S}PLN_CARGA_DIARIA c2
                                WHERE  c2.COD_MAQ = :codMaq
                                  AND  c2.FECHA  <= TRUNC(SYSDATE)
                                  AND  c2.FECHA  >= TRUNC(SYSDATE) - 30)";

        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();

        // Query 1: baños activos en los últimos 7 días
        int banosActivos;
        await using (var cmd = new OracleCommand(sqlBanos, conn))
        {
            cmd.BindByName = true;
            cmd.Parameters.Add("codMaq", codMaq);
            await using var r = (OracleDataReader)await cmd.ExecuteReaderAsync();
            banosActivos = await r.ReadAsync() ? SafeVal<int>(r["BANOS_ACTIVOS"]) : 0;
        }

        // Query 2: carga desde PLN_CARGA_DIARIA — día más reciente disponible (≤ 30 días)
        decimal pctCargaHoy = 0;
        bool hayCargaHoy = false;
        int diasAntiguo = -1;
        await using (var cmd2 = new OracleCommand(sqlCarga, conn))
        {
            cmd2.BindByName = true;
            cmd2.Parameters.Add("codMaq", codMaq);
            await using var r2 = (OracleDataReader)await cmd2.ExecuteReaderAsync();
            if (await r2.ReadAsync() && r2["PCT_HOY"] != DBNull.Value)
            {
                pctCargaHoy  = SafeVal<decimal>(r2["PCT_HOY"]);
                hayCargaHoy  = true;
                diasAntiguo  = SafeVal<int>(r2["DIAS_ANTIGUO"]);
            }
        }

        return (banosActivos, banosActivos == 0, pctCargaHoy, hayCargaHoy, diasAntiguo);
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Detalle completo de Tintorería para una Partida
    // ────────────────────────────────────────────────────────────────────────────

    public async Task<PlnDetalleTt> GetDetalleTtAsync(long numPartida)
    {
        var result = new PlnDetalleTt();
        if (numPartida <= 0) return result;

        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();

        // ── 1. NROPROG y FECHA de la partida ────────────────────────────────────
        long nroprog = 0;
        {
            await using var cmd = new OracleCommand(
                $"SELECT NROPROG, FECHA FROM {S}PARTIDA WHERE NUMERO = :num AND ROWNUM = 1", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("num", numPartida);
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                if (r["NROPROG"] != DBNull.Value) nroprog = Convert.ToInt64(r["NROPROG"]);
                if (r["FECHA"] != DBNull.Value)   result.FechaPartida = SafeVal<DateTime?>(r["FECHA"]);
            }
        }

        // ── 2. Cálculo de recetas planificadas (ING_RECETAS_G vía PARTIDA_MAS) ──
        {
            var sql = $@"
                SELECT ig.numero        AS GUIA,
                       ig.proceso,
                       ig.maquina       AS COD_MAQ_PLANIF,
                       ig.peso_neto,
                       ig.estado        AS ESTADO_RECETA,
                       m.descripcion    AS NOMBRE_MAQ_PLANIF
                FROM   {S}ing_recetas_g ig
                JOIN   {S}partida_mas pm
                    ON  pm.tp_transac = ig.tp_transac
                    AND pm.serie      = ig.serie
                    AND pm.numero     = ig.numero
                LEFT JOIN {S}tt_maquina m ON m.cod_maq = ig.maquina
                WHERE  pm.partida = :numPartida
                ORDER  BY ig.numero";
            await using var cmd = new OracleCommand(sql, conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("numPartida", numPartida);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var proceso = SafeStr(r["PROCESO"]);
                result.CalculoRecetas.Add(new PlnCalculoReceta
                {
                    Guia            = SafeVal<long>(r["GUIA"]),
                    Proceso         = proceso,
                    DescProceso     = DescProceso(proceso),
                    CodMaqPlanif    = SafeStr(r["COD_MAQ_PLANIF"]),
                    NombreMaqPlanif = SafeStr(r["NOMBRE_MAQ_PLANIF"]),
                    PesoNeto        = r["PESO_NETO"]     == DBNull.Value ? null : SafeVal<decimal?>(r["PESO_NETO"]),
                    EstadoReceta    = SafeVal<int>(r["ESTADO_RECETA"]),
                    DescEstReceta   = DescEstadoReceta(SafeVal<int>(r["ESTADO_RECETA"])),
                });
            }
        }

        // ── 3. Baños TT ejecutados (TT_RPRODUC TIPODOC='IR' vía PARTIDA_MAS) ───
        {
            var sql = $@"
                SELECT tt.receta            AS GUIA,
                       tt.proceso,
                       tt.cod_maq,
                       m.descripcion        AS NOMBRE_MAQ,
                       tt.fecha_ini,
                       tt.fecha_fin,
                       ROUND((tt.fecha_fin - tt.fecha_ini) * 24, 2)  AS HORAS,
                       tt.calificacion,
                       tt.estado,
                       ig.maquina           AS COD_MAQ_PLANIF,
                       mpl.descripcion      AS NOMBRE_MAQ_PLANIF
                FROM   {S}tt_rproduc tt
                JOIN   {S}partida_mas pm
                    ON  pm.numero    = tt.receta
                    AND pm.tp_transac = 'IR'
                JOIN   {S}ing_recetas_g ig ON ig.numero = tt.receta
                LEFT JOIN {S}tt_maquina m   ON m.cod_maq  = tt.cod_maq
                LEFT JOIN {S}tt_maquina mpl ON mpl.cod_maq = ig.maquina
                WHERE  pm.partida = :numPartida
                  AND  tt.tipodoc = 'IR'
                ORDER  BY tt.fecha_ini";
            await using var cmd = new OracleCommand(sql, conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("numPartida", numPartida);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var proceso = SafeStr(r["PROCESO"]);
                var calif   = r["CALIFICACION"] == DBNull.Value ? null : SafeStr(r["CALIFICACION"]);
                result.Banos.Add(new PlnBanoTt
                {
                    Guia            = SafeVal<long>(r["GUIA"]),
                    Proceso         = proceso,
                    DescProceso     = DescProceso(proceso),
                    CodMaq          = SafeStr(r["COD_MAQ"]),
                    NombreMaq       = SafeStr(r["NOMBRE_MAQ"]),
                    FechaIni        = SafeVal<DateTime>(r["FECHA_INI"]),
                    FechaFin        = r["FECHA_FIN"]  == DBNull.Value ? null : SafeVal<DateTime?>(r["FECHA_FIN"]),
                    Horas           = r["HORAS"]       == DBNull.Value ? null : SafeVal<decimal?>(r["HORAS"]),
                    Calificacion    = calif,
                    DescCalif       = DescCalificacion(calif),
                    Estado          = SafeStr(r["ESTADO"]),
                    CodMaqPlanif    = SafeStr(r["COD_MAQ_PLANIF"]),
                    NombreMaqPlanif = SafeStr(r["NOMBRE_MAQ_PLANIF"]),
                });
            }
        }

        // ── 4. Registro de secado (TT_RSECADO vía GUIA = PARTIDA.NUMERO) ─────────
        // Devuelve TODOS los registros ordenados por FECHA_INI ASC (S01, S04, etc.)
        {
            var sql = $@"
                SELECT rs.guia,
                       rs.cod_maq,
                       m.descripcion  AS NOMBRE_MAQ,
                       rs.fecha_ini,
                       rs.fecha_fin,
                       rs.secado      AS MIN_SECADO,
                       rs.resecado    AS MIN_RESECADO,
                       rs.peso_neto,
                       rs.estado
                FROM   {S}tt_rsecado rs
                LEFT JOIN {S}tt_maquina m ON m.cod_maq = rs.cod_maq
                WHERE  rs.guia = :numPartida
                ORDER  BY rs.fecha_ini ASC";
            await using var cmd = new OracleCommand(sql, conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("numPartida", numPartida);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var estadoSec = SafeStr(r["ESTADO"]);
                result.Secados.Add(new PlnSecadoTt
                {
                    GuiaPartida = SafeVal<long>(r["GUIA"]),
                    CodMaq      = SafeStr(r["COD_MAQ"]),
                    NombreMaq   = SafeStr(r["NOMBRE_MAQ"]),
                    FechaIni    = SafeVal<DateTime>(r["FECHA_INI"]),
                    FechaFin    = r["FECHA_FIN"]    == DBNull.Value ? null : SafeVal<DateTime?>(r["FECHA_FIN"]),
                    PesoNeto    = r["PESO_NETO"]    == DBNull.Value ? null : SafeVal<decimal?>(r["PESO_NETO"]),
                    MinSecado   = r["MIN_SECADO"]   == DBNull.Value ? null : SafeVal<decimal?>(r["MIN_SECADO"]),
                    MinResecado = r["MIN_RESECADO"] == DBNull.Value ? null : SafeVal<decimal?>(r["MIN_RESECADO"]),
                    Estado      = estadoSec,
                    DescEstado  = DescEstadoSecado(estadoSec),
                });
            }
        }

        // ── 5. Control de Calidad TT (CTCALIDAD_D vía GUIA = PARTIDA.NUMERO) ───
        {
            var sql = $@"
                SELECT cc.numero, cc.guia, cc.fecha,
                       cc.est_evaluacion, cc.resultado,
                       cc.merma_i, cc.merma_f,
                       cc.observa, cc.tono, cc.sf, cc.si, cc.defecto
                FROM   (SELECT cc2.numero, cc2.guia, cc2.fecha,
                               cc2.est_evaluacion, cc2.resultado,
                               cc2.merma_i, cc2.merma_f,
                               cc2.observa, cc2.tono, cc2.sf, cc2.si, cc2.defecto
                        FROM   {S}ctcalidad_d cc2
                        WHERE  cc2.guia = :numPartida
                        ORDER  BY cc2.fecha DESC) cc
                WHERE  ROWNUM = 1";
            await using var cmd = new OracleCommand(sql, conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("numPartida", numPartida);
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                var estEval   = SafeStr(r["EST_EVALUACION"]);
                var resultado = SafeStr(r["RESULTADO"]);
                result.Calidad = new PlnCalidadTt
                {
                    Numero        = SafeVal<long>(r["NUMERO"]),
                    GuiaPartida   = SafeVal<long>(r["GUIA"]),
                    Fecha         = SafeVal<DateTime>(r["FECHA"]),
                    EstEvaluacion = estEval,
                    DescEstEval   = DescEstEvaluacion(estEval),
                    Resultado     = resultado,
                    DescResultado = DescResultadoCC(resultado),
                    MermaInicio   = r["MERMA_I"]   == DBNull.Value ? null : SafeVal<decimal?>(r["MERMA_I"]),
                    MermaFin      = r["MERMA_F"]   == DBNull.Value ? null : SafeVal<decimal?>(r["MERMA_F"]),
                    Observacion   = r["OBSERVA"]   == DBNull.Value ? null : SafeStr(r["OBSERVA"]),
                    Tono          = r["TONO"]      == DBNull.Value ? null : SafeStr(r["TONO"]),
                    Solidez       = r["SF"]        == DBNull.Value ? null : SafeStr(r["SF"]),
                    Igualdad      = r["SI"]        == DBNull.Value ? null : SafeStr(r["SI"]),
                    Defecto       = r["DEFECTO"]   == DBNull.Value ? null : SafeStr(r["DEFECTO"]),
                };
            }
        }

        // ── 6. Validación de Receta de Laboratorio (L_VALIDA_RECETA vía NROPROG) ─
        if (nroprog > 0)
        {
            var sql = $@"
                SELECT lv.numero, lv.nroprog, lv.tipo, lv.c_laboratorista,
                       lv.estado, lv.f_registro, lv.f_validacion
                FROM   (SELECT lv2.numero, lv2.nroprog, lv2.tipo, lv2.c_laboratorista,
                               lv2.estado,
                               lv2.a_adfecha      AS f_registro,
                               lv2.f_estado_tres  AS f_validacion
                        FROM   {S}l_valida_receta lv2
                        WHERE  lv2.nroprog = :nroprog
                        ORDER  BY lv2.a_adfecha DESC) lv
                WHERE  ROWNUM = 1";
            await using var cmd = new OracleCommand(sql, conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("nroprog", nroprog);
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                var tipo   = SafeVal<int>(r["TIPO"]);
                var estado = SafeVal<int>(r["ESTADO"]);
                result.ValidacionReceta = new PlnValidacionReceta
                {
                    Numero        = SafeVal<long>(r["NUMERO"]),
                    Nroprog       = SafeVal<long>(r["NROPROG"]),
                    Tipo          = tipo,
                    DescTipo      = tipo == 2 ? "Reproceso" : "Normal",
                    Laboratorista = SafeStr(r["C_LABORATORISTA"]),
                    Estado        = estado,
                    DescEstado    = DescEstadoLab(estado),
                    FchRegistro   = SafeVal<DateTime>(r["F_REGISTRO"]),
                    FchValidacion = r["F_VALIDACION"] == DBNull.Value ? null : SafeVal<DateTime?>(r["F_VALIDACION"]),
                };
            }
        }

        // ── 7. Programa Conera (H_PROGRAMACION vía GUIA = PARTIDA.NUMERO) ──────────
        {
            var sql = $@"
                SELECT hp.numero, hp.fecha AS fecha_ini, hp.fecha_fin,
                       hp.maq_proced AS cod_maq,
                       NVL(m.descripcion, hp.maq_proced) AS nombre_maq,
                       hp.estado
                FROM   {S}h_programacion hp
                LEFT JOIN {S}h_maquinas m ON m.cod_maq = hp.maq_proced
                WHERE  hp.guia = :numPartida
                ORDER  BY hp.fecha";
            await using var cmd = new OracleCommand(sql, conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("numPartida", numPartida);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var est = SafeStr(r["ESTADO"]);
                result.ProgramasConera.Add(new PlnProgramaConera
                {
                    Numero     = SafeVal<long>(r["NUMERO"]),
                    FechaIni   = SafeVal<DateTime>(r["FECHA_INI"]),
                    FechaFin   = r["FECHA_FIN"] == DBNull.Value ? null : SafeVal<DateTime?>(r["FECHA_FIN"]),
                    CodMaq     = SafeStr(r["COD_MAQ"]),
                    NombreMaq  = SafeStr(r["NOMBRE_MAQ"]),
                    Estado     = est,
                    DescEstado = est switch { "3" => "Completado", "1" => "En proceso", "0" => "Pendiente", _ => est },
                });
            }
        }

        // ── 8. Registro de Devanado (H_RPRODUC TP_MAQ<>'G' vía GUIA = PARTIDA.NUMERO) ─
        //    TP_MAQ='G' = Gaseadora (PASO '09B'). Todo lo demás es Devanado/Autoconer.
        {
            var sql = $@"
                SELECT hr.cod_maq,
                       NVL(m.descripcion, hr.cod_maq) AS nombre_maq,
                       hr.fecha_ini, hr.fecha_fin,
                       hr.unidades, hr.peso_neto, hr.estado
                FROM   {S}h_rproduc hr
                LEFT JOIN {S}h_maquinas m ON m.cod_maq = hr.cod_maq
                WHERE  hr.guia   = :numPartida
                  AND  hr.tp_maq <> 'G'
                ORDER  BY hr.fecha_ini";
            await using var cmd = new OracleCommand(sql, conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("numPartida", numPartida);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                result.Devanados.Add(new PlnDevanado
                {
                    CodMaq    = SafeStr(r["COD_MAQ"]),
                    NombreMaq = SafeStr(r["NOMBRE_MAQ"]),
                    FechaIni  = SafeVal<DateTime>(r["FECHA_INI"]),
                    FechaFin  = r["FECHA_FIN"] == DBNull.Value ? null : SafeVal<DateTime?>(r["FECHA_FIN"]),
                    Unidades  = r["UNIDADES"]  == DBNull.Value ? null : SafeVal<decimal?>(r["UNIDADES"]),
                    PesoNeto  = r["PESO_NETO"] == DBNull.Value ? null : SafeVal<decimal?>(r["PESO_NETO"]),
                    Estado    = SafeStr(r["ESTADO"]),
                });
            }
        }

        // ── 9. Revisado de Productos Acabados (REVISADO_G + REVISADO_D) ─────────────
        {
            // Cabeceras REVISADO_G
            var sqlG = $@"
                SELECT rg.numero, rg.guia, rg.maq_proced,
                       rg.a_adfecha AS fch_registro, rg.fch_fin_revisa
                FROM   {S}revisado_g rg
                WHERE  rg.guia = :numPartida
                ORDER  BY rg.numero";
            await using var cmdG = new OracleCommand(sqlG, conn);
            cmdG.BindByName = true;
            cmdG.Parameters.Add("numPartida", numPartida);
            await using var rG = await cmdG.ExecuteReaderAsync();
            while (await rG.ReadAsync())
            {
                result.Revisados.Add(new PlnRevisado
                {
                    Numero       = SafeVal<long>(rG["NUMERO"]),
                    Guia         = SafeVal<long>(rG["GUIA"]),
                    MaqProced    = SafeStr(rG["MAQ_PROCED"]),
                    FchRegistro  = SafeVal<DateTime>(rG["FCH_REGISTRO"]),
                    FchFinRevisa = rG["FCH_FIN_REVISA"] == DBNull.Value ? null : SafeVal<DateTime?>(rG["FCH_FIN_REVISA"]),
                });
            }
            // Detalles REVISADO_D — un solo query para todos los números
            if (result.Revisados.Count > 0)
            {
                var numeros = string.Join(",", result.Revisados.Select(x => x.Numero));
                var sqlD = $@"
                    SELECT rd.numero, rd.item, rd.fecha,
                           rd.c_codigo AS revisador, rd.turno,
                           NVL(rd.aprobado,0)  AS aprobado,
                           NVL(rd.rechazado,0) AS rechazado,
                           NVL(rd.faltante,0)  AS faltante,
                           NVL(rd.merma,0)     AS merma,
                           rd.observacion
                    FROM   {S}revisado_d rd
                    WHERE  rd.numero IN ({numeros})
                    ORDER  BY rd.numero, rd.item";
                await using var cmdD = new OracleCommand(sqlD, conn);
                await using var rD = await cmdD.ExecuteReaderAsync();
                while (await rD.ReadAsync())
                {
                    var numRev = SafeVal<long>(rD["NUMERO"]);
                    var rev    = result.Revisados.First(x => x.Numero == numRev);
                    rev.Detalle.Add(new PlnRevisadoDet
                    {
                        Item        = SafeVal<int>(rD["ITEM"]),
                        Fecha       = SafeVal<DateTime>(rD["FECHA"]),
                        Revisador   = SafeStr(rD["REVISADOR"]),
                        Turno       = SafeStr(rD["TURNO"]),
                        Aprobado    = SafeVal<decimal>(rD["APROBADO"]),
                        Rechazado   = SafeVal<decimal>(rD["RECHAZADO"]),
                        Faltante    = SafeVal<decimal>(rD["FALTANTE"]),
                        Merma       = SafeVal<decimal>(rD["MERMA"]),
                        Observacion = rD["OBSERVACION"] == DBNull.Value ? null : SafeStr(rD["OBSERVACION"]),
                    });
                }
            }
        }

        // ── 10. Pesaje Ingreso Almacén PT (LOTES TP='16' + KARDEX_D por COD_ART) ────
        {
            var sql = $@"
                SELECT l.cod_alm, l.tp_transac, l.serie, l.numero,
                       MIN(kd.fch_transac)    AS fecha,
                       COUNT(l.lote)          AS lotes_etiq,
                       ROUND(kd.cantidad, 3)  AS peso_pesado
                FROM   {S}lotes l
                JOIN   {S}kardex_d kd
                    ON  kd.cod_alm   = l.cod_alm
                    AND kd.tp_transac = l.tp_transac
                    AND kd.serie     = l.serie
                    AND kd.numero    = l.numero
                    AND kd.cod_art   = l.cod_art
                WHERE  l.partida    = :numPartida
                  AND  l.tp_transac = '16'
                GROUP  BY l.cod_alm, l.tp_transac, l.serie, l.numero, kd.cantidad
                ORDER  BY MIN(kd.fch_transac)";
            await using var cmd = new OracleCommand(sql, conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("numPartida", numPartida);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                result.PesajesAlmacen.Add(new PlnPesajeAlmacen
                {
                    CodAlm     = SafeStr(r["COD_ALM"]),
                    TpTransac  = SafeStr(r["TP_TRANSAC"]),
                    Serie      = SafeVal<int>(r["SERIE"]),
                    Numero     = SafeVal<long>(r["NUMERO"]),
                    Fecha      = SafeVal<DateTime>(r["FECHA"]),
                    LotesEtiq  = SafeVal<int>(r["LOTES_ETIQ"]),
                    PesoPesado = SafeVal<decimal>(r["PESO_PESADO"]),
                });
            }
        }

        // ── 11. Despachos Producto Terminado (LOTES S_TRANSAC='21'/'23' + KARDEX_G) ────
        {
            var sql = $@"
                SELECT l.cod_alm, l.s_transac, l.serie, l.numero,
                       MIN(l.fec_salida)      AS fecha,
                       COUNT(l.lote)          AS lotes,
                       SUM(l.cantidad)        AS unidades,
                       ROUND(SUM(l.saldo), 2) AS peso_kg,
                       kg.glosa
                FROM   {S}lotes l
                LEFT JOIN {S}kardex_g kg
                    ON  kg.cod_alm   = l.cod_alm
                    AND kg.tp_transac = l.s_transac
                    AND kg.serie     = l.serie
                    AND kg.numero    = l.numero
                WHERE  l.partida    = :numPartida
                  AND  l.s_transac IN ('21','23')
                GROUP  BY l.cod_alm, l.s_transac, l.serie, l.numero, kg.glosa
                ORDER  BY MIN(l.fec_salida)";
            await using var cmd = new OracleCommand(sql, conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("numPartida", numPartida);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                result.Despachos.Add(new PlnDespachoProducto
                {
                    CodAlm    = SafeStr(r["COD_ALM"]),
                    TpTransac = SafeStr(r["S_TRANSAC"]),
                    Serie     = SafeVal<int>(r["SERIE"]),
                    Numero    = SafeVal<long>(r["NUMERO"]),
                    Fecha     = SafeVal<DateTime>(r["FECHA"]),
                    Lotes     = SafeVal<int>(r["LOTES"]),
                    Unidades  = SafeVal<decimal>(r["UNIDADES"]),
                    PesoKg    = SafeVal<decimal>(r["PESO_KG"]),
                    Glosa     = r["GLOSA"] == DBNull.Value ? null : SafeStr(r["GLOSA"]),
                });
            }
        }

        // ── 12. Rectificaciones de receta (L_RECTIFICA_RECETA vía GUIA=PARTIDA.NUMERO) ──
        {
            var sql = $@"
                SELECT r.numero,
                       r.fecha                AS fch_registro,
                       r.area, r.situacion, r.estado,
                       hl.abreviado           AS laboratorista,
                       hs.abreviado           AS supervisor,
                       r.proceso, r.cod_causa, r.defecto_orig, r.observacion,
                       r.marca_enproc,
                       r.f_enproceso          AS fch_en_proceso,
                       r.marca_rectif,
                       r.f_rectificado        AS fch_rectificado,
                       r.marca_aprob,
                       r.f_aprobado           AS fch_aprobado
                FROM   {S}l_rectifica_receta r
                LEFT   JOIN {S}h_tprod hl ON hl.tabla = '09' AND hl.codigo = r.c_laboratorista
                LEFT   JOIN {S}h_tprod hs ON hs.tabla = '09' AND hs.codigo = r.supervisor
                WHERE  r.guia  = :numPartida
                  AND  r.estado <> '9'
                ORDER  BY r.numero DESC";
            await using var cmd = new OracleCommand(sql, conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("numPartida", numPartida);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var lab = SafeStr(r["LABORATORISTA"]);
                var sup = SafeStr(r["SUPERVISOR"]);
                var obs = SafeStr(r["OBSERVACION"]);
                result.RectificacionesReceta.Add(new PlnRectificacionReceta
                {
                    Numero         = SafeVal<long>(r["NUMERO"]),
                    FchRegistro    = SafeVal<DateTime>(r["FCH_REGISTRO"]),
                    Area           = SafeStr(r["AREA"]),
                    Situacion      = SafeStr(r["SITUACION"]),
                    Estado         = SafeStr(r["ESTADO"]),
                    Laboratorista  = string.IsNullOrEmpty(lab) ? null : lab,
                    Supervisor     = string.IsNullOrEmpty(sup) ? null : sup,
                    Proceso        = SafeStr(r["PROCESO"]),
                    DefectoOrig    = SafeStr(r["DEFECTO_ORIG"]),
                    CodCausa       = SafeStr(r["COD_CAUSA"]),
                    Observacion    = string.IsNullOrEmpty(obs) ? null : obs,
                    MarcaEnproc    = SafeStr(r["MARCA_ENPROC"]),
                    FchEnProceso   = SafeDate(r["FCH_EN_PROCESO"]),
                    MarcaRectif    = SafeStr(r["MARCA_RECTIF"]),
                    FchRectificado = SafeDate(r["FCH_RECTIFICADO"]),
                    MarcaAprob     = SafeStr(r["MARCA_APROB"]),
                    FchAprobado    = SafeDate(r["FCH_APROBADO"]),
                });
            }
        }

        return result;
    }

    // ── Helpers de descripciones de dominio TT ──────────────────────────────────

    private static string DescProceso(string? proceso) => proceso?.ToUpper() switch
    {
        "TEAC"   => "Teñido y Acabado",
        "BQM"    => "Blanqueo Químico",
        "BLAN"   => "Blanqueo",
        "TINTURA"=> "Tintura",
        "ACID"   => "Acidulado",
        "FIJA"   => "Fijado",
        _        => proceso ?? ""
    };

    private static string DescCalificacion(string? cal) => cal?.ToUpper() switch
    {
        "AP" => "Aprobado",
        "RE" => "Rechazado",
        "OB" => "Observado",
        "OK" => "Correcto",
        _    => string.IsNullOrEmpty(cal) ? "—" : cal
    };

    private static string DescEstEvaluacion(string? e) => e switch
    {
        "31" => "Pendiente",
        "32" => "Evaluado",
        "33" => "Observado",
        _    => string.IsNullOrEmpty(e) ? "—" : e
    };

    private static string DescResultadoCC(string? r) => r switch
    {
        "01" => "Aprobado",
        "02" => "Rechazado",
        "03" => "Aprobado con observación",
        _    => string.IsNullOrEmpty(r) ? "—" : r
    };

    private static string DescEstadoSecado(string? estado) => estado switch
    {
        "1" => "En proceso",
        "2" => "Finalizado",
        "3" => "Completado",
        _   => string.IsNullOrEmpty(estado) ? "—" : estado
    };

    private static string DescEstadoLab(int estado) => estado switch
    {
        1 => "En proceso",
        2 => "Observado",
        3 => "Validado",
        4 => "Rechazado",
        _ => estado.ToString()
    };

    private static string DescEstadoReceta(int estado) => estado switch
    {
        1 => "Ingresada",
        2 => "Aprobada",
        3 => "En proceso",
        4 => "Completada",
        5 => "Procesada",
        6 => "Finalizada",
        _ => estado.ToString()
    };
}
