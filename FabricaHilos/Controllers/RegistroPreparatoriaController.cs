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
    public class RegistroPreparatoriaController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IRecetaService _recetaService;
        private readonly IParoService   _paroService;
        private readonly ILogger<RegistroPreparatoriaController> _logger;

        public RegistroPreparatoriaController(
            ApplicationDbContext context, 
            IRecetaService recetaService,
            IParoService   paroService,
            ILogger<RegistroPreparatoriaController> logger)
        {
            _context       = context;
            _recetaService = recetaService;
            _paroService   = paroService;
            _logger        = logger;
        }

        private static readonly HashSet<string> _apiActions = new(StringComparer.OrdinalIgnoreCase)
        {
            nameof(GetDetallePreparatoria),
            nameof(BuscarReceta),
            nameof(BuscarLote),
            nameof(BuscarOperario),
            nameof(ObtenerMaquinasPorTipo),
            nameof(ObtenerPesoTitulo),
            nameof(ObtenerHusosMaquina),
            nameof(GetMotivosParaModal),
            nameof(GuardarParos),
            nameof(GetParosPorMaquina),
            nameof(EliminarParoBD),
            nameof(AgregarRollo),
            nameof(GetRollosBatan),
            nameof(CerrarPreparatoriaBatan)
        };

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            base.OnActionExecuting(context);
            if (context.ActionDescriptor.RouteValues.TryGetValue("action", out var action) &&
                _apiActions.Contains(action ?? string.Empty))
                return;

            if (string.IsNullOrEmpty(HttpContext.Session.GetString("OracleUser")))
            {
                _logger.LogWarning("Sesión Oracle expirada en RegistroPreparatoria. Redirigiendo al login.");
                TempData["Warning"] = "Su sesión Oracle ha expirado. Por favor, inicie sesión nuevamente.";
                context.Result = RedirectToAction("Login", "Account",
                    new { returnUrl = Request.Path + Request.QueryString });
            }
        }

        public async Task<IActionResult> Index(string? buscar, string? maquina, string? tipoMaquina, List<string>? estado, int page = 1)
        {
            // Si no se indica estado, mostrar solo 'En Proceso' por defecto
            if (estado == null || estado.Count == 0) estado = new List<string> { "1" };

            // Obtener preparatorias desde Oracle filtradas por estado
            const int pageSize = 10;
            var resultado = await _recetaService.ObtenerPreparatoriasAsync(buscar, maquina, tipoMaquina, estado, page, pageSize);
            var preparatorias = resultado.Items;

            // Cruzar con registros locales para obtener el Id de SQLite (para acciones de edición).
            // Receta es opcional: si existe se cruza por CodigoReceta + FechaInicio (segundos);
            // si la fila solo tiene Lote, se cruza por Lote + FechaInicio.
            if (preparatorias.Count > 0)
            {
                var recetas = preparatorias
                    .Where(p => !string.IsNullOrEmpty(p.Receta))
                    .Select(p => p.Receta).Distinct().ToList();

                var lotesSinReceta = preparatorias
                    .Where(p => string.IsNullOrEmpty(p.Receta) && !string.IsNullOrEmpty(p.Lote))
                    .Select(p => p.Lote).Distinct().ToList();

                var locales = await _context.OrdenesProduccion
                    .Where(o => (!string.IsNullOrEmpty(o.CodigoReceta) && recetas.Contains(o.CodigoReceta))
                             || (string.IsNullOrEmpty(o.CodigoReceta) && o.Lote != null && lotesSinReceta.Contains(o.Lote)))
                    .Select(o => new { o.Id, o.CodigoReceta, o.Lote, o.FechaInicio })
                    .ToListAsync();

                foreach (var p in preparatorias)
                {
                    var fechaStr = p.FechaInicio.ToString("yyyy-MM-dd HH:mm:ss");

                    if (!string.IsNullOrEmpty(p.Receta))
                    {
                        // Fila con receta: cruzar por CodigoReceta + FechaInicio
                        p.LocalId = locales.FirstOrDefault(l =>
                            l.CodigoReceta == p.Receta &&
                            l.FechaInicio.ToString("yyyy-MM-dd HH:mm:ss") == fechaStr)?.Id;
                    }
                    else
                    {
                        // Fila sin receta: cruzar por Lote + FechaInicio
                        p.LocalId = locales.FirstOrDefault(l =>
                            string.IsNullOrEmpty(l.CodigoReceta) &&
                            l.Lote == p.Lote &&
                            l.FechaInicio.ToString("yyyy-MM-dd HH:mm:ss") == fechaStr)?.Id;
                    }
                }

                // Crear registro local para preparatorias de Oracle sin contraparte en SQLite
                foreach (var p in preparatorias.Where(p => !p.LocalId.HasValue).ToList())
                {
                    try
                    {
                        var nuevaOrden = new OrdenProduccion
                        {
                            CodigoReceta = string.IsNullOrEmpty(p.Receta) ? null : p.Receta,
                            Lote = string.IsNullOrEmpty(p.Lote) ? "-" : p.Lote,
                            DescripcionMaterial = string.IsNullOrEmpty(p.Material) ? "-" : p.Material,
                            CodigoMaquina = string.IsNullOrEmpty(p.TipoMaquina) ? "-" : p.TipoMaquina,
                            Maquina = string.IsNullOrEmpty(p.CodigoMaquina) ? "-" : p.CodigoMaquina,
                            Titulo = string.IsNullOrEmpty(p.Titulo) ? "-" : p.Titulo,
                            FechaInicio = p.FechaInicio,
                            EmpleadoId = string.IsNullOrEmpty(p.CodigoOperario) ? "-" : p.CodigoOperario,
                            Turno = string.IsNullOrEmpty(p.Turno) ? "-" : p.Turno,
                            PasoManuar = string.IsNullOrEmpty(p.PasoManual) ? "-" : p.PasoManual,
                            Estado = EstadoOrden.EnProceso,
                            Cerrado = false
                        };
                        _context.OrdenesProduccion.Add(nuevaOrden);
                        await _context.SaveChangesAsync();
                        p.LocalId = nuevaOrden.Id;
                        _logger.LogInformation("Registro local creado para Oracle: Receta={Receta}, Lote={Lote}, FechaInicio={FechaInicio}, Id={Id}",
                            p.Receta, p.Lote, p.FechaInicio, nuevaOrden.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "No se pudo crear registro local para Oracle: Receta={Receta}, Lote={Lote}", p.Receta, p.Lote);
                    }
                }
            }

            // Verificar qué máquinas tienen paros registrados (para botón "Ver Paros" en estados Terminado/Anulado)
            var maqParaParos = preparatorias
                .Where(p => (p.Estado == "3" || p.Estado == "9")
                         && !string.IsNullOrEmpty(p.TipoMaquina)
                         && !string.IsNullOrEmpty(p.CodigoMaquina))
                .Select(p => (p.TipoMaquina, p.CodigoMaquina))
                .Distinct()
                .ToList();

            if (maqParaParos.Count > 0)
            {
                var maquinasConParos = await _paroService.ObtenerMaquinasConParosAsync(maqParaParos);
                foreach (var p in preparatorias.Where(p => p.Estado == "3" || p.Estado == "9"))
                    p.TieneParos = maquinasConParos.Contains($"{p.TipoMaquina}|{p.CodigoMaquina}");
            }

            ViewBag.Buscar = buscar;
            ViewBag.TipoMaquinaFiltro = tipoMaquina;
            ViewBag.MaquinaFiltro = maquina;
            ViewBag.EstadoFiltro = estado;
            ViewBag.ReturnUrl = Request.Path + Request.QueryString;

            // Solo mostrar en el combo los tipos de máquina que realmente tienen filas en la lista actual
            var todosTipos = await _recetaService.ObtenerTiposMaquinasAsync();
            var tiposConDatos = preparatorias
                .Where(p => !string.IsNullOrEmpty(p.TipoMaquina))
                .Select(p => p.TipoMaquina)
                .Distinct()
                .ToHashSet();
            ViewBag.TiposMaquinasFiltro = todosTipos
                .Where(t => tiposConDatos.Contains(t.TipoMaquina))
                .ToList();

            // Obtener lista de máquinas únicas desde Oracle para el combo (código + descripción)
            var maquinasUnicas = preparatorias
                .Where(p => !string.IsNullOrEmpty(p.CodigoMaquina))
                .GroupBy(p => p.CodigoMaquina)
                .Select(g => new
                {
                    Codigo = g.Key,
                    Descripcion = string.IsNullOrEmpty(g.First().DescripcionMaquina)
                        ? g.Key
                        : g.First().DescripcionMaquina
                })
                .OrderBy(m => m.Descripcion)
                .ToList();

            ViewBag.Maquinas = maquinasUnicas;

            ViewBag.Page       = page;
            ViewBag.PageSize   = pageSize;
            ViewBag.TotalCount = resultado.TotalCount;
            ViewBag.TotalPages = resultado.TotalCount == 0 ? 1 : (int)Math.Ceiling((double)resultado.TotalCount / pageSize);

            return View(preparatorias);
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        public async Task<IActionResult> Crear()
        {
            ViewBag.Empleados = await _recetaService.ObtenerEmpleadosAsync();
            ViewBag.TiposMaquinas = await _recetaService.ObtenerTiposMaquinasAsync();
            ViewBag.Titulos = await _recetaService.ObtenerTitulosAsync();
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(OrdenProduccion model)
        {
            if (ModelState.IsValid)
            {
                // Validar Metraje: obligatorio para todos los tipos excepto L (CARDAS)
                if (model.CodigoMaquina != "L" && model.CodigoMaquina != "B" && !model.Metraje.HasValue)
                {
                    ModelState.AddModelError("Metraje", "El campo Metraje es obligatorio.");
                }
                else
                {
                try
                {
                    // Validar que la máquina no tenga una preparatoria en proceso
                    if (!string.IsNullOrEmpty(model.CodigoMaquina) && !string.IsNullOrEmpty(model.Maquina))
                    {
                        var enProceso = await _recetaService.TieneMaquinaEnProcesoAsync(model.CodigoMaquina, model.Maquina);
                        if (enProceso)
                        {
                            ModelState.AddModelError(string.Empty, "La máquina seleccionada ya tiene una preparatoria en proceso. No se puede registrar otra hasta que la anterior sea cerrada o anulada.");
                            ViewBag.Empleados = await _recetaService.ObtenerEmpleadosAsync();
                            ViewBag.TiposMaquinas = await _recetaService.ObtenerTiposMaquinasAsync();
                            ViewBag.Titulos = await _recetaService.ObtenerTitulosAsync();
                            return View(model);
                        }
                    }

                    // Establecer valores por defecto
                    model.Estado = EstadoOrden.EnProceso;
                    model.Cerrado = false;

                    // Asegurar que la fecha de inicio sea la actual
                    if (model.FechaInicio == default(DateTime) || model.FechaInicio == DateTime.MinValue)
                    {
                        model.FechaInicio = DateTime.Now;
                    }

                    _logger.LogInformation("Creando preparatoria: Receta={Receta}, Lote={Lote}, FechaInicio={FechaInicio}", 
                        model.CodigoReceta, model.Lote, model.FechaInicio);

                    // Guardar en base de datos local (SQLite)
                    _context.OrdenesProduccion.Add(model);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Preparatoria guardada en SQLite con Id={Id}", model.Id);

                    // Insertar en Oracle (H_RPRODUC)
                    var insertadoEnOracle = await _recetaService.InsertarPreparatoriaAsync(model, User.Identity?.Name);

                    if (insertadoEnOracle)
                    {
                        TempData["Success"] = "Preparatoria creada exitosamente y registrada en Oracle.";
                        _logger.LogInformation("Preparatoria {Id} creada y registrada en Oracle exitosamente", model.Id);
                    }
                    else
                    {
                        TempData["Warning"] = "Preparatoria creada, pero no se pudo registrar en Oracle. Revise los logs.";
                        _logger.LogWarning("Preparatoria {Id} creada pero falló el registro en Oracle", model.Id);
                    }

                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al crear preparatoria. InnerException: {InnerException}", 
                        ex.InnerException?.Message ?? "N/A");

                    var errorMsg = ex.InnerException?.Message ?? ex.Message;
                    TempData["Error"] = $"Error al crear la preparatoria: {errorMsg}";
                }
            } // end else (Metraje válido)
            }
            else
            {
                _logger.LogWarning("ModelState inválido al crear preparatoria. Errores: {Errors}",
                    string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            }

            ViewBag.Empleados = await _recetaService.ObtenerEmpleadosAsync();
            ViewBag.TiposMaquinas = await _recetaService.ObtenerTiposMaquinasAsync();
            ViewBag.Titulos = await _recetaService.ObtenerTitulosAsync();
            return View(model);
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        public async Task<IActionResult> Editar(int id, string? returnUrl = null)
        {
            var orden = await _context.OrdenesProduccion.FindAsync(id);
            if (orden == null)
            {
                TempData["Error"] = "Preparatoria no encontrada.";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Empleados = await _recetaService.ObtenerEmpleadosAsync();
            ViewBag.NombreOperario = await _recetaService.ObtenerNombreEmpleadoAsync(orden.EmpleadoId ?? string.Empty);
            ViewBag.TiposMaquinas = await _recetaService.ObtenerTiposMaquinasAsync();
            ViewBag.Titulos = await _recetaService.ObtenerTitulosAsync();
            ViewBag.ReturnUrl = returnUrl;
            return View(orden);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(int id, OrdenProduccion model, string? returnUrl = null)
        {
            if (id != model.Id) return BadRequest();
            if (ModelState.IsValid)
            {
                var orden = await _context.OrdenesProduccion.FindAsync(id);
                if (orden == null)
                {
                    TempData["Error"] = "Preparatoria no encontrada.";
                    return RedirectToAction(nameof(Index));
                }

                // Guardar valores originales ANTES de modificar (se usan en el WHERE del Oracle UPDATE)
                var oldReceta       = orden.CodigoReceta;
                var oldLote         = orden.Lote;
                var oldTpMaq        = orden.CodigoMaquina;
                var oldCodMaq       = orden.Maquina;
                var oldTitulo       = orden.Titulo;
                var oldFechaInicio  = orden.FechaInicio;

                // Actualizar los campos editables, incluida la Fecha de Inicio.
                orden.CodigoReceta        = model.CodigoReceta;
                orden.Lote                = model.Lote;
                orden.DescripcionMaterial = model.DescripcionMaterial;
                orden.CodigoMaquina       = model.CodigoMaquina;
                orden.Maquina             = model.Maquina;
                orden.Titulo              = model.Titulo;
                orden.EmpleadoId          = model.EmpleadoId;
                orden.Turno               = model.Turno;
                orden.PasoManuar          = model.PasoManuar;
                orden.ContadorInicial      = model.ContadorInicial;
                orden.HorasInactivas       = model.HorasInactivas;
                orden.FechaInicio         = model.FechaInicio;
                orden.Velocidad           = model.Velocidad;
                orden.Metraje             = model.Metraje;

                await _context.SaveChangesAsync();

                // UPDATE en Oracle (H_RPRODUC). Se actualiza también FECHA_INI con la nueva fecha.
                _logger.LogInformation("Editando preparatoria {Id} en Oracle como usuario {User}", id, User.Identity?.Name);
                var actualizadoEnOracle = await _recetaService.ActualizarPreparatoriaOracleAsync(
                    oldReceta, oldLote, oldTpMaq, oldCodMaq, oldTitulo, oldFechaInicio,
                    orden.CodigoReceta, orden.Lote, orden.CodigoMaquina, orden.Maquina, orden.Titulo,
                    orden.EmpleadoId, orden.Turno, orden.PasoManuar, orden.FechaInicio,
                    orden.ContadorInicial, orden.HorasInactivas, User.Identity?.Name, orden.Velocidad, orden.Metraje);

                TempData["Success"] = "Preparatoria actualizada correctamente.";
                if (actualizadoEnOracle)
                    _logger.LogInformation("Preparatoria {Id} actualizada en Oracle y SQLite.", id);
                else
                    _logger.LogWarning("Preparatoria {Id} actualizada en SQLite pero falló en Oracle.", id);

                return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction(nameof(Index));
            }
            ViewBag.Empleados = await _recetaService.ObtenerEmpleadosAsync();
            ViewBag.TiposMaquinas = await _recetaService.ObtenerTiposMaquinasAsync();
            ViewBag.Titulos = await _recetaService.ObtenerTitulosAsync();
            ViewBag.ReturnUrl = returnUrl;
            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Gerencia")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Anular(int id, string? returnUrl = null)
        {
            try
            {
                var orden = await _context.OrdenesProduccion.FindAsync(id);
                if (orden == null)
                {
                    TempData["Error"] = "Preparatoria no encontrada.";
                    return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction(nameof(Index));
                }

                if (orden.Estado == EstadoOrden.Anulado)
                {
                    TempData["Warning"] = "La preparatoria ya se encuentra anulada.";
                    return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction(nameof(Index));
                }

                // Actualizar estado local
                orden.Estado = EstadoOrden.Anulado;
                _context.OrdenesProduccion.Update(orden);
                await _context.SaveChangesAsync();

                // Actualizar ESTADO = '9' en Oracle (H_RPRODUC) usando todos los campos clave
                var anulado = await _recetaService.AnularPreparatoriaOracleAsync(
                    orden.CodigoReceta,
                    orden.Lote,
                    orden.CodigoMaquina,
                    orden.Maquina,
                    orden.Titulo,
                    orden.FechaInicio);

                // Si es BATAN, actualizar A_MDUSER y A_MDFECHA en el último rollo asociado
                if (orden.CodigoMaquina == "B")
                {
                    var fechaTurno = orden.FechaInicio.Hour < 7
                        ? orden.FechaInicio.Date.AddDays(-1)
                        : orden.FechaInicio.Date;

                    await _recetaService.ActualizarUltimoRolloBatanAsync(
                        fechaTurno,
                        orden.Turno ?? string.Empty,
                        orden.CodigoMaquina,
                        orden.Maquina ?? string.Empty,
                        orden.FechaInicio,
                        User.Identity?.Name);
                }

                if (anulado)
                {
                    TempData["Success"] = $"Preparatoria {orden.CodigoReceta} anulada exitosamente.";
                    _logger.LogInformation("Preparatoria {CodigoReceta} anulada en Oracle y SQLite.", orden.CodigoReceta);
                }
                else
                {
                    TempData["Warning"] = $"Preparatoria {orden.CodigoReceta} anulada localmente, pero no se pudo actualizar en Oracle.";
                    _logger.LogWarning("Preparatoria {CodigoReceta} anulada en SQLite pero falló en Oracle.", orden.CodigoReceta);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al anular preparatoria con Id={Id}", id);
                TempData["Error"] = $"Error al anular la preparatoria: {ex.Message}";
            }

            return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CerrarPreparatoria(int id)
        {
            try
            {
                var orden = await _context.OrdenesProduccion.FindAsync(id);
                if (orden == null)
                {
                    TempData["Error"] = "Preparatoria no encontrada.";
                    return RedirectToAction(nameof(Index));
                }

                if (orden.Cerrado)
                {
                    TempData["Warning"] = "La preparatoria ya está cerrada.";
                    return RedirectToAction(nameof(Index));
                }

                // Verificar paros sin fecha fin antes de cerrar
                var tieneParosAbiertos = await _paroService.TieneParosAbiertosAsync(
                    orden.CodigoMaquina ?? string.Empty, orden.Maquina ?? string.Empty);
                if (tieneParosAbiertos)
                {
                    TempData["Error"] = "No se puede cerrar la preparatoria porque tiene paros registrados sin fecha de fin. Cierre los paros abiertos antes de continuar.";
                    return RedirectToAction(nameof(Index));
                }

                orden.Cerrado = true;
                orden.Estado = EstadoOrden.Terminado;
                _context.OrdenesProduccion.Update(orden);
                await _context.SaveChangesAsync();

                // Actualizar ESTADO = '3' en Oracle (H_RPRODUC)
                var cerrado = await _recetaService.CerrarPreparatoriaOracleAsync(
                    orden.CodigoReceta,
                    orden.Lote,
                    orden.CodigoMaquina,
                    orden.Maquina,
                    orden.Titulo,
                    orden.FechaInicio);

                if (cerrado)
                {
                    TempData["Success"] = $"Preparatoria {orden.CodigoReceta} cerrada exitosamente.";
                    _logger.LogInformation("Preparatoria {CodigoReceta} cerrada en Oracle y SQLite.", orden.CodigoReceta);
                }
                else
                {
                    TempData["Warning"] = $"Preparatoria {orden.CodigoReceta} cerrada localmente, pero no se pudo actualizar en Oracle.";
                    _logger.LogWarning("Preparatoria {CodigoReceta} cerrada en SQLite pero falló en Oracle.", orden.CodigoReceta);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cerrar preparatoria con Id={Id}", id);
                TempData["Error"] = $"Error al cerrar la preparatoria: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        public async Task<IActionResult> DetalleProduccion(int id, string? returnUrl = null)
        {
            var orden = await _context.OrdenesProduccion
                .FirstOrDefaultAsync(o => o.Id == id);

            if (orden == null)
            {
                TempData["Error"] = "Preparatoria no encontrada.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.ReturnUrl = returnUrl;
            return View(orden);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DetalleProduccion(int id, int? rolloTacho, decimal? kgNeto, decimal? contadorFinal, int? nroParada, DateTime? fechaFin = null, string? returnUrl = null)
        {
            var orden = await _context.OrdenesProduccion.FindAsync(id);
            if (orden == null)
            {
                TempData["Error"] = "Preparatoria no encontrada.";
                return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction(nameof(Index));
            }

            var esMetrajeOpcional = orden.CodigoMaquina == "L";
            if ((!orden.Metraje.HasValue && !esMetrajeOpcional) || !kgNeto.HasValue)
            {
                TempData["Error"] = !kgNeto.HasValue
                    ? "El campo Kg Neto es obligatorio."
                    : "El campo Metraje es obligatorio. Por favor edite la preparatoria para ingresar el metraje.";
                ViewBag.ReturnUrl = returnUrl;
                return View(orden);
            }

            var fechaFinEfectiva = fechaFin ?? DateTime.Now;
            if (fechaFinEfectiva < orden.FechaInicio)
            {
                TempData["Error"] = $"La Fecha Final ({fechaFinEfectiva:dd/MM/yyyy HH:mm:ss}) no puede ser anterior a la Fecha Inicial ({orden.FechaInicio:dd/MM/yyyy HH:mm:ss}).";
                ViewBag.ReturnUrl = returnUrl;
                return View(orden);
            }

            // Verificar paros sin fecha fin antes de cerrar la preparatoria
            var tieneParosAbiertos = await _paroService.TieneParosAbiertosAsync(
                orden.CodigoMaquina ?? string.Empty, orden.Maquina ?? string.Empty);
            if (tieneParosAbiertos)
            {
                TempData["Error"] = "No se puede dar por terminada la preparatoria porque tiene paros registrados sin fecha de fin. Cierre los paros abiertos antes de continuar.";
                ViewBag.ReturnUrl = returnUrl;
                return View(orden);
            }

            // Actualizar campos de detalle y cerrar localmente
            orden.RolloTacho = rolloTacho;
            orden.KgNeto    = kgNeto;
            orden.ContadorFinal = contadorFinal;
            orden.NroParada = nroParada;
            orden.Cerrado   = true;
            orden.Estado    = EstadoOrden.Terminado;

            _context.OrdenesProduccion.Update(orden);
            await _context.SaveChangesAsync();

            // UPDATE Oracle + SP_CALCULAR_PROD_ESP_TEO
            var resultado = await _recetaService.GuardarYCerrarDetalleProduccionAsync(
                orden.CodigoReceta, orden.Lote,
                orden.CodigoMaquina, orden.Maquina,
                orden.Titulo, orden.FechaInicio,
                orden.Velocidad, rolloTacho, kgNeto,
                nroParada, contadorFinal, fechaFinEfectiva);

            if (!resultado.UpdateExitoso)
            {
                TempData["Warning"] = "Detalle guardado localmente, pero no se pudo actualizar en Oracle.";
                _logger.LogWarning("DetalleProduccion {Id}: guardado en SQLite pero falló en Oracle.", id);
            }
            else if (resultado.Codigo == "0")
            {
                TempData["Success"] = "Detalle de producción guardado y preparatoria cerrada exitosamente.";
                _logger.LogInformation("DetalleProduccion {Id}: guardado y cerrado en Oracle y SQLite.", id);
            }
            else
            {
                TempData["Warning"] = resultado.Mensaje;
                _logger.LogWarning("DetalleProduccion {Id}: SP retornó código {Codigo}: {Mensaje}.", id, resultado.Codigo, resultado.Mensaje);
            }

            return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        public async Task<IActionResult> GetDetallePreparatoria(int id)
        {
            var orden = await _context.OrdenesProduccion.FindAsync(id);
            if (orden == null) return NotFound();

            // Obtener datos descriptivos desde Oracle en paralelo
            var empleadosTask     = _recetaService.ObtenerEmpleadosAsync();
            var titulosTask       = _recetaService.ObtenerTitulosAsync();
            var tiposMaquinasTask = _recetaService.ObtenerTiposMaquinasAsync();
            var maquinasTask      = string.IsNullOrEmpty(orden.CodigoMaquina)
                ? Task.FromResult(new List<MaquinaIndividualDto>())
                : _recetaService.ObtenerMaquinasPorTipoAsync(orden.CodigoMaquina);
            // Obtener el detalle productivo directamente de Oracle (VELOCIDAD, METRAJE, etc.)
            var detalleOracleTask = _recetaService.ObtenerDetalleProductivoOracleAsync(
                orden.CodigoReceta, orden.Lote,
                orden.CodigoMaquina, orden.Maquina,
                orden.Titulo, orden.FechaInicio);

            await Task.WhenAll(empleadosTask, titulosTask, tiposMaquinasTask, maquinasTask, detalleOracleTask);

            var nombreOperario     = empleadosTask.Result.FirstOrDefault(e => e.Codigo == orden.EmpleadoId)?.NombreCorto;
            if (string.IsNullOrEmpty(nombreOperario) && !string.IsNullOrEmpty(orden.EmpleadoId))
                nombreOperario     = await _recetaService.ObtenerNombreEmpleadoAsync(orden.EmpleadoId);
            nombreOperario       ??= orden.EmpleadoId ?? string.Empty;
            var descripcionTitulo  = titulosTask.Result.FirstOrDefault(t => t.Titulo == orden.Titulo)?.Descripcion
                                     ?? orden.Titulo;
            var tipoMaquinaTexto   = tiposMaquinasTask.Result.FirstOrDefault(t => t.TipoMaquina == orden.CodigoMaquina)?.TextoCompleto
                                     ?? orden.CodigoMaquina;
            var descripcionMaquina = maquinasTask.Result.FirstOrDefault(m => m.CodigoMaquina == orden.Maquina)?.DescripcionMaquina
                                     ?? orden.Maquina;

            // Oracle tiene prioridad; si no hay dato en Oracle se usa SQLite como respaldo
            var ora = detalleOracleTask.Result;
            var velocidad       = ora?.Velocidad     ?? orden.Velocidad;
            var metraje         = ora?.Metraje       ?? orden.Metraje;
            var rolloTacho      = ora?.Unidades      ?? orden.RolloTacho;
            var kgNeto          = ora?.PesoNeto      ?? orden.KgNeto;
            var fechaFin        = ora?.FechaFin      ?? orden.FechaFin;
            var producTeorico   = ora?.ProducTeorico ?? orden.ProducTeorico;
            var eficiencTeorico = ora?.ProdEsperado  ?? orden.EficiencTeorico;
            var husosInactivas  = ora?.HusosInac     ?? orden.HorasInactivas;
            var nroParada       = ora?.NroParada     ?? orden.NroParada;
            var contadorInicial = ora?.ContadorIni   ?? orden.ContadorInicial;
            var contadorFinal   = ora?.ContadorFin   ?? orden.ContadorFinal;

            var estadoCodigo = !string.IsNullOrEmpty(ora?.EstadoOracle)
                ? ora.EstadoOracle
                : orden.Estado switch
                {
                    EstadoOrden.EnProceso => "1",
                    EstadoOrden.Terminado => "3",
                    EstadoOrden.Anulado   => "9",
                    _                     => "0"
                };
            var estadoTexto = estadoCodigo switch
            {
                "1" => "En Proceso",
                "3" => "Terminado",
                "9" => "Anulado",
                _   => orden.Estado.ToString()
            };

            return Json(new
            {
                receta          = orden.CodigoReceta,
                lote            = orden.Lote,
                material        = orden.DescripcionMaterial,
                tipoMaquina     = tipoMaquinaTexto,
                maquina         = descripcionMaquina,
                titulo          = descripcionTitulo,
                fechaInicio     = orden.FechaInicio.ToString("dd/MM/yyyy HH:mm:ss"),
                estado          = estadoTexto,
                estadoCodigo,
                operario        = nombreOperario,
                turno           = ProduccionLookups.GetTurno(orden.Turno),
                pasoManuar      = string.IsNullOrEmpty(orden.PasoManuar) ? null : ProduccionLookups.GetPaso(orden.PasoManuar),
                velocidad,
                metraje,
                rolloTacho,
                kgNeto,
                fechaFin        = fechaFin.HasValue ? fechaFin.Value.ToString("dd/MM/yyyy HH:mm:ss") : null,
                producTeorico,
                eficiencTeorico,
                esPabilera      = orden.CodigoMaquina == "P",
                esBatan         = orden.CodigoMaquina == "B",
                contadorInicial,
                husosInactivas,
                contadorFinal,
                nroParada
            });
        }

        /// <summary>
        /// API para buscar receta en Oracle por código
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        public async Task<IActionResult> BuscarReceta(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo))
            {
                return Json(new { success = false, message = "Código requerido" });
            }

            try
            {
                _logger.LogInformation("API BuscarReceta llamada con código: {Codigo}", codigo);

                var recetas = await _recetaService.BuscarRecetaPorCodigoAsync(codigo);

                if (recetas.Count == 0)
                {
                    _logger.LogWarning("Receta no encontrada para código: {Codigo}", codigo);
                    return Json(new { success = false, message = "No se encontró la receta" });
                }

                if (recetas.Count == 1)
                {
                    var receta = recetas[0];
                    _logger.LogInformation("Una sola receta encontrada, retornando datos");
                    return Json(new
                    {
                        success = true,
                        data = new
                        {
                            numero = receta.Numero,
                            lote = receta.Lote,
                            material = receta.Material
                        }
                    });
                }

                // Múltiples resultados: devolver lista para que el usuario elija
                _logger.LogInformation("{Count} recetas encontradas para código: {Codigo}", recetas.Count, codigo);
                return Json(new
                {
                    success = true,
                    multiple = true,
                    items = recetas.Select(r => new { numero = r.Numero, lote = r.Lote, material = r.Material })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en API BuscarReceta: {Codigo}", codigo);
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        public async Task<IActionResult> BuscarLote(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo))
            {
                return Json(new { success = false, message = "Código requerido" });
            }

            try
            {
                _logger.LogInformation("API BuscarLote llamada con código: {Codigo}", codigo);

                var lotes = await _recetaService.BuscarLotePorCodigoAsync(codigo);

                if (lotes.Count == 0)
                {
                    _logger.LogWarning("Lote no encontrado para código: {Codigo}", codigo);
                    return Json(new { success = false, message = "No se encontró el lote" });
                }

                if (lotes.Count == 1)
                {
                    var lote = lotes[0];
                    _logger.LogInformation("Un solo lote encontrado, retornando datos");
                    return Json(new
                    {
                        success = true,
                        data = new
                        {
                            lote = lote.Lote,
                            receta = lote.Receta,
                            material = lote.Material
                        }
                    });
                }

                // Múltiples resultados: devolver lista para que el usuario elija
                _logger.LogInformation("{Count} lotes encontrados para código: {Codigo}", lotes.Count, codigo);
                return Json(new
                {
                    success = true,
                    multiple = true,
                    items = lotes.Select(l => new { lote = l.Lote, receta = l.Receta, material = l.Material })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en API BuscarLote: {Codigo}", codigo);
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        public async Task<IActionResult> BuscarOperario(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo))
                return Json(new { success = false, message = "Código requerido" });

            try
            {
                var operarios = await _recetaService.BuscarOperarioAsync(codigo);
                if (operarios.Count == 0)
                    return Json(new { success = false, message = "No se encontraron operarios" });

                return Json(new
                {
                    success = true,
                    items = operarios.Select(e => new { codigo = e.Codigo, nombre = e.NombreCorto })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en API BuscarOperario: {Codigo}", codigo);
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        /// <summary>
        /// API para obtener máquinas por tipo desde Oracle
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        public async Task<IActionResult> ObtenerMaquinasPorTipo(string tipoMaquina)
        {
            if (string.IsNullOrWhiteSpace(tipoMaquina))
            {
                return Json(new { success = false, message = "Tipo de máquina requerido" });
            }

            try
            {
                _logger.LogInformation("API ObtenerMaquinasPorTipo llamada con tipo: {TipoMaquina}", tipoMaquina);

                var maquinas = await _recetaService.ObtenerMaquinasPorTipoAsync(tipoMaquina);

                _logger.LogInformation("Se obtuvieron {Count} máquinas", maquinas.Count);
                return Json(new 
                { 
                    success = true, 
                    data = maquinas.Select(m => new 
                    {
                        codigo = m.CodigoMaquina,
                        descripcion = m.DescripcionMaquina,
                        textoCompleto = m.TextoCompleto
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en API ObtenerMaquinasPorTipo: {TipoMaquina}", tipoMaquina);
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        /// <summary>
        /// API para obtener el PESO de un título desde H_TITULOS (usado en cálculo de Kg Neto)
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        public async Task<IActionResult> ObtenerPesoTitulo(string titulo)
        {
            try
            {
                var peso = string.IsNullOrWhiteSpace(titulo)
                    ? 0m
                    : await _recetaService.ObtenerPesoTituloAsync(titulo);

                return Json(new { success = true, peso });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en API ObtenerPesoTitulo: {Titulo}", titulo);
                return Json(new { success = false, peso = 0m });
            }
        }

        /// <summary>
        /// API para obtener los HUSOS de una máquina desde H_MAQUINAS (usado en cálculo de Kg Neto Pabileras)
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        public async Task<IActionResult> ObtenerHusosMaquina(string tpMaq, string codMaq)
        {
            try
            {
                var husos = string.IsNullOrWhiteSpace(tpMaq) || string.IsNullOrWhiteSpace(codMaq)
                    ? 0
                    : await _recetaService.ObtenerHusosMaquinaAsync(tpMaq, codMaq);

                return Json(new { success = true, husos });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en API ObtenerHusosMaquina: {TpMaq}/{CodMaq}", tpMaq, codMaq);
                return Json(new { success = false, husos = 0 });
            }
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        public async Task<IActionResult> GetMotivosParaModal()
        {
            try
            {
                var motivos = await _paroService.ObtenerMotivosAsync();
                return Json(motivos.Select(m => new { codigo = m.Codigo, texto = m.TextoCompleto, descr = m.Descripcion }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener motivos para modal de paro");
                return Json(Array.Empty<object>());
            }
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        public async Task<IActionResult> GetParosPorMaquina(string tpMaq, string codMaq, string? fechaTurno, [FromQuery] List<string>? turnos)
        {
            try
            {
                var fecha = !string.IsNullOrEmpty(fechaTurno) && DateTime.TryParse(fechaTurno, out var dt)
                    ? dt
                    : (DateTime.Now.Hour < 7 ? DateTime.Today.AddDays(-1) : DateTime.Today);
                var turnosFiltro = turnos != null && turnos.Count > 0
                    ? turnos
                    : new List<string> { "1", "2", "3", "4", "5" };
                var paros = await _paroService.ObtenerParosPorMaquinaAsync(tpMaq, codMaq, fecha, turnosFiltro);
                return Json(paros);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener paros por máquina: {TpMaq}/{CodMaq}", tpMaq, codMaq);
                return Json(Array.Empty<object>());
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        public async Task<IActionResult> GuardarParos([FromBody] GuardarParosRequest request)
        {
            var tieneNuevos    = request.Paros          != null && request.Paros.Count          > 0;
            var tieneActualiz  = request.ParosActualizar != null && request.ParosActualizar.Count > 0;

            if (!tieneNuevos && !tieneActualiz)
                return Json(new { success = false, message = "No hay paros en la lista para guardar." });

            var adUser = User.Identity?.Name;
            bool exito = true;

            if (tieneNuevos)
            {
                var ok = await _paroService.InsertarParosBatchAsync(request.TpMaq, request.CodMaq, request.Turno, adUser, request.Paros!);
                if (ok)
                    _logger.LogInformation("Paros insertados por {User}: {Count} registros, Maquina={CodMaq}", adUser, request.Paros!.Count, request.CodMaq);
                else
                    _logger.LogWarning("Fallo al insertar paros para Maquina={CodMaq}", request.CodMaq);
                exito &= ok;
            }

            if (tieneActualiz)
            {
                var ok = await _paroService.ActualizarParosBatchAsync(request.TpMaq, request.CodMaq, adUser, request.ParosActualizar!);
                if (ok)
                    _logger.LogInformation("Paros actualizados por {User}: {Count} registros, Maquina={CodMaq}", adUser, request.ParosActualizar!.Count, request.CodMaq);
                else
                    _logger.LogWarning("Fallo al actualizar paros para Maquina={CodMaq}", request.CodMaq);
                exito &= ok;
            }

            return Json(new
            {
                success = exito,
                message = exito ? "Paros guardados exitosamente." : "Error al guardar los paros. Intente nuevamente."
            });
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        public async Task<IActionResult> EliminarParoBD([FromBody] EliminarParoBDRequest request)
        {
            if (string.IsNullOrEmpty(request.TpMaq) || string.IsNullOrEmpty(request.CodMaq) || string.IsNullOrEmpty(request.FechaIni))
                return Json(new { success = false, message = "Datos incompletos." });

            if (!DateTime.TryParse(request.FechaIni, out var fechaIni))
                return Json(new { success = false, message = "Fecha inválida." });

            var adUser = User.Identity?.Name;
            var ok = await _paroService.EliminarParoAsync(request.TpMaq, request.CodMaq, fechaIni, adUser);
            if (ok)
                _logger.LogInformation("Paro eliminado de BD por {User}: TpMaq={TpMaq}, CodMaq={CodMaq}, FechaIni={FechaIni}", adUser, request.TpMaq, request.CodMaq, request.FechaIni);
            else
                _logger.LogWarning("Fallo al eliminar paro de BD: TpMaq={TpMaq}, CodMaq={CodMaq}, FechaIni={FechaIni}", request.TpMaq, request.CodMaq, request.FechaIni);

            return Json(new { success = ok, message = ok ? "Paro eliminado exitosamente." : "No se pudo eliminar el paro. Intente nuevamente." });
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        public async Task<IActionResult> AgregarRollo([FromBody] AgregarRolloRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.TpMaq) || string.IsNullOrEmpty(request.CodMaq))
                return Json(new { success = false, message = "Datos incompletos." });

            if (request.PesoBruto <= 0)
                return Json(new { success = false, message = "El peso bruto debe ser mayor a 0." });

            if (!DateTime.TryParse(request.FechaTurno, out var fechaTurno))
                return Json(new { success = false, message = "Fecha de turno inválida." });

            const decimal varilla = 1.2m;
            var neto   = Math.Round(request.PesoBruto - varilla, 2);
            var adUser = User.Identity?.Name;

            var ok = await _recetaService.AgregarRolloAsync(
                fechaTurno, request.Turno, request.TpMaq, request.CodMaq, neto, adUser);

            if (ok)
                _logger.LogInformation("Rollo agregado por {User}: TpMaq={TpMaq}, CodMaq={CodMaq}, Neto={Neto}", adUser, request.TpMaq, request.CodMaq, neto);
            else
                _logger.LogWarning("Fallo al agregar rollo: TpMaq={TpMaq}, CodMaq={CodMaq}", request.TpMaq, request.CodMaq);

            return Json(new { success = ok, message = ok ? "Rollo registrado exitosamente." : "Error al registrar el rollo. Intente nuevamente." });
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        public async Task<IActionResult> GetRollosBatan(int id)
        {
            var orden = await _context.OrdenesProduccion.FindAsync(id);
            if (orden == null) return NotFound();

            var fechaTurno = orden.FechaInicio.Hour < 7
                ? orden.FechaInicio.Date.AddDays(-1)
                : orden.FechaInicio.Date;

            var rollos = await _recetaService.ObtenerRollosPorMaquinaAsync(
                fechaTurno, orden.Turno ?? string.Empty,
                orden.CodigoMaquina ?? string.Empty, orden.Maquina ?? string.Empty,
                fechaIni: orden.FechaInicio,
                fechaFin: orden.FechaFin);

            return Json(rollos.Select(r => new { item = r.Item, neto = r.Neto, fechaHora = r.FechaRegistro?.ToString("yyyy-MM-ddTHH:mm:ss") }));
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Gerencia,Supervisor")]
        public async Task<IActionResult> CerrarPreparatoriaBatan([FromBody] CerrarBatanRequest request)
        {
            var orden = await _context.OrdenesProduccion.FindAsync(request.Id);
            if (orden == null)
                return Json(new { success = false, message = "Preparatoria no encontrada." });

            if (orden.CodigoMaquina != "B")
                return Json(new { success = false, message = "Esta acción solo aplica para máquinas tipo B (BATAN)." });

            if (orden.Estado != EstadoOrden.EnProceso)
                return Json(new { success = false, message = "La preparatoria no está en proceso." });

            var tieneParosAbiertos = await _paroService.TieneParosAbiertosAsync(
                orden.CodigoMaquina ?? string.Empty, orden.Maquina ?? string.Empty);
            if (tieneParosAbiertos)
                return Json(new { success = false, message = "No se puede cerrar la preparatoria porque tiene paros registrados sin fecha de fin. Cierre los paros abiertos antes de continuar." });

            var mdUser = User.Identity?.Name;
            var ok = await _recetaService.CerrarPreparatoriaOracleAsync(
                orden.CodigoReceta,
                orden.Lote,
                orden.CodigoMaquina,
                orden.Maquina,
                orden.Titulo,
                orden.FechaInicio,
                mdUser);

            if (ok)
            {
                orden.Estado   = EstadoOrden.Terminado;
                orden.FechaFin = DateTime.Now;
                orden.Cerrado  = true;
                _context.OrdenesProduccion.Update(orden);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Preparatoria BATAN {Id} cerrada por {User}", orden.Id, mdUser);
            }
            else
            {
                _logger.LogWarning("Fallo al cerrar preparatoria BATAN {Id} en Oracle", orden.Id);
            }

            return Json(new { success = ok, message = ok ? "Preparatoria cerrada exitosamente." : "Error al cerrar la preparatoria en Oracle. Intente nuevamente." });
        }
    }
}