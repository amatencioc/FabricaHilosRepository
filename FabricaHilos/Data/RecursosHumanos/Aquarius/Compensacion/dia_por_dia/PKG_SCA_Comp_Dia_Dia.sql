/*******************************************************************************
 * PKG_SCA_COMP_DIA_DIA
 * -----------------------------------------------------------------------------
 * Compensacion tipo DIA POR DIA: un empleado trabaja en su dia de descanso
 * (o feriado) y ese tiempo se usa para cubrir una ausencia/tardanza/falta
 * en otro dia especifico.
 *
 * MODELO:
 *   - SCA_COMPENSACION      : tabla maestra (TIEMPO en MINUTOS)
 *   - SCA_ASISTENCIA_TAREO  : tareo diario donde se aplican los efectos
 *
 * TIPOORIGEN  (de donde sale el tiempo - dia trabajado):
 *   E = Horas Extras       (campo HORAEXTRA_AJUS)
 *   D = Horas Dobles Of.   (campo HORADOBLESOF)
 *   B = Banco de Horas dia (campo HORABANCOH)
 *
 * MODO MIXTO AUTOMATICO (transparente para el cliente):
 *   En CALCULAR_HORAS_EVENTO y REGISTRAR_EVENTO_MASIVO los tipos E/D/B
 *   funcionan como combinacion automatica de las TRES fuentes:
 *     Disponible = Dobles_dia + Banco_dia + SUM(HE) Lun-Dom de la semana.
 *   El parametro p_tipo_origen indica solo la FUENTE PREFERENTE (la que
 *   se consume primero); las restantes se usan para completar hasta cubrir
 *   la jornada destino.
 *     'D' -> orden Dobles -> Banco -> HE
 *     'B' -> orden Banco  -> Dobles -> HE
 *     'E' -> orden HE     -> Dobles -> Banco
 *   Cada fuente consumida genera UNA fila SCA_COMPENSACION con su
 *   tipoorigen real (D/B/E) y aux1='M'||id_evento para auditoria.
 *
 * TIPOCOMPENSACION  (a que se aplica - dia a cubrir):
 *   A = Antes de Salida    (HORAANTESALIDA)      validacion exacta
 *   T = Tardanza           (HORATARDANZA)        validacion exacta
 *   N = No Trabajadas      (HORAS_NO_TRABAJADAS) validacion exacta
 *   F = Faltas             (HORAS_FALTA)         validacion >=
 *   P = Permisos           (HORAPERMISO)         validacion >=
 *
 * ALERTAS POST-APLICACION:
 *   ALERTA02='FC' Falta Compensada
 *   ALERTA03='HC' Horas no trabajadas Compensadas
 *   ALERTA04='TC' Tardanza Compensada
 *   ALERTA06='EC' Extras/Banco consumidos
 *   ALERTA07='SC' Salida antes Compensada
 *   ALERTA08='DC' Dobles Compensadas
 *   ALERTA09='PC' Permiso Compensado
 *
 * FLUJO TIPICO DESDE .NET:
 *   1. CALCULAR_HORAS_EVENTO  -> preview por empleado (sin escribir)
 *   2. [usuario confirma]
 *   3. REGISTRAR_EVENTO_MASIVO -> registra + aplica por empleado
 *   4. [leer cursor - estado por empleado]
 *   5. COMMIT o ROLLBACK segun decision del usuario
 *   6. (opcional) VER_ESTADO / CONSULTAR_RANGO
 *
 * REGLA DE REDONDEO (SP_SCA_REDONDEAR_TAREO_HE):
 *   Despues de aplicar/revertir, los campos HE/DOBLES/BANCO se redondean
 *   a hora entera: minutos < 45 bajan, minutos >= 45 suben.
 *   Cuando el campo llega a cero se deja en 01/01/1900 00:00 (no NULL)
 *   como marca visual de que de ahi se desconto la hora (columna muestra 00:00).
 *
 * Autor:   Equipo AQUARIUS
 * Fecha:   27/04/2026
 *******************************************************************************/

-- =============================================================================
-- GLOBAL TEMPORARY TABLE para devolver resultados de REGISTRAR_EVENTO_MASIVO.
-- Filas viven en la sesion (preserve rows). Cada llamada usa id_evento unico.
-- =============================================================================
DECLARE
    v_existe NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_existe FROM USER_TABLES
    WHERE  table_name = 'SCA_TMP_COMPENS_MASIVO';
    IF v_existe = 0 THEN
        EXECUTE IMMEDIATE q'[
            CREATE GLOBAL TEMPORARY TABLE SCA_TMP_COMPENS_MASIVO (
                id_evento             NUMBER,
                cod_personal          VARCHAR2(20),
                nombre_completo       VARCHAR2(200),
                min_disponibles       NUMBER,
                horas_disponibles     VARCHAR2(10),
                min_jornada_destino   NUMBER,
                horas_jornada_destino VARCHAR2(10),
                min_a_compensar       NUMBER,
                horas_a_compensar     VARCHAR2(10),
                min_sobrante          NUMBER,
                horas_sobrante        VARCHAR2(10),
                id_compen             NUMBER,
                estado                VARCHAR2(20),
                motivo                VARCHAR2(500),
                saldo_banco_sem_min   NUMBER
            ) ON COMMIT PRESERVE ROWS
        ]';
    END IF;
END;
/

CREATE OR REPLACE PACKAGE PKG_SCA_COMP_DIA_DIA AS

    /***************************************************************************
        CALCULAR_HORAS_EVENTO
        Solo lectura. Para un evento masivo (mismo origen/destino/tipo para
        varios empleados) devuelve una fila por empleado con:
          - las horas disponibles en el tipo origen (E/D/B) en fecha_origen
          - la jornada del dia destino (tothoras)
          - las horas a compensar = LEAST(disponibles, jornada_destino)

        NO modifica nada.

        PARAMETROS:
        - p_cod_empresa      Empresa
        - p_fecha_origen     'dd/MM/yyyy' dia trabajado (descanso/feriado)
        - p_fecha_destino    'dd/MM/yyyy' dia a compensar (NULL si tipo I)
        - p_tipo_origen          'E'|'D'|'B' (fuente preferente; el SP combina
                                 automaticamente las tres fuentes del rango)
        - p_lista_personal       NULL = todos con horas ese dia
                                 'cod1,cod2,cod3' = solo esos empleados
        - p_fecha_horas_inicio   'dd/MM/yyyy' inicio rango HE/Dobles/Banco.
                                 NULL = semana Lun-Dom que contiene p_fecha_origen.
        - p_fecha_horas_fin      'dd/MM/yyyy' fin rango HE/Dobles/Banco.
                                 NULL = semana Lun-Dom que contiene p_fecha_origen.

        CURSOR resultado (una fila por empleado):
          cod_personal, nombre_completo,
          min_disponibles, horas_disponibles,
          min_jornada_destino, horas_jornada_destino,
          min_a_compensar, horas_a_compensar,
          min_sobrante, horas_sobrante
    ***************************************************************************/
    PROCEDURE CALCULAR_HORAS_EVENTO(
        p_cod_empresa          IN VARCHAR2,
        p_fecha_origen         IN VARCHAR2,
        p_fecha_destino        IN VARCHAR2 DEFAULT NULL,
        p_tipo_origen          IN CHAR,
        p_lista_personal       IN VARCHAR2 DEFAULT NULL,
        p_fecha_horas_inicio   IN VARCHAR2 DEFAULT NULL,
        p_fecha_horas_fin      IN VARCHAR2 DEFAULT NULL,
        cv_resultado           OUT SYS_REFCURSOR
    );

    /***************************************************************************
        REGISTRAR_EVENTO_MASIVO
        Para una lista de empleados que comparten origen/destino/tipo, hace
        AUTOMATICAMENTE en una sola llamada:
          1. Lee horas disponibles en fecha_origen (tipo E/D/B) por empleado.
          2. Calcula cap = jornada destino (tothoras) o p_horas_max si el
             destino no tiene tareo calculado aun (fecha futura).
          3. Si horas > 0:
               Registra en SCA_COMPENSACION.
               Aplica en tareo origen y tareo destino.
          4. Si horas = 0:
               Devuelve fila estado='SIN_HORAS' con saldo banco semanal
               disponible (para que .NET decida).

        PARAMETROS:
        - p_cod_empresa
        - p_fecha_origen      'dd/MM/yyyy' dia trabajado
        - p_fecha_destino     'dd/MM/yyyy' dia a compensar
        - p_tipo_origen       'E'|'D'|'B' (fuente preferente; el SP consume
                              automaticamente las tres fuentes en el orden
                              que dicta el tipo recibido).
        - p_tipo_compensacion 'A'|'T'|'N'|'F'|'P'
        - p_lista_personal    'cod1,cod2,cod3'  OBLIGATORIO
        - p_horas_max            'HH:MI' cap maximo opcional (para destinos sin
                                 tareo calculado). NULL = usa jornada del destino.
        - p_fecha_horas_inicio   'dd/MM/yyyy' inicio rango HE/Dobles/Banco.
                                 NULL = semana Lun-Dom que contiene p_fecha_origen.
        - p_fecha_horas_fin      'dd/MM/yyyy' fin rango HE/Dobles/Banco.
                                 NULL = semana Lun-Dom que contiene p_fecha_origen.

        CURSOR resultado (una fila por empleado):
          cod_personal, nombre_completo,
          min_disponibles, horas_disponibles,
          min_jornada_destino, horas_jornada_destino,
          min_a_compensar, horas_a_compensar,
          min_sobrante, horas_sobrante,
          id_compen,            -- NULL si no se registro
          estado,               -- 'OK' | 'SIN_HORAS' | 'ERR'
          motivo,               -- texto explicativo
          saldo_banco_sem_min,  -- saldo semanal de la semana del origen
          id_evento             -- ID de la llamada (para auditoria)
    ***************************************************************************/
    PROCEDURE REGISTRAR_EVENTO_MASIVO(
        p_cod_empresa          IN VARCHAR2,
        p_fecha_origen         IN VARCHAR2,
        p_fecha_destino        IN VARCHAR2,
        p_tipo_origen          IN CHAR,
        p_tipo_compensacion    IN CHAR,
        p_lista_personal       IN VARCHAR2,
        p_horas_max            IN VARCHAR2 DEFAULT NULL,
        p_fecha_horas_inicio   IN VARCHAR2 DEFAULT NULL,
        p_fecha_horas_fin      IN VARCHAR2 DEFAULT NULL,
        cv_resultado           OUT SYS_REFCURSOR
    );

    /***************************************************************************
        VER_ESTADO
        Detalle completo de una compensacion: datos de SCA_COMPENSACION,
        tareo origen, tareo destino, alertas y datos del empleado.
    ***************************************************************************/
    PROCEDURE VER_ESTADO(
        p_id_compen       IN NUMBER,
        cv_resultado      OUT SYS_REFCURSOR
    );

    /***************************************************************************
        LISTAR_EMPLEADOS_RANGO
        Solo lectura. Devuelve una fila por empleado+fecha para cada dia
        dentro del rango [p_fecha_inicio, p_fecha_fin] en que el empleado
        tiene horas de origen disponibles (HE/dobles/banco).

        Uso tipico: poblar el selector de personal antes de abrir la
        pantalla de Compensacion Dia por Dia.

        PARAMETROS:
        - p_cod_empresa          Empresa
        - p_fecha_inicio         'dd/MM/yyyy' inicio del rango de busqueda de empleados
        - p_fecha_fin            'dd/MM/yyyy' fin del rango de busqueda de empleados
        - p_nombre               NULL = sin filtro por nombre
                                 texto parcial (case-insensitive) sobre
                                 APE_PATERNO||APE_MATERNO||NOM_TRABAJADOR
        - p_fecha_horas_inicio   'dd/MM/yyyy' inicio del rango para sumar HE/Dobles/Banco.
                                 NULL = usa p_fecha_inicio.
        - p_fecha_horas_fin      'dd/MM/yyyy' fin del rango para sumar HE/Dobles/Banco.
                                 NULL = usa p_fecha_fin.

        CURSOR resultado (una fila por empleado+dia con tareo):
          cod_personal, nombre_completo, fechamar, fechamar_str,
          min_trabajadas, horas_trabajadas  (horaefectiva del dia),
          min_he,    horas_he      (SUM(horaextra_ajus) en rango de horas),
          min_dobles, horas_dobles  (SUM(horadoblesof)   en rango de horas),
          min_banco,  horas_banco   (SUM(horabancoh)     en rango de horas),
          min_total,  horas_total   (HE + Dobles + Banco del rango de horas)
    ***************************************************************************/
    PROCEDURE LISTAR_EMPLEADOS_RANGO(
        p_cod_empresa          IN VARCHAR2,
        p_fecha_inicio         IN VARCHAR2,
        p_fecha_fin            IN VARCHAR2,
        p_nombre               IN VARCHAR2 DEFAULT NULL,
        p_fecha_horas_inicio   IN VARCHAR2 DEFAULT NULL,
        p_fecha_horas_fin      IN VARCHAR2 DEFAULT NULL,
        cv_resultado           OUT SYS_REFCURSOR
    );

    /***************************************************************************
        CONSULTAR_RANGO
        Lista compensaciones de un empleado/rango con estado de aplicacion.
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
        CONSULTAR_EVENTO
        Devuelve SOLO las filas de SCA_COMPENSACION generadas por un evento
        especifico de REGISTRAR_EVENTO_MASIVO (filtrado por aux1='M'||p_id_evento).
        Usar para el modal post-commit en lugar de CONSULTAR_RANGO.
    ***************************************************************************/
    PROCEDURE CONSULTAR_EVENTO(
        p_id_evento       IN NUMBER,
        cv_resultado      OUT SYS_REFCURSOR
    );

END PKG_SCA_COMP_DIA_DIA;
/


/*******************************************************************************
 *                              PACKAGE BODY
 *******************************************************************************/
CREATE OR REPLACE PACKAGE BODY PKG_SCA_COMP_DIA_DIA AS

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
        IF p_horas IS NULL OR INSTR(p_horas,':') = 0 THEN RETURN 0; END IF;
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

    -- Lee tiempo "destino" disponible en tareo segun tipocompensacion
    FUNCTION fn_tiempo_destino(
        p_emp IN VARCHAR2, p_per IN VARCHAR2, p_fec IN DATE, p_tipo IN CHAR
    ) RETURN NUMBER IS
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
            WHERE  cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec;
        EXCEPTION WHEN NO_DATA_FOUND THEN v_dt := NULL;
        END;
        RETURN fn_date_a_min(v_dt);
    END fn_tiempo_destino;

    -- Lee tiempo "origen" disponible en tareo segun tipoorigen
    FUNCTION fn_tiempo_origen(
        p_emp IN VARCHAR2, p_per IN VARCHAR2, p_fec IN DATE, p_tipo IN CHAR
    ) RETURN NUMBER IS
        v_dt DATE;
    BEGIN
        BEGIN
            SELECT CASE p_tipo
                     WHEN 'E' THEN horaextra_ajus
                     WHEN 'D' THEN horadoblesof
                     WHEN 'B' THEN horabancoh
                   END
            INTO   v_dt
            FROM   SCA_ASISTENCIA_TAREO
            WHERE  cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec;
        EXCEPTION WHEN NO_DATA_FOUND THEN v_dt := NULL;
        END;
        RETURN fn_date_a_min(v_dt);
    END fn_tiempo_origen;

    -- =========================================================================
    -- INTERNO: aplica efecto en TAREO DESTINO (equivale a InsComDes)
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
        WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec;
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
        -- Descuenta tothoramarcas siempre
        UPDATE SCA_ASISTENCIA_TAREO
        SET tothoramarcas = tothoramarcas - v_min/1440
        WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec;

        IF p_tipo_ori = 'E' THEN
            UPDATE SCA_ASISTENCIA_TAREO
            SET horaextra_ajus = horaextra_ajus - v_min/1440
            WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec;

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

            UPDATE SCA_ASISTENCIA_TAREO
            SET horaexofi3 = TO_DATE('01/01/1900 ' ||
                  TO_CHAR(TRUNC(MOD((horaextra_ajus - NVL(h35f, c_BASE_DATE))*24, 24))) || ':' ||
                  TO_CHAR(TRUNC(MOD((horaextra_ajus - NVL(h35f, c_BASE_DATE))*24*60, 60))),
                  'dd/MM/yyyy HH24:MI')
            WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec
            AND   horaextra_ajus > hni;

            UPDATE SCA_ASISTENCIA_TAREO
            SET alerta06 = CASE WHEN horaextra_ajus = c_BASE_DATE THEN 'EC'
                                ELSE alerta06 END
            WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec;

        ELSIF p_tipo_ori = 'D' THEN
            UPDATE SCA_ASISTENCIA_TAREO
            SET horadoblesof = horadoblesof - v_min/1440
            WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec;

            UPDATE SCA_ASISTENCIA_TAREO
            SET alerta08 = CASE WHEN horadoblesof = c_BASE_DATE THEN 'DC'
                                ELSE alerta08 END
            WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec;

        ELSIF p_tipo_ori = 'B' THEN
            UPDATE SCA_ASISTENCIA_TAREO
            SET horabancoh = horabancoh - v_min/1440
            WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec;

            UPDATE SCA_ASISTENCIA_TAREO
            SET alerta06 = CASE WHEN horabancoh = c_BASE_DATE THEN 'EC'
                                ELSE alerta06 END
            WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec;
        END IF;

        -- Redondeo hora-entera (regla negocio: <45 baja, >=45 sube)
        SP_SCA_REDONDEAR_TAREO_HE(p_emp, p_per, p_fec);

        -- Post-redondeo: si el SP bajo HE a exactamente c_BASE_DATE (ej. 00:25 → 00:00),
        -- el CASE anterior no lo detecto porque corrio antes del redondeo.
        -- Dejar c_BASE_DATE como marca (00:00) y solo actualizar alerta EC.
        IF p_tipo_ori = 'E' THEN
            UPDATE SCA_ASISTENCIA_TAREO
            SET    alerta06 = 'EC'
            WHERE  cod_empresa  = p_emp
            AND    cod_personal = p_per
            AND    fechamar     = p_fec
            AND    horaextra_ajus = c_BASE_DATE;
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
    -- PRIVADO: REGISTRAR (solo llamado desde REGISTRAR_EVENTO_MASIVO)
    -- =========================================================================
    PROCEDURE REGISTRAR(
        p_cod_empresa       IN VARCHAR2,
        p_cod_personal      IN VARCHAR2,
        p_fecha_destino     IN VARCHAR2,
        p_fecha_origen      IN VARCHAR2,
        p_tipo_origen       IN CHAR,
        p_tipo_compensacion IN CHAR,
        p_horas             IN VARCHAR2,
        p_validar           IN VARCHAR2 DEFAULT 'N',
        cv_resultado        OUT SYS_REFCURSOR
    ) AS
        v_min        NUMBER := fn_hhmi_a_min(p_horas);
        v_id         NUMBER;
        v_estado     VARCHAR2(10) := 'OK';
        v_motivo     VARCHAR2(500) := 'Registrada correctamente';
        v_fec_des    DATE := TO_DATE(p_fecha_destino, 'dd/MM/yyyy');
        v_fec_ori    DATE := TO_DATE(p_fecha_origen,  'dd/MM/yyyy');
        v_disp_ori   NUMBER;
        v_def_des    NUMBER;
    BEGIN
        IF NVL(p_validar,'N') = 'S' THEN
            v_disp_ori := fn_tiempo_origen(p_cod_empresa, p_cod_personal, v_fec_ori, p_tipo_origen);
            IF NVL(v_disp_ori,0) < v_min THEN
                v_estado := 'ERR';
                v_motivo := 'Tiempo insuficiente en origen ('||p_tipo_origen||
                            ') disp='||NVL(v_disp_ori,0)||' min, sol='||v_min||' min';
            END IF;
            IF v_estado = 'OK' THEN
                v_def_des := fn_tiempo_destino(p_cod_empresa, p_cod_personal, v_fec_des, p_tipo_compensacion);
                IF p_tipo_compensacion IN ('A','T','N') AND v_def_des <> v_min THEN
                    v_estado := 'ERR';
                    v_motivo := 'Tiempo destino ('||p_tipo_compensacion||')='||v_def_des||
                                ' min, requerido exacto '||v_min||' min';
                ELSIF p_tipo_compensacion IN ('F','P') AND v_def_des < v_min THEN
                    v_estado := 'ERR';
                    v_motivo := 'Tiempo destino ('||p_tipo_compensacion||')='||v_def_des||
                                ' min, insuficiente para '||v_min||' min';
                END IF;
            END IF;
        END IF;

        IF v_estado = 'ERR' THEN
            OPEN cv_resultado FOR
                SELECT NULL AS id_compen, v_estado AS estado, v_motivo AS motivo,
                       v_min AS tiempo_minutos FROM DUAL;
            RETURN;
        END IF;

        INSERT INTO SCA_COMPENSACION (
            id_compen, cod_empresa, cod_personal,
            fechadestino, fechaorigen,
            tipoorigen, tipocompensacion, tiempo, aux1
        ) VALUES (
            id_comp_seq.NEXTVAL, p_cod_empresa, p_cod_personal,
            v_fec_des, v_fec_ori,
            p_tipo_origen, p_tipo_compensacion, v_min, NULL
        ) RETURNING id_compen INTO v_id;

        OPEN cv_resultado FOR
            SELECT v_id AS id_compen, v_estado AS estado, v_motivo AS motivo,
                   v_min AS tiempo_minutos FROM DUAL;
    EXCEPTION
        WHEN OTHERS THEN
            v_motivo := 'EXCEPCION: ' || SQLERRM;
            OPEN cv_resultado FOR
                SELECT NULL AS id_compen, 'ERR' AS estado,
                       v_motivo AS motivo,
                       v_min AS tiempo_minutos FROM DUAL;
    END REGISTRAR;

    -- =========================================================================
    -- PRIVADO: APLICAR_DIA (solo llamado desde REGISTRAR_EVENTO_MASIVO)
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
            WHERE  c.cod_empresa      = p_cod_empresa
            AND    c.cod_personal     = p_cod_personal
            AND    c.tipocompensacion <> 'I'
            AND    c.fechadestino     = v_fec;

        CURSOR c_ori IS
            SELECT c.id_compen, c.cod_empresa, c.cod_personal,
                   c.fechadestino, c.fechaorigen,
                   c.tipoorigen, c.tipocompensacion, c.tiempo
            FROM   SCA_COMPENSACION c
            WHERE  c.cod_empresa  = p_cod_empresa
            AND    c.cod_personal = p_cod_personal
            AND    c.fechaorigen  = v_fec;
    BEGIN
        -- CURSOR 1: dia DESTINO
        FOR r IN c_des LOOP
            BEGIN
                v_disp       := fn_tiempo_destino(r.cod_empresa, r.cod_personal,
                                                   r.fechadestino, r.tipocompensacion);
                v_horas_hhmi := fn_min_a_hhmi(r.tiempo);
                IF (r.tipocompensacion IN ('A','T','N') AND v_disp = r.tiempo)
                OR (r.tipocompensacion IN ('F','P')     AND v_disp >= r.tiempo) THEN
                    prv_aplicar_destino(r.cod_empresa, r.cod_personal, r.fechadestino,
                                        r.tipocompensacion, v_horas_hhmi);
                    v_apl_des := v_apl_des + 1;
                ELSE
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

        -- CURSOR 2: dia ORIGEN
        FOR r IN c_ori LOOP
            BEGIN
                v_horas_hhmi := fn_min_a_hhmi(r.tiempo);
                v_disp := fn_tiempo_origen(r.cod_empresa, r.cod_personal,
                                            r.fechaorigen, r.tipoorigen);
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
            EXCEPTION WHEN OTHERS THEN v_errores := v_errores + 1;
            END;
        END LOOP;

        OPEN cv_resultado FOR
            SELECT p_fecha AS fecha, p_cod_empresa AS cod_empresa,
                   p_cod_personal AS cod_personal,
                   v_apl_des AS aplicadas_destino, v_apl_ori AS aplicadas_origen,
                   v_eliminadas AS eliminadas, v_errores AS errores
            FROM   DUAL;
    END APLICAR_DIA;


    -- =========================================================================
    -- CALCULAR_HORAS_EVENTO  (solo lectura, sin modificar BD)
    -- =========================================================================
    PROCEDURE CALCULAR_HORAS_EVENTO(
        p_cod_empresa          IN VARCHAR2,
        p_fecha_origen         IN VARCHAR2,
        p_fecha_destino        IN VARCHAR2 DEFAULT NULL,
        p_tipo_origen          IN CHAR,
        p_lista_personal       IN VARCHAR2 DEFAULT NULL,
        p_fecha_horas_inicio   IN VARCHAR2 DEFAULT NULL,
        p_fecha_horas_fin      IN VARCHAR2 DEFAULT NULL,
        cv_resultado           OUT SYS_REFCURSOR
    ) AS
        c_BASE        CONSTANT DATE := TO_DATE('01/01/1900','dd/MM/yyyy');
        v_fec_ori     DATE := TO_DATE(p_fecha_origen, 'dd/MM/yyyy');
        v_fec_des     DATE := CASE WHEN p_fecha_destino IS NOT NULL
                                   THEN TO_DATE(p_fecha_destino,'dd/MM/yyyy')
                                   ELSE NULL END;
        -- Rango para sumar HE/Dobles/Banco. Por defecto: semana Lun-Dom de fecha_origen.
        v_fec_hor_ini DATE := NVL(TO_DATE(p_fecha_horas_inicio, 'dd/MM/yyyy'),
                                  TRUNC(TO_DATE(p_fecha_origen,'dd/MM/yyyy'), 'IW'));
        v_fec_hor_fin DATE := NVL(TO_DATE(p_fecha_horas_fin,    'dd/MM/yyyy'),
                                  TRUNC(TO_DATE(p_fecha_origen,'dd/MM/yyyy'), 'IW') + 6);
    BEGIN
        -- Siempre se calcula disponibles como Dobles_dia + Banco_dia + HE_semana
        -- (modo mixto automatico). El parametro p_tipo_origen solo dicta el orden
        -- de consumo en REGISTRAR_EVENTO_MASIVO; aqui no afecta el total.
        OPEN cv_resultado FOR
            WITH base AS (
                SELECT
                    p.cod_empresa,
                    p.cod_personal,
                    p.ape_paterno || ' ' || p.ape_materno || ' ' || p.nom_trabajador AS nombre_completo,
                    -- minutos disponibles = SUM(HE + Dobles + Banco) en el rango de horas
                    -- Mismo criterio que LISTAR_EMPLEADOS_RANGO para consistencia.
                      NVL((SELECT SUM(ROUND((NVL(s.horaextra_ajus, c_BASE) - c_BASE) * 1440))
                           FROM   SCA_ASISTENCIA_TAREO s
                           WHERE  s.cod_empresa  = p.cod_empresa
                           AND    s.cod_personal = p.cod_personal
                           AND    s.fechamar BETWEEN v_fec_hor_ini AND v_fec_hor_fin
                           AND    MOD(ROUND((NVL(s.horaextra_ajus, c_BASE) - c_BASE) * 1440), 30) = 0), 0)
                    + NVL((SELECT SUM(ROUND((NVL(s.horadoblesof, c_BASE) - c_BASE) * 1440))
                           FROM   SCA_ASISTENCIA_TAREO s
                           WHERE  s.cod_empresa  = p.cod_empresa
                           AND    s.cod_personal = p.cod_personal
                           AND    s.fechamar BETWEEN v_fec_hor_ini AND v_fec_hor_fin
                           AND    MOD(ROUND((NVL(s.horadoblesof, c_BASE) - c_BASE) * 1440), 30) = 0), 0)
                    + NVL((SELECT SUM(ROUND((NVL(s.horabancoh, c_BASE) - c_BASE) * 1440))
                           FROM   SCA_ASISTENCIA_TAREO s
                           WHERE  s.cod_empresa  = p.cod_empresa
                           AND    s.cod_personal = p.cod_personal
                           AND    s.fechamar BETWEEN v_fec_hor_ini AND v_fec_hor_fin
                           AND    MOD(ROUND((NVL(s.horabancoh, c_BASE) - c_BASE) * 1440), 30) = 0), 0)
                                                                          AS min_disp,
                    -- jornada destino
                    NVL((SELECT ROUND((NVL(td.tothoras, c_BASE) - c_BASE) * 1440)
                         FROM   SCA_ASISTENCIA_TAREO td
                         WHERE  td.cod_empresa  = p.cod_empresa
                         AND    td.cod_personal = p.cod_personal
                         AND    td.fechamar     = v_fec_des), 0) AS min_jorn
                FROM   PLA_PERSONAL p
                WHERE  p.cod_empresa = p_cod_empresa
                AND    (p_lista_personal IS NULL
                        OR INSTR(','||p_lista_personal||',', ','||p.cod_personal||',') > 0)
            ),
            calc AS (
                SELECT b.*,
                       CASE
                           -- Sin fecha destino (ej. tipo banco): usar todo lo disponible
                           WHEN v_fec_des IS NULL
                                THEN b.min_disp
                           -- Fecha destino con tareo calculado: limitar a la jornada
                           WHEN b.min_jorn > 0
                                THEN LEAST(b.min_disp, b.min_jorn)
                           -- Fecha destino indicada pero sin tareo en esa fecha:
                           -- REGISTRAR_EVENTO_MASIVO fallaria (ERR) porque no hay
                           -- horas_falta/tardanza/etc. que compensar. El preview
                           -- muestra 00:00 para advertir al usuario que el destino
                           -- aun no tiene tareo calculado.
                           ELSE 0
                       END AS min_usar
                FROM   base b
                WHERE  b.min_disp > 0
            )
            SELECT
                cod_personal,
                nombre_completo,
                min_disp                                                AS min_disponibles,
                LPAD(TRUNC(min_disp/60), 2, '0') || ':' ||
                LPAD(MOD(min_disp, 60), 2, '0')                         AS horas_disponibles,
                min_jorn                                                AS min_jornada_destino,
                LPAD(TRUNC(min_jorn/60), 2, '0') || ':' ||
                LPAD(MOD(min_jorn, 60), 2, '0')                         AS horas_jornada_destino,
                min_usar                                                AS min_a_compensar,
                LPAD(TRUNC(min_usar/60), 2, '0') || ':' ||
                LPAD(MOD(min_usar, 60), 2, '0')                         AS horas_a_compensar,
                GREATEST(0, min_disp - min_usar)                        AS min_sobrante,
                LPAD(TRUNC(GREATEST(0, min_disp - min_usar)/60), 2, '0') || ':' ||
                LPAD(MOD(GREATEST(0, min_disp - min_usar), 60), 2, '0') AS horas_sobrante
            FROM   calc
            ORDER  BY cod_personal;
    END CALCULAR_HORAS_EVENTO;


    -- =========================================================================
    -- LISTAR_EMPLEADOS_RANGO  (solo lectura)
    -- =========================================================================
    PROCEDURE LISTAR_EMPLEADOS_RANGO(
        p_cod_empresa          IN VARCHAR2,
        p_fecha_inicio         IN VARCHAR2,
        p_fecha_fin            IN VARCHAR2,
        p_nombre               IN VARCHAR2 DEFAULT NULL,
        p_fecha_horas_inicio   IN VARCHAR2 DEFAULT NULL,
        p_fecha_horas_fin      IN VARCHAR2 DEFAULT NULL,
        cv_resultado           OUT SYS_REFCURSOR
    ) AS
        c_BASE        CONSTANT DATE := TO_DATE('01/01/1900','dd/MM/yyyy');
        v_fec_ini     DATE := TO_DATE(p_fecha_inicio, 'dd/MM/yyyy');
        v_fec_fin     DATE := TO_DATE(p_fecha_fin,    'dd/MM/yyyy');
        -- Rango independiente para sumar HE/Dobles/Banco.
        -- Por defecto usa el mismo rango de busqueda de empleados.
        v_fec_hor_ini DATE := NVL(TO_DATE(p_fecha_horas_inicio, 'dd/MM/yyyy'), TO_DATE(p_fecha_inicio, 'dd/MM/yyyy'));
        v_fec_hor_fin DATE := NVL(TO_DATE(p_fecha_horas_fin,    'dd/MM/yyyy'), TO_DATE(p_fecha_fin,    'dd/MM/yyyy'));
        v_nombre      VARCHAR2(200) := CASE WHEN p_nombre IS NOT NULL
                                            THEN '%' || UPPER(p_nombre) || '%'
                                            ELSE NULL END;
    BEGIN
        OPEN cv_resultado FOR
            SELECT
                t.cod_personal,
                p.ape_paterno || ' ' || p.ape_materno || ' ' || p.nom_trabajador AS nombre_completo,
                t.fechamar,
                -- Horas trabajadas efectivas del dia origen (horaefectiva = H.Efe.)
                -- NO usar tothoramarcas: incluye HE+Dobles y da un total mayor al de jornada
                ROUND((NVL(t.horaefectiva, c_BASE) - c_BASE) * 1440)         AS min_trabajadas,
                LPAD(TRUNC(ROUND((NVL(t.horaefectiva, c_BASE) - c_BASE) * 1440) / 60), 2, '0')
                  || ':' ||
                LPAD(MOD(ROUND((NVL(t.horaefectiva, c_BASE) - c_BASE) * 1440), 60), 2, '0')  AS horas_trabajadas,
                -- HE: suma en el rango de horas (v_fec_hor_ini..v_fec_hor_fin)
                NVL((SELECT SUM(ROUND((NVL(s.horaextra_ajus, c_BASE) - c_BASE) * 1440))
                     FROM   SCA_ASISTENCIA_TAREO s
                     WHERE  s.cod_empresa  = t.cod_empresa
                     AND    s.cod_personal = t.cod_personal
                     AND    s.fechamar BETWEEN v_fec_hor_ini AND v_fec_hor_fin), 0) AS min_he,
                LPAD(TRUNC(NVL((SELECT SUM(ROUND((NVL(s.horaextra_ajus, c_BASE) - c_BASE) * 1440))
                     FROM   SCA_ASISTENCIA_TAREO s
                     WHERE  s.cod_empresa  = t.cod_empresa
                     AND    s.cod_personal = t.cod_personal
                     AND    s.fechamar BETWEEN v_fec_hor_ini AND v_fec_hor_fin), 0) / 60), 2, '0')
                  || ':' ||
                LPAD(MOD(NVL((SELECT SUM(ROUND((NVL(s.horaextra_ajus, c_BASE) - c_BASE) * 1440))
                     FROM   SCA_ASISTENCIA_TAREO s
                     WHERE  s.cod_empresa  = t.cod_empresa
                     AND    s.cod_personal = t.cod_personal
                     AND    s.fechamar BETWEEN v_fec_hor_ini AND v_fec_hor_fin), 0), 60), 2, '0') AS horas_he,
                -- Dobles: suma en el rango de horas (v_fec_hor_ini..v_fec_hor_fin)
                NVL((SELECT SUM(ROUND((NVL(s.horadoblesof, c_BASE) - c_BASE) * 1440))
                     FROM   SCA_ASISTENCIA_TAREO s
                     WHERE  s.cod_empresa  = t.cod_empresa
                     AND    s.cod_personal = t.cod_personal
                     AND    s.fechamar BETWEEN v_fec_hor_ini AND v_fec_hor_fin), 0) AS min_dobles,
                LPAD(TRUNC(NVL((SELECT SUM(ROUND((NVL(s.horadoblesof, c_BASE) - c_BASE) * 1440))
                     FROM   SCA_ASISTENCIA_TAREO s
                     WHERE  s.cod_empresa  = t.cod_empresa
                     AND    s.cod_personal = t.cod_personal
                     AND    s.fechamar BETWEEN v_fec_hor_ini AND v_fec_hor_fin), 0) / 60), 2, '0')
                  || ':' ||
                LPAD(MOD(NVL((SELECT SUM(ROUND((NVL(s.horadoblesof, c_BASE) - c_BASE) * 1440))
                     FROM   SCA_ASISTENCIA_TAREO s
                     WHERE  s.cod_empresa  = t.cod_empresa
                     AND    s.cod_personal = t.cod_personal
                     AND    s.fechamar BETWEEN v_fec_hor_ini AND v_fec_hor_fin), 0), 60), 2, '0') AS horas_dobles,
                -- Banco: suma en el rango de horas (v_fec_hor_ini..v_fec_hor_fin)
                NVL((SELECT SUM(ROUND((NVL(s.horabancoh, c_BASE) - c_BASE) * 1440))
                     FROM   SCA_ASISTENCIA_TAREO s
                     WHERE  s.cod_empresa  = t.cod_empresa
                     AND    s.cod_personal = t.cod_personal
                     AND    s.fechamar BETWEEN v_fec_hor_ini AND v_fec_hor_fin), 0) AS min_banco,
                LPAD(TRUNC(NVL((SELECT SUM(ROUND((NVL(s.horabancoh, c_BASE) - c_BASE) * 1440))
                     FROM   SCA_ASISTENCIA_TAREO s
                     WHERE  s.cod_empresa  = t.cod_empresa
                     AND    s.cod_personal = t.cod_personal
                     AND    s.fechamar BETWEEN v_fec_hor_ini AND v_fec_hor_fin), 0) / 60), 2, '0')
                  || ':' ||
                LPAD(MOD(NVL((SELECT SUM(ROUND((NVL(s.horabancoh, c_BASE) - c_BASE) * 1440))
                     FROM   SCA_ASISTENCIA_TAREO s
                     WHERE  s.cod_empresa  = t.cod_empresa
                     AND    s.cod_personal = t.cod_personal
                     AND    s.fechamar BETWEEN v_fec_hor_ini AND v_fec_hor_fin), 0), 60), 2, '0') AS horas_banco,
                -- Total = HE + Dobles + Banco en el rango de horas
                NVL((SELECT SUM(ROUND((NVL(s.horaextra_ajus, c_BASE) - c_BASE) * 1440))
                     FROM   SCA_ASISTENCIA_TAREO s
                     WHERE  s.cod_empresa  = t.cod_empresa
                     AND    s.cod_personal = t.cod_personal
                     AND    s.fechamar BETWEEN v_fec_hor_ini AND v_fec_hor_fin), 0)
                  + NVL((SELECT SUM(ROUND((NVL(s.horadoblesof, c_BASE) - c_BASE) * 1440))
                     FROM   SCA_ASISTENCIA_TAREO s
                     WHERE  s.cod_empresa  = t.cod_empresa
                     AND    s.cod_personal = t.cod_personal
                     AND    s.fechamar BETWEEN v_fec_hor_ini AND v_fec_hor_fin), 0)
                  + NVL((SELECT SUM(ROUND((NVL(s.horabancoh, c_BASE) - c_BASE) * 1440))
                     FROM   SCA_ASISTENCIA_TAREO s
                     WHERE  s.cod_empresa  = t.cod_empresa
                     AND    s.cod_personal = t.cod_personal
                     AND    s.fechamar BETWEEN v_fec_hor_ini AND v_fec_hor_fin), 0) AS min_total,
                LPAD(TRUNC((
                    NVL((SELECT SUM(ROUND((NVL(s.horaextra_ajus, c_BASE) - c_BASE) * 1440))
                         FROM   SCA_ASISTENCIA_TAREO s
                         WHERE  s.cod_empresa  = t.cod_empresa
                         AND    s.cod_personal = t.cod_personal
                         AND    s.fechamar BETWEEN v_fec_hor_ini AND v_fec_hor_fin), 0)
                    + NVL((SELECT SUM(ROUND((NVL(s.horadoblesof, c_BASE) - c_BASE) * 1440))
                         FROM   SCA_ASISTENCIA_TAREO s
                         WHERE  s.cod_empresa  = t.cod_empresa
                         AND    s.cod_personal = t.cod_personal
                         AND    s.fechamar BETWEEN v_fec_hor_ini AND v_fec_hor_fin), 0)
                    + NVL((SELECT SUM(ROUND((NVL(s.horabancoh, c_BASE) - c_BASE) * 1440))
                         FROM   SCA_ASISTENCIA_TAREO s
                         WHERE  s.cod_empresa  = t.cod_empresa
                         AND    s.cod_personal = t.cod_personal
                         AND    s.fechamar BETWEEN v_fec_hor_ini AND v_fec_hor_fin), 0)
                  ) / 60), 2, '0')
                  || ':' ||
                LPAD(MOD(
                    NVL((SELECT SUM(ROUND((NVL(s.horaextra_ajus, c_BASE) - c_BASE) * 1440))
                         FROM   SCA_ASISTENCIA_TAREO s
                         WHERE  s.cod_empresa  = t.cod_empresa
                         AND    s.cod_personal = t.cod_personal
                         AND    s.fechamar BETWEEN v_fec_hor_ini AND v_fec_hor_fin), 0)
                    + NVL((SELECT SUM(ROUND((NVL(s.horadoblesof, c_BASE) - c_BASE) * 1440))
                         FROM   SCA_ASISTENCIA_TAREO s
                         WHERE  s.cod_empresa  = t.cod_empresa
                         AND    s.cod_personal = t.cod_personal
                         AND    s.fechamar BETWEEN v_fec_hor_ini AND v_fec_hor_fin), 0)
                    + NVL((SELECT SUM(ROUND((NVL(s.horabancoh, c_BASE) - c_BASE) * 1440))
                         FROM   SCA_ASISTENCIA_TAREO s
                         WHERE  s.cod_empresa  = t.cod_empresa
                         AND    s.cod_personal = t.cod_personal
                         AND    s.fechamar BETWEEN v_fec_hor_ini AND v_fec_hor_fin), 0)
                  , 60), 2, '0')                                              AS horas_total,
                TO_CHAR(t.fechamar, 'DD/MM/YYYY')                            AS fechamar_str
            FROM  SCA_ASISTENCIA_TAREO t
            JOIN  PLA_PERSONAL p
                  ON  p.cod_empresa  = t.cod_empresa
                  AND p.cod_personal = t.cod_personal
            WHERE t.cod_empresa = p_cod_empresa
            AND   t.fechamar BETWEEN v_fec_ini AND v_fec_fin
            AND   (
                    -- Al menos una fuente con horas en el rango de horas
                    NVL((SELECT SUM(ROUND((NVL(s.horaextra_ajus, c_BASE) - c_BASE) * 1440))
                         FROM   SCA_ASISTENCIA_TAREO s
                         WHERE  s.cod_empresa  = t.cod_empresa
                         AND    s.cod_personal = t.cod_personal
                         AND    s.fechamar BETWEEN v_fec_hor_ini AND v_fec_hor_fin), 0) > 0
                    OR NVL((SELECT SUM(ROUND((NVL(s.horadoblesof, c_BASE) - c_BASE) * 1440))
                         FROM   SCA_ASISTENCIA_TAREO s
                         WHERE  s.cod_empresa  = t.cod_empresa
                         AND    s.cod_personal = t.cod_personal
                         AND    s.fechamar BETWEEN v_fec_hor_ini AND v_fec_hor_fin), 0) > 0
                    OR NVL((SELECT SUM(ROUND((NVL(s.horabancoh, c_BASE) - c_BASE) * 1440))
                         FROM   SCA_ASISTENCIA_TAREO s
                         WHERE  s.cod_empresa  = t.cod_empresa
                         AND    s.cod_personal = t.cod_personal
                         AND    s.fechamar BETWEEN v_fec_hor_ini AND v_fec_hor_fin), 0) > 0
                  )
            AND   (v_nombre IS NULL
                   OR UPPER(p.ape_paterno || ' ' || p.ape_materno
                            || ' ' || p.nom_trabajador) LIKE v_nombre)
            ORDER BY t.cod_personal, t.fechamar;
    END LISTAR_EMPLEADOS_RANGO;


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
    -- REGISTRAR_EVENTO_MASIVO  (orquestador: 1 llamada -> N empleados)
    -- =========================================================================
    PROCEDURE REGISTRAR_EVENTO_MASIVO(
        p_cod_empresa          IN VARCHAR2,
        p_fecha_origen         IN VARCHAR2,
        p_fecha_destino        IN VARCHAR2,
        p_tipo_origen          IN CHAR,
        p_tipo_compensacion    IN CHAR,
        p_lista_personal       IN VARCHAR2,
        p_horas_max            IN VARCHAR2 DEFAULT NULL,
        p_fecha_horas_inicio   IN VARCHAR2 DEFAULT NULL,
        p_fecha_horas_fin      IN VARCHAR2 DEFAULT NULL,
        cv_resultado           OUT SYS_REFCURSOR
    ) AS
        v_fec_ori     DATE := TO_DATE(p_fecha_origen,  'dd/MM/yyyy');
        v_fec_des     DATE := TO_DATE(p_fecha_destino, 'dd/MM/yyyy');
        -- Rango para sumar HE/Dobles/Banco. Por defecto: semana Lun-Dom de fecha_origen.
        v_fec_hor_ini DATE := NVL(TO_DATE(p_fecha_horas_inicio, 'dd/MM/yyyy'),
                                  TRUNC(TO_DATE(p_fecha_origen,'dd/MM/yyyy'), 'IW'));
        v_fec_hor_fin DATE := NVL(TO_DATE(p_fecha_horas_fin,    'dd/MM/yyyy'),
                                  TRUNC(TO_DATE(p_fecha_origen,'dd/MM/yyyy'), 'IW') + 6);
        v_cap_max   NUMBER := CASE WHEN p_horas_max IS NULL THEN NULL
                                   ELSE fn_hhmi_a_min(p_horas_max) END;
        v_min_disp  NUMBER;
        v_min_jorn  NUMBER;
        v_min_usar  NUMBER;
        v_min_sobr  NUMBER;
        v_estado    VARCHAR2(20);
        v_motivo    VARCHAR2(500);
        v_horas_str VARCHAR2(10);
        v_saldo_sem NUMBER;
        v_dummy     SYS_REFCURSOR;
        v_buf_id    NUMBER;
        v_buf_est   VARCHAR2(20);
        v_buf_mot   VARCHAR2(500);
        v_buf_min   NUMBER;
        v_id_evento NUMBER := id_comp_seq.NEXTVAL;
        v_h_disp    VARCHAR2(10);
        v_h_jorn    VARCHAR2(10);
        v_h_usar    VARCHAR2(10);
        v_h_sobr    VARCHAR2(10);
    BEGIN
        IF p_lista_personal IS NULL THEN
            OPEN cv_resultado FOR
                SELECT NULL AS cod_personal, NULL AS nombre_completo,
                       0 AS min_disponibles, '00:00' AS horas_disponibles,
                       0 AS min_jornada_destino, '00:00' AS horas_jornada_destino,
                       0 AS min_a_compensar, '00:00' AS horas_a_compensar,
                       0 AS min_sobrante, '00:00' AS horas_sobrante,
                       NULL AS id_compen,
                       'ERR' AS estado,
                       'p_lista_personal es OBLIGATORIO' AS motivo,
                       0 AS saldo_banco_sem_min,
                       v_id_evento AS id_evento
                FROM   DUAL;
            RETURN;
        END IF;

        DELETE FROM SCA_TMP_COMPENS_MASIVO WHERE id_evento = v_id_evento;

        FOR e IN (
            SELECT p.cod_personal,
                   p.ape_paterno||' '||p.ape_materno||' '||p.nom_trabajador AS nombre_completo,
                   -- Disponibles = SUM(HE + Dobles + Banco) en el rango de horas
                   -- (mismo criterio que LISTAR y CALCULAR para consistencia)
                   NVL((SELECT SUM(ROUND((NVL(s.horaextra_ajus, c_BASE_DATE) - c_BASE_DATE) * 1440))
                          FROM   SCA_ASISTENCIA_TAREO s
                          WHERE  s.cod_empresa  = p.cod_empresa
                          AND    s.cod_personal = p.cod_personal
                          AND    s.fechamar BETWEEN v_fec_hor_ini AND v_fec_hor_fin
                          AND    MOD(ROUND((NVL(s.horaextra_ajus, c_BASE_DATE) - c_BASE_DATE) * 1440), 30) = 0), 0)
                 + NVL((SELECT SUM(ROUND((NVL(s.horadoblesof, c_BASE_DATE) - c_BASE_DATE) * 1440))
                          FROM   SCA_ASISTENCIA_TAREO s
                          WHERE  s.cod_empresa  = p.cod_empresa
                          AND    s.cod_personal = p.cod_personal
                          AND    s.fechamar BETWEEN v_fec_hor_ini AND v_fec_hor_fin
                          AND    MOD(ROUND((NVL(s.horadoblesof, c_BASE_DATE) - c_BASE_DATE) * 1440), 30) = 0), 0)
                 + NVL((SELECT SUM(ROUND((NVL(s.horabancoh, c_BASE_DATE) - c_BASE_DATE) * 1440))
                          FROM   SCA_ASISTENCIA_TAREO s
                          WHERE  s.cod_empresa  = p.cod_empresa
                          AND    s.cod_personal = p.cod_personal
                          AND    s.fechamar BETWEEN v_fec_hor_ini AND v_fec_hor_fin
                          AND    MOD(ROUND((NVL(s.horabancoh, c_BASE_DATE) - c_BASE_DATE) * 1440), 30) = 0), 0)
                                                                              AS min_disp,
                   NVL(ROUND((NVL(td.tothoras, c_BASE_DATE) - c_BASE_DATE) * 1440), 0) AS min_jorn
            FROM   PLA_PERSONAL p
            LEFT JOIN SCA_ASISTENCIA_TAREO td
                   ON  td.cod_empresa  = p.cod_empresa
                   AND td.cod_personal = p.cod_personal
                   AND td.fechamar     = v_fec_des
            WHERE  p.cod_empresa = p_cod_empresa
            AND    INSTR(','||p_lista_personal||',', ','||p.cod_personal||',') > 0
            ORDER BY p.cod_personal
        ) LOOP
            v_min_disp := NVL(e.min_disp, 0);
            v_min_jorn := NVL(e.min_jorn, 0);

            IF v_min_jorn > 0 THEN
                v_min_usar := LEAST(v_min_disp, v_min_jorn);
            ELSIF v_cap_max IS NOT NULL THEN
                v_min_usar := LEAST(v_min_disp, v_cap_max);
            ELSE
                v_min_usar := v_min_disp;
            END IF;
            v_min_sobr := GREATEST(0, v_min_disp - v_min_usar);

            v_h_disp := fn_min_a_hhmi(v_min_disp);
            v_h_jorn := fn_min_a_hhmi(v_min_jorn);
            v_h_usar := fn_min_a_hhmi(v_min_usar);
            v_h_sobr := fn_min_a_hhmi(v_min_sobr);

            -- Saldo banco semanal del origen (informativo)
            BEGIN
                SELECT NVL(SUM(s.hc_banhorsem), 0)
                INTO   v_saldo_sem
                FROM   SCA_BANCOHORAS_SEM s
                JOIN   SCA_SEM_PROC sp
                       ON  sp.cod_empresa       = s.cod_empresa
                       AND sp.cod_tipo_planilla = s.cod_tipo_planilla
                       AND sp.ano_proceso       = s.ano_proceso
                       AND sp.mes_proceso       = s.mes_proceso
                       AND sp.sem_proceso       = s.sem_proceso
                WHERE  s.cod_empresa  = p_cod_empresa
                AND    s.cod_personal = e.cod_personal
                AND    v_fec_ori BETWEEN sp.fecini AND sp.fecfin;
            EXCEPTION WHEN OTHERS THEN v_saldo_sem := 0;
            END;

            v_buf_id := NULL;

            IF v_min_usar = 0 THEN
                v_estado := 'SIN_HORAS';
                v_motivo := 'Sin horas en origen ('||p_tipo_origen||
                            ') ni Dobles/Banco/HE el '||p_fecha_origen||
                            '. Saldo banco sem: '||v_saldo_sem||' min';

            ELSE
                -- =====================================================
                -- MIXTO AUTOMATICO: combina HE + Dobles + Banco del
                -- rango de horas en el orden que dicta p_tipo_origen.
                -- Aplica destino UNA vez con el total y luego descuenta
                -- cada fuente generando una fila SCA_COMPENSACION.
                -- =====================================================
                DECLARE
                    v_def_des     NUMBER;
                    v_horas_tot   VARCHAR2(10) := fn_min_a_hhmi(v_min_usar);
                    v_restante    NUMBER := v_min_usar;
                    v_consumir    NUMBER;
                    v_id_part     NUMBER;
                    v_partes      NUMBER := 0;

                    -- Procedimientos locales para consumir cada fuente
                    PROCEDURE consumir_dobles IS
                    BEGIN
                        IF v_restante <= 0 THEN RETURN; END IF;
                        -- Itera sobre dias del rango con horadoblesof disponibles
                        FOR rdob IN (
                            SELECT s.fechamar,
                                   ROUND((NVL(s.horadoblesof, c_BASE_DATE)
                                          - c_BASE_DATE) * 1440) AS dob_min
                            FROM   SCA_ASISTENCIA_TAREO s
                            WHERE  s.cod_empresa  = p_cod_empresa
                            AND    s.cod_personal = e.cod_personal
                            AND    s.fechamar BETWEEN v_fec_hor_ini AND v_fec_hor_fin
                            AND    s.horadoblesof > c_BASE_DATE
                            AND    MOD(ROUND((NVL(s.horadoblesof, c_BASE_DATE) - c_BASE_DATE) * 1440), 30) = 0
                            ORDER  BY s.fechamar
                        ) LOOP
                            EXIT WHEN v_restante <= 0;
                            v_consumir := LEAST(rdob.dob_min, v_restante);
                            INSERT INTO SCA_COMPENSACION (
                                id_compen, cod_empresa, cod_personal,
                                fechadestino, fechaorigen,
                                tipoorigen, tipocompensacion, tiempo, aux1
                            ) VALUES (
                                id_comp_seq.NEXTVAL, p_cod_empresa, e.cod_personal,
                                v_fec_des, rdob.fechamar,
                                'D', p_tipo_compensacion, v_consumir, 'M'||TO_CHAR(v_id_evento)
                            ) RETURNING id_compen INTO v_id_part;
                            prv_aplicar_origen(p_cod_empresa, e.cod_personal, rdob.fechamar,
                                               'D', fn_min_a_hhmi(v_consumir));
                            v_restante := v_restante - v_consumir;
                            v_partes   := v_partes + 1;
                            IF v_buf_id IS NULL THEN v_buf_id := v_id_part; END IF;
                        END LOOP;
                    END consumir_dobles;

                    PROCEDURE consumir_banco IS
                    BEGIN
                        IF v_restante <= 0 THEN RETURN; END IF;
                        -- Itera sobre dias del rango con horabancoh disponibles
                        FOR rban IN (
                            SELECT s.fechamar,
                                   ROUND((NVL(s.horabancoh, c_BASE_DATE)
                                          - c_BASE_DATE) * 1440) AS ban_min
                            FROM   SCA_ASISTENCIA_TAREO s
                            WHERE  s.cod_empresa  = p_cod_empresa
                            AND    s.cod_personal = e.cod_personal
                            AND    s.fechamar BETWEEN v_fec_hor_ini AND v_fec_hor_fin
                            AND    s.horabancoh > c_BASE_DATE
                            AND    MOD(ROUND((NVL(s.horabancoh, c_BASE_DATE) - c_BASE_DATE) * 1440), 30) = 0
                            ORDER  BY s.fechamar
                        ) LOOP
                            EXIT WHEN v_restante <= 0;
                            v_consumir := LEAST(rban.ban_min, v_restante);
                            INSERT INTO SCA_COMPENSACION (
                                id_compen, cod_empresa, cod_personal,
                                fechadestino, fechaorigen,
                                tipoorigen, tipocompensacion, tiempo, aux1
                            ) VALUES (
                                id_comp_seq.NEXTVAL, p_cod_empresa, e.cod_personal,
                                v_fec_des, rban.fechamar,
                                'B', p_tipo_compensacion, v_consumir, 'M'||TO_CHAR(v_id_evento)
                            ) RETURNING id_compen INTO v_id_part;
                            prv_aplicar_origen(p_cod_empresa, e.cod_personal, rban.fechamar,
                                               'B', fn_min_a_hhmi(v_consumir));
                            v_restante := v_restante - v_consumir;
                            v_partes   := v_partes + 1;
                            IF v_buf_id IS NULL THEN v_buf_id := v_id_part; END IF;
                        END LOOP;
                    END consumir_banco;

                    PROCEDURE consumir_he IS
                    BEGIN
                        IF v_restante <= 0 THEN RETURN; END IF;
                        FOR rhe IN (
                            SELECT s.fechamar,
                                   ROUND((NVL(s.horaextra_ajus, c_BASE_DATE)
                                          - c_BASE_DATE) * 1440) AS he_min
                            FROM   SCA_ASISTENCIA_TAREO s
                            WHERE  s.cod_empresa  = p_cod_empresa
                            AND    s.cod_personal = e.cod_personal
                            AND    s.fechamar BETWEEN v_fec_hor_ini AND v_fec_hor_fin
                            AND    s.horaextra_ajus > c_BASE_DATE
                            AND    MOD(ROUND((NVL(s.horaextra_ajus, c_BASE_DATE) - c_BASE_DATE) * 1440), 30) = 0
                            ORDER  BY s.fechamar
                        ) LOOP
                            EXIT WHEN v_restante <= 0;
                            v_consumir := LEAST(rhe.he_min, v_restante);
                            INSERT INTO SCA_COMPENSACION (
                                id_compen, cod_empresa, cod_personal,
                                fechadestino, fechaorigen,
                                tipoorigen, tipocompensacion, tiempo, aux1
                            ) VALUES (
                                id_comp_seq.NEXTVAL, p_cod_empresa, e.cod_personal,
                                v_fec_des, rhe.fechamar,
                                'E', p_tipo_compensacion, v_consumir, 'M'||TO_CHAR(v_id_evento)
                            ) RETURNING id_compen INTO v_id_part;
                            prv_aplicar_origen(p_cod_empresa, e.cod_personal,
                                               rhe.fechamar, 'E',
                                               fn_min_a_hhmi(v_consumir));
                            v_restante := v_restante - v_consumir;
                            v_partes   := v_partes + 1;
                            IF v_buf_id IS NULL THEN v_buf_id := v_id_part; END IF;
                        END LOOP;
                    END consumir_he;
                BEGIN
                    -- Validacion destino con el TOTAL combinado
                    v_def_des := fn_tiempo_destino(p_cod_empresa, e.cod_personal,
                                                    v_fec_des, p_tipo_compensacion);
                    IF p_tipo_compensacion IN ('A','T','N') AND v_def_des <> v_min_usar THEN
                        v_estado := 'ERR';
                        v_motivo := 'Destino ('||p_tipo_compensacion||')='||v_def_des||
                                    ' min, requerido exacto '||v_min_usar||' min';
                    ELSIF p_tipo_compensacion IN ('F','P') AND v_def_des < v_min_usar THEN
                        v_estado := 'ERR';
                        v_motivo := 'Destino ('||p_tipo_compensacion||')='||v_def_des||
                                    ' min, insuficiente para '||v_min_usar||' min';
                    ELSE
                        -- Aplica destino una sola vez con el total combinado
                        prv_aplicar_destino(p_cod_empresa, e.cod_personal, v_fec_des,
                                            p_tipo_compensacion, v_horas_tot);

                        -- Orden de consumo segun fuente preferente
                        IF p_tipo_origen = 'D' THEN
                            consumir_dobles;
                            consumir_banco;
                            consumir_he;
                        ELSIF p_tipo_origen = 'B' THEN
                            consumir_banco;
                            consumir_dobles;
                            consumir_he;
                        ELSE  -- 'E' u otro
                            consumir_he;
                            consumir_dobles;
                            consumir_banco;
                        END IF;

                        v_estado := 'OK';
                        v_motivo := 'OK: '||v_partes||' fuente(s), '||
                                    v_min_usar||' min compensados (pref='||
                                    p_tipo_origen||')';
                    END IF;
                EXCEPTION WHEN OTHERS THEN
                    v_estado := 'ERR';
                    v_motivo := 'EXC: '||SQLERRM;
                END;
            END IF;

            INSERT INTO SCA_TMP_COMPENS_MASIVO (
                id_evento, cod_personal, nombre_completo,
                min_disponibles, horas_disponibles,
                min_jornada_destino, horas_jornada_destino,
                min_a_compensar, horas_a_compensar,
                min_sobrante, horas_sobrante,
                id_compen, estado, motivo, saldo_banco_sem_min
            ) VALUES (
                v_id_evento, e.cod_personal, e.nombre_completo,
                v_min_disp, v_h_disp,
                v_min_jorn, v_h_jorn,
                v_min_usar, v_h_usar,
                v_min_sobr, v_h_sobr,
                v_buf_id, v_estado, v_motivo, v_saldo_sem
            );
        END LOOP;

        OPEN cv_resultado FOR
            SELECT cod_personal, nombre_completo,
                   min_disponibles, horas_disponibles,
                   min_jornada_destino, horas_jornada_destino,
                   min_a_compensar, horas_a_compensar,
                   min_sobrante, horas_sobrante,
                   id_compen, estado, motivo,
                   saldo_banco_sem_min, id_evento
            FROM   SCA_TMP_COMPENS_MASIVO
            WHERE  id_evento = v_id_evento
            ORDER  BY cod_personal;
    END REGISTRAR_EVENTO_MASIVO;


    -- =========================================================================
    -- CONSULTAR_EVENTO
    -- =========================================================================
    PROCEDURE CONSULTAR_EVENTO(
        p_id_evento       IN NUMBER,
        cv_resultado      OUT SYS_REFCURSOR
    ) AS
        v_aux VARCHAR2(20) := 'M'||TO_CHAR(p_id_evento);
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
                   -- Estado aplicacion: comprueba alerta en tareo destino
                   CASE
                       WHEN c.tipocompensacion = 'T' AND tdes.alerta04 = 'TC' THEN 'APLICADA'
                       WHEN c.tipocompensacion = 'A' AND tdes.alerta07 = 'SC' THEN 'APLICADA'
                       WHEN c.tipocompensacion = 'N' AND tdes.alerta03 = 'HC' THEN 'APLICADA'
                       WHEN c.tipocompensacion = 'F' AND tdes.alerta02 = 'FC' THEN 'APLICADA'
                       WHEN c.tipocompensacion = 'P' AND tdes.alerta09 = 'PC' THEN 'APLICADA'
                       ELSE 'PENDIENTE'
                   END                                     AS estado_aplicacion,
                   tdes.alerta02 AS des_alerta02,
                   tdes.alerta03 AS des_alerta03,
                   tdes.alerta04 AS des_alerta04,
                   tdes.alerta07 AS des_alerta07,
                   tdes.alerta09 AS des_alerta09,
                   tori.alerta06 AS ori_alerta06,
                   tori.alerta08 AS ori_alerta08,
                   p.ape_paterno||' '||p.ape_materno||' '||p.nom_trabajador AS nombre_completo
            FROM   SCA_COMPENSACION c
            LEFT JOIN SCA_ASISTENCIA_TAREO tdes
                   ON tdes.cod_empresa  = c.cod_empresa
                  AND tdes.cod_personal = c.cod_personal
                  AND tdes.fechamar     = c.fechadestino
            LEFT JOIN SCA_ASISTENCIA_TAREO tori
                   ON tori.cod_empresa  = c.cod_empresa
                  AND tori.cod_personal = c.cod_personal
                  AND tori.fechamar     = c.fechaorigen
            LEFT JOIN PLA_PERSONAL p
                   ON p.cod_empresa  = c.cod_empresa
                  AND p.cod_personal = c.cod_personal
            WHERE  c.aux1 = v_aux
            ORDER  BY c.cod_personal, c.fechaorigen;
    END CONSULTAR_EVENTO;

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
                   tori.horaextra_ajus AS ori_he_ajus,
                   tori.horadoblesof   AS ori_dobles,
                   tori.horabancoh     AS ori_banco,
                   tori.alerta06       AS ori_alerta06,
                   tori.alerta08       AS ori_alerta08,
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

END PKG_SCA_COMP_DIA_DIA;
/
