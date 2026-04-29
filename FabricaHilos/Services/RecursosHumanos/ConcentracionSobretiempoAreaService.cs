using FabricaHilos.Models.RecursosHumanos;
using Oracle.ManagedDataAccess.Client;

namespace FabricaHilos.Services.RecursosHumanos;

public interface IConcentracionSobretiempoAreaService
{
    Task<ConcentracionSobretiempoAreaViewModel> ObtenerKpiAsync(int anoIni, int mesIni, int anoFin, int mesFin);
}

public class ConcentracionSobretiempoAreaService : IConcentracionSobretiempoAreaService
{
    private readonly string _connStr;
    private readonly ILogger<ConcentracionSobretiempoAreaService> _logger;

    private const string SqlKpi = @"
WITH BASE AS (
  SELECT X.ANO,
         X.MES,
         Y.DESC_GRAN_CCOSTO AS AREA,
         P.C_CODIGO,
         SUM(I.VALOR_CAL) SOBRETIEMPO
  FROM PARAMPLA X,
       PLANILLA P,
         INGRE_PLA I,
         T_CONCEPTO T,
         PLA_COSTO C,
         V_CENTRO_DE_COSTOS Y
  WHERE X.ANO BETWEEN :P_ANO_INI AND :P_ANO_FIN
    AND (
        (:P_ANO_INI = :P_ANO_FIN AND X.MES BETWEEN :P_MES_INI AND :P_MES_FIN)
        OR (:P_ANO_INI <> :P_ANO_FIN AND (
            (X.ANO = :P_ANO_INI AND X.MES BETWEEN 1 AND :P_MES_INI)
            OR (X.ANO > :P_ANO_INI AND X.ANO < :P_ANO_FIN)
            OR (X.ANO = :P_ANO_FIN AND X.MES BETWEEN 1 AND :P_MES_FIN)
        ))
    )
    AND X.TIPO_PLA = 'N'
    AND P.NUM_PLA = X.NUM_PLA
    AND I.NUM_PLA = P.NUM_PLA
    AND I.C_CODIGO = P.C_CODIGO
    AND T.C_ID = I.C_ID
    AND T.C_EO = I.C_EO
    AND T.C_CONCEPTO = I.C_CONCEPTO
    AND T.C_CODRTPS IN ('0107','0105','0106')
    AND C.NUM_PLA = P.NUM_PLA
    AND C.C_CODIGO = P.C_CODIGO
    AND Y.CCOSTO_DET = C.C_COSTO
  GROUP BY X.ANO, X.MES, Y.DESC_GRAN_CCOSTO, P.C_CODIGO
)
SELECT
  ANO,
  MES,
  AREA,
  COUNT(C_CODIGO)                                                                       TOTAL_TRAB,
  COUNT(CASE WHEN SOBRETIEMPO > 0 THEN 1 END)                                          CON_HE,
  COUNT(CASE WHEN NVL(SOBRETIEMPO, 0) = 0 THEN 1 END)                                  SIN_HE,
  ROUND(COUNT(CASE WHEN SOBRETIEMPO > 0 THEN 1 END)
        / NULLIF(COUNT(C_CODIGO), 0) * 100, 1)                                         PCT_CON_HE,
  ROUND(COUNT(CASE WHEN NVL(SOBRETIEMPO, 0) = 0 THEN 1 END)
        / NULLIF(COUNT(C_CODIGO), 0) * 100, 1)                                         PCT_SIN_HE
FROM BASE
GROUP BY ANO, MES, AREA
ORDER BY ANO, MES, PCT_CON_HE DESC";

    public ConcentracionSobretiempoAreaService(IConfiguration configuration, ILogger<ConcentracionSobretiempoAreaService> logger)
    {
        _connStr = configuration.GetConnectionString("LaColonialConnection")
            ?? throw new InvalidOperationException("LaColonialConnection not found.");
        _logger = logger;
    }

    public async Task<ConcentracionSobretiempoAreaViewModel> ObtenerKpiAsync(int anoIni, int mesIni, int anoFin, int mesFin)
    {
        var vm = new ConcentracionSobretiempoAreaViewModel
        {
            AnoIni = anoIni,
            MesIni = mesIni,
            AnoFin = anoFin,
            MesFin = mesFin
        };

        await using var conn = new OracleConnection(_connStr);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = SqlKpi;
        cmd.BindByName  = true;
        cmd.Parameters.Add(new OracleParameter("P_ANO_INI", OracleDbType.Int32) { Value = anoIni });
        cmd.Parameters.Add(new OracleParameter("P_MES_INI", OracleDbType.Int32) { Value = mesIni });
        cmd.Parameters.Add(new OracleParameter("P_ANO_FIN", OracleDbType.Int32) { Value = anoFin });
        cmd.Parameters.Add(new OracleParameter("P_MES_FIN", OracleDbType.Int32) { Value = mesFin });

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            vm.Filas.Add(new ConcentracionSobretiempoAreaDto
            {
                Ano       = GetInt(r, "ANO"),
                Mes       = GetInt(r, "MES"),
                Area      = r["AREA"]?.ToString() ?? string.Empty,
                TotalTrab = GetInt(r, "TOTAL_TRAB"),
                ConHe     = GetInt(r, "CON_HE"),
                SinHe     = GetInt(r, "SIN_HE"),
                PctConHe  = GetDecimal(r, "PCT_CON_HE"),
                PctSinHe  = GetDecimal(r, "PCT_SIN_HE")
            });
        }

        return vm;
    }

    private static decimal GetDecimal(System.Data.Common.DbDataReader r, string col)
        => r[col] == DBNull.Value ? 0m : Convert.ToDecimal(r[col]);

    private static int GetInt(System.Data.Common.DbDataReader r, string col)
        => r[col] == DBNull.Value ? 0 : Convert.ToInt32(r[col]);
}
