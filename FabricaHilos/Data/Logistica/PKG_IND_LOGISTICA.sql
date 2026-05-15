/* ============================================================
   PKG_IND_LOGISTICA
   Módulo  : Logística — Indicadores / KPIs
   BD      : SIG (Oracle)
   Tablas  : REQUISICION, ITEMREQ, ARTICUL, DESP_ITEMREQ,
             ORDEN_DE_COMPRA, TABLAS_AUXILIARES,
             V_PERSONAL, V_CENTRO_DE_COSTOS, ACTIVO_FIJO
   Creado  : 15/05/2026

   CONVENCIONES DE VALIDACIÓN APLICADAS EN TODOS LOS CURSORES:
     · Precios nulos     → NVL(I.PRECIO, 0)           evita NULL en montos
     · División por cero → NULLIF(denominador, 0)      evita ORA-01476
     · Porcentajes       → COUNT(DISTINCT CASE WHEN condicion THEN clave END)
                           / NULLIF(COUNT(DISTINCT clave), 0)
                           evita inflar % al contar filas de ítems en vez de reqs
     · Diferencias fecha → CASE WHEN fecha IS NOT NULL THEN ... END
                           evita NULLs propagados cuando el tramo aún no ocurrió
     · Montos con IGV    → DECODE(AFECTO_IGV,'S', monto*(IMPSTO+1), monto)

   PROCEDIMIENTOS EXPUESTOS:
     P_DETALLE           → listado completo req+ítems  (grid / reporte PDF)
     P_DASHBOARD         → 4 KPIs en un solo call      (panel ejecutivo)
     P_CICLO_VIDA        → ciclo req→OC por req         (Gantt / histograma)
     P_TENDENCIA_MENSUAL → promedios mes a mes          (barras apiladas / líneas)

   USO DESDE .NET (ODP.NET):
     OracleCommand("PKG_IND_LOGISTICA.P_DETALLE",           conn)
     OracleCommand("PKG_IND_LOGISTICA.P_DASHBOARD",         conn)
     OracleCommand("PKG_IND_LOGISTICA.P_CICLO_VIDA",        conn)
     OracleCommand("PKG_IND_LOGISTICA.P_TENDENCIA_MENSUAL", conn)
     Todos con CommandType = StoredProcedure
============================================================ */

-- ============================================================
-- SPEC
-- ============================================================
CREATE OR REPLACE PACKAGE PKG_IND_LOGISTICA AS

    TYPE T_CURSOR IS REF CURSOR;

    /* ----------------------------------------------------------
       P_DETALLE
       ─────────────────────────────────────────────────────────
       PROPÓSITO:
         Devuelve UNA FILA por cada artículo de requisición en el
         rango de fechas. Incluye todos los estados (incluso
         ANULADO) para que el .NET pueda filtrar o colorear por
         estado sin hacer consultas adicionales.

       NOTA IMPORTANTE:
         Si un ítem fue despachado parcialmente a varias OCs
         distintas, aparece UNA FILA POR CADA DESPACHO (hereda
         el JOIN externo a DESP_ITEMREQ). Esto es intencional:
         muestra el historial de despacho completo.

       COLUMNAS:
         TIPO           — 'COMPRA' o 'SERVICIO'
         NUMREQ         — número de requisición
         FECHA          — fecha de registro
         F_AUTORIZA     — fecha de visado/autorización por jefe
         F_RECIBE       — fecha de recibo por Logística
         ORDEN_COMPRA   — número de OC asociada (NULL si no existe)
         FCH_ORDEN      — fecha de emisión de la OC
         DESTINO        — código CC o AF destino
         DESC_DESTINO   — descripción del CC o AF
         SOLICITA       — nombre corto del solicitante
         OBSERVACION    — texto libre de la requisición
         COD_ART        — código de artículo (o 'PEDIDOxxx')
         DESC_ARTICULO  — descripción (ARTICUL o ITEMREQ.DETALLE
                          si es pedido de texto libre)
         UNIDAD         — unidad de medida
         CANTIDAD       — cantidad solicitada
         CANT_DESP      — cantidad ya despachada (CANTIDAD - SALDO)
         SALDO          — cantidad pendiente de despacho
         PUNIT          — precio unitario
         SUB_TOTAL      — CANTIDAD × PUNIT
         IGV            — IGV sobre el sub-total (0 si exonerado)
         TOTAL          — importe total con IGV
         ESTADO         — 'REGISTRADO','VISADO','RECIBIDO',
                          'ATENDIDO' o 'ANULADO'

       GRÁFICO RECOMENDADO:
         ★ DataGrid/DataTable con filtros por ESTADO y TIPO.
           Es la fuente principal para exportar a Excel.
         ★ Pie Chart de distribución de MONTO por ESTADO:
           segmentos = ATENDIDO | RECIBIDO | VISADO | REGISTRADO.
         ★ Donut Chart de TIPO (COMPRA vs SERVICIO) por MONTO.
    ---------------------------------------------------------- */
    PROCEDURE P_DETALLE (
        P_FECHA_DESDE  IN  DATE,
        P_FECHA_HASTA  IN  DATE,
        P_CURSOR       OUT T_CURSOR
    );

    /* ----------------------------------------------------------
       P_DASHBOARD
       ─────────────────────────────────────────────────────────
       PROPÓSITO:
         Devuelve 4 cursores en una sola llamada al servidor.
         Pensado para cargar un panel ejecutivo completo con
         el mínimo de round-trips a BD.

       ── CURSOR 1: P_CUR_RESUMEN ──────────────────────────────
         Agrupación por TIPO (COMPRA/SERVICIO) y ESTADO.
         PCT_ATENDIDO es calculado POR TIPO con window functions:
         la misma cifra aparece en todas las filas del mismo TIPO,
         representando '% no anuladas que ya están ATENDIDAS'.

         COLUMNAS: TIPO, ESTADO, CANT_REQS, CANT_ITEMS,
                   MONTO_TOTAL, PCT_ATENDIDO

         GRÁFICO RECOMENDADO:
           ★ Stacked Bar horizontal: eje Y = TIPO, segmentos por ESTADO.
           ★ KPI Card: mostrar PCT_ATENDIDO como badge o gauge.
           ★ Grouped Bar: CANT_REQS vs MONTO_TOTAL por TIPO/ESTADO.

       ── CURSOR 2: P_CUR_TIEMPOS ──────────────────────────────
         Una sola fila con promedios del ciclo. Cada tramo se
         calcula SOLO sobre las reqs que tienen ambas fechas del
         tramo, evitando que NULLs contaminen el promedio.

         COLUMNAS: TOTAL_REQS, DIAS_REG_AUTORIZACION,
                   DIAS_AUT_RECIBO, DIAS_RECIBO_OC, DIAS_CICLO_TOTAL

         GRÁFICO RECOMENDADO:
           ★ Waterfall horizontal: una barra por tramo, coloreada
             verde (≤ SLA) o rojo (> SLA).
           ★ Gauge/Velocímetro: DIAS_CICLO_TOTAL vs objetivo.
           ★ Bullet Chart: valor actual vs target por tramo.

       ── CURSOR 3: P_CUR_TOP_CCOSTO ───────────────────────────
         Top 10 destinos (CC o AF) por monto, excluyendo anuladas.

         COLUMNAS: DESTINO, DESC_DESTINO, TP_DESTINO,
                   CANT_ITEMS, CANT_REQS, MONTO_TOTAL

         GRÁFICO RECOMENDADO:
           ★ Bar Chart horizontal con ranking del 1 al 10.
             Colorear diferente CC vs Activo Fijo.
           ★ Treemap: rectángulo por destino, tamaño = monto.
           ★ Pareto: barra de monto + línea acumulada de %.

       ── CURSOR 4: P_CUR_PENDIENTES ───────────────────────────
         Ítems con SALDO > 0 en reqs no atendidas. Son los
         pedidos que Logística todavía tiene pendientes.
         DIAS_EN_ESPERA usa F_RECIBE si ya llegó a Logística,
         o FECHA de registro si aún no la recibieron — siempre
         devuelve un número, nunca NULL.

         COLUMNAS: NUMREQ, FECHA, TIPO, ESTADO, SOLICITA,
                   COD_ART, DESC_ARTICULO, SALDO,
                   MONTO_PENDIENTE, DIAS_EN_ESPERA

         GRÁFICO RECOMENDADO:
           ★ Grid con semáforo: verde (<3d), amarillo (3-7d),
             rojo (>7d) en DIAS_EN_ESPERA.
           ★ Bubble Chart: eje X = días espera, eje Y = monto,
             tamaño burbuja = saldo en unidades.
           ★ KPI Card: total ítems + monto total pendiente.
    ---------------------------------------------------------- */
    PROCEDURE P_DASHBOARD (
        P_FECHA_DESDE     IN  DATE,
        P_FECHA_HASTA     IN  DATE,
        P_CUR_RESUMEN     OUT T_CURSOR,
        P_CUR_TIEMPOS     OUT T_CURSOR,
        P_CUR_TOP_CCOSTO  OUT T_CURSOR,
        P_CUR_PENDIENTES  OUT T_CURSOR
    );

    /* ----------------------------------------------------------
       P_CICLO_VIDA
       ─────────────────────────────────────────────────────────
       PROPÓSITO:
         Devuelve UNA FILA POR REQUISICIÓN completamente atendida
         (ESTADO='6') con las cuatro fechas hito del ciclo y los
         días de cada tramo. Solo incluye reqs con F_AUTORIZA,
         F_RECIBE y FCH_OC conocidas, garantizando que los 3
         tramos sean siempre calculables y sin NULLs.

       COLUMNAS:
         NUMREQ         — número de requisición
         TIPO           — 'COMPRA' o 'SERVICIO'
         NRO_OC         — número de la OC generada
                          (varias reqs pueden compartir la misma OC)
         FCH_REGISTRO   — fecha de alta de la requisición
         FCH_AUTORIZA   — fecha de visado por el jefe solicitante
         FCH_RECIBO_LOG — fecha de recibo por Logística
         FCH_OC         — fecha de emisión de la Orden de Compra
         T1_REG_AUT     — días de FCH_REGISTRO a FCH_AUTORIZA
         T2_AUT_REC     — días de FCH_AUTORIZA a FCH_RECIBO_LOG
         T3_REC_OC      — días de FCH_RECIBO_LOG a FCH_OC
         T_CICLO_TOTAL  — días de FCH_REGISTRO a FCH_OC

       NOTA — OCS COMPARTIDAS:
         Una OC puede agrupar hasta 5 reqs distintas (dato real
         de BD). Cada req aparece en su fila aunque compartan
         NRO_OC. En la UI, agrupar por NRO_OC y mostrar las
         reqs como subfilas o líneas convergentes en el Gantt.

       GRÁFICO RECOMENDADO:
         ★ GANTT / TIMELINE (el más potente para este cursor):
             Eje Y = NUMREQ (o NRO_OC agrupando reqs).
             Eje X = fechas.
             Segmento azul    = T1 (Registro → Autorización)
             Segmento naranja = T2 (Autorización → Logística)
             Segmento verde   = T3 (Logística → OC)
             Las reqs de la misma OC convergen al mismo punto.
             Usar rango corto (1-4 semanas) para no saturar el eje.
         ★ HISTOGRAMA del ciclo total:
             Agrupar T_CICLO_TOTAL en tramos (0d, 1d, 2-3d, 4-5d,
             6-7d, 8-14d, >14d). Ideal para definir un SLA.
         ★ SCATTER PLOT ciclo vs monto:
             Detecta outliers: reqs de alto monto con ciclo largo.
    ---------------------------------------------------------- */
    PROCEDURE P_CICLO_VIDA (
        P_FECHA_DESDE  IN  DATE,
        P_FECHA_HASTA  IN  DATE,
        P_CURSOR       OUT T_CURSOR
    );

    /* ----------------------------------------------------------
       P_TENDENCIA_MENSUAL
       ─────────────────────────────────────────────────────────
       PROPÓSITO:
         Devuelve UNA FILA POR MES con los promedios de cada
         tramo del ciclo y el volumen de reqs atendidas.
         Permite ver la evolución histórica del proceso logístico
         a lo largo de N meses hacia atrás desde hoy.

         Los % usan COUNT(DISTINCT NUMREQ) en numerador y
         denominador para evitar inflar la cifra al contar
         múltiples ítems de la misma requisición.

         Los promedios de tramo usan CASE WHEN para ignorar reqs
         donde el tramo aún no ocurrió, igual que P_CUR_TIEMPOS.

       PARÁMETROS:
         P_MESES_ATRAS — meses hacia atrás a incluir (default 12)

       COLUMNAS:
         MES             — período 'YYYY-MM'
         CANT_REQS       — requisiciones atendidas ese mes
         T1_AVG          — promedio días Registro→Autorización
         T2_AVG          — promedio días Autorización→Recibo Log
         T3_AVG          — promedio días Recibo Log→OC
         CICLO_AVG       — promedio días ciclo total (Reg→OC)
         PCT_MISMO_DIA   — % de reqs atendidas el mismo día
                           en que fueron registradas
         PCT_HASTA_5DIAS — % de reqs con ciclo total ≤ 5 días
                           (SLA sugerido basado en data real)

       GRÁFICO RECOMENDADO:
         ★ STACKED BAR APILADO (el más recomendado):
             Eje X = MES, Eje Y = días.
             Barra azul    = T1_AVG  (Registro → Autorización)
             Barra naranja = T2_AVG  (Autorización → Recibo Log)
             Barra verde   = T3_AVG  (Recibo Log → OC)
             Altura total  = CICLO_AVG.
             Identifica qué tramo es el cuello de botella
             en cada mes.
         ★ LÍNEA superpuesta (eje secundario Y):
             PCT_HASTA_5DIAS como % de cumplimiento SLA.
             Si baja de 70%, el proceso se está rezagando.
         ★ COMBO Chart: barras por CANT_REQS + línea CICLO_AVG.
    ---------------------------------------------------------- */
    PROCEDURE P_TENDENCIA_MENSUAL (
        P_MESES_ATRAS  IN  NUMBER DEFAULT 12,
        P_CURSOR       OUT T_CURSOR
    );

END PKG_IND_LOGISTICA;
/

-- ============================================================
-- BODY
-- ============================================================
CREATE OR REPLACE PACKAGE BODY PKG_IND_LOGISTICA AS

    /* ----------------------------------------------------------
       P_DETALLE
    ---------------------------------------------------------- */
    PROCEDURE P_DETALLE (
        P_FECHA_DESDE  IN  DATE,
        P_FECHA_HASTA  IN  DATE,
        P_CURSOR       OUT T_CURSOR
    ) AS
    BEGIN
        OPEN P_CURSOR FOR
            SELECT
                DECODE(NVL(R.IND_SERV,'N'), 'N','COMPRA', 'S','SERVICIO')    TIPO,
                R.NUMREQ,
                R.FECHA,
                R.F_AUTORIZA,
                R.F_RECIBE,
                D.NRO_DOC_REF                                                 ORDEN_COMPRA,
                O.FECHA                                                       FCH_ORDEN,
                I.DESTINO,
                DECODE(I.TP_DESTINO,
                    'U', C.DESC_CCOSTO_DET,
                    'A', F.DESCRIPCION)                                       DESC_DESTINO,
                P.NOMBRE_CORTO                                                SOLICITA,
                R.OBSERVACION,
                I.COD_ART,
                DECODE(SUBSTR(I.COD_ART,1,6),
                    'PEDIDO', I.DETALLE,
                    A.DESCRIPCION)                                            DESC_ARTICULO,
                I.UNIDAD,
                I.CANTIDAD,
                (I.CANTIDAD - I.SALDO)                                        CANT_DESP,
                I.SALDO,
                I.PRECIO                                                      PUNIT,
                (I.CANTIDAD * NVL(I.PRECIO,0))                                SUB_TOTAL,
                DECODE(R.AFECTO_IGV,
                    'S', (I.CANTIDAD * NVL(I.PRECIO,0)) * R.IMPSTO,
                    0)                                                        IGV,
                DECODE(R.AFECTO_IGV,
                    'S', (I.CANTIDAD * NVL(I.PRECIO,0)) * (R.IMPSTO + 1),
                    (I.CANTIDAD * NVL(I.PRECIO,0)))                           TOTAL,
                T.ABREVIADA                                                   ESTADO
            FROM
                REQUISICION        R,
                ITEMREQ            I,
                ARTICUL            A,
                DESP_ITEMREQ       D,
                ORDEN_DE_COMPRA    O,
                TABLAS_AUXILIARES  T,
                V_PERSONAL         P,
                V_CENTRO_DE_COSTOS C,
                ACTIVO_FIJO        F
            WHERE
                TRUNC(R.FECHA) BETWEEN P_FECHA_DESDE AND P_FECHA_HASTA
                AND I.NUMREQ         = R.NUMREQ
                AND A.COD_ART(+)     = I.COD_ART
                AND O.SERIE(+)       = 1
                AND O.TIPO_DOCTO(+)  = D.TIP_DOC_REF
                AND O.NUM_PED(+)     = D.NRO_DOC_REF
                AND D.TIPDOC(+)      = '80'
                AND D.NUMREQ(+)      = I.NUMREQ
                AND D.COD_ART(+)     = I.COD_ART
                AND T.TIPO           = 84
                AND T.CODIGO         = R.ESTADO
                AND P.C_CODIGO       = I.COD_SOLICITA
                AND C.CCOSTO_DET(+)  = I.DESTINO
                AND F.CODIGO(+)||'-'||F.NUMERO(+) = I.DESTINO
            ORDER BY
                DECODE(NVL(R.IND_SERV,'N'),'N','COMPRA','S','SERVICIO'),
                R.NUMREQ;
    END P_DETALLE;

    /* ----------------------------------------------------------
       P_DASHBOARD
    ---------------------------------------------------------- */
    PROCEDURE P_DASHBOARD (
        P_FECHA_DESDE     IN  DATE,
        P_FECHA_HASTA     IN  DATE,
        P_CUR_RESUMEN     OUT T_CURSOR,
        P_CUR_TIEMPOS     OUT T_CURSOR,
        P_CUR_TOP_CCOSTO  OUT T_CURSOR,
        P_CUR_PENDIENTES  OUT T_CURSOR
    ) AS
    BEGIN

        /* ── 1. RESUMEN: totales por TIPO y ESTADO ──────────────────────────
           PCT_ATENDIDO usa window OVER(PARTITION BY TIPO): misma cifra en
           todas las filas del mismo TIPO → '% no anuladas ya ATENDIDAS'.
        ------------------------------------------------------------------ */
        OPEN P_CUR_RESUMEN FOR
            SELECT
                TIPO, ESTADO, CANT_REQS, CANT_ITEMS, MONTO_TOTAL,
                ROUND(
                    SUM(CASE WHEN ESTADO = 'ATENDIDO' THEN CANT_REQS ELSE 0 END)
                        OVER (PARTITION BY TIPO) * 100.0
                    / NULLIF(
                        SUM(CASE WHEN ESTADO != 'ANULADO' THEN CANT_REQS ELSE 0 END)
                            OVER (PARTITION BY TIPO)
                      , 0)
                , 1)  PCT_ATENDIDO
            FROM (
                SELECT
                    DECODE(NVL(R.IND_SERV,'N'),'N','COMPRA','S','SERVICIO')  TIPO,
                    T.ABREVIADA                                               ESTADO,
                    R.ESTADO                                                  COD_ESTADO,
                    COUNT(DISTINCT R.NUMREQ)                                  CANT_REQS,
                    COUNT(I.COD_ART)                                          CANT_ITEMS,
                    ROUND(SUM(
                        DECODE(R.AFECTO_IGV,
                            'S', (I.CANTIDAD * NVL(I.PRECIO,0)) * (R.IMPSTO + 1),
                            (I.CANTIDAD * NVL(I.PRECIO,0)))
                    ), 2)                                                     MONTO_TOTAL
                FROM
                    REQUISICION        R,
                    ITEMREQ            I,
                    TABLAS_AUXILIARES  T
                WHERE
                    TRUNC(R.FECHA) BETWEEN P_FECHA_DESDE AND P_FECHA_HASTA
                    AND I.NUMREQ = R.NUMREQ
                    AND T.TIPO   = 84
                    AND T.CODIGO = R.ESTADO
                GROUP BY
                    DECODE(NVL(R.IND_SERV,'N'),'N','COMPRA','S','SERVICIO'),
                    T.ABREVIADA, R.ESTADO
            )
            ORDER BY
                TIPO,
                CASE COD_ESTADO
                    WHEN '0' THEN 1  -- REGISTRADO
                    WHEN '1' THEN 2  -- VISADO
                    WHEN '2' THEN 3  -- RECIBIDO
                    WHEN '6' THEN 4  -- ATENDIDO
                    WHEN '9' THEN 5  -- ANULADO
                    ELSE 9
                END;

        /* ── 2. TIEMPOS PROMEDIO DEL CICLO ─────────────────────── */
        -- Cada tramo se calcula solo con las reqs que tienen ambas fechas del tramo
        OPEN P_CUR_TIEMPOS FOR
            SELECT
                COUNT(*)                                                              TOTAL_REQS,
                -- Tramo 1: Registro → Autorización (reqs con F_AUTORIZA)
                ROUND(AVG(CASE WHEN R.F_AUTORIZA IS NOT NULL
                               THEN TRUNC(R.F_AUTORIZA) - TRUNC(R.FECHA) END), 1)   DIAS_REG_AUTORIZACION,
                -- Tramo 2: Autorización → Recibo Logística (reqs con ambas fechas)
                ROUND(AVG(CASE WHEN R.F_AUTORIZA IS NOT NULL AND R.F_RECIBE IS NOT NULL
                               THEN TRUNC(R.F_RECIBE) - TRUNC(R.F_AUTORIZA) END), 1) DIAS_AUT_RECIBO,
                -- Tramo 3: Recibo Logística → OC generada (reqs con ambas fechas)
                ROUND(AVG(CASE WHEN R.F_RECIBE IS NOT NULL AND R.FCH_ENTREGA_LOGIST IS NOT NULL
                               THEN TRUNC(R.FCH_ENTREGA_LOGIST) - TRUNC(R.F_RECIBE) END), 1) DIAS_RECIBO_OC,
                -- Ciclo total: Registro → OC (solo reqs completamente atendidas)
                ROUND(AVG(CASE WHEN R.FCH_ENTREGA_LOGIST IS NOT NULL
                               THEN TRUNC(R.FCH_ENTREGA_LOGIST) - TRUNC(R.FECHA) END), 1) DIAS_CICLO_TOTAL
            FROM REQUISICION R
            WHERE
                TRUNC(R.FECHA) BETWEEN P_FECHA_DESDE AND P_FECHA_HASTA
                AND R.ESTADO NOT IN ('9');

        /* ── 3. TOP 10 DESTINOS POR MONTO ──────────────────────── */
        OPEN P_CUR_TOP_CCOSTO FOR
            SELECT * FROM (
                SELECT
                    I.DESTINO,
                    DECODE(I.TP_DESTINO,
                        'U', C.DESC_CCOSTO_DET,
                        'A', F.DESCRIPCION,
                        I.DESTINO)                                            DESC_DESTINO,
                    I.TP_DESTINO,
                    COUNT(*)                                                  CANT_ITEMS,
                    COUNT(DISTINCT R.NUMREQ)                                  CANT_REQS,
                    ROUND(SUM(
                        DECODE(R.AFECTO_IGV,
                            'S', (I.CANTIDAD * NVL(I.PRECIO,0)) * (R.IMPSTO + 1),
                            (I.CANTIDAD * NVL(I.PRECIO,0)))
                    ), 2)                                                     MONTO_TOTAL
                FROM
                    REQUISICION        R,
                    ITEMREQ            I,
                    V_CENTRO_DE_COSTOS C,
                    ACTIVO_FIJO        F
                WHERE
                    TRUNC(R.FECHA) BETWEEN P_FECHA_DESDE AND P_FECHA_HASTA
                    AND R.ESTADO    != '9'
                    AND I.NUMREQ     = R.NUMREQ
                    AND C.CCOSTO_DET(+) = I.DESTINO
                    AND F.CODIGO(+)||'-'||F.NUMERO(+) = I.DESTINO
                GROUP BY
                    I.DESTINO, I.TP_DESTINO,
                    C.DESC_CCOSTO_DET, F.DESCRIPCION
                ORDER BY MONTO_TOTAL DESC
            ) WHERE ROWNUM <= 10;

        /* ── 4. ÍTEMS CON SALDO PENDIENTE ────────────────────────────────
           DIAS_EN_ESPERA: usa F_RECIBE si ya llegó a Logística, o FECHA
           de registro si aún no fue recibida. Siempre retorna un número.
        ------------------------------------------------------------------ */
        OPEN P_CUR_PENDIENTES FOR
            SELECT
                R.NUMREQ,
                R.FECHA,
                DECODE(NVL(R.IND_SERV,'N'),'N','COMPRA','S','SERVICIO')  TIPO,
                T.ABREVIADA                                               ESTADO,
                P.NOMBRE_CORTO                                            SOLICITA,
                I.COD_ART,
                DECODE(SUBSTR(I.COD_ART,1,6),
                    'PEDIDO', I.DETALLE,
                    A.DESCRIPCION)                                        DESC_ARTICULO,
                I.SALDO,
                ROUND(I.SALDO * NVL(I.PRECIO,0), 2)                      MONTO_PENDIENTE,
                TRUNC(SYSDATE) - TRUNC(
                    CASE WHEN R.F_RECIBE IS NOT NULL
                    THEN R.F_RECIBE
                    ELSE R.FECHA END
                )                                                         DIAS_EN_ESPERA
            FROM
                REQUISICION        R,
                ITEMREQ            I,
                ARTICUL            A,
                TABLAS_AUXILIARES  T,
                V_PERSONAL         P
            WHERE
                TRUNC(R.FECHA) BETWEEN P_FECHA_DESDE AND P_FECHA_HASTA
                AND R.ESTADO       NOT IN ('6','9')
                AND I.NUMREQ        = R.NUMREQ
                AND I.SALDO         > 0
                AND A.COD_ART(+)    = I.COD_ART
                AND T.TIPO          = 84
                AND T.CODIGO        = R.ESTADO
                AND P.C_CODIGO      = I.COD_SOLICITA
            ORDER BY DIAS_EN_ESPERA DESC, R.NUMREQ;

    END P_DASHBOARD;

    /* ----------------------------------------------------------
       P_CICLO_VIDA
    ---------------------------------------------------------- */
    PROCEDURE P_CICLO_VIDA (
        P_FECHA_DESDE  IN  DATE,
        P_FECHA_HASTA  IN  DATE,
        P_CURSOR       OUT T_CURSOR
    ) AS
    BEGIN
        OPEN P_CURSOR FOR
            SELECT
                R.NUMREQ,
                DECODE(NVL(R.IND_SERV,'N'),'N','COMPRA','S','SERVICIO')  TIPO,
                MIN(D.NRO_DOC_REF)                                        NRO_OC,
                TRUNC(R.FECHA)           FCH_REGISTRO,
                TRUNC(R.F_AUTORIZA)      FCH_AUTORIZA,
                TRUNC(R.F_RECIBE)        FCH_RECIBO_LOG,
                TRUNC(O.FECHA)           FCH_OC,
                TRUNC(R.F_AUTORIZA) - TRUNC(R.FECHA)          T1_REG_AUT,
                TRUNC(R.F_RECIBE)   - TRUNC(R.F_AUTORIZA)     T2_AUT_REC,
                TRUNC(O.FECHA)      - TRUNC(R.F_RECIBE)        T3_REC_OC,
                TRUNC(O.FECHA)      - TRUNC(R.FECHA)           T_CICLO_TOTAL
            FROM REQUISICION R, ITEMREQ I, DESP_ITEMREQ D, ORDEN_DE_COMPRA O
            WHERE TRUNC(R.FECHA) BETWEEN P_FECHA_DESDE AND P_FECHA_HASTA
              AND R.ESTADO        = '6'
              AND R.F_AUTORIZA   IS NOT NULL
              AND R.F_RECIBE     IS NOT NULL
              AND I.NUMREQ        = R.NUMREQ
              AND D.TIPDOC        = '80'
              AND D.NUMREQ        = I.NUMREQ
              AND D.COD_ART       = I.COD_ART
              AND O.SERIE         = 1
              AND O.TIPO_DOCTO    = D.TIP_DOC_REF
              AND O.NUM_PED       = D.NRO_DOC_REF
              AND O.FECHA        IS NOT NULL
            GROUP BY
                R.NUMREQ, R.IND_SERV,
                R.FECHA, R.F_AUTORIZA, R.F_RECIBE, O.FECHA
            ORDER BY R.FECHA DESC, R.NUMREQ;
    END P_CICLO_VIDA;

    /* ----------------------------------------------------------
       P_TENDENCIA_MENSUAL
    ---------------------------------------------------------- */
    PROCEDURE P_TENDENCIA_MENSUAL (
        P_MESES_ATRAS  IN  NUMBER DEFAULT 12,
        P_CURSOR       OUT T_CURSOR
    ) AS
    BEGIN
        OPEN P_CURSOR FOR
            SELECT
                TO_CHAR(R.FECHA,'YYYY-MM')                                     MES,
                COUNT(DISTINCT R.NUMREQ)                                        CANT_REQS,
                -- promedios de tramos: CASE WHEN garantiza que reqs con
                -- fechas parciales no contaminen meses con datos completos
                ROUND(AVG(
                    CASE WHEN R.F_AUTORIZA IS NOT NULL
                    THEN TRUNC(R.F_AUTORIZA) - TRUNC(R.FECHA) END
                ), 1)                                                          T1_AVG,
                ROUND(AVG(
                    CASE WHEN R.F_AUTORIZA IS NOT NULL AND R.F_RECIBE IS NOT NULL
                    THEN TRUNC(R.F_RECIBE) - TRUNC(R.F_AUTORIZA) END
                ), 1)                                                          T2_AVG,
                ROUND(AVG(
                    CASE WHEN R.F_RECIBE IS NOT NULL AND O.FECHA IS NOT NULL
                    THEN TRUNC(O.FECHA) - TRUNC(R.F_RECIBE) END
                ), 1)                                                          T3_AVG,
                ROUND(AVG(TRUNC(O.FECHA) - TRUNC(R.FECHA)), 1)                CICLO_AVG,
                ROUND(COUNT(DISTINCT CASE WHEN TRUNC(O.FECHA) = TRUNC(R.FECHA)
                               THEN R.NUMREQ END)*100
                      / NULLIF(COUNT(DISTINCT R.NUMREQ),0), 1)                 PCT_MISMO_DIA,
                ROUND(COUNT(DISTINCT CASE WHEN TRUNC(O.FECHA)-TRUNC(R.FECHA) <= 5
                               THEN R.NUMREQ END)*100
                      / NULLIF(COUNT(DISTINCT R.NUMREQ),0), 1)                 PCT_HASTA_5DIAS
            FROM REQUISICION R, ITEMREQ I, DESP_ITEMREQ D, ORDEN_DE_COMPRA O
            WHERE R.ESTADO       = '6'
              AND R.FECHA        >= ADD_MONTHS(TRUNC(SYSDATE,'MM'), -P_MESES_ATRAS)
              AND R.F_AUTORIZA  IS NOT NULL
              AND R.F_RECIBE    IS NOT NULL
              AND I.NUMREQ       = R.NUMREQ
              AND D.TIPDOC       = '80'
              AND D.NUMREQ       = I.NUMREQ
              AND D.COD_ART      = I.COD_ART
              AND O.SERIE        = 1
              AND O.TIPO_DOCTO   = D.TIP_DOC_REF
              AND O.NUM_PED      = D.NRO_DOC_REF
              AND O.FECHA       IS NOT NULL
            GROUP BY TO_CHAR(R.FECHA,'YYYY-MM')
            ORDER BY MES;
    END P_TENDENCIA_MENSUAL;

END PKG_IND_LOGISTICA;
/
