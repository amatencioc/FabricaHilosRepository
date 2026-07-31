using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FabricaHilos.Models.Mantenimiento;
using FabricaHilos.Services.Mantenimiento;

namespace FabricaHilos.Controllers.Mantenimiento;

/// <summary>
/// Listado de programas de mantenimiento (MA_PROGRAMA legacy) donde el usuario logueado
/// es el responsable de firma (RESP_FIRMA), con acción de validación. NO crea/edita
/// programas — eso se sigue haciendo en las pantallas legacy existentes.
/// </summary>
[Authorize]
public class ProgramaMantenimientoController : OracleBaseController
{
    private readonly IProgramaMantenimientoService _service;

    public ProgramaMantenimientoController(IProgramaMantenimientoService service)
    {
        _service = service;
    }

    // El C_CODIGO del mecanico asignado se toma SIEMPRE de la sesión (nunca del cliente)
    private string CCodigoUsuario => HttpContext.Session.GetString("OracleUserCodigo") ?? string.Empty;

    // GET: /ProgramaMantenimiento
    public async Task<IActionResult> Index()
    {
        var cCodigo = CCodigoUsuario;
        if (string.IsNullOrWhiteSpace(cCodigo))
        {
            TempData["Warning"] = "Su usuario no tiene un código de mecánico (C_CODIGO) asociado en Oracle.";
            return View(new List<ProgramaMantenimientoListItemDto>());
        }

        var lista = await _service.ListarAsignadosAsync(cCodigo);

        // v1.14: banner "Tiene programas Ejecutados pendientes de validar" ocultado a pedido
        // del usuario (para todos) -- ya no se llama ListarPendientesValidarAsync aquí.

        var vistaJefe = await _service.ListarJefeAsync(cCodigo);
        ViewBag.MostrarVistaJefe = vistaJefe.Count > 0;

        return View(lista);
    }

    // GET: /ProgramaMantenimiento/Detalle/123
    public async Task<IActionResult> Detalle(long id)
    {
        var vm = await _service.ObtenerDetalleAsync(id, CCodigoUsuario);
        if (vm == null)
        {
            TempData["Warning"] = $"El programa {id} no existe.";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.CCodigoUsuario = CCodigoUsuario;
        return View(vm);
    }

    // GET: /ProgramaMantenimiento/PorValidar — pendientes del jefe de área logueado
    public async Task<IActionResult> PorValidar()
    {
        var cCodigo = CCodigoUsuario;
        if (string.IsNullOrWhiteSpace(cCodigo))
        {
            TempData["Warning"] = "Su usuario no tiene un código de personal (C_CODIGO) asociado en Oracle.";
            return View(new List<ProgramaPendienteValidarDto>());
        }

        var lista = await _service.ListarPendientesValidarAsync(cCodigo);
        return View(lista);
    }

    // GET: /ProgramaMantenimiento/VistaJefe — solo lectura, todo lo Ejecutado (validado y
    // pendiente) de los centros de costo donde el usuario logueado es el jefe (escalamiento)
    public async Task<IActionResult> VistaJefe()
    {
        var cCodigo = CCodigoUsuario;
        if (string.IsNullOrWhiteSpace(cCodigo))
        {
            TempData["Warning"] = "Su usuario no tiene un código de personal (C_CODIGO) asociado en Oracle.";
            return View(new List<ProgramaJefeVistaDto>());
        }

        var lista = await _service.ListarJefeAsync(cCodigo);
        return View(lista);
    }

    // POST: /ProgramaMantenimiento/Validar
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Validar(long id, string? returnUrl)
    {
        var cCodigo = CCodigoUsuario;
        if (string.IsNullOrWhiteSpace(cCodigo))
        {
            TempData["Warning"] = "Su usuario no tiene un código de mecánico (C_CODIGO) asociado en Oracle.";
            return RedirectToLocalOrIndex(returnUrl, id);
        }

        var (ok, mensaje) = await _service.ValidarAsync(id, cCodigo);
        TempData[ok ? "Success" : "Warning"] = ok
            ? $"Programa {id} validado correctamente."
            : (mensaje ?? "No se pudo validar el programa.");

        return RedirectToLocalOrIndex(returnUrl, id);
    }

    // Se queda en la vista de origen (Index o Detalle) en vez de forzar siempre el Detalle
    private IActionResult RedirectToLocalOrIndex(string? returnUrl, long id)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(Index));
    }
}
