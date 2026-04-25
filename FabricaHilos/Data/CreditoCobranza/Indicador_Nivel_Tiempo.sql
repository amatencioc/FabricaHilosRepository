SELECT XC.ANO,
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
           AND S.ANO = TO_NUMBER(TO_CHAR(:FECHA, 'YYYY'))
           AND S.MES <= TO_NUMBER(TO_CHAR(:FECHA, 'MM'))
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
   AND FC.MES = XC.MES