-- =============================================================================
-- Script: 01_crear_FH_LECTCORREOS_FACTURAS.sql
-- Descripción: Crea la tabla de control CDR para el servicio LecturaCorreos
-- Usuario Oracle: VICMATE
-- =============================================================================

-- Tabla principal
CREATE TABLE FH_LECTCORREOS_FACTURAS (
    ID                      NUMBER(19)       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    RUC                     VARCHAR2(11)     NOT NULL,
    TIPO_COMPROBANTE        VARCHAR2(2)      NOT NULL,   -- 01=Factura, 03=Boleta, 07=NC, 08=ND
    SERIE                   VARCHAR2(4)      NOT NULL,
    CORRELATIVO             NUMBER(8)        NOT NULL,
    ESTADO                  VARCHAR2(20)     DEFAULT 'PENDIENTE_CDR' NOT NULL,
                                            -- PENDIENTE_CDR | ACEPTADO | RECHAZADO | ERROR
    CODIGO_RESPUESTA_SUNAT  VARCHAR2(10),
    MENSAJE_SUNAT           VARCHAR2(1000),
    CDR_CONTENIDO           BLOB,           -- ZIP del CDR retornado por SUNAT
    MENSAJE_ERROR           VARCHAR2(2000),
    FECHA_CREACION          DATE             DEFAULT SYSDATE NOT NULL,
    FECHA_CONSULTA_SUNAT    DATE,
    INTENTOS                NUMBER(3)        DEFAULT 0 NOT NULL,
    DOCUMENTO_ID            NUMBER(19),     -- FK opcional hacia tabla de documentos
    DOCUMENTO_REFERENCIA    VARCHAR2(100)   -- Nro pedido, guía u otra referencia
);

-- Índice para la consulta principal del SunatCdrWorker
CREATE INDEX IDX_FLCF_ESTADO_INTENTOS
    ON FH_LECTCORREOS_FACTURAS (ESTADO, INTENTOS, FECHA_CREACION);

-- Índice de búsqueda por clave del comprobante
CREATE UNIQUE INDEX IDX_FLCF_COMPROBANTE
    ON FH_LECTCORREOS_FACTURAS (RUC, TIPO_COMPROBANTE, SERIE, CORRELATIVO);

-- Comentarios de columnas
COMMENT ON TABLE  FH_LECTCORREOS_FACTURAS                       IS 'Comprobantes leídos de correo pendientes de verificación CDR en SUNAT';
COMMENT ON COLUMN FH_LECTCORREOS_FACTURAS.ID                    IS 'PK autoincremental';
COMMENT ON COLUMN FH_LECTCORREOS_FACTURAS.RUC                   IS 'RUC del emisor del comprobante';
COMMENT ON COLUMN FH_LECTCORREOS_FACTURAS.TIPO_COMPROBANTE      IS '01=Factura, 03=Boleta, 07=Nota Crédito, 08=Nota Débito';
COMMENT ON COLUMN FH_LECTCORREOS_FACTURAS.SERIE                 IS 'Serie del comprobante (ej: F001)';
COMMENT ON COLUMN FH_LECTCORREOS_FACTURAS.CORRELATIVO           IS 'Número correlativo';
COMMENT ON COLUMN FH_LECTCORREOS_FACTURAS.ESTADO                IS 'PENDIENTE_CDR | ACEPTADO | RECHAZADO | ERROR';
COMMENT ON COLUMN FH_LECTCORREOS_FACTURAS.CODIGO_RESPUESTA_SUNAT IS 'Código retornado por SUNAT (ej: 0=Aceptado)';
COMMENT ON COLUMN FH_LECTCORREOS_FACTURAS.MENSAJE_SUNAT         IS 'Descripción de la respuesta SUNAT';
COMMENT ON COLUMN FH_LECTCORREOS_FACTURAS.CDR_CONTENIDO         IS 'ZIP del CDR retornado por SUNAT en binario';
COMMENT ON COLUMN FH_LECTCORREOS_FACTURAS.MENSAJE_ERROR         IS 'Detalle del error si el proceso falló';
COMMENT ON COLUMN FH_LECTCORREOS_FACTURAS.FECHA_CREACION        IS 'Fecha y hora de ingreso del registro';
COMMENT ON COLUMN FH_LECTCORREOS_FACTURAS.FECHA_CONSULTA_SUNAT  IS 'Última fecha de consulta a SUNAT';
COMMENT ON COLUMN FH_LECTCORREOS_FACTURAS.INTENTOS              IS 'Cantidad de intentos de consulta SUNAT (máx 5)';
COMMENT ON COLUMN FH_LECTCORREOS_FACTURAS.DOCUMENTO_ID          IS 'ID del documento en tabla de documentos logística';
COMMENT ON COLUMN FH_LECTCORREOS_FACTURAS.DOCUMENTO_REFERENCIA  IS 'Referencia libre (Nro pedido, guía, etc.)';

COMMIT;
