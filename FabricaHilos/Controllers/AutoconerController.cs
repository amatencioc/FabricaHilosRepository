using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using FabricaHilos.Data;
using FabricaHilos.Helpers;
using FabricaHilos.Models.Produccion;
using FabricaHilos.Services.Produccion;

namespace FabricaHilos.Controllers
{
    [Authorize]
    public class AutoconerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IRecetaService _recetaService;
        private readonly IParoService   _paroService;
        private readonly ILogger<AutoconerController> _logger;

        public AutoconerController(
            ApplicationDbContext context,
            IRecetaService recetaService,
            IParoService   paroService,
            ILogger<AutoconerController> logger)
        {
            _context       = context;
            _recetaService = recetaService;
            _paroService   = paroService;
            _logger        = logger;
        }

        private static readonly HashSet<string> _apiActions = new(StringComparer.OrdinalIgnoreCase)
        {
            nameof(BuscarReceta),
            nameof(BuscarLote),
            nameof(BuscarOperario),
            nameof(ObtenerMaquinasAutoconer),
            nameof(ObtenerTitulos),
            nameof(ObtenerHusosMaquina),
        };

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            base.OnActionExecuting(context);
            if (context.ActionDescriptor.RouteValues.TryGetValue("action", out var action) &&
                _apiActions.Contains(action ?? string.Empty))
                return;

            if (string.IsNullOrEmpty(HttpContext.Session.GetString("OracleUser")))
            {
                _logger.LogWarning("Sesión Oracle expirada en Autoconer. Redirigiendo al login.");
                TempData["Warning"] = "Su sesión Oracle ha expirado. Por favor, inicie sesión nuevamente.";
                context.Result = RedirectToAction("Login", "Account",
                    new { returnUrl = Request.Path + Request.QueryString });
            }
        }

        // GET: /Autoconer
        public async Task<IActionResult> Index(string? buscar, List<string>? estado, int page = 1)
        {
            if (estado == null || estado.Count == 0) estado = new List<string> { "1" };

            const int pageSize = 10;

            // Obtener preparatorias Autoconer desde Oracle (tipoMaquina = "A")
            var resultado = await _recetaService.ObtenerPreparatoriasAsync(
                filtroLote: buscar,
                filtroMaquina: null,
                filtroTipoMaquina: "A",
                filtroEstados: estado,
                page: page,
                pageSize: pageSize);

            var preparatorias = resultado.Items;

            // Cruzar con registros locales (RegistrosAutoconer)
            if (preparatorias.Count > 0)
            {
                var lotes = preparatorias
                    .Where(p => !string.IsNullOrEmpty(p.Lote))
                    .Select(p => p.Lote).Distinct().ToList();

                var recetas = preparatorias
                    .Where(p => !string.IsNullOrEmpty(p.Receta))
                    .Select(p => p.Receta).Distinct().ToList();

                var locales = await _context.RegistrosAutoconer
                    .Where(r => (r.CodigoReceta != null && recetas.Contains(r.CodigoReceta))
                             || lotes.Contains(r.Lote))
                    .Select(r => new { r.Id, r.CodigoReceta, r.Lote, r.Fecha })
                    .ToListAsync();

                foreach (var p in preparatorias)
                {
                    var fechaStr = p.FechaInicio.ToString("yyyy-MM-dd HH:mm:ss");
                    if (!string.IsNullOrEmpty(p.Receta))
                    {
                        p.LocalId = locales.FirstOrDefault(l =>
                            l.CodigoReceta == p.Receta &&
                            l.Fecha.ToString("yyyy-MM-dd HH:mm:ss") == fechaStr)?.Id;
                    }
                    else
                    {
                        p.LocalId = locales.FirstOrDefault(l =>
                            (l.CodigoReceta == null || l.CodigoReceta == string.Empty) &&
                            l.Lote == p.Lote &&
                            l.Fecha.ToString("yyyy-MM-dd HH:mm:ss") == fechaStr)?.Id;
                    }
                }

                // Crear registro local para registros de Oracle sin contraparte en SQLite
                foreach (var p in preparatorias.Where(p => !p.LocalId.HasValue).ToList())
                {
                    try
                    {
                        var nuevoReg = new RegistroAutoconer
                        {
                            CodigoReceta      = string.IsNullOrEmpty(p.Receta) ? null : p.Receta,
                            Lote              = string.IsNullOrEmpty(p.Lote) ? "-" : p.Lote,
                            DescripcionMaterial = string.IsNullOrEmpty(p.Material) ? "-" : p.Material,
                            NumeroAutoconer   = string.IsNullOrEmpty(p.CodigoMaquina) ? "-" : p.CodigoMaquina,
                            Titulo            = string.IsNullOrEmpty(p.Titulo) ? "-" : p.Titulo,
                            Fecha             = p.FechaInicio,
                            CodigoOperador    = string.IsNullOrEmpty(p.CodigoOperario) ? "-" : p.CodigoOperario,
                            Turno             = string.IsNullOrEmpty(p.Turno) ? "-" : p.Turno,
                            Estado            = EstadoOrden.EnProceso,
                            Cerrado           = false
                        };
                        _context.RegistrosAutoconer.Add(nuevoReg);
                        await _context.SaveChangesAsync();
                        p.LocalId = nuevoReg.Id;
                        _logger.LogInformation("Registro Autoconer local creado: Lote={Lote}, Fecha={Fecha}, Id={Id}",
                            p.Lote, p.FechaInicio, nuevoReg.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "No se pudo crear registro Autoconer local: Lote={Lote}", p.Lote);
                    }
                }
            }

            ViewBag.Buscar      = buscar;
            ViewBag.EstadoFiltro = estado;
            ViewBag.ReturnUrl   = Request.Path + Request.QueryString;
            ViewBag.Page        = page;
            ViewBag.PageSize    = pageSize;
            ViewBag.TotalCount  = resultado.TotalCount;
            ViewBag.TotalPages  = resultado.TotalCount == 0 ? 1 : (int)Math.Ceiling((double)resultado.TotalCount / pageSize);

            return View(preparatorias);
        }

        // GET: /Autoconer/Crear
        [HttpGet]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        public async Task<IActionResult> Crear()
        {
            ViewBag.Titulos   = await _recetaService.ObtenerTitulosAsync();
            ViewBag.Maquinas  = await _recetaService.ObtenerMaquinasPorTipoAsync("A");
            return View(new RegistroAutoconer());
        }

        // POST: /Autoconer/Crear
        [HttpPost]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(RegistroAutoconer model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Validar que la máquina no tenga una preparatoria en proceso
                    if (!string.IsNullOrEmpty(model.NumeroAutoconer))
                    {
                        var enProceso = await _recetaService.TieneMaquinaEnProcesoAsync("A", model.NumeroAutoconer);
                        if (enProceso)
                        {
                            ModelState.AddModelError(string.Empty,
                                "La máquina Autoconer seleccionada ya tiene un registro en proceso. No se puede registrar otro hasta que el anterior sea cerrado o anulado.");
                            await CargarViewBag();
                            return View(model);
                        }
                    }

                    model.Estado  = EstadoOrden.EnProceso;
                    model.Cerrado = false;

                    if (model.Fecha == default || model.Fecha == DateTime.MinValue)
                        model.Fecha = DateTime.Now;

                    _logger.LogInformation("Creando registro Autoconer: Lote={Lote}, Maquina={Maquina}", model.Lote, model.NumeroAutoconer);

                    // Guardar en SQLite
                    _context.RegistrosAutoconer.Add(model);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Registro Autoconer guardado en SQLite con Id={Id}", model.Id);

                    // Insertar en Oracle reutilizando InsertarPreparatoriaAsync con CodigoMaquina = "A"
                    var ordenTemporal = MapearAOrdenProduccion(model);
                    var insertadoEnOracle = await _recetaService.InsertarPreparatoriaAsync(ordenTemporal, User.Identity?.Name);

                    if (insertadoEnOracle)
                    {
                        TempData["Success"] = "Registro Autoconer creado exitosamente y registrado en Oracle.";
                        _logger.LogInformation("Registro Autoconer {Id} creado y registrado en Oracle", model.Id);
                    }
                    else
                    {
                        TempData["Warning"] = "Registro Autoconer creado, pero no se pudo registrar en Oracle. Revise los logs.";
                        _logger.LogWarning("Registro Autoconer {Id} creado pero falló el registro en Oracle", model.Id);
                    }

                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al crear registro Autoconer. InnerException: {InnerException}",
                        ex.InnerException?.Message ?? "N/A");
                    TempData["Error"] = $"Error al crear el registro Autoconer: {ex.InnerException?.Message ?? ex.Message}";
                }
            }
            else
            {
                _logger.LogWarning("ModelState inválido al crear Autoconer. Errores: {Errors}",
                    string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            }

            await CargarViewBag();
            return View(model);
        }

        // GET: /Autoconer/Editar/5
        [HttpGet]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        public async Task<IActionResult> Editar(int id, string? returnUrl = null)
        {
            var registro = await _context.RegistrosAutoconer.FindAsync(id);
            if (registro == null)
            {
                TempData["Error"] = "Registro Autoconer no encontrado.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.NombreOperario = await _recetaService.ObtenerNombreEmpleadoAsync(registro.CodigoOperador);
            ViewBag.Titulos        = await _recetaService.ObtenerTitulosAsync();
            ViewBag.Maquinas       = await _recetaService.ObtenerMaquinasPorTipoAsync("A");
            ViewBag.ReturnUrl      = returnUrl;
            return View(registro);
        }

        // POST: /Autoconer/Editar/5
        [HttpPost]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(int id, RegistroAutoconer model, string? returnUrl = null)
        {
            if (id != model.Id) return BadRequest();

            if (ModelState.IsValid)
            {
                var registro = await _context.RegistrosAutoconer.FindAsync(id);
                if (registro == null)
                {
                    TempData["Error"] = "Registro Autoconer no encontrado.";
                    return RedirectToAction(nameof(Index));
                }

                // Guardar valores originales para Oracle
                var oldReceta       = registro.CodigoReceta;
                var oldLote         = registro.Lote;
                var oldCodMaq       = registro.NumeroAutoconer;
                var oldTitulo       = registro.Titulo;
                var oldFechaInicio  = registro.Fecha;

                // Actualizar campos
                registro.CodigoReceta       = model.CodigoReceta;
                registro.Lote               = model.Lote;
                registro.DescripcionMaterial = model.DescripcionMaterial;
                registro.NumeroAutoconer    = model.NumeroAutoconer;
                registro.Titulo             = model.Titulo;
                registro.CodigoOperador     = model.CodigoOperador;
                registro.Turno              = model.Turno;
                registro.Fecha              = model.Fecha;
                registro.Color              = model.Color;
                registro.VelocidadMMin      = model.VelocidadMMin;
                registro.HusosInactivos     = model.HusosInactivos;
                registro.HoraInicio         = model.HoraInicio;
                registro.HoraFinal          = model.HoraFinal;
                registro.Bloque             = model.Bloque;
                registro.PesoBruto          = model.PesoBruto;
                registro.Cantidad           = model.Cantidad;
                registro.Puntaje            = model.Puntaje;
                registro.Tramo1             = model.Tramo1;
                registro.Tramo2             = model.Tramo2;
                registro.Tramo3             = model.Tramo3;
                registro.Tramo4             = model.Tramo4;
                registro.Tramo5             = model.Tramo5;
                registro.Tramo6             = model.Tramo6;
                registro.Destino            = model.Destino;
                registro.Cliente            = model.Cliente;
                registro.Reproceso          = model.Reproceso;
                registro.MotivoParalizacion = model.MotivoParalizacion;

                await _context.SaveChangesAsync();

                // UPDATE en Oracle
                var actualizadoEnOracle = await _recetaService.ActualizarPreparatoriaOracleAsync(
                    oldReceta, oldLote, "A", oldCodMaq, oldTitulo, oldFechaInicio,
                    registro.CodigoReceta, registro.Lote, "A", registro.NumeroAutoconer, registro.Titulo,
                    registro.CodigoOperador, registro.Turno, null, registro.Fecha,
                    null, registro.HusosInactivos.HasValue ? (decimal?)registro.HusosInactivos.Value : null,
                    User.Identity?.Name, registro.VelocidadMMin, null);

                TempData["Success"] = "Registro Autoconer actualizado correctamente.";
                if (!actualizadoEnOracle)
                    _logger.LogWarning("Registro Autoconer {Id} actualizado en SQLite pero falló en Oracle.", id);

                return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction(nameof(Index));
            }

            ViewBag.NombreOperario = await _recetaService.ObtenerNombreEmpleadoAsync(model.CodigoOperador);
            ViewBag.Titulos        = await _recetaService.ObtenerTitulosAsync();
            ViewBag.Maquinas       = await _recetaService.ObtenerMaquinasPorTipoAsync("A");
            ViewBag.ReturnUrl      = returnUrl;
            return View(model);
        }

        // GET: /Autoconer/Detalle/5
        [HttpGet]
        public async Task<IActionResult> Detalle(int id)
        {
            var registro = await _context.RegistrosAutoconer.FindAsync(id);
            if (registro == null)
            {
                TempData["Error"] = "Registro Autoconer no encontrado.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.NombreOperario = await _recetaService.ObtenerNombreEmpleadoAsync(registro.CodigoOperador);
            return View(registro);
        }

        // POST: /Autoconer/Anular/5
        [HttpPost]
        [Authorize(Roles = "Admin,Gerencia")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Anular(int id, string? returnUrl = null)
        {
            try
            {
                var registro = await _context.RegistrosAutoconer.FindAsync(id);
                if (registro == null)
                {
                    TempData["Error"] = "Registro Autoconer no encontrado.";
                    return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction(nameof(Index));
                }

                if (registro.Estado == EstadoOrden.Anulado)
                {
                    TempData["Warning"] = "El registro ya se encuentra anulado.";
                    return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction(nameof(Index));
                }

                registro.Estado = EstadoOrden.Anulado;
                _context.RegistrosAutoconer.Update(registro);
                await _context.SaveChangesAsync();

                var anulado = await _recetaService.AnularPreparatoriaOracleAsync(
                    registro.CodigoReceta,
                    registro.Lote,
                    "A",
                    registro.NumeroAutoconer,
                    registro.Titulo,
                    registro.Fecha);

                TempData["Success"] = anulado
                    ? "Registro Autoconer anulado exitosamente."
                    : "Registro anulado localmente, pero no se pudo actualizar en Oracle.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al anular registro Autoconer con Id={Id}", id);
                TempData["Error"] = $"Error al anular el registro: {ex.Message}";
            }

            return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction(nameof(Index));
        }

        // ===================== API JSON =====================

        [HttpGet]
        public async Task<IActionResult> BuscarReceta(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo))
                return Json(new { success = false, message = "Código requerido" });

            try
            {
                var resultados = await _recetaService.BuscarRecetaPorCodigoAsync(codigo);
                if (resultados.Count == 0)
                    return Json(new { success = false, message = "No se encontró la receta" });

                if (resultados.Count == 1)
                    return Json(new
                    {
                        success = true,
                        data    = new { numero = resultados[0].Numero, lote = resultados[0].Lote, material = resultados[0].Material }
                    });

                return Json(new
                {
                    success  = true,
                    multiple = true,
                    items    = resultados.Select(r => new { numero = r.Numero, lote = r.Lote, material = r.Material })
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> BuscarLote(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo))
                return Json(new { success = false, message = "Código requerido" });

            try
            {
                var resultados = await _recetaService.BuscarLotePorCodigoAsync(codigo);
                if (resultados.Count == 0)
                    return Json(new { success = false, message = "No se encontró el lote" });

                if (resultados.Count == 1)
                    return Json(new
                    {
                        success = true,
                        data    = new { lote = resultados[0].Lote, receta = resultados[0].Receta, material = resultados[0].Material }
                    });

                return Json(new
                {
                    success  = true,
                    multiple = true,
                    items    = resultados.Select(r => new { lote = r.Lote, receta = r.Receta, material = r.Material })
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> BuscarOperario(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo))
                return Json(new { success = false, message = "Código requerido" });

            try
            {
                var resultados = await _recetaService.BuscarOperarioAsync(codigo);
                if (resultados.Count == 0)
                    return Json(new { success = false, message = "No se encontraron operarios" });

                return Json(new
                {
                    success = true,
                    items   = resultados.Select(r => new { codigo = r.Codigo, nombre = r.NombreCorto })
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerMaquinasAutoconer()
        {
            try
            {
                var maquinas = await _recetaService.ObtenerMaquinasPorTipoAsync("A");
                return Json(new
                {
                    success  = true,
                    maquinas = maquinas.Select(m => new { codigo = m.CodigoMaquina, descripcion = m.DescripcionMaquina })
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerTitulos()
        {
            try
            {
                var titulos = await _recetaService.ObtenerTitulosAsync();
                return Json(new
                {
                    success = true,
                    titulos = titulos.Select(t => new { codigo = t.Titulo, descripcion = t.Descripcion })
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerHusosMaquina(string codMaq)
        {
            if (string.IsNullOrWhiteSpace(codMaq))
                return Json(new { success = false, husos = 0 });

            try
            {
                var husos = await _recetaService.ObtenerHusosMaquinaAsync("A", codMaq);
                return Json(new { success = true, husos });
            }
            catch
            {
                return Json(new { success = false, husos = 0 });
            }
        }

        // ===================== Helpers =====================

        private async Task CargarViewBag()
        {
            ViewBag.Titulos  = await _recetaService.ObtenerTitulosAsync();
            ViewBag.Maquinas = await _recetaService.ObtenerMaquinasPorTipoAsync("A");
        }

        /// <summary>
        /// Mapea un RegistroAutoconer a una OrdenProduccion temporal para reutilizar InsertarPreparatoriaAsync.
        /// CodigoMaquina se fija en "A" (Autoconer).
        /// </summary>
        private static OrdenProduccion MapearAOrdenProduccion(RegistroAutoconer r) => new()
        {
            CodigoReceta      = r.CodigoReceta,
            Lote              = r.Lote,
            DescripcionMaterial = r.DescripcionMaterial,
            CodigoMaquina     = "A",
            Maquina           = r.NumeroAutoconer,
            Titulo            = r.Titulo,
            FechaInicio       = r.Fecha,
            EmpleadoId        = r.CodigoOperador,
            Turno             = r.Turno,
            Velocidad         = r.VelocidadMMin,
            HorasInactivas    = r.HusosInactivos.HasValue ? (decimal?)r.HusosInactivos.Value : null,
            FechaFin          = r.HoraFinal,
            Estado            = EstadoOrden.EnProceso,
            Cerrado           = false
        };
    }
}
