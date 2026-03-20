# FabricaHilos.LecturaCorreos

Worker Service de .NET 9 que consulta al Web Service SOAP de SUNAT para verificar si una factura electrónica ha sido aprobada (tiene su CDR — Constancia de Recepción) y guarda el resultado en Oracle enlazado al documento/factura correspondiente.

## Propósito

Este servicio consulta periódicamente la tabla `FH_LECTCORREOS_FACTURAS` en Oracle buscando facturas con estado `PENDIENTE_CDR` y, para cada una, invoca el servicio `getStatusCdr` del Web Service SOAP de SUNAT. Según la respuesta:

- **ACEPTADO** (código `"0"`): actualiza el estado, guarda el CDR (ZIP binario) en el campo `CDR_CONTENIDO`.
- **RECHAZADO** (códigos `2xx` / `4xx`): actualiza el estado a `RECHAZADO` con el código y mensaje de SUNAT.
- **En proceso**: registra el mensaje informativo y reintenta en el siguiente ciclo (máximo 5 intentos).

## Configuración

Edite el archivo `appsettings.json` con los valores de su entorno:

```json
{
  "ConnectionStrings": {
    "OracleConnection": "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=<SERVIDOR>)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=<SERVICIO>)));User Id=<USUARIO>;Password=<CONTRASENA>;"
  },
  "Sunat": {
    "Ruc": "<RUC_EMISOR>",
    "UsuarioSol": "<USUARIO_SOL>",
    "ClaveSol": "<CLAVE_SOL>",
    "EndpointConsultaCdr": "https://e-factura.sunat.gob.pe/ol-it-wsconscpegem/billConsultService",
    "IntervaloConsultaMinutos": 10
  }
}
```

Para desarrollo/pruebas con el ambiente beta de SUNAT, configure `appsettings.Development.json`:

```json
{
  "Sunat": {
    "EndpointConsultaCdr": "https://e-beta.sunat.gob.pe/ol-it-wsconscpegem-beta/billConsultService"
  }
}
```

## Script DDL Oracle

Ejecute el siguiente script en su base de datos Oracle antes de iniciar el servicio:

```sql
CREATE TABLE FH_LECTCORREOS_FACTURAS (
    ID                      NUMBER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    RUC                     VARCHAR2(11)   NOT NULL,
    TIPO_COMPROBANTE        VARCHAR2(2)    NOT NULL,    -- 01=Factura, 03=Boleta
    SERIE                   VARCHAR2(4)    NOT NULL,
    CORRELATIVO             NUMBER         NOT NULL,
    ESTADO                  VARCHAR2(20)   DEFAULT 'PENDIENTE_CDR' NOT NULL,
    CODIGO_RESPUESTA_SUNAT  VARCHAR2(10),
    MENSAJE_SUNAT           VARCHAR2(500),
    CDR_CONTENIDO           BLOB,
    MENSAJE_ERROR           VARCHAR2(1000),
    FECHA_CREACION          DATE           DEFAULT SYSDATE NOT NULL,
    FECHA_CONSULTA_SUNAT    DATE,
    INTENTOS                NUMBER         DEFAULT 0 NOT NULL,
    DOCUMENTO_ID            NUMBER,        -- FK al documento/factura emitida
    DOCUMENTO_REFERENCIA    VARCHAR2(100)  -- Referencia legible del documento
);

CREATE INDEX IDX_FLCF_ESTADO ON FH_LECTCORREOS_FACTURAS(ESTADO, INTENTOS);
CREATE INDEX IDX_FLCF_DOCUMENTO ON FH_LECTCORREOS_FACTURAS(DOCUMENTO_ID);
```

El sistema externo (que emite las facturas) inserta una fila en esta tabla con `ESTADO = 'PENDIENTE_CDR'` y el `DOCUMENTO_ID` correspondiente. Este servicio solo actualiza el estado del CDR.

## Ejecución como Windows Service

### 1. Publicar la aplicación

```powershell
dotnet publish -c Release -o C:\Servicios\LecturaCorreos
```

### 2. Registrar el servicio

```powershell
sc.exe create "FabricaHilosLecturaCorreos" binPath="C:\Servicios\LecturaCorreos\FabricaHilos.LecturaCorreos.exe"
sc.exe description "FabricaHilosLecturaCorreos" "Consulta CDR de facturas electrónicas en SUNAT"
sc.exe start "FabricaHilosLecturaCorreos"
```

### 3. Detener y eliminar el servicio

```powershell
sc.exe stop "FabricaHilosLecturaCorreos"
sc.exe delete "FabricaHilosLecturaCorreos"
```

## Ejecución en consola (desarrollo)

```bash
dotnet run --environment Development
```
