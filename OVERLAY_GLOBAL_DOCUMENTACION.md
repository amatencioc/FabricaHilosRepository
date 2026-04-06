# Sistema de Overlay de Carga Global

## 📋 Descripción

Se ha implementado un **sistema global de overlay de carga** que se aplica automáticamente en toda la aplicación para proporcionar retroalimentación visual al usuario durante operaciones que requieren espera.

## 🎯 Características

El overlay se muestra automáticamente en las siguientes situaciones:

### 1. **Navegación entre páginas**
- Al hacer clic en cualquier enlace (`<a href="...">`)
- Mensaje: "Cargando página..."

### 2. **Envío de formularios**
- Al enviar cualquier formulario (submit)
- Mensajes contextuales:
  - "Buscando datos..." (formularios de búsqueda)
  - "Subiendo archivo..." (formularios con archivos)
  - "Guardando..." (formularios POST)
  - "Procesando..." (otros casos)

### 3. **Llamadas AJAX/Fetch**
- Intercepta todas las llamadas `fetch()`
- Mensajes según el método HTTP:
  - GET: "Cargando datos..."
  - POST: "Guardando..."
  - PUT: "Actualizando..."
  - DELETE: "Eliminando..."

## 🚀 Uso Automático

**No requiere configuración adicional.** El overlay funciona automáticamente en toda la aplicación.

## ⚙️ Configuración Avanzada

### Desactivar overlay en elementos específicos

#### Enlaces (Links)
```html
<!-- El overlay NO se mostrará al hacer clic en este enlace -->
<a href="/pagina" data-no-loading>Ir a página</a>
```

#### Formularios
```html
<!-- El overlay NO se mostrará al enviar este formulario -->
<form method="post" data-no-loading>
    <!-- campos del formulario -->
</form>
```

#### Llamadas Fetch
```javascript
// El overlay NO se mostrará para esta llamada
fetch('/api/datos', {
    method: 'POST',
    body: JSON.stringify(data),
    noLoading: true  // Opción personalizada
})
```

### Controlar el overlay manualmente

```javascript
// Mostrar overlay con mensaje personalizado
GlobalLoading.show('Procesando archivo grande...');

// Ocultar overlay
GlobalLoading.hide();
```

## 📁 Archivos Modificados

### 1. **FabricaHilos\wwwroot\css\site.css**
- Estilos CSS para el overlay global
- Animación del spinner
- z-index: 99999 para estar sobre todos los elementos

### 2. **FabricaHilos\Views\Shared\_Layout.cshtml**
- HTML del overlay global (disponible en todas las páginas)
- Estructura: overlay → spinner → mensaje → submensaje

### 3. **FabricaHilos\wwwroot\js\site.js**
- Objeto `GlobalLoading` con toda la lógica
- Interceptores de eventos (links, forms, fetch)
- Gestión automática del overlay

### 4. **FabricaHilos\Views\Sgc\Despachos\CargaTc\Detalle.cshtml**
- Actualizado para usar overlay local + global coordinadamente
- Usa `noLoading: true` para evitar conflicto con overlay local

## 🎨 Personalización

### Cambiar el mensaje del overlay

El mensaje se determina automáticamente según el contexto, pero puedes personalizarlo:

```javascript
document.addEventListener('submit', (e) => {
    if (e.target.id === 'miFormularioEspecial') {
        GlobalLoading.show('Enviando email...');
    }
});
```

### Cambiar estilos del overlay

Edita las clases en `site.css`:

```css
.global-loading-overlay {
    background-color: rgba(0, 0, 0, 0.7);  /* Cambiar opacidad */
}

.global-loading-spinner {
    width: 4rem;  /* Cambiar tamaño del spinner */
    border: 0.4rem solid rgba(255, 255, 255, 0.3);
    border-top-color: #fff;  /* Cambiar color del spinner */
}
```

## 🔍 Casos Especiales

### Enlaces que NO muestran overlay:
- `href="#"` o enlaces vacíos
- Enlaces con `target="_blank"` (nueva ventana)
- Enlaces de descarga (`download` attribute)
- Enlaces de Bootstrap collapse (`data-bs-toggle`)
- Enlaces de logout

### Formularios con overlay local
Páginas como `Detalle.cshtml` tienen su propio overlay para operaciones específicas (subida de archivos). En estos casos:
- Se usa `noLoading: true` en fetch para evitar el overlay global
- El overlay local tiene mayor prioridad y control específico

## 🐛 Solución de Problemas

### El overlay no se oculta
```javascript
// Forzar ocultación del overlay
GlobalLoading.activeRequests = 0;
GlobalLoading.hide();
```

### Múltiples overlays simultáneos
El sistema usa un contador (`activeRequests`) para manejar múltiples operaciones:
- Cada operación incrementa el contador
- El overlay solo se oculta cuando el contador llega a 0

### Conflicto con overlay local
Usa `noLoading: true` en fetch o `data-no-loading` en HTML para desactivar el overlay global.

## 📊 Ventajas

✅ **Experiencia de Usuario Mejorada**: El usuario siempre sabe que algo está pasando
✅ **Prevención de Doble Click**: El overlay bloquea la interacción durante operaciones
✅ **Consistencia**: Mismo comportamiento en toda la aplicación
✅ **Sin Configuración**: Funciona automáticamente sin código adicional
✅ **Personalizable**: Fácil de ajustar para casos específicos

## 🔄 Migración de Código Existente

Si tienes páginas con overlays locales:

1. **Mantén el overlay local** para operaciones específicas (carga de archivos, etc.)
2. **Agrega `noLoading: true`** en fetch para evitar el overlay global
3. **Elimina código redundante** si el overlay global es suficiente

Ejemplo:
```javascript
// ANTES
formBusqueda.addEventListener('submit', () => {
    miOverlay.classList.add('show');
});

// DESPUÉS (el overlay global se encarga automáticamente)
// No se necesita código adicional
```

## 🎯 Mejores Prácticas

1. **Mensajes claros**: Los mensajes automáticos son descriptivos ("Guardando...", "Buscando datos...")
2. **No abusar de data-no-loading**: Solo úsalo cuando realmente no necesites feedback visual
3. **Operaciones largas**: Para operaciones muy largas, considera un mensaje más descriptivo
4. **Testing**: Prueba navegación, formularios y AJAX en tu página después de implementar

## 📞 Soporte

Para preguntas o problemas, revisa:
- `FabricaHilos\wwwroot\js\site.js` → Lógica del overlay
- `FabricaHilos\wwwroot\css\site.css` → Estilos del overlay
- Consola del navegador → Errores de JavaScript

---

**Versión**: 1.0  
**Fecha**: 2024  
**Proyecto**: Fábrica de Hilos - Sistema de Gestión Empresarial
