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
-- PLUS: verificar que NO existe evento LOGIX bloqueante (ver sección LOGIX abajo)
```
> **NOTA**: El sistema no distingue automáticamente DDC de falta normal. El operador
> selecciona manualmente desde el listado qué días son realmente DDC.

---

## BUG CORREGIDO — INTEGRACIÓN LOGIX (14/05/2026)

### Síntoma
Un empleado con `alerta02='FT'` y sin ningún `per_xxx` en AQUARIUS aparecía como candidato
DDC compensable, pero en LOGIX (SIG.RH_EVENTOS) tenía registrado un evento `C_TIPO='07'`
(FALTA NO JUSTIFICADA). Al compensar ese día se generaba una compensación incorrecta.

### Caso concreto
- **QUISPE PICOY, NESTOR** (fotocheck 034675, cod_personal 004706, empresa 0003)
- 09/05/2026: alerta02='FT', h_falta=08:00, sin per_xxx → pasaba todos los filtros AQUARIUS
- `SIG.RH_EVENTOS`: C_TIPO='07', C_MOTIVO='13' (VIAJE), D_INICIO=D_FINAL=09/05/2026
- Resultado incorrecto: paquete lo trataba como DDC y lo compensaba
- **Contraste**: MUÑOZ POLACK WALTER (034076, cod 000430) sí debía compensarse — sin eventos LOGIX

### Root cause
El trigger `SIG.TIA_RH_EVENTOS_AQUARIUS` (AFTER INSERT ON SIG.RH_EVENTOS) solo sincroniza
los tipos: `'01'` SUSP, `'05'` PSGO, `'20'` DMED, `'21'` SUBI, `'22'` SUBI, `'23'` VACA,
`'25'` LSIN, `'26'` PGOC, `'28'` LPAT → escribe en SCA_ASISTENCIA_TAREO via SP_SCA_INSERT_PERPERSON_SIG.

El tipo `'07'` (FALTA NO JUSTIFICADA) **NO está en el trigger** → los per_xxx quedan NULL en AQUARIUS
→ PKG_SCA_COMP_DDC no puede detectar la ausencia formal.

### Join clave AQUARIUS ↔ SIG
```sql
-- SIG.RH_EVENTOS.C_CODIGO = SCA_FOTOCHECK.NUM_FOTOCHECK
-- NUM_FOTOCHECK es el código de tarjeta (ej: '034675')
-- C_CODIGO en SIG = mismo código; los 2 primeros dígitos indican empresa ('01','02','03')
SELECT re.c_codigo, re.c_tipo, re.d_inicio, re.d_final
FROM SIG.RH_EVENTOS re
JOIN SCA_FOTOCHECK sf ON sf.num_fotocheck = re.c_codigo
                      AND sf.cod_empresa = :emp
                      AND sf.cod_personal = :per
                      AND NVL(sf.act_fotocheck,1) = 1
WHERE re.c_tipo = '07'
  AND re.d_inicio <= :fecha
  AND NVL(re.d_final, re.d_inicio) >= :fecha
```

### Fix aplicado (PKG_SCA_Comp_DDC.sql)
**1. `prv_cargar_ddc`** — cursor DDC candidatos: agrega NOT EXISTS contra SIG.RH_EVENTOS C_TIPO='07'
**2. `LISTAR_DDC_RANGO`** — rama DDC del UNION ALL: igual NOT EXISTS; agrega nueva rama
`BLOQ_LOGIX` para que la UI muestre los días bloqueados e informe al operador.

### Tipos de eventos SIG.RH_EVENTOS
| C_TIPO | Significado | Sincronizado a AQUARIUS | Bloquea DDC |
|--------|-------------|------------------------|-------------|
| '01'   | Suspensión  | SÍ (per_suspension)    | ya por AQUARIUS |
| '05'   | Permiso c/goce | SÍ (per_goce)       | ya por AQUARIUS |
| '07'   | **FALTA NO JUSTIFICADA** | **NO** | **SÍ — fix aplicado** |
| '20'   | Descanso médico | SÍ (per_desc_med) | ya por AQUARIUS |
| '21'   | Subsidio incap. | SÍ (per_subsidio) | ya por AQUARIUS |
| '22'   | Subsidio       | SÍ (per_subsidio)  | ya por AQUARIUS |
| '23'   | Vacaciones    | SÍ (per_vaca)       | ya por AQUARIUS |
| '25'   | Lic. sin goce | SÍ (per_lic_sind)  | ya por AQUARIUS |
| '26'   | Permiso c/goce (otro) | SÍ             | ya por AQUARIUS |
| '28'   | Lic. paternidad | SÍ (per_lic_pat) | ya por AQUARIUS |
| '52'   | Permiso (parada planta, personal) | NO | Evaluar caso a caso |
| '53'   | Amonestación  | NO                  | NO (acción disciplinaria, no ausencia) |
| '56','57' | Accidente de trabajo (diagnóstico médico) | NO | Evaluar caso a caso |

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
