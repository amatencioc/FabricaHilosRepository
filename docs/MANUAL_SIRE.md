# 📘 Manual de Capacitación — Módulo SIRE
### Arquitectura, flujo de negocio y uso de GitHub / Copilot en el desarrollo

> Documento preparado para transferir conocimiento al equipo sobre cómo se desarrolló el módulo SIRE
> (integración SUNAT RVIE/RCE) y cómo se utilizó GitHub + GitHub Copilot durante su construcción.

---

## Índice

1. [Objetivo del manual](#1-objetivo-del-manual)
2. [Arquitectura general del proyecto](#2-arquitectura-general-del-proyecto)
3. [Cómo se usó GitHub + Copilot en el desarrollo](#3-cómo-se-usó-github--copilot-en-el-desarrollo)
   - 3.1 Control de versiones con ramas y Pull Requests
   - 3.2 GitHub Copilot como asistente de código (Copilot Chat / inline)
   - 3.3 Coding Agent (Copilot Workspace / Agente autónomo)
   - 3.4 Prácticas de trazabilidad y calidad reflejadas en el código
4. [Buenas prácticas de desarrollo reflejadas en el código](#4-buenas-prácticas-de-desarrollo-reflejadas-en-el-código)
   - 4.1 Comentarios `TODO` y atributos `[Obsolete]`
   - 4.2 Logging estructurado con prefijos (`[SIRE]`, `[SIRE-AUTH]`, `[SIRE-WORKER]`, etc.)
   - 4.3 Separación estricta de responsabilidades (librería API vs. Intranet)
5. [Agenda sugerida de capacitación](#5-agenda-sugerida-de-capacitación)

---

## 1. Objetivo del manual

Dejar documentado, en profundidad, **cómo se trabajó con GitHub y GitHub Copilot** durante el desarrollo
del módulo SIRE, de manera que el equipo que se queda pueda:

- Entender el flujo de trabajo con ramas y Pull Requests.
- Usar Copilot Chat / autocompletado para acelerar tareas de mantenimiento.
- Delegar tareas completas al Coding Agent (crear PRs automáticamente).
- Reconocer y **replicar** los patrones de calidad que ya existen en el código (logging, manejo de
  deprecaciones, separación de capas), en vez de romperlos sin darse cuenta.

---

## 2. Arquitectura general del proyecto

El repositorio contiene **dos proyectos independientes** que colaboran entre sí:

| Proyecto | Rol | Ejemplos |
|---|---|---|
| `FabricaHilos.Sire` | Librería/"API" de integración con SUNAT (HTTP, OAuth2, DTOs, endpoints). | `SireAuthService.cs`, `SireVentasService.cs`, `SireComprasService.cs`, `SireEndpoints.cs` |
| `FabricaHilos` (Intranet) | Aplicación MVC que **consume** la librería SIRE. Controladores, vistas, workers, repositorio Oracle. | `SireController.cs`, `SireExportacionWorker.cs`, `SireTicketWatcherWorker.cs` |

```
Intranet (FabricaHilos)  →  consume  →  FabricaHilos.Sire (cliente API SUNAT)  →  SUNAT (RVIE/RCE)
       ↓
   Oracle (SIRE_JOB, SIRE_LOG, SIRE_PROPUESTA, SIRE_LEGACY, SIRE_CONCIL)
```

---

## 3. Cómo se usó GitHub + Copilot en el desarrollo

Esta sección es el corazón de la capacitación: explica, **paso a paso y con ejemplos reales**, cada
herramienta usada.

### 3.1 Control de versiones con ramas y Pull Requests

**¿Qué es y para qué sirve?**
Cada funcionalidad nueva o corrección del módulo SIRE se desarrolló en su **propia rama** (branch),
nunca directamente sobre `main`. Esto evita que un cambio a medio terminar rompa la versión estable
que está en producción.

**Flujo real usado en este proyecto:**

1. Se identifica una tarea (ej. "agregar worker de vigilancia de tickets SUNAT").
2. Se crea una rama descriptiva, por ejemplo: `feature/sire-ticket-watcher` o `fix/sire-auth-token-cache`.
3. Se desarrolla el código en esa rama, haciendo commits pequeños y descriptivos
   (ej. `feat: agrega SireTicketWatcherWorker con polling cada 15 min`).
4. Se abre un **Pull Request (PR)** hacia `main`, describiendo:
   - Qué problema resuelve.
   - Qué archivos toca.
   - Cómo se probó (modo mock/stub, sin llamar a SUNAT real).
5. El PR se revisa (code review) antes de aprobarse y mezclarse (merge).
6. Una vez aprobado, se hace **merge** a `main` y la rama se puede eliminar.

**¿Por qué es importante enseñarlo?**
- Permite trabajar en paralelo sin pisarse el trabajo entre compañeros.
- El historial de PRs sirve como documentación viva: cada PR explica el "por qué" de un cambio,
  no solo el "qué".
- Si algo falla en producción, es fácil identificar en qué PR/commit se introdujo el problema
  (`git bisect`, historial de commits).

**Ejemplo concreto en este repo:**
El worker `SireTicketWatcherWorker.cs` tiene un comentario de cabecera muy detallado explicando su
flujo completo (Fase 2 del polling inteligente). Ese nivel de detalle en el código es el que se espera
también en la descripción de un PR: contexto suficiente para que otra persona entienda la decisión
sin tener que preguntar directamente al autor.

> 💡 **Tip para la práctica:** muéstrale al equipo cómo crear una rama, hacer un cambio pequeño
> (por ejemplo agregar un log en `SireController.cs`), subir el commit y abrir el PR, todo desde
> la interfaz de GitHub o VS Code.

---

### 3.2 GitHub Copilot como asistente de código (Copilot Chat / inline)

**¿Qué es?**
GitHub Copilot es un asistente de IA integrado en el editor (VS Code, Visual Studio, JetBrains) que:

- **Autocompleta código** mientras se escribe (sugerencias en línea, "ghost text").
- Permite conversar con **Copilot Chat** dentro del editor para pedir explicaciones, refactors,
  generación de pruebas, o resolución de errores.

**Usos concretos que se le dieron en el desarrollo de SIRE:**

1. **Generación de clases repetitivas (boilerplate):**
   Servicios como `SireVentasService.cs` y `SireComprasService.cs` tienen una estructura casi idéntica
   (heredan de `SireServiceBase`, implementan una interfaz, exponen `ObtenerPeriodosAsync`,
   `ExportarPropuestaAsync`, etc.). Se usó Copilot para generar el segundo servicio (Compras) a partir
   del primero (Ventas), pidiéndole explícitamente que respetara el mismo patrón pero adaptado a RCE
   en lugar de RVIE.

   > Ejemplo de prompt usado: *"Genera SireComprasService.cs siguiendo exactamente el mismo patrón de
   > SireVentasService.cs, pero usando los endpoints de RCE (compras) en vez de RVIE (ventas)."*

2. **Documentación XML (`<summary>`):**
   Casi todas las clases y métodos del proyecto tienen comentarios `<summary>` explicando qué hacen,
   de dónde viene la información (ej. "servicio 5.17 del manual SUNAT v25 pág. 46"), y qué parámetros
   esperan. Estos comentarios se generaron y refinaron con Copilot Chat, pidiéndole que documentara
   métodos existentes sin comentarios.

   > Ejemplo real (`SireEndpoints.cs`):
   > ```csharp
   > /// 5.17 SIRE: descarga el archivo generado (ZIP de propuesta, constancia, etc.). Manual v25 pág 46.
   > /// Parámetros obligatorios según el manual:
   > /// - nomArchivoReporte: de registros[0].archivoReporte[0].nomArchivoReporte (servicio 5.16)
   > ```

3. **Refactors guiados por instrucciones del proyecto:**
   Cuando se necesitaba modificar un patrón en varios archivos a la vez (por ejemplo, cambiar cómo se
   registran los logs de auditoría en `SIRE_LOG`), se usó Copilot Chat con contexto de varios archivos
   abiertos para que propusiera cambios consistentes en todos ellos, respetando las reglas ya
   establecidas del proyecto (convenciones de nombres, manejo de excepciones, etc.).

4. **Explicación de código heredado:**
   Ante código legado o poco claro (por ejemplo, el manejo de `[Obsolete]` en los métodos
   `ObtenerPropuestaAsync`), se le pidió a Copilot Chat que explicara qué hacía el método y por qué
   estaba marcado como obsoleto, acelerando el entendimiento antes de decidir si modificarlo o no.

**¿Cómo practicarlo con el equipo?**
- Abrir `SireController.cs` en el editor, seleccionar un método, y pedirle a Copilot Chat:
  *"Explícame qué hace este método paso a paso"*.
- Pedirle que genere un test unitario básico para `SireValidaService.CargarDesdeZipAsync`.
- Pedirle que sugiera mejoras de manejo de errores en un bloque `try/catch` existente.

---

### 3.3 Coding Agent (Copilot Workspace / Agente autónomo)

**¿Qué es?**
Es una capacidad más avanzada que el autocompletado: en vez de sugerir código línea por línea, se le
describe una **tarea completa en lenguaje natural** (un "problem statement") y el agente:

1. Explora el repositorio por su cuenta (lee archivos relevantes, entiende el contexto).
2. Escribe el código necesario en una rama nueva.
3. Abre automáticamente un **Pull Request** con los cambios, listo para revisión humana.

**Ejemplo de cómo se pudo usar en este proyecto:**

> Tarea delegada al agente: *"Agrega un nuevo endpoint en SireController que permita reprocesar
> localmente un ZIP ya descargado sin volver a contactar a SUNAT, reutilizando SireValidaService.
> Debe registrar la operación en SIRE_LOG con Operacion = 'REPROCESAR'."*

El agente:
- Busca en el repo dónde están definidas las operaciones (`SireOperacion.Reprocesar` en `SireApiLog.cs`,
  que ya existía preparado para este caso).
- Entiende el patrón de los demás endpoints del controlador (`[HttpPost]`, `[ValidateAntiForgeryToken]`,
  manejo de excepciones con `_logger.LogError`, retorno `Json(...)`).
- Genera el nuevo método siguiendo ese mismo patrón.
- Abre un PR con la descripción de lo que hizo, para que el equipo lo revise antes de aprobar.

**¿Por qué es importante que el equipo lo conozca?**
- Permite delegar tareas bien definidas y repetitivas (nuevos endpoints similares a otros existentes,
  reportes, correcciones puntuales) sin tener que escribir todo el código a mano.
- El resultado **siempre debe revisarse como cualquier otro PR** — el agente no reemplaza el criterio
  humano, acelera el primer borrador.
- Es ideal para tareas que se pueden describir claramente: "agrega X siguiendo el patrón de Y",
  "corrige el bug reportado en el issue #N", "agrega logging a este flujo".

**Cómo se activa (flujo típico):**
1. Se describe la tarea (puede ser desde un Issue de GitHub, asignándolo a Copilot, o desde una
   conversación con el agente).
2. El agente trabaja de forma autónoma en segundo plano.
3. Se recibe una notificación cuando el PR está listo.
4. El equipo revisa el PR como cualquier otro: lee el diff, corre pruebas, comenta, aprueba o pide
   cambios.

---

### 3.4 Prácticas de trazabilidad y calidad reflejadas en el código

Además de las herramientas, es clave que el equipo entienda **qué reglas de calidad ya están
establecidas** en el proyecto, para que Copilot (o cualquier desarrollador) las siga consistentemente:

- Todo cambio importante en el flujo de negocio (autenticación, exportación, descarga) debe quedar
  registrado en `SIRE_LOG` mediante `SireApiLog` / `ISireOracleRepository.InsertApiLogAsync`.
- Los mensajes de log deben usar los prefijos ya establecidos (ver sección 4.2) para poder filtrarlos
  fácilmente en producción.
- Los métodos deprecated no se eliminan sin más: se marcan `[Obsolete(...)]` con una explicación clara
  del reemplazo (ver sección 4.1), para no perder el contexto histórico.

---

## 4. Buenas prácticas de desarrollo reflejadas en el código

### 4.1 Comentarios `TODO` y atributos `[Obsolete]`

**¿Por qué existen?**
Durante el desarrollo se descubrió que ciertos endpoints documentados por SUNAT en versiones antiguas
del manual **ya no funcionan** (retornan HTTP 500) o que ciertas funcionalidades (como el envío real a
la API de SUNAT al aceptar una propuesta) **aún no debían activarse en producción**. En vez de borrar
el código o dejarlo sin explicación, se documentó explícitamente el motivo y el estado.

**Ejemplo 1 — Método marcado obsoleto con instrucciones de reemplazo:**

```csharp
/// <summary>
/// ⚠️ DEPRECATED: Obtiene los registros de compras de la propuesta para un periodo.
/// El endpoint original (/registroslibros/{periodo}/cabecera) no está documentado en manual v25
/// y retorna HTTP 500 en producción.
///
/// USO CORRECTO: Para obtener registros, use el flujo:
/// 1. ExportarPropuestaAsync(periodo) → obtiene TicketEstado
/// 2. TicketPollingHelper.EsperarEstadoFinalAsync() → espera procesamiento
/// 3. DescargarConstanciaAsync(nomArchivo) → descarga archivo ZIP resultante
/// 4. Descomprimir y procesar archivo plano con registros
/// </summary>
[Obsolete("Endpoint no documentado en SUNAT manual v25. Use ExportarPropuestaAsync() + " +
          "TicketPollingHelper + DescargarConstanciaAsync() en su lugar. Este método retorna HTTP 500.", false)]
public Task<IReadOnlyList<RegistroCompra>> ObtenerPropuestaAsync(string periodo, CancellationToken cancellationToken = default)
{
    // ❌ Endpoint INCORRECTO: no documentado en manual v25, retorna 500 en producción
    // Mantenido solo para referencia histórica
    var endpoint = $"/libros/rce/propuesta/web/registroslibros/{periodo}/cabecera";
    return SendAsync<IReadOnlyList<RegistroCompra>>(HttpMethod.Get, endpoint, null, cancellationToken);
}
```

**Lección para el equipo:**
- El atributo `[Obsolete(...)]` hace que el compilador emita una **advertencia visible** cada vez que
  alguien intente usar ese método, evitando errores por desconocimiento.
- El mensaje del atributo siempre debe explicar **qué usar en su lugar**, no solo decir "no usar esto".
- El comentario `<summary>` conserva el contexto histórico (por qué existe, por qué ya no sirve),
  útil para auditorías futuras o para entender decisiones tomadas por SUNAT.

**Ejemplo 2 — TODO pendiente de habilitar una integración real:**

```csharp
// ── 3. API SUNAT — COMENTADO (pendiente de habilitar) ─────────────
// TODO: descomentar cuando se quiera enviar realmente a SUNAT.
```

**Lección para el equipo:**
- Un `TODO` bien escrito indica **qué falta**, **cuándo debe activarse** y, si aplica, **qué
  condición** debe cumplirse antes de hacerlo (en este caso: habilitar el envío real a SUNAT al
  aceptar una propuesta RVIE/RCE, que hoy está deshabilitado intencionalmente).
- Antes de "descomentar" cualquier bloque marcado como TODO, se debe validar con el resto del equipo
  el impacto en producción (en este caso, empezaría a enviar datos reales a SUNAT).

---

### 4.2 Logging estructurado con prefijos (`[SIRE]`, `[SIRE-AUTH]`, `[SIRE-WORKER]`, etc.)

**¿Por qué se hace así?**
El módulo SIRE tiene múltiples componentes trabajando en paralelo (controlador web, workers en
background, servicios de autenticación). Si todos escribieran logs sin distinción, sería muy difícil
diagnosticar un problema en producción. Por eso, **cada componente tiene su propio prefijo fijo**
en los mensajes de log.

**Prefijos usados en el proyecto y su significado:**

| Prefijo | Componente | Cuándo aparece |
|---|---|---|
| `[SIRE]` | Flujo general / controlador | Inicialización lazy, operaciones generales del controlador |
| `[SIRE-AUTH]` | `SireAuthService` | Obtención/renovación de token OAuth2, advertencias de expiración |
| `[SIRE-WORKER]` | `SireExportacionWorker` | Procesamiento de la cola de exportaciones |
| `[SIRE-WATCHER]` | `SireTicketWatcherWorker` | Ciclos de vigilancia de tickets pendientes |
| `[SIRE-PROP]` | `SireValidaService` | Carga/parseo de propuestas ZIP/TXT |
| `[SIRE-ZIP]` | `SirePropuestaZipService` | Generación de ZIP local de propuesta |
| `[SIRE-SSCO-AUTO]` | Descarga automática del padrón SSCO | Proceso de scraping/descarga del Excel de SUNAT |

**Ejemplo real (`SireAuthService.cs`):**

```csharp
_logger.LogWarning("[SIRE-AUTH] ⚠️ Token próximo a expirar en {Minutos:F1} minutos. Renovación inminente en próxima petición.",
    minutosRestantes);
```

**Ejemplo real (`SireTicketWatcherWorker.cs`):**

```csharp
_logger.LogInformation(
    "[SIRE-WATCHER] Esperando inicialización de SIRE. Si no hay actividad en 2 min, se auto-inicializará.");
```

**Lección para el equipo:**
- Al agregar un componente nuevo dentro del módulo SIRE, se debe **elegir un prefijo consistente**
  y usarlo en todos los mensajes de ese componente.
- Esto permite, en producción, **filtrar los logs por prefijo** (por ejemplo en un visor de logs o en
  la vista de Monitoreo del propio módulo) para aislar rápidamente el origen de un problema.
- Los niveles de log también se usan con criterio:
  - `LogInformation`: eventos normales del flujo (ej. "ticket recibido", "ZIP generado").
  - `LogWarning`: situaciones anómalas pero no bloqueantes (ej. "token por expirar", "SUNAT devolvió
    HTML en vez de Excel").
  - `LogError`: errores que impiden completar la operación, siempre con la excepción (`ex`) como
    primer parámetro para conservar el stack trace.

---

### 4.3 Separación estricta de responsabilidades (librería API vs. Intranet)

**¿Por qué se separó en dos proyectos?**

| Responsabilidad | Dónde vive | Por qué |
|---|---|---|
| Hablar HTTP con SUNAT (auth, exportar, descargar) | `FabricaHilos.Sire` | Es lógica pura de integración, reutilizable, sin dependencias de Oracle ni de la web. |
| Guardar resultados, mostrar vistas, correr workers, exponer endpoints al usuario | `FabricaHilos` (Intranet) | Es lógica de negocio y presentación específica de la empresa. |

**Regla de oro que se siguió:**
> `FabricaHilos.Sire` **nunca** debe conocer Oracle, ni `HttpContext`, ni nada de la Intranet.
> Solo conoce SUNAT y sus propios DTOs/opciones.

**Evidencia en el código:**
- `SireAuthService`, `SireVentasService`, `SireComprasService` (en `FabricaHilos.Sire`) solo reciben
  `HttpClient`, `IOptions<SireOptions>`, `IMemoryCache` y `ILogger` — **nada de Oracle**.
- `ISireOracleRepository`, `SireValidaService`, `SirePropuestaZipService` (en `FabricaHilos`, Intranet)
  son los que sí conocen la base de datos (`SIG.SIRE_PROPUESTA`, `SIRE_LOG`, etc.) y el sistema de
  archivos (guardar ZIP en ruta de red).
- El `SireController.cs` es el único punto que **combina** ambos mundos: llama a los servicios de
  `FabricaHilos.Sire` para hablar con SUNAT, y a los servicios de `FabricaHilos` para persistir y
  mostrar resultados.

**Lección para el equipo:**
- Si en el futuro se necesita cambiar de proveedor de base de datos, o exponer la librería SIRE a otro
  sistema distinto de esta Intranet, **no habría que tocar `FabricaHilos.Sire` en absoluto**: solo se
  reemplazaría la capa de consumo.
- Al agregar código nuevo, siempre preguntarse: *"¿esto es integración con SUNAT o es lógica de
  negocio/persistencia de la empresa?"* — y ubicarlo en el proyecto correspondiente.

---

## 5. Agenda sugerida de capacitación

| Sesión | Contenido |
|---|---|
| **Día 1** | Arquitectura general (los 2 proyectos), flujo de negocio RVIE/RCE, recorrido guiado por el código (secciones 2 y 4 de este manual). |
| **Día 2** | Uso de GitHub (ramas, commits, PRs) + demo en vivo de Copilot Chat generando código y documentación (sección 3.1 y 3.2). |
| **Día 3** | Demo del Coding Agent: delegar una tarea real y revisar el PR resultante en equipo (sección 3.3). Cierre con preguntas y práctica libre. |

---

### Referencias rápidas de archivos clave

- `FabricaHilos.Sire/Services/SireAuthService.cs`
- `FabricaHilos.Sire/Services/SireVentasService.cs`
- `FabricaHilos.Sire/Services/SireComprasService.cs`
- `FabricaHilos.Sire/Constants/SireEndpoints.cs`
- `FabricaHilos/Controllers/Contabilidad/SireController.cs`
- `FabricaHilos/Services/Sire/SireExportacionWorker.cs`
- `FabricaHilos/Services/Sire/SireTicketWatcherWorker.cs`
- `FabricaHilos/Services/Sire/SireValidaService.cs`
- `FabricaHilos/Services/Sire/SirePropuestaZipService.cs`
- `FabricaHilos/Models/Sire/SireApiLog.cs`
- `FabricaHilos/Views/Contabilidad/Sire/_SireNavBar.cshtml`
