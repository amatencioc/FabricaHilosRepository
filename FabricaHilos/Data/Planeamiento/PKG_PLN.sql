/* ============================================================
   PKG_PLN.sql
   ============================================================
   MODULO     : PLN_ - Planeamiento, Seguimiento y Control de Planta
   SISTEMA    : SIG - Fabricacion de Hilos (Hilanderia y Tintoreria)
   BD         : Oracle 11.2.0.4 - Esquema SIG (multi-empresa: SIG / ARBONA / SOLSA)
   CREADO     : 18/05/2026
   ULTIMA MOD : 28/05/2026
   VERSION    : v2.5 — FIX-PCT: PCT_CIERRE_DESPACHO desde PLN_PARAM (no hardcodeado)
                        FIX-NVL: GREATEST(0,NULL)=NULL corregido en SP_PLN_GENERA_ALERTAS
                v2.6 — PLN_KGR_TITULO: tabla de velocidades kg/hr derivada de H_RPRODUC
                        SP_PLN_KGR_REFRESH: auto-calcula mediana kgr_hr (ventana 24 meses)
                        SP_PLN_CALCULA_FECHAS: usa PLN_KGR_TITULO en vez de CTRUTAS_TITULO
                        JOB_PLN_KGR: refresco mensual automático (día 1 de cada mes 01:00)
   ============================================================

Ventas graba ITEMPED          → TIA_PLN_FROM_ITEMPED          → PLN_SEGUIMIENTO PASO '01'
Planif. actualiza FHC_PROG    → TUA_PLN_FROM_ITEMPED_DET      → PLN_SEGUIMIENTO PASO '02'
Se crea PARTIDA               → TIA_PLN_FROM_PARTIDA          → PLN_SEGUIMIENTO PASO '03'
Lab aprueba L_VALIDA_RECETA   → TUA_PLN_FROM_L_VALIDA_RECETA  → PLN_SEGUIMIENTO PASO '04'
Se registra H_RPRODUC         → TIA_PLN_FROM_H_RPRODUC        → PLN_SEGUIMIENTO PASO '05'
PARTIDA.SITU_PART='R001'      → TUA_PLN_FROM_PARTIDA          → PLN_SEGUIMIENTO PASO '06'
TT_RPRODUC.ESTADO='3' (IR)    → TUA_PLN_FROM_TT_RPRODUC       → PLN_SEGUIMIENTO PASO '07'
Se inserta TT_RSECADO         → TIA_PLN_FROM_TT_RSECADO       → PLN_SEGUIMIENTO PASO '08'
CC aprueba CTCALIDAD_D        → TUA_PLN_FROM_CTCALIDAD        → PLN_SEGUIMIENTO PASO '09'
Se crea REVISADO_G            → TIA_PLN_FROM_REVISADO_G       → PLN_SEGUIMIENTO PASO '10'
Se aprueba REVISADO_D         → (mismo trigger)               → PLN_SEGUIMIENTO PASO '11'
Entra a almacén PT (LOTES)    → TIA_PLN_FROM_LOTES_PT         → PLN_SEGUIMIENTO PASO '12'
Se despacha (LOTES.S_TRANSAC) → TUA_PLN_FROM_LOTES_DESPACHO  → PLN_SEGUIMIENTO PASO '14'


   -- PROPOSITO --------------------------------------------------
   Modulo de trazabilidad automatica de pedidos de produccion.
   Funciona como un "tablero de control en tiempo real" que lee
   los sistemas legacy mediante triggers y los transforma en un
   estado unificado por item de pedido en PLN_SEGUIMIENTO.

   Los empleados NO modifican sus pantallas ni flujos de trabajo.
   Cada operacion que ya realizan (registrar en ITEMPED, crear
   PARTIDA, aprobar CC, despachar) dispara un trigger que actualiza
   PLN_SEGUIMIENTO automaticamente sin intervencion adicional.

   NOTA IMPORTANTE: Ejecutar este archivo completo hace un
   DROP+RECREATE total (parr.0). Siempre re-ejecutar parr.10 despues
   para re-inicializar los items activos de PLN_SEGUIMIENTO.

   -- PEDIDO BASE DE REFERENCIA (evidencia real de BD) -----------
   Todo el diseno del flujo se baso en el analisis del pedido:
     NUM_PED=88501  NRO=5  PARTIDA=158938
   Ciclo completo: 31/03/2026 -> 18/05/2026 (48 dias)

   Linea de tiempo real confirmada en BD Oracle:
     31/03/2026  ITEMPED INSERT                     -> PASO '01'
     01/04/2026  ITEMPED_DET UPDATE (NROPROG)       -> PASO '02'
     01/05/2026  L_VALIDA_RECETA UPDATE ESTADO='3'  -> PASO '04' (LAB)
     08/05/2026  PARTIDA 158938 INSERT              -> PASO '03' (hilo entra produccion)
     18/05/2026  H_RPRODUC INSERT GUIA=158938       -> PASO '05' (produccion finalizada)
     18/05/2026  LOTES UPDATE S_TRANSAC='21'        -> PASO '14' (despachado)

   HALLAZGO CRITICO (origen BUG #40):
   El laboratorio aprueba la receta ANTES que la PARTIDA exista (caso historico 88501).
   La PARTIDA se crea ANTES que H_RPRODUC registre la produccion.
   v3.0 (26/05/2026): proceso fisico cambia -> PARTIDA se crea ANTES que Lab apruebe.
   Orden correlativo en flujo: '03' (Hilanderia) -> '04' (Lab) -> '05' (codigos alineados con ORDEN_PASO).

   -- FLUJO DE PRODUCCION --- MAQUINA DE ESTADOS (16 PASOS) -----
   Ciclo sin reproceso: 12-18 dias habiles.
   Ciclo con reproceso ('9R'): 14-22 dias habiles.

   HALLAZGO CRITICO v2.0 (21/05/2026):
   H_RPRODUC (TP_MAQ != 'G') es SIEMPRE post-TT (DEVANADO), NUNCA pre-TT.
   SITU_PART='R001' NUNCA ocurre en 2026 (sistema TT nuevo usa TT_RPRODUC TIPODOC='PA').
   El sistema nuevo TT inserta TT_RPRODUC con TIPODOC='PA' y ESTADO='3' directamente.

   ORDEN | PASO  | NOMBRE               | TABLA QUE LO ACTIVA                           | AREA
   ------+-------+----------------------+-----------------------------------------------+--------------
      1  | '01'  | Pedido Registrado    | ITEMPED INSERT                                | Ventas
      2  | '02'  | Planificado          | ITEMPED_DET UPDATE (NROPROG asignado)         | Planeamiento
      3  | '03'  | En Hilanderia        | PARTIDA INSERT (NROPROG NOT NULL)             | Hilanderia
      4  | '04'  | Laboratorio          | L_VALIDA_RECETA UPDATE ESTADO='3'             | Laboratorio
      5  | '05'  | Lote Disponible      | (reservado - ver nota HALLAZGO v2.0)          | Hilanderia
      6  | '06'  | Ingreso Tintoreria   | TT_RPRODUC INSERT TIPODOC='PA' (1er bano)     | Tintoreria
                               | o PARTIDA UPDATE SITU_PART='R001' (sistema old)|
      7  | '07'  | Tenido Completo      | TT_RPRODUC INSERT TIPODOC='PA' (todos banos)  | Tintoreria
                               | o TT_RPRODUC UPDATE ESTADO='3' (sistema old)   |
      8  | '08'  | Secado               | TT_RSECADO INSERT                             | Tintoreria
      9  | '09'  | CC TT Aprobado       | CTCALIDAD_D EST_EVAL='32' RES aprobado        | Calidad
     10  | '09B' | Gaseado (*)          | H_RPRODUC INSERT TP_MAQ='G'                   | Acabados
     11  | '9R'  | Reproceso CC         | CTCALIDAD_D EST_EVAL='32' RES NO aprobado     | Tintoreria
     12  | '10'  | Devanado             | H_RPRODUC INSERT TP_MAQ!='G' post-CC          | Devanado
     13  | '11'  | Revisado             | REVISADO_G INSERT (GUIA NOT NULL)             | Calidad
     14  | '12'  | Ingresado Alm PT     | LOTES INSERT TP='16' ALM IN(03,07,22,30)      | Almacen PT
     15  | '13'  | Listo para Despacho  | (automatico: SP_PLN_AVANZA_PASO)              | Almacen PT
     16  | '14'  | Despachado/Cerrado   | LOTES UPDATE S_TRANSAC IN ('21','23')         | Despacho

   NOTA PASO '05': En el sistema nuevo, H_RPRODUC dispara DEVANADO post-CC.
     Para el sistema legado (antes de 2026), H_RPRODUC era pre-TT. El trigger
     TIA_PLN_FROM_H_RPRODUC ahora detecta el contexto via COD_PASO_ACT:
       - Si PASO_ACT IN ('08','09','09B','9R') + TP_MAQ='G' -> PASO '09B'
       - Si PASO_ACT IN ('08','09','09B','9R') + TP_MAQ!='G' -> PASO '10' (Devanado)
       - Si PASO_ACT IN ('03') o anterior -> PASO '05' (Lote Disponible, sistema legado)

   (*) PASO '09B' Gaseado: SOLO si PROCESO='24' (PEINADO GASEADO).
       Para PROCESO='01' (Cardado) y '20' (Peinado): flujo salta '09'->'10'.

   Flujo nuevo (2026, sistema TT TIPODOC='PA'):
     [01]->[02]->[03]->[04]->[06]->[07]->[08]->[09]->[10]->[11]->[12]->[13]->[14]
   Con gaseado (PROCESO='24'):
     [01]->[02]->[03]->[04]->[06]->[07]->[08]->[09]->[09B]->[10]->[11]->[12]->[13]->[14]
   Reproceso:
     ...[09]->[9R]->[06]->[07]->[08]->[09]... (NRO_CICLO +1 en cada '9R')
   Stock directo (SOLO_DESPACHO='S'):
     [01]->[13] (sin produccion)

   Porcentaje de avance (PctAvance en C#):
     '01'->6%  '02'->13%  '03'->19%  '04'->25%  '05'->31%  '06'->38%
     '07'->44% '08'->50%  '09'->56%  '09B'->62% '9R'->56%  '10'->69%
     '11'->75% '12'->81%  '13'->88%  '14'->100%
   (C# switch: CodPasoAct switch { "01"=>6, "02"=>13, "03"=>19, "04"=>25, "05"=>31,
     "06"=>38, "07"=>44, "08"=>50, "09"=>56, "09B"=>62, "9R"=>56, "10"=>69,
     "11"=>75, "12"=>81, "13"=>88, "14"=>100, _ => 0 })

   Colores UI (PLN_ESTADO_CODIGO.COLOR_UI):
     '01'->#6c757d  '02'->#0d6efd  '03'->#0dcaf0  '04'->#6610f2  '05'->#17a2b8
     '06'->#6f42c1  '07'->#d63384  '08'->#20c997  '09'->#fd7e14  '09B'->#ffd700
     '9R'->#dc3545  '10'->#ffc107  '11'->#0d6efd  '12'->#198754  '13'->#20c997
     '14'->#198754

   -- CONTENIDO (orden de despliegue obligatorio) ----------------
     parr.0  Limpieza     DROP idempotente (jobs->triggers->pkg->vistas->tablas->secuencias)
     parr.1  Secuencias   PLN_SEQ_SEGUIM / EVENTO / ALERTA / FECHAS
     parr.2  Tablas       7 tablas PLN_*
     parr.3  Indices      14 indices de performance
     parr.4  Catalogo     parr.4.1 PLN_PARAM (9 filas) - parr.4.2 PLN_ESTADO_CODIGO (16 filas)
                          parr.4.3 UPDATE ORDEN_PASO para tabla ya deployada (BUG #40)
     parr.5  PKG SPEC     7 procedimientos publicos
     parr.6  PKG BODY     implementacion completa
     parr.7  Triggers     13 triggers -> PKG_PLN.*
     parr.8  Vistas       8 vistas V_PLN_*
     parr.9  Jobs         JOB_PLN_ALERTAS (cada hora) + JOB_PLN_CARGA (cada 4 horas: 00:00,04:00,08:00,12:00,16:00,20:00)
     parr.10 Init         SP_PLN_INIT_SEGUIMIENTO para pedidos activos (idempotente)

   -- TABLAS PLN_ ------------------------------------------------
   PLN_PARAM             PK: COD_PARAM              -- 9 parametros configurables
   PLN_ESTADO_CODIGO     PK: COD_PASO               -- 16 pasos del flujo
   PLN_SEGUIMIENTO       PK: ID_SEGUIM (surrogate)  -- 1 fila por (SERIE,NUM_PED,NRO,NUM_DET)
   PLN_LOG_EVENTOS       PK: ID_EVENTO              -- historial inmutable de transiciones
   PLN_ALERTA            PK: ID_ALERTA              -- alertas para supervisores
   PLN_CARGA_DIARIA      PK: (FECHA, COD_MAQ)       -- capacidad de maquinas por dia
   PLN_FECHAS_ESTIMADAS  PK: ID_FECH                -- historial de recalculos de fechas

   -- PROCEDIMIENTOS PUBLICOS (PKG_PLN.*) ------------------------
   SP_PLN_INIT_SEGUIMIENTO(serie, ped, nro, det, paso_ini)
     Crea la fila inicial en PLN_SEGUIMIENTO + primer evento AV.
     Idempotente: DUP_VAL_ON_INDEX -> NULL (si ya existe, no hace nada).
     paso_ini='01' para pedidos normales; paso_ini='13' para SOLO_DESPACHO='S'.
     Llamado por triggers TIA_PLN_FROM_ITEMPED y TUA_PLN_FROM_ITEMPED_DET.
     SIN COMMIT interno (ORA-04092 -- el padre hace el commit).

   SP_PLN_AVANZA_PASO(serie, ped, nro, det, nuevo_paso, tabla, id, kg, obs, fch_evento)
     Motor central del modulo. Realiza en una sola operacion:
       - Guard anti-retroceso: si ORDEN_PASO_NEW < ORDEN_PASO_ACT -> RETURN
         (excepciones: despacho parcial->'13', reinicio reproceso desde '9R')
       - Actualiza COD_PASO_ACT / COD_PASO_ANT
       - Actualiza la fecha real (FCH_REAL_*) = NVL(fch_evento, SYSDATE)
         fch_evento=NULL (default) => SYSDATE (triggers siempre pasan NULL)
         fch_evento IS NOT NULL    => fecha historica para retroalimentacion manual
       - Acumula KGs: KG_PRODUCIDOS solo en PASO '03' (PARTIDA.PESO_NETO)
                      KG_DESPACHADOS solo en PASO '14'
       - Despacho parcial: si KG_DESPACHADOS < CANTIDAD_ORIG -> PASO='13'
       - Cierra item: ESTADO='C' cuando KG_DESPACHADOS >= CANTIDAD_ORIG
       - Incrementa NRO_CICLO en PASO '9R'
       - Recalcula IND_RETRASO y DIAS_RETRASO
       - Inserta en PLN_LOG_EVENTOS (TIPO_EVENTO: 'AV' o 'RE')
     SELECT FOR UPDATE para concurrencia segura.
     NO_DATA_FOUND -> NULL (si el item aun no existe en PLN_, se ignora).
     SIN COMMIT interno (ORA-04092).

   SP_PLN_CALCULA_FECHAS(serie, ped, nro, det, motivo)
     Recalcula todas las FCH_EST_* del item. Motivos:
       'PED'=al crear pedido / 'PLA'=al planificar / 'REP'=reprogramacion / 'MAQ'=cambio maquina
     Algoritmo (a partir de FCH_REAL_PROGRAMADO, o SYSDATE si aun NULL):
       FCH_EST_HILANDERIA = fch_base
       FCH_EST_PARTIDA    = fch_base + CEIL(cantidad / (kgr_hr * HRS_HILANDERIA))
       FCH_EST_TIN_INI    = FCH_EST_PARTIDA + DIAS_BUFFER_LAB
       FCH_EST_TIN_FIN    = FCH_EST_TIN_INI + (hrs_tenido / 24)
       FCH_EST_SECADO     = FCH_EST_TIN_FIN + (HRS_SECADO / 24)
       FCH_EST_CALIDAD    = TRUNC(FCH_EST_SECADO) + DIAS_BUFFER_QC
       FCH_EST_DESPACHO   = FCH_EST_CALIDAD + DIAS_BUFFER_DESP
     kgr_hr: maquina asignada en ITEMPED_DET (ctrutas_titulo) ->
             MAX(kgr_hr) para titulo/proceso -> fallback: 10.
     Guarda historial en PLN_FECHAS_ESTIMADAS.
     Sincroniza ITEMPED_DET.FCH_ESTIMA_TENIDO y FCH_ESTIMA_CONO_UNO.

   SP_PLN_GENERA_ALERTAS
     Motor de alertas. Ejecutado por JOB_PLN_ALERTAS cada hora.
       'RET1' CRITICO  -> dias_retraso >= DIAS_ALERTA_CRIT (7)
       'RET2' ALTO     -> dias_retraso >= DIAS_ALERTA_ALTA (3)
       'SMP'  ALTO     -> mas de 2 dias en PASO '01' sin planificar
       'STN'  CRITICO  -> en PASO '05' y ya paso FCH_EST_TIN_INI
       'QCF'  CRITICO  -> en PASO '9R' (CC rechazado)
     NOT EXISTS para no duplicar alertas activas. Hace COMMIT propio.

   SP_PLN_CARGA_DIARIA_REFRESH(fch_ini, fch_fin)
     DELETE + INSERT en PLN_CARGA_DIARIA para el rango de fechas.
     Fuente: h_produccion_d. Calcula PCT_UTILIZACION e IND_SOBRECARGADA.
     Ejecutado por JOB_PLN_CARGA cada 4 horas (00:00,04:00,08:00,12:00,16:00,20:00). Hace COMMIT propio.

   SP_PLN_KGR_REFRESH
     Recalcula PLN_KGR_TITULO desde H_RPRODUC (ventana 24 meses, >= 3 muestras).
     Usa mediana (PERCENTILE_CONT 0.5) en vez de promedio para robustez ante outliers.
     Inserta filas por (titulo, proceso, cod_maq) + fila fallback (cod_maq='*') por titulo/proceso.
     Ejecutado por JOB_PLN_KGR el dia 1 de cada mes a las 01:00. Hace COMMIT propio.

   SP_PLN_CIERRE_ITEM(id_seguim, motivo, usuario)
     Cierre manual autorizado: ESTADO='A' -> 'C'. Inserta evento 'CI'.
     Solo para correcciones operativas supervisadas.

   SP_PLN_REPROGRAMAR(serie, ped, nro, det, nueva_fch_desp, motivo, usuario)
     Actualiza FCH_EST_DESPACHO. Recalcula IND_RETRASO/DIAS_RETRASO.
     Guarda en PLN_FECHAS_ESTIMADAS (MOTIVO_RECALCULO='REP').
     Inserta evento TIPO_EVENTO='RE' en PLN_LOG_EVENTOS.

   -- TRIGGERS (15; TODOS con EXCEPTION WHEN OTHERS THEN NULL) --
   TIA_PLN_FROM_ITEMPED          -> PASO '01'       AFTER INSERT ITEMPED
   TUA_PLN_FROM_ITEMPED          -> PASO '01'       COMPOUND AFTER UPDATE ITEMPED (estado '0'→activo)
                                    Captura aprobaciones tardías de ítems insertados como borrador.
   TUA_PLN_FROM_PEDIDO           -> PASO '01'       COMPOUND AFTER UPDATE PEDIDO (f_aprobacion NULL→valor)
                                    Captura pedidos aprobados después del INSERT de ITEMPED.
   TUA_PLN_FROM_ITEMPED_DET      -> PASO '02'       AFTER UPDATE ITEMPED_DET (NROPROG asignado)
   TUA_PLN_FROM_L_VALIDA_RECETA  -> PASO '04'       AFTER UPDATE L_VALIDA_RECETA (ESTADO IN '3','4')
   TIA_PLN_FROM_L_VALIDA_RECETA  -> PASO '04'       AFTER INSERT L_VALIDA_RECETA (ESTADO IN '3','4' — bypass/directo)
   TIA_PLN_FROM_PARTIDA          -> PASO '03'       AFTER INSERT PARTIDA (NROPROG NOT NULL)
   TIA_PLN_FROM_H_RPRODUC        -> PASO '05'/'09B'/'10' AFTER INSERT H_RPRODUC (GUIA NOT NULL)
                                    LOGICA: si PASO_ACT IN ('08','09','09B','9R'):
                                      TP_MAQ='G' -> '09B' (Gaseado)
                                      TP_MAQ!='G' -> '10' (Devanado post-CC)
                                    sino -> '05' (Lote Disponible, sistema legado)
   TIA_PLN_FROM_TT_RPRODUC_PA    -> PASO '06'/'07'  AFTER INSERT TT_RPRODUC (TIPODOC='PA' ESTADO='3'; TIPODOC='IR' cualquier ESTADO)
                                    LOGICA: PA → cnt_banos=1 → PASO '06'; cnt_banos>=tot → PASO '07'
                                            IR → cnt_banos_any=1 → PASO '06' (FIX v2.3: captura INSERT ESTADO<>'3')
   TUA_PLN_FROM_PARTIDA          -> PASO '06'       AFTER UPDATE PARTIDA (SITU_PART='R001')
                                    (sistema legado; 0 registros en 2026)
   TUA_PLN_FROM_TT_RPRODUC       -> PASO '07'       COMPOUND TRIGGER UPDATE TT_RPRODUC (ESTADO='3')
                                    (sistema legado via ING_RECETAS_G)
                                    FIX v2.1: COMPOUND para evitar ORA-04091 mutating table
   TIA_PLN_FROM_TT_RSECADO       -> PASO '08'       AFTER INSERT TT_RSECADO
   TUA_PLN_FROM_CTCALIDAD        -> PASO '09'/'9R'  AFTER UPDATE CTCALIDAD_D (EST_EVAL='32')
                                    RESULTADO IN ('01','21','29') -> '09' (Aprobado/Concesionado)
                                    RESULTADO='30' o cualquier otro no nulo -> '9R' (RECHAZADO)
   TIA_PLN_FROM_RECTIF_RECETA    -> LOG 'RC'  AFTER INSERT L_RECTIFICA_RECETA
                                    Registra inicio de rectificacion en PLN_LOG_EVENTOS
   TUA_PLN_FROM_RECTIF_RECETA    -> LOG 'RA'  AFTER UPDATE ESTADO='6' L_RECTIFICA_RECETA
                                    Registra aprobacion de rectificacion en PLN_LOG_EVENTOS
   TIA_PLN_FROM_REVISADO_G       -> PASO '11'       AFTER INSERT REVISADO_G (GUIA NOT NULL)
                                    (v2.0: Revisado - Calidad final; ya no Devanado)
   TIA_PLN_FROM_LOTES_PT         -> PASO '12'       AFTER INSERT LOTES (TP='16', ALM IN '03','07','22','30')
   TUA_PLN_FROM_LOTES_DESPACHO   -> PASO '14'       AFTER UPDATE LOTES (S_TRANSAC IN '21','23')

   PASO '13' calculado internamente por SP_PLN_AVANZA_PASO (despacho parcial).
   TIA_PLN_FROM_REVISADO (REVISADO_D) ELIMINADO en v2.0: REVISADO_G es suficiente.

   -- VISTAS V_PLN_* ---------------------------------------------
   V_PLN_ESTADO_PEDIDO    Dashboard -- estado por pedido completo (agrupado)
   V_PLN_ESTADO_ITEM      Detalle -- por item con semaforo y join a CLIENTES/ARTICUL
   V_PLN_TRAZABILIDAD     Timeline -- todas las fechas estimadas vs. reales
   V_PLN_ALERTAS_ACTIVAS  Panel -- alertas ESTADO='A' ordenadas C>A>M>B
   V_PLN_CARGA_MAQUINAS   Gantt -- proximos 30 dias (SOBRECARGADA/ALTA/MEDIA/DISPONIBLE)
   V_PLN_PENDIENTES_DESP  Lista de despacho -- pasos '12' y '13', stock disponible
   V_PLN_KPI_CUMPLIMIENTO OTIF mensual -- pct_otif, ciclo_prom, retraso_prom
   V_PLN_KPI_PRODUCCION   KPIs por maquina y mes -- kg_hora, eficiencia

   -- PLN_PARAM --- PARAMETROS CONFIGURABLES ---------------------
   Modificar sin recompilar: UPDATE PLN_PARAM SET VALOR_NUM=x WHERE COD_PARAM='...';

   COD_PARAM         VALOR  DESCRIPCION
   ----------------  -----  -------------------------------------
   HRS_HILANDERIA      22   Horas/dia operativas hilanderia
   HRS_TINTORERIA      24   Horas/dia operativas tintoreria
   HRS_SECADO           8   Horas buffer post-secado
   DIAS_BUFFER_LAB      1   Dias laboratorio antes de TT (receta)
   DIAS_BUFFER_QC       1   Dias control calidad post-secado
   DIAS_BUFFER_DESP     1   Dias para preparar despacho
   DIAS_ALERTA_CRIT     7   Dias retraso -> alerta CRITICA ('C')
   DIAS_ALERTA_ALTA     3   Dias retraso -> alerta ALTA ('A')
   DIAS_ALERTA_MEDIA    1   Dias retraso -> alerta MEDIA ('M')

   -- ALERTAS (PLN_ALERTA) ---------------------------------------
   TIP_ALERTA | NIVEL | CONDICION
   -----------+-------+------------------------------------------
   'RET1'     | 'C'   | dias_retraso >= DIAS_ALERTA_CRIT (7)
   'RET2'     | 'A'   | dias_retraso >= DIAS_ALERTA_ALTA (3)
   'SMP'      | 'A'   | PASO '01' > 2 dias sin planificar
   'STN'      | 'C'   | PASO '03' paso FCH_EST_TIN_INI sin entrar a TT
   'QCF'      | 'C'   | PASO '9R' (CC rechazado)
   Estado: 'A'=Activa  'R'=Resuelta  'I'=Ignorada
   NivelColor C#: 'C'->"danger" | 'A'->"warning" | 'M'->"info" | 'B'->"secondary"

   -- TIPOS DE EVENTO (PLN_LOG_EVENTOS.TIPO_EVENTO) --------------
   'AV' = Avance automatico de paso (trigger)
   'RE' = Reprogramacion de fecha (manual)
   'AL' = Generacion de alerta
   'CI' = Cierre manual de item
   'RC' = Rectificacion de receta iniciada (INSERT L_RECTIFICA_RECETA, paso '9R')
   'RA' = Rectificacion de receta aprobada  (UPDATE L_RECTIFICA_RECETA ESTADO='6')

   -- MOTIVOS RECALCULO (PLN_FECHAS_ESTIMADAS.MOTIVO_RECALCULO) --
   'PED' = Al crear el pedido
   'PLA' = Al planificar (NROPROG asignado)
   'REP' = Reprogramacion manual (SP_PLN_REPROGRAMAR)
   'MAQ' = Cambio de maquina asignada

   -- NAVEGACION CLAVE (legacy -> PLN_SEGUIMIENTO) ---------------
   ITEMPED_DET.NROPROG    = PARTIDA.NROPROG       (1:1 -- unica clave confiable)
   H_RPRODUC.GUIA         = PARTIDA.NUMERO
   TT_RSECADO.GUIA        = PARTIDA.NUMERO
   ING_RECETAS_G.R_NUMERO = PARTIDA.NUMERO        (NO .GUIA -- columna GUIA no existe)
   CTCALIDAD_D.GUIA       = PARTIDA.NUMERO
   CTCALIDAD_D.NRO_PEDIDO = ITEMPED_DET.NUM_PED
   CTCALIDAD_D.SER_PARTIDA= ITEMPED_DET.NRO       (item del pedido)
   CTCALIDAD_D.NROPART    = ITEMPED_DET.NRO_DET   (sub-lote)
   REVISADO_G.GUIA        = PARTIDA.NUMERO
   LOTES.PARTIDA          = PARTIDA.NUMERO
   CLIENTES.NOMBRE        (NO .DESCRIPCION -- verificado en BD)
   ITEMPED columna LOTE   NO EXISTE (solo en ITEMPED_DET)

   -- ESTADOS PARTIDA.SITU_PART (semaforo fisico) ----------------
   (vacio)        -> En hilanderia / disponible    (PASOS '03'-'05')
   'R001'         -> Recibida en tintoreria        (PASO  '06')
   'P'            -> En proceso de tenido          (PASO  '07')
   'A'            -> Acabada / salio de TT         (PASOS '08'-'09')
   'X' + ESTADO=9 -> Cerrada / despachada          (PASO  '14')

   -- ALMACENES PT VALIDOS (LOTES.COD_ALM -> PASO '12') ---------
   '03' = Almacen PT principal     (42,000+ articulos)
   '07' = Almacen PT externo
   '22' = Almacen PT secundario
   '30' = Almacen Madeja/HANK
   '01' = Administrativo (37 articulos, stock=0) -- NO activa PASO '12'

   -- TIPOS DE DESPACHO (LOTES.S_TRANSAC -> PASO '14') ----------
   '21' = Despacho mercado nacional
   '23' = Despacho exportacion

   -- CTCALIDAD_D.RESULTADO --- CC TINTORERIA --------------------
   Aprobados: '01', '21', '29'  -> PASO '09' CC Aprobado
   Reproceso: '30'              -> PASO '9R' CC Rechazado
   Frecuencia real de rechazo: ~2.7% de lotes evaluados.

   -- REGLAS DE NEGOCIO CRITICAS ---------------------------------
   R1. TRAZABILIDAD POR NROPROG:
       ITEMPED_DET.NROPROG = PARTIDA.NROPROG es la unica relacion 1:1
       confiable. El campo LOTE se reutiliza entre pedidos.

   R2. FK desde PLN_SEGUIMIENTO a ITEMPED: NUNCA NOT DEFERRABLE IMMEDIATE.
       En Oracle 11g, el trigger AFTER INSERT ON ITEMPED no puede ver el
       padre aun no commiteado -> ORA-02291 silenciado -> ningun item se
       registra. FK eliminada del DDL. (BUG #39 -- corregido)

   R3. SP_PLN_AVANZA_PASO y SP_PLN_INIT_SEGUIMIENTO SIN COMMIT.
       ORA-04092 prohibe COMMIT dentro de un trigger. El COMMIT lo hace
       la transaccion padre. Cuando se llaman manualmente desde la app,
       ejecutar COMMIT por separado despues de la llamada al SP.

   R4. BANOS MULTIPLES DE TINTORERIA (75% de los casos):
       TUA_PLN_FROM_TT_RPRODUC avanza a '07' SOLO cuando TODOS los
       registros TT_RPRODUC de esa partida tienen ESTADO='3'.

   R5. DESPACHO PARCIAL:
       Si KG_DESPACHADOS < CANTIDAD_ORIG -> COD_PASO_ACT retrocede a '13'.
       El item cierra (ESTADO='C') unicamente cuando KG_DESPACHADOS >= CANTIDAD_ORIG.

   R6. ESTADO CON MULTIPLES SUBLOTES:
       Para el dashboard, mostrar el peor paso (MIN ORDEN_PASO) entre
       todos los NUM_DET activos del mismo (SERIE, NUM_PED, NRO).

   R7. SOLO_DESPACHO='S' (stock / maquila / re-venta):
       SP_PLN_INIT_SEGUIMIENTO inicializa directamente en PASO '13'.
       La app debe mostrar badge "Stock" diferenciado.

   R8. FCH_ENTREGA_COMP (fecha compromiso por ítem — v2.3):
       Prioridad 1: ITEMPED_DET.FHC_ENTREGA   (fecha FINAL de compromiso — LA QUE MANDA)
       Prioridad 2: ITEMPED_DET.FCH_ENTREGA_ORI (fecha ORIGINAL de compromiso del artículo)
       Prioridad 3: ITEMPED_DET.FCH_REG_ENTREGA (fecha de registro del ítem — uso interno)
       Prioridad 4: ITEMPED.F_MAXPED           (máximo comprometido a nivel de ítem)
       Prioridad 5: PEDIDO.FECHA + NVL(PEDIDO.PLAZO_ENTREGA, 30)  (fallback genérico)
       NOTA: cada artículo tiene su propia FCH_ENTREGA_COMP (ya no es igual para todos).
       Es el unico campo que determina retraso: SYSDATE > FCH_ENTREGA_COMP.

   R9. ALMACEN.STOCK NUNCA MANUAL:
       Lo mantienen triggers Oracle. La app solo lee.

   R10. PLN_ NUNCA BLOQUEA OPERACIONES DE PLANTA:
        Todos los triggers usan EXCEPTION WHEN OTHERS THEN NULL.
        Si PLN_ falla, la operacion de planta continua. El dato de
        seguimiento se puede recuperar re-ejecutando parr.10.

   -- COMO LLAMAR DESDE C# / DAPPER -----------------------------
   // Parametros Oracle: ':' (no '@')
   // Correccion manual (solo supervisores autorizados):
   await conn.ExecuteAsync(
       "BEGIN PKG_PLN.SP_PLN_AVANZA_PASO(:serie,:ped,:nro,:det,:paso,'MANUAL',NULL,:kg,:obs); END;",
       new { serie, ped, nro, det, paso, kg = (decimal?)null, obs = "Correccion manual" });
   await conn.ExecuteAsync("COMMIT");

   // Reprogramacion de fecha de despacho:
   await conn.ExecuteAsync(
       "BEGIN PKG_PLN.SP_PLN_REPROGRAMAR(:serie,:ped,:nro,:det,:fch,:motivo,:usuario); END;",
       new { serie, ped, nro, det, fch = nuevaFecha, motivo, usuario = User.Identity!.Name });

   // Consulta Dashboard (prefijo de esquema {S}):
   // $"SELECT s.id_seguim, s.num_ped, ... FROM {S}PLN_SEGUIMIENTO s WHERE s.estado='A'"

   -- STACK TECNOLOGICO DE LA CAPA .NET --------------------------
   Framework : ASP.NET Core MVC (.NET 8) -- Controllers + Views
   ORM       : Dapper con SQL explicito (NO Entity Framework para Oracle)
   Driver    : Oracle.ManagedDataAccess.Core (ODP.NET)
   Multi-emp.: SIG (LaColonial) / ARBONA / SOLSA
     - Empresa activa: HttpContext.Session["EmpresaConexion"]
     - Prefijo esquema: propiedad S -> "SIG." / "ARBONA." / "SOLSA."
     - Queries: $"SELECT * FROM {S}PLN_SEGUIMIENTO WHERE ..."
   Patron controllers: heredar OracleBaseController (verifica Session["OracleUser"])
   Patron services   : heredar OracleServiceBase (GetOracleConnectionString(), S)
   Convenciones Dapper:
     - Parametros con ':': :numPed, :serie, :paso (no '@')
     - Fechas: TO_DATE(:fecha,'DD/MM/YYYY')
     - decimal C# para KGs; double solo para porcentajes UI
     - Tipos nullable en modelos (DBNull.Value -> null)
     - SP_PLN_GENERA_ALERTAS y SP_PLN_CARGA_DIARIA_REFRESH hacen COMMIT propio
     - SP_PLN_AVANZA_PASO y SP_PLN_INIT_SEGUIMIENTO NO hacen COMMIT (ORA-04092)

   -- OBJETOS INVALIDOS PRE-EXISTENTES (no relacionados al PLN_) --
   PKG_COMERCIAL BODY -> PLS-00103 (error de sintaxis)
   POST_TO_API        -> PLS-00302 (columna inexistente)
   REGISTRA_LOGIN     -> ORA-00942 (tabla inexistente)
   V_DRAW             -> ORA-00918 (columna ambigua)

   -- ORDEN DE DESPLIEGUE ----------------------------------------
   0. parr.0  Limpieza   jobs -> triggers -> pkg -> vistas -> tablas -> secuencias
   1. parr.1  Secuencias
   2. parr.2  Tablas
   3. parr.3  Indices
   4. parr.4  Catalogo   PLN_PARAM + PLN_ESTADO_CODIGO + UPDATEs BUG #40
   5. parr.5  PKG SPEC
   6. parr.6  PKG BODY
   7. parr.7  Triggers
   8. parr.8  Vistas
   9. parr.9  Jobs       enabled=>FALSE en DEV / TRUE en PROD
   10. parr.10 Init      SP_PLN_INIT_SEGUIMIENTO para pedidos activos (UNA VEZ, idempotente)
   ============================================================ */



-- ============================================================
-- §0  LIMPIEZA PREVIA — Re-despliegue idempotente
-- ============================================================
-- Elimina todos los objetos PLN_ en orden inverso de dependencia.
-- Cada DROP está protegido con EXCEPTION WHEN OTHERS THEN NULL
-- para que el script funcione tanto en instancia vacía (primera vez)
-- como sobre producción (re-despliegue).
--
-- ORDEN: jobs → triggers → paquete → vistas → tablas → secuencias
--   · Triggers primero: evita que disparen durante el DROP de tablas.
--   · Paquete antes de tablas: usa %ROWTYPE de PLN_SEGUIMIENTO.
--   · Tablas con CASCADE CONSTRAINTS: elimina FKs hijos en cascada.
-- ============================================================

-- Permite líneas en blanco dentro de bloques SQL al ejecutar con @
SET SQLBLANKLINES ON

PROMPT ============================================================
PROMPT §0  Limpieza previa PLN_ (re-despliegue idempotente)
PROMPT ============================================================

-- §0.1  Jobs DBMS_SCHEDULER
PROMPT >>> §0.1 Eliminando jobs PLN_...
BEGIN
  DBMS_SCHEDULER.DROP_JOB(job_name=>'JOB_PLN_ALERTAS', force=>TRUE);
EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN
  DBMS_SCHEDULER.DROP_JOB(job_name=>'JOB_PLN_CARGA', force=>TRUE);
EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN
  DBMS_SCHEDULER.DROP_JOB(job_name=>'JOB_PLN_KGR', force=>TRUE);
EXCEPTION WHEN OTHERS THEN NULL; END;
/

-- §0.2  Triggers (sobre tablas legacy — deben caer antes que el paquete)
PROMPT >>> §0.2 Eliminando triggers PLN_...
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TIA_PLN_FROM_ITEMPED';         EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TUA_PLN_FROM_ITEMPED_DET';     EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TIA_PLN_FROM_ITEMPED_DET';     EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TIA_PLN_FROM_H_RPRODUC';       EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TIA_PLN_FROM_PARTIDA';         EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TUA_PLN_FROM_L_VALIDA_RECETA'; EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TIA_PLN_FROM_L_VALIDA_RECETA'; EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TUA_PLN_FROM_PARTIDA';         EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TUA_PLN_FROM_TT_RPRODUC';      EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TIA_PLN_FROM_TT_RSECADO';      EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TUA_PLN_FROM_CTCALIDAD';       EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TIA_PLN_FROM_RECTIF_RECETA'; EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TUA_PLN_FROM_RECTIF_RECETA'; EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TIA_PLN_FROM_REVISADO_G';      EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TIA_PLN_FROM_REVISADO';        EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TIA_PLN_FROM_LOTES_PT';        EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TUA_PLN_FROM_LOTES_DESPACHO';  EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TIA_PLN_FROM_TT_RPRODUC_PA';  EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TUA_PLN_FROM_ITEMPED';         EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TUA_PLN_FROM_PEDIDO';          EXCEPTION WHEN OTHERS THEN NULL; END;
/

-- §0.3  Paquete
PROMPT >>> §0.3 Eliminando paquete PKG_PLN...
BEGIN EXECUTE IMMEDIATE 'DROP PACKAGE PKG_PLN'; EXCEPTION WHEN OTHERS THEN NULL; END;
/

-- §0.4  Vistas
PROMPT >>> §0.4 Eliminando vistas V_PLN_*...
BEGIN EXECUTE IMMEDIATE 'DROP VIEW V_PLN_KPI_PRODUCCION';   EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP VIEW V_PLN_KPI_CUMPLIMIENTO'; EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP VIEW V_PLN_PENDIENTES_DESP';  EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP VIEW V_PLN_CARGA_MAQUINAS';   EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP VIEW V_PLN_ALERTAS_ACTIVAS';  EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP VIEW V_PLN_TRAZABILIDAD';     EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP VIEW V_PLN_ESTADO_ITEM';      EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP VIEW V_PLN_ESTADO_PEDIDO';    EXCEPTION WHEN OTHERS THEN NULL; END;
/

-- §0.5  Tablas (hijos primero; CASCADE CONSTRAINTS elimina FKs residuales)
PROMPT >>> §0.5 Eliminando tablas PLN_*...
BEGIN EXECUTE IMMEDIATE 'DROP TABLE PLN_FECHAS_ESTIMADAS CASCADE CONSTRAINTS'; EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TABLE PLN_KGR_TITULO       CASCADE CONSTRAINTS'; EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TABLE PLN_CARGA_DIARIA    CASCADE CONSTRAINTS'; EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TABLE PLN_ALERTA          CASCADE CONSTRAINTS'; EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TABLE PLN_LOG_EVENTOS     CASCADE CONSTRAINTS'; EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TABLE PLN_SEGUIMIENTO     CASCADE CONSTRAINTS'; EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TABLE PLN_ESTADO_CODIGO   CASCADE CONSTRAINTS'; EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TABLE PLN_PARAM           CASCADE CONSTRAINTS'; EXCEPTION WHEN OTHERS THEN NULL; END;
/

-- §0.6  Secuencias
PROMPT >>> §0.6 Eliminando secuencias PLN_SEQ_*...
BEGIN EXECUTE IMMEDIATE 'DROP SEQUENCE PLN_SEQ_SEGUIM'; EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP SEQUENCE PLN_SEQ_EVENTO'; EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP SEQUENCE PLN_SEQ_ALERTA'; EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP SEQUENCE PLN_SEQ_FECHAS'; EXCEPTION WHEN OTHERS THEN NULL; END;
/

-- ============================================================
-- §0.7  Migración incremental v2.0 — ADD columnas nuevas
-- ============================================================
-- Permite re-desplegar sobre una BD con datos existentes SIN
-- perder el historial de PLN_SEGUIMIENTO.
-- ORA-01430 = columna ya existe (fresh install post-DROP) → ignorar
-- ORA-00942 = tabla no existe aún (fresh install pre-CREATE) → ignorar
-- Ejecutar SIEMPRE, en ambos modos de despliegue.
PROMPT >>> §0.7 Migración v2.0: ADD columnas nuevas a PLN_SEGUIMIENTO...
BEGIN EXECUTE IMMEDIATE 'ALTER TABLE PLN_SEGUIMIENTO ADD FCH_REAL_GASEADO  DATE';          EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'ALTER TABLE PLN_SEGUIMIENTO ADD COD_MAQ_TT        VARCHAR2(6)';   EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'ALTER TABLE PLN_SEGUIMIENTO ADD COD_MAQ_SECADO    VARCHAR2(6)';   EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'ALTER TABLE PLN_SEGUIMIENTO ADD COD_MAQ_GAS       VARCHAR2(6)';   EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'ALTER TABLE PLN_SEGUIMIENTO ADD COD_MAQ_DEVAN     VARCHAR2(6)';   EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'ALTER TABLE PLN_SEGUIMIENTO ADD TP_MAQ_DEVAN      VARCHAR2(1)';   EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'ALTER TABLE PLN_SEGUIMIENTO ADD COD_MAQ_PLANIF    VARCHAR2(6)';   EXCEPTION WHEN OTHERS THEN NULL; END;
/
-- v2.2 (26/05/2026): 3 fechas del ciclo de aprobación/planificación + usuario planificador
BEGIN EXECUTE IMMEDIATE 'ALTER TABLE PLN_SEGUIMIENTO ADD FCH_APROBACION   DATE';           EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'ALTER TABLE PLN_SEGUIMIENTO ADD FCH_PLANIF        DATE';           EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'ALTER TABLE PLN_SEGUIMIENTO ADD USR_PLANIF        VARCHAR2(15)'; EXCEPTION WHEN OTHERS THEN NULL; END;
/
-- v2.3 (26/05/2026): flujo dual Lab/Hilandería — PASO '03' y '04' son concurrentes
-- 'L'=Lab aprobó antes de crear PARTIDA (81% de casos)
-- 'H'=PARTIDA creada antes de aprobación Lab (3% de casos, p.ej. pedidos urgentes)
-- 'N'=Sin Lab (sin tintorería; flujo solo hilandería/despacho)
BEGIN EXECUTE IMMEDIATE 'ALTER TABLE PLN_SEGUIMIENTO ADD IND_FLUJO         VARCHAR2(1) DEFAULT ''L'''; EXCEPTION WHEN OTHERS THEN NULL; END;
/
-- v2.3 (26/05/2026): fechas de compromiso por artículo desde ITEMPED_DET
-- FCH_ENTREGA_ORI = fecha ORIGINAL de compromiso del artículo (primera promesa formal)
-- FCH_REG_ENTREGA = fecha de registro del ítem (interna, menor prioridad)
-- FCH_ENTREGA_COMP ahora usa FHC_ENTREGA (final, la que manda) > FCH_ENTREGA_ORI > FCH_REG_ENTREGA > F_MAXPED > fallback
BEGIN EXECUTE IMMEDIATE 'ALTER TABLE PLN_SEGUIMIENTO ADD FCH_REG_ENTREGA  DATE';          EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'ALTER TABLE PLN_SEGUIMIENTO ADD FCH_ENTREGA_ORI   DATE';          EXCEPTION WHEN OTHERS THEN NULL; END;
/

PROMPT >>> §0 Limpieza completada — iniciando instalación de objetos PLN_...
PROMPT

-- ============================================================
-- §1  SECUENCIAS
-- ============================================================
-- PLN_SEQ_SEGUIM  : PK surrogate de PLN_SEGUIMIENTO (ID_SEGUIM)
-- PLN_SEQ_EVENTO  : PK de PLN_LOG_EVENTOS (ID_EVENTO)
-- PLN_SEQ_ALERTA  : PK de PLN_ALERTA (ID_ALERTA)
-- PLN_SEQ_FECHAS  : PK de PLN_FECHAS_ESTIMADAS (ID_FECH)
-- START WITH 1000 → deja espacio para inserciones manuales de prueba < 1000

CREATE SEQUENCE PLN_SEQ_SEGUIM
  START WITH 1 INCREMENT BY 1 NOCACHE NOCYCLE;

CREATE SEQUENCE PLN_SEQ_EVENTO
  START WITH 1 INCREMENT BY 1 NOCACHE NOCYCLE;

CREATE SEQUENCE PLN_SEQ_ALERTA
  START WITH 1 INCREMENT BY 1 NOCACHE NOCYCLE;

CREATE SEQUENCE PLN_SEQ_FECHAS
  START WITH 1 INCREMENT BY 1 NOCACHE NOCYCLE;


-- ============================================================
-- §2  TABLAS
-- ============================================================

-- ────────────────────────────────────────────────────────────
-- §2.1  PLN_PARAM — Parámetros configurables del módulo PLN_
-- ────────────────────────────────────────────────────────────
-- Tabla de configuración clave-valor. Permite ajustar umbrales
-- de alertas, horas de turno y buffers SIN tocar código PL/SQL.
-- SP_PLN_CALCULA_FECHAS y SP_PLN_GENERA_ALERTAS leen estos valores
-- en cada ejecución (BEGIN SELECT valor_num INTO ... EXCEPTION WHEN NO_DATA_FOUND THEN NULL).
-- Modificar con: UPDATE PLN_PARAM SET VALOR_NUM=X WHERE COD_PARAM='NOMBRE'; — Parámetros del módulo ──────────────────
CREATE TABLE PLN_PARAM (
  COD_PARAM   VARCHAR2(20)   NOT NULL,
  DESCRIPCION VARCHAR2(100)  NOT NULL,
  VALOR_NUM   NUMBER(12,4),
  VALOR_TEXT  VARCHAR2(100),
  VALOR_DATE  DATE,
  A_MDUSER    VARCHAR2(15),
  A_MDFECHA   DATE,
  CONSTRAINT PK_PLN_PARAM PRIMARY KEY (COD_PARAM)
);

-- ────────────────────────────────────────────────────────────
-- §2.1b PLN_KGR_TITULO — Velocidades de producción kg/hora por título
-- ────────────────────────────────────────────────────────────
-- Tabla calculada automáticamente por SP_PLN_KGR_REFRESH (JOB_PLN_KGR, mensual).
-- Fuente: H_RPRODUC (estado='3', últimos 24 meses, >= 3 muestras por combinación).
-- Sustituye la consulta a CTRUTAS_TITULO en SP_PLN_CALCULA_FECHAS, ya que
-- CTRUTAS_TITULO usa notación textil ("14/2", "20/1") mientras PLN_SEGUIMIENTO
-- y H_RPRODUC usan código numérico ("014", "076"), haciendo el JOIN imposible.
--
-- PK: (TITULO, PROCESO, COD_MAQ)
--   COD_MAQ = '*' → fila de fallback con la mediana de TODAS las máquinas
--             para ese (TITULO, PROCESO). Usada cuando no hay máquina asignada.
--
-- KGR_HR: mediana (PERCENTILE_CONT 0.5) — más robusta que AVG ante outliers.
-- KGR_HR_AVG: promedio — guardado para auditoría / análisis.
-- N_MUESTRAS: cantidad de runs de H_RPRODUC usados en el cálculo.
-- MESES_HIST: ventana usada (por defecto 24).
-- FCH_CALCULO: última vez que se calculó esta fila.
--
-- Consulta desde SP_PLN_CALCULA_FECHAS:
--   1. Si hay máquina asignada (v_maquina IS NOT NULL):
--      SELECT kgr_hr FROM PLN_KGR_TITULO
--      WHERE titulo=... AND proceso=... AND cod_maq=v_maquina
--   2. Fallback (sin máquina o no encontrada):
--      SELECT kgr_hr FROM PLN_KGR_TITULO
--      WHERE titulo=... AND proceso=... AND cod_maq='*'
--   3. Si sigue NULL → fallback hardcodeado 10 kg/hr
CREATE TABLE PLN_KGR_TITULO (
  TITULO        VARCHAR2(10)  NOT NULL,
  PROCESO       VARCHAR2(4)   NOT NULL,
  COD_MAQ       VARCHAR2(10)  NOT NULL,  -- '*' = todas las máquinas (fallback)
  KGR_HR        NUMBER(12,4),            -- mediana kg/hora (robusto ante outliers)
  KGR_HR_AVG    NUMBER(12,4),            -- promedio kg/hora (referencia)
  N_MUESTRAS    NUMBER(6),               -- cantidad de runs usados
  MESES_HIST    NUMBER(3)   DEFAULT 24,  -- ventana de meses del cálculo
  FCH_CALCULO   DATE,                    -- última ejecución de SP_PLN_KGR_REFRESH
  CONSTRAINT PK_PLN_KGR_TITULO PRIMARY KEY (TITULO, PROCESO, COD_MAQ)
);
CREATE INDEX IX_PLN_KGR_TITULO_TP ON PLN_KGR_TITULO (TITULO, PROCESO);

-- ────────────────────────────────────────────────────────────
-- §2.2  PLN_ESTADO_CODIGO — Catálogo de pasos del flujo de producción
-- ────────────────────────────────────────────────────────────
-- Define los 16 pasos de la máquina de estados.
-- ORDEN_PASO : define la secuencia (1=mínimo avance, 16=despachado)
-- TABLA_ORIGEN: tabla legacy cuyo evento activa este paso
-- ES_FINAL    : 'S' solo para PASO '14' (único que cierra el seguimiento)
-- COLOR_UI    : color hexadecimal para badges y barras en la UI Bootstrap/ApexCharts
-- Leído por: V_PLN_ESTADO_ITEM (JOIN PLN_ESTADO_CODIGO) para nombre y color en vistas MVC
-- Consulta típica desde C#:
--   JOIN pln_estado_codigo ec ON ec.cod_paso = s.cod_paso_act — Catálogo de pasos ──────────────
CREATE TABLE PLN_ESTADO_CODIGO (
  COD_PASO    VARCHAR2(3)    NOT NULL,  -- '09B' tiene 3 chars (BUG #39)
  NOMBRE_PASO VARCHAR2(60)   NOT NULL,
  DESCRIPCION VARCHAR2(200),
  ORDEN_PASO  NUMBER(2)      NOT NULL,
  TABLA_ORIGEN VARCHAR2(30),
  ES_FINAL    VARCHAR2(1)    DEFAULT 'N',
  COLOR_UI    VARCHAR2(10),
  CONSTRAINT PK_PLN_ESTADO PRIMARY KEY (COD_PASO)
);

-- ────────────────────────────────────────────────────────────
-- §2.3  PLN_SEGUIMIENTO — Tabla maestra del módulo PLN_
-- ────────────────────────────────────────────────────────────
-- UNA FILA por ítem de pedido + sub-lote (SERIE, NUM_PED, NRO, NUM_DET).
-- PK surrogate ID_SEGUIM + UK (SERIE, NUM_PED, NRO, NUM_DET).
-- FK a ITEMPED (SERIE, NUM_PED, NRO) — ELIMINADA (BUG #39): NOT DEFERRABLE IMMEDIATE
-- causaba ORA-02291 dentro del AFTER INSERT trigger. La integridad referencial
-- la garantiza SP_PLN_INIT_SEGUIMIENTO (lee ITEMPED antes de insertar).
--
-- DUALIDAD DE FECHAS:
--   FCH_EST_* = fechas estimadas calculadas por SP_PLN_CALCULA_FECHAS.
--               Se recalculan en cada replanificación (MOTIVO: PED/PLA/REP/MAQ).
--   FCH_REAL_* = fechas reales escritas por SP_PLN_AVANZA_PASO al avanzar.
--                Son inmutables (log histórico en PLN_LOG_EVENTOS).
--
-- ACUMULACIÓN DE KG (cuándo se suma cada campo):
--   KG_PRODUCIDOS  : solo PASO '03' (En Hilandería — PARTIDA creada)
--   KG_EN_TIN      : solo PASO '06' (ingresó físicamente a tintorería)
--   KG_EN_ALM_PT   : solo PASO '12' (LOTES INSERT almacén PT)
--   KG_DESPACHADOS : solo PASO '14' (LOTES UPDATE despacho, puede ser parcial)
--   KG_PENDIENTES  : = CANTIDAD_ORIG - KG_DESPACHADOS (se reduce en cada PASO '14')
--
-- CICLO DE REPROCESO:
--   NRO_CICLO = 1 (primer ciclo), 2 (primer reproceso), 3 (segundo reproceso)...
--   Al llegar a PASO '9R', NRO_CICLO++ y FCH_REAL_TIN_* se limpian.
--   PLN_LOG_EVENTOS guarda todo el historial de ciclos.
--
-- ESTADOS DEL ÍTEM (ESTADO):
--   'A' = Activo  (en producción)
--   'C' = Cerrado (KG_DESPACHADOS >= CANTIDAD_ORIG, solo en PASO '14')
--   'X' = Anulado (cancelado manualmente por operador)
--
-- INDICADORES:
--   IND_URGENTE    : 'S' si ITEMPED_DET.URGENTE='S' o anticipo cobrado
--   IND_RETRASO    : 'S' si SYSDATE > FCH_ENTREGA_COMP
--   IND_REPROCESO  : 'S' si pasó por PASO '9R', 'N' si aprobó CC ('09')
--
-- AUDITORÍA:
--   A_ADUSER/A_ADFECHA = creación | A_MDUSER/A_MDFECHA = última modificación — Tabla maestra de seguimiento ─────
CREATE TABLE PLN_SEGUIMIENTO (
  -- PK / FK al pedido
  ID_SEGUIM         NUMBER(12)      NOT NULL,
  SERIE             NUMBER(3)       NOT NULL,
  NUM_PED           NUMBER(8)       NOT NULL,
  NRO               NUMBER(2)       NOT NULL,
  NUM_DET           NUMBER(3)       NOT NULL,

  -- Datos del ítem (desnormalizados para performance)
  COD_CLIENTE       VARCHAR2(15),
  COD_ART           VARCHAR2(25),
  COLOR             VARCHAR2(7),
  TITULO            VARCHAR2(10),
  PROCESO           VARCHAR2(4),
  LOTE              VARCHAR2(20),
  CANTIDAD_ORIG     NUMBER(12,4),
  SOLO_DESPACHO     VARCHAR2(1)     DEFAULT 'N',  -- 'S' = omite flujo de producción

  -- Paso actual
  COD_PASO_ACT      VARCHAR2(3)     NOT NULL,  -- '09B' tiene 3 chars (BUG #39)
  COD_PASO_ANT      VARCHAR2(3),

  -- Reproceso
  NRO_CICLO         NUMBER(3)       DEFAULT 1 NOT NULL,  -- incrementa en cada reproceso

  -- Fechas comprometidas
  FCH_PEDIDO        DATE            NOT NULL,     -- PEDIDO.FECHA (registro del pedido)
  FCH_APROBACION    DATE,                         -- PEDIDO.F_APROBACION (aprobación del pedido)
  FCH_PLANIF        DATE,                         -- ITEMPED_DET.FHC_PROG (fecha programada de producción)
  FCH_ENTREGA_COMP  DATE,
  -- v2.3: fechas de compromiso por artículo (desde ITEMPED_DET — prioridad R8)
  -- FHC_ENTREGA (final, la que manda) es la FCH_ENTREGA_COMP directamente.
  -- Las dos columnas siguientes conservan el historial de compromisos.
  FCH_REG_ENTREGA   DATE,                         -- ITEMPED_DET.FCH_REG_ENTREGA — fecha de registro del ítem (interna, menor prioridad)
  FCH_ENTREGA_ORI   DATE,                         -- ITEMPED_DET.FCH_ENTREGA_ORI — fecha ORIGINAL de compromiso del artículo (primera promesa formal)

  -- Fechas estimadas (calculadas por SP_PLN_CALCULA_FECHAS)
  FCH_EST_HILANDERIA  DATE,
  FCH_EST_PARTIDA     DATE,
  FCH_EST_TIN_INI     DATE,
  FCH_EST_TIN_FIN     DATE,
  FCH_EST_SECADO      DATE,
  FCH_EST_CALIDAD     DATE,
  FCH_EST_DESPACHO    DATE,

  -- Fechas reales (actualizado por triggers)
  FCH_REAL_PROGRAMADO DATE,   -- PASO '02': NROPROG asignado
  FCH_REAL_PRODUCCION DATE,   -- PASO '04': inicio hilandería
  FCH_REAL_PARTIDA    DATE,   -- PASO '05': lote creado
  FCH_REAL_TIN_INI    DATE,   -- PASO '06': entrada a TT
  FCH_REAL_TIN_FIN    DATE,   -- PASO '07': todos los baños completos
  FCH_REAL_SECADO     DATE,   -- PASO '08': secado registrado
  FCH_REAL_CC_TINTO   DATE,   -- PASO '09': CC aprobado
  FCH_REAL_CC_RECHAZO DATE,   -- PASO '9R': CC rechazado → reproceso
  FCH_REAL_DEVANADO   DATE,   -- PASO '10': devanado
  FCH_REAL_CALIDAD    DATE,   -- PASO '11': revisado aprobado
  FCH_REAL_ALM_PT     DATE,   -- PASO '12': ingreso almacén PT
  FCH_REAL_DESPACHO   DATE,   -- PASO '14': despacho

  -- Cantidades acumuladas
  KG_PRODUCIDOS     NUMBER(12,4)   DEFAULT 0,
  KG_EN_TIN         NUMBER(12,4)   DEFAULT 0,
  KG_EN_ALM_PT      NUMBER(12,4)   DEFAULT 0,
  KG_DESPACHADOS    NUMBER(12,4)   DEFAULT 0,
  KG_PENDIENTES     NUMBER(12,4)   DEFAULT 0,

  -- Indicadores
  IND_RETRASO       VARCHAR2(1)    DEFAULT 'N',
  DIAS_RETRASO      NUMBER(5)      DEFAULT 0,
  IND_URGENTE       VARCHAR2(1)    DEFAULT 'N',
  IND_REPROCESO     VARCHAR2(1)    DEFAULT 'N',
  -- v2.3: flujo dual — PASO '03' (Hilandería) y '04' (Lab) son CONCURRENTES
  -- 'L'=Lab aprobó ANTES de crear PARTIDA (~81%)  'H'=PARTIDA creada ANTES de Lab (~3%)  'N'=Sin Lab
  IND_FLUJO         VARCHAR2(1)    DEFAULT 'L',

  -- Fechas reales adicionales (v2.0)
  FCH_REAL_GASEADO    DATE,   -- PASO '09B': gaseado (solo PROCESO='24')

  -- Tracking de maquinas por etapa (v2.0)
  COD_MAQ_TT        VARCHAR2(6),  -- PASO '06'/'07': maquina TT (de TT_RPRODUC.COD_MAQ)
  COD_MAQ_SECADO    VARCHAR2(6),  -- PASO '08': maquina secado (de TT_RSECADO.COD_MAQ)
  COD_MAQ_GAS       VARCHAR2(6),  -- PASO '09B': maquina gaseadora (de H_RPRODUC.COD_MAQ)
  COD_MAQ_DEVAN     VARCHAR2(6),  -- PASO '10': maquina devanado (de H_RPRODUC.COD_MAQ)
  TP_MAQ_DEVAN      VARCHAR2(2),  -- PASO '10': tipo maquina devanado (A=AUTOCONER, R=REDINA, J=MADRITE)
  COD_MAQ_PLANIF    VARCHAR2(6),  -- PASO '02': maquina planificada (de ITEMPED_DET.MAQUINA)
  USR_PLANIF        VARCHAR2(15), -- PASO '02': login del planificador (de ITEMPED_DET.A_ADUSER al asignar NROPROG)

  -- Referencias a objetos del flujo
  NUM_PROGRAMA      NUMBER(8),    -- NROPROG asignado (= PARTIDA.NROPROG 1:1; H_PROGRAMACION es obsoleta, datos hasta 2013)
  NUM_PARTIDA       NUMBER(8),    -- PARTIDA.NUMERO
  NUM_RECETA_TIN    NUMBER(8),    -- ING_RECETAS_G.NUMERO
  NUM_KARDEX_DESP   NUMBER(8),    -- KARDEX_G.NUMERO del despacho

  -- Estado
  ESTADO            VARCHAR2(1)    DEFAULT 'A',  -- A=Activo, C=Cerrado, X=Anulado

  -- Auditoría
  A_ADUSER          VARCHAR2(15),
  A_ADFECHA         DATE,
  A_MDUSER          VARCHAR2(15),
  A_MDFECHA         DATE,

  CONSTRAINT PK_PLN_SEGUIMIENTO PRIMARY KEY (ID_SEGUIM),
  CONSTRAINT UK_PLN_SEGUIM      UNIQUE (SERIE, NUM_PED, NRO, NUM_DET)
  -- FK_PLN_SEG_ITEMPED removido intencionalmente (BUG #39):
  -- Un FK NOT DEFERRABLE IMMEDIATE hacia ITEMPED falla con ORA-02291 cuando
  -- TIA_PLN_FROM_ITEMPED (AFTER INSERT ON ITEMPED) intenta insertar en esta tabla,
  -- porque el padre ITEMPED aún no ha hecho COMMIT en el contexto del trigger.
  -- La integridad referencial la garantiza SP_PLN_INIT_SEGUIMIENTO (lee ITEMPED
  -- antes de insertar), por lo que el FK es innecesario y perjudicial aquí.
);

-- ────────────────────────────────────────────────────────────
-- §2.4  PLN_LOG_EVENTOS — Historial inmutable de eventos
-- ────────────────────────────────────────────────────────────
-- Registro append-only de TODOS los eventos del ciclo de vida de un ítem.
-- NUNCA se modifica ni elimina una vez insertado.
-- TABLA_ORIGEN + ID_OBJETO_ORIGEN: identifica qué fila legacy disparó el evento.
-- TIPO_EVENTO:
--   'AV' = Avance de paso (generado por triggers automáticos)
--   'RE' = Reprogramación de fecha (SP_PLN_REPROGRAMAR)
--   'AL' = Alerta generada
--   'CI' = Cierre manual (SP_PLN_CIERRE_ITEM)
-- FCH_ESTIMADA_ANT / FCH_ESTIMADA_NUE: usado solo en tipo 'RE' (antes/después)
-- KG_CANTIDAD: los kg involucrados en el evento (útil para PASO '05','12','14')
-- Consulta típica desde C# (página Pedido.cshtml — historial de trazabilidad):
--   SELECT ev.fch_evento, ec.nombre_paso, ev.tipo_evento, ev.tabla_origen,
--          ev.kg_cantidad, ev.observacion, ev.usuario
--   FROM pln_log_eventos ev JOIN pln_estado_codigo ec ON ec.cod_paso = ev.cod_paso
--   WHERE ev.num_ped = :numPed AND ev.serie = :serie ORDER BY ev.fch_evento DESC
CREATE TABLE PLN_LOG_EVENTOS (
  ID_EVENTO        NUMBER(12)      NOT NULL,
  ID_SEGUIM        NUMBER(12)      NOT NULL,
  SERIE            NUMBER(3)       NOT NULL,
  NUM_PED          NUMBER(8)       NOT NULL,
  NRO              NUMBER(2)       NOT NULL,
  NUM_DET          NUMBER(3)       NOT NULL,
  COD_PASO         VARCHAR2(3)     NOT NULL,  -- '09B' tiene 3 chars (BUG #39)
  DESC_PASO        VARCHAR2(100),
  TABLA_ORIGEN     VARCHAR2(30),
  ID_OBJETO_ORIGEN NUMBER(12),
  FCH_EVENTO       DATE            NOT NULL,
  USUARIO          VARCHAR2(15),
  KG_CANTIDAD      NUMBER(12,4),
  FCH_ESTIMADA_ANT DATE,
  FCH_ESTIMADA_NUE DATE,
  OBSERVACION      VARCHAR2(300),
  TIPO_EVENTO      VARCHAR2(2),   -- AV=Avance, RE=Reprogramación, AL=Alerta, CI=Cierre
  CONSTRAINT PK_PLN_LOG_EVENTOS PRIMARY KEY (ID_EVENTO)
);

-- ────────────────────────────────────────────────────────────
-- §2.5  PLN_ALERTA — Alertas activas del módulo PLN_
-- ────────────────────────────────────────────────────────────
-- Generadas automáticamente por SP_PLN_GENERA_ALERTAS (JOB cada hora).
-- TIP_ALERTA + ID_SEGUIM tiene NOT EXISTS para evitar duplicados activos.
-- FCH_LIMITE: fecha límite antes de escalar (puede ser NULL).
-- NIVEL: 'C'=Crítico → 'A'=Alto → 'M'=Medio → 'B'=Bajo
--   NivelColor en C# (PlnAlerta.NivelColor): "danger"|"warning"|"info"|"secondary"
-- ESTADO: 'A'=Activa | 'R'=Resuelta | 'I'=Ignorada
--   Resolver: UPDATE PLN_ALERTA SET ESTADO='R', FCH_RESOLUCION=SYSDATE, USUARIO_RESUELVE=:usr WHERE id_alerta=:id
-- COD_MAQ: máquina involucrada (para alertas tipo 'SOBR' — sobrecarga)
-- La vista V_PLN_ALERTAS_ACTIVAS filtra ESTADO='A' y ordena por nivel.
CREATE TABLE PLN_ALERTA (
  ID_ALERTA        NUMBER(12)      NOT NULL,
  ID_SEGUIM        NUMBER(12),
  SERIE            NUMBER(3),
  NUM_PED          NUMBER(8),
  NRO              NUMBER(2),
  NUM_DET          NUMBER(3),
  TIP_ALERTA       VARCHAR2(4)     NOT NULL,
  NIVEL            VARCHAR2(1)     NOT NULL,   -- C/A/M/B
  TITULO           VARCHAR2(100)   NOT NULL,
  DETALLE          VARCHAR2(500),
  FCH_ALERTA       DATE            NOT NULL,
  FCH_LIMITE       DATE,
  DIAS_RETRASO     NUMBER(5),
  TABLA_REF        VARCHAR2(30),
  ID_REF           NUMBER(12),
  COD_MAQ          VARCHAR2(6),
  COD_CLIENTE      VARCHAR2(15),
  ESTADO           VARCHAR2(1)     DEFAULT 'A',   -- A=Activa, R=Resuelta, I=Ignorada
  FCH_RESOLUCION   DATE,
  USUARIO_RESUELVE VARCHAR2(15),
  OBSERV_RESOL     VARCHAR2(300),
  A_ADUSER         VARCHAR2(15),
  A_ADFECHA        DATE,
  CONSTRAINT PK_PLN_ALERTA PRIMARY KEY (ID_ALERTA)
);

-- ────────────────────────────────────────────────────────────
-- §2.6  PLN_CARGA_DIARIA — Carga de máquinas por día
-- ────────────────────────────────────────────────────────────
-- PK: (FECHA, COD_MAQ). Una fila por máquina por día.
-- TP_MAQ: 'H'=Hilandería | 'T'=Tintorería
-- Máquinas Hilandería: PAB/HI/etc. | Tintorería: R01-R19 (Thies), M01-M08 (Hank)
-- HORAS_CAPACIDAD = 24 - horas mantenimiento registradas en MA_PROGRAMA
-- KG_CAPACIDAD    = HORAS_CAPACIDAD * (kgr_hr promedio de la máquina)
-- PCT_UTILIZACION = KG_REAL / KG_CAPACIDAD * 100
-- PCT_CARGA       = KG_ASIGNADOS / KG_CAPACIDAD * 100
-- IND_SOBRECARGADA = 'S' si KG_ASIGNADOS > KG_CAPACIDAD
-- Refrescada por JOB_PLN_CARGA (cada 4 horas) con ventana de 30 días.
-- Visualización en CargaMaquinas.cshtml (ApexCharts Heatmap):
--   Verde: PCT_CARGA < 60% | Amarillo: 60-80% | Naranja: 80-95% | Rojo: > 95%
CREATE TABLE PLN_CARGA_DIARIA (
  FECHA            DATE            NOT NULL,
  COD_MAQ          VARCHAR2(6)     NOT NULL,
  TP_MAQ           VARCHAR2(1)     NOT NULL,   -- H=Hilandería, T=Tintorería
  HORAS_CAPACIDAD  NUMBER(5,2)     DEFAULT 24,
  KG_CAPACIDAD     NUMBER(12,4),
  HORAS_ASIGNADAS  NUMBER(5,2)     DEFAULT 0,
  KG_ASIGNADOS     NUMBER(12,4)    DEFAULT 0,
  NRO_PEDIDOS      NUMBER(5)       DEFAULT 0,
  HORAS_REAL       NUMBER(5,2)     DEFAULT 0,
  KG_REAL          NUMBER(12,4)    DEFAULT 0,
  PCT_UTILIZACION  NUMBER(5,2)     DEFAULT 0,
  PCT_CARGA        NUMBER(5,2)     DEFAULT 0,
  IND_SOBRECARGADA VARCHAR2(1)     DEFAULT 'N',
  FCH_CALCULO      DATE,
  A_MDUSER         VARCHAR2(15),
  A_MDFECHA        DATE,
  CONSTRAINT PK_PLN_CARGA PRIMARY KEY (FECHA, COD_MAQ)
);

-- ────────────────────────────────────────────────────────────
-- §2.7  PLN_FECHAS_ESTIMADAS — Historial de recálculos de fechas
-- ────────────────────────────────────────────────────────────
-- Snapshot inmutable de cada vez que SP_PLN_CALCULA_FECHAS o
-- SP_PLN_REPROGRAMAR actualizan las fechas estimadas del ítem.
-- MOTIVO_RECALCULO:
--   'PED' = Primera estimación al registrar el pedido (trigger ITEMPED)
--   'PLA' = Reestimación al asignar programa (trigger ITEMPED_DET NROPROG)
--   'REP' = Reprogramación manual via SP_PLN_REPROGRAMAR (página Pedido.cshtml)
--   'MAQ' = Cambio de máquina asignada
-- DIFER_DIAS: diferencia en días respecto a la estimación anterior
--   (positivo = se demoró más, negativo = se adelantó)
-- Útil para análisis de precisión de la planificación y tendencias.
CREATE TABLE PLN_FECHAS_ESTIMADAS (
  ID_FECH            NUMBER(12)    NOT NULL,
  ID_SEGUIM          NUMBER(12)    NOT NULL,
  FCH_CALCULO        DATE          NOT NULL,
  MOTIVO_RECALCULO   VARCHAR2(4),   -- PED/PLA/REP/MAQ
  FCH_EST_HILANDERIA DATE,
  FCH_EST_PARTIDA    DATE,
  FCH_EST_TIN_INI    DATE,
  FCH_EST_TIN_FIN    DATE,
  FCH_EST_SECADO     DATE,
  FCH_EST_CALIDAD    DATE,
  FCH_EST_DESPACHO   DATE,
  DIFER_DIAS         NUMBER(5),
  USUARIO            VARCHAR2(15),
  CONSTRAINT PK_PLN_FECHAS PRIMARY KEY (ID_FECH)
);


-- ============================================================
-- §3  ÍNDICES
-- ============================================================
-- Estrategia de indexación:
--   PLN_SEGUIMIENTO: índices por los patrones de consulta más frecuentes:
--     · IX_PLN_SEG_PEDIDO   → consultas por pedido (Dashboard, Pedido.cshtml)
--     · IX_PLN_SEG_CLIENTE  → filtro por cliente en Dashboard
--     · IX_PLN_SEG_PASO     → consultas por paso activo + estado (Dashboard, alertas)
--     · IX_PLN_SEG_FCH_DESP → ordenamiento por fecha estimada de despacho
--     · IX_PLN_SEG_ALERTA   → filtro de ítems retrasados (SP_PLN_GENERA_ALERTAS)
--   PLN_LOG_EVENTOS: índices para trazabilidad y auditoría:
--     · IX_PLN_LOG_SEG      → JOIN desde PLN_SEGUIMIENTO
--     · IX_PLN_LOG_PEDIDO   → consulta historial por pedido
--     · IX_PLN_LOG_FECHA    → ordenamiento cronológico
--   PLN_ALERTA: índices para el panel de alertas activas:
--     · IX_PLN_ALERT_SEG    → JOIN desde PLN_SEGUIMIENTO
--     · IX_PLN_ALERT_ESTADO → filtro ESTADO='A' + ordenamiento por nivel/fecha
--     · IX_PLN_ALERT_PEDIDO → consulta alertas de un pedido específico
--   PLN_CARGA_DIARIA: índices para el Gantt de carga de máquinas:
--     · IX_PLN_CARGA_MAQ    → consulta por máquina y rango de fechas
--     · IX_PLN_CARGA_FCH    → consulta por fecha y tipo de máquina

CREATE INDEX IX_PLN_SEG_PEDIDO   ON PLN_SEGUIMIENTO  (NUM_PED, SERIE);
CREATE INDEX IX_PLN_SEG_CLIENTE  ON PLN_SEGUIMIENTO  (COD_CLIENTE);
CREATE INDEX IX_PLN_SEG_PASO     ON PLN_SEGUIMIENTO  (COD_PASO_ACT, ESTADO);
CREATE INDEX IX_PLN_SEG_FCH_DESP ON PLN_SEGUIMIENTO  (FCH_EST_DESPACHO, ESTADO);
CREATE INDEX IX_PLN_SEG_ALERTA   ON PLN_SEGUIMIENTO  (IND_RETRASO, ESTADO);
CREATE INDEX IX_PLN_LOG_SEG      ON PLN_LOG_EVENTOS  (ID_SEGUIM);
CREATE INDEX IX_PLN_LOG_PEDIDO   ON PLN_LOG_EVENTOS  (NUM_PED, SERIE);
CREATE INDEX IX_PLN_LOG_FECHA    ON PLN_LOG_EVENTOS  (FCH_EVENTO);
CREATE INDEX IX_PLN_ALERT_SEG    ON PLN_ALERTA       (ID_SEGUIM);
CREATE INDEX IX_PLN_ALERT_ESTADO ON PLN_ALERTA       (ESTADO, NIVEL, FCH_ALERTA);
CREATE INDEX IX_PLN_ALERT_PEDIDO ON PLN_ALERTA       (NUM_PED);
CREATE INDEX IX_PLN_CARGA_MAQ    ON PLN_CARGA_DIARIA (COD_MAQ, FECHA);
CREATE INDEX IX_PLN_CARGA_FCH    ON PLN_CARGA_DIARIA (FECHA, TP_MAQ);
CREATE INDEX IX_PLN_SEG_DIAS_RET ON PLN_SEGUIMIENTO  (ESTADO, DIAS_RETRASO, COD_PASO_ACT);
-- ↑ Cubre SP_PLN_GENERA_ALERTAS: RET1/RET2 filtran por estado='A' AND dias_retraso >= N

-- Índice en tabla legacy necesario para TUA_PLN_FROM_TT_RPRODUC.
-- ING_RECETAS_G tiene 469K filas. Su PK es (TP_TRANSAC, SERIE, NUMERO); buscar solo por
-- NUMERO sin las columnas líderes obliga a full scan. Este índice lo evita.
-- IX_ING_RECETAS_G_NUM: índice en tabla legacy; usa BEGIN/END para no fallar en re-despliegue
BEGIN
  EXECUTE IMMEDIATE 'CREATE INDEX IX_ING_RECETAS_G_NUM ON ING_RECETAS_G (NUMERO)';
EXCEPTION WHEN OTHERS THEN NULL;  -- ORA-00955 si ya existe: ignorar
END;
/


-- ============================================================
-- §4  DATOS CATÁLOGO
-- ============================================================
-- Datos de referencia del módulo PLN_.
-- Ejecutar en este orden:
--   1. INSERT INTO PLN_PARAM      → 10 parámetros configurables
--   2. INSERT INTO PLN_ESTADO_CODIGO → 16 pasos del flujo
-- Idempotente: si ya existen, usar MERGE o borrar+reinsertar.
-- Los valores de PLN_PARAM se leen en tiempo de ejecución en
-- SP_PLN_CALCULA_FECHAS y SP_PLN_GENERA_ALERTAS (no compilados).
-- Los colores de PLN_ESTADO_CODIGO son usados directamente en
-- V_PLN_ESTADO_ITEM y en el frontend (badges, progress bars).

-- ── §4.1  Parámetros del sistema (PLN_PARAM) ─────────────────
-- Modificables en producción sin recompilar el paquete.
-- Ejemplo: UPDATE PLN_PARAM SET VALOR_NUM=20 WHERE COD_PARAM='HRS_HILANDERIA';
INSERT INTO PLN_PARAM VALUES ('DIAS_ALERTA_CRIT',  'Días retraso para alerta CRÍTICA',         7,  NULL, NULL, USER, SYSDATE);
INSERT INTO PLN_PARAM VALUES ('DIAS_ALERTA_ALTA',  'Días retraso para alerta ALTA',             3,  NULL, NULL, USER, SYSDATE);
INSERT INTO PLN_PARAM VALUES ('DIAS_ALERTA_MEDIA', 'Días retraso para alerta MEDIA',            1,  NULL, NULL, USER, SYSDATE);
INSERT INTO PLN_PARAM VALUES ('HRS_HILANDERIA',    'Horas/día operativas hilandería',           22, NULL, NULL, USER, SYSDATE);
INSERT INTO PLN_PARAM VALUES ('HRS_TINTORERIA',    'Horas/día operativas tintorería',           24, NULL, NULL, USER, SYSDATE);
INSERT INTO PLN_PARAM VALUES ('HRS_SECADO',        'Horas buffer post-secado',                   8, NULL, NULL, USER, SYSDATE);
INSERT INTO PLN_PARAM VALUES ('DIAS_BUFFER_LAB',   'Días buffer para laboratorio (receta)',      1, NULL, NULL, USER, SYSDATE);
INSERT INTO PLN_PARAM VALUES ('DIAS_BUFFER_QC',    'Días para control de calidad',               1, NULL, NULL, USER, SYSDATE);
INSERT INTO PLN_PARAM VALUES ('DIAS_BUFFER_DESP',  'Días para preparar despacho',                1, NULL, NULL, USER, SYSDATE);
INSERT INTO PLN_PARAM VALUES ('PCT_CIERRE_DESPACHO','% mínimo despachado para cerrar ítem (95=5% merma OK)', 95, NULL, NULL, USER, SYSDATE);
COMMIT;

-- ── §4.2  Catálogo de pasos del flujo (PLN_ESTADO_CODIGO) ───────
-- Columnas: COD_PASO, NOMBRE_PASO, DESCRIPCION, ORDEN_PASO,
--            TABLA_ORIGEN, ES_FINAL, COLOR_UI
-- Orden de avance REAL (confirmado 21/05/2026 v2.1):
--   01->02->03->04->(05)->06->07->08->09->(09B)->10->11->12->13->14
-- PASO '05' (Lote Disponible): solo sistema legado pre-2026 (H_RPRODUC pre-TT)
-- PASO '06' sistema nuevo: TT_RPRODUC INSERT TIPODOC='PA' 1er bano
-- PASO '07' sistema nuevo: TT_RPRODUC INSERT TIPODOC='PA' todos banos OK
-- PASO '10' (Devanado): H_RPRODUC INSERT TP_MAQ!='G' cuando PASO_ACT >= '08' (post-CC)
-- PASO '11' (Revisado): REVISADO_G INSERT (v2.0: era PASO '10' via REVISADO_G)
-- Solo PASO '14' tiene ES_FINAL='S'.
INSERT INTO PLN_ESTADO_CODIGO VALUES ('01','Pedido Registrado',        'Item de pedido creado en ITEMPED',                                               1,'ITEMPED',          'N','#6c757d');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('02','Planificado',              'Etapa asignada en ITEMPED_DET (NROPROG asignado)',                                2,'ITEMPED_DET',      'N','#0d6efd');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('03','En Hilanderia',            'PARTIDA INSERT - lote ingresado a produccion hilanderia',                         3,'PARTIDA',          'N','#0dcaf0');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('04','Laboratorio',              'L_VALIDA_RECETA UPDATE ESTADO=3 - receta de tintoreria validada',                 4,'L_VALIDA_RECETA',  'N','#6610f2');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('05','Lote Disponible',          'H_RPRODUC INSERT - sistema legado (pre-2026 hilanderia finalizada)',              5,'H_RPRODUC',        'N','#17a2b8');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('06','Ingreso Tintoreria',       'TT_RPRODUC INSERT TIPODOC=PA (1er bano) o PARTIDA SITU_PART=R001 (legado)',       6,'TT_RPRODUC',       'N','#6f42c1');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('07','Tenido Completo',          'TT_RPRODUC INSERT TIPODOC=PA (todos banos OK) o TT_RPRODUC UPD ESTADO=3 (legado)',7,'TT_RPRODUC',       'N','#d63384');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('08','Secado',                   'TT_RSECADO INSERT - secado post-tintoreria registrado',                           8,'TT_RSECADO',       'N','#20c997');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('09','CC TT Aprobado',           'CTCALIDAD_D RESULTADO IN (01,21,29) - aprobado/concesionado',                     9,'CTCALIDAD_D',      'N','#fd7e14');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('09B','Gaseado',                 'H_RPRODUC INSERT TP_MAQ=G - gaseado (solo PROCESO=24 PEINADO GASEADO)',          10,'H_RPRODUC',        'N','#ffd700');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('9R','CC TT Rechazado/Reproceso','CTCALIDAD_D RESULTADO NOT IN (01,21,29) - rechazado, requiere reproceso',         11,'CTCALIDAD_D',      'N','#dc3545');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('10','Devanado',                 'H_RPRODUC INSERT TP_MAQ!=G cuando PASO_ACT>=08 (post-CC)',                       12,'H_RPRODUC',        'N','#ffc107');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('11','Revisado',                 'REVISADO_G INSERT GUIA=PARTIDA - calidad final aprobada (v2.0)',                  13,'REVISADO_G',       'N','#0d6efd');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('12','Ingresado Alm PT',         'LOTES INSERT COD_ALM IN (03,07,22,30) TP_TRANSAC=16',                            14,'LOTES',            'N','#198754');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('13','Listo para Despacho',      'Stock en almacen, saldo pendiente de despacho (SP_PLN_AVANZA_PASO)',              15,NULL,               'N','#20c997');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('14','Despachado/Cerrado',       'LOTES UPDATE S_TRANSAC IN (21,23) - despacho completo',                          16,'LOTES',            'S','#198754');
COMMIT;

-- ── §4.3  v2.1 — Renombre COD_PASO correlativo (21/05/2026) ───────────────────────
-- En v2.1 los códigos se renombraron para que sean CORRELATIVOS con ORDEN_PASO:
--   Antes (v2.0): '03'=Hilanderia(ord 4), '04'=Lote Disponible(ord 5), '05'=Lab(ord 3)
--   Ahora (v2.1): '03'=Laboratorio(ord 3), '04'=Hilanderia(ord 4), '05'=Lote Disponible(ord 5)
-- Fresh deploy: los INSERTs de §4.2 ya son correctos (v3.0 aplica) — este bloque no se ejecuta.
-- Solo en upgrade de BD existente desde v2.0 a v2.1 (sin DROP de PLN_ESTADO_CODIGO):
/*
  UPDATE PLN_ESTADO_CODIGO SET COD_PASO='Z03' WHERE COD_PASO='03';  -- Hilanderia -> temporal
  UPDATE PLN_ESTADO_CODIGO SET COD_PASO='Z04' WHERE COD_PASO='04';  -- Lote Disponible -> temporal
  UPDATE PLN_ESTADO_CODIGO SET COD_PASO='03'  WHERE COD_PASO='05';  -- Lab          -> '03'
  UPDATE PLN_ESTADO_CODIGO SET COD_PASO='04'  WHERE COD_PASO='Z03'; -- Hilanderia   -> '04'
  UPDATE PLN_ESTADO_CODIGO SET COD_PASO='05'  WHERE COD_PASO='Z04'; -- Lote Disp.   -> '05'
  -- Migrar filas activas en PLN_SEGUIMIENTO:
  UPDATE PLN_SEGUIMIENTO SET COD_PASO_ACT=CASE COD_PASO_ACT
    WHEN '03' THEN 'Z03' WHEN '04' THEN 'Z04' WHEN '05' THEN '03' ELSE COD_PASO_ACT END;
  UPDATE PLN_SEGUIMIENTO SET COD_PASO_ANT=CASE COD_PASO_ANT
    WHEN '03' THEN 'Z03' WHEN '04' THEN 'Z04' WHEN '05' THEN '03' ELSE COD_PASO_ANT END;
  UPDATE PLN_SEGUIMIENTO SET COD_PASO_ACT=CASE COD_PASO_ACT
    WHEN 'Z03' THEN '04' WHEN 'Z04' THEN '05' ELSE COD_PASO_ACT END;
  UPDATE PLN_SEGUIMIENTO SET COD_PASO_ANT=CASE COD_PASO_ANT
    WHEN 'Z03' THEN '04' WHEN 'Z04' THEN '05' ELSE COD_PASO_ANT END;
  COMMIT;
*/

-- ── §4.4  v3.0 — Re-orden En Hilanderia / Laboratorio (26/05/2026) ──────────────
-- El proceso físico cambia: PARTIDA se crea ANTES de la aprobación de laboratorio.
--   Antes (v2.1): '03'=Laboratorio(ord 3), '04'=En Hilanderia(ord 4)
--   Ahora (v3.0): '03'=En Hilanderia(ord 3), '04'=Laboratorio(ord 4)
-- Fresh deploy: los INSERTs de §4.2 ya son correctos para v3.0 — no ejecutar.
-- Solo en upgrade de BD existente desde v2.1:
/*
  -- 1. Actualizar catálogo (solo nombres, tabla origen y color):
  UPDATE PLN_ESTADO_CODIGO
    SET NOMBRE_PASO='En Hilanderia', TABLA_ORIGEN='PARTIDA', COLOR_UI='#0dcaf0'
  WHERE COD_PASO='03';
  UPDATE PLN_ESTADO_CODIGO
    SET NOMBRE_PASO='Laboratorio', TABLA_ORIGEN='L_VALIDA_RECETA', COLOR_UI='#6610f2'
  WHERE COD_PASO='04';
  -- 2. Migrar PLN_SEGUIMIENTO (swap atómico '03' <-> '04'):
  UPDATE PLN_SEGUIMIENTO SET
    COD_PASO_ACT = CASE WHEN COD_PASO_ACT='03' THEN '04'
                        WHEN COD_PASO_ACT='04' THEN '03' ELSE COD_PASO_ACT END,
    COD_PASO_ANT = CASE WHEN COD_PASO_ANT='03' THEN '04'
                        WHEN COD_PASO_ANT='04' THEN '03' ELSE COD_PASO_ANT END
  WHERE COD_PASO_ACT IN ('03','04') OR COD_PASO_ANT IN ('03','04');
  -- 3. Migrar PLN_LOG_EVENTOS (swap '03' <-> '04'):
  UPDATE PLN_LOG_EVENTOS SET
    COD_PASO = CASE WHEN COD_PASO='03' THEN '04'
                    WHEN COD_PASO='04' THEN '03' ELSE COD_PASO END
  WHERE COD_PASO IN ('03','04');
  COMMIT;
*/


-- ============================================================
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
             -- DIAS_ROD positivo = estimada para después del compromiso (= atraso planeado)
             E.FCH_ESTIMA_CONO_UNO                                                 AS ESTIMA_ROD,
             TRUNC(E.FCH_ENTREGA_CONO_UNO)                                        AS ENTREG_ROD,
             CASE WHEN E.FCH_ENTREGA_CONO_UNO IS NULL
                    OR E.FCH_ESTIMA_CONO_UNO  IS NULL THEN 0
                  ELSE TRUNC(E.FCH_ESTIMA_CONO_UNO)
                       - TRUNC(E.FCH_ENTREGA_CONO_UNO)
             END                                                                   AS DIAS_ROD,
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
             DECODE(E.ACAB_MAD, 'S', 'REDINA', 'CONERA')                         AS TIPO_ACABADO,
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
             E.CANTIDAD                                                            AS KG_PROG,
             U.CANTIDAD                                                            AS KG_DESPA,
             -- GAP: NULL si sin despacho (Excel retorna ""); KG_DESPA-KG_PROG en otro caso
             CASE WHEN U.CANTIDAD IS NULL THEN NULL
                  ELSE U.CANTIDAD - E.CANTIDAD
             END                                                                   AS GAP,
             -- PCT_TOLERAN: porcentaje ±% respecto a KG_PROG; NULL=sin despachar
             -- Nota: Excel retorna ratio crudo (0.05=5%); aquí x100 para claridad web (5.00)
             CASE WHEN U.CANTIDAD IS NULL OR E.CANTIDAD = 0 THEN NULL
                  ELSE ROUND((U.CANTIDAD / E.CANTIDAD - 1) * 100, 2)
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
             -- 47-48: columnas de apoyo (solo para fórmulas de otras hojas en Excel)
             NULL                                                                  AS AREA_RESPONSABLE,
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
             TO_NUMBER(E.FCH_ENTREGA - NVL(S.FECHA_ING, TRUNC(SYSDATE)))         AS DIAS_RETRASO
        FROM (
               -- Dedup: por cada (NUM_PED,NRO,NUM_DET) conserva sólo el FHC_PROG más reciente
               SELECT NUM_PED, NRO, NUM_DET,
                      MAX(NVL(FHC_PROG, TO_DATE('31/12/2050','DD/MM/YYYY'))) AS FCH_PROG
               FROM   ITEMPED_DET
               WHERE  (p_opc <> 'POR PEDIDO' OR NUM_PED = p_numped)  -- OPT: filtro temprano para modo POR PEDIDO
               GROUP  BY NUM_PED, NRO, NUM_DET
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
             H_TPROD           V
             -- OPT-1 (sesión anterior): TT_PARAMPROGTIN EE eliminado — ningún campo de EE
             --        aparecía en el SELECT; generaba un producto cartesiano innecesario
       WHERE NVL(E.ESTADO_PART,'0') NOT IN ('8','9')
         -- Dedup FHC_PROG
         AND E.NUM_PED  = F.NUM_PED
         AND E.NRO      = F.NRO
         AND E.NUM_DET  = F.NUM_DET
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
       ORDER BY E.FCH_ENTREGA,
                E.NUM_PED || '-' || E.NRO || '-' || E.NUM_DET || '-' || E.REPROCESO;
  END SP_PLN_SEG_PROG_TINTORERIA;


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
-- §7  TRIGGERS
--     Todos llaman PKG_PLN.*
--     Todos tienen EXCEPTION WHEN OTHERS THEN NULL
--     para no bloquear operaciones de planta.
-- ============================================================

-- ────────────────────────────────────────────────────────────
-- §7.1  TIA_PLN_FROM_ITEMPED — PASO '01' Pedido Registrado
-- ────────────────────────────────────────────────────────────
-- Disparo  : AFTER INSERT ON ITEMPED FOR EACH ROW
-- Acción   : Crea la fila inicial en PLN_SEGUIMIENTO + calcula fechas
-- Tabla     : ITEMPED — ítem de pedido (producto + cantidad + cliente)
-- Paso destino: '01' (pedido confirmado sin stock)
-- REGLAS DE FILTRO (solo registrar si se cumplen TODAS):
--   1. NVL(SOLO_DESPACHO,'N') <> 'S'  → ítems de stock/maquila no entran a producción
--   2. PEDIDO.ESTADO NOT IN ('0','9')  → excluir borradores (caen) y anulados
--   3. PEDIDO.F_APROBACION IS NOT NULL → solo pedidos aprobados/confirmados
--   4. ITEMPED.ESTADO NOT IN ('0','9') → excluir ítems borrador o anulados
--   5. TRUNC(PEDIDO.FECHA,'MM') >= TRUNC(SYSDATE,'MM') → solo mes actual en adelante
-- FIX #27: :NEW.solo_despacho se usa directamente (evita ORA-04091 mutating
--          table en AFTER INSERT FOR EACH ROW — especialmente en inserciones
--          bulk). DEFAULT 'N' cubierto por NVL.
-- Nota: Llama también a SP_PLN_CALCULA_FECHAS(motivo='PED') para tener
--       una primera estimación inmediatamente al registrar el pedido.
-- Si ITEMPED ya tiene un row en PLN_SEGUIMIENTO (re-inserción), el
--   DUP_VAL_ON_INDEX dentro de SP_PLN_INIT_SEGUIMIENTO absorbe el error.
-- EXCEPTION WHEN OTHERS THEN NULL → nunca bloquea el INSERT de ITEMPED.
CREATE OR REPLACE TRIGGER TIA_PLN_FROM_ITEMPED
AFTER INSERT ON ITEMPED
FOR EACH ROW
DECLARE
  v_est_ped   PEDIDO.ESTADO%TYPE;
  v_fch_aprob PEDIDO.F_APROBACION%TYPE;
  v_fch_ped   PEDIDO.FECHA%TYPE;
  -- Copias de :NEW: se asignan al inicio del BEGIN porque en Oracle 11g
  -- :NEW no se puede usar en expresiones de inicialización del DECLARE.
  v_serie     PLN_SEGUIMIENTO.SERIE%TYPE;
  v_num_ped   PLN_SEGUIMIENTO.NUM_PED%TYPE;
  v_nro       PLN_SEGUIMIENTO.NRO%TYPE;
BEGIN
  -- Capturar valores de :NEW antes de cualquier RETURN/EXCEPTION
  v_serie   := :NEW.serie;
  v_num_ped := :NEW.num_ped;
  v_nro     := :NEW.nro;

  -- FILTRO 1: ítems de solo-stock/maquila no entran a producción → no registrar
  IF NVL(:NEW.solo_despacho, 'N') = 'S' THEN RETURN; END IF;

  -- FILTRO 2: ítem borrador o anulado → no registrar
  IF :NEW.estado IN ('0', '9') THEN RETURN; END IF;

  -- FILTRO 3+5: solo pedidos confirmados con aprobación Y del mes actual en adelante
  -- PEDIDO es tabla diferente → no hay riesgo ORA-04091
  BEGIN
    SELECT estado, f_aprobacion, fecha
    INTO   v_est_ped, v_fch_aprob, v_fch_ped
    FROM   pedido
    WHERE  serie = :NEW.serie AND num_ped = :NEW.num_ped;
  EXCEPTION WHEN NO_DATA_FOUND THEN RETURN;
  END;
  IF v_est_ped IN ('0', '9') OR v_fch_aprob IS NULL THEN RETURN; END IF;

  -- FILTRO 5 eliminado (BUG-D FIX): el filtro de mes anterior bloqueaba la creación de
  -- seguimiento para nuevos ítems añadidos a pedidos activos de meses previos.
  -- SP_PLN_INIT_SEGUIMIENTO ya valida internamente ESTADO IN ('6','9') para cerrados/anulados.
  -- Impacto BD confirmado: 1,071 ítems activos sin seguimiento (261 de abril/2026, 237 de marzo/2026).

  PKG_PLN.SP_PLN_INIT_SEGUIMIENTO(:NEW.serie, :NEW.num_ped, :NEW.nro, 0, '01');
  PKG_PLN.SP_PLN_CALCULA_FECHAS(:NEW.serie, :NEW.num_ped, :NEW.nro, 0, 'PED');
EXCEPTION
  WHEN OTHERS THEN
    -- No propagar: no debe romper el INSERT en ITEMPED (sistema de ventas).
    -- Usar EXECUTE IMMEDIATE para evitar restricción de compilación Oracle 11g
    -- sobre INSERT con || en VALUES dentro de bloques anidados en EXCEPTION.
    DECLARE
      v_msg VARCHAR2(400);
      v_nid NUMBER;
    BEGIN
      v_msg := SUBSTR('SER='||v_serie||' PED='||v_num_ped||' NRO='||v_nro||' | '||SQLERRM, 1, 400);
      SELECT pln_seq_alerta.NEXTVAL INTO v_nid FROM DUAL;
      EXECUTE IMMEDIATE
        'INSERT INTO pln_alerta(id_alerta,tip_alerta,nivel,titulo,detalle,'
        ||'serie,num_ped,nro,fch_alerta,estado,a_aduser,a_adfecha)'
        ||' VALUES(:1,:2,:3,:4,:5,:6,:7,:8,SYSDATE,:9,:10,SYSDATE)'
        USING v_nid,'EINIT','A','Error init seguimiento (ITEMPED)',
              v_msg, v_serie, v_num_ped, v_nro, 'A', 'TRIGGER';
    EXCEPTION WHEN OTHERS THEN NULL;
    END;
END TIA_PLN_FROM_ITEMPED;
/

-- ────────────────────────────────────────────────────────────
-- §7.2  TUA_PLN_FROM_ITEMPED_DET — PASO '02' Planificado
-- ────────────────────────────────────────────────────────────
-- Disparo  : AFTER UPDATE ON ITEMPED_DET FOR EACH ROW
-- Condición: NEW.NROPROG IS NOT NULL AND (NROPROG cambió OR FHC_PROG cambió
--            OR FHC_ENTREGA cambió OR FCH_REG_ENTREGA cambió OR FCH_ENTREGA_ORI cambió)
-- Acción   : Crea sub-lote en PLN_SEGUIMIENTO (si num_det > 0)
--             + avanza a PASO '02' + recalcula fechas (motivo='PLA')
--             + actualiza FCH_ENTREGA_COMP, FCH_REG_ENTREGA, FCH_ENTREGA_ORI (v2.3)
--             + actualiza IND_URGENTE='S' si URGENTE='S' o hay ANTICIPO cobrado (BUG #36)
-- Tabla     : ITEMPED_DET — sub-lote del ítem (NROPROG = PARTIDA.NROPROG; H_PROGRAMACION es obsoleta)
-- Campos clave:
--   :NEW.NROPROG        → número de programa asignado
--   :NEW.NUM_DET        → sub-lote
--   :NEW.CANTIDAD       → kg del sub-lote
--   :NEW.FHC_PROG       → fecha de planificación (inicio producción)
--   :NEW.FHC_ENTREGA    → fecha FINAL de compromiso — LA QUE MANDA (v2.3)
--   :NEW.FCH_ENTREGA_ORI → fecha ORIGINAL de compromiso del artículo (v2.3)
--   :NEW.FCH_REG_ENTREGA → fecha de registro del ítem — uso interno (v2.3)
-- La condición WHEN evita disparos innecesarios en updates que no
--   cambian campos relevantes (ej. cambios de precio, etc.).
-- EXCEPTION WHEN OTHERS THEN NULL → nunca bloquea el UPDATE de ITEMPED_DET.
CREATE OR REPLACE TRIGGER TUA_PLN_FROM_ITEMPED_DET
AFTER UPDATE ON ITEMPED_DET
FOR EACH ROW
WHEN (NEW.NROPROG IS NOT NULL
      AND (OLD.NROPROG IS NULL
           OR NEW.NROPROG         != OLD.NROPROG
           OR NVL(NEW.FHC_PROG,        DATE '1900-01-01') != NVL(OLD.FHC_PROG,        DATE '1900-01-01')  -- v2.4 FIX: NVL para detectar NULL→fecha
           OR NVL(NEW.FHC_ENTREGA,    DATE '1900-01-01') != NVL(OLD.FHC_ENTREGA,    DATE '1900-01-01')
           OR NVL(NEW.FCH_REG_ENTREGA,DATE '1900-01-01') != NVL(OLD.FCH_REG_ENTREGA,DATE '1900-01-01')
           OR NVL(NEW.FCH_ENTREGA_ORI,DATE '1900-01-01') != NVL(OLD.FCH_ENTREGA_ORI,DATE '1900-01-01')))
DECLARE
  v_urgente     VARCHAR2(1) := 'N';
  v_fch_entrega DATE;
BEGIN
  PKG_PLN.SP_PLN_INIT_SEGUIMIENTO(:NEW.serie, :NEW.num_ped, :NEW.nro, :NEW.num_det);

  -- Solo avanzar a PASO '02' y recalcular si el NROPROG cambió o FHC_PROG cambió
  IF :OLD.NROPROG IS NULL
     OR :NEW.NROPROG != :OLD.NROPROG
     OR NVL(:NEW.FHC_PROG, DATE '1900-01-01') != NVL(:OLD.FHC_PROG, DATE '1900-01-01') THEN  -- v2.4 FIX: NVL

    PKG_PLN.SP_PLN_AVANZA_PASO(
      :NEW.serie, :NEW.num_ped, :NEW.nro, :NEW.num_det,
      '02', 'ITEMPED_DET', :NEW.nroprog, :NEW.cantidad,
      'Programa asignado: '||:NEW.nroprog
    );
    PKG_PLN.SP_PLN_CALCULA_FECHAS(:NEW.serie, :NEW.num_ped, :NEW.nro, :NEW.num_det, 'PLA');
  END IF;

  -- Calcular FCH_ENTREGA_COMP nueva según prioridad (v2.3)
  -- Prioridad: FHC_ENTREGA (final, la que manda) > FCH_ENTREGA_ORI (original) > FCH_REG_ENTREGA (registro) > F_MAXPED > fallback plazo
  BEGIN
    SELECT NVL(:NEW.FHC_ENTREGA,
           NVL(:NEW.FCH_ENTREGA_ORI,
           NVL(:NEW.FCH_REG_ENTREGA,
           NVL(ip.f_maxped,
               pe.fecha + NVL(pe.plazo_entrega, 30)))))
    INTO   v_fch_entrega
    FROM   ITEMPED ip
    JOIN   PEDIDO  pe ON pe.serie=ip.serie AND pe.num_ped=ip.num_ped
    WHERE  ip.serie=:NEW.serie AND ip.num_ped=:NEW.num_ped AND ip.nro=:NEW.nro;
  EXCEPTION WHEN NO_DATA_FOUND THEN
    v_fch_entrega := NVL(:NEW.FHC_ENTREGA, NVL(:NEW.FCH_ENTREGA_ORI, :NEW.FCH_REG_ENTREGA));
  END;

  -- Guardar FCH_PLANIF, USR_PLANIF + fechas compromiso por artículo (v2.3)
  UPDATE PLN_SEGUIMIENTO SET
    FCH_PLANIF       = NVL(:NEW.FHC_PROG,        FCH_PLANIF),
    USR_PLANIF       = NVL(:NEW.A_ADUSER,         USR_PLANIF),
    FCH_REG_ENTREGA  = NVL(:NEW.FCH_REG_ENTREGA,  FCH_REG_ENTREGA),
    FCH_ENTREGA_ORI  = NVL(:NEW.FCH_ENTREGA_ORI,  FCH_ENTREGA_ORI),
    FCH_ENTREGA_COMP = NVL(v_fch_entrega,         FCH_ENTREGA_COMP),
    A_MDFECHA        = SYSDATE,
    A_MDUSER         = USER
  WHERE serie=:NEW.serie AND num_ped=:NEW.num_ped
    AND nro=:NEW.nro AND num_det=:NEW.num_det AND estado='A';

  -- BUG #36: IND_URGENTE='S' si ITEMPED_DET.URGENTE='S' o hay anticipo cobrado para el pedido
  IF NVL(:NEW.urgente,'N') = 'S' THEN
    v_urgente := 'S';
  ELSE
    BEGIN
      SELECT 'S' INTO v_urgente
      FROM ANTICIPO
      WHERE num_ped=:NEW.num_ped AND serie=:NEW.serie AND ROWNUM=1;
    EXCEPTION WHEN NO_DATA_FOUND THEN v_urgente := 'N';
    END;
  END IF;

  IF v_urgente = 'S' THEN
    UPDATE PLN_SEGUIMIENTO SET IND_URGENTE='S', A_MDFECHA=SYSDATE, A_MDUSER=USER
    WHERE serie=:NEW.serie AND num_ped=:NEW.num_ped
      AND nro=:NEW.nro AND num_det=:NEW.num_det AND estado='A';
  END IF;

  -- Guardar máquina planificada (v2.0: COD_MAQ_PLANIF para cálculo de kgr_hr en SP_PLN_CALCULA_FECHAS)
  IF :NEW.maquina IS NOT NULL THEN
    UPDATE PLN_SEGUIMIENTO SET
      COD_MAQ_PLANIF = :NEW.maquina, A_MDFECHA=SYSDATE, A_MDUSER=USER
    WHERE serie=:NEW.serie AND num_ped=:NEW.num_ped
      AND nro=:NEW.nro AND num_det=:NEW.num_det AND estado='A';
  END IF;
EXCEPTION
  WHEN OTHERS THEN NULL;
END TUA_PLN_FROM_ITEMPED_DET;
/

-- ────────────────────────────────────────────────────────────
-- §7.2B TIA_PLN_FROM_ITEMPED_DET — PASO '02' en INSERT (BUG #45 fix)
-- ────────────────────────────────────────────────────────────
-- Disparo  : AFTER INSERT ON ITEMPED_DET FOR EACH ROW
-- Condición: NEW.NROPROG IS NOT NULL
-- Propósito: Captura INSERTs donde NROPROG y FHC_PROG ya están seteados
--            en el mismo statement (el TUA no dispara en INSERT).
--            Previene que ítems queden en PASO '01' cuando el planificador
--            inserta una fila nueva en ITEMPED_DET con el programa ya asignado.
-- Hereda misma lógica que TUA_PLN_FROM_ITEMPED_DET (v2.3).
CREATE OR REPLACE TRIGGER TIA_PLN_FROM_ITEMPED_DET
AFTER INSERT ON ITEMPED_DET
FOR EACH ROW
WHEN (NEW.NROPROG IS NOT NULL)
DECLARE
  v_urgente     VARCHAR2(1) := 'N';
  v_fch_entrega DATE;
BEGIN
  PKG_PLN.SP_PLN_INIT_SEGUIMIENTO(:NEW.serie, :NEW.num_ped, :NEW.nro, :NEW.num_det);

  -- Avanzar a PASO '02' si FHC_PROG ya está seteada en el INSERT
  IF :NEW.FHC_PROG IS NOT NULL THEN
    PKG_PLN.SP_PLN_AVANZA_PASO(
      :NEW.serie, :NEW.num_ped, :NEW.nro, :NEW.num_det,
      '02', 'ITEMPED_DET', :NEW.nroprog, :NEW.cantidad,
      'Programa asignado (INSERT): '||:NEW.nroprog
    );
    PKG_PLN.SP_PLN_CALCULA_FECHAS(:NEW.serie, :NEW.num_ped, :NEW.nro, :NEW.num_det, 'PLA');
  END IF;

  -- Calcular FCH_ENTREGA_COMP según prioridad (igual que TUA)
  BEGIN
    SELECT NVL(:NEW.FHC_ENTREGA,
           NVL(:NEW.FCH_ENTREGA_ORI,
           NVL(:NEW.FCH_REG_ENTREGA,
           NVL(ip.f_maxped,
               pe.fecha + NVL(pe.plazo_entrega, 30)))))
    INTO   v_fch_entrega
    FROM   ITEMPED ip
    JOIN   PEDIDO  pe ON pe.serie=ip.serie AND pe.num_ped=ip.num_ped
    WHERE  ip.serie=:NEW.serie AND ip.num_ped=:NEW.num_ped AND ip.nro=:NEW.nro;
  EXCEPTION WHEN NO_DATA_FOUND THEN
    v_fch_entrega := NVL(:NEW.FHC_ENTREGA, NVL(:NEW.FCH_ENTREGA_ORI, :NEW.FCH_REG_ENTREGA));
  END;

  UPDATE PLN_SEGUIMIENTO SET
    FCH_PLANIF       = NVL(:NEW.FHC_PROG,        FCH_PLANIF),
    USR_PLANIF       = NVL(:NEW.A_ADUSER,         USR_PLANIF),
    FCH_REG_ENTREGA  = NVL(:NEW.FCH_REG_ENTREGA,  FCH_REG_ENTREGA),
    FCH_ENTREGA_ORI  = NVL(:NEW.FCH_ENTREGA_ORI,  FCH_ENTREGA_ORI),
    FCH_ENTREGA_COMP = NVL(v_fch_entrega,         FCH_ENTREGA_COMP),
    A_MDFECHA        = SYSDATE,
    A_MDUSER         = USER
  WHERE serie=:NEW.serie AND num_ped=:NEW.num_ped
    AND nro=:NEW.nro AND num_det=:NEW.num_det AND estado='A';

  -- IND_URGENTE si URGENTE='S' o hay anticipo cobrado
  IF NVL(:NEW.urgente,'N') = 'S' THEN
    v_urgente := 'S';
  ELSE
    BEGIN
      SELECT 'S' INTO v_urgente
      FROM ANTICIPO
      WHERE num_ped=:NEW.num_ped AND serie=:NEW.serie AND ROWNUM=1;
    EXCEPTION WHEN NO_DATA_FOUND THEN v_urgente := 'N';
    END;
  END IF;

  IF v_urgente = 'S' THEN
    UPDATE PLN_SEGUIMIENTO SET IND_URGENTE='S', A_MDFECHA=SYSDATE, A_MDUSER=USER
    WHERE serie=:NEW.serie AND num_ped=:NEW.num_ped
      AND nro=:NEW.nro AND num_det=:NEW.num_det AND estado='A';
  END IF;

  IF :NEW.maquina IS NOT NULL THEN
    UPDATE PLN_SEGUIMIENTO SET
      COD_MAQ_PLANIF = :NEW.maquina, A_MDFECHA=SYSDATE, A_MDUSER=USER
    WHERE serie=:NEW.serie AND num_ped=:NEW.num_ped
      AND nro=:NEW.nro AND num_det=:NEW.num_det AND estado='A';
  END IF;
EXCEPTION
  WHEN OTHERS THEN NULL;
END TIA_PLN_FROM_ITEMPED_DET;
/

-- ────────────────────────────────────────────────────────────
-- §7.3  TIA_PLN_FROM_H_RPRODUC — PASO '05'/'09B'/'10' (v2.0)
-- ────────────────────────────────────────────────────────────
-- Disparo  : AFTER INSERT ON H_RPRODUC FOR EACH ROW WHEN (NEW.GUIA IS NOT NULL)
-- HALLAZGO CRITICO v2.0 (21/05/2026):
--   H_RPRODUC (TP_MAQ != 'G') es SIEMPRE POST-TT (DEVANADO).
--   Confirmado: ZERO registros con H_RPRODUC.FCH_HPROD < TT_RSECADO.FECHA_INI
--   en toda la data 2025-2026 (consulta ejecutada en BD real).
--   SITU_PART='R001' tampoco ocurre en 2026 — el sistema nuevo usa TT_RPRODUC TIPODOC='PA'.
--
-- LOGICA DEL TRIGGER (basada en PASO_ACT actual del item):
--   TP_MAQ='G' (GASEADORA):
--     -> PASO '09B' (Gaseado), solo cuando PROCESO='24'
--     -> Guarda COD_MAQ en COD_MAQ_GAS + FCH_REAL_GASEADO
--   TP_MAQ != 'G' con PASO_ACT IN ('08','09','09B','9R'):
--     -> PASO '10' (Devanado post-CC, sistema nuevo 2026)
--     -> Guarda COD_MAQ en COD_MAQ_DEVAN, TP_MAQ en TP_MAQ_DEVAN
--   TP_MAQ != 'G' con PASO_ACT = '03' (u otro estado pre-TT):
--     -> PASO '05' (Lote Disponible, sistema legado pre-2026)
--     -> En 2026 este caso ya no ocurre, pero se mantiene para compatibilidad
--
-- Navegación (GUIA -> NROPROG -> item):
--   H_RPRODUC.GUIA -> PARTIDA.NUMERO
--   PARTIDA.NROPROG -> ITEMPED_DET.(NRO, NUM_DET)
--   PARTIDA.SERIE + PARTIDA.NRO_PEDIDO -> identifica el pedido
--
-- EXCEPTION WHEN OTHERS THEN NULL -> no bloquea el INSERT de H_RPRODUC.
CREATE OR REPLACE TRIGGER TIA_PLN_FROM_H_RPRODUC
AFTER INSERT ON H_RPRODUC
FOR EACH ROW
WHEN (NEW.GUIA IS NOT NULL)
DECLARE
  v_nroprog    NUMBER;
  v_serie      NUMBER;
  v_num_ped    NUMBER;
  v_nro        NUMBER;
  v_num_det    NUMBER;
  v_paso_act   VARCHAR2(3);
  v_nuevo_paso VARCHAR2(4);
BEGIN
  SELECT p.nroprog, p.serie, p.nro_pedido
  INTO v_nroprog, v_serie, v_num_ped
  FROM partida p
  WHERE p.numero = :NEW.guia;

  SELECT d.nro, d.num_det INTO v_nro, v_num_det
  FROM itemped_det d
  WHERE d.nroprog = v_nroprog AND ROWNUM = 1;

  -- Leer el paso actual para determinar el contexto
  SELECT cod_paso_act INTO v_paso_act
  FROM pln_seguimiento
  WHERE serie=v_serie AND num_ped=v_num_ped AND nro=v_nro AND num_det=v_num_det
    AND estado='A';

  -- v2.1 FIX BUG#B: verificar PASO_ACT primero para todos los casos.
  -- Sin este fix, TP_MAQ='G' en PASO pre-TT (ej: '03') avanzaría a '09B'
  -- saltando todo el flujo TT. La documentación (header §7.3) siempre exige
  -- que PASO_ACT IN ('08','09','09B','9R') sea condición necesaria para '09B'.
  IF v_paso_act IN ('08','09','09B','9R') THEN
    IF :NEW.tp_maq = 'G' THEN
      -- Gaseadora post-CC (SP_PLN_AVANZA_PASO valida PROCESO='24')
      v_nuevo_paso := '09B';
    ELSE
      -- H_RPRODUC post-CC = DEVANADO (sistema nuevo 2026)
      v_nuevo_paso := '10';
    END IF;
  ELSE
    -- Sistema legado: H_RPRODUC pre-TT = Lote Disponible
    -- (incluye caso inusual TP_MAQ='G' en estado pre-TT)
    v_nuevo_paso := '05';
  END IF;

  PKG_PLN.SP_PLN_AVANZA_PASO(
    v_serie, v_num_ped, v_nro, v_num_det,
    v_nuevo_paso, 'H_RPRODUC', :NEW.guia, :NEW.peso_neto,
    'Maq:'||:NEW.cod_maq||' Tipo:'||:NEW.tp_maq,
    :NEW.fecha_ini  -- p_fch_evento: fecha real del inicio de operacion en maquina
  );

  -- Actualizar campo de maquina segun el tipo de operacion
  IF :NEW.tp_maq = 'G' THEN
    UPDATE pln_seguimiento SET
      COD_MAQ_GAS      = :NEW.cod_maq,
      A_MDFECHA = SYSDATE, A_MDUSER = USER
    WHERE serie=v_serie AND num_ped=v_num_ped AND nro=v_nro AND num_det=v_num_det AND estado='A';
  ELSIF v_nuevo_paso = '10' THEN
    UPDATE pln_seguimiento SET
      COD_MAQ_DEVAN  = :NEW.cod_maq,
      TP_MAQ_DEVAN   = :NEW.tp_maq,
      A_MDFECHA = SYSDATE, A_MDUSER = USER
    WHERE serie=v_serie AND num_ped=v_num_ped AND nro=v_nro AND num_det=v_num_det AND estado='A';
  END IF;
EXCEPTION
  WHEN NO_DATA_FOUND THEN NULL;
  WHEN OTHERS        THEN NULL;
END TIA_PLN_FROM_H_RPRODUC;
/

-- ────────────────────────────────────────────────────────────
-- §7.4  TIA_PLN_FROM_PARTIDA — PASO '03' En Hilanderia
-- ────────────────────────────────────────────────────────────
-- Disparo  : AFTER INSERT ON PARTIDA FOR EACH ROW WHEN (NEW.NROPROG IS NOT NULL)
-- Acción   : Avanza a PASO '03' indicando que el lote fue asignado a producción
-- Tabla     : PARTIDA — representa un lote físico de hilo (unidad de tintorería)
-- Navegación simplificada (corrección aplicada):
--   :NEW.SERIE + :NEW.NRO_PEDIDO → ya identifican pedido y serie directamente
--   :NEW.NROPROG → ITEMPED_DET → obtiene NRO y NUM_DET
-- NOTA CRÍTICA (corrección vs. Propuesta.md):
--   PARTIDA no tiene columna NUM_DET propia. El campo :NEW.num_det NO existe.
--   NRO y NUM_DET se derivan SIEMPRE vía ITEMPED_DET WHERE nroprog = :NEW.nroprog.
-- KG_PRODUCIDOS: se suma :NEW.PESO_NETO en SP_PLN_AVANZA_PASO
--   (es el único paso donde KG_PRODUCIDOS se acumula).
-- v3.0: PARTIDA se crea ANTES que L_VALIDA_RECETA apruebe la receta.
--   PASO '03' (En Hilanderia, ORDEN 3) -> PASO '04' (Laboratorio, ORDEN 4) -> PASO '05'.
-- EXCEPTION WHEN OTHERS THEN NULL → no bloquea el INSERT de PARTIDA.
CREATE OR REPLACE TRIGGER TIA_PLN_FROM_PARTIDA
AFTER INSERT ON PARTIDA
FOR EACH ROW
WHEN (NEW.NROPROG IS NOT NULL)
DECLARE
  v_nro     NUMBER;
  v_num_det NUMBER;
BEGIN
  SELECT d.nro, d.num_det INTO v_nro, v_num_det
  FROM itemped_det d
  WHERE d.nroprog = :NEW.nroprog AND ROWNUM = 1;

  PKG_PLN.SP_PLN_AVANZA_PASO(
    :NEW.serie, :NEW.nro_pedido, v_nro, v_num_det,
    '03', 'PARTIDA', :NEW.numero, :NEW.peso_neto,
    'Lote disponible - NROPROG:'||:NEW.nroprog
  );
EXCEPTION
  WHEN NO_DATA_FOUND THEN NULL;
  WHEN OTHERS        THEN NULL;
END TIA_PLN_FROM_PARTIDA;
/

-- ────────────────────────────────────────────────────────────
-- §7.5  TUA_PLN_FROM_L_VALIDA_RECETA — PASO '04' Laboratorio
-- ────────────────────────────────────────────────────────────
-- Disparo  : AFTER UPDATE OF ESTADO ON L_VALIDA_RECETA FOR EACH ROW
-- Condición: NEW.ESTADO IN ('3','4') AND (OLD.ESTADO IS NULL OR OLD.ESTADO NOT IN ('3','4'))
-- Acción   : Avanza a PASO '04' (receta de tintorería validada por lab.)
-- Tabla     : L_VALIDA_RECETA — validación de receta por laboratorio
-- Navegación:
--   L_VALIDA_RECETA.NROPROG → ITEMPED_DET.(SERIE, NUM_PED, NRO, NUM_DET)
-- ESTADO='3' = receta aprobada (vía proceso lab normal, F_ESTADO_TRES poblada).
-- ESTADO='4' = aprobado directo (bypass lab, F_ESTADO_TRES=NULL). Ambos avanzan a PASO '04'.
-- Si NROPROG IS NULL → RETURN sin avanzar (receta sin ítem de pedido asociado).
-- IMPORTANTE: después del PASO '04', el ítem espera ingresar a TT.
--   Si FCH_EST_TIN_INI ya pasó, SP_PLN_GENERA_ALERTAS generará alerta 'STN'.
-- EXCEPTION WHEN OTHERS THEN NULL → no bloquea el UPDATE de L_VALIDA_RECETA.
CREATE OR REPLACE TRIGGER TUA_PLN_FROM_L_VALIDA_RECETA
AFTER UPDATE OF ESTADO ON L_VALIDA_RECETA
FOR EACH ROW
WHEN (NEW.ESTADO IN ('3','4') AND (OLD.ESTADO IS NULL OR OLD.ESTADO NOT IN ('3','4')))
DECLARE
  v_serie   NUMBER;
  v_num_ped NUMBER;
  v_nro     NUMBER;
  v_num_det NUMBER;
BEGIN
  IF :NEW.nroprog IS NULL THEN RETURN; END IF;

  SELECT d.serie, d.num_ped, d.nro, d.num_det
  INTO v_serie, v_num_ped, v_nro, v_num_det
  FROM itemped_det d
  WHERE d.nroprog = :NEW.nroprog AND ROWNUM = 1;

  PKG_PLN.SP_PLN_AVANZA_PASO(
    v_serie, v_num_ped, v_nro, v_num_det,
    '04', 'L_VALIDA_RECETA', :NEW.numero, NULL,
    'Receta validada - Lab:'||NVL(:NEW.c_laboratorista,'N/A')
  );
EXCEPTION
  WHEN NO_DATA_FOUND THEN NULL;
  WHEN OTHERS        THEN NULL;
END TUA_PLN_FROM_L_VALIDA_RECETA;
/

-- ────────────────────────────────────────────────────────────
-- §7.5B TIA_PLN_FROM_L_VALIDA_RECETA — PASO '04' Laboratorio (INSERT directo / bypass)
-- ────────────────────────────────────────────────────────────
-- Disparo  : AFTER INSERT ON L_VALIDA_RECETA FOR EACH ROW
-- Condición: NEW.ESTADO IN ('3','4') AND NEW.NROPROG IS NOT NULL
-- Propósito : Complemento de TUA_PLN_FROM_L_VALIDA_RECETA (§7.5).
--   El TUA solo captura UPDATE. Si el sistema de lab inserta L_VALIDA_RECETA
--   directamente con ESTADO='3' (aprobado) o ESTADO='4' (bypass), el TUA
--   no dispara y PASO '04' nunca se activa.
--   Casos reales donde ocurre:
--     · ESTADO='4' (aprobado directo/bypass): la app puede insertar con
--       estado final sin pasar por un UPDATE posterior.
--     · Correcciones manuales o cargas masivas con estado final ya seteado.
-- Guard anti-retroceso en SP_PLN_AVANZA_PASO evita doble avance si tanto
--   TIA como TUA disparan para el mismo ítem (idempotente).
-- EXCEPTION WHEN OTHERS THEN NULL → no bloquea el INSERT de L_VALIDA_RECETA.
CREATE OR REPLACE TRIGGER TIA_PLN_FROM_L_VALIDA_RECETA
AFTER INSERT ON L_VALIDA_RECETA
FOR EACH ROW
WHEN (NEW.ESTADO IN ('3','4') AND NEW.NROPROG IS NOT NULL)
DECLARE
  v_serie   NUMBER;
  v_num_ped NUMBER;
  v_nro     NUMBER;
  v_num_det NUMBER;
BEGIN
  SELECT d.serie, d.num_ped, d.nro, d.num_det
  INTO v_serie, v_num_ped, v_nro, v_num_det
  FROM itemped_det d
  WHERE d.nroprog = :NEW.nroprog AND ROWNUM = 1;

  PKG_PLN.SP_PLN_AVANZA_PASO(
    v_serie, v_num_ped, v_nro, v_num_det,
    '04', 'L_VALIDA_RECETA', :NEW.numero, NULL,
    'Receta aprobada (INSERT directo) - Lab:'||NVL(:NEW.c_laboratorista,'N/A')
  );
EXCEPTION
  WHEN NO_DATA_FOUND THEN NULL;
  WHEN OTHERS        THEN NULL;
END TIA_PLN_FROM_L_VALIDA_RECETA;
/

-- ────────────────────────────────────────────────────────────
-- §7.6  TUA_PLN_FROM_PARTIDA — PASO '06' En Tintorería (sistema legado)
-- ────────────────────────────────────────────────────────────
-- Disparo  : AFTER UPDATE ON PARTIDA FOR EACH ROW
-- Condición: NEW.SITU_PART='R001' AND OLD.SITU_PART <> 'R001' AND NEW.NROPROG IS NOT NULL
-- Acción   : Avanza a PASO '06' (ingreso físico a tintorería — sistema legado)
-- NOTA v2.0 (21/05/2026): SITU_PART='R001' NUNCA ocurre en 2026.
--   Verificado en BD: 0 registros con situ_part='R001' en todo 2026.
--   El sistema nuevo de TT usa TT_RPRODUC INSERT TIPODOC='PA' (ver §7.7).
--   Este trigger se mantiene solo para compatibilidad con partidas históricas < ~155000.
-- KG_EN_TIN: se suma :NEW.PESO_NETO en SP_PLN_AVANZA_PASO.
-- EXCEPTION WHEN OTHERS THEN NULL → no bloquea el UPDATE de PARTIDA.
CREATE OR REPLACE TRIGGER TUA_PLN_FROM_PARTIDA
AFTER UPDATE ON PARTIDA
FOR EACH ROW
WHEN (NEW.SITU_PART = 'R001'
      AND (OLD.SITU_PART IS NULL OR OLD.SITU_PART <> 'R001')
      AND NEW.NROPROG IS NOT NULL)
DECLARE
  v_nro     NUMBER;
  v_num_det NUMBER;
BEGIN
  SELECT d.nro, d.num_det INTO v_nro, v_num_det
  FROM itemped_det d
  WHERE d.nroprog = :NEW.nroprog AND ROWNUM = 1;

  PKG_PLN.SP_PLN_AVANZA_PASO(
    :NEW.serie, :NEW.nro_pedido, v_nro, v_num_det,
    '06', 'PARTIDA', :NEW.numero, :NEW.peso_neto,
    'Ingreso TT (legado) - SITU_PART=R001'
  );
EXCEPTION
  WHEN NO_DATA_FOUND THEN NULL;
  WHEN OTHERS        THEN NULL;
END TUA_PLN_FROM_PARTIDA;
/

-- ────────────────────────────────────────────────────────────
-- §7.6b  TIA_PLN_FROM_TT_RPRODUC_PA — PASO '06'/'07' Sistema nuevo TT (v2.3)
-- ────────────────────────────────────────────────────────────
-- Disparo  : FOR INSERT ON TT_RPRODUC (COMPOUND TRIGGER, Oracle 11g+)
-- Condición: (TIPODOC='PA' AND ESTADO='3') OR (TIPODOC='IR')
-- Acción   : PASO '06' al detectar el PRIMER baño activo de esa partida en TT
--             PASO '07' solo cuando TODOS los baños están completos (ESTADO='3')
-- Tabla     : TT_RPRODUC — sistema de producción de tintorería (2016+)
--
-- DIFERENCIA ENTRE SISTEMAS:
--   TIPODOC='PA' (2016-2020): registro insertado directamente con ESTADO='3' (baño completo).
--     Un solo INSERT captura tanto PASO '06' como '07' en la misma sentencia.
--   TIPODOC='IR' (2021+): registro insertado con ESTADO='1' (en proceso). El UPDATE posterior
--     a '3' lo maneja TUA_PLN_FROM_TT_RPRODUC.
--     BUG ANTERIOR (v2.0-v2.2): el trigger ignoraba inserts IR con ESTADO<>'3', por lo que
--     PASO '06' nunca se activaba automáticamente para el sistema IR. Los ítems quedaban en
--     PASO '04' hasta que todos los baños completaban (UPDATE→'3'), saltando '06' directo a '07'.
--     FIX v2.3: se captura el INSERT IR con cualquier ESTADO para activar PASO '06' en cuanto
--     el primer baño INICIA (cnt_banos_any=1), independientemente de si está completo o no.
--
-- *** COMPOUND TRIGGER — FIX ORA-04091 (mutating table) ***
--   AFTER EACH ROW : solo captura los datos (no consulta TT_RPRODUC).
--   AFTER STATEMENT: aquí la tabla ya está estable → COUNT(*) seguro.
--
-- LÓGICA (v2.3):
--   PA: primer baño completo (cnt_banos=1) → PASO '06' + '07' en misma tx.
--   IR: primer baño iniciado  (cnt_banos_any=1) → PASO '06'.
--       PASO '07' solo si este INSERT fue ESTADO='3' y todos los demás también
--       (caso raro; normalmente TUA_FROM_TT_RPRODUC gestiona el avance a '07').
--   Ambos: si viene de '9R' (reproceso) → forzar PASO '06' aunque no sea el primer baño.
--
-- Navegación:
--   PA: TT_RPRODUC.RECETA = PARTIDA.NUMERO (vínculo directo)
--   IR: TT_RPRODUC.RECETA = ING_RECETAS_G.NUMERO → PARTIDA_MAS → PARTIDA.NUMERO
CREATE OR REPLACE TRIGGER TIA_PLN_FROM_TT_RPRODUC_PA
FOR INSERT ON TT_RPRODUC
COMPOUND TRIGGER

  -- ── Tipo para almacenar datos de cada fila PA/IR insertada ──────
  TYPE t_rec IS RECORD (
    receta   TT_RPRODUC.RECETA%TYPE,
    tipodoc  TT_RPRODUC.TIPODOC%TYPE,
    cod_maq  VARCHAR2(6),
    estado   TT_RPRODUC.ESTADO%TYPE   -- v2.3: necesario para diferenciar IR PASO '06'/'07'
  );
  TYPE t_tab IS TABLE OF t_rec INDEX BY PLS_INTEGER;
  g_rows t_tab;
  g_cnt  PLS_INTEGER := 0;

  -- ── AFTER EACH ROW: solo captura datos, NO consulta TT_RPRODUC ──
  -- PA: solo ESTADO='3' (siempre se inserta completo).
  -- IR: cualquier ESTADO (INSERT puede ser '1'/'2'/'3').
  AFTER EACH ROW IS
  BEGIN
    IF (:NEW.TIPODOC = 'PA' AND :NEW.ESTADO = '3')
    OR (:NEW.TIPODOC = 'IR') THEN
      g_cnt := g_cnt + 1;
      g_rows(g_cnt).receta  := :NEW.receta;
      g_rows(g_cnt).tipodoc := :NEW.tipodoc;
      g_rows(g_cnt).cod_maq := NVL(:NEW.cod_maq, '?');
      g_rows(g_cnt).estado  := :NEW.estado;
    END IF;
  EXCEPTION
    WHEN OTHERS THEN NULL;
  END AFTER EACH ROW;

  -- ── AFTER STATEMENT: tabla estable, COUNT(*) sin ORA-04091 ─────
  -- v2.5: do_partida() factorizado para iterar N partidas por receta IR (fix BUG-TRG-1)
  AFTER STATEMENT IS
    v_partida        NUMBER;
    v_nroprog        NUMBER;
    v_serie          NUMBER;
    v_num_ped        NUMBER;
    v_nro            NUMBER;
    v_num_det        NUMBER;
    v_cnt_banos      NUMBER;   -- baños COMPLETADOS (ESTADO='3')
    v_cnt_banos_any  NUMBER;   -- baños ACTIVOS cualquier ESTADO (solo IR — para PASO '06')
    v_tot_banos      NUMBER;
    v_paso_seg_act   VARCHAR2(3) := '00';  -- detecta re-ingreso por reproceso

    -- v2.5 fix BUG-TRG-1: todo el procesamiento por partida factorizado aquí.
    -- Se llama una vez por PA y N veces por receta IR (una por cada partida en PARTIDA_MAS).
    -- v_partida debe estar asignado en el scope externo antes de llamar.
    PROCEDURE do_partida(
      p_tipodoc VARCHAR2, p_cod_maq VARCHAR2,
      p_receta  NUMBER,   p_estado  VARCHAR2
    ) IS
    BEGIN
      SELECT p.nroprog, p.serie, p.nro_pedido
      INTO v_nroprog, v_serie, v_num_ped
      FROM partida p
      WHERE p.numero = v_partida;

      SELECT d.nro, d.num_det INTO v_nro, v_num_det
      FROM itemped_det d
      WHERE d.nroprog = v_nroprog AND ROWNUM = 1;

      -- ── Contar baños según TIPODOC ──────────────────────────────
      IF p_tipodoc = 'PA' THEN
        SELECT COUNT(*) INTO v_cnt_banos
        FROM   tt_rproduc
        WHERE  receta = v_partida AND tipodoc = 'PA' AND estado = '3';
        v_tot_banos     := v_cnt_banos;  -- PA: 1 registro = 1 baño completo
        v_cnt_banos_any := v_cnt_banos;  -- PA: no aplica distinción
      ELSE
        -- IR: baños COMPLETADOS (para lógica de PASO '07')
        SELECT COUNT(*) INTO v_cnt_banos
        FROM   partida_mas pm
        JOIN   tt_rproduc tt ON tt.receta = pm.numero AND tt.tipodoc = 'IR' AND tt.estado = '3'
        WHERE  pm.partida    = v_partida
          AND  pm.tp_transac = 'IR';

        -- IR: baños ACTIVOS cualquier ESTADO (para detectar 1er ingreso a TT, PASO '06')
        SELECT COUNT(*) INTO v_cnt_banos_any
        FROM   partida_mas pm
        JOIN   tt_rproduc tt ON tt.receta = pm.numero AND tt.tipodoc = 'IR'
        WHERE  pm.partida    = v_partida
          AND  pm.tp_transac = 'IR';

        SELECT COUNT(*) INTO v_tot_banos
        FROM   partida_mas pm
        WHERE  pm.partida    = v_partida
          AND  pm.tp_transac = 'IR';
      END IF;

      -- Leer paso actual para detectar re-ingreso por reproceso
      BEGIN
        SELECT cod_paso_act INTO v_paso_seg_act
        FROM   pln_seguimiento
        WHERE  serie=v_serie AND num_ped=v_num_ped AND nro=v_nro AND num_det=v_num_det AND estado='A';
      EXCEPTION
        WHEN NO_DATA_FOUND THEN v_paso_seg_act := '00';
      END;

      -- ── PASO '06': primer ingreso a TT en este ciclo ───────────
      -- PA: cnt_banos=1  (primer baño recién insertado y completo)
      -- IR: cnt_banos_any=1  (primer baño recién iniciado, cualquier estado)
      -- Ambos: también si venía de '9R' (reproceso — nuevo ciclo de TT)
      IF (p_tipodoc = 'PA' AND (v_cnt_banos     = 1 OR v_paso_seg_act = '9R'))
      OR (p_tipodoc = 'IR' AND (v_cnt_banos_any = 1 OR v_paso_seg_act = '9R'))
      THEN
        PKG_PLN.SP_PLN_AVANZA_PASO(
          v_serie, v_num_ped, v_nro, v_num_det,
          '06', 'TT_RPRODUC', v_partida, NULL,
          'Ingreso TT ('||p_tipodoc||') Maq:'||p_cod_maq||' Receta:'||p_receta
        );
        UPDATE pln_seguimiento SET
          COD_MAQ_TT = p_cod_maq,
          A_MDFECHA  = SYSDATE, A_MDUSER = USER
        WHERE serie=v_serie AND num_ped=v_num_ped AND nro=v_nro AND num_det=v_num_det AND estado='A';
      END IF;

      -- ── PASO '07': solo cuando TODOS los baños están completos ─
      -- IR con ESTADO<>'3' en este INSERT: TUA_FROM_TT_RPRODUC gestiona el '07'
      -- al recibir el UPDATE ESTADO→'3'. No avanzar '07' aquí para esos casos.
      IF v_cnt_banos >= v_tot_banos AND v_tot_banos > 0
         AND (p_tipodoc = 'PA' OR p_estado = '3')
      THEN
        PKG_PLN.SP_PLN_AVANZA_PASO(
          v_serie, v_num_ped, v_nro, v_num_det,
          '07', 'TT_RPRODUC', v_partida, NULL,
          'Tenido completo ('||p_tipodoc||') - '||v_cnt_banos||'/'||v_tot_banos||' banos'
        );
      END IF;
    EXCEPTION
      WHEN NO_DATA_FOUND THEN NULL;
      WHEN OTHERS        THEN NULL;
    END do_partida;

  BEGIN
    FOR i IN 1 .. g_cnt LOOP
      BEGIN
        IF g_rows(i).tipodoc = 'PA' THEN
          -- Sistema PA (2016): RECETA = PARTIDA.NUMERO directamente
          v_partida := g_rows(i).receta;
          do_partida(g_rows(i).tipodoc, g_rows(i).cod_maq, g_rows(i).receta, g_rows(i).estado);
        ELSE
          -- v2.5 fix BUG-TRG-1: iterar TODAS las partidas de la receta IR
          -- Antes (v2.0-v2.4): SELECT INTO con ROWNUM=1 → solo 1/N partidas procesadas
          -- para recetas con múltiples partidas (ej. receta 254139 → 22 partidas)
          FOR r_ir IN (SELECT pm.partida
                       FROM   partida_mas pm
                       WHERE  pm.tp_transac = 'IR'
                         AND  pm.numero     = g_rows(i).receta) LOOP
            v_partida := r_ir.partida;
            do_partida(g_rows(i).tipodoc, g_rows(i).cod_maq, g_rows(i).receta, g_rows(i).estado);
          END LOOP;
        END IF;
      EXCEPTION
        WHEN OTHERS THEN NULL;
      END;
    END LOOP;
  END AFTER STATEMENT;

END TIA_PLN_FROM_TT_RPRODUC_PA;
/

-- ────────────────────────────────────────────────────────────
-- §7.7  TUA_PLN_FROM_TT_RPRODUC — PASO '07' Tenido Completo (sistema LEGADO/IR)
-- ────────────────────────────────────────────────────────────
-- Disparo  : COMPOUND TRIGGER — FOR UPDATE OF ESTADO ON TT_RPRODUC
-- Condición: NEW.ESTADO='3' AND OLD.ESTADO <> '3'
-- NOTA v2.0 (21/05/2026): Este trigger es para el sistema IR (UPDATE ESTADO).
--   El sistema IR (2021+) puede usar UPDATE para marcar baños completados.
--   La navegación correcta es via PARTIDA_MAS:
--     TT_RPRODUC.RECETA = ING_RECETAS_G.NUMERO (= PARTIDA_MAS.NUMERO con TP_TRANSAC='IR')
--     PARTIDA_MAS.PARTIDA → PARTIDA.NUMERO (no usar ING_RECETAS_G.R_NUMERO que es código maestro)
-- FIX v2.2 (navegación): Reemplaza ING_RECETAS_G.R_NUMERO (código maestro, no PARTIDA.NUMERO)
--   por PARTIDA_MAS navigation: pm.tp_transac='IR' AND pm.numero=receta → pm.partida
-- REGLA CRITICA (75% de partidas con 2+ baños):
--   Solo avanza cuando TODOS los baños de la partida tienen ESTADO='3' via PARTIDA_MAS JOIN.
-- FIX v2.1 (21/05/2026): Convertido a COMPOUND TRIGGER para resolver ORA-04091.
-- EXCEPTION WHEN OTHERS THEN NULL -> no bloquea el UPDATE de TT_RPRODUC.
CREATE OR REPLACE TRIGGER TUA_PLN_FROM_TT_RPRODUC
FOR UPDATE OF ESTADO ON TT_RPRODUC
COMPOUND TRIGGER

  -- Colección de RECETAs actualizadas a ESTADO='3' en esta sentencia
  TYPE t_num_tab IS TABLE OF NUMBER INDEX BY PLS_INTEGER;
  v_recetas  t_num_tab;
  v_idx      PLS_INTEGER := 0;

  AFTER EACH ROW IS
  BEGIN
    IF :NEW.ESTADO = '3' AND (:OLD.ESTADO IS NULL OR :OLD.ESTADO <> '3') THEN
      v_idx := v_idx + 1;
      v_recetas(v_idx) := :NEW.receta;
    END IF;
  END AFTER EACH ROW;

  AFTER STATEMENT IS
    v_partida    NUMBER;
    v_nroprog    NUMBER;
    v_serie      NUMBER;
    v_num_ped    NUMBER;
    v_nro        NUMBER;
    v_num_det    NUMBER;
    v_pendientes NUMBER;
    -- FIX v2.4 (22/05/2026): una RECETA IR puede estar ligada a N partidas simultaneas
    -- (lote conjunto de tintoreria). ROWNUM=1 solo avanzaba la primera.
    -- Ahora se itera TODAS las partidas del lote via cursor.
    CURSOR c_partidas(p_receta NUMBER) IS
      SELECT pm.partida
      FROM   partida_mas pm
      WHERE  pm.tp_transac = 'IR'
        AND  pm.numero     = p_receta;
  BEGIN
    FOR i IN 1 .. v_idx LOOP
      FOR r_part IN c_partidas(v_recetas(i)) LOOP
        BEGIN
          v_partida := r_part.partida;

          -- Contar banos IR pendientes para esta partida
          SELECT COUNT(*) INTO v_pendientes
          FROM   partida_mas pm2
          JOIN   tt_rproduc r ON r.receta = pm2.numero AND r.tipodoc = 'IR'
          WHERE  pm2.partida     = v_partida
            AND  pm2.tp_transac  = 'IR'
            AND  r.estado <> '3';

          IF v_pendientes > 0 THEN CONTINUE; END IF;

          SELECT p.nroprog, p.serie, p.nro_pedido
          INTO   v_nroprog, v_serie, v_num_ped
          FROM   partida p
          WHERE  p.numero = v_partida;

          SELECT d.nro, d.num_det INTO v_nro, v_num_det
          FROM   itemped_det d
          WHERE  d.nroprog = v_nroprog AND ROWNUM = 1;

          PKG_PLN.SP_PLN_AVANZA_PASO(
            v_serie, v_num_ped, v_nro, v_num_det,
            '07', 'TT_RPRODUC', v_partida, NULL,
            'Tenido completo (IR) - Partida:'||v_partida||' Receta:'||v_recetas(i)
          );
        EXCEPTION
          WHEN NO_DATA_FOUND THEN NULL;
          WHEN OTHERS        THEN NULL;
        END;
      END LOOP;
    END LOOP;
  END AFTER STATEMENT;

END TUA_PLN_FROM_TT_RPRODUC;
/

-- ────────────────────────────────────────────────────────────
-- §7.8  TIA_PLN_FROM_TT_RSECADO — PASO '08' Secado (v2.0: guarda COD_MAQ_SECADO)
-- ────────────────────────────────────────────────────────────
-- Disparo  : AFTER INSERT ON TT_RSECADO FOR EACH ROW
-- WHEN     : NEW.GUIA IS NOT NULL  ← evita llamada inútil a SP_PLN_AVANZA_PASO
--            cuando se inserta un registro de secado sin partida vinculada
-- Acción   : Avanza a PASO '08' (secado post-tintorería registrado)
--             + guarda COD_MAQ en COD_MAQ_SECADO de PLN_SEGUIMIENTO
-- Tabla     : TT_RSECADO — registro de secado por partida y máquina de secado
-- Navegación:
--   TT_RSECADO.GUIA → PARTIDA.NUMERO (mismo patrón que H_RPRODUC en §7.3)
--   PARTIDA.NROPROG → ITEMPED_DET.(NRO, NUM_DET)
-- EXCEPTION WHEN OTHERS THEN NULL → no bloquea el INSERT de TT_RSECADO.
CREATE OR REPLACE TRIGGER TIA_PLN_FROM_TT_RSECADO
AFTER INSERT ON TT_RSECADO
FOR EACH ROW
WHEN (NEW.GUIA IS NOT NULL)
DECLARE
  v_nroprog NUMBER;
  v_serie   NUMBER;
  v_num_ped NUMBER;
  v_nro     NUMBER;
  v_num_det NUMBER;
BEGIN
  SELECT p.nroprog, p.serie, p.nro_pedido
  INTO v_nroprog, v_serie, v_num_ped
  FROM partida p
  WHERE p.numero = :NEW.guia;

  SELECT d.nro, d.num_det INTO v_nro, v_num_det
  FROM itemped_det d
  WHERE d.nroprog = v_nroprog AND ROWNUM = 1;

  PKG_PLN.SP_PLN_AVANZA_PASO(
    v_serie, v_num_ped, v_nro, v_num_det,
    '08', 'TT_RSECADO', :NEW.guia, :NEW.peso_neto,
    'Secado - Maq:'||:NEW.cod_maq
  );

  -- Guardar maquina de secado (v2.0)
  UPDATE pln_seguimiento SET
    COD_MAQ_SECADO = :NEW.cod_maq,
    A_MDFECHA      = SYSDATE, A_MDUSER = USER
  WHERE serie=v_serie AND num_ped=v_num_ped AND nro=v_nro AND num_det=v_num_det AND estado='A';
EXCEPTION
  WHEN NO_DATA_FOUND THEN NULL;
  WHEN OTHERS        THEN NULL;
END TIA_PLN_FROM_TT_RSECADO;
/

-- ────────────────────────────────────────────────────────────
-- §7.9  TUA_PLN_FROM_CTCALIDAD — PASO '09' CC Aprobado / '9R' Reproceso
-- ────────────────────────────────────────────────────────────
-- Disparo  : AFTER UPDATE OF EST_EVALUACION, RESULTADO ON CTCALIDAD_D FOR EACH ROW
-- Condición: NEW.EST_EVALUACION='32' AND (cambió EST_EVALUACION o RESULTADO)
-- FIX #28 REVERTIDO (25/05/2026): RESULTADO='30' es RECHAZADO, NO Concesionado.
--   Evidencia directa: campo legacy muestra "RECHAZADO" para RESULTADO='30';
--   57% de los 4,320 registros con RESULTADO='30' tienen L_RECTIFICA_RECETA
--   (rectificaciones solo ocurren en rechazos). DEFECTO='03'/'04'/'05' tono/matiz.
-- Valores definitivos confirmados en BD:
--   '01' = Aprobado             (126k registros)              → PASO '09'
--   '21' = Concesionado tipo A  (909  registros)              → PASO '09'
--   '29' = Concesionado tipo B  (1.9k registros)              → PASO '09'
--   '30' = RECHAZADO            (4.3k registros, 57% con L_RECTIFICA_RECETA) → PASO '9R'
--   Cualquier otro valor no nulo                              → PASO '9R'
-- Tabla     : CTCALIDAD_D — detalle de evaluación de control de calidad TT
-- NAVEGACIÓN CORREGIDA (error crítico en Propuesta.md original):
--   CTCALIDAD_D.NRO_PEDIDO  → NUM_PED (del pedido)
--   CTCALIDAD_D.SER_PARTIDA → ITEMPED_DET.NRO  (es el ÍTEM, no serie de partida)
--   CTCALIDAD_D.NROPART     → ITEMPED_DET.NUM_DET (es el SUB-LOTE)
--   SERIE se obtiene: SELECT d.serie FROM itemped_det WHERE d.num_ped+d.nro+d.num_det
-- Si v_paso IS NULL (RESULTADO nulo) → RETURN sin avanzar.
-- PASO '9R' (Reproceso): NRO_CICLO++, IND_REPROCESO='S', evento TIPO='RE'
-- PASO '09' (Aprobado): IND_REPROCESO='N' si venía de '9R'
-- EXCEPTION WHEN OTHERS THEN NULL → no bloquea el UPDATE de CTCALIDAD_D.
CREATE OR REPLACE TRIGGER TUA_PLN_FROM_CTCALIDAD
AFTER UPDATE OF EST_EVALUACION, RESULTADO ON CTCALIDAD_D
FOR EACH ROW
WHEN (NEW.EST_EVALUACION = '32'
      AND (OLD.EST_EVALUACION IS NULL OR OLD.EST_EVALUACION <> '32'
           OR NVL(OLD.RESULTADO,'__') <> NVL(NEW.RESULTADO,'__')))
DECLARE
  v_serie NUMBER;
  v_paso  VARCHAR2(3);
BEGIN
  -- RESULTADO='30'=RECHAZADO (FIX #28 revertido 25/05/2026)
  v_paso := CASE
    WHEN :NEW.resultado IN ('01','21','29') THEN '09'  -- Aprobado/Concesionado
    WHEN :NEW.resultado IS NOT NULL        THEN '9R'  -- '30'=RECHAZADO u otro → Reproceso
    ELSE NULL
  END;

  IF v_paso IS NULL THEN RETURN; END IF;

  SELECT d.serie INTO v_serie
  FROM itemped_det d
  WHERE d.num_ped  = :NEW.nro_pedido
    AND d.nro      = :NEW.ser_partida   -- SER_PARTIDA = NRO
    AND d.num_det  = :NEW.nropart       -- NROPART     = NUM_DET
    AND ROWNUM = 1;

  PKG_PLN.SP_PLN_AVANZA_PASO(
    v_serie, :NEW.nro_pedido, :NEW.ser_partida, :NEW.nropart,
    v_paso, 'CTCALIDAD_D', :NEW.numero, NULL,
    'CC resultado='||:NEW.resultado||' REPROCESO='||NVL(:NEW.reproceso,'0')
  );
EXCEPTION
  WHEN NO_DATA_FOUND THEN NULL;
  WHEN OTHERS        THEN NULL;
END TUA_PLN_FROM_CTCALIDAD;
/

-- ────────────────────────────────────────────────────────────
-- §7.9B  TIA_PLN_FROM_RECTIF_RECETA — LOG inicio de rectificación
-- ────────────────────────────────────────────────────────────
-- Disparo  : AFTER INSERT ON L_RECTIFICA_RECETA FOR EACH ROW
-- Acción   : Registra en PLN_LOG_EVENTOS (TIPO_EVENTO='RC') que se inició
--            una rectificación de receta. El ítem permanece en PASO '9R';
--            el avance a '06' ocurrirá cuando el nuevo TT_RPRODUC se inserte.
-- L_RECTIFICA_RECETA.GUIA = PARTIDA.NUMERO (clave de navegación)
-- Solo registra el evento si PLN_SEGUIMIENTO existe para ese ítem.
-- Campos clave de L_RECTIFICA_RECETA:
--   GUIA        → PARTIDA.NUMERO
--   AREA        → área responsable ('CC'=Control Calidad, 'LA'=Laboratorio)
--   DEFECTO_ORIG→ código defecto que motivó el rechazo (03=Tono, 04=Solidez, etc.)
--   ESTADO      → '1'=Pendiente, '3'=En Proceso, '6'=Aprobada, '9'=Anulada
--   F_ENPROCESO → fecha en que el lab tomó la rectificación
--   F_RECTIFICADO → fecha en que terminaron la nueva fórmula
--   F_APROBADO  → fecha en que se aprobó la rectificación (→ lista para re-tinción)
-- EXCEPCIÓN: NO_DATA_FOUND si la partida no está en PLN_ (ítems fuera del módulo)
CREATE OR REPLACE TRIGGER TIA_PLN_FROM_RECTIF_RECETA
AFTER INSERT ON L_RECTIFICA_RECETA
FOR EACH ROW
DECLARE
  v_nroprog NUMBER;
  v_serie   NUMBER;
  v_num_ped NUMBER;
  v_nro     NUMBER;
  v_num_det NUMBER;
  v_id_seg  NUMBER;
  v_cod_paso VARCHAR2(3);
BEGIN
  -- GUIA = PARTIDA.NUMERO → PARTIDA.NROPROG → ITEMPED_DET
  SELECT p.nroprog, p.serie, p.nro_pedido
  INTO v_nroprog, v_serie, v_num_ped
  FROM partida p
  WHERE p.numero = :NEW.guia;

  SELECT d.nro, d.num_det INTO v_nro, v_num_det
  FROM itemped_det d
  WHERE d.nroprog = v_nroprog AND ROWNUM = 1;

  SELECT s.id_seguim, s.cod_paso_act INTO v_id_seg, v_cod_paso
  FROM pln_seguimiento s
  WHERE s.serie   = v_serie
    AND s.num_ped = v_num_ped
    AND s.nro     = v_nro
    AND s.num_det = v_num_det;

  -- Solo loguear si el ítem está en CC/Reproceso (contexto válido)
  IF v_cod_paso IN ('09','9R') THEN
    INSERT INTO pln_log_eventos (
      id_evento, id_seguim, serie, num_ped, nro, num_det,
      cod_paso, desc_paso, tabla_origen, id_objeto_origen,
      fch_evento, usuario, observacion, tipo_evento
    ) VALUES (
      pln_seq_evento.NEXTVAL, v_id_seg, v_serie, v_num_ped, v_nro, v_num_det,
      v_cod_paso, 'Rectificacion de receta iniciada',
      'L_RECTIFICA_RECETA', :NEW.numero,
      SYSDATE, NVL(:NEW.a_aduser, USER),
      'AREA='||NVL(:NEW.area,'?')||' DEFECTO='||NVL(:NEW.defecto_orig,'?'),
      'RC'
    );
  END IF;
EXCEPTION
  WHEN NO_DATA_FOUND THEN NULL;
  WHEN OTHERS        THEN NULL;
END TIA_PLN_FROM_RECTIF_RECETA;
/

-- ────────────────────────────────────────────────────────────
-- §7.9C  TUA_PLN_FROM_RECTIF_RECETA — LOG aprobación de rectificación
-- ────────────────────────────────────────────────────────────
-- Disparo  : AFTER UPDATE OF ESTADO ON L_RECTIFICA_RECETA FOR EACH ROW
-- Condición: NEW.ESTADO='6' (Aprobada) y antes era diferente
-- Acción   : Registra en PLN_LOG_EVENTOS (TIPO_EVENTO='RA') que la receta
--            fue rectificada y aprobada. El paso NO avanza aquí: el ítem
--            sigue en '9R' hasta que el nuevo baño de tintorería se registre
--            (TIA_PLN_FROM_TT_RPRODUC_PA disparará → PASO '06').
-- La web puede leer este evento para mostrar el aviso:
--   "Receta aprobada - Pendiente inicio de re-tinción"
CREATE OR REPLACE TRIGGER TUA_PLN_FROM_RECTIF_RECETA
AFTER UPDATE OF ESTADO ON L_RECTIFICA_RECETA
FOR EACH ROW
WHEN (NEW.ESTADO = '6' AND (OLD.ESTADO IS NULL OR OLD.ESTADO <> '6'))
DECLARE
  v_nroprog NUMBER;
  v_serie   NUMBER;
  v_num_ped NUMBER;
  v_nro     NUMBER;
  v_num_det NUMBER;
  v_id_seg  NUMBER;
  v_cod_paso VARCHAR2(3);
BEGIN
  SELECT p.nroprog, p.serie, p.nro_pedido
  INTO v_nroprog, v_serie, v_num_ped
  FROM partida p
  WHERE p.numero = :NEW.guia;

  SELECT d.nro, d.num_det INTO v_nro, v_num_det
  FROM itemped_det d
  WHERE d.nroprog = v_nroprog AND ROWNUM = 1;

  SELECT s.id_seguim, s.cod_paso_act INTO v_id_seg, v_cod_paso
  FROM pln_seguimiento s
  WHERE s.serie   = v_serie
    AND s.num_ped = v_num_ped
    AND s.nro     = v_nro
    AND s.num_det = v_num_det;

  INSERT INTO pln_log_eventos (
    id_evento, id_seguim, serie, num_ped, nro, num_det,
    cod_paso, desc_paso, tabla_origen, id_objeto_origen,
    fch_evento, usuario, observacion, tipo_evento
  ) VALUES (
    pln_seq_evento.NEXTVAL, v_id_seg, v_serie, v_num_ped, v_nro, v_num_det,
    v_cod_paso, 'Receta rectificada y aprobada - Pendiente reinicio tincion',
    'L_RECTIFICA_RECETA', :NEW.numero,
    SYSDATE, NVL(:NEW.a_mduser, USER),
    'PROC='||NVL(:NEW.proceso,'?')||' LAB='||NVL(:NEW.c_laboratorista,'?'),
    'RA'
  );
EXCEPTION
  WHEN NO_DATA_FOUND THEN NULL;
  WHEN OTHERS        THEN NULL;
END TUA_PLN_FROM_RECTIF_RECETA;
/

-- ────────────────────────────────────────────────────────────
-- §7.10  TIA_PLN_FROM_REVISADO_G — PASO '11' Revisado (v2.0)
-- ────────────────────────────────────────────────────────────
-- Disparo  : AFTER INSERT ON REVISADO_G FOR EACH ROW WHEN (NEW.GUIA IS NOT NULL)
-- Acción   : Avanza a PASO '11' (Revisado - calidad final aprobada)
-- CAMBIO v2.0 (21/05/2026): Era PASO '10' (Devanado). En v2.0:
--   - PASO '10' (Devanado) ahora lo activa H_RPRODUC INSERT post-CC
--   - PASO '11' (Revisado) es lo que REVISADO_G representa (revisión de calidad)
--   Esto es correcto: REVISADO_G es el registro de revisión visual de conos,
--   ocurre DESPUÉS del devanado (H_RPRODUC AUTOCONER/REDINA).
-- Tabla     : REVISADO_G — cabecera de operación de revisado de conos
-- Confirmado: 100% de REVISADO_G tienen GUIA NOT NULL (90,964 registros)
-- Navegación:
--   REVISADO_G.GUIA → PARTIDA.NUMERO
--   PARTIDA.NROPROG → ITEMPED_DET.(NRO, NUM_DET)
-- TIA_PLN_FROM_REVISADO (REVISADO_D) ELIMINADO en v2.0 — redundante.
-- EXCEPTION WHEN OTHERS THEN NULL → no bloquea el INSERT de REVISADO_G.
CREATE OR REPLACE TRIGGER TIA_PLN_FROM_REVISADO_G
AFTER INSERT ON REVISADO_G
FOR EACH ROW
WHEN (NEW.GUIA IS NOT NULL)
DECLARE
  v_nroprog NUMBER;
  v_serie   NUMBER;
  v_num_ped NUMBER;
  v_nro     NUMBER;
  v_num_det NUMBER;
BEGIN
  SELECT p.nroprog, p.serie, p.nro_pedido
  INTO v_nroprog, v_serie, v_num_ped
  FROM partida p
  WHERE p.numero = :NEW.guia;

  SELECT d.nro, d.num_det INTO v_nro, v_num_det
  FROM itemped_det d
  WHERE d.nroprog = v_nroprog AND ROWNUM = 1;

  PKG_PLN.SP_PLN_AVANZA_PASO(
    v_serie, v_num_ped, v_nro, v_num_det,
    '11', 'REVISADO_G', :NEW.numero, NULL,
    'Revisado calidad - Maq:'||:NEW.maq_proced
  );
EXCEPTION
  WHEN NO_DATA_FOUND THEN NULL;
  WHEN OTHERS        THEN NULL;
END TIA_PLN_FROM_REVISADO_G;
/

-- ────────────────────────────────────────────────────────────
-- §7.11  TIA_PLN_FROM_REVISADO — ELIMINADO en v2.0
-- ────────────────────────────────────────────────────────────
-- Este trigger (AFTER INSERT ON REVISADO_D) fue eliminado en v2.0.
-- Razones:
--   1. REVISADO_G (§7.10) es suficiente para capturar el PASO '11' Revisado.
--      Cada REVISADO_G tiene exactamente uno o más REVISADO_D asociados.
--      Usar REVISADO_D causaba múltiples avances de paso por el mismo evento.
--   2. En la práctica, REVISADO_G ya provee el evento de cabecera con GUIA.
-- El DROP del trigger está en §0.2 para limpiezas de re-despliegue.
-- (No hay CREATE TRIGGER aquí — trigger intencionalmente no recreado)


-- ────────────────────────────────────────────────────────────
-- §7.12  TIA_PLN_FROM_LOTES_PT — PASO '12' Ingresado Almacén PT
-- ────────────────────────────────────────────────────────────
-- Disparo  : AFTER INSERT ON LOTES FOR EACH ROW
-- Condición: NEW.TP_TRANSAC='16' AND NEW.PARTIDA IS NOT NULL AND NEW.COD_ALM IN ('03','07','22','30')
-- Acción   : Avanza a PASO '12' (hilo ingresó al almacén de producto terminado)
-- Tabla     : LOTES — movimientos de inventario (el más voluminoso del sistema)
-- COD_ALM reconocidos (confirmados en BD, usuario='ALMTER' o 'CPRODUC4'):
--   '03' = Almacén PT principal  (aprox. 19k registros/semestre con TP='16')
--   '07' = Almacén PT externo    (aprox. 214 registros/semestre con TP='16')
--   '22' = Almacén PT secundario (aprox. 159 registros/semestre con TP='16')
--   '30' = Almacén PT madeja/HANK (aprox. 548 registros/semestre, PROCESO='20')
-- BUG #33 CORRECCIÓN: '22' y '30' faltaban. Pedidos de madeja (COD_ALM='30')
--   y PT secundario ('22') nunca avanzaban a PASO '12'.
-- TP_TRANSAC='16' = código de transacción "Ingreso de producción a PT"
-- KG_EN_ALM_PT: se suma :NEW.SALDO en SP_PLN_AVANZA_PASO.
-- Navegación:
--   LOTES.PARTIDA → PARTIDA.NUMERO
--   PARTIDA.NROPROG → ITEMPED_DET.(NRO, NUM_DET)
-- NOTA: Después de '12', el ítem espera despacho. Si hay stock suficiente
--   aparece en V_PLN_PENDIENTES_DESP con PASO '12' o '13'.
-- EXCEPTION WHEN OTHERS THEN NULL → no bloquea el INSERT de LOTES.
CREATE OR REPLACE TRIGGER TIA_PLN_FROM_LOTES_PT
AFTER INSERT ON LOTES
FOR EACH ROW
WHEN (NEW.TP_TRANSAC = '16' AND NEW.PARTIDA IS NOT NULL
      AND NEW.COD_ALM IN ('03','07','22','30'))
DECLARE
  v_nroprog NUMBER;
  v_serie   NUMBER;
  v_num_ped NUMBER;
  v_nro     NUMBER;
  v_num_det NUMBER;
BEGIN
  SELECT p.nroprog, p.serie, p.nro_pedido
  INTO v_nroprog, v_serie, v_num_ped
  FROM partida p
  WHERE p.numero = :NEW.partida;

  SELECT d.nro, d.num_det INTO v_nro, v_num_det
  FROM itemped_det d
  WHERE d.nroprog = v_nroprog AND ROWNUM = 1;

  PKG_PLN.SP_PLN_AVANZA_PASO(
    v_serie, v_num_ped, v_nro, v_num_det,
    '12', 'LOTES', :NEW.numero,
    :NEW.saldo, 'Almacén PT '||:NEW.cod_alm||' - Lote:'||:NEW.nlote
  );
EXCEPTION
  WHEN NO_DATA_FOUND THEN NULL;
  WHEN OTHERS        THEN NULL;
END TIA_PLN_FROM_LOTES_PT;
/

-- ────────────────────────────────────────────────────────────
-- §7.13  TUA_PLN_FROM_LOTES_DESPACHO — PASO '14' Despachado/Cerrado
-- ────────────────────────────────────────────────────────────
-- Disparo  : AFTER UPDATE OF S_TRANSAC ON LOTES FOR EACH ROW
-- Condición: NEW.S_TRANSAC IN ('21','23') AND OLD.S_TRANSAC NOT IN ('21','23') AND NEW.PARTIDA IS NOT NULL
-- Acción   : Avanza a PASO '14' con kg despachados (:NEW.SALDO)
-- Tabla     : LOTES — el S_TRANSAC se actualiza cuando se emite guía de despacho
-- S_TRANSAC reconocidos:
--   '21' = Despacho mercado nacional
--   '23' = Despacho exportación
-- DESPACHO PARCIAL (regla de negocio más importante del módulo):
--   SP_PLN_AVANZA_PASO calcula v_nuevo_kg = KG_DESPACHADOS_OLD + :NEW.SALDO
--   Si v_nuevo_kg < CANTIDAD_ORIG → COD_PASO_ACT retrocede a '13'
--     (el ítem sigue activo esperando despacho del saldo pendiente)
--   Si v_nuevo_kg >= CANTIDAD_ORIG → ESTADO='C', FCH_REAL_DESPACHO=SYSDATE
-- NO SE USA KARDEX: TIP_DOC_REF está vacío en ~90% de TP='22'.
--   La trazabilidad del despacho se hace vía LOTES (NUM_KARDEX_DESP informativo).
-- KG_DESPACHADOS y KG_PENDIENTES son los únicos campos actualizados en PASO '14'.
-- EXCEPTION WHEN OTHERS THEN NULL → no bloquea el UPDATE de LOTES.
CREATE OR REPLACE TRIGGER TUA_PLN_FROM_LOTES_DESPACHO
AFTER UPDATE OF S_TRANSAC ON LOTES
FOR EACH ROW
WHEN (NEW.S_TRANSAC IN ('21','23')
      AND (OLD.S_TRANSAC IS NULL OR OLD.S_TRANSAC NOT IN ('21','23'))
      AND NEW.PARTIDA IS NOT NULL)
DECLARE
  v_nroprog NUMBER;
  v_serie   NUMBER;
  v_num_ped NUMBER;
  v_nro     NUMBER;
  v_num_det NUMBER;
BEGIN
  SELECT p.nroprog, p.serie, p.nro_pedido
  INTO v_nroprog, v_serie, v_num_ped
  FROM partida p
  WHERE p.numero = :NEW.partida;

  SELECT d.nro, d.num_det INTO v_nro, v_num_det
  FROM itemped_det d
  WHERE d.nroprog = v_nroprog AND ROWNUM = 1;

  PKG_PLN.SP_PLN_AVANZA_PASO(
    v_serie, v_num_ped, v_nro, v_num_det,
    '14', 'LOTES', :NEW.numero,
    :NEW.saldo, 'Despacho S_TRANSAC='||:NEW.s_transac||' Lote:'||:NEW.nlote
  );
EXCEPTION
  WHEN NO_DATA_FOUND THEN NULL;
  WHEN OTHERS        THEN NULL;
END TUA_PLN_FROM_LOTES_DESPACHO;
/

-- ────────────────────────────────────────────────────────────
-- §7.14  TUA_PLN_FROM_ITEMPED — PASO '01': aprobación tardía de ítem
-- ────────────────────────────────────────────────────────────
-- Propósito : Capturar ítems cuyo ITEMPED fue insertado como borrador (ESTADO='0')
--             y luego aprobado via UPDATE (ESTADO→'5'/'6'). El trigger INSERT
--             TIA_PLN_FROM_ITEMPED salta ítems en borrador, dejando huecos en PLN.
-- Diseño    : COMPOUND para evitar ORA-04091 (mutating table) cuando Ventas
--             actualiza múltiples NROs en un solo UPDATE.
-- Tabla     : ITEMPED (AFTER UPDATE OF estado)
-- ────────────────────────────────────────────────────────────
CREATE OR REPLACE TRIGGER TUA_PLN_FROM_ITEMPED
FOR UPDATE OF estado ON ITEMPED
COMPOUND TRIGGER
  -- Acumulador de ítems pendientes (sin SELECT a tabla mutante)
  TYPE t_rec   IS RECORD (serie NUMBER, num_ped NUMBER, nro NUMBER);
  TYPE t_list  IS TABLE OF t_rec INDEX BY PLS_INTEGER;
  v_list t_list;
  v_idx  PLS_INTEGER := 0;

  AFTER EACH ROW IS
  BEGIN
    -- Solo transición borrador→activo, sin solo-despacho
    IF :OLD.estado IN ('0','9')
       AND :NEW.estado NOT IN ('0','9')
       AND NVL(:NEW.solo_despacho,'N') = 'N'
    THEN
      v_idx := v_idx + 1;
      v_list(v_idx).serie   := :NEW.serie;
      v_list(v_idx).num_ped := :NEW.num_ped;
      v_list(v_idx).nro     := :NEW.nro;
    END IF;
  END AFTER EACH ROW;

  AFTER STATEMENT IS
    v_fch_aprob PEDIDO.F_APROBACION%TYPE;
    v_est_ped   PEDIDO.ESTADO%TYPE;
  BEGIN
    FOR i IN 1..v_idx LOOP
      BEGIN
        SELECT estado, f_aprobacion
        INTO   v_est_ped, v_fch_aprob
        FROM   pedido
        WHERE  serie=v_list(i).serie AND num_ped=v_list(i).num_ped;
        IF v_est_ped NOT IN ('0','9') AND v_fch_aprob IS NOT NULL THEN
          PKG_PLN.SP_PLN_INIT_SEGUIMIENTO(v_list(i).serie, v_list(i).num_ped, v_list(i).nro, 0, '01');
          PKG_PLN.SP_PLN_CALCULA_FECHAS  (v_list(i).serie, v_list(i).num_ped, v_list(i).nro, 0, 'PED');
        END IF;
      EXCEPTION WHEN OTHERS THEN NULL;
      END;
    END LOOP;
  END AFTER STATEMENT;

END TUA_PLN_FROM_ITEMPED;
/

-- ────────────────────────────────────────────────────────────
-- §7.15  TUA_PLN_FROM_PEDIDO — PASO '01': pedido aprobado post-inserción
-- ────────────────────────────────────────────────────────────
-- Propósito : Capturar ítems cuyo PEDIDO fue aprobado (F_APROBACION NULL→valor)
--             DESPUÉS de que se insertó ITEMPED con ITEMPED.ESTADO activo.
--             El INSERT trigger TIA_PLN_FROM_ITEMPED vio F_APROBACION=NULL y saltó.
-- Diseño    : COMPOUND para evitar ORA-04091 cuando se aprueba un pedido.
-- Tabla     : PEDIDO (AFTER UPDATE OF f_aprobacion)
-- ────────────────────────────────────────────────────────────
CREATE OR REPLACE TRIGGER TUA_PLN_FROM_PEDIDO
FOR UPDATE OF f_aprobacion ON PEDIDO
COMPOUND TRIGGER
  TYPE t_rec   IS RECORD (serie NUMBER, num_ped NUMBER, f_aprobacion DATE);
  TYPE t_list  IS TABLE OF t_rec INDEX BY PLS_INTEGER;
  v_list t_list;
  v_idx  PLS_INTEGER := 0;

  AFTER EACH ROW IS
  BEGIN
    -- Solo cuando F_APROBACION pasa de NULL a un valor y pedido no anulado
    IF :OLD.f_aprobacion IS NULL
       AND :NEW.f_aprobacion IS NOT NULL
       AND :NEW.estado NOT IN ('0','9')
    THEN
      v_idx := v_idx + 1;
      v_list(v_idx).serie        := :NEW.serie;
      v_list(v_idx).num_ped      := :NEW.num_ped;
      v_list(v_idx).f_aprobacion := :NEW.f_aprobacion;
    END IF;
  END AFTER EACH ROW;

  AFTER STATEMENT IS
    CURSOR cur_items (p_serie NUMBER, p_num_ped NUMBER) IS
      SELECT nro FROM itemped
      WHERE  serie=p_serie AND num_ped=p_num_ped
        AND  estado NOT IN ('0','9')
        AND  NVL(solo_despacho,'N') = 'N'
        AND  cod_art NOT LIKE 'PEDIDO%';
  BEGIN
    FOR i IN 1..v_idx LOOP
      FOR r IN cur_items(v_list(i).serie, v_list(i).num_ped) LOOP
        BEGIN
          PKG_PLN.SP_PLN_INIT_SEGUIMIENTO(v_list(i).serie, v_list(i).num_ped, r.nro, 0, '01');
          PKG_PLN.SP_PLN_CALCULA_FECHAS  (v_list(i).serie, v_list(i).num_ped, r.nro, 0, 'PED');
        EXCEPTION WHEN OTHERS THEN NULL;
        END;
      END LOOP;
      -- Guardar FCH_APROBACION en todos los ítems del pedido (num_det cualquiera)
      BEGIN
        UPDATE PLN_SEGUIMIENTO
           SET FCH_APROBACION = v_list(i).f_aprobacion,
               A_MDFECHA = SYSDATE, A_MDUSER = USER
         WHERE serie=v_list(i).serie AND num_ped=v_list(i).num_ped
           AND estado = 'A';
      EXCEPTION WHEN OTHERS THEN NULL;
      END;
    END LOOP;
  END AFTER STATEMENT;

END TUA_PLN_FROM_PEDIDO;
/


-- ============================================================
-- §8  VISTAS
-- ============================================================

-- ────────────────────────────────────────────────────────────
-- §8.1  V_PLN_ESTADO_PEDIDO — Estado consolidado por pedido
-- ────────────────────────────────────────────────────────────
-- Propósito  : Visión gerencial / dashboard. Una fila por pedido completo.
-- Uso en app : Dashboard.cshtml (GET /Produccion/Planeamiento/Dashboard)
-- Agrupación : por PEDIDO.SERIE + PEDIDO.NUM_PED
-- JOIN       : PEDIDO → CLIENTES → PLN_SEGUIMIENTO (LEFT JOIN)
-- Filtro     : PEDIDO.ESTADO IN ('0','5','9') → pedidos activos/en proceso
-- Campos clave:
--   total_items         → total de ítems del pedido en seguimiento
--   items_cerrados      → ítems en PASO '14' (despachados)
--   items_con_retraso   → ítems con IND_RETRASO='S'
--   pct_avance          → kg_despachados / kg_total × 100
--   max_dias_retraso    → el ítem más atrasado del pedido
--   fch_entrega_minima  → fecha compromiso más próxima entre todos sus ítems
-- Consulta típica desde C# (PlaneamientoController.Dashboard):
--   var result = await conn.QueryAsync<VPlnEstadoPedido>(
--     $"SELECT * FROM {S}V_PLN_ESTADO_PEDIDO ORDER BY max_dias_retraso DESC");
CREATE OR REPLACE VIEW V_PLN_ESTADO_PEDIDO AS
SELECT
  p.serie,
  p.num_ped,
  p.fecha                AS fch_pedido,
  p.cod_cliente,
  cl.nombre              AS nom_cliente,
  p.estado               AS estado_pedido,
  p.prioridad,
  COUNT(s.id_seguim)     AS total_items,
  SUM(CASE WHEN s.estado = 'C' THEN 1 ELSE 0 END)                         AS items_cerrados,
  SUM(CASE WHEN s.estado = 'A' THEN 1 ELSE 0 END)                         AS items_pendientes,
  SUM(CASE WHEN s.ind_retraso = 'S' AND s.estado = 'A' THEN 1 ELSE 0 END) AS items_con_retraso,
  SUM(s.cantidad_orig)   AS kg_total_pedido,
  SUM(s.kg_despachados)  AS kg_despachados,
  SUM(s.kg_pendientes)   AS kg_pendientes,
  ROUND(SUM(s.kg_despachados) / NULLIF(SUM(s.cantidad_orig),0) * 100, 1) AS pct_avance,
  MIN(s.fch_entrega_comp)   AS fch_entrega_minima,
  MAX(s.fch_real_despacho)  AS fch_ultimo_despacho,
  MAX(s.dias_retraso)       AS max_dias_retraso,
  MAX(s.fch_est_despacho)   AS fch_est_despacho_max
FROM pedido p
JOIN clientes cl ON cl.cod_cliente = p.cod_cliente
LEFT JOIN pln_seguimiento s ON s.serie = p.serie AND s.num_ped = p.num_ped AND s.estado IN ('A','C')
WHERE p.estado IN ('0','5')  -- BUG-F FIX: excluir '9' (pedidos anulados); confirmado sin seguimiento activo
GROUP BY p.serie, p.num_ped, p.fecha, p.cod_cliente, cl.nombre, p.estado, p.prioridad;

-- ────────────────────────────────────────────────────────────
-- §8.2  V_PLN_ESTADO_ITEM — Detalle de estado por ítem con semáforo
-- ────────────────────────────────────────────────────────────
-- Propósito  : Listado detallado de ítems activos con toda la información
--              de estado para el Dashboard y la página Pedido.cshtml.
-- JOIN       : PLN_SEGUIMIENTO + CLIENTES + ARTICUL + PLN_ESTADO_CODIGO + PARTIDA
-- COLUMNAS DE AVANCE (dos métricas distintas):
--   pct_kg_despachado  → kg_despachados / kg_pedido × 100
--                        Mide avance de ENTREGA al cliente. Es 0% hasta que
--                        se produce el primer despacho (paso 14).
--   pct_avance_flujo   → orden_paso / 16 × 100
--                        Mide avance del PROCESO productivo (flujo de trabajo).
--                        Refleja en qué etapa está el ítem (01→6%, 13→88%, 14→100%).
--                        Es el valor que usa PlnSeguimiento.PctAvance en C#.
-- SEMÁFORO (campo calculado):
--   'R' → dias_retraso >= PLN_PARAM.DIAS_ALERTA_CRIT  (default 7)
--   'A' → dias_retraso >= PLN_PARAM.DIAS_ALERTA_ALTA   (default 3)
--   'Y' → dias_retraso >= PLN_PARAM.DIAS_ALERTA_MEDIA  (default 1)
--   'G' → sin retraso (verde)
-- Los umbrales se leen dinámicamente de PLN_PARAM; cambiar en PLN_PARAM se refleja sin
-- recompilar la vista ni el paquete.
-- Mapeo en C# (PlnSeguimiento.PctAvance): usados en ApexCharts Timeline
-- COLOR_UI de PLN_ESTADO_CODIGO: para badges <span class="badge"> y barras de progreso
--   <div class="progress-bar" style="width:@seg.PctAvance%; background:@paso.ColorUi">
CREATE OR REPLACE VIEW V_PLN_ESTADO_ITEM AS
SELECT
  s.id_seguim,
  s.serie,
  s.num_ped,
  s.nro,
  s.num_det,
  s.cod_cliente,
  cl.nombre               AS nom_cliente,
  s.cod_art,
  ar.descripcion          AS desc_art,
  s.color,
  s.titulo,
  s.proceso,
  s.cantidad_orig         AS kg_pedido,
  s.kg_producidos,
  s.kg_en_tin,
  s.kg_en_alm_pt,
  s.kg_despachados,
  s.kg_pendientes,
  ROUND(s.kg_despachados / NULLIF(s.cantidad_orig,0) * 100, 1) AS pct_kg_despachado,
  ROUND(ec.orden_paso / 16.0 * 100, 0)                         AS pct_avance_flujo,
  s.cod_paso_act,
  ec.nombre_paso,
  ec.color_ui,
  s.fch_pedido,
  s.fch_entrega_comp,
  s.fch_est_despacho,
  s.fch_real_despacho,
  s.dias_retraso,
  s.ind_retraso,
  s.ind_urgente,
  s.nro_ciclo,
  s.ind_reproceso,
  CASE
    WHEN s.dias_retraso >= (SELECT valor_num FROM pln_param WHERE cod_param='DIAS_ALERTA_CRIT')  THEN 'R'
    WHEN s.dias_retraso >= (SELECT valor_num FROM pln_param WHERE cod_param='DIAS_ALERTA_ALTA')  THEN 'A'
    WHEN s.dias_retraso >= (SELECT valor_num FROM pln_param WHERE cod_param='DIAS_ALERTA_MEDIA') THEN 'Y'
    ELSE 'G'
  END AS semaforo,
  s.num_programa,
  s.num_partida,
  pt.situ_part,
  s.num_kardex_desp,
  s.estado                AS estado_seguim
FROM pln_seguimiento s
LEFT JOIN clientes cl ON cl.cod_cliente = s.cod_cliente
LEFT JOIN articul  ar ON ar.cod_art     = s.cod_art
JOIN pln_estado_codigo ec ON ec.cod_paso = s.cod_paso_act
LEFT JOIN partida pt ON pt.numero = s.num_partida;

-- ────────────────────────────────────────────────────────────
-- §8.3  V_PLN_TRAZABILIDAD — Timeline completo de fechas por ítem
-- ────────────────────────────────────────────────────────────
-- Propósito  : Provee todos los datos necesarios para el gráfico Timeline
--              Horizontal (ApexCharts rangeBar) en Pedido.cshtml.
-- Uso en app : Pedido.cshtml (GET /Produccion/Planeamiento/Pedido?numPed=&serie=)
-- JOIN       : PLN_SEGUIMIENTO + PEDIDO + ITEMPED_DET + TT_PROGPART (LEFT)
-- Campos calculados (diferencias de días entre hitos):
--   dias_pedido_a_partida   → FCH_REAL_PARTIDA  - PEDIDO.FECHA
--   dias_en_tintoreria      → FCH_REAL_TIN_FIN  - FCH_REAL_TIN_INI
--   dias_partida_a_almpt    → FCH_REAL_ALM_PT   - FCH_REAL_PARTIDA
--   dias_almpt_a_despacho   → FCH_REAL_DESPACHO - FCH_REAL_ALM_PT
--   dias_total_ciclo        → FCH_REAL_DESPACHO - PEDIDO.FECHA
--   dias_desvio_cliente     → FCH_REAL_DESPACHO - FCH_ENTREGA_COMP (+ = tarde)
-- ApexCharts Timeline config:
--   type: 'rangeBar', plotOptions.bar.horizontal: true
--   Series 'Estimado': FCH_EST_* con fillColor + '88' (semitransparente)
--   Series 'Real':     FCH_REAL_* con fillColor sólido
--   xaxis.type: 'datetime', tooltip.x.format: 'dd/MM/yyyy HH:mm'
CREATE OR REPLACE VIEW V_PLN_TRAZABILIDAD AS
SELECT
  s.num_ped,
  s.nro,
  s.num_det,
  s.cod_cliente,
  s.cod_art,
  s.color,
  s.titulo,
  -- ── Las 3 fechas del ciclo de registro/aprobación/planificación ──
  s.fch_pedido,                                              -- PEDIDO.FECHA (registro del pedido)
  s.fch_aprobacion,                                         -- PEDIDO.F_APROBACION (aprobación)
  s.fch_planif,                                             -- ITEMPED_DET.FHC_PROG (fecha programada)
  -- ── Usuarios con nombre completo (JOIN a CS_USER) ──
  pe.a_aduser             AS usr_registro,
  cu_reg.c_nombre         AS nombre_registro,               -- quien registró el pedido (PEDIDO.A_ADUSER)
  pe.a_usaprob            AS usr_aprobacion,
  cu_apr.c_nombre         AS nombre_aprobacion,             -- quien aprobó el pedido (PEDIDO.A_USAPROB)
  s.usr_planif,
  cu_pln.c_nombre         AS nombre_planif,                 -- quien planificó (ITEMPED_DET.A_ADUSER al asignar NROPROG)
  -- fallback directo desde legacy (por si PLN_SEGUIMIENTO no fue actualizado aún)
  pe.fecha                AS fch_pedido_raw,
  pe.f_aprobacion         AS fch_aprob_pedido_raw,
  COALESCE(s.fch_planif, id.fhc_prog, s.fch_real_programado) AS fch_planeada,  -- prioriza PLN_ > legacy > SYSDATE
  id.fhc_entrega          AS fch_entrega_plan,
  id.fch_estima_cono_uno  AS fch_est_cono1,
  id.fch_estima_tenido    AS fch_est_tenido,
  -- Fechas estimadas calculadas por SP_PLN_CALCULA_FECHAS (PLN_SEGUIMIENTO)
  s.fch_est_hilanderia,
  s.fch_est_partida,
  s.fch_est_tin_ini,
  s.fch_est_tin_fin,
  s.fch_est_secado,
  s.fch_est_calidad,
  s.fch_est_despacho,
  s.fch_real_programado,
  s.fch_real_produccion,
  s.fch_real_partida,
  s.fch_real_tin_ini,
  tt.fentrega             AS fch_prog_tin,
  s.fch_real_tin_fin,
  s.fch_real_secado,
  s.fch_real_cc_tinto,
  s.fch_real_cc_rechazo,
  s.fch_real_gaseado,   -- v2.0: FCH_REAL_GASEADO (solo PROCESO='24')
  s.fch_real_devanado,
  s.fch_real_calidad,
  s.fch_real_alm_pt,
  s.fch_real_despacho,
  s.fch_entrega_comp      AS fch_compromiso_cliente,
  s.fch_real_partida   - pe.fecha           AS dias_pedido_a_partida,
  s.fch_real_tin_fin   - s.fch_real_tin_ini AS dias_en_tintoreria,
  s.fch_real_alm_pt    - s.fch_real_partida AS dias_partida_a_almpt,
  s.fch_real_despacho  - s.fch_real_alm_pt  AS dias_almpt_a_despacho,
  s.fch_real_despacho  - pe.fecha           AS dias_total_ciclo,
  s.fch_real_despacho  - s.fch_entrega_comp AS dias_desvio_cliente,
  s.cod_paso_act,
  s.dias_retraso,
  s.nro_ciclo
FROM pln_seguimiento s
JOIN pedido     pe ON pe.serie=s.serie AND pe.num_ped=s.num_ped
-- BUG-E FIX: ITEMPED_DET puede tener múltiples filas por (serie,num_ped,nro,num_det)
-- cuando se reasignan NROPROG (confirmado: 6,725 grupos con duplicados, max 30 filas).
-- ROW_NUMBER() toma solo la fila con el NROPROG más alto (programa más reciente).
LEFT JOIN (
  SELECT serie, num_ped, nro, num_det,
         fhc_prog, fhc_entrega, fch_estima_cono_uno, fch_estima_tenido
  FROM (
    SELECT id2.serie, id2.num_ped, id2.nro, id2.num_det,
           id2.fhc_prog, id2.fhc_entrega, id2.fch_estima_cono_uno, id2.fch_estima_tenido,
           ROW_NUMBER() OVER (
             PARTITION BY id2.serie, id2.num_ped, id2.nro, id2.num_det
             ORDER BY NVL(id2.nroprog, 0) DESC NULLS LAST
           ) rn
    FROM itemped_det id2
  ) WHERE rn = 1
) id ON id.serie=s.serie AND id.num_ped=s.num_ped
                       AND id.nro=s.nro AND id.num_det=s.num_det  -- LEFT: ítems sin ITEMPED_DET no se pierden
-- JOINs a CS_USER para resolver nombres de los 3 actores del flujo
LEFT JOIN cs_user cu_reg ON cu_reg.c_user = pe.a_aduser   -- quien registró el pedido
LEFT JOIN cs_user cu_apr ON cu_apr.c_user = pe.a_usaprob  -- quien aprobó el pedido
LEFT JOIN cs_user cu_pln ON cu_pln.c_user = s.usr_planif  -- quien planificó (asignó NROPROG)
-- LEFT JOIN con subquery para evitar duplicados cuando TT_PROGPART tiene múltiples
-- registros por (num_ped, nro, num_det) (distintos programas de TT para el mismo ítem).
LEFT JOIN (SELECT num_ped, nro, num_det, MAX(fentrega) AS fentrega
           FROM tt_progpart GROUP BY num_ped, nro, num_det) tt
        ON tt.num_ped=s.num_ped AND tt.nro=s.nro AND tt.num_det=s.num_det;

-- ────────────────────────────────────────────────────────────
-- §8.4  V_PLN_ALERTAS_ACTIVAS — Panel de alertas activas (v2.3 enriquecida)
-- ────────────────────────────────────────────────────────────
-- Propósito  : Bandeja de alertas para supervisores en Alertas.cshtml.
-- Uso en app : Alertas.cshtml (GET /Produccion/Planeamiento/Alertas)
-- Filtro     : PLN_ALERTA.ESTADO = 'A' (solo alertas activas)
-- Orden      : NIVEL ('C' primero) → FCH_ALERTA
-- CAMPOS ENRIQUECIDOS (v2.3, 26/05/2026):
--   Añadidos LEFT JOIN PLN_SEGUIMIENTO + PLN_ESTADO_CODIGO para llevar a la
--   vista toda la información necesaria sin subqueries adicionales en la app:
--   · serie, cod_art, titulo_art, proceso, cod_paso_act, nombre_paso, color_ui
--   · fch_entrega_comp, dias_retraso_ent (días vencido respecto a FCH_ENTREGA_COMP)
--   · cantidad_orig, kg_pendientes, nro_ciclo, ind_urgente
-- CRÍTICO — horas_sin_resolver:
--   ROUND((SYSDATE-FCH_ALERTA)*24, 2) — en horas con 2 decimales.
--   NO usar sin ROUND: Oracle devuelve NUMBER de alta precisión → ODP.NET
--   DecimalConv.GetDecimal → OverflowException en C#.
-- Acciones POST en Alertas.cshtml:
--   POST /Produccion/Planeamiento/ResolverAlerta → ESTADO='R'
--   POST /Produccion/Planeamiento/IgnorarAlerta  → ESTADO='I'
CREATE OR REPLACE VIEW V_PLN_ALERTAS_ACTIVAS AS
SELECT
  a.id_alerta,
  a.serie,
  a.tip_alerta,
  a.nivel,
  a.titulo,
  a.detalle,
  a.fch_alerta,
  a.fch_limite,
  a.dias_retraso,
  a.num_ped,
  a.nro,
  a.num_det,
  a.cod_cliente,
  cl.nombre                                  AS nom_cliente,
  a.cod_maq,
  a.estado,
  ROUND((SYSDATE - a.fch_alerta) * 24, 2)   AS horas_sin_resolver,
  -- Datos del ítem de seguimiento (enriquecidos v2.3)
  s.cod_art,
  s.titulo                                   AS titulo_art,
  s.proceso,
  s.cod_paso_act,
  ec.nombre_paso,
  ec.color_ui,
  s.fch_entrega_comp,
  CASE WHEN s.fch_entrega_comp IS NOT NULL
       THEN TRUNC(SYSDATE) - TRUNC(s.fch_entrega_comp)
       ELSE NULL END                          AS dias_retraso_ent,
  s.cantidad_orig,
  s.kg_pendientes,
  s.nro_ciclo,
  s.ind_urgente
FROM pln_alerta a
LEFT JOIN clientes         cl ON cl.cod_cliente  = a.cod_cliente
LEFT JOIN pln_seguimiento  s  ON s.id_seguim      = a.id_seguim
LEFT JOIN pln_estado_codigo ec ON ec.cod_paso     = s.cod_paso_act
WHERE a.estado = 'A'
ORDER BY
  CASE a.nivel WHEN 'C' THEN 1 WHEN 'A' THEN 2 WHEN 'M' THEN 3 ELSE 4 END,
  a.fch_alerta;

-- ────────────────────────────────────────────────────────────
-- §8.5  V_PLN_CARGA_MAQUINAS — Carga de máquinas próximos 30 días
-- ────────────────────────────────────────────────────────────
-- Propósito  : Fuente de datos para el Gantt/Heatmap de CargaMaquinas.cshtml.
-- Uso en app : CargaMaquinas.cshtml (GET /Produccion/Planeamiento/CargaMaquinas)
-- Filtro     : FECHA BETWEEN TRUNC(SYSDATE) AND TRUNC(SYSDATE)+30
-- Campo ESTADO_CARGA (para color del heatmap en ApexCharts):
--   PCT_CARGA > 95% → 'SOBRECARGADA'  (rojo)
--   PCT_CARGA > 80% → 'CARGA_ALTA'    (naranja)
--   PCT_CARGA > 50% → 'CARGA_MEDIA'   (amarillo)
--   else            → 'DISPONIBLE'    (verde)
-- Máquinas Tintorería: R01-R19 (Thies), M01-M08 (Hank)
-- Máquinas Hilandería: PAB/HI/etc.
-- Heatmap ApexCharts: eje Y=máquinas, eje X=fechas, intensidad=PCT_CARGA
-- Refrescado por JOB_PLN_CARGA (cada 4 horas: 00:00, 04:00, 08:00, 12:00, 16:00, 20:00).
CREATE OR REPLACE VIEW V_PLN_CARGA_MAQUINAS AS
SELECT
  c.fecha,
  c.cod_maq,
  c.tp_maq,
  c.horas_capacidad,
  c.kg_capacidad,
  c.horas_asignadas,
  c.kg_asignados,
  c.nro_pedidos,
  c.horas_real,
  c.kg_real,
  c.pct_utilizacion,
  c.pct_carga,
  c.ind_sobrecargada,
  CASE
    WHEN c.pct_carga > 95 THEN 'SOBRECARGADA'
    WHEN c.pct_carga > 80 THEN 'CARGA_ALTA'
    WHEN c.pct_carga > 50 THEN 'CARGA_MEDIA'
    ELSE 'DISPONIBLE'
  END AS estado_carga
FROM pln_carga_diaria c
WHERE c.fecha BETWEEN TRUNC(SYSDATE) AND TRUNC(SYSDATE) + 30;

-- ────────────────────────────────────────────────────────────
-- §8.6  V_PLN_PENDIENTES_DESP — Lista de ítems pendientes de despacho
-- ────────────────────────────────────────────────────────────
-- Propósito  : Listado priorizado de ítems listos para despachar.
-- Uso en app : Sección de despacho en Dashboard.cshtml y módulo Almacén
-- FILTRO CORREGIDO (vs. Propuesta.md que usaba pasos '10','11'):
--   cod_paso_act IN ('12','13')  ← almacén PT y listo para despacho
--   kg_pendientes > 0            ← aún hay saldo por despachar
--   estado = 'A'                 ← ítem activo
-- JOIN a ALMACEN (COD_ALM='01') para mostrar stock disponible:
--   kg_a_despachar = LEAST(kg_pendientes, NVL(stock_disponible, 0))
-- Priorización (ORDER BY):
--   1. IND_URGENTE='S' primero
--   2. PEDIDO.PRIORIDAD DESC
--   3. FCH_ENTREGA_COMP ASC (más antiguo primero)
-- Campo DIAS_VENCIDO: TRUNC(SYSDATE) - FCH_ENTREGA_COMP (positivo = vencido)
CREATE OR REPLACE VIEW V_PLN_PENDIENTES_DESP AS
SELECT
  s.serie,
  s.num_ped,
  s.nro,
  s.cod_cliente,
  cl.nombre           AS nom_cliente,
  s.cod_art,
  ar.descripcion      AS desc_art,
  s.color,
  s.titulo,
  s.kg_pendientes,
  al.stock            AS stock_disponible,
  LEAST(s.kg_pendientes, NVL(al.stock,0)) AS kg_a_despachar,
  s.fch_entrega_comp,
  TRUNC(SYSDATE) - s.fch_entrega_comp AS dias_vencido,
  s.dias_retraso,
  s.ind_urgente,
  s.cod_paso_act,
  ec.nombre_paso,
  p.prioridad         AS prioridad_pedido
FROM pln_seguimiento s
LEFT JOIN clientes    cl ON cl.cod_cliente = s.cod_cliente
LEFT JOIN articul     ar ON ar.cod_art     = s.cod_art
JOIN pedido            p ON p.serie=s.serie AND p.num_ped=s.num_ped
JOIN pln_estado_codigo ec ON ec.cod_paso = s.cod_paso_act
-- BUG #38 CORRECCIÓN: COD_ALM='01' tiene 0 stock (solo 37 artículos de admin).
-- PT real está en almacenes '03' (principal), '07' (externo), '22' (secundario), '30' (madeja).
-- Se usa subquery que agrega el stock a través de los 4 almacenes PT confirmados en BD.
LEFT JOIN (SELECT cod_art, SUM(NVL(stock,0)) AS stock
           FROM almacen
           WHERE cod_alm IN ('03','07','22','30')
           GROUP BY cod_art) al ON al.cod_art = s.cod_art
WHERE s.cod_paso_act IN ('12','13')   -- ← corrección: almacén PT y listo para despacho
  AND s.kg_pendientes > 0
  AND s.estado = 'A'
ORDER BY
  CASE WHEN s.ind_urgente='S' THEN 0 ELSE 1 END,
  p.prioridad DESC,
  s.fch_entrega_comp;

-- ────────────────────────────────────────────────────────────
-- §8.7  V_PLN_KPI_CUMPLIMIENTO — KPI OTIF mensual
-- ────────────────────────────────────────────────────────────
-- Propósito  : KPI principal del módulo. On Time In Full.
-- Uso en app : KPIs.cshtml (GET /Produccion/Planeamiento/KPIs)
-- FILTRO CORREGIDO (vs. Propuesta.md original):
--   ESTADO = 'C'          ← solo ítems cerrados (despachados completamente)
--   cod_paso_act = '14'   ← solo ítems en paso final de despacho
--   fch_real_despacho IS NOT NULL  ← con fecha real de despacho
-- MÉTRICAS:
--   pct_otif            : % entregados a tiempo (fch_real_despacho <= fch_entrega_comp)
--   ciclo_promedio_dias : promedio (fch_real_despacho - fch_pedido)
--   dias_prom_tintoreria: promedio días en tintorería
--   dias_prom_pedido_partida: promedio días desde pedido hasta lote disponible
--   retraso_promedio_dias: promedio dias_retraso para todos los cerrados
-- Agrupación: por mes (TRUNC(fch_real_despacho,'MM'))
-- Orden: más reciente primero (ORDER BY 1 DESC)
CREATE OR REPLACE VIEW V_PLN_KPI_CUMPLIMIENTO AS
SELECT
  TRUNC(s.fch_real_despacho,'MM')                     AS periodo,
  COUNT(*)                                            AS total_items_cerrados,
  SUM(CASE WHEN s.fch_real_despacho <= s.fch_entrega_comp THEN 1 ELSE 0 END) AS entregados_a_tiempo,
  SUM(CASE WHEN s.fch_real_despacho >  s.fch_entrega_comp THEN 1 ELSE 0 END) AS entregados_tarde,
  -- BUG-4 CORREGIDO (21/05/2026): OTIF requiere tambien cantidad completa (>= 99%)
  -- La version anterior solo verificaba fecha (OTD, no OTIF verdadero).
  SUM(CASE WHEN s.fch_real_despacho <= s.fch_entrega_comp
            AND s.kg_despachados >= s.cantidad_orig * 0.99
           THEN 1 ELSE 0 END)                                                 AS otif,
  ROUND(SUM(CASE WHEN s.fch_real_despacho <= s.fch_entrega_comp
                  AND s.kg_despachados >= s.cantidad_orig * 0.99
                 THEN 1 ELSE 0 END)
        / NULLIF(COUNT(*),0) * 100, 1)                AS pct_otif,
  ROUND(AVG(s.fch_real_despacho - s.fch_pedido),1)    AS ciclo_promedio_dias,
  ROUND(AVG(s.fch_real_tin_fin - s.fch_real_tin_ini),1) AS dias_prom_tintoreria,
  -- v2.0: FCH_REAL_PARTIDA es NULL en el sistema 2026 (PASO '05' dead code).
  -- NVL fallback a FCH_REAL_PRODUCCION (set en PASO '04') para no perder la métrica.
  ROUND(AVG(NVL(s.fch_real_partida, s.fch_real_produccion) - s.fch_pedido),1) AS dias_prom_pedido_partida,
  SUM(s.kg_despachados)                               AS kg_total_despachados,
  ROUND(AVG(GREATEST(s.dias_retraso,0)),1)            AS retraso_promedio_dias
FROM pln_seguimiento s
WHERE s.estado = 'C'              -- ← corrección: solo cerrados
  AND s.cod_paso_act = '14'       -- ← corrección: solo despacho completo
  AND s.fch_real_despacho IS NOT NULL
GROUP BY TRUNC(s.fch_real_despacho,'MM')
ORDER BY 1 DESC;

-- ────────────────────────────────────────────────────────────
-- §8.8  V_PLN_KPI_PRODUCCION — KPIs de producción por máquina
-- ────────────────────────────────────────────────────────────
-- Propósito  : Indicadores de eficiencia de hilandería y tintorería.
-- Uso en app : KPIs.cshtml (sección de producción por máquina)
-- Fuente     : H_PRODUCCION_D (detalle) + H_PRODUCCION_G (cabecera)
-- Ventana    : últimos 12 meses (ADD_MONTHS(TRUNC(SYSDATE,'MM'), -12))
-- MÉTRICAS:
--   kg_producidos     : suma total de kg producidos en el período
--   horas_prom_turno  : promedio de horas trabajadas por turno
--   horas_prom_parada : promedio de horas de parada (mantenimiento/falla)
--   kg_por_hora       : eficiencia = kg_producidos / horas_trabajadas
--   dias_activos      : COUNT DISTINCT de días con producción registrada
-- Agrupación: por mes + TP_MAQ + COD_MAQ
CREATE OR REPLACE VIEW V_PLN_KPI_PRODUCCION AS
SELECT
  TRUNC(h.fecha,'MM')                                          AS periodo,
  h.tp_maq,
  h.cod_maq,
  SUM(d.cantidad)                                              AS kg_producidos,
  -- HH.MM→decimal: igual que SP_PLN_CARGA_DIARIA_REFRESH. NULL en AVG ignora filas inválidas.
  ROUND(AVG(CASE WHEN REGEXP_LIKE(d.horas_trabajadas,'^\d{2}\.\d{2}$')
                 THEN TO_NUMBER(SUBSTR(d.horas_trabajadas,1,2))
                    + TO_NUMBER(SUBSTR(d.horas_trabajadas,4,2))/60
                 END), 2)                                              AS horas_prom_turno,
  ROUND(AVG(CASE WHEN REGEXP_LIKE(d.horas_parada,'^\d{2}\.\d{2}$')
                 THEN TO_NUMBER(SUBSTR(d.horas_parada,1,2))
                    + TO_NUMBER(SUBSTR(d.horas_parada,4,2))/60
                 END), 2)                                              AS horas_prom_parada,
  ROUND(SUM(d.cantidad) / NULLIF(SUM(CASE
    WHEN REGEXP_LIKE(d.horas_trabajadas,'^\d{2}\.\d{2}$')
    THEN TO_NUMBER(SUBSTR(d.horas_trabajadas,1,2))
       + TO_NUMBER(SUBSTR(d.horas_trabajadas,4,2))/60
    ELSE 0
  END), 0), 2)                                                         AS kg_por_hora,
  COUNT(DISTINCT h.fecha)                                      AS dias_activos
FROM h_produccion_d d
JOIN h_produccion_g h ON h.fecha   = d.fecha
                     AND h.turno   = d.turno
                     AND h.tp_maq  = d.tp_maq
                     AND h.cod_maq = d.cod_maq
                     AND h.c_codigo= d.c_codigo
WHERE h.fecha >= ADD_MONTHS(TRUNC(SYSDATE,'MM'), -12)
GROUP BY TRUNC(h.fecha,'MM'), h.tp_maq, h.cod_maq
ORDER BY 1 DESC, h.tp_maq, h.cod_maq;


-- ============================================================
-- §9  JOBS PROGRAMADOS (DBMS_SCHEDULER — Oracle 11g)
-- ============================================================
-- JOB_PLN_ALERTAS:
--   Ejecuta SP_PLN_GENERA_ALERTAS cada hora en punto.
--   Escanea PLN_SEGUIMIENTO buscando retrasos, ítems sin planificar,
--   partidas esperando TT y reprocesos activos.
--
-- JOB_PLN_CARGA:
--   Ejecuta SP_PLN_CARGA_DIARIA_REFRESH cada 4 horas (planta 24/7).
--   Regenera PLN_CARGA_DIARIA para los próximos 30 días.
--   Patrón: FREQ=HOURLY; INTERVAL=4; BYMINUTE=0
--
-- POR QUÉ enabled=>FALSE en este script:
--   La BD Oracle es una sola instancia compartida (10.0.7.11). No existe un
--   servidor DEV separado. Si se despliega este script en desarrollo con enabled=>TRUE:
--     · JOB_PLN_ALERTAS (c/hora) contamina PLN_ALERTA con alertas de datos de prueba,
--       visibles en la UI de supervisores de producción.
--     · JOB_PLN_CARGA (c/4h) hace DELETE+INSERT de toda PLN_CARGA_DIARIA, borrando
--       cualquier carga insertada manualmente durante las pruebas del día siguiente.
--   EN PRODUCCIÓN: cambiar enabled=>FALSE por enabled=>TRUE antes de ejecutar,
--   o habilitar ambos manualmente tras el despliegue:
--     BEGIN DBMS_SCHEDULER.ENABLE('JOB_PLN_ALERTAS'); END;
--     BEGIN DBMS_SCHEDULER.ENABLE('JOB_PLN_CARGA');   END;
--
-- Para verificar estado de los jobs:
--   SELECT job_name, state, last_start_date, last_run_duration, next_run_date
--   FROM user_scheduler_jobs WHERE job_name LIKE 'JOB_PLN_%';
--
-- Para ejecutar manualmente:
--   BEGIN DBMS_SCHEDULER.RUN_JOB('JOB_PLN_ALERTAS'); END;
--   BEGIN DBMS_SCHEDULER.RUN_JOB('JOB_PLN_CARGA');   END;

BEGIN
  DBMS_SCHEDULER.CREATE_JOB (
    job_name        => 'JOB_PLN_ALERTAS',
    job_type        => 'STORED_PROCEDURE',
    job_action      => 'PKG_PLN.SP_PLN_GENERA_ALERTAS',
    start_date      => SYSTIMESTAMP,
    repeat_interval => 'FREQ=HOURLY; BYMINUTE=0',
    enabled         => FALSE,   -- cambiar a TRUE en PROD
    comments        => 'PLN_: genera alertas de retraso cada hora en punto'
  );
END;
/

BEGIN
  DBMS_SCHEDULER.CREATE_JOB (
    job_name        => 'JOB_PLN_CARGA',
    job_type        => 'STORED_PROCEDURE',
    job_action      => 'PKG_PLN.SP_PLN_CARGA_DIARIA_REFRESH',
    start_date      => SYSTIMESTAMP,
    repeat_interval => 'FREQ=HOURLY; INTERVAL=4; BYMINUTE=0',
    enabled         => FALSE,   -- cambiar a TRUE en PROD
    comments        => 'PLN_: recalcula carga de máquinas próximos 30 días (cada 4 horas: 00:00, 04:00, 08:00, 12:00, 16:00, 20:00)'
  );
END;
/

BEGIN
  DBMS_SCHEDULER.CREATE_JOB (
    job_name        => 'JOB_PLN_KGR',
    job_type        => 'STORED_PROCEDURE',
    job_action      => 'PKG_PLN.SP_PLN_KGR_REFRESH',
    start_date      => SYSTIMESTAMP,
    repeat_interval => 'FREQ=MONTHLY; BYMONTHDAY=1; BYHOUR=1; BYMINUTE=0',
    enabled         => FALSE,   -- cambiar a TRUE en PROD
    comments        => 'PLN_: recalcula velocidades kg/hr desde H_RPRODUC (día 1 de cada mes a las 01:00)'
  );
END;
/


-- ============================================================
-- §10  ACTIVACIÓN DE JOBS — Producción
-- ============================================================
-- Los jobs se crearon en §9 con enabled=>FALSE para que el CREATE
-- no dispare ejecuciones intermedias mientras se instalan las tablas
-- y el paquete.  Aquí se activan una vez que todo el esquema está listo.
--
-- JOB_PLN_ALERTAS : se ejecutará a la siguiente hora en punto.
-- JOB_PLN_CARGA   : se ejecutará al próximo múltiplo de 4 horas (00:00, 04:00, 08:00...).
-- JOB_PLN_KGR     : mensual. Ejecutar manualmente la primera vez (ver §10.3 abajo).
--
-- NOTA SOBRE DATOS HISTÓRICOS:
--   PLN_ captura data ÚNICAMENTE desde este momento de despliegue.
--   Los triggers disparan solo ante operaciones nuevas:
--     · ITEMPED INSERT  → crea fila en PLN_SEGUIMIENTO
--     · PARTIDA INSERT  → avanza a PASO '03'
--     · LOTES UPDATE    → avanza a PASO '14'  (etc.)
--   Los pedidos / partidas / lotes ya existentes antes del despliegue
--   NO quedan en PLN_SEGUIMIENTO. La migración histórica es opcional
--   y deberá construirse por separado (ver §11 cuando corresponda).
-- ============================================================

-- PROMPT ============================================================
-- PROMPT §10  Activando jobs PLN_ en producción
-- PROMPT ============================================================
-- -- Ejecutar manualmente DESPUÉS de verificar el despliegue:
--
-- PROMPT >>> §10.1 Activando JOB_PLN_ALERTAS (cada hora en punto)...
-- BEGIN
--   DBMS_SCHEDULER.ENABLE('JOB_PLN_ALERTAS');
-- END;
-- /
--
-- PROMPT >>> §10.2 Activando JOB_PLN_CARGA (cada 4 horas, planta 24/7)...
-- BEGIN
--   DBMS_SCHEDULER.ENABLE('JOB_PLN_CARGA');
-- END;
-- /
--
-- PROMPT >>> §10.3 Ejecutando SP_PLN_KGR_REFRESH por primera vez + activando JOB_PLN_KGR...
-- BEGIN
--   PKG_PLN.SP_PLN_KGR_REFRESH;         -- pobla PLN_KGR_TITULO (puede tardar ~5-10s)
-- END;
-- /
-- BEGIN
--   DBMS_SCHEDULER.ENABLE('JOB_PLN_KGR');
-- END;
-- /
-- -- Verificar cobertura tras la primera ejecución:
-- SELECT titulo, proceso, COUNT(*) AS maquinas, MAX(n_muestras) AS max_muestras
-- FROM   PLN_KGR_TITULO
-- GROUP  BY titulo, proceso
-- ORDER  BY titulo, proceso;
-- /
--
-- PROMPT >>> Verificando estado de los jobs PLN_:
-- SELECT job_name,
--        state,
--        TO_CHAR(next_run_date, 'DD/MM/YYYY HH24:MI') AS proxima_ejecucion,
--        enabled
-- FROM   user_scheduler_jobs
-- WHERE  job_name LIKE 'JOB_PLN_%'
-- ORDER  BY job_name;
-- /

PROMPT
PROMPT ============================================================
PROMPT Despliegue PLN_ completado exitosamente.
PROMPT   - Triggers activos: 15 (incluye TIA_PLN_FROM_RECTIF_RECETA + TUA_PLN_FROM_RECTIF_RECETA v2.1)
PROMPT   - Vistas disponibles: 8
PROMPT   - Jobs programados: JOB_PLN_ALERTAS (c/hora) + JOB_PLN_CARGA (c/4h) + JOB_PLN_KGR (mensual dia 1)
PROMPT   - IMPORTANTE: ejecutar PKG_PLN.SP_PLN_KGR_REFRESH manualmente para poblar PLN_KGR_TITULO
PROMPT   - Data desde: ahora (no hay migracion historica)
PROMPT ============================================================


-- ============================================================
-- §11  SCRIPT DE MIGRACIÓN HISTÓRICA
-- ============================================================
-- Propósito : Inicializa PLN_SEGUIMIENTO para ítems de pedido que preexistían
--   al despliegue del módulo PLN_ (19/05/2026). Los triggers capturan eventos
--   futuros; este script cubre el "stock inicial" de datos históricos.
--
-- EJECUCIÓN : Idempotente — puede re-ejecutarse sin riesgo.
--   PASO A inicializa filas faltantes (SP_PLN_INIT_SEGUIMIENTO tiene
--   DUP_VAL_ON_INDEX → ignora si ya existe).
--   PASO B sólo avanza el PASO si el calculado es mayor al actual.
--
-- ALCANCE:
--   · Solo pedidos activos: PEDIDO.ESTADO IN ('0','5')
--   · Ítems no cerrados: ITEMPED.ESTADO < '9'
--   · No migra datos de despachos anteriores (KG_DESPACHADOS=0 hasta
--     que el trigger TUA_PLN_FROM_LOTES_DESPACHO los acumule en vivo).
--
-- LIMITACIONES CONOCIDAS:
--   · TT_RPRODUC: para partidas modernas (2021+) el vínculo es via PARTIDA_MAS.
--     PARTIDA.NUMERO → PARTIDA_MAS.PARTIDA (TP_TRANSAC='IR') → PARTIDA_MAS.NUMERO
--     → TT_RPRODUC.RECETA (TIPODOC='IR'). Fechas reales: FECHA_INI / FECHA_FIN.
--   · H_RPRODUC: PASO '10' (Devanado post-CC). Columnas: GUIA, TP_MAQ, FECHA_INI.
--     GUIA = PARTIDA.NUMERO, TP_MAQ NOT IN ('G').
--   · L_VALIDA_RECETA: PASO '04'. Fecha = A_ADFECHA donde ESTADO='3'.
-- BUGS CORREGIDOS vs versión anterior:
--   BUG-1: TT via ING_RECETAS_G (no funciona para partidas 2021+).
--          FIX: PARTIDA_MAS → TT_RPRODUC (sistema IR).
--   NOTA: LOTES.CANTIDAD = unidades físicas (conos/rollos), LOTES.SALDO = peso real en kg.
--         KG_ALM_PT y KG_DESP usan SUM(NVL(SALDO,0)) — es el peso real.
--   BUG-4/5: PASO '10' y FCH_REAL_DEVANADO nunca detectados. FIX: H_RPRODUC.
--   BUG-6: KG_EN_TIN nunca poblado. FIX: = KG_PROD cuando PASO >= '06'.
--   BUG-7: FCH_REAL_TIN_INI/FIN usaban proxy PARTIDA.FECHA.
--          FIX: MIN/MAX de TT_RPRODUC.FECHA_INI/FIN via PARTIDA_MAS.
--
-- FUENTES DE FECHAS REALES (verificadas contra BD):
--   PASO '02' → ITEMPED_DET.FHC_PROG
--   PASO '03' → PARTIDA.FECHA
--   PASO '06' → MIN(TT_RPRODUC.FECHA_INI) via PARTIDA_MAS (sistema IR)
--   PASO '07' → MAX(TT_RPRODUC.FECHA_FIN WHERE ESTADO='3') via PARTIDA_MAS
--   PASO '08' → TT_RSECADO.FECHA_FIN (verificado en BD)
--   PASO '09' → CTCALIDAD_D.FCH_CONSULTA (último resultado aprobado)
--   PASO '9R' → CTCALIDAD_D.FCH_CONSULTA (último resultado rechazado)
--   PASO '10' → H_RPRODUC.FECHA_INI (TP_MAQ NOT IN 'G')
--   PASO '11' → REVISADO_G.NVL(FCH_FIN_REVISA, A_ADFECHA)
--   PASO '12' → LOTES.FECHA (primer ingreso COD_ALM IN ('03','07','22','30') TP='16')
--   PASO '14' → LOTES.FEC_SALIDA (primer despacho S_TRANSAC IN ('21','23'))
-- ============================================================

DECLARE
  -- Contadores de diagnóstico
  v_cnt_init  PLS_INTEGER := 0;
  v_cnt_upd   PLS_INTEGER := 0;
  v_cnt_skip  PLS_INTEGER := 0;
  v_cnt_err   PLS_INTEGER := 0;

  -- ── Función auxiliar: ORDEN_PASO de un código de paso ──────────
  FUNCTION f_orden(p_paso VARCHAR2) RETURN NUMBER IS
    v_ord NUMBER;
  BEGIN
    SELECT orden_paso INTO v_ord FROM pln_estado_codigo WHERE cod_paso = p_paso;
    RETURN NVL(v_ord, 0);
  EXCEPTION WHEN OTHERS THEN RETURN 0;
  END f_orden;

BEGIN
  -- ════════════════════════════════════════════════════════════════
  -- PASO A — Inicializar filas faltantes en PLN_SEGUIMIENTO
  -- ════════════════════════════════════════════════════════════════
  FOR rec IN (
    SELECT d.serie, d.num_ped, d.nro, d.num_det,
           d.nroprog,
           NVL(i.solo_despacho, 'N') AS solo_despacho
    FROM   itemped_det d
    JOIN   itemped i  ON  i.serie=d.serie AND i.num_ped=d.num_ped AND i.nro=d.nro
    JOIN   pedido  p  ON  p.serie=i.serie AND p.num_ped=i.num_ped
    WHERE  p.estado IN ('0','5')       -- pedidos activos / en proceso
      AND  i.estado  < '9'             -- ítem no anulado/cerrado
      AND  p.fecha   >= DATE '2026-05-01'  -- solo pedidos desde Mayo 2026
      AND  NOT EXISTS (
             SELECT 1
             FROM   pln_seguimiento s
             WHERE  s.serie=d.serie AND s.num_ped=d.num_ped
               AND  s.nro=d.nro    AND s.num_det=d.num_det)
    ORDER BY d.num_ped, d.nro, d.num_det
  ) LOOP
    BEGIN
      PKG_PLN.SP_PLN_INIT_SEGUIMIENTO(
        rec.serie, rec.num_ped, rec.nro, rec.num_det,
        CASE WHEN rec.solo_despacho = 'S' THEN '13' ELSE '01' END
      );
      COMMIT;
      v_cnt_init := v_cnt_init + 1;
    EXCEPTION WHEN OTHERS THEN
      v_cnt_err := v_cnt_err + 1;
    END;
  END LOOP;

  DBMS_OUTPUT.PUT_LINE('PASO A: '||v_cnt_init||' filas inicializadas ('||v_cnt_err||' errores).');
  v_cnt_err := 0;

  -- ════════════════════════════════════════════════════════════════
  -- PASO B — Actualizar COD_PASO_ACT y fechas reales históricas
  -- ════════════════════════════════════════════════════════════════
  FOR seg IN (
    SELECT s.id_seguim, s.serie, s.num_ped, s.nro, s.num_det,
           s.cod_paso_act, s.proceso, s.cantidad_orig, s.kg_despachados
    FROM   pln_seguimiento s
    JOIN   pedido p ON p.serie=s.serie AND p.num_ped=s.num_ped
    WHERE  s.estado = 'A'
      AND  p.estado IN ('0','5')
    ORDER BY s.num_ped, s.nro, s.num_det
  ) LOOP
    DECLARE
      v_nroprog     NUMBER;
      v_partida     NUMBER;      -- PARTIDA.NUMERO
      v_paso_nuevo  VARCHAR2(4) := seg.cod_paso_act;

      -- Fechas reales a poblar
      v_fch_prog    DATE;   -- PASO '02': ITEMPED_DET.FHC_PROG
      v_fch_prod    DATE;   -- PASO '03': PARTIDA.FECHA
      v_fch_tin_ini DATE;   -- PASO '06': MIN(TT_RPRODUC.FECHA_INI) via PARTIDA_MAS
      v_fch_tin_fin DATE;   -- PASO '07': MAX(TT_RPRODUC.FECHA_FIN) via PARTIDA_MAS
      v_fch_secado  DATE;   -- PASO '08': TT_RSECADO.FECHA_FIN
      v_fch_cc_ok   DATE;   -- PASO '09': CTCALIDAD_D.FCH_CONSULTA aprobado
      v_fch_cc_rech DATE;   -- PASO '9R': CTCALIDAD_D.FCH_CONSULTA rechazado
      v_fch_devan   DATE;   -- PASO '10': H_RPRODUC.FECHA_INI (BUG-4/5 FIX)
      v_fch_calidad DATE;   -- PASO '11': REVISADO_G.NVL(FCH_FIN_REVISA,A_ADFECHA)
      v_fch_alm_pt  DATE;   -- PASO '12': LOTES.FECHA primer ingreso PT
      v_fch_desp    DATE;   -- PASO '14': LOTES.FEC_SALIDA primer despacho

      -- KG acumulados
      v_kg_prod     NUMBER(12,4) := 0;
      v_kg_en_tin   NUMBER(12,4) := 0;  -- BUG-6 FIX: KG_EN_TIN
      v_kg_alm_pt   NUMBER(12,4) := 0;
      v_kg_desp_tot NUMBER(12,4) := 0;
      v_kg_pend     NUMBER(12,4);

      v_estado      VARCHAR2(1)  := 'A';
      v_ind_repr    VARCHAR2(1)  := 'N';

      -- Para chequeo de baños TT via PARTIDA_MAS (BUG-1 FIX)
      v_cnt_pm_total NUMBER := 0;  -- Total banos en PARTIDA_MAS
      v_cnt_tt_ok    NUMBER := 0;  -- Banos completados en TT_RPRODUC
    BEGIN
      -- ── A: Obtener NROPROG ── prioridad: el que ya tiene PARTIDA (más avanzado) ──
      -- BUG #45 FIX: SELECT INTO fallaba con TOO_MANY_ROWS cuando ITEMPED_DET
      --              tenía varias filas para el mismo (SERIE,NUM_PED,NRO,NUM_DET).
      BEGIN
        SELECT MAX(id.nroprog) INTO v_nroprog
        FROM   itemped_det id
        WHERE  id.serie=seg.serie AND id.num_ped=seg.num_ped
          AND  id.nro=seg.nro    AND id.num_det=seg.num_det
          AND  id.nroprog IS NOT NULL
          AND  EXISTS (SELECT 1 FROM partida p WHERE p.nroprog=id.nroprog);
      EXCEPTION WHEN OTHERS THEN NULL;
      END;
      -- Prioridad 2: cualquier NROPROG asignado, el más reciente (si no hay PARTIDA todavía)
      IF v_nroprog IS NULL THEN
        BEGIN
          SELECT MAX(nroprog) INTO v_nroprog
          FROM   itemped_det
          WHERE  serie=seg.serie AND num_ped=seg.num_ped
            AND  nro=seg.nro    AND num_det=seg.num_det
            AND  nroprog IS NOT NULL;
        EXCEPTION WHEN OTHERS THEN NULL;
        END;
      END IF;

      IF v_nroprog IS NOT NULL THEN
        v_paso_nuevo := '02';

        -- FCH_REAL_PROGRAMADO: FHC_PROG (fecha de programación de producción)
        BEGIN
          SELECT MAX(fhc_prog) INTO v_fch_prog
          FROM   itemped_det
          WHERE  serie=seg.serie AND num_ped=seg.num_ped
            AND  nro=seg.nro    AND num_det=seg.num_det;
        EXCEPTION WHEN OTHERS THEN NULL;
        END;

        -- ── B: Buscar PARTIDA via NROPROG ─────────────────────
        BEGIN
          SELECT numero, TRUNC(fecha)
          INTO   v_partida, v_fch_prod
          FROM   partida
          WHERE  nroprog = v_nroprog AND ROWNUM = 1;
          v_paso_nuevo := '03';
        EXCEPTION WHEN OTHERS THEN NULL;
        END;

        IF v_partida IS NOT NULL THEN
          -- KG_PRODUCIDOS: PESO_NETO de la partida
          BEGIN
            SELECT NVL(peso_neto, 0) INTO v_kg_prod
            FROM   partida WHERE numero = v_partida;
          EXCEPTION WHEN OTHERS THEN NULL;
          END;

          -- ── C: Laboratorio (PASO '04') ── L_VALIDA_RECETA ────
          -- Solo detectamos si existe; usamos A_ADFECHA como fecha (puede ser NULL)
          DECLARE v_existe_lab NUMBER := 0;
          BEGIN
            -- BUG #46 FIX: buscar lab en TODOS los NROPROGs del ítem (no solo v_nroprog)
            --              y aceptar estado '4' (aprobado directo) además de '3'
            SELECT COUNT(*) INTO v_existe_lab
            FROM   l_valida_receta lv
            WHERE  lv.nroprog IN (
                     SELECT id2.nroprog FROM itemped_det id2
                     WHERE  id2.serie=seg.serie AND id2.num_ped=seg.num_ped
                       AND  id2.nro=seg.nro    AND id2.num_det=seg.num_det
                   )
              AND  lv.estado IN ('3','4') AND ROWNUM = 1;
            -- PASO '04' tiene ORDEN=4 > ORDEN de '03'; avanzar si lab ya aprobó
            -- Solo marcar si es el paso más avanzado disponible
            IF v_existe_lab > 0 AND f_orden('04') > f_orden(v_paso_nuevo) THEN
              v_paso_nuevo := '04';
            END IF;
          EXCEPTION WHEN OTHERS THEN NULL;
          END;

          -- ── D: Ingreso Tintorería (PASO '06') ─────────────────
          -- BUG-1 FIX: Usar PARTIDA_MAS → TT_RPRODUC (sistema IR 2021+)
          -- Para partidas modernas: PARTIDA.NUMERO → PARTIDA_MAS.PARTIDA
          --   → PARTIDA_MAS.NUMERO → TT_RPRODUC.RECETA (TIPODOC='IR')
          BEGIN
            SELECT COUNT(DISTINCT pm.numero),
                   SUM(CASE WHEN r.estado = '3' THEN 1 ELSE 0 END),
                   MIN(CASE WHEN r.fecha_ini IS NOT NULL THEN r.fecha_ini END),
                   MAX(CASE WHEN r.estado = '3' THEN r.fecha_fin END)
            INTO   v_cnt_pm_total, v_cnt_tt_ok, v_fch_tin_ini, v_fch_tin_fin
            FROM   partida_mas pm
            LEFT JOIN tt_rproduc r
              ON  r.receta  = pm.numero
              AND r.tipodoc = 'IR'
            WHERE  pm.partida    = v_partida
              AND  pm.tp_transac = 'IR';

            IF NVL(v_cnt_tt_ok, 0) > 0
               AND f_orden('06') > f_orden(v_paso_nuevo) THEN
              v_paso_nuevo := '06';
              -- v_fch_tin_ini ya seteado arriba (fecha real, no proxy)
            END IF;
          EXCEPTION WHEN OTHERS THEN NULL;
          END;

          -- ── E: Tenido Completo (PASO '07') ────────────────────
          -- BUG-1 FIX: datos ya calculados en bloque D
          BEGIN
            IF NVL(v_cnt_pm_total, 0) > 0
               AND NVL(v_cnt_tt_ok, 0) >= NVL(v_cnt_pm_total, 0)
               AND f_orden('07') > f_orden(v_paso_nuevo) THEN
              v_paso_nuevo := '07';
              -- v_fch_tin_fin ya seteado en bloque D (fecha real, no proxy)
            END IF;
          EXCEPTION WHEN OTHERS THEN NULL;
          END;

          -- ── F: Secado (PASO '08') ─────────────────────────────
          BEGIN
            SELECT MAX(TRUNC(FECHA_FIN)) INTO v_fch_secado
            FROM   tt_rsecado
            WHERE  guia = v_partida;
            IF v_fch_secado IS NOT NULL AND f_orden('08') > f_orden(v_paso_nuevo) THEN
              v_paso_nuevo := '08';
            END IF;
          EXCEPTION WHEN OTHERS THEN NULL;
          END;

          -- ── G: CC Tintorería — PASO '09' o '9R' ──────────────
          -- Usa CTCALIDAD_D.GUIA = PARTIDA.NUMERO (columna GUIA confirmada)
          -- El último resultado con EST_EVALUACION='32' determina si aprobado o rechazado
          DECLARE
            v_resultado CTCALIDAD_D.RESULTADO%TYPE;
            v_fch_cc    DATE;
          BEGIN
            SELECT resultado, fch_cc INTO v_resultado, v_fch_cc FROM (
              SELECT resultado,
                     TRUNC(MAX(fch_consulta)) AS fch_cc
              FROM   ctcalidad_d
              WHERE  guia = v_partida
                AND  est_evaluacion = '32'
              GROUP  BY resultado
              ORDER  BY MAX(fch_consulta) DESC
            ) WHERE ROWNUM = 1;

            IF v_resultado IN ('01','21','29')          -- BUG-B FIX: '30'=RECHAZADO eliminado; solo aprobado/concesionado
               AND f_orden('09') > f_orden(v_paso_nuevo) THEN
              v_fch_cc_ok  := v_fch_cc;
              v_paso_nuevo := '09';
            ELSIF v_resultado IS NOT NULL
                  AND f_orden('9R') > f_orden(v_paso_nuevo) THEN
              v_fch_cc_rech := v_fch_cc;
              v_paso_nuevo  := '9R';
              v_ind_repr    := 'S';
            END IF;
          EXCEPTION WHEN OTHERS THEN NULL;
          END;

          -- ── G2: Devanado (PASO '10') ─── BUG-4/5 FIX ───────────
          -- H_RPRODUC: GUIA = PARTIDA.NUMERO, TP_MAQ NOT IN ('G')
          -- En sistema 2021+: H_RPRODUC es POST-CC (devanado)
          BEGIN
            SELECT MIN(TRUNC(fecha_ini)) INTO v_fch_devan
            FROM   h_rproduc
            WHERE  guia   = v_partida
              AND  tp_maq NOT IN ('G');
            IF v_fch_devan IS NOT NULL AND f_orden('10') > f_orden(v_paso_nuevo) THEN
              v_paso_nuevo := '10';
            END IF;
          EXCEPTION WHEN OTHERS THEN NULL;
          END;

          -- ── H: Revisado (PASO '11') ───────────────────────────
          -- FIX v2.1: REVISADO_G no tiene columna FECHA; usar NVL(FCH_FIN_REVISA, A_ADFECHA)
          BEGIN
            SELECT MAX(TRUNC(NVL(fch_fin_revisa, a_adfecha))) INTO v_fch_calidad
            FROM   revisado_g
            WHERE  guia = v_partida;
            IF v_fch_calidad IS NOT NULL AND f_orden('11') > f_orden(v_paso_nuevo) THEN
              v_paso_nuevo := '11';
            END IF;
          EXCEPTION WHEN OTHERS THEN NULL;
          END;

          -- ── I: Almacén PT (PASO '12') ─────────────────────────
          -- LOTES.SALDO = peso real en kg de los conos/rollos
          BEGIN
            SELECT MIN(TRUNC(fecha)), SUM(NVL(saldo, 0))
            INTO   v_fch_alm_pt, v_kg_alm_pt
            FROM   lotes
            WHERE  partida    = v_partida
              AND  tp_transac = '16'
              AND  cod_alm   IN ('03','07','22','30');
            IF v_fch_alm_pt IS NOT NULL AND f_orden('12') > f_orden(v_paso_nuevo) THEN
              v_paso_nuevo := '12';
            END IF;
          EXCEPTION WHEN OTHERS THEN NULL;
          END;

          -- ── J: Despacho (PASO '13'/'14') ─────────────────────
          -- LOTES.SALDO con FEC_SALIDA = kg despachados reales
          BEGIN
            SELECT MIN(TRUNC(fec_salida)), SUM(NVL(saldo, 0))
            INTO   v_fch_desp, v_kg_desp_tot
            FROM   lotes
            WHERE  partida    = v_partida
              AND  s_transac IN ('21','23')
              AND  fec_salida IS NOT NULL;
            IF v_fch_desp IS NOT NULL THEN
              v_kg_pend := GREATEST(seg.cantidad_orig - NVL(v_kg_desp_tot, 0), 0);
              IF NVL(v_kg_desp_tot, 0) >= seg.cantidad_orig * 0.95  -- PCT_CIERRE_DESPACHO=95%
                 AND f_orden('14') > f_orden(v_paso_nuevo) THEN
                v_paso_nuevo := '14';
                v_estado     := 'C';   -- cerrar ítem completamente despachado
              ELSIF f_orden('13') > f_orden(v_paso_nuevo) THEN
                v_paso_nuevo := '13';  -- despacho parcial: saldo pendiente
              END IF;
            ELSIF v_paso_nuevo = '12' THEN
              -- En PT pero sin despacho registrado: avanzar a '13'
              v_paso_nuevo := '13';
            END IF;
          EXCEPTION WHEN OTHERS THEN NULL;
          END;

        END IF; -- v_partida IS NOT NULL
      END IF; -- v_nroprog IS NOT NULL

      -- KG_PENDIENTES final
      v_kg_pend := NVL(v_kg_pend, GREATEST(seg.cantidad_orig - NVL(v_kg_desp_tot, 0), 0));

      -- ── Actualizar solo si el paso calculado es mayor al actual ──
      -- BUG-6 FIX: KG_EN_TIN = KG_PROD cuando PASO >= '06'
      IF f_orden(v_paso_nuevo) >= f_orden('06') THEN
        v_kg_en_tin := v_kg_prod;
      END IF;

      IF f_orden(v_paso_nuevo) > f_orden(seg.cod_paso_act) THEN
        UPDATE pln_seguimiento SET
          cod_paso_act        = v_paso_nuevo,
          cod_paso_ant        = seg.cod_paso_act,
          -- Fechas reales: NVL protege valores ya escritos por los triggers
          fch_real_programado = NVL(fch_real_programado, v_fch_prog),
          -- v2.2 (26/05/2026): backfill FCH_APROBACION, FCH_PLANIF y USR_PLANIF
          fch_aprobacion      = NVL(fch_aprobacion, (SELECT p2.f_aprobacion
                                                      FROM pedido p2
                                                      WHERE p2.serie=seg.serie AND p2.num_ped=seg.num_ped)),
          fch_planif          = NVL(fch_planif, v_fch_prog),
          usr_planif          = NVL(usr_planif, (SELECT MIN(id2.a_aduser)   -- BUG #47: MIN evita TOO_MANY_ROWS si hay NROPROGs duplicados
                                                  FROM itemped_det id2
                                                  WHERE id2.serie=seg.serie AND id2.num_ped=seg.num_ped
                                                    AND id2.nro=seg.nro    AND id2.num_det=seg.num_det
                                                    AND id2.nroprog IS NOT NULL)),
          fch_real_produccion = NVL(fch_real_produccion, v_fch_prod),
          fch_real_partida    = NVL(fch_real_partida,    v_fch_prod),  -- fallback PARTIDA.FECHA
          fch_real_tin_ini    = NVL(fch_real_tin_ini,    v_fch_tin_ini),  -- BUG-7 FIX: real
          fch_real_tin_fin    = NVL(fch_real_tin_fin,    v_fch_tin_fin),  -- BUG-7 FIX: real
          fch_real_secado     = NVL(fch_real_secado,     v_fch_secado),
          fch_real_cc_tinto   = NVL(fch_real_cc_tinto,   v_fch_cc_ok),
          fch_real_cc_rechazo = NVL(fch_real_cc_rechazo, v_fch_cc_rech),
          fch_real_devanado   = NVL(fch_real_devanado,   v_fch_devan),    -- BUG-4 FIX
          fch_real_calidad    = NVL(fch_real_calidad,    v_fch_calidad),
          fch_real_alm_pt     = NVL(fch_real_alm_pt,     v_fch_alm_pt),
          fch_real_despacho   = NVL(fch_real_despacho,   v_fch_desp),
          -- KG acumulados (MAX protege frente a dobles ejecuciones)
          kg_producidos       = GREATEST(kg_producidos, NVL(v_kg_prod,    0)),
          kg_en_tin           = GREATEST(kg_en_tin,     NVL(v_kg_en_tin,  0)),  -- BUG-6 FIX
          kg_en_alm_pt        = GREATEST(kg_en_alm_pt,  NVL(v_kg_alm_pt, 0)),
          kg_despachados      = GREATEST(kg_despachados, NVL(v_kg_desp_tot, 0)),
          kg_pendientes       = v_kg_pend,
          ind_reproceso       = CASE WHEN v_ind_repr = 'S' THEN 'S' ELSE ind_reproceso END,
          estado              = v_estado,
          a_mduser            = USER,
          a_mdfecha           = SYSDATE
        WHERE id_seguim = seg.id_seguim;
        v_cnt_upd := v_cnt_upd + 1;
      ELSE
        v_cnt_skip := v_cnt_skip + 1;
      END IF;
      COMMIT;
    EXCEPTION WHEN OTHERS THEN
      v_cnt_err := v_cnt_err + 1;
      ROLLBACK;
    END;
  END LOOP;

  DBMS_OUTPUT.PUT_LINE('PASO B: '||v_cnt_upd||' actualizados, '||
                       v_cnt_skip||' sin cambio, '||v_cnt_err||' errores.');

  -- ════════════════════════════════════════════════════════════════
  -- PASO C — Backfill FCH_APROBACION (v2.2, 26/05/2026)
  -- ════════════════════════════════════════════════════════════════
  -- El PASO B solo actualiza si el paso avanza; los ítems ya en paso correcto
  -- nunca entran al UPDATE, dejando FCH_APROBACION = NULL aunque PEDIDO.F_APROBACION
  -- exista. Este bloque lo cubre de forma independiente e idempotente.
  DECLARE
    v_cnt_apro PLS_INTEGER := 0;
  BEGIN
    UPDATE pln_seguimiento s
    SET    s.fch_aprobacion = (
               SELECT p.f_aprobacion
               FROM   pedido p
               WHERE  p.serie   = s.serie
                 AND  p.num_ped = s.num_ped
           ),
           s.a_mdfecha = SYSDATE,
           s.a_mduser  = USER
    WHERE  s.estado          = 'A'
      AND  s.fch_aprobacion   IS NULL
      AND  EXISTS (
               SELECT 1 FROM pedido p2
               WHERE  p2.serie   = s.serie
                 AND  p2.num_ped = s.num_ped
                 AND  p2.f_aprobacion IS NOT NULL
           );
    v_cnt_apro := SQL%ROWCOUNT;
    COMMIT;
    DBMS_OUTPUT.PUT_LINE('PASO C: '||v_cnt_apro||' FCH_APROBACION backfilled.');
  EXCEPTION WHEN OTHERS THEN
    ROLLBACK;
    DBMS_OUTPUT.PUT_LINE('PASO C ERROR: '||SQLERRM);
  END;

  -- ════════════════════════════════════════════════════════════════
  -- PASO D — Backfill FCH_REG_ENTREGA, FCH_ENTREGA_ORI, FCH_ENTREGA_COMP (v2.3, 26/05/2026)
  -- ════════════════════════════════════════════════════════════════
  -- Rellena los campos de compromiso por artículo desde ITEMPED_DET.
  -- También recalcula FCH_ENTREGA_COMP con la nueva prioridad por ítem.
  -- Es idempotente: NVL preserva valores ya correctos.
  DECLARE
    v_cnt_ent PLS_INTEGER := 0;
  BEGIN
    UPDATE pln_seguimiento s
    SET    s.fch_reg_entrega  = NVL(s.fch_reg_entrega,
                                 (SELECT MAX(id.fch_reg_entrega)
                                  FROM itemped_det id
                                  WHERE id.serie=s.serie AND id.num_ped=s.num_ped
                                    AND id.nro=s.nro AND id.num_det=s.num_det)),
           s.fch_entrega_ori  = NVL(s.fch_entrega_ori,
                                 (SELECT MAX(id.fch_entrega_ori)
                                  FROM itemped_det id
                                  WHERE id.serie=s.serie AND id.num_ped=s.num_ped
                                    AND id.nro=s.nro AND id.num_det=s.num_det)),
           s.fch_entrega_comp = NVL(
               (SELECT MAX(id.fhc_entrega) FROM itemped_det id
                WHERE id.serie=s.serie AND id.num_ped=s.num_ped
                  AND id.nro=s.nro AND id.num_det=s.num_det),
               NVL(
                 (SELECT MAX(id.fch_entrega_ori) FROM itemped_det id
                  WHERE id.serie=s.serie AND id.num_ped=s.num_ped
                    AND id.nro=s.nro AND id.num_det=s.num_det),
                 NVL(
                   (SELECT MAX(id.fch_reg_entrega) FROM itemped_det id
                    WHERE id.serie=s.serie AND id.num_ped=s.num_ped
                      AND id.nro=s.nro AND id.num_det=s.num_det),
                   NVL(
                     (SELECT ip.f_maxped FROM itemped ip
                      WHERE ip.serie=s.serie AND ip.num_ped=s.num_ped AND ip.nro=s.nro),
                     (SELECT pe.fecha + NVL(pe.plazo_entrega,30)
                      FROM pedido pe
                      WHERE pe.serie=s.serie AND pe.num_ped=s.num_ped)
                   )
                 )
               )
           ),
           s.a_mdfecha = SYSDATE,
           s.a_mduser  = USER
    WHERE  s.estado = 'A';
    v_cnt_ent := SQL%ROWCOUNT;
    COMMIT;
    DBMS_OUTPUT.PUT_LINE('PASO D: '||v_cnt_ent||' fechas compromiso por artículo actualizadas.');
  EXCEPTION WHEN OTHERS THEN
    ROLLBACK;
    DBMS_OUTPUT.PUT_LINE('PASO D ERROR: '||SQLERRM);
  END;

  -- ── PASO E — Backfill IND_RETRASO + DIAS_RETRASO (v2.3, 26/05/2026) ───────────────────────
  -- La migración §11 crea los registros con IND_RETRASO='N' / DIAS_RETRASO=0 por defecto.
  -- SP_PLN_AVANZA_PASO actualiza estos campos en tiempo real para nuevas transacciones.
  -- Este paso retroalimenta el cálculo para todos los ítems activos ya migrados.
  DECLARE
    v_cnt_ret  NUMBER := 0;
  BEGIN
    UPDATE pln_seguimiento
    SET
      ind_retraso  = CASE WHEN fch_entrega_comp IS NOT NULL
                               AND TRUNC(SYSDATE) > TRUNC(fch_entrega_comp)
                          THEN 'S' ELSE 'N' END,
      dias_retraso = CASE WHEN fch_entrega_comp IS NOT NULL
                               AND TRUNC(SYSDATE) > TRUNC(fch_entrega_comp)
                          THEN TRUNC(SYSDATE) - TRUNC(fch_entrega_comp) ELSE 0 END,
      a_mdfecha = SYSDATE,
      a_mduser  = USER
    WHERE estado = 'A';
    v_cnt_ret := SQL%ROWCOUNT;
    COMMIT;
    DBMS_OUTPUT.PUT_LINE('PASO E: '||v_cnt_ret||' ítems actualizados (IND_RETRASO/DIAS_RETRASO).');
  EXCEPTION WHEN OTHERS THEN
    ROLLBACK;
    DBMS_OUTPUT.PUT_LINE('PASO E ERROR: '||SQLERRM);
  END;

  DBMS_OUTPUT.PUT_LINE('─────────────────────────────────────────────────────');
  DBMS_OUTPUT.PUT_LINE('Migración histórica completada.');
  DBMS_OUTPUT.PUT_LINE('  Filas inicializadas (A): '||v_cnt_init);
  DBMS_OUTPUT.PUT_LINE('  Pasos avanzados     (B): '||v_cnt_upd);
  DBMS_OUTPUT.PUT_LINE('Recalcule alertas:');
  DBMS_OUTPUT.PUT_LINE('  BEGIN PKG_PLN.SP_PLN_GENERA_ALERTAS; COMMIT; END;');
  DBMS_OUTPUT.PUT_LINE('Recalcule carga de máquinas:');
  DBMS_OUTPUT.PUT_LINE('  BEGIN PKG_PLN.SP_PLN_CARGA_DIARIA_REFRESH; COMMIT; END;');
EXCEPTION WHEN OTHERS THEN
  DBMS_OUTPUT.PUT_LINE('ERROR FATAL §11: '||SQLERRM);
  ROLLBACK;
END;
/
