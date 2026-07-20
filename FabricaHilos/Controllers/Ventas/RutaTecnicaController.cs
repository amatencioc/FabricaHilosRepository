using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FabricaHilos.Models.Ventas.Cotizacion;
using FabricaHilos.Services.Ventas.Cotizacion;

namespace FabricaHilos.Controllers.Ventas;

/// <summary>
/// Mantenimiento de la ficha técnica de ruta (COT_RUTA_TECNICA_CAB/DET) — pantalla propia para que
/// Preparatoria mantenga los datos que antes se llenaban a mano en un Excel ("1_DATOS_BASE_...xlsx")
/// para armar la cotización. El motor de costeo (CotizacionController/CotizacionService) la consume
/// en modo lectura desde Simular.cshtml (dato "vigente") y la congela en COT_HISTORIAL al guardar.
/// Ver 04_COT_RUTA_TECNICA.sql (SIG) para el DDL y las reglas de negocio.
/// </summary>
[Authorize]
[Route("Ventas/RutaTecnica")]
public class RutaTecnicaController : OracleBaseController
{
    private readonly IRutaTecnicaService _service;
    private readonly ILogger<RutaTecnicaController> _logger;

    public RutaTecnicaController(IRutaTecnicaService service, ILogger<RutaTecnicaController> logger)
    {
        _service = service;
        _logger = logger;
    }

    private string UsuarioActual =>
        HttpContext.Session.GetString("OracleUser") ?? User.Identity?.Name ?? "SISTEMA";

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(string? buscar = null)
    {
        var lista = await _service.ListarCabecerasAsync(buscar);
        ViewBag.Buscar = buscar;
        return View(lista);
    }

    [HttpGet("Editar/{idCab:long?}")]
    public async Task<IActionResult> Editar(long? idCab)
    {
        RutaTecnicaCabDto dto;
        if (idCab is > 0)
        {
            var existente = await _service.ObtenerPorIdAsync(idCab.Value);
            if (existente is null) return NotFound();
            dto = existente;
        }
        else
        {
            dto = new RutaTecnicaCabDto();
        }
        return View(dto);
    }

    [HttpPost("Guardar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar([FromBody] RutaTecnicaCabDto dto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.TituloCod))
                return Json(new { ok = false, error = "Debe seleccionar un título válido (buscado por código/descripción)." });
            if (string.IsNullOrWhiteSpace(dto.TituloRoute))
                return Json(new { ok = false, error = "El título/ruta es obligatorio." });

            // Reordena/normaliza el ORDEN según la posición enviada por la grilla (evita huecos si el
            // usuario reordenó o eliminó filas en el front antes de guardar).
            for (int i = 0; i < dto.Detalle.Count; i++)
                dto.Detalle[i].Orden = i + 1;

            var idCab = await _service.GuardarAsync(dto, UsuarioActual);
            return Json(new { ok = true, idCab });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar la ficha técnica de ruta");
            return Json(new { ok = false, error = ex.Message });
        }
    }

    [HttpPost("Eliminar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(long idCab)
    {
        try
        {
            await _service.EliminarAsync(idCab, UsuarioActual);
            return Json(new { ok = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al anular la ficha técnica de ruta {IdCab}", idCab);
            return Json(new { ok = false, error = ex.Message });
        }
    }

    [HttpPost("Restaurar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restaurar(long idCab)
    {
        try
        {
            await _service.RestaurarAsync(idCab, UsuarioActual);
            return Json(new { ok = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al restaurar la ficha técnica de ruta {IdCab}", idCab);
            return Json(new { ok = false, error = ex.Message });
        }
    }
}
