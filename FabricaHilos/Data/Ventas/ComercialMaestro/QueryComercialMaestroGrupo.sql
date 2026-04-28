/*------------------------------------------------------------------------
  Reporte Comercial por Cliente / Asesor / Giro
  --------------------------------------------------------------------
  - Toma los MONTOS TOTALES (Soles y Dolares) desde la vista SIG.V_DOCUVEN
    (misma fuente que logica_inicial.sql), por lo que el resultado de
    SOLES y DOLARES coincide con dicho script.
  - Mantiene la agrupacion por GRUPO_REL (subquery GRP sobre
    sig.CLIENTE_RELACION) y el vinculo del vendedor a traves de
    CLIENTES.VENDEDOR -> TABLAS_AUXILIARES (TIPO 29).
  - Si :P_OPCION = 'TODOS' usa los montos completos (V.SOLES / V.DOLARES).
    En caso contrario usa los montos sin anticipos (V.SOLES_SINANT /
    V.DOLARES_SINANT) y resta adicionalmente los items de anticipo
    (cod_art 9300049997, 9300049999, 930004999A, 9300049998), tal como
    lo hace logica_inicial.sql.
  - Columnas adicionales (subquery C, sobre DOCUVENT + ITEMDOCU):
        NRODOC   = COUNT(DISTINCT documento)
        TOTUNID  = SUM(I.CANTIDAD * E.FACTOR)              -- KG
    Se agrega por cliente/asesor (consolidado por GRUPO_REL) sin
    partir filas, para no duplicar los montos que vienen de V_DOCUVEN.
    Aplica los mismos filtros (FECHA, ESTADO, anticipos) que el
    resto del reporte.

  Parametros:
    :P_FECHA1, :P_FECHA2  -> rango de fechas
    :P_OPCION             -> 'TODOS' = monto bruto;
                             cualquier otro valor = monto sin anticipos
------------------------------------------------------------------------*/
SELECT A.COD_CLIENTE,
       A.RUC,
       A.NOMBRE,
       A.GIRO,
       A.DESC_GIRO,
       A.COD_ASESOR,
       A.ASESOR,
       NVL(C.NRODOC,  0)  NRODOC,
       NVL(C.TOTUNID, 0)  TOTUNID,
       (A.SOLES  - NVL(B.SOLES_ANT,  0)) SOLES,
       (A.DOLAR  - NVL(B.DOLAR_ANT,  0)) DOLAR
  FROM (SELECT DECODE(C.GRUPO_REL, NULL, V.COD_CLIENTE, GRP.MIN_CLIENTE) AS COD_CLIENTE,
               CLL.RUC,
               CLL.NOMBRE,
               C.GIRO,
               T2.ABREVIADA DESC_GIRO,
               C.VENDEDOR  COD_ASESOR,
               T.DESCRIPCION ASESOR,
               SUM(DECODE(:P_OPCION, 'TODOS', V.SOLES,   V.SOLES_SINANT))   SOLES,
               SUM(DECODE(:P_OPCION, 'TODOS', V.DOLARES, V.DOLARES_SINANT)) DOLAR
          FROM V_DOCUVEN V
          LEFT JOIN CLIENTES C            ON  C.COD_CLIENTE = V.COD_CLIENTE
          LEFT JOIN TABLAS_AUXILIARES T   ON  T.CODIGO  = C.VENDEDOR
                                          AND T.TIPO    = 29
          LEFT JOIN TABLAS_AUXILIARES T2  ON  T2.CODIGO = C.GIRO
                                          AND T2.TIPO   = 27
          LEFT JOIN (SELECT GRUPO, MIN(COD_CLIENTE) AS MIN_CLIENTE
                       FROM sig.CLIENTE_RELACION
                      GROUP BY GRUPO) GRP ON  GRP.GRUPO = C.GRUPO_REL
          LEFT JOIN CLIENTES CLL          ON  CLL.COD_CLIENTE =
                                              DECODE(C.GRUPO_REL, NULL, V.COD_CLIENTE, GRP.MIN_CLIENTE)
         WHERE V.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
         GROUP BY DECODE(C.GRUPO_REL, NULL, V.COD_CLIENTE, GRP.MIN_CLIENTE),
                  CLL.RUC,
                  CLL.NOMBRE,
                  C.GIRO,
                  T2.ABREVIADA,
                  C.VENDEDOR,
                  T.DESCRIPCION) A
  LEFT JOIN (SELECT DECODE(C.GRUPO_REL, NULL, D.COD_CLIENTE, GRP.MIN_CLIENTE) AS COD_CLIENTE,
                    C.VENDEDOR COD_ASESOR,
                    SUM(DECODE(D.MONEDA,
                               'S', I.IMP_VVTA,
                               ROUND(I.IMP_VVTA * D.IMPORT_CAM, 2))) SOLES_ANT,
                    SUM(DECODE(D.MONEDA,
                               'D', I.IMP_VVTA,
                               ROUND(I.IMP_VVTA / NULLIF(D.IMPORT_CAM, 0), 2))) DOLAR_ANT
               FROM DOCUVENT D
               JOIN ITEMDOCU I              ON  I.TIPODOC = D.TIPODOC
                                            AND I.SERIE   = D.SERIE
                                            AND I.NUMERO  = D.NUMERO
               LEFT JOIN CLIENTES C         ON  C.COD_CLIENTE = D.COD_CLIENTE
               LEFT JOIN (SELECT GRUPO, MIN(COD_CLIENTE) AS MIN_CLIENTE
                            FROM sig.CLIENTE_RELACION
                           GROUP BY GRUPO) GRP ON GRP.GRUPO = C.GRUPO_REL
              WHERE :P_OPCION <> 'TODOS'
                AND D.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
                AND D.ESTADO <> '9'
                AND I.COD_ART IN ('9300049997',
                                  '9300049999',
                                  '930004999A',
                                  '9300049998')
              GROUP BY DECODE(C.GRUPO_REL, NULL, D.COD_CLIENTE, GRP.MIN_CLIENTE),
                       C.VENDEDOR) B
    ON  B.COD_CLIENTE = A.COD_CLIENTE
    AND B.COD_ASESOR  = A.COD_ASESOR
  LEFT JOIN (SELECT DECODE(C.GRUPO_REL, NULL, D.COD_CLIENTE, GRP.MIN_CLIENTE) AS COD_CLIENTE,
                    C.VENDEDOR COD_ASESOR,
                    COUNT(DISTINCT D.TIPODOC || '-' || D.SERIE || '-' || D.NUMERO) NRODOC,
                    SUM(I.CANTIDAD * E.FACTOR) TOTUNID
               FROM DOCUVENT D
               JOIN ITEMDOCU I              ON  I.TIPODOC = D.TIPODOC
                                            AND I.SERIE   = D.SERIE
                                            AND I.NUMERO  = D.NUMERO
               LEFT JOIN EQUIVALENCIA E     ON  E.COD_ART = I.COD_ART
                                            AND E.UNIDAD  = 'KG'
               LEFT JOIN CLIENTES C         ON  C.COD_CLIENTE = D.COD_CLIENTE
               LEFT JOIN (SELECT GRUPO, MIN(COD_CLIENTE) AS MIN_CLIENTE
                            FROM sig.CLIENTE_RELACION
                           GROUP BY GRUPO) GRP ON GRP.GRUPO = C.GRUPO_REL
              WHERE D.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
                AND D.ESTADO <> '9'
                AND (:P_OPCION = 'TODOS'
                     OR I.COD_ART NOT IN ('9300049997',
                                          '9300049999',
                                          '930004999A',
                                          '9300049998'))
              GROUP BY DECODE(C.GRUPO_REL, NULL, D.COD_CLIENTE, GRP.MIN_CLIENTE),
                       C.VENDEDOR) C
    ON  C.COD_CLIENTE = A.COD_CLIENTE
    AND C.COD_ASESOR  = A.COD_ASESOR
 ORDER BY A.COD_ASESOR, A.COD_CLIENTE
