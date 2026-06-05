using FabricaHilos.Services.RecursosHumanos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FabricaHilos.Controllers.RecursosHumanos;

// ════════════════════════════════════════════════════════════════════════
//  ARCHIVO: CostoSalarialHorasExtrasController.cs
//  PROPÓSITO: Controlador MVC del módulo KPI Costo Salarial de Horas Extras.
//
//  RUTAS QUE EXPONE:
//   GET /RecursosHumanos/CostoSalarialHorasExtras        → Vista principal
//   GET /RecursosHumanos/CostoSalarialHorasExtras/Index  → Igual a la anterior
//   GET /RecursosHumanos/CostoSalarialHorasExtras/Kpi    → Partial con datos (AJAX)
//
//  FLUJO TÍPICO:
//   1. Usuario navega a /RecursosHumanos/CostoSalarialHorasExtras
//   2. Index() carga el shell (filtros + contenedor vacío)
//   3. JavaScript hace fetch a /Kpi?anoIni=...&mesIni=...
//   4. Kpi() llama al servicio y devuelve el partial _KpiDashboard.cshtml
//   5. JavaScript inyecta el HTML y dibuja los Chart.js
// ════════════════════════════════════════════════════════════════════════

/// <summary>
/// Controlador del dashboard "KPI Costo Salarial de Horas Extras".
/// Maneja la vista principal y el endpoint AJAX que devuelve el partial con los datos.
/// </summary>
/// <remarks>
/// Requiere autenticación (atributo <c>[Authorize]</c>).
/// La lógica de negocio y acceso a Oracle se delega al <see cref="ICostoSalarialHorasExtrasService"/>.
/// </remarks>
[Authorize]
[Route("RecursosHumanos/CostoSalarialHorasExtras")]
public class CostoSalarialHorasExtrasController : OracleBaseController
{
    private readonly ICostoSalarialHorasExtrasService _service;
    private readonly ILogger<CostoSalarialHorasExtrasController> _logger;

    /// <summary>
    /// Constructor — recibe las dependencias por inyección.
    /// </summary>
    /// <param name="service">Servicio que consulta Oracle y aplica el caché.</param>
    /// <param name="logger">Logger para registrar errores y métricas.</param>
    public CostoSalarialHorasExtrasController(
        ICostoSalarialHorasExtrasService service,
        ILogger<CostoSalarialHorasExtrasController> logger)
    {
        _service = service;
        _logger  = logger;
    }

    /// <summary>
    /// Carga la página principal del dashboard con los filtros y el contenedor vacío.
    /// Los datos se cargan por AJAX cuando el usuario presiona "Consultar".
    /// </summary>
    /// <remarks>
    /// Valores por defecto del filtro: <b>Mayo 2025 → Mayo 2026</b>
    /// (comparativo de 12 meses entre mismos meses de años distintos).
    /// </remarks>
    /// <returns>Vista Razor <c>Index.cshtml</c> con los <c>ViewBag</c> de filtros.</returns>
    [HttpGet("")]
    [HttpGet("Index")]
    public IActionResult Index()
    {
        // ── Período por defecto: Mayo 2025 → Mayo 2026 ─────────────────
        // Esto permite al usuario ver inmediatamente un comparativo año-vs-año
        // sin tener que configurar los filtros manualmente.
        ViewBag.AnoIni = 2025;
        ViewBag.MesIni = 5;  // Mayo
        ViewBag.AnoFin = 2026;
        ViewBag.MesFin = 5;  // Mayo
        return View("~/Views/RecursosHumanos/Indicadores/CostoSalarialHorasExtras/Index.cshtml");
    }

    /// <summary>
    /// Endpoint AJAX que consulta los datos del KPI y devuelve el partial renderizado.
    /// </summary>
    /// <param name="anoIni">Año de inicio del período (ej: 2025).</param>
    /// <param name="mesIni">Mes de inicio (1-12).</param>
    /// <param name="anoFin">Año fin del período (ej: 2026).</param>
    /// <param name="mesFin">Mes fin (1-12).</param>
    /// <param name="tipo">
    /// Filtro de tipo de empleado:
    ///  <c>"T"</c> = Todos (default), <c>"O"</c> = Obreros, <c>"E"</c> = Empleados.
    /// </param>
    /// <returns>
    /// HTML del partial <c>_KpiDashboard.cshtml</c> con las tablas, gráficos y JSON
    /// de datos para Chart.js. Si ocurre un error devuelve HTTP 500 con mensaje genérico.
    /// </returns>
    /// <remarks>
    /// Si <c>anoIni == anoFin</c> → se consultan los meses entre mesIni y mesFin.
    /// Si <c>anoIni != anoFin</c> → comparativo año vs año (ver lógica en el servicio).
    /// </remarks>
    [HttpGet("Kpi")]
    public async Task<IActionResult> Kpi(int anoIni, int mesIni, int anoFin, int mesFin, string tipo = "T")
    {
        try
        {
            // Llama al servicio (que primero consulta el caché y luego Oracle si es necesario).
            var vm = await _service.ObtenerKpiAsync(anoIni, mesIni, anoFin, mesFin, tipo);

            // Devuelve el partial — la vista renderiza tablas/gráficos a partir del ViewModel.
            return PartialView("~/Views/RecursosHumanos/Indicadores/CostoSalarialHorasExtras/_KpiDashboard.cshtml", vm);
        }
        catch (Exception ex)
        {
            // Cualquier error de Oracle, parsing o caché aterriza aquí.
            // Se logea con todos los parámetros para facilitar la investigación.
            _logger.LogError(ex, "Error al obtener KPI CostoSalarialHorasExtras ({AnoIni}/{MesIni} - {AnoFin}/{MesFin}) Tipo={Tipo}",
                anoIni, mesIni, anoFin, mesFin, tipo);

            // Mensaje genérico al cliente — no exponemos detalles técnicos.
            return StatusCode(500, "Error al obtener los datos. Intente nuevamente.");
        }
    }
}
