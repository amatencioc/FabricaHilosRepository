-- =============================================================================
-- SIRE - Tablas Oracle (esquema SIG)
-- Ejecutar en Toad conectado como SIG/STARK en 10.0.7.11:1521/ORCL
-- =============================================================================

-- -----------------------------------------------------------------------------
-- SIRE_JOB: Jobs de exportación asíncrona de propuesta SIRE (reemplaza SQLite)
-- -----------------------------------------------------------------------------
CREATE TABLE SIG.SIRE_JOB (
	ID              NUMBER(10,0)    NOT NULL,
	JOB_ID          VARCHAR2(32)    NOT NULL,       -- GUID sin guiones
	TIPO_REGISTRO   VARCHAR2(10)    NOT NULL,       -- 'ventas' | 'compras'
	PERIODO         VARCHAR2(6)     NOT NULL,       -- YYYYMM
	USUARIO_ID      VARCHAR2(450),
	ESTADO          VARCHAR2(20)    NOT NULL,       -- Pendiente | EnProceso | Completado | Error
	NUM_TICKET      VARCHAR2(100),                  -- Ticket SUNAT
	NOMBRE_ARCHIVO  VARCHAR2(300),                  -- Nombre del ZIP generado
	RUTA_ARCHIVO    VARCHAR2(500),                  -- Ruta UNC donde se guardó
	COD_TIPO_ARCHIVO VARCHAR2(10),
	COD_PROCESO     VARCHAR2(50),
	REG_INSERTADOS  NUMBER(10,0),
	REG_DUPLICADOS  NUMBER(10,0),
	MENSAJE_ERROR   VARCHAR2(2000),
	FECHA_CREACION  DATE            NOT NULL,
	FECHA_ACT       DATE            NOT NULL,
	FECHA_FIN       DATE,
	CONSTRAINT PK_SIRE_JOB PRIMARY KEY (ID),
	CONSTRAINT UK_SIRE_JOB_JOBID UNIQUE (JOB_ID)
);

CREATE SEQUENCE SIG.SEQ_SIRE_JOB
	START WITH 1
	INCREMENT BY 1
	NOCACHE
	NOCYCLE;

CREATE INDEX SIG.IDX_SIRE_JOB_ESTADO ON SIG.SIRE_JOB (ESTADO, FECHA_CREACION DESC);
CREATE INDEX SIG.IDX_SIRE_JOB_TIPO_PER ON SIG.SIRE_JOB (TIPO_REGISTRO, PERIODO, ESTADO);

COMMENT ON TABLE  SIG.SIRE_JOB IS 'Jobs de exportación asíncrona de propuesta SIRE (RVIE/RCE)';
COMMENT ON COLUMN SIG.SIRE_JOB.JOB_ID IS 'GUID único retornado al frontend para consultar estado';
COMMENT ON COLUMN SIG.SIRE_JOB.ESTADO IS 'Pendiente | EnProceso | Completado | Error';
COMMENT ON COLUMN SIG.SIRE_JOB.NUM_TICKET IS 'Número de ticket retornado por SUNAT al exportar';

-- NOTA: SIRE_HEALTH fue eliminada (health checks OAuth2 removidos).
-- Para limpiar la BD ejecutar: DROP TABLE SIG.SIRE_HEALTH; DROP SEQUENCE SIG.SEQ_SIRE_HEALTH;

-- -----------------------------------------------------------------------------
-- SIRE_LOG: Auditoría de cada llamada HTTP a SUNAT (nueva tabla)
-- -----------------------------------------------------------------------------
CREATE TABLE SIG.SIRE_LOG (
	ID              NUMBER(10,0)    NOT NULL,
	FECHA           DATE            NOT NULL,
	JOB_ID          VARCHAR2(32),                  -- NULL si no está asociado a un job
	OPERACION       VARCHAR2(30)    NOT NULL,       -- AUTH | EXPORTAR | TICKET | DESCARGAR | HEALTH
	METODO_HTTP     VARCHAR2(10),                   -- GET | POST
	URL             VARCHAR2(1000),
	HTTP_STATUS     NUMBER(5,0),                    -- 200 | 422 | 504 etc.
	DURACION_MS     NUMBER(10,0),
	EXITO           NUMBER(1,0)     DEFAULT 0,      -- 1=OK, 0=error
	MENSAJE         VARCHAR2(2000),                 -- Resumen del resultado / error
	CONSTRAINT PK_SIRE_LOG PRIMARY KEY (ID)
);

CREATE SEQUENCE SIG.SEQ_SIRE_LOG
	START WITH 1
	INCREMENT BY 1
	NOCACHE
	NOCYCLE;

CREATE INDEX SIG.IDX_SIRE_LOG_FECHA    ON SIG.SIRE_LOG (FECHA DESC);
CREATE INDEX SIG.IDX_SIRE_LOG_JOB_ID   ON SIG.SIRE_LOG (JOB_ID);
CREATE INDEX SIG.IDX_SIRE_LOG_OPERACION ON SIG.SIRE_LOG (OPERACION, FECHA DESC);

COMMENT ON TABLE  SIG.SIRE_LOG IS 'Auditoría de llamadas HTTP al API SUNAT-SIRE';
COMMENT ON COLUMN SIG.SIRE_LOG.OPERACION IS 'AUTH | EXPORTAR | TICKET | DESCARGAR | HEALTH';
COMMENT ON COLUMN SIG.SIRE_LOG.JOB_ID IS 'FK lógica a SIRE_JOB.JOB_ID (puede ser NULL)';
COMMENT ON COLUMN SIG.SIRE_LOG.MENSAJE IS 'Resumen del resultado: ticket obtenido, estado SUNAT, mensaje de error, etc.';

-- =============================================================================
-- Fin del script inicial. Verificar con:
--   SELECT TABLE_NAME FROM USER_TABLES WHERE TABLE_NAME LIKE 'SIRE_%';
--   SELECT SEQUENCE_NAME FROM USER_SEQUENCES WHERE SEQUENCE_NAME LIKE 'SEQ_SIRE_%';
-- =============================================================================

-- =============================================================================
-- PATCH v2 — Extender SIRE_JOB para trazabilidad de todas las operaciones SUNAT
-- Ejecutar UNA SOLA VEZ sobre la BD existente.
-- =============================================================================
ALTER TABLE SIG.SIRE_JOB ADD (
    TIPO_OPERACION      VARCHAR2(15)  DEFAULT 'EXPORTAR' NOT NULL,
    -- EXPORTAR | ACEPTAR | CERRAR | REEMPLAZAR
    RUTA_ARCHIVO_ORIGEN VARCHAR2(500),
    -- Solo para REEMPLAZAR: ruta local del TXT/ZIP subido a SUNAT via TUS.
    -- NULL en EXPORTAR / ACEPTAR / CERRAR.
    URL_DESCARGA        VARCHAR2(1000)
    -- URL del servicio 5.17 usada para descargar el archivo resultado (RUTA_ARCHIVO).
    -- NULL en EXPORTAR (la URL la gestiona internamente el worker).
);

COMMENT ON COLUMN SIG.SIRE_JOB.TIPO_OPERACION      IS 'EXPORTAR | ACEPTAR | CERRAR | REEMPLAZAR';
COMMENT ON COLUMN SIG.SIRE_JOB.RUTA_ARCHIVO_ORIGEN IS 'REEMPLAZAR: ruta local del archivo subido a SUNAT via TUS';
COMMENT ON COLUMN SIG.SIRE_JOB.URL_DESCARGA        IS 'URL servicio 5.17 para re-descargar el resultado de SUNAT';

CREATE INDEX SIG.IDX_SIRE_JOB_OP_PER
    ON SIG.SIRE_JOB (TIPO_OPERACION, TIPO_REGISTRO, PERIODO);

-- =============================================================================
-- Fin PATCH v2.
-- =============================================================================
