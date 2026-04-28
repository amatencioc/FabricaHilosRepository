/*******************************************************************************
 * SP_SCA_REDONDEAR_TAREO_HE
 *
 * Aplica la regla de redondeo a hora entera de los campos de HE / DOBLES /
 * BANCO en SCA_ASISTENCIA_TAREO segun la regla del negocio:
 *
 *   - minutos < 45  -> trunca hacia abajo  (08:09 -> 08:00, 08:44 -> 08:00)
 *   - minutos >= 45 -> sube a la siguiente (08:45 -> 09:00, 08:59 -> 09:00)
 *
 *   formula:  nuevos_min = FLOOR((min_actuales + 15) / 60) * 60
 *
 * Campos redondeados:
 *   HORAEXTRA_AJUS, HORAEXOFI1, HORAEXOFI2, HORAEXOFI3,
 *   HORADOBLESOF, HORABANCOH
 *
 * NULL se preserva. 00:00 se preserva.
 *
 * USO:
 *   - Al final de PKG_SCA_DEPURA_TAREO.DEPURA_TOTAL (todos los empleados/dia).
 *   - Tras aplicar/revertir compensaciones en PKG_SCA_COMPENSACIONES (origen).
 *
 * Idempotente: re-ejecutar deja el mismo resultado.
 *
 * Autor: Equipo AQUARIUS  -  Fecha: 27/04/2026
 *******************************************************************************/

CREATE OR REPLACE PROCEDURE SP_SCA_REDONDEAR_TAREO_HE(
    p_cod_empresa  IN VARCHAR2 DEFAULT NULL,
    p_cod_personal IN VARCHAR2 DEFAULT NULL,
    p_fecha        IN DATE     DEFAULT NULL
) AS
    c_BASE CONSTANT DATE := TO_DATE('01/01/1900','dd/MM/yyyy');
BEGIN
    UPDATE SCA_ASISTENCIA_TAREO t
    SET    t.horaextra_ajus = CASE
              WHEN t.horaextra_ajus IS NULL THEN NULL
              ELSE c_BASE + FLOOR( ((t.horaextra_ajus - c_BASE) * 1440 + 15) / 60 ) / 24
           END,
           t.horaexofi1    = CASE
              WHEN t.horaexofi1 IS NULL THEN NULL
              ELSE c_BASE + FLOOR( ((t.horaexofi1 - c_BASE) * 1440 + 15) / 60 ) / 24
           END,
           t.horaexofi2    = CASE
              WHEN t.horaexofi2 IS NULL THEN NULL
              ELSE c_BASE + FLOOR( ((t.horaexofi2 - c_BASE) * 1440 + 15) / 60 ) / 24
           END,
           t.horaexofi3    = CASE
              WHEN t.horaexofi3 IS NULL THEN NULL
              ELSE c_BASE + FLOOR( ((t.horaexofi3 - c_BASE) * 1440 + 15) / 60 ) / 24
           END,
           t.horadoblesof  = CASE
              WHEN t.horadoblesof IS NULL THEN NULL
              ELSE c_BASE + FLOOR( ((t.horadoblesof - c_BASE) * 1440 + 15) / 60 ) / 24
           END,
           t.horabancoh    = CASE
              WHEN t.horabancoh IS NULL THEN NULL
              ELSE c_BASE + FLOOR( ((t.horabancoh - c_BASE) * 1440 + 15) / 60 ) / 24
           END
    WHERE  (p_cod_empresa  IS NULL OR t.cod_empresa  = p_cod_empresa)
    AND    (p_cod_personal IS NULL OR t.cod_personal = p_cod_personal)
    AND    (p_fecha        IS NULL OR t.fechamar     = p_fecha)
    -- solo si hay algo que redondear: alguno de los campos tiene minutos != 0
    AND    (
              MOD(ROUND((NVL(t.horaextra_ajus, c_BASE) - c_BASE) * 1440), 60) <> 0
           OR MOD(ROUND((NVL(t.horaexofi1,    c_BASE) - c_BASE) * 1440), 60) <> 0
           OR MOD(ROUND((NVL(t.horaexofi2,    c_BASE) - c_BASE) * 1440), 60) <> 0
           OR MOD(ROUND((NVL(t.horaexofi3,    c_BASE) - c_BASE) * 1440), 60) <> 0
           OR MOD(ROUND((NVL(t.horadoblesof,  c_BASE) - c_BASE) * 1440), 60) <> 0
           OR MOD(ROUND((NVL(t.horabancoh,    c_BASE) - c_BASE) * 1440), 60) <> 0
           );
END SP_SCA_REDONDEAR_TAREO_HE;
/

SHOW ERRORS PROCEDURE SP_SCA_REDONDEAR_TAREO_HE;
