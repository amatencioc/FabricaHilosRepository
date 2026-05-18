/*******************************************************************************
 * PKG_SCA_COMP_DDC
 * -----------------------------------------------------------------------------
 * Compensacion de Dias de Descanso por Compensar (DDC) con HE SIMPLES.
 *
 * QUE ES UN DDC:
 *   Empleado con horario rotativo que tiene dias "libres" dentro de su semana
 *   laboral. Esos dias aparecen en el tareo como FALTA TOTAL (alerta02='FT')
 *   sin ninguna ausencia formal (sin permiso, sin vacaciones, sin descanso
 *   medico, sin licencias). Las HE simples de los dias laborales del mismo
 *   rango "pagan" esos dias de descanso.
 *
 * DIFERENCIA CLAVE vs PKG_SCA_COMP_HE_SIMPLE:
 *   - Origen: N dias con HE del rango (no un unico dia origen)
 *   - Destino: N dias DDC del rango (no un unico dia destino)
 *   - tipocompensacion: siempre 'F' (horas_falta)
 *   - Compensacion parcial: permitida (reduce horas_falta sin anularla)
 *   - Multiples INSERT en SCA_COMPENSACION por empleado (uno por par origen-destino)
 *
 * IDENTIFICACION DDC (por exclusion en SCA_ASISTENCIA_TAREO + SIG.RH_EVENTOS):
 *   alerta02 = 'FT'  AND horas_falta IS NOT NULL
 *   AND descanso = 'N'
 *   AND NVL(per_dia_comp,'N') = 'N'
 *   AND per_desc_med IS NULL AND per_vaca IS NULL AND per_subsidio IS NULL
 *   AND per_suspension IS NULL AND per_lic_sind IS NULL AND per_lic_pat IS NULL
 *   AND per_lic_fac IS NULL AND per_goce_fis IS NULL AND per_goce IS NULL
 *   AND per_sgoce IS NULL
 *   -- Ademas: NO debe existir evento C_TIPO='07' (FALTA NO JUSTIFICADA) en LOGIX
 *   -- para ese empleado y fecha. C_TIPO='07' no se sincroniza a AQUARIUS via el
 *   -- trigger TIA_RH_EVENTOS_AQUARIUS, por eso se consulta SIG.RH_EVENTOS directamente.
 *   -- Join: SCA_FOTOCHECK.num_fotocheck = SIG.RH_EVENTOS.c_codigo
 *
 * DISTRIBUCION HE -> DDC:
 *   - Pool de HE: todos los dias con horaextra_ajus > 0 en el rango
 *   - Consumo: DDC en orden cronologico, HE en orden cronologico
 *   - Un DDC puede consumir HE de multiples dias origen
 *   - Un dia HE puede contribuir a multiples DDC (si sobra despues del primero)
 *
 * EFECTOS EN TAREO:
 *   - Origen (HE): horaextra_ajus -= tiempo; recalc tramos; redondeo
 *   - Destino (DDC): horas_falta -= tiempo; si=0 -> NULL + alerta='FC'
 *                    horaefectiva += tiempo
 *
 * ADVERTENCIA REDONDEO:
 *   Tras descontar HE, SP_SCA_REDONDEAR_TAREO_HE puede bajar a 0.
 *   CALCULAR_DDC muestra min_he_post_round y estado='ADVERTENCIA_REDONDEO'.
 *
 * AUX1 pattern: 'D'||id_evento  (distingue de 'H' de HE_Simple y 'M' de Dia_Dia)
 *
 * FLUJO TIPICO DESDE .NET:
 *   1. LISTAR_DDC_RANGO      -> grid semana: dias HE + dias DDC candidatos
 *   2. [usuario selecciona empleados + confirma que dias son DDC]
 *   3. CALCULAR_DDC          -> preview de distribucion y advertencias redondeo
 *   4. [usuario confirma]
 *   5. REGISTRAR_DDC_MASIVO  -> registra + aplica; devuelve GGT por DDC
 *   6. COMMIT o ROLLBACK
 *   7. (opcional) CONSULTAR_RANGO_DDC / CONSULTAR_EVENTO_DDC
 *
 * COMPATIBILIDAD PASO 15:
 *   PASO 15 CUR_COMPENSACIONES para tipo='F': valida tiempo <= horas_falta (parcial ok).
 *   PASO 15 CUR_COMPENSACIONES para origen='E': valida horaextra_ajus >= tiempo.
 *   APLICAR_DIA_DDC re-aplica/revierte si el tareo cambio desde el registro.
 *
 * Autor:   Equipo AQUARIUS
 * Fecha:   09/05/2026
 *******************************************************************************/

-- =============================================================================
-- GLOBAL TEMPORARY TABLE para devolver resultados de REGISTRAR_DDC_MASIVO.
-- Filas viven en la sesion (preserve rows). Cada llamada usa id_evento unico.
-- =============================================================================
DECLARE
    v_existe NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_existe FROM USER_TABLES
    WHERE  table_name = 'SCA_TMP_DDC_RES';
    IF v_existe = 0 THEN
        EXECUTE IMMEDIATE q'[
            CREATE GLOBAL TEMPORARY TABLE SCA_TMP_DDC_RES (
                id_evento               NUMBER,
                cod_personal            VARCHAR2(20),
                nombre_completo         VARCHAR2(200),
                fecha_ddc               DATE,
                fecha_ddc_str           VARCHAR2(10),
                dia_semana              VARCHAR2(5),
                min_falta_total         NUMBER,         -- horas_falta original del DDC
                horas_falta_total       VARCHAR2(10),
                min_he_asignadas        NUMBER,         -- HE que se usaron para este DDC
                horas_he_asignadas      VARCHAR2(10),
                min_falta_restante      NUMBER,         -- sin compensar
                horas_falta_restante    VARCHAR2(10),
                estado                  VARCHAR2(30),   -- OK|PARCIAL|SIN_HE|ERR|ADVERTENCIA_REDONDEO
                motivo                  VARCHAR2(500)
            ) ON COMMIT PRESERVE ROWS
        ]';
    END IF;
END;
/

-- =============================================================================
-- PACKAGE SPEC
-- =============================================================================
CREATE OR REPLACE PACKAGE PKG_SCA_COMP_DDC AS

    /***************************************************************************
        LISTAR_DDC_RANGO
        Solo lectura. Muestra para cada empleado en el rango los dias con HE
        disponibles y los dias candidatos DDC (falta sin ausencia formal).
        Util para que el operador vea la semana completa y decida que dias
        son realmente DDC antes de aplicar compensacion.

        PARAMETROS:
        - p_cod_empresa       Empresa
        - p_fecha_inicio      'dd/MM/yyyy' inicio del rango DDC (ej. lunes)
        - p_fecha_fin         'dd/MM/yyyy' fin del rango DDC (ej. sabado/domingo)
        - p_nombre            NULL = sin filtro; texto parcial sobre nombre
        - p_fecha_he_inicio   'dd/MM/yyyy' inicio del rango para buscar dias con HE.
                              NULL = usa p_fecha_inicio (mismo rango que DDC).
        - p_fecha_he_fin      'dd/MM/yyyy' fin del rango para buscar dias con HE.
                              NULL = usa p_fecha_fin (mismo rango que DDC).

        - p_solo_ddc        'S' (default) = solo devuelve filas DDC y BLOQ_LOGIX (vista inicial).
                              'N'           = devuelve todos los tipos (HE, DDC, BLOQ_LOGIX, DESCANSO).

        CURSOR resultado (una fila por empleado+dia):
          cod_personal, nombre_completo,
          fechamar, fechamar_str, dia_semana,
          tipo_dia     ('HE'=dia con HE | 'DDC'=candidato DDC | 'DESCANSO' | 'BLOQ_LOGIX'=DDC bloqueado por evento en LOGIX)
          min_he, horas_he              (0 si no es dia HE)
          min_falta, horas_falta        (0 si no es candidato DDC)
          alerta02, alerta06, descanso,
          ya_compensado  ('S'/'N': si ya existe comp DDC para ese dia)
          desc_alerta06  descripcion textual de alerta06 (NULL si no aplica)
    ***************************************************************************/
    PROCEDURE LISTAR_DDC_RANGO(
        p_cod_empresa      IN VARCHAR2,
        p_fecha_inicio     IN VARCHAR2,
        p_fecha_fin        IN VARCHAR2,
        p_nombre           IN VARCHAR2 DEFAULT NULL,
        p_fecha_he_inicio  IN VARCHAR2 DEFAULT NULL,
        p_fecha_he_fin     IN VARCHAR2 DEFAULT NULL,
        p_solo_ddc         IN VARCHAR2 DEFAULT 'S',
        cv_resultado       OUT SYS_REFCURSOR
    );

    /***************************************************************************
        LISTAR_HE_PERSONAL
        Solo lectura. Devuelve los dias con HE disponibles de un empleado
        especifico en el rango de fechas HE indicado.
        Llamar al hacer click en un empleado en la vista DDC para expandir
        los dias HE que pueden financiar la compensacion.

        PARAMETROS:
        - p_cod_empresa       Empresa
        - p_cod_personal      Empleado especifico
        - p_fecha_he_inicio   'dd/MM/yyyy' inicio del rango HE
        - p_fecha_he_fin      'dd/MM/yyyy' fin del rango HE

        CURSOR resultado (mismas columnas que LISTAR_DDC_RANGO, tipo_dia='HE'):
          cod_personal, nombre_completo,
          fechamar, fechamar_str, dia_semana,
          tipo_dia     siempre 'HE'
          min_he, horas_he
          min_falta (siempre 0), horas_falta (NULL)
          alerta02, alerta06, descanso, nummarcaciones,
          ya_compensado (siempre 'N'), logix_* (siempre NULL)
          desc_alerta06 descripcion textual de alerta06
    ***************************************************************************/
    PROCEDURE LISTAR_HE_PERSONAL(
        p_cod_empresa      IN VARCHAR2,
        p_cod_personal     IN VARCHAR2,
        p_fecha_he_inicio  IN VARCHAR2,
        p_fecha_he_fin     IN VARCHAR2,
        cv_resultado       OUT SYS_REFCURSOR
    );

    /***************************************************************************
        CALCULAR_DDC
        Solo lectura. Simula la distribucion de HE hacia DDC para los empleados
        de la lista en el rango dado. Muestra por cada DDC cuanto se asignaria,
        si hay advertencia de redondeo y si la compensacion seria parcial.

        PARAMETROS:
        - p_cod_empresa       Empresa
        - p_fecha_inicio      'dd/MM/yyyy' inicio rango DDC
        - p_fecha_fin         'dd/MM/yyyy' fin rango DDC
        - p_lista_personal    'cod1,cod2,...' OBLIGATORIO
        - p_fecha_he_inicio   'dd/MM/yyyy' inicio rango HE (NULL = igual al rango DDC)
        - p_fecha_he_fin      'dd/MM/yyyy' fin rango HE    (NULL = igual al rango DDC)

        CURSOR resultado (una fila por DDC por empleado):
          cod_personal, nombre_completo,
          fecha_ddc, fecha_ddc_str, dia_semana,
          min_falta, horas_falta,
          min_he_asignadas_sim, horas_he_asignadas_sim,
          min_falta_restante_sim, horas_falta_restante_sim,
          total_he_rango_sim,    horas_total_he_rango_sim,
          estado   ('OK'|'PARCIAL'|'SIN_HE'|'ADVERTENCIA_REDONDEO')
    ***************************************************************************/
    PROCEDURE CALCULAR_DDC(
        p_cod_empresa      IN VARCHAR2,
        p_fecha_inicio     IN VARCHAR2,
        p_fecha_fin        IN VARCHAR2,
        p_lista_personal   IN VARCHAR2,
        p_fecha_he_inicio  IN VARCHAR2 DEFAULT NULL,
        p_fecha_he_fin     IN VARCHAR2 DEFAULT NULL,
        cv_resultado       OUT SYS_REFCURSOR
    );

    /***************************************************************************
        REGISTRAR_DDC_MASIVO
        Aplica la compensacion DDC para los empleados de la lista en el rango.
        Para cada empleado:
          1. Identifica dias DDC del rango (sin ausencias formales, alerta='FT')
          2. Identifica dias con HE del rango
          3. Distribuye HE a DDC en orden cronologico
          4. Por cada par (fechaorigen, fechadestino):
               - INSERT SCA_COMPENSACION (tipoorigen='E', tipocomp='F', aux1='D'||id)
               - Aplica efecto origen (reduce horaextra_ajus)
               - Aplica efecto destino (reduce horas_falta)
          5. Guarda en GGT (una fila por DDC con resumen)

        PARAMETROS:
        - p_cod_empresa       Empresa
        - p_fecha_inicio      'dd/MM/yyyy' inicio rango DDC
        - p_fecha_fin         'dd/MM/yyyy' fin rango DDC
        - p_lista_personal    'cod1,cod2,...' OBLIGATORIO
        - p_fecha_he_inicio   'dd/MM/yyyy' inicio rango HE (NULL = igual al rango DDC)
        - p_fecha_he_fin      'dd/MM/yyyy' fin rango HE    (NULL = igual al rango DDC)

        CURSOR resultado (una fila por DDC procesado):
          id_evento, cod_personal, nombre_completo,
          fecha_ddc, fecha_ddc_str, dia_semana,
          min_falta_total, horas_falta_total,
          min_he_asignadas, horas_he_asignadas,
          min_falta_restante, horas_falta_restante,
          estado, motivo
    ***************************************************************************/
    PROCEDURE REGISTRAR_DDC_MASIVO(
        p_cod_empresa      IN VARCHAR2,
        p_fecha_inicio     IN VARCHAR2,
        p_fecha_fin        IN VARCHAR2,
        p_lista_personal   IN VARCHAR2,
        p_lista_ddc_fechas IN VARCHAR2 DEFAULT NULL,  -- 'cod:dd/MM/yyyy,cod:dd/MM/yyyy,...' solo esos dias DDC
        p_fecha_he_inicio  IN VARCHAR2 DEFAULT NULL,
        p_fecha_he_fin     IN VARCHAR2 DEFAULT NULL,
        cv_resultado       OUT SYS_REFCURSOR
    );

    /***************************************************************************
        APLICAR_DIA_DDC
        Re-aplica o revierte compensaciones DDC (aux1 LIKE 'D%') donde
        este dia es ORIGEN o DESTINO. Para integracion con PASO 15 o reproceso.

        PARAMETROS:
        - p_cod_empresa         Empresa
        - p_cod_personal        Empleado
        - p_fecha               'dd/MM/yyyy'
        - p_eliminar_no_cuadra  'S' (default) = elimina si tareo cambio y no cuadra
                                'N' = solo aplica, no elimina

        CURSOR resultado:
          fecha, cod_empresa, cod_personal,
          aplicadas_destino, aplicadas_origen, eliminadas, errores
    ***************************************************************************/
    PROCEDURE APLICAR_DIA_DDC(
        p_cod_empresa        IN VARCHAR2,
        p_cod_personal       IN VARCHAR2,
        p_fecha              IN VARCHAR2,
        p_eliminar_no_cuadra IN VARCHAR2 DEFAULT 'S',
        cv_resultado         OUT SYS_REFCURSOR
    );

    /***************************************************************************
        CONSULTAR_RANGO_DDC
        Lista compensaciones DDC (tipoorigen='E', aux1 LIKE 'D%') en el rango.
        Incluye estado de aplicacion en tareo origen y destino.

        CURSOR incluye:
          id_compen, cod_empresa, cod_personal,
          fechaorigen_str, fechadestino_str,
          tipoorigen, tipocompensacion, tiempo_min, tiempo_hhmi, aux1,
          ori_alerta06, dest_alerta02
    ***************************************************************************/
    PROCEDURE CONSULTAR_RANGO_DDC(
        p_cod_empresa    IN VARCHAR2 DEFAULT NULL,
        p_cod_personal   IN VARCHAR2 DEFAULT NULL,
        p_fecha_inicio   IN VARCHAR2,
        p_fecha_fin      IN VARCHAR2,
        cv_resultado     OUT SYS_REFCURSOR
    );

    /***************************************************************************
        CONSULTAR_EVENTO_DDC
        Devuelve todas las filas de SCA_COMPENSACION de un evento especifico
        (aux1 = 'D'||p_id_evento). Util para auditoria post-registro.
    ***************************************************************************/
    PROCEDURE CONSULTAR_EVENTO_DDC(
        p_id_evento  IN NUMBER,
        cv_resultado OUT SYS_REFCURSOR
    );

    /***************************************************************************
        CONSULTAR_COMP_DDC
        Devuelve la fila de SCA_COMPENSACION correspondiente a un id_compen
        especifico. Se usa desde el historial cuando el usuario hace clic en
        el detalle de una fila individual (no del evento completo).
    ***************************************************************************/
    PROCEDURE CONSULTAR_COMP_DDC(
        p_id_compen  IN NUMBER,
        cv_resultado OUT SYS_REFCURSOR
    );

END PKG_SCA_COMP_DDC;
/
SHOW ERRORS PACKAGE PKG_SCA_COMP_DDC;

-- =============================================================================
-- PACKAGE BODY
-- =============================================================================
CREATE OR REPLACE PACKAGE BODY PKG_SCA_COMP_DDC AS

    -- =========================================================================
    -- CONSTANTES
    -- =========================================================================
    c_BASE_DATE  CONSTANT DATE := TO_DATE('01/01/1900','dd/MM/yyyy');

    -- =========================================================================
    -- TIPOS INTERNOS para distribucion HE -> DDC
    -- =========================================================================
    TYPE t_dia_rec IS RECORD (
        fechamar  DATE,
        min_valor NUMBER
    );
    TYPE t_lista IS TABLE OF t_dia_rec INDEX BY PLS_INTEGER;

    -- =========================================================================
    -- UTILIDADES PRIVADAS
    -- =========================================================================

    FUNCTION fn_hhmi_a_min(p_horas IN VARCHAR2) RETURN NUMBER IS
    BEGIN
        IF p_horas IS NULL OR INSTR(p_horas,':') = 0 THEN RETURN 0; END IF;
        RETURN (TO_NUMBER(SUBSTR(p_horas,1,INSTR(p_horas,':')-1)) * 60)
             + TO_NUMBER(SUBSTR(p_horas, INSTR(p_horas,':')+1));
    END fn_hhmi_a_min;

    FUNCTION fn_min_a_hhmi(p_min IN NUMBER) RETURN VARCHAR2 IS
    BEGIN
        IF p_min IS NULL OR p_min <= 0 THEN RETURN '00:00'; END IF;
        RETURN SUBSTR('00' || TO_CHAR(TRUNC(p_min/60,0)), -2, 2)
            || ':' ||
               SUBSTR('00' || TO_CHAR(MOD(p_min, 60)), -2, 2);
    END fn_min_a_hhmi;

    FUNCTION fn_date_a_min(p_dt IN DATE) RETURN NUMBER IS
    BEGIN
        IF p_dt IS NULL THEN RETURN 0; END IF;
        -- Usa HH24:MI para ser robusto a fecha-base incorrecta (31/12/1899 vs 01/01/1900).
        -- El sistema original graba horaextra_ajus con base 31/12/1899 cuando la
        -- autorizacion viene del sistema Aquarius antiguo; la arimetica (p_dt - c_BASE)
        -- devolveria negativo en ese caso. La extraccion de tiempo es invariante.
        RETURN TO_NUMBER(TO_CHAR(p_dt, 'HH24')) * 60
             + TO_NUMBER(TO_CHAR(p_dt, 'MI'));
    END fn_date_a_min;

    -- Calcula minutos HE post-redondeo si se deducen v_deducir minutos
    -- Formula: FLOOR((min + 15) / 60) * 60
    FUNCTION fn_he_post_round(p_he_min IN NUMBER, p_deducir IN NUMBER) RETURN NUMBER IS
        v_resultado NUMBER;
    BEGIN
        v_resultado := p_he_min - p_deducir;
        IF v_resultado <= 0 THEN RETURN 0; END IF;
        RETURN FLOOR((v_resultado + 15) / 60) * 60;
    END fn_he_post_round;

    -- Lee horaextra_ajus actual (minutos) de un empleado en una fecha.
    -- Para dias sin autorizacion pendiente (hayhed_poraut='S') sin horaextra_ajus,
    -- devuelve horaextra cruda: toda la HE esta disponible para compensar.
    FUNCTION fn_he_actual(
        p_emp IN VARCHAR2, p_per IN VARCHAR2, p_fec IN DATE
    ) RETURN NUMBER IS
        v_ajus   DATE;
        v_raw    DATE;
        v_poraut VARCHAR2(1);
    BEGIN
        BEGIN
            SELECT horaextra_ajus, horaextra, NVL(hayhed_poraut,'N')
            INTO   v_ajus, v_raw, v_poraut
            FROM   SCA_ASISTENCIA_TAREO
            WHERE  cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec;
        EXCEPTION WHEN NO_DATA_FOUND THEN RETURN 0;
        END;
        -- Dia sin autorizacion formal (hayhed_poraut='S') y sin horaextra_ajus:
        -- toda la HE cruda esta disponible para compensar (no va a planilla).
        IF v_poraut = 'S' AND v_ajus IS NULL THEN
            RETURN fn_date_a_min(v_raw);
        END IF;
        RETURN fn_date_a_min(v_ajus);
    END fn_he_actual;

    -- Lee horas_falta actual (minutos) de un empleado en una fecha
    FUNCTION fn_falta_actual(
        p_emp IN VARCHAR2, p_per IN VARCHAR2, p_fec IN DATE
    ) RETURN NUMBER IS
        v_dt DATE;
    BEGIN
        BEGIN
            SELECT horas_falta INTO v_dt
            FROM   SCA_ASISTENCIA_TAREO
            WHERE  cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec;
        EXCEPTION WHEN NO_DATA_FOUND THEN v_dt := NULL;
        END;
        RETURN fn_date_a_min(v_dt);
    END fn_falta_actual;

    -- =========================================================================
    -- INTERNO: aplica descuento de HE en TAREO ORIGEN (tipoorigen='E')
    -- Identico a PKG_SCA_COMP_HE_SIMPLE.prv_aplicar_origen_E
    -- =========================================================================
    PROCEDURE prv_aplicar_origen_E(
        p_emp IN VARCHAR2, p_per IN VARCHAR2, p_fec IN DATE,
        p_min IN NUMBER
    ) AS
    BEGIN
        IF p_min <= 0 THEN RETURN; END IF;

        -- 1. Descuenta tothoramarcas
        UPDATE SCA_ASISTENCIA_TAREO
        SET tothoramarcas = tothoramarcas - p_min/1440
        WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec;

        -- 1b. Pre-popula horaextra_ajus=horaextra para dias nunca autorizados.
        --     horaexofi1/2/3 se dejan NULL intencionalmente: la HE no va a planilla.
        UPDATE SCA_ASISTENCIA_TAREO
        SET    horaextra_ajus = horaextra
        WHERE  cod_empresa  = p_emp
        AND    cod_personal = p_per
        AND    fechamar     = p_fec
        AND    NVL(hayhed_poraut,'N') = 'S'
        AND    horaextra_ajus IS NULL
        AND    horaextra > c_BASE_DATE;

        -- 2. Descuenta horaextra_ajus
        UPDATE SCA_ASISTENCIA_TAREO
        SET horaextra_ajus = horaextra_ajus - p_min/1440
        WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec;

        -- 3. Recalcula tramos horaexofi1/2/3
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

        -- 3b. Para dias nunca autorizados: anula tramos recalculados.
        --     horaexofi1/2/3 deben quedar NULL: la HE no va a planilla.
        UPDATE SCA_ASISTENCIA_TAREO
        SET    horaexofi1 = NULL, horaexofi2 = NULL, horaexofi3 = NULL
        WHERE  cod_empresa  = p_emp
        AND    cod_personal = p_per
        AND    fechamar     = p_fec
        AND    NVL(hayhed_poraut,'N') = 'S'
        AND    horaextraofi IS NULL;

        -- 4. Marca alerta06='EC' si llega a 0
        UPDATE SCA_ASISTENCIA_TAREO
        SET alerta06 = CASE WHEN horaextra_ajus = c_BASE_DATE THEN 'EC' ELSE alerta06 END
        WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec;

        -- 5. Redondeo a hora entera (FLOOR((min+15)/60)*60)
        SP_SCA_REDONDEAR_TAREO_HE(p_emp, p_per, p_fec);

        -- 6. Post-redondeo: garantizar alerta EC si quedo en 0 exacto
        UPDATE SCA_ASISTENCIA_TAREO
        SET    alerta06 = 'EC'
        WHERE  cod_empresa  = p_emp
        AND    cod_personal = p_per
        AND    fechamar     = p_fec
        AND    horaextra_ajus = c_BASE_DATE;
    END prv_aplicar_origen_E;

    -- =========================================================================
    -- INTERNO: revierte descuento HE en TAREO ORIGEN
    -- =========================================================================
    PROCEDURE prv_revertir_origen_E(
        p_emp IN VARCHAR2, p_per IN VARCHAR2, p_fec IN DATE,
        p_min IN NUMBER
    ) AS
    BEGIN
        IF p_min <= 0 THEN RETURN; END IF;

        -- Restaura tothoramarcas y horaextra_ajus
        UPDATE SCA_ASISTENCIA_TAREO
        SET tothoramarcas  = NVL(tothoramarcas,  c_BASE_DATE) + p_min/1440,
            horaextra_ajus = NVL(horaextra_ajus, c_BASE_DATE) + p_min/1440
        WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec;

        -- Recalcula tramos
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

        -- Quita alerta EC si ya no es 0
        UPDATE SCA_ASISTENCIA_TAREO
        SET    alerta06 = CASE WHEN horaextra_ajus > c_BASE_DATE THEN 'EE' ELSE alerta06 END
        WHERE  cod_empresa  = p_emp
        AND    cod_personal = p_per
        AND    fechamar     = p_fec;

        -- Redondeo post-restauracion
        SP_SCA_REDONDEAR_TAREO_HE(p_emp, p_per, p_fec);

        -- Para dias nunca autorizados: anula tramos recalculados.
        -- El recalculo de tramos de arriba es correcto para dias autorizados;
        -- para dias con hayhed_poraut='S' sin horaextraofi, la HE no va a planilla.
        UPDATE SCA_ASISTENCIA_TAREO
        SET    horaexofi1 = NULL, horaexofi2 = NULL, horaexofi3 = NULL
        WHERE  cod_empresa  = p_emp
        AND    cod_personal = p_per
        AND    fechamar     = p_fec
        AND    NVL(hayhed_poraut,'N') = 'S'
        AND    horaextraofi IS NULL;
    END prv_revertir_origen_E;

    -- =========================================================================
    -- INTERNO: aplica efecto en TAREO DESTINO DDC (tipo='F', parcial permitido)
    -- Reduce horas_falta en p_min. Si llega a 0: anula + alerta='FC'.
    -- Si parcial: reduce y mantiene alerta='FT'.
    -- =========================================================================
    PROCEDURE prv_aplicar_destino_ddc(
        p_emp IN VARCHAR2, p_per IN VARCHAR2, p_fec IN DATE,
        p_min IN NUMBER
    ) AS
    BEGIN
        IF p_min <= 0 THEN RETURN; END IF;

        -- Reduce horas_falta en p_min; acredita horaefectiva y tothoramarcas
        UPDATE SCA_ASISTENCIA_TAREO
        SET
            horas_falta   = CASE
                                WHEN (NVL(horas_falta, c_BASE_DATE) - p_min/1440) <= c_BASE_DATE
                                THEN NULL
                                ELSE horas_falta - p_min/1440
                            END,
            alerta02      = CASE
                                WHEN (NVL(horas_falta, c_BASE_DATE) - p_min/1440) <= c_BASE_DATE
                                THEN 'FC'
                                ELSE alerta02   -- mantiene 'FT' si parcial
                            END,
            horaefectiva  = NVL(horaefectiva,  c_BASE_DATE) + p_min/1440,
            tothoramarcas = NVL(tothoramarcas, c_BASE_DATE) + p_min/1440
        WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec;

        -- Normalizar si horaefectiva quedo en base_date (no deberia, pero por seguridad)
        UPDATE SCA_ASISTENCIA_TAREO
        SET horaefectiva = CASE WHEN horaefectiva = c_BASE_DATE THEN NULL ELSE horaefectiva END
        WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec;
    END prv_aplicar_destino_ddc;

    -- =========================================================================
    -- INTERNO: revierte efecto en TAREO DESTINO DDC
    -- Suma de vuelta p_min a horas_falta
    -- =========================================================================
    PROCEDURE prv_revertir_destino_ddc(
        p_emp IN VARCHAR2, p_per IN VARCHAR2, p_fec IN DATE,
        p_min IN NUMBER
    ) AS
    BEGIN
        IF p_min <= 0 THEN RETURN; END IF;

        UPDATE SCA_ASISTENCIA_TAREO
        SET
            horas_falta   = NVL(horas_falta, c_BASE_DATE) + p_min/1440,
            alerta02      = CASE
                                -- Si vuelve a tothoras completo -> FT; si parcial mantiene estado
                                WHEN (NVL(horas_falta, c_BASE_DATE) + p_min/1440) >= NVL(tothoras, c_BASE_DATE)
                                THEN 'FT'
                                ELSE alerta02
                            END,
            horaefectiva  = horaefectiva  - p_min/1440,
            tothoramarcas = tothoramarcas - p_min/1440
        WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec;

        -- Normalizar si bajaron a base_date o menos
        UPDATE SCA_ASISTENCIA_TAREO
        SET horaefectiva  = CASE WHEN horaefectiva  <= c_BASE_DATE THEN NULL ELSE horaefectiva  END,
            tothoramarcas = CASE WHEN tothoramarcas <= c_BASE_DATE THEN NULL ELSE tothoramarcas END
        WHERE cod_empresa = p_emp AND cod_personal = p_per AND fechamar = p_fec;
    END prv_revertir_destino_ddc;

    -- =========================================================================
    -- INTERNO: carga dias DDC candidatos de un empleado en el rango
    -- =========================================================================
    PROCEDURE prv_cargar_ddc(
        p_emp   IN VARCHAR2,
        p_per   IN VARCHAR2,
        p_fini  IN DATE,
        p_ffin  IN DATE,
        v_lista OUT t_lista
    ) IS
        v_i PLS_INTEGER := 0;
    BEGIN
        v_lista.DELETE;
        FOR r IN (
            SELECT t.fechamar,
                   ROUND((NVL(t.horas_falta, c_BASE_DATE) - c_BASE_DATE) * 1440) AS falta_min
            FROM   SCA_ASISTENCIA_TAREO t
            WHERE  t.cod_empresa = p_emp
            AND    t.cod_personal = p_per
            AND    t.fechamar BETWEEN p_fini AND p_ffin
            AND    t.alerta02 = 'FT'
            AND    t.horas_falta IS NOT NULL
            AND    NVL(t.descanso,'N') = 'N'
            AND    NVL(t.per_dia_comp,'N') = 'N'
            AND    t.per_desc_med IS NULL
            AND    t.per_vaca IS NULL
            AND    t.per_subsidio IS NULL
            AND    t.per_suspension IS NULL
            AND    t.per_lic_sind IS NULL
            AND    t.per_lic_pat IS NULL
            AND    t.per_lic_fac IS NULL
            AND    t.per_goce_fis IS NULL
            AND    t.per_goce IS NULL
            AND    t.per_sgoce IS NULL
            -- Excluir si existe FALTA NO JUSTIFICADA (C_TIPO='07') en LOGIX para ese dia.
            -- C_TIPO='07' no se sincroniza a AQUARIUS, se consulta SIG.RH_EVENTOS directamente.
            -- NOTA: Se usa el ultimo fotocheck del empleado (MAX id_fotocheck), sin filtrar act_fotocheck.
            AND    NOT EXISTS (
                       SELECT 1
                       FROM   SIG.RH_EVENTOS re
                       WHERE  re.c_tipo = '07'
                       AND    TO_NUMBER(re.c_codigo) = (
                                  SELECT TO_NUMBER(
                                             MAX(sf2.num_fotocheck)
                                             KEEP (DENSE_RANK LAST ORDER BY sf2.id_fotocheck)
                                         )
                                  FROM   SCA_FOTOCHECK sf2
                                  WHERE  sf2.cod_empresa  = t.cod_empresa
                                  AND    sf2.cod_personal = t.cod_personal
                              )
                       AND    re.d_inicio                  <= t.fechamar
                       AND    NVL(re.d_final, re.d_inicio) >= t.fechamar
                   )
            -- Excluir dias que ya tienen comp DDC aplicada
            AND    NOT EXISTS (
                       SELECT 1 FROM SCA_COMPENSACION c2
                       WHERE  c2.cod_empresa      = t.cod_empresa
                       AND    c2.cod_personal     = t.cod_personal
                       AND    c2.fechadestino     = t.fechamar
                       AND    c2.tipocompensacion = 'F'
                       AND    c2.aux1 LIKE 'D%'
                   )
            ORDER  BY t.fechamar
        ) LOOP
            IF r.falta_min > 0 THEN
                v_i := v_i + 1;
                v_lista(v_i).fechamar  := r.fechamar;
                v_lista(v_i).min_valor := r.falta_min;
            END IF;
        END LOOP;
    END prv_cargar_ddc;

    -- =========================================================================
    -- INTERNO: carga dias HE de un empleado en el rango
    -- =========================================================================
    PROCEDURE prv_cargar_he(
        p_emp   IN VARCHAR2,
        p_per   IN VARCHAR2,
        p_fini  IN DATE,
        p_ffin  IN DATE,
        v_lista OUT t_lista
    ) IS
        v_i PLS_INTEGER := 0;
    BEGIN
        v_lista.DELETE;
        FOR r IN (
            SELECT t.fechamar,
                   CASE WHEN NVL(t.hayhed_poraut,'S') != 'N' AND t.horaextra_ajus IS NULL
                        THEN NVL(TO_NUMBER(TO_CHAR(t.horaextra,      'HH24'))*60
                               + TO_NUMBER(TO_CHAR(t.horaextra,      'MI')), 0)
                        ELSE NVL(TO_NUMBER(TO_CHAR(t.horaextra_ajus, 'HH24'))*60
                               + TO_NUMBER(TO_CHAR(t.horaextra_ajus, 'MI')), 0)
                   END AS he_min
            FROM   SCA_ASISTENCIA_TAREO t
            WHERE  t.cod_empresa  = p_emp
            AND    t.cod_personal = p_per
            AND    t.fechamar BETWEEN p_fini AND p_ffin
            AND    (  (t.horaextra_ajus IS NOT NULL
                       AND (TO_NUMBER(TO_CHAR(t.horaextra_ajus,'HH24'))*60
                          + TO_NUMBER(TO_CHAR(t.horaextra_ajus,'MI'))) > 0)
                   OR (NVL(t.hayhed_poraut,'S') != 'N'
                       AND t.horaextra_ajus IS NULL
                       AND t.horaextra IS NOT NULL
                       AND (TO_NUMBER(TO_CHAR(t.horaextra,'HH24'))*60
                          + TO_NUMBER(TO_CHAR(t.horaextra,'MI'))) > 0)
                   )
            AND    NVL(t.descanso,'N') = 'N'
            AND    NVL(t.hayhed_poraut,'S') != 'N'   -- solo HE pendientes de autorizar (no comprometidas con planilla)
            ORDER  BY t.fechamar
        ) LOOP
            IF r.he_min > 0 THEN
                v_i := v_i + 1;
                v_lista(v_i).fechamar  := r.fechamar;
                v_lista(v_i).min_valor := r.he_min;
            END IF;
        END LOOP;
    END prv_cargar_he;

    -- =========================================================================
    -- LISTAR_DDC_RANGO
    -- =========================================================================
    PROCEDURE LISTAR_DDC_RANGO(
        p_cod_empresa      IN VARCHAR2,
        p_fecha_inicio     IN VARCHAR2,
        p_fecha_fin        IN VARCHAR2,
        p_nombre           IN VARCHAR2 DEFAULT NULL,
        p_fecha_he_inicio  IN VARCHAR2 DEFAULT NULL,
        p_fecha_he_fin     IN VARCHAR2 DEFAULT NULL,
        p_solo_ddc         IN VARCHAR2 DEFAULT 'S',
        cv_resultado       OUT SYS_REFCURSOR
    ) AS
        v_fec_ini    DATE := TO_DATE(p_fecha_inicio, 'dd/MM/yyyy');
        v_fec_fin    DATE := TO_DATE(p_fecha_fin,    'dd/MM/yyyy');
        -- Rango independiente para buscar dias con HE disponibles.
        -- NULL = usa el mismo rango que los candidatos DDC.
        v_fec_he_ini DATE := NVL(TO_DATE(p_fecha_he_inicio, 'dd/MM/yyyy'),
                                 TO_DATE(p_fecha_inicio,    'dd/MM/yyyy'));
        v_fec_he_fin DATE := NVL(TO_DATE(p_fecha_he_fin,    'dd/MM/yyyy'),
                                 TO_DATE(p_fecha_fin,       'dd/MM/yyyy'));
        v_nombre     VARCHAR2(200) := CASE WHEN p_nombre IS NOT NULL
                                          THEN '%'||UPPER(TRIM(p_nombre))||'%'
                                          ELSE NULL END;
    BEGIN
        OPEN cv_resultado FOR
            WITH
            -- ① Ultimo fotocheck por empleado (una sola lectura de SCA_FOTOCHECK)
            ft AS (
                SELECT cod_empresa, cod_personal,
                       MAX(num_fotocheck)
                           KEEP (DENSE_RANK LAST ORDER BY id_fotocheck) AS num_fotocheck
                FROM   SCA_FOTOCHECK
                WHERE  cod_empresa = p_cod_empresa
                GROUP  BY cod_empresa, cod_personal
            ),
            -- ② Eventos LOGIX c_tipo='07' del rango (una sola lectura cross-schema)
            lx AS (
                SELECT TO_NUMBER(re.c_codigo)              AS fch_num,
                       re.d_inicio,
                       NVL(re.d_final, re.d_inicio)        AS d_final,
                       re.c_motivo,
                       rt.descripcion                      AS desc_motivo
                FROM   SIG.RH_EVENTOS re
                LEFT JOIN SIG.RH_RTPS rt
                       ON  rt.tabla  = '100'
                       AND rt.codigo = re.c_motivo
                WHERE  re.c_tipo = '07'
                AND    re.d_inicio                <= v_fec_fin
                AND    NVL(re.d_final,re.d_inicio) >= v_fec_ini
            ),
            -- ③ Candidatos FT: un solo scan de SCA_ASISTENCIA_TAREO con LEFT JOIN
            --    a ft y lx; clasifica cada fila como 'DDC' o 'BLOQ_LOGIX'.
            tareo_cands AS (
                SELECT t.cod_empresa, t.cod_personal, t.fechamar,
                       CASE WHEN lx.fch_num IS NOT NULL
                            THEN 'BLOQ_LOGIX' ELSE 'DDC' END          AS tipo_dia,
                       ROUND((NVL(t.horas_falta, c_BASE_DATE) - c_BASE_DATE)*1440) AS min_falta,
                       t.alerta02, t.alerta06,
                       NVL(t.descanso,'N')                             AS descanso,
                       t.nummarcaciones,
                       lx.c_motivo                                     AS logix_cmotivo,
                       lx.desc_motivo                                  AS logix_desc_motivo,
                       TO_CHAR(lx.d_inicio, 'DD/MM/YYYY')              AS logix_dinicio,
                       TO_CHAR(lx.d_final,  'DD/MM/YYYY')              AS logix_dfinal
                FROM   SCA_ASISTENCIA_TAREO t
                LEFT JOIN ft
                       ON  ft.cod_empresa  = t.cod_empresa
                       AND ft.cod_personal = t.cod_personal
                LEFT JOIN lx
                       ON  lx.fch_num  = TO_NUMBER(ft.num_fotocheck)
                       AND lx.d_inicio <= t.fechamar
                       AND lx.d_final  >= t.fechamar
                WHERE  t.cod_empresa = p_cod_empresa
                AND    t.fechamar BETWEEN v_fec_ini AND v_fec_fin
                AND    t.alerta02 = 'FT'
                AND    t.horas_falta IS NOT NULL
                AND    NVL(t.descanso,'N') = 'N'
                AND    NVL(t.per_dia_comp,'N') = 'N'
                AND    t.per_desc_med IS NULL AND t.per_vaca IS NULL
                AND    t.per_subsidio IS NULL AND t.per_suspension IS NULL
                AND    t.per_lic_sind IS NULL AND t.per_lic_pat IS NULL
                AND    t.per_lic_fac IS NULL  AND t.per_goce_fis IS NULL
                AND    t.per_goce IS NULL AND t.per_sgoce IS NULL
            ),
            dias AS (
                -- Dias con HE disponibles para compensacion (solo pendientes de autorizar: hayhed_poraut='S')
                SELECT t.cod_empresa, t.cod_personal, t.fechamar,
                       'HE' AS tipo_dia,
                       CASE WHEN NVL(t.hayhed_poraut,'S') != 'N' AND t.horaextra_ajus IS NULL
                            THEN NVL(TO_NUMBER(TO_CHAR(t.horaextra,      'HH24'))*60
                                   + TO_NUMBER(TO_CHAR(t.horaextra,      'MI')), 0)
                            ELSE NVL(TO_NUMBER(TO_CHAR(t.horaextra_ajus, 'HH24'))*60
                                   + TO_NUMBER(TO_CHAR(t.horaextra_ajus, 'MI')), 0)
                       END AS min_he,
                       0 AS min_falta,
                       t.alerta02, t.alerta06,
                       NVL(t.descanso,'N') AS descanso,
                       t.nummarcaciones,
                       NULL AS logix_cmotivo,
                       NULL AS logix_desc_motivo,
                       NULL AS logix_dinicio,
                       NULL AS logix_dfinal
                FROM   SCA_ASISTENCIA_TAREO t
                WHERE  t.cod_empresa = p_cod_empresa
                AND    t.fechamar BETWEEN v_fec_he_ini AND v_fec_he_fin
                AND    (  (t.horaextra_ajus IS NOT NULL
                           AND (TO_NUMBER(TO_CHAR(t.horaextra_ajus,'HH24'))*60
                              + TO_NUMBER(TO_CHAR(t.horaextra_ajus,'MI'))) > 0)
                       OR (NVL(t.hayhed_poraut,'S') != 'N'
                           AND t.horaextra_ajus IS NULL
                           AND t.horaextra IS NOT NULL
                           AND (TO_NUMBER(TO_CHAR(t.horaextra,'HH24'))*60
                              + TO_NUMBER(TO_CHAR(t.horaextra,'MI'))) > 0)
                       )
                AND    NVL(t.descanso,'N') = 'N'
                AND    NVL(t.hayhed_poraut,'S') != 'N'   -- solo HE pendientes de autorizar (no comprometidas con planilla)
                AND    p_solo_ddc = 'N'   -- solo se incluye si el caller pide todos los tipos
                AND    EXISTS (
                           SELECT 1 FROM tareo_cands tc
                           WHERE  tc.cod_empresa  = t.cod_empresa
                           AND    tc.cod_personal = t.cod_personal
                       )
                UNION ALL
                -- Dias DDC: no bloqueados por LOGIX, no compensados aun
                SELECT c.cod_empresa, c.cod_personal, c.fechamar,
                       'DDC' AS tipo_dia,
                       0 AS min_he,
                       c.min_falta,
                       c.alerta02, c.alerta06,
                       c.descanso,
                       c.nummarcaciones,
                       NULL AS logix_cmotivo,
                       NULL AS logix_desc_motivo,
                       NULL AS logix_dinicio,
                       NULL AS logix_dfinal
                FROM   tareo_cands c
                WHERE  c.tipo_dia = 'DDC'
                AND    NOT EXISTS (
                           SELECT 1 FROM SCA_COMPENSACION c2
                           WHERE  c2.cod_empresa      = c.cod_empresa
                           AND    c2.cod_personal     = c.cod_personal
                           AND    c2.fechadestino     = c.fechamar
                           AND    c2.tipocompensacion = 'F'
                           AND    c2.aux1 LIKE 'D%'
                       )
                UNION ALL
                -- Dias bloqueados por LOGIX (solo informativo para la UI)
                SELECT c.cod_empresa, c.cod_personal, c.fechamar,
                       'BLOQ_LOGIX' AS tipo_dia,
                       0 AS min_he,
                       c.min_falta,
                       c.alerta02, c.alerta06,
                       c.descanso,
                       c.nummarcaciones,
                       c.logix_cmotivo,
                       c.logix_desc_motivo,
                       c.logix_dinicio,
                       c.logix_dfinal
                FROM   tareo_cands c
                WHERE  c.tipo_dia = 'BLOQ_LOGIX'
                UNION ALL
                -- Descansos obligatorios del rango (info)
                SELECT t.cod_empresa, t.cod_personal, t.fechamar,
                       'DESCANSO' AS tipo_dia,
                       0 AS min_he, 0 AS min_falta,
                       t.alerta02, t.alerta06,
                       'S' AS descanso,
                       t.nummarcaciones,
                       NULL AS logix_cmotivo,
                       NULL AS logix_desc_motivo,
                       NULL AS logix_dinicio,
                       NULL AS logix_dfinal
                FROM   SCA_ASISTENCIA_TAREO t
                WHERE  t.cod_empresa = p_cod_empresa
                AND    t.fechamar BETWEEN v_fec_ini AND v_fec_fin
                AND    NVL(t.descanso,'N') = 'S'
                AND    p_solo_ddc = 'N'   -- solo se incluye si el caller pide todos los tipos
            )
            SELECT
                d.cod_personal,
                ft.num_fotocheck,
                p.ape_paterno||' '||p.ape_materno||' '||p.nom_trabajador AS nombre_completo,
                d.fechamar,
                TO_CHAR(d.fechamar,'DD/MM/YYYY') AS fechamar_str,
                TO_CHAR(d.fechamar,'DY','NLS_DATE_LANGUAGE=SPANISH') AS dia_semana,
                d.tipo_dia,
                d.min_he,
                -- fn_min_a_hhmi es privada y no puede usarse en contexto SQL; se usa SQL inline
                CASE WHEN d.min_he > 0
                     THEN TO_CHAR(TRUNC(d.min_he/60),'FM00')||':'||TO_CHAR(MOD(d.min_he,60),'FM00')
                     ELSE NULL END AS horas_he,
                d.min_falta,
                CASE WHEN d.min_falta > 0
                     THEN TO_CHAR(TRUNC(d.min_falta/60),'FM00')||':'||TO_CHAR(MOD(d.min_falta,60),'FM00')
                     ELSE NULL END AS horas_falta,
                d.alerta02,
                d.alerta06,
                d.descanso,
                d.nummarcaciones,
                -- Indica si ya tiene comp DDC aplicada
                CASE WHEN EXISTS (
                         SELECT 1 FROM SCA_COMPENSACION c2
                         WHERE  c2.cod_empresa      = d.cod_empresa
                         AND    c2.cod_personal     = d.cod_personal
                         AND    c2.fechadestino     = d.fechamar
                         AND    c2.tipocompensacion = 'F'
                         AND    c2.aux1 LIKE 'D%'
                     ) THEN 'S' ELSE 'N' END AS ya_compensado,
                -- Evento LOGIX bloqueante (solo para tipo_dia='BLOQ_LOGIX', NULL en el resto)
                d.logix_cmotivo,
                d.logix_dinicio,
                d.logix_dfinal,
                d.logix_desc_motivo,
                -- Descripcion textual de alerta06 (util para filas HE con p_solo_ddc='N')
                CASE d.alerta06
                    WHEN 'EN' THEN 'Normal (HE dentro de razonabilidad)'
                    WHEN 'EE' THEN 'Excede razonabilidad'
                    WHEN 'EC' THEN 'HE compensadas/consumidas'
                    ELSE NULL
                END AS desc_alerta06
            FROM dias d
            JOIN PLA_PERSONAL p
                 ON  p.cod_empresa  = d.cod_empresa
                 AND p.cod_personal = d.cod_personal
            LEFT JOIN ft
                 ON  ft.cod_empresa  = d.cod_empresa
                 AND ft.cod_personal = d.cod_personal
            WHERE (v_nombre IS NULL
                   OR UPPER(p.ape_paterno||' '||p.ape_materno||' '||p.nom_trabajador)
                      LIKE v_nombre)
            ORDER BY d.cod_personal, d.fechamar, d.tipo_dia;
    END LISTAR_DDC_RANGO;

    -- =========================================================================
    -- CALCULAR_DDC  (solo lectura, simula distribucion)
    -- =========================================================================
    PROCEDURE CALCULAR_DDC(
        p_cod_empresa      IN VARCHAR2,
        p_fecha_inicio     IN VARCHAR2,
        p_fecha_fin        IN VARCHAR2,
        p_lista_personal   IN VARCHAR2,
        p_fecha_he_inicio  IN VARCHAR2 DEFAULT NULL,
        p_fecha_he_fin     IN VARCHAR2 DEFAULT NULL,
        cv_resultado       OUT SYS_REFCURSOR
    ) AS
        v_fec_ini    DATE := TO_DATE(p_fecha_inicio, 'dd/MM/yyyy');
        v_fec_fin    DATE := TO_DATE(p_fecha_fin,    'dd/MM/yyyy');
        -- Rango independiente para buscar dias con HE disponibles.
        -- NULL = usa el mismo rango que los candidatos DDC.
        v_fec_he_ini DATE := NVL(TO_DATE(p_fecha_he_inicio, 'dd/MM/yyyy'),
                                 TO_DATE(p_fecha_inicio,    'dd/MM/yyyy'));
        v_fec_he_fin DATE := NVL(TO_DATE(p_fecha_he_fin,    'dd/MM/yyyy'),
                                 TO_DATE(p_fecha_fin,       'dd/MM/yyyy'));
        v_dias_he   t_lista;
        v_dias_ddc  t_lista;
        v_i_cur     PLS_INTEGER;
        v_tomar     NUMBER;
        v_he_asig   NUMBER;
        v_falt_rest NUMBER;
        v_total_he  NUMBER;
        v_id_sim    NUMBER;
        -- Variables para pre-calcular hhmi antes de INSERT (fn privada no usable en SQL)
        v_estado    VARCHAR2(30);
        v_he_post_r NUMBER;
        v_hft       VARCHAR2(10);
        v_hha       VARCHAR2(10);
        v_hfr       VARCHAR2(10);
        v_motivo_ins VARCHAR2(200);
        v_fdc_str   VARCHAR2(10);
        v_dia_s     VARCHAR2(5);
    BEGIN
        -- ID de simulacion negativo para no colisionar con registros reales
        SELECT -ABS(id_comp_seq.NEXTVAL) INTO v_id_sim FROM DUAL;

        -- Limpiar simulaciones previas de esta sesion
        DELETE FROM SCA_TMP_DDC_RES WHERE id_evento < 0;

        FOR e IN (
            SELECT p.cod_empresa, p.cod_personal,
                   p.ape_paterno||' '||p.ape_materno||' '||p.nom_trabajador AS nombre_completo
            FROM   PLA_PERSONAL p
            WHERE  p.cod_empresa = p_cod_empresa
            AND    INSTR(','||p_lista_personal||',', ','||p.cod_personal||',') > 0
            ORDER  BY p.cod_personal
        ) LOOP
            -- Cargar dias HE y DDC en memoria
            prv_cargar_he (p_cod_empresa, e.cod_personal, v_fec_he_ini, v_fec_he_fin, v_dias_he);
            prv_cargar_ddc(p_cod_empresa, e.cod_personal, v_fec_ini,    v_fec_fin,    v_dias_ddc);

            -- Total HE disponibles en el rango
            v_total_he := 0;
            FOR k IN 1..v_dias_he.COUNT LOOP
                v_total_he := v_total_he + v_dias_he(k).min_valor;
            END LOOP;

            IF v_dias_ddc.COUNT = 0 THEN
                CONTINUE; -- Este empleado no tiene DDC en el rango
            END IF;

            v_i_cur := 1;

            -- Simular distribucion: DDC en orden cronologico
            FOR j IN 1..v_dias_ddc.COUNT LOOP
                v_falt_rest := v_dias_ddc(j).min_valor;
                v_he_asig   := 0;

                -- Avanzar desde el primer HE disponible
                WHILE v_falt_rest > 0 AND v_i_cur <= v_dias_he.COUNT LOOP
                    -- Saltar HEs agotadas en simulacion
                    WHILE v_i_cur <= v_dias_he.COUNT
                      AND v_dias_he(v_i_cur).min_valor = 0 LOOP
                        v_i_cur := v_i_cur + 1;
                    END LOOP;
                    EXIT WHEN v_i_cur > v_dias_he.COUNT;

                    v_tomar := LEAST(v_dias_he(v_i_cur).min_valor, v_falt_rest);
                    IF v_tomar > 0 THEN
                        v_dias_he(v_i_cur).min_valor := v_dias_he(v_i_cur).min_valor - v_tomar;
                        v_falt_rest := v_falt_rest - v_tomar;
                        v_he_asig   := v_he_asig + v_tomar;
                    END IF;

                    -- Si este dia HE se agoto, avanzar
                    IF v_dias_he(v_i_cur).min_valor = 0 THEN
                        v_i_cur := v_i_cur + 1;
                    END IF;
                END LOOP;

                -- Determinar estado
                IF v_total_he = 0 THEN
                    v_estado := 'SIN_HE';
                ELSIF v_falt_rest > 0 THEN
                    v_estado := 'PARCIAL';
                ELSE
                    -- Advertencia si algun dia HE tiene sobrante entre 1-44 min
                    -- (SP_SCA_REDONDEAR_TAREO_HE lo llevaria a 0 al aplicar)
                    v_estado := 'OK';
                    FOR k IN 1..v_dias_he.COUNT LOOP
                        v_he_post_r := fn_he_post_round(v_dias_he(k).min_valor, 0);
                        IF v_dias_he(k).min_valor > 0 AND v_he_post_r = 0 THEN
                            v_estado := 'ADVERTENCIA_REDONDEO';
                            EXIT;
                        END IF;
                    END LOOP;
                END IF;

                -- Pre-calcular hhmi: fn_min_a_hhmi es privada, no usable en SQL VALUES
                v_hft        := fn_min_a_hhmi(v_dias_ddc(j).min_valor);
                v_hha        := fn_min_a_hhmi(v_he_asig);
                v_hfr        := fn_min_a_hhmi(v_falt_rest);
                v_motivo_ins := TO_CHAR(v_total_he);  -- minutos numericos; el cursor los formatea
                v_fdc_str    := TO_CHAR(v_dias_ddc(j).fechamar,'DD/MM/YYYY');
                v_dia_s      := TO_CHAR(v_dias_ddc(j).fechamar,'DY','NLS_DATE_LANGUAGE=SPANISH');

                INSERT INTO SCA_TMP_DDC_RES (
                    id_evento, cod_personal, nombre_completo,
                    fecha_ddc, fecha_ddc_str, dia_semana,
                    min_falta_total,      horas_falta_total,
                    min_he_asignadas,     horas_he_asignadas,
                    min_falta_restante,   horas_falta_restante,
                    estado, motivo
                ) VALUES (
                    v_id_sim, e.cod_personal, e.nombre_completo,
                    v_dias_ddc(j).fechamar, v_fdc_str, v_dia_s,
                    v_dias_ddc(j).min_valor, v_hft,
                    v_he_asig,              v_hha,
                    v_falt_rest,            v_hfr,
                    v_estado, v_motivo_ins
                );
            END LOOP;
        END LOOP;

        OPEN cv_resultado FOR
            SELECT r.cod_personal, r.nombre_completo,
                   r.fecha_ddc, r.fecha_ddc_str, r.dia_semana,
                   r.min_falta_total,       r.horas_falta_total,
                   r.min_he_asignadas,      r.horas_he_asignadas,
                   r.min_falta_restante,    r.horas_falta_restante,
                   TO_NUMBER(r.motivo)   AS total_he_rango_sim,
                   LPAD(TRUNC(TO_NUMBER(NVL(r.motivo,'0'))/60),2,'0')||':'
                   ||LPAD(MOD(TO_NUMBER(NVL(r.motivo,'0')),60),2,'0')
                                        AS horas_total_he_rango_sim,
                   r.estado
            FROM   SCA_TMP_DDC_RES r
            WHERE  r.id_evento = v_id_sim
            ORDER  BY r.cod_personal, r.fecha_ddc;
        -- NOTA: NO se elimina aqui. El DELETE en OPEN FOR + DELETE mismo tx = cursor vacio.
        -- La limpieza de simulaciones anteriores (id < 0) se hace al inicio del siguiente CALCULAR_DDC.
    END CALCULAR_DDC;

    -- =========================================================================
    -- REGISTRAR_DDC_MASIVO
    -- =========================================================================
    PROCEDURE REGISTRAR_DDC_MASIVO(
        p_cod_empresa      IN VARCHAR2,
        p_fecha_inicio     IN VARCHAR2,
        p_fecha_fin        IN VARCHAR2,
        p_lista_personal   IN VARCHAR2,
        p_lista_ddc_fechas IN VARCHAR2 DEFAULT NULL,
        p_fecha_he_inicio  IN VARCHAR2 DEFAULT NULL,
        p_fecha_he_fin     IN VARCHAR2 DEFAULT NULL,
        cv_resultado       OUT SYS_REFCURSOR
    ) AS
        v_fec_ini    DATE := TO_DATE(p_fecha_inicio, 'dd/MM/yyyy');
        v_fec_fin    DATE := TO_DATE(p_fecha_fin,    'dd/MM/yyyy');
        -- Rango independiente para buscar dias con HE disponibles.
        -- NULL = usa el mismo rango que los candidatos DDC.
        v_fec_he_ini DATE := NVL(TO_DATE(p_fecha_he_inicio, 'dd/MM/yyyy'),
                                 TO_DATE(p_fecha_inicio,    'dd/MM/yyyy'));
        v_fec_he_fin DATE := NVL(TO_DATE(p_fecha_he_fin,    'dd/MM/yyyy'),
                                 TO_DATE(p_fecha_fin,       'dd/MM/yyyy'));
        v_dias_he   t_lista;
        v_dias_ddc  t_lista;
        v_id_evento NUMBER;
        v_i_cur     PLS_INTEGER;
        v_tomar     NUMBER;
        v_he_asig   NUMBER;
        v_falt_rest NUMBER;
        v_total_he  NUMBER;
        v_estado    VARCHAR2(30);
        v_motivo    VARCHAR2(500);
        v_id_comp   NUMBER;
        -- Variables para pre-calcular hhmi antes de INSERT (fn privada no usable en SQL)
        v_hft       VARCHAR2(10);
        v_hha       VARCHAR2(10);
        v_hfr       VARCHAR2(10);
        v_fdc_str   VARCHAR2(10);
        v_dia_s     VARCHAR2(5);

        -- Filtra si p_lista_ddc_fechas contiene la clave 'cod:dd/MM/yyyy'
        FUNCTION fn_ddc_permitida(
            p_cod IN VARCHAR2,
            p_fec IN DATE
        ) RETURN BOOLEAN IS
            v_clave VARCHAR2(40);
        BEGIN
            IF p_lista_ddc_fechas IS NULL THEN RETURN TRUE; END IF;
            v_clave := p_cod || ':' || TO_CHAR(p_fec, 'DD/MM/YYYY');
            RETURN INSTR(',' || p_lista_ddc_fechas || ',', ',' || v_clave || ',') > 0;
        END fn_ddc_permitida;
    BEGIN
        -- ID unico para este evento
        SELECT id_comp_seq.NEXTVAL INTO v_id_evento FROM DUAL;

        -- Limpiar GGT de este evento
        DELETE FROM SCA_TMP_DDC_RES WHERE id_evento = v_id_evento;

        FOR e IN (
            SELECT p.cod_empresa, p.cod_personal,
                   p.ape_paterno||' '||p.ape_materno||' '||p.nom_trabajador AS nombre_completo
            FROM   PLA_PERSONAL p
            WHERE  p.cod_empresa = p_cod_empresa
            AND    INSTR(','||p_lista_personal||',', ','||p.cod_personal||',') > 0
            ORDER  BY p.cod_personal
        ) LOOP
            BEGIN
                -- Cargar listas en memoria para este empleado
                prv_cargar_he (p_cod_empresa, e.cod_personal, v_fec_he_ini, v_fec_he_fin, v_dias_he);
                prv_cargar_ddc(p_cod_empresa, e.cod_personal, v_fec_ini,    v_fec_fin,    v_dias_ddc);

                -- Total HE disponibles
                v_total_he := 0;
                FOR k IN 1..v_dias_he.COUNT LOOP
                    v_total_he := v_total_he + v_dias_he(k).min_valor;
                END LOOP;

                IF v_dias_ddc.COUNT = 0 THEN
                    CONTINUE; -- Sin DDC para este empleado
                END IF;

                v_i_cur := 1;

                -- Distribuir HE a DDC en orden cronologico
                FOR j IN 1..v_dias_ddc.COUNT LOOP
                    -- Si hay lista de fechas DDC especificas, saltar los no solicitados
                    IF NOT fn_ddc_permitida(e.cod_personal, v_dias_ddc(j).fechamar) THEN
                        CONTINUE;
                    END IF;
                    v_falt_rest := v_dias_ddc(j).min_valor;
                    v_he_asig   := 0;
                    v_estado    := 'ERR';
                    v_motivo    := NULL;

                    BEGIN
                        IF v_total_he = 0 THEN
                            v_estado := 'SIN_HE';
                            v_motivo := 'Sin horaextra_ajus en rango HE '
                                        ||NVL(p_fecha_he_inicio, p_fecha_inicio)
                                        ||' a '||NVL(p_fecha_he_fin, p_fecha_fin);
                        ELSE
                            -- Consumir HE disponibles para este DDC
                            WHILE v_falt_rest > 0 AND v_i_cur <= v_dias_he.COUNT LOOP
                                -- Saltar dias HE agotados
                                WHILE v_i_cur <= v_dias_he.COUNT
                                  AND v_dias_he(v_i_cur).min_valor = 0 LOOP
                                    v_i_cur := v_i_cur + 1;
                                END LOOP;
                                EXIT WHEN v_i_cur > v_dias_he.COUNT;

                                v_tomar := LEAST(v_dias_he(v_i_cur).min_valor, v_falt_rest);

                                IF v_tomar > 0 THEN
                                    -- Aplicar efectos en tareo origen
                                    prv_aplicar_origen_E(
                                        p_cod_empresa, e.cod_personal,
                                        v_dias_he(v_i_cur).fechamar,
                                        v_tomar
                                    );

                                    -- Aplicar efectos en tareo destino (DDC)
                                    prv_aplicar_destino_ddc(
                                        p_cod_empresa, e.cod_personal,
                                        v_dias_ddc(j).fechamar,
                                        v_tomar
                                    );

                                    -- Insertar registro de compensacion
                                    INSERT INTO SCA_COMPENSACION (
                                        id_compen, cod_empresa, cod_personal,
                                        fechadestino, fechaorigen,
                                        tipoorigen, tipocompensacion,
                                        tiempo, aux1
                                    ) VALUES (
                                        id_comp_seq.NEXTVAL,
                                        p_cod_empresa, e.cod_personal,
                                        v_dias_ddc(j).fechamar,
                                        v_dias_he(v_i_cur).fechamar,
                                        'E', 'F',
                                        v_tomar,
                                        'D'||TO_CHAR(v_id_evento)
                                    ) RETURNING id_compen INTO v_id_comp;

                                    -- Re-sincronizar memoria desde BD (SP_SCA_REDONDEAR_TAREO_HE
                                    -- pudo bajar la HE mas de v_tomar por redondeo)
                                    v_dias_he(v_i_cur).min_valor :=
                                        fn_he_actual(p_cod_empresa, e.cod_personal,
                                                     v_dias_he(v_i_cur).fechamar);
                                    v_falt_rest := v_falt_rest - v_tomar;
                                    v_he_asig   := v_he_asig + v_tomar;
                                END IF;

                                -- Avanzar si dia HE agotado
                                IF v_dias_he(v_i_cur).min_valor = 0 THEN
                                    v_i_cur := v_i_cur + 1;
                                END IF;
                            END LOOP;

                            -- Determinar estado final del DDC
                            IF v_he_asig = 0 THEN
                                v_estado := 'SIN_HE';
                                v_motivo := 'HE del rango ya agotadas por DDC anteriores';
                            ELSIF v_falt_rest > 0 THEN
                                v_estado := 'PARCIAL';
                                v_motivo := 'Compensado '||fn_min_a_hhmi(v_he_asig)
                                            ||' de '||fn_min_a_hhmi(v_dias_ddc(j).min_valor)
                                            ||'. Faltan '||fn_min_a_hhmi(v_falt_rest);
                            ELSE
                                v_estado := 'OK';
                                v_motivo := 'Compensacion completa: '
                                            ||fn_min_a_hhmi(v_he_asig)||' HE -> DDC '
                                            ||TO_CHAR(v_dias_ddc(j).fechamar,'DD/MM/YYYY');
                            END IF;
                        END IF;

                    EXCEPTION
                        WHEN OTHERS THEN
                            v_estado := 'ERR';
                            v_motivo := SUBSTR(SQLERRM, 1, 490);
                    END;

                    -- Pre-calcular hhmi: fn_min_a_hhmi es privada, no usable en SQL VALUES
                    v_hft     := fn_min_a_hhmi(v_dias_ddc(j).min_valor);
                    v_hha     := fn_min_a_hhmi(v_he_asig);
                    v_hfr     := fn_min_a_hhmi(v_falt_rest);
                    v_fdc_str := TO_CHAR(v_dias_ddc(j).fechamar,'DD/MM/YYYY');
                    v_dia_s   := TO_CHAR(v_dias_ddc(j).fechamar,'DY','NLS_DATE_LANGUAGE=SPANISH');

                    -- Guardar resultado DDC en GGT
                    INSERT INTO SCA_TMP_DDC_RES (
                        id_evento, cod_personal, nombre_completo,
                        fecha_ddc, fecha_ddc_str, dia_semana,
                        min_falta_total,    horas_falta_total,
                        min_he_asignadas,   horas_he_asignadas,
                        min_falta_restante, horas_falta_restante,
                        estado, motivo
                    ) VALUES (
                        v_id_evento, e.cod_personal, e.nombre_completo,
                        v_dias_ddc(j).fechamar, v_fdc_str, v_dia_s,
                        v_dias_ddc(j).min_valor, v_hft,
                        v_he_asig,               v_hha,
                        v_falt_rest,             v_hfr,
                        v_estado, v_motivo
                    );
                END LOOP; -- cada DDC

            EXCEPTION
                WHEN OTHERS THEN
                    -- Error general del empleado: registrar en GGT si habia DDC
                    NULL; -- Los errores por DDC individual ya se capturaron arriba
            END;
        END LOOP; -- cada empleado

        -- Devolver resultados del evento
        OPEN cv_resultado FOR
            SELECT r.*,
                   v_id_evento AS id_evento_out
            FROM   SCA_TMP_DDC_RES r
            WHERE  r.id_evento = v_id_evento
            ORDER  BY r.cod_personal, r.fecha_ddc;
    END REGISTRAR_DDC_MASIVO;

    -- =========================================================================
    -- APLICAR_DIA_DDC  (integracion PASO 15 / reproceso)
    -- =========================================================================
    PROCEDURE APLICAR_DIA_DDC(
        p_cod_empresa        IN VARCHAR2,
        p_cod_personal       IN VARCHAR2,
        p_fecha              IN VARCHAR2,
        p_eliminar_no_cuadra IN VARCHAR2 DEFAULT 'S',
        cv_resultado         OUT SYS_REFCURSOR
    ) AS
        v_fec        DATE := TO_DATE(p_fecha, 'dd/MM/yyyy');
        v_disp_he    NUMBER;
        v_disp_falt  NUMBER;
        v_apl_des    NUMBER := 0;
        v_apl_ori    NUMBER := 0;
        v_eliminadas NUMBER := 0;
        v_errores    NUMBER := 0;

        -- Compensaciones DDC donde este dia es DESTINO (DDC recibe HE)
        CURSOR c_des IS
            SELECT c.id_compen, c.cod_empresa, c.cod_personal,
                   c.fechadestino, c.fechaorigen, c.tiempo
            FROM   SCA_COMPENSACION c
            WHERE  c.cod_empresa      = p_cod_empresa
            AND    c.cod_personal     = p_cod_personal
            AND    c.tipoorigen       = 'E'
            AND    c.tipocompensacion = 'F'
            AND    c.aux1 LIKE 'D%'
            AND    c.fechadestino     = v_fec;

        -- Compensaciones DDC donde este dia es ORIGEN (dia HE)
        CURSOR c_ori IS
            SELECT c.id_compen, c.cod_empresa, c.cod_personal,
                   c.fechadestino, c.fechaorigen, c.tiempo
            FROM   SCA_COMPENSACION c
            WHERE  c.cod_empresa      = p_cod_empresa
            AND    c.cod_personal     = p_cod_personal
            AND    c.tipoorigen       = 'E'
            AND    c.tipocompensacion = 'F'
            AND    c.aux1 LIKE 'D%'
            AND    c.fechaorigen      = v_fec;
    BEGIN
        -- CURSOR 1: este dia es DDC (destino tipo='F')
        FOR r IN c_des LOOP
            BEGIN
                v_disp_falt := fn_falta_actual(r.cod_empresa, r.cod_personal, r.fechadestino);
                -- Para tipo 'F' (parcial): validacion v_disp >= tiempo
                IF v_disp_falt >= r.tiempo THEN
                    prv_aplicar_destino_ddc(r.cod_empresa, r.cod_personal,
                                            r.fechadestino, r.tiempo);
                    v_apl_des := v_apl_des + 1;
                ELSE
                    IF NVL(p_eliminar_no_cuadra,'S') = 'S' THEN
                        -- Revertir el origen tambien
                        prv_revertir_origen_E(r.cod_empresa, r.cod_personal,
                                              r.fechaorigen, r.tiempo);
                        DELETE SCA_COMPENSACION WHERE id_compen = r.id_compen;
                        v_eliminadas := v_eliminadas + 1;
                    END IF;
                END IF;
            EXCEPTION WHEN OTHERS THEN v_errores := v_errores + 1;
            END;
        END LOOP;

        -- CURSOR 2: este dia es origen de HE
        FOR r IN c_ori LOOP
            BEGIN
                v_disp_he := fn_he_actual(r.cod_empresa, r.cod_personal, r.fechaorigen);
                IF v_disp_he >= r.tiempo THEN
                    prv_aplicar_origen_E(r.cod_empresa, r.cod_personal,
                                         r.fechaorigen, r.tiempo);
                    v_apl_ori := v_apl_ori + 1;
                ELSE
                    IF NVL(p_eliminar_no_cuadra,'S') = 'S' THEN
                        -- Revertir el destino tambien
                        prv_revertir_destino_ddc(r.cod_empresa, r.cod_personal,
                                                  r.fechadestino, r.tiempo);
                        DELETE SCA_COMPENSACION WHERE id_compen = r.id_compen;
                        v_eliminadas := v_eliminadas + 1;
                    END IF;
                END IF;
            EXCEPTION WHEN OTHERS THEN v_errores := v_errores + 1;
            END;
        END LOOP;

        OPEN cv_resultado FOR
            SELECT p_fecha     AS fecha,
                   p_cod_empresa   AS cod_empresa,
                   p_cod_personal  AS cod_personal,
                   v_apl_des    AS aplicadas_destino,
                   v_apl_ori    AS aplicadas_origen,
                   v_eliminadas AS eliminadas,
                   v_errores    AS errores
            FROM   DUAL;
    END APLICAR_DIA_DDC;

    -- =========================================================================
    -- CONSULTAR_RANGO_DDC
    -- =========================================================================
    PROCEDURE CONSULTAR_RANGO_DDC(
        p_cod_empresa    IN VARCHAR2 DEFAULT NULL,
        p_cod_personal   IN VARCHAR2 DEFAULT NULL,
        p_fecha_inicio   IN VARCHAR2,
        p_fecha_fin      IN VARCHAR2,
        cv_resultado     OUT SYS_REFCURSOR
    ) AS
        v_fec_ini DATE := TO_DATE(p_fecha_inicio, 'dd/MM/yyyy');
        v_fec_fin DATE := TO_DATE(p_fecha_fin,    'dd/MM/yyyy');
        v_emp     VARCHAR2(20) := NVL(p_cod_empresa,'%');
        v_per     VARCHAR2(20) := NVL(p_cod_personal,'%');
    BEGIN
        OPEN cv_resultado FOR
            SELECT
                c.id_compen,
                c.cod_empresa,
                c.cod_personal,
                p.ape_paterno||' '||p.ape_materno||' '||p.nom_trabajador AS nombre_completo,
                c.fechaorigen,
                TO_CHAR(c.fechaorigen,'DD/MM/YYYY')  AS fechaorigen_str,
                c.fechadestino,
                TO_CHAR(c.fechadestino,'DD/MM/YYYY') AS fechadestino_str,
                c.tipoorigen,
                c.tipocompensacion,
                c.tiempo                             AS tiempo_min,
                SUBSTR('00'||TO_CHAR(TRUNC(c.tiempo/60)),-2,2)
                  ||':'||SUBSTR('00'||TO_CHAR(MOD(c.tiempo,60)),-2,2) AS tiempo_hhmi,
                c.aux1                               AS evento,
                -- Alertas tareo origen (dia HE)
                tori.alerta06  AS ori_alerta06,      -- EC=HE consumidas, EE=HE existentes
                TO_CHAR(NVL(tori.horaextra_ajus, c_BASE_DATE),'HH24:MI') AS ori_he_actual,
                -- Alertas tareo destino (dia DDC)
                tdes.alerta02  AS dest_alerta02,     -- FC=falta compensada, FT=falta total
                TO_CHAR(NVL(tdes.horas_falta, c_BASE_DATE),'HH24:MI')    AS dest_falta_actual,
                TO_CHAR(NVL(tdes.horaefectiva, c_BASE_DATE),'HH24:MI')   AS dest_hefec_actual
            FROM   SCA_COMPENSACION c
            JOIN   PLA_PERSONAL p
                   ON  p.cod_empresa  = c.cod_empresa
                   AND p.cod_personal = c.cod_personal
            LEFT JOIN SCA_ASISTENCIA_TAREO tori
                   ON  tori.cod_empresa  = c.cod_empresa
                   AND tori.cod_personal = c.cod_personal
                   AND tori.fechamar     = c.fechaorigen
            LEFT JOIN SCA_ASISTENCIA_TAREO tdes
                   ON  tdes.cod_empresa  = c.cod_empresa
                   AND tdes.cod_personal = c.cod_personal
                   AND tdes.fechamar     = c.fechadestino
            WHERE  c.tipoorigen       = 'E'
            AND    c.tipocompensacion = 'F'
            AND    c.aux1 LIKE 'D%'
            AND    c.cod_empresa  LIKE v_emp
            AND    c.cod_personal LIKE v_per
            AND    c.fechadestino BETWEEN v_fec_ini AND v_fec_fin
            ORDER  BY c.fechadestino DESC, c.cod_personal, c.fechaorigen;
    END CONSULTAR_RANGO_DDC;

    -- =========================================================================
    -- CONSULTAR_EVENTO_DDC
    -- =========================================================================
    PROCEDURE CONSULTAR_EVENTO_DDC(
        p_id_evento  IN NUMBER,
        cv_resultado OUT SYS_REFCURSOR
    ) AS
    BEGIN
        OPEN cv_resultado FOR
            SELECT
                c.id_compen,
                c.cod_empresa,
                c.cod_personal,
                p.ape_paterno||' '||p.ape_materno||' '||p.nom_trabajador AS nombre_completo,
                TO_CHAR(c.fechaorigen,'DD/MM/YYYY')  AS fechaorigen_str,
                TO_CHAR(c.fechadestino,'DD/MM/YYYY') AS fechadestino_str,
                c.tipocompensacion,
                c.tiempo AS tiempo_min,
                SUBSTR('00'||TO_CHAR(TRUNC(c.tiempo/60)),-2,2)
                  ||':'||SUBSTR('00'||TO_CHAR(MOD(c.tiempo,60)),-2,2) AS tiempo_hhmi,
                -- Estado actual tareo
                tori.alerta06  AS ori_alerta06,
                TO_CHAR(NVL(tori.horaextra_ajus, c_BASE_DATE),'HH24:MI') AS ori_he_actual,
                tdes.alerta02  AS dest_alerta02,
                TO_CHAR(NVL(tdes.horas_falta, c_BASE_DATE),'HH24:MI')    AS dest_falta_actual,
                TO_CHAR(NVL(tdes.horaefectiva, c_BASE_DATE),'HH24:MI')   AS dest_hefec_actual
            FROM   SCA_COMPENSACION c
            JOIN   PLA_PERSONAL p
                   ON  p.cod_empresa  = c.cod_empresa
                   AND p.cod_personal = c.cod_personal
            LEFT JOIN SCA_ASISTENCIA_TAREO tori
                   ON  tori.cod_empresa  = c.cod_empresa
                   AND tori.cod_personal = c.cod_personal
                   AND tori.fechamar     = c.fechaorigen
            LEFT JOIN SCA_ASISTENCIA_TAREO tdes
                   ON  tdes.cod_empresa  = c.cod_empresa
                   AND tdes.cod_personal = c.cod_personal
                   AND tdes.fechamar     = c.fechadestino
            WHERE  c.tipoorigen       = 'E'
            AND    c.tipocompensacion = 'F'
            AND    c.aux1             = 'D'||TO_CHAR(p_id_evento)
            ORDER  BY c.cod_personal, c.fechadestino, c.fechaorigen;
    END CONSULTAR_EVENTO_DDC;

    -- =========================================================================
    -- CONSULTAR_COMP_DDC
    -- =========================================================================
    PROCEDURE CONSULTAR_COMP_DDC(
        p_id_compen  IN NUMBER,
        cv_resultado OUT SYS_REFCURSOR
    ) AS
    BEGIN
        OPEN cv_resultado FOR
            SELECT
                c.id_compen,
                c.cod_empresa,
                c.cod_personal,
                p.ape_paterno||' '||p.ape_materno||' '||p.nom_trabajador AS nombre_completo,
                TO_CHAR(c.fechaorigen,'DD/MM/YYYY')  AS fechaorigen_str,
                TO_CHAR(c.fechadestino,'DD/MM/YYYY') AS fechadestino_str,
                c.tipocompensacion,
                c.tiempo AS tiempo_min,
                SUBSTR('00'||TO_CHAR(TRUNC(c.tiempo/60)),-2,2)
                  ||':'||SUBSTR('00'||TO_CHAR(MOD(c.tiempo,60)),-2,2) AS tiempo_hhmi,
                -- ID del evento al que pertenece (util para mostrar en UI)
                TO_NUMBER(SUBSTR(c.aux1, 2)) AS id_evento,
                -- Estado actual tareo
                tori.alerta06  AS ori_alerta06,
                TO_CHAR(NVL(tori.horaextra_ajus, c_BASE_DATE),'HH24:MI') AS ori_he_actual,
                tdes.alerta02  AS dest_alerta02,
                TO_CHAR(NVL(tdes.horas_falta, c_BASE_DATE),'HH24:MI')    AS dest_falta_actual,
                TO_CHAR(NVL(tdes.horaefectiva, c_BASE_DATE),'HH24:MI')   AS dest_hefec_actual
            FROM   SCA_COMPENSACION c
            JOIN   PLA_PERSONAL p
                   ON  p.cod_empresa  = c.cod_empresa
                   AND p.cod_personal = c.cod_personal
            LEFT JOIN SCA_ASISTENCIA_TAREO tori
                   ON  tori.cod_empresa  = c.cod_empresa
                   AND tori.cod_personal = c.cod_personal
                   AND tori.fechamar     = c.fechaorigen
            LEFT JOIN SCA_ASISTENCIA_TAREO tdes
                   ON  tdes.cod_empresa  = c.cod_empresa
                   AND tdes.cod_personal = c.cod_personal
                   AND tdes.fechamar     = c.fechadestino
            WHERE  c.id_compen        = p_id_compen
            AND    c.tipoorigen       = 'E'
            AND    c.tipocompensacion = 'F'
            AND    c.aux1 LIKE 'D%';
    END CONSULTAR_COMP_DDC;

    -- =========================================================================
    -- LISTAR_HE_PERSONAL
    -- Devuelve solo los dias con HE de un empleado especifico en el rango.
    -- Llamar al hacer click en un empleado en la vista DDC (p_solo_ddc='S').
    -- Mismas columnas que LISTAR_DDC_RANGO para compatibilidad con el grid .NET.
    -- =========================================================================
    PROCEDURE LISTAR_HE_PERSONAL(
        p_cod_empresa      IN VARCHAR2,
        p_cod_personal     IN VARCHAR2,
        p_fecha_he_inicio  IN VARCHAR2,
        p_fecha_he_fin     IN VARCHAR2,
        cv_resultado       OUT SYS_REFCURSOR
    ) AS
        v_fec_he_ini DATE := TO_DATE(p_fecha_he_inicio, 'dd/MM/yyyy');
        v_fec_he_fin DATE := TO_DATE(p_fecha_he_fin,    'dd/MM/yyyy');
    BEGIN
        OPEN cv_resultado FOR
            SELECT
                t.cod_personal,
                ft.num_fotocheck,
                p.ape_paterno||' '||p.ape_materno||' '||p.nom_trabajador AS nombre_completo,
                t.fechamar,
                TO_CHAR(t.fechamar,'DD/MM/YYYY')                          AS fechamar_str,
                TO_CHAR(t.fechamar,'DY','NLS_DATE_LANGUAGE=SPANISH')      AS dia_semana,
                'HE'                                                      AS tipo_dia,
                CASE WHEN NVL(t.hayhed_poraut,'S') != 'N' AND t.horaextra_ajus IS NULL
                     THEN NVL(TO_NUMBER(TO_CHAR(t.horaextra,      'HH24'))*60
                            + TO_NUMBER(TO_CHAR(t.horaextra,      'MI')), 0)
                     ELSE NVL(TO_NUMBER(TO_CHAR(t.horaextra_ajus, 'HH24'))*60
                            + TO_NUMBER(TO_CHAR(t.horaextra_ajus, 'MI')), 0)
                END AS min_he,
                CASE WHEN (CASE WHEN NVL(t.hayhed_poraut,'S') != 'N' AND t.horaextra_ajus IS NULL
                                THEN NVL(TO_NUMBER(TO_CHAR(t.horaextra,      'HH24'))*60
                                       + TO_NUMBER(TO_CHAR(t.horaextra,      'MI')), 0)
                                ELSE NVL(TO_NUMBER(TO_CHAR(t.horaextra_ajus, 'HH24'))*60
                                       + TO_NUMBER(TO_CHAR(t.horaextra_ajus, 'MI')), 0)
                           END) > 0
                     THEN TO_CHAR(TRUNC((CASE WHEN NVL(t.hayhed_poraut,'S') != 'N' AND t.horaextra_ajus IS NULL
                                             THEN NVL(TO_NUMBER(TO_CHAR(t.horaextra,      'HH24'))*60
                                                    + TO_NUMBER(TO_CHAR(t.horaextra,      'MI')), 0)
                                             ELSE NVL(TO_NUMBER(TO_CHAR(t.horaextra_ajus, 'HH24'))*60
                                                    + TO_NUMBER(TO_CHAR(t.horaextra_ajus, 'MI')), 0)
                                        END)/60),'FM00')
                          ||':'||
                          TO_CHAR(MOD((CASE WHEN NVL(t.hayhed_poraut,'S') != 'N' AND t.horaextra_ajus IS NULL
                                           THEN NVL(TO_NUMBER(TO_CHAR(t.horaextra,      'HH24'))*60
                                                  + TO_NUMBER(TO_CHAR(t.horaextra,      'MI')), 0)
                                           ELSE NVL(TO_NUMBER(TO_CHAR(t.horaextra_ajus, 'HH24'))*60
                                                  + TO_NUMBER(TO_CHAR(t.horaextra_ajus, 'MI')), 0)
                                      END),60),'FM00')
                     ELSE NULL END                                        AS horas_he,
                0                                                         AS min_falta,
                NULL                                                      AS horas_falta,
                t.alerta02,
                t.alerta06,
                NVL(t.descanso,'N')                                       AS descanso,
                t.nummarcaciones,
                'N'                                                       AS ya_compensado,
                NULL                                                      AS logix_cmotivo,
                NULL                                                      AS logix_dinicio,
                NULL                                                      AS logix_dfinal,
                NULL                                                      AS logix_desc_motivo,
                CASE t.alerta06
                    WHEN 'EN' THEN 'Normal (HE dentro de razonabilidad)'
                    WHEN 'EE' THEN 'Excede razonabilidad'
                    WHEN 'EC' THEN 'HE compensadas/consumidas'
                    ELSE NULL
                END                                                       AS desc_alerta06
            FROM   SCA_ASISTENCIA_TAREO t
            JOIN   PLA_PERSONAL p
                   ON  p.cod_empresa  = t.cod_empresa
                   AND p.cod_personal = t.cod_personal
            LEFT JOIN (
                SELECT cod_empresa, cod_personal,
                       MAX(num_fotocheck)
                           KEEP (DENSE_RANK LAST ORDER BY id_fotocheck) AS num_fotocheck
                FROM   SCA_FOTOCHECK
                WHERE  cod_empresa  = p_cod_empresa
                AND    cod_personal = p_cod_personal
                GROUP  BY cod_empresa, cod_personal
            ) ft ON ft.cod_empresa = t.cod_empresa AND ft.cod_personal = t.cod_personal
            WHERE  t.cod_empresa  = p_cod_empresa
            AND    t.cod_personal = p_cod_personal
            AND    t.fechamar     BETWEEN v_fec_he_ini AND v_fec_he_fin
            AND    (  (t.horaextra_ajus IS NOT NULL
                       AND (TO_NUMBER(TO_CHAR(t.horaextra_ajus,'HH24'))*60
                          + TO_NUMBER(TO_CHAR(t.horaextra_ajus,'MI'))) > 0)
                   OR (NVL(t.hayhed_poraut,'S') != 'N'
                       AND t.horaextra_ajus IS NULL
                       AND t.horaextra IS NOT NULL
                       AND (TO_NUMBER(TO_CHAR(t.horaextra,'HH24'))*60
                          + TO_NUMBER(TO_CHAR(t.horaextra,'MI'))) > 0)
                   )
            AND    NVL(t.descanso,'N') = 'N'
            AND    NVL(t.hayhed_poraut,'S') != 'N'   -- solo HE pendientes de autorizar (no comprometidas con planilla)
            ORDER  BY t.fechamar;
    END LISTAR_HE_PERSONAL;

END PKG_SCA_COMP_DDC;
/
SHOW ERRORS PACKAGE BODY PKG_SCA_COMP_DDC;
