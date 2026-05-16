/* ============================================================
   PKG_IND_LOGISTICA
   Módulo  : Logística — Indicadores / KPIs
   BD      : SIG (Oracle)
   Tablas  : REQUISICION, ITEMREQ, ARTICUL, DESP_ITEMREQ,
             ORDEN_DE_COMPRA, TABLAS_AUXILIARES,
             V_PERSONAL, V_CENTRO_DE_COSTOS, ACTIVO_FIJO
   Creado  : 15/05/2026

   CONVENCIONES DE VALIDACIÓN APLICADAS EN TODOS LOS CURSORES:
     · Campos numéricos  → NVL(I.CANTIDAD,0)  NVL(I.SALDO,0)
                           NVL(I.PRECIO,0)    NVL(R.IMPSTO,0)
                           Evita que un NULL en cualquier operando propague NULL
                           al resultado de SUM/producto sin dar error visible.
     · División por cero → NULLIF(denominador, 0)      evita ORA-01476
     · Porcentajes       → COUNT(DISTINCT CASE WHEN condicion THEN clave END)
                           / NULLIF(COUNT(DISTINCT clave), 0)
                           evita inflar % al contar filas de ítems en vez de reqs
     · Diferencias fecha → CASE WHEN fecha IS NOT NULL THEN ... END
                           en tramos donde la fecha puede ser NULL (T1/T2 en SUB-A).
                           El AVG ignora NULLs, produciendo el promedio correcto
                           solo sobre reqs que ya completaron ese tramo.
     · Valores negativos → GREATEST(fecha_fin - fecha_ini, 0)
                           evita días negativos por errores de captura en la BD
                           (fecha autorización anterior a fecha registro, etc.).
     · Montos con IGV    → DECODE(AFECTO_IGV,'S',
                             (NVL(I.CANTIDAD,0)*NVL(I.PRECIO,0))*(NVL(R.IMPSTO,0)+1),
                             (NVL(I.CANTIDAD,0)*NVL(I.PRECIO,0)))
                           NVL en los tres factores: cantidad, precio e IMPSTO.

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
         Una sola fila con promedios del ciclo usando CROSS JOIN
         de dos subqueries de una sola fila:

         SUB-A (REQUISICION sola): TOTAL_REQS, T1 y T2 sobre
         reqs no anuladas del período. Sin JOIN → sin duplicados.

         SUB-B (OC real, deduplicado 1 fila/req con MIN(O.FECHA)):
         Solo reqs ATENDIDAS (ESTADO='6') con F_RECIBE y O.FECHA
         conocidas. Misma fuente y granularidad que P_CICLO_VIDA
         y P_TENDENCIA_MENSUAL → coherencia garantizada.

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

       NOTA — 1 FILA POR REQUISICIÓN (GARANTIZADO):
         O.FECHA fue removido del GROUP BY. Si una req fue
         despachada a varias OCs, aparece UNA SOLA fila usando
         MIN(O.FECHA) = fecha de la primera OC generada.
         FCH_OC es coherente con P_TENDENCIA_MENSUAL (mismo MIN).

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

         ESTRATEGIA ANTI-DUPLICADOS:
           El JOIN a ITEMREQ+DESP_ITEMREQ+OC produce varias filas
           por req (una por cada ítem × despacho). Si se promedia
           directamente, reqs con muchos ítems pesan más en el AVG.
           Este cursor usa un subquery que agrupa por NUMREQ primero
           (dejando 1 fila por req con MIN(O.FECHA)) y luego agrega
           por mes — garantizando que cada req pese igual en el
           promedio, sin importar cuántos ítems tenga.

         CANT_REQS es COUNT(*) del subquery (ya 1 fila por req).
         PCT_MISMO_DIA y PCT_HASTA_5DIAS usan COUNT simple ya que
         el subquery garantiza 1 fila por req.

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
                    'A', F.DESCRIPCION,
                    I.DESTINO)                                            DESC_DESTINO,
                NVL(P.NOMBRE_CORTO,'(ex-empleado)')                          SOLICITA,
                R.OBSERVACION,
                I.COD_ART,
                DECODE(SUBSTR(I.COD_ART,1,6),
                    'PEDIDO', I.DETALLE,
                    A.DESCRIPCION)                                            DESC_ARTICULO,
                I.UNIDAD,
                I.CANTIDAD,
                GREATEST(NVL(I.CANTIDAD,0) - NVL(I.SALDO,0), 0)              CANT_DESP,
                NVL(I.SALDO,0)                                                SALDO,
                I.PRECIO                                                      PUNIT,
                (NVL(I.CANTIDAD,0) * NVL(I.PRECIO,0))                        SUB_TOTAL,
                DECODE(R.AFECTO_IGV,
                    'S', (NVL(I.CANTIDAD,0) * NVL(I.PRECIO,0)) * NVL(R.IMPSTO,0),
                    0)                                                        IGV,
                DECODE(R.AFECTO_IGV,
                    'S', (NVL(I.CANTIDAD,0) * NVL(I.PRECIO,0)) * (NVL(R.IMPSTO,0) + 1),
                    (NVL(I.CANTIDAD,0) * NVL(I.PRECIO,0)))                   TOTAL,
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
                AND P.C_CODIGO(+)    = I.COD_SOLICITA
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
                            'S', (NVL(I.CANTIDAD,0) * NVL(I.PRECIO,0)) * (NVL(R.IMPSTO,0) + 1),
                            (NVL(I.CANTIDAD,0) * NVL(I.PRECIO,0)))
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

        /* ── 2. TIEMPOS PROMEDIO DEL CICLO ─────────────────────────────────────────
           Patrón CROSS JOIN de dos subqueries de una sola fila:

           SUB-A (REQUISICION sola, sin JOIN a OC):
             Cuenta TOTAL_REQS (no anuladas) y calcula T1/T2 directamente
             desde REQUISICION. Sin riesgo de duplicados, máximo rendimiento.

           SUB-B (subquery deduplicado por NUMREQ con JOIN a OC):
             Solo reqs ATENDIDAS (ESTADO='6') con F_RECIBE y O.FECHA conocidas.
             GROUP BY NUMREQ + MIN(O.FECHA) → 1 fila por req, igual que
             P_TENDENCIA_MENSUAL y P_CICLO_VIDA. Coherencia garantizada.

           MOTIVO DEL CAMBIO:
             FCH_ENTREGA_LOGIST en REQUISICION solo se escribe en
             cancelaciones masivas (CambiarEstadoAsync ESTADO='9').
             Para reqs ATENDIDAS normalmente, el campo queda NULL y
             el promedio resultaba incorrecto o vacío.
        ─────────────────────────────────────────────────────────────────────────── */
        OPEN P_CUR_TIEMPOS FOR
            SELECT
                A.TOTAL_REQS,
                A.DIAS_REG_AUTORIZACION,
                A.DIAS_AUT_RECIBO,
                B.DIAS_RECIBO_OC,
                B.DIAS_CICLO_TOTAL
            FROM
                /* SUB-A: T1 y T2 desde REQUISICION sola — sin JOIN, sin duplicados */
                (
                    SELECT
                        COUNT(*)  TOTAL_REQS,
                        ROUND(AVG(
                            CASE WHEN F_AUTORIZA IS NOT NULL
                            THEN GREATEST(TRUNC(F_AUTORIZA) - TRUNC(FECHA), 0) END
                        ), 1)  DIAS_REG_AUTORIZACION,
                        ROUND(AVG(
                            CASE WHEN F_AUTORIZA IS NOT NULL AND F_RECIBE IS NOT NULL
                            THEN GREATEST(TRUNC(F_RECIBE) - TRUNC(F_AUTORIZA), 0) END
                        ), 1)  DIAS_AUT_RECIBO
                    FROM REQUISICION
                    WHERE TRUNC(FECHA) BETWEEN P_FECHA_DESDE AND P_FECHA_HASTA
                      AND ESTADO NOT IN ('9')
                      AND EXISTS (SELECT 1 FROM ITEMREQ II WHERE II.NUMREQ = REQUISICION.NUMREQ)
                ) A,
                /* SUB-B: T3 y CICLO desde OC real — deduplicado 1 fila/req */
                (
                    SELECT
                        ROUND(AVG(T3),    1)  DIAS_RECIBO_OC,
                        ROUND(AVG(CICLO), 1)  DIAS_CICLO_TOTAL
                    FROM (
                        SELECT
                            GREATEST(TRUNC(MIN(O.FECHA)) - TRUNC(R.F_RECIBE), 0)  T3,
                            GREATEST(TRUNC(MIN(O.FECHA)) - TRUNC(R.FECHA),     0)  CICLO
                        FROM
                            REQUISICION     R,
                            ITEMREQ         I,
                            DESP_ITEMREQ    D,
                            ORDEN_DE_COMPRA O
                        WHERE
                            TRUNC(R.FECHA) BETWEEN P_FECHA_DESDE AND P_FECHA_HASTA
                            AND R.ESTADO      = '6'
                            AND R.F_RECIBE   IS NOT NULL
                            AND I.NUMREQ      = R.NUMREQ
                            AND D.TIPDOC      = '80'
                            AND D.NUMREQ      = I.NUMREQ
                            AND D.COD_ART     = I.COD_ART
                            AND O.SERIE       = 1
                            AND O.TIPO_DOCTO  = D.TIP_DOC_REF
                            AND O.NUM_PED     = D.NRO_DOC_REF
                            AND O.FECHA      IS NOT NULL
                        GROUP BY R.NUMREQ, R.FECHA, R.F_RECIBE
                    )
                ) B;

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
                            'S', (NVL(I.CANTIDAD,0) * NVL(I.PRECIO,0)) * (NVL(R.IMPSTO,0) + 1),
                            (NVL(I.CANTIDAD,0) * NVL(I.PRECIO,0)))
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
                NVL(P.NOMBRE_CORTO,'(ex-empleado)')                           SOLICITA,
                I.COD_ART,
                DECODE(SUBSTR(I.COD_ART,1,6),
                    'PEDIDO', I.DETALLE,
                    A.DESCRIPCION)                                        DESC_ARTICULO,
                I.SALDO,
                ROUND(I.SALDO * NVL(I.PRECIO,0), 2)                      MONTO_PENDIENTE,
                GREATEST(
                    TRUNC(SYSDATE) - TRUNC(
                        CASE WHEN R.F_RECIBE IS NOT NULL
                        THEN R.F_RECIBE
                        ELSE R.FECHA END
                    )
                , 0)                                                          DIAS_EN_ESPERA
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
                AND P.C_CODIGO(+)   = I.COD_SOLICITA
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
        /* O.FECHA NO está en GROUP BY — se usa MIN(O.FECHA) en SELECT.
           Motivo: si una req fue despachada a 2 OCs con fechas distintas,
           incluir O.FECHA en GROUP BY produce 2 filas por req, rompiendo
           el Gantt y sesgando el histograma de ciclo.
           MIN(O.FECHA) = fecha de la OC más temprana = primera vez que
           la req fue procesada en una OC. Es la referencia correcta
           del ciclo y coherente con P_TENDENCIA_MENSUAL. */
        OPEN P_CURSOR FOR
            SELECT
                R.NUMREQ,
                DECODE(NVL(R.IND_SERV,'N'),'N','COMPRA','S','SERVICIO')  TIPO,
                MIN(D.NRO_DOC_REF) KEEP (DENSE_RANK FIRST ORDER BY O.FECHA)  NRO_OC,
                TRUNC(R.FECHA)                                            FCH_REGISTRO,
                TRUNC(R.F_AUTORIZA)                                       FCH_AUTORIZA,
                TRUNC(R.F_RECIBE)                                         FCH_RECIBO_LOG,
                TRUNC(MIN(O.FECHA))                                       FCH_OC,
                GREATEST(TRUNC(R.F_AUTORIZA) - TRUNC(R.FECHA),       0)     T1_REG_AUT,
                GREATEST(TRUNC(R.F_RECIBE)   - TRUNC(R.F_AUTORIZA),  0)     T2_AUT_REC,
                GREATEST(TRUNC(MIN(O.FECHA)) - TRUNC(R.F_RECIBE),    0)     T3_REC_OC,
                GREATEST(TRUNC(MIN(O.FECHA)) - TRUNC(R.FECHA),       0)     T_CICLO_TOTAL
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
                R.FECHA, R.F_AUTORIZA, R.F_RECIBE
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
            /* Subquery interno: 1 FILA POR NUMREQ.
               El JOIN a ITEMREQ+DESP_ITEMREQ+OC produce múltiples filas cuando
               una req tiene varios ítems o despachos. Si se promedia directamente,
               una req con 5 ítems pesa 5 veces más que una con 1 ítem.
               GROUP BY NUMREQ + MIN(O.FECHA) resuelve el problema antes de
               calcular promedios mensuales. */
            SELECT
                TO_CHAR(FCH_REG,'YYYY-MM')                                    MES,
                COUNT(*)                                                        CANT_REQS,
                ROUND(AVG(T1), 1)                                              T1_AVG,
                ROUND(AVG(T2), 1)                                              T2_AVG,
                ROUND(AVG(T3), 1)                                              T3_AVG,
                ROUND(AVG(CICLO), 1)                                           CICLO_AVG,
                -- PCT_MISMO_DIA: % de reqs cuya OC se emitió el mismo día del registro
                ROUND(COUNT(CASE WHEN CICLO = 0 THEN 1 END) * 100
                      / NULLIF(COUNT(*), 0), 1)                                PCT_MISMO_DIA,
                -- PCT_HASTA_5DIAS: todas las filas del subquery tienen OC (O.FECHA IS NOT NULL
                -- está en el WHERE interior), por tanto denominador = COUNT(*) correcto.
                ROUND(COUNT(CASE WHEN CICLO <= 5 THEN 1 END) * 100
                      / NULLIF(COUNT(*), 0), 1)                                PCT_HASTA_5DIAS
            FROM (
                SELECT
                    R.NUMREQ,
                    R.FECHA                                        FCH_REG,
                    GREATEST(TRUNC(R.F_AUTORIZA) - TRUNC(R.FECHA),       0)  T1,
                    GREATEST(TRUNC(R.F_RECIBE)   - TRUNC(R.F_AUTORIZA),  0)  T2,
                    GREATEST(TRUNC(MIN(O.FECHA)) - TRUNC(R.F_RECIBE),    0)  T3,
                    GREATEST(TRUNC(MIN(O.FECHA)) - TRUNC(R.FECHA),       0)  CICLO
                FROM REQUISICION R, ITEMREQ I, DESP_ITEMREQ D, ORDEN_DE_COMPRA O
                WHERE R.ESTADO       = '6'
                  AND R.FECHA        >= ADD_MONTHS(TRUNC(SYSDATE,'MM'), -P_MESES_ATRAS)
                  AND R.FECHA         < TRUNC(SYSDATE,'MM')
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
                GROUP BY
                    R.NUMREQ, R.FECHA, R.F_AUTORIZA, R.F_RECIBE
            )
            GROUP BY TO_CHAR(FCH_REG,'YYYY-MM')
            ORDER BY MES;
    END P_TENDENCIA_MENSUAL;

END PKG_IND_LOGISTICA;
/
