using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using FabricaHilos.Data;
using FabricaHilos.Helpers;
using FabricaHilos.Models.Produccion;
using FabricaHilos.Services.Produccion;

namespace FabricaHilos.Controllers.Produccion
{
    [Authorize]
    public class AutoconerController : OracleBaseController
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
            nameof(BuscarPartida),
            nameof(BuscarOperario),
            nameof(ObtenerMaquinasAutoconer),
            nameof(ObtenerTitulos),
            nameof(ObtenerHusosMaquina),
            nameof(ObtenerDestinosAutoconer),
        };

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            // Saltar verificación de sesión para endpoints API internos (AJAX)
            if (context.ActionDescriptor.RouteValues.TryGetValue("action", out var action) &&
                _apiActions.Contains(action ?? string.Empty))
                return;

            base.OnActionExecuting(context);
        }

        // GET: /Autoconer
        public async Task<IActionResult> Index(string? buscar, List<string>? estado, int page = 1)
        {
            if (estado == null || estado.Count == 0) estado = new List<string> { "3" };

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
                    .Select(r => new { 
                        r.Id, 
                        r.CodigoReceta, 
                        r.Lote, 
                        r.Fecha, 
                        r.DescripcionMaterial,
                        r.Guia,
                        r.Tramo1,
                        r.Tramo2,
                        r.Tramo3,
                        r.Tramo4,
                        r.Tramo5,
                        r.Tramo6
                    })
                    .ToListAsync();

                foreach (var p in preparatorias)
                {
                    var fechaStr = p.FechaInicio.ToString("yyyy-MM-dd HH:mm:ss");
                    if (!string.IsNullOrEmpty(p.Receta))
                    {
                        var local = locales.FirstOrDefault(l =>
                            l.CodigoReceta == p.Receta &&
                            l.Fecha.ToString("yyyy-MM-dd HH:mm:ss") == fechaStr);
                        if (local != null)
                        {
                            p.LocalId = local.Id;
                            // PRIORIDAD ORACLE: Solo usar material local si Oracle no tiene dato
                            if ((string.IsNullOrEmpty(p.Material) || p.Material == "-") &&
                                !string.IsNullOrEmpty(local.DescripcionMaterial) && 
                                local.DescripcionMaterial != "-")
                            {
                                p.Material = local.DescripcionMaterial;
                            }
                            // PRIORIDAD ORACLE: Solo usar Guía local si Oracle no tiene dato
                            if (string.IsNullOrEmpty(p.Guia) && !string.IsNullOrEmpty(local.Guia))
                            {
                                p.Guia = local.Guia;
                            }
                            // PRIORIDAD ORACLE: Solo usar Tramos locales si Oracle tiene 0
                            if (p.Tramo1 == 0 && local.Tramo1 > 0) p.Tramo1 = local.Tramo1;
                            if (p.Tramo2 == 0 && local.Tramo2 > 0) p.Tramo2 = local.Tramo2;
                            if (p.Tramo3 == 0 && local.Tramo3 > 0) p.Tramo3 = local.Tramo3;
                            if (p.Tramo4 == 0 && local.Tramo4 > 0) p.Tramo4 = local.Tramo4;
                            if (p.Tramo5 == 0 && local.Tramo5 > 0) p.Tramo5 = local.Tramo5;
                            if (p.Tramo6 == 0 && local.Tramo6 > 0) p.Tramo6 = local.Tramo6;
                        }
                    }
                    else
                    {
                        var local = locales.FirstOrDefault(l =>
                            (l.CodigoReceta == null || l.CodigoReceta == string.Empty) &&
                            l.Lote == p.Lote &&
                            l.Fecha.ToString("yyyy-MM-dd HH:mm:ss") == fechaStr);
                        if (local != null)
                        {
                            p.LocalId = local.Id;
                            // PRIORIDAD ORACLE: Solo usar material local si Oracle no tiene dato
                            if ((string.IsNullOrEmpty(p.Material) || p.Material == "-") &&
                                !string.IsNullOrEmpty(local.DescripcionMaterial) && 
                                local.DescripcionMaterial != "-")
                            {
                                p.Material = local.DescripcionMaterial;
                            }
                            // PRIORIDAD ORACLE: Solo usar Guía local si Oracle no tiene dato
                            if (string.IsNullOrEmpty(p.Guia) && !string.IsNullOrEmpty(local.Guia))
                            {
                                p.Guia = local.Guia;
                            }
                            // PRIORIDAD ORACLE: Solo usar Tramos locales si Oracle tiene 0
                            if (p.Tramo1 == 0 && local.Tramo1 > 0) p.Tramo1 = local.Tramo1;
                            if (p.Tramo2 == 0 && local.Tramo2 > 0) p.Tramo2 = local.Tramo2;
                            if (p.Tramo3 == 0 && local.Tramo3 > 0) p.Tramo3 = local.Tramo3;
                            if (p.Tramo4 == 0 && local.Tramo4 > 0) p.Tramo4 = local.Tramo4;
                            if (p.Tramo5 == 0 && local.Tramo5 > 0) p.Tramo5 = local.Tramo5;
                            if (p.Tramo6 == 0 && local.Tramo6 > 0) p.Tramo6 = local.Tramo6;
                        }
                    }
                }

                // Crear registro local para registros de Oracle sin contraparte en SQLite
                foreach (var p in preparatorias.Where(p => !p.LocalId.HasValue).ToList())
                {
                    try
                    {
                        // Mapear estado de Oracle a estado local: "1" → EnProceso, "3" → Terminado, "9" → Anulado
                        var estadoLocal = p.Estado switch
                        {
                            "1" => EstadoOrden.EnProceso,
                            "3" => EstadoOrden.Terminado,
                            "9" => EstadoOrden.Anulado,
                            _   => EstadoOrden.EnProceso
                        };

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
                            Estado            = estadoLocal,
                            Cerrado           = p.Estado == "3", // Cerrado si está terminado
                            Guia              = p.Guia,
                            Tramo1            = p.Tramo1,
                            Tramo2            = p.Tramo2,
                            Tramo3            = p.Tramo3,
                            Tramo4            = p.Tramo4,
                            Tramo5            = p.Tramo5,
                            Tramo6            = p.Tramo6
                        };
                        _context.RegistrosAutoconer.Add(nuevoReg);
                        await _context.SaveChangesAsync();
                        p.LocalId = nuevoReg.Id;
                        _logger.LogInformation("Registro Autoconer local creado: Lote={Lote}, Fecha={Fecha}, Estado={Estado}, Id={Id}, Guia={Guia}, Tramos=T1:{T1},T2:{T2},T3:{T3},T4:{T4},T5:{T5},T6:{T6}",
                            p.Lote, p.FechaInicio, estadoLocal, nuevoReg.Id, p.Guia ?? "(null)", 
                            p.Tramo1, p.Tramo2, p.Tramo3, p.Tramo4, p.Tramo5, p.Tramo6);
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

            return View("Partida/Index", preparatorias);
        }

        // GET: /Autoconer/Partida/Crear
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Crear()
        {
            ViewBag.Titulos   = await _recetaService.ObtenerTitulosAutoconerAsync();
            ViewBag.Maquinas  = await _recetaService.ObtenerMaquinasPorTipoAsync("A");
            return View("Partida/Crear", new RegistroAutoconer());
        }

        // POST: /Autoconer/Crear
        [HttpPost]
        [Authorize]
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

                    model.Estado  = EstadoOrden.Terminado;
                    model.Cerrado = true;

                    if (model.Fecha == default || model.Fecha == DateTime.MinValue)
                        model.Fecha = DateTime.Now;

                    _logger.LogInformation("Creando registro Autoconer: Lote={Lote}, Maquina={Maquina}", model.Lote, model.NumeroAutoconer);

                    // Guardar en SQLite
                    _context.RegistrosAutoconer.Add(model);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Registro Autoconer guardado en SQLite con Id={Id}", model.Id);

                    // Insertar en Oracle con el método dedicado para Autoconer
                    var insertadoEnOracle = await _recetaService.InsertarPreparatoriaAutoconerAsync(model, User.Identity?.Name);

                    if (insertadoEnOracle)
                    {
                        // Ejecutar SP_CALCULAR_PROD_ESP_TEO tras inserción exitosa en Oracle
                        var spResult = await _recetaService.EjecutarSpCalcularProdTeoAsync(
                            model.CodigoReceta, model.Lote, "A", model.NumeroAutoconer,
                            model.Titulo, model.HoraInicio ?? model.Fecha);

                        if (!spResult.UpdateExitoso)
                        {
                            _logger.LogWarning("Autoconer {Id}: SP_CALCULAR_PROD_ESP_TEO falló tras INSERT.", model.Id);
                            TempData["Success"] = "Registro Autoconer creado exitosamente, pero no se pudo calcular PROD_TEORICO.";
                        }
                        else if (spResult.Codigo != "0")
                        {
                            _logger.LogWarning("Autoconer {Id}: SP_CALCULAR_PROD_ESP_TEO devolvió código {Codigo}: {Mensaje}", model.Id, spResult.Codigo, spResult.Mensaje);
                            TempData["Success"] = $"Registro Autoconer creado. Advertencia en cálculo: {spResult.Mensaje}";
                        }
                        else
                        {
                            _logger.LogInformation("Autoconer {Id}: SP_CALCULAR_PROD_ESP_TEO ejecutado correctamente tras INSERT.", model.Id);
                            TempData["Success"] = "Registro Autoconer creado exitosamente y registrado en Oracle.";
                        }

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
            return View("Partida/Crear", model);
        }

        // GET: /Autoconer/Partida/Editar (Oracle keys)
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Editar(string? receta, string lote, string codMaq, string titulo, string fechaIni)
        {
            _logger.LogInformation("╔═══════════════════════════════════════════════════════════════════════════════╗");
            _logger.LogInformation("║ GET Editar - Parámetros Recibidos                                            ║");
            _logger.LogInformation("╠═══════════════════════════════════════════════════════════════════════════════╣");
            _logger.LogInformation("║ receta   = [{Receta}]", receta ?? "(null)");
            _logger.LogInformation("║ lote     = [{Lote}]", lote);
            _logger.LogInformation("║ codMaq   = [{CodMaq}]", codMaq);
            _logger.LogInformation("║ titulo   = [{Titulo}]", titulo);
            _logger.LogInformation("║ fechaIni = [{FechaIni}]", fechaIni);
            _logger.LogInformation("╚═══════════════════════════════════════════════════════════════════════════════╝");

            if (string.IsNullOrEmpty(lote) || string.IsNullOrEmpty(codMaq) || string.IsNullOrEmpty(titulo) || string.IsNullOrEmpty(fechaIni))
            {
                TempData["Error"] = "Parámetros incompletos para editar el registro.";
                return RedirectToAction(nameof(Index));
            }

            if (!DateTime.TryParse(fechaIni, out DateTime fecha))
            {
                TempData["Error"] = "Formato de fecha inválido.";
                return RedirectToAction(nameof(Index));
            }

            var detalle = await _recetaService.ObtenerDetalleAutoconerAsync(receta, lote, codMaq, titulo, fecha);
            if (detalle == null)
            {
                TempData["Error"] = "No se encontró el registro en Oracle.";
                return RedirectToAction(nameof(Index));
            }

            // Fallback automático: Si Material vacío, buscar por GUIA o LOTE
            if (string.IsNullOrEmpty(detalle.DescripcionMaterial) || detalle.DescripcionMaterial == "-")
            {
                _logger.LogWarning("Material vacío, buscando automáticamente... Guia={Guia}, Lote={Lote}", 
                    detalle.Guia ?? "(null)", detalle.Lote);

                // Prioridad 1: Buscar por GUIA (Nº Partida)
                if (!string.IsNullOrEmpty(detalle.Guia))
                {
                    var partidaResults = await _recetaService.BuscarPartidaPorGuiaAsync(detalle.Guia);
                    if (partidaResults != null && partidaResults.Count > 0)
                    {
                        detalle.DescripcionMaterial = partidaResults[0].Material;
                        _logger.LogInformation("✓ Material obtenido por GUIA: {Material}", detalle.DescripcionMaterial);
                    }
                }

                // Prioridad 2: Buscar por LOTE (si GUIA falló o no existe)
                if ((string.IsNullOrEmpty(detalle.DescripcionMaterial) || detalle.DescripcionMaterial == "-") 
                    && !string.IsNullOrEmpty(detalle.Lote))
                {
                    var loteResults = await _recetaService.BuscarLotePorCodigoAsync(detalle.Lote);
                    if (loteResults != null && loteResults.Count > 0)
                    {
                        detalle.DescripcionMaterial = loteResults[0].Material;
                        _logger.LogInformation("✓ Material obtenido por LOTE: {Material}", detalle.DescripcionMaterial);
                    }
                }

                // Si aún está vacío
                if (string.IsNullOrEmpty(detalle.DescripcionMaterial) || detalle.DescripcionMaterial == "-")
                {
                    _logger.LogWarning("✗ No se pudo obtener Material para Lote={Lote}", detalle.Lote);
                    detalle.DescripcionMaterial = "-";
                }
            }

            // Convertir AutoconerDetalleOracleDto → RegistroAutoconer para edición
            var registro = new RegistroAutoconer
            {
                CodigoReceta         = detalle.CodigoReceta,
                Lote                 = detalle.Lote,
                DescripcionMaterial  = detalle.DescripcionMaterial,
                NumeroAutoconer      = detalle.NumeroAutoconer,
                Titulo               = detalle.Titulo,
                Fecha                = detalle.Fecha,
                Turno                = detalle.Turno,
                CodigoOperador       = detalle.CodigoOperador,
                VelocidadMMin        = detalle.VelocidadMMin,
                HoraInicio           = detalle.FechaInicio,
                HoraFinal            = detalle.FechaFinal,
                Cantidad             = detalle.Cantidad,
                PesoBruto            = detalle.PesoBruto ?? 0m, // KG_UNIDAD en Oracle
                Guia                 = detalle.Guia,
                Destino              = detalle.Destino,
                Proceso              = detalle.Proceso,
                Tramo1               = detalle.Tramo1,
                Tramo2               = detalle.Tramo2,
                Tramo3               = detalle.Tramo3,
                Tramo4               = detalle.Tramo4,
                Tramo5               = detalle.Tramo5,
                Tramo6               = detalle.Tramo6,
                Estado               = EstadoOrden.Terminado, // Estado="3" → Terminado
                Cerrado              = true
            };

            // Guardar valores antiguos para el POST
            TempData["OldReceta"]   = detalle.CodigoReceta;
            TempData["OldLote"]     = detalle.Lote;
            TempData["OldCodMaq"]   = detalle.NumeroAutoconer;
            TempData["OldTitulo"]   = detalle.Titulo;
            TempData["OldFechaIni"] = detalle.Fecha.ToString("yyyy-MM-dd HH:mm:ss");

            ViewBag.NombreOperario = detalle.NombreOperario ?? await _recetaService.ObtenerNombreEmpleadoAsync(registro.CodigoOperador);
            ViewBag.Titulos        = await _recetaService.ObtenerTitulosAutoconerAsync();
            ViewBag.Maquinas       = await _recetaService.ObtenerMaquinasPorTipoAsync("A");
            ViewBag.Destinos       = await _recetaService.ObtenerDestinosAutoconerAsync();

            return View("Partida/Editar", registro);
        }

        // POST: /Autoconer/Partida/Editar
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(RegistroAutoconer model)
        {
            if (ModelState.IsValid)
            {
                // Recuperar valores antiguos de TempData (guardados en GET)
                var oldReceta   = TempData["OldReceta"]?.ToString();
                var oldLote     = TempData["OldLote"]?.ToString();
                var oldCodMaq   = TempData["OldCodMaq"]?.ToString();
                var oldTitulo   = TempData["OldTitulo"]?.ToString();
                var oldFechaStr = TempData["OldFechaIni"]?.ToString();

                if (string.IsNullOrEmpty(oldLote) || string.IsNullOrEmpty(oldCodMaq) || 
                    string.IsNullOrEmpty(oldTitulo) || string.IsNullOrEmpty(oldFechaStr))
                {
                    TempData["Error"] = "Sesión expirada. Por favor, vuelva a cargar el formulario de edición.";
                    return RedirectToAction(nameof(Index));
                }

                if (!DateTime.TryParse(oldFechaStr, out DateTime oldFecha))
                {
                    TempData["Error"] = "Fecha antigua inválida.";
                    return RedirectToAction(nameof(Index));
                }

                // Crear registroAntiguo con los valores originales
                var registroAntiguo = new RegistroAutoconer
                {
                    CodigoReceta    = oldReceta,
                    Lote            = oldLote,
                    NumeroAutoconer = oldCodMaq,
                    Titulo          = oldTitulo,
                    Fecha           = oldFecha
                };

                // Asegurar estado correcto
                model.Estado  = EstadoOrden.Terminado;
                model.Cerrado = true;

                try
                {
                    // UPDATE en Oracle con método específico de Autoconer
                    var actualizadoEnOracle = await _recetaService.ActualizarPreparatoriaAutoconerAsync(
                        model, registroAntiguo, User.Identity?.Name);

                    if (!actualizadoEnOracle)
                    {
                        _logger.LogWarning("Falló actualización en Oracle para Autoconer: Lote={Lote}, CodMaq={CodMaq}", model.Lote, model.NumeroAutoconer);
                        TempData["Warning"] = "No se pudo actualizar el registro en Oracle. Verifique los logs.";
                        return RedirectToAction(nameof(Index));
                    }

                    // Ejecutar SP_CALCULAR_PROD_ESP_TEO tras actualización exitosa en Oracle
                    var spResult = await _recetaService.EjecutarSpCalcularProdTeoAsync(
                        model.CodigoReceta, model.Lote, "A", model.NumeroAutoconer,
                        model.Titulo, model.HoraInicio ?? model.Fecha);

                    if (!spResult.UpdateExitoso)
                    {
                        _logger.LogWarning("Autoconer: SP_CALCULAR_PROD_ESP_TEO falló tras UPDATE. Lote={Lote}", model.Lote);
                        TempData["Success"] = "Registro Autoconer actualizado, pero no se pudo calcular PROD_TEORICO.";
                    }
                    else if (spResult.Codigo != "0")
                    {
                        _logger.LogWarning("Autoconer: SP_CALCULAR_PROD_ESP_TEO devolvió código {Codigo}: {Mensaje}", spResult.Codigo, spResult.Mensaje);
                        TempData["Success"] = $"Registro Autoconer actualizado. Advertencia en cálculo: {spResult.Mensaje}";
                    }
                    else
                    {
                        _logger.LogInformation("Autoconer: SP_CALCULAR_PROD_ESP_TEO ejecutado correctamente tras UPDATE. Lote={Lote}", model.Lote);
                        TempData["Success"] = "Registro Autoconer actualizado exitosamente en Oracle.";
                    }

                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al actualizar Autoconer en Oracle: Lote={Lote}, CodMaq={CodMaq}", model.Lote, model.NumeroAutoconer);
                    TempData["Error"] = $"Error al actualizar el registro: {ex.Message}";
                }
            }
            else
            {
                _logger.LogWarning("ModelState inválido al editar Autoconer. Errores: {Errors}",
                    string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            }

            ViewBag.NombreOperario = await _recetaService.ObtenerNombreEmpleadoAsync(model.CodigoOperador);
            ViewBag.Titulos        = await _recetaService.ObtenerTitulosAutoconerAsync();
            ViewBag.Maquinas       = await _recetaService.ObtenerMaquinasPorTipoAsync("A");
            ViewBag.Destinos       = await _recetaService.ObtenerDestinosAutoconerAsync();
            return View("Partida/Editar", model);
        }

        // GET: /Autoconer/Partida/Detalle
        [HttpGet]
        public async Task<IActionResult> Detalle(string? receta, string lote, string codMaq, string titulo, string fechaIni)
        {
            // Logging de parámetros recibidos
            _logger.LogInformation("╔═══════════════════════════════════════════════════════════════════════════════╗");
            _logger.LogInformation("║ DETALLE AUTOCONER - Parámetros Recibidos                                     ║");
            _logger.LogInformation("╠═══════════════════════════════════════════════════════════════════════════════╣");
            _logger.LogInformation("║ receta:   [{Receta}]", receta ?? "(NULL)");
            _logger.LogInformation("║ lote:     [{Lote}]", lote ?? "(NULL)");
            _logger.LogInformation("║ codMaq:   [{CodMaq}]", codMaq ?? "(NULL)");
            _logger.LogInformation("║ titulo:   [{Titulo}]", titulo ?? "(NULL)");
            _logger.LogInformation("║ fechaIni: [{FechaIni}]", fechaIni ?? "(NULL)");
            _logger.LogInformation("╚═══════════════════════════════════════════════════════════════════════════════╝");

            // Validar parámetros requeridos
            if (string.IsNullOrEmpty(lote) || string.IsNullOrEmpty(codMaq) || 
                string.IsNullOrEmpty(titulo) || string.IsNullOrEmpty(fechaIni))
            {
                _logger.LogWarning("Parámetros incompletos. lote={Lote}, codMaq={CodMaq}, titulo={Titulo}, fechaIni={FechaIni}",
                    lote ?? "NULL", codMaq ?? "NULL", titulo ?? "NULL", fechaIni ?? "NULL");
                TempData["Error"] = "Parámetros incompletos para buscar el registro.";
                return RedirectToAction(nameof(Index));
            }

            // Parsear la fecha
            if (!DateTime.TryParse(fechaIni, out DateTime fecha))
            {
                _logger.LogWarning("Formato de fecha inválido: {FechaIni}", fechaIni);
                TempData["Error"] = "Formato de fecha inválido.";
                return RedirectToAction(nameof(Index));
            }

            _logger.LogInformation("Fecha parseada exitosamente: {Fecha}", fecha.ToString("yyyy-MM-dd HH:mm:ss"));

            // Consultar Oracle directamente usando los parámetros de la URL
            var detalleOracle = await _recetaService.ObtenerDetalleAutoconerAsync(
                receta,
                lote,
                codMaq,
                titulo,
                fecha);

            if (detalleOracle == null)
            {
                _logger.LogWarning("╔═══════════════════════════════════════════════════════════════════════════════╗");
                _logger.LogWarning("║ NO SE ENCONTRÓ REGISTRO EN ORACLE                                             ║");
                _logger.LogWarning("╠═══════════════════════════════════════════════════════════════════════════════╣");
                _logger.LogWarning("║ Receta:   [{Receta}]", receta ?? "(NULL)");
                _logger.LogWarning("║ Lote:     [{Lote}]", lote);
                _logger.LogWarning("║ CodMaq:   [{CodMaq}]", codMaq);
                _logger.LogWarning("║ Titulo:   [{Titulo}]", titulo);
                _logger.LogWarning("║ FechaIni: [{FechaIni}]", fecha.ToString("yyyy-MM-dd HH:mm:ss"));
                _logger.LogWarning("╚═══════════════════════════════════════════════════════════════════════════════╝");
                TempData["Error"] = "No se encontró el registro en Oracle. Los datos pueden estar desincronizados.";
                return RedirectToAction(nameof(Index));
            }

            // Las descripciones ya vienen desde Oracle en el DTO
            ViewBag.NombreOperario = detalleOracle.NombreOperario;
            ViewBag.DescripcionMaquina = detalleOracle.DescripcionMaquina;
            ViewBag.DescripcionTitulo = detalleOracle.DescripcionTitulo;
            ViewBag.DescripcionDestino = detalleOracle.DescripcionDestino;

            return View("Partida/Detalle", detalleOracle);
        }

        // POST: /Autoconer/Anular/5
        [HttpPost]
        [Authorize]
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
                        data    = new { numero = resultados[0].Numero, lote = resultados[0].Lote, material = resultados[0].Material, proceso = resultados[0].Proceso }
                    });

                return Json(new
                {
                    success  = true,
                    multiple = true,
                    items    = resultados.Select(r => new { numero = r.Numero, lote = r.Lote, material = r.Material, proceso = r.Proceso })
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
                        data    = new { lote = resultados[0].Lote, receta = resultados[0].Receta, material = resultados[0].Material, proceso = resultados[0].Proceso }
                    });

                return Json(new
                {
                    success  = true,
                    multiple = true,
                    items    = resultados.Select(r => new { lote = r.Lote, receta = r.Receta, material = r.Material, proceso = r.Proceso })
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> BuscarPartida(string guia)
        {
            if (string.IsNullOrWhiteSpace(guia))
                return Json(new { success = false, message = "Guía requerida" });

            try
            {
                var resultados = await _recetaService.BuscarPartidaPorGuiaAsync(guia);
                if (resultados.Count == 0)
                    return Json(new { success = false, message = "No se encontró la partida" });

                if (resultados.Count == 1)
                    return Json(new
                    {
                        success = true,
                        data    = new { partida = resultados[0].Partida, guia = resultados[0].Guia, lote = resultados[0].Lote, material = resultados[0].Material, titulo = resultados[0].Titulo, proceso = resultados[0].Proceso }
                    });

                return Json(new
                {
                    success  = true,
                    multiple = true,
                    items    = resultados.Select(r => new { partida = r.Partida, guia = r.Guia, lote = r.Lote, material = r.Material, titulo = r.Titulo, cliente = r.DescCliente, proceso = r.Proceso })
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
                var titulos = await _recetaService.ObtenerTitulosAutoconerAsync();
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

        [HttpGet]
        public async Task<IActionResult> ObtenerDestinosAutoconer()
        {
            try
            {
                var destinos = await _recetaService.ObtenerDestinosAutoconerAsync();
                return Json(new
                {
                    success  = true,
                    destinos = destinos.Select(d => new { codigo = d.Codigo, descripcion = d.Descripcion })
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // ===================== Helpers =====================

        private async Task CargarViewBag()
        {
            ViewBag.Titulos  = await _recetaService.ObtenerTitulosAutoconerAsync();
            ViewBag.Maquinas = await _recetaService.ObtenerMaquinasPorTipoAsync("A");
        }

        // ===================== POR CANILLAS =====================

        // GET: /Autoconer/PorCanillas
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> PorCanillas()
        {
            ViewBag.Titulos   = await _recetaService.ObtenerTitulosAutoconerAsync();
            ViewBag.Maquinas  = await _recetaService.ObtenerMaquinasPorTipoAsync("A");
            ViewBag.Destinos  = await _recetaService.ObtenerDestinosAutoconerAsync();
            return View("Canillas/Index");
        }

        // POST: /Autoconer/GuardarCanillas
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarCanillas([FromBody] List<RegistroAutoconer> registros)
        {
            if (registros == null || registros.Count == 0)
            {
                return Json(new { success = false, message = "No se recibieron registros para guardar." });
            }

            var errores = new List<string>();
            var guardados = 0;

            try
            {
                foreach (var registro in registros)
                {
                    try
                    {
                        // Validar que la máquina no tenga una preparatoria en proceso
                        if (!string.IsNullOrEmpty(registro.NumeroAutoconer))
                        {
                            var enProceso = await _recetaService.TieneMaquinaEnProcesoAsync("A", registro.NumeroAutoconer);
                            if (enProceso)
                            {
                                errores.Add($"La máquina {registro.NumeroAutoconer} ya tiene un registro en proceso.");
                                continue;
                            }
                        }

                        registro.Estado  = EstadoOrden.Terminado;
                        registro.Cerrado = true;

                        if (registro.Fecha == default || registro.Fecha == DateTime.MinValue)
                            registro.Fecha = DateTime.Now;

                        // Guardar en SQLite
                        _context.RegistrosAutoconer.Add(registro);
                        await _context.SaveChangesAsync();

                        // Insertar en Oracle
                        var insertadoEnOracle = await _recetaService.InsertarPreparatoriaAutoconerAsync(registro, User.Identity?.Name);

                        if (insertadoEnOracle)
                        {
                            // Ejecutar SP_CALCULAR_PROD_ESP_TEO
                            var spResult = await _recetaService.EjecutarSpCalcularProdTeoAsync(
                                registro.CodigoReceta, registro.Lote, "A", registro.NumeroAutoconer,
                                registro.Titulo, registro.HoraInicio ?? registro.Fecha);

                            if (!spResult.UpdateExitoso || spResult.Codigo != "0")
                            {
                                _logger.LogWarning("Autoconer {Id}: SP_CALCULAR_PROD_ESP_TEO falló o devolvió código {Codigo}", 
                                    registro.Id, spResult.Codigo);
                            }

                            guardados++;
                        }
                        else
                        {
                            errores.Add($"Registro para máquina {registro.NumeroAutoconer} no se pudo guardar en Oracle.");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al guardar registro Autoconer para máquina {Maquina}", registro.NumeroAutoconer);
                        errores.Add($"Error en máquina {registro.NumeroAutoconer}: {ex.Message}");
                    }
                }

                var mensaje = $"Se guardaron {guardados} de {registros.Count} registros.";
                if (errores.Count > 0)
                {
                    mensaje += $" Errores: {string.Join(", ", errores)}";
                }

                return Json(new { 
                    success = guardados > 0, 
                    message = mensaje,
                    guardados = guardados,
                    total = registros.Count,
                    errores = errores
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general al guardar registros de canillas");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }
    }
}
