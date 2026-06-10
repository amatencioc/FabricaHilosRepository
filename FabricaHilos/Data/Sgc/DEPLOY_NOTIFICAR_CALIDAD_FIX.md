# FIX: Cambio de Estado al Notificar a Calidad

## Problema
El botón "Enviar a Calidad" no desaparecía después de enviar exitosamente la notificación porque el stored procedure `P_NOTIFICAR_CALIDAD` no cambiaba el estado del reclamo de "01" (Abierto) a "02" (En Revisión).

## Solución
Se modificó el stored procedure `PKG_SGC_RECLAMO.P_NOTIFICAR_CALIDAD` para actualizar el estado del reclamo a "02" cuando se notifica a calidad.

## Cambios
En el Paso 4 de `P_NOTIFICAR_CALIDAD`, se agregó la actualización del campo `ESTADO`:

```sql
-- Antes:
UPDATE SGC_RECLAMO
SET    FCH_NOTI_CALIDAD = SYSDATE,
	   A_MDUSER         = SUBSTR(NVL(P_USUARIO,'SYS'), 1, 30),
	   A_MDFECHA        = SYSDATE
WHERE  ID_RECLAMO = P_ID_RECLAMO;

-- Después:
UPDATE SGC_RECLAMO
SET    ESTADO            = '02',
	   FCH_NOTI_CALIDAD = SYSDATE,
	   A_MDUSER         = SUBSTR(NVL(P_USUARIO,'SYS'), 1, 30),
	   A_MDFECHA        = SYSDATE
WHERE  ID_RECLAMO = P_ID_RECLAMO;
```

## Deploy
Ejecutar el script SQL para recompilar el package:

```bash
sqlplus usuario/contraseña@servidor @FabricaHilos/Data/Sgc/PKG_SGC_RECLAMO.sql
```

O en Toad:
1. Abrir el archivo `FabricaHilos/Data/Sgc/PKG_SGC_RECLAMO.sql`
2. Ejecutar el script completo (F5)
3. Verificar que no hay errores de compilación en el package body

## Verificación
Después del deploy, probar:
1. Crear un nuevo reclamo o usar uno en estado "01"
2. Hacer click en "Enviar a Calidad"
3. Confirmar que:
   - Se muestra el mensaje de éxito
   - El botón "Enviar a Calidad" desaparece
   - El estado del reclamo cambia a "02" (En Revisión)

## Notas
- Este cambio es compatible con Oracle 10g
- El cambio de estado es atómico junto con la actualización de `FCH_NOTI_CALIDAD`
