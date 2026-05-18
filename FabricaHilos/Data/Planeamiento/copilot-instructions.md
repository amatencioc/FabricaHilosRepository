# Copilot Instructions — Sistema SIG · Módulo PLN_ Planeamiento de Planta
> Empresa manufacturera de **hilandería y tintorería de hilados**. Base de datos Oracle 11.2.0.4 (esquema SIG).
> Fecha de referencia: 18/05/2026

---

## 1. STACK TECNOLÓGICO

| Capa | Tecnología |
|---|---|
| Frontend | ASP.NET Core MVC (.NET 8) — Controllers + Views |
| Backend | C# · `Oracle.ManagedDataAccess.Core` (ODP.NET) · Dapper |
| Base de datos Oracle | Oracle 11.2.0.4 — multi-empresa: esquemas SIG / ARBONA / SOLSA |
| Base de datos local | SQLite — solo para ASP.NET Core Identity (usuarios/roles de la app) |
| Gráficos | ApexCharts.js (Gantt, Timeline, Swimlane, Heatmap) |
| Estilos | Bootstrap 5 + Bootstrap Icons |
| Autenticación | ASP.NET Core Identity + Session Oracle (doble verificación) |
| Logging | Serilog rolling files (`Logs/log-YYYYMMDD.txt`, retención 30 días) |

### Cadena de conexión Oracle — multi-empresa

```json
// appsettings.json
"ConnectionStrings": {
  "LaColonialConnection": "Data Source=10.0.7.11:1521/ORCL;User Id=SIG;Password=STARK;Pooling=true;",
  "ArbonaConnection":     "Data Source=10.0.7.11:1521/ORCL;User Id=ARBONA;Password=...;Pooling=true;",
  "SolsaConnection":      "Data Source=10.0.7.11:1521/ORCL;User Id=SOLSA;Password=...;Pooling=true;",
  "DefaultConnection":    "Data Source=FabricaHilos.db"
}
```

> La empresa activa se guarda en `HttpContext.Session["EmpresaConexion"]` al login.  
> Todos los servicios Oracle leen esa clave para seleccionar la cadena de conexión correcta.

> **CRÍTICO — Charset**: La BD usa `WE8ISO8859P15`. Configurar `OracleGlobalization.NCharConversionException = false` y `NLS_DATE_FORMAT = DD/MM/YYYY` en el proveedor.

### Prefijo de esquema Oracle (propiedad `S`)

```csharp
// OracleServiceBase.S — prefijo usado en TODOS los queries:
// "SIG." para LaColonial | "ARBONA." para Arbona | "SOLSA." para Solsa
// Uso: $"SELECT * FROM {S}PLN_SEGUIMIENTO WHERE ..."
protected string S => GetEmpresaConnKey() switch
{
    "ArbonaConnection" => "ARBONA.",
    "SolsaConnection"  => "SOLSA.",
    _                  => "SIG."
};
```

---

## 2. ARQUITECTURA DEL PROYECTO

```
FabricaHilos/
├── Controllers/
│   ├── OracleBaseController.cs      ← base abstracta: verifica sesión Oracle en OnActionExecuting
│   ├── Account/                     ← login, logout, acceso denegado
│   └── Produccion/
│       └── PlaneamientoController.cs ← nuevo: Dashboard, Pedido, CargaMaquinas, Alertas, KPIs
├── Views/
│   ├── Produccion/
│   │   └── Planeamiento/
│   │       ├── Dashboard.cshtml
│   │       ├── Pedido.cshtml
│   │       ├── CargaMaquinas.cshtml
│   │       ├── Alertas.cshtml
│   │       └── KPIs.cshtml
│   └── Shared/
│       └── _Layout.cshtml
├── Services/
│   ├── OracleServiceBase.cs         ← base abstracta: GetOracleConnectionString(), S (prefijo esquema)
│   └── Produccion/
│       ├── IPlnSeguimientoService.cs + PlnSeguimientoService.cs
│       ├── IPlnAlertaService.cs     + PlnAlertaService.cs
│       └── IPlnKpiService.cs        + PlnKpiService.cs
├── Models/
│   └── Produccion/
│       ├── PlnSeguimiento.cs
│       ├── PlnAlerta.cs
│       ├── PlnEstadoCodigo.cs
│       ├── PlnCargaDiaria.cs
│       └── PlnKpi.cs
├── Data/
│   └── ApplicationDbContext.cs      ← EF Core solo para Identity (SQLite)
└── wwwroot/
    └── js/charts/                   ← configuraciones ApexCharts
```

> **Patrón**: MVC Controller + Views. Todos los controllers de Oracle heredan de `OracleBaseController`.  
> **ORM Oracle**: Dapper con SQL explícito. **No usar EF Core para Oracle** (PKs compuestas, Oracle legacy).  
> **EF Core**: Solo para `ApplicationDbContext` (Identity en SQLite).

### OracleBaseController — patrón obligatorio

```csharp
// Todos los controllers que accedan a Oracle DEBEN heredar de OracleBaseController.
// Verifica automáticamente que la sesión Oracle esté activa en cada request.
// Si Session["OracleUser"] está vacío → redirect a Login.
[Authorize]
public class PlaneamientoController : OracleBaseController
{
    private readonly IPlnSeguimientoService _seguimiento;
    private readonly IPlnAlertaService      _alerta;

    public PlaneamientoController(
        IPlnSeguimientoService seguimiento,
        IPlnAlertaService      alerta)
    {
        _seguimiento = seguimiento;
        _alerta      = alerta;
    }

    public async Task<IActionResult> Dashboard() { ... }
    public async Task<IActionResult> Pedido(long numPed, int serie) { ... }
    public async Task<IActionResult> CargaMaquinas() { ... }
    public async Task<IActionResult> Alertas() { ... }
    public async Task<IActionResult> KPIs() { ... }
}
```

### OracleServiceBase — patrón obligatorio para servicios

```csharp
// Todos los servicios que ejecuten queries Oracle DEBEN heredar de OracleServiceBase.
public class PlnSeguimientoService : OracleServiceBase, IPlnSeguimientoService
{
    public PlnSeguimientoService(
        IConfiguration       configuration,
        IHttpContextAccessor httpContextAccessor)
        : base(configuration, httpContextAccessor) { }

    public async Task<IEnumerable<PlnSeguimiento>> GetActivosAsync()
    {
        await using var conn = new OracleConnection(GetOracleConnectionString());
        return await conn.QueryAsync<PlnSeguimiento>(
            $"SELECT * FROM {S}PLN_SEGUIMIENTO WHERE ESTADO = 'A' ORDER BY IND_URGENTE DESC");
    }
}
```

### Registro de servicios en Program.cs

```csharp
// Patrón existente — agregar los nuevos servicios PLN_ igual que los demás:
builder.Services.AddScoped<IPlnSeguimientoService, PlnSeguimientoService>();
builder.Services.AddScoped<IPlnAlertaService,      PlnAlertaService>();
builder.Services.AddScoped<IPlnKpiService,          PlnKpiService>();
```

---

## 3. BASE DE DATOS — TABLAS PLN_ NUEVAS

### 3.1 PLN_PARAM — Parámetros configurables

| COD_PARAM | VALOR_NUM | Descripción |
|---|---|---|
| `HRS_HILANDERIA` | 22 | Horas/día operativas hilandería |
| `HRS_TINTORERIA` | 24 | Horas/día operativas tintorería |
| `HRS_SECADO` | 8 | Horas buffer post-secado |
| `DIAS_BUFFER_LAB` | 1 | Días para laboratorio (antes de TT) |
| `DIAS_BUFFER_QC` | 1 | Días para control de calidad |
| `DIAS_BUFFER_DESP` | 1 | Días para preparar despacho |
| `DIAS_ALERTA_CRIT` | 7 | Días de retraso para alerta CRÍTICA |
| `DIAS_ALERTA_ALTA` | 3 | Días de retraso para alerta ALTA |
| `DIAS_ALERTA_MEDIA` | 1 | Días de retraso para alerta MEDIA |

### 3.2 PLN_ESTADO_CODIGO — Catálogo de pasos (máquina de estados)

| COD_PASO | NOMBRE_PASO | ORDEN | ÁREA | COLOR_UI |
|---|---|---|---|---|
| `'01'` | Pedido Registrado | 1 | Ventas | `#6c757d` |
| `'02'` | Planificado | 2 | Planeamiento | `#0d6efd` |
| `'03'` | En Hilandería | 3 | Hilandería | `#0dcaf0` |
| `'04'` | Lote Disponible | 4 | Hilandería | `#17a2b8` |
| `'05'` | Laboratorio | 5 | Laboratorio | `#6610f2` |
| `'06'` | En Tintorería | 6 | Tintorería | `#6f42c1` |
| `'07'` | Tenido Completo | 7 | Tintorería | `#d63384` |
| `'08'` | Secado | 8 | Tintorería | `#20c997` |
| `'09'` | CC TT Aprobado | 9 | Calidad | `#fd7e14` |
| `'09B'` | Gaseado *(solo PROCESO='24')* | 10 | Acabados | `#ffd700` |
| `'9R'` | Reproceso (CC rechazado) | 11 | Tintorería | `#dc3545` |
| `'10'` | Devanado | 12 | Devanado | `#ffc107` |
| `'11'` | Revisado | 13 | Calidad | `#0d6efd` |
| `'12'` | Ingresado Almacén PT | 14 | Almacén PT | `#198754` |
| `'13'` | Listo para Despacho | 15 | Almacén PT | `#20c997` |
| `'14'` | Despachado/Cerrado | 16 | Despacho | `#198754` |

### 3.3 PLN_SEGUIMIENTO — Tabla maestra (1 fila por ítem+sublote)

**PK**: `(SERIE, NUM_PED, NRO, NUM_DET)` — corresponde a `ITEMPED_DET`.

```sql
-- Campos clave para la app:
ID_SEGUIM          NUMBER(12)     -- PK surrogate
SERIE              NUMBER(3)      -- FK → ITEMPED
NUM_PED            NUMBER(8)      -- FK → ITEMPED
NRO                NUMBER(2)      -- FK → ITEMPED
NUM_DET            NUMBER(3)      -- sub-lote (0 = único)

-- Datos desnormalizados
COD_CLIENTE        VARCHAR2(15)
COD_ART            VARCHAR2(25)
COLOR              VARCHAR2(7)
TITULO             VARCHAR2(10)
PROCESO            VARCHAR2(4)    -- '01'=Cardado, '20'=Peinado, '24'=P.Gaseado
CANTIDAD_ORIG      NUMBER(12,4)   -- kg pedidos total
SOLO_DESPACHO      VARCHAR2(1)    -- 'S' si no hay producción (bypass al PASO 13)

-- Estado actual
COD_PASO_ACT       VARCHAR2(2)    -- valor de PLN_ESTADO_CODIGO.COD_PASO
COD_PASO_ANT       VARCHAR2(2)
NRO_CICLO          NUMBER(3)      -- 1=primer ciclo, 2=primer reproceso, etc.

-- Fechas comprometidas
FCH_PEDIDO         DATE
FCH_ENTREGA_COMP   DATE           -- de ITEMPED.F_MAXPED (o PEDIDO.FECHA + plazo)

-- Fechas estimadas (calculadas por SP_PLN_CALCULA_FECHAS)
FCH_EST_HILANDERIA DATE
FCH_EST_PARTIDA    DATE
FCH_EST_TIN_INI    DATE
FCH_EST_TIN_FIN    DATE
FCH_EST_SECADO     DATE
FCH_EST_CALIDAD    DATE
FCH_EST_DESPACHO   DATE           -- ← comparar con FCH_ENTREGA_COMP para semáforo

-- Fechas reales (actualizadas automáticamente por triggers)
FCH_REAL_PROGRAMADO DATE          -- PASO 02
FCH_REAL_PRODUCCION DATE          -- PASO 03
FCH_REAL_PARTIDA    DATE          -- PASO 04
FCH_REAL_TIN_INI    DATE          -- PASO 06
FCH_REAL_TIN_FIN    DATE          -- PASO 07
FCH_REAL_SECADO     DATE          -- PASO 08
FCH_REAL_CC_TINTO   DATE          -- PASO 09
FCH_REAL_CC_RECHAZO DATE          -- PASO 9R
FCH_REAL_DEVANADO   DATE          -- PASO 10
FCH_REAL_CALIDAD    DATE          -- PASO 11
FCH_REAL_ALM_PT     DATE          -- PASO 12
FCH_REAL_DESPACHO   DATE          -- PASO 14

-- KGs acumulados
KG_PRODUCIDOS      NUMBER(12,4)   -- actualizados en PASO 04
KG_EN_TIN          NUMBER(12,4)   -- actualizados en PASO 06
KG_EN_ALM_PT       NUMBER(12,4)   -- actualizados en PASO 12
KG_DESPACHADOS     NUMBER(12,4)   -- actualizados en PASO 14 únicamente
KG_PENDIENTES      NUMBER(12,4)   -- = CANTIDAD_ORIG - KG_DESPACHADOS

-- Indicadores
IND_RETRASO        VARCHAR2(1)    -- 'S'/'N'
DIAS_RETRASO       NUMBER(5)
IND_URGENTE        VARCHAR2(1)    -- 'S'/'N'
IND_REPROCESO      VARCHAR2(1)    -- 'S'/'N'

-- Referencias a objetos del flujo
NUM_PROGRAMA       NUMBER(8)      -- H_PROGRAMACION.NUMERO
NUM_PARTIDA        NUMBER(8)      -- PARTIDA.NUMERO
NUM_RECETA_TIN     NUMBER(8)      -- ING_RECETAS_G.NUMERO
NUM_KARDEX_DESP    NUMBER(8)      -- KARDEX_G.NUMERO del despacho

ESTADO             VARCHAR2(1)    -- 'A'=Activo, 'C'=Cerrado, 'X'=Anulado
```

### 3.4 PLN_ALERTA — Alertas activas

| Campo clave | Descripción |
|---|---|
| `TIP_ALERTA` | `'RETR'`=Retraso, `'SMP'`=Sin programa 2d+, `'SCM'`=Sin CC 3d+, `'REPR'`=Reproceso, `'SOBR'`=Sobrecarga máquina, `'INCP'`=Incumplimiento TT |
| `NIVEL` | `'C'`=Crítico, `'A'`=Alto, `'M'`=Medio, `'B'`=Bajo |
| `ESTADO` | `'A'`=Activa, `'R'`=Resuelta, `'I'`=Ignorada |
| `FCH_LIMITE` | Fecha límite antes de escalar |
| `DIAS_RETRASO` | Días actuales de retraso |

### 3.5 PLN_CARGA_DIARIA — Capacidad de máquinas

```sql
-- Llave: (FECHA, COD_MAQ)
HORAS_CAPACIDAD    NUMBER(5,2)    -- 24 - horas mantenimiento MA_PROGRAMA
KG_CAPACIDAD       NUMBER(12,4)
HORAS_ASIGNADAS    NUMBER(5,2)
KG_ASIGNADOS       NUMBER(12,4)
PCT_UTILIZACION    NUMBER(5,2)    -- KG_ASIGNADOS / KG_CAPACIDAD * 100
IND_SOBRECARGADA   VARCHAR2(1)    -- 'S' si PCT > 90
```

---

## 4. VISTAS PRINCIPALES PARA CONSULTA (LEER DESDE LA APP)

### V_PLN_ESTADO_PEDIDO
Estado por pedido (agrupa todos los NUM_DET de todos los ítems).

```sql
SELECT num_ped, serie, cod_cliente,
       COUNT(*)                                          AS total_items,
       SUM(CASE WHEN estado='C' THEN 1 ELSE 0 END)      AS items_cerrados,
       MIN(ec.orden_paso)                                AS paso_min_activo,
       MAX(dias_retraso)                                 AS max_retraso,
       SUM(CASE WHEN ind_retraso='S' THEN 1 ELSE 0 END) AS items_con_retraso,
       SUM(kg_pendientes)                                AS kg_pendientes_total
FROM   pln_seguimiento s
JOIN   pln_estado_codigo ec ON ec.cod_paso = s.cod_paso_act
WHERE  s.estado = 'A'
GROUP  BY num_ped, serie, cod_cliente
```

### V_PLN_PENDIENTES_DESP
Ítems listos para despachar (en almacén PT con saldo pendiente).

```sql
WHERE cod_paso_act IN ('12','13') AND estado = 'A' AND kg_pendientes > 0
```

### V_PLN_KPI_CUMPLIMIENTO
OTIF — On Time In Full.

```sql
WHERE estado = 'C'              -- solo ítems cerrados
  AND fch_real_despacho IS NOT NULL
  AND cod_paso_act = '14'
-- OTD = fch_real_despacho <= fch_entrega_comp
-- OTIF requiere también KG_DESPACHADOS >= CANTIDAD_ORIG * 0.99
```

---

## 5. PROCEDIMIENTOS PL/SQL (llamar con Dapper)

### PKG_PLN.SP_PLN_AVANZA_PASO
Nunca llamar directamente desde la app — es invocado por los triggers de la BD.  
Solo llamar manualmente para correcciones con usuario autorizado.

```csharp
// Ejemplo Dapper para llamada manual (corrección supervisada):
await db.ExecuteAsync(
    "BEGIN PKG_PLN.SP_PLN_AVANZA_PASO(:serie,:ped,:nro,:det,:paso,:tabla,:id,:kg,:obs); END;",
    new { serie, ped, nro, det, paso, tabla = "MANUAL", id = (int?)null, kg, obs });
```

### PKG_PLN.SP_PLN_REPROGRAMAR
Para reprogramación manual de fecha de despacho.

```csharp
await db.ExecuteAsync(
    "BEGIN PKG_PLN.SP_PLN_REPROGRAMAR(:serie,:ped,:nro,:det,:fch,:motivo,:usuario); END;",
    new { serie, ped, nro, det, fch = nuevaFecha, motivo, usuario = User.Identity.Name });
```

---

## 6. MODELOS C# PRINCIPALES

```csharp
public class PlnSeguimiento
{
    public long   IdSeguim         { get; set; }
    public int    Serie            { get; set; }
    public long   NumPed           { get; set; }
    public int    Nro              { get; set; }
    public int    NumDet           { get; set; }

    public string? CodCliente      { get; set; }
    public string? CodArt          { get; set; }
    public string? Color           { get; set; }
    public string? Titulo          { get; set; }
    public string? Proceso         { get; set; }
    public decimal CantidadOrig    { get; set; }

    public string  CodPasoAct      { get; set; } = "01";
    public int     NroCiclo        { get; set; } = 1;
    public DateTime FchPedido      { get; set; }
    public DateTime? FchEntregaComp { get; set; }

    // Fechas estimadas
    public DateTime? FchEstHilanderia { get; set; }
    public DateTime? FchEstPartida    { get; set; }
    public DateTime? FchEstTinIni     { get; set; }
    public DateTime? FchEstTinFin     { get; set; }
    public DateTime? FchEstSecado     { get; set; }
    public DateTime? FchEstCalidad    { get; set; }
    public DateTime? FchEstDespacho   { get; set; }

    // Fechas reales
    public DateTime? FchRealProgramado { get; set; }
    public DateTime? FchRealProduccion { get; set; }
    public DateTime? FchRealPartida    { get; set; }
    public DateTime? FchRealTinIni     { get; set; }
    public DateTime? FchRealTinFin     { get; set; }
    public DateTime? FchRealSecado     { get; set; }
    public DateTime? FchRealCcTinto    { get; set; }
    public DateTime? FchRealCcRechazo  { get; set; }
    public DateTime? FchRealDevanado   { get; set; }
    public DateTime? FchRealCalidad    { get; set; }
    public DateTime? FchRealAlmPt      { get; set; }
    public DateTime? FchRealDespacho   { get; set; }

    // KGs
    public decimal KgProducidos  { get; set; }
    public decimal KgEnTin       { get; set; }
    public decimal KgEnAlmPt     { get; set; }
    public decimal KgDespachados { get; set; }
    public decimal KgPendientes  { get; set; }

    // Indicadores
    public string IndRetraso   { get; set; } = "N";
    public int    DiasRetraso  { get; set; }
    public string IndUrgente   { get; set; } = "N";
    public string IndReproceso { get; set; } = "N";
    public string Estado       { get; set; } = "A";

    // Helpers
    public bool EstaRetrasado  => IndRetraso == "S";
    public bool EsUrgente      => IndUrgente == "S";
    public bool EstaEnReproceso => IndReproceso == "S";
    public bool EstaCerrado    => Estado == "C";

    /// Porcentaje de avance en el flujo (0-100)
    public int PctAvance => CodPasoAct switch
    {
        "01" =>  6, "02" => 13, "03" => 19, "04" => 25,
        "05" => 31, "06" => 38, "07" => 44, "08" => 50,
        "09" => 56, "09B"=> 62, "10" => 69, "11" => 75,
        "12" => 81, "13" => 88, "14" => 100, _ => 0
    };
}

public class PlnEstadoCodigo
{
    public string CodPaso     { get; set; } = "";
    public string NombrePaso  { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public int    OrdenPaso   { get; set; }
    public string ColorUi     { get; set; } = "#6c757d";
    public string EsFinal     { get; set; } = "N";
}

public class PlnAlerta
{
    public long    IdAlerta    { get; set; }
    public string  TipAlerta   { get; set; } = "";
    public string  Nivel       { get; set; } = "B";  // C/A/M/B
    public string  Titulo      { get; set; } = "";
    public string  Detalle     { get; set; } = "";
    public DateTime FchAlerta  { get; set; }
    public DateTime? FchLimite { get; set; }
    public int?    DiasRetraso { get; set; }
    public string  Estado      { get; set; } = "A";

    public string NivelColor => Nivel switch
    {
        "C" => "danger", "A" => "warning",
        "M" => "info",   "B" => "secondary", _ => "secondary"
    };
}
```

---

## 7. VISUALIZACIONES REQUERIDAS

### 7.1 Timeline Horizontal por Pedido (ApexCharts Timeline)
Muestra fechas estimadas vs. reales de cada etapa para un pedido específico.

```javascript
// Configuración ApexCharts Timeline — Página: Planeamiento/Pedido
options = {
  chart:  { type: 'rangeBar', height: 450 },
  plotOptions: { bar: { horizontal: true, barHeight: '60%', rangeBarGroupRows: true } },
  series: [
    {
      name: 'Estimado',
      data: pasos.map(p => ({
        x: p.nombrePaso,
        y: [p.fchEstIni.getTime(), p.fchEstFin.getTime()],
        fillColor: p.colorUi + '88'   // semitransparente
      }))
    },
    {
      name: 'Real',
      data: pasos
        .filter(p => p.fchReal != null)
        .map(p => ({
          x: p.nombrePaso,
          y: [p.fchRealIni.getTime(), p.fchRealFin.getTime()],
          fillColor: p.colorUi
        }))
    }
  ],
  xaxis:  { type: 'datetime' },
  tooltip: { x: { format: 'dd/MM/yyyy HH:mm' } }
}
```

### 7.2 Swimlane — Diagrama de responsabilidad por área (ApexCharts RangeBar agrupado)
Muestra todos los pedidos activos divididos por área (Ventas / Hilandería / Tintorería / Calidad / Almacén).

```javascript
// Agrupar PLN_SEGUIMIENTO.COD_PASO_ACT → área responsable
const areaMap = {
  '01':'VENTAS', '02':'PLANEAMIENTO',
  '03':'HILANDERÍA', '04':'HILANDERÍA',
  '05':'LABORATORIO',
  '06':'TINTORERÍA', '07':'TINTORERÍA', '08':'TINTORERÍA', '09':'TINTORERÍA', '9R':'TINTORERÍA',
  '09B':'ACABADOS',
  '10':'DEVANADO',
  '11':'CALIDAD', '12':'ALMACÉN PT', '13':'ALMACÉN PT', '14':'DESPACHO'
};
// Cada "swim lane" = un área; cada barra = un pedido activo en esa área
```

### 7.3 Gantt de Carga de Máquinas (ApexCharts Heatmap o RangeBar)
Fuente de datos: `PLN_CARGA_DIARIA`. Eje Y = máquinas, Eje X = fechas (próximos 30 días).

```javascript
// Heatmap: intensidad = PCT_UTILIZACION
// Rojo si IND_SOBRECARGADA = 'S' (>90%)
// Verde si PCT < 60%, Amarillo 60-90%, Rojo >90%
```

### 7.4 Flowchart de estado en tiempo real (Dashboard principal)
No usa librería de gráficos — se construye con HTML+CSS usando `PLN_ESTADO_CODIGO.COLOR_UI`.

```html
<!-- Tarjeta por pedido: muestra el paso actual como badge con el color del estado -->
<div class="badge" style="background-color: @paso.ColorUi">@paso.NombrePaso</div>
<!-- Progress bar con PctAvance -->
<div class="progress-bar" style="width: @seg.PctAvance%; background: @paso.ColorUi">
```

---

## 8. VISTAS MVC — PLANEAMIENTO

Ruta de las vistas: `Views/Produccion/Planeamiento/`.  
Controller: `Controllers/Produccion/PlaneamientoController.cs` : `OracleBaseController`.

### Dashboard.cshtml — `GET /Produccion/Planeamiento/Dashboard`
- **Propósito**: Vista general de todos los pedidos activos agrupados por `COD_PASO_ACT`.
- **ViewModel**: `IEnumerable<PlnSeguimiento>` + conteo por área.
- **Filtros**: Por cliente, por área (paso), por fecha de entrega, por alerta activa.
- **Visualización**: Swimlane + contadores por paso + semáforo de alertas.

### Pedido.cshtml — `GET /Produccion/Planeamiento/Pedido?numPed=&serie=`
- **Propósito**: Trazabilidad completa de un pedido.
- **ViewModel**: modelo compuesto con:
  - `IEnumerable<PlnSeguimiento>` (todos los NUM_DET del pedido)
  - `IEnumerable<PlnLogEvento>` (historial ordenado por `FCH_EVENTO`)
  - `IEnumerable<PlnAlerta>` (alertas del pedido)
- **Visualización**: Timeline Horizontal (estimado vs. real) + historial de eventos inmutable.

### CargaMaquinas.cshtml — `GET /Produccion/Planeamiento/CargaMaquinas`
- **Propósito**: Capacidad vs. carga por máquina en los próximos 30 días.
- **ViewModel**: `IEnumerable<PlnCargaDiaria>` filtrado por `FECHA BETWEEN TRUNC(SYSDATE) AND TRUNC(SYSDATE)+30`.
- **Visualización**: Gantt/Heatmap. Máquinas: R01-R19 (Thies), M01-M08 (Hank), PAB/HI/etc. (Hilandería).

### Alertas.cshtml — `GET /Produccion/Planeamiento/Alertas`
- **Propósito**: Bandeja de alertas activas para supervisores.
- **ViewModel**: `IEnumerable<PlnAlerta>` donde `ESTADO='A'`.
- **Acciones POST**: Resolver alerta (`/ResolverAlerta`), ignorar (`/IgnorarAlerta`).

### KPIs.cshtml — `GET /Produccion/Planeamiento/KPIs`
- **Propósito**: Indicadores de gestión mensual.
- **KPI 1 — OTIF**: % pedidos despachados a tiempo y con cantidad completa.
- **KPI 2 — Ciclo promedio**: Días promedio pedido→despacho por proceso/fibra.
- **KPI 3 — Tasa de reproceso**: % lotes que pasaron por `NRO_CICLO > 1`.
- **KPI 4 — Retrasos activos**: Distribución por área responsable.

---

## 9. REGLAS DE NEGOCIO CRÍTICAS

1. **Trazabilidad siempre por NROPROG**: La relación `ITEMPED_DET.NROPROG = PARTIDA.NROPROG` es 1:1 y es la única clave de trazabilidad confiable. El campo `LOTE` es reutilizable entre pedidos y no es único.

2. **Despacho parcial**: Un ítem con `KG_PENDIENTES > 0` después de un despacho NO se cierra. `COD_PASO_ACT` regresa a `'13'` y el ítem permanece activo hasta despachar el total. Solo cierra cuando `KG_DESPACHADOS >= CANTIDAD_ORIG`.

3. **PASO 07 — baños múltiples**: El 75% de las partidas tienen 2+ baños de tintorería (`PARTIDA_MAS`). El paso avanza a `'07'` solo cuando **todos** los registros `TT_RPRODUC` vinculados a esa partida tienen `ESTADO='3'`.

4. **PASO '09B' — Gaseado condicional**: Solo aplica cuando `PLN_SEGUIMIENTO.PROCESO = '24'` (PEINADO GASEADO). Para todos los demás procesos, el flujo salta directo de `'09'` a `'10'`.

5. **Reproceso**: Al llegar a `'9R'`, `NRO_CICLO` se incrementa y las fechas reales de TT se limpian. El seguimiento muestra el ciclo actual. El historial en `PLN_LOG_EVENTOS` conserva todos los ciclos.

6. **SOLO_DESPACHO = 'S'**: Ítems que son despachos directos desde stock (maquila, re-venta). Inician directo en PASO `'13'` sin pasar por producción. La app debe marcarlos visualmente diferente (badge "Stock").

7. **Stock nunca manual**: `ALMACEN.STOCK` es mantenido exclusivamente por triggers Oracle. La app lee el valor; nunca lo calcula ni lo actualiza.

8. **FCH_ENTREGA_COMP**: Se toma de `ITEMPED.F_MAXPED` si existe; si no, de `PEDIDO.FECHA + PEDIDO.PLAZO_ENTREGA`. Es el único campo que determina si un ítem está retrasado (`SYSDATE > FCH_ENTREGA_COMP`).

9. **Estado del ítem con múltiples sublotes**: Para mostrar el estado de un ítem de pedido en el dashboard, usar el **peor paso activo** (mínimo `ORDEN_PASO`) entre todos sus `NUM_DET`.

10. **Semáforo de urgencia**: Si `ITEMPED` tiene anticipo cobrado en `ANTICIPO` o si `ITEMPED_DET.URGENTE='S'`, `IND_URGENTE='S'` y el ítem aparece primero en todas las listas.

---

## 10. CONSULTAS SQL DAPPER DE REFERENCIA

### Pedidos activos para el Dashboard

```sql
SELECT s.id_seguim, s.num_ped, s.nro, s.num_det,
       s.cod_cliente, c.descripcion AS nombre_cliente,
       s.cod_art, s.color, s.titulo, s.proceso,
       s.cod_paso_act, ec.nombre_paso, ec.color_ui,
       s.fch_entrega_comp, s.fch_est_despacho,
       s.dias_retraso, s.ind_retraso, s.ind_urgente, s.ind_reproceso,
       s.kg_pendientes, s.cantidad_orig,
       s.pct_avance,
       s.nro_ciclo
FROM   pln_seguimiento s
JOIN   pln_estado_codigo ec ON ec.cod_paso = s.cod_paso_act
LEFT   JOIN clientes c ON c.cod_cliente = s.cod_cliente
WHERE  s.estado = 'A'
ORDER  BY s.ind_urgente DESC, s.dias_retraso DESC, s.fch_entrega_comp
```

### Timeline de un pedido (fechas estimadas vs. reales)

```sql
SELECT ec.orden_paso,
       ec.nombre_paso,
       ec.color_ui,
       -- Fecha estimada inicio: la del paso anterior fin o la calculada
       CASE ec.cod_paso
         WHEN '02' THEN s.fch_real_programado
         WHEN '03' THEN s.fch_est_hilanderia
         WHEN '04' THEN s.fch_est_partida
         WHEN '06' THEN s.fch_est_tin_ini
         WHEN '07' THEN s.fch_est_tin_fin
         WHEN '08' THEN s.fch_est_secado
         WHEN '09' THEN s.fch_est_calidad
         WHEN '14' THEN s.fch_est_despacho
       END                            AS fch_estimada,
       -- Fecha real
       CASE ec.cod_paso
         WHEN '02' THEN s.fch_real_programado
         WHEN '03' THEN s.fch_real_produccion
         WHEN '04' THEN s.fch_real_partida
         WHEN '06' THEN s.fch_real_tin_ini
         WHEN '07' THEN s.fch_real_tin_fin
         WHEN '08' THEN s.fch_real_secado
         WHEN '09' THEN s.fch_real_cc_tinto
         WHEN '9R' THEN s.fch_real_cc_rechazo
         WHEN '10' THEN s.fch_real_devanado
         WHEN '11' THEN s.fch_real_calidad
         WHEN '12' THEN s.fch_real_alm_pt
         WHEN '14' THEN s.fch_real_despacho
       END                            AS fch_real,
       s.fch_entrega_comp,
       s.cod_paso_act                 AS paso_actual
FROM   pln_seguimiento s
CROSS  JOIN pln_estado_codigo ec
WHERE  s.num_ped = :numPed
  AND  s.serie   = :serie
  AND  s.num_det = :numDet
ORDER  BY ec.orden_paso
```

### Historial de eventos (log inmutable)

```sql
SELECT ev.fch_evento, ec.nombre_paso, ev.tipo_evento,
       ev.tabla_origen, ev.kg_cantidad,
       ev.observacion, ev.usuario
FROM   pln_log_eventos ev
JOIN   pln_estado_codigo ec ON ec.cod_paso = ev.cod_paso
WHERE  ev.num_ped = :numPed
  AND  ev.serie   = :serie
ORDER  BY ev.fch_evento DESC
```

### Alertas activas del dashboard

```sql
SELECT a.id_alerta, a.tip_alerta, a.nivel, a.titulo,
       a.detalle, a.fch_alerta, a.fch_limite,
       a.dias_retraso, a.cod_maq,
       c.descripcion AS nombre_cliente
FROM   pln_alerta a
LEFT   JOIN pln_seguimiento s ON s.id_seguim = a.id_seguim
LEFT   JOIN clientes c ON c.cod_cliente = s.cod_cliente
WHERE  a.estado = 'A'
ORDER  BY DECODE(a.nivel,'C',1,'A',2,'M',3,'B',4), a.fch_limite
```

### KPI OTIF mensual

```sql
SELECT TO_CHAR(fch_real_despacho,'MM/YYYY')  AS mes,
       COUNT(*)                               AS total_despachados,
       SUM(CASE WHEN fch_real_despacho <= fch_entrega_comp
                 AND kg_despachados >= cantidad_orig * 0.99
                THEN 1 ELSE 0 END)            AS otif,
       ROUND(SUM(CASE WHEN fch_real_despacho <= fch_entrega_comp
                       AND kg_despachados >= cantidad_orig * 0.99
                      THEN 1.0 ELSE 0 END)
             / COUNT(*) * 100, 1)             AS pct_otif,
       AVG(fch_real_despacho - fch_pedido)    AS ciclo_promedio_dias
FROM   pln_seguimiento
WHERE  estado = 'C'
  AND  fch_real_despacho IS NOT NULL
  AND  fch_real_despacho >= ADD_MONTHS(TRUNC(SYSDATE,'MM'), -6)
GROUP  BY TO_CHAR(fch_real_despacho,'MM/YYYY')
ORDER  BY 1
```

---

## 11. ESTRUCTURA DE TABLAS ORACLE LEGACY RELACIONADAS

### Tablas de origen de triggers PLN_ (leer-only desde la app)

| Tabla | PK | Campo clave para PLN_ |
|---|---|---|
| `ITEMPED` | `(SERIE, NUM_PED, NRO)` | → crea `PLN_SEGUIMIENTO` |
| `ITEMPED_DET` | `(SERIE, NUM_PED, NRO, NUM_DET)` | `NROPROG` — vínculo con PARTIDA |
| `H_RPRODUC` | `(FECHA, TP_MAQ, COD_MAQ, LOTE)` | `GUIA` → PARTIDA.NUMERO |
| `PARTIDA` | `(NUMERO)` | `NROPROG` (1:1 con ITEMPED_DET) |
| `L_VALIDA_RECETA` | compuesta | `ESTADO='3'` → laboratorio OK |
| `TT_RPRODUC` | `(RECETA, PROCESO)` | `ESTADO='3'` → baño terminado |
| `TT_RSECADO` | `(GUIA, COD_MAQ)` | `GUIA` → PARTIDA.NUMERO |
| `CTCALIDAD_D` | compuesta | `NROPART`=NUM_DET, `SER_PARTIDA`=NRO |
| `REVISADO_D` | compuesta | `GUIA` via REVISADO_G |
| `LOTES` | `(COD_ALM, TP_TRANSAC, SERIE, NUM, COD_ART, LOTE)` | `PARTIDA` → PARTIDA.NUMERO |

### Estados de PARTIDA.SITU_PART (semáforo físico)

| SITU_PART | Descripción | Corresponde a PASO |
|---|---|---|
| `(vacío)` | En hilandería / disponible | 03–04 |
| `'R001'` | Recibida en tintorería | 06 |
| `'P'` | En proceso en tintorería | 07 |
| `'A'` | Acabada (salió de TT) | 08–09 |
| `'X'` + ESTADO=9 | Cerrada/Despachada | 14 |

---

## 12. CONVENCIONES DE CÓDIGO

- **Nombres de tabla Oracle**: MAYÚSCULAS (es case-insensitive en Oracle 11g pero por convención).
- **Parámetros Dapper para Oracle**: Prefijo `:` (no `@`). Ejemplo: `:numPed`, no `@numPed`.
- **Fechas Oracle**: Siempre usar `TO_DATE(:fecha, 'DD/MM/YYYY')` en SQL embebido.
- **Decimales**: `decimal` en C# para cantidades en kg; `double` solo para porcentajes de UI.
- **Nulos Oracle**: `DBNull.Value` mapea a `null` en C# — usar tipos nullable en modelos.
- **Transacciones**: Usar `IDbTransaction` de Dapper. Los SPs PLN_ hacen su propio `COMMIT` interno.
- **No usar EF Core**: Las PKs compuestas de 4-5 campos de Oracle legacy son incompatibles con EF convencional.
- **Timezone**: La BD trabaja en hora local Lima (PET, UTC-5). No convertir fechas en la app.
