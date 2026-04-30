using System.Collections.Concurrent;
using FabricaHilos.Models.Sistemas;
using Oracle.ManagedDataAccess.Client;

namespace FabricaHilos.Services.Sistemas;

/// <summary>
/// Singleton que lanza y rastrea los jobs de Restablecer/Revertir
/// de AnularDocumento. Los jobs corren en el servidor de forma
/// independiente del ciclo de vida HTTP: si el navegador se cierra
/// o se cae la red, el proceso continúa hasta completarse.
/// </summary>
public class AnularDocumentoJobManager
{
    private readonly ConcurrentDictionary<string, AnularDocumentoJob> _jobs = new();
    private readonly ILogger<AnularDocumentoJobManager>               _logger;

    public AnularDocumentoJobManager(ILogger<AnularDocumentoJobManager> logger)
    {
        _logger = logger;
    }

    // ── Consulta ───────────────────────────────────────────────────────────────

    public AnularDocumentoJob? Get(string jobId) =>
        _jobs.TryGetValue(jobId, out var j) ? j : null;

    // ── Crear y lanzar: Restablecer ────────────────────────────────────────────

    public AnularDocumentoJob IniciarRestablecer(
        string connString, string schema,
        string tipoDoc, string serie, string numero,
        string numeroBusqueda, string voucherBusqueda,
        string ano, string mes, string libro)
    {
        var job = new AnularDocumentoJob
        {
            Tipo             = "restablecer",
            ConnString       = connString,
            Schema           = schema,
            TipoDoc          = tipoDoc,
            Serie            = serie,
            Numero           = numero,
            NumeroBusqueda   = numeroBusqueda,
            VoucherBusqueda  = voucherBusqueda,
            Ano              = ano,
            Mes              = mes,
            Libro            = libro,
            Pasos            =
            [
                new() { Numero = 1, Estado = "pending" },
                new() { Numero = 2, Estado = "pending" },
                new() { Numero = 3, Estado = "pending" },
                new() { Numero = 4, Estado = "pending" },
            ]
        };

        _jobs[job.JobId] = job;
        _ = Task.Run(() => EjecutarRestablecerAsync(job));
        return job;
    }

    // ── Crear y lanzar: Revertir ───────────────────────────────────────────────

    public AnularDocumentoJob IniciarRevertir(
        string connString, string schema,
        string tipoDoc, string serie,
        string numeroAnterior,
        string ano, string mes, string libro,
        string voucherAnterior)
    {
        var job = new AnularDocumentoJob
        {
            Tipo            = "revertir",
            ConnString      = connString,
            Schema          = schema,
            TipoDoc         = tipoDoc,
            Serie           = serie,
            NumeroAnterior  = numeroAnterior,
            Ano             = ano,
            Mes             = mes,
            Libro           = libro,
            VoucherAnterior = voucherAnterior,
            Pasos           =
            [
                new() { Numero = 1, Estado = "pending" },
                new() { Numero = 2, Estado = "pending" },
            ]
        };

        _jobs[job.JobId] = job;
        _ = Task.Run(() => EjecutarRevertirAsync(job));
        return job;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Ejecución: RESTABLECER (4 pasos)
    // ══════════════════════════════════════════════════════════════════════════

    private async Task EjecutarRestablecerAsync(AnularDocumentoJob job)
    {
        try
        {
            // ── Paso 1: DELETE DOCUVENT ──────────────────────────────────────
            await EjecutarPaso(job, 1, "Ejecutando DELETE en DOCUVENT...", async () =>
            {
                const string sql =
                    @"DELETE FROM {0}DOCUVENT
                       WHERE TIPODOC = :tipoDoc
                         AND SERIE   = :serie
                         AND NUMERO  = :numero";

                await using var con = new OracleConnection(job.ConnString);
                await con.OpenAsync();
                await using var cmd = new OracleCommand(string.Format(sql, job.Schema), con);
                cmd.Parameters.Add("tipoDoc", OracleDbType.Varchar2).Value = job.TipoDoc;
                cmd.Parameters.Add("serie",   OracleDbType.Varchar2).Value = job.Serie;
                cmd.Parameters.Add("numero",  OracleDbType.Varchar2).Value = job.Numero;

                var filas = await cmd.ExecuteNonQueryAsync();
                if (filas == 0)
                    throw new InvalidOperationException("No se encontró el registro en DOCUVENT para eliminar.");
                return (filas, $"DELETE en DOCUVENT ejecutado. Filas afectadas: {filas}.");
            });

            if (job.Estado == "aborted") return;

            // ── Paso 2: ESPERAR MOVGLOS ESTADO=9, luego DELETE MOVGLOS ───────
            await EjecutarPaso(job, 2, "Esperando que MOVGLOS alcance ESTADO = 9...", async () =>
            {
                const string sqlCheck =
                    @"SELECT COUNT(1) FROM {0}MOVGLOS
                       WHERE TIPO_REFERENCIA = :tipoDoc
                         AND SERIE           = :serie
                         AND NRO_REFERENCIA  = :numero
                         AND ESTADO          = '9'";

                var deadline  = DateTime.UtcNow.AddSeconds(30);
                bool encontrado = false;

                while (DateTime.UtcNow < deadline)
                {
                    await using var conCheck = new OracleConnection(job.ConnString);
                    await conCheck.OpenAsync();
                    await using var cmdCheck = new OracleCommand(string.Format(sqlCheck, job.Schema), conCheck);
                    cmdCheck.Parameters.Add("tipoDoc", OracleDbType.Varchar2).Value = job.TipoDoc;
                    cmdCheck.Parameters.Add("serie",   OracleDbType.Varchar2).Value = job.Serie;
                    cmdCheck.Parameters.Add("numero",  OracleDbType.Varchar2).Value = job.Numero;

                    var cnt = Convert.ToInt32(await cmdCheck.ExecuteScalarAsync());
                    if (cnt > 0) { encontrado = true; break; }

                    await Task.Delay(2000);
                }

                if (!encontrado)
                    throw new TimeoutException("Timeout (30s): MOVGLOS nunca alcanzó ESTADO = 9.");

                const string sqlDelete =
                    @"DELETE FROM {0}MOVGLOS
                       WHERE TIPO_REFERENCIA = :tipoDoc
                         AND SERIE           = :serie
                         AND NRO_REFERENCIA  = :numero";

                await using var conDel = new OracleConnection(job.ConnString);
                await conDel.OpenAsync();
                await using var cmdDel = new OracleCommand(string.Format(sqlDelete, job.Schema), conDel);
                cmdDel.Parameters.Add("tipoDoc", OracleDbType.Varchar2).Value = job.TipoDoc;
                cmdDel.Parameters.Add("serie",   OracleDbType.Varchar2).Value = job.Serie;
                cmdDel.Parameters.Add("numero",  OracleDbType.Varchar2).Value = job.Numero;

                var filas = await cmdDel.ExecuteNonQueryAsync();
                return (filas, $"MOVGLOS alcanzó ESTADO=9. DELETE ejecutado. Filas: {filas}.");
            });

            if (job.Estado == "aborted") return;

            // ── Paso 3: UPDATE NRODOC ────────────────────────────────────────
            await EjecutarPaso(job, 3, $"Actualizando NRODOC.NUMERO = {job.NumeroBusqueda}...", async () =>
            {
                const string sql =
                    @"UPDATE {0}NRODOC
                         SET NUMERO  = :numeroBusqueda
                       WHERE TIPODOC = :tipoDoc
                         AND SERIE   = :serie";

                await using var con = new OracleConnection(job.ConnString);
                await con.OpenAsync();
                await using var cmd = new OracleCommand(string.Format(sql, job.Schema), con);
                cmd.Parameters.Add("numeroBusqueda", OracleDbType.Varchar2).Value = job.NumeroBusqueda;
                cmd.Parameters.Add("tipoDoc",        OracleDbType.Varchar2).Value = job.TipoDoc;
                cmd.Parameters.Add("serie",          OracleDbType.Varchar2).Value = job.Serie;

                var filas = await cmd.ExecuteNonQueryAsync();
                return (filas, $"UPDATE NRODOC ejecutado. NUMERO = {job.NumeroBusqueda}. Filas: {filas}.");
            });

            if (job.Estado == "aborted") return;

            // ── Paso 4: UPDATE NROLIBR ───────────────────────────────────────
            await EjecutarPaso(job, 4, $"Actualizando NROLIBR.NUMERO = {job.VoucherBusqueda}...", async () =>
            {
                const string sql =
                    @"UPDATE {0}NROLIBR
                         SET NUMERO = :voucherBusqueda
                       WHERE ANO    = :ano
                         AND MES    = :mes
                         AND LIBRO  = :libro";

                await using var con = new OracleConnection(job.ConnString);
                await con.OpenAsync();
                await using var cmd = new OracleCommand(string.Format(sql, job.Schema), con);
                cmd.Parameters.Add("voucherBusqueda", OracleDbType.Varchar2).Value = job.VoucherBusqueda;
                cmd.Parameters.Add("ano",             OracleDbType.Varchar2).Value = job.Ano;
                cmd.Parameters.Add("mes",             OracleDbType.Varchar2).Value = job.Mes;
                cmd.Parameters.Add("libro",           OracleDbType.Varchar2).Value = job.Libro;

                var filas = await cmd.ExecuteNonQueryAsync();
                return (filas, $"UPDATE NROLIBR ejecutado. NUMERO = {job.VoucherBusqueda}. Filas: {filas}.");
            });

            if (job.Estado != "aborted")
            {
                job.Estado       = "done";
                job.FinalizadoEn = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en job {JobId}", job.JobId);
            job.Estado       = "aborted";
            job.Error        = ex.Message;
            job.FinalizadoEn = DateTime.UtcNow;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Ejecución: REVERTIR (2 pasos)
    // ══════════════════════════════════════════════════════════════════════════

    private async Task EjecutarRevertirAsync(AnularDocumentoJob job)
    {
        try
        {
            // ── Paso 1: UPDATE NRODOC ────────────────────────────────────────
            await EjecutarPaso(job, 1, $"Restaurando NRODOC.NUMERO = {job.NumeroAnterior}...", async () =>
            {
                const string sql =
                    @"UPDATE {0}NRODOC
                         SET NUMERO  = :numeroAnterior
                       WHERE TIPODOC = :tipoDoc
                         AND SERIE   = :serie";

                await using var con = new OracleConnection(job.ConnString);
                await con.OpenAsync();
                await using var cmd = new OracleCommand(string.Format(sql, job.Schema), con);
                cmd.Parameters.Add("numeroAnterior", OracleDbType.Varchar2).Value = job.NumeroAnterior;
                cmd.Parameters.Add("tipoDoc",        OracleDbType.Varchar2).Value = job.TipoDoc;
                cmd.Parameters.Add("serie",          OracleDbType.Varchar2).Value = job.Serie;

                var filas = await cmd.ExecuteNonQueryAsync();
                return (filas, $"NRODOC.NUMERO restaurado a {job.NumeroAnterior}. Filas: {filas}.");
            });

            if (job.Estado == "aborted") return;

            // ── Paso 2: UPDATE NROLIBR ───────────────────────────────────────
            await EjecutarPaso(job, 2, $"Restaurando NROLIBR.NUMERO = {job.VoucherAnterior}...", async () =>
            {
                const string sql =
                    @"UPDATE {0}NROLIBR
                         SET NUMERO = :voucherAnterior
                       WHERE ANO    = :ano
                         AND MES    = :mes
                         AND LIBRO  = :libro";

                await using var con = new OracleConnection(job.ConnString);
                await con.OpenAsync();
                await using var cmd = new OracleCommand(string.Format(sql, job.Schema), con);
                cmd.Parameters.Add("voucherAnterior", OracleDbType.Varchar2).Value = job.VoucherAnterior;
                cmd.Parameters.Add("ano",             OracleDbType.Varchar2).Value = job.Ano;
                cmd.Parameters.Add("mes",             OracleDbType.Varchar2).Value = job.Mes;
                cmd.Parameters.Add("libro",           OracleDbType.Varchar2).Value = job.Libro;

                var filas = await cmd.ExecuteNonQueryAsync();
                return (filas, $"NROLIBR.NUMERO restaurado a {job.VoucherAnterior}. Filas: {filas}.");
            });

            if (job.Estado != "aborted")
            {
                job.Estado       = "done";
                job.FinalizadoEn = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en job revertir {JobId}", job.JobId);
            job.Estado       = "aborted";
            job.Error        = ex.Message;
            job.FinalizadoEn = DateTime.UtcNow;
        }
    }

    // ── Helper: ejecuta un paso, actualiza estado en el job ──────────────────

    private static async Task EjecutarPaso(
        AnularDocumentoJob job,
        int numeroPaso,
        string mensajeInicio,
        Func<Task<(int filas, string mensaje)>> accion)
    {
        var paso = job.Pasos.First(p => p.Numero == numeroPaso);
        paso.Estado  = "running";
        paso.Mensaje = mensajeInicio;

        try
        {
            var (filas, mensaje) = await accion();
            paso.Estado  = "ok";
            paso.Filas   = filas;
            paso.Mensaje = mensaje;
        }
        catch (Exception ex)
        {
            paso.Estado = "error";
            paso.Error  = ex.Message;
            job.Estado  = "aborted";
            job.Error   = $"Error en paso {numeroPaso}: {ex.Message}";
            job.FinalizadoEn = DateTime.UtcNow;
        }
    }
}
