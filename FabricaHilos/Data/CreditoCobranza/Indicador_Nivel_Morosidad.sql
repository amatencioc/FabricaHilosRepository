SELECT S.ANO,
           S.MES,
           ROUND(SUM(DECODE(S.MONEDA, 'D', S.SALDO * S.TCAM_SAL, S.SALDO)),
                 2) SALDO_SOLES,
           ROUND(SUM(DECODE(DECODE(F.TIPDOC,
                                   '07',
                                   0,
                                   GREATEST(LAST_DAY(TO_DATE('01/'||LPAD(S.MES,2,'0')||'/'||S.ANO,'DD/MM/YYYY')), F_VENCTO) - F.F_VENCTO),
                            0,
                            0,
                            DECODE(S.MONEDA,
                                   'D',
                                   S.SALDO * S.TCAM_SAL,
                                   S.SALDO))),
                 2) VENC_SOLES,
           ROUND(ROUND(SUM(DECODE(DECODE(F.TIPDOC,
                                   '07',
                                   0,
                                   GREATEST(LAST_DAY(TO_DATE('01/'||LPAD(S.MES,2,'0')||'/'||S.ANO,'DD/MM/YYYY')), F_VENCTO) - F.F_VENCTO),
                            0,
                            0,
                            DECODE(S.MONEDA,
                                   'D',
                                   S.SALDO * S.TCAM_SAL,
                                   S.SALDO))),
                 2) /
           ROUND(SUM(DECODE(S.MONEDA, 'D', S.SALDO * S.TCAM_SAL, S.SALDO)),
                 2) * 100,2) IND_SOLES,
               ROUND(SUM(DECODE(S.MONEDA, 'D', S.SALDO, S.SALDO / S.TCAM_SAL)), 2) SALDO_DOLAR,
           ROUND(SUM(DECODE(DECODE(F.TIPDOC,
                                   '07',
                                   0,
                                   GREATEST(LAST_DAY(TO_DATE('01/'||LPAD(S.MES,2,'0')||'/'||S.ANO,'DD/MM/YYYY')), F_VENCTO) - F.F_VENCTO),
                            0,
                            0,
                            DECODE(S.MONEDA,
                                   'D',
                                   S.SALDO,
                                   S.SALDO / S.TCAM_SAL))),
                 2) VENC_DOLAR,
           ROUND(ROUND(SUM(DECODE(DECODE(F.TIPDOC,
                                   '07',
                                   0,
                                   GREATEST(LAST_DAY(TO_DATE('01/'||LPAD(S.MES,2,'0')||'/'||S.ANO,'DD/MM/YYYY')), F_VENCTO) - F.F_VENCTO),
                            0,
                            0,
                            DECODE(S.MONEDA,
                                   'D',
                                   S.SALDO,
                                   S.SALDO / S.TCAM_SAL))),
                 2) /
           ROUND(SUM(DECODE(S.MONEDA, 'D', S.SALDO, S.SALDO / S.TCAM_SAL)),
                 2) * 100,2) IND_DOLAR
      FROM SALDOS_CXC S, FACTCOB F
     WHERE S.TIPDOC NOT IN ('A1')
       AND SUBSTR(S.CTACTBLE, 5, 2) IN ('12', '13')
       AND S.ANO = TO_NUMBER(TO_CHAR(:FECHA, 'YYYY'))
       AND S.MES <= TO_NUMBER(TO_CHAR(:FECHA, 'MM'))
       AND F.TIPDOC = S.TIPDOC
       AND F.SERIE_NUM = S.SERIE_NUM
       AND F.NUMERO = S.NUMERO
     GROUP BY S.ANO, S.MES