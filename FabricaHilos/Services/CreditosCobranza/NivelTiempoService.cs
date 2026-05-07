using System.Data;
using Oracle.ManagedDataAccess.Client;
using FabricaHilos.Models.CreditosCobranza;

namespace FabricaHilos.Services.CreditosCobranza;

public class NivelTiempoService : OracleServiceBase, INivelTiempoService
{
    private readonly ILogger<NivelTiempoService> _logger;

    public NivelTiempoService(
        IConfiguration configuration,
        ILogger<NivelTiempoService> logger,
        IHttpContextAccessor httpContextAccessor)
        : base(configuration, httpContextAccessor)
    {
        _logger = logger;
    }

    public async Task<List<NivelTiempoDto>> ObtenerNivelTiempoAsync(DateTime fechaInicio, DateTime fechaFin)
    {
        var result  = new List<NivelTiempoDto>();
        var connStr = GetOracleConnectionString();
        if (string.IsNullOrEmpty(connStr)) return result;

        try
        {
            // Iterar mes a mes: el paquete filtra ventas BETWEEN p_fechai y p_fechaf
            // y saldos acumulados hasta p_fechaf
            var fechas = new List<DateTime>();
            var cur = new DateTime(fechaInicio.Year, fechaInicio.Month, 1);
            var end = new DateTime(fechaFin.Year, fechaFin.Month, 1);
            while (cur <= end)
            {
                fechas.Add(new DateTime(cur.Year, cur.Month, DateTime.DaysInMonth(cur.Year, cur.Month)));
                cur = cur.AddMonths(1);
            }

            var seen = new HashSet<string>();
            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();

            foreach (var fecha in fechas)
            {
                var mesInicio = new DateTime(fecha.Year, fecha.Month, 1);

                await using var cmd = new OracleCommand($"{S}PKG_COBRANZA.ObtenerReporte", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.BindByName  = true;
                cmd.Parameters.Add(new OracleParameter("p_tipo",   OracleDbType.Char)     { Value = "T" });
                cmd.Parameters.Add(new OracleParameter("p_fecha",  OracleDbType.Date)     { Value = fecha });
                cmd.Parameters.Add(new OracleParameter("p_fechai", OracleDbType.Date)     { Value = mesInicio });
                cmd.Parameters.Add(new OracleParameter("p_fechaf", OracleDbType.Date)     { Value = fecha });
                cmd.Parameters.Add(new OracleParameter("p_cursor", OracleDbType.RefCursor, ParameterDirection.Output));

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var ano = Convert.ToInt32(reader["ANO"]);
                    var mes = Convert.ToInt32(reader["MES"]);
                    var key = $"{ano}-{mes}";
                    if (!seen.Contains(key))
                    {
                        seen.Add(key);
                        result.Add(new NivelTiempoDto
                        {
                            Ano        = ano,
                            Mes        = mes,
                            SaldoSoles = reader["SALDO_SOLES"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["SALDO_SOLES"]),
                            VtaSoles   = reader["VTA_SOLES"]   == DBNull.Value ? 0m : Convert.ToDecimal(reader["VTA_SOLES"]),
                            IndSoles   = reader["IND_SOLES"]   == DBNull.Value ? 0m : Convert.ToDecimal(reader["IND_SOLES"]),
                            SaldoDolar = reader["SALDO_DOLAR"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["SALDO_DOLAR"]),
                            VtaDolar   = reader["VTA_DOLAR"]   == DBNull.Value ? 0m : Convert.ToDecimal(reader["VTA_DOLAR"]),
                            IndDolar   = reader["IND_DOLAR"]   == DBNull.Value ? 0m : Convert.ToDecimal(reader["IND_DOLAR"]),
                        });
                    }
                }
            }

            result = result.OrderBy(x => x.Ano).ThenBy(x => x.Mes).ToList();

            // Rellenar meses sin datos del rango con valores en cero
            var cur2 = new DateTime(fechaInicio.Year, fechaInicio.Month, 1);
            var end2 = new DateTime(fechaFin.Year,    fechaFin.Month,    1);
            while (cur2 <= end2)
            {
                if (!result.Any(r => r.Ano == cur2.Year && r.Mes == cur2.Month))
                {
                    result.Add(new NivelTiempoDto { Ano = cur2.Year, Mes = cur2.Month });
                }
                cur2 = cur2.AddMonths(1);
            }

            result = result.OrderBy(x => x.Ano).ThenBy(x => x.Mes).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener nivel de tiempo promedio");
        }

        return result;
    }
}
