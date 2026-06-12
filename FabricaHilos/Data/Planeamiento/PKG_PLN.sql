/* ============================================================
   02_PLN_PKG_SPEC_BODY.sql  —  Módulo PLN_ · Planeamiento de Planta
   ============================================================
   CONTENIDO : §5 CREATE OR REPLACE PACKAGE PKG_PLN (especificación)
               §6 CREATE OR REPLACE PACKAGE BODY PKG_PLN
   INCLUYE   : SP_PLN_INIT_SEGUIMIENTO, SP_PLN_AVANZA_PASO,
               SP_PLN_CALCULA_FECHAS, SP_PLN_GENERA_ALERTAS,
               SP_PLN_CARGA_DIARIA_REFRESH, SP_PLN_KGR_REFRESH,
               SP_PLN_REPROGRAMAR, SP_PLN_FILTRO_PROCESOS
   PREREQUISITO: 01_PLN_TABLAS.sql ya ejecutado
   EJECUTAR  : SEGUNDO
   SIGUIENTE : 03_PLN_VISTAS.sql  (opcional, puede ir antes de triggers)
   Fuente    : PKG_PLN.sql v2.6  (líneas 1197–3306)
   ============================================================ */
-- §5  PACKAGE PKG_PLN — ESPECIFICACIÓN
-- ============================================================
CREATE OR REPLACE PACKAGE PKG_PLN AS

  /*
    Procedimientos públicos del módulo de Planeamiento y
    Seguimiento de Planta (PLN_).

    REGLA: ninguno de estos procedimientos lanza excepción al
    llamador externo (triggers de planta).  Los triggers capturan
    WHEN OTHERS THEN NULL.  Los SPs sí pueden re-levantar errores
    cuando son llamados directamente (por jobs o UTL).
  */

  -- ── Inicialización del seguimiento ─────────────────────────
  PROCEDURE SP_PLN_INIT_SEGUIMIENTO (
    p_serie     IN NUMBER,
    p_num_ped   IN NUMBER,
    p_nro       IN NUMBER,
    p_num_det   IN NUMBER    DEFAULT 0,
    p_paso_ini  IN VARCHAR2  DEFAULT '01'  -- '13' para SOLO_DESPACHO='S'
  );

  -- ── Avance de paso (llamado desde todos los triggers) ───────
  PROCEDURE SP_PLN_AVANZA_PASO (
    p_serie        IN NUMBER,
    p_num_ped      IN NUMBER,
    p_nro          IN NUMBER,
    p_num_det      IN NUMBER,
    p_nuevo_paso   IN VARCHAR2,
    p_tabla_origen IN VARCHAR2,
    p_id_origen    IN NUMBER    DEFAULT NULL,
    p_kg_cantidad  IN NUMBER    DEFAULT NULL,
    p_observacion  IN VARCHAR2  DEFAULT NULL,
    p_fch_evento   IN DATE      DEFAULT NULL  -- v2.1: NULL=SYSDATE; NOT NULL=fecha historica
  );

  -- ── Cálculo de fechas estimadas ─────────────────────────────
  PROCEDURE SP_PLN_CALCULA_FECHAS (
    p_serie     IN NUMBER,
    p_num_ped   IN NUMBER,
    p_nro       IN NUMBER,
    p_num_det   IN NUMBER,
    p_motivo    IN VARCHAR2  DEFAULT 'PED'  -- PED/PLA/REP/MAQ
  );

  -- ── Motor de alertas (invocado por JOB_PLN_ALERTAS) ─────────
  PROCEDURE SP_PLN_GENERA_ALERTAS;

  -- ── Recálculo de carga de máquinas (invocado por JOB_PLN_CARGA)
  PROCEDURE SP_PLN_CARGA_DIARIA_REFRESH (
    p_fch_ini IN DATE DEFAULT TRUNC(SYSDATE),
    p_fch_fin IN DATE DEFAULT TRUNC(SYSDATE) + 30
  );

  -- ── Recálculo de velocidades kg/hr (invocado por JOB_PLN_KGR) ─
  -- Trunca y repobla PLN_KGR_TITULO desde H_RPRODUC.
  -- Ventana: últimos p_meses meses. Mínimo p_min_muestras por combinación.
  PROCEDURE SP_PLN_KGR_REFRESH (
    p_meses        IN NUMBER DEFAULT 24,
    p_min_muestras IN NUMBER DEFAULT 3
  );

  -- ── Cierre manual de un ítem ────────────────────────────────
  PROCEDURE SP_PLN_CIERRE_ITEM (
    p_id_seguim  IN NUMBER,
    p_motivo     IN VARCHAR2  DEFAULT 'CIERRE_MANUAL',
    p_usuario    IN VARCHAR2  DEFAULT NULL
  );

  -- ── Reprogramación manual de un ítem ────────────────────────
  PROCEDURE SP_PLN_REPROGRAMAR (
    p_serie          IN NUMBER,
    p_num_ped        IN NUMBER,
    p_nro            IN NUMBER,
    p_num_det        IN NUMBER,
    p_nueva_fch_desp IN DATE,
    p_motivo         IN VARCHAR2  DEFAULT 'REPROG_MANUAL',
    p_usuario        IN VARCHAR2  DEFAULT NULL
  );

  -- ── Seguimiento Programación Tintorería (ex QUERY_PRODUCCION + hoja DT Excel) ──
  -- Reporte principal de seguimiento por ítem de pedido.
  -- Devuelve ~57 columnas: las 40 originales de QUERY_PRODUCCION + 17 calculadas
  -- equivalentes a la hoja "DT" del Excel SEGUIMIENTO_PARTIDAS_TINTORERIA_KAREN.xlsm.
  --
  -- Columnas DT añadidas (v3.1):
  --   MES, MES_TEX, ANO, SEM          → dimensiones de tiempo por FCH_ENTREGA
  --   DIAS_ROD                         → ESTIMA_CONO_UNO − ENTREGA_CONO_UNO
  --   DIAS_MH                          → demora material hilandería (MAX 0)
  --   DIAS_REC                         → demora receta (MAX 0)
  --   DIAS_TENIDO                      → demora teñido (MAX 0)
  --   TIME_APROV                       → días CC tintorería: FECHA_CCALID − FECHA_SECADO
  --   TIPO_ACABADO                     → 'REDINA' (madeja) | 'CONERA' (cono/rodete)
  --   EV_ENCON                         → 'APROBADO' | 'CONCESIONADO' | 'RECHAZADO' | 'EN CONSULTA'
  --   DIAS_EN_ESPERA                   → MAX(0, ING_ALMPT − FCH_ENTREGA) — positivo=atrasado
  --   DE                               → ídem con signo (negativo=llegó antes)
  --   GAP_KG                           → CANT_DESP − CANT_PROG (negativo=faltó despachar)
  --   PCT_TOLERANCIA                   → % ± respecto a CANT_PROG; NULL=sin despachar
  --   ESTADO_FLUJO                     → etapa más avanzada ('DESPACHADO'...'SIN RECETA')
  --   ESTADO_DESPACHO                  → semáforo ('VENCIDO'|'VENCE HOY'|'A TIEMPO'|etc.)
  --
  -- p_opc : 'POR FECHA DE ENTREGA'   → p_fechai / p_fechaf con FCH_ENTREGA
  --         'POR PEDIDO'              → p_numped  con NUM_PED
  --         'POR FECHA DE PROGRAMA'   → p_fechai / p_fechaf con FHC_PROG
  --         'POR FECHA DE TEÑIDO'     → p_fechai / p_fechaf con fecha tenido
  --         'POR FECHA APROB PEDIDO'  → p_fechai / p_fechaf con FCH_PEDIDO_APROB
  -- Filtros: p_cliente ('%'=todos, 'X'=excluye internos 77777777/88888888)
  --          p_asesor  ('%'=todos)  → PEDIDO.COD_VENDE
  --          p_titulo  ('%'=todos)  → ITEMPED_DET.TITULO  (código de H_TITULOS)
  --          p_fibra   ('%'=todos)  → ITEMPED_DET.TIPO_FIBRA (código de H_FIBRA)
  --          p_proceso ('%'=todos)  → ITEMPED_DET.PROCESO  (código de H_PROCESOS)
  PROCEDURE SP_PLN_SEG_PROG_TINTORERIA (
    p_opc      IN  VARCHAR2,
    p_fechai   IN  DATE       DEFAULT NULL,
    p_fechaf   IN  DATE       DEFAULT NULL,
    p_numped   IN  NUMBER     DEFAULT NULL,
    p_cliente  IN  VARCHAR2   DEFAULT '%',
    p_asesor   IN  VARCHAR2   DEFAULT '%',
    p_titulo   IN  VARCHAR2   DEFAULT '%',
    p_fibra    IN  VARCHAR2   DEFAULT '%',
    p_proceso  IN  VARCHAR2   DEFAULT '%',
    p_cursor   OUT SYS_REFCURSOR
  );

  -- ── Filtros para poblar combos del formulario ─────────────────
  -- Clientes con pedidos activos (excluye internos 77777777/88888888)
  PROCEDURE SP_PLN_FILTRO_CLIENTES   (p_cursor OUT SYS_REFCURSOR);
  -- Asesores/vendedores con pedidos activos
  PROCEDURE SP_PLN_FILTRO_ASESORES   (p_cursor OUT SYS_REFCURSOR);
  -- Títulos distintos usados en ítems activos
  PROCEDURE SP_PLN_FILTRO_TITULOS    (p_cursor OUT SYS_REFCURSOR);
  -- Fibras distintas usadas en ítems activos
  PROCEDURE SP_PLN_FILTRO_FIBRAS     (p_cursor OUT SYS_REFCURSOR);
  -- Procesos de producción usados en ítems activos
  PROCEDURE SP_PLN_FILTRO_PROCESOS   (p_cursor OUT SYS_REFCURSOR);

  -- Guarda OBSERVACIONES y COLORHEXA en ITEMPED_DET desde la web (botón Guardar)
  PROCEDURE SP_PLN_UPD_ITEM_OBS_COLOR (
    p_nroprog       IN NUMBER   DEFAULT NULL,
    p_num_ped       IN NUMBER   DEFAULT NULL,
    p_nro           IN NUMBER   DEFAULT NULL,
    p_num_det       IN NUMBER   DEFAULT NULL,
    p_reproceso     IN VARCHAR2 DEFAULT NULL,
    p_fch_prog      IN DATE     DEFAULT NULL,
    p_observaciones IN VARCHAR2,
    p_colorhexa     IN VARCHAR2,
    p_usuario       IN VARCHAR2 DEFAULT NULL
  );

END PKG_PLN;
/


-- ============================================================
-- §6  PACKAGE PKG_PLN — CUERPO
-- ============================================================
CREATE OR REPLACE PACKAGE BODY PKG_PLN AS

  -- ============================================================
  -- SP_PLN_INIT_SEGUIMIENTO
  -- ────────────────────────────────────────────────────────────
  -- Crea la fila inicial en PLN_SEGUIMIENTO al registrar un ítem.
  -- Idempotente: si ya existe (UK_PLN_SEGUIM), ignora silenciosamente.
  --
  -- CUÁNDO SE LLAMA:
  --   · TIA_PLN_FROM_ITEMPED      → INSERT ITEMPED (PASO '01')
  --   · TUA_PLN_FROM_ITEMPED_DET  → UPDATE ITEMPED_DET NROPROG (PASO '02')
  --
  -- LÓGICA FCH_ENTREGA_COMP (v2.3 — prioridad por ítem desde ITEMPED_DET):
  --   Prioridad 1: ITEMPED_DET.FHC_ENTREGA   (fecha FINAL de compromiso — LA QUE MANDA)
  --   Prioridad 2: ITEMPED_DET.FCH_ENTREGA_ORI (fecha ORIGINAL de compromiso del artículo)
  --   Prioridad 3: ITEMPED_DET.FCH_REG_ENTREGA (fecha de registro del ítem — uso interno)
  --   Prioridad 4: ITEMPED.F_MAXPED           (máximo por ítem de pedido)
  --   Prioridad 5: PEDIDO.FECHA + NVL(PEDIDO.PLAZO_ENTREGA, 30)  (fallback genérico)
  --   NO usar PEDIDO.FECHA_ENTREGA (no confiable en datos históricos).
  --
  -- LÓGICA SOLO_DESPACHO:
  --   Si ITEMPED.SOLO_DESPACHO='S' → p_paso_ini='13' (Listo para Despacho).
  --   El ítem arranca directamente desde stock sin pasar por producción.
  --   El trigger TIA_PLN_FROM_ITEMPED detecta este flag y ajusta p_paso_ini.
  --
  -- INSERTA también el primer evento en PLN_LOG_EVENTOS (TIPO_EVENTO='AV').
  -- Hace COMMIT al finalizar.
  -- ============================================================
  PROCEDURE SP_PLN_INIT_SEGUIMIENTO (
    p_serie     IN NUMBER,
    p_num_ped   IN NUMBER,
    p_nro       IN NUMBER,
    p_num_det   IN NUMBER    DEFAULT 0,
    p_paso_ini  IN VARCHAR2  DEFAULT '01'
  ) AS
    v_id            NUMBER;
    v_pedido        PEDIDO%ROWTYPE;
    v_item          ITEMPED%ROWTYPE;
    v_fch_entrega   DATE;
    v_fch_reg_ent   DATE;   -- ITEMPED_DET.FCH_REG_ENTREGA (fecha de registro del ítem)
    v_fch_ent_ori   DATE;   -- ITEMPED_DET.FCH_ENTREGA_ORI  (fecha ORIGINAL de compromiso)
    v_solo_desp     VARCHAR2(1) := 'N';
    v_cantidad      NUMBER(12,4);
    v_lote          VARCHAR2(20);
  BEGIN
    -- Leer cabecera
    SELECT * INTO v_pedido FROM PEDIDO  WHERE serie=p_serie AND num_ped=p_num_ped;
    SELECT * INTO v_item   FROM ITEMPED WHERE serie=p_serie AND num_ped=p_num_ped AND nro=p_nro;

    -- Guard: no inicializar pedidos cerrados (ESTADO='6') ni anulados (ESTADO='9')
    -- FIX v2.1 (21/05/2026): sin este guard, ITEMPED INSERTs en pedidos históricos
    -- o re-aperturas accidentales creaban filas PLN_ con retraso de miles de días.
    IF v_pedido.estado IN ('6', '9') THEN RETURN; END IF;

    -- Leer fechas de compromiso por artículo (v2.3): preferencia desde ITEMPED_DET
    -- Agrupación MAX: para el caso de reproceso con 2 filas en ITEMPED_DET para el mismo NUM_DET.
    BEGIN
      SELECT MAX(fhc_entrega),
             MAX(fch_reg_entrega),
             MAX(fch_entrega_ori)
      INTO   v_fch_entrega, v_fch_reg_ent, v_fch_ent_ori
      FROM   ITEMPED_DET
      WHERE  serie=p_serie AND num_ped=p_num_ped
        AND  nro=p_nro AND num_det=p_num_det;
    EXCEPTION WHEN NO_DATA_FOUND THEN NULL;
    END;

    -- Fecha compromiso: prioridad por ítem (v2.3) → fallback genérico
    -- Orden: FHC_ENTREGA (FINAL, la que manda) > FCH_ENTREGA_ORI (original) > FCH_REG_ENTREGA (registro)
    --        > F_MAXPED (ITEMPED) > PEDIDO.FECHA + plazo
    v_fch_entrega := NVL(v_fch_entrega,
                     NVL(v_fch_ent_ori,
                     NVL(v_fch_reg_ent,
                     NVL(v_item.f_maxped,
                         v_pedido.fecha + NVL(v_pedido.plazo_entrega, 30)))));

    -- SOLO_DESPACHO: campo ITEMPED si existe; asumir 'N' si no
    BEGIN
      SELECT NVL(solo_despacho,'N') INTO v_solo_desp
      FROM ITEMPED WHERE serie=p_serie AND num_ped=p_num_ped AND nro=p_nro;
    EXCEPTION WHEN OTHERS THEN v_solo_desp := 'N';
    END;

    -- Lote y cantidad del sub-lote si existe ITEMPED_DET
    -- NOTA: ITEMPED no tiene columna LOTE (verificado en BD). El lote solo existe en ITEMPED_DET.
    v_cantidad := v_item.cantidad;
    BEGIN
      SELECT lote, NVL(cantidad, v_item.cantidad)
      INTO v_lote, v_cantidad
      FROM ITEMPED_DET
      WHERE serie=p_serie AND num_ped=p_num_ped AND nro=p_nro AND num_det=p_num_det
        AND ROWNUM = 1;  -- en reproceso puede haber 2 filas; tomar la primera (NROPROG se actualiza luego)
    EXCEPTION WHEN NO_DATA_FOUND THEN
      v_lote := NULL;  -- ITEMPED no tiene LOTE; sin ITEMPED_DET el lote queda NULL
    END;

    SELECT PLN_SEQ_SEGUIM.NEXTVAL INTO v_id FROM DUAL;

    INSERT INTO PLN_SEGUIMIENTO (
      ID_SEGUIM, SERIE, NUM_PED, NRO, NUM_DET,
      COD_CLIENTE, COD_ART, COLOR, TITULO, PROCESO, LOTE,
      CANTIDAD_ORIG, SOLO_DESPACHO,
      COD_PASO_ACT, NRO_CICLO, FCH_PEDIDO, FCH_APROBACION, FCH_ENTREGA_COMP,
      FCH_REG_ENTREGA, FCH_ENTREGA_ORI,
      KG_PENDIENTES, IND_RETRASO, IND_URGENTE, ESTADO,
      A_ADUSER, A_ADFECHA
    ) VALUES (
      v_id, p_serie, p_num_ped, p_nro, p_num_det,
      v_pedido.cod_cliente, v_item.cod_art, v_item.color,
      v_item.titulo, v_item.proceso, v_lote,
      v_cantidad, v_solo_desp,
      p_paso_ini, 1, v_pedido.fecha, v_pedido.f_aprobacion, v_fch_entrega,
      v_fch_reg_ent, v_fch_ent_ori,
      v_cantidad, 'N', 'N', 'A',  -- IND_URGENTE='N' por defecto (ITEMPED.DESAPRB ≠ urgencia; actualizar desde ITEMPED_DET.URGENTE)
      USER, SYSDATE
    );

    -- Evento inicial
    INSERT INTO PLN_LOG_EVENTOS (
      ID_EVENTO, ID_SEGUIM, SERIE, NUM_PED, NRO, NUM_DET,
      COD_PASO, DESC_PASO, TABLA_ORIGEN, FCH_EVENTO, USUARIO,
      KG_CANTIDAD, TIPO_EVENTO
    ) VALUES (
      PLN_SEQ_EVENTO.NEXTVAL, v_id, p_serie, p_num_ped, p_nro, p_num_det,
      p_paso_ini, 'Seguimiento inicializado', 'ITEMPED', SYSDATE, USER,
      v_cantidad, 'AV'
    );
    -- *** Sin COMMIT aquí ***
    -- ORA-04092: los procedimientos llamados desde un trigger NO pueden hacer COMMIT.
    -- El COMMIT lo cierra la transacción padre (trigger → commit implícito al finalizar la DML).
    -- Cuando se llama directamente desde la app usar:
    --   BEGIN PKG_PLN.SP_PLN_INIT_SEGUIMIENTO(...); COMMIT; END;

  EXCEPTION
    WHEN DUP_VAL_ON_INDEX THEN NULL;  -- ya existe → ignorar silenciosamente
    WHEN OTHERS THEN RAISE;           -- propaga; el trigger lo absorbe con WHEN OTHERS THEN NULL
  END SP_PLN_INIT_SEGUIMIENTO;


  -- ============================================================
  -- SP_PLN_AVANZA_PASO — Motor central del módulo PLN_
  -- ────────────────────────────────────────────────────────────
  -- Responsable de TODAS las transiciones de estado en PLN_SEGUIMIENTO.
  -- Llamado exclusivamente por los 12 triggers del §7.
  -- También puede llamarse manualmente para correcciones autorizadas.
  --
  -- PARÁMETROS:
  --   p_serie        : número de serie del pedido (PEDIDO.SERIE)
  --   p_num_ped      : número de pedido (PEDIDO.NUM_PED)
  --   p_nro          : número de ítem (ITEMPED.NRO)
  --   p_num_det      : sub-lote (ITEMPED_DET.NUM_DET; 0 si ítem sin sublotes)
  --   p_nuevo_paso   : código de paso destino (PLN_ESTADO_CODIGO.COD_PASO)
  --   p_tabla_origen : tabla que disparó el cambio (ej. 'PARTIDA', 'LOTES')
  --   p_id_origen    : PK del registro en la tabla origen (para trazabilidad)
  --   p_kg_cantidad  : kg involucrados en este evento (null si no aplica)
  --   p_observacion  : texto libre para PLN_LOG_EVENTOS.OBSERVACION
  --
  -- REGLAS CRÍTICAS (correcciones incorporadas vs. Propuesta.md original):
  --   · FCH_REAL_PARTIDA  → solo PASO '05' (Lote Disponible, hilo crudo producido)
  --   · FCH_REAL_TIN_FIN  → solo PASO '07' (Tenido completo, NO con SECADO '08')
  --   · KG_PRODUCIDOS     → solo se SUMA en PASO '03' (PARTIDA INSERT)
  --   · KG_EN_TIN         → solo se SUMA en PASO '06' (entrada física a TT)
  --   · KG_EN_ALM_PT      → solo se SUMA en PASO '12' (ingreso almacén PT)
  --   · KG_DESPACHADOS    → solo se SUMA en PASO '14' (puede ser parcial)
  --   · KG_PENDIENTES     → se RESTA en PASO '14' (mínimo 0 via GREATEST)
  --   · ESTADO='C'        → solo PASO '14' cuando v_nuevo_kg >= CANTIDAD_ORIG
  --   · Despacho parcial  → si v_nuevo_kg < CANTIDAD_ORIG, COD_PASO_ACT='13'
  --   · NRO_CICLO         → se incrementa SOLO en PASO '9R' (reproceso)
  --   · IND_REPROCESO     → 'S' en PASO '9R', 'N' en PASO '09' (CC aprobado)
  --
  -- CONCURRENCIA:
  --   SELECT ... FOR UPDATE bloquea la fila durante toda la transacción.
  --   Múltiples baños de TT (75% de partidas) pueden llegar concurrentemente.
  --
  -- MANEJO DE ERRORES:
  --   NO_DATA_FOUND → NULL (el seguimiento aún no existe; el trigger del ITEMPED
  --                          lo creará luego; este avance se descarta silenciosamente)
  --   OTHERS        → ROLLBACK + RAISE (propaga al trigger que tiene WHEN OTHERS THEN NULL)
  -- ============================================================
  PROCEDURE SP_PLN_AVANZA_PASO (
    p_serie        IN NUMBER,
    p_num_ped      IN NUMBER,
    p_nro          IN NUMBER,
    p_num_det      IN NUMBER,
    p_nuevo_paso   IN VARCHAR2,
    p_tabla_origen IN VARCHAR2,
    p_id_origen    IN NUMBER    DEFAULT NULL,
    p_kg_cantidad  IN NUMBER    DEFAULT NULL,
    p_observacion  IN VARCHAR2  DEFAULT NULL,
    p_fch_evento   IN DATE      DEFAULT NULL  -- v2.1: fecha real del evento (NULL=SYSDATE). Usar
                                              -- para retroalimentar ítems históricos ya existentes
                                              -- sin requerir triggers. Triggers siempre pasan NULL.
  ) AS
    v_seg        PLN_SEGUIMIENTO%ROWTYPE;
    v_id_evt     NUMBER;
    v_nuevo_kg   NUMBER;    -- KG_DESPACHADOS proyectado (con old value)
    v_orden_act  NUMBER := 0;  -- Orden del paso actual  (protección anti-retroceso)
    v_orden_new  NUMBER := 0;  -- Orden del paso entrante
    -- FIX-PCT: leer umbral de cierre desde PLN_PARAM (evita que el parámetro sea ignorado).
    -- Default 0.95 si no existe la fila (por retrocompatibilidad).
    v_pct_cierre NUMBER := 0.95;
  BEGIN
    BEGIN
      SELECT valor_num / 100
      INTO   v_pct_cierre
      FROM   PLN_PARAM
      WHERE  cod_param = 'PCT_CIERRE_DESPACHO';
    EXCEPTION WHEN NO_DATA_FOUND THEN NULL;
    END;
    -- ESTADO='A': ignora ítems cerrados (C) o anulados (X) → NO_DATA_FOUND → EXCEPTION
    SELECT * INTO v_seg
    FROM PLN_SEGUIMIENTO
    WHERE serie=p_serie AND num_ped=p_num_ped AND nro=p_nro AND num_det=p_num_det
      AND ESTADO = 'A'
    FOR UPDATE;

    -- KG_DESPACHADOS proyectado (OLD + nuevo)
    v_nuevo_kg := v_seg.kg_despachados + NVL(p_kg_cantidad, 0);

    -- BUG-A FIX: leer órdenes ANTES de cualquier check (zona concurrente + anti-retroceso).
    -- ANTES: v_orden_act=0 cuando se evaluaba IF p_nuevo_paso='03' AND v_orden_act>=4
    --        → condición siempre FALSE → retorno incorrecto nunca se ejecutaba.
    BEGIN
      SELECT ec.orden_paso INTO v_orden_act
      FROM pln_estado_codigo ec WHERE ec.cod_paso = v_seg.cod_paso_act;
    EXCEPTION WHEN NO_DATA_FOUND THEN v_orden_act := 0; END;
    BEGIN
      SELECT ec.orden_paso INTO v_orden_new
      FROM pln_estado_codigo ec WHERE ec.cod_paso = p_nuevo_paso;
    EXCEPTION WHEN NO_DATA_FOUND THEN v_orden_new := 0; END;

    -- ═══════════════════════════════════════════════════════════════════════
    -- ZONA CONCURRENTE — PASO '03' (Hilandería/PARTIDA) y PASO '04' (Lab)
    -- ═══════════════════════════════════════════════════════════════════════
    -- Ambos pasos son PARALELOS: pueden ocurrir en CUALQUIER orden según el pedido.
    -- Distribución real en BD (últimos 6 meses):
    --   ~81% IND_FLUJO='L' → Lab aprueba ANTES de crear PARTIDA formal
    --   ~ 3% IND_FLUJO='H' → PARTIDA creada ANTES de aprobación Lab (pedidos urgentes)
    --   ~16% misma fecha
    --
    -- Cuando PASO '03' (Hilandería) dispara y el ítem YA está en '04' o más:
    --   → FCH_REAL_PRODUCCION + KG_PRODUCIDOS + NUM_PARTIDA se actualizan
    --   → COD_PASO_ACT NO cambia (el ítem ya está más avanzado)
    --   → Evento log se inserta con tipo 'AV' y PASO '03'
    --   → Se retorna sin pasar por el UPDATE principal
    -- (Si el ítem está en '03' y dispara '04', el flujo normal avanza a '04' — OK)
    IF p_nuevo_paso = '03'
       AND v_orden_act >= 4   -- ítem ya en '04' o más avanzado
       AND v_seg.fch_real_produccion IS NULL  -- Hilandería aún no registrada
    THEN
      UPDATE PLN_SEGUIMIENTO
      SET fch_real_produccion = NVL(p_fch_evento, SYSDATE),
          num_partida         = NVL(p_id_origen,  num_partida),
          kg_producidos       = kg_producidos + NVL(p_kg_cantidad, 0),
          ind_flujo           = 'L'  -- Lab fue primero (flujo normal ~81%)
      WHERE id_seguim = v_seg.id_seguim;

      -- Registrar el evento (Lab ya avanzó el PASO_ACT; este evento complementa)
      SELECT PLN_SEQ_EVENTO.NEXTVAL INTO v_id_evt FROM DUAL;
      INSERT INTO PLN_LOG_EVENTOS (
        id_evento,   id_seguim,
        serie,       num_ped,     nro,     num_det,
        cod_paso,    tipo_evento,
        tabla_origen,
        kg_cantidad, id_objeto_origen,
        fch_evento,
        observacion, usuario
      ) VALUES (
        v_id_evt,    v_seg.id_seguim,
        v_seg.serie, v_seg.num_ped, v_seg.nro, v_seg.num_det,
        '03',        'AV',
        NVL(p_tabla_origen, 'PARTIDA'),
        p_kg_cantidad, p_id_origen,
        NVL(p_fch_evento, SYSDATE),
        NVL(p_observacion, 'Hilanderia: PARTIDA creada (Lab aprobo antes — flujo L)'),
        SYS_CONTEXT('USERENV','SESSION_USER')
      );
      RETURN;  -- NO continúa al UPDATE principal — COD_PASO_ACT no cambia
    END IF;

    -- Protección anti-retroceso: un mismo trigger puede dispararse varias veces
    -- para el mismo lote (ej. múltiples H_RPRODUC o LOTES para la misma PARTIDA).
    -- Si el nuevo paso tiene ORDEN inferior al actual, se ignora sin retroceder.
    -- PASO '14' se excluye: su retroceso a '13' por despacho parcial es intencional.
    -- PASO '9R' se excluye: después de reproceso ('9R', ORDEN=11) el ciclo reinicia
    --   y TUA_PLN_FROM_PARTIDA intenta avanzar a '06' (ORDEN=6) → debe permitirse.
    --   Sin esta excepción, el ítem quedaría bloqueado en '9R' para siempre. (BUG #34)
    -- (BUG-A FIX: v_orden_act y v_orden_new ya fueron leídos antes de la zona concurrente)
    IF p_nuevo_paso NOT IN ('14')            -- '14' tiene retroceso intencional a '13'
       AND v_seg.cod_paso_act <> '9R'        -- '9R' reinicia ciclo (BUG #34)
       AND v_orden_new < v_orden_act
    THEN
      RETURN;  -- Trigger duplicado/desordenado → no retroceder
    END IF;

    -- BUG #35: PASO '09B' (Gaseado) solo aplica para ítems con PROCESO='24'.
    -- Si el trigger H_RPRODUC TP_MAQ='G' dispara para un ítem no-'24' (no debería
    -- ocurrir en producción normal), ignorar para evitar avances incorrectos.
    IF p_nuevo_paso = '09B' AND NVL(v_seg.proceso, '') <> '24' THEN
      RETURN;
    END IF;

    UPDATE PLN_SEGUIMIENTO SET
      COD_PASO_ANT        = COD_PASO_ACT,
      -- Despacho parcial → retrocede a '13'; cierre completo → '14'
      -- PCT_CIERRE_DESPACHO=95%: tolera hasta 5% merma textil (merma tintorería/devanado)
      COD_PASO_ACT        = CASE
                              WHEN p_nuevo_paso = '14' AND v_nuevo_kg < CANTIDAD_ORIG * v_pct_cierre THEN '13'
                              ELSE p_nuevo_paso
                            END,
      -- v2.3: IND_FLUJO — determina cuál llegó primero entre PASO '03' y '04'
      -- Cuando PASO '04' (Lab) dispara y FCH_REAL_PRODUCCION ya tiene valor →
      --   PARTIDA fue creada antes → IND_FLUJO='H' (Hilandería fue primero ~3%)
      -- Cuando PASO '04' dispara y FCH_REAL_PRODUCCION es NULL →
      --   Lab es primero → IND_FLUJO='L' (flujo normal ~81%)
      IND_FLUJO           = CASE
                              WHEN p_nuevo_paso = '04' AND fch_real_produccion IS NOT NULL THEN 'H'
                              WHEN p_nuevo_paso = '04' AND fch_real_produccion IS NULL     THEN 'L'
                              ELSE IND_FLUJO
                            END,
      -- ── Fechas reales por paso ──────────────────────────────
      -- ── Número de programa (BUG #41: NROPROG viene como p_id_origen en PASO '02') ──
      NUM_PROGRAMA        = CASE WHEN p_nuevo_paso='02' THEN p_id_origen ELSE NUM_PROGRAMA END,
      -- v3.0: NUM_PARTIDA se guarda cuando PARTIDA INSERT activa PASO '03' (En Hilanderia)
      NUM_PARTIDA         = CASE WHEN p_nuevo_paso='03' THEN p_id_origen ELSE NUM_PARTIDA END,
      FCH_REAL_PROGRAMADO = CASE WHEN p_nuevo_paso='02' THEN NVL(p_fch_evento,SYSDATE) ELSE FCH_REAL_PROGRAMADO END,
      FCH_REAL_PRODUCCION = CASE WHEN p_nuevo_paso='03' THEN NVL(p_fch_evento,SYSDATE) ELSE FCH_REAL_PRODUCCION END,
      -- v3.0: FCH_REAL_PRODUCCION en PASO '03' (Hilanderia/PARTIDA INSERT)
      -- FCH_REAL_PARTIDA: PASO '05' legado H_RPRODUC, PASO '04' (Lab = L_VALIDA_RECETA),
      --   y PASO '03' como fallback si lab no ha disparado (sin este fallback seria NULL)
      FCH_REAL_PARTIDA    = CASE WHEN p_nuevo_paso='05' THEN NVL(p_fch_evento,SYSDATE)
                                 WHEN p_nuevo_paso='04' THEN NVL(p_fch_evento,SYSDATE)
                                 WHEN p_nuevo_paso='03' AND FCH_REAL_PARTIDA IS NULL THEN NVL(p_fch_evento,SYSDATE)
                                 ELSE FCH_REAL_PARTIDA END,
      FCH_REAL_TIN_INI    = CASE WHEN p_nuevo_paso='06' THEN NVL(p_fch_evento,SYSDATE)
                                 WHEN p_nuevo_paso='9R' THEN NULL   ELSE FCH_REAL_TIN_INI    END,
      FCH_REAL_TIN_FIN    = CASE WHEN p_nuevo_paso='07' THEN NVL(p_fch_evento,SYSDATE)
                                 WHEN p_nuevo_paso='9R' THEN NULL   ELSE FCH_REAL_TIN_FIN    END,
      FCH_REAL_SECADO     = CASE WHEN p_nuevo_paso='08' THEN NVL(p_fch_evento,SYSDATE)
                                 WHEN p_nuevo_paso='9R' THEN NULL   ELSE FCH_REAL_SECADO     END,
      FCH_REAL_CC_TINTO   = CASE WHEN p_nuevo_paso='09'  THEN NVL(p_fch_evento,SYSDATE) ELSE FCH_REAL_CC_TINTO   END,
      FCH_REAL_CC_RECHAZO = CASE WHEN p_nuevo_paso='9R'  THEN NVL(p_fch_evento,SYSDATE) ELSE FCH_REAL_CC_RECHAZO END,
      FCH_REAL_DEVANADO   = CASE WHEN p_nuevo_paso='10'  THEN NVL(p_fch_evento,SYSDATE) ELSE FCH_REAL_DEVANADO   END,
      FCH_REAL_GASEADO    = CASE WHEN p_nuevo_paso='09B' THEN NVL(p_fch_evento,SYSDATE) ELSE FCH_REAL_GASEADO   END,  -- v2.0
      FCH_REAL_CALIDAD    = CASE WHEN p_nuevo_paso='11'  THEN NVL(p_fch_evento,SYSDATE) ELSE FCH_REAL_CALIDAD    END,
      FCH_REAL_ALM_PT     = CASE WHEN p_nuevo_paso='12'  THEN NVL(p_fch_evento,SYSDATE) ELSE FCH_REAL_ALM_PT     END,
      FCH_REAL_DESPACHO   = CASE WHEN p_nuevo_paso='14' AND v_nuevo_kg >= CANTIDAD_ORIG * 0.95
                                    THEN NVL(p_fch_evento,SYSDATE) ELSE FCH_REAL_DESPACHO END,
      -- ── KG acumulados ──────────────────────────────────────
      KG_PRODUCIDOS       = CASE WHEN p_nuevo_paso='03'
                                   THEN KG_PRODUCIDOS + NVL(p_kg_cantidad,0) ELSE KG_PRODUCIDOS END,
      KG_EN_TIN           = CASE WHEN p_nuevo_paso='06'
                                   THEN KG_EN_TIN    + NVL(p_kg_cantidad,0) ELSE KG_EN_TIN    END,
      KG_EN_ALM_PT        = CASE WHEN p_nuevo_paso='12'
                                   THEN KG_EN_ALM_PT + NVL(p_kg_cantidad,0) ELSE KG_EN_ALM_PT END,
      KG_DESPACHADOS      = CASE WHEN p_nuevo_paso='14'
                                   THEN v_nuevo_kg                           ELSE KG_DESPACHADOS END,
      KG_PENDIENTES       = CASE WHEN p_nuevo_paso='14'
                                   THEN GREATEST(KG_PENDIENTES - NVL(p_kg_cantidad,0), 0)
                                   ELSE KG_PENDIENTES END,
      -- ── Estado cierre ──────────────────────────────────────
      ESTADO              = CASE WHEN p_nuevo_paso='14' AND v_nuevo_kg >= CANTIDAD_ORIG * 0.95
                                    THEN 'C' ELSE ESTADO END,
      -- ── Indicadores reproceso / ciclo ──────────────────────
      IND_REPROCESO       = CASE WHEN p_nuevo_paso='9R' THEN 'S'
                                 ELSE IND_REPROCESO END,  -- INCONS-2 FIX: flag permanente; usar NRO_CICLO>1 para detectar historial de reproceso
      NRO_CICLO           = CASE WHEN p_nuevo_paso='9R' THEN NRO_CICLO + 1 ELSE NRO_CICLO END,
      -- ── Retraso ────────────────────────────────────────────
      -- BUG-2 CORREGIDO (21/05/2026): congelar DIAS_RETRASO al cerrar el item.
      -- Sin este fix, DIAS_RETRASO de items cerrados creceria indefinidamente.
      DIAS_RETRASO        = CASE
                              WHEN p_nuevo_paso = '14' AND v_nuevo_kg >= CANTIDAD_ORIG * 0.95
                              THEN NVL(GREATEST(TRUNC(SYSDATE) - TRUNC(FCH_ENTREGA_COMP), 0), 0)
                              WHEN ESTADO = 'C' THEN DIAS_RETRASO  -- item ya cerrado: congelado
                              ELSE NVL(GREATEST(TRUNC(SYSDATE) - TRUNC(FCH_ENTREGA_COMP), 0), 0)
                            END,
      IND_RETRASO         = CASE WHEN SYSDATE > FCH_ENTREGA_COMP THEN 'S' ELSE 'N' END,
      A_MDUSER            = USER,
      A_MDFECHA           = SYSDATE
    WHERE serie=p_serie AND num_ped=p_num_ped AND nro=p_nro AND num_det=p_num_det;

    -- Registrar evento
    SELECT PLN_SEQ_EVENTO.NEXTVAL INTO v_id_evt FROM DUAL;
    INSERT INTO PLN_LOG_EVENTOS (
      ID_EVENTO, ID_SEGUIM, SERIE, NUM_PED, NRO, NUM_DET,
      COD_PASO, TABLA_ORIGEN, ID_OBJETO_ORIGEN, FCH_EVENTO, USUARIO,
      KG_CANTIDAD, OBSERVACION, TIPO_EVENTO
    ) VALUES (
      v_id_evt, v_seg.id_seguim, p_serie, p_num_ped, p_nro, p_num_det,
      p_nuevo_paso, p_tabla_origen, p_id_origen, NVL(p_fch_evento,SYSDATE), USER,
      p_kg_cantidad, p_observacion,
      'AV'  -- '9R' es avance automático de paso (trigger), no reprogramación ('RE' es solo SP_PLN_REPROGRAMAR)
    );
    -- *** Sin COMMIT aquí ***
    -- ORA-04092: mismo motivo que SP_PLN_INIT_SEGUIMIENTO.
    -- SELECT FOR UPDATE bloquea la fila hasta el COMMIT de la transacción padre (correcto).
    -- Llamada manual desde app: BEGIN PKG_PLN.SP_PLN_AVANZA_PASO(...); COMMIT; END;

    -- Recalcular FCH_EST_* tras cada cambio de paso.
    -- SP_PLN_CALCULA_FECHAS re-lee PLN_SEGUIMIENTO (ve las FCH_REAL_* ya actualizadas
    -- en esta misma transacción) y ancla los estimados pendientes desde el último paso real.
    SP_PLN_CALCULA_FECHAS(p_serie, p_num_ped, p_nro, p_num_det, 'AV');

    -- ── AUTO-RESOLUCIÓN INMEDIATA DE ALERTAS ─────────────────────────────────
    -- El JOB horario (SP_PLN_GENERA_ALERTAS) limpia alertas obsoletas cada hora,
    -- pero cuando un trigger avanza el paso en tiempo real las alertas quedan
    -- activas hasta la próxima ejecución del JOB. Este bloque las resuelve al instante.
    -- NOTA: sin COMMIT aquí (ORA-04092 en triggers); el UPDATE entra en la misma TXN.

    -- R1: ítem cerrado → resolver TODO
    IF v_seg.estado = 'C' THEN
      UPDATE PLN_ALERTA SET
        ESTADO='R', FCH_RESOLUCION=SYSDATE,
        USUARIO_RESUELVE='AUTO', OBSERV_RESOL='Auto: ítem cerrado (paso 14)'
      WHERE id_seguim=v_seg.id_seguim AND estado='A';

    ELSE
      -- R2: SMP — ítem avanzó del PASO '01'
      IF v_orden_new >= 2 THEN
        UPDATE PLN_ALERTA SET
          ESTADO='R', FCH_RESOLUCION=SYSDATE,
          USUARIO_RESUELVE='AUTO', OBSERV_RESOL='Auto: ítem avanzó del paso 01'
        WHERE id_seguim=v_seg.id_seguim AND estado='A' AND tip_alerta='SMP';
      END IF;

      -- R3: STN — ítem entró a Tintorería (ORDEN paso '06' = 6)
      IF v_orden_new >= 6 THEN
        UPDATE PLN_ALERTA SET
          ESTADO='R', FCH_RESOLUCION=SYSDATE,
          USUARIO_RESUELVE='AUTO', OBSERV_RESOL='Auto: ítem ingresó a Tintorería'
        WHERE id_seguim=v_seg.id_seguim AND estado='A' AND tip_alerta='STN';
      END IF;

      -- R4: QCF — ítem salió del PASO '9R' (reproceso activo terminó)
      IF v_seg.cod_paso_act = '9R' AND p_nuevo_paso <> '9R' THEN
        UPDATE PLN_ALERTA SET
          ESTADO='R', FCH_RESOLUCION=SYSDATE,
          USUARIO_RESUELVE='AUTO', OBSERV_RESOL='Auto: ítem salió del paso 9R'
        WHERE id_seguim=v_seg.id_seguim AND estado='A' AND tip_alerta='QCF';
      END IF;

      -- R5: REPR — reproceso superó PASO '09' (CC TT Aprobado, ORDEN=9)
      IF v_orden_new >= 9 THEN
        UPDATE PLN_ALERTA SET
          ESTADO='R', FCH_RESOLUCION=SYSDATE,
          USUARIO_RESUELVE='AUTO', OBSERV_RESOL='Auto: reproceso superó CC TT'
        WHERE id_seguim=v_seg.id_seguim AND estado='A' AND tip_alerta='REPR';
      END IF;

      -- R6: RET1/RET2 — retraso recalculado, ya no supera umbral
      -- Se lee IND_RETRASO después del UPDATE principal (aplica al paso nuevo).
      DECLARE v_ind_ret VARCHAR2(1); BEGIN
        SELECT ind_retraso INTO v_ind_ret
        FROM PLN_SEGUIMIENTO WHERE id_seguim=v_seg.id_seguim;
        IF v_ind_ret = 'N' THEN
          UPDATE PLN_ALERTA SET
            ESTADO='R', FCH_RESOLUCION=SYSDATE,
            USUARIO_RESUELVE='AUTO', OBSERV_RESOL='Auto: retraso eliminado'
          WHERE id_seguim=v_seg.id_seguim AND estado='A'
            AND tip_alerta IN ('RET1','RET2');
        END IF;
      EXCEPTION WHEN NO_DATA_FOUND THEN NULL; END;
    END IF;
    -- ─────────────────────────────────────────────────────────────────────────

  EXCEPTION
    WHEN NO_DATA_FOUND THEN NULL;  -- seguimiento no existe aún → ignorar
    WHEN OTHERS        THEN RAISE; -- propaga; trigger absorbe con WHEN OTHERS THEN NULL
  END SP_PLN_AVANZA_PASO;


  -- ============================================================
  -- SP_PLN_CALCULA_FECHAS — Estimación de fechas del ciclo productivo
  -- ────────────────────────────────────────────────────────────
  -- Calcula todas las FCH_EST_* del ítem basándose en:
  --   · Capacidad de la máquina asignada (kgr_hr de CTRUTAS_TITULO)
  --   · Parámetros configurables de PLN_PARAM (HRS_*, DIAS_BUFFER_*)
  --   · Tiempo de tenido de TT_PARAMPROGTIN.tenido
  --
  -- PARÁMETROS:
  --   p_motivo : 'PED'=pedido / 'PLA'=planificado / 'REP'=reprogramado / 'MAQ'=máquina
  --              Guardado en PLN_FECHAS_ESTIMADAS.MOTIVO_RECALCULO para auditoría.
  --
  -- ALGORITMO (fechas calculadas en cascada):
  --   fch_base           = FCH_REAL_PROGRAMADO ?? SYSDATE
  --   FCH_EST_HILANDERIA = fch_base
  --   FCH_EST_PARTIDA    = fch_base + CEIL(cantidad_kg / (kgr_hr × HRS_HILANDERIA))
  --   FCH_EST_TIN_INI    = FCH_EST_PARTIDA + DIAS_BUFFER_LAB  ← buffer laboratorio
  --   FCH_EST_TIN_FIN    = FCH_EST_TIN_INI + (hrs_tenido / 24)
  --   FCH_EST_SECADO     = FCH_EST_TIN_FIN + (HRS_SECADO / 24)
  --   FCH_EST_CALIDAD    = TRUNC(FCH_EST_SECADO) + DIAS_BUFFER_QC
  --   FCH_EST_DESPACHO   = FCH_EST_CALIDAD + DIAS_BUFFER_DESP
  --
  -- SELECCIÓN DE kgr_hr (corrección aplicada vs. Propuesta.md):
  --   1. Si ITEMPED_DET.MAQUINA no es NULL:
  --        → buscar en CTRUTAS_TITULO WHERE titulo+proceso+cod_maq+estado≠'X'
  --   2. Si no hay máquina asignada o no se encontró:
  --        → usar MAX(kgr_hr) de CTRUTAS_TITULO para ese título+proceso
  --   3. Si aún es NULL o 0 → fallback = 10 kg/hora
  --
  -- EFECTOS SECUNDARIOS:
  --   · Actualiza PLN_SEGUIMIENTO.FCH_EST_* y recalcula IND_RETRASO
  --   · Inserta snapshot en PLN_FECHAS_ESTIMADAS (DIFER_DIAS respecto a anterior)
  --   · Sincroniza ITEMPED_DET.FCH_ESTIMA_TENIDO y FCH_ESTIMA_CONO_UNO
  --     (para compatibilidad con módulos legacy que leen esos campos)
  -- ============================================================
  PROCEDURE SP_PLN_CALCULA_FECHAS (
    p_serie     IN NUMBER,
    p_num_ped   IN NUMBER,
    p_nro       IN NUMBER,
    p_num_det   IN NUMBER,
    p_motivo    IN VARCHAR2  DEFAULT 'PED'
  ) AS
    v_seg      PLN_SEGUIMIENTO%ROWTYPE;
    v_item     ITEMPED%ROWTYPE;
    v_itemdet  ITEMPED_DET%ROWTYPE;
    v_maquina  VARCHAR2(6);     -- máquina asignada en ITEMPED_DET
    v_kgr_hr   NUMBER;           -- NULL inicial; fallback := 10 se aplica al final del bloque
    v_hrs_hil  NUMBER := 22;
    v_hrs_tin  NUMBER := 6;
    v_hrs_sec  NUMBER := 8;
    v_buf_lab  NUMBER := 1;     -- días buffer laboratorio
    v_buf_qc   NUMBER := 1;
    v_buf_desp NUMBER := 1;
    v_fch_base DATE;
    v_est_hil  DATE;
    v_est_part DATE;
    v_est_tini DATE;
    v_est_tfin DATE;
    v_est_sec  DATE;
    v_est_cal  DATE;
    v_est_desp DATE;
    v_id_fech  NUMBER;
  BEGIN
    SELECT * INTO v_seg  FROM PLN_SEGUIMIENTO WHERE serie=p_serie AND num_ped=p_num_ped AND nro=p_nro AND num_det=p_num_det;
    SELECT * INTO v_item FROM ITEMPED         WHERE serie=p_serie AND num_ped=p_num_ped AND nro=p_nro;

    BEGIN
      -- BUG-38 (25/05/2026): ITEMPED_DET puede tener múltiples filas para el mismo
      -- (SERIE,NUM_PED,NRO,NUM_DET) cuando hay reprocesos (cada reproceso genera
      -- un nuevo NROPROG). Seleccionar siempre el más reciente (NROPROG DESC).
      SELECT * INTO v_itemdet
      FROM (
        SELECT * FROM ITEMPED_DET
        WHERE serie=p_serie AND num_ped=p_num_ped AND nro=p_nro AND num_det=p_num_det
        ORDER BY nroprog DESC
      ) WHERE ROWNUM = 1;
      v_maquina := v_itemdet.maquina;
    EXCEPTION WHEN NO_DATA_FOUND THEN NULL;
    END;

    -- Parámetros
    BEGIN SELECT valor_num INTO v_hrs_hil  FROM PLN_PARAM WHERE cod_param='HRS_HILANDERIA';    EXCEPTION WHEN NO_DATA_FOUND THEN NULL; END;
    -- HRS_TINTORERIA no se usa aquí: la duración real del tenido viene de TT_PARAMPROGTIN (ver bloque abajo)
    BEGIN SELECT valor_num INTO v_hrs_sec  FROM PLN_PARAM WHERE cod_param='HRS_SECADO';        EXCEPTION WHEN NO_DATA_FOUND THEN NULL; END;
    BEGIN SELECT valor_num INTO v_buf_lab  FROM PLN_PARAM WHERE cod_param='DIAS_BUFFER_LAB';   EXCEPTION WHEN NO_DATA_FOUND THEN NULL; END;
    BEGIN SELECT valor_num INTO v_buf_qc   FROM PLN_PARAM WHERE cod_param='DIAS_BUFFER_QC';    EXCEPTION WHEN NO_DATA_FOUND THEN NULL; END;
    BEGIN SELECT valor_num INTO v_buf_desp FROM PLN_PARAM WHERE cod_param='DIAS_BUFFER_DESP';  EXCEPTION WHEN NO_DATA_FOUND THEN NULL; END;

    -- kgr_hr: PLN_KGR_TITULO (velocidades reales desde H_RPRODUC, formato numérico)
    -- Prioridad 1: máquina específica asignada al ítem
    -- Prioridad 2: fila fallback '*' (mediana de todas las máquinas para ese titulo/proceso)
    -- Prioridad 3: hardcoded 10 kg/hr (solo si no hay ningún dato histórico)
    -- NOTA: NO usa CTRUTAS_TITULO porque su columna TITULO usa notación textil ("14/2")
    --       incompatible con el código numérico de PLN_SEGUIMIENTO/H_RPRODUC ("014").
    IF v_maquina IS NOT NULL THEN
      BEGIN
        SELECT kgr_hr INTO v_kgr_hr
        FROM   PLN_KGR_TITULO
        WHERE  titulo  = v_item.titulo
          AND  proceso = v_item.proceso
          AND  cod_maq = v_maquina;
      EXCEPTION WHEN NO_DATA_FOUND THEN NULL;
      END;
    END IF;
    IF v_kgr_hr IS NULL THEN   -- sin máquina asignada, o máquina no tiene histórico
      BEGIN
        SELECT kgr_hr INTO v_kgr_hr
        FROM   PLN_KGR_TITULO
        WHERE  titulo  = v_item.titulo
          AND  proceso = v_item.proceso
          AND  cod_maq = '*';   -- fila fallback: mediana de todas las máquinas
      EXCEPTION WHEN NO_DATA_FOUND THEN NULL;
      END;
    END IF;
    IF NVL(v_kgr_hr, 0) = 0 THEN v_kgr_hr := 10; END IF;  -- fallback final

    -- Tiempo de tenido (horas) de TT_PARAMPROGTIN
    BEGIN
      SELECT NVL(tenido, 6) INTO v_hrs_tin
      FROM tt_paramprogtin WHERE ROWNUM = 1;
    EXCEPTION WHEN NO_DATA_FOUND THEN NULL;
    END;

    -- Fecha base: inicio del ciclo (programación real o SYSDATE)
    v_fch_base := NVL(v_seg.fch_real_programado, SYSDATE);

    -- Cálculo de fechas: si el paso ya ocurrió usa la FCH_REAL_* como ancla;
    -- si no, proyecta desde el paso anterior estimado o real.
    -- BUG #37: usar v_seg.cantidad_orig (PLN_SEGUIMIENTO) en vez de v_item.cantidad (ITEMPED).
    v_est_hil  := NVL(v_seg.fch_real_produccion,
                      TRUNC(v_fch_base));
    v_est_part := NVL(v_seg.fch_real_partida,
                      TRUNC(NVL(v_seg.fch_real_produccion, v_fch_base))
                      + CEIL(v_seg.cantidad_orig / NULLIF(v_kgr_hr * v_hrs_hil, 0)));
    v_est_tini := NVL(v_seg.fch_real_tin_ini,
                      NVL(v_seg.fch_real_partida, v_est_part) + v_buf_lab);
    v_est_tfin := NVL(v_seg.fch_real_tin_fin,
                      NVL(v_seg.fch_real_tin_ini, v_est_tini) + (v_hrs_tin / 24));
    v_est_sec  := NVL(v_seg.fch_real_secado,
                      NVL(v_seg.fch_real_tin_fin, v_est_tfin) + (v_hrs_sec / 24));
    v_est_cal  := NVL(v_seg.fch_real_calidad,
                      TRUNC(NVL(v_seg.fch_real_gaseado,    -- v2.0: PASO '09B' (PROCESO='24') es el último hito pre-calidad
                              NVL(v_seg.fch_real_secado, v_est_sec))) + v_buf_qc);
    v_est_desp := NVL(v_seg.fch_real_despacho,
                      NVL(v_seg.fch_real_calidad, v_est_cal)  + v_buf_desp);

    -- Actualizar PLN_SEGUIMIENTO
    UPDATE PLN_SEGUIMIENTO SET
      FCH_EST_HILANDERIA = v_est_hil,
      FCH_EST_PARTIDA    = v_est_part,
      FCH_EST_TIN_INI    = v_est_tini,
      FCH_EST_TIN_FIN    = v_est_tfin,
      FCH_EST_SECADO     = v_est_sec,
      FCH_EST_CALIDAD    = v_est_cal,
      FCH_EST_DESPACHO   = v_est_desp,
      DIAS_RETRASO       = NVL(GREATEST(TRUNC(SYSDATE) - TRUNC(FCH_ENTREGA_COMP), 0), 0),
      -- BUG-3 CORREGIDO (21/05/2026): unificado con SP_PLN_AVANZA_PASO
      -- La version anterior usaba v_est_desp > FCH_ENTREGA_COMP (estimado, no real).
      IND_RETRASO        = CASE WHEN SYSDATE > FCH_ENTREGA_COMP THEN 'S' ELSE 'N' END,
      A_MDUSER           = USER,
      A_MDFECHA          = SYSDATE
    WHERE id_seguim = v_seg.id_seguim;

    -- Historial de recálculo
    SELECT PLN_SEQ_FECHAS.NEXTVAL INTO v_id_fech FROM DUAL;
    INSERT INTO PLN_FECHAS_ESTIMADAS (
      ID_FECH, ID_SEGUIM, FCH_CALCULO, MOTIVO_RECALCULO,
      FCH_EST_HILANDERIA, FCH_EST_PARTIDA, FCH_EST_TIN_INI,
      FCH_EST_TIN_FIN, FCH_EST_SECADO, FCH_EST_CALIDAD, FCH_EST_DESPACHO,
      DIFER_DIAS, USUARIO
    ) VALUES (
      v_id_fech, v_seg.id_seguim, SYSDATE, p_motivo,
      v_est_hil, v_est_part, v_est_tini,
      v_est_tfin, v_est_sec, v_est_cal, v_est_desp,
      TRUNC(v_est_desp) - TRUNC(NVL(v_seg.fch_est_despacho, v_est_desp)),
      USER
    );

    -- DIRECTIVA: Las tablas legacy son solo lectura para PLN_.
    -- FCH_EST_TIN_INI y FCH_EST_TIN_FIN ya están en PLN_SEGUIMIENTO arriba.
    -- El UPDATE a ITEMPED_DET.FCH_ESTIMA_TENIDO/CONO_UNO fue eliminado (22/05/2026).

    -- *** Sin COMMIT aquí ***
    -- ORA-04092: mismo motivo. El COMMIT lo hace la transacción padre.
    -- Llamada manual desde app: BEGIN PKG_PLN.SP_PLN_CALCULA_FECHAS(...); COMMIT; END;

  EXCEPTION
    WHEN NO_DATA_FOUND THEN NULL;  -- ítem no existe aún (trigger fuera de orden) → ignorar
    WHEN OTHERS        THEN RAISE;
  END SP_PLN_CALCULA_FECHAS;


  -- ============================================================
  -- SP_PLN_GENERA_ALERTAS — Motor automático de alertas
  -- ────────────────────────────────────────────────────────────
  -- Escanea PLN_SEGUIMIENTO buscando situaciones anómalas.
  -- Ejecutar vía JOB_PLN_ALERTAS (FREQ=HOURLY; BYMINUTE=0).
  -- Si se invoca manualmente, es idempotente (NOT EXISTS evita duplicados).
  --
  -- ALERTAS GENERADAS (en este orden):
  --   'RET1' Nivel 'C' → dias_retraso >= DIAS_ALERTA_CRIT (default 7)
  --   'RET2' Nivel 'A' → dias_retraso >= DIAS_ALERTA_ALTA (default 3) y < CRIT
  --   'SMP'  Nivel 'A' → PASO='01' más de 2 días sin planificación
  --   'STN'  Nivel 'C' → PASO='03' y SYSDATE > FCH_EST_TIN_INI (esperando TT)
  --   'QCF'  Nivel 'C' → PASO='9R' (CC rechazado, ciclo activo en reproceso)
  --   'REPR' Nivel 'A' → IND_REPROCESO='S' y PASO<>'9R' (ciclo 2+ ya retomó flujo)
  --
  -- ANTI-DUPLICADO:
  --   INSERT ... WHERE NOT EXISTS (SELECT 1 FROM PLN_ALERTA
  --     WHERE id_seguim=X AND tip_alerta=Y AND estado='A')
  --   Garantiza una sola alerta activa del mismo tipo por ítem.
  --
  -- RESOLUCIÓN DE ALERTAS (no está en este SP):
  --   Desde Alertas.cshtml: POST /Produccion/Planeamiento/ResolverAlerta
  --     UPDATE PLN_ALERTA SET ESTADO='R', FCH_RESOLUCION=SYSDATE,
  --                           USUARIO_RESUELVE=:usr WHERE id_alerta=:id
  --   POST /Produccion/Planeamiento/IgnorarAlerta → ESTADO='I'
  --
  -- HELPER INTERNO: ins_alerta (procedure local, no expuesto en SPEC)
  -- ============================================================
  PROCEDURE SP_PLN_GENERA_ALERTAS AS
    v_dias_crit  NUMBER := 7;
    v_dias_alta  NUMBER := 3;
    v_dias_media NUMBER := 1;

    PROCEDURE ins_alerta (
      p_id_seg NUMBER, p_serie NUMBER, p_ped NUMBER, p_nro NUMBER,
      p_det NUMBER, p_tip VARCHAR2, p_nivel VARCHAR2,
      p_titulo VARCHAR2, p_detalle VARCHAR2, p_dias NUMBER,
      p_cli VARCHAR2
    ) IS
    BEGIN
      INSERT INTO PLN_ALERTA (
        ID_ALERTA, ID_SEGUIM, SERIE, NUM_PED, NRO, NUM_DET,
        TIP_ALERTA, NIVEL, TITULO, DETALLE, FCH_ALERTA,
        DIAS_RETRASO, COD_CLIENTE, ESTADO, A_ADUSER, A_ADFECHA
      )
      SELECT PLN_SEQ_ALERTA.NEXTVAL, p_id_seg, p_serie, p_ped, p_nro, p_det,
             p_tip, p_nivel, p_titulo, p_detalle, SYSDATE,
             p_dias, p_cli, 'A', USER, SYSDATE
      FROM DUAL
      WHERE NOT EXISTS (
        SELECT 1 FROM PLN_ALERTA
        WHERE id_seguim=p_id_seg AND tip_alerta=p_tip AND estado='A'
      );
    END ins_alerta;

  BEGIN
    BEGIN SELECT valor_num INTO v_dias_crit  FROM PLN_PARAM WHERE cod_param='DIAS_ALERTA_CRIT';  EXCEPTION WHEN NO_DATA_FOUND THEN NULL; END;
    BEGIN SELECT valor_num INTO v_dias_alta  FROM PLN_PARAM WHERE cod_param='DIAS_ALERTA_ALTA';  EXCEPTION WHEN NO_DATA_FOUND THEN NULL; END;
    BEGIN SELECT valor_num INTO v_dias_media FROM PLN_PARAM WHERE cod_param='DIAS_ALERTA_MEDIA'; EXCEPTION WHEN NO_DATA_FOUND THEN NULL; END;

    -- ── AUTO-RESOLUCIÓN DE ALERTAS OBSOLETAS ────────────────────────────────
    -- Se ejecuta ANTES de generar nuevas, para que el anti-duplicado NOT EXISTS
    -- no bloquee la re-inserción cuando la condición volvió a ser cierta.

    -- R1: ítem cerrado (PASO 14 / estado='C') → cualquier tipo de alerta ya no aplica
    UPDATE PLN_ALERTA a SET a.estado='R', a.fch_resolucion=SYSDATE,
           a.usuario_resuelve='AUTO', a.observ_resol='Auto: ítem cerrado'
    WHERE  a.estado='A'
      AND  EXISTS (SELECT 1 FROM PLN_SEGUIMIENTO s
                   WHERE s.id_seguim=a.id_seguim AND s.estado='C');

    -- R2: SMP (Sin Programa) → ítem ya avanzó del PASO '01'
    UPDATE PLN_ALERTA a SET a.estado='R', a.fch_resolucion=SYSDATE,
           a.usuario_resuelve='AUTO', a.observ_resol='Auto: ítem avanzó del paso 01'
    WHERE  a.estado='A' AND a.tip_alerta='SMP'
      AND  EXISTS (SELECT 1 FROM PLN_SEGUIMIENTO s
                   WHERE s.id_seguim=a.id_seguim AND s.estado='A'
                     AND s.cod_paso_act != '01');

    -- R3: STN (Sin Tintorería) → ítem ya superó el paso 03 (ORDEN_PASO >= 6)
    UPDATE PLN_ALERTA a SET a.estado='R', a.fch_resolucion=SYSDATE,
           a.usuario_resuelve='AUTO', a.observ_resol='Auto: ítem ingresó a TT o más allá'
    WHERE  a.estado='A' AND a.tip_alerta='STN'
      AND  EXISTS (SELECT 1 FROM PLN_SEGUIMIENTO s
                   JOIN PLN_ESTADO_CODIGO ec ON ec.cod_paso=s.cod_paso_act
                   WHERE s.id_seguim=a.id_seguim AND s.estado='A'
                     AND ec.orden_paso >= 6);

    -- R4: RET1/RET2 → ítem ya no está retrasado (FCH_ENTREGA_COMP fue corregida o avanzó)
    UPDATE PLN_ALERTA a SET a.estado='R', a.fch_resolucion=SYSDATE,
           a.usuario_resuelve='AUTO', a.observ_resol='Auto: retraso eliminado'
    WHERE  a.estado='A' AND a.tip_alerta IN ('RET1','RET2')
      AND  EXISTS (SELECT 1 FROM PLN_SEGUIMIENTO s
                   WHERE s.id_seguim=a.id_seguim AND s.estado='A'
                     AND s.ind_retraso='N');

    -- R5: QCF (CC rechazado) → ítem ya salió del PASO '9R'
    UPDATE PLN_ALERTA a SET a.estado='R', a.fch_resolucion=SYSDATE,
           a.usuario_resuelve='AUTO', a.observ_resol='Auto: reproceso retomó flujo'
    WHERE  a.estado='A' AND a.tip_alerta='QCF'
      AND  EXISTS (SELECT 1 FROM PLN_SEGUIMIENTO s
                   WHERE s.id_seguim=a.id_seguim AND s.estado='A'
                     AND s.cod_paso_act != '9R');

    -- R6: REPR → reproceso superó PASO '09' (ya absorbido en flujo normal)
    UPDATE PLN_ALERTA a SET a.estado='R', a.fch_resolucion=SYSDATE,
           a.usuario_resuelve='AUTO', a.observ_resol='Auto: reproceso superó CC TT'
    WHERE  a.estado='A' AND a.tip_alerta='REPR'
      AND  EXISTS (SELECT 1 FROM PLN_SEGUIMIENTO s
                   JOIN PLN_ESTADO_CODIGO ec ON ec.cod_paso=s.cod_paso_act
                   WHERE s.id_seguim=a.id_seguim AND s.estado='A'
                     AND ec.orden_paso >= 9);
    -- ────────────────────────────────────────────────────────────────────────

    -- fix BUG-PLN-1: recalcular DIAS_RETRASO/IND_RETRASO antes de escanear alertas.
    -- SP_PLN_AVANZA_PASO solo los actualiza al cambiar de paso; ítems que llevan
    -- días sin moverse acumularían retraso real sin que la columna lo refleje,
    -- por lo que las alertas RET1/RET2 nunca se generarían para esos ítems.
    -- FIX-NVL: GREATEST(0, NULL) = NULL en Oracle → usar NVL para ítems sin FCH_ENTREGA_COMP.
    UPDATE PLN_SEGUIMIENTO
    SET    dias_retraso = NVL(GREATEST(0, TRUNC(SYSDATE) - TRUNC(fch_entrega_comp)), 0),
           ind_retraso  = CASE WHEN fch_entrega_comp IS NOT NULL
                                    AND TRUNC(SYSDATE) > TRUNC(fch_entrega_comp)
                               THEN 'S' ELSE 'N' END
    WHERE  estado = 'A'
      AND  cod_paso_act <> '14';

    -- Retraso CRÍTICO (>= 7 días)
    FOR r IN (SELECT id_seguim, serie, num_ped, nro, num_det, cod_cliente, dias_retraso
              FROM PLN_SEGUIMIENTO
              WHERE estado='A' AND cod_paso_act != '14' AND dias_retraso >= v_dias_crit) LOOP
      ins_alerta(r.id_seguim, r.serie, r.num_ped, r.nro, r.num_det, 'RET1', 'C',
                 'Retraso crítico > '||v_dias_crit||' días',
                 'Ped '||r.num_ped||' ítem '||r.nro||': '||r.dias_retraso||' días de retraso.',
                 r.dias_retraso, r.cod_cliente);
    END LOOP;

    -- Retraso ALTO (3..v_dias_crit-1 días)
    FOR r IN (SELECT id_seguim, serie, num_ped, nro, num_det, cod_cliente, dias_retraso
              FROM PLN_SEGUIMIENTO
              WHERE estado='A' AND cod_paso_act != '14'
                AND dias_retraso >= v_dias_alta AND dias_retraso < v_dias_crit) LOOP
      ins_alerta(r.id_seguim, r.serie, r.num_ped, r.nro, r.num_det, 'RET2', 'A',
                 'Retraso alto '||r.dias_retraso||' días',
                 'Ped '||r.num_ped||' ítem '||r.nro||': '||r.dias_retraso||' días de retraso.',
                 r.dias_retraso, r.cod_cliente);
    END LOOP;

    -- Sin programa asignado > 2 días tras el pedido
    FOR r IN (SELECT s.id_seguim, s.serie, s.num_ped, s.nro, s.num_det, s.cod_cliente
              FROM PLN_SEGUIMIENTO s
              WHERE s.estado='A' AND s.cod_paso_act='01'
                AND TRUNC(SYSDATE) - TRUNC(s.fch_pedido) > 2) LOOP
      ins_alerta(r.id_seguim, r.serie, r.num_ped, r.nro, r.num_det, 'SMP', 'A',
                 'Sin programa asignado',
                 'Ped '||r.num_ped||' ítem '||r.nro||': más de 2 días sin planificación.',
                 NULL, r.cod_cliente);
    END LOOP;

    -- Partida sin ingresar a TT después de FCH_EST_TIN_INI
    FOR r IN (SELECT s.id_seguim, s.serie, s.num_ped, s.nro, s.num_det, s.cod_cliente
              FROM PLN_SEGUIMIENTO s
              WHERE s.estado='A' AND s.cod_paso_act='03'
                AND TRUNC(SYSDATE) > TRUNC(NVL(s.fch_est_tin_ini, SYSDATE))) LOOP
      ins_alerta(r.id_seguim, r.serie, r.num_ped, r.nro, r.num_det, 'STN', 'C',
                 'Partida sin ingresar a Tintorería',
                 'Ped '||r.num_ped||': partida lista pero no ingresó a TT.',
                 NULL, r.cod_cliente);
    END LOOP;

    -- CC rechazado (reproceso activo)
    FOR r IN (SELECT s.id_seguim, s.serie, s.num_ped, s.nro, s.num_det,
                     s.cod_cliente, s.nro_ciclo
              FROM PLN_SEGUIMIENTO s
              WHERE s.estado='A' AND s.cod_paso_act='9R') LOOP
      ins_alerta(r.id_seguim, r.serie, r.num_ped, r.nro, r.num_det, 'QCF', 'C',
                 'Partida en reproceso (CC rechazado)',
                 'Ped '||r.num_ped||' ítem '||r.nro||': CC rechazado. Ciclo '||r.nro_ciclo||'.',
                 NULL, r.cod_cliente);
    END LOOP;

    -- Reproceso en ciclo 2+ (IND_REPROCESO='S', ya retomó la cadena de producción)
    -- Distinto de QCF: el ítem no está bloqueado en '9R', viene de un rechazo anterior
    -- y ya avanzó al nuevo ciclo (PASO '03'..'13'). Alerta informativa para supervisión.
    -- BUG-H FIX: excluir ítems que ya superaron el paso '09' en el nuevo ciclo.
    -- Sin este filtro la alerta REPR se regenera cada hora aunque el supervisor la resuelva.
    -- Confirmado en BD: 4 alertas REPR activas para ítems en pasos '03','08','10' (NRO_CICLO=2).
    FOR r IN (SELECT s.id_seguim, s.serie, s.num_ped, s.nro, s.num_det,
                     s.cod_cliente, s.nro_ciclo, s.cod_paso_act,
                     TO_CHAR(s.fch_real_cc_rechazo,'DD/MM/YY') AS fch_rechaz
              FROM PLN_SEGUIMIENTO s
              WHERE s.estado='A' AND s.ind_reproceso='S'
                AND s.cod_paso_act <> '9R'
                AND s.cod_paso_act NOT IN ('09','09B','10','11','12','13','14')) LOOP
      ins_alerta(r.id_seguim, r.serie, r.num_ped, r.nro, r.num_det, 'REPR', 'A',
                 'Reproceso Ciclo '||r.nro_ciclo||' en progreso',
                 'Ped '||r.num_ped||' item '||r.nro
                   ||': ciclo '||r.nro_ciclo||' en curso (CC rechazado '
                   ||NVL(r.fch_rechaz,'?')||'). Paso: '||r.cod_paso_act||'.',
                 NULL, r.cod_cliente);
    END LOOP;

    COMMIT;
  END SP_PLN_GENERA_ALERTAS;


  -- ============================================================
  -- SP_PLN_CARGA_DIARIA_REFRESH — Actualización de carga de máquinas
  -- ────────────────────────────────────────────────────────────
  -- Regenera PLN_CARGA_DIARIA para el rango p_fch_ini..p_fch_fin.
  -- Por defecto: TRUNC(SYSDATE)..TRUNC(SYSDATE)+30 (próximos 30 días).
  -- Ejecutar vía JOB_PLN_CARGA (FREQ=HOURLY; INTERVAL=4; BYMINUTE=0).
  --
  -- OPERACIÓN:
  --   1. DELETE FROM PLN_CARGA_DIARIA WHERE fecha BETWEEN fch_ini AND fch_fin
  --   2. INSERT hilandería  → H_RPRODUC ESTADO='3'
  --      FIX-J: reemplaza H_PRODUCCION_D (sin datos desde 14/05/2026).
  --      Horas = (FECHA_FIN-FECHA_INI)*24; KG = PESO_NETO. Excluye TP_MAQ='G'.
  --   3. INSERT tintorería REAL → TT_RPRODUC TIPODOC='IR' ESTADO='3'
  --      → puebla HORAS_REAL / KG_REAL (baños ya terminados)
  --   4. MERGE  tintorería COLA → TT_RPRODUC TIPODOC='IR' ESTADO='1' (en proceso)
  --      FIX-K: nueva lógica de cola activa.
  --      Duración estimada = mediana histórica 90d por máquina (fallback 8h).
  --      FECHA_FIN_EST = TRUNC(FECHA_INI + hrs_med/24).
  --      → puebla HORAS_ASIGNADAS / KG_ASIGNADOS / NRO_PEDIDOS
  --      MERGE: si ya hay fila (producción real ese día) → UPDATE los campos de cola
  --             si no hay fila (día futuro) → INSERT fila nueva con datos de cola
  --   5. UPDATE PCT_UTILIZACION / PCT_CARGA / IND_SOBRECARGADA
  --      PCT_UTILIZACION = HORAS_REAL / HORAS_CAPACIDAD * 100    (producido)
  --      PCT_CARGA       = (HORAS_REAL+HORAS_ASIGNADAS) / HORAS_CAPACIDAD * 100 (total carga)
  --      IND_SOBRECARGADA = 'S' si PCT_CARGA > 100%
  --
  -- CAPACIDAD por tipo:
  --   Hilandería  → PLN_PARAM.COD_PARAM='HRS_HILANDERIA'  (default 22 h/día)
  --   Tintorería  → PLN_PARAM.COD_PARAM='HRS_TINTORERIA'  (default 24 h/día)
  --
  -- TP_MAQ:
  --   Hilandería → valor directo de H_RPRODUC.TP_MAQ (A,B,C,E,L,M,P,R,T)
  --   Tintorería → 'W' (Wet processing; Thies R01-R19, Matisa MR, Hank M01-M08)
  -- ============================================================
  PROCEDURE SP_PLN_CARGA_DIARIA_REFRESH (
    p_fch_ini IN DATE DEFAULT TRUNC(SYSDATE),
    p_fch_fin IN DATE DEFAULT TRUNC(SYSDATE) + 30
  ) AS
    v_hrs_tt  NUMBER := 24;   -- capacidad h/día tintorería (default)
    v_hrs_hil NUMBER := 22;   -- capacidad h/día hilandería (default)
  BEGIN
    -- Leer capacidades configuradas en PLN_PARAM
    SELECT NVL(MAX(CASE WHEN cod_param = 'HRS_TINTORERIA' THEN valor_num END), 24),
           NVL(MAX(CASE WHEN cod_param = 'HRS_HILANDERIA'  THEN valor_num END), 22)
    INTO   v_hrs_tt, v_hrs_hil
    FROM   PLN_PARAM
    WHERE  cod_param IN ('HRS_TINTORERIA', 'HRS_HILANDERIA');

    DELETE FROM PLN_CARGA_DIARIA
    WHERE fecha BETWEEN p_fch_ini AND p_fch_fin;

    -- 1. Hilandería: producción real desde H_RPRODUC (FIX-J: reemplaza H_PRODUCCION_D)
    --    H_PRODUCCION_D dejó de actualizarse el 14/05/2026; H_RPRODUC es la fuente activa.
    --    FIX-L (ORA-01438): Las autoconeadoras (TP_MAQ='A') tienen N spindles simultáneos.
    --    SUM de tiempos de spindle = varios cientos de horas/día → desborda NUMBER(5,2).
    --    Solución: HORAS_REAL = tiempo CALENDARIO = (MAX(fecha_fin) - MIN(fecha_ini)) * 24,
    --    que representa cuántas horas del día estuvo activa la máquina (≤ 24h siempre).
    --    KG_REAL sigue siendo SUM(peso_neto) — suma real de producción.
    INSERT INTO PLN_CARGA_DIARIA (
      FECHA, COD_MAQ, TP_MAQ,
      HORAS_CAPACIDAD, HORAS_REAL, KG_REAL,
      FCH_CALCULO, A_MDFECHA
    )
    SELECT
      TRUNC(h.fecha_ini),
      h.cod_maq,
      h.tp_maq,
      v_hrs_hil,
      ROUND(LEAST(
        (MAX(h.fecha_fin) - MIN(h.fecha_ini)) * 24,
        v_hrs_hil
      ), 2),
      ROUND(SUM(NVL(h.peso_neto, 0)), 4),
      SYSDATE, SYSDATE
    FROM h_rproduc h
    WHERE h.estado    = '3'
      AND h.tp_maq    NOT IN ('G')
      AND h.peso_neto > 0
      AND TRUNC(h.fecha_ini) BETWEEN p_fch_ini AND p_fch_fin
    GROUP BY TRUNC(h.fecha_ini), h.cod_maq, h.tp_maq;

    -- 2. Tintorería REAL: baños terminados → HORAS_REAL / KG_REAL
    --    Navegación IR: TT_RPRODUC.RECETA → PARTIDA_MAS.NUMERO (tp_transac='IR')
    --                   PARTIDA_MAS.PARTIDA → PARTIDA.NUMERO (para PESO_NETO)
    INSERT INTO PLN_CARGA_DIARIA (
      FECHA, COD_MAQ, TP_MAQ,
      HORAS_CAPACIDAD, HORAS_REAL, KG_REAL,
      FCH_CALCULO, A_MDFECHA
    )
    SELECT
      TRUNC(tt.fecha_ini),
      tt.cod_maq,
      'W',
      v_hrs_tt,
      ROUND(SUM((tt.fecha_fin - tt.fecha_ini) * 24), 2),
      ROUND(SUM(NVL(p.peso_neto, 0)), 4),
      SYSDATE, SYSDATE
    FROM   tt_rproduc tt
    JOIN   partida_mas pm ON pm.numero = tt.receta AND pm.tp_transac = 'IR'
    LEFT   JOIN partida p ON p.numero = pm.partida
    WHERE  tt.tipodoc = 'IR'
      AND  tt.estado  = '3'
      AND  tt.fecha_ini IS NOT NULL
      AND  tt.fecha_fin IS NOT NULL
      AND  TRUNC(tt.fecha_ini) BETWEEN p_fch_ini AND p_fch_fin
    GROUP BY TRUNC(tt.fecha_ini), tt.cod_maq;

    -- 3. Tintorería COLA: baños en proceso (ESTADO='1') → HORAS_ASIGNADAS / KG_ASIGNADOS
    --    FIX-K: permite ver la cola activa de cada máquina en el Heatmap.
    --    Duración estimada = MEDIAN de (fecha_fin-fecha_ini)*24 de baños completados
    --    en los últimos 90 días para esa máquina. Fallback: 8h si no hay historial.
    --    FECHA_FIN_EST = TRUNC(FECHA_INI + hrs_med/24) → determina el día en que ocupa.
    --    MERGE (no INSERT): la fila puede ya existir si la máquina tuvo baños terminados
    --    el mismo día (ej: máquina con 2 baños: uno completado ESTADO='3' + otro en curso).
    MERGE INTO PLN_CARGA_DIARIA cd
    USING (
      SELECT
        TRUNC(tt.fecha_ini + NVL(hist.hrs_med, 8) / 24)  AS fecha,
        tt.cod_maq,
        'W'                                               AS tp_maq,
        v_hrs_tt                                          AS horas_capacidad,
        ROUND(SUM(NVL(hist.hrs_med, 8)), 2)               AS horas_asignadas,
        ROUND(SUM(NVL(p.peso_neto, 0)), 4)                AS kg_asignados,
        COUNT(*)                                          AS nro_pedidos
      FROM   tt_rproduc tt
      JOIN   partida_mas pm ON pm.numero = tt.receta AND pm.tp_transac = 'IR'
      LEFT   JOIN partida p ON p.numero = pm.partida
      LEFT   JOIN (
        -- Mediana histórica 90d de duración de baños por máquina
        SELECT cod_maq,
               MEDIAN((fecha_fin - fecha_ini) * 24) AS hrs_med
        FROM   tt_rproduc
        WHERE  estado  = '3'
          AND  tipodoc = 'IR'
          AND  fecha_ini IS NOT NULL
          AND  fecha_fin > fecha_ini
          AND  fecha_ini >= TRUNC(SYSDATE) - 90
          AND  (fecha_fin - fecha_ini) * 24 BETWEEN 0.5 AND 100
        GROUP BY cod_maq
      ) hist ON hist.cod_maq = tt.cod_maq
      WHERE  tt.estado  = '1'
        AND  tt.tipodoc = 'IR'
        AND  tt.fecha_ini IS NOT NULL
        AND  TRUNC(tt.fecha_ini + NVL(hist.hrs_med, 8) / 24)
             BETWEEN p_fch_ini AND p_fch_fin
      GROUP  BY TRUNC(tt.fecha_ini + NVL(hist.hrs_med, 8) / 24), tt.cod_maq
    ) cola ON (cd.fecha = cola.fecha AND cd.cod_maq = cola.cod_maq)
    WHEN MATCHED THEN
      UPDATE SET
        cd.horas_asignadas = cola.horas_asignadas,
        cd.kg_asignados    = cola.kg_asignados,
        cd.nro_pedidos     = cola.nro_pedidos,
        cd.a_mdfecha       = SYSDATE
    WHEN NOT MATCHED THEN
      INSERT (fecha, cod_maq, tp_maq, horas_capacidad,
              horas_asignadas, kg_asignados, nro_pedidos,
              horas_real, kg_real,
              fch_calculo, a_mdfecha)
      VALUES (cola.fecha, cola.cod_maq, cola.tp_maq, cola.horas_capacidad,
              cola.horas_asignadas, cola.kg_asignados, cola.nro_pedidos,
              0, 0,
              SYSDATE, SYSDATE);

    -- 4. Calcular porcentajes finales
    --    PCT_UTILIZACION : lo producido hasta ahora (solo baños/runs terminados)
    --    PCT_CARGA       : carga total = producido + cola activa estimada
    --    IND_SOBRECARGADA: 'S' si la carga total supera la capacidad del día
    --    LEAST(..., 999.99): cap de seguridad para NUMBER(5,2); evita ORA-01438
    --    en máquinas multi-spindle (autoconer) si se agrega otro tipo de datos.
    UPDATE PLN_CARGA_DIARIA SET
      PCT_UTILIZACION  = LEAST(ROUND(HORAS_REAL / NULLIF(HORAS_CAPACIDAD, 0) * 100, 2), 999.99),
      PCT_CARGA        = LEAST(ROUND((HORAS_REAL + HORAS_ASIGNADAS) / NULLIF(HORAS_CAPACIDAD, 0) * 100, 2), 999.99),
      IND_SOBRECARGADA = CASE WHEN (HORAS_REAL + HORAS_ASIGNADAS) > HORAS_CAPACIDAD
                              THEN 'S' ELSE 'N' END
    WHERE fecha BETWEEN p_fch_ini AND p_fch_fin;

    COMMIT;
  END SP_PLN_CARGA_DIARIA_REFRESH;


  -- ============================================================
  -- SP_PLN_KGR_REFRESH — Recálculo de velocidades kg/hora desde H_RPRODUC
  -- ────────────────────────────────────────────────────────────
  -- Propósito:
  --   Poblar PLN_KGR_TITULO con la velocidad de producción real (kg/hora)
  --   por combinación (TITULO, PROCESO, COD_MAQ), derivada de los registros
  --   completados en H_RPRODUC.
  --
  -- Por qué existe y no usa CTRUTAS_TITULO:
  --   CTRUTAS_TITULO usa notación textil ("14/2", "20/1") mientras que
  --   PLN_SEGUIMIENTO y H_RPRODUC usan código numérico ("014", "076").
  --   El JOIN nunca encuentra nada → SP_PLN_CALCULA_FECHAS cae siempre al
  --   fallback de 10 kg/hr → fechas estimadas incorrectas para todos los ítems.
  --
  -- Lógica de cálculo:
  --   kgr_hr = PESO_NETO / ((FECHA_FIN - FECHA_INI) * 24)
  --   · Solo runs completados: H_RPRODUC.ESTADO = '3'
  --   · Solo con peso real:    PESO_NETO > 0
  --   · Solo con duración válida: FECHA_FIN > FECHA_INI
  --   · Filtra duración aberrante: entre 0.5 y 500 horas (outliers de datos)
  --   · Ventana temporal: últimos p_meses meses (default 24)
  --   · Mínimo p_min_muestras runs por combinación (default 3)
  --
  -- Usa PERCENTILE_CONT(0.5) (mediana) en vez de AVG:
  --   La producción tiene outliers frecuentes (paradas por turno, cambios de lote).
  --   La mediana es robusta ante estos casos; el AVG inflaría o desinflaría el valor.
  --
  -- Filas generadas:
  --   A) Por (TITULO, PROCESO, COD_MAQ específica): mediana de esa máquina.
  --      Usada cuando v_maquina IS NOT NULL en SP_PLN_CALCULA_FECHAS.
  --   B) Por (TITULO, PROCESO, '*'): mediana de TODAS las máquinas combinadas.
  --      Fallback cuando no hay máquina asignada al ítem.
  --
  -- Cobertura verificada (28/05/2026):
  --   · 730/878 ítems activos (83%) cubiertos con >= 3 muestras (24 meses).
  --   · 14 ítems con 1-2 muestras: incluidos con lo que hay (valor real > fallback 10).
  --   · 134 ítems sin historial (TITULO/PROCESO combinaciones nuevas o de stock):
  --     mantienen el fallback de 10 kg/hr hasta acumular datos.
  --
  -- Ejecución:
  --   · Automática: JOB_PLN_KGR (día 1 de cada mes a las 01:00)
  --   · Manual: BEGIN PKG_PLN.SP_PLN_KGR_REFRESH; END;
  --   · Con parámetros: BEGIN PKG_PLN.SP_PLN_KGR_REFRESH(p_meses=>12, p_min_muestras=>5); END;
  -- ============================================================
  PROCEDURE SP_PLN_KGR_REFRESH (
    p_meses        IN NUMBER DEFAULT 24,
    p_min_muestras IN NUMBER DEFAULT 3
  ) AS
    v_fch_desde  DATE := ADD_MONTHS(TRUNC(SYSDATE), -p_meses);
    v_cnt_maq    PLS_INTEGER := 0;
    v_cnt_fall   PLS_INTEGER := 0;
  BEGIN
    -- ── Paso 1: Borrar datos anteriores ──────────────────────
    DELETE FROM PLN_KGR_TITULO;

    -- ── Paso 2: Insertar filas por (titulo, proceso, cod_maq) ─
    -- Usa mediana (PERCENTILE_CONT 0.5) para robustez ante outliers.
    -- Filtra duraciones aberrantes: 0.5 <= horas <= 500.
    INSERT INTO PLN_KGR_TITULO (titulo, proceso, cod_maq,
                                 kgr_hr, kgr_hr_avg, n_muestras,
                                 meses_hist, fch_calculo)
    SELECT titulo, proceso, cod_maq,
           ROUND(
             PERCENTILE_CONT(0.5) WITHIN GROUP (
               ORDER BY peso_neto / NULLIF((fecha_fin - fecha_ini) * 24, 0)
             ), 4
           )                 AS kgr_hr,
           ROUND(
             AVG(peso_neto / NULLIF((fecha_fin - fecha_ini) * 24, 0))
           , 4)               AS kgr_hr_avg,
           COUNT(*)           AS n_muestras,
           p_meses            AS meses_hist,
           SYSDATE            AS fch_calculo
    FROM   h_rproduc
    WHERE  estado    = '3'
      AND  peso_neto > 0
      AND  fecha_fin > fecha_ini
      AND  titulo    IS NOT NULL
      AND  proceso   IS NOT NULL
      AND  cod_maq   IS NOT NULL
      AND  fecha_ini >= v_fch_desde
      AND  (fecha_fin - fecha_ini) * 24 BETWEEN 0.5 AND 500
    GROUP  BY titulo, proceso, cod_maq
    HAVING COUNT(*) >= p_min_muestras;

    v_cnt_maq := SQL%ROWCOUNT;

    -- ── Paso 3: Insertar fila de fallback por (titulo, proceso, '*') ──
    -- Mediana de TODAS las máquinas combinadas para ese título/proceso.
    -- Solo se inserta si no existe ya (por si alguna máquina tiene cod_maq='*' en H_RPRODUC).
    INSERT INTO PLN_KGR_TITULO (titulo, proceso, cod_maq,
                                 kgr_hr, kgr_hr_avg, n_muestras,
                                 meses_hist, fch_calculo)
    SELECT titulo, proceso, '*' AS cod_maq,
           ROUND(
             PERCENTILE_CONT(0.5) WITHIN GROUP (
               ORDER BY peso_neto / NULLIF((fecha_fin - fecha_ini) * 24, 0)
             ), 4
           )                 AS kgr_hr,
           ROUND(
             AVG(peso_neto / NULLIF((fecha_fin - fecha_ini) * 24, 0))
           , 4)               AS kgr_hr_avg,
           COUNT(*)           AS n_muestras,
           p_meses            AS meses_hist,
           SYSDATE            AS fch_calculo
    FROM   h_rproduc
    WHERE  estado    = '3'
      AND  peso_neto > 0
      AND  fecha_fin > fecha_ini
      AND  titulo    IS NOT NULL
      AND  proceso   IS NOT NULL
      AND  fecha_ini >= v_fch_desde
      AND  (fecha_fin - fecha_ini) * 24 BETWEEN 0.5 AND 500
      AND  NOT EXISTS (
             SELECT 1 FROM PLN_KGR_TITULO k
             WHERE  k.titulo  = h_rproduc.titulo
               AND  k.proceso = h_rproduc.proceso
               AND  k.cod_maq = '*'
           )
    GROUP  BY titulo, proceso
    HAVING COUNT(*) >= p_min_muestras;

    v_cnt_fall := SQL%ROWCOUNT;

    COMMIT;
    DBMS_OUTPUT.PUT_LINE('SP_PLN_KGR_REFRESH: '||v_cnt_maq||' filas por maquina, '
                         ||v_cnt_fall||' fallbacks (*). Ventana: '||p_meses||' meses.');
  END SP_PLN_KGR_REFRESH;


  -- ============================================================
  -- SP_PLN_CIERRE_ITEM — Cierre manual de un ítem de seguimiento
  -- ────────────────────────────────────────────────────────────
  -- Uso: cuando un ítem debe cerrarse sin cumplir el flujo normal
  --   (cancelaciones, mermas totales, decisiones gerenciales).
  -- Solo cierra ítems en ESTADO='A'. Si ya es 'C' o 'X', no hace nada.
  -- Inserta evento TIPO_EVENTO='CI' en PLN_LOG_EVENTOS.
  -- Invocar desde C# (PlaneamientoController.CerrarItem):
  --   await conn.ExecuteAsync(
  --     "BEGIN PKG_PLN.SP_PLN_CIERRE_ITEM(:idSeguim, :motivo, :usuario); END;",
  --     new { idSeguim, motivo = "Cancelación cliente", usuario = User.Identity!.Name });
  -- ============================================================
  PROCEDURE SP_PLN_CIERRE_ITEM (
    p_id_seguim IN NUMBER,
    p_motivo    IN VARCHAR2 DEFAULT 'CIERRE_MANUAL',
    p_usuario   IN VARCHAR2 DEFAULT NULL
  ) AS
    v_usr VARCHAR2(15) := NVL(p_usuario, USER);
  BEGIN
    UPDATE PLN_SEGUIMIENTO SET
      ESTADO         = 'C',
      KG_PENDIENTES  = 0,   -- cancelado: no habrá más despachos
      A_MDUSER       = v_usr,
      A_MDFECHA      = SYSDATE
    WHERE id_seguim = p_id_seguim AND estado = 'A';

    -- Resolver todas las alertas activas del ítem cerrado
    -- (evita que aparezcan como huérfanas en V_PLN_ALERTAS_ACTIVAS)
    UPDATE PLN_ALERTA SET
      ESTADO           = 'R',
      FCH_RESOLUCION   = SYSDATE,
      USUARIO_RESUELVE = v_usr,
      OBSERV_RESOL     = 'Cierre manual del ítem de seguimiento'
    WHERE id_seguim = p_id_seguim AND estado = 'A';

    INSERT INTO PLN_LOG_EVENTOS (
      ID_EVENTO, ID_SEGUIM, COD_PASO, DESC_PASO, FCH_EVENTO, USUARIO, TIPO_EVENTO,
      SERIE, NUM_PED, NRO, NUM_DET
    )
    -- COD_PASO = paso en que se encontraba el ítem al cerrar (≠ '14' Despachado).
    -- TIPO_EVENTO='CI' distingue cierre manual de un despacho real (TIPO_EVENTO='PA'/'14').
    SELECT PLN_SEQ_EVENTO.NEXTVAL, id_seguim, cod_paso_act, p_motivo, SYSDATE, v_usr, 'CI',
           serie, num_ped, nro, num_det
    FROM PLN_SEGUIMIENTO WHERE id_seguim = p_id_seguim;

    COMMIT;
  EXCEPTION
    WHEN OTHERS THEN ROLLBACK; RAISE;
  END SP_PLN_CIERRE_ITEM;


  -- ============================================================
  -- SP_PLN_REPROGRAMAR — Reprogramación manual de fecha de despacho
  -- ────────────────────────────────────────────────────────────
  -- Cambia FCH_EST_DESPACHO de un ítem y recalcula IND_RETRASO.
  -- NO recalcula las demás fechas estimadas intermedias.
  -- Guarda el snapshot en PLN_FECHAS_ESTIMADAS (MOTIVO='REP').
  -- Inserta evento TIPO_EVENTO='RE' con las fechas anterior y nueva.
  --
  -- PARÁMETROS:
  --   p_nueva_fch_desp : nueva fecha estimada de despacho
  --   p_motivo         : motivo de la reprogramación (ej. 'CAMBIO CLIENTE')
  --   p_usuario        : usuario que autoriza (desde User.Identity.Name en .NET)
  --
  -- Invocar desde C# (PlaneamientoController.Reprogramar):
  --   await conn.ExecuteAsync(
  --     "BEGIN PKG_PLN.SP_PLN_REPROGRAMAR(:serie,:ped,:nro,:det,:fch,:motivo,:usuario); END;",
  --     new { serie, ped, nro, det,
  --           fch = nuevaFecha, motivo, usuario = User.Identity!.Name });
  -- ============================================================
  PROCEDURE SP_PLN_REPROGRAMAR (
    p_serie          IN NUMBER,
    p_num_ped        IN NUMBER,
    p_nro            IN NUMBER,
    p_num_det        IN NUMBER,
    p_nueva_fch_desp IN DATE,
    p_motivo         IN VARCHAR2 DEFAULT 'REPROG_MANUAL',
    p_usuario        IN VARCHAR2 DEFAULT NULL
  ) AS
    v_usr       VARCHAR2(15) := NVL(p_usuario, USER);
    v_fch_ant   DATE;
    v_id_seg    NUMBER;
    v_id_evt    NUMBER;
  BEGIN
    SELECT id_seguim, fch_est_despacho
    INTO v_id_seg, v_fch_ant
    FROM PLN_SEGUIMIENTO
    WHERE serie=p_serie AND num_ped=p_num_ped AND nro=p_nro AND num_det=p_num_det;

    UPDATE PLN_SEGUIMIENTO SET
      FCH_EST_DESPACHO = p_nueva_fch_desp,
      DIAS_RETRASO     = NVL(GREATEST(TRUNC(SYSDATE) - TRUNC(FCH_ENTREGA_COMP), 0), 0),
      -- FIX: IND_RETRASO unificado con SP_PLN_AVANZA_PASO: usa SYSDATE (retraso actual),
      -- no p_nueva_fch_desp (retraso proyectado). Si ya hay retraso hoy, IND_RETRASO='S'
      -- aunque la nueva fecha estimada sea optimista.
      IND_RETRASO      = CASE WHEN SYSDATE > FCH_ENTREGA_COMP THEN 'S' ELSE 'N' END,
      A_MDUSER         = v_usr,
      A_MDFECHA        = SYSDATE
    WHERE id_seguim = v_id_seg;

    -- Guardar en historial de fechas
    INSERT INTO PLN_FECHAS_ESTIMADAS (
      ID_FECH, ID_SEGUIM, FCH_CALCULO, MOTIVO_RECALCULO,
      FCH_EST_DESPACHO, DIFER_DIAS, USUARIO
    ) VALUES (
      PLN_SEQ_FECHAS.NEXTVAL, v_id_seg, SYSDATE, 'REP',
      p_nueva_fch_desp,
      TRUNC(p_nueva_fch_desp) - TRUNC(NVL(v_fch_ant, p_nueva_fch_desp)),
      v_usr
    );

    -- Evento
    SELECT PLN_SEQ_EVENTO.NEXTVAL INTO v_id_evt FROM DUAL;
    INSERT INTO PLN_LOG_EVENTOS (
      ID_EVENTO, ID_SEGUIM, SERIE, NUM_PED, NRO, NUM_DET,
      COD_PASO, DESC_PASO, FCH_EVENTO, USUARIO,
      FCH_ESTIMADA_ANT, FCH_ESTIMADA_NUE, OBSERVACION, TIPO_EVENTO
    )
    SELECT v_id_evt, v_id_seg, serie, num_ped, nro, num_det,
           cod_paso_act, p_motivo, SYSDATE, v_usr,
           v_fch_ant, p_nueva_fch_desp, p_motivo, 'RE'
    FROM PLN_SEGUIMIENTO WHERE id_seguim = v_id_seg;

    -- Auto-resolución: si la nueva fecha elimina el retraso → resolver RET1/RET2
    UPDATE PLN_ALERTA SET
      ESTADO='R', FCH_RESOLUCION=SYSDATE,
      USUARIO_RESUELVE=v_usr, OBSERV_RESOL='Auto: reprogramación eliminó el retraso'
    WHERE id_seguim=v_id_seg AND estado='A'
      AND tip_alerta IN ('RET1','RET2')
      AND p_nueva_fch_desp > TRUNC(SYSDATE);

    COMMIT;
  EXCEPTION
    WHEN OTHERS THEN ROLLBACK; RAISE;
  END SP_PLN_REPROGRAMAR;

  -- ============================================================
  -- SP_PLN_SEG_PROG_TINTORERIA
  -- Seguimiento Programación Tintorería  (ex QUERY_PRODUCCION + hoja DT Excel).
  -- v3.1 (11/06/2026): +17 columnas DT (MES/ANO/SEM, DIAS_*, ESTADO_FLUJO, ESTADO_DESPACHO, etc.)
  -- Devuelve un SYS_REFCURSOR con el estado de producción de
  -- los ítems de pedido según la opción y filtros recibidos.
  -- ============================================================
  PROCEDURE SP_PLN_SEG_PROG_TINTORERIA (
    p_opc      IN  VARCHAR2,
    p_fechai   IN  DATE       DEFAULT NULL,
    p_fechaf   IN  DATE       DEFAULT NULL,
    p_numped   IN  NUMBER     DEFAULT NULL,
    p_cliente  IN  VARCHAR2   DEFAULT '%',
    p_asesor   IN  VARCHAR2   DEFAULT '%',
    p_titulo   IN  VARCHAR2   DEFAULT '%',
    p_fibra    IN  VARCHAR2   DEFAULT '%',
    p_proceso  IN  VARCHAR2   DEFAULT '%',
    p_cursor   OUT SYS_REFCURSOR
  ) AS
  BEGIN
    OPEN p_cursor FOR
      SELECT
             -- ═══════════════════════════════════════════════════════════════
             -- Col 0: Color hexadecimal del ítem (desde ITEMPED_DET.COLORHEXA)
             -- ═══════════════════════════════════════════════════════════════
             ID.COLORHEXA                                                         AS COLORHEXA,
             -- ═══════════════════════════════════════════════════════════════
             -- Cols 1-48 en orden IDÉNTICO a hoja DT del Excel
             -- SEGUIMIENTO_PARTIDAS_TINTORERIA_KAREN.xlsm
             -- ═══════════════════════════════════════════════════════════════
             -- 1-4: Dimensiones de tiempo (derivadas de FCH_ENTREGA)
             CASE WHEN E.FCH_ENTREGA IS NULL THEN 'SF'
                  ELSE TO_CHAR(EXTRACT(MONTH FROM E.FCH_ENTREGA))
             END                                                                   AS MES,
             CASE WHEN E.FCH_ENTREGA IS NULL THEN 'SF'
                  ELSE DECODE(EXTRACT(MONTH FROM E.FCH_ENTREGA),
                         1,'Ene', 2,'Feb', 3,'Mar', 4,'Abr', 5,'May', 6,'Jun',
                         7,'Jul', 8,'Ago', 9,'Sep', 10,'Oct', 11,'Nov', 12,'Dic')
             END                                                                   AS MES_TEX,
             CASE WHEN E.FCH_ENTREGA IS NULL THEN 'SF'
                  ELSE TO_CHAR(EXTRACT(YEAR FROM E.FCH_ENTREGA))
             END                                                                   AS ANO,
             -- Semana ISO (lunes=inicio de semana); 'SF' si no hay fecha de entrega
             CASE WHEN E.FCH_ENTREGA IS NULL THEN 'SF'
                  ELSE TO_CHAR(E.FCH_ENTREGA, 'IW')
             END                                                                   AS SEM,
             -- 5-9: Identificación del ítem
             E.NUM_PED || '-' || E.NRO || '-' || E.NUM_DET || '-' || E.REPROCESO AS PARTIDA,
             C.NOMBRE                                                              AS CLIENTE,
             DECODE(A.DESCRIPCION, 'VARIOS', I.DETALLE, A.DESCRIPCION)
               || ' ' || I.COLOR_DET                                              AS MATERIAL,
             E.ESTADO_PROG                                                         AS EST,
             -- 9: Ne — Título/contaje del hilo (ej: "08/4 T C")
             N.DESCRIPCION                                                         AS NE,
             -- 10: MAT — Tipo de fibra (ej: LANA, ACRÍLICO)
             E.TIPO_FIBRA                                                          AS MAT,
             -- 11: LOTE — Número de lote de la partida física
             Q.LOTE                                                                AS LOTE,
             -- 12: Fecha del pedido
             J.FECHA                                                               AS FCH_PEDIDO,
             -- 13-15: 1er Rodete — fecha estimada / comprometida / días diferencia
             -- DIAS_ROD positivo = real llegó después del estimado (= atraso); negativo = adelanto
             E.FCH_ESTIMA_CONO_UNO                                                 AS ESTIMA_ROD,
             TRUNC(E.FCH_ENTREGA_CONO_UNO)                                        AS ENTREG_ROD,
             CASE WHEN E.FCH_ENTREGA_CONO_UNO IS NULL
                    OR E.FCH_ESTIMA_CONO_UNO  IS NULL THEN 0
                  ELSE TRUNC(E.FCH_ENTREGA_CONO_UNO)
                       - TRUNC(E.FCH_ESTIMA_CONO_UNO)
             END                                                                   AS DIAS_ROD,
             -- semáforo RODETE: copia de DIAS_ROD (control visual tabla dinámica / web)
             CASE WHEN E.FCH_ENTREGA_CONO_UNO IS NULL
                    OR E.FCH_ESTIMA_CONO_UNO  IS NULL THEN 0
                  ELSE TRUNC(E.FCH_ENTREGA_CONO_UNO)
                       - TRUNC(E.FCH_ESTIMA_CONO_UNO)
             END                                                                   AS X_ROD,
             -- 13-15: Material Hilandería — estimada entrada TT / real / demora en días
             -- DIAS_MH = MAX(0, real-est); si aún no llegó: MAX(0, HOY-est)
             E.FCH_ENT_TIN                                                         AS ESTIMA_MAT,
             RES.FECHA                                                             AS ENTREG_MAT,
             CASE WHEN E.FCH_ENT_TIN IS NULL THEN 0
                  WHEN RES.FECHA IS NULL
                    THEN GREATEST(0, TRUNC(SYSDATE) - TRUNC(E.FCH_ENT_TIN))
                  ELSE GREATEST(0, TRUNC(RES.FECHA) - TRUNC(E.FCH_ENT_TIN))
             END                                                                   AS DIAS_MH,
             -- 16: Fecha de la partida física (DATE — web formatea; misma fuente que col 26)
             Q.FECHA                                                               AS FCHA_GUIA,
             -- 17-20: Receta TT — estimada validación / entrega real / demora / copia
             -- DIAS_REC = MAX(0, ENTREG_RECETA-ESTIMA_RECETA); si pendiente: MAX(0, HOY-est)
             E.FCH_PROGVAL                                                         AS ESTIMA_RECETA,
             L.F_ENTREGA                                                           AS ENTREG_RECETA,
             CASE WHEN E.FCH_PROGVAL IS NULL THEN 0
                  WHEN L.F_ENTREGA IS NULL
                    THEN GREATEST(0, TRUNC(SYSDATE) - TRUNC(E.FCH_PROGVAL))
                  ELSE GREATEST(0, TRUNC(L.F_ENTREGA) - TRUNC(E.FCH_PROGVAL))
             END                                                                   AS DIAS_REC,
             -- col 20 "X" en Excel = copia exacta de DIAS_REC (control visual en tabla dinámica)
             CASE WHEN E.FCH_PROGVAL IS NULL THEN 0
                  WHEN L.F_ENTREGA IS NULL
                    THEN GREATEST(0, TRUNC(SYSDATE) - TRUNC(E.FCH_PROGVAL))
                  ELSE GREATEST(0, TRUNC(L.F_ENTREGA) - TRUNC(E.FCH_PROGVAL))
             END                                                                   AS X,
             -- 21-25: Programa Tintorería
             E.FHC_PROG                                                            AS FCH_PROGRAMA,
             E.DESMAQUINA                                                          AS MAQ_TEN,
             E.FCH_ESTIMA_TENIDO                                                   AS ESTIMA_TENIDO,
             B.FECHA                                                               AS ENTREG_TENIDO,
             -- DIAS_TENIDO = MAX(0, real-est); si pendiente: MAX(0, HOY-est)
             CASE WHEN E.FCH_ESTIMA_TENIDO IS NULL THEN 0
                  WHEN B.FECHA IS NULL
                    THEN GREATEST(0, TRUNC(SYSDATE) - TRUNC(E.FCH_ESTIMA_TENIDO))
                  ELSE GREATEST(0, TRUNC(B.FECHA) - TRUNC(E.FCH_ESTIMA_TENIDO))
             END                                                                   AS DIAS_TENIDO,
             -- semáforo TENIDO: copia de DIAS_TENIDO (control visual tabla dinámica / web)
             CASE WHEN E.FCH_ESTIMA_TENIDO IS NULL THEN 0
                  WHEN B.FECHA IS NULL
                    THEN GREATEST(0, TRUNC(SYSDATE) - TRUNC(E.FCH_ESTIMA_TENIDO))
                  ELSE GREATEST(0, TRUNC(B.FECHA) - TRUNC(E.FCH_ESTIMA_TENIDO))
             END                                                                   AS X_TENIDO,
             -- 26-31: Fechas reales de producción
             -- col 26 "FCH PARTIDA" en Excel = misma fuente que col 16 FCHA_GUIA (Q.FECHA)
             Q.FECHA                                                               AS FCH_PARTIDA,
             K.FECHA                                                               AS FCH_RECETA,
             H.FECHA                                                               AS FCH_SEC_RODETE,
             SM.FECHA                                                              AS FCH_SEC_MADEJA,
             D.FECHA                                                               AS FCH_APROB_CAL,
             -- TIME_APROV = MAX(0, FCH_APROB_CAL-FCH_SEC_RODETE); 0 si alguna es NULL
             GREATEST(0, NVL(TRUNC(D.FECHA) - TRUNC(H.FECHA), 0))                AS TIME_APROV,
             -- 31-34: Acabado, enconado y evaluación CC
             -- TIPO_ACABADO: presentación final del producto (independiente de la máquina de teñido)
             --   ACAB_MAD='S' → REDINA (presentado en forma de madeja)
             --   otro/NULL    → CONERA  (presentado en cono)
             DECODE(E.ACAB_MAD, 'S', 'REDINA', 'CONERA')                         AS TIPO_ACABADO,
             -- ACABADO: secadora destino según máquina de teñido programada
             --   R01-R19 (THIES)                        → RODETE  → S01 Sec. Thies
             --   M01-M08 (LORIS/BRAZZOS/MEZZERA/CUBOTEX/HANK MASTER) → MADEJA → S02/S04 Sec. Madejas/Minnetti
             --   Sin máquina asignada o tipo diferente   → NULL
             CASE
               WHEN E.MAQUINA LIKE 'R%' THEN 'RODETE'
               WHEN E.MAQUINA LIKE 'M0%' THEN 'MADEJA'
               ELSE NULL
             END                                                                   AS ACABADO,
             Z.FECHA                                                               AS FCH_ENCONADO,
             R.FECHA                                                               AS FCH_REVISADO,
             -- EV_ENCON: NULL si sin secado; 'EN CONSULTA' si sin CC; resultado CC en otro caso
             CASE WHEN H.FECHA IS NULL             THEN NULL
                  WHEN CAL.RESULTADO IS NULL        THEN 'EN CONSULTA'
                  WHEN CAL.RESULTADO = 'APROBADO'   THEN 'APROBADO'
                  WHEN CAL.RESULTADO = 'CONCESIONADO' THEN 'CONCESIONADO'
                  ELSE 'RECHAZADO'
             END                                                                   AS EV_ENCON,
             -- 35-39: Entrega y días de espera
             E.FCH_ENTREGA                                                         AS FCH_ENTREGA,
             S.FECHA_ING                                                           AS ING_ALMPT,
             -- col 37 DIAS_EN_ESPERA: MAX(0, llegó_o_hoy-comprometida); NULL si sin FCH_ENTREGA
             GREATEST(0, NVL(S.FECHA_ING, TRUNC(SYSDATE))
                         - TRUNC(E.FCH_ENTREGA))                                  AS DIAS_EN_ESPERA,
             -- col 38 "D.E" Excel: con signo — negativo=llegó antes, positivo=atrasado
             TRUNC(NVL(S.FECHA_ING, TRUNC(SYSDATE)))
               - TRUNC(E.FCH_ENTREGA)                                             AS DE,
             -- col 39 "DE" Excel = copia exacta de col 37 DIAS_EN_ESPERA (MAX 0)
             GREATEST(0, NVL(S.FECHA_ING, TRUNC(SYSDATE))
                         - TRUNC(E.FCH_ENTREGA))                                  AS DE_COPIA,
             -- 40-43: Kilogramos y tolerancia de despacho
             -- KG_PEDIDO: kg pedidos por el cliente (ITEMPED_DET.CANTIDAD)
             E.CANTIDAD                                                            AS KG_PEDIDO,
             -- KG_PROG: kg destinados al rodete (PARTIDA.PESO_NETO = peso real del lote físico)
             Q.NETO                                                                AS KG_PROG,
             U.CANTIDAD                                                            AS KG_DESPA,
             -- GAP: NULL si sin despacho; KG_DESPA - KG_PROG (vs peso real del lote en rodete)
             CASE WHEN U.CANTIDAD IS NULL THEN NULL
                  ELSE U.CANTIDAD - NVL(Q.NETO, E.CANTIDAD)
             END                                                                   AS GAP,
             -- PCT_TOLERAN: ±% vs KG_PROG (peso rodete); NULL=sin despachar; rojo si |%|>5
             CASE WHEN U.CANTIDAD IS NULL OR NVL(Q.NETO, E.CANTIDAD) = 0 THEN NULL
                  ELSE ROUND((U.CANTIDAD / NVL(Q.NETO, E.CANTIDAD) - 1) * 100, 2)
             END                                                                   AS PCT_TOLERAN,
             -- 47: ESTADO_FLUJO (col "PROCESO" en Excel DT) — etapa más avanzada alcanzada
             CASE
               WHEN U.CANTIDAD    IS NOT NULL THEN 'DESPACHADO'
               WHEN S.FECHA_ING   IS NOT NULL THEN 'EN ALMACÉN'
               WHEN R.FECHA       IS NOT NULL THEN 'PENDIENTE DE PESAR'
               WHEN Z.FECHA       IS NOT NULL THEN 'PENDIENTE DE REVISAR'
               WHEN H.FECHA       IS NOT NULL THEN 'PENDIENTE DE ENCONAR'
               WHEN B.FECHA       IS NOT NULL THEN 'PENDIENTE DE SECAR'
               WHEN L.F_ENTREGA   IS NOT NULL THEN 'PENDIENTE DE TEÑIR'
               ELSE                                'SIN RECETA'
             END                                                                   AS ESTADO_FLUJO,
             -- 46: ESTADO_DESPACHO (col "DESPACHOS" en Excel DT) — semáforo de puntualidad
             CASE
               WHEN E.FCH_ENTREGA IS NULL AND S.FECHA_ING IS NULL
                                                              THEN 'PENDIENTE SF'
               WHEN E.FCH_ENTREGA IS NULL AND S.FECHA_ING IS NOT NULL
                                                              THEN 'DESP SF'
               WHEN S.FECHA_ING IS NULL
                AND TRUNC(E.FCH_ENTREGA) = TRUNC(SYSDATE)    THEN 'VENCE HOY'
               WHEN TRUNC(NVL(S.FECHA_ING, TRUNC(SYSDATE)))
                      - TRUNC(E.FCH_ENTREGA) > 0             THEN 'VENCIDO'
               ELSE                                               'A TIEMPO'
             END                                                                   AS ESTADO_DESPACHO,
             -- 47-48: columnas de apoyo
             -- AREA_RESPONSABLE = ITEMPED_DET.OBSERVACIONES (campo libre de área responsable)
             ID.OBSERVACIONES                                                      AS AREA_RESPONSABLE,
             NULL                                                                  AS BP,
             -- ═══════════════════════════════════════════════════════════════
             -- Columnas adicionales de QUERY_PRODUCCION (no en DT, útiles en web)
             -- ═══════════════════════════════════════════════════════════════
             Q.NETO                                                                AS PESO_NETO,
             Q.RMC,
             Q.NRO_RMC,
             N.DESCRIPCION                                                         AS TITULO,
             'Ne ' || N.DESCRIPCION                                               AS TITULO_TEXTO,
             I.TIPO_REF || '-' || I.NUM_REF || '-' || I.ITEM_REF
               || DECODE(I.TIPO_REF, 'M1', '-' || I.OPC_REF, '')                 AS REFERENCIA,
             B.PROCESO                                                             AS PROCESO_TT,
             -- OPT-2: EXISTS reemplaza PARTIDA_CON_MATIZ(E.GUIA) — evita N subquerys row-by-row
             CASE WHEN EXISTS (
               SELECT 1
               FROM   PARTIDA_MAS PM2
               JOIN   ING_RECETAS_G RG2 ON RG2.NUMERO = PM2.NUMERO
                                       AND RG2.TP_TRANSAC = 'IR'
                                       AND RG2.SERIE      = 1
                                       AND RG2.PROCESO    IN ('MA','MAPL','ACOL')
                                       AND NVL(RG2.ESTADO,'0') <> '9'
               WHERE  PM2.PARTIDA = E.GUIA
                 AND  NVL(PM2.ESTADO,'1') <> '9'
             ) THEN 'S' ELSE 'N' END                                              AS PART_MATIZ,
             CAL.EST_EVALUACION,
             CAL.DEFECTO,
             CAL.RESULTADO,
             V.ABREVIADO                                                           AS LABO_VAL,
             E.ACAB_MAD                                                            AS ACA_MAD,
             -- DIAS_RETRASO original: FCH_ENTREGA − ING_ALMPT (negativo = llegó tarde)
             -- Distinto de DE (col 38): DE usa TRUNC; DIAS_RETRASO es NUMBER exacto
             TO_NUMBER(E.FCH_ENTREGA - NVL(S.FECHA_ING, TRUNC(SYSDATE)))         AS DIAS_RETRASO,
             -- Clave de edición para guardar OBSERVACIONES y COLORHEXA desde la web:
             -- NROPROG_DET: ITEMPED_DET.NROPROG (surrogate key cuando el ítem tiene programa).
             -- Cuando es NULL (sin programa aún), el front-end debe usar el composite key
             -- NUM_PED + NRO + NUM_DET + REPROCESO + FCH_PROG para llamar a SP_PLN_UPD_ITEM_OBS_COLOR.
             -- Los campos necesarios ya están en la fila: NUM_PED (en PARTIDA), y si se necesitan
             -- separados, se exponen junto con NROPROG_DET para que el boton Guardar los use.
             ID.NROPROG                                                            AS NROPROG_DET,
             F.NUM_PED                                                             AS NUM_PED_KEY,
             F.NRO                                                                 AS NRO_KEY,
             F.NUM_DET                                                             AS NUM_DET_KEY,
             E.REPROCESO                                                           AS REPROCESO_KEY,
             F.FCH_PROG                                                            AS FCH_PROG_KEY
        FROM (
               -- Dedup: por cada (NUM_PED,NRO,NUM_DET,REPROCESO) conserva el FHC_PROG más reciente.
               -- BUG-FIX-1: REPROCESO incluido en GROUP BY — sin él, MAX cruzaba datos entre
               --            reprocesos distintos del mismo ítem.
               -- BUG-FIX-2: COALESCE(MAX(FHC_PROG),...) en vez de MAX(NVL(FHC_PROG,...)) para que
               --            una fila sin FHC_PROG (NULL→centinela '31/12/2050') no "gane" a filas
               --            con programa real (cualquier fecha real < '31/12/2050').
               SELECT NUM_PED, NRO, NUM_DET, REPROCESO,
                      COALESCE(MAX(FHC_PROG), TO_DATE('31/12/2050','DD/MM/YYYY')) AS FCH_PROG
               FROM   ITEMPED_DET
               WHERE  (p_opc <> 'POR PEDIDO' OR NUM_PED = p_numped)  -- OPT: filtro temprano para modo POR PEDIDO
               GROUP  BY NUM_PED, NRO, NUM_DET, REPROCESO
             ) F,
             V_ITEMPEDET E,
             -- CC tintorería: última consulta aprobada por guía
             (
               SELECT MAX(TRUNC(FCH_CONSULTA)) AS FECHA, GUIA
               FROM   CTCALIDAD_D
               WHERE  (NVL(CONSULTA,'00') = '01' OR RESULTADO IN ('01','29'))
               GROUP  BY GUIA
             ) D,
             CTCALIDAD_D X,
             -- Partida activa (excluye anuladas/cerradas) -- OPT: P era duplicado de W; T era join muerto (no referenciado en SELECT)
             (
               SELECT P.GUIA, P.PARTIDA
               FROM   V_PARTIDA P
               WHERE  NVL(P.ESTADO,'0') NOT IN ('8','9')
               GROUP  BY P.GUIA, P.PARTIDA
             ) W,
             V_PARTIDA Q,
             ITEMPED I,
             PEDIDO  J,
             CLIENTES C,
             -- Tintorería: último baño completado (ESTADO='3', proceso con cálculo TT)
             -- OPT-3: CTPROCESOS IN literal (9 códigos estables verificados en BD; evita
             --        subquery decorrelacionado ejecutado en cada evaluación de la subquery B)
             (
               SELECT PARTIDA, PROCESO, MAX(TRUNC(FECHA_FIN)) AS FECHA
               FROM   V_RPRODUC
               WHERE  ESTADO = '3'
                 AND  PROCESO IN ('BQ','DSTEAC','DSTEPS','IN','PRTE','PRTEPS','TE','TEAC','TEPS')
               GROUP  BY PARTIDA, PROCESO
             ) B,
             -- Revisado: fecha más reciente
             (
               SELECT G.GUIA, MAX(D2.FECHA) AS FECHA
               FROM   REVISADO_G G
               JOIN   REVISADO_D D2 ON D2.NUMERO = G.NUMERO
               GROUP  BY G.GUIA
             ) R,
             -- Encono: H_PROGRAMACION (fecha fin o fecha)
             (
               SELECT GUIA, MAX(NVL(FECHA_FIN, FECHA)) AS FECHA
               FROM   H_PROGRAMACION
               WHERE  ESTADO <> '9'
               GROUP  BY GUIA
             ) Z,
             -- Almacén PT: primera fecha de ingreso (almacenes 03 y 07)
             (
               SELECT PARTIDA, MIN(FECHA) AS FECHA_ING
               FROM   LOTES
               WHERE  COD_ALM IN ('03','07')
                 AND  ESTADO  <> '9'
                 AND  PARTIDA IS NOT NULL
               GROUP  BY PARTIDA
             ) S,
             -- Secado en máquinas R/X (S01=RMC_R, S03=RMC_X)
             -- OPT-4: elimina JOIN TT_MAQUINA (39 filas); TT_MAQUINA.TIPO_MAQ='S' tiene
             --        solo 4 máquinas fijas. IDX_RSECADO_MAQ_GUIA(COD_MAQ,GUIA) ahora sirve.
             (
               SELECT S2.GUIA, MAX(TRUNC(S2.FECHA_FIN)) AS FECHA
               FROM   TT_RSECADO S2
               WHERE  S2.COD_MAQ IN ('S01','S03')
               GROUP  BY S2.GUIA
             ) H,
             -- Secado en máquinas madeja (S02=RMC_M, S04=RMC_M)
             (
               SELECT S3.GUIA, MAX(TRUNC(S3.FECHA_FIN)) AS FECHA
               FROM   TT_RSECADO S3
               WHERE  S3.COD_MAQ IN ('S02','S04')
               GROUP  BY S3.GUIA
             ) SM,
             -- Receta: fecha más reciente por guía/proceso TT
             -- OPT-3 (mismo conjunto de códigos que B)
             (
               SELECT GUIA, PROCESO, MAX(FEC_RECETA) AS FECHA
               FROM   V_RECETAPARTIDA
               WHERE  PROCESO IN ('BQ','DSTEAC','DSTEPS','IN','PRTE','PRTEPS','TE','TEAC','TEPS')
               GROUP  BY GUIA, PROCESO
             ) K,
             -- Cantidad despachada desde almacenes PT
             (
               SELECT PARTIDA AS GUIA, SUM(STOCK_INIC) AS CANTIDAD
               FROM   LOTES
               WHERE  COD_ALM   IN ('03','07')
                 AND  ESTADO     <> '9'
                 AND  PARTIDA    IS NOT NULL
                 AND  S_TRANSAC  IS NOT NULL
                 AND  S_SERIE    IS NOT NULL
                 AND  S_NUMERO   IS NOT NULL
                 AND  FEC_SALIDA IS NOT NULL
               GROUP  BY PARTIDA
             ) U,
             -- Último ingreso a almacén PI (PARTIDA_RESERVA)
             (
               SELECT NROPROG, MAX(FECHA) AS FECHA
               FROM   PARTIDA_RESERVA
               GROUP  BY NROPROG
             ) RES,
             ARTICUL           A,
             H_TITULOS         N,
             V_STATUS_CCAL_TINTO CAL,
             L_VALIDA_RECETA   L,
             H_TPROD           V,
             -- COLORHEXA/OBSERVACIONES: subquery agrupada por (NUM_PED,NRO,NUM_DET,REPROCESO).
             -- BUG-FIX-3: el join anterior era INNER sobre FHC_PROG → cuando las notas fueron
             --            guardadas en un programa anterior (FHC_PROG distinto al del dedup),
             --            el INNER JOIN no encontraba match y la fila DESAPARECÍA completamente.
             -- Solución: agrupar a nivel ítem+reproceso y tomar el valor guardado más reciente
             --           (DENSE_RANK LAST por A_MDFECHA). Outer join (en WHERE con (+)) para
             --           que ítems sin anotaciones sigan apareciendo con NULL en esas columnas.
             (
               SELECT NUM_PED, NRO, NUM_DET, REPROCESO,
                      MAX(COLORHEXA)
                        KEEP (DENSE_RANK LAST ORDER BY NVL(A_MDFECHA, DATE '1900-01-01'))  AS COLORHEXA,
                      MAX(OBSERVACIONES)
                        KEEP (DENSE_RANK LAST ORDER BY NVL(A_MDFECHA, DATE '1900-01-01'))  AS OBSERVACIONES,
                      MAX(NROPROG)                                                          AS NROPROG
               FROM   ITEMPED_DET
               WHERE  ESTADO <> '9'
               GROUP  BY NUM_PED, NRO, NUM_DET, REPROCESO
             )                 ID
             -- OPT-1 (sesión anterior): TT_PARAMPROGTIN EE eliminado — ningún campo de EE
             --        aparecía en el SELECT; generaba un producto cartesiano innecesario
       WHERE NVL(E.ESTADO_PART,'0') NOT IN ('8','9')
         -- Dedup FHC_PROG (por ítem + reproceso)
         AND E.NUM_PED   = F.NUM_PED
         AND E.NRO       = F.NRO
         AND E.NUM_DET   = F.NUM_DET
         AND E.REPROCESO = F.REPROCESO  -- BUG-FIX-1: alinea reproceso entre E y F
         AND NVL(E.FHC_PROG, TO_DATE('31/12/2050','DD/MM/YYYY')) = F.FCH_PROG
         -- ── Opción de búsqueda ─────────────────────────────
         AND (
               (p_opc = 'POR FECHA DE ENTREGA'
                AND TRUNC(E.FCH_ENTREGA) BETWEEN p_fechai AND p_fechaf)
            OR (p_opc = 'POR PEDIDO'
                AND E.NUM_PED = p_numped)
            OR (p_opc = 'POR FECHA DE PROGRAMA'
                AND TRUNC(E.FHC_PROG) BETWEEN p_fechai AND p_fechaf)
            OR (p_opc = 'POR FECHA DE TEÑIDO'
                AND E.FHC_PROG >= ADD_MONTHS(p_fechai, -3)
                AND B.FECHA    BETWEEN p_fechai AND p_fechaf)
            OR (p_opc = 'POR FECHA APROB PEDIDO'
                AND TRUNC(E.FCH_PEDIDO_APROB) BETWEEN p_fechai AND p_fechaf)
             )
         -- ── Filtros adicionales ────────────────────────────
         AND (p_cliente = '%'
              OR (p_cliente = 'X'
                  AND E.COD_CLIENTE NOT IN ('77777777','88888888'))
              OR (p_cliente NOT IN ('%','X')
                  AND E.COD_CLIENTE = p_cliente))
         AND (p_titulo  = '%' OR E.TITULO      = p_titulo)
         AND (p_fibra   = '%' OR E.TIPO_FIBRA  = p_fibra)
         AND (p_proceso = '%' OR E.PROCESO     = p_proceso)
         -- ── CC tintorería ──────────────────────────────────
         AND D.GUIA(+) = E.GUIA
         AND X.FCH_CONSULTA(+) = D.FECHA
         AND X.GUIA(+)         = D.GUIA
         AND X.RESULTADO(+)   IN ('01','29')
         AND NVL(X.ESTADO(+),'1') <> '9'
         -- ── Partidas ──────────────────────────────────────
         AND W.GUIA(+) = E.GUIA
         AND Q.GUIA(+) = W.GUIA
         -- ── Pedido / ítem / cliente ────────────────────────
         AND I.ESTADO  <> '9'
         AND I.NUM_PED  = E.NUM_PED
         AND I.NRO      = E.NRO
         AND (p_asesor = '%' OR J.COD_VENDE = p_asesor)
         AND J.NUM_PED  = I.NUM_PED
         AND C.COD_CLIENTE = J.COD_CLIENTE
         -- ── Tintorería ────────────────────────────────────
         AND B.PARTIDA(+) = Q.GUIA
         -- ── Revisado / encono / almacén PT ────────────────
         AND R.GUIA(+)  = W.GUIA
         AND Z.GUIA(+)  = W.GUIA
         AND S.PARTIDA(+) = W.GUIA
         -- ── Secado ────────────────────────────────────────
         AND H.GUIA(+)  = W.GUIA
         AND SM.GUIA(+) = W.GUIA
         -- ── Receta / despacho ─────────────────────────────
         AND K.GUIA(+)  = Q.GUIA
         AND U.GUIA(+)  = W.GUIA
         -- ── PARTIDA_RESERVA ───────────────────────────────
         AND RES.NROPROG(+) = E.NUMERO
         -- ── Artículo / título ─────────────────────────────
         AND A.COD_ART = E.COD_ART
         AND N.TITULO(+) = I.TITULO
         -- ── CC status (vista) ─────────────────────────────
         -- OPT-5: join numérico en vez de string concat; evita comparar strings de 10+ chars
         --        y permite usar IDX_CTCALIDAD_ITEM(NRO_PEDIDO,SER_PARTIDA,NROPART)
         --        dentro del GROUP BY de V_STATUS_CCAL_TINTO
         AND CAL.NUM_PED(+)  = F.NUM_PED
         AND CAL.ITEM_PED(+) = F.NRO
         AND CAL.NROPART(+)  = F.NUM_DET
         -- ── Validación receta / laboratorista ─────────────
         AND L.NUMERO(+) = E.NRO_VALREC
         AND V.TABLA(+)  = '09'
         AND V.CODIGO(+) = L.C_LABORATORISTA
         -- ── Anotaciones COLORHEXA/OBSERVACIONES (outer join — ítem sin notas sigue visible)
         AND ID.NUM_PED(+)   = E.NUM_PED
         AND ID.NRO(+)       = E.NRO
         AND ID.NUM_DET(+)   = E.NUM_DET
         AND ID.REPROCESO(+) = E.REPROCESO
         -- FHC_PROG excluido del join: la subquery ya agrupa a nivel ítem+reproceso
       ORDER BY E.FCH_ENTREGA,
                E.NUM_PED || '-' || E.NRO || '-' || E.NUM_DET || '-' || E.REPROCESO;
  END SP_PLN_SEG_PROG_TINTORERIA;


  -- ============================================================
  -- SP_PLN_UPD_ITEM_OBS_COLOR
  -- Actualiza ITEMPED_DET.OBSERVACIONES y ITEMPED_DET.COLORHEXA
  -- desde la web (botón Guardar en la tabla de seguimiento TT).
  -- ------------------------------------------------------------
  -- CLAVE DE IDENTIFICACIóN (usar en este orden de prioridad):
  --   1. Si p_nroprog IS NOT NULL  → UPDATE WHERE NROPROG = p_nroprog
  --   2. Si p_nroprog IS NULL      → UPDATE WHERE
  --        NUM_PED=p_num_ped AND NRO=p_nro AND NUM_DET=p_num_det
  --        AND REPROCESO=p_reproceso
  --        AND NVL(FHC_PROG, TO_DATE('31/12/2050','DD/MM/YYYY'))=
  --            NVL(p_fch_prog, TO_DATE('31/12/2050','DD/MM/YYYY'))
  --      (mismo predicado que usa el dedup interno del SP de consulta)
  --
  -- INVOCACIÓN DESDE C# (Dapper):
  --   await conn.ExecuteAsync(
  --     "BEGIN PKG_PLN.SP_PLN_UPD_ITEM_OBS_COLOR(:nroprog,:numPed,:nro,:numDet,:repro,:fchProg,:obs,:hex,:user); END;",
  --     new { nroprog   = row.NroprogDet,          -- null si sin programa
  --           numPed    = row.NumPedKey,
  --           nro       = row.NroKey,
  --           numDet    = row.NumDetKey,
  --           repro     = row.ReprocesoPKey,
  --           fchProg   = row.FchProgKey,           -- null si sin programa
  --           obs       = txtObservaciones.Text,
  --           hex       = inputColorHexa.Value,
  --           user      = User.Identity!.Name });
  -- ============================================================
  PROCEDURE SP_PLN_UPD_ITEM_OBS_COLOR (
    p_nroprog      IN NUMBER   DEFAULT NULL,
    p_num_ped      IN NUMBER   DEFAULT NULL,
    p_nro          IN NUMBER   DEFAULT NULL,
    p_num_det      IN NUMBER   DEFAULT NULL,
    p_reproceso    IN VARCHAR2 DEFAULT NULL,
    p_fch_prog     IN DATE     DEFAULT NULL,
    p_observaciones IN VARCHAR2,
    p_colorhexa    IN VARCHAR2,
    p_usuario      IN VARCHAR2 DEFAULT NULL
  ) AS
    v_rows  PLS_INTEGER := 0;
  BEGIN
    IF p_nroprog IS NOT NULL THEN
      -- Caso 1: NROPROG localiza el ítem → actualizar TODOS los FHC_PROG del mismo
      -- (NUM_PED,NRO,NUM_DET,REPROCESO) para que las notas persistan si el ítem
      -- es reprogramado a una fecha diferente y el dedup apunta a la nueva fila.
      UPDATE ITEMPED_DET D
      SET    D.OBSERVACIONES = p_observaciones,
             D.COLORHEXA     = p_colorhexa,
             D.A_MDUSER      = NVL(p_usuario, USER),
             D.A_MDFECHA     = SYSDATE
      WHERE  (D.NUM_PED, D.NRO, D.NUM_DET, D.REPROCESO) = (
               SELECT NUM_PED, NRO, NUM_DET, REPROCESO
               FROM   ITEMPED_DET
               WHERE  NROPROG = p_nroprog
                 AND  ROWNUM  = 1
             )
        AND  D.ESTADO <> '9';
    ELSE
      -- Caso 2: composite key sin NROPROG — actualizar TODOS los FHC_PROG del mismo
      -- (NUM_PED,NRO,NUM_DET,REPROCESO). FHC_PROG excluido intencionalmente del WHERE:
      -- las notas son del ítem, no de un programa concreto; si el ítem se reprograma
      -- a otro mes el color/obs no se pierden porque están en todas las filas.
      UPDATE ITEMPED_DET
      SET    OBSERVACIONES = p_observaciones,
             COLORHEXA     = p_colorhexa,
             A_MDUSER      = NVL(p_usuario, USER),
             A_MDFECHA     = SYSDATE
      WHERE  NUM_PED   = p_num_ped
        AND  NRO       = p_nro
        AND  NUM_DET   = p_num_det
        AND  REPROCESO = NVL(p_reproceso, '0')
        AND  ESTADO    <> '9';
      -- FHC_PROG excluida del WHERE: actualiza todos los programas del ítem.
    END IF;

    v_rows := SQL%ROWCOUNT;
    IF v_rows = 0 THEN
      RAISE_APPLICATION_ERROR(-20101,
        'SP_PLN_UPD_ITEM_OBS_COLOR: no se encontró el registro a actualizar.');
    END IF;

    COMMIT;
  EXCEPTION
    WHEN OTHERS THEN ROLLBACK; RAISE;
  END SP_PLN_UPD_ITEM_OBS_COLOR;


  -- ============================================================
  -- SP_PLN_FILTRO_CLIENTES
  -- Devuelve clientes con pedidos activos (no internos).
  -- Columnas: COD_CLIENTE, NOMBRE
  -- ============================================================
  PROCEDURE SP_PLN_FILTRO_CLIENTES (p_cursor OUT SYS_REFCURSOR) AS
  BEGIN
    OPEN p_cursor FOR
      SELECT DISTINCT J.COD_CLIENTE,
             C.NOMBRE
      FROM   PEDIDO J
      JOIN   CLIENTES C ON C.COD_CLIENTE = J.COD_CLIENTE
      WHERE  J.SERIE   = 1
        AND  J.ESTADO NOT IN ('0','9')
        AND  J.F_APROBACION IS NOT NULL
        AND  J.COD_CLIENTE NOT IN ('77777777','88888888')
      ORDER BY C.NOMBRE;
  END SP_PLN_FILTRO_CLIENTES;


  -- ============================================================
  -- SP_PLN_FILTRO_ASESORES
  -- Devuelve asesores/vendedores con pedidos activos.
  -- Columnas: COD_VENDE, ABREVIADA, NOMBRE
  -- Fuente: TABLAS_AUXILIARES TIPO=29 (verificado en BD)
  -- ============================================================
  PROCEDURE SP_PLN_FILTRO_ASESORES (p_cursor OUT SYS_REFCURSOR) AS
  BEGIN
    OPEN p_cursor FOR
      SELECT DISTINCT J.COD_VENDE,
             T.ABREVIADA,
             T.DESCRIPCION AS NOMBRE
      FROM   PEDIDO J
      JOIN   TABLAS_AUXILIARES T
               ON T.TIPO   = 29
              AND T.CODIGO = J.COD_VENDE
      WHERE  J.SERIE  = 1
        AND  J.ESTADO NOT IN ('0','9')
        AND  J.F_APROBACION IS NOT NULL
        AND  J.COD_VENDE IS NOT NULL
        AND  T.CODIGO  <> '....'
      ORDER BY T.DESCRIPCION;
  END SP_PLN_FILTRO_ASESORES;


  -- ============================================================
  -- SP_PLN_FILTRO_TITULOS
  -- Devuelve títulos distintos usados en ítems activos.
  -- Columnas: TITULO (código), DESCRIPCION (ej. '04/2')
  -- Fuente: H_TITULOS (TITULO=código, DESCRIPCION=texto)
  -- ============================================================
  PROCEDURE SP_PLN_FILTRO_TITULOS (p_cursor OUT SYS_REFCURSOR) AS
  BEGIN
    OPEN p_cursor FOR
      SELECT DISTINCT D.TITULO,
             T.DESCRIPCION
      FROM   ITEMPED_DET D
      JOIN   PEDIDO J ON J.NUM_PED = D.NUM_PED AND J.SERIE = 1
      JOIN   H_TITULOS T ON T.TITULO = D.TITULO
      WHERE  J.ESTADO NOT IN ('0','9')
        AND  J.F_APROBACION IS NOT NULL
        AND  D.TITULO IS NOT NULL
        AND  D.TITULO <> '000'
      ORDER BY T.DESCRIPCION;
  END SP_PLN_FILTRO_TITULOS;


  -- ============================================================
  -- SP_PLN_FILTRO_FIBRAS
  -- Devuelve fibras distintas usadas en ítems activos.
  -- Columnas: TIPO_FIBRA (código), ABREVIADO, DESCRIPCION
  -- Fuente: H_FIBRA (verificado en BD: FIBRA=código, DESCRIPCION=nombre)
  -- ============================================================
  PROCEDURE SP_PLN_FILTRO_FIBRAS (p_cursor OUT SYS_REFCURSOR) AS
  BEGIN
    OPEN p_cursor FOR
      SELECT DISTINCT D.TIPO_FIBRA,
             F.ABREVIADO,
             F.DESCRIPCION
      FROM   ITEMPED_DET D
      JOIN   PEDIDO J ON J.NUM_PED = D.NUM_PED AND J.SERIE = 1
      JOIN   H_FIBRA F ON F.FIBRA = D.TIPO_FIBRA
      WHERE  J.ESTADO NOT IN ('0','9')
        AND  J.F_APROBACION IS NOT NULL
        AND  D.TIPO_FIBRA IS NOT NULL
        AND  D.TIPO_FIBRA <> '000'
      ORDER BY F.DESCRIPCION;
  END SP_PLN_FILTRO_FIBRAS;


  -- ============================================================
  -- SP_PLN_FILTRO_PROCESOS
  -- Devuelve procesos de producción usados en ítems activos.
  -- Columnas: PROCESO (código), DESCRIPCION
  -- Fuente: H_PROCESOS (verificado en BD)
  -- ============================================================
  PROCEDURE SP_PLN_FILTRO_PROCESOS (p_cursor OUT SYS_REFCURSOR) AS
  BEGIN
    OPEN p_cursor FOR
      SELECT DISTINCT D.PROCESO,
             R.DESCRIPCION
      FROM   ITEMPED_DET D
      JOIN   PEDIDO J ON J.NUM_PED = D.NUM_PED AND J.SERIE = 1
      JOIN   H_PROCESOS R ON R.PROCESO = D.PROCESO
      WHERE  J.ESTADO NOT IN ('0','9')
        AND  J.F_APROBACION IS NOT NULL
        AND  D.PROCESO IS NOT NULL
        AND  D.PROCESO <> '00'
      ORDER BY R.DESCRIPCION;
  END SP_PLN_FILTRO_PROCESOS;


END PKG_PLN;
/


-- ============================================================
