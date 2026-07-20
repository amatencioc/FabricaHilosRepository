// Ficha técnica de ruta (COT_RUTA_TECNICA_CAB/DET) — reemplaza el Excel manual de Preparatoria.
// Renderiza RutaTecnicaCabDto (con su lista Detalle) como panel informativo, tanto en Simular.cshtml
// (ficha VIGENTE, vía AJAX) como en Detalle.cshtml (ficha CONGELADA al momento de guardar, vía script tag).

(function (global) {
    'use strict';

    function escapeHtml(s) {
        return (s ?? '').toString()
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    function fmtFecha(iso) {
        if (!iso) return '-';
        const d = new Date(iso);
        if (isNaN(d.getTime())) return '-';
        return d.toLocaleDateString('es-PE');
    }

    /**
     * @param {HTMLElement} container
     * @param {object|null} ficha  RutaTecnicaCabDto (camelCase) o null si no existe ficha para el título.
     * @param {object} [opts]      { editUrl?: string } — si se pasa, muestra un link "Editar ficha técnica".
     */
    function render(container, ficha, opts) {
        if (!container) return;
        opts = opts || {};

        if (!ficha) {
            container.innerHTML = `
                <p class="text-muted small mb-0">
                    <i class="bi bi-info-circle me-1"></i>Preparatoria aún no registró una ficha técnica de ruta para este título.
                    ${opts.editUrl ? `<a href="${opts.editUrl}" target="_blank">Crear ficha técnica</a>.` : ''}
                </p>`;
            return;
        }

        const det = ficha.detalle || [];
        let html = '<div class="rt-ficha">';

        html += '<div class="row g-2 small mb-2">';
        if (ficha.productoDesc) html += `<div class="col-md-6"><strong>Producto:</strong> ${escapeHtml(ficha.productoDesc)}</div>`;
        if (ficha.clienteRef) html += `<div class="col-md-6"><strong>Cliente ref.:</strong> ${escapeHtml(ficha.clienteRef)}</div>`;
        if (ficha.fchActualizado) html += `<div class="col-md-3"><strong>Actualizado:</strong> ${fmtFecha(ficha.fchActualizado)}</div>`;
        if (ficha.pedidoMinKg != null || ficha.pedidoMaxKg != null) html += `<div class="col-md-3"><strong>Pedido:</strong> ${ficha.pedidoMinKg ?? '-'} - ${ficha.pedidoMaxKg ?? '-'} kg</div>`;
        if (ficha.lineaAlimPct) html += `<div class="col-md-3"><strong>Línea alim.:</strong> ${escapeHtml(ficha.lineaAlimPct)} ${ficha.lineaAlimDesc ? '(' + escapeHtml(ficha.lineaAlimDesc) + ')' : ''}</div>`;
        if (ficha.tonalidad) html += `<div class="col-md-3"><strong>Tonalidad:</strong> ${escapeHtml(ficha.tonalidad)}</div>`;
        html += '</div>';

        html += `<div class="table-responsive"><table class="sire-table w-100 rt-tabla-det">
            <thead><tr>
                <th>Sección</th><th class="text-end">%Merma</th><th class="text-end">Factor acum.</th><th class="text-end">N.H.</th>
                <th class="text-end">KG/H Máq</th><th class="text-end">KG/H Teórico</th><th class="text-end">Ne</th>
                <th class="text-end">%Efic</th><th class="text-end">Oper</th><th class="text-end">M/Min</th><th>Obs.</th>
            </tr></thead><tbody>`;
        // Cadena de rendimiento (hoja "Merma" del Excel manual): factor acumulado = producto de
        // 1/(1-%merma) etapa por etapa, en el mismo orden del flujo real (ORDEN de COT_RUTA_TECNICA_DET).
        let factorAcum = 1;
        const detOrdenado = det.slice().sort((a, b) => (a.orden ?? 0) - (b.orden ?? 0));
        detOrdenado.forEach(d => {
            const pct = Number(d.pctMerma);
            if (!isNaN(pct) && pct !== 0 && pct < 100) {
                factorAcum *= 1 / (1 - pct / 100);
            }
            html += `<tr>
                <td>${escapeHtml(d.seccion)}</td>
                <td class="text-end">${d.pctMerma ?? '-'}</td>
                <td class="text-end rt-factor-acum">${factorAcum.toFixed(6)}</td>
                <td class="text-end">${d.nroH ?? '-'}</td>
                <td class="text-end">${escapeHtml(d.kgHMaq) || '-'}</td>
                <td class="text-end">${escapeHtml(d.kgHMaqTeorico) || '-'}</td>
                <td class="text-end">${escapeHtml(d.ne) || '-'}</td>
                <td class="text-end">${escapeHtml(d.pctEfic) || '-'}</td>
                <td class="text-end">${escapeHtml(d.oper) || '-'}</td>
                <td class="text-end">${escapeHtml(d.mMin) || '-'}</td>
                <td class="small text-muted">${escapeHtml(d.obs)}</td>
            </tr>`;
        });
        if (det.length === 0) {
            html += '<tr><td colspan="11" class="text-center text-muted">Ficha sin detalle de secciones.</td></tr>';
        }
        html += '</tbody></table></div>';
        if (det.length > 0) {
            html += `<div class="rt-merma-total small">
                <strong>Factor de merma total (cascada de etapas):</strong> ${factorAcum.toFixed(6)}
                <span class="text-muted">— compárelo contra el factor que usa el motor de costeo
                (fila MERMA del panel "Ver detalle del cálculo"); si difieren, revisar %Merma por etapa arriba.</span>
            </div>`;
        }

        if (ficha.notaPedidoMin) html += `<div class="small text-muted">${escapeHtml(ficha.notaPedidoMin)}</div>`;
        if (ficha.notaPartida) html += `<div class="small text-muted">${escapeHtml(ficha.notaPartida)}</div>`;
        if (opts.editUrl) html += `<div class="mt-1"><a href="${opts.editUrl}" target="_blank" class="small"><i class="bi bi-pencil me-1"></i>Editar ficha técnica</a></div>`;

        html += '</div>';
        container.innerHTML = html;
    }

    global.RutaTecnica = { render };
})(window);
