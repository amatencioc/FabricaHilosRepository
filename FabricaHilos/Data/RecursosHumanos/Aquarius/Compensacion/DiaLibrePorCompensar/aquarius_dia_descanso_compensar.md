# Aquarius — Día de Descanso por Compensar (DDC) — Análisis completo (09/05/2026)

## CONCEPTO
Un empleado con horario rotativo tiene días "de descanso compensatorio" dentro de su semana laboral.
Esos días aparecen en SCA_ASISTENCIA_TAREO como FALTA TOTAL (alerta02='FT') porque el empleado
no marcó, pero NO son ausencias formales (sin permiso, sin descanso médico, sin licencias).
Las HE simples (horaextra_ajus) de los días laborales del mismo período "pagan" esos días.

## CASO DE EJEMPLO
```
L  = Lunes     → TRABAJA 8H + 4H HE_simples  (alerta06='EE', horaextra_ajus=04:00)
M  = Martes    → DDC (alerta02='FT', horas_falta=08:00, sin per_*, sin descanso)
Mi = Miércoles → TRABAJA 8H + 4H HE_simples
J  = Jueves    → DDC (alerta02='FT', horas_falta=08:00)
V  = Viernes   → TRABAJA 8H + 4H HE_simples
S  = Sábado    → TRABAJA 8H + 4H HE_simples
D  = Domingo   → DESCANSO OBLIGATORIO (descanso='S')
```
Total HE disponibles: 4×4 = 16H = 960 min. Total DDC: 2×8 = 16H = 960 min → compensación total.

## CRITERIOS DDC (identificación por exclusión en SCA_ASISTENCIA_TAREO)
```sql
alerta02 = 'FT'               -- falta total (sin marcación)
horas_falta IS NOT NULL       -- tiene horas de falta registradas
descanso = 'N'                -- no es descanso semanal obligatorio
NVL(per_dia_comp,'N') = 'N'  -- sin permiso día completo
per_desc_med IS NULL          -- sin descanso médico
per_vaca IS NULL              -- sin vacaciones
per_subsidio IS NULL          -- sin subsidio
per_suspension IS NULL        -- sin sanción
per_lic_sind IS NULL          -- sin licencia sindical
per_lic_pat IS NULL           -- sin licencia paternidad
per_lic_fac IS NULL           -- sin licencia fallecimiento
per_goce_fis IS NULL          -- sin goce físico
per_goce IS NULL              -- sin permiso con goce
per_sgoce IS NULL             -- sin permiso sin goce
```
> **NOTA**: El sistema no distingue automáticamente DDC de falta normal. El operador
> selecciona manualmente desde el listado qué días son realmente DDC.

## PARÁMETROS DE COMPENSACIÓN
| Campo            | Valor          |
|------------------|----------------|
| tipoorigen       | 'E' (horaextra_ajus) |
| tipocompensacion | 'F' (horas_falta)    |
| aux1             | 'D'||id_evento       |
| tiempo           | MIN(falta_min, he_disponible) en MINUTOS |

## DISTRIBUCIÓN HE → DDC (rango semana)
- Pool de HE: suma de horaextra_ajus de todos los días laborales del rango
- Consumo: recorre DDC en orden cronológico, asignando HE día por día hasta cubrir cada DDC
- Compensación parcial: permitida. Si HE < horas_falta DDC → se compensa lo disponible
- Múltiples INSERT en SCA_COMPENSACION: uno por cada par (fechaorigen, fechadestino) cuando
  un DDC necesita HE de varios días origen

## EFECTOS EN TAREO
### Día HE (origen tipo='E')
Misma lógica que PKG_SCA_COMP_HE_SIMPLE / prv_aplicar_origen_E:
- horaextra_ajus -= tiempo
- Recalcula tramos horaexofi1/2/3
- Llama SP_SCA_REDONDEAR_TAREO_HE
- alerta06='EC' si horaextra_ajus llega a 0

### Día DDC (destino tipo='F', compensación parcial)
- horas_falta -= tiempo (si resta > 0: mantiene 'FT'; si resta = 0: horas_falta=NULL, alerta02='FC')
- horaefectiva += tiempo

## PAQUETE: PKG_SCA_COMP_DDC
- Archivo: `Compensaciones/PKG_SCA_COMP_DDC.sql`
- GGT: `SCA_TMP_DDC_RES`

### Procedimientos:
1. `LISTAR_DDC_RANGO` — muestra días candidatos DDC + días HE del período por empleado
2. `CALCULAR_DDC` — preview READ-ONLY de distribución HE→DDC
3. `REGISTRAR_DDC_MASIVO` — aplica compensación, devuelve GGT
4. `APLICAR_DIA_DDC` — integración PASO 15 (reaplica/revierte si no cuadra)
5. `CONSULTAR_RANGO_DDC` — auditoría por rango (aux1 LIKE 'D%')
6. `CONSULTAR_EVENTO_DDC` — auditoría por evento específico

## ADVERTENCIA REDONDEO
Igual que COMP_HE_SIMPLE: horaextra_ajus post-deducción puede redondear hacia abajo.
Ej: 04:00 HE - 03:50 usados = 00:10 → redondea a 00:00 → PASO 15 auto-elimina.
CALCULAR_DDC muestra `min_he_post_round` y emite `ADVERTENCIA_REDONDEO`.

## DIFERENCIAS VS PKG_SCA_COMP_HE_SIMPLE
| Aspecto | HE_SIMPLE | DDC |
|---------|-----------|-----|
| Días origen | 1 específico | N días del rango |
| Días destino | 1 específico | N DDC del rango |
| tipocompensacion | T/A/N/F/P | siempre 'F' |
| aux1 | 'H'||id | 'D'||id |
| INSERT por empleado | 1 | N (uno por par origen-destino) |
| Comp. parcial | NO (LEAST exacto) | SÍ (reduce horas_falta) |
