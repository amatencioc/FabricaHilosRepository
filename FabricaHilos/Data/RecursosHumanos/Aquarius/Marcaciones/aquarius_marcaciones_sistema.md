# Aquarius - Sistema de Marcaciones: Análisis Completo (23/04/2026 · actualizado 28/04/2026)

## ENTORNO DEL USUARIO
- Oracle 11.2.0.4 (servidor), cliente Toad 7.5 (compatible Oracle 10g)
- Usar siempre TO_DATE('...','dd/MM/yyyy') — NO usar DATE '...' (sintaxis ANSI no soportada en Toad 7.5)
- NO usar SET PAGESIZE / SET LINESIZE desde MCP (comando restringido)

## REGLA CRÍTICA: QUÉ SE PUEDE MODIFICAR
- **SOLO modificar paquetes/SPs propios** del proyecto (ej. `PKG_SCA_COMP_DIA_DIA`, `PKG_SCA_DEPURA_TAREO`).
- **NUNCA modificar** `aquarius.txt` ni ningún SP/función del sistema base de Aquarius (`sp_SCA_Read_*`, `sp_SCA_Insert_*`, `INTERFACE_ASSITIME`, etc.).
- El archivo `aquarius.txt` es solo referencia/documentación del sistema. No editarlo.


## FLUJO GENERAL
```
SCA_HISTORIAL (marcas crudas)
  → TT_V_SCA_MARCAS (temporal de proceso)
  → SCA_ASISTENCIA_TAREO (tareo/resumen por persona por día)
  → Reportes / INTERFACE_ASSITIME (exporta a planillas ARBONA/SOLSA/SIG)
```

---

## TABLAS PRINCIPALES

### SCA_HISTORIAL — Marcaciones individuales crudas
| Campo | Tipo | Descripción |
|---|---|---|
| IDCOD | NUMBER(18) | PK, generado por secuencia `id_cod_seq` |
| IDTARJETA | VARCHAR2(30) | Número de fotocheck |
| FECHA | CHAR(10) | Fecha en texto 'DD/MM/YYYY' |
| HORA | CHAR(8) | Hora en texto 'HH24:MI:SS' |
| FEC_EQUIV | DATE | Fecha equivalente (para turnos que cruzan medianoche) |
| TIPOREG | CHAR(1) | 1=automática(FC), 2=permiso, 3=manual |
| IND_ANULADO | CHAR(1) | 'A' = anulada/ignorada |
| IND_NOPROCESAR | NUMBER(1) | 1 = no procesar |
| OBS_NOPROCESAR | NVARCHAR2(100) | Observación del no-procesar |
| MOTIVO | VARCHAR2(219) | Motivo (usado por PKG_SCA_DEPURA_TAREO para 'DEPURACION%') |
| TIP_PERMISO | CHAR(5) | Grupo del permiso (ej: DMED, VACA, SUBI, etc.) |
| PERID | CHAR(8) | ID del permiso |
| IND_AMAN_HOR_EST | CHAR(1) | 'N'=normal, 'A'=horario amanecida/especial |
| ORDEN | INTEGER | Orden de la marca |
| INDREFRI | CHAR(1) | Indicador de refrigerio |
| IND_CERRADO | CHAR(1) | 'S' = cerrado (no se procesa) |
| ASIID | CHAR(5) | ID de asistencia |
| IDLECTORA | CHAR(5) | ID del reloj/lectora |

### SCA_ASISTENCIA_TAREO — Tareo diario por persona
**Identificación:** FECHAMAR + COD_EMPRESA + COD_PERSONAL
#### Horario teórico (base 01/01/1900)
- `ENTRADA_FIJADA`, `SALIDA_FIJADA` — entrada y salida programadas
- `TOTHORAS` — total horas jornada
- `HORINIREF`, `HORFINREF` — inicio/fin refrigerio teórico
- `TOTREF` — duración refrigerio teórico
- `HORINIREF2`, `HORFINREF2` — segundo refrigerio
- `HORINIHORNOC`, `HORFINHORNOC` — ventana nocturna (desde hora_cierre_diur de PLA_TIPO_PLANILLA)
- `HORSAL_RESREFDES`, `TIEDES_RESREFDES` — horario residual refrigerio descanso
#### Marcaciones reales calculadas
- `ENTRADA`, `SALIDA` — horario real (fecha real = FECHAMAR)
- `INIREFRI`, `FINREFRI` — inicio/fin refrigerio real
- `NUMMARCACIONES` — conteo de marcas válidas del día
#### Horas calculadas (base 01/01/1900)
- `TOTHORAMARCAS` — total horas marcadas (salida-entrada menos refri)
- `HORAEFECTIVA` — horas efectivas (descontando tardanza si aplica)
- `HORATARDANZA` — minutos de tardanza
- `HORAANTESENTRADA` — horas antes de entrada (sobretiempo anticipado)
- `HORAANTESALIDA` — horas de salida antes de hora
- `HORAREFRIGERIO` — tiempo real de refrigerio marcado
- `HORAEXTRA` — horas extras totales
- `HORAEXTRA1/2/3` — extras por tramo (25%, 35%, dobles)
- `TOTALHORASEXTRAS` — total extras
- `HORAEXTRAOFI` — extras para oficiales
- `HORAEXOFI1/2/3` — extras oficiales por tramo
- `HORADOBLES` — horas dobles (ej: descanso trabajado)
- `HORADOBLESOF` — horas dobles para oficiales
- `TOTHORANOCTURNA`, `TOTHORANOCTURNA_OF` — horas nocturnas
- `HORABANCOH` — banco de horas
- `HORAS_RECUP` — horas recuperadas
- `HORAS_FALTA`, `HORAS_NO_TRABAJADAS`, `HORAS_NO_AUT` — faltas
#### Parámetros copiados de reglas/parametros
- `MIN_MAX_RAZ_TARD` — minutos máx tardanza razonable
- `MIN_RAZ_HNORMAL` — minutos mín para h.normal
- `MIN_REFRI` — minutos mín refrigerio
- `MIN_A_PART_HEXTRA` — minutos para partir extras
- `AJUSTE_HEXTRA`, `AJUSTE_TOTHORANOCTURNA`, `REDONDEO_TOTHORANOCTURNA`
- `H25F`, `H35I`, `H35F`, `HNI` — rangos horas extras
- `MINTOLEING`, `MINTOLEREF`, `MINTOLEMES` — tolerancias
- `HAYHEING`, `HAYPAGOHE`, `TIPPAGOHE` — flags extras
- `HAYDCTOTAR`, `TIPDCTOTAR`, `TIPDCTOTARREAL`, `TIPODESCUENTOFALTA`
- `HAYHEA_PORAUT`, `HAYHED_PORAUT`, `HAYHEO_PORAUT` — autorización HE
- `MINHEAUT`, `AUTHD`, `MINHDAUT` — umbrales autorización
- `TIPDISHE` — tipo disposición HE
#### Indicadores / Flags
- `IND_OBRERO` — 'S' = obrero (requiere 4 marcas: E+IR+FR+S)
- `DESCANSO` — 'S' = día de descanso
- `DESCANSOROTATIVO` — 'S' = descanso rotativo
- `FERIADO` — 'F' = feriado
- `ANT_FERIADO` — anterior a feriado
- `IND_FLEXIBLE` — 'S' = horario flexible
- `IND_EMP_ASUM_REFRI` — empleado asume refrigerio
- `ORI_FOTOCHECK` — origen del fotocheck
- `DIA_PROCESO` — día de la semana (DIAID)
- `HORTUR` — turno (T1, T2, T3, etc.)
- `HORID`, `HORCLA` — ID y clasificación horario
- `PER_DIA_COMP` — 'S' = permiso día completo
#### Alertas
- `ALERTA01`: MI=marca impar, FT=falta
- `ALERTA02`: FT=falta total
- `ALERTA04`: TN=tardanza normal, TE=tardanza excesiva
- `ALERTA06`: EN=extras normal, EE=excede razonabilidad
- `ALERTA07`: SN=salida normal antes, SE=salida excesiva
- `ALERTA09`: PE=permiso, PC=permiso compensado
- `ALERTA13`: (usado en casos especiales)
- **Alertas de compensación** (ver `aquarius_compensaciones.md`): TC, SC, HC, FC, EC, DC, PC
#### Permisos/Ausencias
- `PER_SUBSIDIO`, `PER_GOCE`, `PER_SGOCE`, `PER_VACA`, `PER_LIC_SIND`
- `PER_DESC_MED`, `PER_SUSPENSION`, `PER_DIA_COMP`, `PER_GOCE_FIS`
- `PER_LIC_PAT`, `PER_LIC_FAC`, `HORAPERMISO`
#### Campos auxiliares (usados por PKG_SCA_DEPURA_TAREO)
- `CODAUX1`, `CODAUX2`, `CODAUX3`, `CODAUX4`, `CODAUX5` — NVARCHAR2(50) — tags/etiquetas
- `HORAUX1`, ..., `HORAUX5` — DATE — valores auxiliares

### SCA_FOTOCHECK — Fotochecks/badges por empleado
- PK: ID_FOTOCHECK (secuencia)
- FK a PLA_PERSONAL (COD_EMPRESA, COD_PERSONAL)
- FECINI_FOTOCHECK, FECFIN_FOTOCHECK — vigencia del fotocheck
- ORI_FOTOCHECK — origen (FC=biométrico, etc.)
- ACT_FOTOCHECK — activo (1=activo)

### SCA_HORARIO_CAB — Cabecera de horario
- PK: HORID (CHAR 4)
- HORDES — descripción
- HORTIP — tipo
- REGGID — regla general
- REGTID — regla tardanza
- REGHEID — regla horas extras
- HORCLA — clasificación (EP, AM, normal...)
- IND_ROTATIVO, IND_FLEXIBLE
- DESCANSOROTATIVO — 'S' = descanso rotativo
- IND_EMP_ASUM_REFRI
- TIEMPOTRANSITO — minutos de tránsito

### SCA_HORARIO_DET — Detalle de horario por día
- PK: (HORID, DIAID)
- DIAID — código día semana (resultado de ProcessDay())
- HORING, HORSAL — entrada/salida teórica (base 01/01/1900)
- TOTHORAS — total horas jornada
- HORINIREF, HORFINREF, TOTREF — refrigerio teórico
- HORINIREF2, HORFINREF2 — segundo refrigerio
- DESCANSO — 'S' = descanso
- APLICA — 'S'/'N' = si este día tiene jornada activa
- HORCLADET — clasificación del día (AM, EP, normal)
- H_DIA_INI — día de inicio (para turnos que cruzan semana)
- HORTUR — turno (T1, T2, T3...)
- HORSAL_RESREFDES, TIEDES_RESREFDES — refrigerio residual en descanso
- IND_RESREF, IND_RESREFDES — indicadores refrigerio residual
- APLICAREF — si aplica refrigerio

### SCA_HORARIO_PERSONAL — Asignación horario→empleado
- PK: (COD_EMPRESA, COD_PERSONAL, FEC_VIGENCIA)
- HORID — FK a SCA_HORARIO_CAB
- Para obtener horario vigente: MAX(FEC_VIGENCIA) WHERE <= fecha_proceso
- CODAUX1, CODAUX2 — auxiliares (copian a SCA_ASISTENCIA_TAREO)

### SCA_PARAMETROS — Parámetros del sistema por empresa/sucursal/cc
| Campo | Descripción |
|---|---|
| COD_EMPRESA | empresa/sucursal/cc según v_modo |
| HORARIO_NOCT | '1'=nocturno(cruza medianoche), '2'=amanecida, otro=normal |
| DIF_MAR_CONS | diferencia para marcas consecutivas |
| MIN_MAX_RAZ_TARD | máx tardanza razonable (minutos) |
| MIN_RAZ_HNORMAL | mín horas normales (minutos) |
| MIN_REFRI | mín refrigerio (minutos) |
| MIN_A_PART_HEXTRA | mín para contar extras (minutos) |
| AJUSTE_HEXTRA | ajuste cálculo extras |
| MIN_MIN_RAZ_HEXTRA | mín mínimo extras razonables |
| AJUSTE_TOTHORANOCTURNA | ajuste horas nocturnas |
| REDONDEO_TOTHORANOCTURNA | redondeo nocturnas |
| MIN_REFRI_REAL | mín refrigerio real |

### SCA_REGLAS_CAB — Cabecera de reglas
- REGID (PK), REGDES, REGTIP, REGFEC, COD_CONCEPTO

### SCA_REGLAS_DET — Detalle de reglas (tardanza + HE)
Campos tardanza: MINTOLEING, MINTOLEREF, MINTOLEMES, HAYDCTOTAR, TIPDCTOTAR,
TIPDEDTAR, TIPDCTOTARREAL, TIPODESCUENTOFALTA
Campos HE: HAYHEING, HAYHESAL, HAYPAGOHE, TIPPAGOHE, HAYHEDBL, HAYBONNOC,
TIPBANCOHE, MINHEAUT, AUTHD, MINHDAUT, TIPDISHE

### SCA_RANGOS_HEXTRAS — Rangos para cálculo horas extras
- FK desde REGHEID → SCA_REGLAS_DET → SCA_RANGOS_HEXTRAS
- H25F — fin del tramo H25% (extra al 25%)
- H35I, H35F — inicio/fin del tramo H35%
- HNI — inicio horas nocturnas

### PLA_PERSONAL — Maestro de empleados
- PK: (COD_EMPRESA, COD_PERSONAL)
- TIP_ASISTENCIA: C1=asistencia automática sin marcas, C2=automática con marcas
- COD_TIPO_PLANILLA, COD_SUCURSAL, COD_C_COSTOS, NUM_VER_C_COSTOS
- COD_SPRING — código para exportar a sistemas externos

### PLA_TIPO_PLANILLA — Tipo de planilla
- PK: (COD_EMPRESA, COD_TIPO_PLANILLA)
- IND_OBRERO — 'S' = obreros (4 marcas requeridas)
- HORA_CIERRE_DIUR — hora límite diurno (para calcular horinihornoc/horfinhornoc)
- HORA_CIERRE_NOCT — hora límite nocturno
- IND_TAREO, IND_ASISTENCIA, IND_CALC_HEXTRAS

---

## TABLAS TEMPORALES (por sesión de proceso)

### TT_V_SCA_MARCAS — Marcas del día de proceso
Cargada en PASO 02 de SP_SCA_PROCESO_TRABAJADOR. Campos clave:
- IDCOD, IDTARJETA, COD_EMPRESA, COD_PERSONAL
- HORID, HORCLA, CODAUX1, CODAUX2
- HORING, HORSAL (teórico del horario), TOTHORAS
- HORINIREF, HORFINREF, TOTREF (teórico)
- DESCANSO, HORTUR, IND_FLEXIBLE, IND_EMP_ASUM_REFRI, DESCANSOROTATIVO
- FECHA_EQUIV, FECHA (DATE), HORA (CHAR 8)
- TIPMAR (1/2/3), ORDEN, IND_AMAN_HOR_EST
- HORCLA_DA, HORSAL_DA, HORCLA_DD (horarios día anterior/siguiente)
- ORI_FOTOCHECK

### TT_SCA_TRAMOS — Tramos de refrigerio
- COD_EMPRESA, COD_PERSONAL
- ENTRADA_FIJADA, SALIDA_FIJADA, HORINIREF, HORFINREF, HORINIREF2, HORFINREF2
- MIN_REFRI, INI, FIN
- IND_REFRI1 ('R1'), INDCER_REFRI1 — indicadores primer refrigerio
- IND_REFRI2 ('R2'), INDCER_REFRI2 — indicadores segundo refrigerio
- HNT_HAI, HNT_HEF, HNT_HEX — horas nocturnas del tramo

### TT_SCA_TRAMOS2 — Horas nocturnas acumuladas
- COD_EMPRESA, COD_PERSONAL
- HNT_HAI, HNT_HEF, HNT_HEX

---

## OTRAS TABLAS SCA

| Tabla | Descripción |
|---|---|
| SCA_PERMISO_CAB | Cabecera de permisos. PER_DIA_COMP='S' = día completo |
| SCA_PERMISO_DET | Detalle de permisos (por fecha) |
| SCA_CONCEPTO_PERMISO | Tipos de permiso. GRUPOID: DMED, VACA, SUBI, SUSP, LSIN, LPAT, LFAC, PGOF, PGOC, PSGO |
| SCA_MARCASPERMISO | Marcas insertadas por permisos |
| SCA_AUTORIZACION | Autorizaciones de HE. TIP_AUTHE='E'/'D'/'O'. FEC_AUTHE, CAN_AUTHE |
| SCA_COMPENSACION | Compensaciones de horas (TIPOORIGEN E/D/B/I → TIPOCOMPENSACION A/T/N/F/P/I). Detalle: `aquarius_compensaciones.md` |
| SCA_BANCOHORAS_MES/SEM | Banco de horas mensual/semanal |
| SCA_ALERTAAUTOMATICA | Alertas automáticas |
| SCA_FECHA_PROCESO | Días de proceso (FEC_PROCESO, DIA_PROCESO=DIAID) |
| SCA_FEC_PROC_MARC | Fechas de proceso de marcas |
| SCA_MES_PROC | Meses de proceso |
| SCA_SEM_PROC | Semanas de proceso |
| SCA_PERIODOS | Períodos de liquidación |
| SCA_TRASLADO | Traslados de personal |
| SCA_FOTOCHECK_ESP | Fotochecks especiales |
| SCA_RELOJ | Reloj/lectora biométrica |
| SCA_RELOJ_USUARIO | Reloj asignado a usuario |
| SCA_CONCEPTO_ASIS | Conceptos de asistencia |
| SCA_RANGOS_TAR | Rangos de tardanza |
| SCA_HORASPENDIENTES_PERIODO | Horas pendientes por período |
| SCA_ACTUALIZAR_PERSONAL | Actualización de personal |
| PLA_CALEND_FERIADO_* | Calendarios de feriados (SUCURSAL, COSTOS, EVENTUAL, FIJO) |

---

## FUNCIONES CLAVE

| Función | Descripción |
|---|---|
| `ProcessDay(fecha DATE)` | Retorna DIAID (código día semana) para consulta en SCA_HORARIO_DET |
| `Holiday(fecha, empresa, personal)` | Retorna 'F' si feriado, NULL si no. Consulta 4 tablas de feriados |
| `DATEADD(fecha1, fecha2)` | Suma la porción horaria de fecha2 a fecha1 |
| `DATEDIFF(fecha1, fecha2)` | Resta fecha2 de fecha1, retorna DATE base 01/01/1900 |
| `DATEDIFF2(fecha1, fecha2)` | Similar a DATEDIFF pero solo extrae HH24:MI (no días) |
| `DATEADD2(f1, f2, f3)` | Suma los tiempos de f2 y f3 a f1 |
| `CantidadConLetra(n)` | Número en letras (para impresión) |
| `SPLIT2(texto, separador, pos)` | Split de string |

---

## PROCEDIMIENTO PRINCIPAL: SP_SCA_PROCESO_TRABAJADOR
Procesa un empleado para un día. Parámetros: v_fec_proceso, v_cod_empresa, v_cod_personal, v_modo, cv_1 (cursor salida).

**v_modo determina de qué tabla de SCA_PARAMETROS se leen parámetros:**
- '1' = por empresa (cod_empresa)
- '2' = por sucursal (cod_sucursal de empleado)
- '3' = por centro de costos

**PASO 01 — Variables globales**
- Lee `horario_noct` de SCA_PARAMETROS
- Determina rango de fechas para buscar marcas:
  - hornoc='1' (nocturno, cruza medianoche): v_fecdes=proceso, v_fechas=proceso+1
  - hornoc='2' (amanecida): v_fecdes=proceso-1, v_fechas=proceso
  - otro (normal): v_fecdes=proceso-1, v_fechas=proceso

**PASO 02 — Carga TT_V_SCA_MARCAS**
- JOIN: SCA_HISTORIAL ← SCA_FOTOCHECK ← PLA_PERSONAL ← SCA_HORARIO_PERSONAL ← SCA_HORARIO_CAB ← SCA_HORARIO_DET
- Filtros: marcas entre v_fecdes y v_fechas, no anuladas, no cerradas
- Casos especiales AM (amanecida) y EP (especial con horing=00:00)
- UPDATE fec_equiv para marcas de horarios AM/EP

**PASO 03 — Actualiza FEC_EQUIV**
- Elimina de TT_V_SCA_MARCAS las que no pertenecen al día de proceso
- UPDATE SCA_HISTORIAL con fec_equiv calculada

**PASO 04 — Insert SCA_ASISTENCIA_TAREO**
1. Primero maneja permisos día completo: anula automáticas, elimina manuales de SCA_HISTORIAL
2. Maneja tip_asistencia C1/C2
3. DELETE+INSERT a SCA_ASISTENCIA_TAREO:
   - `entrada` = MIN(fecha+hora) de TT_V_SCA_MARCAS
   - `salida` = MAX(fecha+hora) de TT_V_SCA_MARCAS
   - `nummarcaciones` = COUNT(*)
   - `alerta01` = 'MI' si COUNT(*) es impar
   - Datos de horario copiados de SCA_HORARIO_DET
   - UNION con registros de permisos DÍA COMPLETO (alerta09='PE')
   - UNION con C1 (asistencia automática completa) — solo si no hay permiso ni feriado
   - UNION con C2 (automática con marcas)
4. UPDATE con ind_obrero, horinihornoc/horfinhornoc de PLA_TIPO_PLANILLA
5. UPDATE parámetros de SCA_PARAMETROS
6. UPDATE reglas de SCA_REGLAS_DET + SCA_RANGOS_HEXTRAS (h25f, h35i, h35f, hni, etc.)

**PASO 05 — Tardanza y salida anticipada**
- Tardanza: compara entrada vs entrada_fijada (+tolerancia si flexible)
  - Descuenta refri si entrada cae dentro del horario de refri
  - TN (≤ min_max_raz_tard) o TE (excede)
- Salida antes: compara salida vs salida_fijada (+tardanza+refriExtra si flexible)
  - Descuenta totref si salida antes del refri
  - SN o SE
- Asignación automática de permisos: si hay permiso aprobado que cubre exactamente la tardanza/salida, inserta 2 o 4 marcas en SCA_HISTORIAL con tiporeg='2' y actualiza nummarcaciones/entrada

**PASO 06+ — Tramos y cálculos (TT_SCA_TRAMOS)**
- Procesa cursor de marcas para asignar R1/R2 a cada marca intermedia
- Luego cursor tramos_aux1/2 detecta duplicados de R1/R2
- Calcula horarefrigerio real
- Calcula TOTHORAMARCAS, HORAEFECTIVA
- Calcula horas extras (HORAEXTANTES, HORAEXTRA, breakdown H25/H35/Dob)
- Calcula TOTHORANOCTURNA
- Autorizaciones (SCA_AUTORIZACION): aplica HEA (antes entrada), HED (después salida), HEO (otras)
- Compensaciones (PASO 15): aplica SCA_COMPENSACION — 2 cursores (fechadestino, fechaorigen). Detalle completo en `aquarius_compensaciones.md`

---

## BATCH: SP_SCA_PROCESO_TOTAL
Llama SP_SCA_PROCESO_TRABAJADOR para todos los empleados del día.

---

## SPs DE LECTURA USADOS POR EL CRONOGRAMA .NET

### sp_SCA_Read_Tareo_ByPer (CRONOGRAMA INDIVIDUAL — línea 9800 aquarius.txt)
Parámetros: v_cod_empresa, v_cod_personal, v_fecha_inicio, v_fecha_final, v_cod_alerta
- Retorna TODAS las columnas de `SCA_ASISTENCIA_TAREO` + campos calculados
- Incluye: `horaextra_ajus`, `horadoblesof`, `horabancoh`, `horaexofi1/2/3`, todas las alertas
- **TOOLTIPS de compensaciones (descomp_*)**:
  - `descomp_hnt`: si `alerta03='HC'` → subquery a `SCA_COMPENSACION WHERE tipocompensacion='N' AND fechadestino=fechamar AND ROWNUM=1`
  - `descomp_tar`: si `alerta04='TC'` → subquery WHERE `tipocompensacion='T' AND ROWNUM=1`
  - `descomp_has`: si `alerta07='SC'` → subquery WHERE `tipocompensacion='A' AND ROWNUM=1`
  - Formato: "Origen: Horas Extras, Fecha: dd/MM/yyyy, Horas: HH:MM"
  - **LIMITACIÓN ROWNUM=1**: cuando hay múltiples filas en SCA_COMPENSACION (caso multi-día en PKG_SCA_COMP_DIA_DIA), solo muestra la PRIMERA fuente en el tooltip. No es un error funcional, solo display.
- Columna extra: `horas_tras` — tiempo total de SCA_TRASLADO para ese día
- Filter por `SCA_FECHA_PROCESO` (JOIN): solo muestra días de proceso activos

### sp_SCA_Read_Tareo_Masivo (CRONOGRAMA MASIVO — línea 9890 aquarius.txt)
Parámetros adicionales: v_cod_sucursal, v_cod_tipo_planilla, v_c_costos, v_cod_horario, v_cod_grupo_menu, v_cod_usuario
- Retorna mismos campos de SCA_ASISTENCIA_TAREO + nombre empleado
- **NO tiene descomp_* tooltips** (versión simplificada para grilla masiva)
- Filtros adicionales: permisos de usuario (MAE_USUARIO_EMP, MAE_SUCURSAL_USUARIO, PLA_PERFIL_PLANILLA/ACCESO/USUARIO_PLANILLA)

### Comportamiento después de compensación (PKG_SCA_COMP_DIA_DIA):
- DÍA ORIGEN: `horaextra_ajus` / `horadoblesof` / `horabancoh` se DESCUENTAN por `prv_aplicar_origen`. Si llegan a 0 → quedan NULL → cronograma muestra `00:00` / blanco. ✅ Funciona correctamente.
- DÍA DESTINO: `horatardanza/horaantesalida/horas_no_trabajadas/horas_falta` → NULL; `horaefectiva` += horas compensadas; alerta = TC/SC/HC/FC/PC.
- Los queries de cronograma leen valores actualizados directamente de SCA_ASISTENCIA_TAREO — NO cachean.

---

## INTERFACE PROCEDURES

### INTERFACE_ASSITIME (empresa + numpla + fecini + fecfin)
Exporta a sistemas de planilla. Conceptos:
- 1000 = horas efectivas (en horas, con feriados añadidos)
- 1022 = turno T2 (horas efectivas con hortur='T2')
- 1024 = turno T3 (hortur='T3')
- 1074 = días trabajados (con feriados)
- 2018 = tardanza en minutos (solo si >10 min)
- 1010 = HORAEXOFI1 (extras ofi 25%)
- 1039 = HORAEXOFI2 (extras ofi 35%)
- 1011/1012/1072 = HORADOBLESOF (dobles ofi, según empresa 0001/0002/0003)

Destino de UPDATE según empresa:
- 0001 → ARBONA.INGRE_PLA
- 0002 → SOLSA.INGRE_PLA
- 0003 → SIG.INGRE_PLA

### INTERFACE_ASSITIME_EMP (empresa + numpla + año + mes)
Versión mensual. Concepto 1000 = 30 - días_falta.

---

## VALORES Y CÓDIGOS IMPORTANTES

### TIPMAR / TIPOREG (en SCA_HISTORIAL)
- '1' = automática (lectora/biométrico FC)
- '2' = permiso (asignación automática por sistema)
- '3' = manual (cargada manualmente)

### HORCLA / HORCLADET (clasificación horario)
- **EP** = Especial (horing=01/01/1900 00:00, es decir entrada a medianoche, marca el día anterior)
- **AM** = Amanecida (turno nocturno que termina en madrugada del día siguiente)
- Normal = cualquier turno diurno estándar

### DIAID (ProcessDay)
Código numérico/char del día de semana para consultar SCA_HORARIO_DET

### IND_OBRERO
- 'S' en PLA_TIPO_PLANILLA = tipo obrero, requiere 4 marcas: E + IR + FR + S
- Empleados sin ind_obrero solo requieren 2 (entrada + salida)

### DESCANSO
- 'S' en SCA_HORARIO_DET/SCA_ASISTENCIA_TAREO = día de descanso programado
- En descanso con marcas: todas las horas son dobles (PKG_SCA_DEPURA_TAREO lo maneja)

### ALERTA01 = 'MI' (Marca Impar)
- Contador de marcas es impar → sistema no puede determinar E/S/IR/FR correctamente
- Es la alerta más importante para procesos de depuración

### TIP_ASISTENCIA en PLA_PERSONAL
- C1 = asistencia automática completa SIN marcas (sistema crea el tareo con E/S del horario)
- C2 = asistencia automática solo SI hay marcas en SCA_HISTORIAL para ese día

### hornoc en SCA_PARAMETROS
- '1' = turno NOCTURNO (cruza medianoche: entra ~23:00, sale ~07:00 del día siguiente)
- '2' = turno AMANECIDA (entra tarde del día anterior, sale temprano)
- otro = normal

---

## OBJETOS EN BD (todos VALID)
**Funciones:** CANTIDADCONLETRA, DATEADD, DATEADD2, DATEDIFF, DATEDIFF2, HOLIDAY, PROCESSDAY, SPLIT2
**Packages:** PKG_SCA_DEPURA_TAREO — ver nota abajo
**Procedures SP_SCA_***: PROCESO_TOTAL, PROCESO_TRABAJADOR, INSERT/UPDATE/DELETE/READ de todas las entidades
**Procedures SP_MAE_***: mantenimiento de empresa, sucursal, tipo planilla, centros costos
**Procedures INTERFACE_***: exportación a sistemas de planilla
**Secuencia:** id_cod_seq (para IDCOD en SCA_HISTORIAL)

---

## NOTA CRÍTICA: PKG_SCA_DEPURA_TAREO
- Este paquete fue creado **fuera de la lógica estándar** del sistema Aquarius
- Su propósito: resolver/corregir problemas en los tareos y marcaciones que el proceso nativo (SP_SCA_PROCESO_TRABAJADOR) genera incorrectamente
- NO usarlo como referencia para entender la lógica base del sistema
- Su funcionamiento completo está documentado en `aquarius_pkg_depura_body.md`

1. **Paridad de marcas**: sistema espera número PAR de marcas. Impar → alerta01='MI'
2. **Obreros**: 4 marcas (E, IR, FR, S). Empleados: 2 marcas (E, S)
3. **Horario vigente**: MAX(FEC_VIGENCIA) WHERE <= fecha_proceso en SCA_HORARIO_PERSONAL
4. **Refrigerio real vs teórico**: si hay marcas de refri → reales; si no → teórico de SCA_HORARIO_DET
5. **Base de fechas**: campos "tiempo" (horiniref teórico, tothoras, etc.) usan base 01/01/1900. Campos "marca real" usan fecha real. NO mezclar sin TO_CHAR/TO_NUMBER
6. **Fotocheck**: un empleado puede tener múltiples fotochecks con rangos de vigencia
7. **Feriados**: 4 niveles: sucursal > centro costos > eventual > fijo (función Holiday())
8. **Nocturno**: v_fecdes/v_fechas definen qué días buscar marcas según hornoc
9. **Descanso**: descanso='S' + marcas = día trabajado en descanso = horas dobles
10. **Permisos día completo**: anulan/eliminan marcas FC y manuales; crean registro con alerta09='PE'
