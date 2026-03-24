# FabricaHilosRepository

Sistema multi-proyecto para la gestión y procesamiento de documentos PDF de La Colonial.

## Arquitectura

El sistema está compuesto por cuatro proyectos:

| Proyecto | Tipo | Puerto (dev) | Descripción |
|---|---|---|---|
| **FabricaHilos** | ASP.NET MVC | `https://localhost:55400` | Aplicación web principal |
| **FabricaHilos.DocumentExtractor** | Web API | `https://localhost:7200` | API de extracción de texto (OCR) |
| **FabricaHilos.LecturaCorreos** | Worker Service | — | Lector de correos IMAP |
| **FabricaHilos.Notificaciones** | Class Library | — | Envío de correos HTML |

## Flujo principal — Extracción de PDF

```
Usuario (FabricaHilos MVC)
  └─→ POST /Facturacion/ImportarFacturas (IFormFile archivoPdf)
        └─→ DocumentExtractorClient.ExtraerAsync()
              └─→ POST https://localhost:7200/api/v1/extractor/extraer
                    └─→ ExtractorController.Extraer()
                          ├─→ PdfExtractorService.ExtraerAsync()
                          │     ├─→ PdfPig (texto nativo)
                          │     │     Si texto < 20 chars o sin palabras útiles:
                          │     └─→ Tesseract OCR (PDF→imagen@400dpi→preprocessing→OCR)
                          └─→ DocumentoExtraido { TipoDocumento, RUC, Serie, Monto, Items... }
              └─→ View("VistaPrevia", extraido)  ← muestra campos extraídos
```

## Flujo secundario — Correos IMAP y notificaciones PDF en limbo

```
Correos IMAP (FabricaHilos.LecturaCorreos)
  └─→ LecturaCorreosSunatCdrWorker (cada 10 min)
        ├─→ PDFs con XML válido → FH_LECTCORREOS_ARCHIVOS + disco
        └─→ PDFs huérfanos → FH_LECTCORREOS_PDF_ADJUNTOS (ESTADO='PENDIENTE')
              └─→ NotificacionPdfLimboWorker (cada 5 min)
                    └─→ FabricaHilos.Notificaciones.IEmailNotificacionService.EnviarAsync()
                          └─→ MailKit SMTP → correo HTML al remitente
```

## Configuración

### FabricaHilos (MVC)

**`appsettings.json`** — Variables clave:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=FabricaHilos.db"
  },
  "DocumentExtractor": {
    "BaseUrl": "https://localhost:7200/"
  }
}
```

### FabricaHilos.DocumentExtractor (Web API)

No requiere configuración adicional. Tessdata se copia automáticamente al directorio de salida.

**Endpoints disponibles:**
- `POST /api/v1/extractor/extraer` — Extrae datos de un PDF o imagen (máx. 30 MB).
- `GET  /api/v1/extractor/diagnostico` — Verifica la configuración del OCR.
- `POST /api/v1/extractor/diagnostico` — Diagnóstico con archivo: texto crudo + resultado de extracción.

**Tipos MIME aceptados:** `application/pdf`, `image/png`, `image/jpeg`, `image/tiff`, `image/bmp`, `image/webp`.

### FabricaHilos.LecturaCorreos (Worker Service)

**`appsettings.json`** — Variables clave:

```json
{
  "ConnectionStrings": {
    "OracleConnection": "..."
  },
  "LecturaCorreos": {
    "IntervaloMinutos": 10,
    "WorkerCorreosActivo": true,
    "WorkerSunatActivo": true,
    "WorkerNotificacionPdfActivo": true,
    "IntervaloNotificacionPdfMinutos": 5,
    "Cuentas": [ { ... } ]
  },
  "Notificaciones": {
    "Email": {
      "SmtpHost": "smtp.office365.com",
      "SmtpPort": 587,
      "UsarOAuth2": true,
      "TenantId": "...",
      "ClientId": "...",
      "ClientSecret": "..."
    }
  }
}
```

### FabricaHilos.Notificaciones (Class Library)

Librería de envío de correos. Se configura a través de `IServiceCollection.AddNotificaciones(IConfiguration)`.
La sección requerida en la configuración del proyecto consumidor es `Notificaciones:Email`.

## Ejecución

```bash
# Levantar la API de extracción (debe estar corriendo antes que el MVC)
cd FabricaHilos.DocumentExtractor
dotnet run

# Levantar la aplicación web
cd FabricaHilos
dotnet run

# Levantar el worker de correos (requiere Oracle y credenciales IMAP/SMTP)
cd FabricaHilos.LecturaCorreos
dotnet run
```

## Dependencias externas

- **Oracle Database** — `FabricaHilos.LecturaCorreos` almacena los correos y PDFs procesados.
- **SQLite** — `FabricaHilos` (MVC) usa SQLite para usuarios e inventario local.
- **Tesseract OCR** — El proyecto `FabricaHilos.DocumentExtractor` requiere los archivos de idioma en `tessdata/`. El idioma configurado es español (`spa`).
- **MailKit / MimeKit** — Usado por `FabricaHilos.Notificaciones` para envío SMTP con soporte OAuth2.
- **Microsoft OAuth2** — Las cuentas de correo IMAP y SMTP se autentican mediante OAuth2 con Microsoft Entra ID (Azure AD).
