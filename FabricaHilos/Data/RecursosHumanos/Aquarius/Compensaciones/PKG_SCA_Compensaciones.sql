/*******************************************************************************
 * PKG_SCA_COMPENSACIONES
 * -----------------------------------------------------------------------------
 * Paquete completo para gestion de compensaciones del sistema AQUARIUS.
 *
 * PROPOSITO:
 *   Encapsular en un unico paquete toda la logica de COMPENSACIONES (alta,
 *   baja, validacion, aplicacion al tareo, reversa, consulta) para ser
 *   invocado tanto desde la base de datos (PL/SQL) como desde un sistema
 *   externo (.NET / Java / web) via SYS_REFCURSOR.
 *
 * MODELO:
 *   - SCA_COMPENSACION      : tabla maestra de compensaciones (TIEMPO en MIN)
 *   - SCA_ASISTENCIA_TAREO  : tareo diario donde se aplican los efectos
 *   - SCA_BANCOHORAS_MES    : saldo mensual del banco de horas
 *   - SCA_BANCOHORAS_SEM    : saldo semanal del banco de horas
 *   - SCA_PERMISO_DET       : se actualiza tiempo_compensado para tipo 'P'
 *
 * TIPOORIGEN  (de donde sale el tiempo, valida en FECHAORIGEN):
 *   E = Horas Extras       (campo HORAEXTRA_AJUS)
 *   D = Horas Dobles Of.   (campo HORADOBLESOF)
 *   B = Banco de Horas dia (campo HORABANCOH)
 *   I = Intercambio        (no valida en tareo, va al banco mes/sem)
 *
 * TIPOCOMPENSACION  (a que se aplica, valida en FECHADESTINO):
 *   A = Antes de Salida    (HORAANTESALIDA)   validacion exacta
 *   T = Tardanza           (HORATARDANZA)     validacion exacta
 *   N = No Trabajadas      (HORAS_NO_TRABAJADAS) validacion exacta
 *   F = Faltas             (HORAS_FALTA)      validacion >=
 *   P = Permisos           (HORAPERMISO)      validacion >=
 *   I = Intercambio        (sin destino diario)
 *
 * ALERTAS DE COMPENSACION (post-aplicacion):
 *   ALERTA02 = 'FC' Falta Compensada
 *   ALERTA03 = 'HC' Horas no trabajadas Compensadas
 *   ALERTA04 = 'TC' Tardanza Compensada
 *   ALERTA06 = 'EC' Extras / Banco Compensado (origen consumido)
 *   ALERTA07 = 'SC' Salida antes Compensada
 *   ALERTA08 = 'DC' Dobles Compensadas
 *   ALERTA09 = 'PC' Permiso Compensado
 *
 * IDEMPOTENCIA Y AUDITORIA:
 *   - APLICAR_DIA puede re-ejecutarse sin duplicar (las alertas TC/SC/EC...
 *     marcan el estado aplicado).
 *   - REVERTIR_DIA deshace los efectos en el tareo SIN borrar la fila de
 *     SCA_COMPENSACION (a diferencia de PASO 15 que la borra cuando no
 *     cuadra).
 *   - REGISTRAR/ELIMINAR replican la logica de sp_SCA_Insert_Compensacion
 *     / sp_SCA_Delete_Compensacion (incluyendo banco y permiso).
 *
 * RELACION CON EL SISTEMA NATIVO (PASO 15 de SP_SCA_Proceso_Trabajador):
 *   Este paquete ES la version invocable de ese PASO 15. Reproduce los
 *   cursores CUR_COMPENSACIONES1 y CUR_COMPENSACIONES2 con la misma logica
 *   pero filtrable por empresa/empleado y con resumen por refcursor.
 *
 *   IMPORTANTE: si el sistema nativo ya proceso el dia (PASO 15), volver a
 *   ejecutar APLICAR_DIA es seguro pero redundante. Usar REVERTIR_DIA
 *   primero si se quiere recalcular desde cero.
 *
 * PRECONDICIONES:
 *   - SCA_ASISTENCIA_TAREO debe existir para FECHAORIGEN y FECHADESTINO.
 *   - El tareo debe estar calculado (PASOS 1-14 de SP_SCA_Proceso_Trabajador)
 *     antes de aplicar compensaciones.
 *
 * Autor:   Equipo AQUARIUS
 * Fecha:   27/04/2026
 *******************************************************************************/

CREATE OR REPLACE PACKAGE PKG_SCA_COMPENSACIONES AS

    /***************************************************************************
        REGISTRAR
        Crea una compensacion. Replica sp_SCA_Insert_Compensacion pero con
        validacion previa opcional, retornando ID y diagnostico.

        PARAMETROS:
        - p_cod_empresa       Codigo de empresa
        - p_cod_personal      Codigo de empleado
        - p_fecha_destino     'dd/MM/yyyy' (NULL si tipo='I')
        - p_fecha_origen      'dd/MM/yyyy'
        - p_tipo_origen       'E'|'D'|'B'|'I'
        - p_tipo_compensacion 'A'|'T'|'N'|'F'|'P'|'I'
        - p_horas             'HH:MI'   (string con formato fijo)
        - p_perid             ID permiso (solo si tipo_compensacion='P')
        - p_tipo_banco        'N'=mes, otro=semana (solo si tipo_origen='I'
                              o tipo_compensacion='I')
        - p_proceso           Periodo:
                              'MM/AAAA' (mes) | 'SS/MM/AAAA' (semana)
        - p_validar           'S'=valida saldo origen y deficit destino
                              'N'=registra sin validar
        - cv_resultado        Cursor: id_compen, estado('OK'|'ERR'),
                              motivo, tiempo_minutos
    ***************************************************************************/
    PROCEDURE REGISTRAR(
        p_cod_empresa       IN VARCHAR2,
        p_cod_personal      IN VARCHAR2,
        p_fecha_destino     IN VARCHAR2 DEFAULT NULL,
        p_fecha_origen      IN VARCHAR2,
        p_tipo_origen       IN CHAR,
        p_tipo_compensacion IN CHAR,
        p_horas             IN VARCHAR2,
        p_perid             IN VARCHAR2 DEFAULT NULL,
        p_tipo_banco        IN VARCHAR2 DEFAULT NULL,
        p_proceso           IN VARCHAR2 DEFAULT NULL,
        p_validar           IN VARCHAR2 DEFAULT 'S',
        cv_resultado        OUT SYS_REFCURSOR
    );

    /***************************************************************************
        ELIMINAR
        Baja de una compensacion. Equivalente a sp_SCA_Delete_Compensacion.

        PARAMETROS:
        - p_id_compen   Si > 0 elimina por ID (recomendado).
        - resto         Mismos que REGISTRAR (usados solo si p_id_compen es NULL).
        - p_revertir_tareo  'S' antes de eliminar revierte el efecto en tareo
                            'N' solo elimina la fila (no toca tareo)
        - cv_resultado  estado, motivo, filas_eliminadas
    ***************************************************************************/
    PROCEDURE ELIMINAR(
        p_id_compen         IN NUMBER   DEFAULT NULL,
        p_cod_empresa       IN VARCHAR2 DEFAULT NULL,
        p_cod_personal      IN VARCHAR2 DEFAULT NULL,
        p_fecha_destino     IN VARCHAR2 DEFAULT NULL,
        p_fecha_origen      IN VARCHAR2 DEFAULT NULL,
        p_tipo_origen       IN CHAR     DEFAULT NULL,
        p_tipo_compensacion IN CHAR     DEFAULT NULL,
        p_horas             IN VARCHAR2 DEFAULT NULL,
        p_perid             IN VARCHAR2 DEFAULT NULL,
        p_tipo_banco        IN VARCHAR2 DEFAULT NULL,
        p_proceso           IN VARCHAR2 DEFAULT NULL,
        p_revertir_tareo    IN VARCHAR2 DEFAULT 'S',
        cv_resultado        OUT SYS_REFCURSOR
    );

    /***************************************************************************
        VALIDAR
        Pre-flight check: verifica si una compensacion PUEDE aplicarse
        consultando saldos en tareo. NO modifica nada.

        cv_resultado columnas:
        - puede_aplicar    'S'|'N'
        - motivo           texto explicativo
        - tiempo_solicitado_min
        - tiempo_disponible_origen_min
        - tiempo_deficit_destino_min
        - tipo_validacion  'EXACTA' (A/T/N) | 'PARCIAL' (F/P) | 'BANCO' (I)
    ***************************************************************************/
    PROCEDURE VALIDAR(
        p_cod_empresa       IN VARCHAR2,
        p_cod_personal      IN VARCHAR2,
        p_fecha_destino     IN VARCHAR2 DEFAULT NULL,
        p_fecha_origen      IN VARCHAR2,
        p_tipo_origen       IN CHAR,
        p_tipo_compensacion IN CHAR,
        p_horas             IN VARCHAR2,
        cv_resultado        OUT SYS_REFCURSOR
    );

    /***************************************************************************
        APLICAR_DIA  (ATOMICO - 1 empleado, 1 dia)
        Reproduce PASO 15 de SP_SCA_Proceso_Trabajador para UN solo empleado
        en UN solo dia. Aplica compensaciones cuyo fechadestino O fechaorigen
        sea p_fecha. Si una no cuadra, la elimina (igual que el sistema nativo)
        salvo que p_eliminar_no_cuadra='N'.

        Esta procedure es la unidad atomica. Para procesar muchos empleados
        usar APLICAR_DIA_MASIVO (que recursivamente la invoca).

        PARAMETROS:
        - p_cod_empresa     OBLIGATORIO
        - p_cod_personal    OBLIGATORIO
        - p_fecha           'dd/MM/yyyy'
        - p_eliminar_no_cuadra 'S' (default, igual al nativo) | 'N' solo loguea
        - cv_resultado      cursor con resumen: aplicadas_des, aplicadas_ori,
                            eliminadas, errores
    ***************************************************************************/
    PROCEDURE APLICAR_DIA(
        p_cod_empresa          IN VARCHAR2,
        p_cod_personal         IN VARCHAR2,
        p_fecha                IN VARCHAR2,
        p_eliminar_no_cuadra   IN VARCHAR2 DEFAULT 'S',
        cv_resultado           OUT SYS_REFCURSOR
    );

    /***************************************************************************
        APLICAR_RANGO  (ATOMICO - 1 empleado, N dias)
        Loop por dias entre p_fecha_inicio y p_fecha_fin para UN empleado,
        llamando APLICAR_DIA por cada uno con COMMIT independiente.
    ***************************************************************************/
    PROCEDURE APLICAR_RANGO(
        p_cod_empresa          IN VARCHAR2,
        p_cod_personal         IN VARCHAR2,
        p_fecha_inicio         IN VARCHAR2,
        p_fecha_fin            IN VARCHAR2,
        p_eliminar_no_cuadra   IN VARCHAR2 DEFAULT 'S',
        cv_resultado           OUT SYS_REFCURSOR
    );

    /***************************************************************************
        REVERTIR_DIA  (ATOMICO - 1 empleado, 1 dia)
        Deshace los efectos de las compensaciones en el TAREO (devuelve los
        valores a tardanza/falta/HE/etc.) SIN borrar SCA_COMPENSACION.

        Util cuando se quiere recalcular el tareo o corregir un dia mal
        compensado.
    ***************************************************************************/
    PROCEDURE REVERTIR_DIA(
        p_cod_empresa     IN VARCHAR2,
        p_cod_personal    IN VARCHAR2,
        p_fecha           IN VARCHAR2,
        cv_resultado      OUT SYS_REFCURSOR
    );

    /***************************************************************************
        REVERTIR_RANGO  (ATOMICO - 1 empleado, N dias)
        Loop por dias para UN empleado llamando REVERTIR_DIA por cada uno.
    ***************************************************************************/
    PROCEDURE REVERTIR_RANGO(
        p_cod_empresa     IN VARCHAR2,
        p_cod_personal    IN VARCHAR2,
        p_fecha_inicio    IN VARCHAR2,
        p_fecha_fin       IN VARCHAR2,
        cv_resultado      OUT SYS_REFCURSOR
    );

    -- =========================================================================
    -- CAPA MASIVA (recursiva: itera empleados y llama a la capa atomica)
    -- =========================================================================

    /***************************************************************************
        APLICAR_DIA_MASIVO
        Itera empleados (filtrables por empresa y/o LIKE de cod_personal) y
        llama recursivamente a APLICAR_DIA por cada uno con COMMIT
        independiente. Resumen agregado en el cursor.

        - p_cod_empresa     NULL = todas las empresas
        - p_cod_personal    NULL = todos | sino LIKE (admite '%')
    ***************************************************************************/
    PROCEDURE APLICAR_DIA_MASIVO(
        p_cod_empresa          IN VARCHAR2 DEFAULT NULL,
        p_cod_personal         IN VARCHAR2 DEFAULT NULL,
        p_fecha                IN VARCHAR2,
        p_eliminar_no_cuadra   IN VARCHAR2 DEFAULT 'S',
        cv_resultado           OUT SYS_REFCURSOR
    );

    /***************************************************************************
        APLICAR_RANGO_MASIVO
        Itera empleados y llama APLICAR_RANGO por cada uno (que a su vez
        llama APLICAR_DIA por cada dia). Doble recursion: empleados x dias.
    ***************************************************************************/
    PROCEDURE APLICAR_RANGO_MASIVO(
        p_cod_empresa          IN VARCHAR2 DEFAULT NULL,
        p_cod_personal         IN VARCHAR2 DEFAULT NULL,
        p_fecha_inicio         IN VARCHAR2,
        p_fecha_fin            IN VARCHAR2,
        p_eliminar_no_cuadra   IN VARCHAR2 DEFAULT 'S',
        cv_resultado           OUT SYS_REFCURSOR
    );

    /***************************************************************************
        REVERTIR_DIA_MASIVO
        Itera empleados y llama REVERTIR_DIA por cada uno.
    ***************************************************************************/
    PROCEDURE REVERTIR_DIA_MASIVO(
        p_cod_empresa     IN VARCHAR2 DEFAULT NULL,
        p_cod_personal    IN VARCHAR2 DEFAULT NULL,
        p_fecha           IN VARCHAR2,
        cv_resultado      OUT SYS_REFCURSOR
    );

    /***************************************************************************
        REVERTIR_RANGO_MASIVO
        Itera empleados y llama REVERTIR_RANGO por cada uno.
    ***************************************************************************/
    PROCEDURE REVERTIR_RANGO_MASIVO(
        p_cod_empresa     IN VARCHAR2 DEFAULT NULL,
        p_cod_personal    IN VARCHAR2 DEFAULT NULL,
        p_fecha_inicio    IN VARCHAR2,
        p_fecha_fin       IN VARCHAR2,
        cv_resultado      OUT SYS_REFCURSOR
    );

    /***************************************************************************
        REGISTRAR_EVENTO
        Caso especial: un evento unico (ej: trabajan domingo para no venir al
        feriado) que aplica la MISMA regla de compensacion a varios empleados.

        El procedimiento detecta automaticamente las horas disponibles de cada
        empleado en la FECHAORIGEN segun el TIPO_ORIGEN indicado:
          'E' -> usa HORAEXTRA_AJUS   del tareo de fecha_origen
          'D' -> usa HORADOBLESOF     del tareo de fecha_origen
          'B' -> usa HORABANCOH       del tareo de fecha_origen

        Solo procesa empleados que TENGAN horas en ese tipo en fecha_origen.

        PARAMETROS:
        - p_cod_empresa        Empresa
        - p_fecha_origen       'dd/MM/yyyy' dia trabajado (ej: domingo)
        - p_fecha_destino      'dd/MM/yyyy' dia a compensar (ej: feriado)
                               NULL si tipo_compensacion = 'I'
        - p_tipo_origen        'E'|'D'|'B'
        - p_tipo_compensacion  'F'|'T'|'A'|'N'|'P'|'I'
        - p_lista_personal     NULL = todos los empleados con horas ese dia
                               'cod1,cod2,cod3' = solo esos empleados
        - p_horas_override     NULL = auto desde tareo de cada empleado
                               'HH:MI' = mismo tiempo fijo para todos
        - p_aplicar            'S' = registrar y aplicar en el mismo paso
                               'N' = solo registrar (aplicar luego con APLICAR_DIA_MASIVO)
        - p_eliminar_no_cuadra 'S'|'N' (solo aplica si p_aplicar='S')
        - p_validar            'S'=valida saldo/deficit antes de registrar

        CURSOR resultado:
          fecha_origen, fecha_destino, tipo_origen, tipo_compensacion,
          empleados_encontrados, registradas_ok, aplicadas_ok, errores, estado
    ***************************************************************************/
    PROCEDURE REGISTRAR_EVENTO(
        p_cod_empresa        IN VARCHAR2,
        p_fecha_origen       IN VARCHAR2,
        p_fecha_destino      IN VARCHAR2 DEFAULT NULL,
        p_tipo_origen        IN CHAR,
        p_tipo_compensacion  IN CHAR,
        p_lista_personal     IN VARCHAR2 DEFAULT NULL,
        p_horas_override     IN VARCHAR2 DEFAULT NULL,
        p_aplicar            IN VARCHAR2 DEFAULT 'N',
        p_eliminar_no_cuadra IN VARCHAR2 DEFAULT 'S',
        p_validar            IN VARCHAR2 DEFAULT 'S',
        cv_resultado         OUT SYS_REFCURSOR
    );

    /***************************************************************************
        CONSULTAR_RANGO
        Lista compensaciones de un empleado/rango con diagnostico de aplicabilidad.
        Cursor incluye: id_compen, fechaorigen, fechadestino, tipoorigen,
        tipocompensacion, tiempo_min, tiempo_hhmi, aux1, estado_aplicacion,
        alertas_destino, alertas_origen.
    ***************************************************************************/
    PROCEDURE CONSULTAR_RANGO(
        p_cod_empresa     IN VARCHAR2 DEFAULT NULL,
        p_cod_personal    IN VARCHAR2 DEFAULT NULL,
        p_fecha_inicio    IN VARCHAR2,
        p_fecha_fin       IN VARCHAR2,
        cv_resultado      OUT SYS_REFCURSOR
    );

    /***************************************************************************
        CONSULTAR_SALDO_BANCO
        Retorna saldo del banco de horas mensual y semanal de un empleado.
    ***************************************************************************/
    PROCEDURE CONSULTAR_SALDO_BANCO(
        p_cod_empresa     IN VARCHAR2,
        p_cod_personal    IN VARCHAR2,
        p_anio            IN NUMBER   DEFAULT NULL,
        p_mes             IN NUMBER   DEFAULT NULL,
        cv_resultado      OUT SYS_REFCURSOR
    );

    /***************************************************************************
        VER_ESTADO
        Detalle completo de una compensacion: datos de la fila, tareo origen,
        tareo destino, alertas, validacion actual.
    ***************************************************************************/
    PROCEDURE VER_ESTADO(
        p_id_compen       IN NUMBER,
        cv_resultado      OUT SYS_REFCURSOR
    );

    /***************************************************************************
        DIAGNOSTICO_RANGO
        Vista completa dia a dia para un empleado: que tiene disponible
        para compensar (ORIGEN) y que deficits tiene (DESTINO), cruzado
        con las compensaciones ya registradas.

        Una fila por dia del rango con tareo calculado.
        Las columnas *_min son en minutos; *_hhmi en formato 'HH:MI'.

        COLUMNAS DE ORIGEN (disponible para ceder):
        - he_min / he_hhmi          Horas extras disponibles    (tipo 'E')
        - dobles_min / dobles_hhmi  Horas dobles disponibles    (tipo 'D')
        - banco_min / banco_hhmi    Banco de horas disponible   (tipo 'B')

        COLUMNAS DE DESTINO (deficit a cubrir):
        - tard_min / tard_hhmi      Tardanza pendiente          (tipo 'T')
        - antes_min / antes_hhmi    Salida antes pendiente      (tipo 'A')
        - falta_min / falta_hhmi    Falta pendiente             (tipo 'F')
        - notrab_min / notrab_hhmi  Horas no trabajadas         (tipo 'N')
        - permiso_min / permiso_hhmi Permiso pendiente          (tipo 'P')

        ESTADO DEL DIA:
        - tiene_origen    'S' si hay algun origen disponible
        - tiene_deficit   'S' si hay algun deficit que cubrir
        - compen_registradas  numero de compensaciones ya registradas ese dia
        - compen_aplicadas    numero de compensaciones ya aplicadas ese dia
        - sugerencia      texto corto con accion recomendada

        INDICADORES:
        - es_descanso     'S'/'N'
        - es_feriado      'S'/'N'
        - alerta01        MI=marca impar, FT=falta total (del tareo)
    ***************************************************************************/
    PROCEDURE DIAGNOSTICO_RANGO(
        p_cod_empresa     IN VARCHAR2,
        p_cod_personal    IN VARCHAR2,
        p_fecha_inicio    IN VARCHAR2,
        p_fecha_fin       IN VARCHAR2,
        cv_resultado      OUT SYS_REFCURSOR
    );

    /***************************************************************************
        BUSCAR_EMPLEADO
        Misma firma que PKG_SCA_DEPURA_TAREO.BUSCAR_EMPLEADO.
    ***************************************************************************/
    PROCEDURE BUSCAR_EMPLEADO(
        p_cod_empresa   IN VARCHAR2,
        p_nombre        IN VARCHAR2 DEFAULT NULL,
        cv_resultado    OUT SYS_REFCURSOR
    );

END PKG_SCA_COMPENSACIONES;
/


/*******************************************************************************
 *                              PACKAGE BODY
 *******************************************************************************/
CREATE OR REPLACE PACKAGE BODY PKG_SCA_COMPENSACIONES AS

    -- =========================================================================
    -- CONSTANTES
    -- =========================================================================
    c_BASE_DATE  CONSTANT DATE := TO_DATE('01/01/1900','dd/MM/yyyy');

    -- =========================================================================
    -- UTILIDADES PRIVADAS
    -- =========================================================================

    -- Convierte 'HH:MI' -> minutos
    FUNCTION fn_hhmi_a_min(p_horas IN VARCHAR2) RETURN NUMBER IS
    BEGIN
        IF p_horas IS NULL OR INSTR(p_horas,':') = 0 THEN
            RETURN 0;
        END IF;
        RETURN (TO_NUMBER(SUBSTR(p_horas,1,INSTR(p_horas,':')-1)) * 60)
             + TO_NUMBER(SUBSTR(p_horas, INSTR(p_horas,':')+1));
    END fn_hhmi_a_min;

    -- Convierte minutos -> 'HH:MI'
    FUNCTION fn_min_a_hhmi(p_min IN NUMBER) RETURN VARCHAR2 IS
    BEGIN
        IF p_min IS NULL THEN RETURN '00:00'; END IF;
        RETURN SUBSTR('00' || TO_CHAR(TRUNC(p_min/60,0)), -2, 2)
            || ':' ||
               SUBSTR('00' || TO_CHAR(MOD(p_min, 60)), -2, 2);
    END fn_min_a_hhmi;

    -- Convierte campo DATE base 1900 -> minutos
    FUNCTION fn_date_a_min(p_dt IN DATE) RETURN NUMBER IS
    BEGIN
        IF p_dt IS NULL THEN RETURN 0; END IF;
        RETURN (TO_NUMBER(TO_CHAR(p_dt,'HH24')) * 60)
             + TO_NUMBER(TO_CHAR(p_dt,'MI'));
    END fn_date_a_min;

    -- Lee el tiempo "destino" disponible/requerido en tareo segun tipocompensacion
    FUNCTION fn_tiempo_destino(
        p_emp IN VARCHAR2, p_per IN VARCHAR2, p_fec IN DATE, p_tipo IN CHAR
    ) RETURN NUMBER IS
        v NUMBER := 0;
        v_dt DATE;
    BEGIN
        BEGIN
            SELECT CASE p_tipo
                     WHEN 'A' THEN horaantesalida
                     WHEN 'T' THEN horatardanza
                     WHEN 'N' THEN horas_no_trabajadas
                     WHEN 'F' THEN horas_falta
                     WHEN 'P' THEN horapermiso
                   END
            INTO   v_dt
            FROM   SCA_ASISTENCIA_TAREO
            WHERE  cod_empresa = p_emp
            AND    cod_personal = p_per
            AND    fechamar = p_fec;
        EXCEPTION WHEN NO_DATA_FOUND THEN v_dt := NULL;
        END;
        v := fn_date_a_min(v_dt);
        RETURN v;
    END fn_tiempo_destino;

    -- Lee el tiempo "origen" disponible en tareo segun tipoorigen
    FUNCTION fn_tiempo_origen(
        p_emp IN VARCHAR2, p_per IN VARCHAR2, p_fec IN DATE, p_tipo IN CHAR
    ) RETURN NUMBER IS
        v_dt DATE;
    BEGIN
        IF p_tipo = 'I' THEN
            RETURN NULL; -- intercambio no se valida en tareo
        END IF;
        BEGIN
            SELECT CASE p_tipo
                     WHEN 'E' THEN horaextra_ajus
                     WHEN 'D' THEN horadoblesof
                     WHEN 'B' THEN horabancoh
                   END
            INTO   v_dt
            FROM   SCA_ASISTENCIA_TAREO
            WHERE  cod_empresa = p_emp
            AND    cod_personal = p_per
            AND    fechamar = p_fec;
        EXCEPTION WHEN NO_DATA_FOUND THEN v_dt := NULL;
        END;
        RETURN fn_date_a_min(v_dt);
    END fn_tiempo_origen;

    -- =========================================================================
    -- INTERNO: aplica efecto en TAREO DESTINO  (equivale a InsComDes)
    -- =========================================================================
    PROCEDURE prv_aplicar_destino(
        p_emp IN VARCHAR2, p_per IN VARCHAR2, p_fec IN DATE,
        p_tipo IN CHAR, p_horas_hhmi IN VARCHAR2
    ) AS
        v_min NUMBER := fn_hhmi_a_min(p_horas_hhmi);
        v_dt  DATE   := TO_DATE('01/01/1900 ' || p_horas_hhmi, 'dd/MM/yyyy HH24:MI');
    BEGIN
        UPDATE SCA_ASISTENCIA_TAREO
        SET horatardanza        = CASE WHEN p_tipo='T' THEN NULL ELSE horatardanza END,
            alerta04            = CASE WHEN p_tipo='T' THEN 'TC' ELSE alerta04 END,
            horaantesalida      = CASE WHEN p_tipo='A' THEN NULL ELSE horaantesalida END,
            alerta07            = CASE WHEN p_tipo='A' THEN 'SC' ELSE alerta07 END,
            horas_no_trabajadas = CASE WHEN p_tipo='N' THEN NULL ELSE horas_no_trabajadas END,
            alerta03            = CASE WHEN p_tipo='N' THEN 'HC' ELSE alerta03 END,
            horas_recup         = CASE WHEN p_tipo='P' THEN
                                       CASE WHEN horas_recup = c_BASE_DATE + v_min/1440
                                            THEN NULL
                                            ELSE horas_recup - v_min/1440
                                       END
                                   ELSE horas_recup END,
            alerta09            = CASE WHEN p_tipo='P'
                                       AND horas_recup = c_BASE_DATE + v_min/1440
                                       THEN 'PC' ELSE alerta09 END,
            horas_falta         = CASE WHEN p_tipo='F' THEN NULL ELSE horas_falta END,
            alerta02            = CASE WHEN p_tipo='F' THEN 'FC' ELSE alerta02 END,
            horaefectiva        = NVL(horaefectiva, c_BASE_DATE) + v_min/1440,
            tothoramarcas       = CASE WHEN p_tipo <> 'P'
                                       THEN NVL(tothoramarcas, c_BASE_DATE) + v_min/1440
                                       ELSE tothoramarcas END
        WHERE cod_empresa = p_emp
        AND   cod_personal = p_per
        AND   fechamar = p_fec;
    END prv_aplicar_destino;

    -- =========================================================================
    -- INTERNO: aplica efecto en TAREO ORIGEN (equivale a InsComOri)
    -- =========================================================================
    PROCEDURE prv_aplicar_origen(
        p_emp IN VARCHAR2, p_per IN VARCHAR2, p_fec IN DATE,
        p_tipo_ori IN CHAR, p_horas_hhmi IN VARCHAR2
    ) AS
        v_min NUMBER := fn_hhmi_a_min(p_horas_hhmi);
    BEGIN
        IF p_tipo_ori = 'I' THEN
            -- Intercambio: no descuenta de tareo (saldo va a banco)
            RETURN;
        END IF;

        -- Descuenta tothoramarcas siempre (origen efectivamente trabaja menos)
        UPDATE SCA_ASISTENCIA_TAREO
        SET tothoramarcas = tothoramarcas - v_min/1440
        WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec;

        IF p_tipo_ori = 'E' THEN
            -- 1. Descontar HE
            UPDATE SCA_ASISTENCIA_TAREO
            SET horaextra_ajus = horaextra_ajus - v_min/1440
            WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec;

            -- 2. Reset HE oficiales y recalcular
            UPDATE SCA_ASISTENCIA_TAREO
            SET horaexofi1 = NULL, horaexofi2 = NULL, horaexofi3 = NULL
            WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec;

            -- HE 25%
            UPDATE SCA_ASISTENCIA_TAREO
            SET horaexofi1 = horaextra_ajus
            WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec
            AND   horaextra_ajus <= h25f;

            UPDATE SCA_ASISTENCIA_TAREO
            SET horaexofi1 = h25f
            WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec
            AND   horaextra_ajus > h25f;

            -- HE 35%
            UPDATE SCA_ASISTENCIA_TAREO
            SET horaexofi2 = TO_DATE('01/01/1900 ' ||
                  TO_CHAR(TRUNC(MOD((horaextra_ajus - NVL(h25f, c_BASE_DATE))*24, 24))) || ':' ||
                  TO_CHAR(TRUNC(MOD((horaextra_ajus - NVL(h25f, c_BASE_DATE))*24*60, 60))),
                  'dd/MM/yyyy HH24:MI')
            WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec
            AND   horaextra_ajus > h35i AND horaextra_ajus <= h35f;

            UPDATE SCA_ASISTENCIA_TAREO
            SET horaexofi2 = TO_DATE('01/01/1900 ' ||
                  TO_CHAR(TRUNC(MOD((h35f - h35i)*24, 24))) || ':' ||
                  TO_CHAR(TRUNC(MOD((h35f - h35i)*24*60, 60))),
                  'dd/MM/yyyy HH24:MI')
            WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec
            AND   horaextra_ajus > h35f;

            -- HE 50% (nocturnas)
            UPDATE SCA_ASISTENCIA_TAREO
            SET horaexofi3 = TO_DATE('01/01/1900 ' ||
                  TO_CHAR(TRUNC(MOD((horaextra_ajus - NVL(h35f, c_BASE_DATE))*24, 24))) || ':' ||
                  TO_CHAR(TRUNC(MOD((horaextra_ajus - NVL(h35f, c_BASE_DATE))*24*60, 60))),
                  'dd/MM/yyyy HH24:MI')
            WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec
            AND   horaextra_ajus > hni;

            -- alerta06 + limpieza si llega a cero
            UPDATE SCA_ASISTENCIA_TAREO
            SET horaextra_ajus = CASE WHEN horaextra_ajus = c_BASE_DATE THEN NULL
                                      ELSE horaextra_ajus END,
                alerta06       = CASE WHEN horaextra_ajus = c_BASE_DATE THEN 'EC'
                                      ELSE alerta06 END
            WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec;

        ELSIF p_tipo_ori = 'D' THEN
            UPDATE SCA_ASISTENCIA_TAREO
            SET horadoblesof = horadoblesof - v_min/1440
            WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec;

            UPDATE SCA_ASISTENCIA_TAREO
            SET horadoblesof = CASE WHEN horadoblesof = c_BASE_DATE THEN NULL
                                    ELSE horadoblesof END,
                alerta08     = CASE WHEN horadoblesof = c_BASE_DATE THEN 'DC'
                                    ELSE alerta08 END
            WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec;

        ELSIF p_tipo_ori = 'B' THEN
            UPDATE SCA_ASISTENCIA_TAREO
            SET horabancoh = horabancoh - v_min/1440
            WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec;

            UPDATE SCA_ASISTENCIA_TAREO
            SET horabancoh = CASE WHEN horabancoh = c_BASE_DATE THEN NULL
                                  ELSE horabancoh END,
                alerta06   = CASE WHEN horabancoh = c_BASE_DATE THEN 'EC'
                                  ELSE alerta06 END
            WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec;
        END IF;
    END prv_aplicar_origen;

    -- =========================================================================
    -- INTERNO: revierte efecto en TAREO DESTINO (equivale a DelComDes)
    -- =========================================================================
    PROCEDURE prv_revertir_destino(
        p_emp IN VARCHAR2, p_per IN VARCHAR2, p_fec IN DATE,
        p_tipo IN CHAR, p_horas_hhmi IN VARCHAR2
    ) AS
        v_min NUMBER := fn_hhmi_a_min(p_horas_hhmi);
        v_dt  DATE   := TO_DATE('01/01/1900 ' || p_horas_hhmi, 'dd/MM/yyyy HH24:MI');
    BEGIN
        UPDATE SCA_ASISTENCIA_TAREO
        SET horatardanza        = CASE WHEN p_tipo='T' THEN v_dt ELSE horatardanza END,
            alerta04            = CASE WHEN p_tipo='T' THEN
                                       CASE WHEN v_min <= min_max_raz_tard THEN 'TN' ELSE 'TE' END
                                   ELSE alerta04 END,
            horaantesalida      = CASE WHEN p_tipo='A' THEN v_dt ELSE horaantesalida END,
            alerta07            = CASE WHEN p_tipo='A' THEN
                                       CASE WHEN v_min <= min_raz_hnormal THEN 'SN' ELSE 'SE' END
                                   ELSE alerta07 END,
            horas_no_trabajadas = CASE WHEN p_tipo='N' THEN v_dt ELSE horas_no_trabajadas END,
            alerta03            = CASE WHEN p_tipo='N' THEN 'HI' ELSE alerta03 END,
            horas_recup         = CASE WHEN p_tipo='P'
                                       THEN NVL(horas_recup, c_BASE_DATE) + v_min/1440
                                       ELSE horas_recup END,
            alerta09            = CASE WHEN NVL(horas_recup, c_BASE_DATE) + v_min/1440 = tothoras
                                       THEN 'FT' ELSE alerta09 END,
            horas_falta         = CASE WHEN p_tipo='F'
                                       THEN NVL(horas_falta, c_BASE_DATE) + v_min/1440
                                       ELSE horas_falta END,
            alerta02            = CASE WHEN NVL(horas_falta, c_BASE_DATE) + v_min/1440 = tothoras
                                       THEN 'FT' ELSE alerta02 END,
            horaefectiva        = horaefectiva - v_min/1440,
            tothoramarcas       = CASE WHEN p_tipo <> 'P'
                                       THEN tothoramarcas - v_min/1440
                                       ELSE tothoramarcas END
        WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec;

        UPDATE SCA_ASISTENCIA_TAREO
        SET horaefectiva  = CASE WHEN horaefectiva  = c_BASE_DATE THEN NULL ELSE horaefectiva END,
            tothoramarcas = CASE WHEN tothoramarcas = c_BASE_DATE THEN NULL ELSE tothoramarcas END
        WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec;
    END prv_revertir_destino;

    -- =========================================================================
    -- INTERNO: revierte efecto en TAREO ORIGEN (equivale a DelComOri)
    -- =========================================================================
    PROCEDURE prv_revertir_origen(
        p_emp IN VARCHAR2, p_per IN VARCHAR2, p_fec IN DATE,
        p_tipo_ori IN CHAR, p_horas_hhmi IN VARCHAR2
    ) AS
        v_min NUMBER := fn_hhmi_a_min(p_horas_hhmi);
    BEGIN
        IF p_tipo_ori = 'I' THEN RETURN; END IF;

        UPDATE SCA_ASISTENCIA_TAREO
        SET tothoramarcas = tothoramarcas + v_min/1440
        WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec;

        IF p_tipo_ori = 'E' THEN
            UPDATE SCA_ASISTENCIA_TAREO
            SET horaextra_ajus = NVL(horaextra_ajus, c_BASE_DATE) + v_min/1440
            WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec;
            -- Recalcular oficiales
            UPDATE SCA_ASISTENCIA_TAREO
            SET horaexofi1 = NULL, horaexofi2 = NULL, horaexofi3 = NULL
            WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec;
            UPDATE SCA_ASISTENCIA_TAREO
            SET horaexofi1 = horaextra_ajus
            WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec
            AND   horaextra_ajus <= h25f;
            UPDATE SCA_ASISTENCIA_TAREO
            SET horaexofi1 = h25f
            WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec
            AND   horaextra_ajus > h25f;
            UPDATE SCA_ASISTENCIA_TAREO
            SET alerta06 = CASE WHEN (TO_NUMBER(TO_CHAR(horaextra_ajus,'HH24'))*60)
                                  + TO_NUMBER(TO_CHAR(horaextra_ajus,'MI'))
                                  <= min_min_raz_hextra THEN 'EN' ELSE 'EE' END
            WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec;

        ELSIF p_tipo_ori = 'D' THEN
            UPDATE SCA_ASISTENCIA_TAREO
            SET horadoblesof = NVL(horadoblesof, c_BASE_DATE) + v_min/1440,
                alerta08    = 'DO'
            WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec;

        ELSIF p_tipo_ori = 'B' THEN
            UPDATE SCA_ASISTENCIA_TAREO
            SET horabancoh = NVL(horabancoh, c_BASE_DATE) - v_min/1440,
                alerta06   = CASE WHEN (TO_NUMBER(TO_CHAR(horaextra_ajus,'HH24'))*60)
                                    + TO_NUMBER(TO_CHAR(horaextra_ajus,'MI'))
                                    <= min_min_raz_hextra THEN 'EN' ELSE 'EE' END
            WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec;
        END IF;
    END prv_revertir_origen;


    -- =========================================================================
    -- REGISTRAR
    -- =========================================================================
    PROCEDURE REGISTRAR(
        p_cod_empresa       IN VARCHAR2,
        p_cod_personal      IN VARCHAR2,
        p_fecha_destino     IN VARCHAR2 DEFAULT NULL,
        p_fecha_origen      IN VARCHAR2,
        p_tipo_origen       IN CHAR,
        p_tipo_compensacion IN CHAR,
        p_horas             IN VARCHAR2,
        p_perid             IN VARCHAR2 DEFAULT NULL,
        p_tipo_banco        IN VARCHAR2 DEFAULT NULL,
        p_proceso           IN VARCHAR2 DEFAULT NULL,
        p_validar           IN VARCHAR2 DEFAULT 'S',
        cv_resultado        OUT SYS_REFCURSOR
    ) AS
        v_min        NUMBER := fn_hhmi_a_min(p_horas);
        v_id         NUMBER;
        v_estado     VARCHAR2(10) := 'OK';
        v_motivo     VARCHAR2(500) := 'Registrada correctamente';
        v_fec_des    DATE;
        v_fec_ori    DATE;
        v_disp_ori   NUMBER;
        v_def_des    NUMBER;
    BEGIN
        v_fec_ori := TO_DATE(p_fecha_origen, 'dd/MM/yyyy');
        IF p_tipo_compensacion = 'I' THEN
            v_fec_des := NULL;
        ELSE
            v_fec_des := TO_DATE(p_fecha_destino, 'dd/MM/yyyy');
        END IF;

        -- Validacion previa opcional
        IF NVL(p_validar,'S') = 'S' THEN
            -- Origen
            IF p_tipo_origen IN ('E','D','B') THEN
                v_disp_ori := fn_tiempo_origen(p_cod_empresa, p_cod_personal, v_fec_ori, p_tipo_origen);
                IF v_disp_ori IS NULL OR v_disp_ori < v_min THEN
                    v_estado := 'ERR';
                    v_motivo := 'Tiempo insuficiente en origen ('||p_tipo_origen||
                                ') disp='||NVL(v_disp_ori,0)||' min, sol='||v_min||' min';
                END IF;
            END IF;
            -- Destino
            IF v_estado = 'OK' AND p_tipo_compensacion IN ('A','T','N','F','P') THEN
                v_def_des := fn_tiempo_destino(p_cod_empresa, p_cod_personal, v_fec_des, p_tipo_compensacion);
                IF p_tipo_compensacion IN ('A','T','N') AND v_def_des <> v_min THEN
                    v_estado := 'ERR';
                    v_motivo := 'Tiempo destino ('||p_tipo_compensacion||') = '||v_def_des||
                                ' min, requerido EXACTO '||v_min||' min';
                ELSIF p_tipo_compensacion IN ('F','P') AND v_def_des < v_min THEN
                    v_estado := 'ERR';
                    v_motivo := 'Tiempo destino ('||p_tipo_compensacion||') = '||v_def_des||
                                ' min, insuficiente para '||v_min||' min';
                END IF;
            END IF;
        END IF;

        IF v_estado = 'ERR' THEN
            OPEN cv_resultado FOR
                SELECT NULL AS id_compen, v_estado AS estado, v_motivo AS motivo,
                       v_min AS tiempo_minutos
                FROM   DUAL;
            RETURN;
        END IF;

        -- Insert principal
        INSERT INTO SCA_COMPENSACION (
            id_compen, cod_empresa, cod_personal,
            fechadestino, fechaorigen,
            tipoorigen, tipocompensacion, tiempo, aux1
        ) VALUES (
            id_comp_seq.NEXTVAL, p_cod_empresa, p_cod_personal,
            v_fec_des, v_fec_ori,
            p_tipo_origen, p_tipo_compensacion, v_min, p_proceso
        ) RETURNING id_compen INTO v_id;

        -- Efectos colaterales
        IF p_tipo_compensacion = 'P' THEN
            UPDATE SCA_PERMISO_DET
            SET tiempo_compensado = NVL(tiempo_compensado, c_BASE_DATE) + v_min/1440
            WHERE perid = p_perid AND perfec = v_fec_des;
        END IF;

        IF p_tipo_compensacion = 'I' THEN
            IF p_tipo_banco = 'N' THEN
                UPDATE SCA_BANCOHORAS_MES
                SET hc_banhormes = NVL(hc_banhormes, 0) + v_min
                WHERE cod_empresa  = p_cod_empresa
                AND   cod_personal = p_cod_personal
                AND   mes_proceso || '/' || ano_proceso = p_proceso;
            ELSE
                UPDATE SCA_BANCOHORAS_SEM
                SET hc_banhorsem = NVL(hc_banhorsem, 0) + v_min
                WHERE cod_empresa  = p_cod_empresa
                AND   cod_personal = p_cod_personal
                AND   sem_proceso || '/' || mes_proceso || '/' || ano_proceso = p_proceso;
            END IF;
        END IF;

        OPEN cv_resultado FOR
            SELECT v_id AS id_compen, v_estado AS estado, v_motivo AS motivo,
                   v_min AS tiempo_minutos
            FROM   DUAL;
    EXCEPTION
        WHEN OTHERS THEN
            v_estado := 'ERR';
            v_motivo := 'EXCEPCION: '||SQLERRM;
            OPEN cv_resultado FOR
                SELECT NULL AS id_compen, v_estado AS estado, v_motivo AS motivo,
                       v_min AS tiempo_minutos
                FROM   DUAL;
    END REGISTRAR;


    -- =========================================================================
    -- ELIMINAR
    -- =========================================================================
    PROCEDURE ELIMINAR(
        p_id_compen         IN NUMBER   DEFAULT NULL,
        p_cod_empresa       IN VARCHAR2 DEFAULT NULL,
        p_cod_personal      IN VARCHAR2 DEFAULT NULL,
        p_fecha_destino     IN VARCHAR2 DEFAULT NULL,
        p_fecha_origen      IN VARCHAR2 DEFAULT NULL,
        p_tipo_origen       IN CHAR     DEFAULT NULL,
        p_tipo_compensacion IN CHAR     DEFAULT NULL,
        p_horas             IN VARCHAR2 DEFAULT NULL,
        p_perid             IN VARCHAR2 DEFAULT NULL,
        p_tipo_banco        IN VARCHAR2 DEFAULT NULL,
        p_proceso           IN VARCHAR2 DEFAULT NULL,
        p_revertir_tareo    IN VARCHAR2 DEFAULT 'S',
        cv_resultado        OUT SYS_REFCURSOR
    ) AS
        v_filas      NUMBER := 0;
        v_estado     VARCHAR2(10) := 'OK';
        v_motivo     VARCHAR2(500) := 'Eliminada';
        v_horas_hhmi VARCHAR2(10);
        v_emp        VARCHAR2(20);
        v_per        VARCHAR2(20);
        v_fec_des    DATE;
        v_fec_ori    DATE;
        v_tipo_ori   CHAR(1);
        v_tipo_com   CHAR(1);
        v_min        NUMBER;
        v_aux1       VARCHAR2(50);
    BEGIN
        -- Cargar datos: por id si viene, si no por compuesta
        IF p_id_compen IS NOT NULL THEN
            BEGIN
                SELECT cod_empresa, cod_personal, fechadestino, fechaorigen,
                       tipoorigen, tipocompensacion, tiempo, aux1
                INTO   v_emp, v_per, v_fec_des, v_fec_ori,
                       v_tipo_ori, v_tipo_com, v_min, v_aux1
                FROM   SCA_COMPENSACION
                WHERE  id_compen = p_id_compen;
            EXCEPTION WHEN NO_DATA_FOUND THEN
                v_estado := 'ERR'; v_motivo := 'id_compen no encontrado';
                OPEN cv_resultado FOR
                    SELECT v_estado estado, v_motivo motivo, 0 filas_eliminadas FROM DUAL;
                RETURN;
            END;
            v_horas_hhmi := fn_min_a_hhmi(v_min);
        ELSE
            v_emp        := p_cod_empresa;
            v_per        := p_cod_personal;
            v_fec_ori    := TO_DATE(p_fecha_origen, 'dd/MM/yyyy');
            v_fec_des    := CASE WHEN p_tipo_compensacion = 'I' THEN NULL
                                 ELSE TO_DATE(p_fecha_destino, 'dd/MM/yyyy') END;
            v_tipo_ori   := p_tipo_origen;
            v_tipo_com   := p_tipo_compensacion;
            v_min        := fn_hhmi_a_min(p_horas);
            v_horas_hhmi := p_horas;
            v_aux1       := p_proceso;
        END IF;

        -- Revertir efectos en tareo (si se pidio)
        IF NVL(p_revertir_tareo,'S') = 'S' AND v_tipo_com <> 'I' THEN
            BEGIN prv_revertir_destino(v_emp, v_per, v_fec_des, v_tipo_com, v_horas_hhmi);
            EXCEPTION WHEN OTHERS THEN NULL; END;
            BEGIN prv_revertir_origen(v_emp, v_per, v_fec_ori, v_tipo_ori, v_horas_hhmi);
            EXCEPTION WHEN OTHERS THEN NULL; END;
        END IF;

        -- DELETE compensacion
        IF p_id_compen IS NOT NULL THEN
            DELETE SCA_COMPENSACION WHERE id_compen = p_id_compen;
        ELSE
            DELETE SCA_COMPENSACION
            WHERE cod_empresa = v_emp AND cod_personal = v_per
            AND   NVL(fechadestino, c_BASE_DATE) =
                  CASE WHEN v_tipo_com = 'I' THEN NVL(fechadestino, c_BASE_DATE)
                       ELSE v_fec_des END
            AND   fechaorigen = v_fec_ori
            AND   tipoorigen = v_tipo_ori
            AND   tipocompensacion = v_tipo_com;
        END IF;
        v_filas := SQL%ROWCOUNT;

        -- Reverso de efectos colaterales
        IF v_tipo_com = 'P' THEN
            UPDATE SCA_PERMISO_DET
            SET tiempo_compensado = NULL
            WHERE perid = NVL(p_perid, perid) AND perfec = v_fec_des;
        END IF;

        IF v_tipo_com = 'I' THEN
            IF NVL(p_tipo_banco,'N') = 'N' THEN
                UPDATE SCA_BANCOHORAS_MES
                SET hc_banhormes = NVL(hc_banhormes,0) - v_min
                WHERE cod_empresa  = v_emp
                AND   cod_personal = v_per
                AND   mes_proceso || '/' || ano_proceso = NVL(p_proceso, v_aux1);
            ELSE
                UPDATE SCA_BANCOHORAS_SEM
                SET hc_banhorsem = NVL(hc_banhorsem,0) - v_min
                WHERE cod_empresa  = v_emp
                AND   cod_personal = v_per
                AND   sem_proceso || '/' || mes_proceso || '/' || ano_proceso = NVL(p_proceso, v_aux1);
            END IF;
        END IF;

        OPEN cv_resultado FOR
            SELECT v_estado estado, v_motivo motivo, v_filas filas_eliminadas FROM DUAL;
    EXCEPTION
        WHEN OTHERS THEN
            v_estado := 'ERR'; v_motivo := 'EXCEPCION: '||SQLERRM;
            OPEN cv_resultado FOR
                SELECT v_estado estado, v_motivo motivo, v_filas filas_eliminadas FROM DUAL;
    END ELIMINAR;


    -- =========================================================================
    -- VALIDAR
    -- =========================================================================
    PROCEDURE VALIDAR(
        p_cod_empresa       IN VARCHAR2,
        p_cod_personal      IN VARCHAR2,
        p_fecha_destino     IN VARCHAR2 DEFAULT NULL,
        p_fecha_origen      IN VARCHAR2,
        p_tipo_origen       IN CHAR,
        p_tipo_compensacion IN CHAR,
        p_horas             IN VARCHAR2,
        cv_resultado        OUT SYS_REFCURSOR
    ) AS
        v_min        NUMBER := fn_hhmi_a_min(p_horas);
        v_disp_ori   NUMBER;
        v_def_des    NUMBER;
        v_puede      VARCHAR2(1) := 'S';
        v_motivo     VARCHAR2(500) := 'OK';
        v_tipo_val   VARCHAR2(10);
        v_fec_ori    DATE := TO_DATE(p_fecha_origen, 'dd/MM/yyyy');
        v_fec_des    DATE := CASE WHEN p_fecha_destino IS NULL THEN NULL
                                  ELSE TO_DATE(p_fecha_destino,'dd/MM/yyyy') END;
    BEGIN
        -- Tipo de validacion
        v_tipo_val := CASE
            WHEN p_tipo_compensacion IN ('A','T','N') THEN 'EXACTA'
            WHEN p_tipo_compensacion IN ('F','P')     THEN 'PARCIAL'
            ELSE 'BANCO'
        END;

        -- Origen
        IF p_tipo_origen IN ('E','D','B') THEN
            v_disp_ori := fn_tiempo_origen(p_cod_empresa, p_cod_personal, v_fec_ori, p_tipo_origen);
            IF v_disp_ori IS NULL OR v_disp_ori < v_min THEN
                v_puede  := 'N';
                v_motivo := 'Origen insuficiente: disp='||NVL(v_disp_ori,0)||
                            ' / sol='||v_min;
            END IF;
        END IF;

        -- Destino
        IF p_tipo_compensacion IN ('A','T','N','F','P') THEN
            v_def_des := fn_tiempo_destino(p_cod_empresa, p_cod_personal, v_fec_des, p_tipo_compensacion);
            IF v_tipo_val = 'EXACTA' AND v_def_des <> v_min THEN
                v_puede  := 'N';
                v_motivo := NVL(v_motivo,'')||' | Destino debe ser EXACTO: '||v_def_des||
                            ' / req '||v_min;
            ELSIF v_tipo_val = 'PARCIAL' AND v_def_des < v_min THEN
                v_puede  := 'N';
                v_motivo := NVL(v_motivo,'')||' | Destino insuficiente: '||v_def_des||
                            ' / req '||v_min;
            END IF;
        END IF;

        OPEN cv_resultado FOR
            SELECT v_puede                  AS puede_aplicar,
                   v_motivo                 AS motivo,
                   v_min                    AS tiempo_solicitado_min,
                   NVL(v_disp_ori, 0)       AS tiempo_disponible_origen_min,
                   NVL(v_def_des,  0)       AS tiempo_deficit_destino_min,
                   v_tipo_val               AS tipo_validacion
            FROM   DUAL;
    END VALIDAR;


    -- =========================================================================
    -- APLICAR_DIA  (ATOMICO: 1 empleado, 1 dia)
    -- =========================================================================
    PROCEDURE APLICAR_DIA(
        p_cod_empresa          IN VARCHAR2,
        p_cod_personal         IN VARCHAR2,
        p_fecha                IN VARCHAR2,
        p_eliminar_no_cuadra   IN VARCHAR2 DEFAULT 'S',
        cv_resultado           OUT SYS_REFCURSOR
    ) AS
        v_fec        DATE := TO_DATE(p_fecha, 'dd/MM/yyyy');
        v_apl_des    NUMBER := 0;
        v_apl_ori    NUMBER := 0;
        v_eliminadas NUMBER := 0;
        v_errores    NUMBER := 0;
        v_disp       NUMBER;
        v_horas_hhmi VARCHAR2(10);

        CURSOR c_des IS
            SELECT c.id_compen, c.cod_empresa, c.cod_personal,
                   c.fechadestino, c.fechaorigen,
                   c.tipoorigen, c.tipocompensacion, c.tiempo
            FROM   SCA_COMPENSACION c
            WHERE  c.cod_empresa     = p_cod_empresa
            AND    c.cod_personal    = p_cod_personal
            AND    c.tipocompensacion <> 'I'
            AND    c.fechadestino    = v_fec;

        CURSOR c_ori IS
            SELECT c.id_compen, c.cod_empresa, c.cod_personal,
                   c.fechadestino, c.fechaorigen,
                   c.tipoorigen, c.tipocompensacion, c.tiempo
            FROM   SCA_COMPENSACION c
            WHERE  c.cod_empresa  = p_cod_empresa
            AND    c.cod_personal = p_cod_personal
            AND    c.fechaorigen  = v_fec;
    BEGIN
        IF p_cod_empresa IS NULL OR p_cod_personal IS NULL THEN
            OPEN cv_resultado FOR
                SELECT p_fecha AS fecha, 0 AS aplicadas_destino, 0 AS aplicadas_origen,
                       0 AS eliminadas, 1 AS errores,
                       'cod_empresa y cod_personal son obligatorios' AS motivo
                FROM DUAL;
            RETURN;
        END IF;

        -- ==== CURSOR 1: dia DESTINO ====
        FOR r IN c_des LOOP
            BEGIN
                v_disp       := fn_tiempo_destino(r.cod_empresa, r.cod_personal, r.fechadestino, r.tipocompensacion);
                v_horas_hhmi := fn_min_a_hhmi(r.tiempo);

                IF (r.tipocompensacion IN ('A','T','N') AND v_disp = r.tiempo)
                OR (r.tipocompensacion IN ('F','P')     AND v_disp >= r.tiempo) THEN
                    prv_aplicar_destino(r.cod_empresa, r.cod_personal, r.fechadestino,
                                        r.tipocompensacion, v_horas_hhmi);
                    v_apl_des := v_apl_des + 1;
                ELSE
                    -- Revierte fecha origen y elimina (igual que PASO 15)
                    IF NVL(p_eliminar_no_cuadra,'S') = 'S' THEN
                        prv_revertir_destino(r.cod_empresa, r.cod_personal, r.fechaorigen,
                                             r.tipoorigen, v_horas_hhmi);
                        DELETE SCA_COMPENSACION WHERE id_compen = r.id_compen;
                        v_eliminadas := v_eliminadas + 1;
                    END IF;
                END IF;
            EXCEPTION WHEN OTHERS THEN v_errores := v_errores + 1;
            END;
        END LOOP;

        -- ==== CURSOR 2: dia ORIGEN ====
        FOR r IN c_ori LOOP
            BEGIN
                v_horas_hhmi := fn_min_a_hhmi(r.tiempo);

                IF r.tipoorigen = 'I' THEN
                    prv_aplicar_origen(r.cod_empresa, r.cod_personal, r.fechaorigen,
                                       r.tipoorigen, v_horas_hhmi);
                    v_apl_ori := v_apl_ori + 1;
                ELSE
                    v_disp := fn_tiempo_origen(r.cod_empresa, r.cod_personal, r.fechaorigen, r.tipoorigen);
                    IF v_disp >= r.tiempo THEN
                        prv_aplicar_origen(r.cod_empresa, r.cod_personal, r.fechaorigen,
                                           r.tipoorigen, v_horas_hhmi);
                        v_apl_ori := v_apl_ori + 1;
                    ELSE
                        IF NVL(p_eliminar_no_cuadra,'S') = 'S' THEN
                            prv_revertir_destino(r.cod_empresa, r.cod_personal, r.fechadestino,
                                                 r.tipocompensacion, v_horas_hhmi);
                            DELETE SCA_COMPENSACION WHERE id_compen = r.id_compen;
                            v_eliminadas := v_eliminadas + 1;
                        END IF;
                    END IF;
                END IF;
            EXCEPTION WHEN OTHERS THEN v_errores := v_errores + 1;
            END;
        END LOOP;

        OPEN cv_resultado FOR
            SELECT p_fecha          AS fecha,
                   p_cod_empresa    AS cod_empresa,
                   p_cod_personal   AS cod_personal,
                   v_apl_des        AS aplicadas_destino,
                   v_apl_ori        AS aplicadas_origen,
                   v_eliminadas     AS eliminadas,
                   v_errores        AS errores
            FROM   DUAL;
    END APLICAR_DIA;


    -- =========================================================================
    -- APLICAR_RANGO  (ATOMICO: 1 empleado, N dias)
    -- =========================================================================
    PROCEDURE APLICAR_RANGO(
        p_cod_empresa          IN VARCHAR2,
        p_cod_personal         IN VARCHAR2,
        p_fecha_inicio         IN VARCHAR2,
        p_fecha_fin            IN VARCHAR2,
        p_eliminar_no_cuadra   IN VARCHAR2 DEFAULT 'S',
        cv_resultado           OUT SYS_REFCURSOR
    ) AS
        v_fec_ini  DATE := TO_DATE(p_fecha_inicio, 'dd/MM/yyyy');
        v_fec_fin  DATE := TO_DATE(p_fecha_fin,    'dd/MM/yyyy');
        v_actual   DATE;
        v_total_d  NUMBER := 0;
        v_total_o  NUMBER := 0;
        v_total_e  NUMBER := 0;
        v_total_x  NUMBER := 0;
        v_dias     NUMBER := 0;
        v_cur_dia  SYS_REFCURSOR;
        v_d        NUMBER; v_o NUMBER; v_el NUMBER; v_er NUMBER;
        v_fec_str  VARCHAR2(10);
        v_emp_str  VARCHAR2(20);
        v_per_str  VARCHAR2(20);
    BEGIN
        IF p_cod_empresa IS NULL OR p_cod_personal IS NULL THEN
            OPEN cv_resultado FOR
                SELECT p_fecha_inicio AS fecha_inicio, p_fecha_fin AS fecha_fin,
                       0 AS dias_procesados, 0 AS total_aplicadas_destino,
                       0 AS total_aplicadas_origen, 0 AS total_eliminadas,
                       1 AS total_errores,
                       'cod_empresa y cod_personal son obligatorios' AS motivo
                FROM DUAL;
            RETURN;
        END IF;

        v_actual := v_fec_ini;
        WHILE v_actual <= v_fec_fin LOOP
            v_fec_str := TO_CHAR(v_actual, 'dd/MM/yyyy');
            APLICAR_DIA(p_cod_empresa, p_cod_personal, v_fec_str,
                        p_eliminar_no_cuadra, v_cur_dia);
            FETCH v_cur_dia INTO v_fec_str, v_emp_str, v_per_str, v_d, v_o, v_el, v_er;
            CLOSE v_cur_dia;
            v_total_d := v_total_d + v_d;
            v_total_o := v_total_o + v_o;
            v_total_e := v_total_e + v_el;
            v_total_x := v_total_x + v_er;
            v_dias    := v_dias + 1;
            COMMIT;
            v_actual := v_actual + 1;
        END LOOP;

        OPEN cv_resultado FOR
            SELECT p_fecha_inicio   AS fecha_inicio,
                   p_fecha_fin      AS fecha_fin,
                   p_cod_empresa    AS cod_empresa,
                   p_cod_personal   AS cod_personal,
                   v_dias           AS dias_procesados,
                   v_total_d        AS total_aplicadas_destino,
                   v_total_o        AS total_aplicadas_origen,
                   v_total_e        AS total_eliminadas,
                   v_total_x        AS total_errores
            FROM   DUAL;
    END APLICAR_RANGO;


    -- =========================================================================
    -- REVERTIR_DIA  (ATOMICO: 1 empleado, 1 dia)
    -- =========================================================================
    PROCEDURE REVERTIR_DIA(
        p_cod_empresa     IN VARCHAR2,
        p_cod_personal    IN VARCHAR2,
        p_fecha           IN VARCHAR2,
        cv_resultado      OUT SYS_REFCURSOR
    ) AS
        v_fec        DATE := TO_DATE(p_fecha, 'dd/MM/yyyy');
        v_revertidas NUMBER := 0;
        v_errores    NUMBER := 0;
        v_horas_hhmi VARCHAR2(10);
    BEGIN
        IF p_cod_empresa IS NULL OR p_cod_personal IS NULL THEN
            OPEN cv_resultado FOR
                SELECT p_fecha AS fecha, NULL AS cod_empresa, NULL AS cod_personal,
                       0 AS revertidas, 1 AS errores,
                       'cod_empresa y cod_personal son obligatorios' AS motivo
                FROM DUAL;
            RETURN;
        END IF;

        FOR r IN (
            SELECT c.id_compen, c.cod_empresa, c.cod_personal,
                   c.fechadestino, c.fechaorigen,
                   c.tipoorigen, c.tipocompensacion, c.tiempo
            FROM   SCA_COMPENSACION c
            WHERE  c.cod_empresa  = p_cod_empresa
            AND    c.cod_personal = p_cod_personal
            AND    (c.fechadestino = v_fec OR c.fechaorigen = v_fec)
        ) LOOP
            BEGIN
                v_horas_hhmi := fn_min_a_hhmi(r.tiempo);
                IF r.tipocompensacion <> 'I' THEN
                    prv_revertir_destino(r.cod_empresa, r.cod_personal,
                                         r.fechadestino, r.tipocompensacion, v_horas_hhmi);
                END IF;
                IF r.tipoorigen <> 'I' THEN
                    prv_revertir_origen(r.cod_empresa, r.cod_personal,
                                        r.fechaorigen, r.tipoorigen, v_horas_hhmi);
                END IF;
                v_revertidas := v_revertidas + 1;
            EXCEPTION WHEN OTHERS THEN v_errores := v_errores + 1;
            END;
        END LOOP;

        OPEN cv_resultado FOR
            SELECT p_fecha         AS fecha,
                   p_cod_empresa   AS cod_empresa,
                   p_cod_personal  AS cod_personal,
                   v_revertidas    AS revertidas,
                   v_errores       AS errores
            FROM   DUAL;
    END REVERTIR_DIA;


    -- =========================================================================
    -- REVERTIR_RANGO  (ATOMICO: 1 empleado, N dias)
    -- =========================================================================
    PROCEDURE REVERTIR_RANGO(
        p_cod_empresa     IN VARCHAR2,
        p_cod_personal    IN VARCHAR2,
        p_fecha_inicio    IN VARCHAR2,
        p_fecha_fin       IN VARCHAR2,
        cv_resultado      OUT SYS_REFCURSOR
    ) AS
        v_fec_ini  DATE := TO_DATE(p_fecha_inicio, 'dd/MM/yyyy');
        v_fec_fin  DATE := TO_DATE(p_fecha_fin,    'dd/MM/yyyy');
        v_actual   DATE;
        v_total_r  NUMBER := 0;
        v_total_x  NUMBER := 0;
        v_dias     NUMBER := 0;
        v_cur_dia  SYS_REFCURSOR;
        v_fec_str  VARCHAR2(10);
        v_emp_str  VARCHAR2(20);
        v_per_str  VARCHAR2(20);
        v_r        NUMBER; v_x NUMBER;
    BEGIN
        IF p_cod_empresa IS NULL OR p_cod_personal IS NULL THEN
            OPEN cv_resultado FOR
                SELECT p_fecha_inicio AS fecha_inicio, p_fecha_fin AS fecha_fin,
                       NULL AS cod_empresa, NULL AS cod_personal,
                       0 AS dias_procesados, 0 AS total_revertidas, 1 AS total_errores,
                       'cod_empresa y cod_personal son obligatorios' AS motivo
                FROM DUAL;
            RETURN;
        END IF;

        v_actual := v_fec_ini;
        WHILE v_actual <= v_fec_fin LOOP
            v_fec_str := TO_CHAR(v_actual, 'dd/MM/yyyy');
            REVERTIR_DIA(p_cod_empresa, p_cod_personal, v_fec_str, v_cur_dia);
            FETCH v_cur_dia INTO v_fec_str, v_emp_str, v_per_str, v_r, v_x;
            CLOSE v_cur_dia;
            v_total_r := v_total_r + v_r;
            v_total_x := v_total_x + v_x;
            v_dias    := v_dias + 1;
            COMMIT;
            v_actual := v_actual + 1;
        END LOOP;

        OPEN cv_resultado FOR
            SELECT p_fecha_inicio   AS fecha_inicio,
                   p_fecha_fin      AS fecha_fin,
                   p_cod_empresa    AS cod_empresa,
                   p_cod_personal   AS cod_personal,
                   v_dias           AS dias_procesados,
                   v_total_r        AS total_revertidas,
                   v_total_x        AS total_errores
            FROM   DUAL;
    END REVERTIR_RANGO;


    -- =========================================================================
    -- CAPA MASIVA: itera empleados (PLA_PERSONAL) y llama capa atomica
    -- =========================================================================

    PROCEDURE APLICAR_DIA_MASIVO(
        p_cod_empresa          IN VARCHAR2 DEFAULT NULL,
        p_cod_personal         IN VARCHAR2 DEFAULT NULL,
        p_fecha                IN VARCHAR2,
        p_eliminar_no_cuadra   IN VARCHAR2 DEFAULT 'S',
        cv_resultado           OUT SYS_REFCURSOR
    ) AS
        v_emp_filtro VARCHAR2(20) := NVL(p_cod_empresa,'%');
        v_per_filtro VARCHAR2(20) := NVL(p_cod_personal,'%');
        v_total_emp  NUMBER := 0;
        v_total_d    NUMBER := 0;
        v_total_o    NUMBER := 0;
        v_total_e    NUMBER := 0;
        v_total_x    NUMBER := 0;
        v_cur        SYS_REFCURSOR;
        v_fec_str    VARCHAR2(10);
        v_emp_str    VARCHAR2(20);
        v_per_str    VARCHAR2(20);
        v_d NUMBER; v_o NUMBER; v_el NUMBER; v_er NUMBER;
    BEGIN
        FOR e IN (
            SELECT DISTINCT p.cod_empresa, p.cod_personal
            FROM   PLA_PERSONAL p
            WHERE  p.cod_empresa  LIKE v_emp_filtro
            AND    p.cod_personal LIKE v_per_filtro
            -- Solo empleados con compensaciones en esa fecha (optimiza)
            AND    EXISTS (
                SELECT 1 FROM SCA_COMPENSACION c
                WHERE  c.cod_empresa  = p.cod_empresa
                AND    c.cod_personal = p.cod_personal
                AND    (c.fechadestino = TO_DATE(p_fecha,'dd/MM/yyyy')
                     OR c.fechaorigen  = TO_DATE(p_fecha,'dd/MM/yyyy'))
            )
            ORDER BY p.cod_empresa, p.cod_personal
        ) LOOP
            BEGIN
                APLICAR_DIA(e.cod_empresa, e.cod_personal, p_fecha,
                            p_eliminar_no_cuadra, v_cur);
                FETCH v_cur INTO v_fec_str, v_emp_str, v_per_str, v_d, v_o, v_el, v_er;
                CLOSE v_cur;
                v_total_d := v_total_d + NVL(v_d,0);
                v_total_o := v_total_o + NVL(v_o,0);
                v_total_e := v_total_e + NVL(v_el,0);
                v_total_x := v_total_x + NVL(v_er,0);
                v_total_emp := v_total_emp + 1;
                COMMIT;
            EXCEPTION WHEN OTHERS THEN
                v_total_x := v_total_x + 1;
                IF v_cur%ISOPEN THEN CLOSE v_cur; END IF;
            END;
        END LOOP;

        OPEN cv_resultado FOR
            SELECT p_fecha           AS fecha,
                   v_total_emp       AS empleados_procesados,
                   v_total_d         AS total_aplicadas_destino,
                   v_total_o         AS total_aplicadas_origen,
                   v_total_e         AS total_eliminadas,
                   v_total_x         AS total_errores
            FROM   DUAL;
    END APLICAR_DIA_MASIVO;


    PROCEDURE APLICAR_RANGO_MASIVO(
        p_cod_empresa          IN VARCHAR2 DEFAULT NULL,
        p_cod_personal         IN VARCHAR2 DEFAULT NULL,
        p_fecha_inicio         IN VARCHAR2,
        p_fecha_fin            IN VARCHAR2,
        p_eliminar_no_cuadra   IN VARCHAR2 DEFAULT 'S',
        cv_resultado           OUT SYS_REFCURSOR
    ) AS
        v_emp_filtro VARCHAR2(20) := NVL(p_cod_empresa,'%');
        v_per_filtro VARCHAR2(20) := NVL(p_cod_personal,'%');
        v_fec_ini    DATE := TO_DATE(p_fecha_inicio,'dd/MM/yyyy');
        v_fec_fin    DATE := TO_DATE(p_fecha_fin,   'dd/MM/yyyy');
        v_total_emp  NUMBER := 0;
        v_total_d    NUMBER := 0;
        v_total_o    NUMBER := 0;
        v_total_e    NUMBER := 0;
        v_total_x    NUMBER := 0;
        v_cur        SYS_REFCURSOR;
        v_fi VARCHAR2(10); v_ff VARCHAR2(10);
        v_emp_str VARCHAR2(20); v_per_str VARCHAR2(20);
        v_dias NUMBER; v_d NUMBER; v_o NUMBER; v_el NUMBER; v_er NUMBER;
    BEGIN
        FOR e IN (
            SELECT DISTINCT p.cod_empresa, p.cod_personal
            FROM   PLA_PERSONAL p
            WHERE  p.cod_empresa  LIKE v_emp_filtro
            AND    p.cod_personal LIKE v_per_filtro
            AND    EXISTS (
                SELECT 1 FROM SCA_COMPENSACION c
                WHERE  c.cod_empresa  = p.cod_empresa
                AND    c.cod_personal = p.cod_personal
                AND    NVL(c.fechadestino, c.fechaorigen) BETWEEN v_fec_ini AND v_fec_fin
            )
            ORDER BY p.cod_empresa, p.cod_personal
        ) LOOP
            BEGIN
                APLICAR_RANGO(e.cod_empresa, e.cod_personal,
                              p_fecha_inicio, p_fecha_fin,
                              p_eliminar_no_cuadra, v_cur);
                FETCH v_cur INTO v_fi, v_ff, v_emp_str, v_per_str,
                                  v_dias, v_d, v_o, v_el, v_er;
                CLOSE v_cur;
                v_total_d := v_total_d + NVL(v_d,0);
                v_total_o := v_total_o + NVL(v_o,0);
                v_total_e := v_total_e + NVL(v_el,0);
                v_total_x := v_total_x + NVL(v_er,0);
                v_total_emp := v_total_emp + 1;
            EXCEPTION WHEN OTHERS THEN
                v_total_x := v_total_x + 1;
                IF v_cur%ISOPEN THEN CLOSE v_cur; END IF;
            END;
        END LOOP;

        OPEN cv_resultado FOR
            SELECT p_fecha_inicio   AS fecha_inicio,
                   p_fecha_fin      AS fecha_fin,
                   v_total_emp      AS empleados_procesados,
                   v_total_d        AS total_aplicadas_destino,
                   v_total_o        AS total_aplicadas_origen,
                   v_total_e        AS total_eliminadas,
                   v_total_x        AS total_errores
            FROM   DUAL;
    END APLICAR_RANGO_MASIVO;


    PROCEDURE REVERTIR_DIA_MASIVO(
        p_cod_empresa     IN VARCHAR2 DEFAULT NULL,
        p_cod_personal    IN VARCHAR2 DEFAULT NULL,
        p_fecha           IN VARCHAR2,
        cv_resultado      OUT SYS_REFCURSOR
    ) AS
        v_emp_filtro VARCHAR2(20) := NVL(p_cod_empresa,'%');
        v_per_filtro VARCHAR2(20) := NVL(p_cod_personal,'%');
        v_total_emp  NUMBER := 0;
        v_total_r    NUMBER := 0;
        v_total_x    NUMBER := 0;
        v_cur        SYS_REFCURSOR;
        v_fec_str VARCHAR2(10); v_emp_str VARCHAR2(20); v_per_str VARCHAR2(20);
        v_r NUMBER; v_x NUMBER;
    BEGIN
        FOR e IN (
            SELECT DISTINCT p.cod_empresa, p.cod_personal
            FROM   PLA_PERSONAL p
            WHERE  p.cod_empresa  LIKE v_emp_filtro
            AND    p.cod_personal LIKE v_per_filtro
            AND    EXISTS (
                SELECT 1 FROM SCA_COMPENSACION c
                WHERE  c.cod_empresa  = p.cod_empresa
                AND    c.cod_personal = p.cod_personal
                AND    (c.fechadestino = TO_DATE(p_fecha,'dd/MM/yyyy')
                     OR c.fechaorigen  = TO_DATE(p_fecha,'dd/MM/yyyy'))
            )
            ORDER BY p.cod_empresa, p.cod_personal
        ) LOOP
            BEGIN
                REVERTIR_DIA(e.cod_empresa, e.cod_personal, p_fecha, v_cur);
                FETCH v_cur INTO v_fec_str, v_emp_str, v_per_str, v_r, v_x;
                CLOSE v_cur;
                v_total_r := v_total_r + NVL(v_r,0);
                v_total_x := v_total_x + NVL(v_x,0);
                v_total_emp := v_total_emp + 1;
                COMMIT;
            EXCEPTION WHEN OTHERS THEN
                v_total_x := v_total_x + 1;
                IF v_cur%ISOPEN THEN CLOSE v_cur; END IF;
            END;
        END LOOP;

        OPEN cv_resultado FOR
            SELECT p_fecha           AS fecha,
                   v_total_emp       AS empleados_procesados,
                   v_total_r         AS total_revertidas,
                   v_total_x         AS total_errores
            FROM   DUAL;
    END REVERTIR_DIA_MASIVO;


    PROCEDURE REVERTIR_RANGO_MASIVO(
        p_cod_empresa     IN VARCHAR2 DEFAULT NULL,
        p_cod_personal    IN VARCHAR2 DEFAULT NULL,
        p_fecha_inicio    IN VARCHAR2,
        p_fecha_fin       IN VARCHAR2,
        cv_resultado      OUT SYS_REFCURSOR
    ) AS
        v_emp_filtro VARCHAR2(20) := NVL(p_cod_empresa,'%');
        v_per_filtro VARCHAR2(20) := NVL(p_cod_personal,'%');
        v_fec_ini    DATE := TO_DATE(p_fecha_inicio,'dd/MM/yyyy');
        v_fec_fin    DATE := TO_DATE(p_fecha_fin,   'dd/MM/yyyy');
        v_total_emp  NUMBER := 0;
        v_total_r    NUMBER := 0;
        v_total_x    NUMBER := 0;
        v_cur        SYS_REFCURSOR;
        v_fi VARCHAR2(10); v_ff VARCHAR2(10);
        v_emp_str VARCHAR2(20); v_per_str VARCHAR2(20);
        v_dias NUMBER; v_r NUMBER; v_x NUMBER;
    BEGIN
        FOR e IN (
            SELECT DISTINCT p.cod_empresa, p.cod_personal
            FROM   PLA_PERSONAL p
            WHERE  p.cod_empresa  LIKE v_emp_filtro
            AND    p.cod_personal LIKE v_per_filtro
            AND    EXISTS (
                SELECT 1 FROM SCA_COMPENSACION c
                WHERE  c.cod_empresa  = p.cod_empresa
                AND    c.cod_personal = p.cod_personal
                AND    NVL(c.fechadestino, c.fechaorigen) BETWEEN v_fec_ini AND v_fec_fin
            )
            ORDER BY p.cod_empresa, p.cod_personal
        ) LOOP
            BEGIN
                REVERTIR_RANGO(e.cod_empresa, e.cod_personal,
                               p_fecha_inicio, p_fecha_fin, v_cur);
                FETCH v_cur INTO v_fi, v_ff, v_emp_str, v_per_str, v_dias, v_r, v_x;
                CLOSE v_cur;
                v_total_r := v_total_r + NVL(v_r,0);
                v_total_x := v_total_x + NVL(v_x,0);
                v_total_emp := v_total_emp + 1;
            EXCEPTION WHEN OTHERS THEN
                v_total_x := v_total_x + 1;
                IF v_cur%ISOPEN THEN CLOSE v_cur; END IF;
            END;
        END LOOP;

        OPEN cv_resultado FOR
            SELECT p_fecha_inicio   AS fecha_inicio,
                   p_fecha_fin      AS fecha_fin,
                   v_total_emp      AS empleados_procesados,
                   v_total_r        AS total_revertidas,
                   v_total_x        AS total_errores
            FROM   DUAL;
    END REVERTIR_RANGO_MASIVO;


    -- =========================================================================
    -- REGISTRAR_EVENTO
    -- =========================================================================
    PROCEDURE REGISTRAR_EVENTO(
        p_cod_empresa        IN VARCHAR2,
        p_fecha_origen       IN VARCHAR2,
        p_fecha_destino      IN VARCHAR2 DEFAULT NULL,
        p_tipo_origen        IN CHAR,
        p_tipo_compensacion  IN CHAR,
        p_lista_personal     IN VARCHAR2 DEFAULT NULL,
        p_horas_override     IN VARCHAR2 DEFAULT NULL,
        p_aplicar            IN VARCHAR2 DEFAULT 'N',
        p_eliminar_no_cuadra IN VARCHAR2 DEFAULT 'S',
        p_validar            IN VARCHAR2 DEFAULT 'S',
        cv_resultado         OUT SYS_REFCURSOR
    ) AS
        c_BASE   CONSTANT DATE := TO_DATE('01/01/1900','dd/MM/yyyy');
        v_fec_ori    DATE := TO_DATE(p_fecha_origen, 'dd/MM/yyyy');
        v_total_emp  NUMBER := 0;
        v_total_ok   NUMBER := 0;
        v_total_apl  NUMBER := 0;
        v_total_err  NUMBER := 0;
        v_horas      VARCHAR2(10);
        v_min_orig   NUMBER;
        v_cur_reg    SYS_REFCURSOR;
        v_cur_apl    SYS_REFCURSOR;
        v_id_compen  NUMBER;
        v_estado     VARCHAR2(10);
        v_motivo     VARCHAR2(500);
        v_tiempo_min NUMBER;
        -- variables para FETCH de APLICAR_DIA (7 columnas)
        v_fec_str    VARCHAR2(10);
        v_emp_str    VARCHAR2(20);
        v_per_str    VARCHAR2(20);
        v_apl_d      NUMBER; v_apl_o NUMBER; v_apl_el NUMBER; v_apl_er NUMBER;
    BEGIN
        FOR t IN (
            SELECT t.cod_empresa, t.cod_personal,
                   t.horaextra_ajus, t.horadoblesof, t.horabancoh
            FROM   SCA_ASISTENCIA_TAREO t
            WHERE  t.cod_empresa = p_cod_empresa
            AND    t.fechamar    = v_fec_ori
            -- solo empleados con horas en el tipo origen solicitado
            AND    CASE p_tipo_origen
                       WHEN 'E' THEN CASE WHEN t.horaextra_ajus > c_BASE THEN 1 ELSE 0 END
                       WHEN 'D' THEN CASE WHEN t.horadoblesof   > c_BASE THEN 1 ELSE 0 END
                       WHEN 'B' THEN CASE WHEN t.horabancoh     > c_BASE THEN 1 ELSE 0 END
                       ELSE 0 END = 1
            -- filtro lista_personal (NULL = todos)
            AND    (p_lista_personal IS NULL
                    OR INSTR(','||p_lista_personal||',', ','||t.cod_personal||',') > 0)
            ORDER BY t.cod_personal
        ) LOOP
            BEGIN
                v_total_emp := v_total_emp + 1;

                -- Determinar horas: manual (override) o auto desde tareo
                IF p_horas_override IS NOT NULL THEN
                    v_horas := p_horas_override;
                ELSE
                    v_min_orig := CASE p_tipo_origen
                        WHEN 'E' THEN ROUND((t.horaextra_ajus - c_BASE)*1440)
                        WHEN 'D' THEN ROUND((t.horadoblesof   - c_BASE)*1440)
                        WHEN 'B' THEN ROUND((t.horabancoh     - c_BASE)*1440)
                        ELSE 0 END;
                    v_horas := fn_min_a_hhmi(v_min_orig);
                END IF;

                -- Registrar compensacion para este empleado
                REGISTRAR(
                    p_cod_empresa       => t.cod_empresa,
                    p_cod_personal      => t.cod_personal,
                    p_fecha_destino     => p_fecha_destino,
                    p_fecha_origen      => p_fecha_origen,
                    p_tipo_origen       => p_tipo_origen,
                    p_tipo_compensacion => p_tipo_compensacion,
                    p_horas             => v_horas,
                    p_validar           => p_validar,
                    cv_resultado        => v_cur_reg
                );
                FETCH v_cur_reg INTO v_id_compen, v_estado, v_motivo, v_tiempo_min;
                CLOSE v_cur_reg;

                IF v_estado = 'OK' THEN
                    v_total_ok := v_total_ok + 1;

                    -- Aplicar si se solicito (ambos dias: origen y destino)
                    IF NVL(p_aplicar,'N') = 'S' THEN
                        -- Lado ORIGEN: marca el domingo como consumido
                        APLICAR_DIA(t.cod_empresa, t.cod_personal, p_fecha_origen,
                                    p_eliminar_no_cuadra, v_cur_apl);
                        FETCH v_cur_apl INTO v_fec_str, v_emp_str, v_per_str,
                                            v_apl_d, v_apl_o, v_apl_el, v_apl_er;
                        CLOSE v_cur_apl;
                        -- Lado DESTINO: marca el feriado como compensado
                        IF p_fecha_destino IS NOT NULL THEN
                            APLICAR_DIA(t.cod_empresa, t.cod_personal, p_fecha_destino,
                                        p_eliminar_no_cuadra, v_cur_apl);
                            FETCH v_cur_apl INTO v_fec_str, v_emp_str, v_per_str,
                                                v_apl_d, v_apl_o, v_apl_el, v_apl_er;
                            CLOSE v_cur_apl;
                        END IF;
                        v_total_apl := v_total_apl + 1;
                    END IF;
                    COMMIT;
                ELSE
                    v_total_err := v_total_err + 1;
                END IF;
            EXCEPTION WHEN OTHERS THEN
                v_total_err := v_total_err + 1;
                IF v_cur_reg%ISOPEN THEN CLOSE v_cur_reg; END IF;
                IF v_cur_apl%ISOPEN THEN CLOSE v_cur_apl; END IF;
            END;
        END LOOP;

        OPEN cv_resultado FOR
            SELECT p_fecha_origen        AS fecha_origen,
                   p_fecha_destino       AS fecha_destino,
                   p_tipo_origen         AS tipo_origen,
                   p_tipo_compensacion   AS tipo_compensacion,
                   v_total_emp           AS empleados_encontrados,
                   v_total_ok            AS registradas_ok,
                   v_total_apl           AS aplicadas_ok,
                   v_total_err           AS errores,
                   CASE WHEN v_total_err = 0 AND v_total_ok > 0 THEN 'OK'
                        WHEN v_total_err > 0 AND v_total_ok > 0 THEN 'PARCIAL'
                        WHEN v_total_ok  = 0 THEN 'SIN_DATOS'
                        ELSE 'ERR' END  AS estado
            FROM   DUAL;
    END REGISTRAR_EVENTO;


    -- =========================================================================
    -- CONSULTAR_RANGO
    -- =========================================================================
    PROCEDURE CONSULTAR_RANGO(
        p_cod_empresa     IN VARCHAR2 DEFAULT NULL,
        p_cod_personal    IN VARCHAR2 DEFAULT NULL,
        p_fecha_inicio    IN VARCHAR2,
        p_fecha_fin       IN VARCHAR2,
        cv_resultado      OUT SYS_REFCURSOR
    ) AS
        v_fec_ini DATE := TO_DATE(p_fecha_inicio, 'dd/MM/yyyy');
        v_fec_fin DATE := TO_DATE(p_fecha_fin,    'dd/MM/yyyy');
        v_emp     VARCHAR2(20) := NVL(p_cod_empresa,'%');
        v_per     VARCHAR2(20) := NVL(p_cod_personal,'%');
    BEGIN
        OPEN cv_resultado FOR
            SELECT c.id_compen,
                   c.cod_empresa,
                   c.cod_personal,
                   c.fechaorigen,
                   c.fechadestino,
                   c.tipoorigen,
                   c.tipocompensacion,
                   c.tiempo                                AS tiempo_min,
                   SUBSTR('00'||TO_CHAR(TRUNC(c.tiempo/60)),-2,2)
                     ||':'||SUBSTR('00'||TO_CHAR(MOD(c.tiempo,60)),-2,2) AS tiempo_hhmi,
                   c.aux1                                  AS periodo,
                   tdes.alerta02 AS dest_alerta02,
                   tdes.alerta03 AS dest_alerta03,
                   tdes.alerta04 AS dest_alerta04,
                   tdes.alerta07 AS dest_alerta07,
                   tdes.alerta09 AS dest_alerta09,
                   tori.alerta06 AS ori_alerta06,
                   tori.alerta08 AS ori_alerta08,
                   CASE
                     WHEN c.tipocompensacion = 'I' THEN 'BANCO'
                     WHEN tdes.alerta04 = 'TC' OR tdes.alerta07 = 'SC'
                       OR tdes.alerta03 = 'HC' OR tdes.alerta02 = 'FC'
                       OR tdes.alerta09 = 'PC' THEN 'APLICADA'
                     ELSE 'PENDIENTE'
                   END AS estado_aplicacion
            FROM   SCA_COMPENSACION c
            LEFT JOIN SCA_ASISTENCIA_TAREO tdes
                   ON tdes.cod_empresa  = c.cod_empresa
                  AND tdes.cod_personal = c.cod_personal
                  AND tdes.fechamar     = c.fechadestino
            LEFT JOIN SCA_ASISTENCIA_TAREO tori
                   ON tori.cod_empresa  = c.cod_empresa
                  AND tori.cod_personal = c.cod_personal
                  AND tori.fechamar     = c.fechaorigen
            WHERE  c.cod_empresa  LIKE v_emp
            AND    c.cod_personal LIKE v_per
            AND    NVL(c.fechadestino, c.fechaorigen) BETWEEN v_fec_ini AND v_fec_fin
            ORDER  BY c.fechaorigen, c.fechadestino, c.id_compen;
    END CONSULTAR_RANGO;


    -- =========================================================================
    -- CONSULTAR_SALDO_BANCO
    -- =========================================================================
    PROCEDURE CONSULTAR_SALDO_BANCO(
        p_cod_empresa     IN VARCHAR2,
        p_cod_personal    IN VARCHAR2,
        p_anio            IN NUMBER   DEFAULT NULL,
        p_mes             IN NUMBER   DEFAULT NULL,
        cv_resultado      OUT SYS_REFCURSOR
    ) AS
    BEGIN
        OPEN cv_resultado FOR
            SELECT 'MES' AS tipo_banco,
                   m.ano_proceso, m.mes_proceso, NULL AS sem_proceso,
                   m.hc_banhormes AS saldo_min,
                   SUBSTR('00'||TO_CHAR(TRUNC(m.hc_banhormes/60)),-2,2)
                     ||':'||SUBSTR('00'||TO_CHAR(MOD(m.hc_banhormes,60)),-2,2) AS saldo_hhmi
            FROM   SCA_BANCOHORAS_MES m
            WHERE  m.cod_empresa  = p_cod_empresa
            AND    m.cod_personal = p_cod_personal
            AND    (p_anio IS NULL OR TO_NUMBER(m.ano_proceso) = p_anio)
            AND    (p_mes  IS NULL OR TO_NUMBER(m.mes_proceso) = p_mes)
            UNION ALL
            SELECT 'SEM' AS tipo_banco,
                   s.ano_proceso, s.mes_proceso, s.sem_proceso,
                   s.hc_banhorsem AS saldo_min,
                   SUBSTR('00'||TO_CHAR(TRUNC(s.hc_banhorsem/60)),-2,2)
                     ||':'||SUBSTR('00'||TO_CHAR(MOD(s.hc_banhorsem,60)),-2,2) AS saldo_hhmi
            FROM   SCA_BANCOHORAS_SEM s
            WHERE  s.cod_empresa  = p_cod_empresa
            AND    s.cod_personal = p_cod_personal
            AND    (p_anio IS NULL OR TO_NUMBER(s.ano_proceso) = p_anio)
            AND    (p_mes  IS NULL OR TO_NUMBER(s.mes_proceso) = p_mes)
            ORDER  BY 2, 3, 4;
    END CONSULTAR_SALDO_BANCO;


    -- =========================================================================
    -- VER_ESTADO
    -- =========================================================================
    PROCEDURE VER_ESTADO(
        p_id_compen       IN NUMBER,
        cv_resultado      OUT SYS_REFCURSOR
    ) AS
    BEGIN
        OPEN cv_resultado FOR
            SELECT c.id_compen,
                   c.cod_empresa, c.cod_personal,
                   c.fechaorigen, c.fechadestino,
                   c.tipoorigen, c.tipocompensacion,
                   c.tiempo AS tiempo_min,
                   SUBSTR('00'||TO_CHAR(TRUNC(c.tiempo/60)),-2,2)
                     ||':'||SUBSTR('00'||TO_CHAR(MOD(c.tiempo,60)),-2,2) AS tiempo_hhmi,
                   c.aux1 AS periodo,
                   -- Tareo origen
                   tori.horaextra_ajus AS ori_he_ajus,
                   tori.horadoblesof   AS ori_dobles,
                   tori.horabancoh     AS ori_banco,
                   tori.alerta06       AS ori_alerta06,
                   tori.alerta08       AS ori_alerta08,
                   -- Tareo destino
                   tdes.horatardanza        AS des_tardanza,
                   tdes.horaantesalida      AS des_antesalida,
                   tdes.horas_no_trabajadas AS des_no_trab,
                   tdes.horas_falta         AS des_falta,
                   tdes.horapermiso         AS des_permiso,
                   tdes.alerta02            AS des_alerta02,
                   tdes.alerta03            AS des_alerta03,
                   tdes.alerta04            AS des_alerta04,
                   tdes.alerta07            AS des_alerta07,
                   tdes.alerta09            AS des_alerta09,
                   -- Empleado
                   p.nom_trabajador, p.ape_paterno, p.ape_materno
            FROM   SCA_COMPENSACION c
            LEFT JOIN SCA_ASISTENCIA_TAREO tori
                   ON tori.cod_empresa  = c.cod_empresa
                  AND tori.cod_personal = c.cod_personal
                  AND tori.fechamar     = c.fechaorigen
            LEFT JOIN SCA_ASISTENCIA_TAREO tdes
                   ON tdes.cod_empresa  = c.cod_empresa
                  AND tdes.cod_personal = c.cod_personal
                  AND tdes.fechamar     = c.fechadestino
            LEFT JOIN PLA_PERSONAL p
                   ON p.cod_empresa  = c.cod_empresa
                  AND p.cod_personal = c.cod_personal
            WHERE  c.id_compen = p_id_compen;
    END VER_ESTADO;


    -- =========================================================================
    -- DIAGNOSTICO_RANGO
    -- =========================================================================
    PROCEDURE DIAGNOSTICO_RANGO(
        p_cod_empresa     IN VARCHAR2,
        p_cod_personal    IN VARCHAR2,
        p_fecha_inicio    IN VARCHAR2,
        p_fecha_fin       IN VARCHAR2,
        cv_resultado      OUT SYS_REFCURSOR
    ) AS
    BEGIN
        OPEN cv_resultado FOR
            SELECT
                TO_CHAR(t.fechamar,'DD/MM/YYYY')  AS fecha,
                t.cod_empresa,
                t.cod_personal,
                -- ----------------------------------------------------------------
                -- ORIGEN: cuanto tiene disponible para ceder
                -- Minutos: ROUND((campo - base)*1440)  |  'HH:MI': SUBSTR de chars
                -- ----------------------------------------------------------------
                CASE WHEN t.horaextra_ajus > TO_DATE('01/01/1900','dd/MM/yyyy')
                     THEN ROUND((t.horaextra_ajus - TO_DATE('01/01/1900','dd/MM/yyyy'))*1440)
                     ELSE 0 END                    AS he_min,
                CASE WHEN t.horaextra_ajus > TO_DATE('01/01/1900','dd/MM/yyyy')
                     THEN SUBSTR('00'||TO_CHAR(TRUNC((t.horaextra_ajus - TO_DATE('01/01/1900','dd/MM/yyyy'))*1440/60)),-2,2)
                          ||':'||SUBSTR('00'||TO_CHAR(MOD(ROUND((t.horaextra_ajus - TO_DATE('01/01/1900','dd/MM/yyyy'))*1440),60)),-2,2)
                     END                           AS he_hhmi,
                CASE WHEN t.horadoblesof > TO_DATE('01/01/1900','dd/MM/yyyy')
                     THEN ROUND((t.horadoblesof - TO_DATE('01/01/1900','dd/MM/yyyy'))*1440)
                     ELSE 0 END                    AS dobles_min,
                CASE WHEN t.horadoblesof > TO_DATE('01/01/1900','dd/MM/yyyy')
                     THEN SUBSTR('00'||TO_CHAR(TRUNC((t.horadoblesof - TO_DATE('01/01/1900','dd/MM/yyyy'))*1440/60)),-2,2)
                          ||':'||SUBSTR('00'||TO_CHAR(MOD(ROUND((t.horadoblesof - TO_DATE('01/01/1900','dd/MM/yyyy'))*1440),60)),-2,2)
                     END                           AS dobles_hhmi,
                CASE WHEN t.horabancoh > TO_DATE('01/01/1900','dd/MM/yyyy')
                     THEN ROUND((t.horabancoh - TO_DATE('01/01/1900','dd/MM/yyyy'))*1440)
                     ELSE 0 END                    AS banco_min,
                CASE WHEN t.horabancoh > TO_DATE('01/01/1900','dd/MM/yyyy')
                     THEN SUBSTR('00'||TO_CHAR(TRUNC((t.horabancoh - TO_DATE('01/01/1900','dd/MM/yyyy'))*1440/60)),-2,2)
                          ||':'||SUBSTR('00'||TO_CHAR(MOD(ROUND((t.horabancoh - TO_DATE('01/01/1900','dd/MM/yyyy'))*1440),60)),-2,2)
                     END                           AS banco_hhmi,
                -- ----------------------------------------------------------------
                -- DESTINO: cuanto le falta / debe cubrir
                -- ----------------------------------------------------------------
                CASE WHEN t.horatardanza > TO_DATE('01/01/1900','dd/MM/yyyy')
                     THEN ROUND((t.horatardanza - TO_DATE('01/01/1900','dd/MM/yyyy'))*1440)
                     ELSE 0 END                    AS tard_min,
                CASE WHEN t.horatardanza > TO_DATE('01/01/1900','dd/MM/yyyy')
                     THEN SUBSTR('00'||TO_CHAR(TRUNC((t.horatardanza - TO_DATE('01/01/1900','dd/MM/yyyy'))*1440/60)),-2,2)
                          ||':'||SUBSTR('00'||TO_CHAR(MOD(ROUND((t.horatardanza - TO_DATE('01/01/1900','dd/MM/yyyy'))*1440),60)),-2,2)
                     END                           AS tard_hhmi,
                CASE WHEN t.horaantesalida > TO_DATE('01/01/1900','dd/MM/yyyy')
                     THEN ROUND((t.horaantesalida - TO_DATE('01/01/1900','dd/MM/yyyy'))*1440)
                     ELSE 0 END                    AS antes_min,
                CASE WHEN t.horaantesalida > TO_DATE('01/01/1900','dd/MM/yyyy')
                     THEN SUBSTR('00'||TO_CHAR(TRUNC((t.horaantesalida - TO_DATE('01/01/1900','dd/MM/yyyy'))*1440/60)),-2,2)
                          ||':'||SUBSTR('00'||TO_CHAR(MOD(ROUND((t.horaantesalida - TO_DATE('01/01/1900','dd/MM/yyyy'))*1440),60)),-2,2)
                     END                           AS antes_hhmi,
                CASE WHEN t.horas_falta > TO_DATE('01/01/1900','dd/MM/yyyy')
                     THEN ROUND((t.horas_falta - TO_DATE('01/01/1900','dd/MM/yyyy'))*1440)
                     ELSE 0 END                    AS falta_min,
                CASE WHEN t.horas_falta > TO_DATE('01/01/1900','dd/MM/yyyy')
                     THEN SUBSTR('00'||TO_CHAR(TRUNC((t.horas_falta - TO_DATE('01/01/1900','dd/MM/yyyy'))*1440/60)),-2,2)
                          ||':'||SUBSTR('00'||TO_CHAR(MOD(ROUND((t.horas_falta - TO_DATE('01/01/1900','dd/MM/yyyy'))*1440),60)),-2,2)
                     END                           AS falta_hhmi,
                CASE WHEN t.horas_no_trabajadas > TO_DATE('01/01/1900','dd/MM/yyyy')
                     THEN ROUND((t.horas_no_trabajadas - TO_DATE('01/01/1900','dd/MM/yyyy'))*1440)
                     ELSE 0 END                    AS notrab_min,
                CASE WHEN t.horas_no_trabajadas > TO_DATE('01/01/1900','dd/MM/yyyy')
                     THEN SUBSTR('00'||TO_CHAR(TRUNC((t.horas_no_trabajadas - TO_DATE('01/01/1900','dd/MM/yyyy'))*1440/60)),-2,2)
                          ||':'||SUBSTR('00'||TO_CHAR(MOD(ROUND((t.horas_no_trabajadas - TO_DATE('01/01/1900','dd/MM/yyyy'))*1440),60)),-2,2)
                     END                           AS notrab_hhmi,
                CASE WHEN t.horapermiso > TO_DATE('01/01/1900','dd/MM/yyyy')
                     THEN ROUND((t.horapermiso - TO_DATE('01/01/1900','dd/MM/yyyy'))*1440)
                     ELSE 0 END                    AS permiso_min,
                CASE WHEN t.horapermiso > TO_DATE('01/01/1900','dd/MM/yyyy')
                     THEN SUBSTR('00'||TO_CHAR(TRUNC((t.horapermiso - TO_DATE('01/01/1900','dd/MM/yyyy'))*1440/60)),-2,2)
                          ||':'||SUBSTR('00'||TO_CHAR(MOD(ROUND((t.horapermiso - TO_DATE('01/01/1900','dd/MM/yyyy'))*1440),60)),-2,2)
                     END                           AS permiso_hhmi,
                -- ----------------------------------------------------------------
                -- COMPENSACIONES YA REGISTRADAS PARA ESTE DIA
                -- (como origen O como destino)
                -- ----------------------------------------------------------------
                NVL((
                    SELECT COUNT(*)
                    FROM   SCA_COMPENSACION c
                    WHERE  c.cod_empresa  = t.cod_empresa
                    AND    c.cod_personal = t.cod_personal
                    AND    (c.fechaorigen = t.fechamar OR c.fechadestino = t.fechamar)
                ),0)                               AS compen_registradas,
                NVL((
                    SELECT COUNT(*)
                    FROM   SCA_COMPENSACION c
                    JOIN   SCA_ASISTENCIA_TAREO td
                           ON  td.cod_empresa  = c.cod_empresa
                           AND td.cod_personal = c.cod_personal
                           AND td.fechamar     = c.fechadestino
                    WHERE  c.cod_empresa  = t.cod_empresa
                    AND    c.cod_personal = t.cod_personal
                    AND    (c.fechaorigen = t.fechamar OR c.fechadestino = t.fechamar)
                    AND    (td.alerta02='FC' OR td.alerta03='HC' OR td.alerta04='TC'
                            OR td.alerta07='SC' OR td.alerta08='DC' OR td.alerta09='PC')
                ),0)                               AS compen_aplicadas,
                -- ----------------------------------------------------------------
                -- FLAGS DE ESTADO DEL DIA
                -- ----------------------------------------------------------------
                CASE WHEN t.horaextra_ajus > TO_DATE('01/01/1900','dd/MM/yyyy')
                          OR t.horadoblesof  > TO_DATE('01/01/1900','dd/MM/yyyy')
                          OR t.horabancoh    > TO_DATE('01/01/1900','dd/MM/yyyy')
                     THEN 'S' ELSE 'N' END         AS tiene_origen,
                CASE WHEN t.horatardanza      > TO_DATE('01/01/1900','dd/MM/yyyy')
                          OR t.horaantesalida > TO_DATE('01/01/1900','dd/MM/yyyy')
                          OR t.horas_falta    > TO_DATE('01/01/1900','dd/MM/yyyy')
                          OR t.horas_no_trabajadas > TO_DATE('01/01/1900','dd/MM/yyyy')
                          OR t.horapermiso    > TO_DATE('01/01/1900','dd/MM/yyyy')
                     THEN 'S' ELSE 'N' END         AS tiene_deficit,
                NVL(t.descanso,'N')                AS es_descanso,
                NVL(t.feriado,'N')                 AS es_feriado,
                NVL(t.alerta01,' ')                AS alerta01,
                -- ----------------------------------------------------------------
                -- SUGERENCIA AUTOMATICA
                -- ----------------------------------------------------------------
                CASE
                    WHEN t.descanso = 'S' AND t.horadoblesof > TO_DATE('01/01/1900','dd/MM/yyyy')
                        THEN 'ORIGEN D: usar dobles de descanso'
                    WHEN t.horaextra_ajus > TO_DATE('01/01/1900','dd/MM/yyyy')
                         AND (t.horas_falta > TO_DATE('01/01/1900','dd/MM/yyyy')
                              OR t.horatardanza > TO_DATE('01/01/1900','dd/MM/yyyy')
                              OR t.horaantesalida > TO_DATE('01/01/1900','dd/MM/yyyy'))
                        THEN 'ORIGEN E -> compensar ' ||
                             CASE WHEN t.horas_falta > TO_DATE('01/01/1900','dd/MM/yyyy')
                                  THEN 'FALTA (F)'
                                  WHEN t.horatardanza > TO_DATE('01/01/1900','dd/MM/yyyy')
                                  THEN 'TARDANZA (T)'
                                  ELSE 'SALIDA ANTES (A)' END
                    WHEN t.horaextra_ajus > TO_DATE('01/01/1900','dd/MM/yyyy')
                        THEN 'ORIGEN E disponible - buscar destino en otro dia'
                    WHEN t.horas_falta > TO_DATE('01/01/1900','dd/MM/yyyy')
                         OR t.horatardanza > TO_DATE('01/01/1900','dd/MM/yyyy')
                        THEN 'DEFICIT sin origen - revisar otros dias con HE'
                    WHEN t.descanso = 'S' AND t.horadoblesof IS NULL
                        THEN 'DESCANSO trabajado sin dobles calculadas'
                    ELSE 'Sin accion requerida'
                END                                AS sugerencia
            FROM   SCA_ASISTENCIA_TAREO t
            WHERE  t.cod_empresa  = p_cod_empresa
            AND    t.cod_personal = p_cod_personal
            AND    t.fechamar BETWEEN TO_DATE(p_fecha_inicio,'dd/MM/yyyy')
                                  AND TO_DATE(p_fecha_fin,   'dd/MM/yyyy')
            ORDER BY t.fechamar;
    END DIAGNOSTICO_RANGO;


    -- =========================================================================
    -- BUSCAR_EMPLEADO
    -- =========================================================================
    PROCEDURE BUSCAR_EMPLEADO(
        p_cod_empresa   IN VARCHAR2,
        p_nombre        IN VARCHAR2 DEFAULT NULL,
        cv_resultado    OUT SYS_REFCURSOR
    ) AS
    BEGIN
        OPEN cv_resultado FOR
            SELECT  p.cod_personal,
                    p.num_fotocheck,
                    TRIM(p.ape_paterno||' '||p.ape_materno||', '||p.nom_trabajador)
                        AS nombre_completo,
                    p.tip_estado
            FROM    PLA_PERSONAL p
            WHERE   p.cod_empresa = p_cod_empresa
            AND p.tip_estado  = 'AC'
            AND     (p_nombre IS NULL
                     OR UPPER(p.nom_trabajador||' '||p.ape_paterno||' '||p.ape_materno)
                        LIKE '%'||UPPER(p_nombre)||'%')
            GROUP BY p.cod_personal, p.num_fotocheck,
                     p.ape_paterno, p.ape_materno, p.nom_trabajador,
                     p.tip_estado
            ORDER BY p.ape_paterno, p.ape_materno, p.nom_trabajador;
    END BUSCAR_EMPLEADO;

END PKG_SCA_COMPENSACIONES;
/

-- =============================================================================
-- VERIFICAR COMPILACION
-- =============================================================================
-- SELECT name, type, line, position, text
-- FROM   user_errors
-- WHERE  name = 'PKG_SCA_COMPENSACIONES'
-- ORDER  BY type, sequence;
