// Fábrica de Hilos - JavaScript del sitio

// =============================================
// OVERLAY DE CARGA GLOBAL
// =============================================
const GlobalLoading = {
    overlay: null,
    messageElement: null,
    activeRequests: 0,

    init: function() {
        this.overlay = document.getElementById('globalLoadingOverlay');
        this.messageElement = document.getElementById('loadingMessage');

        // Interceptar todos los clicks en enlaces (navegación)
        this.interceptLinks();

        // Interceptar todos los envíos de formularios
        this.interceptForms();

        // Interceptar llamadas fetch
        this.interceptFetch();

        // Ocultar overlay cuando la página termine de cargar
        window.addEventListener('load', () => {
            this.hide();
        });

        // Ocultar overlay al recibir la página (incluye bfcache de iOS Safari)
        // event.persisted === true cuando la página viene del back-forward cache
        window.addEventListener('pageshow', (event) => {
            // Forzar reset completo del contador para que el overlay siempre desaparezca
            this.activeRequests = 0;
            this.hide();
        });
    },

    show: function(message = 'Cargando...') {
        if (this.overlay && this.messageElement) {
            this.messageElement.textContent = message;
            this.overlay.classList.add('show');
            this.activeRequests++;
        }
    },

    hide: function() {
        if (this.overlay) {
            this.activeRequests--;
            if (this.activeRequests <= 0) {
                this.activeRequests = 0;
                this.overlay.classList.remove('show');
            }
        }
    },

    interceptLinks: function() {
        document.addEventListener('click', (e) => {
            const link = e.target.closest('a');
            if (link && link.href && !link.hasAttribute('data-no-loading')) {
                const href = link.getAttribute('href');

                // Ignorar enlaces con:
                // - href="#" o vacío
                // - target="_blank" (nueva ventana)
                // - descarga de archivos
                // - collapse de Bootstrap
                // - logout
                if (href && 
                    href !== '#' && 
                    !href.startsWith('#') &&
                    !link.hasAttribute('target') &&
                    !link.hasAttribute('download') &&
                    !link.hasAttribute('data-bs-toggle') &&
                    !href.toLowerCase().includes('logout')) {

                    this.show('Cargando página...');
                }
            }
        });
    },

    interceptForms: function() {
        document.addEventListener('submit', (e) => {
            const form = e.target;
            if (form && !form.hasAttribute('data-no-loading')) {
                // Determinar el mensaje según el tipo de formulario
                let message = 'Procesando...';

                if (form.id.toLowerCase().includes('buscar') || form.querySelector('[name*="buscar"]')) {
                    message = 'Buscando datos...';
                } else if (form.enctype === 'multipart/form-data') {
                    message = 'Subiendo archivo...';
                } else if (form.method.toLowerCase() === 'post') {
                    message = 'Guardando...';
                }

                this.show(message);
            }
        });
    },

    interceptFetch: function() {
        // Guardar el fetch original
        const originalFetch = window.fetch;
        const self = this;

        window.fetch = function(...args) {
            // Verificar si la URL tiene el atributo data-no-loading
            const url = args[0];
            const options = args[1] || {};

            if (!options.noLoading) {
                // Determinar mensaje según el método
                const method = (options.method || 'GET').toUpperCase();
                let message = 'Cargando datos...';

                if (method === 'POST') {
                    message = 'Guardando...';
                } else if (method === 'PUT') {
                    message = 'Actualizando...';
                } else if (method === 'DELETE') {
                    message = 'Eliminando...';
                }

                self.show(message);
            }

            return originalFetch.apply(this, args)
                .then(response => {
                    if (!options.noLoading) {
                        self.hide();
                    }
                    return response;
                })
                .catch(error => {
                    if (!options.noLoading) {
                        self.hide();
                    }
                    throw error;
                });
        };
    }
};

// Toggle del sidebar en cualquier tamaño de pantalla
document.addEventListener('DOMContentLoaded', function () {
    // Inicializar overlay de carga global
    GlobalLoading.init();

    const toggleBtn = document.getElementById('sidebarToggle');
    const sidebar = document.getElementById('sidebar');

    if (toggleBtn && sidebar) {
        // Sidebar siempre oculto al cargar la página
        sidebar.classList.add('sidebar-collapsed');

        toggleBtn.addEventListener('click', function () {
            sidebar.classList.toggle('sidebar-collapsed');
        });
    }

    // Auto-cerrar alertas después de 5 segundos
    const alertas = document.querySelectorAll('.alert.alert-success');
    alertas.forEach(function (alerta) {
        setTimeout(function () {
            const bsAlert = new bootstrap.Alert(alerta);
            bsAlert.close();
        }, 5000);
    });
});

// =============================================
// MODALES GLOBALES: AppAlert y AppConfirm
// =============================================
const AppModal = {
    _alertModal: null,
    _confirmModal: null,
    _confirmResolve: null,

    _getOrCreateAlert: function () {
        let el = document.getElementById('appAlertModal');
        if (!el) {
            el = document.createElement('div');
            el.innerHTML = `
<div class="modal fade" id="appAlertModal" tabindex="-1" aria-hidden="true">
  <div class="modal-dialog modal-dialog-centered">
    <div class="modal-content">
      <div class="modal-header" id="appAlertHeader">
        <h5 class="modal-title" id="appAlertTitle">Aviso</h5>
        <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
      </div>
      <div class="modal-body">
        <p id="appAlertMessage" class="mb-0"></p>
      </div>
      <div class="modal-footer">
        <button type="button" class="btn btn-primary" data-bs-dismiss="modal">Aceptar</button>
      </div>
    </div>
  </div>
</div>`;
            document.body.appendChild(el.firstElementChild);
            el = document.getElementById('appAlertModal');
        }
        return el;
    },

    _getOrCreateConfirm: function () {
        let el = document.getElementById('appConfirmModal');
        if (!el) {
            el = document.createElement('div');
            el.innerHTML = `
<div class="modal fade" id="appConfirmModal" tabindex="-1" aria-hidden="true">
  <div class="modal-dialog modal-dialog-centered">
    <div class="modal-content">
      <div class="modal-header">
        <h5 class="modal-title" id="appConfirmTitle">Confirmar</h5>
        <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
      </div>
      <div class="modal-body">
        <p id="appConfirmMessage" class="mb-0"></p>
      </div>
      <div class="modal-footer">
        <button type="button" class="btn btn-secondary" id="appConfirmCancel" data-bs-dismiss="modal">Cancelar</button>
        <button type="button" class="btn btn-danger"    id="appConfirmOk">Confirmar</button>
      </div>
    </div>
  </div>
</div>`;
            document.body.appendChild(el.firstElementChild);
            el = document.getElementById('appConfirmModal');
        }
        return el;
    }
};

/**
 * Reemplaza alert(). Muestra un modal de aviso.
 * @param {string} message  Texto del mensaje
 * @param {string} [title]  Título opcional (default: "Aviso")
 * @param {string} [type]   "info"|"warning"|"danger"|"success" (default: "info")
 * @returns {Promise<void>} Se resuelve cuando el usuario cierra el modal
 */
function AppAlert(message, title, type) {
    return new Promise(function (resolve) {
        const el = AppModal._getOrCreateAlert();
        const headerEl = document.getElementById('appAlertHeader');
        const titleEl  = document.getElementById('appAlertTitle');
        const msgEl    = document.getElementById('appAlertMessage');

        titleEl.textContent = title || 'Aviso';
        msgEl.textContent   = message || '';

        // Color del header según tipo
        const typeMap = { success: 'bg-success text-white', warning: 'bg-warning', danger: 'bg-danger text-white', info: 'bg-info text-white' };
        headerEl.className = 'modal-header ' + (typeMap[type] || typeMap['info']);

        const modal = bootstrap.Modal.getOrCreateInstance(el);
        el.addEventListener('hidden.bs.modal', function handler() {
            el.removeEventListener('hidden.bs.modal', handler);
            resolve();
        });
        modal.show();
    });
}

/**
 * Reemplaza confirm(). Muestra un modal de confirmación.
 * @param {string} message       Texto de la pregunta
 * @param {string} [title]       Título opcional (default: "Confirmar")
 * @param {string} [okLabel]     Texto del botón OK (default: "Confirmar")
 * @param {string} [cancelLabel] Texto del botón Cancelar (default: "Cancelar")
 * @returns {Promise<boolean>}   true si el usuario confirma, false si cancela
 */
function AppConfirm(message, title, okLabel, cancelLabel) {
    return new Promise(function (resolve) {
        const el      = AppModal._getOrCreateConfirm();
        const titleEl = document.getElementById('appConfirmTitle');
        const msgEl   = document.getElementById('appConfirmMessage');
        const okBtn   = document.getElementById('appConfirmOk');
        const cancelBtn = document.getElementById('appConfirmCancel');

        titleEl.textContent     = title        || 'Confirmar';
        msgEl.textContent       = message      || '¿Está seguro?';
        okBtn.textContent       = okLabel      || 'Confirmar';
        cancelBtn.textContent   = cancelLabel  || 'Cancelar';

        const modal = bootstrap.Modal.getOrCreateInstance(el);

        function onOk() {
            cleanup();
            modal.hide();
            resolve(true);
        }
        function onCancel() {
            cleanup();
            resolve(false);
        }
        function cleanup() {
            okBtn.removeEventListener('click', onOk);
            el.removeEventListener('hidden.bs.modal', onCancel);
        }

        okBtn.addEventListener('click', onOk);
        el.addEventListener('hidden.bs.modal', function handler() {
            el.removeEventListener('hidden.bs.modal', handler);
            onCancel();
        });

        modal.show();
    });
}
