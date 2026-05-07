# Proyecto BD_Horarios - Aquarius

## ORDEN DE ANALISIS DE MARCACIONES (OBLIGATORIO)
Al revisar cualquier caso de marcación, SIEMPRE leer en este orden:
1. **Jornada Laboral** (columna izquierda del detalle): horario teórico, refrigerio teórico, T.Horas
2. **Horas Reales** (columna derecha): Ingreso, Inic.Ref., Fin.Ref., Salida reales calculadas
3. **Marcaciones** (tabla inferior): lista de FC/Manual con fecha, hora, tipo
Con esto se identifica: qué debería tener (jornada), qué calculó el sistema (reales), y con qué marcas cuenta (origen).

## ESTANDAR DE RESPUESTA (OBLIGATORIO)
Al final de CUALQUIER cambio realizado o consulta de análisis, SIEMPRE incluir:
- **Resultado esperado:** qué campos/valores deberían quedar y si es correcto o no
- **Resultado actual (antes del fix):** qué tenía mal
- **HORARIO FINAL QUE SE REGISTRARÁ:** tabla con los 4 campos (Entrada, IniRefri, FinRefri, Salida) + horas calculadas (T.Horas, H.Efect, H.Extra, Refrig, Tardanza)
- Formato de tabla cuando hay múltiples días/campos

## Entorno técnico
- Base de datos: Oracle 10g
- Herramienta: Toad version 7.5 2.0 / VS Code + SQLcl MCP
- Workspace: d:\.Net\AQUARIUS (nuevo), d:\.Net\BD_Horarios (antiguo)
- Conexión activa: AQUARIUS/AQUARIUS@10.0.7.11:1521/ORCL (nombre BD=ORCL)

## Estructura del sistema
- Sistema de control de asistencia, marcaciones, horarios, compensaciones
- Multi-empresa: 0001 (ARBONA), 0002 (SOLSA), 0003 (SIG)
- Tablas principales: SCA_HISTORIAL, SCA_ASISTENCIA_TAREO, SCA_HORARIO_*, PLA_PERSONAL

## Reglas de negocio - Marcaciones
- Obreros requieren 4 marcaciones: ingreso, salida refrigerio, regreso refrigerio, salida
- Horarios rotativos: consultar SCA_HORARIO_PERSONAL con MAX(fec_vigencia)
- alerta01='MI' indica marcación impar
- tipmar: 1=automática, 2=permiso, 3=manual

## Preferencias desarrollo
- Crear nuevos procedures, no modificar existentes
- Separar por casos para facilitar pruebas
- Incluir en leyendas: tablas ESCRITURA vs CONSULTA


## Casos de negocio identificados
- Caso 2: Marcación incompleta de refrigerio
  - Si falta FIN_REF: calcular = INI_REF_real + tiempo_refrigerio
  - Si falta INI_REF: calcular = FIN_REF_real - tiempo_refrigerio
  - Tiempo refrigerio viene de SCA_HORARIO_DET.totref
- Caso 8 (PASO 7): Descanso con marcaciones reales
  - descanso='S' pero empleado trabajó (tiene entrada/salida)
  - Tareo original calcula mal tothoramarcas (deduce TT_SCA_TRAMOS2)
  - Fix: recalcular tothoramarcas, horaefectiva, horadobles
  - Nocturno: si salida < entrada, sumar 1 día al cálculo
  - En descanso TODAS las horas son dobles (horadobles = tothoramarcas)
  - Código: DC = Descanso con marcaciones corregidas

## Estado actual PKG_SCA_Depura_Tareo (20/04/2026)
- 4 procedures: DEPURA_TOTAL, DEPURA_RANGO, ROLLBACK_MARCACIONES, VER_ESTADO
- Solo procesa empleados con >= 1 marcación (0 marcas = proceso futuro)
- PASO 4 (día completo forzado) REMOVIDO → extraído a PASO4_Completar_DiaCompleto.sql
- DEPURA_RANGO: rango de fechas, COMMIT por día, normaliza '%' → NULL
- Archivo PRODUCCION es el definitivo (PRUEBAS y raíz son versiones antiguas)
- PASOs nuevos: 0-RESTORE, 0-RESTORE-B, 6-PHANTOM, 6-PHANTOM-B, 6-PHANTOM-C, 1B-HIS
- PASO 6-PHANTOM (3 variantes, todas ejecutan al inicio antes de cualquier otro PASO):
  - PHANTOM  : DESCANSO + SCA_HIS tiene 0 marcas reales → limpia E/S/IR/FR
  - PHANTOM-B: DESCANSO + entrada=NULL + salida fantasma madrugada + dia ant nocturno
  - PHANTOM-C: DESCANSO + entrada NOT NULL + entrada NO existe en SCA_HIS + dia ant nocturno
    Ejemplo: Dom 19/04, tareo E=22:45 (fantasma Sab), S=07:01; SCA_HIS tiene 00:11,02:10,04:09,07:01
  - Código: PH = Phantom. PASO 8/8B excluyen dias con tag PH

## Regla nocturno: threshold para deteccion
- PASO 0A/0B: threshold bajado de >= 2000 a >= 1800
- Motivo: HORARIO 19-03 arranca 19:00, empleados llegan 18:55
- 1855 < 2000 pero >= 1800, ahora se detecta
- PASO 0C/0D mantienen >= 2000 (casos distintos)
- N4 (PASO 0B2): salida vespertina huerfana post-N1 → mover a entrada

## Horario nocturno / 3er turno - REGLAS ESPECIALES
- Horario teórico: 23:00 a 07:00 (TERCER TURNO)
- PERO empleados pueden entrar desde las 19:00 (7PM) con sobretiempo
- Entrada anticipada de 4+ horas es VÁLIDA = genera horas extras
- Obreros SIEMPRE tienen 30 min de refrigerio (incluso en nocturno)
- **REGLA NUEVA 24/04/2026**: Nocturno con entrada anticipada SIEMPRE tiene refrigerio,
  AUNQUE el horario teorico tenga horiniref=00:00 (ej: VIGILANCIA). Se detecta por marcas reales:
    * PASO 5G ELSE (horiniref=00:00) busca 1 o 2 marcas en ventana entrada+30min..entrada_fijada
    * 1 marca: IR=marca, FR=marca+30min (inserta DEPURACION)
    * 2 marcas: IR=marca1, FR=marca2 (par real, sin insertar)
- Marcaciones esperadas en entrada muy anticipada:
  * ~19:00 = ENTRADA real (anticipada)
  * ~21:00-22:00 = Posible marca de refrigerio
  * ~07:00 = SALIDA real
- NO eliminar marcas tempranas - son entrada anticipada con sobretiempo
- El sobretiempo se calcula: entrada_fijada(23:00) - entrada_real(~19:00)

## Bug fix: PASO 3D condición fecha vs hora (20/04/2026)
- inirefri asignado por PASO 2B (teorico) usa base 01/01/1900 → salida (2026) < inirefri (1900) = FALSE
- PASO 3D nunca disparaba cuando refri era teorico; solo disparaba con refri de PASO 2B-PRE (fecha real)
- Fix condición: TO_CHAR(salida,'HH24MI') < TO_CHAR(inirefri,'HH24MI') + excluir entrada_fijada >= 20:00
- Nocturno excluido porque salida(07:00) < inirefri(23:00) en hora es correcto cronológicamente

## Bug fix: PASO 2B-PRE ventana truncada (20/04/2026)
- Ventana búsqueda = entrada+30min a salida-30min. Si salida=12:17 → límite=11:47
- Manuales (12:30, 13:00) después de salida=12:17 quedan fuera del rango → no encontrados
- PASO 2B asigna teórico (1900-base) en lugar de reales → PASO 3D no disparaba
- Fix: si TO_CHAR(salida) < TO_CHAR(horiniref) → extender límite a salida_fijada-30min
- Resultado: 2B-PRE encuentra reales → 2B no aplica → 3D corrige salida con comparación hora

## Importante: horiniref/inirefri tipo DATE
- Campos "tiempo" (horiniref, horfinref, tothoras, horaantesentrada, etc.) = base 01/01/1900
- Campos "marca real" (entrada, salida, inirefri cuando viene de SCA_HIS) = fecha real (fechamar)
- Siempre usar TO_CHAR(campo,'HH24:MI:SS') para comparar horas entre ambos tipos
- Para INSERT en SCA_HISTORIAL usar TO_CHAR(fechamar,'DD/MM/YYYY') como fecha, no el campo tiempo

## CASO 14: Refrigerio imposible (inirefri < entrada) - PASO 3E (20/04/2026)
- Empleado llega tarde sin FC de entrada; manuales cargados ANTES de que llegara
- Aquarius asigna primera FC como entrada y manuales anteriores como inirefri/finrefri
- Resultado: inirefri(12:30) < entrada(13:06) = cronológicamente imposible
- Fix: limpiar inirefri/finrefri → sin refri, tardanza real queda intacta
- Código: RI = Refrigerio Imposible
- Excluir turnos nocturnos (entrada_fijada >= 20:00)
- Ejemplo: ARREDONDO 032933, 16/04/2026

## Bug fix: PASO 8 sobreescribe nummarcaciones correcto (20/04/2026)
PASO 8 cuenta desde SCA_HIS y puede sobreescribir valores correctos ya calculados.
Tres exclusiones en PASO 8 WHERE para evitarlo:

1. **E+IR+FR+S completos (13/04, 14/04)**: Tareo tiene 4 campos poblados; SCA_HIS tiene marks extra
   (checkpoints 20:11, 20:28). PASO 5C pone nummarcaciones=4, PASO 8 lo sobreescribe a 6/8.
   Fix: `AND NOT (E IS NOT NULL AND IR IS NOT NULL AND FR IS NOT NULL AND S IS NOT NULL)`

2. **Tag RN - nocturno sin refri (17/04 CHOCCARE)**: PASO 2B asigna IR/FR teórico (23:00/23:30)
   e inserta DEPURACION marks en SCA_HIS. PASO 3C-NOC limpia IR=NULL,FR=NULL,nummar=2.
   Pero PASO 8 cuenta SCA_HIS incluyendo esas DEPURACION marks → sobreescribe a 4.
   Fix: `AND NOT (t.codaux4 LIKE '%RN%')`

3. **Tag PH - descanso phantom**: SCA_HIS tiene marca fantasma de turno nocturno anterior.
   Fix: `AND NOT (t.descanso = 'S' AND t.codaux4 LIKE '%PH%')`

Regla general: si un PASO anterior ya calculó nummarcaciones correctamente y lo indicó con un tag
en codaux4, PASO 8 NO debe interferir.

## Bug fix: Marca faltante en SCA_HISTORIAL (14/04/2026) - PASO 8-PRE
- Caso: Tareo tiene 4 campos (E/IR/FR/S) pero SCA_HISTORIAL solo tiene 3 marcas
- Ejemplo: 037470, 06/04/2026: E=06:53, IR=11:28, FR=11:55, S=19:02 pero falta 19:02 en historial
- PASO 8 contaba solo marcas de SCA_HISTORIAL → nummarcaciones=3, alerta01='MI'
- Fix: Nuevo PASO 8-PRE antes de PASO 8 que inserta marcas faltantes en SCA_HISTORIAL
- Código MF = Marca Faltante insertada desde tareo
- NOT EXISTS previene duplicados; v_mf_insertado controla codaux4/5 solo si realmente insertó
- ROLLBACK y PASO 0-CLEAN ya manejan estas marcas (motivo LIKE 'DEPURACION%')

## Bug fix: Salida imposible (salida < inirefri) - PASO 3D (20/04/2026)
- Caso: empleado tiene 2 FC (E=06:47, S=12:17) + 2 Manuales refrigerio (12:30, 13:00)
- PASO 2B asignó IR=12:30, FR=13:00 teórico → S(12:17) < IR(12:30) = IMPOSIBLE
- Causa raíz: PASO 2B-PRE buscaba marks entre salida-30min=11:47; los manuales 12:30/13:00 quedan fuera del rango
- PASO 3D implementado como LOOP con 2 fases por registro:
  - Fase A: buscar marca OCULTA (ind_anulado='S' o ind_noprocesar<>0) después de finrefri,
    dentro de 2h de salida_fijada, más cercana → SSR (Salida real Restaurada)
    Inserta nueva marca visible DEPURACION en SCA_HISTORIAL; oculta original no se toca
  - Fase B: sin marca oculta → salida_fijada teórico → SS (Salida Swap)
- Códigos: SS = teórico, SSR = real oculta restaurada
- ROLLBACK: R-SSR revierte salida=NULL para SSR; motivo 'DEPURACION%' cubre borrado historial
- Ejemplo: CERVANTES 037810, 14/04/2026 → E=06:47, IR=12:30, FR=13:00, S=15:01(SSR) o 15:00(SS)

## Bug fix: PASO 0-RESTORE reinserta near-dup finrefri como DEPURACION (23/04/2026)
- Caso: 5 marcas (08:51, 12:44, 12:45, 13:18, 18:03) → 12:44/12:45 son near-duplicados (1 min)
- PASO 0-DUP elimina 12:45 correctamente
- PERO Aquarius tenía finrefri=12:45 en el tareo → PASO 0-RESTORE lo reinserta como 'DEPURACION: Marca restaurada finrefri 0-REST'
- PASO 5F no puede eliminar marcas DEPURACION (motivo LIKE 'DEPURACION%') → 12:45 DEPURACION queda en SCA_HIS → count=5 (impar) → ciclo infinito entre runs
- Mismo problema cuando Aquarius almacena inirefri=finrefri=01/01/1900 00:00 (Aquarius null): 0-RESTORE inserta marca '00:00' en SCA_HIS
- Fix en PASO 0-RESTORE cursor: agregar `AND (t.finrefri - t.inirefri) >= (5/1440)` 
  - Threshold idéntico a 0-DUP (< 5 min = near-dup). Cubre: 1 min diff y epoch null (0 diff)
  - 0-CLEAN en el próximo run limpia el DEPURACION 12:45 que quedó de runs anteriores
- PASO 2B-PRE fix también en código: `ABS(marca - v_marca_inter) >= (5/1440)` excluye near-dup en búsqueda de segunda marca
- Resultado: IR=12:44, FR=13:18 ✓. SCA_HIS = 4 marcas ✓. PASO 5F limpia 12:45 si es marca FC (no DEPURACION)
- Fotocheck 034572, 21/04/2026

## Bug fix: PASO 0-ORF y 0-RESTORE-B epoch mismatch (23/04/2026)
- PASO 0-ORF usaba alerta01='MI' para decidir eliminar marca, pero alerta01 está DESACTUALIZADO después de que PASO 0-DUP reduce de 5->4 marcas (par). ORF disparaba igualmente y eliminaba 13:18 (válido).
- Fix: ORF agrega total_marcas real de SCA_HIS al cursor y usa MOD(total_marcas,2)=1 en vez de alerta01='MI'
- PASO 0-ORF y 0-RESTORE-B comparaban ABS(fec_equiv_2026 - horiniref_1900) para verificar "> 90 min de refrigerio". El resultado es ~46,000 días >> 90/1440 → protección NUNCA funcionaba → cualquier intermedia podía eliminarse.
- Fix: Cambiar a comparación de minutos del día: TO_NUMBER(SUBSTR(hora,1,2))*60 + TO_NUMBER(SUBSTR(hora,4,2)) vs TO_CHAR(horiniref,'HH24')*60 + TO_CHAR(horiniref,'MI'). Umbral: > 90 (minutos, no días)
- Mismo patrón de epoch bug que fue corregido en PASO 2B-PRE el 21/04/2026

## Bug fix: horiniref=00:00 causa R1 fantasma y bloquea 5G (15/04/2026)
- Horario VIGILANCIA tiene horiniref=00:00 = NO tiene refrigerio
- Pero horiniref almacena fecha distinta a 01/01/1900 → condición <> TO_DATE('01/01/1900') pasa
- PASO 2B asignaba R1 (IR=00:00, FR=00:00) → insertaba marca 00:00 en SCA_HIS → impar
- PASO 5G no entraba porque IR/FR ya no son NULL (bloqueado por R1)
- Fix PASO 2B: agregar AND TO_CHAR(t.horiniref, 'HH24:MI') <> '00:00'
- Fix PASO 5G: buscar marca solo si horiniref<>00:00; ELSE branch calcula HE anticipadas
- Fix PASO 6-PHANTOM: resetear tothoramarcas/horadobles/etc en DESCANSO limpiado

## Bug fix: Horas extras breakdown (10/04/2026)
- PKG solo actualizaba horaextra y totalhorasextras en PASO 5B
- El reporte lee de horaextra1/2/3 (H25%, H35%, Dob) que quedaban viejos
- Campos config rangos: H25F, H35I, H35F, HNI, AJUSTE_HEXTRA, TIPPAGOHE
- alerta06: EN=extras normal, EE=excede razonabilidad
- Oficiales (horaexofi1/2/3) solo cuando tippagohe='1'

## Bug fix: PASO 0-DUP/ORF eliminan marcas legítimas (15/04/2026)
- PASO 0-DUP eliminaba pares inirefri/finrefri que están <5 min (ej: 01:05→01:06 = 1 min)
- PASO 0-ORF contaba inirefri/finrefri como "intermedias huérfanas" y podía eliminarlas
- Fix DUP: NOT EXISTS para proteger marcas cuya hora coincide con tareo (E/IR/FR/S)
- Fix ORF: excluir inirefri/finrefri del conteo intermedias y del DELETE
- Caso ejemplo: Vigilancia 23-07, fotocheck 034161, 8 marcas con checkpoints de ronda

## REGLA UI .NET: Ocultar marcas no asignadas (PASO 9) - actualizado 24/04/2026 v3
- Aquarius estandar para anular marca: IND_ANULADO='A' (NO 'S', NO ind_noprocesar=1)
- Confirmado en sp_SCA_Delete_Marca: UPDATE SCA_HISTORIAL SET ind_anulado='A', motivo=v_motivo
- Restauracion: ind_anulado=NULL (no 'N')
- PASO 9 usa: ind_anulado='A' + motivo='DEPURACION: Marca no asignada (oculta UI)'
- PASO 0-UNHIDE / ROLLBACK revierten a ind_anulado=NULL, motivo=NULL (limpian tambien legacy 'S' y ind_noprocesar=1)
- Aplica a TODOS los empleados que pase DEPURA_TOTAL
- IMPORTANTE: NO usar 'S' ni IND_NOPROCESAR=1 para ocultar en UI .NET

## Bug fix: PASO 1B-HIS actualiza marca anticipada en SCA_HISTORIAL (15/04/2026)
- PASO 1B ajusta entrada (ej: 14:13→14:45) pero marca vieja quedaba en SCA_HISTORIAL
- PASO 8-PRE insertaba la nueva marca → 2 entradas = impar

## REGLA UI .NET: Detalle de Marcacion solo muestra las 4 validas (24/04/2026)
- La ventana "Detalle de Marcacion" debe mostrar SOLO las marcas asignadas a campos del tareo:
  * Entrada, IniRefri, FinRefri, Salida (max 4 marcas visibles)
- TODAS las demas marcas de SCA_HISTORIAL del dia (rondas lect=004, checkpoints, marcas
  intermedias no usadas, marcas duplicadas, etc.) DEBEN quedar OCULTAS (no visibles para .NET).
- Mecanismo: marcar con ind_anulado='S' y motivo descriptivo ('DEPURACION: Ronda/checkpoint
  no usado', 'DEPURACION: Marca no asignada', etc.). El .NET filtra por ind_anulado<>'S'.
- Aplica al final del paquete depurador, una vez consolidados los 4 campos del tareo.
- Caso ejemplo: CHOCCARE 034161 23/04/2026 - tareo tiene 4 marcas (18:34, 20:06, 20:36, 07:02);
  rondas 00:03, 02:03, 04:01 (lect=004) deben anularse para no aparecer en el detalle.

## Bug fix: PASO 0-ORF y 0-RESTORE-B sin refri teorico (24/04/2026)
- Horarios VIGILANCIA tienen horiniref=00:00 (sin refri teorico). PASO 0-ORF y 0-RESTORE-B
  miden distancia de cada marca contra horiniref/horfinref para decidir cual eliminar como
  "huerfana/ronda lejana". Con horiniref=00:00, la salida a comer (ej: 20:06) queda lejos
  de medianoche -> ORF la elimina por error.
- Fix: ambos PASOs excluir horiniref=00:00 con AND TO_CHAR(t.horiniref,'HH24:MI') <> '00:00'

## Bug fix: PASO 5G horarios sin refri teorico + cap LEAST horaefectiva (24/04/2026)
- Horarios sin refri teorico (horiniref=00:00, ej: VIGILANCIA) con entrada anticipada nocturna
  no asignaban IR/FR aunque hubiera marca real (ej: 20:06).
- Tampoco capeaba horaefectiva a tothoras (8h) -> daba 12:28 brutas como efectivas.
- Fix PASO 5G:
  * Branch IF horiniref<>00:00: busca marca entre 20:00-23:59 (3er turno con sobretiempo)
  * NUEVO Branch ELSE (horiniref=00:00): busca marca UNICA entre entrada+30min y entrada_fijada
    (ventana de anticipacion). Si hay 1 sola, es salida a comer -> IR=marca, FR=marca+30min.
    Comparacion por minutos del dia (no DATE) para evitar epoch mismatch.
  * En el branch ELSE sin marca, horaefectiva = LEAST(brutas, tothoras) en lugar de brutas directo.
- Caso: CHOCCARE 034161 23/04, horario 23:00-07:00 sin refri, entrada 18:34, marca 20:06
  -> IR=20:06, FR=20:36 (DEPURACION), HEfect=08:00, HEx_ant=04:00, HNoc=08:02

## Bug fix: d_N7 buffer overflow ORA-06502 (23/04/2026)
- Constante `d_N7 NVARCHAR2(30) := 'Entrada corregida a marca previa'` = 32 chars
- Asignación en BEGIN constants fallaba silenciosamente → ORA-06502 en TODA invocación de DEPURA_TOTAL
- Fix: shortened a `'Entrada corregida marca previa'` (30 chars)
- Línea 731 PKG_SCA_Depura_Tareo.sql

## Bug fix: PASO 0-PRE excluía marcas 0-REST → MI residual (23/04/2026)
- PASO 0-CLEAN borra marcas DEPURACION% al inicio
- PASO 0-RESTORE re-inserta REALES (E/IR/FR/S del tareo) con motivo
  'DEPURACION: Marca restaurada {campo} 0-REST'
- PASO 0-PRE conta marcas excluyendo 'DEPURACION%' → no contaba las restauradas
- Resultado: tareo con SCA_HIS=4 (3 son 0-REST + 1 real) quedaba nm=1 alerta01='MI'
- Fix: en 6 sub-queries de PASO 0-PRE incluir:
  `(NVL(h.motivo,' ') NOT LIKE 'DEPURACION%' OR NVL(h.motivo,' ') LIKE '%0-REST%')`
- Casos: 034269/25-30/03, 034474/27/03

## NEW PASO 3F: Refrigerio truncado - salida < finrefri (23/04/2026)
- Caso: empleado entra normal pero sale temprano (sabado corto)
- Ejemplo: 034086 28/03 → E=06:52, S=13:24; refri teorico 13:15-14:00
- Run anterior PASO 2B asigna IR=13:15, FR=14:00; pero S(13:24) < FR(14:00) = imposible
- PASO 0-RESTORE perpetua el problema reinsertando IR/FR como '0-REST'
- Fix #1: PASO 2B agrega guard `TO_CHAR(salida) > TO_CHAR(horfinref) OR nocturno`
- Fix #2: NUEVO PASO 3F detecta y limpia casos ya existentes
  - Igual que PASO 3E (RI) pero comparando salida<finrefri en lugar de inirefri<entrada
  - Codigo: RT = Refrigerio Truncado
  - Excluye nocturnos por entrada_fijada >= 20:00
  - PASO 8 excluye tag RT (analogo a RI/RN)
  - DELETE de marcas DEPURACION del IR/FR antes de limpiar tareo
- Fix: PASO 1B-HIS actualiza la hora de la marca vieja tras el ajuste
- Cálculo hora original: entrada_fijada - (horaantesentrada - 01/01/1900)

## Bug fix: Nocturno + Refrigerio + Duplicados (14/04/2026)
### Exclusión nocturna incorrecta
- PASOs 2A,2B-PRE,2B,3A,3B,4B excluían turnos con entrada_fijada >= 20:00
- TERCER TURNO SÍ tiene refrigerio (23:00-23:30 en SCA_HORARIO_DET)
- Fix: removida condición, horiniref IS NOT NULL ya filtra suficiente

### Marcas duplicadas en SCA_HISTORIAL (5 marcas impar)
- NOT EXISTS en INSERT usaba h.fecha = TO_CHAR(campo, 'DD/MM/YYYY')
- entrada_fijada tiene fecha real (09/04/2026) pero horiniref fecha base (01/01/1900)
- Cuando inirefri=entrada=23:00, insertaba 2 marcas 23:00 (fechas distintas)
- Fix: removida condición h.fecha de TODOS los NOT EXISTS (14 instancias)
- fec_equiv + hora + idtarjeta es suficiente para detectar duplicados

## CASO 13: Nocturno sin entrada anticipada NO tiene refrigerio (14/04/2026)
- Horario 23:00-07:00 (8 horas) entrada cerca de las 23:00 → NO tiene refrigerio
- Horario con entrada anticipada (>=2h antes, ej: 19:00) → SÍ tiene refrigerio
- PASO 0-NOC-REF: Limpia inirefri/finrefri + elimina marca duplicada
- Código: RN = Nocturno sin refri: marca dup
- Ejemplo APOLINARIO 032410, 09/04: E=22:54, IR=22:55 (dup), S=07:00 → IR/FR limpiados
- Criterio: entrada >= entrada_fijada - 2 horas = sin anticipación
