// ── Dashboard Comercial — Exportar Excel ───────────────────────────────────
// clientesTodosData: { asesor, ruc, razonSocial, giro, moneda, importe, igv, total, cantidadKg }

function _dcMonLabel() {
    var el = document.getElementById('moneda');
    return el && el.value === 'S' ? 'S/' : 'USD';
}

function _dcFecha() {
    var d = new Date();
    return d.getFullYear()
        + String(d.getMonth() + 1).padStart(2, '0')
        + String(d.getDate()).padStart(2, '0');
}

function _dcDescargar(html, nombre) {
    var blob = new Blob([html], { type: 'application/vnd.ms-excel;charset=utf-8' });
    var url  = URL.createObjectURL(blob);
    var a    = document.createElement('a');
    a.href     = url;
    a.download = nombre;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
}

function _dcHeaderRow(monLabel, conAsesor) {
    var html = '<tr style="background:#1B4D3E;color:#fff;font-weight:bold">';
    html += '<th>#</th>';
    if (conAsesor) html += '<th>Asesor</th>';
    html += '<th>RUC</th><th>Raz\u00f3n Social</th><th>Giro</th>';
    html += '<th>N\u00b0 Docs</th><th>Kilos</th>';
    html += '<th>Importe (' + monLabel + ')</th>';
    html += '</tr>';
    return html;
}

function _dcFila(row, idx, conAsesor) {
    var html = '<tr>';
    html += '<td>' + (idx + 1) + '</td>';
    if (conAsesor) html += '<td>' + (row.asesor || '') + '</td>';
    html += '<td>' + (row.ruc         || '') + '</td>';
    html += '<td>' + (row.razonSocial || '') + '</td>';
    html += '<td>' + (row.giro        || '') + '</td>';
    html += '<td>' + (row.nroDoc      || 0)  + '</td>';
    html += '<td>' + (row.cantidadKg  || 0)  + '</td>';
    html += '<td>' + (row.importe     || 0)  + '</td>';
    html += '</tr>';
    return html;
}

// Exportar clientes de un asesor especifico
window.exportarClientesAsesorExcel = function () {
    var asesor   = (document.getElementById('lblAsesorDetalle') || {}).textContent || 'Asesor';
    var monLabel = _dcMonLabel();

    var rows = (window._clientesTodosData || []).filter(function (r) {
        return r.asesor && r.asesor.toLowerCase() === asesor.toLowerCase();
    });
    if (!rows.length) { alert('Sin datos para exportar.'); return; }

    var html = '<html xmlns:o="urn:schemas-microsoft-com:office:office" xmlns:x="urn:schemas-microsoft-com:office:excel">';
    html += '<head><meta charset="UTF-8"></head><body><table border="1">';
    html += '<tr><td colspan="7" style="background:#1B4D3E;color:#fff;font-weight:bold;font-size:13pt;text-align:center;padding:6px;">Clientes del Asesor: ' + asesor + '</td></tr>';
    html += _dcHeaderRow(monLabel, false);
    rows.forEach(function (r, i) { html += _dcFila(r, i, false); });
    html += '</table></body></html>';

    _dcDescargar(html, 'Clientes_' + asesor.replace(/\s+/g, '_') + '_' + _dcFecha() + '.xls');
};

// Exportar todos los clientes de todos los asesores
window.exportarTodosClientesExcel = function () {
    var monLabel = _dcMonLabel();
    var rows     = window._clientesTodosData || [];
    if (!rows.length) { if (window.showToast) window.showToast('No hay datos para exportar.', 'warning'); return; }

    try {
        var html = '<html xmlns:o="urn:schemas-microsoft-com:office:office" xmlns:x="urn:schemas-microsoft-com:office:excel">';
        html += '<head><meta charset="UTF-8"></head><body><table border="1">';
        html += _dcHeaderRow(monLabel, true);
        rows.forEach(function (r, i) { html += _dcFila(r, i, true); });
        html += '</table></body></html>';

        _dcDescargar(html, 'Clientes_TODOS_' + _dcFecha() + '.xls');
    } catch (e) {
        console.error(e);
        if (window.showToast) window.showToast('Error al exportar.', 'danger');
    }
};
