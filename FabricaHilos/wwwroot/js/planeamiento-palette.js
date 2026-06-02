/**
 * planeamiento-palette.js
 * ─────────────────────────────────────────────────────────────────────────────
 * Lee en runtime las variables CSS definidas en planeamiento.css (:root)
 * y las expone en window.PlnColors para que todos los charts de Planeamiento
 * (ApexCharts, Chart.js, etc.) usen el mismo sistema de color.
 *
 * USO en cualquier vista de Planeamiento:
 *   const c = window.PlnColors;
 *   colors: [c.navy, c.blueMid]
 * ─────────────────────────────────────────────────────────────────────────────
 */
(function () {
    const s = getComputedStyle(document.documentElement);
    const v = (name) => s.getPropertyValue(name).trim();

    window.PlnColors = {
        /* ── Paleta navy ── */
        navy:        v('--pln-navy'),
        navyMid:     v('--pln-navy-mid'),
        navyLight:   v('--pln-navy-light'),
        blueLink:    v('--pln-blue-link'),
        bluePale:    v('--pln-blue-pale'),
        steel:       v('--pln-steel'),
        steelBg:     v('--pln-steel-bg'),

        /* ── Escala gris ── */
        darkDeep:    v('--pln-dark-deep'),
        darkBg:      v('--pln-dark-bg'),
        gray700:     v('--pln-gray-700'),
        gray600:     v('--pln-gray-600'),
        gray500:     v('--pln-gray-500'),
        gray300:     v('--pln-gray-300'),
        gray200:     v('--pln-gray-200'),
        gray100:     v('--pln-gray-100'),
        gray50:      v('--pln-gray-50'),

        /* ── Texto sobre fondos oscuros ── */
        lightText:   v('--pln-light-text'),
        lightMuted:  v('--pln-light-muted'),
        accentBlue:  v('--pln-accent-blue'),

        /* ── Estados semáforo ── */
        danger:      v('--pln-danger'),
        warning:     v('--pln-warning'),
        successDk:   v('--pln-success-dk'),
        successLt:   v('--pln-success-lt'),
        blueDk:      v('--pln-blue-dk'),
        blueLt:      v('--pln-blue-lt'),
        blueBg:      v('--pln-blue-bg'),
        blueBright:  v('--pln-blue-bright'),

        /* ── Ámbar / strip urgencia ── */
        amberDk:     v('--pln-amber-dk'),
        amberMid:    v('--pln-amber-mid'),
        amberBorder: v('--pln-amber-border'),
        amberText:   v('--pln-amber-text'),

        /* ── Ok / retraso (charts) ── */
        ok:          v('--pln-ok'),
        okDk:        v('--pln-ok-dk'),
        late:        v('--pln-late'),
        lateDk:      v('--pln-late-dk'),

        /* ── Indigo preset ── */
        indigo:      v('--pln-indigo'),
    };
})();
