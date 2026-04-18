# 📋 Documentación de Infraestructura — FabricaHilosRepository

> **Audiencia:** Personal de Infraestructura  
> **Última actualización:** Abril 2026  
> **Propósito:** Documentar toda la configuración de infraestructura (rutas, puertos, servicios, conexiones) de cada proyecto del repositorio.

---

## 📑 Índice

1. [Estructura General del Repositorio](#1-estructura-general-del-repositorio)
2. [FabricaHilos — Aplicación Web Principal](#2-fabricahilos--aplicación-web-principal)
3. [FabricaHilos.DocumentExtractor — API de Extracción de Documentos](#3-fabricahilosdocumentextractor--api-de-extracción-de-documentos)
4. [FabricaHilos.LecturaCorreos — Servicio Windows de Lectura de Correos](#4-fabricahiloslecturacorreos--servicio-windows-de-lectura-de-correos)
5. [FabricaHilos.Notificaciones — Librería de Notificaciones por Correo](#5-fabricahilosnotificaciones--librería-de-notificaciones-por-correo)
6. [LaColonial — Aplicación Web Secundaria](#6-lacolonial--aplicación-web-secundaria)
7. [Scripts SQL](#7-scripts-sql)
8. [Claves de Protección de Datos (DataProtectionKeys)](#8-claves-de-protección-de-datos-dataprotectionkeys)
9. [Archivos Sensibles Excluidos del Repositorio](#9-archivos-sensibles-excluidos-del-repositorio)
10. [Resumen de Puertos y URLs](#10-resumen-de-puertos-y-urls)
11. [Resumen de Rutas de Red (UNC)](#11-resumen-de-rutas-de-red-unc)
12. [Resumen de Configuraciones por Proyecto](#12-resumen-de-configuraciones-por-proyecto)

---

## 1. Estructura General del Repositorio

```
FabricaHilosRepository/
├── FabricaHilos/                        ← Aplicación web principal (ASP.NET Core MVC)
├── FabricaHilos.DocumentExtractor/      ← API para extracción de datos de PDFs
├── FabricaHilos.LecturaCorreos/         ← Servicio Windows para lectura de correos
├── FabricaHilos.Notificaciones/         ← Librería de notificaciones por email
├── LaColonial/                          ← Aplicación web secundaria
├── Scripts/SQL/                         ← Scripts de base de datos
├── DataProtectionKeys/                  ← Claves de protección de ASP.NET Core
└── FabricaHilosRepository.slnx          ← Archivo de solución
```

**Requisitos generales:**
- .NET 8 SDK
- Sistema operativo Windows (requerido para acceso a carpetas compartidas de red vía SMB)
- Acceso de red al servidor `10.0.7.14` (carpetas compartidas)
- Acceso a base de datos Oracle (esquema VICMATE)

---

## 2. FabricaHilos — Aplicación Web Principal

### 2.1 Archivo de Configuración

El archivo `appsettings.json` **NO está en el repositorio** (está en `.gitignore`). Se debe crear a partir de la plantilla:

```
FabricaHilos/appsettings-template.json  →  copiar a  →  FabricaHilos/appsettings.json
```

### 2.2 Cadenas de Conexión

| Base de Datos | Clave en appsettings.json | Valor por defecto | Descripción |
|---|---|---|---|
| SQLite (Identity) | `ConnectionStrings:DefaultConnection` | `Data Source=FabricaHilos.db` | Usuarios, roles y claims de ASP.NET Core Identity |
| Oracle | `ConnectionStrings:OracleConnection` | *(debe configurarse)* | Base de datos principal de producción (esquema VICMATE) |

### 2.3 Puertos y URLs

| Perfil | Protocolo | URL | Puerto |
|---|---|---|---|
| IIS Express | HTTP | `http://localhost` | **1436** |
| IIS Express | HTTPS (SSL) | `https://localhost` | **44352** |
| Kestrel (FabricaHilos) | HTTP | `http://0.0.0.0` | **5000** |
| Kestrel (FabricaHilos) | HTTPS | `https://localhost` | **5001** |

> **Nota:** En Kestrel el binding HTTP es `0.0.0.0:5000` (escucha en todas las interfaces).

### 2.4 Rutas de Red (Carpetas Compartidas)

| Propósito | Clave en appsettings.json | Ruta UNC | Servidor |
|---|---|---|---|
| Fotos de Seguridad | `RutaSeguridad` | `\\10.0.7.14\6-20100096260\Seguridad` | 10.0.7.14 |
| Certificados TC (SGC) | `RutaCertificados` | `\\10.0.7.14\6-20100096260\Certificados` | 10.0.7.14 |

**Autenticación de red (SMB):**

Para acceder a las carpetas compartidas, se requiere configurar credenciales de red:

```json
{
  "NetworkShare": {
    "Username": "usuario_de_red",
    "Password": "contraseña",
    "Domain": "dominio"
  }
}
```

> La autenticación usa la API de Windows `WNetAddConnection2`. **Solo funciona en Windows.**

### 2.5 Servicio DocumentExtractor

La aplicación principal se comunica con la API DocumentExtractor:

| Clave en appsettings.json | Valor por defecto | Timeout |
|---|---|---|
| `DocumentExtractor:BaseUrl` | `https://localhost:7200/` | 60 segundos |

### 2.6 Red Interna (Control de Acceso)

Se configura qué subnets son consideradas "red interna" para control de acceso:

```json
{
  "RedInterna": {
    "Subnets": ["10.0.7.0/24"]
  }
}
```

**Rutas permitidas desde fuera de la red interna:**
- `/account/login`, `/account/logout`, `/account/accesodenegado`
- `/seguridad`, `/produccion`, `/registropreparatoria`, `/autoconer`

**Archivos estáticos siempre permitidos:**
- `/css/`, `/js/`, `/lib/`, `/images/`, `/favicon.ico`, `/_framework/`

### 2.7 Logs

| Ubicación | Patrón de archivo | Retención |
|---|---|---|
| `{DirectorioApp}/Logs/` | `log-{fecha}.txt` | 30 días (rolling diario) |

### 2.8 Sesión y Seguridad

| Configuración | Valor |
|---|---|
| Nombre de cookie de sesión | `.FabricaHilos.Session` |
| Timeout de sesión | 8 horas |
| HttpOnly | Sí |
| SameSite | Lax |
| Rate limit en `/Account/Login` | 10 intentos por IP cada 5 minutos |

### 2.9 Protección de Datos (DataProtection Keys)

| Clave en appsettings.json | Valor por defecto | Descripción |
|---|---|---|
| `DataProtection:KeysPath` | `DataProtectionKeys` (relativo al directorio de la app) | Ruta donde se almacenan las claves de cifrado de cookies |

> Soporta rutas absolutas y relativas. La carpeta debe existir y tener permisos de escritura.

### 2.10 Menús Configurables

La visibilidad de los módulos del menú se puede controlar desde `appsettings.json` bajo la sección `"Menus"`:

- Dashboard, Produccion, Sgc, Facturacion, Ventas, RecursosHumanos, Seguridad

---

## 3. FabricaHilos.DocumentExtractor — API de Extracción de Documentos

### 3.1 Descripción

API REST para extracción de datos de documentos PDF (facturas SUNAT). Usa Tesseract OCR para procesamiento de documentos escaneados.

### 3.2 Puertos y URLs

| Perfil | Protocolo | URL | Puerto |
|---|---|---|---|
| Kestrel | HTTPS | `https://localhost` | **56823** |
| Kestrel | HTTP | `http://localhost` | **56824** |

### 3.3 Límites de Carga de Archivos

| Configuración | Valor |
|---|---|
| Tamaño máximo de cuerpo multipart | **30 MB** |
| Tamaño máximo de request body (Kestrel) | **30 MB** |

### 3.4 Dependencia: Tesseract OCR

Requiere archivos de datos de entrenamiento de Tesseract (no incluidos en el repositorio por tamaño):

| Archivo | Idioma | Ruta esperada |
|---|---|---|
| `eng.traineddata` | Inglés | `tessdata/eng.traineddata` |
| `spa.traineddata` | Español | `tessdata/spa.traineddata` |

> Estos archivos deben descargarse manualmente y colocarse en la carpeta `tessdata/` del proyecto.

### 3.5 Swagger

Disponible en ambiente de desarrollo para documentación de la API:
- **Título:** FabricaHilos - Document Extractor API
- **Versión:** v1

---

## 4. FabricaHilos.LecturaCorreos — Servicio Windows de Lectura de Correos

### 4.1 Descripción

Servicio Windows que lee correos electrónicos con facturas electrónicas (XML/PDF) y consulta CDRs en SUNAT. Se despliega como servicio de Windows.

### 4.2 Despliegue como Servicio Windows

| Configuración | Valor |
|---|---|
| Nombre del servicio | `FabricaHilos LecturaCorreos CDR` |
| Ruta de publicación sugerida | `C:\Servicios\LecturaCorreos` |
| Nombre para `sc create` | `FabricaHilosLecturaCorreos` |
| Timeout de apagado | 30 segundos |

**Comando para instalar el servicio:**
```cmd
sc create FabricaHilosLecturaCorreos binPath="C:\Servicios\LecturaCorreos\FabricaHilos.LecturaCorreos.exe"
```

### 4.3 Perfiles de Ejecución

| Perfil | Variable de entorno | Archivos de configuración |
|---|---|---|
| Producción | `DOTNET_ENVIRONMENT=Production` | `appsettings.json` |
| Desarrollo | `DOTNET_ENVIRONMENT=Development` | `appsettings.json` + `appsettings.Development.json` |

### 4.4 Configuración de Correo (IMAP)

Se configura bajo la sección `"LecturaCorreos"` en `appsettings.json`:

**Cuentas de correo:**

| Clave | Descripción | Valor por defecto |
|---|---|---|
| `Proveedor` | Tipo de proveedor | `"Office365"`, `"Gmail"`, o `"Office365OAuth2"` |
| `ImapHost` | Servidor IMAP | *(debe configurarse)* |
| `ImapPort` | Puerto IMAP | **993** |
| `UsarSsl` | Usar SSL/TLS | `true` |
| `Usuario` | Usuario de la cuenta | *(debe configurarse)* |
| `Contrasena` | Contraseña de la cuenta | *(debe configurarse)* |
| `Carpeta` | Carpeta IMAP a leer | `"INBOX"` |
| `CarpetaProcesados` | Carpeta para correos procesados | *(opcional)* |
| `MarcarLeido` | Marcar correos como leídos | `true` |
| `MoverProcesado` | Mover correos procesados | `false` |

**Configuración OAuth2 (Office365):**

| Clave | Descripción |
|---|---|
| `TenantId` | ID del tenant de Azure AD |
| `ClientId` | ID de la aplicación registrada en Azure AD |
| `ClientSecret` | Secreto de la aplicación |
| `AuthUrl` | URL de autenticación (auto-generada) |
| `Scope` | `https://outlook.office365.com/.default` |

**URL de token OAuth2:** `https://login.microsoftonline.com/{TenantId}/oauth2/v2.0/token`

### 4.5 Intervalos y Límites

| Clave | Descripción | Valor por defecto |
|---|---|---|
| `IntervaloMinutos` | Intervalo de lectura de correos | **10 minutos** |
| `MaxCorreosPorCiclo` | Máximo de correos por ciclo | **50** |
| `IntervaloConsultaMinutos` | Intervalo de consulta CDR a SUNAT | **15 minutos** |
| `MaxCuentasParalelo` | Máximo de conexiones IMAP paralelas | **4** |

### 4.6 Workers (Activación/Desactivación)

| Clave | Descripción | Valor por defecto |
|---|---|---|
| `WorkerCorreosActivo` | Worker de lectura de correos | *(configurable)* |
| `WorkerSunatActivo` | Worker de consulta CDR a SUNAT | *(configurable)* |
| `WorkerNotificacionPdfActivo` | Worker de notificación de PDFs en limbo | *(configurable)* |

### 4.7 Endpoints SUNAT

| Ambiente | URL |
|---|---|
| **Producción** | `https://e-factura.sunat.gob.pe/ol-it-wsconscpegem/billConsultService` |
| **Beta/Desarrollo** | `https://e-beta.sunat.gob.pe/ol-it-wsconscpegem-beta/billConsultService` |

Se configura con la clave `EndpointConsultaCdr`. Si se deja vacío, usa el endpoint de producción por defecto.

### 4.8 Credenciales SUNAT (SOL)

| Clave | Descripción |
|---|---|
| `UsuarioSol` | Usuario SOL de SUNAT |
| `ClaveSol` | Clave SOL de SUNAT |

### 4.9 Almacenamiento de Documentos (Facturas)

| Clave | Descripción |
|---|---|
| `RutaArchivos` | Ruta base para guardar documentos descargados |

**Estructura de carpetas generada automáticamente:**
```
{RutaArchivos}/{RucEmpresa}/{año}/{mes}/{día}/
```

**Ejemplo:**
```
C:\Documentos\20100096260\2026\04\18\
```

**Patrón de nombre de archivo de facturas:**
```
{ruc_emisor}-{tipo_doc}-{serie}-{correlativo}.{extensión}
```

**Ejemplo:**
```
20551234567-01-F001-00000001.pdf
20551234567-01-F001-00000001.xml
```

**Tipos de documento soportados:**
| Código | Tipo |
|---|---|
| `01` | Factura |
| `03` | Boleta |
| `07` | Nota de Crédito |
| `08` | Nota de Débito |

**Formatos de nombre de archivo reconocidos:**
- Estándar SUNAT: `{RUC}-{TIPO}-{SERIE}-{CORRELATIVO}`
- Con guion bajo: `{RUC}_{tipo}_{serie}-{correlativo}`
- Mínimo: `{SERIE}-{CORRELATIVO}`
- CDR: `R-{RUC}-{TIPO}-{SERIE}-{CORRELATIVO}`

### 4.10 Soporte Multi-Empresa

El servicio soporta múltiples empresas, cada una con sus propias:
- Cuentas de correo
- Credenciales SUNAT (SOL)
- RUC

Se configura bajo la sección `"Empresas"` en `appsettings.json`.

### 4.11 Cadena de Conexión Oracle

| Clave | Descripción |
|---|---|
| `ConnectionStrings:OracleConnection` | Conexión a la base de datos Oracle principal |

> Se requiere validación de la cadena de conexión Oracle al iniciar el servicio.

### 4.12 Logs

| Ubicación | Patrón de archivo | Retención |
|---|---|---|
| `Logs/` | `lecturaCorreos-{fecha}.log` | 30 días (rolling diario) |

### 4.13 Timeouts

| Operación | Timeout |
|---|---|
| Consulta SOAP a SUNAT | 30 segundos |
| Obtención de token OAuth2 | 30 segundos |
| HttpClient para SUNAT | 30 segundos |

---

## 5. FabricaHilos.Notificaciones — Librería de Notificaciones por Correo

### 5.1 Descripción

Librería de clases para envío de notificaciones por correo electrónico. Es una dependencia de otros proyectos (no se despliega por separado).

### 5.2 Configuración SMTP

Se configura bajo la sección `"EmailSettings"` en el `appsettings.json` del proyecto que la consume:

| Clave | Descripción | Valor por defecto |
|---|---|---|
| `SmtpHost` | Servidor SMTP | *(ejemplo: `smtp.office365.com`)* |
| `SmtpPort` | Puerto SMTP | **587** |
| `UsarSsl` | Usar SSL/TLS | `true` |
| `UsuarioEnvio` | Dirección de correo remitente | *(ejemplo: `notificaciones@colonial.com.pe`)* |
| `NombreEnvio` | Nombre visible del remitente | `"Sistema La Colonial"` |
| `PasswordEnvio` | Contraseña del correo | *(debe configurarse)* |

### 5.3 Configuración OAuth2 (Opcional)

| Clave | Descripción |
|---|---|
| `UsarOAuth2` | Activar autenticación OAuth2 |
| `TenantId` | ID del tenant de Azure AD |
| `ClientId` | ID de la aplicación en Azure AD |
| `ClientSecret` | Secreto de la aplicación |

### 5.4 Tipos de Notificación

| Tipo | Descripción |
|---|---|
| DocumentoLimbo | PDF recibido sin XML válido asociado |

---

## 6. LaColonial — Aplicación Web Secundaria

### 6.1 Descripción

Aplicación web ASP.NET Core MVC complementaria.

### 6.2 Puertos y URLs

| Perfil | Protocolo | URL | Puerto |
|---|---|---|---|
| IIS Express | HTTP | `http://localhost` | **34013** |
| IIS Express | HTTPS (SSL) | `https://localhost` | **44334** |
| Kestrel | HTTP | `http://localhost` | **5186** |
| Kestrel | HTTPS | `https://localhost` | **7255** |

### 6.3 Caché de Archivos Estáticos

| Ambiente | Duración de caché |
|---|---|
| Producción | **30 días** (2,592,000 segundos) |
| Desarrollo | Sin caché |

### 6.4 Configuración

- Redirección HTTPS habilitada en producción.
- Usa `appsettings.json` (no incluido en el repositorio).

---

## 7. Scripts SQL

### 7.1 Scripts Disponibles

| Script | Descripción |
|---|---|
| `Scripts/SQL/CREATE_SI_INSPECCION.sql` | Crea la tabla `SI_INSPECCION` para inspecciones de seguridad |

### 7.2 Tabla SI_INSPECCION

| Campo relevante | Descripción |
|---|---|
| `RUTA_FOTO_H` | Ruta de la foto del hallazgo |
| `RUTA_FOTO_AC` | Ruta de la foto de acción correctiva |
| Esquema de permisos | Otorgados a **VICMATE** |

---

## 8. Claves de Protección de Datos (DataProtectionKeys)

| Elemento | Detalle |
|---|---|
| Directorio | `DataProtectionKeys/` |
| Archivo actual | `key-4f0ced06-28c6-4561-8d67-49dc974a53bf.xml` |
| Propósito | Proteger cookies de autenticación de ASP.NET Core Identity |
| Configuración | `DataProtection:KeysPath` en `appsettings.json` |

> **Importante:** Esta carpeta debe persistir entre despliegues y reinicios de IIS. Si se eliminan las claves, todos los usuarios serán deslogueados.

---

## 9. Archivos Sensibles Excluidos del Repositorio

Los siguientes archivos **NO están en el repositorio** y deben ser creados manualmente en cada ambiente:

| Archivo | Proyecto | Contenido |
|---|---|---|
| `appsettings.json` | Todos los proyectos | Cadenas de conexión, credenciales, rutas |
| `appsettings.Development.json` | Todos los proyectos | Configuración de desarrollo |
| `appsettings.*.json` | Todos los proyectos | Cualquier configuración por ambiente |
| `tessdata/eng.traineddata` | DocumentExtractor | Datos OCR en inglés |
| `tessdata/spa.traineddata` | DocumentExtractor | Datos OCR en español |

---

## 10. Resumen de Puertos y URLs

### Puertos Internos

| Proyecto | IIS HTTP | IIS HTTPS | Kestrel HTTP | Kestrel HTTPS |
|---|---|---|---|---|
| FabricaHilos | 1436 | 44352 | 5000 | 5001 |
| DocumentExtractor | — | — | 56824 | 56823 |
| LaColonial | 34013 | 44334 | 5186 | 7255 |
| LecturaCorreos | — | — | — | — *(servicio Windows, sin HTTP)* |

### URLs de Servicios Externos

| Servicio | URL | Proyecto |
|---|---|---|
| SUNAT Producción (CDR) | `https://e-factura.sunat.gob.pe/ol-it-wsconscpegem/billConsultService` | LecturaCorreos |
| SUNAT Beta (CDR) | `https://e-beta.sunat.gob.pe/ol-it-wsconscpegem-beta/billConsultService` | LecturaCorreos |
| Microsoft OAuth2 Token | `https://login.microsoftonline.com/{TenantId}/oauth2/v2.0/token` | LecturaCorreos |
| Office365 Scope | `https://outlook.office365.com/.default` | LecturaCorreos |

---

## 11. Resumen de Rutas de Red (UNC)

| Propósito | Ruta UNC | Servidor | Clave de configuración |
|---|---|---|---|
| Fotos de Seguridad | `\\10.0.7.14\6-20100096260\Seguridad` | 10.0.7.14 | `RutaSeguridad` |
| Certificados TC (SGC) | `\\10.0.7.14\6-20100096260\Certificados` | 10.0.7.14 | `RutaCertificados` |
| Documentos de Facturas | Configurable vía `RutaArchivos` | *(variable)* | `LecturaCorreos:RutaArchivos` |

> **Nota:** El share `6-20100096260` corresponde al RUC de la empresa. Se requieren credenciales de dominio configuradas en `NetworkShare` para acceso programático.

---

## 12. Resumen de Configuraciones por Proyecto

### Configuración Completa de `appsettings.json` — FabricaHilos

```json
{
  "RedInterna": {
    "Subnets": ["10.0.7.0/24"]
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=FabricaHilos.db",
    "OracleConnection": "... cadena Oracle ..."
  },
  "DataProtection": {
    "KeysPath": "DataProtectionKeys"
  },
  "DocumentExtractor": {
    "BaseUrl": "https://localhost:7200/"
  },
  "RutaSeguridad": "\\\\10.0.7.14\\6-20100096260\\Seguridad",
  "RutaCertificados": "\\\\10.0.7.14\\6-20100096260\\Certificados",
  "NetworkShare": {
    "Username": "...",
    "Password": "...",
    "Domain": "..."
  },
  "Menus": {
    "Dashboard": true,
    "Produccion": true,
    "Sgc": true,
    "Facturacion": true,
    "Ventas": true,
    "RecursosHumanos": true,
    "Seguridad": true
  }
}
```

### Configuración Completa de `appsettings.json` — LecturaCorreos

```json
{
  "ConnectionStrings": {
    "OracleConnection": "... cadena Oracle ..."
  },
  "LecturaCorreos": {
    "IntervaloMinutos": 10,
    "MaxCorreosPorCiclo": 50,
    "IntervaloConsultaMinutos": 15,
    "MaxCuentasParalelo": 4,
    "RutaArchivos": "C:\\Documentos",
    "WorkerCorreosActivo": true,
    "WorkerSunatActivo": true,
    "WorkerNotificacionPdfActivo": true,
    "Empresas": [
      {
        "RucEmpresa": "20100096260",
        "UsuarioSol": "...",
        "ClaveSol": "...",
        "EndpointConsultaCdr": "",
        "Cuentas": [
          {
            "Nombre": "Cuenta Principal",
            "Activa": true,
            "Proveedor": "Office365",
            "ImapHost": "outlook.office365.com",
            "ImapPort": 993,
            "UsarSsl": true,
            "Usuario": "facturas@colonial.com.pe",
            "Contrasena": "...",
            "Carpeta": "INBOX",
            "MarcarLeido": true
          }
        ]
      }
    ]
  },
  "EmailSettings": {
    "SmtpHost": "smtp.office365.com",
    "SmtpPort": 587,
    "UsarSsl": true,
    "UsuarioEnvio": "notificaciones@colonial.com.pe",
    "NombreEnvio": "Sistema La Colonial",
    "PasswordEnvio": "..."
  }
}
```

---

## 📌 Notas Importantes para Infraestructura

1. **Firewall:** Asegurar que los servidores de aplicación tengan acceso saliente a:
   - `e-factura.sunat.gob.pe` (puerto 443) — Consultas CDR producción
   - `login.microsoftonline.com` (puerto 443) — Autenticación OAuth2
   - `outlook.office365.com` (puerto 993) — IMAP para lectura de correos
   - `smtp.office365.com` (puerto 587) — Envío de notificaciones

2. **Carpetas de red:** El servidor de aplicación debe tener:
   - Permisos de lectura/escritura en `\\10.0.7.14\6-20100096260\Seguridad`
   - Permisos de lectura/escritura en `\\10.0.7.14\6-20100096260\Certificados`
   - Credenciales de dominio configuradas para acceso SMB

3. **Logs:** Todas las aplicaciones escriben logs en una carpeta `Logs/` dentro de su directorio. Asegurar permisos de escritura y monitorear espacio en disco (retención de 30 días).

4. **DataProtectionKeys:** Mantener la persistencia de la carpeta `DataProtectionKeys/` entre despliegues para evitar invalidación de sesiones de usuario.

5. **Certificados SSL:** Para ambientes de producción, configurar certificados SSL válidos en IIS o en la configuración de Kestrel.

6. **Tesseract OCR:** Descargar los archivos de entrenamiento (`eng.traineddata`, `spa.traineddata`) desde el [repositorio oficial de Tesseract](https://github.com/tesseract-ocr/tessdata) y colocarlos en la carpeta `tessdata/` del proyecto DocumentExtractor.
