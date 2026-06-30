-- KPI Desarrollo - Complejidad
-- Verificado contra BD producción 30/06/2026:
--   CS_SOPCOMP.COMPLEJIDAD  → existe pero está 100% nulo (campo sin uso aún).
--   CS_SOPCOMP.PRIORIDAD    → VARCHAR2(2); 01=BAJA · 02=MEDIA · 03=ALTA
--                             decodificada via CS_TABLAS TIPO='4'.
--   Se usa PRIORIDAD como campo de peso/complejidad de cada requerimiento.
--   Los gráficos se agrupan por la descripción de PRIORIDAD (BAJA/MEDIA/ALTA).
SELECT S.NUMERO,
       C_NOMBRE,
       C.C_COSTO,
       X.NOMBRE                                          AS AREA,
       TRUNC(S.FECHA)                                    AS FECHA,
       TRUNC(S.F_APROBACION)                             AS APROBADO,
       REPLACE(S.REQUERIMIENTO, CHR(10), ' ')            AS REQUERIMIENTO,
       REPLACE(S.SOLUCION, CHR(10), ' ')                 AS SOLUCION,
       S.F_SOLUCION_INI                                  AS F_INICIO,
       NVL(S.F_TEST_INI, S.F_SOLUCION)                  AS F_TERMINO,
       S.ESTADO,
       -- Complejidad = decodificación de PRIORIDAD via CS_TABLAS TIPO='4'
       -- Orden: 01-BAJO → 02-MEDIO → 03-ALTO (pesos crecientes en gráficos)
       NVL(TP.DESCRIPCION, '(Sin clasificar)')           AS COMPLEJIDAD,
       NVL(S.PRIORIDAD, '00')                            AS COD_COMPLEJIDAD
  FROM CS_SOPCOMP S
  -- Técnico asignado (outer: puede no tener soporte asignado)
  LEFT JOIN CS_TABLAS T  ON T.TIPO   = '6'  AND T.CODIGO = S.USER_SOPORTE
  -- Descripción de PRIORIDAD (outer: no todos tienen prioridad asignada)
  LEFT JOIN CS_TABLAS TP ON TP.TIPO  = '4'  AND TP.CODIGO = S.PRIORIDAD
  -- Centro de costo solicitante
  JOIN T_CCOSTO        C ON C.C_CODIGO     = S.C_CODIGO
  JOIN CENTRO_DE_COSTOS X ON X.CENTRO_COSTO = C.C_COSTO
 WHERE S.TIPODOC = 'S'
   AND S.MOTIVO  IN ('11', '16')
   AND (
         (S.ESTADO = '1'  AND NVL(S.F_TEST_INI, S.F_SOLUCION) IS NULL)
      OR (S.ESTADO IN ('2') AND NVL(S.F_TEST_INI, S.F_SOLUCION) BETWEEN :P_fecini AND :P_fecfin)
       )
