
const BASE = '/RecursosHumanos/Aquarius/PlanillaMensual';
let allData = [];

// ── Cargar empresas ──────────────────────────────────────────────────────────
async function cargarEmpresas() {
    const sel = document.getElementById('selEmpresa');
    try {
        const r = await fetch(`${BASE}/api/Empresas`);
        if (!r.ok) throw new Error(`HTTP ${r.status}`);
        const data = await r.json();
        sel.innerHTML = '<option value="0">— Seleccione empresa —</option>';
        data.forEach(e => sel.insertAdjacentHTML('beforeend',
            `<option value="${e.codEmpresa}">${e.desEmpresa}</option>`));
        const codDefault = 'X';
        if (codDefault && data.some(e => e.codEmpresa === codDefault)) {
            sel.value = codDefault;
            await onEmpresaChange();
        } else if (data.length === 1) {
            sel.value = data[0].codEmpresa;
            await onEmpresaChange();
        }
    } catch (ex) {
        console.error('cargarEmpresas', ex);
        sel.innerHTML = '<option value="0">— Error al cargar empresas —</option>';
    }
}

async function onEmpresaChange() {
    const cod = document.getElementById('selEmpresa').value;
    if (!cod || cod === '0') return;
    // Sucursales
    const rS   = await fetch(`${BASE}/api/Sucursales?codEmpresa=${encodeURIComponent(cod)}`);
    const suc  = await rS.json();
    const selS = document.getElementById('selSucursal');
    selS.innerHTML = '<option value="0">TODOS</option>';
    suc.forEach(s => selS.insertAdjacentHTML('beforeend',
        `<option value="${s.codSucursal}">${s.desSucursal}</option>`));
    // Planillas
    const rP   = await fetch(`${BASE}/api/TiposPlanilla?codEmpresa=${encodeURIComponent(cod)}`);
    const plan = await rP.json();
    const selP = document.getElementById('selPlanilla');
    selP.innerHTML = '<option value="0">— Todos —</option>';
    plan.forEach(t => {
        const sel = t.desTipoPlanilla?.trim().toUpperCase() === 'EMPLEADO' ? ' selected' : '';
        selP.insertAdjacentHTML('beforeend',
            `<option value="${t.codTipoPlanilla}"${sel}>${t.desTipoPlanilla}</option>`);
    });
    // C. Costos
    const rC   = await fetch(`${BASE}/api/CCostos?codEmpresa=${encodeURIComponent(cod)}`);
    const cc   = await rC.json();
    const selC = document.getElementById('selCCostos');
    selC.innerHTML = '<option value="TODOS">TODOS</option>';
    cc.forEach(c => selC.insertAdjacentHTML('beforeend',
        `<option value="${c.codCCostos}">${c.desCCostos}</option>`));
}

function setPeriodoDefault() {
    const hoy = new Date();
    const ini = new Date(hoy.getFullYear(), hoy.getMonth() - 1, 24);
    const fin = new Date(hoy.getFullYear(), hoy.getMonth(),     23);
    document.getElementById('dtDesde').value = ini.toISOString().slice(0,10);
    document.getElementById('dtHasta').value = fin.toISOString().slice(0,10);
}

// ── Consultar ────────────────────────────────────────────────────────────────
async function consultar() {
    const emp   = document.getElementById('selEmpresa').value;
    const suc   = document.getElementById('selSucursal').value;
    const plan  = document.getElementById('selPlanilla').value;
    const cc    = document.getElementById('selCCostos').value;
    const desde = document.getElementById('dtDesde').value;
    const hasta = document.getElementById('dtHasta').value;

    if (!emp || emp === '0') { alert('Seleccione una Empresa.'); return; }
    if (!desde || !hasta)    { alert('Seleccione el período.'); return; }
    if (!plan || plan === '0') { alert('Seleccione un Tipo de Planilla.'); return; }

    document.getElementById('spinner').style.display = '';
    document.getElementById('btnBuscar').disabled    = true;
    document.getElementById('tblDetalle').querySelector('tbody').innerHTML =
        '<tr><td colspan="21" class="text-center text-muted py-3"><span class="spinner-border spinner-border-sm me-2"></span>Consultando…</td></tr>';

    try {
        const resp = await fetch(`${BASE}/api/Detalle`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                codEmpresa:      emp,
                codSucursal:     suc,
                codTipoPlanilla: plan,
                cCostos:         cc,
                fechaInicio:     formatFecha(desde),
                fechaFinal:      formatFecha(hasta)
            })
        });
        if (!resp.ok) { const e = await resp.json(); throw new Error(e.error || resp.statusText); }
        allData = await resp.json();
        renderDetalle(allData);
        const empCount = [...new Set(allData.filter(r => r.tipoFila === 'T').map(r => r.nomTrabajador))].length;
        document.getElementById('divInfo').classList.remove('d-none');
        document.getElementById('lblInfo').textContent =
            `${empCount} empleados — Período: ${desde} al ${hasta}`;
        document.getElementById('btnExportar').disabled = allData.length === 0;
    } catch(ex) {
        document.getElementById('tblDetalle').querySelector('tbody').innerHTML =
            `<tr><td colspan="21" class="text-center text-danger py-3"><i class="bi bi-exclamation-triangle me-2"></i>${ex.message}</td></tr>`;
    } finally {
        document.getElementById('spinner').style.display = 'none';
        document.getElementById('btnBuscar').disabled    = false;
    }
}

// ── Render tabla detalle ──────────────────────────────────────────────────────
function renderDetalle(rows) {
    const tbody = document.getElementById('tbodyDet');
    if (!rows.length) {
        tbody.innerHTML = '<tr><td colspan="21" class="text-center text-muted py-4">Sin resultados.</td></tr>';
        return;
    }

    let html       = '';
    let lastEmp    = null;
    let lastSem    = null;

    for (const r of rows) {
        const isTotal   = r.tipoFila === 'T';
        const esDescanso = !r.horarioTeorico && !isTotal;
        const esFeriado  = r.feriado === 'F';
        const sinHorario = r.diLab === 'SIN Horario';

        // ── Separador de empleado (antes de la primera fila del empleado) ──
        if (r.nomTrabajador !== lastEmp) {
            lastEmp = r.nomTrabajador;
            lastSem = null;
            html += `<tr class="tr-emp">
                <td>${r.codPersonal ?? ''}</td>
                <td>${r.numDocIdentidad ?? ''}</td>
                <td colspan="19">${r.nomTrabajador ?? ''}</td>
            </tr>`;
        }

        if (isTotal) {
            // ── Fila TOTAL (corregida) ────────────────────────────────────
            const h25  = minToHHMM(r.horaExofi1MRnd);
            const h35  = minToHHMM(r.horaExofi2MRnd);
            const hdob = minToHHMM(r.horaDoblesRnd);
            const hnoc = minToHHMM(r.totHoraNocturnaM);
            const hef  = minToHHMM(r.horaEfectivaM);
            const hef1 = minToHHMM(r.horaEfectivaT1M);
            const tard = minToHHMM(r.horaTardanzaM);
            const perm = minToHHMM(r.horaPermisoM);
            html += `<tr class="tr-total">
                <td colspan="4" class="text-end pe-2 text-muted" style="font-size:.7rem">TOTAL</td>
                <td></td>
                <td class="col-sep text-center" colspan="4"></td>
                <td class="col-sep text-center th-grp-ef fw-bold">${hef}</td>
                <td class="text-center th-grp-ef">${hef1}</td>
                <td class="text-center th-grp-ef fw-bold" title="Días T2">${r.diasT2} días</td>
                <td class="text-center th-grp-ef fw-bold" title="Días T3">${r.diasT3} días</td>
                <td class="text-center th-grp-ef">${hnoc}</td>
                <td class="col-sep text-center ${r.horaTardanzaM > 0 ? 'td-tard' : ''}">${tard}</td>
                <td class="text-center"></td>
                <td class="text-center">${perm}</td>
                <td class="col-sep text-center th-grp-he fw-bold">${h25}</td>
                <td class="text-center th-grp-he fw-bold">${h35}</td>
                <td class="text-center th-grp-he fw-bold">${hdob}</td>
                <td class="col-sep"></td>
            </tr>`;
            continue;
        }

        // ── Separador de semana ───────────────────────────────────────────
        if (r.semProceso && r.semProceso !== lastSem) {
            lastSem = r.semProceso;
            html += `<tr style="background:#f0f4f8;border-top:1px solid var(--aq-mid)">
                <td colspan="21" style="font-size:.7rem;color:var(--aq-dk);padding:.25rem .5rem">
                    <i class="bi bi-calendar3-week me-1"></i>Semana ${r.semProceso}
                </td>
            </tr>`;
        }

        // ── Fila detalle diaria ───────────────────────────────────────────
        const trClass = sinHorario ? 'tr-descanso' : (esFeriado ? 'tr-feriado' : '');
        const fecLabel = formatFechaLabel(r.fecProceso, r.dia);
        const tardClass = r.horaTardanzaM > 0 ? 'td-tard' : '';

        html += `<tr class="${trClass}">
            <td></td>
            <td></td>
            <td></td>
            <td class="text-center text-muted" style="font-size:.68rem">${r.semProceso ?? ''}</td>
            <td class="text-center">${fecLabel}</td>
            <td class="col-sep text-center text-muted" style="font-size:.71rem">${r.horarioTeorico ?? '—'}</td>
            <td class="text-center" style="font-size:.71rem">${r.horarioJornada ?? (sinHorario ? '<em>Descanso</em>' : '—')}</td>
            <td class="text-center text-muted" style="font-size:.71rem">${r.horarioRefrigerio ?? '—'}</td>
            <td class="text-center">${r.horaRef ?? ''}</td>
            <td class="col-sep text-center fw-semibold">${r.horaEfectiva ?? (sinHorario ? '' : '—')}</td>
            <td class="text-center text-muted">${r.horaEfectivaT1 ?? ''}</td>
            <td class="text-center" style="color:var(--aq-info)">${r.horaEfectivaT2 ?? ''}</td>
            <td class="text-center" style="color:#6259a8">${r.horaEfectivaT3 ?? ''}</td>
            <td class="text-center text-muted">${r.totHoraNocturna ?? ''}</td>
            <td class="col-sep text-center ${tardClass}">${r.horaTardanza ?? ''}</td>
            <td class="text-center">${r.horaAnteSalida ?? ''}</td>
            <td class="text-center">${r.horaPermiso ?? ''}</td>
            <td class="col-sep text-center th-grp-he">${r.horaExofi1 ?? ''}</td>
            <td class="text-center th-grp-he">${r.horaExofi2 ?? ''}</td>
            <td class="text-center th-grp-he">${r.horaDobles ?? ''}</td>
            <td class="col-sep text-muted" style="font-size:.71rem">${r.diLab ?? ''}</td>
        </tr>`;
    }
    tbody.innerHTML = html;
}

// ── Helpers ────────────────────────────────────────────────────────────────────
function minToHHMM(min) {
    if (!min || min === 0) return '<span class="badge-0">0:00</span>';
    const h = Math.floor(min / 60), m = min % 60;
    return `${h}:${String(m).padStart(2,'0')}`;
}

function formatFecha(iso) {
    const [y, m, d] = iso.split('-');
    return `${d}/${m}/${y}`;
}

function formatFechaLabel(fec, dia) {
    if (!fec) return '';
    const dias = { 'LU':'Lu','MA':'Ma','MI':'Mi','JU':'Ju','VI':'Vi','SA':'<span style="color:var(--aq-info)">Sa</span>','DO':'<span style="color:var(--aq-err,#AE3F3C)">Do</span>' };
    const label = dias[dia?.trim()] ?? dia ?? '';
    return `<span class="text-muted me-1">${label}</span>${fec}`;
}

// ── Helpers para exportación Excel ───────────────────────────────────────
function minToHHMMPlano(min) {
    if (!min || min === 0) return '0:00';
    const h = Math.floor(min / 60), m = min % 60;
    return `${h}:${String(m).padStart(2,'0')}`;
}

function formatFechaLabelPlano(fec, dia) {
    if (!fec) return '';
    const dias = { LU:'Lu', MA:'Ma', MI:'Mi', JU:'Ju', VI:'Vi', SA:'Sa', DO:'Do' };
    const label = dias[dia?.trim()] ?? dia ?? '';
    return `${label} ${fec}`;
}

function construirTablaExcel(rows) {
    const headers = ['Cod.','DNI','Apellidos y Nombres','Sem','Fecha',
        'H.Teor.','H.Real','H.Refrig.','H.Ref',
        'H.Efe','H.T1','H.T2','H.T3','H.Noc',
        'Tard','H.Ant','Perm',
        'H25%','H35%','Dob','Observ.'];
    const coloresEmp = ['#ffffff', '#eaf1f8'];

    let lastEmp = null;
    let lastSem = null;
    let colorIdx = -1;
    let body = '';

    for (const r of rows) {
        const isTotal    = r.tipoFila === 'T';
        const sinHorario = r.diLab === 'SIN Horario';

        if (r.nomTrabajador !== lastEmp) {
            lastEmp = r.nomTrabajador;
            lastSem = null;
            colorIdx = colorIdx === 0 ? 1 : 0;
            body += '<tr style="background:#d7e3ef;font-weight:bold">' +
                '<td>' + (r.codPersonal ?? '') + '</td>' +
                '<td>' + (r.numDocIdentidad ?? '') + '</td>' +
                '<td colspan="19">' + (r.nomTrabajador ?? '') + '</td>' +
                '</tr>';
        }

        const bg = coloresEmp[colorIdx];

        if (isTotal) {
            const h25  = minToHHMMPlano(r.horaExofi1MRnd);
            const h35  = minToHHMMPlano(r.horaExofi2MRnd);
            const hdob = minToHHMMPlano(r.horaDoblesRnd);
            const hnoc = minToHHMMPlano(r.totHoraNocturnaM);
            const hef  = minToHHMMPlano(r.horaEfectivaM);
            const hef1 = minToHHMMPlano(r.horaEfectivaT1M);
            const tard = minToHHMMPlano(r.horaTardanzaM);
            const perm = minToHHMMPlano(r.horaPermisoM);
            body += '<tr style="background:' + bg + ';font-weight:bold;border-top:1px solid #999">' +
                '<td colspan="5" style="text-align:right;color:#777">TOTAL</td>' +
                '<td colspan="4"></td>' +
                '<td style="text-align:center">' + hef + '</td>' +
                '<td style="text-align:center">' + hef1 + '</td>' +
                '<td style="text-align:center">' + (r.diasT2 ?? 0) + ' dias</td>' +
                '<td style="text-align:center">' + (r.diasT3 ?? 0) + ' dias</td>' +
                '<td style="text-align:center">' + hnoc + '</td>' +
                '<td style="text-align:center">' + tard + '</td>' +
                '<td></td>' +
                '<td style="text-align:center">' + perm + '</td>' +
                '<td style="text-align:center">' + h25 + '</td>' +
                '<td style="text-align:center">' + h35 + '</td>' +
                '<td style="text-align:center">' + hdob + '</td>' +
                '<td></td>' +
                '</tr>';
            continue;
        }

        if (r.semProceso && r.semProceso !== lastSem) {
            lastSem = r.semProceso;
            body += '<tr style="background:#f0f4f8">' +
                '<td colspan="21" style="padding:2px 6px;font-style:italic">Semana ' + r.semProceso + '</td>' +
                '</tr>';
        }

        const fecLabel = formatFechaLabelPlano(r.fecProceso, r.dia);

        body += '<tr style="background:' + bg + '">' +
            '<td></td><td></td><td></td>' +
            '<td style="text-align:center">' + (r.semProceso ?? '') + '</td>' +
            '<td style="text-align:center">' + fecLabel + '</td>' +
            '<td style="text-align:center">' + (r.horarioTeorico ?? '-') + '</td>' +
            '<td style="text-align:center">' + (r.horarioJornada ?? (sinHorario ? 'Descanso' : '-')) + '</td>' +
            '<td style="text-align:center">' + (r.horarioRefrigerio ?? '-') + '</td>' +
            '<td style="text-align:center">' + (r.horaRef ?? '') + '</td>' +
            '<td style="text-align:center">' + (r.horaEfectiva ?? (sinHorario ? '' : '-')) + '</td>' +
            '<td style="text-align:center">' + (r.horaEfectivaT1 ?? '') + '</td>' +
            '<td style="text-align:center">' + (r.horaEfectivaT2 ?? '') + '</td>' +
            '<td style="text-align:center">' + (r.horaEfectivaT3 ?? '') + '</td>' +
            '<td style="text-align:center">' + (r.totHoraNocturna ?? '') + '</td>' +
            '<td style="text-align:center">' + (r.horaTardanza ?? '') + '</td>' +
            '<td style="text-align:center">' + (r.horaAnteSalida ?? '') + '</td>' +
            '<td style="text-align:center">' + (r.horaPermiso ?? '') + '</td>' +
            '<td style="text-align:center">' + (r.horaExofi1 ?? '') + '</td>' +
            '<td style="text-align:center">' + (r.horaExofi2 ?? '') + '</td>' +
            '<td style="text-align:center">' + (r.horaDobles ?? '') + '</td>' +
            '<td>' + (r.diLab ?? '') + '</td>' +
            '</tr>';
    }

    let thead = '<tr>';
    for (const h of headers) {
        thead += '<th style="background:#f4a462;color:#222;border:1px solid #ccc;padding:4px">' + h + '</th>';
    }
    thead += '</tr>';

    return '<table border="1" style="border-collapse:collapse;font-family:Calibri,Arial;font-size:11px">' +
        '<thead>' + thead + '</thead>' +
        '<tbody>' + body + '</tbody>' +
        '</table>';
}

// ── Exportar Excel (tabla HTML agrupada por empleado, con color alternado) ──
document.getElementById('btnExportar').addEventListener('click', () => {
    if (!allData.length) return;
    const tabla = construirTablaExcel(allData);
    const html = '<html xmlns:o="urn:schemas-microsoft-com:office:office" xmlns:x="urn:schemas-microsoft-com:office:excel" xmlns="http://www.w3.org/TR/REC-html40">' +
        '<head><meta charset="UTF-8">' +
        '<!--[if gte mso 9]><xml><x:ExcelWorkbook><x:ExcelWorksheets><x:ExcelWorksheet>' +
        '<x:Name>DetalleSemanal</x:Name><x:WorksheetOptions><x:DisplayGridlines/></x:WorksheetOptions>' +
        '</x:ExcelWorksheet></x:ExcelWorksheets></x:ExcelWorkbook></xml><![endif]-->' +
        '</head><body>' + tabla + '</body></html>';
    const blob = new Blob(['\uFEFF' + html], { type: 'application/vnd.ms-excel;charset=utf-8;' });
    const url  = URL.createObjectURL(blob);
    Object.assign(document.createElement('a'), { href: url, download: 'DetalleSemanal.xls' }).click();
    URL.revokeObjectURL(url);
});

// ── Init ─────────────────────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', async () => {
    setPeriodoDefault();
    await cargarEmpresas();
    document.getElementById('selEmpresa').addEventListener('change', onEmpresaChange);
    document.getElementById('btnBuscar').addEventListener('click', consultar);
});

