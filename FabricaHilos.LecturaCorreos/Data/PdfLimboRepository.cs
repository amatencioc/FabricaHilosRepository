using Dapper;
using FabricaHilos.LecturaCorreos.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;

namespace FabricaHilos.LecturaCorreos.Data;

public class PdfLimboRepository : IPdfLimboRepository
{
    private readonly string _connStr;
    private readonly ILogger<PdfLimboRepository> _logger;

    public PdfLimboRepository(IConfiguration configuration, ILogger<PdfLimboRepository> logger)
    {
        _connStr = configuration.GetConnectionString("OracleConnection")
            ?? throw new InvalidOperationException("No se encontró la cadena de conexión 'OracleConnection'.");
        _logger = logger;
    }

    private OracleConnection CrearConexion() => new(_connStr);

    public async Task<IReadOnlyList<AdjuntoPdf>> ObtenerPendientesNotificacionAsync()
    {
        // Se excluye CONTENIDO (blob) para evitar cargar binarios innecesariamente.
        // ROWNUM <= 100: procesa en lotes para no cargar grandes backlog en memoria de una vez.
        const string sql = @"
            SELECT * FROM (
                SELECT ID, NOMBRE_ARCHIVO   AS NombreArchivo,
                            CUENTA_CORREO   AS CuentaCorreo,
                            ASUNTO_CORREO   AS AsuntoCorreo,
                            REMITENTE_CORREO AS RemitenteCorreo,
                            FECHA_CORREO    AS FechaCorreo,
                            ESTADO,
                            FECHA_CREACION  AS FechaCreacion
                FROM  FH_LECTCORREOS_PDF_ADJUNTOS
                WHERE ESTADO = 'PENDIENTE'
                ORDER BY FECHA_CREACION ASC
            ) WHERE ROWNUM <= 100";

        return await OracleRetry.EjecutarAsync(
            async () =>
            {
                using var conn = CrearConexion();
                var resultado = await conn.QueryAsync<AdjuntoPdf>(sql);
                return (IReadOnlyList<AdjuntoPdf>)resultado.ToList();
            },
            _logger, nameof(ObtenerPendientesNotificacionAsync));
    }

    public async Task MarcarCorreoEnviadoAsync(long id)
    {
        const string sql = @"
            UPDATE FH_LECTCORREOS_PDF_ADJUNTOS
            SET    ESTADO = 'ENVIO_CORREO_OK'
            WHERE  ID = :Id";

        await OracleRetry.EjecutarAsync(
            async () =>
            {
                using var conn = CrearConexion();
                await conn.ExecuteAsync(sql, new { Id = id });
            },
            _logger, nameof(MarcarCorreoEnviadoAsync));
    }

    public async Task MarcarErrorNotificacionAsync(long id, string mensajeError)
    {
        const string sql = @"
            UPDATE FH_LECTCORREOS_PDF_ADJUNTOS
            SET    ESTADO = 'ERROR_CORREO'
            WHERE  ID = :Id";

        await OracleRetry.EjecutarAsync(
            async () =>
            {
                using var conn = CrearConexion();
                await conn.ExecuteAsync(sql, new { Id = id });
            },
            _logger, nameof(MarcarErrorNotificacionAsync));

        _logger.LogWarning(
            "PDF adjunto ID={Id} marcado como ERROR_CORREO. Detalle: {Error}", id, mensajeError);
    }
}
