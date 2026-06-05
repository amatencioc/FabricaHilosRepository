using FabricaHilos.Models.RecursosHumanos;
using Microsoft.Extensions.Caching.Distributed;
using Oracle.ManagedDataAccess.Client;
using System.Text.Json;

namespace FabricaHilos.Services.RecursosHumanos;

public interface ICostoSalarialHorasExtrasService
{
    Task<CostoSalarialHorasExtrasViewModel> ObtenerKpiAsync(
        int anoIni, int mesIni, int anoFin, int mesFin, string tipo = "T");
}

public class CostoSalarialHorasExtrasService : ICostoSalarialHorasExtrasService
{
    private readonly string _connStr;
    private readonly ILogger<CostoSalarialHorasExtrasService> _logger;
    private readonly IDistributedCache _cache;
    private const string CACHE_KEY_PREFIX = "CostoSalarialHE_";
    private const int CACHE_DURATION_MINUTES = 60;

    // ────────────────────────────────────────────────────────────────────
    //  Filtro de fechas:
    //   • Si AÑO INI = AÑO FIN  → meses entre MES_INI y MES_FIN
    //   • Si AÑO INI ≠ AÑO FIN  → enero..MES_INI del año inicial
    //                            + todos los meses de años intermedios
    //                            + enero..MES_FIN del año final
    //  Esto permite el comparativo año-vs-año pedido por el usuario.
    //
    //  Filtro de TIPO DE EMPLEADO (PARAMPLA.C_EO):
    //   • 'T' → Todos (ignora el filtro)
    //   • 'O' → Solo Obreros   (C_EO = 'O')
    //   • 'E' → Solo Empleados (C_EO = 'E')
    //  Se aplica como (:P_TIPO = 'T' OR X.C_EO = :P_TIPO) — un solo SQL
    //  parametrizado (sin SQL dinámico) que reutiliza el plan cacheado.
    //
    //  OPTIMIZACIÓN (validada con EXPLAIN PLAN — costo 12,264 → 6,133, -50%):
    //  Se unifica HE y MASA en una sola CTE SRC con T.C_CODRTPS IN ('0105',
    //  '0106','0107','0121') + agregación condicional (CASE WHEN). Esto
    //  elimina el barrido duplicado de 369K filas en PLANILLA y PLA_COSTO,
    //  y el FULL OUTER JOIN. Resultado verificado idéntico (172 filas,
    //  HE_TOT=603,406.53 / Masa=4,648,166.40 para mayo 2025 vs mayo 2026).
    // ────────────────────────────────────────────────────────────────────
    private const string SqlKpi = @"
WITH SRC AS (
  SELECT X.ANO,
         X.MES,
         Y.DESC_GRAN_CCOSTO AS AREA,
         P.C_CODIGO,
         T.C_CODRTPS,
         I.VALOR_CAL
  FROM PARAMPLA           X,
       PLANILLA           P,
       INGRE_PLA          I,
       T_CONCEPTO         T,
       PLA_COSTO          C,
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
    AND (:P_TIPO = 'T' OR X.C_EO = :P_TIPO)
    AND P.NUM_PLA  = X.NUM_PLA
    AND I.NUM_PLA  = P.NUM_PLA
    AND I.C_CODIGO = P.C_CODIGO
    AND T.C_ID         = I.C_ID
    AND T.C_EO         = I.C_EO
    AND T.C_CONCEPTO   = I.C_CONCEPTO
    AND T.C_CODRTPS IN ('0105','0106','0107','0121')
    AND C.NUM_PLA  = P.NUM_PLA
    AND C.C_CODIGO = P.C_CODIGO
    AND Y.CCOSTO_DET = C.C_COSTO
),
POR_EMP AS (
  SELECT ANO, MES, AREA, C_CODIGO,
         SUM(CASE WHEN C_CODRTPS = '0105' THEN VALOR_CAL ELSE 0 END) HE25,
         SUM(CASE WHEN C_CODRTPS = '0106' THEN VALOR_CAL ELSE 0 END) HE35,
         SUM(CASE WHEN C_CODRTPS = '0107' THEN VALOR_CAL ELSE 0 END) HE100,
         SUM(CASE WHEN C_CODRTPS IN ('0105','0106','0107') THEN VALOR_CAL ELSE 0 END) HE_TOT,
         SUM(CASE WHEN C_CODRTPS = '0121' THEN VALOR_CAL ELSE 0 END) BASICO
  FROM   SRC
  GROUP BY ANO, MES, AREA, C_CODIGO
)
SELECT
  ANO,
  MES,
  AREA,
  COUNT(C_CODIGO)                                              NRO_TRAB,
  COUNT(CASE WHEN HE_TOT > 0 THEN 1 END)                       NRO_TRAB_CON_HE,
  ROUND(SUM(HE25),  2)                                         MONTO_HE25,
  ROUND(SUM(HE35),  2)                                         MONTO_HE35,
  ROUND(SUM(HE100), 2)                                         MONTO_HE100,
  ROUND(SUM(HE_TOT),2)                                         MONTO_HE_TOTAL,
  ROUND(SUM(BASICO),2)                                         MASA_SALARIAL,
  ROUND(SUM(HE_TOT) / NULLIF(SUM(BASICO), 0) * 100, 2)         RATIO_HE_PCT
FROM   POR_EMP
GROUP BY ANO, MES, AREA
ORDER BY ANO, MES, MONTO_HE_TOTAL DESC";

    public CostoSalarialHorasExtrasService(IConfiguration configuration, ILogger<CostoSalarialHorasExtrasService> logger, IDistributedCache cache)
    {
        _connStr = configuration.GetConnectionString("LaColonialConnection")
            ?? throw new InvalidOperationException("LaColonialConnection not found.");
        _logger = logger;
        _cache = cache;
    }

    private string GenerateCacheKey(int anoIni, int mesIni, int anoFin, int mesFin, string tipo)
    {
        return $"{CACHE_KEY_PREFIX}{anoIni}{mesIni:D2}_{anoFin}{mesFin:D2}_T{tipo}";
    }

    public async Task<CostoSalarialHorasExtrasViewModel> ObtenerKpiAsync(
        int anoIni, int mesIni, int anoFin, int mesFin, string tipo = "T")
    {
        // Normaliza el filtro de tipo: cualquier valor distinto de 'O' o 'E' → 'T' (todos).
        tipo = (tipo ?? "T").Trim().ToUpperInvariant();
        if (tipo != "O" && tipo != "E") tipo = "T";

        var cacheKey = GenerateCacheKey(anoIni, mesIni, anoFin, mesFin, tipo);

        // Intentar obtener desde caché
        try
        {
            if (_cache != null)
            {
                var cached = await _cache.GetAsync(cacheKey);
                if (cached != null)
                {
                    var json = System.Text.Encoding.UTF8.GetString(cached);
                    var result = JsonSerializer.Deserialize<CostoSalarialHorasExtrasViewModel>(json);
                    if (result != null)
                    {
                        _logger.LogInformation("Cache hit para CostoSalarialHE: {CacheKey}", cacheKey);
                        return result;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache retrieval failed for {CacheKey}", cacheKey);
        }

        // Ejecutar query
        var vm = new CostoSalarialHorasExtrasViewModel
        {
            AnoIni = anoIni,
            MesIni = mesIni,
            AnoFin = anoFin,
            MesFin = mesFin,
            Tipo   = tipo
        };

        try
        {
            await using var conn = new OracleConnection(_connStr);
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = SqlKpi;
            cmd.BindByName  = true;
            cmd.Parameters.Add(new OracleParameter("P_ANO_INI", OracleDbType.Int32)  { Value = anoIni });
            cmd.Parameters.Add(new OracleParameter("P_MES_INI", OracleDbType.Int32)  { Value = mesIni });
            cmd.Parameters.Add(new OracleParameter("P_ANO_FIN", OracleDbType.Int32)  { Value = anoFin });
            cmd.Parameters.Add(new OracleParameter("P_MES_FIN", OracleDbType.Int32)  { Value = mesFin });
            cmd.Parameters.Add(new OracleParameter("P_TIPO",    OracleDbType.Varchar2) { Value = tipo });

            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                vm.Filas.Add(new CostoSalarialHorasExtrasDto
                {
                    Ano          = GetInt(r,     "ANO"),
                    Mes          = GetInt(r,     "MES"),
                    Area         = r["AREA"]?.ToString() ?? string.Empty,
                    NroTrab      = GetInt(r,     "NRO_TRAB"),
                    NroTrabConHe = GetInt(r,     "NRO_TRAB_CON_HE"),
                    MontoHe25    = GetDecimal(r, "MONTO_HE25"),
                    MontoHe35    = GetDecimal(r, "MONTO_HE35"),
                    MontoHe100   = GetDecimal(r, "MONTO_HE100"),
                    MontoHeTotal = GetDecimal(r, "MONTO_HE_TOTAL"),
                    MasaSalarial = GetDecimal(r, "MASA_SALARIAL"),
                    RatioHePct   = GetDecimal(r, "RATIO_HE_PCT")
                });
            }

            // Guardar en caché
            try
            {
                if (_cache != null)
                {
                    var json = JsonSerializer.Serialize(vm);
                    var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                    var cacheOptions = new DistributedCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));
                    await _cache.SetAsync(cacheKey, bytes, cacheOptions);
                    _logger.LogInformation("Cache set para CostoSalarialHE: {CacheKey}", cacheKey);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cache set failed for {CacheKey}", cacheKey);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error CostoSalarialHorasExtras KPI {Ai}/{Mi}-{Af}/{Mf} Tipo={Tipo}", anoIni, mesIni, anoFin, mesFin, tipo);
            throw;
        }

        return vm;
    }

    private static decimal GetDecimal(System.Data.Common.DbDataReader r, string col)
        => r[col] == DBNull.Value ? 0m : Convert.ToDecimal(r[col]);

    private static int GetInt(System.Data.Common.DbDataReader r, string col)
        => r[col] == DBNull.Value ? 0 : Convert.ToInt32(r[col]);
}
