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

-- -----------------------------------------------------------------------------
-- SIRE_HEALTH: Logs de health check periódico (reemplaza SQLite)
-- -----------------------------------------------------------------------------
CREATE TABLE SIG.SIRE_HEALTH (
	ID              NUMBER(10,0)    NOT NULL,
	FECHA           DATE            NOT NULL,
	ESTADO          VARCHAR2(20)    NOT NULL,       -- Healthy | Degraded | Unhealthy
	TOKEN_OK        NUMBER(1,0)     DEFAULT 0,      -- 1=sí, 0=no
	RVIE_OK         NUMBER(1,0)     DEFAULT 0,
	RVIE_PERIODOS   NUMBER(5,0),
	RCE_OK          NUMBER(1,0)     DEFAULT 0,
	RCE_PERIODOS    NUMBER(5,0),
	DURACION_MS     NUMBER(10,0),
	MENSAJE_ERROR   VARCHAR2(2000),
	CONSTRAINT PK_SIRE_HEALTH PRIMARY KEY (ID)
);

CREATE SEQUENCE SIG.SEQ_SIRE_HEALTH
	START WITH 1
	INCREMENT BY 1
	NOCACHE
	NOCYCLE;

CREATE INDEX SIG.IDX_SIRE_HEALTH_FECHA ON SIG.SIRE_HEALTH (FECHA DESC);

COMMENT ON TABLE  SIG.SIRE_HEALTH IS 'Historial de health checks del servicio SIRE';
COMMENT ON COLUMN SIG.SIRE_HEALTH.TOKEN_OK IS '1=token obtenido OK, 0=falló';
COMMENT ON COLUMN SIG.SIRE_HEALTH.ESTADO IS 'Healthy | Degraded | Unhealthy';

-- -----------------------------------------------------------------------------
-- SIRE_LOG: Auditoría de cada llamada HTTP a SUNAT (nueva tabla)
-- Permite investigar qué pasó en cada operación sin necesidad de logs de consola
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
-- Fin del script. Verificar con:
--   SELECT TABLE_NAME FROM USER_TABLES WHERE TABLE_NAME LIKE 'SIRE_%';
--   SELECT SEQUENCE_NAME FROM USER_SEQUENCES WHERE SEQUENCE_NAME LIKE 'SEQ_SIRE_%';
-- =============================================================================
