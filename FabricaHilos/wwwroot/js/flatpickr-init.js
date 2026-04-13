/**
 * flatpickr-init.js
 * Auto-inicializa flatpickr en todos los inputs type="date" y type="datetime-local"
 * para mostrar formato dd/mm/aaaa (o dd/mm/aaaa HH:mm) en español,
 * enviando siempre el valor en formato ISO al servidor.
 */
(function () {
    'use strict';

    function initFlatpickr() {
        // ── Inputs type="date" ──────────────────────────────────────
        document.querySelectorAll('input[type="date"]').forEach(function (el) {
            // Evitar doble inicialización
            if (el._flatpickr) return;

            flatpickr(el, {
                locale: 'es',
                dateFormat: 'Y-m-d',
                altInput: true,
                altFormat: 'd/m/Y',
                allowInput: true,
                disableMobile: true
            });
        });

        // ── Inputs type="datetime-local" ────────────────────────────
        document.querySelectorAll('input[type="datetime-local"]').forEach(function (el) {
            if (el._flatpickr) return;

            // Leer atributos del input original
            var stepAttr = el.getAttribute('step');
            var enableSeconds = stepAttr === '1';
            var minDate = el.getAttribute('min') || undefined;
            var maxDate = el.getAttribute('max') || undefined;

            flatpickr(el, {
                locale: 'es',
                enableTime: true,
                time_24hr: true,
                enableSeconds: enableSeconds,
                dateFormat: 'Y-m-d\\TH:i' + (enableSeconds ? ':S' : ''),
                altInput: true,
                altFormat: 'd/m/Y H:i' + (enableSeconds ? ':S' : ''),
                allowInput: true,
                disableMobile: true,
                minDate: minDate,
                maxDate: maxDate
            });
        });

        // ── Inputs type="text" ya preparados para flatpickr (ej: ListadoDespachos) ──
        document.querySelectorAll('input[data-flatpickr="date"]').forEach(function (el) {
            if (el._flatpickr) return;

            flatpickr(el, {
                locale: 'es',
                dateFormat: 'Y-m-d',
                altInput: true,
                altFormat: 'd/m/Y',
                allowInput: true,
                disableMobile: true
            });
        });
    }

    // Ejecutar cuando el DOM esté listo
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initFlatpickr);
    } else {
        initFlatpickr();
    }

    // Exponer para re-inicialización dinámica si se agregan inputs por JS
    window.initFlatpickr = initFlatpickr;

    /**
     * Establecer fecha en un input con flatpickr (actualiza tanto el value como el display).
     * @param {string|HTMLElement} el - Selector CSS o elemento del input original
     * @param {string} value - Fecha en formato ISO (YYYY-MM-DD o YYYY-MM-DDTHH:mm)
     */
    window.fpSetDate = function (el, value) {
        var input = typeof el === 'string' ? document.querySelector(el) : el;
        if (!input) return;
        if (input._flatpickr) {
            input._flatpickr.setDate(value, false);
        } else {
            input.value = value;
        }
    };

    /**
     * Habilitar o deshabilitar un input con flatpickr.
     * @param {string|HTMLElement} el - Selector CSS o elemento del input original
     * @param {boolean} disabled - true para deshabilitar, false para habilitar
     */
    window.fpSetDisabled = function (el, disabled) {
        var input = typeof el === 'string' ? document.querySelector(el) : el;
        if (!input) return;
        if (input._flatpickr) {
            input._flatpickr.altInput.disabled = disabled;
            input.disabled = disabled;
        } else {
            input.disabled = disabled;
        }
    };
})();
