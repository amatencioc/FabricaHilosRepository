# 🧪 Scripts SIRE - Guía Rápida

## 📍 Estado Actual
- **Modo**: 🟡 Mock Mode ACTIVO
- **Status**: ✅ Funcional
- **Documento**: `../Docs/SIRE_ESTADO_ACTUAL.md`

---

## 📋 Scripts Disponibles

### 1. **Validar-MockMode.ps1** ← ⭐ COMIENZA AQUÍ
Valida que todos los endpoints SIRE funcionan correctamente.

```powershell
.\Validar-MockMode.ps1
```

**Prueba**: 6 endpoints (Health, Diagnostico, Dashboard, RVIE, RCE, History)  
**Esperado**: ✅ TODOS LOS TESTS PASARON

---

### 2. **Probar-SireOperaciones.ps1**
Script completo de validación de operaciones RVIE y RCE.

```powershell
.\Probar-SireOperaciones.ps1
```

**Prueba**: 
- Endpoints SIRE
- Consultar periodos
- Operaciones RVIE/RCE
- Monitoreo y Health History

**Esperado**: 10/10 tests exitosos

---

### 3. **Test-SunatAuth.ps1**
Prueba autenticación directa con SUNAT (para cuando ResuelVas permisos).

```powershell
.\Test-SunatAuth.ps1
```

**Nota**: Falla mientras no tengas permisos API en SUNAT

---

### 4. **Test-SunatAuth-DosAplicaciones.ps1**
Prueba las 2 primeras aplicaciones SUNAT registradas.

```powershell
.\Test-SunatAuth-DosAplicaciones.ps1
```

---

### 5. **Test-SunatAuth-Variaciones.ps1**
Prueba variaciones de contraseña (ya ejecutado, útil para debug).

```powershell
.\Test-SunatAuth-Variaciones.ps1
```

---

### 6. **Test-SunatAuth-MultiFormat.ps1**
Prueba diferentes formatos de ClientSecret.

```powershell
.\Test-SunatAuth-MultiFormat.ps1
```

---

### 7. **Test-SunatAuth-AppNueva.ps1**
Prueba la aplicación registrada hoy (06/06/2026).

```powershell
.\Test-SunatAuth-AppNueva.ps1
```

---

## 🚀 FLUJO RECOMENDADO

### Para Desarrollo (Ahora)
```powershell
# 1. Validar que todo funciona
.\Validar-MockMode.ps1

# 2. Ejecutar pruebas completas
.\Probar-SireOperaciones.ps1

# 3. Hacer cambios en código
# ... tu desarrollo ...

# 4. Volver a ejecutar scripts si hiciste cambios críticos
```

### Cuando Resuelvas Permisos SUNAT
```powershell
# 1. Cambiar UseMock a false en appsettings.json

# 2. Probar autenticación real
.\Test-SunatAuth-AppNueva.ps1

# 3. Si funciona, ejecutar operaciones reales
.\Probar-SireOperaciones.ps1

# 4. Monitorear en /Sire/HealthHistory
```

---

## 📊 Salida Esperada

### ✅ MockMode Funciona
```
✅ Health Check SIRE ............................ 200 (OK)
✅ Diagnostico JSON ............................ 200 (OK)
✅ Dashboard SIRE .............................. 200 (OK)
✅ UI RVIE (Ventas) ............................ 200 (OK)
✅ UI RCE (Compras) ............................ 200 (OK)
✅ Historial de Monitoreo ....................... 200 (OK)

🎉 ¡TODOS LOS TESTS PASARON!
```

### ❌ Problema?
```
Verifica que:
  1. La aplicación está corriendo: dotnet run
  2. URL correcta: http://localhost:5000
  3. UseMock está en true en appsettings.json
```

---

## 🔍 Troubleshooting

### "No se puede conectar a localhost:5000"
```
→ Asegúrate que Visual Studio está ejecutando la app
→ O ejecuta: dotnet run en terminal
```

### "Status 404 Not Found"
```
→ Verifica que los endpoints existen en SireController.cs
→ Mira que UseMock está en true
```

### "Status 500 Error"
```
→ Revisa Visual Studio Output window
→ Ve a /Sire/DiagnosticoJson para más detalles
```

---

## 📚 Documentación Relacionada

- `../Docs/SIRE_ESTADO_ACTUAL.md` - 📍 **DOCUMENTO PRINCIPAL**
- `../Docs/RESUMEN_SIRE_MOCK.md` - Resumen ejecutivo
- `../Docs/SIRE_MONITOREO_Y_RESUMEN.md` - Detalles técnicos
- `../Docs/SIRE_AUTENTICACION_TROUBLESHOOTING.md` - Guía troubleshooting

---

**Última actualización**: 06/06/2026  
**Modo**: 🟡 Mock (para desarrollo)  
**Próximo paso**: Resolver permisos SISTEM10 con SUNAT
