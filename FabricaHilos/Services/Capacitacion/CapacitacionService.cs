using Dapper;
using FabricaHilos.Models.Capacitacion;
using Oracle.ManagedDataAccess.Client;

namespace FabricaHilos.Services.Capacitacion;

public class CapacitacionService : OracleServiceBase, ICapacitacionService
{
    public CapacitacionService(IConfiguration cfg, IHttpContextAccessor http)
        : base(cfg, http) { }

    // ─────────────────────────────────────────────────────────────────────────
    // CATEGORÍAS
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<List<CapCategoria>> GetCategoriasAsync()
    {
        await using var db = new OracleConnection(GetOracleConnectionString());
        var rows = await db.QueryAsync<CapCategoria>(
            $@"SELECT c.ID_CATEGORIA, c.NOMBRE, c.DESCRIPCION, c.ICONO_BS, c.COLOR_UI, c.ORDEN,
                      COUNT(cu.ID_CURSO) AS TOTAL_CURSOS
               FROM {S}CAP_CATEGORIA c
               LEFT JOIN {S}CAP_CURSO cu ON cu.ID_CATEGORIA = c.ID_CATEGORIA AND cu.ESTADO = 'A'
               WHERE c.ACTIVO = 'S'
               GROUP BY c.ID_CATEGORIA, c.NOMBRE, c.DESCRIPCION, c.ICONO_BS, c.COLOR_UI, c.ORDEN
               ORDER BY c.ORDEN");
        return rows.ToList();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CATÁLOGO
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<List<CapCurso>> GetCatalogoAsync(
        string codUsuario, int? idCategoria = null, string? busqueda = null,
        string? nivel = null, bool soloObligatorios = false, bool soloPendientes = false,
        int? idCurso = null, int pagina = 1, int tamPag = 0)
    {
        await using var db = new OracleConnection(GetOracleConnectionString());

        var innerSql = $@"
            SELECT cu.ID_CURSO, cu.ID_CATEGORIA, cu.TITULO, cu.DESCRIPCION, cu.IMAGEN_PORTADA,
                   cu.DURACION_MIN, cu.NIVEL, cu.OBLIGATORIO, cu.NOTA_APROBACION, cu.MAX_INTENTOS,
                   cu.TIENE_EXAMEN, cu.TIENE_CERTIFICADO, cu.TIENE_TAREAS,
                   cu.CERT_VALIDEZ_DIAS, cu.ID_CURSO_REQUISITO, cu.NOTA_MIN_REQUISITO, cu.ESTADO,
                   ca.NOMBRE AS NOMBRE_CATEGORIA, ca.COLOR_UI AS COLOR_CATEGORIA, ca.ICONO_BS AS ICONO_CATEGORIA,
                   creq.TITULO AS TITULO_REQUISITO,
                   -- inscripción del usuario
                   i.ESTADO AS ESTADO_INSCRIPCION,
                   CASE WHEN i.ID_INSCRIPCION IS NOT NULL THEN 1 ELSE 0 END AS ESTA_INSCRITO,
                   NVL(pct.pct, 0)  AS PCT_PROGRESO,
                   NVL(pct.total,0) AS TOTAL_LECCIONES,
                   NVL(pct.vistas,0) AS LECCIONES_VISTAS,
                   i.ID_INSCRIPCION,
                   CASE WHEN cert.ID_CERTIFICADO IS NOT NULL THEN 1 ELSE 0 END AS TIENE_CERTIFICADO_EMITIDO,
                   CASE WHEN EXISTS (SELECT 1 FROM {S}CAP_INTENTO_EXAMEN ie
                                     WHERE ie.ID_INSCRIPCION = i.ID_INSCRIPCION
                                       AND ie.APROBADO = 'S' AND ie.ANULADO = 'N')
                        THEN 1 ELSE 0 END AS EXAMEN_APROBADO
            FROM   {S}CAP_CURSO cu
            JOIN   {S}CAP_CATEGORIA ca ON ca.ID_CATEGORIA = cu.ID_CATEGORIA
            LEFT   JOIN {S}CAP_CURSO creq ON creq.ID_CURSO = cu.ID_CURSO_REQUISITO
            LEFT   JOIN {S}CAP_INSCRIPCION i ON i.ID_CURSO = cu.ID_CURSO AND i.COD_USUARIO = :usr AND i.ESTADO <> 'X'
            LEFT   JOIN (
                SELECT p.ID_INSCRIPCION,
                       COUNT(*) AS total,
                       SUM(CASE WHEN p.COMPLETADO='S' THEN 1 ELSE 0 END) AS vistas,
                       ROUND(SUM(CASE WHEN p.COMPLETADO='S' THEN 1 ELSE 0 END)*100.0/COUNT(*)) AS pct
                FROM {S}CAP_PROGRESO p
                GROUP BY p.ID_INSCRIPCION
            ) pct ON pct.ID_INSCRIPCION = i.ID_INSCRIPCION
            LEFT   JOIN {S}CAP_CERTIFICADO cert ON cert.ID_INSCRIPCION = i.ID_INSCRIPCION AND cert.ESTADO = 'V'
            WHERE  cu.ESTADO = 'A'
              AND  (:cat IS NULL OR cu.ID_CATEGORIA = :cat)
              AND  (:niv IS NULL OR cu.NIVEL = :niv)
              AND  (:bus IS NULL OR UPPER(cu.TITULO) LIKE '%' || UPPER(:bus) || '%')
              AND  (:oblig = 0 OR cu.OBLIGATORIO = 'S')
              AND  (:pend = 0 OR i.ID_INSCRIPCION IS NULL)
              AND  (:id IS NULL OR cu.ID_CURSO = :id)
            ORDER BY cu.OBLIGATORIO DESC, cu.ORDEN_LISTA NULLS LAST, cu.TITULO";

        // Paginación Oracle 10g — ROWNUM en subquery
        var sql = tamPag > 0
            ? $"SELECT * FROM (SELECT t.*, ROWNUM AS RN FROM ({innerSql}) t WHERE ROWNUM <= :hasta) WHERE RN > :desde"
            : innerSql;

        var p = new DynamicParameters();
        p.Add("usr",   codUsuario);
        p.Add("cat",   idCategoria,  System.Data.DbType.Int32);
        p.Add("niv",   nivel,        System.Data.DbType.String);
        p.Add("bus",   busqueda,     System.Data.DbType.String);
        p.Add("oblig", soloObligatorios ? 1 : 0);
        p.Add("pend",  soloPendientes   ? 1 : 0);
        p.Add("id",    idCurso,      System.Data.DbType.Int32);
        if (tamPag > 0)
        {
            p.Add("desde", (pagina - 1) * tamPag);
            p.Add("hasta", pagina * tamPag);
        }

        var rows = await db.QueryAsync<dynamic>(sql, p);

        return rows.Select(r => new CapCurso
        {
            IdCurso                = (int)r.ID_CURSO,
            IdCategoria            = (int)r.ID_CATEGORIA,
            Titulo                 = (string)r.TITULO,
            Descripcion            = r.DESCRIPCION is DBNull ? null : (string)r.DESCRIPCION,
            ImagenPortada          = r.IMAGEN_PORTADA is DBNull ? null : (string)r.IMAGEN_PORTADA,
            DuracionMin            = r.DURACION_MIN is DBNull ? null : (int?)Convert.ToInt32(r.DURACION_MIN),
            Nivel                  = (string)r.NIVEL,
            Obligatorio            = (string)r.OBLIGATORIO,
            NotaAprobacion         = Convert.ToDecimal(r.NOTA_APROBACION),
            MaxIntentos            = Convert.ToInt32(r.MAX_INTENTOS),
            TieneExamen            = (string)r.TIENE_EXAMEN,
            TieneCertificado       = (string)r.TIENE_CERTIFICADO,
            TieneTareas            = (string)r.TIENE_TAREAS,
            CertValidezDias        = r.CERT_VALIDEZ_DIAS    is DBNull ? null : (int?)Convert.ToInt32(r.CERT_VALIDEZ_DIAS),
            IdCursoRequisito       = r.ID_CURSO_REQUISITO   is DBNull ? null : (int?)Convert.ToInt32(r.ID_CURSO_REQUISITO),
            TituloRequisito        = r.TITULO_REQUISITO     is DBNull ? null : (string)r.TITULO_REQUISITO,
            NotaMinRequisito       = r.NOTA_MIN_REQUISITO   is DBNull ? 70m   : Convert.ToDecimal(r.NOTA_MIN_REQUISITO),
            Estado                 = (string)r.ESTADO,
            NombreCategoria        = (string)r.NOMBRE_CATEGORIA,
            ColorCategoria         = (string)r.COLOR_CATEGORIA,
            IconoCategoria         = (string)r.ICONO_CATEGORIA,
            EstadoInscripcion      = r.ESTADO_INSCRIPCION is DBNull ? null : (string)r.ESTADO_INSCRIPCION,
            EstaInscrito           = Convert.ToInt32(r.ESTA_INSCRITO) == 1,
            PctProgreso            = Convert.ToInt32(r.PCT_PROGRESO),
            TotalLecciones         = Convert.ToInt32(r.TOTAL_LECCIONES),
            LeccionesVistas        = Convert.ToInt32(r.LECCIONES_VISTAS),
            IdInscripcion          = r.ID_INSCRIPCION is DBNull ? null : (long?)Convert.ToInt64(r.ID_INSCRIPCION),
            TieneCertificadoEmitido = Convert.ToInt32(r.TIENE_CERTIFICADO_EMITIDO) == 1,
            ExamenAprobado          = Convert.ToInt32(r.EXAMEN_APROBADO) == 1,
        }).ToList();
    }

    public async Task<CapCurso?> GetCursoDetalleAsync(int idCurso, string codUsuario)
    {
        var lista = await GetCatalogoAsync(codUsuario, idCurso: idCurso);
        return lista.FirstOrDefault();
    }

    public async Task<int> GetCatalogoTotalAsync(
        string codUsuario, int? idCategoria = null, string? busqueda = null,
        string? nivel = null, bool soloObligatorios = false, bool soloPendientes = false)
    {
        await using var db = new OracleConnection(GetOracleConnectionString());
        var sql = $@"SELECT COUNT(*)
            FROM   {S}CAP_CURSO cu
            JOIN   {S}CAP_CATEGORIA ca ON ca.ID_CATEGORIA = cu.ID_CATEGORIA
            LEFT   JOIN {S}CAP_INSCRIPCION i ON i.ID_CURSO = cu.ID_CURSO AND i.COD_USUARIO = :usr AND i.ESTADO <> 'X'
            WHERE  cu.ESTADO = 'A'
              AND  (:cat IS NULL OR cu.ID_CATEGORIA = :cat)
              AND  (:niv IS NULL OR cu.NIVEL = :niv)
              AND  (:bus IS NULL OR UPPER(cu.TITULO) LIKE '%' || UPPER(:bus) || '%')
              AND  (:oblig = 0 OR cu.OBLIGATORIO = 'S')
              AND  (:pend = 0 OR i.ID_INSCRIPCION IS NULL)";

        var p2 = new DynamicParameters();
        p2.Add("usr",   codUsuario);
        p2.Add("cat",   idCategoria, System.Data.DbType.Int32);
        p2.Add("niv",   nivel,       System.Data.DbType.String);
        p2.Add("bus",   busqueda,    System.Data.DbType.String);
        p2.Add("oblig", soloObligatorios ? 1 : 0);
        p2.Add("pend",  soloPendientes   ? 1 : 0);
        return await db.ExecuteScalarAsync<int>(sql, p2);
    }

    // MI PANEL
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<List<CapCurso>> GetMisCursosAsync(string codUsuario)
    {
        await using var db = new OracleConnection(GetOracleConnectionString());

        var sql = $@"
            SELECT cu.ID_CURSO, cu.TITULO, cu.NIVEL, cu.IMAGEN_PORTADA, cu.DURACION_MIN,
                   cu.OBLIGATORIO, cu.TIENE_CERTIFICADO, cu.NOTA_APROBACION,
                   ca.NOMBRE AS NOMBRE_CATEGORIA, ca.COLOR_UI AS COLOR_CATEGORIA, ca.ICONO_BS AS ICONO_CATEGORIA,
                   i.ID_INSCRIPCION, i.ESTADO AS ESTADO_INSCRIPCION, i.FCH_VENCIMIENTO,
                   NVL(pct.pct,0)   AS PCT_PROGRESO,
                   NVL(pct.total,0) AS TOTAL_LECCIONES,
                   NVL(pct.vistas,0) AS LECCIONES_VISTAS,
                   CASE WHEN cert.ID_CERTIFICADO IS NOT NULL THEN 1 ELSE 0 END AS TIENE_CERTIFICADO_EMITIDO,
                   CASE WHEN EXISTS (SELECT 1 FROM {S}CAP_INTENTO_EXAMEN ie
                                     WHERE ie.ID_INSCRIPCION = i.ID_INSCRIPCION
                                       AND ie.APROBADO = 'S' AND ie.ANULADO = 'N')
                        THEN 1 ELSE 0 END AS EXAMEN_APROBADO
            FROM   {S}CAP_INSCRIPCION i
            JOIN   {S}CAP_CURSO cu ON cu.ID_CURSO = i.ID_CURSO
            JOIN   {S}CAP_CATEGORIA ca ON ca.ID_CATEGORIA = cu.ID_CATEGORIA
            LEFT   JOIN (
                SELECT p.ID_INSCRIPCION,
                       COUNT(*) AS total,
                       SUM(CASE WHEN p.COMPLETADO='S' THEN 1 ELSE 0 END) AS vistas,
                       ROUND(SUM(CASE WHEN p.COMPLETADO='S' THEN 1 ELSE 0 END)*100.0 / COUNT(*)) AS pct
                FROM {S}CAP_PROGRESO p
                GROUP BY p.ID_INSCRIPCION
            ) pct ON pct.ID_INSCRIPCION = i.ID_INSCRIPCION
            LEFT   JOIN {S}CAP_CERTIFICADO cert ON cert.ID_INSCRIPCION = i.ID_INSCRIPCION AND cert.ESTADO = 'V'
            WHERE  i.COD_USUARIO = :usr AND i.ESTADO IN ('P','C')
            ORDER BY i.FCH_INSCRIPCION DESC";

        var rows = await db.QueryAsync<dynamic>(sql, new { usr = codUsuario });
        return rows.Select(r => new CapCurso
        {
            IdCurso                = (int)r.ID_CURSO,
            Titulo                 = (string)r.TITULO,
            Nivel                  = (string)r.NIVEL,
            ImagenPortada          = r.IMAGEN_PORTADA is DBNull ? null : (string)r.IMAGEN_PORTADA,
            DuracionMin            = r.DURACION_MIN is DBNull ? null : (int?)Convert.ToInt32(r.DURACION_MIN),
            Obligatorio            = (string)r.OBLIGATORIO,
            TieneCertificado       = (string)r.TIENE_CERTIFICADO,
            NotaAprobacion         = Convert.ToDecimal(r.NOTA_APROBACION),
            NombreCategoria        = (string)r.NOMBRE_CATEGORIA,
            ColorCategoria         = (string)r.COLOR_CATEGORIA,
            IconoCategoria         = (string)r.ICONO_CATEGORIA,
            IdInscripcion          = Convert.ToInt64(r.ID_INSCRIPCION),
            EstadoInscripcion      = (string)r.ESTADO_INSCRIPCION,
            EstaInscrito           = true,
            DiasParaVencer         = (r.FCH_VENCIMIENTO is null || r.FCH_VENCIMIENTO is DBNull)
                                        ? (int?)null
                                        : (int?)(((DateTime)r.FCH_VENCIMIENTO) - DateTime.Today).Days,
            PctProgreso            = Convert.ToInt32(r.PCT_PROGRESO),
            TotalLecciones         = Convert.ToInt32(r.TOTAL_LECCIONES),
            LeccionesVistas        = Convert.ToInt32(r.LECCIONES_VISTAS),
            TieneCertificadoEmitido = Convert.ToInt32(r.TIENE_CERTIFICADO_EMITIDO) == 1,
            ExamenAprobado          = Convert.ToInt32(r.EXAMEN_APROBADO) == 1,
        }).ToList();
    }

    public async Task<MiPanelVm> GetMiPanelAsync(string codUsuario)
    {
        // Las 4 queries son independientes entre sí → paralelizar
        var cursosTask = GetMisCursosAsync(codUsuario);

        await using var db = new OracleConnection(GetOracleConnectionString());
        var hrsTask = db.ExecuteScalarAsync<int>(
            $@"SELECT NVL(SUM(cu.DURACION_MIN),0) / 60
               FROM {S}CAP_INSCRIPCION i
               JOIN {S}CAP_CURSO cu ON cu.ID_CURSO = i.ID_CURSO
               WHERE i.COD_USUARIO = :usr AND i.ESTADO = 'C'",
            new { usr = codUsuario });

        await using var db2 = new OracleConnection(GetOracleConnectionString());
        var certsTask = db2.ExecuteScalarAsync<int>(
            $"SELECT COUNT(*) FROM {S}CAP_CERTIFICADO WHERE COD_USUARIO = :usr AND ESTADO = 'V'",
            new { usr = codUsuario });

        var recomendadosTask = GetCatalogoAsync(codUsuario, soloPendientes: true);

        await Task.WhenAll(cursosTask, hrsTask, certsTask, recomendadosTask);

        var cursos = cursosTask.Result;
        return new MiPanelVm
        {
            NombreUsuario       = codUsuario,
            CursosEnCurso       = cursos.Count(c => c.EstadoInscripcion == "P"),
            CursosCompletados   = cursos.Count(c => c.EstadoInscripcion == "C"),
            Certificados        = certsTask.Result,
            HorasCapacitacion   = hrsTask.Result,
            CursosActivos       = cursos.Where(c => c.EstadoInscripcion == "P").ToList(),
            CursosAprobados     = cursos.Where(c => c.EstadoInscripcion == "C").ToList(),
            CursosRecomendados  = recomendadosTask.Result.Take(6).ToList(),
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PLAYER
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<CursoPlayerVm?> GetPlayerAsync(int idCurso, long idContenido, string codUsuario)
    {
        await using var db = new OracleConnection(GetOracleConnectionString());

        // Resolver idCurso desde el contenido cuando no se proporciona (ej.: ServirMedia)
        if (idCurso <= 0 && idContenido > 0)
        {
            idCurso = await db.ExecuteScalarAsync<int>(
                $"SELECT NVL(MAX(ID_CURSO), 0) FROM {S}CAP_CONTENIDO WHERE ID_CONTENIDO = :cont AND ACTIVO='S'",
                new { cont = idContenido });
        }
        if (idCurso <= 0) return null;

        // Verificar inscripción activa
        var insc = await db.QueryFirstOrDefaultAsync<CapInscripcion>(
            $@"SELECT ID_INSCRIPCION, COD_USUARIO, ID_CURSO, ESTADO
               FROM {S}CAP_INSCRIPCION
               WHERE COD_USUARIO = :usr AND ID_CURSO = :cur AND ESTADO IN ('P','C')",
            new { usr = codUsuario, cur = idCurso });

        if (insc == null) return null;

        // Secciones con contenidos
        var secciones = (await db.QueryAsync<CapSeccion>(
            $"SELECT * FROM {S}CAP_SECCION WHERE ID_CURSO = :cur AND ACTIVO='S' ORDER BY ORDEN",
            new { cur = idCurso })).ToList();

        var todosContenidos = (await db.QueryAsync<dynamic>(
            $@"SELECT ct.*, NVL(p.COMPLETADO,'N') AS COMPLETADO, NVL(p.SEG_REPRODUCIDO,0) AS SEG_REPRODUCIDO
               FROM {S}CAP_CONTENIDO ct
               LEFT JOIN {S}CAP_PROGRESO p ON p.ID_CONTENIDO = ct.ID_CONTENIDO AND p.ID_INSCRIPCION = :insc
               WHERE ct.ID_CURSO = :cur AND ct.ACTIVO = 'S'
               ORDER BY ct.ORDEN",
            new { cur = idCurso, insc = insc.IdInscripcion })).ToList();

        static CapContenido MapCont(dynamic r) => new()
        {
            IdContenido    = Convert.ToInt64(r.ID_CONTENIDO),
            IdCurso        = Convert.ToInt32(r.ID_CURSO),
            Titulo         = (string)r.TITULO,
            Tipo           = (string)r.TIPO,
            Orden          = Convert.ToInt32(r.ORDEN),
            RutaArchivo    = r.RUTA_ARCHIVO   is DBNull ? null : (string)r.RUTA_ARCHIVO,
            NombreArchOri  = r.NOMBRE_ARCH_ORI is DBNull ? null : (string)r.NOMBRE_ARCH_ORI,
            UrlExterna     = r.URL_EXTERNA     is DBNull ? null : (string)r.URL_EXTERNA,
            ContenidoHtml  = r.CONTENIDO_HTML  is DBNull ? null : (string)r.CONTENIDO_HTML,
            DuracionSeg    = r.DURACION_SEG    is DBNull ? null : (int?)Convert.ToInt32(r.DURACION_SEG),
            Obligatorio    = (string)r.OBLIGATORIO,
            IdSeccion      = r.ID_SECCION is DBNull ? null : (int?)Convert.ToInt32(r.ID_SECCION),
            Completado     = (string)r.COMPLETADO == "S",
            SegReproducido = Convert.ToInt32(r.SEG_REPRODUCIDO),
        };

        var listaCont = todosContenidos.Select(r => MapCont(r)).ToList();

        // Asignar contenidos a secciones, marcar bloqueados
        bool algoBloqueado = false;
        foreach (var sec in secciones)
        {
            sec.Contenidos = listaCont.Where(c => c.IdSeccion == sec.IdSeccion).ToList();
            foreach (var ct in sec.Contenidos)
                if (!algoBloqueado) { ct.Bloqueado = false; if (ct.Obligatorio == "S" && !ct.Completado) algoBloqueado = true; }
                else ct.Bloqueado = true;
            sec.Total      = sec.Contenidos.Count(c => c.Obligatorio == "S");
            sec.Completados = sec.Contenidos.Count(c => c.Obligatorio == "S" && c.Completado);
        }
        var sinSeccion = listaCont.Where(c => c.IdSeccion == null).ToList();

        // Resume inteligente: contenido explícito > video en curso > primer pendiente obligatorio > primer pendiente > último
        CapContenido? actual = idContenido > 0
            ? listaCont.FirstOrDefault(c => c.IdContenido == idContenido)
            : null;
        actual ??= listaCont.FirstOrDefault(c => c.SegReproducido > 0 && !c.Completado)   // video en progreso
                ?? listaCont.FirstOrDefault(c => c.Obligatorio == "S" && !c.Completado)    // siguiente obligatorio
                ?? listaCont.FirstOrDefault(c => !c.Completado)                            // siguiente opcional
                ?? listaCont.LastOrDefault();                                               // todo completado → último
        if (actual == null) return null;
        var idx      = listaCont.IndexOf(actual);
        var anterior = idx > 0 ? listaCont[idx - 1] : null;
        var siguiente = idx < listaCont.Count - 1 ? listaCont[idx + 1] : null;

        // Examen final
        var examen = await db.QueryFirstOrDefaultAsync<CapExamen>(
            $"SELECT * FROM {S}CAP_EXAMEN WHERE ID_CURSO = :cur AND TIPO_EXAMEN='F' AND ACTIVO='S' AND ROWNUM=1",
            new { cur = idCurso });

        bool examAprobado = false;
        if (examen != null)
        {
            examAprobado = await db.ExecuteScalarAsync<int>(
                $@"SELECT COUNT(*) FROM {S}CAP_INTENTO_EXAMEN
                   WHERE ID_INSCRIPCION = :insc AND ID_EXAMEN = :exam AND APROBADO='S' AND ANULADO='N'",
                new { insc = insc.IdInscripcion, exam = examen.IdExamen }) > 0;
        }

        int totalOblig = listaCont.Count(c => c.Obligatorio == "S");
        int vistas     = listaCont.Count(c => c.Obligatorio == "S" && c.Completado);

        // Certificado
        bool certEmitido = await db.ExecuteScalarAsync<int>(
            $"SELECT COUNT(*) FROM {S}CAP_CERTIFICADO WHERE ID_INSCRIPCION = :insc AND ESTADO='V'",
            new { insc = insc.IdInscripcion }) > 0;

        var curso = (await GetCatalogoAsync(codUsuario, idCurso: idCurso))
                        .FirstOrDefault();

        return new CursoPlayerVm
        {
            Curso                  = curso ?? new CapCurso { IdCurso = idCurso },
            IdInscripcion          = insc.IdInscripcion,
            PctProgreso            = totalOblig > 0 ? vistas * 100 / totalOblig : 0,
            LeccionesVistas        = vistas,
            TotalLecciones         = totalOblig,
            Actual                 = actual,
            Anterior               = anterior,
            Siguiente              = siguiente,
            Secciones              = secciones,
            ContenidosSinSeccion   = sinSeccion,
            TieneExamen            = examen != null,
            IdExamen               = examen?.IdExamen,
            ExamenFinalAprobado    = examAprobado,
            ExamenFinalBloqueado   = !examAprobado && vistas < totalOblig,
            TieneCertificado       = curso?.TieneCertificado == "S",
            CertificadoEmitido     = certEmitido,
        };
    }

    public async Task<bool> MarcarCompletadoAsync(long idInscripcion, long idContenido, int segReproducido)
    {
        await using var db = new OracleConnection(GetOracleConnectionString());
        await db.OpenAsync();

        await using var cmd = db.CreateCommand();
        cmd.CommandText =
            $"BEGIN {S}PKG_CAP.SP_CAP_MARCAR_COMPLETADO(:p_insc, :p_cont, :p_seg, :p_res); END;";

        cmd.Parameters.Add(new OracleParameter("p_insc", OracleDbType.Decimal)       { Value     = idInscripcion  });
        cmd.Parameters.Add(new OracleParameter("p_cont", OracleDbType.Decimal)       { Value     = idContenido    });
        cmd.Parameters.Add(new OracleParameter("p_seg",  OracleDbType.Decimal)       { Value     = segReproducido });
        cmd.Parameters.Add(new OracleParameter("p_res",  OracleDbType.Varchar2, 20)  { Direction = System.Data.ParameterDirection.Output });

        await cmd.ExecuteNonQueryAsync();
        return cmd.Parameters["p_res"].Value?.ToString() == "OK";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // INSCRIPCIÓN
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<(bool ok, string msg, long idInscripcion)> InscribirseAsync(int idCurso, string codUsuario)
    {
        await using var db = new OracleConnection(GetOracleConnectionString());
        await db.OpenAsync();

        await using var cmd = db.CreateCommand();
        cmd.CommandText =
            $"BEGIN {S}PKG_CAP.SP_CAP_INSCRIBIR(:p_usr, :p_cur, :p_por, :p_oblig, :p_id, :p_res); END;";

        cmd.Parameters.Add(new OracleParameter("p_usr",  OracleDbType.Varchar2, 50)  { Value     = codUsuario });
        cmd.Parameters.Add(new OracleParameter("p_cur",  OracleDbType.Decimal)        { Value     = idCurso    });
        cmd.Parameters.Add(new OracleParameter("p_por",  OracleDbType.Varchar2, 50)  { Value     = codUsuario });
        cmd.Parameters.Add(new OracleParameter("p_oblig",OracleDbType.Varchar2, 1)   { Value     = "N"        });
        cmd.Parameters.Add(new OracleParameter("p_id",   OracleDbType.Decimal)        { Direction = System.Data.ParameterDirection.Output });
        cmd.Parameters.Add(new OracleParameter("p_res",  OracleDbType.Varchar2, 50)  { Direction = System.Data.ParameterDirection.Output });

        await cmd.ExecuteNonQueryAsync();

        var resultado = cmd.Parameters["p_res"].Value?.ToString() ?? "ERROR";
        var idOut     = cmd.Parameters["p_id"].Value is Oracle.ManagedDataAccess.Types.OracleDecimal od && !od.IsNull
                        ? Convert.ToInt64(od.Value) : 0L;

        return resultado switch
        {
            "OK"            => (true,  "Inscripción realizada.",    idOut),
            "YA_EXISTE"     => (true,  "Ya estás inscrito.",         idOut),
            "SIN_REQUISITO" => (false, "Debe completar el curso prerequisito antes de inscribirse.", 0L),
            _               => (false, "Error al procesar la inscripción.", 0L)
        };
    }

    public async Task<bool> ValidarRequisitoAsync(int idCurso, string codUsuario)
    {
        await using var db = new OracleConnection(GetOracleConnectionString());

        var requisito = await db.QueryFirstOrDefaultAsync<(int? IdReq, decimal NotaMin)>(
            $@"SELECT ID_CURSO_REQUISITO, NVL(NOTA_MIN_REQUISITO, 70)
               FROM {S}CAP_CURSO WHERE ID_CURSO = :cur",
            new { cur = idCurso });

        if (requisito.IdReq == null) return true;

        return await db.ExecuteScalarAsync<int>(
            $@"SELECT COUNT(*) FROM {S}CAP_INTENTO_EXAMEN i
               JOIN {S}CAP_INSCRIPCION ins ON ins.ID_INSCRIPCION = i.ID_INSCRIPCION
               WHERE ins.COD_USUARIO = :usr AND ins.ID_CURSO = :cur
                 AND i.APROBADO = 'S' AND i.ANULADO = 'N'
                 AND i.PUNTAJE_OBT >= :nota",
            new { usr = codUsuario, cur = requisito.IdReq, nota = requisito.NotaMin }) > 0;
    }


    public async Task<List<CapCurso>> GetCursosDependientesAsync(int idCurso)
    {
        await using var db = new OracleConnection(GetOracleConnectionString());

        var sql = $@"SELECT cu.ID_CURSO, cu.TITULO, cu.NIVEL, cu.IMAGEN_PORTADA,
                           ca.NOMBRE AS NOMBRE_CATEGORIA, ca.COLOR_UI AS COLOR_CATEGORIA, ca.ICONO_BS AS ICONO_CATEGORIA
                    FROM {S}CAP_CURSO cu
                    JOIN {S}CAP_CATEGORIA ca ON ca.ID_CATEGORIA = cu.ID_CATEGORIA
                    WHERE cu.ID_CURSO_REQUISITO = :idCurso AND cu.ESTADO = 'A'
                    ORDER BY cu.TITULO";

        var rows = await db.QueryAsync<dynamic>(sql, new { idCurso });

        return rows.Select(r => new CapCurso
        {
            IdCurso         = (int)r.ID_CURSO,
            Titulo          = (string)r.TITULO,
            Nivel           = (string)r.NIVEL,
            ImagenPortada   = r.IMAGEN_PORTADA is DBNull ? null : (string)r.IMAGEN_PORTADA,
            NombreCategoria = (string)r.NOMBRE_CATEGORIA,
            ColorCategoria  = (string)r.COLOR_CATEGORIA,
            IconoCategoria  = (string)r.ICONO_CATEGORIA,
        }).ToList();
    }
    // ─────────────────────────────────────────────────────────────────────────
    // ADMIN
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<List<CapInscripcion>> GetInscripcionesAsync(int idCurso)
    {
        await using var db = new OracleConnection(GetOracleConnectionString());
        var rows = await db.QueryAsync<CapInscripcion>(
            $@"SELECT i.*, c.TIENE_EXAMEN,
                      NVL(pct.pct,0) AS PCT_PROGRESO,
                      NVL(ex.TOTAL_INTENTOS,0) AS TOTAL_INTENTOS,
                      ex.MEJOR_NOTA,
                      ex.EXAMEN_APROBADO,
                      ex.INTENTO_APROBADO
               FROM {S}CAP_INSCRIPCION i
               INNER JOIN {S}CAP_CURSO c ON c.ID_CURSO = i.ID_CURSO
               LEFT JOIN (SELECT ID_INSCRIPCION,
                                 ROUND(SUM(CASE WHEN COMPLETADO='S' THEN 1 ELSE 0 END)*100.0/COUNT(*)) AS pct
                          FROM {S}CAP_PROGRESO GROUP BY ID_INSCRIPCION) pct
                    ON pct.ID_INSCRIPCION = i.ID_INSCRIPCION
               LEFT JOIN (SELECT ie.ID_INSCRIPCION,
                                 COUNT(*) AS TOTAL_INTENTOS,
                                 MAX(ie.PUNTAJE_OBT) AS MEJOR_NOTA,
                                 MAX(ie.APROBADO) AS EXAMEN_APROBADO,
                                 MIN(CASE WHEN ie.APROBADO='S' THEN ie.NRO_INTENTO END) AS INTENTO_APROBADO
                          FROM {S}CAP_INTENTO_EXAMEN ie
                          WHERE ie.ANULADO = 'N'
                          GROUP BY ie.ID_INSCRIPCION) ex
                    ON ex.ID_INSCRIPCION = i.ID_INSCRIPCION
               WHERE i.ID_CURSO = :cur AND i.ESTADO <> 'X'
               ORDER BY i.COD_USUARIO",
            new { cur = idCurso });
        return rows.ToList();
    }

    public async Task<List<CapInscripcion>> GetTodasInscripcionesAsync()
    {
        await using var db = new OracleConnection(GetOracleConnectionString());
        var rows = await db.QueryAsync<CapInscripcion>(
            $@"SELECT i.*, c.TITULO AS TITULO_CURSO, c.TIENE_EXAMEN,
                      NVL(pct.pct,0) AS PCT_PROGRESO,
                      NVL(ex.TOTAL_INTENTOS,0) AS TOTAL_INTENTOS,
                      ex.MEJOR_NOTA,
                      ex.EXAMEN_APROBADO,
                      ex.INTENTO_APROBADO
               FROM {S}CAP_INSCRIPCION i
               INNER JOIN {S}CAP_CURSO c ON c.ID_CURSO = i.ID_CURSO
               LEFT JOIN (SELECT ID_INSCRIPCION,
                                 ROUND(SUM(CASE WHEN COMPLETADO='S' THEN 1 ELSE 0 END)*100.0/COUNT(*)) AS pct
                          FROM {S}CAP_PROGRESO GROUP BY ID_INSCRIPCION) pct
                    ON pct.ID_INSCRIPCION = i.ID_INSCRIPCION
               LEFT JOIN (SELECT ie.ID_INSCRIPCION,
                                 COUNT(*) AS TOTAL_INTENTOS,
                                 MAX(ie.PUNTAJE_OBT) AS MEJOR_NOTA,
                                 MAX(ie.APROBADO) AS EXAMEN_APROBADO,
                                 MIN(CASE WHEN ie.APROBADO='S' THEN ie.NRO_INTENTO END) AS INTENTO_APROBADO
                          FROM {S}CAP_INTENTO_EXAMEN ie
                          WHERE ie.ANULADO = 'N'
                          GROUP BY ie.ID_INSCRIPCION) ex
                    ON ex.ID_INSCRIPCION = i.ID_INSCRIPCION
               WHERE i.ESTADO <> 'X'
               ORDER BY c.TITULO, i.COD_USUARIO");
        return rows.ToList();
    }

    public async Task<bool> InscribirMasivoAsync(int idCurso, IEnumerable<string> usuarios,
        string inscritoPor, bool obligatorio)
    {
        await using var db = new OracleConnection(GetOracleConnectionString());
        await db.OpenAsync();
        await using var trx = await db.BeginTransactionAsync();
        try
        {
            foreach (var usr in usuarios)
                await db.ExecuteAsync(
                    $@"MERGE INTO {S}CAP_INSCRIPCION tgt
                       USING (SELECT :usr AS COD_USUARIO, :cur AS ID_CURSO FROM DUAL) src
                       ON (tgt.COD_USUARIO = src.COD_USUARIO AND tgt.ID_CURSO = src.ID_CURSO AND tgt.ESTADO <> 'X')
                       WHEN NOT MATCHED THEN
                            INSERT (ID_INSCRIPCION, COD_USUARIO, ID_CURSO, FCH_INSCRIPCION, INSCRITO_POR, OBLIGATORIO, ESTADO)
                            VALUES ({S}CAP_SEQ_INSCRIPCION.NEXTVAL, :usr, :cur, SYSDATE, :por, :oblig, 'P')",
                    new { usr, cur = idCurso, por = inscritoPor, oblig = obligatorio ? "S" : "N" },
                    transaction: (System.Data.IDbTransaction)trx);

            await trx.CommitAsync();
            return true;
        }
        catch
        {
            await trx.RollbackAsync();
            return false;
        }
    }

    }
