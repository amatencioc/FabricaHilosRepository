-- ============================================================
-- Secuencia para generar el ID_GRUPO de los ítems de
-- requerimientos (tabla DETREQ, campo ID_GRUPO)
-- Ejecutar con el usuario propietario del esquema (VICMATE o similar)
-- ============================================================

-- Verificar si ya existe antes de crear
-- SELECT SEQUENCE_NAME FROM USER_SEQUENCES WHERE SEQUENCE_NAME = 'LG_GRUPO_SEQ';

CREATE SEQUENCE LG_GRUPO_SEQ
    START WITH 1
    INCREMENT BY 1
    NOCACHE
    NOCYCLE
    NOORDER;

-- Verificar creación
-- SELECT LG_GRUPO_SEQ.NEXTVAL FROM DUAL;
