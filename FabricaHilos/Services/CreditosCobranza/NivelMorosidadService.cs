using System.Data;
using Oracle.ManagedDataAccess.Client;
using FabricaHilos.Models.CreditosCobranza;

namespace FabricaHilos.Services.CreditosCobranza;

public class NivelMorosidadService : OracleServiceBase, INivelMorosidadService
{
    private readonly ILogger<NivelMorosidadService> _logger;

    public NivelMorosidadService(
        IConfiguration configuration,
        ILogger<NivelMorosidadService> logger,
        IHttpContextAccessor httpContextAccessor)
        : base(configuration, httpContextAccessor)
    {
        _logger = logger;
    }

    public async Task<List<NivelMorosidadDto>> ObtenerNivelMorosidadAsync(DateTime fechaInicio, DateTime fechaFin)
    {
        var result  = new List<NivelMorosidadDto>();
        var connStr = GetOracleConnectionString();
        if (string.IsNullOrEmpty(connStr)) return result;

        try
        {
            // El paquete devuelve filas ya agrupadas por ANO/MES con p_fechaf como corte.
            // Filtra S.ANO = año(p_fechaf) y S.MES <= mes(p_fechaf), por eso se llama
            // una vez por año presente en el rango para capturar todos los meses.
            var anios = Enumerable.Range(fechaInicio.Year, fechaFin.Year - fechaInicio.Year + 1);

            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();

            var seen = new HashSet<string>();

            foreach (var anio in anios)
            {
                // Corte: último día del mes final dentro de este año
                var corte = anio < fechaFin.Year
                    ? new DateTime(anio, 12, 31)
                    : new DateTime(fechaFin.Year, fechaFin.Month,
                                   DateTime.DaysInMonth(fechaFin.Year, fechaFin.Month));

                await using var cmd = new OracleCommand($"{S}PKG_COBRANZA.ObtenerReporte", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.BindByName  = true;
                cmd.Parameters.Add(new OracleParameter("p_tipo",   OracleDbType.Char)     { Value = "M" });
                cmd.Parameters.Add(new OracleParameter("p_fecha",  OracleDbType.Date)     { Value = DBNull.Value });
                cmd.Parameters.Add(new OracleParameter("p_fechai", OracleDbType.Date)     { Value = DBNull.Value });
                cmd.Parameters.Add(new OracleParameter("p_fechaf", OracleDbType.Date)     { Value = corte });
                cmd.Parameters.Add(new OracleParameter("p_cursor", OracleDbType.RefCursor, ParameterDirection.Output));

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var ano = Convert.ToInt32(reader["ANO"]);
                    var mes = Convert.ToInt32(reader["MES"]);

                    // Filtrar meses fuera del rango solicitado y evitar duplicados
                    var fecha = new DateTime(ano, mes, 1);
                    if (fecha < new DateTime(fechaInicio.Year, fechaInicio.Month, 1)) continue;
                    var key = $"{ano}-{mes}";
                    if (!seen.Add(key)) continue;

                    result.Add(new NivelMorosidadDto
                    {
                        Ano        = ano,
                        Mes        = mes,
                        SaldoSoles = reader["SALDO_SOLES"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["SALDO_SOLES"]),
                        VencSoles  = reader["VENC_SOLES"]  == DBNull.Value ? 0m : Convert.ToDecimal(reader["VENC_SOLES"]),
                        IndSoles   = reader["IND_SOLES"]   == DBNull.Value ? 0m : Convert.ToDecimal(reader["IND_SOLES"]),
                        SaldoDolar = reader["SALDO_DOLAR"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["SALDO_DOLAR"]),
                        VencDolar  = reader["VENC_DOLAR"]  == DBNull.Value ? 0m : Convert.ToDecimal(reader["VENC_DOLAR"]),
                        IndDolar   = reader["IND_DOLAR"]   == DBNull.Value ? 0m : Convert.ToDecimal(reader["IND_DOLAR"]),
                    });
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
                    result.Add(new NivelMorosidadDto { Ano = cur2.Year, Mes = cur2.Month });
                }
                cur2 = cur2.AddMonths(1);
            }

            result = result.OrderBy(x => x.Ano).ThenBy(x => x.Mes).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener nivel de morosidad");
        }

        return result;
    }
}
