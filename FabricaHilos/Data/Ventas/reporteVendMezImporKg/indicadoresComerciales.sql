-- ============================================================
-- INDICADORES COMERCIALES 2026
-- 3 Queries independientes para validación
-- ============================================================
-- Parámetros comunes:
--   :P_FECHA1  Fecha desde (ej: 01/01/2026)
--   :P_FECHA2  Fecha hasta (ej: 09/04/2026)
--   :P_MON     Moneda: 'D' (dólares, default) / 'S' (soles)
-- ============================================================


-- ============================================================
-- QUERY 1: IMPORTE POR ASESOR / MES
-- Opción: SIN CONCEP. INAFECTO A COMISIONES
-- Validar contra: Reporte VESTVEND
-- Gráfico: Barras agrupadas IMPORTE por ASESOR x MES
-- ============================================================

SELECT A.VENDEDOR                       COD_ASESOR,
       A.ASESOR,
       A.MES,
       (A.MONTO - NVL(B.MONTO, 0))     IMPORTE
  FROM (SELECT A.VENDEDOR,
               T.DESCRIPCION                   ASESOR,
               TO_CHAR(A.FECHA, 'YYYY/MM')     MES,
               SUM(DECODE(:P_MON,
                          'S', SOLES_SINANT,
                               DOLARES_SINANT)) MONTO
          FROM V_DOCUVEN A, TABLAS_AUXILIARES T
         WHERE A.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
           AND T.TIPO(+) = 29
           AND T.CODIGO(+) = A.VENDEDOR
         GROUP BY A.VENDEDOR,
                  T.DESCRIPCION,
                  TO_CHAR(A.FECHA, 'YYYY/MM')) A,
       (SELECT D.COD_VENDE                     VENDEDOR,
               TO_CHAR(D.FECHA, 'YYYY/MM')     MES,
               SUM(DECODE(:P_MON,
                          'S',
                          DECODE(D.MONEDA,
                                 'S', I.IMP_VVTA,
                                 ROUND(I.IMP_VVTA * D.IMPORT_CAM, 2)),
                          DECODE(D.MONEDA,
                                 'D', I.IMP_VVTA,
                                 ROUND(I.IMP_VVTA / D.IMPORT_CAM, 2)))) MONTO
          FROM DOCUVENT D, ITEMDOCU I
         WHERE D.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
           AND D.ESTADO <> '9'
           AND I.TIPODOC = D.TIPODOC
           AND I.SERIE = D.SERIE
           AND I.NUMERO = D.NUMERO
           AND I.COD_ART IN ('9300049997', '9300049999',
                             '930004999A', '9300049998')
         GROUP BY D.COD_VENDE,
                  TO_CHAR(D.FECHA, 'YYYY/MM')) B
 WHERE B.VENDEDOR(+) = A.VENDEDOR
   AND B.MES(+) = A.MES
 ORDER BY A.ASESOR, A.MES;


-- ============================================================
-- QUERY 1.1: DETALLE DE IMPORTE POR CLIENTE POR ASESOR / MES
-- Razón social y monto de cada cliente por asesor y mes
-- Drill-down del Query 1 para verificar qué clientes componen
-- el importe de cada asesor
-- Ordenado de mayor a menor importe
-- ============================================================

SELECT A.ASESOR,
       A.MES,
       A.COD_CLIENTE,
       X.RUC,
       X.NOMBRE                          RAZON_SOCIAL,
       (A.MONTO - NVL(B.MONTO, 0))      IMPORTE
  FROM (SELECT A.VENDEDOR,
               T.DESCRIPCION                   ASESOR,
               TO_CHAR(A.FECHA, 'YYYY/MM')     MES,
               A.COD_CLIENTE,
               SUM(DECODE(:P_MON,
                          'S', SOLES_SINANT,
                               DOLARES_SINANT)) MONTO
          FROM V_DOCUVEN A, TABLAS_AUXILIARES T
         WHERE A.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
           AND T.TIPO(+) = 29
           AND T.CODIGO(+) = A.VENDEDOR
           AND T.DESCRIPCION = :P_ASESOR
           AND TO_CHAR(A.FECHA, 'YYYY/MM') = :P_MES
         GROUP BY A.VENDEDOR,
                  T.DESCRIPCION,
                  TO_CHAR(A.FECHA, 'YYYY/MM'),
                  A.COD_CLIENTE) A,
       (SELECT D.COD_VENDE                     VENDEDOR,
               TO_CHAR(D.FECHA, 'YYYY/MM')     MES,
               D.COD_CLIENTE,
               SUM(DECODE(:P_MON,
                          'S',
                          DECODE(D.MONEDA,
                                 'S', I.IMP_VVTA,
                                 ROUND(I.IMP_VVTA * D.IMPORT_CAM, 2)),
                          DECODE(D.MONEDA,
                                 'D', I.IMP_VVTA,
                                 ROUND(I.IMP_VVTA / D.IMPORT_CAM, 2)))) MONTO
          FROM DOCUVENT D, ITEMDOCU I
         WHERE D.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
           AND D.ESTADO <> '9'
           AND I.TIPODOC = D.TIPODOC
           AND I.SERIE = D.SERIE
           AND I.NUMERO = D.NUMERO
           AND I.COD_ART IN ('9300049997', '9300049999',
                             '930004999A', '9300049998')
         GROUP BY D.COD_VENDE,
                  TO_CHAR(D.FECHA, 'YYYY/MM'),
                  D.COD_CLIENTE) B,
       CLIENTES X
 WHERE B.VENDEDOR(+) = A.VENDEDOR
   AND B.COD_CLIENTE(+) = A.COD_CLIENTE
   AND B.MES(+) = A.MES
   AND X.COD_CLIENTE = A.COD_CLIENTE
 ORDER BY IMPORTE DESC;


-- ============================================================
-- QUERY 2: CANTIDAD (KG) POR ASESOR / MES
-- Validar contra: Reporte VCOMPCLMC (sumar totales por asesor)
-- Gráfico: Barras agrupadas CANTIDAD KG por ASESOR x MES
-- ============================================================

SELECT T.DESCRIPCION                    ASESOR,
       TO_CHAR(C.FECHA, 'YYYY/MM')     MES,
       SUM(B.CANTIDAD * E.FACTOR)       CANTIDAD_KG
  FROM ITEMDOCU         B,
       DOCUVENT         C,
       ARTICUL          A,
       TABLAS_AUXILIARES T,
       EQUIVALENCIA     E
 WHERE C.TIPODOC = B.TIPODOC
   AND C.SERIE = B.SERIE
   AND C.NUMERO = B.NUMERO
   AND C.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
   AND C.ESTADO <> '9'
   AND A.TP_ART IN ('T', 'S')
   AND A.COD_ART = B.COD_ART
   AND T.TIPO = 29
   AND T.CODIGO = C.COD_VENDE
   AND E.UNIDAD = 'KG'
   AND E.COD_ART = A.COD_ART
 GROUP BY T.DESCRIPCION,
          TO_CHAR(C.FECHA, 'YYYY/MM')
 ORDER BY 1, 2;


-- ============================================================
-- QUERY 3: NRO. DE CLIENTES POR ASESOR / MES  (NUEVO)
-- Misma base que Query 2 (mismo universo de productos)
-- Gráfico: Barras agrupadas NRO CLIENTES por ASESOR x MES
-- ============================================================

SELECT T.DESCRIPCION                    ASESOR,
       TO_CHAR(C.FECHA, 'YYYY/MM')     MES,
       COUNT(DISTINCT C.COD_CLIENTE)    NRO_CLIENTES
  FROM DOCUVENT         C,
       ITEMDOCU         B,
       ARTICUL          A,
       TABLAS_AUXILIARES T,
       EQUIVALENCIA     E
 WHERE C.TIPODOC = B.TIPODOC
   AND C.SERIE = B.SERIE
   AND C.NUMERO = B.NUMERO
   AND C.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
   AND C.ESTADO <> '9'
   AND A.TP_ART IN ('T', 'S')
   AND A.COD_ART = B.COD_ART
   AND T.TIPO = 29
   AND T.CODIGO = C.COD_VENDE
   AND E.UNIDAD = 'KG'
   AND E.COD_ART = A.COD_ART
 GROUP BY T.DESCRIPCION,
          TO_CHAR(C.FECHA, 'YYYY/MM')
 ORDER BY 1, 2;


-- ============================================================
-- QUERY 3.1: DETALLE DE CLIENTES POR ASESOR / MES (ANEXO)
-- Razón social de cada cliente por asesor y mes
-- Drill-down del Query 3 para verificar qué clientes componen
-- el conteo de cada asesor
-- Incluye CANTIDAD_KG e IMPORTE, ordenado de mayor a menor
-- Tabla maestra: V_DOCUVEN (misma base que Q1.1)
--   → Garantiza mismo universo de clientes e IMPORTE que Q1.1
-- CANTIDAD_KG: LEFT JOIN al universo textil/KG (TP_ART T,S)
--   → 0 para clientes sin productos textiles
-- Parámetros adicionales: :P_ASESOR, :P_MES
-- ============================================================

SELECT V.ASESOR,
       V.MES,
       V.COD_CLIENTE,
       X.RUC,
       X.NOMBRE                              RAZON_SOCIAL,
       NVL(K.CANTIDAD_KG, 0)                 CANTIDAD_KG,
       (V.MONTO - NVL(I.MONTO, 0))           IMPORTE
  FROM (SELECT A.VENDEDOR,
               T.DESCRIPCION                   ASESOR,
               TO_CHAR(A.FECHA, 'YYYY/MM')     MES,
               A.COD_CLIENTE,
               SUM(DECODE(:P_MON,
                          'S', SOLES_SINANT,
                               DOLARES_SINANT)) MONTO
          FROM V_DOCUVEN A, TABLAS_AUXILIARES T
         WHERE A.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
           AND T.TIPO(+)   = 29
           AND T.CODIGO(+) = A.VENDEDOR
           AND T.DESCRIPCION = :P_ASESOR
           AND TO_CHAR(A.FECHA, 'YYYY/MM') = :P_MES
         GROUP BY A.VENDEDOR,
                  T.DESCRIPCION,
                  TO_CHAR(A.FECHA, 'YYYY/MM'),
                  A.COD_CLIENTE) V,
       (SELECT D.COD_VENDE                     VENDEDOR,
               TO_CHAR(D.FECHA, 'YYYY/MM')     MES,
               D.COD_CLIENTE,
               SUM(DECODE(:P_MON,
                          'S',
                          DECODE(D.MONEDA,
                                 'S', I.IMP_VVTA,
                                 ROUND(I.IMP_VVTA * D.IMPORT_CAM, 2)),
                          DECODE(D.MONEDA,
                                 'D', I.IMP_VVTA,
                                 ROUND(I.IMP_VVTA / D.IMPORT_CAM, 2)))) MONTO
          FROM DOCUVENT D, ITEMDOCU I
         WHERE D.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
           AND D.ESTADO <> '9'
           AND I.TIPODOC = D.TIPODOC
           AND I.SERIE   = D.SERIE
           AND I.NUMERO  = D.NUMERO
           AND I.COD_ART IN ('9300049997', '9300049999',
                             '930004999A', '9300049998')
         GROUP BY D.COD_VENDE,
                  TO_CHAR(D.FECHA, 'YYYY/MM'),
                  D.COD_CLIENTE) I,
       (SELECT C.COD_VENDE                   VENDEDOR,
               TO_CHAR(C.FECHA, 'YYYY/MM')   MES,
               C.COD_CLIENTE,
               SUM(B.CANTIDAD * E.FACTOR)    CANTIDAD_KG
          FROM DOCUVENT         C,
               ITEMDOCU         B,
               ARTICUL          A,
               EQUIVALENCIA     E
         WHERE C.TIPODOC = B.TIPODOC
           AND C.SERIE   = B.SERIE
           AND C.NUMERO  = B.NUMERO
           AND C.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
           AND C.ESTADO <> '9'
           AND A.TP_ART IN ('T', 'S')
           AND A.COD_ART = B.COD_ART
           AND E.UNIDAD  = 'KG'
           AND E.COD_ART = A.COD_ART
         GROUP BY C.COD_VENDE,
                  TO_CHAR(C.FECHA, 'YYYY/MM'),
                  C.COD_CLIENTE) K,
       CLIENTES X
 WHERE I.VENDEDOR(+)    = V.VENDEDOR
   AND I.COD_CLIENTE(+) = V.COD_CLIENTE
   AND I.MES(+)         = V.MES
   AND K.VENDEDOR(+)    = V.VENDEDOR
   AND K.COD_CLIENTE(+) = V.COD_CLIENTE
   AND K.MES(+)         = V.MES
   AND X.COD_CLIENTE    = V.COD_CLIENTE
 ORDER BY IMPORTE DESC, CANTIDAD_KG DESC;
