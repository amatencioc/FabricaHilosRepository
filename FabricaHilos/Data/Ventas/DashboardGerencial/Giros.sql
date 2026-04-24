SELECT
 C.GIRO,
 (SELECT MAX(t2.ABREVIADA)
   FROM TABLAS_AUXILIARES t2
  WHERE TIPO = 27 AND codigo = C.GIRO
  GROUP BY CODIGO) NOMBRE_GIRO,
 SUM(I.CANTIDAD * E.FACTOR) TOTUNID,
 ROUND(SUM(DECODE(A.MONEDA,
              'S',
              (I.IMP_VVTA *
              ((100 - A.POR_DESC1) * (100 - A.POR_DESC2) / 10000)),
              ((I.IMP_VVTA *
              ((100 - A.POR_DESC1) * (100 - A.POR_DESC2) / 10000)) *
              A.IMPORT_CAM))),2) SOLES,
	ROUND(SUM(DECODE(A.MONEDA,
              'D',
              (I.IMP_VVTA *
              ((100 - A.POR_DESC1) * (100 - A.POR_DESC2) / 10000)),
              ((I.IMP_VVTA *
              ((100 - A.POR_DESC1) * (100 - A.POR_DESC2) / 10000)) /
              A.IMPORT_CAM))),2) DOLAR,			 
	ROUND(SUM(DECODE(A.MONEDA,
              'S',
              (((I.IMP_VVTA * ((100 - A.POR_DESC1) *
              (100 - A.POR_DESC2) / 10000)) * I.P_IGV)),
              (((I.IMP_VVTA * ((100 - A.POR_DESC1) *
              (100 - A.POR_DESC2) / 10000)) * I.P_IGV)) *
              A.IMPORT_CAM)),2) IGV_SOLES,
       ROUND(SUM(DECODE(A.MONEDA,
              'D',
              (((I.IMP_VVTA * ((100 - A.POR_DESC1) *
              (100 - A.POR_DESC2) / 10000)) * I.P_IGV)),
              (((I.IMP_VVTA * ((100 - A.POR_DESC1) *
              (100 - A.POR_DESC2) / 10000)) * I.P_IGV)) /
              A.IMPORT_CAM)),2) IGV_DOLAR
 FROM DOCUVENT A
   LEFT JOIN ITEMDOCU I            ON  I.TIPODOC = A.TIPODOC
                                   AND I.SERIE   = A.SERIE
                                   AND I.NUMERO  = A.NUMERO
   LEFT JOIN EQUIVALENCIA E        ON  E.COD_ART = I.COD_ART
                                   AND E.UNIDAD  = 'KG'
   LEFT JOIN ARTICUL M             ON  M.COD_ART = I.COD_ART
   LEFT JOIN CLIENTES C            ON  C.COD_CLIENTE = A.COD_CLIENTE
   LEFT JOIN TABLAS_AUXILIARES T   ON  T.CODIGO  = C.VENDEDOR
                                   AND T.TIPO    = 29
   LEFT JOIN TABLAS_AUXILIARES T2  ON  T2.CODIGO = C.GIRO
                                   AND T2.TIPO   = 27
   -- ? Pre-calcular MIN cliente por grupo
   LEFT JOIN (SELECT GRUPO, MIN(COD_CLIENTE) AS MIN_CLIENTE
              FROM SIG.CLIENTE_RELACION
              GROUP BY GRUPO) GRP  ON  GRP.GRUPO = C.GRUPO_REL
   -- ? CLL usa el resultado pre-calculado
   LEFT JOIN CLIENTES CLL          ON  CLL.COD_CLIENTE = DECODE(C.GRUPO_REL, NULL, A.COD_CLIENTE, GRP.MIN_CLIENTE)
WHERE A.FECHA BETWEEN :FECHA1 AND :FECHA2
  AND NVL(A.ESTADO,'0') <> 9
  AND (A.ORIGEN <> 'A')
  AND M.TP_ART IN ('T','S')
 GROUP BY  C.GIRO
