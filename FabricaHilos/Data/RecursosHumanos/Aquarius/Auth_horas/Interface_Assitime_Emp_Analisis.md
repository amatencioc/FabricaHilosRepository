# Interface_Assitime_Emp — Análisis meticuloso (07/05/2026)

Planilla `'02'` (empleados de oficina/staff). Este documento analiza el SP en detalle
y al final incluye la comparación completa contra `INTERFACE_ASSITIME` (planilla `'05'`).

---

## 1. FIRMA Y PARÁMETROS

```sql
CREATE OR REPLACE PROCEDURE Interface_Assitime_Emp(
    P_EMPRESA  IN VARCHAR2,   -- '0001', '0002', '0003'
    P_NUMPLA   IN NUMBER,     -- número de planilla en el sistema de pago
    P_ANO      IN VARCHAR2,   -- '2026'
    P_MES      IN VARCHAR2    -- '5' o '05'  (acepta con y sin cero)
)
```

**Las fechas reales del período NO vienen como parámetro.**
Se resuelven internamente desde `SCA_MES_PROC` (ver sección 3).

---

## 2. VARIABLES INTERNAS CLAVE

| Variable | Tipo | Propósito |
|---|---|---|
| `V_DATEINI` | VARCHAR2 | Fecha inicio período de proceso (dd/MM/yyyy) |
| `V_DATEFIN` | VARCHAR2 | Fecha fin período de proceso (dd/MM/yyyy) |
| `V_DATEINI_C` | VARCHAR2 | Primer día del mes calendario (dd/MM/yyyy) |
| `V_DATEFIN_C` | VARCHAR2 | Último día del mes calendario (dd/MM/yyyy) |

Hay **dos rangos de fechas distintos**:
- Período de proceso → `V_DATEINI / V_DATEFIN` → para tareo (HED, HEO, tardanza, turnos)
- Mes calendario → `V_DATEINI_C / V_DATEFIN_C` → para permisos LPAT y DMED

---

## 3. FLUJO DE EJECUCIÓN (BEGIN)

```
PASO 1: INSERT INTO ARGUMENTOS(P_EMPRESA, P_NUMPLA, P_ANO, P_MES)
        ↓ log de auditoría de cada ejecución

PASO 2: SELECT FECINI → V_DATEINI
        SELECT FECFIN  → V_DATEFIN
        FROM SCA_MES_PROC
        WHERE COD_EMPRESA = P_EMPRESA
          AND COD_TIPO_PLANILLA = '02'
          AND ANO_PROCESO = P_ANO
          AND MES_PROCESO = LPAD(P_MES,2,'0')
          AND ROWNUM < 2
        ↓ Ambas queries son independientes (2 SELECT separados)

PASO 3: V_DATEINI_C = '01/' || LPAD(P_MES,2,'0') || '/' || P_ANO
        V_DATEFIN_C = lógica mes:
          meses 31 días (01,03,05,07,08,10,12) → '31/MM/AAAA'
          febrero (02)                          → '28/MM/AAAA'  ← NO considera bisiestos
          meses 30 días (04,06,09,11)           → '30/MM/AAAA'

PASO 4: RESET a cero — UPDATE planilla destino
        (ver detalle por empresa en sección 6)

PASO 5: OPEN C1;
        FETCH C1 INTO vc_codigo, np_numpla, vc_concepto, vvalor_ori;
        WHILE C1%FOUND LOOP
          BEGIN
            UPDATE planilla destino SET VALOR_ORI = vvalor_ori
            WHERE C_CODIGO = vc_codigo AND NUM_PLA = np_numpla AND C_CONCEPTO = vc_concepto;
            FETCH C1 INTO ...;
          END;
        END LOOP;
        CLOSE C1;

PASO 6: COMMIT;
```

> **Diferencia de patrón de loop**: usa `OPEN/FETCH/WHILE` con `BEGIN...END` interno
> (permite capturar excepciones por fila sin abortar todo el proceso).

---

## 4. CURSOR C1 — CONCEPTOS (9 UNION)

El cursor se define **ANTES** del BEGIN. En el momento de definición, las variables
`V_DATEINI / V_DATEFIN / V_DATEINI_C / V_DATEFIN_C` no tienen valor todavía.
Se resuelven al hacer el `OPEN C1` dentro del BEGIN (comportamiento Oracle: el cursor
se abre en tiempo de ejecución, no de compilación).

Filtro global del cursor (outer WHERE):
```sql
WHERE valor_ori > 0  -- No graba filas con cero
```

---

### UNION 1 — Concepto `'1000'` : Días trabajados

```sql
30 - SUM(CASE
  WHEN alerta02 = 'FT'
  OR (alerta09 = 'PE' AND (
       PER_SUBSIDIO IS NOT NULL OR PER_SGOCE IS NOT NULL
       OR PER_VACA IS NOT NULL OR PER_LIC_SIND IS NOT NULL
       OR PER_DESC_MED IS NOT NULL OR PER_SUSPENSION IS NOT NULL
       OR PER_GOCE_FIS IS NOT NULL OR PER_LIC_PAT IS NOT NULL
       OR PER_LIC_FAC IS NOT NULL))
  OR PER_DIA_COMP = 'S'
  THEN 1 ELSE 0
END)
```

**Condiciones de ausencia** (cuenta como 1 día fuera):
| Condición | Significado |
|---|---|
| `alerta02 = 'FT'` | Falta total del día |
| `alerta09='PE'` + algún permiso NOT NULL | Permiso especial (subsidio, vacaciones, sindicato, desc. médico, suspensión, goce físico, lic. paternidad, lic. facultativa, s/goce) |
| `PER_DIA_COMP = 'S'` | Día de compensación utilizado |

**Filtro de fecha especial** (distinto a los otros UNION):
```sql
WHERE TO_CHAR(fechamar,'YYYYMM') = P_ANO || LPAD(P_MES,2,'0')
```
No usa V_DATEINI/V_DATEFIN — usa el mes calendario exacto.
Esto puede diferir si el período de proceso cae entre dos meses.

---

### UNION 2 — Concepto `'2018'` : Tardanza (en minutos)

```sql
SUM(CASE WHEN horatardanza IS NOT NULL THEN
    (TO_NUMBER(TO_CHAR(horatardanza,'HH24')) * 60) + TO_NUMBER(TO_CHAR(horatardanza,'MI'))
    ELSE 0 END)
```
Filtro externo: `WHERE valor_ori > 10` → tardanzas ≤ 10 minutos totales **no van a planilla**.
Rango: `V_DATEINI / V_DATEFIN` (período de proceso).

---

### UNION 3 — Concepto `'1011'` / `'1012'` : Horas Dobles/HEO

```sql
CASE WHEN P_EMPRESA = '0001' THEN '1011' ELSE '1012' END c_concepto,
TRUNC(SUM(horadoblesof_en_minutos) / 60, 2) valor_ori
```
- Empresa `0001` → concepto `'1011'`
- Empresas `0002` y `0003` → concepto `'1012'`

Requiere `horadoblesof` NOT NULL (campo llenado por PASO 14 si hay autorización).

---

### UNION 4 — Concepto `'1010'` : HED 25% (horaexofi1)

```sql
TRUNC(SUM(CASE WHEN horaexofi1 IS NOT NULL THEN
    (TO_NUMBER(TO_CHAR(horaexofi1,'HH24')) * 60) + TO_NUMBER(TO_CHAR(horaexofi1,'MI'))
    ELSE 0 END) / 60, 2)
```
Fuente: `horaexofi1` — populado por PASO 14 con las HE autorizadas al 25%.

---

### UNION 5 — Concepto `'1008'` : HED 35% (horaexofi2)

```sql
-- CASE comentado que decía '1039' para 0001 / '1008' para otras
'1008'  -- hardcodeado para TODAS las empresas
TRUNC(SUM(CASE WHEN horaexofi2 IS NOT NULL THEN
    (TO_NUMBER(TO_CHAR(horaexofi2,'HH24')) * 60) + TO_NUMBER(TO_CHAR(horaexofi2,'MI'))
    ELSE 0 END) / 60, 2)
```
> **OJO**: El código tiene comentado un CASE que usaba `'1039'` para empresa `'0001'`.
> Actualmente hardcodeado `'1008'` para todas. Si necesitas diferenciar por empresa,
> el CASE está comentado arriba de la cadena literal.

---

### UNION 6 — Concepto `'1023'` : Días en Turno 2

```sql
SUM(CASE
  WHEN COALESCE(alerta02,'T2') <> 'FT'
    AND PER_VACA IS NULL
    AND (P_EMPRESA = '0001' OR P_EMPRESA = '0003')
  THEN 1 ELSE 0
END)
WHERE hortur = 'T2'
```
Condiciones de inclusión:
- El día no es falta total (`alerta02 <> 'FT'`)
- El empleado no está de vacaciones (`PER_VACA IS NULL`)
- Solo genera valor para empresa `'0001'` o `'0003'` — empresa `'0002'` siempre devuelve 0

El `COALESCE(alerta02,'T2')` evita que NULL en alerta02 se interprete como 'FT'.

---

### UNION 7 — Concepto `'1024'` : Días en Turno 3

```sql
SUM(CASE
  WHEN COALESCE(alerta02,'T3') <> 'FT'
    AND PER_VACA IS NULL
    AND (P_EMPRESA = '0001' OR P_EMPRESA = '0003')
  THEN 1 ELSE 0
END)
WHERE hortur IN ('T3', '0311')
```
Idéntico al turno 2, pero filtra `hortur IN ('T3','0311')`.
El código `'0311'` es un horario específico tratado como turno 3.

---

### UNION 8 — Concepto `'1089'` / `'1068'` : Licencia Paternidad (LPAT)

```sql
CASE WHEN P_EMPRESA = '0003' THEN '1089' ELSE '1068' END c_concepto,
SUM(
  (CASE WHEN S.perfecfin  >= TO_DATE(V_DATEFIN_C,'dd/MM/yyyy')
        THEN TO_DATE(V_DATEFIN_C,'dd/MM/yyyy')
        ELSE S.perfecfin  END
   -
   CASE WHEN S.perfecini <= TO_DATE(V_DATEINI_C,'dd/MM/yyyy')
        THEN TO_DATE(V_DATEINI_C,'dd/MM/yyyy')
        ELSE S.perfecini  END)
  + 1
)
FROM SCA_PERMISO_CAB S
WHERE S.grupoid = 'LPAT'
  AND (TO_CHAR(S.perfecini,'YYYYMM') = P_ANO||LPAD(P_MES,2,'0')
    OR TO_CHAR(S.perfecfin,'YYYYMM') = P_ANO||LPAD(P_MES,2,'0'))
```
- Fuente: `SCA_PERMISO_CAB` (no `SCA_ASISTENCIA_TAREO`)
- Cálculo: intersección del permiso con el mes calendario (MIN(fin, fin_mes) - MAX(ini, ini_mes) + 1)
- Empresa `0003` → `'1089'`; empresas `0001`/`0002` → `'1068'`
- Filtro: el permiso toca el mes (inicio o fin dentro del mes)

---

### UNION 9 — Concepto `'1052'` / `'1019'` / `'1018'` : Descanso Médico (DMED)

```sql
CASE WHEN P_EMPRESA = '0001' THEN '1052'
     WHEN P_EMPRESA = '0002' THEN '1019'
     ELSE '1018'
END c_concepto,
-- mismo cálculo de intersección que LPAT
FROM SCA_PERMISO_CAB S
WHERE S.grupoid = 'DMED'
  AND (TO_CHAR(S.perfecini,'YYYYMM') = P_ANO||P_MES           -- ← SIN LPAD
    OR TO_CHAR(S.perfecfin,'YYYYMM') = P_ANO||P_MES)          -- ← SIN LPAD
```

> **BUG POTENCIAL**: usa `P_ANO||P_MES` sin `LPAD(P_MES,2,'0')`.
> Si se llama con `P_MES='5'` busca `'20265'` en lugar de `'202605'`.
> LPAT (UNION 8) sí usa LPAD correctamente.
> Para evitar el bug, siempre llamar con `P_MES='05'` (dos dígitos).

---

## 5. RESET PREVIO POR EMPRESA

Antes de actualizar, se ponen a cero los conceptos del período en la tabla de planilla:

| Empresa | Tabla | Conceptos reseteados |
|---|---|---|
| `'0003'` | `SIG_INGRE_PLA` | `'1000','1012','1010','1008','1023','1024','1089','1018','2018'` |
| `'0002'` | `SOL_INGRE_PLA` | `'1000','1012','1010','1008','1068','1019','2018'` |
| `'0001'` | `ARB_INGRE_PLA` | `'1000','1011','1010','1008','1023','1024','1068','1052','2018'` |

Coherencias:
- `'1011'` solo en 0001 (dobles 0001); `'1012'` en 0002 y 0003
- Turno `'1023'/'1024'` solo en 0001 y 0003 (empresa 0002 no tiene turnos)
- LPAT: `'1089'` (0003), `'1068'` (0001/0002); DMED: `'1018'` (0003), `'1019'` (0002), `'1052'` (0001)

---

## 6. TABLA DESTINO POR EMPRESA

```
P_EMPRESA = '0003' → SIG_INGRE_PLA  (sinónimo local → SIG.INGRE_PLA)
P_EMPRESA = '0002' → SOL_INGRE_PLA  (sinónimo local → SOLSA.INGRE_PLA)
P_EMPRESA = '0001' → ARB_INGRE_PLA  (sinónimo local → ARBONA.INGRE_PLA)
```

UPDATE:
```sql
SET VALOR_ORI = vvalor_ori
WHERE C_CODIGO = TO_CHAR(vc_codigo)
  AND NUM_PLA = np_numpla
  AND C_CONCEPTO = vc_concepto;
```
Solo actualiza filas existentes — si el empleado no tiene fila para ese concepto, el UPDATE
no hace nada (0 rows affected, sin INSERT, sin error).

---

## 7. REGLAS DE NEGOCIO ESPECÍFICAS v2

1. **Días normales = 30 - ausentes**: Siempre parte de 30 fijos. Si alguien tuvo 2 faltas,
   da 28. No considera meses más cortos (febrero = 28 días calendario pero base sigue siendo 30).

2. **HED solo entra si PASO 14 autorizó**: `horaexofi1/2` = NULL sin autorización
   → `VALOR_ORI = 0` → filtrado por `WHERE valor_ori > 0` → no llega a planilla.

3. **Período vs. mes calendario**: el concepto `'1000'` usa mes exacto (`YYYYMM`),
   los demás usan el rango `V_DATEINI/V_DATEFIN`. En períodos que cruzan meses
   habrá inconsistencia entre `'1000'` y los demás conceptos.

4. **Empresa 0002 no tiene turnos**: turno 2/3 generan 0 para 0002. El cursor los
   incluye pero son filtrados por `valor_ori > 0`.

5. **Re-ejecución segura**: gracias al RESET previo, ejecutar dos veces el mismo
   período genera el mismo resultado (idempotente, a diferencia de v1).

6. **Febrero no considera bisiesto**: `V_DATEFIN_C` siempre será `'28/02/AAAA'`
   para febrero, incluso en años bisiestos (2024, 2028...). LPAT/DMED perderán el día 29.

---

## 8. CONSULTAS DE DIAGNÓSTICO

```sql
-- ¿Qué conceptos enviará Interface_Assitime_Emp para un empleado?
SELECT p.cod_spring, p.ape_paterno||' '||p.nom_trabajador nombre,
       TO_CHAR(t.fechamar,'MM/YYYY') periodo,
       SUM(CASE WHEN t.horaexofi1 IS NOT NULL THEN
             (TO_NUMBER(TO_CHAR(t.horaexofi1,'HH24'))*60+TO_NUMBER(TO_CHAR(t.horaexofi1,'MI')))
           ELSE 0 END) min_1010,
       SUM(CASE WHEN t.horaexofi2 IS NOT NULL THEN
             (TO_NUMBER(TO_CHAR(t.horaexofi2,'HH24'))*60+TO_NUMBER(TO_CHAR(t.horaexofi2,'MI')))
           ELSE 0 END) min_1008,
       SUM(CASE WHEN t.horadoblesof IS NOT NULL THEN
             (TO_NUMBER(TO_CHAR(t.horadoblesof,'HH24'))*60+TO_NUMBER(TO_CHAR(t.horadoblesof,'MI')))
           ELSE 0 END) min_dobles,
       SUM(CASE WHEN t.alerta02='FT' OR t.per_dia_comp='S'
               OR (t.alerta09='PE' AND (t.per_subsidio IS NOT NULL OR t.per_sgoce IS NOT NULL
                 OR t.per_vaca IS NOT NULL OR t.per_lic_sind IS NOT NULL
                 OR t.per_desc_med IS NOT NULL OR t.per_suspension IS NOT NULL
                 OR t.per_goce_fis IS NOT NULL OR t.per_lic_pat IS NOT NULL
                 OR t.per_lic_fac IS NOT NULL))
               THEN 1 ELSE 0 END) dias_ausentes,
       30 - SUM(CASE WHEN t.alerta02='FT' OR t.per_dia_comp='S'
               OR (t.alerta09='PE' AND (t.per_subsidio IS NOT NULL OR t.per_sgoce IS NOT NULL
                 OR t.per_vaca IS NOT NULL OR t.per_lic_sind IS NOT NULL
                 OR t.per_desc_med IS NOT NULL OR t.per_suspension IS NOT NULL
                 OR t.per_goce_fis IS NOT NULL OR t.per_lic_pat IS NOT NULL
                 OR t.per_lic_fac IS NOT NULL))
               THEN 1 ELSE 0 END) conc_1000
FROM SCA_ASISTENCIA_TAREO t
JOIN PLA_PERSONAL p ON p.cod_empresa=t.cod_empresa AND p.cod_personal=t.cod_personal
WHERE t.cod_empresa='0003'
  AND TO_CHAR(t.fechamar,'YYYYMM') = '202605'
  AND p.cod_tipo_planilla='02'
GROUP BY p.cod_spring, p.ape_paterno, p.nom_trabajador, TO_CHAR(t.fechamar,'MM/YYYY')
ORDER BY p.ape_paterno;

-- Verificar historial de ejecuciones del interface
SELECT p_empresa, p_numpla, p_ano, p_mes
FROM ARGUMENTOS
ORDER BY ROWID DESC;

-- LPAT del mes para una empresa
SELECT p.cod_spring, pc.grupoid, pc.perfecini, pc.perfecfin,
       (LEAST(pc.perfecfin, LAST_DAY(TO_DATE('01/05/2026','DD/MM/YYYY')))
        - GREATEST(pc.perfecini, TO_DATE('01/05/2026','DD/MM/YYYY')) + 1) dias_en_mes
FROM SCA_PERMISO_CAB pc
JOIN PLA_PERSONAL p ON p.cod_empresa=pc.cod_empresa AND p.cod_personal=pc.cod_personal
WHERE pc.grupoid IN ('LPAT','DMED')
  AND p.cod_empresa='0003'
  AND p.cod_tipo_planilla='02'
  AND (TO_CHAR(pc.perfecini,'YYYYMM')='202605' OR TO_CHAR(pc.perfecfin,'YYYYMM')='202605')
ORDER BY pc.grupoid, p.cod_spring;
```

---

---

# COMPARACIÓN: INTERFACE_ASSITIME vs Interface_Assitime_Emp

---

## A. DIFERENCIAS DE DISEÑO FUNDAMENTALES

| Aspecto | `INTERFACE_ASSITIME` (v1) | `Interface_Assitime_Emp` (v2) |
|---|---|---|
| **Planilla** | `'05'` — maquinaria/planta | `'02'` — empleados/staff |
| **Parámetro fecha** | P_FECINI / P_FECFIN directos | P_ANO + P_MES → lee `SCA_MES_PROC` |
| **Rangos de fecha** | Solo uno (P_FECINI/P_FECFIN) | Dos: período proceso + mes calendario |
| **Log de ejecución** | NO | SÍ → `INSERT INTO ARGUMENTOS` |
| **Reset previo** | **NO** — riesgo residual | **SÍ** — idempotente |
| **Acceso a planilla** | Schema remoto (`SIG.INGRE_PLA`) | Sinónimo local (`SIG_INGRE_PLA`) |
| **Patrón de loop** | `FOR I IN C1 LOOP` | `OPEN/FETCH/WHILE/CLOSE` |

---

## B. DIFERENCIAS EN CONCEPTOS

| Concepto | v1 (planilla '05') | v2 (planilla '02') | Diferencia |
|---|---|---|---|
| Días/Horas normales | `'1000'` = horas efectivas + feriados | `'1000'` = 30 − días ausentes | **Modelo diferente** |
| Días efectivos | `'1074'` = días + feriados | **No existe** | Solo en v1 |
| HED 25% | `'1010'` → horaexofi1 | `'1010'` → horaexofi1 | Igual |
| HED 35% | `'1039'` → horaexofi2 | `'1008'` → horaexofi2 | **Concepto distinto** |
| Dobles 0001 | `'1011'` | `'1011'` | Igual |
| Dobles 0002 | `'1012'` | `'1012'` | Igual |
| Dobles 0003 | **`'1072'`** | `'1012'` | **Concepto distinto** |
| Tardanza | `'2018'` (minutos, umbral >10) | `'2018'` (minutos, umbral >10) | Igual |
| Turno 2 | `'1022'` = **horas efectivas** del turno | `'1023'` = **días count** del turno | **Concepto y unidad** |
| Turno 3 | `'1024'` = **horas efectivas**, solo `hortur='T3'` | `'1024'` = **días count**, `hortur IN ('T3','0311')` | **Unidad + código extra** |
| Feriados trabajados | Suma a '1074' y '1000' via HOLIDAY() + SCA_HORARIO_DET | **No existe** | Solo en v1 |
| Lic. Paternidad (LPAT) | **No existe** | `'1089'`(0003)/`'1068'`(0001/0002) | Solo en v2 |
| Descanso Médico (DMED) | **No existe** | `'1052'`(0001)/`'1019'`(0002)/`'1018'`(0003) | Solo en v2 |
| HEA (horaextantesofi) | **No mapeada** | **No mapeada** | Ausente en ambas |

---

## C. DIFERENCIAS EN LA LÓGICA DEL CONCEPTO '1000'

| | v1 — Horas normales | v2 — Días trabajados |
|---|---|---|
| **Tipo de valor** | Horas (decimal, ej: 176.25) | Días enteros (ej: 28) |
| **Cálculo** | SUM(horaefectiva + horatardanza) + horas feriados | 30 − COUNT(días ausentes) |
| **Incluye tardanza** | Sí (se suma con DATEADD) | No directamente |
| **Feriados** | Añade horas del horario teórico | No los considera |
| **Filtro fecha** | Rango P_FECINI/P_FECFIN | Mes exacto YYYYMM |
| **Fuente ausencia** | No aplica | alerta02='FT', alerta09='PE'+permiso, per_dia_comp='S' |

---

## D. DIFERENCIAS EN TURNO 2 ('1022' vs '1023') y TURNO 3 ('1024')

| | v1 Turno 2 `'1022'` | v2 Turno 2 `'1023'` |
|---|---|---|
| **Unidad** | **Horas** (decimal) | **Días** (entero) |
| **Fuente** | horaefectiva + horatardanza | COUNT de días |
| **Exclusión falta** | No excluye (suma 0 si no hay horaefectiva) | Explícita: alerta02≠'FT' AND PER_VACA IS NULL |
| **Empresa** | Todas (con planilla '05') | Solo 0001 y 0003 |

| | v1 Turno 3 `'1024'` | v2 Turno 3 `'1024'` |
|---|---|---|
| **Unidad** | **Horas** (decimal) | **Días** (entero) |
| **Filtro hortur** | `= 'T3'` | `IN ('T3','0311')` |
| **Empresa** | Todas | Solo 0001 y 0003 |

> Ambas versiones usan el **mismo código de concepto** `'1024'` para turno 3,
> pero generan **valores con unidades distintas** (horas vs días). Tener en cuenta
> si el sistema de planilla aplica una tasa diferente.

---

## E. DIFERENCIAS EN LA FUENTE DE PERMISOS

| | v1 | v2 |
|---|---|---|
| Fuente permisos | No usa `SCA_PERMISO_CAB` | Sí (LPAT y DMED) |
| Feriados | Usa `HOLIDAY()` + `SCA_HORARIO_DET` | No usa |
| Fuente de fechas feriados | `SCA_FECHA_PROCESO` | N/A |
| Restricción fotocheck | Sí (`FECINI_FOTOCHECK`, `FECFIN_FOTOCHECK`) | No |

---

## F. BUGS / RIESGOS IDENTIFICADOS

| # | Procedimiento | Descripción | Impacto |
|---|---|---|---|
| 1 | **v1 sin reset** | Si se re-ejecuta v1 después de que un empleado pierde HE autorizadas, el valor anterior permanece en planilla | Planilla con HE de más |
| 2 | **v2 DMED sin LPAD** | `P_ANO\|\|P_MES` sin padding — con P_MES='5' busca '20265' en lugar de '202605' | DMED no se reporta para mayo, julio, etc. si se pasan sin cero |
| 3 | **v2 febrero sin bisiesto** | `V_DATEFIN_C = '28/02/AAAA'` fijo | En años bisiestos, LPAT/DMED pierde el día 29 |
| 4 | **v2 '1000' filtro fecha** | Usa mes calendario YYYYMM, no el rango V_DATEINI/V_DATEFIN | Inconsistencia con el resto de conceptos en períodos que cruzan meses |
| 5 | **v2 empresa 0002 turnos** | Genera filas con valor_ori=0 para turno 2/3 — filtradas pero procesadas | Ineficiencia menor, no es error |
| 6 | **v1 INTERVAL '70' YEAR(2)** | Límite de 70 años para vigencia de fotocheck cuando no tiene fecha fin | Técnicamente solo es cosmético |

---

## G. TABLA RESUMEN DE CONCEPTOS POR VERSIÓN

```
CONCEPTO  |   v1 (planilla '05')        |   v2 (planilla '02')
----------|-----------------------------|------------------------------
'1074'    | Días efectivos (horas/8)    | —
'1000'    | Horas normales              | Días trabajados (30-ausentes)
'1022'    | Horas turno 2               | —
'1023'    | —                           | Días turno 2 (solo 0001/0003)
'1024'    | Horas turno 3               | Días turno 3 (solo 0001/0003)
'2018'    | Tardanza (min, umbral >10)  | Tardanza (min, umbral >10)
'1010'    | HED 25% (horaexofi1)        | HED 25% (horaexofi1)
'1039'    | HED 35% (horaexofi2)        | —
'1008'    | —                           | HED 35% (horaexofi2)
'1011'    | Dobles/HEO emp. 0001        | Dobles/HEO emp. 0001
'1012'    | Dobles/HEO emp. 0002        | Dobles/HEO emp. 0002 y 0003
'1072'    | Dobles/HEO emp. 0003        | —
'1068'    | —                           | LPAT emp. 0001/0002
'1089'    | —                           | LPAT emp. 0003
'1052'    | —                           | DMED emp. 0001
'1019'    | —                           | DMED emp. 0002
'1018'    | —                           | DMED emp. 0003
```

---

## H. ¿CUÁNDO SE EJECUTA CADA UNO?

```
Fin de período (quincenal o mensual según tipo planilla):

  Para planilla '05' (planta):
    EXEC INTERFACE_ASSITIME('0003', :numpla, '01/05/2026', '31/05/2026');

  Para planilla '02' (empleados):
    EXEC Interface_Assitime_Emp('0003', :numpla, '2026', '5');
    -- OJO: pasar P_MES con dos dígitos para evitar bug DMED:
    EXEC Interface_Assitime_Emp('0003', :numpla, '2026', '05');
```

**Prerequisito para HED/HEO**: PASO 14 del proceso nocturno debe haberse ejecutado
**después** de que RRHH autorizó en `SCA_AUTORIZACION`. Si se ejecuta el interface
antes del proceso, `horaexofi1/2` y `horadoblesof` estarán en NULL → valor 0 → no va a planilla.
