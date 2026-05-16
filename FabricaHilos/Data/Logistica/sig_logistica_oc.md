# SIG — Logística: Orden de Compra (PARAMLG)

## TIPOS DE DOCUMENTO (desde PARAMLG — tabla con 1 sola fila)
| Campo     | Valor | Descripción           |
|-----------|-------|-----------------------|
| DOCORDE   | '82'  | O.COMPRA (Orden de Compra) |
| DOCSERV   | '83'  | O.SERVICIO (Orden de Servicio) |
| DOCREQU   | '80'  | Requisición           |
| DOCSOLC   | '81'  | Solicitud             |

## SERIE
- Siempre = 1 para O.COMPRA y O.SERVICIO. No varía.
- No hay tabla de series; SERIE se hardcodea a 1.

## NUMERACIÓN (contadores en PARAMLG)
| Campo    | Tipo  | Descripción                          |
|----------|-------|--------------------------------------|
| NUMORDE  | NUMBER| Siguiente NUM_PED para DOCORDE ('82') |
| NUMSERV  | NUMBER| Siguiente NUM_PED para DOCSERV ('83') |

**Regla**: Al registrar OC → leer NUMORDE/NUMSERV con SELECT FOR UPDATE → usarlo como NUM_PED → UPDATE PARAMLG SET NUMORDE/NUMSERV = NUMORDE/NUMSERV + 1.

## PKG_REG_ORDEN_COMPRA — P_REGISTRAR_OC
- P_SERIE fue ELIMINADO del spec (la serie siempre es 1, la asigna el sistema)
- P_TIPO_DOCTO se valida contra PARAMLG.DOCORDE / PARAMLG.DOCSERV (no hardcoded)
- F_SIGUIENTE_NUM_PED fue ELIMINADO; reemplazado por lógica PARAMLG con FOR UPDATE
- P_ANULAR_OC conserva P_SERIE como IN (necesario para identificar la OC a anular)

## ARCHIVO
`d:\.Net\WorkSpace_BD\SIG\Logistica\PKG_REG_ORDEN_COMPRA.sql`
