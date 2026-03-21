# FabricaHilos.Notificaciones

Librería de clase (.NET 9) independiente y reutilizable para el envío de notificaciones por correo electrónico dentro de la solución **FabricaHilosRepository**.

## Propósito

- Centralizar la lógica de envío de correo (MailKit + MimeKit).
- Gestionar los templates HTML con la identidad visual de **La Colonial Fábrica de Hilos S.A.**
- Ser consumida por cualquier proyecto de la solución mediante inyección de dependencias (DI).

## Arquitectura

```
FabricaHilos.Notificaciones/
├── Abstractions/
│   ├── INotificacionPayload.cs        ← Contrato que cada payload debe cumplir
│   └── IEmailNotificacionService.cs   ← Contrato del servicio de envío
├── Configuration/
│   └── EmailSettings.cs               ← Mapeo de appsettings.json
├── Extensions/
│   └── NotificacionesServiceExtensions.cs  ← AddNotificaciones() para DI
├── Models/
│   ├── TipoNotificacion.cs            ← Enum de tipos de correo disponibles
│   └── Payloads/
│       └── DocumentoLimboPayload.cs   ← Datos para el correo de "documento en limbo"
├── Rendering/
│   └── TemplateRenderer.cs            ← Carga el template y reemplaza {{placeholders}}
├── Services/
│   └── EmailNotificacionService.cs    ← Implementación con MailKit (SMTP + OAuth2)
└── Templates/
    └── DocumentoLimbo.html            ← Template con branding de La Colonial
```

## Uso

### 1. Registrar en el contenedor DI

En el `Program.cs` del proyecto consumidor (e.g. `FabricaHilos.LecturaCorreos`):

```csharp
builder.Services.AddNotificaciones(builder.Configuration);
```

### 2. Agregar la configuración en `appsettings.json`

```json
{
  "Notificaciones": {
    "Email": {
      "SmtpHost":     "smtp.office365.com",
      "SmtpPort":     587,
      "UsarSsl":      true,
      "UsuarioEnvio": "notificaciones@lacolonial.com.gt",
      "NombreEnvio":  "Sistema La Colonial",
      "TenantId":     "<azure-tenant-id>",
      "ClientId":     "<azure-client-id>",
      "ClientSecret": "<azure-client-secret>"
    }
  }
}
```

### 3. Inyectar y usar el servicio

```csharp
public class MiWorker
{
    private readonly IEmailNotificacionService _emailService;

    public MiWorker(IEmailNotificacionService emailService)
        => _emailService = emailService;

    public async Task NotificarDocumentoLimboAsync(AdjuntoPdf adjunto)
    {
        await _emailService.EnviarAsync(new DocumentoLimboPayload
        {
            CorreoDestinatario = adjunto.CorreoRemitente,
            NombreDestinatario = adjunto.NombreRemitente,
            CorreoRemitente    = adjunto.CorreoRemitente,
            FechaRecepcion     = adjunto.FechaRecepcion.ToString("dd/MM/yyyy HH:mm"),
            NombreArchivo      = adjunto.NombreArchivo,
            MotivoError        = "Sin archivo XML asociado"
        });
    }
}
```

## Agregar un nuevo tipo de notificación

1. Agregar el valor al enum `TipoNotificacion` (e.g. `DocumentoPorVencer`).
2. Crear el archivo `Templates/DocumentoPorVencer.html` con los `{{placeholders}}` que necesites.
3. Crear el payload `Models/Payloads/DocumentoPorVencerPayload.cs` implementando `INotificacionPayload`.
4. Implementar `ObtenerReemplazos()` devolviendo el diccionario `{ "NombrePlaceholder" => valor }`.

No es necesario modificar ningún otro archivo existente.

## Templates disponibles

| Tipo | Archivo | Descripción |
|------|---------|-------------|
| `DocumentoLimbo` | `Templates/DocumentoLimbo.html` | Notifica a un proveedor que su PDF no tiene XML válido o tipo no registrado |
