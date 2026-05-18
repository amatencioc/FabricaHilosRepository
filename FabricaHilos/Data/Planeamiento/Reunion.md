# REUNIÓN — SISTEMA DE PLANEAMIENTO DE PLANTA
## Guía de presentación · 18 de Mayo 2026

> **Para qué sirve este documento:**  
> Explicar el proyecto a usuarios, jefes de área y gerencia, en lenguaje claro,  
> sin tecnicismos. Toda la parte técnica está en `Propuesta.md`.

---

## PARTE 1 — LA PREGUNTA QUE EL SISTEMA RESPONDE

> *"Un cliente llamó hoy preguntando por su pedido. ¿En qué etapa está exactamente? ¿Va a llegar a tiempo?"*

Hoy esa pregunta requiere llamar a 4 o 5 personas distintas — y aun así nadie tiene la imagen completa.  
Con el sistema nuevo: **una sola pantalla, respuesta en segundos.**

---

## PARTE 2 — EL FLUJO COMPLETO: DE LA ORDEN AL DESPACHO

Todo pedido de hilado recorre exactamente **14 etapas** (más 1 ramal de reproceso si el control de calidad rechaza).  
El sistema las monitorea **todas**, de forma automática, sin que nadie tenga que hacer nada extra.

```
╔══════════════════════════════════════════════════════════════════════════════════════════════╗
║        FLUJO COMPLETO DEL PEDIDO — 14 PASOS + RAMAL DE REPROCESO                           ║
║        Del contrato con el cliente hasta el camión que sale a entregar                      ║
║        Ciclo típico total: 12 a 18 días hábiles (22 si hay reproceso)                      ║
╚══════════════════════════════════════════════════════════════════════════════════════════════╝

 ┌──────────────────────────────────────────────────────────────────────────────────────────┐
 │  ÁREA: VENTAS / COMERCIAL                                               Tiempo: ~1 día   │
 │                                                                                          │
 │  PASO 01 — PEDIDO REGISTRADO                                                             │
 │  ─────────────────────────────────────────────────────────────────────────────────────  │
 │  Qué pasa: El vendedor ingresa el pedido del cliente en el sistema.                      │
 │            Especifica: artículo, kilogramos, color, título del hilado y la               │
 │            fecha de entrega comprometida al cliente.                                     │
 │                                                                                          │
 │  Tabla que lo activa: ITEMPED (cuando el vendedor graba el ítem del pedido)              │
 │  Automáticamente: el sistema crea el registro de seguimiento en PLN_SEGUIMIENTO          │
 │                   con estado "PENDIENTE DE PLANIFICACIÓN"                                │
 └──────────────────────────────────────────────┬───────────────────────────────────────────┘
                                                │
                                                ▼
 ┌──────────────────────────────────────────────────────────────────────────────────────────┐
 │  ÁREA: PLANIFICACIÓN                                                    Tiempo: ~1 día   │
 │                                                                                          │
 │  PASO 02 — PLANIFICADO                                                                   │
 │  ─────────────────────────────────────────────────────────────────────────────────────  │
 │  Qué pasa: El planificador abre el ítem del pedido y le asigna:                         │
 │            · El PROCESO productivo (p. ej. "Hilado peinado 30/1")                       │
 │            · El PROGRAMA de trabajo (número de programa = NROPROG)                      │
 │            · Las FECHAS ESTIMADAS para cada etapa del ciclo                              │
 │                                                                                          │
 │  ► POSIBLE DIVISIÓN EN SUBLOTES: Si el pedido es grande, el planificador puede          │
 │    asignarlo a 2 o más máquinas en paralelo. Cada una produce un SUBLOTE independiente. │
 │    En el seguimiento aparecen como: Pedido 186432 ítem 1 — sublote A / B / C...         │
 │                                                                                          │
 │  Tabla que lo activa: ITEMPED_DET (cuando el planificador asigna el NROPROG)             │
 │  Automáticamente: sistema calcula fechas estimadas para las 14 etapas                   │
 │                   y las copia también en ITEMPED_DET (FCH_ESTIMA_TENIDO, etc.)          │
 └──────────────────────────────────────────────┬───────────────────────────────────────────┘
                                                │
                    ╔═══════════════════════════╧════════════════════════════╗
                    ║           PRODUCCIÓN EN HILANDERÍA                     ║
                    ╚═══════════════════════════╤════════════════════════════╝
                                                │
                                                ▼
 ┌──────────────────────────────────────────────────────────────────────────────────────────┐
 │  ÁREA: HILANDERÍA — Operarios de máquina                        Tiempo: 1 a 3 días       │
 │                                                                                          │
 │  PASO 03 — EN HILANDERÍA (PRODUCCIÓN INICIADA)                                          │
 │  ─────────────────────────────────────────────────────────────────────────────────────  │
 │  Qué pasa: Los operarios comienzan a producir el hilo crudo en las máquinas              │
 │            (Batan, Carda, Mechera, Continua, Retorcedora).                               │
 │            Cada parte diario de producción queda registrado en el sistema.               │
 │                                                                                          │
 │  Tabla que lo activa: H_RPRODUC (INSERT — parte diario, campo GUIA ≠ NULL)               │
 │  Cómo se llega al pedido: H_RPRODUC.GUIA → PARTIDA.NUMERO → NROPROG → ITEMPED_DET       │
 └──────────────────────────────────────────────┬───────────────────────────────────────────┘
                                                │
                                                ▼
 ┌──────────────────────────────────────────────────────────────────────────────────────────┐
 │  ÁREA: HILANDERÍA — Supervisor                                  Tiempo: ~4 horas         │
 │                                                                                          │
 │  PASO 04 — LOTE DISPONIBLE (HILO CRUDO LISTO)                                           │
 │  ─────────────────────────────────────────────────────────────────────────────────────  │
 │  Qué pasa: Se finaliza la producción y se crea la PARTIDA física en el sistema:          │
 │            el paquete de hilo crudo (madejas o conos crudos) listo para teñir.          │
 │            Se pesa, se etiqueta y queda en estado "disponible para tintorería".          │
 │            En este momento se registran los KG_PRODUCIDOS reales del sublote.            │
 │                                                                                          │
 │  Tabla que lo activa: PARTIDA (INSERT — se crea el lote, campo NROPROG ≠ NULL)           │
 │  Cómo se llega al pedido: PARTIDA.SERIE + PARTIDA.NRO_PEDIDO (directos) + NROPROG       │
 └──────────────────────────────────────────────┬───────────────────────────────────────────┘
                                                │
                    ╔═══════════════════════════╧════════════════════════════╗
                    ║           EL LOTE PASA A TINTORERÍA                    ║
                    ╚═══════════════════════════╤════════════════════════════╝
                                                │
                                                ▼
 ┌──────────────────────────────────────────────────────────────────────────────────────────┐
 │  ÁREA: LABORATORIO                                                      Tiempo: ~1 día   │
 │                                                                                          │
 │  PASO 05 — RECETA VALIDADA (LABORATORIO)                                                │
 │  ─────────────────────────────────────────────────────────────────────────────────────  │
 │  Qué pasa: El laboratorista crea y valida la receta de teñido:                           │
 │            qué colorantes usar, en qué proporción, temperatura, tiempo de baño.         │
 │            Solo cuando aprueba la receta (ESTADO = '3') el proceso puede avanzar.        │
 │                                                                                          │
 │  Tabla que lo activa: L_VALIDA_RECETA (UPDATE ESTADO → '3' = aprobada)                  │
 │  Cómo se llega al pedido: L_VALIDA_RECETA.NROPROG → ITEMPED_DET                        │
 └──────────────────────────────────────────────┬───────────────────────────────────────────┘
                                                │
                                                ▼
 ┌──────────────────────────────────────────────────────────────────────────────────────────┐
 │  ÁREA: TINTORERÍA — Operario de carga                           Tiempo: ~2 horas         │
 │                                                                                          │
 │  PASO 06 — INGRESÓ A TINTORERÍA                                                         │
 │  ─────────────────────────────────────────────────────────────────────────────────────  │
 │  Qué pasa: La PARTIDA de hilo crudo es cargada físicamente en la máquina de tintorería. │
 │            Se registra su ingreso y la situación de la partida cambia a "EN PROCESO".    │
 │            En este momento se registran los KG_EN_TIN (kg que ingresaron a TT).          │
 │                                                                                          │
 │  Tabla que lo activa: PARTIDA (UPDATE — SITU_PART cambia a 'R001' = recibida en TT)      │
 │  Cómo se llega al pedido: PARTIDA.NROPROG → ITEMPED_DET (NRO + NUM_DET)                │
 └──────────────────────────────────────────────┬───────────────────────────────────────────┘
                                                │
                                                ▼
 ┌──────────────────────────────────────────────────────────────────────────────────────────┐
 │  ÁREA: TINTORERÍA — Operarios de máquina                        Tiempo: 6 h a 24 h       │
 │                                                                                          │
 │  PASO 07 — TENIDO COMPLETO (TODOS LOS BAÑOS TERMINADOS)                                 │
 │  ─────────────────────────────────────────────────────────────────────────────────────  │
 │  Qué pasa: Se ejecutan todos los BAÑOS del proceso de teñido:                            │
 │            blanqueo → teñido principal → acabados (cada uno es un baño separado).        │
 │            Cada baño se registra en el sistema al terminar.                              │
 │                                                                                          │
 │  ► REGLA CLAVE — Baños múltiples: El 75% de las partidas tienen 2 o más baños.          │
 │    El sistema SOLO avanza a PASO 07 cuando TODOS los baños de esa partida están          │
 │    terminados (ESTADO = '3'). Si solo terminó el primero, sigue esperando.               │
 │                                                                                          │
 │  Tabla que lo activa: TT_RPRODUC (UPDATE ESTADO → '3', verifica TODOS los baños)         │
 │  Cómo se llega al pedido: TT_RPRODUC.RECETA → ING_RECETAS_G.GUIA → PARTIDA → NROPROG   │
 └──────────────────────────────────────────────┬───────────────────────────────────────────┘
                                                │
                                                ▼
 ┌──────────────────────────────────────────────────────────────────────────────────────────┐
 │  ÁREA: TINTORERÍA — Operario de secado                          Tiempo: 6 a 8 horas      │
 │                                                                                          │
 │  PASO 08 — SECADO                                                                        │
 │  ─────────────────────────────────────────────────────────────────────────────────────  │
 │  Qué pasa: El hilo húmedo (recién teñido) pasa a la secadora de radiofrecuencia          │
 │            o al horno. Se registra el ingreso con el peso del lote antes y después.      │
 │                                                                                          │
 │  Tabla que lo activa: TT_RSECADO (INSERT — se registra el secado, campo GUIA)            │
 │  Cómo se llega al pedido: TT_RSECADO.GUIA → PARTIDA.NROPROG → ITEMPED_DET              │
 └──────────────────────────────────────────────┬───────────────────────────────────────────┘
                                                │
                                                ▼
 ┌──────────────────────────────────────────────────────────────────────────────────────────┐
 │  ÁREA: CONTROL DE CALIDAD — Tintorería                          Tiempo: ~1 día           │
 │                                                                                          │
 │  PASO 09 — CONTROL DE CALIDAD TINTORERÍA                                                │
 │  ─────────────────────────────────────────────────────────────────────────────────────  │
 │  Qué pasa: El analista de calidad evalúa el lote teñido:                                 │
 │            color (tono, igualación), solidez del tinte, resistencia del hilado.          │
 │            El sistema registra el resultado (EST_EVALUACION = '32' = evaluación hecha). │
 │                                                                                          │
 │  Tabla que lo activa: CTCALIDAD_D (UPDATE EST_EVALUACION → '32' y RESULTADO)             │
 │  Cómo se llega al pedido: CTCALIDAD_D.NRO_PEDIDO + SER_PARTIDA + NROPART → ITEMPED_DET │
 │                                                                                          │
 │  ┌────────────────────────────────────────┬────────────────────────────────────────────┐ │
 │  │     RESULTADO = APROBADO               │       RESULTADO = RECHAZADO                │ │
 │  │   (códigos '01', '29' ó '21')          │           (código '30')                    │ │
 │  │                                        │                                            │ │
 │  │   ✔ PASO 09 — Aprobado / Concesionado  │   ✖ PASO 9R — Reproceso                   │ │
 │  │   · IND_REPROCESO queda en 'N'         │   · IND_REPROCESO se pone en 'S'          │ │
 │  │   · Continúa hacia PASO 10             │   · Se genera alerta tipo "REPR"           │ │
 │  │   · Tiempo: sin impacto adicional      │   · La partida VUELVE a tintorería         │ │
 │  │                                        │   · El ciclo se repite (+2 a 4 días)       │ │
 │  │                                        │   · Cuando el nuevo CC apruebe, continúa  │ │
 │  └────────────────────────────────────────┴────────────────────────────────────────────┘ │
 │                                                                                          │
 │  Frecuencia real de rechazo: ~2.7% de los lotes evaluados (dato verificado en BD)       │
 └──────────┬──────────────────────────────────────────────────────┬───────────────────────┘
            │ APROBADO                                              │ RECHAZADO (9R)
            │                                                       │
            │                       ╔══════════════════════════════╧══════════════════════╗
            │                       ║  RAMAL DE REPROCESO — PASO 9R                       ║
            │                       ║  ────────────────────────────────────────────────── ║
            │                       ║  El lote regresa a la línea de tintorería.          ║
            │                       ║  El planificador lo re-agenda en una máquina libre. ║
            │                       ║  El seguimiento muestra "EN REPROCESO" en rojo.     ║
            │                       ║  Cuando el nuevo control de calidad aprueba,        ║
            │                       ║  el flujo continúa normalmente desde PASO 09.       ║
            │                       ╚═════════════════════════════════════════════════════╝
            │
            ▼
 ┌──────────────────────────────────────────────────────────────────────────────────────────┐
 │  ÁREA: DEVANADO / ACABADO                                       Tiempo: 4 a 8 horas      │
 │                                                                                          │
 │  PASO 10 — DEVANADO (MADEJA → CONO)                                                     │
 │  ─────────────────────────────────────────────────────────────────────────────────────  │
 │  Qué pasa: El hilo aprobado (que viene en madejas) pasa por la máquina devanadora        │
 │            o enconadora. Se convierte en conos listos para usar o vender.                │
 │            El programa de devanado queda registrado en el sistema.                       │
 │                                                                                          │
 │  Tabla que lo activa: H_PROGRAMACION (INSERT — se asigna el programa de devanado)        │
 │  Nota: este paso puede solaparse con PASO 11 si devanado y revisado ocurren juntos      │
 └──────────────────────────────────────────────┬───────────────────────────────────────────┘
                                                │
                                                ▼
 ┌──────────────────────────────────────────────────────────────────────────────────────────┐
 │  ÁREA: CONTROL DE CALIDAD — Revisado final                      Tiempo: 4 horas          │
 │                                                                                          │
 │  PASO 11 — REVISADO FINAL                                                                │
 │  ─────────────────────────────────────────────────────────────────────────────────────  │
 │  Qué pasa: Los conos terminados se revisan uno a uno (o en muestra estadística):         │
 │            se verifica peso, presentación, etiquetado y cantidad aprobada final.         │
 │            Se registra cuántos conos APROBADOS pasan y cuántos se rechazan.              │
 │                                                                                          │
 │  Tabla que lo activa: REVISADO_D (INSERT — cuando APROBADO > 0)                          │
 │  Cómo se llega al pedido: REVISADO_D → REVISADO_G.GUIA → PARTIDA.NROPROG → ITEMPED_DET │
 └──────────────────────────────────────────────┬───────────────────────────────────────────┘
                                                │
                    ╔═══════════════════════════╧════════════════════════════╗
                    ║           ALMACÉN PRODUCTO TERMINADO                   ║
                    ╚═══════════════════════════╤════════════════════════════╝
                                                │
                                                ▼
 ┌──────────────────────────────────────────────────────────────────────────────────────────┐
 │  ÁREA: ALMACÉN PT                                               Tiempo: ~2 horas         │
 │                                                                                          │
 │  PASO 12 — INGRESADO A ALMACÉN PT                                                        │
 │  ─────────────────────────────────────────────────────────────────────────────────────  │
 │  Qué pasa: Los conos aprobados son ingresados físicamente al almacén de producto         │
 │            terminado. Se registra el LOTE, la cantidad en kg y el código de almacén.     │
 │            · ALM='03' = Almacén PT principal                                             │
 │            · ALM='07' = Almacén PT externo                                               │
 │            En este momento se registran los KG_EN_ALM_PT reales.                         │
 │                                                                                          │
 │  Tabla que lo activa: LOTES (INSERT — TP_TRANSAC='16', COD_ALM IN ('03','07'))           │
 │  Cómo se llega al pedido: LOTES.PARTIDA → PARTIDA.NROPROG → ITEMPED_DET                 │
 └──────────────────────────────────────────────┬───────────────────────────────────────────┘
                                                │
                                                ▼
 ┌──────────────────────────────────────────────────────────────────────────────────────────┐
 │  ÁREA: ALMACÉN PT / SISTEMA (automático)                        Tiempo: automático       │
 │                                                                                          │
 │  PASO 13 — LISTO PARA DESPACHO                                                          │
 │  ─────────────────────────────────────────────────────────────────────────────────────  │
 │  Qué pasa: El sistema detecta que hay STOCK DISPONIBLE en almacén y que el pedido        │
 │            aún tiene SALDO PENDIENTE. Nadie tiene que hacer nada manual.                 │
 │            El ítem aparece automáticamente en la "Lista de despacho" del área.           │
 │                                                                                          │
 │  Calculado por: la vista V_PLN_PENDIENTES_DESP compara stock en ALMACEN vs saldo ITEMPED│
 │  Este paso NO tiene trigger — es calculado en tiempo real por la vista                  │
 └──────────────────────────────────────────────┬───────────────────────────────────────────┘
                                                │
                                                ▼
 ┌──────────────────────────────────────────────────────────────────────────────────────────┐
 │  ÁREA: DESPACHO / EXPEDICIONES                                  Tiempo: ~2 horas         │
 │                                                                                          │
 │  PASO 14 — DESPACHADO ✔  CICLO CERRADO                                                  │
 │  ─────────────────────────────────────────────────────────────────────────────────────  │
 │  Qué pasa: El despachador registra la salida del lote con su guía de remisión.           │
 │            El lote sale del almacén hacia el cliente.                                    │
 │            · S_TRANSAC = '21' → despacho mercado nacional                               │
 │            · S_TRANSAC = '23' → despacho exportación                                    │
 │            KG_DESPACHADOS se acumula. Si KG_PENDIENTES llega a 0, el ítem se cierra.    │
 │                                                                                          │
 │  ► DESPACHO PARCIAL: Si salen 200 kg de 500 pedidos → KG_DESPACHADOS=200,               │
 │    KG_PENDIENTES=300, ESTADO sigue 'A' (Activo). El ítem NO se cierra hasta llegar a 0. │
 │                                                                                          │
 │  Tabla que lo activa: LOTES (UPDATE S_TRANSAC → '21' ó '23')                             │
 │  Cómo se llega al pedido: LOTES.PARTIDA → PARTIDA.NROPROG → ITEMPED_DET                 │
 │  Estado en PLN_SEGUIMIENTO: ESTADO = 'C' (Cerrado) cuando KG_PENDIENTES = 0             │
 └──────────────────────────────────────────────┬───────────────────────────────────────────┘
                                                │
                                                ▼
                               ┌────────────────────────────────┐
                               │   CLIENTE RECIBE SU PEDIDO ✔   │
                               └────────────────────────────────┘
```

---

## PARTE 3 — TIEMPO ESTIMADO POR ETAPA

Estos tiempos son **parámetros configurables** en el sistema (`PLN_PARAM`).  
Si la planta cambia su velocidad o contrata una máquina nueva, se ajusta sin tocar código.

| Paso | Etapa | Área responsable | Tiempo típico |
|------|-------|-----------------|---------------|
| 01 | Pedido registrado | Ventas | ~1 día |
| 02 | Planificado | Planificación | ~1 día |
| 03 | En hilandería (producción) | Hilandería — Operarios | 1 a 3 días según kg y título |
| 04 | Lote disponible (hilo crudo) | Hilandería — Supervisor | ~4 horas |
| 05 | Laboratorio (receta validada) | Laboratorio | ~1 día |
| 06 | Ingresó a tintorería | Tintorería | ~2 horas |
| 07 | Tenido completo | Tintorería — Operarios | 6 a 24 horas (varía por Nro. de baños) |
| 08 | Secado | Tintorería | 6 a 8 horas |
| 09 | Control calidad TT | Control Calidad | ~1 día |
| 9R | *(reproceso si CC rechaza)* | Tintorería + CC | +2 a 4 días extra |
| 10 | Devanado (madeja → cono) | Devanado | 4 a 8 horas |
| 11 | Revisado final | Control Calidad | ~4 horas |
| 12 | Ingreso almacén PT | Almacén PT | ~2 horas |
| 13 | Listo para despacho | *(automático)* | Inmediato (calculado) |
| 14 | Despachado | Despacho | ~2 horas |
| | **TOTAL CICLO SIN REPROCESO** | | **12 a 18 días hábiles** |
| | **TOTAL CICLO CON REPROCESO** | | **14 a 22 días hábiles** |

---

## PARTE 4 — CÓMO FUNCIONA POR DENTRO (SIN TECNICISMOS)

### La analogía: el libro de actas y el tablero de control

```
┌──────────────────────────────────────────┐     ┌──────────────────────────────────────────────┐
│  SISTEMA ANTIGUO (SIG — el que ya        │     │  SISTEMA NUEVO (Módulo PLN_)                 │
│  usan todos los empleados)               │     │                                              │
│                                          │     │                                              │
│  Es el LIBRO DE ACTAS.                   │     │  Es el TABLERO DE CONTROL.                   │
│                                          │     │                                              │
│  Registra cada operación                 │     │  Lee el libro automáticamente y              │
│  tal como ocurrió:                       │◄────│  calcula en tiempo real:                     │
│  · Qué se produjo                        │     │  · ¿En qué paso está el pedido?              │
│  · Cuántos kg                            │     │  · ¿Va a llegar a tiempo?                    │
│  · En qué máquina                        │     │  · ¿Dónde está el cuello de botella?         │
│  · A qué hora                            │     │  · ¿Qué máquinas tienen capacidad?           │
│  · Quién lo hizo                         │     │  · ¿Qué alertas hay activas ahora?           │
│                                          │     │                                              │
│  Los empleados siguen usando             │     │  Nadie tiene que hacer nada extra.           │
│  EXACTAMENTE LAS MISMAS                  │     │  El tablero se actualiza solo,               │
│  PANTALLAS DE SIEMPRE.                   │     │  cada vez que alguien hace su trabajo.       │
└──────────────────────────────────────────┘     └──────────────────────────────────────────────┘
```

### Las 3 cosas que pasan automáticamente con cada registro

Cada vez que un empleado graba algo en el sistema, **sin que él haga nada adicional**, el módulo PLN_ ejecuta tres acciones en milisegundos:

```
  EMPLEADO GRABA        ══►  1. ACTUALIZA el estado del pedido en tiempo real
  EN SISTEMA VIEJO
  (parte diario,             Ejemplo:
   CC, despacho, etc.)         Antes:  Pedido 186432 ítem 1  →  "En Tintorería"
                               El lab aprueba CC a las 08:15
                               Ahora:  Pedido 186432 ítem 1  →  "CC TT Aprobado"
                               Despacho estimado actualizado: 25/05 → 26/05

                        ══►  2. ESCRIBE en el historial del ítem (inmutable, nadie puede borrar)

                               16/05 08:15 · CC TT Aprobado  · RESULTADO=01 · Eval. Nro 4823
                               15/05 06:45 · Secado completo · 448 kg teñidos
                               14/05 16:20 · Tenido completo · 2 baños ESTADO=3
                               14/05 08:30 · Ingresó a tintorería · SITU_PART=R001
                               13/05 15:10 · Receta validada · Lab: MFERNANDEZ
                               13/05 09:45 · Lote disponible · PARTIDA 7842 · 455 kg
                               12/05 07:00 · En hilandería  · Máq: C05 CARDA 5
                               11/05 14:30 · Planificado    · NROPROG: 4892 · MLOPEZ
                               11/05 08:20 · Pedido registrado · JVEGA

                        ══►  3. GENERA ALERTA si algo no va bien

                               Si la fecha estimada de despacho supera la prometida al cliente
                               → Alerta automática para el supervisor (no hay que "darse cuenta")
                               → El supervisor la ve en su pantalla de alertas inmediatamente
```

---

## PARTE 5 — CÓMO SE CONECTA CADA TABLA VIEJA CON EL SISTEMA NUEVO

```
TABLA ANTIGUA          EVENTO QUE LO DISPARA                   QUÉ ACTUALIZA EN PLN_
═════════════          ═════════════════════════════════════    ══════════════════════════════════

ITEMPED                INSERT — vendedor graba el ítem      ──► PASO 01: crea PLN_SEGUIMIENTO
(ítems de pedido)

ITEMPED_DET            UPDATE — planificador asigna         ──► PASO 02: calcula fechas estimadas
(planificación)        NROPROG (número de programa)              y las copia en FCH_ESTIMA_* de ITEMPED_DET

H_RPRODUC              INSERT — operario graba parte        ──► PASO 03: "en hilandería"
(producción            diario (campo GUIA ≠ NULL)                Navega: GUIA → PARTIDA → NROPROG → ITEMPED_DET
 hilandería)

PARTIDA                INSERT — se crea el lote físico      ──► PASO 04: "lote disponible"
(lote de hilo crudo)   (campo NROPROG ≠ NULL)                    KG_PRODUCIDOS se acumula en seguimiento
                       SERIE + NRO_PEDIDO vienen directo
                       en PARTIDA — sin joins extras

L_VALIDA_RECETA        UPDATE — laboratorista aprueba       ──► PASO 05: "receta validada"
(recetas de teñido)    la receta (ESTADO cambia a '3')           Navega: L_VALIDA_RECETA.NROPROG → ITEMPED_DET

PARTIDA                UPDATE — operario registra           ──► PASO 06: "en tintorería"
(lote de hilo crudo)   ingreso a TT (SITU_PART = 'R001')         KG_EN_TIN se acumula en seguimiento
                                                                  Navega: PARTIDA.NROPROG → ITEMPED_DET

ING_RECETAS_G          ─── (tabla puente interna) ───       Vincula TT_RPRODUC.RECETA → PARTIDA.NUMERO
(recetas → partidas)   Se consulta para saber a qué         (no genera paso directamente)
                       PARTIDA pertenece cada baño de TT

TT_RPRODUC             UPDATE — operario completa un        ──► PASO 07: "tenido completo"
(proceso de teñido)    baño (ESTADO → '3') Y ADEMÁS               Solo avanza si NO quedan baños con
                       todos los baños de esa partida              ESTADO ≠ '3' en esa misma PARTIDA
                       están en ESTADO = '3'                       Navega: RECETA → ING_RECETAS_G.GUIA → PARTIDA → NROPROG

TT_RSECADO             INSERT — operario registra           ──► PASO 08: "secado"
(secado)               el secado (campo GUIA)                     Navega: GUIA → PARTIDA → NROPROG → ITEMPED_DET

CTCALIDAD_D            UPDATE — analista registra           ──► PASO 09: "CC aprobado"   si RESULTADO = '01'/'29'
(control calidad TT)   resultado (EST_EVALUACION = '32')    ──► PASO 9R: "CC rechazado"  si RESULTADO = '30'
                                                                  Navega: NRO_PEDIDO + SER_PARTIDA + NROPART → ITEMPED_DET

H_PROGRAMACION         INSERT — se asigna el programa       ──► PASO 10: "devanado"
(programas devanado)   de devanado (GUIA = PARTIDA)               Navega: GUIA → PARTIDA → NROPROG → ITEMPED_DET

REVISADO_D             INSERT — conos aprobados             ──► PASO 11: "revisado final"
(revisión final)       (campo APROBADO > 0)                       Navega: → REVISADO_G.GUIA → PARTIDA → NROPROG

LOTES                  INSERT — ingreso a almacén PT         ──► PASO 12: "ingresado almacén PT"
(movimiento de stock)  (TP_TRANSAC='16', ALM='03' ó '07')         KG_EN_ALM_PT se acumula
                                                                  Navega: LOTES.PARTIDA → PARTIDA.NROPROG

LOTES                  UPDATE — despacho registrado         ──► PASO 14: "despachado — CERRADO"
(movimiento de stock)  (S_TRANSAC = '21' nacional                 KG_DESPACHADOS acumula
                               ó '23' exportación)                KG_PENDIENTES se reduce
                                                                  Si llega a 0: ESTADO = 'C' (Cerrado)
                                                                  Navega: LOTES.PARTIDA → PARTIDA.NROPROG
```

> **¿Por qué ya no se usa KARDEX_G para detectar el despacho?**  
> Se verificó en la base de datos que el campo de referencia al pedido (`TIP_DOC_REF`) está vacío en más del 90% de los registros de despacho (TP='22'). La tabla `LOTES` tiene el dato confiable y directo en el campo `S_TRANSAC`.

---

## PARTE 6 — QUÉ VE CADA ÁREA (LAS PANTALLAS NUEVAS)

### Vista general — Panel maestro de seguimiento

```
┌──────────────────────────────────────────────────────────────────────────────────────────┐
│  SEGUIMIENTO DE PEDIDOS EN TIEMPO REAL                        [Actualizado: 18/05 09:41] │
├────────┬──────────┬────────────────────────┬──────────────────────────┬────┬─────────────┤
│ PEDIDO │ CLIENTE  │ ARTÍCULO               │ ESTADO ACTUAL            │ KG │ SEMÁFORO    │
├────────┼──────────┼────────────────────────┼──────────────────────────┼────┼─────────────┤
│ 186432 │ CLIENTE A│ HILADO PEINADO 30/1 CR │ 09 · CC TT Aprobado      │ 450│ 🟢 A TIEMPO │
│ 186415 │ CLIENTE B│ HILADO CARD. 20/1 AZUL │ 07 · Tenido Completo     │ 280│ 🟡 ATENCIÓN │
│ 186398 │ CLIENTE C│ MEZCLA 50/1 BLANCO     │ 06 · En Tintorería       │ 620│ 🔴 RETRASO  │
│ 186385 │ CLIENTE D│ HILADO PEINADO 40/1 CR │ 9R · EN REPROCESO        │ 190│ 🔴 RETRASO  │
│ 186355 │ CLIENTE A│ HILADO CARD. 16/1 NATU │ 14 · Despachado ✔        │ 800│ ✔  CERRADO  │
└────────┴──────────┴────────────────────────┴──────────────────────────┴────┴─────────────┘
[Ver detalle]  [Ver historial]  [Ver alertas]  [Exportar]
```

### Pantalla de detalle — Línea de tiempo del ítem

```
Pedido 186432  ·  Ítem 1  ·  Hilado Peinado 30/1 Crudo  ·  450 kg  ·  Cliente A

PASOS COMPLETADOS         FECHA REAL    FECHA ESTIMADA  DIF.
─────────────────────────────────────────────────────────────
✔ 01 · Pedido registrado   11/05/26      11/05/26         0 días
✔ 02 · Planificado         11/05/26      11/05/26         0 días
✔ 03 · En hilandería       12/05/26      12/05/26         0 días
✔ 04 · Lote disponible     13/05/26      13/05/26         0 días  · 455 kg crudo
✔ 05 · Receta validada     13/05/26      13/05/26         0 días
✔ 06 · En tintorería       14/05/26      14/05/26         0 días  · 450 kg
✔ 07 · Tenido completo     14/05/26      15/05/26        -1 día   ← adelantado ✔
✔ 08 · Secado              15/05/26      15/05/26         0 días
✔ 09 · CC TT Aprobado      16/05/26      16/05/26         0 días  · RESULTADO=01
  ── ── ── ── ── ── ── ── ── ── ── ── ── ── ── ── ── ── ── ──
→ 10 · Devanado            PENDIENTE     17/05/26
→ 11 · Revisado            PENDIENTE     17/05/26
→ 12 · Almacén PT          PENDIENTE     18/05/26
→ 13 · Listo despacho      PENDIENTE     18/05/26
→ 14 · DESPACHO            PENDIENTE     19/05/26  ← Comprometido: 20/05 ✔ A TIEMPO
```

### Por área — ¿qué ven ellos en particular?

| Área | Su pantalla principal | Para qué la usan |
|------|-----------------------|-----------------|
| **Ventas / Comercial** | Panel de pedidos filtrado por cliente | Responder al cliente cuándo llega su pedido, sin llamar a planta |
| **Planificación** | Carga de máquinas — calendario 30 días | Decidir dónde asignar un pedido nuevo sin colapsar una máquina ya llena |
| **Supervisores de planta** | Panel de alertas (retrasos, reprocesos) | Ver qué pedidos necesitan atención HOY, sin depender de reportes del día siguiente |
| **Control de Calidad** | Pedidos en CC pendientes / historial de rechazos | Priorizar qué evaluar primero; ver % de reproceso por artículo |
| **Almacén / Despacho** | Lista de pendientes de despacho (priorizada) | Los urgentes arriba; saber exactamente qué lotes están disponibles para cada pedido |
| **Gerencia** | KPI mensual OTIF + ciclo promedio + reprocesos % | Tomar decisiones de capacidad con datos reales, no con intuición |

---

## PARTE 7 — QUÉ CAMBIA Y QUÉ NO CAMBIA

### Lo que NO cambia para ningún empleado

- Las pantallas de ingreso de ventas (pedidos) → **exactamente igual**
- Los partes diarios de hilandería → **exactamente igual**
- Las pantallas de tintorería (recetas, procesos, secado) → **exactamente igual**
- El control de calidad (CTCALIDAD_D) → **exactamente igual**
- El kardex y despacho de almacén → **exactamente igual**

> **Punto clave para convencer al usuario:** No hay pantallas nuevas que aprender para hacer el trabajo diario. El módulo PLN_ trabaja en segundo plano, de forma invisible, leyendo lo que ya se graba.

### Lo que SÍ cambia (se agrega)

| Área | Qué nuevo tienen | Valor para ellos |
|------|-----------------|-----------------|
| **Ventas** | Consulta estado del pedido en tiempo real | Pueden responder al cliente en segundos, no en horas |
| **Planificación** | Panel de carga de máquinas | Asignan nuevos pedidos con datos reales de capacidad |
| **Supervisores** | Alertas automáticas de retraso | El problema llega a ellos antes, no después |
| **Calidad** | Historial de reprocesos por artículo/cliente | Pueden detectar patrones de rechazo que antes no eran visibles |
| **Gerencia** | KPI de cumplimiento (OTIF) con datos reales | Decisiones de inversión/capacidad basadas en hechos |

---

## PARTE 8 — PREGUNTAS FRECUENTES

**P: ¿Los operarios de piso tienen que aprender algo nuevo?**
> No. Siguen usando las mismas pantallas. El módulo PLN_ trabaja en segundo plano, de forma completamente invisible para ellos.

**P: ¿Qué pasa si alguien registra mal un dato?**
> El seguimiento refleja lo que hay en la base de datos. Si hay un error en el origen (un parte mal cargado, un kg incorrecto), el seguimiento lo muestra igual. La calidad del dato sigue siendo responsabilidad del área que lo ingresa. El módulo no inventa ni corrige información.

**P: ¿Qué pasa exactamente si una partida va a reproceso?**
> El analista de calidad registra el resultado '30' (rechazado) en CTCALIDAD_D. En ese momento el sistema activa automáticamente el PASO 9R: el ítem queda marcado "EN REPROCESO", se genera una alerta para el supervisor y el planificador puede re-agendar el lote. Cuando el nuevo ciclo de calidad apruebe, el flujo continúa normalmente desde PASO 09. Aproximadamente el 2.7% de los lotes van por este camino.

**P: ¿Podemos cambiar la fecha prometida al cliente si hay demoras?**
> Sí. Hay un formulario específico para eso. El comercial ingresa la nueva fecha y escribe el motivo. El sistema recalcula si sigue habiendo retraso o no, y actualiza o cierra las alertas correspondientes.

**P: ¿Cómo sé qué máquinas de tintorería tienen capacidad para la semana que viene?**
> Con la pantalla de "Carga de Máquinas". Muestra un calendario de 30 días por máquina, con el porcentaje de ocupación real. Verde = disponible. Amarillo = casi llena. Rojo = saturada.

**P: Un pedido se dividió en dos sublotes que van a máquinas diferentes. ¿Cómo se ve?**
> Aparecen como dos filas separadas en el seguimiento: "Pedido 186432 ítem 1 — sublote A" y "sublote B". Cada uno avanza de forma independiente. En el resumen del pedido se ve el total combinado de kg producidos, despachados y pendientes.

**P: ¿Y si el sistema nuevo tiene un error de programación?**
> Todos los triggers tienen manejo de excepciones: si algo falla en el módulo PLN_, el error se captura en silencio y la operación original (el parte diario, el control de calidad, el despacho) se graba normalmente. El módulo PLN_ **nunca puede bloquear el trabajo de planta**. Este fue un requisito de diseño crítico.

**P: ¿Cuánto tiempo lleva implementar esto?**
> El plan es 6 semanas — divididas en 4 fases que se ejecutan sin detener la planta:
> - Semana 1-2: Crear las tablas y cargar el historial de pedidos activos
> - Semana 3-4: Instalar los 12 triggers automáticos de captura de eventos
> - Semana 5: Configurar alertas y jobs de recálculo nocturno
> - Semana 6: Pantallas .NET + validación con usuarios

---

## PARTE 9 — RESUMEN EJECUTIVO (para presentar en 5 minutos)

```
╔══════════════════════════════════════════════════════════════════════════════════════╗
║  EL PROBLEMA HOY                                                                    ║
╠══════════════════════════════════════════════════════════════════════════════════════╣
║                                                                                     ║
║  Un pedido pasa por 14 etapas, manejadas por 6 áreas diferentes.                   ║
║  Cada área tiene su propia pantalla. Nadie tiene la imagen completa.               ║
║                                                                                     ║
║  Resultado: los retrasos se detectan tarde, los clientes llaman sin recibir        ║
║  respuesta concreta, y las decisiones se toman en base a llamadas telefónicas       ║
║  en lugar de datos reales.                                                          ║
║                                                                                     ║
╠══════════════════════════════════════════════════════════════════════════════════════╣
║  LA PROPUESTA                                                                       ║
╠══════════════════════════════════════════════════════════════════════════════════════╣
║                                                                                     ║
║  No reemplazar nada. Agregar una capa de inteligencia encima de lo que ya existe.  ║
║                                                                                     ║
║  Cada vez que un empleado hace su trabajo normal en el sistema, el módulo PLN_     ║
║  captura ese evento en milisegundos, actualiza el estado del pedido, y — si algo   ║
║  va mal — genera una alerta automática antes de que sea demasiado tarde.            ║
║                                                                                     ║
╠══════════════════════════════════════════════════════════════════════════════════════╣
║  EL RESULTADO                                                                       ║
╠══════════════════════════════════════════════════════════════════════════════════════╣
║                                                                                     ║
║  ✔ Un solo lugar para ver el estado de todos los pedidos en tiempo real            ║
║  ✔ 14 etapas monitoreadas: hilandería → TT → devanado → almacén → despacho        ║
║  ✔ Reprocesos detectados al instante — el PASO 9R no se "descubre" al día siguiente║
║  ✔ Alertas automáticas de retraso antes de que el cliente llame a preguntar        ║
║  ✔ Lista de despacho priorizada — los urgentes siempre al tope                     ║
║  ✔ KPI de cumplimiento OTIF visible para gerencia con datos reales de BD           ║
║  ✔ Historial completo e inmutable de cada pedido: qué pasó, cuándo, en qué tabla  ║
║  ✔ Nadie cambia su forma de trabajar — el módulo es invisible para los operarios  ║
║  ✔ Si el módulo falla por cualquier motivo, la planta sigue operando sin cortes   ║
║                                                                                     ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

---

*Documento de presentación · 18 de Mayo 2026 · Para el detalle técnico completo ver `Propuesta.md`*
