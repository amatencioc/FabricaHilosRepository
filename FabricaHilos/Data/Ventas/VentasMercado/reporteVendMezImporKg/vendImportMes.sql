SELECT A.GRUPO,
       A.VENDEDOR,
       A.DESCRIPCION,
       A.FECHA,
       (A.MONTO - NVL(B.MONTO, 0)) MONTO
  FROM (SELECT T.INDICADOR2 GRUPO,
               A.VENDEDOR,
               T.DESCRIPCION,
               TO_CHAR(A.FECHA, 'YYYY/MM') FECHA,
               DECODE(:P_OPCION,
                      'TODOS',
                      SUM(DECODE(:P_MON, 'S', SOLES, DOLARES)),
                      SUM(DECODE(:P_MON, 'S', SOLES_SINANT, DOLARES_SINANT))) MONTO
          FROM V_DOCUVEN A, TABLAS_AUXILIARES T
         WHERE A.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
           AND T.TIPO(+) = 29
           AND T.CODIGO(+) = A.VENDEDOR
         GROUP BY T.INDICADOR2,
                  A.VENDEDOR,
                  T.DESCRIPCION,
                  TO_CHAR(A.FECHA, 'YYYY/MM')) A,
       (SELECT T.INDICADOR2 GRUPO,
               D.COD_VENDE VENDEDOR,
               T.DESCRIPCION,
               TO_CHAR(D.FECHA, 'YYYY/MM') FECHA,
               SUM(DECODE(:P_MON,
                          'S',
                          DECODE(D.MONEDA,
                                 'S',
                                 I.IMP_VVTA,
                                 ROUND(IMP_VVTA * D.IMPORT_CAM, 2)),
                          DECODE(D.MONEDA,
                                 'D',
                                 I.IMP_VVTA,
                                 ROUND(IMP_VVTA / D.IMPORT_CAM, 2)))) MONTO
          FROM DOCUVENT D, ITEMDOCU I, TABLAS_AUXILIARES T
         WHERE :P_OPCION <> 'TODOS'
           AND D.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
           AND D.ESTADO <> '9'
           AND I.TIPODOC = D.TIPODOC
           AND I.SERIE = D.SERIE
           AND I.NUMERO = D.NUMERO
           AND I.COD_ART IN ('9300049997',
                             '9300049999',
                             '930004999A',
                             '9300049998')
           AND T.TIPO(+) = 29
           AND T.CODIGO(+) = D.COD_VENDE
         GROUP BY T.INDICADOR2,
                  D.COD_VENDE,
                  T.DESCRIPCION,
                  TO_CHAR(D.FECHA, 'YYYY/MM')) B
 WHERE B.GRUPO(+) = A.GRUPO
   AND B.VENDEDOR(+) = A.VENDEDOR
   AND B.FECHA(+) = A.FECHA
UNION ALL
SELECT '3' GRUPO,
       '00' VENDEDOR,
       'INTERES' DESCRIPCION,
       TO_CHAR(:P_FECHA2, 'YYYY/MM'),
       SUM(DECODE(:P_MON,
                  'S',
                  DECODE(D.MONEDA,
                         'S',
                         (D.IMP_INTERES + NVL(I.INTERES, 0)),
                         ROUND((D.IMP_INTERES + NVL(I.INTERES, 0)) *
                               D.IMPORT_CAM,
                               2)),
                  DECODE(D.MONEDA,
                         'D',
                         (D.IMP_INTERES + NVL(I.INTERES, 0)),
                         ROUND((D.IMP_INTERES + NVL(I.INTERES, 0)) /
                               D.IMPORT_CAM,
                               2)))) MONTO
  FROM DOCUVENT D,
       (SELECT D.TIPODOC, D.SERIE, D.NUMERO, SUM(I.IMP_VVTA) INTERES
          FROM DOCUVENT D, ITEMDOCU I
         WHERE D.ESTADO <> '9'
           AND D.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
           AND I.TIPODOC = D.TIPODOC
           AND I.SERIE = D.SERIE
           AND I.NUMERO = D.NUMERO
           AND I.COD_ART IN ('N/A08', 'N/A09')
         GROUP BY D.TIPODOC, D.SERIE, D.NUMERO) I
 WHERE :P_OPCION <> 'TODOS'
   AND D.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
   AND I.TIPODOC(+) = D.TIPODOC
   AND I.SERIE(+) = D.SERIE
   AND I.NUMERO(+) = D.NUMERO
 GROUP BY TO_CHAR(:P_FECHA2, 'YYYY/MM')
UNION ALL
SELECT '3' GRUPO,
       '00' VENDEDOR,
       'FLETE Y SEGURO' DESCRIPCION,
       TO_CHAR(D.FECHA, 'YYYY/MM') FECHA,
       SUM(DECODE(:P_MON,
                  'S',
                  DECODE(D.MONEDA,
                         'S',
                         I.IMP_VVTA,
                         ROUND(IMP_VVTA * D.IMPORT_CAM, 2)),
                  DECODE(D.MONEDA,
                         'D',
                         I.IMP_VVTA,
                         ROUND(IMP_VVTA / D.IMPORT_CAM, 2)))) MONTO
  FROM DOCUVENT D, ITEMDOCU I
 WHERE :P_OPCION <> 'TODOS'
   AND D.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
   AND D.ESTADO <> '9'
   AND I.TIPODOC = D.TIPODOC
   AND I.SERIE = D.SERIE
   AND I.NUMERO = D.NUMERO
   AND I.COD_ART IN
       ('9300049997', '9300049999', '930004999A', '9300049998')
 GROUP BY TO_CHAR(D.FECHA, 'YYYY/MM')
UNION ALL
SELECT '3' GRUPO,
       '00' VENDEDOR,
       'POR OTROS GASTOS' DESCRIPCION,
       TO_CHAR(D.FECHA, 'YYYY/MM') FECHA,
       SUM(DECODE(:P_MON,
                  'S',
                  DECODE(D.MONEDA,
                         'S',
                         I.IMP_VVTA,
                         ROUND(I.IMP_VVTA * D.IMPORT_CAM, 2)),
                  DECODE(D.MONEDA,
                         'D',
                         I.IMP_VVTA,
                         ROUND(IMP_VVTA / D.IMPORT_CAM, 2)))) MONTO
  FROM DOCUVENT D, ITEMDOCU I, ARTICUL A
 WHERE :P_OPCION <> 'TODOS'
   AND D.TIPODOC = '08'
   AND D.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
   AND D.ESTADO <> '9'
   AND I.TIPODOC = D.TIPODOC
   AND I.SERIE = D.SERIE
   AND I.NUMERO = D.NUMERO
   AND A.COD_ART = I.COD_ART
   AND A.TP_ART = 'Z'
 GROUP BY TO_CHAR(D.FECHA, 'YYYY/MM')
UNION ALL
SELECT '3' GRUPO,
       '00' VENDEDOR,
       'COMPROBANTES GRATUITOS' DESCRIPCION,
       TO_CHAR(D.FECHA, 'YYYY/MM') FECHA,
       SUM(DECODE(:P_MON,
                  'S',
                  DECODE(D.MONEDA,
                         'S',
                         I.IMP_VVTA,
                         ROUND(I.IMP_VVTA * D.IMPORT_CAM, 2)),
                  DECODE(D.MONEDA,
                         'D',
                         I.IMP_VVTA,
                         ROUND(IMP_VVTA / D.IMPORT_CAM, 2)))) MONTO
  FROM DOCUVENT D, ITEMDOCU I
 WHERE :P_OPCION <> 'TODOS'
   AND D.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
   AND D.ESTADO <> '9'
   AND NVL(D.TGRAT, 'N') = 'S'
   AND I.TIPODOC = D.TIPODOC
   AND I.SERIE = D.SERIE
   AND I.NUMERO = D.NUMERO
 GROUP BY TO_CHAR(D.FECHA, 'YYYY/MM')
UNION ALL
SELECT '3' GRUPO,
       '00' VENDEDOR,
       'COMPROBANTE POR ANTICIPOS ' DESCRIPCION,
       TO_CHAR(D.FECHA, 'YYYY/MM') FECHA,
       SUM(DECODE(:P_MON,
                  'S',
                  DECODE(D.MONEDA,
                         'S',
                         I.IMP_VVTA,
                         ROUND(IMP_VVTA * D.IMPORT_CAM, 2)),
                  DECODE(D.MONEDA,
                         'D',
                         I.IMP_VVTA,
                         ROUND(IMP_VVTA / D.IMPORT_CAM, 2)))) MONTO
  FROM DOCUVENT D, ITEMDOCU I
 WHERE :P_OPCION <> 'TODOS'
   AND D.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
   AND D.ORIGEN = 'A'
   AND D.ESTADO <> '9'
   AND I.TIPODOC = D.TIPODOC
   AND I.SERIE = D.SERIE
   AND I.NUMERO = D.NUMERO
   --AND I.COD_ART IN ('ANTICIPO1', 'ANTICIPO2', 'N/A12', 'N/A13')
 GROUP BY TO_CHAR(D.FECHA, 'YYYY/MM')
UNION ALL
SELECT '3' GRUPO,
       '00' VENDEDOR,
       'APLICACIÓN DE ANTICIPOS ' DESCRIPCION,
       TO_CHAR(D.FECHA, 'YYYY/MM') FECHA,
       (SUM(DECODE(:P_MON,
                   'S',
                   DECODE(D.MONEDA,
                          'S',
                          D.IMP_ANTICIPO,
                          ROUND(D.IMP_ANTICIPO * D.IMPORT_CAM, 2)),
                   DECODE(D.MONEDA,
                          'D',
                          D.IMP_ANTICIPO,
                          ROUND(D.IMP_ANTICIPO / D.IMPORT_CAM, 2)))) *-1) MONTO
  FROM DOCUVENT D
 WHERE :P_OPCION <> 'TODOS'
   AND D.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
   AND D.ESTADO <> '9'
 GROUP BY TO_CHAR(D.FECHA, 'YYYY/MM')
 ORDER BY 4 DESC
