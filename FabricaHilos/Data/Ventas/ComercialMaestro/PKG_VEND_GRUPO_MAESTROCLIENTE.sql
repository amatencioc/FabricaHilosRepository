-- ============================================================
-- PAQUETE : PKG_VEND_GRUPO_MAESTROCLIENTE
-- NOMBRE  : VENDEDOR_GRUPO_MAESTROCLIENTE
-- VERSION : 2.0
-- FECHA   : 05/05/2026
-- AUTOR   : SIG - VICTOR MATENCIO
-- ------------------------------------------------------------
-- DESCRIPCION:
--   Reporte de ventas por vendedor agrupado por mes.
--   Versión 2 — incorpora los siguientes cambios respecto
--   al query original (VendedorMes):
--
--   1. VENDEDOR -> MAESTRO DE CLIENTES (C.VENDEDOR)
--      El vendedor ya no se toma del campo COD_VENDE del
--      documento (DOCUVENT.COD_VENDE / V_DOCUVEN.VENDEDOR)
--      sino del campo VENDEDOR del maestro de clientes
--      (CLIENTES.VENDEDOR). Esto asegura que cada venta
--      quede atribuida al asesor responsable del cliente,
--      independientemente de quién emitió el documento.
--
--   2. TABLAS_AUXILIARES (T)
--      El join T.CODIGO apunta a C.VENDEDOR (maestro)
--      en los 3 subqueries (A, B, TU), no a COD_VENDE.
--
--   3. FUENTE DE MONTOS -> V_DOCUVEN (SOLES_SINANT / DOLARES_SINANT)
--      Se mantiene la vista V_DOCUVEN como fuente de montos
--      (monto neto real, excluye gratitudes TGRAT='S',
--      descuenta fletes/seguros vía subquery B).
--
--   4. GRUPO_REL (CLIENTE_RELACION)
--      Join a CLIENTE_RELACION incluido en los 3 subqueries
--      (A, B, TU) de ambos procedures.
--      En DETALLE: COD_CLIENTE = DECODE(C.GRUPO_REL, NULL,
--        doc.COD_CLIENTE, MIN_CLIENTE_del_grupo) — consolida
--        clientes relacionados bajo un único representante.
--      En CABECERA: join presente; no altera la agregación
--        por DESCRIPCION/FECHA (sin cliente individual).
--
-- ------------------------------------------------------------
-- PROCEDIMIENTOS PUBLICOS:
--   SP_REPORTE(P_FECHA1, P_FECHA2, P_MON, P_TIPO, P_CURSOR)
--     P_MON  : 'S' = Soles  |  'D' = Dolares
--     P_TIPO : 'C' = Cabecera (resumen por vendedor/periodo/giro)
--              'D' = Detalle  (por cliente dentro de vendedor)
-- ============================================================
-- ESPECIFICACION
-- ============================================================
CREATE OR REPLACE PACKAGE PKG_VEND_GRUPO_MAESTROCLIENTE AS

  TYPE T_CURSOR IS REF CURSOR;

  -- P_TIPO: 'C' = Cabecera (resumen por vendedor)
  --         'D' = Detalle  (por cliente)
  PROCEDURE SP_REPORTE(
    P_FECHA1  IN DATE,
    P_FECHA2  IN DATE,
    P_MON     IN VARCHAR2,   -- 'S' = Soles  |  'D' = Dolares
    P_TIPO    IN CHAR,       -- 'C' = Cabecera | 'D' = Detalle
    P_CURSOR  OUT T_CURSOR
  );

  -- TOP N familias de hilados por importe facturado
  PROCEDURE SP_TOP_HILADOS_IMPORTE(
    P_FECHA1  IN DATE,
    P_FECHA2  IN DATE,
    P_MON     IN VARCHAR2,   -- 'S' = Soles  |  'D' = Dolares
    P_TOP     IN NUMBER,     -- cantidad de registros (ej. 5)
    P_CURSOR  OUT T_CURSOR
  );

  -- Ventas netas por giro de cliente
  PROCEDURE SP_VENTAS_POR_GIRO(
    P_FECHA1  IN DATE,
    P_FECHA2  IN DATE,
    P_MON     IN VARCHAR2,   -- 'S' = Soles  |  'D' = Dolares
    P_CURSOR  OUT T_CURSOR
  );

  -- TOP N articulos por kilogramos
  PROCEDURE SP_TOP_HILADOS_KG(
    P_FECHA1  IN DATE,
    P_FECHA2  IN DATE,
    P_TOP     IN NUMBER,     -- cantidad de registros (ej. 5)
    P_CURSOR  OUT T_CURSOR
  );

  -- Ventas netas por mercado (Peru / LATAM / Europa / Asia / Oceania / Otros)
  PROCEDURE SP_VENTAS_POR_MERCADO(
    P_FECHA1  IN DATE,
    P_FECHA2  IN DATE,
    P_MON     IN VARCHAR2,   -- 'S' = Soles  |  'D' = Dolares
    P_CURSOR  OUT T_CURSOR
  );

  -- Detalle por pais dentro de un mercado
  PROCEDURE SP_DETALLE_POR_PAIS(
    P_FECHA1   IN DATE,
    P_FECHA2   IN DATE,
    P_MON      IN VARCHAR2,   -- 'S' = Soles  |  'D' = Dolares
    P_MERCADO  IN VARCHAR2,   -- NULL=todos | 'Global'=Europa+Asia+Oceania+Otros
    P_CURSOR   OUT T_CURSOR
  );

  -- Ventas por departamento (solo Peru)
  PROCEDURE SP_DETALLE_POR_DPTO(
    P_FECHA1  IN DATE,
    P_FECHA2  IN DATE,
    P_MON     IN VARCHAR2,
    P_CURSOR  OUT T_CURSOR
  );

  -- Ventas por distrito dentro de un departamento (Peru)
  PROCEDURE SP_DETALLE_POR_DISTRITO(
    P_FECHA1  IN DATE,
    P_FECHA2  IN DATE,
    P_MON     IN VARCHAR2,
    P_DPTO    IN VARCHAR2,
    P_CURSOR  OUT T_CURSOR
  );

  -- Ciudades de un pais extranjero
  PROCEDURE SP_CIUDADES_POR_PAIS(
    P_FECHA1  IN DATE,
    P_FECHA2  IN DATE,
    P_MON     IN VARCHAR2,
    P_PAIS    IN VARCHAR2,
    P_CURSOR  OUT T_CURSOR
  );

  -- Evolucion mensual de ventas por mercado
  PROCEDURE SP_EVOLUCION_MENSUAL(
    P_FECHA1  IN DATE,
    P_FECHA2  IN DATE,
    P_MON     IN VARCHAR2,
    P_CURSOR  OUT T_CURSOR
  );

  -- Catalogo de paises ISO (TABLAS_AUXILIARES TIPO=25)
  PROCEDURE SP_PAISES_ISO(
    P_CURSOR  OUT T_CURSOR
  );

  -- Kilogramos facturados por mes
  PROCEDURE SP_KG_MENSUAL(
    P_FECHA1  IN DATE,
    P_FECHA2  IN DATE,
    P_CURSOR  OUT T_CURSOR
  );

END PKG_VEND_GRUPO_MAESTROCLIENTE;
/

-- ============================================================
-- CUERPO
-- ============================================================
CREATE OR REPLACE PACKAGE BODY PKG_VEND_GRUPO_MAESTROCLIENTE AS

  -- ----------------------------------------------------------
  -- CABECERA: resumen por vendedor / periodo / giro
  -- ----------------------------------------------------------
  PROCEDURE SP_CABECERA(
    P_FECHA1  IN DATE,
    P_FECHA2  IN DATE,
    P_MON     IN VARCHAR2,
    P_CURSOR  OUT T_CURSOR
  ) IS
  BEGIN
    OPEN P_CURSOR FOR
      SELECT A.DESCRIPCION,
             A.FECHA                           PERIODO,
             SUM(TU.TOTUNID)                   TOTUNID,
             SUM((A.MONTO - NVL(B.MONTO, 0))) MONTO
        FROM (SELECT NVL(T.INDICADOR2, C.VENDEDOR)       GRUPO,
                     C.VENDEDOR                          COD_VENDE,
                     T.DESCRIPCION,
                     TO_CHAR(V.FECHA, 'YYYY/MM')         FECHA,
                     C.GIRO,
                     SUM(DECODE(P_MON,
                                'S', V.SOLES_SINANT,
                                     V.DOLARES_SINANT))  MONTO
                FROM V_DOCUVEN V
                   , CLIENTES C
                   , TABLAS_AUXILIARES T
                   , (SELECT GRUPO, MIN(COD_CLIENTE) MIN_CLIENTE
                        FROM CLIENTE_RELACION
                       GROUP BY GRUPO) GRP
               WHERE V.FECHA   BETWEEN P_FECHA1 AND P_FECHA2
                 AND C.COD_CLIENTE = V.COD_CLIENTE
                 AND T.TIPO(+)     = 29
                 AND T.CODIGO(+)   = C.VENDEDOR
                 AND GRP.GRUPO(+)  = C.GRUPO_REL
               GROUP BY NVL(T.INDICADOR2, C.VENDEDOR),
                        C.VENDEDOR,
                        T.DESCRIPCION,
                        TO_CHAR(V.FECHA, 'YYYY/MM'),
                        C.GIRO) A,
             (SELECT NVL(T.INDICADOR2, C.VENDEDOR) GRUPO,
                     C.VENDEDOR                 COD_VENDE,
                     TO_CHAR(D.FECHA, 'YYYY/MM') FECHA,
                     C.GIRO,
                     SUM(DECODE(P_MON,
                                'S', DECODE(D.MONEDA, 'S', I.IMP_VVTA, ROUND(I.IMP_VVTA * D.IMPORT_CAM, 2)),
                                     DECODE(D.MONEDA, 'D', I.IMP_VVTA, ROUND(I.IMP_VVTA / D.IMPORT_CAM, 2)))) MONTO
                FROM DOCUVENT D
                   , ITEMDOCU I
                   , CLIENTES C
                   , TABLAS_AUXILIARES T
                   , (SELECT GRUPO, MIN(COD_CLIENTE) MIN_CLIENTE
                        FROM CLIENTE_RELACION
                       GROUP BY GRUPO) GRP
               WHERE D.FECHA   BETWEEN P_FECHA1 AND P_FECHA2
                 AND D.ESTADO  <> '9'
                 AND I.TIPODOC  = D.TIPODOC
                 AND I.SERIE    = D.SERIE
                 AND I.NUMERO   = D.NUMERO
                 AND I.COD_ART IN ('9300049997','9300049999','930004999A','9300049998')
                 AND C.COD_CLIENTE = D.COD_CLIENTE
                 AND T.TIPO(+)    = 29
                 AND T.CODIGO(+)  = C.VENDEDOR
                 AND GRP.GRUPO(+) = C.GRUPO_REL
               GROUP BY NVL(T.INDICADOR2, C.VENDEDOR),
                        C.VENDEDOR,
                        TO_CHAR(D.FECHA, 'YYYY/MM'),
                        C.GIRO) B,
             (SELECT NVL(T.INDICADOR2, C.VENDEDOR)       GRUPO,
                     C.VENDEDOR                          COD_VENDE,
                     TO_CHAR(D.FECHA, 'YYYY/MM')         FECHA,
                     C.GIRO,
                     SUM(I.CANTIDAD * E.FACTOR)           TOTUNID
                FROM DOCUVENT D
                   , ITEMDOCU I
                   , EQUIVALENCIA E
                   , CLIENTES C
                   , ARTICUL M
                   , TABLAS_AUXILIARES T
                   , (SELECT GRUPO, MIN(COD_CLIENTE) MIN_CLIENTE
                        FROM CLIENTE_RELACION
                       GROUP BY GRUPO) GRP
               WHERE D.FECHA    BETWEEN P_FECHA1 AND P_FECHA2
                 AND NVL(D.ESTADO, '0') <> 9
                 AND D.ORIGEN    <> 'A'
                 AND I.TIPODOC    = D.TIPODOC
                 AND I.SERIE      = D.SERIE
                 AND I.NUMERO     = D.NUMERO
                 AND M.TP_ART    IN ('T', 'S')
                 AND M.COD_ART    = I.COD_ART
                 AND E.COD_ART(+) = I.COD_ART
                 AND E.UNIDAD(+)  = 'KG'
                 AND C.COD_CLIENTE = D.COD_CLIENTE
                 AND T.TIPO(+)    = 29
                 AND T.CODIGO(+)  = C.VENDEDOR
                 AND GRP.GRUPO(+) = C.GRUPO_REL
               GROUP BY NVL(T.INDICADOR2, C.VENDEDOR),
                        C.VENDEDOR,
                        TO_CHAR(D.FECHA, 'YYYY/MM'),
                        C.GIRO) TU
       WHERE B.GRUPO(+)      = A.GRUPO
         AND B.COD_VENDE(+)  = A.COD_VENDE
         AND B.FECHA(+)      = A.FECHA
         AND B.GIRO(+)       = A.GIRO
         AND TU.GRUPO(+)     = A.GRUPO
         AND TU.COD_VENDE(+) = A.COD_VENDE
         AND TU.FECHA(+)     = A.FECHA
         AND TU.GIRO(+)      = A.GIRO
         AND A.COD_VENDE       <> '99'
       GROUP BY A.DESCRIPCION, A.FECHA
       ORDER BY A.DESCRIPCION;
  END SP_CABECERA;

  -- ----------------------------------------------------------
  -- DETALLE: por cliente dentro de cada vendedor
  -- ----------------------------------------------------------
  PROCEDURE SP_DETALLE(
    P_FECHA1  IN DATE,
    P_FECHA2  IN DATE,
    P_MON     IN VARCHAR2,
    P_CURSOR  OUT T_CURSOR
  ) IS
  BEGIN
    OPEN P_CURSOR FOR
      SELECT A.GRUPO,
             A.COD_VENDE     VENDEDOR,
             A.DESCRIPCION,
             A.COD_CLIENTE,
             A.NOMBRE,
             A.RUC,
             A.FECHA         PERIODO,
             A.DESC_GIRO     GIRO,
             TU.TOTUNID,
             (A.MONTO - NVL(B.MONTO, 0)) MONTO
        FROM (SELECT NVL(T.INDICADOR2, C.VENDEDOR)       GRUPO,
                     C.VENDEDOR                          COD_VENDE,
                     T.DESCRIPCION,
                     DECODE(C.GRUPO_REL, NULL, V.COD_CLIENTE,
                            GRP.MIN_CLIENTE)              COD_CLIENTE,
                     C.NOMBRE,
                     C.RUC,
                     TO_CHAR(V.FECHA, 'YYYY/MM')         FECHA,
                     C.GIRO,
                     NVL(T2.DESCRIPCION, C.GIRO)         DESC_GIRO,
                     SUM(DECODE(P_MON,
                                'S', V.SOLES_SINANT,
                                     V.DOLARES_SINANT))  MONTO
                FROM V_DOCUVEN V
                   , CLIENTES C
                   , TABLAS_AUXILIARES T
                   , TABLAS_AUXILIARES T2
                   , (SELECT GRUPO, MIN(COD_CLIENTE) MIN_CLIENTE
                        FROM CLIENTE_RELACION
                       GROUP BY GRUPO) GRP
               WHERE V.FECHA   BETWEEN P_FECHA1 AND P_FECHA2
                 AND C.COD_CLIENTE = V.COD_CLIENTE
                 AND T.TIPO(+)     = 29
                 AND T.CODIGO(+)   = C.VENDEDOR
                 AND T2.TIPO(+)    = 27
                 AND T2.CODIGO(+)  = C.GIRO
                 AND GRP.GRUPO(+)  = C.GRUPO_REL
               GROUP BY NVL(T.INDICADOR2, C.VENDEDOR),
                        C.VENDEDOR,
                        T.DESCRIPCION,
                        DECODE(C.GRUPO_REL, NULL, V.COD_CLIENTE,
                               GRP.MIN_CLIENTE),
                        C.NOMBRE,
                        C.RUC,
                        TO_CHAR(V.FECHA, 'YYYY/MM'),
                        C.GIRO,
                        NVL(T2.DESCRIPCION, C.GIRO)) A,
             (SELECT NVL(T.INDICADOR2, C.VENDEDOR) GRUPO,
                     C.VENDEDOR                 COD_VENDE,
                     DECODE(C.GRUPO_REL, NULL, D.COD_CLIENTE,
                            GRP.MIN_CLIENTE)              COD_CLIENTE,
                     TO_CHAR(D.FECHA, 'YYYY/MM') FECHA,
                     C.GIRO,
                     SUM(DECODE(P_MON,
                                'S', DECODE(D.MONEDA, 'S', I.IMP_VVTA, ROUND(I.IMP_VVTA * D.IMPORT_CAM, 2)),
                                     DECODE(D.MONEDA, 'D', I.IMP_VVTA, ROUND(I.IMP_VVTA / D.IMPORT_CAM, 2)))) MONTO
                FROM DOCUVENT D
                   , ITEMDOCU I
                   , CLIENTES C
                   , TABLAS_AUXILIARES T
                   , (SELECT GRUPO, MIN(COD_CLIENTE) MIN_CLIENTE
                        FROM CLIENTE_RELACION
                       GROUP BY GRUPO) GRP
               WHERE D.FECHA   BETWEEN P_FECHA1 AND P_FECHA2
                 AND D.ESTADO  <> '9'
                 AND I.TIPODOC  = D.TIPODOC
                 AND I.SERIE    = D.SERIE
                 AND I.NUMERO   = D.NUMERO
                 AND I.COD_ART IN ('9300049997','9300049999','930004999A','9300049998')
                 AND C.COD_CLIENTE = D.COD_CLIENTE
                 AND T.TIPO(+)    = 29
                 AND T.CODIGO(+)  = C.VENDEDOR
                 AND GRP.GRUPO(+) = C.GRUPO_REL
               GROUP BY NVL(T.INDICADOR2, C.VENDEDOR),
                        C.VENDEDOR,
                        DECODE(C.GRUPO_REL, NULL, D.COD_CLIENTE,
                               GRP.MIN_CLIENTE),
                        TO_CHAR(D.FECHA, 'YYYY/MM'),
                        C.GIRO) B,
             (SELECT NVL(T.INDICADOR2, C.VENDEDOR)       GRUPO,
                     C.VENDEDOR                          COD_VENDE,
                     DECODE(C.GRUPO_REL, NULL, D.COD_CLIENTE,
                            GRP.MIN_CLIENTE)              COD_CLIENTE,
                     TO_CHAR(D.FECHA, 'YYYY/MM')         FECHA,
                     C.GIRO,
                     SUM(I.CANTIDAD * E.FACTOR)           TOTUNID
                FROM DOCUVENT D
                   , ITEMDOCU I
                   , EQUIVALENCIA E
                   , CLIENTES C
                   , ARTICUL M
                   , TABLAS_AUXILIARES T
                   , (SELECT GRUPO, MIN(COD_CLIENTE) MIN_CLIENTE
                        FROM CLIENTE_RELACION
                       GROUP BY GRUPO) GRP
               WHERE D.FECHA    BETWEEN P_FECHA1 AND P_FECHA2
                 AND NVL(D.ESTADO, '0') <> 9
                 AND D.ORIGEN    <> 'A'
                 AND I.TIPODOC    = D.TIPODOC
                 AND I.SERIE      = D.SERIE
                 AND I.NUMERO     = D.NUMERO
                 AND M.TP_ART    IN ('T', 'S')
                 AND M.COD_ART    = I.COD_ART
                 AND E.COD_ART(+) = I.COD_ART
                 AND E.UNIDAD(+)  = 'KG'
                 AND C.COD_CLIENTE = D.COD_CLIENTE
                 AND T.TIPO(+)    = 29
                 AND T.CODIGO(+)  = C.VENDEDOR
                 AND GRP.GRUPO(+) = C.GRUPO_REL
               GROUP BY NVL(T.INDICADOR2, C.VENDEDOR),
                        C.VENDEDOR,
                        DECODE(C.GRUPO_REL, NULL, D.COD_CLIENTE,
                               GRP.MIN_CLIENTE),
                        TO_CHAR(D.FECHA, 'YYYY/MM'),
                        C.GIRO) TU
       WHERE B.GRUPO(+)       = A.GRUPO
         AND B.COD_VENDE(+)   = A.COD_VENDE
         AND B.COD_CLIENTE(+) = A.COD_CLIENTE
         AND B.FECHA(+)       = A.FECHA
         AND B.GIRO(+)        = A.GIRO
         AND TU.GRUPO(+)      = A.GRUPO
         AND TU.COD_VENDE(+)  = A.COD_VENDE
         AND TU.COD_CLIENTE(+)= A.COD_CLIENTE
         AND TU.FECHA(+)      = A.FECHA
         AND TU.GIRO(+)       = A.GIRO
         AND A.COD_VENDE        <> '99'
       ORDER BY A.DESCRIPCION, A.NOMBRE;
  END SP_DETALLE;

  -- ----------------------------------------------------------
  -- TOP N familias de hilados por importe facturado
  -- CLIENTES/GRP no necesarios: GROUP BY es solo por FAMILIA
  -- ----------------------------------------------------------
  PROCEDURE SP_TOP_HILADOS_IMPORTE(
    P_FECHA1  IN DATE,
    P_FECHA2  IN DATE,
    P_MON     IN VARCHAR2,
    P_TOP     IN NUMBER,
    P_CURSOR  OUT T_CURSOR
  ) IS
  BEGIN
    OPEN P_CURSOR FOR
      SELECT FAMILIA, IMPORTE
        FROM (
          SELECT NVL(F.DESCRIPCION, 'SIN FAMILIA') FAMILIA,
                 SUM(DECODE(P_MON,
                       'S', DECODE(D.MONEDA, 'S', I.IMP_VVTA,
                                   ROUND(I.IMP_VVTA * D.IMPORT_CAM, 2)),
                            DECODE(D.MONEDA, 'D', I.IMP_VVTA,
                                   ROUND(I.IMP_VVTA / NULLIF(D.IMPORT_CAM, 0), 2)))) IMPORTE
            FROM DOCUVENT D
               , ITEMDOCU I
               , ARTICUL M
               , TFAMLIN F
           WHERE D.FECHA   BETWEEN P_FECHA1 AND P_FECHA2
             AND D.ESTADO  <> '9'
             AND I.TIPODOC  = D.TIPODOC
             AND I.SERIE    = D.SERIE
             AND I.NUMERO   = D.NUMERO
             AND M.COD_ART(+) = I.COD_ART
             AND F.COD_FAM(+) = M.COD_FAM
             AND F.COD_LIN(+) = M.COD_LIN
             AND I.COD_ART NOT IN ('9300049997','9300049999','930004999A','9300049998')
           GROUP BY NVL(F.DESCRIPCION, 'SIN FAMILIA')
           ORDER BY IMPORTE DESC
        )
       WHERE ROWNUM <= P_TOP;
  END SP_TOP_HILADOS_IMPORTE;

  -- ----------------------------------------------------------
  -- Ventas netas por giro de cliente
  -- ANSI JOIN: Oracle 10g no admite (+) en cadena multiple
  -- Excluye asesor OFICINA; HAVING > 0
  -- ----------------------------------------------------------
  PROCEDURE SP_VENTAS_POR_GIRO(
    P_FECHA1  IN DATE,
    P_FECHA2  IN DATE,
    P_MON     IN VARCHAR2,
    P_CURSOR  OUT T_CURSOR
  ) IS
  BEGIN
    OPEN P_CURSOR FOR
      SELECT CODIGO_GIRO,
             NVL(DESC_GIRO, 'SIN GIRO') DESC_GIRO,
             SUM(IMPORTE_CLI)            IMPORTE
        FROM (
          SELECT C.GIRO          CODIGO_GIRO,
                 T2.ABREVIADA    DESC_GIRO,
                 ( SUM(DECODE(P_MON, 'S', V.SOLES_SINANT, V.DOLARES_SINANT))
                   - NVL(MAX(B.IMP_ANT), 0) ) IMPORTE_CLI
            FROM V_DOCUVEN V
            LEFT JOIN CLIENTES C
                   ON C.COD_CLIENTE  = V.COD_CLIENTE
            LEFT JOIN TABLAS_AUXILIARES T
                   ON T.CODIGO = C.VENDEDOR AND T.TIPO = 29
            LEFT JOIN TABLAS_AUXILIARES T2
                   ON T2.CODIGO = C.GIRO    AND T2.TIPO = 27
            LEFT JOIN (SELECT GRUPO, MIN(COD_CLIENTE) MIN_CLIENTE
                         FROM CLIENTE_RELACION
                        GROUP BY GRUPO) GRP
                   ON GRP.GRUPO = C.GRUPO_REL
            LEFT JOIN (SELECT DECODE(C2.GRUPO_REL, NULL, D.COD_CLIENTE,
                                     GRP2.MIN_CLIENTE) COD_CLIENTE,
                              C2.VENDEDOR              COD_ASESOR,
                              SUM(DECODE(P_MON,
                                    'S', DECODE(D.MONEDA, 'S', I.IMP_VVTA,
                                                ROUND(I.IMP_VVTA * D.IMPORT_CAM, 2)),
                                         DECODE(D.MONEDA, 'D', I.IMP_VVTA,
                                                ROUND(I.IMP_VVTA / NULLIF(D.IMPORT_CAM, 0), 2)))) IMP_ANT
                         FROM DOCUVENT D
                         JOIN ITEMDOCU I
                           ON I.TIPODOC = D.TIPODOC
                          AND I.SERIE   = D.SERIE
                          AND I.NUMERO  = D.NUMERO
                         LEFT JOIN CLIENTES C2
                           ON C2.COD_CLIENTE = D.COD_CLIENTE
                         LEFT JOIN (SELECT GRUPO, MIN(COD_CLIENTE) MIN_CLIENTE
                                      FROM CLIENTE_RELACION
                                     GROUP BY GRUPO) GRP2
                           ON GRP2.GRUPO = C2.GRUPO_REL
                        WHERE D.FECHA  BETWEEN P_FECHA1 AND P_FECHA2
                          AND D.ESTADO <> '9'
                          AND I.COD_ART IN ('9300049997','9300049999',
                                            '930004999A','9300049998')
                        GROUP BY DECODE(C2.GRUPO_REL, NULL, D.COD_CLIENTE,
                                        GRP2.MIN_CLIENTE),
                                 C2.VENDEDOR) B
                   ON B.COD_CLIENTE = DECODE(C.GRUPO_REL, NULL,
                                             V.COD_CLIENTE, GRP.MIN_CLIENTE)
                  AND B.COD_ASESOR  = C.VENDEDOR
           WHERE V.FECHA BETWEEN P_FECHA1 AND P_FECHA2
             AND UPPER(NVL(T.DESCRIPCION, '')) <> 'OFICINA'
           GROUP BY DECODE(C.GRUPO_REL, NULL, V.COD_CLIENTE, GRP.MIN_CLIENTE),
                    C.GIRO,
                    T2.ABREVIADA,
                    C.VENDEDOR
          HAVING ( SUM(DECODE(P_MON, 'S', V.SOLES_SINANT, V.DOLARES_SINANT))
                   - NVL(MAX(B.IMP_ANT), 0) ) > 0
        )
       GROUP BY CODIGO_GIRO, DESC_GIRO
       ORDER BY IMPORTE DESC;
  END SP_VENTAS_POR_GIRO;

  -- ----------------------------------------------------------
  -- TOP N articulos por kilogramos
  -- CLIENTES/GRP no necesarios: GROUP BY es solo por ARTICULO
  --
  -- NOTA: se usa DOCUVENT directamente (no V_DOCUVEN) porque la
  -- vista consolida montos a nivel de comprobante y NO expone
  -- ITEMDOCU.CANTIDAD por articulo, necesaria para calcular KG.
  -- Para mantener consistencia con los importes de V_DOCUVEN se
  -- replican los mismos filtros de exclusion que aplica la vista
  -- internamente en SOLES_SINANT / DOLARES_SINANT:
  --   D.ORIGEN <> 'A'        -> excluye abonos
  --   NVL(D.TGRAT,'N') <> 'S' -> excluye gratitudes
  -- ----------------------------------------------------------
  PROCEDURE SP_TOP_HILADOS_KG(
    P_FECHA1  IN DATE,
    P_FECHA2  IN DATE,
    P_TOP     IN NUMBER,
    P_CURSOR  OUT T_CURSOR
  ) IS
  BEGIN
    OPEN P_CURSOR FOR
      SELECT FAMILIA, KILOS
        FROM (
          SELECT NVL(M.DESCRIPCION, 'SIN ARTICULO') FAMILIA,
                 NVL(SUM(I.CANTIDAD * E.FACTOR), 0)  KILOS
            FROM DOCUVENT D
               , ITEMDOCU I
               , EQUIVALENCIA E
               , ARTICUL M
           WHERE D.FECHA    BETWEEN P_FECHA1 AND P_FECHA2
             AND D.ESTADO   <> '9'
             AND D.ORIGEN   <> 'A'
             AND NVL(D.TGRAT,'N') <> 'S'
             AND I.TIPODOC   = D.TIPODOC
             AND I.SERIE     = D.SERIE
             AND I.NUMERO    = D.NUMERO
             AND E.COD_ART(+) = I.COD_ART
             AND E.UNIDAD(+)  = 'KG'
             AND M.COD_ART(+) = I.COD_ART
             AND I.COD_ART NOT IN ('9300049997','9300049999','930004999A','9300049998')
           GROUP BY NVL(M.DESCRIPCION, 'SIN ARTICULO')
          HAVING NVL(SUM(I.CANTIDAD * E.FACTOR), 0) > 0
           ORDER BY KILOS DESC
        )
       WHERE ROWNUM <= P_TOP;
  END SP_TOP_HILADOS_KG;

  -- ----------------------------------------------------------
  -- PUNTO DE ENTRADA PRINCIPAL
  -- ----------------------------------------------------------
  PROCEDURE SP_REPORTE(
    P_FECHA1  IN DATE,
    P_FECHA2  IN DATE,
    P_MON     IN VARCHAR2,
    P_TIPO    IN CHAR,
    P_CURSOR  OUT T_CURSOR
  ) IS
  BEGIN
    IF P_TIPO = 'D' THEN
      SP_DETALLE(P_FECHA1, P_FECHA2, P_MON, P_CURSOR);
    ELSE
      SP_CABECERA(P_FECHA1, P_FECHA2, P_MON, P_CURSOR);
    END IF;
  END SP_REPORTE;

  -- ----------------------------------------------------------
  -- Ventas netas por mercado
  -- ----------------------------------------------------------
  PROCEDURE SP_VENTAS_POR_MERCADO(
    P_FECHA1  IN DATE,
    P_FECHA2  IN DATE,
    P_MON     IN VARCHAR2,
    P_CURSOR  OUT T_CURSOR
  ) IS
  BEGIN
    OPEN P_CURSOR FOR
      SELECT MERCADO, SUM(IMPORTE_NETO) IMPORTE
        FROM (
          SELECT CASE
                   WHEN C.PAIS = '01'                     THEN 'Perú'
                   WHEN NVL(TA.INDICADOR1,'X') = 'L' THEN 'LATAM'
                   WHEN NVL(TA.INDICADOR1,'X') = 'E' THEN 'Europa'
                   WHEN NVL(TA.INDICADOR1,'X') = 'A' THEN 'Asia'
                   WHEN NVL(TA.INDICADOR1,'X') = 'O' THEN 'Oceanía'
                   ELSE 'Otros'
                 END MERCADO,
                 DECODE(C.GRUPO_REL,NULL,V.COD_CLIENTE,GRP.MIN_CLIENTE) COD_CLI_GRP,
                 ( SUM(DECODE(P_MON,'S',V.SOLES_SINANT,V.DOLARES_SINANT))
                   - NVL(MAX(B.IMP_ANT),0) ) IMPORTE_NETO
            FROM V_DOCUVEN V
            JOIN CLIENTES C ON C.COD_CLIENTE = V.COD_CLIENTE
            LEFT JOIN (SELECT GRUPO, MIN(COD_CLIENTE) MIN_CLIENTE
                         FROM CLIENTE_RELACION GROUP BY GRUPO) GRP
                   ON GRP.GRUPO = C.GRUPO_REL
            LEFT JOIN (SELECT CODIGO, MAX(INDICADOR1) INDICADOR1
                         FROM TABLAS_AUXILIARES WHERE TIPO = 25
                        GROUP BY CODIGO) TA
                   ON TA.CODIGO = C.PAIS
            LEFT JOIN (SELECT DECODE(C2.GRUPO_REL,NULL,D.COD_CLIENTE,GRP2.MIN_CLIENTE) COD_CLIENTE,
                              SUM(DECODE(P_MON,
                                    'S',DECODE(D.MONEDA,'S',I.IMP_VVTA,ROUND(I.IMP_VVTA*D.IMPORT_CAM,2)),
                                        DECODE(D.MONEDA,'D',I.IMP_VVTA,ROUND(I.IMP_VVTA/NULLIF(D.IMPORT_CAM,0),2)))) IMP_ANT
                         FROM DOCUVENT D
                         JOIN ITEMDOCU I ON I.TIPODOC=D.TIPODOC AND I.SERIE=D.SERIE AND I.NUMERO=D.NUMERO
                         LEFT JOIN CLIENTES C2 ON C2.COD_CLIENTE=D.COD_CLIENTE
                         LEFT JOIN (SELECT GRUPO, MIN(COD_CLIENTE) MIN_CLIENTE
                                      FROM CLIENTE_RELACION GROUP BY GRUPO) GRP2
                                ON GRP2.GRUPO=C2.GRUPO_REL
                        WHERE D.FECHA BETWEEN P_FECHA1 AND P_FECHA2
                          AND D.ESTADO <> '9'
                          AND I.COD_ART IN ('9300049997','9300049999','930004999A','9300049998')
                        GROUP BY DECODE(C2.GRUPO_REL,NULL,D.COD_CLIENTE,GRP2.MIN_CLIENTE)) B
                   ON B.COD_CLIENTE = DECODE(C.GRUPO_REL,NULL,V.COD_CLIENTE,GRP.MIN_CLIENTE)
           WHERE V.FECHA BETWEEN P_FECHA1 AND P_FECHA2
           GROUP BY CASE
                      WHEN C.PAIS = '01'                     THEN 'Perú'
                      WHEN NVL(TA.INDICADOR1,'X') = 'L' THEN 'LATAM'
                      WHEN NVL(TA.INDICADOR1,'X') = 'E' THEN 'Europa'
                      WHEN NVL(TA.INDICADOR1,'X') = 'A' THEN 'Asia'
                      WHEN NVL(TA.INDICADOR1,'X') = 'O' THEN 'Oceanía'
                      ELSE 'Otros'
                    END,
                    DECODE(C.GRUPO_REL,NULL,V.COD_CLIENTE,GRP.MIN_CLIENTE)
        )
       GROUP BY MERCADO
       ORDER BY DECODE(MERCADO,'Perú',1,'LATAM',2,'Europa',3,'Asia',4,'Oceanía',5,6);
  END SP_VENTAS_POR_MERCADO;

  -- ----------------------------------------------------------
  -- Detalle por pais dentro de un mercado
  -- ----------------------------------------------------------
  PROCEDURE SP_DETALLE_POR_PAIS(
    P_FECHA1   IN DATE,
    P_FECHA2   IN DATE,
    P_MON      IN VARCHAR2,
    P_MERCADO  IN VARCHAR2,
    P_CURSOR   OUT T_CURSOR
  ) IS
  BEGIN
    OPEN P_CURSOR FOR
      SELECT MERCADO, CODIGO_PAIS, PAIS_NOMBRE, SUM(IMPORTE_NETO) IMPORTE
        FROM (
          SELECT CASE
                   WHEN C.PAIS = '01'                     THEN 'Perú'
                   WHEN NVL(TA.INDICADOR1,'X') = 'L' THEN 'LATAM'
                   WHEN NVL(TA.INDICADOR1,'X') = 'E' THEN 'Europa'
                   WHEN NVL(TA.INDICADOR1,'X') = 'A' THEN 'Asia'
                   WHEN NVL(TA.INDICADOR1,'X') = 'O' THEN 'Oceanía'
                   ELSE 'Otros'
                 END MERCADO,
                 C.PAIS                          CODIGO_PAIS,
                 NVL(TA.DESCRIPCION, C.PAIS)     PAIS_NOMBRE,
                 DECODE(C.GRUPO_REL,NULL,V.COD_CLIENTE,GRP.MIN_CLIENTE) COD_CLI_GRP,
                 ( SUM(DECODE(P_MON,'S',V.SOLES_SINANT,V.DOLARES_SINANT))
                   - NVL(MAX(B.IMP_ANT),0) ) IMPORTE_NETO
            FROM V_DOCUVEN V
            JOIN CLIENTES C ON C.COD_CLIENTE = V.COD_CLIENTE
            LEFT JOIN (SELECT GRUPO, MIN(COD_CLIENTE) MIN_CLIENTE
                         FROM CLIENTE_RELACION GROUP BY GRUPO) GRP
                   ON GRP.GRUPO = C.GRUPO_REL
            LEFT JOIN (SELECT CODIGO, MAX(INDICADOR1) INDICADOR1,
                              MAX(DESCRIPCION) DESCRIPCION
                         FROM TABLAS_AUXILIARES WHERE TIPO = 25
                        GROUP BY CODIGO) TA
                   ON TA.CODIGO = C.PAIS
            LEFT JOIN (SELECT DECODE(C2.GRUPO_REL,NULL,D.COD_CLIENTE,GRP2.MIN_CLIENTE) COD_CLIENTE,
                              SUM(DECODE(P_MON,
                                    'S',DECODE(D.MONEDA,'S',I.IMP_VVTA,ROUND(I.IMP_VVTA*D.IMPORT_CAM,2)),
                                        DECODE(D.MONEDA,'D',I.IMP_VVTA,ROUND(I.IMP_VVTA/NULLIF(D.IMPORT_CAM,0),2)))) IMP_ANT
                         FROM DOCUVENT D
                         JOIN ITEMDOCU I ON I.TIPODOC=D.TIPODOC AND I.SERIE=D.SERIE AND I.NUMERO=D.NUMERO
                         LEFT JOIN CLIENTES C2 ON C2.COD_CLIENTE=D.COD_CLIENTE
                         LEFT JOIN (SELECT GRUPO, MIN(COD_CLIENTE) MIN_CLIENTE
                                      FROM CLIENTE_RELACION GROUP BY GRUPO) GRP2
                                ON GRP2.GRUPO=C2.GRUPO_REL
                        WHERE D.FECHA BETWEEN P_FECHA1 AND P_FECHA2
                          AND D.ESTADO <> '9'
                          AND I.COD_ART IN ('9300049997','9300049999','930004999A','9300049998')
                        GROUP BY DECODE(C2.GRUPO_REL,NULL,D.COD_CLIENTE,GRP2.MIN_CLIENTE)) B
                   ON B.COD_CLIENTE = DECODE(C.GRUPO_REL,NULL,V.COD_CLIENTE,GRP.MIN_CLIENTE)
           WHERE V.FECHA BETWEEN P_FECHA1 AND P_FECHA2
           GROUP BY CASE
                      WHEN C.PAIS = '01'                     THEN 'Perú'
                      WHEN NVL(TA.INDICADOR1,'X') = 'L' THEN 'LATAM'
                      WHEN NVL(TA.INDICADOR1,'X') = 'E' THEN 'Europa'
                      WHEN NVL(TA.INDICADOR1,'X') = 'A' THEN 'Asia'
                      WHEN NVL(TA.INDICADOR1,'X') = 'O' THEN 'Oceanía'
                      ELSE 'Otros'
                    END,
                    C.PAIS,
                    NVL(TA.DESCRIPCION, C.PAIS),
                    DECODE(C.GRUPO_REL,NULL,V.COD_CLIENTE,GRP.MIN_CLIENTE)
        )
       WHERE (P_MERCADO IS NULL
           OR MERCADO = P_MERCADO
           OR (P_MERCADO = 'Global' AND MERCADO IN ('Europa','Asia','Oceanía','Otros')))
       GROUP BY MERCADO, CODIGO_PAIS, PAIS_NOMBRE
       ORDER BY IMPORTE DESC;
  END SP_DETALLE_POR_PAIS;

  -- ----------------------------------------------------------
  -- Ventas por departamento (solo Peru)
  -- ----------------------------------------------------------
  PROCEDURE SP_DETALLE_POR_DPTO(
    P_FECHA1  IN DATE,
    P_FECHA2  IN DATE,
    P_MON     IN VARCHAR2,
    P_CURSOR  OUT T_CURSOR
  ) IS
  BEGIN
    OPEN P_CURSOR FOR
      SELECT DEPARTAMENTO, SUM(IMPORTE_NETO) IMPORTE
        FROM (
          SELECT NVL(U.NOM_DPT,'Sin Departamento') DEPARTAMENTO,
                 DECODE(C.GRUPO_REL,NULL,V.COD_CLIENTE,GRP.MIN_CLIENTE) COD_CLI_GRP,
                 ( SUM(DECODE(P_MON,'S',V.SOLES_SINANT,V.DOLARES_SINANT))
                   - NVL(MAX(B.IMP_ANT),0) ) IMPORTE_NETO
            FROM V_DOCUVEN V
            JOIN CLIENTES C ON C.COD_CLIENTE = V.COD_CLIENTE
            LEFT JOIN (SELECT GRUPO, MIN(COD_CLIENTE) MIN_CLIENTE
                         FROM CLIENTE_RELACION GROUP BY GRUPO) GRP
                   ON GRP.GRUPO = C.GRUPO_REL
            LEFT JOIN UBIGEO U ON U.COD_UBC = C.COD_UBC
            LEFT JOIN (SELECT DECODE(C2.GRUPO_REL,NULL,D.COD_CLIENTE,GRP2.MIN_CLIENTE) COD_CLIENTE,
                              SUM(DECODE(P_MON,
                                    'S',DECODE(D.MONEDA,'S',I.IMP_VVTA,ROUND(I.IMP_VVTA*D.IMPORT_CAM,2)),
                                        DECODE(D.MONEDA,'D',I.IMP_VVTA,ROUND(I.IMP_VVTA/NULLIF(D.IMPORT_CAM,0),2)))) IMP_ANT
                         FROM DOCUVENT D
                         JOIN ITEMDOCU I ON I.TIPODOC=D.TIPODOC AND I.SERIE=D.SERIE AND I.NUMERO=D.NUMERO
                         LEFT JOIN CLIENTES C2 ON C2.COD_CLIENTE=D.COD_CLIENTE
                         LEFT JOIN (SELECT GRUPO, MIN(COD_CLIENTE) MIN_CLIENTE
                                      FROM CLIENTE_RELACION GROUP BY GRUPO) GRP2
                                ON GRP2.GRUPO=C2.GRUPO_REL
                        WHERE D.FECHA BETWEEN P_FECHA1 AND P_FECHA2
                          AND D.ESTADO <> '9'
                          AND I.COD_ART IN ('9300049997','9300049999','930004999A','9300049998')
                        GROUP BY DECODE(C2.GRUPO_REL,NULL,D.COD_CLIENTE,GRP2.MIN_CLIENTE)) B
                   ON B.COD_CLIENTE = DECODE(C.GRUPO_REL,NULL,V.COD_CLIENTE,GRP.MIN_CLIENTE)
           WHERE V.FECHA BETWEEN P_FECHA1 AND P_FECHA2
             AND (U.PAIS = '01' OR U.COD_UBC IS NULL)
           GROUP BY NVL(U.NOM_DPT,'Sin Departamento'),
                    DECODE(C.GRUPO_REL,NULL,V.COD_CLIENTE,GRP.MIN_CLIENTE)
        )
       GROUP BY DEPARTAMENTO
       ORDER BY IMPORTE DESC;
  END SP_DETALLE_POR_DPTO;

  -- ----------------------------------------------------------
  -- Ventas por distrito dentro de un departamento (Peru)
  -- ----------------------------------------------------------
  PROCEDURE SP_DETALLE_POR_DISTRITO(
    P_FECHA1  IN DATE,
    P_FECHA2  IN DATE,
    P_MON     IN VARCHAR2,
    P_DPTO    IN VARCHAR2,
    P_CURSOR  OUT T_CURSOR
  ) IS
  BEGIN
    OPEN P_CURSOR FOR
      SELECT DEPARTAMENTO, DISTRITO, SUM(IMPORTE_NETO) IMPORTE
        FROM (
          SELECT NVL(U.NOM_DPT,'Sin Departamento') DEPARTAMENTO,
                 NVL(U.NOM_DTT,'Sin Distrito')      DISTRITO,
                 DECODE(C.GRUPO_REL,NULL,V.COD_CLIENTE,GRP.MIN_CLIENTE) COD_CLI_GRP,
                 ( SUM(DECODE(P_MON,'S',V.SOLES_SINANT,V.DOLARES_SINANT))
                   - NVL(MAX(B.IMP_ANT),0) ) IMPORTE_NETO
            FROM V_DOCUVEN V
            JOIN CLIENTES C ON C.COD_CLIENTE = V.COD_CLIENTE
            LEFT JOIN (SELECT GRUPO, MIN(COD_CLIENTE) MIN_CLIENTE
                         FROM CLIENTE_RELACION GROUP BY GRUPO) GRP
                   ON GRP.GRUPO = C.GRUPO_REL
            LEFT JOIN UBIGEO U ON U.COD_UBC = C.COD_UBC
            LEFT JOIN (SELECT DECODE(C2.GRUPO_REL,NULL,D.COD_CLIENTE,GRP2.MIN_CLIENTE) COD_CLIENTE,
                              SUM(DECODE(P_MON,
                                    'S',DECODE(D.MONEDA,'S',I.IMP_VVTA,ROUND(I.IMP_VVTA*D.IMPORT_CAM,2)),
                                        DECODE(D.MONEDA,'D',I.IMP_VVTA,ROUND(I.IMP_VVTA/NULLIF(D.IMPORT_CAM,0),2)))) IMP_ANT
                         FROM DOCUVENT D
                         JOIN ITEMDOCU I ON I.TIPODOC=D.TIPODOC AND I.SERIE=D.SERIE AND I.NUMERO=D.NUMERO
                         LEFT JOIN CLIENTES C2 ON C2.COD_CLIENTE=D.COD_CLIENTE
                         LEFT JOIN (SELECT GRUPO, MIN(COD_CLIENTE) MIN_CLIENTE
                                      FROM CLIENTE_RELACION GROUP BY GRUPO) GRP2
                                ON GRP2.GRUPO=C2.GRUPO_REL
                        WHERE D.FECHA BETWEEN P_FECHA1 AND P_FECHA2
                          AND D.ESTADO <> '9'
                          AND I.COD_ART IN ('9300049997','9300049999','930004999A','9300049998')
                        GROUP BY DECODE(C2.GRUPO_REL,NULL,D.COD_CLIENTE,GRP2.MIN_CLIENTE)) B
                   ON B.COD_CLIENTE = DECODE(C.GRUPO_REL,NULL,V.COD_CLIENTE,GRP.MIN_CLIENTE)
           WHERE V.FECHA BETWEEN P_FECHA1 AND P_FECHA2
             AND (U.PAIS = '01' OR U.COD_UBC IS NULL)
             AND UPPER(NVL(U.NOM_DPT,'Sin Departamento')) = UPPER(P_DPTO)
           GROUP BY NVL(U.NOM_DPT,'Sin Departamento'),
                    NVL(U.NOM_DTT,'Sin Distrito'),
                    DECODE(C.GRUPO_REL,NULL,V.COD_CLIENTE,GRP.MIN_CLIENTE)
        )
       GROUP BY DEPARTAMENTO, DISTRITO
       ORDER BY IMPORTE DESC;
  END SP_DETALLE_POR_DISTRITO;

  -- ----------------------------------------------------------
  -- Ciudades de un pais extranjero
  -- ----------------------------------------------------------
  PROCEDURE SP_CIUDADES_POR_PAIS(
    P_FECHA1  IN DATE,
    P_FECHA2  IN DATE,
    P_MON     IN VARCHAR2,
    P_PAIS    IN VARCHAR2,
    P_CURSOR  OUT T_CURSOR
  ) IS
  BEGIN
    OPEN P_CURSOR FOR
      SELECT PAIS_NOMBRE, CIUDAD, SUM(IMPORTE_NETO) IMPORTE
        FROM (
          SELECT NVL(U.NOM_DPT, C.PAIS)     PAIS_NOMBRE,
                 NVL(U.NOM_DTT,'Sin Ciudad') CIUDAD,
                 DECODE(C.GRUPO_REL,NULL,V.COD_CLIENTE,GRP.MIN_CLIENTE) COD_CLI_GRP,
                 ( SUM(DECODE(P_MON,'S',V.SOLES_SINANT,V.DOLARES_SINANT))
                   - NVL(MAX(B.IMP_ANT),0) ) IMPORTE_NETO
            FROM V_DOCUVEN V
            JOIN CLIENTES C ON C.COD_CLIENTE = V.COD_CLIENTE
            LEFT JOIN (SELECT GRUPO, MIN(COD_CLIENTE) MIN_CLIENTE
                         FROM CLIENTE_RELACION GROUP BY GRUPO) GRP
                   ON GRP.GRUPO = C.GRUPO_REL
            LEFT JOIN UBIGEO U ON U.COD_UBC = C.COD_UBC
            LEFT JOIN (SELECT DECODE(C2.GRUPO_REL,NULL,D.COD_CLIENTE,GRP2.MIN_CLIENTE) COD_CLIENTE,
                              SUM(DECODE(P_MON,
                                    'S',DECODE(D.MONEDA,'S',I.IMP_VVTA,ROUND(I.IMP_VVTA*D.IMPORT_CAM,2)),
                                        DECODE(D.MONEDA,'D',I.IMP_VVTA,ROUND(I.IMP_VVTA/NULLIF(D.IMPORT_CAM,0),2)))) IMP_ANT
                         FROM DOCUVENT D
                         JOIN ITEMDOCU I ON I.TIPODOC=D.TIPODOC AND I.SERIE=D.SERIE AND I.NUMERO=D.NUMERO
                         LEFT JOIN CLIENTES C2 ON C2.COD_CLIENTE=D.COD_CLIENTE
                         LEFT JOIN (SELECT GRUPO, MIN(COD_CLIENTE) MIN_CLIENTE
                                      FROM CLIENTE_RELACION GROUP BY GRUPO) GRP2
                                ON GRP2.GRUPO=C2.GRUPO_REL
                        WHERE D.FECHA BETWEEN P_FECHA1 AND P_FECHA2
                          AND D.ESTADO <> '9'
                          AND I.COD_ART IN ('9300049997','9300049999','930004999A','9300049998')
                        GROUP BY DECODE(C2.GRUPO_REL,NULL,D.COD_CLIENTE,GRP2.MIN_CLIENTE)) B
                   ON B.COD_CLIENTE = DECODE(C.GRUPO_REL,NULL,V.COD_CLIENTE,GRP.MIN_CLIENTE)
           WHERE V.FECHA BETWEEN P_FECHA1 AND P_FECHA2
             AND C.PAIS = P_PAIS
           GROUP BY NVL(U.NOM_DPT, C.PAIS),
                    NVL(U.NOM_DTT,'Sin Ciudad'),
                    DECODE(C.GRUPO_REL,NULL,V.COD_CLIENTE,GRP.MIN_CLIENTE)
        )
       GROUP BY PAIS_NOMBRE, CIUDAD
       ORDER BY IMPORTE DESC;
  END SP_CIUDADES_POR_PAIS;

  -- ----------------------------------------------------------
  -- Evolucion mensual de ventas por mercado
  -- ----------------------------------------------------------
  PROCEDURE SP_EVOLUCION_MENSUAL(
    P_FECHA1  IN DATE,
    P_FECHA2  IN DATE,
    P_MON     IN VARCHAR2,
    P_CURSOR  OUT T_CURSOR
  ) IS
  BEGIN
    OPEN P_CURSOR FOR
      SELECT PERIODO, MERCADO, SUM(IMPORTE_NETO) IMPORTE
        FROM (
          SELECT TO_CHAR(V.FECHA,'YYYY-MM') PERIODO,
                 CASE
                   WHEN C.PAIS = '01'                     THEN 'Perú'
                   WHEN NVL(TA.INDICADOR1,'X') = 'L' THEN 'LATAM'
                   WHEN NVL(TA.INDICADOR1,'X') = 'E' THEN 'Europa'
                   WHEN NVL(TA.INDICADOR1,'X') = 'A' THEN 'Asia'
                   WHEN NVL(TA.INDICADOR1,'X') = 'O' THEN 'Oceanía'
                   ELSE 'Otros'
                 END MERCADO,
                 DECODE(C.GRUPO_REL,NULL,V.COD_CLIENTE,GRP.MIN_CLIENTE) COD_CLI_GRP,
                 ( SUM(DECODE(P_MON,'S',V.SOLES_SINANT,V.DOLARES_SINANT))
                   - NVL(MAX(B.IMP_ANT),0) ) IMPORTE_NETO
            FROM V_DOCUVEN V
            JOIN CLIENTES C ON C.COD_CLIENTE = V.COD_CLIENTE
            LEFT JOIN (SELECT GRUPO, MIN(COD_CLIENTE) MIN_CLIENTE
                         FROM CLIENTE_RELACION GROUP BY GRUPO) GRP
                   ON GRP.GRUPO = C.GRUPO_REL
            LEFT JOIN (SELECT CODIGO, MAX(INDICADOR1) INDICADOR1
                         FROM TABLAS_AUXILIARES WHERE TIPO = 25
                        GROUP BY CODIGO) TA
                   ON TA.CODIGO = C.PAIS
            LEFT JOIN (SELECT DECODE(C2.GRUPO_REL,NULL,D.COD_CLIENTE,GRP2.MIN_CLIENTE) COD_CLIENTE,
                              TO_CHAR(D.FECHA,'YYYY-MM') PERIODO,
                              SUM(DECODE(P_MON,
                                    'S',DECODE(D.MONEDA,'S',I.IMP_VVTA,ROUND(I.IMP_VVTA*D.IMPORT_CAM,2)),
                                        DECODE(D.MONEDA,'D',I.IMP_VVTA,ROUND(I.IMP_VVTA/NULLIF(D.IMPORT_CAM,0),2)))) IMP_ANT
                         FROM DOCUVENT D
                         JOIN ITEMDOCU I ON I.TIPODOC=D.TIPODOC AND I.SERIE=D.SERIE AND I.NUMERO=D.NUMERO
                         LEFT JOIN CLIENTES C2 ON C2.COD_CLIENTE=D.COD_CLIENTE
                         LEFT JOIN (SELECT GRUPO, MIN(COD_CLIENTE) MIN_CLIENTE
                                      FROM CLIENTE_RELACION GROUP BY GRUPO) GRP2
                                ON GRP2.GRUPO=C2.GRUPO_REL
                        WHERE D.FECHA BETWEEN P_FECHA1 AND P_FECHA2
                          AND D.ESTADO <> '9'
                          AND I.COD_ART IN ('9300049997','9300049999','930004999A','9300049998')
                        GROUP BY DECODE(C2.GRUPO_REL,NULL,D.COD_CLIENTE,GRP2.MIN_CLIENTE),
                                 TO_CHAR(D.FECHA,'YYYY-MM')) B
                   ON  B.COD_CLIENTE = DECODE(C.GRUPO_REL,NULL,V.COD_CLIENTE,GRP.MIN_CLIENTE)
                   AND B.PERIODO     = TO_CHAR(V.FECHA,'YYYY-MM')
           WHERE V.FECHA BETWEEN P_FECHA1 AND P_FECHA2
           GROUP BY TO_CHAR(V.FECHA,'YYYY-MM'),
                    CASE
                      WHEN C.PAIS = '01'                     THEN 'Perú'
                      WHEN NVL(TA.INDICADOR1,'X') = 'L' THEN 'LATAM'
                      WHEN NVL(TA.INDICADOR1,'X') = 'E' THEN 'Europa'
                      WHEN NVL(TA.INDICADOR1,'X') = 'A' THEN 'Asia'
                      WHEN NVL(TA.INDICADOR1,'X') = 'O' THEN 'Oceanía'
                      ELSE 'Otros'
                    END,
                    DECODE(C.GRUPO_REL,NULL,V.COD_CLIENTE,GRP.MIN_CLIENTE)
        )
       GROUP BY PERIODO, MERCADO
       ORDER BY PERIODO,
                DECODE(MERCADO,'Perú',1,'LATAM',2,'Europa',3,'Asia',4,'Oceanía',5,6);
  END SP_EVOLUCION_MENSUAL;

  -- ----------------------------------------------------------
  -- Catalogo de paises ISO
  -- ----------------------------------------------------------
  PROCEDURE SP_PAISES_ISO(
    P_CURSOR  OUT T_CURSOR
  ) IS
  BEGIN
    OPEN P_CURSOR FOR
      SELECT CODIGO, INDICADOR2, DESCRIPCION
        FROM TABLAS_AUXILIARES
       WHERE TIPO = 25
       ORDER BY CODIGO;
  END SP_PAISES_ISO;

  -- ----------------------------------------------------------
  -- Kilogramos facturados por mes
  --
  -- NOTA: mismo caso que SP_TOP_HILADOS_KG: se usa DOCUVENT
  -- directamente porque V_DOCUVEN no expone ITEMDOCU.CANTIDAD.
  -- Filtros replicados de V_DOCUVEN para coherencia con importes:
  --   D.ORIGEN <> 'A'        -> excluye abonos
  --   NVL(D.TGRAT,'N') <> 'S' -> excluye gratitudes
  -- ----------------------------------------------------------
  PROCEDURE SP_KG_MENSUAL(
    P_FECHA1  IN DATE,
    P_FECHA2  IN DATE,
    P_CURSOR  OUT T_CURSOR
  ) IS
  BEGIN
    OPEN P_CURSOR FOR
      SELECT TO_CHAR(D.FECHA,'YYYY-MM') PERIODO,
             SUM(I.CANTIDAD * E.FACTOR)  CANTIDAD_KG
        FROM DOCUVENT D
        JOIN ITEMDOCU I    ON I.TIPODOC=D.TIPODOC AND I.SERIE=D.SERIE AND I.NUMERO=D.NUMERO
        LEFT JOIN EQUIVALENCIA E ON E.COD_ART=I.COD_ART AND E.UNIDAD='KG'
        LEFT JOIN CLIENTES C     ON C.COD_CLIENTE=D.COD_CLIENTE
        LEFT JOIN (SELECT GRUPO, MIN(COD_CLIENTE) MIN_CLIENTE
                     FROM CLIENTE_RELACION GROUP BY GRUPO) GRP
               ON GRP.GRUPO=C.GRUPO_REL
       WHERE D.FECHA BETWEEN P_FECHA1 AND P_FECHA2
         AND D.ESTADO <> '9'
         AND D.ORIGEN <> 'A'
         AND NVL(D.TGRAT,'N') <> 'S'
         AND I.COD_ART NOT IN ('9300049997','9300049999','930004999A','9300049998')
       GROUP BY TO_CHAR(D.FECHA,'YYYY-MM')
       ORDER BY 1;
  END SP_KG_MENSUAL;

END PKG_VEND_GRUPO_MAESTROCLIENTE;
/
