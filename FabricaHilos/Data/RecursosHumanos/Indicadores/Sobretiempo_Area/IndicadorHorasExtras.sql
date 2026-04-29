-- ============================================================
-- KPI HORAS EXTRAS / SOBRETIEMPO POR ÁREA
-- Parámetros: :P_ANO_INI, :P_MES_INI, :P_ANO_FIN, :P_MES_FIN
--             :P_TIPO  →  'E' = Empleados | 'O' = Obreros | 'T' = Todos
-- ============================================================

-- -------------------------------------------------------
-- BLOQUE 1: RESUMEN GENERAL (KPI Cards del dashboard)
-- -------------------------------------------------------
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
  WHERE (X.ANO * 100 + X.MES) BETWEEN (:P_ANO_INI * 100 + :P_MES_INI) AND (:P_ANO_FIN * 100 + :P_MES_FIN)
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
    AND (:P_TIPO = 'T' OR X.C_EO = :P_TIPO)
  GROUP BY X.ANO, X.MES, Y.DESC_GRAN_CCOSTO, P.C_CODIGO
)
-- RESUMEN GENERAL
SELECT
  ANO,
  MES,
  ROUND(SUM(SOBRETIEMPO), 2)                                        TOTAL_SOBRETIEMPO,
  COUNT(C_CODIGO)                                                    TOTAL_TRABAJADORES,
  ROUND(SUM(SOBRETIEMPO) / NULLIF(COUNT(C_CODIGO), 0), 2)          PROM_HE_POR_TRAB,
  COUNT(CASE WHEN SOBRETIEMPO > 0 THEN 1 END)                       TRAB_CON_HE,
  ROUND(COUNT(CASE WHEN SOBRETIEMPO > 0 THEN 1 END)
        / NULLIF(COUNT(C_CODIGO), 0) * 100, 1)                      PCT_TRAB_CON_HE
FROM BASE
GROUP BY ANO, MES
ORDER BY ANO, MES;

-- -------------------------------------------------------
-- BLOQUE 2: DETALLE POR ÁREA (tabla del dashboard)
-- -------------------------------------------------------
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
  WHERE (X.ANO * 100 + X.MES) BETWEEN (:P_ANO_INI * 100 + :P_MES_INI) AND (:P_ANO_FIN * 100 + :P_MES_FIN)
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
    AND (:P_TIPO = 'T' OR X.C_EO = :P_TIPO)
  GROUP BY X.ANO, X.MES, Y.DESC_GRAN_CCOSTO, P.C_CODIGO
)
SELECT
  ANO,
  MES,
  AREA,
  COUNT(C_CODIGO)                                                          NRO_TRABAJADORES,
  ROUND(SUM(SOBRETIEMPO), 2)                                               TOTAL_SOBRETIEMPO,
  ROUND(SUM(SOBRETIEMPO) / NULLIF(COUNT(C_CODIGO), 0), 2)                 PROM_HE_POR_TRAB,
  ROUND(SUM(SOBRETIEMPO) / SUM(SUM(SOBRETIEMPO)) OVER () * 100, 1)        PCT_DEL_TOTAL_HE,
  COUNT(CASE WHEN SOBRETIEMPO > 0 THEN 1 END)                              TRAB_CON_HE,
  ROUND(COUNT(CASE WHEN SOBRETIEMPO > 0 THEN 1 END)
        / NULLIF(COUNT(C_CODIGO), 0) * 100, 1)                             PCT_TRAB_CON_HE
FROM BASE
GROUP BY ANO, MES, AREA
ORDER BY ANO, MES, TOTAL_SOBRETIEMPO DESC;
