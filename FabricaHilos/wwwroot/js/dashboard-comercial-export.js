// Exportar a Excel los clientes de un asesor específico (desde la tabla renderizada en el DOM)
window.exportarClientesAsesorExcel = function () {
    var asesor = document.getElementById('lblAsesorDetalle').textContent || 'Asesor';
    var moneda = document.getElementById('moneda');
    var monLabel = moneda && moneda.value === 'S' ? 'S/' : 'USD';
    var body = document.getElementById('bodyClientesAsesor');
    var rows = body ? body.querySelectorAll('tr') : [];
    if (!rows.length) return;

    var html = '<html xmlns:o="urn:schemas-microsoft-com:office:office" xmlns:x="urn:schemas-microsoft-com:office:excel">';
    html += '<head><meta charset="UTF-8"></head><body>';
    html += '<table border="1">';
    html += '<tr><td colspan="5" style="background:#1B4D3E;color:#fff;font-weight:bold;font-size:13pt;text-align:center;padding:6px;">Clientes del Asesor: ' + asesor + '</td></tr>';
    html += '<tr style="background:#1B4D3E;color:#fff;font-weight:bold">';
    html += '<th>#</th><th>RUC</th><th>Raz\u00f3n Social</th><th>Giro</th><th>Importe (' + monLabel + ')</th>';
    html += '</tr>';

    rows.forEach(function (tr) {
        var cells = tr.querySelectorAll('td');
        if (!cells.length) return;
        html += '<tr>';
        cells.forEach(function (td, idx) {
            var val = td.textContent.trim();
            if (idx === 4) {
                if (val.indexOf('USD ') === 0) val = val.slice(4);
                else if (val.indexOf('S/ ') === 0) val = val.slice(3);
            }
            html += '<td>' + val + '</td>';
        });
        html += '</tr>';
    });

    html += '</table></body></html>';

    var now = new Date();
    var fecha = now.getFullYear() + String(now.getMonth() + 1).padStart(2, '0') + String(now.getDate()).padStart(2, '0');
    var blob = new Blob([html], { type: 'application/vnd.ms-excel;charset=utf-8' });
    var url = URL.createObjectURL(blob);
    var a = document.createElement('a');
    a.href = url;
    a.download = 'Total_Clientes_' + asesor.split(' ').join('_').split('/').join('_') + '_giro_importe_' + fecha + '.xls';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
};

// Exportar a Excel todos los clientes de todos los asesores (usa datos ya cargados en memoria)
window.exportarTodosClientesExcel = function () {
    var moneda = document.getElementById('moneda');
    var monLabel = moneda && moneda.value === 'S' ? 'S/' : 'USD';
    var rows = window._clientesTodosData || [];
    if (!rows.length) {
        if (window.showToast) window.showToast('No hay datos para exportar.', 'warning');
        return;
    }

    try {

        var html = '<html xmlns:o="urn:schemas-microsoft-com:office:office" xmlns:x="urn:schemas-microsoft-com:office:excel">';
        html += '<head><meta charset="UTF-8"></head><body>';
        html += '<table border="1">';
        html += '<tr style="background:#1B4D3E;color:#fff;font-weight:bold">';
        html += '<th>#</th><th>Asesor</th><th>RUC</th><th>Raz\u00f3n Social</th><th>Giro</th><th>Importe (' + monLabel + ')</th>';
        html += '</tr>';

        rows.forEach(function (row, i) {
            html += '<tr>';
            html += '<td>' + (i + 1) + '</td>';
            html += '<td>' + (row.asesor || '') + '</td>';
            html += '<td>' + (row.ruc || '') + '</td>';
            html += '<td>' + (row.razonSocial || '') + '</td>';
            html += '<td>' + (row.giro || '') + '</td>';
            html += '<td>' + (row.importe || 0) + '</td>';
            html += '</tr>';
        });

        html += '</table></body></html>';

        var now = new Date();
        var fecha = now.getFullYear() + String(now.getMonth() + 1).padStart(2, '0') + String(now.getDate()).padStart(2, '0');
        var blob = new Blob([html], { type: 'application/vnd.ms-excel;charset=utf-8' });
        var url = URL.createObjectURL(blob);
        var a = document.createElement('a');
        a.href = url;
        a.download = 'Total_Clientes_TODOS_giro_importe_' + fecha + '.xls';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    } catch (e) {
        console.error(e);
        if (window.showToast) window.showToast('Error al exportar. Intente nuevamente.', 'danger');
    }
};
