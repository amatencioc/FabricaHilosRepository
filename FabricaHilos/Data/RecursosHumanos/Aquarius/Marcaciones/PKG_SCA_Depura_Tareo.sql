/*******************************************************************************
    SISTEMA AQUARIUS - PAQUETE DE DEPURACION DE MARCACIONES
    Oracle 10g - Compatible con Toad 7.5
    
    !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
    IMPORTANTE: Este paquete apunta a SCA_ASISTENCIA_TAREO (PRODUCCION - AQUARIUS)
                Ejecutar con usuario AQUARIUS.
    !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
    
    ESTRATEGIA:
    ===========
    - SCA_HISTORIAL     = Data cruda (NO SE MODIFICA, SOLO SE INSERTAN MARCAS)
    - SCA_ASISTENCIA_TAREO = Data procesada (SE TRABAJA AQUI - PRODUCCION)
    
    Si hay algun problema, solo se vuelve a generar el tareo y listo.
    No se afecta la data original de las lectoras.
    
    ALCANCE:
    ========
    Solo procesa empleados con >= 1 marcacion real.
    Empleados con 0 marcaciones NO se tocan (proceso independiente futuro).
    
    PROCEDIMIENTOS:
    ===============
    - DEPURA_TOTAL          : Depura marcaciones de UN DIA (individual o masivo)
    - DEPURA_RANGO          : Depura un RANGO DE FECHAS (llama DEPURA_TOTAL por dia)
    - ROLLBACK_MARCACIONES  : Revierte cambios automaticos de depuracion
    - VER_ESTADO            : Consulta estado actual de marcaciones
    
    ORDEN DE EJECUCION (PASOs):
    ===========================
    FASE 0 - LIMPIEZA:
      0-CLEAN  : Eliminar marcas de depuracion anterior en SCA_HISTORIAL
      0-DUP    : Eliminar marcas duplicadas cercanas (<5 min)
      0-ORF    : Eliminar marcas huerfanas intermedias (>90 min del refri)
      0-RESTORE: Re-insertar marcas del tareo eliminadas por DUP/ORF
      0-RESTORE-B: Corregir paridad impar causada por rondas/checkpoints
      0-PRE    : Sincronizar NUMMARCACIONES con SCA_HISTORIAL + alerta01
      6-PHANTOM: Limpiar marcaciones fantasma en dias de DESCANSO (0 marcas SCA_HIS)
      6-PHANTOM-B: Limpiar salida fantasma nocturna en DESCANSO (entrada=NULL, salida fantasma)
      6-PHANTOM-C: Limpiar entrada+salida fantasma en DESCANSO (entrada no existe en SCA_HIS)
      6-PHANTOM-D: Limpiar entrada madrugada en DESCANSO (salida del turno nocturno anterior asignada como entrada del dia de descanso)
    
    FASE 0 - TURNOS NOCTURNOS:
      0A       : Mover ENTRADA de madrugada a SALIDA del dia anterior
      0B       : Limpiar ENTRADA incorrecta del dia actual
      0B2      : Reubicar SALIDA vespertina como ENTRADA post-N1
      0B3      : Completar SALIDA desde dia siguiente (forward)
      0B3c     : Salida REAL posterior en dia siguiente (sobretiempo)
      0B3b     : Salida TEORICA cuando no hay marca forward
      0B3d     : Re-asignar ENTRADA a marca temprana del mismo dia (sobretiempo)
      0B4      : Limpiar ENTRADA del dia siguiente movida por 0B3
      0B5      : N4 para dia siguiente (reubicar SALIDA vespertina)
      0C       : SALIDA nocturna mal ubicada - Mover a ENTRADA + completar SALIDA
      0D       : Marca de manana puesta en ENTRADA de turno nocturno
    
    FASE 1 - COMPLETAR ENTRADA:
      1        : Completar ENTRADA con horario teorico (tiene SALIDA)
      1B       : Ajustar ENTRADA anticipada (-15 min)
      1C-NOC   : Marca duplicada en turno nocturno
      1C       : Corregir ENTRADA = SALIDA (marca duplicada)
    
    FASE 2 - COMPLETAR SALIDA Y REFRIGERIO:
      2        : Completar SALIDA con horario teorico (tiene ENTRADA)
      2A       : Validar refrigerio asignado por tareo
      2B-PRE   : Recuperar marcas de refrigerio desde SCA_HISTORIAL
      2B       : Limpiar INIREFRI/FINREFRI incorrectos y completar con teorico
      3A       : Completar INIREFRI (tiene FINREFRI real)
      3B       : Completar FINREFRI (tiene INIREFRI real)
      3C-NOC   : Limpiar refrigerio en nocturno sin entrada anticipada
      3D       : Corregir salida imposible (salida < inirefri)
                 Fase A: buscar marca oculta como salida real (SSR)
                 Fase B: sin oculta -> salida_fijada (SS)
      3E       : Corregir refrigerio imposible (inirefri < entrada)
                 Limpiar inirefri/finrefri cuando el refri es anterior a la entrada
    
    FASE 4 - ANOMALIAS:
      4B       : Marcaciones consecutivas anomalas (4 en <1 hora)
    
    FASE 5 - 3ER TURNO Y RECALCULO:
      5G       : 3er turno con entrada anticipada y sobretiempo
      5        : Recalcular tothoramarcas
      5A       : Hora nocturna oficial con redondeo
      5B-TAG   : Marcar registros con hora extra < 1 hora
      5B       : Recalcular horas extras
      5B-2     : Recalcular campos derivados de horas extras
      5B-3     : Actualizar alerta06 (excede razonabilidad)
      5B-4     : Recalcular rangos H25%/H35%/H50%
      5C       : Recontar NUMMARCACIONES segun marcas reales
      5D       : Limpiar alerta01='MI' cuando depuracion completo marcas
      5E       : Recalcular horas_no_trabajadas y alerta03
      5F       : Eliminar marcas phantom/duplicadas de SCA_HISTORIAL
    
    FASE 7 - DESCANSO:
      7A       : Ajustar entrada anticipada en dia de descanso
      7        : Descanso con marcaciones reales (recalcular hrs/dobles)
    
    FASE 8 - SINCRONIZACION FINAL:
      8-PRE    : Insertar marcas faltantes en SCA_HISTORIAL
      8        : Sincronizar NUMMARCACIONES con SCA_HISTORIAL
    
    CASOS QUE RESUELVE:
    ===================
    CASO 1: Empleado con 1-2 marcaciones -> Completar ENTRADA/SALIDA segun horario
    CASO 1C: ENTRADA = SALIDA (marca duplicada) -> Corregir SALIDA con horario
    CASO 1C-NOC: Marca duplicada en turno nocturno -> ENTRADA=horario nocturno, SALIDA=marca real
            Ejemplo: Horario 23:00->07:00, unica marca a las 07:16
                  -> 07:16 es SALIDA real, ENTRADA=entrada_fijada (23:00)
    CASO 2: Empleado con 3 marcaciones -> Completar INIREFRI o FINREFRI
    CASO 2B: INIREFRI/FINREFRI incorrectos -> Limpiar y poner horario teorico
    CASO 3: Entrada anticipada -> Ajustar ENTRADA = ENTRADA_FIJADA - 15 min
    CASO 5: Turno nocturno (3er turno) -> Mover marca de madrugada a SALIDA del dia anterior
            Ejemplo: 28/03 ENTRADA=23:00 sin SALIDA + 29/03 ENTRADA=07:07
                  -> 28/03 ENTRADA=23:00, SALIDA=07:07 + 29/03 ENTRADA=NULL
    CASO 5B: Salida nocturna mal ubicada -> Mover SALIDA a ENTRADA
            Ejemplo: 29/03 sin ENTRADA, SALIDA=22:56 (nocturna)
                  -> 29/03 ENTRADA=22:56, SALIDA=NULL
    CASO 5C: Marca de manana en ENTRADA de turno nocturno -> Mover a SALIDA
            Ejemplo: Horario 23:00->07:00, ENTRADA=07:16, SALIDA=NULL
                  -> ENTRADA=23:00 (teorica), SALIDA=07:16 (marca real)
    CASO 5D: Post-N1: SALIDA vespertina huerfana -> Mover a ENTRADA
            Ejemplo: Horario 19-03, Descanso. Despues de N1: ENTRADA=NULL, SALIDA=18:55
                  -> ENTRADA=18:55, SALIDA=NULL (es entrada de nuevo turno nocturno)
    CASO 7: Marcaciones consecutivas anomalas (SALIDA - ENTRADA < 1 hora)
            Ejemplo: 08:00, 08:01, 08:02, 08:03 (4 marcas en 3 minutos)
                  -> Conservar ENTRADA real, resto con horario teorico
    CASO 8: Descanso con marcaciones reales
            Cuando: descanso='S' pero empleado tiene ENTRADA y SALIDA reales
            El tareo calcula mal porque horario=00:00 y tothoras=NULL
            Accion: Recalcular tothoramarcas, horadobles, nummarcaciones
            - horaefectiva = NULL (no hay jornada laboral en descanso)
            - horadobles = tothoramarcas (TODAS las horas son dobles)
            - nummarcaciones = conteo real de campos (E/IR/FR/S)
            - Para nocturnos: si salida < entrada, sumar 1 dia al calculo
    CASO 8B: Entrada anticipada en descanso
            Cuando: descanso='S' con marcaciones Y la entrada es mas de 
            15 min antes del horario normal del turno.
            En descanso, entrada_fijada es NULL porque horario=00:00.
            Se busca la hora de ingreso de un dia NO-descanso del mismo
            HORID en SCA_HORARIO_DET como referencia.
            Accion: Ajustar entrada a horario_normal - 15min (regla E2)
            - Se ejecuta ANTES de PASO 7 para que dobles se calculen bien
            - Se registra horaantesentrada (cuanto llego antes)
            - Se llena entrada_fijada con la hora del turno normal
    CASO 9: Hora extra menor a 1 hora
            Cuando: El tareo calcula horaextra > 0 pero el empleado salio
            menos de 1 hora despues de su horario de salida.
            Regla: Solo se pagan horas extras si el exceso >= 1 hora.
            Accion: Marcar registro para que PASO 5B lo limpie a 00:00.
    CASO 10: 3er Turno con entrada anticipada y sobretiempo (FIX 13/04/2026)
            Cuando: Horario TERCER TURNO (23:00-07:00) pero empleado entra
            a las 19:00 (7PM) para hacer sobretiempo.
            Ejemplo: 3 marcas: 18:59 (ENTRADA), 21:03 (INIREFRI), 07:00 (SALIDA)
            Accion: 
            - NO ajustar entrada (es sobretiempo valido)
            - Asignar marca intermedia como INIREFRI
            - Calcular FINREFRI = INIREFRI + 30min (refrigerio obreros)
            Nota: Obreros SIEMPRE tienen 30 min de refrigerio, incluso nocturno.
    CASO 11: Marca duplicada cerca de entrada (FIX 13/04/2026)
            Cuando: Marca a menos de 30 min de entrada/salida confundida
            como refrigerio. Ejemplo: entrada 18:56, marca 18:57 asignada
            como IniRefri cuando deberia ignorarse.
            Accion: PASO 2B-PRE ahora excluye marcas <30 min de bordes.
    CASO 12: Marca faltante en SCA_HISTORIAL (FIX 14/04/2026)
            Cuando: El tareo tiene campos poblados (E/IR/FR/S) que no tienen
            marca correspondiente en SCA_HISTORIAL. Ejemplo: E=06:53,
            IR=11:28, FR=11:55, S=19:02 pero solo 3 marcas en SCA_HISTORIAL
            (falta 19:02). PASO 8 sincroniza nummarcaciones=3 -> IMPAR.
            Accion: PASO 8-PRE inserta las marcas faltantes antes de contar.
    CASO 16: Turno vespertino con entrada mal asignada - marca temprana ignorada (FIX 21/04/2026)
            Cuando: Horario vespertino (ej: 19:00-03:00), existe una marca temprana
            en SCA_HIS en la ventana del inicio de turno (ej: 18:48) pero Aquarius
            asigno una marca mas tardia (ej: 22:47) como entrada.
            Deteccion: entrada actual > entrada_fijada + 2h Y existe marca valida
            en SCA_HIS del mismo dia dentro de [entrada_fijada-2h, entrada_fijada+2h]
            Y anterior a la entrada actual.
            Ejemplo: MATTOS 037740, 13/04/2026
              SCA_HIS 13/04: 18:48 FC, 22:47 FC, 03:00 Manual
              Tareo Aquarius: E=22:47 -> deberia ser E=18:48
            Accion: actualizar entrada a la primera marca valida en la
            ventana de inicio de turno. Codigo: N7
    CASO 15: Turno nocturno - sobretiempo posterior a salida programada (FIX 21/04/2026)
            Cuando: Horario nocturno (ej: 19:00-03:00), Aquarius asigna una marca
            manual del MISMO DIA (ej: 03:00 del 17/04) como salida. Pero la salida
            REAL es una marca Fotocheck del DIA SIGUIENTE (ej: 07:03 del 18/04).
            El empleado trabajo 4h de sobretiempo mas alla de la salida programada.
            Ejemplo: MATTOS 037740, 17/04/2026
              Marcas: 17/04 03:00 Manual, 17/04 18:56 FC, 17/04 20:06 M, 17/04 20:36 FC
                      18/04 07:03 FC (salida real con sobretiempo)
              Tareo: E=18:56, IR=20:06, FR=20:36, S=03:00 (INCORRECTO)
              Detectar: t.salida < t.entrada (03:00 17/04 < 18:56 17/04 = mismo dia)
              Fix: S=07:03 del 18/04 (sobretiempo real de 4h)
            Accion: PASO 0B3c busca marca real en dia+1 > salida actual y no usada
            como entrada del dia siguiente. Codigo: N6
    CASO 13: Horario nocturno puro sin entrada anticipada (FIX 14/04/2026)
            Cuando: Horario 23:00-07:00, entrada cercana a las 23:00 (normal)
            El tareo asigno INIREFRI una marca cercana a ENTRADA pero es
            DUPLICADO de entrada, no refrigerio. Jornada de 8h no tiene refri.
            Ejemplo: APOLINARIO 09/04: E=22:54, IR=22:55, S=07:00
            22:55 esta a 1 min de 22:54 = duplicado, no es refrigerio.
            Accion: PASO 0-NOC-REF limpia inirefri/finrefri y elimina
            la marca duplicada de SCA_HISTORIAL.
    
    EXCLUSION DE PERMISOS:
    ======================
    Se verifican los campos per_* en SCA_ASISTENCIA_TAREO (poblados por el tareo).
    Si el empleado tiene algun permiso activo, NO se le completan marcaciones.
    
    Campos verificados:
    - per_desc_med   : Descanso Medico   - per_vaca      : Vacaciones
    - per_subsidio   : Subsidio          - per_suspension: Suspension
    - per_lic_pat    : Lic. Paternidad   - per_lic_fac   : Lic. Fallecimiento
    - per_goce       : Permiso Con Goce  - per_sgoce     : Permiso Sin Goce
    
    NOTA: No se manejan duplicados de lectora porque SCA_ASISTENCIA_TAREO 
          ya tiene las marcaciones consolidadas (1 registro por dia/empleado).
    
    CODIGOS DE DEPURACION (CODAUX4 / CODAUX5):
    ==========================================
    N1 = Turno nocturno: Entrada movida a salida dia anterior
    N2 = Turno nocturno: Salida movida a entrada
    N3 = Turno nocturno: Marca manana movida a salida
    N4 = Turno nocturno: Salida vespertina reubicada como entrada post-N1
    N5 = Turno nocturno: Salida teorica completada (sin marca forward)
    N6 = Turno nocturno: Salida extendida con marca real posterior (sobretiempo)
    N7 = Turno vespertino: Entrada corregida a marca temprana del mismo dia
    E1 = Entrada completada con horario teorico
    E2 = Entrada anticipada ajustada (-15min)
    E3 = Salida corregida por entrada duplicada
    E4 = Entrada nocturna por marca duplicada en manana
    S1 = Salida completada con horario teorico
    R1 = Refrigerio completo con horario teorico
    R2 = IniRefri calculado desde fin
    R3 = FinRefri calculado desde inicio
    R4 = IniRefri recuperado desde SCA_HISTORIAL
    R5 = FinRefri recuperado desde SCA_HISTORIAL
    R6 = Refrigerio anomalo reasignado (duracion < 50% teorico)
    A1 = Marcas anomalas corregidas (4 en <1hr)
    RC = Horas recalculadas
    DC = Descanso con marcaciones: hrs/dobles/nummarcas recalculadas
    HE = Hora extra < 1h limpiada (tareo calculaba extras incorrectos)
    MF = Marca faltante insertada en SCA_HISTORIAL desde tareo
    RN = Nocturno sin entrada anticipada: refrigerio limpiado (marca duplicada)
    
    TABLAS:
    =======
    ESCRITURA:
    - SCA_ASISTENCIA_TAREO : Marcaciones procesadas (UPDATE)
    - SCA_HISTORIAL        : Registro de marcas generadas (INSERT/DELETE)
    
    CONSULTA:
    - SCA_ASISTENCIA_TAREO  : Datos del dia anterior/siguiente (nocturnos)
    - SCA_HISTORIAL         : Recuperar marcas intermedias (2B-PRE)
    - SCA_HORARIO_DET       : Horario del turno normal (descanso 8B)
    - SCA_HORARIO_CAB       : Descripcion/tipo horario (5G)
    
    SECUENCIAS:
    - id_cod_seq : Para SCA_HISTORIAL.IDCOD
    
    REGLAS DE CALCULO:
    ==================
    - HORAEFECTIVA se toma de TOTHORAS (teorico), NO se calcula de marcas
    - HORAEXTRA solo aplica si excede 1 hora DESPUES del horario de salida
    - HORAEXTANTES solo aplica si excede 1 hora ANTES del horario de entrada
    - Al recalcular HORAEXTRA/TOTALHORASEXTRAS, tambien se recalculan:
      HORADESPUESSALIDA, HORAEXTRAOFI, TOTALHORASEXTRASOFI,
      HORAEXTRA_AJUS (redondeado por AJUSTE_HEXTRA), ALERTA06,
      HORAEXTRA1 (H25%), HORAEXTRA2 (H35%), HORAEXTRA3 (H50%),
      HORAEXOFI1, HORAEXOFI2, HORAEXOFI3 (oficiales con HORAEXTRA_AJUS)
    - Minutos de horas extras NO se consideran (se trunca a horas completas)
    - Al completar marcaciones, se inserta registro en SCA_HISTORIAL
    - DESCANSO con marcaciones: se recalcula tothoramarcas, horadobles
      y nummarcaciones. horaefectiva se pone NULL (no hay jornada).
      Todas las horas son dobles. Para nocturnos se ajusta
      la diferencia sumando 1 dia cuando salida < entrada.
      PASO 7 es idempotente: se puede re-ejecutar sin duplicar DC.
    - DESCANSO entrada anticipada: si entrada < horario_normal - 15min,
      se ajusta entrada. Se usa SCA_HORARIO_DET (dia no-descanso)
      para obtener la hora de ingreso del turno. PASO 7A va antes de PASO 7.
    
    Si hay multiples correcciones:
    - CODAUX4 concatena codigos con '|' (ej: 'E1|R1|RC')
    - CODAUX5 solo guarda la PRIMERA descripcion (por limite de 50 chars)
    
    CURSOR DE RESULTADO (DEPURA_TOTAL):
    ====================================
    resultado                       : 'OK' o 'ERROR: ...'
    fecha_proceso                   : Fecha procesada
    turnos_nocturnos_corregidos     : PASOs 0A-0D
    entradas_completadas            : PASOs 1, 1C-NOC, 1C
    entradas_anticipadas_ajustadas  : PASO 1B
    salidas_completadas             : PASO 2
    inirefri_completados            : PASOs 2B-PRE, 2B, 3A
    finrefri_completados            : PASOs 2B-PRE, 2B, 3B
    marcaciones_anomalas            : PASO 4B
    nocturnos_sin_refri_limpiados   : PASO 3C-NOC
    horas_recalculadas              : PASOs 5, 5B, 7
    total_marcas_generadas          : Suma de marcas completadas
    marcas_historial_insertadas     : INSERTs en SCA_HISTORIAL
    
    HISTORIAL DE CAMBIOS:
    =====================
    07/04/2026 - Creacion inicial
    10/04/2026 - FIX: Horas extras breakdown (horaextra1/2/3)
    12/04/2026 - FIX: PASO 0-DUP, 0-ORF nuevos. PASO 0-PRE actualiza alerta01
    12/04/2026 - FIX: PASO 2B-PRE excluye marcas <30 min de bordes
    12/04/2026 - FIX: PASO 5G movido ANTES de PASO 5
    13/04/2026 - FIX: PASO 5G maneja 3er turno con sobretiempo + refrigerio
    13/04/2026 - FIX: PASO 8 sincroniza NUMMARCACIONES con SCA_HISTORIAL
    13/04/2026 - NEW: DEPURA_RANGO para procesar rangos de fecha
    13/04/2026 - DEL: Removido PASO 4 (dia completo forzado para 0-2 marcas)
                      Logica extraida a PASO4_Completar_DiaCompleto.sql
                      El paquete solo procesa empleados con >= 1 marcacion
    14/04/2026 - FIX: Removida exclusion nocturna de PASOs 2A,2B-PRE,2B,3A,3B,4B
                      3er turno SI tiene refrigerio (23:00-23:30 segun horario)
                      La guardia horiniref IS NOT NULL ya filtra horarios sin refri
    14/04/2026 - FIX: NOT EXISTS en INSERT SCA_HISTORIAL - removida condicion h.fecha
                      Causa: entrada_fijada(09/04) vs horiniref(01/01/1900) generaba
                      marcas duplicadas a las 23:00 (5 marcas impar)
    14/04/2026 - NEW: PASO 8-PRE: Insertar marcas faltantes en SCA_HISTORIAL
                      Caso: Tareo tiene campo poblado (ej: Salida=19:02 REAL)
                      pero marca no existe en SCA_HISTORIAL -> PASO 8 cuenta
                      menos marcas de las reales -> MARCACION IMPAR falsa
    14/04/2026 - FIX: INSERTs SCA_HISTORIAL usaban fecha del campo DATE (01/01/1900)
                      en vez de fechamar. 14 INSERTs corregidos para usar fechamar.
                      Causa: marcas no aparecian en UI porque fecha era incorrecta.
    14/04/2026 - NEW: PASO 3C-NOC: Horario nocturno sin entrada anticipada
                      CASO 13: Horario 23:00-07:00 sin entrada anticipada NO tiene
                      refrigerio. Se ejecuta DESPUES de asignar refrigerio para
                      limpiar lo que no corresponde. Elimina inirefri/finrefri.
    20/04/2026 - FIX: PASO 0-RESTORE-B protege marcas de refri reales cuando inirefri=NULL
                      Causa: fotocheck 034482 requeria 2 ejecuciones para resolver salida
                      correctamente. La marca real de refrigerio (13:03) era eliminada por
                      0-RESTORE-B porque inirefri=NULL hace la proteccion siempre TRUE.
                      Solucion: umbral 90 min (igual que 0-ORF) para no eliminar marcas
                      cercanas al horario teorico de refrigerio.
    20/04/2026 - FIX: ind_anulado/ind_noprocesar agregados a queries SCA_HISTORIAL en
                      PASOs 0-DUP, 0-ORF, 0-PRE, 2A, 2B-PRE, 5F, 8-PRE (7 PASOs)
                      Causa: marcas anuladas en UI eran contadas/usadas como activas
    20/04/2026 - FIX: PASO 0-ORF excluye inirefri/finrefri del tareo en conteo y DELETE
                      Causa: podia eliminar marcas de refri legitimas como "huerfanas"
    20/04/2026 - FIX: PASO 5 calculo tothoramarcas para turnos nocturnos (salida<entrada)
                      Causa: resultado negativo cuando salida amanecer < entrada noche
    20/04/2026 - FIX: PASO 8 y 8B manejan horiniref IS NULL (tratado como '00:00')
                      Causa: empleados sin horario asignado eran excluidos de PASO 8
    20/04/2026 - NEW: PASO 3D: Corregir salida imposible (salida < inirefri)
                      Causa: PASO 2B asignaba refrigerio teorico sin verificar si
                      la salida existente era ANTES del horiniref teorico.
                      Ejemplo: CERVANTES 037810, 14/04/2026
                        Marcas: 06:47 FC, 12:17 FC, 12:30 Manual, 13:00 Manual
                        PASO 2B: IR=12:30, FR=13:00 (teorico), S=12:17 -> S < IR = ERROR
                      Fix FASE A: buscar marca oculta (ind_anulado/noprocesar) despues
                        de finrefri, cercana a salida_fijada. Si existe -> SSR (Salida real
                        Restaurada): insertar visible en SCA_HISTORIAL, usar como salida.
                      Fix FASE B: sin marca oculta -> salida_fijada (SS como antes)
                      ROLLBACK: R-SSR revierte salida=NULL; marca DEPURACION% se elimina.
    20/04/2026 - BUG FIX critico en PASO 3D condicion de disparo:
                      inirefri asignado por PASO 2B tiene base 01/01/1900 (horiniref),
                      salida tiene fecha real 2026. Comparacion DATE: 2026 < 1900 = FALSE.
                      PASO 3D NUNCA disparaba para caso CERVANTES (2B teorico).
                      Fix: comparar solo HORA via TO_CHAR(campo, 'HH24MI').
                      Nocturno excluido: entrada_fijada >= 20:00 (salida 07:00 < IR 23:00
                      es correcto cronologicamente en turno nocturno).
    20/04/2026 - BUG FIX en PASO 2B-PRE ventana de busqueda:
                      Cuando salida(hora) < horiniref(hora) = estado imposible, los
                      manuales agregados DESPUES de salida erronea (ej: 12:30, 13:00
                      > salida=12:17) quedaban fuera del limite salida-30min=11:47.
                      Fix: si TO_CHAR(salida,'HH24MI') < TO_CHAR(horiniref,'HH24MI')
                      extender ventana superior a salida_fijada-30min.
                      Resultado: PASO 2B-PRE encuentra marcas reales como refri;
                      PASO 2B teorico ya no aplica; PASO 3D corrige salida.
    20/04/2026 - NEW: PASO 3E: Corregir refrigerio imposible (inirefri < entrada)
                      Causa: empleado llego tarde sin FC de entrada; alguien cargo
                      manuales de refrigerio ANTES de que llegara; Aquarius asigno
                      primera FC como entrada y los manuales como inirefri/finrefri.
                      Resultado: inirefri(12:30) < entrada(13:06) = IMPOSIBLE.
                      Ejemplo: ARREDONDO MACO 032933, 16/04/2026
                        Marcas: 12:30 Manual, 13:00 Manual, 13:06 FC, 15:01 FC
                        Tareo: E=13:06, IR=12:30, FR=13:00 -> IR < E = error
                      Fix: limpiar inirefri/finrefri (marcas quedan en SCA_HIS).
                      Resultado: E=13:06, S=15:01, sin refri, tardanza=06:06 (real)
                      Codigo: RI = Refrigerio Imposible limpiado
    21/04/2026 - FIX: PASO 3B fallback horfinref-horiniref cuando totref es NULL/0
                      Caso: empleado diurno (07:00-15:45, refri 13:15-14:00=45min)
                      tiene 3 marcas: 06:49, 13:38 (IniRefri), 17:26.
                      PASO 3B bloqueaba porque totref IS NULL o totref=01/01/1900.
                      Fix: si totref no disponible, usar (horfinref - horiniref)
                      como duracion de refrigerio. Ejemplo: 13:38 + 45min = 14:23.
    21/04/2026 - NEW: PASO 0B3d: Turno vespertino - entrada mal asignada
                      CASO 16: Aquarius asigna una marca tardia (ej: 22:47) como
                      entrada de turno vespertino (19:00-03:00) ignorando la marca
                      real temprana en SCA_HIS (ej: 18:48, dentro de ventana 17:00-21:00).
                      Deteccion: entrada actual > entrada_fijada + 2h Y existe marca
                      en SCA_HIS en [entrada_fijada-2h, entrada_fijada+2h] < entrada.
                      Accion: entrada = MIN(marca en ventana) de SCA_HIS.
                      Requisito: salida > entrada (cross-day, salida ya corregida).
                      Codigo: N7. Posterior PASO 2B-PRE asigna refrigerio si existe.
    21/04/2026 - FIX: PASO 0B3c condicion >= en lugar de > para hora de salida
                      Caso 13/04: salida errónea almacenada como 13/04 07:01 (mismo dia).
                      La marca real en SCA_HIS 14/04 es exactamente '07:01:00'.
                      Con > : '07:01:00' > '07:01:00' = FALSE -> no encontraba la marca.
                      Con >=: '07:01:00' >= '07:01:00' = TRUE -> encuentra y corrige.
                      Diferencia con 17/04: ahi salida=03:00 y real=07:03 -> > pasaba OK.
    21/04/2026 - FIX: PASO 5B y 5B-2 ajuste nocturno de salida_fijada
                      Aquarius almacena salida_fijada en la fecha del dia (ej: 17/04 03:00)
                      para turnos que cruzan medianoche (HORARIO 19:00-03:00).
                      Salida real dia+1 (18/04 07:03) - salida_fijada(17/04 03:00) = 28h.
                      Fix: detectar con salida_fijada < entrada_fijada (DATE) y agregar
                      1 dia a salida_fijada antes de calcular horaextra/horadespuessalida.
    21/04/2026 - NEW: PASO 0B3c: Turno nocturno - salida extendida (sobretiempo)
                      CASO 15: Aquarius asigna marca manual del mismo dia como salida
                      de turno nocturno, ignorando marca Fotocheck posterior del dia+1.
                      Ejemplo: MATTOS 037740, 17/04: S=03:00 (Manual 17/04, INCORRECTO)
                      -> S=07:03 (Fotocheck 18/04, 4h sobretiempo real)
                      Deteccion: t.salida < t.entrada (salida es del mismo dia, antes
                      de la entrada nocturna -> invalida cronologicamente).
                      Condicion extra: marca en SCA_HIS dia+1 con hora > salida actual
                      y < 12:00 y NO usada como entrada del tareo dia+1.
                      Accion: UPDATE salida = marca dia+1 (con fecha real 18/04).
                      Codigo: N6. PASO 5 recalcula tothoramarcas/extras automaticamente.
    21/04/2026 - BUG FIX: PASO 3D interferencia con N6 (salida dia+1 < inirefri-hora)
                      PASO 3D detecta salida < inirefri comparando solo hora. Para N6
                      (salida=18/04 07:03, HH24MI='0703') e inirefri=20:06, '0703'<'2006'=TRUE
                      pero la salida YA esta corregida. Ademas entrada_fijada=19:00 < '2000'
                      no excluia el caso. Fix: AND NOT codaux4 LIKE '%N6%'.
    21/04/2026 - BUG FIX: PASO 8-PRE insertaba marca espuria cross-day (N6)
                      Para N6, salida=18/04 07:03 en tareo fechamar=17/04. NOT EXISTS
                      buscaba fec_equiv=17/04 hora='07:03' y no la encontraba (esta en
                      fec_equiv=18/04) -> insertaba DEPURACION '07:03' en 17/04 -> espuria.
                      Fix: solo insertar SALIDA si TRUNC(salida) = fechamar (mismo dia).
    21/04/2026 - NEW: ROLLBACK R-N6: revertir salida extendida dia+1
                      FASE 2 del ROLLBACK revierte solo salida=salida_fijada; N6 pone
                      salida con fecha real dia+1 -> no revertia -> quedaba valor N6.
                      Fix: nuevo PASO R-N6 antes de FASE 2: salida=NULL para '%N6%'.
    21/04/2026 - NUEVO PASO 6-PHANTOM-D: Limpiar entrada madrugada en DESCANSO cuando es la
                      salida del turno nocturno anterior asignada por Aquarius como entrada del
                      dia de descanso. Ejemplo: LA TORRE 032559, 19/04/2026 (Domingo descanso):
                      07:00 FC (salida turno Sabado 18/04) asignado como entrada Domingo ->
                      PASO 7A calculaba horaantesentrada=16:00 y ajustaba entrada=22:45.
                      Fix: nuevo PASO 6-PHANTOM-D: descanso='S' + entrada<08:00 + ayer nocturno
                      -> limpia entrada/salida/horaantesentrada/horaextantes antes de PASO 7A.
    21/04/2026 - BUG FIX: PASO 2B-PRE decision IniRefri/FinRefri con fechas de distintas epocas
                      v_marca_inter tiene fecha real (2026); horiniref/horfinref base 1900.
                      ABS(marca_2026 - hor_1900) siempre ~46000 dias. El lado con menor
                      diferencia de HORAS quedaba con mayor valor absoluto de FECHA ->
                      siempre asignaba como FinRefri aunque estuviera mas cerca de IniRefri.
                      Fix: mismo patron TO_CHAR('HH24')*60 + MI para la comparacion.
                      Ejemplo: QUIÑONES 034083, 15/04: marca 13:38, horini=13:15, horfin=14:00
                      |13:38-13:15|=23min vs |13:38-14:00|=22min -> antes: FR, ahora: IR ✓
                      -> IR=13:38 (real), FR=14:23 (PASO 3B calcula ini+45min).
    21/04/2026 - BUG FIX: PASO 2B-PRE validacion 2h con fechas de distintas epocas
                      v_marca_inter/fin tienen fecha real (2026); rec.horiniref/horfinref
                      tienen base 01/01/1900. ABS(fecha_2026 - fecha_1900) = ~46000 dias
                      >>> 120/1440 = 0.0833 -> la condicion siempre era TRUE -> la marca
                      valida siempre se descartaba -> PASO 2B asignaba teorico base-1900
                      -> PASO 5 calculaba 00:00 (fechas mezcladas).
                      Fix: comparar solo minutos del dia usando TO_CHAR('HH24')*60 + MI.
                      Aplica a ambas validaciones (v_marca_inter y v_marca_fin).
                      Ejemplo: RODRIGO 032666, 17/04: 3 FC (06:48, 12:00, 15:07)
                      -> IR=12:00 (real), FR=12:30 (PASO 3B), S=15:07, 4 marcaciones.
    21/04/2026 - FIX: PASO 3A simetrico con PASO 3B (fallback horfinref-horiniref)
                      PASO 3A (completa INIREFRI dado FINREFRI) solo usaba totref.
                      Si totref IS NULL o = 01/01/1900, no calculaba aunque horfinref-
                      horiniref este disponible. Igual asimetria que tenia PASO 3B.
                      Fix: mismo CASE WHEN totref / ELSE (horfinref-horiniref) + misma
                      condicion OR en WHERE que se aplico a PASO 3B el 21/04.
    23/04/2026 - FIX: PASO 2B-PRE segunda marca excluye near-duplicados de la primera
                      Caso: 5 marcas (08:51, 12:44, 12:45, 13:18, 18:03); 12:44/12:45 a 1 min.
                      PASO 2B-PRE elegia 12:45 como finrefri (15 min de horfinref=13:00)
                      en lugar de 13:18 (18 min). Fix: excluir marcas a < 5 min de inirefri.
                      PASO 5F (orphan cleanup) elimina 12:45 de SCA_HIS post-asignacion.
                      Fotocheck 034572, 21/04/2026.
    23/04/2026 - BUG FIX: PASO 0-RESTORE reinsertaba near-dup finrefri como DEPURACION
                      Causa raiz del caso 034572 21/04: PASO 0-DUP elimino 12:45 (near-dup
                      de 12:44, 1 min). PERO el tareo de Aquarius tenia finrefri=12:45
                      (Aquarius asigno las 2 primeras marcas intermedias como refri aunque
                      impar). PASO 0-RESTORE detecta finrefri IS NOT NULL y lo reinserta
                      como 'DEPURACION: Marca restaurada finrefri 0-REST'.
                      PASO 5F no puede eliminar marcas DEPURACION (motivo LIKE 'DEPURACION%')
                      -> 12:45 DEPURACION queda en SCA_HIS -> cuenta=5 (impar) -> ciclo.
                      Mismo problema con Aquarius null: si inirefri=finrefri=01/01/1900 00:00,
                      0-RESTORE inserta una marca a las '00:00' (diff=0) en SCA_HIS.
                      Fix: agregar AND (t.finrefri - t.inirefri) >= (5/1440) al cursor
                      de 0-RESTORE. Threshold = mismo que 0-DUP (<5 min elimina near-dup).
                      Cubre: near-dup 1 min (< 5/1440) y epoch null (diff=0 < 5/1440).

    20/04/2026 - BUG FIX en PASO 5 horaefectiva:
                      Anterior: horaefectiva = tothoras siempre que existia (08:00).
                      Esto era incorrecto cuando el empleado solo trabajo una fraccion
                      del dia (ej: ARREDONDO llego 6h tarde, horaefectiva debia ser
                      01:55 no 08:00). Comportamiento verificado: Aquarius usa
                      horaefectiva = tothoras para dias completos (gross = tothoras);
                      refri se trackea en horarefrigerio separado, no se deduce.
                      Fix: horaefectiva = LEAST(gross_actual, tothoras)
                      Casos: dia completo 08:00=08:00 ✓, solo 01:55<08:00→01:55 ✓,
                             overtime 12:00>08:00→08:00 ✓, nocturno ajuste +1dia ✓.
*******************************************************************************/


/*******************************************************************************
    ESPECIFICACION DEL PAQUETE (PACKAGE SPECIFICATION)
*******************************************************************************/
CREATE OR REPLACE PACKAGE PKG_SCA_DEPURA_TAREO AS

    /***************************************************************************
        DEPURA_TOTAL
        
        Procedimiento PRINCIPAL que ejecuta todo el proceso de depuracion.
        TRANSACCION ATOMICA: Todo tiene exito o todo falla.
        
        PARAMETROS:
        - p_cod_empresa   : NULL = todas las empresas
        - p_cod_personal  : NULL = todos los empleados
        - p_fecha         : Fecha a procesar (dd/MM/yyyy)
        - p_solo_obreros  : 'S' = Solo procesar obreros
                           'N' = Procesar todos
        - cv_resultado    : Cursor con resumen de operacion
        
        NOTA: Solo procesa empleados con >= 1 marcacion.
              Empleados con 0 marcaciones NO se tocan.
    ***************************************************************************/
    PROCEDURE DEPURA_TOTAL(
        p_cod_empresa    IN VARCHAR2 DEFAULT NULL,
        p_cod_personal   IN VARCHAR2 DEFAULT NULL,
        p_fecha          IN VARCHAR2,
        p_solo_obreros   IN VARCHAR2 DEFAULT 'S',
        cv_resultado     OUT SYS_REFCURSOR
    );
    
    /***************************************************************************
        ROLLBACK_MARCACIONES
        
        Revierte las marcaciones generadas automaticamente.
        Detecta cuales fueron auto-generadas comparando con horario teorico.
        
        PARAMETROS:
        - p_cod_empresa  : NULL = todas las empresas
        - p_cod_personal : NULL = todos los empleados
        - p_fecha        : Fecha a procesar (dd/MM/yyyy)
        - cv_resultado   : Cursor con resumen de operacion
    ***************************************************************************/
    PROCEDURE ROLLBACK_MARCACIONES(
        p_cod_empresa   IN VARCHAR2 DEFAULT NULL,
        p_cod_personal  IN VARCHAR2 DEFAULT NULL,
        p_fecha         IN VARCHAR2,
        cv_resultado    OUT SYS_REFCURSOR
    );
    
    /***************************************************************************
        VER_ESTADO
        
        Consulta el estado actual de las marcaciones de un empleado.
        Util para verificar antes/despues de ejecutar depuracion.
        
        PARAMETROS:
        - p_cod_empresa  : Codigo de empresa
        - p_cod_personal : Codigo de personal
        - p_fecha        : Fecha a consultar (dd/MM/yyyy)
        - cv_resultado   : Cursor con datos del empleado
    ***************************************************************************/
    PROCEDURE VER_ESTADO(
        p_cod_empresa   IN VARCHAR2,
        p_cod_personal  IN VARCHAR2,
        p_fecha         IN VARCHAR2,
        cv_resultado    OUT SYS_REFCURSOR
    );

    /***************************************************************************
        DEPURA_RANGO
        
        Ejecuta DEPURA_TOTAL para un RANGO de fechas (fecha_inicio a fecha_fin).
        Funciona tanto masivo (todos los empleados) como individual.
        Cada dia se procesa en su propia transaccion (COMMIT independiente).
        Si un dia falla, continua con el siguiente.
        
        PARAMETROS:
        - p_cod_empresa    : NULL = todas las empresas
        - p_cod_personal   : NULL = todos los empleados
        - p_fecha_inicio   : Fecha inicio del rango (dd/MM/yyyy)
        - p_fecha_fin      : Fecha fin del rango (dd/MM/yyyy)
        - p_solo_obreros   : 'S' = Solo obreros / 'N' = Todos
        - cv_resultado     : Cursor con resumen consolidado por fecha
        
        NOTA: Solo procesa empleados con >= 1 marcacion.
    ***************************************************************************/
    PROCEDURE DEPURA_RANGO(
        p_cod_empresa    IN VARCHAR2 DEFAULT NULL,
        p_cod_personal   IN VARCHAR2 DEFAULT NULL,
        p_fecha_inicio   IN VARCHAR2,
        p_fecha_fin      IN VARCHAR2,
        p_solo_obreros   IN VARCHAR2 DEFAULT 'N',
        cv_resultado     OUT SYS_REFCURSOR
    );

    /***************************************************************************
        CONSULTAR_RANGO

        Retorna un cursor con el estado de marcaciones para un rango de fechas.
        Util para consultar desde un proyecto externo sin ejecutar depuracion.
        Incluye: horario teorico, marcaciones actuales, horas calculadas,
        alertas, codigos de depuracion y diagnostico (caso_aplica / problema).

        PARAMETROS:
        - p_cod_empresa      : NULL = todas las empresas
        - p_cod_personal     : NULL = todos los empleados
        - p_fecha_inicio     : Fecha inicio del rango (dd/MM/yyyy)
        - p_fecha_fin        : Fecha fin del rango (dd/MM/yyyy)
        - p_solo_obreros     : 'O'=Solo obreros / 'E'=Solo empleados / 'T'=Todos
        - p_solo_pendientes  : 'S'=Solo dias con caso_aplica (necesitan depuracion)
                               'N'=Todos los dias (default)
        - cv_resultado       : Cursor con filas de marcaciones + diagnostico

        CAMPO EXTRA EN CURSOR:
        - tiene_pendiente    : 'S' si ese dia necesita depuracion, 'N' si OK
                               Util para resaltar en grilla sin filtrar
    ***************************************************************************/
    PROCEDURE CONSULTAR_RANGO(
        p_cod_empresa      IN VARCHAR2 DEFAULT NULL,
        p_cod_personal     IN VARCHAR2 DEFAULT NULL,
        p_fecha_inicio     IN VARCHAR2,
        p_fecha_fin        IN VARCHAR2,
        cv_resultado       OUT SYS_REFCURSOR
    );

    /***************************************************************************
        BUSCAR_EMPLEADO
        
        Busca empleados activos por nombre (parcial) en SCA_ASISTENCIA_TAREO.
        Retorna cod_personal, num_fotocheck, nombre completo y tip_estado.
        Util para buscar un empleado por nombre y obtener su codigo.
        
        TABLAS:
        - CONSULTA: SCA_ASISTENCIA_TAREO, PLA_PERSONAL
        
        PARAMETROS:
        - p_cod_empresa  : Codigo de empresa (ej: '0003')
        - p_nombre       : Texto parcial del nombre a buscar (case-insensitive)
        - cv_resultado   : Cursor con empleados encontrados
    ***************************************************************************/
    PROCEDURE BUSCAR_EMPLEADO(
        p_cod_empresa   IN VARCHAR2,
        p_nombre        IN VARCHAR2 DEFAULT NULL,
        cv_resultado    OUT SYS_REFCURSOR
    );

END PKG_SCA_DEPURA_TAREO;
/


/*******************************************************************************
    CUERPO DEL PAQUETE (PACKAGE BODY)
*******************************************************************************/
CREATE OR REPLACE PACKAGE BODY PKG_SCA_DEPURA_TAREO AS

    -- =========================================================================
    -- DEPURA_TOTAL: Proceso principal de depuracion
    -- =========================================================================
    PROCEDURE DEPURA_TOTAL(
        p_cod_empresa    IN VARCHAR2 DEFAULT NULL,
        p_cod_personal   IN VARCHAR2 DEFAULT NULL,
        p_fecha          IN VARCHAR2,
        p_solo_obreros   IN VARCHAR2 DEFAULT 'S',
        cv_resultado     OUT SYS_REFCURSOR
    )
    AS
        v_fecha_proceso   DATE;
        v_count_nocturno  NUMBER := 0;
        v_count_entrada   NUMBER := 0;
        v_count_anticipada NUMBER := 0;
        v_count_salida    NUMBER := 0;
        v_count_inirefri  NUMBER := 0;
        v_count_finrefri  NUMBER := 0;
        v_count_anomala   NUMBER := 0;
        v_count_recalculo NUMBER := 0;
        v_count_historial NUMBER := 0;
        v_count_5g        NUMBER := 0;  -- Contador para PASO 5G (3er turno anticipado)
        v_count_rn        NUMBER := 0;  -- Contador para PASO 0-NOC-REF (nocturno sin refrigerio)
        v_count_ss        NUMBER := 0;  -- Contador para PASO 3D (salida imposible corregida)
        v_count_ri        NUMBER := 0;  -- Contador para PASO 3E (refrigerio anterior a entrada)
        v_empleados_sin_tareo NUMBER := 0; -- PRECONDICION: empleados sin fila en SCA_ASISTENCIA_TAREO para p_fecha
        v_empresa_filtro  VARCHAR2(10);
        v_personal_filtro VARCHAR2(10);
        v_error_msg       VARCHAR2(500);
        
        -- Variables para recuperacion de marcas desde SCA_HISTORIAL
        v_marca_inter     DATE;
        v_marca_inter_b   DATE;  -- Segunda marca (FinRefri real cuando hay par)
        v_idcod_inter     NUMBER;
        v_marca_fin       DATE;
        v_mf_insertado    NUMBER := 0;  -- Contador marcas faltantes insertadas por iteracion
        v_salida_teorica  DATE;         -- Para PASO 0-SWAP: salida teorica calculada
        
        -- Codigos de depuracion (CODAUX4)
        c_N1  CONSTANT NVARCHAR2(5)  := N'N1';
        c_N2  CONSTANT NVARCHAR2(5)  := N'N2';
        c_N3  CONSTANT NVARCHAR2(5)  := N'N3';
        c_N4  CONSTANT NVARCHAR2(5)  := N'N4';
        c_N5  CONSTANT NVARCHAR2(5)  := N'N5';
        c_N6  CONSTANT NVARCHAR2(5)  := N'N6';
        c_N7  CONSTANT NVARCHAR2(5)  := N'N7';
        c_E1  CONSTANT NVARCHAR2(5)  := N'E1';
        c_E2  CONSTANT NVARCHAR2(5)  := N'E2';
        c_E3  CONSTANT NVARCHAR2(5)  := N'E3';
        c_E4  CONSTANT NVARCHAR2(5)  := N'E4';
        c_S1  CONSTANT NVARCHAR2(5)  := N'S1';
        c_R1  CONSTANT NVARCHAR2(5)  := N'R1';
        c_R2  CONSTANT NVARCHAR2(5)  := N'R2';
        c_R3  CONSTANT NVARCHAR2(5)  := N'R3';
        c_R4  CONSTANT NVARCHAR2(5)  := N'R4';
        c_R5  CONSTANT NVARCHAR2(5)  := N'R5';
        c_R6  CONSTANT NVARCHAR2(5)  := N'R6';
        c_A1  CONSTANT NVARCHAR2(5)  := N'A1';
        c_RC  CONSTANT NVARCHAR2(5)  := N'RC';
        c_DC  CONSTANT NVARCHAR2(5)  := N'DC';
        c_HE  CONSTANT NVARCHAR2(5)  := N'HE';
        c_HX  CONSTANT NVARCHAR2(5)  := N'HX';  -- HE no oficializada por tareo (ajus=0)
        c_MF  CONSTANT NVARCHAR2(5)  := N'MF';
        c_RN  CONSTANT NVARCHAR2(5)  := N'RN';  -- Refrigerio Nocturno limpiado
        c_NC  CONSTANT NVARCHAR2(5)  := N'NC';  -- Nocturno: marca duplicada limpiada
        c_SS  CONSTANT NVARCHAR2(5)  := N'SS';  -- Salida Swap: imposible, reemplazada con teorico
        c_SSR CONSTANT NVARCHAR2(5)  := N'SSR'; -- Salida Swap Real: oculta restaurada
        c_RI  CONSTANT NVARCHAR2(5)  := N'RI';  -- Refrigerio Imposible: anterior a entrada, limpiado
        c_RT  CONSTANT NVARCHAR2(5)  := N'RT';  -- Refrigerio Truncado: salida antes de finrefri, limpiado
        c_PH  CONSTANT NVARCHAR2(5)  := N'PH';  -- Phantom: descanso con marcas fantasma limpiadas
        c_SEP CONSTANT NVARCHAR2(5)  := N'|';
        
        -- Descripciones de depuracion (CODAUX5) - max 30 chars
        d_N1_ayer CONSTANT NVARCHAR2(30) := N'T.nocturno: salida completada';
        d_N1_hoy  CONSTANT NVARCHAR2(30) := N'Entrada movida a dia anterior';
        d_N2      CONSTANT NVARCHAR2(30) := N'Salida noct movida a entrada';
        d_N3      CONSTANT NVARCHAR2(30) := N'Noct: marca manana es salida';
        d_N4      CONSTANT NVARCHAR2(30) := N'Salida reubicada como entrada';
        d_N5      CONSTANT NVARCHAR2(30) := N'Salida teorica nocturna';
        d_N6      CONSTANT NVARCHAR2(30) := N'Noct: salida extendida DIA+1';
        d_N7      CONSTANT NVARCHAR2(30) := N'Entrada corregida marca previa';
        d_E1      CONSTANT NVARCHAR2(30) := N'Entrada teorica';
        d_E2      CONSTANT NVARCHAR2(30) := N'Entrada ajustada -15min';
        d_E3      CONSTANT NVARCHAR2(30) := N'Salida por entrada duplicada';
        d_E4      CONSTANT NVARCHAR2(30) := N'Entrada noct. marca mañana';
        d_S1      CONSTANT NVARCHAR2(30) := N'Salida teorica';
        d_R1      CONSTANT NVARCHAR2(30) := N'Refrigerio teorico';
        d_R2      CONSTANT NVARCHAR2(30) := N'IniRefri calculado';
        d_R3      CONSTANT NVARCHAR2(30) := N'FinRefri calculado';
        d_R4      CONSTANT NVARCHAR2(30) := N'IniRefri desde historial';
        d_R5      CONSTANT NVARCHAR2(30) := N'FinRefri desde historial';
        d_R6      CONSTANT NVARCHAR2(30) := N'Refri anomalo reasignado';
        d_A1      CONSTANT NVARCHAR2(30) := N'Marcas anomalas corregidas';
        d_RC      CONSTANT NVARCHAR2(30) := N'Horas recalculadas';
        d_DC      CONSTANT NVARCHAR2(30) := N'Descanso: hrs recalculadas';
        d_HE      CONSTANT NVARCHAR2(30) := N'HExtra <1h limpiada';
        d_HX      CONSTANT NVARCHAR2(30) := N'HExtra ajus reoficializada';
        d_MF      CONSTANT NVARCHAR2(30) := N'Marca faltante insertada';
        d_RN      CONSTANT NVARCHAR2(30) := N'Noct.sin refri: marca dup';
        d_NC      CONSTANT NVARCHAR2(30) := N'Noct: salida dup limpiada';
        d_PH      CONSTANT NVARCHAR2(30) := N'Descanso: fantasma limpiado';
        d_SS      CONSTANT NVARCHAR2(30) := N'Salida imposible -> teorico';
        d_SSR     CONSTANT NVARCHAR2(30) := N'Salida real oculta restaurada';
        d_RI      CONSTANT NVARCHAR2(30) := N'Refri anterior a entrada';
        d_RT      CONSTANT NVARCHAR2(30) := N'Refri trunc: salida<finrefri';
    BEGIN
        v_fecha_proceso := TO_DATE(p_fecha, 'dd/MM/yyyy');
        v_empresa_filtro := NVL(p_cod_empresa, '%');
        v_personal_filtro := NVL(p_cod_personal, '%');

        -- =====================================================================
        -- PRECONDICION (informativa): contar empleados objetivo SIN fila en
        -- SCA_ASISTENCIA_TAREO para p_fecha. Estos empleados requieren ejecutar
        -- previamente SP_SCA_Proceso_Trabajador (PASOS 1-14) para tener tareo
        -- calculado. NO aborta el proceso, solo informa en el cursor de salida.
        -- =====================================================================
        SELECT COUNT(*)
        INTO   v_empleados_sin_tareo
        FROM   PLA_PERSONAL p
        WHERE  p.cod_empresa  LIKE v_empresa_filtro
        AND    p.cod_personal LIKE v_personal_filtro
        AND    NOT EXISTS (
            SELECT 1 FROM SCA_ASISTENCIA_TAREO t
            WHERE  t.cod_empresa  = p.cod_empresa
            AND    t.cod_personal = p.cod_personal
            AND    t.fechamar     = v_fecha_proceso
        );

        IF v_empleados_sin_tareo > 0 THEN
            DBMS_OUTPUT.PUT_LINE('AVISO: ' || v_empleados_sin_tareo ||
                ' empleado(s) SIN tareo calculado para ' || p_fecha ||
                ' - Ejecute SP_SCA_Proceso_Trabajador antes de depurar.');
        END IF;

        -- =====================================================================
        -- PASO 0-CLEAN: Eliminar marcas de depuracion anterior en SCA_HISTORIAL
        -- Si el paquete se ejecuto antes, dejo marcas tipo Manual con
        -- motivo='DEPURACION:...' que ya no son validas.
        -- Ejemplo: 3 marcas reales + 2 Manual(13:15, 14:00) de depura anterior
        --          = 5 entradas en SCA_HISTORIAL pero solo 3 son reales.
        -- Se eliminan ANTES de sincronizar nummarcaciones y ANTES de que
        -- PASO 2B-PRE busque marcas intermedias.
        -- =====================================================================
        DELETE FROM SCA_HISTORIAL h
        WHERE h.fec_equiv = v_fecha_proceso
        AND h.motivo LIKE 'DEPURACION%'
        AND h.tiporeg = '3'
        AND h.idtarjeta IN (
            SELECT t.num_fotocheck
            FROM SCA_ASISTENCIA_TAREO t
            WHERE t.fechamar = v_fecha_proceso
            AND t.cod_empresa LIKE v_empresa_filtro
            AND t.cod_personal LIKE v_personal_filtro
            AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
            AND t.num_fotocheck IS NOT NULL
        );
        
        IF SQL%ROWCOUNT > 0 THEN
            DBMS_OUTPUT.PUT_LINE('PASO 0-CLEAN: Eliminadas ' || SQL%ROWCOUNT || ' marcas de depuracion anterior en SCA_HISTORIAL');
        END IF;
        
        -- =====================================================================
        -- PASO 0-UNHIDE: Reactivar marcas ocultadas por PASO 9 en run anterior
        -- (24/04/2026 v3) - IDEMPOTENCIA del PASO 9
        -- PASO 9 marca con ind_anulado='A' (estandar Aquarius) las marcas reales
        -- (tiporeg=1) que no fueron asignadas a campo del tareo (rondas, checkpoints).
        -- Para que el run actual pueda re-evaluarlas, las re-activamos al inicio.
        -- Identificacion: motivo LIKE 'DEPURACION: Marca no asignada%'
        -- (incluye limpieza de versiones legacy: ind_anulado='S' y ind_noprocesar=1)
        -- =====================================================================
        UPDATE SCA_HISTORIAL h
        SET h.ind_anulado = NULL,
            h.ind_noprocesar = 0,
            h.obs_noprocesar = NULL,
            h.motivo = NULL
        WHERE h.fec_equiv = v_fecha_proceso
        AND ( h.motivo LIKE 'DEPURACION: Marca no asignada%'
           OR h.obs_noprocesar LIKE 'DEPURACION: Marca no asignada%' )
        AND h.idtarjeta IN (
            SELECT t.num_fotocheck
            FROM SCA_ASISTENCIA_TAREO t
            WHERE t.fechamar = v_fecha_proceso
            AND t.cod_empresa LIKE v_empresa_filtro
            AND t.cod_personal LIKE v_personal_filtro
            AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
            AND t.num_fotocheck IS NOT NULL
        );
        
        IF SQL%ROWCOUNT > 0 THEN
            DBMS_OUTPUT.PUT_LINE('PASO 0-UNHIDE: Reactivadas ' || SQL%ROWCOUNT || ' marcas ocultadas en run anterior');
        END IF;
        
        -- =====================================================================
        -- PASO 0-RESET-TAGS: Resetear codaux4/codaux5 al inicio (IDEMPOTENCIA)
        -- FIX 23/04/2026
        -- Caso: Cada re-run del paquete ANEXA tags al codaux4 existente
        --       (ej: "R1|RC" -> "R1|RC|R1|RC|HE|MF" -> ...). Eventualmente:
        --       a) El string excede 50 chars y se trunca cortando tags.
        --       b) Las clausulas "WHERE codaux4 NOT LIKE '%RC'" empiezan a
        --          fallar porque siempre contienen RC de runs anteriores.
        --       c) Tags duplicados (E3|RC|RC observado en datos reales).
        -- Accion: Limpiar codaux4/codaux5 a NULL al inicio de cada DEPURA_TOTAL,
        --         despues de borrar las marcas DEPURACION% del SCA_HISTORIAL.
        --         Asi el run actual reconstruye los tags desde cero, garantiza
        --         idempotencia y produce siempre el mismo resultado.
        -- IMPORTANTE: ROLLBACK_MARCACIONES tambien limpia codaux al final, asi
        --             que esta logica es consistente.
        --
        -- TABLAS ESCRITURA: SCA_ASISTENCIA_TAREO (UPDATE)
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t
        SET t.codaux4 = NULL,
            t.codaux5 = NULL
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
        AND t.num_fotocheck IS NOT NULL
        AND NVL(t.ind_cerrado, 'N') <> 'S'
        AND (t.codaux4 IS NOT NULL OR t.codaux5 IS NOT NULL);
        
        IF SQL%ROWCOUNT > 0 THEN
            DBMS_OUTPUT.PUT_LINE('PASO 0-RESET-TAGS: Reseteados ' || SQL%ROWCOUNT || ' codaux4/5 (idempotencia)');
        END IF;
        
        -- =====================================================================
        -- PASO 0-NOC-RECOVER: Restaurar SALIDA real nocturna desde SCA_HIS
        -- FIX 23/04/2026
        -- Caso: En runs previos buggy, PASO 3D detectaba "salida<inirefri" en
        --       turnos nocturnos (ej: HORARIO 19-03, EF=19:00, SF=03:00) y
        --       reemplazaba la salida REAL (ej: 07:03 cross-day) con la
        --       salida_fijada (03:00). Tag SS|RT|RC marcaba el destrozo.
        --       PASO 3D ya esta corregido para no disparar en nocturno, pero
        --       los datos quedaron persistidos: tareo.salida = salida_fijada.
        --       Despues PASO 0-CLEAN borra DEPURACION 03:00, pero tareo sigue
        --       con salida=03:00 -> 0-RESTORE re-inserta DEPURACION 03:00.
        --       Bucle infinito de salida fantasma.
        --
        -- HUELLA del bug: tareo es nocturno (SF<EF), salida=salida_fijada exacto,
        --                 NO existe marca FC real a esa hora en SCA_HIS,
        --                 SI existe marca FC real DESPUES de SF dentro de 5h.
        -- ACCION: Reemplazar tareo.salida con la marca real mas tardia (cross-day).
        --
        -- TABLAS ESCRITURA: SCA_ASISTENCIA_TAREO (UPDATE salida)
        -- TABLAS CONSULTA:  SCA_HISTORIAL
        -- =====================================================================
        DECLARE
            v_marca_real DATE;
        BEGIN
            FOR rec_noc IN (
                SELECT t.ROWID AS rid, t.num_fotocheck, t.fechamar,
                       t.entrada_fijada, t.salida_fijada, t.salida
                FROM SCA_ASISTENCIA_TAREO t
                WHERE t.fechamar = v_fecha_proceso
                AND t.cod_empresa LIKE v_empresa_filtro
                AND t.cod_personal LIKE v_personal_filtro
                AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
                AND t.num_fotocheck IS NOT NULL
                AND NVL(t.ind_cerrado, 'N') <> 'S'
                AND NVL(t.descanso, 'N') <> 'S'
                AND t.entrada_fijada IS NOT NULL
                AND t.salida_fijada IS NOT NULL
                -- Nocturno cross-day (comparar HH24MI; las fechas pueden tener epocas distintas)
                AND TO_CHAR(t.salida_fijada,'HH24MI') < TO_CHAR(t.entrada_fijada,'HH24MI')
                AND t.salida IS NOT NULL
                -- salida coincide exactamente con salida_fijada (sospechoso)
                AND TO_CHAR(t.salida, 'HH24:MI:SS') = TO_CHAR(t.salida_fijada, 'HH24:MI:SS')
                -- NO hay marca real (no DEPURACION) en SCA_HIS a esa hora
                AND NOT EXISTS (
                    SELECT 1 FROM SCA_HISTORIAL h
                    WHERE h.idtarjeta = t.num_fotocheck
                    AND h.fec_equiv = t.fechamar
                    AND RTRIM(h.hora) = TO_CHAR(t.salida_fijada, 'HH24:MI:SS')
                    AND NVL(h.motivo, ' ') NOT LIKE 'DEPURACION%'
                    AND NVL(h.ind_anulado, 'N') <> 'S'
                    AND NVL(h.ind_noprocesar, 0) = 0
                )
            ) LOOP
                v_marca_real := NULL;
                -- Buscar marca real cross-day mas tardia entre SF y SF+5h
                -- IMPORTANTE: salida_fijada base 01/01/1900, marca tiene fec_equiv real
                -- -> comparar por minutos del dia, NO por DATE completo (epoch mismatch)
                BEGIN
                    SELECT MAX(TO_DATE(TO_CHAR(h.fec_equiv,'DD/MM/YYYY')||' '||RTRIM(h.hora),'DD/MM/YYYY HH24:MI:SS'))
                    INTO v_marca_real
                    FROM SCA_HISTORIAL h
                    WHERE h.idtarjeta = rec_noc.num_fotocheck
                    AND h.fec_equiv = rec_noc.fechamar
                    AND NVL(h.motivo, ' ') NOT LIKE 'DEPURACION%'
                    AND NVL(h.ind_anulado, 'N') <> 'S'
                    AND NVL(h.ind_noprocesar, 0) = 0
                    -- ventana por minutos del dia: marca >= SF (5 min de gracia) y <= SF+5h
                    -- Usa TO_NUMBER de SUBSTR(hora) para minutos del dia de la marca
                    AND (TO_NUMBER(SUBSTR(h.hora,1,2))*60 + TO_NUMBER(SUBSTR(h.hora,4,2)))
                        >= (TO_NUMBER(TO_CHAR(rec_noc.salida_fijada,'HH24'))*60
                            + TO_NUMBER(TO_CHAR(rec_noc.salida_fijada,'MI'))) - 5
                    AND (TO_NUMBER(SUBSTR(h.hora,1,2))*60 + TO_NUMBER(SUBSTR(h.hora,4,2)))
                        <= (TO_NUMBER(TO_CHAR(rec_noc.salida_fijada,'HH24'))*60
                            + TO_NUMBER(TO_CHAR(rec_noc.salida_fijada,'MI'))) + 300;
                EXCEPTION WHEN OTHERS THEN
                    v_marca_real := NULL;
                END;
                
                IF v_marca_real IS NOT NULL
                   AND TO_CHAR(v_marca_real,'HH24:MI:SS') <> TO_CHAR(rec_noc.salida_fijada,'HH24:MI:SS')
                THEN
                    UPDATE SCA_ASISTENCIA_TAREO
                    SET salida = v_marca_real
                    WHERE ROWID = rec_noc.rid;
                    DBMS_OUTPUT.PUT_LINE('PASO 0-NOC-RECOVER: fotocheck=' || rec_noc.num_fotocheck ||
                                         ' fecha=' || TO_CHAR(rec_noc.fechamar,'DD/MM') ||
                                         ' SF=' || TO_CHAR(rec_noc.salida_fijada,'HH24:MI') ||
                                         ' salida_fantasma=' || TO_CHAR(rec_noc.salida,'HH24:MI') ||
                                         ' -> salida_real=' || TO_CHAR(v_marca_real,'HH24:MI'));
                END IF;
            END LOOP;
        END;
        
        -- =====================================================================
        -- PASO 0-DUP: Eliminar marcas duplicadas cercanas
        -- Caso: Empleado marca 2 veces seguidas (ej: 18:56 y 18:57)
        -- Regla: Si 2 marcas estan a menos de 5 minutos, eliminar la segunda
        -- Se elimina ANTES de sincronizar nummarcaciones
        --
        -- FIX 23/04/2026: Tambien eliminar marcas con motivo administrativo
        -- 'MARCA DUPLICADA' (Aquarius marco la marca como duplicada). Se ejecuta
        -- ANTES del near-dup para que la marca real (no la marcada) prevalezca.
        -- Ejemplo: 034572 21/04 tenia 12:44 'MARCA DUPLICADA' + 12:45 real.
        --   Sin este fix, near-dup eliminaba la 12:45 (posterior) y dejaba la
        --   marcada como duplicada -> tareo perdia el inirefri real.
        -- =====================================================================
        DELETE FROM SCA_HISTORIAL h1
        WHERE h1.fec_equiv = v_fecha_proceso
        AND UPPER(NVL(h1.motivo, ' ')) LIKE '%DUPLICAD%'
        AND NVL(h1.ind_anulado, 'N') <> 'S'
        AND h1.idtarjeta IN (
            SELECT t.num_fotocheck
            FROM SCA_ASISTENCIA_TAREO t
            WHERE t.fechamar = v_fecha_proceso
            AND t.cod_empresa LIKE v_empresa_filtro
            AND t.cod_personal LIKE v_personal_filtro
            AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
            AND t.num_fotocheck IS NOT NULL
        );
        
        IF SQL%ROWCOUNT > 0 THEN
            DBMS_OUTPUT.PUT_LINE('PASO 0-DUP-MOT: Eliminadas ' || SQL%ROWCOUNT || ' marcas con motivo DUPLICADA');
        END IF;
        
        DELETE FROM SCA_HISTORIAL h1
        WHERE h1.fec_equiv = v_fecha_proceso
        AND NVL(h1.motivo, ' ') NOT LIKE 'DEPURACION%'
        AND NVL(h1.ind_anulado, 'N') <> 'S'
        AND h1.idtarjeta IN (
            SELECT t.num_fotocheck
            FROM SCA_ASISTENCIA_TAREO t
            WHERE t.fechamar = v_fecha_proceso
            AND t.cod_empresa LIKE v_empresa_filtro
            AND t.cod_personal LIKE v_personal_filtro
            AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
            AND t.num_fotocheck IS NOT NULL
        )
        -- Existe otra marca del mismo empleado/fecha a menos de 5 min ANTES
        AND EXISTS (
            SELECT 1 FROM SCA_HISTORIAL h2
            WHERE h2.idtarjeta = h1.idtarjeta
            AND h2.fec_equiv = h1.fec_equiv
            AND h2.idcod <> h1.idcod
            AND NVL(h2.motivo, ' ') NOT LIKE 'DEPURACION%'
            AND NVL(h2.ind_anulado, 'N') <> 'S'
            -- h2 es ANTERIOR a h1 y a menos de 5 min
            AND TO_DATE(TO_CHAR(h2.fec_equiv, 'DD/MM/YYYY') || ' ' || RTRIM(h2.hora), 'DD/MM/YYYY HH24:MI:SS')
                < TO_DATE(TO_CHAR(h1.fec_equiv, 'DD/MM/YYYY') || ' ' || RTRIM(h1.hora), 'DD/MM/YYYY HH24:MI:SS')
            AND TO_DATE(TO_CHAR(h1.fec_equiv, 'DD/MM/YYYY') || ' ' || RTRIM(h1.hora), 'DD/MM/YYYY HH24:MI:SS')
                - TO_DATE(TO_CHAR(h2.fec_equiv, 'DD/MM/YYYY') || ' ' || RTRIM(h2.hora), 'DD/MM/YYYY HH24:MI:SS')
                < (5/1440)  -- 5 minutos
        );
        
        IF SQL%ROWCOUNT > 0 THEN
            DBMS_OUTPUT.PUT_LINE('PASO 0-DUP: Eliminadas ' || SQL%ROWCOUNT || ' marcas duplicadas cercanas (<5 min)');
        END IF;
        
        -- =====================================================================
        -- PASO 0-ORF: Eliminar marcas huerfanas intermedias sobrantes
        -- Caso 1: Mas de 2 marcas intermedias (solo se necesitan 2 para refri)
        -- Caso 2: Exactamente 2 intermedias con paridad impar REAL en SCA_HISTORIAL
        --         y una esta muy lejos del refrigerio (>90 min) = huerfana
        -- Elimina la marca MAS LEJANA del horario refrigerio teorico
        -- FIX 23/04/2026: Usar total_marcas real de SCA_HIS (no alerta01 desactualizado)
        --   Problema: PASO 0-DUP puede reducir de 5->4 marcas (par), pero alerta01
        --   sigue siendo 'MI' (no se actualizo aun). ORF veia alerta01='MI' y eliminaba
        --   una marca valida de refrigerio (ej: 13:18) aunque ya habia paridad par.
        -- =====================================================================
        FOR rec_orf IN (
            SELECT t.num_fotocheck, t.fechamar, t.entrada, t.salida,
                   t.inirefri, t.finrefri,
                   t.horiniref, t.horfinref, t.alerta01,
                   (SELECT COUNT(*) FROM SCA_HISTORIAL h 
                    WHERE h.idtarjeta = t.num_fotocheck 
                    AND h.fec_equiv = t.fechamar
                    AND NVL(h.motivo, ' ') NOT LIKE 'DEPURACION%'
                    AND NVL(h.ind_anulado, 'N') <> 'S'
                    AND RTRIM(h.hora) <> TO_CHAR(t.entrada, 'HH24:MI:SS')
                    AND RTRIM(h.hora) <> TO_CHAR(t.salida, 'HH24:MI:SS')
                    AND (t.inirefri IS NULL OR RTRIM(h.hora) <> TO_CHAR(t.inirefri, 'HH24:MI:SS'))
                    AND (t.finrefri IS NULL OR RTRIM(h.hora) <> TO_CHAR(t.finrefri, 'HH24:MI:SS'))
                   ) as num_intermedias,
                   -- FIX 23/04/2026: total_marcas real para evaluar paridad actual
                   -- No usar alerta01 que puede estar desactualizado despues de PASO 0-DUP
                   (SELECT COUNT(*) FROM SCA_HISTORIAL h_total
                    WHERE h_total.idtarjeta = t.num_fotocheck 
                    AND h_total.fec_equiv = t.fechamar
                    AND NVL(h_total.motivo, ' ') NOT LIKE 'DEPURACION%'
                    AND NVL(h_total.ind_anulado, 'N') <> 'S'
                   ) as total_marcas
            FROM SCA_ASISTENCIA_TAREO t
            WHERE t.fechamar = v_fecha_proceso
            AND t.cod_empresa LIKE v_empresa_filtro
            AND t.cod_personal LIKE v_personal_filtro
            AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
            AND t.num_fotocheck IS NOT NULL
            AND t.entrada IS NOT NULL
            AND t.salida IS NOT NULL
            AND t.horiniref IS NOT NULL
            -- FIX 24/04/2026: excluir horarios sin refri teorico (horiniref=00:00, ej: VIGILANCIA).
            -- Sin refri teorico, todas las distancias se miden contra medianoche y la marca
            -- de salida a comer (ej: 20:06) seria "la mas lejana" -> se eliminaria por error.
            -- Caso: CHOCCARE 034161 23/04, marca refri 20:06 fue eliminada por ORF.
            AND TO_CHAR(t.horiniref, 'HH24:MI') <> '00:00'
        ) LOOP
            -- Caso 1: Mas de 2 intermedias - eliminar la mas lejana
            -- Caso 2: Exactamente 2 pero paridad REAL es impar - eliminar huerfana lejana
            -- FIX 23/04/2026: usar MOD(total_marcas,2) en vez de alerta01 (puede ser stale)
            IF rec_orf.num_intermedias > 2 OR 
               (rec_orf.num_intermedias = 2 AND MOD(rec_orf.total_marcas, 2) = 1) THEN
                
                -- Eliminar la marca intermedia MAS LEJANA del horario de refrigerio
                -- Solo si esta a mas de 90 min del rango refrigerio (claramente huerfana)
                DELETE FROM SCA_HISTORIAL h1
                WHERE h1.idcod = (
                    SELECT idcod FROM (
                        SELECT h.idcod,
                               -- FIX 23/04/2026: epoch fix - dist en minutos del dia, no dias desde 1900
                               -- horiniref/horfinref tienen base 01/01/1900; h.fec_equiv tiene fecha real 2026
                               -- ABS(fecha_2026 - fecha_1900) = ~46000 dias, no minutos
                               LEAST(
                                   ABS(  TO_NUMBER(SUBSTR(RTRIM(h.hora),1,2))*60 + TO_NUMBER(SUBSTR(RTRIM(h.hora),4,2))
                                       - TO_NUMBER(TO_CHAR(rec_orf.horiniref,'HH24'))*60 - TO_NUMBER(TO_CHAR(rec_orf.horiniref,'MI'))
                                   ),
                                   ABS(  TO_NUMBER(SUBSTR(RTRIM(h.hora),1,2))*60 + TO_NUMBER(SUBSTR(RTRIM(h.hora),4,2))
                                       - TO_NUMBER(TO_CHAR(rec_orf.horfinref,'HH24'))*60 - TO_NUMBER(TO_CHAR(rec_orf.horfinref,'MI'))
                                   )
                               ) as dist_refri
                        FROM SCA_HISTORIAL h
                        WHERE h.idtarjeta = rec_orf.num_fotocheck
                        AND h.fec_equiv = rec_orf.fechamar
                        AND NVL(h.motivo, ' ') NOT LIKE 'DEPURACION%'
                        AND NVL(h.ind_anulado, 'N') <> 'S'
                        -- No es la entrada
                        AND RTRIM(h.hora) <> TO_CHAR(rec_orf.entrada, 'HH24:MI:SS')
                        -- No es la salida
                        AND RTRIM(h.hora) <> TO_CHAR(rec_orf.salida, 'HH24:MI:SS')
                        -- No es inirefri del tareo (aunque difiera del teorico)
                        AND (rec_orf.inirefri IS NULL OR RTRIM(h.hora) <> TO_CHAR(rec_orf.inirefri, 'HH24:MI:SS'))
                        -- No es finrefri del tareo
                        AND (rec_orf.finrefri IS NULL OR RTRIM(h.hora) <> TO_CHAR(rec_orf.finrefri, 'HH24:MI:SS'))
                        -- Esta a mas de 90 min del refrigerio (claramente huerfana)
                        -- FIX 23/04/2026: comparar minutos del dia (no dias desde 1900)
                        -- > 90 minutos (antes era > 90/1440 dias, que con epoch ~46000 dias siempre era TRUE)
                        AND LEAST(
                               ABS(  TO_NUMBER(SUBSTR(RTRIM(h.hora),1,2))*60 + TO_NUMBER(SUBSTR(RTRIM(h.hora),4,2))
                                   - TO_NUMBER(TO_CHAR(rec_orf.horiniref,'HH24'))*60 - TO_NUMBER(TO_CHAR(rec_orf.horiniref,'MI'))
                               ),
                               ABS(  TO_NUMBER(SUBSTR(RTRIM(h.hora),1,2))*60 + TO_NUMBER(SUBSTR(RTRIM(h.hora),4,2))
                                   - TO_NUMBER(TO_CHAR(rec_orf.horfinref,'HH24'))*60 - TO_NUMBER(TO_CHAR(rec_orf.horfinref,'MI'))
                               )
                            ) > 90
                        ORDER BY dist_refri DESC  -- La mas lejana primero
                    ) WHERE ROWNUM = 1
                );
                
                IF SQL%ROWCOUNT > 0 THEN
                    DBMS_OUTPUT.PUT_LINE('PASO 0-ORF: Eliminada 1 marca huerfana para fotocheck ' || rec_orf.num_fotocheck);
                END IF;
            END IF;
        END LOOP;
        
        -- =====================================================================
        -- PASO 0-RESTORE: Re-insertar marcas del tareo eliminadas por DUP/ORF
        -- FIX 15/04/2026 (original) + FIX 23/04/2026 (condicion correcta)
        -- Caso: PASO 0-DUP elimina marcas <5 min como "duplicadas", pero
        --       inirefri/finrefri pueden estar a <5 min y son legitimas.
        --       Ejemplo: IR=01:05, FR=01:06 (1 min) -> DUP elimina 01:06
        --       PASO 0-ORF tambien puede eliminar marcas de refri como "huerfanas"
        -- Accion: Para cada campo del tareo (entrada, inirefri, finrefri, salida),
        --         verificar si su marca existe en SCA_HISTORIAL. Si no, re-insertarla.
        -- Nota: Solo aplica a campos que ya estaban poblados en el tareo ORIGINAL
        --       (no a marcas generadas por el paquete).
        --
        -- BUG CORREGIDO 23/04/2026 (datos prod): el cursor exigia
        --   nummarcaciones>=4 AND inirefri IS NOT NULL AND finrefri IS NOT NULL.
        --   Pero despues de runs previos con PASO 0-DUP/0-ORF, nummarcaciones
        --   pudo bajar a 2 o 3 -> el cursor NUNCA disparaba -> tareo quedaba
        --   inconsistente con SCA_HIS para siempre.
        --   Ejemplo real: fotocheck 034628, 23/03/2026: tareo tiene 4 campos
        --   (E=08:16, IR=13:15, FR=14:00, S=18:14) pero SCA_HIS solo tiene
        --   2 marcas (E y S). 0-RESTORE no restauraba IR/FR.
        --
        -- Ahora: cursor incluye TODOS los tareos del dia con num_fotocheck.
        --        Cada IF dentro del LOOP chequea su campo individualmente.
        --        Guard near-dup (>=5/1440) aplica SOLO al par IR-FR cuando
        --        ambos vienen del mismo tareo (excluye caso 12:44/12:45 y
        --        caso Aquarius null 01/01/1900 00:00/00:00 -> diff=0).
        --
        -- TABLAS ESCRITURA: SCA_HISTORIAL (INSERT)
        -- TABLAS CONSULTA:  SCA_ASISTENCIA_TAREO, SCA_HISTORIAL
        -- =====================================================================
        FOR rec_rest IN (
            SELECT t.num_fotocheck, t.fechamar,
                   t.entrada, t.inirefri, t.finrefri, t.salida,
                   -- Flag: par refri es near-dup (<5 min) o Aquarius null
                   CASE
                     WHEN t.inirefri IS NOT NULL AND t.finrefri IS NOT NULL
                          AND (t.finrefri - t.inirefri) < (5/1440) THEN 'S'
                     ELSE 'N'
                   END AS refri_neardup
            FROM SCA_ASISTENCIA_TAREO t
            WHERE t.fechamar = v_fecha_proceso
            AND t.cod_empresa LIKE v_empresa_filtro
            AND t.cod_personal LIKE v_personal_filtro
            AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
            AND t.num_fotocheck IS NOT NULL
            AND NVL(t.ind_cerrado, 'N') <> 'S'
            -- Al menos un campo del tareo poblado (sino no hay nada que restaurar)
            AND (t.entrada IS NOT NULL OR t.salida IS NOT NULL
                 OR t.inirefri IS NOT NULL OR t.finrefri IS NOT NULL)
        ) LOOP
            -- Verificar cada campo del tareo contra SCA_HISTORIAL
            -- ENTRADA
            IF rec_rest.entrada IS NOT NULL THEN
                INSERT INTO SCA_HISTORIAL (idCod, idLectora, idTarjeta, fecha, hora, tiporeg, fecreg, fec_equiv, motivo, ind_aman_hor_est)
                SELECT id_cod_seq.NEXTVAL, '005', rec_rest.num_fotocheck,
                       TO_CHAR(rec_rest.fechamar, 'DD/MM/YYYY'),
                       TO_CHAR(rec_rest.entrada, 'HH24:MI:SS'),
                       '3', SYSDATE, rec_rest.fechamar,
                       'DEPURACION: Marca restaurada entrada 0-REST', 'A'
                FROM DUAL
                WHERE NOT EXISTS (
                    SELECT 1 FROM SCA_HISTORIAL h
                    WHERE h.idtarjeta = rec_rest.num_fotocheck
                    AND h.fec_equiv = rec_rest.fechamar
                    AND RTRIM(h.hora) = TO_CHAR(rec_rest.entrada, 'HH24:MI:SS')
                    AND NVL(h.ind_anulado, 'N') <> 'S'
                );
                IF SQL%ROWCOUNT > 0 THEN
                    DBMS_OUTPUT.PUT_LINE('PASO 0-RESTORE: Re-insertada marca ENTRADA ' || TO_CHAR(rec_rest.entrada, 'HH24:MI:SS') || ' para fotocheck ' || rec_rest.num_fotocheck);
                    v_count_historial := v_count_historial + 1;
                END IF;
            END IF;
            
            -- INIREFRI: solo restaurar si NO es near-dup con FINREFRI
            -- (caso 12:44/12:45 -> 0-DUP elimino correctamente, no restaurar)
            IF rec_rest.inirefri IS NOT NULL AND rec_rest.refri_neardup = 'N' THEN
                INSERT INTO SCA_HISTORIAL (idCod, idLectora, idTarjeta, fecha, hora, tiporeg, fecreg, fec_equiv, motivo, ind_aman_hor_est)
                SELECT id_cod_seq.NEXTVAL, '005', rec_rest.num_fotocheck,
                       TO_CHAR(rec_rest.fechamar, 'DD/MM/YYYY'),
                       TO_CHAR(rec_rest.inirefri, 'HH24:MI:SS'),
                       '3', SYSDATE, rec_rest.fechamar,
                       'DEPURACION: Marca restaurada inirefri 0-REST', 'A'
                FROM DUAL
                WHERE NOT EXISTS (
                    SELECT 1 FROM SCA_HISTORIAL h
                    WHERE h.idtarjeta = rec_rest.num_fotocheck
                    AND h.fec_equiv = rec_rest.fechamar
                    AND RTRIM(h.hora) = TO_CHAR(rec_rest.inirefri, 'HH24:MI:SS')
                    AND NVL(h.ind_anulado, 'N') <> 'S'
                );
                IF SQL%ROWCOUNT > 0 THEN
                    DBMS_OUTPUT.PUT_LINE('PASO 0-RESTORE: Re-insertada marca INIREFRI ' || TO_CHAR(rec_rest.inirefri, 'HH24:MI:SS') || ' para fotocheck ' || rec_rest.num_fotocheck);
                    v_count_historial := v_count_historial + 1;
                END IF;
            END IF;
            
            -- FINREFRI: solo restaurar si NO es near-dup con INIREFRI
            IF rec_rest.finrefri IS NOT NULL AND rec_rest.refri_neardup = 'N' THEN
                INSERT INTO SCA_HISTORIAL (idCod, idLectora, idTarjeta, fecha, hora, tiporeg, fecreg, fec_equiv, motivo, ind_aman_hor_est)
                SELECT id_cod_seq.NEXTVAL, '005', rec_rest.num_fotocheck,
                       TO_CHAR(rec_rest.fechamar, 'DD/MM/YYYY'),
                       TO_CHAR(rec_rest.finrefri, 'HH24:MI:SS'),
                       '3', SYSDATE, rec_rest.fechamar,
                       'DEPURACION: Marca restaurada finrefri 0-REST', 'A'
                FROM DUAL
                WHERE NOT EXISTS (
                    SELECT 1 FROM SCA_HISTORIAL h
                    WHERE h.idtarjeta = rec_rest.num_fotocheck
                    AND h.fec_equiv = rec_rest.fechamar
                    AND RTRIM(h.hora) = TO_CHAR(rec_rest.finrefri, 'HH24:MI:SS')
                    AND NVL(h.ind_anulado, 'N') <> 'S'
                );
                IF SQL%ROWCOUNT > 0 THEN
                    DBMS_OUTPUT.PUT_LINE('PASO 0-RESTORE: Re-insertada marca FINREFRI ' || TO_CHAR(rec_rest.finrefri, 'HH24:MI:SS') || ' para fotocheck ' || rec_rest.num_fotocheck);
                    v_count_historial := v_count_historial + 1;
                END IF;
            END IF;
            
            -- SALIDA
            IF rec_rest.salida IS NOT NULL THEN
                INSERT INTO SCA_HISTORIAL (idCod, idLectora, idTarjeta, fecha, hora, tiporeg, fecreg, fec_equiv, motivo, ind_aman_hor_est)
                SELECT id_cod_seq.NEXTVAL, '005', rec_rest.num_fotocheck,
                       TO_CHAR(rec_rest.fechamar, 'DD/MM/YYYY'),
                       TO_CHAR(rec_rest.salida, 'HH24:MI:SS'),
                       '3', SYSDATE, rec_rest.fechamar,
                       'DEPURACION: Marca restaurada salida 0-REST', 'A'
                FROM DUAL
                WHERE NOT EXISTS (
                    SELECT 1 FROM SCA_HISTORIAL h
                    WHERE h.idtarjeta = rec_rest.num_fotocheck
                    AND h.fec_equiv = rec_rest.fechamar
                    AND RTRIM(h.hora) = TO_CHAR(rec_rest.salida, 'HH24:MI:SS')
                    AND NVL(h.ind_anulado, 'N') <> 'S'
                );
                IF SQL%ROWCOUNT > 0 THEN
                    DBMS_OUTPUT.PUT_LINE('PASO 0-RESTORE: Re-insertada marca SALIDA ' || TO_CHAR(rec_rest.salida, 'HH24:MI:SS') || ' para fotocheck ' || rec_rest.num_fotocheck);
                    v_count_historial := v_count_historial + 1;
                END IF;
            END IF;
        END LOOP;
        
        -- =====================================================================
        -- PASO 0-RESTORE-B: Corregir paridad impar causada por rondas/checkpoints
        -- FIX 15/04/2026
        -- Caso: Despues de DUP, ORF y RESTORE-A, el total de marcas puede ser
        --       IMPAR si hay marcas de ronda (lectoras 004, 005, 010) que no
        --       corresponden a ningun campo del tareo (E, IR, FR, S).
        --       Ejemplo: 8 marcas (4 tareo + 4 ronda) -> DUP(-2) ORF(-1) 
        --       RESTORE(+2) = 7 marcas (impar)
        -- Accion: Si el total es impar, eliminar UNA marca intermedia que NO
        --         coincida con ningun campo del tareo. Prioriza la mas lejana
        --         del horario de refrigerio (consistente con logica ORF).
        -- FIX 20/04/2026: Agrega restriccion: solo elimina marcas a MAS de 90 min
        --         del horario refri teorico. Si inirefri=NULL pero hay una marca
        --         cercana al horario refri (ej: 13:03 con horiniref=13:15), es el
        --         refri real y NO debe eliminarse. Sin este fix, el paquete requeria
        --         2 ejecuciones para resolver casos como fotocheck 034482 (28/03).
        --
        -- TABLAS ESCRITURA: SCA_HISTORIAL (DELETE)
        -- TABLAS CONSULTA:  SCA_ASISTENCIA_TAREO, SCA_HISTORIAL
        -- =====================================================================
        FOR rec_par IN (
            SELECT t.num_fotocheck, t.fechamar,
                   t.entrada, t.inirefri, t.finrefri, t.salida,
                   t.horiniref, t.horfinref,
                   (SELECT COUNT(*) FROM SCA_HISTORIAL h 
                    WHERE h.idtarjeta = t.num_fotocheck 
                    AND h.fec_equiv = t.fechamar
                    AND NVL(h.ind_anulado, 'N') <> 'S'
                   ) as total_marcas
            FROM SCA_ASISTENCIA_TAREO t
            WHERE t.fechamar = v_fecha_proceso
            AND t.cod_empresa LIKE v_empresa_filtro
            AND t.cod_personal LIKE v_personal_filtro
            AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
            AND t.num_fotocheck IS NOT NULL
            AND NVL(t.ind_cerrado, 'N') <> 'S'
            AND t.entrada IS NOT NULL
            AND t.salida IS NOT NULL
            -- FIX 24/04/2026: excluir horarios sin refri teorico (horiniref=00:00, ej: VIGILANCIA).
            -- Sin refri teorico, la marca de salida a comer queda lejos de medianoche
            -- y se eliminaria por error como "ronda".
            AND (t.horiniref IS NOT NULL AND TO_CHAR(t.horiniref, 'HH24:MI') <> '00:00')
        ) LOOP
            -- Solo actuar si el total es impar
            IF MOD(rec_par.total_marcas, 2) = 1 THEN
                -- Eliminar UNA marca que no coincida con ningun campo del tareo
                DELETE FROM SCA_HISTORIAL h1
                WHERE h1.idcod = (
                    SELECT idcod FROM (
                        SELECT h.idcod
                        FROM SCA_HISTORIAL h
                        WHERE h.idtarjeta = rec_par.num_fotocheck
                        AND h.fec_equiv = rec_par.fechamar
                        AND NVL(h.ind_anulado, 'N') <> 'S'
                        -- No es entrada del tareo
                        AND RTRIM(h.hora) <> TO_CHAR(rec_par.entrada, 'HH24:MI:SS')
                        -- No es salida del tareo
                        AND RTRIM(h.hora) <> TO_CHAR(rec_par.salida, 'HH24:MI:SS')
                        -- No es inirefri del tareo
                        AND (rec_par.inirefri IS NULL OR RTRIM(h.hora) <> TO_CHAR(rec_par.inirefri, 'HH24:MI:SS'))
                        -- No es finrefri del tareo
                        AND (rec_par.finrefri IS NULL OR RTRIM(h.hora) <> TO_CHAR(rec_par.finrefri, 'HH24:MI:SS'))
                        -- FIX 20/04/2026: No eliminar si esta CERCA del horario refri teorico
                        -- Una marca dentro de 90 min de horiniref/horfinref puede ser el
                        -- refrigerio real cuando inirefri=NULL en el tareo (CASO 034482)
                        -- FIX 23/04/2026: epoch fix - comparar minutos del dia, no dias desde 1900
                        -- horiniref/horfinref tienen base 01/01/1900; h.fec_equiv tiene fecha real 2026
                        -- ABS(fecha_2026 - fecha_1900) = ~46000 dias >> 90/1440, la proteccion NUNCA disparaba
                        AND (
                            rec_par.horiniref IS NULL
                            OR LEAST(
                                ABS(  TO_NUMBER(SUBSTR(RTRIM(h.hora),1,2))*60 + TO_NUMBER(SUBSTR(RTRIM(h.hora),4,2))
                                    - TO_NUMBER(TO_CHAR(rec_par.horiniref,'HH24'))*60 - TO_NUMBER(TO_CHAR(rec_par.horiniref,'MI'))
                                ),
                                ABS(  TO_NUMBER(SUBSTR(RTRIM(h.hora),1,2))*60 + TO_NUMBER(SUBSTR(RTRIM(h.hora),4,2))
                                    - TO_NUMBER(TO_CHAR(rec_par.horfinref,'HH24'))*60 - TO_NUMBER(TO_CHAR(rec_par.horfinref,'MI'))
                                )
                            ) > 90  -- > 90 minutos del dia (no dias)
                        )
                        -- Ordenar por distancia al refrigerio (farthest first)
                        -- FIX 23/04/2026: epoch fix - minutos del dia; NVL->00:00 para horiniref NULL
                        ORDER BY LEAST(
                            ABS(  TO_NUMBER(SUBSTR(RTRIM(h.hora),1,2))*60 + TO_NUMBER(SUBSTR(RTRIM(h.hora),4,2))
                                - TO_NUMBER(TO_CHAR(NVL(rec_par.horiniref, TO_DATE('01/01/1900','DD/MM/YYYY')),'HH24'))*60
                                - TO_NUMBER(TO_CHAR(NVL(rec_par.horiniref, TO_DATE('01/01/1900','DD/MM/YYYY')),'MI'))
                            ),
                            ABS(  TO_NUMBER(SUBSTR(RTRIM(h.hora),1,2))*60 + TO_NUMBER(SUBSTR(RTRIM(h.hora),4,2))
                                - TO_NUMBER(TO_CHAR(NVL(rec_par.horfinref, TO_DATE('01/01/1900','DD/MM/YYYY')),'HH24'))*60
                                - TO_NUMBER(TO_CHAR(NVL(rec_par.horfinref, TO_DATE('01/01/1900','DD/MM/YYYY')),'MI'))
                            )
                        ) DESC
                    ) WHERE ROWNUM = 1
                );
                
                IF SQL%ROWCOUNT > 0 THEN
                    DBMS_OUTPUT.PUT_LINE('PASO 0-RESTORE-B: Eliminada marca de ronda para paridad par, fotocheck ' || rec_par.num_fotocheck);
                END IF;
            END IF;
        END LOOP;
        
        -- =====================================================================
        -- PASO 0-PRE: Sincronizar NUMMARCACIONES con SCA_HISTORIAL
        -- El tareo pone nummarcaciones segun las marcas que el asigno a los
        -- campos (entrada, salida), pero si hay marcacion impar (3, 5 marcas)
        -- las marcas intermedias quedan huerfanas en SCA_HISTORIAL y 
        -- nummarcaciones NO las cuenta.
        -- Ejemplo: 3 marcas reales (06:40, 13:03, 16:00) -> tareo pone 
        --          entrada=06:40, salida=16:00, nummarcaciones=2
        --          pero SCA_HISTORIAL tiene 3 marcas reales.
        -- Esto afecta PASOs que usan nummarcaciones como filtro (0D, 1C, 4).
        -- Sincronizar ANTES de cualquier operacion.
        -- =====================================================================
        -- FIX 23/04/2026: Incluir marcas restauradas por PASO 0-RESTORE en el conteo.
        -- PASO 0-CLEAN borra todas las DEPURACION% al inicio. PASO 0-RESTORE re-inserta
        -- las marcas reales del tareo (entrada/inirefri/finrefri/salida) con motivo
        -- 'DEPURACION: Marca restaurada * 0-REST'. Si excluimos todo DEPURACION%, esas
        -- marcas REALES no se cuentan -> nm=1 y alerta01='MI' aunque el dia tiene 4 marcas.
        -- Bug observado: 034269/25/03 con SCA_HIS=4 marcas (3 son '0-REST') quedaba nm=1 MI.
        -- En PASO 0-PRE, las unicas DEPURACION% existentes son las de PASO 0-RESTORE,
        -- porque PASOs posteriores (R1/S1/SS/etc) corren DESPUES.
        UPDATE SCA_ASISTENCIA_TAREO t
        SET t.nummarcaciones = (
            SELECT COUNT(*) 
            FROM SCA_HISTORIAL h
            WHERE h.idtarjeta = t.num_fotocheck
            AND h.fec_equiv = t.fechamar
            AND (NVL(h.motivo, ' ') NOT LIKE 'DEPURACION%'
                 OR NVL(h.motivo, ' ') LIKE '%0-REST%')
            AND NVL(h.ind_anulado, 'N') <> 'S'
            AND NVL(h.ind_noprocesar, 0) = 0
        ),
            -- FIX 13/04/2026: Tambien actualizar alerta01 segun paridad
            -- Si PASO 0-DUP/ORF eliminaron marcas, el conteo pudo cambiar de
            -- impar a par. Sin esto, alerta01 quedaba 'MI' aunque marcas ya son pares.
            t.alerta01 = CASE 
                WHEN MOD((
                    SELECT COUNT(*) 
                    FROM SCA_HISTORIAL h3
                    WHERE h3.idtarjeta = t.num_fotocheck
                    AND h3.fec_equiv = t.fechamar
                    AND (NVL(h3.motivo, ' ') NOT LIKE 'DEPURACION%'
                         OR NVL(h3.motivo, ' ') LIKE '%0-REST%')
                    AND NVL(h3.ind_anulado, 'N') <> 'S'
                    AND NVL(h3.ind_noprocesar, 0) = 0
                ), 2) = 0 THEN NULL  -- Par = OK, limpiar alerta
                ELSE 'MI'             -- Impar = mantener/poner alerta
            END
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
        AND t.num_fotocheck IS NOT NULL
        AND NVL(t.ind_cerrado, 'N') <> 'S'
        AND (
            t.nummarcaciones <> (
                SELECT COUNT(*) 
                FROM SCA_HISTORIAL h2
                WHERE h2.idtarjeta = t.num_fotocheck
                AND h2.fec_equiv = t.fechamar
                AND (NVL(h2.motivo, ' ') NOT LIKE 'DEPURACION%'
                     OR NVL(h2.motivo, ' ') LIKE '%0-REST%')
                AND NVL(h2.ind_anulado, 'N') <> 'S'
                AND NVL(h2.ind_noprocesar, 0) = 0
            )
            OR t.alerta01 <> CASE 
                WHEN MOD((
                    SELECT COUNT(*) 
                    FROM SCA_HISTORIAL h4
                    WHERE h4.idtarjeta = t.num_fotocheck
                    AND h4.fec_equiv = t.fechamar
                    AND (NVL(h4.motivo, ' ') NOT LIKE 'DEPURACION%'
                         OR NVL(h4.motivo, ' ') LIKE '%0-REST%')
                    AND NVL(h4.ind_anulado, 'N') <> 'S'
                    AND NVL(h4.ind_noprocesar, 0) = 0
                ), 2) = 0 THEN NULL
                ELSE 'MI'
            END
            OR (t.alerta01 IS NULL AND MOD((
                    SELECT COUNT(*) 
                    FROM SCA_HISTORIAL h5
                    WHERE h5.idtarjeta = t.num_fotocheck
                    AND h5.fec_equiv = t.fechamar
                    AND (NVL(h5.motivo, ' ') NOT LIKE 'DEPURACION%'
                         OR NVL(h5.motivo, ' ') LIKE '%0-REST%')
                    AND NVL(h5.ind_anulado, 'N') <> 'S'
                    AND NVL(h5.ind_noprocesar, 0) = 0
                ), 2) = 1)
            OR (t.alerta01 = 'MI' AND MOD((
                    SELECT COUNT(*) 
                    FROM SCA_HISTORIAL h6
                    WHERE h6.idtarjeta = t.num_fotocheck
                    AND h6.fec_equiv = t.fechamar
                    AND (NVL(h6.motivo, ' ') NOT LIKE 'DEPURACION%'
                         OR NVL(h6.motivo, ' ') LIKE '%0-REST%')
                    AND NVL(h6.ind_anulado, 'N') <> 'S'
                    AND NVL(h6.ind_noprocesar, 0) = 0
                ), 2) = 0)
        )
        -- FIX 15/04/2026: NO recontear dias DESCANSO ya limpiados por PASO 6-PHANTOM
        -- Esos dias tienen nummarcaciones=0 correctamente; SCA_HISTORIAL puede
        -- tener marcas de ronda que inflarian el conteo y pondrian alerta01='MI'
        AND NOT (t.descanso = 'S' AND t.codaux4 LIKE '%' || c_PH || '%');
        
        IF SQL%ROWCOUNT > 0 THEN
            DBMS_OUTPUT.PUT_LINE('PASO 0-PRE: nummarcaciones/alerta01 sincronizado con SCA_HISTORIAL -> ' || SQL%ROWCOUNT || ' registros corregidos');
        END IF;
        
        -- =====================================================================
        -- PASO 6-PHANTOM: Limpiar marcaciones fantasma en dias de DESCANSO
        -- FIX 15/04/2026 - MOVIDO antes de PASO 0-CLEAN2 para ejecutar PRIMERO
        -- Caso: Turno nocturno (ej: 23:00->07:00) cruza medianoche.
        --       Las marcas despues de medianoche (01:05, 07:02) pertenecen al
        --       turno del DIA ANTERIOR pero el sistema las asigno al tareo del
        --       dia siguiente (fec_equiv = dia anterior, pero el tareo del dia
        --       siguiente tiene entrada/salida poblados con esos valores).
        --       Si ese dia siguiente es DESCANSO, el empleado NO trabajo.
        --       SCA_HISTORIAL tiene 0 marcas para ese dia (todas estan en el
        --       fec_equiv del dia anterior).
        -- Ejemplo: Viernes nocturno 18:39->07:02
        --          Sabado DESCANSO: tareo tiene E=01:05 S=07:02 (fantasmas)
        --          SCA_HISTORIAL dia sabado: 0 marcas
        -- Accion: Limpiar entrada/salida/inirefri/finrefri del tareo DESCANSO
        --         Solo si SCA_HISTORIAL tiene 0 marcas para ese dia.
        -- IMPORTANTE: Ejecutar ANTES de cualquier PASO que procese marcaciones
        --            (0A-7) para evitar que PASOs 7A/7 procesen fantasmas.
        --
        -- TABLAS ESCRITURA: SCA_ASISTENCIA_TAREO (UPDATE)
        -- TABLAS CONSULTA:  SCA_HISTORIAL
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t
        SET t.entrada = NULL,
            t.salida = NULL,
            t.inirefri = NULL,
            t.finrefri = NULL,
            t.nummarcaciones = 0,
            t.alerta01 = NULL,
            -- FIX 15/04/2026: Resetear campos de horas derivadas (quedan residuos del tareo)
            t.tothoramarcas = NULL,
            t.horarefrigerio = NULL,
            t.horaefectiva = NULL,
            t.horadobles = NULL,
            t.horatardanza = NULL,
            t.tothoranocturna = NULL,
            t.tothoranocturna_of = NULL,
            t.codaux4 = CASE WHEN t.codaux4 IS NULL THEN c_PH ELSE t.codaux4 || c_SEP || c_PH END,
            t.codaux5 = SUBSTR(CASE WHEN t.codaux5 IS NULL THEN d_PH ELSE t.codaux5 || c_SEP || d_PH END, 1, 50)
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
        AND t.descanso = 'S'
        AND (t.entrada IS NOT NULL OR t.salida IS NOT NULL)
        AND NVL(t.ind_cerrado, 'N') <> 'S'
        -- Solo si NO hay marcas reales en SCA_HISTORIAL para este dia
        AND NOT EXISTS (
            SELECT 1 FROM SCA_HISTORIAL h
            WHERE h.idtarjeta = t.num_fotocheck
            AND h.fec_equiv = t.fechamar
            AND NVL(h.ind_anulado, 'N') <> 'S'
            AND NVL(h.ind_noprocesar, 0) = 0
            AND NVL(h.motivo, ' ') NOT LIKE 'DEPURACION%'
        );
        
        IF SQL%ROWCOUNT > 0 THEN
            DBMS_OUTPUT.PUT_LINE('PASO 6-PHANTOM: Limpiadas ' || SQL%ROWCOUNT || ' marcaciones fantasma en dias de descanso (0 marcas en SCA_HISTORIAL)');
        END IF;
        
        -- =====================================================================
        -- PASO 6-PHANTOM-B: Limpiar SALIDA fantasma de turno nocturno en DESCANSO
        -- FIX 20/04/2026
        -- Caso: Turno nocturno del dia anterior (ej: Sabado E=22:51) cruza
        --       medianoche. La salida (ej: 07:02 del Domingo) queda registrada
        --       como SALIDA del tareo del dia siguiente (DESCANSO).
        --       A diferencia de PASO 6-PHANTOM, SCA_HISTORIAL SI tiene esa
        --       marca con fec_equiv = dia descanso (1 marca real).
        --       PASO 0B3/0B4 no actuan porque el tareo tiene la marca como
        --       SALIDA (no ENTRADA), por lo que sus condiciones no se cumplen.
        -- Ejemplo: Sabado 18/04 E=22:51 (nocturno)
        --          Domingo 19/04 DESCANSO: tareo entrada=NULL, salida=07:02
        --          SCA_HISTORIAL domingo: 1 marca (07:02 con fec_equiv=19/04)
        -- Accion: Limpiar salida/inirefri/finrefri del tareo DESCANSO, tag PH
        -- IMPORTANTE: PASO 8 y 8B excluyen dias PH para no recontear la marca
        --             fantasma de SCA_HISTORIAL.
        --
        -- TABLAS ESCRITURA: SCA_ASISTENCIA_TAREO (UPDATE)
        -- TABLAS CONSULTA:  SCA_ASISTENCIA_TAREO (dia anterior)
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t
        SET t.entrada = NULL,
            t.salida = NULL,
            t.inirefri = NULL,
            t.finrefri = NULL,
            t.nummarcaciones = 0,
            t.alerta01 = NULL,
            t.tothoramarcas = NULL,
            t.horarefrigerio = NULL,
            t.horaefectiva = NULL,
            t.horadobles = NULL,
            t.horatardanza = NULL,
            t.tothoranocturna = NULL,
            t.tothoranocturna_of = NULL,
            t.codaux4 = CASE WHEN t.codaux4 IS NULL THEN c_PH ELSE t.codaux4 || c_SEP || c_PH END,
            t.codaux5 = SUBSTR(CASE WHEN t.codaux5 IS NULL THEN d_PH ELSE t.codaux5 || c_SEP || d_PH END, 1, 50)
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
        AND t.descanso = 'S'
        AND t.entrada IS NULL                              -- Sin entrada real
        AND t.salida IS NOT NULL                           -- Pero tiene salida fantasma
        AND TO_CHAR(t.salida, 'HH24MI') < '1200'          -- Salida de madrugada (turno nocturno)
        AND NVL(t.codaux4, ' ') NOT LIKE '%' || c_PH || '%'  -- No procesado aun por PHANTOM
        AND NVL(t.ind_cerrado, 'N') <> 'S'
        -- Dia anterior fue turno nocturno (entrada >= 18:00)
        AND EXISTS (
            SELECT 1 FROM SCA_ASISTENCIA_TAREO t_ayer
            WHERE t_ayer.fechamar = t.fechamar - 1
            AND t_ayer.cod_empresa = t.cod_empresa
            AND t_ayer.cod_personal = t.cod_personal
            AND t_ayer.entrada IS NOT NULL
            AND TO_CHAR(t_ayer.entrada, 'HH24MI') >= '1800'
        );
        
        IF SQL%ROWCOUNT > 0 THEN
            DBMS_OUTPUT.PUT_LINE('PASO 6-PHANTOM-B: Limpiadas ' || SQL%ROWCOUNT || ' salidas fantasma de turno nocturno en dias de descanso');
        END IF;
        
        -- =====================================================================
        -- PASO 6-PHANTOM-C: Limpiar ENTRADA+SALIDA fantasma en DESCANSO cuando
        --   la entrada no existe en SCA_HISTORIAL para ese dia
        -- FIX 20/04/2026
        -- Caso: Turno nocturno del dia anterior (ej: Sabado E=22:45) cruza
        --       medianoche. Aquarius asigna esa misma entrada (22:45) al tareo
        --       del dia siguiente DESCANSO (Domingo). La salida (07:01) SI
        --       existe en SCA_HIS del Domingo pero la ENTRADA (22:45) NO.
        --       SCA_HIS del Domingo tiene marcas reales de checkpoint nocturno
        --       (00:11, 02:10, 04:09, 07:01) que pertenecen al turno del Sabado.
        -- Diferencia con otros PHANTOM:
        --   PHANTOM  : entrada IS NOT NULL pero SCA_HIS tiene 0 marcas -> no aplica
        --   PHANTOM-B: entrada IS NULL -> no aplica
        --   PHANTOM-C: entrada IS NOT NULL Y SCA_HIS tiene marcas PERO
        --              la entrada NO es ninguna de esas marcas (es fantasma)
        -- Condicion clave: NOT EXISTS (marca en SCA_HIS con hora = entrada del tareo)
        -- Ejemplo: Sabado 18/04 E=22:45 (nocturno)
        --          Domingo 19/04 DESCANSO: tareo E=22:45 (fantasma), S=07:01
        --          SCA_HIS domingo: 00:11, 02:10, 04:09, 07:01 (checkpoints)
        --          22:45 NO esta en SCA_HIS domingo -> PHANTOM-C dispara
        --
        -- TABLAS ESCRITURA: SCA_ASISTENCIA_TAREO (UPDATE)
        -- TABLAS CONSULTA:  SCA_HISTORIAL, SCA_ASISTENCIA_TAREO (dia anterior)
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t
        SET t.entrada = NULL,
            t.salida = NULL,
            t.inirefri = NULL,
            t.finrefri = NULL,
            t.nummarcaciones = 0,
            t.alerta01 = NULL,
            t.tothoramarcas = NULL,
            t.horarefrigerio = NULL,
            t.horaefectiva = NULL,
            t.horadobles = NULL,
            t.horatardanza = NULL,
            t.tothoranocturna = NULL,
            t.tothoranocturna_of = NULL,
            t.codaux4 = CASE WHEN t.codaux4 IS NULL THEN c_PH ELSE t.codaux4 || c_SEP || c_PH END,
            t.codaux5 = SUBSTR(CASE WHEN t.codaux5 IS NULL THEN d_PH ELSE t.codaux5 || c_SEP || d_PH END, 1, 50)
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
        AND t.descanso = 'S'
        AND t.entrada IS NOT NULL
        AND NVL(t.codaux4, ' ') NOT LIKE '%' || c_PH || '%'
        AND NVL(t.ind_cerrado, 'N') <> 'S'
        -- La ENTRADA del tareo NO existe como marca real en SCA_HISTORIAL para este dia
        -- (es una entrada fantasma copiada del turno nocturno del dia anterior)
        AND NOT EXISTS (
            SELECT 1 FROM SCA_HISTORIAL h
            WHERE h.idtarjeta = t.num_fotocheck
            AND h.fec_equiv = t.fechamar
            AND RTRIM(h.hora) = TO_CHAR(t.entrada, 'HH24:MI:SS')
            AND NVL(h.ind_anulado, 'N') <> 'S'
            AND NVL(h.ind_noprocesar, 0) = 0
            AND NVL(h.motivo, ' ') NOT LIKE 'DEPURACION%'
        )
        -- Dia anterior fue turno nocturno (entrada >= 18:00)
        AND EXISTS (
            SELECT 1 FROM SCA_ASISTENCIA_TAREO t_ayer
            WHERE t_ayer.fechamar = t.fechamar - 1
            AND t_ayer.cod_empresa = t.cod_empresa
            AND t_ayer.cod_personal = t.cod_personal
            AND t_ayer.entrada IS NOT NULL
            AND TO_CHAR(t_ayer.entrada, 'HH24MI') >= '1800'
        );
        
        IF SQL%ROWCOUNT > 0 THEN
            DBMS_OUTPUT.PUT_LINE('PASO 6-PHANTOM-C: Limpiadas ' || SQL%ROWCOUNT || ' entradas+salidas fantasma en dias de descanso (entrada no en SCA_HIS)');
        END IF;
        
        -- =====================================================================
        -- PASO 6-PHANTOM-D: Limpiar ENTRADA de madrugada en DESCANSO cuando
        --   la entrada proviene del turno nocturno del dia anterior
        -- FIX 21/04/2026
        -- Caso: Turno nocturno del dia anterior (ej: Sabado 22:52) cruza
        --       medianoche. La marca de SALIDA (07:00 FC) queda en SCA_HIS
        --       con fec_equiv=Domingo. Aquarius asigna esa marca (07:00) como
        --       ENTRADA del Domingo (dia de DESCANSO), creyendo que llego
        --       16 horas antes del horario (23:00 - 07:00 = 16h).
        -- Diferencia con otros PHANTOM:
        --   PHANTOM  : SCA_HIS tiene 0 marcas reales -> no aplica (hay 3 marcas)
        --   PHANTOM-B: entrada IS NULL -> no aplica (entrada=07:00)
        --   PHANTOM-C: entrada NOT EXISTS en SCA_HIS -> no aplica (07:00 si existe)
        --   PHANTOM-D: entrada EXISTS en SCA_HIS PERO es la madrugada de la
        --              salida del turno nocturno anterior (< 08:00)
        -- Ejemplo: LA TORRE ARROYO 032559, 19/04/2026 (Domingo)
        --   Sabado 18/04: entrada=22:52 nocturno, salida=07:01
        --   Domingo 19/04: DESCANSO, SCA_HIS tiene 07:00 FC, 07:01 Manual, 22:45 Manual
        --   Aquarius asigno: entrada=07:00 (FC de fec_equiv=19/04),
        --   horaantesentrada=16:00 (23:00-07:00), salida=07:01 o 22:45
        --   PASO 7A luego ajustaria entrada=22:45 (horario-15min) -> INCORRECTO
        -- Condicion clave: entrada < 08:00 + dia anterior fue nocturno
        --
        -- TABLAS ESCRITURA: SCA_ASISTENCIA_TAREO (UPDATE)
        -- TABLAS CONSULTA:  SCA_ASISTENCIA_TAREO (dia anterior)
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t
        SET t.entrada = NULL,
            t.salida = NULL,
            t.inirefri = NULL,
            t.finrefri = NULL,
            t.nummarcaciones = 0,
            t.alerta01 = NULL,
            t.tothoramarcas = NULL,
            t.horarefrigerio = NULL,
            t.horaefectiva = NULL,
            t.horadobles = NULL,
            t.horatardanza = NULL,
            t.tothoranocturna = NULL,
            t.tothoranocturna_of = NULL,
            t.horaantesentrada = NULL,
            t.horaextantes = NULL,
            t.horaextantesofi = NULL,
            t.codaux4 = CASE WHEN t.codaux4 IS NULL THEN c_PH ELSE t.codaux4 || c_SEP || c_PH END,
            t.codaux5 = SUBSTR(CASE WHEN t.codaux5 IS NULL THEN d_PH ELSE t.codaux5 || c_SEP || d_PH END, 1, 50)
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
        AND t.descanso = 'S'
        AND t.entrada IS NOT NULL
        -- Entrada en la madrugada = es la salida del turno nocturno anterior
        AND TO_CHAR(t.entrada, 'HH24MI') < '0800'
        AND NVL(t.codaux4, ' ') NOT LIKE '%' || c_PH || '%'
        AND NVL(t.ind_cerrado, 'N') <> 'S'
        -- Dia anterior fue turno nocturno (confirma que el 07:00 es salida de ayer)
        AND EXISTS (
            SELECT 1 FROM SCA_ASISTENCIA_TAREO t_ayer
            WHERE t_ayer.fechamar = t.fechamar - 1
            AND t_ayer.cod_empresa = t.cod_empresa
            AND t_ayer.cod_personal = t.cod_personal
            AND t_ayer.entrada IS NOT NULL
            AND TO_CHAR(t_ayer.entrada, 'HH24MI') >= '1800'
        );
        
        IF SQL%ROWCOUNT > 0 THEN
            DBMS_OUTPUT.PUT_LINE('PASO 6-PHANTOM-D: Limpiadas ' || SQL%ROWCOUNT || ' entradas madrugada de turno nocturno anterior en dias de descanso');
        END IF;
        
        -- =====================================================================
        -- PASO 0-CLEAN: Limpiar marcaciones duplicadas del sistema de captura
        -- Cuando: El dia tiene entrada=salida (misma hora exacta)
        --         Y esa hora es de madrugada (<08:00) 
        --         Y el dia anterior es turno nocturno (entrada >= 18:00)
        -- Esto indica que el sistema de captura duplico la salida del dia anterior
        -- Ejemplo: Dia 11 entrada=18:55 salida=NULL
        --          Dia 12 entrada=07:03 salida=07:03 nummarcaciones=1 (BASURA)
        --          La marca 07:03 es la salida del dia 11, no trabajo del dia 12
        -- Accion: Limpiar dia 12 (entrada=NULL, salida=NULL, nummarcaciones=0)
        --         Mover la marca al dia anterior como salida
        --
        -- TABLAS ESCRITURA: SCA_ASISTENCIA_TAREO (UPDATE), SCA_HISTORIAL (UPDATE/DELETE)
        -- TABLAS CONSULTA:  SCA_ASISTENCIA_TAREO
        -- =====================================================================
        FOR rec_clean IN (
            SELECT t.ROWID AS rid, t.entrada, t.salida, t.num_fotocheck, t.fechamar,
                   t.cod_empresa, t.cod_personal,
                   (SELECT t_ayer.salida
                    FROM SCA_ASISTENCIA_TAREO t_ayer
                    WHERE t_ayer.fechamar = t.fechamar - 1
                    AND t_ayer.cod_empresa = t.cod_empresa
                    AND t_ayer.cod_personal = t.cod_personal) AS salida_ayer
            FROM SCA_ASISTENCIA_TAREO t
            WHERE t.fechamar = v_fecha_proceso
            AND t.cod_empresa LIKE v_empresa_filtro
            AND t.cod_personal LIKE v_personal_filtro
            AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
            AND t.entrada IS NOT NULL
            AND t.salida IS NOT NULL
            -- Marca duplicada: entrada = salida (misma hora exacta)
            AND TO_CHAR(t.entrada, 'HH24:MI:SS') = TO_CHAR(t.salida, 'HH24:MI:SS')
            -- Hora de madrugada (salida de turno nocturno)
            AND TO_CHAR(t.entrada, 'HH24MI') < '0800'
            AND NVL(t.ind_cerrado, 'N') <> 'S'
            -- El dia anterior tiene turno nocturno (entrada >= 18:00)
            -- Caso A: sin salida (incompleto) -> mover marca a ayer
            -- Caso B: ya tiene salida = misma hora -> solo limpiar hoy
            AND EXISTS (
                SELECT 1 FROM SCA_ASISTENCIA_TAREO t_ayer
                WHERE t_ayer.fechamar = t.fechamar - 1
                AND t_ayer.cod_empresa = t.cod_empresa
                AND t_ayer.cod_personal = t.cod_personal
                AND t_ayer.entrada IS NOT NULL
                AND TO_CHAR(t_ayer.entrada, 'HH24MI') >= '1800'  -- Turno nocturno
                AND (
                    t_ayer.salida IS NULL                         -- Caso A: sin salida
                    OR TO_CHAR(t_ayer.salida, 'HH24:MI:SS') = TO_CHAR(t.entrada, 'HH24:MI:SS')  -- Caso B: ya tiene misma salida
                )
            )
        ) LOOP
            -- Poner la salida en el dia anterior (SOLO si no la tiene - Caso A)
            IF rec_clean.salida_ayer IS NULL THEN
                UPDATE SCA_ASISTENCIA_TAREO t_ayer
                SET t_ayer.salida = rec_clean.entrada,
                    t_ayer.codaux4 = CASE WHEN t_ayer.codaux4 IS NULL THEN c_N1 ELSE t_ayer.codaux4 || c_SEP || c_N1 END,
                    t_ayer.codaux5 = SUBSTR(CASE WHEN t_ayer.codaux5 IS NULL THEN d_N1_ayer ELSE t_ayer.codaux5 || c_SEP || d_N1_ayer END, 1, 50)
                WHERE t_ayer.fechamar = rec_clean.fechamar - 1
                AND t_ayer.cod_empresa = rec_clean.cod_empresa
                AND t_ayer.cod_personal = rec_clean.cod_personal;
            END IF;
            
            -- Limpiar el registro basura del dia actual (ambos casos)
            -- FIX 23/04/2026: limpiar tambien alerta01='MI' que quedaba huerfana
            -- (caso real: 19/04/2026 fotochecks 032217, 037513, 037665 con tag NC
            --  pero alerta01='MI' persistia tras quedar nummarcaciones=0)
            UPDATE SCA_ASISTENCIA_TAREO
            SET entrada = NULL,
                salida = NULL,
                inirefri = NULL,
                finrefri = NULL,
                nummarcaciones = 0,
                alerta01 = NULL,
                tothoramarcas = NULL,
                horarefrigerio = NULL,
                horaefectiva = NULL,
                horatardanza = NULL,
                tothoranocturna = NULL,
                tothoranocturna_of = NULL,
                codaux4 = CASE WHEN codaux4 IS NULL THEN c_NC ELSE codaux4 || c_SEP || c_NC END,
                codaux5 = SUBSTR(CASE WHEN codaux5 IS NULL THEN d_NC ELSE codaux5 || c_SEP || d_NC END, 1, 50)
            WHERE ROWID = rec_clean.rid;
            
            -- Mover la marca en SCA_HISTORIAL al dia anterior (solo si ayer no la tiene ya)
            UPDATE SCA_HISTORIAL h
            SET h.fec_equiv = rec_clean.fechamar - 1,
                h.motivo = 'DEPURACION: Marca movida a dia anterior (NC)'
            WHERE h.idtarjeta = rec_clean.num_fotocheck
            AND h.fec_equiv = rec_clean.fechamar
            AND RTRIM(h.hora) = TO_CHAR(rec_clean.entrada, 'HH24:MI:SS')
            AND NVL(h.ind_anulado, 'N') <> 'S'
            AND NOT EXISTS (
                SELECT 1 FROM SCA_HISTORIAL h2
                WHERE h2.idtarjeta = rec_clean.num_fotocheck
                AND h2.fec_equiv = rec_clean.fechamar - 1
                AND RTRIM(h2.hora) = TO_CHAR(rec_clean.entrada, 'HH24:MI:SS')
            );
            
            -- Si no se pudo mover (ayer ya tiene esa marca), eliminar duplicado de hoy
            IF SQL%ROWCOUNT = 0 THEN
                DELETE FROM SCA_HISTORIAL h
                WHERE h.idtarjeta = rec_clean.num_fotocheck
                AND h.fec_equiv = rec_clean.fechamar
                AND RTRIM(h.hora) = TO_CHAR(rec_clean.entrada, 'HH24:MI:SS')
                AND NVL(h.ind_anulado, 'N') <> 'S';
            END IF;
            
            DBMS_OUTPUT.PUT_LINE('PASO 0-CLEAN: Marca duplicada ' || 
                TO_CHAR(rec_clean.entrada, 'HH24:MI:SS') ||
                CASE WHEN rec_clean.salida_ayer IS NULL
                    THEN ' movida de ' || TO_CHAR(rec_clean.fechamar, 'DD/MM') || ' a ' || TO_CHAR(rec_clean.fechamar - 1, 'DD/MM')
                    ELSE ' limpiada de ' || TO_CHAR(rec_clean.fechamar, 'DD/MM') || ' (ayer ya tiene salida)'
                END ||
                ' (fotocheck ' || rec_clean.num_fotocheck || ')');
            
            v_count_nocturno := v_count_nocturno + 1;
        END LOOP;
        
        -- =====================================================================
        -- PASO 0-SWAP: Corregir campos invertidos por sistema de captura
        -- Cuando: El sistema copio la salida del dia anterior en la entrada de hoy
        --         Y la entrada real de hoy quedo en el campo salida
        -- Detectar: entrada < 08:00 (madrugada) Y salida >= 18:00 (nocturno)
        --           Y entrada coincide con salida del dia anterior
        -- Ejemplo: Dia 11 salida=07:03, Dia 12 entrada=07:03 salida=22:50
        --          Corregir: Dia 12 entrada=22:50 salida=NULL (se completa despues)
        -- Accion: Mover salida a entrada, limpiar salida, mover marca historial
        --
        -- TABLAS ESCRITURA: SCA_ASISTENCIA_TAREO (UPDATE), SCA_HISTORIAL (UPDATE)
        -- TABLAS CONSULTA:  SCA_ASISTENCIA_TAREO
        -- =====================================================================
        FOR rec_swap IN (
            SELECT t.ROWID AS rid, t.entrada, t.salida, t.num_fotocheck, t.fechamar,
                   t.cod_empresa, t.cod_personal, t.salida_fijada, t.descanso,
                   -- Si es descanso, tomar salida_fijada del dia anterior (turno nocturno)
                   (SELECT t_ayer.salida_fijada 
                    FROM SCA_ASISTENCIA_TAREO t_ayer
                    WHERE t_ayer.fechamar = t.fechamar - 1
                    AND t_ayer.cod_empresa = t.cod_empresa
                    AND t_ayer.cod_personal = t.cod_personal) AS salida_fijada_ayer
            FROM SCA_ASISTENCIA_TAREO t
            WHERE t.fechamar = v_fecha_proceso
            AND t.cod_empresa LIKE v_empresa_filtro
            AND t.cod_personal LIKE v_personal_filtro
            AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
            AND t.entrada IS NOT NULL
            AND t.salida IS NOT NULL
            -- Campos invertidos: entrada de madrugada, salida nocturna
            AND TO_CHAR(t.entrada, 'HH24MI') < '0800'         -- Entrada < 08:00
            AND TO_CHAR(t.salida, 'HH24MI') >= '1800'         -- Salida >= 18:00
            -- NO son iguales (ese caso lo maneja PASO 0-CLEAN)
            AND TO_CHAR(t.entrada, 'HH24:MI:SS') <> TO_CHAR(t.salida, 'HH24:MI:SS')
            AND NVL(t.ind_cerrado, 'N') <> 'S'
            -- NO excluir descanso: si tiene marcacion, se procesa
            -- La entrada actual coincide con la salida del dia anterior
            AND EXISTS (
                SELECT 1 FROM SCA_ASISTENCIA_TAREO t_ayer
                WHERE t_ayer.fechamar = t.fechamar - 1
                AND t_ayer.cod_empresa = t.cod_empresa
                AND t_ayer.cod_personal = t.cod_personal
                AND t_ayer.salida IS NOT NULL
                AND TO_CHAR(t_ayer.salida, 'HH24:MI:SS') = TO_CHAR(t.entrada, 'HH24:MI:SS')
            )
        ) LOOP
            -- Determinar salida teorica: usar la del dia o la del dia anterior si es descanso
            IF rec_swap.salida_fijada IS NOT NULL 
               AND TO_CHAR(rec_swap.salida_fijada, 'HH24MI') <> '0000' THEN
                v_salida_teorica := rec_swap.salida_fijada;
            ELSIF rec_swap.salida_fijada_ayer IS NOT NULL 
                  AND TO_CHAR(rec_swap.salida_fijada_ayer, 'HH24MI') <> '0000' THEN
                v_salida_teorica := rec_swap.salida_fijada_ayer;
            ELSE
                -- Default 07:00 para turno nocturno
                v_salida_teorica := TO_DATE('07:00', 'HH24:MI');
            END IF;
            
            -- Corregir: mover salida a entrada, poner salida teorica
            -- Si es DESCANSO, horaefectiva = 0
            UPDATE SCA_ASISTENCIA_TAREO
            SET entrada = rec_swap.salida,
                salida = v_salida_teorica,
                inirefri = NULL,
                finrefri = NULL,
                nummarcaciones = 2,
                horaefectiva = CASE WHEN NVL(rec_swap.descanso, 'N') = 'S' THEN NULL ELSE horaefectiva END,
                codaux4 = CASE WHEN codaux4 IS NULL THEN c_N4 ELSE codaux4 || c_SEP || c_N4 END,
                codaux5 = SUBSTR(CASE WHEN codaux5 IS NULL THEN N'Noct: campos invertidos' ELSE codaux5 || c_SEP || N'Noct: campos invertidos' END, 1, 50)
            WHERE ROWID = rec_swap.rid;
            
            -- Mover la marca de "entrada" (que era copia) al dia anterior en SCA_HISTORIAL
            UPDATE SCA_HISTORIAL h
            SET h.fec_equiv = rec_swap.fechamar - 1,
                h.motivo = 'DEPURACION: Marca movida a dia anterior (SWAP)'
            WHERE h.idtarjeta = rec_swap.num_fotocheck
            AND h.fec_equiv = rec_swap.fechamar
            AND RTRIM(h.hora) = TO_CHAR(rec_swap.entrada, 'HH24:MI:SS')
            AND NVL(h.ind_anulado, 'N') <> 'S';
            
            -- Insertar marca de salida teorica en SCA_HISTORIAL
            INSERT INTO SCA_HISTORIAL (idCod, idLectora, idTarjeta, fecha, hora, tiporeg, fecreg, fec_equiv, motivo, ind_aman_hor_est)
            SELECT id_cod_seq.NEXTVAL, '005', rec_swap.num_fotocheck,
                   TO_CHAR(rec_swap.fechamar, 'DD/MM/YYYY'),
                   TO_CHAR(v_salida_teorica, 'HH24:MI:SS'),
                   '3', SYSDATE, rec_swap.fechamar,
                   'DEPURACION: Salida teorica nocturna (SWAP)', 'A'
            FROM DUAL
            WHERE NOT EXISTS (
                SELECT 1 FROM SCA_HISTORIAL h
                WHERE h.idtarjeta = rec_swap.num_fotocheck
                AND h.fec_equiv = rec_swap.fechamar
                AND RTRIM(h.hora) = TO_CHAR(v_salida_teorica, 'HH24:MI:SS')
            );
            
            IF SQL%ROWCOUNT > 0 THEN
                v_count_historial := v_count_historial + 1;
            END IF;
            
            DBMS_OUTPUT.PUT_LINE('PASO 0-SWAP: Campos invertidos corregidos para fotocheck ' || 
                rec_swap.num_fotocheck || ' - entrada ' || TO_CHAR(rec_swap.entrada, 'HH24:MI') || 
                ' (basura) -> entrada ' || TO_CHAR(rec_swap.salida, 'HH24:MI') || 
                ', salida ' || TO_CHAR(v_salida_teorica, 'HH24:MI') || 
                CASE WHEN NVL(rec_swap.descanso, 'N') = 'S' THEN ' (DESCANSO)' ELSE '' END);
            
            v_count_nocturno := v_count_nocturno + 1;
        END LOOP;
        
        -- =====================================================================
        -- PASO 0A: TURNO NOCTURNO - Mover ENTRADA de madrugada a SALIDA del dia anterior
        -- Cuando: El dia anterior tiene ENTRADA nocturna (>=20:00) sin SALIDA
        --         Y el dia actual tiene ENTRADA de madrugada (<08:00)
        -- Accion: La ENTRADA de hoy es realmente la SALIDA de ayer
        -- =====================================================================
        -- Primero: Actualizar el dia ANTERIOR poniendo la SALIDA
        UPDATE SCA_ASISTENCIA_TAREO t_ayer
        SET t_ayer.salida = (
                SELECT t_hoy.entrada 
                FROM SCA_ASISTENCIA_TAREO t_hoy
                WHERE t_hoy.fechamar = t_ayer.fechamar + 1
                AND t_hoy.cod_empresa = t_ayer.cod_empresa
                AND t_hoy.cod_personal = t_ayer.cod_personal
                AND t_hoy.entrada IS NOT NULL
                AND TO_CHAR(t_hoy.entrada, 'HH24MI') < '0800'  -- Entrada antes de 08:00
            ),
            t_ayer.codaux4 = CASE WHEN t_ayer.codaux4 IS NULL THEN c_N1 ELSE t_ayer.codaux4 || c_SEP || c_N1 END,
            t_ayer.codaux5 = SUBSTR(CASE WHEN t_ayer.codaux5 IS NULL THEN d_N1_ayer ELSE t_ayer.codaux5 || c_SEP || d_N1_ayer END, 1, 50)
        WHERE t_ayer.fechamar = v_fecha_proceso - 1           -- Dia anterior
        AND t_ayer.cod_empresa LIKE v_empresa_filtro
        AND t_ayer.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t_ayer.ind_obrero = 'S')
        AND t_ayer.entrada IS NOT NULL
        AND TO_CHAR(t_ayer.entrada, 'HH24MI') >= '1800'       -- Entrada nocturna >= 18:00 (captura turnos 19:00 con llegada temprana)
        AND t_ayer.salida IS NULL                              -- No tiene salida
        -- NO filtrar por descanso: la marca de madrugada puede caer en dia de descanso
        AND NVL(t_ayer.ind_cerrado, 'N') <> 'S'
        -- Excluir si ya fue procesado por PASO 0-CLEAN o PASO 0-SWAP
        AND NVL(t_ayer.codaux4, ' ') NOT LIKE '%' || 'NC' || '%'
        AND EXISTS (
            SELECT 1 FROM SCA_ASISTENCIA_TAREO t_hoy
            WHERE t_hoy.fechamar = t_ayer.fechamar + 1
            AND t_hoy.cod_empresa = t_ayer.cod_empresa
            AND t_hoy.cod_personal = t_ayer.cod_personal
            AND t_hoy.entrada IS NOT NULL
            AND TO_CHAR(t_hoy.entrada, 'HH24MI') < '0800'
            -- Excluir si dia de hoy ya fue procesado por PASO 0-CLEAN o SWAP
            AND NVL(t_hoy.codaux4, ' ') NOT LIKE '%NC%'
            AND NVL(t_hoy.codaux4, ' ') NOT LIKE '%N4%'
        );
        
        -- =====================================================================
        -- PASO 0B: TURNO NOCTURNO - Limpiar ENTRADA incorrecta del dia actual
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t_hoy
        SET t_hoy.entrada = NULL,
            t_hoy.nummarcaciones = t_hoy.nummarcaciones - 1,
            t_hoy.codaux4 = CASE WHEN t_hoy.codaux4 IS NULL THEN c_N1 ELSE t_hoy.codaux4 || c_SEP || c_N1 END,
            t_hoy.codaux5 = SUBSTR(CASE WHEN t_hoy.codaux5 IS NULL THEN d_N1_hoy ELSE t_hoy.codaux5 || c_SEP || d_N1_hoy END, 1, 50)
        WHERE t_hoy.fechamar = v_fecha_proceso
        AND t_hoy.cod_empresa LIKE v_empresa_filtro
        AND t_hoy.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t_hoy.ind_obrero = 'S')
        AND t_hoy.entrada IS NOT NULL
        AND TO_CHAR(t_hoy.entrada, 'HH24MI') < '0800'         -- Entrada antes de 08:00
        -- NO filtrar por descanso: la marca de madrugada puede caer en dia de descanso
        AND NVL(t_hoy.ind_cerrado, 'N') <> 'S'
        -- Excluir si ya fue procesado por PASO 0-CLEAN o PASO 0-SWAP
        AND NVL(t_hoy.codaux4, ' ') NOT LIKE '%NC%'
        AND NVL(t_hoy.codaux4, ' ') NOT LIKE '%N4%'
        AND EXISTS (
            SELECT 1 FROM SCA_ASISTENCIA_TAREO t_ayer
            WHERE t_ayer.fechamar = t_hoy.fechamar - 1
            AND t_ayer.cod_empresa = t_hoy.cod_empresa
            AND t_ayer.cod_personal = t_hoy.cod_personal
            AND t_ayer.entrada IS NOT NULL
            AND TO_CHAR(t_ayer.entrada, 'HH24MI') >= '1800'   -- Ayer tuvo entrada nocturna (>= 18:00)
            AND t_ayer.salida = t_hoy.entrada                  -- La salida de ayer es mi entrada
            AND t_ayer.codaux4 LIKE '%N1%'                     -- Solo si PASO 0A puso esa salida
        );
        
        v_count_nocturno := v_count_nocturno + SQL%ROWCOUNT;
        
        -- =====================================================================
        -- PASO 0B-HIS: Mover marca en SCA_HISTORIAL al dia anterior
        -- Cuando: PASO 0A/0B movieron la marca de hoy a ayer, tambien debemos
        --         actualizar fec_equiv en SCA_HISTORIAL para mantener consistencia
        -- Solo para registros procesados por N1 (no NC ni N4 que ya manejan historial)
        -- =====================================================================
        UPDATE SCA_HISTORIAL h
        SET h.fec_equiv = v_fecha_proceso - 1,
            h.motivo = 'DEPURACION: Marca movida a dia anterior (N1)'
        WHERE h.fec_equiv = v_fecha_proceso
        AND TO_NUMBER(SUBSTR(RTRIM(h.hora), 1, 2)) < 8        -- Marca de madrugada (comparacion numerica)
        AND h.idtarjeta IN (
            SELECT t.num_fotocheck
            FROM SCA_ASISTENCIA_TAREO t
            WHERE t.fechamar = v_fecha_proceso
            AND t.cod_empresa LIKE v_empresa_filtro
            AND t.cod_personal LIKE v_personal_filtro
            AND t.codaux4 LIKE '%' || c_N1 || '%'             -- Ya procesado por N1
            AND NVL(t.codaux4, ' ') NOT LIKE '%NC%'           -- No procesado por 0-CLEAN
            AND NVL(t.codaux4, ' ') NOT LIKE '%N4%'           -- No procesado por 0-SWAP
        )
        AND NVL(h.ind_anulado, 'N') <> 'S';
        
        -- =====================================================================
        -- PASO 0B2: TURNO NOCTURNO - Reubicar SALIDA vespertina como ENTRADA post-N1
        -- Cuando: PASO 0B limpio la ENTRADA (movida a dia anterior por N1)
        --         Queda ENTRADA=NULL, SALIDA no nula en horario vespertino (>=15:00)
        --         La SALIDA es realmente la ENTRADA de un nuevo turno nocturno
        -- Ejemplo: Horario 19-03, dia descanso. Despues de N1:
        --          ENTRADA=NULL, SALIDA=18:55 -> ENTRADA=18:55, SALIDA=NULL
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t_hoy
        SET t_hoy.entrada = t_hoy.salida,
            t_hoy.salida = NULL,
            t_hoy.codaux4 = CASE WHEN t_hoy.codaux4 IS NULL THEN c_N4 ELSE t_hoy.codaux4 || c_SEP || c_N4 END,
            t_hoy.codaux5 = SUBSTR(CASE WHEN t_hoy.codaux5 IS NULL THEN d_N4 ELSE t_hoy.codaux5 || c_SEP || d_N4 END, 1, 50)
        WHERE t_hoy.fechamar = v_fecha_proceso
        AND t_hoy.cod_empresa LIKE v_empresa_filtro
        AND t_hoy.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t_hoy.ind_obrero = 'S')
        AND t_hoy.entrada IS NULL                                -- Entrada fue limpiada por N1
        AND t_hoy.salida IS NOT NULL                             -- Queda salida huerfana
        AND TO_CHAR(t_hoy.salida, 'HH24MI') >= '1500'           -- Horario vespertino/nocturno
        AND t_hoy.codaux4 LIKE '%' || c_N1 || '%'                -- Ya fue procesado por PASO 0B
        AND NVL(t_hoy.ind_cerrado, 'N') <> 'S';
        
        v_count_nocturno := v_count_nocturno + SQL%ROWCOUNT;
        
        -- =====================================================================
        -- PASO 0B3: TURNO NOCTURNO - Completar SALIDA desde dia siguiente (forward)
        -- Cuando: Hoy tiene ENTRADA nocturna (>=18:00) y SALIDA es NULL
        --         Y el dia siguiente tiene ENTRADA de madrugada (<08:00)
        --         Esa marca es realmente la SALIDA de hoy, no la ENTRADA de manana
        -- Ejemplo: 29/03 ENTRADA=18:55, SALIDA=NULL + 30/03 ENTRADA=07:08
        --       -> 29/03 SALIDA=07:08 + 30/03 ENTRADA se limpia
        -- NOTA: Espejo de PASO 0A pero mirando HACIA ADELANTE
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t
        SET t.salida = (
                SELECT t_sig.entrada 
                FROM SCA_ASISTENCIA_TAREO t_sig
                WHERE t_sig.fechamar = t.fechamar + 1
                AND t_sig.cod_empresa = t.cod_empresa
                AND t_sig.cod_personal = t.cod_personal
                AND t_sig.entrada IS NOT NULL
                AND TO_CHAR(t_sig.entrada, 'HH24MI') < '0800'
            ),
            t.codaux4 = CASE WHEN t.codaux4 IS NULL THEN c_N1 ELSE t.codaux4 || c_SEP || c_N1 END,
            t.codaux5 = SUBSTR(CASE WHEN t.codaux5 IS NULL THEN d_N1_ayer ELSE t.codaux5 || c_SEP || d_N1_ayer END, 1, 50)
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
        AND t.entrada IS NOT NULL
        AND TO_CHAR(t.entrada, 'HH24MI') >= '1800'            -- Entrada nocturna >= 18:00
        AND t.salida IS NULL                                   -- No tiene salida
        AND NVL(t.ind_cerrado, 'N') <> 'S'
        AND EXISTS (
            SELECT 1 FROM SCA_ASISTENCIA_TAREO t_sig
            WHERE t_sig.fechamar = t.fechamar + 1
            AND t_sig.cod_empresa = t.cod_empresa
            AND t_sig.cod_personal = t.cod_personal
            AND t_sig.entrada IS NOT NULL
            AND TO_CHAR(t_sig.entrada, 'HH24MI') < '0800'
        );
        
        v_count_nocturno := v_count_nocturno + SQL%ROWCOUNT;
        
        -- =====================================================================
        -- PASO 0B3c: TURNO NOCTURNO - Salida real en dia siguiente (sobretiempo)
        -- Cuando: Hoy tiene ENTRADA nocturna (>=18:00) y SALIDA ya asignada
        --         PERO la salida es ANTERIOR a la entrada (mismo dia calendariamente)
        --         = Aquarius asigno una marca de madrugada del mismo dia como salida
        --         Y en el dia siguiente existe una marca real posterior = salida real
        -- Ejemplo: 17/04 E=18:56, S=17/04 03:00 (Manual, INCORRECTO)
        --          SCA_HIS 18/04: 07:03 Fotocheck (no es entrada del tareo 18/04)
        --       -> 17/04 S=18/04 07:03 (sobretiempo real: 4h extras)
        -- Deteccion: t.salida < t.entrada (comparacion DATE completa)
        --   - Correcto: entrada=17/04 18:56, salida=18/04 07:03 -> salida > entrada (no dispara)
        --   - Incorrecto: entrada=17/04 18:56, salida=17/04 03:00 -> salida < entrada (DISPARA)
        -- Diferencia con PASO 0B3: ese PASO actua cuando salida IS NULL.
        --   Este PASO actua cuando salida IS NOT NULL pero es INVALIDA (< entrada)
        --   = marca del mismo dia que Aquarius interpreto como salida nocturna,
        --     ignorando la marca real del dia siguiente.
        -- Exclusion c_N4: PASO 0-SWAP puede dejar salida < entrada (teorica),
        --   esos casos ya estan marcados c_N4 y seran completados por 0B3/0B3b.
        --
        -- TABLAS ESCRITURA: SCA_ASISTENCIA_TAREO (UPDATE)
        -- TABLAS CONSULTA:  SCA_HISTORIAL, SCA_ASISTENCIA_TAREO (dia siguiente)
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t
        SET t.salida = (
                SELECT TO_DATE(
                           TO_CHAR(t.fechamar + 1, 'DD/MM/YYYY') || ' ' ||
                           MAX(RTRIM(h2.hora)),
                           'DD/MM/YYYY HH24:MI:SS')
                FROM SCA_HISTORIAL h2
                WHERE h2.idtarjeta = t.num_fotocheck
                AND h2.fec_equiv = t.fechamar + 1
                AND RTRIM(h2.hora) >= TO_CHAR(t.salida, 'HH24:MI:SS')  -- >= incluye caso hora=salida exacta (ej: 07:01 = 07:01)
                AND RTRIM(h2.hora) < '12:00:00'
                AND NVL(h2.ind_anulado, 'N') <> 'S'
                AND NVL(h2.ind_noprocesar, 0) = 0
                AND NVL(h2.motivo, ' ') NOT LIKE 'DEPURACION%'
                AND NOT EXISTS (
                    SELECT 1 FROM SCA_ASISTENCIA_TAREO t_sig
                    WHERE t_sig.fechamar = t.fechamar + 1
                    AND t_sig.cod_empresa = t.cod_empresa
                    AND t_sig.cod_personal = t.cod_personal
                    AND TO_CHAR(t_sig.entrada, 'HH24:MI:SS') = RTRIM(h2.hora)
                )
            ),
            t.codaux4 = CASE WHEN t.codaux4 IS NULL THEN c_N6 ELSE t.codaux4 || c_SEP || c_N6 END,
            t.codaux5 = SUBSTR(CASE WHEN t.codaux5 IS NULL THEN d_N6 ELSE t.codaux5 || c_SEP || d_N6 END, 1, 50)
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
        AND t.entrada IS NOT NULL
        AND TO_CHAR(t.entrada, 'HH24MI') >= '1800'             -- Turno nocturno
        AND t.salida IS NOT NULL
        AND t.salida < t.entrada                                -- Salida INVALIDA: anterior a entrada (mismo dia)
        AND NVL(t.descanso, 'N') <> 'S'
        AND NVL(t.ind_cerrado, 'N') <> 'S'
        AND NVL(t.codaux4, ' ') NOT LIKE '%' || c_N4 || '%'    -- No procesado por SWAP (falso positivo)
        AND t.num_fotocheck IS NOT NULL
        AND EXISTS (
            SELECT 1 FROM SCA_HISTORIAL h2
            WHERE h2.idtarjeta = t.num_fotocheck
            AND h2.fec_equiv = t.fechamar + 1
            AND RTRIM(h2.hora) >= TO_CHAR(t.salida, 'HH24:MI:SS')  -- >= incluye caso hora=salida exacta
            AND RTRIM(h2.hora) < '12:00:00'
            AND NVL(h2.ind_anulado, 'N') <> 'S'
            AND NVL(h2.ind_noprocesar, 0) = 0
            AND NVL(h2.motivo, ' ') NOT LIKE 'DEPURACION%'
            AND NOT EXISTS (
                SELECT 1 FROM SCA_ASISTENCIA_TAREO t_sig
                WHERE t_sig.fechamar = t.fechamar + 1
                AND t_sig.cod_empresa = t.cod_empresa
                AND t_sig.cod_personal = t.cod_personal
                AND TO_CHAR(t_sig.entrada, 'HH24:MI:SS') = RTRIM(h2.hora)
            )
        );
        
        IF SQL%ROWCOUNT > 0 THEN
            DBMS_OUTPUT.PUT_LINE('PASO 0B3c: Salida nocturna extendida -> ' || SQL%ROWCOUNT || ' registros (sobretiempo DIA+1)');
        END IF;
        v_count_nocturno := v_count_nocturno + SQL%ROWCOUNT;
        
        -- =====================================================================
        -- PASO 0B3b: TURNO NOCTURNO - Salida TEORICA cuando no hay marca forward
        -- Cuando: PASO 0B3 no encontro marca matutina en dia siguiente
        --         El registro tiene ENTRADA nocturna (>=18:00) y SALIDA sigue NULL
        --         Calcular SALIDA = ENTRADA + tothoras del horario (dia no-descanso)
        -- Ejemplo: Horario 19-03 (8h), entrada=18:55
        --       -> salida teorica = 18:55 + 8h = 02:55 del dia siguiente
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t
        SET t.salida = t.entrada + (
                SELECT MAX(d.tothoras) - TO_DATE('01/01/1900', 'dd/MM/yyyy')
                FROM SCA_HORARIO_DET d 
                WHERE d.horid = t.horid 
                AND NVL(d.descanso, 'N') <> 'S'
                AND d.tothoras > TO_DATE('01/01/1900', 'dd/MM/yyyy')
            ),
            t.codaux4 = CASE WHEN t.codaux4 IS NULL THEN c_N5 ELSE t.codaux4 || c_SEP || c_N5 END,
            t.codaux5 = SUBSTR(CASE WHEN t.codaux5 IS NULL THEN d_N5 ELSE t.codaux5 || c_SEP || d_N5 END, 1, 50)
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
        AND t.entrada IS NOT NULL
        AND TO_CHAR(t.entrada, 'HH24MI') >= '1800'            -- Entrada nocturna
        AND t.salida IS NULL                                   -- Aun sin salida (PASO 0B3 no encontro)
        AND t.codaux4 LIKE '%' || c_N4 || '%'                  -- Ya fue procesado por N4
        AND t.horid IS NOT NULL                                -- Tiene horario asignado
        AND NVL(t.ind_cerrado, 'N') <> 'S'
        AND EXISTS (
            SELECT 1 FROM SCA_HORARIO_DET d 
            WHERE d.horid = t.horid 
            AND NVL(d.descanso, 'N') <> 'S'
            AND d.tothoras > TO_DATE('01/01/1900', 'dd/MM/yyyy')
        );
        
        v_count_nocturno := v_count_nocturno + SQL%ROWCOUNT;
        
        -- INSERT a SCA_HISTORIAL para salida teorica nocturna (PASO 0B3b)
        INSERT INTO SCA_HISTORIAL (idCod, idLectora, idTarjeta, fecha, hora, tiporeg, fecreg, fec_equiv, motivo, ind_aman_hor_est)
        SELECT id_cod_seq.NEXTVAL, '005', t.num_fotocheck,
               TO_CHAR(t.fechamar, 'DD/MM/YYYY'),  -- FIX: usar fechamar
               TO_CHAR(t.salida, 'HH24:MI:SS'),
               '3', SYSDATE, t.fechamar,
               'DEPURACION: Salida noct teorica 0B3b', 'A'
        FROM SCA_ASISTENCIA_TAREO t
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND t.salida IS NOT NULL
        AND t.codaux4 LIKE '%' || c_N5 || '%'
        AND t.num_fotocheck IS NOT NULL
        AND NOT EXISTS (
            SELECT 1 FROM SCA_HISTORIAL h
            WHERE h.idtarjeta = t.num_fotocheck
            AND h.fec_equiv = t.fechamar
            AND RTRIM(h.hora) = TO_CHAR(t.salida, 'HH24:MI:SS')
        );
        v_count_historial := v_count_historial + SQL%ROWCOUNT;
        
        -- =====================================================================
        -- PASO 0B3d: TURNO VESPERTINO - Re-asignar ENTRADA a marca temprana
        -- Cuando: Horario vespertino (entrada_fijada entre 17:00 y 22:00)
        --         Y la entrada actual del tareo es MAS DE 2h despues del inicio
        --         del turno (ej: entrada=22:47, entrada_fijada=19:00).
        --         Y existe una marca valida en SCA_HIS del mismo dia dentro de
        --         la ventana [entrada_fijada-2h, entrada_fijada+2h] ANTERIOR
        --         a la entrada actual (ej: 18:48 entre 17:00 y 21:00).
        --         Y la salida ya es correcta (salida > entrada = cross-day shift).
        -- Ejemplo: 13/04 HORARIO 19-03
        --          SCA_HIS 13/04: 18:48 FC, 22:47 FC, 03:00 Manual
        --          Tareo: E=22:47 (incorrecto, Aquarius ignoro 18:48)
        --       -> E=18:48 (marca temprana en ventana 17:00-21:00)
        -- Diferencia con PASO 5G:
        --   PASO 5G: entrada_fijada >= 22:00 (3er turno) + 2h anticipados
        --   PASO 0B3d: entrada_fijada 17:00-22:00 (turno vespertino) + entrada
        --              asignada INCORRECTAMENTE tarde por Aquarius
        -- REQUISITO: salida > entrada (salida ya fue corregida al dia siguiente)
        --
        -- TABLAS ESCRITURA: SCA_ASISTENCIA_TAREO (UPDATE)
        -- TABLAS CONSULTA:  SCA_HISTORIAL
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t
        SET t.entrada = (
                SELECT TO_DATE(
                           TO_CHAR(t.fechamar, 'DD/MM/YYYY') || ' ' ||
                           MIN(RTRIM(h.hora)),
                           'DD/MM/YYYY HH24:MI:SS')
                FROM SCA_HISTORIAL h
                WHERE h.idtarjeta = t.num_fotocheck
                AND h.fec_equiv = t.fechamar
                AND RTRIM(h.hora) >= TO_CHAR(t.entrada_fijada - (2/24), 'HH24:MI:SS')
                AND RTRIM(h.hora) <= TO_CHAR(t.entrada_fijada + (2/24), 'HH24:MI:SS')
                AND RTRIM(h.hora) < TO_CHAR(t.entrada, 'HH24:MI:SS')
                AND NVL(h.ind_anulado, 'N') <> 'S'
                AND NVL(h.ind_noprocesar, 0) = 0
                AND NVL(h.motivo, ' ') NOT LIKE 'DEPURACION%'
            ),
            t.codaux4 = CASE WHEN t.codaux4 IS NULL THEN c_N7 ELSE t.codaux4 || c_SEP || c_N7 END,
            t.codaux5 = SUBSTR(CASE WHEN t.codaux5 IS NULL THEN d_N7 ELSE t.codaux5 || c_SEP || d_N7 END, 1, 50)
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
        AND t.num_fotocheck IS NOT NULL
        AND t.entrada IS NOT NULL
        AND t.salida IS NOT NULL
        AND t.salida > t.entrada                                -- Salida ya correcta (dia siguiente)
        AND t.entrada_fijada IS NOT NULL
        -- Turno vespertino: horario inicia entre 17:00 y 22:00
        AND TO_CHAR(t.entrada_fijada, 'HH24MI') BETWEEN '1700' AND '2200'
        -- Entrada actual es MAS DE 2h despues del inicio del turno (mal asignada)
        AND t.entrada > t.entrada_fijada + (2/24)
        AND NVL(t.descanso, 'N') <> 'S'
        AND NVL(t.ind_cerrado, 'N') <> 'S'
        AND EXISTS (
            SELECT 1 FROM SCA_HISTORIAL h
            WHERE h.idtarjeta = t.num_fotocheck
            AND h.fec_equiv = t.fechamar
            AND RTRIM(h.hora) >= TO_CHAR(t.entrada_fijada - (2/24), 'HH24:MI:SS')
            AND RTRIM(h.hora) <= TO_CHAR(t.entrada_fijada + (2/24), 'HH24:MI:SS')
            AND RTRIM(h.hora) < TO_CHAR(t.entrada, 'HH24:MI:SS')
            AND NVL(h.ind_anulado, 'N') <> 'S'
            AND NVL(h.ind_noprocesar, 0) = 0
            AND NVL(h.motivo, ' ') NOT LIKE 'DEPURACION%'
        );
        
        IF SQL%ROWCOUNT > 0 THEN
            DBMS_OUTPUT.PUT_LINE('PASO 0B3d: Entrada corregida a marca temprana -> ' || SQL%ROWCOUNT || ' registros');
        END IF;
        v_count_nocturno := v_count_nocturno + SQL%ROWCOUNT;
        
        -- =====================================================================
        -- PASO 0B4: Limpiar ENTRADA del dia siguiente que fue movida por PASO 0B3
        -- Espejo de PASO 0B pero para el dia siguiente
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t_sig
        SET t_sig.entrada = NULL,
            t_sig.nummarcaciones = t_sig.nummarcaciones - 1,
            t_sig.codaux4 = CASE WHEN t_sig.codaux4 IS NULL THEN c_N1 ELSE t_sig.codaux4 || c_SEP || c_N1 END,
            t_sig.codaux5 = SUBSTR(CASE WHEN t_sig.codaux5 IS NULL THEN d_N1_hoy ELSE t_sig.codaux5 || c_SEP || d_N1_hoy END, 1, 50)
        WHERE t_sig.fechamar = v_fecha_proceso + 1
        AND t_sig.cod_empresa LIKE v_empresa_filtro
        AND t_sig.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t_sig.ind_obrero = 'S')
        AND t_sig.entrada IS NOT NULL
        AND TO_CHAR(t_sig.entrada, 'HH24MI') < '0800'
        AND NVL(t_sig.ind_cerrado, 'N') <> 'S'
        AND EXISTS (
            SELECT 1 FROM SCA_ASISTENCIA_TAREO t_hoy
            WHERE t_hoy.fechamar = t_sig.fechamar - 1
            AND t_hoy.cod_empresa = t_sig.cod_empresa
            AND t_hoy.cod_personal = t_sig.cod_personal
            AND t_hoy.entrada IS NOT NULL
            AND TO_CHAR(t_hoy.entrada, 'HH24MI') >= '1800'
            AND t_hoy.salida = t_sig.entrada                  -- La salida de hoy es mi entrada
        );
        
        v_count_nocturno := v_count_nocturno + SQL%ROWCOUNT;
        
        -- =====================================================================
        -- PASO 0B5: N4 para dia siguiente - Reubicar SALIDA vespertina como ENTRADA
        -- Espejo de PASO 0B2 pero para el dia siguiente
        -- Cuando: PASO 0B4 limpio la ENTRADA del dia siguiente
        --         Queda con ENTRADA=NULL, SALIDA vespertina (>=15:00)
        -- Ejemplo: 30/03 despues de 0B4: ENTRADA=NULL, SALIDA=18:50
        --       -> 30/03: ENTRADA=18:50, SALIDA=NULL
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t_sig
        SET t_sig.entrada = t_sig.salida,
            t_sig.salida = NULL,
            t_sig.codaux4 = CASE WHEN t_sig.codaux4 IS NULL THEN c_N4 ELSE t_sig.codaux4 || c_SEP || c_N4 END,
            t_sig.codaux5 = SUBSTR(CASE WHEN t_sig.codaux5 IS NULL THEN d_N4 ELSE t_sig.codaux5 || c_SEP || d_N4 END, 1, 50)
        WHERE t_sig.fechamar = v_fecha_proceso + 1
        AND t_sig.cod_empresa LIKE v_empresa_filtro
        AND t_sig.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t_sig.ind_obrero = 'S')
        AND t_sig.entrada IS NULL
        AND t_sig.salida IS NOT NULL
        AND TO_CHAR(t_sig.salida, 'HH24MI') >= '1500'
        AND t_sig.codaux4 LIKE '%' || c_N1 || '%'
        AND NVL(t_sig.ind_cerrado, 'N') <> 'S';
        
        v_count_nocturno := v_count_nocturno + SQL%ROWCOUNT;
        
        -- =====================================================================
        -- PASO 0C: SALIDA NOCTURNA MAL UBICADA - Mover a ENTRADA + completar SALIDA
        -- Cuando: El registro tiene SALIDA en horario nocturno (>=20:00) pero NO tiene ENTRADA
        -- Esto ocurre cuando el sistema pone la marca nocturna en SALIDA por error
        -- Accion: Mover SALIDA a ENTRADA
        --         SALIDA = salida_fijada del mismo dia, o del dia siguiente si es descanso
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t
        SET t.entrada = t.salida,
            t.salida = NVL(t.salida_fijada,
                (SELECT t_sig.salida_fijada
                 FROM SCA_ASISTENCIA_TAREO t_sig
                 WHERE t_sig.fechamar = t.fechamar + 1
                 AND t_sig.cod_empresa = t.cod_empresa
                 AND t_sig.cod_personal = t.cod_personal)),
            t.nummarcaciones = CASE
                WHEN NVL(t.salida_fijada,
                    (SELECT t_sig.salida_fijada
                     FROM SCA_ASISTENCIA_TAREO t_sig
                     WHERE t_sig.fechamar = t.fechamar + 1
                     AND t_sig.cod_empresa = t.cod_empresa
                     AND t_sig.cod_personal = t.cod_personal)) IS NOT NULL
                THEN t.nummarcaciones + 1
                ELSE t.nummarcaciones END,
            t.codaux4 = CASE WHEN t.codaux4 IS NULL THEN c_N2 ELSE t.codaux4 || c_SEP || c_N2 END,
            t.codaux5 = SUBSTR(CASE WHEN t.codaux5 IS NULL THEN d_N2 ELSE t.codaux5 || c_SEP || d_N2 END, 1, 50)
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
        AND t.entrada IS NULL                                 -- No tiene entrada
        AND t.salida IS NOT NULL                              -- Pero tiene salida
        AND TO_CHAR(t.salida, 'HH24MI') >= '2000'             -- Salida es nocturna (>=20:00)
        AND NVL(t.ind_cerrado, 'N') <> 'S';
        
        v_count_nocturno := v_count_nocturno + SQL%ROWCOUNT;
        
        -- Insertar marca de SALIDA teorica en SCA_HISTORIAL (Case 0C)
        IF SQL%ROWCOUNT > 0 THEN
            INSERT INTO SCA_HISTORIAL (idCod, idLectora, idTarjeta, fecha, hora, tiporeg, fecreg, fec_equiv, motivo, ind_aman_hor_est)
            SELECT id_cod_seq.NEXTVAL, '005', t.num_fotocheck,
                   TO_CHAR(t.fechamar, 'DD/MM/YYYY'),  -- FIX: usar fechamar
                   TO_CHAR(t.salida, 'HH24:MI:SS'),
                   '3', SYSDATE, t.fechamar,
                   'DEPURACION: Salida noct teorica', 'A'
            FROM SCA_ASISTENCIA_TAREO t
            WHERE t.fechamar = v_fecha_proceso
            AND t.cod_empresa LIKE v_empresa_filtro
            AND t.cod_personal LIKE v_personal_filtro
            AND t.salida IS NOT NULL
            AND t.codaux4 LIKE '%' || c_N2 || '%'
            AND t.num_fotocheck IS NOT NULL
            AND NOT EXISTS (
                SELECT 1 FROM SCA_HISTORIAL h
                WHERE h.idtarjeta = t.num_fotocheck
                AND h.fec_equiv = t.fechamar
                AND RTRIM(h.hora) = TO_CHAR(t.salida, 'HH24:MI:SS')
            );
            v_count_historial := v_count_historial + SQL%ROWCOUNT;
        END IF;
        
        -- =====================================================================
        -- PASO 0D: TURNO NOCTURNO - Marca de manana puesta en ENTRADA
        -- Cuando: Horario nocturno (entrada_fijada >= 20:00)
        --         ENTRADA tiene una marca de manana (<12:00) y SALIDA es NULL
        --         Solo 1 marcacion real
        -- Diagnostico: La marca de manana es realmente la SALIDA del turno nocturno,
        --              pero el sistema la puso en ENTRADA por error.
        --         Ejemplo: Horario 23:00->07:00, unica marca a las 07:16
        --                  El 07:16 es la SALIDA, no la ENTRADA
        -- Accion: Mover ENTRADA a SALIDA, poner ENTRADA = entrada_fijada
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t
        SET t.salida = t.entrada,              -- La marca real va a SALIDA
            t.entrada = t.entrada_fijada,      -- ENTRADA = hora teorica nocturna
            t.codaux4 = CASE WHEN t.codaux4 IS NULL THEN c_N3 ELSE t.codaux4 || c_SEP || c_N3 END,
            t.codaux5 = SUBSTR(CASE WHEN t.codaux5 IS NULL THEN d_N3 ELSE t.codaux5 || c_SEP || d_N3 END, 1, 50)
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
        AND t.entrada IS NOT NULL
        AND t.salida IS NULL                                  -- No tiene salida
        AND t.entrada_fijada IS NOT NULL
        AND TO_CHAR(t.entrada_fijada, 'HH24MI') >= '2000'    -- Horario nocturno (>=20:00)
        AND TO_CHAR(t.entrada, 'HH24MI') < '1200'            -- Marca esta en la manana
        AND t.nummarcaciones <= 1                             -- Solo 1 marca real
        AND NVL(t.descanso, 'N') <> 'S'
        AND NVL(t.ind_cerrado, 'N') <> 'S';
        
        v_count_nocturno := v_count_nocturno + SQL%ROWCOUNT;
        
        -- Insertar marca de ENTRADA nocturna en SCA_HISTORIAL (Case 0D)
        IF SQL%ROWCOUNT > 0 THEN
            INSERT INTO SCA_HISTORIAL (idCod, idLectora, idTarjeta, fecha, hora, tiporeg, fecreg, fec_equiv, motivo, ind_aman_hor_est)
            SELECT id_cod_seq.NEXTVAL, '005', t.num_fotocheck,
                   TO_CHAR(t.fechamar, 'DD/MM/YYYY'),  -- FIX: usar fechamar
                   TO_CHAR(t.entrada, 'HH24:MI:SS'),
                   '3', SYSDATE, t.fechamar,
                   'DEPURACION: Entrada nocturna (0D)', 'A'
            FROM SCA_ASISTENCIA_TAREO t
            WHERE t.fechamar = v_fecha_proceso
            AND t.cod_empresa LIKE v_empresa_filtro
            AND t.cod_personal LIKE v_personal_filtro
            AND t.entrada = t.entrada_fijada
            AND t.codaux4 LIKE '%' || c_N3 || '%'
            AND t.num_fotocheck IS NOT NULL
            AND NOT EXISTS (
                SELECT 1 FROM SCA_HISTORIAL h
                WHERE h.idtarjeta = t.num_fotocheck
                AND h.fec_equiv = t.fechamar
                AND RTRIM(h.hora) = TO_CHAR(t.entrada, 'HH24:MI:SS')
            );
            v_count_historial := v_count_historial + SQL%ROWCOUNT;
        END IF;
        
        -- =====================================================================
        -- PASO 1: Completar ENTRADA donde falta pero tiene SALIDA
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t
        SET t.entrada = t.entrada_fijada,
            t.nummarcaciones = t.nummarcaciones + 1,
            t.codaux4 = CASE WHEN t.codaux4 IS NULL THEN c_E1 ELSE t.codaux4 || c_SEP || c_E1 END,
            t.codaux5 = SUBSTR(CASE WHEN t.codaux5 IS NULL THEN d_E1 ELSE t.codaux5 || c_SEP || d_E1 END, 1, 50)
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
        AND t.entrada IS NULL
        AND t.salida IS NOT NULL
        AND t.entrada_fijada IS NOT NULL
        AND NVL(t.descanso, 'N') <> 'S'
        AND NVL(t.ind_cerrado, 'N') <> 'S'
        -- Excluir empleados con permisos/ausencias activos
        AND t.per_desc_med IS NULL
        AND t.per_subsidio IS NULL
        AND t.per_goce IS NULL
        AND t.per_sgoce IS NULL
        AND t.per_vaca IS NULL
        AND t.per_suspension IS NULL
        AND t.per_lic_pat IS NULL
        AND t.per_lic_fac IS NULL;
        
        v_count_entrada := SQL%ROWCOUNT;
        
        -- Insertar marca de ENTRADA en SCA_HISTORIAL
        IF v_count_entrada > 0 THEN
            INSERT INTO SCA_HISTORIAL (idCod, idLectora, idTarjeta, fecha, hora, tiporeg, fecreg, fec_equiv, motivo, ind_aman_hor_est)
            SELECT id_cod_seq.NEXTVAL, '005', t.num_fotocheck,
                   TO_CHAR(t.fechamar, 'DD/MM/YYYY'),  -- FIX: usar fechamar
                   TO_CHAR(t.entrada, 'HH24:MI:SS'),
                   '3', SYSDATE, t.fechamar,
                   'DEPURACION: Entrada completada', 'A'
            FROM SCA_ASISTENCIA_TAREO t
            WHERE t.fechamar = v_fecha_proceso
            AND t.cod_empresa LIKE v_empresa_filtro
            AND t.cod_personal LIKE v_personal_filtro
            AND t.entrada = t.entrada_fijada
            AND t.num_fotocheck IS NOT NULL
            AND NOT EXISTS (
                SELECT 1 FROM SCA_HISTORIAL h
                WHERE h.idtarjeta = t.num_fotocheck
                AND h.fec_equiv = t.fechamar
                AND RTRIM(h.hora) = TO_CHAR(t.entrada, 'HH24:MI:SS')
            );
            v_count_historial := v_count_historial + SQL%ROWCOUNT;
        END IF;
        
        -- =====================================================================
        -- PASO 1B: Ajustar ENTRADA ANTICIPADA (CASO 3)
        -- Si llego MAS DE 15 MIN antes de hora, ajustar a ENTRADA_FIJADA - 15 min
        -- Ejemplo: Horario 19:00, llego 18:23 -> Se pone 18:45 (19:00 - 15min)
        -- 
        -- HORAANTESENTRADA = Tiempo que llego antes (para registro)
        -- HORAEXTANTES = HE por entrada anticipada (solo si >= 1 hora antes)
        -- Se trunca a horas completas (no se pagan minutos)
        -- HORAEXTRA y TOTALHORASEXTRAS se recalculan en PASO 5B
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t
        SET 
            -- Guardar cuanto llego antes (para auditoría)
            t.horaantesentrada = TO_DATE('01/01/1900', 'dd/MM/yyyy') + 
                                 (t.entrada_fijada - t.entrada),
            -- HE antes: solo si llego 1+ hora antes, truncar a horas completas
            t.horaextantes = CASE 
                WHEN (t.entrada_fijada - t.entrada) * 24 >= 1
                THEN TO_DATE('01/01/1900', 'dd/MM/yyyy') + 
                     TRUNC((t.entrada_fijada - t.entrada) * 24) / 24
                ELSE TO_DATE('01/01/1900 00:00', 'dd/MM/yyyy HH24:MI')
            END,
            t.horaextantesofi = CASE 
                WHEN (t.entrada_fijada - t.entrada) * 24 >= 1
                THEN TO_DATE('01/01/1900', 'dd/MM/yyyy') + 
                     TRUNC((t.entrada_fijada - t.entrada) * 24) / 24
                ELSE TO_DATE('01/01/1900 00:00', 'dd/MM/yyyy HH24:MI')
            END,
            -- Ajustar entrada a 15 min antes de la hora teorica
            t.entrada = t.entrada_fijada - (15/1440),
            t.codaux4 = CASE WHEN t.codaux4 IS NULL THEN c_E2 ELSE t.codaux4 || c_SEP || c_E2 END,
            t.codaux5 = SUBSTR(CASE WHEN t.codaux5 IS NULL THEN d_E2 ELSE t.codaux5 || c_SEP || d_E2 END, 1, 50)
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
        AND t.entrada IS NOT NULL
        AND t.entrada_fijada IS NOT NULL
        AND t.entrada < t.entrada_fijada - (15/1440)  -- Llego mas de 15 min antes
        AND NVL(t.descanso, 'N') <> 'S'
        AND NVL(t.ind_cerrado, 'N') <> 'S'
        -- Solo ajustar si NO tiene horas extras antes autorizadas
        AND NVL(t.hayhea_poraut, 'N') <> 'S'
        -- FIX 13/04/2026: EXCLUIR 3er turno con entrada MUY anticipada (>= 2 horas)
        -- Estos casos tienen sobretiempo real y seran manejados por PASO 5G
        AND NOT (
            TO_CHAR(t.entrada_fijada, 'HH24MI') >= '2200' 
            AND t.entrada < t.entrada_fijada - (2/24)
        );
        
        v_count_anticipada := SQL%ROWCOUNT;
        
        -- =====================================================================
        -- PASO 1B-HIS: Actualizar marca de entrada anticipada en SCA_HISTORIAL
        -- FIX 15/04/2026: Cuando PASO 1B ajusta entrada (ej: 14:13 -> 14:45),
        -- la marca original (14:13) queda en SCA_HISTORIAL. PASO 8-PRE luego
        -- inserta la nueva (14:45), creando 2 marcas de entrada = marcacion impar.
        -- Fix: Actualizar la hora de la marca vieja al nuevo valor ajustado.
        -- Calculo hora original: entrada_fijada - (horaantesentrada - 01/01/1900)
        -- Ejemplo: entrada_fijada=15:00, horaantesentrada=00:47 -> 15:00-00:47 = 14:13
        --
        -- TABLAS ESCRITURA: SCA_HISTORIAL (UPDATE hora)
        -- TABLAS CONSULTA:  SCA_ASISTENCIA_TAREO
        -- =====================================================================
        IF v_count_anticipada > 0 THEN
            UPDATE SCA_HISTORIAL h
            SET h.hora = (
                SELECT TO_CHAR(t.entrada, 'HH24:MI:SS')
                FROM SCA_ASISTENCIA_TAREO t
                WHERE t.num_fotocheck = h.idtarjeta
                AND t.fechamar = h.fec_equiv
                AND t.fechamar = v_fecha_proceso
                AND t.cod_empresa LIKE v_empresa_filtro
                AND t.cod_personal LIKE v_personal_filtro
                AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
                AND t.codaux4 LIKE '%' || c_E2 || '%'
            )
            WHERE h.fec_equiv = v_fecha_proceso
            AND NVL(h.ind_anulado, 'N') <> 'S'
            AND EXISTS (
                SELECT 1 FROM SCA_ASISTENCIA_TAREO t
                WHERE t.num_fotocheck = h.idtarjeta
                AND t.fechamar = h.fec_equiv
                AND t.cod_empresa LIKE v_empresa_filtro
                AND t.cod_personal LIKE v_personal_filtro
                AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
                AND t.codaux4 LIKE '%' || c_E2 || '%'
                -- Match: la marca SCA_HIS esta en la hora de entrada ORIGINAL (antes del ajuste)
                -- hora_original = entrada_fijada - (horaantesentrada - 01/01/1900)
                AND RTRIM(h.hora) = TO_CHAR(
                    t.entrada_fijada - (t.horaantesentrada - TO_DATE('01/01/1900', 'dd/MM/yyyy')),
                    'HH24:MI:SS'
                )
            );
            
            IF SQL%ROWCOUNT > 0 THEN
                DBMS_OUTPUT.PUT_LINE('PASO 1B-HIS: Actualizadas ' || SQL%ROWCOUNT || ' marcas de entrada anticipada en SCA_HISTORIAL');
            END IF;
        END IF;
        
        -- =====================================================================
        -- PASO 1C-NOC: Marca duplicada en TURNO NOCTURNO
        -- Cuando: ENTRADA = SALIDA (marca duplicada) Y el horario es nocturno
        --         (entrada_fijada >= 20:00) Y la marca esta en la manana (<12:00)
        -- Diagnostico: La marca real es la SALIDA (salida de madrugada/manana),
        --              no la ENTRADA. El sistema la duplico por error.
        --         Ejemplo: Horario 23:00->07:00, marca a las 07:16
        --                  El 07:16 es la SALIDA, no la ENTRADA
        -- Accion: ENTRADA = entrada_fijada (hora teorica nocturna)
        --         SALIDA = se mantiene la marca real (hora de salida verdadera)
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t
        SET t.entrada = t.entrada_fijada,
            t.codaux4 = CASE WHEN t.codaux4 IS NULL THEN c_E4 ELSE t.codaux4 || c_SEP || c_E4 END,
            t.codaux5 = SUBSTR(CASE WHEN t.codaux5 IS NULL THEN d_E4 ELSE t.codaux5 || c_SEP || d_E4 END, 1, 50)
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
        AND t.entrada IS NOT NULL
        AND t.salida IS NOT NULL
        AND t.entrada = t.salida                              -- Marca duplicada
        AND t.nummarcaciones <= 2                             -- Solo 1-2 marcas reales
        AND t.entrada_fijada IS NOT NULL
        AND TO_CHAR(t.entrada_fijada, 'HH24MI') >= '2000'    -- Horario nocturno (>=20:00)
        AND TO_CHAR(t.entrada, 'HH24MI') < '1200'            -- Marca esta en la manana
        AND NVL(t.descanso, 'N') <> 'S'
        AND NVL(t.ind_cerrado, 'N') <> 'S';
        
        v_count_nocturno := v_count_nocturno + SQL%ROWCOUNT;
        
        -- Insertar marca de ENTRADA nocturna en SCA_HISTORIAL (Case 1C-NOC)
        IF SQL%ROWCOUNT > 0 THEN
            INSERT INTO SCA_HISTORIAL (idCod, idLectora, idTarjeta, fecha, hora, tiporeg, fecreg, fec_equiv, motivo, ind_aman_hor_est)
            SELECT id_cod_seq.NEXTVAL, '005', t.num_fotocheck,
                   TO_CHAR(t.fechamar, 'DD/MM/YYYY'),  -- FIX: usar fechamar
                   TO_CHAR(t.entrada, 'HH24:MI:SS'),
                   '3', SYSDATE, t.fechamar,
                   'DEPURACION: Entrada nocturna', 'A'
            FROM SCA_ASISTENCIA_TAREO t
            WHERE t.fechamar = v_fecha_proceso
            AND t.cod_empresa LIKE v_empresa_filtro
            AND t.cod_personal LIKE v_personal_filtro
            AND t.entrada = t.entrada_fijada
            AND t.codaux4 LIKE '%' || c_E4 || '%'
            AND t.num_fotocheck IS NOT NULL
            AND NOT EXISTS (
                SELECT 1 FROM SCA_HISTORIAL h
                WHERE h.idtarjeta = t.num_fotocheck
                AND h.fec_equiv = t.fechamar
                AND RTRIM(h.hora) = TO_CHAR(t.entrada, 'HH24:MI:SS')
            );
            v_count_historial := v_count_historial + SQL%ROWCOUNT;
        END IF;
        
        -- =====================================================================
        -- PASO 1C: Corregir ENTRADA = SALIDA (marca duplicada por error)
        -- Cuando la misma marca se copio en ambos campos, corregir SALIDA
        -- NOTA: Turnos nocturnos ya fueron tratados por PASO 1C-NOC arriba
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t
        SET t.salida = t.salida_fijada,
            t.codaux4 = CASE WHEN t.codaux4 IS NULL THEN c_E3 ELSE t.codaux4 || c_SEP || c_E3 END,
            t.codaux5 = SUBSTR(CASE WHEN t.codaux5 IS NULL THEN d_E3 ELSE t.codaux5 || c_SEP || d_E3 END, 1, 50)
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
        AND t.entrada IS NOT NULL
        AND t.salida IS NOT NULL
        AND t.entrada = t.salida                    -- Misma hora exacta = error
        AND t.nummarcaciones <= 2                   -- Solo 1-2 marcas reales
        AND t.salida_fijada IS NOT NULL
        AND t.salida <> t.salida_fijada             -- No es ya la hora teorica
        AND NVL(t.descanso, 'N') <> 'S'
        AND NVL(t.ind_cerrado, 'N') <> 'S';
        
        v_count_salida := v_count_salida + SQL%ROWCOUNT;
        
        -- Insertar marca de SALIDA corregida en SCA_HISTORIAL (Case 1C)
        IF SQL%ROWCOUNT > 0 THEN
            INSERT INTO SCA_HISTORIAL (idCod, idLectora, idTarjeta, fecha, hora, tiporeg, fecreg, fec_equiv, motivo, ind_aman_hor_est)
            SELECT id_cod_seq.NEXTVAL, '005', t.num_fotocheck,
                   TO_CHAR(t.fechamar, 'DD/MM/YYYY'),  -- FIX: usar fechamar
                   TO_CHAR(t.salida, 'HH24:MI:SS'),
                   '3', SYSDATE, t.fechamar,
                   'DEPURACION: Salida corregida', 'A'
            FROM SCA_ASISTENCIA_TAREO t
            WHERE t.fechamar = v_fecha_proceso
            AND t.cod_empresa LIKE v_empresa_filtro
            AND t.cod_personal LIKE v_personal_filtro
            AND t.salida = t.salida_fijada
            AND t.codaux4 LIKE '%' || c_E3 || '%'
            AND t.num_fotocheck IS NOT NULL
            AND NOT EXISTS (
                SELECT 1 FROM SCA_HISTORIAL h
                WHERE h.idtarjeta = t.num_fotocheck
                AND h.fec_equiv = t.fechamar
                AND RTRIM(h.hora) = TO_CHAR(t.salida, 'HH24:MI:SS')
            );
            v_count_historial := v_count_historial + SQL%ROWCOUNT;
        END IF;
        
        -- =====================================================================
        -- PASO 2: Completar SALIDA donde falta pero tiene ENTRADA
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t
        SET t.salida = t.salida_fijada,
            t.nummarcaciones = t.nummarcaciones + 1,
            t.codaux4 = CASE WHEN t.codaux4 IS NULL THEN c_S1 ELSE t.codaux4 || c_SEP || c_S1 END,
            t.codaux5 = SUBSTR(CASE WHEN t.codaux5 IS NULL THEN d_S1 ELSE t.codaux5 || c_SEP || d_S1 END, 1, 50)
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
        AND t.salida IS NULL
        AND t.entrada IS NOT NULL
        AND t.salida_fijada IS NOT NULL
        AND NVL(t.descanso, 'N') <> 'S'
        AND NVL(t.ind_cerrado, 'N') <> 'S'
        -- Excluir empleados con permisos/ausencias activos
        AND t.per_desc_med IS NULL
        AND t.per_subsidio IS NULL
        AND t.per_goce IS NULL
        AND t.per_sgoce IS NULL
        AND t.per_vaca IS NULL
        AND t.per_suspension IS NULL
        AND t.per_lic_pat IS NULL
        AND t.per_lic_fac IS NULL;
        
        v_count_salida := SQL%ROWCOUNT;
        
        -- Insertar marca de SALIDA en SCA_HISTORIAL (Case 2)
        IF v_count_salida > 0 THEN
            INSERT INTO SCA_HISTORIAL (idCod, idLectora, idTarjeta, fecha, hora, tiporeg, fecreg, fec_equiv, motivo, ind_aman_hor_est)
            SELECT id_cod_seq.NEXTVAL, '005', t.num_fotocheck,
                   TO_CHAR(t.fechamar, 'DD/MM/YYYY'),  -- FIX: usar fechamar
                   TO_CHAR(t.salida, 'HH24:MI:SS'),
                   '3', SYSDATE, t.fechamar,
                   'DEPURACION: Salida completada', 'A'
            FROM SCA_ASISTENCIA_TAREO t
            WHERE t.fechamar = v_fecha_proceso
            AND t.cod_empresa LIKE v_empresa_filtro
            AND t.cod_personal LIKE v_personal_filtro
            AND t.salida = t.salida_fijada
            AND t.codaux4 LIKE '%' || c_S1 || '%'
            AND t.num_fotocheck IS NOT NULL
            AND NOT EXISTS (
                SELECT 1 FROM SCA_HISTORIAL h
                WHERE h.idtarjeta = t.num_fotocheck
                AND h.fec_equiv = t.fechamar
                AND RTRIM(h.hora) = TO_CHAR(t.salida, 'HH24:MI:SS')
            );
            v_count_historial := v_count_historial + SQL%ROWCOUNT;
        END IF;
        
        -- =====================================================================
        -- PASO 2A: Validar refrigerio asignado por tareo
        -- El tareo con 6+ marcas puede asignar mal el refrigerio.
        -- Ejemplo: 6 marcas (06:57, 13:30, 13:31, 14:10, 14:11, 15:50)
        --   Tareo asigna inirefri=13:30, finrefri=13:31 -> 1 minuto!
        --   Pero real es 13:30 -> 14:10 = 40 min (cerca de 45 min teorico)
        -- Detectar: duracion refrigerio < 50% del teorico
        -- Y existen marcas intermedias alternativas en SCA_HISTORIAL
        -- Accion: limpiar inirefri/finrefri para que PASO 2B-PRE reasigne
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t
        SET t.inirefri = NULL,
            t.finrefri = NULL,
            t.codaux4 = CASE WHEN t.codaux4 IS NULL THEN c_R6 ELSE t.codaux4 || c_SEP || c_R6 END,
            t.codaux5 = SUBSTR(CASE WHEN t.codaux5 IS NULL THEN d_R6 ELSE t.codaux5 || c_SEP || d_R6 END, 1, 50)
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
        -- Tiene refrigerio asignado
        AND t.inirefri IS NOT NULL
        AND t.finrefri IS NOT NULL
        -- Tiene horario teorico de referencia
        AND t.horiniref IS NOT NULL
        AND t.horfinref IS NOT NULL
        AND t.horiniref <> TO_DATE('01/01/1900', 'dd/MM/yyyy')
        -- Duracion actual < 50% del teorico (anomala)
        AND (t.finrefri - t.inirefri) < (t.horfinref - t.horiniref) * 0.5
        -- Entrada y salida deben existir
        AND t.entrada IS NOT NULL
        AND t.salida IS NOT NULL
        AND t.num_fotocheck IS NOT NULL
        -- Existen marcas intermedias alternativas en SCA_HISTORIAL
        -- (diferentes a las ya asignadas como inirefri/finrefri)
        AND EXISTS (
            SELECT 1 FROM SCA_HISTORIAL h
            WHERE h.idtarjeta = t.num_fotocheck
            AND h.fec_equiv = t.fechamar
            AND NVL(h.motivo, ' ') NOT LIKE 'DEPURACION%'
            AND NVL(h.ind_anulado, 'N') <> 'S'
            AND NVL(h.ind_noprocesar, 0) = 0
            AND TO_DATE(TO_CHAR(h.fec_equiv, 'DD/MM/YYYY') || ' ' || RTRIM(h.hora), 'DD/MM/YYYY HH24:MI:SS') > t.entrada
            AND TO_DATE(TO_CHAR(h.fec_equiv, 'DD/MM/YYYY') || ' ' || RTRIM(h.hora), 'DD/MM/YYYY HH24:MI:SS') < t.salida
            AND RTRIM(h.hora) <> TO_CHAR(t.inirefri, 'HH24:MI:SS')
            AND RTRIM(h.hora) <> TO_CHAR(t.finrefri, 'HH24:MI:SS')
        )
        -- FIX 14/04/2026: Removida exclusion nocturna (3er turno SI tiene refrigerio 23:00-23:30)
        -- La guardia t.horiniref IS NOT NULL ya filtra horarios sin refrigerio
        AND NVL(t.descanso, 'N') <> 'S'
        AND NVL(t.ind_cerrado, 'N') <> 'S';
        
        IF SQL%ROWCOUNT > 0 THEN
            DBMS_OUTPUT.PUT_LINE('PASO 2A: Refrigerio anomalo detectado y limpiado -> ' || SQL%ROWCOUNT || ' registros (PASO 2B-PRE reasignara)');
        END IF;
        
        -- =====================================================================
        -- PASO 2B-PRE: Recuperar marcas de refrigerio desde SCA_HISTORIAL
        -- ANTES de aplicar refrigerio teorico (R1), verificar si existen
        -- marcas REALES en SCA_HISTORIAL que el tareo no pudo asignar.
        -- Esto ocurre con marcacion impar (3, 5 marcas) donde el tareo
        -- solo asigna MIN(marca)=ENTRADA y MAX(marca)=SALIDA,
        -- dejando las marcas intermedias (refrigerio) sin asignar.
        --
        -- Ejemplo: 3 marcas reales 06:40, 13:03, 16:00
        --   Tareo puso: entrada=06:40, salida=16:00, inirefri=NULL
        --   La marca 13:03 esta en SCA_HISTORIAL, cercana a horiniref(13:15)
        --   -> IniRefri = 13:03 (REAL)
        --   -> PASO 3B calculara FinRefri = 13:03 + 00:45 = 13:48
        --
        -- Si encuentra 2 marcas intermedias (caso 5 marcas), asigna ambas.
        -- Si no encuentra marcas intermedias validas, PASO 2B (R1) aplica teorico.
        -- =====================================================================
        FOR rec IN (
            SELECT t.ROWID AS rid,
                   t.num_fotocheck, t.entrada, t.salida, t.salida_fijada,
                   t.horiniref, t.horfinref,
                   t.codaux4, t.codaux5
            FROM SCA_ASISTENCIA_TAREO t
            WHERE t.fechamar = v_fecha_proceso
            AND t.cod_empresa LIKE v_empresa_filtro
            AND t.cod_personal LIKE v_personal_filtro
            AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
            AND t.inirefri IS NULL
            AND t.finrefri IS NULL
            AND t.entrada IS NOT NULL
            AND t.salida IS NOT NULL
            AND t.horiniref IS NOT NULL
            AND t.horfinref IS NOT NULL
            AND t.horiniref <> TO_DATE('01/01/1900', 'dd/MM/yyyy')
            -- FIX 14/04/2026: Removida exclusion nocturna (3er turno SI tiene refrigerio)
            AND NVL(t.descanso, 'N') <> 'S'
            AND NVL(t.ind_cerrado, 'N') <> 'S'
            AND t.per_desc_med IS NULL
            AND t.per_subsidio IS NULL
            AND t.per_goce IS NULL
            AND t.per_sgoce IS NULL
            AND t.per_vaca IS NULL
            AND t.per_suspension IS NULL
            AND t.per_lic_pat IS NULL
            AND t.per_lic_fac IS NULL
            AND t.num_fotocheck IS NOT NULL
        ) LOOP
            v_marca_inter := NULL;
            v_idcod_inter := NULL;
            v_marca_fin := NULL;
            
            -- Buscar marca intermedia mas cercana a horiniref
            -- FIX 13/04/2026: Excluir marcas duplicadas de entrada/salida
            --                 Caso ALLCCARIMA: entrada 18:56, marca 18:57 = duplicado de entrada
            --                 (empleado marco 2 veces seguidas al ingresar)
            --                 Estas marcas NO son refrigerio, son duplicados de entrada/salida
            BEGIN
                SELECT idcod, marca INTO v_idcod_inter, v_marca_inter FROM (
                    SELECT h.idcod,
                           TO_DATE(TO_CHAR(h.fec_equiv, 'DD/MM/YYYY') || ' ' || RTRIM(h.hora), 'DD/MM/YYYY HH24:MI:SS') AS marca
                    FROM SCA_HISTORIAL h
                    WHERE h.idtarjeta = rec.num_fotocheck
                    AND h.fec_equiv = v_fecha_proceso
                    -- Solo marcas entre entrada y salida (excluyendo bordes)
                    -- FIX: al menos 30 min despues de entrada (excluir duplicados de entrada)
                    AND TO_DATE(TO_CHAR(h.fec_equiv, 'DD/MM/YYYY') || ' ' || RTRIM(h.hora), 'DD/MM/YYYY HH24:MI:SS') > rec.entrada + (30/1440)
                    -- FIX: al menos 30 min antes de salida (excluir duplicados de salida)
                    -- FIX 20/04/2026: si salida(hora) < horiniref(hora) = estado imposible,
                    -- extender ventana a salida_fijada para encontrar marcas manuales
                    -- que estan DESPUES de la salida erronea (ej: manuales 12:30, 13:00 > salida=12:17)
                    AND TO_DATE(TO_CHAR(h.fec_equiv, 'DD/MM/YYYY') || ' ' || RTRIM(h.hora), 'DD/MM/YYYY HH24:MI:SS') <
                        CASE WHEN TO_CHAR(rec.salida, 'HH24MI') < TO_CHAR(rec.horiniref, 'HH24MI')
                                  AND rec.salida_fijada IS NOT NULL
                             THEN rec.salida_fijada - (30/1440)
                             ELSE rec.salida - (30/1440)
                        END
                    -- Excluir marcas generadas por depuracion previa
                    AND NVL(h.motivo, ' ') NOT LIKE 'DEPURACION%'
                    AND NVL(h.ind_anulado, 'N') <> 'S'
                    AND NVL(h.ind_noprocesar, 0) = 0
                    ORDER BY ABS(TO_DATE(TO_CHAR(h.fec_equiv, 'DD/MM/YYYY') || ' ' || RTRIM(h.hora), 'DD/MM/YYYY HH24:MI:SS') - rec.horiniref)
                ) WHERE ROWNUM = 1;
            EXCEPTION WHEN NO_DATA_FOUND THEN
                v_marca_inter := NULL;
                v_idcod_inter := NULL;
            END;
            
            -- FIX 23/04/2026 (BUG A): si no encontro marca activa, REINTENTAR
            -- permitiendo marcas anuladas por Aquarius nativo como 'MARCA DUPLICADA'.
            -- Caso real: fotocheck 032666/17-04. Marcas reales 06:48,12:00,15:07.
            -- Aquarius nativo anula 12:00 como 'MARCA DUPLICADA' (heuristica
            -- agresiva: si no esta cerca del horiniref teorico la anula). 
            -- 12:00 es marca biometrica REAL del empleado iniciando refri temprano.
            -- Sin este reintento, PASO 2B aplica refri teorico (12:30/13:00) y la
            -- marca 12:00 se pierde. Con este reintento, se recupera 12:00 como
            -- IniRefri y PASO 3B calcula FinRefri = 12:00 + totref = 12:30.
            -- Solo se aceptan anulaciones del nativo (no DEPURACION del PKG mismo).
            IF v_marca_inter IS NULL THEN
                BEGIN
                    SELECT idcod, marca INTO v_idcod_inter, v_marca_inter FROM (
                        SELECT h.idcod,
                               TO_DATE(TO_CHAR(h.fec_equiv, 'DD/MM/YYYY') || ' ' || RTRIM(h.hora), 'DD/MM/YYYY HH24:MI:SS') AS marca
                        FROM SCA_HISTORIAL h
                        WHERE h.idtarjeta = rec.num_fotocheck
                        AND h.fec_equiv = v_fecha_proceso
                        AND TO_DATE(TO_CHAR(h.fec_equiv, 'DD/MM/YYYY') || ' ' || RTRIM(h.hora), 'DD/MM/YYYY HH24:MI:SS') > rec.entrada + (30/1440)
                        AND TO_DATE(TO_CHAR(h.fec_equiv, 'DD/MM/YYYY') || ' ' || RTRIM(h.hora), 'DD/MM/YYYY HH24:MI:SS') <
                            CASE WHEN TO_CHAR(rec.salida, 'HH24MI') < TO_CHAR(rec.horiniref, 'HH24MI')
                                      AND rec.salida_fijada IS NOT NULL
                                 THEN rec.salida_fijada - (30/1440)
                                 ELSE rec.salida - (30/1440)
                            END
                        AND NVL(h.motivo, ' ') NOT LIKE 'DEPURACION%'
                        -- Aceptar marcas anuladas SOLO con motivo 'MARCA DUPLICADA'
                        -- (anulacion automatica del nativo, recuperable)
                        -- FIX 23/04/2026: nativo Aquarius usa ind_anulado='A' (no 'S').
                        -- Aceptar ambos por defensa.
                        AND NVL(h.ind_anulado, 'N') IN ('S','A')
                        AND UPPER(NVL(h.motivo, ' ')) LIKE '%MARCA DUPLICADA%'
                        AND NVL(h.ind_noprocesar, 0) = 0
                        ORDER BY ABS(  (TO_NUMBER(TO_CHAR(TO_DATE(TO_CHAR(h.fec_equiv, 'DD/MM/YYYY') || ' ' || RTRIM(h.hora), 'DD/MM/YYYY HH24:MI:SS'), 'HH24')) * 60
                                       + TO_NUMBER(TO_CHAR(TO_DATE(TO_CHAR(h.fec_equiv, 'DD/MM/YYYY') || ' ' || RTRIM(h.hora), 'DD/MM/YYYY HH24:MI:SS'), 'MI')))
                                     - (TO_NUMBER(TO_CHAR(rec.horiniref, 'HH24')) * 60 + TO_NUMBER(TO_CHAR(rec.horiniref, 'MI')))
                                  )
                    ) WHERE ROWNUM = 1;
                    
                    -- Si recupero una marca anulada, des-anularla para que sea visible
                    IF v_marca_inter IS NOT NULL AND v_idcod_inter IS NOT NULL THEN
                        UPDATE SCA_HISTORIAL
                        SET ind_anulado = 'N',
                            motivo = 'DEPURACION: Marca recuperada de MARCA DUPLICADA (2B-PRE)'
                        WHERE idcod = v_idcod_inter;
                        DBMS_OUTPUT.PUT_LINE('PASO 2B-PRE BUG-A: Recuperada marca anulada ' || TO_CHAR(v_marca_inter, 'HH24:MI:SS') || ' para fotocheck ' || rec.num_fotocheck);
                    END IF;
                EXCEPTION WHEN NO_DATA_FOUND THEN
                    v_marca_inter := NULL;
                    v_idcod_inter := NULL;
                END;
            END IF;
            
            -- Validar: debe estar dentro de 2 horas del horario de refrigerio
            -- FIX 21/04/2026: v_marca_inter tiene fecha real (2026), rec.horiniref tiene
            -- base 01/01/1900 -> ABS(fecha_real - fecha_1900) = ~46000 dias, siempre > 0.0833
            -- -> la marca valida siempre se descartaba. Comparar solo minutos del dia.
            IF v_marca_inter IS NOT NULL
               AND ABS(  (TO_NUMBER(TO_CHAR(v_marca_inter,  'HH24')) * 60 + TO_NUMBER(TO_CHAR(v_marca_inter,  'MI')))
                       - (TO_NUMBER(TO_CHAR(rec.horiniref, 'HH24')) * 60 + TO_NUMBER(TO_CHAR(rec.horiniref, 'MI')))
                    ) > 120 THEN
                v_marca_inter := NULL;
            END IF;
            
            IF v_marca_inter IS NOT NULL THEN
                -- Buscar segunda marca intermedia cercana a horfinref (diferente de la primera)
                BEGIN
                    SELECT marca INTO v_marca_fin FROM (
                        SELECT TO_DATE(TO_CHAR(h.fec_equiv, 'DD/MM/YYYY') || ' ' || RTRIM(h.hora), 'DD/MM/YYYY HH24:MI:SS') AS marca
                        FROM SCA_HISTORIAL h
                        WHERE h.idtarjeta = rec.num_fotocheck
                        AND h.fec_equiv = v_fecha_proceso
                        -- FIX: al menos 30 min despues de entrada
                        AND TO_DATE(TO_CHAR(h.fec_equiv, 'DD/MM/YYYY') || ' ' || RTRIM(h.hora), 'DD/MM/YYYY HH24:MI:SS') > rec.entrada + (30/1440)
                        -- FIX: al menos 30 min antes de salida
                        -- FIX 20/04/2026: misma extension de ventana que primera busqueda
                        AND TO_DATE(TO_CHAR(h.fec_equiv, 'DD/MM/YYYY') || ' ' || RTRIM(h.hora), 'DD/MM/YYYY HH24:MI:SS') <
                            CASE WHEN TO_CHAR(rec.salida, 'HH24MI') < TO_CHAR(rec.horiniref, 'HH24MI')
                                      AND rec.salida_fijada IS NOT NULL
                                 THEN rec.salida_fijada - (30/1440)
                                 ELSE rec.salida - (30/1440)
                            END
                        AND NVL(h.motivo, ' ') NOT LIKE 'DEPURACION%'
                        AND NVL(h.ind_anulado, 'N') <> 'S'
                        AND NVL(h.ind_noprocesar, 0) = 0
                        AND h.idcod <> v_idcod_inter  -- Excluir la marca ya usada
                        -- FIX 23/04/2026: Excluir near-duplicados de la primera marca (inirefri)
                        -- Caso: 5 marcas (08:51, 12:44, 12:45, 13:18, 18:03) donde 12:44/12:45
                        -- son near-duplicados (1 min). PASO 2B-PRE elegia 12:45 como finrefri
                        -- porque 12:45 (15 min de horfinref=13:00) > 13:18 (18 min) en proximidad.
                        -- Con este fix, 12:45 queda excluido (< 5 min de inirefri=12:44)
                        -- -> se elige 13:18 correctamente. PASO 5F limpia 12:45 como orphan.
                        AND ABS(
                            TO_DATE(TO_CHAR(h.fec_equiv, 'DD/MM/YYYY') || ' ' || RTRIM(h.hora), 'DD/MM/YYYY HH24:MI:SS')
                            - v_marca_inter
                        ) >= (5/1440)  -- Al menos 5 min de separacion con la primera marca
                        ORDER BY ABS(TO_DATE(TO_CHAR(h.fec_equiv, 'DD/MM/YYYY') || ' ' || RTRIM(h.hora), 'DD/MM/YYYY HH24:MI:SS') - rec.horfinref)
                    ) WHERE ROWNUM = 1;
                EXCEPTION WHEN NO_DATA_FOUND THEN
                    v_marca_fin := NULL;
                END;
                
                -- Validar segunda marca
                -- FIX 21/04/2026: mismo fix que v_marca_inter; comparar solo minutos del dia
                IF v_marca_fin IS NOT NULL
                   AND ABS(  (TO_NUMBER(TO_CHAR(v_marca_fin,   'HH24')) * 60 + TO_NUMBER(TO_CHAR(v_marca_fin,   'MI')))
                           - (TO_NUMBER(TO_CHAR(rec.horfinref, 'HH24')) * 60 + TO_NUMBER(TO_CHAR(rec.horfinref, 'MI')))
                        ) > 120 THEN
                    v_marca_fin := NULL;
                END IF;
                
                -- Asignar marcas encontradas
                IF v_marca_fin IS NOT NULL THEN
                    -- Encontro AMBAS marcas de refrigerio reales
                    -- Asegurar orden correcto (ini < fin)
                    IF v_marca_inter <= v_marca_fin THEN
                        UPDATE SCA_ASISTENCIA_TAREO
                        SET inirefri = v_marca_inter,
                            finrefri = v_marca_fin,
                            codaux4 = CASE WHEN codaux4 IS NULL THEN c_R4 || c_SEP || c_R5 ELSE codaux4 || c_SEP || c_R4 || c_SEP || c_R5 END,
                            codaux5 = SUBSTR(CASE WHEN codaux5 IS NULL THEN d_R4 ELSE codaux5 || c_SEP || d_R4 END, 1, 50)
                        WHERE ROWID = rec.rid;
                    ELSE
                        UPDATE SCA_ASISTENCIA_TAREO
                        SET inirefri = v_marca_fin,
                            finrefri = v_marca_inter,
                            codaux4 = CASE WHEN codaux4 IS NULL THEN c_R4 || c_SEP || c_R5 ELSE codaux4 || c_SEP || c_R4 || c_SEP || c_R5 END,
                            codaux5 = SUBSTR(CASE WHEN codaux5 IS NULL THEN d_R4 ELSE codaux5 || c_SEP || d_R4 END, 1, 50)
                        WHERE ROWID = rec.rid;
                    END IF;
                    v_count_inirefri := v_count_inirefri + 1;
                    v_count_finrefri := v_count_finrefri + 1;
                ELSE
                    -- Solo encontro UNA marca intermedia
                    -- Determinar si es IniRefri o FinRefri
                    -- FIX 21/04/2026: v_marca_inter tiene fecha real (2026), horiniref/horfinref
                    -- tienen base 01/01/1900 -> ABS(fecha) siempre favorece al que este
                    -- lejos en FECHA, no en HORA -> siempre asignaba como FinRefri.
                    -- Fix: comparar solo minutos del dia.
                    IF ABS(  (TO_NUMBER(TO_CHAR(v_marca_inter, 'HH24')) * 60 + TO_NUMBER(TO_CHAR(v_marca_inter, 'MI')))
                           - (TO_NUMBER(TO_CHAR(rec.horiniref,  'HH24')) * 60 + TO_NUMBER(TO_CHAR(rec.horiniref,  'MI')))
                        )
                       <=
                       ABS(  (TO_NUMBER(TO_CHAR(v_marca_inter, 'HH24')) * 60 + TO_NUMBER(TO_CHAR(v_marca_inter, 'MI')))
                           - (TO_NUMBER(TO_CHAR(rec.horfinref,  'HH24')) * 60 + TO_NUMBER(TO_CHAR(rec.horfinref,  'MI')))
                        )
                    THEN
                        -- Mas cercana a IniRefri -> asignar como IniRefri
                        -- PASO 3B calculara FinRefri = IniRefri + totref
                        UPDATE SCA_ASISTENCIA_TAREO
                        SET inirefri = v_marca_inter,
                            codaux4 = CASE WHEN codaux4 IS NULL THEN c_R4 ELSE codaux4 || c_SEP || c_R4 END,
                            codaux5 = SUBSTR(CASE WHEN codaux5 IS NULL THEN d_R4 ELSE codaux5 || c_SEP || d_R4 END, 1, 50)
                        WHERE ROWID = rec.rid;
                        v_count_inirefri := v_count_inirefri + 1;
                    ELSE
                        -- Mas cercana a FinRefri -> asignar como FinRefri
                        -- PASO 3A calculara IniRefri = FinRefri - totref
                        UPDATE SCA_ASISTENCIA_TAREO
                        SET finrefri = v_marca_inter,
                            codaux4 = CASE WHEN codaux4 IS NULL THEN c_R5 ELSE codaux4 || c_SEP || c_R5 END,
                            codaux5 = SUBSTR(CASE WHEN codaux5 IS NULL THEN d_R5 ELSE codaux5 || c_SEP || d_R5 END, 1, 50)
                        WHERE ROWID = rec.rid;
                        v_count_finrefri := v_count_finrefri + 1;
                    END IF;
                END IF;
            END IF;
        END LOOP;
        
        -- =====================================================================
        -- PASO 2B: Limpiar INIREFRI/FINREFRI incorrectos y completar con teorico
        -- SOLO cuando AMBOS son NULL o tienen valores erroneos (ej: 08:46)
        -- NO tocar si INIREFRI tiene un valor real (diferente al teorico)
        -- INCLUYE turnos nocturnos si el horario define refrigerio (horiniref IS NOT NULL)
        -- NOTA: Solo aplica si PASO 2B-PRE no encontro marcas en SCA_HISTORIAL
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t
        SET t.inirefri = t.horiniref,
            t.finrefri = t.horfinref,
            t.codaux4 = CASE WHEN t.codaux4 IS NULL THEN c_R1 ELSE t.codaux4 || c_SEP || c_R1 END,
            t.codaux5 = SUBSTR(CASE WHEN t.codaux5 IS NULL THEN d_R1 ELSE t.codaux5 || c_SEP || d_R1 END, 1, 50)
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
        -- Quitado: AND t.nummarcaciones <= 2 (puede tener mas marcas y aun no tener refrigerio)
        AND t.horiniref IS NOT NULL                 -- Tiene refrigerio programado
        AND t.horfinref IS NOT NULL
        AND t.horiniref <> TO_DATE('01/01/1900', 'dd/MM/yyyy')
        -- FIX 15/04/2026: Excluir horarios con refri=00:00 (ej: VIGILANCIA 8h sin refrigerio)
        -- horiniref puede tener fecha distinta a 01/01/1900 pero hora 00:00 = no refri
        AND TO_CHAR(t.horiniref, 'HH24:MI') <> '00:00'
        -- SOLO si ambos son NULL (no tiene marcas de refrigerio)
        AND t.inirefri IS NULL 
        AND t.finrefri IS NULL
        AND t.entrada IS NOT NULL
        AND t.salida IS NOT NULL
        -- FIX 14/04/2026: Removida exclusion nocturna (3er turno SI tiene refrigerio 23:00-23:30)
        -- La guardia t.horiniref IS NOT NULL ya filtra horarios sin refrigerio
        -- FIX 23/04/2026: NO asignar refri teorico si salida es ANTES o DURANTE el fin del
        -- refrigerio teorico. Caso: empleado salio temprano (E=06:52, S=13:24) pero el refri
        -- teorico es 13:15-14:00 -> S(13:24) < FR(14:00) = imposible, no tomo refri.
        -- Comparacion por hora del dia (no DATE) porque salida=fechamar y horfinref=01/01/1900.
        -- Excluir nocturnos: salida(07:00) puede ser cronologicamente despues de horfinref(02:30)
        -- aunque numericamente menor. Detectar nocturno por salida_fijada < entrada_fijada.
        AND (
            TO_CHAR(t.salida, 'HH24:MI') > TO_CHAR(t.horfinref, 'HH24:MI')
            OR (t.entrada_fijada IS NOT NULL AND t.salida_fijada IS NOT NULL
                AND t.salida_fijada < t.entrada_fijada)
        )
        -- FIX 23/04/2026: NO asignar refri teorico si entrada es POSTERIOR a horiniref
        -- (empleado llego tarde, no pudo tomar refri en su horario teorico).
        -- Caso: 034686 31/03/2026 entrada=13:41, horiniref=13:15. Aplicar R1
        -- generaria IR(13:15)<entrada(13:41) -> PASO 3E lo borraria igual.
        -- Mejor no intentar: el dia queda SIN refri y SIN tag (codaux4=NULL),
        -- el sistema .NET lo marcara como pendiente para analisis manual.
        -- Excluir nocturnos (cruza dia, comparacion HH24MI no aplica).
        AND (
            (t.entrada_fijada IS NOT NULL AND t.salida_fijada IS NOT NULL
             AND t.salida_fijada < t.entrada_fijada)  -- nocturno: no aplicar guarda
            OR TO_CHAR(t.entrada, 'HH24MI') <= TO_CHAR(t.horiniref, 'HH24MI')
        )
        AND NVL(t.descanso, 'N') <> 'S'
        AND NVL(t.ind_cerrado, 'N') <> 'S'
        -- Excluir empleados con permisos/ausencias activos
        AND t.per_desc_med IS NULL
        AND t.per_subsidio IS NULL
        AND t.per_goce IS NULL
        AND t.per_sgoce IS NULL
        AND t.per_vaca IS NULL
        AND t.per_suspension IS NULL
        AND t.per_lic_pat IS NULL
        AND t.per_lic_fac IS NULL;
        
        v_count_inirefri := v_count_inirefri + SQL%ROWCOUNT;
        v_count_finrefri := v_count_finrefri + SQL%ROWCOUNT;
        
        -- Insertar marcas de INIREFRI teorico en SCA_HISTORIAL (R1)
        IF SQL%ROWCOUNT > 0 THEN
            INSERT INTO SCA_HISTORIAL (idCod, idLectora, idTarjeta, fecha, hora, tiporeg, fecreg, fec_equiv, motivo, ind_aman_hor_est)
            SELECT id_cod_seq.NEXTVAL, '005', t.num_fotocheck,
                   TO_CHAR(t.fechamar, 'DD/MM/YYYY'),  -- FIX: usar fechamar, no inirefri (tiene fecha 01/01/1900)
                   TO_CHAR(t.inirefri, 'HH24:MI:SS'),
                   '3', SYSDATE, t.fechamar,
                   'DEPURACION: IniRefri teorico (R1)', 'A'
            FROM SCA_ASISTENCIA_TAREO t
            WHERE t.fechamar = v_fecha_proceso
            AND t.cod_empresa LIKE v_empresa_filtro
            AND t.cod_personal LIKE v_personal_filtro
            AND t.inirefri IS NOT NULL
            AND t.codaux4 LIKE '%' || c_R1 || '%'
            AND t.num_fotocheck IS NOT NULL
            AND NOT EXISTS (
                SELECT 1 FROM SCA_HISTORIAL h
                WHERE h.idtarjeta = t.num_fotocheck
                AND h.fec_equiv = t.fechamar
                AND RTRIM(h.hora) = TO_CHAR(t.inirefri, 'HH24:MI:SS')
            );
            v_count_historial := v_count_historial + SQL%ROWCOUNT;
            
            -- Insertar marcas de FINREFRI teorico en SCA_HISTORIAL (R1)
            INSERT INTO SCA_HISTORIAL (idCod, idLectora, idTarjeta, fecha, hora, tiporeg, fecreg, fec_equiv, motivo, ind_aman_hor_est)
            SELECT id_cod_seq.NEXTVAL, '005', t.num_fotocheck,
                   TO_CHAR(t.fechamar, 'DD/MM/YYYY'),  -- FIX: usar fechamar, no finrefri (tiene fecha 01/01/1900)
                   TO_CHAR(t.finrefri, 'HH24:MI:SS'),
                   '3', SYSDATE, t.fechamar,
                   'DEPURACION: FinRefri teorico (R1)', 'A'
            FROM SCA_ASISTENCIA_TAREO t
            WHERE t.fechamar = v_fecha_proceso
            AND t.cod_empresa LIKE v_empresa_filtro
            AND t.cod_personal LIKE v_personal_filtro
            AND t.finrefri IS NOT NULL
            AND t.codaux4 LIKE '%' || c_R1 || '%'
            AND t.num_fotocheck IS NOT NULL
            AND NOT EXISTS (
                SELECT 1 FROM SCA_HISTORIAL h
                WHERE h.idtarjeta = t.num_fotocheck
                AND h.fec_equiv = t.fechamar
                AND RTRIM(h.hora) = TO_CHAR(t.finrefri, 'HH24:MI:SS')
            );
            v_count_historial := v_count_historial + SQL%ROWCOUNT;
        END IF;
        
        -- =====================================================================
        -- PASO 3A: Completar INIREFRI donde falta pero tiene FINREFRI real
        -- Calcula: INIREFRI = FINREFRI - TIEMPO_REFRIGERIO (TOTREF)
        -- INCLUYE turnos nocturnos si el horario define refrigerio
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t
        SET t.inirefri = t.finrefri -
                CASE
                    -- Fuente 1: totref disponible y > 0
                    WHEN t.totref IS NOT NULL AND t.totref <> TO_DATE('01/01/1900', 'dd/MM/yyyy')
                    THEN (TO_NUMBER(TO_CHAR(t.totref, 'HH24')) * 60 +
                          TO_NUMBER(TO_CHAR(t.totref, 'MI'))) / 1440
                    -- Fuente 2: calcular de horfinref - horiniref (igual que PASO 3B)
                    ELSE (t.horfinref - t.horiniref)
                END,
            t.codaux4 = CASE WHEN t.codaux4 IS NULL THEN c_R2 ELSE t.codaux4 || c_SEP || c_R2 END,
            t.codaux5 = SUBSTR(CASE WHEN t.codaux5 IS NULL THEN d_R2 ELSE t.codaux5 || c_SEP || d_R2 END, 1, 50)
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
        AND t.inirefri IS NULL
        AND t.finrefri IS NOT NULL
        -- FIX 21/04/2026: aceptar totref valido O horfinref-horiniref calculable (igual que PASO 3B)
        AND (
            (t.totref IS NOT NULL AND t.totref <> TO_DATE('01/01/1900', 'dd/MM/yyyy'))
            OR
            (t.horfinref IS NOT NULL AND t.horiniref IS NOT NULL
             AND t.horfinref > t.horiniref
             AND t.horfinref <> TO_DATE('01/01/1900', 'dd/MM/yyyy'))
        )
        AND t.entrada IS NOT NULL
        AND t.salida IS NOT NULL
        -- FIX 14/04/2026: Removida exclusion nocturna (3er turno SI tiene refrigerio)
        AND NVL(t.descanso, 'N') <> 'S'
        AND NVL(t.ind_cerrado, 'N') <> 'S'
        -- Excluir empleados con permisos/ausencias activos
        AND t.per_desc_med IS NULL
        AND t.per_subsidio IS NULL
        AND t.per_goce IS NULL
        AND t.per_sgoce IS NULL
        AND t.per_vaca IS NULL
        AND t.per_suspension IS NULL
        AND t.per_lic_pat IS NULL
        AND t.per_lic_fac IS NULL;
        
        v_count_inirefri := SQL%ROWCOUNT;
        
        -- Insertar marca de INIREFRI calculado en SCA_HISTORIAL (R2)
        IF v_count_inirefri > 0 THEN
            INSERT INTO SCA_HISTORIAL (idCod, idLectora, idTarjeta, fecha, hora, tiporeg, fecreg, fec_equiv, motivo, ind_aman_hor_est)
            SELECT id_cod_seq.NEXTVAL, '005', t.num_fotocheck,
                   TO_CHAR(t.fechamar, 'DD/MM/YYYY'),  -- FIX: usar fechamar, no inirefri
                   TO_CHAR(t.inirefri, 'HH24:MI:SS'),
                   '3', SYSDATE, t.fechamar,
                   'DEPURACION: IniRefri calculado (R2)', 'A'
            FROM SCA_ASISTENCIA_TAREO t
            WHERE t.fechamar = v_fecha_proceso
            AND t.cod_empresa LIKE v_empresa_filtro
            AND t.cod_personal LIKE v_personal_filtro
            AND t.inirefri IS NOT NULL
            AND t.codaux4 LIKE '%' || c_R2 || '%'
            AND t.num_fotocheck IS NOT NULL
            AND NOT EXISTS (
                SELECT 1 FROM SCA_HISTORIAL h
                WHERE h.idtarjeta = t.num_fotocheck
                AND h.fec_equiv = t.fechamar
                AND RTRIM(h.hora) = TO_CHAR(t.inirefri, 'HH24:MI:SS')
            );
            v_count_historial := v_count_historial + SQL%ROWCOUNT;
        END IF;
        
        -- =====================================================================
        -- PASO 3B: Completar FINREFRI donde falta pero tiene INIREFRI real
        -- Calcula: FINREFRI = INIREFRI + TIEMPO_REFRIGERIO
        -- Fuente 1: totref (campo directo en tareo)
        -- Fuente 2 (fallback): horfinref - horiniref (del horario)
        -- FIX 21/04/2026: cuando totref IS NULL o 00:00 (campo no poblado),
        -- usar horfinref - horiniref como duracion. Ejemplo: horario 07:00-15:45
        -- con refri 13:15-14:00 (45 min): totref puede ser NULL pero horfinref-horiniref=00:45
        -- Ejemplo: INIREFRI=13:38, horfinref-horiniref=45min -> FINREFRI=14:23
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t
        SET t.finrefri = t.inirefri + 
                CASE
                    -- Fuente 1: totref disponible y > 0
                    WHEN t.totref IS NOT NULL AND t.totref <> TO_DATE('01/01/1900', 'dd/MM/yyyy')
                    THEN (TO_NUMBER(TO_CHAR(t.totref, 'HH24')) * 60 +
                          TO_NUMBER(TO_CHAR(t.totref, 'MI'))) / 1440
                    -- Fuente 2: calcular de horfinref - horiniref
                    ELSE (t.horfinref - t.horiniref)
                END,
            t.codaux4 = CASE WHEN t.codaux4 IS NULL THEN c_R3 ELSE t.codaux4 || c_SEP || c_R3 END,
            t.codaux5 = SUBSTR(CASE WHEN t.codaux5 IS NULL THEN d_R3 ELSE t.codaux5 || c_SEP || d_R3 END, 1, 50)
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
        AND t.finrefri IS NULL
        AND t.inirefri IS NOT NULL
        -- FIX 21/04/2026: aceptar totref valido O horfinref-horiniref calculable
        AND (
            (t.totref IS NOT NULL AND t.totref <> TO_DATE('01/01/1900', 'dd/MM/yyyy'))
            OR
            (t.horfinref IS NOT NULL AND t.horiniref IS NOT NULL
             AND t.horfinref > t.horiniref
             AND t.horfinref <> TO_DATE('01/01/1900', 'dd/MM/yyyy'))
        )
        AND t.entrada IS NOT NULL
        AND t.salida IS NOT NULL
        -- FIX 14/04/2026: Removida exclusion nocturna (3er turno SI tiene refrigerio)
        AND NVL(t.descanso, 'N') <> 'S'
        AND NVL(t.ind_cerrado, 'N') <> 'S'
        -- Excluir empleados con permisos/ausencias activos
        AND t.per_desc_med IS NULL
        AND t.per_subsidio IS NULL
        AND t.per_goce IS NULL
        AND t.per_sgoce IS NULL
        AND t.per_vaca IS NULL
        AND t.per_suspension IS NULL
        AND t.per_lic_pat IS NULL
        AND t.per_lic_fac IS NULL;
        
        v_count_finrefri := SQL%ROWCOUNT;
        
        -- Insertar marca de FINREFRI calculado en SCA_HISTORIAL (R3)
        IF v_count_finrefri > 0 THEN
            INSERT INTO SCA_HISTORIAL (idCod, idLectora, idTarjeta, fecha, hora, tiporeg, fecreg, fec_equiv, motivo, ind_aman_hor_est)
            SELECT id_cod_seq.NEXTVAL, '005', t.num_fotocheck,
                   TO_CHAR(t.fechamar, 'DD/MM/YYYY'),  -- FIX: usar fechamar, no finrefri
                   TO_CHAR(t.finrefri, 'HH24:MI:SS'),
                   '3', SYSDATE, t.fechamar,
                   'DEPURACION: FinRefri calculado (R3)', 'A'
            FROM SCA_ASISTENCIA_TAREO t
            WHERE t.fechamar = v_fecha_proceso
            AND t.cod_empresa LIKE v_empresa_filtro
            AND t.cod_personal LIKE v_personal_filtro
            AND t.finrefri IS NOT NULL
            AND t.codaux4 LIKE '%' || c_R3 || '%'
            AND t.num_fotocheck IS NOT NULL
            AND NOT EXISTS (
                SELECT 1 FROM SCA_HISTORIAL h
                WHERE h.idtarjeta = t.num_fotocheck
                AND h.fec_equiv = t.fechamar
                AND RTRIM(h.hora) = TO_CHAR(t.finrefri, 'HH24:MI:SS')
            );
            v_count_historial := v_count_historial + SQL%ROWCOUNT;
        END IF;
        
        -- =====================================================================
        -- PASO 3C-NOC: Limpiar refrigerio en horarios nocturnos sin entrada anticipada
        -- 
        -- CASO 13: Horario nocturno puro (23:00-07:00) SIN entrada anticipada
        --          El turno de 8 horas NO tiene derecho a refrigerio.
        --          Cualquier marca cercana a entrada es DUPLICADO, no refrigerio.
        --
        -- IMPORTANTE: Se ejecuta DESPUES de 2B, 3A, 3B para LIMPIAR el refrigerio
        --             que esos PASOs pudieron haber asignado incorrectamente.
        --
        -- Ejemplo: APOLINARIO 09/04/2026
        --   Horario: TERCER TURNO (23:00-07:00), entrada_fijada=23:00
        --   Marcas: 22:54 (entrada), 22:55 (inirefri MAL), 07:00 (salida)
        --   22:55 esta a 1 min de 22:54 = marca duplicada, no refrigerio
        --   
        -- REGLA: Horario nocturno SIN entrada anticipada NO tiene refrigerio
        --   - Entrada anticipada = entrada real >= 2 horas antes de entrada_fijada
        --   - Sin anticipacion = entrada real < 2 horas antes (entrada normal)
        --   - Turno nocturno puro = 8 horas de trabajo = sin refrigerio
        --
        -- DIFERENCIA CON PASO 5G:
        --   PASO 5G: Entrada MUY anticipada (>=2h antes, ej: 19:00) -> SI tiene refrigerio
        --   PASO 3C-NOC: Entrada normal (< 2h antes, ej: 22:54) -> NO tiene refrigerio
        --
        -- ACCION:
        --   - Limpiar inirefri/finrefri (ya asignados por PASOs anteriores)
        --   - Eliminar marcas de refrigerio de SCA_HISTORIAL
        --   - nummarcaciones se corrige en PASO 8
        --
        -- TABLAS ESCRITURA: SCA_ASISTENCIA_TAREO, SCA_HISTORIAL
        -- TABLAS CONSULTA:  (ninguna adicional)
        -- =====================================================================
        FOR rec_rn IN (
            SELECT t.ROWID AS rid,
                   t.num_fotocheck, t.fechamar,
                   t.entrada, t.inirefri, t.finrefri, t.salida,
                   t.entrada_fijada, t.nummarcaciones,
                   t.codaux4, t.codaux5
            FROM SCA_ASISTENCIA_TAREO t
            WHERE t.fechamar = v_fecha_proceso
            AND t.cod_empresa LIKE v_empresa_filtro
            AND t.cod_personal LIKE v_personal_filtro
            AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
            -- Turno nocturno (entrada_fijada >= 22:00)
            AND t.entrada_fijada IS NOT NULL
            AND TO_CHAR(t.entrada_fijada, 'HH24MI') >= '2200'
            -- SIN entrada anticipada significativa (< 2 horas antes de horario)
            -- Comparar solo HORA, ignorando fecha (entrada_fijada puede tener fecha diferente)
            AND t.entrada IS NOT NULL
            AND (
                -- Caso 1: Entrada en la misma franja horaria (~20:00-23:59)
                TO_NUMBER(TO_CHAR(t.entrada, 'HH24')) >= 20
                OR
                -- Caso 2: Entrada justo despues de medianoche (00:00-01:59) - aun es cerca del horario
                TO_NUMBER(TO_CHAR(t.entrada, 'HH24')) < 2
            )
            -- Tiene refrigerio asignado (por tareo o por PASOs 2B/3A/3B)
            AND (t.inirefri IS NOT NULL OR t.finrefri IS NOT NULL)
            AND t.salida IS NOT NULL
            AND NVL(t.descanso, 'N') <> 'S'
            AND NVL(t.ind_cerrado, 'N') <> 'S'
            AND t.num_fotocheck IS NOT NULL
        ) LOOP
            DBMS_OUTPUT.PUT_LINE('PASO 3C-NOC: Nocturno sin anticipacion, fotocheck=' || rec_rn.num_fotocheck ||
                                 ' entrada=' || TO_CHAR(rec_rn.entrada, 'HH24:MI') ||
                                 ' inirefri=' || TO_CHAR(rec_rn.inirefri, 'HH24:MI') || 
                                 ' finrefri=' || TO_CHAR(rec_rn.finrefri, 'HH24:MI') || ' -> LIMPIANDO');
            
            -- Eliminar marca INIREFRI: si es diferente de ENTRADA y SALIDA
            -- FIX: remover condicion de 5 minutos - si vamos a limpiar inirefri,
            --      la marca debe eliminarse de historial (no dejarla huerfana)
            IF rec_rn.inirefri IS NOT NULL 
               AND TO_CHAR(rec_rn.inirefri, 'HH24:MI:SS') <> TO_CHAR(rec_rn.entrada, 'HH24:MI:SS')
               AND TO_CHAR(rec_rn.inirefri, 'HH24:MI:SS') <> TO_CHAR(rec_rn.salida, 'HH24:MI:SS') THEN
                DELETE FROM SCA_HISTORIAL h
                WHERE h.idtarjeta = rec_rn.num_fotocheck
                AND h.fec_equiv = rec_rn.fechamar
                AND RTRIM(h.hora) = TO_CHAR(rec_rn.inirefri, 'HH24:MI:SS');
                
                IF SQL%ROWCOUNT > 0 THEN
                    DBMS_OUTPUT.PUT_LINE('  -> Eliminada marca inirefri ' || TO_CHAR(rec_rn.inirefri, 'HH24:MI:SS') || ' de SCA_HISTORIAL');
                END IF;
            ELSE
                -- inirefri es igual a entrada o salida, no eliminar
                IF rec_rn.inirefri IS NOT NULL THEN
                    DBMS_OUTPUT.PUT_LINE('  -> Marca inirefri ' || TO_CHAR(rec_rn.inirefri, 'HH24:MI:SS') || 
                                         ' NO eliminada (es igual a entrada/salida)');
                END IF;
            END IF;
            
            -- Eliminar marca FINREFRI de SCA_HISTORIAL
            -- SOLO si es diferente de ENTRADA y SALIDA
            IF rec_rn.finrefri IS NOT NULL
               AND TO_CHAR(rec_rn.finrefri, 'HH24:MI:SS') <> TO_CHAR(rec_rn.entrada, 'HH24:MI:SS')
               AND TO_CHAR(rec_rn.finrefri, 'HH24:MI:SS') <> TO_CHAR(rec_rn.salida, 'HH24:MI:SS') THEN
                DELETE FROM SCA_HISTORIAL h
                WHERE h.idtarjeta = rec_rn.num_fotocheck
                AND h.fec_equiv = rec_rn.fechamar
                AND RTRIM(h.hora) = TO_CHAR(rec_rn.finrefri, 'HH24:MI:SS');
                
                IF SQL%ROWCOUNT > 0 THEN
                    DBMS_OUTPUT.PUT_LINE('  -> Eliminada marca finrefri ' || TO_CHAR(rec_rn.finrefri, 'HH24:MI:SS') || ' de SCA_HISTORIAL');
                END IF;
            END IF;
            
            -- Limpiar inirefri/finrefri del tareo
            UPDATE SCA_ASISTENCIA_TAREO t
            SET t.inirefri = NULL,
                t.finrefri = NULL,
                t.nummarcaciones = 2,  -- Solo entrada y salida
                t.alerta01 = NULL,     -- Ya no es marca impar
                t.codaux4 = CASE WHEN t.codaux4 IS NULL THEN c_RN ELSE t.codaux4 || c_SEP || c_RN END,
                t.codaux5 = SUBSTR(CASE WHEN t.codaux5 IS NULL THEN d_RN ELSE t.codaux5 || c_SEP || d_RN END, 1, 50)
            WHERE ROWID = rec_rn.rid;
            
            v_count_rn := v_count_rn + 1;
        END LOOP;
        
        IF v_count_rn > 0 THEN
            DBMS_OUTPUT.PUT_LINE('PASO 3C-NOC: Limpiados ' || v_count_rn || ' registros (nocturno sin refrigerio)');
        END IF;
        
        -- =====================================================================
        -- PASO 3D: Corregir salida imposible (salida < inirefri)
        --
        -- CASO: El tareo tiene E+S pero la salida cae ANTES del inicio de refrigerio.
        --       Esto es cronologicamente imposible.
        --
        -- CAUSA TIPICA:
        --   1. Empleado tiene 2 marcas FC: 06:47 (entrada) y 12:17 (refrigerio real)
        --   2. Alguien agrego manualmente 12:30 y 13:00 como refrigerio en el sistema
        --   3. Aquarius asigno: E=06:47, S=12:17 (solo via FC), IR=NULL, FR=NULL
        --   4. PASO 2B-PRE no encontro las marcas manuales (fuera del rango salida-30min)
        --   5. PASO 2B asigno refrigerio teorico: IR=12:30, FR=13:00
        --   6. Resultado: S(12:17) < IR(12:30) -> IMPOSIBLE
        --
        -- ESTRATEGIA (dos fases por cada registro):
        --
        --   FASE A - Buscar marca OCULTA en SCA_HISTORIAL:
        --     Aquarius puede marcar ind_anulado='S' o ind_noprocesar<>0 en la
        --     salida real cuando detecta "exceso" de marcaciones.
        --     Criterios: despues de finrefri (o inirefri), dentro de 2h de salida_fijada,
        --     la mas cercana a salida_fijada.
        --     Si existe -> esa ES la salida real (SSR = Salida real Restaurada).
        --     Se inserta nueva marca visible en SCA_HISTORIAL para que PASO 8 la cuente.
        --     La marca oculta original NO se toca.
        --
        --   FASE B - No hay marca oculta -> salida_fijada (teorico) (SS).
        --
        -- Ejemplo: CERVANTES 037810, 14/04/2026
        --   Marcas visibles: 06:47 FC, 12:17 FC, 12:30 Manual, 13:00 Manual
        --   Posible oculta:  15:01 FC (ind_anulado='S' por Aquarius)
        --   Si existe oculta: E=06:47, IR=12:30, FR=13:00, S=15:01 (SSR)
        --   Si no existe:     E=06:47, IR=12:30, FR=13:00, S=15:00 (SS)
        --
        -- TABLAS ESCRITURA: SCA_ASISTENCIA_TAREO, SCA_HISTORIAL
        -- TABLAS CONSULTA:  SCA_HISTORIAL
        -- =====================================================================
        FOR rec_ss IN (
            SELECT t.ROWID AS rid,
                   t.num_fotocheck, t.fechamar,
                   t.entrada, t.inirefri, t.finrefri, t.salida,
                   t.salida_fijada, t.codaux4, t.codaux5
            FROM SCA_ASISTENCIA_TAREO t
            WHERE t.fechamar = v_fecha_proceso
            AND t.cod_empresa LIKE v_empresa_filtro
            AND t.cod_personal LIKE v_personal_filtro
            AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
            AND t.salida IS NOT NULL
            AND t.inirefri IS NOT NULL
            -- FIX 20/04/2026: comparar solo HORA (inirefri puede tener base 01/01/1900 desde PASO 2B
            -- o fecha real desde PASO 2B-PRE; la comparacion de DATE completa falla cuando
            -- inirefri=1900-01-01 12:30 y salida=2026-04-14 12:17 -> 2026 < 1900 = FALSE)
            AND TO_CHAR(t.salida, 'HH24MI') < TO_CHAR(t.inirefri, 'HH24MI')
            -- FIX 27/04/2026: Excluir nocturnos por cruce de dia (no por hora fija >= 20).
            -- ANTES: entrada_fijada < 20:00 -> bug en HORARIO 19-03 (ent_fij=19:00) que disparaba
            --        SS aunque la salida real era 07:04 del dia siguiente (correcto cronologicamente).
            -- AHORA: si salida_fijada < entrada_fijada (turno cruza medianoche) -> es nocturno;
            --        ademas si la salida real ya es del dia siguiente (TRUNC > fechamar) tampoco
            --        debe corregirse. Mismo criterio que PASO 3F.
            AND (t.entrada_fijada IS NULL OR t.salida_fijada IS NULL
                 OR t.salida_fijada > t.entrada_fijada)
            AND TRUNC(t.salida) = t.fechamar
            -- FIX 21/04/2026: Excluir N6 (salida ya corregida al dia siguiente)
            -- Para N6: salida=18/04 07:03, HH24MI='0703' < inirefri HH24MI='2006' = TRUE
            -- pero la salida YA esta correcta; PASO 3D la sobreescribiria con teorico.
            AND NVL(t.codaux4, ' ') NOT LIKE '%' || c_N6 || '%'
            AND t.salida_fijada IS NOT NULL
            AND t.entrada IS NOT NULL
            AND NVL(t.descanso, 'N') <> 'S'
            AND NVL(t.ind_cerrado, 'N') <> 'S'
            AND t.per_desc_med IS NULL
            AND t.per_subsidio IS NULL
            AND t.per_goce IS NULL
            AND t.per_sgoce IS NULL
            AND t.per_vaca IS NULL
            AND t.per_suspension IS NULL
            AND t.per_lic_pat IS NULL
            AND t.per_lic_fac IS NULL
        ) LOOP
            v_marca_inter := NULL;
            v_idcod_inter := NULL;
            
            -- FASE A: Buscar marca oculta (anulada o noprocesar) como candidata a salida real
            BEGIN
                SELECT idcod, marca INTO v_idcod_inter, v_marca_inter FROM (
                    SELECT h.idcod,
                           TO_DATE(TO_CHAR(h.fec_equiv, 'DD/MM/YYYY') || ' ' || RTRIM(h.hora), 'DD/MM/YYYY HH24:MI:SS') AS marca
                    FROM SCA_HISTORIAL h
                    WHERE h.idtarjeta = rec_ss.num_fotocheck
                    AND h.fec_equiv = rec_ss.fechamar
                    -- Solo marcas ocultas (anuladas o marcadas noprocesar)
                    AND (NVL(h.ind_anulado, 'N') = 'S' OR NVL(h.ind_noprocesar, 0) <> 0)
                    -- Despues del fin de refrigerio (o de inirefri si finrefri es NULL)
                    AND TO_DATE(TO_CHAR(h.fec_equiv, 'DD/MM/YYYY') || ' ' || RTRIM(h.hora), 'DD/MM/YYYY HH24:MI:SS')
                        > NVL(rec_ss.finrefri, rec_ss.inirefri)
                    -- Dentro de 2 horas de salida_fijada (rango razonable)
                    AND TO_DATE(TO_CHAR(h.fec_equiv, 'DD/MM/YYYY') || ' ' || RTRIM(h.hora), 'DD/MM/YYYY HH24:MI:SS')
                        <= rec_ss.salida_fijada + (120/1440)
                    ORDER BY ABS(
                        TO_DATE(TO_CHAR(h.fec_equiv, 'DD/MM/YYYY') || ' ' || RTRIM(h.hora), 'DD/MM/YYYY HH24:MI:SS')
                        - rec_ss.salida_fijada
                    )
                ) WHERE ROWNUM = 1;
            EXCEPTION WHEN NO_DATA_FOUND THEN
                v_marca_inter := NULL;
                v_idcod_inter := NULL;
            END;
            
            IF v_marca_inter IS NOT NULL THEN
                -- FASE A: Marca oculta encontrada -> es la salida real (SSR)
                -- Insertar nueva marca VISIBLE en SCA_HISTORIAL con motivo DEPURACION
                -- (la original oculta NO se toca; si ROLLBACK, se elimina esta nueva)
                INSERT INTO SCA_HISTORIAL (idCod, idLectora, idTarjeta, fecha, hora, tiporeg, fecreg, fec_equiv, motivo, ind_aman_hor_est)
                SELECT id_cod_seq.NEXTVAL, '005', rec_ss.num_fotocheck,
                       TO_CHAR(rec_ss.fechamar, 'DD/MM/YYYY'),
                       TO_CHAR(v_marca_inter, 'HH24:MI:SS'),
                       '3', SYSDATE, rec_ss.fechamar,
                       'DEPURACION: Salida real restaurada (SSR)', 'A'
                FROM DUAL
                WHERE NOT EXISTS (
                    SELECT 1 FROM SCA_HISTORIAL h
                    WHERE h.idtarjeta = rec_ss.num_fotocheck
                    AND h.fec_equiv = rec_ss.fechamar
                    AND RTRIM(h.hora) = TO_CHAR(v_marca_inter, 'HH24:MI:SS')
                    AND NVL(h.ind_anulado, 'N') <> 'S'
                    AND NVL(h.ind_noprocesar, 0) = 0
                );
                v_count_historial := v_count_historial + SQL%ROWCOUNT;
                
                UPDATE SCA_ASISTENCIA_TAREO
                SET salida  = v_marca_inter,
                    codaux4 = CASE WHEN codaux4 IS NULL THEN c_SSR ELSE codaux4 || c_SEP || c_SSR END,
                    codaux5 = SUBSTR(CASE WHEN codaux5 IS NULL THEN d_SSR ELSE codaux5 || c_SEP || d_SSR END, 1, 50)
                WHERE ROWID = rec_ss.rid;
                
                v_count_ss := v_count_ss + 1;
                DBMS_OUTPUT.PUT_LINE('PASO 3D (SSR): fotocheck=' || rec_ss.num_fotocheck ||
                                     ' salida_erronea=' || TO_CHAR(rec_ss.salida, 'HH24:MI') ||
                                     ' -> salida_real_oculta=' || TO_CHAR(v_marca_inter, 'HH24:MI'));
            ELSE
                -- FASE B: Sin marca oculta -> usar salida_fijada (teorico) (SS)
                UPDATE SCA_ASISTENCIA_TAREO
                SET salida  = rec_ss.salida_fijada,
                    codaux4 = CASE WHEN codaux4 IS NULL THEN c_SS ELSE codaux4 || c_SEP || c_SS END,
                    codaux5 = SUBSTR(CASE WHEN codaux5 IS NULL THEN d_SS ELSE codaux5 || c_SEP || d_SS END, 1, 50)
                WHERE ROWID = rec_ss.rid;
                
                -- Insertar marca de SALIDA teorica en SCA_HISTORIAL
                INSERT INTO SCA_HISTORIAL (idCod, idLectora, idTarjeta, fecha, hora, tiporeg, fecreg, fec_equiv, motivo, ind_aman_hor_est)
                SELECT id_cod_seq.NEXTVAL, '005', rec_ss.num_fotocheck,
                       TO_CHAR(rec_ss.fechamar, 'DD/MM/YYYY'),
                       TO_CHAR(rec_ss.salida_fijada, 'HH24:MI:SS'),
                       '3', SYSDATE, rec_ss.fechamar,
                       'DEPURACION: Salida corregida (SS)', 'A'
                FROM DUAL
                WHERE NOT EXISTS (
                    SELECT 1 FROM SCA_HISTORIAL h
                    WHERE h.idtarjeta = rec_ss.num_fotocheck
                    AND h.fec_equiv = rec_ss.fechamar
                    AND RTRIM(h.hora) = TO_CHAR(rec_ss.salida_fijada, 'HH24:MI:SS')
                    AND NVL(h.ind_anulado, 'N') <> 'S'
                    AND NVL(h.ind_noprocesar, 0) = 0
                );
                v_count_historial := v_count_historial + SQL%ROWCOUNT;
                v_count_ss := v_count_ss + 1;
                DBMS_OUTPUT.PUT_LINE('PASO 3D (SS): fotocheck=' || rec_ss.num_fotocheck ||
                                     ' salida_erronea=' || TO_CHAR(rec_ss.salida, 'HH24:MI') ||
                                     ' -> salida_teorica=' || TO_CHAR(rec_ss.salida_fijada, 'HH24:MI'));
            END IF;
        END LOOP;
        
        IF v_count_ss > 0 THEN
            DBMS_OUTPUT.PUT_LINE('PASO 3D: Salidas imposibles corregidas -> ' || v_count_ss || ' registros');
        END IF;
        
        -- =====================================================================
        -- PASO 3E: Corregir refrigerio imposible (inirefri ANTES de entrada)
        --
        -- CASO: El tareo tiene inirefri/finrefri anteriores a la hora de entrada.
        --       Esto es cronologicamente imposible: no se puede salir a refrigerio
        --       antes de haber entrado.
        --
        -- CAUSA TIPICA:
        --   1. Empleado no tiene FC de entrada (llego muy tarde o no marco).
        --   2. Alguien cargo manualmente marcas de refrigerio (12:30, 13:00)
        --      antes de que llegara (error administrativo).
        --   3. Aquarius asigno la primera FC del dia (13:06) como ENTRADA y las
        --      manuales anteriores (12:30, 13:00) como INIREFRI/FINREFRI.
        --   4. Resultado: inirefri(12:30) < entrada(13:06) = IMPOSIBLE.
        --
        -- EJEMPLO: ARREDONDO MACO 032933, 16/04/2026
        --   Marcas: 12:30 Manual, 13:00 Manual, 13:06 FC, 15:01 FC
        --   Tareo: E=13:06, IR=12:30, FR=13:00, S=15:01, Tardanza=06:06
        --   Fix: limpiar inirefri/finrefri
        --   Resultado: E=13:06, S=15:01, sin refri, tardanza=06:06 (real)
        --
        -- ACCION:
        --   1. Eliminar de SCA_HISTORIAL SOLO las marcas DEPURACION que corresponden
        --      a inirefri/finrefri (insertadas por PASO 2B como teorico R1/R2/R3).
        --      NO se eliminan marcas reales (motivo no LIKE 'DEPURACION%').
        --      Ejemplo HERRERA 037641 17/04: PASO 2B inserto 12:30 y 13:00 DEPUR;
        --      PASO 3E limpia tareo pero sin este DELETE quedan 5 marks -> IMPAR.
        --   2. Limpiar inirefri/finrefri del tareo.
        --   PASO 2B NO re-asignara teorico porque PASO 3E ocurre despues.
        --   PASO 8 excluye registros con tag RI (igual que RN).
        --
        -- EXCLUYE: Turnos nocturnos (entrada_fijada >= 20:00) donde inirefri
        --          nocturno (23:00) es naturalmente mayor a una entrada tardiana.
        --
        -- TABLAS ESCRITURA: SCA_ASISTENCIA_TAREO, SCA_HISTORIAL (DELETE DEPURACION)
        -- TABLAS CONSULTA:  SCA_HISTORIAL
        -- =====================================================================
        -- Eliminar PRIMERO las marcas DEPURACION de SCA_HIS antes de limpiar tareo
        DELETE FROM SCA_HISTORIAL h
        WHERE h.fec_equiv = v_fecha_proceso
        AND h.motivo LIKE 'DEPURACION%'
        AND NVL(h.ind_anulado, 'N') <> 'S'
        AND EXISTS (
            SELECT 1 FROM SCA_ASISTENCIA_TAREO t
            WHERE t.fechamar = v_fecha_proceso
            AND t.cod_empresa LIKE v_empresa_filtro
            AND t.cod_personal LIKE v_personal_filtro
            AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
            AND t.num_fotocheck = h.idtarjeta
            AND t.inirefri IS NOT NULL
            AND t.entrada IS NOT NULL
            AND TO_CHAR(t.inirefri, 'HH24MI') < TO_CHAR(t.entrada, 'HH24MI')
            AND (t.entrada_fijada IS NULL OR t.salida_fijada IS NULL
                 OR t.salida_fijada > t.entrada_fijada)  -- NO nocturno cruzando dia
            AND NVL(t.descanso, 'N') <> 'S'
            AND NVL(t.ind_cerrado, 'N') <> 'S'
            AND (
                RTRIM(h.hora) = TO_CHAR(t.inirefri, 'HH24:MI:SS')
                OR RTRIM(h.hora) = TO_CHAR(t.finrefri, 'HH24:MI:SS')
            )
        );
        
        IF SQL%ROWCOUNT > 0 THEN
            DBMS_OUTPUT.PUT_LINE('PASO 3E: Eliminadas ' || SQL%ROWCOUNT || ' marcas DEPURACION de SCA_HIS (refri imposible)');
        END IF;
        
        UPDATE SCA_ASISTENCIA_TAREO t
        SET t.inirefri = NULL,
            t.finrefri = NULL,
            t.codaux4 = CASE WHEN t.codaux4 IS NULL THEN c_RI ELSE t.codaux4 || c_SEP || c_RI END,
            t.codaux5 = SUBSTR(CASE WHEN t.codaux5 IS NULL THEN d_RI ELSE t.codaux5 || c_SEP || d_RI END, 1, 50)
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
        AND t.inirefri IS NOT NULL
        AND t.entrada IS NOT NULL
        -- inirefri ANTES de entrada = cronologicamente imposible (comparar solo hora)
        -- Cubre tanto inirefri con fecha real como con base 01/01/1900
        AND TO_CHAR(t.inirefri, 'HH24MI') < TO_CHAR(t.entrada, 'HH24MI')
        -- Excluir turnos nocturnos: en horario 23:00-07:00, inirefri(23:30) puede
        -- compararse con entrada real de tarde (19:00) pero es correcto.
        -- Detectar nocturno por salida_fijada < entrada_fijada (cruza dia).
        -- Si no hay entrada_fijada, asumir turno diurno (no excluir)
        AND (t.entrada_fijada IS NULL OR t.salida_fijada IS NULL
             OR t.salida_fijada > t.entrada_fijada)
        AND NVL(t.descanso, 'N') <> 'S'
        AND NVL(t.ind_cerrado, 'N') <> 'S';
        
        v_count_ri := SQL%ROWCOUNT;
        IF v_count_ri > 0 THEN
            DBMS_OUTPUT.PUT_LINE('PASO 3E: Refrigerio anterior a entrada limpiado -> ' || v_count_ri || ' registros');
        END IF;
        
        -- =====================================================================
        -- PASO 3F: Corregir refrigerio truncado (SALIDA antes de FINREFRI)
        --
        -- CASO: El tareo tiene finrefri DESPUES de la salida real. Esto significa
        --       que el empleado salio temprano y no pudo terminar el refrigerio.
        --       Si el refri es teorico (asignado por PASO 2B en run anterior),
        --       es cronologicamente imposible.
        --
        -- CAUSA TIPICA:
        --   1. Empleado entra normal (06:52) pero sale temprano (13:24).
        --   2. Run anterior PASO 2B asigno refri teorico R1 (13:15-14:00).
        --   3. PASO 0-RESTORE re-inserta esas marcas como '0-REST' en SCA_HIS.
        --   4. Resultado: tareo perpetua refri teorico imposible (S(13:24)<FR(14:00)).
        --
        -- EJEMPLO: DOMINGUEZ MORI 034086, 28/03/2026
        --   Marcas reales: 06:52 entrada, 13:24 salida (sabado corto)
        --   Tareo: E=06:52, IR=13:15, FR=14:00, S=13:24 -> S<FR = IMPOSIBLE
        --   Fix: limpiar IR/FR. Resultado: E=06:52, S=13:24, sin refri.
        --
        -- ACCION: Igual que PASO 3E pero comparando SALIDA < FINREFRI
        --   1. Eliminar marcas DEPURACION/0-REST de SCA_HIS para esos IR/FR.
        --   2. Limpiar inirefri/finrefri del tareo.
        --
        -- EXCLUYE: Turnos nocturnos (entrada_fijada >= 20:00) donde salida
        --          puede ser cronologicamente despues aunque numericamente menor.
        --
        -- TABLAS ESCRITURA: SCA_ASISTENCIA_TAREO, SCA_HISTORIAL (DELETE DEPURACION)
        -- TABLAS CONSULTA:  SCA_HISTORIAL
        -- =====================================================================
        DELETE FROM SCA_HISTORIAL h
        WHERE h.fec_equiv = v_fecha_proceso
        AND h.motivo LIKE 'DEPURACION%'
        AND NVL(h.ind_anulado, 'N') <> 'S'
        AND EXISTS (
            SELECT 1 FROM SCA_ASISTENCIA_TAREO t
            WHERE t.fechamar = v_fecha_proceso
            AND t.cod_empresa LIKE v_empresa_filtro
            AND t.cod_personal LIKE v_personal_filtro
            AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
            AND t.num_fotocheck = h.idtarjeta
            AND t.finrefri IS NOT NULL
            AND t.salida IS NOT NULL
            AND TO_CHAR(t.salida, 'HH24MI') < TO_CHAR(t.finrefri, 'HH24MI')
            AND (t.entrada_fijada IS NULL OR t.salida_fijada IS NULL
                 OR t.salida_fijada > t.entrada_fijada)  -- NO nocturno cruzando dia
            AND NVL(t.descanso, 'N') <> 'S'
            AND NVL(t.ind_cerrado, 'N') <> 'S'
            AND (
                RTRIM(h.hora) = TO_CHAR(t.inirefri, 'HH24:MI:SS')
                OR RTRIM(h.hora) = TO_CHAR(t.finrefri, 'HH24:MI:SS')
            )
        );
        
        IF SQL%ROWCOUNT > 0 THEN
            DBMS_OUTPUT.PUT_LINE('PASO 3F: Eliminadas ' || SQL%ROWCOUNT || ' marcas DEPURACION de SCA_HIS (refri truncado)');
        END IF;
        
        UPDATE SCA_ASISTENCIA_TAREO t
        SET t.inirefri = NULL,
            t.finrefri = NULL,
            t.codaux4 = CASE WHEN t.codaux4 IS NULL THEN c_RT ELSE t.codaux4 || c_SEP || c_RT END,
            t.codaux5 = SUBSTR(CASE WHEN t.codaux5 IS NULL THEN d_RT ELSE t.codaux5 || c_SEP || d_RT END, 1, 50)
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
        AND t.finrefri IS NOT NULL
        AND t.salida IS NOT NULL
        -- salida ANTES de finrefri = cronologicamente imposible (comparar solo hora)
        AND TO_CHAR(t.salida, 'HH24MI') < TO_CHAR(t.finrefri, 'HH24MI')
        -- Excluir turnos nocturnos: detectar por salida_fijada < entrada_fijada (cruza dia)
        AND (t.entrada_fijada IS NULL OR t.salida_fijada IS NULL
             OR t.salida_fijada > t.entrada_fijada)
        AND NVL(t.descanso, 'N') <> 'S'
        AND NVL(t.ind_cerrado, 'N') <> 'S';
        
        IF SQL%ROWCOUNT > 0 THEN
            DBMS_OUTPUT.PUT_LINE('PASO 3F: Refrigerio truncado limpiado -> ' || SQL%ROWCOUNT || ' registros');
        END IF;
        
        -- =====================================================================
        -- PASO 4B: CASO 7 - Marcaciones consecutivas anómalas
        -- Cuando: Las 4 marcaciones están separadas por menos de 1 hora total
        --         (SALIDA - ENTRADA < 60 minutos)
        -- Esto indica error de marcación (ej: empleado marcó 4 veces seguidas)
        -- Acción: Conservar ENTRADA real, reemplazar resto con horario teórico
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t
        SET 
            -- Conservar ENTRADA real (no se modifica)
            -- Reemplazar refrigerio con teórico
            t.inirefri = t.horiniref,
            t.finrefri = t.horfinref,
            -- Reemplazar salida con teórico
            t.salida = t.salida_fijada,
            t.codaux4 = CASE WHEN t.codaux4 IS NULL THEN c_A1 ELSE t.codaux4 || c_SEP || c_A1 END,
            t.codaux5 = SUBSTR(CASE WHEN t.codaux5 IS NULL THEN d_A1 ELSE t.codaux5 || c_SEP || d_A1 END, 1, 50)
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
        -- Condición clave: SALIDA - ENTRADA < 1 hora (60 minutos = 60/1440 días)
        AND t.entrada IS NOT NULL
        AND t.salida IS NOT NULL
        AND (t.salida - t.entrada) < (60/1440)  -- Menos de 60 minutos
        -- Tiene marcaciones de refrigerio (indica que marcó 4 veces)
        AND (t.inirefri IS NOT NULL OR t.finrefri IS NOT NULL)
        -- Tiene horario teórico para corregir
        AND t.salida_fijada IS NOT NULL
        AND t.horiniref IS NOT NULL
        AND t.horfinref IS NOT NULL
        -- FIX 14/04/2026: Removida exclusion nocturna (3er turno SI tiene refrigerio)
        AND NVL(t.descanso, 'N') <> 'S'
        AND NVL(t.ind_cerrado, 'N') <> 'S';
        
        v_count_anomala := SQL%ROWCOUNT;
        
        -- Insertar marcas de SALIDA en SCA_HISTORIAL (PASO 4B)
        INSERT INTO SCA_HISTORIAL (idCod, idLectora, idTarjeta, fecha, hora, tiporeg, fecreg, fec_equiv, motivo, ind_aman_hor_est)
        SELECT id_cod_seq.NEXTVAL, '005', t.num_fotocheck,
               TO_CHAR(t.fechamar, 'DD/MM/YYYY'),  -- FIX: usar fechamar
               TO_CHAR(t.salida, 'HH24:MI:SS'),
               '3', SYSDATE, t.fechamar,
               'DEPURACION: Salida anomala 4B', 'A'
        FROM SCA_ASISTENCIA_TAREO t
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND t.codaux4 LIKE '%' || c_A1 || '%'
        AND t.salida IS NOT NULL
        AND t.num_fotocheck IS NOT NULL
        AND NOT EXISTS (
            SELECT 1 FROM SCA_HISTORIAL h
            WHERE h.idtarjeta = t.num_fotocheck
            AND h.fec_equiv = t.fechamar
            AND RTRIM(h.hora) = TO_CHAR(t.salida, 'HH24:MI:SS')
        );
        v_count_historial := v_count_historial + SQL%ROWCOUNT;
        
        -- Insertar marcas de INI REFRIGERIO en SCA_HISTORIAL (PASO 4B)
        INSERT INTO SCA_HISTORIAL (idCod, idLectora, idTarjeta, fecha, hora, tiporeg, fecreg, fec_equiv, motivo, ind_aman_hor_est)
        SELECT id_cod_seq.NEXTVAL, '005', t.num_fotocheck,
               TO_CHAR(t.fechamar, 'DD/MM/YYYY'),  -- FIX: usar fechamar, no inirefri
               TO_CHAR(t.inirefri, 'HH24:MI:SS'),
               '3', SYSDATE, t.fechamar,
               'DEPURACION: IniRefri anomala 4B', 'A'
        FROM SCA_ASISTENCIA_TAREO t
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND t.codaux4 LIKE '%' || c_A1 || '%'
        AND t.inirefri IS NOT NULL
        AND t.num_fotocheck IS NOT NULL
        AND NOT EXISTS (
            SELECT 1 FROM SCA_HISTORIAL h
            WHERE h.idtarjeta = t.num_fotocheck
            AND h.fec_equiv = t.fechamar
            AND RTRIM(h.hora) = TO_CHAR(t.inirefri, 'HH24:MI:SS')
        );
        v_count_historial := v_count_historial + SQL%ROWCOUNT;
        
        -- Insertar marcas de FIN REFRIGERIO en SCA_HISTORIAL (PASO 4B)
        INSERT INTO SCA_HISTORIAL (idCod, idLectora, idTarjeta, fecha, hora, tiporeg, fecreg, fec_equiv, motivo, ind_aman_hor_est)
        SELECT id_cod_seq.NEXTVAL, '005', t.num_fotocheck,
               TO_CHAR(t.fechamar, 'DD/MM/YYYY'),  -- FIX: usar fechamar, no finrefri
               TO_CHAR(t.finrefri, 'HH24:MI:SS'),
               '3', SYSDATE, t.fechamar,
               'DEPURACION: FinRefri anomala 4B', 'A'
        FROM SCA_ASISTENCIA_TAREO t
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND t.codaux4 LIKE '%' || c_A1 || '%'
        AND t.finrefri IS NOT NULL
        AND t.num_fotocheck IS NOT NULL
        AND NOT EXISTS (
            SELECT 1 FROM SCA_HISTORIAL h
            WHERE h.idtarjeta = t.num_fotocheck
            AND h.fec_equiv = t.fechamar
            AND RTRIM(h.hora) = TO_CHAR(t.finrefri, 'HH24:MI:SS')
        );
        v_count_historial := v_count_historial + SQL%ROWCOUNT;
        
        -- =====================================================================
        -- PASO 5G: 3er Turno con entrada anticipada y sobretiempo (FIX 13/04/2026)
        -- 
        -- CASO: Horario TERCER TURNO (23:00-07:00) pero empleado entra a las
        --       19:00 (7PM) para hacer sobretiempo. Tiene 3 marcas:
        --       18:59 (ENTRADA), 21:03 (INIREFRI), 07:00 (SALIDA)
        --       Falta FINREFRI que se calcula como INIREFRI + 30min.
        --
        -- REGLA: Empleados en 3er turno con sobretiempo tienen 30 min de refrigerio.
        --        Si hay marca intermedia entre entrada y salida, es INIREFRI.
        --
        -- CONDICIONES:
        -- 1. Turno nocturno (entrada_fijada >= 22:00, ej: TERCER TURNO 23:00)
        -- 2. Entrada real MUY anticipada (>= 2 horas antes de entrada_fijada)
        -- 3. Tiene marca intermedia en SCA_HISTORIAL entre entrada y salida
        -- 4. INIREFRI y FINREFRI son NULL
        --
        -- NOTA: PASO 1B (E2) excluye estos casos para no ajustar la entrada.
        -- NOTA: Ubicado ANTES de PASO 5 para que el recalculo de horas lo incluya.
        --
        -- ACCION:
        -- - Asignar marca intermedia como INIREFRI
        -- - Calcular FINREFRI = INIREFRI + 30 min
        -- - Calcular horas extras antes (sobretiempo)
        -- - NO ajustar entrada (es sobretiempo valido)
        --
        -- TABLAS ESCRITURA: SCA_ASISTENCIA_TAREO, SCA_HISTORIAL
        -- TABLAS CONSULTA:  SCA_HISTORIAL
        -- =====================================================================
        v_count_5g := 0;  -- Contador especifico para PASO 5G
        FOR rec IN (
            SELECT t.ROWID AS rid,
                   t.num_fotocheck, t.entrada, t.salida,
                   t.entrada_fijada, t.horiniref,
                   t.codaux4, t.codaux5
            FROM SCA_ASISTENCIA_TAREO t
            WHERE t.fechamar = v_fecha_proceso
            AND t.cod_empresa LIKE v_empresa_filtro
            AND t.cod_personal LIKE v_personal_filtro
            -- NO filtrar por obrero - aplica a todos los empleados en 3er turno
            -- AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
            -- Turno nocturno (3er turno: entrada >= 22:00)
            AND t.entrada_fijada IS NOT NULL
            AND TO_CHAR(t.entrada_fijada, 'HH24MI') >= '2200'
            -- Entrada MUY anticipada (>= 2 horas antes de horario)
            AND t.entrada IS NOT NULL
            AND t.entrada < t.entrada_fijada - (2/24)
            -- Faltan marcas de refrigerio
            AND t.inirefri IS NULL
            AND t.finrefri IS NULL
            AND t.salida IS NOT NULL
            AND NVL(t.descanso, 'N') <> 'S'
            AND NVL(t.ind_cerrado, 'N') <> 'S'
            AND t.num_fotocheck IS NOT NULL
        ) LOOP
            DBMS_OUTPUT.PUT_LINE('PASO 5G: Procesando fotocheck=' || rec.num_fotocheck || 
                                 ' entrada=' || TO_CHAR(rec.entrada, 'HH24:MI') ||
                                 ' entrada_fijada=' || TO_CHAR(rec.entrada_fijada, 'HH24:MI'));
            v_marca_inter := NULL;
            
            -- Buscar marca intermedia SOLO si el horario tiene refrigerio
            -- FIX 15/04/2026: Horarios con horiniref=00:00 (ej: VIGILANCIA) NO tienen refri
            -- No buscar marca intermedia para estos casos (evita asignar rondas como refri)
            IF rec.horiniref IS NOT NULL AND TO_CHAR(rec.horiniref, 'HH24:MI') <> '00:00' THEN
                -- Buscar marca intermedia (entre entrada y salida)
                -- Para nocturno, salida puede ser < entrada (ej: 07:00 < 19:00)
                BEGIN
                    SELECT marca INTO v_marca_inter FROM (
                        SELECT TO_DATE(TO_CHAR(h.fec_equiv, 'DD/MM/YYYY') || ' ' || RTRIM(h.hora), 'DD/MM/YYYY HH24:MI:SS') AS marca
                        FROM SCA_HISTORIAL h
                        WHERE h.idtarjeta = rec.num_fotocheck
                        AND h.fec_equiv = v_fecha_proceso
                        AND NVL(h.motivo, ' ') NOT LIKE 'DEPURACION%'
                        -- Marca despues de entrada (al menos 30 min)
                        AND TO_DATE(TO_CHAR(h.fec_equiv, 'DD/MM/YYYY') || ' ' || RTRIM(h.hora), 'DD/MM/YYYY HH24:MI:SS') 
                            > rec.entrada + (30/1440)
                        -- Marca antes de medianoche (para el dia actual del turno nocturno)
                        -- Las marcas de refrigerio en 3er turno anticipado estan entre ~20:00 y 23:59
                        AND TO_NUMBER(SUBSTR(RTRIM(h.hora), 1, 2)) >= 20
                        AND TO_NUMBER(SUBSTR(RTRIM(h.hora), 1, 2)) <= 23
                        ORDER BY h.hora
                    ) WHERE ROWNUM = 1;
                EXCEPTION WHEN NO_DATA_FOUND THEN
                    v_marca_inter := NULL;
                END;
            ELSE
                -- FIX 24/04/2026: horiniref=00:00 (horario sin refri teorico, ej: VIGILANCIA)
                -- REGLA: Nocturno con entrada anticipada SIEMPRE tiene refrigerio,
                -- aunque el horario teorico no lo defina. Se detecta por las marcas reales.
                --
                -- Variantes detectadas en la ventana (entrada+30min .. entrada_fijada):
                --  a) 1 marca   -> IR=marca, FR=marca+30min (insertar FR en SCA_HIS)
                --  b) 2 marcas  -> IR=marca1, FR=marca2 (par real, no insertar nada)
                --  c) >2 marcas -> ambiguo, no procesar (ELSE-no-mark)
                -- Comparacion por minutos del dia para evitar epoch mismatch.
                -- Casos: 23/04 (1 marca 20:06) y 21/04 (2 marcas 20:09 y 20:30) CHOCCARE 034161.
                v_marca_inter   := NULL;
                v_marca_inter_b := NULL;
                BEGIN
                    SELECT MIN(marca), MAX(marca)
                    INTO v_marca_inter, v_marca_inter_b
                    FROM (
                        SELECT TO_DATE(TO_CHAR(h.fec_equiv, 'DD/MM/YYYY') || ' ' || RTRIM(h.hora), 'DD/MM/YYYY HH24:MI:SS') AS marca,
                               COUNT(*) OVER () AS cnt
                        FROM SCA_HISTORIAL h
                        WHERE h.idtarjeta = rec.num_fotocheck
                        AND h.fec_equiv = v_fecha_proceso
                        AND NVL(h.ind_anulado, 'N') NOT IN ('A','S')
                        AND NVL(h.ind_noprocesar, 0) = 0
                        AND NVL(h.motivo, ' ') NOT LIKE 'DEPURACION%'
                        AND (TO_NUMBER(SUBSTR(RTRIM(h.hora),1,2))*60 + TO_NUMBER(SUBSTR(RTRIM(h.hora),4,2)))
                            > (TO_NUMBER(TO_CHAR(rec.entrada,'HH24'))*60 + TO_NUMBER(TO_CHAR(rec.entrada,'MI')) + 30)
                        AND (TO_NUMBER(SUBSTR(RTRIM(h.hora),1,2))*60 + TO_NUMBER(SUBSTR(RTRIM(h.hora),4,2)))
                            < (TO_NUMBER(TO_CHAR(rec.entrada_fijada,'HH24'))*60 + TO_NUMBER(TO_CHAR(rec.entrada_fijada,'MI')))
                    )
                    WHERE cnt IN (1, 2);
                EXCEPTION WHEN NO_DATA_FOUND THEN
                    v_marca_inter   := NULL;
                    v_marca_inter_b := NULL;
                END;
                -- Si solo se detecto 1 marca, MIN=MAX (mismo valor); usar +30 como FR
                -- Si se detectaron 2 marcas, MIN=IR y MAX=FR (par real)
                IF v_marca_inter IS NOT NULL AND v_marca_inter = v_marca_inter_b THEN
                    -- 1 marca: FR sera marca+30min, se insertara DEPURACION en SCA_HIS
                    v_marca_inter_b := NULL;
                END IF;
            END IF;
            
            IF v_marca_inter IS NOT NULL THEN
                IF v_marca_inter_b IS NOT NULL THEN
                    DBMS_OUTPUT.PUT_LINE('PASO 5G: Refri par real IR=' || TO_CHAR(v_marca_inter,'HH24:MI') ||
                                         ' FR=' || TO_CHAR(v_marca_inter_b,'HH24:MI'));
                ELSE
                    DBMS_OUTPUT.PUT_LINE('PASO 5G: Encontrada marca intermedia=' || TO_CHAR(v_marca_inter, 'HH24:MI'));
                END IF;
                
                -- Asignar como INIREFRI y calcular FINREFRI
                --  - Si hay segunda marca real (par): FR = v_marca_inter_b (NO insertar DEPURACION)
                --  - Si solo hay 1 marca: FR = IR + 30min (insertar DEPURACION en SCA_HIS)
                -- Tambien calcular horas extras antes (sobretiempo)
                UPDATE SCA_ASISTENCIA_TAREO
                SET inirefri = v_marca_inter,
                    finrefri = NVL(v_marca_inter_b, v_marca_inter + (30/1440)),
                    -- Guardar cuanto llego antes (para auditoria)
                    horaantesentrada = TO_DATE('01/01/1900', 'dd/MM/yyyy') + 
                                      (entrada_fijada - entrada),
                    -- HE antes: solo si llego 1+ hora antes, truncar a horas completas
                    horaextantes = CASE 
                        WHEN (entrada_fijada - entrada) * 24 >= 1
                        THEN TO_DATE('01/01/1900', 'dd/MM/yyyy') + 
                             TRUNC((entrada_fijada - entrada) * 24) / 24
                        ELSE TO_DATE('01/01/1900 00:00', 'dd/MM/yyyy HH24:MI')
                    END,
                    horaextantesofi = CASE 
                        WHEN (entrada_fijada - entrada) * 24 >= 1
                        THEN TO_DATE('01/01/1900', 'dd/MM/yyyy') + 
                             TRUNC((entrada_fijada - entrada) * 24) / 24
                        ELSE TO_DATE('01/01/1900 00:00', 'dd/MM/yyyy HH24:MI')
                    END,
                    codaux4 = CASE WHEN codaux4 IS NULL THEN c_R4 || c_SEP || c_R3 ELSE codaux4 || c_SEP || c_R4 || c_SEP || c_R3 END,
                    codaux5 = SUBSTR(CASE WHEN codaux5 IS NULL THEN d_R4 ELSE codaux5 || c_SEP || d_R4 END, 1, 50)
                WHERE ROWID = rec.rid;
                
                v_count_5g := v_count_5g + 1;
                v_count_inirefri := v_count_inirefri + 1;
                v_count_finrefri := v_count_finrefri + 1;
                
                -- Insertar FINREFRI calculado en SCA_HISTORIAL SOLO si fue calculado (+30min).
                -- Si v_marca_inter_b NOT NULL, FR es marca real existente, no insertar.
                IF v_marca_inter_b IS NULL THEN
                    INSERT INTO SCA_HISTORIAL (
                        IDCOD, IDLECTORA, IDTARJETA, FEC_EQUIV, FECHA, HORA, TIPOREG, FECREG,
                        IND_ANULADO, ORDEN, IND_CERRADO, INDREFRI, MOTIVO
                    ) VALUES (
                        id_cod_seq.NEXTVAL,
                        '00001',
                        rec.num_fotocheck,
                        v_fecha_proceso,
                        TO_CHAR(v_fecha_proceso, 'DD/MM/YYYY'),
                        TO_CHAR(v_marca_inter + (30/1440), 'HH24:MI:SS'),
                        '3',  -- Manual
                        SYSDATE,
                        'N',
                        4,
                        'N',
                        'S',
                        'DEPURACION: FinRefri 3er turno anticipado'
                    );
                    v_count_historial := v_count_historial + 1;
                END IF;
                
            ELSE
                -- FIX 15/04/2026: Calcular HE anticipadas + horas brutas/efectivas
                -- cuando no hay marca intermedia o el horario no tiene refrigerio
                -- (ej: VIGILANCIA 8h sin refri)
                -- PASO 5 NO procesara este registro (codaux4 termina en RC),
                -- por lo que calculamos AQUI tothoramarcas y horaefectiva.
                -- Nocturno: si salida < entrada (ej: 07:02 < 18:39), sumar 1 dia.
                UPDATE SCA_ASISTENCIA_TAREO
                SET horaantesentrada = TO_DATE('01/01/1900', 'dd/MM/yyyy') + 
                                      (entrada_fijada - entrada),
                    horaextantes = CASE 
                        WHEN (entrada_fijada - entrada) * 24 >= 1
                        THEN TO_DATE('01/01/1900', 'dd/MM/yyyy') + 
                             TRUNC((entrada_fijada - entrada) * 24) / 24
                        ELSE TO_DATE('01/01/1900 00:00', 'dd/MM/yyyy HH24:MI')
                    END,
                    horaextantesofi = CASE 
                        WHEN (entrada_fijada - entrada) * 24 >= 1
                        THEN TO_DATE('01/01/1900', 'dd/MM/yyyy') + 
                             TRUNC((entrada_fijada - entrada) * 24) / 24
                        ELSE TO_DATE('01/01/1900 00:00', 'dd/MM/yyyy HH24:MI')
                    END,
                    -- FIX: Horas brutas (nocturno: salida < entrada => +1 dia)
                    tothoramarcas = TO_DATE('01/01/1900', 'dd/MM/yyyy') + 
                        CASE WHEN salida < entrada 
                             THEN (salida + 1 - entrada)
                             ELSE (salida - entrada) 
                        END,
                    -- Sin refrigerio
                    horarefrigerio = TO_DATE('01/01/1900 00:00', 'dd/MM/yyyy HH24:MI'),
                    -- Horas efectivas = LEAST(brutas, tothoras) - cap a la jornada teorica
                    -- FIX 24/04/2026: anteriormente horaefectiva = brutas (12:28 para entrada
                    -- anticipada nocturna), ahora cap a tothoras=08:00.
                    horaefectiva = LEAST(
                        TO_DATE('01/01/1900', 'dd/MM/yyyy') + 
                            CASE WHEN salida < entrada 
                                 THEN (salida + 1 - entrada)
                                 ELSE (salida - entrada) 
                            END,
                        NVL(tothoras, TO_DATE('01/01/1900 23:59', 'dd/MM/yyyy HH24:MI'))
                    ),
                    -- Tardanza: solo si llego DESPUES de horario fijado
                    horatardanza = CASE 
                        WHEN entrada > entrada_fijada
                        THEN TO_DATE('01/01/1900', 'dd/MM/yyyy') + (entrada - entrada_fijada)
                        ELSE TO_DATE('01/01/1900 00:00', 'dd/MM/yyyy HH24:MI')
                    END,
                    -- FIX: nummarcaciones = campos tareo llenos (2 sin refri)
                    nummarcaciones = 
                        CASE WHEN entrada IS NOT NULL THEN 1 ELSE 0 END +
                        CASE WHEN inirefri IS NOT NULL THEN 1 ELSE 0 END +
                        CASE WHEN finrefri IS NOT NULL THEN 1 ELSE 0 END +
                        CASE WHEN salida IS NOT NULL THEN 1 ELSE 0 END,
                    alerta01 = NULL,  -- Par (2) = sin alerta
                    codaux4 = CASE WHEN codaux4 IS NULL THEN c_RC ELSE codaux4 || c_SEP || c_RC END,
                    codaux5 = SUBSTR(CASE WHEN codaux5 IS NULL THEN d_RC ELSE codaux5 || c_SEP || d_RC END, 1, 50)
                WHERE ROWID = rec.rid;
                
                v_count_5g := v_count_5g + 1;
                v_count_recalculo := v_count_recalculo + 1;
                DBMS_OUTPUT.PUT_LINE('PASO 5G: HE anticipadas + horas calculadas (sin refri) para fotocheck=' || rec.num_fotocheck ||
                                     ' anticipado=' || LTRIM(TO_CHAR((rec.entrada_fijada - rec.entrada) * 24, '00.00')) || 'h' ||
                                     ' brutas=' || TO_CHAR(
                                         TO_DATE('01/01/1900','dd/MM/yyyy') + 
                                         CASE WHEN rec.salida < rec.entrada THEN (rec.salida + 1 - rec.entrada) ELSE (rec.salida - rec.entrada) END,
                                         'HH24:MI'));
            END IF;
        END LOOP;
        
        IF v_count_5g > 0 THEN
            DBMS_OUTPUT.PUT_LINE('PASO 5G: 3er turno anticipado procesado -> ' || v_count_5g || ' registros');
        END IF;
        
        -- =====================================================================
        -- PASO 5: Recalcular horas
        -- SOLO para registros modificados por pasos anteriores (codaux4 NOT NULL)
        -- y que NO terminan ya en RC (evitar recalculos duplicados)
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t
        SET 
            t.tothoramarcas = CASE 
                WHEN t.entrada IS NOT NULL AND t.salida IS NOT NULL
                THEN TO_DATE('01/01/1900', 'dd/MM/yyyy') +
                     CASE WHEN t.salida < t.entrada
                          THEN (t.salida + 1 - t.entrada)  -- Nocturno: salida amanecer < entrada noche
                          ELSE (t.salida - t.entrada)
                     END
                ELSE t.tothoramarcas
            END,
            t.horarefrigerio = CASE 
                WHEN t.inirefri IS NOT NULL AND t.finrefri IS NOT NULL
                THEN TO_DATE('01/01/1900', 'dd/MM/yyyy') + (t.finrefri - t.inirefri)
                -- FIX 20/04/2026: si inirefri fue limpiado (ej: PASO 3E), horarefrigerio
                -- debe resetarse a 00:00 (no dejar el valor calculado por Aquarius con
                -- el refri imposible). Cuando inirefri=NULL desde el origen, horarefrigerio
                -- del tareo ya es 00:00, asi que este reset es seguro en ambos casos.
                WHEN t.inirefri IS NULL OR t.finrefri IS NULL
                THEN TO_DATE('01/01/1900 00:00', 'dd/MM/yyyy HH24:MI')
                ELSE t.horarefrigerio
            END,
            t.horaefectiva = CASE 
                WHEN t.entrada IS NOT NULL AND t.salida IS NOT NULL
                THEN
                    -- FIX 20/04/2026: usar LEAST(gross_actual, tothoras)
                    -- Anterior: siempre tothoras cuando existe -> 08:00 aunque solo se
                    -- trabajaron 01:55 (ej: ARREDONDO 032933, llego 6h tarde, refri limpiado
                    -- por PASO 3E -> PASO 5 seguia poniendo 08:00 = incorrecto)
                    -- Correcto: si el empleado no completo la jornada, usar tiempo real.
                    -- horaefectiva = GROSS (no deduce refri, igual al comportamiento Aquarius
                    -- para dias completos donde horaefectiva = tothoras = salida-entrada).
                    -- Verificado con otros dias de la grilla: H.Efe siempre = tothoras en
                    -- dias completos (refri se trackea en campo separado horarefrigerio).
                    LEAST(
                        -- Horas brutas reales (con ajuste nocturno)
                        TO_DATE('01/01/1900', 'dd/MM/yyyy') + 
                            CASE WHEN t.salida < t.entrada
                                 THEN (t.salida + 1 - t.entrada)
                                 ELSE (t.salida - t.entrada)
                            END,
                        -- Cap: horas teoricas del horario (no exceder jornada)
                        -- Si tothoras no existe, usar valor muy alto (sin cap)
                        NVL(t.tothoras, TO_DATE('01/01/1900 23:59', 'dd/MM/yyyy HH24:MI'))
                    )
                ELSE t.horaefectiva
            END,
            t.horatardanza = CASE 
                WHEN t.entrada IS NOT NULL 
                     AND t.entrada_fijada IS NOT NULL
                     AND t.entrada > t.entrada_fijada
                THEN TO_DATE('01/01/1900', 'dd/MM/yyyy') + (t.entrada - t.entrada_fijada)
                ELSE TO_DATE('01/01/1900 00:00', 'dd/MM/yyyy HH24:MI')
            END,
            -- TOTHORANOCTURNA: Interseccion de (entrada,salida) con ventana nocturna
            -- Replica PASO 12 de sp_SCA_Proceso_Total (tramo del dia)
            t.tothoranocturna = CASE 
                WHEN t.horinihornoc IS NOT NULL AND t.horfinhornoc IS NOT NULL THEN
                    NVL(
                        CASE 
                            WHEN t.entrada <= t.horinihornoc THEN
                                CASE 
                                    WHEN t.salida > t.horinihornoc AND t.salida < t.horfinhornoc
                                    THEN TO_DATE('01/01/1900', 'dd/MM/yyyy') + (t.salida - t.horinihornoc)
                                    WHEN t.salida >= t.horfinhornoc
                                    THEN TO_DATE('01/01/1900', 'dd/MM/yyyy') + (t.horfinhornoc - t.horinihornoc)
                                END
                            WHEN t.entrada > t.horinihornoc AND t.entrada < t.horfinhornoc THEN
                                CASE 
                                    WHEN t.salida <= t.horfinhornoc
                                    THEN TO_DATE('01/01/1900', 'dd/MM/yyyy') + (t.salida - t.entrada)
                                    WHEN t.salida > t.horfinhornoc
                                    THEN TO_DATE('01/01/1900', 'dd/MM/yyyy') + (t.horfinhornoc - t.entrada)
                                END
                        END,
                        TO_DATE('01/01/1900', 'dd/MM/yyyy')
                    )
                ELSE t.tothoranocturna
            END,
            -- TOTHORANOCTURNA_OF: Se calcula en UPDATE aparte (necesita el NUEVO valor de tothoranocturna)
            t.tothoranocturna_of = NULL,
            t.codaux4 = CASE WHEN t.codaux4 IS NULL THEN c_RC ELSE t.codaux4 || c_SEP || c_RC END,
            t.codaux5 = SUBSTR(CASE WHEN t.codaux5 IS NULL THEN d_RC ELSE t.codaux5 || c_SEP || d_RC END, 1, 50)
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
        AND t.entrada IS NOT NULL
        AND t.salida IS NOT NULL
        AND NVL(t.descanso, 'N') <> 'S'
        AND NVL(t.ind_cerrado, 'N') <> 'S'
        -- Solo registros que fueron modificados por pasos anteriores (0-4B)
        -- y que no terminan ya en RC (evita R1|RC|RC en segunda ejecucion)
        AND t.codaux4 IS NOT NULL
        AND t.codaux4 NOT LIKE '%' || c_RC;
        
        v_count_recalculo := SQL%ROWCOUNT;
        
        -- =====================================================================
        -- PASO 5A: Hora nocturna oficial con redondeo
        -- Replica PASO 12 de sp_SCA_Proceso_Total (hora nocturna oficial)
        -- Requiere que tothoranocturna ya haya sido actualizada en PASO 5
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t
        SET t.tothoranocturna_of = CASE 
                WHEN (NVL(TO_NUMBER(TO_CHAR(t.tothoranocturna, 'HH24'))*60, 0)
                    + NVL(TO_NUMBER(TO_CHAR(t.tothoranocturna, 'MI')), 0)
                     ) >= t.ajuste_tothoranocturna
                THEN 
                    CASE 
                        WHEN MOD(
                            NVL(TO_NUMBER(TO_CHAR(t.tothoranocturna, 'HH24'))*60, 0)
                          + NVL(TO_NUMBER(TO_CHAR(t.tothoranocturna, 'MI')), 0),
                            t.redondeo_tothoranocturna
                        ) = 0
                        THEN t.tothoranocturna
                        ELSE t.tothoranocturna - (
                            MOD(
                                NVL(TO_NUMBER(TO_CHAR(t.tothoranocturna, 'HH24'))*60, 0)
                              + NVL(TO_NUMBER(TO_CHAR(t.tothoranocturna, 'MI')), 0),
                                t.redondeo_tothoranocturna
                            ) / (24*60)
                        )
                    END
                ELSE NULL
            END
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
        AND t.tothoranocturna IS NOT NULL
        AND t.tothoranocturna > TO_DATE('01/01/1900', 'dd/MM/yyyy')
        AND t.ajuste_tothoranocturna IS NOT NULL
        AND t.redondeo_tothoranocturna IS NOT NULL
        AND t.redondeo_tothoranocturna > 0
        AND NVL(t.descanso, 'N') <> 'S'
        AND NVL(t.ind_cerrado, 'N') <> 'S';
        
        -- =====================================================================
        -- PASO 5B-TAG: Marcar registros con hora extra incorrecta (< 1 hora)
        -- El tareo calcula horaextra para cualquier tiempo despues de salida,
        -- pero la regla de negocio dice que solo aplica si >= 1 hora.
        -- Este paso MARCA el registro con codaux4='HE' para que PASO 5B
        -- (que ya tiene la logica correcta) lo procese y limpie.
        -- Se ejecuta DESPUES de PASO 5A y ANTES de PASO 5B.
        -- NO requiere codaux4 previo (aplica a registros del tareo original).
        --
        -- TABLAS ESCRITURA: SCA_ASISTENCIA_TAREO (codaux4, codaux5)
        -- TABLAS CONSULTA:  (ninguna adicional)
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t
        SET t.codaux4 = CASE WHEN t.codaux4 IS NULL THEN c_HE ELSE t.codaux4 || c_SEP || c_HE END,
            t.codaux5 = SUBSTR(CASE WHEN t.codaux5 IS NULL THEN d_HE ELSE t.codaux5 || c_SEP || d_HE END, 1, 50)
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
        AND t.salida IS NOT NULL
        AND t.salida_fijada IS NOT NULL
        AND t.salida > t.salida_fijada
        AND (t.salida - t.salida_fijada) * 24 < 1             -- < 1 hora despues de salida
        AND t.horaextra IS NOT NULL
        AND t.horaextra > TO_DATE('01/01/1900', 'dd/MM/yyyy') -- Tiene hora extra calculada
        AND NVL(t.descanso, 'N') <> 'S'
        AND NVL(t.ind_cerrado, 'N') <> 'S'
        AND NVL(t.hayhed_poraut, 'N') <> 'S';
        
        -- =====================================================================
        -- PASO 5B-TAG2: Marcar registros con HE no oficializada por el tareo
        -- BUG detectado 27/04/2026 (caso WILSON MELENDEZ 21/04/2026):
        -- El tareo original calculo correctamente t.horaextra (ej: 04:08)
        -- pero NO oficializo el ajuste: t.horaextra_ajus quedo en NULL/0,
        -- y por consecuencia t.horaexofi1/2/3 quedaron en 0 -> reporte = 00:00
        -- Causa raiz observada: cuando el dia tiene horas_no_trabajadas > 0
        -- (refrigerio anomalo) el tareo omite la oficializacion.
        -- 
        -- Accion: Marcar codaux4='HX' para que PASO 5B-2 reoficialice
        -- horaextra_ajus + horaexofi1/2/3 con la logica correcta.
        -- 
        -- TABLAS ESCRITURA: SCA_ASISTENCIA_TAREO (codaux4, codaux5)
        -- TABLAS CONSULTA:  (ninguna adicional)
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t
        SET t.codaux4 = CASE WHEN t.codaux4 IS NULL THEN c_HX ELSE t.codaux4 || c_SEP || c_HX END,
            t.codaux5 = SUBSTR(CASE WHEN t.codaux5 IS NULL THEN d_HX ELSE t.codaux5 || c_SEP || d_HX END, 1, 50)
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
        AND t.entrada IS NOT NULL
        AND t.salida IS NOT NULL
        AND t.salida_fijada IS NOT NULL
        AND t.horaextra IS NOT NULL
        AND t.horaextra >= TO_DATE('01/01/1900 01:00','dd/MM/yyyy HH24:MI')   -- HE >= 1 hora
        AND (t.horaextra_ajus IS NULL
             OR t.horaextra_ajus = TO_DATE('01/01/1900','dd/MM/yyyy'))         -- ajus NO oficializado
        AND NVL(t.descanso, 'N') <> 'S'
        AND NVL(t.ind_cerrado, 'N') <> 'S'
        AND NVL(t.hayhed_poraut, 'N') <> 'S';
        
        -- =====================================================================
        -- PASO 5B: Recalcular horas extras (despues de salida)
        -- Regla: Solo aplica si excede 1 hora despues del horario de salida
        -- Se trunca a horas completas (no se pagan minutos)
        -- Ejemplo: Sale 1h30m despues -> 1 hora extra
        --          Sale 45min despues -> 0 horas extras
        -- NOTA: HORAEXTANTES (por entrada anticipada) ya fue calculada en PASO 1B
        --       TOTALHORASEXTRAS = HORAEXTRA + HORAEXTANTES
        -- FIX 21/04/2026: Ajuste nocturno de salida_fijada
        -- Aquarius almacena salida_fijada en la misma fecha que fechamar (ej: 17/04 03:00)
        -- para turnos que cruzan medianoche (ej: HORARIO 19:00-03:00).
        -- Cuando la salida real queda en el dia siguiente (ej: 18/04 07:03),
        -- la diferencia (salida - salida_fijada) daria ~28h en lugar de ~4h.
        -- Deteccion: salida_fijada < entrada_fijada (DATE comparison, mismo dia).
        -- Fix inline: usar salida_fijada + 1 en ese caso.
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t
        SET 
            t.horaextra = CASE 
                WHEN t.salida > CASE WHEN t.entrada_fijada IS NOT NULL AND t.salida_fijada < t.entrada_fijada THEN t.salida_fijada + 1 ELSE t.salida_fijada END
                     AND (t.salida - CASE WHEN t.entrada_fijada IS NOT NULL AND t.salida_fijada < t.entrada_fijada THEN t.salida_fijada + 1 ELSE t.salida_fijada END) * 24 >= 1
                THEN TO_DATE('01/01/1900', 'dd/MM/yyyy') + 
                     TRUNC((t.salida - CASE WHEN t.entrada_fijada IS NOT NULL AND t.salida_fijada < t.entrada_fijada THEN t.salida_fijada + 1 ELSE t.salida_fijada END) * 24) / 24
                ELSE TO_DATE('01/01/1900 00:00', 'dd/MM/yyyy HH24:MI')
            END,
            t.totalhorasextras = CASE 
                WHEN t.salida > CASE WHEN t.entrada_fijada IS NOT NULL AND t.salida_fijada < t.entrada_fijada THEN t.salida_fijada + 1 ELSE t.salida_fijada END
                     AND (t.salida - CASE WHEN t.entrada_fijada IS NOT NULL AND t.salida_fijada < t.entrada_fijada THEN t.salida_fijada + 1 ELSE t.salida_fijada END) * 24 >= 1
                THEN TO_DATE('01/01/1900', 'dd/MM/yyyy') + 
                     TRUNC((t.salida - CASE WHEN t.entrada_fijada IS NOT NULL AND t.salida_fijada < t.entrada_fijada THEN t.salida_fijada + 1 ELSE t.salida_fijada END) * 24) / 24 +
                     NVL(t.horaextantes - TO_DATE('01/01/1900', 'dd/MM/yyyy'), 0)
                ELSE TO_DATE('01/01/1900', 'dd/MM/yyyy') +
                     NVL(t.horaextantes - TO_DATE('01/01/1900', 'dd/MM/yyyy'), 0)
            END
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
        AND t.entrada IS NOT NULL
        AND t.salida IS NOT NULL
        AND t.salida_fijada IS NOT NULL
        AND NVL(t.descanso, 'N') <> 'S'
        AND NVL(t.ind_cerrado, 'N') <> 'S'
        -- No modificar si tiene HE despues autorizadas
        AND NVL(t.hayhed_poraut, 'N') <> 'S'
        -- Solo registros modificados por depuracion (no sobreescribir calculo del tareo)
        AND t.codaux4 IS NOT NULL;
        
        -- =====================================================================
        -- PASO 5B-2: Recalcular campos derivados de horas extras
        -- El tareo original calcula una cadena de campos a partir de horaextra:
        --   horadespuessalida -> horaextraofi -> totalhorasextrasofi ->
        --   horaextra_ajus -> alerta06 -> horaextra1/2/3 -> horaexofi1/2/3
        -- Nuestro PKG actualizo horaextra/totalhorasextras (PASO 5B) pero
        -- NO actualizo estos campos derivados. Los reportes leen de
        -- horaextra1/2/3 (H25%, H35%, Dob), que quedaron con valores viejos.
        -- 
        -- TABLAS ESCRITURA: SCA_ASISTENCIA_TAREO
        -- TABLAS CONSULTA:  (ninguna adicional, usa campos del mismo registro)
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t
        SET 
            -- HORADESPUESSALIDA: tiempo total despues de salida (con minutos)
            -- FIX 21/04/2026: mismo ajuste nocturno que PASO 5B
            t.horadespuessalida = CASE 
                WHEN t.salida > CASE WHEN t.entrada_fijada IS NOT NULL AND t.salida_fijada < t.entrada_fijada THEN t.salida_fijada + 1 ELSE t.salida_fijada END
                THEN TO_DATE('01/01/1900', 'dd/MM/yyyy') + (t.salida - CASE WHEN t.entrada_fijada IS NOT NULL AND t.salida_fijada < t.entrada_fijada THEN t.salida_fijada + 1 ELSE t.salida_fijada END)
                ELSE NULL
            END,
            -- HORAEXTRAOFI: simplificado (sin TT_SCA_TRAMOS2)
            t.horaextraofi = t.horaextra,
            -- TOTALHORASEXTRASOFI: suma de oficiales
            -- Nota: horaextraofi aun no se escribe, usar t.horaextra inline
            t.totalhorasextrasofi = TO_DATE('01/01/1900', 'dd/MM/yyyy') + 
                NVL(t.horaextantesofi - TO_DATE('01/01/1900', 'dd/MM/yyyy'), 0) +
                NVL(t.horaextra - TO_DATE('01/01/1900', 'dd/MM/yyyy'), 0),
            -- HORAEXTRA_AJUS: total oficial redondeado por ajuste_hextra
            t.horaextra_ajus = CASE 
                WHEN t.ajuste_hextra IS NOT NULL AND t.ajuste_hextra > 0 THEN
                    CASE 
                        WHEN MOD(
                            NVL(TO_NUMBER(TO_CHAR(t.horaextantesofi, 'HH24'))*60, 0) 
                          + NVL(TO_NUMBER(TO_CHAR(t.horaextantesofi, 'MI')), 0)
                          + NVL(TO_NUMBER(TO_CHAR(t.horaextra, 'HH24'))*60, 0) 
                          + NVL(TO_NUMBER(TO_CHAR(t.horaextra, 'MI')), 0),
                            t.ajuste_hextra
                        ) = 0
                        THEN TO_DATE('01/01/1900', 'dd/MM/yyyy') + 
                            NVL(t.horaextantesofi - TO_DATE('01/01/1900', 'dd/MM/yyyy'), 0) +
                            NVL(t.horaextra - TO_DATE('01/01/1900', 'dd/MM/yyyy'), 0)
                        ELSE TO_DATE('01/01/1900', 'dd/MM/yyyy') + 
                            NVL(t.horaextantesofi - TO_DATE('01/01/1900', 'dd/MM/yyyy'), 0) +
                            NVL(t.horaextra - TO_DATE('01/01/1900', 'dd/MM/yyyy'), 0) -
                            MOD(
                                NVL(TO_NUMBER(TO_CHAR(t.horaextantesofi, 'HH24'))*60, 0) 
                              + NVL(TO_NUMBER(TO_CHAR(t.horaextantesofi, 'MI')), 0)
                              + NVL(TO_NUMBER(TO_CHAR(t.horaextra, 'HH24'))*60, 0) 
                              + NVL(TO_NUMBER(TO_CHAR(t.horaextra, 'MI')), 0),
                                t.ajuste_hextra
                            ) / (24*60)
                    END
                ELSE TO_DATE('01/01/1900', 'dd/MM/yyyy') + 
                    NVL(t.horaextantesofi - TO_DATE('01/01/1900', 'dd/MM/yyyy'), 0) +
                    NVL(t.horaextra - TO_DATE('01/01/1900', 'dd/MM/yyyy'), 0)
            END,
            -- ALERTA06: indicador de horas extras (EN=normal, EE=excede razonabilidad)
            t.alerta06 = CASE 
                WHEN t.tippagohe = '1'
                     AND (NVL(t.horaextantesofi, TO_DATE('01/01/1900', 'dd/MM/yyyy')) > TO_DATE('01/01/1900', 'dd/MM/yyyy')
                       OR NVL(t.horaextra, TO_DATE('01/01/1900', 'dd/MM/yyyy')) > TO_DATE('01/01/1900', 'dd/MM/yyyy'))
                THEN 'EN'
                WHEN t.tippagohe = '1'
                THEN NULL   -- Tiene config HE pero no tiene extras: limpiar alerta
                ELSE t.alerta06
            END
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
        AND t.entrada IS NOT NULL
        AND t.salida IS NOT NULL
        AND t.salida_fijada IS NOT NULL
        AND NVL(t.descanso, 'N') <> 'S'
        AND NVL(t.ind_cerrado, 'N') <> 'S'
        AND NVL(t.hayhed_poraut, 'N') <> 'S'
        AND t.codaux4 IS NOT NULL;
        
        -- =====================================================================
        -- PASO 5B-3: Actualizar alerta06 a 'EE' si excede razonabilidad
        -- Replica logica del tareo: si horaextra_ajus >= min_min_raz_hextra
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t
        SET t.alerta06 = 'EE'
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
        AND t.alerta06 = 'EN'
        AND t.horaextra_ajus IS NOT NULL
        AND t.min_min_raz_hextra IS NOT NULL
        AND (TO_NUMBER(TO_CHAR(t.horaextra_ajus, 'HH24'))*60 
           + TO_NUMBER(TO_CHAR(t.horaextra_ajus, 'MI'))) >= t.min_min_raz_hextra
        AND t.codaux4 IS NOT NULL;
        
        -- =====================================================================
        -- PASO 5B-4: Recalcular rangos de horas extras (H25%, H35%, H50%)
        -- Replica logica del tareo:
        --   horaextra1 = MIN(totalhorasextras, H25F)     -> H. extras 25%
        --   horaextra2 = totalhorasextras - H25F          -> H. extras 35%
        --     (limitado al rango H35I..H35F)
        --   horaextra3 = totalhorasextras - H35F          -> H. extras 50%
        --     (solo si totalhorasextras > HNI)
        -- Tambien los oficiales (horaexofi1/2/3) basados en horaextra_ajus
        -- 
        -- TABLAS ESCRITURA: SCA_ASISTENCIA_TAREO
        -- TABLAS CONSULTA:  (ninguna adicional)
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t
        SET 
            -- HORAEXTRA1 (H25%): min(totalhorasextras, H25F)
            t.horaextra1 = CASE 
                WHEN t.totalhorasextras IS NOT NULL 
                     AND t.totalhorasextras > TO_DATE('01/01/1900', 'dd/MM/yyyy')
                THEN CASE 
                    WHEN t.h25f IS NOT NULL AND t.totalhorasextras > t.h25f
                    THEN t.h25f
                    ELSE t.totalhorasextras
                END
                ELSE NULL
            END,
            -- HORAEXTRA2 (H35%): totalhorasextras - H25F (dentro del rango H35I..H35F)
            t.horaextra2 = CASE 
                WHEN t.totalhorasextras IS NOT NULL 
                     AND t.totalhorasextras > TO_DATE('01/01/1900', 'dd/MM/yyyy')
                     AND t.h35i IS NOT NULL
                     AND t.totalhorasextras > t.h35i
                THEN CASE 
                    WHEN t.h35f IS NOT NULL AND t.totalhorasextras > t.h35f
                    -- Excede H35F: tomar rango completo H35F - H35I
                    THEN TO_DATE('01/01/1900', 'dd/MM/yyyy') + (t.h35f - t.h35i)
                    -- Dentro del rango: totalhorasextras - H25F
                    ELSE TO_DATE('01/01/1900', 'dd/MM/yyyy') + 
                         (t.totalhorasextras - NVL(t.h25f, TO_DATE('01/01/1900', 'dd/MM/yyyy')))
                END
                ELSE NULL
            END,
            -- HORAEXTRA3 (H50%/Doble): totalhorasextras - H35F (solo si > HNI)
            t.horaextra3 = CASE 
                WHEN t.totalhorasextras IS NOT NULL 
                     AND t.totalhorasextras > TO_DATE('01/01/1900', 'dd/MM/yyyy')
                     AND t.hni IS NOT NULL
                     AND t.totalhorasextras > t.hni
                THEN TO_DATE('01/01/1900', 'dd/MM/yyyy') + 
                     (t.totalhorasextras - NVL(t.h35f, TO_DATE('01/01/1900', 'dd/MM/yyyy')))
                ELSE NULL
            END,
            -- HORAEXOFI1 (H25% oficial): basado en horaextra_ajus
            t.horaexofi1 = CASE 
                WHEN t.tippagohe = '1'
                     AND t.horaextra_ajus IS NOT NULL 
                     AND t.horaextra_ajus > TO_DATE('01/01/1900', 'dd/MM/yyyy')
                THEN CASE 
                    WHEN t.h25f IS NOT NULL AND t.horaextra_ajus > t.h25f
                    THEN t.h25f
                    ELSE t.horaextra_ajus
                END
                ELSE NULL
            END,
            -- HORAEXOFI2 (H35% oficial): basado en horaextra_ajus
            t.horaexofi2 = CASE 
                WHEN t.tippagohe = '1'
                     AND t.horaextra_ajus IS NOT NULL 
                     AND t.horaextra_ajus > TO_DATE('01/01/1900', 'dd/MM/yyyy')
                     AND t.h35i IS NOT NULL
                     AND t.horaextra_ajus > t.h35i
                THEN CASE 
                    WHEN t.h35f IS NOT NULL AND t.horaextra_ajus > t.h35f
                    THEN TO_DATE('01/01/1900', 'dd/MM/yyyy') + (t.h35f - t.h35i)
                    ELSE TO_DATE('01/01/1900', 'dd/MM/yyyy') + 
                         (t.horaextra_ajus - NVL(t.h25f, TO_DATE('01/01/1900', 'dd/MM/yyyy')))
                END
                ELSE NULL
            END,
            -- HORAEXOFI3 (H50% oficial): basado en horaextra_ajus
            t.horaexofi3 = CASE 
                WHEN t.tippagohe = '1'
                     AND t.horaextra_ajus IS NOT NULL 
                     AND t.horaextra_ajus > TO_DATE('01/01/1900', 'dd/MM/yyyy')
                     AND t.hni IS NOT NULL
                     AND t.horaextra_ajus > t.hni
                THEN TO_DATE('01/01/1900', 'dd/MM/yyyy') + 
                     (t.horaextra_ajus - NVL(t.h35f, TO_DATE('01/01/1900', 'dd/MM/yyyy')))
                ELSE NULL
            END
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
        AND (t.alerta06 = 'EN' OR t.alerta06 = 'EE')
        AND NVL(t.descanso, 'N') <> 'S'
        AND NVL(t.ind_cerrado, 'N') <> 'S'
        AND t.codaux4 IS NOT NULL;
        
        -- =====================================================================
        -- PASO 5C: Recontar NUMMARCACIONES segun marcas reales al final
        -- El numero de marcaciones debe reflejar las marcas que existen
        -- despues de todo el proceso de depuracion
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t
        SET t.nummarcaciones = 
            CASE WHEN t.entrada IS NOT NULL THEN 1 ELSE 0 END +
            CASE WHEN t.inirefri IS NOT NULL THEN 1 ELSE 0 END +
            CASE WHEN t.finrefri IS NOT NULL THEN 1 ELSE 0 END +
            CASE WHEN t.salida IS NOT NULL THEN 1 ELSE 0 END
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
        AND NVL(t.descanso, 'N') <> 'S'
        AND NVL(t.ind_cerrado, 'N') <> 'S'
        AND t.codaux4 IS NOT NULL;  -- Solo registros modificados por depuracion
        
        -- =====================================================================
        -- PASO 5D: Limpiar alerta01='MI' cuando la depuracion completo todas
        -- las marcaciones. El tareo original pone MI cuando el historial tiene
        -- numero impar de marcas. Si el depura resolvio el caso (4 marcaciones
        -- en tareo), se limpia la alerta para que no siga mostrando
        -- "MARCACION IMPAR" en el sistema.
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t
        SET t.alerta01 = NULL
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
        AND t.alerta01 = 'MI'
        AND t.entrada IS NOT NULL
        AND t.salida IS NOT NULL
        -- Para turnos normales: 4 marcaciones (con refrigerio)
        -- Para turnos nocturnos: 2 marcaciones (sin refrigerio)
        AND (
            (t.inirefri IS NOT NULL AND t.finrefri IS NOT NULL)  -- Tiene refrigerio completo
            OR (t.entrada_fijada IS NOT NULL AND TO_CHAR(t.entrada_fijada, 'HH24MI') >= '2000')  -- Turno nocturno (no necesita refrigerio)
        )
        AND t.codaux4 IS NOT NULL;  -- Solo registros modificados por depuracion
        
        -- =====================================================================
        -- PASO 5E: Recalcular horas_no_trabajadas y limpiar alerta03='HI'
        -- El tareo original calculo horas_no_trabajadas con refrigerio erroneo.
        -- Ejemplo: Refri=1min -> H.Efect baja -> horas_no_trabajadas=00:55
        --          -> alerta03='HI' -> "Horario Incompleto" en reporte
        -- Despues de corregir refrigerio: si el tiempo neto trabajado 
        -- (salida - entrada - refrigerio) >= tothoras -> no hay horas faltantes
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t
        SET t.horas_no_trabajadas = NULL,
            t.alerta03 = CASE 
                WHEN t.alerta03 = 'HI' THEN NULL 
                ELSE t.alerta03 
            END
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
        AND t.codaux4 IS NOT NULL
        AND t.codaux4 LIKE '%' || c_RC || '%'
        AND t.entrada IS NOT NULL
        AND t.salida IS NOT NULL
        AND t.tothoras IS NOT NULL
        AND t.tothoras > TO_DATE('01/01/1900', 'dd/MM/yyyy')
        -- Tiempo neto trabajado (salida - entrada - refrigerio) >= horas teoricas
        AND (t.salida - t.entrada - NVL(t.finrefri - t.inirefri, 0))
            >= (t.tothoras - TO_DATE('01/01/1900', 'dd/MM/yyyy'))
        AND (t.horas_no_trabajadas IS NOT NULL OR t.alerta03 = 'HI')
        AND NVL(t.descanso, 'N') <> 'S'
        AND NVL(t.ind_cerrado, 'N') <> 'S';
        
        IF SQL%ROWCOUNT > 0 THEN
            DBMS_OUTPUT.PUT_LINE('PASO 5E: Limpiado horas_no_trabajadas/alerta03 -> ' || SQL%ROWCOUNT || ' registros (Horario Incompleto resuelto)');
        END IF;
        
        -- =====================================================================
        -- PASO 5F: Eliminar marcas phantom/duplicadas de SCA_HISTORIAL
        -- Algunos dispositivos registran la marca 2 veces (ej: 13:30 y 13:31)
        -- El tareo toma ambas como marcas validas pero solo una es real.
        -- Despues de depurar y asignar los 4 campos (E/IR/FR/S), eliminar
        -- marcas que NO corresponden a ninguno de esos 4 campos Y estan
        -- dentro de 3 minutos de otra marca (patron de duplicado de lector).
        -- =====================================================================
        DELETE FROM SCA_HISTORIAL h
        WHERE h.fec_equiv = v_fecha_proceso
        AND NVL(h.motivo, ' ') NOT LIKE 'DEPURACION%'
        AND NVL(h.ind_anulado, 'N') <> 'S'
        AND h.idtarjeta IN (
            SELECT t.num_fotocheck
            FROM SCA_ASISTENCIA_TAREO t
            WHERE t.fechamar = v_fecha_proceso
            AND t.cod_empresa LIKE v_empresa_filtro
            AND t.cod_personal LIKE v_personal_filtro
            AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
            AND t.codaux4 IS NOT NULL
            AND t.num_fotocheck IS NOT NULL
        )
        -- La marca NO corresponde a ninguno de los 4 campos del tareo
        AND NOT EXISTS (
            SELECT 1 FROM SCA_ASISTENCIA_TAREO t
            WHERE t.num_fotocheck = h.idtarjeta
            AND t.fechamar = h.fec_equiv
            AND t.codaux4 IS NOT NULL
            AND (
                RTRIM(h.hora) = TO_CHAR(t.entrada, 'HH24:MI:SS')
                OR RTRIM(h.hora) = TO_CHAR(t.inirefri, 'HH24:MI:SS')
                OR RTRIM(h.hora) = TO_CHAR(t.finrefri, 'HH24:MI:SS')
                OR RTRIM(h.hora) = TO_CHAR(t.salida, 'HH24:MI:SS')
            )
        )
        -- Y esta dentro de 3 minutos de otra marca (patron duplicado de lector)
        AND EXISTS (
            SELECT 1 FROM SCA_HISTORIAL h2
            WHERE h2.idtarjeta = h.idtarjeta
            AND h2.fec_equiv = h.fec_equiv
            AND h2.idcod <> h.idcod
            AND NVL(h2.ind_anulado, 'N') <> 'S'
            AND ABS(
                TO_DATE(TO_CHAR(h2.fec_equiv, 'DD/MM/YYYY') || ' ' || RTRIM(h2.hora), 'DD/MM/YYYY HH24:MI:SS')
                - TO_DATE(TO_CHAR(h.fec_equiv, 'DD/MM/YYYY') || ' ' || RTRIM(h.hora), 'DD/MM/YYYY HH24:MI:SS')
            ) <= 3/1440  -- 3 minutos
        );
        
        IF SQL%ROWCOUNT > 0 THEN
            DBMS_OUTPUT.PUT_LINE('PASO 5F: Eliminadas ' || SQL%ROWCOUNT || ' marcas phantom/duplicadas de SCA_HISTORIAL');
        END IF;
        
        -- =====================================================================
        -- PASO 7A: Ajustar entrada anticipada en dia de descanso (CASO 8B)
        -- En descanso, entrada_fijada es NULL porque el horario tiene 00:00.
        -- Se busca la hora de ingreso normal del turno (dia no-descanso del
        -- mismo HORID en SCA_HORARIO_DET) y se aplica la regla de -15 min.
        -- Si llego MAS DE 15 MIN antes del horario normal, ajustar entrada.
        -- Se registra horaantesentrada para auditoria.
        -- Se ejecuta ANTES de PASO 7 para que brutas/dobles se calculen bien.
        --
        -- TABLAS ESCRITURA: SCA_ASISTENCIA_TAREO
        -- TABLAS CONSULTA:  SCA_HORARIO_DET
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t
        SET 
            -- Llenar entrada_fijada con la hora del turno normal
            t.entrada_fijada = TRUNC(t.entrada) + (
                SELECT MIN(d.horing) - TO_DATE('01/01/1900', 'dd/MM/yyyy')
                FROM SCA_HORARIO_DET d 
                WHERE d.horid = t.horid 
                AND NVL(d.descanso, 'N') <> 'S'
                AND d.horing > TO_DATE('01/01/1900', 'dd/MM/yyyy')
            ),
            -- Guardar cuanto llego antes (horario_normal - entrada_real)
            t.horaantesentrada = TO_DATE('01/01/1900', 'dd/MM/yyyy') + (
                TRUNC(t.entrada) + (
                    SELECT MIN(d.horing) - TO_DATE('01/01/1900', 'dd/MM/yyyy')
                    FROM SCA_HORARIO_DET d 
                    WHERE d.horid = t.horid 
                    AND NVL(d.descanso, 'N') <> 'S'
                    AND d.horing > TO_DATE('01/01/1900', 'dd/MM/yyyy')
                ) - t.entrada
            ),
            -- Ajustar entrada a horario_normal - 15 min
            t.entrada = TRUNC(t.entrada) + (
                SELECT MIN(d.horing) - TO_DATE('01/01/1900', 'dd/MM/yyyy')
                FROM SCA_HORARIO_DET d 
                WHERE d.horid = t.horid 
                AND NVL(d.descanso, 'N') <> 'S'
                AND d.horing > TO_DATE('01/01/1900', 'dd/MM/yyyy')
            ) - 15/1440,
            t.codaux4 = CASE WHEN t.codaux4 IS NULL THEN c_E2 ELSE t.codaux4 || c_SEP || c_E2 END,
            t.codaux5 = SUBSTR(CASE WHEN t.codaux5 IS NULL THEN d_E2 ELSE t.codaux5 || c_SEP || d_E2 END, 1, 50)
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
        -- Descanso: tareo con flag 'S' o el horario del dia (DIAID) marcado como descanso
        AND (t.descanso = 'S' OR EXISTS (
            SELECT 1 FROM SCA_HORARIO_DET dd
            WHERE dd.horid = t.horid
            AND dd.diaid = ProcessDay(t.fechamar)
            AND NVL(dd.descanso, 'N') = 'S'
        ))
        AND t.entrada IS NOT NULL
        AND t.salida IS NOT NULL
        AND NVL(t.ind_cerrado, 'N') <> 'S'
        AND t.horid IS NOT NULL
        -- Verificar que el horario tenga al menos un dia no-descanso con horing
        AND EXISTS (
            SELECT 1 FROM SCA_HORARIO_DET d 
            WHERE d.horid = t.horid 
            AND NVL(d.descanso, 'N') <> 'S'
            AND d.horing > TO_DATE('01/01/1900', 'dd/MM/yyyy')
        )
        -- Llego mas de 15 min antes del horario normal
        AND t.entrada < (
            TRUNC(t.entrada) + (
                SELECT MIN(d.horing) - TO_DATE('01/01/1900', 'dd/MM/yyyy')
                FROM SCA_HORARIO_DET d 
                WHERE d.horid = t.horid 
                AND NVL(d.descanso, 'N') <> 'S'
                AND d.horing > TO_DATE('01/01/1900', 'dd/MM/yyyy')
            ) - 15/1440
        )
        AND NVL(t.hayhea_poraut, 'N') <> 'S';
        
        v_count_anticipada := v_count_anticipada + SQL%ROWCOUNT;
        IF SQL%ROWCOUNT > 0 THEN
            DBMS_OUTPUT.PUT_LINE('PASO 7A: Entrada anticipada en descanso ajustada -15min -> ' || SQL%ROWCOUNT || ' registros');
        END IF;
        
        -- =====================================================================
        -- PASO 7: Descanso con marcaciones reales
        -- Cuando: el dia es descanso (tareo.descanso='S' O el horario para ese
        -- dia de la semana tiene SCA_HORARIO_DET.descanso='S') y el empleado
        -- tiene entrada y salida reales.
        -- El tareo original calcula mal las horas para dias de descanso
        -- porque el horario tiene todos 00:00 y tothoras es NULL.
        -- Ademas, el calculo del tareo aplica deducciones de TT_SCA_TRAMOS2
        -- que distorsionan tothoramarcas.
        -- Accion: Recalcular tothoramarcas, horadobles, nummarcaciones
        -- Regla negocio: Domingo (o cualquier dia marcado descanso en el
        -- horario) con marcaciones -> TODAS las horas son DOBLES.
        -- NOTA: Para turnos nocturnos, si salida < entrada (misma fecha)
        --       la salida es del dia siguiente -> sumar 1 dia al calculo
        -- NOTA: Si PASO 7A ajusto la entrada, PASO 7 usa la entrada ajustada
        -- En descanso:
        --   - TODAS las horas son dobles (horadobles = tothoramarcas)
        --   - horaefectiva NO se modifica (es dia de descanso, no hay jornada)
        --   - nummarcaciones se recalcula segun campos reales del tareo
        --
        -- TABLAS ESCRITURA: SCA_ASISTENCIA_TAREO
        -- TABLAS CONSULTA:  (ninguna adicional)
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t
        SET 
            -- TOTHORAMARCAS (hrs brutas): salida - entrada
            t.tothoramarcas = CASE 
                WHEN t.salida >= t.entrada
                THEN TO_DATE('01/01/1900', 'dd/MM/yyyy') + (t.salida - t.entrada)
                ELSE TO_DATE('01/01/1900', 'dd/MM/yyyy') + (t.salida + 1 - t.entrada)
            END,
            -- HORAREFRIGERIO: recalcular si tiene refrigerio
            t.horarefrigerio = CASE 
                WHEN t.inirefri IS NOT NULL AND t.finrefri IS NOT NULL
                THEN TO_DATE('01/01/1900', 'dd/MM/yyyy') + (t.finrefri - t.inirefri)
                ELSE NVL(t.horarefrigerio, TO_DATE('01/01/1900 00:00', 'dd/MM/yyyy HH24:MI'))
            END,
            -- HORAEFECTIVA: NO se modifica en descanso (no hay jornada laboral)
            -- Las horas van a horadobles, no a horaefectiva
            -- Si una ejecucion anterior puso valor aqui, restaurar a NULL
            t.horaefectiva = NULL,
            -- HORATARDANZA: no hay tardanza en descanso (no hay horario)
            t.horatardanza = TO_DATE('01/01/1900 00:00', 'dd/MM/yyyy HH24:MI'),
            -- HORADOBLES: en descanso, TODAS las horas son dobles
            t.horadobles = CASE 
                WHEN t.salida >= t.entrada
                THEN TO_DATE('01/01/1900', 'dd/MM/yyyy') + (t.salida - t.entrada)
                ELSE TO_DATE('01/01/1900', 'dd/MM/yyyy') + (t.salida + 1 - t.entrada)
            END,
            -- HORADOBLESOF (oficiales): igual a brutas en descanso/feriado.
            -- FIX 27/04/2026: Aquarius nativo descontaba el refrigerio de horadoblesof
            -- en descanso de obreros (mostraba 07:30 en lugar de 08:00 cuando habia refri).
            -- En descanso/feriado el refrigerio NO debe descontar de las dobles, porque
            -- la jornada completa se compensa o paga doble. Se sobrescribe con brutas.
            t.horadoblesof = CASE 
                WHEN t.salida >= t.entrada
                THEN TO_DATE('01/01/1900', 'dd/MM/yyyy') + (t.salida - t.entrada)
                ELSE TO_DATE('01/01/1900', 'dd/MM/yyyy') + (t.salida + 1 - t.entrada)
            END,
            -- NUMMARCACIONES: recontar segun campos reales del tareo
            -- (PASO 0-PRE puede haber puesto valor incorrecto del historial)
            t.nummarcaciones = 
                CASE WHEN t.entrada IS NOT NULL THEN 1 ELSE 0 END +
                CASE WHEN t.inirefri IS NOT NULL THEN 1 ELSE 0 END +
                CASE WHEN t.finrefri IS NOT NULL THEN 1 ELSE 0 END +
                CASE WHEN t.salida IS NOT NULL THEN 1 ELSE 0 END,
            t.codaux4 = CASE 
                WHEN t.codaux4 IS NULL THEN c_DC 
                WHEN t.codaux4 LIKE '%' || c_DC || '%' THEN t.codaux4
                ELSE t.codaux4 || c_SEP || c_DC 
            END,
            t.codaux5 = SUBSTR(CASE 
                WHEN t.codaux5 IS NULL THEN d_DC 
                WHEN t.codaux4 LIKE '%' || c_DC || '%' THEN t.codaux5
                ELSE t.codaux5 || c_SEP || d_DC 
            END, 1, 50)
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
        -- Descanso: tareo con flag 'S' o el horario del dia (DIAID) marcado como descanso
        -- Cubre caso: domingo con marcaciones donde tareo.descanso='N' pero el
        -- horario tiene SCA_HORARIO_DET.descanso='S' para ese dia de la semana.
        AND (t.descanso = 'S' OR EXISTS (
            SELECT 1 FROM SCA_HORARIO_DET dd
            WHERE dd.horid = t.horid
            AND dd.diaid = ProcessDay(t.fechamar)
            AND NVL(dd.descanso, 'N') = 'S'
        ))
        AND t.entrada IS NOT NULL
        AND t.salida IS NOT NULL
        AND NVL(t.ind_cerrado, 'N') <> 'S';
        
        IF SQL%ROWCOUNT > 0 THEN
            v_count_recalculo := v_count_recalculo + SQL%ROWCOUNT;
            DBMS_OUTPUT.PUT_LINE('PASO 7: Descanso con marcaciones: horas recalculadas -> ' || SQL%ROWCOUNT || ' registros');
        END IF;
        
        -- =====================================================================
        -- PASO 8-PRE: Insertar marcas faltantes en SCA_HISTORIAL
        -- Caso: El tareo tiene campos poblados (ENTRADA, INIREFRI, FINREFRI,
        --       SALIDA) que NO tienen marca correspondiente en SCA_HISTORIAL.
        --       Esto causa que PASO 8 cuente menos marcas de las que realmente
        --       existen en el tareo, generando MARCACION IMPAR falsa.
        -- Ejemplo: Tareo tiene E=06:53, IR=11:28, FR=11:55, S=19:02
        --          SCA_HISTORIAL solo tiene 3 marcas (falta 19:02)
        --          -> PASO 8 pondria nummarcaciones=3, alerta01='MI'
        -- Accion: Insertar las marcas que existen en el tareo pero faltan
        --         en SCA_HISTORIAL, ANTES de que PASO 8 sincronice.
        --         Solo se insertan marcas REALES (diferentes del horario teorico)
        --         para no contaminar SCA_HISTORIAL con marcas ficticias.
        --
        -- TABLAS ESCRITURA: SCA_HISTORIAL (INSERT), SCA_ASISTENCIA_TAREO (UPDATE codaux4/5)
        -- TABLAS CONSULTA:  SCA_HISTORIAL, SCA_ASISTENCIA_TAREO
        -- =====================================================================
        FOR rec_mf IN (
            SELECT t.ROWID AS rid,
                   t.num_fotocheck, t.fechamar,
                   t.entrada, t.inirefri, t.finrefri, t.salida,
                   t.entrada_fijada, t.salida_fijada,
                   t.horiniref, t.horfinref,
                   t.codaux4, t.codaux5,
                   -- Contar campos poblados en tareo
                   CASE WHEN t.entrada IS NOT NULL THEN 1 ELSE 0 END +
                   CASE WHEN t.inirefri IS NOT NULL THEN 1 ELSE 0 END +
                   CASE WHEN t.finrefri IS NOT NULL THEN 1 ELSE 0 END +
                   CASE WHEN t.salida IS NOT NULL THEN 1 ELSE 0 END AS campos_poblados,
                   -- Contar marcas en SCA_HISTORIAL
                   (SELECT COUNT(*) FROM SCA_HISTORIAL h
                    WHERE h.idtarjeta = t.num_fotocheck
                    AND h.fec_equiv = t.fechamar
                    AND NVL(h.ind_anulado, 'N') <> 'S'
                    AND NVL(h.ind_noprocesar, 0) = 0
                   ) AS marcas_his
            FROM SCA_ASISTENCIA_TAREO t
            WHERE t.fechamar = v_fecha_proceso
            AND t.cod_empresa LIKE v_empresa_filtro
            AND t.cod_personal LIKE v_personal_filtro
            AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
            AND t.num_fotocheck IS NOT NULL
            AND NVL(t.ind_cerrado, 'N') <> 'S'
            AND t.nummarcaciones >= 1
        ) LOOP
            -- Solo procesar si hay desbalance (mas campos en tareo que marcas en historial)
            IF rec_mf.campos_poblados > rec_mf.marcas_his THEN
                v_mf_insertado := 0;
                
                -- Verificar ENTRADA
                IF rec_mf.entrada IS NOT NULL THEN
                    INSERT INTO SCA_HISTORIAL (idCod, idLectora, idTarjeta, fecha, hora, tiporeg, fecreg, fec_equiv, motivo, ind_aman_hor_est)
                    SELECT id_cod_seq.NEXTVAL, '005', rec_mf.num_fotocheck,
                           TO_CHAR(rec_mf.fechamar, 'DD/MM/YYYY'),
                           TO_CHAR(rec_mf.entrada, 'HH24:MI:SS'),
                           '3', SYSDATE, rec_mf.fechamar,
                           'DEPURACION: Marca faltante entrada 8-PRE', 'A'
                    FROM DUAL
                    WHERE NOT EXISTS (
                        SELECT 1 FROM SCA_HISTORIAL h
                        WHERE h.idtarjeta = rec_mf.num_fotocheck
                        AND h.fec_equiv = rec_mf.fechamar
                        AND RTRIM(h.hora) = TO_CHAR(rec_mf.entrada, 'HH24:MI:SS')
                        AND NVL(h.ind_anulado, 'N') <> 'S'
                    );
                    IF SQL%ROWCOUNT > 0 THEN
                        v_count_historial := v_count_historial + 1;
                        v_mf_insertado := v_mf_insertado + 1;
                        DBMS_OUTPUT.PUT_LINE('PASO 8-PRE: Insertada marca faltante ENTRADA ' || TO_CHAR(rec_mf.entrada, 'HH24:MI:SS') || ' para fotocheck ' || rec_mf.num_fotocheck);
                    END IF;
                END IF;
                
                -- Verificar INIREFRI
                IF rec_mf.inirefri IS NOT NULL THEN
                    INSERT INTO SCA_HISTORIAL (idCod, idLectora, idTarjeta, fecha, hora, tiporeg, fecreg, fec_equiv, motivo, ind_aman_hor_est)
                    SELECT id_cod_seq.NEXTVAL, '005', rec_mf.num_fotocheck,
                           TO_CHAR(rec_mf.fechamar, 'DD/MM/YYYY'),
                           TO_CHAR(rec_mf.inirefri, 'HH24:MI:SS'),
                           '3', SYSDATE, rec_mf.fechamar,
                           'DEPURACION: Marca faltante inirefri 8-PRE', 'A'
                    FROM DUAL
                    WHERE NOT EXISTS (
                        SELECT 1 FROM SCA_HISTORIAL h
                        WHERE h.idtarjeta = rec_mf.num_fotocheck
                        AND h.fec_equiv = rec_mf.fechamar
                        AND RTRIM(h.hora) = TO_CHAR(rec_mf.inirefri, 'HH24:MI:SS')
                        AND NVL(h.ind_anulado, 'N') <> 'S'
                    );
                    IF SQL%ROWCOUNT > 0 THEN
                        v_count_historial := v_count_historial + 1;
                        v_mf_insertado := v_mf_insertado + 1;
                        DBMS_OUTPUT.PUT_LINE('PASO 8-PRE: Insertada marca faltante INIREFRI ' || TO_CHAR(rec_mf.inirefri, 'HH24:MI:SS') || ' para fotocheck ' || rec_mf.num_fotocheck);
                    END IF;
                END IF;
                
                -- Verificar FINREFRI
                IF rec_mf.finrefri IS NOT NULL THEN
                    INSERT INTO SCA_HISTORIAL (idCod, idLectora, idTarjeta, fecha, hora, tiporeg, fecreg, fec_equiv, motivo, ind_aman_hor_est)
                    SELECT id_cod_seq.NEXTVAL, '005', rec_mf.num_fotocheck,
                           TO_CHAR(rec_mf.fechamar, 'DD/MM/YYYY'),
                           TO_CHAR(rec_mf.finrefri, 'HH24:MI:SS'),
                           '3', SYSDATE, rec_mf.fechamar,
                           'DEPURACION: Marca faltante finrefri 8-PRE', 'A'
                    FROM DUAL
                    WHERE NOT EXISTS (
                        SELECT 1 FROM SCA_HISTORIAL h
                        WHERE h.idtarjeta = rec_mf.num_fotocheck
                        AND h.fec_equiv = rec_mf.fechamar
                        AND RTRIM(h.hora) = TO_CHAR(rec_mf.finrefri, 'HH24:MI:SS')
                        AND NVL(h.ind_anulado, 'N') <> 'S'
                    );
                    IF SQL%ROWCOUNT > 0 THEN
                        v_count_historial := v_count_historial + 1;
                        v_mf_insertado := v_mf_insertado + 1;
                        DBMS_OUTPUT.PUT_LINE('PASO 8-PRE: Insertada marca faltante FINREFRI ' || TO_CHAR(rec_mf.finrefri, 'HH24:MI:SS') || ' para fotocheck ' || rec_mf.num_fotocheck);
                    END IF;
                END IF;
                
                -- Verificar SALIDA
                -- FIX 21/04/2026: Excluir cuando salida es del dia siguiente (N6: sobretiempo)
                -- Para N6, salida=18/04 07:03 en tareo fechamar=17/04. La marca real
                -- existe en SCA_HIS con fec_equiv=18/04. NOT EXISTS busca en fec_equiv=17/04
                -- y no la encuentra -> inserta DEPURACION 07:03 en 17/04 -> marca espuria.
                -- Fix: solo insertar si TRUNC(salida) = fechamar (salida del mismo dia).
                --
                -- FIX 23/04/2026 (BUG B): Antes de INSERT, buscar la marca real en
                -- fec_equiv = fechamar +/- 1 SOLO SI esta anulada por Aquarius nativo
                -- (ind_anulado='S' AND motivo LIKE '%MARCA DUPLICADA%'). Si existe,
                -- hacer UPDATE fec_equiv = fechamar y des-anular en lugar de insertar
                -- DEPURACION duplicada.
                -- Caso real: fotocheck 034161/18-04. La salida real biometrica 07:01
                -- esta con fec_equiv=19/04 anulada como MARCA DUPLICADA. PASO 8-PRE
                -- insertaba nueva DEPURACION 07:01 en fec_equiv=18/04 -> queda
                -- duplicada (real anulada en 19/04 + DEPURACION en 18/04).
                -- Con este fix: se mueve la marca real (UPDATE fec_equiv).
                --
                -- HARDENING 23/04/2026: Restringir SOLO a marcas anuladas (ind_anulado='S')
                -- para no robar marcas activas que el tareo del dia adyacente pueda
                -- necesitar legitimamente (ej: nocturno que cruza media noche con tareos
                -- en ambos dias, ambos validos).
                IF rec_mf.salida IS NOT NULL AND TRUNC(rec_mf.salida) = rec_mf.fechamar THEN
                    -- Primero intentar mover marca real ANULADA en dia adyacente
                    UPDATE SCA_HISTORIAL h
                    SET h.fec_equiv = rec_mf.fechamar,
                        h.ind_anulado = 'N',
                        h.motivo = 'DEPURACION: Marca movida desde dia adyacente 8-PRE'
                    WHERE h.idtarjeta = rec_mf.num_fotocheck
                    AND h.fec_equiv IN (rec_mf.fechamar + 1, rec_mf.fechamar - 1)
                    AND RTRIM(h.hora) = TO_CHAR(rec_mf.salida, 'HH24:MI:SS')
                    AND h.tiporeg = '1'  -- solo marcas biometricas reales (no DEPURACION)
                    AND NVL(h.motivo, ' ') NOT LIKE 'DEPURACION%'
                    -- SOLO marcas ya descartadas por Aquarius nativo (recuperables)
                    -- FIX 23/04/2026: nativo usa ind_anulado='A' (no 'S').
                    AND NVL(h.ind_anulado, 'N') IN ('S','A')
                    AND UPPER(NVL(h.motivo, ' ')) LIKE '%MARCA DUPLICADA%'
                    AND NOT EXISTS (
                        SELECT 1 FROM SCA_HISTORIAL h2
                        WHERE h2.idtarjeta = rec_mf.num_fotocheck
                        AND h2.fec_equiv = rec_mf.fechamar
                        AND RTRIM(h2.hora) = TO_CHAR(rec_mf.salida, 'HH24:MI:SS')
                        AND NVL(h2.ind_anulado, 'N') <> 'S'
                    )
                    AND ROWNUM = 1;
                    
                    IF SQL%ROWCOUNT > 0 THEN
                        v_count_historial := v_count_historial + 1;
                        v_mf_insertado := v_mf_insertado + 1;
                        DBMS_OUTPUT.PUT_LINE('PASO 8-PRE BUG-B: Movida marca real SALIDA ' || TO_CHAR(rec_mf.salida, 'HH24:MI:SS') || ' desde dia adyacente para fotocheck ' || rec_mf.num_fotocheck);
                    ELSE
                        -- Si no se pudo mover, insertar DEPURACION (comportamiento original)
                        INSERT INTO SCA_HISTORIAL (idCod, idLectora, idTarjeta, fecha, hora, tiporeg, fecreg, fec_equiv, motivo, ind_aman_hor_est)
                        SELECT id_cod_seq.NEXTVAL, '005', rec_mf.num_fotocheck,
                               TO_CHAR(rec_mf.fechamar, 'DD/MM/YYYY'),
                               TO_CHAR(rec_mf.salida, 'HH24:MI:SS'),
                               '3', SYSDATE, rec_mf.fechamar,
                               'DEPURACION: Marca faltante salida 8-PRE', 'A'
                        FROM DUAL
                        WHERE NOT EXISTS (
                            SELECT 1 FROM SCA_HISTORIAL h
                            WHERE h.idtarjeta = rec_mf.num_fotocheck
                            AND h.fec_equiv = rec_mf.fechamar
                            AND RTRIM(h.hora) = TO_CHAR(rec_mf.salida, 'HH24:MI:SS')
                            AND NVL(h.ind_anulado, 'N') <> 'S'
                        );
                        IF SQL%ROWCOUNT > 0 THEN
                            v_count_historial := v_count_historial + 1;
                            v_mf_insertado := v_mf_insertado + 1;
                            DBMS_OUTPUT.PUT_LINE('PASO 8-PRE: Insertada marca faltante SALIDA ' || TO_CHAR(rec_mf.salida, 'HH24:MI:SS') || ' para fotocheck ' || rec_mf.num_fotocheck);
                        END IF;
                    END IF;
                END IF;
                
                -- Marcar empleado con codigo MF solo si se inserto alguna marca
                IF v_mf_insertado > 0 THEN
                    UPDATE SCA_ASISTENCIA_TAREO
                    SET codaux4 = CASE 
                            WHEN codaux4 IS NULL THEN c_MF 
                            WHEN codaux4 LIKE '%' || c_MF || '%' THEN codaux4
                            ELSE codaux4 || c_SEP || c_MF 
                        END,
                        codaux5 = SUBSTR(CASE 
                            WHEN codaux5 IS NULL THEN d_MF 
                            WHEN codaux4 LIKE '%' || c_MF || '%' THEN codaux5
                            ELSE codaux5 || c_SEP || d_MF 
                        END, 1, 50)
                    WHERE ROWID = rec_mf.rid;
                END IF;
                
            END IF;
        END LOOP;
        
        -- =====================================================================
        -- PASO 8: Sincronizar NUMMARCACIONES con SCA_HISTORIAL
        -- Actualiza nummarcaciones basandose en el conteo REAL de registros
        -- validos en SCA_HISTORIAL para todos los empleados procesados.
        -- Tambien limpia alerta01 si las marcas ahora son pares.
        --
        -- NOTA: Para horarios SIN refrigerio (horiniref=00:00), SCA_HISTORIAL
        -- puede tener marcas extra (rondas de vigilante). PASO 8B corrige eso.
        --
        -- NOTA 20/04/2026: Tareos con los 4 campos COMPLETOS (E+IR+FR+S) se
        -- excluyen porque PASO 5C ya los corrigio con el conteo exacto del tareo.
        -- SCA_HISTORIAL puede tener marcas intermedias extra (ej: checkpoints de
        -- ronda, doble pasada por lectora) que inflarian el conteo si se cuenta
        -- desde SCA_HIS. Para tareos incompletos (algun campo NULL) si es
        -- necesario contar desde SCA_HIS para detectar marca impar.
        --
        -- TABLAS ESCRITURA: SCA_ASISTENCIA_TAREO
        -- TABLAS CONSULTA:  SCA_HISTORIAL
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t
        SET t.nummarcaciones = (
                SELECT COUNT(*) 
                FROM SCA_HISTORIAL h
                WHERE h.idtarjeta = t.num_fotocheck
                AND h.fec_equiv = t.fechamar
                AND NVL(h.ind_anulado, 'N') <> 'S'
                AND NVL(h.ind_noprocesar, 0) = 0
            ),
            t.alerta01 = CASE 
                WHEN MOD((
                    SELECT COUNT(*) 
                    FROM SCA_HISTORIAL h
                    WHERE h.idtarjeta = t.num_fotocheck
                    AND h.fec_equiv = t.fechamar
                    AND NVL(h.ind_anulado, 'N') <> 'S'
                    AND NVL(h.ind_noprocesar, 0) = 0
                ), 2) = 0 THEN NULL  -- Par = OK
                ELSE 'MI'  -- Impar = alerta
            END
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
        AND t.num_fotocheck IS NOT NULL
        -- Solo actualizar registros que fueron modificados por el PKG
        AND t.codaux4 IS NOT NULL
        -- Excluir horarios SIN refrigerio (se manejan en PASO 8B)
        AND NOT (NVL(TO_CHAR(t.horiniref, 'HH24:MI'), '00:00') = '00:00' AND NVL(TO_CHAR(t.horfinref, 'HH24:MI'), '00:00') = '00:00')
        -- Excluir dias de descanso limpiados por PHANTOM (marca SCA_HIS es fantasma)
        AND NOT (t.descanso = 'S' AND t.codaux4 LIKE '%' || c_PH || '%')
        -- Excluir tareos con los 4 campos COMPLETOS: PASO 5C ya los corrigio.
        -- SCA_HIS puede tener marcas extra (checkpoints, doble pasada) que
        -- inflarian el conteo. Si el tareo esta completo, el conteo correcto es 4.
        AND NOT (
            t.entrada   IS NOT NULL
            AND t.inirefri  IS NOT NULL
            AND t.finrefri  IS NOT NULL
            AND t.salida    IS NOT NULL
        )
        -- Excluir tareos ya procesados por 3C-NOC (nocturno sin refrigerio).
        -- PASO 3C-NOC limpio inirefri/finrefri y puso nummarcaciones=2 correctamente.
        -- SCA_HIS puede tener marcas DEPURACION de R1/R3 que inflarian el conteo.
        AND NOT (t.codaux4 LIKE '%' || c_RN || '%')
        -- Excluir tareos ya procesados por 3E (refrigerio imposible).
        -- PASO 3E limpio inirefri/finrefri; PASO 5C ya puso nummarcaciones=2.
        -- SCA_HIS puede tener marcas reales del refri (12:30, 13:00) que son
        -- cronologicamente antes de la entrada pero son marcas validas del dia.
        AND NOT (t.codaux4 LIKE '%' || c_RI || '%')
        -- Excluir tareos ya procesados por 3F (refrigerio truncado).
        -- PASO 3F limpio inirefri/finrefri (S<FR); puede haber marcas 0-REST en SCA_HIS.
        AND NOT (t.codaux4 LIKE '%' || c_RT || '%');
        
        IF SQL%ROWCOUNT > 0 THEN
            DBMS_OUTPUT.PUT_LINE('PASO 8: NUMMARCACIONES sincronizado -> ' || SQL%ROWCOUNT || ' registros');
        END IF;
        
        -- =====================================================================
        -- PASO 8B: Corregir NUMMARCACIONES para horarios SIN refrigerio
        -- 
        -- CASO: Horarios como VIGILANCIA tienen horiniref=00:00, horfinref=00:00
        --       (NO tienen refrigerio). SCA_HISTORIAL puede tener marcas extras
        --       (rondas del vigilante) que no son E/IR/FR/S.
        --       PASO 8 contaria 4+ marcas cuando solo hay E+S = 2.
        --
        -- SOLUCION: Para estos horarios, contar desde campos del tareo (no SCA_HIS).
        --
        -- TABLAS ESCRITURA: SCA_ASISTENCIA_TAREO
        -- =====================================================================
        UPDATE SCA_ASISTENCIA_TAREO t
        SET t.nummarcaciones = 
                CASE WHEN t.entrada IS NOT NULL THEN 1 ELSE 0 END +
                CASE WHEN t.inirefri IS NOT NULL THEN 1 ELSE 0 END +
                CASE WHEN t.finrefri IS NOT NULL THEN 1 ELSE 0 END +
                CASE WHEN t.salida IS NOT NULL THEN 1 ELSE 0 END,
            t.alerta01 = CASE 
                WHEN MOD(
                    CASE WHEN t.entrada IS NOT NULL THEN 1 ELSE 0 END +
                    CASE WHEN t.inirefri IS NOT NULL THEN 1 ELSE 0 END +
                    CASE WHEN t.finrefri IS NOT NULL THEN 1 ELSE 0 END +
                    CASE WHEN t.salida IS NOT NULL THEN 1 ELSE 0 END,
                2) = 0 THEN NULL
                ELSE 'MI'
            END
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE v_empresa_filtro
        AND t.cod_personal LIKE v_personal_filtro
        AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
        AND t.num_fotocheck IS NOT NULL
        AND t.codaux4 IS NOT NULL
        -- SOLO horarios sin refrigerio (incluye horiniref IS NULL = sin horario asignado)
        AND NVL(TO_CHAR(t.horiniref, 'HH24:MI'), '00:00') = '00:00' 
        AND NVL(TO_CHAR(t.horfinref, 'HH24:MI'), '00:00') = '00:00'
        -- Excluir dias de descanso limpiados por PHANTOM (marca SCA_HIS es fantasma)
        AND NOT (t.descanso = 'S' AND t.codaux4 LIKE '%' || c_PH || '%');
        
        IF SQL%ROWCOUNT > 0 THEN
            DBMS_OUTPUT.PUT_LINE('PASO 8B: NUMMARCACIONES corregido (sin refri) -> ' || SQL%ROWCOUNT || ' registros');
        END IF;
        
        -- =====================================================================
        -- PASO 9: Ocultar marcas no usadas en SCA_HISTORIAL (24/04/2026)
        -- REGLA UI .NET: La ventana "Detalle de Marcacion" solo debe mostrar las
        -- 4 marcas validas asignadas a campos del tareo (Entrada, IniRefri,
        -- FinRefri, Salida). Cualquier otra marca activa del dia (rondas internas
        -- lect=004, checkpoints, marcas intermedias no usadas) debe ocultarse.
        --
        -- FIX 24/04/2026 v3: Aquarius standard es IND_ANULADO='A' (Anulada),
        -- NO 'S' ni IND_NOPROCESAR=1. Confirmado en sp_SCA_Delete_Marca:
        --     UPDATE SCA_HISTORIAL SET ind_anulado='A', motivo=v_motivo
        -- y se restaura con ind_anulado=NULL. El .NET filtra por este valor.
        --
        -- Filtros:
        -- - Solo procesa dias del rango v_fecha_proceso
        -- - Solo marca activas (ind_anulado IS NULL/N AND ind_noprocesar=0)
        -- - NO toca marcas DEPURACION% (son las que el paquete inserta para campos)
        -- - NO toca marcas que coincidan con E/IR/FR/S del tareo final
        -- - Excluye dias cerrados (ind_cerrado='S')
        --
        -- TABLAS ESCRITURA: SCA_HISTORIAL (UPDATE ind_anulado='A', motivo)
        -- TABLAS CONSULTA:  SCA_ASISTENCIA_TAREO
        -- =====================================================================
        UPDATE SCA_HISTORIAL h
        SET h.ind_anulado = 'A',
            h.motivo = 'DEPURACION: Marca no asignada (oculta UI)'
        WHERE h.fec_equiv = v_fecha_proceso
        AND NVL(h.ind_anulado, 'N') NOT IN ('A','S')
        AND NVL(h.ind_noprocesar, 0) = 0
        AND NVL(h.motivo, ' ') NOT LIKE 'DEPURACION%'
        AND EXISTS (
            SELECT 1 FROM SCA_ASISTENCIA_TAREO t
            WHERE t.num_fotocheck = h.idtarjeta
            AND t.fechamar = h.fec_equiv
            AND t.cod_empresa LIKE v_empresa_filtro
            AND t.cod_personal LIKE v_personal_filtro
            AND (p_solo_obreros = 'N' OR t.ind_obrero = 'S')
            AND NVL(t.ind_cerrado, 'N') <> 'S'
            -- La marca NO coincide con ninguno de los 4 campos validos del tareo
            AND RTRIM(h.hora) <> NVL(TO_CHAR(t.entrada,  'HH24:MI:SS'), '##')
            AND RTRIM(h.hora) <> NVL(TO_CHAR(t.inirefri, 'HH24:MI:SS'), '##')
            AND RTRIM(h.hora) <> NVL(TO_CHAR(t.finrefri, 'HH24:MI:SS'), '##')
            AND RTRIM(h.hora) <> NVL(TO_CHAR(t.salida,   'HH24:MI:SS'), '##')
        );
        
        IF SQL%ROWCOUNT > 0 THEN
            DBMS_OUTPUT.PUT_LINE('PASO 9: Marcas ocultas para UI (rondas/no asignadas) -> ' || SQL%ROWCOUNT || ' marcas');
        END IF;

        -- =====================================================================
        -- PASO FINAL: REDONDEO HORA-ENTERA HE / DOBLES / BANCO
        -- Regla: minutos < 45 baja, >= 45 sube. Se aplica DESPUES de todos
        -- los recalculos para garantizar que los campos de horas extras,
        -- dobles oficiales y banco de horas queden en horas enteras antes
        -- de que las compensaciones (PASO 15 del proceso principal) los lean.
        -- Idempotente: si ya estan en horas enteras no hace nada.
        -- =====================================================================
        SP_SCA_REDONDEAR_TAREO_HE(
            p_cod_empresa  => p_cod_empresa,
            p_cod_personal => p_cod_personal,
            p_fecha        => v_fecha_proceso
        );

        COMMIT;
        
        -- =====================================================================
        -- Retornar resumen
        -- =====================================================================
        OPEN cv_resultado FOR
            SELECT 'OK' AS resultado,
                   p_fecha AS fecha_proceso,
                   v_count_nocturno AS turnos_nocturnos_corregidos,
                   v_count_entrada AS entradas_completadas,
                   v_count_anticipada AS entradas_anticipadas_ajustadas,
                   v_count_salida AS salidas_completadas,
                   v_count_inirefri AS inirefri_completados,
                   v_count_finrefri AS finrefri_completados,
                   v_count_anomala AS marcaciones_anomalas,
                   v_count_rn AS nocturnos_sin_refri_limpiados,
                   v_count_recalculo AS horas_recalculadas,
                   v_count_nocturno + v_count_entrada + v_count_salida + v_count_inirefri + 
                   v_count_finrefri + v_count_anomala AS total_marcas_generadas,
                   v_count_historial AS marcas_historial_insertadas,
                   v_empleados_sin_tareo AS empleados_sin_tareo
            FROM DUAL;

    EXCEPTION
        WHEN OTHERS THEN
            ROLLBACK;
            v_error_msg := SQLERRM;
            OPEN cv_resultado FOR
                SELECT 'ERROR: ' || v_error_msg AS resultado,
                       p_fecha AS fecha_proceso,
                       0 AS turnos_nocturnos_corregidos,
                       0 AS entradas_completadas,
                       0 AS entradas_anticipadas_ajustadas,
                       0 AS salidas_completadas,
                       0 AS inirefri_completados,
                       0 AS finrefri_completados,
                       0 AS marcaciones_anomalas,
                       0 AS nocturnos_sin_refri_limpiados,
                       0 AS horas_recalculadas,
                       0 AS total_marcas_generadas,
                       0 AS marcas_historial_insertadas,
                       v_empleados_sin_tareo AS empleados_sin_tareo
                FROM DUAL;
            
    END DEPURA_TOTAL;


    -- =========================================================================
    -- ROLLBACK_MARCACIONES: Revertir cambios automaticos
    -- =========================================================================
    PROCEDURE ROLLBACK_MARCACIONES(
        p_cod_empresa   IN VARCHAR2 DEFAULT NULL,
        p_cod_personal  IN VARCHAR2 DEFAULT NULL,
        p_fecha         IN VARCHAR2,
        cv_resultado    OUT SYS_REFCURSOR
    )
    AS
        v_fecha_proceso DATE;
        v_count_revertidos NUMBER := 0;
        v_count_nocturno_rev NUMBER := 0;
        v_error_msg VARCHAR2(500);
    BEGIN
        v_fecha_proceso := TO_DATE(p_fecha, 'dd/MM/yyyy');
        
        -- =================================================================
        -- FASE 1: Revertir turnos nocturnos (N5, N4, N1)
        -- Estos mueven marcas REALES entre campos/dias, NO se detectan
        -- por comparacion con _fijada. Se identifican por codaux4.
        -- ORDEN: primero N5 (salida teorica), luego N4 (reubicacion), luego N1 (movimiento entre dias)
        -- =================================================================
        
        -- PASO R-N5: Revertir N5 (PASO 0B3b: salida teorica nocturna)
        -- N5 hizo: salida = entrada + tothoras (valor calculado)
        -- Reverse: salida = NULL
        UPDATE SCA_ASISTENCIA_TAREO t
        SET t.salida = NULL
        WHERE t.fechamar IN (v_fecha_proceso, v_fecha_proceso + 1)
        AND t.cod_empresa LIKE NVL(p_cod_empresa, '%')
        AND t.cod_personal LIKE NVL(p_cod_personal, '%')
        AND t.codaux4 LIKE '%N5%'
        AND NVL(t.ind_cerrado, 'N') <> 'S';
        
        v_count_nocturno_rev := SQL%ROWCOUNT;
        
        IF v_count_nocturno_rev > 0 THEN
            DBMS_OUTPUT.PUT_LINE('ROLLBACK N5: ' || v_count_nocturno_rev || ' salidas teoricas nocturnas revertidas');
        END IF;
        
        -- PASO R-N4: Revertir N4 (PASO 0B2/0B5: salida reubicada como entrada)
        -- N4 hizo: entrada=salida, salida=NULL
        -- Reverse: salida=entrada, entrada=NULL
        -- Aplica a HOY y al DIA SIGUIENTE (0B5 modifica fecha+1)
        UPDATE SCA_ASISTENCIA_TAREO t
        SET t.salida = t.entrada,
            t.entrada = NULL
        WHERE t.fechamar IN (v_fecha_proceso, v_fecha_proceso + 1)
        AND t.cod_empresa LIKE NVL(p_cod_empresa, '%')
        AND t.cod_personal LIKE NVL(p_cod_personal, '%')
        AND t.codaux4 LIKE '%N4%'
        AND NVL(t.ind_cerrado, 'N') <> 'S';
        
        v_count_nocturno_rev := SQL%ROWCOUNT;
        
        IF v_count_nocturno_rev > 0 THEN
            DBMS_OUTPUT.PUT_LINE('ROLLBACK N4: ' || v_count_nocturno_rev || ' reubicaciones revertidas');
        END IF;
        
        -- PASO R-N1a: Revertir N1 en HOY (restaurar entrada desde salida de ayer)
        -- N1 hizo en HOY: entrada=NULL (la marca fue movida a ayer.salida)
        -- Reverse: entrada = ayer.salida (trae de vuelta la marca original)
        UPDATE SCA_ASISTENCIA_TAREO t_hoy
        SET t_hoy.entrada = (
            SELECT t_ayer.salida
            FROM SCA_ASISTENCIA_TAREO t_ayer
            WHERE t_ayer.fechamar = t_hoy.fechamar - 1
            AND t_ayer.cod_empresa = t_hoy.cod_empresa
            AND t_ayer.cod_personal = t_hoy.cod_personal
            AND t_ayer.codaux4 LIKE '%N1%'
        )
        WHERE t_hoy.fechamar = v_fecha_proceso
        AND t_hoy.cod_empresa LIKE NVL(p_cod_empresa, '%')
        AND t_hoy.cod_personal LIKE NVL(p_cod_personal, '%')
        AND t_hoy.codaux4 LIKE '%N1%'
        AND NVL(t_hoy.ind_cerrado, 'N') <> 'S'
        AND EXISTS (
            SELECT 1 FROM SCA_ASISTENCIA_TAREO t_ayer
            WHERE t_ayer.fechamar = t_hoy.fechamar - 1
            AND t_ayer.cod_empresa = t_hoy.cod_empresa
            AND t_ayer.cod_personal = t_hoy.cod_personal
            AND t_ayer.codaux4 LIKE '%N1%'
            AND t_ayer.salida IS NOT NULL
        );
        
        v_count_nocturno_rev := v_count_nocturno_rev + SQL%ROWCOUNT;
        
        -- PASO R-N1a2: Revertir N1 en DIA SIGUIENTE (PASO 0B4 limpio su entrada)
        -- Restaurar entrada del dia siguiente desde la salida de hoy
        UPDATE SCA_ASISTENCIA_TAREO t_sig
        SET t_sig.entrada = (
            SELECT t_hoy.salida
            FROM SCA_ASISTENCIA_TAREO t_hoy
            WHERE t_hoy.fechamar = t_sig.fechamar - 1
            AND t_hoy.cod_empresa = t_sig.cod_empresa
            AND t_hoy.cod_personal = t_sig.cod_personal
            AND t_hoy.codaux4 LIKE '%N1%'
        )
        WHERE t_sig.fechamar = v_fecha_proceso + 1
        AND t_sig.cod_empresa LIKE NVL(p_cod_empresa, '%')
        AND t_sig.cod_personal LIKE NVL(p_cod_personal, '%')
        AND t_sig.codaux4 LIKE '%N1%'
        AND NVL(t_sig.ind_cerrado, 'N') <> 'S'
        AND EXISTS (
            SELECT 1 FROM SCA_ASISTENCIA_TAREO t_hoy
            WHERE t_hoy.fechamar = t_sig.fechamar - 1
            AND t_hoy.cod_empresa = t_sig.cod_empresa
            AND t_hoy.cod_personal = t_sig.cod_personal
            AND t_hoy.codaux4 LIKE '%N1%'
            AND t_hoy.salida IS NOT NULL
        );
        
        v_count_nocturno_rev := v_count_nocturno_rev + SQL%ROWCOUNT;
        
        -- PASO R-N1b: Revertir N1 en AYER (limpiar salida que fue puesta por PASO 0A)
        -- N1 hizo en AYER: salida = hoy.entrada_original
        -- Reverse: salida = NULL, limpiar codaux
        UPDATE SCA_ASISTENCIA_TAREO t_ayer
        SET t_ayer.salida = NULL,
            t_ayer.codaux4 = NULL,
            t_ayer.codaux5 = NULL
        WHERE t_ayer.fechamar = v_fecha_proceso - 1
        AND t_ayer.cod_empresa LIKE NVL(p_cod_empresa, '%')
        AND t_ayer.cod_personal LIKE NVL(p_cod_personal, '%')
        AND t_ayer.codaux4 LIKE '%N1%'
        AND NVL(t_ayer.ind_cerrado, 'N') <> 'S';
        
        v_count_nocturno_rev := v_count_nocturno_rev + SQL%ROWCOUNT;
        
        -- PASO R-N1c: Revertir N1 en HOY (limpiar salida que fue puesta por PASO 0B3)
        -- PASO 0B3 puso salida = sig.entrada (forward). Limpiarla.
        -- Nota: solo limpia si la salida fue puesta por PASO 0B3 (codaux4 tiene N1)
        -- y si el dia siguiente tenia su entrada modificada (codaux4 tiene N1)
        UPDATE SCA_ASISTENCIA_TAREO t
        SET t.salida = NULL
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE NVL(p_cod_empresa, '%')
        AND t.cod_personal LIKE NVL(p_cod_personal, '%')
        AND t.codaux4 LIKE '%N1%'
        AND t.salida IS NOT NULL
        AND NVL(t.ind_cerrado, 'N') <> 'S'
        AND EXISTS (
            SELECT 1 FROM SCA_ASISTENCIA_TAREO t_sig
            WHERE t_sig.fechamar = t.fechamar + 1
            AND t_sig.cod_empresa = t.cod_empresa
            AND t_sig.cod_personal = t.cod_personal
            AND t_sig.codaux4 LIKE '%N1%'
        );
        
        v_count_nocturno_rev := v_count_nocturno_rev + SQL%ROWCOUNT;
        
        IF v_count_nocturno_rev > 0 THEN
            DBMS_OUTPUT.PUT_LINE('ROLLBACK N1: ' || v_count_nocturno_rev || ' movimientos nocturnos revertidos (incluye dia anterior)');
        END IF;
        
        -- =================================================================
        -- ROLLBACK R-SSR: Revertir SSR (salida real oculta restaurada)
        -- SSR puso salida = marca_oculta_real (NO = salida_fijada), por eso
        -- FASE 2 (comparacion con _fijada) no lo captura.
        -- La marca insertada en SCA_HISTORIAL (motivo 'DEPURACION: Salida real
        -- restaurada (SSR)') se elimina automaticamente en FASE 4 (DELETE DEPURACION%).
        -- =================================================================
        UPDATE SCA_ASISTENCIA_TAREO t
        SET t.salida  = NULL,
            t.codaux4 = NULL,
            t.codaux5 = NULL
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE NVL(p_cod_empresa, '%')
        AND t.cod_personal LIKE NVL(p_cod_personal, '%')
        AND t.codaux4 LIKE '%SSR%'
        AND NVL(t.ind_cerrado, 'N') <> 'S';
        
        v_count_revertidos := v_count_revertidos + SQL%ROWCOUNT;
        IF SQL%ROWCOUNT > 0 THEN
            DBMS_OUTPUT.PUT_LINE('ROLLBACK R-SSR: ' || SQL%ROWCOUNT || ' salidas reales restauradas revertidas');
        END IF;
        
        -- PASO R-N6: Revertir N6 (PASO 0B3c: salida extendida al dia siguiente con sobretiempo)
        -- N6 puso salida = marca_real_dia+1 (fecha cruzada, TRUNC(salida) > fechamar)
        -- El valor original de Aquarius no es recuperable (era incorrecto de origen).
        -- Reverse: salida = NULL (consistente con R-N5 y R-SSR)
        UPDATE SCA_ASISTENCIA_TAREO t
        SET t.salida  = NULL,
            t.codaux4 = NULL,
            t.codaux5 = NULL
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE NVL(p_cod_empresa, '%')
        AND t.cod_personal LIKE NVL(p_cod_personal, '%')
        AND t.codaux4 LIKE '%' || 'N6' || '%'
        AND NVL(t.ind_cerrado, 'N') <> 'S';
        
        v_count_nocturno_rev := v_count_nocturno_rev + SQL%ROWCOUNT;
        IF SQL%ROWCOUNT > 0 THEN
            DBMS_OUTPUT.PUT_LINE('ROLLBACK R-N6: ' || SQL%ROWCOUNT || ' salidas dia+1 revertidas a NULL');
        END IF;
        
        -- =================================================================
        -- FASE 2: Revertir marcaciones teoricas (comparando con _fijada)
        -- Detecta marcas auto-generadas que coinciden con horario teorico
        -- =================================================================
        UPDATE SCA_ASISTENCIA_TAREO t
        SET 
            t.entrada = CASE 
                WHEN t.entrada = t.entrada_fijada THEN NULL 
                ELSE t.entrada 
            END,
            t.salida = CASE 
                WHEN t.salida = t.salida_fijada THEN NULL 
                ELSE t.salida 
            END,
            t.inirefri = CASE 
                WHEN t.inirefri = t.horiniref THEN NULL 
                ELSE t.inirefri 
            END,
            t.finrefri = CASE 
                WHEN t.finrefri = t.horfinref THEN NULL 
                ELSE t.finrefri 
            END,
            t.nummarcaciones = 
                CASE WHEN t.entrada IS NOT NULL AND t.entrada <> t.entrada_fijada THEN 1 ELSE 0 END +
                CASE WHEN t.inirefri IS NOT NULL AND t.inirefri <> t.horiniref THEN 1 ELSE 0 END +
                CASE WHEN t.finrefri IS NOT NULL AND t.finrefri <> t.horfinref THEN 1 ELSE 0 END +
                CASE WHEN t.salida IS NOT NULL AND t.salida <> t.salida_fijada THEN 1 ELSE 0 END,
            t.codaux4 = NULL,
            t.codaux5 = NULL
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE NVL(p_cod_empresa, '%')
        AND t.cod_personal LIKE NVL(p_cod_personal, '%')
        AND (
            t.entrada = t.entrada_fijada OR
            t.salida = t.salida_fijada OR
            t.inirefri = t.horiniref OR
            t.finrefri = t.horfinref
        )
        AND NVL(t.ind_cerrado, 'N') <> 'S';
        
        v_count_revertidos := SQL%ROWCOUNT;
        
        -- =================================================================
        -- FASE 3: Limpiar codaux4/codaux5 restantes
        -- Algunos codigos (DC, RC, N1 sin _fijada) no se limpian en FASE 2
        -- Incluye HOY y DIA SIGUIENTE (PASO 0B4/0B5 los modifica)
        -- =================================================================
        UPDATE SCA_ASISTENCIA_TAREO t
        SET t.codaux4 = NULL,
            t.codaux5 = NULL
        WHERE t.fechamar IN (v_fecha_proceso, v_fecha_proceso + 1)
        AND t.cod_empresa LIKE NVL(p_cod_empresa, '%')
        AND t.cod_personal LIKE NVL(p_cod_personal, '%')
        AND (t.codaux4 IS NOT NULL OR t.codaux5 IS NOT NULL)
        AND NVL(t.ind_cerrado, 'N') <> 'S';
        
        -- Eliminar marcas de depuracion en SCA_HISTORIAL
        DELETE FROM SCA_HISTORIAL h
        WHERE h.fec_equiv = v_fecha_proceso
        AND h.motivo LIKE 'DEPURACION%'
        AND h.tiporeg = '3'
        AND h.idtarjeta IN (
            SELECT t.num_fotocheck
            FROM SCA_ASISTENCIA_TAREO t
            WHERE t.fechamar = v_fecha_proceso
            AND t.cod_empresa LIKE NVL(p_cod_empresa, '%')
            AND t.cod_personal LIKE NVL(p_cod_personal, '%')
            AND t.num_fotocheck IS NOT NULL
        );
        
        -- Reactivar marcas reales ocultadas por PASO 9 (24/04/2026 v3)
        UPDATE SCA_HISTORIAL h
        SET h.ind_anulado = NULL,
            h.ind_noprocesar = 0,
            h.obs_noprocesar = NULL,
            h.motivo = NULL
        WHERE h.fec_equiv = v_fecha_proceso
        AND ( h.motivo LIKE 'DEPURACION: Marca no asignada%'
           OR h.obs_noprocesar LIKE 'DEPURACION: Marca no asignada%' )
        AND h.idtarjeta IN (
            SELECT t.num_fotocheck
            FROM SCA_ASISTENCIA_TAREO t
            WHERE t.fechamar = v_fecha_proceso
            AND t.cod_empresa LIKE NVL(p_cod_empresa, '%')
            AND t.cod_personal LIKE NVL(p_cod_personal, '%')
            AND t.num_fotocheck IS NOT NULL
        );
        
        DBMS_OUTPUT.PUT_LINE('ROLLBACK: ' || v_count_revertidos || ' registros teoricos revertidos, '
            || v_count_nocturno_rev || ' nocturnos revertidos, '
            || SQL%ROWCOUNT || ' marcas depuracion eliminadas de SCA_HISTORIAL');
        
        -- Resincronizar nummarcaciones con marcas reales (HOY)
        UPDATE SCA_ASISTENCIA_TAREO t
        SET t.nummarcaciones = (
            SELECT COUNT(*) 
            FROM SCA_HISTORIAL h
            WHERE h.idtarjeta = t.num_fotocheck
            AND h.fec_equiv = t.fechamar
        )
        WHERE t.fechamar = v_fecha_proceso
        AND t.cod_empresa LIKE NVL(p_cod_empresa, '%')
        AND t.cod_personal LIKE NVL(p_cod_personal, '%')
        AND t.num_fotocheck IS NOT NULL
        AND NVL(t.ind_cerrado, 'N') <> 'S';
        
        -- Resincronizar nummarcaciones del dia ANTERIOR (si N1 fue revertido)
        IF v_count_nocturno_rev > 0 THEN
            -- Dia anterior
            UPDATE SCA_ASISTENCIA_TAREO t
            SET t.nummarcaciones = (
                SELECT COUNT(*) 
                FROM SCA_HISTORIAL h
                WHERE h.idtarjeta = t.num_fotocheck
                AND h.fec_equiv = t.fechamar
            )
            WHERE t.fechamar = v_fecha_proceso - 1
            AND t.cod_empresa LIKE NVL(p_cod_empresa, '%')
            AND t.cod_personal LIKE NVL(p_cod_personal, '%')
            AND t.num_fotocheck IS NOT NULL
            AND NVL(t.ind_cerrado, 'N') <> 'S';
            
            -- Dia siguiente (PASO 0B4/0B5 modifica fecha+1)
            UPDATE SCA_ASISTENCIA_TAREO t
            SET t.nummarcaciones = (
                SELECT COUNT(*) 
                FROM SCA_HISTORIAL h
                WHERE h.idtarjeta = t.num_fotocheck
                AND h.fec_equiv = t.fechamar
            )
            WHERE t.fechamar = v_fecha_proceso + 1
            AND t.cod_empresa LIKE NVL(p_cod_empresa, '%')
            AND t.cod_personal LIKE NVL(p_cod_personal, '%')
            AND t.num_fotocheck IS NOT NULL
            AND NVL(t.ind_cerrado, 'N') <> 'S';
        END IF;
        
        COMMIT;
        
        OPEN cv_resultado FOR
            SELECT 'OK' AS resultado,
                   v_count_revertidos + v_count_nocturno_rev AS registros_revertidos,
                   'Marcaciones revertidas (teoricas + nocturnos), historial limpio' AS descripcion
            FROM DUAL;
            
    EXCEPTION
        WHEN OTHERS THEN
            ROLLBACK;
            v_error_msg := SQLERRM;
            OPEN cv_resultado FOR
                SELECT 'ERROR' AS resultado,
                       v_error_msg AS mensaje_error
                FROM DUAL;
                
    END ROLLBACK_MARCACIONES;


    -- =========================================================================
    -- VER_ESTADO: Consultar estado actual del empleado
    -- =========================================================================
    PROCEDURE VER_ESTADO(
        p_cod_empresa   IN VARCHAR2,
        p_cod_personal  IN VARCHAR2,
        p_fecha         IN VARCHAR2,
        cv_resultado    OUT SYS_REFCURSOR
    )
    AS
        v_fecha_proceso DATE;
        v_error_msg VARCHAR2(500);
    BEGIN
        v_fecha_proceso := TO_DATE(p_fecha, 'dd/MM/yyyy');
        
        OPEN cv_resultado FOR
            SELECT 
                t.cod_empresa,
                t.cod_personal,
                t.num_fotocheck AS fotocheck,
                t.nummarcaciones,
                -- Marcaciones actuales
                TO_CHAR(t.entrada, 'HH24:MI:SS') AS entrada,
                TO_CHAR(t.inirefri, 'HH24:MI:SS') AS inirefri,
                TO_CHAR(t.finrefri, 'HH24:MI:SS') AS finrefri,
                TO_CHAR(t.salida, 'HH24:MI:SS') AS salida,
                -- Horario original (SCA_HORARIO_DET)
                hc.hordes AS hor_descripcion,
                hd.horcladet AS hor_clase,
                TO_CHAR(hd.horing, 'HH24:MI') AS hor_entrada,
                TO_CHAR(hd.horiniref, 'HH24:MI') AS hor_inirefri,
                TO_CHAR(hd.horfinref, 'HH24:MI') AS hor_finrefri,
                TO_CHAR(hd.horsal, 'HH24:MI') AS hor_salida,
                TO_CHAR(hd.tothoras, 'HH24:MI') AS hor_total_hrs,
                NVL(hd.descanso, 'N') AS hor_descanso,
                -- Horario fijado en tareo
                TO_CHAR(t.entrada_fijada, 'HH24:MI') AS entrada_teorica,
                TO_CHAR(t.horiniref, 'HH24:MI') AS inirefri_teorico,
                TO_CHAR(t.horfinref, 'HH24:MI') AS finrefri_teorico,
                TO_CHAR(t.salida_fijada, 'HH24:MI') AS salida_teorica,
                -- Horas calculadas
                TO_CHAR(t.tothoramarcas, 'HH24:MI') AS hrs_brutas,
                TO_CHAR(t.horarefrigerio, 'HH24:MI') AS hrs_refrigerio,
                TO_CHAR(t.horaefectiva, 'HH24:MI') AS hrs_efectivas,
                TO_CHAR(t.horatardanza, 'HH24:MI') AS tardanza,
                -- Horas nocturnas
                TO_CHAR(t.tothoranocturna, 'HH24:MI') AS hrs_nocturnas,
                TO_CHAR(t.tothoranocturna_of, 'HH24:MI') AS hrs_nocturnas_of,
                -- Horas teoricas (para debug)
                TO_CHAR(t.tothoras, 'HH24:MI') AS tothoras,
                -- Horas dobles (descanso/feriado)
                TO_CHAR(t.horadobles, 'HH24:MI') AS horadobles,
                -- Indicadores
                NVL(t.descanso, 'N') AS descanso,
                NVL(t.ind_cerrado, 'N') AS cerrado,
                NVL(t.ind_obrero, 'N') AS obrero,
                -- Indicar auto-generadas
                CASE WHEN t.entrada = t.entrada_fijada THEN 'AUTO' ELSE 'REAL' END AS tipo_entrada,
                CASE WHEN t.salida = t.salida_fijada THEN 'AUTO' ELSE 'REAL' END AS tipo_salida,
                -- Permisos/Ausencias (si tiene valor = tiene permiso)
                CASE WHEN t.per_desc_med IS NOT NULL THEN 'S' ELSE 'N' END AS desc_medico,
                CASE WHEN t.per_subsidio IS NOT NULL THEN 'S' ELSE 'N' END AS subsidio,
                CASE WHEN t.per_goce IS NOT NULL THEN 'S' ELSE 'N' END AS perm_goce,
                CASE WHEN t.per_sgoce IS NOT NULL THEN 'S' ELSE 'N' END AS perm_sgoce,
                CASE WHEN t.per_vaca IS NOT NULL THEN 'S' ELSE 'N' END AS vacaciones,
                CASE WHEN t.per_suspension IS NOT NULL THEN 'S' ELSE 'N' END AS suspension,
                CASE WHEN t.per_lic_pat IS NOT NULL THEN 'S' ELSE 'N' END AS lic_paternidad,
                CASE WHEN t.per_lic_fac IS NOT NULL THEN 'S' ELSE 'N' END AS lic_fallecimiento,
                -- Horas extras
                TO_CHAR(t.horaextra, 'HH24:MI') AS hora_extra,
                TO_CHAR(t.horaextantes, 'HH24:MI') AS hora_extra_antes,
                TO_CHAR(t.totalhorasextras, 'HH24:MI') AS total_horas_extras,
                TO_CHAR(t.horaantesentrada, 'HH24:MI') AS tiempo_anticipado,
                NVL(t.hayhea_poraut, 'N') AS he_antes_autorizada,
                -- Horas extras - campos derivados (rangos/breakdown)
                TO_CHAR(t.horadespuessalida, 'HH24:MI') AS hora_desp_salida,
                TO_CHAR(t.horaextraofi, 'HH24:MI') AS hora_extra_ofi,
                TO_CHAR(t.totalhorasextrasofi, 'HH24:MI') AS total_extras_ofi,
                TO_CHAR(t.horaextra_ajus, 'HH24:MI') AS hora_extra_ajus,
                t.alerta06 AS alerta06,
                TO_CHAR(t.horaextra1, 'HH24:MI') AS he_25pct,
                TO_CHAR(t.horaextra2, 'HH24:MI') AS he_35pct,
                TO_CHAR(t.horaextra3, 'HH24:MI') AS he_50pct,
                TO_CHAR(t.horaexofi1, 'HH24:MI') AS he_ofi_25pct,
                TO_CHAR(t.horaexofi2, 'HH24:MI') AS he_ofi_35pct,
                TO_CHAR(t.horaexofi3, 'HH24:MI') AS he_ofi_50pct,
                -- Configuracion de rangos (para debug)
                TO_CHAR(t.h25f, 'HH24:MI') AS cfg_h25f,
                TO_CHAR(t.h35i, 'HH24:MI') AS cfg_h35i,
                TO_CHAR(t.h35f, 'HH24:MI') AS cfg_h35f,
                TO_CHAR(t.hni, 'HH24:MI') AS cfg_hni,
                t.ajuste_hextra AS cfg_ajuste_he,
                t.tippagohe AS cfg_tippagohe,
                -- Campos de auditoría de depuración
                t.codaux4 AS cod_depuracion,
                t.codaux5 AS desc_depuracion,
                -- Alerta de marcacion impar
                t.alerta01 AS alerta01,
                -- Marcaciones en SCA_HISTORIAL (verificacion cruzada)
                (SELECT COUNT(*) FROM SCA_HISTORIAL h 
                 WHERE h.idtarjeta = t.num_fotocheck 
                 AND h.fec_equiv = t.fechamar) AS marcas_historial
            FROM SCA_ASISTENCIA_TAREO t
            LEFT JOIN SCA_HORARIO_CAB hc ON t.horid = hc.horid
            LEFT JOIN SCA_HORARIO_DET hd ON hc.horid = hd.horid
                AND hd.diaid = ProcessDay(t.fechamar)
            WHERE t.fechamar = v_fecha_proceso
            AND t.cod_empresa = p_cod_empresa
            AND t.cod_personal = p_cod_personal;
            
    EXCEPTION
        WHEN OTHERS THEN
            v_error_msg := SQLERRM;
            DBMS_OUTPUT.PUT_LINE('ERROR en VER_ESTADO: ' || v_error_msg);
            OPEN cv_resultado FOR
                SELECT 
                    NULL AS cod_empresa, NULL AS cod_personal, NULL AS fotocheck, 0 AS nummarcaciones,
                    NULL AS entrada, NULL AS inirefri, NULL AS finrefri, NULL AS salida,
                    NULL AS hor_descripcion, NULL AS hor_clase,
                    NULL AS hor_entrada, NULL AS hor_inirefri, NULL AS hor_finrefri,
                    NULL AS hor_salida, NULL AS hor_total_hrs, NULL AS hor_descanso,
                    NULL AS entrada_teorica, NULL AS inirefri_teorico,
                    NULL AS finrefri_teorico, NULL AS salida_teorica,
                    NULL AS hrs_brutas, NULL AS hrs_refrigerio, NULL AS hrs_efectivas,
                    NULL AS tardanza, NULL AS hrs_nocturnas, NULL AS hrs_nocturnas_of,
                    NULL AS tothoras,
                    NULL AS horadobles,
                    NULL AS descanso, NULL AS cerrado, NULL AS obrero,
                    NULL AS tipo_entrada, NULL AS tipo_salida,
                    NULL AS desc_medico, NULL AS subsidio, NULL AS perm_goce,
                    NULL AS perm_sgoce, NULL AS vacaciones, NULL AS suspension,
                    NULL AS lic_paternidad, NULL AS lic_fallecimiento,
                    NULL AS hora_extra, NULL AS hora_extra_antes,
                    NULL AS total_horas_extras, NULL AS tiempo_anticipado,
                    NULL AS he_antes_autorizada,
                    NULL AS hora_desp_salida, NULL AS hora_extra_ofi,
                    NULL AS total_extras_ofi, NULL AS hora_extra_ajus,
                    NULL AS alerta06,
                    NULL AS he_25pct, NULL AS he_35pct, NULL AS he_50pct,
                    NULL AS he_ofi_25pct, NULL AS he_ofi_35pct, NULL AS he_ofi_50pct,
                    NULL AS cfg_h25f, NULL AS cfg_h35i, NULL AS cfg_h35f, NULL AS cfg_hni,
                    TO_NUMBER(NULL) AS cfg_ajuste_he, NULL AS cfg_tippagohe,
                    'ERROR: ' || v_error_msg AS cod_depuracion,
                    NULL AS desc_depuracion,
                    NULL AS alerta01, 0 AS marcas_historial
                FROM DUAL;
                
    END VER_ESTADO;


    -- =========================================================================
    -- DEPURA_RANGO: Proceso masivo para rango de fechas
    -- Llama a DEPURA_TOTAL para cada dia del rango.
    -- Cada dia es una transaccion independiente (si falla uno, los demas ya estan COMMIT).
    -- Los resultados por dia se muestran via DBMS_OUTPUT.
    -- El cursor final retorna el consolidado.
    -- =========================================================================
    PROCEDURE DEPURA_RANGO(
        p_cod_empresa    IN VARCHAR2 DEFAULT NULL,
        p_cod_personal   IN VARCHAR2 DEFAULT NULL,
        p_fecha_inicio   IN VARCHAR2,
        p_fecha_fin      IN VARCHAR2,
        p_solo_obreros   IN VARCHAR2 DEFAULT 'N',
        cv_resultado     OUT SYS_REFCURSOR
    )
    AS
        v_fecha_ini       DATE;
        v_fecha_fin       DATE;
        v_fecha_actual    DATE;
        v_total_dias      NUMBER := 0;
        v_dias_ok         NUMBER := 0;
        v_dias_error      NUMBER := 0;
        v_empresa_f       VARCHAR2(10);   -- empresa normalizado (% -> NULL)
        v_personal_f      VARCHAR2(10);   -- personal normalizado (% -> NULL)
        
        -- Acumuladores totales
        v_tot_nocturno    NUMBER := 0;
        v_tot_entrada     NUMBER := 0;
        v_tot_anticipada  NUMBER := 0;
        v_tot_salida      NUMBER := 0;
        v_tot_inirefri    NUMBER := 0;
        v_tot_finrefri    NUMBER := 0;
        v_tot_anomala     NUMBER := 0;
        v_tot_rn          NUMBER := 0;
        v_tot_recalculo   NUMBER := 0;
        v_tot_generadas   NUMBER := 0;
        v_tot_historial   NUMBER := 0;
        v_tot_sin_tareo   NUMBER := 0;
        
        -- Variables para leer cursor de cada dia
        cv_dia            SYS_REFCURSOR;
        v_resultado       VARCHAR2(500);
        v_fecha_str       VARCHAR2(20);
        v_nocturno        NUMBER;
        v_entrada         NUMBER;
        v_anticipada      NUMBER;
        v_salida          NUMBER;
        v_inirefri        NUMBER;
        v_finrefri        NUMBER;
        v_anomala         NUMBER;
        v_rn              NUMBER;
        v_recalculo       NUMBER;
        v_generadas       NUMBER;
        v_historial       NUMBER;
        v_sin_tareo       NUMBER;
        
        v_error_msg       VARCHAR2(500);
    BEGIN
        v_fecha_ini := TO_DATE(p_fecha_inicio, 'dd/MM/yyyy');
        v_fecha_fin := TO_DATE(p_fecha_fin, 'dd/MM/yyyy');
        
        -- Convertir '%' a NULL para DEPURA_TOTAL (que hace NVL(param, '%'))
        -- Asi se puede llamar con '%' desde Toad DEFINE o con NULL desde PL/SQL
        v_empresa_f  := CASE WHEN p_cod_empresa = '%' THEN NULL ELSE p_cod_empresa END;
        v_personal_f := CASE WHEN p_cod_personal = '%' THEN NULL ELSE p_cod_personal END;
        
        IF v_fecha_fin < v_fecha_ini THEN
            OPEN cv_resultado FOR
                SELECT 'ERROR: fecha_fin debe ser >= fecha_inicio' AS resultado,
                       p_fecha_inicio AS fecha_inicio,
                       p_fecha_fin AS fecha_fin,
                       0 AS total_dias, 0 AS dias_ok, 0 AS dias_error,
                       0 AS turnos_nocturnos, 0 AS entradas, 0 AS anticipadas,
                       0 AS salidas, 0 AS inirefris, 0 AS finrefris,
                       0 AS anomalas, 0 AS nocturnos_sin_refri, 0 AS recalculos,
                       0 AS total_generadas, 0 AS total_historial,
                       0 AS total_empleados_sin_tareo
                FROM DUAL;
            RETURN;
        END IF;
        
        DBMS_OUTPUT.PUT_LINE('================================================================');
        DBMS_OUTPUT.PUT_LINE('DEPURA_RANGO: ' || p_fecha_inicio || ' al ' || p_fecha_fin);
        DBMS_OUTPUT.PUT_LINE('Empresa: ' || NVL(v_empresa_f, 'TODAS') || 
                             ' | Personal: ' || NVL(v_personal_f, 'TODOS') ||
                             ' | Obreros: ' || p_solo_obreros);
        DBMS_OUTPUT.PUT_LINE('================================================================');
        
        v_fecha_actual := v_fecha_ini;
        
        WHILE v_fecha_actual <= v_fecha_fin LOOP
            v_total_dias := v_total_dias + 1;
            v_fecha_str := TO_CHAR(v_fecha_actual, 'dd/MM/yyyy');
            
            BEGIN
                -- Llamar DEPURA_TOTAL para este dia
                DEPURA_TOTAL(
                    p_cod_empresa    => v_empresa_f,
                    p_cod_personal   => v_personal_f,
                    p_fecha          => v_fecha_str,
                    p_solo_obreros   => p_solo_obreros,
                    cv_resultado     => cv_dia
                );
                
                -- Leer resultado del dia
                FETCH cv_dia INTO v_resultado, v_fecha_str,
                    v_nocturno, v_entrada, v_anticipada, v_salida,
                    v_inirefri, v_finrefri, v_anomala, v_rn,
                    v_recalculo, v_generadas, v_historial, v_sin_tareo;
                CLOSE cv_dia;
                
                IF v_resultado = 'OK' THEN
                    v_dias_ok := v_dias_ok + 1;
                    -- Acumular totales
                    v_tot_nocturno   := v_tot_nocturno + NVL(v_nocturno, 0);
                    v_tot_entrada    := v_tot_entrada + NVL(v_entrada, 0);
                    v_tot_anticipada := v_tot_anticipada + NVL(v_anticipada, 0);
                    v_tot_salida     := v_tot_salida + NVL(v_salida, 0);
                    v_tot_inirefri   := v_tot_inirefri + NVL(v_inirefri, 0);
                    v_tot_finrefri   := v_tot_finrefri + NVL(v_finrefri, 0);
                    v_tot_anomala    := v_tot_anomala + NVL(v_anomala, 0);
                    v_tot_rn         := v_tot_rn + NVL(v_rn, 0);
                    v_tot_recalculo  := v_tot_recalculo + NVL(v_recalculo, 0);
                    v_tot_generadas  := v_tot_generadas + NVL(v_generadas, 0);
                    v_tot_historial  := v_tot_historial + NVL(v_historial, 0);
                    v_tot_sin_tareo  := v_tot_sin_tareo + NVL(v_sin_tareo, 0);
                    
                    -- Log por dia
                    DBMS_OUTPUT.PUT_LINE(TO_CHAR(v_fecha_actual, 'dd/MM/yyyy DD') || 
                        ' -> OK | Noct:' || v_nocturno ||
                        ' Ent:' || v_entrada ||
                        ' Sal:' || v_salida ||
                        ' Ref:' || (NVL(v_inirefri,0) + NVL(v_finrefri,0)) ||
                        ' RN:' || NVL(v_rn,0) ||
                        ' RC:' || v_recalculo ||
                        ' Gen:' || v_generadas ||
                        ' Hist:' || v_historial);
                ELSE
                    v_dias_error := v_dias_error + 1;
                    DBMS_OUTPUT.PUT_LINE(TO_CHAR(v_fecha_actual, 'dd/MM/yyyy DD') || 
                        ' -> ' || v_resultado);
                END IF;
                
            EXCEPTION
                WHEN OTHERS THEN
                    v_dias_error := v_dias_error + 1;
                    DBMS_OUTPUT.PUT_LINE(TO_CHAR(v_fecha_actual, 'dd/MM/yyyy DD') || 
                        ' -> ERROR: ' || SQLERRM);
            END;
            
            v_fecha_actual := v_fecha_actual + 1;
        END LOOP;
        
        -- Resumen final
        DBMS_OUTPUT.PUT_LINE('================================================================');
        DBMS_OUTPUT.PUT_LINE('RESUMEN: ' || v_total_dias || ' dias procesados | ' ||
                             v_dias_ok || ' OK | ' || v_dias_error || ' ERROR');
        DBMS_OUTPUT.PUT_LINE('Total marcas generadas: ' || v_tot_generadas || 
                             ' | Historial insertado: ' || v_tot_historial);
        DBMS_OUTPUT.PUT_LINE('================================================================');
        
        -- Cursor con resumen consolidado
        OPEN cv_resultado FOR
            SELECT 'OK' AS resultado,
                   p_fecha_inicio AS fecha_inicio,
                   p_fecha_fin AS fecha_fin,
                   v_total_dias AS total_dias,
                   v_dias_ok AS dias_ok,
                   v_dias_error AS dias_error,
                   v_tot_nocturno AS turnos_nocturnos,
                   v_tot_entrada AS entradas,
                   v_tot_anticipada AS anticipadas,
                   v_tot_salida AS salidas,
                   v_tot_inirefri AS inirefris,
                   v_tot_finrefri AS finrefris,
                   v_tot_anomala AS anomalas,
                   v_tot_rn AS nocturnos_sin_refri,
                   v_tot_recalculo AS recalculos,
                   v_tot_generadas AS total_generadas,
                   v_tot_historial AS total_historial,
                   v_tot_sin_tareo AS total_empleados_sin_tareo
            FROM DUAL;

    EXCEPTION
        WHEN OTHERS THEN
            v_error_msg := SQLERRM;
            DBMS_OUTPUT.PUT_LINE('ERROR FATAL en DEPURA_RANGO: ' || v_error_msg);
            OPEN cv_resultado FOR
                SELECT 'ERROR: ' || v_error_msg AS resultado,
                       p_fecha_inicio AS fecha_inicio,
                       p_fecha_fin AS fecha_fin,
                       v_total_dias AS total_dias,
                       v_dias_ok AS dias_ok,
                       v_dias_error + 1 AS dias_error,
                       0 AS turnos_nocturnos, 0 AS entradas, 0 AS anticipadas,
                       0 AS salidas, 0 AS inirefris, 0 AS finrefris,
                       0 AS anomalas, 0 AS nocturnos_sin_refri, 0 AS recalculos,
                       0 AS total_generadas, 0 AS total_historial,
                       v_tot_sin_tareo AS total_empleados_sin_tareo
                FROM DUAL;

    END DEPURA_RANGO;

    -- =========================================================================
    -- CONSULTAR_RANGO: Retorna cursor con estado de marcaciones por rango
    -- =========================================================================
    PROCEDURE CONSULTAR_RANGO(
        p_cod_empresa      IN VARCHAR2 DEFAULT NULL,
        p_cod_personal     IN VARCHAR2 DEFAULT NULL,
        p_fecha_inicio     IN VARCHAR2,
        p_fecha_fin        IN VARCHAR2,
        cv_resultado       OUT SYS_REFCURSOR
    )
    AS
        v_fecha_ini  DATE;
        v_fecha_fin  DATE;
    BEGIN
        v_fecha_ini := TO_DATE(p_fecha_inicio, 'DD/MM/YYYY');
        v_fecha_fin := TO_DATE(p_fecha_fin,    'DD/MM/YYYY');

        OPEN cv_resultado FOR
            SELECT
                TO_CHAR(t.fechamar, 'DD/MM/YYYY')                                 AS fechamar,
                -- ==================== IDENTIFICACION ====================
                t.cod_empresa                                                      AS emp,
                t.cod_personal                                                     AS personal,
                t.num_fotocheck                                                    AS fotocheck,
                INITCAP(p.ape_paterno || ' ' || p.ape_materno
                        || ', ' || p.nom_trabajador)                               AS empleado,
                tp.des_tipo_planilla                                               AS planilla,

                -- ==================== HORARIO TEORICO (SCA_HORARIO_DET) ==============
                NVL(hd.descanso, 'N')                                              AS hor_descanso,
                TO_CHAR(hd.horing,    'HH24:MI')                                  AS hor_entrada,
                TO_CHAR(hd.horsal,    'HH24:MI')                                  AS hor_salida,
                TO_CHAR(hd.horiniref, 'HH24:MI')                                  AS hor_ini_ref,
                TO_CHAR(hd.horfinref, 'HH24:MI')                                  AS hor_fin_ref,
                TO_CHAR(hd.totref,    'HH24:MI')                                  AS hor_refri,
                TO_CHAR(hd.tothoras,  'HH24:MI')                                  AS hor_total_hrs,

                -- ==================== HORARIO FIJADO EN TAREO =======================

                -- ==================== MARCACIONES ACTUALES ==========================
                t.nummarcaciones                                                   AS n_marcas,
                TO_CHAR(t.entrada,  'HH24:MI:SS')                                 AS entrada,
                TO_CHAR(t.inirefri, 'HH24:MI:SS')                                 AS ini_refri,
                TO_CHAR(t.finrefri, 'HH24:MI:SS')                                 AS fin_refri,
                TO_CHAR(t.salida,   'HH24:MI:SS')                                 AS salida,

                -- ==================== AUTO vs REAL ====================

                -- ==================== MARCAS EN SCA_HISTORIAL =======================

                -- ==================== HORAS (campos que DEPURA_TAREO recalcula) =====
                TO_CHAR(t.tothoras,           'HH24:MI')                          AS hrs_teoricas,
                TO_CHAR(t.tothoramarcas,      'HH24:MI')                          AS hrs_brutas,
                TO_CHAR(t.horarefrigerio,     'HH24:MI')                          AS hrs_refri,
                TO_CHAR(t.horaefectiva,       'HH24:MI')                          AS hrs_efect,
                TO_CHAR(t.horatardanza,       'HH24:MI')                          AS tardanza,
                TO_CHAR(t.horadobles,         'HH24:MI')                          AS hrs_dobles,
                TO_CHAR(t.horas_no_trabajadas,'HH24:MI')                          AS hrs_no_trab,

                -- ==================== HORAS EXTRAS ==================================
                TO_CHAR(t.horaantesentrada,   'HH24:MI')                          AS t_anticipado,
                TO_CHAR(t.horaextantes,       'HH24:MI')                          AS he_antes,
                TO_CHAR(t.horaextra,          'HH24:MI')                          AS he_desp,
                TO_CHAR(t.totalhorasextras,   'HH24:MI')                          AS he_total,
                TO_CHAR(t.horaextra1,         'HH24:MI')                          AS he_25,
                TO_CHAR(t.horaextra2,         'HH24:MI')                          AS he_35,
                TO_CHAR(t.horaextra3,         'HH24:MI')                          AS he_50,
                TO_CHAR(t.horaextra_ajus,     'HH24:MI')                          AS he_ajus,
                NVL(t.hayhea_poraut, 'N')                                          AS he_ant_aut,
                NVL(t.hayhed_poraut, 'N')                                          AS he_des_aut,

                -- ==================== HORAS NOCTURNAS ================================
                TO_CHAR(t.tothoranocturna,    'HH24:MI')                          AS hrs_noct,
                TO_CHAR(t.tothoranocturna_of, 'HH24:MI')                          AS hrs_noct_of,

                -- ==================== INDICADORES ====================
                NVL(t.descanso,         'N')                                       AS descanso,
                NVL(t.feriado,          'N')                                       AS feriado,
                NVL(t.ind_cerrado,      'N')                                       AS cerrado,
                NVL(t.descansorotativo, 'N')                                       AS desc_rot,

                -- ==================== PERMISOS/AUSENCIAS ====================
                CASE WHEN t.per_desc_med   IS NOT NULL THEN 'DM'  ELSE NULL END ||
                CASE WHEN t.per_subsidio   IS NOT NULL THEN '|SB' ELSE NULL END ||
                CASE WHEN t.per_goce       IS NOT NULL THEN '|CG' ELSE NULL END ||
                CASE WHEN t.per_sgoce      IS NOT NULL THEN '|SG' ELSE NULL END ||
                CASE WHEN t.per_vaca       IS NOT NULL THEN '|VA' ELSE NULL END ||
                CASE WHEN t.per_suspension IS NOT NULL THEN '|SU' ELSE NULL END ||
                CASE WHEN t.per_lic_pat    IS NOT NULL THEN '|LP' ELSE NULL END ||
                CASE WHEN t.per_lic_fac    IS NOT NULL THEN '|LF' ELSE NULL END   AS permisos,

                -- ==================== ALERTAS / ERRORES =============================
                t.alerta01                                                         AS a01_impar,
                t.alerta03                                                         AS a03_hor_inc,
                t.alerta06                                                         AS a06_hextra,

                -- ==================== DEPURACION YA APLICADA ====================
                t.codaux4                                                          AS cod_dep,
                t.codaux5                                                          AS desc_dep,

                -- ==================== ESTADO PENDIENTE ==============================
                -- S = necesita depuracion / D = ya depurado (tiene cod_dep) / N = OK
                CASE
                    WHEN NVL(t.ind_cerrado, 'N') = 'S'
                        THEN 'N'
                    WHEN t.codaux4 IS NOT NULL
                        THEN 'D'
                    WHEN NVL(t.descanso,'N') = 'S'
                         AND (t.entrada IS NOT NULL OR t.salida IS NOT NULL)
                        THEN 'S'
                    WHEN (t.entrada IS NULL OR t.salida IS NULL)
                         AND NVL(t.descanso,'N') <> 'S'
                         AND t.per_desc_med IS NULL AND t.per_subsidio IS NULL
                         AND t.per_goce IS NULL AND t.per_sgoce IS NULL
                         AND t.per_vaca IS NULL AND t.per_suspension IS NULL
                         AND t.per_lic_pat IS NULL AND t.per_lic_fac IS NULL
                        THEN 'S'
                    WHEN t.inirefri IS NULL AND t.finrefri IS NULL
                         AND t.entrada IS NOT NULL AND t.salida IS NOT NULL
                         AND t.horiniref IS NOT NULL
                         AND TO_CHAR(t.horiniref,'HH24:MI') <> '00:00'
                         AND NVL(t.descanso,'N') <> 'S'
                        THEN 'S'
                    WHEN t.entrada IS NOT NULL AND t.salida IS NOT NULL
                         AND ((t.inirefri IS NULL AND t.finrefri IS NOT NULL)
                           OR (t.inirefri IS NOT NULL AND t.finrefri IS NULL))
                        THEN 'S'
                    ELSE 'N'
                END                                                                AS pendiente,

                -- ==================== CASO QUE APLICA ==============================
                CASE
                    -- PASO 6-PHANTOM-C
                    WHEN t.descanso = 'S'
                         AND t.entrada IS NOT NULL
                         AND NVL(t.codaux4, ' ') NOT LIKE '%PH%'
                         AND NOT EXISTS (
                            SELECT 1 FROM SCA_HISTORIAL h
                            WHERE h.idtarjeta = t.num_fotocheck
                            AND h.fec_equiv = t.fechamar
                            AND RTRIM(h.hora) = TO_CHAR(t.entrada, 'HH24:MI:SS')
                            AND NVL(h.ind_anulado, 'N') <> 'S'
                            AND NVL(h.ind_noprocesar, 0) = 0
                            AND NVL(h.motivo, ' ') NOT LIKE 'DEPURACION%'
                         )
                         AND EXISTS (
                            SELECT 1 FROM SCA_ASISTENCIA_TAREO t_ayer
                            WHERE t_ayer.fechamar   = t.fechamar - 1
                            AND t_ayer.cod_empresa  = t.cod_empresa
                            AND t_ayer.cod_personal = t.cod_personal
                            AND t_ayer.entrada      IS NOT NULL
                            AND TO_CHAR(t_ayer.entrada, 'HH24MI') >= '1800'
                         )
                        THEN 'PASO 6-PH-C: Descanso entrada fantasma (no en SCA_HIS)'
                    -- PASO 6-PHANTOM-D
                    WHEN t.descanso = 'S'
                         AND t.entrada IS NOT NULL
                         AND TO_CHAR(t.entrada, 'HH24MI') < '0800'
                         AND NVL(t.codaux4, ' ') NOT LIKE '%PH%'
                         AND EXISTS (
                            SELECT 1 FROM SCA_ASISTENCIA_TAREO t_ayer
                            WHERE t_ayer.fechamar   = t.fechamar - 1
                            AND t_ayer.cod_empresa  = t.cod_empresa
                            AND t_ayer.cod_personal = t.cod_personal
                            AND t_ayer.entrada      IS NOT NULL
                            AND TO_CHAR(t_ayer.entrada, 'HH24MI') >= '1800'
                         )
                        THEN 'PASO 6-PH-D: Descanso entrada madrugada (salida turno noct ant)'
                    -- PASO 6-PHANTOM-B
                    WHEN t.descanso = 'S'
                         AND t.entrada IS NULL
                         AND t.salida IS NOT NULL
                         AND TO_CHAR(t.salida, 'HH24MI') < '1200'
                         AND NVL(t.codaux4, ' ') NOT LIKE '%PH%'
                         AND EXISTS (
                            SELECT 1 FROM SCA_ASISTENCIA_TAREO t_ayer
                            WHERE t_ayer.fechamar   = t.fechamar - 1
                            AND t_ayer.cod_empresa  = t.cod_empresa
                            AND t_ayer.cod_personal = t.cod_personal
                            AND t_ayer.entrada      IS NOT NULL
                            AND TO_CHAR(t_ayer.entrada, 'HH24MI') >= '1800'
                         )
                        THEN 'PASO 6-PH-B: Descanso salida fantasma (noct dia ant)'
                    -- PASO 6-PHANTOM
                    WHEN t.descanso = 'S'
                         AND (t.entrada IS NOT NULL OR t.salida IS NOT NULL)
                         AND NOT EXISTS (
                            SELECT 1 FROM SCA_HISTORIAL h
                            WHERE h.idtarjeta = t.num_fotocheck
                            AND h.fec_equiv   = t.fechamar
                            AND NVL(h.ind_anulado,    'N') <> 'S'
                            AND NVL(h.ind_noprocesar, 0)  =  0
                            AND NVL(h.motivo, ' ') NOT LIKE 'DEPURACION%'
                         )
                        THEN 'PASO 6-PH: Descanso fantasma (0 marcas SCA_HIS)'
                    -- PASO 0-SWAP
                    WHEN t.entrada IS NOT NULL AND t.salida IS NOT NULL
                         AND TO_CHAR(t.entrada, 'HH24MI') < '0800'
                         AND TO_CHAR(t.salida,  'HH24MI') >= '1800'
                         AND TO_CHAR(t.entrada, 'HH24:MI:SS') <> TO_CHAR(t.salida, 'HH24:MI:SS')
                         AND EXISTS (
                            SELECT 1 FROM SCA_ASISTENCIA_TAREO t_ayer
                            WHERE t_ayer.fechamar    = t.fechamar - 1
                            AND t_ayer.cod_empresa   = t.cod_empresa
                            AND t_ayer.cod_personal  = t.cod_personal
                            AND t_ayer.salida        IS NOT NULL
                            AND TO_CHAR(t_ayer.salida, 'HH24:MI:SS') = TO_CHAR(t.entrada, 'HH24:MI:SS')
                         )
                        THEN 'PASO 0-SWAP: Campos invertidos (E=' ||
                             TO_CHAR(t.entrada,'HH24:MI') || ' S=' || TO_CHAR(t.salida,'HH24:MI') || ')'
                    -- PASO 0A/0B
                    WHEN t.entrada IS NOT NULL
                         AND TO_CHAR(t.entrada, 'HH24MI') < '0800'
                         AND EXISTS (
                            SELECT 1 FROM SCA_ASISTENCIA_TAREO t_ayer
                            WHERE t_ayer.fechamar    = t.fechamar - 1
                            AND t_ayer.cod_empresa   = t.cod_empresa
                            AND t_ayer.cod_personal  = t.cod_personal
                            AND t_ayer.entrada       IS NOT NULL
                            AND TO_CHAR(t_ayer.entrada, 'HH24MI') >= '1800'
                            AND t_ayer.salida IS NULL
                            AND NVL(t_ayer.ind_cerrado, 'N') <> 'S'
                         )
                        THEN 'PASO 0A/0B: Noct entrada->salida ayer'
                    -- PASO 0B3
                    WHEN t.entrada IS NOT NULL
                         AND TO_CHAR(t.entrada, 'HH24MI') >= '1800'
                         AND t.salida IS NULL
                         AND EXISTS (
                            SELECT 1 FROM SCA_ASISTENCIA_TAREO t_sig
                            WHERE t_sig.fechamar   = t.fechamar + 1
                            AND t_sig.cod_empresa  = t.cod_empresa
                            AND t_sig.cod_personal = t.cod_personal
                            AND t_sig.entrada      IS NOT NULL
                            AND TO_CHAR(t_sig.entrada, 'HH24MI') < '0800'
                         )
                        THEN 'PASO 0B3: Noct forward'
                    -- PASO 0C
                    WHEN t.entrada IS NULL AND t.salida IS NOT NULL
                         AND TO_CHAR(t.salida, 'HH24MI') >= '2000'
                        THEN 'PASO 0C: Salida noct sin entrada'
                    -- PASO 0D
                    WHEN t.entrada IS NOT NULL AND t.salida IS NULL
                         AND t.entrada_fijada IS NOT NULL
                         AND TO_CHAR(t.entrada_fijada, 'HH24MI') >= '2000'
                         AND TO_CHAR(t.entrada, 'HH24MI') < '1200'
                         AND t.nummarcaciones <= 1 AND NVL(t.descanso, 'N') <> 'S'
                        THEN 'PASO 0D: Noct marca manana->salida'
                    -- PASO 1C-NOC
                    WHEN t.entrada IS NOT NULL AND t.salida IS NOT NULL
                         AND t.entrada = t.salida AND t.nummarcaciones <= 2
                         AND t.entrada_fijada IS NOT NULL
                         AND TO_CHAR(t.entrada_fijada, 'HH24MI') >= '2000'
                         AND TO_CHAR(t.entrada,        'HH24MI') <  '1200'
                         AND NVL(t.descanso, 'N') <> 'S'
                        THEN 'PASO 1C-NOC: Duplicada nocturna'
                    -- PASO 1C
                    WHEN t.entrada IS NOT NULL AND t.salida IS NOT NULL
                         AND t.entrada = t.salida AND t.nummarcaciones <= 2
                         AND t.salida_fijada IS NOT NULL AND t.salida <> t.salida_fijada
                         AND NVL(t.descanso, 'N') <> 'S'
                        THEN 'PASO 1C: Entrada=Salida duplicada'
                    -- PASO 1
                    WHEN t.entrada IS NULL AND t.salida IS NOT NULL
                         AND t.entrada_fijada IS NOT NULL AND NVL(t.descanso, 'N') <> 'S'
                         AND t.per_desc_med IS NULL AND t.per_subsidio IS NULL
                         AND t.per_goce IS NULL AND t.per_sgoce IS NULL
                         AND t.per_vaca IS NULL AND t.per_suspension IS NULL
                         AND t.per_lic_pat IS NULL AND t.per_lic_fac IS NULL
                        THEN 'PASO 1: Completar ENTRADA'
                    -- PASO 5G
                    WHEN t.entrada IS NOT NULL AND t.entrada_fijada IS NOT NULL
                         AND TO_CHAR(t.entrada_fijada, 'HH24MI') >= '2200'
                         AND t.entrada < t.entrada_fijada - (2/24)
                         AND t.inirefri IS NULL AND t.finrefri IS NULL
                         AND t.salida IS NOT NULL AND NVL(t.descanso, 'N') <> 'S'
                        THEN 'PASO 5G: 3er turno anticipado ' ||
                             TRIM(TO_CHAR(ROUND((t.entrada_fijada - t.entrada)*60), '999')) || 'min antes'
                    -- PASO 1B
                    WHEN t.entrada IS NOT NULL AND t.entrada_fijada IS NOT NULL
                         AND t.entrada < t.entrada_fijada - (15/1440)
                         AND NVL(t.descanso, 'N') <> 'S' AND NVL(t.hayhea_poraut, 'N') <> 'S'
                         AND NOT (TO_CHAR(t.entrada_fijada, 'HH24MI') >= '2200'
                                  AND t.entrada < t.entrada_fijada - (2/24))
                        THEN 'PASO 1B: Anticipada ' ||
                             TRIM(TO_CHAR(ROUND((t.entrada_fijada - t.entrada)*1440), '999')) || 'min'
                    -- PASO 2
                    WHEN t.salida IS NULL AND t.entrada IS NOT NULL
                         AND t.salida_fijada IS NOT NULL AND NVL(t.descanso, 'N') <> 'S'
                         AND t.per_desc_med IS NULL AND t.per_subsidio IS NULL
                         AND t.per_goce IS NULL AND t.per_sgoce IS NULL
                         AND t.per_vaca IS NULL AND t.per_suspension IS NULL
                         AND t.per_lic_pat IS NULL AND t.per_lic_fac IS NULL
                        THEN 'PASO 2: Completar SALIDA'
                    -- PASO 4B
                    WHEN t.entrada IS NOT NULL AND t.salida IS NOT NULL
                         AND (t.salida - t.entrada) < (60/1440)
                         AND (t.inirefri IS NOT NULL OR t.finrefri IS NOT NULL)
                         AND t.salida_fijada IS NOT NULL
                         AND t.horiniref IS NOT NULL AND t.horfinref IS NOT NULL
                         AND NVL(t.descanso, 'N') <> 'S'
                        THEN 'PASO 4B: Anomala <' ||
                             TRIM(TO_CHAR(ROUND((t.salida - t.entrada)*1440), '99')) || 'min'
                    -- ENTRADA TARDE: no aplica PASO 2B (queda para analisis manual)
                    -- FIX 23/04/2026: si empleado llego despues de horiniref, no se asigna
                    -- refri teorico (PASO 2B esta bloqueado) -> requiere intervencion manual
                    WHEN t.inirefri IS NULL AND t.finrefri IS NULL
                         AND t.entrada IS NOT NULL AND t.salida IS NOT NULL
                         AND t.horiniref IS NOT NULL AND t.horfinref IS NOT NULL
                         AND TO_CHAR(t.horiniref, 'HH24:MI') <> '00:00'
                         AND TO_CHAR(t.entrada, 'HH24MI') > TO_CHAR(t.horiniref, 'HH24MI')
                         AND NOT (t.entrada_fijada IS NOT NULL AND t.salida_fijada IS NOT NULL
                                  AND t.salida_fijada < t.entrada_fijada)
                         AND NVL(t.descanso, 'N') <> 'S'
                         AND t.per_desc_med IS NULL AND t.per_subsidio IS NULL
                         AND t.per_goce IS NULL AND t.per_sgoce IS NULL
                         AND t.per_vaca IS NULL AND t.per_suspension IS NULL
                         AND t.per_lic_pat IS NULL AND t.per_lic_fac IS NULL
                        THEN 'MANUAL: Entrada tarde (despues de horiniref) - sin refri'
                    -- PASO 2B
                    WHEN t.inirefri IS NULL AND t.finrefri IS NULL
                         AND t.entrada IS NOT NULL AND t.salida IS NOT NULL
                         AND t.horiniref IS NOT NULL AND t.horfinref IS NOT NULL
                         AND TO_CHAR(t.horiniref, 'HH24:MI') <> '00:00'
                         AND NVL(t.descanso, 'N') <> 'S'
                         AND t.per_desc_med IS NULL AND t.per_subsidio IS NULL
                         AND t.per_goce IS NULL AND t.per_sgoce IS NULL
                         AND t.per_vaca IS NULL AND t.per_suspension IS NULL
                         AND t.per_lic_pat IS NULL AND t.per_lic_fac IS NULL
                        THEN 'PASO 2B: Completar refri completo'
                    -- PASO 3A
                    -- FIX 21/04/2026: aceptar totref valido O horfinref-horiniref calculable
                    WHEN t.inirefri IS NULL AND t.finrefri IS NOT NULL
                         AND (
                             (t.totref IS NOT NULL AND t.totref <> TO_DATE('01/01/1900', 'DD/MM/YYYY'))
                             OR
                             (t.horfinref IS NOT NULL AND t.horiniref IS NOT NULL
                              AND t.horfinref > t.horiniref
                              AND t.horfinref <> TO_DATE('01/01/1900', 'dd/MM/yyyy'))
                         )
                         AND t.entrada IS NOT NULL AND t.salida IS NOT NULL
                         AND NVL(t.descanso, 'N') <> 'S'
                         AND t.per_desc_med IS NULL AND t.per_subsidio IS NULL
                         AND t.per_goce IS NULL AND t.per_sgoce IS NULL
                         AND t.per_vaca IS NULL AND t.per_suspension IS NULL
                         AND t.per_lic_pat IS NULL AND t.per_lic_fac IS NULL
                        THEN 'PASO 3A: Calcular INIREFRI'
                    -- PASO 3B
                    -- FIX 21/04/2026: aceptar totref valido O horfinref-horiniref calculable
                    WHEN t.finrefri IS NULL AND t.inirefri IS NOT NULL
                         AND (
                             (t.totref IS NOT NULL AND t.totref <> TO_DATE('01/01/1900', 'DD/MM/YYYY'))
                             OR
                             (t.horfinref IS NOT NULL AND t.horiniref IS NOT NULL
                              AND t.horfinref > t.horiniref
                              AND t.horfinref <> TO_DATE('01/01/1900', 'dd/MM/yyyy'))
                         )
                         AND t.entrada IS NOT NULL AND t.salida IS NOT NULL
                         AND NVL(t.descanso, 'N') <> 'S'
                         AND t.per_desc_med IS NULL AND t.per_subsidio IS NULL
                         AND t.per_goce IS NULL AND t.per_sgoce IS NULL
                         AND t.per_vaca IS NULL AND t.per_suspension IS NULL
                         AND t.per_lic_pat IS NULL AND t.per_lic_fac IS NULL
                        THEN 'PASO 3B: Calcular FINREFRI'
                    -- PASO 3C-NOC
                    WHEN t.entrada_fijada IS NOT NULL
                         AND TO_CHAR(t.entrada_fijada, 'HH24MI') >= '2200'
                         AND t.entrada IS NOT NULL
                         AND (TO_NUMBER(TO_CHAR(t.entrada, 'HH24')) >= 20
                              OR TO_NUMBER(TO_CHAR(t.entrada, 'HH24')) < 2)
                         AND (t.inirefri IS NOT NULL OR t.finrefri IS NOT NULL)
                         AND t.salida IS NOT NULL AND NVL(t.descanso, 'N') <> 'S'
                        THEN 'PASO 3C-NOC: Noct sin anticipacion -> limpiar refri'
                    -- PASO 5B-TAG
                    WHEN t.salida IS NOT NULL AND t.salida_fijada IS NOT NULL
                         AND t.salida > t.salida_fijada
                         AND (t.salida - t.salida_fijada) * 24 < 1
                         AND t.horaextra IS NOT NULL
                         AND t.horaextra > TO_DATE('01/01/1900', 'dd/MM/yyyy')
                         AND NVL(t.descanso, 'N') <> 'S' AND NVL(t.hayhed_poraut, 'N') <> 'S'
                        THEN 'PASO 5B-TAG: HExtra <1h (' ||
                             TRIM(TO_CHAR(ROUND((t.salida - t.salida_fijada)*1440), '99')) || 'min)'
                    -- PASO 5B-TAG2: HE calculada pero no oficializada por el tareo
                    WHEN t.entrada IS NOT NULL AND t.salida IS NOT NULL
                         AND t.salida_fijada IS NOT NULL
                         AND t.horaextra IS NOT NULL
                         AND t.horaextra >= TO_DATE('01/01/1900 01:00','dd/MM/yyyy HH24:MI')
                         AND (t.horaextra_ajus IS NULL
                              OR t.horaextra_ajus = TO_DATE('01/01/1900','dd/MM/yyyy'))
                         AND NVL(t.descanso, 'N') <> 'S'
                         AND NVL(t.ind_cerrado, 'N') <> 'S'
                         AND NVL(t.hayhed_poraut, 'N') <> 'S'
                        THEN 'PASO 5B-TAG2: HE no oficializada (ajus=0) -> reoficializar'
                    -- PASO 7A+7
                    WHEN t.descanso = 'S' AND t.entrada IS NOT NULL AND t.salida IS NOT NULL
                         AND t.horid IS NOT NULL AND NVL(t.hayhea_poraut, 'N') <> 'S'
                         AND EXISTS (
                            SELECT 1 FROM SCA_HORARIO_DET d
                            WHERE d.horid = t.horid AND NVL(d.descanso, 'N') <> 'S'
                            AND d.horing > TO_DATE('01/01/1900', 'dd/MM/yyyy')
                         )
                         AND t.entrada < (
                            TRUNC(t.entrada) + (
                                SELECT MIN(d.horing) - TO_DATE('01/01/1900', 'dd/MM/yyyy')
                                FROM SCA_HORARIO_DET d
                                WHERE d.horid = t.horid AND NVL(d.descanso, 'N') <> 'S'
                                AND d.horing > TO_DATE('01/01/1900', 'dd/MM/yyyy')
                            ) - 15/1440
                         )
                        THEN 'PASO 7A+7: Descanso anticipado + recalculo dobles'
                    -- PASO 7
                    WHEN t.descanso = 'S' AND t.entrada IS NOT NULL AND t.salida IS NOT NULL
                        THEN 'PASO 7: Descanso con marcas -> recalc dobles'
                    -- PASO 5
                    WHEN t.entrada IS NOT NULL AND t.salida IS NOT NULL
                         AND NVL(t.descanso, 'N') <> 'S'
                        THEN 'PASO 5: Solo recalculo horas'
                    ELSE NULL
                END                                                                AS caso_aplica,

                -- ==================== DIAGNOSTICO GENERAL ==========================
                CASE
                    WHEN NVL(t.descanso, 'N') = 'S'
                         AND (t.entrada IS NOT NULL OR t.salida IS NOT NULL)
                         AND NOT EXISTS (
                            SELECT 1 FROM SCA_HISTORIAL h
                            WHERE h.idtarjeta = t.num_fotocheck
                            AND h.fec_equiv   = t.fechamar
                            AND NVL(h.ind_anulado,    'N') <> 'S'
                            AND NVL(h.ind_noprocesar, 0)  =  0
                            AND NVL(h.motivo, ' ') NOT LIKE 'DEPURACION%'
                         )
                        THEN 'DESCANSO FANTASMA (0 marcas SCA_HIS)'
                    WHEN NVL(t.descanso, 'N') = 'S'
                         AND t.entrada IS NOT NULL AND t.salida IS NOT NULL
                        THEN 'DESCANSO CON MARCACIONES'
                    WHEN NVL(t.descanso, 'N') = 'S' THEN 'DESCANSO'
                    WHEN t.per_desc_med IS NOT NULL OR t.per_subsidio IS NOT NULL
                         OR t.per_goce IS NOT NULL OR t.per_sgoce IS NOT NULL
                         OR t.per_vaca IS NOT NULL OR t.per_suspension IS NOT NULL
                         OR t.per_lic_pat IS NOT NULL OR t.per_lic_fac IS NOT NULL
                        THEN 'PERMISO ACTIVO'
                    WHEN t.entrada IS NULL AND t.salida IS NULL THEN 'SIN MARCACIONES'
                    WHEN t.entrada IS NOT NULL AND t.salida IS NULL
                         AND TO_CHAR(t.entrada, 'HH24MI') >= '1800'
                        THEN 'NOCTURNO: Entrada sin salida (forward/teorica)'
                    WHEN t.entrada IS NOT NULL AND t.salida IS NULL THEN 'FALTA SALIDA'
                    WHEN t.entrada IS NULL AND t.salida IS NOT NULL
                         AND TO_CHAR(t.salida, 'HH24MI') >= '2000'
                        THEN 'SALIDA NOCTURNA MAL UBICADA'
                    WHEN t.entrada IS NULL AND t.salida IS NOT NULL THEN 'FALTA ENTRADA'
                    WHEN t.entrada IS NOT NULL AND t.salida IS NOT NULL
                         AND t.entrada = t.salida THEN 'MARCA DUPLICADA (entrada=salida)'
                    WHEN t.entrada IS NOT NULL AND t.salida IS NOT NULL
                         AND (t.salida - t.entrada) < (60/1440)
                         AND (t.inirefri IS NOT NULL OR t.finrefri IS NOT NULL)
                        THEN 'ANOMALA: 4 marcas en <1hr'
                    WHEN t.entrada IS NOT NULL AND t.salida IS NOT NULL
                         AND t.inirefri IS NULL AND t.finrefri IS NULL
                         AND t.horiniref IS NOT NULL
                         AND TO_CHAR(t.horiniref, 'HH24:MI') <> '00:00'
                         AND TO_CHAR(t.entrada, 'HH24MI') > TO_CHAR(t.horiniref, 'HH24MI')
                         AND NOT (t.entrada_fijada IS NOT NULL AND t.salida_fijada IS NOT NULL
                                  AND t.salida_fijada < t.entrada_fijada)
                        THEN 'ENTRADA TARDE sin refri (revisar manualmente)'
                    WHEN t.entrada IS NOT NULL AND t.salida IS NOT NULL
                         AND t.inirefri IS NULL AND t.finrefri IS NULL
                         AND t.horiniref IS NOT NULL
                         AND TO_CHAR(t.horiniref, 'HH24:MI') <> '00:00'
                        THEN 'FALTA REFRIGERIO'
                    WHEN t.entrada IS NOT NULL AND t.salida IS NOT NULL
                         AND ((t.inirefri IS NULL AND t.finrefri IS NOT NULL)
                           OR (t.inirefri IS NOT NULL AND t.finrefri IS NULL))
                        THEN 'REFRIGERIO INCOMPLETO'
                    WHEN t.entrada IS NOT NULL AND t.salida IS NOT NULL
                         AND t.entrada_fijada IS NOT NULL
                         AND TO_CHAR(t.entrada_fijada, 'HH24MI') >= '2200'
                         AND (TO_NUMBER(TO_CHAR(t.entrada, 'HH24')) >= 20
                              OR TO_NUMBER(TO_CHAR(t.entrada, 'HH24')) < 2)
                         AND (t.inirefri IS NOT NULL OR t.finrefri IS NOT NULL)
                        THEN 'NOCTURNO CON REFRI (no corresponde)'
                    WHEN t.entrada IS NOT NULL AND t.salida IS NOT NULL
                         AND NVL(t.ind_cerrado, 'N') <> 'S'
                        THEN 'OK (recalculo horas)'
                    ELSE 'OK'
                END                                                                AS problema

            FROM SCA_ASISTENCIA_TAREO t
            INNER JOIN PLA_PERSONAL p
                ON  t.cod_empresa  = p.cod_empresa
                AND t.cod_personal = p.cod_personal
            LEFT JOIN PLA_TIPO_PLANILLA tp
                ON  t.cod_empresa       = tp.cod_empresa
                AND t.cod_tipo_planilla = tp.cod_tipo_planilla
            LEFT JOIN SCA_HORARIO_CAB hc
                ON  t.horid = hc.horid
            LEFT JOIN SCA_HORARIO_DET hd
                ON  hc.horid = hd.horid
                AND hd.diaid = ProcessDay(t.fechamar)
            WHERE t.fechamar BETWEEN v_fecha_ini AND v_fecha_fin
            AND (p_cod_empresa  IS NULL OR t.cod_empresa  LIKE p_cod_empresa)
            AND (p_cod_personal IS NULL OR t.cod_personal LIKE p_cod_personal)
            AND NVL(t.ind_cerrado, 'N') <> 'S'
            ORDER BY
                p.ape_paterno, p.ape_materno, p.nom_trabajador,
                t.cod_empresa, t.cod_personal,
                t.fechamar;

    EXCEPTION
        WHEN OTHERS THEN
            IF cv_resultado%ISOPEN THEN CLOSE cv_resultado; END IF;
            RAISE;
    END CONSULTAR_RANGO;

    -- =========================================================================
    -- BUSCAR_EMPLEADO: Busca empleados activos por nombre
    -- PERFORMANCE FIX (27/04/2026): Eliminado JOIN con SCA_ASISTENCIA_TAREO.
    -- La version anterior escaneaba toda esa tabla (>1M filas) y hacia GROUP BY
    -- solo para deduplicar cod_personal y obtener num_fotocheck.
    -- Ahora consulta PLA_PERSONAL (tabla de catalogo, ~258 filas) y obtiene
    -- el fotocheck activo directamente de SCA_FOTOCHECK (~265 filas).
    -- Reduccion: de >1M filas procesadas a ~523 filas. Sin GROUP BY.
    -- =========================================================================
    PROCEDURE BUSCAR_EMPLEADO(
        p_cod_empresa   IN VARCHAR2,
        p_nombre        IN VARCHAR2 DEFAULT NULL,
        cv_resultado    OUT SYS_REFCURSOR
    )
    AS
    BEGIN
        OPEN cv_resultado FOR
            SELECT
                p.cod_personal AS personal,
                -- Fotocheck activo vigente (MAX id_fotocheck como desempate si hubiera varios)
                (SELECT MAX(f.num_fotocheck)
                    KEEP (DENSE_RANK LAST ORDER BY f.id_fotocheck)
                 FROM SCA_FOTOCHECK f
                 WHERE f.cod_empresa   = p.cod_empresa
                   AND f.cod_personal  = p.cod_personal
                   AND f.act_fotocheck = 1
                ) AS fotocheck,
                INITCAP(p.ape_paterno || ' ' || p.ape_materno || ', ' || p.nom_trabajador) AS empleado,
                p.tip_estado
            FROM PLA_PERSONAL p
            WHERE p.cod_empresa = p_cod_empresa
              AND p.tip_estado  = 'AC'
              AND (p_nombre IS NULL
                   OR UPPER(p.ape_paterno || ' ' || p.ape_materno || ', ' || p.nom_trabajador)
                      LIKE '%' || UPPER(p_nombre) || '%')
            ORDER BY
                p.ape_paterno, p.ape_materno, p.nom_trabajador, p.cod_personal;

    EXCEPTION
        WHEN OTHERS THEN
            IF cv_resultado%ISOPEN THEN CLOSE cv_resultado; END IF;
            RAISE;
    END BUSCAR_EMPLEADO;

END PKG_SCA_DEPURA_TAREO;
/
