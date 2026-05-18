# PLANEAMIENTO DE PLANTA — EMPRESA MANUFACTURERA (HILANDERÍA / TINTORERÍA)
## Base de Datos SIG · Oracle 11.2.0.4 · Esquema: SIG

---

## ROL DEL ASISTENTE

> Actúa como un Senior Technical Product Manager experto en la industria textil de hilados. Tu objetivo es ayudarme a traducir el planeamiento físico de la planta 'La Colonial' en un sistema web robusto en .NET Core. Debes ser crítico con los tiempos de entrega y proponer validaciones para evitar retrasos en el flujo de producción.

---

## SKILLS DEL ASISTENTE

El agente no solo debe saber de ingeniería: debe actuar como un **Analista de Sistemas con enfoque en Procesos Industriales**, capaz de traducir la realidad física de la fábrica (hilos, tintorería, máquinas) en lógica de software para una plataforma **.NET Core / Razor**.

---

### 🧠 Skills de Dominio — Industria Textil

| Skill | Descripción |
|---|---|
| **Mapeo de Flujo de Valor (VSM)** | Identificar cuellos de botella desde la orden de compra hasta el despacho de hilos (hilandería, teñido, acabados). |
| **Gestión de Producción Textil** | Conocimiento en procesos de tintorería automatizada y tiempos de reposo/secado para estimar fechas de entrega reales. |
| **Logística de Despacho** | Organizar la salida de pedidos según prioridad de cliente o ruta de transporte. |

---

### 💻 Skills de Desarrollo — .NET Core / Razor

| Skill | Descripción |
|---|---|
| **Arquitectura de Software** | Diseñar modelos de datos que reflejen procesos físicos. Entidades clave: `Pedido`, `Proceso`, `Máquina`, `Operario`, `TiempoEstándar`. |
| **Lógica de Negocio en C#** | Crear algoritmos de estimación de tiempo basados en la carga actual de la planta. |
| **Diseño de Dashboards** | UX para operarios y gerencia: visualización de retrasos mediante interfaces limpias con Razor Pages. |

---

### �️ Skills Técnicos — Oracle Legacy & Data Mapping

| Skill | Descripción |
|---|---|
| **Optimización para Oracle 10g** | Escribir consultas SQL optimizadas para el optimizador de 10g, evitando funciones modernas no soportadas. |
| **Modelado de Datos Relacional** | Diseñar diagramas Entidad-Relación normalizados que minimicen la redundancia en grandes volúmenes de datos textiles. |
| **ODP.NET / Entity Framework Legacy** | Conexión de .NET Core con Oracle 10g mediante proveedores compatibles (`Oracle.ManagedDataAccess`). |
| **PL/SQL Avanzado** | Crear Stored Procedures y Triggers en Toad que manejen la lógica pesada (cálculo de tiempos, etc.) directamente en el servidor. |

---

### �📋 Skills de Consultoría de Negocios

| Skill | Descripción |
|---|---|
| **Levantamiento de Requerimientos** | Guiar qué preguntas hacerle a la consultora de planeamiento para no escapar ningún detalle técnico. |
| **Documentación de Procesos** | Generar documentación clara que sirva de puente entre los ingenieros de planta y el código. |

---

## HABILIDADES SECUNDARIAS (Críticas para el Proyecto)

---

### ⚙️ Habilidades Técnicas (Hard Skills)

| Habilidad | Descripción |
|---|---|
| **Ingeniería de Métodos y Tiempos** | Analizar diagramas de flujo de proceso y calcular tiempos estándar (cycle time). |
| **Metodología SLP** *(Systematic Layout Planning)* | Uso de diagramas de hilos y matrices de relación de actividades para optimizar espacios. |
| **Lean Manufacturing** | Conocimiento en 5S, Kanban, SMED y eliminación de desperdicios (Muda). |
| **Ergonomía y Seguridad Industrial** | Aplicación de normativas de salud ocupacional para el diseño de estaciones de trabajo. |

---

### 📊 Habilidades Analíticas y de Gestión

| Habilidad | Descripción |
|---|---|
| **Gestión de Proyectos (PMO)** | Estructurar fases, cronogramas y definir hitos críticos. |
| **Análisis de Datos y Pronósticos** | Interpretación de tendencias de demanda para calcular la capacidad instalada necesaria. |
| **Simulación de Procesos** | Modelado de cuellos de botella y flujos de materiales. |

---

### 🗣️ Habilidades de Facilitación (Soft Skills)

| Habilidad | Descripción |
|---|---|
| **Secretario Ejecutivo / Moderador** | Estructurar actas de reuniones y resumir acuerdos clave de los stakeholders. |
| **Gestión del Cambio** | Redactar comunicaciones internas que reduzcan la resistencia de los operarios al nuevo diseño. |

---

## ⚠️ REGLA DE TRABAJO — CARPETA PLNM

**CADA VEZ QUE SE HAGAN CAMBIOS EN ESTA CARPETA: `D:\.Net\WorkSpace_BD\SIG\PLNM`**

**DEBES ANALIZAR LA MEMORIA ANTES DE EMPEZAR A HACER CUALQUIER CAMBIO, ANÁLISIS O ACCIÓN.**

Esto incluye:
- Leer `Planeamiento.md` (este archivo) desde el inicio.
- Revisar todo el contenido de la carpeta `D:\.Net\WorkSpace_BD\SIG\PLNM`.

Solo después de ese análisis previo se debe proceder con cualquier cambio o consulta.

---

## 1. CONEXIÓN

| Parámetro        | Valor                              |
|------------------|------------------------------------|
| **Driver**       | Oracle / ODP.NET / JDBC Thin       |
| **Host**         | 10.0.7.11                          |
| **Puerto**       | 1521                               |
| **Servicio**     | ORCL                               |
| **Usuario**      | SIG                                |
| **Password**     | STARK                              |
| **ConnString**   | `Data Source=10.0.7.11:1521/ORCL;User Id=SIG;Password=STARK;` |
| **JDBC URL**     | `jdbc:oracle:thin:@10.0.7.11:1521/ORCL` |
| **Charset DB**   | WE8ISO8859P15                      |
| **NLS_DATE**     | DD-MON-RR                          |
| **Versión**      | Oracle Database 11.2.0.4.0         |
| **Modo**         | READ WRITE                         |

---

## 2. RESUMEN GENERAL DEL ESQUEMA

| Objeto            | Cantidad |
|-------------------|----------|
| Tablas            | 1.016    |
| Vistas            | 157      |
| Paquetes          | 18       |
| Procedimientos    | ~50      |
| Funciones         | ~170     |
| Triggers          | ~400     |
| Secuencias        | 43       |
| Sinónimos         | 2        |

### Contexto del negocio
El esquema **SIG** es el sistema integrado de gestión de una empresa manufacturera de **hilandería y tintorería** (hilados de fibra textil). Cubre:

- Planeamiento de producción (hilatura, preparatorias, tintorería)
- Control de rutas de producción y recetas de tintorería
- Gestión de pedidos y despacho a clientes
- Compras y logística (órdenes de compra, requisiciones)
- Inventario de materias primas, productos en proceso y terminados (almacenes, lotes, partidas, kardex)
- Control de calidad (calificación de partidas, no conformidades, hallazgos)
- Mantenimiento de maquinaria (cronograma, programa, sobreparte)
- Planilla / RRHH (empleados, sueldos, vacaciones, CTS)
- Contabilidad (movimientos, registro diario, plan de cuentas)
- Cobranzas / Finanzas (facturas, letras, saldos, conciliación)
- Activos fijos
- Sistema de seguridad / usuarios

---

## 3. MÓDULOS Y TABLAS (CATÁLOGO COMPLETO)

### 3.1 PLANEAMIENTO DE PRODUCCIÓN — Prefijo `L_` / `PLA_ANUAL` / `PARAMPLA` / `HORAS_PLA`

Tablas centrales del módulo de **planeamiento de planta**.

| Tabla | Filas | Descripción |
|-------|------:|-------------|
| `L_PLANTILLA_G` | 96 | Cabecera de plantilla de planeamiento (color, curva, título, fibra, proceso, máquina) |
| `L_PLANTILLA_D` | 1.584 | Detalle de plantilla: artículos (receta) con % y GL por ítem |
| `L_PLANTILLA_FIBRA` | 4 | Asociación plantilla-fibra (código de filtro de fibra) |
| `L_PLANTILLA_MAQUINA` | 20 | Asociación plantilla-máquina |
| `L_PLANTILLA_PROCESO` | 37 | Asociación plantilla-proceso |
| `L_PLANTILLA_TITULO` | 63 | Asociación plantilla-título |
| `L_PLA_PARAM_FIBRA` | 1 | Parámetro de agrupación de fibras (cabecera) |
| `L_PLA_PARAM_FIBRA_D` | 4 | Detalle de fibras por parámetro |
| `L_PLA_PARAM_MAQUINA` | 3 | Parámetro de agrupación de máquinas (cabecera) |
| `L_PLA_PARAM_MAQUINA_D` | 61 | Detalle de máquinas por parámetro |
| `L_PLA_PARAM_PROCESO` | 2 | Parámetro de agrupación de procesos (cabecera) |
| `L_PLA_PARAM_PROCESO_D` | 46 | Detalle de procesos por parámetro |
| `L_PLA_PARAM_TITULO` | 5 | Parámetro de agrupación de títulos (cabecera) |
| `L_PLA_PARAM_TITULO_D` | 245 | Detalle de títulos por parámetro |
| `L_PROCESOD` | 3 | Detalle proceso para planeamiento |
| `L_PROCESOG` | 3 | Cabecera proceso para planeamiento |
| `L_PROCESOL` | 10 | Lista proceso planeamiento |
| `L_RECETAD` | 19 | Detalle de receta de planeamiento |
| `L_RECETAG` | 1 | Cabecera de receta de planeamiento |
| `L_RECTIFICA_RECETA` | 11.280 | Historial de rectificaciones de receta |
| `L_TRICOMIA` | 63 | Tricomía de planeamiento |
| `L_TRICOMIA_D` | 2 | Detalle tricomía |
| `L_TRICOMIA_G` | 3 | Cabecera tricomía |
| `L_VALIDA_RECETA` | 71.742 | Validación de recetas (resultado de cálculo) |
| `L_VALIDA_REC_TRIC` | 0 | Validación receta + tricomía |
| `L_CURVAS` | 1 | Tabla de curvas de planeamiento |
| `PLA_ANUAL` | 7.548 | Plan anual de producción por periodo/concepto |
| `PARAMPLA` | 4.593 | Parámetros del módulo de planeamiento (valores de configuración) |
| `HORAS_PLA` | 1.448 | Horas planilladas por empleado/concepto/centro de costo |
| `CARGA_MAQ` | 88 | Carga de máquinas (parámetros de capacidad) |
| `CARGA_PROVI` | 93 | Cargas provisionales (vacaciones, gratif., CTS) |
| `ESTACIONALIDAD` | 1 | Factor de estacionalidad de producción |

#### L_PLANTILLA_G — Columnas

| # | Columna | Tipo | Null | Descripción |
|---|---------|------|------|-------------|
| 1 | `NUMERO` | NUMBER(8) | N (PK) | ID plantilla |
| 2 | `FECHA` | DATE | Y | Fecha creación |
| 3 | `NCOLOR` | VARCHAR2(7) | Y | Código de color |
| 4 | `GLOSA` | VARCHAR2(120) | Y | Descripción libre |
| 5 | `CURVA` | VARCHAR2(2) | Y | Código curva |
| 6 | `INTENSIDAD_DESDE` | NUMBER(8,4) | Y | Rango intensidad inicio |
| 7 | `INTENSIDAD_HASTA` | NUMBER(8,4) | Y | Rango intensidad fin |
| 8 | `ESTADO` | VARCHAR2(1) | Y | Estado ('0'=activo) |
| 9 | `A_ADUSER` | VARCHAR2(15) | Y | Usuario creación |
| 10 | `A_ADFECHA` | DATE | Y | Fecha creación |
| 11 | `A_MDUSER` | VARCHAR2(15) | Y | Usuario modificación |
| 12 | `A_MDFECHA` | DATE | Y | Fecha modificación |
| 13 | `TITULO` | VARCHAR2(3) | Y | Código de título (ref. L_PLA_PARAM_TITULO) |
| 14 | `FIBRA` | VARCHAR2(3) | Y | Código fibra (ref. L_PLA_PARAM_FIBRA) |
| 15 | `PROCESO` | VARCHAR2(3) | Y | Código proceso (ref. L_PLA_PARAM_PROCESO) |
| 16 | `MAQUINA` | VARCHAR2(3) | Y | Código máquina (ref. L_PLA_PARAM_MAQUINA) |
| 17 | `PROCESO_RECETA` | VARCHAR2(6) | Y | Proceso para receta |
| 18 | `CURVA_ANT` | VARCHAR2(10) | Y | Curva anterior |
| 19 | `ANTES_DESPUES_SAL` | VARCHAR2(1) | Y | Indicador antes/después de salida |

#### L_PLANTILLA_D — Columnas

| # | Columna | Tipo | Null | Descripción |
|---|---------|------|------|-------------|
| 1 | `NUMERO` | NUMBER(8) | N (PK) | FK → L_PLANTILLA_G.NUMERO |
| 2 | `ITEM` | NUMBER(8) | N (PK) | Nro. de ítem |
| 3 | `COD_ART` | VARCHAR2(25) | Y | Artículo (reactivo/insumo) → ARTICUL |
| 4 | `GL` | NUMBER(8,4) | Y | Gramos por litro |
| 5 | `PORCENT` | NUMBER(8,4) | Y | Porcentaje |
| 6 | `OBSERVACIONES` | VARCHAR2(200) | Y | Observaciones |
| 7 | `ESTADO` | VARCHAR2(1) | Y | Estado |
| 8 | `A_ADUSER` | VARCHAR2(15) | Y | Auditoría |
| 9 | `A_ADFECHA` | DATE | Y | Auditoría |
| 10 | `A_MDUSER` | VARCHAR2(15) | Y | Auditoría |
| 11 | `A_MDFECHA` | DATE | Y | Auditoría |
| 12 | `PROCESO` | VARCHAR2(1) | Y | Indicador proceso |
| 13 | `SOLO_TEXTO` | VARCHAR2(1) | Y | Solo texto (sin cálculo) |

---

### 3.2 MANTENIMIENTO DE MAQUINARIA — Prefijo `MA_`

| Tabla | Filas | Descripción |
|-------|------:|-------------|
| `MA_PLANIFICACION` | 54 | Cabecera de plan de mantenimiento |
| `MA_PLANIFICACION_D` | 123 | Detalle del plan de mantenimiento |
| `MA_PLANIFICACION_F` | 8 | Fechas del plan de mantenimiento |
| `MA_PLANIFICACION_T` | 421 | Tareas del plan de mantenimiento |
| `MA_PROGRAMA` | 17.114 | Programa de mantenimiento (OT cabecera) |
| `MA_PROGRAMA_A` | 402 | Auxiliar del programa |
| `MA_PROGRAMA_D` | 15.088 | Detalle del programa (tareas ejecutadas) |
| `MA_PROGRAMA_P` | 8 | Parámetros del programa |
| `MA_PROGRAMA_T` | 14.026 | Trabajadores del programa |
| `MA_PROYECTO` | 99 | Proyectos de mantenimiento/mejora |
| `MA_TAREA` | 504 | Catálogo de tareas de mantenimiento |
| `MA_ACTIVIDAD_G` | 41 | Cabecera de actividades |
| `MA_ACTIVIDAD_D` | 737 | Detalle de actividades |
| `MA_ACTIVIDAD_A` | 220 | Artículos (repuestos) por actividad |
| `MA_PARTE` | 40 | Partes de máquinas |
| `MA_PARTEMAQ` | 221 | Partes por máquina |
| `MA_REGREPSUM` | 780 | Resumen de repuestos consumidos |
| `MA_SOBRETPO_G` | 44.796 | Cabecera sobretiempo por mantenimiento |
| `MA_SOBRETPO_D` | 8.041 | Detalle sobretiempo |
| `MA_CRONOGRAMA_ACTIVO` | 279 | Cronograma activo de mantenimiento |
| `MA_FICHA_PROG` | 0 | Ficha de programación |
| `MA_MAQ_ACTIV` | 19 | Máquinas por actividad |
| `MA_TABLAS` | 11 | Tablas auxiliares de mantenimiento |
| `MA_VCMTO_COLOR` | 4 | Vencimiento por color |

---

### 3.3 PRODUCCIÓN / HILANDERÍA — Prefijo `H_PRODUCCION` / `H_PROGRAMACION`

| Tabla | Filas | Descripción |
|-------|------:|-------------|
| `H_PRODUCCION_G` | 933.269 | Cabecera de parte de producción (turno/máquina/operario) |
| `H_PRODUCCION_D` | 1.263.670 | Detalle parte de producción (TC, título, fibra, pesos, mermas) |
| `H_PRODUCCION_A` | 694.300 | Alternativo parte de producción (preparatorias) |
| `H_PRODUCCION_GA` | 88.791 | Cabecera parte producción alternativo |
| `H_PRODUCCIOND_HI` | 1.249 | Detalle hilandería |
| `H_PRODUCCIONG_HI` | 1.207 | Cabecera hilandería |
| `H_PROGRAMACION` | 45.159 | Programación de producción de hilatura |
| `H_RPRODUC` | 259.571 | Resumen de producción |
| `H_RPRODUC_BCK_2021` | 10.096 | Backup producción 2021 |
| `H_RPRODUC_BCK_CONTINUA` | 208 | Backup producción continua |
| `H_RPRODUC_ROLLO` | 18 | Producción por rollo |
| `H_PARADAS` | 514.108 | Paradas de producción (cabecera) |
| `H_PARADAS_A` | 281.911 | Paradas alternativas |
| `H_RPARADA` | 1.991 | Resumen de paradas |
| `H_ROLLOS` | 214.422 | Producción de rollos |
| `H_RUTA` | 42 | Ruta de producción cabecera |
| `H_RUTA_D` | 467 | Ruta de producción detalle |
| `H_RUTA_LOTE_G` | 1.220 | Ruta por lote (cabecera) |
| `H_RUTA_LOTE_D` | 8.171 | Ruta por lote (detalle) |
| `H_TPROD` | 1.247 | Tipos de producción |
| `H_TURNOS` | 5 | Configuración de turnos |
| `H_MAQUINAS` | 238 | Historial de máquinas |
| `H_TCOCHE` | 257 | Tipos de coche/transporte interno |
| `P_RPRODUC` | 371 | Producción preparatoria (resumen) |
| `PR_PROGRAMA` | 8 | Programa de preparatoria |
| `PR_PROG_MAQ` | 5 | Máquinas del programa preparatorio |

#### H_PRODUCCION_D — Columnas clave

| # | Columna | Tipo | Descripción |
|---|---------|------|-------------|
| 1 | `FECHA` | DATE (PK) | Fecha del parte |
| 2 | `TURNO` | VARCHAR2(4) (PK) | Turno ('1','2','3','N') |
| 3 | `TP_MAQ` | VARCHAR2(1) (PK) | Tipo máquina |
| 4 | `COD_MAQ` | VARCHAR2(6) (PK) | Código máquina |
| 5 | `C_CODIGO` | VARCHAR2(8) (PK) | Código del operario/trabajador |
| 6 | `ITEM` | NUMBER(2) (PK) | Ítem del parte |
| 7 | `TC` / `TC_INI` / `TC_FIN` | VARCHAR2(10) / NUMBER | Ticket continua / rango |
| 8 | `TITULO` | VARCHAR2(10) | Título producido |
| 9 | `TIPO_FIBRA` | VARCHAR2(4) | Fibra trabajada |
| 10 | `COLOR` | VARCHAR2(4) | Color |
| 11 | `HORAS_INI` / `HORAS_FIN` | VARCHAR2(5) | Rango horario |
| 12 | `LOTE_FARDO` | VARCHAR2(15) | Lote de materia prima |
| 13 | `PROGRAMA` | VARCHAR2(30) | Número de programa |
| 14 | `VELOCIDAD` | NUMBER(15,2) | Velocidad de trabajo |
| 15 | `HORAS_PARADA` | VARCHAR2(5) | Horas paradas |
| 16 | `HORAS_TRABAJADAS` | VARCHAR2(5) | Horas netas trabajadas |
| 17 | `METRAJE` | NUMBER(15,2) | Metraje producido |
| 18 | `TARROS` | NUMBER(15,4) | Cantidad de tarros |
| 19 | `PESO_BRUTO` | NUMBER(15,4) | Peso bruto producido |
| 20 | `CANTIDAD` | NUMBER(15,4) | Cantidad neta |
| 21 | `MERMA1..9` | NUMBER(15,4) | Desglose de mermas |
| 22 | `COD_ART` | VARCHAR2(25) | Artículo producido |
| 23 | `NUM_PED` | NUMBER(8) | Pedido asociado |
| 24 | `TICKET_PROD` | NUMBER(8) | Ticket de producción |

---

### 3.4 TINTORERÍA — Prefijo `TT_`

| Tabla | Filas | Descripción |
|-------|------:|-------------|
| `TT_PROGPART` | 75.963 | Programa de partidas en tintorería |
| `TT_PROGPARTD` | 8.807 | Detalle programa de partidas |
| `TT_RPRODUC` | 178.003 | Producción real tintorería |
| `TT_RSECADO` | 99.200 | Secado en tintorería |
| `TT_RPARADA` | 68.338 | Paradas en tintorería |
| `TT_MAQUINA` | 39 | Máquinas de tintorería |
| `TT_EQMAQUINA` | 31 | Equivalencias de máquinas |
| `TT_MAQVOL` | 179 | Volumen por máquina |
| `TT_INCUMPLE_PROG` | 27 | Incumplimientos de programa |
| `TT_PARAMPROGTIN` | 1 | Parámetro de programación tintorería |
| `TT_ULTFECHA` | 33 | Últimas fechas de proceso |
| `TT_TEORICO_TENIDO` | 1 | Teórico de tenido |

---

### 3.5 RUTAS DE PRODUCCIÓN — Prefijo `CTRUTAS_`

Las rutas definen el flujo de fabricación estándar (procesos, máquinas, mermas, eficiencias, velocidades, costos).

| Tabla | Filas | Descripción |
|-------|------:|-------------|
| `CTRUTAS` | 632 | Ruta estándar (fibra+proceso+secuencia+máquina → merma, eficiencia) |
| `CTRUTAS_D` | 97 | Variante detallada de ruta |
| `CTRUTAS_G` | 19 | Resumen de ruta (conos, KG producidos) |
| `CTRUTAS_GEN` | 56 | Ruta genérica |
| `CTRUTAS_M` | 2 | Ruta de máquina (densidad, velocidad) |
| `CTRUTAS_TITULO` | 2.610 | Ruta por título (kg/hr, hor/máq, kW) |
| `CTRUTAS_TITULO_COLOR` | 19.590 | Ruta título+color+receta (costos de tintorería) |
| `CTRUTAS_TITULO_COLOR_D` | 341.777 | Detalle receta por ruta título+color (artículos/reactivos) |
| `CTRUTAS_TITULO_COS` | 6.062 | Costo estándar por ruta título+color |
| `CTRUTAS_TITULO_D` | 3.439 | Detalle de ruta por título |
| `CTRUTAS_TITULO_G` | 328 | Cabecera ruta título |
| `CTRUTAS_TITULO_GEN` | 266 | Ruta título genérica |
| `CTTABRUT` | 24 | Tabla de tipos de ruta |
| `CT_RUTAD` | 41 | Ruta detalle (análisis) |
| `CT_RUTAG` | 7 | Ruta cabecera (análisis) |
| `CT_RUTAH_D` | 2.643 | Historial ruta detalle |
| `CT_RUTAH_G` | 225 | Historial ruta cabecera |
| `RUTA_PROC_STD` | 31 | Proceso estándar por ruta |

#### CTRUTAS — Columnas

| # | Columna | Tipo | Descripción |
|---|---------|------|-------------|
| 1 | `FIBRA` | VARCHAR2(6) (PK) | Tipo de fibra |
| 2 | `PROCESO` | VARCHAR2(25) (PK) | Proceso productivo |
| 3 | `SECUENCIA` | VARCHAR2(3) (PK) | Orden en la ruta |
| 4 | `COD_MAQ` | VARCHAR2(6) (PK) | Máquina |
| 5 | `MERMA` | NUMBER(12,6) | % merma estándar |
| 6 | `EFICIENCIA` | NUMBER(12,6) | % eficiencia |
| 7 | `KGR_INI` | NUMBER(12,6) | Kg entrada |
| 8 | `KGR_FIN` | NUMBER(12,6) | Kg salida |
| 9 | `ESTADO` | VARCHAR2(2) | Estado |
| 10 | `RETORCIDO` | VARCHAR2(8) | Indicador retorcido |
| 11 | `COLOR` | VARCHAR2(6) | Color |
| 12 | `NRO` | VARCHAR2(6) | Número |

#### CTRUTAS_TITULO_COLOR — Columnas clave

| # | Columna | Tipo | Descripción |
|---|---------|------|-------------|
| 1-8 | FIBRA, PROCESO, RETORCIDO, COLOR, NRO, TITULO, RECETA, MAQ_TEN | PK compuesta | Identifica la ruta |
| 9 | `KILO_INI` / `KILO_FIN` | NUMBER(12,6) | Kg entrada/salida |
| 10 | `PESO` / `VOLUMEN` | NUMBER(12,6) | Peso/Volumen de proceso |
| 11 | `COSTO` | NUMBER(12,10) | Costo total |
| 12 | `COS_QUIM` | NUMBER(12,10) | Costo químicos |
| 13 | `COS_AGUA` | NUMBER(12,10) | Costo agua |
| 14 | `COS_ELEC` | NUMBER(12,10) | Costo electricidad |
| 15 | `COS_PETRO` | NUMBER(12,10) | Costo petróleo |
| 16 | `COS_MOBRA` | NUMBER(12,10) | Costo mano de obra |
| 17 | `TIEMPO` | NUMBER(12,6) | Tiempo de proceso |
| 18 | `CONSUMO` | NUMBER(12,6) | Consumo de agua |
| 19 | `SECADO` | NUMBER(12,6) | Tiempo secado |
| 20 | `KW` | NUMBER(12,6) | Consumo eléctrico kW |
| 21 | `CODIGO` | VARCHAR2(30) | Código receta |

---

### 3.6 PROCESOS Y SECCIONES

| Tabla | Filas | Descripción |
|-------|------:|-------------|
| `CTPROCESOS` | 265 | Catálogo de procesos productivos |
| `CTSECCIONES` | 54 | Secciones de planta (CC + sección) con parámetros de eficiencia |
| `PROCESO` | 62 | Tabla de procesos base |
| `H_PROCESOS` | 54 | Historial de procesos |
| `H_PROCDET` | 75 | Historial proceso detallado |
| `PA_PROCESO` | 1.953 | Proceso de análisis/control |

#### CTSECCIONES — Columnas

| # | Columna | Tipo | Descripción |
|---|---------|------|-------------|
| 1 | `CENTRO_COSTO` | VARCHAR2(6) (PK) | Centro de costos |
| 2 | `SECCION` | VARCHAR2(8) (PK) | Código de sección |
| 3 | `HOM_MAQ` | NUMBER(12,6) | Horas/hombre por máquina |
| 4 | `KGR_HRS` | NUMBER(12,6) | Kg por hora |
| 5 | `KGR_INI`/`KGR_FIN` | NUMBER(12,6) | Kg entrada/salida |
| 6 | `MERMA` | NUMBER(12,6) | % merma |
| 7 | `HRS_MAQ` | NUMBER(12,6) | Horas máquina |
| 8 | `KW_MAQ` | NUMBER(12,6) | kW por máquina |
| 9 | `TP_MAQ` | VARCHAR2(2) | Tipo de máquina |
| 10 | `HUSOS` | NUMBER(6) | Cantidad de husos |
| 11 | `EFICIENCIA` | NUMBER(12,6) | % eficiencia |

---

### 3.7 RECETAS DE TINTORERÍA — Prefijo `RECETA_` / `ING_RECETA` / `ING_RECETAS` / `II_`

| Tabla | Filas | Descripción |
|-------|------:|-------------|
| `RECETA_G` | 55.345 | Cabecera de receta de tintorería |
| `RECETA_D` | 1.013.996 | Detalle de receta (artículos/reactivos por receta) |
| `ING_RECETA_G` | 9.969 | Cabecera de ingreso de receta |
| `ING_RECETA_D` | 124.931 | Detalle de ingreso de receta |
| `ING_RECETAS_G` | 469.713 | Cabecera de receta de proceso |
| `ING_RECETAS_D` | 4.794.953 | Detalle de receta de proceso (mayor tabla del esquema) |
| `ING_RECETAS_L` | 0 | Receta proceso (temporal) |
| `RECETAS_G` | 1.848 | Recetas genéricas (cabecera) |
| `RECETAS_D` | 26.876 | Recetas genéricas (detalle) |
| `H_RECETA_G` | 3.917 | Historial receta cabecera |
| `H_RECETA_D` | 12.198 | Historial receta detalle |
| `H_RECETA_P` | 872 | Historial receta proceso |
| `H_RECETA_R` | 45.768 | Historial receta (resumen) |
| `L_RECTIFICA_RECETA` | 11.280 | Rectificaciones de receta |
| `L_VALIDA_RECETA` | 71.742 | Validación de recetas |
| `II_DESPQUI_G` | 304.324 | Despacho de químicos cabecera |
| `II_DESPQUI_D` | 1.901.694 | Despacho de químicos detalle |
| `II_DESPQUI_L` | 17.413 | Despacho de químicos (lotes) |
| `II_INPRODVAL` | 73.063 | Ingreso producción valorizado |
| `EQUIV_RECETA` | 44 | Equivalencias de receta |
| `EQUIV_RECETA_F` | 1.270 | Equivalencias de receta (fibra) |
| `TI_RECETA_TEMP` | 0 | Receta temporal tintorería |

---

### 3.8 ARTÍCULOS / CATÁLOGO DE MATERIALES — `ARTICUL` / `FIBRA` / `TFAMLIN`

| Tabla | Filas | Descripción |
|-------|------:|-------------|
| `ARTICUL` | 77.411 | Maestro de artículos (materias primas, PT, insumos, reactivos) |
| `FIBRA` | 741 | Tipos de fibra |
| `FIBRA_TMP` | 2.099 | Fibra temporal |
| `TFAMLIN` | 2.114 | Familias y líneas de artículos |
| `I_TFAMLIN` | 102 | Relación interna de familias |
| `B_HFIBRA` | 56 | Base histórica de fibra |
| `CV_FIBRA` | 9 | Variante de fibra |
| `V_FIBRA` | 58 | Vista de fibra |
| `V_TFIBRA` | 33 | Vista de tipos de fibra |
| `I_ARTICUL` | 166 | Índice/auxiliar de artículos |

#### ARTICUL — Columnas

| # | Columna | Tipo | Descripción |
|---|---------|------|-------------|
| 1 | `COD_ART` | VARCHAR2(25) (PK) | Código de artículo |
| 2 | `DESCRIPCION` | VARCHAR2(100) | Descripción |
| 3 | `TP_ART` | VARCHAR2(1) | Tipo ('M'=materia prima, 'P'=PT, 'S'=servicio...) |
| 4 | `COD_FAM` | VARCHAR2(4) | Familia |
| 5 | `COD_LIN` | VARCHAR2(6) | Línea |
| 6 | `TP_C_STCK` | VARCHAR2(1) | Tipo control stock |
| 7 | `UNIDAD` | VARCHAR2(4) | Unidad de medida |
| 8 | `S_ACT` / `S_FIS` | NUMBER(12,4) | Stock actual / físico |
| 9 | `S_PPR` / `S_PPD` | NUMBER(12,4) | Stock en proceso (recepción/despacho) |
| 10 | `S_MIN` / `S_MAX` | NUMBER(12,4) | Stock mínimo/máximo |
| 11 | `C_PRO` | NUMBER(18,6) | Costo promedio |
| 12 | `FIBRA` | VARCHAR2(8) | FK → FIBRA |
| 13 | `PROCESO` | VARCHAR2(4) | FK → CTPROCESOS |
| 14 | `TITULO` | VARCHAR2(8) | FK → tabla títulos |
| 15 | `COLOR` | VARCHAR2(7) | Color del artículo |
| 16 | `ESTADO` | VARCHAR2(1) | Estado ('A'=activo) |
| 17 | `COD_IGV` | VARCHAR2(1) | Afecto/inafecto IGV |
| 18 | `VALPF` | VARCHAR2(5) | Valor presentación fibra |
| 19 | `TFIBRA` | VARCHAR2(1) | Tipo fibra |
| 20 | `CONTROLADO` | VARCHAR2(1) | Artículo controlado |

---

### 3.9 ALMACÉN / INVENTARIO — `ALMACEN` / `LOTES` / `PARTIDA` / `KARDEX_`

| Tabla | Filas | Descripción |
|-------|------:|-------------|
| `ALMACEN` | 64.624 | Stock por artículo+almacén (saldos) |
| `LOTES` | 1.604.169 | Lotes de materiales (con saldo, tara, cantidad) |
| `ART_LOTE` | 1.346.474 | Relación artículo-lote-almacén |
| `ART_LOTE_HIST` | 225.357 | Historial artículo-lote |
| `PARTIDA` | 171.955 | Partidas de producción (unidades en proceso/terminadas) |
| `PARTIDA_MAS` | 512.530 | Partidas maestro (historial) |
| `PARTIDA_PESO` | 45.515 | Pesos de partidas |
| `PARTIDA_FENTREGA` | 3.648 | Fecha de entrega de partida |
| `PARTIDA_RESERVA` | 2.907 | Reservas de partidas |
| `PARTIDA_TMP` | 623 | Partidas temporales |
| `PARTIDA_USR` | 259 | Partidas por usuario |
| `KARDEX_D` | 4.033.904 | Kardex detallado (movimientos de almacén) |
| `KARDEX_G` | 1.105.741 | Kardex general (cabecera de movimientos) |
| `KARDEX_L` | 3.177.815 | Kardex por lote |
| `KARDEX_RESUMEN` | 23.177 | Resumen de kardex |
| `KARDEX_SUNAT` | 185.117 | Kardex formato SUNAT |
| `BARRA_INV` | 571.885 | Inventario por código de barras |
| `INV_FISICO` | 1.518.611 | Inventario físico |
| `INV_FLOCA` | 855 | Inventario floca (fibra suelta) |
| `SALDOS` | 489.573 | Saldos contables |
| `SALDOS_CXC` | 257.199 | Saldos cuentas por cobrar |
| `SALDOS_CXP` | 357.579 | Saldos cuentas por pagar |
| `HISTART` | 8.799.533 | Historial de artículos (mayor tabla del esquema) |
| `HISTART_COSTO` | 3.219.138 | Historial de costo de artículos |
| `DLOTES` | 1.257.960 | Detalle de lotes |

---

### 3.10 PEDIDOS / VENTAS — `PEDIDO` / `ITEMPED` / `DOCUVENT` / `FACTCOB`

| Tabla | Filas | Descripción |
|-------|------:|-------------|
| `PEDIDO` | 88.651 | Cabecera de pedidos de venta |
| `ITEMPED` | 229.208 | Ítems de pedidos (artículo, título, fibra, cantidad, precio) |
| `ITEMPED_DET` | 155.446 | Detalle adicional de ítems |
| `ITEMPED_COS` | 942 | Costos por ítem de pedido |
| `ITEMPED_SITU` | 1.939 | Situación de ítems |
| `ITEMPED_DUP` | 186 | Pedidos duplicados (control) |
| `ITEMPED_ALM` | 33 | Ítems pedido por almacén |
| `ITEMPED_DESTINO` | 1.061 | Destinos de entrega por ítem |
| `HISTPED` | 130.977 | Historial de pedidos |
| `HI_ITEMPED` | 161 | Historial ítems de pedidos |
| `DOCUVENT` | 198.082 | Documentos de venta (facturas, guías, NC, ND) |
| `ITEMDOCU` | 304.396 | Ítems de documentos de venta |
| `ITEMDOCU_VPED` | 0 | Ítems documento venta-pedido |
| `FACTCOB` | 300.134 | Facturas cobradas |
| `FACTCOB_2010` | 146.066 | Facturas cobradas 2010 (archivo) |
| `CABFCOB` | 330.236 | Cabecera de cobros |
| `CLIENTES` | 4.356 | Maestro de clientes |
| `CLIENTE_GRUPO` | 5 | Grupos de clientes |
| `CLIENTE_RESP` | 3.068 | Responsables de clientes |
| `CLIENTE_RELACION` | 32 | Relaciones entre clientes |
| `COTIZACION_G` | 3.022 | Cotizaciones (cabecera) |
| `COTIZACION_D` | 9.308 | Cotizaciones (detalle) |
| `COTIZACION_P` | 18.420 | Cotizaciones (precios) |
| `COTIZACION_RANGO` | 3.899 | Rangos de cotización |
| `MUESTRA` | 17.479 | Muestras enviadas a clientes |
| `ITEMUEST` | 55.426 | Ítems de muestras |
| `ARTMUEST` | 30.295 | Artículos en muestras |
| `ESTADISTICO` | 20.798 | Estadísticas de ventas |
| `VENTAS` | 649 | Resumen de ventas |
| `DETAVENT` | 20.799 | Detalle de ventas |
| `PACKING_G` | 12.865 | Packing cabecera |
| `PACKING_D` | 20.007 | Packing detalle |
| `PACKING_L` | 103.063 | Packing por lote |
| `DESPACHO_GUIA` | 3.442 | Guías de despacho |

#### ITEMPED — Columnas clave

| # | Columna | Tipo | Descripción |
|---|---------|------|-------------|
| 1 | `SERIE` | NUMBER(3) (PK) | Serie del pedido |
| 2 | `NUM_PED` | NUMBER(8) (PK) | Número de pedido |
| 3 | `NRO` | NUMBER(2) (PK) | Ítem del pedido |
| 4 | `COD_ART` | VARCHAR2(25) | Artículo → ARTICUL |
| 5 | `TITULO` | VARCHAR2(10) | Título del hilo |
| 6 | `TIPO_FIBRA` | VARCHAR2(8) | Tipo de fibra |
| 7 | `VALPF` | VARCHAR2(4) | Presentación fibra |
| 8 | `PROCESO` | VARCHAR2(4) | Proceso requerido |
| 9 | `COLOR` | VARCHAR2(7) | Color solicitado |
| 10 | `CANTIDAD` | NUMBER(12,4) | Cantidad pedida (kg) |
| 11 | `PRECIO` | NUMBER(12,2) | Precio unitario |
| 12 | `SALDO` | NUMBER(12,4) | Saldo pendiente |
| 13 | `ESTADO` | VARCHAR2(1) | Estado del ítem |
| 14 | `F_CIERRE` | DATE | Fecha de cierre |
| 15 | `NCOLOR` | VARCHAR2(7) | Número de color |
| 16 | `NUM_PED_DEST` | NUMBER(8) | Pedido destino (transferencia) |
| 17 | `LOTE_SERV` | VARCHAR2(30) | Lote de servicio (maquila) |

---

### 3.11 COMPRAS / LOGÍSTICA — `ORDEN_DE_COMPRA` / `REQUISICION` / `ITEMREQ`

| Tabla | Filas | Descripción |
|-------|------:|-------------|
| `ORDEN_DE_COMPRA` | 31.529 | Órdenes de compra |
| `ITEMORD` | 76.851 | Ítems de órdenes de compra |
| `REQUISICION` | 42.060 | Requisiciones de compra |
| `ITEMREQ` | 107.388 | Ítems de requisiciones |
| `DESP_ITEMREQ` | 41.019 | Despacho de ítems de requisiciones |
| `CT_ITEMREQ` | 52.608 | Ítems de requisición (control) |
| `CT_REQVAR` | 20.165 | Variables de requisición |
| `CT_ARTREQ` | 56 | Artículos de requisición |
| `REQFLOCA` | 911 | Requisición de floca/fibra suelta |
| `PROVEED` | 8.389 | Maestro de proveedores |
| `PROVEED_RESP` | 177 | Responsables de proveedores |
| `PROVEED_CONCEPTO` | 739 | Conceptos de proveedores |
| `FACTPAG` | 211.446 | Facturas por pagar |
| `CABFPAG` | 227.534 | Cabecera de pagos |

---

### 3.12 PLANILLA / RRHH — Prefijo `PLA_` / `RH_`

| Tabla | Filas | Descripción |
|-------|------:|-------------|
| `PLANILLA` | 369.287 | Detalle de planilla (conceptos por empleado/mes) |
| `PLA_MENSUAL` | 378.839 | Planilla mensual consolidada |
| `PLA_COSTO` | 369.239 | Costo de planilla por CC |
| `INGRE_PLA` | 36.931.386 | Ingresos de planilla (tabla más grande del esquema) |
| `APORTA_PLA` | 5.580.503 | Aportes de planilla |
| `LIQUI_PLA` | 479 | Liquidaciones de planilla |
| `LIQUI_INGRE` | 2.284 | Liquidaciones de ingresos |
| `LIQUI_APORTA` | 2.241 | Liquidaciones de aportes |
| `HORAS_PLA` | 1.448 | Horas extra en planilla |
| `PLA_ANUAL` | 7.548 | Plan anual |
| `PLA_CTS` | 216 | CTS por trabajador |
| `PLA_CAL_CTS` | 10.060 | Cálculo de CTS |
| `PLA_UTILI` | 7.315 | Utilidades |
| `RH_PERSONAL` | 4.071 | Personal activo e inactivo |
| `RH_PERLAB` | 4.071 | Periodos laborales |
| `RH_PERSONAS` | 4.188 | Datos personales |
| `RH_CONTRATOS` | 9.884 | Contratos de trabajo |
| `RH_HISTPERS` | 27.610 | Historial de personal |
| `RH_HISTPERSDET` | 96.331 | Historial detallado de personal |
| `RH_HCARGOS` | 3.616 | Historial de cargos |
| `RH_VACACIONES` | 48.076 | Vacaciones del personal |
| `RH_ADELANTOS` | 52.627 | Adelantos de sueldo |
| `RH_EVALUACION` | 6.065 | Evaluaciones de personal |
| `RH_COMEDOR` | 74.573 | Registro de comedor |
| `RH_EVENTOS` | 32.339 | Eventos laborales (altas, bajas, etc.) |
| `RH_CONVENIO` | 1.358 | Convenios colectivos |
| `RH_DESTAQUE` | 3.065 | Destacamento de personal |
| `INGRE_FIJO` | 61.035 | Ingresos fijos (remuneraciones base) |

---

### 3.13 CONTABILIDAD — `MOVDETA` / `MOVGLOS` / `REGISTRO_DIARIO` / `PLANCTA`

| Tabla | Filas | Descripción |
|-------|------:|-------------|
| `MOVDETA` | 6.351.642 | Movimientos contables detallados |
| `MOVGLOS` | 569.840 | Glosas de movimientos |
| `MOVFIDE` | 1.130.701 | Movimientos fideicomiso |
| `MOVFIGL` | 200.064 | Glosas fideicomiso |
| `REGISTRO_DIARIO` | 92.533 | Registro diario contable |
| `PLANCTA` | 8.400 | Plan de cuentas |
| `PLANCTA_TMP` | 2.703 | Plan de cuentas temporal |
| `NEW_PLANCTA` | 2.323 | Nuevo plan de cuentas |
| `BALANCE` | 77 | Balance (estructura) |
| `CUENTA_CORRIENTE` | 1.417.801 | Cuenta corriente contable |
| `ICIERRE` | 20 | Cierre contable |
| `SALDOS_DIARIOS` | 16.216 | Saldos diarios |
| `NROLIBR` | 7.897 | Numeración de libros contables |

---

### 3.14 COBRANZAS / FINANZAS — `LETRAS` / `CABFCOB` / `CJ_`

| Tabla | Filas | Descripción |
|-------|------:|-------------|
| `LETRAS` | 95.249 | Letras de cambio |
| `CABFCOB` | 330.236 | Cobros (cabecera) |
| `FACTCOB` | 300.134 | Facturas cobradas |
| `CJ_PLACOBD` | 28.652 | Plan de cobros detalle |
| `CJ_PLACOBF` | 39.782 | Plan de cobros (facturas) |
| `CJ_PLACOBH` | 28.701 | Plan de cobros (cabecera) |
| `CJ_CONCILIA` | 113.467 | Conciliación bancaria |
| `CXC_CONCILIA` | 122.397 | Conciliación CxC |
| `ANTICIPO` | 2.906 | Anticipos de clientes |
| `CONDPAG` | 758 | Condiciones de pago |
| `CAMBDOL` | 22.888 | Tipo de cambio dólar |
| `CAMBMON` | 1.602 | Tipo de cambio otras monedas |
| `CTABNCO` | 136 | Cuentas bancarias |

---

### 3.15 ACTIVOS FIJOS — `ACTIVO_FIJO` / `ACTIMOV_`

| Tabla | Filas | Descripción |
|-------|------:|-------------|
| `ACTIVO_FIJO` | 1.821 | Registro de activos fijos |
| `ACTIMOV_D` | 107.583 | Movimientos de activos (detalle) |
| `ACTIMOV_G` | 186 | Movimientos de activos (cabecera) |
| `ACTIMOV_R` | 2 | Movimientos de activos (resumen) |
| `ACTIMOV_2013` | 2.807 | Movimientos activos 2013 |
| `ACTIMOV_D_TMP` | 925 | Movimientos temporales |
| `ACTIVO_HIPOTECA` | 32 | Activos hipotecados |
| `ACTIVO_IMPORTACION` | 89 | Activos importados |
| `ACTIVO_NUMDOC` | 7 | Numeración de documentos de activos |
| `ACTIVO_VENTA` | 114 | Activos vendidos |
| `ACTIVO_REVNIIF` | 0 | Revisión NIIF |
| `AF_CLASE` | 9 | Clases de activos fijos |
| `AF_TABLAS_AUXILIARES` | 148 | Tablas auxiliares activos |
| `AC_CONTROL` | 495 | Control de activos |
| `AC_GRUPO` | 21 | Grupos de activos |

---

### 3.16 CALIDAD / NO CONFORMIDADES — `CTCALIDAD_` / `NOCONFORMIDAD` / `HALLAZGO` / `RECLAMO`

| Tabla | Filas | Descripción |
|-------|------:|-------------|
| `CTCALIDAD_G` | 6.065 | Cabecera evaluación de calidad de partidas |
| `CTCALIDAD_D` | 140.019 | Detalle evaluación de calidad (tono, tela, CI, SF, SI) |
| `NOCONFORMIDAD_G` | 986 | Cabecera de no conformidades |
| `NOCONFORMIDAD_D` | 8.557 | Detalle de no conformidades |
| `HALLAZGO` | 673 | Hallazgos de calidad |
| `HALLAZGO_D` | 456 | Detalle de hallazgos |
| `HALLAZGO_ACCIONES` | 881 | Acciones correctivas de hallazgos |
| `HALLAZGO_CAUSA` | 480 | Causas de hallazgos |
| `RECLAMO_G` | 1.700 | Reclamos de clientes (cabecera) |
| `RECLAMO_D` | 2.296 | Reclamos (detalle) |
| `RECLAMO_DOC` | 2.654 | Documentos de reclamos |
| `RECLAMO_ACCIONES` | 372 | Acciones por reclamo |
| `RECLAMO_RESP` | 397 | Responsables de reclamos |
| `SEG_NCONFORME` | 225 | Seguimiento no conforme |
| `H_CCALIDAD` | 21.625 | Historial de control de calidad |
| `H_EVALUA_CCALIDAD` | 49 | Historial evaluación CC |

---

### 3.17 TINTORERÍA / COLORIMETRÍA — `H_COLOR` / `H_EQUIVALENCIA` / `H_RECETA`

| Tabla | Filas | Descripción |
|-------|------:|-------------|
| `H_EQUIVALENCIA` | 5.495.708 | Equivalencias de colorimetría (mayor tabla colorimétrica) |
| `EQUIVALENCIA` | 72.611 | Equivalencias de colorimetría base |
| `H_COLOR` | 12.578 | Historial de colores |
| `H_COLORCLI` | 13.236 | Colores por cliente |
| `H_COLOR_TINTO` | 36.964 | Colores de tintorería |
| `H_COLOR_TINTO_ORI` | 3.439 | Colores de tintorería (original) |
| `CTPRODU_TINTO` | 266.705 | Producción de tintorería |
| `CTPROTIN` | 40.487 | Control producción tintorería |
| `H_TINTO_PRODUCD` | 84 | Producción tintorería detalle |
| `H_TINTO_PRODUCG` | 88 | Producción tintorería cabecera |
| `H_PROCESO_TENIDO_G` | 4 | Proceso tenido cabecera |
| `H_PROCESO_TENIDO_D` | 109 | Proceso tenido detalle |
| `H_PROCESO_TENIDO_L` | 794 | Proceso tenido lote |
| `CTRUTAS_TITULO_COLOR` | 19.590 | Ruta título-color (receta y costo de tintorería) |
| `CTRUTAS_TITULO_COLOR_D` | 341.777 | Detalle de la receta de tintorería por ruta |
| `LA_TRICOMIA` | 755 | Tricomías del laboratorio |
| `L_TRICOMIA` | 63 | Tricomías de planeamiento |
| `CC_TITULACION_D` | 261.480 | Titulación CC (detalle) |
| `CC_TITULACION_G` | 52.071 | Titulación CC (cabecera) |

---

### 3.18 MUESTRAS — `MUESTRA` / `ITEMUEST` / `ARTMUEST`

| Tabla | Filas | Descripción |
|-------|------:|-------------|
| `MUESTRA` | 17.479 | Cabecera de muestras a clientes |
| `ITEMUEST` | 55.426 | Ítems de muestras |
| `ARTMUEST` | 30.295 | Artículos en muestras |
| `AUXMUEST` | 376 | Datos auxiliares de muestras |
| `OPCMUEST` | 109.029 | Opciones de muestras |
| `ILUMUEST` | 15.053 | Iluminación de muestras |

---

### 3.19 SISTEMA / SEGURIDAD — `CS_` / `ACC_` / `SI_`

| Tabla | Filas | Descripción |
|-------|------:|-------------|
| `CS_USER` | 85 | Usuarios del sistema |
| `CS_ROL` | 2 | Roles del sistema |
| `ACC_USER_ROL` | 209 | Asignación usuario-rol |
| `ACC_X_FORM_ROL` | 1 | Acceso a formularios por rol |
| `CS_ACCESO` | 79 | Accesos a módulos |
| `CS_APP` | 3 | Aplicaciones |
| `CS_COMPROBANTE` | 76 | Comprobantes del sistema |
| `CS_COMPUTO` | 1.851 | Registro de cómputo/inventario IT |
| `CS_SOPCOMP` | 20.044 | Soporte de cómputo (incidencias IT) |
| `CS_INCIDENCIA` | 6 | Incidencias |
| `CS_LICENCIA` | 18 | Licencias de software |
| `CS_MANTENIMIENTO` | 10 | Mantenimiento de sistemas |
| `CS_TABLAS` | 591 | Tablas de configuración del sistema |
| `CS_AVISO_EMAIL` | 51.904 | Avisos por correo electrónico |
| `CS_DETCOMP` | 27.749 | Detalle de cómputo |
| `CS_PLACA` | 25 | Placas de vehículos IT |
| `SI_REGPERS` | 521.659 | Registro de ingresos/salidas del personal |
| `SI_RECOMENDACION` | 2.105 | Recomendaciones de seguridad industrial |
| `SI_RECOMIENDA` | 371 | Recomendaciones |
| `SI_RECOMIENDA_REL` | 4.676 | Relaciones de recomendaciones |
| `SI_EPPE` | 230 | EPP asignado |
| `SI_INSPECCION` | 4 | Inspecciones de seguridad |
| `USER_AUTHORIZED` | 118 | Usuarios autorizados |

---

### 3.20 PARÁMETROS GENERALES

| Tabla | Filas | Descripción |
|-------|------:|-------------|
| `PARAMAC` | 1 | Parámetros de activos corrientes |
| `PARAMAJU` | 111 | Parámetros de ajuste |
| `PARAMCC` | 1 | Parámetros centro de costos |
| `PARAMCO` | 1 | Parámetros de cobranza |
| `PARAMCOS` | 1 | Parámetros de costos |
| `PARAMCP` | 1 | Parámetros de cuentas por pagar |
| `PARAMCTS` | 79 | Parámetros CTS |
| `PARAMDOC` | 36 | Parámetros de documentos |
| `PARAMFA` | 1 | Parámetros de facturación |
| `PARAMFI` | 1 | Parámetros financieros |
| `PARAMIN` | 1 | Parámetros de inventario |
| `PARAMLG` | 1 | Parámetros de logística |
| `PARAMPLA` | 4.593 | Parámetros de planeamiento |
| `PARAMPRD` | 1 | Parámetros de producción |
| `PARAMRRHH` | 1 | Parámetros RRHH |
| `PARAMSIS` | 1 | Parámetros del sistema |
| `CS_TABLAS` | 591 | Tabla de valores del sistema |
| `TABLAS_AUXILIARES` | 3.494 | Tablas auxiliares generales |

---

### 3.21 COSTOS DE PRODUCCIÓN — `SC_` / `CTCOSMAQ` / `CTTABCOS`

| Tabla | Filas | Descripción |
|-------|------:|-------------|
| `SC_COMPANIA` | 1 | Datos de la compañía (costo estándar) |
| `SC_GRUPOS_PROCESOS` | 9 | Grupos de procesos para costeo |
| `SC_TIPO_COSTO` | 8 | Tipos de costo |
| `SC_TIPO_MAQUINA` | 23 | Tipos de máquina (costeo) |
| `SC_CENTROCOSTO_AREAPROCESO` | 24 | CC vs área de proceso |
| `SC_DEPRECIACION` | 0 | Depreciación estándar |
| `CTTABCOS` | 36 | Tabla de costos de producción |
| `CTCOSMAQ` | 39 | Costos por máquina |
| `CTCOSMAQ_BAK` | 25 | Backup costos máquina |
| `DCOSTO_ART` | 223 | Distribución de costos por artículo |
| `DIST_COSTO` | 12 | Distribución de costos |
| `FCOSTO_ART` | 62 | Factor de costo por artículo |
| `HISTART_COSTO` | 3.219.138 | Historial de costos de artículos |
| `CTARTI_COS` | 45.888 | Costo estándar por artículo |
| `COSTOS_SUNAT` | 22.191 | Costos reportados a SUNAT |

---

### 3.22 TABLAS DE MAYOR VOLUMEN

| Tabla | Filas | Módulo |
|-------|------:|--------|
| `INGRE_PLA` | 36.931.386 | Planilla |
| `HISTART` | 8.799.533 | Inventario |
| `ING_RECETAS_D` | 4.794.953 | Recetas |
| `KARDEX_D` | 4.033.904 | Almacén |
| `KARDEX_L` | 3.177.815 | Almacén |
| `HISTART_COSTO` | 3.219.138 | Costos |
| `APORTA_PLA` | 5.580.503 | Planilla |
| `H_EQUIVALENCIA` | 5.495.708 | Colorimetría |
| `II_DESPQUI_D` | 1.901.694 | Recetas/Químicos |
| `LOTES` | 1.604.169 | Inventario |
| `INV_FISICO` | 1.518.611 | Inventario físico |
| `CUENTA_CORRIENTE` | 1.417.801 | Contabilidad |
| `ART_LOTE` | 1.346.474 | Inventario |
| `DLOTES` | 1.257.960 | Lotes |
| `CT_RECPART` | 1.020.783 | Recetas partida |
| `H_PRODUCCION_D` | 1.263.670 | Producción |
| `MOVDETA` | 6.351.642 | Contabilidad |
| `H_PRODUCCION_G` | 933.269 | Producción |

---

## 4. OBJETOS DE BASE DE DATOS

### 4.1 PAQUETES (PACKAGES)

| Paquete | Estado | Último DDL | Descripción |
|---------|--------|-----------|-------------|
| `CB_PAC_RUTINAS` | VALID | 14/05/26 | Rutinas de cobranza/caja |
| `CJ_PAC_RUTINAS` | VALID | 14/05/26 | Rutinas de caja/tesorería |
| `CT_PAC_RUTINAS` | VALID | 14/05/26 | Rutinas de costos/tintorería |
| `HH_PAC_RUTINAS` | VALID | 14/05/26 | Rutinas de hilandería |
| `II_PAC_RUTINAS` | VALID | 14/05/26 | Rutinas de insumos/recetas |
| `LG_PAC_RUTINAS` | VALID | 14/05/26 | Rutinas de logística |
| `PAK_MOVILES` | VALID | 14/05/26 | Rutinas de app móvil |
| `PED_PAC_RUTINAS` | VALID | 14/05/26 | Rutinas de pedidos |
| `PKG_COBRANZA` | VALID | 14/05/26 | Proceso de cobranza (KPI/reportes) |
| `PKG_IND_LOGISTICA` | VALID | 16/05/26 | Indicadores de logística |
| `PKG_PROD_RUTINAS` | VALID | 14/05/26 | Rutinas de producción |
| `PKG_REG_ORDEN_COMPRA` | VALID | 14/05/26 | Registro de órdenes de compra |
| `PKG_VEND_GRUPO_MAESTROCLIENTE` | VALID | 14/05/26 | Vendedor/grupo/cliente maestro |
| `RH_PAC_RUTINAS` | VALID | 14/05/26 | Rutinas de RRHH/planilla |
| `SCLIMPLE` | VALID | 14/05/26 | Impresión de documentos (simple) |
| `SCLIMPLE_CJ` | VALID | 14/05/26 | Impresión de documentos caja |
| `VV_PAC_RUTINAS` | VALID | 14/05/26 | Rutinas de ventas |

### 4.2 PROCEDIMIENTOS ALMACENADOS

| Procedimiento | Estado | Descripción |
|--------------|--------|-------------|
| `ABRE_PLANILLA` | VALID | Abre período de planilla |
| `ACTUALIZA_PEDIDO` | VALID | Actualiza estado de pedido |
| `ANULA_PEDIDO_TOTAL` | VALID | Anulación total de pedido |
| `ARMA_FECHA_ENTREGA` | VALID | Calcula fechas de entrega |
| `ARMA_FECHA_ENTREGA_VALIDA` | VALID | Valida fechas de entrega |
| `ARMA_ITEMPED_ALMACEN` | VALID | Asocia ítems de pedido a almacén |
| `ARMA_VACACIONES` | VALID | Genera cálculo de vacaciones |
| `CALCULO_PLA` | VALID | Cálculo de planilla |
| `CALCULO_QUINCENA` | VALID | Cálculo de quincena |
| `CARGA_CUENTAS_X_PAGAR` | VALID | Carga CxP |
| `CIERRA_PEDIDO_TOTAL` | VALID | Cierre total de pedido |
| `CIERRA_PLANILLA` | VALID | Cierre de planilla |
| `COPIA_LIBRO` | VALID | Copia libro contable |
| `CREA_FACTORES` | VALID | Crea factores de cálculo |
| `CREA_SALDOS` | VALID | Genera saldos |
| `CREA_SALDOS_CAJA` | VALID | Genera saldos de caja |
| `CREA_SALDOS_MOV` | VALID | Genera saldos de movimientos |
| `DATOS_5TA` | VALID | Cálculo 5ta categoría RRHH |
| `DATOS_EPS` | VALID | Datos EPS (seguro médico) |
| `DESHABILITA_PLA` | VALID | Deshabilita planilla |
| `DIF_DOL_MOVDETA` | VALID | Diferencia de cambio en movimientos |
| `EJECUTA_PROCESOS` | VALID | Ejecuta procesos en lote |
| `ELEGIBLE_TSERVICIO` | VALID | Verifica elegibilidad tiempo de servicio |
| `ENVIAR_CORREO` | VALID | Envío de correo |
| `FACTURA_AUTOM` | VALID | Facturación automática |
| `GENERA_ASIENTO_VTA` | VALID | Genera asiento contable de ventas |
| `GENERA_BIZLINKS_GRV001` | VALID | Genera para sistema Bizlinks |
| `GENERA_CPE_BLZ` | VALID | Genera CPE para Bizlinks (factura electrónica) |
| `GENERA_GUIA_RETORNO` | VALID | Genera guía de retorno |
| `GENERA_HIST_PROGTINTO` | VALID | Genera historial de programación tintorería |
| `GENERA_REQ_FACTURACION` | VALID | Genera requerimiento de facturación |
| `GENERA_RETENCION_BLZ` | VALID | Genera retención Bizlinks |
| `GENERA_RVIE` | VALID | Genera RVIE (libro electrónico) |
| `GENERA_V21_CPE` | VALID | Genera CPE versión 2.1 |
| `HABI_CAJA` | VALID | Habilita caja |
| `HABI_CONTAB` | VALID | Habilita contabilidad |
| `HABI_PLANILLA` | VALID | Habilita planilla |
| `INTERFACE_ASISTENCIA` | VALID | Interfaz con sistema de asistencia |
| `INTERFACE_ASISTENCIA_EMP` | VALID | Interfaz asistencia empresa |
| `OPERACION_BANCARIA_CXC` | VALID | Operación bancaria CxC |
| `OPERACION_BANCARIA_CXP` | VALID | Operación bancaria CxP |
| `PA_SEG` | VALID | Proceso de seguridad |
| `POST_TO_API` | **INVALID** | Post a API (código legado) |
| `RCOMPRAS_SEEN` | VALID | Reporte de compras |
| `REGISTRA_LOGIN` | **INVALID** | Registro de login (código legado) |
| `REGISTRA_PESO_CERO` | VALID | Registra pesos en cero |
| `SP_EDITAR_USUARIO` | VALID | Editar usuario |
| `SP_ELIMINAR_USUARIO` | VALID | Eliminar usuario |
| `SP_REGISTRAR_TIPOCAMBIO` | VALID | Registrar tipo de cambio |
| `SP_REGISTRAR_USUARIO` | VALID | Registrar usuario |
| `SP_SI_CONSULTA_TRAB` | VALID | Consulta trabajador (seguridad industrial) |
| `SP_SI_REGISTRAR_INGRESO` | VALID | Registrar ingreso SI |
| `SP_SI_REGISTRAR_INGRESO2` | VALID | Registrar ingreso SI v2 |

> ⚠️ **INVALID**: `POST_TO_API` y `REGISTRA_LOGIN` están en estado INVALID. Código legado preexistente — no referenciados por ningún módulo activo, incluyendo PLN_.

### 4.3 FUNCIONES

| Función | Descripción |
|---------|-------------|
| `ACABADO_MADEJA` | Estado de acabado de madeja |
| `ARTICULO_VTA_A_PROD` | Artículo de venta a producción |
| `ARTICULO_X_RECETA` | Artículo por receta |
| `ASESORCOMERCIAL` | Asesor comercial del cliente |
| `BUSCA_HALLAZGO_PEDIDO` | Hallazgos de un pedido |
| `BUSCA_RECLAMO_PEDIDO` | Reclamos de un pedido |
| `BUSCA_TIPO_DE_FIBRA` | Tipo de fibra de un artículo |
| `CARGO_ANTERIOR_A_FECHA` | Cargo previo a una fecha |
| `CCOSTO_ANTERIOR_A_FECHA` | CC previo a una fecha |
| `CLIENTE` | Nombre del cliente |
| `CODIGO_ORIGEN` | Código de origen del artículo |
| `DAY_A_DIA` / `DAY_A_DIA_LARGO` | Nombre del día (en/es) |
| `DESCRIPCION_MATERIAL` | Descripción de material (variantes 1-3, ATLAS) |
| `DESVSTD_DOS_ANIOS` | Desviación estándar 2 años |
| `DNI_TRABAJADOR` | DNI de un trabajador |
| `ESTADO_CPE_BZL` | Estado de CPE en Bizlinks |
| `ESTATUS_UBICACION_PARTIDA` | Ubicación de partida en almacén |
| `ESTATUS_UBICACION_PEDIDO` | Estado de pedido |
| `ES_NUMERO` | Valida si es número |
| `ES_PARTIDA` | Valida si es partida |
| `ES_REINGRESANTE` | Trabajador reingresante |
| `FBANCOS` | Información de bancos |
| `FCH_ENTREGA_PED` | Fecha de entrega de pedido |
| `FIBRA_100ALGODON` | Artículo es 100% algodón |
| `FIBRA_ORG` / `FIBRA_ORG_GOTS` / `FIBRA_ORG_OCS` | Validaciones de fibra orgánica |
| `FN_RELFACT_NCONF` | Relación factura-no conformidad |
| `FT_OBT_BASE_VAC` | Base de vacaciones |
| `GET_FCIEN` / `GET_FMIL` / `GET_FNUMLET` | Conversión número a letras |
| `HORA_A_NUMERO` / `NUMERO_A_HORA` | Conversión hora |
| `INGRESOS_CONTRATO` | Ingresos del contrato |
| `INTENSIDAD_RECETA` | Intensidad de la receta de color |
| `KG_PARTIDA_ING_ALMTER` | Kg de partida en almacén terminal |
| `KG_PEDIDO_DESPCH_ALMTER` | Kg despachados al almacén terminal |
| `KG_RECETA_ING_ALMTER` | Kg de receta ingresados |
| `LOTE_ALMPT_FACTURA` | Lotes facturados |
| `LUGARENTREGA_PEDIDO` | Lugar de entrega del pedido |
| `MATERIAL_COTIZACION` | Material de cotización |
| `MATERIAL_LOTE_HILANDERIA` | Material del lote en hilandería |
| `MATERIAL_PEDIDO_ABREVIADO` | Descripción abreviada de material del pedido |
| `NOMBRE_TRABAJADOR` | Nombre completo del trabajador |
| `OBTENER_CV_USTER` | CV de Uster (calidad de hilo) |
| `OBTIENE_EMAIL_CLIENTE` | Email del cliente |
| `OBTIENE_PEDIDO_ANTICIPO` | Pedido de anticipo |
| `PARTIDAS_DE_RECETA` | Partidas asociadas a una receta |
| `PARTIDAS_HALLAZGO` | Partidas con hallazgos |
| `PARTIDA_CON_ACOL` | Partida con acabado de color |
| `PARTIDA_CON_MAPL` | Partida con mapl |
| `PARTIDA_LABORATORISTA` | Laboratorista de la partida |
| `PEDIDO_CON_SOLICITUD` | Pedido con solicitud de muestra |
| `PORC_FIBRA_TENIDA_FT` | % fibra tenida |
| `PRIMERA_FECHA_INGRESO` | Primera fecha de ingreso del trabajador |
| `PRODUC_TEORICA_HILANDERIA` | Producción teórica hilandería |
| `PRODUC_TEORICA_PREPARATORIA` | Producción teórica preparatoria |
| `PROVEEDOR` | Nombre del proveedor |
| `PR_AM` / `PR_ANUMES` | Funciones de fecha |
| `PR_RECETA_KGR` | Kg de receta |
| `PR_SALDO_INI` | Saldo inicial |
| `PR_ULT_COMPRA` / `PR_ULT_COMPRA_S` | Última compra |
| `PR_VOL_AGUA` | Volumen de agua |
| `RECETA_ORGANICA` | Receta orgánica certificada |
| `REPRODUCIDAD_PARTIDA` / `REPRODUCIDAD_PEDIDO` | Reproducibilidad |
| `SALDO_INTERMEDIO_LOTE_ALM` | Saldo intermedio |
| `SOLICITUD_CON_PEDIDO` | Solicitud de muestra con pedido |
| `SP_VALIDA_USUARIO` | Valida usuario |
| `SUM_HORAS` / `SUM_RHORAS` | Suma de horas |
| `TIEMPO_CONTRATO` | Tiempo de contrato |
| `TIEMPO_TENIDO_TEORICO` | Tiempo teórico de tenido |
| `TIENE_RECETA` / `TIENE_TENIDO` / `TIENE_FICHA_TECNICA` | Indicadores de estado |
| `TRABAJADOR` / `TRABAJADOR_PRODUCCION` | Datos del trabajador |
| `T_TIEMPO` / `Z_HORAS` / `Z2_HORAS` | Funciones de tiempo |

### 4.4 VISTAS

| Vista | Descripción |
|-------|-------------|
| `ANTICIPOS_V` | Anticipos de clientes |
| `BASE_COMISIONES` | Base de cálculo de comisiones |
| `HORAS_MAQ_XDIAS` / `HORAS_MAQ_XDIASB` | Horas de máquina por día |
| `HORAS_PARADAS` / `HORAS_PARADAS_A` | Horas de parada |
| `H_PRODUCCIONAD_V` / `H_PRODUCCIOND_V` / `H_PRODUCCIONC_V` | Producción detallada |
| `H_PRODUCCIOND_GASEADO` / `_MADEJERA` / `_REDINA` | Producción por tipo |
| `H_TINTO_PRODUCV` | Producción tintorería |
| `H_ROLLOS_V` | Producción de rollos |
| `PLANM_COSTO` | Plan mensual de costos |
| `V_PLA_PARAM_MAQUINA_D` / `_PROCESO_D` / `_TITULO_D` | Parámetros de planeamiento |
| `V_ANALIZA_PARTIDA` | Análisis de partida |
| `V_ARTICUL` | Vista completa de artículos |
| `V_CAMBIOTIT` | Cambio de título |
| `V_CANTXEDAD` | Cantidad por edad (RRHH) |
| `V_CAPACITACION` | Capacitaciones |
| `V_CENTRO_DE_COSTOS` | Centros de costos |
| `V_CLIENTES` | Clientes |
| `V_COLOR_HIL` / `V_COLOR_VTA` / `V_COLOR_CRMEHE` | Colores |
| `V_CONTRATO` | Contratos laborales |
| `V_COTIZACION_SIN_PEDIDO` | Cotizaciones sin pedido |
| `V_CTRUTAS` / `V2_CTRUTAS` | Rutas de producción |
| `V_DATOS` | Datos generales |
| `V_DB_COMPARA_VTAS_ANIOS` / `_ASESOR` / `_LINEA` | Comparativos de ventas |
| `V_DOCUVEN` / `V_DOCUVEN_2` | Documentos de venta |
| `V_EVALUA_CCALIDAD` | Evaluación de calidad |
| `V_FACTCOB` / `V_FACTPAG` | Facturas cobradas/pagadas |
| `V_FAMILIA` | Familias de artículos |
| `V_HALLAZGO` | Hallazgos |
| `V_HORASEXTRAS` | Horas extras |
| `V_INDICADOR_SISTEMAS` | Indicadores de sistemas |
| `V_ITEMDOC` / `V_ITEMPED` / `V_ITEMPEDET` | Ítems |
| `V_KARDEXD_PED` / `V_KARDEXL` | Kardex |
| `V_MAQUINA` | Máquinas |
| `V_PACKING` | Packing |
| `V_PARTIDA` | Partidas |
| `V_PEDIDOS` / `V_PEDIDOS_X_MUESTRA` | Pedidos |
| `V_PENDXDESTVTA` | Pendientes por destino de venta |
| `V_PERSONAL` / `PERSONAL` | Personal |
| `V_PLANILLA` | Planilla |
| `V_PLA_COSTO` | Plan de costos |
| `V_PVT_ALMREP_CONS_CANT` / `_COSTO` | Almacén repuesto consumo |
| `V_PVT_TITULACION` | Titulación pivote |
| `V_RECETA` / `V_RECETAHIL` / `V_RECETAPARTIDA` | Recetas |
| `V_RECLAMO` | Reclamos |
| `V_RELACION` | Relación de movimientos |
| `V_RETENCIONES` / `V_RETENIGV` | Retenciones |
| `V_RPRODUC` / `V_RSECADO` | Producción resumida |
| `V_SALPED` | Saldo de pedidos |
| `V_SEGUIMIENTO_HILANDERIA` | Seguimiento en hilandería |
| `V_STATUS_CCAL_TINTO` | Estado control calidad tintorería |
| `V_STATUS_PARTIDA` / `V_STATUS_PARTIDA2` | Estado de partidas |
| `V_STATUS_PEDIDO` | Estado de pedidos |
| `V_STOCK_MARKET` | Stock mercado |
| `V_TRICOMIA_ORDEN3` | Tricomía orden 3 |
| `V_VCMTO_PARTEMAQ` | Vencimiento de partes de máquina |
| `VCC_CONTROLPROD` / `VCC_GASTOSCONT` / `VCC_GASTOSINV` / `VCC_GASTOSPLANI` | Control de costos CC |
| `VCC_PARTIDA` | Partidas CC |
| `VW_FACTURACION_DOLARES` / `VW_FACTURACION_SOLES` | Facturación por moneda |
| `V_DRAW` | Vista de Draw (hilo bobinado) |
| `VSALDOS` / `VSALDOSC` | Saldos |
| `VPLAFIJO` / `VPLANI` | Planilla fija |
| `VENXVEN` | Ventas por vendedor |
| `SEGUIMIENTO` | Seguimiento de muestras |

### 4.5 SECUENCIAS

| Secuencia | Descripción |
|-----------|-------------|
| `AF_CONTPREN` | Activos fijos - numeración de prendas |
| `ANUALC` | Numeración anual contable |
| `BLOBID` | IDs de blobs |
| `CC_SEQCAMBIONE` | Cambios de NE (control calidad) |
| `COMPUTO` | Numeración de cómputo IT |
| `CONTPLA` | Control planilla |
| `CS_CONTREQ` | Requerimientos CS |
| `CS_SEQINCIDEN` | Incidencias CS |
| `CUSTID` | IDs de clientes |
| `EVT_NOTIFY_SEQ` / `EVT_OPERATORS_SEQ` / `EVT_PROFILE_SEQ` | Eventos notificaciones |
| `H_CONTRECHIL` / `H_CONTRECHILM` | Hilandería - control recetas |
| `H_RANGO_ACEPTA` | Rango de aceptación |
| `H_SEQCONO_UNO` | Cono uno |
| `INV_REPSUM` | Resumen de inventario |
| `LA_CONTRECTREC` | Tricomía laboratorio |
| `LA_CONTTRICOM` | Tricomía |
| `LG_GRUPO_SEQ` | Grupos logística |
| `L_CONTTRICOM` / `L_SEQTRICOM` | Tricomías planeamiento |
| `MA_CONTREQ` | Requerimientos mantenimiento |
| `MA_PROGMANT` | Programas de mantenimiento |
| `ORDID` | IDs de órdenes |
| `PCP_SEQPROGPREP` | Programación preparatoria |
| `PRODID` | IDs de productos |
| `PUESTO` | Numeración de puestos |
| `RH_CONTCAPAC` / `RH_CONTEVAL` / `RH_CONTEVALN` | Capacitaciones/evaluaciones |
| `RH_CONTPROGV` / `RH_CONTVAC` | Programas/vacaciones RRHH |
| `SEQ_REQ_CERT` | Requisición certificada |
| `SEQ_SEG` | Seguridad industrial |
| `SI_CONTEPP` / `SI_CONTINCID` / `SI_CONTRECOMSEG` / `SI_CONTREQ` | Seguridad industrial |
| `VT_CONTCOTIZA` / `VT_CONTMARK` / `VT_CONTVISITA` | Ventas |

### 4.6 TRIGGERS (resumen por módulo)

El esquema cuenta con ~400 triggers. Convención de nombres:
- **`TIA_`** — INSTEAD OF / INSERT/UPDATE mixto (lógica de negocio)
- **`TIB_`** — BEFORE INSERT/UPDATE/DELETE (validaciones y auditoría)
- **`TDA_`** — AFTER DELETE (limpieza relacionada)
- **`TDB_`** — BEFORE DELETE
- Otros: `CONTRATOS`, `DIRECCIONES`, `HISTORIA_ICIERRE`, `PERIODOS` — triggers de mantenimiento

**Triggers de auditoría AUD** (patrón `TIB_xxx_AUD`):
Registran el usuario y fecha en campos `A_ADUSER`, `A_ADFECHA`, `A_MDUSER`, `A_MDFECHA` en todas las tablas transaccionales. Presentes en: `ACTIMOV_D`, `ANTICIPO`, `CABFPAG`, `CAMBDOL`, `CAMBMON`, `CLIENTES`, `CONVENIO`, `DOCUVENT`, `FACTCOB`, `FACTPAG`, `HISTPERS`, `INGRE_FIJO`, `ITEMDOCU`, `ITEMPED`, `KARDEX_D/G/L`, `LETRAS`, `LOTES`, `MOVDETA`, `PARTIDA`, `PEDIDO`, `PLANILLA`, `PROVEED`, `REDIARIO`, `REQUISICION`, y muchos más.

**Triggers de integración con AQUARIUS**:
- `TIA_RH_DESTAQUE_AQUARIUS` — Sincroniza destacamento de personal
- `TIA_RH_EVENTOS_AQUARIUS` — Sincroniza eventos laborales
- `TIA_RH_PERLAB_AQUARIUS` — Sincroniza periodos laborales
- `TIA_RH_PERSONAL_AQUARIUS` — Sincroniza datos de personal
- `TIA_TCCOSTO_AQUARIUS` — Sincroniza centros de costos

---

# FLUJO COMPLETO: TOMA DE PEDIDO → DESPACHO

> Análisis detallado del ciclo de vida de un pedido en el sistema SIG.
> Cada paso indica: tabla(s) afectada, trigger(s) activo(s), objeto de negocio y lógica de datos.

---

## VISIÓN GENERAL (Diagrama dual-path — actualizado con análisis de datos)

> Cada ítem de pedido sigue **dos rutas paralelas** que convergen en la PARTIDA.
> `ITEMPED_DET.NROPROG` = `PARTIDA.NROPROG` es el punto de fusión (1:1).

```
[CLIENTE] ──► [PEDIDO] ──► [ITEMPED] ──► [ITEMPED_DET] ──► NROPROG (clave puente)
                                              │                    │
                                              │ LOTE               │ 1:1
                                              │                    ▼
       ┌──────────────────[PATH A: HILANDERÍA]│──────────────────────────────────────┐
       │                                      │                                      │
       │   [H_RECETA_G]  (receta de hilatura  │  LOTE)                               │
       │        │                             │                                      │
       │        ▼                             │                                      │
       │   [H_RPRODUC]  (producción real por máquina y etapa)                        │
       │        TP_MAQ: B=BATAN | L=CARDA | M=MANUAR | P=PABILERA | C=CONTINUA      │
       │        Máquinas: BAT01/02, SS21-30, PEIN01-04, MA01-10, PAB01-06...         │
       └──────────────────────────────────────────────────────────────────────────────┘
                                              │
                                              │ (LOTE produce el hilo crudo)
                                              ▼
       ┌──────────────────[PATH B: TINTORERÍA]────────────────────────────────────────┐
       │                                                                              │
       │   [PARTIDA]  (lote físico — NROPROG = ITEMPED_DET.NROPROG)                  │
       │        │                                                                     │
       │        ├──► [PARTIDA_MAS] ──► [ING_RECETAS_G/D] (uno o más baños)           │
       │        │         │                    │                                      │
       │        │         │                    ▼                                      │
       │        │         │           [TT_RPRODUC]  (corrida real: TE/BQM/GAS)       │
       │        │         │           Máquinas: R01-R19 (Thies), M01-M08 (Hank)      │
       │        │         └──► [RECETA_G] (receta maestra cliente/color/título)       │
       │        │                                                                     │
       │        ├──► [TT_RSECADO]  (GUIA = PARTIDA.NUMERO)                           │
       │        │                                                                     │
       │        ├──► [CTCALIDAD_D]  (NRO_PEDIDO + SER_PARTIDA — control de color)    │
       │        │         EST_EVALUACION: '13'=Pend | '02'=Reval | '32'=Aprobado     │
       │        │                                                                     │
       │        ├──► [H_PROGRAMACION] (GUIA = PARTIDA.NUMERO — devanado madeja→cono) │
       │        │                                                                     │
       │        ├──► [REVISADO_G/D]  (GUIA = PARTIDA.NUMERO — peso y calidad final)  │
       │        │                                                                     │
       │        ├──► [LOTES]  (COD_ALM='03'/'07', TP_TRANSAC='16', PARTIDA=NUMERO)   │
       │        │                                                                     │
       │        └──► [KARDEX_G/D/L]  (TP='16'=ingreso PT | TP='22'/'23'=despacho)   │
       │                                                                              │
       └──────────────────────────────────────────────────────────────────────────────┘
                                              │
                                              ▼
                             [ALMACEN]  (stock por artículo/almacén)
                                              │
                                              ▼
                          [DESPACHO_GUIA]  (guía de remisión al cliente)
```

### Tabla SEGUIMIENTO — Tracking de eventos existente

```
SEGUIMIENTO (NUM_PED, NRO, PARTICION, FECHA, AREA, ACCION)
```

| AREA | Descripción |
|------|-------------|
| `'PLANEAMIENTO'` | "PROGRAMADO XX" — al crear ITEMPED_DET |
| `'CCALIDAD'` | Evaluación de control de calidad |
| `'PROG HILAND'` | Programación de hilandería post-tintorería |
| `'REVISADO'` | Revisión y aprobación final de conos |

### V_STATUS_PEDIDO — Pipeline de 9 etapas (vista existente)

| Etapa | Nombre | Tabla fuente | Condición |
|-------|--------|--------------|-----------|
| 1 | LABORATORIO | `L_VALIDA_RECETA` | registro existe |
| 2 | RECETA | `V_RECETAPARTIDA` / `RECETA_G` | receta validada |
| 3 | TINTORERIA | `TT_RPRODUC` | ESTADO IN ('1','3') |
| 4 | SECADORA | `TT_RSECADO` | ESTADO IN ('1','3') |
| 5 | CCAL-TINTO | `CTCALIDAD_D` | EST_EVALUACION IN ('13','02','32') |
| 6 | DEVANADO | `H_PROGRAMACION` | ESTADO IN ('3','6'), GUIA=PARTIDA.NUMERO |
| 7 | REVISADO | `REVISADO_G/D` | APROBADO > 0 |
| 8 | ALMACEN-PT | `LOTES` | COD_ALM IN ('03','07'), TP='16', PARTIDA≠NULL |
| 9 | DESPACHO | `KARDEX_L` | S_TRANSAC IN ('21','23') |

---

## PASO 1 — TOMA DEL PEDIDO

### Tablas involucradas
| Tabla | Rol |
|-------|-----|
| `CLIENTES` | Maestro del cliente (validación FK) |
| `PEDIDO` | Cabecera del pedido |
| `ITEMPED` | Líneas del pedido (ítems) |
| `ARTICUL` | Catálogo de artículos (FK de `ITEMPED.COD_ART`) |
| `H_PROCESOS` | Catálogo de procesos productivos (FK) |
| `H_TITULOS` | Catálogo de títulos de hilo (FK) |
| `H_FIBRA` | Catálogo de fibras (FK) |

### Tabla PEDIDO — Estructura completa

| Campo | Tipo | Descripción |
|-------|------|-------------|
| `SERIE` | NUMBER (PK) | Serie del pedido |
| `NUM_PED` | NUMBER (PK) | Número del pedido |
| `TIPO_DOCTO` | VARCHAR2(1) | Tipo de documento (`P`=Pedido, `C`=Cotización, etc.) |
| `ESTADO` | VARCHAR2(1) | **Estado del pedido** (ver tabla de estados) |
| `FECHA` | DATE | Fecha de emisión |
| `COD_CLIENTE` | VARCHAR2(15) → FK CLIENTES | Cliente |
| `COD_VENDE` | VARCHAR2(4) | Código vendedor |
| `MONEDA` | VARCHAR2(1) | `S`=Soles, `D`=Dólares |
| `PLAZO_ENTREGA` | NUMBER | Plazo de entrega en días |
| `TOTAL_PEDIDO` | NUMBER | Total del pedido |
| `TOTAL_FACTURADO` | NUMBER | Total ya facturado |
| `RUTAS` | VARCHAR2(1) | `S`/`N` — si tiene rutas de producción definidas |
| `TEJIDO` | VARCHAR2(1) | `1`=tejido, `0`=no |
| `THIOTAN` | VARCHAR2(1) | Indicador de tratamiento especial |
| `F_APROBACION` | DATE | Fecha de aprobación del pedido |
| `REVISADO` | VARCHAR2(1) | Revisión completada |
| `VALIDADO` | VARCHAR2(1) | Pedido validado |
| `VISADO` | VARCHAR2(1) | Pedido visado |
| `PRIORIDAD` | NUMBER | Prioridad de atención (mayor = más urgente) |
| `TIPOPED` | VARCHAR2(1) | Tipo de pedido |
| `A_ADUSER` / `A_ADFECHA` | VARCHAR2/DATE | Auditoría INSERT |
| `A_MDUSER` / `A_MDFECHA` | VARCHAR2/DATE | Auditoría UPDATE |
| `A_USAPROB` | VARCHAR2(15) | Usuario aprobador |

#### Estados de PEDIDO / ITEMPED (datos reales)

| ESTADO | Significado | Pedidos | Ítems |
|--------|-------------|---------|-------|
| `'0'` | Abierto/Activo | 13 | 50 |
| `'5'` | Pendiente de cierre | 357 | 807 |
| `'6'` | **Cerrado/Despachado** | 86.839 | 224.106 |
| `'8'` | Anulado | 69 | 141 |
| `'9'` | En proceso / Con partidas | 1.384 | 4.144 |

### Tabla ITEMPED — Campos críticos para producción

| Campo | Tipo | Descripción |
|-------|------|-------------|
| `SERIE` / `NUM_PED` / `NRO` | NUMBER (PK) | Identificador del ítem |
| `COD_ART` | VARCHAR2(25) → FK ARTICUL | Artículo pedido |
| `PROCESO` | VARCHAR2(4) → FK H_PROCESOS | Proceso requerido |
| `COLOR` | VARCHAR2(7) | Código de color |
| `INTENSIDAD` | VARCHAR2(1) | Intensidad del color |
| `CANTIDAD` | NUMBER | Cantidad pedida |
| `SALDO` | NUMBER | Saldo pendiente de despachar |
| `PRECIO` | NUMBER | Precio unitario |
| `ESTADO` | VARCHAR2(1) | Estado del ítem (misma tabla de estados) |
| `FENTTIN_INI` / `FENTTIN_FIN` | DATE | Fechas compromiso de entrada a tintorería |
| `NUM_PED_DEST` / `ITEM_PED_DEST` | NUMBER | Pedido destino (si es transferencia) |
| `NUM_PROG` | NUMBER | Número de programa asignado |
| `F_MAXPED` | DATE | Fecha máxima de entrega comprometida |
| `SOLO_DESPACHO` | VARCHAR2(1) | `S` = solo despacho (sin producir) |
| `APROB_CIERRE` | VARCHAR2(1) | Aprobación para cierre |
| `I_STOCK` / `I_SPPD` / `I_SPPR` | NUMBER | Stock disponible / en producción / en proceso |

### Triggers en PEDIDO

| Trigger | Evento | Descripción |
|---------|--------|-------------|
| `TIB_PEDIDO` | BEFORE INSERT | Asigna PK automática desde secuencia; rellena auditoría |
| `TUB_PEDIDO` | BEFORE UPDATE | Actualiza auditoría A_MDUSER/A_MDFECHA |
| `TUA_PED_ESTADO` | AFTER UPDATE | Al cambiar ESTADO → actualiza estados en cascada |
| `TUA_PED_CLIENTE` | AFTER UPDATE | Al cambiar cliente → sincroniza datos en ítems |

### Triggers en ITEMPED

| Trigger | Evento | Descripción |
|---------|--------|-------------|
| `TIB_ITEMPED` | BEFORE INSERT | Asigna PK; rellena auditoría; inicializa saldo = cantidad |
| `TIA_ITEMPED` | AFTER INSERT | Actualiza totales en PEDIDO |
| `TUB_ITEMPED` | BEFORE UPDATE | Auditoría |
| `TUB_ITEMPED_CIERRE` | BEFORE UPDATE | Valida condiciones de cierre del ítem |
| `TUB_ITEMPED_APERTURA` | BEFORE UPDATE | Valida reapertura |
| `TUB_ITEMPED_REAPERTURA` | BEFORE UPDATE | Lógica de re-apertura de ítem cerrado |
| `TUB_ITEMPED_CODART` | BEFORE UPDATE | Valida que no se cambie artículo con partidas abiertas |
| `TUB_ITEMPED_NCOLOR` | BEFORE UPDATE | Propagación de cambio de NCOLOR |
| `TUA_ITEMPED_SALDO` | AFTER UPDATE | Actualiza saldo del ítem y del PEDIDO |
| `TUA_ITEMP_ESTADO` | AFTER UPDATE | Actualiza estado del PEDIDO según estado del ítem |
| `TUA_ITEMP_ESTDO_ART` | AFTER UPDATE | Actualiza estado del artículo en maestro ARTICUL |

### FK del módulo de pedidos

```
PEDIDO.COD_CLIENTE  ──► CLIENTES.COD_CLIENTE
PEDIDO.COND_PAG     ──► CONDPAG.COND_PAG
ITEMPED.(SERIE+NUM_PED) ──► PEDIDO.(SERIE+NUM_PED)
ITEMPED.COD_ART     ──► ARTICUL.COD_ART
ITEMPED.PROCESO     ──► H_PROCESOS.PROCESO
ITEMPED.TITULO      ──► H_TITULOS.TITULO
```

### Procedimientos / Funciones del módulo

| Objeto | Tipo | Descripción |
|--------|------|-------------|
| `PED_PAC_RUTINAS` | PACKAGE | Rutinas generales del módulo de pedidos |
| `VV_PAC_RUTINAS` | PACKAGE | Rutinas de ventas |
| `ACTUALIZA_PEDIDO` | PROCEDURE | Recalcula totales del pedido |
| `ANULA_PEDIDO_TOTAL` | PROCEDURE | Anula pedido completo con todos sus ítems |
| `CIERRA_PEDIDO_TOTAL` | PROCEDURE | Cierra pedido (cambia estado a '6') |
| `ARMA_ITEMPED_ALMACEN` | PROCEDURE | Genera ítems de almacén desde el pedido |
| `ESTATUS_UBICACION_PEDIDO` | FUNCTION | Retorna el estado/ubicación actual del pedido |
| `REPRODUCIDAD_PEDIDO` | FUNCTION | Indica si el pedido tiene partidas reproducibles |
| `LUGARENTREGA_PEDIDO` | FUNCTION | Retorna descripción del lugar de entrega |
| `OBTIENE_PEDIDO_ANTICIPO` | FUNCTION | Obtiene el anticipo vinculado al pedido |
| `MATERIAL_PEDIDO_ABREVIADO` | FUNCTION | Descripción abreviada del material del pedido |
| `PEDIDO_CON_SOLICITUD` | FUNCTION | Indica si el pedido tiene solicitud de crédito |
| `V_PEDIDOS` | VIEW | Vista con datos completos del pedido + cliente |
| `V_ITEMPED` | VIEW | Vista de ítems con información calculada |
| `V_SALPED` | VIEW | Vista de saldos pendientes por despachar |
| `V_STATUS_PEDIDO` | VIEW | Vista de estados del pedido |
| `VHRECPED` | VIEW | Histórico recepción-pedido |

---

## PASO 2 — PLANIFICACIÓN DE PRODUCCIÓN (ITEMPED_DET)

Después de tomar el pedido, el área de planeamiento asigna cada ítem a un proceso de producción específico. Esto genera registros en `ITEMPED_DET`.

### Tabla ITEMPED_DET — Descomposición por etapa/lote

> **CLAVE DE ENLACE**: `ITEMPED_DET.NROPROG` = `PARTIDA.NROPROG` (1:1). Este campo es el puente principal entre la planificación y el lote físico.

> **PARTICIÓN DE ÍTEMS**: Un mismo ítem (`NRO`) puede tener `NUM_DET = 0, 1, 2, 3...` cuando la cantidad se divide en varios sub-lotes para diferentes máquinas. Cada `NUM_DET` tiene su propio `NROPROG` y su propia `PARTIDA`.

| Campo | Tipo | Descripción |
|-------|------|-------------|
| `SERIE`/`NUM_PED`/`NRO` | NUMBER | FK → ITEMPED |
| `NUM_DET` | NUMBER | Sub-lote (0=único, >0=partición del ítem) |
| `NROPROG` | NUMBER | **PK natural** — vincula con PARTIDA.NROPROG |
| `MAQUINA` | VARCHAR2(4) | Máquina asignada |
| `PROCESO` | VARCHAR2(4) | Proceso específico |
| `LOTE` | VARCHAR2(15) | Lote de producción |
| `FHC_PROG` | DATE | Fecha programada de inicio |
| `FHC_ENTREGA` | DATE | Fecha comprometida de entrega |
| `FCH_ENTREGA_CONO_UNO` | DATE | Fecha estimada: primera mano de conos |
| `FCH_ESTIMA_CONO_UNO` | DATE | Fecha estimada definitiva: cono uno |
| `FCH_ESTIMA_TENIDO` | DATE | Fecha estimada de tenido |
| `FCH_ENT_TIN` | DATE | Fecha entrada a tintorería |
| `FCH_REG_ENTREGA` | DATE | Fecha real de entrega registrada |
| `CANTIDAD` | NUMBER | Kg/unidades asignados a este detalle |
| `SALDO` | NUMBER | Saldo pendiente de esta etapa |
| `ESTADO` | VARCHAR2(1) | Estado de la etapa |
| `ESTADO_PROG` | VARCHAR2(2) | Estado del programa |
| `URGENTE` | VARCHAR2(1) | Indicador de urgencia |
| `NIVEL_URGENCIA` | NUMBER | Nivel numérico de urgencia |
| `NROPROG_REL` | NUMBER | Programa relacionado |
| `SIN_MATERIAL` | VARCHAR2(1) | Sin material disponible |
| `MATERIAL_PROD` | VARCHAR2(1) | Material en producción |
| `DESTINO` | VARCHAR2(4) | Destino de la partida |

### Triggers en ITEMPED_DET

| Trigger | Evento | Descripción |
|---------|--------|-------------|
| `TIB_ITEMPED_DET` | BEFORE INSERT | Asigna PK; auditoría |
| `TIA_ITEMPED_DET` | AFTER INSERT | Actualiza saldo de ITEMPED |
| `TIU_ITEMPED_DET` | BEFORE INSERT/UPDATE | Lógica de asignación de programa |
| `TUB_ITEMPED_DET` | BEFORE UPDATE | Auditoría |
| `TUB_ITEMPED_DET_NCOLOR` | BEFORE UPDATE | Propagación NCOLOR |
| `TUA_ITEMPEDET_EST` | AFTER UPDATE | Actualiza estado de ITEMPED al cambiar estado del detalle |

### Vistas de análisis

| Vista | Descripción |
|-------|-------------|
| `V_ITEMPEDET` | Vista completa de ítems detallados con fechas |
| `V_FECING_FECDESP_PT` | Vista: fecha ingreso vs. fecha despacho en planta |

---

## PASO 3 — PROGRAMACIÓN DE DEVANADO (post-tintorería)

> **ATENCIÓN**: `H_PROGRAMACION` NO es el programa inicial de hilandería (spinning). Es el programa de **devanado** — la conversión de madejas teñidas a conos. Se crea DESPUÉS de que la PARTIDA sale de tintorería. El campo `GUIA` apunta a `PARTIDA.NUMERO`.
>
> La producción de hilatura (spinning) se registra en `H_RPRODUC` por tipo de máquina (BATAN → CARDA → MANUAR → PABILERA → CONTINUA → RETORCEDORA). Ver Section 10 (Análisis de datos).

El programador asigna las partidas teñidas a máquinas de devanado/enconado en `H_PROGRAMACION`.

### Tabla H_PROGRAMACION

| Campo | Tipo | Descripción |
|-------|------|-------------|
| `NUMERO` | NUMBER (PK) | ID del programa |
| `FECHA` | DATE | Fecha de inicio del programa |
| `TIPO` | VARCHAR2(1) | Tipo de programa |
| `GUIA` | NUMBER → FK PARTIDA | Guía / partida vinculada |
| `PEDIDO` | VARCHAR2(15) | Referencia al pedido |
| `LOTE` | VARCHAR2(10) | Lote de producción |
| `ORDEN` | NUMBER | Orden de ejecución |
| `MAQ_PROCED` | VARCHAR2(6) | Máquina procedente |
| `TITULO` | VARCHAR2(8) | Título a producir |
| `FIBRA` | VARCHAR2(8) | Fibra a trabajar |
| `ARTSERV` | VARCHAR2(8) | Artículo/servicio |
| `PROCESO` | VARCHAR2(4) | Proceso |
| `VALPF` | VARCHAR2(5) | Valor de peinado/fino |
| `ESTADO` | VARCHAR2(1) | Estado del programa |
| `FECHA_FIN` | DATE | Fecha fin estimada |
| `F_APROB_CCAL` | DATE | Fecha aprobación control de calidad |
| `VELOCIDAD` | NUMBER | Velocidad configurada |
| `HUSOS_ACT` | NUMBER | Husos activos |
| `KG_UNIDAD` | NUMBER | Kg por unidad |
| `TICKET_PROD` | NUMBER | Ticket de producción |

### Triggers en H_PROGRAMACION

| Trigger | Evento | Descripción |
|---------|--------|-------------|
| `TIB_H_PROGRAMACION` | BEFORE INSERT | Asigna secuencia; auditoría; vincula con PARTIDA |
| `TUB_H_PROGRAMACION` | BEFORE UPDATE | Auditoría; valida cambios de estado |

### FK de programación
```
H_PROGRAMACION.GUIA ──► PARTIDA.NUMERO
```

---

## PASO 4 — PARTE DIARIO DE PRODUCCIÓN (HILANDERÍA)

Los operarios registran la producción real turno a turno.

### Tablas H_PRODUCCION_G / H_PRODUCCION_D

#### H_PRODUCCION_G — Cabecera (PK compuesta)

| Campo | Tipo | Descripción |
|-------|------|-------------|
| `FECHA` | DATE (PK) | Fecha del parte |
| `TURNO` | VARCHAR2(4) (PK) | Turno |
| `TP_MAQ` | VARCHAR2(1) (PK) | Tipo máquina (`C`=continua, `A`=anillera, etc.) |
| `COD_MAQ` | VARCHAR2(6) (PK) | Código de máquina |
| `C_CODIGO` | VARCHAR2(8) (PK) | Código del operario |
| `NOMBRE` | VARCHAR2(60) | Nombre del operario |

#### H_PRODUCCION_D — Detalle (PK extiende G)

Extiende la PK con `ITEM`. Registra por cada producto fabricado en ese turno:
- Título, fibra, color, metraje, pesos, velocidad
- Horas trabajadas vs. paradas
- Lote de materia prima (`LOTE_FARDO`)
- Mermas desagregadas (`MERMA1` a `MERMA9`)
- Vinculación directa al pedido (`NUM_PED`, `ITEM_PED`)
- Ticket de producción (`TICKET_PROD`)

### FK de producción
```
H_PRODUCCION_D.(FECHA+TURNO+TP_MAQ+COD_MAQ+C_CODIGO)
    ──► H_PRODUCCION_G.(FECHA+TURNO+TP_MAQ+COD_MAQ+C_CODIGO)
```

### Vistas de producción

| Vista | Descripción |
|-------|-------------|
| `H_PRODUCCIONAD_V` | Producción alternativa (preparatorias) |
| `H_PRODUCCIONC_V` | Producción continua |
| `H_PRODUCCIOND_GASEADO` | Producción por proceso Gaseado |
| `H_PRODUCCIOND_MADEJERA` | Producción en Madejera |
| `H_PRODUCCIOND_REDINA` | Producción en Redina |
| `H_PRODUCCIOND_V` | Vista general de producción detallada |
| `H_TINTO_PRODUCV` | Producción vinculada a tintorería |
| `V_ULT_TKT_PROD` | Último ticket de producción por máquina |

---

## PASO 5 — CREACIÓN DE PARTIDA (LOTE FÍSICO)

Cuando la producción de hilandería termina, se registra una `PARTIDA` — el lote físico que avanza hacia tintorería.

> **CLAVE DE ENLACE**: `PARTIDA.NROPROG = ITEMPED_DET.NROPROG` (relación 1:1 — cada sub-programa tiene exactamente una partida)

> **TABLA PUENTE PARTIDA_MAS**: Una partida puede tener múltiples baños de tintorería. La tabla `PARTIDA_MAS` (PARTIDA → ING_RECETAS_G) registra cada etapa de teñido para la misma partida física (ej: Teñido en R07 + Blanqueo en R03).

### Tabla PARTIDA — Campos clave

| Campo | Tipo | Descripción |
|-------|------|-------------|
| `NUMERO` | NUMBER (PK) | ID de la partida |
| `NRO_PEDIDO` | NUMBER | Pedido origen |
| `SER_PARTIDA` | NUMBER | Serie partida |
| `NROPART` | NUMBER | Número de partida |
| `FECHA` | DATE | Fecha de creación |
| `COD_CLIENTE` | VARCHAR2(15) | Cliente destino |
| `C_CODIGO` | VARCHAR2(8) | Código del trabajador responsable |
| `RMC` | VARCHAR2(2) | Código de RMC (Receta/Mezcla de Color) |
| `NRO_RMC` | NUMBER | Número de RMC |
| `LOTE_PRODUC` | VARCHAR2(20) | Lote de producción |
| `PESO_BRUTO` | NUMBER | Peso bruto |
| `PESO_NETO` | NUMBER | Peso neto (el dato que importa) |
| `COLOR` | VARCHAR2(6) | Color |
| `TITULO` | VARCHAR2(10) | Título del hilo |
| `TIPO_FIBRA` | VARCHAR2(8) | Fibra |
| `ARTSERV` | VARCHAR2(8) | Artículo/servicio |
| `PROCESO1` | VARCHAR2(4) | Proceso aplicado |
| `COD_ART` | VARCHAR2(25) | Artículo → ARTICUL |
| `COD_MAQ` | VARCHAR2(4) | Máquina que lo produjo |
| `SITU_PART` | VARCHAR2(4) | **Situación de la partida** (ver tabla) |
| `ESTADO` | VARCHAR2(1) | Estado |
| `FCH_ENTREGA` | DATE | Fecha de entrega comprometida |
| `FCH_ENTREGA_ORI` | DATE | Fecha original de entrega |
| `NROPROG` | NUMBER | Número de programa de hilandería |
| `GUIA_ORIGEN` | NUMBER | Guía de origen (si viene de tintorería) |
| `SALDO` | NUMBER | Saldo disponible para despachar |
| `TIPO` | VARCHAR2(1) | Tipo de partida |
| `TIPO_RODETE` | VARCHAR2(1) | Tipo de rodete |
| `REPRODUCIBLE` | VARCHAR2(1) | Indica si es reproducible en tintorería |
| `SERIE` / `NRO` | NUMBER | FK → ITEMPED (serie/item del pedido) |
| `NUM_PED_ORIG` / `NRO_ORIG` | NUMBER | Pedido/ítem origen (si es reproceso) |

#### Situaciones de PARTIDA (SITU_PART)

| Código | Descripción |
|--------|-------------|
| `(vacío)` | En hilandería (154.063 partidas activas) |
| `R001` | Recibida en tintorería — en espera (4.644) |
| `P` | En proceso en tintorería (3.262) |
| `A` | Acabada (terminada) (972) |
| `T002` | Tenido etapa 2 (640) |
| `R003` | Recibida etapa 3 (476) |
| `R007` | Recibida etapa 7 (421) |
| `X` (ESTADO=9) | Cerrada/Despachada (4.275) |

### FK de PARTIDA
```
PARTIDA.(SERIE+NRO) ──► ITEMPED.(SERIE+NRO)   [FK_PROGRAMA_PARTIDA]
H_PROGRAMACION.GUIA ──► PARTIDA.NUMERO         [FK_H_PROGRAMACION_PARTIDA]
```

### Triggers en PARTIDA

| Trigger | Evento | Descripción |
|---------|--------|-------------|
| `TIB_PARTIDA` | BEFORE INSERT | Asigna PK desde secuencia; auditoría |
| `TIA_PARTIDA` | AFTER INSERT | Actualiza saldos en ITEMPED; genera movimiento inicial |
| `TUB_PARTIDA` | BEFORE UPDATE | Auditoría; validaciones de cambio de situación |
| `TUA_PARTIDA_ESTADO` | AFTER UPDATE | Al cerrar partida → actualiza ITEMPED.SALDO |
| `TDB_PARTIDA` | BEFORE DELETE | Protege eliminación con partidas vinculadas |

### Funciones de análisis de partidas

| Función | Descripción |
|---------|-------------|
| `ESTATUS_UBICACION_PARTIDA` | Retorna texto con la ubicación actual de la partida |
| `ESTATUS_UBICACION_PARTIDA2` | Versión alternativa (sin JOINs costosos) |
| `ES_PARTIDA` | Verifica si un número corresponde a una partida |
| `PARTIDA_CON_ACOL` | ¿Tiene acolchado? |
| `PARTIDA_CON_ADIC` | ¿Tiene adicional? |
| `PARTIDA_CON_MATIZ` | ¿Tiene matiz? |
| `PARTIDA_CON_REBAJE` | ¿Tiene rebaje? |
| `PARTIDAS_DE_RECETA` | Retorna partidas asociadas a una receta |
| `PARTIDAS_HALLAZGO` | Partidas con hallazgo de calidad |
| `PARTIDAS_MADEJA` | Partidas en formato madeja |
| `PARTIDA_LABORATORISTA` | Laboratorista asignado |
| `REPRODUCIDAD_PARTIDA` | ¿Es reproducible? |
| `V_PARTIDA` | Vista con estado completo de la partida |
| `V_ANALIZA_PARTIDA` | Vista de análisis de la partida |
| `V_STATUS_PARTIDA` / `V_STATUS_PARTIDA2` | Vistas de estatus |
| `VCC_PARTIDA` | Vista de control de calidad |

---

## PASO 6 — RECETA DE TINTORERÍA

Cuando la partida entra a tintorería, se genera la receta con los reactivos químicos.

### Tablas ING_RECETAS_G / ING_RECETAS_D / PARTIDA_MAS

#### PARTIDA_MAS — Tabla puente PARTIDA ↔ ING_RECETAS_G

Una PARTIDA puede requerir múltiples baños de tintorería en etapas sucesivas (ej: Teñido → Blanqueo). `PARTIDA_MAS` registra cada relación:

```
PARTIDA.NUMERO ──(PARTIDA_MAS.PARTIDA)──► PARTIDA_MAS ──(PARTIDA_MAS.RECETA)──► ING_RECETAS_G.NUMERO
```

Ejemplo real (pedido 88586, PARTIDA 158939):
- `ING_RECETAS_G` 469500: PROCESO=`TE` (Teñido), MAQUINA=R07, RECETA_G=54310
- `ING_RECETAS_G` 469594: PROCESO=`BQM` (Blanqueo químico), MAQUINA=R03

#### ING_RECETAS_G — Cabecera de receta

| Campo | Tipo | Descripción |
|-------|------|-------------|
| `TP_TRANSAC` | VARCHAR2(4) (PK) | Tipo de transacción |
| `SERIE` | NUMBER (PK) | Serie |
| `NUMERO` | NUMBER (PK) | Número de receta |
| `R_TRANSAC` / `R_SERIE` / `R_NUMERO` | VARCHAR2/NUMBER | Receta maestra de referencia |
| `MAQUINA` | VARCHAR2(4) | Máquina de tintorería |
| `COD_VOL` | VARCHAR2(6) | Código de volumen |
| `PROCESO` | VARCHAR2(6) | Proceso de tintura |
| `PESO_NETO` | NUMBER | Peso neto del baño |
| `OBSERVACION` | VARCHAR2(120) | Observaciones |
| `FECHA` | DATE | Fecha de preparación |
| `ESTADO` | VARCHAR2(1) | Estado |
| `PLANTILLA` | NUMBER | FK → L_PLANTILLA_G |
| `TIPO_REF` / `NUM_REF` / `ITEM_REF` | — | Referencia al pedido |
| `COD_RECETA` | VARCHAR2(8) | Código receta maestra |

#### ING_RECETAS_D — Detalle (insumos por proceso)

| Campo | Tipo | Descripción |
|-------|------|-------------|
| `PROCESO` | VARCHAR2(30) | Proceso del baño |
| `ITEM` | NUMBER | Ítem |
| `COD_ART` | VARCHAR2(25) | Artículo/reactivo → ARTICUL |
| `CANTIDAD` | NUMBER | Cantidad a consumir |
| `UNIDAD` | VARCHAR2(6) | Unidad de medida |
| `TOTAL` | NUMBER | Total calculado |
| `DESPACHO` | NUMBER | Cantidad ya despachada del almacén |
| `LOTE` | VARCHAR2(30) | Lote del reactivo |

### Función de apoyo

| Función | Descripción |
|---------|-------------|
| `ARTICULO_X_RECETA` | Retorna artículo principal de la receta |
| `INTENSIDAD_RECETA` | Intensidad calculada de la receta |
| `PR_RECETA_KGR` | Kg de reactivo reales |
| `TIENE_RECETA` | ¿Tiene receta activa? |
| `V_RECETAPARTIDA` | Vista receta + partida |

---

## PASO 7 — PROGRAMACIÓN DE TINTORERÍA

### Tablas TT_PROGPART / TT_PROGPARTD

| Tabla | Descripción |
|-------|-------------|
| `TT_PROGPART` | Programa de entrega de partida desde tintorería (75.963 registros) |
| `TT_PROGPARTD` | Historial de cambios de fechas de entrega (motivos de reprogramación) |
| `TT_PARAMPROGTIN` | Parámetros: tiempos (tenido, madeja, acabado, calidad, enconado, revisado) |
| `TT_ULTFECHA` | Última fecha procesada por máquina |

#### TT_PROGPART — Campos

| Campo | Tipo | Descripción |
|-------|------|-------------|
| `NUM_PED` | NUMBER (PK) | Pedido |
| `NRO` | NUMBER (PK) | Ítem del pedido |
| `NUM_DET` | NUMBER (PK) | Detalle del ítem |
| `FENTREGA` | DATE | Fecha comprometida de entrega desde TT |
| `ESTADO` | VARCHAR2(1) | Estado del programa TT |

#### TT_PROGPARTD — Campos (historial de reprogramaciones)

| Campo | Tipo | Descripción |
|-------|------|-------------|
| `FENTREGA` | DATE | Nueva fecha de entrega |
| `AREA` | VARCHAR2(2) | Área responsable del cambio |
| `MOTIVO` | VARCHAR2(2) | Motivo de reprogramación |

### Triggers TT_PROGPART / TT_PROGPARTD

| Trigger | Evento | Descripción |
|---------|--------|-------------|
| `TIB_TT_PROGPART` | BEFORE INSERT | Auditoría |
| `TUB_TT_PROGPART` | BEFORE UPDATE | Auditoría; genera historial en TT_PROGPARTD |
| `TIB_TT_PROGPARTD` | BEFORE INSERT | Auditoría |
| `TUB_TT_PROGPARTD` | BEFORE UPDATE | Auditoría |

---

## PASO 8 — PRODUCCIÓN REAL EN TINTORERÍA

### Tabla TT_RPRODUC (178.003 registros)

Registro de cada proceso de tintura ejecutado.

| Campo | Tipo | Descripción |
|-------|------|-------------|
| `RECETA` | NUMBER (PK) | Número de receta |
| `PROCESO` | VARCHAR2(6) (PK) | Proceso de tintura |
| `FECHA_INI` | DATE | Inicio del proceso |
| `FECHA_FIN` | DATE | Fin del proceso |
| `C_CODIGO` | VARCHAR2(8) | Operario |
| `ESTADO` | VARCHAR2(1) | Estado |
| `COD_MAQ` | VARCHAR2(6) | Máquina utilizada |
| `CALIFICACION` | VARCHAR2(2) | Calificación del resultado |
| `TIPODOC` | VARCHAR2(2) | Tipo de documento |
| `PARTICION` | NUMBER | Número de partición del baño |
| `COD_SUPERV` | VARCHAR2(8) | Supervisor |

### Tabla TT_RSECADO (99.200 registros)

Registro del proceso de secado post-tenido.

| Campo | Tipo | Descripción |
|-------|------|-------------|
| `GUIA` | NUMBER (PK) | Guía/partida |
| `COD_MAQ` | VARCHAR2(6) | Máquina de secado |
| `FECHA_INI` / `FECHA_FIN` | DATE | Rango de secado |
| `SECADO` | NUMBER | Tiempo de secado (minutos) |
| `RESECADO` | NUMBER | Tiempo de re-secado si aplica |
| `ESTADO` | VARCHAR2(1) | Estado |
| `PESO_NETO` | NUMBER | Peso neto post-secado |
| `COD_ALM_KD` | VARCHAR2(2) | Almacén del kardex vinculado |
| `TP_TRANSAC_KD` | VARCHAR2(4) | Tipo transacción kardex |
| `SERIE_KD` / `NUMERO_KD` | NUMBER | Referencia al Kardex de ingreso |
| `IND_FLOCA` | VARCHAR2(1) | Indicador de floca |

### Tabla TT_RPARADA (68.338 registros)

Paradas de máquinas en tintorería.

| Campo | Tipo | Descripción |
|-------|------|-------------|
| `COD_MAQ` | VARCHAR2(4) (PK) | Máquina parada |
| `MOTIVO` | VARCHAR2(4) (PK) | Código de motivo |
| `FECHA_INI` / `FECHA_FIN` | DATE | Rango de parada |
| `RECETA` | NUMBER | Receta interrumpida |
| `OBS_MANT` | VARCHAR2(200) | Observación de mantenimiento |
| `TIPO` | VARCHAR2(1) | Tipo de parada |

### Vistas de tintorería

| Vista | Descripción |
|-------|-------------|
| `H_TINTO_PRODUCV` | Producción vinculada hilandería-tintorería |
| `V_RPRODUC` | Vista de producción real de tintorería |
| `GUIAS_PEDIDO_LOTES` | Relación guías + pedidos + lotes |

---

## PASO 9 — GENERACIÓN DE LOTES (INVENTARIO DE PT)

Al terminar el proceso en tintorería, se crean los `LOTES` — las unidades físicas individuales que irán al almacén de producto terminado.

### Tabla LOTES (1.604.169 registros)

| Campo | Tipo | Descripción |
|-------|------|-------------|
| `COD_ALM` | VARCHAR2(2) (PK) | Almacén |
| `TP_TRANSAC` | VARCHAR2(4) (PK) | Tipo de transacción |
| `SERIE` | NUMBER (PK) | Serie |
| `NUMERO` | NUMBER (PK) | Número |
| `COD_ART` | VARCHAR2(25) (PK) | Artículo |
| `LOTE` | NUMBER (PK) | Número de lote |
| `FECHA` | DATE | Fecha de creación |
| `STOCK_INIC` | NUMBER | Stock inicial del lote |
| `SALDO` | NUMBER | Saldo actual del lote |
| `TARA` | NUMBER | Tara (peso del envase) |
| `PESO_BRUTO` | NUMBER | Peso bruto |
| `CANTIDAD` | NUMBER | Cantidad en unidades |
| `MAQUINA` | VARCHAR2(4) | Máquina de origen |
| `ESTADO` | VARCHAR2(1) | Estado |
| `UBICACION` | VARCHAR2(8) | Ubicación en almacén |
| `NLOTE` | VARCHAR2(30) | Nombre/descripción del lote |
| `TORSIDO` | VARCHAR2(4) | Torsión del hilo |
| `OPERARIO` | VARCHAR2(8) | Operario responsable |
| `FEC_SALIDA` | DATE | Fecha de salida del almacén |
| `CALIFICACION` | VARCHAR2(4) | Calificación de calidad |
| `PARTIDA` | NUMBER | FK → PARTIDA.NUMERO |
| `NUM_PED` | NUMBER | Pedido asociado |
| `REVISADOR` | VARCHAR2(8) | Revisor de calidad |

### Triggers en LOTES

| Trigger | Evento | Descripción |
|---------|--------|-------------|
| `TIB_LOTES_AUD` | BEFORE INSERT | Auditoría y validación |
| `TUA_LOTES` | AFTER UPDATE | Actualiza stock en ALMACEN cuando cambia saldo |
| `TUB_LOTES_AUD` | BEFORE UPDATE | Auditoría |

---

## PASO 10 — MOVIMIENTO DE ALMACÉN (KARDEX)

Cada ingreso o salida de mercadería genera registros en `KARDEX_G` (cabecera) y `KARDEX_D` (detalle por artículo).

### Tabla KARDEX_G (1.105.741 registros) — Cabecera

| Campo | Tipo | Descripción |
|-------|------|-------------|
| `COD_ALM` | VARCHAR2(2) (PK) | Almacén |
| `TP_TRANSAC` | VARCHAR2(4) (PK) | **Tipo de transacción** |
| `SERIE` | NUMBER (PK) | Serie |
| `NUMERO` | NUMBER (PK) | Número del documento |
| `FCH_TRANSAC` | DATE | Fecha de la transacción |
| `ING_SAL` | VARCHAR2(1) | `I`=Ingreso, `S`=Salida, `C`=Corrección |
| `MOTIVO` | VARCHAR2(4) | Motivo del movimiento |
| `ESTADO` | VARCHAR2(1) | Estado |
| `GLOSA` | VARCHAR2(250) | Descripción |
| `COD_RELACION` | VARCHAR2(15) | Código del cliente/proveedor |
| `NOMBRE` / `RUC` | VARCHAR2 | Datos del tercero |
| `NRO_DESPACHO` | NUMBER | Número de despacho |
| `ORDEN_DESPACHO` | NUMBER | Orden del despacho |
| `F_ENTREGA` | DATE | Fecha de entrega |
| `IND_DESP` | VARCHAR2(1) | Indicador de despacho |
| `C_TIPO`/`C_SERIE`/`C_NUMERO` | VARCHAR2 | Comprobante generado (factura, etc.) |
| `CONTENEDOR` / `NRO_PRECINTO` | VARCHAR2 | Datos de exportación |

#### Tipos de Transacción KARDEX más frecuentes

| TP_TRANSAC | ING_SAL | Frecuencia | Significado |
|------------|---------|-----------|-------------|
| `22` | S | 724.901 | **Salida por Despacho/Venta** (más frecuente) |
| `21` | S | 186.658 | Salida por transferencia interna |
| `16` | I | 90.444 | Ingreso de producción PT |
| `11` | I | 49.995 | Ingreso de compra/MP |
| `24` | S | 35.717 | Salida por merma/ajuste |
| `23` | S | 3.922 | Salida por devolución |
| `20` | I | 3.286 | Ingreso por devolución de cliente |
| `17` | I | 1.864 | Ingreso por producción en proceso |
| `12` | I | 1.779 | Ingreso por transferencia |
| `29` | S | 430 | Salida exportación |
| `31` | S | 418 | Salida por consignación |

### Tabla KARDEX_D (4.033.904 registros) — Detalle

| Campo | Tipo | Descripción |
|-------|------|-------------|
| `COD_ALM`/`TP_TRANSAC`/`SERIE`/`NUMERO` | PK | FK → KARDEX_G |
| `COD_ART` | VARCHAR2(25) | Artículo movido → ARTICUL |
| `CANTIDAD` | NUMBER | Cantidad movida |
| `COSTO_D` / `COSTO_S` | NUMBER | Costo en dólares / soles |
| `FCH_TRANSAC` | DATE | Fecha |
| `ING_SAL` | VARCHAR2(1) | Dirección del movimiento |
| `IMP_VVB` | NUMBER | Importe valor de venta base |
| `NROPART` | NUMBER | Número de partida |
| `NUM_PED` / `NRO` | NUMBER | Pedido e ítem vinculado |
| `GUIA` | NUMBER | Guía de remisión |
| `ORDEN` | NUMBER | Orden de despacho |

### FK del Kardex
```
KARDEX_D.(COD_ALM+TP_TRANSAC+SERIE+NUMERO)
    ──► KARDEX_G.(COD_ALM+TP_TRANSAC+SERIE+NUMERO)
KARDEX_D.COD_ART ──► ARTICUL.COD_ART
```

### Triggers en KARDEX

| Trigger | Evento | Descripción |
|---------|--------|-------------|
| `TIB_KARDEXG_AUD` | BEFORE INSERT KD_G | Auditoría |
| `TIA_KARDEX_G` | AFTER INSERT KD_G | Actualiza stock en ALMACEN |
| `TUA_KARDEXG_ESTADO` | AFTER UPDATE KD_G | Al anular → reversa ALMACEN.STOCK |
| `TUA_KARDEXG_FECHA` | AFTER UPDATE KD_G | Al cambiar fecha → recalcula saldos |
| `TIB_KARDEXD_AUD` | BEFORE INSERT KD_D | Auditoría |
| `TIB_KARDEXD_CUENTAS` | BEFORE INSERT KD_D | Asigna cuenta contable automática |
| `TIA_KARDEX_D` | AFTER INSERT KD_D | Actualiza ALMACEN.STOCK; genera asiento contable |
| `TUA_KARDEXD_EST` | AFTER UPDATE KD_D | Reversa movimiento al anular |
| `TUA_KARDEX_D_CANTIDAD` | AFTER UPDATE KD_D | Ajuste de cantidad → recalcula stock |
| `TDB_KARDEX_D` | BEFORE DELETE KD_D | Protege eliminación |
| `TDB_KARDEX_G` | BEFORE DELETE KD_G | Protege eliminación |

---

## PASO 11 — CONTROL DE STOCK

### Tabla ALMACEN (64.624 registros)

Stock actual consolidado por artículo + almacén.

| Campo | Tipo | Descripción |
|-------|------|-------------|
| `COD_ART` | VARCHAR2(25) (PK) | Artículo |
| `COD_ALM` | VARCHAR2(2) (PK) | Almacén |
| `STOCK` | NUMBER | Stock actual (mantenido automáticamente por triggers) |
| `UBIC` | VARCHAR2(8) | Ubicación en almacén |
| `INV_FIS` | NUMBER | Último inventario físico |
| `FCH_INVE` | DATE | Fecha del último inventario |

> ⚠️ **CRÍTICO**: El campo `STOCK` es mantenido **automáticamente** por los triggers `TIA_KARDEX_D` y `TUA_KARDEX_D_CANTIDAD`. **Nunca se debe actualizar manualmente.**

### Tabla SALDOS (489.573 registros)

Saldos contables por cuenta (para conciliación).

### Vistas de stock

| Vista | Descripción |
|-------|-------------|
| `VSALDOS` | Vista de saldos contables |
| `VSALDOSC` | Vista de saldos consolidados |
| `V_KARDEXD_PED` | Vista kardex detalle vinculado a pedido |

---

## PASO 12 — DESPACHO AL CLIENTE

El despacho es la salida física del producto terminado: genera una transacción `TP_TRANSAC='22'` en KARDEX + guía de remisión.

### Tabla DESPACHO_GUIA (3.442 registros)

Cabecera del despacho logístico.

| Campo | Tipo | Descripción |
|-------|------|-------------|
| `NUM_DESPACHO` | NUMBER (PK) | Número del despacho |
| `F_TRASLADO` | DATE | Fecha de traslado físico |
| `C_CONDUCTOR` | VARCHAR2(2) | Código del conductor |
| `PLACA_TRANSP` | VARCHAR2(10) | Placa del vehículo |
| `DETALLE` | VARCHAR2(100) | Descripción del despacho |
| `ESTADO` | VARCHAR2(1) | Estado del despacho |

El despacho genera en KARDEX_G:
- `TP_TRANSAC = '22'` (Salida por Venta)
- `ING_SAL = 'S'`
- `NRO_DESPACHO` → referencia al número de despacho
- `ORDEN_DESPACHO` → orden dentro del despacho
- `C_TIPO`/`C_SERIE`/`C_NUMERO` → datos del comprobante (factura/boleta)

### DESPED_ALM (480 registros)

Detalle de artículos despachados por almacén.

| Campo | Tipo | Descripción |
|-------|------|-------------|
| `TIPODOC` | VARCHAR2(2) (PK) | Tipo documento |
| `SERIE` / `NUMERO` / `NRO` | NUMBER (PK) | Identificador |
| `COD_ART` | VARCHAR2(25) | Artículo despachado |
| `CANTIDAD` | NUMBER | Cantidad |
| `TIPODOC_REL` / `SERIE_REL` / `NUMERO_REL` | — | Documento fuente (pedido/factura) |

### Funciones de despacho

| Función | Descripción |
|---------|-------------|
| `KG_PEDIDO_DESPCH_ALMTER` | Kg despachados del pedido desde almacén terminado |
| `KG_PEDIDO_ING_ALMTER` | Kg ingresados al almacén terminado para el pedido |
| `KG_PARTIDA_ING_ALMTER` | Kg ingresados al almacén terminado para la partida |
| `LOTE_ALMPT_FACTURA` | Lotes del almacén PT asociados a una factura |
| `GENERA_GUIA_RETORNO` | PROCEDURE: genera guía de retorno/devolución |
| `ESTADO_GUIA_BZL` / `ESTADO_GUIA_BZLS` | Estado de guía en sistema externo |
| `V_PEDPROD_VS_PARTDESP` | Vista: producción pedida vs. partidas despachadas |

---

## CIERRE DEL CICLO — Actualización de saldo del pedido

Al confirmar el despacho (TP_TRANSAC='22'), los triggers del Kardex actualizan la cadena:

```
KARDEX_D INSERT (TP=22, ING_SAL='S')
   │
   ▼  [TIA_KARDEX_D]
ALMACEN.STOCK -= cantidad                 ← stock baja
   │
   ▼  [TUA_ITEMPED_SALDO + TUA_ITEMP_ESTADO]
ITEMPED.SALDO -= cantidad                 ← saldo del ítem baja
   │
   ▼  [TUA_PED_ESTADO]
PEDIDO.TOTAL_FACTURADO += importe         ← pedido se actualiza
   │
   ▼  (si ITEMPED.SALDO = 0)
ITEMPED.ESTADO = '6'  (Cerrado)
   │
   ▼  (si todos los ITEMPED cerrados)
PEDIDO.ESTADO = '6'   (Cerrado/Despachado)
```

---

## RESUMEN CONSOLIDADO — TABLAS POR PASO

| Paso | Tabla(s) Principal(es) | Filas | Rol |
|------|------------------------|------:|-----|
| 1 | `PEDIDO` + `ITEMPED` | 88.651 + 229.208 | Toma del pedido |
| 2 | `ITEMPED_DET` | 155.446 | Planificación por etapa |
| 3 | `H_PROGRAMACION` | 45.159 | Programa de hilandería |
| 4 | `H_PRODUCCION_G/D` | 933.269 / 1.263.670 | Parte diario producción |
| 5 | `PARTIDA` | 171.955 | Lote físico producido |
| 6 | `ING_RECETAS_G/D` | 469.713 / 4.794.953 | Receta de tintorería |
| 7 | `TT_PROGPART/D` | 75.963 / 8.807 | Programa de tintorería |
| 8 | `TT_RPRODUC` + `TT_RSECADO` | 178.003 / 99.200 | Producción y secado TT |
| 9 | `LOTES` | 1.604.169 | Inventario de PT |
| 10 | `KARDEX_G` + `KARDEX_D` | 1.105.741 / 4.033.904 | Movimientos de almacén |
| 11 | `ALMACEN` | 64.624 | Stock consolidado |
| 12 | `DESPACHO_GUIA` + `DESPED_ALM` | 3.442 / 480 | Despacho físico |

---

## RESUMEN DE TRIGGERS POR TABLA (FLUJO)

| Tabla | Triggers | Función principal |
|-------|---------|-------------------|
| `PEDIDO` | 4 | PK auto, auditoría, estado en cascada |
| `ITEMPED` | 11 | PK auto, auditoría, saldo, estado, validaciones |
| `ITEMPED_DET` | 6 | PK, auditoría, estado cascada |
| `H_PROGRAMACION` | 2 | PK, auditoría, vínculo con PARTIDA |
| `PARTIDA` | 5 | PK, saldo ITEMPED, protección DELETE |
| `TT_PROGPART` | 2 | Auditoría, historial de cambios |
| `TT_PROGPARTD` | 2 | Auditoría |
| `TT_RPRODUC` | 2 | Auditoría |
| `TT_RSECADO` | 2 | Auditoría |
| `LOTES` | 3 | Auditoría, actualiza ALMACEN.STOCK |
| `KARDEX_G` | 6 | Auditoría, stock, reversa en anulación |
| `KARDEX_D` | 7 | **Stock automático**, cuentas contables, protección |
| `L_PLANTILLA_G/D` | 4 | Auditoría de plantillas de planeamiento |

---

## PAQUETES INVOLUCRADOS EN EL FLUJO

| Paquete | Módulo | Descripción |
|---------|--------|-------------|
| `PED_PAC_RUTINAS` | Pedidos | Rutinas generales de pedidos |
| `VV_PAC_RUTINAS` | Ventas | Rutinas de ventas y facturación |
| `II_PAC_RUTINAS` | Inventario | Rutinas de kardex, lotes, stock |
| `HH_PAC_RUTINAS` | Hilandería | Rutinas de producción hilatura |
| `PKG_PROD_RUTINAS` | Producción | Rutinas de producción general |
| `CT_PAC_RUTINAS` | Rutas/Costos | Rutinas de rutas estándar |
| `LG_PAC_RUTINAS` | Logística | Rutinas de logística/compras |

---

## VISTAS CLAVE DEL FLUJO

| Vista | Descripción |
|-------|-------------|
| `V_PEDIDOS` | Pedidos con datos completos de cliente |
| `V_ITEMPED` | Ítems del pedido con datos calculados |
| `V_ITEMPEDET` | Ítems detallados (por etapa de producción) |
| `V_SALPED` | Saldos pendientes por despachar |
| `V_STATUS_PEDIDO` | Estado actual de cada pedido |
| `V_ANALIZA_PARTIDA` | Análisis completo de la partida |
| `V_STATUS_PARTIDA` / `V_STATUS_PARTIDA2` | Estado de la partida en el flujo |
| `V_PARTIDA` | Vista con datos completos de la partida |
| `V_RECETAPARTIDA` | Receta vinculada a la partida |
| `V_RPRODUC` | Producción real de tintorería |
| `V_KARDEXD_PED` | Movimientos de almacén por pedido |
| `V_PEDPROD_VS_PARTDESP` | Comparativo: KG pedidos vs. KG despachados |
| `V_FECING_FECDESP_PT` | Fechas de ingreso a PT vs. despacho |
| `GUIAS_PEDIDO_LOTES` | Guías + pedidos + lotes (trazabilidad completa) |
| `VCC_CONTROLPROD` | Control de producción (calidad) |
| `VITEMDOC` / `V_ITEMDOC` | Ítems de documentos (facturas/notas) |
| `VHRECPED` | Histórico recepción-pedido |

---

## NOTAS CRÍTICAS PARA EL DESARROLLO .NET CORE

1. **Stock nunca manual**: `ALMACEN.STOCK` es mantenido 100% por triggers. Leer siempre de la tabla; nunca calcular en la app.

2. **PKs compuestas en Kardex y Producción**: Las entidades `KARDEX_G/D`, `H_PRODUCCION_G/D` usan PK compuesta de 4-5 campos. En Entity Framework usar Fluent API con `HasKey(e => new { e.CodAlm, e.TpTransac, e.Serie, e.Numero })`.

3. **Fechas en Oracle**: El esquema usa `NLS_DATE_FORMAT='DD-MON-RR'`. Usar siempre `TO_DATE(:p, 'DD/MM/YYYY')` en los parámetros desde .NET para evitar ambigüedad.

4. **SITU_PART como semáforo de tintorería**: El campo `PARTIDA.SITU_PART` funciona como máquina de estados del flujo tintorería. Los valores `R001` → `P` → `A` representan Recibida → En proceso → Acabada.

5. **Trazabilidad completa**: La vista `GUIAS_PEDIDO_LOTES` permite trazar desde el pedido hasta el lote físico individual. Usarla para el dashboard de seguimiento.

6. **Reprogramación de fechas**: `TT_PROGPARTD` guarda el historial completo de cambios de fecha de entrega con motivo y área responsable. Fundamental para KPIs de cumplimiento.

7. **Indicadores de urgencia**: `ITEMPED_DET.URGENTE` y `ITEMPED_DET.NIVEL_URGENCIA` controlan la prioridad de fabricación. El dashboard debe reflejar estos valores.

8. **Datos de tintorería para estimación**: `TT_PARAMPROGTIN` tiene los tiempos estándar de cada etapa (tenido, madeja, acabado, calidad, enconado, revisado). Usarlos para calcular la fecha estimada de entrega.

9. **Partidas con hallazgos**: La función `PARTIDAS_HALLAZGO` y la vista `VCC_PARTIDA` permiten detectar partidas con problemas de calidad antes de despachar.

10. **Exportación**: Los campos `CONTENEDOR`, `NRO_PRECINTO`, `T_PUERTO`, `C_PUERTO` en `KARDEX_G` manejan el flujo de exportación. Si el cliente es exportador, estos datos son obligatorios.


### 4.7 SINÓNIMOS

| Sinónimo | Objeto apuntado | Descripción |
|----------|----------------|-------------|
| `CC_NOCONFORME` | (objeto externo) | No conformidades CC |
| `SPE_ERROR_LOG` | (objeto externo) | Log de errores SPE |

---

## 5. RELACIONES CLAVE (FOREIGN KEYS PRINCIPALES)

### Cadena de Producción
```
FIBRA (PK: FIBRA)
  └── ARTICUL (FK: FIBRA → FIBRA.FIBRA) [disabled]
        └── ALMACEN (FK: COD_ART → ARTICUL.COD_ART)
              └── ART_LOTE (FK: COD_ART+COD_ALM → ALMACEN)
        └── LOTES (COD_ART)
        └── ITEMPED (COD_ART)
        └── CTRUTAS_TITULO_COLOR_D (COD_ART)

CTPROCESOS / H_PROCESOS (PK: CODIGO)
  └── ARTICUL (FK: PROCESO)
  └── CTRUTAS (FK: PROCESO)
  └── CTRUTAS_TITULO (FK: PROCESO)

PEDIDO (PK: SERIE + NUM_PED)
  └── ITEMPED (FK: SERIE + NUM_PED → PEDIDO)
        └── ITEMPED_DET (FK: SERIE + NUM_PED + NRO → ITEMPED)

FACTCOB (PK: TIPDOC + SERIE_NUM + NUMERO)
  └── CABFCOB (FK → FACTCOB)

FACTPAG (PK: COD_PROVEEDOR + TIPDOC + SERIE_NUM + NUMERO)
  └── CABFPAG (FK → FACTPAG)

KARDEX_G (PK: COD_ALM + TP_TRANSAC + SERIE + NUMERO)
  └── KARDEX_D (FK → KARDEX_G)
  └── KARDEX_L (FK → KARDEX_G)

PLANILLA (PK: C_CODIGO + NUM_PLA)
  └── APORTA_PLA (FK: C_CODIGO + NUM_PLA → PLANILLA)
  └── INGRE_PLA (FK)
  └── HORAS_PLA (FK)

L_PLANTILLA_G (PK: NUMERO)
  └── L_PLANTILLA_D (FK: NUMERO → L_PLANTILLA_G)

RECETA_G (PK: NUMERO)
  └── RECETA_D (FK: NUMERO → RECETA_G)

CJ_PLACOBH (PK: PLANILLA)
  └── CJ_PLACOBD (FK: PLANILLA → CJ_PLACOBH)
  └── CJ_PLACOBF (FK: PLANILLA → CJ_PLACOBH)

ACTIMOV_G (PK: TIPO + SERIE + NUMERO)
  └── ACTIMOV_D (FK → ACTIMOV_G)
  └── ACTIMOV_D (FK → ACTIVO_FIJO: CLASE+CODIGO+NUMERO)

COTIZACION_G (PK: TIPODOC + SERIE + NUMERO)
  └── COTIZACION_D (FK → COTIZACION_G)

MA_ACTIVIDAD_G (PK: COD_ACTIV)
  └── MA_ACTIVIDAD_D (FK → MA_ACTIVIDAD_G)
  └── MA_ACTIVIDAD_A (FK → MA_ACTIVIDAD_D)

CTCALIDAD_G (PK: NUMERO)
  └── CTCALIDAD_D (FK: NUMERO → CTCALIDAD_G)
```

---

## 6. ESTRUCTURA DE PARÁMETROS — PARAMPLA

La tabla `PARAMPLA` (4.593 filas) almacena todos los parámetros del módulo de planeamiento en formato clave-valor extendido. Estructura implícita: `(TIPO, CODIGO, SUBCOD, DESCRIPCION, VALOR, IND1..N)`.

---

## 7. NOMENCLATURA / CONVENCIONES DE LA BD

| Convención | Descripción |
|------------|-------------|
| `_G` suffix | Cabecera (General/Glosa) de un documento |
| `_D` suffix | Detalle de un documento |
| `_L` suffix | Lote asociado a un documento |
| `_H_` prefix | Historial / Histórico |
| `_TMP` suffix | Tabla temporal |
| `_BCK` suffix | Backup |
| `A_ADUSER` / `A_ADFECHA` | Auditoría: usuario/fecha de creación |
| `A_MDUSER` / `A_MDFECHA` | Auditoría: usuario/fecha de modificación |
| `TP_ART` | Tipo de artículo: 'M'=MP, 'P'=PT, 'S'=servicio, etc. |
| `CAN_AUTHE` base date | Tiempos almacenados como DATE base 01/01/1900 |
| `COD_ALM` | Código de almacén (2 chars) |
| `COD_ART` | Código de artículo (hasta 25 chars) |
| `COD_MAQ` | Código de máquina (6 chars) |
| `C_CODIGO` | Código de personal/trabajador (8 chars) |
| `NCOLOR` | Número/código de color (7 chars) |
| `FIBRA` | Código de tipo de fibra |
| `TITULO` | Denominación del hilo (Ne, Nm, dtex) |
| `PROCESO` | Proceso productivo (hilatura, preparatoria, etc.) |
| `ESTADO` | '0'=activo, '1'=inactivo o según módulo |
| `MA_` prefix | Mantenimiento |
| `RH_` prefix | Recursos Humanos |
| `CS_` prefix | Sistemas / IT |
| `CT_` prefix | Control de producción / calidad |
| `SI_` prefix | Seguridad Industrial |
| `TT_` prefix | Tintorería (producción) |
| `II_` prefix | Insumos e ingredientes (químicos) |
| `L_` prefix | Laboratorio / Planeamiento |
| `CJ_` prefix | Caja / Cobranza planificada |
| `PLA_` prefix | Planilla (RRHH) |
| `PKG_` prefix | Paquetes PL/SQL |
| `TIB_` prefix | Trigger BEFORE |
| `TIA_` prefix | Trigger AFTER |
| `TDA_`/`TDB_` prefix | Trigger DELETE AFTER/BEFORE |

---

## 8. TABLAS DE CONTROL DE PRODUCCIÓN (RESUMEN EJECUTIVO)

```
                    ┌──────────────────────────────────────────┐
                    │         PLANEAMIENTO DE PLANTA           │
                    │   L_PLANTILLA_G/D  ←→  L_PLA_PARAM_*   │
                    │   PARAMPLA · HORAS_PLA · PLA_ANUAL       │
                    └─────────────────┬────────────────────────┘
                                      │
              ┌───────────────────────┼───────────────────────┐
              │                       │                       │
    ┌─────────▼────────┐  ┌───────────▼─────────┐  ┌────────▼────────────┐
    │   RUTAS/RECETAS  │  │   PROGRAMACIÓN       │  │   PARÁMETROS MAQ   │
    │  CTRUTAS_*       │  │   H_PROGRAMACION     │  │   CTSECCIONES      │
    │  RECETA_G/D      │  │   MA_PROGRAMA        │  │   CTTABCOS         │
    │  ING_RECETAS_*   │  │   PR_PROGRAMA        │  │   CARGA_MAQ        │
    └─────────┬────────┘  └───────────┬─────────┘  └────────────────────┘
              │                       │
    ┌─────────▼──────────────────────▼─────────────────────────────────┐
    │                    PRODUCCIÓN REAL                                │
    │  H_PRODUCCION_G/D/A/GA  ·  TT_RPRODUC  ·  TT_PROGPART           │
    │  H_RPRODUC  ·  H_PARADAS  ·  TT_RSECADO  ·  H_ROLLOS            │
    └───────────────────────────────────────────────────────────────────┘
              │
    ┌─────────▼──────────────────────────────────────────────────────────┐
    │                    CONTROL DE CALIDAD                              │
    │  CTCALIDAD_G/D  ·  LOTES  ·  PARTIDA  ·  NOCONFORMIDAD_G/D       │
    └────────────────────────────────────────────────────────────────────┘
              │
    ┌─────────▼──────────────────────────────────────────────────────────┐
    │                    ALMACÉN / KARDEX                                │
    │  ALMACEN  ·  KARDEX_G/D/L  ·  ART_LOTE  ·  HISTART               │
    └────────────────────────────────────────────────────────────────────┘
              │
    ┌─────────▼──────────────────────────────────────────────────────────┐
    │              PEDIDOS → DESPACHO → FACTURACIÓN                      │
    │  PEDIDO  ·  ITEMPED  ·  PARTIDA  ·  PACKING_*  ·  DOCUVENT        │
    │  FACTCOB  ·  LETRAS  ·  COBROS  ·  SALDOS_CXC                     │
    └────────────────────────────────────────────────────────────────────┘
```

---

## 9. NOTAS TÉCNICAS

1. **Charset**: `WE8ISO8859P15` — los textos en español con tildes se almacenan en ISO-8859-15. Al consultar desde .NET usar `ODP.NET` con la configuración correcta de NLS.

2. **Auditoría estándar**: Todas las tablas transaccionales tienen `A_ADUSER`, `A_ADFECHA`, `A_MDUSER`, `A_MDFECHA` gestionados por triggers `TIB_*_AUD`.

3. **Fechas**: NLS_DATE_FORMAT = `DD-MON-RR`. Siempre usar `TO_DATE('...', 'DD/MM/YYYY')` en scripts PL/SQL.

4. **Objetos inválidos**: `POST_TO_API` y `REGISTRA_LOGIN` están en estado INVALID. Lo más probable es que sean código legado nunca utilizado activamente (llevan inválidos desde siempre). **No bloquean el módulo PLN_**, que no los referencia en ningún trigger ni procedimiento.

5. **Integración AQUARIUS**: 5 triggers sincronizan datos de RRHH/personal hacia el sistema de asistencia Aquarius (esquema `AQUARIUS` en el mismo servidor).

6. **Tablas más grandes**: `INGRE_PLA` (37M filas), `HISTART` (8.8M filas), `ING_RECETAS_D` (4.8M filas), `MOVDETA` (6.4M filas), `H_EQUIVALENCIA` (5.5M filas). Usar siempre filtros de fecha/periodo.

7. **Numeración de documentos**: Controlada por tablas `NUMDOC`, `NRODOC`, `NUMLOTES`, `NUMPROD`, `NROLIBR`, `NROMANT`. Actualización via triggers `TIB_NRODOC`, etc.

8. **Unidades de producción**: Kg con 4 decimales (`NUMBER(12,4)`), costos con 6-10 decimales, porcentajes con 6 decimales.

9. **Módulo Planeamiento (L_)**: Gestiona la plantilla de colores/intensidades para determinar la receta de tintorería óptima para cada pedido. Relacionado con `CTRUTAS_TITULO_COLOR` (costo estándar) y `RECETA_G/D` (formulación real).

10. **Sin triggers de FK explícitos**: Varias FKs están `DISABLED` (ej. `FK_ARTICUL_FIBRA`). La integridad referencial se maneja a nivel de aplicación en esos casos.

---

## 10. ANÁLISIS PROFUNDO DE DATOS — FLUJO REAL PEDIDO → DESPACHO

> Sección generada tras análisis empírico con datos reales de pedido 88586 (14/04/2026). Actualiza y profundiza los pasos 1-12.

---

### 10.1 CAMPO ITEMPED.PROCESO — Significado Correcto

`ITEMPED.PROCESO` y `ITEMPED_DET.PROCESO` indican el **tipo de proceso de hilatura** del hilo que se está pidiendo, NO el proceso de tintorería.

Catálogo `H_PROCESOS` (valores más frecuentes):

| PROCESO | Descripción | % Pedidos |
|---------|-------------|-----------|
| `'01'` | CARDADO | ~60% |
| `'20'` | PEINADO | ~25% |
| `'24'` | PEINADO GASEADO | ~10% |
| `'26'` | PEINADO GASEADO MERCERIZADO | ~3% |
| `'90'` | RETORCIDO | — |
| `'00'` | MADEJERO | — |

El proceso `'24'` PEINADO GASEADO implica que después del teñido, la partida pasará por un proceso adicional de **gaseado (GAS)** en una máquina específica antes de ir al devanado. La hilandería produce el hilo como PEINADO (`'20'`) y el gaseado lo convierte en PEINADO GASEADO (`'24'`).

---

### 10.2 DOS RUTAS PARALELAS (PATH A y PATH B)

Cada ítem de pedido sigue **dos rutas paralelas** que convergen en la PARTIDA:

```
ITEMPED (ítem pedido)
    │
    ├──[PATH A — HILANDERÍA]──────────────────────────────────────────────┐
    │   H_RECETA_G (NUM_PED + ITEM_PED + LOTE → receta de hilatura)       │
    │       │                                                              │
    │       └── H_RPRODUC (producción real por máquina y tipo)            │
    │               TP_MAQ: B=BATAN | L=CARDA | M=MANUAR                  │
    │                        P=PABILERA | C=CONTINUA | T=RETORCEDORA       │
    │               Máquinas reales: BAT01/02, SS21-SS30, PEIN01-04,      │
    │                                MA01-10, PAB01-06, etc.              │
    │                                                                      │
    └──[PATH B — TINTORERÍA]──────────────────────────────────────────────┘
        ITEMPED_DET (NROPROG → programa específico de tintorería)
            │ 1:1
            ▼
        PARTIDA.NROPROG (lote físico del hilo no teñido)
            │
            ├── PARTIDA_MAS → ING_RECETAS_G (1 o más baños de tintura)
            │       │              PROCESO: TE=Teñido | BQM=Blanqueo
            │       │              COD_MAQ: R01-R19 (Thies), M01-M08 (Hank)
            │       │
            │       └── TT_RPRODUC (corrida real en máquina)
            │               ESTADO: '1'=Iniciado | '3'=Terminado
            │
            ├── TT_RSECADO (GUIA = PARTIDA.NUMERO)
            │
            ├── CTCALIDAD_D (NRO_PEDIDO + SER_PARTIDA, EST: '32'=OK, '13'=Pend, '02'=Reval)
            │
            ├── H_PROGRAMACION (GUIA = PARTIDA.NUMERO — devanado)
            │
            ├── REVISADO_G/D (GUIA = PARTIDA.NUMERO)
            │
            ├── LOTES (COD_ALM='03'/'07', TP_TRANSAC='16', PARTIDA = PARTIDA.NUMERO)
            │
            └── KARDEX_L (S_TRANSAC IN ('21','23') — despacho)
```

**Conector entre paths**: El campo `LOTE` (varchar)
- `H_RECETA_G.LOTE` = `ITEMPED_DET.LOTE` = `PARTIDA.LOTE_PRODUC`

---

### 10.3 EL LOTE COMO UNIDAD PRODUCCIÓN

El LOTE representa una cantidad de hilo crudo (sin teñir) producida en hilandería.

**Hallazgo clave**: Un LOTE puede producirse para un pedido pero **consumirse en otro**.

Ejemplo real:
- LOTE `'1176-M'` producido para pedido **87442** (Nov 2025): TITULO=040, PROCESO=20 (PEINADO), 1000 kg
- Mismo LOTE usado en pedido **88586** (Abr 2026): PROCESO=24 (PEINADO GASEADO)
- El gaseado post-teñido convierte el hilo de PEINADO → PEINADO GASEADO

**Implicación para planeamiento**: La disponibilidad de LOTE en stock es la señal de que hilandería terminó su parte. El sistema de planeamiento debe consultar `H_RECETA_G` + `H_RPRODUC` para saber si el LOTE está producido o en producción.

---

### 10.4 LINK TABLES — RELACIONES CLAVE

| Relación | Tabla | Campo(s) | Cardinalidad |
|----------|-------|----------|--------------|
| ITEMPED_DET → PARTIDA | `PARTIDA` | `PARTIDA.NROPROG = ITEMPED_DET.NROPROG` | 1:1 |
| PARTIDA → ING_RECETAS | `PARTIDA_MAS` | `PARTIDA_MAS.PARTIDA = PARTIDA.NUMERO`, `PARTIDA_MAS.RECETA = ING_RECETAS_G.NUMERO` | 1:N |
| ING_RECETAS → TT_RPRODUC | `TT_RPRODUC` | `TT_RPRODUC.RECETA = ING_RECETAS_G.NUMERO` | 1:N |
| PARTIDA → TT_RSECADO | `TT_RSECADO` | `TT_RSECADO.GUIA = PARTIDA.NUMERO` | 1:1 |
| PARTIDA → H_PROGRAMACION | `H_PROGRAMACION` | `H_PROGRAMACION.GUIA = PARTIDA.NUMERO` | 1:N |
| PARTIDA → REVISADO_G | `REVISADO_G` | `REVISADO_G.GUIA = PARTIDA.NUMERO` | 1:N |
| PARTIDA → LOTES | `LOTES` | `LOTES.PARTIDA = PARTIDA.NUMERO` | 1:N |
| ITEMPED → CTCALIDAD_D | `CTCALIDAD_D` | `NRO_PEDIDO + SER_PARTIDA` | 1:N |

---

### 10.5 NUM_DET > 0 — PARTICIÓN DE ÍTEMS

Un mismo ítem de pedido (`ITEMPED`) puede dividirse en múltiples sub-programaciones usando el campo `ITEMPED_DET.NUM_DET`:

- `NUM_DET = 0` → Programación principal (única)
- `NUM_DET > 0` → Sub-lotes del mismo ítem para diferentes máquinas o lotes de producción

Ejemplo real: ítem 2 del pedido 47888 → NUM_DET=1 (R09, 700 kg) + NUM_DET=2 (R09, 326 kg). Esto indica que el ítem se dividió en dos máquinas para cumplir con capacidad.

**Para planeamiento**: La cantidad total del ítem = suma de `ITEMPED_DET.CANTIDAD` para todos los NUM_DET del mismo ítem.

---

### 10.6 V_STATUS_PEDIDO — PIPELINE DE 9 ETAPAS

La vista `V_STATUS_PEDIDO` rastrea cada ítem (NRO_PEDIDO + SER_PARTIDA) a través de 9 etapas ordenadas:

| Etapa | Nombre | Tabla fuente | Condición |
|-------|--------|--------------|-----------|
| 1 | LABORATORIO | `L_VALIDA_RECETA` | registro existe |
| 2 | RECETA | `V_RECETAPARTIDA` / `RECETA_G` | receta validada |
| 3 | TINTORERIA | `TT_RPRODUC` | ESTADO IN ('1','3') |
| 4 | SECADORA | `TT_RSECADO` | ESTADO IN ('1','3') |
| 5 | CCAL-TINTO | `CTCALIDAD_D` | EST_EVALUACION IN ('13','02','32') |
| 6 | DEVANADO | `H_PROGRAMACION` | ESTADO IN ('3','6'), GUIA=PARTIDA |
| 7 | REVISADO | `REVISADO_G/D` | APROBADO > 0 |
| 8 | ALMACEN-PT | `LOTES` | COD_ALM IN ('03','07'), TP_TRANSAC='16', PARTIDA IS NOT NULL |
| 9 | ALMACEN-PT EN DESPACHO | `KARDEX_L` | S_TRANSAC IN ('21','23') |

**Uso recomendado**: Para el dashboard de seguimiento, consultar esta vista agrupando por pedido para obtener el porcentaje de ítems en cada etapa.

---

### 10.7 EST_EVALUACION — CONTROL DE CALIDAD (CTCALIDAD_D)

| EST_EVALUACION | Significado |
|----------------|-------------|
| `'13'` | Pendiente de evaluación |
| `'02'` | Necesita re-evaluación (observado) |
| `'32'` | **Aprobado** (RESULTADO='01') |

El campo `RESULTADO='01'` confirma que el lote pasa a la siguiente etapa. `RESULTADO=''` (vacío) indica resultado pendiente o en proceso.

---

### 10.8 TABLA SEGUIMIENTO — TRACKING EXISTENTE

La tabla `SEGUIMIENTO` (sin prefijo PLN_) ya existe en la BD y registra eventos del ciclo de vida de cada ítem de pedido:

```sql
SEGUIMIENTO (NUM_PED, NRO, PARTICION, FECHA, AREA, ACCION)
```

| AREA | Eventos registrados |
|------|---------------------|
| `'PLANEAMIENTO'` | "PROGRAMADO XX" — cuando se crea ITEMPED_DET |
| `'CCALIDAD'` | "SIN EVALUACION..." — estado de CC |
| `'PROG HILAND'` | Eventos de programación de hilandería |
| `'REVISADO'` | "XX / notes" — resultado revisado |

> **IMPORTANTE**: La tabla `SEGUIMIENTO` cubre el tracking a nivel de ítem de pedido. Los triggers sobre `PARTIDA`, `TT_RPRODUC`, `TT_RSECADO`, `CTCALIDAD_D`, `REVISADO_G` ya generan eventos. El diseño de `PLN_LOG_EVENTOS` en Propuesta.md debe **complementar** (no duplicar) a SEGUIMIENTO, enfocándose en eventos de planeamiento/capacidad (asignación de máquina, cambio de fecha, alerta de retraso).

---

### 10.9 EJEMPLO TRAZADO — PEDIDO 88586 (14/04/2026)

**Pedido**: 88586 · Cliente: 20511653 · 14 ítems

| Ítems | PROCESO | TITULO | LOTE | Kg aprox | Máquinas TT |
|-------|---------|--------|------|----------|-------------|
| 1-6 | `'24'` PEINADO GASEADO | 040 | 1176-M | 53 kg c/u | R08/R12/M01/M07 |
| 7-8 | `'20'` PEINADO | 057 | 61 | — | R19/M01 |
| 9-14 | `'01'` CARDADO | 014 | 61 | — | R08/R18/M08/M07 |

**Traza completa del ítem 1** (NRO=1, SER_PARTIDA=1):

```
ITEMPED_DET.NROPROG = 157724
    → MAQUINA=R08 | FHC_PROG=13/05/26 | FHC_ENTREGA=22/05/26
    → LOTE='1176-M'

PARTIDA 158939 (NROPROG=157724)
    → COD_MAQ=R07 | FECHA=08/05/26 | PESO_NETO=53.688 kg
    → LOTE_PRODUC='1176-M'

PARTIDA_MAS 158939:
    → ING_RECETAS 469500: PROCESO=TE (Teñido), MAQUINA=R07, RECETA_G=54310
                          Cliente=20511653, TITULO=040, PROCESO1=24, COLOR=513145
    → ING_RECETAS 469594: PROCESO=BQM (Blanqueo/Quim), MAQUINA=R03, ESTADO='5'

TT_RPRODUC:
    → 469500 → R08 | ESTADO=3 (Terminado) | 12/05/26 → 13/05/26
    → 469594 → R03 | ESTADO=3 (Terminado) | 10/05/26

CTCALIDAD_D: ítem 1 → SIN DATOS (calidad pendiente al 16/05/26)
SEGUIMIENTO: PLANEAMIENTO='PROGRAMADO XX' (14/04/26), resto pendiente
```

**Estado al 16/05/2026**: El ítem 1 está en **etapa 3 (TINTORERIA completada)**, esperando CCALIDAD-TINTO. El resto del pedido (ítems 2-9, 12-13) ya tiene CTCALIDAD_D con `EST_EVALUACION='32'` (aprobado). Ítems 7-9 en REVISADO_G aprobados.

---

### 10.10 MÁQUINAS POR ÁREA

#### Tintorería (TT_MAQUINA)
| Rango | Tipo |
|-------|------|
| R01–R19 | Thies (teñido a presión) |
| M01–M08 | Hank Master (madejeras) |
| MR2 | Máquina de reproceso 2 |

#### Hilandería (H_RPRODUC)
| TP_MAQ | Proceso | Máquinas |
|--------|---------|----------|
| `'B'` | Batán (apertura) | BAT01, BAT02 |
| `'L'` | Carda | SS21–SS30 (semi-peinado), PEIN01–PEIN04 |
| `'M'` | Manuar | MA01–MA10 |
| `'P'` | Pabilera | PAB01–PAB06 |
| `'C'` | Continua (hilatura final) | HI01–HI30 aprox. |
| `'T'` | Retorcedora | RET01–RET06 aprox. |

#### Acabados post-tintura
| Proceso | Descripción |
|---------|-------------|
| `GAS` | Gaseado (convierte PEINADO → PEINADO GASEADO) |
| `RED03`, `10 AUT` | Máquinas de devanado/revisado |

---

### 10.11 CONSULTAS DIAGNÓSTICO PARA EL SISTEMA

```sql
-- Estado completo de un pedido en el pipeline
SELECT ser_partida, nro_pedido, etapa_actual, fecha_etapa
FROM v_status_pedido
WHERE nro_pedido = :num_ped
ORDER BY ser_partida;

-- Partidas del pedido con su situación actual
SELECT p.numero, p.ser_partida, p.situ_part, p.peso_neto, p.lote_produc,
       p.cod_maq, p.fecha, id.nroprog, id.maquina prog_maq,
       id.fhc_entrega fecha_comprometida
FROM partida p
JOIN itemped_det id ON id.nroprog = p.nroprog
WHERE p.nro_pedido = :num_ped
ORDER BY p.ser_partida;

-- Recetas de tintorería activas para una partida
SELECT irg.numero, irg.proceso, irg.maquina, irg.peso_neto,
       irg.estado, irg.fecha, rg.numero receta_master, rg.color
FROM partida_mas pm
JOIN ing_recetas_g irg ON irg.numero = pm.receta
JOIN receta_g rg ON rg.numero = irg.r_numero
WHERE pm.partida = :num_partida
ORDER BY irg.fecha;

-- Producción real tintorería de una receta
SELECT tr.receta, tr.proceso, tr.cod_maq, tr.fecha_ini, tr.fecha_fin,
       tr.estado, tr.calificacion
FROM tt_rproduc tr
WHERE tr.receta IN (
    SELECT receta FROM partida_mas WHERE partida = :num_partida
)
ORDER BY tr.fecha_ini;

-- Carga de máquinas tintorería (próximas 7 días)
SELECT id.maquina, COUNT(*) partidas_prog,
       SUM(p.peso_neto) kg_programados,
       MIN(id.fhc_prog) proxima_fecha
FROM itemped_det id
JOIN partida p ON p.nroprog = id.nroprog
WHERE id.fhc_prog BETWEEN TRUNC(SYSDATE) AND TRUNC(SYSDATE) + 7
AND id.maquina IN (SELECT cod_maq FROM tt_maquina)
AND p.situ_part NOT IN ('A','X')
GROUP BY id.maquina
ORDER BY kg_programados DESC;

-- LOTES disponibles de hilo crudo para un título/proceso
SELECT lote, SUM(saldo) kg_disponible, COUNT(*) partidas
FROM partida
WHERE titulo = :titulo AND proceso1 = :proceso
AND estado NOT IN ('6','9')
AND saldo > 0
GROUP BY lote
ORDER BY kg_disponible DESC;

-- Seguimiento de eventos de un ítem
SELECT s.fecha, s.area, s.accion, s.particion
FROM seguimiento s
WHERE s.num_ped = :num_ped AND s.nro = :nro
ORDER BY s.fecha, s.particion;
```

---

*Documento actualizado: 16/05/2026 · Sección 10 agregada tras análisis empírico con datos reales del sistema SIG*

---

## 11. ANÁLISIS CRÍTICO — BUGS, GAPS Y CONSIDERACIONES (18/05/2026)

> Resultado de una revisión exhaustiva de `Propuesta.md` cruzada con el flujo real PEDIDO → DESPACHO.
> El análisis parte de la premisa: **todo comienza en el ITEMPED (ítem del pedido) y la trazabilidad debe seguir cada ítem individualmente hasta el despacho físico**.
> Se identifican 3 categorías: (A) Bugs que romperían el sistema, (B) Gaps de trazabilidad, (C) Escenarios de negocio no cubiertos.

---

### 11.1 BUGS CRÍTICOS — Propuesta SP / Triggers

---

#### BUG 1 — `SP_PLN_AVANZA_PASO`: FCH_REAL_PARTIDA asignada en PASO incorrecto

**Código actual (incorrecto):**
```sql
FCH_REAL_PARTIDA = CASE WHEN p_nuevo_paso='05' THEN SYSDATE ... END,
```
**Problema**: PASO `'05'` = Laboratorio (L_VALIDA_RECETA). La PARTIDA (lote físico) se crea cuando el hilo crudo termina de producirse en hilandería, es decir al PASO `'04'` (Lote disponible). Asignar `FCH_REAL_PARTIDA` en el PASO de laboratorio hace que el campo quede vacío si se registra calidad antes de crear la partida.

**Corrección:**
```sql
FCH_REAL_PARTIDA = CASE WHEN p_nuevo_paso='04' THEN SYSDATE ELSE FCH_REAL_PARTIDA END,
```

---

#### BUG 2 — `SP_PLN_AVANZA_PASO`: FCH_REAL_TIN_FIN asignada junto con secado (PASO 08)

**Código actual (incorrecto):**
```sql
FCH_REAL_TIN_FIN = CASE WHEN p_nuevo_paso='08' THEN SYSDATE ... END,
FCH_REAL_SECADO  = CASE WHEN p_nuevo_paso='08' THEN SYSDATE ... END,
```
**Problema**: Ambas fechas se registran en el mismo paso `'08'` (Secado), haciendo `FCH_REAL_TIN_FIN = FCH_REAL_SECADO`. Pero el fin del teñido ocurre cuando el **último** `TT_RPRODUC.ESTADO='3'` (Terminado) de la partida se registra, ANTES de que entre a secado. El gap entre `FCH_REAL_TIN_FIN` y `FCH_REAL_SECADO` es el tiempo de espera entre fin de baño y entrada a secadora, que puede ser horas.

**Corrección**: `FCH_REAL_TIN_FIN` debe asignarse en el trigger `TIA_PLN_FROM_TT_RPRODUC` cuando el proceso TT queda en ESTADO='3' (terminado), específicamente cuando no quedan más procesos pendientes para esa PARTIDA.
```sql
-- En SP_PLN_AVANZA_PASO:
FCH_REAL_TIN_FIN = CASE WHEN p_nuevo_paso='07' AND p_observacion LIKE '%TERMINADO%'
                         THEN SYSDATE ELSE FCH_REAL_TIN_FIN END,
FCH_REAL_SECADO  = CASE WHEN p_nuevo_paso='08' THEN SYSDATE ELSE FCH_REAL_SECADO END,
```

---

#### BUG 3 — `SP_PLN_AVANZA_PASO`: ESTADO='C' se cierra en PASO '12' (Almacén PT), no en PASO '14' (Despacho)

**Código actual (incorrecto):**
```sql
ESTADO = CASE WHEN p_nuevo_paso='12' THEN 'C' ELSE ESTADO END,
```
**Problema**: El PASO `'12'` = Ingresado Almacén PT. El ítem todavía **no fue despachado al cliente**. Cerrarlo (ESTADO='C') en este punto hace que el KPI de despacho, las alertas de retraso y el panel de pendientes dejen de funcionar para este ítem. El cierre correcto es PASO `'14'` (Despachado/Cerrado). Además, si hay despacho parcial (se despacha el 60% y queda el 40% en almacén), el ESTADO='C' prematuro oculta el saldo pendiente.

**Corrección:**
```sql
ESTADO = CASE WHEN p_nuevo_paso='14'
               AND (KG_DESPACHADOS + NVL(p_kg_cantidad,0)) >= CANTIDAD_ORIG
              THEN 'C' ELSE ESTADO END,
```

---

#### BUG 4 — `SP_PLN_AVANZA_PASO`: KG_DESPACHADOS se actualiza en PASO '12' (Almacén PT)

**Código actual (incorrecto):**
```sql
KG_DESPACHADOS = CASE WHEN p_nuevo_paso='12' THEN KG_DESPACHADOS + NVL(p_kg_cantidad,0) ... END,
KG_PENDIENTES  = CASE WHEN p_nuevo_paso='12' THEN GREATEST(KG_PENDIENTES - NVL(p_kg_cantidad,0),0) ... END,
```
**Problema**: El despacho real es PASO `'14'` (KARDEX_G TP='22'/'23'). Actualizar `KG_DESPACHADOS` al ingresar al almacén PT (`'12'`) es incorrecto porque el hilo puede estar en almacén días o semanas antes de salir. La vista `V_PLN_KPI_CUMPLIMIENTO` y `V_PLN_PENDIENTES_DESP` quedarían con datos falsos.

**Corrección**: Mover la actualización de `KG_DESPACHADOS` y `KG_PENDIENTES` a PASO `'14'`:
```sql
KG_EN_ALM_PT   = CASE WHEN p_nuevo_paso='12' THEN KG_EN_ALM_PT + NVL(p_kg_cantidad,0)  ELSE KG_EN_ALM_PT   END,
KG_DESPACHADOS = CASE WHEN p_nuevo_paso='14' THEN KG_DESPACHADOS + NVL(p_kg_cantidad,0) ELSE KG_DESPACHADOS END,
KG_PENDIENTES  = CASE WHEN p_nuevo_paso='14' THEN GREATEST(KG_PENDIENTES - NVL(p_kg_cantidad,0),0) ELSE KG_PENDIENTES END,
```

---

#### BUG 5 — `SP_PLN_AVANZA_PASO`: KG_PRODUCIDOS se acumula en PASO '04' Y '05'

**Código actual (incorrecto):**
```sql
KG_PRODUCIDOS = CASE WHEN p_nuevo_paso IN ('04','05') THEN KG_PRODUCIDOS + NVL(p_kg_cantidad,0) ... END,
```
**Problema**: PASO `'05'` = Laboratorio (L_VALIDA_RECETA). El laboratorio no produce KG, solo valida la receta. Incluir `'05'` en esta condición duplica el peso producido si se llama el SP dos veces (primero al pasar a '04', luego al pasar a '05').

**Corrección:**
```sql
KG_PRODUCIDOS = CASE WHEN p_nuevo_paso='04' THEN KG_PRODUCIDOS + NVL(p_kg_cantidad,0) ELSE KG_PRODUCIDOS END,
KG_EN_TIN     = CASE WHEN p_nuevo_paso IN ('06','07') THEN KG_EN_TIN + NVL(p_kg_cantidad,0) ELSE KG_EN_TIN END,
```

---

#### BUG 6 — `TUA_PLN_FROM_PARTIDA`: Usa `:NEW.num_det` que NO existe en la tabla PARTIDA

**Código actual (incorrecto):**
```sql
SP_PLN_AVANZA_PASO(
    v_serie, :NEW.nro_pedido, v_nro, :NEW.num_det,   -- ← :NEW.num_det NO EXISTE en PARTIDA
    v_paso, 'PARTIDA', ...
```
**Problema**: La tabla `PARTIDA` no tiene campo `NUM_DET`. El trigger fallará en runtime con `ORA-00904: "NUM_DET": invalid identifier`. La compilación del trigger no detecta este error porque es un trigger con `EXCEPTION WHEN OTHERS THEN NULL`, que silencia todos los errores.

**Corrección**: Derivar `NUM_DET` desde `ITEMPED_DET` usando `NROPROG`:
```sql
DECLARE
    v_paso    VARCHAR2(2);
    v_serie   NUMBER;
    v_nro     NUMBER;
    v_num_det NUMBER := 0;  -- default
BEGIN
    ...
    BEGIN
        SELECT d.serie, d.nro, d.num_det
        INTO v_serie, v_nro, v_num_det
        FROM itemped_det d
        WHERE d.nroprog = :NEW.nroprog AND rownum = 1;
    EXCEPTION WHEN NO_DATA_FOUND THEN RETURN;
    END;

    SP_PLN_AVANZA_PASO(v_serie, :NEW.nro_pedido, v_nro, v_num_det, ...);
```

---

#### BUG 7 — `TIA_PLN_FROM_TT_RPRODUC`, `TIA_PLN_FROM_TT_RSECADO`, `TIA_PLN_FROM_KARDEX_DESPACHO`: SERIE y NUM_DET hardcodeados en 1

**Código actual (incorrecto):**
```sql
SP_PLN_AVANZA_PASO(
    1,          -- ← SERIE siempre = 1 (INCORRECTO)
    v_num_ped,
    v_nro,
    1,          -- ← NUM_DET siempre = 1 (INCORRECTO)
    ...
```
**Problema**: `PEDIDO.SERIE` no es siempre 1 — puede ser cualquier número del catálogo de series. Si el pedido es SERIE=2 o SERIE=3, el `SP_PLN_AVANZA_PASO` buscará un registro con SERIE=1 en PLN_SEGUIMIENTO que no existe, y el avance se silenciará con `WHEN NO_DATA_FOUND THEN NULL`. La trazabilidad fallará silenciosamente para todos los pedidos de series ≠ 1.

**Corrección**: Obtener SERIE desde la tabla fuente en cada trigger:
```sql
-- En TIA_PLN_FROM_TT_RPRODUC:
SELECT ig.num_ref, ig.item_ref, id.serie, id.num_det
INTO v_num_ped, v_nro, v_serie, v_num_det
FROM ing_recetas_g ig
JOIN partida_mas pm ON pm.receta = ig.numero
JOIN partida p ON p.numero = pm.partida
JOIN itemped_det id ON id.nroprog = p.nroprog
WHERE ig.numero = :NEW.receta AND ig.tipo_ref = 'PE'
AND rownum = 1;
```

---

#### BUG 8 — `TIA_PLN_FROM_CTCALIDAD`: Usa `:NEW.ser_partida` como `num_det`

**Código actual (incorrecto):**
```sql
SP_PLN_AVANZA_PASO(
    v_serie, :NEW.nro_pedido, v_nro, :NEW.ser_partida,   -- ← SER_PARTIDA ≠ NUM_DET
    '09', ...
```
**Problema**: `SER_PARTIDA` en `CTCALIDAD_D` es la serie de la partida (número que identifica la partición interna de la partida, puede ser 1, 2, 3…), NO el `NUM_DET` de `ITEMPED_DET` (que indica el sub-lote del ítem del pedido: 0, 1, 2…). Son campos de semántica completamente diferente. Esto causaría actualizaciones al registro PLN equivocado.

**Corrección**: Derivar `NUM_DET` desde PARTIDA → ITEMPED_DET usando el NUMERO de PARTIDA:
```sql
SELECT d.serie, d.nro, d.num_det
INTO v_serie, v_nro, v_num_det
FROM partida p
JOIN itemped_det d ON d.nroprog = p.nroprog
WHERE p.numero = :NEW.guia AND rownum = 1;   -- usar guia, no ser_partida
```

---

#### BUG 9 — `V_PLN_PENDIENTES_DESP`: Filtro sobre pasos incorrectos

**Código actual (incorrecto):**
```sql
WHERE s.cod_paso_act IN ('10','11')   -- PASO 10=Devanado, 11=Revisado
```
**Problema**: Los ítems en PASO '10' (Devanado) y '11' (Revisado) todavía no están en almacén PT. Los ítems listos para despachar están en PASO `'12'` (Ingresado Almacén PT) y PASO `'13'` (Listo para Despacho).

**Corrección:**
```sql
WHERE s.cod_paso_act IN ('12','13')
```

---

#### BUG 10 — `V_PLN_KPI_CUMPLIMIENTO`: Considera cerrado en PASO '12', no en PASO '14'

**Código actual (incorrecto):**
```sql
WHERE s.cod_paso_act = '12'
  AND s.fch_real_despacho IS NOT NULL
```
**Problema**: PASO `'12'` = Almacén PT, NO Despachado. El despacho real está en PASO `'14'`. Los KPIs de cumplimiento OTIF estarían midiendo "ingresó a almacén" en vez de "salió al cliente".

**Corrección:**
```sql
WHERE s.estado = 'C'        -- solo items cerrados
  AND s.fch_real_despacho IS NOT NULL
  AND s.cod_paso_act = '14' -- paso final confirmado
```

---

#### BUG 11 — `SP_PLN_CALCULA_FECHAS`: Usa MAX(kgr_hr) en vez de tasa de la máquina asignada

**Código actual (incorrecto):**
```sql
SELECT MAX(t.kgr_hr) INTO v_kgr_hr
FROM ctrutas_titulo t
WHERE t.titulo = v_item.titulo AND t.proceso = v_item.proceso
```
**Problema**: `MAX(kgr_hr)` selecciona la máquina **más rápida** posible para ese título/proceso. Esto produce una estimación optimista que no corresponde a la máquina realmente asignada en `ITEMPED_DET.MAQUINA`. Si la máquina asignada tiene `kgr_hr` menor, la fecha estimada será inalcanzable.

**Corrección**: Usar la tasa de la máquina específica asignada:
```sql
SELECT NVL(ct.kgr_hr, 10) INTO v_kgr_hr
FROM ctrutas_titulo ct
WHERE ct.titulo  = v_item.titulo
  AND ct.proceso = v_item.proceso
  AND ct.cod_maq = NVL(v_itemdet.maquina, ct.cod_maq)   -- máquina asignada
  AND ct.estado != 'X'
  AND rownum = 1;
```

---

#### BUG 12 — `SP_PLN_CALCULA_FECHAS`: No incluye buffer de Laboratorio entre hilandería y tintorería

**Código actual (incorrecto):**
```sql
v_est_part := TRUNC(v_fch_base) + CEIL(v_item.cantidad / NULLIF(v_kgr_hr * v_hrs_hil, 0));
v_est_tini := v_est_part;   -- ← laboratorio no tiene tiempo = 0 días
```
**Problema**: El laboratorio que valida la receta de teñido (PASO '05') tarda tiempo real (≈1 día per `Reunion.md`). Si `v_est_tini = v_est_part`, la estimación de entrada a tintorería ignora el tiempo de laboratorio y es optimista por al menos 1 día hábil.

**Corrección**: Agregar parámetro y leer de PLN_PARAM:
```sql
-- Agregar a PLN_PARAM:
INSERT INTO PLN_PARAM VALUES ('DIAS_BUFFER_LAB', 'Días buffer laboratorio (receta)', 1, NULL, NULL, USER, SYSDATE);

-- En SP_PLN_CALCULA_FECHAS:
v_buf_lab  NUMBER := 1;
BEGIN SELECT valor_num INTO v_buf_lab FROM PLN_PARAM WHERE cod_param='DIAS_BUFFER_LAB'; EXCEPTION WHEN NO_DATA_FOUND THEN NULL; END;
v_est_tini := v_est_part + v_buf_lab;   -- buffer laboratorio
```

---

#### BUG 13 — `SP_PLN_INIT_SEGUIMIENTO`: FCH_ENTREGA_COMP calculada incorrectamente

**Código actual (incorrecto):**
```sql
v_pedido.fecha + NVL(v_pedido.plazo_entrega, 30),  -- fecha pedido + plazo genérico
```
**Problema**: `PEDIDO.PLAZO_ENTREGA` es un plazo genérico del pedido. Cada ítem puede tener una fecha comprometida diferente en `ITEMPED.F_MAXPED` (fecha máxima por ítem). Al planificarse el ítem, la fecha más precisa está en `ITEMPED_DET.FHC_ENTREGA`. Usar el plazo del pedido genera una fecha comprometida incorrecta para ítems con fechas diferenciadas.

**Corrección**: Priorizar `ITEMPED.F_MAXPED`, fallback a pedido + plazo:
```sql
-- En SP_PLN_INIT_SEGUIMIENTO:
SELECT NVL(it.f_maxped, v_pedido.fecha + NVL(v_pedido.plazo_entrega, 30))
INTO v_fch_entrega
FROM itemped it
WHERE it.serie=p_serie AND it.num_ped=p_num_ped AND it.nro=p_nro;
```
> Al asignarse `ITEMPED_DET` (trigger `TUA_PLN_FROM_ITEMPED_DET`), actualizar `FCH_ENTREGA_COMP` con `ITEMPED_DET.FHC_ENTREGA` si es más preciso.

---

#### BUG 14 — `PLN_SEGUIMIENTO` FK: no cubre ITEMPED_DET (NUM_DET)

**Código actual (incorrecto):**
```sql
CONSTRAINT FK_PLN_SEG_ITEMPED FOREIGN KEY (SERIE, NUM_PED, NRO)
    REFERENCES ITEMPED (SERIE, NUM_PED, NRO)
```
**Problema**: La tabla PLN_SEGUIMIENTO tiene una fila por `(SERIE, NUM_PED, NRO, NUM_DET)` — es decir, por sub-lote de `ITEMPED_DET`. Pero la FK solo valida `ITEMPED` (sin NUM_DET). Esto permite insertar registros PLN con `NUM_DET` que no existen en `ITEMPED_DET`. La integridad del sub-lote queda sin validar a nivel de BD.

**Corrección**: Agregar FK hacia ITEMPED_DET para NUM_DET > 0:
```sql
-- Agregar una CHECK constraint o segundo FK condicional:
CONSTRAINT FK_PLN_SEG_ITEMPED_DET FOREIGN KEY (SERIE, NUM_PED, NRO, NUM_DET)
    REFERENCES ITEMPED_DET (SERIE, NUM_PED, NRO, NUM_DET)
    DEFERRABLE INITIALLY DEFERRED   -- para permitir inserción en mismo TX
```

---

### 11.2 GAPS DE TRAZABILIDAD — Ítems del Pedido sin Cobertura Completa

---

#### GAP 1 — GASEADO (PROCESO='24'): Paso productivo omitido en el flujo PLN

**Descripción**: Para ítems con `ITEMPED.PROCESO='24'` (PEINADO GASEADO), después de la aprobación de CC-Tintorería (PASO '09'), la partida pasa por un proceso de **gaseado** en una máquina específica (`GAS`, `GAS01`, etc.) antes del devanado. Este proceso transforma el hilo teñido de PEINADO a PEINADO GASEADO.

**Impacto en el flujo**: El gap entre PASO '09' (CC-Tinto aprobado) y PASO '10' (Devanado) puede ser de 1-2 días para estos ítems, pero el sistema PLN no lo detecta ni alerta. La máquina de gaseado tampoco aparece en `PLN_CARGA_DIARIA`.

**Solución propuesta**: Agregar PASO `'09B'` condicional en `PLN_ESTADO_CODIGO`:
```sql
INSERT INTO PLN_ESTADO_CODIGO VALUES
('09B','Gaseado','Proceso de gaseado post-tintorería (solo PROCESO=24)',
  9.5, 'H_PRODUCCION_D', 'N', '#e83e8c');
```
> Activar este paso SOLO cuando `PLN_SEGUIMIENTO.PROCESO = '24'`. El trigger correspondiente detecta producción en H_PRODUCCION_D con TP_MAQ='G' (gaseado) o COD_MAQ LIKE 'GAS%'.

---

#### GAP 2 — Despacho parcial: ITEMPED.SALDO > 0 después del primer despacho

**Descripción**: Un ítem pedido por 1.000 kg puede despacharse en múltiples entregas (500 kg el 15/05, 300 kg el 22/05, 200 kg el 01/06). Cada despacho genera un KARDEX_G TP='22'. El sistema PLN actual, al llegar al PASO '14', cierra el seguimiento (ESTADO='C') sin verificar si queda saldo pendiente.

**Impacto**: Los 500 kg restantes quedan invisibles para el sistema de seguimiento. El panel de pendientes de despacho no los muestra. Las alertas de retraso no se generan para el saldo no despachado.

**Corrección en SP_PLN_AVANZA_PASO** (ya incluida en BUG 3):
```sql
ESTADO = CASE
    WHEN p_nuevo_paso='14' AND (KG_DESPACHADOS + NVL(p_kg_cantidad,0)) >= CANTIDAD_ORIG
    THEN 'C'   -- solo cerrar si se despachó TODO
    ELSE ESTADO
END,
-- Después del despacho parcial, retroceder a PASO '13' (Listo para Despacho)
COD_PASO_ACT = CASE
    WHEN p_nuevo_paso='14' AND (KG_DESPACHADOS + NVL(p_kg_cantidad,0)) < CANTIDAD_ORIG
    THEN '13'  -- volver a "Listo para Despacho" por el saldo restante
    ELSE p_nuevo_paso
END,
```

---

#### GAP 3 — ITEMPED.SOLO_DESPACHO='S': Ítems sin producción crean seguimiento innecesario

**Descripción**: Los ítems con `ITEMPED.SOLO_DESPACHO='S'` son despachos directos desde stock (re-venta, maquila, devolución). No requieren ningún proceso productivo. El trigger `TIA_PLN_FROM_ITEMPED` los trataría igual que cualquier ítem, creando un registro PLN en PASO '01' que nunca avanzará a través del flujo productivo.

**Impacto**: Genera registros "atascados" en PASO '01', activando la alerta `SMP` (Sin Programa Asignado) después de 2 días, aunque el ítem sea correcto.

**Corrección en TIA_PLN_FROM_ITEMPED**:
```sql
CREATE OR REPLACE TRIGGER TIA_PLN_FROM_ITEMPED
AFTER INSERT ON ITEMPED
FOR EACH ROW
BEGIN
    IF NVL(:NEW.solo_despacho, 'N') = 'S' THEN
        -- Ítem de solo-despacho: init en PASO '13' (Listo para Despacho)
        SP_PLN_INIT_SEGUIMIENTO(:NEW.serie, :NEW.num_ped, :NEW.nro, 0, '13');
    ELSE
        SP_PLN_INIT_SEGUIMIENTO(:NEW.serie, :NEW.num_ped, :NEW.nro, 0, '01');
        SP_PLN_CALCULA_FECHAS(:NEW.serie, :NEW.num_ped, :NEW.nro, 0, 'PED');
    END IF;
EXCEPTION WHEN OTHERS THEN NULL;
END;
```
> Requiere agregar parámetro `p_paso_ini` a `SP_PLN_INIT_SEGUIMIENTO`.

---

#### GAP 4 — Reproceso de partidas: CTCALIDAD rechaza y la partida vuelve a TT

**Descripción**: Cuando `CTCALIDAD_D.EST_EVALUACION='02'` (necesita re-evaluación), la partida puede volver a tintorería para un baño adicional. Este es el ciclo de **reproceso**: Aprobado CC → Reclasificado/Rechazado → Vuelve a TT → Re-aprobado CC.

**Impacto**: El PLN avanza el paso de '06'→'07'→'08'→'09' durante el primer ciclo. Si la partida vuelve a TT, los triggers `TIA_PLN_FROM_TT_RPRODUC` y `TIA_PLN_FROM_TT_RSECADO` volverán a llamar `SP_PLN_AVANZA_PASO` con pasos `'07'` y `'08'`. Pero ya están en esos pasos, no hay lógica para retroceder o ciclar.

**Solución propuesta**:
- Agregar campo `NRO_CICLO NUMBER DEFAULT 1` en PLN_SEGUIMIENTO para contar los ciclos de reproceso.
- Agregar alerta `REPR` (ya definida en PLN_ALERTA) con detección automática: si `PLN_SEGUIMIENTO.COD_PASO_ACT` ya pasó el PASO '09' y vuelve a registrarse un `TT_RPRODUC` para esa PARTIDA, incrementar `NRO_CICLO` y limpiar `FCH_REAL_TIN_FIN`, `FCH_REAL_SECADO`, `FCH_REAL_CC_TINTO`.

---

#### GAP 5 — Tabla SEGUIMIENTO (existente) no está sincronizada con PLN_LOG_EVENTOS

**Descripción**: La tabla `SEGUIMIENTO` (sin prefijo PLN_) ya registra eventos clave:
- `AREA='PLANEAMIENTO'` → "PROGRAMADO XX" al crear ITEMPED_DET
- `AREA='CCALIDAD'` → estado de control de calidad
- `AREA='PROG HILAND'` → eventos de hilandería
- `AREA='REVISADO'` → resultado de revisado

`PLN_LOG_EVENTOS` registraría eventos similares pero no hay coordinación. El resultado es duplicación de eventos y posible confusión sobre cuál es la fuente de verdad.

**Solución propuesta**: Al insertar en `PLN_LOG_EVENTOS`, agregar campo `ID_SEGUIMIENTO_OLD` que apunte al registro correspondiente de `SEGUIMIENTO` (si existe). Alternativamente, convertir `PLN_LOG_EVENTOS` en una **vista materializada** que enriquece `SEGUIMIENTO` en vez de duplicarlo.

---

#### GAP 6 — PARTIDA_FENTREGA no está integrada al PLN

**Descripción**: La tabla `PARTIDA_FENTREGA` (3.648 registros) registra fechas de entrega específicas por partida, que pueden diferir de las fechas del ítem del pedido. No está mapeada en ningún trigger ni en PLN_SEGUIMIENTO.

**Campo a agregar en PLN_SEGUIMIENTO**:
```sql
FCH_ENTREGA_PARTIDA  DATE,  -- de PARTIDA_FENTREGA (fecha compromiso a nivel partida)
```
> Llenar desde el trigger `TUA_PLN_FROM_PARTIDA` al detectar INSERT en PARTIDA_FENTREGA.

---

#### GAP 7 — ITEMPED_DESTINO: destinos de entrega múltiples no están en el flujo PLN

**Descripción**: La tabla `ITEMPED_DESTINO` (1.061 registros) permite que un ítem de pedido tenga múltiples destinos de entrega (diferentes almacenes o clientes destino). Cuando esto aplica, el despacho se puede dividir por destino. El flujo PLN no contempla esta división.

**Impacto**: Si un ítem de 500 kg tiene 2 destinos (250 kg a Lima, 250 kg a Arequipa), los despachos separados para cada destino generarán 2 registros KARDEX TP='22'. El sistema PLN verá 2 avances al PASO '14' para el mismo ítem, potencialmente cerrándolo al primer 50%.

**Solución**: Verificar en `TIA_PLN_FROM_KARDEX_DESPACHO` si `ITEMPED_DESTINO` existe y si el despacho cubre TODOS los destinos antes de cerrar.

---

#### GAP 8 — PACKING_G/D/L: Etapa de embalaje no tiene PASO en el flujo

**Descripción**: Las tablas `PACKING_G` (12.865), `PACKING_D` (20.007) y `PACKING_L` (103.063) registran el embalaje de conos en cajas/fardos antes del despacho. Esta etapa ocurre entre PASO '12' (Almacén PT) y PASO '14' (Despacho). El embalaje asigna lotes a cajas con peso, código de barras y etiqueta.

**Impacto para despacho**: Un pedido no puede despacharse sin packing completado. Si el PLN no rastrea esta etapa, el panel de despacho mostrará ítems como "Listos" cuando en realidad el packing no está listo.

**Solución propuesta**: Agregar PASO `'13B'` o usar PASO `'13'` existente (Listo para Despacho) como punto que requiere validación de packing:
```sql
-- Condición para avanzar de '12' a '13': PACKING_G registrado para NUM_PED + NRO
INSERT INTO PLN_ESTADO_CODIGO VALUES
('13B','Packing Completado','PACKING_G/D registrado y lotes pesados',
  13.5, 'PACKING_G', 'N', '#ffc107');
```

---

#### GAP 9 — CT_RECPART (1.020.783 registros) no está en el flujo

**Descripción**: La tabla `CT_RECPART` con más de 1 millón de registros tiene prefijo `CT_` (Control de producción). Su nombre sugiere "Control de Receta de Partida". No está mencionada en ningún PASO del flujo ni en los triggers PLN.

**Investigación recomendada**: Antes de implementar los triggers, ejecutar:
```sql
SELECT COUNT(*), MIN(rowid), MAX(rowid) FROM ct_recpart;
SELECT column_name, data_type FROM user_tab_columns
WHERE table_name = 'CT_RECPART' ORDER BY column_id;
```
Esta tabla podría ser la tabla de trazabilidad detallada de recetas por partida que complementa `PARTIDA_MAS`.

---

### 11.3 ESCENARIOS DE NEGOCIO NO CUBIERTOS

---

#### ESCENARIO 1 — LOTE cross-pedido: un LOTE puede pertenecer a MÚLTIPLES pedidos

**Del análisis de datos reales (sección 10.3)**:
> LOTE `'1176-M'` producido para pedido 87442 → consumido en pedido 88586

El LOTE como campo de texto es una clave de negocio reutilizable, NO un identificador único de pedido. Usar `LOTE` como enlace entre hilandería y tintorería puede generar **ambigüedad cuando el mismo LOTE sirve a varios pedidos en paralelo**.

**Regla definitiva de trazabilidad**:
```
ITEMPED_DET.NROPROG = PARTIDA.NROPROG   ← ÚNICA relación 1:1 confiable
```
> Todos los triggers y vistas de PLN deben navegar por `NROPROG`, nunca por `LOTE` directamente. El LOTE es solo una referencia humana de producción.

---

#### ESCENARIO 2 — Múltiples PARTIDAS para el mismo ítem (NUM_DET > 0): estado ambiguo

**Descripción**: Un ítem con `NUM_DET=0` y `NUM_DET=1` (dos sub-lotes) tiene dos registros en PLN_SEGUIMIENTO. Si NUM_DET=0 pasa CC (PASO '09') pero NUM_DET=1 está en reproceso (PASO '07'), el ítem aparece como parcialmente en dos etapas diferentes.

**Para el dashboard**: Mostrar el **peor paso activo** de todos los NUM_DET como estado del ítem. Un ítem está "terminado" solo cuando **todos** sus NUM_DET están en PASO '14' y ESTADO='C'.

**Vista sugerida** en `V_PLN_ESTADO_PEDIDO`:
```sql
-- Estado del ítem = mínimo paso (el más atrasado)
MIN(ec.orden_paso) AS paso_peor,
SUM(CASE WHEN s.estado='C' THEN 1 ELSE 0 END) AS num_det_cerrados,
COUNT(s.num_det) AS num_det_total
```

---

#### ESCENARIO 3 — PARTIDA_MAS multi-baño: el PASO '07' debe capturar el ÚLTIMO baño, no el primero

**Descripción**: Una partida puede tener varios baños de tintorería:
- PROCESO=TE (Teñido) → TT_RPRODUC ESTADO='3' (Terminado)
- PROCESO=BQM (Blanqueo) → TT_RPRODUC ESTADO='3' (Terminado)

El trigger `TIA_PLN_FROM_TT_RPRODUC` avanza el ítem a PASO '07' al insertar el PRIMER baño. Sin embargo, la partida no está "tenida" hasta que el ÚLTIMO baño de `PARTIDA_MAS` esté terminado.

**Corrección del trigger**: Antes de avanzar al PASO '08' (Secado), verificar que todos los procesos de `PARTIDA_MAS` están en ESTADO='3':
```sql
-- En TIA_PLN_FROM_TT_RSECADO (al registrar secado, todos los baños ya terminaron):
-- Este trigger ya es el evento correcto para avanzar a PASO '08'.
-- Para PASO '07' (Tenido), solo avanzar si el baño insertado es el primer proceso TE:
IF :NEW.proceso IN ('TE', 'TE1') THEN
    SP_PLN_AVANZA_PASO(..., '07', ...);
END IF;
-- FCH_REAL_TIN_FIN se captura en TIA_PLN_FROM_TT_RSECADO (ya que secado = post-baños)
```

---

#### ESCENARIO 4 — OBJETOS INVÁLIDOS: POST_TO_API, REGISTRA_LOGIN, PKG_COMERCIAL, V_DRAW

**Descripción (sección 4.2 del documento)**:
> `POST_TO_API`, `REGISTRA_LOGIN`, `PKG_COMERCIAL BODY` y `V_DRAW` están en estado **INVALID**

**Evaluación de riesgo para el módulo PLN_**:

Verificado: **ninguno de los triggers, procedimientos ni vistas PLN_** hace referencia a estos objetos. No aparecen en Propuesta.md ni en Reunion.md.

Lo más probable es que sean **código legado nunca activo** — llevan inválidos desde siempre y ninguna funcionalidad actual del sistema SIG depende de ellos:
- `POST_TO_API` — integración externa que nunca se completó
- `REGISTRA_LOGIN` — referencia a tabla inexistente (ORA-00942), tabla fue renombrada o eliminada
- `PKG_COMERCIAL BODY` — error de sintaxis en línea 1 (parámetro en cabecera del BODY); la spec es VALID, solo el body está roto
- `V_DRAW` — columna ambigua (ORA-00918), vista de reporte auxiliar

**Impacto en PLN_**: Ninguno. El despliegue de PLN_ puede proceder sin corregir estos objetos.

**Acción recomendada (baja prioridad, fuera del alcance PLN_)**: Registrar en backlog de BD para eventual corrección. No ejecutar acción bloqueante antes del despliegue.

---

#### ESCENARIO 5 — TT_INCUMPLE_PROG: incumplimientos de TT no alimentan alertas PLN

**Descripción**: La tabla `TT_INCUMPLE_PROG` (27 registros) registra incumplimientos del programa de tintorería. Estos incumplimientos son directamente predictores de retraso en el despacho al cliente. No están conectados al motor de alertas `SP_PLN_GENERA_ALERTAS`.

**Alerta sugerida**: Agregar tipo `INCP` (Incumplimiento Programa TT) en el catálogo de alertas:
```sql
INSERT INTO PLN_ESTADO_CODIGO — o en PLN_ALERTA tipo catálogo:
('INCP', 'A', 'Incumplimiento programa tintorería',
 'Registro en TT_INCUMPLE_PROG para pedido activo');
```

---

#### ESCENARIO 6 — Mantenimiento programado de máquinas no afecta PLN_CARGA_DIARIA

**Descripción**: `MA_CRONOGRAMA_ACTIVO` (279 registros) tiene el cronograma de mantenimiento preventivo. Cuando una máquina entra a mantenimiento (MA_PROGRAMA), su capacidad baja a 0 horas. `PLN_CARGA_DIARIA` no descuenta las horas de mantenimiento de `HORAS_CAPACIDAD`.

**Corrección en `SP_PLN_CARGA_DIARIA_REFRESH`**: Al calcular `HORAS_CAPACIDAD`, restar horas de mantenimiento programado:
```sql
-- HORAS_CAPACIDAD = 24 - horas mantenimiento programado ese día para esa máquina
SELECT NVL(SUM(FECHA_FIN - FECHA_INI) * 24, 0)
INTO v_horas_mant
FROM ma_programa mp
WHERE mp.cod_maq = v_cod_maq
  AND TRUNC(mp.fecha_ini) <= v_fecha
  AND TRUNC(NVL(mp.fecha_fin, mp.fecha_ini)) >= v_fecha
  AND mp.estado NOT IN ('X','A');  -- no anulados
```

---

#### ESCENARIO 7 — Anticipos de cliente: pedidos con anticipo tienen mayor prioridad real

**Descripción**: La tabla `ANTICIPO` (2.906 registros) registra anticipos de clientes contra pedidos. Un pedido con anticipo ya cobrado tiene un compromiso comercial más fuerte que uno sin anticipo. El sistema PLN usa `PEDIDO.PRIORIDAD` como campo genérico pero no refleja si hay un anticipo cobrado.

**Sugerencia**: Al inicializar PLN_SEGUIMIENTO, verificar `ANTICIPO` y aumentar automáticamente `IND_URGENTE='S'` si el pedido tiene anticipo cobrado con saldo > 0.

---

#### ESCENARIO 8 — Exportación: campos CONTENEDOR/NRO_PRECINTO no están en PLN

**Descripción (NOTAS CRÍTICAS punto 10)**: Para clientes exportadores, el despacho requiere datos de contenedor, precinto, puerto de destino. Estos campos están en `KARDEX_G` pero no se reflejan en `PLN_SEGUIMIENTO`.

**Para el dashboard de despacho**: Cuando `CLIENTES.TIPO_CLIENTE` indica exportador (o cuando existe datos de exportación en el pedido), el PASO '14' debería requerir validación adicional de datos de contenedor.

---

### 11.4 RESUMEN CONSOLIDADO — PRIORIDADES DE CORRECCIÓN

| # | Categoría | Severidad | Descripción | Objeto afectado |
|---|-----------|-----------|-------------|-----------------|
| 1 | BUG | 🔴 CRÍTICO | `:NEW.num_det` inexistente en PARTIDA | `TUA_PLN_FROM_PARTIDA` |
| 2 | BUG | 🔴 CRÍTICO | SERIE hardcodeada = 1 en triggers TT y KARDEX | `TIA_PLN_FROM_TT_RPRODUC`, `TIA_PLN_FROM_TT_RSECADO`, `TIA_PLN_FROM_KARDEX_DESPACHO` |
| 3 | BUG | 🔴 CRÍTICO | `SER_PARTIDA` ≠ `NUM_DET` en trigger CC | `TIA_PLN_FROM_CTCALIDAD` |
| 4 | BUG | 🔴 CRÍTICO | ESTADO='C' se cierra en PASO '12', no en '14' | `SP_PLN_AVANZA_PASO` |
| 5 | BUG | 🟠 ALTO | `FCH_REAL_PARTIDA` asignada en PASO '05' en vez de '04' | `SP_PLN_AVANZA_PASO` |
| 6 | BUG | 🟠 ALTO | `FCH_REAL_TIN_FIN` = `FCH_REAL_SECADO` (mismo paso '08') | `SP_PLN_AVANZA_PASO` |
| 7 | BUG | 🟠 ALTO | `KG_DESPACHADOS` actualizado en PASO '12' en vez de '14' | `SP_PLN_AVANZA_PASO` |
| 8 | BUG | 🟠 ALTO | `KG_PRODUCIDOS` acumulado también en PASO '05' (Lab) | `SP_PLN_AVANZA_PASO` |
| 9 | BUG | 🟠 ALTO | V_PLN_PENDIENTES_DESP filtra pasos '10','11' (Devanado/Revisado) | `V_PLN_PENDIENTES_DESP` |
| 10 | BUG | 🟠 ALTO | V_PLN_KPI_CUMPLIMIENTO mide PASO '12' como despacho | `V_PLN_KPI_CUMPLIMIENTO` |
| 11 | BUG | 🟡 MEDIO | `MAX(kgr_hr)` optimista; ignorar máquina asignada | `SP_PLN_CALCULA_FECHAS` |
| 12 | BUG | 🟡 MEDIO | Sin buffer de laboratorio en estimación de fechas | `SP_PLN_CALCULA_FECHAS` |
| 13 | BUG | 🟡 MEDIO | `FCH_ENTREGA_COMP` calcula con plazo genérico del pedido | `SP_PLN_INIT_SEGUIMIENTO` |
| 14 | BUG | 🟡 MEDIO | FK solo cubre ITEMPED, no ITEMPED_DET (NUM_DET) | `PLN_SEGUIMIENTO` |
| 15 | GAP | 🔴 CRÍTICO | Proceso GASEADO (PROCESO='24') sin PASO en flujo PLN | `PLN_ESTADO_CODIGO` |
| 16 | GAP | 🔴 CRÍTICO | Despacho parcial cierra el ítem antes de tiempo | `SP_PLN_AVANZA_PASO` |
| 17 | GAP | 🟠 ALTO | `SOLO_DESPACHO='S'` genera registros atascados en PASO '01' | `TIA_PLN_FROM_ITEMPED` |
| 18 | GAP | 🟠 ALTO | Reproceso de partidas no tiene lógica de retorno de pasos | `SP_PLN_AVANZA_PASO` |
| 19 | GAP | 🟠 ALTO | PACKING_G/D (12K registros) no tiene PASO en flujo PLN | `PLN_ESTADO_CODIGO` |
| 20 | GAP | 🟡 MEDIO | CT_RECPART (1M registros) no mapeada en flujo | Investigar antes de desplegar |
| 21 | GAP | 🟡 MEDIO | `SEGUIMIENTO` y `PLN_LOG_EVENTOS` sin coordinación | Diseñar integración |
| 22 | GAP | 🟡 MEDIO | `PARTIDA_FENTREGA` no vinculada a PLN_SEGUIMIENTO | `PLN_SEGUIMIENTO` |
| 23 | GAP | 🟡 MEDIO | `ITEMPED_DESTINO` (destinos múltiples) ignora despacho parcial por destino | `TIA_PLN_FROM_KARDEX_DESPACHO` |
| 24 | ESCENARIO | 🔴 CRÍTICO | LOTE cross-pedido: LOTE ≠ enlace único; usar NROPROG | Todos los triggers |
| 25 | ESCENARIO | 🔴 CRÍTICO | Multi-baño TT: PASO '07' avanza en 1er baño, no en último | `TIA_PLN_FROM_TT_RPRODUC` |
| 26 | ESCENARIO | 🟠 ALTO | Objetos INVÁLIDOS `POST_TO_API` y `REGISTRA_LOGIN` | Previo al despliegue |
| 27 | ESCENARIO | 🟠 ALTO | `TT_INCUMPLE_PROG` no alimenta alertas PLN | `SP_PLN_GENERA_ALERTAS` |
| 28 | ESCENARIO | 🟠 ALTO | Mantenimiento preventivo (MA_CRONOGRAMA_ACTIVO) no reduce PLN_CARGA_DIARIA | `SP_PLN_CARGA_DIARIA_REFRESH` |
| 29 | ESCENARIO | 🟡 MEDIO | Estado del ítem con múltiples NUM_DET = debe mostrar el peor paso | `V_PLN_ESTADO_PEDIDO` |
| 30 | ESCENARIO | 🟡 MEDIO | Anticipos cobrados deberían marcar `IND_URGENTE='S'` automáticamente | `SP_PLN_INIT_SEGUIMIENTO` |

---

### 11.5 REGLA DE ORO — TRAZABILIDAD POR ÍTEM

> Extraída del análisis completo del flujo real. Debe guiar TODA la implementación del módulo PLN_.

```
PEDIDO (cabecera)
    └── ITEMPED (serie+num_ped+nro)         ← 1 fila por artículo pedido
          └── ITEMPED_DET (nroprog)         ← 1 fila por sub-lote; NROPROG es la clave maestra
                └── PARTIDA (nroprog)       ← 1:1 con ITEMPED_DET.NROPROG
                      ├── PARTIDA_MAS       ← 1:N (múltiples baños TT)
                      │     └── ING_RECETAS_G/D
                      │           └── TT_RPRODUC (producción por baño)
                      ├── TT_RSECADO        ← 1:1 (secado post-baños)
                      ├── CTCALIDAD_D       ← control de calidad TT
                      ├── H_PROGRAMACION    ← devanado post-TT
                      ├── REVISADO_G/D      ← revisado conos
                      ├── LOTES             ← inventario PT
                      └── KARDEX_G/D/L (TP='22') ← despacho físico
```

**La navegación siempre va de ITEMPED_DET.NROPROG → PARTIDA.NROPROG.**
**Nunca navegar por LOTE, LOTE_PRODUC o NRO_PEDIDO directamente desde triggers.**

---

*Sección 11 agregada: 18/05/2026 · Análisis de bugs, gaps y consideraciones del módulo PLN_ · Revisión completa del ciclo PEDIDO → DESPACHO*

---

## 12. ANÁLISIS ADICIONAL — REVISIÓN DE BASE DE DATOS (18/05/2026)

> Hallazgos obtenidos ejecutando queries diagnósticos directamente sobre Oracle 11.2.0.4 · Esquema SIG. Complementan y corrigen la Sección 11.

---

### 12.1 NUEVOS BUGS CONFIRMADOS POR BD

---

#### BUG-A · PKG_COMERCIAL PACKAGE BODY — Error de sintaxis en línea 1

**Fuente:** `USER_ERRORS` + `USER_SOURCE`

**Síntoma:**
```
PKG_COMERCIAL | PACKAGE BODY | line 1 | PLS-00103: Encountered the symbol "("
PKG_COMERCIAL | PACKAGE BODY | line 51 | PLS-00103: Encountered the symbol "END"
```

**Root cause — línea 1 del body:**
```sql
-- INCORRECTO (actual):
PACKAGE BODY Pkg_Comercial (P_RS OUT SYS_REFCURSOR) AS

-- CORRECTO:
PACKAGE BODY Pkg_Comercial AS
   PROCEDURE PA_ULT_VTA_CLIENTE_UBG_GIRO (P_RS OUT SYS_REFCURSOR) IS
```

El parámetro `P_RS OUT SYS_REFCURSOR` fue colocado en la declaración del PACKAGE BODY en vez de en el encabezado del PROCEDURE. Oracle no acepta parámetros en el cuerpo del paquete.

**Impacto:** El PACKAGE BODY no compila. `PA_ULT_VTA_CLIENTE_UBG_GIRO` (última venta por cliente/UBG/giro) es completamente no-funcional. La spec es VALID pero el body está roto.

**Objetos INVALID adicionales detectados (código legado, no relacionado con PLN_):**

| Objeto | Tipo | Error | Impacto en PLN_ |
|--------|------|-------|-----------------|
| `PKG_COMERCIAL` | PACKAGE BODY | PLS-00103 síntesis, línea 1 | Ninguno |
| `POST_TO_API` | PROCEDURE | PLS-00302 OTHERS no declarado | Ninguno |
| `REGISTRA_LOGIN` | PROCEDURE | ORA-00942 tabla inexistente | Ninguno |
| `V_DRAW` | VIEW | ORA-00918 columna ambigua | Ninguno |

> **Nota (18/05/2026):** Verificado que PLN_ no referencia ninguno de estos objetos en triggers, procedimientos ni vistas. Lo más probable es que sean código legado preexistente nunca activo. No bloquean el despliegue del módulo PLN_. Registrar en backlog de BD para corrección futura independiente.

---

#### BUG-B · CORRECCIÓN A SECCIÓN 11 BUG 8 — Mapeo de SER_PARTIDA/NROPART en CTCALIDAD_D

**Fuente:** Cuerpo del trigger `TIA_CTCALIDADD_RESULTADO` (verificado desde BD)

**Mapeo correcto confirmado por el trigger existente:**
```sql
-- TIA_CTCALIDADD_RESULTADO (trigger existente, ya en BD):
UPDATE ITEMPED_DET
   SET SITU_COD = '6'
 WHERE NUM_PED = :NEW.NRO_PEDIDO
   AND NRO     = :NEW.SER_PARTIDA   -- SER_PARTIDA → ITEMPED_DET.NRO (número de ítem!)
   AND NUM_DET = :NEW.NROPART       -- NROPART → ITEMPED_DET.NUM_DET (sub-lote!)
   AND REPROCESO = :NEW.REPROCESO;
```

**Tabla de mapeo:**

| Campo CTCALIDAD_D | Campo ITEMPED_DET | Descripción |
|-------------------|-------------------|-------------|
| `NRO_PEDIDO` | `NUM_PED` | Número de pedido |
| `SER_PARTIDA` | `NRO` | Número de ítem del pedido |
| `NROPART` | `NUM_DET` | Sub-lote (NUM_DET) |

La Sección 11 BUG 8 describía la corrección incompleta. El trigger propuesto `TIA_PLN_FROM_CTCALIDAD` debe usar `:NEW.ser_partida` como `p_nro` y `:NEW.nropart` como `p_num_det`:

```sql
-- Correcto en TIA_PLN_FROM_CTCALIDAD (derivar solo SERIE):
SELECT d.serie INTO v_serie
FROM itemped_det d
WHERE d.num_ped = :NEW.nro_pedido
  AND d.nro     = :NEW.ser_partida   -- SER_PARTIDA = NRO (ítem)
  AND d.num_det = :NEW.nropart       -- NROPART = NUM_DET (sub-lote)
  AND ROWNUM = 1;
```

---

#### BUG-C · TIA_PLN_FROM_TT_RPRODUC debe ser AFTER UPDATE, no AFTER INSERT

**Fuente:** Cuerpo de `TIB_TT_RPRODUC` (BEFORE INSERT, verificado desde BD)

**El trigger existente `TIB_TT_RPRODUC` solo establece auditoría:**
```sql
:NEW.A_ADFECHA := SYSDATE;
:NEW.A_ADUSER  := USER;
```

**ESTADO no es '3' en el INSERT.** TT_RPRODUC se inserta con ESTADO inicial (probablemente '1' = En Proceso). El estado '3' (Terminado) se asigna mediante un UPDATE posterior por la aplicación.

Distribución real de TT_RPRODUC.ESTADO:
- `'3'`: 24,819 registros (Terminado)
- `'9'`: 180 registros (Anulado)

**Impacto:** `TIA_PLN_FROM_TT_RPRODUC` (AFTER INSERT) avanzaría PASO '07' cuando el baño **comienza**, no cuando **termina**. PLN mostraría "Teñido Completo" en el momento de iniciar la producción.

**Corrección:**
```sql
-- INCORRECTO (Propuesta.md):
CREATE OR REPLACE TRIGGER TIA_PLN_FROM_TT_RPRODUC
AFTER INSERT ON TT_RPRODUC FOR EACH ROW ...

-- CORRECTO:
CREATE OR REPLACE TRIGGER TUA_PLN_FROM_TT_RPRODUC
AFTER UPDATE OF ESTADO ON TT_RPRODUC FOR EACH ROW
WHEN (NEW.ESTADO = '3')
DECLARE
  v_pendientes NUMBER;
BEGIN
  -- Verificar que TODOS los baños de esta PARTIDA estén terminados
  SELECT COUNT(*) INTO v_pendientes
  FROM partida_mas pm
  JOIN ing_recetas_g rg ON rg.numero = pm.numero
  JOIN tt_rproduc r ON r.receta = rg.numero
  WHERE pm.guia = :NEW.guia
    AND r.estado <> '3';

  IF v_pendientes = 0 THEN
    -- Todos los baños terminaron → avanzar a PASO '07'
    SP_PLN_AVANZA_PASO(v_serie, v_num_ped, v_nro, v_num_det, '07', ...);
  END IF;
END;
```

> **Nota crítica:** El 75% de partidas tienen 2 o más baños TT (ver §12.3 Hallazgo-H). Avanzar en el primer baño afecta a la mayoría de producción.

---

#### BUG-D · TIA_PLN_FROM_CTCALIDAD debe ser AFTER UPDATE, no AFTER INSERT para avanzar PASO

**Fuente:** `TUA_CTCALIDADD_RESULTADO` (verificado desde BD) + distribución de EST_EVALUACION

**RESULTADO y EST_EVALUACION='32' se fijan via UPDATE, no en el INSERT inicial.**

Distribución de CTCALIDAD_D.EST_EVALUACION:

| EST_EVALUACION | RESULTADO | Registros | Significado |
|---------------|-----------|-----------|-------------|
| `'13'` | — | 44 | En evaluación |
| `'02'` | — | 16 | En consulta (reproceso) |
| `'21'` | — | 2,111 | En reproceso |
| `'32'` | `'01'` | 124,438 | **Aprobado** |
| `'32'` | `'29'` | 1,770 | **Concesionado** |
| `'32'` | `'30'` | 3,672 | **Rechazado** (2.7% → reproceso) |
| `'32'` | `'21'` | 829 | Otra aprobación |

El trigger existente `TUA_CTCALIDADD_RESULTADO` ya actúa en UPDATE. El trigger PLN propuesto `TIA_PLN_FROM_CTCALIDAD` también debe ser en UPDATE para detectar el resultado final:

```sql
-- CORRECTO:
CREATE OR REPLACE TRIGGER TUA_PLN_FROM_CTCALIDAD
AFTER UPDATE OF EST_EVALUACION, RESULTADO ON CTCALIDAD_D FOR EACH ROW
WHEN (NEW.EST_EVALUACION = '32')
BEGIN
  IF :NEW.RESULTADO IN ('01','29','21') THEN
    SP_PLN_AVANZA_PASO(..., '09', ...);  -- CC Aprobado
  ELSIF :NEW.RESULTADO = '30' THEN
    SP_PLN_AVANZA_PASO(..., '9R', ...);  -- Reproceso
  END IF;
END;
```

---

#### BUG-E · SIMPLIFICACIÓN A SECCIÓN 11 BUG 6 — PARTIDA.SERIE y NRO_PEDIDO ya existen en :NEW

**Fuente:** `USER_TAB_COLUMNS` — PARTIDA tiene columnas `SERIE NUMBER` y `NRO_PEDIDO` directamente.

La Sección 11 BUG 6 describía una subconsulta compleja para recuperar SERIE. En realidad:

```sql
-- En TUA_PLN_FROM_PARTIDA, SERIE y NRO_PEDIDO ya están en :NEW:
-- Solo NRO y NUM_DET requieren subconsulta desde ITEMPED_DET:
SELECT d.nro, d.num_det
INTO v_nro, v_num_det
FROM itemped_det d
WHERE d.nroprog = :NEW.nroprog AND ROWNUM = 1;

SP_PLN_AVANZA_PASO(:NEW.serie, :NEW.nro_pedido, v_nro, v_num_det, v_paso, ...);
```

---

#### BUG-F · PASO '04' requiere trigger AFTER INSERT ON PARTIDA (ausente en Propuesta.md)

**Fuente:** `USER_TRIGGERS` — Propuesta.md solo define `TUA_PLN_FROM_PARTIDA` (UPDATE).

PASO '04' (Lote Disponible) ocurre cuando la PARTIDA es **creada** (INSERT) con hilo de hilandería. La Propuesta.md no tiene ningún trigger INSERT en PARTIDA para PLN.

Verificado: `H_RPRODUC.GUIA = PARTIDA.NUMERO` en el **99.99%** de los casos (25,005 de 25,007 registros con GUIA).

**Corrección: agregar trigger TIA_PLN_FROM_PARTIDA:**
```sql
CREATE OR REPLACE TRIGGER TIA_PLN_FROM_PARTIDA
AFTER INSERT ON PARTIDA FOR EACH ROW
DECLARE
  v_nro     ITEMPED_DET.NRO%TYPE;
  v_num_det ITEMPED_DET.NUM_DET%TYPE;
BEGIN
  -- PASO '04': Lote de hilo disponible desde hilandería
  SELECT d.nro, d.num_det INTO v_nro, v_num_det
  FROM itemped_det d
  WHERE d.nroprog = :NEW.nroprog AND ROWNUM = 1;

  SP_PLN_AVANZA_PASO(:NEW.serie, :NEW.nro_pedido, v_nro, v_num_det, '04', ...);
EXCEPTION WHEN NO_DATA_FOUND THEN NULL;
END;
```

El trigger `TUA_PLN_FROM_PARTIDA` existente (UPDATE) cubre PASO '06' cuando `SITU_PART` cambia a `'R001'` (recibida en tintorería).

---

### 12.2 CORRECCIONES A TRIGGERS CON EVENTO INCORRECTO

#### Resumen: triggers AFTER INSERT vs. AFTER UPDATE

| Trigger propuesto en Propuesta.md | Evento actual (incorrecto) | Evento correcto | Razón |
|-----------------------------------|---------------------------|-----------------|-------|
| `TIA_PLN_FROM_TT_RPRODUC` | AFTER INSERT | AFTER UPDATE OF ESTADO WHEN (NEW.ESTADO='3') | ESTADO='3' se fija via UPDATE |
| `TIA_PLN_FROM_CTCALIDAD` | AFTER INSERT | AFTER UPDATE OF EST_EVALUACION, RESULTADO WHEN (NEW.EST_EVALUACION='32') | RESULTADO se fija via UPDATE |
| `TIA_PLN_FROM_PARTIDA` | *(ausente)* | AFTER INSERT | PASO '04' requiere INSERT |

Triggers que SÍ son correctos en AFTER INSERT:
- `TIA_PLN_FROM_LOTES` (TP_TRANSAC='16') → ingreso PT es un INSERT
- `TIA_PLN_FROM_KARDEX_DESPACHO` → el evento de despacho podría ser INSERT o UPDATE en LOTES

---

### 12.3 HALLAZGOS ESTRUCTURALES

---

#### Hallazgo-E · ITEMPED_DET ya tiene campos de fecha estimada

`ITEMPED_DET` contiene los siguientes campos (verificado desde BD):

| Campo | Tipo | Descripción |
|-------|------|-------------|
| `FCH_ESTIMA_TENIDO` | DATE | Fecha estimada de teñido |
| `FCH_ESTIMA_CONO_UNO` | DATE | Fecha estimada primer cono |
| `FCH_ENT_TIN` | DATE | Fecha estimada entrada tintorería |
| `FCH_PROG` / `FHC_PROG` | DATE | Fechas de programación |
| `NIVEL_URGENCIA` | NUMBER | Nivel de urgencia |
| `URGENTE` | VARCHAR2(1) | Indicador urgente |

**Implicación:** `SP_PLN_CALCULA_FECHAS` debe sincronizar con `FCH_ESTIMA_TENIDO` y `FCH_ESTIMA_CONO_UNO` de ITEMPED_DET, no calcular fechas en paralelo. Dos sistemas calculando fechas estimadas divergirán con el tiempo.

---

#### Hallazgo-F · TT_PROGPART — tabla de programación TT no documentada

**Fuente:** Cuerpo del trigger `TIA_ITEMPED_DET` (verificado desde BD)

```sql
-- TIA_ITEMPED_DET popula TT_PROGPART automáticamente:
SELECT COUNT(*) INTO XCANT FROM TT_PROGPART
WHERE NUM_PED = :NEW.NUM_PED AND NRO = :NEW.NRO AND NUM_DET = :NEW.NUM_DET;
IF XCANT = 0 THEN
   INSERT INTO TT_PROGPART (NUM_PED, NRO, NUM_DET, FENTREGA)
   VALUES (:NEW.NUM_PED, :NEW.NRO, :NEW.NUM_DET, :NEW.FHC_ENTREGA);
END IF;
```

**Estructura de TT_PROGPART:**

| Campo | Tipo | Descripción |
|-------|------|-------------|
| `NUM_PED` | NUMBER (N) | Pedido |
| `NRO` | NUMBER (N) | Ítem |
| `NUM_DET` | NUMBER (N) | Sub-lote |
| `FENTREGA` | DATE | Fecha de entrega |
| `ESTADO` | VARCHAR2(1) | Estado TT del sub-lote |

Sin campo `SERIE`. PK implícita: (NUM_PED, NRO, NUM_DET).

**Nota:** TT_PROGPART es otra fuente de verdad de fechas de entrega, auto-populada antes de cualquier acción PLN. PLN no debe duplicar este tracking; puede leerlo o actualizarlo.

---

#### Hallazgo-G · Trigger de PASO '12' — LOTES TP_TRANSAC='16' en ALM PT

**Fuente:** Distribución de LOTES por TP_TRANSAC y COD_ALM (verificado desde BD)

| COD_ALM | TP_TRANSAC | Registros |
|---------|------------|-----------|
| `'03'` | `'16'` | 1,434,979 |
| `'07'` | `'16'` | 27,828 |

El trigger `TIA_PLN_FROM_LOTES` debe filtrar:
```sql
WHEN (NEW.COD_ALM IN ('03','07') AND NEW.TP_TRANSAC = '16' AND NEW.PARTIDA IS NOT NULL)
```

Navegación LOTES → ITEMPED_DET:
```
LOTES.PARTIDA → PARTIDA.NUMERO
              → PARTIDA.NROPROG → ITEMPED_DET.NROPROG (+ SERIE, NRO_PEDIDO desde PARTIDA)
```

---

#### Hallazgo-H · Multi-baño es la norma: 75% de partidas tienen 2+ baños TT

**Fuente:** Distribución de TT_RPRODUC por PARTIDA_MAS.GUIA (verificado desde BD)

| Baños por partida | Partidas |
|-------------------|----------|
| 1 baño | ~40,844 (25%) |
| 2 baños | ~31,603 (20%) |
| 3 baños | ~27,071 (17%) |
| 4 baños | ~21,125 (13%) |
| 5+ baños | ~35,000+ (25%) |

**El BUG-C (avanzar PASO '07' al primer INSERT en TT_RPRODUC) afecta al 75% de la producción real.** El trigger correcto debe verificar que TODOS los baños de la PARTIDA tengan ESTADO='3' antes de avanzar.

---

#### Hallazgo-I · L_VALIDA_RECETA.NROPROG — link directo a ITEMPED_DET

**Fuente:** `USER_TAB_COLUMNS` para L_VALIDA_RECETA

`L_VALIDA_RECETA` tiene campo `NROPROG NUMBER` que enlaza directamente a `ITEMPED_DET.NROPROG`.

El trigger `TUA_PLN_FROM_L_VALIDA_RECETA` (AFTER UPDATE WHEN ESTADO='3') puede navegar:
```sql
SELECT d.serie, d.num_ped, d.nro, d.num_det
FROM itemped_det d
WHERE d.nroprog = :NEW.nroprog AND ROWNUM = 1;
```

Distribución de L_VALIDA_RECETA.ESTADO:
- `'3'`: 70,872 registros (validado/aprobado) — estado de referencia para PLN
- `'4'`: 537 registros
- `'9'`: 437 registros (anulado)

---

#### Hallazgo-J · TT_RSECADO.GUIA → PARTIDA.NUMERO — link directo para PASO '08'

**Fuente:** `USER_TAB_COLUMNS` para TT_RSECADO

`TT_RSECADO` tiene campo `GUIA NUMBER`. Análogo a H_RPRODUC.GUIA, este campo enlaza a `PARTIDA.NUMERO`. El trigger para PASO '08' (Secado) puede navegar directamente via GUIA → PARTIDA → ITEMPED_DET.

Campos adicionales de interés en TT_RSECADO:
- `COD_ALM_KD, TP_TRANSAC_KD, SERIE_KD, NUMERO_KD` — referencia al Kardex de secado
- `IND_FLOCA` — indicador floca (variante de producto)

---

#### Hallazgo-K · TIA_PARTIDA ya crea PARTIDA_FENTREGA automáticamente

**Fuente:** Cuerpo del trigger `TIA_PARTIDA` (verificado desde BD)

```sql
-- TIA_PARTIDA (existente):
INSERT INTO PARTIDA_FENTREGA(GUIA, FCH_ENTREGA, NRO_RMC, PESO_NETO, FCH_ENTREGA_ORI, ESTADO)
VALUES (:NEW.NUMERO, :NEW.FCH_ENTREGA, :NEW.NRO_RMC, :NEW.PESO_NETO, :NEW.FCH_ENTREGA, '0');
```

**Corrección al GAP 6 de la Sección 11:** PARTIDA_FENTREGA SÍ es auto-poblada en cada INSERT de PARTIDA. El gap real no es la creación, sino la actualización: cuando el usuario modifica `PARTIDA_FENTREGA.FCH_ENTREGA` (reprogramación), `PLN_SEGUIMIENTO.FCH_ENTREGA_PARTIDA` debe reflejar ese cambio via trigger en PARTIDA_FENTREGA.

---

#### Hallazgo-L · PARTIDA.SITU_PART — estados del ciclo TT confirmados

**Fuente:** Distribución de PARTIDA.SITU_PART (verificado desde BD)

| SITU_PART | Registros | Fase del ciclo |
|-----------|-----------|----------------|
| `''` (blanco) | 157,079 | En hilandería (PASOS '03'–'04') |
| `'R001'` | 4,644 | Recibida en TT (PASO '06') |
| `'P'` | 3,262 | En proceso TT (PASO '07') |
| `'A'` | 972 | Acabada TT (PASO '08'/'09') |
| `'X'` | 4,276 | Cerrada/despacho |
| `'9'` (ESTADO) | 4,365 | Anulada |

El trigger `TUA_PLN_FROM_PARTIDA` debe disparar al cambio de `SITU_PART`:
- `'' → 'R001'` → PASO '06' (En Tintorería)

---

#### Hallazgo-M · Despacho mejor trazado via LOTES.S_TRANSAC que KARDEX_G INSERT

**Fuente:** V_STATUS_PEDIDO (sección EN DESPACHO, verificado)

```sql
-- V_STATUS_PEDIDO usa:
FROM LOTES L, KARDEX_L X
WHERE L.S_TRANSAC IN ('21','23')   -- '21'=despacho nacional, '23'=despacho exportación
  AND X.TP_TRANSAC = L.S_TRANSAC
  AND X.NRO_SER_LOTE = L.LOTE
```

El trigger propuesto `TIA_PLN_FROM_KARDEX_DESPACHO` (AFTER INSERT ON KARDEX_G) tiene dificultad para enlazar a ITEMPED_DET porque `KARDEX_G.TIP_DOC_REF` está en blanco en ~90% de los registros TP='22'.

**Alternativa recomendada:** Trigger `TUA_PLN_FROM_LOTES_DESPACHO` AFTER UPDATE ON LOTES WHEN (NEW.S_TRANSAC IN ('21','23')):
```
LOTES.PARTIDA → PARTIDA.NUMERO → PARTIDA.NROPROG → ITEMPED_DET.NROPROG
```

---

#### Hallazgo-N · KARDEX_G ya tiene NRO_DESPACHO e IND_EXP directamente

**Fuente:** `USER_TAB_COLUMNS` para KARDEX_G

`KARDEX_G` tiene campos nativos:
- `NRO_DESPACHO NUMBER` — enlace directo a DESPACHO_GUIA
- `IND_EXP VARCHAR2(1)` — indicador de exportación
- `CONTENEDOR, NRO_PRECINTO, C_PUERTO, T_PUERTO` — datos de exportación

Si se usa trigger en KARDEX_G, `:NEW.nro_despacho` e `:NEW.ind_exp` están disponibles sin subconsulta adicional.

---

#### Hallazgo-O · PROCESO='24' (Gaseado) — volumen significativo

**Fuente:** Distribución de ITEMPED_DET por PROCESO (verificado desde BD)

| PROCESO | Ítems | KG estimados |
|---------|-------|-------------|
| `'20'` PEINADO | 66,916 | 10.8M |
| `'01'` | 59,029 | 12.1M |
| `'24'` GASEADO | **4,983** | **~540K** |

**GAP 1 de Sección 11 (paso Gaseado ausente)** representa 4,983 ítems y ~540 toneladas. Es un volumen significativo que justifica implementar el PASO correspondiente.

---

#### Hallazgo-P · PACKING_G.NUM_PED es VARCHAR2(20), no NUMBER

**Fuente:** `USER_TAB_COLUMNS` para PACKING_G

El campo `PACKING_G.NUM_PED` es `VARCHAR2(20)` (no NUMBER). El join a ITEMPED_DET.NUM_PED requiere conversión:
```sql
WHERE d.num_ped = TO_NUMBER(pg.num_ped)
```
O bien asegurar que la comparación sea por tipo compatible. También no hay campos NRO/NUM_DET directos en PACKING_G, complicando el join a nivel de sub-lote.

---

### 12.4 RESUMEN COMPARATIVO — CORRECCIONES A SECCIÓN 11

| Referencia S11 | Descripción original | Corrección/Complemento S12 |
|----------------|---------------------|---------------------------|
| BUG 6 (TUA_PLN_FROM_PARTIDA) | Subconsulta compleja para SERIE | SERIE y NRO_PEDIDO ya están en :NEW; solo NRO y NUM_DET requieren subconsulta (BUG-E) |
| BUG 8 (CTCALIDAD_D) | Mapeo de SER_PARTIDA incompleto | SER_PARTIDA=NRO, NROPART=NUM_DET — confirmado por trigger existente (BUG-B) |
| GAP 5 (multi-baño) | Mencionado como excepción | 75% de partidas tienen 2+ baños (Hallazgo-H); es el caso NORMAL |
| GAP 6 (PARTIDA_FENTREGA) | "No mapeada en ningún trigger" | TIA_PARTIDA ya la crea automáticamente; el gap es la sincronización en UPDATE (Hallazgo-K) |
| Trigger CTCALIDAD_D | AFTER INSERT | RESULTADO se fija via UPDATE; trigger debe ser AFTER UPDATE (BUG-D) |
| Trigger TT_RPRODUC | AFTER INSERT | ESTADO='3' via UPDATE; trigger debe ser AFTER UPDATE (BUG-C) |
| *(ausente en S11)* | — | PASO '04' requiere TIA_PLN_FROM_PARTIDA (AFTER INSERT) ausente en Propuesta.md (BUG-F) |
| *(ausente en S11)* | — | PKG_COMERCIAL BODY completamente INVALID por error de sintaxis en línea 1 (BUG-A) |

---

### 12.5 TABLA CONSOLIDADA DE TRIGGERS PLN — EVENTO CORRECTO

| Trigger PLN propuesto | Tabla | Evento correcto | Condición WHEN | PASO PLN |
|-----------------------|-------|-----------------|----------------|----------|
| `TIA_PLN_FROM_PARTIDA` *(nuevo)* | PARTIDA | AFTER INSERT | — | '04' Lote Disponible |
| `TUA_PLN_FROM_PARTIDA` | PARTIDA | AFTER UPDATE OF SITU_PART | `NEW.SITU_PART='R001'` | '06' En Tintorería |
| `TUA_PLN_FROM_L_VALIDA_RECETA` | L_VALIDA_RECETA | AFTER UPDATE OF ESTADO | `NEW.ESTADO='3'` | '05' Receta Validada |
| `TUA_PLN_FROM_TT_RPRODUC` *(renombrado)* | TT_RPRODUC | AFTER UPDATE OF ESTADO | `NEW.ESTADO='3'` + todos baños OK | '07' Teñido Completo |
| `TIA_PLN_FROM_TT_RSECADO` | TT_RSECADO | AFTER INSERT | GUIA IS NOT NULL | '08' Secado |
| `TUA_PLN_FROM_CTCALIDAD` *(renombrado)* | CTCALIDAD_D | AFTER UPDATE OF EST_EVALUACION,RESULTADO | `NEW.EST_EVALUACION='32'` | '09'/'9R' CC Resultado |
| `TIA_PLN_FROM_LOTES` | LOTES | AFTER INSERT | `NEW.COD_ALM IN ('03','07') AND NEW.TP_TRANSAC='16'` | '12' Ingreso PT |
| `TUA_PLN_FROM_LOTES_DESPACHO` *(alternativa)* | LOTES | AFTER UPDATE OF S_TRANSAC | `NEW.S_TRANSAC IN ('21','23')` | '14' Despachado/Cerrado |

---

*Sección 12 agregada: 18/05/2026 · Diagnóstico directo sobre BD Oracle 11.2.0.4 · Esquema SIG · Correcciones y nuevos hallazgos sobre módulo PLN_*

---

*Sección 11 agregada: 18/05/2026 · Análisis de bugs, gaps y consideraciones del módulo PLN_ · Revisión completa del ciclo PEDIDO → DESPACHO*

---

## 13. REVISIÓN FINAL — RESUMEN EJECUTIVO DE IMPLEMENTACIÓN (18/05/2026)

> Resultado de la revisión final del documento completo (Secciones 1–12). Consolida el estado actual del análisis, valida la coherencia interna y lista los ítems pendientes para la implementación definitiva.

---

### 13.1 COHERENCIA INTERNA — CORRECCIONES AL PROPIO DOCUMENTO

---

#### CORRECCIÓN 1 — Sección 12.5: PASO '13' en DESPACHO debería ser PASO '14'

En la tabla de triggers consolidada (Sección 12.5), el trigger `TUA_PLN_FROM_LOTES_DESPACHO` aparecía con PASO `'13'`. **Corregido en esta revisión**: PASO `'13'` = "Listo para Despacho" (calculado, sin trigger). PASO `'14'` = "Despachado/Cerrado" (LOTES.S_TRANSAC IN ('21','23')). El trigger de despacho avanza a PASO `'14'`.

---

#### CORRECCIÓN 2 — Sección 11 BUG 8: código de corrección referencia campo `GUIA` inexistente en CTCALIDAD_D

La corrección propuesta en BUG 8 sugería navegar con `p.numero = :NEW.guia`. **Problema**: `CTCALIDAD_D` NO tiene campo `GUIA`. La corrección definitiva (confirmada en Sección 12 BUG-B por el trigger existente `TIA_CTCALIDADD_RESULTADO`) es el mapeo directo:

| Campo CTCALIDAD_D | Campo ITEMPED_DET | Rol |
|---|---|---|
| `NRO_PEDIDO` | `NUM_PED` | Número de pedido |
| `SER_PARTIDA` | `NRO` | Número de ítem del pedido |
| `NROPART` | `NUM_DET` | Sub-lote |

Solo se requiere subconsulta para obtener `SERIE` desde `ITEMPED_DET`, ya que CTCALIDAD_D no la tiene.

---

#### CORRECCIÓN 3 — Sección 12.2: H_RPRODUC.GUIA = PARTIDA.NUMERO confirma navegación de TIA_PLN_FROM_H_RPRODUC

BUG-F documenta que `H_RPRODUC.GUIA = PARTIDA.NUMERO` en el 99.99% de los casos. Esto confirma que el trigger `TIA_PLN_FROM_H_RPRODUC` (PASO '03') puede navegar:

```sql
H_RPRODUC.GUIA → PARTIDA.NUMERO
               → PARTIDA.NROPROG  → ITEMPED_DET.NROPROG
               → PARTIDA.SERIE    → ITEMPED_DET.SERIE
               → PARTIDA.NRO_PEDIDO
```

`PARTIDA.SERIE` y `PARTIDA.NRO_PEDIDO` están disponibles en `:NEW` para `TUA_PLN_FROM_PARTIDA`; para `TIA_PLN_FROM_H_RPRODUC` requieren subconsulta a PARTIDA via GUIA.

---

### 13.2 ÍTEMS PENDIENTES — NO DOCUMENTADOS EN SECCIONES 11-12

---

#### PENDIENTE 1 — Propuesta.md: PLN_PARAM falta INSERT de DIAS_BUFFER_LAB

BUG 12 (Sección 11) propone `DIAS_BUFFER_LAB` como parámetro de buffer de laboratorio, pero el INSERT no está en el listado formal de PLN_PARAM en Propuesta.md.

**Acción:**
```sql
INSERT INTO PLN_PARAM VALUES
('DIAS_BUFFER_LAB', 'Días buffer laboratorio (validación receta)', 1, NULL, NULL, USER, SYSDATE);
```

---

#### PENDIENTE 2 — Propuesta.md: PLN_ESTADO_CODIGO falta INSERT de PASO '09B' (Gaseado)

GAP 1 (Sección 11) propone PASO `'09B'` para el proceso de gaseado post-tintorería (PROCESO='24'), pero el INSERT no está incluido en la lista formal de Propuesta.md.

**Acción:**
```sql
INSERT INTO PLN_ESTADO_CODIGO VALUES
('09B','Gaseado post-TT',
 'Proceso GAS post-tintorería; condicional solo para PROCESO=24 (Peinado Gaseado)',
 9.5, 'H_PRODUCCION_D', 'N', '#e83e8c');
```
> Este paso representa ~4.983 ítems y ~540 toneladas (Hallazgo-O, Sección 12).

---

#### PENDIENTE 3 — DDL PLN_SEGUIMIENTO: campo NRO_CICLO para reprocesos

GAP 4 (Sección 11) propone `NRO_CICLO NUMBER DEFAULT 1` para contar ciclos de reproceso TT, pero este campo no está en el DDL actual de PLN_SEGUIMIENTO en Propuesta.md.

**Acción — agregar al DDL:**
```sql
NRO_CICLO    NUMBER(3)  DEFAULT 1  NOT NULL,  -- ciclo TT: 1=primera vez, 2=primer reproceso...
```

---

#### PENDIENTE 4 — TT_PROGPART carece del campo SERIE

Hallazgo-F (Sección 12) confirma que `TT_PROGPART` tiene `(NUM_PED, NRO, NUM_DET, FENTREGA, ESTADO)` — sin campo `SERIE`. Al necesitar actualizar PLN_SEGUIMIENTO cruzando TT_PROGPART, derivar SERIE así:

```sql
SELECT p.serie INTO v_serie
FROM pedido p WHERE p.num_ped = :NEW.num_ped AND ROWNUM = 1;
```

---

#### PENDIENTE 5 — V_DRAW, PKG_COMERCIAL, POST_TO_API, REGISTRA_LOGIN: objetos INVALID (backlog BD)

Verificado que **ninguno de estos objetos es referenciado por PLN_**. Son código legado preexistente. **No bloquean el despliegue**. Registrar en backlog de BD para corrección futura independiente del proyecto PLN_.

| Objeto | Error | Prioridad |
|--------|-------|-----------|
| `PKG_COMERCIAL BODY` | PLS-00103 sintaxis | Baja |
| `V_DRAW` | ORA-00918 columna ambigua | Baja |
| `POST_TO_API` | PLS-00302 OTHERS no declarado | Baja |
| `REGISTRA_LOGIN` | ORA-00942 tabla inexistente | Baja |

---

#### PENDIENTE 6 — PACKING_G.NUM_PED es VARCHAR2(20): join requiere conversión de tipo

Hallazgo-P (Sección 12) confirma que `PACKING_G.NUM_PED VARCHAR2(20)` ≠ `ITEMPED_DET.NUM_PED NUMBER`. Además, PACKING_G no tiene campos NRO ni NUM_DET directos.

**Ruta de navegación correcta para trigger de PASO '13B':**
```
PACKING_L → LOTES.LOTE → LOTES.PARTIDA → PARTIDA.NROPROG → ITEMPED_DET.NROPROG
```
> Sin depender de TO_NUMBER(PACKING_G.NUM_PED).

---

#### PENDIENTE 7 — CT_RECPART (1.020.783 registros): estructura y rol desconocidos

GAP 9 (Sección 11) recomienda investigar CT_RECPART antes del despliegue. Si esta tabla registra trazabilidad receta→partida complementaria a PARTIDA_MAS, los triggers PLN podrían conflictuar con ella.

**Acción previa al despliegue:**
```sql
SELECT column_name, data_type, nullable
FROM user_tab_columns WHERE table_name = 'CT_RECPART' ORDER BY column_id;

SELECT COUNT(*) total, COUNT(DISTINCT num_ped) pedidos_distintos FROM ct_recpart;
```

---

### 13.3 ORDEN DE IMPLEMENTACIÓN RECOMENDADO

```
FASE 1 — Previo al despliegue (Día 0)
══════════════════════════════════════
  [ ] Backup completo de tablas involucradas: ITEMPED_DET, PARTIDA, TT_RPRODUC, CTCALIDAD_D, LOTES
  [ ] Analizar CT_RECPART (estructura y rol — ver PENDIENTE 7) [puede hacerse en paralelo]

  NOTA — Objetos INVALID (PKG_COMERCIAL BODY, POST_TO_API, REGISTRA_LOGIN, V_DRAW):
  ─────────────────────────────────────────────────────────────────────────────────
  Son código legado preexistente. PLN_ NO los referencia. NO bloquean este despliegue.
  Registrar en backlog de BD para corrección futura independiente.

FASE 2 — Tablas y catálogos (Semana 1)
═══════════════════════════════════════
  1. PLN_PARAM — con todos los registros, incluye DIAS_BUFFER_LAB
  2. PLN_ESTADO_CODIGO — incluye PASO '09B' (Gaseado)
  3. PLN_SEGUIMIENTO — DDL con NRO_CICLO y FCH_ENTREGA_PARTIDA
  4. PLN_LOG_EVENTOS, PLN_ALERTA, PLN_CARGA_DIARIA, PLN_FECHAS_ESTIMADAS
  5. Secuencias PLN_SEQ_*
  6. Carga histórica de pedidos activos (ESTADO IN ('0','5','9'))

FASE 3 — Procedimientos base (Semana 1-2)
══════════════════════════════════════════
  7. SP_PLN_INIT_SEGUIMIENTO — con p_paso_ini para SOLO_DESPACHO='S'
  8. SP_PLN_AVANZA_PASO — con correcciones de BUGs 1-14 (Sección 11)
  9. SP_PLN_CALCULA_FECHAS — usa kgr_hr de máquina asignada + buffer laboratorio
 10. SP_PLN_GENERA_ALERTAS — incluye alerta INCP (TT_INCUMPLE_PROG)

FASE 4 — Triggers en orden de dependencia (Semana 2-3)
════════════════════════════════════════════════════════
 11. TIA_PLN_FROM_ITEMPED          (PASO '01') — maneja SOLO_DESPACHO='S'
 12. TUA_PLN_FROM_ITEMPED_DET      (PASO '02')
 13. TIA_PLN_FROM_H_RPRODUC        (PASO '03') — navega via GUIA → PARTIDA
 14. TIA_PLN_FROM_PARTIDA          (PASO '04') — AFTER INSERT; ausente en borrador inicial
 15. TUA_PLN_FROM_L_VALIDA_RECETA  (PASO '05') — usa .NROPROG directo
 16. TUA_PLN_FROM_PARTIDA          (PASO '06') — SITU_PART='R001'; :NEW.SERIE y NRO_PEDIDO disponibles
 17. TUA_PLN_FROM_TT_RPRODUC       (PASO '07') — AFTER UPDATE ESTADO='3' + verifica todos los baños
 18. TIA_PLN_FROM_TT_RSECADO       (PASO '08') — navega via GUIA → PARTIDA
 19. TUA_PLN_FROM_CTCALIDAD        (PASO '09'/'9R') — AFTER UPDATE; mapeo SER_PARTIDA=NRO, NROPART=NUM_DET
 20. TIA_PLN_FROM_REVISADO         (PASO '11')
 21. TIA_PLN_FROM_LOTES_PT         (PASO '12') — COD_ALM IN ('03','07'), TP_TRANSAC='16'
 22. TUA_PLN_FROM_LOTES_DESPACHO   (PASO '14') — S_TRANSAC IN ('21','23'); navega via PARTIDA

FASE 5 — Vistas y KPIs (Semana 4)
═══════════════════════════════════
 23. V_PLN_ESTADO_PEDIDO     — muestra peor paso (MIN orden) cuando hay múltiples NUM_DET
 24. V_PLN_ESTADO_ITEM
 25. V_PLN_PENDIENTES_DESP   — filtra COD_PASO_ACT IN ('12','13'), no ('10','11')
 26. V_PLN_ALERTAS_ACTIVAS
 27. V_PLN_CARGA_MAQUINAS    — descuenta horas de MA_CRONOGRAMA_ACTIVO
 28. V_PLN_KPI_CUMPLIMIENTO  — mide en PASO '14' (despacho real), no PASO '12'
 29. V_PLN_KPI_PRODUCCION
 30. V_PLN_KPI_RETRASOS

FASE 6 — Jobs programados (Semana 5)
══════════════════════════════════════
 31. Job nocturno  : SP_PLN_GENERA_ALERTAS
 32. Job semanal   : SP_PLN_CARGA_DIARIA_REFRESH (con MA_CRONOGRAMA_ACTIVO)
 33. Job mensual   : SP_PLN_CIERRE_ITEM

FASE 7 — Pantallas .NET + Validación (Semana 6)
════════════════════════════════════════════════
 34. Panel maestro       (V_PLN_ESTADO_PEDIDO)
 35. Detalle de ítem     (V_PLN_TRAZABILIDAD)
 36. Panel de alertas    (V_PLN_ALERTAS_ACTIVAS)
 37. Panel de despacho   (V_PLN_PENDIENTES_DESP)
 38. KPI gerencia        (V_PLN_KPI_*)
 39. Validación funcional con usuarios clave
```

---

### 13.4 CHECKLIST DEFINITIVO — ANTES DE IR A PRODUCCIÓN

```
PRE-DESPLIEGUE
══════════════
[ ] Backup tomado: ITEMPED_DET, PARTIDA, TT_RPRODUC, CTCALIDAD_D, LOTES
[ ] Script DROP TRIGGER IF EXISTS preparado para rollback completo
[ ] CT_RECPART analizada (puede hacerse en paralelo con FASE 2 — no bloqueante)
[ ] — Objetos INVALID (PKG_COMERCIAL, POST_TO_API, REGISTRA_LOGIN, V_DRAW): código legado
      PLN_ no los usa → NO requieren corrección previa al despliegue
      Registrar en backlog de BD para corrección independiente

TABLAS PLN_
═══════════
[ ] PLN_SEGUIMIENTO incluye NRO_CICLO y FCH_ENTREGA_PARTIDA en el DDL
[ ] PLN_PARAM incluye DIAS_BUFFER_LAB
[ ] PLN_ESTADO_CODIGO incluye PASO '09B' (Gaseado, color '#e83e8c')
[ ] Secuencias PLN_SEQ_* creadas

PROCEDIMIENTOS
══════════════
[ ] SP_PLN_AVANZA_PASO: FCH_REAL_PARTIDA en PASO '04' (no '05')
[ ] SP_PLN_AVANZA_PASO: FCH_REAL_TIN_FIN en PASO '07' (no igualado a '08')
[ ] SP_PLN_AVANZA_PASO: ESTADO='C' solo en PASO '14' + KG_DESPACHADOS >= CANTIDAD_ORIG
[ ] SP_PLN_AVANZA_PASO: KG_DESPACHADOS actualizado en PASO '14' (no '12')
[ ] SP_PLN_AVANZA_PASO: KG_PRODUCIDOS acumulado solo en PASO '04' (no '05')
[ ] SP_PLN_AVANZA_PASO: despacho parcial retrocede a PASO '13' si KG < CANTIDAD_ORIG
[ ] SP_PLN_CALCULA_FECHAS: usa kgr_hr de ITEMPED_DET.MAQUINA (no MAX global)
[ ] SP_PLN_CALCULA_FECHAS: incluye buffer de laboratorio (DIAS_BUFFER_LAB días)
[ ] SP_PLN_INIT_SEGUIMIENTO: usa ITEMPED.F_MAXPED → ITEMPED_DET.FHC_ENTREGA (no plazo genérico)
[ ] SP_PLN_INIT_SEGUIMIENTO: acepta p_paso_ini para SOLO_DESPACHO='S' (inicia en '13')

TRIGGERS
════════
[ ] TIA_PLN_FROM_PARTIDA (INSERT) existe — PASO '04'
[ ] TUA_PLN_FROM_PARTIDA (UPDATE SITU_PART='R001') existe — PASO '06'
[ ] TUA_PLN_FROM_TT_RPRODUC es AFTER UPDATE OF ESTADO (no INSERT) — PASO '07'
    y verifica que TODOS los baños de PARTIDA_MAS están ESTADO='3'
[ ] TUA_PLN_FROM_CTCALIDAD es AFTER UPDATE OF EST_EVALUACION,RESULTADO (no INSERT) — PASO '09'/'9R'
    y usa mapeo: SER_PARTIDA→NRO, NROPART→NUM_DET
[ ] TUA_PLN_FROM_LOTES_DESPACHO actúa sobre LOTES.S_TRANSAC (no KARDEX_G) — PASO '14'
[ ] NINGÚN trigger tiene SERIE = 1 hardcodeada; SERIE siempre derivada de :NEW o subconsulta
[ ] TIA_PLN_FROM_ITEMPED maneja SOLO_DESPACHO='S' (inicio en PASO '13')
[ ] Todos los triggers tienen EXCEPTION WHEN OTHERS THEN NULL (no bloquean la operación)

VISTAS
══════
[ ] V_PLN_PENDIENTES_DESP filtra COD_PASO_ACT IN ('12','13') — no ('10','11')
[ ] V_PLN_KPI_CUMPLIMIENTO filtra ESTADO='C' AND COD_PASO_ACT='14'
[ ] V_PLN_ESTADO_PEDIDO muestra MIN(orden_paso) cuando hay múltiples NUM_DET del mismo ítem

VALIDACIÓN FUNCIONAL
═════════════════════
[ ] Prueba con pedido 88586 (14 ítems, mezcla PROCESO='01','20','24')
[ ] Prueba con ítem SOLO_DESPACHO='S' — debe iniciar en PASO '13', no '01'
[ ] Prueba de despacho parcial — PLN no debe cerrar hasta KG_DESPACHADOS >= CANTIDAD_ORIG
[ ] Prueba de reproceso (partida rechazada CC → vuelve TT → re-aprobada)
[ ] Prueba con ítem que tiene 2 NUM_DET en paralelo en pasos distintos
[ ] Prueba de multi-baño (PARTIDA_MAS con 2+ baños) — PASO '07' solo avanza al terminar TODOS
[ ] Verificar que los triggers NO bloquean inserts/updates en tablas productivas
[ ] Verificar PLN_LOG_EVENTOS no duplica eventos de tabla SEGUIMIENTO existente
```

---

### 13.5 REGLAS ABSOLUTAS — NUNCA VIOLAR EN IMPLEMENTACIÓN PLN_

```
1. NAVEGACIÓN:     Siempre por ITEMPED_DET.NROPROG → PARTIDA.NROPROG. NUNCA por LOTE directamente.
2. SERIE:          Nunca hardcodear SERIE=1. Derivar siempre desde :NEW o subconsulta.
3. NUM_DET:        Nunca asumir NUM_DET=0 o 1. Derivar desde ITEMPED_DET.
4. STOCK:          ALMACEN.STOCK es 100% gestionado por triggers. Nunca calcular manualmente.
5. TRIGGERS:       Siempre con EXCEPTION WHEN OTHERS THEN NULL. PLN no puede bloquear planta.
6. MULTI-BAÑO:     PASO '07' (Teñido Completo) solo avanza cuando TODOS los baños ESTADO='3'.
7. CIERRE:         ESTADO='C' solo en PASO '14' Y KG_DESPACHADOS >= CANTIDAD_ORIG.
8. DESPACHO:       Trigger de despacho va en LOTES.S_TRANSAC IN ('21','23'), no en KARDEX_G.
9. CTCALIDAD_D:    SER_PARTIDA=NRO (ítem), NROPART=NUM_DET (sub-lote). Confirmado por trigger existente.
10. ORACLE 11g:    No usar FETCH FIRST N ROWS ONLY. Usar patrón WHERE ROWNUM = 1.
```

---

*Sección 13 agregada: 18/05/2026 · Revisión final completa del documento · Correcciones de coherencia interna + ítems pendientes + checklist definitivo de implementación*

---

*Documento generado: 16/05/2026 · Fuente: Oracle 11.2.0.4 · Esquema SIG · 1.016 tablas · 157 vistas · 18 paquetes*
