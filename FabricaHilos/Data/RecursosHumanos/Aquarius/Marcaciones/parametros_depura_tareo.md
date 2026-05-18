# PKG_SCA_DEPURA_TAREO — Parámetros, Umbrales y Consideraciones
> Extraído directamente del código del paquete. Estado: 18/05/2026.

---

## 1. PARÁMETROS FIJOS (hardcodeados en el código)

Estos valores **no se leen de tablas** — están escritos directamente en el paquete y solo cambian con una modificación del código.

| # | Parámetro | Valor | Dónde aplica |
|---|-----------|-------|--------------|
| 1 | Umbral duplicado cercano (0-DUP) | **< 5 minutos** | Elimina marcas casi idénticas del biométrico |
| 2 | Umbral marca huérfana (0-ORF) | **> 90 minutos** del refrigerio teórico | Elimina marcas intermedias que no son refrigerio |
| 3 | Umbral restauración par refrigerio (0-RESTORE) | **≥ 5 minutos** entre IniRefri y FinRefri | No restaura si el par es casi idéntico (near-dup o nulo) |
| 4 | Umbral marcas anómalas (4B) | **4 marcas en < 60 minutos** | Corrige jornada completa con horario teórico |
| 5 | Entrada anticipada — zona de ajuste (1B) | **> 15 min y < 1 hora** antes del turno | Ajusta entrada a `hora_entrada_turno − 15 min` |
| 6 | Entrada anticipada — zona de HE (1B-HE) | **≥ 1 hora** antes del turno | No ajusta; registra como Horas Extras antes de entrada |
| 7 | Entrada anticipada — excluir 3er turno (1B/1B-HE) | **≥ 2 horas** antes del turno con horario ≥ 22:00 | PASO 5G lo maneja separado |
| 8 | Ajuste al ajustar entrada (1B) | **−15 minutos** respecto a entrada teórica | Valor de ajuste fijo |
| 9 | Refrigerio de obrero nocturno (5G) | **30 minutos** | Siempre se asigna en 3er turno |
| 10 | Exclusión de marca cercana al borde E/S (2B-PRE) | **< 30 minutos** de entrada o salida | No la considera como refrigerio |
| 11 | Ventana de búsqueda de refrigerio en historial (2B-PRE) | Entrada **+30 min** hasta salida **−30 min** (extensible si salida imposible) | Busca marcas intermedias en SCA_HISTORIAL |
| 12 | Refrigerio anómalo descartado (2A) | Duración real **< 50%** del teórico | Se limpia y usa teórico |
| 13 | HE mínima reconocida (5B) | **≥ 1 hora entera** después de salida | Extras < 1h se limpian a 00:00 (PASO 5B-TAG) |
| 14 | HE se **trunca** a horas enteras (5B) | Sin redondeo hacia arriba | Ej: 1h 30min → se registra 1h, no 2h |
| 15 | Ventana para detectar entrada mal asignada — turno vespertino (0B3d/N7) | **±2 horas** respecto a entrada teórica | Busca marca temprana ignorada por Aquarius |
| 16 | Umbral turno nocturno (0A/0B/0B3) | Entrada **≥ 18:00** | Activa lógica de mover marca de madrugada al día anterior |
| 17 | Umbral madrugada para mover marcas (0A/PHANTOM) | Marca **< 08:00** | Se considera perteneciente al turno del día anterior |
| 18 | Umbral turno 3er turno (PASO 5G/0C/0D) | Horario entrada **≥ 22:00** | Activa lógica de tercer turno con sobretiempo |
| 19 | Umbral madrugada en descanso (PHANTOM-D) | Entrada **< 08:00** + día anterior nocturno ≥ 18:00 | Marca como fantasma de turno anterior |
| 20 | Salida de madrugada en PHANTOM-B | Salida **< 12:00** + descanso + día anterior nocturno | Fantasma de turno nocturno anterior |
| 21 | Salida en día siguiente — SWAP de nocturno (0-SWAP) | Entrada **< 08:00** y salida **≥ 18:00** | Intercambia: la salida pasa a ser entrada del nuevo turno |
| 22 | Marca cruzada día siguiente (0B3c — sobretiempo N6) | Salida en `dia+1` con hora **< 12:00** | Empleado trabajó más allá de la salida programada |

---

## 2. PARÁMETROS QUE SE LEEN DE LA BASE DE DATOS

Estos valores vienen de los registros de configuración del tareo de cada empleado (campo a campo en `SCA_ASISTENCIA_TAREO`). El paquete los lee y los usa; **no los calcula ni los define**.

| Campo en BD | Descripción | Cómo lo usa el paquete |
|-------------|-------------|------------------------|
| `ENTRADA_FIJADA` | Hora de entrada teórica del horario | Referencia para tardanza, anticipación y HE antes de entrada |
| `SALIDA_FIJADA` | Hora de salida teórica del horario | Referencia para calcular HE después de salida |
| `TOTHORAS` | Total horas jornada teórica | Techo de `horaefectiva` |
| `HORINIREF` / `HORFINREF` | Inicio y fin de refrigerio teórico | Base para buscar marcas reales y calcular faltantes |
| `TOTREF` | Duración del refrigerio teórico | Fallback para calcular IniRefri/FinRefri |
| `HORTUR` | Tipo de turno (`T1`, `T2`, `T3`) | Identifica 3er turno |
| `HORCLA` | Clasificación del horario (`AM`, `PM`, etc.) | Usado en lógica de nocturnos cruzados |
| `HORINIHORNOC` / `HORFINHORNOC` | Ventana de horas nocturnas | Calcula `tothoranocturna` (intersección jornada ∩ ventana nocturna) |
| `AJUSTE_HEXTRA` | Redondeo para `horaextra_ajus` (en minutos) | Si el total de HE no es múltiplo de este valor, se trunca al múltiplo inferior |
| `AJUSTE_TOTHORANOCTURNA` | Ajuste de horas nocturnas | Aplicado en PASO 5A |
| `REDONDEO_TOTHORANOCTURNA` | Redondeo de horas nocturnas (en minutos) | Aplicado en PASO 5A |
| `H25F` | Límite superior del tramo al 25% | `horaextra1 = MIN(totalHE, H25F)` |
| `H35I` / `H35F` | Rango del tramo al 35% | `horaextra2` aplica en este rango |
| `HNI` | Inicio del tramo de dobles (100%) | `horaextra3` aplica a partir de HNI |
| `MIN_MIN_RAZ_HEXTRA` | Minutos máximos de HE "razonables" | Si se supera → alerta `EE` (excede razonabilidad) |
| `TIPPAGOHE` | Tipo de pago de HE (`'1'`=dinero, `'2'`=banco) | Determina si se recalcula `horaextra_ajus` o `horabancoh` |
| `HAYHEA_PORAUT` | Indicador HE-antes pendiente de autorización (`'S'`) | Si = `'S'` → no se modifica esa HE |
| `HAYHED_PORAUT` | Indicador HE-después pendiente de autorización (`'S'`) | Si = `'S'` → no se recalcula en PASO 5B |
| `MIN_A_PART_HEXTRA` | Minutos mínimos para que cuente como HE oficial | Umbral de oficialización |
| `MINTOLEING` | Tolerancia de ingreso (minutos) | Tardanza debajo de este umbral no se penaliza |
| `IND_OBRERO` | `'S'` = obrero (requiere 4 marcas: E+IR+FR+S) | Si es obrero → se exige y completa el par de refrigerio |
| `DESCANSO` | `'S'` = día de descanso | PASO 7: todas las horas trabajadas son dobles |
| `DESCANSOROTATIVO` | `'S'` = descanso rotativo | Considerado en lógica de descansos |
| `FERIADO` | `'F'` = feriado | Considerado en lógica de horas dobles |
| `IND_FLEXIBLE` | Horario flexible | Excluye ciertas validaciones de tardanza |
| `NUM_FOTOCHECK` | Número de tarjeta del empleado | Clave de unión con `SCA_HISTORIAL` |

---

## 3. PARÁMETROS DE EJECUCIÓN (pasan al llamar el paquete)

Cuando se ejecuta `DEPURA_TOTAL` o `DEPURA_RANGO` se pasan estos parámetros:

| Parámetro | Tipo | Valores posibles | Descripción |
|-----------|------|-----------------|-------------|
| `p_cod_empresa` | VARCHAR2 | `'0001'`, `'0002'`, `'0003'` / NULL | Empresa (NULL = todas) |
| `p_cod_personal` | VARCHAR2 | Código del empleado / NULL | Empleado específico (NULL = todos) |
| `p_fecha` | VARCHAR2 `dd/MM/yyyy` | Ej: `'18/05/2026'` | Fecha a depurar |
| `p_fecha_inicio` / `p_fecha_fin` | VARCHAR2 `dd/MM/yyyy` | Rango | Solo para `DEPURA_RANGO` |
| `p_solo_obreros` | `'S'` / `'N'` | `'S'` = solo obreros, `'N'` = todos | Filtra tipo de empleado |

---

## 4. PERMISOS/AUSENCIAS QUE BLOQUEAN EL PROCESO

Si un empleado tiene **cualquiera** de estos permisos activos en el día, el paquete **NO completa marcaciones** (PASO 3A, 3B, y derivados los excluyen):

| Campo en BD | Tipo de ausencia |
|-------------|-----------------|
| `per_desc_med` | Descanso médico |
| `per_subsidio` | Subsidio |
| `per_goce` | Permiso con goce |
| `per_sgoce` | Permiso sin goce |
| `per_vaca` | Vacaciones |
| `per_suspension` | Suspensión |
| `per_lic_pat` | Licencia por paternidad |
| `per_lic_fac` | Licencia por fallecimiento |

> **Nota:** El campo `per_dia_comp` (día compensatorio) se verifica por separado en otros contextos.

---

## 5. CONDICIONES QUE ACTIVAN CADA PASO (resumen ejecutivo)

| PASO | Se activa cuando… | Valor clave |
|------|-------------------|------------|
| **0-DUP** | Hay 2 marcas del mismo empleado/día separadas por menos de... | **< 5 min** |
| **0-ORF** | Hay marca intermedia a más de… del refrigerio teórico | **> 90 min** |
| **0-RESTORE** | Campo E/IR/FR/S del tareo no tiene marca en historial (y par refri ≥...) | **≥ 5 min** entre IR y FR |
| **PHANTOM** | Día de descanso con marcas heredadas del turno nocturno anterior | Entrada o salida < 08:00 + ayer entrada ≥ 18:00 |
| **0A/0B** (N1) | Entrada del día actual está en madrugada + ayer fue nocturno | Entrada < 08:00 + ayer ≥ 18:00 |
| **0-SWAP** (N4) | Entrada < 08:00 y salida ≥ 18:00 (marcas cruzadas) | Entrada < 08:00 |
| **0B3d** (N7) | Turno vespertino: entrada actual > teórica +2h y hay marca anterior válida | **> 2h** de desvío |
| **0B3c** (N6) | Turno nocturno: salida < entrada (misma fecha) y hay marca posterior en día siguiente | Salida < 12:00 en día+1 |
| **1** (E1) | Solo tiene salida, falta entrada | Entrada NULL |
| **2** (S1) | Solo tiene entrada, falta salida | Salida NULL |
| **1B** (E2) | Llegó entre 15 min y menos de 1h antes del turno | 15/1440 < anticipación < 1/24 días |
| **1B-HE** (E2H) | Llegó **1 hora o más** antes (HE real) | anticipación ≥ 1/24 días (1h) |
| **2B-PRE** (R4/R5) | Falta IniRefri/FinRefri y hay marcas intermedias en historial | Ventana: entrada+30min a salida−30min |
| **2B** (R1) | Aún falta refrigerio después de buscar en historial | IniRefri = FinRefri = teórico del horario |
| **2A** (R6) | Refrigerio del tareo < 50% del teórico | Duración real < 50% de `totref` |
| **3C-NOC** (RN) | Turno nocturno sin entrada anticipada (> 2h) tiene refrigerio asignado | Entrada dentro de 2h del turno + sin anticipación |
| **3D** (SS/SSR) | Salida < IniRefri (cronológicamente imposible) | Hora salida < hora IniRefri |
| **3E** (RI) | IniRefri < Entrada (refrigerio anterior a la entrada) | Hora IniRefri < hora Entrada |
| **3F** (RT) | Salida < FinRefri (corte dentro del refri) y turno **no nocturno** | `TRUNC(salida_fijada) = TRUNC(entrada_fijada)` — mismo día calendario |
| **4B** (A1) | 4 marcas en menos de 1 hora | Diff entrada-4ª marca < 60 min |
| **5B-TAG** (HE) | Salida > salida_fijada pero diferencia < 1h con HE calculada | `(salida - salida_fijada) * 24 < 1` |
| **5B** | Recalcula HE solo en registros ya modificados por el paquete | `codaux4 IS NOT NULL` |
| **5B-3** (EE) | HE ajustada supera el límite de razonabilidad | `horaextra_ajus (minutos) >= min_min_raz_hextra` |
| **7** (DC) | Día de descanso con entrada y salida reales | `descanso='S'` + entrada IS NOT NULL + salida IS NOT NULL |
| **7A** (E2) | Descanso con entrada anticipada ≥ 15 min antes del horario normal del turno | Busca en `SCA_HORARIO_DET` la hora de un día no-descanso |

---

## 6. UMBRALES PARA RECONOCER HORAS EXTRAS (HEA y HED)

### 6.1 Horas Extras ANTES de la entrada (HEA)

El sistema analiza cuánto tiempo antes del turno llegó el empleado y aplica **tres zonas**:

| Zona | Rango de anticipación | ¿Genera HE? | ¿Ajusta entrada? | PASO |
|------|-----------------------|-------------|-----------------|------|
| **Zona normal** | ≤ 15 minutos antes | ❌ No | ❌ No | — (dentro de tolerancia) |
| **Zona ajuste** | > 15 min y < 1 hora antes | ❌ No | ✅ Sí → entrada = turno − 15 min | 1B (E2) |
| **Zona HE** | ≥ 1 hora antes | ✅ Sí | ❌ No (mantiene hora real) | 1B-HE (E2H) |

> **Excepción 3er turno (≥ 22:00):** Si el empleado llegó **≥ 2 horas** antes, el PASO 5G lo maneja aparte (con su propio cálculo de refrigerio). Los PASOs 1B y 1B-HE **lo excluyen** en ese caso.

**Cómo se calcula `horaextantes` en zona HE:**
```
horaextantes = entrada_fijada − entrada_real   (truncado a horas enteras)
Ejemplo: turno 07:00, llegó 05:30 → 1h 30min → horaextantes = 1h 00min
```

**Condición de no-modificación:** Si `hayhea_poraut = 'S'` (ya hay una autorización de HEA pendiente del supervisor) el paquete **no toca** ese día.

---

### 6.2 Horas Extras DESPUÉS de la salida (HED)

| Zona | Condición | ¿Genera HE? | PASO |
|------|-----------|-------------|------|
| **Sin HE** | Salida ≤ salida teórica | ❌ No | — |
| **HE descartada** | 0 < exceso < 1 hora | ❌ No → se limpia a 00:00 | 5B-TAG + 5B |
| **HE reconocida** | Exceso **≥ 1 hora entera** | ✅ Sí | 5B (E2H) |

**Cómo se calcula `horaextra` en zona HE:**
```
horaextra = TRUNC((salida_real − salida_fijada) en horas)   ← horas enteras, sin minutos
Ejemplo: salida 20:45, turno termina 19:00 → 1h 45min → horaextra = 1h 00min
```

> **Ajuste nocturno:** Si el turno cruza medianoche (`salida_fijada < entrada_fijada` en fecha), se agrega +1 día a `salida_fijada` antes de calcular para evitar diferencias falsas de 28 horas.

**Condición de no-modificación:** Si `hayhed_poraut = 'S'` (autorización HED pendiente del supervisor) el paquete **no recalcula** ese día.

---

### 6.3 Resumen visual de umbrales

```
HEA (antes de entrada):
   |--- ≤15min ---|--- 15min a 1h ---|--- ≥ 1h ---|--- ≥ 2h + 3er turno ---|
   [ sin acción ] [ ajusta entrada  ] [  HE real  ] [   PASO 5G especial    ]

HED (después de salida):
   |--- ≤0min ---|--- 0 a <1h ---|--- ≥ 1h entera ---|
   [ sin HE     ] [  HE = 0     ] [  HE registrada   ]
```

---

## 7. LÓGICA DE HORAS EXTRAS — TRAMOS (5B-4)

> Aplica **después** de haber determinado que hay HE (ver sección 6). Los tramos se aplican sobre el total ya reconocido.

Los tramos se calculan sobre `totalhorasextras` para **horaextra1/2/3** y sobre `horaextra_ajus` para **horaexofi1/2/3** (las oficiales que van a planilla):

```
horaextra1 = MIN(totalHE, H25F)                     → Tramo 25%
horaextra2 = ENTRE H35I y H35F (de totalHE − H25F) → Tramo 35%
horaextra3 = totalHE − H35F  (si totalHE > HNI)    → Tramo 50%/Dobles
```

> Los valores H25F, H35I, H35F, HNI son **propios de cada horario/planilla** y se leen de `SCA_ASISTENCIA_TAREO`. El paquete no los define.

---

## 7. AJUSTE DE HORAS EXTRAS (`AJUSTE_HEXTRA`)

`horaextra_ajus` = total HE oficial **truncado al múltiplo inferior** de `ajuste_hextra` minutos.

Ejemplo con `ajuste_hextra = 30`:
- HE total = 2h 10min (130 min) → `130 MOD 30 = 10` → se descuentan 10 min → `horaextra_ajus = 2h 00min`
- HE total = 3h 00min (180 min) → `180 MOD 30 = 0` → sin descuento → `horaextra_ajus = 3h 00min`

> Si `ajuste_hextra` es NULL o 0, no se aplica redondeo.

---

## 8. ALERTA DE RAZONABILIDAD (`min_min_raz_hextra`)

Si `horaextra_ajus` en minutos **≥ `min_min_raz_hextra`** → `alerta06 = 'EE'` (Excede Razonabilidad).

- `alerta06 = 'EN'` → extras normales dentro del rango aceptable
- `alerta06 = 'EE'` → extras sospechosamente altas; el supervisor debería revisar

El valor de `min_min_raz_hextra` viene de la configuración de la planilla/empresa.

---

## 9. HORAS NOCTURNAS (PASO 5A)

Se calcula como la **intersección** entre el tiempo real trabajado (entrada → salida) y la **ventana nocturna** configurada (`horinihornoc` → `horfinhornoc`).

Luego se aplica:
1. `ajuste_tothoranocturna`: ajuste fijo en minutos
2. `redondeo_tothoranocturna`: redondeo al múltiplo (ej: 15 min) → resultado = `tothoranocturna_of`

---

## 10. CONDICIÓN ESPECIAL: 3ER TURNO CON SOBRETIEMPO (PASO 5G)

Se activa cuando:
- Horario ≥ 22:00 (tercer turno)
- Entrada anticipada **≥ 2 horas** antes del turno

Comportamiento:
- La entrada **no se ajusta** (es HE válida)
- Se busca marca intermedia para IniRefri dentro de la ventana entrada+30min hasta entrada_fijada
- Si hay 1 marca: IniRefri = marca, FinRefri = marca + 30 min
- Si hay 2 marcas: IniRefri y FinRefri = marcas reales (par completo)

---

## 11. RESUMEN DE HORARIOS UTILIZADOS COMO FRONTERA

| Hora | Significado en el código |
|------|--------------------------|
| **< 08:00** | Se trata como "madrugada" — pertenece al turno nocturno del día anterior |
| **< 12:00** | Salida de madrugada en PHANTOM-B |
| **≥ 18:00** | Entrada de turno nocturno/vespertino — activa lógica N1 |
| **≥ 22:00** | Entrada de 3er turno (medianoche) |
| **'00:00'** en `horiniref` | Horario sin refrigerio teórico (ej: VIGILANCIA) — ORF lo excluye para no eliminar marcas válidas |

---

## 12. REGLA DE PROCESAMIENTO: QUIÉN SE PROCESA Y QUIÉN NO

| Condición | ¿Se procesa? |
|-----------|-------------|
| Empleado con ≥ 1 marcación real en el día | ✅ SÍ |
| Empleado con 0 marcaciones | ❌ NO (proceso independiente futuro) |
| Día de descanso con 0 marcas en SCA_HISTORIAL | ❌ NO → limpiar fantasmas (PHANTOM) |
| Empleado con permiso/ausencia activo | ✅ Parcial — solo se completa entrada/salida; NO se completan IR/FR |
| Día cerrado (`ind_cerrado='S'`) | ❌ NO |
| HE con autorización activa (`hayhed_poraut='S'`) | ❌ NO se recalcula esa HE |

---

*Archivo generado para reunión de usuario. Última versión del código analizada: PKG_SCA_Depura_Tareo.sql — 14/05/2026.*
