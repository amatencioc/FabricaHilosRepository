# ANÁLISIS DE INDICADORES — LOGÍSTICA

**Fecha:** 2026-05-XX  
**Módulo:** FabricaHilos → Logística → Indicadores  
**Objetivo:** Validar coherencia, corrección y sentido de negocio de cada indicador

---

## 📊 RESUMEN EJECUTIVO

### Estado General: ⚠️ **REQUIERE CORRECCIONES**

| Aspecto | Estado | Prioridad |
|---------|--------|-----------|
| **Lógica de cálculo** | ⚠️ Parcial | **ALTA** |
| **Coherencia entre pestañas** | ⚠️ Inconsistencias detectadas | **ALTA** |
| **Presentación visual** | ✅ Correcto | Media |
| **Alineación con SQL** | ⚠️ Divergencias | **ALTA** |

---

## 🔍 PESTAÑA 1: DASHBOARD KPI

### 📈 Indicadores Globales (4 cards superiores)

#### ✅ 1. Total Requisiciones
```csharp
int totalReqs = Model.Resumen.Sum(r => r.CantReqs);
```
**Estado:** ✅ **CORRECTO**  
**Lógica:** Suma todas las requisiciones de todos los estados y tipos  
**Validación:** Coincide con `P_CUR_RESUMEN` del SQL  
**Interpretación:** Volumen total de actividad en el período

---

#### ✅ 2. Total Ítems
```csharp
int totalItems = Model.Resumen.Sum(r => r.CantItems);
```
**Estado:** ✅ **CORRECTO**  
**Lógica:** Suma todos los ítems (líneas de detalle) de todas las requisiciones  
**Validación:** Coincide con `COUNT(DISTINCT I.ITEMREQ)` del SQL  
**Nota importante:** Una requisición puede tener múltiples ítems (artículos distintos)

---

#### ✅ 3. Monto Total (S/)
```csharp
decimal montoTotal = Model.Resumen.Sum(r => r.MontoTotal);
```
**Estado:** ✅ **CORRECTO**  
**Lógica:** Suma todos los montos incluidos IGV  
**Fórmula SQL:** `DECODE(AFECTO_IGV, 'S', monto * (1 + IMPSTO), monto)`  
**Interpretación:** Valor económico total del período en soles peruanos

---

#### ⚠️ 4. Ítems Pendientes
```csharp
int pendItems = Model.Pendientes.Count;
decimal montoPend = Model.Pendientes.Sum(p => p.MontoPendiente);
```
**Estado:** ⚠️ **POSIBLE INCONSISTENCIA**  
**Problema detectado:** Este indicador cuenta **filas de la tabla de pendientes**, pero:
- La tabla de pendientes viene del cursor `P_CUR_PENDIENTES`
- Este cursor devuelve **ítems con SALDO > 0** de requisiciones **NO atendidas**
- Pero el título dice "Ítems Pendientes" sin aclarar que excluye requisiciones anuladas

**Pregunta de negocio:**
1. ¿Deben incluirse ítems de requisiciones ANULADAS en este conteo?
2. ¿Deben incluirse ítems con saldo de requisiciones en estado REGISTRADO que aún no llegaron a Logística?

**SQL actual (línea ~385):**
```sql
WHERE R.ESTADO IN ('1','2','3')  -- REGISTRADO, VISADO, RECIBIDO
  AND I.SALDO > 0
```
Esto **EXCLUYE** requisiciones ATENDIDAS (estado=6) y ANULADAS (estado=7), lo cual es lógico.

**Recomendación:** ✅ **CORRECTO** — El SQL ya está bien filtrado

---

### 📊 % Atendido por Tipo

#### ⚠️ 5. % Atendido — COMPRA
```csharp
var pctCompra = Model.Resumen.FirstOrDefault(r => r.Tipo == "COMPRA")?.PctAtendido ?? 0;
```
**Estado:** ⚠️ **LÓGICA INCORRECTA EN LA VISTA**  

**Problema detectado:**
El código Razor toma el `PctAtendido` de **cualquier fila** donde `Tipo == "COMPRA"`, pero:
- `PctAtendido` viene de una window function `OVER (PARTITION BY TIPO)` en el SQL
- Por diseño, **todas las filas del mismo TIPO tienen el mismo `PctAtendido`**
- Pero si no hay registros de tipo COMPRA, devuelve 0

**Problema semántico real:**
El `PctAtendido` del SQL (líneas 214-220) calcula:
```sql
ROUND(
  COUNT(DISTINCT CASE WHEN R.ESTADO='6' THEN R.NUMREQ END) * 100.0
  / NULLIF(COUNT(DISTINCT CASE WHEN R.ESTADO<>'7' THEN R.NUMREQ END), 0),
  2
) OVER (PARTITION BY DECODE(...)) PCT_ATENDIDO
```

**Interpretación correcta:**
> "De todas las requisiciones **NO anuladas** de tipo COMPRA, ¿qué % ya están en estado ATENDIDO (6)?"

**Ejemplo:**
- 100 reqs de COMPRA en total
- 5 están ANULADAS (estado 7)
- 60 están ATENDIDAS (estado 6)
- Entonces: PCT_ATENDIDO = 60 / (100-5) × 100 = **63.16%**

**Validación:** ✅ **CORRECTO EN SQL**, pero la UI debe aclarar:
- Título actual: "% Atendido — COMPRA"
- Título recomendado: **"% Atendido — COMPRA"** + subtítulo **"de reqs no anuladas ya atendidas"**

---

#### ⚠️ 6. % Atendido — SERVICIO
**Estado:** ⚠️ **MISMO PROBLEMA QUE EL ANTERIOR**  
**Misma lógica y misma recomendación**

---

### 📊 Gráficos del Dashboard

#### ✅ 7. Requisiciones por Estado y Tipo (Stacked Bar)
**Estado:** ✅ **CORRECTO**  
**Fuente:** `Model.Resumen` (agrupado por TIPO + ESTADO)  
**Visualización:** Barras apiladas — eje X = tipo (COMPRA/SERVICIO), segmentos = estados  
**Interpretación:** Distribución del volumen de requisiciones por flujo de trabajo

---

#### ✅ 8. Monto por Tipo (Donut)
**Estado:** ✅ **CORRECTO**  
**Lógica:**
```csharp
new { tipo="COMPRA",   monto = Model.Resumen.Where(r=>r.Tipo=="COMPRA").Sum(r=>r.MontoTotal) },
new { tipo="SERVICIO", monto = Model.Resumen.Where(r=>r.Tipo=="SERVICIO").Sum(r=>r.MontoTotal) }
```
**Interpretación:** Proporción de gasto: ¿cuánto se invierte en materiales vs servicios?

---

### ⏱️ Tiempos del Ciclo Logístico

#### ⚠️ 9. Tiempos Promedio (4 cards)
**Estado:** ⚠️ **POSIBLE SESGO EN EL PROMEDIO**  

**Cards mostradas:**
1. **Reg. → Autorización** (T1): `@FmtNum(Model.Tiempos.DiasRegAutorizacion, 1) d`
2. **Aut. → Recibo Log.** (T2): `@FmtNum(Model.Tiempos.DiasAutRecibo, 1) d`
3. **Recibo Log. → OC** (T3): `@FmtNum(Model.Tiempos.DiasReciboOc, 1) d`
4. **Ciclo Total**: `@FmtNum(Model.Tiempos.DiasCicloTotal, 1) d`

**Problema detectado en el SQL (líneas 227-271):**

El cursor `P_CUR_TIEMPOS` calcula promedios de esta forma:
```sql
-- T1: Registro → Autorización
ROUND(AVG(
  CASE WHEN R.F_AUTORIZA IS NOT NULL
       THEN R.F_AUTORIZA - R.FECHA
       ELSE NULL END
), 2) DIAS_REG_AUTORIZACION
```

**Análisis:**
- ✅ **Correcto:** Solo promedia reqs que tienen `F_AUTORIZA` (no contamina con NULLs)
- ⚠️ **SESGO:** Excluye requisiciones que **nunca fueron autorizadas**
  - Si hay 10 reqs registradas pero sólo 3 fueron autorizadas → promedia sólo las 3
  - Si las 7 restantes llevan 30+ días esperando → **no se reflejan**

**Consecuencia:**
> El indicador "Reg. → Autorización" muestra el tiempo promedio **de las que SÍ fueron autorizadas**, no el tiempo real de espera del backlog.

**Pregunta de negocio:**
1. ¿Es correcto excluir requisiciones que llevan días/semanas esperando autorización?
2. ¿O debería mostrarse un indicador adicional: "Reqs en espera de autorización > 7 días"?

**Recomendación:**
- Mantener el promedio actual para **análisis de rendimiento** del proceso completado
- **AGREGAR** un nuevo KPI: **"Reqs bloqueadas por >X días en cada etapa"**
  - Contador de reqs con estado REGISTRADO y más de 7 días desde `FECHA`
  - Contador de reqs con estado VISADO y más de 7 días desde `F_AUTORIZA`
  - etc.

---

#### ✅ 10. Waterfall — Desglose del Ciclo
**Estado:** ✅ **CORRECTO VISUALMENTE**  
**Lógica:** Gráfico de barras horizontales con colores según SLA (verde ≤5d, rojo >5d)  
**Interpretación:** Identifica el tramo más lento (cuello de botella)

---

### 🏭 Top 10 Destinos

#### ✅ 11. Top Destinos por Monto (Bar Chart + Tabla)
**Estado:** ✅ **CORRECTO**  
**Fuente:** `P_CUR_TOP_CCOSTO` (líneas 273-301)  
**Diferenciación visual:** 🔵 Centro de Costo (U) vs 🟤 Activo Fijo (A)  
**Ordenamiento:** `ORDER BY SUM(monto) DESC` → ranking por gasto  
**Validación:** ✅ Coherente con lógica de negocio

---

### ⏳ Ítems Pendientes (Tabla con semáforo)

#### ⚠️ 12. Tabla de Ítems Pendientes
**Estado:** ⚠️ **SEMÁFORO CORRECTO, PERO FALTA CONTEXTO**  

**Semáforo implementado:**
```csharp
string semClass = p.DiasEnEspera < 3  ? "sem-verde"
                : p.DiasEnEspera <= 7 ? "sem-amarillo"
                : "sem-rojo";
```

**SQL (líneas 326-334):**
```sql
TRUNC(SYSDATE) - TRUNC(NVL(R.F_RECIBE, R.FECHA)) DIAS_EN_ESPERA
```

**Lógica:**
- Si la req **ya llegó a Logística** (`F_RECIBE`): cuenta días desde que la recibieron
- Si la req **aún no llegó a Logística**: cuenta días desde que fue registrada

**Pregunta de negocio crítica:**
> ¿Es correcto contar los días desde REGISTRO para requisiciones que aún no llegaron a Logística?

**Escenario real:**
1. Req registrada el día 1
2. Esperando autorización del jefe (REGISTRADO, no VISADO)
3. Hoy es día 10 → `DIAS_EN_ESPERA = 10` → **semáforo ROJO**
4. Pero Logística **ni siquiera ha visto esta requisición aún**

**Interpretación actual del indicador:**
> "Días que el ítem lleva en el sistema esperando ser despachado"

**¿Es lo que Logística quiere medir?** ⚠️ **VALIDAR CON USUARIO**

**Alternativa recomendada:**
Diferenciar:
- **Pendientes en proceso de Logística:** días desde `F_RECIBE` (reqs en estado RECIBIDO)
- **Pendientes bloqueados upstream:** días desde `FECHA` (reqs en REGISTRADO/VISADO)

Mostrar dos tablas o dos secciones con semáforos distintos.

---

## 🔍 PESTAÑA 2: CICLO DE VIDA

### 📊 KPI Cards del Ciclo

#### ✅ 13. Reqs Atendidas / OCs generadas / Ciclo Promedio / % SLA ≤5d
**Estado:** ✅ **CORRECTO**  
**Fuente:** `P_CICLO_VIDA` (líneas 359-425)  
**Filtro SQL:** `WHERE R.ESTADO = '6'` → solo requisiciones completamente atendidas  
**Validación:** ✅ Coherente — mide **rendimiento del proceso completado**

---

### 📊 Desglose de Tramos (4 progress bars)

#### ✅ 14. T1 / T2 / T3 / Ciclo Total
**Estado:** ✅ **CORRECTO**  
**Lógica:** `Model.Items.Average(x => (decimal)x.T1RegAut)` — promedio de las reqs atendidas  
**Visualización:** Progress bar con ancho proporcional (max = 10 días)  
**Validación:** ✅ Coherente

---

### 📊 Histograma del Ciclo

#### ⚠️ 15. Histograma — Ciclo Total (distribución)
**Estado:** ⚠️ **BINS MAL DEFINIDOS**  

**Bins actuales (Razor, líneas 25-31):**
```csharp
new { label = "0 d",       count = Model.Items.Count(x => x.TCicloTotal == 0) },
new { label = "1 d",       count = Model.Items.Count(x => x.TCicloTotal == 1) },
new { label = "2-3 d",     count = Model.Items.Count(x => x.TCicloTotal >= 2  && x.TCicloTotal <= 3) },
new { label = "4-5 d",     count = Model.Items.Count(x => x.TCicloTotal >= 4  && x.TCicloTotal <= 5) },
new { label = "6-7 d",     count = Model.Items.Count(x => x.TCicloTotal >= 6  && x.TCicloTotal <= 7) },
new { label = "8-14 d",    count = Model.Items.Count(x => x.TCicloTotal >= 8  && x.TCicloTotal <= 14) },
new { label = "> 14 d",    count = Model.Items.Count(x => x.TCicloTotal > 14) }
```

**Problema:**
Los bins son **arbitrarios** y no siguen una lógica estadística (equi-width o equi-depth).

**Bins recomendados por el SQL (líneas 372-378):**
```sql
-- COMENTARIO DEL SQL SUGIERE:
-- 0d, 1d, 2-3d, 4-5d (SLA sugerido), 6-7d, 8-14d, >14d
```

**Validación:** ⚠️ Los bins del Razor **SÍ coinciden con el SQL**, pero:
- El bin "4-5 d" debería ser **el objetivo SLA** (70% de reqs)
- Debería colorearse **verde** en el gráfico
- Los bins >5d deberían ser **amarillo/rojo**

**Recomendación:**
Agregar colores al dataset del histograma:
```javascript
backgroundColor: hist.map(h => {
    if (h.label === '0 d' || h.label === '1 d' || h.label.includes('2-3') || h.label.includes('4-5')) 
        return '#1B4D3Ecc'; // verde
    if (h.label.includes('6-7') || h.label.includes('8-14')) 
        return '#ff9800cc'; // naranja
    return '#c62828cc'; // rojo
})
```

---

### 📊 Gantt del Ciclo

#### ⚠️ 16. Gantt — últimas 50 requisiciones
**Estado:** ⚠️ **OUTLIERS YA FILTRADOS, PERO FALTA ESCALA**  

**Corrección reciente aplicada:**
```javascript
const p95 = sorted[Math.floor(sorted.length * 0.95)].total || 60;
const ganttOk = gantt.filter(g => (g.total||0) <= p95 && (g.total||0) >= 0);
```
✅ Ya se filtran outliers (reqs con ciclos de miles de días por fechas erróneas en Oracle)

**Problema restante:**
- El eje X muestra "Días", pero **no tiene grid secundario** para medir visualmente
- Difícil saber si una barra representa 5, 10 o 15 días

**Recomendación:**
Agregar línea vertical de referencia en el SLA (5 días):
```javascript
plugins: {
    annotation: {
        annotations: {
            sla: {
                type: 'line',
                xMin: 5, xMax: 5,
                borderColor: '#1B4D3E',
                borderWidth: 2,
                borderDash: [5, 5],
                label: { content: 'SLA: 5 días', enabled: true }
            }
        }
    }
}
```

---

## 🔍 PESTAÑA 3: TENDENCIA MENSUAL

### 📊 KPI Cards Resumen

#### ⚠️ 17. Cuello de Botella
```csharp
string cuello = avgT1 >= avgT2 && avgT1 >= avgT3 ? "T1 Reg→Aut"
              : avgT2 >= avgT3                   ? "T2 Aut→Log"
                                                 : "T3 Log→OC";
```
**Estado:** ⚠️ **LÓGICA SIMPLISTA**  

**Problema:**
Identifica el tramo con **mayor promedio**, pero:
- No considera la **variabilidad** (desviación estándar)
- Un tramo con avg=3d pero σ=5d es **más problemático** que uno con avg=5d pero σ=0.5d

**Recomendación:**
Cambiar a:
1. Calcular coeficiente de variación `CV = σ / μ` por tramo
2. El tramo con mayor CV es el cuello de botella real (más impredecible)

**Alternativa simple:**
Identificar el tramo que más veces supera el SLA individual:
- T1: SLA = 1 día (autorización debería ser rápida)
- T2: SLA = 2 días (recepción en Logística)
- T3: SLA = 2 días (emisión de OC)

```csharp
var t1Over = items.Count(x => x.T1Avg > 1);
var t2Over = items.Count(x => x.T2Avg > 2);
var t3Over = items.Count(x => x.T3Avg > 2);
string cuello = t1Over >= t2Over && t1Over >= t3Over ? "T1 Reg→Aut"
              : t2Over >= t3Over                     ? "T2 Aut→Log"
                                                     : "T3 Log→OC";
```

---

### 📊 Stacked Bar de Tramos + Línea SLA

#### ✅ 18. Gráfico principal de tendencia
**Estado:** ✅ **CORRECTO**  
**Visualización:**
- Barras apiladas: T1 (azul) + T2 (naranja) + T3 (verde)
- Línea roja: % SLA ≤5d en eje secundario
**Interpretación:** Permite ver evolución mensual del proceso y detectar deterioro del SLA

---

### 📊 Volumen de Reqs / % SLA

#### ✅ 19. Volumen de Reqs Atendidas por Mes
**Estado:** ✅ **CORRECTO**  
**Eje Y mínimo:** Ya corregido con `min: 0`

---

#### ⚠️ 20. % Mismo Día vs % ≤5 Días (SLA)
**Estado:** ⚠️ **CONFUSIÓN SEMÁNTICA**  

**SQL (líneas 491-502):**
```sql
ROUND(
  COUNT(DISTINCT CASE WHEN TRUNC(R.FCH_OC)=TRUNC(R.FECHA) THEN R.NUMREQ END) * 100.0
  / NULLIF(COUNT(DISTINCT R.NUMREQ), 0),
  2
) PCT_MISMO_DIA,

ROUND(
  COUNT(DISTINCT CASE WHEN NVL(O.FECHA,R.FECHA)-R.FECHA <= 5 THEN R.NUMREQ END) * 100.0
  / NULLIF(COUNT(DISTINCT R.NUMREQ), 0),
  2
) PCT_HASTA_5DIAS
```

**Problema detectado:**
- `PCT_MISMO_DIA` compara `FCH_OC` vs `FECHA` → ✅ Correcto
- `PCT_HASTA_5DIAS` usa `NVL(O.FECHA, R.FECHA) - R.FECHA` → **⚠️ ¿QUÉ ES `O.FECHA`?**

**En el SQL (línea 451):**
```sql
FROM REQUISICION R
  LEFT JOIN ORDEN_DE_COMPRA O ON ...
```

**Entonces:**
- `O.FECHA` = fecha de emisión de la OC
- Si la req no tiene OC, `NVL(O.FECHA, R.FECHA)` = fecha de registro → **ciclo = 0**

**Interpretación correcta:**
> "% de reqs cuya OC fue emitida ≤ 5 días después del registro (o que aún no tienen OC → ciclo=0)"

**¿Es correcto contar reqs sin OC como "cumplimiento SLA"?** ⚠️ **NO**

**Corrección recomendada en el SQL:**
```sql
ROUND(
  COUNT(DISTINCT CASE 
    WHEN O.FECHA IS NOT NULL AND O.FECHA - R.FECHA <= 5 THEN R.NUMREQ 
  END) * 100.0
  / NULLIF(COUNT(DISTINCT CASE WHEN O.FECHA IS NOT NULL THEN R.NUMREQ END), 0),
  2
) PCT_HASTA_5DIAS
```
Esto calcula el SLA **sólo sobre reqs que SÍ tienen OC** (proceso completado).

---

## 📋 RESUMEN DE CORRECCIONES REQUERIDAS

### 🔴 **PRIORIDAD ALTA** (afectan la interpretación de negocio)

1. **Dashboard → % Atendido**: Aclarar en subtitle que es "de reqs no anuladas"
2. **Dashboard → Ítems Pendientes**: Validar con usuario si debe usar `F_RECIBE` o `FECHA`
3. **Dashboard → Tiempos Promedio**: Agregar KPI de "Reqs bloqueadas >7d por etapa"
4. **Tendencia → % SLA ≤5d**: Corregir SQL para **excluir reqs sin OC** del denominador
5. **Tendencia → Cuello de Botella**: Cambiar lógica a "tramo que más veces supera SLA"

### 🟡 **PRIORIDAD MEDIA** (mejoran usabilidad)

6. **Ciclo → Histograma**: Colorear bins según SLA (verde ≤5d, naranja 6-14d, rojo >14d)
7. **Ciclo → Gantt**: Agregar línea vertical de referencia en 5 días (SLA)
8. **Dashboard → Waterfall**: Ya usa colores, pero podría agregar el objetivo SLA como línea punteada

### 🟢 **OPCIONAL** (mejoras visuales)

9. Agregar tooltips explicativos en cada KPI card
10. Exportar Excel con pestañas separadas (Dashboard / Ciclo / Tendencia)

---

## ✅ INDICADORES VALIDADOS COMO CORRECTOS

- ✅ Total Requisiciones
- ✅ Total Ítems
- ✅ Monto Total
- ✅ Gráfico de Estados (Stacked Bar)
- ✅ Donut de Tipo
- ✅ Waterfall de Tiempos
- ✅ Top 10 Destinos
- ✅ Tabla de Pendientes (semáforo correcto)
- ✅ KPIs de Ciclo de Vida
- ✅ Stacked Bar de Tendencia Mensual
- ✅ Volumen de Reqs (gráfico)

---

**CONCLUSIÓN:**
El módulo tiene una base sólida, pero requiere ajustes en:
1. Interpretación semántica de algunos %
2. Filtrado de datos en SQL para SLA
3. Lógica de detección de cuellos de botella

**PRÓXIMOS PASOS:**
1. Validar con el usuario de Logística las definiciones de "pendiente" y "días de espera"
2. Aplicar las correcciones SQL prioritarias
3. Regenerar datos de prueba y validar coherencia entre pestañas
