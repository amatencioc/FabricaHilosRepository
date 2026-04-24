SELECT D.COD_CLIENTE,
       C.NOMBRE,
       C.GIRO,
       T2.ABREVIADA DESC_GIRO,
       D.COD_VENDE COD_ASESOR,
       T.DESCRIPCION ASESOR,
       I.TIPODOC,
       I.SERIE,
       I.NUMERO NUMDOC,
       G.COD_ALM,
       D.FECHA,
       A.TP_ART,
       A.COD_FAM,
       A.COD_LIN,
       I.COD_ART,
       DECODE(A.DESCRIPCION,
              'VARIOS',
              I.DETALLE,
              A.DESCRIPCION || ' (' || I.COLOR_DET || ')') DESCRIPCION,
       A.UNIDAD,
       (I.CANTIDAD * E.FACTOR) KILOS,
       I.CANTIDAD,
       I.IMP_VVTA,
       DECODE(D.MONEDA, 'S', 'S', 'D') MON,
       G.NRO_DOC_REF NUMERO1,
       TO_CHAR(D.FECHA, 'YYYY/MM') FEC,
       I.VVTU,
       DECODE(D.MONEDA,
              'S',
              (I.IMP_VVTA *
              ((100 - D.POR_DESC1) * (100 - D.POR_DESC2) / 10000)),
              ((I.IMP_VVTA *
              ((100 - D.POR_DESC1) * (100 - D.POR_DESC2) / 10000)) *
              D.IMPORT_CAM)) SOLES,
       DECODE(D.MONEDA,
              'D',
              (I.IMP_VVTA *
              ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)),
              ((I.IMP_VVTA *
              ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)) /
              D.IMPORT_CAM)) DOLAR,
       I.POR_DESC1,
       I.POR_DESC2
  FROM ITEMDOCU          I,
       DOCUVENT          D,
       KARDEX_G          G,
       EQUIVALENCIA      E,
       ARTICUL           A,
       CLIENTES          C,
       TABLAS_AUXILIARES T,
       TABLAS_AUXILIARES T2
 WHERE D.TIPODOC = I.TIPODOC
   AND D.SERIE = I.SERIE
   AND D.NUMERO = I.NUMERO
   AND D.FECHA BETWEEN :FECHAI AND :FECHAF
   AND D.ESTADO <> '9'
   AND G.TP_TRANSAC(+) = D.TIP_DOC_REF
   AND TO_CHAR(G.SERIE(+)) = D.SER_DOC_REF
   AND G.NUMERO(+) = D.NRO_DOC_REF
   AND G.COD_RELACION(+) = D.COD_CLIENTE
   AND E.COD_ART(+) = I.COD_ART
   AND E.UNIDAD(+) = 'KG'
   AND A.COD_ART = I.COD_ART
   AND C.COD_CLIENTE = D.COD_CLIENTE
   AND T.TIPO(+) = 29
   AND T.CODIGO(+) = D.COD_VENDE
   AND T2.TIPO(+) = 27
   AND T2.CODIGO(+) = C.GIRO
 ORDER BY C.NOMBRE, T.DESCRIPCION, I.TIPODOC, I.SERIE, I.NUMERO

