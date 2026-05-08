# Aquarius — INTERFACE_ASSITIME / Interface_Assitime_Emp — Análisis completo (07/05/2026)

## VISIÓN GENERAL

Existen **DOS versiones** del interface. Son SP distintos, para tipos de planilla distintos.

| Procedimiento | Tipo planilla | Período | Tabla destino |
|---|---|---|---|
| `INTERFACE_ASSITIME` | `'05'` (maquinaria/planta) | P_FECINI/P_FECFIN (DD/MM/YYYY) | `SIG.INGRE_PLA`, `SOLSA.INGRE_PLA`, `ARBONA.INGRE_PLA` |
| `Interface_Assitime_Emp` | `'02'` (empleados) | P_ANO + P_MES → `SCA_MES_PROC` | `SIG_INGRE_PLA`, `SOL_INGRE_PLA`, `ARB_INGRE_PLA` |

> **Diferencia crítica de alias**: La v1 usa `SIG.INGRE_PLA` (schema remoto); la v2 usa `SIG_INGRE_PLA` (sinónimo local). Son la misma tabla física pero diferente forma de acceso.

---

## PARÁMETROS

### INTERFACE_ASSITIME (v1 - planilla '05')
```sql
P_EMPRESA  VARCHAR2   -- '0001','0002','0003'
P_NUMPLA   NUMBER     -- número de planilla en sistema de pago
P_FECINI   VARCHAR2   -- 'DD/MM/YYYY' inicio del período
P_FECFIN   VARCHAR2   -- 'DD/MM/YYYY' fin del período
```

### Interface_Assitime_Emp (v2 - planilla '02')
```sql
P_EMPRESA  VARCHAR2   -- '0001','0002','0003'
P_NUMPLA   NUMBER     -- número de planilla en sistema de pago
P_ANO      VARCHAR2   -- '2026'
P_MES      VARCHAR2   -- '5' (sin cero) o '05'
```
La v2 lee las fechas reales del período desde `SCA_MES_PROC` (V_DATEINI/V_DATEFIN) y calcula el mes calendario (V_DATEINI_C/V_DATEFIN_C) para permisos. También registra cada ejecución en tabla `ARGUMENTOS`.

---

## CAMPO CLAVE: COD_SPRING ≠ COD_PERSONAL

`COD_SPRING` = código del empleado en el **sistema de planilla** (SIG/SOLSA/ARBONA).  
`COD_PERSONAL` = código en AQUARIUS asistencia.  
El JOIN es: `INGRE_PLA.C_CODIGO = PLA_PERSONAL.COD_SPRING`  
**Nunca usar COD_PERSONAL directo para cruzar con planilla.**

---

## FUNCIÓN DATEADD (custom Oracle)
`DATEADD(date1, date2)` suma dos columnas DATE de base 01/01/1900.  
Ejemplo: `DATEADD(horaefectiva, horatardanza)` = horaefectiva + tardanza (en minutos equivalentes).  
Se usa para conceptos de horas normales — la tardanza se "devuelve" al empleado en el cálculo de horas trabajadas.

---

## CONCEPTOS — INTERFACE_ASSITIME (planilla '05')

| Concepto | Descripción | Cálculo | Fuente en tareo |
|---|---|---|---|
| `'1074'` | Días efectivos | `ROUND(TRUNC(SUM(horaefectiva+tardanza)/60)/8, 2), 0)` + días feriados trabajados | `horaefectiva`, `horatardanza` |
| `'1000'` | Horas normales | `TRUNC(SUM(horaefectiva+tardanza)/60, 2)` + horas feriados | `horaefectiva`, `horatardanza` |
| `'1022'` | Horas turno 2 | `TRUNC(SUM(horaefectiva+tardanza)/60, 2)` WHERE `hortur='T2'` | `horaefectiva` |
| `'1024'` | Horas turno 3 | idem WHERE `hortur='T3'` | `horaefectiva` |
| `'2018'` | Tardanza (minutos) | `SUM(horatardanza minutos)` — solo si total > 10 | `horatardanza` |
| `'1010'` | HED 25% (horas) | `TRUNC(SUM(horaexofi1 minutos)/60, 2)` | `horaexofi1` ← de PASO 14 |
| `'1039'` | HED 35% (horas) | `TRUNC(SUM(horaexofi2 minutos)/60, 2)` | `horaexofi2` ← de PASO 14 |
| `'1072'/'1012'/'1011'` | Dobles/HEO (horas) | `TRUNC(SUM(horadoblesof minutos)/60, 2)` | `horadoblesof` ← de PASO 14 |

**Empresa → concepto dobles**: `0003→'1072'`, `0002→'1012'`, `0001→'1011'`

### Cálculo especial conceptos '1074' y '1000': Feriados trabajados
Para cada día feriado trabajado (`HOLIDAY(...) = 'F'`) en el período:
- Se suman las horas del horario del día (`SCA_HORARIO_DET.TOTHORAS`)
- Filtro `APLICA='S'` en `SCA_HORARIO_DET`
- Divide por 60/8 para días ('1074') o por 60 para horas ('1000')

### Filtro final del cursor
```sql
WHERE VALOR_ORI > 0  -- NO se graban filas con cero
```
**OJO**: No hay paso previo de reset a cero. Si el empleado ya no tiene HE pero antes sí tenía, el valor antiguo queda en planilla.

---

## CONCEPTOS — Interface_Assitime_Emp (planilla '02')

| Concepto | Descripción | Cálculo | Fuente |
|---|---|---|---|
| `'1000'` | Días trabajados | `30 - SUM(dias_ausentes)` | `alerta02='FT'` ó permiso especial ó `per_dia_comp='S'` |
| `'2018'` | Tardanza (minutos) | `SUM(horatardanza minutos)` — solo si > 10 | `horatardanza` |
| `'1011'/'1012'` | Dobles/HEO (horas) | `TRUNC(SUM(horadoblesof min)/60, 2)` | `horadoblesof` ← PASO 14 |
| `'1010'` | HED 25% (horas) | `TRUNC(SUM(horaexofi1 min)/60, 2)` | `horaexofi1` ← PASO 14 |
| `'1008'` | HED 35% (horas) | `TRUNC(SUM(horaexofi2 min)/60, 2)` | `horaexofi2` ← PASO 14 (**'1008' ≠ '1039' de v1**) |
| `'1023'` | Días turno 2 | COUNT dias hortur='T2' sin falta ni vacaciones | `hortur`, `alerta02`, `per_vaca` |
| `'1024'` | Días turno 3 | COUNT dias hortur='T3'/'0311' sin falta ni vacaciones | `hortur` |
| `'1089'/'1068'` | Licencia Paternidad | Días LPAT en el mes calendario | `SCA_PERMISO_CAB.grupoid='LPAT'` |
| `'1018'/'1019'/'1052'` | Descanso Médico | Días DMED en el mes calendario | `SCA_PERMISO_CAB.grupoid='DMED'` |

**Empresa → concepto LPAT**: `0003→'1089'`, `0001'/'0002'→'1068'`  
**Empresa → concepto DMED**: `0003→'1018'`, `0002→'1019'`, `0001→'1052'`  
**Empresa → concepto dobles**: `0001→'1011'`, `0002/'0003'→'1012'` (distinto al de v1!)

### Concepto '1000' v2: lógica de días ausentes
```sql
30 - SUM(CASE
  WHEN alerta02 = 'FT'  -- falta total
  OR (alerta09 = 'PE'   -- permiso especial con alguno de:
      AND (PER_SUBSIDIO IS NOT NULL OR PER_SGOCE IS NOT NULL OR PER_VACA IS NOT NULL
           OR PER_LIC_SIND IS NOT NULL OR PER_DESC_MED IS NOT NULL
           OR PER_SUSPENSION IS NOT NULL OR PER_GOCE_FIS IS NOT NULL
           OR PER_LIC_PAT IS NOT NULL OR PER_LIC_FAC IS NOT NULL))
  OR PER_DIA_COMP = 'S'
  THEN 1 ELSE 0
END)
```
Filtro: `TO_CHAR(fechamar,'YYYYMM') = P_ANO||LPAD(P_MES,2,'0')` (mes exacto, no rango de fechas)

### Paso previo de RESET a cero (v2 solamente)
Antes de actualizar, la v2 limpia los conceptos a 0:
```sql
UPDATE SIG_INGRE_PLA SET VALOR_ORI = 0
WHERE NUM_PLA = P_NUMPLA
AND C_CONCEPTO IN ('1000','1012','1010','1008','1023','1024','1089','1018','2018')
```
La v1 **NO tiene** este reset → riesgo de valores residuales si re-ejecuta.

---

## DIFERENCIAS CRÍTICAS ENTRE VERSIONES

| Característica | v1 (planilla '05') | v2 (planilla '02') |
|---|---|---|
| Concepto HED 35% | `'1039'` | `'1008'` |
| Concepto dobles 0003 | `'1072'` | `'1012'` |
| Concepto días normales | Horas efectivas (continuo) | 30 - días_ausentes (discreto) |
| Reset previo | NO | SÍ |
| Turno 2/3 | Horas efectivas del turno | Días count del turno |
| Permisos LPAT/DMED | No incluye | Sí incluye |
| Fuente fechas | Parámetros directos | `SCA_MES_PROC` |
| HEA (`horaextantesofi`) | **No mapeada** en ninguna versión | **No mapeada** |

---

## AUSENCIA DE HEA EN EL INTERFACE

`horaextantesofi` (Horas Extras Antes) **no aparece en ningún concepto** de ninguna versión.  
Posibles explicaciones:
1. Se paga por otro concepto no visible aquí
2. Se convierte a banco de horas (tippagohe='2') y no va a planilla de dinero
3. Es marginal (solo 23 registros en BD vs 16,356 de HED)

---

## FLUJO DE EJECUCIÓN v2

```
1. INSERT INTO ARGUMENTOS  (log de ejecución)
2. SELECT FECINI/FECFIN de SCA_MES_PROC (período de proceso)
3. Calcular V_DATEINI_C/V_DATEFIN_C (mes calendario para permisos)
4. UPDATE INGRE_PLA SET VALOR_ORI = 0  (reset conceptos)
5. OPEN C1 → FETCH loop → UPDATE INGRE_PLA SET VALOR_ORI = valor
6. COMMIT
```

---

## REGLAS CRÍTICAS

1. **COD_SPRING** es la clave de cruce con planilla, no COD_PERSONAL.
2. **v1 sin reset**: Si se re-ejecuta v1 con menor valor, no baja el previo. Siempre queda el último valor > 0.
3. **HED entra a planilla solo si PASO 14 ya autorizó**: `horaexofi1/2` = NULL si sin auth → `VALOR_ORI = 0` → no se actualiza planilla.
4. **Tardanza**: umbral de 10 minutos (`VALOR_ORI > 10`). Tardanzas ≤ 10 min no van a planilla.
5. **Feriados**: la v1 los suma a '1074' y '1000' usando el horario teórico del día (no marcaciones).
6. **Febrero**: la v2 hardcodea 28 días. No considera años bisiestos.
7. **APLICA='S'**: condición en `SCA_HORARIO_DET` para que el día del horario aplique al cálculo de feriados.
8. **Permisos LPAT/DMED**: usan `SCA_PERMISO_CAB`, no `SCA_ASISTENCIA_TAREO`. Cruzan con mes calendario, no con período de proceso.

---

## CONSULTAS ÚTILES

```sql
-- Ver qué conceptos actualizaría INTERFACE_ASSITIME para un empleado
SELECT p.cod_spring, p.ape_paterno||','||p.nom_trabajador nombre,
       TRUNC(SUM(CASE WHEN t.horaexofi1 IS NOT NULL THEN
         (TO_NUMBER(TO_CHAR(t.horaexofi1,'HH24'))*60+TO_NUMBER(TO_CHAR(t.horaexofi1,'MI'))) ELSE 0 END)/60,2) conc_1010,
       TRUNC(SUM(CASE WHEN t.horaexofi2 IS NOT NULL THEN
         (TO_NUMBER(TO_CHAR(t.horaexofi2,'HH24'))*60+TO_NUMBER(TO_CHAR(t.horaexofi2,'MI'))) ELSE 0 END)/60,2) conc_1039,
       TRUNC(SUM(CASE WHEN t.horadoblesof IS NOT NULL THEN
         (TO_NUMBER(TO_CHAR(t.horadoblesof,'HH24'))*60+TO_NUMBER(TO_CHAR(t.horadoblesof,'MI'))) ELSE 0 END)/60,2) conc_1072
FROM SCA_ASISTENCIA_TAREO t
JOIN PLA_PERSONAL p ON p.cod_empresa=t.cod_empresa AND p.cod_personal=t.cod_personal
WHERE t.cod_empresa='0003'
  AND t.fechamar BETWEEN TO_DATE('01/05/2026','DD/MM/YYYY') AND TO_DATE('31/05/2026','DD/MM/YYYY')
  AND p.cod_tipo_planilla='05'
GROUP BY p.cod_spring, p.ape_paterno, p.nom_trabajador
HAVING TRUNC(SUM(CASE WHEN t.horaexofi1 IS NOT NULL THEN
  (TO_NUMBER(TO_CHAR(t.horaexofi1,'HH24'))*60+TO_NUMBER(TO_CHAR(t.horaexofi1,'MI'))) ELSE 0 END)/60,2) > 0
   OR TRUNC(SUM(CASE WHEN t.horadoblesof IS NOT NULL THEN
  (TO_NUMBER(TO_CHAR(t.horadoblesof,'HH24'))*60+TO_NUMBER(TO_CHAR(t.horadoblesof,'MI'))) ELSE 0 END)/60,2) > 0
ORDER BY p.ape_paterno;

-- Verificar si un empleado tiene COD_SPRING asignado
SELECT cod_personal, cod_spring, ape_paterno, cod_tipo_planilla
FROM PLA_PERSONAL
WHERE cod_empresa='0003' AND cod_spring IS NOT NULL
ORDER BY cod_tipo_planilla, ape_paterno;
```
