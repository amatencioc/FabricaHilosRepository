using FabricaHilos.Models.Produccion.Planeamiento;
using Oracle.ManagedDataAccess.Client;

namespace FabricaHilos.Services.Produccion.Planeamiento;

public class PlnRegistroService : OracleServiceBase, IPlnRegistroService
{
    public PlnRegistroService(
        IConfiguration       configuration,
        IHttpContextAccessor httpContextAccessor)
        : base(configuration, httpContextAccessor) { }

    // ── helpers ──────────────────────────────────────────────────────────────
    private static DateTime? SafeDate(object? val) =>
        val == null || val == DBNull.Value ? null : Convert.ToDateTime(val);

    private static string SafeStr(object? val) =>
        val == null || val == DBNull.Value ? "" : val.ToString()!.Trim();

    private static decimal SafeDec(object? val) =>
        val == null || val == DBNull.Value ? 0m :
        decimal.TryParse(val.ToString(), out var d) ? d : 0m;

    // ── query principal ───────────────────────────────────────────────────────

    private const string SqlRegistro = @"
SELECT
    p.serie,
    p.num_ped,
    p.fecha          AS fch_pedido,
    p.f_aprobacion   AS fch_aprobacion,
    p.estado         AS estado_ped,
    p.cod_cliente,
    p.cod_vende,
    NVL(ta.abreviada, p.cod_vende) AS nombre_vende,
    NVL(p.giro,'')   AS giro,
    COALESCE(cl.nombre, p.nombre, p.cod_cliente) AS nombre_cliente,
    i.nro,
    i.cod_art,
    COALESCE(a.descripcion, i.cod_art) AS desc_art,
    NVL(i.titulo,'')       AS titulo,
    NVL(i.tipo_fibra,'')   AS tipo_fibra,
    NVL(i.tfibra,'')       AS tfibra,
    NVL(vf.descripcion,'') AS desc_tfibra,
    NVL(i.proceso,'')      AS proceso,
    NVL(hp.descripcion,'') AS nombre_proceso_db,
    NVL(i.cod_serv,'')     AS cod_serv,
    i.cantidad,
    i.precio,
    NVL(i.color,'')        AS color,
    NVL(i.color_det,'')    AS color_det,
    NVL(i.intensidad,'')   AS intensidad,
    NVL(ht.abreviado,'')   AS intensidad_abrev,
    NVL(i.presentacion,'') AS presentacion,
    i.estado               AS estado_item,
    i.f_maxped,
    NVL(i.solo_despacho,'N') AS solo_despacho,
    NVL(SUBSTR(i.detalle,1,200),'') AS detalle,
    NVL(a.cod_fam,'')      AS cod_fam,
    NVL(a.cod_lin,'')      AS cod_lin,
    NVL(fl_f.descripcion,'') AS desc_familia,
    NVL(fl_l.descripcion,'') AS desc_linea,
    NVL(pln.paso_actual,'')       AS paso_actual,
    NVL(pln.paso_color,'#6c757d') AS paso_color
FROM   {S}pedido p
JOIN   {S}itemped i
       ON  i.serie   = p.serie
       AND i.num_ped = p.num_ped
LEFT   JOIN {S}clientes cl
       ON  cl.cod_cliente = p.cod_cliente
LEFT   JOIN {S}articul a
       ON  a.cod_art = i.cod_art
LEFT   JOIN {S}tfamlin fl_f
       ON  fl_f.tp_art  = 'I'
       AND fl_f.cod_fam = LPAD(TRIM(a.cod_fam), 2, '0')
       AND fl_f.cod_lin = '....'
LEFT   JOIN {S}tfamlin fl_l
       ON  fl_l.tp_art  = 'I'
       AND fl_l.cod_fam = LPAD(TRIM(a.cod_fam), 2, '0')
       AND fl_l.cod_lin = a.cod_lin
LEFT   JOIN {S}v_tfibra vf
       ON  vf.fibra = i.tfibra
LEFT   JOIN {S}tablas_auxiliares ta
       ON  ta.tipo   = 29
       AND ta.codigo = p.cod_vende
LEFT   JOIN {S}h_tprod ht
       ON  ht.tabla  = '03'
       AND ht.codigo = i.intensidad
LEFT   JOIN {S}h_procesos hp
       ON  hp.proceso = i.proceso
LEFT   JOIN (
           SELECT ps.serie, ps.num_ped, ps.nro,
                  MIN(ec.nombre_paso) KEEP (DENSE_RANK FIRST ORDER BY ec.orden_paso) AS paso_actual,
                  MIN(ec.color_ui)    KEEP (DENSE_RANK FIRST ORDER BY ec.orden_paso) AS paso_color
           FROM   {S}pln_seguimiento ps
           JOIN   {S}pln_estado_codigo ec ON ec.cod_paso = ps.cod_paso_act
           WHERE  ps.estado = 'A'
           GROUP  BY ps.serie, ps.num_ped, ps.nro
       ) pln ON pln.serie = i.serie AND pln.num_ped = i.num_ped AND pln.nro = i.nro
WHERE  p.fecha >= TRUNC(:fchDesde)
  AND  p.fecha <  TRUNC(:fchHasta) + 1
  AND  {condServ}
  AND  {condCliente}
  AND  {condProceso}
  AND  {condEstado}
  AND  {condTfibra}
  AND  {condPaso}
ORDER  BY p.fecha DESC, p.num_ped, i.nro";

    public async Task<IReadOnlyList<RegistroPedidoItem>> GetRegistroDiarioAsync(
        DateTime fchDesde,
        DateTime fchHasta,
        string   filtroServ         = "",
        string   filtroCliente      = "",
        string   filtroProceso      = "",
        string   filtroEstado       = "",
        string   filtroTfibra       = "",
        string   filtroPasoActual   = "")
    {
        var sqlFinal = SqlRegistro
            .Replace("{S}", S)
            .Replace("{condServ}",     string.IsNullOrEmpty(filtroServ)       ? "1=1" : "i.cod_serv    = :cod_serv")
            .Replace("{condCliente}",  string.IsNullOrEmpty(filtroCliente)    ? "1=1" : "p.cod_cliente = :cod_cliente")
            .Replace("{condProceso}",  string.IsNullOrEmpty(filtroProceso)    ? "1=1" : "i.proceso     = :proceso")
            .Replace("{condEstado}",   string.IsNullOrEmpty(filtroEstado)     ? "1=1" : "p.estado      = :estado")
            .Replace("{condTfibra}",   string.IsNullOrEmpty(filtroTfibra)     ? "1=1" : "i.tfibra      = :tfibra")
            .Replace("{condPaso}",     string.IsNullOrEmpty(filtroPasoActual) ? "1=1" : "pln.paso_actual = :paso_actual");

        var result = new List<RegistroPedidoItem>(256);

        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sqlFinal;
        // Reducir round-trips a Oracle leyendo filas en bloques de 1 MB
        cmd.FetchSize = 1_048_576;

        cmd.Parameters.Add(":fchDesde", OracleDbType.Date).Value = fchDesde;
        cmd.Parameters.Add(":fchHasta", OracleDbType.Date).Value = fchHasta;

        // Solo agregar parámetros de filtro cuando realmente se usan en la condición SQL
        if (!string.IsNullOrEmpty(filtroServ))
            cmd.Parameters.Add(":cod_serv",    OracleDbType.Varchar2).Value = filtroServ;
        if (!string.IsNullOrEmpty(filtroCliente))
            cmd.Parameters.Add(":cod_cliente", OracleDbType.Varchar2).Value = filtroCliente;
        if (!string.IsNullOrEmpty(filtroProceso))
            cmd.Parameters.Add(":proceso",     OracleDbType.Varchar2).Value = filtroProceso;
        if (!string.IsNullOrEmpty(filtroEstado))
            cmd.Parameters.Add(":estado",      OracleDbType.Varchar2).Value = filtroEstado;
        if (!string.IsNullOrEmpty(filtroTfibra))
            cmd.Parameters.Add(":tfibra",      OracleDbType.Varchar2).Value = filtroTfibra;
        if (!string.IsNullOrEmpty(filtroPasoActual))
            cmd.Parameters.Add(":paso_actual", OracleDbType.Varchar2).Value = filtroPasoActual;

        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync();

        // Pre-calcular ordinals una sola vez para evitar O(n×cols) lookups por nombre
        var oSerie          = reader.GetOrdinal("serie");
        var oNumPed         = reader.GetOrdinal("num_ped");
        var oFchPedido      = reader.GetOrdinal("fch_pedido");
        var oFchAprobacion  = reader.GetOrdinal("fch_aprobacion");
        var oEstadoPed      = reader.GetOrdinal("estado_ped");
        var oCodCliente     = reader.GetOrdinal("cod_cliente");
        var oNombreCliente  = reader.GetOrdinal("nombre_cliente");
        var oCodVende       = reader.GetOrdinal("cod_vende");
        var oGiro           = reader.GetOrdinal("giro");
        var oNro            = reader.GetOrdinal("nro");
        var oCodArt         = reader.GetOrdinal("cod_art");
        var oDescArt        = reader.GetOrdinal("desc_art");
        var oTitulo         = reader.GetOrdinal("titulo");
        var oTipoFibra      = reader.GetOrdinal("tipo_fibra");
        var oProceso        = reader.GetOrdinal("proceso");
        var oCodServ        = reader.GetOrdinal("cod_serv");
        var oCantidad       = reader.GetOrdinal("cantidad");
        var oPrecio         = reader.GetOrdinal("precio");
        var oColor          = reader.GetOrdinal("color");
        var oColorDet       = reader.GetOrdinal("color_det");
        var oIntensidad     = reader.GetOrdinal("intensidad");
        var oPresentacion   = reader.GetOrdinal("presentacion");
        var oEstadoItem     = reader.GetOrdinal("estado_item");
        var oFMaxPed        = reader.GetOrdinal("f_maxped");
        var oSoloDespacho   = reader.GetOrdinal("solo_despacho");
        var oDetalle        = reader.GetOrdinal("detalle");
        var oCodFam         = reader.GetOrdinal("cod_fam");
        var oCodLin         = reader.GetOrdinal("cod_lin");
        var oDescFamilia    = reader.GetOrdinal("desc_familia");
        var oDescLinea      = reader.GetOrdinal("desc_linea");
        var oPasoActual     = reader.GetOrdinal("paso_actual");
        var oPasoColor      = reader.GetOrdinal("paso_color");
        var oTfibra         = reader.GetOrdinal("tfibra");
        var oDescTfibra     = reader.GetOrdinal("desc_tfibra");
        var oNombreVende    = reader.GetOrdinal("nombre_vende");
        var oIntensidadAbrev= reader.GetOrdinal("intensidad_abrev");
        var oNombreProcesoDb= reader.GetOrdinal("nombre_proceso_db");

        while (await reader.ReadAsync())
        {
            result.Add(new RegistroPedidoItem
            {
                Serie           = reader.GetInt32(oSerie),
                NumPed          = reader.GetInt64(oNumPed),
                FchPedido       = reader.GetDateTime(oFchPedido),
                FchAprobacion   = SafeDate(reader.GetValue(oFchAprobacion)),
                EstadoPed       = SafeStr(reader.GetValue(oEstadoPed)),
                CodCliente      = SafeStr(reader.GetValue(oCodCliente)),
                NombreCliente   = SafeStr(reader.GetValue(oNombreCliente)),
                CodVende        = SafeStr(reader.GetValue(oCodVende)),
                Giro            = SafeStr(reader.GetValue(oGiro)),
                Nro             = reader.GetInt32(oNro),
                CodArt          = SafeStr(reader.GetValue(oCodArt)),
                DescArt         = SafeStr(reader.GetValue(oDescArt)),
                Titulo          = SafeStr(reader.GetValue(oTitulo)),
                TipoFibra       = SafeStr(reader.GetValue(oTipoFibra)),
                Proceso         = SafeStr(reader.GetValue(oProceso)),
                CodServ         = SafeStr(reader.GetValue(oCodServ)),
                Cantidad        = SafeDec(reader.GetValue(oCantidad)),
                Precio          = SafeDec(reader.GetValue(oPrecio)),
                Color           = SafeStr(reader.GetValue(oColor)),
                ColorDet        = SafeStr(reader.GetValue(oColorDet)),
                Intensidad      = SafeStr(reader.GetValue(oIntensidad)),
                Presentacion    = SafeStr(reader.GetValue(oPresentacion)),
                EstadoItem      = SafeStr(reader.GetValue(oEstadoItem)),
                FMaxPed         = SafeDate(reader.GetValue(oFMaxPed)),
                SoloDespacho    = SafeStr(reader.GetValue(oSoloDespacho)),
                Detalle         = SafeStr(reader.GetValue(oDetalle)),
                CodFam          = SafeStr(reader.GetValue(oCodFam)),
                CodLin          = SafeStr(reader.GetValue(oCodLin)),
                DescFamilia     = SafeStr(reader.GetValue(oDescFamilia)),
                DescLinea       = SafeStr(reader.GetValue(oDescLinea)),
                PasoActual      = SafeStr(reader.GetValue(oPasoActual)),
                PasoActualColor = SafeStr(reader.GetValue(oPasoColor)),
                Tfibra          = SafeStr(reader.GetValue(oTfibra)),
                DescTfibra      = SafeStr(reader.GetValue(oDescTfibra)),
                NombreVende     = SafeStr(reader.GetValue(oNombreVende)),
                IntensidadAbrev = SafeStr(reader.GetValue(oIntensidadAbrev)),
                NombreProcesoDb = SafeStr(reader.GetValue(oNombreProcesoDb)),
            });
        }

        return result.AsReadOnly();
    }
}
