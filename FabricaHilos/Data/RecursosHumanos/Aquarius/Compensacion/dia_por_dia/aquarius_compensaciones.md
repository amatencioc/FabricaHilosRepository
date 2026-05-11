# Aquarius — Compensaciones (SCA_COMPENSACION) — Análisis completo (27/04/2026)

## CONCEPTO
Compensación = trasladar **tiempo (en minutos)** desde un día/concepto **ORIGEN** (donde sobra: HE, dobles, banco) hacia un día/concepto **DESTINO** (donde falta: tardanza, falta, salida antes, permiso, horas no trabajadas) — o ingresarlo al **banco de horas** mensual/semanal.

Fuente: bloque `PASO 15` de `SP_SCA_PROCESO_TRABAJADOR` (aquarius.txt L5874–6000) y SPs de mantenimiento.

---

## TABLA: SCA_COMPENSACION
| Campo | Tipo | Descripción |
|---|---|---|
| ID_COMPEN | NUMBER | PK, secuencia `id_comp_seq` |
| COD_EMPRESA | VARCHAR2 | empresa |
| COD_PERSONAL | VARCHAR2 | empleado |
| FECHADESTINO | DATE | día receptor del tiempo. NULL cuando `tipocompensacion='I'` |
| FECHAORIGEN | DATE | día donante del tiempo |
| TIPOORIGEN | CHAR(1) | de dónde sale el tiempo (ver tabla) |
| TIPOCOMPENSACION | CHAR(1) | a qué se aplica (ver tabla) |
| TIEMPO | NUMBER | duración en **MINUTOS** (HH*60 + MM) |
| AUX1 | VARCHAR2 | identifica el periodo cuando es banco: `'MM/AAAA'` (mes) o `'SS/MM/AAAA'` (semana) |

Secuencia: `id_comp_seq` (no documentada antes).

---

## CÓDIGOS

### TIPOORIGEN (fuente del tiempo) — se valida contra `SCA_ASISTENCIA_TAREO` en `fechaorigen`
| Cód | Significado | Campo origen en tareo |
|---|---|---|
| `E` | Horas Extras | `HORAEXTRA_AJUS` |
| `D` | Horas Dobles (oficiales) | `HORADOBLESOF` |
| `B` | Banco de Horas del día | `HORABANCOH` |
| `I` | Intercambio (banco mensual/semanal) | NO se valida contra tareo; solo se aplica |

### TIPOCOMPENSACION (destino del tiempo) — se valida contra `SCA_ASISTENCIA_TAREO` en `fechadestino`
| Cód | Significado | Campo destino en tareo | Validación |
|---|---|---|---|
| `A` | Horas Antes de Salida | `HORAANTESALIDA` | exacta (=) |
| `T` | Tardanza | `HORATARDANZA` | exacta (=) |
| `N` | Horas No Trabajadas | `HORAS_NO_TRABAJADAS` | exacta (=) |
| `F` | Faltas | `HORAS_FALTA` | parcial (≥) |
| `P` | Permisos | `HORAPERMISO` | parcial (≥); también afecta `SCA_PERMISO_DET.tiempo_compensado` |
| `I` | Intercambio → Banco | (no aplica) | sin destino diario |

### Otros parámetros del SP de inserción
- `v_tipo`: `'N'` = banco MENSUAL (`SCA_BANCOHORAS_MES`), cualquier otro = SEMANAL (`SCA_BANCOHORAS_SEM`)
- `v_proceso`: identifica el período (`'MM/AAAA'` o `'SS/MM/AAAA'`)
- `v_horas`: formato `'HH:MI'` (string); se convierte a minutos
- `v_perid`: ID del permiso (cuando `tipocompensacion='P'`)

---

## SP_SCA_INSERT_COMPENSACION (alta manual)
1. INSERT a `SCA_COMPENSACION` (tiempo en minutos, aux1 = periodo)
2. Si `tipocompensacion='P'`:
   - `UPDATE SCA_PERMISO_DET SET tiempo_compensado = NVL(tiempo_compensado, base) + horas WHERE perid=v_perid AND perfec=fechadestino`
3. Si `tipocompensacion='I'`:
   - Si `v_tipo='N'`: `UPDATE SCA_BANCOHORAS_MES SET hc_banhormes += minutos WHERE cod_personal AND mes/ano = v_proceso`
   - Si no: `UPDATE SCA_BANCOHORAS_SEM SET hc_banhorsem += minutos WHERE cod_personal AND sem/mes/ano = v_proceso`

## SP_SCA_DELETE_COMPENSACION (baja manual)
- Espejo del insert: hace los mismos UPDATE pero RESTANDO; usa `tiempo_compensado=NULL` para permisos.
- Para `tipocompensacion='I'`, ignora `fechadestino` (que es NULL) usando NVL+CASE.

---

## PASO 15 EN SP_SCA_PROCESO_TRABAJADOR (aplicación diaria)
Se ejecuta DESPUÉS de autorizaciones (PASO 14). Dos cursores:

### CUR_COMPENSACIONES1 — `fechadestino = v_fec_proceso AND tipocompensacion <> 'I'`
Procesa el día como **RECEPTOR**:

```
1. Lee v_aux_tiempo_des desde tareo según tipocompensacion (A/T/N/F/P)
2. Validación:
   - (A,T,N): v_aux_tiempo_des = v_com_tiempo  (exacta)
   - (F,P):   v_aux_tiempo_des >= v_com_tiempo (parcial)
3. Si OK:
     sp_SCA_Update_Tareo_InsComDes(empresa, personal, fechadestino, tipcom, horas)
4. Si NO OK:
     sp_SCA_Update_Tareo_DelComDes(empresa, personal, fechaorigen, tipori, horas)
     DELETE FROM SCA_COMPENSACION WHERE id_compen = v_idcom
```

### CUR_COMPENSACIONES2 — `fechaorigen = v_fec_proceso`
Procesa el día como **DONANTE** (incluye `tipocompensacion='I'`):

```
1. Lee v_aux_tiempo_ori desde tareo según tipoorigen (E/D/B). Para I no valida.
2. Validación: v_aux_tiempo_ori >= v_com_tiempo
3. Si OK:
     sp_SCA_Update_Tareo_InsComOri(empresa, personal, fechaorigen, tipori, horas)
4. Si NO OK:
     sp_SCA_Update_Tareo_DelComDes(empresa, personal, fechadestino, tipcom, horas)
     DELETE FROM SCA_COMPENSACION WHERE id_compen = v_idcom
```

> **Importante**: la compensación se evalúa en **ambos días** (origen y destino). Si cualquiera falla la validación, se REVIERTE (DELETE). La auto-eliminación es esperada cuando el tareo cambia y ya no cuadra.

---

## EFECTOS EN TAREO

### sp_SCA_Update_Tareo_InsComDes (aplica en FECHADESTINO)
| tipocom | Acción en SCA_ASISTENCIA_TAREO |
|---|---|
| `T` | `horatardanza = NULL`, `alerta04 = 'TC'` (Tardanza Compensada) |
| `A` | `horaantesalida = NULL`, `alerta07 = 'SC'` (Salida Compensada) |
| `N` | `horas_no_trabajadas = NULL`, `alerta03 = 'HC'` |
| `F` | `horas_falta = NULL`, `alerta02 = 'FC'` |
| `P` | `horas_recup -= horas`; si llega a cero → `alerta09 = 'PC'` |
| **siempre** | `horaefectiva += horas` |
| **si tipcom ≠ 'P'** | `tothoramarcas += horas` |

### sp_SCA_Update_Tareo_InsComOri (aplica en FECHAORIGEN)
Siempre: `tothoramarcas -= horas`. Adicional según tipori:
| tipori | Acción |
|---|---|
| `E` | `horaextra_ajus -= horas`; recalcula `horaexofi1/2/3` con `h25f/h35i/h35f`; `alerta06 = 'EC'` si llega a cero |
| `D` | `horadoblesof -= horas`; `alerta08 = 'DC'` si llega a cero |
| `B` | `horabancoh -= horas`; `alerta06 = 'EC'` si llega a cero |

### sp_SCA_Update_Tareo_DelComDes / DelComOri (reversión)
Restauran los valores: re-suman al campo origen y descuentan del receptor. Recalculan alertas (`'TN'/'TE'`, `'SN'/'SE'`, `'EN'/'EE'`, `'HI'`, `'FT'`).

---

## ALERTAS NUEVAS (introducidas por compensaciones)
| Alerta | Valor | Significado |
|---|---|---|
| ALERTA02 | `FC` | Falta Compensada |
| ALERTA03 | `HC` | Horas no trabajadas Compensadas |
| ALERTA04 | `TC` | Tardanza Compensada |
| ALERTA06 | `EC` | Extras Compensadas (HE consumidas) |
| ALERTA07 | `SC` | Salida antes Compensada |
| ALERTA08 | `DC` | Dobles Compensadas |
| ALERTA09 | `PC` | Permiso Compensado |

---

## TABLAS RELACIONADAS

### SCA_BANCOHORAS_MES
- PK lógica: `cod_empresa`, `cod_personal`, `mes_proceso`, `ano_proceso`
- `hc_banhormes` — saldo en MINUTOS (acumulado por compensaciones tipo `'I'`)

### SCA_BANCOHORAS_SEM
- PK lógica: `cod_empresa`, `cod_personal`, `sem_proceso`, `mes_proceso`, `ano_proceso`
- `hc_banhorsem` — saldo en MINUTOS

### SCA_PERMISO_DET
- Campo `tiempo_compensado` (DATE base 01/01/1900) — actualizado solo por compensaciones tipo `'P'`

### SCA_TRASLADO
- Estructura parecida (`cod_empresa, cod_personal, fechadestino, fechaorigen, tiempo`) pero **distinto propósito**: traslados de tiempo simple, sin clasificación origen/destino, sin alertas.

---

## REGLAS CRÍTICAS
1. **TIEMPO en MINUTOS** en `SCA_COMPENSACION`. En `SCA_ASISTENCIA_TAREO` es DATE base 01/01/1900. Convertir con `HH*60+MI` o `to_date('01/01/1900 '||v_horas,'dd/MM/yyyy HH24:MI')`.
2. **Idempotencia**: `PASO 15` re-ejecuta cada día → puede DELETE compensaciones que ya no cuadran. Si necesitas conservar, valida ANTES de re-procesar.
3. **Orden estricto**: PASO 14 (autorizaciones) DEBE ir antes que PASO 15. Cambia `horaextra_ajus`, base de validación de tipo `E`.
4. **Tipo `I` (banco)** no requiere fechadestino: usa `NVL(fechadestino, 01/01/1900)` en queries.
5. **NO toca `PKG_SCA_DEPURA_TAREO`** — la depuración no interactúa con compensaciones (verificado 27/04/2026).
6. **`horas_recup` para `P`**: solo se considera "completamente compensado" cuando `horas_recup = tothoras` exactamente (alerta09='PC' o 'FT').
7. **Validación asimétrica**: A/T/N exigen igualdad exacta; F/P aceptan parcial. Si la HE/dobles/banco origen no alcanza → DELETE auto.
8. **`aux1` rastrea el periodo** del banco al momento del INSERT — útil para auditar a qué mes/semana entró el tiempo.

---

## CONSULTAS ÚTILES
```sql
-- Compensaciones activas de un empleado
SELECT id_compen, fechaorigen, tipoorigen, fechadestino, tipocompensacion,
       TRUNC(tiempo/60)||':'||LPAD(MOD(tiempo,60),2,'0') hh_mm, aux1
FROM   SCA_COMPENSACION
WHERE  cod_empresa = :emp AND cod_personal = :per
ORDER  BY fechaorigen, fechadestino;

-- Saldo banco de horas mensual
SELECT mes_proceso, ano_proceso,
       TRUNC(hc_banhormes/60)||':'||LPAD(MOD(hc_banhormes,60),2,'0') saldo
FROM   SCA_BANCOHORAS_MES
WHERE  cod_personal = :per
ORDER  BY ano_proceso, mes_proceso;

-- Tareos con compensaciones aplicadas (alertas C*)
SELECT fechamar, alerta02, alerta03, alerta04, alerta06, alerta07, alerta08, alerta09
FROM   SCA_ASISTENCIA_TAREO
WHERE  cod_empresa = :emp AND cod_personal = :per
AND    (alerta02='FC' OR alerta03='HC' OR alerta04='TC'
        OR alerta06='EC' OR alerta07='SC' OR alerta08='DC' OR alerta09='PC');
```
