# Aquarius — Autorizaciones de HE/Dobles (SCA_AUTORIZACION) — Análisis completo (06/05/2026)

## TABLA: SCA_AUTORIZACION
| Campo | Tipo | Descripción |
|---|---|---|
| ID_AUTHE | NUMBER(18) | PK, secuencia `id_authe_seq` |
| TIP_AUTHE | CHAR(1) | tipo de autorización (ver tabla) |
| FEC_AUTHE | DATE | fecha del día al que aplica la autorización |
| COD_EMPRESA | NVARCHAR2(8) | empresa |
| COD_PERSONAL | VARCHAR2(6) | empleado |
| CAN_AUTHE | DATE | cantidad de tiempo (base 01/01/1900, formato HH24:MI) |
| OBS_AUTHE | VARCHAR2(100) | observación libre |
| COD_USUARIO | VARCHAR2(30) | usuario que registró la autorización |
| ORI_AUTHE | NUMBER | origen (solo valor observado: 2 = manual desde .NET) |

---

## CÓDIGOS TIP_AUTHE (6 tipos)

| Cód | Significado | Efecto en PASO 14 |
|---|---|---|
| `'1'` | **Autorizar HEA** (Horas Extras Antes de entrada) | `horaextantesofi` = valor autorizado |
| `'2'` | **Autorizar HED** (Horas Extras Después de salida) | `horaextraofi` = valor autorizado |
| `'3'` | **Desautorizar HEA** | `horaextantesofi` = NULL |
| `'4'` | **Desautorizar HED** | `horaextraofi` = NULL |
| `'5'` | **Autorizar HEO** (Horas Dobles/Descanso) | `horadoblesof` = valor autorizado |
| `'6'` | **Desautorizar HEO** | `horadoblesof` = NULL |

**Estadísticas por tipo (datos reales BD)**:
- '1' HEA autorizada: 23 registros
- '2' HED autorizada: 16,356 registros (la más frecuente)
- '3' HEA desaut.: 1,662
- '4' HED desaut.: 4,548
- '5' HEO autorizada: 609
- '6' HEO desaut.: 266

---

## LÓGICA DE sp_SCA_Insert_Autorizacion (registro manual)

Cada par autorización/desautorización es **mutuamente excluyente por día**:

| Tipo ingresado | Si existe tipo opuesto | Acción |
|---|---|---|
| '1' (auth HEA) | Elimina '3' (desauth) si existe, también elimina '1' previo | INSERT nuevo '1' |
| '2' (auth HED) | Elimina '4' (desauth) si existe, también elimina '2' previo | INSERT nuevo '2' |
| '3' (desauth HEA) | Si existe '1' (auth) → lo ELIMINA (= cancelar auth) | Solo si NO hay '1' → INSERT '3' |
| '4' (desauth HED) | Si existe '2' (auth) → lo ELIMINA | Solo si NO hay '2' → INSERT '4' |
| '5' (auth HEO) | Elimina '6' si existe, también elimina '5' previo | INSERT nuevo '5' |
| '6' (desauth HEO) | Si existe '5' → lo ELIMINA | Solo si NO hay '5' → INSERT '6' |

> **Invariante**: nunca coexisten '1' y '3', ni '2' y '4', ni '5' y '6' para el mismo empleado/fecha.

---

## PASO 14 — Aplicación en SP_SCA_PROCESO_TRABAJADOR

### CUR_AUTORIZACIONES (cursor)
```sql
SELECT id_authe, PLA_PERSONAL.cod_empresa, PLA_PERSONAL.cod_personal, can_authe, tip_authe
FROM SCA_AUTORIZACION JOIN PLA_PERSONAL ON cod_empresa + cod_personal
WHERE fec_authe = TO_DATE(v_fec_proceso,'DD/MM/YYYY')
AND [filtro empresa/sucursal/CC según v_modo]
```

### Validaciones y acciones

| Tipo | Campo validado en tareo | Condición válida | Acción si pasa | Acción si no pasa |
|---|---|---|---|---|
| '1' HEA | `horaextantes` | `can_authe <= horaextantes` | sp_SCA_Upd_Tar_InsAut '1' | DELETE SCA_AUTORIZACION |
| '2' HED | `horaextra` | `can_authe <= horaextra` | sp_SCA_Upd_Tar_InsAut '2' | DELETE SCA_AUTORIZACION |
| '3' D-HEA | `horaextantes` | `can_authe = horaextantes` | sp_SCA_Upd_Tar_InsAut '3' | DELETE SCA_AUTORIZACION |
| '4' D-HED | `horaextra` | `can_authe = horaextra` | sp_SCA_Upd_Tar_InsAut '4' | DELETE SCA_AUTORIZACION |
| '5' HEO | `horadobles` | `can_authe <= horadobles` | sp_SCA_Upd_Tar_InsAut '5' | DELETE SCA_AUTORIZACION |
| '6' D-HEO | `horadobles` | `can_authe = horadobles` | sp_SCA_Upd_Tar_InsAut '6' | DELETE SCA_AUTORIZACION |

> **CRÍTICO**: Si el tareo ya no cuadra (marcaciones cambiaron), la autorización se AUTO-ELIMINA.
> Esto es idéntico al comportamiento de compensaciones.

---

## sp_SCA_Upd_Tar_InsAut — Efectos en SCA_ASISTENCIA_TAREO

### Tipos '1'/'2' (autorizar HEA/HED)
- `horaextantesofi` ó `horaextraofi` ← valor autorizado (solo si >= `min_a_part_hextra`)
- `hayhea_poraut`/`hayhed_poraut` ← 'N' si el valor autorizado = `horaextantes`/`horaextra` exacto
- **Post si `tippagohe='1'`** (pago en dinero): recalcula `horaextra_ajus`, `horaexofi1/2/3`, `alerta06`
- **Post si `tippagohe='2'`** (banco): recalcula `horabancoh`

### Tipos '3'/'4' (desautorizar HEA/HED)
- `horaextantesofi`/`horaextraofi` ← NULL
- `hayhea_poraut`/`hayhed_poraut` ← 'S' (vuelve a pendiente)
- **Post si `tippagohe='1'`**: `horaextra_ajus`=NULL, `horaexofi1/2/3`=NULL, `alerta06`=NULL
- **Post si `tippagohe='2'`**: `horabancoh`=NULL

### Tipo '5' (autorizar HEO/Dobles)
- `horadoblesof` ← valor autorizado
- `hayheo_poraut` ← 'N' si valor = `horadobles` exacto

### Tipo '6' (desautorizar HEO)
- `horadoblesof` ← NULL
- `hayheo_poraut` ← 'S'

---

## INDICADORES DE PENDIENTE DE AUTORIZACIÓN en SCA_ASISTENCIA_TAREO

| Campo | Qué indica | Condición que lo activa (PASO previo al 14) |
|---|---|---|
| `HAYHEA_PORAUT='S'` | HEA por autorizar | `horaextantes > horaextantesofi` |
| `HAYHED_PORAUT='S'` | HED por autorizar | `haypagohe='S'` AND HE >= `min_a_part_hextra` |
| `HAYHEO_PORAUT='S'` | Dobles por autorizar | `haypagohe='S'` AND `horadobles` >= `min_a_part_hextra` |

Luego el PASO 14 los pone en 'N' si aplica la autorización guardada en SCA_AUTORIZACION.
Aparecen en el cronograma .NET como indicadores de que requieren atención.

---

## REGLAS CRÍTICAS DE AUTORIZACIONES
1. **`can_authe` en base 01/01/1900** — es DATE: `to_date('01/01/1900 '||v_valor,'dd/MM/yyyy HH24:MI')`
2. **Idempotencia**: PASO 14 re-ejecuta cada día. Si marcaciones cambiaron → DELETE auto de auth inválidas.
3. **Orden**: PASO 14 va ANTES que PASO 15 (compensaciones). Auth determina el `horaextra_ajus` base para compensaciones.
4. **Tope**: si `can_authe > horas_reales` → se elimina. Las auth nunca crean horas de la nada.
5. **Desauth activa (tip '3'/'4'/'6')**: solo se inserta cuando no hay auth previa. Si hay auth y se manda desauth → elimina la auth (limpieza).
6. **min_a_part_hextra**: umbral mínimo en minutos para contar HE. Viene de `SCA_REGLAS_DET`.

---

## CONSULTAS ÚTILES

```sql
-- Ver autorizaciones activas de un empleado
SELECT tip_authe, fec_authe, TO_CHAR(can_authe,'HH24:MI') horas, obs_authe, cod_usuario
FROM SCA_AUTORIZACION
WHERE cod_empresa=:emp AND cod_personal=:per
ORDER BY fec_authe, tip_authe;

-- Ver empleados con HE pendientes de autorizar
SELECT t.cod_empresa, t.cod_personal, t.fechamar,
       TO_CHAR(t.horaextantes,'HH24:MI') hea_raw, TO_CHAR(t.horaextra,'HH24:MI') hed_raw,
       TO_CHAR(t.horadobles,'HH24:MI') heo_raw,
       t.hayhea_poraut, t.hayhed_poraut, t.hayheo_poraut
FROM SCA_ASISTENCIA_TAREO t
WHERE (t.hayhea_poraut='S' OR t.hayhed_poraut='S' OR t.hayheo_poraut='S')
AND t.fechamar BETWEEN :fecini AND :fecfin
ORDER BY t.fechamar, t.cod_personal;

-- Historial autorizaciones por usuario autorizador
SELECT cod_usuario, tip_authe, COUNT(*) cnt, MIN(fec_authe) fec_min, MAX(fec_authe) fec_max
FROM SCA_AUTORIZACION
GROUP BY cod_usuario, tip_authe
ORDER BY cod_usuario, tip_authe;
```
