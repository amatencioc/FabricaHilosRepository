-- ============================================================
-- Salud Ocupacional – Inspección Comedor
-- Secuencias faltantes
-- Ejecutar en el schema SIG (o el schema activo del sistema)
-- ============================================================

-- Secuencia para SO_INSP_HALLAZGO
CREATE SEQUENCE SIG.SO_HALLAZGO_SEQ
	START WITH 1
	INCREMENT BY 1
	NOCACHE
	NOCYCLE;

-- Secuencia para SO_HALLAZGO_IMG
CREATE SEQUENCE SIG.SO_HALLAZGO_IMG_SEQ
	START WITH 1
	INCREMENT BY 1
	NOCACHE
	NOCYCLE;
