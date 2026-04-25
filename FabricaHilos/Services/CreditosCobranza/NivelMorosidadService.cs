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
        var result = new List<NivelMorosidadDto>();
        var connStr = GetOracleConnectionString();
        if (string.IsNullOrEmpty(connStr)) return result;

        // El script original usa :FECHA como parámetro de un solo periodo.
        // Para rango (inicio–fin) iteramos por cada mes del rango con el último día del mes.
        // El script filtra: S.ANO = año de :FECHA y S.MES <= mes de :FECHA,
        // por lo que para obtener sólo el mes exacto ejecutamos por cada mes del rango
        // y restamos el acumulado del mes anterior.
        // Para mantener el script IDÉNTICO, lo ejecutamos acumulado por año/mes y filtramos en .NET.

        var sql = @"SELECT S.ANO,
           S.MES,
           ROUND(SUM(DECODE(S.MONEDA, 'D', S.SALDO * S.TCAM_SAL, S.SALDO)),
                 2) SALDO_SOLES,
           ROUND(SUM(DECODE(DECODE(F.TIPDOC,
                                   '07',
                                   0,
                                   GREATEST(LAST_DAY(TO_DATE('01/'||LPAD(S.MES,2,'0')||'/'||S.ANO,'DD/MM/YYYY')), F_VENCTO) - F.F_VENCTO),
                            0,
                            0,
                            DECODE(S.MONEDA,
                                   'D',
                                   S.SALDO * S.TCAM_SAL,
                                   S.SALDO))),
                 2) VENC_SOLES,
           ROUND(ROUND(SUM(DECODE(DECODE(F.TIPDOC,
                                   '07',
                                   0,
                                   GREATEST(LAST_DAY(TO_DATE('01/'||LPAD(S.MES,2,'0')||'/'||S.ANO,'DD/MM/YYYY')), F_VENCTO) - F.F_VENCTO),
                            0,
                            0,
                            DECODE(S.MONEDA,
                                   'D',
                                   S.SALDO * S.TCAM_SAL,
                                   S.SALDO))),
                 2) /
           ROUND(SUM(DECODE(S.MONEDA, 'D', S.SALDO * S.TCAM_SAL, S.SALDO)),
                 2) * 100,2) IND_SOLES,
               ROUND(SUM(DECODE(S.MONEDA, 'D', S.SALDO, S.SALDO / S.TCAM_SAL)), 2) SALDO_DOLAR,
           ROUND(SUM(DECODE(DECODE(F.TIPDOC,
                                   '07',
                                   0,
                                   GREATEST(LAST_DAY(TO_DATE('01/'||LPAD(S.MES,2,'0')||'/'||S.ANO,'DD/MM/YYYY')), F_VENCTO) - F.F_VENCTO),
                            0,
                            0,
                            DECODE(S.MONEDA,
                                   'D',
                                   S.SALDO,
                                   S.SALDO / S.TCAM_SAL))),
                 2) VENC_DOLAR,
           ROUND(ROUND(SUM(DECODE(DECODE(F.TIPDOC,
                                   '07',
                                   0,
                                   GREATEST(LAST_DAY(TO_DATE('01/'||LPAD(S.MES,2,'0')||'/'||S.ANO,'DD/MM/YYYY')), F_VENCTO) - F.F_VENCTO),
                            0,
                            0,
                            DECODE(S.MONEDA,
                                   'D',
                                   S.SALDO,
                                   S.SALDO / S.TCAM_SAL))),
                 2) /
           ROUND(SUM(DECODE(S.MONEDA, 'D', S.SALDO, S.SALDO / S.TCAM_SAL)),
                 2) * 100,2) IND_DOLAR
      FROM SALDOS_CXC S, FACTCOB F
     WHERE S.TIPDOC NOT IN ('A1')
       AND SUBSTR(S.CTACTBLE, 5, 2) IN ('12', '13')
       AND S.ANO = TO_NUMBER(TO_CHAR(:FECHA, 'YYYY'))
       AND S.MES <= TO_NUMBER(TO_CHAR(:FECHA, 'MM'))
       AND F.TIPDOC = S.TIPDOC
       AND F.SERIE_NUM = S.SERIE_NUM
       AND F.NUMERO = S.NUMERO
     GROUP BY S.ANO, S.MES";

        try
        {
            // Collect all month-end dates in the range
            var fechas = new List<DateTime>();
            var cur = new DateTime(fechaInicio.Year, fechaInicio.Month, 1);
            var end = new DateTime(fechaFin.Year, fechaFin.Month, 1);
            while (cur <= end)
            {
                fechas.Add(new DateTime(cur.Year, cur.Month, DateTime.DaysInMonth(cur.Year, cur.Month)));
                cur = cur.AddMonths(1);
            }

            // Execute once per month to get exact monthly snapshot
            var seen = new HashSet<string>();
            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();

            foreach (var fecha in fechas)
            {
                await using var cmd = new OracleCommand(sql, conn);
                cmd.Parameters.Add(new OracleParameter("FECHA", OracleDbType.Date) { Value = fecha });

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var ano = Convert.ToInt32(reader["ANO"]);
                    var mes = Convert.ToInt32(reader["MES"]);
                    var key = $"{ano}-{mes}";
                    if (!seen.Contains(key))
                    {
                        seen.Add(key);
                        result.Add(new NivelMorosidadDto
                        {
                            Ano       = ano,
                            Mes       = mes,
                            SaldoSoles = reader["SALDO_SOLES"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["SALDO_SOLES"]),
                            VencSoles  = reader["VENC_SOLES"]  == DBNull.Value ? 0m : Convert.ToDecimal(reader["VENC_SOLES"]),
                            IndSoles   = reader["IND_SOLES"]   == DBNull.Value ? 0m : Convert.ToDecimal(reader["IND_SOLES"]),
                            SaldoDolar = reader["SALDO_DOLAR"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["SALDO_DOLAR"]),
                            VencDolar  = reader["VENC_DOLAR"]  == DBNull.Value ? 0m : Convert.ToDecimal(reader["VENC_DOLAR"]),
                            IndDolar   = reader["IND_DOLAR"]   == DBNull.Value ? 0m : Convert.ToDecimal(reader["IND_DOLAR"]),
                        });
                    }
                }
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
