# Aquarius — Flujo completo HE → Planilla (07/05/2026)

## VISIÓN GENERAL
El flujo es **totalmente automático** una vez registradas las autorizaciones.
El trabajo manual de RRHH = solo ingresar la autorización. Todo lo demás lo hace Oracle.

---

## FLUJO PASO A PASO

### 1. RRHH ingresa autorización
- Vía portal .NET → `sp_grabar_autorizacion` (PKG_AUTH_HE_SUPERVISOR)
- Inserta en `SCA_AUTORIZACION`: `tip_authe`, `fec_authe`, `can_authe` (base 01/01/1900), `cod_usuario`
- Tipos: '1'=HEA, '2'=HED, '5'=HEO/Dobles
- Los días **sin** autorización quedan con `hayhea/hed/heo_poraut='S'` → NO van a planilla
- Los días **con** desautorización explícita (tipos '3','4','6') tampoco van

### 2. PROCESO NOCTURNO — SP_SCA_PROCESO_TRABAJADOR (PASO 14)
Se ejecuta automáticamente cada noche por empleado y fecha.

**Cursor CUR_AUTORIZACIONES**: Lee `SCA_AUTORIZACION` para `fec_authe = v_fec_proceso`

Para cada autorización encontrada:
| Tipo | Valida contra tareo | Si OK → escribe en tareo | Si falla → |
|---|---|---|---|
| '1' HEA | `can_authe <= horaextantes` | `horaextantesofi = can_authe` | DELETE SCA_AUTORIZACION |
| '2' HED | `can_authe <= horaextra` | `horaextraofi = can_authe` | DELETE SCA_AUTORIZACION |
| '5' HEO | `can_authe <= horadobles` | `horadoblesof = can_authe` | DELETE SCA_AUTORIZACION |
| '3' D-HEA | `can_authe = horaextantes` | `horaextantesofi = NULL` | DELETE SCA_AUTORIZACION |
| '4' D-HED | `can_authe = horaextra` | `horaextraofi = NULL` | DELETE SCA_AUTORIZACION |
| '6' D-HEO | `can_authe = horadobles` | `horadoblesof = NULL` | DELETE SCA_AUTORIZACION |

**Post-autorización tipo '2' HED** (cuando `tippagohe='1'` = pago en dinero):
- Recalcula `horaextra_ajus` = HED ajustada
- Reparte en `horaexofi1` (25%), `horaexofi2` (35%), `horaexofi3` (nocturna) según `SCA_RANGOS_HEXTRAS`
- Si `tippagohe='2'` (banco): recalcula `horabancoh`

**Después del PASO 14 → PASO 15 (Compensaciones)**
Las compensaciones de tipo 'E' (HE origen) usan `horaextra_ajus` ya calculado por PASO 14.

### 3. INTERFACE_ASSITIME — Al cierre de período
Lee `SCA_ASISTENCIA_TAREO` ya procesado y actualiza planilla:

| Campo tareo | Concepto planilla | Tabla destino |
|---|---|---|
| `HORAEXOFI1` (HED 25%) | `'1010'` | `SIG.INGRE_PLA` (0003) / `SOLSA.INGRE_PLA` (0002) / `ARBONA.INGRE_PLA` (0001) |
| `HORAEXOFI2` (HED 35%) | `'1039'` | ídem |
| `HORADOBLESOF` (Dobles) | `'1072'` (0003) / `'1012'` (0002) / `'1011'` (0001) | ídem |
| `HORAEXTANTESOFI` (HEA) | no visto en interface, posible concepto separado | — |

Unidad: **horas decimales** (`TRUNC(min/60, 2)`). Escribe con `UPDATE INGRE_PLA SET VALOR_ORI = ...`

### 4. Días NO autorizados → Compensación (PASO 15)
Los días sin `tip_authe IN ('1','2','5')`:
- `horaexofi1/2/3 = NULL`, `horadoblesof = NULL` → NO van a planilla de pago
- Si el horario tiene `tippagohe='2'` (banco): van a `horabancoh` → banco de horas
- Si hay registro en `SCA_COMPENSACION` con `tipoorigen='E'/'D'/'B'`: compensan tardanzas/faltas/permisos (PASO 15)

---

## RESUMEN DEL "¿CÓMO SE HACE AUTOMÁTICO?"

```
RRHH ingresa auth → sp_grabar_autorizacion → SCA_AUTORIZACION
                                                    ↓
                         SP_SCA_PROCESO_TRABAJADOR PASO 14 (noche)
                                                    ↓
                    SCA_ASISTENCIA_TAREO.horaexofi1/2/3 + horadoblesof
                                                    ↓
                               INTERFACE_ASSITIME (fin de período)
                                                    ↓
                                 SIG/SOLSA/ARBONA.INGRE_PLA (planilla)
```

**Solo las horas CON autorización en `SCA_AUTORIZACION` llegan a planilla.**
Las horas sin autorización quedan en tareo pero con campos `_ofi = NULL`.

---

## CAMPOS CLAVE en SCA_ASISTENCIA_TAREO

| Campo | Descripción |
|---|---|
| `horaextra` | HED bruta del día (lo que marcó el reloj) |
| `horaextraofi` | HED autorizada (≤ horaextra). NULL si no autorizada |
| `horaextra_ajus` | HED ajustada post-PASO14 para compensaciones |
| `horaexofi1` | HED al 25% (pago) — fuente para concepto '1010' |
| `horaexofi2` | HED al 35% (pago) — fuente para concepto '1039' |
| `horaextantes` | HEA bruta |
| `horaextantesofi` | HEA autorizada |
| `horadobles` | HEO/Dobles bruta |
| `horadoblesof` | HEO/Dobles autorizada — fuente para concepto '1072' |
| `hayhea_poraut` | 'S' = pendiente de auth, 'N' = ya autorizada |
| `hayhed_poraut` | ídem HED |
| `hayheo_poraut` | ídem HEO |

---

## REGLAS CRÍTICAS
1. **Auto-eliminación**: Si tareo cambia (re-procesa) y `can_authe > horas_reales` → PASO 14 borra la auth. RRHH debe re-ingresar.
2. **Idempotencia**: PASO 14 y 15 re-ejecutan todos los días del período → seguro re-procesar.
3. **`tippagohe`** en `SCA_REGLAS_DET` (via horario): '1'=pago dinero, '2'=banco. Determina si las HED van a `horaexofi1/2/3` o a `horabancoh`.
4. **`min_a_part_hextra`** en `SCA_PARAMETROS`: umbral mínimo en minutos para que se considere HE pagable.
5. **INTERFACE_ASSITIME** filtra por `P.COD_TIPO_PLANILLA = '05'` — solo ciertos tipos de planilla usan este interface.
6. **Orden estricto**: PASO 14 (auth) → PASO 15 (compensaciones) → INTERFACE_ASSITIME (planilla).
