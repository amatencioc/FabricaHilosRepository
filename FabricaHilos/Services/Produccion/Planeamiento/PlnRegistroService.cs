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
    NVL(p.giro,'')   AS giro,
    COALESCE(cl.nombre, p.nombre, p.cod_cliente) AS nombre_cliente,
    i.nro,
    i.cod_art,
    COALESCE(a.descripcion, i.cod_art) AS desc_art,
    NVL(i.titulo,'')      AS titulo,
    NVL(i.tipo_fibra,'')  AS tipo_fibra,
    NVL(i.proceso,'')     AS proceso,
    NVL(i.cod_serv,'')    AS cod_serv,
    i.cantidad,
    i.precio,
    NVL(i.color,'')       AS color,
    NVL(i.color_det,'')   AS color_det,
    NVL(i.intensidad,'')  AS intensidad,
    NVL(i.presentacion,'') AS presentacion,
    i.estado             AS estado_item,
    i.f_maxped,
    NVL(i.solo_despacho,'N') AS solo_despacho,
    NVL(SUBSTR(i.detalle,1,200),'') AS detalle,
    NVL(a.cod_fam,'')     AS cod_fam,
    NVL(a.cod_lin,'')     AS cod_lin,
    NVL(fl_f.descripcion,'') AS desc_familia,
    NVL(fl_l.descripcion,'') AS desc_linea
FROM   {S}pedido p
JOIN   {S}itemped i
       ON  i.serie   = p.serie
       AND i.num_ped = p.num_ped
LEFT   JOIN {S}clientes cl
       ON  cl.cod_cliente = p.cod_cliente
LEFT   JOIN {S}articul a
       ON  a.cod_art = i.cod_art
LEFT   JOIN {S}i_tfamlin fl_f
       ON  fl_f.tp_art  = 'I'
       AND fl_f.cod_fam = a.cod_fam
       AND fl_f.cod_lin = '....'
LEFT   JOIN {S}i_tfamlin fl_l
       ON  fl_l.tp_art  = 'I'
       AND fl_l.cod_fam = a.cod_fam
       AND fl_l.cod_lin = a.cod_lin
WHERE  p.fecha >= TRUNC(:fchDesde)
  AND  p.fecha <  TRUNC(:fchHasta) + 1
  AND  {condServ}
  AND  {condCliente}
  AND  {condProceso}
  AND  {condEstado}
ORDER  BY p.fecha DESC, p.num_ped, i.nro";

    public async Task<IReadOnlyList<RegistroPedidoItem>> GetRegistroDiarioAsync(
        DateTime fchDesde,
        DateTime fchHasta,
        string   filtroServ     = "",
        string   filtroCliente  = "",
        string   filtroProceso  = "",
        string   filtroEstado   = "")
    {
        var sqlFinal = SqlRegistro
            .Replace("{S}", S)
            .Replace("{condServ}",     string.IsNullOrEmpty(filtroServ)    ? "1=1" : "i.cod_serv    = :cod_serv")
            .Replace("{condCliente}",  string.IsNullOrEmpty(filtroCliente) ? "1=1" : "p.cod_cliente = :cod_cliente")
            .Replace("{condProceso}",  string.IsNullOrEmpty(filtroProceso) ? "1=1" : "i.proceso     = :proceso")
            .Replace("{condEstado}",   string.IsNullOrEmpty(filtroEstado)  ? "1=1" : "p.estado      = :estado");

        var result = new List<RegistroPedidoItem>();

        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sqlFinal;

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

        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new RegistroPedidoItem
            {
                Serie          = reader.GetInt32(reader.GetOrdinal("serie")),
                NumPed         = reader.GetInt64(reader.GetOrdinal("num_ped")),
                FchPedido      = reader.GetDateTime(reader.GetOrdinal("fch_pedido")),
                FchAprobacion  = SafeDate(reader["fch_aprobacion"]),
                EstadoPed      = SafeStr(reader["estado_ped"]),
                CodCliente     = SafeStr(reader["cod_cliente"]),
                NombreCliente  = SafeStr(reader["nombre_cliente"]),
                CodVende       = SafeStr(reader["cod_vende"]),
                Giro           = SafeStr(reader["giro"]),
                Nro            = reader.GetInt32(reader.GetOrdinal("nro")),
                CodArt         = SafeStr(reader["cod_art"]),
                DescArt        = SafeStr(reader["desc_art"]),
                Titulo         = SafeStr(reader["titulo"]),
                TipoFibra      = SafeStr(reader["tipo_fibra"]),
                Proceso        = SafeStr(reader["proceso"]),
                CodServ        = SafeStr(reader["cod_serv"]),
                Cantidad       = SafeDec(reader["cantidad"]),
                Precio         = SafeDec(reader["precio"]),
                Color          = SafeStr(reader["color"]),
                ColorDet       = SafeStr(reader["color_det"]),
                Intensidad     = SafeStr(reader["intensidad"]),
                Presentacion   = SafeStr(reader["presentacion"]),
                EstadoItem     = SafeStr(reader["estado_item"]),
                FMaxPed        = SafeDate(reader["f_maxped"]),
                SoloDespacho   = SafeStr(reader["solo_despacho"]),
                Detalle        = SafeStr(reader["detalle"]),
                CodFam         = SafeStr(reader["cod_fam"]),
                CodLin         = SafeStr(reader["cod_lin"]),
                DescFamilia    = SafeStr(reader["desc_familia"]),
                DescLinea      = SafeStr(reader["desc_linea"]),
            });
        }

        return result.AsReadOnly();
    }
}
