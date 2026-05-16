# SIG — Logística: Indicadores / Query_indicadores (15/05/2026)

## QUERY PRINCIPAL (`Query_indicadores.sql`)

Devuelve el **detalle de requisiciones con sus ítems**, incluyendo:
- Tipo (COMPRA o SERVICIO según IND_SERV)
- Número y fechas de la requisición (registro, autorización, recibo logística)
- Orden de compra asociada (si existe) y su fecha
- Destino (Centro de Costos o Activo Fijo)
- Solicitante, artículo, cantidades, precios e IGV
- Estado legible de la requisición

Parámetros de entrada: `:FECHA_DESDE` y `:FECHA_HASTA` (sobre `REQUISICION.FECHA`)

---

## TABLAS INVOLUCRADAS

| Alias | Tabla / Vista         | Tipo join | Rol                                             |
|-------|-----------------------|-----------|-------------------------------------------------|
| R     | REQUISICION           | INNER     | Cabecera de requisición (filtro de fechas y estado) |
| I     | ITEMREQ               | INNER     | Ítems de la requisición (artículos, cant., saldo, precio, destino) |
| A     | ARTICUL               | LEFT (+)  | Catálogo de artículos; puede ser NULL si COD_ART empieza con 'PEDIDO' |
| D     | DESP_ITEMREQ          | LEFT (+)  | Despacho del ítem a una OC (NRO_DOC_REF = NUM_PED de OC); TIPDOC='80' |
| O     | ORDEN_DE_COMPRA       | LEFT (+)  | Cabecera de la OC (SERIE=1 siempre, TIPO_DOCTO=D.TIP_DOC_REF) |
| T     | TABLAS_AUXILIARES     | INNER     | TIPO=84 → estados de REQUISICION (CODIGO=ESTADO, ABREVIADA=label) |
| P     | V_PERSONAL            | INNER     | Nombre del solicitante (I.COD_SOLICITA = P.C_CODIGO) |
| C     | V_CENTRO_DE_COSTOS    | LEFT (+)  | Descripción cuando I.TP_DESTINO='U' (C.CCOSTO_DET=I.DESTINO) |
| F     | ACTIVO_FIJO           | LEFT (+)  | Descripción cuando I.TP_DESTINO='A' (CODIGO||'-'||NUMERO=I.DESTINO) |

---

## CAMPOS CLAVE EN REQUISICION

| Campo          | Tipo  | Descripción                                              |
|----------------|-------|----------------------------------------------------------|
| NUMREQ         | NUM   | PK de requisición (con TIPDOC='80', SERIE=1)             |
| FECHA          | DATE  | Fecha de registro (filtro principal)                     |
| F_AUTORIZA     | DATE  | Fecha autorización por jefe                              |
| F_RECIBE       | DATE  | Fecha recibo por Logística                               |
| FCH_ENTREGA_LOGIST | DATE | Fecha en que Logística entregó/generó la OC           |
| IND_SERV       | CHAR  | 'N'=Compra, 'S'=Servicio                                 |
| ESTADO         | CHAR  | '0'=Registrado, '1'=Visado, '2'=Recibido, '6'=Atendido, '9'=Anulado |
| AFECTO_IGV     | CHAR  | 'S'=afecto IGV, 'N'=exonerado                            |
| IMPSTO         | NUM   | Tasa IGV (ej. 0.18)                                      |
| OBSERVACION    | VARCHAR| Comentario libre                                        |

## CAMPOS CLAVE EN ITEMREQ

| Campo       | Tipo | Descripción                                                    |
|-------------|------|----------------------------------------------------------------|
| NUMREQ      | NUM  | FK a REQUISICION                                               |
| COD_ART     | VAR  | Código de artículo (puede iniciar con 'PEDIDO' → texto libre) |
| CANTIDAD    | NUM  | Cantidad solicitada                                            |
| SALDO       | NUM  | Cantidad pendiente de despachar                                |
| PRECIO      | NUM  | Precio unitario                                                |
| UNIDAD      | VAR  | Unidad de medida                                               |
| TP_DESTINO  | CHAR | 'U'=Centro de costos, 'A'=Activo fijo                         |
| DESTINO     | VAR  | Código del CC o AF                                             |
| COD_SOLICITA| VAR  | Código del personal solicitante                                |

---

## CAMPOS CALCULADOS EN EL QUERY

| Alias       | Cálculo                                                                     |
|-------------|-----------------------------------------------------------------------------|
| CANT_DESP   | `I.CANTIDAD - I.SALDO` — cantidad ya despachada                             |
| SUB_TOTAL   | `I.CANTIDAD * I.PRECIO`                                                     |
| IGV         | `SUB_TOTAL * R.IMPSTO` si `AFECTO_IGV='S'`, 0 si no                        |
| TOTAL       | `SUB_TOTAL * (IMPSTO+1)` si afecto, `SUB_TOTAL` si no                      |

---

## FLUJO DEL PROCESO LOGÍSTICO

```
Solicitante → REGISTRO REQ (ESTADO='0')
           → VISADO jefe (F_AUTORIZA, ESTADO='1')
           → RECIBO Logística (F_RECIBE, ESTADO='2')
           → GENERAR O/C (ORDEN_DE_COMPRA, DESP_ITEMREQ, ESTADO='6' si todo despachado)
```

**Tiempos reales (últimos 12 meses):**
- Registro → Autorización : ~0 días (mismo día)
- Autorización → Recibo Log: ~9 días
- Recibo Log → OC generada : ~3 días

**Distribución actual (últimos 12 meses):**
- 87.5% ATENDIDO, 7.4% ANULADO, 2.7% RECIBIDO, 1.9% VISADO, 0.5% REGISTRADO

---

## PACKAGE DE INDICADORES

**Archivo:** `d:\.Net\WorkSpace_BD\SIG\Logistica\PKG_IND_LOGISTICA.sql`
**Nombre:** `PKG_IND_LOGISTICA`

### Procedimientos

| Procedimiento       | Descripción                                                      |
|---------------------|------------------------------------------------------------------|
| `P_DETALLE`         | Devuelve el query completo (1 cursor, igual al query original)   |
| `P_DASHBOARD`       | 4 cursores KPI: resumen estados, tiempos ciclo, top CC, pendientes |

### Parámetros comunes
- `P_FECHA_DESDE IN DATE`
- `P_FECHA_HASTA IN DATE`
- Cursores OUT de tipo `T_CURSOR` (REF CURSOR)

---

## INDICADORES IMPLEMENTADOS

### P_DASHBOARD — 4 cursores:
1. **CUR_RESUMEN**: por TIPO y ESTADO → CNT_REQS, CNT_ITEMS, MONTO_TOTAL, PCT_ATENDIDO
2. **CUR_TIEMPOS**: promedios del ciclo → DIAS_REG_AUTORIZACION, DIAS_AUT_RECIBO, DIAS_RECIBO_OC, DIAS_CICLO_TOTAL
3. **CUR_TOP_CCOSTO**: top 10 CC/AF por monto total solicitado
4. **CUR_PENDIENTES**: ítems con SALDO>0 agrupados → CNT, MONTO_PENDIENTE, DIAS_EN_ESPERA

---

## NOTAS DE INTEGRACIÓN .NET (ODP.NET)

```csharp
var cmd = new OracleCommand("PKG_IND_LOGISTICA.P_DASHBOARD", conn);
cmd.CommandType = CommandType.StoredProcedure;
cmd.Parameters.Add("P_FECHA_DESDE", OracleDbType.Date, fechaDesde, ParameterDirection.Input);
cmd.Parameters.Add("P_FECHA_HASTA", OracleDbType.Date, fechaHasta, ParameterDirection.Input);
cmd.Parameters.Add("P_CUR_RESUMEN",   OracleDbType.RefCursor, ParameterDirection.Output);
cmd.Parameters.Add("P_CUR_TIEMPOS",   OracleDbType.RefCursor, ParameterDirection.Output);
cmd.Parameters.Add("P_CUR_TOP_CCOSTO",OracleDbType.RefCursor, ParameterDirection.Output);
cmd.Parameters.Add("P_CUR_PENDIENTES",OracleDbType.RefCursor, ParameterDirection.Output);
```
