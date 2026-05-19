using System.Data;
using Oracle.ManagedDataAccess.Client;
using FabricaHilos.Models.Produccion.Planeamiento;

namespace FabricaHilos.Services.Produccion.Planeamiento;

public class PlnAlertaService : OracleServiceBase, IPlnAlertaService
{
    public PlnAlertaService(
        IConfiguration       configuration,
        IHttpContextAccessor httpContextAccessor)
        : base(configuration, httpContextAccessor) { }

    private static DateTime? SafeDate(object? v) =>
        v == null || v == DBNull.Value ? null : Convert.ToDateTime(v);

    private static string SafeStr(object? v) =>
        v == null || v == DBNull.Value ? "" : v.ToString()!;

    private static T SafeVal<T>(object? v, T def = default!) =>
        v == null || v == DBNull.Value ? def : (T)Convert.ChangeType(v, typeof(T));

    public async Task<IEnumerable<PlnAlerta>> GetActivasAsync()
    {
        // V_PLN_ALERTAS_ACTIVAS (§8.4): filtra ESTADO='A', ordena C→A→M→B, incluye nom_cliente
        // BUG FIX: horas_sin_resolver (SYSDATE-fch_alerta) devuelve días decimales; se convierte a horas
        var sql = $@"
            SELECT a.id_alerta, a.tip_alerta, a.nivel, a.titulo, a.detalle,
                   a.fch_alerta, a.fch_limite, a.dias_retraso, a.num_ped, a.nro,
                   a.cod_cliente, a.nom_cliente, a.cod_maq, a.estado,
                   a.horas_sin_resolver
            FROM   {S}V_PLN_ALERTAS_ACTIVAS a";

        var list = new List<PlnAlerta>();
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd = new OracleCommand(sql, conn);
        await using var r   = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var diasSinResolver = r["horas_sin_resolver"] == DBNull.Value ? null : SafeVal<double?>(r["horas_sin_resolver"]);
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
                Nro              = r["nro"] == DBNull.Value ? null : SafeVal<int?>(r["nro"]),
                CodCliente       = SafeStr(r["cod_cliente"]),
                NombreCliente    = SafeStr(r["nom_cliente"]),
                HorasSinResolver = diasSinResolver.HasValue ? diasSinResolver.Value * 24 : null,
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
}
