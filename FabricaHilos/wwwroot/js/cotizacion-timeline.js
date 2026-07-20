// Cotización — línea de tiempo de costeo (Camino A / Camino B)
// Renderiza el resultado de CotizacionTimelineDto (pasos + resumen) como una línea de tiempo vertical.

(function (global) {
    'use strict';

    function fmtMoney(v) {
        const n = Number(v ?? 0);
        return n.toLocaleString('es-PE', { minimumFractionDigits: 4, maximumFractionDigits: 4 });
    }

    function escapeHtml(s) {
        return (s ?? '').toString()
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    function buscarDetalle(detalle, componente) {
        return (detalle || []).find(d => d.componente === componente) || null;
    }

    /** Bloque de bullets a partir del texto "DETALLE" (segmentos separados por " | "). */
    function bulletsDetalle(texto) {
        if (!texto) return '';
        return texto.split(' | ').map(t => `<div>&bull; ${escapeHtml(t)}</div>`).join('');
    }

    const ETIQUETAS_PROCESO = { '01': 'Cardado', '20': 'Peinado', '24': 'Peinado Gaseado' };
    const ETIQUETAS_INTENSIDAD = { '0': 'Crudo', '5': 'Blanco', '1': 'Claro', '2': 'Medio', '3': 'Oscuro', '4': 'Intenso' };
    const ETIQUETAS_PRESENTACION = { MADEJA: 'Madeja', CONO: 'Cono', RODETE: 'Rodete' };

    /**
     * Renderiza la línea de tiempo dentro de un contenedor.
     * @param {HTMLElement} container
     * @param {object} timeline  CotizacionTimelineDto
     */
    function renderTimeline(container, timeline) {
        if (!container) return;
        if (!timeline || !timeline.pasos) {
            container.innerHTML = '<p class="text-muted small">Sin datos para mostrar.</p>';
            return;
        }

        const componentes = timeline.pasos.filter(p => p.grupo === 'componente');
        const resumen = timeline.pasos.filter(p => p.grupo === 'resumen');

        let html = '<div class="cot-timeline">';
        componentes.forEach((p, idx) => {
            html += `
                <div class="cot-timeline-item d-flex">
                    <div class="cot-timeline-marker" style="--cot-color:${p.color}">
                        <i class="bi ${p.icono}"></i>
                    </div>
                    <div class="cot-timeline-content flex-fill">
                        <div class="d-flex justify-content-between align-items-center">
                            <span class="fw-semibold">${p.etiquetaCorta}</span>
                            <span class="badge" style="background-color:${p.color}">${fmtMoney(p.costoUsdKg)} USD/kg</span>
                        </div>
                        ${p.notas ? `<div class="small text-muted">${p.notas}</div>` : ''}
                    </div>
                </div>`;
        });
        html += '</div>';

        if (resumen.length > 0) {
            html += '<div class="row g-2 mt-2">';
            resumen.forEach(p => {
                html += `
                    <div class="col-6 col-md-4">
                        <div class="border rounded p-2 text-center h-100">
                            <div class="small text-muted">${p.etiquetaCorta}</div>
                            <div class="fw-bold">${fmtMoney(p.costoUsdKg)}</div>
                        </div>
                    </div>`;
            });
            html += '</div>';
        }

        html += `
            <div class="row g-2 mt-3">
                <div class="col-6 col-md-3">
                    <div class="border rounded p-2 text-center bg-success-subtle">
                        <div class="small text-muted">Costo Total</div>
                        <div class="fw-bold">${fmtMoney(timeline.costoTotal)}</div>
                    </div>
                </div>
                <div class="col-6 col-md-3">
                    <div class="border rounded p-2 text-center">
                        <div class="small text-muted">Precio 25%</div>
                        <div class="fw-bold">${fmtMoney(timeline.precio25)}</div>
                    </div>
                </div>
                <div class="col-6 col-md-3">
                    <div class="border rounded p-2 text-center bg-primary-subtle">
                        <div class="small text-muted">Precio 30%</div>
                        <div class="fw-bold">${fmtMoney(timeline.precio30)}</div>
                    </div>
                </div>
                <div class="col-6 col-md-3">
                    <div class="border rounded p-2 text-center">
                        <div class="small text-muted">Precio 35% / 40%</div>
                        <div class="fw-bold">${fmtMoney(timeline.precio35)} / ${fmtMoney(timeline.precio40)}</div>
                    </div>
                </div>
            </div>`;

        container.innerHTML = html;
    }

    /**
     * "Hoja 1 — Datos Base": título/ruta resuelta, materia prima y parámetros del pedido,
     * con la fuente exacta (F_COTIZAR_DETALLE) de cada dato. Equivalente al archivo
     * "Datos Base" del Excel manual de costeo.
     * @param {HTMLElement} container
     * @param {object} timeline  CotizacionTimelineDto (se usa timeline.parametros)
     * @param {Array} detalle    Array de CotizarDetalleDto (PKG_COT.F_COTIZAR_DETALLE)
     */
    function renderDatosBase(container, timeline, detalle) {
        if (!container) return;
        const p = timeline?.parametros;
        if (!p) { container.innerHTML = '<p class="text-muted small">Sin datos para mostrar.</p>'; return; }

        const ruta = buscarDetalle(detalle, 'RUTA_TITULO');
        const mp1 = buscarDetalle(detalle, 'MP1');
        const mp2 = buscarDetalle(detalle, 'MP2');

        let html = '<div class="cot-db-grid">';

        html += `<div class="cot-db-card">
            <h6><i class="bi bi-tag me-1"></i>Título / Ruta</h6>
            <div><strong>Título ingresado:</strong> ${escapeHtml(p.tituloCod) || '<span class="text-muted">(sin título)</span>'}</div>
            ${ruta ? `<div class="small text-muted mt-1">${bulletsDetalle(ruta.detalle)}</div>` : ''}
        </div>`;

        html += `<div class="cot-db-card">
            <h6><i class="bi bi-flower1 me-1"></i>Materia Prima</h6>
            <div><strong>MP1</strong> (${escapeHtml(p.codArtMp1) || '-'}) — ${p.pctMp1}%</div>
            ${mp1 ? `<div class="small text-muted mb-2">${bulletsDetalle(mp1.detalle)}</div>` : ''}
            ${p.codArtMp2 ? `
                <div><strong>MP2</strong> (${escapeHtml(p.codArtMp2)}) — ${100 - p.pctMp1}%</div>
                ${mp2 ? `<div class="small text-muted">${bulletsDetalle(mp2.detalle)}</div>` : ''}
            ` : '<div class="small text-muted">Sin MP2 (100% de una sola fibra).</div>'}
        </div>`;

        html += `<div class="cot-db-card">
            <h6><i class="bi bi-clipboard-data me-1"></i>Parámetros del pedido</h6>
            <table class="table table-sm mb-0">
                <tbody>
                    <tr><td class="text-muted">Proceso</td><td class="text-end">${ETIQUETAS_PROCESO[p.proceso] || p.proceso}</td></tr>
                    <tr><td class="text-muted">Tonalidad</td><td class="text-end">${ETIQUETAS_INTENSIDAD[p.intensidadCod] || p.intensidadCod}</td></tr>
                    <tr><td class="text-muted">Presentación</td><td class="text-end">${ETIQUETAS_PRESENTACION[p.presentacion] || p.presentacion}</td></tr>
                    <tr><td class="text-muted">Cantidad</td><td class="text-end">${p.cantidadKg} kg</td></tr>
                    <tr><td class="text-muted">N° cabos (Nplies)</td><td class="text-end">${p.nplies}</td></tr>
                    <tr><td class="text-muted">Margen</td><td class="text-end">${p.margenPct}%</td></tr>
                </tbody>
            </table>
        </div>`;

        html += `<p class="small text-muted mb-0"><i class="bi bi-info-circle me-1"></i>
            Conversión física usada por el motor: <strong>1 QQ de algodón = 46 kg</strong>
            (convención interna de la empresa; NO es el quintal internacional de 45.36 kg).
        </p>`;

        html += '</div>';
        container.innerHTML = html;
    }

    // Agrupación de componentes en secciones, en el mismo orden del proceso productivo —
    // equivalente a las hojas CRUDO/BLANCO/COLOR del Excel (Materia Prima → Proceso → Overhead).
    const GRUPOS_COSTEO = [
        { titulo: 'Materia Prima', tipos: ['MP1_BRUTO', 'MP2_BRUTO', 'MERMA_DELTA', 'MP_CON_MERMA'] },
        { titulo: 'Proceso productivo (hilatura, tintorería, acabado)', tipos: ['HILATURA', 'CABLE_PLYING', 'TINTURA_TT', 'DEVANADO', 'EMPAQUE', 'FIJADO'] },
        { titulo: 'Overhead', tipos: ['OVERHEAD'] },
    ];
    // Mapeo TIPO (F_COTIZAR) → COMPONENTE (F_COTIZAR_DETALLE), para mostrar la fuente inline.
    const DETALLE_POR_TIPO = {
        MP1_BRUTO: 'MP1', MP2_BRUTO: 'MP2', MERMA_DELTA: 'MERMA', MP_CON_MERMA: 'MERMA',
        HILATURA: 'HILATURA', TINTURA_TT: 'TINTORERIA_TT', DEVANADO: 'DEVANADO',
        EMPAQUE: 'EMPAQUE', FIJADO: 'FIJADO', OVERHEAD: 'OVERHEAD',
    };

    /**
     * "Hoja 3 — Costeo": tabla clara (no línea de tiempo decorativa) agrupada en secciones
     * Materia Prima / Proceso productivo / Overhead + Total + precios sugeridos. Cada fila
     * tiene un botón (i) que despliega su fuente/fórmula (F_COTIZAR_DETALLE), reemplazando
     * el antiguo panel separado "Ver detalle del cálculo".
     * @param {HTMLElement} container
     * @param {object} timeline  CotizacionTimelineDto
     * @param {Array} detalle    Array de CotizarDetalleDto
     */
    function renderCosteo(container, timeline, detalle) {
        if (!container) return;
        if (!timeline || !timeline.pasos) {
            container.innerHTML = '<p class="text-muted small">Sin datos para mostrar.</p>';
            return;
        }

        const porTipo = {};
        timeline.pasos.forEach(p => { porTipo[p.tipo] = p; });

        let rowIdx = 0;
        let html = '<div class="cot-costeo">';

        GRUPOS_COSTEO.forEach(grupo => {
            const filas = grupo.tipos.map(t => porTipo[t]).filter(Boolean);
            if (!filas.length) return;
            html += `<div class="cot-costeo-grupo-titulo">${grupo.titulo}</div>`;
            html += '<table class="table table-sm cot-costeo-tabla mb-3">';
            filas.forEach(p => {
                const componenteDetalle = DETALLE_POR_TIPO[p.tipo];
                const det = componenteDetalle ? buscarDetalle(detalle, componenteDetalle) : null;
                const rid = `cotCosteoDet${rowIdx++}`;
                html += `<tr>
                    <td style="width:2rem"><i class="bi ${p.icono}" style="color:${p.color}"></i></td>
                    <td>${p.etiquetaCorta}
                        ${det ? `<button type="button" class="btn btn-link btn-sm p-0 ms-1 align-baseline" data-bs-toggle="collapse" data-bs-target="#${rid}" title="Ver de dónde sale este valor"><i class="bi bi-info-circle"></i></button>` : ''}
                        ${p.notas ? `<div class="small text-muted">${escapeHtml(p.notas)}</div>` : ''}
                        ${det ? `<div class="collapse small text-muted mt-1" id="${rid}">${bulletsDetalle(det.detalle)}</div>` : ''}
                    </td>
                    <td class="text-end fw-semibold" style="width:9rem">${fmtMoney(p.costoUsdKg)}</td>
                </tr>`;
            });
            html += '</table>';
        });

        const mermaEscala = porTipo['---MERMA_ESCALA'];
        const escalaKg = porTipo['---ESCALA_KG'];

        html += `<div class="cot-costeo-total d-flex justify-content-between align-items-center">
            <span><i class="bi bi-flag-fill me-1"></i>Costo Total</span>
            <span class="fw-bold fs-5">${fmtMoney(timeline.costoTotal)} USD/kg</span>
        </div>`;

        html += '<div class="row g-2 mt-2">';
        if (mermaEscala) html += `<div class="col-6 col-md-6"><div class="border rounded p-2 text-center h-100"><div class="small text-muted">${mermaEscala.etiquetaCorta}</div><div class="fw-bold">${fmtMoney(mermaEscala.costoUsdKg)}</div></div></div>`;
        if (escalaKg) html += `<div class="col-6 col-md-6"><div class="border rounded p-2 text-center h-100"><div class="small text-muted">${escalaKg.etiquetaCorta}</div><div class="fw-bold">${fmtMoney(escalaKg.costoUsdKg)}</div></div></div>`;
        html += '</div>';

        html += `<div class="row g-2 mt-2">
            <div class="col-6 col-md-3"><div class="border rounded p-2 text-center"><div class="small text-muted">Precio 25%</div><div class="fw-bold">${fmtMoney(timeline.precio25)}</div></div></div>
            <div class="col-6 col-md-3"><div class="border rounded p-2 text-center bg-primary-subtle"><div class="small text-muted">Precio 30%</div><div class="fw-bold">${fmtMoney(timeline.precio30)}</div></div></div>
            <div class="col-6 col-md-3"><div class="border rounded p-2 text-center"><div class="small text-muted">Precio 35%</div><div class="fw-bold">${fmtMoney(timeline.precio35)}</div></div></div>
            <div class="col-6 col-md-3"><div class="border rounded p-2 text-center"><div class="small text-muted">Precio 40%</div><div class="fw-bold">${fmtMoney(timeline.precio40)}</div></div></div>
        </div>`;

        html += '</div>';
        container.innerHTML = html;
    }

    /**
     * Renderiza el panel "Ver detalle del cálculo" (PKG_COT.F_COTIZAR_DETALLE) como un
     * accordion: un tab por componente, con la tabla/COT_KB de origen, las claves buscadas
     * y los valores crudos leídos (para quien recién está aprendiendo el motor de costeo).
     * @param {HTMLElement} container
     * @param {Array} detalle  Array de CotizarDetalleDto
     */
    function renderDetalle(container, detalle) {
        if (!container) return;
        if (!detalle || !detalle.length) {
            container.innerHTML = '<p class="text-muted small">Sin datos de detalle para mostrar.</p>';
            return;
        }

        const accId = 'accDetalleCalculo';
        let html = `<div class="accordion accordion-flush" id="${accId}">`;
        detalle.forEach((d, idx) => {
            const itemId = `${accId}Item${idx}`;
            html += `
                <div class="accordion-item">
                    <h2 class="accordion-header">
                        <button class="accordion-button collapsed" type="button" data-bs-toggle="collapse" data-bs-target="#${itemId}">
                            <span class="fw-semibold me-2">${d.componente}</span>
                            <span class="text-muted small">${d.fuente ?? ''}</span>
                        </button>
                    </h2>
                    <div id="${itemId}" class="accordion-collapse collapse" data-bs-parent="#${accId}">
                        <div class="accordion-body small">
                            ${(d.detalle ?? '').split(' | ').map(t => `<div>&bull; ${t}</div>`).join('')}
                        </div>
                    </div>
                </div>`;
        });
        html += '</div>';
        container.innerHTML = html;
    }

    /**
     * Renderiza el comparativo por tonalidad (CotizacionComparativoDto) como una tabla tipo
     * hoja "Resumen" de Excel: filas = componentes de costo (en orden de proceso), columnas =
     * tonalidades (CRUDO/BLANCO/CLARO/MEDIO/OSCURO/INTENSO). La columna de la tonalidad
     * actualmente seleccionada en el formulario se resalta para ubicarla de un vistazo.
     * @param {HTMLElement} container
     * @param {object} comparativo  CotizacionComparativoDto
     */
    function renderComparativo(container, comparativo) {
        if (!container) return;
        if (!comparativo || !comparativo.filas || !comparativo.filas.length) {
            container.innerHTML = '<p class="text-muted small">Sin datos para comparar.</p>';
            return;
        }

        const cols = comparativo.columnas || [];
        const rowClass = (grupo) => grupo === 'resumen' ? 'table-light fw-semibold'
            : grupo === 'precio' ? 'table-primary-subtle'
            : '';

        let html = '<div class="table-responsive"><table class="sire-table w-100 cot-comparativo-tabla">';
        html += '<thead><tr><th>Componente</th>';
        cols.forEach(c => {
            html += `<th class="text-end${c.esActual ? ' cot-col-actual' : ''}">${c.etiqueta}${c.esActual ? ' <i class="bi bi-arrow-down-circle-fill small"></i>' : ''}</th>`;
        });
        html += '</tr></thead><tbody>';

        comparativo.filas.forEach(f => {
            const esTotal = f.tipo === '---TOTAL_COSTO';
            html += `<tr class="${rowClass(f.grupo)}${esTotal ? ' table-success-subtle fw-bold' : ''}">`;
            html += `<td><i class="bi ${f.icono} me-1" style="color:${f.color}"></i>${f.etiqueta}</td>`;
            f.valores.forEach((v, i) => {
                const actual = cols[i] && cols[i].esActual;
                html += `<td class="text-end${actual ? ' cot-col-actual' : ''}">${fmtMoney(v)}</td>`;
            });
            html += '</tr>';
        });

        html += '</tbody></table></div>';
        html += `<p class="small text-muted mb-0"><i class="bi bi-arrow-down-circle-fill me-1"></i>Columna resaltada = tonalidad actualmente seleccionada en el formulario.</p>`;
        container.innerHTML = html;
    }

    // ── Auxiliares y Servicios (COT_KB + PARAMCOS) — hojas "Auxiliares"/"Gas Natural" del Excel ──
    const ETIQUETAS_CATEGORIA_KB = {
        MP_PRECIO: 'Precio de Materia Prima (override manual)',
        MERMA_FACTOR: 'Factor de merma por ruta técnica',
        MERMA_ESCALA: '% de merma adicional por lote pequeño (tramo de kilos)',
        HILATURA_TOTAL: 'Hilatura (Spinning) — costo total',
        HILATURA_SUMIN: 'Hilatura — suministros',
        TT_COSTO: 'Tintorería — costo por tonalidad',
        DEVANADO: 'Devanado',
        EMPAQUE: 'Empaque',
        FIJADO: 'Fijado / Torsión',
        CABLE_INCREMENT: 'Cable / Retorcido — incremento',
        OVERHEAD_GOF: 'Overhead — GOF (Gastos Operativos/Financieros)',
        OVERHEAD_GRUPO: 'Overhead por grupo de título (Grueso/Fino)',
        ESCALA_KG: 'Factor de escala por tramo de kilos (precio)',
        MP_FACTOR_COMPRA: 'Factor de compra de Materia Prima',
        TITULO_ROUTE: 'Título → Ruta técnica',
        TITULO_MAP_TONALIDAD: 'Título → Tonalidad',
        FIBRA_MAP: 'Fibra → Código de artículo',
        PARAM: 'Parámetro general',
    };

    function claveCompuesta(a) {
        return [a.clave1, a.clave2, a.clave3, a.clave4].filter(v => v !== null && v !== undefined && v !== '').join(' / ') || '—';
    }

    /**
     * Renderiza el catálogo de referencia (COT_KB + PARAMCOS): pestaña "Auxiliares y Servicios".
     * @param {HTMLElement} container
     * @param {object} data  CotizacionAuxiliaresDto { parametros, auxiliares }
     */
    function renderAuxiliares(container, data) {
        if (!container) return;
        if (!data || (!data.parametros?.length && !data.auxiliares?.length)) {
            container.innerHTML = '<p class="text-muted small">Sin datos de referencia disponibles.</p>';
            return;
        }

        let html = '';

        // Parámetros generales (PARAMCOS, con corrección de columnas desfasadas)
        if (data.parametros?.length) {
            html += '<h6 class="mt-1"><i class="bi bi-sliders me-1"></i>Parámetros generales (PARAMCOS)</h6>';
            html += '<div class="table-responsive mb-3"><table class="sire-table w-100">';
            html += '<thead><tr><th>Parámetro</th><th class="text-end">Valor</th><th>Unidad</th><th>Origen</th></tr></thead><tbody>';
            data.parametros.forEach(p => {
                html += `<tr>
                    <td>${escapeHtml(p.nombre)}</td>
                    <td class="text-end fw-semibold">${p.valor != null ? fmtMoney(p.valor) : '—'}</td>
                    <td>${escapeHtml(p.unidad ?? '')}</td>
                    <td class="small text-muted">${escapeHtml(p.nota ?? '')}</td>
                </tr>`;
            });
            html += '</tbody></table></div>';
        }

        // COT_KB agrupado por categoría
        if (data.auxiliares?.length) {
            const porCategoria = {};
            data.auxiliares.forEach(a => {
                (porCategoria[a.categoria] ??= []).push(a);
            });

            html += '<h6><i class="bi bi-collection-fill me-1"></i>Catálogo de insumos, tarifas y factores (COT_KB)</h6>';
            html += '<p class="small text-muted">Estos son los mismos valores que usa el motor de cálculo. Si un monto cambió (por ejemplo un tarifario o un precio de insumo), se actualiza aquí y afecta automáticamente el costeo de todas las cotizaciones nuevas.</p>';

            Object.keys(porCategoria).sort().forEach(cat => {
                const filas = porCategoria[cat];
                const etiqueta = ETIQUETAS_CATEGORIA_KB[cat] || cat;
                html += `<div class="cot-aux-categoria mb-3">`;
                html += `<div class="cot-aux-categoria-titulo"><span class="badge text-bg-secondary">${escapeHtml(cat)}</span> ${escapeHtml(etiqueta)}</div>`;
                html += '<div class="table-responsive"><table class="sire-table w-100">';
                html += '<thead><tr><th>Clave</th><th class="text-end">Valor</th><th>Texto</th><th>Unidad</th><th>Confianza</th><th>Observación</th><th>Actualizado</th></tr></thead><tbody>';
                filas.forEach(a => {
                    html += `<tr>
                        <td class="small">${escapeHtml(claveCompuesta(a))}</td>
                        <td class="text-end fw-semibold">${a.valorNum != null ? fmtMoney(a.valorNum) : '—'}</td>
                        <td class="small">${escapeHtml(a.valorText ?? '')}</td>
                        <td class="small">${escapeHtml(a.unidad ?? '')}</td>
                        <td class="small">${escapeHtml(a.confianza ?? '')}</td>
                        <td class="small text-muted">${escapeHtml(a.observacion ?? '')}</td>
                        <td class="small text-muted">${a.fchActualiz ? new Date(a.fchActualiz).toLocaleDateString('es-PE') : ''}</td>
                    </tr>`;
                });
                html += '</tbody></table></div></div>';
            });
        }

        container.innerHTML = html;
    }

    global.CotizacionTimeline = { render: renderTimeline, renderDatosBase, renderCosteo, renderDetalle, renderComparativo, renderAuxiliares };
})(window);
