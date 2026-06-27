using Dapper;
using FabricaHilos.Models.Capacitacion;
using Oracle.ManagedDataAccess.Client;

namespace FabricaHilos.Services.Capacitacion;

public class ExamenService : OracleServiceBase, IExamenService
{
    public ExamenService(IConfiguration cfg, IHttpContextAccessor http) : base(cfg, http) { }

    // ─────────────────────────────────────────────────────────────────────────
    // OBTENER EXAMEN
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<CapExamen?> GetExamenAsync(int idExamen)
    {
        await using var db = new OracleConnection(GetOracleConnectionString());
        return await db.QueryFirstOrDefaultAsync<CapExamen>(
            $"SELECT * FROM {S}CAP_EXAMEN WHERE ID_EXAMEN = :id AND ACTIVO = 'S'",
            new { id = idExamen });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // INICIAR INTENTO
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<(bool ok, string msg, long idIntento)> IniciarIntentoAsync(
        int idExamen, long idInscripcion, string codUsuario)
    {
        await using var db = new OracleConnection(GetOracleConnectionString());
        await db.OpenAsync();

        await using var cmd = db.CreateCommand();
        cmd.CommandText =
            $"BEGIN {S}PKG_CAP.SP_CAP_INICIAR_INTENTO(:p_exam, :p_insc, :p_usr, :p_id, :p_res); END;";

        cmd.Parameters.Add(new OracleParameter("p_exam", OracleDbType.Decimal)       { Value     = idExamen      });
        cmd.Parameters.Add(new OracleParameter("p_insc", OracleDbType.Decimal)       { Value     = idInscripcion });
        cmd.Parameters.Add(new OracleParameter("p_usr",  OracleDbType.Varchar2, 50)  { Value     = codUsuario    });
        cmd.Parameters.Add(new OracleParameter("p_id",   OracleDbType.Decimal)        { Direction = System.Data.ParameterDirection.Output });
        cmd.Parameters.Add(new OracleParameter("p_res",  OracleDbType.Varchar2, 50)  { Direction = System.Data.ParameterDirection.Output });

        await cmd.ExecuteNonQueryAsync();

        var resultado = cmd.Parameters["p_res"].Value?.ToString() ?? "ERROR";
        var idOut     = cmd.Parameters["p_id"].Value is Oracle.ManagedDataAccess.Types.OracleDecimal od && !od.IsNull
                        ? Convert.ToInt64(od.Value) : 0L;

        return resultado switch
        {
            "OK"               => (true,  "Intento iniciado.",                 idOut),
            "EN_PROGRESO"      => (true,  "Intento en curso.",                 idOut),
            "LIMITE_ALCANZADO" => (false, "Se alcanzó el límite de intentos.", 0L),
            "NO_ENCONTRADO"    => (false, "Examen no encontrado.",             0L),
            _                  => (false, "Error al iniciar el intento.",      0L)
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PREGUNTAS
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<List<CapPregunta>> GetPreguntasParaIntentoAsync(int idExamen, long idIntento)
    {
        await using var db = new OracleConnection(GetOracleConnectionString());
        var examen = await GetExamenAsync(idExamen);
        if (examen == null) return [];

        List<CapPregunta> preguntas;

        if (examen.EsModoAleatorio)
        {
            // Multi-banco: tomar N preguntas aleatorias de todos los bancos vinculados
            preguntas = (await db.QueryAsync<CapPregunta>(
                $@"SELECT * FROM (
                       SELECT p.* FROM {S}CAP_PREGUNTA p
                       JOIN {S}CAP_EXAMEN_BANCO eb ON eb.ID_BANCO = p.ID_BANCO
                       WHERE eb.ID_EXAMEN = :exam AND p.ACTIVO = 'S'
                       ORDER BY DBMS_RANDOM.VALUE
                   ) WHERE ROWNUM <= :n",
                new { exam = idExamen, n = examen.NroPregAleatorias ?? 10 })).ToList();
        }
        else
        {
            preguntas = (await db.QueryAsync<CapPregunta>(
                $@"SELECT * FROM {S}CAP_PREGUNTA
                   WHERE ID_EXAMEN = :exam AND ACTIVO = 'S'
                   ORDER BY {(examen.MezclarPreg == "S" ? "DBMS_RANDOM.VALUE" : "NVL(ORDEN,999), ID_PREGUNTA")}",
                new { exam = idExamen })).ToList();
        }

        // Cargar opciones
        if (preguntas.Any())
        {
            var ids = preguntas.Select(p => p.IdPregunta).ToList();
            var opciones = (await db.QueryAsync<CapOpcion>(
                $@"SELECT * FROM {S}CAP_OPCION WHERE ID_PREGUNTA IN ({string.Join(",", ids)})
                   ORDER BY {(examen.MezclarOpc == "S" ? "DBMS_RANDOM.VALUE" : "NVL(ORDEN,999)")}")).ToList();

            foreach (var p in preguntas)
                p.Opciones = opciones.Where(o => o.IdPregunta == p.IdPregunta).ToList();
        }

        return preguntas;
    }

    public async Task<ExamenRendirVm?> GetRendirVmAsync(long idIntento, int nroPregunta)
    {
        await using var db = new OracleConnection(GetOracleConnectionString());
        var intento = await db.QueryFirstOrDefaultAsync<CapIntentoExamen>(
            $@"SELECT i.*, ex.TIEMPO_MIN, cu.TITULO AS TITULO_EXAMEN,
                      c.TITULO AS TITULO_CURSO
               FROM {S}CAP_INTENTO_EXAMEN i
               JOIN {S}CAP_EXAMEN ex ON ex.ID_EXAMEN = i.ID_EXAMEN
               JOIN {S}CAP_CURSO c ON c.ID_CURSO = ex.ID_CURSO
               WHERE i.ID_INTENTO = :id AND i.FCH_FIN IS NULL AND i.ANULADO = 'N'",
            new { id = idIntento });

        if (intento == null) return null;

        var preguntas = await GetPreguntasParaIntentoAsync(intento.IdExamen, idIntento);
        if (!preguntas.Any()) return null;

        int idx = Math.Clamp(nroPregunta, 0, preguntas.Count - 1);

        // Cargar respuestas ya guardadas
        var respYa = (await db.QueryAsync<CapRespuesta>(
            $"SELECT * FROM {S}CAP_RESPUESTA WHERE ID_INTENTO = :id",
            new { id = idIntento })).ToList();

        var respondidas = preguntas.Select(p =>
            respYa.Any(r => r.IdPregunta == p.IdPregunta)).ToList();

        var pregActual = preguntas[idx];
        var respActual = respYa.FirstOrDefault(r => r.IdPregunta == pregActual.IdPregunta);
        if (respActual != null)
            foreach (var o in pregActual.Opciones)
                o.Seleccionada = o.IdOpcion == respActual.IdOpcion;

        return new ExamenRendirVm
        {
            IdIntento        = idIntento,
            IdInscripcion    = intento.IdInscripcion,
            IdExamen         = intento.IdExamen,
            TituloCurso      = intento.TituloExamen ?? "",   // mapeado como TituloExamen
            TituloExamen     = intento.TituloExamen ?? "",
            TiempoMin        = intento.TiempoMin ?? 30,
            MinutosRestantes = intento.MinutosRestantes,
            FchVencimiento   = intento.FchVencimiento,
            TotalPreguntas   = preguntas.Count,
            PregActual       = idx,
            PreguntaActual   = pregActual,
            Respondidas      = respondidas,
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GUARDAR RESPUESTA
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<bool> GuardarRespuestaAsync(long idIntento, long idPregunta, long idOpcion)
    {
        if (!await ValidarTiempoAsync(idIntento)) return false;

        await using var db = new OracleConnection(GetOracleConnectionString());
        // MERGE — reemplazar si ya respondió
        await db.ExecuteAsync(
            $@"MERGE INTO {S}CAP_RESPUESTA tgt
               USING (SELECT :int AS ID_INTENTO, :preg AS ID_PREGUNTA FROM DUAL) src
               ON (tgt.ID_INTENTO = src.ID_INTENTO AND tgt.ID_PREGUNTA = src.ID_PREGUNTA)
               WHEN MATCHED THEN
                   UPDATE SET ID_OPCION = :opc
               WHEN NOT MATCHED THEN
                   INSERT (ID_RESPUESTA, ID_INTENTO, ID_PREGUNTA, ID_OPCION, ES_CORRECTA)
                   VALUES ({S}SEQ_CAP_RESPUESTA.NEXTVAL, :int, :preg, :opc, 'N')",
            new { @int = idIntento, preg = idPregunta, opc = idOpcion });
        return true;
    }

    public async Task<bool> GuardarRespuestaTextoAsync(long idIntento, long idPregunta, string texto)
    {
        if (!await ValidarTiempoAsync(idIntento)) return false;

        await using var db = new OracleConnection(GetOracleConnectionString());
        await db.ExecuteAsync(
            $@"MERGE INTO {S}CAP_RESPUESTA_TEXTO tgt
               USING (SELECT :int AS ID_INTENTO, :preg AS ID_PREGUNTA FROM DUAL) src
               ON (tgt.ID_INTENTO = src.ID_INTENTO AND tgt.ID_PREGUNTA = src.ID_PREGUNTA)
               WHEN MATCHED THEN UPDATE SET TEXTO_RESPUESTA = :txt, FCH_RESPUESTA = SYSDATE
               WHEN NOT MATCHED THEN
                   INSERT (ID_RESP_TEXTO, ID_INTENTO, ID_PREGUNTA, TEXTO_RESPUESTA, FCH_RESPUESTA, ESTADO_CORR)
                   VALUES ({S}SEQ_CAP_RESP_TEXTO.NEXTVAL, :int, :preg, :txt, SYSDATE, 'P')",
            new { @int = idIntento, preg = idPregunta, txt = texto });
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PROCESAR Y CERRAR
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<ExamenResultadoVm?> ProcesarYCerrarAsync(long idIntento, string codUsuario)
    {
        await using var db = new OracleConnection(GetOracleConnectionString());
        await db.OpenAsync();

        await using var cmd = db.CreateCommand();
        cmd.CommandText =
            $"BEGIN {S}PKG_CAP.SP_CAP_PROCESAR_EXAMEN(:p_int, :p_usr, :p_pun, :p_apr, :p_res); END;";

        cmd.Parameters.Add(new OracleParameter("p_int", OracleDbType.Decimal)       { Value     = idIntento  });
        cmd.Parameters.Add(new OracleParameter("p_usr", OracleDbType.Varchar2, 50)  { Value     = codUsuario });
        cmd.Parameters.Add(new OracleParameter("p_pun", OracleDbType.Decimal)        { Direction = System.Data.ParameterDirection.Output });
        cmd.Parameters.Add(new OracleParameter("p_apr", OracleDbType.Varchar2, 1)   { Direction = System.Data.ParameterDirection.Output });
        cmd.Parameters.Add(new OracleParameter("p_res", OracleDbType.Varchar2, 20)  { Direction = System.Data.ParameterDirection.Output });

        await cmd.ExecuteNonQueryAsync();

        if (cmd.Parameters["p_res"].Value?.ToString() != "OK")
            return null;

        return await GetResultadoAsync(idIntento, codUsuario);
    }

    public async Task<ExamenResultadoVm?> GetResultadoAsync(long idIntento, string codUsuario)
    {
        await using var db = new OracleConnection(GetOracleConnectionString());
        var row = await db.QueryFirstOrDefaultAsync<dynamic>(
            $@"SELECT ie.ID_INTENTO, ie.ID_EXAMEN, ie.NRO_INTENTO, ie.PUNTAJE_OBT, ie.APROBADO,
                      ex.TITULO, ex.ID_CURSO, cu.TITULO AS TITULO_CURSO,
                      cu.NOTA_APROBACION, cu.MAX_INTENTOS, cu.TIENE_CERTIFICADO,
                      (SELECT COUNT(*) FROM {S}CAP_INTENTO_EXAMEN i2
                       WHERE i2.ID_INSCRIPCION=ie.ID_INSCRIPCION AND i2.ID_EXAMEN=ie.ID_EXAMEN AND i2.ANULADO='N') AS total_int
               FROM {S}CAP_INTENTO_EXAMEN ie
               JOIN {S}CAP_EXAMEN ex ON ex.ID_EXAMEN = ie.ID_EXAMEN
               JOIN {S}CAP_CURSO cu ON cu.ID_CURSO = ex.ID_CURSO
               WHERE ie.ID_INTENTO = :id",
            new { id = idIntento });

        if (row == null) return null;

        return new ExamenResultadoVm
        {
            IdIntento       = idIntento,
            IdExamen        = (int)row.ID_EXAMEN,
            IdCurso         = (int)row.ID_CURSO,
            TituloExamen    = (string)row.TITULO,
            TituloCurso     = (string)row.TITULO_CURSO,
            PuntajeObt      = row.PUNTAJE_OBT == DBNull.Value ? 0 : Convert.ToDecimal(row.PUNTAJE_OBT),
            NotaAprobacion  = Convert.ToDecimal(row.NOTA_APROBACION),
            Aprobado        = (string)row.APROBADO == "S",
            NroIntento      = Convert.ToInt32(row.TOTAL_INT),
            MaxIntentos     = Convert.ToInt32(row.MAX_INTENTOS),
            TieneCertificado = (string)row.TIENE_CERTIFICADO == "S",
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ANTI-TRAMPA — validar tiempo server-side
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<bool> ValidarTiempoAsync(long idIntento)
    {
        await using var db = new OracleConnection(GetOracleConnectionString());
        return await db.ExecuteScalarAsync<int>(
            $@"SELECT COUNT(*) FROM {S}CAP_INTENTO_EXAMEN ie
               JOIN {S}CAP_EXAMEN ex ON ex.ID_EXAMEN = ie.ID_EXAMEN
               WHERE ie.ID_INTENTO = :id AND ie.FCH_FIN IS NULL AND ie.ANULADO = 'N'
               AND (SYSDATE - ie.FCH_INI) * 1440 <= ex.TIEMPO_MIN + 1",
            new { id = idIntento }) > 0;
    }
}
