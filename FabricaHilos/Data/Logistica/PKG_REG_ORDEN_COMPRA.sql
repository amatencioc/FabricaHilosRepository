/* ============================================================
   PKG_REG_ORDEN_COMPRA
   Módulo  : Logística — Registro de Orden de Compra / Servicio
   BD      : SIG (Oracle)
   Tablas  : ORDEN_DE_COMPRA, ITEMORD, DESP_ITEMREQ,
             REQUISICION, ITEMREQ
   Creado  : 11/05/2026

   FLUJO:
     1. Consultar reqs pendientes  → P_OBTENER_REQUISICIONES
     2. Consultar ítems de un req  → P_OBTENER_ITEMS_REQ
     3. Usuario selecciona ítems y completa cabecera de O/C
        (mismos COD_ART de distintos reqs se unifican sumando)
     4. Registrar O/C              → P_REGISTRAR_OC
        · INSERT ORDEN_DE_COMPRA (ESTADO='0' = EMITIDA)
        · INSERT ITEMORD  (1 fila por COD_ART único, cant. sumada)
        · INSERT DESP_ITEMREQ (1 fila por req-item original)
        · UPDATE ITEMREQ.SALDO    (descuenta la cant. despachada)
        · UPDATE REQUISICION.ESTADO='6' si queda SALDO total = 0
     5. Anular O/C (solo ESTADO='0') → P_ANULAR_OC
        · Restaura ITEMREQ.SALDO
        · Elimina DESP_ITEMREQ
        · Reactiva REQUISICION cerrada
   ============================================================ */

-- ==============================================================
--  ESPECIFICACIÓN
-- ==============================================================
CREATE OR REPLACE PACKAGE PKG_REG_ORDEN_COMPRA AS

    -- ── Tipos públicos ──────────────────────────────────────────

    /*  Un registro por cada ítem seleccionado del requerimiento.
        Cuando dos ítems comparten COD_ART (ya sea del mismo req
        o de reqs distintos) el paquete los unifica automáticamente
        sumando CANTIDAD y recalculando IMP_VVTA. */
    TYPE T_ITEM_SEL IS RECORD (
        TIPDOC      REQUISICION.TIPDOC%TYPE,     -- tipo doc req ('80')
        SERIE       REQUISICION.SERIE%TYPE,
        NUMREQ      REQUISICION.NUMREQ%TYPE,
        ORDEN       ITEMREQ.ORDEN%TYPE,           -- línea dentro del req
        COD_ART     ITEMREQ.COD_ART%TYPE,
        DETALLE     ITEMREQ.DETALLE%TYPE,         -- descripción del ítem
        UNIDAD      ITEMREQ.UNIDAD%TYPE,
        COD_ORIG    ITEMORD.COD_ORIG%TYPE,        -- cód. artículo del proveedor
        CANTIDAD    ITEMREQ.CANTIDAD%TYPE,        -- cantidad a despachar
        PRECIO      ITEMORD.PRECIO%TYPE,          -- precio unitario (proveedor)
        POR_DESC1   ITEMORD.POR_DESC1%TYPE,       -- % descuento 1 (p.ej. 5.00)
        POR_DESC2   ITEMORD.POR_DESC2%TYPE,       -- % descuento 2
        TP_DESTINO  ITEMREQ.TP_DESTINO%TYPE,      -- 'U'=unidad  'A'=área
        DESTINO     ITEMREQ.DESTINO%TYPE,
        C_CODIGO    ITEMORD.C_CODIGO%TYPE         -- responsable del ítem
    );

    TYPE T_ITEMS IS TABLE OF T_ITEM_SEL INDEX BY PLS_INTEGER;

    TYPE T_CURSOR IS REF CURSOR;

    -- ── Consultas ───────────────────────────────────────────────

    /*  Lista de requerimientos pendientes (ESTADO='1' Visado o
        '2' Recibido) que tienen al menos un ítem con SALDO > 0.
        Incluye conteo de ítems pendientes por req. */
    PROCEDURE P_OBTENER_REQUISICIONES (
        P_CURSOR OUT T_CURSOR
    );

    /*  Ítems con SALDO > 0 de un requerimiento específico.
        Incluye descripción del artículo desde ARTICUL y descripción
        del destino (DESC_DESTINO) desde CENTRO_DE_COSTOS o ACTIVO_FIJO
        según TP_DESTINO ('U' o 'A'). */
    PROCEDURE P_OBTENER_ITEMS_REQ (
        P_TIPDOC IN  REQUISICION.TIPDOC%TYPE,
        P_SERIE  IN  REQUISICION.SERIE%TYPE,
        P_NUMREQ IN  REQUISICION.NUMREQ%TYPE,
        P_CURSOR OUT T_CURSOR
    );

    -- ── Registro ────────────────────────────────────────────────

    /*  Genera una nueva Orden de Compra / Servicio.
        P_TIPO_DOCTO : '82' = Orden de Compra  (PARAMLG.DOCORDE)
                       '83' = Orden de Servicio (PARAMLG.DOCSERV)
        P_IMPSTO     : fracción decimal (0.18 = 18 % IGV)
        P_ITEMS      : colección de ítems seleccionados; el paquete
                       agrupa los de igual COD_ART en un solo ITEMORD.
        SERIE siempre = 1 (asignada por el sistema).
        NUM_PED se obtiene de PARAMLG.NUMORDE / NUMSERV y se incrementa.
        Retorna P_NUM_PED con el número asignado, P_MSGERROR=NULL si OK. */
    PROCEDURE P_REGISTRAR_OC (
        P_TIPO_DOCTO  IN  ORDEN_DE_COMPRA.TIPO_DOCTO%TYPE,
        P_FECHA       IN  ORDEN_DE_COMPRA.FECHA%TYPE,
        P_F_ENTREGA   IN  ORDEN_DE_COMPRA.F_ENTREGA%TYPE,
        P_COD_PROVEED IN  ORDEN_DE_COMPRA.COD_PROVEED%TYPE,
        P_COND_PAG    IN  ORDEN_DE_COMPRA.COND_PAG%TYPE,
        P_MONEDA      IN  ORDEN_DE_COMPRA.MONEDA%TYPE,      -- 'S' o 'D'
        P_IMPSTO      IN  ORDEN_DE_COMPRA.IMPSTO%TYPE,      -- 0.18
        P_C_COSTO     IN  ORDEN_DE_COMPRA.C_COSTO%TYPE,
        P_DETALLE     IN  ORDEN_DE_COMPRA.DETALLE%TYPE,
        P_OPC_LENTR   IN  ORDEN_DE_COMPRA.OPC_LENTR%TYPE,  -- '1'=DIRECCION ACTUAL  '2'=OTRO LOCAL
        P_L_ENTREGA   IN  ORDEN_DE_COMPRA.L_ENTREGA%TYPE,   -- dirección libre (solo si OPC_LENTR='2')
        P_C_CODIGO    IN  ORDEN_DE_COMPRA.C_CODIGO%TYPE,    -- responsable O/C
        P_USUARIO     IN  VARCHAR2,                         -- usuario del sistema
        P_ITEMS       IN  T_ITEMS,
        P_NUM_PED     OUT ORDEN_DE_COMPRA.NUM_PED%TYPE,
        P_MSGERROR    OUT VARCHAR2
    );

    /*  Lista de destinos filtrable.
        TP_DESTINO = 'U' → Centros de Costo (CENTRO_DE_COSTOS.CENTRO_COSTO / NOMBRE)
        TP_DESTINO = 'A' → Activos Fijos    (ACTIVO_FIJO.CODIGO + '-' + NUMERO / DESCRIPCION)
        P_TIPO   : 'U', 'A' o NULL (ambos).
        P_BUSCAR : texto libre; filtra por código y descripción (LIKE insensible). */
    PROCEDURE P_OBTENER_DESTINOS (
        P_TIPO   IN  VARCHAR2 DEFAULT NULL,
        P_BUSCAR IN  VARCHAR2 DEFAULT NULL,
        P_CURSOR OUT T_CURSOR
    );

    /*  Lista de proveedores activos (ESTADO='0').
        Búsqueda opcional por nombre o código (LIKE insensible a mayúsculas). */
    PROCEDURE P_OBTENER_PROVEEDORES (
        P_BUSCAR IN  VARCHAR2 DEFAULT NULL,
        P_CURSOR OUT T_CURSOR
    );

    /*  Opciones de lugar de entrega (lista fija).
        OPC_LENTR = '1' -> DIRECCION ACTUAL (L_ENTREGA vacío)
        OPC_LENTR = '2' -> OTRO LOCAL      (L_ENTREGA = dirección libre) */
    PROCEDURE P_OBTENER_OPC_ENTREGA (
        P_CURSOR OUT T_CURSOR
    );

    /*  Condiciones de pago activas (FLAG_EST='S'). */
    PROCEDURE P_OBTENER_CONDPAG (
        P_CURSOR OUT T_CURSOR
    );

    /*  Valores de IGV / tasa de impuesto para el combo del formulario.
        Lee desde la tabla IMPUESTO y agrega el valor -0.10 (descuento/NC)
        ordenado por VALOR. Retorna: CODIGO, DESCRIPCION, VALOR. */
    PROCEDURE P_OBTENER_IGV (
        P_CURSOR OUT T_CURSOR
    );

    /*  Anula una O/C que esté en ESTADO='0' (EMITIDA).
        Restaura el SALDO de cada ITEMREQ afectado,
        elimina los registros en DESP_ITEMREQ y reactiva
        los requerimientos que hubieran quedado cerrados. */
    PROCEDURE P_ANULAR_OC (
        P_TIPO_DOCTO IN  ORDEN_DE_COMPRA.TIPO_DOCTO%TYPE,
        P_SERIE      IN  ORDEN_DE_COMPRA.SERIE%TYPE,
        P_NUM_PED    IN  ORDEN_DE_COMPRA.NUM_PED%TYPE,
        P_USUARIO    IN  VARCHAR2,
        P_MSGERROR   OUT VARCHAR2
    );

END PKG_REG_ORDEN_COMPRA;
/


-- ==============================================================
--  CUERPO
-- ==============================================================
CREATE OR REPLACE PACKAGE BODY PKG_REG_ORDEN_COMPRA AS

    -- ── Constantes privadas ─────────────────────────────────────
    /*  Valor especial de IGV: descuento / nota de crédito.
        Se usa en P_OBTENER_IGV (para mostrarlo en el combo) y en
        P_REGISTRAR_OC (para validar que el valor recibido sea válido).
        Cambiar aquí actualiza automáticamente ambos puntos. */
    C_IGV_ESPECIAL CONSTANT NUMBER := -0.10;

    -- ── P_OBTENER_REQUISICIONES ─────────────────────────────────
    PROCEDURE P_OBTENER_REQUISICIONES (
        P_CURSOR OUT T_CURSOR
    ) IS
    BEGIN
        OPEN P_CURSOR FOR
            SELECT R.TIPDOC,
                   R.SERIE,
                   R.NUMREQ,
                   R.CENTRO_COSTO,
                   R.PROVEEDORES,
                   R.FECHA,
                   R.F_ENTREGA,
                   R.RESPONSABLE,
                   R.PRIORIDAD,           -- '01'=URGENTE  '02'=CORRIENTE
                   R.OBSERVACION,
                   R.ESTADO,              -- '1'=Visado  '2'=Recibido
                   R.DESTINO,
                   R.IND_SERV,            -- 'S'=servicio  'N'=bien
                   R.AUTORIZA,
                   R.F_AUTORIZA,
                   R.A_ADUSER,
                   R.A_ADFECHA,
                   (SELECT COUNT(*)
                    FROM   ITEMREQ I
                    WHERE  I.TIPDOC = R.TIPDOC
                      AND  I.SERIE  = R.SERIE
                      AND  I.NUMREQ = R.NUMREQ) AS TOTAL_ITEMS,
                   (SELECT COUNT(*)
                    FROM   ITEMREQ I
                    WHERE  I.TIPDOC = R.TIPDOC
                      AND  I.SERIE  = R.SERIE
                      AND  I.NUMREQ = R.NUMREQ
                      AND  I.SALDO  > 0)        AS ITEMS_PENDIENTES
            FROM   REQUISICION R
            WHERE  R.ESTADO = '2'        -- solo RECIBIDO (llegó a Logística)
              AND  EXISTS (
                       SELECT 1
                       FROM   ITEMREQ I
                       WHERE  I.TIPDOC = R.TIPDOC
                         AND  I.SERIE  = R.SERIE
                         AND  I.NUMREQ = R.NUMREQ
                         AND  I.SALDO  > 0
                   )
            ORDER BY R.PRIORIDAD ASC, R.FECHA DESC, R.NUMREQ DESC;
    END P_OBTENER_REQUISICIONES;

    -- ── P_OBTENER_PROVEEDORES ───────────────────────────────────
    PROCEDURE P_OBTENER_PROVEEDORES (
        P_BUSCAR IN  VARCHAR2 DEFAULT NULL,
        P_CURSOR OUT T_CURSOR
    ) IS
    BEGIN
        IF P_BUSCAR IS NULL OR TRIM(P_BUSCAR) IS NULL THEN
            OPEN P_CURSOR FOR
                SELECT COD_PROVEED, NOMBRE, RUC, DIRECCION, TELEFONO
                FROM   PROVEED
                WHERE  ESTADO = '0'        -- activos (0=activo, 9=inactivo)
                ORDER BY NOMBRE;
        ELSE
            OPEN P_CURSOR FOR
                SELECT COD_PROVEED, NOMBRE, RUC, DIRECCION, TELEFONO
                FROM   PROVEED
                WHERE  ESTADO = '0'
                  AND  (UPPER(NOMBRE)      LIKE '%' || UPPER(TRIM(P_BUSCAR)) || '%'
                    OR  UPPER(COD_PROVEED) LIKE '%' || UPPER(TRIM(P_BUSCAR)) || '%'
                    OR  UPPER(RUC)         LIKE '%' || UPPER(TRIM(P_BUSCAR)) || '%')
                ORDER BY NOMBRE;
        END IF;
    END P_OBTENER_PROVEEDORES;

    PROCEDURE P_OBTENER_OPC_ENTREGA (
        P_CURSOR OUT T_CURSOR
    ) IS
    BEGIN
        OPEN P_CURSOR FOR
            SELECT '1'       AS OPC_LENTR,
                   'DIRECCION ACTUAL' AS DESCRIPCION,
                   DIRECCION AS L_ENTREGA_REF    -- dirección real de la empresa
            FROM   RH_EMPRESAS
            WHERE  ROWNUM = 1
            UNION ALL
            SELECT '2', 'OTRO LOCAL', NULL FROM DUAL;
    END P_OBTENER_OPC_ENTREGA;

    PROCEDURE P_OBTENER_CONDPAG (
        P_CURSOR OUT T_CURSOR
    ) IS
    BEGIN
        OPEN P_CURSOR FOR
            SELECT COND_PAG, DESCRIPCION
            FROM   CONDPAG
            WHERE  FLAG_EST = 'S'   -- activas
            ORDER BY DESCRIPCION;
    END P_OBTENER_CONDPAG;

    PROCEDURE P_OBTENER_ITEMS_REQ (
        P_TIPDOC IN  REQUISICION.TIPDOC%TYPE,
        P_SERIE  IN  REQUISICION.SERIE%TYPE,
        P_NUMREQ IN  REQUISICION.NUMREQ%TYPE,
        P_CURSOR OUT T_CURSOR
    ) IS
    BEGIN
        OPEN P_CURSOR FOR
            SELECT I.TIPDOC,
                   I.SERIE,
                   I.NUMREQ,
                   I.ORDEN,
                   I.COD_ART,
                   I.DETALLE,
                   I.UNIDAD,
                   I.CANTIDAD,
                   I.SALDO,               -- max que puede despacharse
                   I.MONEDA,
                   I.PRECIO,              -- precio referencial del req
                   I.TP_DESTINO,
                   I.DESTINO,
                   CASE
                       WHEN I.TP_DESTINO = 'U' THEN
                           (SELECT CC.NOMBRE
                            FROM   CENTRO_DE_COSTOS CC
                            WHERE  CC.CENTRO_COSTO = I.DESTINO
                              AND  CC.TIPO         = 'D'
                              AND  CC.ESTADO      <> '9')
                       WHEN I.TP_DESTINO = 'A' THEN
                           (SELECT AF.DESCRIPCION
                            FROM   ACTIVO_FIJO AF
                            WHERE  AF.CODIGO || '-' || TO_CHAR(AF.NUMERO) = I.DESTINO
                              AND  AF.ESTADO NOT IN ('2', '9'))
                   END AS DESC_DESTINO,
                   I.COD_SOLICITA,
                   I.MARCA,
                   I.STK_MIN,
                   I.STK_HIST,
                   I.OBSERVACIONES,
                   I.ID_GRUPO,
                   I.F_APROBADO,
                   A.DESCRIPCION AS DESC_ARTICULO,
                   -- indica si ya tiene O/C previa (despacho parcial)
                   (SELECT NVL(MAX(D.NRO_DOC_REF), 0)
                    FROM   DESP_ITEMREQ D
                    WHERE  D.TIPDOC = I.TIPDOC
                      AND  D.SERIE  = I.SERIE
                      AND  D.NUMREQ = I.NUMREQ
                      AND  D.ORDEN  = I.ORDEN) AS NUM_OC_PREVIO
            FROM   ITEMREQ I
            LEFT JOIN ARTICUL A ON A.COD_ART = I.COD_ART
            WHERE  I.TIPDOC = P_TIPDOC
              AND  I.SERIE  = P_SERIE
              AND  I.NUMREQ = P_NUMREQ
              AND  I.SALDO  > 0
            ORDER BY I.ORDEN;
    END P_OBTENER_ITEMS_REQ;

    -- ── P_REGISTRAR_OC ──────────────────────────────────────────
    PROCEDURE P_REGISTRAR_OC (
        P_TIPO_DOCTO  IN  ORDEN_DE_COMPRA.TIPO_DOCTO%TYPE,
        P_FECHA       IN  ORDEN_DE_COMPRA.FECHA%TYPE,
        P_F_ENTREGA   IN  ORDEN_DE_COMPRA.F_ENTREGA%TYPE,
        P_COD_PROVEED IN  ORDEN_DE_COMPRA.COD_PROVEED%TYPE,
        P_COND_PAG    IN  ORDEN_DE_COMPRA.COND_PAG%TYPE,
        P_MONEDA      IN  ORDEN_DE_COMPRA.MONEDA%TYPE,
        P_IMPSTO      IN  ORDEN_DE_COMPRA.IMPSTO%TYPE,
        P_C_COSTO     IN  ORDEN_DE_COMPRA.C_COSTO%TYPE,
        P_DETALLE     IN  ORDEN_DE_COMPRA.DETALLE%TYPE,
        P_OPC_LENTR   IN  ORDEN_DE_COMPRA.OPC_LENTR%TYPE,
        P_L_ENTREGA   IN  ORDEN_DE_COMPRA.L_ENTREGA%TYPE,
        P_C_CODIGO    IN  ORDEN_DE_COMPRA.C_CODIGO%TYPE,
        P_USUARIO     IN  VARCHAR2,
        P_ITEMS       IN  T_ITEMS,
        P_NUM_PED     OUT ORDEN_DE_COMPRA.NUM_PED%TYPE,
        P_MSGERROR    OUT VARCHAR2
    ) IS

        /* ── Tipo interno: ítem unificado para ITEMORD ── */
        TYPE T_ITEM_OC IS RECORD (
            ORD_ITEM     PLS_INTEGER,
            COD_ART      ITEMORD.COD_ART%TYPE,
            COD_ORIG     ITEMORD.COD_ORIG%TYPE,
            UNIDAD       ITEMORD.UNIDAD%TYPE,
            DESCRIPCION  ITEMORD.DESCRIPCION%TYPE,
            CANTIDAD     ITEMORD.CANTIDAD%TYPE,
            PRECIO       ITEMORD.PRECIO%TYPE,
            POR_DESC1    ITEMORD.POR_DESC1%TYPE,
            POR_DESC2    ITEMORD.POR_DESC2%TYPE,
            IMP_VVTA     ITEMORD.IMP_VVTA%TYPE,   -- precio neto * cant
            IGV_ITEM     ITEMORD.IGV%TYPE,         -- IMP_VVTA * P_IMPSTO
            TIPO_DESTINO ITEMORD.TIPO_DESTINO%TYPE,
            COD_DESTINO  ITEMORD.COD_DESTINO%TYPE,
            C_CODIGO     ITEMORD.C_CODIGO%TYPE
        );
        -- Mapa indexado por COD_ART para la fusión
        TYPE T_MERGE_MAP IS TABLE OF T_ITEM_OC INDEX BY VARCHAR2(25);

        V_MERGE       T_MERGE_MAP;
        V_KEY         VARCHAR2(25);
        V_ORD_CTR     PLS_INTEGER  := 0;
        V_NUM_PED     ORDEN_DE_COMPRA.NUM_PED%TYPE;

        /* totales de cabecera O/C */
        V_VAL_VENTA   NUMBER(14,2) := 0;
        V_IMP_NETO    NUMBER(14,2) := 0;
        V_IMP_IGV     NUMBER(14,2) := 0;
        V_PRECIO_VTA  NUMBER(14,2) := 0;

        /* aux cálculo por ítem */
        V_PRECIO_NETO NUMBER;
        V_IMP_VVTA    NUMBER;
        V_IGV_ITEM    NUMBER;

        /* validaciones */
        V_SALDO_ACT   ITEMREQ.SALDO%TYPE;
        V_EST_REQ     REQUISICION.ESTADO%TYPE;
        V_CNT         NUMBER;
        V_ORD_REF     ITEMORD.ORDEN%TYPE;

        /* para detectar reqs ya revisados al cerrar */
        TYPE T_REQS_PROC IS TABLE OF NUMBER INDEX BY VARCHAR2(20);
        V_REQS_PROC   T_REQS_PROC;
        V_CLAVE_REQ   VARCHAR2(20);
        V_SALDO_TOTAL NUMBER;

        /* para detectar ítems duplicados en P_ITEMS (evita saldo negativo) */
        TYPE T_ITEM_KEYS IS TABLE OF NUMBER INDEX BY VARCHAR2(50);
        V_ITEM_KEYS   T_ITEM_KEYS;
        V_ITEM_KEY    VARCHAR2(50);

        /* tipo, serie y número asignados por el sistema */
        V_SERIE       NUMBER := 1;          -- serie siempre = 1
        V_DOCORDE     PARAMLG.DOCORDE%TYPE; -- '82'
        V_DOCSERV     PARAMLG.DOCSERV%TYPE; -- '83'

    BEGIN
        P_MSGERROR := NULL;
        P_NUM_PED  := NULL;

        -- ── 1. Validaciones de entrada ──────────────────────────

        IF P_ITEMS.COUNT = 0 THEN
            P_MSGERROR := 'Debe seleccionar al menos un ítem del requerimiento.';
            RETURN;
        END IF;

        -- Obtener tipos válidos desde PARAMLG
        SELECT DOCORDE, DOCSERV INTO V_DOCORDE, V_DOCSERV FROM PARAMLG;

        IF P_TIPO_DOCTO NOT IN (V_DOCORDE, V_DOCSERV) THEN
            P_MSGERROR := 'TIPO_DOCTO inválido. Use '''||V_DOCORDE||''' (Orden de Compra) o '''||V_DOCSERV||''' (Orden de Servicio).';
            RETURN;
        END IF;

        IF P_FECHA IS NULL OR P_F_ENTREGA IS NULL THEN
            P_MSGERROR := 'Fecha y Fecha de Entrega son obligatorias.';
            RETURN;
        END IF;

        -- Proveedor
        SELECT COUNT(*) INTO V_CNT FROM PROVEED WHERE COD_PROVEED = P_COD_PROVEED;
        IF V_CNT = 0 THEN
            P_MSGERROR := 'Proveedor ' || P_COD_PROVEED || ' no existe.';
            RETURN;
        END IF;

        -- Condición de pago
        SELECT COUNT(*) INTO V_CNT FROM CONDPAG WHERE COND_PAG = P_COND_PAG;
        IF V_CNT = 0 THEN
            P_MSGERROR := 'Condición de pago ' || P_COND_PAG || ' no existe.';
            RETURN;
        END IF;

        -- Moneda
        IF P_MONEDA NOT IN ('S', 'D') THEN
            P_MSGERROR := 'Moneda inválida. Use ''S'' (Soles) o ''D'' (Dólares).';
            RETURN;
        END IF;

        -- IGV: debe existir en IMPUESTO o ser el valor especial (C_IGV_ESPECIAL)
        SELECT COUNT(*) INTO V_CNT
        FROM (
            SELECT VALOR FROM IMPUESTO
            UNION ALL
            SELECT C_IGV_ESPECIAL FROM DUAL
        )
        WHERE VALOR = P_IMPSTO;
        IF V_CNT = 0 THEN
            P_MSGERROR := 'IMPSTO inválido (' || P_IMPSTO || '). Use un valor de la lista de IGV.';
            RETURN;
        END IF;

        -- ── 2. Validar cada ítem: estado del req y saldo disponible ─
        /*  También detecta ítems duplicados en la colección P_ITEMS.
            Sin esta guarda, dos entradas con el mismo (TIPDOC, SERIE,
            NUMREQ, ORDEN, COD_ART) pasarían la validación individual
            pero luego el UPDATE de ITEMREQ.SALDO se ejecutaría dos
            veces, dejando el saldo negativo.                           */

        FOR I IN 1 .. P_ITEMS.COUNT LOOP

            IF P_ITEMS(I).CANTIDAD <= 0 THEN
                P_MSGERROR := 'La cantidad del ítem ' || P_ITEMS(I).COD_ART
                              || ' (REQ ' || P_ITEMS(I).NUMREQ || ') debe ser mayor a cero.';
                RETURN;
            END IF;

            -- Detectar ítem duplicado dentro de P_ITEMS
            V_ITEM_KEY := P_ITEMS(I).TIPDOC  || '|'
                       || P_ITEMS(I).SERIE   || '|'
                       || P_ITEMS(I).NUMREQ  || '|'
                       || P_ITEMS(I).ORDEN   || '|'
                       || P_ITEMS(I).COD_ART;
            IF V_ITEM_KEYS.EXISTS(V_ITEM_KEY) THEN
                P_MSGERROR := 'Ítem duplicado en la selección: '
                              || P_ITEMS(I).COD_ART
                              || ' (REQ ' || P_ITEMS(I).NUMREQ
                              || ' / ORDEN ' || P_ITEMS(I).ORDEN
                              || '). Combine las cantidades en una sola fila.';
                RETURN;
            END IF;
            V_ITEM_KEYS(V_ITEM_KEY) := I;

            -- Estado del requerimiento
            BEGIN
                SELECT ESTADO
                INTO   V_EST_REQ
                FROM   REQUISICION
                WHERE  TIPDOC = P_ITEMS(I).TIPDOC
                  AND  SERIE  = P_ITEMS(I).SERIE
                  AND  NUMREQ = P_ITEMS(I).NUMREQ;
            EXCEPTION
                WHEN NO_DATA_FOUND THEN
                    P_MSGERROR := 'La requisición ' || P_ITEMS(I).NUMREQ || ' no existe.';
                    RETURN;
            END;

            IF V_EST_REQ NOT IN ('1', '2') THEN
                P_MSGERROR := 'La requisición ' || P_ITEMS(I).NUMREQ
                              || ' no está en estado válido para generar O/C'
                              || ' (estado actual: ' || V_EST_REQ || ').';
                RETURN;
            END IF;

            -- Saldo disponible del ítem
            BEGIN
                SELECT SALDO
                INTO   V_SALDO_ACT
                FROM   ITEMREQ
                WHERE  TIPDOC  = P_ITEMS(I).TIPDOC
                  AND  SERIE   = P_ITEMS(I).SERIE
                  AND  NUMREQ  = P_ITEMS(I).NUMREQ
                  AND  ORDEN   = P_ITEMS(I).ORDEN
                  AND  COD_ART = P_ITEMS(I).COD_ART;
            EXCEPTION
                WHEN NO_DATA_FOUND THEN
                    P_MSGERROR := 'Ítem ' || P_ITEMS(I).COD_ART
                                  || ' (REQ ' || P_ITEMS(I).NUMREQ
                                  || ' / ORDEN ' || P_ITEMS(I).ORDEN || ') no existe.';
                    RETURN;
            END;

            IF P_ITEMS(I).CANTIDAD > V_SALDO_ACT THEN
                P_MSGERROR := 'La cantidad a despachar (' || P_ITEMS(I).CANTIDAD
                              || ') supera el saldo disponible (' || V_SALDO_ACT
                              || ') del ítem ' || P_ITEMS(I).COD_ART
                              || ' (REQ ' || P_ITEMS(I).NUMREQ || ').';
                RETURN;
            END IF;

        END LOOP;

        -- ── 3. Siguiente número de OC (desde PARAMLG, con FOR UPDATE) ──
        /*  SELECT FOR UPDATE bloquea la fila de PARAMLG durante la
            transacción, evitando que dos sesiones simultáneas obtengan
            el mismo número. El UPDATE al final del paso 10 incrementa
            el contador para la siguiente O/C.                          */
        IF P_TIPO_DOCTO = V_DOCORDE THEN
            SELECT NUMORDE INTO V_NUM_PED FROM PARAMLG FOR UPDATE;
        ELSE
            SELECT NUMSERV INTO V_NUM_PED FROM PARAMLG FOR UPDATE;
        END IF;

        -- ── 4. Construir mapa de ítems unificados (merge por COD_ART) ──
        /*  Regla: si dos ítems (del mismo req o de reqs distintos)
            tienen el mismo COD_ART, se agrupan en una sola línea
            de ITEMORD sumando sus cantidades.
            El PRECIO se toma del primer registro del grupo (el usuario
            fija un único precio por artículo en el grid de la O/C). */

        FOR I IN 1 .. P_ITEMS.COUNT LOOP
            V_KEY := P_ITEMS(I).COD_ART;

            -- Precio neto unitario con descuentos
            V_PRECIO_NETO := P_ITEMS(I).PRECIO
                             * (1 - P_ITEMS(I).POR_DESC1 / 100)
                             * (1 - P_ITEMS(I).POR_DESC2 / 100);

            IF V_MERGE.EXISTS(V_KEY) THEN
                -- Ya existe: acumular cantidad y recalcular IMP_VVTA
                V_MERGE(V_KEY).CANTIDAD := V_MERGE(V_KEY).CANTIDAD + P_ITEMS(I).CANTIDAD;

                V_MERGE(V_KEY).IMP_VVTA :=
                    ROUND(V_MERGE(V_KEY).CANTIDAD
                          * V_MERGE(V_KEY).PRECIO
                          * (1 - V_MERGE(V_KEY).POR_DESC1 / 100)
                          * (1 - V_MERGE(V_KEY).POR_DESC2 / 100), 2);

                V_MERGE(V_KEY).IGV_ITEM :=
                    ROUND(V_MERGE(V_KEY).IMP_VVTA * P_IMPSTO, 2);
            ELSE
                -- Primer ítem con este COD_ART: crear nueva línea
                V_ORD_CTR := V_ORD_CTR + 1;
                V_IMP_VVTA := ROUND(P_ITEMS(I).CANTIDAD * V_PRECIO_NETO, 2);
                V_IGV_ITEM := ROUND(V_IMP_VVTA * P_IMPSTO, 2);

                V_MERGE(V_KEY).ORD_ITEM     := V_ORD_CTR;
                V_MERGE(V_KEY).COD_ART      := P_ITEMS(I).COD_ART;
                V_MERGE(V_KEY).COD_ORIG     := P_ITEMS(I).COD_ORIG;
                V_MERGE(V_KEY).UNIDAD       := P_ITEMS(I).UNIDAD;
                V_MERGE(V_KEY).DESCRIPCION  := P_ITEMS(I).DETALLE;
                V_MERGE(V_KEY).CANTIDAD     := P_ITEMS(I).CANTIDAD;
                V_MERGE(V_KEY).PRECIO       := P_ITEMS(I).PRECIO;
                V_MERGE(V_KEY).POR_DESC1    := P_ITEMS(I).POR_DESC1;
                V_MERGE(V_KEY).POR_DESC2    := P_ITEMS(I).POR_DESC2;
                V_MERGE(V_KEY).IMP_VVTA     := V_IMP_VVTA;
                V_MERGE(V_KEY).IGV_ITEM     := V_IGV_ITEM;
                V_MERGE(V_KEY).TIPO_DESTINO := P_ITEMS(I).TP_DESTINO;
                V_MERGE(V_KEY).COD_DESTINO  := P_ITEMS(I).DESTINO;
                V_MERGE(V_KEY).C_CODIGO     := P_ITEMS(I).C_CODIGO;
            END IF;
        END LOOP;

        -- ── 5. Calcular totales de cabecera ──────────────────────
        /*  IMPORTE      = suma de IMP_VVTA (subtotal sin descuento global)
            DESCUENTO    = 0 (descuentos ya aplicados a nivel de ítem)
            VALOR VENTA  = IMPORTE - DESCUENTO
            I.G.V.       = VALOR VENTA * P_IMPSTO
            TOTAL        = VALOR VENTA + I.G.V.              */

        V_VAL_VENTA := 0;
        V_KEY := V_MERGE.FIRST;
        WHILE V_KEY IS NOT NULL LOOP
            V_VAL_VENTA := V_VAL_VENTA + V_MERGE(V_KEY).IMP_VVTA;
            V_KEY := V_MERGE.NEXT(V_KEY);
        END LOOP;

        V_IMP_NETO   := V_VAL_VENTA;                          -- sin descuento global
        V_IMP_IGV    := ROUND(V_IMP_NETO * P_IMPSTO, 2);
        V_PRECIO_VTA := V_IMP_NETO + V_IMP_IGV;

        -- ── 6. INSERT ORDEN_DE_COMPRA ────────────────────────────
        /*  ESTADO = '0' (EMITIDA) al momento de la creación.
            Avanza a '6' (CERRADA) cuando todos los ítems
            de ITEMORD tienen SALDO = 0 (recepción de almacén). */

        INSERT INTO ORDEN_DE_COMPRA (
            TIPO_DOCTO, SERIE, NUM_PED, ESTADO, FECHA,
            COD_PROVEED, COND_PAG, MONEDA, IMPSTO,
            C_COSTO, OPC_LENTR, L_ENTREGA, F_ENTREGA, DETALLE,
            C_CODIGO,
            VAL_VENTA, IMP_DESCTO, IMP_NETO, IMP_IGV, PRECIO_VTA,
            TOTAL_PEDIDO, TOTAL_FACTURADO,
            POR_DESC1, POR_DESC2,
            APROB_GERENCIA,
            A_ADUSER, A_ADFECHA, A_MDUSER, A_MDFECHA
        ) VALUES (
            P_TIPO_DOCTO, V_SERIE, V_NUM_PED, '0', P_FECHA,
            P_COD_PROVEED, P_COND_PAG, P_MONEDA, P_IMPSTO,
            P_C_COSTO, P_OPC_LENTR,
            CASE WHEN P_OPC_LENTR = '2' THEN P_L_ENTREGA
                 ELSE (SELECT DIRECCION FROM RH_EMPRESAS WHERE ROWNUM=1)
            END,
            P_F_ENTREGA, P_DETALLE,
            P_C_CODIGO,
            V_VAL_VENTA, 0, V_IMP_NETO, V_IMP_IGV, V_PRECIO_VTA,
            V_PRECIO_VTA, 0,       -- TOTAL_PEDIDO = total con IGV; TOTAL_FACTURADO = 0
            0, 0,                  -- POR_DESC1/2 globales = 0
            NULL,                  -- APROB_GERENCIA: pendiente de aprobación gerencial
            P_USUARIO, SYSDATE, P_USUARIO, SYSDATE
        );

        -- ── 7. INSERT ITEMORD (una fila por COD_ART unificado) ───
        /*  SALDO inicial = CANTIDAD (pendiente de recepción).
            ESTADO = '0' (registrado / pendiente de recibir).
            El almacén reduce SALDO conforme recibe la mercadería. */

        V_KEY := V_MERGE.FIRST;
        WHILE V_KEY IS NOT NULL LOOP
            INSERT INTO ITEMORD (
                TIPO_DOCTO, SERIE, NUM_PED, ORDEN, COD_ART,
                COD_ORIG, UNIDAD, DESCRIPCION,
                CANTIDAD, CANTIDAD_EQV, SALDO,
                PRECIO, POR_DESC1, POR_DESC2,
                IGV, IMP_VVTA,
                ESTADO,
                TIPO_DESTINO, COD_DESTINO, C_CODIGO,
                ID_GRUPO, F_GRUPO,
                A_ADUSER, A_ADFECHA, A_MDUSER, A_MDFECHA
            ) VALUES (
                P_TIPO_DOCTO, V_SERIE, V_NUM_PED,
                V_MERGE(V_KEY).ORD_ITEM,
                V_MERGE(V_KEY).COD_ART,
                V_MERGE(V_KEY).COD_ORIG,
                V_MERGE(V_KEY).UNIDAD,
                V_MERGE(V_KEY).DESCRIPCION,
                V_MERGE(V_KEY).CANTIDAD,
                V_MERGE(V_KEY).CANTIDAD,   -- CANTIDAD_EQV = CANTIDAD (sin conv. de unidad)
                V_MERGE(V_KEY).CANTIDAD,   -- SALDO = CANTIDAD total (aún no recibido)
                V_MERGE(V_KEY).PRECIO,
                V_MERGE(V_KEY).POR_DESC1,
                V_MERGE(V_KEY).POR_DESC2,
                V_MERGE(V_KEY).IGV_ITEM,
                V_MERGE(V_KEY).IMP_VVTA,
                '0',                       -- ESTADO = registrado
                V_MERGE(V_KEY).TIPO_DESTINO,
                V_MERGE(V_KEY).COD_DESTINO,
                V_MERGE(V_KEY).C_CODIGO,
                NULL, NULL,                -- ID_GRUPO / F_GRUPO: se asignan al cotizar
                P_USUARIO, SYSDATE, P_USUARIO, SYSDATE
            );
            V_KEY := V_MERGE.NEXT(V_KEY);
        END LOOP;

        -- ── 8. Para cada ítem original: DESP_ITEMREQ + actualizar SALDO ──
        /*  DESP_ITEMREQ registra la trazabilidad req-ítem → OC-ítem.
            FK_DESP_OCOMPRA valida que exista (TIPO_DOCTO, SERIE, NUM_PED,
            ORDEN_REF, COD_ART) en ITEMORD (ya insertado en paso 7).
            Se descuenta la CANTIDAD despachada del SALDO del ITEMREQ. */

        FOR I IN 1 .. P_ITEMS.COUNT LOOP
            -- ORDEN_REF: orden del COD_ART en ITEMORD (del mapa de merge)
            V_ORD_REF := V_MERGE(P_ITEMS(I).COD_ART).ORD_ITEM;

            -- Registrar despacho
            INSERT INTO DESP_ITEMREQ (
                TIPDOC, SERIE, NUMREQ, ORDEN, COD_ART,
                TIP_DOC_REF, SER_DOC_REF, NRO_DOC_REF,
                CANTIDAD, ESTADO, ORDEN_REF
            ) VALUES (
                P_ITEMS(I).TIPDOC, P_ITEMS(I).SERIE,
                P_ITEMS(I).NUMREQ, P_ITEMS(I).ORDEN,
                P_ITEMS(I).COD_ART,
                P_TIPO_DOCTO, V_SERIE, V_NUM_PED,
                P_ITEMS(I).CANTIDAD,
                '0',                  -- estado despacho: pendiente de recepción
                V_ORD_REF
            );

            -- Descontar saldo del ítem de requisición
            UPDATE ITEMREQ
            SET    SALDO     = SALDO - P_ITEMS(I).CANTIDAD,
                   A_MDUSER  = P_USUARIO,
                   A_MDFECHA = SYSDATE
            WHERE  TIPDOC  = P_ITEMS(I).TIPDOC
              AND  SERIE   = P_ITEMS(I).SERIE
              AND  NUMREQ  = P_ITEMS(I).NUMREQ
              AND  ORDEN   = P_ITEMS(I).ORDEN
              AND  COD_ART = P_ITEMS(I).COD_ART;

        END LOOP;

        -- ── 9. Cerrar requerimientos completamente atendidos ─────
        /*  Un requerimiento pasa a ESTADO='6' cuando la SUMA de SALDO
            de todos sus ítems llega a 0 (cada ítem fue despachado
            totalmente en esta u otras O/Cs anteriores). */

        FOR I IN 1 .. P_ITEMS.COUNT LOOP
            -- Construir clave única del requerimiento
            V_CLAVE_REQ := P_ITEMS(I).TIPDOC
                           || LPAD(TO_CHAR(P_ITEMS(I).SERIE), 3, '0')
                           || LPAD(TO_CHAR(P_ITEMS(I).NUMREQ), 8, '0');

            -- Procesar cada req una sola vez
            IF NOT V_REQS_PROC.EXISTS(V_CLAVE_REQ) THEN
                V_REQS_PROC(V_CLAVE_REQ) := 1;

                -- Saldo total pendiente del requerimiento
                SELECT NVL(SUM(SALDO), 0)
                INTO   V_SALDO_TOTAL
                FROM   ITEMREQ
                WHERE  TIPDOC = P_ITEMS(I).TIPDOC
                  AND  SERIE  = P_ITEMS(I).SERIE
                  AND  NUMREQ = P_ITEMS(I).NUMREQ;

                IF V_SALDO_TOTAL = 0 THEN
                    -- Todos los ítems atendidos: cerrar requisición
                    UPDATE REQUISICION
                    SET    ESTADO             = '6',
                           FCH_ENTREGA_LOGIST = SYSDATE,
                           A_MDUSER           = P_USUARIO,
                           A_MDFECHA          = SYSDATE
                    WHERE  TIPDOC = P_ITEMS(I).TIPDOC
                      AND  SERIE  = P_ITEMS(I).SERIE
                      AND  NUMREQ = P_ITEMS(I).NUMREQ
                      AND  ESTADO IN ('1', '2');    -- no pisar si ya está en otro estado
                END IF;
            END IF;
        END LOOP;

        -- ── 10. Incrementar contador en PARAMLG y confirmar ────────
        IF P_TIPO_DOCTO = V_DOCORDE THEN
            UPDATE PARAMLG SET NUMORDE = NUMORDE + 1;
        ELSE
            UPDATE PARAMLG SET NUMSERV = NUMSERV + 1;
        END IF;

        P_NUM_PED  := V_NUM_PED;
        P_MSGERROR := NULL;
        COMMIT;

    EXCEPTION
        WHEN DUP_VAL_ON_INDEX THEN
            ROLLBACK;
            -- Reintentar con el siguiente número (raro, sólo en alta concurrencia)
            P_MSGERROR := 'El número de O/C generado ya existe. Intente nuevamente.';
            P_NUM_PED  := NULL;
        WHEN OTHERS THEN
            ROLLBACK;
            P_MSGERROR := 'Error al registrar O/C: ' || SQLERRM;
            P_NUM_PED  := NULL;
    END P_REGISTRAR_OC;

    -- ── P_OBTENER_IGV ────────────────────────────────────────────
    PROCEDURE P_OBTENER_IGV (
        P_CURSOR OUT T_CURSOR
    ) IS
    BEGIN
        OPEN P_CURSOR FOR
            SELECT CODIGO, DESCRIPCION, VALOR
            FROM (
                SELECT CODIGO, DESCRIPCION, VALOR
                FROM   IMPUESTO
                UNION ALL
                -- Valor especial de descuento / nota de crédito
                SELECT 'X' AS CODIGO, 'DESCUENTO/N.C.' AS DESCRIPCION, C_IGV_ESPECIAL AS VALOR
                FROM   DUAL
            )
            ORDER BY VALOR;
    END P_OBTENER_IGV;

    -- ── P_OBTENER_DESTINOS ───────────────────────────────────────
    PROCEDURE P_OBTENER_DESTINOS (
        P_TIPO   IN  VARCHAR2 DEFAULT NULL,
        P_BUSCAR IN  VARCHAR2 DEFAULT NULL,
        P_CURSOR OUT T_CURSOR
    ) IS
        V_TIPO   VARCHAR2(1) := UPPER(TRIM(P_TIPO));
        V_BUSCAR VARCHAR2(200);
    BEGIN
        IF TRIM(P_BUSCAR) IS NOT NULL THEN
            V_BUSCAR := '%' || UPPER(TRIM(P_BUSCAR)) || '%';
        END IF;

        OPEN P_CURSOR FOR
            SELECT *
            FROM (
                -- Tipo 'U': Centro de Costo
                SELECT 'U'                    AS TP_DESTINO,
                       CENTRO_COSTO           AS CODIGO,
                       NOMBRE                 AS DESCRIPCION
                FROM   CENTRO_DE_COSTOS
                WHERE  TIPO   = 'D'
                  AND  ESTADO <> '9'
                  AND  (V_BUSCAR IS NULL
                        OR UPPER(CENTRO_COSTO) LIKE V_BUSCAR
                        OR UPPER(NOMBRE)       LIKE V_BUSCAR)
                UNION ALL
                -- Tipo 'A': Activo Fijo (CODIGO-NUMERO -> código del destino)
                SELECT 'A'                                   AS TP_DESTINO,
                       CODIGO || '-' || TO_CHAR(NUMERO)      AS CODIGO,
                       DESCRIPCION                           AS DESCRIPCION
                FROM   ACTIVO_FIJO
                WHERE  (V_TIPO IS NULL OR V_TIPO = 'A')
                  AND  ESTADO NOT IN ('2','9')   -- excluir dados de baja
                  AND  (V_BUSCAR IS NULL
                        OR UPPER(CODIGO || '-' || TO_CHAR(NUMERO)) LIKE V_BUSCAR
                        OR UPPER(DESCRIPCION)                      LIKE V_BUSCAR)
            )
            ORDER BY TP_DESTINO, CODIGO;
    END P_OBTENER_DESTINOS;

    -- ── P_ANULAR_OC ─────────────────────────────────────────────
    PROCEDURE P_ANULAR_OC (
        P_TIPO_DOCTO IN  ORDEN_DE_COMPRA.TIPO_DOCTO%TYPE,
        P_SERIE      IN  ORDEN_DE_COMPRA.SERIE%TYPE,
        P_NUM_PED    IN  ORDEN_DE_COMPRA.NUM_PED%TYPE,
        P_USUARIO    IN  VARCHAR2,
        P_MSGERROR   OUT VARCHAR2
    ) IS
        V_ESTADO ORDEN_DE_COMPRA.ESTADO%TYPE;
        V_CNT    NUMBER;
    BEGIN
        P_MSGERROR := NULL;

        -- Verificar existencia y estado anulable
        BEGIN
            SELECT ESTADO
            INTO   V_ESTADO
            FROM   ORDEN_DE_COMPRA
            WHERE  TIPO_DOCTO = P_TIPO_DOCTO
              AND  SERIE      = P_SERIE
              AND  NUM_PED    = P_NUM_PED;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN
                P_MSGERROR := 'La O/C ' || P_NUM_PED || ' no existe.';
                RETURN;
        END;

        IF V_ESTADO <> '0' THEN
            P_MSGERROR := 'Solo se pueden anular órdenes en estado EMITIDA (0).'
                          || ' Estado actual: ' || V_ESTADO || '.';
            RETURN;
        END IF;

        -- Verificar que no tenga recepciones de almacén
        SELECT COUNT(*)
        INTO   V_CNT
        FROM   ITEMORD
        WHERE  TIPO_DOCTO = P_TIPO_DOCTO
          AND  SERIE      = P_SERIE
          AND  NUM_PED    = P_NUM_PED
          AND  SALDO      < CANTIDAD;   -- si SALDO < CANTIDAD → ya hubo recepción parcial

        IF V_CNT > 0 THEN
            P_MSGERROR := 'La O/C ' || P_NUM_PED
                          || ' tiene recepciones de almacén registradas y no puede anularse.';
            RETURN;
        END IF;

        -- Restaurar SALDO en ITEMREQ y reactivar reqs cerrados
        FOR REC IN (
            SELECT D.TIPDOC, D.SERIE, D.NUMREQ, D.ORDEN, D.COD_ART, D.CANTIDAD
            FROM   DESP_ITEMREQ D
            WHERE  D.TIP_DOC_REF = P_TIPO_DOCTO
              AND  D.SER_DOC_REF = P_SERIE
              AND  D.NRO_DOC_REF = P_NUM_PED
        ) LOOP
            -- Devolver saldo al ítem de requisición
            UPDATE ITEMREQ
            SET    SALDO     = SALDO + REC.CANTIDAD,
                   A_MDUSER  = P_USUARIO,
                   A_MDFECHA = SYSDATE
            WHERE  TIPDOC  = REC.TIPDOC
              AND  SERIE   = REC.SERIE
              AND  NUMREQ  = REC.NUMREQ
              AND  ORDEN   = REC.ORDEN
              AND  COD_ART = REC.COD_ART;

            -- Si la requisición estaba cerrada por esta O/C, reactivarla
            -- (vuelve a '1' si tenía autorización, o a '2' si ya fue recibida)
            UPDATE REQUISICION
            SET    ESTADO             = CASE
                                            WHEN AUTORIZA IS NOT NULL
                                              AND TRIM(AUTORIZA) != '' THEN '1'
                                            ELSE '2'
                                        END,
                   FCH_ENTREGA_LOGIST = NULL,
                   A_MDUSER           = P_USUARIO,
                   A_MDFECHA          = SYSDATE
            WHERE  TIPDOC = REC.TIPDOC
              AND  SERIE  = REC.SERIE
              AND  NUMREQ = REC.NUMREQ
              AND  ESTADO = '6';     -- solo si estaba cerrado
        END LOOP;

        -- Eliminar registros de despacho
        DELETE FROM DESP_ITEMREQ
        WHERE  TIP_DOC_REF = P_TIPO_DOCTO
          AND  SER_DOC_REF = P_SERIE
          AND  NRO_DOC_REF = P_NUM_PED;

        -- Anular ítems de la O/C
        UPDATE ITEMORD
        SET    ESTADO    = '9',
               A_MDUSER  = P_USUARIO,
               A_MDFECHA = SYSDATE
        WHERE  TIPO_DOCTO = P_TIPO_DOCTO
          AND  SERIE      = P_SERIE
          AND  NUM_PED    = P_NUM_PED;

        -- Anular cabecera
        UPDATE ORDEN_DE_COMPRA
        SET    ESTADO    = '9',
               A_MDUSER  = P_USUARIO,
               A_MDFECHA = SYSDATE
        WHERE  TIPO_DOCTO = P_TIPO_DOCTO
          AND  SERIE      = P_SERIE
          AND  NUM_PED    = P_NUM_PED;

        COMMIT;

    EXCEPTION
        WHEN OTHERS THEN
            ROLLBACK;
            P_MSGERROR := 'Error al anular O/C: ' || SQLERRM;
    END P_ANULAR_OC;

END PKG_REG_ORDEN_COMPRA;
/
