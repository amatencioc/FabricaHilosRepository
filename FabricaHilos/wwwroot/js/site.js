// Fábrica de Hilos - JavaScript del sitio

// Toggle del sidebar en cualquier tamaño de pantalla
document.addEventListener('DOMContentLoaded', function () {
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
