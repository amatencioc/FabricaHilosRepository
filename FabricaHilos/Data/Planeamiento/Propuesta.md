# PROPUESTA TÉCNICA — SISTEMA DE PLANEAMIENTO, SEGUIMIENTO Y CONTROL DE PLANTA
## Base de datos SIG · Oracle 11.2.0.4 · Módulo: PLN_

> **Basado en:** análisis completo del esquema SIG (ver `Planeamiento.md`)  
> **Alcance:** BD completa — nuevas tablas, vistas, procedures, triggers y secuencias  
> **Objetivo:** Cobertura total del ciclo Pedido → Despacho con trazabilidad, alertas y KPIs en tiempo real

---

## 1. DIAGNÓSTICO — QUÉ EXISTE VS. QUÉ FALTA

### Lo que YA existe (base de datos)

| Proceso | Tablas existentes | Estado |
|---------|-------------------|--------|
| Toma del pedido | `PEDIDO`, `ITEMPED` | ✅ Completo |
| Planificación por etapa | `ITEMPED_DET` (con `NUM_DET` para sub-lotes) | ✅ Existe, incompleto en fechas |
| Programación hilandería (spinning) | `H_RECETA_G`, `H_RPRODUC` (por tipo máquina) | ✅ Completo |
| Devanado post-tintorería | `H_PROGRAMACION` (GUIA=PARTIDA.NUMERO) | ✅ Existe |
| Parte diario producción | `H_PRODUCCION_G/D` | ✅ Completo |
| Lote físico producido | `PARTIDA` (NROPROG=ITEMPED_DET.NROPROG) | ✅ Existe, SITU_PART como semáforo |
| Puente partida → recetas | `PARTIDA_MAS` (1 PARTIDA → N ING_RECETAS) | ✅ Existe |
| Receta tintorería | `ING_RECETAS_G/D`, `RECETA_G/D` | ✅ Completo |
| Programa tintorería | `TT_PROGPART/D` | ✅ Existe |
| Producción real TT | `TT_RPRODUC`, `TT_RSECADO` | ✅ Completo |
| Control calidad tintorería | `CTCALIDAD_D` (EST: '13'=Pend, '02'=Reval, '32'=OK) | ✅ Existe |
| Revisión de conos | `REVISADO_G/D` (peso, aprobados, merma) | ✅ Existe |
| Inventario PT | `LOTES` | ✅ Completo |
| Movimiento almacén | `KARDEX_G/D/L`, `ALMACEN` | ✅ Completo |
| Despacho | `DESPACHO_GUIA`, `DESPED_ALM` | ✅ Existe |
| Tiempos estándar | `CTRUTAS_TITULO`, `TT_PARAMPROGTIN` | ✅ Existen |
| Tracking de eventos existente | `SEGUIMIENTO` (AREA: PLANEAMIENTO/CCALIDAD/PROG HILAND/REVISADO) | ✅ Existe, parcial |
| Pipeline de estado por ítem | `V_STATUS_PEDIDO` (9 etapas: LAB→RECETA→TT→SECADO→CCAL→DEVNADO→REVISADO→ALMPT→DESP) | ✅ Vista existente |

### Lo que FALTA (brecha detectada)

| Necesidad | Problema actual | Solución propuesta |
|-----------|----------------|-------------------|
| Estado consolidado del pedido | Los estados están dispersos en 12+ tablas | `PLN_SEGUIMIENTO` |
| Cálculo automático de fechas estimadas | Solo hay campos manuales en `ITEMPED_DET` | `PLN_FECHAS_ESTIMADAS` + `SP_PLN_CALCULA_FECHAS` |
| Alertas de retraso | No existe ningún mecanismo | `PLN_ALERTA` + `SP_PLN_GENERA_ALERTAS` |
| Log estructurado de eventos PLN | SEGUIMIENTO existe pero es parcial y manual | `PLN_LOG_EVENTOS` (complementa, no duplica) |
| Control de carga de máquinas | Solo `CARGA_MAQ` estático | `PLN_CARGA_DIARIA` (dinámica) |
| KPIs de cumplimiento | No existen | Vistas `V_PLN_KPI_*` |
| Trazabilidad por lote | Dispersa en varias tablas | `V_PLN_TRAZABILIDAD` |
| Panel de despacho pendiente | No existe | `V_PLN_PENDIENTES_DESP` |
| Visibilidad de sub-lotes (NUM_DET>0) | ITEMPED_DET oculta la partición | PLN_SEGUIMIENTO por NUM_DET |

---

## 2. ARQUITECTURA GENERAL DEL SISTEMA PROPUESTO

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    MÓDULO PLN_ — PLANEAMIENTO DE PLANTA                 │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  TABLAS BASE (existentes)           TABLAS NUEVAS (PLN_)               │
│  ─────────────────────────          ─────────────────────────────────   │
│  PEDIDO / ITEMPED                   PLN_SEGUIMIENTO  ← tabla maestra   │
│  ITEMPED_DET                        PLN_LOG_EVENTOS                    │
│  H_PROGRAMACION                     PLN_ALERTA                         │
│  H_PRODUCCION_G/D                   PLN_CARGA_DIARIA                   │
│  PARTIDA                            PLN_FECHAS_ESTIMADAS               │
│  ING_RECETAS_G/D                    PLN_PARAM  (config. del módulo)    │
│  TT_PROGPART/D                      PLN_ESTADO_CODIGO (catálogo)        │
│  TT_RPRODUC / TT_RSECADO                                                │
│  LOTES / KARDEX_G/D                                                     │
│  DESPACHO_GUIA                                                          │
│                                                                         │
│  VISTAS NUEVAS (V_PLN_)             PROCEDURES NUEVOS (SP_PLN_)        │
│  ─────────────────────              ──────────────────────────────────  │
│  V_PLN_ESTADO_PEDIDO                SP_PLN_INIT_SEGUIMIENTO             │
│  V_PLN_ESTADO_ITEM                  SP_PLN_CALCULA_FECHAS              │
│  V_PLN_TRAZABILIDAD                 SP_PLN_AVANZA_PASO                 │
│  V_PLN_ALERTAS_ACTIVAS              SP_PLN_GENERA_ALERTAS              │
│  V_PLN_CARGA_MAQUINAS               SP_PLN_CARGA_DIARIA_REFRESH        │
│  V_PLN_PENDIENTES_DESP              SP_PLN_CIERRE_ITEM                 │
│  V_PLN_KPI_CUMPLIMIENTO             SP_PLN_REPROGRAMAR                 │
│  V_PLN_KPI_PRODUCCION                                                   │
│  V_PLN_KPI_RETRASOS                                                     │
│                                                                         │
│  TRIGGERS NUEVOS (TIA_/TUA_/TIB_PLN_)                                  │
│  ───────────────────────────────────────                                │
│  TIA_PLN_FROM_ITEMPED               → al insertar ítem de pedido       │
│  TUA_PLN_FROM_ITEMPED_DET           → al asignar programa/fechas       │
│  TIA_PLN_FROM_H_RPRODUC             → al iniciar producción hilandería │
│  TIA_PLN_FROM_PARTIDA               → al crear PARTIDA (lote listo)    │
│  TUA_PLN_FROM_L_VALIDA_RECETA       → al validar receta laboratorio    │
│  TUA_PLN_FROM_PARTIDA               → al avanzar SITU_PART='R001'      │
│  TUA_PLN_FROM_TT_RPRODUC            → al terminar TODOS los baños TT   │
│  TIA_PLN_FROM_TT_RSECADO            → al registrar secado              │
│  TUA_PLN_FROM_CTCALIDAD             → al aprobar/rechazar CC           │
│  TIA_PLN_FROM_REVISADO              → al aprobar revisado conos        │
│  TIA_PLN_FROM_LOTES_PT              → al ingresar a Almacén PT         │
│  TUA_PLN_FROM_LOTES_DESPACHO        → al despachar (LOTES.S_TRANSAC)  │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 3. CATÁLOGO DE PASOS DEL FLUJO (PLN_ESTADO_CODIGO)

Tabla de referencia de los 14 pasos del ciclo completo. Los pasos 03-04 cubren hilandería (PATH A) en paralelo con la planificación de tintorería (PATH B). Los pasos 06-12 mapean las 9 etapas de `V_STATUS_PEDIDO`:

| COD_PASO | NOMBRE | TABLA_ORIGEN | EVENTO_TRIGGER | NOTA |
|----------|--------|--------------|----------------|------|
| `01` | Pedido registrado | `ITEMPED` INSERT | `TIA_PLN_FROM_ITEMPED` | |
| `02` | Planificado (etapa asignada) | `ITEMPED_DET` UPDATE `NROPROG IS NOT NULL` | `TUA_PLN_FROM_ITEMPED_DET` | |
| `03` | En hilandería | `H_RPRODUC` INSERT (`GUIA`=PARTIDA.NUMERO) | `TIA_PLN_FROM_H_RPRODUC` | GUIA→PARTIDA→NROPROG→ITEMPED_DET |
| `04` | Lote disponible (hilo crudo listo) | `PARTIDA` INSERT (NROPROG IS NOT NULL) | `TIA_PLN_FROM_PARTIDA` | SERIE y NRO_PEDIDO en :NEW |
| `05` | Laboratorio (receta validada) | `L_VALIDA_RECETA` UPDATE ESTADO='3' | `TUA_PLN_FROM_L_VALIDA_RECETA` | NROPROG→ITEMPED_DET |
| `06` | Partida en tintorería | `PARTIDA` UPDATE SITU_PART='R001' | `TUA_PLN_FROM_PARTIDA` | SERIE/:NEW, NRO+NUM_DET←ITEMPED_DET |
| `07` | **Tenido completo** (todos los baños) | `TT_RPRODUC` UPDATE ESTADO='3' (último baño) | `TUA_PLN_FROM_TT_RPRODUC` | Verifica TODOS los baños ESTADO='3' antes de avanzar |
| `08` | Secado | `TT_RSECADO` INSERT (GUIA=PARTIDA.NUMERO) | `TIA_PLN_FROM_TT_RSECADO` | GUIA→PARTIDA→NROPROG→ITEMPED_DET |
| `09` | CC TT aprobado | `CTCALIDAD_D` UPDATE EST_EVALUACION='32' RESULTADO IN ('01','29') | `TUA_PLN_FROM_CTCALIDAD` | NRO_PEDIDO+SER_PARTIDA+NROPART→ITEMPED_DET |
| `9R` | **CC TT rechazado → reproceso** | `CTCALIDAD_D` UPDATE RESULTADO='30' | `TUA_PLN_FROM_CTCALIDAD` | Mismo trigger, camino alternativo |
| `10` | Devanado (madeja→cono) | `H_PROGRAMACION` GUIA=PARTIDA | *(via SP manual)* | |
| `11` | Revisado (peso y calidad final) | `REVISADO_D` INSERT APROBADO>0 | `TIA_PLN_FROM_REVISADO` | REVISADO_G→PARTIDA→NROPROG→ITEMPED_DET |
| `12` | Ingresado almacén PT | `LOTES` INSERT TP_TRANSAC='16' COD_ALM IN ('03','07') | `TIA_PLN_FROM_LOTES_PT` | PARTIDA→NROPROG→ITEMPED_DET |
| `13` | Listo para despacho | `ALMACEN.STOCK > 0` + `ITEMPED.SALDO > 0` | *(calculado)* | |
| `14` | **Despachado / Cerrado** | `LOTES` UPDATE S_TRANSAC IN ('21','23') | `TUA_PLN_FROM_LOTES_DESPACHO` | PARTIDA→NROPROG→ITEMPED_DET |

---

## 4. NUEVAS TABLAS

### 4.1 PLN_SEGUIMIENTO — Tabla maestra de seguimiento

Esta es la tabla más importante del módulo. Una fila por cada `(SERIE, NUM_PED, NRO, NUM_DET)` de `ITEMPED_DET`. Consolida el estado actual de cada etapa planificada.

```sql
CREATE TABLE PLN_SEGUIMIENTO (
  -- PK / FK al pedido
  ID_SEGUIM         NUMBER(12)       NOT NULL,  -- PK, secuencia PLN_SEQ_SEGUIM
  SERIE             NUMBER(3)        NOT NULL,  -- FK ITEMPED
  NUM_PED           NUMBER(8)        NOT NULL,  -- FK ITEMPED
  NRO               NUMBER(2)        NOT NULL,  -- FK ITEMPED (ítem)
  NUM_DET           NUMBER(3)        NOT NULL,  -- FK ITEMPED_DET (sub-lote; 0 si único)

  -- Datos del pedido (desnormalizados para performance)
  COD_CLIENTE       VARCHAR2(15),               -- de PEDIDO
  COD_ART           VARCHAR2(25),               -- de ITEMPED
  COLOR             VARCHAR2(7),
  TITULO            VARCHAR2(10),
  PROCESO           VARCHAR2(4),                -- H_PROCESOS (01=CARDADO, 20=PEINADO, 24=PEIN.GAS.)
  LOTE              VARCHAR2(20),               -- de ITEMPED_DET.LOTE (vincula a hilandería)
  CANTIDAD_ORIG     NUMBER(12,4),               -- cantidad original pedida

  -- Paso actual
  COD_PASO_ACT      VARCHAR2(2)      NOT NULL,  -- FK PLN_ESTADO_CODIGO
  COD_PASO_ANT      VARCHAR2(2),                -- paso anterior (historial)

  -- Fechas comprometidas (del contrato con el cliente)
  FCH_PEDIDO        DATE             NOT NULL,  -- fecha del pedido
  FCH_ENTREGA_COMP  DATE,                       -- fecha comprometida al cliente

  -- Fechas estimadas (calculadas por SP_PLN_CALCULA_FECHAS)
  FCH_EST_HILANDERIA  DATE,                     -- estimado inicio hilandería
  FCH_EST_PARTIDA     DATE,                     -- estimado salida hilandería
  FCH_EST_TIN_INI     DATE,                     -- estimado entrada tintorería
  FCH_EST_TIN_FIN     DATE,                     -- estimado salida tintorería
  FCH_EST_SECADO      DATE,                     -- estimado fin secado
  FCH_EST_CALIDAD     DATE,                     -- estimado salida QC
  FCH_EST_DESPACHO    DATE,                     -- estimado despacho

  -- Fechas reales (actualizado por triggers)
  FCH_REAL_PROGRAMADO DATE,                     -- cuando se programó en H_PROGRAMACION (devanado)
  FCH_REAL_PRODUCCION DATE,                     -- primer H_PRODUCCION_D (hilatura)
  FCH_REAL_PARTIDA    DATE,                     -- cuando se creó PARTIDA
  FCH_REAL_TIN_INI    DATE,                     -- cuando entró a tintorería
  FCH_REAL_TIN_FIN    DATE,                     -- cuando salió de tintorería
  FCH_REAL_SECADO     DATE,                     -- cuando terminó secado
  FCH_REAL_CC_TINTO   DATE,                     -- cuando CTCALIDAD_D aprobó (EST='32', RESULTADO IN ('01','29'))
  FCH_REAL_CC_RECHAZO DATE,                     -- cuando CTCALIDAD_D rechazó (RESULTADO='30') → reproceso
  FCH_REAL_DEVANADO   DATE,                     -- cuando entró a devanado (H_PROGRAMACION)
  FCH_REAL_CALIDAD    DATE,                     -- cuando REVISADO_G registró aprobados
  FCH_REAL_ALM_PT     DATE,                     -- cuando ingresó a almacén PT (LOTES TP=16)
  FCH_REAL_DESPACHO   DATE,                     -- cuando se despachó

  -- Cantidades acumuladas
  KG_PRODUCIDOS     NUMBER(12,4)   DEFAULT 0,   -- kg acumulados en hilandería
  KG_EN_TIN         NUMBER(12,4)   DEFAULT 0,   -- kg en tintorería
  KG_EN_ALM_PT      NUMBER(12,4)   DEFAULT 0,   -- kg en almacén PT
  KG_DESPACHADOS    NUMBER(12,4)   DEFAULT 0,   -- kg despachados
  KG_PENDIENTES     NUMBER(12,4)   DEFAULT 0,   -- kg aún pendientes

  -- Indicadores de estado y alerta
  IND_RETRASO       VARCHAR2(1)    DEFAULT 'N', -- S/N retraso detectado
  DIAS_RETRASO      NUMBER(5)      DEFAULT 0,   -- días de retraso
  IND_URGENTE       VARCHAR2(1)    DEFAULT 'N', -- S/N urgente
  IND_REPROCESO     VARCHAR2(1)    DEFAULT 'N', -- S/N en reproceso (CC rechazado)

  -- Referencias a objetos del flujo
  NUM_PROGRAMA      NUMBER(8),                  -- H_PROGRAMACION.NUMERO
  NUM_PARTIDA       NUMBER(8),                  -- PARTIDA.NUMERO
  NUM_RECETA_TIN    NUMBER(8),                  -- ING_RECETAS_G.NUMERO
  NUM_KARDEX_DESP   NUMBER(8),                  -- KARDEX_G.NUMERO del despacho

  -- Estado del seguimiento
  ESTADO            VARCHAR2(1)    DEFAULT 'A', -- A=Activo, C=Cerrado, X=Anulado

  -- Auditoría
  A_ADUSER          VARCHAR2(15),
  A_ADFECHA         DATE,
  A_MDUSER          VARCHAR2(15),
  A_MDFECHA         DATE,

  CONSTRAINT PK_PLN_SEGUIMIENTO PRIMARY KEY (ID_SEGUIM),
  CONSTRAINT UK_PLN_SEGUIM      UNIQUE (SERIE, NUM_PED, NRO, NUM_DET),
  CONSTRAINT FK_PLN_SEG_ITEMPED FOREIGN KEY (SERIE, NUM_PED, NRO)
    REFERENCES ITEMPED (SERIE, NUM_PED, NRO)
);

CREATE INDEX IX_PLN_SEG_PEDIDO    ON PLN_SEGUIMIENTO (NUM_PED, SERIE);
CREATE INDEX IX_PLN_SEG_CLIENTE   ON PLN_SEGUIMIENTO (COD_CLIENTE);
CREATE INDEX IX_PLN_SEG_PASO      ON PLN_SEGUIMIENTO (COD_PASO_ACT, ESTADO);
CREATE INDEX IX_PLN_SEG_FCH_DESP  ON PLN_SEGUIMIENTO (FCH_EST_DESPACHO, ESTADO);
CREATE INDEX IX_PLN_SEG_ALERTA    ON PLN_SEGUIMIENTO (IND_RETRASO, ESTADO);

CREATE SEQUENCE PLN_SEQ_SEGUIM START WITH 1 INCREMENT BY 1 NOCACHE NOCYCLE;
```

---

### 4.2 PLN_LOG_EVENTOS — Historial de eventos del flujo

Cada avance de paso queda registrado aquí. Inmutable (solo INSERT). Es la auditoría del flujo.

```sql
CREATE TABLE PLN_LOG_EVENTOS (
  ID_EVENTO         NUMBER(12)       NOT NULL,  -- PK, secuencia PLN_SEQ_EVENTO
  ID_SEGUIM         NUMBER(12)       NOT NULL,  -- FK PLN_SEGUIMIENTO
  SERIE             NUMBER(3)        NOT NULL,
  NUM_PED           NUMBER(8)        NOT NULL,
  NRO               NUMBER(2)        NOT NULL,
  NUM_DET           NUMBER(3)        NOT NULL,

  COD_PASO          VARCHAR2(2)      NOT NULL,  -- paso que ocurrió
  DESC_PASO         VARCHAR2(100),              -- descripción del evento
  TABLA_ORIGEN      VARCHAR2(30),               -- tabla que disparó el evento
  ID_OBJETO_ORIGEN  NUMBER(12),                 -- ID del registro origen
  FCH_EVENTO        DATE             NOT NULL,  -- fecha/hora del evento
  USUARIO           VARCHAR2(15),               -- usuario que actuó

  -- Datos del evento
  KG_CANTIDAD       NUMBER(12,4),               -- cantidad involucrada
  FCH_ESTIMADA_ANT  DATE,                       -- fecha estimada ANTES del evento
  FCH_ESTIMADA_NUE  DATE,                       -- fecha estimada NUEVA
  OBSERVACION       VARCHAR2(300),              -- observación libre
  TIPO_EVENTO       VARCHAR2(2),                -- AV=Avance, RE=Retraso, AL=Alerta, CI=Cierre

  CONSTRAINT PK_PLN_LOG_EVENTOS PRIMARY KEY (ID_EVENTO)
);

CREATE INDEX IX_PLN_LOG_SEG    ON PLN_LOG_EVENTOS (ID_SEGUIM);
CREATE INDEX IX_PLN_LOG_PEDIDO ON PLN_LOG_EVENTOS (NUM_PED, SERIE);
CREATE INDEX IX_PLN_LOG_FECHA  ON PLN_LOG_EVENTOS (FCH_EVENTO);

CREATE SEQUENCE PLN_SEQ_EVENTO START WITH 1 INCREMENT BY 1 NOCACHE NOCYCLE;
```

---

### 4.3 PLN_ALERTA — Alertas activas del sistema

```sql
CREATE TABLE PLN_ALERTA (
  ID_ALERTA         NUMBER(12)       NOT NULL,  -- PK, secuencia PLN_SEQ_ALERTA
  ID_SEGUIM         NUMBER(12),                 -- FK PLN_SEGUIMIENTO (puede ser NULL para alertas globales)
  SERIE             NUMBER(3),
  NUM_PED           NUMBER(8),
  NRO               NUMBER(2),
  NUM_DET           NUMBER(3),

  TIP_ALERTA        VARCHAR2(4)      NOT NULL,  -- ver catálogo abajo
  NIVEL             VARCHAR2(1)      NOT NULL,  -- C=Crítico, A=Alto, M=Medio, B=Bajo
  TITULO            VARCHAR2(100)    NOT NULL,  -- descripción corta
  DETALLE           VARCHAR2(500),              -- detalle completo
  FCH_ALERTA        DATE             NOT NULL,  -- cuando se generó
  FCH_LIMITE        DATE,                       -- fecha límite antes de escalar
  DIAS_RETRASO      NUMBER(5),                  -- días de retraso calculados

  -- Referencias del objeto afectado
  TABLA_REF         VARCHAR2(30),               -- tabla del objeto
  ID_REF            NUMBER(12),                 -- ID del objeto
  COD_MAQ           VARCHAR2(6),                -- máquina si aplica
  COD_CLIENTE       VARCHAR2(15),               -- cliente si aplica

  -- Estado de la alerta
  ESTADO            VARCHAR2(1)    DEFAULT 'A', -- A=Activa, R=Resuelta, I=Ignorada
  FCH_RESOLUCION    DATE,
  USUARIO_RESUELVE  VARCHAR2(15),
  OBSERV_RESOL      VARCHAR2(300),

  -- Auditoría
  A_ADUSER          VARCHAR2(15),
  A_ADFECHA         DATE,

  CONSTRAINT PK_PLN_ALERTA PRIMARY KEY (ID_ALERTA)
);

CREATE INDEX IX_PLN_ALERT_SEG    ON PLN_ALERTA (ID_SEGUIM);
CREATE INDEX IX_PLN_ALERT_ESTADO ON PLN_ALERTA (ESTADO, NIVEL, FCH_ALERTA);
CREATE INDEX IX_PLN_ALERT_PEDIDO ON PLN_ALERTA (NUM_PED);

CREATE SEQUENCE PLN_SEQ_ALERTA START WITH 1 INCREMENT BY 1 NOCACHE NOCYCLE;
```

#### Catálogo de tipos de alerta (TIP_ALERTA)

| Código | Nivel | Descripción |
|--------|-------|-------------|
| `RET1` | C | Retraso > 7 días vs. fecha comprometida al cliente |
| `RET2` | A | Retraso 3–7 días |
| `RET3` | M | Retraso 1–2 días |
| `SMP` | A | Sin material/programa asignado (más de 2 días sin `ITEMPED_DET.NROPROG`) |
| `STN` | C | Partida sin entrada a tintorería después de FCH_EST_TIN_INI |
| `MAQP` | A | Máquina en parada no programada (`TT_RPARADA` > 8 horas) |
| `QCF` | C | Partida rechazada en control de calidad |
| `SLPT` | M | Sin lotes en almacén PT y fecha estimada despacho < hoy + 3 días |
| `REPR` | M | Partida en reproceso (PARTIDA.TIPO_RODETE = 'R') |
| `PREC` | A | Precio del ítem sin aprobar (`ITEMPED.IND_PRECIOVAL` IS NULL) |

---

### 4.4 PLN_CARGA_DIARIA — Carga real de máquinas por día

```sql
CREATE TABLE PLN_CARGA_DIARIA (
  FECHA             DATE             NOT NULL,
  COD_MAQ           VARCHAR2(6)      NOT NULL,
  TP_MAQ            VARCHAR2(1)      NOT NULL,  -- H=Hilandería, T=Tintorería

  -- Capacidad (de CTRUTAS_TITULO / TT_MAQUINA)
  HORAS_CAPACIDAD   NUMBER(5,2)      DEFAULT 24, -- horas disponibles del día
  KG_CAPACIDAD      NUMBER(12,4),               -- kg/día según velocidad estándar

  -- Carga asignada (suma de ITEMPED_DET programados ese día)
  HORAS_ASIGNADAS   NUMBER(5,2)      DEFAULT 0,
  KG_ASIGNADOS      NUMBER(12,4)     DEFAULT 0,
  NRO_PEDIDOS       NUMBER(5)        DEFAULT 0,  -- cantidad de ítems asignados

  -- Real (de H_PRODUCCION_D / TT_RPRODUC)
  HORAS_REAL        NUMBER(5,2)      DEFAULT 0,
  KG_REAL           NUMBER(12,4)     DEFAULT 0,

  -- Indicadores
  PCT_UTILIZACION   NUMBER(5,2)      DEFAULT 0,  -- % utilización real
  PCT_CARGA         NUMBER(5,2)      DEFAULT 0,  -- % carga asignada vs capacidad
  IND_SOBRECARGADA  VARCHAR2(1)      DEFAULT 'N',

  -- Auditoría
  FCH_CALCULO       DATE,
  A_MDUSER          VARCHAR2(15),
  A_MDFECHA         DATE,

  CONSTRAINT PK_PLN_CARGA PRIMARY KEY (FECHA, COD_MAQ)
);

CREATE INDEX IX_PLN_CARGA_MAQ   ON PLN_CARGA_DIARIA (COD_MAQ, FECHA);
CREATE INDEX IX_PLN_CARGA_FCH   ON PLN_CARGA_DIARIA (FECHA, TP_MAQ);
```

---

### 4.5 PLN_FECHAS_ESTIMADAS — Log de recálculos de fechas

Guarda cada vez que SP_PLN_CALCULA_FECHAS recalcula las fechas, con el motivo.

```sql
CREATE TABLE PLN_FECHAS_ESTIMADAS (
  ID_FECH           NUMBER(12)       NOT NULL,
  ID_SEGUIM         NUMBER(12)       NOT NULL,  -- FK PLN_SEGUIMIENTO
  FCH_CALCULO       DATE             NOT NULL,
  MOTIVO_RECALCULO  VARCHAR2(4),                -- PED=nuevo pedido, REP=reprogramación, MAQ=parada máquina

  -- Fechas calculadas en esta iteración
  FCH_EST_HILANDERIA  DATE,
  FCH_EST_PARTIDA     DATE,
  FCH_EST_TIN_INI     DATE,
  FCH_EST_TIN_FIN     DATE,
  FCH_EST_SECADO      DATE,
  FCH_EST_CALIDAD     DATE,
  FCH_EST_DESPACHO    DATE,

  -- Diferencia vs. cálculo anterior (días)
  DIFER_DIAS        NUMBER(5),
  USUARIO           VARCHAR2(15),

  CONSTRAINT PK_PLN_FECHAS PRIMARY KEY (ID_FECH)
);

CREATE SEQUENCE PLN_SEQ_FECHAS START WITH 1 INCREMENT BY 1 NOCACHE NOCYCLE;
```

---

### 4.6 PLN_PARAM — Parámetros del módulo de planeamiento

```sql
CREATE TABLE PLN_PARAM (
  COD_PARAM         VARCHAR2(20)     NOT NULL,
  DESCRIPCION       VARCHAR2(100)    NOT NULL,
  VALOR_NUM         NUMBER(12,4),
  VALOR_TEXT        VARCHAR2(100),
  VALOR_DATE        DATE,
  A_MDUSER          VARCHAR2(15),
  A_MDFECHA         DATE,

  CONSTRAINT PK_PLN_PARAM PRIMARY KEY (COD_PARAM)
);

-- Datos iniciales
INSERT INTO PLN_PARAM VALUES ('DIAS_ALERTA_CRIT',  'Días retraso para alerta CRÍTICA', 7,    NULL, NULL, USER, SYSDATE);
INSERT INTO PLN_PARAM VALUES ('DIAS_ALERTA_ALTA',  'Días retraso para alerta ALTA',    3,    NULL, NULL, USER, SYSDATE);
INSERT INTO PLN_PARAM VALUES ('DIAS_ALERTA_MEDIA', 'Días retraso para alerta MEDIA',   1,    NULL, NULL, USER, SYSDATE);
INSERT INTO PLN_PARAM VALUES ('HRS_HILANDERIA',    'Horas/día operativas hilandería',   22,   NULL, NULL, USER, SYSDATE);
INSERT INTO PLN_PARAM VALUES ('HRS_TINTORERIA',    'Horas/día operativas tintorería',   24,   NULL, NULL, USER, SYSDATE);
INSERT INTO PLN_PARAM VALUES ('HRS_SECADO',        'Horas buffer post-secado',           8,   NULL, NULL, USER, SYSDATE);
INSERT INTO PLN_PARAM VALUES ('DIAS_BUFFER_QC',    'Días para control calidad',          1,   NULL, NULL, USER, SYSDATE);
INSERT INTO PLN_PARAM VALUES ('DIAS_BUFFER_DESP',  'Días para preparar despacho',        1,   NULL, NULL, USER, SYSDATE);
COMMIT;
```

---

### 4.7 PLN_ESTADO_CODIGO — Catálogo de pasos del flujo

```sql
CREATE TABLE PLN_ESTADO_CODIGO (
  COD_PASO          VARCHAR2(2)      NOT NULL,
  NOMBRE_PASO       VARCHAR2(60)     NOT NULL,
  DESCRIPCION       VARCHAR2(200),
  ORDEN_PASO        NUMBER(2)        NOT NULL,
  TABLA_ORIGEN      VARCHAR2(30),
  ES_FINAL          VARCHAR2(1)      DEFAULT 'N',
  COLOR_UI          VARCHAR2(10),               -- color para el dashboard (#RRGGBB)

  CONSTRAINT PK_PLN_ESTADO PRIMARY KEY (COD_PASO)
);

INSERT INTO PLN_ESTADO_CODIGO VALUES ('01','Pedido Registrado',     'Ítem de pedido creado en ITEMPED',                                              1,'ITEMPED',         'N','#6c757d');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('02','Planificado',           'Etapa asignada en ITEMPED_DET (NROPROG asignado)',                               2,'ITEMPED_DET',      'N','#0d6efd');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('03','En Hilandería',         'H_RPRODUC INSERT — GUIA=PARTIDA.NUMERO → producción inicio',                    3,'H_RPRODUC',        'N','#0dcaf0');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('04','Lote Disponible',       'PARTIDA INSERT (NROPROG NOT NULL) — hilo crudo producido y lote creado',        4,'PARTIDA',          'N','#17a2b8');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('05','Laboratorio',           'L_VALIDA_RECETA UPDATE ESTADO=3 — receta de teñido validada',                   5,'L_VALIDA_RECETA',  'N','#6610f2');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('06','En Tintorería',         'PARTIDA UPDATE SITU_PART=R001 — partida ingresó a tintorería',                  6,'PARTIDA',          'N','#6f42c1');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('07','Tenido Completo',       'TT_RPRODUC UPDATE ESTADO=3 — TODOS los baños de la partida terminados (75% multi-baño)', 7,'TT_RPRODUC','N','#d63384');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('08','Secado',                'TT_RSECADO INSERT — GUIA=PARTIDA.NUMERO → secado registrado',                   8,'TT_RSECADO',       'N','#20c997');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('09','CC TT Aprobado',        'CTCALIDAD_D UPDATE EST_EVALUACION=32 RESULTADO IN (01,29) — aprobado/concesionado', 9,'CTCALIDAD_D','N','#fd7e14');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('9R','CC TT Rechazado→Reproceso','CTCALIDAD_D UPDATE RESULTADO=30 — rechazado, requiere reproceso (2.7% de evaluados)',10,'CTCALIDAD_D','N','#dc3545');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('10','Devanado',              'H_PROGRAMACION GUIA=PARTIDA — madeja a cono',                                  11,'H_PROGRAMACION',   'N','#ffc107');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('11','Revisado',              'REVISADO_D INSERT APROBADO>0 — peso y calidad final aprobados',                 12,'REVISADO_D',       'N','#0d6efd');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('12','Ingresado Almacén PT',  'LOTES INSERT COD_ALM IN (03,07) TP_TRANSAC=16 PARTIDA NOT NULL',               13,'LOTES',            'N','#198754');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('13','Listo para Despacho',   'Stock en ALMACEN > 0, saldo ITEMPED > 0',                                       14,NULL,               'N','#20c997');
INSERT INTO PLN_ESTADO_CODIGO VALUES ('14','Despachado/Cerrado',    'LOTES UPDATE S_TRANSAC IN (21,23) — despacho nacional o exportación',           15,'LOTES',            'S','#198754');
COMMIT;
```

---

## 5. NUEVAS VISTAS

### 5.1 V_PLN_ESTADO_PEDIDO — Dashboard principal por pedido

```sql
CREATE OR REPLACE VIEW V_PLN_ESTADO_PEDIDO AS
SELECT
  p.serie,
  p.num_ped,
  p.fecha               AS fch_pedido,
  p.cod_cliente,
  cl.nombre             AS nom_cliente,
  p.estado              AS estado_pedido,
  p.prioridad,
  COUNT(s.id_seguim)    AS total_items,
  SUM(CASE WHEN s.cod_paso_act = '12' THEN 1 ELSE 0 END)  AS items_cerrados,
  SUM(CASE WHEN s.cod_paso_act != '12' THEN 1 ELSE 0 END) AS items_pendientes,
  SUM(CASE WHEN s.ind_retraso = 'S' THEN 1 ELSE 0 END)    AS items_con_retraso,
  SUM(s.cantidad_orig)  AS kg_total_pedido,
  SUM(s.kg_despachados) AS kg_despachados,
  SUM(s.kg_pendientes)  AS kg_pendientes,
  ROUND(SUM(s.kg_despachados) / NULLIF(SUM(s.cantidad_orig),0) * 100, 1) AS pct_avance,
  MIN(s.fch_entrega_comp)  AS fch_entrega_minima,
  MAX(s.fch_real_despacho) AS fch_ultimo_despacho,
  MAX(s.dias_retraso)      AS max_dias_retraso,
  MAX(s.fch_est_despacho)  AS fch_est_despacho_max
FROM pedido p
JOIN clientes cl ON cl.cod_cliente = p.cod_cliente
LEFT JOIN pln_seguimiento s ON s.serie = p.serie AND s.num_ped = p.num_ped AND s.estado = 'A'
WHERE p.estado IN ('0','5','9')  -- pedidos activos
GROUP BY p.serie, p.num_ped, p.fecha, p.cod_cliente, cl.nombre, p.estado, p.prioridad;
```

---

### 5.2 V_PLN_ESTADO_ITEM — Detalle por ítem con semáforo

```sql
CREATE OR REPLACE VIEW V_PLN_ESTADO_ITEM AS
SELECT
  s.id_seguim,
  s.serie,
  s.num_ped,
  s.nro,
  s.num_det,
  s.cod_cliente,
  cl.nombre              AS nom_cliente,
  s.cod_art,
  ar.descripcion         AS desc_art,
  s.color,
  s.titulo,
  s.proceso,
  s.cantidad_orig        AS kg_pedido,
  s.kg_producidos,
  s.kg_en_tin,
  s.kg_en_alm_pt,
  s.kg_despachados,
  s.kg_pendientes,
  ROUND(s.kg_despachados / NULLIF(s.cantidad_orig,0) * 100,1) AS pct_avance,
  s.cod_paso_act,
  ec.nombre_paso,
  ec.color_ui,
  s.fch_pedido,
  s.fch_entrega_comp,
  s.fch_est_despacho,
  s.fch_real_despacho,
  s.dias_retraso,
  s.ind_retraso,
  s.ind_urgente,
  -- Semáforo calculado
  CASE
    WHEN s.dias_retraso >= 7 THEN 'R'          -- Rojo
    WHEN s.dias_retraso >= 3 THEN 'A'           -- Ámbar
    WHEN s.dias_retraso >= 1 THEN 'Y'           -- Amarillo
    ELSE 'G'                                     -- Verde
  END AS semaforo,
  s.num_programa,
  s.num_partida,
  pt.situ_part,
  s.num_kardex_desp,
  s.estado               AS estado_seguim
FROM pln_seguimiento s
JOIN clientes cl ON cl.cod_cliente = s.cod_cliente
JOIN articul  ar ON ar.cod_art     = s.cod_art
JOIN pln_estado_codigo ec ON ec.cod_paso = s.cod_paso_act
LEFT JOIN partida pt ON pt.numero = s.num_partida;
```

---

### 5.3 V_PLN_TRAZABILIDAD — Trazabilidad completa del ítem

```sql
CREATE OR REPLACE VIEW V_PLN_TRAZABILIDAD AS
SELECT
  s.num_ped,
  s.nro,
  s.num_det,
  s.cod_cliente,
  s.cod_art,
  -- Pedido
  pe.fecha               AS fch_pedido,
  pe.f_aprobacion        AS fch_aprob_pedido,
  -- Planificación
  id.fhc_prog            AS fch_planeada,
  id.fhc_entrega         AS fch_entrega_plan,
  id.fch_estima_cono_uno AS fch_est_cono1,
  id.fch_estima_tenido   AS fch_est_tenido,
  -- Hilandería
  s.fch_real_programado,
  s.fch_real_produccion,
  s.fch_real_partida,
  -- Tintorería
  s.fch_real_tin_ini,
  tt.fentrega            AS fch_prog_tin,
  s.fch_real_tin_fin,
  s.fch_real_secado,
  -- Calidad / Almacén PT
  s.fch_real_calidad,
  s.fch_real_alm_pt,
  -- Despacho
  s.fch_real_despacho,
  s.fch_entrega_comp     AS fch_compromiso_cliente,
  -- KPIs de tiempo (días entre etapas)
  s.fch_real_partida    - pe.fecha                     AS dias_pedido_a_partida,
  s.fch_real_tin_fin    - s.fch_real_tin_ini            AS dias_en_tintoreria,
  s.fch_real_alm_pt     - s.fch_real_partida            AS dias_partida_a_almpt,
  s.fch_real_despacho   - s.fch_real_alm_pt             AS dias_almpt_a_despacho,
  s.fch_real_despacho   - pe.fecha                      AS dias_total_ciclo,
  s.fch_real_despacho   - s.fch_entrega_comp            AS dias_desvio_cliente,
  -- Estado actual
  s.cod_paso_act,
  s.dias_retraso
FROM pln_seguimiento s
JOIN pedido     pe ON pe.serie = s.serie AND pe.num_ped = s.num_ped
JOIN itemped_det id ON id.serie = s.serie AND id.num_ped = s.num_ped
                   AND id.nro = s.nro AND id.num_det = s.num_det
LEFT JOIN tt_progpart tt ON tt.num_ped = s.num_ped AND tt.nro = s.nro AND tt.num_det = s.num_det;
```

---

### 5.4 V_PLN_ALERTAS_ACTIVAS — Panel de alertas

```sql
CREATE OR REPLACE VIEW V_PLN_ALERTAS_ACTIVAS AS
SELECT
  a.id_alerta,
  a.tip_alerta,
  a.nivel,
  a.titulo,
  a.detalle,
  a.fch_alerta,
  a.fch_limite,
  a.dias_retraso,
  a.num_ped,
  a.nro,
  a.cod_cliente,
  cl.nombre     AS nom_cliente,
  a.cod_maq,
  a.estado,
  SYSDATE - a.fch_alerta  AS horas_sin_resolver
FROM pln_alerta a
LEFT JOIN clientes cl ON cl.cod_cliente = a.cod_cliente
WHERE a.estado = 'A'
ORDER BY
  CASE a.nivel WHEN 'C' THEN 1 WHEN 'A' THEN 2 WHEN 'M' THEN 3 ELSE 4 END,
  a.fch_alerta;
```

---

### 5.5 V_PLN_CARGA_MAQUINAS — Carga de máquinas (próximos 30 días)

```sql
CREATE OR REPLACE VIEW V_PLN_CARGA_MAQUINAS AS
SELECT
  c.fecha,
  c.cod_maq,
  c.tp_maq,
  c.horas_capacidad,
  c.kg_capacidad,
  c.horas_asignadas,
  c.kg_asignados,
  c.nro_pedidos,
  c.horas_real,
  c.kg_real,
  c.pct_utilizacion,
  c.pct_carga,
  c.ind_sobrecargada,
  -- Semáforo de carga
  CASE
    WHEN c.pct_carga > 95 THEN 'SOBRECARGADA'
    WHEN c.pct_carga > 80 THEN 'CARGA_ALTA'
    WHEN c.pct_carga > 50 THEN 'CARGA_MEDIA'
    ELSE 'DISPONIBLE'
  END AS estado_carga
FROM pln_carga_diaria c
WHERE c.fecha BETWEEN TRUNC(SYSDATE) AND TRUNC(SYSDATE) + 30;
```

---

### 5.6 V_PLN_PENDIENTES_DESP — Pendientes de despacho (priorizado)

```sql
CREATE OR REPLACE VIEW V_PLN_PENDIENTES_DESP AS
SELECT
  s.num_ped,
  s.nro,
  s.cod_cliente,
  cl.nombre           AS nom_cliente,
  s.cod_art,
  ar.descripcion      AS desc_art,
  s.color,
  s.titulo,
  s.kg_pendientes,
  al.stock            AS stock_disponible,
  LEAST(s.kg_pendientes, NVL(al.stock,0)) AS kg_a_despachar,
  s.fch_entrega_comp,
  TRUNC(SYSDATE) - s.fch_entrega_comp  AS dias_vencido,
  s.dias_retraso,
  s.ind_urgente,
  s.cod_paso_act,
  ec.nombre_paso,
  p.prioridad         AS prioridad_pedido
FROM pln_seguimiento s
JOIN clientes    cl ON cl.cod_cliente = s.cod_cliente
JOIN articul     ar ON ar.cod_art     = s.cod_art
JOIN pedido       p ON p.serie = s.serie AND p.num_ped = s.num_ped
JOIN pln_estado_codigo ec ON ec.cod_paso = s.cod_paso_act
LEFT JOIN almacen al ON al.cod_art = s.cod_art AND al.cod_alm = '01'  -- almacén PT principal
WHERE s.cod_paso_act IN ('10','11')   -- en almacén PT o listo para despacho
  AND s.kg_pendientes > 0
  AND s.estado = 'A'
ORDER BY
  CASE WHEN s.ind_urgente='S' THEN 0 ELSE 1 END,
  p.prioridad DESC,
  s.fch_entrega_comp;
```

---

### 5.7 V_PLN_KPI_CUMPLIMIENTO — KPI de cumplimiento de entregas

```sql
CREATE OR REPLACE VIEW V_PLN_KPI_CUMPLIMIENTO AS
SELECT
  TRUNC(s.fch_real_despacho,'MM')                    AS periodo,
  COUNT(*)                                           AS total_items_cerrados,
  SUM(CASE WHEN s.fch_real_despacho <= s.fch_entrega_comp THEN 1 ELSE 0 END) AS entregados_a_tiempo,
  SUM(CASE WHEN s.fch_real_despacho >  s.fch_entrega_comp THEN 1 ELSE 0 END) AS entregados_tarde,
  ROUND(SUM(CASE WHEN s.fch_real_despacho <= s.fch_entrega_comp THEN 1 ELSE 0 END)
        / NULLIF(COUNT(*),0) * 100, 1)               AS pct_otif,
  ROUND(AVG(s.fch_real_despacho - s.fch_pedido),1)   AS ciclo_promedio_dias,
  ROUND(AVG(s.fch_real_tin_fin - s.fch_real_tin_ini),1) AS dias_prom_tintoreria,
  ROUND(AVG(s.fch_real_partida - s.fch_pedido),1)    AS dias_prom_pedido_partida,
  SUM(s.kg_despachados)                              AS kg_total_despachados,
  ROUND(AVG(GREATEST(s.dias_retraso,0)),1)           AS retraso_promedio_dias
FROM pln_seguimiento s
WHERE s.cod_paso_act = '12'
  AND s.fch_real_despacho IS NOT NULL
GROUP BY TRUNC(s.fch_real_despacho,'MM')
ORDER BY 1 DESC;
```

---

### 5.8 V_PLN_KPI_PRODUCCION — KPI de eficiencia de producción

```sql
CREATE OR REPLACE VIEW V_PLN_KPI_PRODUCCION AS
SELECT
  TRUNC(h.fecha,'MM')                                         AS periodo,
  h.tp_maq,
  h.cod_maq,
  SUM(d.cantidad)                                             AS kg_producidos,
  ROUND(AVG(TO_NUMBER(d.horas_trabajadas)),2)                 AS horas_prom_turno,
  ROUND(AVG(TO_NUMBER(d.horas_parada)),2)                     AS horas_prom_parada,
  ROUND(SUM(d.cantidad) / NULLIF(SUM(TO_NUMBER(d.horas_trabajadas)),0),2) AS kg_por_hora,
  COUNT(DISTINCT h.fecha)                                     AS dias_activos
FROM h_produccion_d d
JOIN h_produccion_g h ON h.fecha   = d.fecha
                     AND h.turno   = d.turno
                     AND h.tp_maq  = d.tp_maq
                     AND h.cod_maq = d.cod_maq
                     AND h.c_codigo= d.c_codigo
WHERE h.fecha >= ADD_MONTHS(TRUNC(SYSDATE,'MM'), -12)
GROUP BY TRUNC(h.fecha,'MM'), h.tp_maq, h.cod_maq
ORDER BY 1 DESC, h.tp_maq, h.cod_maq;
```

---

## 6. NUEVOS PROCEDURES Y FUNCTIONS

### 6.1 SP_PLN_INIT_SEGUIMIENTO — Inicializa el seguimiento al crear el ítem

Se llama desde el trigger `TIA_PLN_FROM_ITEMPED`.

```sql
-- ESPECIFICACIÓN
CREATE OR REPLACE PROCEDURE SP_PLN_INIT_SEGUIMIENTO (
  p_serie     IN ITEMPED.SERIE%TYPE,
  p_num_ped   IN ITEMPED.NUM_PED%TYPE,
  p_nro       IN ITEMPED.NRO%TYPE,
  p_num_det   IN NUMBER DEFAULT 1
) AS
/*
  Inserta el primer registro en PLN_SEGUIMIENTO al crear el ítem de pedido.
  Paso inicial = '01' (Pedido registrado).
  Registra el evento en PLN_LOG_EVENTOS.
*/
  v_id        NUMBER;
  v_pedido    PEDIDO%ROWTYPE;
  v_item      ITEMPED%ROWTYPE;
BEGIN
  SELECT * INTO v_pedido FROM PEDIDO WHERE serie=p_serie AND num_ped=p_num_ped;
  SELECT * INTO v_item   FROM ITEMPED WHERE serie=p_serie AND num_ped=p_num_ped AND nro=p_nro;

  SELECT PLN_SEQ_SEGUIM.NEXTVAL INTO v_id FROM DUAL;

  INSERT INTO PLN_SEGUIMIENTO (
    ID_SEGUIM, SERIE, NUM_PED, NRO, NUM_DET,
    COD_CLIENTE, COD_ART, COLOR, TITULO, PROCESO, CANTIDAD_ORIG,
    COD_PASO_ACT, FCH_PEDIDO, FCH_ENTREGA_COMP,
    KG_PENDIENTES, IND_RETRASO, IND_URGENTE, ESTADO,
    A_ADUSER, A_ADFECHA
  ) VALUES (
    v_id, p_serie, p_num_ped, p_nro, p_num_det,
    v_pedido.cod_cliente, v_item.cod_art, v_item.color,
    v_item.titulo, v_item.proceso, v_item.cantidad,
    '01', v_pedido.fecha,
    v_pedido.fecha + NVL(v_pedido.plazo_entrega, 30),
    v_item.cantidad, 'N', NVL(v_item.desaprb,'N'), 'A',
    USER, SYSDATE
  );

  -- Log del evento
  INSERT INTO PLN_LOG_EVENTOS (
    ID_EVENTO, ID_SEGUIM, SERIE, NUM_PED, NRO, NUM_DET,
    COD_PASO, DESC_PASO, TABLA_ORIGEN, FCH_EVENTO, USUARIO,
    KG_CANTIDAD, TIPO_EVENTO
  ) VALUES (
    PLN_SEQ_EVENTO.NEXTVAL, v_id, p_serie, p_num_ped, p_nro, p_num_det,
    '01', 'Pedido registrado - ítem creado', 'ITEMPED', SYSDATE, USER,
    v_item.cantidad, 'AV'
  );
  COMMIT;
EXCEPTION
  WHEN OTHERS THEN
    ROLLBACK;
    RAISE;
END SP_PLN_INIT_SEGUIMIENTO;
```

---

### 6.2 SP_PLN_AVANZA_PASO — Avanza el paso en PLN_SEGUIMIENTO

```sql
CREATE OR REPLACE PROCEDURE SP_PLN_AVANZA_PASO (
  p_serie         IN  NUMBER,
  p_num_ped       IN  NUMBER,
  p_nro           IN  NUMBER,
  p_num_det       IN  NUMBER,
  p_nuevo_paso    IN  VARCHAR2,
  p_tabla_origen  IN  VARCHAR2,
  p_id_origen     IN  NUMBER    DEFAULT NULL,
  p_kg_cantidad   IN  NUMBER    DEFAULT NULL,
  p_observacion   IN  VARCHAR2  DEFAULT NULL
) AS
/*
  Avanza el paso de un ítem en PLN_SEGUIMIENTO.
  Registra el evento, actualiza fechas reales, recalcula días de retraso.
*/
  v_seg  PLN_SEGUIMIENTO%ROWTYPE;
  v_id_evt NUMBER;
BEGIN
  SELECT * INTO v_seg FROM PLN_SEGUIMIENTO
  WHERE serie=p_serie AND num_ped=p_num_ped AND nro=p_nro AND num_det=p_num_det
  FOR UPDATE;

  -- Actualizar fechas reales según el paso
  -- Mapa exacto de pasos → campos:
  --   02 → FCH_REAL_PROGRAMADO (NROPROG asignado en ITEMPED_DET)
  --   03 → FCH_REAL_PRODUCCION (H_RPRODUC INSERT: inicio hilandería)
  --   04 → FCH_REAL_PARTIDA    (PARTIDA INSERT: lote físico creado)
  --   06 → FCH_REAL_TIN_INI    (PARTIDA SITU_PART='R001': entró a TT)
  --   07 → FCH_REAL_TIN_FIN    (TT_RPRODUC ESTADO='3': TODOS los baños completos)
  --   08 → FCH_REAL_SECADO     (TT_RSECADO INSERT)
  --   09 → FCH_REAL_CC_TINTO   (CTCALIDAD_D RESULTADO='01'/'29': aprobado)
  --   9R → FCH_REAL_CC_RECHAZO (CTCALIDAD_D RESULTADO='30': rechazado/reproceso)
  --   10 → FCH_REAL_DEVANADO   (H_PROGRAMACION entrada)
  --   11 → FCH_REAL_CALIDAD    (REVISADO_D aprobados)
  --   12 → FCH_REAL_ALM_PT     (LOTES TP='16' COD_ALM IN ('03','07'))
  --   14 → FCH_REAL_DESPACHO   (LOTES S_TRANSAC IN ('21','23'))
  UPDATE PLN_SEGUIMIENTO SET
    COD_PASO_ANT        = COD_PASO_ACT,
    COD_PASO_ACT        = p_nuevo_paso,
    FCH_REAL_PROGRAMADO = CASE WHEN p_nuevo_paso='02' THEN SYSDATE ELSE FCH_REAL_PROGRAMADO END,
    FCH_REAL_PRODUCCION = CASE WHEN p_nuevo_paso='03' THEN SYSDATE ELSE FCH_REAL_PRODUCCION END,
    FCH_REAL_PARTIDA    = CASE WHEN p_nuevo_paso='04' THEN SYSDATE ELSE FCH_REAL_PARTIDA    END,
    FCH_REAL_TIN_INI    = CASE WHEN p_nuevo_paso='06' THEN SYSDATE ELSE FCH_REAL_TIN_INI    END,
    FCH_REAL_TIN_FIN    = CASE WHEN p_nuevo_paso='07' THEN SYSDATE ELSE FCH_REAL_TIN_FIN    END,
    FCH_REAL_SECADO     = CASE WHEN p_nuevo_paso='08' THEN SYSDATE ELSE FCH_REAL_SECADO     END,
    FCH_REAL_CC_TINTO   = CASE WHEN p_nuevo_paso='09' THEN SYSDATE ELSE FCH_REAL_CC_TINTO   END,
    FCH_REAL_CC_RECHAZO = CASE WHEN p_nuevo_paso='9R' THEN SYSDATE ELSE FCH_REAL_CC_RECHAZO END,
    FCH_REAL_DEVANADO   = CASE WHEN p_nuevo_paso='10' THEN SYSDATE ELSE FCH_REAL_DEVANADO   END,
    FCH_REAL_CALIDAD    = CASE WHEN p_nuevo_paso='11' THEN SYSDATE ELSE FCH_REAL_CALIDAD    END,
    FCH_REAL_ALM_PT     = CASE WHEN p_nuevo_paso='12' THEN SYSDATE ELSE FCH_REAL_ALM_PT     END,
    FCH_REAL_DESPACHO   = CASE WHEN p_nuevo_paso='14' THEN SYSDATE ELSE FCH_REAL_DESPACHO   END,
    -- Estado: cierre al despachar
    ESTADO              = CASE WHEN p_nuevo_paso='14' THEN 'C' ELSE ESTADO END,
    -- Reproceso: marcar/desmarcar según CC
    IND_REPROCESO       = CASE WHEN p_nuevo_paso='9R' THEN 'S'
                               WHEN p_nuevo_paso='09' THEN 'N'
                               ELSE IND_REPROCESO END,
    -- KG acumulados por etapa clave
    KG_PRODUCIDOS       = CASE WHEN p_nuevo_paso='04' THEN KG_PRODUCIDOS + NVL(p_kg_cantidad,0) ELSE KG_PRODUCIDOS END,
    KG_EN_TIN           = CASE WHEN p_nuevo_paso='06' THEN KG_EN_TIN    + NVL(p_kg_cantidad,0) ELSE KG_EN_TIN    END,
    KG_EN_ALM_PT        = CASE WHEN p_nuevo_paso='12' THEN KG_EN_ALM_PT + NVL(p_kg_cantidad,0) ELSE KG_EN_ALM_PT END,
    KG_DESPACHADOS      = CASE WHEN p_nuevo_paso='14' THEN KG_DESPACHADOS + NVL(p_kg_cantidad,0) ELSE KG_DESPACHADOS END,
    KG_PENDIENTES       = CASE WHEN p_nuevo_paso='14' THEN GREATEST(KG_PENDIENTES - NVL(p_kg_cantidad,0),0) ELSE KG_PENDIENTES END,
    -- Retraso
    DIAS_RETRASO        = GREATEST(TRUNC(SYSDATE) - TRUNC(FCH_ENTREGA_COMP), 0),
    IND_RETRASO         = CASE WHEN SYSDATE > FCH_ENTREGA_COMP THEN 'S' ELSE 'N' END,
    A_MDUSER            = USER,
    A_MDFECHA           = SYSDATE
  WHERE serie=p_serie AND num_ped=p_num_ped AND nro=p_nro AND num_det=p_num_det;

  -- Log del evento
  SELECT PLN_SEQ_EVENTO.NEXTVAL INTO v_id_evt FROM DUAL;
  INSERT INTO PLN_LOG_EVENTOS (
    ID_EVENTO, ID_SEGUIM, SERIE, NUM_PED, NRO, NUM_DET,
    COD_PASO, TABLA_ORIGEN, ID_OBJETO_ORIGEN, FCH_EVENTO, USUARIO,
    KG_CANTIDAD, OBSERVACION, TIPO_EVENTO
  ) VALUES (
    v_id_evt, v_seg.id_seguim, p_serie, p_num_ped, p_nro, p_num_det,
    p_nuevo_paso, p_tabla_origen, p_id_origen, SYSDATE, USER,
    p_kg_cantidad, p_observacion, 'AV'
  );
  COMMIT;
EXCEPTION
  WHEN NO_DATA_FOUND THEN NULL;  -- El seguimiento aún no existe, ignorar
  WHEN OTHERS THEN ROLLBACK; RAISE;
END SP_PLN_AVANZA_PASO;
```

---

### 6.3 SP_PLN_CALCULA_FECHAS — Cálculo de fechas estimadas

Usa tiempos estándar de `CTRUTAS_TITULO`, `TT_PARAMPROGTIN`, y `PLN_PARAM`.

```sql
CREATE OR REPLACE PROCEDURE SP_PLN_CALCULA_FECHAS (
  p_serie       IN NUMBER,
  p_num_ped     IN NUMBER,
  p_nro         IN NUMBER,
  p_num_det     IN NUMBER,
  p_motivo      IN VARCHAR2 DEFAULT 'PED'
) AS
/*
  Calcula y actualiza las fechas estimadas en PLN_SEGUIMIENTO.
  Lógica:
  1. Fecha estimada partida = fecha programación + (KG_pedido / KGR_HR de CTRUTAS_TITULO) días
  2. Fecha estimada TIN_INI = fecha_partida + 0 días buffer
  3. Fecha estimada TIN_FIN = fecha_TIN_INI + (tenido_horas/24) días
  4. Fecha estimada secado  = fecha_TIN_FIN + (secado_horas/24) días
  5. Fecha estimada calidad = fecha_secado + DIAS_BUFFER_QC días
  6. Fecha estimada despacho= fecha_calidad + DIAS_BUFFER_DESP días
*/
  v_seg      PLN_SEGUIMIENTO%ROWTYPE;
  v_item     ITEMPED%ROWTYPE;
  v_itemdet  ITEMPED_DET%ROWTYPE;
  v_kgr_hr   NUMBER := 10;    -- default si no se encuentra en CTRUTAS
  v_hrs_tin  NUMBER := 6;     -- tenido (de TT_PARAMPROGTIN)
  v_hrs_sec  NUMBER := 8;     -- secado (de PLN_PARAM)
  v_buf_qc   NUMBER := 1;
  v_buf_desp NUMBER := 1;
  v_hrs_hil  NUMBER := 22;
  v_fch_base DATE;
  -- Fechas calculadas
  v_est_hil  DATE;
  v_est_part DATE;
  v_est_tini DATE;
  v_est_tfin DATE;
  v_est_sec  DATE;
  v_est_cal  DATE;
  v_est_desp DATE;
  v_id_fech  NUMBER;
BEGIN
  SELECT * INTO v_seg  FROM PLN_SEGUIMIENTO WHERE serie=p_serie AND num_ped=p_num_ped AND nro=p_nro AND num_det=p_num_det;
  SELECT * INTO v_item FROM ITEMPED         WHERE serie=p_serie AND num_ped=p_num_ped AND nro=p_nro;

  BEGIN
    SELECT * INTO v_itemdet FROM ITEMPED_DET WHERE serie=p_serie AND num_ped=p_num_ped AND nro=p_nro AND num_det=p_num_det;
  EXCEPTION WHEN NO_DATA_FOUND THEN NULL;
  END;

  -- Leer parámetros
  BEGIN SELECT valor_num INTO v_hrs_tin  FROM PLN_PARAM WHERE cod_param='HRS_TINTORERIA'; EXCEPTION WHEN NO_DATA_FOUND THEN NULL; END;
  BEGIN SELECT valor_num INTO v_hrs_sec  FROM PLN_PARAM WHERE cod_param='HRS_SECADO';     EXCEPTION WHEN NO_DATA_FOUND THEN NULL; END;
  BEGIN SELECT valor_num INTO v_buf_qc   FROM PLN_PARAM WHERE cod_param='DIAS_BUFFER_QC'; EXCEPTION WHEN NO_DATA_FOUND THEN NULL; END;
  BEGIN SELECT valor_num INTO v_buf_desp FROM PLN_PARAM WHERE cod_param='DIAS_BUFFER_DESP'; EXCEPTION WHEN NO_DATA_FOUND THEN NULL; END;
  BEGIN SELECT valor_num INTO v_hrs_hil  FROM PLN_PARAM WHERE cod_param='HRS_HILANDERIA'; EXCEPTION WHEN NO_DATA_FOUND THEN NULL; END;

  -- Leer KGR_HR de CTRUTAS_TITULO
  BEGIN
    SELECT MAX(t.kgr_hr) INTO v_kgr_hr
    FROM ctrutas_titulo t
    WHERE t.titulo = v_item.titulo AND t.proceso = v_item.proceso
      AND t.estado != 'X';
  EXCEPTION WHEN NO_DATA_FOUND THEN NULL;
  END;

  -- Leer tiempo de tenido de TT_PARAMPROGTIN
  BEGIN
    SELECT NVL(tenido,6) INTO v_hrs_tin FROM tt_paramprogtin WHERE rownum=1;
  EXCEPTION WHEN NO_DATA_FOUND THEN NULL;
  END;

  -- Fecha base: hoy o fecha de programación si ya existe
  v_fch_base := NVL(v_seg.fch_real_programado, SYSDATE);

  -- Calcular días de hilandería (KG / KGR_HR / HRS_DÍA)
  v_est_hil  := TRUNC(v_fch_base);
  v_est_part := TRUNC(v_fch_base) + CEIL(v_item.cantidad / NULLIF(v_kgr_hr * v_hrs_hil, 0));
  v_est_tini := v_est_part;
  v_est_tfin := v_est_tini + (v_hrs_tin / 24);
  v_est_sec  := v_est_tfin + (v_hrs_sec / 24);
  v_est_cal  := TRUNC(v_est_sec)  + v_buf_qc;
  v_est_desp := v_est_cal + v_buf_desp;

  -- Actualizar PLN_SEGUIMIENTO
  UPDATE PLN_SEGUIMIENTO SET
    FCH_EST_HILANDERIA = v_est_hil,
    FCH_EST_PARTIDA    = v_est_part,
    FCH_EST_TIN_INI    = v_est_tini,
    FCH_EST_TIN_FIN    = v_est_tfin,
    FCH_EST_SECADO     = v_est_sec,
    FCH_EST_CALIDAD    = v_est_cal,
    FCH_EST_DESPACHO   = v_est_desp,
    DIAS_RETRASO       = GREATEST(TRUNC(SYSDATE) - TRUNC(FCH_ENTREGA_COMP), 0),
    IND_RETRASO        = CASE WHEN v_est_desp > FCH_ENTREGA_COMP THEN 'S' ELSE 'N' END,
    A_MDUSER           = USER,
    A_MDFECHA          = SYSDATE
  WHERE id_seguim = v_seg.id_seguim;

  -- Guardar historial de recálculo
  SELECT PLN_SEQ_FECHAS.NEXTVAL INTO v_id_fech FROM DUAL;
  INSERT INTO PLN_FECHAS_ESTIMADAS (
    ID_FECH, ID_SEGUIM, FCH_CALCULO, MOTIVO_RECALCULO,
    FCH_EST_HILANDERIA, FCH_EST_PARTIDA, FCH_EST_TIN_INI,
    FCH_EST_TIN_FIN, FCH_EST_SECADO, FCH_EST_CALIDAD, FCH_EST_DESPACHO,
    DIFER_DIAS, USUARIO
  ) VALUES (
    v_id_fech, v_seg.id_seguim, SYSDATE, p_motivo,
    v_est_hil, v_est_part, v_est_tini,
    v_est_tfin, v_est_sec, v_est_cal, v_est_desp,
    TRUNC(v_est_desp) - TRUNC(NVL(v_seg.fch_est_despacho, v_est_desp)),
    USER
  );

  -- Sincronizar ITEMPED_DET.FCH_ESTIMA_TENIDO y FCH_ESTIMA_CONO_UNO
  -- Estos campos ya existen en ITEMPED_DET (verificado en BD) y deben
  -- mantenerse en sincronía con los calculados por PLN_ para evitar divergencia
  BEGIN
    UPDATE ITEMPED_DET SET
      FCH_ESTIMA_TENIDO   = v_est_tini,   -- estimado entrada tintorería (= FCH_EST_TIN_INI)
      FCH_ESTIMA_CONO_UNO = v_est_tfin    -- estimado primer cono (= FCH_EST_TIN_FIN)
    WHERE serie = p_serie AND num_ped = p_num_ped
      AND nro = p_nro     AND num_det = p_num_det;
  EXCEPTION WHEN OTHERS THEN NULL;  -- no bloquear si falla
  END;

  COMMIT;
END SP_PLN_CALCULA_FECHAS;
```

---

### 6.4 SP_PLN_GENERA_ALERTAS — Motor de alertas

Debe ejecutarse con un **JOB nocturno** (DBMS_JOB o DBMS_SCHEDULER).

```sql
CREATE OR REPLACE PROCEDURE SP_PLN_GENERA_ALERTAS AS
/*
  Recorre PLN_SEGUIMIENTO y genera alertas en PLN_ALERTA.
  Se sugiere ejecutar 1 vez/hora.
*/
  v_dias_crit  NUMBER := 7;
  v_dias_alta  NUMBER := 3;
  v_dias_media NUMBER := 1;
  v_id_alerta  NUMBER;

  PROCEDURE ins_alerta (p_id_seg NUMBER, p_serie NUMBER, p_ped NUMBER, p_nro NUMBER,
                        p_det NUMBER, p_tip VARCHAR2, p_nivel VARCHAR2,
                        p_titulo VARCHAR2, p_detalle VARCHAR2, p_dias NUMBER,
                        p_cli VARCHAR2) IS
  BEGIN
    -- Solo insertar si no existe alerta activa del mismo tipo para el mismo ítem
    INSERT INTO PLN_ALERTA (
      ID_ALERTA, ID_SEGUIM, SERIE, NUM_PED, NRO, NUM_DET,
      TIP_ALERTA, NIVEL, TITULO, DETALLE, FCH_ALERTA,
      DIAS_RETRASO, COD_CLIENTE, ESTADO, A_ADUSER, A_ADFECHA
    )
    SELECT PLN_SEQ_ALERTA.NEXTVAL, p_id_seg, p_serie, p_ped, p_nro, p_det,
           p_tip, p_nivel, p_titulo, p_detalle, SYSDATE,
           p_dias, p_cli, 'A', USER, SYSDATE
    FROM DUAL
    WHERE NOT EXISTS (
      SELECT 1 FROM PLN_ALERTA
      WHERE id_seguim=p_id_seg AND tip_alerta=p_tip AND estado='A'
    );
  END;

BEGIN
  -- Leer parámetros
  BEGIN SELECT valor_num INTO v_dias_crit  FROM PLN_PARAM WHERE cod_param='DIAS_ALERTA_CRIT';  EXCEPTION WHEN NO_DATA_FOUND THEN NULL; END;
  BEGIN SELECT valor_num INTO v_dias_alta  FROM PLN_PARAM WHERE cod_param='DIAS_ALERTA_ALTA';  EXCEPTION WHEN NO_DATA_FOUND THEN NULL; END;
  BEGIN SELECT valor_num INTO v_dias_media FROM PLN_PARAM WHERE cod_param='DIAS_ALERTA_MEDIA'; EXCEPTION WHEN NO_DATA_FOUND THEN NULL; END;

  -- Retraso CRÍTICO (>= 7 días)
  FOR r IN (SELECT id_seguim, serie, num_ped, nro, num_det, cod_cliente, dias_retraso
            FROM PLN_SEGUIMIENTO
            WHERE estado='A' AND cod_paso_act != '12' AND dias_retraso >= v_dias_crit) LOOP
    ins_alerta(r.id_seguim, r.serie, r.num_ped, r.nro, r.num_det, 'RET1', 'C',
               'Retraso crítico > '||v_dias_crit||' días',
               'Pedido '||r.num_ped||' ítem '||r.nro||': '||r.dias_retraso||' días de retraso.',
               r.dias_retraso, r.cod_cliente);
  END LOOP;

  -- Retraso ALTO (3-6 días)
  FOR r IN (SELECT id_seguim, serie, num_ped, nro, num_det, cod_cliente, dias_retraso
            FROM PLN_SEGUIMIENTO
            WHERE estado='A' AND cod_paso_act != '12'
              AND dias_retraso >= v_dias_alta AND dias_retraso < v_dias_crit) LOOP
    ins_alerta(r.id_seguim, r.serie, r.num_ped, r.nro, r.num_det, 'RET2', 'A',
               'Retraso alto '||r.dias_retraso||' días',
               'Pedido '||r.num_ped||' ítem '||r.nro||': '||r.dias_retraso||' días de retraso.',
               r.dias_retraso, r.cod_cliente);
  END LOOP;

  -- Sin programa asignado más de 2 días después del pedido
  FOR r IN (SELECT s.id_seguim, s.serie, s.num_ped, s.nro, s.num_det, s.cod_cliente
            FROM PLN_SEGUIMIENTO s
            WHERE s.estado='A' AND s.cod_paso_act='01'
              AND TRUNC(SYSDATE) - TRUNC(s.fch_pedido) > 2) LOOP
    ins_alerta(r.id_seguim, r.serie, r.num_ped, r.nro, r.num_det, 'SMP', 'A',
               'Sin programa asignado',
               'Pedido '||r.num_ped||' ítem '||r.nro||': más de 2 días sin planificación.',
               NULL, r.cod_cliente);
  END LOOP;

  -- Partida sin ingresar a TT después de fecha estimada
  FOR r IN (SELECT s.id_seguim, s.serie, s.num_ped, s.nro, s.num_det, s.cod_cliente
            FROM PLN_SEGUIMIENTO s
            WHERE s.estado='A' AND s.cod_paso_act='05'
              AND TRUNC(SYSDATE) > TRUNC(NVL(s.fch_est_tin_ini, SYSDATE))) LOOP
    ins_alerta(r.id_seguim, r.serie, r.num_ped, r.nro, r.num_det, 'STN', 'C',
               'Partida sin ingresar a Tintorería',
               'Pedido '||r.num_ped||': partida lista pero no ha ingresado a TT.',
               NULL, r.cod_cliente);
  END LOOP;

  COMMIT;
END SP_PLN_GENERA_ALERTAS;
```

---

### 6.5 SP_PLN_CARGA_DIARIA_REFRESH — Recalcula carga de máquinas

```sql
CREATE OR REPLACE PROCEDURE SP_PLN_CARGA_DIARIA_REFRESH (
  p_fch_ini IN DATE DEFAULT TRUNC(SYSDATE),
  p_fch_fin IN DATE DEFAULT TRUNC(SYSDATE) + 30
) AS
/*
  Recalcula PLN_CARGA_DIARIA para el rango de fechas dado.
  Cruza H_PRODUCCION_D (real) con CTRUTAS_TITULO (estándar).
*/
BEGIN
  -- Eliminar el rango a recalcular
  DELETE FROM PLN_CARGA_DIARIA
  WHERE fecha BETWEEN p_fch_ini AND p_fch_fin;

  -- Insertar carga real desde H_PRODUCCION_D
  INSERT INTO PLN_CARGA_DIARIA (
    FECHA, COD_MAQ, TP_MAQ, HORAS_REAL, KG_REAL, FCH_CALCULO, A_MDFECHA
  )
  SELECT
    d.fecha,
    d.cod_maq,
    d.tp_maq,
    SUM(TO_NUMBER(REPLACE(d.horas_trabajadas,':','.'))),
    SUM(d.cantidad),
    SYSDATE,
    SYSDATE
  FROM h_produccion_d d
  WHERE d.fecha BETWEEN p_fch_ini AND p_fch_fin
  GROUP BY d.fecha, d.cod_maq, d.tp_maq;

  -- Actualizar PCT_UTILIZACION
  UPDATE PLN_CARGA_DIARIA SET
    PCT_UTILIZACION   = ROUND(KG_REAL / NULLIF(KG_CAPACIDAD,0) * 100, 2),
    PCT_CARGA         = ROUND(KG_ASIGNADOS / NULLIF(KG_CAPACIDAD,0) * 100, 2),
    IND_SOBRECARGADA  = CASE WHEN KG_ASIGNADOS > KG_CAPACIDAD THEN 'S' ELSE 'N' END
  WHERE fecha BETWEEN p_fch_ini AND p_fch_fin;

  COMMIT;
END SP_PLN_CARGA_DIARIA_REFRESH;
```

---

## 7. NUEVOS TRIGGERS

> **Convención de navegación uniforme:**  
> La cadena siempre termina en `ITEMPED_DET.(SERIE, NUM_PED, NRO, NUM_DET)`.  
> Los campos `SERIE` y `NRO_PEDIDO` están disponibles directamente en `PARTIDA.:NEW`.  
> El campo `LOTE` **no** es identificador único entre pedidos; **nunca navegar por LOTE**.

---

### 7.1 TIA_PLN_FROM_ITEMPED — Al insertar ítem de pedido (PASO '01')

```sql
CREATE OR REPLACE TRIGGER TIA_PLN_FROM_ITEMPED
AFTER INSERT ON ITEMPED
FOR EACH ROW
BEGIN
  -- PASO 01: Pedido registrado
  -- Inicializa con NUM_DET=0 (placeholder antes de que existan sub-lotes en ITEMPED_DET)
  SP_PLN_INIT_SEGUIMIENTO(:NEW.serie, :NEW.num_ped, :NEW.nro, 0);
  SP_PLN_CALCULA_FECHAS(:NEW.serie, :NEW.num_ped, :NEW.nro, 0, 'PED');
EXCEPTION
  WHEN OTHERS THEN NULL;  -- no bloquear la operación principal
END TIA_PLN_FROM_ITEMPED;
```

---

### 7.2 TUA_PLN_FROM_ITEMPED_DET — Al asignar programa/fechas (PASO '02')

```sql
CREATE OR REPLACE TRIGGER TUA_PLN_FROM_ITEMPED_DET
AFTER UPDATE ON ITEMPED_DET
FOR EACH ROW
WHEN (NEW.NROPROG IS NOT NULL AND (OLD.NROPROG IS NULL OR NEW.FHC_PROG != OLD.FHC_PROG))
BEGIN
  -- PASO 02: Planificado (NROPROG asignado)
  -- Si es el primer NUM_DET (primer sub-lote), crea el registro de seguimiento propio
  SP_PLN_INIT_SEGUIMIENTO(:NEW.serie, :NEW.num_ped, :NEW.nro, :NEW.num_det);
  SP_PLN_AVANZA_PASO(
    :NEW.serie, :NEW.num_ped, :NEW.nro, :NEW.num_det,
    '02', 'ITEMPED_DET', :NEW.nroprog, :NEW.cantidad,
    'Programa asignado: '|| :NEW.nroprog
  );
  SP_PLN_CALCULA_FECHAS(:NEW.serie, :NEW.num_ped, :NEW.nro, :NEW.num_det, 'PLA');
EXCEPTION
  WHEN OTHERS THEN NULL;
END TUA_PLN_FROM_ITEMPED_DET;
```

---

### 7.3 TIA_PLN_FROM_H_RPRODUC — Al iniciar producción en hilandería (PASO '03')

> `H_RPRODUC.GUIA = PARTIDA.NUMERO` (confirmado: 99.99% de registros).  
> Navegación: `H_RPRODUC.GUIA → PARTIDA.NROPROG → ITEMPED_DET`.  
> `PARTIDA.SERIE` y `PARTIDA.NRO_PEDIDO` ya disponibles desde PARTIDA.

```sql
CREATE OR REPLACE TRIGGER TIA_PLN_FROM_H_RPRODUC
AFTER INSERT ON H_RPRODUC
FOR EACH ROW
WHEN (NEW.GUIA IS NOT NULL)
DECLARE
  v_nroprog  NUMBER;
  v_part_ser NUMBER;
  v_part_ped NUMBER;
  v_serie    NUMBER;
  v_num_ped  NUMBER;
  v_nro      NUMBER;
  v_num_det  NUMBER;
BEGIN
  -- Obtener PARTIDA desde GUIA
  SELECT p.nroprog, p.serie, p.nro_pedido
  INTO v_nroprog, v_part_ser, v_part_ped
  FROM partida p
  WHERE p.numero = :NEW.guia;

  -- Derivar (SERIE, NUM_PED, NRO, NUM_DET) desde ITEMPED_DET via NROPROG
  SELECT d.serie, d.num_ped, d.nro, d.num_det
  INTO v_serie, v_num_ped, v_nro, v_num_det
  FROM itemped_det d
  WHERE d.nroprog = v_nroprog AND ROWNUM = 1;

  -- PASO 03: En Hilandería (producción iniciada)
  SP_PLN_AVANZA_PASO(
    v_serie, v_num_ped, v_nro, v_num_det,
    '03', 'H_RPRODUC', :NEW.guia, :NEW.peso_neto,
    'Hilandería inicio - Máq:'||:NEW.cod_maq||' Tipo:'||:NEW.tp_maq
  );
EXCEPTION
  WHEN NO_DATA_FOUND THEN NULL;
  WHEN OTHERS       THEN NULL;
END TIA_PLN_FROM_H_RPRODUC;
```

---

### 7.4 TIA_PLN_FROM_PARTIDA — Al crear la PARTIDA = lote físico listo (PASO '04')

> **Trigger NUEVO** — ausente en versión anterior.  
> `PARTIDA.SERIE` y `PARTIDA.NRO_PEDIDO` disponibles directamente en `:NEW`.  
> Solo necesita `NRO` y `NUM_DET` desde `ITEMPED_DET` vía `NROPROG`.  
> Tiene su propio trigger existente en BD (`TIA_PARTIDA`) que crea `PARTIDA_FENTREGA`; este trigger PLN no interfiere.

```sql
CREATE OR REPLACE TRIGGER TIA_PLN_FROM_PARTIDA
AFTER INSERT ON PARTIDA
FOR EACH ROW
WHEN (NEW.NROPROG IS NOT NULL)
DECLARE
  v_nro     NUMBER;
  v_num_det NUMBER;
BEGIN
  -- Obtener NRO y NUM_DET del sub-lote asociado al programa
  SELECT d.nro, d.num_det INTO v_nro, v_num_det
  FROM itemped_det d
  WHERE d.nroprog = :NEW.nroprog AND ROWNUM = 1;

  -- PASO 04: Lote Disponible — hilo crudo producido y lote creado
  -- SERIE y NRO_PEDIDO vienen directamente de PARTIDA.:NEW
  SP_PLN_AVANZA_PASO(
    :NEW.serie, :NEW.nro_pedido, v_nro, v_num_det,
    '04', 'PARTIDA', :NEW.numero, :NEW.peso_neto,
    'Lote disponible - NROPROG:'||:NEW.nroprog
  );
EXCEPTION
  WHEN NO_DATA_FOUND THEN NULL;
  WHEN OTHERS        THEN NULL;
END TIA_PLN_FROM_PARTIDA;
```

---

### 7.5 TUA_PLN_FROM_L_VALIDA_RECETA — Al validar receta de laboratorio (PASO '05')

> **Trigger NUEVO** — ausente en versión anterior.  
> `L_VALIDA_RECETA.NROPROG` es el link directo a `ITEMPED_DET.NROPROG` (campo verificado en BD).  
> Dispara en **UPDATE** cuando `ESTADO` cambia a `'3'` (validado/aprobado). Distribución real: 70,872 registros con ESTADO='3'.

```sql
CREATE OR REPLACE TRIGGER TUA_PLN_FROM_L_VALIDA_RECETA
AFTER UPDATE OF ESTADO ON L_VALIDA_RECETA
FOR EACH ROW
WHEN (NEW.ESTADO = '3' AND (OLD.ESTADO IS NULL OR OLD.ESTADO <> '3'))
DECLARE
  v_serie   NUMBER;
  v_num_ped NUMBER;
  v_nro     NUMBER;
  v_num_det NUMBER;
BEGIN
  IF :NEW.nroprog IS NULL THEN RETURN; END IF;

  SELECT d.serie, d.num_ped, d.nro, d.num_det
  INTO v_serie, v_num_ped, v_nro, v_num_det
  FROM itemped_det d
  WHERE d.nroprog = :NEW.nroprog AND ROWNUM = 1;

  -- PASO 05: Laboratorio — receta validada
  SP_PLN_AVANZA_PASO(
    v_serie, v_num_ped, v_nro, v_num_det,
    '05', 'L_VALIDA_RECETA', :NEW.numero, NULL,
    'Receta validada - Lab:'||NVL(:NEW.c_laboratorista,'N/A')
  );
EXCEPTION
  WHEN NO_DATA_FOUND THEN NULL;
  WHEN OTHERS        THEN NULL;
END TUA_PLN_FROM_L_VALIDA_RECETA;
```

---

### 7.6 TUA_PLN_FROM_PARTIDA — Al cambiar situación de la partida (PASO '06')

> **Corrección:** `PARTIDA.SERIE` y `PARTIDA.NRO_PEDIDO` disponibles en `:NEW` directamente.  
> Solo se necesita `NRO` y `NUM_DET` desde `ITEMPED_DET` via `:NEW.nroprog`.  
> Solo avanza a PASO '06' (SITU_PART='R001'). PASO '07' lo maneja `TUA_PLN_FROM_TT_RPRODUC`.

```sql
CREATE OR REPLACE TRIGGER TUA_PLN_FROM_PARTIDA
AFTER UPDATE ON PARTIDA
FOR EACH ROW
WHEN (NEW.SITU_PART = 'R001' AND (OLD.SITU_PART IS NULL OR OLD.SITU_PART <> 'R001')
      AND NEW.NROPROG IS NOT NULL)
DECLARE
  v_nro     NUMBER;
  v_num_det NUMBER;
BEGIN
  -- Derivar NRO y NUM_DET del sub-lote (SERIE y NRO_PEDIDO vienen de :NEW)
  SELECT d.nro, d.num_det INTO v_nro, v_num_det
  FROM itemped_det d
  WHERE d.nroprog = :NEW.nroprog AND ROWNUM = 1;

  -- PASO 06: En Tintorería (SITU_PART cambió a 'R001' = recibida en TT)
  SP_PLN_AVANZA_PASO(
    :NEW.serie, :NEW.nro_pedido, v_nro, v_num_det,
    '06', 'PARTIDA', :NEW.numero, :NEW.peso_neto,
    'Ingresó a Tintorería - SITU_PART=R001'
  );
EXCEPTION
  WHEN NO_DATA_FOUND THEN NULL;
  WHEN OTHERS        THEN NULL;
END TUA_PLN_FROM_PARTIDA;
```

---

### 7.7 TUA_PLN_FROM_TT_RPRODUC — Al completar TODOS los baños TT (PASO '07')

> **Trigger RENOMBRADO y REESCRITO.** Versión anterior era `TIA_PLN_FROM_TT_RPRODUC` (AFTER INSERT) — incorrecto porque `ESTADO='3'` se fija via UPDATE, nunca en INSERT.  
> **Criterio clave:** el 75% de las partidas tienen 2+ baños. Solo avanzar cuando TODOS los baños de la PARTIDA tienen `ESTADO='3'`.  
> Navegación: `TT_RPRODUC.RECETA → ING_RECETAS_G.NUMERO → ING_RECETAS_G.GUIA → PARTIDA.NROPROG → ITEMPED_DET`.

```sql
CREATE OR REPLACE TRIGGER TUA_PLN_FROM_TT_RPRODUC
AFTER UPDATE OF ESTADO ON TT_RPRODUC
FOR EACH ROW
WHEN (NEW.ESTADO = '3' AND (OLD.ESTADO IS NULL OR OLD.ESTADO <> '3'))
DECLARE
  v_partida NUMBER;
  v_nroprog NUMBER;
  v_serie   NUMBER;
  v_num_ped NUMBER;
  v_nro     NUMBER;
  v_num_det NUMBER;
  v_pendientes NUMBER := 0;
BEGIN
  -- Obtener PARTIDA desde ING_RECETAS_G.GUIA (= PARTIDA.NUMERO)
  SELECT ig.guia INTO v_partida
  FROM ing_recetas_g ig
  WHERE ig.numero = :NEW.receta AND ROWNUM = 1;

  -- Verificar que TODOS los baños de la PARTIDA están terminados (ESTADO='3')
  -- Incluye el baño actual (ya actualizado a '3' en AFTER trigger)
  SELECT COUNT(*) INTO v_pendientes
  FROM ing_recetas_g ig2
  JOIN tt_rproduc r ON r.receta = ig2.numero
  WHERE ig2.guia = v_partida
    AND r.estado <> '3';

  -- Solo avanzar si no quedan baños pendientes
  IF v_pendientes > 0 THEN RETURN; END IF;

  -- Obtener NROPROG → ITEMPED_DET
  SELECT p.nroprog, p.serie, p.nro_pedido
  INTO v_nroprog, v_serie, v_num_ped
  FROM partida p
  WHERE p.numero = v_partida;

  SELECT d.nro, d.num_det INTO v_nro, v_num_det
  FROM itemped_det d
  WHERE d.nroprog = v_nroprog AND ROWNUM = 1;

  -- PASO 07: Tenido Completo (TODOS los baños terminados)
  SP_PLN_AVANZA_PASO(
    v_serie, v_num_ped, v_nro, v_num_det,
    '07', 'TT_RPRODUC', :NEW.receta, NULL,
    'Tenido completo - Último baño RECETA:'||:NEW.receta
  );
EXCEPTION
  WHEN NO_DATA_FOUND THEN NULL;
  WHEN OTHERS        THEN NULL;
END TUA_PLN_FROM_TT_RPRODUC;
```

---

### 7.8 TIA_PLN_FROM_TT_RSECADO — Al registrar secado (PASO '08')

> **Corrección:** la versión anterior usaba `p.nropart` que no existe en PARTIDA.  
> Navegación correcta: `TT_RSECADO.GUIA → PARTIDA.NROPROG → ITEMPED_DET`.

```sql
CREATE OR REPLACE TRIGGER TIA_PLN_FROM_TT_RSECADO
AFTER INSERT ON TT_RSECADO
FOR EACH ROW
DECLARE
  v_nroprog NUMBER;
  v_serie   NUMBER;
  v_num_ped NUMBER;
  v_nro     NUMBER;
  v_num_det NUMBER;
BEGIN
  -- TT_RSECADO.GUIA = PARTIDA.NUMERO (mismo patrón que H_RPRODUC.GUIA)
  SELECT p.nroprog, p.serie, p.nro_pedido
  INTO v_nroprog, v_serie, v_num_ped
  FROM partida p
  WHERE p.numero = :NEW.guia;

  SELECT d.nro, d.num_det INTO v_nro, v_num_det
  FROM itemped_det d
  WHERE d.nroprog = v_nroprog AND ROWNUM = 1;

  -- PASO 08: Secado registrado
  SP_PLN_AVANZA_PASO(
    v_serie, v_num_ped, v_nro, v_num_det,
    '08', 'TT_RSECADO', :NEW.guia, :NEW.peso_neto,
    'Secado registrado'
  );
EXCEPTION
  WHEN NO_DATA_FOUND THEN NULL;
  WHEN OTHERS        THEN NULL;
END TIA_PLN_FROM_TT_RSECADO;
```

---

### 7.9 TUA_PLN_FROM_CTCALIDAD — Al obtener resultado CC tintorería (PASO '09' / '9R')

> **Trigger RENOMBRADO y REESCRITO.** Versión anterior era `TIA_PLN_FROM_CTCALIDAD` (AFTER INSERT) — incorrecto porque `RESULTADO` y `EST_EVALUACION='32'` se fijan via UPDATE.  
> **Navegación corregida:** `CTCALIDAD_D.NRO_PEDIDO + SER_PARTIDA + NROPART → ITEMPED_DET.(NUM_PED, NRO, NUM_DET)` (confirmado por trigger existente `TUA_CTCALIDADD_RESULTADO`).  
> `SER_PARTIDA` = `ITEMPED_DET.NRO` (ítem), `NROPART` = `ITEMPED_DET.NUM_DET` (sub-lote).  
> Frecuencia real: RESULTADO='30' (rechazado) ocurre en el 2.7% de evaluados → no es despreciable.

```sql
CREATE OR REPLACE TRIGGER TUA_PLN_FROM_CTCALIDAD
AFTER UPDATE OF EST_EVALUACION, RESULTADO ON CTCALIDAD_D
FOR EACH ROW
WHEN (NEW.EST_EVALUACION = '32'
      AND (OLD.EST_EVALUACION IS NULL OR OLD.EST_EVALUACION <> '32'
           OR NVL(OLD.RESULTADO,'__') <> NVL(NEW.RESULTADO,'__')))
DECLARE
  v_serie NUMBER;
  v_paso  VARCHAR2(2);
BEGIN
  -- Determinar paso según resultado
  v_paso := CASE
    WHEN :NEW.resultado IN ('01','29','21') THEN '09'   -- Aprobado / Concesionado
    WHEN :NEW.resultado = '30'             THEN '9R'   -- Rechazado → Reproceso
    ELSE NULL
  END;

  IF v_paso IS NULL THEN RETURN; END IF;

  -- Obtener SERIE desde ITEMPED_DET
  -- SER_PARTIDA = ITEMPED_DET.NRO  (ítem del pedido)
  -- NROPART     = ITEMPED_DET.NUM_DET (sub-lote)
  SELECT d.serie INTO v_serie
  FROM itemped_det d
  WHERE d.num_ped = :NEW.nro_pedido
    AND d.nro     = :NEW.ser_partida
    AND d.num_det = :NEW.nropart
    AND ROWNUM = 1;

  SP_PLN_AVANZA_PASO(
    v_serie, :NEW.nro_pedido, :NEW.ser_partida, :NEW.nropart,
    v_paso, 'CTCALIDAD_D', :NEW.numero, NULL,
    'CC resultado='||:NEW.resultado||' REPROCESO='||NVL(:NEW.reproceso,'0')
  );
EXCEPTION
  WHEN NO_DATA_FOUND THEN NULL;
  WHEN OTHERS        THEN NULL;
END TUA_PLN_FROM_CTCALIDAD;
```

---

### 7.10 TIA_PLN_FROM_REVISADO — Al aprobar revisión de conos (PASO '11')

> **Corrección:** se pasa `v_num_det` derivado desde `ITEMPED_DET` en lugar del valor hardcodeado `1`.  
> Navegación: `REVISADO_G.GUIA → PARTIDA.NROPROG → ITEMPED_DET.(NRO, NUM_DET)`.

```sql
CREATE OR REPLACE TRIGGER TIA_PLN_FROM_REVISADO
AFTER INSERT ON REVISADO_D
FOR EACH ROW
WHEN (NEW.APROBADO > 0)
DECLARE
  v_nroprog NUMBER;
  v_serie   NUMBER;
  v_num_ped NUMBER;
  v_nro     NUMBER;
  v_num_det NUMBER;
BEGIN
  -- Navegar REVISADO_D → REVISADO_G → PARTIDA → ITEMPED_DET
  SELECT p.nroprog, p.serie, p.nro_pedido
  INTO v_nroprog, v_serie, v_num_ped
  FROM revisado_g rg
  JOIN partida p ON p.numero = rg.guia
  WHERE rg.numero = :NEW.numero AND ROWNUM = 1;

  SELECT d.nro, d.num_det INTO v_nro, v_num_det
  FROM itemped_det d
  WHERE d.nroprog = v_nroprog AND ROWNUM = 1;

  -- PASO 11: Revisado — conos aprobados
  SP_PLN_AVANZA_PASO(
    v_serie, v_num_ped, v_nro, v_num_det,
    '11', 'REVISADO_D', :NEW.numero,
    :NEW.aprobado, 'Revisado: '||:NEW.aprobado||' conos aprobados'
  );
EXCEPTION
  WHEN NO_DATA_FOUND THEN NULL;
  WHEN OTHERS        THEN NULL;
END TIA_PLN_FROM_REVISADO;
```

---

### 7.11 TIA_PLN_FROM_LOTES_PT — Al ingresar lotes a almacén PT (PASO '12')

> **Corrección:** se pasa `v_num_det` derivado desde `ITEMPED_DET` en lugar del valor hardcodeado `1`.  
> Volumen verificado en BD: 1,434,979 lotes en ALM='03' + 27,828 en ALM='07' con TP_TRANSAC='16'.

```sql
CREATE OR REPLACE TRIGGER TIA_PLN_FROM_LOTES_PT
AFTER INSERT ON LOTES
FOR EACH ROW
WHEN (NEW.TP_TRANSAC = '16' AND NEW.PARTIDA IS NOT NULL
      AND NEW.COD_ALM IN ('03','07'))
DECLARE
  v_nroprog NUMBER;
  v_serie   NUMBER;
  v_num_ped NUMBER;
  v_nro     NUMBER;
  v_num_det NUMBER;
BEGIN
  -- Navegar LOTES.PARTIDA → PARTIDA.NROPROG → ITEMPED_DET
  SELECT p.nroprog, p.serie, p.nro_pedido
  INTO v_nroprog, v_serie, v_num_ped
  FROM partida p
  WHERE p.numero = :NEW.partida;

  SELECT d.nro, d.num_det INTO v_nro, v_num_det
  FROM itemped_det d
  WHERE d.nroprog = v_nroprog AND ROWNUM = 1;

  -- PASO 12: Ingresado Almacén PT
  SP_PLN_AVANZA_PASO(
    v_serie, v_num_ped, v_nro, v_num_det,
    '12', 'LOTES', :NEW.numero,
    :NEW.saldo, 'Almacén PT '||:NEW.cod_alm||' - Lote:'||:NEW.nlote
  );
EXCEPTION
  WHEN NO_DATA_FOUND THEN NULL;
  WHEN OTHERS        THEN NULL;
END TIA_PLN_FROM_LOTES_PT;
```

---

### 7.12 TUA_PLN_FROM_LOTES_DESPACHO — Al despachar (PASO '14')

> **Trigger NUEVO** en reemplazo de `TIA_PLN_FROM_KARDEX_DESPACHO` (AFTER INSERT ON KARDEX_D).  
> Razón: `KARDEX_G.TIP_DOC_REF` está en blanco en ~90% de registros TP='22', imposibilitando el join confiable a ITEMPED_DET.  
> El evento de despacho real queda registrado en `LOTES.S_TRANSAC IN ('21','23')` (confirmado por `V_STATUS_PEDIDO`).  
> '21' = despacho nacional, '23' = despacho exportación.

```sql
CREATE OR REPLACE TRIGGER TUA_PLN_FROM_LOTES_DESPACHO
AFTER UPDATE OF S_TRANSAC ON LOTES
FOR EACH ROW
WHEN (NEW.S_TRANSAC IN ('21','23')
      AND (OLD.S_TRANSAC IS NULL OR OLD.S_TRANSAC NOT IN ('21','23'))
      AND NEW.PARTIDA IS NOT NULL)
DECLARE
  v_nroprog NUMBER;
  v_serie   NUMBER;
  v_num_ped NUMBER;
  v_nro     NUMBER;
  v_num_det NUMBER;
BEGIN
  -- Navegar LOTES.PARTIDA → PARTIDA.NROPROG → ITEMPED_DET
  SELECT p.nroprog, p.serie, p.nro_pedido
  INTO v_nroprog, v_serie, v_num_ped
  FROM partida p
  WHERE p.numero = :NEW.partida;

  SELECT d.nro, d.num_det INTO v_nro, v_num_det
  FROM itemped_det d
  WHERE d.nroprog = v_nroprog AND ROWNUM = 1;

  -- PASO 14: Despachado / Cerrado
  SP_PLN_AVANZA_PASO(
    v_serie, v_num_ped, v_nro, v_num_det,
    '14', 'LOTES', :NEW.numero,
    :NEW.saldo, 'Despacho S_TRANSAC='||:NEW.s_transac||' Lote:'||:NEW.nlote
  );
EXCEPTION
  WHEN NO_DATA_FOUND THEN NULL;
  WHEN OTHERS        THEN NULL;
END TUA_PLN_FROM_LOTES_DESPACHO;
```

---

### 7.13 NOTA — Integración con tabla SEGUIMIENTO existente

> La tabla `SEGUIMIENTO` (sin prefijo PLN_) ya registra eventos de negocio en el mismo nivel de ítem de pedido. Los nuevos triggers `PLN_` **no deben duplicar** esa funcionalidad.
>
> **Regla de división:**
> - `SEGUIMIENTO` → eventos de operación (qué se hizo, en producción)
> - `PLN_LOG_EVENTOS` → eventos de planeamiento (fechas, estados, retrasos, alertas)
>
> `PLN_LOG_EVENTOS.TIPO_EVENTO`:
> - `'AV'` = Avance de paso (gatillado por trigger)
> - `'RE'` = Reprogramación manual de fecha
> - `'AL'` = Alerta generada
> - `'CI'` = Cierre del ítem

---

## 8. JOB PROGRAMADO — ALERTAS Y CARGA

```sql
-- Job para generar alertas cada hora
BEGIN
  DBMS_SCHEDULER.CREATE_JOB (
    job_name        => 'JOB_PLN_ALERTAS',
    job_type        => 'STORED_PROCEDURE',
    job_action      => 'SP_PLN_GENERA_ALERTAS',
    start_date      => SYSTIMESTAMP,
    repeat_interval => 'FREQ=HOURLY; BYMINUTE=0',
    enabled         => TRUE,
    comments        => 'Genera alertas de retraso en el flujo de producción'
  );
END;

-- Job para recalcular carga diaria (cada noche a las 23:30)
BEGIN
  DBMS_SCHEDULER.CREATE_JOB (
    job_name        => 'JOB_PLN_CARGA',
    job_type        => 'STORED_PROCEDURE',
    job_action      => 'SP_PLN_CARGA_DIARIA_REFRESH',
    start_date      => SYSTIMESTAMP,
    repeat_interval => 'FREQ=DAILY; BYHOUR=23; BYMINUTE=30',
    enabled         => TRUE,
    comments        => 'Recalcula carga de máquinas próximos 30 días'
  );
END;
```

---

## 9. PLAN DE IMPLEMENTACIÓN POR FASES

### FASE 1 — Fundación (Semana 1-2)
Crea las tablas base y las pobla con datos históricos.

| # | Acción | Objeto | Prioridad |
|---|--------|--------|-----------|
| 1 | Crear secuencias | `PLN_SEQ_SEGUIM`, `PLN_SEQ_EVENTO`, `PLN_SEQ_ALERTA`, `PLN_SEQ_FECHAS` | 🔴 Alta |
| 2 | Crear tabla catálogo | `PLN_ESTADO_CODIGO` + INSERT datos | 🔴 Alta |
| 3 | Crear tabla parámetros | `PLN_PARAM` + INSERT datos | 🔴 Alta |
| 4 | Crear tabla maestra | `PLN_SEGUIMIENTO` + índices | 🔴 Alta |
| 5 | Crear tabla log | `PLN_LOG_EVENTOS` | 🔴 Alta |
| 6 | Poblar seguimiento histórico | Script INSERT desde `ITEMPED_DET` + `PARTIDA` | 🟡 Media |

```sql
-- Script de población inicial (pedidos activos)
INSERT INTO PLN_SEGUIMIENTO (
  ID_SEGUIM, SERIE, NUM_PED, NRO, NUM_DET, COD_CLIENTE,
  COD_ART, COLOR, TITULO, PROCESO, CANTIDAD_ORIG,
  COD_PASO_ACT, FCH_PEDIDO, FCH_ENTREGA_COMP,
  KG_DESPACHADOS, KG_PENDIENTES, ESTADO, A_ADUSER, A_ADFECHA
)
SELECT
  PLN_SEQ_SEGUIM.NEXTVAL,
  i.serie, i.num_ped, i.nro, NVL(d.num_det,1),
  p.cod_cliente, i.cod_art, i.color, i.titulo, i.proceso, i.cantidad,
  CASE
    WHEN i.estado = '6' THEN '12'
    WHEN i.estado = '9' THEN '05'
    WHEN d.nroprog IS NOT NULL THEN '02'
    ELSE '01'
  END,
  p.fecha,
  p.fecha + NVL(p.plazo_entrega, 30),
  i.cantidad - i.saldo,
  i.saldo,
  CASE WHEN i.estado='6' THEN 'C' WHEN i.estado='8' THEN 'X' ELSE 'A' END,
  USER, SYSDATE
FROM itemped i
JOIN pedido p ON p.serie=i.serie AND p.num_ped=i.num_ped
LEFT JOIN itemped_det d ON d.serie=i.serie AND d.num_ped=i.num_ped AND d.nro=i.nro AND d.num_det=1
WHERE NOT EXISTS (
  SELECT 1 FROM PLN_SEGUIMIENTO s
  WHERE s.serie=i.serie AND s.num_ped=i.num_ped AND s.nro=i.nro AND s.num_det=NVL(d.num_det,1)
);
COMMIT;
```

---

### FASE 2 — Automatización (Semana 3-4)

| # | Acción | Objeto |
|---|--------|--------|
| 1 | Crear procedures de avance | `SP_PLN_INIT_SEGUIMIENTO`, `SP_PLN_AVANZA_PASO` |
| 2 | Crear procedure de fechas | `SP_PLN_CALCULA_FECHAS` (incluye sync `ITEMPED_DET.FCH_ESTIMA_*`) |
| 3 | Instalar triggers ITEMPED / ITEMPED_DET | `TIA_PLN_FROM_ITEMPED`, `TUA_PLN_FROM_ITEMPED_DET` |
| 4 | Instalar triggers hilandería y lote | `TIA_PLN_FROM_H_RPRODUC`, `TIA_PLN_FROM_PARTIDA` |
| 5 | Instalar trigger laboratorio | `TUA_PLN_FROM_L_VALIDA_RECETA` |
| 6 | Instalar triggers tintorería | `TUA_PLN_FROM_PARTIDA`, `TUA_PLN_FROM_TT_RPRODUC`, `TIA_PLN_FROM_TT_RSECADO` |
| 7 | Instalar trigger CC tintorería | `TUA_PLN_FROM_CTCALIDAD` (aprobado='09' + rechazado='9R') |
| 8 | Instalar triggers revisado + almacén | `TIA_PLN_FROM_REVISADO`, `TIA_PLN_FROM_LOTES_PT` |
| 9 | Instalar trigger despacho | `TUA_PLN_FROM_LOTES_DESPACHO` (reemplaza antiguo KARDEX) |
| 10 | Validar que no haya conflictos con triggers existentes | Test de regresión manual |

---

### FASE 3 — Alertas y Carga (Semana 5)

| # | Acción | Objeto |
|---|--------|--------|
| 1 | Crear tablas de alertas y carga | `PLN_ALERTA`, `PLN_CARGA_DIARIA`, `PLN_FECHAS_ESTIMADAS` |
| 2 | Crear procedure de alertas | `SP_PLN_GENERA_ALERTAS` |
| 3 | Crear procedure de carga | `SP_PLN_CARGA_DIARIA_REFRESH` |
| 4 | Crear jobs programados | `JOB_PLN_ALERTAS`, `JOB_PLN_CARGA` |
| 5 | Ejecución inicial manual | Validar resultados |

---

### FASE 4 — Vistas y KPIs (Semana 6)

| # | Vista | Propósito .NET |
|---|-------|----------------|
| 1 | `V_PLN_ESTADO_PEDIDO` | Dashboard principal — grilla de pedidos |
| 2 | `V_PLN_ESTADO_ITEM` | Detalle de ítem con semáforo |
| 3 | `V_PLN_TRAZABILIDAD` | Línea de tiempo por ítem |
| 4 | `V_PLN_ALERTAS_ACTIVAS` | Panel de alertas en tiempo real |
| 5 | `V_PLN_CARGA_MAQUINAS` | Gantt de carga de máquinas |
| 6 | `V_PLN_PENDIENTES_DESP` | Lista de despacho priorizada |
| 7 | `V_PLN_KPI_CUMPLIMIENTO` | Gráfico OTIF mensual |
| 8 | `V_PLN_KPI_PRODUCCION` | KPIs de producción por máquina |

---

## 10. ARQUITECTURA .NET CORE — RECOMENDACIONES DE CAPA DE DATOS

### Consultas recomendadas para cada pantalla

| Pantalla | Query / View | Frecuencia refresco |
|----------|-------------|---------------------|
| Dashboard principal | `V_PLN_ESTADO_PEDIDO` WHERE `estado_pedido IN ('0','5','9')` | Cada 5 min |
| Detalle de pedido | `V_PLN_ESTADO_ITEM` WHERE `num_ped = :p` | Bajo demanda |
| Línea de tiempo | `V_PLN_TRAZABILIDAD` WHERE `id_seguim = :s` | Bajo demanda |
| Panel de alertas | `V_PLN_ALERTAS_ACTIVAS` WHERE `nivel IN ('C','A')` | Cada 1 min |
| Carga de máquinas | `V_PLN_CARGA_MAQUINAS` | Cada 15 min |
| Pendientes despacho | `V_PLN_PENDIENTES_DESP` | Cada 10 min |
| KPI mensual | `V_PLN_KPI_CUMPLIMIENTO` | 1 vez/día |
| Log del ítem | `PLN_LOG_EVENTOS` WHERE `id_seguim = :s` ORDER BY `fch_evento` | Bajo demanda |

### Patrón de uso desde C# / Oracle.ManagedDataAccess

```csharp
// Ejemplo: obtener estado de un pedido
using var cmd = new OracleCommand(
  "SELECT * FROM V_PLN_ESTADO_PEDIDO WHERE num_ped = :p_num_ped", conn);
cmd.Parameters.Add("p_num_ped", OracleDbType.Int32).Value = numPed;

// Ejemplo: resolver alerta
using var cmd2 = new OracleCommand(
  @"UPDATE PLN_ALERTA SET ESTADO='R', FCH_RESOLUCION=SYSDATE,
    USUARIO_RESUELVE=:usr, OBSERV_RESOL=:obs
    WHERE ID_ALERTA=:id", conn);
```

### PKs compuestas — mapeo Entity Framework

```csharp
// PLN_SEGUIMIENTO
modelBuilder.Entity<PlnSeguimiento>().HasKey(e => e.IdSeguim);
modelBuilder.Entity<PlnSeguimiento>().HasIndex(e => new { e.Serie, e.NumPed, e.Nro, e.NumDet }).IsUnique();

// ITEMPED (existente, PK compuesta)
modelBuilder.Entity<Itemped>().HasKey(e => new { e.Serie, e.NumPed, e.Nro });
```

---

## 11. KPIs DEL SISTEMA

| KPI | Descripción | Fórmula | Fuente |
|-----|-------------|---------|--------|
| **OTIF** | On Time In Full | `items_a_tiempo / total_items * 100` | `V_PLN_KPI_CUMPLIMIENTO` |
| **Lead Time** | Días promedio pedido→despacho | `AVG(fch_real_despacho - fch_pedido)` | `V_PLN_KPI_CUMPLIMIENTO` |
| **Días en Tintorería** | Tiempo promedio en TT | `AVG(fch_real_tin_fin - fch_real_tin_ini)` | `V_PLN_KPI_CUMPLIMIENTO` |
| **Utilización Máquinas** | % tiempo activo real vs. capacidad | `kg_real / kg_capacidad * 100` | `V_PLN_CARGA_MAQUINAS` |
| **Alertas activas críticas** | Conteo de alertas nivel C | `COUNT(*) WHERE nivel='C'` | `V_PLN_ALERTAS_ACTIVAS` |
| **Ítems sin programa > 2 días** | Ítems abandonados | `COUNT(*) WHERE cod_paso='01' AND SYSDATE-fch_pedido>2` | `PLN_SEGUIMIENTO` |
| **% items en retraso** | Salud del flujo | `items_con_retraso / total_items * 100` | `V_PLN_ESTADO_PEDIDO` |
| **KG/hora por máquina** | Eficiencia real | `SUM(kg_real) / SUM(horas_real)` | `V_PLN_KPI_PRODUCCION` |

---

## 12. RESUMEN DE OBJETOS A CREAR

| Tipo | Nombre | Propósito |
|------|--------|-----------|
| TABLA | `PLN_SEGUIMIENTO` | Maestro de trazabilidad — 1 fila por ítem/sub-lote de pedido |
| TABLA | `PLN_LOG_EVENTOS` | Historial inmutable de eventos del flujo |
| TABLA | `PLN_ALERTA` | Alertas activas de retraso/problema |
| TABLA | `PLN_CARGA_DIARIA` | Carga real y asignada por máquina y día |
| TABLA | `PLN_FECHAS_ESTIMADAS` | Historial de recálculos de fechas |
| TABLA | `PLN_PARAM` | Parámetros configurables del módulo |
| TABLA | `PLN_ESTADO_CODIGO` | Catálogo de **15 pasos** del flujo (01–14 + 9R) |
| SECUENCIA | `PLN_SEQ_SEGUIM` | PK de PLN_SEGUIMIENTO |
| SECUENCIA | `PLN_SEQ_EVENTO` | PK de PLN_LOG_EVENTOS |
| SECUENCIA | `PLN_SEQ_ALERTA` | PK de PLN_ALERTA |
| SECUENCIA | `PLN_SEQ_FECHAS` | PK de PLN_FECHAS_ESTIMADAS |
| PROCEDURE | `SP_PLN_INIT_SEGUIMIENTO` | Inicia el seguimiento al crear un ítem |
| PROCEDURE | `SP_PLN_AVANZA_PASO` | Avanza el paso y actualiza fechas reales + KG acumulados |
| PROCEDURE | `SP_PLN_CALCULA_FECHAS` | Recalcula fechas estimadas + sync a ITEMPED_DET |
| PROCEDURE | `SP_PLN_GENERA_ALERTAS` | Motor de alertas (ejecutar por JOB) |
| PROCEDURE | `SP_PLN_CARGA_DIARIA_REFRESH` | Recalcula carga de máquinas |
| TRIGGER | `TIA_PLN_FROM_ITEMPED` | AFTER INSERT ITEMPED → init seguimiento (paso 01) |
| TRIGGER | `TUA_PLN_FROM_ITEMPED_DET` | AFTER UPDATE ITEMPED_DET WHEN NROPROG IS NOT NULL → paso 02 |
| TRIGGER | `TIA_PLN_FROM_H_RPRODUC` | AFTER INSERT H_RPRODUC WHEN GUIA IS NOT NULL → paso 03 |
| TRIGGER | `TIA_PLN_FROM_PARTIDA` | AFTER INSERT PARTIDA WHEN NROPROG IS NOT NULL → paso 04 |
| TRIGGER | `TUA_PLN_FROM_L_VALIDA_RECETA` | AFTER UPDATE L_VALIDA_RECETA WHEN ESTADO='3' → paso 05 |
| TRIGGER | `TUA_PLN_FROM_PARTIDA` | AFTER UPDATE PARTIDA WHEN SITU_PART='R001' → paso 06 |
| TRIGGER | `TUA_PLN_FROM_TT_RPRODUC` | AFTER UPDATE TT_RPRODUC WHEN ESTADO='3' + todos baños → paso 07 |
| TRIGGER | `TIA_PLN_FROM_TT_RSECADO` | AFTER INSERT TT_RSECADO → paso 08 |
| TRIGGER | `TUA_PLN_FROM_CTCALIDAD` | AFTER UPDATE CTCALIDAD_D WHEN EST_EVAL='32' → paso 09/9R |
| TRIGGER | `TIA_PLN_FROM_REVISADO` | AFTER INSERT REVISADO_D WHEN APROBADO>0 → paso 11 |
| TRIGGER | `TIA_PLN_FROM_LOTES_PT` | AFTER INSERT LOTES WHEN TP_TRANSAC='16' COD_ALM IN ('03','07') → paso 12 |
| TRIGGER | `TUA_PLN_FROM_LOTES_DESPACHO` | AFTER UPDATE LOTES WHEN S_TRANSAC IN ('21','23') → paso 14 |
| VISTA | `V_PLN_ESTADO_PEDIDO` | Dashboard por pedido |
| VISTA | `V_PLN_ESTADO_ITEM` | Detalle por ítem con semáforo |
| VISTA | `V_PLN_TRAZABILIDAD` | Línea de tiempo completa |
| VISTA | `V_PLN_ALERTAS_ACTIVAS` | Panel de alertas |
| VISTA | `V_PLN_CARGA_MAQUINAS` | Carga de máquinas próximos 30 días |
| VISTA | `V_PLN_PENDIENTES_DESP` | Lista de despacho priorizada |
| VISTA | `V_PLN_KPI_CUMPLIMIENTO` | OTIF y lead time mensual |
| VISTA | `V_PLN_KPI_PRODUCCION` | KPIs de eficiencia por máquina |
| JOB | `JOB_PLN_ALERTAS` | Cada 1 hora — genera alertas |
| JOB | `JOB_PLN_CARGA` | Cada noche 23:30 — recalcula carga |

**Total: 7 tablas · 4 secuencias · 5 procedures · 12 triggers · 8 vistas · 2 jobs · 4 pantallas**

> **Triggers agregados** respecto a propuesta inicial: `TIA_PLN_FROM_H_RPRODUC`, `TIA_PLN_FROM_PARTIDA`, `TUA_PLN_FROM_L_VALIDA_RECETA`, `TUA_PLN_FROM_LOTES_DESPACHO` (+4).  
> **Triggers renombrados/corregidos:** `TIA→TUA_PLN_FROM_TT_RPRODUC`, `TIA→TUA_PLN_FROM_CTCALIDAD`, `TUA_PLN_FROM_PARTIDA` dividido en INSERT + UPDATE separados.  
> **Trigger eliminado:** `TIA_PLN_FROM_KARDEX_DESPACHO` (reemplazado por `TUA_PLN_FROM_LOTES_DESPACHO`).

---

## 13. PANTALLAS DE ENTRADA MANUAL — FORMULARIOS REQUERIDOS

> El sistema es mayoritariamente automático (trigger-driven). Las 4 pantallas de esta sección cubren los únicos casos donde un empleado debe **intervenir activamente** sobre el módulo PLN_.

---

### 13.1 PANTALLA: Panel de Alertas

**Rol:** Supervisor de producción, Jefe de planta  
**Frecuencia de uso:** Varias veces al día

#### Qué muestra (solo lectura)
Lee de `V_PLN_ALERTAS_ACTIVAS`:

```
┌────────────────────────────────────────────────────────────────────────────┐
│  ALERTAS ACTIVAS  [Críticas: 3]  [Altas: 7]  [Medias: 12]                │
├────────┬──────────┬──────────────────────────────┬───────┬────────────────┤
│ NIVEL  │ PEDIDO   │ DESCRIPCIÓN                  │ DÍAS  │ ACCIONES       │
├────────┼──────────┼──────────────────────────────┼───────┼────────────────┤
│ 🔴 C  │ 186432-1 │ Retraso crítico > 7 días     │  9    │ [Ver] [Atender]│
│ 🟠 A  │ 186415-2 │ Sin programa asignado         │  3    │ [Ver] [Atender]│
│ 🟡 M  │ 186398-3 │ Partida sin ingresar a TT     │  1    │ [Ver] [Ignorar]│
└────────┴──────────┴──────────────────────────────┴───────┴────────────────┘
```

#### Formulario de resolución (modal al presionar [Atender])

```
Alerta: "Retraso crítico > 7 días — Pedido 186432 ítem 1"
─────────────────────────────────────────────────────────
Acción tomada:  [Resuelto ▼]   ← opciones: Resuelto / Ignorado
Observación:    [__________________________________]  ← texto libre, obligatorio
                                    [ Cancelar ]  [ Guardar ]
```

#### SQL ejecutado al guardar

```sql
-- SP a crear: SP_PLN_RESOLVER_ALERTA
CREATE OR REPLACE PROCEDURE SP_PLN_RESOLVER_ALERTA (
  p_id_alerta  IN NUMBER,
  p_estado     IN VARCHAR2,   -- 'R'=Resuelto, 'I'=Ignorado
  p_observ     IN VARCHAR2,
  p_usuario    IN VARCHAR2
) AS
BEGIN
  UPDATE PLN_ALERTA SET
    ESTADO           = p_estado,
    FCH_RESOLUCION   = SYSDATE,
    USUARIO_RESUELVE = p_usuario,
    OBSERV_RESOL     = p_observ
  WHERE ID_ALERTA = p_id_alerta
    AND ESTADO    = 'A';     -- solo si sigue activa (evita doble clic)

  -- Registrar en el log
  INSERT INTO PLN_LOG_EVENTOS (
    ID_EVENTO, ID_SEGUIM, SERIE, NUM_PED, NRO, NUM_DET,
    COD_PASO, DESC_PASO, TABLA_ORIGEN, ID_OBJETO_ORIGEN,
    FCH_EVENTO, USUARIO, OBSERVACION, TIPO_EVENTO
  )
  SELECT PLN_SEQ_EVENTO.NEXTVAL, a.id_seguim, a.serie, a.num_ped, a.nro, a.num_det,
         s.cod_paso_act, 'Alerta resuelta: '||a.tip_alerta, 'PLN_ALERTA', a.id_alerta,
         SYSDATE, p_usuario, p_observ, 'AL'
  FROM PLN_ALERTA a
  JOIN PLN_SEGUIMIENTO s ON s.id_seguim = a.id_seguim
  WHERE a.id_alerta = p_id_alerta;

  COMMIT;
END SP_PLN_RESOLVER_ALERTA;
```

---

### 13.2 PANTALLA: Ajuste de Fecha Comprometida

**Rol:** Comercial / Jefe de ventas  
**Cuándo se usa:** El cliente renegocia la fecha de entrega, o gerencia aprueba una extensión

#### Qué muestra (solo lectura)

Carga de `V_PLN_ESTADO_ITEM` filtrando por pedido:

```
┌──────────────────────────────────────────────────────────────────────────┐
│  PEDIDO 186432  —  CLIENTE: TEXTILES ANDINOS SAC                        │
├───┬────────────┬────────────┬──────────────────┬───────────┬────────────┤
│NRO│ ARTÍCULO   │ KG PEDIDO  │ F.COMPROMISO ACT │ F.EST.DSP │ RETRASO    │
├───┼────────────┼────────────┼──────────────────┼───────────┼────────────┤
│ 1 │ 21/1 CO    │  450.00    │   20/05/2026     │ 27/05/26  │  🔴 7 días │
│ 2 │ 30/1 AZ    │  320.00    │   25/05/2026     │ 26/05/26  │  🟢 ok     │
└───┴────────────┴────────────┴──────────────────┴───────────┴────────────┘
```

#### Formulario de ajuste (modal por ítem)

```
Ítem: Pedido 186432 · Artículo 21/1 CO · 450 kg
─────────────────────────────────────────────────────────────────────────
Fecha comprometida actual:  20/05/2026
Nueva fecha comprometida:   [ 30/05/2026  📅 ]   ← date picker
Motivo del cambio:          [ Reprogramación acordada con cliente _____ ]
                                        [ Cancelar ]  [ Confirmar ]
```

#### SQL ejecutado al confirmar

```sql
-- SP a crear: SP_PLN_AJUSTA_FECHA_COMP
CREATE OR REPLACE PROCEDURE SP_PLN_AJUSTA_FECHA_COMP (
  p_id_seguim   IN NUMBER,
  p_nueva_fecha IN DATE,
  p_motivo      IN VARCHAR2,
  p_usuario     IN VARCHAR2
) AS
  v_fecha_ant DATE;
BEGIN
  SELECT fch_entrega_comp INTO v_fecha_ant
  FROM PLN_SEGUIMIENTO WHERE id_seguim = p_id_seguim;

  UPDATE PLN_SEGUIMIENTO SET
    FCH_ENTREGA_COMP = p_nueva_fecha,
    DIAS_RETRASO     = GREATEST(TRUNC(SYSDATE) - TRUNC(p_nueva_fecha), 0),
    IND_RETRASO      = CASE WHEN SYSDATE > p_nueva_fecha THEN 'S' ELSE 'N' END,
    A_MDUSER         = p_usuario,
    A_MDFECHA        = SYSDATE
  WHERE ID_SEGUIM = p_id_seguim;

  -- Cerrar alertas de retraso anteriores que ya no aplican
  UPDATE PLN_ALERTA SET
    ESTADO = 'R', FCH_RESOLUCION = SYSDATE,
    USUARIO_RESUELVE = p_usuario,
    OBSERV_RESOL     = 'Fecha renegociada: '|| TO_CHAR(p_nueva_fecha,'DD/MM/YYYY')
  WHERE ID_SEGUIM = p_id_seguim
    AND TIP_ALERTA IN ('RET1','RET2','RET3')
    AND ESTADO    = 'A'
    AND p_nueva_fecha > SYSDATE;   -- solo si la nueva fecha ya no implica retraso

  -- Registrar en log
  INSERT INTO PLN_LOG_EVENTOS (
    ID_EVENTO, ID_SEGUIM, SERIE, NUM_PED, NRO, NUM_DET,
    COD_PASO, DESC_PASO, TABLA_ORIGEN,
    FCH_EVENTO, USUARIO,
    FCH_ESTIMADA_ANT, FCH_ESTIMADA_NUE, OBSERVACION, TIPO_EVENTO
  )
  SELECT PLN_SEQ_EVENTO.NEXTVAL, id_seguim, serie, num_ped, nro, num_det,
         cod_paso_act, 'Fecha comprometida ajustada', 'PLN_SEGUIMIENTO',
         SYSDATE, p_usuario,
         v_fecha_ant, p_nueva_fecha, p_motivo, 'RE'
  FROM PLN_SEGUIMIENTO WHERE id_seguim = p_id_seguim;

  COMMIT;
END SP_PLN_AJUSTA_FECHA_COMP;
```

---

### 13.3 PANTALLA: Gestión de Urgentes

**Rol:** Gerencia de producción / Gerencia comercial  
**Cuándo se usa:** Un pedido necesita priorizarse sobre los demás (cliente VIP, contrato crítico)

#### Qué muestra

Grilla de todos los ítems activos con semáforo, cargando de `V_PLN_ESTADO_ITEM`:

```
┌──────────────────────────────────────────────────────────────────────────────┐
│  GESTIÓN DE URGENTES                              [Solo urgentes 🔘] [Todos]  │
├──────┬──────────┬─────────────┬────────────┬──────────┬───────────┬──────────┤
│ URG  │ PEDIDO   │ CLIENTE     │ ARTÍCULO   │ KG PEND  │ F.COMP    │ PASO     │
├──────┼──────────┼─────────────┼────────────┼──────────┼───────────┼──────────┤
│  🔥  │ 186432-1 │ TEX.ANDINOS │ 21/1 CO    │  450     │ 30/05/26  │ En TT    │
│  ○   │ 186415-2 │ CONFEC SA   │ 40/2 BL    │  220     │ 02/06/26  │ Planif.  │
│  ○   │ 186398-3 │ MODAS PERU  │ 30/1 AZ    │  180     │ 05/06/26  │ Hilan.   │
└──────┴──────────┴─────────────┴────────────┴──────────┴───────────┴──────────┘
                                          [🔥 Marcar urgente]  [○ Quitar urgente]
```

#### SQL ejecutado al marcar/desmarcar

```sql
-- SP a crear: SP_PLN_SET_URGENTE
CREATE OR REPLACE PROCEDURE SP_PLN_SET_URGENTE (
  p_id_seguim  IN NUMBER,
  p_urgente    IN VARCHAR2,   -- 'S' o 'N'
  p_motivo     IN VARCHAR2,
  p_usuario    IN VARCHAR2
) AS
BEGIN
  UPDATE PLN_SEGUIMIENTO SET
    IND_URGENTE = p_urgente,
    A_MDUSER    = p_usuario,
    A_MDFECHA   = SYSDATE
  WHERE ID_SEGUIM = p_id_seguim;

  INSERT INTO PLN_LOG_EVENTOS (
    ID_EVENTO, ID_SEGUIM, SERIE, NUM_PED, NRO, NUM_DET,
    COD_PASO, DESC_PASO, TABLA_ORIGEN, FCH_EVENTO, USUARIO, OBSERVACION, TIPO_EVENTO
  )
  SELECT PLN_SEQ_EVENTO.NEXTVAL, id_seguim, serie, num_ped, nro, num_det,
         cod_paso_act,
         CASE p_urgente WHEN 'S' THEN 'Marcado como URGENTE' ELSE 'Urgencia removida' END,
         'PLN_SEGUIMIENTO', SYSDATE, p_usuario, p_motivo, 'AL'
  FROM PLN_SEGUIMIENTO WHERE id_seguim = p_id_seguim;

  COMMIT;
END SP_PLN_SET_URGENTE;
```

> **Efecto en cascada:** `V_PLN_PENDIENTES_DESP` ya ordena por `IND_URGENTE DESC` — los ítems urgentes suben automáticamente al tope de la lista de despacho.

---

### 13.4 PANTALLA: Configuración de Parámetros

**Rol:** Administrador del sistema (acceso restringido)  
**Frecuencia de uso:** Esporádica (ajustes de temporada, cambios de turno, etc.)

#### Qué muestra y edita

Grilla editable leyendo directamente de `PLN_PARAM`:

```
┌────────────────────────────────────────────────────────────────────┐
│  PARÁMETROS DEL MÓDULO PLN_                                        │
├───────────────────┬───────────────────────────────┬───────┬────────┤
│ CÓDIGO            │ DESCRIPCIÓN                   │ VALOR │        │
├───────────────────┼───────────────────────────────┼───────┼────────┤
│ DIAS_ALERTA_CRIT  │ Días retraso → alerta CRÍTICA │  [7]  │ ✏ Editar│
│ DIAS_ALERTA_ALTA  │ Días retraso → alerta ALTA    │  [3]  │ ✏ Editar│
│ HRS_HILANDERIA    │ Horas operativas / día (hilan)│  [22] │ ✏ Editar│
│ HRS_TINTORERIA    │ Horas operativas / día (TT)   │  [24] │ ✏ Editar│
│ DIAS_BUFFER_QC    │ Días para control de calidad  │  [1]  │ ✏ Editar│
│ DIAS_BUFFER_DESP  │ Días para preparar despacho   │  [1]  │ ✏ Editar│
└───────────────────┴───────────────────────────────┴───────┴────────┘
```

#### SQL ejecutado al guardar cambio

```sql
UPDATE PLN_PARAM SET
  VALOR_NUM  = :nuevo_valor,
  A_MDUSER   = :usuario,
  A_MDFECHA  = SYSDATE
WHERE COD_PARAM = :cod_param;
COMMIT;
```

> **Validación en .NET antes del UPDATE:** `VALOR_NUM` debe ser `> 0` y `<= 365`. No se permite dejar en NULL.

---

### Resumen de pantallas y objetos de soporte

| Pantalla | Rol | Lee de | Escribe en | SP de soporte |
|----------|-----|--------|-----------|---------------|
| Panel de Alertas | Supervisor, Jefe planta | `V_PLN_ALERTAS_ACTIVAS` | `PLN_ALERTA`, `PLN_LOG_EVENTOS` | `SP_PLN_RESOLVER_ALERTA` |
| Ajuste Fecha Comprometida | Comercial | `V_PLN_ESTADO_ITEM` | `PLN_SEGUIMIENTO`, `PLN_ALERTA`, `PLN_LOG_EVENTOS` | `SP_PLN_AJUSTA_FECHA_COMP` |
| Gestión de Urgentes | Gerencia | `V_PLN_ESTADO_ITEM` | `PLN_SEGUIMIENTO`, `PLN_LOG_EVENTOS` | `SP_PLN_SET_URGENTE` |
| Configuración Parámetros | Administrador | `PLN_PARAM` | `PLN_PARAM` | *(UPDATE directo)* |

**Nuevos procedures en esta sección: `SP_PLN_RESOLVER_ALERTA` · `SP_PLN_AJUSTA_FECHA_COMP` · `SP_PLN_SET_URGENTE`**

---

**Total actualizado: 7 tablas · 4 secuencias · 8 procedures · 6 triggers · 8 vistas · 2 jobs · 4 pantallas**

---

> *Propuesta generada el 16/05/2026. Base: análisis completo del esquema SIG (1.016 tablas, ~400 triggers, 18 paquetes). Ver Planeamiento.md para referencia de todos los objetos existentes.*
