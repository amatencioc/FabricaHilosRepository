# 🏭 FabricaHilos — Sistema de Gestión La Colonial

Sistema web interno de gestión empresarial para **La Colonial S.A.** (RUC: 20100096260).  
Desarrollado en **ASP.NET Core 8 (MVC)** con base de datos **Oracle** y autenticación local mediante **ASP.NET Core Identity + SQLite**.

---

## 📋 Tabla de Contenidos

- [Arquitectura de la solución](#arquitectura-de-la-solución)
- [Requisitos de infraestructura](#requisitos-de-infraestructura)
- [Base de datos](#base-de-datos)
- [Carpeta compartida en red](#carpeta-compartida-en-red)
- [Correo electrónico SMTP](#correo-electrónico-smtp)
- [Configuración de la aplicación](#configuración-de-la-aplicación)
- [Módulos habilitados](#módulos-habilitados)
- [Servicio de sesión y seguridad](#servicio-de-sesión-y-seguridad)
- [Logs](#logs)
- [Publicación y despliegue](#publicación-y-despliegue)
- [Proyectos en la solución](#proyectos-en-la-solución)
- [Dependencias NuGet destacadas](#dependencias-nuget-destacadas)

---

## 🏗 Arquitectura de la solución

| Proyecto | Tipo | Descripción |
|---|---|---|
| `FabricaHilos` | ASP.NET Core 8 MVC | Aplicación web principal |
| `FabricaHilos.DocumentExtractor` | Worker Service (.NET 9) | Extractor de documentos en segundo plano |
| `FabricaHilos.LecturaCorreos` | Worker Service | Lectura automática de correos entrantes |
| `FabricaHilos.Notificaciones` | Class Library | Envío de notificaciones por correo electrónico |
| `LaColonial` | Proyecto auxiliar | Módulos complementarios |

---

## ⚙️ Requisitos de infraestructura

### Servidor de aplicación

| Parámetro | Valor |
|---|---|
| Runtime requerido | [.NET 8 ASP.NET Core Hosting Bundle](https://dotnet.microsoft.com/en-us/download/dotnet/8) |
| Framework web | ASP.NET Core 8 MVC |
| Servidor web | IIS con módulo ASP.NET Core, o Kestrel directo |
| Sistema operativo | Windows Server 2019+ recomendado |
| Puerto por defecto | 80 / 443 |

### Conectividad de red requerida

| Recurso | IP / Host | Puerto | Protocolo | Descripción |
|---|---|---|---|---|
| Servidor Oracle | `10.0.7.11` | `1521` | TCP | Base de datos principal |
| Servidor de archivos | `10.0.7.14` | `445` | SMB | Carpetas compartidas |
| Servidor SMTP | `smtp.office365.com` | `587` | STARTTLS | Envío de correos |

---

## 🗄 Base de datos

### Oracle (datos en tiempo real)

| Parámetro | Valor |
|---|---|
| Host | `10.0.7.11` |
| Puerto | `1521` |
| Service Name | `ORCL` |
| Usuario principal | `VICMATE` |
| Usuario Aquarius | `AQUARIUS` |
| Código empresa Aquarius | `0003` |

> ⚠️ **Importante:** Los usuarios de `CS_USER` son usuarios reales de Oracle. Las credenciales del usuario logueado en la aplicación se usan para conectar a Oracle en tiempo real. La aplicación **no cachea datos de negocio**.

### SQLite (autenticación local)

La gestión de usuarios, roles y sesiones usa una base de datos SQLite local:

| Parámetro | Valor |
|---|---|
| Archivo | `FabricaHilos.db` (en el directorio de publicación) |
| Uso | ASP.NET Core Identity — login, roles, bloqueo de cuentas |

Las **claves de protección de datos** (DataProtection) se almacenan en `../DataProtectionKeys` relativo al directorio de publicación.  
Esta carpeta debe **persistir entre redespliegues** para no invalidar las cookies de sesión activas.

---

## 📁 Carpeta compartida en red

La aplicación lee y escribe archivos en el servidor `10.0.7.14`.

| Parámetro | Valor |
|---|---|
| Servidor | `\\10.0.7.14` |
| Share raíz | `\\10.0.7.14\6-20100096260` |
| Usuario de red | `BIZLINKS_COL` |
| Dominio | `WORKGROUP` |

### Rutas configuradas

| Clave appsettings | Ruta UNC | Uso |
|---|---|---|
| `RutaProv` | `\\10.0.7.14\6-20100096260` | Documentos de proveedores |
| `RutaSeguridad` | `\\10.0.7.14\FotosSeguridad\Inspeccion` | Fotos de inspección de seguridad |
| `RutaCertificados` | `\\10.0.7.14\6-20100096260\Certificados` | Certificados de calidad |
| `RutaRequerimientos` | `\\10.0.7.14\6-20100096260\Requerimientos` | Adjuntos de requerimientos logísticos |

> ⚠️ La cuenta de servicio del Application Pool de IIS debe tener **permisos de lectura y escritura** sobre todas las rutas UNC listadas, o bien el acceso se gestiona con las credenciales `BIZLINKS_COL` en la sección `NetworkShare` de `appsettings.json`.

### Estructura de carpetas en Requerimientos

```
\\10.0.7.14\6-20100096260\Requerimientos\
└── {ID_GRUPO}\          <- Una carpeta por grupo (número de secuencia Oracle SIG.LG_GRUPO_SEQ)
    ├── archivo1.pdf
    ├── archivo2.xlsx
    └── ...
```

---

## 📧 Correo electrónico SMTP

| Parámetro | Valor |
|---|---|
| Servidor SMTP | `smtp.office365.com` |
| Puerto | `587` (STARTTLS) |
| Usuario de envío | `vmatencio@colonial.com.pe` |
| Nombre visible | `Sistema La Colonial` |
| Correo facturación | `iramirez@colonial.com.pe` |
| Correo facturación (copia) | `matusparia@colonial.com.pe` |

> El servidor de aplicación debe tener **salida a Internet por el puerto 587** hacia `smtp.office365.com`.

---

## 🔧 Configuración de la aplicación

Archivo principal: `appsettings.json`  
Sobrescritura por entorno: `appsettings.Production.json` (si existe)

Claves relevantes para infraestructura:

```json
{
  "ConnectionStrings": {
    "DefaultConnection":  "Data Source=FabricaHilos.db",
    "OracleConnection":   "Data Source=10.0.7.11:1521/ORCL;User Id=VICMATE;Password=***",
    "AquariusConnection": "Data Source=10.0.7.11:1521/ORCL;User Id=AQUARIUS;Password=***"
  },
  "DataProtection": { "KeysPath": "..\\DataProtectionKeys" },
  "RedInterna": { "Subnets": [ "10.0.7.0/24" ] },
  "RutaProv":           "\\\\10.0.7.14\\6-20100096260",
  "RutaSeguridad":      "\\\\10.0.7.14\\FotosSeguridad\\Inspeccion",
  "RutaCertificados":   "\\\\10.0.7.14\\6-20100096260\\Certificados",
  "RutaRequerimientos": "\\\\10.0.7.14\\6-20100096260\\Requerimientos",
  "RucEmpresa": "20100096260",
  "NetworkShare": { "Username": "BIZLINKS_COL", "Domain": "WORKGROUP" },
  "DocumentExtractor": { "BaseUrl": "https://localhost:56823/" }
}
```

> 🔒 Las contraseñas no deben almacenarse en texto plano en producción. Usar variables de entorno para sobreescribir los valores sensibles (ver sección de despliegue).

---

## 🗂 Módulos habilitados

Los módulos se habilitan/deshabilitan desde `appsettings.json` sección `Menus`:

| Módulo | Clave | Estado |
|---|---|---|
| Dashboard | `Dashboard` | ✅ Activo |
| Producción | `Produccion` | ✅ Activo |
| SGC (Calidad) | `Sgc` | ✅ Activo |
| Ventas | `Ventas` | ✅ Activo |
| Seguridad | `Seguridad` | ✅ Activo |
| Recursos Humanos | `RecursosHumanos` | ✅ Activo |
| Logística | `Logistica` | ✅ Activo |
| Facturación | `Facturacion` | ❌ Desactivado |

---

## 🔐 Servicio de sesión y seguridad

| Parámetro | Valor |
|---|---|
| Timeout de sesión | **8 horas** (1 turno laboral, deslizante) |
| Renovación automática | Sí — se renueva con cada request activo |
| Intentos fallidos de login | 5 intentos → bloqueo de **10 minutos** |
| Red interna reconocida | Subred `10.0.7.0/24` |
| Cookie | HttpOnly — no accesible desde JavaScript |

---

## 📝 Logs

Los logs se generan con **Serilog** en dos destinos simultáneos:

| Destino | Detalle |
|---|---|
| Consola | Siempre activo |
| Archivo | `{DirectorioPublicación}/Logs/log-YYYYMMDD.txt` |

| Parámetro | Valor |
|---|---|
| Rotación | Diaria |
| Retención | 30 días |
| Nivel mínimo general | `Information` |
| Nivel mínimo `FabricaHilos.*` | `Debug` |

> La carpeta `Logs/` se crea automáticamente. La cuenta de servicio debe tener permiso de escritura en el directorio de publicación.

---

## 🚀 Publicación y despliegue

### 1. Compilar y publicar

```powershell
dotnet publish FabricaHilos\FabricaHilos.csproj -c Release -o C:\Publicacion\FabricaHilos
```

### 2. Configurar IIS

1. Instalar el [ASP.NET Core Hosting Bundle para .NET 8](https://dotnet.microsoft.com/en-us/download/dotnet/8)
2. Crear un **Application Pool** → Sin código administrado (No Managed Code)
3. Identidad del pool: cuenta con acceso a las carpetas UNC `\\10.0.7.14\...`
4. Crear el **sitio web** apuntando a `C:\Publicacion\FabricaHilos`

### 3. Permisos necesarios en el servidor

| Carpeta | Permiso |
|---|---|
| `C:\Publicacion\FabricaHilos\` | Lectura + Escritura |
| `C:\Publicacion\FabricaHilos\Logs\` | Escritura |
| `..\DataProtectionKeys\` | Lectura + Escritura |
| `\\10.0.7.14\6-20100096260\` | Lectura + Escritura |
| `\\10.0.7.14\FotosSeguridad\Inspeccion\` | Lectura + Escritura |

### 4. Variables de entorno en producción (recomendado)

Configurar en IIS → Aplicación → Variables de entorno:

```
ConnectionStrings__OracleConnection    = Data Source=10.0.7.11:1521/ORCL;User Id=VICMATE;Password=<REAL>
ConnectionStrings__AquariusConnection  = Data Source=10.0.7.11:1521/ORCL;User Id=AQUARIUS;Password=<REAL>
NetworkShare__Password                 = <REAL>
Notificaciones__Email__PasswordEnvio   = <REAL>
```

### 5. Verificar conectividad

```powershell
Test-NetConnection -ComputerName 10.0.7.11 -Port 1521    # Oracle
Test-NetConnection -ComputerName 10.0.7.14 -Port 445     # Archivos compartidos
Test-NetConnection -ComputerName smtp.office365.com -Port 587  # SMTP
```

---

## 🗂 Proyectos en la solución

```
FabricaHilosRepository/
├── FabricaHilos/                    <- Aplicación web principal (ASP.NET Core 8)
├── FabricaHilos.DocumentExtractor/  <- Worker Service (.NET 9) extractor de documentos
├── FabricaHilos.LecturaCorreos/     <- Worker Service lectura de correos entrantes
├── FabricaHilos.Notificaciones/     <- Librería de notificaciones por email
├── LaColonial/                      <- Módulos auxiliares
└── DataProtectionKeys/              <- Claves DataProtection (NO versionar, NO borrar en redespliegue)
```

---

## 📦 Dependencias NuGet destacadas

| Paquete | Uso |
|---|---|
| `Oracle.ManagedDataAccess.Core` | Conexión a Oracle (ADO.NET directo) |
| `Microsoft.AspNetCore.Identity` | Autenticación y autorización local |
| `SixLabors.ImageSharp` | Procesamiento de imágenes |
| `QuestPDF` | Generación de reportes PDF |
| `ClosedXML` | Exportación a Excel |
| `Serilog.AspNetCore` | Logging estructurado con rotación |
| `MailKit` | Envío de correos SMTP |

```powershell
dotnet restore
```

---

