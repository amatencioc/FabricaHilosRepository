using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System.Data;
using FabricaHilos.Models.Sgc;
using FabricaHilos.Notificaciones.Abstractions;
using FabricaHilos.Notificaciones.Models.Payloads;
using FabricaHilos.Services.Logistica;

namespace FabricaHilos.Services.Sgc.AnalisisReclamo;

// ────────────────────────────────────────────────────────────────────────────
//  INTERFAZ
// ────────────────────────────────────────────────────────────────────────────

public interface IAnalisisReclamoService
{
    /// <summary>Lista de reclamos con filtros opcionales.</summary>
    Task<List<ReclamoDto>> ObtenerReclamosAsync(string? buscar, string? estado);

    /// <summary>Cabecera de un reclamo específico.</summary>
    Task<ReclamoDto?> ObtenerReclamoAsync(long idReclamo);

    /// <summary>Descargos de un reclamo ordenados por fecha.</summary>
    Task<List<ReclamoDescargoDto>> ObtenerDescargosAsync(long idReclamo);

    /// <summary>Archivos adjuntos de un reclamo ordenados por fecha.</summary>
    Task<List<ReclamoArchivoDto>> ObtenerArchivosAsync(long idReclamo);

    /// <summary>Crea el reclamo y el primer descargo del vendedor. Retorna (IdReclamo, Error).</summary>
    Task<(long IdReclamo, string? Error)> CrearReclamoAsync(CrearReclamoRequest req, string usuario);

    /// <summary>Agrega un descargo de vendedor (VD) o analista (AC). Retorna (IdDescargo, Error).</summary>
    Task<(long IdDescargo, string? Error)> AgregarDescargoAsync(
        long idReclamo, string rol, string descripcion, string usuario);

    /// <summary>Registra la referencia de un archivo en BD. Retorna (IdArchivo, Error).</summary>
    Task<(long IdArchivo, string? Error)> RegistrarArchivoAsync(
        long idReclamo, string rol,
        string nombreOrig, string nombreServer,
        string mimeType, long tamanio, string usuario);

    /// <summary>Elimina el registro del archivo en BD. Retorna null si OK o mensaje de error.</summary>
    Task<string?> EliminarArchivoAsync(long idArchivo, string usuario);

    /// <summary>Cambia el estado del reclamo (corrección manual '01'-'05').</summary>
    Task<string?> CambiarEstadoAsync(long idReclamo, string estado, string usuario);

    /// <summary>Analista escala el reclamo a Gerencia (estado '02' → '03').</summary>
    Task<string?> EscalarGerenciaAsync(long idReclamo, string usuario);

    /// <summary>Gerente aprueba el reclamo ('03' → '04'). Observación opcional.</summary>
    Task<string?> AprobarReclamoAsync(long idReclamo, string? observacion, string usuario);

    /// <summary>Gerente rechaza el reclamo ('02'/'03' → '05'). Motivo obligatorio.</summary>
    Task<string?> RechazarReclamoAsync(long idReclamo, string motivo, string usuario);

    /// <summary>Clientes activos para el combo — búsqueda opcional.</summary>
    Task<List<ClienteComboDto>> ObtenerClientesAsync(string? buscar = null);

    /// <summary>
    /// Obtiene el nombre del archivo en servidor desde un ID de archivo.
    /// Se usa antes de eliminar el archivo físico.
    /// </summary>
    Task<ReclamoArchivoDto?> ObtenerArchivoAsync(long idArchivo);

    /// <summary>
    /// Elimina completamente un reclamo (archivos BD, descargos, cabecera).
    /// Devuelve (NombresServer, Error) — NombresServer es lista sep. por '|'.
    /// La capa de presentación debe borrar la carpeta física.
    /// </summary>
    Task<(string? NombresServer, string? Error)> EliminarReclamoAsync(long idReclamo, string usuario);

    /// <summary>Guarda el Análisis de Causa del Analista de Calidad.</summary>
    Task<string?> GuardarAnalisisCausaAsync(long idReclamo, string texto, string usuario);

    /// <summary>Guarda la Decisión del Analista de Calidad (solo cuando estado=04).</summary>
    Task<string?> GuardarDecisionAsync(long idReclamo, string texto, string usuario);

    /// <summary>
    /// Notifica al área de Calidad que el vendedor ha enviado un reclamo.
    /// Devuelve (Destinatarios, AsuntoMail, NomCliente, Error).
    /// Destinatarios es una cadena con correos separados por ';'.
    /// </summary>
    Task<(string? Destinatarios, string? AsuntoMail, string? NomCliente, string? Error)> NotificarCalidadAsync(long idReclamo, string usuario);

    /// <summary>
    /// Notifica al Vendedor que el reclamo ha sido aprobado y evaluado.
    /// Devuelve (Destinatario, AsuntoMail, NomCliente, Error).
    /// </summary>
    Task<(string? Destinatario, string? AsuntoMail, string? NomCliente, string? Error)> NotificarVendedorAprobadoAsync(long idReclamo, string usuario);

    /// <summary>
    /// Obtiene todos los datos necesarios para imprimir un reclamo aprobado.
    /// Incluye cabecera, descargos, archivos, análisis de causa, decisión y datos del gerente.
    /// Solo permitido cuando ESTADO='04' (Aprobado).
    /// </summary>
    Task<ReclamoImpresionDto?> ObtenerDatosImpresionAsync(long idReclamo);
}

// ────────────────────────────────────────────────────────────────────────────
//  IMPLEMENTACIÓN
// ────────────────────────────────────────────────────────────────────────────

public class AnalisisReclamoService : OracleServiceBase, IAnalisisReclamoService
{
    private readonly ILogger<AnalisisReclamoService> _logger;
    private readonly IEmailNotificacionService       _emailService;

    public AnalisisReclamoService(
        IConfiguration       configuration,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AnalisisReclamoService> logger,
        IEmailNotificacionService emailService)
        : base(configuration, httpContextAccessor)
    {
        _logger       = logger;
        _emailService = emailService;
    }

    // ── Helpers de lectura de OracleDataReader ───────────────────────────────

    private static string?   Str(OracleDataReader r, string col) =>
        r[col] == DBNull.Value ? null : r[col]?.ToString();

    private static long      Long(OracleDataReader r, string col) =>
        r[col] == DBNull.Value ? 0L : Convert.ToInt64(r[col]);

    private static int       Int(OracleDataReader r, string col) =>
        r[col] == DBNull.Value ? 0 : Convert.ToInt32(r[col]);

    private static DateTime  Dt(OracleDataReader r, string col) =>
        Convert.ToDateTime(r[col]);

    private static DateTime? NullDt(OracleDataReader r, string col) =>
        r[col] == DBNull.Value ? null : Convert.ToDateTime(r[col]);

    /// <summary>
    /// Lee un parámetro de salida ODP.NET evitando el literal "null" que
    /// ODP.NET devuelve cuando Oracle retorna NULL en un VARCHAR2 de salida.
    /// </summary>
    private static string? OraOutStr(OracleCommand cmd, string paramName)
    {
        var raw = cmd.Parameters[paramName].Value;
        if (raw == null || raw == DBNull.Value) return null;
        var s = raw.ToString();
        return string.Equals(s, "null", StringComparison.OrdinalIgnoreCase) ? null : s?.Trim();
    }


    public async Task<List<ReclamoDto>> ObtenerReclamosAsync(string? buscar, string? estado)
    {
        var result = new List<ReclamoDto>();
        try
        {
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"BEGIN {S}PKG_SGC_RECLAMO.P_OBTENER_RECLAMOS(:buscar,:estado,:cursor); END;";
            cmd.CommandType = CommandType.Text;
            cmd.Parameters.Add("buscar", OracleDbType.Varchar2, buscar ?? (object)DBNull.Value, ParameterDirection.Input);
            cmd.Parameters.Add("estado", OracleDbType.Varchar2, estado ?? (object)DBNull.Value, ParameterDirection.Input);
            cmd.Parameters.Add("cursor", OracleDbType.RefCursor, ParameterDirection.Output);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(MapReclamo((OracleDataReader)reader));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en ObtenerReclamosAsync");
        }
        return result;
    }

    // ── P_OBTENER_RECLAMO ────────────────────────────────────────────────────

    public async Task<ReclamoDto?> ObtenerReclamoAsync(long idReclamo)
    {
        try
        {
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"BEGIN {S}PKG_SGC_RECLAMO.P_OBTENER_RECLAMO(:id,:cursor); END;";
            cmd.Parameters.Add("id",     OracleDbType.Decimal, idReclamo, ParameterDirection.Input);
            cmd.Parameters.Add("cursor", OracleDbType.RefCursor, ParameterDirection.Output);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
                return MapReclamo((OracleDataReader)reader);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en ObtenerReclamoAsync({Id})", idReclamo);
        }
        return null;
    }

    // ── P_OBTENER_DESCARGOS ──────────────────────────────────────────────────

    public async Task<List<ReclamoDescargoDto>> ObtenerDescargosAsync(long idReclamo)
    {
        var result = new List<ReclamoDescargoDto>();
        try
        {
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"BEGIN {S}PKG_SGC_RECLAMO.P_OBTENER_DESCARGOS(:id,:cursor); END;";
            cmd.Parameters.Add("id",     OracleDbType.Decimal, idReclamo, ParameterDirection.Input);
            cmd.Parameters.Add("cursor", OracleDbType.RefCursor, ParameterDirection.Output);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var r = (OracleDataReader)reader;
                result.Add(new ReclamoDescargoDto
                {
                    IdDescargo  = Long(r, "ID_DESCARGO"),
                    IdReclamo   = Long(r, "ID_RECLAMO"),
                    Rol         = Str(r, "ROL")         ?? "",
                    Descripcion = Str(r, "DESCRIPCION") ?? "",
                    Usuario     = Str(r, "USUARIO")     ?? "",
                    FchRegistro = Dt(r,  "FCH_REGISTRO")
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en ObtenerDescargosAsync({Id})", idReclamo);
        }
        return result;
    }

    // ── P_OBTENER_ARCHIVOS ───────────────────────────────────────────────────

    public async Task<List<ReclamoArchivoDto>> ObtenerArchivosAsync(long idReclamo)
    {
        var result = new List<ReclamoArchivoDto>();
        try
        {
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"BEGIN {S}PKG_SGC_RECLAMO.P_OBTENER_ARCHIVOS(:id,:cursor); END;";
            cmd.Parameters.Add("id",     OracleDbType.Decimal, idReclamo, ParameterDirection.Input);
            cmd.Parameters.Add("cursor", OracleDbType.RefCursor, ParameterDirection.Output);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                result.Add(MapArchivo((OracleDataReader)reader));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en ObtenerArchivosAsync({Id})", idReclamo);
        }
        return result;
    }

    // ── ObtenerArchivoAsync (por ID) ─────────────────────────────────────────

    public async Task<ReclamoArchivoDto?> ObtenerArchivoAsync(long idArchivo)
    {
        try
        {
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                SELECT ID_ARCHIVO, ID_RECLAMO, ROL,
                       NOMBRE_ORIG, NOMBRE_SERVER,
                       MIME_TYPE, TAMANIO_BYTES, USUARIO, FCH_CARGA
                FROM   {S}SGC_RECLAMO_ARCHIVO
                WHERE  ID_ARCHIVO = :id";
            cmd.Parameters.Add("id", OracleDbType.Decimal, idArchivo, ParameterDirection.Input);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
                return MapArchivo((OracleDataReader)reader);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en ObtenerArchivoAsync({Id})", idArchivo);
        }
        return null;
    }

    // ── P_OBTENER_CLIENTES ───────────────────────────────────────────────────

    public async Task<List<ClienteComboDto>> ObtenerClientesAsync(string? buscar = null)
    {
        var result = new List<ClienteComboDto>();
        try
        {
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"BEGIN {S}PKG_SGC_RECLAMO.P_OBTENER_CLIENTES(:buscar,:cursor); END;";
            cmd.Parameters.Add("buscar", OracleDbType.Varchar2, buscar ?? (object)DBNull.Value, ParameterDirection.Input);
            cmd.Parameters.Add("cursor", OracleDbType.RefCursor, ParameterDirection.Output);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var r = (OracleDataReader)reader;
                result.Add(new ClienteComboDto
                {
                    CodCliente = Str(r, "COD_CLIENTE") ?? "",
                    NomCliente = Str(r, "NOM_CLIENTE") ?? "",
                    RucCliente = Str(r, "RUC_CLIENTE") ?? ""
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en ObtenerClientesAsync");
        }
        return result;
    }

    // ── P_CREAR_RECLAMO ──────────────────────────────────────────────────────

    public async Task<(long IdReclamo, string? Error)> CrearReclamoAsync(
        CrearReclamoRequest req, string usuario)
    {
        try
        {
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                BEGIN {S}PKG_SGC_RECLAMO.P_CREAR_RECLAMO(
                    :codCliente, :nomCliente, :contacto, :telefono,
                    :asunto, :descargo, :usuario,
                    :idReclamo, :msgerror
                ); END;";

            cmd.Parameters.Add("codCliente", OracleDbType.Varchar2,  req.CodCliente,    ParameterDirection.Input);
            cmd.Parameters.Add("nomCliente", OracleDbType.Varchar2,  req.NomCliente,    ParameterDirection.Input);
            cmd.Parameters.Add("contacto",   OracleDbType.Varchar2,  req.Contacto,      ParameterDirection.Input);
            cmd.Parameters.Add("telefono",   OracleDbType.Varchar2,  req.Telefono,      ParameterDirection.Input);
            cmd.Parameters.Add("asunto",     OracleDbType.Varchar2,  req.Asunto,        ParameterDirection.Input);
            cmd.Parameters.Add("descargo",   OracleDbType.Varchar2,  req.Descargo,      ParameterDirection.Input);
            cmd.Parameters.Add("usuario",    OracleDbType.Varchar2,  usuario,           ParameterDirection.Input);
            cmd.Parameters.Add("idReclamo",  OracleDbType.Decimal,   ParameterDirection.Output);
            cmd.Parameters.Add("msgerror",   OracleDbType.Varchar2,  4000, ParameterDirection.Output);

            await cmd.ExecuteNonQueryAsync();

            var msgerror = OraOutStr(cmd, "msgerror");
            if (!string.IsNullOrWhiteSpace(msgerror)) return (0, msgerror);

            long id = Convert.ToInt64(cmd.Parameters["idReclamo"].Value.ToString());
            return (id, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en CrearReclamoAsync");
            return (0, $"Error al crear reclamo: {ex.Message}");
        }
    }

    // ── P_AGREGAR_DESCARGO ───────────────────────────────────────────────────

    public async Task<(long IdDescargo, string? Error)> AgregarDescargoAsync(
        long idReclamo, string rol, string descripcion, string usuario)
    {
        try
        {
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                BEGIN {S}PKG_SGC_RECLAMO.P_AGREGAR_DESCARGO(
                    :idReclamo, :rol, :descripcion, :usuario,
                    :idDescargo, :msgerror
                ); END;";

            cmd.Parameters.Add("idReclamo",   OracleDbType.Decimal,  idReclamo,   ParameterDirection.Input);
            cmd.Parameters.Add("rol",         OracleDbType.Varchar2, rol,         ParameterDirection.Input);
            cmd.Parameters.Add("descripcion", OracleDbType.Varchar2, descripcion, ParameterDirection.Input);
            cmd.Parameters.Add("usuario",     OracleDbType.Varchar2, usuario,     ParameterDirection.Input);
            cmd.Parameters.Add("idDescargo",  OracleDbType.Decimal,  ParameterDirection.Output);
            cmd.Parameters.Add("msgerror",    OracleDbType.Varchar2, 4000, ParameterDirection.Output);

            await cmd.ExecuteNonQueryAsync();

            var msgerror = OraOutStr(cmd, "msgerror");
            if (!string.IsNullOrWhiteSpace(msgerror)) return (0, msgerror);

            long id = Convert.ToInt64(cmd.Parameters["idDescargo"].Value.ToString());
            return (id, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en AgregarDescargoAsync");
            return (0, $"Error al agregar descargo: {ex.Message}");
        }
    }

    // ── P_REGISTRAR_ARCHIVO ──────────────────────────────────────────────────
    // NOTA: Se bypasea el SP del paquete Oracle (buffer overflow en versión
    //       desplegada) y se ejecuta el INSERT directamente desde C#.

    public async Task<(long IdArchivo, string? Error)> RegistrarArchivoAsync(
        long idReclamo, string rol,
        string nombreOrig, string nombreServer,
        string mimeType, long tamanio, string usuario)
    {
        try
        {
            _logger.LogInformation("[RegistrarArchivo] Inicio — reclamo={Id} rol={Rol} archivo={Nom}", idReclamo, rol, nombreOrig);

            if (rol != "VD" && rol != "AC")
                return (0, "ROL inválido.");

            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();

            // 1. Verificar existencia y estado del reclamo
            string? estado;
            using (var chkCmd = conn.CreateCommand())
            {
                chkCmd.CommandText = $"SELECT ESTADO FROM {S}SGC_RECLAMO WHERE ID_RECLAMO = :id";
                chkCmd.Parameters.Add("id", OracleDbType.Decimal, idReclamo, ParameterDirection.Input);
                var val = await chkCmd.ExecuteScalarAsync();
                if (val == null || val == DBNull.Value)
                    return (0, $"El reclamo {idReclamo} no existe.");
                estado = val.ToString();
            }
            _logger.LogInformation("[RegistrarArchivo] Estado reclamo={Estado}", estado);

            if (estado is "03" or "04" or "05")
                return (0, "No se puede adjuntar archivos a un reclamo cerrado, aprobado o rechazado.");

            // 2. Truncar valores a los tamaños de columna para evitar overflow
            var nomOrig   = nombreOrig  .Length > 500 ? nombreOrig  [..500] : nombreOrig;
            var nomServer = nombreServer.Length > 500 ? nombreServer[..500] : nombreServer;
            var mime      = mimeType    .Length > 100 ? mimeType    [..100] : mimeType;
            var usr       = usuario     .Length >  30 ? usuario     [.. 30] : usuario;

            // 3. INSERT con RETURNING para obtener el ID generado por la secuencia
            long newId;
            using var tran = conn.BeginTransaction();
            try
            {
                using (var insCmd = conn.CreateCommand())
                {
                    insCmd.Transaction = tran;
                    insCmd.CommandText = $@"
                        INSERT INTO {S}SGC_RECLAMO_ARCHIVO (
                            ID_ARCHIVO, ID_RECLAMO, ROL,
                            NOMBRE_ORIG, NOMBRE_SERVER,
                            MIME_TYPE, TAMANIO_BYTES,
                            USUARIO, FCH_CARGA
                        ) VALUES (
                            {S}SGC_RECLAMO_ARCH_SEQ.NEXTVAL, :idReclamo, :rol,
                            :nomOrig, :nomServer,
                            :mime, :tamanio,
                            :usr, SYSDATE
                        ) RETURNING ID_ARCHIVO INTO :newId";
                    insCmd.Parameters.Add("idReclamo", OracleDbType.Decimal,  idReclamo, ParameterDirection.Input);
                    insCmd.Parameters.Add("rol",       OracleDbType.Varchar2, rol,       ParameterDirection.Input);
                    insCmd.Parameters.Add("nomOrig",   OracleDbType.Varchar2, nomOrig,   ParameterDirection.Input);
                    insCmd.Parameters.Add("nomServer", OracleDbType.Varchar2, nomServer, ParameterDirection.Input);
                    insCmd.Parameters.Add("mime",      OracleDbType.Varchar2, mime,      ParameterDirection.Input);
                    insCmd.Parameters.Add("tamanio",   OracleDbType.Decimal,  tamanio,   ParameterDirection.Input);
                    insCmd.Parameters.Add("usr",       OracleDbType.Varchar2, usr,       ParameterDirection.Input);
                    insCmd.Parameters.Add("newId",     OracleDbType.Decimal,  ParameterDirection.Output);
                    await insCmd.ExecuteNonQueryAsync();
                    newId = Convert.ToInt64(insCmd.Parameters["newId"].Value.ToString());
                }
                _logger.LogInformation("[RegistrarArchivo] INSERT OK — newId={NewId}", newId);

                // 4. Actualizar auditoría del reclamo
                using (var updCmd = conn.CreateCommand())
                {
                    updCmd.Transaction = tran;
                    updCmd.CommandText = $@"
                        UPDATE {S}SGC_RECLAMO
                        SET    A_MDUSER  = :usr,
                               A_MDFECHA = SYSDATE
                        WHERE  ID_RECLAMO = :idReclamo";
                    updCmd.Parameters.Add("usr",       OracleDbType.Varchar2, usr,       ParameterDirection.Input);
                    updCmd.Parameters.Add("idReclamo", OracleDbType.Decimal,  idReclamo, ParameterDirection.Input);
                    await updCmd.ExecuteNonQueryAsync();
                }

                tran.Commit();
                _logger.LogInformation("[RegistrarArchivo] Commit OK");
            }
            catch
            {
                tran.Rollback();
                throw;
            }

            return (newId, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en RegistrarArchivoAsync");
            return (0, $"Error al registrar archivo: {ex.Message}");
        }
    }

    // ── P_ELIMINAR_ARCHIVO ───────────────────────────────────────────────────

    public async Task<string?> EliminarArchivoAsync(long idArchivo, string usuario)
    {
        try
        {
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                BEGIN {S}PKG_SGC_RECLAMO.P_ELIMINAR_ARCHIVO(:idArchivo,:usuario,:msgerror); END;";

            cmd.Parameters.Add("idArchivo", OracleDbType.Decimal,  idArchivo, ParameterDirection.Input);
            cmd.Parameters.Add("usuario",   OracleDbType.Varchar2, usuario,   ParameterDirection.Input);
            cmd.Parameters.Add("msgerror",  OracleDbType.Varchar2, 4000, ParameterDirection.Output);

            await cmd.ExecuteNonQueryAsync();

            var msgerror = OraOutStr(cmd, "msgerror");
            return string.IsNullOrWhiteSpace(msgerror) ? null : msgerror;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en EliminarArchivoAsync({Id})", idArchivo);
            return $"Error al eliminar archivo: {ex.Message}";
        }
    }

    // ── P_CAMBIAR_ESTADO ─────────────────────────────────────────────────────

    public async Task<string?> CambiarEstadoAsync(long idReclamo, string estado, string usuario)
    {
        try
        {
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                BEGIN {S}PKG_SGC_RECLAMO.P_CAMBIAR_ESTADO(:idReclamo,:estado,:usuario,:msgerror); END;";

            cmd.Parameters.Add("idReclamo", OracleDbType.Decimal,  idReclamo, ParameterDirection.Input);
            cmd.Parameters.Add("estado",    OracleDbType.Varchar2, estado,    ParameterDirection.Input);
            cmd.Parameters.Add("usuario",   OracleDbType.Varchar2, usuario,   ParameterDirection.Input);
            cmd.Parameters.Add("msgerror",  OracleDbType.Varchar2, 4000, ParameterDirection.Output);

            await cmd.ExecuteNonQueryAsync();

            var msgerror = OraOutStr(cmd, "msgerror");
            return string.IsNullOrWhiteSpace(msgerror) ? null : msgerror;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en CambiarEstadoAsync({Id})", idReclamo);
            return $"Error al cambiar estado: {ex.Message}";
        }
    }

    // ── P_ESCALAR_GERENCIA ───────────────────────────────────────────────────

    public async Task<string?> EscalarGerenciaAsync(long idReclamo, string usuario)
    {
        try
        {
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"BEGIN {S}PKG_SGC_RECLAMO.P_ESCALAR_GERENCIA(:idReclamo,:usuario,:msgerror); END;";
            cmd.Parameters.Add("idReclamo", OracleDbType.Decimal,  idReclamo, ParameterDirection.Input);
            cmd.Parameters.Add("usuario",   OracleDbType.Varchar2, usuario,   ParameterDirection.Input);
            cmd.Parameters.Add("msgerror",  OracleDbType.Varchar2, 4000, ParameterDirection.Output);

            await cmd.ExecuteNonQueryAsync();
            var msg = OraOutStr(cmd, "msgerror");
            return string.IsNullOrWhiteSpace(msg) ? null : msg;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en EscalarGerenciaAsync({Id})", idReclamo);
            return $"Error al escalar a gerencia: {ex.Message}";
        }
    }

    // ── P_APROBAR_RECLAMO ────────────────────────────────────────────────────

    public async Task<string?> AprobarReclamoAsync(long idReclamo, string? observacion, string usuario)
    {
        try
        {
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"BEGIN {S}PKG_SGC_RECLAMO.P_APROBAR_RECLAMO(:idReclamo,:observacion,:usuario,:msgerror); END;";
            cmd.Parameters.Add("idReclamo",   OracleDbType.Decimal,  idReclamo,                   ParameterDirection.Input);
            cmd.Parameters.Add("observacion", OracleDbType.Varchar2, observacion ?? (object)DBNull.Value, ParameterDirection.Input);
            cmd.Parameters.Add("usuario",     OracleDbType.Varchar2, usuario,                     ParameterDirection.Input);
            cmd.Parameters.Add("msgerror",    OracleDbType.Varchar2, 4000, ParameterDirection.Output);

            await cmd.ExecuteNonQueryAsync();
            var msg = OraOutStr(cmd, "msgerror");
            return string.IsNullOrWhiteSpace(msg) ? null : msg;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en AprobarReclamoAsync({Id})", idReclamo);
            return $"Error al aprobar reclamo: {ex.Message}";
        }
    }

    // ── P_RECHAZAR_RECLAMO ───────────────────────────────────────────────────

    public async Task<string?> RechazarReclamoAsync(long idReclamo, string motivo, string usuario)
    {
        try
        {
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"BEGIN {S}PKG_SGC_RECLAMO.P_RECHAZAR_RECLAMO(:idReclamo,:motivo,:usuario,:msgerror); END;";
            cmd.Parameters.Add("idReclamo", OracleDbType.Decimal,  idReclamo, ParameterDirection.Input);
            cmd.Parameters.Add("motivo",    OracleDbType.Varchar2, motivo,    ParameterDirection.Input);
            cmd.Parameters.Add("usuario",   OracleDbType.Varchar2, usuario,   ParameterDirection.Input);
            cmd.Parameters.Add("msgerror",  OracleDbType.Varchar2, 4000, ParameterDirection.Output);

            await cmd.ExecuteNonQueryAsync();
            var msg = OraOutStr(cmd, "msgerror");
            return string.IsNullOrWhiteSpace(msg) ? null : msg;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en RechazarReclamoAsync({Id})", idReclamo);
            return $"Error al rechazar reclamo: {ex.Message}";
        }
    }

    // ── P_ELIMINAR_RECLAMO ───────────────────────────────────────────────────

    public async Task<(string? NombresServer, string? Error)> EliminarReclamoAsync(long idReclamo, string usuario)
    {
        try
        {
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"BEGIN {S}PKG_SGC_RECLAMO.P_ELIMINAR_RECLAMO(:idReclamo,:usuario,:nombresServer,:msgerror); END;";
            cmd.Parameters.Add("idReclamo",     OracleDbType.Decimal,  idReclamo, ParameterDirection.Input);
            cmd.Parameters.Add("usuario",        OracleDbType.Varchar2, usuario,   ParameterDirection.Input);
            cmd.Parameters.Add("nombresServer",  OracleDbType.Varchar2, 32767, ParameterDirection.Output);
            cmd.Parameters.Add("msgerror",       OracleDbType.Varchar2, 4000,  ParameterDirection.Output);

            await cmd.ExecuteNonQueryAsync();

            var msg     = OraOutStr(cmd, "msgerror");
            var nombres = OraOutStr(cmd, "nombresServer");

            if (!string.IsNullOrWhiteSpace(msg))
                return (null, msg);

            return (nombres, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en EliminarReclamoAsync({Id})", idReclamo);
            return (null, $"Error al eliminar reclamo: {ex.Message}");
        }
    }

    // ── P_GUARDAR_ANALISIS_CAUSA ─────────────────────────────────────────────

    public async Task<string?> GuardarAnalisisCausaAsync(long idReclamo, string texto, string usuario)
    {
        try
        {
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"BEGIN {S}PKG_SGC_RECLAMO.P_GUARDAR_ANALISIS_CAUSA(:idReclamo,:texto,:usuario,:msgerror); END;";
            cmd.Parameters.Add("idReclamo", OracleDbType.Decimal,  idReclamo, ParameterDirection.Input);
            cmd.Parameters.Add("texto",     OracleDbType.Varchar2, texto,     ParameterDirection.Input);
            cmd.Parameters.Add("usuario",   OracleDbType.Varchar2, usuario,   ParameterDirection.Input);
            cmd.Parameters.Add("msgerror",  OracleDbType.Varchar2, 4000, ParameterDirection.Output);

            await cmd.ExecuteNonQueryAsync();
            var msg = OraOutStr(cmd, "msgerror");
            return string.IsNullOrWhiteSpace(msg) ? null : msg;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GuardarAnalisisCausaAsync({Id})", idReclamo);
            return $"Error al guardar Análisis de Causa: {ex.Message}";
        }
    }

    // ── P_GUARDAR_DECISION ───────────────────────────────────────────────────

    public async Task<string?> GuardarDecisionAsync(long idReclamo, string texto, string usuario)
    {
        try
        {
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"BEGIN {S}PKG_SGC_RECLAMO.P_GUARDAR_DECISION(:idReclamo,:texto,:usuario,:msgerror); END;";
            cmd.Parameters.Add("idReclamo", OracleDbType.Decimal,  idReclamo, ParameterDirection.Input);
            cmd.Parameters.Add("texto",     OracleDbType.Varchar2, texto,     ParameterDirection.Input);
            cmd.Parameters.Add("usuario",   OracleDbType.Varchar2, usuario,   ParameterDirection.Input);
            cmd.Parameters.Add("msgerror",  OracleDbType.Varchar2, 4000, ParameterDirection.Output);

            await cmd.ExecuteNonQueryAsync();
            var msg = OraOutStr(cmd, "msgerror");
            return string.IsNullOrWhiteSpace(msg) ? null : msg;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GuardarDecisionAsync({Id})", idReclamo);
            return $"Error al guardar Decisión: {ex.Message}";
        }
    }

    // ── NotificarCalidadAsync ────────────────────────────────────────────────
    // NOTA: No llama a P_NOTIFICAR_CALIDAD para evitar ORA-06502 en versiones
    //       antiguas del paquete Oracle. Hace el UPDATE directamente en C#.

    public async Task<(string? Destinatarios, string? AsuntoMail, string? NomCliente, string? Error)>
        NotificarCalidadAsync(long idReclamo, string usuario)
    {
        try
        {
            // Paso 1: Leer datos del reclamo (validación + datos para email)
            _logger.LogInformation("[Reclamo {Id}] Paso 1: leyendo datos del reclamo", idReclamo);
            var reclamo = await ObtenerReclamoAsync(idReclamo);
            if (reclamo == null)
            {
                _logger.LogError("[Reclamo {Id}] Paso 1 FALLO — reclamo no encontrado", idReclamo);
                return (null, null, null, $"El reclamo {idReclamo} no existe.");
            }
            _logger.LogInformation("[Reclamo {Id}] Paso 1 OK — estado={Estado} asunto={Asunto}",
                idReclamo, reclamo.Estado, reclamo.Asunto);

            // Paso 2: Validar estado ('01'=Abierto o '02'=En Revisión)
            _logger.LogInformation("[Reclamo {Id}] Paso 2: validando estado={Estado}", idReclamo, reclamo.Estado);
            if (reclamo.Estado is not ("01" or "02"))
            {
                var msgEstado = $"Solo se puede notificar a Calidad cuando el reclamo está Abierto o En Revisión. Estado actual: {reclamo.Estado}";
                _logger.LogWarning("[Reclamo {Id}] Paso 2 FALLO — {Msg}", idReclamo, msgEstado);
                return (null, null, null, msgEstado);
            }
            _logger.LogInformation("[Reclamo {Id}] Paso 2 OK — estado válido", idReclamo);

            // Paso 3: Cambiar estado a '02' (En Revisión) + marcar FCH_NOTI_CALIDAD
            //         Solo cambia estado si está en '01'; si ya es '02' solo actualiza la fecha.
            _logger.LogInformation("[Reclamo {Id}] Paso 3: cambiando estado a '02' y marcando FCH_NOTI_CALIDAD", idReclamo);
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();

            using var cmdUpd = conn.CreateCommand();
            cmdUpd.CommandText = $@"
                UPDATE {S}SGC_RECLAMO
                SET    ESTADO           = CASE WHEN ESTADO = '01' THEN '02' ELSE ESTADO END,
                       FCH_NOTI_CALIDAD = SYSDATE,
                       A_MDUSER         = :u,
                       A_MDFECHA        = SYSDATE
                WHERE  ID_RECLAMO = :id";
            cmdUpd.CommandType = System.Data.CommandType.Text;
            cmdUpd.Parameters.Add("u",  OracleDbType.Varchar2, usuario,   ParameterDirection.Input);
            cmdUpd.Parameters.Add("id", OracleDbType.Decimal,  idReclamo, ParameterDirection.Input);
            var filas = await cmdUpd.ExecuteNonQueryAsync();
            _logger.LogInformation("[Reclamo {Id}] Paso 3 OK — filas actualizadas={Filas} (estado ahora '02' si era '01')", idReclamo, filas);

            // Paso 4: Construir datos de notificación en C# (sin OUT params de Oracle)
            _logger.LogInformation("[Reclamo {Id}] Paso 4: construyendo datos de notificación", idReclamo);
            const string destinatarios = "vmatencio@colonial.com.pe";
            var asuntoRaw  = $"Nuevo reclamo #{idReclamo} - {reclamo.Asunto}";
            var asuntoMail = asuntoRaw.Length > 400 ? asuntoRaw[..400] : asuntoRaw;
            var nomCliente = reclamo.NomCliente ?? reclamo.CodCliente;
            if (nomCliente.Length > 200) nomCliente = nomCliente[..200];
            _logger.LogInformation("[Reclamo {Id}] Paso 4 OK — dest={Dest} asunto(40)={Asunto}",
                idReclamo, destinatarios, asuntoMail[..Math.Min(asuntoMail.Length, 40)]);

            // Paso 5: Enviar correos
            _logger.LogInformation("[Reclamo {Id}] Paso 5: enviando correos", idReclamo);
            var emails = destinatarios.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.Trim()).Where(e => !string.IsNullOrEmpty(e)).ToList();
            int enviados = 0, fallidos = 0;

            foreach (var email in emails)
            {
                try
                {
                    _logger.LogInformation("[Reclamo {Id}] Paso 5: preparando correo para {Email}", idReclamo, email);
                    var correoVendedor = await ObtenerCorreoVendedorAsync(reclamo.UsuVendedor);
                    var payload = new ReclamoEnviadoCalidadPayload
                    {
                        CorreoDestinatario = email,
                        NombreDestinatario = "Equipo de Calidad",
                        IdReclamo          = reclamo.IdReclamo.ToString(),
                        NombreCliente      = reclamo.NomCliente ?? reclamo.CodCliente,
                        RucCliente         = reclamo.RucCliente ?? "-",
                        Asunto             = reclamo.Asunto,
                        NombreVendedor     = reclamo.UsuVendedor,
                        CorreoVendedor     = correoVendedor,
                        FechaCreacion      = reclamo.FchCreacion.ToString("dd/MM/yyyy HH:mm:ss"),
                        Descripcion        = reclamo.Descripcion ?? ""
                    };
                    var ok = await _emailService.EnviarAsync(payload);
                    if (ok) { enviados++; _logger.LogInformation("[Reclamo {Id}] Paso 5: correo OK → {Email}", idReclamo, email); }
                    else    { fallidos++; _logger.LogWarning(    "[Reclamo {Id}] Paso 5: EmailService=false → {Email}", idReclamo, email); }
                }
                catch (Exception exMail)
                {
                    fallidos++;
                    _logger.LogError(exMail, "[Reclamo {Id}] Paso 5: excepción al enviar correo a {Email}", idReclamo, email);
                }
            }
            _logger.LogInformation("[Reclamo {Id}] Paso 5 OK — {Enviados} enviados, {Fallidos} fallidos",
                idReclamo, enviados, fallidos);

            return (destinatarios, asuntoMail, nomCliente, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Reclamo {Id}] Error en NotificarCalidadAsync", idReclamo);
            return (null, null, null, $"Error al notificar a calidad: {ex.Message}");
        }
    }

    // ── NotificarVendedorAprobadoAsync ──────────────────────────────────────
    // NOTA: No llama a P_NOTIFICAR_VENDEDOR_APROBADO para evitar ORA-06502 en
    //       versiones antiguas del paquete. Hace el UPDATE directamente en C#.

    public async Task<(string? Destinatario, string? AsuntoMail, string? NomCliente, string? Error)>
        NotificarVendedorAprobadoAsync(long idReclamo, string usuario)
    {
        try
        {
            // Paso 1: Leer datos del reclamo
            _logger.LogInformation("[NotifVendedor {Id}] Paso 1: leyendo datos del reclamo", idReclamo);
            var reclamo = await ObtenerReclamoAsync(idReclamo);
            if (reclamo == null)
            {
                _logger.LogError("[NotifVendedor {Id}] Paso 1 FALLO — reclamo no encontrado", idReclamo);
                return (null, null, null, $"El reclamo {idReclamo} no existe.");
            }
            _logger.LogInformation("[NotifVendedor {Id}] Paso 1 OK — estado={Estado}", idReclamo, reclamo.Estado);

            // Paso 2: Validar estado = '04' Aprobado
            _logger.LogInformation("[NotifVendedor {Id}] Paso 2: validando estado={Estado}", idReclamo, reclamo.Estado);
            if (reclamo.Estado != "04")
            {
                var msgEstado = $"Solo se puede notificar al vendedor cuando el reclamo está Aprobado. Estado actual: {reclamo.Estado}";
                _logger.LogWarning("[NotifVendedor {Id}] Paso 2 FALLO — {Msg}", idReclamo, msgEstado);
                return (null, null, null, msgEstado);
            }
            _logger.LogInformation("[NotifVendedor {Id}] Paso 2 OK", idReclamo);

            // Paso 3: Marcar FCH_NOTI_VEND directamente en C# (sin SP Oracle)
            _logger.LogInformation("[NotifVendedor {Id}] Paso 3: actualizando FCH_NOTI_VEND en BD", idReclamo);
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();

            using var cmdUpd = conn.CreateCommand();
            cmdUpd.CommandText = $@"
                UPDATE {S}SGC_RECLAMO
                SET    FCH_NOTI_VEND = SYSDATE,
                       A_MDUSER      = :u,
                       A_MDFECHA     = SYSDATE
                WHERE  ID_RECLAMO = :id";
            cmdUpd.CommandType = System.Data.CommandType.Text;
            cmdUpd.Parameters.Add("u",  OracleDbType.Varchar2, usuario,   ParameterDirection.Input);
            cmdUpd.Parameters.Add("id", OracleDbType.Decimal,  idReclamo, ParameterDirection.Input);
            var filas = await cmdUpd.ExecuteNonQueryAsync();
            _logger.LogInformation("[NotifVendedor {Id}] Paso 3 OK — filas={Filas}", idReclamo, filas);

            // Paso 4: Construir datos de notificación en C#
            _logger.LogInformation("[NotifVendedor {Id}] Paso 4: construyendo datos de notificación", idReclamo);
            var destinatario = await ObtenerCorreoVendedorAsync(reclamo.UsuVendedor);
            if (string.IsNullOrWhiteSpace(destinatario))
                destinatario = "vmatencio@colonial.com.pe";   // fallback de pruebas
            var asuntoRaw  = reclamo.Asunto;
            var asuntoMail = asuntoRaw.Length > 400 ? asuntoRaw[..400] : asuntoRaw;
            var nomCliente = reclamo.NomCliente ?? reclamo.CodCliente;
            if (nomCliente.Length > 200) nomCliente = nomCliente[..200];
            _logger.LogInformation("[NotifVendedor {Id}] Paso 4 OK — dest={Dest}", idReclamo, destinatario);

            // Paso 5: Enviar correo al vendedor
            _logger.LogInformation("[NotifVendedor {Id}] Paso 5: enviando correo a {Dest}", idReclamo, destinatario);
            if (destinatario.Contains('@'))
            {
                var urlPortal = GetUrlPortal(reclamo.IdReclamo);
                var payload = new ReclamoEvaluadoVendedorPayload
                {
                    CorreoDestinatario = destinatario,
                    NombreDestinatario = reclamo.UsuVendedor,
                    IdReclamo          = reclamo.IdReclamo.ToString(),
                    NombreCliente      = reclamo.NomCliente ?? reclamo.CodCliente,
                    RucCliente         = reclamo.RucCliente ?? "-",
                    Asunto             = reclamo.Asunto,
                    FechaCreacion      = reclamo.FchCreacion.ToString("dd/MM/yyyy HH:mm:ss"),
                    DecisionFinal      = reclamo.DecisionFinal ?? "",
                    NombreAnalista     = reclamo.UsuAnalista ?? "",
                    NombreGerente      = reclamo.UsuGerente ?? "",
                    FechaAprobacion    = reclamo.FchAprobacion?.ToString("dd/MM/yyyy HH:mm:ss") ?? "",
                    UrlPortal          = urlPortal
                };
                var enviado = await _emailService.EnviarAsync(payload);
                if (enviado) _logger.LogInformation("[NotifVendedor {Id}] Paso 5 OK — correo enviado a {Dest}", idReclamo, destinatario);
                else         _logger.LogWarning(    "[NotifVendedor {Id}] Paso 5 — EmailService=false para {Dest}", idReclamo, destinatario);
            }
            else
            {
                _logger.LogWarning("[NotifVendedor {Id}] Paso 5 — destinatario sin '@', correo omitido: {Dest}", idReclamo, destinatario);
            }

            return (destinatario, asuntoMail, nomCliente, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NotifVendedor {Id}] Error en NotificarVendedorAprobadoAsync", idReclamo);
            return (null, null, null, $"Error al notificar al vendedor: {ex.Message}");
        }
    }

    // ── ObtenerDatosImpresionAsync ──────────────────────────────────────────

    // Código de firma hardcodeado — cambiar cuando se requiera lógica dinámica.
    private const string CodigoFirmaGerencia = "034001";

    public async Task<ReclamoImpresionDto?> ObtenerDatosImpresionAsync(long idReclamo)
    {
        try
        {
            var reclamo = await ObtenerReclamoAsync(idReclamo);
            if (reclamo == null) return null;

            if (reclamo.Estado != "04") return null;

            var descargos = await ObtenerDescargosAsync(idReclamo);
            var archivos  = await ObtenerArchivosAsync(idReclamo);

            // ── Leer datos del firmante desde RH_PERSONAS / RH_PERSONAL / T_CARGO ──
            byte[]?  firmaBytes      = null;
            string?  nombreCompleto  = null;
            string?  cargo           = null;

            try
            {
                await using var connF = new OracleConnection(GetOracleConnectionString());
                await connF.OpenAsync();

                // 1. Nombre completo + cargo
                var sqlPer = $@"
                    SELECT ps.APELLIDO_PATERNO || ' ' || ps.APELLIDO_MATERNO
                           || ', ' || ps.NOMBRES AS NOMBRE_COMPLETO,
                           NVL(tc.DESCRIPCION, '') AS CARGO
                    FROM   {S}RH_PERSONAS ps
                    JOIN   {S}RH_PERSONAL pr  ON pr.C_CODIGO = ps.C_CODIGO
                    LEFT JOIN {S}T_CARGO  tc  ON tc.C_CARGO  = pr.C_CARGO
                    WHERE  ps.C_CODIGO = :cod
                    AND    ROWNUM = 1";
                await using (var cmdP = new OracleCommand(sqlPer, connF) { BindByName = true })
                {
                    cmdP.Parameters.Add("cod", OracleDbType.Varchar2, 20).Value = CodigoFirmaGerencia;
                    await using var rp = (OracleDataReader)await cmdP.ExecuteReaderAsync();
                    if (await rp.ReadAsync())
                    {
                        nombreCompleto = rp["NOMBRE_COMPLETO"] == DBNull.Value ? null : rp["NOMBRE_COMPLETO"].ToString()?.Trim();
                        cargo          = rp["CARGO"]           == DBNull.Value ? null : rp["CARGO"].ToString()?.Trim();
                    }
                }

                // 2. Imagen de firma desde RH_FIRMAS
                await using var cmdF = new OracleCommand(
                    $"SELECT FIRMA FROM {S}RH_FIRMAS WHERE C_CODIGO = :cod", connF)
                {
                    InitialLONGFetchSize = -1
                };
                cmdF.Parameters.Add("cod", OracleDbType.Varchar2, 20).Value = CodigoFirmaGerencia;

                await using var rdr = (OracleDataReader)await cmdF.ExecuteReaderAsync();
                if (await rdr.ReadAsync() && !rdr.IsDBNull(0))
                {
                    var val = rdr.GetValue(0);
                    byte[]? raw = val is byte[] b && b.Length > 0 ? b
                               : val is OracleBinary ob && !ob.IsNull ? ob.Value
                               : null;

                    if (raw != null && raw.Length > 0)
                    {
                        var mime = OrdenCompraService.DetectImageMimeType(raw);
                        if (mime == "image/tiff")
                            raw = OrdenCompraService.ConvertirTiffAPng(raw);
                        if (raw != null && raw.Length > 0)
                            firmaBytes = raw;
                    }
                }
            }
            catch (Exception exF)
            {
                _logger.LogWarning(exF, "No se pudo leer firma/datos para código {Cod}", CodigoFirmaGerencia);
            }

            return new ReclamoImpresionDto
            {
                Reclamo               = reclamo,
                Descargos             = descargos,
                Archivos              = archivos,
                NombreGerenteAprobador = reclamo.UsuGerente,
                NombreCompletoGerente = nombreCompleto,
                CargoGerente          = cargo,
                FirmaGerente          = firmaBytes
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en ObtenerDatosImpresionAsync({Id})", idReclamo);
            return null;
        }
    }



    private static ReclamoDto MapReclamo(OracleDataReader r)
    {
        var dto = new ReclamoDto
        {
            IdReclamo    = Long(r, "ID_RECLAMO"),
            CodCliente   = Str(r, "COD_CLIENTE")  ?? "",
            RucCliente   = Str(r, "RUC_CLIENTE"),
            NomCliente   = Str(r, "NOM_CLIENTE"),
            Contacto     = Str(r, "CONTACTO")     ?? "",
            Telefono     = Str(r, "TELEFONO")      ?? "",
            Asunto       = Str(r, "ASUNTO")        ?? "",
            Estado       = Str(r, "ESTADO")        ?? "01",
            UsuVendedor  = Str(r, "USU_VENDEDOR")  ?? "",
            FchCreacion  = Dt(r,  "FCH_CREACION"),
            UsuAnalista    = Str(r, "USU_ANALISTA"),
            FchAnalisis    = NullDt(r, "FCH_ANALISIS"),
            UsuGerente     = Str(r, "USU_GERENTE"),
            FchAprobacion  = NullDt(r, "FCH_APROBACION"),
            MotRechazo     = Str(r, "MOT_RECHAZO"),
            AnalisisCausa  = Str(r, "ANALISIS_CAUSA"),
            DecisionFinal  = Str(r, "DECISION_FINAL"),
            FchDecision    = NullDt(r, "FCH_DECISION"),
            UsuDecision    = Str(r, "USU_DECISION"),
            FchNotiCalidad = NullDt(r, "FCH_NOTI_CALIDAD"),
            FchNotiVend    = NullDt(r, "FCH_NOTI_VEND")
        };

        // Contadores opcionales (presentes en el listado, no en detalle individual)
        if (HasColumn(r, "TOTAL_DESCARGOS")) dto.TotalDescargos = Int(r, "TOTAL_DESCARGOS");
        if (HasColumn(r, "TOTAL_ARCHIVOS"))  dto.TotalArchivos  = Int(r, "TOTAL_ARCHIVOS");

        // Descripción del primer descargo del vendedor (solo en P_OBTENER_RECLAMO)
        if (HasColumn(r, "DESCRIPCION")) dto.Descripcion = Str(r, "DESCRIPCION");

        return dto;
    }

    private static ReclamoArchivoDto MapArchivo(OracleDataReader r) => new()
    {
        IdArchivo    = Long(r, "ID_ARCHIVO"),
        IdReclamo    = Long(r, "ID_RECLAMO"),
        Rol          = Str(r, "ROL")           ?? "",
        NombreOrig   = Str(r, "NOMBRE_ORIG")   ?? "",
        NombreServer = Str(r, "NOMBRE_SERVER") ?? "",
        MimeType     = Str(r, "MIME_TYPE"),
        TamanioBytes = r["TAMANIO_BYTES"] == DBNull.Value ? 0L : Convert.ToInt64(r["TAMANIO_BYTES"]),
        Usuario      = Str(r, "USUARIO")       ?? "",
        FchCarga     = Dt(r,  "FCH_CARGA")
    };

    private static bool HasColumn(OracleDataReader r, string name)
    {
        for (int i = 0; i < r.FieldCount; i++)
            if (string.Equals(r.GetName(i), name, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers para notificaciones
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<string> ObtenerCorreoVendedorAsync(string usuVendedor)
    {
        if (string.IsNullOrWhiteSpace(usuVendedor)) return "";

        try
        {
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();

            // Intento 1: CS_USER.C_EMAIL (el más actualizado)
            using var cmd1 = conn.CreateCommand();
            cmd1.CommandText = $@"
                SELECT U.C_EMAIL
                FROM   {S}CS_USER U
                WHERE  U.C_USER  = :usuario
                  AND  U.ESTADO  = '1'
                  AND  U.C_EMAIL IS NOT NULL";
            cmd1.Parameters.Add("usuario", OracleDbType.Varchar2, usuVendedor, ParameterDirection.Input);

            using var r1 = await cmd1.ExecuteReaderAsync();
            if (await r1.ReadAsync())
            {
                var email = r1[0] == DBNull.Value ? null : r1[0].ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(email))
                {
                    _logger.LogInformation("[ObtenerCorreoVendedor] Email de CS_USER para {Usuario}: {Email}", usuVendedor, email);
                    return email;
                }
            }
            r1.Close();

            // Intento 2: CS_ANEXO.EMAIL vinculado por C_CODIGO
            using var cmd2 = conn.CreateCommand();
            cmd2.CommandText = $@"
                SELECT AN.EMAIL
                FROM   {S}CS_USER  U
                JOIN   {S}CS_ANEXO AN ON AN.C_CODIGO = U.C_CODIGO
                WHERE  U.C_USER  = :usuario
                  AND  U.ESTADO  = '1'
                  AND  AN.EMAIL  IS NOT NULL";
            cmd2.Parameters.Add("usuario", OracleDbType.Varchar2, usuVendedor, ParameterDirection.Input);

            using var r2 = await cmd2.ExecuteReaderAsync();
            if (await r2.ReadAsync())
            {
                var email = r2[0] == DBNull.Value ? null : r2[0].ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(email))
                {
                    _logger.LogInformation("[ObtenerCorreoVendedor] Email de CS_ANEXO para {Usuario}: {Email}", usuVendedor, email);
                    return email;
                }
            }
            r2.Close();

            _logger.LogWarning("[ObtenerCorreoVendedor] No se encontró email para {Usuario}, usando placeholder", usuVendedor);
            return $"{usuVendedor.ToLower()}@lacolonial.com.pe";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ObtenerCorreoVendedor] Error consultando email de {Usuario}", usuVendedor);
            return "";
        }
    }

    private string GetUrlPortal(long idReclamo)
    {
        try
        {
            var httpContext = _httpContextAccessor?.HttpContext;
            if (httpContext == null) return "";

            var scheme   = httpContext.Request.Scheme;
            var host     = httpContext.Request.Host;
            var baseUrl  = $"{scheme}://{host}";

            return $"{baseUrl}/Sgc/Reclamos/Detalle/{idReclamo}";
        }
        catch
        {
            return "";
        }
    }
}


