# SIG — Módulo Logística: Requisición y Orden de Compra

> Análisis basado en: `D:\.Net\WorkSpace_BD\SIG\Logistica\RequisicionService.cs` y `OrdenCompraService.cs`
> BD Oracle: 10.0.7.11:1521/ORCL — usuario: SIG — analizado: 11/05/2026

---

## TABLAS PRINCIPALES

### 1. REQUISICION (cabecera de requerimiento)
**PK**: TIPDOC + SERIE + NUMREQ

| Campo | Tipo | Desc / Notas |
|---|---|---|
| TIPDOC | VARCHAR2(2) | Tipo doc. Siempre `'80'` (REQUERIMIENTO DE COMPRA Y/O SERVICIO) |
| SERIE | NUMBER(3) | Serie. Siempre `1` en BD actual |
| NUMREQ | NUMBER(8) | Número de requerimiento. 42,026 registros (hasta NUMREQ=42029) |
| CENTRO_COSTO | VARCHAR2(6) | Centro de costo solicitante (FK no enforced a CENTRO_DE_COSTOS) |
| PROVEEDORES | VARCHAR2(200) | Sugerencia de proveedor (libre, no FK) |
| FECHA | DATE | Fecha de creación del requerimiento |
| F_ENTREGA | DATE | Fecha de entrega solicitada |
| RESPONSABLE | VARCHAR2(8) | Código del empleado responsable (FK no enforced a V_PERSONAL) |
| PRIORIDAD | VARCHAR2(4) | `'01'`=URGENTE, `'02'`=CORRIENTE (TABLAS_AUX TIPO=70) |
| OBSERVACION | VARCHAR2(250) | Texto libre |
| ESTADO | VARCHAR2(1) | **Ver tabla de estados abajo** |
| DESTINO | VARCHAR2(4) | Destino general del requerimiento (TABLAS_AUX TIPO=85) |
| IND_SERV | VARCHAR2(1) | `'S'`=servicio, `'N'`=bien/material. 34,519 N / 7,506 S |
| IMPSTO | NUMBER(2,2) | Porcentaje de impuesto |
| AFECTO_IGV | VARCHAR2(1) | Afecto a IGV |
| AFECTO_IRENTA | VARCHAR2(1) | Afecto a retención de renta |
| TIP_REF | VARCHAR2(2) | Tipo doc de referencia (si viene de otro doc) |
| SER_REF | NUMBER(5) | Serie doc referencia |
| NRO_REF | NUMBER(20) | Número doc referencia |
| F_AUTORIZA | DATE | Fecha de autorización (PASO visado) |
| AUTORIZA | VARCHAR2(8) | Código empleado que autorizó |
| USER_AUTORIZA | VARCHAR2(15) | Usuario del sistema que registró autorización |
| IP_AUTORIZA | VARCHAR2(20) | IP desde donde se autorizó |
| F_RECIBE | DATE | Fecha recepción logística |
| RECIBE | VARCHAR2(8) | Código empleado que recibe en logística |
| FCH_ENTREGA_LOGIST | DATE | Fecha entrega de logística (se pone en cancelaciones masivas) |
| NOTA_ANULACION | VARCHAR2(500) | Motivo de anulación / histórico de activaciones |
| A_ADUSER | VARCHAR2(15) | Usuario que creó |
| A_ADFECHA | DATE | Fecha de creación |
| A_MDUSER | VARCHAR2(15) | Usuario última modificación |
| A_MDFECHA | DATE | Fecha última modificación |

#### ESTADOS DE REQUISICION (TABLAS_AUX TIPO=84)
| ESTADO | Descripción | Qty real |
|---|---|---|
| `'0'` | REGISTRADO (sin visar) | 7 |
| `'1'` | VISADO / AUTORIZADO | 54 |
| `'2'` | RECIBIDO por logística | 48 |
| `'6'` | ATENDIDO / CERRADO | 38,918 (92.6%) |
| `'9'` | ANULADO | 2,999 |

**FLUJO**: 0 (registro) → 1 (visado/autorizado) → 2 (recibido logística) → 6 (atendido)
- En el listado por defecto se **excluyen** estados 6 y 9 (solo si no hay filtro explícito)
- `CambiarEstadoAsync` estado `'9'`: también escribe `FCH_ENTREGA_LOGIST=SYSDATE` y `NOTA_ANULACION='Anulacion Masiva'`
- `ActivarRequisicionesAsync` (ESTADO=9 → activo): si tiene AUTORIZA → `'1'`; si no → `'2'`

---

### 2. ITEMREQ (ítems del requerimiento)
**PK**: TIPDOC + SERIE + NUMREQ + ORDEN + COD_ART
**FK**: → REQUISICION (TIPDOC,SERIE,NUMREQ) via FK_ITEMREQ_REQ
**FK**: → ARTICUL (COD_ART) via FK_ITEMREQ_ARTICUL

| Campo | Tipo | Desc / Notas |
|---|---|---|
| TIPDOC | VARCHAR2(2) | Mismo que cabecera |
| SERIE | NUMBER(3) | Mismo que cabecera |
| NUMREQ | NUMBER(8) | Número de requerimiento |
| ORDEN | NUMBER(2) | Nro de línea (1, 2, 3…) |
| COD_ART | VARCHAR2(25) | Código artículo (FK a ARTICUL) |
| CANTIDAD | NUMBER(12,4) | Cantidad solicitada |
| SALDO | NUMBER(12,4) | Saldo pendiente de atender. 39,510 con SALDO=0 (atendidos); 67,350 SALDO=CANTIDAD (sin atender) |
| DETALLE | VARCHAR2(400) | Descripción/detalle libre del ítem |
| UNIDAD | VARCHAR2(4) | Unidad de medida |
| TP_DESTINO | VARCHAR2(1) | `'U'`=Unidad productiva (89,393), `'A'`=Área (17,923) |
| DESTINO | VARCHAR2(15) | Código específico del destino (CC o área) |
| COD_SOLICITA | VARCHAR2(8) | Empleado solicitante del ítem |
| MONEDA | VARCHAR2(1) | `'S'`=Soles (76,694), `'D'`=Dólares (30,622) |
| PRECIO | NUMBER(18,6) | Precio referencial (puede ser 0) |
| STK_MIN | NUMBER(12,4) | Stock mínimo al momento de solicitar |
| STK_HIST | NUMBER(12,4) | Stock histórico referencial |
| MARCA | VARCHAR2(4) | Marca del artículo |
| CTACTBLE | VARCHAR2(15) | Cuenta contable |
| OBSERVACIONES | VARCHAR2(150) | Observaciones del ítem |
| F_APROBADO | DATE | Fecha de aprobación del grupo (se pone vía `AprobarGrupoAsync`) |
| ID_GRUPO | NUMBER(8) | ID del grupo de aprobación (de secuencia LG_GRUPO_SEQ) |
| A_ADUSER… | | Auditoría estándar |

---

### 3. ORDEN_DE_COMPRA (cabecera de O/C)
**PK**: TIPO_DOCTO + SERIE + NUM_PED

| Campo | Tipo | Desc / Notas |
|---|---|---|
| TIPO_DOCTO | VARCHAR2(2) | `'82'`=ORDEN DE COMPRA (26,525), `'83'`=ORDEN DE SERVICIO (4,975) |
| SERIE | NUMBER(3) | Serie. Siempre `1` en BD |
| NUM_PED | NUMBER(8) | Número de OC/OS. 31,500 registros |
| ESTADO | VARCHAR2(1) | **Ver tabla de estados abajo** |
| FECHA | DATE | Fecha de la OC. Rango: 21/12/2009 – 08/05/2026 |
| COD_PROVEED | VARCHAR2(15) | Código proveedor (RUC generalmente). FK a PROVEED |
| NRO_SUCUR | VARCHAR2(4) | Sucursal del proveedor |
| COND_PAG | VARCHAR2(4) | Condición de pago. FK a CONDPAG. Top: AO=CONTADO, AI=30d, OF=90d |
| MONEDA | VARCHAR2(1) | `'D'`=Dólares (18,613=59%), `'S'`=Soles (12,887=41%) |
| COD_VENDE | VARCHAR2(4) | Sin uso en BD actual (0 registros con valor) |
| C_CODIGO | VARCHAR2(8) | Responsable de la OC (empleado). 31,480/31,500 usan este campo |
| PLAZO_ENTREGA | NUMBER(3) | Días de plazo de entrega |
| TIPDOC_REF | VARCHAR2(2) | Tipo doc referencia (legado) |
| NUMREQU_REF | VARCHAR2(8) | Num requerimiento referencia (legado, sin uso funcional) |
| DETALLE | VARCHAR2(200) | Descripción general de la OC |
| POLIZA | VARCHAR2(20) | Póliza de seguro (importaciones) |
| CUENTA42 | VARCHAR2(15) | Cuenta contable clase 42 |
| TOTAL_PEDIDO | NUMBER(14,2) | Total del pedido |
| TOTAL_FACTURADO | NUMBER(14,2) | Total facturado. Siempre 0 en BD actual (no se actualiza) |
| C_COSTO | VARCHAR2(6) | Centro de costo de la OC |
| L_ENTREGA / OPC_LENTR | VARCHAR2 | Lugar de entrega y opción |
| F_ENTREGA | DATE | Fecha estimada de entrega |
| VAL_VENTA | NUMBER(14,2) | Valor de venta (sin IGV, sin descuento) |
| IMP_DESCTO | NUMBER(14,2) | Importe descuento |
| IMP_NETO | NUMBER(14,2) | Importe neto (VAL_VENTA - IMP_DESCTO) |
| IMP_IGV | NUMBER(14,2) | Importe IGV |
| PRECIO_VTA | NUMBER(14,2) | Precio de venta total (IMP_NETO + IMP_IGV) |
| POR_DESC1 / POR_DESC2 | NUMBER(6,4) | Porcentajes de descuento |
| IMPSTO | NUMBER(2,2) | Porcentaje de impuesto |
| APROB_GERENCIA | VARCHAR2(1) | `'S'`=aprobada por gerencia (24,369 registros). Vacío=sin aprobación |
| FIRMA_GERENCIA | VARCHAR2(1) | Flag firma gerencia |
| F_APROB_GER | DATE | Fecha aprobación gerencia |
| COD_FLUJOEF | VARCHAR2(6) | Código flujo de efectivo |
| A_ADUSER… | | Auditoría estándar |

#### ESTADOS DE ORDEN_DE_COMPRA
| ESTADO | Descripción | Qty real |
|---|---|---|
| `'0'` | ABIERTA / ACTIVA | 4,619 (14.7%) |
| `'5'` | (raro) | 1 |
| `'6'` | CERRADA / ATENDIDA | 25,856 (82.1%) |
| `'9'` | ANULADA | 1,024 (3.3%) |

---

### 4. ITEMORD (ítems de la orden de compra)
**PK**: TIPO_DOCTO + SERIE + NUM_PED + ORDEN + COD_ART
**FK**: → ORDEN_DE_COMPRA (TIPO_DOCTO,SERIE,NUM_PED) via FK_ITEMORD_OCOMPRA

| Campo | Tipo | Desc / Notas |
|---|---|---|
| TIPO_DOCTO | VARCHAR2(2) | Mismo que cabecera OC |
| SERIE | NUMBER(3) | Mismo que cabecera |
| NUM_PED | NUMBER(8) | Número de OC |
| ORDEN | NUMBER(2) | Nro de línea |
| COD_ART | VARCHAR2(25) | Código artículo |
| COD_ORIG | VARCHAR2(25) | Código artículo original del proveedor |
| UNIDAD | VARCHAR2(4) | Unidad |
| U_EQV | NUMBER(16,4) | Factor de equivalencia de unidad |
| DESCRIPCION | VARCHAR2(500) | Descripción del ítem en la OC |
| CANTIDAD | NUMBER(18,6) | Cantidad ordenada |
| CANTIDAD_EQV | NUMBER(18,6) | Cantidad equivalente |
| SALDO | NUMBER(18,6) | Saldo pendiente de recibir. 12,027 con SALDO=0 (atendidos) |
| PRECIO | NUMBER(18,6) | Precio unitario |
| POR_DESC1/2 | NUMBER(6,4) | Descuentos del ítem |
| IGV | NUMBER(14,6) | Monto IGV |
| IMP_VVTA | NUMBER(14,2) | Importe valor de venta del ítem |
| ESTADO | VARCHAR2(1) | `'6'`=cerrado(61,796), `''`=abierto(11,825), `'9'`=anulado(2,426), `'0'`=reg(733) |
| TIPO_DESTINO | VARCHAR2(1) | `'U'`=unidad, `'A'`=área. 60,298 vacío (OCs antiguas) |
| COD_DESTINO | VARCHAR2(15) | Código destino específico |
| C_CODIGO | VARCHAR2(8) | Responsable del ítem |
| ID_GRUPO | NUMBER(8) | ID grupo de aprobación (sincronizado con ITEMREQ.ID_GRUPO) |
| F_GRUPO | DATE | Fecha aprobación del grupo (= ITEMREQ.F_APROBADO) |
| A_ADUSER… | | Auditoría estándar |

---

### 5. DESP_ITEMREQ (tabla puente: ítems de req → ítems de OC)
**FK → ITEMREQ**: FK_DESP_ITEMREQ (TIPDOC,SERIE,NUMREQ,ORDEN,COD_ART)
**FK → ITEMORD**: FK_DESP_OCOMPRA (TIP_DOC_REF,SER_DOC_REF,NRO_DOC_REF,ORDEN_REF,COD_ART)

| Campo | Tipo | Desc |
|---|---|---|
| TIPDOC | VARCHAR2(2) | Tipo doc del requerimiento |
| SERIE | NUMBER(3) | Serie del requerimiento |
| NUMREQ | NUMBER(8) | Número de requerimiento |
| ORDEN | NUMBER(2) | Orden del ítem en el requerimiento |
| COD_ART | VARCHAR2(25) | Código artículo |
| TIP_DOC_REF | VARCHAR2(2) | Tipo doc de la OC (82/83) |
| SER_DOC_REF | NUMBER(4) | Serie de la OC |
| NRO_DOC_REF | NUMBER(8) | **Número de la OC** (NUM_PED). Clave de join a ORDEN_DE_COMPRA |
| CANTIDAD | NUMBER(12,4) | Cantidad asignada a la OC |
| ESTADO | VARCHAR2(1) | Estado del despacho (default '0') |
| ORDEN_REF | NUMBER(2) | Orden del ítem en la OC (ITEMORD.ORDEN) |

**Estadísticas**: 40,949 registros; 15,553 REQs con OC; 15,580 OCs distintas.
**NOTA**: En el código, `NRO_DOC_REF` se usa como `NUMBER` en BD pero como `VARCHAR2` al hacer JOIN con `ITEMORD` (`TO_CHAR(IO.NUM_PED) = D.NRO_DOC_REF`). En consultas directas se une numéricamente: `O.NUM_PED = D.NRO_DOC_REF`.

---

## TABLAS DE SOPORTE

### ARTICUL
**PK**: COD_ART (VARCHAR2(25))
- `COD_ART`, `DESCRIPCION` (VARCHAR2(100)), `UNIDAD` (VARCHAR2(4))
- 69 columnas en total (stock, costos, características de artículo)

### PROVEED
- `COD_PROVEED` (PK), `NOMBRE`
- OC usa `COD_PROVEED` que generalmente es RUC del proveedor

### CONDPAG
- `COND_PAG` (PK, VARCHAR2(4)), `DESCRIPCION`
- 758 condiciones de pago. FK referenciada por ORDEN_DE_COMPRA
- Más usadas: `AO`=CONTADO, `AI`=FACT.30d, `OF`=LETRA$90d, `AA`=CONTRA/ENTREGA, `AF`=FACT.15d

### CENTRO_DE_COSTOS
- `CENTRO_COSTO` (PK), `NOMBRE`

### TABLAS_AUXILIARES
- `TIPO` + `CODIGO` + `DESCRIPCION`
- TIPO=`70`: PRIORIDADES (`01`=URGENTE, `02`=CORRIENTE)
- TIPO=`84`: ESTADOS REQUERIMIENTO (`0`=Registrado…`9`=Anulado)
- TIPO=`85`: DESTINOS REQUERIMIENTO (`00`=Otros svc, `01`=Stock, `02`=Directo máquina, `03`=Proyecto, `04`=Economato, `05`=Mant.edif., `06`=Activo fijo, `99`=Feria)
- TIPO=`83`: GRAN CENTRO DE COSTO (16=Logística, 09=Mantenimiento, etc.)
- TIPO=`2`: DOCUMENTOS (`80`=Requerimiento, `82`=Orden Compra, `83`=Orden Servicio)

### V_PERSONAL (vista)
- `C_CODIGO` (código empleado), `NOMBRE_CORTO`
- Filtro estándar: `SITUACION='1'` (activos)

### REGISTRO_DIARIO
- `TIPO='RS'` = facturas/documentos de proveedor ligados a OC (19,200 registros)
- `NUM_REF` = referencia a NUM_PED de la OC
- `RELACION` = COD_PROVEED
- `TIPDOC`, `SERIE`, `NUMERO` = identifican la factura

### FACTPAG
- Tabla de saldos por pagar
- `COD_PROVEEDOR`, `TIPDOC`, `SERIE_NUM`, `NUMERO` = identifica el doc
- `SALDO` = saldo pendiente (SALDO=0 significa pagado)
- `PVENTA` = precio de venta

---

## SECUENCIA

| Secuencia | Rango | Uso |
|---|---|---|
| `LG_GRUPO_SEQ` | 0 – 99,999,999 | Genera `ID_GRUPO` para agrupación de aprobación. Último valor: 6 (poca actividad) |

---

## FLUJO LOGÍSTICO (4 ETAPAS — `ObtenerProgresoGeneralAsync`)

```
REQUISICION
    │  ESTADO: 0→1→2→6
    │
    ├── ITEMREQ (ítems del req)
    │       │  ID_GRUPO → agrupación para cotización/aprobación
    │       │  F_APROBADO → fecha aprobación del grupo
    │       │
    │       └── [ETAPA 1: Grupos aprobados / total grupos]
    │
    ├── DESP_ITEMREQ (puente)
    │       │  NUMREQ+ORDEN ↔ NRO_DOC_REF(NUM_PED)+ORDEN_REF
    │       │
    │       └── [ETAPA 2: Ítems con OC asignada / total ítems aprobados]
    │
    ├── ORDEN_DE_COMPRA (cabecera)
    │       │  TIPO_DOCTO: 82=OC, 83=OS
    │       │  ESTADO: 0=abierta, 6=cerrada, 9=anulada
    │       │
    │       └── ITEMORD (ítems de OC)
    │               ID_GRUPO / F_GRUPO = sincronizado con ITEMREQ
    │
    ├── REGISTRO_DIARIO (TIPO='RS')
    │       │  NUM_REF = NUM_PED (enlace OC → factura proveedor)
    │       │
    │       └── [ETAPA 3: OCs con factura / total OCs]
    │
    └── FACTPAG
            │  Join por TIPDOC+SERIE+NUMERO+COD_PROVEEDOR
            │  SALDO=0 → pagado
            │
            └── [ETAPA 4: Facturas pagadas / total facturas]
```

---

## MECANISMO DE GRUPOS (aprobación de cotizaciones)

La funcionalidad de grupos sincroniza `ITEMREQ` y `ITEMORD` para el flujo de aprobación de cotizaciones:

1. **Asignar grupo** (`ActualizarIdGrupoItemsAsync`):
   - Desde REQ: `UPDATE ITEMREQ SET ID_GRUPO = :id` por ordenes seleccionadas
   - Propaga a `ITEMORD` via `DESP_ITEMREQ` usando `NRO_DOC_REF`
   - Desde OC: `UPDATE ITEMORD SET ID_GRUPO = :id` por COD_ART+ORDEN
   - Propaga a `ITEMREQ` via `DESP_ITEMREQ`

2. **Aprobar grupo** (`AprobarGrupoAsync`):
   - `ITEMREQ.F_APROBADO = SYSDATE` donde `ID_GRUPO = :id`
   - `ITEMORD.F_GRUPO = SYSDATE` propagando via `DESP_ITEMREQ`

3. **Desaprobar** (`DesaprobarGrupoAsync`): reversa (NULL)

4. **Limpiar** (`LimpiarIdGrupoAsync`): `ID_GRUPO=NULL, F_APROBADO/F_GRUPO=NULL`
   - **ORDEN**: primero ITEMORD, luego ITEMREQ (versión REQ-service)
   - **ORDEN inverso**: primero ITEMREQ, luego ITEMORD (versión OC-service) ← pequeña diferencia de implementación

---

## JOINS CRÍTICOS

```sql
-- Req → O/C via DESP_ITEMREQ
FROM REQUISICION R
JOIN ITEMREQ IR ON IR.TIPDOC=R.TIPDOC AND IR.SERIE=R.SERIE AND IR.NUMREQ=R.NUMREQ
LEFT JOIN DESP_ITEMREQ D ON D.TIPDOC=IR.TIPDOC AND D.SERIE=IR.SERIE 
                         AND D.NUMREQ=IR.NUMREQ AND D.ORDEN=IR.ORDEN
LEFT JOIN ORDEN_DE_COMPRA OC ON OC.NUM_PED=D.NRO_DOC_REF AND OC.TIPO_DOCTO=D.TIP_DOC_REF
LEFT JOIN ITEMORD IO ON IO.TIPO_DOCTO=OC.TIPO_DOCTO AND IO.SERIE=OC.SERIE 
                     AND IO.NUM_PED=OC.NUM_PED AND IO.COD_ART=IR.COD_ART

-- Facturas de una O/C
FROM ORDEN_DE_COMPRA OC
JOIN REGISTRO_DIARIO RD ON RD.NUM_REF = OC.NUM_PED AND RD.TIPO = 'RS'
LEFT JOIN FACTPAG FP ON FP.TIPDOC=RD.TIPDOC AND FP.SERIE_NUM=RD.SERIE 
                     AND FP.NUMERO=RD.NUMERO AND FP.COD_PROVEEDOR=RD.RELACION
```

---

## NOTAS IMPORTANTES

1. **Un requerimiento → múltiples OCs**: un req puede dividirse en varias OCs (distintos proveedores o fechas). DESP_ITEMREQ registra cada asignación ítem-por-ítem.
2. **SALDO en ITEMREQ**: cantidad pendiente de atender. Disminuye conforme se atiende desde la OC. SALDO=0 significa ítem completamente atendido.
3. **SALDO en ITEMORD**: cantidad pendiente de recibir físicamente. SALDO=0 significa ítem de OC recibido en almacén.
4. **TOTAL_FACTURADO en ORDEN_DE_COMPRA**: siempre 0 en BD actual. No se actualiza desde este módulo.
5. **COD_VENDE en ORDEN_DE_COMPRA**: sin uso (0 registros con valor). El responsable es `C_CODIGO`.
6. **Búsqueda sin fechas**: cuando hay texto de búsqueda (`buscar`), se ignoran los filtros de fecha en ambos servicios.
7. **Filtro base de listado**: REQ excluye estado 6 y 9 por defecto; OC excluye estado 6 y 9 por defecto.
8. **NOTA_ANULACION en REQ**: se acumula (append `' - Activacion Masiva'`) en reactivaciones.
9. **Antigüedad**: datos desde 2009 (OC) y 2013 (REQ). Gran mayoría histórica (ESTADO=6).
10. **FK_DESP_OCOMPRA** apunta a PK_ITEMORD (no a PK_ORDEN_COMPRA). El join es a nivel de ítem, no de cabecera.

---

## PKG_REG_ORDEN_COMPRA — NOTAS DEL PACKAGE

- `P_SERIE` eliminado del spec (la serie siempre es 1, la asigna el sistema)
- `P_TIPO_DOCTO` se valida contra `PARAMLG.DOCORDE` / `PARAMLG.DOCSERV` (no hardcoded)
- Numeración con `SELECT FOR UPDATE` en `PARAMLG.NUMORDE` / `NUMSERV` (evita duplicados en concurrencia)
- `P_ANULAR_OC` conserva `P_SERIE` como IN (necesario para identificar la OC)

### P_OBTENER_FIRMAS_OC (formato final 12/05/2026)

Retorna las **2 firmas del PDF de la Orden de Compra**:

```sql
PROCEDURE P_OBTENER_FIRMAS_OC (
    P_TIPO_DOCTO      IN  ORDEN_DE_COMPRA.TIPO_DOCTO%TYPE,
    P_SERIE           IN  ORDEN_DE_COMPRA.SERIE%TYPE,
    P_NUM_PED         IN  ORDEN_DE_COMPRA.NUM_PED%TYPE,
    P_CURSOR_GENERADO OUT T_CURSOR,   -- caja 1: GENERADO POR  (Logística)
    P_CURSOR_APROBADO OUT T_CURSOR    -- caja 2: APROBADO POR  (Gerencia General)
);
```

**Cada cursor retorna:** `C_CODIGO, NOMBRE_COMPLETO, CARGO, ROL_ETIQUETA, FIRMA (LONG RAW)`

#### Mapa de cajas PDF (izq → der)
| # | Etiqueta | Sub-título | Fuente | Ejemplo (OC 26532) |
|---|---|---|---|---|
| 1 | GENERADO POR | Logística | `ORDEN_DE_COMPRA.C_CODIGO` (dinámico) | `034628` JOSHELYN KAROL YAÑEZ |
| 2 | APROBADO POR | Gerencia General | fijo `'034001'` | FERNANDO CARMELO FIOCCO BLOISA |

Constante interna en body: `C_GERENTE CONSTANT VARCHAR2(8) := '034001'`
Si cambia el gerente → actualizar solo esa constante.

#### Campos de firma en REQUISICION (para referencia)
| Campo | Caja en REQ impresa | Persona |
|---|---|---|
| `RESPONSABLE` | HECHO POR | Quien solicitó |
| `AUTORIZA` | APROBADO POR | Jefe del área |
| `RECIBE` | RECIBIDO POR | Logística (generalmente = `OC.C_CODIGO`) |

#### Tablas de soporte para firmas
- `RH_PERSONAS` (`C_CODIGO`, `APELLIDO_PATERNO`, `APELLIDO_MATERNO`, `NOMBRES`)
- `RH_PERSONAL` (`C_CODIGO`, `C_CARGO`)
- `T_CARGO` (`C_CARGO`, `DESCRIPCION`) — catálogo de puestos
- `RH_FIRMAS` (`C_CODIGO`, `FIRMA LONG RAW`) — 114 registros, todos con firma cargada

**Técnica LONG RAW**: Oracle no permite `UNION`/`DISTINCT` sobre `LONG RAW`. Solución: subquery `DISTINCT` sin la columna `FIRMA`, luego `JOIN RH_FIRMAS` en capa exterior. Evita ORA-00997.
