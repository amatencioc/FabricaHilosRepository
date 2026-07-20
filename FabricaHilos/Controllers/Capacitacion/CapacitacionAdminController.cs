using Dapper;
using FabricaHilos.Models.Capacitacion;
using FabricaHilos.Services;
using FabricaHilos.Services.Capacitacion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Oracle.ManagedDataAccess.Client;
using System.Text.Json;

namespace FabricaHilos.Controllers.Capacitacion;

[Authorize]
[Route("RecursosHumanos/CapacitacionAdmin/[action]")]
public class CapacitacionAdminController : OracleBaseController
{
    private readonly ICapacitacionService _capSvc;
    private readonly IExamenService       _examSvc;
    private readonly ContenidoMediaService _media;
    private readonly IMenuService         _menuService;

    public CapacitacionAdminController(
        ICapacitacionService capSvc,
        IExamenService       examSvc,
        ContenidoMediaService media,
        IMenuService         menuService)
    {
        _capSvc      = capSvc;
        _examSvc     = examSvc;
        _media       = media;
        _menuService = menuService;
    }

    private string UsuarioActual => HttpContext.Session.GetString("OracleUser") ?? "";

    // ── Verificación de administrador ────────────────────────────────────────
    // Usa el token ACCESO_WEB = 'CapacitacionAdmin' resuelto por MenuService,
    // evitando la consulta extra a CAP_ADMIN en cada request.
    public override async Task OnActionExecutionAsync(
        ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // 1) Verificación de sesión Oracle del base (síncrona)
        base.OnActionExecuting(context);
        if (context.Result != null)
            return;

        // 2) Verificar permiso via ACCESO_WEB (sin consulta adicional a BD)
        var menus = _menuService.GetMenusActuales();
        if (!menus.CapacitacionAdmin)
        {
            TempData["Error"] = "No tiene permisos para acceder al área de administración de Capacitación.";
            context.Result = RedirectToAction("MiPanel", "Capacitacion",
                new { area = "" });
            return;
        }

        await next();
    }


    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var categorias = await _capSvc.GetCategoriasAsync();
        var cursos     = await _capSvc.GetCatalogoAsync(UsuarioActual, paraAdmin: true);
        ViewBag.Categorias = categorias;
        return View("~/Views/RecursosHumanos/Capacitacion/Admin/Index.cshtml", cursos);
    }

    // GET /RecursosHumanos/CapacitacionAdmin/CursoForm?id=5  (0=nuevo)
    [HttpGet]
    public async Task<IActionResult> CursoForm(int id = 0)
    {
        var categorias = await _capSvc.GetCategoriasAsync();
        ViewBag.Categorias = categorias;
        ViewBag.Areas = await _capSvc.GetAreasAsync();
        ViewBag.CentrosCosto = await _capSvc.GetCentrosCostoAsync();

        CapCurso curso = new();
        if (id > 0)
        {
            var existente = await _capSvc.GetCursoDetalleAsync(id, UsuarioActual, paraAdmin: true);
            if (existente != null) curso = existente;

            ViewBag.AreasCurso    = await _capSvc.GetCursoAreasAsync(id);
            ViewBag.UsuariosCurso = await _capSvc.GetCursoUsuariosAsync(id);
            ViewBag.CcostoCurso   = await _capSvc.GetCursoCcostoAsync(id);

            // Cargar contenidos existentes
            await using var db = new OracleConnection(Configuration.GetConnectionString(
                HttpContext.Session.GetString("EmpresaConexion") ?? "LaColonialConnection") ?? "");

            var contenidos = (await db.QueryAsync<CapContenido>(
                $@"SELECT ID_CONTENIDO, ID_CURSO, TITULO, TIPO, ORDEN,
                          RUTA_ARCHIVO, NOMBRE_ARCH_ORI, DURACION_SEG, OBLIGATORIO, ACTIVO
                   FROM {S()}CAP_CONTENIDO
                   WHERE ID_CURSO = :id AND ACTIVO = 'S'
                   ORDER BY ORDEN, ID_CONTENIDO",
                new { id })).ToList();
            ViewBag.Contenidos = contenidos;

            // Cargar examen del curso si existe
            var examen = await db.QueryFirstOrDefaultAsync<CapExamen>(
                $"SELECT * FROM {S()}CAP_EXAMEN WHERE ID_CURSO = :id AND ACTIVO = 'S'",
                new { id });
            ViewBag.Examen = examen;

            if (examen != null)
            {
                var totalPreguntas = await db.ExecuteScalarAsync<int>(
                    $"SELECT COUNT(*) FROM {S()}CAP_PREGUNTA WHERE ID_EXAMEN = :idEx",
                    new { idEx = examen.IdExamen });
                ViewBag.TotalPreguntas = totalPreguntas;
            }
        }
        else
        {
            ViewBag.AreasCurso    = new List<CapCursoArea>();
            ViewBag.UsuariosCurso = new List<CapCursoUsuario>();
            ViewBag.CcostoCurso   = new List<CapCursoCcosto>();
        }

        return View("~/Views/RecursosHumanos/Capacitacion/Admin/CursoForm.cshtml", curso);
    }

    // GET /RecursosHumanos/CapacitacionAdmin/BuscarEmpleados?term=juan
    [HttpGet]
    public async Task<IActionResult> BuscarEmpleados(string term)
    {
        if (string.IsNullOrWhiteSpace(term) || term.Trim().Length < 2)
            return Json(new { results = Array.Empty<object>() });

        var empleados = await _capSvc.BuscarEmpleadosAsync(term.Trim());
        return Json(new
        {
            // id = C_CODIGO (V_PERSONAL, existe para TODO el personal activo, tenga o no
            // cuenta CS_USER). Si no tiene cuenta LMS todav\u00eda, se marca "(sin acceso LMS)"
            // para que el admin sepa que no podr\u00e1 verlo hasta que Sistemas le cree usuario.
            results = empleados.Select(e => new
            {
                id   = e.CCodigo,
                text = $"{e.Nombre}{(string.IsNullOrEmpty(e.DescArea) ? "" : " · " + e.DescArea)}"
                       + (string.IsNullOrEmpty(e.CodUsuario) ? " (sin acceso LMS)" : $" ({e.CodUsuario})")
            })
        });
    }

    // POST /RecursosHumanos/CapacitacionAdmin/GuardarCurso
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarCurso(
        int idCurso, string titulo, int idCategoria, string nivel,
        int? duracionMin, string? descripcion, string? objetivo,
        decimal notaAprobacion, int maxIntentos, int? certValidezDias,
        string estado, bool obligatorio, bool tieneExamen, bool tieneCertificado,
        string visibilidad = "PUB", string alcance = "TODOS",
        string? areasJson = null, string? usuariosJson = null, string? ccostosJson = null)
    {
        await using var db = new OracleConnection(Configuration.GetConnectionString(
            HttpContext.Session.GetString("EmpresaConexion") ?? "LaColonialConnection") ?? "");

        // Un curso público siempre tiene alcance TODOS (coherencia visibilidad/alcance)
        if (visibilidad != "PRI") alcance = "TODOS";

        int idFinal;
        if (idCurso == 0)
        {
            // INSERT - obtener próximo ID de la secuencia
            var newId = await db.ExecuteScalarAsync<int>(
                $"SELECT {S()}CAP_SEQ_CURSO.NEXTVAL FROM DUAL");

            await db.ExecuteAsync(
                $@"INSERT INTO {S()}CAP_CURSO
                   (ID_CURSO, ID_CATEGORIA, TITULO, DESCRIPCION, OBJETIVO,
                    DURACION_MIN, NIVEL, OBLIGATORIO, NOTA_APROBACION, MAX_INTENTOS,
                    TIENE_EXAMEN, TIENE_CERTIFICADO, TIENE_TAREAS, CERT_VALIDEZ_DIAS,
                    ESTADO, VISIBILIDAD, ALCANCE, USR_CREADOR, FCH_CREACION)
                   VALUES (
                    :id, :cat, :tit, :desc, :obj, :dur, :niv, :oblig, :nota, :intentos,
                    :examen, :cert, 'N', :certDias, :estado, :vis, :alc, :usr, SYSDATE)",
                new
                {
                    id = newId,
                    cat = idCategoria, tit = titulo,
                    desc = (object?)descripcion ?? DBNull.Value,
                    obj = (object?)objetivo ?? DBNull.Value,
                    dur = (object?)duracionMin ?? DBNull.Value,
                    niv = nivel, oblig = obligatorio ? "S" : "N",
                    nota = notaAprobacion, intentos = maxIntentos,
                    examen = tieneExamen ? "S" : "N",
                    cert = tieneCertificado ? "S" : "N",
                    certDias = (object?)certValidezDias ?? DBNull.Value,
                    estado, vis = visibilidad, alc = alcance, usr = UsuarioActual
                });

            idFinal = newId;
        }
        else
        {
            // UPDATE
            await db.ExecuteAsync(
                $@"UPDATE {S()}CAP_CURSO SET
                    ID_CATEGORIA     = :cat,
                    TITULO           = :tit,
                    DESCRIPCION      = :desc,
                    OBJETIVO         = :obj,
                    DURACION_MIN     = :dur,
                    NIVEL            = :niv,
                    OBLIGATORIO      = :oblig,
                    NOTA_APROBACION  = :nota,
                    MAX_INTENTOS     = :intentos,
                    TIENE_EXAMEN     = :examen,
                    TIENE_CERTIFICADO= :cert,
                    CERT_VALIDEZ_DIAS= :certDias,
                    ESTADO           = :estado,
                    VISIBILIDAD      = :vis,
                    ALCANCE          = :alc,
                    FCH_MODIF        = SYSDATE
                   WHERE ID_CURSO = :id",
                new
                {
                    cat = idCategoria, tit = titulo,
                    desc = (object?)descripcion ?? DBNull.Value,
                    obj = (object?)objetivo ?? DBNull.Value,
                    dur = (object?)duracionMin ?? DBNull.Value,
                    niv = nivel, oblig = obligatorio ? "S" : "N",
                    nota = notaAprobacion, intentos = maxIntentos,
                    examen = tieneExamen ? "S" : "N",
                    cert = tieneCertificado ? "S" : "N",
                    certDias = (object?)certValidezDias ?? DBNull.Value,
                    estado, vis = visibilidad, alc = alcance, id = idCurso
                });

            idFinal = idCurso;
        }

        // Sincronizar áreas / centros de costo / usuarios asignados
        // (CAP_CURSO_AREA / CAP_CURSO_CCOSTO / CAP_CURSO_USUARIO — ver 12_CAP_JERARQUIA_CCOSTO.sql)
        var areas       = string.IsNullOrWhiteSpace(areasJson)    ? new List<string>() : (JsonSerializer.Deserialize<List<string>>(areasJson)    ?? new List<string>());
        var centrosCosto = string.IsNullOrWhiteSpace(ccostosJson) ? new List<string>() : (JsonSerializer.Deserialize<List<string>>(ccostosJson) ?? new List<string>());
        var usuarios    = string.IsNullOrWhiteSpace(usuariosJson) ? new List<string>() : (JsonSerializer.Deserialize<List<string>>(usuariosJson) ?? new List<string>());
        await _capSvc.SetAlcanceCursoAsync(idFinal, visibilidad, alcance, areas, centrosCosto, usuarios);

        return Json(new { ok = true, msg = idCurso == 0 ? "Curso creado." : "Curso actualizado.", idCurso = idFinal });
    }

    // POST /RecursosHumanos/CapacitacionAdmin/GuardarContenido
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarContenido(int idCurso, string titulo, string tipo,
        int orden, IFormFile? archivo, string? urlExterna, string? contenidoHtml,
        int duracionSeg = 0, string obligatorio = "S", int? idSeccion = null)
    {
        string rutaArchivo  = "";
        string mimeType     = "";
        string nombreOri    = "";
        string claveMedia   = "";
        long   tamanio      = 0;

        if (archivo != null && archivo.Length > 0 && tipo is "VID" or "PDF" or "ARC")
        {
            var (ok, msg, clave, ruta, mime, bytes) = await _media.GuardarArchivoAsync(archivo, idCurso);
            if (!ok) return Json(new { ok = false, msg });
            claveMedia  = clave;
            rutaArchivo = ruta;
            mimeType    = mime;
            nombreOri   = Path.GetFileName(archivo.FileName);
            tamanio     = bytes;   // tamaño real en disco (imagen puede haber sido comprimida)
        }

        await using var db = new OracleConnection(Configuration.GetConnectionString(
            HttpContext.Session.GetString("EmpresaConexion") ?? "LaColonialConnection") ?? "");

        await db.ExecuteAsync(
            $@"INSERT INTO {S()}CAP_CONTENIDO
               (ID_CONTENIDO, ID_CURSO, TITULO, TIPO, ORDEN, CLAVE_MEDIA, RUTA_ARCHIVO, NOMBRE_ARCH_ORI,
                TAMANIO_BYTES, MIME_TYPE, URL_EXTERNA, CONTENIDO_HTML, DURACION_SEG,
                OBLIGATORIO, ID_SECCION, ACTIVO)
               VALUES ({S()}CAP_SEQ_CONTENIDO.NEXTVAL, :cur, :tit, :tipo, :ord, :clave, :ruta, :nom,
                       :tam, :mime, :url, :html, :dur, :oblig, :sec, 'S')",
            new {
                cur   = idCurso, tit = titulo, tipo, ord = orden,
                clave = (object?)claveMedia.NullIfEmpty()  ?? DBNull.Value,
                ruta  = (object?)rutaArchivo.NullIfEmpty() ?? DBNull.Value,
                nom   = (object?)nombreOri.NullIfEmpty()   ?? DBNull.Value,
                tam   = tamanio > 0 ? (object)tamanio : DBNull.Value,
                mime  = (object?)mimeType.NullIfEmpty()    ?? DBNull.Value,
                url   = (object?)urlExterna                ?? DBNull.Value,
                html  = (object?)contenidoHtml             ?? DBNull.Value,
                dur   = duracionSeg > 0 ? (object)duracionSeg : DBNull.Value,
                oblig = obligatorio,
                sec   = (object?)idSeccion ?? DBNull.Value
            });

        return Json(new { ok = true, msg = "Contenido guardado." });
    }

    // POST /RecursosHumanos/CapacitacionAdmin/EliminarContenido
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarContenido(long idContenido)
    {
        await using var db = new OracleConnection(Configuration.GetConnectionString(
            HttpContext.Session.GetString("EmpresaConexion") ?? "LaColonialConnection") ?? "");

        await db.ExecuteAsync(
            $"UPDATE {S()}CAP_CONTENIDO SET ACTIVO = 'N' WHERE ID_CONTENIDO = :id",
            new { id = idContenido });

        return Json(new { ok = true });
    }

    // GET /RecursosHumanos/CapacitacionAdmin/ObtenerContenido?idContenido=5
    [HttpGet]
    public async Task<IActionResult> ObtenerContenido(long idContenido)
    {
        await using var db = new OracleConnection(Configuration.GetConnectionString(
            HttpContext.Session.GetString("EmpresaConexion") ?? "LaColonialConnection") ?? "");

        var cont = await db.QueryFirstOrDefaultAsync<dynamic>(
            $@"SELECT ID_CONTENIDO, ID_CURSO, TITULO, TIPO, ORDEN,
                      URL_EXTERNA, CONTENIDO_HTML, DURACION_SEG, OBLIGATORIO
               FROM {S()}CAP_CONTENIDO
               WHERE ID_CONTENIDO = :id AND ACTIVO = 'S'",
            new { id = idContenido });

        if (cont == null)
            return Json(new { ok = false, msg = "Contenido no encontrado." });

        return Json(new {
            ok = true,
            idContenido = Convert.ToInt64(cont.ID_CONTENIDO),
            idCurso = Convert.ToInt32(cont.ID_CURSO),
            titulo = (string)cont.TITULO,
            tipo = (string)cont.TIPO,
            orden = cont.ORDEN is DBNull ? 1 : Convert.ToInt32(cont.ORDEN),
            urlExterna = cont.URL_EXTERNA is DBNull ? "" : (string)cont.URL_EXTERNA,
            contenidoHtml = cont.CONTENIDO_HTML is DBNull ? "" : (string)cont.CONTENIDO_HTML,
            duracionSeg = cont.DURACION_SEG is DBNull ? 0 : Convert.ToInt32(cont.DURACION_SEG),
            obligatorio = cont.OBLIGATORIO is DBNull ? "S" : (string)cont.OBLIGATORIO
        });
    }

    // POST /RecursosHumanos/CapacitacionAdmin/ActualizarContenido
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActualizarContenido(long idContenido, string titulo, string tipo,
        int orden, IFormFile? archivo, string? urlExterna, string? contenidoHtml,
        int duracionSeg = 0, string obligatorio = "S")
    {
        await using var db = new OracleConnection(Configuration.GetConnectionString(
            HttpContext.Session.GetString("EmpresaConexion") ?? "LaColonialConnection") ?? "");

        // Si se sube un nuevo archivo, guardarlo
        string? rutaArchivo = null;
        string? mimeType    = null;
        string? nombreOri   = null;
        string? claveMedia  = null;
        long    tamanio     = 0;

        if (archivo != null && archivo.Length > 0 && tipo is "VID" or "PDF" or "ARC")
        {
            // Obtener idCurso del contenido existente
            var idCurso = await db.ExecuteScalarAsync<int>(
                $"SELECT ID_CURSO FROM {S()}CAP_CONTENIDO WHERE ID_CONTENIDO = :id",
                new { id = idContenido });

            var (ok, msg, clave, ruta, mime, bytes) = await _media.GuardarArchivoAsync(archivo, idCurso);
            if (!ok) return Json(new { ok = false, msg });
            claveMedia  = clave;
            rutaArchivo = ruta;
            mimeType    = mime;
            nombreOri   = Path.GetFileName(archivo.FileName);
            tamanio     = bytes;   // tamaño real en disco
        }

        // Construir UPDATE dinámico
        var setClauses = new List<string>
        {
            "TITULO = :tit",
            "TIPO = :tipo",
            "ORDEN = :ord",
            "URL_EXTERNA = :url",
            "CONTENIDO_HTML = :html",
            "DURACION_SEG = :dur",
            "OBLIGATORIO = :oblig"
        };

        var parameters = new DynamicParameters();
        parameters.Add("tit", titulo);
        parameters.Add("tipo", tipo);
        parameters.Add("ord", orden);
        parameters.Add("url", (object?)urlExterna ?? DBNull.Value);
        parameters.Add("html", (object?)contenidoHtml ?? DBNull.Value);
        parameters.Add("dur", duracionSeg > 0 ? duracionSeg : DBNull.Value);
        parameters.Add("oblig", obligatorio);
        parameters.Add("id", idContenido);

        if (rutaArchivo != null)
        {
            setClauses.Add("CLAVE_MEDIA = :clave");
            setClauses.Add("RUTA_ARCHIVO = :ruta");
            setClauses.Add("NOMBRE_ARCH_ORI = :nom");
            setClauses.Add("TAMANIO_BYTES = :tam");
            setClauses.Add("MIME_TYPE = :mime");
            parameters.Add("clave", claveMedia);
            parameters.Add("ruta", rutaArchivo);
            parameters.Add("nom", nombreOri);
            parameters.Add("tam", tamanio);
            parameters.Add("mime", mimeType);
        }

        await db.ExecuteAsync(
            $"UPDATE {S()}CAP_CONTENIDO SET {string.Join(", ", setClauses)} WHERE ID_CONTENIDO = :id",
            parameters);

        return Json(new { ok = true, msg = "Contenido actualizado." });
    }

    // POST /RecursosHumanos/CapacitacionAdmin/CrearExamen
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearExamen(int idCurso, string titulo, int tiempoMin,
        string mezclarPreg = "S", string mezclarOpc = "S", string modoPreguntas = "F",
        int? nroPregAleatorias = null, string? instrucciones = null)
    {
        await using var db = new OracleConnection(Configuration.GetConnectionString(
            HttpContext.Session.GetString("EmpresaConexion") ?? "LaColonialConnection") ?? "");

        // Verificar que no exista ya
        var existe = await db.ExecuteScalarAsync<int>(
            $"SELECT COUNT(*) FROM {S()}CAP_EXAMEN WHERE ID_CURSO = :cur AND ACTIVO = 'S'",
            new { cur = idCurso });
        if (existe > 0)
            return Json(new { ok = false, msg = "Ya existe un examen activo para este curso." });

        var newId = await db.ExecuteScalarAsync<int>(
            $"SELECT {S()}CAP_SEQ_EXAMEN.NEXTVAL FROM DUAL");

        await db.ExecuteAsync(
            $@"INSERT INTO {S()}CAP_EXAMEN
               (ID_EXAMEN, ID_CURSO, TITULO, INSTRUCCIONES, TIEMPO_MIN,
                MEZCLAR_PREG, MEZCLAR_OPC, MOSTRAR_RESULT, MODO_PREGUNTAS,
                NRO_PREG_ALEATORIAS, TIPO_EXAMEN, ACTIVO)
               VALUES (:id, :cur, :tit, :inst, :tiempo, :mPreg, :mOpc, 'S', :modo, :nroAl, 'F', 'S')",
            new {
                id = newId, cur = idCurso, tit = titulo, tiempo = tiempoMin,
                inst = (object?)instrucciones ?? DBNull.Value,
                mPreg = mezclarPreg, mOpc = mezclarOpc, modo = modoPreguntas,
                nroAl = (object?)nroPregAleatorias ?? DBNull.Value
            });

        return Json(new { ok = true, msg = "Examen creado.", idExamen = newId });
    }

    // GET /RecursosHumanos/CapacitacionAdmin/ListarPreguntas?idExamen=5
    [HttpGet]
    public async Task<IActionResult> ListarPreguntas(int idExamen)
    {
        await using var db = new OracleConnection(Configuration.GetConnectionString(
            HttpContext.Session.GetString("EmpresaConexion") ?? "LaColonialConnection") ?? "");

        var preguntas = (await db.QueryAsync<dynamic>(
            $@"SELECT p.ID_PREGUNTA, p.ENUNCIADO, p.TIPO_PREG, p.PUNTAJE, p.ORDEN
               FROM {S()}CAP_PREGUNTA p
               WHERE p.ID_EXAMEN = :idEx AND p.ACTIVO = 'S'
               ORDER BY p.ORDEN, p.ID_PREGUNTA",
            new { idEx = idExamen })).ToList();

        var result = new List<object>();
        foreach (var p in preguntas)
        {
            var opciones = (await db.QueryAsync<dynamic>(
                $@"SELECT ID_OPCION, TEXTO, ES_CORRECTA, ORDEN
                   FROM {S()}CAP_OPCION
                   WHERE ID_PREGUNTA = :idP
                   ORDER BY ORDEN, ID_OPCION",
                new { idP = (long)Convert.ToInt64(p.ID_PREGUNTA) })).ToList();

            result.Add(new {
                idPregunta = Convert.ToInt64(p.ID_PREGUNTA),
                enunciado = (string)p.ENUNCIADO,
                tipoPreg = (string)p.TIPO_PREG,
                puntaje = Convert.ToDecimal(p.PUNTAJE),
                orden = p.ORDEN is DBNull ? (int?)null : Convert.ToInt32(p.ORDEN),
                opciones = opciones.Select(o => new {
                    idOpcion = Convert.ToInt64(o.ID_OPCION),
                    texto = (string)o.TEXTO,
                    esCorrecta = (string)o.ES_CORRECTA,
                    orden = o.ORDEN is DBNull ? (int?)null : Convert.ToInt32(o.ORDEN)
                })
            });
        }

        return Json(result);
    }

    // POST /RecursosHumanos/CapacitacionAdmin/GuardarPregunta
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarPregunta(int idExamen, string enunciado,
        string tipoPreg, decimal puntaje, int? orden, string opcionesJson)
    {
        await using var db = new OracleConnection(Configuration.GetConnectionString(
            HttpContext.Session.GetString("EmpresaConexion") ?? "LaColonialConnection") ?? "");

        await db.OpenAsync();
        await using var trx = await db.BeginTransactionAsync();
        long idPreg;
        try
        {
            idPreg = await db.ExecuteScalarAsync<long>(
                $"SELECT {S()}CAP_SEQ_PREGUNTA.NEXTVAL FROM DUAL",
                transaction: (System.Data.IDbTransaction)trx);

            await db.ExecuteAsync(
                $@"INSERT INTO {S()}CAP_PREGUNTA
                   (ID_PREGUNTA, ID_EXAMEN, ENUNCIADO, TIPO_PREG, PUNTAJE, ORDEN, ACTIVO)
                   VALUES (:id, :ex, :enun, :tipo, :pts, :ord, 'S')",
                new {
                    id = idPreg, ex = idExamen, enun = enunciado, tipo = tipoPreg,
                    pts = puntaje, ord = (object?)orden ?? DBNull.Value
                },
                transaction: (System.Data.IDbTransaction)trx);

            // Guardar opciones si las hay
            if (!string.IsNullOrEmpty(opcionesJson) && tipoPreg is "OM" or "OV" or "VF")
            {
                var opciones = System.Text.Json.JsonSerializer.Deserialize<List<OpcionDto>>(opcionesJson);
                if (opciones != null)
                {
                    foreach (var opc in opciones)
                    {
                        await db.ExecuteAsync(
                            $@"INSERT INTO {S()}CAP_OPCION
                               (ID_OPCION, ID_PREGUNTA, TEXTO, ES_CORRECTA, ORDEN)
                               VALUES ({S()}CAP_SEQ_OPCION.NEXTVAL, :idP, :txt, :corr, :ord)",
                            new {
                                idP = idPreg, txt = opc.Texto,
                                corr = opc.EsCorrecta ? "S" : "N",
                                ord = (object?)opc.Orden ?? DBNull.Value
                            },
                            transaction: (System.Data.IDbTransaction)trx);
                    }
                }
            }

            await trx.CommitAsync();
        }
        catch (Exception ex)
        {
            await trx.RollbackAsync();
            Logger.LogError(ex, "Error en GuardarPregunta idExamen={IdExamen}", idExamen);
            return Json(new { ok = false, msg = "Error al guardar la pregunta." });
        }

        return Json(new { ok = true, msg = "Pregunta guardada.", idPregunta = idPreg });
    }

    // POST /RecursosHumanos/CapacitacionAdmin/EliminarPregunta
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarPregunta(long idPregunta)
    {
        await using var db = new OracleConnection(Configuration.GetConnectionString(
            HttpContext.Session.GetString("EmpresaConexion") ?? "LaColonialConnection") ?? "");

        await db.ExecuteAsync(
            $"UPDATE {S()}CAP_PREGUNTA SET ACTIVO = 'N' WHERE ID_PREGUNTA = :id",
            new { id = idPregunta });

        return Json(new { ok = true });
    }

    // GET /RecursosHumanos/CapacitacionAdmin/Reportes
    [HttpGet]
    public async Task<IActionResult> Reportes(int? idCurso, int? idCategoria, string? area, string? supervisor, string? centroCosto)
    {
        var cursos = await _capSvc.GetCatalogoAsync(UsuarioActual, paraAdmin: true);
        ViewBag.Cursos = cursos;
        ViewBag.IdCursoActivo = idCurso;

        ViewBag.Categorias        = await _capSvc.GetCategoriasAsync();
        ViewBag.Areas             = await _capSvc.GetAreasAsync();
        // Si ya hay un área elegida, el combo de Centro de Costo se acota a esa área (cascada);
        // si no, se listan los 95 centros agrupados por área (ver 12_CAP_JERARQUIA_CCOSTO.sql)
        ViewBag.CentrosCosto      = await _capSvc.GetCentrosCostoAsync(area);
        ViewBag.Supervisores      = await _capSvc.GetSupervisoresAsync();
        ViewBag.FiltroCategoria   = idCategoria;
        ViewBag.FiltroArea        = area;
        ViewBag.FiltroSupervisor  = supervisor;
        ViewBag.FiltroCentroCosto = centroCosto;

        List<CapInscripcion> inscritos;
        if (idCurso.HasValue)
        {
            inscritos = await _capSvc.GetInscripcionesAsync(idCurso.Value, area, supervisor, centroCosto);
        }
        else
        {
            inscritos = await _capSvc.GetTodasInscripcionesAsync(idCategoria, area, supervisor, centroCosto);
        }

        return View("~/Views/RecursosHumanos/Capacitacion/Admin/Reportes.cshtml", inscritos);
    }

    // GET /RecursosHumanos/CapacitacionAdmin/DashboardJefaturas
    [HttpGet]
    public async Task<IActionResult> DashboardJefaturas()
    {
        var detalle = await _capSvc.GetHeadcountJefaturasAsync();
        return View("~/Views/RecursosHumanos/Capacitacion/Admin/DashboardJefaturas.cshtml", detalle);
    }

    // ── Helper ──────────────────────────────────────────────────────────────
    // Necesario porque este controller no hereda directamente de OracleServiceBase
    private string S()
    {
        return HttpContext.Session.GetString("EmpresaConexion") switch
        {
            "ArbonaConnection" => "ARBONA.",
            "SolsaConnection"  => "SOLSA.",
            _                  => "SIG."
        };
    }
}

internal static class StringExt
{
    public static string? NullIfEmpty(this string? s) =>
        string.IsNullOrEmpty(s) ? null : s;
}

internal class OpcionDto
{
    public string Texto { get; set; } = "";
    public bool EsCorrecta { get; set; }
    public int? Orden { get; set; }
}
