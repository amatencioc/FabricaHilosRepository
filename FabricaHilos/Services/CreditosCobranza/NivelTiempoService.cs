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

        var sql = @"SELECT XC.ANO,
       XC.MES,
       XC.SALDO_SOLES,
       FC.VTA_SOLES,
         ROUND((XC.SALDO_SOLES/FC.VTA_SOLES)*30,0) IND_SOLES,
       XC.SALDO_DOLAR,
       FC.VTA_DOLAR,
         ROUND((XC.SALDO_DOLAR/FC.VTA_DOLAR)*30,0) IND_DOLAR
  FROM (SELECT S.ANO,
               S.MES,
               ROUND(SUM(DECODE(S.MONEDA, 'D', S.SALDO * S.TCAM_SAL, S.SALDO)),2) SALDO_SOLES,
               ROUND(SUM(DECODE(S.MONEDA, 'D', S.SALDO, S.SALDO / S.TCAM_SAL)),2) SALDO_DOLAR
          FROM SALDOS_CXC S, FACTCOB F
         WHERE S.TIPDOC NOT IN ('A1')
           AND SUBSTR(S.CTACTBLE, 5, 2) IN ('12', '13')
           AND S.ANO = TO_NUMBER(TO_CHAR(:FECHAF, 'YYYY'))
           AND S.MES <= TO_NUMBER(TO_CHAR(:FECHAF, 'MM'))
           AND F.TIPDOC = S.TIPDOC
           AND F.SERIE_NUM = S.SERIE_NUM
           AND F.NUMERO = S.NUMERO
         GROUP BY S.ANO, S.MES) XC,
       (SELECT TO_NUMBER(TO_CHAR(D.FECHA, 'YYYY')) ANO,
               TO_NUMBER(TO_CHAR(D.FECHA, 'MM')) MES,
               SUM(DECODE(D.MONEDA,
                          'S',
                          D.PRECIO_VTA,
                          ROUND(D.PRECIO_VTA * X.IMPORT_CAM, 2))) VTA_SOLES,
               SUM(DECODE(D.MONEDA,
                          'D',
                          D.PRECIO_VTA,
                          ROUND(D.PRECIO_VTA / X.IMPORT_CAM, 2))) VTA_DOLAR
          FROM DOCUVENT D, CLIENTES C, PLANCTA P, CAMBDOL X
         WHERE D.ESTADO <> '9'
           AND D.FECHA BETWEEN :FECHAI AND :FECHAF
           AND C.COD_CLIENTE = D.COD_CLIENTE
           AND P.CUENTA = D.CTA_PVTA
           AND X.TIPO_CAMBIO = P.TIPO
           AND X.FECHA = LAST_DAY(D.FECHA)
         GROUP BY TO_NUMBER(TO_CHAR(D.FECHA, 'YYYY')),
                  TO_NUMBER(TO_CHAR(D.FECHA, 'MM'))) FC
 WHERE FC.ANO = XC.ANO
   AND FC.MES = XC.MES";

        try
        {
            var fechas = new List<DateTime>();
            var cur = new DateTime(fechaInicio.Year, fechaInicio.Month, 1);
            var end = new DateTime(fechaFin.Year,    fechaFin.Month,    1);
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
                var mesFin    = fecha; // último día del mes

                await using var cmd = new OracleCommand(sql, conn);
                cmd.BindByName = true;
                cmd.Parameters.Add(new OracleParameter("FECHAI", OracleDbType.Date) { Value = mesInicio });
                cmd.Parameters.Add(new OracleParameter("FECHAF", OracleDbType.Date) { Value = mesFin });

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
                            Ano       = ano,
                            Mes       = mes,
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener nivel de tiempo promedio");
        }

        return result;
    }
}
