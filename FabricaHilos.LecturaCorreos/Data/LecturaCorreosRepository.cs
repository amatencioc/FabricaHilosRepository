using System.Data;
using Dapper;
using FabricaHilos.LecturaCorreos.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;

namespace FabricaHilos.LecturaCorreos.Data;

public class LecturaCorreosRepository : ILecturaCorreosRepository
{
    private readonly string _connectionString;
    private readonly ILogger<LecturaCorreosRepository> _logger;

    public LecturaCorreosRepository(IConfiguration configuration, ILogger<LecturaCorreosRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("OracleConnection")
            ?? throw new InvalidOperationException("No se encontró la cadena de conexión 'OracleConnection'.");
        _logger = logger;
    }

    private OracleConnection CrearConexion() => new(_connectionString);

    public async Task<IEnumerable<FacturaCorreo>> ObtenerFacturasPendientesCdrAsync()
    {
        const string sql = @"
            SELECT * FROM (
                SELECT f.ID, f.RUC, f.TIPO_COMPROBANTE AS TipoComprobante, f.SERIE, f.CORRELATIVO,
                       f.ESTADO, f.CODIGO_RESPUESTA_SUNAT AS CodigoRespuestaSunat,
                       f.MENSAJE_SUNAT AS MensajeSunat, f.CDR_CONTENIDO AS CdrContenido,
                       f.MENSAJE_ERROR AS MensajeError,
                       f.FECHA_CREACION AS FechaCreacion, f.FECHA_CONSULTA_SUNAT AS FechaConsultaSunat,
                       f.INTENTOS, f.DOCUMENTO_ID AS DocumentoId, f.DOCUMENTO_REFERENCIA AS DocumentoReferencia,
                       d.RUC_RECEPTOR AS RucReceptor
                FROM FH_LECTCORREOS_FACTURAS f
                LEFT JOIN FH_LC_DOCUMENTO d ON d.ID = f.DOCUMENTO_ID
                WHERE f.ESTADO = 'PENDIENTE_CDR'
                  AND f.INTENTOS < 5
                ORDER BY f.FECHA_CREACION ASC
            ) WHERE ROWNUM <= 50";

        return await OracleRetry.EjecutarAsync(
            async () =>
            {
                using var conn = CrearConexion();
                return await conn.QueryAsync<FacturaCorreo>(sql);
            },
            _logger, nameof(ObtenerFacturasPendientesCdrAsync));
    }

    public async Task ActualizarEstadoAsync(
        long id,
        string estado,
        string codigoSunat,
        string mensajeSunat,
        byte[]? cdrZip)
    {
        const string sql = @"
            UPDATE FH_LECTCORREOS_FACTURAS
            SET ESTADO = :Estado,
                CODIGO_RESPUESTA_SUNAT = :CodigoSunat,
                MENSAJE_SUNAT = :MensajeSunat,
                CDR_CONTENIDO = :CdrZip,
                FECHA_CONSULTA_SUNAT = SYSDATE
            WHERE ID = :Id";

        await OracleRetry.EjecutarAsync(
            async () =>
            {
                using var conn = CrearConexion();
                await conn.ExecuteAsync(sql, new
                {
                    Id = id,
                    Estado = estado,
                    CodigoSunat = codigoSunat,
                    MensajeSunat = mensajeSunat,
                    CdrZip = cdrZip
                });
            },
            _logger, nameof(ActualizarEstadoAsync));
    }

    public async Task IncrementarIntentosAsync(long id)
    {
        const string sql = @"
            UPDATE FH_LECTCORREOS_FACTURAS
            SET INTENTOS = INTENTOS + 1,
                FECHA_CONSULTA_SUNAT = SYSDATE
            WHERE ID = :Id";

        await OracleRetry.EjecutarAsync(
            async () =>
            {
                using var conn = CrearConexion();
                await conn.ExecuteAsync(sql, new { Id = id });
            },
            _logger, nameof(IncrementarIntentosAsync));
    }

    public async Task GuardarErrorAsync(long id, string mensajeError)
    {
        const string sql = @"
            UPDATE FH_LECTCORREOS_FACTURAS
            SET MENSAJE_ERROR = :MensajeError
            WHERE ID = :Id";

        await OracleRetry.EjecutarAsync(
            async () =>
            {
                using var conn = CrearConexion();
                await conn.ExecuteAsync(sql, new { Id = id, MensajeError = mensajeError });
            },
            _logger, nameof(GuardarErrorAsync));
    }

    public async Task InsertarFacturaPendienteCdrAsync(FacturaCorreo factura)
    {
        const string sql = @"
            INSERT INTO FH_LECTCORREOS_FACTURAS
                (RUC, TIPO_COMPROBANTE, SERIE, CORRELATIVO,
                 ESTADO, FECHA_CREACION, INTENTOS,
                 DOCUMENTO_ID, DOCUMENTO_REFERENCIA)
            VALUES
                (:Ruc, :TipoComprobante, :Serie, :Correlativo,
                 :Estado, SYSDATE, 0,
                 :DocumentoId, :DocumentoReferencia)";

        await OracleRetry.EjecutarAsync(
            async () =>
            {
                using var conn = CrearConexion();
                await conn.ExecuteAsync(sql, new
                {
                    factura.Ruc,
                    factura.TipoComprobante,
                    factura.Serie,
                    factura.Correlativo,
                    factura.Estado,
                    factura.DocumentoId,
                    factura.DocumentoReferencia
                });
            },
            _logger, nameof(InsertarFacturaPendienteCdrAsync));
    }

    public async Task<long?> ObtenerIdFacturaPorSerieAsync(string ruc, string serie, int correlativo)
    {
        const string sql = @"
            SELECT ID FROM FH_LECTCORREOS_FACTURAS
            WHERE RUC = :Ruc AND SERIE = :Serie AND CORRELATIVO = :Correlativo
              AND ESTADO = 'PENDIENTE_CDR'
              AND ROWNUM = 1";

        return await OracleRetry.EjecutarAsync(
            async () =>
            {
                using var conn = CrearConexion();
                return await conn.QueryFirstOrDefaultAsync<long?>(sql, new { Ruc = ruc, Serie = serie, Correlativo = correlativo });
            },
            _logger, nameof(ObtenerIdFacturaPorSerieAsync));
    }

    // ── SOLO PRUEBAS: limpieza de tablas ─────────────────────────────────────
    public async Task<LimpiezaResultado> LimpiarTablasAsync(CancellationToken ct = default)
    {
        // El orden respeta las FK: primero tablas hijo, luego la tabla padre.
        // FH_LECTCORREOS_ARCHIVOS      → hijo de FH_LC_DOCUMENTO (DOCUMENTO_ID nullable)
        // FH_LC_LINEA                  → hijo de FH_LC_DOCUMENTO
        // FH_LC_CUOTA                  → hijo de FH_LC_DOCUMENTO
        // FH_LECTCORREOS_FACTURAS      → hijo de FH_LC_DOCUMENTO
        // FH_LC_ERROR                  → independiente
        // FH_LECTCORREOS_PDF_ADJUNTOS  → independiente
        // FH_LC_DOCUMENTO              → padre
        using var conn = CrearConexion();
        await conn.OpenAsync(ct);

        using var tx = conn.BeginTransaction();
        try
        {
            // Timeout de 30 s por DELETE: evita colgarse indefinidamente si otra
            // sesión Oracle mantiene un bloqueo sobre alguna de estas tablas.
            static CommandDefinition Cmd(string sql, IDbTransaction t, CancellationToken c) =>
                new(sql, transaction: t, cancellationToken: c, commandTimeout: 30);

            int archivos     = await conn.ExecuteAsync(Cmd("DELETE FROM FH_LECTCORREOS_ARCHIVOS",        tx, ct));
            int lineas       = await conn.ExecuteAsync(Cmd("DELETE FROM FH_LC_LINEA",                   tx, ct));
            int cuotas       = await conn.ExecuteAsync(Cmd("DELETE FROM FH_LC_CUOTA",                   tx, ct));
            int facturas     = await conn.ExecuteAsync(Cmd("DELETE FROM FH_LECTCORREOS_FACTURAS",        tx, ct));
            int errores      = await conn.ExecuteAsync(Cmd("DELETE FROM FH_LC_ERROR",                   tx, ct));
            int pdfAdjuntos  = await conn.ExecuteAsync(Cmd("DELETE FROM FH_LECTCORREOS_PDF_ADJUNTOS",   tx, ct));
            int documentos   = await conn.ExecuteAsync(Cmd("DELETE FROM FH_LC_DOCUMENTO",               tx, ct));

            tx.Commit();

            return new LimpiezaResultado(lineas, cuotas, facturas, errores, documentos, pdfAdjuntos, archivos);
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}
