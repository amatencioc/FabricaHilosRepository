/* ============================================================
   PKG_PLN.sql
   ============================================================
   MODULO     : PLN_ - Planeamiento, Seguimiento y Control de Planta
   SISTEMA    : SIG - Fabricacion de Hilos (Hilanderia y Tintoreria)
   BD         : Oracle 11.2.0.4 - Esquema SIG (multi-empresa: SIG / ARBONA / SOLSA)
   CREADO     : 18/05/2026
   ULTIMA MOD : 20/05/2026
   VERSION    : 43 correcciones acumuladas (BUGs #1-#40 + Fix A-F)
   ============================================================

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
     01/05/2026  L_VALIDA_RECETA UPDATE ESTADO='3'  -> PASO '05' (LAB)
     08/05/2026  PARTIDA 158938 INSERT              -> PASO '03' (hilo entra produccion)
     18/05/2026  H_RPRODUC INSERT GUIA=158938       -> PASO '04' (produccion finalizada)
     18/05/2026  LOTES UPDATE S_TRANSAC='21'        -> PASO '14' (despachado)

   HALLAZGO CRITICO (origen BUG #40):
   El laboratorio aprueba la receta ANTES que la PARTIDA exista.
   La PARTIDA se crea ANTES que H_RPRODUC registre la produccion.
   Orden real cronologico: '05' -> '03' -> '04' (NO '03' -> '04' -> '05').

   -- FLUJO DE PRODUCCION --- MAQUINA DE ESTADOS (16 PASOS) -----
   Ciclo sin reproceso: 12-18 dias habiles.
   Ciclo con reproceso ('9R'): 14-22 dias habiles.

   ORDEN | PASO  | NOMBRE               | TABLA QUE LO ACTIVA                    | AREA
   ------+-------+----------------------+----------------------------------------+--------------
      1  | '01'  | Pedido Registrado    | ITEMPED INSERT                         | Ventas
      2  | '02'  | Planificado          | ITEMPED_DET UPDATE (NROPROG asignado)  | Planeamiento
      3  | '05'  | Laboratorio          | L_VALIDA_RECETA UPDATE ESTADO='3'      | Laboratorio
      4  | '03'  | En Hilanderia        | PARTIDA INSERT (NROPROG NOT NULL)      | Hilanderia
      5  | '04'  | Lote Disponible      | H_RPRODUC INSERT (GUIA NOT NULL)       | Hilanderia
      6  | '06'  | En Tintoreria        | PARTIDA UPDATE SITU_PART='R001'        | Tintoreria
      7  | '07'  | Tenido Completo      | TT_RPRODUC UPDATE ESTADO='3' (todos)   | Tintoreria
      8  | '08'  | Secado               | TT_RSECADO INSERT                      | Tintoreria
      9  | '09'  | CC TT Aprobado       | CTCALIDAD_D EST_EVAL='32' RES aprobado | Calidad
     10  | '09B' | Gaseado (*)          | H_RPRODUC INSERT TP_MAQ='G'            | Acabados
     11  | '9R'  | Reproceso CC         | CTCALIDAD_D EST_EVAL='32' RES='30'     | Tintoreria
     12  | '10'  | Devanado             | REVISADO_G INSERT (GUIA NOT NULL)      | Devanado
     13  | '11'  | Revisado             | REVISADO_D INSERT (APROBADO > 0)       | Calidad
     14  | '12'  | Ingresado Alm PT     | LOTES INSERT TP='16' ALM IN(03,07,22,30)| Almacen PT
     15  | '13'  | Listo para Despacho  | (automatico: SP_PLN_AVANZA_PASO)       | Almacen PT
     16  | '14'  | Despachado/Cerrado   | LOTES UPDATE S_TRANSAC IN ('21','23')  | Despacho

   (*) PASO '09B' Gaseado: SOLO si PROCESO='24' (PEINADO GASEADO).
       Para PROCESO='01' (Cardado) y '20' (Peinado): flujo salta '09'->'10'.

   Flujo normal:
     [01]->[02]->[05]->[03]->[04]->[06]->[07]->[08]->[09]->[10]->[11]->[12]->[13]->[14]
   Con gaseado (PROCESO='24'):
     [01]->[02]->[05]->[03]->[04]->[06]->[07]->[08]->[09]->[09B]->[10]->[11]->[12]->[13]->[14]
   Reproceso:
     ...[09]->[9R]->[06]->[07]->[08]->[09]... (NRO_CICLO +1 en cada '9R')
   Stock directo (SOLO_DESPACHO='S'):
     [01]->[13] (sin produccion)

   NOTA: Los PASOS '06' y '07' NO disparan para PARTIDA.NUMERO >= 158000
   (sistema nuevo de tintoreria - no usa TT_RPRODUC ni SITU_PART='R001').
   El flujo para partidas nuevas salta: ...[04]->[08]->[09]...
   Gap de diseno conocido, no es un bug del modulo PLN_.

   Porcentaje de avance (PctAvance en C#):
     '01'->6%  '02'->13%  '03'->19%  '04'->25%  '05'->31%  '06'->38%
     '07'->44% '08'->50%  '09'->56%  '09B'->62% '10'->69%  '11'->75%
     '12'->81% '13'->88%  '14'->100%

   Colores UI (PLN_ESTADO_CODIGO.COLOR_UI):
     '01'->#6c757d  '02'->#0d6efd  '05'->#6610f2  '03'->#0dcaf0  '04'->#17a2b8
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
     parr.9  Jobs         JOB_PLN_ALERTAS (cada hora) + JOB_PLN_CARGA (23:30 diario)
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

   SP_PLN_AVANZA_PASO(serie, ped, nro, det, nuevo_paso, tabla, id, kg, obs)
     Motor central del modulo. Realiza en una sola operacion:
       - Guard anti-retroceso: si ORDEN_PASO_NEW < ORDEN_PASO_ACT -> RETURN
         (excepciones: despacho parcial->'13', reinicio reproceso desde '9R')
       - Actualiza COD_PASO_ACT / COD_PASO_ANT
       - Actualiza la fecha real (FCH_REAL_*) correspondiente al nuevo paso
       - Acumula KGs: KG_PRODUCIDOS solo en PASO '03' (PARTIDA.PESO_NETO) [BUG #40]
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
     Ejecutado por JOB_PLN_CARGA a las 23:30. Hace COMMIT propio.

   SP_PLN_CIERRE_ITEM(id_seguim, motivo, usuario)
     Cierre manual autorizado: ESTADO='A' -> 'C'. Inserta evento 'CI'.
     Solo para correcciones operativas supervisadas.

   SP_PLN_REPROGRAMAR(serie, ped, nro, det, nueva_fch_desp, motivo, usuario)
     Actualiza FCH_EST_DESPACHO. Recalcula IND_RETRASO/DIAS_RETRASO.
     Guarda en PLN_FECHAS_ESTIMADAS (MOTIVO_RECALCULO='REP').
     Inserta evento TIPO_EVENTO='RE' en PLN_LOG_EVENTOS.

   -- TRIGGERS (13; TODOS con EXCEPTION WHEN OTHERS THEN NULL) --
   TIA_PLN_FROM_ITEMPED          -> PASO '01'       AFTER INSERT ITEMPED
   TUA_PLN_FROM_ITEMPED_DET      -> PASO '02'       AFTER UPDATE ITEMPED_DET (NROPROG asignado)
   TUA_PLN_FROM_L_VALIDA_RECETA  -> PASO '05'       AFTER UPDATE L_VALIDA_RECETA (ESTADO='3')
   TIA_PLN_FROM_PARTIDA          -> PASO '03'       AFTER INSERT PARTIDA (NROPROG NOT NULL)
   TIA_PLN_FROM_H_RPRODUC        -> PASO '04'/'09B' AFTER INSERT H_RPRODUC (GUIA NOT NULL)
                                                     TP_MAQ='G' -> '09B'; resto -> '04'
   TUA_PLN_FROM_PARTIDA          -> PASO '06'       AFTER UPDATE PARTIDA (SITU_PART='R001')
   TUA_PLN_FROM_TT_RPRODUC       -> PASO '07'       AFTER UPDATE TT_RPRODUC (ESTADO='3')
                                                     Solo avanza cuando TODOS los banos OK
   TIA_PLN_FROM_TT_RSECADO       -> PASO '08'       AFTER INSERT TT_RSECADO
   TUA_PLN_FROM_CTCALIDAD        -> PASO '09'/'9R'  AFTER UPDATE CTCALIDAD_D (EST_EVAL='32')
                                                     RESULTADO IN ('01','21','29') -> '09'
                                                     RESULTADO = '30' -> '9R'
   TIA_PLN_FROM_REVISADO_G       -> PASO '10'       AFTER INSERT REVISADO_G (GUIA NOT NULL)
   TIA_PLN_FROM_REVISADO         -> PASO '11'       AFTER INSERT REVISADO_D (APROBADO > 0)
   TIA_PLN_FROM_LOTES_PT         -> PASO '12'       AFTER INSERT LOTES (TP='16', ALM IN '03','07','22','30')
   TUA_PLN_FROM_LOTES_DESPACHO   -> PASO '14'       AFTER UPDATE LOTES (S_TRANSAC IN '21','23')

   PASO '13' calculado internamente por SP_PLN_AVANZA_PASO (despacho parcial).

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
   'STN'      | 'C'   | PASO '05' paso FCH_EST_TIN_INI sin entrar a TT
   'QCF'      | 'C'   | PASO '9R' (CC rechazado)
   Estado: 'A'=Activa  'R'=Resuelta  'I'=Ignorada
   NivelColor C#: 'C'->"danger" | 'A'->"warning" | 'M'->"info" | 'B'->"secondary"

   -- TIPOS DE EVENTO (PLN_LOG_EVENTOS.TIPO_EVENTO) --------------
   'AV' = Avance automatico de paso (trigger)
   'RE' = Reprogramacion de fecha (manual)
   'AL' = Generacion de alerta
   'CI' = Cierre manual de item

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
   (vacio)        -> En hilanderia / disponible    (PASOS '03'-'04')
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

   R8. FCH_ENTREGA_COMP (fecha compromiso):
       Prioridad: ITEMPED.F_MAXPED -> PEDIDO.FECHA + PEDIDO.PLAZO_ENTREGA.
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

-- §0.2  Triggers (sobre tablas legacy — deben caer antes que el paquete)
PROMPT >>> §0.2 Eliminando triggers PLN_...
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TIA_PLN_FROM_ITEMPED';         EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TUA_PLN_FROM_ITEMPED_DET';     EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TIA_PLN_FROM_H_RPRODUC';       EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TIA_PLN_FROM_PARTIDA';         EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TUA_PLN_FROM_L_VALIDA_RECETA'; EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TUA_PLN_FROM_PARTIDA';         EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TUA_PLN_FROM_TT_RPRODUC';      EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TIA_PLN_FROM_TT_RSECADO';      EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TUA_PLN_FROM_CTCALIDAD';       EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TIA_PLN_FROM_REVISADO_G';      EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TIA_PLN_FROM_REVISADO';        EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TIA_PLN_FROM_LOTES_PT';        EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TUA_PLN_FROM_LOTES_DESPACHO';  EXCEPTION WHEN OTHERS THEN NULL; END;
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
--   KG_PRODUCIDOS  : solo PASO '03' (En Hilandería — PARTIDA creada) [BUG #40]
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
  FCH_PEDIDO        DATE            NOT NULL,
  FCH_ENTREGA_COMP  DATE,

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
  FCH_REAL_PRODUCCION DATE,   -- PASO '03': inicio hilandería
  FCH_REAL_PARTIDA    DATE,   -- PASO '04': lote creado
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

  -- Referencias a objetos del flujo
  NUM_PROGRAMA      NUMBER(8),    -- H_PROGRAMACION.NUMERO
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
-- KG_CANTIDAD: los kg involucrados en el evento (útil para PASO '04','12','14')
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
-- Refrescada por JOB_PLN_CARGA (23:30 diario) con ventana de 30 días.
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
CREATE INDEX IX_ING_RECETAS_G_NUM ON ING_RECETAS_G (NUMERO);


-- ============================================================
-- §4  DATOS CATÁLOGO
-- ============================================================
-- Datos de referencia del módulo PLN_.
-- Ejecutar en este orden:
--   1. INSERT INTO PLN_PARAM      → 9 parámetros configurables
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
COMMIT;

-- ── §4.2  Catálogo de pasos del flujo (PLN_ESTADO_CODIGO) ───────
-- Columnas: COD_PASO, NOMBRE_PASO, DESCRIPCION, ORDEN_PASO,
--            TABLA_ORIGEN, ES_FINAL, COLOR_UI
-- Orden de avance REAL (confirmado 19/05/2026): 01→02→05→03→04→06→07→08→09→(09B)→10→11→12→13→14
-- Orden de reproceso:     ...→09→9R→06→07→08→09→... (ciclo adicional)
-- BUG #40 FIX: '05' LAB (ORDEN=3) ocurre ANTES que '03' PARTIDA (ORDEN=4) en producción real.
--   L_VALIDA_RECETA se aprueba → luego PARTIDA se crea → luego H_RPRODUC finishing.
-- Solo PASO '14' tiene ES_FINAL='S'. El trigger TUA_PLN_FROM_LOTES_DESPACHO
-- es el único que puede llevar un ítem a ESTADO='C' en PLN_SEGUIMIENTO.
-- Los colores son los hex de Bootstrap 5 + paleta corporativa:
INSERT INTO PLN_ESTADO_CODIGO VALUES ('01','Pedido Registrado',        'Ítem de pedido creado en ITEMPED',                                              1,'ITEMPED',          'N','#6c757d');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('02','Planificado',              'Etapa asignada en ITEMPED_DET (NROPROG asignado)',                               2,'ITEMPED_DET',      'N','#0d6efd');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('03','En Hilandería',            'PARTIDA INSERT - lote ingresado a produccion hilanderia (BUG #40)',              4,'PARTIDA',          'N','#0dcaf0');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('04','Lote Disponible',          'H_RPRODUC INSERT - produccion finalizada, lote disponible (BUG #40)',            5,'H_RPRODUC',        'N','#17a2b8');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('05','Laboratorio',              'L_VALIDA_RECETA UPDATE ESTADO=3 - receta validada',                              3,'L_VALIDA_RECETA',  'N','#6610f2');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('06','En Tintorería',            'PARTIDA UPDATE SITU_PART=R001 - ingreso a TT',                                   6,'PARTIDA',          'N','#6f42c1');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('07','Tenido Completo',          'TT_RPRODUC UPDATE ESTADO=3 - TODOS los banios completos',                         7,'TT_RPRODUC',       'N','#d63384');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('08','Secado',                   'TT_RSECADO INSERT - secado registrado',                                          8,'TT_RSECADO',       'N','#20c997');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('09','CC TT Aprobado',           'CTCALIDAD_D RESULTADO IN (01,21,29,30) - aprobado/concesionado (BUG #28)',       9,'CTCALIDAD_D',      'N','#fd7e14');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('09B','Gaseado',                 'H_RPRODUC INSERT TP_MAQ=G GUIA NOT NULL - gaseado (solo PROCESO=24, BUG #31)',  10,'H_RPRODUC',        'N','#ffd700');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('9R','CC TT Rechazado/Reproceso','CTCALIDAD_D RESULTADO NOT IN (01,21,29,30) - rechazado, requiere reproceso',      11,'CTCALIDAD_D',      'N','#dc3545');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('10','Devanado',                 'REVISADO_G INSERT GUIA=PARTIDA - inicio devanado/rewinding (BUG #32)',            12,'REVISADO_G',      'N','#ffc107');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('11','Revisado',                 'REVISADO_D INSERT APROBADO>0 - calidad final aprobada',                          13,'REVISADO_D',       'N','#0d6efd');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('12','Ingresado Almacén PT',     'LOTES INSERT COD_ALM IN (03,07,22,30) TP_TRANSAC=16 (BUG #33)',                  14,'LOTES',            'N','#198754');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('13','Listo para Despacho',      'Stock en almacén, saldo pendiente de despacho',                                  15,NULL,               'N','#20c997');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('14','Despachado/Cerrado',       'LOTES UPDATE S_TRANSAC IN (21,23) - despacho completo',                          16,'LOTES',            'S','#198754');
COMMIT;

-- ── §4.3  BUG #40 FIX — Corrección ORDEN_PASO ya deployado en Oracle ─────────
-- Ejecutar UNA SOLA VEZ contra la BD de producción para corregir la tabla
-- PLN_ESTADO_CODIGO que fue deployada con ORDENes incorrectos (19/05/2026).
-- Orden real confirmado con pedido 88501 NRO=5:
--   L_VALIDA_RECETA aprobada (01/05) ANTES que PARTIDA creada (08/05)
--   PARTIDA creada (08/05) ANTES que H_RPRODUC finishing (18/05)
-- Por lo tanto: '05' ORDEN=3, '03' ORDEN=4, '04' ORDEN=5
UPDATE PLN_ESTADO_CODIGO SET ORDEN_PASO=3, DESCRIPCION='L_VALIDA_RECETA UPDATE ESTADO=3 - receta validada'
  WHERE COD_PASO='05';
UPDATE PLN_ESTADO_CODIGO SET ORDEN_PASO=4, DESCRIPCION='PARTIDA INSERT - lote ingresado a produccion hilanderia (BUG #40)'
  WHERE COD_PASO='03';
UPDATE PLN_ESTADO_CODIGO SET ORDEN_PASO=5, DESCRIPCION='H_RPRODUC INSERT - produccion finalizada, lote disponible (BUG #40)'
  WHERE COD_PASO='04';
COMMIT;


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
    p_observacion  IN VARCHAR2  DEFAULT NULL
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
  -- LÓGICA FCH_ENTREGA_COMP (corrección aplicada):
  --   Prioridad 1: ITEMPED.F_MAXPED (fecha máxima comprometida con cliente)
  --   Prioridad 2: PEDIDO.FECHA + NVL(PEDIDO.PLAZO_ENTREGA, 30)
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
    v_id         NUMBER;
    v_pedido     PEDIDO%ROWTYPE;
    v_item       ITEMPED%ROWTYPE;
    v_fch_entrega DATE;
    v_solo_desp   VARCHAR2(1) := 'N';
    v_cantidad    NUMBER(12,4);
    v_lote        VARCHAR2(20);
  BEGIN
    -- Leer cabecera
    SELECT * INTO v_pedido FROM PEDIDO  WHERE serie=p_serie AND num_ped=p_num_ped;
    SELECT * INTO v_item   FROM ITEMPED WHERE serie=p_serie AND num_ped=p_num_ped AND nro=p_nro;

    -- Fecha compromiso: prioridad F_MAXPED, luego plazo genérico
    v_fch_entrega := NVL(v_item.f_maxped, v_pedido.fecha + NVL(v_pedido.plazo_entrega, 30));

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
      WHERE serie=p_serie AND num_ped=p_num_ped AND nro=p_nro AND num_det=p_num_det;
    EXCEPTION WHEN NO_DATA_FOUND THEN
      v_lote := NULL;  -- ITEMPED no tiene LOTE; sin ITEMPED_DET el lote queda NULL
    END;

    SELECT PLN_SEQ_SEGUIM.NEXTVAL INTO v_id FROM DUAL;

    INSERT INTO PLN_SEGUIMIENTO (
      ID_SEGUIM, SERIE, NUM_PED, NRO, NUM_DET,
      COD_CLIENTE, COD_ART, COLOR, TITULO, PROCESO, LOTE,
      CANTIDAD_ORIG, SOLO_DESPACHO,
      COD_PASO_ACT, NRO_CICLO, FCH_PEDIDO, FCH_ENTREGA_COMP,
      KG_PENDIENTES, IND_RETRASO, IND_URGENTE, ESTADO,
      A_ADUSER, A_ADFECHA
    ) VALUES (
      v_id, p_serie, p_num_ped, p_nro, p_num_det,
      v_pedido.cod_cliente, v_item.cod_art, v_item.color,
      v_item.titulo, v_item.proceso, v_lote,
      v_cantidad, v_solo_desp,
      p_paso_ini, 1, v_pedido.fecha, v_fch_entrega,
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
  --   · FCH_REAL_PARTIDA  → solo PASO '04' (Lote Disponible, hilo crudo producido)
  --   · FCH_REAL_TIN_FIN  → solo PASO '07' (Tenido completo, NO con SECADO '08')
  --   · KG_PRODUCIDOS     → solo se SUMA en PASO '03' (no en '04' ni '05') [BUG #40]
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
    p_observacion  IN VARCHAR2  DEFAULT NULL
  ) AS
    v_seg        PLN_SEGUIMIENTO%ROWTYPE;
    v_id_evt     NUMBER;
    v_nuevo_kg   NUMBER;    -- KG_DESPACHADOS proyectado (con old value)
    v_orden_act  NUMBER := 0;  -- Orden del paso actual  (protección anti-retroceso)
    v_orden_new  NUMBER := 0;  -- Orden del paso entrante
  BEGIN
    -- ESTADO='A': ignora ítems cerrados (C) o anulados (X) → NO_DATA_FOUND → EXCEPTION
    SELECT * INTO v_seg
    FROM PLN_SEGUIMIENTO
    WHERE serie=p_serie AND num_ped=p_num_ped AND nro=p_nro AND num_det=p_num_det
      AND ESTADO = 'A'
    FOR UPDATE;

    -- KG_DESPACHADOS proyectado (OLD + nuevo)
    v_nuevo_kg := v_seg.kg_despachados + NVL(p_kg_cantidad, 0);

    -- Protección anti-retroceso: un mismo trigger puede dispararse varias veces
    -- para el mismo lote (ej. múltiples H_RPRODUC o LOTES para la misma PARTIDA).
    -- Si el nuevo paso tiene ORDEN inferior al actual, se ignora sin retroceder.
    -- PASO '14' se excluye: su retroceso a '13' por despacho parcial es intencional.
    -- PASO '9R' se excluye: después de reproceso ('9R', ORDEN=11) el ciclo reinicia
    --   y TUA_PLN_FROM_PARTIDA intenta avanzar a '06' (ORDEN=6) → debe permitirse.
    --   Sin esta excepción, el ítem quedaría bloqueado en '9R' para siempre. (BUG #34)
    BEGIN
      SELECT ec.orden_paso INTO v_orden_act
      FROM pln_estado_codigo ec WHERE ec.cod_paso = v_seg.cod_paso_act;
    EXCEPTION WHEN NO_DATA_FOUND THEN v_orden_act := 0; END;
    BEGIN
      SELECT ec.orden_paso INTO v_orden_new
      FROM pln_estado_codigo ec WHERE ec.cod_paso = p_nuevo_paso;
    EXCEPTION WHEN NO_DATA_FOUND THEN v_orden_new := 0; END;
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
      COD_PASO_ACT        = CASE
                              WHEN p_nuevo_paso = '14' AND v_nuevo_kg < CANTIDAD_ORIG THEN '13'
                              ELSE p_nuevo_paso
                            END,
      -- ── Fechas reales por paso ──────────────────────────────
      -- ── Número de programa (BUG #41: NROPROG viene como p_id_origen en PASO '02') ──
      NUM_PROGRAMA        = CASE WHEN p_nuevo_paso='02' THEN p_id_origen ELSE NUM_PROGRAMA END,
      FCH_REAL_PROGRAMADO = CASE WHEN p_nuevo_paso='02' THEN SYSDATE ELSE FCH_REAL_PROGRAMADO END,
      FCH_REAL_PRODUCCION = CASE WHEN p_nuevo_paso='03' THEN SYSDATE ELSE FCH_REAL_PRODUCCION END,
      FCH_REAL_PARTIDA    = CASE WHEN p_nuevo_paso='04' THEN SYSDATE ELSE FCH_REAL_PARTIDA    END,
      FCH_REAL_TIN_INI    = CASE WHEN p_nuevo_paso='06' THEN SYSDATE
                                 WHEN p_nuevo_paso='9R' THEN NULL   ELSE FCH_REAL_TIN_INI    END,
      FCH_REAL_TIN_FIN    = CASE WHEN p_nuevo_paso='07' THEN SYSDATE
                                 WHEN p_nuevo_paso='9R' THEN NULL   ELSE FCH_REAL_TIN_FIN    END,
      FCH_REAL_SECADO     = CASE WHEN p_nuevo_paso='08' THEN SYSDATE
                                 WHEN p_nuevo_paso='9R' THEN NULL   ELSE FCH_REAL_SECADO     END,
      FCH_REAL_CC_TINTO   = CASE WHEN p_nuevo_paso='09' THEN SYSDATE ELSE FCH_REAL_CC_TINTO   END,
      FCH_REAL_CC_RECHAZO = CASE WHEN p_nuevo_paso='9R' THEN SYSDATE ELSE FCH_REAL_CC_RECHAZO END,
      FCH_REAL_DEVANADO   = CASE WHEN p_nuevo_paso='10' THEN SYSDATE ELSE FCH_REAL_DEVANADO   END,
      FCH_REAL_CALIDAD    = CASE WHEN p_nuevo_paso='11' THEN SYSDATE ELSE FCH_REAL_CALIDAD    END,
      FCH_REAL_ALM_PT     = CASE WHEN p_nuevo_paso='12' THEN SYSDATE ELSE FCH_REAL_ALM_PT     END,
      FCH_REAL_DESPACHO   = CASE WHEN p_nuevo_paso='14' AND v_nuevo_kg >= CANTIDAD_ORIG
                                    THEN SYSDATE ELSE FCH_REAL_DESPACHO END,
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
      ESTADO              = CASE WHEN p_nuevo_paso='14' AND v_nuevo_kg >= CANTIDAD_ORIG
                                    THEN 'C' ELSE ESTADO END,
      -- ── Indicadores reproceso / ciclo ──────────────────────
      IND_REPROCESO       = CASE WHEN p_nuevo_paso='9R' THEN 'S'
                                 WHEN p_nuevo_paso='09' THEN 'N'
                                 ELSE IND_REPROCESO END,
      NRO_CICLO           = CASE WHEN p_nuevo_paso='9R' THEN NRO_CICLO + 1 ELSE NRO_CICLO END,
      -- ── Retraso ────────────────────────────────────────────
      DIAS_RETRASO        = NVL(GREATEST(TRUNC(SYSDATE) - TRUNC(FCH_ENTREGA_COMP), 0), 0),
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
      p_nuevo_paso, p_tabla_origen, p_id_origen, SYSDATE, USER,
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
      SELECT * INTO v_itemdet FROM ITEMPED_DET WHERE serie=p_serie AND num_ped=p_num_ped AND nro=p_nro AND num_det=p_num_det;
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

    -- kgr_hr: de la máquina asignada; si no hay, MAX del título/proceso
    IF v_maquina IS NOT NULL THEN
      BEGIN
        SELECT kgr_hr INTO v_kgr_hr
        FROM ctrutas_titulo
        WHERE titulo=v_item.titulo AND proceso=v_item.proceso
          AND cod_maq=v_maquina   AND estado != 'X'
          AND ROWNUM = 1;
      EXCEPTION WHEN NO_DATA_FOUND THEN NULL;
      END;
    END IF;
    IF v_kgr_hr IS NULL THEN   -- ninguna máquina asignada o sin datos en ctrutas_titulo
      BEGIN
        SELECT MAX(kgr_hr) INTO v_kgr_hr
        FROM ctrutas_titulo
        WHERE titulo=v_item.titulo AND proceso=v_item.proceso AND estado != 'X';
      EXCEPTION WHEN NO_DATA_FOUND THEN NULL;
      END;
    END IF;
    IF NVL(v_kgr_hr, 0) = 0 THEN v_kgr_hr := 10; END IF;  -- fallback

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
                      TRUNC(NVL(v_seg.fch_real_secado, v_est_sec)) + v_buf_qc);
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
      IND_RETRASO        = CASE WHEN v_est_desp > FCH_ENTREGA_COMP THEN 'S' ELSE 'N' END,
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

    -- Sincronizar ITEMPED_DET.FCH_ESTIMA_TENIDO y FCH_ESTIMA_CONO_UNO
    BEGIN
      UPDATE ITEMPED_DET SET
        FCH_ESTIMA_TENIDO   = v_est_tini,
        FCH_ESTIMA_CONO_UNO = v_est_tfin
      WHERE serie=p_serie AND num_ped=p_num_ped AND nro=p_nro AND num_det=p_num_det;
    EXCEPTION WHEN OTHERS THEN NULL;
    END;

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
  --   'STN'  Nivel 'C' → PASO='05' y SYSDATE > FCH_EST_TIN_INI (esperando TT)
  --   'QCF'  Nivel 'C' → PASO='9R' (CC rechazado, en reproceso)
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
              WHERE s.estado='A' AND s.cod_paso_act='05'
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

    COMMIT;
  END SP_PLN_GENERA_ALERTAS;


  -- ============================================================
  -- SP_PLN_CARGA_DIARIA_REFRESH — Actualización de carga de máquinas
  -- ────────────────────────────────────────────────────────────
  -- Regenera PLN_CARGA_DIARIA para el rango p_fch_ini..p_fch_fin.
  -- Por defecto: TRUNC(SYSDATE)..TRUNC(SYSDATE)+30 (próximos 30 días).
  -- Ejecutar vía JOB_PLN_CARGA (FREQ=DAILY; BYHOUR=23; BYMINUTE=30).
  --
  -- OPERACIÓN:
  --   1. DELETE FROM PLN_CARGA_DIARIA WHERE fecha BETWEEN fch_ini AND fch_fin
  --   2. INSERT desde H_PRODUCCION_D (producción real registrada)
  --   3. UPDATE: PCT_UTILIZACION, PCT_CARGA, IND_SOBRECARGADA
  --
  -- FUENTE DE DATOS:
  --   H_PRODUCCION_D (detalle diario de producción por máquina)
  --   JOIN H_PRODUCCION_G (cabecera: fecha, turno, tp_maq, cod_maq)
  --
  -- NOTA: KG_CAPACIDAD y HORAS_CAPACIDAD deben actualizarse por separado
  --   desde MA_PROGRAMA (programación de mantenimiento de máquinas).
  --   Este SP solo actualiza los valores reales (KG_REAL, HORAS_REAL).
  -- ============================================================
  PROCEDURE SP_PLN_CARGA_DIARIA_REFRESH (
    p_fch_ini IN DATE DEFAULT TRUNC(SYSDATE),
    p_fch_fin IN DATE DEFAULT TRUNC(SYSDATE) + 30
  ) AS
  BEGIN
    DELETE FROM PLN_CARGA_DIARIA
    WHERE fecha BETWEEN p_fch_ini AND p_fch_fin;

    -- Carga real desde H_PRODUCCION_D
    INSERT INTO PLN_CARGA_DIARIA (
      FECHA, COD_MAQ, TP_MAQ, HORAS_REAL, KG_REAL, FCH_CALCULO, A_MDFECHA
    )
    SELECT
      d.fecha,
      d.cod_maq,
      d.tp_maq,
      -- HORAS_TRABAJADAS es VARCHAR2(5) en formato 'HH.MM' (no decimal ni 'HH:MM').
      -- Conversión correcta: parte entera (horas) + parte decimal (minutos/60).
      -- Guard REGEXP evita ORA-01722 por datos inválidos ('.' , '00.' , '-5.10', etc.).
      SUM(CASE WHEN REGEXP_LIKE(d.horas_trabajadas, '^\d{2}\.\d{2}$')
               THEN TO_NUMBER(SUBSTR(d.horas_trabajadas,1,2))
                  + TO_NUMBER(SUBSTR(d.horas_trabajadas,4,2))/60
               ELSE 0
          END),
      SUM(d.cantidad),
      SYSDATE,
      SYSDATE
    FROM h_produccion_d d
    WHERE d.fecha BETWEEN p_fch_ini AND p_fch_fin
    GROUP BY d.fecha, d.cod_maq, d.tp_maq;

    -- Porcentajes
    UPDATE PLN_CARGA_DIARIA SET
      PCT_UTILIZACION  = ROUND(KG_REAL       / NULLIF(KG_CAPACIDAD,0) * 100, 2),
      PCT_CARGA        = ROUND(KG_ASIGNADOS  / NULLIF(KG_CAPACIDAD,0) * 100, 2),
      IND_SOBRECARGADA = CASE WHEN KG_ASIGNADOS > KG_CAPACIDAD THEN 'S' ELSE 'N' END
    WHERE fecha BETWEEN p_fch_ini AND p_fch_fin;

    COMMIT;
  END SP_PLN_CARGA_DIARIA_REFRESH;


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
      ESTADO    = 'C',
      A_MDUSER  = v_usr,
      A_MDFECHA = SYSDATE
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
    SELECT PLN_SEQ_EVENTO.NEXTVAL, id_seguim, '14', p_motivo, SYSDATE, v_usr, 'CI',
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
      IND_RETRASO      = CASE WHEN p_nueva_fch_desp > FCH_ENTREGA_COMP THEN 'S' ELSE 'N' END,
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

    COMMIT;
  EXCEPTION
    WHEN OTHERS THEN ROLLBACK; RAISE;
  END SP_PLN_REPROGRAMAR;

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
-- Paso destino: '01' (normal) ó '13' (si SOLO_DESPACHO='S')
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
  v_solo_desp VARCHAR2(1) := 'N';
  v_paso_ini  VARCHAR2(3) := '01';
BEGIN
  -- Si el ítem es solo-despacho, inicia en PASO '13'
  -- Usar :NEW directamente evita ORA-04091 (tabla mutante) en inserciones bulk.
  v_solo_desp := NVL(:NEW.solo_despacho, 'N');
  IF v_solo_desp = 'S' THEN v_paso_ini := '13'; END IF;

  PKG_PLN.SP_PLN_INIT_SEGUIMIENTO(:NEW.serie, :NEW.num_ped, :NEW.nro, 0, v_paso_ini);
  PKG_PLN.SP_PLN_CALCULA_FECHAS(:NEW.serie, :NEW.num_ped, :NEW.nro, 0, 'PED');
EXCEPTION
  WHEN OTHERS THEN NULL;
END TIA_PLN_FROM_ITEMPED;
/

-- ────────────────────────────────────────────────────────────
-- §7.2  TUA_PLN_FROM_ITEMPED_DET — PASO '02' Planificado
-- ────────────────────────────────────────────────────────────
-- Disparo  : AFTER UPDATE ON ITEMPED_DET FOR EACH ROW
-- Condición: NEW.NROPROG IS NOT NULL AND (OLD.NROPROG IS NULL OR NEW.FHC_PROG changed)
-- Acción   : Crea sub-lote en PLN_SEGUIMIENTO (si num_det > 0)
--             + avanza a PASO '02' + recalcula fechas (motivo='PLA')
--             + actualiza IND_URGENTE='S' si URGENTE='S' o hay ANTICIPO cobrado (BUG #36)
-- Tabla     : ITEMPED_DET — sub-lote del ítem (NROPROG = programa H_PROGRAMACION)
-- Campos clave:
--   :NEW.NROPROG → número de programa asignado (FK a H_PROGRAMACION)
--   :NEW.NUM_DET → sub-lote (distingue múltiples corridas del mismo ítem)
--   :NEW.CANTIDAD → kg del sub-lote
--   :NEW.FHC_PROG → fecha de planificación
-- La condición WHEN evita disparos innecesarios en updates que no
--   cambian el NROPROG (ej. cambios de precio, estado, etc.).
-- EXCEPTION WHEN OTHERS THEN NULL → nunca bloquea el UPDATE de ITEMPED_DET.
CREATE OR REPLACE TRIGGER TUA_PLN_FROM_ITEMPED_DET
AFTER UPDATE ON ITEMPED_DET
FOR EACH ROW
WHEN (NEW.NROPROG IS NOT NULL
      AND (OLD.NROPROG IS NULL
           OR NEW.NROPROG  != OLD.NROPROG
           OR NEW.FHC_PROG != OLD.FHC_PROG))
DECLARE
  v_urgente VARCHAR2(1) := 'N';
BEGIN
  PKG_PLN.SP_PLN_INIT_SEGUIMIENTO(:NEW.serie, :NEW.num_ped, :NEW.nro, :NEW.num_det);
  PKG_PLN.SP_PLN_AVANZA_PASO(
    :NEW.serie, :NEW.num_ped, :NEW.nro, :NEW.num_det,
    '02', 'ITEMPED_DET', :NEW.nroprog, :NEW.cantidad,
    'Programa asignado: '||:NEW.nroprog
  );
  PKG_PLN.SP_PLN_CALCULA_FECHAS(:NEW.serie, :NEW.num_ped, :NEW.nro, :NEW.num_det, 'PLA');

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
EXCEPTION
  WHEN OTHERS THEN NULL;
END TUA_PLN_FROM_ITEMPED_DET;
/

-- ────────────────────────────────────────────────────────────
-- §7.3  TIA_PLN_FROM_H_RPRODUC — PASO '04'/'09B' Lote Disponible o Gaseado
-- ────────────────────────────────────────────────────────────
-- Disparo  : AFTER INSERT ON H_RPRODUC FOR EACH ROW WHEN (NEW.GUIA IS NOT NULL)
-- Acción   : Avanza a PASO '04' (lote disponible) o '09B' (gaseado para PROCESO='24')
-- Tabla     : H_RPRODUC — registro de producción de hilandería y acabados
-- Navegación (GUIA → NROPROG → item):
--   H_RPRODUC.GUIA → PARTIDA.NUMERO
--   PARTIDA.NROPROG → ITEMPED_DET.(NRO, NUM_DET)
--   PARTIDA.SERIE + PARTIDA.NRO_PEDIDO → identifica el pedido
-- Campos clave en :NEW:
--   GUIA      → número de PARTIDA (lote de hilo en producción)
--   PESO_NETO → kg procesados en esta corrida
--   COD_MAQ   → máquina usada (ej. 'GAS01', 'GAS02' para gaseadora)
--   TP_MAQ    → 'G'=GASEADORA → PASO '09B' / demás → PASO '04'
-- BUG #31 CORREGIDO: TP_MAQ='G' (GASEADORA) avanzaba incorrectamente a '03'.
--   Ahora: si TP_MAQ='G' → intenta PASO '09B' (SP valida que PROCESO='24').
-- BUG #40 CORREGIDO: PASO '03'/'04' estaban invertidos. H_RPRODUC finishing ocurre
--   DESPUÉS de que la PARTIDA es creada, por lo que corresponde a PASO '04'
--   (Lote Disponible), no a PASO '03' (En Hilandería).
-- EXCEPTION WHEN OTHERS THEN NULL → no bloquea el INSERT de H_RPRODUC.
CREATE OR REPLACE TRIGGER TIA_PLN_FROM_H_RPRODUC
AFTER INSERT ON H_RPRODUC
FOR EACH ROW
WHEN (NEW.GUIA IS NOT NULL)
DECLARE
  v_nroprog  NUMBER;
  v_serie    NUMBER;
  v_num_ped  NUMBER;
  v_nro      NUMBER;
  v_num_det  NUMBER;
  v_nuevo_paso VARCHAR2(4);
BEGIN
  SELECT p.nroprog, p.serie, p.nro_pedido
  INTO v_nroprog, v_serie, v_num_ped
  FROM partida p
  WHERE p.numero = :NEW.guia;

  SELECT d.nro, d.num_det INTO v_nro, v_num_det
  FROM itemped_det d
  WHERE d.nroprog = v_nroprog AND ROWNUM = 1;

  -- BUG #31: Diferenciar GASEADORA (TP_MAQ='G') → PASO '09B'
  -- BUG #40: H_RPRODUC finishing ocurre DESPUÉS de PARTIDA → PASO '04' (no '03')
  v_nuevo_paso := CASE WHEN :NEW.tp_maq = 'G' THEN '09B' ELSE '04' END;

  PKG_PLN.SP_PLN_AVANZA_PASO(
    v_serie, v_num_ped, v_nro, v_num_det,
    v_nuevo_paso, 'H_RPRODUC', :NEW.guia, :NEW.peso_neto,
    'Producción - Máq:'||:NEW.cod_maq||' Tipo:'||:NEW.tp_maq
  );
EXCEPTION
  WHEN NO_DATA_FOUND THEN NULL;
  WHEN OTHERS        THEN NULL;
END TIA_PLN_FROM_H_RPRODUC;
/

-- ────────────────────────────────────────────────────────────
-- §7.4  TIA_PLN_FROM_PARTIDA — PASO '03' En Hilandería
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
-- BUG #40 CORREGIDO: PARTIDA INSERT es el primer evento de producción (el lote
--   entra a hilandería/producción antes que H_RPRODUC finishing) → PASO '03',
--   no '04'. El trigger TIA_PLN_FROM_H_RPRODUC fue actualizado a PASO '04'.
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
-- §7.5  TUA_PLN_FROM_L_VALIDA_RECETA — PASO '05' Laboratorio
-- ────────────────────────────────────────────────────────────
-- Disparo  : AFTER UPDATE OF ESTADO ON L_VALIDA_RECETA FOR EACH ROW
-- Condición: NEW.ESTADO='3' AND (OLD.ESTADO IS NULL OR OLD.ESTADO <> '3')
-- Acción   : Avanza a PASO '05' (receta de tintorería validada por lab.)
-- Tabla     : L_VALIDA_RECETA — validación de receta por laboratorio
-- Navegación:
--   L_VALIDA_RECETA.NROPROG → ITEMPED_DET.(SERIE, NUM_PED, NRO, NUM_DET)
-- ESTADO='3' significa receta aprobada.
-- Si NROPROG IS NULL → RETURN sin avanzar (receta sin ítem de pedido asociado).
-- IMPORTANTE: después del PASO '05', el ítem espera ingresar a TT.
--   Si FCH_EST_TIN_INI ya pasó, SP_PLN_GENERA_ALERTAS generará alerta 'STN'.
-- EXCEPTION WHEN OTHERS THEN NULL → no bloquea el UPDATE de L_VALIDA_RECETA.
CREATE OR REPLACE TRIGGER TUA_PLN_FROM_L_VALIDA_RECETA
AFTER UPDATE OF ESTADO ON L_VALIDA_RECETA
FOR EACH ROW
WHEN (NEW.ESTADO = '3' AND (OLD.ESTADO IS NULL OR OLD.ESTADO <> '3'))
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
    '05', 'L_VALIDA_RECETA', :NEW.numero, NULL,
    'Receta validada - Lab:'||NVL(:NEW.c_laboratorista,'N/A')
  );
EXCEPTION
  WHEN NO_DATA_FOUND THEN NULL;
  WHEN OTHERS        THEN NULL;
END TUA_PLN_FROM_L_VALIDA_RECETA;
/

-- ────────────────────────────────────────────────────────────
-- §7.6  TUA_PLN_FROM_PARTIDA — PASO '06' En Tintorería
-- ────────────────────────────────────────────────────────────
-- Disparo  : AFTER UPDATE ON PARTIDA FOR EACH ROW
-- Condición: NEW.SITU_PART='R001' AND OLD.SITU_PART <> 'R001' AND NEW.NROPROG IS NOT NULL
-- Acción   : Avanza a PASO '06' (la partida ingresó físicamente a tintorería)
-- Tabla     : PARTIDA — actualización de SITU_PART por el sistema de TT
-- Navegación: misma que TIA_PLN_FROM_PARTIDA (§7.4)
--   :NEW.SERIE + :NEW.NRO_PEDIDO → pedido
--   :NEW.NROPROG → ITEMPED_DET → NRO, NUM_DET
-- SITU_PART='R001' = código de estado "Recibida en Tintorería".
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
    'Ingresó a Tintorería - SITU_PART=R001'
  );
EXCEPTION
  WHEN NO_DATA_FOUND THEN NULL;
  WHEN OTHERS        THEN NULL;
END TUA_PLN_FROM_PARTIDA;
/

-- ────────────────────────────────────────────────────────────
-- §7.7  TUA_PLN_FROM_TT_RPRODUC — PASO '07' Tenido Completo
-- ────────────────────────────────────────────────────────────
-- Disparo  : AFTER UPDATE OF ESTADO ON TT_RPRODUC FOR EACH ROW
-- Condición: NEW.ESTADO='3' AND (OLD.ESTADO IS NULL OR OLD.ESTADO <> '3')
-- Acción   : Avanza a PASO '07' SOLO cuando TODOS los baños de la partida OK
-- Tabla     : TT_RPRODUC — registro de producción de tintorería por baño
-- REGLA CRÍTICA (75% de partidas con 2+ baños):
--   Primero busca la PARTIDA via ING_RECETAS_G.R_NUMERO (verificado en BD: la columna
--   que referencia PARTIDA.NUMERO es R_NUMERO, NO 'GUIA' — ese campo no existe).
--   Luego cuenta baños pendientes:
--     SELECT COUNT(*) FROM ing_recetas_g ig2 JOIN tt_rproduc r ON r.receta=ig2.numero
--     WHERE ig2.r_numero = v_partida AND r.estado <> '3'
--   Si v_pendientes > 0 → RETURN (aún hay baños sin terminar, no avanzar)
--   Si v_pendientes = 0 → todos los baños completados → avanzar a '07'
-- Navegación:
--   TT_RPRODUC.RECETA → ING_RECETAS_G.NUMERO
--   ING_RECETAS_G.R_NUMERO → PARTIDA.NUMERO  (verificado: R_TRANSAC='PR')
--   PARTIDA.NROPROG → ITEMPED_DET.(NRO, NUM_DET)
--   PARTIDA.SERIE + PARTIDA.NRO_PEDIDO → pedido
-- NOTA: Es AFTER UPDATE, no AFTER INSERT. ESTADO='3' en TT_RPRODUC
--   se actualiza via pantalla del operador de TT, no via INSERT.
-- EXCEPTION WHEN OTHERS THEN NULL → no bloquea el UPDATE de TT_RPRODUC.
CREATE OR REPLACE TRIGGER TUA_PLN_FROM_TT_RPRODUC
AFTER UPDATE OF ESTADO ON TT_RPRODUC
FOR EACH ROW
WHEN (NEW.ESTADO = '3' AND (OLD.ESTADO IS NULL OR OLD.ESTADO <> '3'))
DECLARE
  v_partida    NUMBER;
  v_nroprog    NUMBER;
  v_serie      NUMBER;
  v_num_ped    NUMBER;
  v_nro        NUMBER;
  v_num_det    NUMBER;
  v_pendientes NUMBER := 0;
BEGIN
  -- ING_RECETAS_G.R_NUMERO referencia a PARTIDA.NUMERO (verificado en BD: no existe columna GUIA)
  SELECT ig.r_numero INTO v_partida
  FROM ing_recetas_g ig
  WHERE ig.numero = :NEW.receta AND ROWNUM = 1;

  -- Solo avanzar cuando NO quedan baños pendientes
  SELECT COUNT(*) INTO v_pendientes
  FROM ing_recetas_g ig2
  JOIN tt_rproduc r ON r.receta = ig2.numero
  WHERE ig2.r_numero = v_partida
    AND r.estado <> '3';

  IF v_pendientes > 0 THEN RETURN; END IF;

  SELECT p.nroprog, p.serie, p.nro_pedido
  INTO v_nroprog, v_serie, v_num_ped
  FROM partida p
  WHERE p.numero = v_partida;

  SELECT d.nro, d.num_det INTO v_nro, v_num_det
  FROM itemped_det d
  WHERE d.nroprog = v_nroprog AND ROWNUM = 1;

  PKG_PLN.SP_PLN_AVANZA_PASO(
    v_serie, v_num_ped, v_nro, v_num_det,
    '07', 'TT_RPRODUC', :NEW.receta, NULL,
    'Tenido completo - Último baño RECETA:'||:NEW.receta
  );
EXCEPTION
  WHEN NO_DATA_FOUND THEN NULL;
  WHEN OTHERS        THEN NULL;
END TUA_PLN_FROM_TT_RPRODUC;
/

-- ────────────────────────────────────────────────────────────
-- §7.8  TIA_PLN_FROM_TT_RSECADO — PASO '08' Secado
-- ────────────────────────────────────────────────────────────
-- Disparo  : AFTER INSERT ON TT_RSECADO FOR EACH ROW
-- Acción   : Avanza a PASO '08' (secado post-tintorería registrado)
-- Tabla     : TT_RSECADO — registro de secado por partida y máquina de secado
-- Navegación:
--   TT_RSECADO.GUIA → PARTIDA.NUMERO (mismo patrón que H_RPRODUC en §7.3)
--   PARTIDA.NROPROG → ITEMPED_DET.(NRO, NUM_DET)
-- PESO_NETO: peso del hilo ya seco (puede diferir del peso húmedo post-TT).
-- Tiempo buffer de secado configurado en PLN_PARAM.HRS_SECADO (default 8h).
-- EXCEPTION WHEN OTHERS THEN NULL → no bloquea el INSERT de TT_RSECADO.
CREATE OR REPLACE TRIGGER TIA_PLN_FROM_TT_RSECADO
AFTER INSERT ON TT_RSECADO
FOR EACH ROW
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
    'Secado registrado'
  );
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
-- FIX #28: Valores de RESULTADO confirmados en BD (trigger TUA_CTCALIDADD_RESULTADO):
--   RESULTADO IN ('01','21','29','30') → APROBADO → PASO '09'
--   Cualquier otro valor no nulo       → RECHAZADO → PASO '9R' (Reproceso)
--   '01'=Aprobado (124k), '21'=Concesionado (829), '29'=Conces. (1.7k), '30'=Conces. (3.6k)
--   La versión anterior mapeaba '30' a '9R' — incorrecto: la BD lo trata como aprobado.
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
  -- FIX #28: '30' es aprobado según TUA_CTCALIDADD_RESULTADO de producción
  v_paso := CASE
    WHEN :NEW.resultado IN ('01','21','29','30') THEN '09'  -- Aprobado/Concesionado
    WHEN :NEW.resultado IS NOT NULL              THEN '9R'  -- Rechazado → Reproceso
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
-- §7.10  TIA_PLN_FROM_REVISADO_G — PASO '10' Devanado
-- ────────────────────────────────────────────────────────────
-- Disparo  : AFTER INSERT ON REVISADO_G FOR EACH ROW WHEN (NEW.GUIA IS NOT NULL)
-- Acción   : Avanza a PASO '10' (inicio del proceso de devanado/rewinding)
-- Tabla     : REVISADO_G — cabecera de operación de devanado + revisado
-- Confirmado: 100% de REVISADO_G tienen GUIA NOT NULL (90,964 registros verificados)
-- Máquinas en MAQ_PROCED: 'XX AUT' (AUTOCONER), 'RED0X' (REDINA), etc.
-- Navegación:
--   REVISADO_G.GUIA → PARTIDA.NUMERO
--   PARTIDA.NROPROG → ITEMPED_DET.(NRO, NUM_DET)
-- BUG #32 CORRECCIÓN: Sin este trigger, PASO '10' jamás se registraba.
--   El ítem pasaba directamente de '09' a '11' sin registrar el devanado.
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
    '10', 'REVISADO_G', :NEW.numero, NULL,
    'Devanado - Máq:'||:NEW.maq_proced
  );
EXCEPTION
  WHEN NO_DATA_FOUND THEN NULL;
  WHEN OTHERS        THEN NULL;
END TIA_PLN_FROM_REVISADO_G;
/

-- ────────────────────────────────────────────────────────────
-- §7.11  TIA_PLN_FROM_REVISADO — PASO '11' Revisado
-- ────────────────────────────────────────────────────────────
-- Disparo  : AFTER INSERT ON REVISADO_D FOR EACH ROW WHEN (NEW.APROBADO > 0)
-- Acción   : Avanza a PASO '11' (calidad final aprobada — conos revisados)
-- Tabla     : REVISADO_D — detalle de revisión de calidad de conos
-- Navegación (dos saltos):
--   REVISADO_D.NUMERO → REVISADO_G.NUMERO (cabecera de revisado)
--   REVISADO_G.GUIA → PARTIDA.NUMERO
--   PARTIDA.NROPROG → ITEMPED_DET.(NRO, NUM_DET)
--   PARTIDA.SERIE + PARTIDA.NRO_PEDIDO → pedido
-- CONDICIÓN: APROBADO > 0 (solo filas con conos aprobados disparan el trigger)
-- APROBADO: cantidad de conos que pasaron la revisión visual.
-- NOTA: PASO '10' (Devanado) se captura en §7.10 TIA_PLN_FROM_REVISADO_G.
-- EXCEPTION WHEN OTHERS THEN NULL → no bloquea el INSERT de REVISADO_D.
CREATE OR REPLACE TRIGGER TIA_PLN_FROM_REVISADO
AFTER INSERT ON REVISADO_D
FOR EACH ROW
WHEN (NEW.APROBADO > 0)
DECLARE
  v_nroprog NUMBER;
  v_serie   NUMBER;
  v_num_ped NUMBER;
  v_nro     NUMBER;
  v_num_det NUMBER;
BEGIN
  SELECT p.nroprog, p.serie, p.nro_pedido
  INTO v_nroprog, v_serie, v_num_ped
  FROM revisado_g rg
  JOIN partida p ON p.numero = rg.guia
  WHERE rg.numero = :NEW.numero AND ROWNUM = 1;

  SELECT d.nro, d.num_det INTO v_nro, v_num_det
  FROM itemped_det d
  WHERE d.nroprog = v_nroprog AND ROWNUM = 1;

  PKG_PLN.SP_PLN_AVANZA_PASO(
    v_serie, v_num_ped, v_nro, v_num_det,
    '11', 'REVISADO_D', :NEW.numero,
    :NEW.aprobado, 'Revisado: '||:NEW.aprobado||' conos aprobados'
  );
EXCEPTION
  WHEN NO_DATA_FOUND THEN NULL;
  WHEN OTHERS        THEN NULL;
END TIA_PLN_FROM_REVISADO;
/

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
WHERE p.estado IN ('0','5','9')
GROUP BY p.serie, p.num_ped, p.fecha, p.cod_cliente, cl.nombre, p.estado, p.prioridad;

-- ────────────────────────────────────────────────────────────
-- §8.2  V_PLN_ESTADO_ITEM — Detalle de estado por ítem con semáforo
-- ────────────────────────────────────────────────────────────
-- Propósito  : Listado detallado de ítems activos con toda la información
--              de estado para el Dashboard y la página Pedido.cshtml.
-- JOIN       : PLN_SEGUIMIENTO + CLIENTES + ARTICUL + PLN_ESTADO_CODIGO + PARTIDA
-- SEMÁFORO (campo calculado):
--   'R' → dias_retraso >= 7 (rojo)
--   'A' → dias_retraso >= 3 (naranja/amber)
--   'Y' → dias_retraso >= 1 (amarillo)
--   'G' → sin retraso      (verde)
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
  ROUND(s.kg_despachados / NULLIF(s.cantidad_orig,0) * 100, 1) AS pct_avance,
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
    WHEN s.dias_retraso >= 7 THEN 'R'
    WHEN s.dias_retraso >= 3 THEN 'A'
    WHEN s.dias_retraso >= 1 THEN 'Y'
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
  pe.fecha                AS fch_pedido,
  pe.f_aprobacion         AS fch_aprob_pedido,
  COALESCE(id.fhc_prog, s.fch_real_programado) AS fch_planeada,  -- fallback a FCH_REAL_PROGRAMADO si FHC_PROG es null
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
LEFT JOIN itemped_det id ON id.serie=s.serie AND id.num_ped=s.num_ped
                       AND id.nro=s.nro AND id.num_det=s.num_det  -- LEFT: ítems sin ITEMPED_DET no se pierden
LEFT JOIN tt_progpart tt ON tt.num_ped=s.num_ped AND tt.nro=s.nro AND tt.num_det=s.num_det;

-- ────────────────────────────────────────────────────────────
-- §8.4  V_PLN_ALERTAS_ACTIVAS — Panel de alertas activas
-- ────────────────────────────────────────────────────────────
-- Propósito  : Bandeja de alertas para supervisores en Alertas.cshtml.
-- Uso en app : Alertas.cshtml (GET /Produccion/Planeamiento/Alertas)
-- Filtro     : PLN_ALERTA.ESTADO = 'A' (solo alertas activas)
-- Orden      : NIVEL ('C' primero) → FCH_ALERTA
-- Campo calculado:
--   horas_sin_resolver = SYSDATE - FCH_ALERTA (en horas fraccionarias)
-- Acciones POST disponibles en la app:
--   POST /Produccion/Planeamiento/ResolverAlerta → ESTADO='R'
--   POST /Produccion/Planeamiento/IgnorarAlerta  → ESTADO='I'
-- Badge de nivel en Bootstrap 5:
--   'C' → <span class="badge bg-danger">Crítico</span>
--   'A' → <span class="badge bg-warning text-dark">Alto</span>
--   'M' → <span class="badge bg-info">Medio</span>
--   'B' → <span class="badge bg-secondary">Bajo</span>
CREATE OR REPLACE VIEW V_PLN_ALERTAS_ACTIVAS AS
SELECT
  a.id_alerta,
  a.tip_alerta,
  a.nivel,
  a.titulo,
  a.detalle,
  a.fch_alerta,
  a.fch_limite,
  a.dias_retraso,
  a.num_ped,
  a.nro,
  a.cod_cliente,
  cl.nombre           AS nom_cliente,
  a.cod_maq,
  a.estado,
  SYSDATE - a.fch_alerta AS horas_sin_resolver
FROM pln_alerta a
LEFT JOIN clientes cl ON cl.cod_cliente = a.cod_cliente
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
-- Refrescado diariamente por JOB_PLN_CARGA (23:30 diario).
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
  ROUND(SUM(CASE WHEN s.fch_real_despacho <= s.fch_entrega_comp THEN 1 ELSE 0 END)
        / NULLIF(COUNT(*),0) * 100, 1)                AS pct_otif,
  ROUND(AVG(s.fch_real_despacho - s.fch_pedido),1)    AS ciclo_promedio_dias,
  ROUND(AVG(s.fch_real_tin_fin - s.fch_real_tin_ini),1) AS dias_prom_tintoreria,
  ROUND(AVG(s.fch_real_partida - s.fch_pedido),1)     AS dias_prom_pedido_partida,
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
--   Ejecuta SP_PLN_CARGA_DIARIA_REFRESH diariamente a las 23:30.
--   Regenera PLN_CARGA_DIARIA para los próximos 30 días.
--   Patrón: FREQ=DAILY; BYHOUR=23; BYMINUTE=30
--
-- POR QUÉ enabled=>FALSE en este script:
--   La BD Oracle es una sola instancia compartida (10.0.7.11). No existe un
--   servidor DEV separado. Si se despliega este script en desarrollo con enabled=>TRUE:
--     · JOB_PLN_ALERTAS (c/hora) contamina PLN_ALERTA con alertas de datos de prueba,
--       visibles en la UI de supervisores de producción.
--     · JOB_PLN_CARGA (23:30) hace DELETE+INSERT de toda PLN_CARGA_DIARIA, borrando
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
    repeat_interval => 'FREQ=DAILY; BYHOUR=23; BYMINUTE=30',
    enabled         => FALSE,   -- cambiar a TRUE en PROD
    comments        => 'PLN_: recalcula carga de máquinas próximos 30 días (23:30 diario)'
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
-- JOB_PLN_CARGA   : se ejecutará esta noche a las 23:30.
--
-- NOTA SOBRE DATOS HISTÓRICOS:
--   PLN_ captura data ÚNICAMENTE desde este momento de despliegue.
--   Los triggers disparan solo ante operaciones nuevas:
--     · ITEMPED INSERT  → crea fila en PLN_SEGUIMIENTO
--     · PARTIDA INSERT  → avanza a PASO '04'
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
-- PROMPT >>> §10.2 Activando JOB_PLN_CARGA (diario 23:30)...
-- BEGIN
--   DBMS_SCHEDULER.ENABLE('JOB_PLN_CARGA');
-- END;
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
PROMPT   - Triggers activos: 13
PROMPT   - Vistas disponibles: 8
PROMPT   - Jobs programados: JOB_PLN_ALERTAS (c/hora) + JOB_PLN_CARGA (23:30)
PROMPT   - Data desde: ahora (no hay migracion historica)
PROMPT ============================================================


-- ============================================================
-- §11  SCRIPT DE MIGRACIÓN HISTÓRICA — PENDIENTE / OPCIONAL
-- ============================================================
-- El módulo PLN_ opera con pedidos nuevos desde el despliegue.
-- Los pedidos existentes NO se migran en esta versión.
-- Si en el futuro se decide migrar, el script irá aquí
-- contra ITEMPED / ITEMPED_DET / PARTIDA / LOTES.
