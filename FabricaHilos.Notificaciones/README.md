# FabricaHilos.Notificaciones

Biblioteca de clases (.NET 9) para la gestión centralizada de notificaciones por correo electrónico en La Colonial Fábrica de Hilos S.A.

## Propósito

Proyecto independiente que centraliza:
- Templates HTML de correo con la identidad visual de La Colonial
- Configuración del servidor de correo saliente (SMTP / Office365 OAuth2)
- Envío de correos mediante MailKit + MimeKit

Cualquier proyecto de la solución puede referenciar esta librería y enviar notificaciones sin conocer los detalles de implementación.

## Cómo agregar un nuevo tipo de notificación (3 pasos)

1. **Agregar al enum** en `Models/TipoNotificacion.cs`:
   ```csharp
   public enum TipoNotificacion
   {
       DocumentoLimbo,
       NuevoTipo,      // <- agregar aquí
   }
   ```

2. **Crear el payload** en `Models/Payloads/NuevoTipoPayload.cs`:
   ```csharp
   public class NuevoTipoPayload : INotificacionPayload
   {
       public TipoNotificacion Tipo => TipoNotificacion.NuevoTipo;
       public required string CorreoDestinatario { get; set; }
       public required string NombreDestinatario { get; set; }
       // ... propiedades específicas del caso ...
       public Dictionary<string, string> ObtenerReemplazos() => new()
       {
           { "PlaceholderUno", PropiedadUno },
       };
   }
   ```

3. **Crear el template HTML** en `Templates/NuevoTipo.html` con los `{{Placeholder}}` correspondientes.

## Configuración en appsettings.json del proyecto consumidor

```json
{
  "Notificaciones": {
    "Email": {
      "SmtpHost": "smtp.office365.com",
      "SmtpPort": 587,
      "UsarSsl": true,
      "UsuarioEnvio": "notificaciones@colonial.com.pe",
      "NombreEnvio": "Sistema La Colonial",
      "PasswordEnvio": ""
    }
  }
}
```

## Registro en Program.cs del proyecto consumidor

```csharp
using FabricaHilos.Notificaciones.Extensions;

builder.Services.AddNotificaciones(builder.Configuration);
```

## Ejemplo de uso desde FabricaHilos.LecturaCorreos

```csharp
// Inyectar en el constructor:
private readonly IEmailNotificacionService _emailService;

// Enviar notificación:
await _emailService.EnviarAsync(new DocumentoLimboPayload
{
    CorreoDestinatario = correoRemitente,
    NombreDestinatario = nombreRemitente,
    NombreRemitente    = nombreRemitente,
    CorreoRemitente    = correoRemitente,
    FechaRecepcion     = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
    NombreArchivo      = adjunto.NombreArchivo,
    MotivoError        = "Sin archivo XML asociado"
});
```

## Tipos de notificación disponibles

| Tipo | Template | Descripción |
|------|----------|-------------|
| `DocumentoLimbo` | `DocumentoLimbo.html` | PDF sin XML válido o tipo no registrado |
