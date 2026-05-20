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
                    "03"             => "Laboratorio",       // v2.1: L_VALIDA_RECETA
                    "04" or "05"     => "Hilandería",         // v2.1: PARTIDA / H_RPRODUC
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
        // V_PLN_PENDIENTES_DESP (§8.6 PKG_PLN): ítems en paso '12'/'13' con kg_pendientes > 0.
        // Ordenados: urgentes primero, luego prioridad, luego fch_entrega_comp.
        var sql = $@"
            SELECT num_ped, nro, cod_cliente, nom_cliente, cod_art, desc_art,
                   color, titulo, kg_pendientes, stock_disponible, kg_a_despachar,
                   fch_entrega_comp, dias_vencido, dias_retraso, ind_urgente,
                   cod_paso_act, nombre_paso, prioridad_pedido
            FROM   {S}V_PLN_PENDIENTES_DESP";

        var list = new List<PlnPendienteDespacho>();
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd = new OracleCommand(sql, conn);
        await using var r   = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new PlnPendienteDespacho
            {
                NumPed          = SafeVal<long>(r["num_ped"]),
                Nro             = SafeVal<int>(r["nro"]),
                CodCliente      = SafeStr(r["cod_cliente"]),
                NomCliente      = SafeStr(r["nom_cliente"]),
                CodArt          = SafeStr(r["cod_art"]),
                DescArt         = SafeStr(r["desc_art"]),
                Color           = SafeStr(r["color"]),
                Titulo          = SafeStr(r["titulo"]),
                KgPendientes    = SafeVal<decimal>(r["kg_pendientes"]),
                StockDisponible = SafeVal<decimal>(r["stock_disponible"]),
                KgADespachar    = SafeVal<decimal>(r["kg_a_despachar"]),
                FchEntregaComp  = r["fch_entrega_comp"] == DBNull.Value ? null : Convert.ToDateTime(r["fch_entrega_comp"]),
                DiasVencido     = SafeVal<int>(r["dias_vencido"]),
                DiasRetraso     = SafeVal<int>(r["dias_retraso"]),
                IndUrgente      = SafeStr(r["ind_urgente"]),
                CodPasoAct      = SafeStr(r["cod_paso_act"]),
                NombrePaso      = SafeStr(r["nombre_paso"]),
                PrioridadPedido = SafeStr(r["prioridad_pedido"]),
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
}
