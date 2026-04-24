
SELECT A.COD_CLIENTE, --72
	   C.RUC,
       C.NOMBRE,
	   C.GIRO,
	   T2.ABREVIADA DESC_GIRO,	   
	   C.VENDEDOR COD_ASESOR,
	   T.DESCRIPCION ASESOR,
       COUNT(A.NUMERO) NRODOC,
       SUM(I.CANTIDAD * E.FACTOR) TOTUNID,   
	   A.MONEDA,   				  
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
  	   , ITEMDOCU I
	   , EQUIVALENCIA E
	   , CLIENTES C
	   , ARTICUL M
	   , TABLAS_AUXILIARES T	   	  
	  , TABLAS_AUXILIARES T2
 WHERE A.FECHA BETWEEN :FECHA1 AND :FECHA2
   AND NVL(A.ESTADO,'0') <> 9
   AND (A.ORIGEN <> 'A')
   AND I.TIPODOC(+) = A.TIPODOC
   AND I.SERIE(+) = A.SERIE
   AND I.NUMERO(+) = A.NUMERO
   AND M.TP_ART IN ('T','S')
   AND M.COD_ART(+) = I.COD_ART
   AND E.COD_ART(+) = I.COD_ART
   AND E.UNIDAD(+) = 'KG'
   AND C.COD_CLIENTE(+) = A.COD_CLIENTE
   AND T.CODIGO(+) = C.VENDEDOR --TABLA C.CLIENTES
   AND T.TIPO(+) = 29
   AND T2.CODIGO(+) = C.GIRO
   AND T2.TIPO(+) = 27
 GROUP BY A.COD_CLIENTE, C.NOMBRE, C.RUC, T.DESCRIPCION, C.GIRO, T2.ABREVIADA, C.VENDEDOR, A.MONEDA
 ORDER BY C.VENDEDOR ASC