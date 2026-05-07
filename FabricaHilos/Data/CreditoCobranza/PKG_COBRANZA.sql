CREATE OR REPLACE PACKAGE PKG_COBRANZA AS

  TYPE T_CURSOR IS REF CURSOR;

  /*
    p_tipo   : 'T' = Tiempo en Cartera  |  'M' = Morosidad
    p_fecha  : Fecha de corte de saldos       (solo Tiempo)
    p_fechai : Fecha inicio de ventas         (solo Tiempo)
    p_fechaf : Fecha fin / corte de morosidad (ambas queries)
    p_cursor : Cursor de salida con el resultado
  */
  PROCEDURE ObtenerReporte(
    p_tipo   IN  CHAR,
    p_fecha  IN  DATE,
    p_fechai IN  DATE,
    p_fechaf IN  DATE,
    p_cursor OUT T_CURSOR
  );

END PKG_COBRANZA;
/

CREATE OR REPLACE PACKAGE BODY PKG_COBRANZA AS

  PROCEDURE ObtenerReporte(
    p_tipo   IN  CHAR,
    p_fecha  IN  DATE,
    p_fechai IN  DATE,
    p_fechaf IN  DATE,
    p_cursor OUT T_CURSOR
  ) IS
  BEGIN

    IF p_tipo = 'T' THEN
      -- Tiempo en Cartera
      OPEN p_cursor FOR
        SELECT XC.ANO,
               XC.MES,
               XC.SALDO_SOLES,
               FC.VTA_SOLES,
                 ROUND((XC.SALDO_SOLES / FC.VTA_SOLES) * 30, 0) IND_SOLES,
               XC.SALDO_DOLAR,
               FC.VTA_DOLAR,
                 ROUND((XC.SALDO_DOLAR / FC.VTA_DOLAR) * 30, 0) IND_DOLAR
          FROM (SELECT ANO, MES,
                       ROUND(SUM(SALDO_SOLES), 2) SALDO_SOLES,
                       ROUND(SUM(SALDO_DOLAR),  2) SALDO_DOLAR
                  FROM (
                    SELECT S.ANO,
                           S.MES,
                           ROUND(SUM(DECODE(S.MONEDA, 'D', S.SALDO * S.TCAM_SAL, S.SALDO)), 2) SALDO_SOLES,
                           ROUND(SUM(DECODE(S.MONEDA, 'D', S.SALDO, S.SALDO / S.TCAM_SAL)), 2) SALDO_DOLAR
                      FROM SALDOS_CXC S, FACTCOB F
                     WHERE S.TIPDOC NOT IN ('A1')
                       AND SUBSTR(S.CTACTBLE, 5, 2) IN ('12', '13')
                       AND S.ANO = TO_NUMBER(TO_CHAR(p_fechaf, 'YYYY'))
                       AND S.MES <= TO_NUMBER(TO_CHAR(p_fechaf, 'MM'))
                       AND F.TIPDOC    = S.TIPDOC
                       AND F.SERIE_NUM = S.SERIE_NUM
                       AND F.NUMERO    = S.NUMERO
                     GROUP BY S.ANO, S.MES
                    UNION ALL
                    SELECT TO_NUMBER(TO_CHAR(F.FECHA, 'YYYY'))                                          ANO,
                           TO_NUMBER(TO_CHAR(F.FECHA, 'MM'))                                            MES,
                           ROUND((F.IMPORTE +
                                 NVL(SUM(DECODE(C.MONEDA, F.MONEDA, C.IMPORTE, C.IMPORTE_X)), 0)) *
                                  DECODE(F.MONEDA, 'S', 1, F.TCAM_SAL), 2)                             SALDO_SOLES,
                           ROUND((F.IMPORTE +
                                 NVL(SUM(DECODE(C.MONEDA, F.MONEDA, C.IMPORTE, C.IMPORTE_X)), 0)) /
                                  DECODE(F.MONEDA, 'D', 1, F.TCAM_SAL), 2)                             SALDO_DOLAR
                      FROM FACTCOB F, CABFCOB C, PLANCTA P, CLIENTES Y
                     WHERE F.TIPDOC NOT IN ('A1')
                       AND F.ANO || LPAD(F.MES, 2, 0) <=
                           TO_CHAR(p_fechaf, 'YYYY') || LPAD(TO_CHAR(p_fechaf, 'MM'), 2, 0)
                       AND SUBSTR(F.CTACTBLE, 5, 2) IN ('12', '13')
                       AND C.TIPDOC(+)    = F.TIPDOC
                       AND C.SERIE_NUM(+) = F.SERIE_NUM
                       AND C.NUMERO(+)    = F.NUMERO
                       AND C.FECHA(+)    <= p_fechaf
                       AND P.CUENTA       = F.CTACTBLE
                       AND Y.COD_CLIENTE  = F.COD_CLIENTE
                       AND NOT EXISTS (SELECT 1 FROM SALDOS_CXC S2
                                        WHERE S2.TIPDOC    = F.TIPDOC
                                          AND S2.SERIE_NUM = F.SERIE_NUM
                                          AND S2.NUMERO    = F.NUMERO
                                          AND S2.ANO = TO_NUMBER(TO_CHAR(p_fechaf, 'YYYY'))
                                          AND S2.MES <= TO_NUMBER(TO_CHAR(p_fechaf, 'MM')))
                     HAVING F.IMPORTE +
                            NVL(SUM(DECODE(C.MONEDA, F.MONEDA, C.IMPORTE, C.IMPORTE_X)), 0) <> 0
                      GROUP BY TO_NUMBER(TO_CHAR(F.FECHA, 'YYYY')),
                               TO_NUMBER(TO_CHAR(F.FECHA, 'MM')),
                               F.CTACTBLE, F.COD_CLIENTE,
                               F.TIPDOC, F.SERIE_NUM, F.NUMERO,
                               F.FECHA, F.F_VENCTO, F.MONEDA, F.IMPORTE, F.TCAM_SAL
                  )
                 GROUP BY ANO, MES) XC,
               (SELECT TO_NUMBER(TO_CHAR(D.FECHA, 'YYYY')) ANO,
                       TO_NUMBER(TO_CHAR(D.FECHA, 'MM'))   MES,
                       SUM(DECODE(D.MONEDA, 'S', D.PRECIO_VTA,
                                  ROUND(D.PRECIO_VTA * X.IMPORT_CAM, 2))) VTA_SOLES,
                       SUM(DECODE(D.MONEDA, 'D', D.PRECIO_VTA,
                                  ROUND(D.PRECIO_VTA / X.IMPORT_CAM, 2))) VTA_DOLAR
                  FROM DOCUVENT D, CLIENTES C, PLANCTA P, CAMBDOL X
                 WHERE D.ESTADO <> '9'
                   AND D.FECHA BETWEEN p_fechai AND p_fechaf
                   AND C.COD_CLIENTE = D.COD_CLIENTE
                   AND P.CUENTA      = D.CTA_PVTA
                   AND X.TIPO_CAMBIO = P.TIPO
                   AND X.FECHA       = (SELECT MAX(XX.FECHA) FROM CAMBDOL XX
                                         WHERE XX.TIPO_CAMBIO = P.TIPO
                                           AND XX.FECHA <= LAST_DAY(D.FECHA))
                 GROUP BY TO_NUMBER(TO_CHAR(D.FECHA, 'YYYY')),
                          TO_NUMBER(TO_CHAR(D.FECHA, 'MM'))) FC
         WHERE FC.ANO = XC.ANO
           AND FC.MES = XC.MES;

    ELSIF p_tipo = 'M' THEN
      -- Morosidad
      OPEN p_cursor FOR
        SELECT ANO,
               MES,
               ROUND(SUM(SALDO_SOLES), 2)                                                        AS SALDO_SOLES,
               ROUND(SUM(DECODE(DIAS_VENCTO, 0, 0, SALDO_SOLES)), 2)                            AS VENC_SOLES,
               ROUND(SUM(DECODE(DIAS_VENCTO, 0, 0, SALDO_SOLES)) /
                     DECODE(SUM(SALDO_SOLES), 0, NULL, SUM(SALDO_SOLES)) * 100, 2)              AS IND_SOLES,
               ROUND(SUM(SALDO_DOLAR), 2)                                                        AS SALDO_DOLAR,
               ROUND(SUM(DECODE(DIAS_VENCTO, 0, 0, SALDO_DOLAR)), 2)                            AS VENC_DOLAR,
               ROUND(SUM(DECODE(DIAS_VENCTO, 0, 0, SALDO_DOLAR)) /
                     DECODE(SUM(SALDO_DOLAR), 0, NULL, SUM(SALDO_DOLAR)) * 100, 2)              AS IND_DOLAR
          FROM (
            SELECT S.ANO,
                   S.MES,
                   DECODE(S.MONEDA, 'D', S.SALDO * S.TCAM_SAL, S.SALDO)                        SALDO_SOLES,
                   DECODE(S.MONEDA, 'D', S.SALDO, S.SALDO / S.TCAM_SAL)                        SALDO_DOLAR,
                   DECODE(F.TIPDOC, '07', 0,
                          GREATEST(LAST_DAY(TO_DATE('01/' || LPAD(S.MES, 2, '0') || '/' || S.ANO,
                                                    'DD/MM/YYYY')),
                                   F.F_VENCTO) - F.F_VENCTO)                                   DIAS_VENCTO
              FROM SALDOS_CXC S, FACTCOB F, CLIENTES Y
             WHERE S.TIPDOC NOT IN ('A1')
               AND SUBSTR(S.CTACTBLE, 5, 2) IN ('12', '13')
               AND S.ANO = TO_NUMBER(TO_CHAR(p_fechaf, 'YYYY'))
               AND S.MES <= TO_NUMBER(TO_CHAR(p_fechaf, 'MM'))
               AND F.TIPDOC    = S.TIPDOC
               AND F.SERIE_NUM = S.SERIE_NUM
               AND F.NUMERO    = S.NUMERO
               AND Y.COD_CLIENTE = S.COD_CLIENTE
            UNION ALL
            SELECT TO_NUMBER(TO_CHAR(p_fechaf, 'YYYY'))                                         ANO,
                   TO_NUMBER(TO_CHAR(p_fechaf, 'MM'))                                           MES,
                   ROUND((F.IMPORTE +
                         NVL(SUM(DECODE(C.MONEDA, F.MONEDA, C.IMPORTE, C.IMPORTE_X)), 0)) *
                          DECODE(F.MONEDA, 'S', 1, F.TCAM_SAL), 2)                             SALDO_SOLES,
                   ROUND((F.IMPORTE +
                         NVL(SUM(DECODE(C.MONEDA, F.MONEDA, C.IMPORTE, C.IMPORTE_X)), 0)) /
                          DECODE(F.MONEDA, 'D', 1, F.TCAM_SAL), 2)                             SALDO_DOLAR,
                   DECODE(F.TIPDOC, '07', 0,
                          GREATEST(p_fechaf, F.F_VENCTO) - F.F_VENCTO)                         DIAS_VENCTO
              FROM FACTCOB F, CABFCOB C, PLANCTA P, CLIENTES Y
             WHERE F.TIPDOC NOT IN ('A1')
               AND F.ANO || LPAD(F.MES, 2, 0) <=
                   TO_CHAR(p_fechaf, 'YYYY') || LPAD(TO_CHAR(p_fechaf, 'MM'), 2, 0)
               AND SUBSTR(F.CTACTBLE, 5, 2) IN ('12', '13')
               AND C.TIPDOC(+)    = F.TIPDOC
               AND C.SERIE_NUM(+) = F.SERIE_NUM
               AND C.NUMERO(+)    = F.NUMERO
               AND C.FECHA(+)    <= p_fechaf
               AND P.CUENTA       = F.CTACTBLE
               AND Y.COD_CLIENTE  = F.COD_CLIENTE
               AND NOT EXISTS (SELECT 1 FROM SALDOS_CXC S2
                                WHERE S2.TIPDOC    = F.TIPDOC
                                  AND S2.SERIE_NUM = F.SERIE_NUM
                                  AND S2.NUMERO    = F.NUMERO
                                  AND S2.ANO = TO_NUMBER(TO_CHAR(p_fechaf, 'YYYY'))
                                  AND S2.MES <= TO_NUMBER(TO_CHAR(p_fechaf, 'MM')))
             HAVING F.IMPORTE +
                    NVL(SUM(DECODE(C.MONEDA, F.MONEDA, C.IMPORTE, C.IMPORTE_X)), 0) <> 0
              GROUP BY TO_NUMBER(TO_CHAR(F.FECHA, 'YYYY')),
                       TO_NUMBER(TO_CHAR(F.FECHA, 'MM')),
                       F.CTACTBLE, F.COD_CLIENTE,
                       F.TIPDOC, F.SERIE_NUM, F.NUMERO,
                       F.FECHA, F.F_VENCTO, F.MONEDA, F.IMPORTE, F.TCAM_SAL
          )
         GROUP BY ANO, MES
         ORDER BY ANO, MES;

    END IF;

  END ObtenerReporte;

END PKG_COBRANZA;
/
