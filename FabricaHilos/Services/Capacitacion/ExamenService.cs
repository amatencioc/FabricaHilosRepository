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

        // Usar ORA_HASH con idIntento como seed para orden determinístico reproducible
        // Así cada intento tiene su propio orden fijo de preguntas/opciones
        var orderPreg = examen.MezclarPreg == "S"
            ? $"ORA_HASH(p.ID_PREGUNTA + :seed)"
            : "NVL(p.ORDEN,999), p.ID_PREGUNTA";

        List<CapPregunta> preguntas;

        if (examen.EsModoAleatorio)
        {
            // Multi-banco: tomar N preguntas con orden reproducible por intento
            preguntas = (await db.QueryAsync<CapPregunta>(
                $@"SELECT * FROM (
                       SELECT p.* FROM {S}CAP_PREGUNTA p
                       JOIN {S}CAP_EXAMEN_BANCO eb ON eb.ID_BANCO = p.ID_BANCO
                       WHERE eb.ID_EXAMEN = :exam AND p.ACTIVO = 'S'
                       ORDER BY ORA_HASH(p.ID_PREGUNTA + :seed)
                   ) WHERE ROWNUM <= :n",
                new { exam = idExamen, seed = idIntento, n = examen.NroPregAleatorias ?? 10 })).ToList();
        }
        else
        {
            preguntas = (await db.QueryAsync<CapPregunta>(
                $@"SELECT p.* FROM {S}CAP_PREGUNTA p
                   WHERE p.ID_EXAMEN = :exam AND p.ACTIVO = 'S'
                   ORDER BY {orderPreg}",
                new { exam = idExamen, seed = idIntento })).ToList();
        }

        // Cargar opciones con orden reproducible por intento
        if (preguntas.Any())
        {
            var ids = preguntas.Select(p => p.IdPregunta).ToList();
            var orderOpc = examen.MezclarOpc == "S"
                ? "ORA_HASH(o.ID_OPCION + :seed)"
                : "NVL(o.ORDEN,999)";

            var opciones = (await db.QueryAsync<CapOpcion>(
                $@"SELECT o.* FROM {S}CAP_OPCION o WHERE o.ID_PREGUNTA IN ({string.Join(",", ids)})
                   ORDER BY {orderOpc}",
                new { seed = idIntento })).ToList();

            foreach (var p in preguntas)
                p.Opciones = opciones.Where(o => o.IdPregunta == p.IdPregunta).ToList();
        }

        return preguntas;
    }

    public async Task<ExamenRendirVm?> GetRendirVmAsync(long idIntento, int nroPregunta)
    {
        await using var db = new OracleConnection(GetOracleConnectionString());
        var intento = await db.QueryFirstOrDefaultAsync<CapIntentoExamen>(
            $@"SELECT i.*, ex.TIEMPO_MIN, ex.TITULO AS TITULO_EXAMEN, c.TITULO AS TITULO_CURSO
               FROM {S}CAP_INTENTO_EXAMEN i
               JOIN {S}CAP_EXAMEN ex ON ex.ID_EXAMEN = i.ID_EXAMEN
               JOIN {S}CAP_CURSO c ON c.ID_CURSO = ex.ID_CURSO
               WHERE i.ID_INTENTO = :id AND i.FCH_FIN IS NULL AND i.ANULADO = 'N'",
            new { id = idIntento });

        if (intento == null) return null;

        var preguntas = await GetPreguntasParaIntentoAsync(intento.IdExamen, idIntento);
        if (!preguntas.Any()) return null;

        int idx = Math.Clamp(nroPregunta, 0, preguntas.Count - 1);

        // Cargar respuestas ya guardadas (opciones)
        var respYa = (await db.QueryAsync<CapRespuesta>(
            $"SELECT * FROM {S}CAP_RESPUESTA WHERE ID_INTENTO = :id",
            new { id = idIntento })).ToList();

        // Cargar respuestas de texto (RC/ENS)
        var respTexto = (await db.QueryAsync<dynamic>(
            $"SELECT ID_PREGUNTA, TEXTO_ALUMNO FROM {S}CAP_RESPUESTA_TEXTO WHERE ID_INTENTO = :id",
            new { id = idIntento })).ToList();

        var respondidas = preguntas.Select(p =>
            respYa.Any(r => r.IdPregunta == p.IdPregunta)
            || respTexto.Any(rt => Convert.ToInt64(rt.ID_PREGUNTA) == p.IdPregunta)).ToList();

        var pregActual = preguntas[idx];
        var respActuales = respYa.Where(r => r.IdPregunta == pregActual.IdPregunta).ToList();
        if (respActuales.Count > 0)
            foreach (var o in pregActual.Opciones)
                o.Seleccionada = respActuales.Any(r => r.IdOpcion == o.IdOpcion);

        // Restaurar texto guardado para preguntas RC/ENS (dynamic de Oracle — CS8602 esperado)
        if (pregActual.RequiereCalificacionManual && respTexto.Count > 0)
        {
#pragma warning disable CS8602, CS8605
            var txtGuardado = respTexto.FirstOrDefault(
                rt => rt != null && Convert.ToInt64(rt.ID_PREGUNTA) == pregActual.IdPregunta);
            if (txtGuardado != null && txtGuardado.TEXTO_ALUMNO is not DBNull)
                pregActual.TextoRespuestaGuardado = txtGuardado.TEXTO_ALUMNO?.ToString() ?? "";
#pragma warning restore CS8602, CS8605
        }

        return new ExamenRendirVm
        {
            IdIntento        = idIntento,
            IdInscripcion    = intento.IdInscripcion,
            IdExamen         = intento.IdExamen,
            TituloCurso      = intento.TituloCurso ?? "",
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

    public async Task<bool> GuardarRespuestaAsync(long idIntento, long idPregunta, string idOpcion)
    {
        if (!await ValidarTiempoAsync(idIntento)) return false;

        await using var db = new OracleConnection(GetOracleConnectionString());
        await db.OpenAsync();

        // Parsear opciones (puede ser una sola o varias separadas por coma para OV)
        var opciones = (idOpcion ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(v => long.TryParse(v.Trim(), out var id) ? id : (long?)null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToList();

        if (opciones.Count == 0) return false;

        // Usar transacción para garantizar atomicidad (DELETE + INSERTs)
        await using var trx = await db.BeginTransactionAsync();
        try
        {
            // Eliminar respuestas previas para esta pregunta
            await db.ExecuteAsync(
                $"DELETE FROM {S}CAP_RESPUESTA WHERE ID_INTENTO = :int AND ID_PREGUNTA = :preg",
                new { @int = idIntento, preg = idPregunta },
                transaction: (System.Data.IDbTransaction)trx);

            // Insertar cada opción seleccionada
            foreach (var opc in opciones)
            {
                await db.ExecuteAsync(
                    $@"INSERT INTO {S}CAP_RESPUESTA (ID_RESPUESTA, ID_INTENTO, ID_PREGUNTA, ID_OPCION, ES_CORRECTA)
                       VALUES ({S}CAP_SEQ_RESPUESTA.NEXTVAL, :int, :preg, :opc, 'N')",
                    new { @int = idIntento, preg = idPregunta, opc },
                    transaction: (System.Data.IDbTransaction)trx);
            }

            await trx.CommitAsync();
            return true;
        }
        catch
        {
            await trx.RollbackAsync();
            return false;
        }
    }

    public async Task<bool> GuardarRespuestaTextoAsync(long idIntento, long idPregunta, string texto)
    {
        if (!await ValidarTiempoAsync(idIntento)) return false;

        await using var db = new OracleConnection(GetOracleConnectionString());
        await db.ExecuteAsync(
            $@"MERGE INTO {S}CAP_RESPUESTA_TEXTO tgt
               USING (SELECT :int AS ID_INTENTO, :preg AS ID_PREGUNTA FROM DUAL) src
               ON (tgt.ID_INTENTO = src.ID_INTENTO AND tgt.ID_PREGUNTA = src.ID_PREGUNTA)
               WHEN MATCHED THEN UPDATE SET TEXTO_ALUMNO = :txt
               WHEN NOT MATCHED THEN
                   INSERT (ID_RESP_TEXTO, ID_INTENTO, ID_PREGUNTA, TEXTO_ALUMNO, ESTADO_CORR)
                   VALUES ({S}CAP_SEQ_RESP_TXT.NEXTVAL, :int, :preg, :txt, 'P')",
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
                      ie.ID_INSCRIPCION,
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
            IdInscripcion   = Convert.ToInt64(row.ID_INSCRIPCION),
            TituloExamen    = (string)row.TITULO,
            TituloCurso     = (string)row.TITULO_CURSO,
            PuntajeObt      = row.PUNTAJE_OBT is DBNull ? 0 : Convert.ToDecimal(row.PUNTAJE_OBT),
            NotaAprobacion  = row.NOTA_APROBACION is DBNull ? 0 : Convert.ToDecimal(row.NOTA_APROBACION),
            Aprobado        = row.APROBADO is DBNull ? false : Convert.ToString(row.APROBADO) == "S",
            NroIntento      = Convert.ToInt32(row.NRO_INTENTO),
            MaxIntentos     = row.MAX_INTENTOS is DBNull ? 0 : Convert.ToInt32(row.MAX_INTENTOS),
            TieneCertificado = row.TIENE_CERTIFICADO is DBNull ? false : Convert.ToString(row.TIENE_CERTIFICADO) == "S",
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

    public async Task<List<CapIntentoExamen>> GetIntentosAsync(long idInscripcion)
    {
        await using var db = new OracleConnection(GetOracleConnectionString());
        var rows = await db.QueryAsync<CapIntentoExamen>(
            $@"SELECT ID_INTENTO AS IdIntento, ID_INSCRIPCION AS IdInscripcion,
                      ID_EXAMEN AS IdExamen, NRO_INTENTO AS NroIntento,
                      FCH_INI AS FchIni, FCH_FIN AS FchFin,
                      PUNTAJE_OBT AS PuntajeObt, APROBADO, ANULADO
               FROM {S}CAP_INTENTO_EXAMEN
               WHERE ID_INSCRIPCION = :id AND ANULADO = 'N'
               ORDER BY NRO_INTENTO",
            new { id = idInscripcion });
        return rows.ToList();
    }
}
