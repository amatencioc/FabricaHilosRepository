using Oracle.ManagedDataAccess.Client;
using System.Data;
using FabricaHilos.Models.Sgc;

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
}

// ────────────────────────────────────────────────────────────────────────────
//  IMPLEMENTACIÓN
// ────────────────────────────────────────────────────────────────────────────

public class AnalisisReclamoService : OracleServiceBase, IAnalisisReclamoService
{
    private readonly ILogger<AnalisisReclamoService> _logger;

    public AnalisisReclamoService(
        IConfiguration       configuration,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AnalisisReclamoService> logger)
        : base(configuration, httpContextAccessor)
    {
        _logger = logger;
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
                    NomCliente = Str(r, "NOM_CLIENTE") ?? ""
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

    public async Task<(long IdArchivo, string? Error)> RegistrarArchivoAsync(
        long idReclamo, string rol,
        string nombreOrig, string nombreServer,
        string mimeType, long tamanio, string usuario)
    {
        try
        {
            await using var conn = new OracleConnection(GetOracleConnectionString());
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                BEGIN {S}PKG_SGC_RECLAMO.P_REGISTRAR_ARCHIVO(
                    :idReclamo, :rol,
                    :nombreOrig, :nombreServer,
                    :mimeType, :tamanio, :usuario,
                    :idArchivo, :msgerror
                ); END;";

            cmd.Parameters.Add("idReclamo",    OracleDbType.Decimal,  idReclamo,    ParameterDirection.Input);
            cmd.Parameters.Add("rol",          OracleDbType.Varchar2, rol,          ParameterDirection.Input);
            cmd.Parameters.Add("nombreOrig",   OracleDbType.Varchar2, nombreOrig,   ParameterDirection.Input);
            cmd.Parameters.Add("nombreServer", OracleDbType.Varchar2, nombreServer, ParameterDirection.Input);
            cmd.Parameters.Add("mimeType",     OracleDbType.Varchar2, mimeType,     ParameterDirection.Input);
            cmd.Parameters.Add("tamanio",      OracleDbType.Decimal,  tamanio,      ParameterDirection.Input);
            cmd.Parameters.Add("usuario",      OracleDbType.Varchar2, usuario,      ParameterDirection.Input);
            cmd.Parameters.Add("idArchivo",    OracleDbType.Decimal,  ParameterDirection.Output);
            cmd.Parameters.Add("msgerror",     OracleDbType.Varchar2, 4000, ParameterDirection.Output);

            await cmd.ExecuteNonQueryAsync();

            var msgerror = OraOutStr(cmd, "msgerror");
            if (!string.IsNullOrWhiteSpace(msgerror)) return (0, msgerror);

            long id = Convert.ToInt64(cmd.Parameters["idArchivo"].Value.ToString());
            return (id, null);
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



    private static ReclamoDto MapReclamo(OracleDataReader r)
    {
        var dto = new ReclamoDto
        {
            IdReclamo    = Long(r, "ID_RECLAMO"),
            CodCliente   = Str(r, "COD_CLIENTE")  ?? "",
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
            MotRechazo     = Str(r, "MOT_RECHAZO")
        };

        // Contadores opcionales (presentes en el listado, no en detalle individual)
        if (HasColumn(r, "TOTAL_DESCARGOS")) dto.TotalDescargos = Int(r, "TOTAL_DESCARGOS");
        if (HasColumn(r, "TOTAL_ARCHIVOS"))  dto.TotalArchivos  = Int(r, "TOTAL_ARCHIVOS");

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
}
