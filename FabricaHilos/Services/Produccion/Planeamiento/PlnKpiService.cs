using System.Data;
using Oracle.ManagedDataAccess.Client;
using FabricaHilos.Models.Produccion.Planeamiento;

namespace FabricaHilos.Services.Produccion.Planeamiento;

public class PlnKpiService : OracleServiceBase, IPlnKpiService
{
    public PlnKpiService(
        IConfiguration       configuration,
        IHttpContextAccessor httpContextAccessor)
        : base(configuration, httpContextAccessor) { }

    private static string SafeStr(object? v) =>
        v == null || v == DBNull.Value ? "" : v.ToString()!;

    private static T SafeVal<T>(object? v, T def = default!) =>
        v == null || v == DBNull.Value ? def : (T)Convert.ChangeType(v, typeof(T));

    private static async Task<T> ScalarAsync<T>(OracleConnection conn, string sql, T def = default!)
    {
        await using var cmd = new OracleCommand(sql, conn);
        var val = await cmd.ExecuteScalarAsync();
        return val == null || val == DBNull.Value ? def : (T)Convert.ChangeType(val, typeof(T));
    }

    public async Task<PlnKpiResumen> GetResumenAsync()
    {
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();

        // V_PLN_KPI_CUMPLIMIENTO (§8.7): OTIF mensual con filtro correcto ESTADO='C' AND COD_PASO_ACT='14'
        var sqlOtif = $@"
            SELECT periodo,
                   total_items_cerrados        AS total_pedidos,
                   entregados_a_tiempo         AS pedidos_a_tiempo,
                   entregados_tarde            AS pedidos_retrasados,
                   pct_otif,
                   ciclo_promedio_dias,
                   dias_prom_tintoreria,
                   dias_prom_pedido_partida,
                   retraso_promedio_dias,
                   kg_total_despachados        AS kg_despachados
            FROM   {S}V_PLN_KPI_CUMPLIMIENTO
            WHERE  periodo >= ADD_MONTHS(TRUNC(SYSDATE,'MM'), -6)";

        var otif = new List<PlnKpi>();
        await using (var cmd = new OracleCommand(sqlOtif, conn))
        await using (var r   = await cmd.ExecuteReaderAsync())
        {
            while (await r.ReadAsync())
            {
                otif.Add(new PlnKpi
                {
                    Periodo                = SafeVal<DateTime>(r["periodo"]),
                    TotalItemsCerrados     = SafeVal<int>(r["total_pedidos"]),
                    EntregadosATiempo      = SafeVal<int>(r["pedidos_a_tiempo"]),
                    EntregadosTarde        = SafeVal<int>(r["pedidos_retrasados"]),
                    PctOtif                = SafeVal<double>(r["pct_otif"]),
                    CicloPromedioDias      = SafeVal<double>(r["ciclo_promedio_dias"]),
                    DiasPromTintoreria     = SafeVal<double>(r["dias_prom_tintoreria"]),
                    DiasPromPedidoPartida  = SafeVal<double>(r["dias_prom_pedido_partida"]),
                    RetrasoPromedioDias    = SafeVal<double>(r["retraso_promedio_dias"]),
                    KgTotalDespachados     = SafeVal<decimal>(r["kg_despachados"]),
                });
            }
        }

        // Tasa reproceso — V_PLN_ESTADO_ITEM §8.2: NRO_CICLO > 1 indica al menos un reproceso
        var tasaReproceso = await ScalarAsync<double>(conn, $@"
            SELECT ROUND(SUM(CASE WHEN nro_ciclo > 1 THEN 1.0 ELSE 0 END)
                         / NULLIF(COUNT(*),0) * 100, 1)
            FROM   {S}V_PLN_ESTADO_ITEM
            WHERE  estado_seguim IN ('A','C')");

        // Retrasos: SEMAFORO 'R'=días>=7 (crítico), 'A'=días 3-6 (alto) — §8.2
        var criticos = await ScalarAsync<int>(conn, $@"
            SELECT COUNT(*) FROM {S}V_PLN_ESTADO_ITEM
            WHERE  estado_seguim = 'A' AND semaforo = 'R'");

        var altos = await ScalarAsync<int>(conn, $@"
            SELECT COUNT(*) FROM {S}V_PLN_ESTADO_ITEM
            WHERE  estado_seguim = 'A' AND semaforo = 'A'");

        // Retrasos por área: V_PLN_ESTADO_ITEM §8.2 (agrupa por cod_paso_act)
        var sqlArea = $@"
            SELECT v.cod_paso_act                    AS cod_paso,
                   COUNT(*)                          AS cant_retrasos,
                   ROUND(AVG(v.dias_retraso), 1)     AS dias_promedio
            FROM   {S}V_PLN_ESTADO_ITEM v
            WHERE  v.estado_seguim = 'A' AND v.ind_retraso = 'S'
            GROUP  BY v.cod_paso_act
            ORDER  BY 2 DESC";

        var areas = new List<PlnRetrasoArea>();
        await using (var cmd = new OracleCommand(sqlArea, conn))
        await using (var r   = await cmd.ExecuteReaderAsync())
        {
            while (await r.ReadAsync())
            {
                var codPaso = SafeStr(r["cod_paso"]);
                var area = codPaso switch
                {
                    "01"             => "Ventas",
                    "02"             => "Planeamiento",
                    "03" or "04"     => "Hilandería",        // '03'=En Hilandería, '04'=Lote Disponible
                    "05"             => "Laboratorio",       // '05'=Laboratorio (L_VALIDA_RECETA)
                    "06" or "07" or "08" or "9R" => "Tintorería",
                    "09"             => "Calidad",
                    "09B"            => "Acabados",
                    "10"             => "Devanado",
                    "11"             => "Calidad",
                    "12" or "13"     => "Almacén PT",
                    "14"             => "Despacho",
                    _                => "Otros"
                };
                areas.Add(new PlnRetrasoArea
                {
                    Area         = area,
                    CantRetrasos = SafeVal<int>(r["cant_retrasos"]),
                    DiasPromedio = SafeVal<double>(r["dias_promedio"]),
                });
            }
        }

        return new PlnKpiResumen
        {
            OtifMensual        = otif.AsReadOnly(),
            TasaReproceso      = tasaReproceso,
            RetrasosCriticos   = criticos,
            RetrasosAltos      = altos,
            CicloPromedioTotal = otif.Count > 0 ? otif.Average(k => k.CicloPromedioDias) : 0,
            RetrasosPorArea    = areas.AsReadOnly(),
        };
    }

    public async Task<IEnumerable<PlnCargaDiaria>> GetCargaMaquinasAsync()
    {
        // V_PLN_CARGA_MAQUINAS (§8.5): ventana 30 días, incluye ESTADO_CARGA y PCT_CARGA.
        // ORA-00904 corregido: PLN_CARGA_DIARIA no tiene COD_PASO ni AREA; TP_MAQ se usa
        // para derivar Area en el modelo (PlnCargaDiaria.Area).
        // Sin filtro de fecha adicional: la vista V_PLN_CARGA_MAQUINAS ya filtra internamente
        // BETWEEN TRUNC(SYSDATE) AND TRUNC(SYSDATE)+30, entregando la ventana completa de 30 días.
        var sql = $@"
            SELECT fecha, cod_maq, tp_maq,
                   horas_capacidad, kg_capacidad,
                   horas_asignadas, kg_asignados,
                   nro_pedidos,
                   horas_real, kg_real,
                   pct_utilizacion, pct_carga,
                   ind_sobrecargada, estado_carga
            FROM   {S}V_PLN_CARGA_MAQUINAS
            ORDER  BY fecha, tp_maq, cod_maq";

        var list = new List<PlnCargaDiaria>();
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd = new OracleCommand(sql, conn);
        await using var r   = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new PlnCargaDiaria
            {
                Fecha           = SafeVal<DateTime>(r["fecha"]),
                CodMaq          = SafeStr(r["cod_maq"]),
                TpMaq           = SafeStr(r["tp_maq"]),
                HorasCapacidad  = SafeVal<double>(r["horas_capacidad"]),
                KgCapacidad     = SafeVal<decimal>(r["kg_capacidad"]),
                HorasAsignadas  = SafeVal<double>(r["horas_asignadas"]),
                KgAsignados     = SafeVal<decimal>(r["kg_asignados"]),
                NroPedidos      = SafeVal<int>(r["nro_pedidos"]),
                HorasReal       = SafeVal<double>(r["horas_real"]),
                KgReal          = SafeVal<decimal>(r["kg_real"]),
                PctUtilizacion  = SafeVal<double>(r["pct_utilizacion"]),
                PctCarga        = SafeVal<double>(r["pct_carga"]),
                IndSobrecargada = SafeStr(r["ind_sobrecargada"]),
                EstadoCarga     = SafeStr(r["estado_carga"]),
            });
        }
        return list;
    }

    public async Task<IEnumerable<PlnCargaDiaria>> GetCargaMaquinasRangoAsync(DateTime fchIni, DateTime fchFin)
    {
        // Lee PLN_CARGA_DIARIA directamente (sin filtro de vista) para rangos arbitrarios.
        // V_PLN_CARGA_MAQUINAS solo cubre TRUNC(SYSDATE) a TRUNC(SYSDATE)+30.
        // Calcula ESTADO_CARGA con la misma lógica que la vista.
        var sql = $@"
            SELECT c.fecha, c.cod_maq, c.tp_maq,
                   c.horas_capacidad, c.kg_capacidad,
                   c.horas_asignadas, c.kg_asignados,
                   c.nro_pedidos,
                   c.horas_real, c.kg_real,
                   c.pct_utilizacion, c.pct_carga,
                   c.ind_sobrecargada,
                   CASE
                     WHEN c.pct_carga > 95 THEN 'SOBRECARGADA'
                     WHEN c.pct_carga > 80 THEN 'CARGA_ALTA'
                     WHEN c.pct_carga > 50 THEN 'CARGA_MEDIA'
                     ELSE 'DISPONIBLE'
                   END AS estado_carga
            FROM   {S}PLN_CARGA_DIARIA c
            WHERE  c.fecha BETWEEN TRUNC(:fchIni) AND TRUNC(:fchFin)
            ORDER  BY c.fecha, c.tp_maq, c.cod_maq";

        var list = new List<PlnCargaDiaria>();
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        cmd.Parameters.Add(new OracleParameter("fchIni", OracleDbType.Date) { Value = fchIni });
        cmd.Parameters.Add(new OracleParameter("fchFin", OracleDbType.Date) { Value = fchFin });
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new PlnCargaDiaria
            {
                Fecha           = SafeVal<DateTime>(r["fecha"]),
                CodMaq          = SafeStr(r["cod_maq"]),
                TpMaq           = SafeStr(r["tp_maq"]),
                HorasCapacidad  = SafeVal<double>(r["horas_capacidad"]),
                KgCapacidad     = SafeVal<decimal>(r["kg_capacidad"]),
                HorasAsignadas  = SafeVal<double>(r["horas_asignadas"]),
                KgAsignados     = SafeVal<decimal>(r["kg_asignados"]),
                NroPedidos      = SafeVal<int>(r["nro_pedidos"]),
                HorasReal       = SafeVal<double>(r["horas_real"]),
                KgReal          = SafeVal<decimal>(r["kg_real"]),
                PctUtilizacion  = SafeVal<double>(r["pct_utilizacion"]),
                PctCarga        = SafeVal<double>(r["pct_carga"]),
                IndSobrecargada = SafeStr(r["ind_sobrecargada"]),
                EstadoCarga     = SafeStr(r["estado_carga"]),
            });
        }
        return list;
    }

    public async Task<IEnumerable<PlnEstadoPedido>> GetEstadoPedidosAsync()
    {
        // V_PLN_ESTADO_PEDIDO (§8.1 PKG_PLN): resumen por pedido con avance y retrasos.
        // Filtra pedidos en estado '0','5','9' (vigentes).
        var sql = $@"
            SELECT serie, num_ped, fch_pedido, cod_cliente, nom_cliente,
                   estado_pedido, prioridad,
                   total_items, items_cerrados, items_pendientes, items_con_retraso,
                   kg_total_pedido, kg_despachados, kg_pendientes, pct_avance,
                   fch_entrega_minima, fch_ultimo_despacho,
                   max_dias_retraso, fch_est_despacho_max
            FROM   {S}V_PLN_ESTADO_PEDIDO
            ORDER  BY max_dias_retraso DESC, fch_entrega_minima";

        var list = new List<PlnEstadoPedido>();
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd = new OracleCommand(sql, conn);
        await using var r   = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new PlnEstadoPedido
            {
                Serie              = SafeVal<int>(r["serie"]),
                NumPed             = SafeVal<long>(r["num_ped"]),
                FchPedido          = SafeVal<DateTime>(r["fch_pedido"]),
                CodCliente         = SafeStr(r["cod_cliente"]),
                NomCliente         = SafeStr(r["nom_cliente"]),
                EstadoPedido       = SafeStr(r["estado_pedido"]),
                Prioridad          = SafeStr(r["prioridad"]),
                TotalItems         = SafeVal<int>(r["total_items"]),
                ItemsCerrados      = SafeVal<int>(r["items_cerrados"]),
                ItemsPendientes    = SafeVal<int>(r["items_pendientes"]),
                ItemsConRetraso    = SafeVal<int>(r["items_con_retraso"]),
                KgTotalPedido      = SafeVal<decimal>(r["kg_total_pedido"]),
                KgDespachados      = SafeVal<decimal>(r["kg_despachados"]),
                KgPendientes       = SafeVal<decimal>(r["kg_pendientes"]),
                PctAvance          = SafeVal<double>(r["pct_avance"]),
                FchEntregaMinima   = r["fch_entrega_minima"]  == DBNull.Value ? null : Convert.ToDateTime(r["fch_entrega_minima"]),
                FchUltimoDespacho  = r["fch_ultimo_despacho"] == DBNull.Value ? null : Convert.ToDateTime(r["fch_ultimo_despacho"]),
                MaxDiasRetraso     = SafeVal<int>(r["max_dias_retraso"]),
                FchEstDespachoMax  = r["fch_est_despacho_max"] == DBNull.Value ? null : Convert.ToDateTime(r["fch_est_despacho_max"]),
            });
        }
        return list;
    }

    public async Task<IEnumerable<PlnPendienteDespacho>> GetPendientesDespachoAsync()
    {
        // SQL directo (bypass vista) para incluir campos enriquecidos:
        // serie, num_det, color_ui, cod_maq_devan, proceso, kg_producidos, dias_en_paso.
        var sql = $@"
            SELECT s.serie, s.num_ped, s.nro, s.num_det,
                   s.cod_cliente, cl.nombre            AS nom_cliente,
                   s.cod_art,     ar.descripcion       AS desc_art,
                   s.color, s.titulo, s.proceso,
                   s.kg_pendientes, s.kg_producidos,
                   NVL(al.stock, 0)                                          AS stock_disponible,
                   LEAST(s.kg_pendientes, NVL(al.stock, 0))                  AS kg_a_despachar,
                   s.fch_entrega_comp, s.fch_est_despacho,
                   TRUNC(SYSDATE) - s.fch_entrega_comp                       AS dias_vencido,
                   s.dias_retraso, s.ind_urgente, s.ind_retraso,
                   s.cod_paso_act, ec.nombre_paso, ec.color_ui,
                   p.prioridad                                                AS prioridad_pedido,
                   NVL((SELECT TRUNC(SYSDATE) - TRUNC(MIN(ev.fch_evento))
                        FROM   {S}pln_log_eventos ev
                        WHERE  ev.num_ped  = s.num_ped
                          AND  ev.serie    = s.serie
                          AND  ev.nro      = s.nro
                          AND  ev.cod_paso = s.cod_paso_act), 0)             AS dias_en_paso,
                   NVL((SELECT MAX(hp.kg_unidad)
                        FROM   {S}h_programacion hp
                        WHERE  hp.guia = s.num_partida
                          AND  hp.kg_unidad > 0), 0)                        AS kg_por_cono,
                   NVL(pa.nro_rmc, 0)                                        AS nro_rmc,
                   NVL(pa.rmc, '')                                            AS rmc
            FROM   {S}pln_seguimiento s
            JOIN   {S}pln_estado_codigo ec ON ec.cod_paso = s.cod_paso_act
            LEFT JOIN {S}clientes   cl ON cl.cod_cliente = s.cod_cliente
            LEFT JOIN {S}articul    ar ON ar.cod_art     = s.cod_art
            JOIN   {S}pedido         p ON p.num_ped = s.num_ped AND p.serie = s.serie
            LEFT JOIN {S}partida     pa ON pa.numero = s.num_partida
            LEFT JOIN (SELECT cod_art, SUM(NVL(stock, 0)) AS stock
                       FROM   {S}almacen
                       WHERE  cod_alm IN ('03','07','22','30')
                       GROUP BY cod_art)                al ON al.cod_art = s.cod_art
            WHERE  s.cod_paso_act IN ('12','13')
              AND  s.kg_pendientes > 0
              AND  s.estado = 'A'
            ORDER BY CASE WHEN s.ind_urgente='S' THEN 0 ELSE 1 END,
                     p.prioridad DESC NULLS LAST,
                     s.fch_entrega_comp NULLS LAST";

        var list = new List<PlnPendienteDespacho>();
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd = new OracleCommand(sql, conn);
        await using var r   = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new PlnPendienteDespacho
            {
                Serie           = SafeVal<int>(r["serie"]),
                NumPed          = SafeVal<long>(r["num_ped"]),
                Nro             = SafeVal<int>(r["nro"]),
                NumDet          = SafeVal<int>(r["num_det"]),
                CodCliente      = SafeStr(r["cod_cliente"]),
                NomCliente      = SafeStr(r["nom_cliente"]),
                CodArt          = SafeStr(r["cod_art"]),
                DescArt         = SafeStr(r["desc_art"]),
                Color           = SafeStr(r["color"]),
                Titulo          = SafeStr(r["titulo"]),
                Proceso         = SafeStr(r["proceso"]),
                KgPendientes    = SafeVal<decimal>(r["kg_pendientes"]),
                KgProducidos    = SafeVal<decimal>(r["kg_producidos"]),
                StockDisponible = SafeVal<decimal>(r["stock_disponible"]),
                KgADespachar    = SafeVal<decimal>(r["kg_a_despachar"]),
                FchEntregaComp  = r["fch_entrega_comp"]  == DBNull.Value ? null : Convert.ToDateTime(r["fch_entrega_comp"]),
                FchEstDespacho  = r["fch_est_despacho"]  == DBNull.Value ? null : Convert.ToDateTime(r["fch_est_despacho"]),
                DiasVencido     = SafeVal<int>(r["dias_vencido"]),
                DiasRetraso     = SafeVal<int>(r["dias_retraso"]),
                IndUrgente      = SafeStr(r["ind_urgente"]),
                IndRetraso      = SafeStr(r["ind_retraso"]),
                CodPasoAct      = SafeStr(r["cod_paso_act"]),
                NombrePaso      = SafeStr(r["nombre_paso"]),
                ColorUi         = SafeStr(r["color_ui"]),
                PrioridadPedido = SafeStr(r["prioridad_pedido"]),
                DiasEnPaso      = SafeVal<int>(r["dias_en_paso"]),
                KgPorCono       = SafeVal<decimal>(r["kg_por_cono"]),
                NumConos        = SafeVal<decimal>(r["kg_por_cono"]) > 0
                                  ? (int)Math.Round(SafeVal<decimal>(r["kg_pendientes"]) / SafeVal<decimal>(r["kg_por_cono"]))
                                  : 0,
                NroRmc          = SafeVal<int>(r["nro_rmc"]),
                Rmc             = SafeStr(r["rmc"]),
            });
        }
        return list;
    }

    public async Task<IEnumerable<PlnPendienteDespacho>> GetProximosDespachoAsync()
    {
        // Ítems en pasos '08'-'11': próximos a llegar a Almacén PT.
        // Ordenados por FCH_ENTREGA_COMP ascendente (más urgentes primero).
        var sql = $@"
            SELECT s.serie, s.num_ped, s.nro, s.num_det,
                   s.cod_cliente, cl.nombre            AS nom_cliente,
                   s.cod_art,     ar.descripcion       AS desc_art,
                   s.color, s.titulo, s.proceso,
                   s.kg_pendientes, s.kg_producidos,
                   s.fch_entrega_comp,
                   s.fch_est_despacho,
                   TRUNC(SYSDATE) - s.fch_entrega_comp                        AS dias_vencido,
                   s.dias_retraso, s.ind_urgente, s.ind_retraso,
                   s.cod_paso_act, ec.nombre_paso, ec.color_ui,
                   p.prioridad                                                 AS prioridad_pedido,
                   NVL(pa.nro_rmc, 0)                                         AS nro_rmc,
                   NVL(pa.rmc, '')                                             AS rmc,
                   -- Fecha real de inicio del paso actual (para DiasEnPaso)
                   CASE s.cod_paso_act
                     WHEN '08'  THEN s.fch_real_secado
                     WHEN '09'  THEN s.fch_real_cc_tinto
                     WHEN '09B' THEN s.fch_real_gaseado
                     WHEN '9R'  THEN s.fch_real_cc_rechazo
                     WHEN '10'  THEN s.fch_real_devanado
                     WHEN '11'  THEN s.fch_real_calidad
                     ELSE NULL
                   END                                                         AS fch_ini_paso,
                   NVL(TRUNC(SYSDATE) - TRUNC(
                     CASE s.cod_paso_act
                       WHEN '08'  THEN s.fch_real_secado
                       WHEN '09'  THEN s.fch_real_cc_tinto
                       WHEN '09B' THEN s.fch_real_gaseado
                       WHEN '9R'  THEN s.fch_real_cc_rechazo
                       WHEN '10'  THEN s.fch_real_devanado
                       WHEN '11'  THEN s.fch_real_calidad
                       ELSE NULL
                     END), 0)                                                  AS dias_en_paso
            FROM   {S}pln_seguimiento s
            JOIN   {S}pln_estado_codigo ec ON ec.cod_paso = s.cod_paso_act
            LEFT JOIN {S}clientes   cl ON cl.cod_cliente = s.cod_cliente
            LEFT JOIN {S}articul    ar ON ar.cod_art     = s.cod_art
            JOIN   {S}pedido         p ON p.num_ped = s.num_ped AND p.serie = s.serie
            LEFT JOIN {S}partida    pa ON pa.numero = s.num_partida
            WHERE  s.estado = 'A'
              AND  s.cod_paso_act IN ('08','09','09B','9R','10','11')
            ORDER BY CASE WHEN s.ind_urgente='S' THEN 0 ELSE 1 END,
                     s.fch_entrega_comp NULLS LAST,
                     ec.orden_paso";

        var list = new List<PlnPendienteDespacho>();
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd = new OracleCommand(sql, conn);
        await using var r   = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new PlnPendienteDespacho
            {
                Serie           = SafeVal<int>(r["serie"]),
                NumPed          = SafeVal<long>(r["num_ped"]),
                Nro             = SafeVal<int>(r["nro"]),
                NumDet          = SafeVal<int>(r["num_det"]),
                CodCliente      = SafeStr(r["cod_cliente"]),
                NomCliente      = SafeStr(r["nom_cliente"]),
                CodArt          = SafeStr(r["cod_art"]),
                DescArt         = SafeStr(r["desc_art"]),
                Color           = SafeStr(r["color"]),
                Titulo          = SafeStr(r["titulo"]),
                Proceso         = SafeStr(r["proceso"]),
                KgPendientes    = SafeVal<decimal>(r["kg_pendientes"]),
                KgProducidos    = SafeVal<decimal>(r["kg_producidos"]),
                StockDisponible = 0,
                KgADespachar    = 0,
                FchEntregaComp  = r["fch_entrega_comp"]  == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(r["fch_entrega_comp"]),
                FchEstDespacho  = r["fch_est_despacho"]  == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(r["fch_est_despacho"]),
                DiasVencido     = SafeVal<int>(r["dias_vencido"]),
                DiasRetraso     = SafeVal<int>(r["dias_retraso"]),
                DiasEnPaso      = SafeVal<int>(r["dias_en_paso"]),
                IndUrgente      = SafeStr(r["ind_urgente"]),
                IndRetraso      = SafeStr(r["ind_retraso"]),
                CodPasoAct      = SafeStr(r["cod_paso_act"]),
                NombrePaso      = SafeStr(r["nombre_paso"]),
                ColorUi         = SafeStr(r["color_ui"]),
                PrioridadPedido = SafeStr(r["prioridad_pedido"]),
                NroRmc          = SafeVal<int>(r["nro_rmc"]),
                Rmc             = SafeStr(r["rmc"]),
            });
        }
        return list;
    }

    public async Task<IEnumerable<PlnKpiProduccion>> GetKpiProduccionAsync()
    {
        // V_PLN_KPI_PRODUCCION (§8.8 PKG_PLN): KPIs de eficiencia por máquina.
        // Ventana: últimos 12 meses desde H_PRODUCCION_D.
        var sql = $@"
            SELECT periodo, tp_maq, cod_maq,
                   kg_producidos, horas_prom_turno, horas_prom_parada,
                   kg_por_hora, dias_activos
            FROM   {S}V_PLN_KPI_PRODUCCION
            ORDER  BY periodo DESC, tp_maq, cod_maq";

        var list = new List<PlnKpiProduccion>();
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd = new OracleCommand(sql, conn);
        await using var r   = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new PlnKpiProduccion
            {
                Periodo          = SafeVal<DateTime>(r["periodo"]),
                TpMaq            = SafeStr(r["tp_maq"]),
                CodMaq           = SafeStr(r["cod_maq"]),
                KgProducidos     = SafeVal<decimal>(r["kg_producidos"]),
                HorasPromTurno   = SafeVal<double>(r["horas_prom_turno"]),
                HorasPromParada  = SafeVal<double>(r["horas_prom_parada"]),
                KgPorHora        = SafeVal<double>(r["kg_por_hora"]),
                DiasActivos      = SafeVal<int>(r["dias_activos"]),
            });
        }
        return list;
    }

    public async Task RefreshCargaDiariaAsync(DateTime fchIni, DateTime fchFin)
    {
        // PKG_PLN.SP_PLN_CARGA_DIARIA_REFRESH (§6 PKG_PLN): recalcula PLN_CARGA_DIARIA
        // para el rango dado (DELETE + INSERT desde h_produccion_d).
        // Normalmente lo ejecuta JOB_PLN_CARGA a las 23:30; disponible aquí para refresco manual.
        const string sql =
            "BEGIN PKG_PLN.SP_PLN_CARGA_DIARIA_REFRESH(:fchIni,:fchFin); END;";
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        cmd.Parameters.Add(new OracleParameter("fchIni", OracleDbType.Date) { Value = fchIni });
        cmd.Parameters.Add(new OracleParameter("fchFin", OracleDbType.Date) { Value = fchFin });
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Compromisos de máquinas activos.
    /// Combina cuatro fuentes:
    ///   1. PLN_SEGUIMIENTO.COD_MAQ_SECADO  → compromisos de secadoras (S01, S04)
    ///   2. PLN_SEGUIMIENTO.COD_MAQ_DEVAN   → compromisos de devanadoras/retorcedoras
    ///   3. TT_RSECADO activo               → procesos reales sin PLN tracking
    ///   4. TT_RPRODUC (TT baths) activo    → tintorería en proceso sin PLN tracking
    /// Las fuentes 3 y 4 solo muestran ítems NO presentes en PLN_SEGUIMIENTO.
    /// </summary>
    public async Task<IEnumerable<PlnMaquinaCompromiso>> GetMaquinasCompromisoAsync()
    {
        var sql = $@"
            -- Fuente 1: Compromisos SECADO desde PLN_SEGUIMIENTO
            -- Fuente 1: Compromisos SECADO desde PLN_SEGUIMIENTO
            SELECT 'Secado'    AS area,
                   s.cod_maq_secado AS cod_maq,
                   s.num_ped, s.nro, s.num_det, s.serie,
                   s.cod_paso_act,
                   ec.nombre_paso,
                   ec.color_ui,
                   s.fch_entrega_comp,
                   s.kg_en_tin  AS kg,
                   s.ind_retraso,
                   s.dias_retraso,
                   CASE
                     -- Secado físico terminado (TT_RSECADO.FECHA_FIN existe) → COMPROMETIDA
                     -- aunque PLN sigue en PASO '08'. Coherente con Línea de Tiempo.
                     WHEN s.cod_paso_act = '08' AND trs_fin.fecha_fin IS NOT NULL THEN 'COMPROMETIDA'
                     WHEN s.cod_paso_act = '08'                                   THEN 'EN_PROCESO'
                     WHEN s.cod_paso_act IN ('06','07','9R')                      THEN 'COMPROMETIDA'
                     ELSE 'ASIGNADA'
                   END AS estado_maq,
                   trs_fin.fecha_fin AS fecha_fin_fisico,
                   'PLN' AS fuente
            FROM   {S}pln_seguimiento s
            JOIN   {S}pln_estado_codigo ec ON ec.cod_paso = s.cod_paso_act
            -- Detecta si el secado físico ya terminó en TT_RSECADO
            LEFT JOIN (SELECT guia, cod_maq, MAX(fecha_fin) AS fecha_fin
                       FROM   {S}tt_rsecado
                       WHERE  fecha_fin IS NOT NULL
                       GROUP BY guia, cod_maq) trs_fin
                   ON  trs_fin.guia    = s.num_partida
                   AND trs_fin.cod_maq = s.cod_maq_secado
            WHERE  s.estado          = 'A'
              AND  s.cod_maq_secado IS NOT NULL
              AND  s.cod_paso_act NOT IN ('09','09B','10','11','12','13','14')  -- excluir items ya pasados por secado
            UNION ALL
            -- Fuente 2: Compromisos DEVANADO desde PLN_SEGUIMIENTO
            SELECT 'Devanado'  AS area,
                   s.cod_maq_devan  AS cod_maq,
                   s.num_ped, s.nro, s.num_det, s.serie,
                   s.cod_paso_act,
                   ec.nombre_paso,
                   ec.color_ui,
                   s.fch_entrega_comp,
                   s.kg_producidos  AS kg,
                   s.ind_retraso,
                   s.dias_retraso,
                   CASE
                     WHEN s.cod_paso_act = '10'               THEN 'EN_PROCESO'
                     WHEN s.cod_paso_act IN ('08','09','09B','9R') THEN 'COMPROMETIDA'
                     ELSE 'ASIGNADA'
                   END AS estado_maq,
                   CAST(NULL AS DATE) AS fecha_fin_fisico,
                   'PLN' AS fuente
            FROM   {S}pln_seguimiento s
            JOIN   {S}pln_estado_codigo ec ON ec.cod_paso = s.cod_paso_act
            WHERE  s.estado          = 'A'
              AND  s.cod_maq_devan  IS NOT NULL
              AND  s.cod_paso_act NOT IN ('11','12','13','14')  -- excluir items ya pasados por devanado
            UNION ALL
            -- Fuente 3: SECADO físico activo (TT_RSECADO), solo ítems SIN PLN tracking
            SELECT 'Secado'    AS area,
                   t.cod_maq,
                   NVL(id.num_ped, 0),
                   NVL(id.nro,     0),
                   NVL(id.num_det, 0),
                   NVL(id.serie,   0),
                   '08'               AS cod_paso_act,
                   'Secado'           AS nombre_paso,
                   '#20c997'          AS color_ui,
                   CAST(NULL AS DATE) AS fch_entrega_comp,
                   NVL(t.peso_neto, 0),
                   'N'                AS ind_retraso,
                   0                  AS dias_retraso,
                   'EN_PROCESO'       AS estado_maq,
                   t.fecha_fin        AS fecha_fin_fisico,
                   'TT_RSECADO'       AS fuente
            FROM   {S}tt_rsecado t
            JOIN   {S}partida p ON p.numero = t.guia
            LEFT JOIN {S}itemped_det id ON id.nroprog = p.nroprog
            WHERE  t.estado IN ('1','2')
              AND  NOT EXISTS (
                       SELECT 1 FROM {S}pln_seguimiento ps
                       WHERE  ps.num_ped       = id.num_ped
                         AND  ps.nro           = id.nro
                         AND  ps.num_det       = id.num_det
                         AND  ps.serie         = id.serie
                         AND  ps.estado        = 'A'
                         AND  ps.cod_maq_secado = t.cod_maq)
            UNION ALL
            -- Fuente 4: TINTORERÍA física activa (TT_RPRODUC TIPODOC='PA'), solo ítems SIN PLN tracking
            SELECT 'Tintorería' AS area,
                   tt.cod_maq,
                   NVL(id.num_ped, 0),
                   NVL(id.nro,     0),
                   NVL(id.num_det, 0),
                   NVL(id.serie,   0),
                   '06'               AS cod_paso_act,
                   'En Tintorería'    AS nombre_paso,
                   '#6f42c1'          AS color_ui,
                   CAST(NULL AS DATE) AS fch_entrega_comp,
                   NVL(p.peso_neto, 0),
                   'N'                AS ind_retraso,
                   0                  AS dias_retraso,
                   'EN_PROCESO'       AS estado_maq,
                   CAST(NULL AS DATE) AS fecha_fin_fisico,
                   'TT_RPRODUC'       AS fuente
            FROM   {S}tt_rproduc tt
            LEFT JOIN {S}partida p ON p.numero = tt.receta
            LEFT JOIN {S}itemped_det id ON id.nroprog = p.nroprog
            WHERE  tt.tipodoc = 'PA'
              AND  tt.estado  IN ('1','2')
              AND  NOT EXISTS (
                       SELECT 1 FROM {S}pln_seguimiento ps
                       WHERE  ps.num_ped = id.num_ped
                         AND  ps.nro     = id.nro
                         AND  ps.num_det = id.num_det
                         AND  ps.serie   = id.serie
                         AND  ps.estado  = 'A')
            ORDER BY 1, 2, 13 DESC, 10";   /* area, cod_maq, estado_maq(EN_PROCESO primero), fch_entrega_comp */

        var list = new List<PlnMaquinaCompromiso>();
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd  = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        await using var r    = (OracleDataReader)await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new PlnMaquinaCompromiso
            {
                Area           = SafeStr(r["area"]),
                CodMaq         = SafeStr(r["cod_maq"]),
                NumPed         = r.IsDBNull(r.GetOrdinal("num_ped"))   ? 0 : Convert.ToInt64(r["num_ped"]),
                Nro            = r.IsDBNull(r.GetOrdinal("nro"))       ? 0 : Convert.ToInt32(r["nro"]),
                NumDet         = r.IsDBNull(r.GetOrdinal("num_det"))   ? 0 : Convert.ToInt32(r["num_det"]),
                Serie          = r.IsDBNull(r.GetOrdinal("serie"))     ? 0 : Convert.ToInt32(r["serie"]),
                CodPasoAct     = SafeStr(r["cod_paso_act"]),
                NombrePaso     = SafeStr(r["nombre_paso"]),
                ColorUi        = SafeStr(r["color_ui"]),
                FchEntregaComp = r.IsDBNull(r.GetOrdinal("fch_entrega_comp")) ? null : (DateTime?)Convert.ToDateTime(r["fch_entrega_comp"]),
                Kg             = r.IsDBNull(r.GetOrdinal("kg"))        ? 0m  : Convert.ToDecimal(r["kg"]),
                IndRetraso     = SafeStr(r["ind_retraso"]),
                DiasRetraso    = r.IsDBNull(r.GetOrdinal("dias_retraso")) ? 0 : Convert.ToInt32(r["dias_retraso"]),
                EstadoMaq      = SafeStr(r["estado_maq"]),
                FechaFinFisico = r.IsDBNull(r.GetOrdinal("fecha_fin_fisico")) ? null : (DateTime?)Convert.ToDateTime(r["fecha_fin_fisico"]),
                Fuente         = SafeStr(r["fuente"]),
            });
        }
        return list;
    }

    // ── §NEW: Estado de máquinas Tintorería ─────────────────────────────────
    /// <summary>
    /// Estado en tiempo real de todas las máquinas TT (Thies R/Hank M/MR).
    /// Fuente: TT_RPRODUC catálogo. ACTIVA=proceso activo(estado 1/2), LIBRE=sin proceso activo.
    /// Navegación: IR → ING_RECETAS_G → PARTIDA_MAS → PARTIDA; PA → PARTIDA directo.
    /// </summary>
    public async Task<IEnumerable<PlnEstadoMaquinaTT>> GetEstadoMaquinasTintoreriaAsync()
    {
        var sql = $@"
            SELECT estado_maq, cod_maq, tipodoc, proceso,
                   num_ped, serie, nro, num_det,
                   nombre_cliente, cod_art, titulo,
                   cod_paso_act, nombre_paso, color_ui,
                   fch_entrega_comp, dias_retraso, ind_retraso, ind_urgente, kg,
                   descripcion
            FROM (
                -- Activas via IR: TT_RPRODUC → ING_RECETAS_G → PARTIDA_MAS → PARTIDA → ITEMPED_DET
                SELECT 'ACTIVA'                                     AS estado_maq,
                       tt.cod_maq,
                       tt.tipodoc,
                       tt.proceso,
                       CASE WHEN id.num_ped IS NOT NULL THEN id.num_ped
                            WHEN p.nro_pedido > 1000      THEN p.nro_pedido
                            ELSE 0 END                              AS num_ped,
                       CASE WHEN id.serie IS NOT NULL    THEN id.serie
                            WHEN p.nro_pedido > 1000      THEN p.serie
                            ELSE 0 END                              AS serie,
                       NVL(id.nro,     0)                           AS nro,
                       NVL(id.num_det, 0)                           AS num_det,
                       NVL(cl.nombre, CASE WHEN p.nro_pedido > 1000 THEN cl_p.nombre END) AS nombre_cliente,
                       NVL(it.cod_art, p.cod_art)                   AS cod_art,
                       NVL(it.titulo,  p.titulo)                    AS titulo,
                       NVL(s.cod_paso_act,  '06')                   AS cod_paso_act,
                       NVL(ec.nombre_paso, 'En Tintorería')         AS nombre_paso,
                       NVL(ec.color_ui,    '#6f42c1')               AS color_ui,
                       s.fch_entrega_comp,
                       NVL(s.dias_retraso, 0)                       AS dias_retraso,
                       NVL(s.ind_retraso,  'N')                     AS ind_retraso,
                       NVL(s.ind_urgente,  'N')                     AS ind_urgente,
                       NVL(s.kg_en_tin, 0)                          AS kg,
                       NVL(tm.descripcion, '-')                     AS descripcion
                FROM   {S}TT_RPRODUC tt
                LEFT   JOIN {S}T_MAQUINAS tm ON tm.cod_maq = tt.cod_maq AND tm.tp_maq = 'T'
                JOIN   {S}ING_RECETAS_G ig ON ig.numero  = tt.receta
                JOIN   {S}PARTIDA_MAS   pm ON pm.numero  = ig.r_numero
                LEFT   JOIN {S}PARTIDA p   ON p.numero    = pm.partida
                LEFT   JOIN {S}ITEMPED_DET id ON id.nroprog = p.nroprog AND id.serie = p.serie
                LEFT   JOIN {S}ITEMPED it  ON it.serie    = id.serie
                                          AND it.num_ped  = id.num_ped
                                          AND it.nro      = id.nro
                LEFT   JOIN {S}CLIENTES cl   ON cl.cod_cliente  = it.cod_cliente
                LEFT   JOIN {S}CLIENTES cl_p ON cl_p.cod_cliente = p.cod_cliente
                LEFT   JOIN {S}PLN_SEGUIMIENTO s ON s.serie   = id.serie
                                                AND s.num_ped = id.num_ped
                                                AND s.nro     = id.nro
                                                AND s.num_det = id.num_det
                                                AND s.estado  = 'A'
                LEFT   JOIN {S}PLN_ESTADO_CODIGO ec ON ec.cod_paso = s.cod_paso_act
                WHERE  tt.tipodoc = 'IR'
                  AND  tt.estado IN ('1','2')
                UNION ALL
                -- Activas via PA: TT_RPRODUC → PARTIDA directo
                SELECT 'ACTIVA'                                     AS estado_maq,
                       tt.cod_maq,
                       tt.tipodoc,
                       tt.proceso,
                       CASE WHEN id.num_ped IS NOT NULL THEN id.num_ped
                            WHEN p.nro_pedido > 1000      THEN p.nro_pedido
                            ELSE 0 END                              AS num_ped,
                       CASE WHEN id.serie IS NOT NULL    THEN id.serie
                            WHEN p.nro_pedido > 1000      THEN p.serie
                            ELSE 0 END                              AS serie,
                       NVL(id.nro,     0)                           AS nro,
                       NVL(id.num_det, 0)                           AS num_det,
                       NVL(cl.nombre, CASE WHEN p.nro_pedido > 1000 THEN cl_p.nombre END) AS nombre_cliente,
                       NVL(it.cod_art, p.cod_art)                   AS cod_art,
                       NVL(it.titulo,  p.titulo)                    AS titulo,
                       NVL(s.cod_paso_act,  '06')                   AS cod_paso_act,
                       NVL(ec.nombre_paso, 'En Tintorería')         AS nombre_paso,
                       NVL(ec.color_ui,    '#6f42c1')               AS color_ui,
                       s.fch_entrega_comp,
                       NVL(s.dias_retraso, 0)                       AS dias_retraso,
                       NVL(s.ind_retraso,  'N')                     AS ind_retraso,
                       NVL(s.ind_urgente,  'N')                     AS ind_urgente,
                       NVL(s.kg_en_tin, 0)                          AS kg,
                       NVL(tm.descripcion, '-')                     AS descripcion
                FROM   {S}TT_RPRODUC tt
                LEFT   JOIN {S}T_MAQUINAS tm ON tm.cod_maq = tt.cod_maq AND tm.tp_maq = 'T'
                LEFT   JOIN {S}PARTIDA p   ON p.numero = tt.receta
                LEFT   JOIN {S}ITEMPED_DET id ON id.nroprog = p.nroprog AND id.serie = p.serie
                LEFT   JOIN {S}ITEMPED it  ON it.serie    = id.serie
                                          AND it.num_ped  = id.num_ped
                                          AND it.nro      = id.nro
                LEFT   JOIN {S}CLIENTES cl   ON cl.cod_cliente  = it.cod_cliente
                LEFT   JOIN {S}CLIENTES cl_p ON cl_p.cod_cliente = p.cod_cliente
                LEFT   JOIN {S}PLN_SEGUIMIENTO s ON s.serie   = id.serie
                                                AND s.num_ped = id.num_ped
                                                AND s.nro     = id.nro
                                                AND s.num_det = id.num_det
                                                AND s.estado  = 'A'
                LEFT   JOIN {S}PLN_ESTADO_CODIGO ec ON ec.cod_paso = s.cod_paso_act
                WHERE  tt.tipodoc = 'PA'
                  AND  tt.estado IN ('1','2')
                UNION ALL
                -- Libres: TODAS las máquinas de Tintorería (R,M,MR) sin proceso activo
                SELECT 'LIBRE'                                      AS estado_maq,
                       maq.cod_maq,
                       '-'                                          AS tipodoc,
                       '-'                                          AS proceso,
                       0                                            AS num_ped,
                       0                                            AS serie,
                       0                                            AS nro,
                       0                                            AS num_det,
                       CAST(NULL AS VARCHAR2(100))                  AS nombre_cliente,
                       CAST(NULL AS VARCHAR2(25))                   AS cod_art,
                       CAST(NULL AS VARCHAR2(10))                   AS titulo,
                       ''                                           AS cod_paso_act,
                       ''                                           AS nombre_paso,
                       '#6c757d'                                    AS color_ui,
                       CAST(NULL AS DATE)                           AS fch_entrega_comp,
                       0                                            AS dias_retraso,
                       'N'                                          AS ind_retraso,
                       'N'                                          AS ind_urgente,
                       0                                            AS kg,
                       maq.descripcion                              AS descripcion
                FROM   {S}TT_MAQUINA maq
                WHERE  SUBSTR(maq.cod_maq, 1, 1) IN ('R','M')
                  AND  maq.cod_maq NOT IN (
                           SELECT act.cod_maq FROM {S}TT_RPRODUC act
                           WHERE  act.tipodoc IN ('PA','IR')
                             AND  act.estado  IN ('1','2'))
            )
            ORDER BY estado_maq ASC, cod_maq ASC";  /* ACTIVA < LIBRE alfabético */

        var list = new List<PlnEstadoMaquinaTT>();
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        await using var r = (OracleDataReader)await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new PlnEstadoMaquinaTT
            {
                EstadoMaq      = SafeStr(r["estado_maq"]),
                CodMaq         = SafeStr(r["cod_maq"]),
                Descripcion    = r.IsDBNull(r.GetOrdinal("descripcion")) ? null : SafeStr(r["descripcion"]),
                TipDoc         = SafeStr(r["tipodoc"]),
                Proceso        = SafeStr(r["proceso"]),
                NumPed         = r.IsDBNull(r.GetOrdinal("num_ped"))         ? 0  : Convert.ToInt64(r["num_ped"]),
                Nro            = r.IsDBNull(r.GetOrdinal("nro"))             ? 0  : Convert.ToInt32(r["nro"]),
                NumDet         = r.IsDBNull(r.GetOrdinal("num_det"))         ? 0  : Convert.ToInt32(r["num_det"]),
                Serie          = r.IsDBNull(r.GetOrdinal("serie"))           ? 0  : Convert.ToInt32(r["serie"]),
                NombreCliente  = r.IsDBNull(r.GetOrdinal("nombre_cliente"))  ? null : SafeStr(r["nombre_cliente"]),
                CodArt         = r.IsDBNull(r.GetOrdinal("cod_art"))         ? null : SafeStr(r["cod_art"]),
                Titulo         = r.IsDBNull(r.GetOrdinal("titulo"))          ? null : SafeStr(r["titulo"]),
                CodPasoAct     = SafeStr(r["cod_paso_act"]),
                NombrePaso     = SafeStr(r["nombre_paso"]),
                ColorUi        = SafeStr(r["color_ui"]),
                FchEntregaComp = r.IsDBNull(r.GetOrdinal("fch_entrega_comp")) ? null : (DateTime?)Convert.ToDateTime(r["fch_entrega_comp"]),
                DiasRetraso    = r.IsDBNull(r.GetOrdinal("dias_retraso"))     ? 0   : Convert.ToInt32(r["dias_retraso"]),
                IndRetraso     = SafeStr(r["ind_retraso"]),
                IndUrgente     = SafeStr(r["ind_urgente"]),
                Kg             = r.IsDBNull(r.GetOrdinal("kg"))               ? 0m  : Convert.ToDecimal(r["kg"]),
            });
        }
        return list;
    }

    // ── §NEW: Resumen de máquinas Hilandería ─────────────────────────────────
    /// <summary>
    /// Actividad de máquinas de hilandería (H_RPRODUC) últimas 24h, agrupada por tipo+máquina.
    /// Incluye lote, título, proceso, kg, husos, velocidad y estado activo/completado.
    /// </summary>
    public async Task<IEnumerable<PlnResumenHilanderia>> GetResumenHilanderiaAsync()
    {
        var sql = $@"
            SELECT h.tp_maq,
                   h.cod_maq,
                   MAX(h.lote)       AS lote,
                   MAX(h.titulo)     AS titulo,
                   MAX(h.proceso)    AS proceso,
                   MAX(h.estado)     AS estado_max,
                   SUM(h.peso_neto)  AS kg_total,
                   MAX(h.husos_act)  AS husos_max,
                   MAX(h.velocidad)  AS velocidad,
                   MAX(h.fecha_ini)  AS ultima_fecha_ini,
                   COUNT(*)          AS registros
            FROM   {S}H_RPRODUC h
            WHERE  h.fecha_ini >= TRUNC(SYSDATE) - 1
            GROUP BY h.tp_maq, h.cod_maq
            ORDER BY CASE MAX(h.estado) WHEN '1' THEN 0 WHEN '2' THEN 0 ELSE 1 END,
                     h.tp_maq, h.cod_maq";

        var list = new List<PlnResumenHilanderia>();
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        await using var r = (OracleDataReader)await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new PlnResumenHilanderia
            {
                TpMaq          = SafeStr(r["tp_maq"]),
                CodMaq         = SafeStr(r["cod_maq"]),
                Lote           = SafeStr(r["lote"]),
                Titulo         = SafeStr(r["titulo"]),
                Proceso        = SafeStr(r["proceso"]),
                EstadoMax      = SafeStr(r["estado_max"]),
                KgTotal        = r.IsDBNull(r.GetOrdinal("kg_total"))        ? 0m  : Convert.ToDecimal(r["kg_total"]),
                HusosMax       = r.IsDBNull(r.GetOrdinal("husos_max"))       ? 0   : Convert.ToInt32(r["husos_max"]),
                Velocidad      = r.IsDBNull(r.GetOrdinal("velocidad"))       ? 0.0 : Convert.ToDouble(r["velocidad"]),
                UltimaFechaIni = r.IsDBNull(r.GetOrdinal("ultima_fecha_ini")) ? null : (DateTime?)Convert.ToDateTime(r["ultima_fecha_ini"]),
                Registros      = r.IsDBNull(r.GetOrdinal("registros"))       ? 0   : Convert.ToInt32(r["registros"]),
            });
        }
        return list;
    }

    // ── §NEW: Estado de máquinas Secado ─────────────────────────────────
    /// <summary>
    /// Estado en tiempo real de TODAS las máquinas de secado (S01–S04).
    /// Catálogo: T_MAQUINAS TIPO_MAQ='S' (activas, ESTADO='0').
    /// Actividad: TT_RSECADO estado IN ('1','2').
    /// Para máquinas con múltiples lotes activos se toma el más reciente (ROW_NUMBER).
    /// Navegación: TT_RSECADO.GUIA → PARTIDA.NUMERO → PARTIDA.NROPROG → ITEMPED_DET → PLN_SEGUIMIENTO.
    /// </summary>
    public async Task<IEnumerable<PlnEstadoMaquinaTT>> GetEstadoMaquinasSecadoAsync()
    {
        var sql = $@"
            SELECT estado_maq, cod_maq, tipodoc, proceso,
                   num_ped, serie, nro, num_det,
                   nombre_cliente, cod_art, titulo,
                   cod_paso_act, nombre_paso, color_ui,
                   fch_entrega_comp, dias_retraso, ind_retraso, ind_urgente, kg,
                   descripcion
            FROM (
                -- Activas (una fila por máquina: el lote más reciente, ROW_NUMBER)
                SELECT q.*, ROW_NUMBER() OVER (PARTITION BY q.cod_maq ORDER BY q.fch_ini_sec DESC) AS rn
                FROM (
                    SELECT 'ACTIVA'                                     AS estado_maq,
                           t.cod_maq,
                           NVL(t.tipodoc, 'SE')                         AS tipodoc,
                           CASE WHEN NVL(t.resecado, 0) = 1 THEN 'RE' ELSE 'SE' END AS proceso,
                           CASE WHEN id.num_ped IS NOT NULL THEN id.num_ped
                                WHEN p.nro_pedido > 1000      THEN p.nro_pedido
                                ELSE 0 END                              AS num_ped,
                           CASE WHEN id.serie IS NOT NULL    THEN id.serie
                                WHEN p.nro_pedido > 1000      THEN p.serie
                                ELSE 0 END                              AS serie,
                           NVL(id.nro,     0)                           AS nro,
                           NVL(id.num_det, 0)                           AS num_det,
                           NVL(cl.nombre, cl_p.nombre)                  AS nombre_cliente,
                           NVL(it.cod_art, p.cod_art)                   AS cod_art,
                           NVL(it.titulo,  p.titulo)                    AS titulo,
                           NVL(s.cod_paso_act,  '08')                   AS cod_paso_act,
                           NVL(ec.nombre_paso, 'Secado')                AS nombre_paso,
                           NVL(ec.color_ui,    '#20c997')               AS color_ui,
                           s.fch_entrega_comp,
                           NVL(s.dias_retraso, 0)                       AS dias_retraso,
                           NVL(s.ind_retraso,  'N')                     AS ind_retraso,
                           NVL(s.ind_urgente,  'N')                     AS ind_urgente,
                           NVL(t.peso_neto, 0)                          AS kg,
                           NVL(tm.descripcion, '-')                     AS descripcion,
                           t.fecha_ini                                  AS fch_ini_sec
                    FROM   {S}TT_RSECADO t
                    LEFT   JOIN {S}TT_MAQUINA tm ON tm.cod_maq = t.cod_maq AND tm.tipo_maq = 'S'
                    LEFT   JOIN {S}PARTIDA p       ON p.numero   = t.guia
                    LEFT   JOIN {S}ITEMPED_DET id  ON id.nroprog = p.nroprog AND id.serie = p.serie
                    LEFT   JOIN {S}ITEMPED it      ON it.serie   = id.serie
                                                  AND it.num_ped = id.num_ped
                                                  AND it.nro     = id.nro
                    LEFT   JOIN {S}CLIENTES cl     ON cl.cod_cliente  = it.cod_cliente
                    LEFT   JOIN {S}CLIENTES cl_p   ON cl_p.cod_cliente = p.cod_cliente
                    LEFT   JOIN {S}PLN_SEGUIMIENTO s
                                                   ON s.serie   = id.serie
                                                  AND s.num_ped = id.num_ped
                                                  AND s.nro     = id.nro
                                                  AND s.num_det = id.num_det
                                                  AND s.estado  = 'A'
                    LEFT   JOIN {S}PLN_ESTADO_CODIGO ec ON ec.cod_paso = s.cod_paso_act
                    WHERE  t.estado IN ('1','2')
                ) q
            )
            WHERE rn = 1
            UNION ALL
            -- Libres: TT_MAQUINA tipo Secadora (S) sin proceso activo en TT_RSECADO
            SELECT 'LIBRE'                                      AS estado_maq,
                   maq.cod_maq,
                   '-'                                          AS tipodoc,
                   '-'                                          AS proceso,
                   0                                            AS num_ped,
                   0                                            AS serie,
                   0                                            AS nro,
                   0                                            AS num_det,
                   CAST(NULL AS VARCHAR2(100))                  AS nombre_cliente,
                   CAST(NULL AS VARCHAR2(25))                   AS cod_art,
                   CAST(NULL AS VARCHAR2(10))                   AS titulo,
                   ''                                           AS cod_paso_act,
                   ''                                           AS nombre_paso,
                   '#6c757d'                                    AS color_ui,
                   CAST(NULL AS DATE)                           AS fch_entrega_comp,
                   0                                            AS dias_retraso,
                   'N'                                          AS ind_retraso,
                   'N'                                          AS ind_urgente,
                   0                                            AS kg,
                   maq.descripcion                              AS descripcion
            FROM   {S}TT_MAQUINA maq
            WHERE  maq.TIPO_MAQ = 'S'
              AND  maq.ESTADO   = '0'
              AND  NOT EXISTS (SELECT 1 FROM {S}TT_RSECADO t
                               WHERE  t.cod_maq = maq.cod_maq
                                 AND  t.estado IN ('1','2'))
            ORDER BY estado_maq ASC, cod_maq ASC";

        var list = new List<PlnEstadoMaquinaTT>();
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        await using var r = (OracleDataReader)await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new PlnEstadoMaquinaTT
            {
                EstadoMaq      = SafeStr(r["estado_maq"]),
                CodMaq         = SafeStr(r["cod_maq"]),
                TipDoc         = SafeStr(r["tipodoc"]),
                Proceso        = SafeStr(r["proceso"]),
                NumPed         = r.IsDBNull(r.GetOrdinal("num_ped"))         ? 0  : Convert.ToInt64(r["num_ped"]),
                Nro            = r.IsDBNull(r.GetOrdinal("nro"))             ? 0  : Convert.ToInt32(r["nro"]),
                NumDet         = r.IsDBNull(r.GetOrdinal("num_det"))         ? 0  : Convert.ToInt32(r["num_det"]),
                Serie          = r.IsDBNull(r.GetOrdinal("serie"))           ? 0  : Convert.ToInt32(r["serie"]),
                NombreCliente  = r.IsDBNull(r.GetOrdinal("nombre_cliente"))  ? null : SafeStr(r["nombre_cliente"]),
                CodArt         = r.IsDBNull(r.GetOrdinal("cod_art"))         ? null : SafeStr(r["cod_art"]),
                Titulo         = r.IsDBNull(r.GetOrdinal("titulo"))          ? null : SafeStr(r["titulo"]),
                CodPasoAct     = SafeStr(r["cod_paso_act"]),
                NombrePaso     = SafeStr(r["nombre_paso"]),
                ColorUi        = SafeStr(r["color_ui"]),
                FchEntregaComp = r.IsDBNull(r.GetOrdinal("fch_entrega_comp")) ? null : (DateTime?)Convert.ToDateTime(r["fch_entrega_comp"]),
                DiasRetraso    = r.IsDBNull(r.GetOrdinal("dias_retraso"))     ? 0   : Convert.ToInt32(r["dias_retraso"]),
                IndRetraso     = SafeStr(r["ind_retraso"]),
                IndUrgente     = SafeStr(r["ind_urgente"]),
                Kg             = r.IsDBNull(r.GetOrdinal("kg"))               ? 0m  : Convert.ToDecimal(r["kg"]),
                Descripcion    = r.IsDBNull(r.GetOrdinal("descripcion")) ? null : SafeStr(r["descripcion"]),
            });
        }
        return list;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  OTRAS MÁQUINAS TT-SOPORTE (Centrífugas, Mercerizadora, Prensadora, Calderos)
    // ──────────────────────────────────────────────────────────────────────────
    public async Task<IEnumerable<PlnEstadoMaquinaTT>> GetEstadoMaquinasOtrasAsync()
    {
        var sql = $@"
            SELECT 'LIBRE'                               AS estado_maq,
                   maq.cod_maq,
                   maq.descripcion,
                   maq.tipo_maq                          AS proceso,
                   '-'                                   AS tipodoc,
                   0                                     AS num_ped,
                   0                                     AS serie,
                   0                                     AS nro,
                   0                                     AS num_det,
                   CAST(NULL AS VARCHAR2(100))            AS nombre_cliente,
                   CAST(NULL AS VARCHAR2(25))             AS cod_art,
                   CAST(NULL AS VARCHAR2(10))             AS titulo,
                   ''                                    AS cod_paso_act,
                   ''                                    AS nombre_paso,
                   '#6c757d'                             AS color_ui,
                   CAST(NULL AS DATE)                    AS fch_entrega_comp,
                   0                                     AS dias_retraso,
                   'N'                                   AS ind_retraso,
                   'N'                                   AS ind_urgente,
                   0                                     AS kg
            FROM   {S}TT_MAQUINA maq
            WHERE  maq.TIPO_MAQ IN ('C','M','P','Q')
              AND  maq.ESTADO   = '0'
            ORDER  BY maq.tipo_maq, maq.cod_maq";

        var list = new List<PlnEstadoMaquinaTT>();
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd  = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        await using var r    = (OracleDataReader)await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new PlnEstadoMaquinaTT
            {
                EstadoMaq     = "LIBRE",
                CodMaq        = SafeStr(r["cod_maq"]),
                Descripcion   = r.IsDBNull(r.GetOrdinal("descripcion")) ? null : SafeStr(r["descripcion"]),
                TipDoc        = "-",
                Proceso       = SafeStr(r["proceso"]),
                NumPed        = 0,
                Serie         = 0,
                Nro           = 0,
                NumDet        = 0,
                CodPasoAct    = "",
                NombrePaso    = "",
                ColorUi       = "#6c757d",
                DiasRetraso   = 0,
                IndRetraso    = "N",
                IndUrgente    = "N",
                Kg            = 0m,
            });
        }
        return list;
    }
}
