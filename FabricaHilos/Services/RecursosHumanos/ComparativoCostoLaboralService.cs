using FabricaHilos.Models.RecursosHumanos;
using Microsoft.Extensions.Caching.Distributed;
using Oracle.ManagedDataAccess.Client;
using System.Text.Json;

namespace FabricaHilos.Services.RecursosHumanos;

public interface IComparativoCostoLaboralService
{
    Task<ComparativoCostoLaboralViewModel> ObtenerKpiAsync(
        int ano1, int mesIniAno1, int mesFinAno1,
        int ano2, int mesIniAno2, int mesFinAno2,
        decimal basicoManual = 0m,
        string tipo = "T");
}

// ════════════════════════════════════════════════════════════════════════════
//  ComparativoCostoLaboralService — KPI Comparativo Año1 vs Año2 (por área)
//  ─────────────────────────────────────────────────────────────────────────
//  EL "COSTO LABORAL" SE DEFINE COMO Básico × 1.4232 (Opción C confirmada).
//
//  Composición del factor (todos % sobre el básico):
//    Sueldo Básico   100.00 %  ← concepto RTPS '0121' en planilla mensual
//  + EsSalud           9.00 %   Ley 26790 (carga patronal mensual)
//  + CTS               8.33 %   1/12 — devengado mensual
//  + Gratificaciones  16.66 %   2/12 — Julio y Diciembre, devengado mensual
//  + Vacaciones        8.33 %   1/12 — devengado mensual
//  ──────────────────────────────
//  Total             142.32 %   →  Factor 1.4232  (constante FactorTotal)
//
//  Por qué se calcula y no se lee de planilla:
//   • En la planilla mensual NORMAL ('N') sólo se paga el básico.
//   • Vacaciones, Gratificaciones, EsSalud Vida y CTS se devengan mes a mes
//     pero se desembolsan en planillas especiales o pagos directos en
//     fechas distintas (Jul/Dic para grat., May/Nov para CTS, etc.).
//   • Por ello el costo laboral REAL mensual = básico × 1.4232 (estándar
//     contable en hilanderías peruanas — alineado con el PDF del cliente:
//     S/ 1,243.11 × 1.4233 ≈ S/ 1,769.19).
//
//  SQL: una sola pasada con CTE SRC (mismo patrón que CostoSalarialHorasExtras
//  para reutilizar cache de plan, costo bajo). Devuelve por (ANO, AREA):
//     NRO_TRAB_PROM, BASICO_TOTAL, HE_TOTAL, BASICO_PROM_MES, HE_PROM_MES,
//     BASICO_PROM_X_TRAB. Todo lo demás se calcula en C# para mantener
//     el SQL simple y portable.
// ════════════════════════════════════════════════════════════════════════════
public class ComparativoCostoLaboralService : IComparativoCostoLaboralService
{
    private readonly string _connStr;
    private readonly ILogger<ComparativoCostoLaboralService> _logger;
    private readonly IDistributedCache _cache;
    private const string CACHE_KEY_PREFIX = "ComparativoCostoLab_v2_";
    private const int CACHE_DURATION_MINUTES = 60;

    // Devuelve una fila por (ANO, AREA) con promedios mensuales y totales.
    // El filtro: AÑO=:P_ANO con MES BETWEEN :P_MES_INI AND :P_MES_FIN.
    // Para ejecutarlo dos veces (año1 y año2) se invoca el comando dos veces.
    //
    //  Filtro TIPO DE EMPLEADO (PARAMPLA.C_EO):
    //    'T' → Todos (ignora el filtro)
    //    'O' → Solo Obreros   (C_EO = 'O')
    //    'E' → Solo Empleados (C_EO = 'E')
    //  Se aplica como (:P_TIPO = 'T' OR X.C_EO = :P_TIPO) — un solo SQL
    //  parametrizado (sin SQL dinámico) que reutiliza el plan cacheado.
    private const string SqlPorAno = @"
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
  WHERE X.ANO      = :P_ANO
    AND X.MES      BETWEEN :P_MES_INI AND :P_MES_FIN
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
POR_MES_AREA AS (
  SELECT ANO, MES, AREA,
         COUNT(DISTINCT C_CODIGO)                                              NRO_TRAB,
         SUM(CASE WHEN C_CODRTPS = '0121' THEN VALOR_CAL ELSE 0 END)           BASICO,
         SUM(CASE WHEN C_CODRTPS IN ('0105','0106','0107') THEN VALOR_CAL ELSE 0 END) HE
  FROM   SRC
  GROUP BY ANO, MES, AREA
)
SELECT ANO,
       AREA,
       COUNT(DISTINCT MES)                          NUM_MESES,
       ROUND(AVG(NRO_TRAB), 1)                      NRO_TRAB_PROM,
       ROUND(SUM(BASICO), 2)                        BASICO_TOTAL,
       ROUND(SUM(HE),     2)                        HE_TOTAL,
       ROUND(AVG(BASICO), 2)                        BASICO_PROM_MES,
       ROUND(AVG(HE),     2)                        HE_PROM_MES,
       ROUND(AVG(BASICO / NULLIF(NRO_TRAB, 0)), 2)  BASICO_PROM_X_TRAB
FROM   POR_MES_AREA
GROUP BY ANO, AREA
ORDER BY ANO, AREA";

    public ComparativoCostoLaboralService(
        IConfiguration configuration,
        ILogger<ComparativoCostoLaboralService> logger,
        IDistributedCache cache)
    {
        _connStr = configuration.GetConnectionString("LaColonialConnection")
            ?? throw new InvalidOperationException("LaColonialConnection not found.");
        _logger = logger;
        _cache  = cache;
    }

    private string GenerateCacheKey(int a1, int mi1, int mf1, int a2, int mi2, int mf2, decimal basicoManual, string tipo)
        => $"{CACHE_KEY_PREFIX}{a1}_{mi1:D2}-{mf1:D2}_vs_{a2}_{mi2:D2}-{mf2:D2}_BM{basicoManual:0.##}_T{tipo}";

    public async Task<ComparativoCostoLaboralViewModel> ObtenerKpiAsync(
        int ano1, int mesIniAno1, int mesFinAno1,
        int ano2, int mesIniAno2, int mesFinAno2,
        decimal basicoManual = 0m,
        string tipo = "T")
    {
        // Normaliza: cualquier valor <= 0 se trata como "no manual" (vuelve al cálculo desde planilla).
        if (basicoManual < 0m) basicoManual = 0m;

        // Normaliza filtro de tipo: cualquier valor distinto de 'O' o 'E' → 'T' (todos).
        tipo = (tipo ?? "T").Trim().ToUpperInvariant();
        if (tipo != "O" && tipo != "E") tipo = "T";

        var cacheKey = GenerateCacheKey(ano1, mesIniAno1, mesFinAno1, ano2, mesIniAno2, mesFinAno2, basicoManual, tipo);

        // ── Caché ──
        try
        {
            var cached = await _cache.GetAsync(cacheKey);
            if (cached != null)
            {
                var json = System.Text.Encoding.UTF8.GetString(cached);
                var hit  = JsonSerializer.Deserialize<ComparativoCostoLaboralViewModel>(json);
                if (hit != null)
                {
                    _logger.LogInformation("Cache hit ComparativoCostoLaboral: {Key}", cacheKey);
                    return hit;
                }
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Cache get failed {Key}", cacheKey); }

        var vm = new ComparativoCostoLaboralViewModel
        {
            Ano1 = ano1, MesIniAno1 = mesIniAno1, MesFinAno1 = mesFinAno1,
            Ano2 = ano2, MesIniAno2 = mesIniAno2, MesFinAno2 = mesFinAno2,
            BasicoManual = basicoManual,
            Tipo = tipo,
        };

        try
        {
            await using var conn = new OracleConnection(_connStr);
            await conn.OpenAsync();

            // Ejecutar el query dos veces (una por año). Esto permite usar
            // rangos de meses distintos en cada año (ej. compara YTD parcial).
            vm.FilasCrudo.AddRange(await EjecutarPorAnoAsync(conn, ano1, mesIniAno1, mesFinAno1, tipo));
            vm.FilasCrudo.AddRange(await EjecutarPorAnoAsync(conn, ano2, mesIniAno2, mesFinAno2, tipo));

            ConstruirCuadros(vm);

            // ── Guardar cache ──
            try
            {
                var json = JsonSerializer.Serialize(vm);
                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                var opts = new DistributedCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));
                await _cache.SetAsync(cacheKey, bytes, opts);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Cache set failed {Key}", cacheKey); }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ComparativoCostoLaboral ({A1}/{A2})", ano1, ano2);
            throw;
        }

        return vm;
    }

    private static async Task<List<ComparativoCostoLaboralFilaDto>> EjecutarPorAnoAsync(
        OracleConnection conn, int ano, int mesIni, int mesFin, string tipo)
    {
        var filas = new List<ComparativoCostoLaboralFilaDto>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = SqlPorAno;
        cmd.BindByName  = true;
        cmd.Parameters.Add(new OracleParameter("P_ANO",     OracleDbType.Int32)    { Value = ano });
        cmd.Parameters.Add(new OracleParameter("P_MES_INI", OracleDbType.Int32)    { Value = mesIni });
        cmd.Parameters.Add(new OracleParameter("P_MES_FIN", OracleDbType.Int32)    { Value = mesFin });
        cmd.Parameters.Add(new OracleParameter("P_TIPO",    OracleDbType.Varchar2) { Value = tipo });

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            filas.Add(new ComparativoCostoLaboralFilaDto
            {
                Ano             = GetInt    (r, "ANO"),
                Area            = r["AREA"]?.ToString() ?? string.Empty,
                NumMeses        = GetInt    (r, "NUM_MESES"),
                NroTrabProm     = GetDecimal(r, "NRO_TRAB_PROM"),
                BasicoTotal     = GetDecimal(r, "BASICO_TOTAL"),
                HeTotal         = GetDecimal(r, "HE_TOTAL"),
                BasicoPromMes   = GetDecimal(r, "BASICO_PROM_MES"),
                HePromMes       = GetDecimal(r, "HE_PROM_MES"),
                BasicoPromXTrab = GetDecimal(r, "BASICO_PROM_X_TRAB"),
            });
        }
        return filas;
    }

    // ────────────────────────────────────────────────────────────────────────
    //  Construye los 2 cuadros a partir de las filas crudo (Año1 + Año2).
    //  TODO el cálculo de beneficios sociales (factor 1.4232) se hace aquí.
    //
    //  REDONDEO Δ PERSONAS (Cuadros 1 y 2):
    //   La diferencia de personas se redondea al entero más cercano usando
    //   MidpointRounding.AwayFromZero, de modo que:
    //     0.6 → 1,  4.2 → 4,  0.2 → 0,  −0.6 → −1,  −4.2 → −4.
    //   A partir de este entero redondeado se recalcula el Δ Costo y el
    //   Impacto Mensual / Anual para que todo sea internamente consistente.
    //
    //  Override de básico manual (vm.BasicoManual > 0):
    //   • El básico por trabajador deja de leerse de planilla y pasa a ser el
    //     valor único ingresado por el usuario (mismo para Año1 y Año2).
    //   • El N° de trabajadores promedio se mantiene tal cual sale de planilla.
    //   • Costo laboral por mes = BasicoManual × NroTrabProm × 1.4232.
    //   • Costo laboral por trabajador = BasicoManual × 1.4232.
    //  Si BasicoManual == 0 → se usa el básico calculado desde planilla
    //  (comportamiento original).
    // ────────────────────────────────────────────────────────────────────────
    private static void ConstruirCuadros(ComparativoCostoLaboralViewModel vm)
    {
        var porAno = vm.FilasCrudo
            .GroupBy(f => f.Ano)
            .ToDictionary(g => g.Key, g => g.ToDictionary(f => f.Area, f => f));

        if (!porAno.TryGetValue(vm.Ano1, out var fA1)) fA1 = new();
        if (!porAno.TryGetValue(vm.Ano2, out var fA2)) fA2 = new();

        var areas = fA1.Keys.Union(fA2.Keys).OrderBy(a => a).ToList();

        bool usarManual = vm.BasicoManual > 0m;
        decimal factor   = ComparativoCostoLaboralConstants.FactorTotal;

        // ── Cuadro 1 ──
        foreach (var area in areas)
        {
            fA1.TryGetValue(area, out var a1);
            fA2.TryGetValue(area, out var a2);

            decimal nro1 = a1?.NroTrabProm ?? 0m;
            decimal nro2 = a2?.NroTrabProm ?? 0m;

            decimal c1 = usarManual
                ? Math.Round(vm.BasicoManual * nro1 * factor, 2)
                : (a1?.CostoLaboralPromMes ?? 0m);
            decimal c2 = usarManual
                ? Math.Round(vm.BasicoManual * nro2 * factor, 2)
                : (a2?.CostoLaboralPromMes ?? 0m);

            // Δ Personas redondeado al entero: 0.6→1, 4.2→4, 0.2→0
            decimal difRaw        = nro2 - nro1;
            decimal difRedondeada = Math.Round(difRaw, 0, MidpointRounding.AwayFromZero);

            // Δ Costo recalculado a partir del Δ Personas redondeado × costo/trab Año2
            decimal costoXTrabA2c1 = usarManual
                ? Math.Round(vm.BasicoManual * factor, 2)
                : (a2?.CostoLaboralPromXTrab ?? 0m);
            decimal difCosto = Math.Round(difRedondeada * costoXTrabA2c1, 2);

            // VariacionCostoPct recalculado en base al Δ Costo redondeado
            decimal varPct = c1 > 0 ? Math.Round(difCosto / c1 * 100m, 2) : 0m;

            vm.Cuadro1.Add(new ComparativoCuadro1Dto
            {
                Area                        = area,
                NroTrabPromAno1             = nro1,
                NroTrabPromAno2             = nro2,
                CostoLaboralPromMesAno1     = c1,
                CostoLaboralPromMesAno2     = c2,
                DiferenciaNroTrab           = difRaw,
                DiferenciaNroTrabRedondeada = difRedondeada,
                DiferenciaCostoMes          = difCosto,
                VariacionCostoPct           = varPct,
            });
        }

        // ── Cuadro 2 ──
        // SEMÁNTICA DEL IMPACTO (validada con el usuario, jun/2026):
        //   • DiferenciaNroTrab = nro2 − nro1   → variación REAL de dotación
        //                                          (positiva = creció, negativa = bajó).
        //   • Impacto = (nro1 − nro2_redondeado) × CostoXTrabAño2
        //       → POSITIVO  = AHORRO    (la dotación BAJÓ en Año 2 ⇒ menos costo)
        //       → NEGATIVO  = SOBRECOSTO (la dotación SUBIÓ en Año 2 ⇒ más costo)
        //   Usa el mismo redondeo que Cuadro 1.
        foreach (var area in areas)
        {
            fA1.TryGetValue(area, out var a1);
            fA2.TryGetValue(area, out var a2);

            decimal nro1 = a1?.NroTrabProm ?? 0m;
            decimal nro2 = a2?.NroTrabProm ?? 0m;

            decimal difRaw        = nro2 - nro1;
            decimal difRedondeada = Math.Round(difRaw, 0, MidpointRounding.AwayFromZero);

            decimal costoXTrabA2 = usarManual
                ? Math.Round(vm.BasicoManual * factor, 2)
                : (a2?.CostoLaboralPromXTrab ?? 0m);

            // Impacto usa Δ redondeado: (nro1 − nro2_redondeado) × CostoXTrab
            decimal impacto = Math.Round(-difRedondeada * costoXTrabA2, 2);   // + ahorro, − sobrecosto

            vm.Cuadro2.Add(new ComparativoCuadro2Dto
            {
                Area                        = area,
                NroTrabPromAno1             = nro1,
                NroTrabPromAno2             = nro2,
                DiferenciaNroTrab           = difRaw,
                DiferenciaNroTrabRedondeada = difRedondeada,
                CostoXTrabAno2              = costoXTrabA2,
                ImpactoMensual              = impacto,
                ImpactoAnualEstimado        = Math.Round(impacto * 12m, 2),
            });
        }

        // ── Totales generales ──
        vm.TotalNroTrabAno1      = vm.Cuadro1.Sum(c => c.NroTrabPromAno1);
        vm.TotalNroTrabAno2      = vm.Cuadro1.Sum(c => c.NroTrabPromAno2);
        vm.TotalCostoLaboralAno1 = vm.Cuadro1.Sum(c => c.CostoLaboralPromMesAno1);
        vm.TotalCostoLaboralAno2 = vm.Cuadro1.Sum(c => c.CostoLaboralPromMesAno2);
        vm.TotalImpactoMensual   = vm.Cuadro2.Sum(c => c.ImpactoMensual);
        vm.TotalImpactoAnual     = vm.Cuadro2.Sum(c => c.ImpactoAnualEstimado);
    }

    private static decimal GetDecimal(System.Data.Common.DbDataReader r, string col)
        => r[col] == DBNull.Value ? 0m : Convert.ToDecimal(r[col]);

    private static int GetInt(System.Data.Common.DbDataReader r, string col)
        => r[col] == DBNull.Value ? 0 : Convert.ToInt32(r[col]);
}
