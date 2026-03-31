# FabricaHilos — Sistema de Gestión

Sistema ASP.NET Core MVC para la gestión de producción, inventario, ventas, facturación, SGC y seguridad de la fábrica de hilos La Colonial.

---

## Requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Acceso de escritura a la carpeta de red de seguridad (ver configuración)

---

## Configuración inicial

### 1. Crear `appsettings.json`

Copia el archivo de ejemplo y completa los valores reales:

```bash
cp FabricaHilos/appsettings-template.json FabricaHilos/appsettings.json
```

El archivo `appsettings.json` está excluido del control de versiones (`.gitignore`) porque contiene cadenas de conexión y rutas sensibles.

### 2. Configurar la ruta de seguridad

En `appsettings.json`, asegúrate de que la clave `RutaSeguridad` apunte a la carpeta de red correcta:

```json
{
  "RutaSeguridad": "\\\\10.0.7.14\\6-20100096260\\Seguridad"
}
```

> **Nota:** El servidor/proceso de la aplicación debe tener permisos de escritura en esa ruta de red.

---

## Dependencias NuGet destacadas

| Paquete | Versión | Uso |
|---|---|---|
| `SixLabors.ImageSharp` | 3.1.12 | Procesamiento y optimización de imágenes de seguridad |
| `QuestPDF` | 2024.12.0 | Generación de PDFs |
| `ClosedXML` | 0.102.3 | Exportación a Excel |
| `Oracle.ManagedDataAccess.Core` | 23.26.100 | Conexión a base de datos Oracle |

### Instalar / restaurar paquetes

```bash
cd FabricaHilos
dotnet restore
```

Para agregar `SixLabors.ImageSharp` manualmente (ya incluido en el proyecto):

```bash
dotnet add package SixLabors.ImageSharp --version 3.1.12
```

---

## Módulo de Seguridad — Subida de Fotos

### Funcionalidad

- Accede en `/Seguridad/SubirFoto`.
- Permite tomar una foto desde la cámara del celular (cámara trasera) o seleccionar una imagen existente.
- Formatos aceptados: `.jpg`, `.jpeg`, `.png`, `.webp`.
- La imagen se redimensiona automáticamente a un máximo de **1600 px** por lado, manteniendo la relación de aspecto.
- Se comprime a **JPEG 75 %** de calidad antes de guardarla.
- El archivo se almacena en la ruta de red configurada con un nombre único (UUID).

### Archivos del módulo

| Tipo | Ruta |
|---|---|
| Modelo | `Models/Seguridad/FotoSeguridadViewModel.cs` |
| Servicio | `Services/Seguridad/ProcesadorImagenSeguridad.cs` |
| Controlador | `Controllers/SeguridadController.cs` |
| Vista | `Views/Seguridad/SubirFoto.cshtml` |

---

## Ejecutar en desarrollo

```bash
cd FabricaHilos
dotnet run
```

La aplicación estará disponible en `http://localhost:44300`.
