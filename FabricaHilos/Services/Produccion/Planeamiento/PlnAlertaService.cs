using System.Data;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using FabricaHilos.Models.Produccion.Planeamiento;

namespace FabricaHilos.Services.Produccion.Planeamiento;

public class PlnAlertaService : OracleServiceBase, IPlnAlertaService
{
    private readonly ILogger<PlnAlertaService> _logger;

    public PlnAlertaService(
        IConfiguration       configuration,
        IHttpContextAccessor httpContextAccessor,
        ILogger<PlnAlertaService> logger)
        : base(configuration, httpContextAccessor)
    {
        _logger = logger;
    }

    private static DateTime? SafeDate(object? v) =>
        v == null || v == DBNull.Value ? null : Convert.ToDateTime(v);

    private static string SafeStr(object? v) =>
        v == null || v == DBNull.Value ? "" : v.ToString()!;

    private static T SafeVal<T>(object? v, T def = default!)
    {
        if (v == null || v == DBNull.Value) return def;
        var t = typeof(T);
        var u = Nullable.GetUnderlyingType(t) ?? t;
        return (T)Convert.ChangeType(v, u);
    }

    public async Task<IEnumerable<PlnAlerta>> GetActivasAsync()
    {
        // V_PLN_ALERTAS_ACTIVAS (§8.4 v2.3): filtra ESTADO='A', ordena C→A→M→B.
        // Enriquecida con JOIN PLN_SEGUIMIENTO+PLN_ESTADO_CODIGO para evitar subqueries en la app.
        // BUG FIX: leer horas_sin_resolver por ordinal con GetDouble() para evitar OverflowException
        // (ROUND((SYSDATE-DATE)*24, 2) devuelve NUMBER de alta precisión que C# decimal no tolera).
        var sql = $@"
            SELECT a.id_alerta, a.serie, a.tip_alerta, a.nivel, a.titulo, a.detalle,
                   a.fch_alerta, a.fch_limite, a.dias_retraso, a.num_ped, a.nro, a.num_det,
                   a.cod_cliente, a.nom_cliente, a.cod_maq, a.estado, a.horas_sin_resolver,
                   a.cod_art, ar.descripcion AS desc_art, a.titulo_art, a.proceso,
                   a.cod_paso_act, a.nombre_paso, a.color_ui,
                   a.fch_entrega_comp, a.dias_retraso_ent, a.cantidad_orig, a.kg_pendientes,
                   a.nro_ciclo, a.ind_urgente
            FROM   {S}V_PLN_ALERTAS_ACTIVAS a
            LEFT JOIN {S}ARTICUL ar ON ar.cod_art = a.cod_art";

        var list = new List<PlnAlerta>();
        try
        {
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd = new OracleCommand(sql, conn);
        await using var r   = await cmd.ExecuteReaderAsync();
        // Usar ordinal + GetDouble para horas_sin_resolver: evita DecimalConv.GetDecimal overflow
        // (SYSDATE-DATE devuelve NUMBER de alta precisión que desborda decimal de C#)
        int oHoras = r.GetOrdinal("horas_sin_resolver");
        while (await r.ReadAsync())
        {
            double? horasSinResolver = r.IsDBNull(oHoras) ? null : (double?)r.GetDouble(oHoras);
            list.Add(new PlnAlerta
            {
                IdAlerta         = SafeVal<long>(r["id_alerta"]),
                Serie            = r["serie"]          == DBNull.Value ? null : SafeVal<int?>(r["serie"]),
                TipAlerta        = SafeStr(r["tip_alerta"]),
                Nivel            = SafeStr(r["nivel"]),
                Titulo           = SafeStr(r["titulo"]),
                Detalle          = SafeStr(r["detalle"]),
                FchAlerta        = SafeVal<DateTime>(r["fch_alerta"]),
                FchLimite        = SafeDate(r["fch_limite"]),
                DiasRetraso      = r["dias_retraso"]   == DBNull.Value ? null : SafeVal<int?>(r["dias_retraso"]),
                CodMaq           = SafeStr(r["cod_maq"]),
                Estado           = SafeStr(r["estado"]),
                NumPed           = r["num_ped"]        == DBNull.Value ? null : SafeVal<long?>(r["num_ped"]),
                Nro              = r["nro"]            == DBNull.Value ? null : SafeVal<int?>(r["nro"]),
                NumDet           = r["num_det"]        == DBNull.Value ? null : SafeVal<int?>(r["num_det"]),
                CodCliente       = SafeStr(r["cod_cliente"]),
                NombreCliente    = SafeStr(r["nom_cliente"]),
                CodArt           = r["cod_art"]        == DBNull.Value ? null : SafeStr(r["cod_art"]),
                DescArt          = r["desc_art"]       == DBNull.Value ? null : SafeStr(r["desc_art"]),
                TituloArt        = r["titulo_art"]     == DBNull.Value ? null : SafeStr(r["titulo_art"]),
                Proceso          = SafeStr(r["proceso"]),
                CodPasoAct       = SafeStr(r["cod_paso_act"]),
                NombrePaso       = SafeStr(r["nombre_paso"]),
                ColorUiPaso      = SafeStr(r["color_ui"]),
                FchEntregaComp   = SafeDate(r["fch_entrega_comp"]),
                DiasRetrasoEnt   = r["dias_retraso_ent"] == DBNull.Value ? null : SafeVal<int?>(r["dias_retraso_ent"]),
                CantidadOrig     = r["cantidad_orig"]  == DBNull.Value ? null : SafeVal<decimal?>(r["cantidad_orig"]),
                KgPendientes     = r["kg_pendientes"]  == DBNull.Value ? null : SafeVal<decimal?>(r["kg_pendientes"]),
                NroCiclo         = r["nro_ciclo"]      == DBNull.Value ? null : SafeVal<int?>(r["nro_ciclo"]),
                IndUrgente       = SafeStr(r["ind_urgente"]),
                HorasSinResolver = horasSinResolver,
            });
        }
        }
        catch (OracleException ex) when (ex.Number == 942)
        {
            // ORA-00942: tabla o vista no existe — el script PKG_PLN.sql aún no se ejecutó
            // en esta instancia de BD. Se devuelve lista vacía para no bloquear el Dashboard.
            _logger.LogWarning(ex,
                "[PlnAlertaService] V_PLN_ALERTAS_ACTIVAS no existe en el esquema actual ({Schema}). " +
                "Ejecute PKG_PLN.sql contra la BD para activar el módulo de alertas.",
                S?.TrimEnd('.'));
        }
        catch (IndexOutOfRangeException ex)
        {
            // La vista existe pero no tiene la columna esperada (versión desactualizada del script).
            _logger.LogWarning(ex,
                "[PlnAlertaService] V_PLN_ALERTAS_ACTIVAS no contiene todas las columnas esperadas. " +
                "Verifique que el script PKG_PLN.sql esté actualizado.");
        }
        return list;
    }

    public async Task<IEnumerable<PlnAlerta>> GetHistorialAsync(int ultDias = 30)
    {
        // Lee PLN_ALERTA directamente (ESTADO IN ('R','I')) para historial de alertas
        // resueltas o ignoradas. V_PLN_ALERTAS_ACTIVAS filtra solo ESTADO='A'.
        var sql = $@"
            SELECT a.id_alerta, a.tip_alerta, a.nivel, a.titulo, a.detalle,
                   a.fch_alerta, a.fch_limite, a.dias_retraso, a.num_ped, a.nro,
                   a.cod_cliente, cl.nombre AS nom_cliente, a.cod_maq, a.estado,
                   a.fch_resolucion, a.usuario_resuelve,
                   NULL AS horas_sin_resolver
            FROM   {S}PLN_ALERTA a
            LEFT   JOIN {S}CLIENTES cl ON cl.cod_cliente = a.cod_cliente
            WHERE  a.estado IN ('R','I')
              AND  a.fch_alerta >= SYSDATE - :dias
            ORDER  BY a.fch_alerta DESC";

        var list = new List<PlnAlerta>();
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        cmd.Parameters.Add("dias", ultDias);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new PlnAlerta
            {
                IdAlerta         = SafeVal<long>(r["id_alerta"]),
                TipAlerta        = SafeStr(r["tip_alerta"]),
                Nivel            = SafeStr(r["nivel"]),
                Titulo           = SafeStr(r["titulo"]),
                Detalle          = SafeStr(r["detalle"]),
                FchAlerta        = SafeVal<DateTime>(r["fch_alerta"]),
                FchLimite        = SafeDate(r["fch_limite"]),
                DiasRetraso      = r["dias_retraso"] == DBNull.Value ? null : SafeVal<int?>(r["dias_retraso"]),
                CodMaq           = SafeStr(r["cod_maq"]),
                Estado           = SafeStr(r["estado"]),
                NumPed           = r["num_ped"] == DBNull.Value ? null : SafeVal<long?>(r["num_ped"]),
                Nro              = r["nro"]     == DBNull.Value ? null : SafeVal<int?>(r["nro"]),
                CodCliente       = SafeStr(r["cod_cliente"]),
                NombreCliente    = SafeStr(r["nom_cliente"]),
                FchResolucion    = SafeDate(r["fch_resolucion"]),
                UsuarioResuelve  = SafeStr(r["usuario_resuelve"]),
            });
        }
        return list;
    }

    public async Task ResolverAsync(long idAlerta, string usuario)
    {
        var sql = $@"
            UPDATE {S}PLN_ALERTA
            SET    ESTADO = 'R', FCH_RESOLUCION = SYSDATE, USUARIO_RESUELVE = :usuario
            WHERE  ID_ALERTA = :idAlerta";

        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        cmd.Parameters.Add("usuario",  usuario);
        cmd.Parameters.Add("idAlerta", idAlerta);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task IgnorarAsync(long idAlerta, string usuario)
    {
        var sql = $@"
            UPDATE {S}PLN_ALERTA
            SET    ESTADO = 'I', FCH_RESOLUCION = SYSDATE, USUARIO_RESUELVE = :usuario
            WHERE  ID_ALERTA = :idAlerta";

        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        cmd.Parameters.Add("usuario",  usuario);
        cmd.Parameters.Add("idAlerta", idAlerta);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task GenerarAlertasAsync()
    {
        // PKG_PLN.SP_PLN_GENERA_ALERTAS (§6 PKG_PLN): genera/actualiza alertas activas.
        // Normalmente ejecutado cada hora por JOB_PLN_ALERTAS.
        // Disponible aquí para forzar regeneración manual desde el panel de alertas.
        const string sql = "BEGIN PKG_PLN.SP_PLN_GENERA_ALERTAS; END;";
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd = new OracleCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IEnumerable<PlnProximoVencer>> GetProximosVencerAsync(
        DateTime fchIni, DateTime fchFin, int diasAtras = 0)
    {
        // Ítems activos cuya FCH_ENTREGA_COMP cae en [fchIni - diasAtras, fchFin].
        // diasAtras permite incluir ítems ya vencidos pero aún sin cerrar.
        // Una fila por ítem de pedido (NUM_PED/NRO), agrupando todos sus sub-lotes (NUM_DET).
        // Muestra la etapa más rezagada (mínimo orden_paso) y suma los KGs.
        var sql = $@"
            WITH paso_min AS (
                SELECT s2.serie, s2.num_ped, s2.nro,
                       MIN(ec2.orden_paso) AS min_orden
                FROM   {S}PLN_SEGUIMIENTO s2
                JOIN   {S}PLN_ESTADO_CODIGO ec2 ON ec2.cod_paso = s2.cod_paso_act
                WHERE  s2.estado = 'A'
                GROUP BY s2.serie, s2.num_ped, s2.nro
            ),
            base AS (
                SELECT s.serie, s.num_ped, s.nro,
                       s.cod_cliente,
                       MAX(NVL(cl.nombre, s.cod_cliente)) AS nombre_cliente,
                       s.cod_art, MAX(ar.descripcion) AS desc_art, s.color, s.titulo, s.proceso,
                       MAX(CASE WHEN ec.orden_paso = pm.min_orden THEN s.cod_paso_act END) AS cod_paso_act,
                       MAX(CASE WHEN ec.orden_paso = pm.min_orden THEN ec.nombre_paso END) AS nombre_paso,
                       MAX(CASE WHEN ec.orden_paso = pm.min_orden THEN ec.color_ui    END) AS color_ui,
                       MIN(s.fch_pedido)           AS fch_pedido,
                       -- F.COMPROMISO del sublote más retrasado (mismo que etapa mostrada)
                       MAX(CASE WHEN ec.orden_paso = pm.min_orden THEN s.fch_entrega_comp END) AS fch_entrega_comp,
                       TRUNC(MAX(CASE WHEN ec.orden_paso = pm.min_orden THEN s.fch_entrega_comp END)) - TRUNC(SYSDATE) AS dias_hasta_vencer,
                       MAX(s.dias_retraso)         AS dias_retraso,
                       MAX(s.ind_retraso)          AS ind_retraso,
                       MAX(s.ind_urgente)          AS ind_urgente,
                       MAX(s.ind_reproceso)        AS ind_reproceso,
                       SUM(s.cantidad_orig)        AS cantidad_orig,
                       SUM(s.kg_pendientes)        AS kg_pendientes,
                       MAX(s.nro_ciclo)            AS nro_ciclo,
                       COUNT(DISTINCT s.num_det)   AS num_sublotes_activos,
                       LISTAGG(
                           'SL' || s.num_det || ': ' || ec.nombre_paso ||
                           ' · ' || ROUND(s.kg_pendientes) || ' kg pend.' ||
                           ' · Ent.' || NVL(TO_CHAR(s.fch_entrega_comp,'DD/MM/YY'),'?'),
                           '§')
                         WITHIN GROUP (ORDER BY ec.orden_paso, s.num_det) AS sublotes_detalle,
                       MAX(CASE WHEN ec.orden_paso = pm.min_orden THEN
                         CASE s.cod_paso_act
                           WHEN '01'  THEN s.fch_pedido
                           WHEN '02'  THEN s.fch_real_programado
                           WHEN '03'  THEN s.fch_real_produccion
                           WHEN '04'  THEN s.fch_real_partida
                           WHEN '05'  THEN s.fch_real_partida
                           WHEN '06'  THEN s.fch_real_tin_ini
                           WHEN '07'  THEN s.fch_real_tin_fin
                           WHEN '08'  THEN s.fch_real_secado
                           WHEN '09'  THEN s.fch_real_cc_tinto
                           WHEN '09B' THEN s.fch_real_gaseado
                           WHEN '9R'  THEN s.fch_real_cc_rechazo
                           WHEN '10'  THEN s.fch_real_devanado
                           WHEN '11'  THEN s.fch_real_calidad
                           WHEN '12'  THEN s.fch_real_alm_pt
                           WHEN '13'  THEN s.fch_real_alm_pt
                           WHEN '14'  THEN s.fch_real_despacho
                           ELSE NULL
                         END
                       END)                        AS fch_ini_paso
                FROM   {S}PLN_SEGUIMIENTO s
                JOIN   {S}PLN_ESTADO_CODIGO ec ON ec.cod_paso = s.cod_paso_act
                JOIN   paso_min pm ON pm.serie = s.serie AND pm.num_ped = s.num_ped AND pm.nro = s.nro
                LEFT JOIN {S}CLIENTES cl ON cl.cod_cliente = s.cod_cliente
                LEFT JOIN {S}ARTICUL  ar ON ar.cod_art     = s.cod_art
                WHERE  s.estado = 'A'
                  AND  s.fch_entrega_comp BETWEEN TRUNC(:fchIni) - :diasAtras AND TRUNC(:fchFin)
                GROUP BY s.serie, s.num_ped, s.nro,
                         s.cod_cliente, s.cod_art, s.color, s.titulo, s.proceso
            )
            SELECT b.*,
                   NVL(b.fch_ini_paso,
                       (SELECT MIN(ev.fch_evento)
                        FROM   {S}PLN_LOG_EVENTOS ev
                        WHERE  ev.num_ped  = b.num_ped
                          AND  ev.serie    = b.serie
                          AND  ev.nro      = b.nro
                          AND  ev.cod_paso = b.cod_paso_act)) AS fch_ini_paso
            FROM   base b
            WHERE  b.cod_paso_act NOT IN ('12','13','14')
            ORDER BY b.fch_entrega_comp, b.ind_urgente DESC, b.num_ped";

        var list = new List<PlnProximoVencer>();
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        cmd.Parameters.Add("fchIni",    fchIni);
        cmd.Parameters.Add("diasAtras", diasAtras);
        cmd.Parameters.Add("fchFin",    fchFin);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new PlnProximoVencer
            {
                Serie            = SafeVal<int>(r["serie"]),
                NumPed           = SafeVal<long>(r["num_ped"]),
                Nro              = SafeVal<int>(r["nro"]),
                CodCliente       = SafeStr(r["cod_cliente"]),
                NombreCliente    = SafeStr(r["nombre_cliente"]),
                CodArt           = SafeStr(r["cod_art"]),
                DescArt          = SafeStr(r["desc_art"]),
                Color            = SafeStr(r["color"]),
                Titulo           = SafeStr(r["titulo"]),
                Proceso          = SafeStr(r["proceso"]),
                CodPasoAct       = SafeStr(r["cod_paso_act"]),
                NombrePaso       = SafeStr(r["nombre_paso"]),
                ColorUiPaso      = SafeStr(r["color_ui"]),
                FchPedido        = SafeVal<DateTime>(r["fch_pedido"]),
                FchEntregaComp   = SafeDate(r["fch_entrega_comp"]),
                DiasHastaVencer  = SafeVal<int>(r["dias_hasta_vencer"]),
                DiasRetraso      = SafeVal<int>(r["dias_retraso"]),
                IndRetraso       = SafeStr(r["ind_retraso"]),
                IndUrgente       = SafeStr(r["ind_urgente"]),
                IndReproceso     = SafeStr(r["ind_reproceso"]),
                CantidadOrig     = SafeVal<decimal>(r["cantidad_orig"]),
                KgPendientes     = SafeVal<decimal>(r["kg_pendientes"]),
                NroCiclo         = SafeVal<int>(r["nro_ciclo"]),
                NumSubLotesActivos = SafeVal<int>(r["num_sublotes_activos"]),
                SubLotesDetalle  = SafeStr(r["sublotes_detalle"]),
                FchIniPaso       = SafeDate(r["fch_ini_paso"]),
            });
        }
        return list;
    }
}
