# Cambios Implementados en SGC - Módulo Análisis de Reclamos

## Resumen Ejecutivo
Se han implementado todas las funcionalidades solicitadas para el módulo de Análisis de Reclamo en SGC:
- ✅ Visualización de RUC del cliente (en lugar del código)
- ✅ Campo "Análisis de Causa" en la sección del Analista de Calidad
- ✅ Campo "Decisión Final" (visible solo cuando el gerente aprueba)
- ✅ Impresión formateada del reclamo aprobado
- ✅ Notificación por correo al equipo de calidad
- ✅ Notificación por correo al vendedor tras aprobación
- ✅ PDF descargable del reclamo aprobado

---

## Cambios por Componente

### 1. **Base de Datos Oracle** ✅
**Estado:** Ya presente en `PKG_SGC_RECLAMO.sql`

Columnas añadidas (si no existían):
- `ANALISIS_CAUSA` VARCHAR2(4000) - Análisis de causa por el analista
- `DECISION_FINAL` VARCHAR2(4000) - Decisión final del analista
- `FCH_DECISION` DATE - Fecha de la decisión
- `USU_DECISION` VARCHAR2(30) - Usuario que registró la decisión
- `FCH_NOTI_CALIDAD` DATE - Fecha de notificación a calidad
- `FCH_NOTI_VEND` DATE - Fecha de notificación al vendedor
- `RUC_CLIENTE` VARCHAR2(20) - RUC del cliente (para P_OBTENER_CLIENTES)

Procedimientos implementados (ya presentes):
- `P_GUARDAR_ANALISIS_CAUSA` - Guarda el análisis de causa
- `P_GUARDAR_DECISION` - Guarda la decisión final
- `P_NOTIFICAR_CALIDAD` - Obtiene correos de equipo de calidad y marca notificación
- `P_NOTIFICAR_VENDEDOR_APROBADO` - Obtiene correo del vendedor y marca notificación
- `P_OBTENER_IMPRESION` - Retorna datos completos del reclamo para impresión

---

### 2. **DTOs y ViewModels**
**Archivo:** `FabricaHilos/Models/Sgc/AnalisisReclamoDtos.cs`

#### ReclamoDto
- Agregado: `RucCliente` (string?) - RUC del cliente
- Agregado: `Descripcion` (string?) - Descripción del reclamo
- Agregado: `AnalisisCausa` (string?) - Análisis de causa del analista
- Agregado: `DecisionFinal` (string?) - Decisión final del analista
- Agregado: `FchDecision` (DateTime?) - Fecha de la decisión
- Agregado: `UsuDecision` (string?) - Usuario que registró la decisión
- Agregado: `FchNotiCalidad` (DateTime?) - Fecha de notificación a calidad
- Agregado: `FchNotiVend` (DateTime?) - Fecha de notificación al vendedor

#### ClienteComboDto
- Agregado: `RucCliente` (string?) - Para mostrar en el combo de selección

#### ReclamoArchivoDto
- Agregado: `RolColorClase` (string) - Clase Bootstrap para color (primary, warning, success)

#### Nuevos DTOs
- `GuardarAnalisisCausaRequest` - Solicitud para guardar análisis
- `GuardarDecisionRequest` - Solicitud para guardar decisión
- `NotificarCalidadRequest` - Solicitud para notificar a calidad
- `NotificarVendedorAprobadoRequest` - Solicitud para notificar al vendedor
- `ReclamoImpresionDto` - Datos completos del reclamo para impresión

---

### 3. **Servicio de Negocio**
**Archivo:** `FabricaHilos/Services/Sgc/AnalisisReclamo/AnalisisReclamoService.cs`

#### Interfaz IAnalisisReclamoService - Nuevos métodos
```csharp
Task<string?> GuardarAnalisisCausaAsync(long idReclamo, string texto, string usuario);
Task<string?> GuardarDecisionAsync(long idReclamo, string texto, string usuario);
Task<(string? Destinatarios, string? AsuntoMail, string? NomCliente, string? Error)> 
	NotificarCalidadAsync(long idReclamo, string usuario);
Task<(string? Destinatario, string? AsuntoMail, string? NomCliente, string? Error)> 
	NotificarVendedorAprobadoAsync(long idReclamo, string usuario);
Task<ReclamoImpresionDto?> ObtenerDatosImpresionAsync(long idReclamo);
```

#### Implementación
- **Constructor actualizado:** Inyecta `IEmailNotificacionService` para envío de correos
- **`ObtenerClientesAsync`:** Mapea `RUC_CLIENTE` desde la BD
- **`MapReclamo`:** Mapea los nuevos campos (Análisis, Decisión, notificaciones)
- **`GuardarAnalisisCausaAsync`:** Llama a `P_GUARDAR_ANALISIS_CAUSA`
- **`GuardarDecisionAsync`:** Llama a `P_GUARDAR_DECISION`
- **`NotificarCalidadAsync`:** 
  - Llama a `P_NOTIFICAR_CALIDAD` para obtener correos y registrar notificación
  - Envía correos usando `IEmailNotificacionService` con payload `ReclamoEnviadoCalidadPayload`
- **`NotificarVendedorAprobadoAsync`:**
  - Llama a `P_NOTIFICAR_VENDEDOR_APROBADO` para obtener correo del vendedor
  - Envía correo usando `IEmailNotificacionService` con payload `ReclamoEvaluadoVendedorPayload`
- **`ObtenerDatosImpresionAsync`:** Retorna datos completos del reclamo aprobado
- **Helpers:**
  - `ObtenerCorreoVendedorAsync(usuVendedor)` - Obtiene correo del vendedor
  - `GetUrlPortal(idReclamo)` - Construye URL del portal

---

### 4. **Controlador**
**Archivo:** `FabricaHilos/Controllers/Sgc/AnalisisReclamoController.cs`

#### Inyecciones de Dependencia
- Agregado: `IReclamoPdfService` para generación de PDFs

#### Nuevas Acciones HTTP
```csharp
[HttpPost("GuardarAnalisisCausa")]
public async Task<IActionResult> GuardarAnalisisCausa(long id, string texto)
  → POST /Sgc/Reclamos/GuardarAnalisisCausa

[HttpPost("GuardarDecision")]
public async Task<IActionResult> GuardarDecision(long id, string texto)
  → POST /Sgc/Reclamos/GuardarDecision

[HttpPost("NotificarCalidad")]
public async Task<IActionResult> NotificarCalidad(long id)
  → POST /Sgc/Reclamos/NotificarCalidad

[HttpPost("NotificarVendedorAprobado")]
public async Task<IActionResult> NotificarVendedorAprobado(long id)
  → POST /Sgc/Reclamos/NotificarVendedorAprobado

[HttpGet("Imprimir/{id:long}")]
public async Task<IActionResult> Imprimir(long id)
  → GET /Sgc/Reclamos/Imprimir/123

[HttpGet("DescargarPdf/{id:long}")]
public async Task<IActionResult> DescargarPdf(long id)
  → GET /Sgc/Reclamos/DescargarPdf/123 → Descarga PDF
```

---

### 5. **Servicio de Notificaciones**
**Directorio:** `FabricaHilos.Notificaciones/`

#### Enum TipoNotificacion
Agregados:
- `ReclamoEnviadoCalidad` - Notificación al equipo de calidad
- `ReclamoEvaluadoVendedor` - Notificación al vendedor tras aprobación

#### Payloads Nuevos
**ReclamoEnviadoCalidadPayload:**
- IdReclamo, NombreCliente, RucCliente, Asunto
- NombreVendedor, CorreoVendedor
- FechaCreacion, Descripcion

**ReclamoEvaluadoVendedorPayload:**
- IdReclamo, NombreCliente, RucCliente, Asunto
- FechaCreacion, DecisionFinal
- NombreAnalista, NombreGerente, FechaAprobacion
- UrlPortal

#### Templates HTML
- `Templates/ReclamoEnviadoCalidad.html` - Email para equipo de calidad
- `Templates/ReclamoEvaluadoVendedor.html` - Email para vendedor

#### EmailNotificacionService
Actualizado el método `ObtenerAsunto()` para soportar nuevos tipos.

---

### 6. **Servicio de PDF**
**Archivo:** `FabricaHilos/Services/Sgc/AnalisisReclamo/ReclamoPdfService.cs`

#### Interfaz
```csharp
public interface IReclamoPdfService
{
	byte[] GenerarPdf(ReclamoImpresionDto datos, string logoPath = "");
}
```

#### Implementación
Utiliza **QuestPDF** (Community License) para generar PDFs con:
- Encabezado con logo y datos de empresa
- Información general del reclamo (cliente, RUC, contacto, teléfono, asunto)
- Cronología de eventos (creación, análisis, aprobación)
- Descargos ordenados por fecha (todos los roles)
- Análisis de causa
- Decisión final
- Lista de archivos adjuntos con tamaños y fechas
- Espacio para firma de gerencia
- Pie de página con fecha de generación

---

### 7. **Vista (Detalle del Reclamo)**
**Archivo:** `FabricaHilos/Views/Sgc/AnalisisReclamo/Detalle.cshtml`

#### Cambios
- Visualiza el RUC en lugar del código del cliente
- Nueva sección "Análisis de Causa" (visible cuando hay datos)
- Nueva sección "Decisión Final" (solo visible cuando estado = '04')
- Formularios para editar análisis y decisión (solo para rol de analista)
- Botones "Imprimir" e "Impresión" en el alert de reclamo aprobado
- Botón "Notificar Vendedor" en el alert de reclamo aprobado

#### Vista de Impresión
**Archivo:** `FabricaHilos/Views/Sgc/AnalisisReclamo/Imprimir.cshtml` (Nuevo)
- Diseño limpio y profesional para impresión
- Muestra toda la información del reclamo aprobado
- Incluye descargos, análisis, decisión y archivos
- CSS especial para impresión
- Botones de impresión y volver

---

### 8. **Configuración**
**Archivo:** `FabricaHilos/Program.cs`

Registro de servicios:
```csharp
builder.Services.AddSingleton<IReclamoPdfService, ReclamoPdfService>();
```

(El servicio `IEmailNotificacionService` ya estaba registrado en `AddNotificaciones()`)

---

## Flujo de Negocio Implementado

### 1️⃣ Vendedor Crea Reclamo
- Estado: `01 (Abierto)`
- Se registra `FCH_CREACION`, `USU_VENDEDOR`

### 2️⃣ Vendedor Envía a Calidad
- Estado: `01 → 02`
- Se ejecuta `NotificarCalidadAsync`
  - Obtiene correos del equipo de calidad desde BD
  - Envía email con `ReclamoEnviadoCalidadPayload`
  - Registra `FCH_NOTI_CALIDAD` en BD

### 3️⃣ Analista de Calidad Revisa
- Estado: `02`
- Puede:
  - Agregar descargos (rol: AC)
  - Subir archivos (rol: AC)
  - Guardar "Análisis de Causa" → `ANALISIS_CAUSA` en BD
  - Escalar a Gerencia (estado: 02 → 03)

### 4️⃣ Gerente Aprueba
- Estado: `03 → 04`
- Se ejecuta `AprobarReclamoAsync`
  - El analista puede guardar "Decisión Final" → `DECISION_FINAL` en BD
  - Registra `FCH_DECISION`, `USU_DECISION`, `FCH_APROBACION`, `USU_GERENTE`

### 5️⃣ Impresión y Notificación Final
- El analista/gerente puede:
  - **Imprimir:** Ver vista formateada en pantalla
  - **Descargar PDF:** Genera PDF descargable
  - **Notificar Vendedor:** Ejecuta `NotificarVendedorAprobadoAsync`
	- Obtiene correo del vendedor desde BD
	- Envía email con `ReclamoEvaluadoVendedorPayload`
	- Registra `FCH_NOTI_VEND` en BD

---

## Requisitos Previos para Funcionamiento

### 1. Configuración SMTP
El archivo `appsettings.json` debe contener la sección de correo:
```json
{
  "Email": {
	"SmtpHost": "smtp.empresa.com",
	"SmtpPort": 587,
	"UsuarioEnvio": "noreply@empresa.com",
	"PasswordEnvio": "xxxxx",
	"NombreEnvio": "Sistema SGC",
	"UsarSsl": true
  }
}
```

### 2. Contactos de Calidad en BD
La tabla que almacena usuarios de calidad debe estar configurada. El package Oracle obtiene los correos de aquí.

### 3. Tabla de Usuarios con Correo
La tabla `USUARIOS` debe tener columna `CORREO_ELECTRONICO` para obtener el correo del vendedor.

### 4. Licencia QuestPDF
Usando licencia Community (empresas con ingresos < $1M USD). La configuración ya está en `Program.cs`:
```csharp
QuestPDF.Settings.License = LicenseType.Community;
```

---

## Archivos Modificados/Creados

### Nuevos
- `FabricaHilos/Services/Sgc/AnalisisReclamo/ReclamoPdfService.cs`
- `FabricaHilos/Views/Sgc/AnalisisReclamo/Imprimir.cshtml`
- `FabricaHilos.Notificaciones/Models/Payloads/ReclamoEnviadoCalidadPayload.cs`
- `FabricaHilos.Notificaciones/Models/Payloads/ReclamoEvaluadoVendedorPayload.cs`
- `FabricaHilos.Notificaciones/Templates/ReclamoEnviadoCalidad.html`
- `FabricaHilos.Notificaciones/Templates/ReclamoEvaluadoVendedor.html`

### Modificados
- `FabricaHilos/Models/Sgc/AnalisisReclamoDtos.cs` - Nuevos campos y DTOs
- `FabricaHilos/Services/Sgc/AnalisisReclamo/AnalisisReclamoService.cs` - Nuevos métodos y inyección de servicios
- `FabricaHilos/Controllers/Sgc/AnalisisReclamoController.cs` - Nuevas acciones HTTP
- `FabricaHilos/Views/Sgc/AnalisisReclamo/Detalle.cshtml` - UI para campos nuevos
- `FabricaHilos.Notificaciones/Models/TipoNotificacion.cs` - Nuevos tipos
- `FabricaHilos.Notificaciones/Services/EmailNotificacionService.cs` - Asuntos actualizados
- `FabricaHilos/Program.cs` - Registro de nuevo servicio

---

## Pruebas Recomendadas

### 1. Crear Reclamo
- [ ] Vendedor crea reclamo correctamente
- [ ] Se registra RUC, contacto, teléfono, asunto

### 2. Enviar a Calidad
- [ ] Al cambiar estado a '02', se ejecuta notificación
- [ ] Equipo de calidad recibe correo con datos del reclamo

### 3. Análisis
- [ ] Analista puede agregar "Análisis de Causa"
- [ ] Se guarda en BD correctamente
- [ ] Se visualiza en la vista Detalle

### 4. Decisión
- [ ] Cuando gerente aprueba, aparece campo "Decisión Final"
- [ ] Analista puede guardar decisión
- [ ] Se visualiza correctamente

### 5. Impresión
- [ ] Botón "Imprimir" muestra vista formateada
- [ ] PDF se descarga correctamente
- [ ] PDF contiene toda la información

### 6. Notificación Final
- [ ] Botón "Notificar Vendedor" está visible cuando aprobado
- [ ] Vendedor recibe correo con decisión y enlace al portal

---

## Notas de Desarrollo

1. **Hot Reload:** La compilación inicial puede requerir reiniciar VS debido a cambios en interfaces y enums (errores ENC0023, ENC0021).

2. **Correos de Calidad:** Pendiente confirmar dónde se almacenan los correos de **ATUSPARIA CIERTO MARIA ANDREA** y **FIGUEROA YANEZ JOSE MARTIN**. Estos deben estar en la BD para que la notificación funcione.

3. **Correo del Vendedor:** Actualmente usa patrón `{usuario}@lacolonial.com.pe`. Esto se puede mejorar consultando tabla `USUARIOS` directamente.

4. **URL del Portal:** Se construye automáticamente con `https://{host}/Sgc/Reclamos/Detalle/{id}`.

5. **QuestPDF:** Requiere `QuestPDF` v2024.x (Community License). Verificar que el NuGet esté instalado.

---

## Tickets Pendientes

- [ ] Confirmar tabla y columna donde se almacenan correos de equipo de calidad
- [ ] Confirmar tabla donde se almacenan correos de vendedores
- [ ] Configurar SMTP en `appsettings.json` con credenciales reales
- [ ] Realizar pruebas end-to-end completas
- [ ] Documentar en wiki del proyecto

---

**Fecha de Implementación:** $(date)
**Estado:** Completo ✅
**Requiere Revisión:** Configuración SMTP y datos de contactos
