-- ============================================================
-- INTERFACE_ASSITIME  — copia con fix de reset HE (08/05/2026)
--
-- CAMBIO respecto al original:
--   Al inicio del BEGIN se agregó un UPDATE que pone a 0 los
--   conceptos de HE (1010, 1039 y el de dobles por empresa)
--   ANTES de ejecutar el cursor.
--
-- MOTIVO:
--   El cursor usa WHERE VALOR_ORI > 0, por lo que empleados
--   que tuvieron HE autorizadas y luego las desautorizaron
--   calculan VALOR_ORI = 0 y no aparecen en el cursor.
--   Sin reset, el valor anterior queda indefinidamente en
--   INGRE_PLA (planilla recibe horas que ya no corresponden).
--
-- CONCEPTOS reseteados (dependen de autorizaciones):
--   0003-SIG   : 1010 (HE 25%), 1039 (HE 35%), 1072 (dobles)
--   0002-SOLSA : 1010 (HE 25%), 1039 (HE 35%), 1012 (dobles)
--   0001-ARBONA: 1010 (HE 25%), 1039 (HE 35%), 1011 (dobles)
--
-- NO se resetean 1074, 1000, 1022, 1024, 2018 porque se
-- recalculan desde marcaciones y siempre tienen empleados.
-- ============================================================
create or replace PROCEDURE INTERFACE_ASSITIME(P_EMPRESA IN VARCHAR2,
                                               P_NUMPLA  IN NUMBER,
                                               P_FECINI  IN VARCHAR2,
                                               P_FECFIN  IN VARCHAR2) IS
  CURSOR C1 IS
    SELECT C_CODIGO, P_NUMPLA, C_CONCEPTO, VALOR_ORI
      FROM (SELECT TABLA1.C_CODIGO,
                   '1074' C_CONCEPTO,
                   CASE
                     WHEN TABLA1.VALOR_ORI = 0 THEN
                      0
                     ELSE
                      TABLA1.VALOR_ORI + TABLA2.DIASFER
                   END VALOR_ORI
              FROM (SELECT COD_SPRING C_CODIGO,
                           '1074' C_CONCEPTO,
                           ROUND(TRUNC((SUM(CASE
                                              WHEN HORAEFECTIVA IS NOT NULL THEN
                                               (TO_NUMBER(TO_CHAR(DATEADD(HORAEFECTIVA,
                                                                          NVL(HORATARDANZA,
                                                                              TO_DATE('01/01/1900',
                                                                                      'DD/MM/YYYY'))),
                                                                  'HH24')) * 60) +
                                               TO_NUMBER(TO_CHAR(DATEADD(HORAEFECTIVA,
                                                                         NVL(HORATARDANZA,
                                                                             TO_DATE('01/01/1900',
                                                                                     'DD/MM/YYYY'))),
                                                                 'MI'))
                                              ELSE
                                               0
                                            END) / 60) / 8,
                                       2),
                                 0) VALOR_ORI
                      FROM SCA_ASISTENCIA_TAREO S
                     INNER JOIN PLA_PERSONAL P
                        ON S.COD_EMPRESA = P.COD_EMPRESA
                       AND S.COD_PERSONAL = P.COD_PERSONAL
                     WHERE FECHAMAR BETWEEN TO_DATE(P_FECINI, 'DD/MM/YYYY') AND
                           TO_DATE(P_FECFIN, 'DD/MM/YYYY')
                       AND P.COD_EMPRESA = P_EMPRESA
                       AND P.COD_TIPO_PLANILLA = '05'
                     GROUP BY COD_SPRING, P.COD_TIPO_PLANILLA) TABLA1
              LEFT JOIN (SELECT C_CODIGO, SUM(TOTHORASFER) / 60 / 8 DIASFER
                          FROM (SELECT COD_SPRING C_CODIGO,
                                       CASE
                                         WHEN HOLIDAY(SCA_FECHA_PROCESO.FEC_PROCESO,
                                                      P.COD_EMPRESA,
                                                      P.COD_PERSONAL) = 'F' THEN
                                          (TO_NUMBER(TO_CHAR(SCA_HORARIO_DET.TOTHORAS,
                                                             'HH24')) * 60) +
                                          TO_NUMBER(TO_CHAR(SCA_HORARIO_DET.TOTHORAS,
                                                            'MI'))
                                         ELSE
                                          0
                                       END TOTHORASFER
                                  FROM SCA_HORARIO_CAB
                                 INNER JOIN PLA_PERSONAL P
                                 INNER JOIN SCA_HORARIO_PERSONAL
                                    ON P.COD_EMPRESA =
                                       SCA_HORARIO_PERSONAL.COD_EMPRESA
                                   AND P.COD_PERSONAL =
                                       SCA_HORARIO_PERSONAL.COD_PERSONAL ON
                                 SCA_HORARIO_CAB.HORID =
                                       SCA_HORARIO_PERSONAL.HORID
                                 INNER JOIN SCA_FECHA_PROCESO
                                 INNER JOIN SCA_HORARIO_DET
                                    ON SCA_FECHA_PROCESO.DIA_PROCESO =
                                       SCA_HORARIO_DET.DIAID ON
                                 SCA_HORARIO_CAB.HORID =
                                       SCA_HORARIO_DET.HORID
                                 INNER JOIN SCA_FOTOCHECK
                                    ON P.COD_EMPRESA =
                                       SCA_FOTOCHECK.COD_EMPRESA
                                   AND P.COD_PERSONAL =
                                       SCA_FOTOCHECK.COD_PERSONAL
                                  LEFT OUTER JOIN SCA_ASISTENCIA_TAREO
                                    ON P.COD_EMPRESA =
                                       SCA_ASISTENCIA_TAREO.COD_EMPRESA
                                   AND P.COD_PERSONAL =
                                       SCA_ASISTENCIA_TAREO.COD_PERSONAL
                                   AND SCA_FECHA_PROCESO.FEC_PROCESO =
                                       SCA_ASISTENCIA_TAREO.FECHAMAR
                                 WHERE P.COD_EMPRESA = P_EMPRESA
                                   AND P.COD_TIPO_PLANILLA = '05'
                                   AND SCA_FECHA_PROCESO.FEC_PROCESO BETWEEN
                                       TO_DATE(P_FECINI, 'DD/MM/YYYY') AND
                                       TO_DATE(P_FECFIN, 'DD/MM/YYYY')
                                   AND SCA_FECHA_PROCESO.FEC_PROCESO BETWEEN
                                       TO_DATE(FECINI_FOTOCHECK, 'DD/MM/YYYY') AND
                                       (CASE
                                         WHEN FECFIN_FOTOCHECK <> '' THEN
                                          TO_DATE(FECFIN_FOTOCHECK,
                                                  'DD/MM/YYYY')
                                         ELSE
                                          TO_DATE(FECINI_FOTOCHECK,
                                                  'DD/MM/YYYY') + INTERVAL '70'
                                          YEAR(2)
                                       END)
                                   AND FEC_VIGENCIA =
                                       (SELECT MAX(FEC_VIGENCIA)
                                          FROM SCA_HORARIO_PERSONAL B
                                         WHERE B.COD_EMPRESA = P.COD_EMPRESA
                                           AND B.COD_PERSONAL = P.COD_PERSONAL
                                           AND B.FEC_VIGENCIA <=
                                               SCA_FECHA_PROCESO.FEC_PROCESO)
                                   AND APLICA = 'S') TABLA
                         GROUP BY C_CODIGO) TABLA2
                ON TABLA1.C_CODIGO = TABLA2.C_CODIGO
            UNION
            SELECT TABLA1.C_CODIGO,
                   '1000' C_CONCEPTO,
                   CASE
                     WHEN TABLA1.VALOR_ORI = 0 THEN
                      0
                     ELSE
                      TABLA1.VALOR_ORI + TABLA2.HORASFER
                   END VALOR_ORI
              FROM (SELECT COD_SPRING C_CODIGO,
                           TRUNC(SUM(CASE
                                       WHEN HORAEFECTIVA IS NOT NULL THEN
                                        (TO_NUMBER(TO_CHAR(DATEADD(HORAEFECTIVA,
                                                                   NVL(HORATARDANZA,
                                                                       TO_DATE('01/01/1900',
                                                                               'DD/MM/YYYY'))),
                                                           'HH24')) * 60) +
                                        TO_NUMBER(TO_CHAR(DATEADD(HORAEFECTIVA,
                                                                  NVL(HORATARDANZA,
                                                                      TO_DATE('01/01/1900',
                                                                              'DD/MM/YYYY'))),
                                                          'MI'))
                                       ELSE
                                        0
                                     END) / 60,
                                 2) VALOR_ORI
                      FROM SCA_ASISTENCIA_TAREO S
                     INNER JOIN PLA_PERSONAL P
                        ON S.COD_EMPRESA = P.COD_EMPRESA
                       AND S.COD_PERSONAL = P.COD_PERSONAL
                     WHERE FECHAMAR BETWEEN TO_DATE(P_FECINI, 'DD/MM/YYYY') AND
                           TO_DATE(P_FECFIN, 'DD/MM/YYYY')
                       AND P.COD_EMPRESA = P_EMPRESA
                       AND P.COD_TIPO_PLANILLA = '05'
                     GROUP BY COD_SPRING, P.COD_TIPO_PLANILLA) TABLA1
              LEFT JOIN (SELECT C_CODIGO, SUM(TOTHORASFER) / 60 HORASFER
                           FROM (SELECT COD_SPRING C_CODIGO,
                                        CASE
                                          WHEN HOLIDAY(SCA_FECHA_PROCESO.FEC_PROCESO,
                                                       P.COD_EMPRESA,
                                                       P.COD_PERSONAL) = 'F' THEN
                                           (TO_NUMBER(TO_CHAR(SCA_HORARIO_DET.TOTHORAS,
                                                              'HH24')) * 60) +
                                           TO_NUMBER(TO_CHAR(SCA_HORARIO_DET.TOTHORAS,
                                                             'MI'))
                                          ELSE
                                           0
                                        END TOTHORASFER
                                   FROM SCA_HORARIO_CAB
                                  INNER JOIN PLA_PERSONAL P
                                  INNER JOIN SCA_HORARIO_PERSONAL
                                     ON P.COD_EMPRESA =
                                        SCA_HORARIO_PERSONAL.COD_EMPRESA
                                    AND P.COD_PERSONAL =
                                        SCA_HORARIO_PERSONAL.COD_PERSONAL ON
                                  SCA_HORARIO_CAB.HORID =
                                        SCA_HORARIO_PERSONAL.HORID
                                  INNER JOIN SCA_FECHA_PROCESO
                                  INNER JOIN SCA_HORARIO_DET
                                     ON SCA_FECHA_PROCESO.DIA_PROCESO =
                                        SCA_HORARIO_DET.DIAID ON
                                  SCA_HORARIO_CAB.HORID =
                                        SCA_HORARIO_DET.HORID
                                  INNER JOIN SCA_FOTOCHECK
                                     ON P.COD_EMPRESA =
                                        SCA_FOTOCHECK.COD_EMPRESA
                                    AND P.COD_PERSONAL =
                                        SCA_FOTOCHECK.COD_PERSONAL
                                   LEFT OUTER JOIN SCA_ASISTENCIA_TAREO
                                     ON P.COD_EMPRESA =
                                        SCA_ASISTENCIA_TAREO.COD_EMPRESA
                                    AND P.COD_PERSONAL =
                                        SCA_ASISTENCIA_TAREO.COD_PERSONAL
                                    AND SCA_FECHA_PROCESO.FEC_PROCESO =
                                        SCA_ASISTENCIA_TAREO.FECHAMAR
                                  WHERE P.COD_EMPRESA = P_EMPRESA
                                    AND P.COD_TIPO_PLANILLA = '05'
                                    AND SCA_FECHA_PROCESO.FEC_PROCESO BETWEEN
                                        TO_DATE(P_FECINI, 'DD/MM/YYYY') AND
                                        TO_DATE(P_FECFIN, 'DD/MM/YYYY')
                                    AND SCA_FECHA_PROCESO.FEC_PROCESO BETWEEN
                                        TO_DATE(FECINI_FOTOCHECK, 'DD/MM/YYYY') AND
                                        (CASE
                                          WHEN FECFIN_FOTOCHECK <> '' THEN
                                           TO_DATE(FECFIN_FOTOCHECK,
                                                   'DD/MM/YYYY')
                                          ELSE
                                           TO_DATE(FECINI_FOTOCHECK,
                                                   'DD/MM/YYYY') + INTERVAL '70'
                                           YEAR(2)
                                        END)
                                    AND FEC_VIGENCIA =
                                        (SELECT MAX(FEC_VIGENCIA)
                                           FROM SCA_HORARIO_PERSONAL B
                                          WHERE B.COD_EMPRESA = P.COD_EMPRESA
                                            AND B.COD_PERSONAL = P.COD_PERSONAL
                                            AND B.FEC_VIGENCIA <=
                                                SCA_FECHA_PROCESO.FEC_PROCESO)
                                    AND APLICA = 'S') TABLA
                          GROUP BY C_CODIGO) TABLA2
                ON TABLA1.C_CODIGO = TABLA2.C_CODIGO
            UNION
            SELECT COD_SPRING C_CODIGO,
                   '1022' C_CONCEPTO,
                   TRUNC(SUM(CASE
                               WHEN HORAEFECTIVA IS NOT NULL THEN
                                (TO_NUMBER(TO_CHAR(DATEADD(HORAEFECTIVA,
                                                           NVL(HORATARDANZA,
                                                               TO_DATE('01/01/1900',
                                                                       'DD/MM/YYYY'))),
                                                   'HH24')) * 60) +
                                TO_NUMBER(TO_CHAR(DATEADD(HORAEFECTIVA,
                                                          NVL(HORATARDANZA,
                                                              TO_DATE('01/01/1900',
                                                                      'DD/MM/YYYY'))),
                                                  'MI'))
                               ELSE
                                0
                             END) / 60,
                         2) VALOR_ORI
              FROM SCA_ASISTENCIA_TAREO S
             INNER JOIN PLA_PERSONAL P
                ON S.COD_EMPRESA = P.COD_EMPRESA
               AND S.COD_PERSONAL = P.COD_PERSONAL
             WHERE FECHAMAR BETWEEN TO_DATE(P_FECINI, 'DD/MM/YYYY') AND
                   TO_DATE(P_FECFIN, 'DD/MM/YYYY')
               AND HORTUR = 'T2'
               AND P.COD_EMPRESA = P_EMPRESA
               AND P.COD_TIPO_PLANILLA = '05'
             GROUP BY COD_SPRING, P.COD_TIPO_PLANILLA
            UNION
            SELECT COD_SPRING C_CODIGO,
                   '1024' C_CONCEPTO,
                   TRUNC(SUM(CASE
                               WHEN HORAEFECTIVA IS NOT NULL THEN
                                (TO_NUMBER(TO_CHAR(DATEADD(HORAEFECTIVA,
                                                           NVL(HORATARDANZA,
                                                               TO_DATE('01/01/1900',
                                                                       'DD/MM/YYYY'))),
                                                   'HH24')) * 60) +
                                TO_NUMBER(TO_CHAR(DATEADD(HORAEFECTIVA,
                                                          NVL(HORATARDANZA,
                                                              TO_DATE('01/01/1900',
                                                                      'DD/MM/YYYY'))),
                                                  'MI'))
                               ELSE
                                0
                             END) / 60,
                         2) VALOR_ORI
              FROM SCA_ASISTENCIA_TAREO S
             INNER JOIN PLA_PERSONAL P
                ON S.COD_EMPRESA = P.COD_EMPRESA
               AND S.COD_PERSONAL = P.COD_PERSONAL
             WHERE FECHAMAR BETWEEN TO_DATE(P_FECINI, 'DD/MM/YYYY') AND
                   TO_DATE(P_FECFIN, 'DD/MM/YYYY')
               AND HORTUR = 'T3'
               AND P.COD_EMPRESA = P_EMPRESA
               AND P.COD_TIPO_PLANILLA = '05'
             GROUP BY COD_SPRING, P.COD_TIPO_PLANILLA
            UNION
            SELECT *
              FROM (SELECT COD_SPRING C_CODIGO,
                           '2018' C_CONCEPTO,
                           SUM(CASE
                                 WHEN HORATARDANZA IS NOT NULL THEN
                                  (TO_NUMBER(TO_CHAR(HORATARDANZA, 'HH24')) * 60) +
                                  TO_NUMBER(TO_CHAR(HORATARDANZA, 'MI'))
                                 ELSE
                                  0
                               END) VALOR_ORI
                      FROM SCA_ASISTENCIA_TAREO S
                     INNER JOIN PLA_PERSONAL P
                        ON S.COD_EMPRESA = P.COD_EMPRESA
                       AND S.COD_PERSONAL = P.COD_PERSONAL
                     WHERE FECHAMAR BETWEEN TO_DATE(P_FECINI, 'DD/MM/YYYY') AND
                           TO_DATE(P_FECFIN, 'DD/MM/YYYY')
                       AND P.COD_EMPRESA = P_EMPRESA
                       AND P.COD_TIPO_PLANILLA = '05'
                     GROUP BY COD_SPRING, P.COD_TIPO_PLANILLA) TABLA1
             WHERE VALOR_ORI > 10
            UNION
            SELECT COD_SPRING C_CODIGO,
                   '1010' C_CONCEPTO,
                   TRUNC(SUM(CASE
                               WHEN HORAEXOFI1 IS NOT NULL THEN
                                (TO_NUMBER(TO_CHAR(HORAEXOFI1, 'HH24')) * 60) +
                                TO_NUMBER(TO_CHAR(HORAEXOFI1, 'MI'))
                               ELSE
                                0
                             END) / 60,
                         2) VALOR_ORI
              FROM SCA_ASISTENCIA_TAREO S
             INNER JOIN PLA_PERSONAL P
                ON S.COD_EMPRESA = P.COD_EMPRESA
               AND S.COD_PERSONAL = P.COD_PERSONAL
             WHERE FECHAMAR BETWEEN TO_DATE(P_FECINI, 'DD/MM/YYYY') AND
                   TO_DATE(P_FECFIN, 'DD/MM/YYYY')
               AND P.COD_EMPRESA = P_EMPRESA
               AND P.COD_TIPO_PLANILLA = '05'
             GROUP BY COD_SPRING, P.COD_TIPO_PLANILLA
            UNION
            SELECT COD_SPRING C_CODIGO,
                   '1039' C_CONCEPTO,
                   TRUNC(SUM(CASE
                               WHEN HORAEXOFI2 IS NOT NULL THEN
                                (TO_NUMBER(TO_CHAR(HORAEXOFI2, 'HH24')) * 60) +
                                TO_NUMBER(TO_CHAR(HORAEXOFI2, 'MI'))
                               ELSE
                                0
                             END) / 60,
                         2) VALOR_ORI
              FROM SCA_ASISTENCIA_TAREO S
             INNER JOIN PLA_PERSONAL P
                ON S.COD_EMPRESA = P.COD_EMPRESA
               AND S.COD_PERSONAL = P.COD_PERSONAL
             WHERE FECHAMAR BETWEEN TO_DATE(P_FECINI, 'DD/MM/YYYY') AND
                   TO_DATE(P_FECFIN, 'DD/MM/YYYY')
               AND P.COD_EMPRESA = P_EMPRESA
               AND P.COD_TIPO_PLANILLA = '05'
             GROUP BY COD_SPRING, P.COD_TIPO_PLANILLA
            UNION
            SELECT COD_SPRING C_CODIGO,
                   CASE
                     WHEN P_EMPRESA = '0003' THEN
                      '1072'
                     WHEN P_EMPRESA = '0002' THEN
                      '1012'
                     WHEN P_EMPRESA = '0001' THEN
                      '1011'
                   END C_CONCEPTO,
                   TRUNC(SUM(CASE
                               WHEN HORADOBLESOF IS NOT NULL THEN
                                (TO_NUMBER(TO_CHAR(HORADOBLESOF, 'HH24')) * 60) +
                                TO_NUMBER(TO_CHAR(HORADOBLESOF, 'MI'))
                               ELSE
                                0
                             END) / 60,
                         2) VALOR_ORI
              FROM SCA_ASISTENCIA_TAREO S
             INNER JOIN PLA_PERSONAL P
                ON S.COD_EMPRESA = P.COD_EMPRESA
               AND S.COD_PERSONAL = P.COD_PERSONAL
             WHERE FECHAMAR BETWEEN TO_DATE(P_FECINI, 'DD/MM/YYYY') AND
                   TO_DATE(P_FECFIN, 'DD/MM/YYYY')
               AND P.COD_EMPRESA = P_EMPRESA
               AND P.COD_TIPO_PLANILLA = '05'
             GROUP BY COD_SPRING, P.COD_TIPO_PLANILLA) TABLA
     WHERE VALOR_ORI > 0;
BEGIN
  -- >>>>>>>>>>>>>>>>>>>>>>>>>> INICIO CAMBIO 08/05/2026 <<<<<<<<<<<<<<<<<<<<<<<<
  -- Motivo  : Portal web de aprobación y desaprobación de Horas Extras
  --           (HEA, HED, Dobles), desarrollado por Victor Matencio.
  --           El portal permite que los supervisores autoricen o retiren
  --           la autorización de HE directamente desde un navegador.
  --           Cuando un supervisor DES-autoriza una HE, el tareo recalcula
  --           HORAEXOFI1/2 = 0, por lo que el cursor (WHERE VALOR_ORI > 0)
  --           ya no devuelve al empleado y el valor antiguo permanecería
  --           en INGRE_PLA indefinidamente.  El reset previo garantiza que
  --           los conceptos de HE queden en 0 antes de que el cursor
  --           actualice solo a quienes sí tienen HE autorizadas.
  -- Nota    : Solo se resetean conceptos que dependen de autorizaciones
  --           (1010 HE25%, 1039 HE35%, 1072/1012/1011 Dobles).
  --           Los conceptos 1074, 1000, 1022, 1024 y 2018 no se tocan
  --           porque se calculan desde marcaciones y siempre tienen datos.
  IF P_EMPRESA = '0003' THEN
    UPDATE SIG.INGRE_PLA
       SET VALOR_ORI = 0
     WHERE NUM_PLA    = P_NUMPLA
       AND C_CONCEPTO IN ('1010','1039','1072');
  ELSIF P_EMPRESA = '0002' THEN
    UPDATE SOLSA.INGRE_PLA
       SET VALOR_ORI = 0
     WHERE NUM_PLA    = P_NUMPLA
       AND C_CONCEPTO IN ('1010','1039','1012');
  ELSIF P_EMPRESA = '0001' THEN
    UPDATE ARBONA.INGRE_PLA
       SET VALOR_ORI = 0
     WHERE NUM_PLA    = P_NUMPLA
       AND C_CONCEPTO IN ('1010','1039','1011');
  END IF;
  -- >>>>>>>>>>>>>>>>>>>>>>>>>>>> FIN CAMBIO 08/05/2026 <<<<<<<<<<<<<<<<<<<<<<<<<

  FOR I IN C1 LOOP
    IF P_EMPRESA = '0003' THEN
      UPDATE SIG.INGRE_PLA
         SET VALOR_ORI = I.VALOR_ORI
       WHERE C_CODIGO   = TO_CHAR(I.C_CODIGO)
         AND NUM_PLA    = P_NUMPLA
         AND C_CONCEPTO = I.C_CONCEPTO;
    ELSIF P_EMPRESA = '0002' THEN
      UPDATE SOLSA.INGRE_PLA --@AQUASOL
         SET VALOR_ORI = I.VALOR_ORI
       WHERE C_CODIGO   = TO_CHAR(I.C_CODIGO)
         AND NUM_PLA    = P_NUMPLA
         AND C_CONCEPTO = I.C_CONCEPTO;
    ELSIF P_EMPRESA = '0001' THEN
      UPDATE ARBONA.INGRE_PLA --@AQUARBO
         SET VALOR_ORI = I.VALOR_ORI
       WHERE C_CODIGO   = TO_CHAR(I.C_CODIGO)
         AND NUM_PLA    = P_NUMPLA
         AND C_CONCEPTO = I.C_CONCEPTO;
    END IF;
  END LOOP;
  COMMIT;
END;
/
