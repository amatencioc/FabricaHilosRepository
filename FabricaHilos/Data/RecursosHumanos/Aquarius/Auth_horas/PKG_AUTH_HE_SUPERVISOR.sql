-- ============================================================
-- PKG_AUTH_HE_SUPERVISOR
-- Portal de autorización de horas extras para supervisores
--
-- FLUJO:
--   1. sp_login            → valida credenciales, devuelve datos del supervisor
--   2. sp_read_empleados   → lista empleados a cargo con CC y horario vigente
--   3. sp_read_tareo_he    → tareo diario del empleado + HE + autorizaciones vigentes
--   4. sp_grabar_autorizacion → graba/revoca autorización por día
--
-- TABLAS LECTURA:
--   MAE_USUARIO, MAE_USUARIO_EMP, MAE_SUCURSAL_USUARIO
--   MAE_C_COSTOS, MAE_C_COSTOS_VERSION, MAE_C_COSTOS_USUARIO
--   PLA_PERSONAL, PLA_TIPO_PLANILLA
--   PLA_PERFIL_PLANILLA, PLA_PERFIL_ACCESO_PLANI, PLA_USUARIO_PLANILLA
--   SCA_HORARIO_PERSONAL, SCA_HORARIO_CAB
--   SCA_ASISTENCIA_TAREO, SCA_AUTORIZACION
--
-- TABLAS ESCRITURA:
--   SCA_AUTORIZACION    (INSERT/DELETE via sp_grabar_autorizacion)
--   SCA_ASISTENCIA_TAREO (UPDATE via sp_SCA_Upd_Tar_InsAut — campos _ofi y horaexofi1/2/3)
--
-- Oracle 11g / Toad 7.5 — usa TO_DATE, ROWNUM; sin DATE '...' ni FETCH FIRST
-- ============================================================

-- ============================================================
-- SPEC
-- ============================================================
CREATE OR REPLACE PACKAGE PKG_AUTH_HE_SUPERVISOR AS

    -- ------------------------------------------------------
    -- 1. LOGIN
    --    v_cod_usuario    : login del usuario
    --    v_password_hash  : contraseña ya hasheada por el cliente (.NET)
    --
    --    Retorna (1 fila o 0 si credenciales inválidas):
    --      resultado        : 'OK' | 'CREDENCIAL_INVALIDA' | 'USUARIO_BAJA'
    --      mensaje          : descripción del error (vacío si OK)
    --      cod_usuario, nom_usuario, cod_personal
    --      ind_admin        : 'S'/'N'
    --      cnt_empresas     : cuántas empresas tiene habilitadas en módulo 1002
    --      cod_empresa_unica: empresa si cnt_empresas=1, NULL si varias
    --      es_adm_alguna    : 'S' si es Adm en al menos una empresa del módulo
    -- ------------------------------------------------------
    PROCEDURE sp_login(
        v_cod_usuario   IN  VARCHAR2,
        v_password_plain IN  VARCHAR2,   -- clave en texto plano (la decodificación se hace aquí)
        cv_1            OUT SYS_REFCURSOR
    );

    -- ------------------------------------------------------
    -- 1b. LOGIN SIN CONTRASEÑA (uso interno intranet)
    --    Igual que sp_login pero sin validar password.
    --    Útil cuando el usuario ya fue autenticado por la intranet.
    --
    --    Retorna los mismos campos que sp_login.
    -- ------------------------------------------------------
    PROCEDURE sp_login_intranet(
        v_cod_usuario   IN  VARCHAR2,
        cv_1            OUT SYS_REFCURSOR
    );

    -- ------------------------------------------------------
    -- 2. EMPLEADOS A CARGO
    --    Devuelve los empleados visibles para el usuario
    --    en la empresa indicada (seguridad: empresa, sucursal,
    --    tipo planilla, centro de costo).
    --    Solo empleados activos con horario asignado.
    --
    --    Retorna:
    --      cod_personal, nombre_completo, cod_c_costos, des_c_costos
    --      cod_tipo_planilla, des_tipo_planilla, cod_sucursal
    --      horid, hordes, horcla, tip_estado
    -- ------------------------------------------------------
    PROCEDURE sp_read_empleados(
        v_cod_usuario   IN  VARCHAR2,
        v_cod_empresa   IN  VARCHAR2,
        cv_1            OUT SYS_REFCURSOR
    );

    -- ------------------------------------------------------
    -- 3. TAREO DIARIO CON HE Y AUTORIZACIONES
    --    Retorna un registro por día procesado del empleado
    --    en el rango indicado, con todas las columnas de HE
    --    y las autorizaciones vigentes para cada día.
    --    Valida que el usuario tenga acceso al empleado.
    --
    --    Campos de horas y pendientes:
    --      horaextantes, horaextantesofi, hayhea_poraut   (HEA)
    --      horaextra, horaextraofi, horaextra_ajus,
    --        horaexofi1/2/3, hayhed_poraut                (HED)
    --      horadobles, horadoblesof, hayheo_poraut        (HEO/Dobles)
    --      horabancoh                                     (Banco)
    --      tothoranocturna_of                             (Nocturnas)
    --
    --    Autorizaciones vigentes (tipos '1','2','5'):
    --      auth_hea_horas / auth_hea_obs / auth_hea_usr
    --      auth_hed_horas / auth_hed_obs / auth_hed_usr
    --      auth_heo_horas / auth_heo_obs / auth_heo_usr
    --
    --    Desautorizaciones vigentes (tipos '3','4','6'):
    --      desauth_hea_horas / desauth_hed_horas / desauth_heo_horas
    -- ------------------------------------------------------
    PROCEDURE sp_read_tareo_he(
        v_cod_usuario   IN  VARCHAR2,
        v_cod_empresa   IN  VARCHAR2,
        v_cod_personal  IN  VARCHAR2,
        v_fecha_inicio  IN  VARCHAR2,   -- 'dd/MM/yyyy'
        v_fecha_final   IN  VARCHAR2,   -- 'dd/MM/yyyy'
        cv_1            OUT SYS_REFCURSOR
    );

    -- ------------------------------------------------------
    -- 4. GRABAR AUTORIZACIÓN / DESAUTORIZACIÓN
    --    Tipos de autorización:
    --      '1' = Autorizar HEA (horas antes de entrada)
    --      '2' = Autorizar HED (horas después de salida)
    --      '5' = Autorizar HEO (horas dobles / descanso trabajado)
    --      '3' = Desautorizar HEA  → si existe auth '1': la elimina
    --      '4' = Desautorizar HED  → si existe auth '2': la elimina
    --      '6' = Desautorizar HEO  → si existe auth '5': la elimina
    --
    --    v_valor: 'HH:MI'  ej: '02:30'
    --      - Para auth  (1/2/5): horas a autorizar
    --      - Para desauth (3/4/6): horas actuales del tareo
    --        (necesario para la validación del PASO 14 del proceso)
    --
    --    Efecto en tareo (inmediato, vía sp_SCA_Upd_Tar_InsAut):
    --      auth  1/2  → horaextantesofi/horaextraofi = v_valor;
    --                    recalcula horaextra_ajus, horaexofi1/2/3 (tippagohe='1')
    --                    o horabancoh (tippagohe='2')
    --      auth  5    → horadoblesof = v_valor
    --      desauth 3/4 → horaextantesofi/horaextraofi = NULL;
    --                    horaextra_ajus/horaexofi1/2/3/alerta06 = NULL
    --      desauth 6  → horadoblesof = NULL
    --    Sin este paso el Interface no llevaría las HE a planilla.
    --
    --    cv_1: resultado 'OK'/'ERROR', mensaje
    -- ------------------------------------------------------
    PROCEDURE sp_grabar_autorizacion(
        v_cod_usuario   IN  VARCHAR2,
        v_cod_empresa   IN  VARCHAR2,
        v_cod_personal  IN  VARCHAR2,
        v_fecha         IN  VARCHAR2,   -- 'dd/MM/yyyy'
        v_tipo          IN  VARCHAR2,   -- '1'..'6'
        v_valor         IN  VARCHAR2,   -- 'HH:MI'
        v_observaciones IN  VARCHAR2 DEFAULT NULL,
        cv_1            OUT SYS_REFCURSOR
    );

    -- ------------------------------------------------------
    -- 5. LISTA DE SUPERVISORES (solo para administradores)
    --    Retorna los usuarios tipo 'Usu' que tienen acceso
    --    a la empresa en el módulo de Control de Asistencia.
    --    El administrador usa esta lista para elegir un
    --    supervisor y ver sus empleados en sp_read_resumen_he.
    --
    --    Retorna: cod_usuario, nom_usuario
    -- ------------------------------------------------------
    PROCEDURE sp_read_supervisores(
        v_cod_usuario   IN  VARCHAR2,
        v_cod_empresa   IN  VARCHAR2,
        cv_1            OUT SYS_REFCURSOR
    );

    -- ------------------------------------------------------
    -- 6. RESUMEN DE HE POR EMPLEADO (Autorizadas / Pendientes)
    --    Para cada empleado visible al usuario en la empresa,
    --    agrega sus horas extras del período e indica cuántas
    --    están autorizadas y cuántas siguen pendientes.
    --
    --    v_cod_usuario: supervisor = sus propios empleados;
    --                   admin = pasa cod_usuario del supervisor
    --                   elegido en sp_read_supervisores.
    --
    --    Retorna por empleado:
    --      cod_personal, nombre_completo, num_fotocheck
    --      cod_c_costos, des_c_costos
    --      dias_con_he, dias_pendientes, dias_autorizados
    --      min_hed / min_hea / min_heo   (minutos PENDIENTES de autorizar)
    --      min_hed_aut / min_hea_aut / min_heo_aut (ya autorizados)
    --      estado: 'SIN_HE' | 'PENDIENTE' | 'PARCIAL' | 'COMPLETO'
    -- ------------------------------------------------------
    PROCEDURE sp_read_resumen_he(
        v_cod_usuario   IN  VARCHAR2,
        v_cod_empresa   IN  VARCHAR2,
        v_fecha_inicio  IN  VARCHAR2,   -- 'dd/MM/yyyy'
        v_fecha_final   IN  VARCHAR2,   -- 'dd/MM/yyyy'
        cv_1            OUT SYS_REFCURSOR
    );

END PKG_AUTH_HE_SUPERVISOR;
/


-- ============================================================
-- BODY
-- ============================================================
CREATE OR REPLACE PACKAGE BODY PKG_AUTH_HE_SUPERVISOR AS

    -- Módulo Control de Asistencia (constante interna)
    c_grupo CONSTANT VARCHAR2(4) := '1002';

    -- =========================================================
    -- FUNCIÓN PRIVADA: decodifica des_password → texto plano
    --
    -- ALGORITMO (Delphi legacy):
    --   Estructura: [salt:3] + [bloque×2 (con sep opcional)] + [2*salt+30:3]
    --   Cada carácter: decoded_char = CHR(encoded_val - salt - 15)
    --   encoded_val = ASCII(char) + salt + 15
    --
    -- Verificación de integridad: suffix = 2*salt + 30
    -- =========================================================
    FUNCTION fn_decode_pwd(v_encoded IN VARCHAR2) RETURN VARCHAR2 AS
        v_salt     NUMBER;
        v_suffix   NUMBER;
        v_n        NUMBER;   -- cantidad de chars del password
        v_central  VARCHAR2(300);
        v_central_len NUMBER;
        v_result   VARCHAR2(100) := '';
        v_val      NUMBER;
        i          NUMBER;
    BEGIN
        IF v_encoded IS NULL OR LENGTH(v_encoded) < 9 OR MOD(LENGTH(v_encoded),3) <> 0 THEN
            RETURN NULL;
        END IF;

        v_salt   := TO_NUMBER(SUBSTR(v_encoded, 1, 3));
        v_suffix := TO_NUMBER(SUBSTR(v_encoded, LENGTH(v_encoded)-2, 3));

        -- Verificar integridad: suffix = 2*salt + 30
        IF v_suffix <> 2 * v_salt + 30 THEN RETURN NULL; END IF;

        -- Bloque central = todo menos prefix(3) y suffix(3)
        v_central     := SUBSTR(v_encoded, 4, LENGTH(v_encoded)-6);
        v_central_len := LENGTH(v_central);  -- en caracteres (grupos de 3)

        -- ¿Tiene separador central? → central_len/3 grupos es impar
        -- Con separador: grupos_totales = 2*N + 1 → N = (grupos-1)/2
        -- Sin separador: grupos_totales = 2*N   → N = grupos/2
        IF MOD(v_central_len/3, 2) = 1 THEN
            v_n := (v_central_len/3 - 1) / 2;   -- con separador
        ELSE
            v_n := (v_central_len/3) / 2;        -- sin separador
        END IF;

        -- Decodificar solo el primer bloque (N chars)
        FOR i IN 0..v_n-1 LOOP
            v_val    := TO_NUMBER(SUBSTR(v_central, 1 + i*3, 3));
            v_result := v_result || CHR(v_val - v_salt - 15);
        END LOOP;

        RETURN v_result;
    EXCEPTION
        WHEN OTHERS THEN RETURN NULL;
    END fn_decode_pwd;

    -- =========================================================
    -- FUNCIÓN PRIVADA: verifica si el usuario tiene acceso
    -- al empleado en la empresa (usa el modelo de seguridad
    -- estándar de 4 filtros: empresa, sucursal, tipo planilla,
    -- centro de costos). Retorna 1 si tiene acceso, 0 si no.
    -- =========================================================
    FUNCTION fn_tiene_acceso(
        v_cod_usuario  IN VARCHAR2,
        v_cod_empresa  IN VARCHAR2,
        v_cod_personal IN VARCHAR2
    ) RETURN NUMBER AS
        v_cnt NUMBER := 0;
    BEGIN
        SELECT COUNT(*) INTO v_cnt
        FROM PLA_PERSONAL p
        WHERE p.cod_empresa  = v_cod_empresa
          AND p.cod_personal = v_cod_personal
          -- Filtro 1: empresa habilitada para el usuario
          AND p.cod_empresa IN (
                SELECT cod_empresa
                FROM   MAE_USUARIO_EMP
                WHERE  cod_usuario   = v_cod_usuario
                  AND  cod_grupo_menu = c_grupo
              )
          -- Filtro 2: sucursal habilitada para el usuario
          AND p.cod_sucursal IN (
                SELECT cod_sucursal
                FROM   MAE_SUCURSAL_USUARIO
                WHERE  cod_usuario    = v_cod_usuario
                  AND  cod_empresa    = p.cod_empresa
                  AND  cod_grupo_menu = c_grupo
              )
          -- Filtro 3: tipo planilla habilitado para el usuario
          AND p.cod_tipo_planilla IN (
                SELECT PPA.cod_tipo_planilla
                FROM   PLA_PERFIL_PLANILLA    PP
                JOIN   PLA_PERFIL_ACCESO_PLANI PPA
                       ON PP.cod_empresa   = PPA.cod_empresa
                      AND PP.cod_perfil_plani = PPA.cod_perfil_plani
                JOIN   PLA_USUARIO_PLANILLA   PUP
                       ON PP.cod_empresa   = PUP.cod_empresa
                      AND PP.cod_perfil_plani = PUP.cod_perfil_plani
                JOIN   PLA_TIPO_PLANILLA      PT
                       ON PPA.cod_empresa      = PT.cod_empresa
                      AND PPA.cod_tipo_planilla = PT.cod_tipo_planilla
                WHERE  PUP.cod_usuario    = v_cod_usuario
                  AND  PUP.cod_grupo_menu = c_grupo
                  AND  PP.cod_empresa     = v_cod_empresa
                  AND  PT.ind_asistencia  = 'S'
              )
          -- Filtro 4: centro de costo
          -- Administradores ('Adm') ven todos los CCs de su empresa
          -- Usuarios ('Usu') solo ven los CCs asignados
          AND (
                EXISTS (
                    SELECT 1
                    FROM   MAE_USUARIO_EMP
                    WHERE  cod_usuario    = v_cod_usuario
                      AND  cod_empresa    = v_cod_empresa
                      AND  cod_grupo_menu = c_grupo
                      AND  tip_usuario    = 'Adm'
                )
                OR p.cod_c_costos IN (
                    SELECT MC.cod_c_costos
                    FROM   MAE_C_COSTOS_VERSION  MCV
                    JOIN   MAE_C_COSTOS           MC
                           ON MCV.cod_empresa        = MC.cod_empresa
                          AND MCV.num_ver_c_costos   = MC.num_ver_c_costos
                    JOIN   MAE_C_COSTOS_USUARIO   MCU
                           ON MC.cod_empresa        = MCU.cod_empresa
                          AND MC.num_ver_c_costos   = MCU.num_ver_c_costos
                          AND MC.cod_c_costos       = MCU.cod_c_costos
                    WHERE  MCU.cod_usuario    = v_cod_usuario
                      AND  MC.cod_empresa     = v_cod_empresa
                      AND  MCU.cod_grupo_menu = c_grupo
                      AND  MCV.ind_vigente    = 'S'
                )
              );
        RETURN v_cnt;
    EXCEPTION
        WHEN OTHERS THEN RETURN 0;
    END fn_tiene_acceso;


    -- =========================================================
    -- 1. LOGIN  (acepta clave en texto plano — decodifica el hash almacenado)
    -- =========================================================
    PROCEDURE sp_login(
        v_cod_usuario    IN  VARCHAR2,
        v_password_plain IN  VARCHAR2,
        cv_1             OUT SYS_REFCURSOR
    ) AS
        v_stored  VARCHAR2(300);
        v_decoded VARCHAR2(100);
        v_baja    VARCHAR2(1);
    BEGIN
        -- Obtener password almacenado y estado
        BEGIN
            SELECT des_password, NVL(ind_baja,'N')
            INTO   v_stored, v_baja
            FROM   MAE_USUARIO
            WHERE  cod_usuario = v_cod_usuario;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN
                OPEN cv_1 FOR SELECT 'ERROR' resultado,'CREDENCIAL_INVALIDA' mensaje FROM DUAL;
                RETURN;
        END;

        -- Verificar si el usuario está dado de baja
        IF v_baja = 'S' THEN
            OPEN cv_1 FOR SELECT 'ERROR' resultado,'USUARIO_BAJA' mensaje FROM DUAL;
            RETURN;
        END IF;

        -- Decodificar hash almacenado y comparar con texto plano
        v_decoded := fn_decode_pwd(v_stored);
        IF v_decoded IS NULL OR v_decoded <> v_password_plain THEN
            OPEN cv_1 FOR SELECT 'ERROR' resultado,'CREDENCIAL_INVALIDA' mensaje FROM DUAL;
            RETURN;
        END IF;

        -- Credenciales OK → devolver datos del usuario
        OPEN cv_1 FOR
            SELECT
                u.cod_usuario,
                u.nom_usuario,
                u.cod_personal,
                NVL(u.ind_admin,'N') ind_admin,
                (SELECT COUNT(DISTINCT cod_empresa) FROM MAE_USUARIO_EMP
                 WHERE cod_usuario=u.cod_usuario AND cod_grupo_menu=c_grupo)          cnt_empresas,
                (SELECT CASE WHEN COUNT(DISTINCT cod_empresa)=1 THEN MAX(cod_empresa) ELSE NULL END
                 FROM MAE_USUARIO_EMP WHERE cod_usuario=u.cod_usuario AND cod_grupo_menu=c_grupo) cod_empresa_unica,
                (SELECT CASE WHEN SUM(CASE WHEN tip_usuario='Adm' THEN 1 ELSE 0 END)>0 THEN 'S' ELSE 'N' END
                 FROM MAE_USUARIO_EMP WHERE cod_usuario=u.cod_usuario AND cod_grupo_menu=c_grupo) es_adm_alguna,
                'OK' resultado,
                ''   mensaje
            FROM MAE_USUARIO u
            WHERE u.cod_usuario = v_cod_usuario;
    END sp_login;


    -- =========================================================
    -- 1b. LOGIN SIN CONTRASEÑA (intranet)
    -- =========================================================
    PROCEDURE sp_login_intranet(
        v_cod_usuario   IN  VARCHAR2,
        cv_1            OUT SYS_REFCURSOR
    ) AS
        v_baja  VARCHAR2(1);
    BEGIN
        -- Verificar existencia del usuario
        BEGIN
            SELECT NVL(ind_baja,'N')
            INTO   v_baja
            FROM   MAE_USUARIO
            WHERE  cod_usuario = v_cod_usuario;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN
                OPEN cv_1 FOR SELECT 'ERROR' resultado,'CREDENCIAL_INVALIDA' mensaje FROM DUAL;
                RETURN;
        END;

        -- Verificar si el usuario está dado de baja
        IF v_baja = 'S' THEN
            OPEN cv_1 FOR SELECT 'ERROR' resultado,'USUARIO_BAJA' mensaje FROM DUAL;
            RETURN;
        END IF;

        -- Usuario OK → devolver datos (sin validar contraseña)
        OPEN cv_1 FOR
            SELECT
                u.cod_usuario,
                u.nom_usuario,
                u.cod_personal,
                NVL(u.ind_admin,'N') ind_admin,
                (SELECT COUNT(DISTINCT cod_empresa) FROM MAE_USUARIO_EMP
                 WHERE cod_usuario=u.cod_usuario AND cod_grupo_menu=c_grupo)          cnt_empresas,
                (SELECT CASE WHEN COUNT(DISTINCT cod_empresa)=1 THEN MAX(cod_empresa) ELSE NULL END
                 FROM MAE_USUARIO_EMP WHERE cod_usuario=u.cod_usuario AND cod_grupo_menu=c_grupo) cod_empresa_unica,
                (SELECT CASE WHEN SUM(CASE WHEN tip_usuario='Adm' THEN 1 ELSE 0 END)>0 THEN 'S' ELSE 'N' END
                 FROM MAE_USUARIO_EMP WHERE cod_usuario=u.cod_usuario AND cod_grupo_menu=c_grupo) es_adm_alguna,
                'OK' resultado,
                ''   mensaje
            FROM MAE_USUARIO u
            WHERE u.cod_usuario = v_cod_usuario;
    END sp_login_intranet;


    -- =========================================================
    -- 2. EMPLEADOS A CARGO
    -- =========================================================
    PROCEDURE sp_read_empleados(
        v_cod_usuario   IN  VARCHAR2,
        v_cod_empresa   IN  VARCHAR2,
        cv_1            OUT SYS_REFCURSOR
    ) AS
    BEGIN
        OPEN cv_1 FOR
            SELECT
                p.cod_personal,
                p.ape_paterno || ' ' || p.ape_materno || ', ' || p.nom_trabajador  nombre_completo,
                p.cod_empresa,
                p.cod_sucursal,
                p.cod_c_costos,
                NVL(cc.des_c_costos, p.cod_c_costos)                               des_c_costos,
                p.cod_tipo_planilla,
                NVL(tp.des_tipo_planilla, p.cod_tipo_planilla)                     des_tipo_planilla,
                p.tip_estado,
                -- Fotocheck más reciente (activo o no)
                (SELECT f.num_fotocheck
                 FROM   SCA_FOTOCHECK f
                 WHERE  f.cod_empresa  = p.cod_empresa
                   AND  f.cod_personal = p.cod_personal
                   AND  f.id_fotocheck = (SELECT MAX(f2.id_fotocheck)
                                         FROM   SCA_FOTOCHECK f2
                                         WHERE  f2.cod_empresa  = p.cod_empresa
                                           AND  f2.cod_personal = p.cod_personal)
                ) num_fotocheck,
                -- Horario vigente a hoy
                hor.horid,
                hor.hordes,
                hor.horcla
            FROM PLA_PERSONAL p
            -- Descripción del centro de costo
            LEFT JOIN MAE_C_COSTOS cc
                   ON cc.cod_empresa       = p.cod_empresa
                  AND cc.num_ver_c_costos  = p.num_ver_c_costos
                  AND cc.cod_c_costos      = p.cod_c_costos
            -- Descripción del tipo de planilla
            JOIN PLA_TIPO_PLANILLA tp
                 ON tp.cod_empresa       = p.cod_empresa
                AND tp.cod_tipo_planilla = p.cod_tipo_planilla
                AND tp.ind_asistencia    = 'S'   -- solo planillas de asistencia
            -- Horario vigente (último fec_vigencia <= hoy)
            LEFT JOIN (
                SELECT hp.cod_empresa,
                       hp.cod_personal,
                       hc.horid,
                       hc.hordes,
                       hc.horcla
                FROM   SCA_HORARIO_PERSONAL hp
                JOIN   SCA_HORARIO_CAB hc ON hc.horid = hp.horid
                WHERE  hp.fec_vigencia = (
                           SELECT MAX(hp2.fec_vigencia)
                           FROM   SCA_HORARIO_PERSONAL hp2
                           WHERE  hp2.cod_empresa  = hp.cod_empresa
                             AND  hp2.cod_personal = hp.cod_personal
                             AND  hp2.fec_vigencia <= TRUNC(SYSDATE)
                       )
            ) hor ON hor.cod_empresa  = p.cod_empresa
                 AND hor.cod_personal = p.cod_personal
            -- Seguridad: empresa habilitada
            WHERE p.cod_empresa = v_cod_empresa
              AND p.tip_estado = 'AC'  -- solo activos ('AC'; cesados = 'CE')
              AND p.cod_empresa IN (
                    SELECT cod_empresa
                    FROM   MAE_USUARIO_EMP
                    WHERE  cod_usuario    = v_cod_usuario
                      AND  cod_grupo_menu = c_grupo
                  )
              -- Seguridad: sucursal habilitada
              AND p.cod_sucursal IN (
                    SELECT cod_sucursal
                    FROM   MAE_SUCURSAL_USUARIO
                    WHERE  cod_usuario    = v_cod_usuario
                      AND  cod_empresa   = p.cod_empresa
                      AND  cod_grupo_menu = c_grupo
                  )
              -- Seguridad: tipo planilla habilitado
              AND p.cod_tipo_planilla IN (
                    SELECT PPA.cod_tipo_planilla
                    FROM   PLA_PERFIL_PLANILLA     PP
                    JOIN   PLA_PERFIL_ACCESO_PLANI PPA
                           ON PP.cod_empresa      = PPA.cod_empresa
                          AND PP.cod_perfil_plani = PPA.cod_perfil_plani
                    JOIN   PLA_USUARIO_PLANILLA    PUP
                           ON PP.cod_empresa      = PUP.cod_empresa
                          AND PP.cod_perfil_plani = PUP.cod_perfil_plani
                    WHERE  PUP.cod_usuario    = v_cod_usuario
                      AND  PUP.cod_grupo_menu = c_grupo
                      AND  PP.cod_empresa     = v_cod_empresa
                  )
              -- Seguridad: centro de costo (Adm=todos / Usu=asignados)
              AND (
                    EXISTS (
                        SELECT 1
                        FROM   MAE_USUARIO_EMP
                        WHERE  cod_usuario    = v_cod_usuario
                          AND  cod_empresa    = v_cod_empresa
                          AND  cod_grupo_menu = c_grupo
                          AND  tip_usuario    = 'Adm'
                    )
                    OR p.cod_c_costos IN (
                        SELECT MC.cod_c_costos
                        FROM   MAE_C_COSTOS_VERSION  MCV
                        JOIN   MAE_C_COSTOS           MC
                               ON MCV.cod_empresa       = MC.cod_empresa
                              AND MCV.num_ver_c_costos  = MC.num_ver_c_costos
                        JOIN   MAE_C_COSTOS_USUARIO   MCU
                               ON MC.cod_empresa       = MCU.cod_empresa
                              AND MC.num_ver_c_costos  = MCU.num_ver_c_costos
                              AND MC.cod_c_costos      = MCU.cod_c_costos
                        WHERE  MCU.cod_usuario    = v_cod_usuario
                          AND  MC.cod_empresa     = v_cod_empresa
                          AND  MCU.cod_grupo_menu = c_grupo
                          AND  MCV.ind_vigente    = 'S'
                    )
                  )
            ORDER BY p.cod_c_costos,
                     p.ape_paterno, p.ape_materno, p.nom_trabajador;
    END sp_read_empleados;


    -- =========================================================
    -- 3. TAREO DIARIO CON HE Y AUTORIZACIONES
    -- =========================================================
    PROCEDURE sp_read_tareo_he(
        v_cod_usuario   IN  VARCHAR2,
        v_cod_empresa   IN  VARCHAR2,
        v_cod_personal  IN  VARCHAR2,
        v_fecha_inicio  IN  VARCHAR2,
        v_fecha_final   IN  VARCHAR2,
        cv_1            OUT SYS_REFCURSOR
    ) AS
    BEGIN
        -- Sin acceso → cursor vacío (sin levantar error)
        IF fn_tiene_acceso(v_cod_usuario, v_cod_empresa, v_cod_personal) = 0 THEN
            OPEN cv_1 FOR SELECT * FROM SCA_ASISTENCIA_TAREO WHERE 1=0;
            RETURN;
        END IF;

        OPEN cv_1 FOR
            SELECT
                -- Identificación del día
                t.fechamar,
                TO_CHAR(t.fechamar, 'DY')                                          dia_semana,
                t.descanso,
                t.feriado,
                -- Horario teórico
                t.entrada_fijada,
                t.salida_fijada,
                t.tothoras,
                t.horiniref,
                t.horfinref,
                t.totref,
                t.horid,
                t.hortur,
                -- Marcaciones reales
                t.entrada,
                t.salida,
                t.inirefri,
                t.finrefri,
                t.nummarcaciones,
                -- Horas calculadas (base 01/01/1900, formato HH24:MI)
                t.horaefectiva,
                t.horatardanza,
                t.tothoramarcas,
                t.horarefrigerio,
                t.tothoranocturna_of,
                -- Horas Extras Antes de entrada (HEA)
                t.horaantesentrada,
                TRUNC(t.horaextantes,    'HH')   horaextantes,
                -- FIX 09/06/2026: si el dia ya esta compensado (EC) y la HEA no fue
                -- autorizada para planilla, no exponer como pendiente de autorizar.
                CASE WHEN t.alerta06 = 'EC' AND NVL(t.hayhea_poraut,'S') != 'N'
                     THEN TO_DATE('01/01/1900','DD/MM/YYYY')
                     ELSE TRUNC(t.horaextantesofi, 'HH')
                END                              horaextantesofi,
                NVL(t.hayhea_poraut, 'N')                                           hayhea_poraut,
                -- Horas Extras Después de salida (HED)
                TRUNC(t.horaextra,       'HH')   horaextra,
                TRUNC(t.horaextraofi,    'HH')   horaextraofi,
                t.horaextra_ajus,
                -- ---------------------------------------------------------------
                -- horaexofi1/2/3 se calculan en minutos para evitar el problema de
                -- fecha-base inconsistente (31/12/1899 vs 01/01/1900) que ocurre
                -- cuando la autorización fue grabada por el sistema original (sin
                -- llamar a sp_SCA_Upd_Tar_InsAut) y deja la fecha-base errónea.
                -- ---------------------------------------------------------------
                -- H25%: minutos autorizados hasta el tope h25f
                CASE
                    WHEN t.tippagohe = '1'
                         AND t.horaextra_ajus IS NOT NULL
                         AND (  TO_NUMBER(TO_CHAR(t.horaextra_ajus, 'HH24')) * 60
                              + TO_NUMBER(TO_CHAR(t.horaextra_ajus, 'MI'))  ) > 0
                    THEN TO_DATE('01/01/1900', 'dd/MM/yyyy')
                         + LEAST(
                               TO_NUMBER(TO_CHAR(t.horaextra_ajus, 'HH24')) * 60
                             + TO_NUMBER(TO_CHAR(t.horaextra_ajus, 'MI')),
                               NVL(  TO_NUMBER(TO_CHAR(t.h25f, 'HH24')) * 60
                                   + TO_NUMBER(TO_CHAR(t.h25f, 'MI')),
                                   99999)
                           ) / 1440.0
                    ELSE NULL
                END                                                                 horaexofi1,
                -- H35%: minutos entre h35i y min(ajus, h35f)
                CASE
                    WHEN t.tippagohe = '1'
                         AND t.horaextra_ajus IS NOT NULL
                         AND t.h35i IS NOT NULL
                         AND (  TO_NUMBER(TO_CHAR(t.horaextra_ajus, 'HH24')) * 60
                              + TO_NUMBER(TO_CHAR(t.horaextra_ajus, 'MI'))  )
                           > (  TO_NUMBER(TO_CHAR(t.h35i, 'HH24')) * 60
                              + TO_NUMBER(TO_CHAR(t.h35i, 'MI'))  )
                    THEN TO_DATE('01/01/1900', 'dd/MM/yyyy')
                         + (  LEAST(
                                  TO_NUMBER(TO_CHAR(t.horaextra_ajus, 'HH24')) * 60
                                + TO_NUMBER(TO_CHAR(t.horaextra_ajus, 'MI')),
                                  NVL(  TO_NUMBER(TO_CHAR(t.h35f, 'HH24')) * 60
                                      + TO_NUMBER(TO_CHAR(t.h35f, 'MI')),
                                      99999)
                              )
                            - NVL(  TO_NUMBER(TO_CHAR(t.h25f, 'HH24')) * 60
                                  + TO_NUMBER(TO_CHAR(t.h25f, 'MI')),
                                  0)
                           ) / 1440.0
                    ELSE NULL
                END                                                                 horaexofi2,
                -- H50%/Doble: minutos por encima de hni (cuando aplica)
                CASE
                    WHEN t.tippagohe = '1'
                         AND t.horaextra_ajus IS NOT NULL
                         AND t.hni IS NOT NULL
                         AND (  TO_NUMBER(TO_CHAR(t.horaextra_ajus, 'HH24')) * 60
                              + TO_NUMBER(TO_CHAR(t.horaextra_ajus, 'MI'))  )
                           > (  TO_NUMBER(TO_CHAR(t.hni, 'HH24')) * 60
                              + TO_NUMBER(TO_CHAR(t.hni, 'MI'))  )
                    THEN TO_DATE('01/01/1900', 'dd/MM/yyyy')
                         + (  TO_NUMBER(TO_CHAR(t.horaextra_ajus, 'HH24')) * 60
                            + TO_NUMBER(TO_CHAR(t.horaextra_ajus, 'MI'))
                            - NVL(  TO_NUMBER(TO_CHAR(t.h35f, 'HH24')) * 60
                                  + TO_NUMBER(TO_CHAR(t.h35f, 'MI')),
                                  0)
                           ) / 1440.0
                    ELSE NULL
                END                                                                 horaexofi3,
                NVL(t.hayhed_poraut, 'N')                                           hayhed_poraut,
                t.haypagohe,
                -- Horas Dobles / Descanso trabajado (HEO)
                TRUNC(t.horadobles,      'HH')   horadobles,
                TRUNC(t.horadoblesof,    'HH')   horadoblesof,
                NVL(t.hayheo_poraut, 'N')                                           hayheo_poraut,
                -- Banco de horas
                t.horabancoh,
                -- Faltas / Permisos / Subsidios
                t.horas_falta,
                t.horas_no_trabajadas,
                t.horapermiso,
                t.per_vaca,
                t.per_desc_med,
                t.per_subsidio,
                t.per_goce,
                t.per_sgoce,
                t.per_lic_sind,
                t.per_suspension,
                -- Alertas relevantes
                t.alerta01,     -- MI=marca impar
                t.alerta04,     -- TN/TE=tardanza; TC=compensada
                t.alerta06,     -- EN/EE=extras; EC=compensadas
                t.alerta07,     -- SN/SE=salida antes; SC=compensada
                t.alerta08,     -- DC=dobles compensadas
                t.alerta09,     -- PE=permiso; PC=compensado
                -- Auditoría depuración
                t.codaux4,
                -- -----------------------------------------------
                -- AUTORIZACIÓN '1' (HEA autorizada) vigente
                -- -----------------------------------------------
                a1.can_authe_str                                                    auth_hea_horas,
                a1.obs_authe                                                        auth_hea_obs,
                a1.cod_usuario                                                      auth_hea_usr,
                -- DESAUTORIZACIÓN '3' (HEA desautorizada) vigente
                a3.can_authe_str                                                    desauth_hea_horas,
                -- -----------------------------------------------
                -- AUTORIZACIÓN '2' (HED autorizada) vigente
                -- -----------------------------------------------
                a2.can_authe_str                                                    auth_hed_horas,
                a2.obs_authe                                                        auth_hed_obs,
                a2.cod_usuario                                                      auth_hed_usr,
                -- DESAUTORIZACIÓN '4' (HED desautorizada) vigente
                a4.can_authe_str                                                    desauth_hed_horas,
                -- -----------------------------------------------
                -- AUTORIZACIÓN '5' (HEO/Dobles autorizada) vigente
                -- -----------------------------------------------
                a5.can_authe_str                                                    auth_heo_horas,
                a5.obs_authe                                                        auth_heo_obs,
                a5.cod_usuario                                                      auth_heo_usr,
                -- DESAUTORIZACIÓN '6' (HEO desautorizada) vigente
                a6.can_authe_str                                                    desauth_heo_horas

            FROM SCA_ASISTENCIA_TAREO t
            -- Autorizaciones vigentes para cada día (LEFT JOIN por tipo)
            LEFT JOIN (SELECT cod_empresa, cod_personal, fec_authe,
                              TO_CHAR(can_authe,'HH24:MI') can_authe_str,
                              obs_authe, cod_usuario
                       FROM   SCA_AUTORIZACION
                       WHERE  tip_authe = '1') a1
                   ON a1.cod_empresa  = t.cod_empresa
                  AND a1.cod_personal = t.cod_personal
                  AND a1.fec_authe    = t.fechamar
            LEFT JOIN (SELECT cod_empresa, cod_personal, fec_authe,
                              TO_CHAR(can_authe,'HH24:MI') can_authe_str
                       FROM   SCA_AUTORIZACION
                       WHERE  tip_authe = '3') a3
                   ON a3.cod_empresa  = t.cod_empresa
                  AND a3.cod_personal = t.cod_personal
                  AND a3.fec_authe    = t.fechamar
            LEFT JOIN (SELECT cod_empresa, cod_personal, fec_authe,
                              TO_CHAR(can_authe,'HH24:MI') can_authe_str,
                              obs_authe, cod_usuario
                       FROM   SCA_AUTORIZACION
                       WHERE  tip_authe = '2') a2
                   ON a2.cod_empresa  = t.cod_empresa
                  AND a2.cod_personal = t.cod_personal
                  AND a2.fec_authe    = t.fechamar
            LEFT JOIN (SELECT cod_empresa, cod_personal, fec_authe,
                              TO_CHAR(can_authe,'HH24:MI') can_authe_str
                       FROM   SCA_AUTORIZACION
                       WHERE  tip_authe = '4') a4
                   ON a4.cod_empresa  = t.cod_empresa
                  AND a4.cod_personal = t.cod_personal
                  AND a4.fec_authe    = t.fechamar
            LEFT JOIN (SELECT cod_empresa, cod_personal, fec_authe,
                              TO_CHAR(can_authe,'HH24:MI') can_authe_str,
                              obs_authe, cod_usuario
                       FROM   SCA_AUTORIZACION
                       WHERE  tip_authe = '5') a5
                   ON a5.cod_empresa  = t.cod_empresa
                  AND a5.cod_personal = t.cod_personal
                  AND a5.fec_authe    = t.fechamar
            LEFT JOIN (SELECT cod_empresa, cod_personal, fec_authe,
                              TO_CHAR(can_authe,'HH24:MI') can_authe_str
                       FROM   SCA_AUTORIZACION
                       WHERE  tip_authe = '6') a6
                   ON a6.cod_empresa  = t.cod_empresa
                  AND a6.cod_personal = t.cod_personal
                  AND a6.fec_authe    = t.fechamar
            WHERE t.cod_empresa  = v_cod_empresa
              AND t.cod_personal = v_cod_personal
              AND t.fechamar BETWEEN TO_DATE(v_fecha_inicio, 'dd/MM/yyyy')
                                 AND TO_DATE(v_fecha_final,  'dd/MM/yyyy')
            ORDER BY t.fechamar;
    END sp_read_tareo_he;


    -- =========================================================
    -- 4. GRABAR AUTORIZACIÓN / DESAUTORIZACIÓN
    -- =========================================================
    PROCEDURE sp_grabar_autorizacion(
        v_cod_usuario   IN  VARCHAR2,
        v_cod_empresa   IN  VARCHAR2,
        v_cod_personal  IN  VARCHAR2,
        v_fecha         IN  VARCHAR2,
        v_tipo          IN  VARCHAR2,
        v_valor         IN  VARCHAR2,
        v_observaciones IN  VARCHAR2 DEFAULT NULL,
        cv_1            OUT SYS_REFCURSOR
    ) AS
        v_fec_authe    DATE;
        v_can_authe    DATE;
        v_tipo_opuesto VARCHAR2(1);
        v_existe       NUMBER        := 0;
        v_auth_borrado NUMBER        := 0;  -- filas eliminadas del tipo opuesto (auth)
        v_err_msg      VARCHAR2(4000);
        v_cur_aux      SYS_REFCURSOR;  -- cursor auxiliar para sp_SCA_Upd_Tar_InsAut
    BEGIN
        -- --------------------------------------------------
        -- Validar tipo
        -- --------------------------------------------------
        IF v_tipo NOT IN ('1','2','3','4','5','6') THEN
            OPEN cv_1 FOR
                SELECT 'ERROR' resultado,
                       'Tipo de autorización inválido: ' || v_tipo || '. Use 1-6.' mensaje
                FROM DUAL;
            RETURN;
        END IF;

        -- --------------------------------------------------
        -- Validar acceso al empleado
        -- --------------------------------------------------
        IF fn_tiene_acceso(v_cod_usuario, v_cod_empresa, v_cod_personal) = 0 THEN
            OPEN cv_1 FOR
                SELECT 'ERROR' resultado,
                       'Sin acceso al empleado ' || v_cod_personal mensaje
                FROM DUAL;
            RETURN;
        END IF;

        -- --------------------------------------------------
        -- Convertir parámetros
        -- --------------------------------------------------
        BEGIN
            v_fec_authe := TO_DATE(v_fecha, 'dd/MM/yyyy');
        EXCEPTION
            WHEN OTHERS THEN
                OPEN cv_1 FOR
                    SELECT 'ERROR' resultado, 'Fecha inválida: ' || v_fecha mensaje
                    FROM DUAL;
                RETURN;
        END;

        BEGIN
            -- v_valor formato 'HH:MI' → base 01/01/1900 HH:MI
            -- TRUNC('HH'): elimina residuos de minutos; solo se autorizan horas enteras.
            v_can_authe := TRUNC(TO_DATE('01/01/1900 ' || v_valor, 'dd/MM/yyyy HH24:MI'), 'HH');
        EXCEPTION
            WHEN OTHERS THEN
                OPEN cv_1 FOR
                    SELECT 'ERROR' resultado, 'Valor de horas inválido: ' || v_valor || '. Use HH:MI' mensaje
                    FROM DUAL;
                RETURN;
        END;

        -- --------------------------------------------------
        -- Lógica de inserción / eliminación por tipo
        --
        -- Pares mutuamente excluyentes:
        --   '1' (auth HEA) ↔ '3' (desauth HEA)
        --   '2' (auth HED) ↔ '4' (desauth HED)
        --   '5' (auth HEO) ↔ '6' (desauth HEO)
        -- --------------------------------------------------

        -- Determinar el tipo opuesto
        v_tipo_opuesto := CASE v_tipo
                              WHEN '1' THEN '3'
                              WHEN '3' THEN '1'
                              WHEN '2' THEN '4'
                              WHEN '4' THEN '2'
                              WHEN '5' THEN '6'
                              WHEN '6' THEN '5'
                          END;

        -- Eliminar tipo opuesto si existe (no deben coexistir)
        DELETE SCA_AUTORIZACION
        WHERE  cod_empresa  = v_cod_empresa
          AND  cod_personal = v_cod_personal
          AND  fec_authe    = v_fec_authe
          AND  tip_authe    = v_tipo_opuesto;

        -- Registrar si se eliminó un auth (opuesto).
        -- Para desauth (3/4/6): si v_auth_borrado > 0 = se canceló una auth existente.
        -- En ese caso NO se inserta desauth: el delete ya es suficiente y evita que
        -- PASO 14 re-aplique la desauth cada noche (anulando el estado pendiente).
        v_auth_borrado := SQL%ROWCOUNT;

        -- Para auth (1/2/5): eliminar también el mismo tipo previo (reemplaza)
        IF v_tipo IN ('1','2','5') THEN
            DELETE SCA_AUTORIZACION
            WHERE  cod_empresa  = v_cod_empresa
              AND  cod_personal = v_cod_personal
              AND  fec_authe    = v_fec_authe
              AND  tip_authe    = v_tipo;
        END IF;

        -- Para desauth (3/4/6): solo INSERT si el auth NO existía (v_auth_borrado = 0).
        -- Motivo: si existía el auth (v_auth_borrado > 0), cancelarlo borrándolo es
        -- la acción correcta (igual que sp_SCA_Insert_Autorizacion original).
        -- Insertar adicionalmente un desauth haría que PASO 14 re-aplique la
        -- desauth nightly (horaextraofi/horadoblesof = NULL), deshaciendo el
        -- estado visual de "pendiente" que se restaura más abajo.
        IF v_tipo IN ('3','4','6') AND v_auth_borrado = 0 THEN
            SELECT COUNT(*) INTO v_existe
            FROM   SCA_AUTORIZACION
            WHERE  cod_empresa  = v_cod_empresa
              AND  cod_personal = v_cod_personal
              AND  fec_authe    = v_fec_authe
              AND  tip_authe    = v_tipo;
        END IF;

        -- INSERT del nuevo registro
        -- Para auth (1/2/5): siempre inserta
        -- Para desauth (3/4/6): solo inserta cuando NO hubo auth que cancelar
        --   (v_auth_borrado = 0) y no había ya una desauth previa (v_existe = 0)
        IF v_tipo IN ('1','2','5') OR
           (v_tipo IN ('3','4','6') AND v_auth_borrado = 0 AND v_existe = 0) THEN
            INSERT INTO SCA_AUTORIZACION
                (id_authe, cod_empresa, cod_personal, fec_authe,
                 ori_authe, tip_authe, can_authe, obs_authe, cod_usuario)
            VALUES
                (id_authe_seq.NEXTVAL, v_cod_empresa, v_cod_personal, v_fec_authe,
                 2, v_tipo, v_can_authe, v_observaciones, v_cod_usuario);
        END IF;

        -- --------------------------------------------------
        -- Actualizar tareo de forma inmediata.
        -- sp_SCA_Upd_Tar_InsAut escribe los campos _ofi
        -- (horaextantesofi / horaextraofi / horadoblesof)
        -- y recalcula horaextra_ajus + horaexofi1/2/3 según
        -- tippagohe, dejando el tareo listo para que el
        -- Interface lleve las HE a planilla sin esperar el
        -- proceso nocturno.
        -- --------------------------------------------------
        sp_SCA_Upd_Tar_InsAut(
            v_cod_empresa,
            v_cod_personal,
            v_fecha,           -- NVARCHAR2 aceptado desde VARCHAR2 (implicit cast)
            v_tipo,            -- CHAR      aceptado desde VARCHAR2 (implicit cast)
            v_valor,           -- NVARCHAR2 aceptado desde VARCHAR2 (implicit cast)
            v_cur_aux
        );
        -- Liberar el cursor interno devuelto por sp_SCA_Upd_Tar_InsAut
        IF v_cur_aux%ISOPEN THEN CLOSE v_cur_aux; END IF;

        -- --------------------------------------------------
        -- Restaurar campos _ofi SOLO cuando se canceló una auth existente
        -- (v_auth_borrado > 0 = se borró el registro auth del tipo opuesto).
        --
        -- Al cancelar una auth, sp_SCA_Upd_Tar_InsAut (tipo '3'/'4'/'6')
        -- pone el campo _ofi = NULL y limpia horaexofi1/2/3 (correcto para
        -- planilla). Pero la UI usa _ofi para mostrar la hora; con NULL
        -- aparece '—' en vez de la hora real con botón "Autorizar".
        --
        -- Al restaurar _ofi = valor bruto:
        --   · La UI vuelve a mostrar la hora en estado pendiente ✓
        --   · horaexofi1/2/3 siguen en NULL → planilla no la recibe ✓
        --   · Sin registro de desauth en SCA_AUTORIZACION, PASO 14 no
        --     vuelve a anular _ofi en el proceso nocturno ✓
        --
        -- Si NO había auth previa (v_auth_borrado = 0) = desauth explícita:
        -- no se restaura → _ofi queda NULL = UI muestra estado desautorizado ✓
        -- --------------------------------------------------
        IF v_tipo = '3' AND v_auth_borrado > 0 THEN
            UPDATE SCA_ASISTENCIA_TAREO
            SET    horaextantesofi = horaextantes
            WHERE  cod_empresa  = v_cod_empresa
              AND  cod_personal = v_cod_personal
              AND  fechamar     = v_fec_authe
              AND  horaextantes IS NOT NULL;
        END IF;

        IF v_tipo = '4' AND v_auth_borrado > 0 THEN
            UPDATE SCA_ASISTENCIA_TAREO
            SET    horaextraofi = horaextra
            WHERE  cod_empresa  = v_cod_empresa
              AND  cod_personal = v_cod_personal
              AND  fechamar     = v_fec_authe
              AND  horaextra IS NOT NULL;
        END IF;

        IF v_tipo = '6' AND v_auth_borrado > 0 THEN
            UPDATE SCA_ASISTENCIA_TAREO
            SET    horadoblesof = horadobles
            WHERE  cod_empresa  = v_cod_empresa
              AND  cod_personal = v_cod_personal
              AND  fechamar     = v_fec_authe
              AND  horadobles IS NOT NULL;
        END IF;

        OPEN cv_1 FOR
            SELECT 'OK'  resultado,
                   CASE v_tipo
                       WHEN '1' THEN 'HEA autorizada: ' || v_valor
                       WHEN '2' THEN 'HED autorizada: ' || v_valor
                       WHEN '5' THEN 'HEO/Dobles autorizada: ' || v_valor
                       WHEN '3' THEN 'HEA desautorizada'
                       WHEN '4' THEN 'HED desautorizada'
                       WHEN '6' THEN 'HEO/Dobles desautorizada'
                   END  mensaje
            FROM DUAL;

    EXCEPTION
        WHEN OTHERS THEN
            ROLLBACK;
            v_err_msg := SQLERRM;
            OPEN cv_1 FOR SELECT 'ERROR' resultado, v_err_msg mensaje FROM DUAL;
    END sp_grabar_autorizacion;


    -- =========================================================
    -- 5. LISTA DE SUPERVISORES
    --    Solo accesible para administradores ('Adm').
    --    Si el caller no es Adm en esa empresa → cursor vacío.
    -- =========================================================
    PROCEDURE sp_read_supervisores(
        v_cod_usuario   IN  VARCHAR2,
        v_cod_empresa   IN  VARCHAR2,
        cv_1            OUT SYS_REFCURSOR
    ) AS
        v_es_admin NUMBER := 0;
    BEGIN
        -- Verificar que el caller sea administrador en esta empresa
        SELECT COUNT(*) INTO v_es_admin
        FROM   MAE_USUARIO_EMP
        WHERE  cod_usuario    = v_cod_usuario
          AND  cod_empresa    = v_cod_empresa
          AND  cod_grupo_menu = c_grupo
          AND  tip_usuario    = 'Adm';

        IF v_es_admin = 0 THEN
            -- No es admin → devolver cursor vacío sin error
            OPEN cv_1 FOR
                SELECT cod_usuario, nom_usuario
                FROM   MAE_USUARIO
                WHERE  1 = 0;
            RETURN;
        END IF;

        OPEN cv_1 FOR
            SELECT u.cod_usuario,
                   u.nom_usuario
            FROM   MAE_USUARIO u
            WHERE  NVL(u.ind_baja,'N') <> 'S'
              AND  EXISTS (
                       SELECT 1
                       FROM   MAE_USUARIO_EMP ue
                       WHERE  ue.cod_usuario    = u.cod_usuario
                         AND  ue.cod_empresa    = v_cod_empresa
                         AND  ue.cod_grupo_menu = c_grupo
                         AND  ue.tip_usuario    = 'Usu'
                   )
            ORDER BY u.nom_usuario;
    END sp_read_supervisores;


    -- =========================================================
    -- 6. RESUMEN DE HE POR EMPLEADO (Autorizadas / Pendientes)
    -- =========================================================
    PROCEDURE sp_read_resumen_he(
        v_cod_usuario   IN  VARCHAR2,
        v_cod_empresa   IN  VARCHAR2,
        v_fecha_inicio  IN  VARCHAR2,
        v_fecha_final   IN  VARCHAR2,
        cv_1            OUT SYS_REFCURSOR
    ) AS
        v_fec_ini DATE;
        v_fec_fin DATE;
        v_base    DATE;
    BEGIN
        v_fec_ini := TO_DATE(v_fecha_inicio, 'dd/MM/yyyy');
        v_fec_fin := TO_DATE(v_fecha_final,  'dd/MM/yyyy');
        v_base    := TO_DATE('01/01/1900',   'dd/MM/yyyy');

        OPEN cv_1 FOR
            SELECT
                sq.cod_personal,
                sq.nombre_completo,
                sq.cod_c_costos,
                sq.des_c_costos,
                sq.num_fotocheck,
                sq.dias_con_he,
                sq.dias_pendientes,
                sq.dias_autorizados,
                sq.min_hed,
                sq.min_hea,
                sq.min_heo,
                sq.min_hed_aut,
                sq.min_hea_aut,
                sq.min_heo_aut,
                CASE
                    WHEN sq.dias_con_he    = 0 THEN 'SIN_HE'
                    WHEN sq.dias_pendientes = 0 THEN 'COMPLETO'
                    WHEN sq.dias_autorizados > 0 THEN 'PARCIAL'
                    ELSE                              'PENDIENTE'
                END  estado,
                sq.obs_authe
            FROM (
                SELECT
                    p.cod_personal,
                    p.ape_paterno || ' ' || p.ape_materno || ', ' || p.nom_trabajador  nombre_completo,
                    p.cod_c_costos,
                    NVL(cc.des_c_costos, p.cod_c_costos)  des_c_costos,
                    -- Fotocheck más reciente
                    (SELECT f.num_fotocheck
                     FROM   SCA_FOTOCHECK f
                     WHERE  f.cod_empresa  = p.cod_empresa
                       AND  f.cod_personal = p.cod_personal
                       AND  f.id_fotocheck = (SELECT MAX(f2.id_fotocheck)
                                              FROM   SCA_FOTOCHECK f2
                                              WHERE  f2.cod_empresa  = p.cod_empresa
                                                AND  f2.cod_personal = p.cod_personal)
                    ) num_fotocheck,
                    -- Días con HE de cualquier tipo en el período.
                    -- Lógica: el PROCESO es el árbitro de qué cuenta como HE real:
                    --   HED: hayhed_poraut IN ('S','N')  → proceso confirmó HE ≥ umbral
                    --        OR (NULL + alerta06 IN ('EN','EE')) → HE calculadas por
                    --           depura o tareo pero hayhed_poraut no fue seteado
                    --           (ocurre cuando HAYPAGOHE='N'). EN=normal, EE=excede raz.
                    --   HEO: hayheo_poraut IN ('S','N')
                    --   HEA: horaextantesofi IS NOT NULL con tiempo > 0
                    (SELECT COUNT(*)
                     FROM   SCA_ASISTENCIA_TAREO t
                     WHERE  t.cod_empresa  = p.cod_empresa
                       AND  t.cod_personal = p.cod_personal
                       AND  t.fechamar     BETWEEN v_fec_ini AND v_fec_fin
                       AND  (
                                t.hayhed_poraut IN ('S','N')
                             OR (t.hayhed_poraut IS NULL AND t.alerta06 IN ('EN','EE'))
                             OR (   t.horaextantesofi IS NOT NULL
                                AND (t.horaextantesofi - TRUNC(t.horaextantesofi)) > 0
                                AND NVL(t.alerta06,'') != 'EC')  -- excluir dias ya compensados
                             OR t.hayheo_poraut IN ('S','N')
                            )
                    ) dias_con_he,
                    -- Días con HE sin ninguna autorización (pendientes)
                    (SELECT COUNT(*)
                     FROM   SCA_ASISTENCIA_TAREO t
                     WHERE  t.cod_empresa  = p.cod_empresa
                       AND  t.cod_personal = p.cod_personal
                       AND  t.fechamar     BETWEEN v_fec_ini AND v_fec_fin
                       AND  (
                                t.hayhed_poraut IN ('S','N')
                             OR (t.hayhed_poraut IS NULL AND t.alerta06 IN ('EN','EE'))
                             OR (   t.horaextantesofi IS NOT NULL
                                AND (t.horaextantesofi - TRUNC(t.horaextantesofi)) > 0
                                AND NVL(t.alerta06,'') != 'EC')  -- excluir dias ya compensados
                             OR t.hayheo_poraut IN ('S','N')
                            )
                       AND  NOT EXISTS (
                                SELECT 1
                                FROM   SCA_AUTORIZACION a
                                WHERE  a.cod_empresa  = p.cod_empresa
                                  AND  a.cod_personal = p.cod_personal
                                  AND  a.fec_authe    = t.fechamar
                                  AND  a.tip_authe   IN ('1','2','5'))
                    ) dias_pendientes,
                    -- Días con al menos una autorización en el período
                    (SELECT COUNT(DISTINCT a.fec_authe)
                     FROM   SCA_AUTORIZACION a
                     WHERE  a.cod_empresa  = p.cod_empresa
                       AND  a.cod_personal = p.cod_personal
                       AND  a.fec_authe    BETWEEN v_fec_ini AND v_fec_fin
                       AND  a.tip_authe   IN ('1','2','5')
                    ) dias_autorizados,
                    -- Minutos HED pendientes de autorizar.
                    -- hayhed_poraut='S': pendiente explícito por proceso.
                    -- hayhed_poraut=NULL + alerta06 IN ('EN','EE'): HE calculadas por
                    -- depura/tareo pero hayhed_poraut no fue seteado (ej: HAYPAGOHE='N').
                    -- EN=normal (dentro de razonabilidad), EE=excede razonabilidad.
                    TRUNC(NVL((SELECT SUM(ROUND((t.horaextra - v_base) * 1440))
                         FROM   SCA_ASISTENCIA_TAREO t
                         WHERE  t.cod_empresa  = p.cod_empresa
                           AND  t.cod_personal = p.cod_personal
                           AND  t.fechamar     BETWEEN v_fec_ini AND v_fec_fin
                           AND  (t.hayhed_poraut = 'S' OR (t.hayhed_poraut IS NULL AND t.alerta06 IN ('EN','EE')))
                           AND  NOT EXISTS (      -- FIX 09/06/2026: excluir dias ya autorizados en BD
                                    SELECT 1 FROM SCA_AUTORIZACION a
                                    WHERE  a.cod_empresa  = p.cod_empresa
                                      AND  a.cod_personal = p.cod_personal
                                      AND  a.fec_authe    = t.fechamar
                                      AND  a.tip_authe    = '2')
                         ), 0) / 60) * 60  min_hed,
                    -- Minutos HEA pendientes de autorizar.
                    -- Usa horaextantesofi (seteado por el proceso cuando supera
                    -- el umbral) con TRUNC para extraer solo la parte horaria
                    -- (base de fecha variable: no se puede restar v_base directo)
                    TRUNC(NVL((SELECT SUM(ROUND((t.horaextantesofi - TRUNC(t.horaextantesofi)) * 1440))
                         FROM   SCA_ASISTENCIA_TAREO t
                         WHERE  t.cod_empresa   = p.cod_empresa
                           AND  t.cod_personal  = p.cod_personal
                           AND  t.fechamar      BETWEEN v_fec_ini AND v_fec_fin
                           AND  t.horaextantesofi IS NOT NULL
                           AND  (t.horaextantesofi - TRUNC(t.horaextantesofi)) > 0
                           AND  NVL(t.alerta06,'') != 'EC'  -- excluir dias ya compensados
                           AND  NOT EXISTS (
                                    SELECT 1 FROM SCA_AUTORIZACION a
                                    WHERE  a.cod_empresa  = p.cod_empresa
                                      AND  a.cod_personal = p.cod_personal
                                      AND  a.fec_authe    = t.fechamar
                                      AND  a.tip_authe    = '1')
                         ), 0) / 60) * 60  min_hea,
                    -- Minutos HEO pendientes de autorizar.
                    -- hayheo_poraut='S': pendiente (proceso confirmó HEO >= umbral).
                    TRUNC(NVL((SELECT SUM(ROUND((t.horadobles - v_base) * 1440))
                         FROM   SCA_ASISTENCIA_TAREO t
                         WHERE  t.cod_empresa  = p.cod_empresa
                           AND  t.cod_personal = p.cod_personal
                           AND  t.fechamar     BETWEEN v_fec_ini AND v_fec_fin
                           AND  t.hayheo_poraut = 'S'
                           AND  NOT EXISTS (      -- FIX 09/06/2026: excluir dias ya autorizados
                                    SELECT 1 FROM SCA_AUTORIZACION a
                                    WHERE  a.cod_empresa  = p.cod_empresa
                                      AND  a.cod_personal = p.cod_personal
                                      AND  a.fec_authe    = t.fechamar
                                      AND  a.tip_authe    = '5')
                         ), 0) / 60) * 60  min_heo,
                    -- Minutos HED autorizados (tip='2')
                    TRUNC(NVL((SELECT SUM(ROUND((a.can_authe - v_base) * 1440))
                         FROM   SCA_AUTORIZACION a
                         WHERE  a.cod_empresa  = p.cod_empresa
                           AND  a.cod_personal = p.cod_personal
                           AND  a.fec_authe    BETWEEN v_fec_ini AND v_fec_fin
                           AND  a.tip_authe    = '2'), 0) / 60) * 60  min_hed_aut,
                    -- Minutos HEA autorizados (tip='1')
                    TRUNC(NVL((SELECT SUM(ROUND((a.can_authe - v_base) * 1440))
                         FROM   SCA_AUTORIZACION a
                         WHERE  a.cod_empresa  = p.cod_empresa
                           AND  a.cod_personal = p.cod_personal
                           AND  a.fec_authe    BETWEEN v_fec_ini AND v_fec_fin
                           AND  a.tip_authe    = '1'), 0) / 60) * 60  min_hea_aut,
                    -- Minutos HEO autorizados (tip='5')
                    TRUNC(NVL((SELECT SUM(ROUND((a.can_authe - v_base) * 1440))
                         FROM   SCA_AUTORIZACION a
                         WHERE  a.cod_empresa  = p.cod_empresa
                           AND  a.cod_personal = p.cod_personal
                           AND  a.fec_authe    BETWEEN v_fec_ini AND v_fec_fin
                           AND  a.tip_authe    = '5'), 0) / 60) * 60  min_heo_aut,
                    -- Última observación registrada en el período (la más reciente por id_authe)
                    (SELECT a.obs_authe
                    FROM   SCA_AUTORIZACION a
                    WHERE  a.cod_empresa  = p.cod_empresa
                    AND  a.cod_personal = p.cod_personal
                    AND  a.fec_authe    BETWEEN v_fec_ini AND v_fec_fin
                    AND  a.tip_authe   IN ('1','2','5')
                    AND  a.id_authe     = (SELECT MAX(a2.id_authe)
                                            FROM   SCA_AUTORIZACION a2
                                            WHERE  a2.cod_empresa  = p.cod_empresa
                                                AND  a2.cod_personal = p.cod_personal
                                                AND  a2.fec_authe    BETWEEN v_fec_ini AND v_fec_fin
                                                AND  a2.tip_authe   IN ('1','2','5'))
                    )  obs_authe
                FROM PLA_PERSONAL p
                LEFT JOIN MAE_C_COSTOS cc
                       ON cc.cod_empresa      = p.cod_empresa
                      AND cc.num_ver_c_costos = p.num_ver_c_costos
                      AND cc.cod_c_costos     = p.cod_c_costos
                JOIN  PLA_TIPO_PLANILLA tp
                       ON tp.cod_empresa       = p.cod_empresa
                      AND tp.cod_tipo_planilla = p.cod_tipo_planilla
                      AND tp.ind_asistencia    = 'S'
                WHERE p.cod_empresa = v_cod_empresa
                  AND p.tip_estado = 'AC'  -- solo activos ('AC'; cesados = 'CE')
                  -- Seguridad: empresa habilitada
                  AND p.cod_empresa IN (
                        SELECT cod_empresa
                        FROM   MAE_USUARIO_EMP
                        WHERE  cod_usuario    = v_cod_usuario
                          AND  cod_grupo_menu = c_grupo
                      )
                  -- Seguridad: sucursal habilitada
                  AND p.cod_sucursal IN (
                        SELECT cod_sucursal
                        FROM   MAE_SUCURSAL_USUARIO
                        WHERE  cod_usuario    = v_cod_usuario
                          AND  cod_empresa    = p.cod_empresa
                          AND  cod_grupo_menu = c_grupo
                      )
                  -- Seguridad: tipo planilla habilitado
                  AND p.cod_tipo_planilla IN (
                        SELECT PPA.cod_tipo_planilla
                        FROM   PLA_PERFIL_PLANILLA     PP
                        JOIN   PLA_PERFIL_ACCESO_PLANI PPA
                               ON PP.cod_empresa      = PPA.cod_empresa
                              AND PP.cod_perfil_plani = PPA.cod_perfil_plani
                        JOIN   PLA_USUARIO_PLANILLA    PUP
                               ON PP.cod_empresa      = PUP.cod_empresa
                              AND PP.cod_perfil_plani = PUP.cod_perfil_plani
                        WHERE  PUP.cod_usuario    = v_cod_usuario
                          AND  PUP.cod_grupo_menu = c_grupo
                          AND  PP.cod_empresa     = v_cod_empresa
                      )
                  -- Seguridad: CC (Adm=todos / Usu=asignados)
                  AND (
                        EXISTS (
                            SELECT 1
                            FROM   MAE_USUARIO_EMP
                            WHERE  cod_usuario    = v_cod_usuario
                              AND  cod_empresa    = v_cod_empresa
                              AND  cod_grupo_menu = c_grupo
                              AND  tip_usuario    = 'Adm'
                        )
                        OR p.cod_c_costos IN (
                            SELECT MC.cod_c_costos
                            FROM   MAE_C_COSTOS_VERSION  MCV
                            JOIN   MAE_C_COSTOS           MC
                                   ON MCV.cod_empresa      = MC.cod_empresa
                                  AND MCV.num_ver_c_costos = MC.num_ver_c_costos
                            JOIN   MAE_C_COSTOS_USUARIO   MCU
                                   ON MC.cod_empresa       = MCU.cod_empresa
                                  AND MC.num_ver_c_costos  = MCU.num_ver_c_costos
                                  AND MC.cod_c_costos      = MCU.cod_c_costos
                            WHERE  MCU.cod_usuario    = v_cod_usuario
                              AND  MC.cod_empresa     = v_cod_empresa
                              AND  MCU.cod_grupo_menu = c_grupo
                              AND  MCV.ind_vigente    = 'S'
                        )
                      )
            ) sq
            ORDER BY sq.cod_c_costos, sq.nombre_completo;
    END sp_read_resumen_he;

END PKG_AUTH_HE_SUPERVISOR;
/
