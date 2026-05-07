# PKG_SCA_DEPURA_TAREO — Análisis Completo del BODY (23/04/2026)

## ESTRATEGIA GENERAL
- SCA_HISTORIAL = datos crudos. Solo INSERT/UPDATE fec_equiv/motivo. NUNCA modificar hora original.
- SCA_ASISTENCIA_TAREO = área de trabajo. Lee + escribe.
- Todas las marcas generadas tienen motivo LIKE 'DEPURACION%' → fácil limpiar con ROLLBACK.
- codaux4 / codaux5 = audit trail (códigos | descripciones, max 50 chars).
- Solo procesa empleados con >= 1 marcación real. 0 marcas = proceso futuro.
- COMMIT por día (DEPURA_RANGO llama DEPURA_TOTAL por cada día).

## CONSTANTES DE CÓDIGO (codaux4)
| Código | PASO | Descripción |
|---|---|---|
| N1 | 0A/0B/0B3/0B4 | Nocturno: marca movida entre días |
| N2 | 0C | Salida nocturna reubicada como entrada |
| N3 | 0D | Entrada mañana movida a salida, entrada=teórico |
| N4 | 0B2/0B5/0-SWAP | Vespertino/SWAP: salida→entrada |
| N5 | 0B3b | Salida nocturna teórica (calculada) |
| N6 | 0B3c | Salida extendida al día siguiente (sobretiempo) |
| N7 | 0B3d | Entrada vespertina corregida a marca temprana |
| NC | 0-CLEAN | Marca duplicada de madrugada limpiada |
| E1 | 1 | Entrada completada con teórico |
| E2 | 1B/7A | Entrada anticipada ajustada (-15min) |
| E3 | 1C | Entrada=Salida duplicada → corregida salida |
| E4 | 1C-NOC | Duplicada nocturna → entrada=teórico nocturno |
| S1 | 2 | Salida completada con teórico |
| R1 | 2B | IniRefri+FinRefri teórico (base 01/01/1900) |
| R2 | 3A | IniRefri calculado (FinRefri - totref) |
| R3 | 3B | FinRefri calculado (IniRefri + totref) |
| R4 | 2B-PRE/5G | IniRefri real encontrado en SCA_HIS |
| R5 | 2B-PRE | FinRefri real encontrado en SCA_HIS |
| R6 | 2A | Refrigerio anómalo limpiado (<50% del teórico) |
| A1 | 4B | Marcaciones anómalas (<1h) → teórico completo |
| RC | 5/5G(else) | Horas recalculadas |
| DC | 7 | Descanso con marcaciones: dobles calculadas (dispara con t.descanso='S' O SCA_HORARIO_DET.descanso='S' del DIAID) |
| HE | 5B-TAG | Hora extra < 1h detectada |
| MF | 8-PRE | Marca faltante insertada en SCA_HIS |
| RN | 3C-NOC | Nocturno sin anticipación: refrigerio limpiado |
| NC | 0-CLEAN | Marca duplicada madrugada de turno nocturno ayer |
| SS | 3D(B) | Salida imposible → salida_fijada (teórico) |
| SSR | 3D(A) | Salida imposible → salida real oculta restaurada |
| RI | 3E | Refrigerio imposible (inirefri < entrada) |
| PH | 6-PHANTOM/B/C/D | Descanso con marcas fantasma → limpiado |

---

## ORDEN DE EJECUCIÓN COMPLETO (DEPURA_TOTAL)

### FASE 0-LIMPIEZA (en SCA_HISTORIAL)
1. **0-CLEAN** — DELETE marcas DEPURACION% (de runs anteriores)
2. **0-DUP** — DELETE near-duplicates (<5 min), protege E/IR/FR/S del tareo
3. **0-ORF** — LOOP DELETE marca huérfana intermedia (>90 min del refri, comparación minutos del día)
4. **0-RESTORE** — LOOP INSERT restores E/IR/FR/S si faltan en SCA_HIS (threshold >= 5/1440)
5. **0-RESTORE-B** — LOOP DELETE marca de ronda si total_marcas=IMPAR (>90 min del refri)
6. **0-PRE** — UPDATE SCA_ASISTENCIA_TAREO: nummarcaciones y alerta01 sincronizados con SCA_HIS; excluye días DESCANSO con tag PH

### FASE 0-PHANTOM (siempre antes de cualquier PASO de marcaciones)
7. **6-PHANTOM** — UPDATE: DESCANSO + 0 marcas SCA_HIS → limpia E/S/IR/FR/horas; tag PH
8. **6-PHANTOM-B** — UPDATE: DESCANSO + entrada=NULL + salida_madrugada + dia_ant_nocturno → tag PH
9. **6-PHANTOM-C** — UPDATE: DESCANSO + entrada NOT NULL + entrada NO en SCA_HIS + dia_ant_nocturno → tag PH
10. **6-PHANTOM-D** — UPDATE: DESCANSO + entrada<08:00 + dia_ant_nocturno → tag PH

### FASE 0-NOCTURNOS (ajustes entre días)
11. **0-CLEAN** *(real name in code)* — LOOP: entrada=salida de madrugada + ayer nocturno → mueve marca a ayer o limpia; tag NC
12. **0-SWAP** — LOOP: entrada<08:00 + salida>=18:00 + entrada=ayer.salida → intercambia campos; tag N4
13. **0A** — UPDATE dia_anterior: salida = hoy.entrada_madrugada; tag N1
14. **0B** — UPDATE hoy: entrada = NULL (fue movida a ayer); tag N1
15. **0B-HIS** — UPDATE SCA_HIS: fec_equiv de marca madrugada → dia_anterior; motivo DEPURACION N1
16. **0B2** — UPDATE hoy: si entrada=NULL + salida>=15:00 después de N1 → entrada=salida, salida=NULL; tag N4
17. **0B3** — UPDATE hoy: entrada_nocturna>=18:00 + salida=NULL + sig.entrada<08:00 → salida=sig.entrada; tag N1
18. **0B3c** — UPDATE hoy: nocturno + salida IS NOT NULL + salida<entrada (inválida) → salida=MAX(SCA_HIS dia+1 dentro de rango); tag N6
19. **0B3b** — UPDATE hoy: nocturno + salida=NULL aún (no encontró en 0B3) + tag N4 → salida=entrada+tothoras; tag N5; INSERT en SCA_HIS
20. **0B3d** — UPDATE hoy: vespertino (17:00-22:00) + entrada>horario+2h + marca temprana en SCA_HIS → entrada=marca_temprana; tag N7
21. **0B4** — UPDATE dia_siguiente: limpia entrada<08:00 si fue movida por 0B3; tag N1
22. **0B5** — UPDATE dia_siguiente: si entrada=NULL + salida>=15:00 después de 0B4 → entrada=salida, salida=NULL; tag N4
23. **0C** — UPDATE: entrada=NULL + salida>=20:00 → entrada=salida, salida=salida_fijada; tag N2; INSERT en SCA_HIS
24. **0D** — UPDATE: nocturno + entrada<12:00 + salida=NULL + nummarcaciones<=1 → salida=entrada, entrada=entrada_fijada; tag N3; INSERT en SCA_HIS

### FASE 1 (completar E/S)
25. **1** — UPDATE: entrada=NULL, salida IS NOT NULL → entrada=entrada_fijada; tag E1; INSERT SCA_HIS
26. **1B** — UPDATE: entrada > 15 min antes del horario → ajusta a entrada_fijada-15min; calcula horaantesentrada, horaextantes; tag E2; excluye 3er turno >2h anticipado
27. **1B-HIS** — UPDATE SCA_HIS: actualiza hora de marca anticipada original al nuevo valor ajustado
28. **1C-NOC** — UPDATE: nocturno + entrada=salida + entrada<12:00 → entrada=entrada_fijada; tag E4; INSERT SCA_HIS
29. **1C** — UPDATE: entrada=salida (duplicada no-nocturna) → salida=salida_fijada; tag E3; INSERT SCA_HIS

### FASE 2 (completar refrigerio)
30. **2** — UPDATE: salida=NULL, entrada IS NOT NULL → salida=salida_fijada; tag S1; INSERT SCA_HIS
31. **2A** — UPDATE: inirefri/finrefri con duración <50% del teórico + hay marcas alternativas → limpia IR/FR; tag R6
32. **2B-PRE** — LOOP: inirefri/finrefri=NULL → busca marcas intermedias en SCA_HIS cerca del horiniref/horfinref (ventana ±2h, comparación minutos del día); asigna R4/R5 si encuentra; excluye near-dups (<5min)
33. **2B** — UPDATE: inirefri/finrefri=NULL aún → pone teórico (horiniref/horfinref, base 01/01/1900); tag R1; INSERT ambas en SCA_HIS
34. **3A** — UPDATE: inirefri=NULL, finrefri IS NOT NULL → inirefri=finrefri-totref (o horfinref-horiniref); tag R2; INSERT SCA_HIS
35. **3B** — UPDATE: finrefri=NULL, inirefri IS NOT NULL → finrefri=inirefri+totref (o horfinref-horiniref); tag R3; INSERT SCA_HIS
36. **3C-NOC** — LOOP: nocturno (>=22:00) sin entrada anticipada (<2h) + tiene refri → DELETE marcas IR/FR de SCA_HIS; limpia IR/FR; nummarcaciones=2; tag RN
37. **3D** — LOOP: salida<inirefri (imposible, comparación HH24MI) excluye nocturnos y N6 → Fase A: busca marca oculta (ind_anulado='S') → inserta visible SSR; Fase B: usa salida_fijada SS; tag SSR/SS
38. **3E** — DELETE SCA_HIS marcas DEPURACION de IR/FR; UPDATE: inirefri<entrada (imposible) → limpia IR/FR; tag RI

### FASE 4
39. **4B** — UPDATE: 4 marcas en <60 min → corrige con teórico completo (IR+FR+S del horario); tag A1; INSERT 3 marcas en SCA_HIS

### FASE 5 (cálculos)
40. **5G** — LOOP: 3er turno (>=22:00) + entrada >2h anticipada → busca marca intermedia; si la encuentra: asigna IR + calcula FR=IR+30min; si no: calcula horas directamente; tag R4|R3 o RC
41. **5** — UPDATE: todos los modificados por PASOs 0-4B (codaux4 IS NOT NULL, no termina en RC) → recalcula tothoramarcas, horarefrigerio, horaefectiva, horatardanza, tothoranocturna; tag RC. horaefectiva=LEAST(brutas, tothoras). Nocturno: si salida<entrada → +1 día.
42. **5A** — UPDATE: tothoranocturna_of con redondeo (ajuste_tothoranocturna, redondeo_tothoranocturna)
43. **5B-TAG** — UPDATE: salida>salida_fijada, diferencia <1h, tiene horaextra → marca con HE (para que 5B lo limpie)
44. **5B** — UPDATE: recalcula horaextra y totalhorasextras (solo >=1h, truncado a horas); ajuste nocturno salida_fijada cuando salida_fijada<entrada_fijada. Solo registros con codaux4 IS NOT NULL.
45. **5B-2** — UPDATE: horadespuessalida, horaextraofi, totalhorasextrasofi, horaextra_ajus, alerta06='EN'/'EE'
46. **5B-3** — UPDATE: alerta06='EE' si horaextra_ajus >= min_min_raz_hextra
47. **5B-4** — UPDATE: horaextra1/2/3 (H25/H35/H50) y horaexofi1/2/3 basados en totalhorasextras/horaextra_ajus y rangos H25F/H35I/H35F/HNI
48. **5C** — UPDATE: nummarcaciones = campos poblados (E+IR+FR+S)
49. **5D** — UPDATE: limpia alerta01='MI' si ahora hay 4 campos completos o es turno nocturno (>=20:00)
50. **5E** — UPDATE: limpia horas_no_trabajadas y alerta03='HI' si tiempo_neto >= tothoras
51. **5F** — DELETE SCA_HIS: marcas no-DEPURACION que no coinciden con E/IR/FR/S del tareo Y están <3 min de otra marca (near-dup de lector)

### FASE 7 (descanso)
52. **7A** — UPDATE: DESCANSO + entrada IS NOT NULL → entrada_fijada=MIN(horing del horario no-descanso); ajusta entrada a -15min; calcula horaantesentrada; tag E2
53. **7** — UPDATE: DESCANSO + entrada IS NOT NULL + salida IS NOT NULL → recalcula tothoramarcas, horadobles (= tothoramarcas, TODAS son dobles), nummarcaciones; horaefectiva=NULL; horatardanza=0; tag DC

### FASE 8 (sincronización final)
54. **8-PRE** — LOOP: tareo tiene más campos que marcas en SCA_HIS → INSERT marcas faltantes (solo si TRUNC(salida)=fechamar para salida); tag MF
55. **8** — UPDATE: nummarcaciones + alerta01 desde COUNT(SCA_HIS); excluye: 4 campos completos (5C ya calculó), tag RN, tag RI, tag PH, horarios SIN refri (los maneja 8B)
56. **8B** — UPDATE: horarios sin refrigerio (horiniref='00:00') → nummarcaciones desde campos tareo; alerta01 por paridad

**→ COMMIT**

---

## PROCEDURES ADICIONALES

### ROLLBACK_MARCACIONES
Revierte cambios de DEPURA_TOTAL. 4 fases:
1. **FASE 1 — Nocturnos**: R-N5 (salida=NULL), R-N4 (salida=entrada, entrada=NULL), R-N1a/a2/b/c (restaura movimientos entre días), R-SSR (salida=NULL), R-N6 (salida=NULL)
2. **FASE 2 — Teóricos**: compara campo vs _fijada → si coincide → NULL; limpia codaux4/5
3. **FASE 3 — Limpiar codaux**: NULL para todos con codaux4 IS NOT NULL (incluye DC, RC, N1 sin _fijada); hoy y día siguiente
4. **FASE 4 — Historial**: DELETE SCA_HIS WHERE motivo LIKE 'DEPURACION%'
5. **FASE 5 — Resync**: nummarcaciones desde SCA_HIS para hoy, ayer (si hubo N1), mañana (si hubo N1)

**Bug conocido en ROLLBACK N2 (PASO 0C)**: N2 no está en FASE 1. FASE 2 nullea salida (=salida_fijada) pero no nullea entrada (=old_salida). Después de ROLLBACK, entrada queda con valor erróneo (debería estar NULL). No se restaura el estado original (entrada=NULL, salida=marca_nocturna).

**Limitación N1 en SCA_HIS**: 0B-HIS actualizó fec_equiv de la marca madrugada a día anterior con motivo DEPURACION. Ese registro NO está en v_fecha_proceso → FASE 4 no lo borra. La marca queda permanentemente en fec_equiv=ayer. Sin embargo al re-ejecutar DEPURA_TOTAL la marca ya está donde debe estar (ayer), por lo que N1 no se re-aplica.

### VER_ESTADO
Consulta de diagnóstico para un empleado/fecha. Retorna:
- Horario teórico (SCA_HORARIO_DET via ProcessDay())
- Marcaciones actuales + tipo (AUTO vs REAL)
- Horas calculadas: brutas, refri, efectivas, tardanza, nocturnas, dobles
- Horas extras: antes/después, desglose por tramo, configuración rangos
- Permisos/ausencias (flags)
- codaux4/5 (audit depuración), alerta01
- Conteo de marcas en SCA_HISTORIAL

### DEPURA_RANGO
Loop por fecha (fecha_ini a fecha_fin), llama DEPURA_TOTAL por cada día. COMMIT independiente por día. Normaliza '%' → NULL antes de llamar DEPURA_TOTAL. Retorna acumulados por columna.

### CONSULTAR_RANGO
SELECT diagnóstico sin modificar datos. Retorna por empleado/fecha:
- caso_aplica: PASO específico que aplicaría (PHANTOM/0A-0D/1-5G/4B/7/8)
- problema: diagnóstico general (DESCANSO FANTASMA, FALTA SALIDA, OK, etc.)
- Todos los campos de horas, marcaciones, permisos, alertas, codaux4/5
- JOIN con PLA_PERSONAL, PLA_TIPO_PLANILLA, SCA_HORARIO_CAB/DET

### BUSCAR_EMPLEADO
SELECT empleados activos por empresa y nombre parcial. GROUP BY para eliminar duplicados de fechas.

---

## BUGS CONOCIDOS / LIMITACIONES

### Documentados en bd_horarios_aquarius.md
- PASO 0-RESTORE reinserción near-dup finrefri (23/04/2026) — FIXED
- PASO 0-ORF/RESTORE-B epoch mismatch 1900 vs 2026 (23/04/2026) — FIXED
- PASO 2B-PRE epoch fix + near-dup exclusión (21/04/2026) — FIXED
- PASO 3D condición fecha vs hora (20/04/2026) — FIXED
- PASO 2B-PRE ventana truncada (20/04/2026) — FIXED
- PASO 8 sobreescribe nummarcaciones correcto → 3 exclusiones (20/04/2026) — FIXED
- horiniref=00:00 causa R1 fantasma (15/04/2026) — FIXED
- PASO 0-DUP/ORF eliminan marcas legítimas de refri (15/04/2026) — FIXED
- PASO 1B-HIS duplicado entrada en SCA_HIS (15/04/2026) — FIXED
- PASO 8-PRE marca faltante (14/04/2026) — FIXED
- PASO 3D condición imposible con ROLLBACK SSR (14/04/2026) — FIXED
- horaextra1/2/3 breakdown (10/04/2026) — FIXED

### Detectados durante análisis completo del BODY
1. **ROLLBACK para N2 (PASO 0C) incompleto**: FASE 2 nullea salida=salida_fijada correctamente, pero entrada (=old_salida_nocturna) no es nulleada porque no coincide con entrada_fijada. Resultado: entrada queda poblada erróneamente. Corrección: agregar N2 a FASE 1 del ROLLBACK (reverso: salida=entrada, entrada=NULL, similar a N4).

2. **PASO 5F threshold 3min podría ser muy agresivo**: DELETE marcas no-E/IR/FR/S dentro de 3 minutos de otra. Si un empleado tiene 2 marcas válidas a 2 min de distancia (lectora lenta), la segunda podría eliminarse. El threshold de DUP es 5 min pero 5F usa 3 min. Hay inconsistencia.

3. **PASO 4B no INSERT inirefri/finrefri en SCA_HIS correctamente cuando ya son NULL**: La condición `AND (t.inirefri IS NOT NULL OR t.finrefri IS NOT NULL)` en el WHERE del PASO 4B implica que antes de actualizar, IR/FR ya tenían valor. Sin embargo las INSERTs posteriores usan los valores DESPUÉS del UPDATE (que ya son horiniref/horfinref). Los NOT EXISTS verifican hora correcta. OK.

4. **PASO 3C-NOC solo excluye entrada < 2h**: La condición `TO_NUMBER(TO_CHAR(t.entrada, 'HH24')) >= 20 OR TO_NUMBER(TO_CHAR(t.entrada, 'HH24')) < 2` cubre entrada entre 20:00-01:59. Si el empleado entra a las 02:00-19:59 en un turno nocturno, 3C-NOC no dispararía. Esto es intencional (esas serían entradas muy anticipadas = PASO 5G debería manejarlas).

5. **PASO 5 excluye registros con codaux4 que ya terminan en RC** (`AND t.codaux4 NOT LIKE '%' || c_RC`): Si un PASO posterior a 5 modifica el tareo y agrega un tag, PASO 5 no re-calcularía. Pero PASO 7 (descanso) y PASO 7A calculan sus propias horas, por lo que OK.

6. **PASO 8-PRE inserta inirefri/finrefri con fecha real en SCA_HIS** pero inirefri puede tener base 01/01/1900 (si fue asignado por PASO 2B). El INSERT usa `TO_CHAR(rec_mf.inirefri, 'HH24:MI:SS')` para hora, que funciona correctamente. El fec_equiv se pone como fechamar (correcto).

---

## PATRONES DE DISEÑO DEL PKG

### Separación lectura/escritura
- SCA_HISTORIAL: solo INSERT (marcas nuevas con motivo 'DEPURACION%') o UPDATE fec_equiv/motivo
- SCA_ASISTENCIA_TAREO: UPDATE de campos calculados y codaux4/5

### Idempotencia
- NOT EXISTS en todos los INSERT a SCA_HISTORIAL
- PASO 0-CLEAN al inicio elimina DEPURACION% de runs anteriores
- codaux4 LIKE '%' || c_XX || '%' previene re-aplicar mismo PASO
- DEPURA_RANGO normaliza '%' para filtros

### Trazabilidad
- codaux4 = cadena de códigos (e.g. "N1|E1|R4|R3|RC")
- codaux5 = descripción humana (max 50 chars, se trunca)
- DBMS_OUTPUT en cada PASO para logging

### Convención de fechas en INSERT SCA_HISTORIAL
- FECHA = TO_CHAR(fechamar, 'DD/MM/YYYY') — SIEMPRE usar fechamar, NO el campo tiempo
- FEC_EQUIV = fechamar (DATE real)
- HORA = TO_CHAR(campo, 'HH24:MI:SS') — funciona tanto con base 1900 como fecha real

### Manejo del epoch 01/01/1900
- Comparaciones entre hora-tiempo (base 1900) y hora-real (fecha 2026): SIEMPRE usar TO_CHAR o extraer minutos del día
- ABS(fecha_2026 - fecha_1900) ≈ 46000 días → nunca usar comparación directa de DATE para umbrales en horas/minutos
- Pattern correcto: `TO_NUMBER(TO_CHAR(campo,'HH24'))*60 + TO_NUMBER(TO_CHAR(campo,'MI'))`
# PKG_SCA_DEPURA_TAREO — Análisis Completo del BODY (23/04/2026)

## ESTRATEGIA GENERAL
- SCA_HISTORIAL = datos crudos. Solo INSERT/UPDATE fec_equiv/motivo. NUNCA modificar hora original.
- SCA_ASISTENCIA_TAREO = área de trabajo. Lee + escribe.
- Todas las marcas generadas tienen motivo LIKE 'DEPURACION%' → fácil limpiar con ROLLBACK.
- codaux4 / codaux5 = audit trail (códigos | descripciones, max 50 chars).
- Solo procesa empleados con >= 1 marcación real. 0 marcas = proceso futuro.
- COMMIT por día (DEPURA_RANGO llama DEPURA_TOTAL por cada día).

## CONSTANTES DE CÓDIGO (codaux4)
| Código | PASO | Descripción |
|---|---|---|
| N1 | 0A/0B/0B3/0B4 | Nocturno: marca movida entre días |
| N2 | 0C | Salida nocturna reubicada como entrada |
| N3 | 0D | Entrada mañana movida a salida, entrada=teórico |
| N4 | 0B2/0B5/0-SWAP | Vespertino/SWAP: salida→entrada |
| N5 | 0B3b | Salida nocturna teórica (calculada) |
| N6 | 0B3c | Salida extendida al día siguiente (sobretiempo) |
| N7 | 0B3d | Entrada vespertina corregida a marca temprana |
| NC | 0-CLEAN | Marca duplicada de madrugada limpiada |
| E1 | 1 | Entrada completada con teórico |
| E2 | 1B/7A | Entrada anticipada ajustada (-15min) |
| E3 | 1C | Entrada=Salida duplicada → corregida salida |
| E4 | 1C-NOC | Duplicada nocturna → entrada=teórico nocturno |
| S1 | 2 | Salida completada con teórico |
| R1 | 2B | IniRefri+FinRefri teórico (base 01/01/1900) |
| R2 | 3A | IniRefri calculado (FinRefri - totref) |
| R3 | 3B | FinRefri calculado (IniRefri + totref) |
| R4 | 2B-PRE/5G | IniRefri real encontrado en SCA_HIS |
| R5 | 2B-PRE | FinRefri real encontrado en SCA_HIS |
| R6 | 2A | Refrigerio anómalo limpiado (<50% del teórico) |
| A1 | 4B | Marcaciones anómalas (<1h) → teórico completo |
| RC | 5/5G(else) | Horas recalculadas |
| DC | 7 | Descanso con marcaciones: dobles calculadas (dispara con t.descanso='S' O SCA_HORARIO_DET.descanso='S' del DIAID) |
| HE | 5B-TAG | Hora extra < 1h detectada |
| MF | 8-PRE | Marca faltante insertada en SCA_HIS |
| RN | 3C-NOC | Nocturno sin anticipación: refrigerio limpiado |
| NC | 0-CLEAN | Marca duplicada madrugada de turno nocturno ayer |
| SS | 3D(B) | Salida imposible → salida_fijada (teórico) |
| SSR | 3D(A) | Salida imposible → salida real oculta restaurada |
| RI | 3E | Refrigerio imposible (inirefri < entrada) |
| PH | 6-PHANTOM/B/C/D | Descanso con marcas fantasma → limpiado |

---

## ORDEN DE EJECUCIÓN COMPLETO (DEPURA_TOTAL)

### FASE 0-LIMPIEZA (en SCA_HISTORIAL)
1. **0-CLEAN** — DELETE marcas DEPURACION% (de runs anteriores)
2. **0-DUP** — DELETE near-duplicates (<5 min), protege E/IR/FR/S del tareo
3. **0-ORF** — LOOP DELETE marca huérfana intermedia (>90 min del refri, comparación minutos del día)
4. **0-RESTORE** — LOOP INSERT restores E/IR/FR/S si faltan en SCA_HIS (threshold >= 5/1440)
5. **0-RESTORE-B** — LOOP DELETE marca de ronda si total_marcas=IMPAR (>90 min del refri)
6. **0-PRE** — UPDATE SCA_ASISTENCIA_TAREO: nummarcaciones y alerta01 sincronizados con SCA_HIS; excluye días DESCANSO con tag PH

### FASE 0-PHANTOM (siempre antes de cualquier PASO de marcaciones)
7. **6-PHANTOM** — UPDATE: DESCANSO + 0 marcas SCA_HIS → limpia E/S/IR/FR/horas; tag PH
8. **6-PHANTOM-B** — UPDATE: DESCANSO + entrada=NULL + salida_madrugada + dia_ant_nocturno → tag PH
9. **6-PHANTOM-C** — UPDATE: DESCANSO + entrada NOT NULL + entrada NO en SCA_HIS + dia_ant_nocturno → tag PH
10. **6-PHANTOM-D** — UPDATE: DESCANSO + entrada<08:00 + dia_ant_nocturno → tag PH

### FASE 0-NOCTURNOS (ajustes entre días)
11. **0-CLEAN** *(real name in code)* — LOOP: entrada=salida de madrugada + ayer nocturno → mueve marca a ayer o limpia; tag NC
12. **0-SWAP** — LOOP: entrada<08:00 + salida>=18:00 + entrada=ayer.salida → intercambia campos; tag N4
13. **0A** — UPDATE dia_anterior: salida = hoy.entrada_madrugada; tag N1
14. **0B** — UPDATE hoy: entrada = NULL (fue movida a ayer); tag N1
15. **0B-HIS** — UPDATE SCA_HIS: fec_equiv de marca madrugada → dia_anterior; motivo DEPURACION N1
16. **0B2** — UPDATE hoy: si entrada=NULL + salida>=15:00 después de N1 → entrada=salida, salida=NULL; tag N4
17. **0B3** — UPDATE hoy: entrada_nocturna>=18:00 + salida=NULL + sig.entrada<08:00 → salida=sig.entrada; tag N1
18. **0B3c** — UPDATE hoy: nocturno + salida IS NOT NULL + salida<entrada (inválida) → salida=MAX(SCA_HIS dia+1 dentro de rango); tag N6
19. **0B3b** — UPDATE hoy: nocturno + salida=NULL aún (no encontró en 0B3) + tag N4 → salida=entrada+tothoras; tag N5; INSERT en SCA_HIS
20. **0B3d** — UPDATE hoy: vespertino (17:00-22:00) + entrada>horario+2h + marca temprana en SCA_HIS → entrada=marca_temprana; tag N7
21. **0B4** — UPDATE dia_siguiente: limpia entrada<08:00 si fue movida por 0B3; tag N1
22. **0B5** — UPDATE dia_siguiente: si entrada=NULL + salida>=15:00 después de 0B4 → entrada=salida, salida=NULL; tag N4
23. **0C** — UPDATE: entrada=NULL + salida>=20:00 → entrada=salida, salida=salida_fijada; tag N2; INSERT en SCA_HIS
24. **0D** — UPDATE: nocturno + entrada<12:00 + salida=NULL + nummarcaciones<=1 → salida=entrada, entrada=entrada_fijada; tag N3; INSERT en SCA_HIS

### FASE 1 (completar E/S)
25. **1** — UPDATE: entrada=NULL, salida IS NOT NULL → entrada=entrada_fijada; tag E1; INSERT SCA_HIS
26. **1B** — UPDATE: entrada > 15 min antes del horario → ajusta a entrada_fijada-15min; calcula horaantesentrada, horaextantes; tag E2; excluye 3er turno >2h anticipado
27. **1B-HIS** — UPDATE SCA_HIS: actualiza hora de marca anticipada original al nuevo valor ajustado
28. **1C-NOC** — UPDATE: nocturno + entrada=salida + entrada<12:00 → entrada=entrada_fijada; tag E4; INSERT SCA_HIS
29. **1C** — UPDATE: entrada=salida (duplicada no-nocturna) → salida=salida_fijada; tag E3; INSERT SCA_HIS

### FASE 2 (completar refrigerio)
30. **2** — UPDATE: salida=NULL, entrada IS NOT NULL → salida=salida_fijada; tag S1; INSERT SCA_HIS
31. **2A** — UPDATE: inirefri/finrefri con duración <50% del teórico + hay marcas alternativas → limpia IR/FR; tag R6
32. **2B-PRE** — LOOP: inirefri/finrefri=NULL → busca marcas intermedias en SCA_HIS cerca del horiniref/horfinref (ventana ±2h, comparación minutos del día); asigna R4/R5 si encuentra; excluye near-dups (<5min)
33. **2B** — UPDATE: inirefri/finrefri=NULL aún → pone teórico (horiniref/horfinref, base 01/01/1900); tag R1; INSERT ambas en SCA_HIS
34. **3A** — UPDATE: inirefri=NULL, finrefri IS NOT NULL → inirefri=finrefri-totref (o horfinref-horiniref); tag R2; INSERT SCA_HIS
35. **3B** — UPDATE: finrefri=NULL, inirefri IS NOT NULL → finrefri=inirefri+totref (o horfinref-horiniref); tag R3; INSERT SCA_HIS
36. **3C-NOC** — LOOP: nocturno (>=22:00) sin entrada anticipada (<2h) + tiene refri → DELETE marcas IR/FR de SCA_HIS; limpia IR/FR; nummarcaciones=2; tag RN
37. **3D** — LOOP: salida<inirefri (imposible, comparación HH24MI) excluye nocturnos y N6 → Fase A: busca marca oculta (ind_anulado='S') → inserta visible SSR; Fase B: usa salida_fijada SS; tag SSR/SS
38. **3E** — DELETE SCA_HIS marcas DEPURACION de IR/FR; UPDATE: inirefri<entrada (imposible) → limpia IR/FR; tag RI

### FASE 4
39. **4B** — UPDATE: 4 marcas en <60 min → corrige con teórico completo (IR+FR+S del horario); tag A1; INSERT 3 marcas en SCA_HIS

### FASE 5 (cálculos)
40. **5G** — LOOP: 3er turno (>=22:00) + entrada >2h anticipada → busca marca intermedia; si la encuentra: asigna IR + calcula FR=IR+30min; si no: calcula horas directamente; tag R4|R3 o RC
41. **5** — UPDATE: todos los modificados por PASOs 0-4B (codaux4 IS NOT NULL, no termina en RC) → recalcula tothoramarcas, horarefrigerio, horaefectiva, horatardanza, tothoranocturna; tag RC. horaefectiva=LEAST(brutas, tothoras). Nocturno: si salida<entrada → +1 día.
42. **5A** — UPDATE: tothoranocturna_of con redondeo (ajuste_tothoranocturna, redondeo_tothoranocturna)
43. **5B-TAG** — UPDATE: salida>salida_fijada, diferencia <1h, tiene horaextra → marca con HE (para que 5B lo limpie)
44. **5B** — UPDATE: recalcula horaextra y totalhorasextras (solo >=1h, truncado a horas); ajuste nocturno salida_fijada cuando salida_fijada<entrada_fijada. Solo registros con codaux4 IS NOT NULL.
45. **5B-2** — UPDATE: horadespuessalida, horaextraofi, totalhorasextrasofi, horaextra_ajus, alerta06='EN'/'EE'
46. **5B-3** — UPDATE: alerta06='EE' si horaextra_ajus >= min_min_raz_hextra
47. **5B-4** — UPDATE: horaextra1/2/3 (H25/H35/H50) y horaexofi1/2/3 basados en totalhorasextras/horaextra_ajus y rangos H25F/H35I/H35F/HNI
48. **5C** — UPDATE: nummarcaciones = campos poblados (E+IR+FR+S)
49. **5D** — UPDATE: limpia alerta01='MI' si ahora hay 4 campos completos o es turno nocturno (>=20:00)
50. **5E** — UPDATE: limpia horas_no_trabajadas y alerta03='HI' si tiempo_neto >= tothoras
51. **5F** — DELETE SCA_HIS: marcas no-DEPURACION que no coinciden con E/IR/FR/S del tareo Y están <3 min de otra marca (near-dup de lector)

### FASE 7 (descanso)
52. **7A** — UPDATE: DESCANSO + entrada IS NOT NULL → entrada_fijada=MIN(horing del horario no-descanso); ajusta entrada a -15min; calcula horaantesentrada; tag E2
53. **7** — UPDATE: DESCANSO + entrada IS NOT NULL + salida IS NOT NULL → recalcula tothoramarcas, horadobles (= tothoramarcas, TODAS son dobles), nummarcaciones; horaefectiva=NULL; horatardanza=0; tag DC

### FASE 8 (sincronización final)
54. **8-PRE** — LOOP: tareo tiene más campos que marcas en SCA_HIS → INSERT marcas faltantes (solo si TRUNC(salida)=fechamar para salida); tag MF
55. **8** — UPDATE: nummarcaciones + alerta01 desde COUNT(SCA_HIS); excluye: 4 campos completos (5C ya calculó), tag RN, tag RI, tag PH, horarios SIN refri (los maneja 8B)
56. **8B** — UPDATE: horarios sin refrigerio (horiniref='00:00') → nummarcaciones desde campos tareo; alerta01 por paridad

**→ COMMIT**

---

## PROCEDURES ADICIONALES

### ROLLBACK_MARCACIONES
Revierte cambios de DEPURA_TOTAL. 4 fases:
1. **FASE 1 — Nocturnos**: R-N5 (salida=NULL), R-N4 (salida=entrada, entrada=NULL), R-N1a/a2/b/c (restaura movimientos entre días), R-SSR (salida=NULL), R-N6 (salida=NULL)
2. **FASE 2 — Teóricos**: compara campo vs _fijada → si coincide → NULL; limpia codaux4/5
3. **FASE 3 — Limpiar codaux**: NULL para todos con codaux4 IS NOT NULL (incluye DC, RC, N1 sin _fijada); hoy y día siguiente
4. **FASE 4 — Historial**: DELETE SCA_HIS WHERE motivo LIKE 'DEPURACION%'
5. **FASE 5 — Resync**: nummarcaciones desde SCA_HIS para hoy, ayer (si hubo N1), mañana (si hubo N1)

**Bug conocido en ROLLBACK N2 (PASO 0C)**: N2 no está en FASE 1. FASE 2 nullea salida (=salida_fijada) pero no nullea entrada (=old_salida). Después de ROLLBACK, entrada queda con valor erróneo (debería estar NULL). No se restaura el estado original (entrada=NULL, salida=marca_nocturna).

**Limitación N1 en SCA_HIS**: 0B-HIS actualizó fec_equiv de la marca madrugada a día anterior con motivo DEPURACION. Ese registro NO está en v_fecha_proceso → FASE 4 no lo borra. La marca queda permanentemente en fec_equiv=ayer. Sin embargo al re-ejecutar DEPURA_TOTAL la marca ya está donde debe estar (ayer), por lo que N1 no se re-aplica.

### VER_ESTADO
Consulta de diagnóstico para un empleado/fecha. Retorna:
- Horario teórico (SCA_HORARIO_DET via ProcessDay())
- Marcaciones actuales + tipo (AUTO vs REAL)
- Horas calculadas: brutas, refri, efectivas, tardanza, nocturnas, dobles
- Horas extras: antes/después, desglose por tramo, configuración rangos
- Permisos/ausencias (flags)
- codaux4/5 (audit depuración), alerta01
- Conteo de marcas en SCA_HISTORIAL

### DEPURA_RANGO
Loop por fecha (fecha_ini a fecha_fin), llama DEPURA_TOTAL por cada día. COMMIT independiente por día. Normaliza '%' → NULL antes de llamar DEPURA_TOTAL. Retorna acumulados por columna.

### CONSULTAR_RANGO
SELECT diagnóstico sin modificar datos. Retorna por empleado/fecha:
- caso_aplica: PASO específico que aplicaría (PHANTOM/0A-0D/1-5G/4B/7/8)
- problema: diagnóstico general (DESCANSO FANTASMA, FALTA SALIDA, OK, etc.)
- Todos los campos de horas, marcaciones, permisos, alertas, codaux4/5
- JOIN con PLA_PERSONAL, PLA_TIPO_PLANILLA, SCA_HORARIO_CAB/DET

### BUSCAR_EMPLEADO
SELECT empleados activos por empresa y nombre parcial. GROUP BY para eliminar duplicados de fechas.

---

## BUGS CONOCIDOS / LIMITACIONES

### Documentados en bd_horarios_aquarius.md
- PASO 0-RESTORE reinserción near-dup finrefri (23/04/2026) — FIXED
- PASO 0-ORF/RESTORE-B epoch mismatch 1900 vs 2026 (23/04/2026) — FIXED
- PASO 2B-PRE epoch fix + near-dup exclusión (21/04/2026) — FIXED
- PASO 3D condición fecha vs hora (20/04/2026) — FIXED
- PASO 2B-PRE ventana truncada (20/04/2026) — FIXED
- PASO 8 sobreescribe nummarcaciones correcto → 3 exclusiones (20/04/2026) — FIXED
- horiniref=00:00 causa R1 fantasma (15/04/2026) — FIXED
- PASO 0-DUP/ORF eliminan marcas legítimas de refri (15/04/2026) — FIXED
- PASO 1B-HIS duplicado entrada en SCA_HIS (15/04/2026) — FIXED
- PASO 8-PRE marca faltante (14/04/2026) — FIXED
- PASO 3D condición imposible con ROLLBACK SSR (14/04/2026) — FIXED
- horaextra1/2/3 breakdown (10/04/2026) — FIXED

### Detectados durante análisis completo del BODY
1. **ROLLBACK para N2 (PASO 0C) incompleto**: FASE 2 nullea salida=salida_fijada correctamente, pero entrada (=old_salida_nocturna) no es nulleada porque no coincide con entrada_fijada. Resultado: entrada queda poblada erróneamente. Corrección: agregar N2 a FASE 1 del ROLLBACK (reverso: salida=entrada, entrada=NULL, similar a N4).

2. **PASO 5F threshold 3min podría ser muy agresivo**: DELETE marcas no-E/IR/FR/S dentro de 3 minutos de otra. Si un empleado tiene 2 marcas válidas a 2 min de distancia (lectora lenta), la segunda podría eliminarse. El threshold de DUP es 5 min pero 5F usa 3 min. Hay inconsistencia.

3. **PASO 4B no INSERT inirefri/finrefri en SCA_HIS correctamente cuando ya son NULL**: La condición `AND (t.inirefri IS NOT NULL OR t.finrefri IS NOT NULL)` en el WHERE del PASO 4B implica que antes de actualizar, IR/FR ya tenían valor. Sin embargo las INSERTs posteriores usan los valores DESPUÉS del UPDATE (que ya son horiniref/horfinref). Los NOT EXISTS verifican hora correcta. OK.

4. **PASO 3C-NOC solo excluye entrada < 2h**: La condición `TO_NUMBER(TO_CHAR(t.entrada, 'HH24')) >= 20 OR TO_NUMBER(TO_CHAR(t.entrada, 'HH24')) < 2` cubre entrada entre 20:00-01:59. Si el empleado entra a las 02:00-19:59 en un turno nocturno, 3C-NOC no dispararía. Esto es intencional (esas serían entradas muy anticipadas = PASO 5G debería manejarlas).

5. **PASO 5 excluye registros con codaux4 que ya terminan en RC** (`AND t.codaux4 NOT LIKE '%' || c_RC`): Si un PASO posterior a 5 modifica el tareo y agrega un tag, PASO 5 no re-calcularía. Pero PASO 7 (descanso) y PASO 7A calculan sus propias horas, por lo que OK.

6. **PASO 8-PRE inserta inirefri/finrefri con fecha real en SCA_HIS** pero inirefri puede tener base 01/01/1900 (si fue asignado por PASO 2B). El INSERT usa `TO_CHAR(rec_mf.inirefri, 'HH24:MI:SS')` para hora, que funciona correctamente. El fec_equiv se pone como fechamar (correcto).

---

## PATRONES DE DISEÑO DEL PKG

### Separación lectura/escritura
- SCA_HISTORIAL: solo INSERT (marcas nuevas con motivo 'DEPURACION%') o UPDATE fec_equiv/motivo
- SCA_ASISTENCIA_TAREO: UPDATE de campos calculados y codaux4/5

### Idempotencia
- NOT EXISTS en todos los INSERT a SCA_HISTORIAL
- PASO 0-CLEAN al inicio elimina DEPURACION% de runs anteriores
- codaux4 LIKE '%' || c_XX || '%' previene re-aplicar mismo PASO
- DEPURA_RANGO normaliza '%' para filtros

### Trazabilidad
- codaux4 = cadena de códigos (e.g. "N1|E1|R4|R3|RC")
- codaux5 = descripción humana (max 50 chars, se trunca)
- DBMS_OUTPUT en cada PASO para logging

### Convención de fechas en INSERT SCA_HISTORIAL
- FECHA = TO_CHAR(fechamar, 'DD/MM/YYYY') — SIEMPRE usar fechamar, NO el campo tiempo
- FEC_EQUIV = fechamar (DATE real)
- HORA = TO_CHAR(campo, 'HH24:MI:SS') — funciona tanto con base 1900 como fecha real

### Manejo del epoch 01/01/1900
- Comparaciones entre hora-tiempo (base 1900) y hora-real (fecha 2026): SIEMPRE usar TO_CHAR o extraer minutos del día
- ABS(fecha_2026 - fecha_1900) ≈ 46000 días → nunca usar comparación directa de DATE para umbrales en horas/minutos
- Pattern correcto: `TO_NUMBER(TO_CHAR(campo,'HH24'))*60 + TO_NUMBER(TO_CHAR(campo,'MI'))`
