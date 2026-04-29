using FabricaHilos.Models.RecursosHumanos;
using Oracle.ManagedDataAccess.Client;

namespace FabricaHilos.Services.RecursosHumanos;

public interface IEvolucionMasaSalarialService
{
    Task<EvolucionMasaSalarialViewModel> ObtenerKpiAsync(int anoIni, int mesIni, int anoFin, int mesFin);
}

public class EvolucionMasaSalarialService : IEvolucionMasaSalarialService
{
    private readonly string _connStr;
    private readonly ILogger<EvolucionMasaSalarialService> _logger;

    // ── BLOQUE 1: Detalle por mes y área ────────────────────────────────
    private const string SqlDetalle = @"
WITH MASA AS (
  SELECT X.ANO,
         X.MES,
         Y.DESC_GRAN_CCOSTO AS AREA,
         COUNT(DISTINCT P.C_CODIGO)  NRO_TRAB,
         ROUND(SUM(I.VALOR_CAL), 2)  MASA_SALARIAL
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
    AND T.C_CODRTPS = '0121'
    AND C.NUM_PLA = P.NUM_PLA
    AND C.C_CODIGO = P.C_CODIGO
    AND Y.CCOSTO_DET = C.C_COSTO
  GROUP BY X.ANO, X.MES, Y.DESC_GRAN_CCOSTO
),
HE AS (
  SELECT X.ANO,
         X.MES,
         Y.DESC_GRAN_CCOSTO AS AREA,
         ROUND(SUM(I.VALOR_CAL), 2)  SOBRETIEMPO
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
    AND T.C_CODRTPS IN ('0105','0106','0107')
    AND C.NUM_PLA = P.NUM_PLA
    AND C.C_CODIGO = P.C_CODIGO
    AND Y.CCOSTO_DET = C.C_COSTO
  GROUP BY X.ANO, X.MES, Y.DESC_GRAN_CCOSTO
)
SELECT
  M.ANO,
  M.MES,
  M.AREA,
  M.NRO_TRAB,
  M.MASA_SALARIAL,
  NVL(H.SOBRETIEMPO, 0)                                                    SOBRETIEMPO,
  ROUND(NVL(H.SOBRETIEMPO, 0) / NULLIF(M.MASA_SALARIAL, 0) * 100, 2)     RATIO_HE_PCT
FROM MASA M, HE H
WHERE M.ANO  = H.ANO
  AND M.MES  = H.MES
  AND M.AREA = H.AREA
ORDER BY M.ANO, M.MES, M.MASA_SALARIAL DESC";

    // ── BLOQUE 2: Resumen mensual empresa ────────────────────────────────
    private const string SqlResumen = @"
WITH MASA AS (
  SELECT X.ANO, X.MES,
         ROUND(SUM(I.VALOR_CAL), 2) MASA_SALARIAL
  FROM PARAMPLA X,
       PLANILLA P,
         INGRE_PLA I,
         T_CONCEPTO T
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
    AND T.C_CODRTPS = '0121'
  GROUP BY X.ANO, X.MES
),
HE AS (
  SELECT X.ANO, X.MES,
         ROUND(SUM(I.VALOR_CAL), 2) SOBRETIEMPO
  FROM PARAMPLA X,
       PLANILLA P,
         INGRE_PLA I,
         T_CONCEPTO T
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
    AND T.C_CODRTPS IN ('0105','0106','0107')
  GROUP BY X.ANO, X.MES
)
SELECT
  M.ANO,
  M.MES,
  M.MASA_SALARIAL,
  NVL(H.SOBRETIEMPO, 0)                                                    SOBRETIEMPO,
  ROUND(NVL(H.SOBRETIEMPO, 0) / NULLIF(M.MASA_SALARIAL, 0) * 100, 2)     RATIO_HE_PCT,
  ROUND(M.MASA_SALARIAL - LAG(M.MASA_SALARIAL) OVER (ORDER BY M.ANO, M.MES), 2)  VAR_VS_MES_ANT,
  ROUND((M.MASA_SALARIAL - LAG(M.MASA_SALARIAL) OVER (ORDER BY M.ANO, M.MES))
        / NULLIF(LAG(M.MASA_SALARIAL) OVER (ORDER BY M.ANO, M.MES), 0) * 100, 1) VAR_PCT
FROM MASA M, HE H
WHERE M.ANO = H.ANO
  AND M.MES = H.MES
ORDER BY M.ANO, M.MES";

    public EvolucionMasaSalarialService(IConfiguration configuration, ILogger<EvolucionMasaSalarialService> logger)
    {
        _connStr = configuration.GetConnectionString("LaColonialConnection")
            ?? throw new InvalidOperationException("LaColonialConnection not found.");
        _logger = logger;
    }

    public async Task<EvolucionMasaSalarialViewModel> ObtenerKpiAsync(int anoIni, int mesIni, int anoFin, int mesFin)
    {
        var vm = new EvolucionMasaSalarialViewModel
        {
            AnoIni = anoIni,
            MesIni = mesIni,
            AnoFin = anoFin,
            MesFin = mesFin
        };

        await using var conn = new OracleConnection(_connStr);
        await conn.OpenAsync();

        // ── BLOQUE 1: Detalle por área ─────────────────────────────────
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = SqlDetalle;
            AgregarParametros(cmd, anoIni, mesIni, anoFin, mesFin);

            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                vm.Areas.Add(new EvolucionMasaSalarialAreaDto
                {
                    Ano          = GetInt(r, "ANO"),
                    Mes          = GetInt(r, "MES"),
                    Area         = r["AREA"]?.ToString() ?? string.Empty,
                    NroTrab      = GetInt(r, "NRO_TRAB"),
                    MasaSalarial = GetDecimal(r, "MASA_SALARIAL"),
                    Sobretiempo  = GetDecimal(r, "SOBRETIEMPO"),
                    RatioHePct   = GetDecimal(r, "RATIO_HE_PCT")
                });
            }
        }

        // ── BLOQUE 2: Resumen mensual empresa ──────────────────────────
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = SqlResumen;
            AgregarParametros(cmd, anoIni, mesIni, anoFin, mesFin);

            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                vm.Resumen.Add(new EvolucionMasaSalarialResumenDto
                {
                    Ano          = GetInt(r, "ANO"),
                    Mes          = GetInt(r, "MES"),
                    MasaSalarial = GetDecimal(r, "MASA_SALARIAL"),
                    Sobretiempo  = GetDecimal(r, "SOBRETIEMPO"),
                    RatioHePct   = GetDecimal(r, "RATIO_HE_PCT"),
                    VarVsMesAnt  = GetDecimal(r, "VAR_VS_MES_ANT"),
                    VarPct       = GetDecimal(r, "VAR_PCT")
                });
            }
        }

        return vm;
    }

    private static void AgregarParametros(OracleCommand cmd, int anoIni, int mesIni, int anoFin, int mesFin)
    {
        cmd.BindByName = true;
        cmd.Parameters.Add(new OracleParameter("P_ANO_INI", OracleDbType.Int32) { Value = anoIni });
        cmd.Parameters.Add(new OracleParameter("P_MES_INI", OracleDbType.Int32) { Value = mesIni });
        cmd.Parameters.Add(new OracleParameter("P_ANO_FIN", OracleDbType.Int32) { Value = anoFin });
        cmd.Parameters.Add(new OracleParameter("P_MES_FIN", OracleDbType.Int32) { Value = mesFin });
    }

    private static decimal GetDecimal(System.Data.Common.DbDataReader r, string col)
        => r[col] == DBNull.Value ? 0m : Convert.ToDecimal(r[col]);

    private static int GetInt(System.Data.Common.DbDataReader r, string col)
        => r[col] == DBNull.Value ? 0 : Convert.ToInt32(r[col]);
}
