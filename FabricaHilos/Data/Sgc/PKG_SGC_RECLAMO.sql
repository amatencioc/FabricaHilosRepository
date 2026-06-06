/* ============================================================
   SGC_RECLAMO — Análisis de Reclamos
   Módulo  : SGC — Sistema de Gestión de Calidad
   BD      : SIG (Oracle 11.2.0.4)

   TABLAS:
     SGC_RECLAMO           — Cabecera del reclamo
     SGC_RECLAMO_DESCARGO  — Descargos de vendedor (VD), analista (AC) y gerente (GE)
     SGC_RECLAMO_ARCHIVO   — Archivos adjuntos (referencia a disco)

   FLUJO:
     1. Vendedor abre el reclamo: elige cliente, contacto, teléfono,
        asunto, agrega su descargo y adjunta archivos.
        → P_CREAR_RECLAMO  (ESTADO='01' Abierto)

     2. Analista de Calidad revisa, agrega su descargo y sube evidencia.
        → P_AGREGAR_DESCARGO (ROL='AC')
        → ESTADO pasa a '02' (En Revisión) automáticamente.

     3. Analista escala a Gerencia para aprobación.
        → P_ESCALAR_GERENCIA  (ESTADO='03' Pend. Aprobación)

     4a. Gerente aprueba el reclamo (ES válido).
        → P_APROBAR_RECLAMO  (ESTADO='04' Aprobado)

     4b. Gerente rechaza el reclamo (NO es válido).
        → P_RECHAZAR_RECLAMO  (ESTADO='05' Rechazado)
        El gerente también puede rechazar desde ESTADO='02'.

   ESTADOS:
     '01' = Abierto             (sólo vendedor actuó)
     '02' = En Revisión         (analista participó)
     '03' = Pend. Aprobación    (escalado a gerencia)
     '04' = Aprobado            (terminal — es un reclamo válido)
     '05' = Rechazado           (terminal — no es un reclamo válido)

   ROLES DE DESCARGO / ARCHIVO:
     'VD' = Vendedor
     'AC' = Analista de Calidad
     'GE' = Gerente
   ============================================================ */

-- ──────────────────────────────────────────────────────────────
--  1. TABLAS Y SECUENCIAS
-- ──────────────────────────────────────────────────────────────

CREATE TABLE SGC_RECLAMO (
    ID_RECLAMO      NUMBER(10)      NOT NULL,
    COD_CLIENTE     VARCHAR2(15)    NOT NULL,
    NOM_CLIENTE     VARCHAR2(200),               -- desnormalizado (evita JOIN en listados)
    CONTACTO        VARCHAR2(100)   NOT NULL,
    TELEFONO        VARCHAR2(30)    NOT NULL,
    ASUNTO          VARCHAR2(400)   NOT NULL,
    ESTADO          VARCHAR2(2)     DEFAULT '01' NOT NULL,
    USU_VENDEDOR    VARCHAR2(30)    NOT NULL,     -- usuario que creó el reclamo
    FCH_CREACION    DATE            DEFAULT SYSDATE NOT NULL,
    USU_ANALISTA    VARCHAR2(30),                -- primer analista que intervino
    FCH_ANALISIS    DATE,                        -- fecha de primera intervención del analista
    USU_GERENTE     VARCHAR2(30),                -- gerente que aprobó/rechazó
    FCH_APROBACION  DATE,                        -- fecha de aprobación/rechazo
    MOT_RECHAZO     VARCHAR2(1000),              -- motivo de rechazo (ESTADO='05')
    ANALISIS_CAUSA  VARCHAR2(4000),              -- análisis de causa (analista de calidad)
    DECISION_FINAL  VARCHAR2(4000),              -- decisión (analista, sólo cuando ESTADO='04')
    FCH_DECISION    DATE,                        -- fecha en que se registró la decisión
    USU_DECISION    VARCHAR2(30),                -- usuario que registró la decisión
    FCH_NOTI_CALIDAD DATE,                       -- última fecha de notificación a calidad
    FCH_NOTI_VEND   DATE,                        -- última fecha de notificación al vendedor (post-aprobación)
    A_ADUSER        VARCHAR2(30),
    A_ADFECHA       DATE,
    A_MDUSER        VARCHAR2(30),
    A_MDFECHA       DATE,
    CONSTRAINT PK_SGC_RECLAMO       PRIMARY KEY (ID_RECLAMO),
    CONSTRAINT CK_SGC_RECLAMO_EST   CHECK (ESTADO IN ('01','02','03','04','05'))
);

-- ALTER para BDs ya desplegadas (idempotente: ignorar ORA-01430 si la columna ya existe)
-- ALTER TABLE SGC_RECLAMO ADD (ANALISIS_CAUSA   VARCHAR2(4000));
-- ALTER TABLE SGC_RECLAMO ADD (DECISION_FINAL   VARCHAR2(4000));
-- ALTER TABLE SGC_RECLAMO ADD (FCH_DECISION     DATE);
-- ALTER TABLE SGC_RECLAMO ADD (USU_DECISION     VARCHAR2(30));
-- ALTER TABLE SGC_RECLAMO ADD (FCH_NOTI_CALIDAD DATE);
-- ALTER TABLE SGC_RECLAMO ADD (FCH_NOTI_VEND    DATE);

CREATE SEQUENCE SGC_RECLAMO_SEQ
    START WITH 1 INCREMENT BY 1 NOCACHE NOCYCLE;

COMMENT ON TABLE  SGC_RECLAMO               IS 'Reclamos de clientes — módulo SGC';
COMMENT ON COLUMN SGC_RECLAMO.ESTADO        IS '01=Abierto  02=En Revisión  03=Pend.Aprobación  04=Aprobado  05=Rechazado';
COMMENT ON COLUMN SGC_RECLAMO.USU_VENDEDOR  IS 'Usuario del vendedor que abrió el reclamo';
COMMENT ON COLUMN SGC_RECLAMO.USU_ANALISTA  IS 'Primer analista de calidad que intervino';
COMMENT ON COLUMN SGC_RECLAMO.USU_GERENTE   IS 'Gerente que aprobó o rechazó el reclamo';
COMMENT ON COLUMN SGC_RECLAMO.MOT_RECHAZO   IS 'Motivo de rechazo (solo ESTADO=05)';
COMMENT ON COLUMN SGC_RECLAMO.ANALISIS_CAUSA IS 'Análisis de causa registrado por el analista de calidad';
COMMENT ON COLUMN SGC_RECLAMO.DECISION_FINAL IS 'Decisión registrada por el analista (sólo cuando ESTADO=04)';
COMMENT ON COLUMN SGC_RECLAMO.FCH_NOTI_CALIDAD IS 'Última fecha en que el vendedor notificó al área de calidad';
COMMENT ON COLUMN SGC_RECLAMO.FCH_NOTI_VEND IS 'Última fecha en que calidad notificó al vendedor (post-aprobación)';
/

CREATE TABLE SGC_RECLAMO_DESCARGO (
    ID_DESCARGO     NUMBER(10)      NOT NULL,
    ID_RECLAMO      NUMBER(10)      NOT NULL,
    ROL             VARCHAR2(2)     NOT NULL,    -- 'VD', 'AC' o 'GE'
    DESCRIPCION     VARCHAR2(4000)  NOT NULL,
    USUARIO         VARCHAR2(30)    NOT NULL,
    FCH_REGISTRO    DATE            DEFAULT SYSDATE NOT NULL,
    CONSTRAINT PK_SGC_DESCARGO          PRIMARY KEY (ID_DESCARGO),
    CONSTRAINT FK_SGC_DESC_RECLAMO      FOREIGN KEY (ID_RECLAMO)
        REFERENCES SGC_RECLAMO (ID_RECLAMO),
    CONSTRAINT CK_SGC_DESCARGO_ROL      CHECK (ROL IN ('VD','AC','GE'))
);

CREATE SEQUENCE SGC_RECLAMO_DESC_SEQ
    START WITH 1 INCREMENT BY 1 NOCACHE NOCYCLE;

CREATE INDEX IX_SGC_DESCARGO_RECLAMO ON SGC_RECLAMO_DESCARGO (ID_RECLAMO);

COMMENT ON COLUMN SGC_RECLAMO_DESCARGO.ROL IS 'VD=Vendedor  AC=Analista de Calidad  GE=Gerente';
/

CREATE TABLE SGC_RECLAMO_ARCHIVO (
    ID_ARCHIVO      NUMBER(10)      NOT NULL,
    ID_RECLAMO      NUMBER(10)      NOT NULL,
    ROL             VARCHAR2(2)     NOT NULL,    -- 'VD', 'AC' o 'GE'
    NOMBRE_ORIG     VARCHAR2(500)   NOT NULL,    -- nombre original del archivo
    NOMBRE_SERVER   VARCHAR2(500)   NOT NULL,    -- nombre en disco (GUID + ext)
    MIME_TYPE       VARCHAR2(100),
    TAMANIO_BYTES   NUMBER(15),
    USUARIO         VARCHAR2(30)    NOT NULL,
    FCH_CARGA       DATE            DEFAULT SYSDATE NOT NULL,
    CONSTRAINT PK_SGC_ARCHIVO           PRIMARY KEY (ID_ARCHIVO),
    CONSTRAINT FK_SGC_ARCH_RECLAMO      FOREIGN KEY (ID_RECLAMO)
        REFERENCES SGC_RECLAMO (ID_RECLAMO),
    CONSTRAINT CK_SGC_ARCHIVO_ROL       CHECK (ROL IN ('VD','AC','GE'))
);

CREATE SEQUENCE SGC_RECLAMO_ARCH_SEQ
    START WITH 1 INCREMENT BY 1 NOCACHE NOCYCLE;

CREATE INDEX IX_SGC_ARCHIVO_RECLAMO ON SGC_RECLAMO_ARCHIVO (ID_RECLAMO);
/

-- ──────────────────────────────────────────────────────────────
--  2. ESPECIFICACIÓN DEL PAQUETE
-- ──────────────────────────────────────────────────────────────

CREATE OR REPLACE PACKAGE PKG_SGC_RECLAMO AS

    TYPE T_CURSOR IS REF CURSOR;

    -- ── Consultas ────────────────────────────────────────────────

    /*  Lista todos los reclamos con conteo de descargos y archivos.
        Filtros opcionales: texto libre (cliente / asunto / usuario)
        y estado ('01','02','03' o NULL = todos). */
    PROCEDURE P_OBTENER_RECLAMOS (
        P_BUSCAR IN  VARCHAR2 DEFAULT NULL,
        P_ESTADO IN  VARCHAR2 DEFAULT NULL,
        P_CURSOR OUT T_CURSOR
    );

    /*  Cabecera completa de un reclamo. */
    PROCEDURE P_OBTENER_RECLAMO (
        P_ID_RECLAMO IN  NUMBER,
        P_CURSOR     OUT T_CURSOR
    );

    /*  Descargos de un reclamo, ordenados por fecha. */
    PROCEDURE P_OBTENER_DESCARGOS (
        P_ID_RECLAMO IN  NUMBER,
        P_CURSOR     OUT T_CURSOR
    );

    /*  Archivos adjuntos de un reclamo, ordenados por fecha. */
    PROCEDURE P_OBTENER_ARCHIVOS (
        P_ID_RECLAMO IN  NUMBER,
        P_CURSOR     OUT T_CURSOR
    );

    /*  Lista de clientes activos para el combo del formulario.
        Búsqueda opcional por código o nombre. Devuelve también el RUC. */
    PROCEDURE P_OBTENER_CLIENTES (
        P_BUSCAR IN  VARCHAR2 DEFAULT NULL,
        P_CURSOR OUT T_CURSOR
    );

    -- ── Escritura ────────────────────────────────────────────────

    /*  Crea un nuevo reclamo (lo abre el vendedor).
        Inserta la cabecera en SGC_RECLAMO (ESTADO='01')
        y el primer descargo del vendedor en SGC_RECLAMO_DESCARGO.
        Retorna el ID asignado. */
    PROCEDURE P_CREAR_RECLAMO (
        P_COD_CLIENTE IN  VARCHAR2,
        P_NOM_CLIENTE IN  VARCHAR2,
        P_CONTACTO    IN  VARCHAR2,
        P_TELEFONO    IN  VARCHAR2,
        P_ASUNTO      IN  VARCHAR2,
        P_DESCARGO    IN  VARCHAR2,
        P_USUARIO     IN  VARCHAR2,
        P_ID_RECLAMO  OUT NUMBER,
        P_MSGERROR    OUT VARCHAR2
    );

    /*  Agrega un descargo al reclamo.
        Si ROL='AC' y el reclamo está en ESTADO='01', lo avanza a '02'.
        Retorna el ID del descargo insertado. */
    PROCEDURE P_AGREGAR_DESCARGO (
        P_ID_RECLAMO  IN  NUMBER,
        P_ROL         IN  VARCHAR2,
        P_DESCRIPCION IN  VARCHAR2,
        P_USUARIO     IN  VARCHAR2,
        P_ID_DESCARGO OUT NUMBER,
        P_MSGERROR    OUT VARCHAR2
    );

    /*  Registra la referencia de un archivo subido al servidor.
        El archivo físico ya fue guardado en disco por la capa de C#. */
    PROCEDURE P_REGISTRAR_ARCHIVO (
        P_ID_RECLAMO    IN  NUMBER,
        P_ROL           IN  VARCHAR2,
        P_NOMBRE_ORIG   IN  VARCHAR2,
        P_NOMBRE_SERVER IN  VARCHAR2,
        P_MIME_TYPE     IN  VARCHAR2,
        P_TAMANIO       IN  NUMBER,
        P_USUARIO       IN  VARCHAR2,
        P_ID_ARCHIVO    OUT NUMBER,
        P_MSGERROR      OUT VARCHAR2
    );

    /*  Elimina el registro de un archivo de la BD.
        La capa de C# elimina el archivo físico antes o después. */
    PROCEDURE P_ELIMINAR_ARCHIVO (
        P_ID_ARCHIVO IN  NUMBER,
        P_USUARIO    IN  VARCHAR2,
        P_MSGERROR   OUT VARCHAR2
    );

    /*  Cambia el estado de un reclamo ('01'-'05') — solo para correcciones. */
    PROCEDURE P_CAMBIAR_ESTADO (
        P_ID_RECLAMO IN  NUMBER,
        P_ESTADO     IN  VARCHAR2,
        P_USUARIO    IN  VARCHAR2,
        P_MSGERROR   OUT VARCHAR2
    );

    /*  Analista escala el reclamo a Gerencia. Requiere ESTADO='02'. → '03'. */
    PROCEDURE P_ESCALAR_GERENCIA (
        P_ID_RECLAMO IN  NUMBER,
        P_USUARIO    IN  VARCHAR2,
        P_MSGERROR   OUT VARCHAR2
    );

    /*  Gerente aprueba el reclamo (SÍ es válido). Requiere ESTADO='03'. → '04'. */
    PROCEDURE P_APROBAR_RECLAMO (
        P_ID_RECLAMO  IN  NUMBER,
        P_OBSERVACION IN  VARCHAR2 DEFAULT NULL,
        P_USUARIO     IN  VARCHAR2,
        P_MSGERROR    OUT VARCHAR2
    );

    /*  Gerente rechaza el reclamo (NO es válido). Requiere ESTADO='02'/'03'. → '05'. */
    PROCEDURE P_RECHAZAR_RECLAMO (
        P_ID_RECLAMO IN  NUMBER,
        P_MOTIVO     IN  VARCHAR2,
        P_USUARIO    IN  VARCHAR2,
        P_MSGERROR   OUT VARCHAR2
    );

    /*  Elimina completamente un reclamo: archivos BD, descargos y cabecera.
        Devuelve en P_NOMBRES_SERVER la lista separada por '|' de nombres
        de archivo en servidor, para que C# borre la carpeta física. */
    PROCEDURE P_ELIMINAR_RECLAMO (
        P_ID_RECLAMO     IN  NUMBER,
        P_USUARIO        IN  VARCHAR2,
        P_NOMBRES_SERVER OUT VARCHAR2,
        P_MSGERROR       OUT VARCHAR2
    );

    -- ── Análisis de Causa / Decisión ─────────────────────────────

    /*  Guarda el ANÁLISIS DE CAUSA (campo del analista de calidad).
        Permitido en estados '01'..'04'. No permitido en '05' (Rechazado). */
    PROCEDURE P_GUARDAR_ANALISIS_CAUSA (
        P_ID_RECLAMO IN  NUMBER,
        P_TEXTO      IN  VARCHAR2,
        P_USUARIO    IN  VARCHAR2,
        P_MSGERROR   OUT VARCHAR2
    );

    /*  Guarda la DECISIÓN (campo del analista de calidad).
        Sólo permitido cuando el reclamo está APROBADO (ESTADO='04'). */
    PROCEDURE P_GUARDAR_DECISION (
        P_ID_RECLAMO IN  NUMBER,
        P_TEXTO      IN  VARCHAR2,
        P_USUARIO    IN  VARCHAR2,
        P_MSGERROR   OUT VARCHAR2
    );

    -- ── Notificaciones (la capa C# realiza el envío SMTP) ────────

    /*  Marca el reclamo como "notificado a calidad" (cuando el vendedor envía).
        Devuelve en P_DESTINATARIOS los correos del área de calidad separados por ';'.
        Permitido sólo en estados '01' o '02'. */
    PROCEDURE P_NOTIFICAR_CALIDAD (
        P_ID_RECLAMO    IN  NUMBER,
        P_USUARIO       IN  VARCHAR2,
        P_DESTINATARIOS OUT VARCHAR2,
        P_ASUNTO_MAIL   OUT VARCHAR2,
        P_NOM_CLIENTE   OUT VARCHAR2,
        P_MSGERROR      OUT VARCHAR2
    );

    /*  Marca que calidad notificó al vendedor que el reclamo ya fue aprobado.
        Devuelve en P_DESTINATARIO el correo del vendedor.
        Sólo permitido cuando ESTADO='04'. */
    PROCEDURE P_NOTIFICAR_VENDEDOR_APROBADO (
        P_ID_RECLAMO    IN  NUMBER,
        P_USUARIO       IN  VARCHAR2,
        P_DESTINATARIO  OUT VARCHAR2,
        P_ASUNTO_MAIL   OUT VARCHAR2,
        P_NOM_CLIENTE   OUT VARCHAR2,
        P_MSGERROR      OUT VARCHAR2
    );

    /*  Devuelve toda la información necesaria para imprimir el reclamo aprobado:
        cabecera + descargos + archivos + análisis de causa + decisión + gerente firmante.
        Sólo se permite imprimir cuando ESTADO='04'. */
    PROCEDURE P_OBTENER_IMPRESION (
        P_ID_RECLAMO    IN  NUMBER,
        P_CUR_CABECERA  OUT T_CURSOR,
        P_CUR_DESCARGOS OUT T_CURSOR,
        P_CUR_ARCHIVOS  OUT T_CURSOR,
        P_MSGERROR      OUT VARCHAR2
    );

    /*  Obtiene el email de un usuario a partir de su código. */
    PROCEDURE P_OBTENER_EMAIL_USUARIO (
        P_COD_USUARIO   IN  VARCHAR2,
        P_EMAIL         OUT VARCHAR2,
        P_MSGERROR      OUT VARCHAR2
    );

END PKG_SGC_RECLAMO;
/

-- ──────────────────────────────────────────────────────────────
--  3. CUERPO DEL PAQUETE
-- ──────────────────────────────────────────────────────────────

CREATE OR REPLACE PACKAGE BODY PKG_SGC_RECLAMO AS

    -- ── P_OBTENER_RECLAMOS ──────────────────────────────────────
    PROCEDURE P_OBTENER_RECLAMOS (
        P_BUSCAR IN  VARCHAR2 DEFAULT NULL,
        P_ESTADO IN  VARCHAR2 DEFAULT NULL,
        P_CURSOR OUT T_CURSOR
    ) IS
        V_BUSCAR VARCHAR2(400);
    BEGIN
        IF TRIM(P_BUSCAR) IS NOT NULL THEN
            V_BUSCAR := '%' || UPPER(TRIM(P_BUSCAR)) || '%';
        END IF;

        OPEN P_CURSOR FOR
            SELECT R.ID_RECLAMO,
                   R.COD_CLIENTE,
                   C.RUC                  AS RUC_CLIENTE,
                   R.NOM_CLIENTE,
                   R.CONTACTO,
                   R.TELEFONO,
                   R.ASUNTO,
                   R.ESTADO,
                   R.USU_VENDEDOR,
                   R.FCH_CREACION,
                   R.USU_ANALISTA,
                   R.FCH_ANALISIS,
                   R.USU_GERENTE,
                   R.FCH_APROBACION,
                   R.MOT_RECHAZO,
                   R.ANALISIS_CAUSA,
                   R.DECISION_FINAL,
                   R.FCH_DECISION,
                   R.USU_DECISION,
                   R.FCH_NOTI_CALIDAD,
                   R.FCH_NOTI_VEND,
                   (SELECT COUNT(*)
                    FROM   SGC_RECLAMO_DESCARGO D
                    WHERE  D.ID_RECLAMO = R.ID_RECLAMO) AS TOTAL_DESCARGOS,
                   (SELECT COUNT(*)
                    FROM   SGC_RECLAMO_ARCHIVO  A
                    WHERE  A.ID_RECLAMO = R.ID_RECLAMO) AS TOTAL_ARCHIVOS
            FROM   SGC_RECLAMO R
            LEFT   JOIN CLIENTES C ON C.COD_CLIENTE = R.COD_CLIENTE
            WHERE  (P_ESTADO IS NULL OR R.ESTADO = P_ESTADO)
              AND  (V_BUSCAR IS NULL
                    OR UPPER(R.NOM_CLIENTE)  LIKE V_BUSCAR
                    OR UPPER(R.COD_CLIENTE)  LIKE V_BUSCAR
                    OR UPPER(C.RUC)          LIKE V_BUSCAR
                    OR UPPER(R.ASUNTO)       LIKE V_BUSCAR
                    OR UPPER(R.USU_VENDEDOR) LIKE V_BUSCAR
                    OR UPPER(R.CONTACTO)     LIKE V_BUSCAR)
            ORDER BY R.FCH_CREACION DESC, R.ID_RECLAMO DESC;
    END P_OBTENER_RECLAMOS;

    -- ── P_OBTENER_RECLAMO ───────────────────────────────────────
    PROCEDURE P_OBTENER_RECLAMO (
        P_ID_RECLAMO IN  NUMBER,
        P_CURSOR     OUT T_CURSOR
    ) IS
    BEGIN
        OPEN P_CURSOR FOR
            SELECT R.ID_RECLAMO, R.COD_CLIENTE,
                   C.RUC               AS RUC_CLIENTE,
                   R.NOM_CLIENTE,
                   R.CONTACTO, R.TELEFONO, R.ASUNTO, R.ESTADO,
                   R.USU_VENDEDOR, R.FCH_CREACION,
                   R.USU_ANALISTA, R.FCH_ANALISIS,
                   R.USU_GERENTE, R.FCH_APROBACION, R.MOT_RECHAZO,
                   R.ANALISIS_CAUSA, R.DECISION_FINAL,
                   R.FCH_DECISION, R.USU_DECISION,
                   R.FCH_NOTI_CALIDAD, R.FCH_NOTI_VEND,
                   R.A_ADUSER, R.A_ADFECHA, R.A_MDUSER, R.A_MDFECHA,
                   (SELECT D.DESCRIPCION
                    FROM   SGC_RECLAMO_DESCARGO D
                    WHERE  D.ID_RECLAMO = R.ID_RECLAMO
                      AND  D.ROL = 'VD'
                      AND  D.ID_DESCARGO = (
                               SELECT MIN(D2.ID_DESCARGO)
                               FROM   SGC_RECLAMO_DESCARGO D2
                               WHERE  D2.ID_RECLAMO = R.ID_RECLAMO
                                 AND  D2.ROL = 'VD')
                   ) AS DESCRIPCION
            FROM   SGC_RECLAMO R
            LEFT   JOIN CLIENTES C ON C.COD_CLIENTE = R.COD_CLIENTE
            WHERE  R.ID_RECLAMO = P_ID_RECLAMO;
    END P_OBTENER_RECLAMO;

    -- ── P_OBTENER_DESCARGOS ─────────────────────────────────────
    PROCEDURE P_OBTENER_DESCARGOS (
        P_ID_RECLAMO IN  NUMBER,
        P_CURSOR     OUT T_CURSOR
    ) IS
    BEGIN
        OPEN P_CURSOR FOR
            SELECT ID_DESCARGO, ID_RECLAMO, ROL,
                   DESCRIPCION, USUARIO, FCH_REGISTRO
            FROM   SGC_RECLAMO_DESCARGO
            WHERE  ID_RECLAMO = P_ID_RECLAMO
            ORDER  BY FCH_REGISTRO ASC, ID_DESCARGO ASC;
    END P_OBTENER_DESCARGOS;

    -- ── P_OBTENER_ARCHIVOS ──────────────────────────────────────
    PROCEDURE P_OBTENER_ARCHIVOS (
        P_ID_RECLAMO IN  NUMBER,
        P_CURSOR     OUT T_CURSOR
    ) IS
    BEGIN
        OPEN P_CURSOR FOR
            SELECT ID_ARCHIVO, ID_RECLAMO, ROL,
                   NOMBRE_ORIG, NOMBRE_SERVER,
                   MIME_TYPE, TAMANIO_BYTES,
                   USUARIO, FCH_CARGA
            FROM   SGC_RECLAMO_ARCHIVO
            WHERE  ID_RECLAMO = P_ID_RECLAMO
            ORDER  BY FCH_CARGA ASC, ID_ARCHIVO ASC;
    END P_OBTENER_ARCHIVOS;

    -- ── P_OBTENER_CLIENTES ──────────────────────────────────────
    PROCEDURE P_OBTENER_CLIENTES (
        P_BUSCAR IN  VARCHAR2 DEFAULT NULL,
        P_CURSOR OUT T_CURSOR
    ) IS
        V_BUSCAR VARCHAR2(300);
    BEGIN
        IF TRIM(P_BUSCAR) IS NOT NULL THEN
            V_BUSCAR := '%' || UPPER(TRIM(P_BUSCAR)) || '%';
        END IF;

        OPEN P_CURSOR FOR
            SELECT COD_CLIENTE,
                   NOMBRE      AS NOM_CLIENTE,
                   RUC         AS RUC_CLIENTE
            FROM   CLIENTES
            WHERE  (V_BUSCAR IS NULL
                    OR UPPER(NOMBRE)      LIKE V_BUSCAR
                    OR UPPER(COD_CLIENTE) LIKE V_BUSCAR
                    OR UPPER(RUC)         LIKE V_BUSCAR)
            ORDER BY NOMBRE;
    END P_OBTENER_CLIENTES;

    -- ── P_CREAR_RECLAMO ─────────────────────────────────────────
    PROCEDURE P_CREAR_RECLAMO (
        P_COD_CLIENTE IN  VARCHAR2,
        P_NOM_CLIENTE IN  VARCHAR2,
        P_CONTACTO    IN  VARCHAR2,
        P_TELEFONO    IN  VARCHAR2,
        P_ASUNTO      IN  VARCHAR2,
        P_DESCARGO    IN  VARCHAR2,
        P_USUARIO     IN  VARCHAR2,
        P_ID_RECLAMO  OUT NUMBER,
        P_MSGERROR    OUT VARCHAR2
    ) IS
        V_ID     NUMBER;
        V_ID_DES NUMBER;
        V_CNT    NUMBER;
    BEGIN
        P_MSGERROR   := NULL;
        P_ID_RECLAMO := NULL;

        -- Validaciones básicas
        IF TRIM(P_COD_CLIENTE) IS NULL THEN
            P_MSGERROR := 'Debe seleccionar un cliente.';
            RETURN;
        END IF;
        IF TRIM(P_CONTACTO) IS NULL THEN
            P_MSGERROR := 'El campo Contacto es obligatorio.';
            RETURN;
        END IF;
        IF TRIM(P_TELEFONO) IS NULL THEN
            P_MSGERROR := 'El campo Teléfono es obligatorio.';
            RETURN;
        END IF;
        IF TRIM(P_ASUNTO) IS NULL THEN
            P_MSGERROR := 'El campo Asunto es obligatorio.';
            RETURN;
        END IF;
        IF TRIM(P_DESCARGO) IS NULL THEN
            P_MSGERROR := 'Debe ingresar el descargo del vendedor.';
            RETURN;
        END IF;

        -- Verificar cliente existente
        SELECT COUNT(*) INTO V_CNT FROM CLIENTES
        WHERE  COD_CLIENTE = P_COD_CLIENTE;
        IF V_CNT = 0 THEN
            P_MSGERROR := 'El cliente ' || P_COD_CLIENTE || ' no existe.';
            RETURN;
        END IF;

        -- Insertar cabecera
        V_ID := SGC_RECLAMO_SEQ.NEXTVAL;

        INSERT INTO SGC_RECLAMO (
            ID_RECLAMO, COD_CLIENTE, NOM_CLIENTE,
            CONTACTO, TELEFONO, ASUNTO,
            ESTADO, USU_VENDEDOR, FCH_CREACION,
            A_ADUSER, A_ADFECHA, A_MDUSER, A_MDFECHA
        ) VALUES (
            V_ID, P_COD_CLIENTE, P_NOM_CLIENTE,
            P_CONTACTO, P_TELEFONO, P_ASUNTO,
            '01', P_USUARIO, SYSDATE,
            P_USUARIO, SYSDATE, P_USUARIO, SYSDATE
        );

        -- Insertar descargo inicial del vendedor
        V_ID_DES := SGC_RECLAMO_DESC_SEQ.NEXTVAL;
        INSERT INTO SGC_RECLAMO_DESCARGO (
            ID_DESCARGO, ID_RECLAMO, ROL,
            DESCRIPCION, USUARIO, FCH_REGISTRO
        ) VALUES (
            V_ID_DES, V_ID, 'VD',
            SUBSTR(P_DESCARGO, 1, 4000), P_USUARIO, SYSDATE
        );

        COMMIT;
        P_ID_RECLAMO := V_ID;

    EXCEPTION
        WHEN OTHERS THEN
            ROLLBACK;
            P_MSGERROR := SUBSTR('Error al crear reclamo: ' || SQLERRM, 1, 4000);
            P_ID_RECLAMO := NULL;
    END P_CREAR_RECLAMO;

    -- ── P_AGREGAR_DESCARGO ──────────────────────────────────────
    PROCEDURE P_AGREGAR_DESCARGO (
        P_ID_RECLAMO  IN  NUMBER,
        P_ROL         IN  VARCHAR2,
        P_DESCRIPCION IN  VARCHAR2,
        P_USUARIO     IN  VARCHAR2,
        P_ID_DESCARGO OUT NUMBER,
        P_MSGERROR    OUT VARCHAR2
    ) IS
        V_ID      NUMBER;
        V_ESTADO  SGC_RECLAMO.ESTADO%TYPE;
    BEGIN
        P_MSGERROR    := NULL;
        P_ID_DESCARGO := NULL;

        -- Validaciones
        IF P_ROL NOT IN ('VD','AC','GE') THEN
            P_MSGERROR := 'ROL inválido. Use VD, AC o GE.';
            RETURN;
        END IF;
        IF TRIM(P_DESCRIPCION) IS NULL THEN
            P_MSGERROR := 'El descargo no puede estar vacío.';
            RETURN;
        END IF;

        -- Verificar reclamo y obtener estado
        BEGIN
            SELECT ESTADO INTO V_ESTADO
            FROM   SGC_RECLAMO
            WHERE  ID_RECLAMO = P_ID_RECLAMO;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN
                P_MSGERROR := 'El reclamo ' || P_ID_RECLAMO || ' no existe.';
                RETURN;
        END;

        IF V_ESTADO IN ('04','05') THEN
            P_MSGERROR := 'No se puede agregar un descargo a un reclamo Aprobado o Rechazado.';
            RETURN;
        END IF;

        -- Insertar descargo
        V_ID := SGC_RECLAMO_DESC_SEQ.NEXTVAL;
        INSERT INTO SGC_RECLAMO_DESCARGO (
            ID_DESCARGO, ID_RECLAMO, ROL,
            DESCRIPCION, USUARIO, FCH_REGISTRO
        ) VALUES (
            V_ID, P_ID_RECLAMO, P_ROL,
            SUBSTR(P_DESCRIPCION, 1, 4000), P_USUARIO, SYSDATE
        );

        -- Si es el analista y el reclamo está Abierto → pasarlo a En Revisión
        IF P_ROL = 'AC' AND V_ESTADO = '01' THEN
            UPDATE SGC_RECLAMO
            SET    ESTADO       = '02',
                   USU_ANALISTA = P_USUARIO,
                   FCH_ANALISIS = SYSDATE,
                   A_MDUSER     = P_USUARIO,
                   A_MDFECHA    = SYSDATE
            WHERE  ID_RECLAMO = P_ID_RECLAMO;
        ELSE
            UPDATE SGC_RECLAMO
            SET    A_MDUSER  = P_USUARIO,
                   A_MDFECHA = SYSDATE
            WHERE  ID_RECLAMO = P_ID_RECLAMO;
        END IF;

        COMMIT;
        P_ID_DESCARGO := V_ID;

    EXCEPTION
        WHEN OTHERS THEN
            ROLLBACK;
            P_MSGERROR := SUBSTR('Error al agregar descargo: ' || SQLERRM, 1, 4000);
            P_ID_DESCARGO := NULL;
    END P_AGREGAR_DESCARGO;

    -- ── P_REGISTRAR_ARCHIVO ─────────────────────────────────────
    PROCEDURE P_REGISTRAR_ARCHIVO (
        P_ID_RECLAMO    IN  NUMBER,
        P_ROL           IN  VARCHAR2,
        P_NOMBRE_ORIG   IN  VARCHAR2,
        P_NOMBRE_SERVER IN  VARCHAR2,
        P_MIME_TYPE     IN  VARCHAR2,
        P_TAMANIO       IN  NUMBER,
        P_USUARIO       IN  VARCHAR2,
        P_ID_ARCHIVO    OUT NUMBER,
        P_MSGERROR      OUT VARCHAR2
    ) IS
        V_ID      NUMBER;
        V_ESTADO  SGC_RECLAMO.ESTADO%TYPE;
    BEGIN
        P_MSGERROR   := NULL;
        P_ID_ARCHIVO := NULL;

        IF P_ROL NOT IN ('VD','AC') THEN
            P_MSGERROR := 'ROL inválido.';
            RETURN;
        END IF;

        BEGIN
            SELECT ESTADO INTO V_ESTADO
            FROM   SGC_RECLAMO
            WHERE  ID_RECLAMO = P_ID_RECLAMO;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN
                P_MSGERROR := 'El reclamo ' || P_ID_RECLAMO || ' no existe.';
                RETURN;
        END;

        IF V_ESTADO IN ('03','04','05') THEN
            P_MSGERROR := 'No se puede adjuntar archivos a un reclamo cerrado, aprobado o rechazado.';
            RETURN;
        END IF;

        V_ID := SGC_RECLAMO_ARCH_SEQ.NEXTVAL;
        INSERT INTO SGC_RECLAMO_ARCHIVO (
            ID_ARCHIVO, ID_RECLAMO, ROL,
            NOMBRE_ORIG, NOMBRE_SERVER,
            MIME_TYPE, TAMANIO_BYTES,
            USUARIO, FCH_CARGA
        ) VALUES (
            V_ID, P_ID_RECLAMO, P_ROL,
            P_NOMBRE_ORIG, P_NOMBRE_SERVER,
            P_MIME_TYPE, P_TAMANIO,
            P_USUARIO, SYSDATE
        );

        -- Actualizar auditoría del reclamo
        UPDATE SGC_RECLAMO
        SET    A_MDUSER  = P_USUARIO,
               A_MDFECHA = SYSDATE
        WHERE  ID_RECLAMO = P_ID_RECLAMO;

        COMMIT;
        P_ID_ARCHIVO := V_ID;

    EXCEPTION
        WHEN OTHERS THEN
            ROLLBACK;
            P_MSGERROR := SUBSTR('Error al registrar archivo: ' || SQLERRM, 1, 4000);
            P_ID_ARCHIVO := NULL;
    END P_REGISTRAR_ARCHIVO;

    -- ── P_ELIMINAR_ARCHIVO ──────────────────────────────────────
    PROCEDURE P_ELIMINAR_ARCHIVO (
        P_ID_ARCHIVO IN  NUMBER,
        P_USUARIO    IN  VARCHAR2,
        P_MSGERROR   OUT VARCHAR2
    ) IS
        V_RECLAMO  NUMBER;
        V_ESTADO   SGC_RECLAMO.ESTADO%TYPE;
    BEGIN
        P_MSGERROR := NULL;

        BEGIN
            SELECT A.ID_RECLAMO, R.ESTADO
            INTO   V_RECLAMO, V_ESTADO
            FROM   SGC_RECLAMO_ARCHIVO A
            JOIN   SGC_RECLAMO         R ON R.ID_RECLAMO = A.ID_RECLAMO
            WHERE  A.ID_ARCHIVO = P_ID_ARCHIVO;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN
                P_MSGERROR := 'El archivo ' || P_ID_ARCHIVO || ' no existe.';
                RETURN;
        END;

        IF V_ESTADO IN ('03','04','05') THEN
            P_MSGERROR := 'No se puede eliminar archivos de un reclamo cerrado, aprobado o rechazado.';
            RETURN;
        END IF;

        DELETE FROM SGC_RECLAMO_ARCHIVO
        WHERE  ID_ARCHIVO = P_ID_ARCHIVO;

        UPDATE SGC_RECLAMO
        SET    A_MDUSER  = P_USUARIO,
               A_MDFECHA = SYSDATE
        WHERE  ID_RECLAMO = V_RECLAMO;

        COMMIT;

    EXCEPTION
        WHEN OTHERS THEN
            ROLLBACK;
            P_MSGERROR := SUBSTR('Error al eliminar archivo: ' || SQLERRM, 1, 4000);
    END P_ELIMINAR_ARCHIVO;

    -- ── P_CAMBIAR_ESTADO ────────────────────────────────────────
    PROCEDURE P_CAMBIAR_ESTADO (
        P_ID_RECLAMO IN  NUMBER,
        P_ESTADO     IN  VARCHAR2,
        P_USUARIO    IN  VARCHAR2,
        P_MSGERROR   OUT VARCHAR2
    ) IS
        V_ESTADO_ACT SGC_RECLAMO.ESTADO%TYPE;
    BEGIN
        P_MSGERROR := NULL;

        IF P_ESTADO NOT IN ('01','02','03') THEN
            P_MSGERROR := 'Estado inválido. Use: 01 Abierto, 02 En Revisión, 03 Cerrado.';
            RETURN;
        END IF;

        BEGIN
            SELECT ESTADO INTO V_ESTADO_ACT
            FROM   SGC_RECLAMO
            WHERE  ID_RECLAMO = P_ID_RECLAMO;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN
                P_MSGERROR := 'El reclamo ' || P_ID_RECLAMO || ' no existe.';
                RETURN;
        END;

        UPDATE SGC_RECLAMO
        SET    ESTADO    = P_ESTADO,
               A_MDUSER  = P_USUARIO,
               A_MDFECHA = SYSDATE
        WHERE  ID_RECLAMO = P_ID_RECLAMO;

        COMMIT;

    EXCEPTION
        WHEN OTHERS THEN
            ROLLBACK;
            P_MSGERROR := SUBSTR('Error al cambiar estado: ' || SQLERRM, 1, 4000);
    END P_CAMBIAR_ESTADO;

    -- ── P_ESCALAR_GERENCIA ──────────────────────────────────────
    PROCEDURE P_ESCALAR_GERENCIA (
        P_ID_RECLAMO IN  NUMBER,
        P_USUARIO    IN  VARCHAR2,
        P_MSGERROR   OUT VARCHAR2
    ) IS
        V_ESTADO SGC_RECLAMO.ESTADO%TYPE;
    BEGIN
        P_MSGERROR := NULL;
        BEGIN
            SELECT ESTADO INTO V_ESTADO FROM SGC_RECLAMO WHERE ID_RECLAMO = P_ID_RECLAMO;
        EXCEPTION WHEN NO_DATA_FOUND THEN
            P_MSGERROR := 'El reclamo ' || P_ID_RECLAMO || ' no existe.'; RETURN;
        END;
        IF V_ESTADO != '02' THEN
            P_MSGERROR := 'Solo se pueden escalar reclamos en estado "En Revision".'; RETURN;
        END IF;
        UPDATE SGC_RECLAMO SET ESTADO = '03', A_MDUSER = P_USUARIO, A_MDFECHA = SYSDATE
        WHERE  ID_RECLAMO = P_ID_RECLAMO;
        COMMIT;
    EXCEPTION
        WHEN OTHERS THEN ROLLBACK;
            P_MSGERROR := SUBSTR('Error al escalar a gerencia: ' || SQLERRM, 1, 4000);
    END P_ESCALAR_GERENCIA;

    -- ── P_APROBAR_RECLAMO ───────────────────────────────────────
    PROCEDURE P_APROBAR_RECLAMO (
        P_ID_RECLAMO  IN  NUMBER,
        P_OBSERVACION IN  VARCHAR2 DEFAULT NULL,
        P_USUARIO     IN  VARCHAR2,
        P_MSGERROR    OUT VARCHAR2
    ) IS
        V_ESTADO SGC_RECLAMO.ESTADO%TYPE;
        V_ID_DES NUMBER;
    BEGIN
        P_MSGERROR := NULL;
        BEGIN
            SELECT ESTADO INTO V_ESTADO FROM SGC_RECLAMO WHERE ID_RECLAMO = P_ID_RECLAMO;
        EXCEPTION WHEN NO_DATA_FOUND THEN
            P_MSGERROR := 'El reclamo ' || P_ID_RECLAMO || ' no existe.'; RETURN;
        END;
        IF V_ESTADO != '03' THEN
            P_MSGERROR := 'Solo se pueden aprobar reclamos en estado "Pendiente Aprobacion".'; RETURN;
        END IF;
        UPDATE SGC_RECLAMO
        SET    ESTADO = '04', USU_GERENTE = P_USUARIO, FCH_APROBACION = SYSDATE,
               A_MDUSER = P_USUARIO, A_MDFECHA = SYSDATE
        WHERE  ID_RECLAMO = P_ID_RECLAMO;
        IF TRIM(P_OBSERVACION) IS NOT NULL THEN
            V_ID_DES := SGC_RECLAMO_DESC_SEQ.NEXTVAL;
            INSERT INTO SGC_RECLAMO_DESCARGO (ID_DESCARGO, ID_RECLAMO, ROL, DESCRIPCION, USUARIO, FCH_REGISTRO)
            VALUES (V_ID_DES, P_ID_RECLAMO, 'GE', SUBSTR(P_OBSERVACION, 1, 4000), P_USUARIO, SYSDATE);
        END IF;
        COMMIT;
    EXCEPTION
        WHEN OTHERS THEN ROLLBACK;
            P_MSGERROR := SUBSTR('Error al aprobar reclamo: ' || SQLERRM, 1, 4000);
    END P_APROBAR_RECLAMO;

    -- ── P_RECHAZAR_RECLAMO ──────────────────────────────────────
    PROCEDURE P_RECHAZAR_RECLAMO (
        P_ID_RECLAMO IN  NUMBER,
        P_MOTIVO     IN  VARCHAR2,
        P_USUARIO    IN  VARCHAR2,
        P_MSGERROR   OUT VARCHAR2
    ) IS
        V_ESTADO SGC_RECLAMO.ESTADO%TYPE;
        V_ID_DES NUMBER;
    BEGIN
        P_MSGERROR := NULL;
        IF TRIM(P_MOTIVO) IS NULL THEN
            P_MSGERROR := 'Debe indicar el motivo del rechazo.'; RETURN;
        END IF;
        BEGIN
            SELECT ESTADO INTO V_ESTADO FROM SGC_RECLAMO WHERE ID_RECLAMO = P_ID_RECLAMO;
        EXCEPTION WHEN NO_DATA_FOUND THEN
            P_MSGERROR := 'El reclamo ' || P_ID_RECLAMO || ' no existe.'; RETURN;
        END;
        IF V_ESTADO NOT IN ('02','03') THEN
            P_MSGERROR := 'Solo se pueden rechazar reclamos en estado "En Revision" o "Pendiente Aprobacion".'; RETURN;
        END IF;
        UPDATE SGC_RECLAMO
        SET    ESTADO = '05', USU_GERENTE = P_USUARIO, FCH_APROBACION = SYSDATE,
               MOT_RECHAZO = P_MOTIVO,
               A_MDUSER = P_USUARIO, A_MDFECHA = SYSDATE
        WHERE  ID_RECLAMO = P_ID_RECLAMO;
        V_ID_DES := SGC_RECLAMO_DESC_SEQ.NEXTVAL;
        INSERT INTO SGC_RECLAMO_DESCARGO (ID_DESCARGO, ID_RECLAMO, ROL, DESCRIPCION, USUARIO, FCH_REGISTRO)
        VALUES (V_ID_DES, P_ID_RECLAMO, 'GE', SUBSTR('RECHAZO: ' || P_MOTIVO, 1, 4000), P_USUARIO, SYSDATE);
        COMMIT;
    EXCEPTION
        WHEN OTHERS THEN ROLLBACK;
            P_MSGERROR := SUBSTR('Error al rechazar reclamo: ' || SQLERRM, 1, 4000);
    END P_RECHAZAR_RECLAMO;

    -- ── P_ELIMINAR_RECLAMO ─────────────────────────────────────
    PROCEDURE P_ELIMINAR_RECLAMO (
        P_ID_RECLAMO     IN  NUMBER,
        P_USUARIO        IN  VARCHAR2,
        P_NOMBRES_SERVER OUT VARCHAR2,
        P_MSGERROR       OUT VARCHAR2
    ) IS
        V_CNT     NUMBER;
    BEGIN
        P_MSGERROR       := NULL;
        P_NOMBRES_SERVER := NULL;

        SELECT COUNT(*) INTO V_CNT FROM SGC_RECLAMO WHERE ID_RECLAMO = P_ID_RECLAMO;
        IF V_CNT = 0 THEN
            P_MSGERROR := 'El reclamo ' || P_ID_RECLAMO || ' no existe.'; RETURN;
        END IF;

        -- El controlador C# elimina la carpeta física completa (Directory.Delete),
        -- no necesita la lista de nombres. Se evita así el ORA-06502 por overflow
        -- de VARCHAR2 cuando hay muchos archivos adjuntos.
        P_NOMBRES_SERVER := NULL;

        -- Borrar archivos BD → descargos → cabecera
        DELETE FROM SGC_RECLAMO_ARCHIVO  WHERE ID_RECLAMO = P_ID_RECLAMO;
        DELETE FROM SGC_RECLAMO_DESCARGO WHERE ID_RECLAMO = P_ID_RECLAMO;
        DELETE FROM SGC_RECLAMO          WHERE ID_RECLAMO = P_ID_RECLAMO;

        COMMIT;
    EXCEPTION
        WHEN OTHERS THEN
            ROLLBACK;
            P_MSGERROR := SUBSTR('Error al eliminar reclamo: ' || SQLERRM, 1, 4000);
    END P_ELIMINAR_RECLAMO;

    -- ── P_GUARDAR_ANALISIS_CAUSA ───────────────────────────────
    PROCEDURE P_GUARDAR_ANALISIS_CAUSA (
        P_ID_RECLAMO IN  NUMBER,
        P_TEXTO      IN  VARCHAR2,
        P_USUARIO    IN  VARCHAR2,
        P_MSGERROR   OUT VARCHAR2
    ) IS
        V_ESTADO SGC_RECLAMO.ESTADO%TYPE;
    BEGIN
        P_MSGERROR := NULL;

        IF TRIM(P_TEXTO) IS NULL THEN
            P_MSGERROR := 'El Análisis de Causa no puede estar vacío.';
            RETURN;
        END IF;

        BEGIN
            SELECT ESTADO INTO V_ESTADO
            FROM   SGC_RECLAMO
            WHERE  ID_RECLAMO = P_ID_RECLAMO;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN
                P_MSGERROR := 'El reclamo ' || P_ID_RECLAMO || ' no existe.';
                RETURN;
        END;

        IF V_ESTADO = '05' THEN
            P_MSGERROR := 'No se puede registrar Análisis de Causa en un reclamo Rechazado.';
            RETURN;
        END IF;

        UPDATE SGC_RECLAMO
        SET    ANALISIS_CAUSA = SUBSTR(P_TEXTO, 1, 4000),
               A_MDUSER       = P_USUARIO,
               A_MDFECHA      = SYSDATE
        WHERE  ID_RECLAMO = P_ID_RECLAMO;

        COMMIT;
    EXCEPTION
        WHEN OTHERS THEN
            ROLLBACK;
            P_MSGERROR := SUBSTR('Error al guardar Análisis de Causa: ' || SQLERRM, 1, 4000);
    END P_GUARDAR_ANALISIS_CAUSA;

    -- ── P_GUARDAR_DECISION ─────────────────────────────────────
    PROCEDURE P_GUARDAR_DECISION (
        P_ID_RECLAMO IN  NUMBER,
        P_TEXTO      IN  VARCHAR2,
        P_USUARIO    IN  VARCHAR2,
        P_MSGERROR   OUT VARCHAR2
    ) IS
        V_ESTADO SGC_RECLAMO.ESTADO%TYPE;
    BEGIN
        P_MSGERROR := NULL;

        IF TRIM(P_TEXTO) IS NULL THEN
            P_MSGERROR := 'La Decisión no puede estar vacía.';
            RETURN;
        END IF;

        BEGIN
            SELECT ESTADO INTO V_ESTADO
            FROM   SGC_RECLAMO
            WHERE  ID_RECLAMO = P_ID_RECLAMO;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN
                P_MSGERROR := 'El reclamo ' || P_ID_RECLAMO || ' no existe.';
                RETURN;
        END;

        IF V_ESTADO != '04' THEN
            P_MSGERROR := 'La Decisión sólo puede registrarse cuando el reclamo está Aprobado.';
            RETURN;
        END IF;

        UPDATE SGC_RECLAMO
        SET    DECISION_FINAL = SUBSTR(P_TEXTO, 1, 4000),
               FCH_DECISION   = SYSDATE,
               USU_DECISION   = P_USUARIO,
               A_MDUSER       = P_USUARIO,
               A_MDFECHA      = SYSDATE
        WHERE  ID_RECLAMO = P_ID_RECLAMO;

        COMMIT;
    EXCEPTION
        WHEN OTHERS THEN
            ROLLBACK;
            P_MSGERROR := SUBSTR('Error al guardar Decisión: ' || SQLERRM, 1, 4000);
    END P_GUARDAR_DECISION;

    -- ── P_OBTENER_EMAIL_USUARIO ────────────────────────────────
    /*  Obtiene el email de un usuario a partir de su código (C_USER).
        Primero busca en CS_USER.C_EMAIL.
        Si no está disponible o es nulo, busca en CS_ANEXO.EMAIL vinculada por C_CODIGO.
        Solo busca usuarios activos (ESTADO='1'). */
    PROCEDURE P_OBTENER_EMAIL_USUARIO (
        P_COD_USUARIO   IN  VARCHAR2,
        P_EMAIL         OUT VARCHAR2,
        P_MSGERROR      OUT VARCHAR2
    ) IS
    BEGIN
        P_MSGERROR := NULL;
        P_EMAIL    := NULL;

        IF TRIM(P_COD_USUARIO) IS NULL THEN
            P_MSGERROR := 'El código de usuario no puede ser nulo.';
            RETURN;
        END IF;

        BEGIN
            -- Intenta obtener el email de CS_USER.C_EMAIL primero (es el más actualizado)
            SELECT U.C_EMAIL
            INTO   P_EMAIL
            FROM   CS_USER U
            WHERE  U.C_USER = P_COD_USUARIO
              AND  U.ESTADO = '1'
              AND  U.C_EMAIL IS NOT NULL;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN
                -- Si no lo encontró o está nulo, busca en CS_ANEXO por C_CODIGO
                BEGIN
                    SELECT AN.EMAIL
                    INTO   P_EMAIL
                    FROM   CS_USER U
                    JOIN   CS_ANEXO AN ON U.C_CODIGO = AN.C_CODIGO
                    WHERE  U.C_USER = P_COD_USUARIO
                      AND  U.ESTADO = '1'
                      AND  AN.EMAIL IS NOT NULL;
                EXCEPTION
                    WHEN NO_DATA_FOUND THEN
                        P_MSGERROR := 'Usuario ' || P_COD_USUARIO || ' no encontrado, no está activo o no tiene email asignado.';
                    WHEN OTHERS THEN
                        P_MSGERROR := SUBSTR('Error al obtener email en CS_ANEXO: ' || SQLERRM, 1, 4000);
                END;
            WHEN OTHERS THEN
                P_MSGERROR := SUBSTR('Error al obtener email en CS_USER: ' || SQLERRM, 1, 4000);
        END;

    END P_OBTENER_EMAIL_USUARIO;

    -- ── P_NOTIFICAR_CALIDAD ────────────────────────────────────
    --   El vendedor pulsa "Enviar a Calidad". Esta SP:
    --   1) Valida estado del reclamo ('01' o '02').
    --   2) Marca FCH_NOTI_CALIDAD = SYSDATE.
    --   3) Devuelve la lista de correos destinatarios separados por ';'.
    --   4) La capa C# realiza el envío SMTP.
    --
    --   Todos los parámetros OUT están protegidos con SUBSTR respetando
    --   los tamaños de columna de la tabla (ASUNTO=400, NOM_CLIENTE=200)
    --   para garantizar que NUNCA se produzca ORA-06502.
    --   DBMS_OUTPUT activo para debugging en Toad/SQL*Plus.
    PROCEDURE P_NOTIFICAR_CALIDAD (
        P_ID_RECLAMO    IN  NUMBER,
        P_USUARIO       IN  VARCHAR2,
        P_DESTINATARIOS OUT VARCHAR2,
        P_ASUNTO_MAIL   OUT VARCHAR2,
        P_NOM_CLIENTE   OUT VARCHAR2,
        P_MSGERROR      OUT VARCHAR2
    ) IS
        -- Variables internas con tamaño explícito >= tamaño de columna en BD
        V_ESTADO      VARCHAR2(2);
        V_ASUNTO      VARCHAR2(400);   -- SGC_RECLAMO.ASUNTO     VARCHAR2(400)
        V_CLIENTE     VARCHAR2(200);   -- SGC_RECLAMO.NOM_CLIENTE VARCHAR2(200)
        V_DEST        VARCHAR2(4000);  -- buffer igual al parámetro OUT de C#
        V_ASUNTO_MAIL VARCHAR2(4000);  -- buffer igual al parámetro OUT de C#
        V_NOM_CLIENTE VARCHAR2(4000);  -- buffer igual al parámetro OUT de C#
        V_MSGERROR    VARCHAR2(4000);  -- buffer igual al parámetro OUT de C#
    BEGIN
        DBMS_OUTPUT.PUT_LINE('[P_NOTIFICAR_CALIDAD] INICIO — reclamo=' || P_ID_RECLAMO || ' usuario=' || P_USUARIO);

        -- Inicializar parámetros OUT en NULL antes de cualquier operación
        P_MSGERROR      := NULL;
        P_DESTINATARIOS := NULL;
        P_ASUNTO_MAIL   := NULL;
        P_NOM_CLIENTE   := NULL;

        -- Paso 1: Leer datos del reclamo
        DBMS_OUTPUT.PUT_LINE('[P_NOTIFICAR_CALIDAD] Paso 1: Leyendo reclamo de BD');
        BEGIN
            SELECT ESTADO,
                   SUBSTR(NVL(ASUNTO,     ''), 1, 400),
                   SUBSTR(NVL(NOM_CLIENTE,''), 1, 200)
            INTO   V_ESTADO, V_ASUNTO, V_CLIENTE
            FROM   SGC_RECLAMO
            WHERE  ID_RECLAMO = P_ID_RECLAMO;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN
                V_MSGERROR := 'El reclamo ' || P_ID_RECLAMO || ' no existe.';
                DBMS_OUTPUT.PUT_LINE('[P_NOTIFICAR_CALIDAD] ERROR: ' || V_MSGERROR);
                P_MSGERROR := V_MSGERROR;
                RETURN;
        END;
        DBMS_OUTPUT.PUT_LINE('[P_NOTIFICAR_CALIDAD] Paso 1 OK — estado=' || V_ESTADO || ' asunto(30)=' || SUBSTR(V_ASUNTO, 1, 30));

        -- Paso 2: Validar estado
        DBMS_OUTPUT.PUT_LINE('[P_NOTIFICAR_CALIDAD] Paso 2: Validando estado');
        IF V_ESTADO NOT IN ('01','02') THEN
            V_MSGERROR := SUBSTR('Solo se puede notificar a Calidad cuando el reclamo esta Abierto o En Revision. Estado actual: ' || V_ESTADO, 1, 4000);
            DBMS_OUTPUT.PUT_LINE('[P_NOTIFICAR_CALIDAD] ERROR estado invalido: ' || V_MSGERROR);
            P_MSGERROR := V_MSGERROR;
            RETURN;
        END IF;
        DBMS_OUTPUT.PUT_LINE('[P_NOTIFICAR_CALIDAD] Paso 2 OK');

        -- Paso 3: Determinar destinatarios
        --   PRUEBAS: correo hardcodeado.
        --   PRODUCCION: descomentar el bloque SELECT para obtenerlos de CS_USER.
        DBMS_OUTPUT.PUT_LINE('[P_NOTIFICAR_CALIDAD] Paso 3: Asignando destinatarios');
        V_DEST := 'vmatencio@colonial.com.pe';
        -- TODO PRODUCCION: reemplazar bloque anterior por:
        -- BEGIN
        --     SELECT SUBSTR(
        --                (SELECT LISTAGG(NVL(U2.C_EMAIL, A2.EMAIL), ';')
        --                 FROM   (SELECT DISTINCT U3.C_USER, NVL(U3.C_EMAIL, A3.EMAIL) AS EMAIL
        --                         FROM   CS_USER U3
        --                         LEFT   JOIN CS_ANEXO A3 ON U3.C_CODIGO = A3.C_CODIGO
        --                         WHERE  U3.ACCESO_WEB LIKE 'Sgc%'
        --                           AND  U3.ESTADO = '1'
        --                           AND  NVL(U3.C_EMAIL, A3.EMAIL) IS NOT NULL) U2
        --                         LEFT JOIN CS_ANEXO A2 ON 1=0
        --                ), 1, 3900)
        --     INTO V_DEST FROM DUAL;
        -- EXCEPTION WHEN OTHERS THEN V_DEST := NULL; END;
        DBMS_OUTPUT.PUT_LINE('[P_NOTIFICAR_CALIDAD] Paso 3 OK — destinatarios(50)=' || SUBSTR(NVL(V_DEST,'(null)'), 1, 50));

        -- Paso 4: Actualizar FCH_NOTI_CALIDAD
        DBMS_OUTPUT.PUT_LINE('[P_NOTIFICAR_CALIDAD] Paso 4: Actualizando FCH_NOTI_CALIDAD');
        UPDATE SGC_RECLAMO
        SET    FCH_NOTI_CALIDAD = SYSDATE,
               A_MDUSER         = SUBSTR(NVL(P_USUARIO,'SYS'), 1, 30),
               A_MDFECHA        = SYSDATE
        WHERE  ID_RECLAMO = P_ID_RECLAMO;
        COMMIT;
        DBMS_OUTPUT.PUT_LINE('[P_NOTIFICAR_CALIDAD] Paso 4 OK — filas=' || SQL%ROWCOUNT);

        -- Paso 5: Construir asunto y nombre cliente con SUBSTR al tamaño de columna
        --   ASUNTO max 400 → prefijo 30 chars + id 10 → total <= 440 → substr a 400
        --   NOM_CLIENTE max 200 → substr a 200
        DBMS_OUTPUT.PUT_LINE('[P_NOTIFICAR_CALIDAD] Paso 5: Construyendo valores de salida');
        V_ASUNTO_MAIL := SUBSTR('Nuevo reclamo #' || TO_CHAR(P_ID_RECLAMO) || ' - ' || V_ASUNTO, 1, 400);
        V_NOM_CLIENTE := SUBSTR(V_CLIENTE, 1, 200);
        DBMS_OUTPUT.PUT_LINE('[P_NOTIFICAR_CALIDAD] Paso 5 OK — asunto_mail(50)=' || SUBSTR(V_ASUNTO_MAIL,1,50));

        -- Paso 6: Asignar parámetros OUT (siempre desde variables intermedias ya truncadas)
        DBMS_OUTPUT.PUT_LINE('[P_NOTIFICAR_CALIDAD] Paso 6: Asignando parametros OUT');
        P_DESTINATARIOS := V_DEST;
        P_ASUNTO_MAIL   := V_ASUNTO_MAIL;
        P_NOM_CLIENTE   := V_NOM_CLIENTE;
        DBMS_OUTPUT.PUT_LINE('[P_NOTIFICAR_CALIDAD] FIN OK');

    EXCEPTION
        WHEN OTHERS THEN
            ROLLBACK;
            V_MSGERROR    := SUBSTR('Error en P_NOTIFICAR_CALIDAD [reclamo ' || P_ID_RECLAMO || ']: ' || SQLERRM, 1, 4000);
            P_MSGERROR    := V_MSGERROR;
            P_DESTINATARIOS := NULL;
            P_ASUNTO_MAIL   := NULL;
            P_NOM_CLIENTE   := NULL;
            DBMS_OUTPUT.PUT_LINE('[P_NOTIFICAR_CALIDAD] EXCEPTION: ' || SUBSTR(SQLERRM, 1, 200));
    END P_NOTIFICAR_CALIDAD;

    -- ── P_NOTIFICAR_VENDEDOR_APROBADO ──────────────────────────
    --   Calidad pulsa "Avisar al vendedor". Sólo si ESTADO='04'.
    --   Devuelve el correo del vendedor.
    --   Todos los parámetros OUT protegidos con SUBSTR para evitar ORA-06502.
    --   DBMS_OUTPUT activo para debugging en Toad/SQL*Plus.
    PROCEDURE P_NOTIFICAR_VENDEDOR_APROBADO (
        P_ID_RECLAMO    IN  NUMBER,
        P_USUARIO       IN  VARCHAR2,
        P_DESTINATARIO  OUT VARCHAR2,
        P_ASUNTO_MAIL   OUT VARCHAR2,
        P_NOM_CLIENTE   OUT VARCHAR2,
        P_MSGERROR      OUT VARCHAR2
    ) IS
        -- Variables internas con tamaño explícito >= tamaño de columna en BD
        V_ESTADO      VARCHAR2(2);
        V_USU_VEND    VARCHAR2(30);    -- SGC_RECLAMO.USU_VENDEDOR VARCHAR2(30)
        V_ASUNTO      VARCHAR2(400);   -- SGC_RECLAMO.ASUNTO       VARCHAR2(400)
        V_CLIENTE     VARCHAR2(200);   -- SGC_RECLAMO.NOM_CLIENTE  VARCHAR2(200)
        V_DEST        VARCHAR2(4000);  -- buffer igual al parámetro OUT de C#
        V_ASUNTO_MAIL VARCHAR2(4000);  -- buffer igual al parámetro OUT de C#
        V_NOM_CLIENTE VARCHAR2(4000);  -- buffer igual al parámetro OUT de C#
        V_MSGERROR    VARCHAR2(4000);  -- buffer igual al parámetro OUT de C#
    BEGIN
        DBMS_OUTPUT.PUT_LINE('[P_NOTIFICAR_VENDEDOR_APROBADO] INICIO — reclamo=' || P_ID_RECLAMO || ' usuario=' || P_USUARIO);

        -- Inicializar parámetros OUT
        P_MSGERROR     := NULL;
        P_DESTINATARIO := NULL;
        P_ASUNTO_MAIL  := NULL;
        P_NOM_CLIENTE  := NULL;

        -- Paso 1: Leer datos del reclamo
        DBMS_OUTPUT.PUT_LINE('[P_NOTIFICAR_VENDEDOR_APROBADO] Paso 1: Leyendo reclamo de BD');
        BEGIN
            SELECT ESTADO,
                   SUBSTR(NVL(USU_VENDEDOR,''), 1, 30),
                   SUBSTR(NVL(NOM_CLIENTE, ''), 1, 200),
                   SUBSTR(NVL(ASUNTO,      ''), 1, 400)
            INTO   V_ESTADO, V_USU_VEND, V_CLIENTE, V_ASUNTO
            FROM   SGC_RECLAMO
            WHERE  ID_RECLAMO = P_ID_RECLAMO;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN
                V_MSGERROR := 'El reclamo ' || P_ID_RECLAMO || ' no existe.';
                DBMS_OUTPUT.PUT_LINE('[P_NOTIFICAR_VENDEDOR_APROBADO] ERROR: ' || V_MSGERROR);
                P_MSGERROR := V_MSGERROR;
                RETURN;
        END;
        DBMS_OUTPUT.PUT_LINE('[P_NOTIFICAR_VENDEDOR_APROBADO] Paso 1 OK — estado=' || V_ESTADO || ' vendedor=' || V_USU_VEND);

        -- Paso 2: Validar estado = '04' Aprobado
        DBMS_OUTPUT.PUT_LINE('[P_NOTIFICAR_VENDEDOR_APROBADO] Paso 2: Validando estado');
        IF V_ESTADO != '04' THEN
            V_MSGERROR := SUBSTR('Solo se puede notificar al vendedor cuando el reclamo esta Aprobado. Estado actual: ' || V_ESTADO, 1, 4000);
            DBMS_OUTPUT.PUT_LINE('[P_NOTIFICAR_VENDEDOR_APROBADO] ERROR estado invalido: ' || V_MSGERROR);
            P_MSGERROR := V_MSGERROR;
            RETURN;
        END IF;
        DBMS_OUTPUT.PUT_LINE('[P_NOTIFICAR_VENDEDOR_APROBADO] Paso 2 OK');

        -- Paso 3: Determinar destinatario
        --   PRUEBAS: correo hardcodeado.
        --   PRODUCCION: descomentar la llamada a P_OBTENER_EMAIL_USUARIO.
        DBMS_OUTPUT.PUT_LINE('[P_NOTIFICAR_VENDEDOR_APROBADO] Paso 3: Determinando destinatario (vendedor=' || V_USU_VEND || ')');
        V_DEST := 'vmatencio@colonial.com.pe';
        -- TODO PRODUCCION: reemplazar línea anterior por:
        -- BEGIN
        --     P_OBTENER_EMAIL_USUARIO(V_USU_VEND, V_DEST, V_MSGERROR);
        --     IF V_MSGERROR IS NOT NULL THEN
        --         DBMS_OUTPUT.PUT_LINE('[P_NOTIFICAR_VENDEDOR_APROBADO] ADVERTENCIA email: ' || V_MSGERROR);
        --         V_MSGERROR := NULL;   -- No fatal: continuamos sin correo
        --     END IF;
        -- END;
        DBMS_OUTPUT.PUT_LINE('[P_NOTIFICAR_VENDEDOR_APROBADO] Paso 3 OK — dest=' || NVL(V_DEST,'(null)'));

        -- Paso 4: Actualizar FCH_NOTI_VEND
        DBMS_OUTPUT.PUT_LINE('[P_NOTIFICAR_VENDEDOR_APROBADO] Paso 4: Actualizando FCH_NOTI_VEND');
        UPDATE SGC_RECLAMO
        SET    FCH_NOTI_VEND = SYSDATE,
               A_MDUSER      = SUBSTR(NVL(P_USUARIO,'SYS'), 1, 30),
               A_MDFECHA     = SYSDATE
        WHERE  ID_RECLAMO = P_ID_RECLAMO;
        COMMIT;
        DBMS_OUTPUT.PUT_LINE('[P_NOTIFICAR_VENDEDOR_APROBADO] Paso 4 OK — filas=' || SQL%ROWCOUNT);

        -- Paso 5: Construir asunto y nombre cliente con SUBSTR al tamaño de columna
        DBMS_OUTPUT.PUT_LINE('[P_NOTIFICAR_VENDEDOR_APROBADO] Paso 5: Construyendo valores de salida');
        V_ASUNTO_MAIL := SUBSTR(V_ASUNTO, 1, 400);
        V_NOM_CLIENTE := SUBSTR(V_CLIENTE, 1, 200);
        DBMS_OUTPUT.PUT_LINE('[P_NOTIFICAR_VENDEDOR_APROBADO] Paso 5 OK');

        -- Paso 6: Asignar parámetros OUT (siempre desde variables intermedias ya truncadas)
        DBMS_OUTPUT.PUT_LINE('[P_NOTIFICAR_VENDEDOR_APROBADO] Paso 6: Asignando parametros OUT');
        P_DESTINATARIO := V_DEST;
        P_ASUNTO_MAIL  := V_ASUNTO_MAIL;
        P_NOM_CLIENTE  := V_NOM_CLIENTE;
        DBMS_OUTPUT.PUT_LINE('[P_NOTIFICAR_VENDEDOR_APROBADO] FIN OK');

    EXCEPTION
        WHEN OTHERS THEN
            ROLLBACK;
            V_MSGERROR     := SUBSTR('Error en P_NOTIFICAR_VENDEDOR_APROBADO [reclamo ' || P_ID_RECLAMO || ']: ' || SQLERRM, 1, 4000);
            P_MSGERROR     := V_MSGERROR;
            P_DESTINATARIO := NULL;
            P_ASUNTO_MAIL  := NULL;
            P_NOM_CLIENTE  := NULL;
            DBMS_OUTPUT.PUT_LINE('[P_NOTIFICAR_VENDEDOR_APROBADO] EXCEPTION: ' || SUBSTR(SQLERRM, 1, 200));
    END P_NOTIFICAR_VENDEDOR_APROBADO;

    -- ── P_OBTENER_IMPRESION ────────────────────────────────────
    PROCEDURE P_OBTENER_IMPRESION (
        P_ID_RECLAMO    IN  NUMBER,
        P_CUR_CABECERA  OUT T_CURSOR,
        P_CUR_DESCARGOS OUT T_CURSOR,
        P_CUR_ARCHIVOS  OUT T_CURSOR,
        P_MSGERROR      OUT VARCHAR2
    ) IS
        V_ESTADO SGC_RECLAMO.ESTADO%TYPE;
    BEGIN
        P_MSGERROR := NULL;

        BEGIN
            SELECT ESTADO INTO V_ESTADO
            FROM   SGC_RECLAMO
            WHERE  ID_RECLAMO = P_ID_RECLAMO;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN
                P_MSGERROR := 'El reclamo ' || P_ID_RECLAMO || ' no existe.';
                RETURN;
        END;

        IF V_ESTADO != '04' THEN
            P_MSGERROR := 'Solo se puede imprimir un reclamo Aprobado.';
            RETURN;
        END IF;

        -- Cursor 1: Cabecera
        OPEN P_CUR_CABECERA FOR
            SELECT R.*,
                   C.RUC AS RUC_CLIENTE
            FROM   SGC_RECLAMO R
            LEFT   JOIN CLIENTES C ON C.COD_CLIENTE = R.COD_CLIENTE
            WHERE  R.ID_RECLAMO = P_ID_RECLAMO;

        -- Cursor 2: Descargos
        OPEN P_CUR_DESCARGOS FOR
            SELECT * FROM SGC_RECLAMO_DESCARGO
            WHERE  ID_RECLAMO = P_ID_RECLAMO
            ORDER  BY FCH_REGISTRO ASC;

        -- Cursor 3: Archivos
        OPEN P_CUR_ARCHIVOS FOR
            SELECT * FROM SGC_RECLAMO_ARCHIVO
            WHERE  ID_RECLAMO = P_ID_RECLAMO
            ORDER  BY FCH_CARGA ASC;

    EXCEPTION
        WHEN OTHERS THEN
            P_MSGERROR := SUBSTR('Error al obtener datos para impresión: ' || SQLERRM, 1, 4000);
    END P_OBTENER_IMPRESION;

END PKG_SGC_RECLAMO;
/
