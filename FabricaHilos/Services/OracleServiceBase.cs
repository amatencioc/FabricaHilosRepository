using Oracle.ManagedDataAccess.Client;
using Microsoft.Extensions.Logging;

namespace FabricaHilos.Services;

/// <summary>
/// Se lanza cuando Oracle rechaza las credenciales del usuario logueado (ORA-01017).
/// El controlador debe capturarla y redirigir al login con un mensaje explicativo.
/// Ocurre cuando la contraseña en CS_USER (psw_sig) difiere de la contraseña
/// del usuario en la base de datos Oracle (ej: fue cambiada con ALTER USER desde Toad).
/// </summary>
public sealed class OracleCredencialesInvalidasException : Exception
{
    public string OracleUser { get; }
    public OracleCredencialesInvalidasException(string oracleUser)
        : base($"Las credenciales Oracle del usuario '{oracleUser}' son inválidas (ORA-01017). Verifique que la contraseña en CS_USER.psw_sig coincida con la contraseña del usuario en la base de datos Oracle.")
    {
        OracleUser = oracleUser;
    }
}

/// <summary>
/// Clase base para todos los servicios que ejecutan queries Oracle.
/// Centraliza:
///   - GetOracleConnectionString(): conexión dinámica según el usuario logueado.
///   - S (propiedad): prefijo de esquema Oracle según la empresa.
///       LaColonial → "SIG."
///       Arbona     → "ARBONA."
///       Solsa      → "SOLSA."
/// Para referenciar una tabla en un query, usar simplemente: $"{S}TABLA"
/// </summary>
public abstract class OracleServiceBase
{
    protected readonly IConfiguration        _configuration;
    protected readonly IHttpContextAccessor  _httpContextAccessor;
    private   readonly string                _fallbackConnectionString;

    protected OracleServiceBase(
        IConfiguration       configuration,
        IHttpContextAccessor httpContextAccessor)
    {
        _configuration           = configuration;
        _httpContextAccessor     = httpContextAccessor;
        _fallbackConnectionString = configuration.GetConnectionString("LaColonialConnection")
            ?? throw new InvalidOperationException("LaColonialConnection not found in configuration.");
    }

    // ── Clave de empresa activa ────────────────────────────────────────────────

    private string GetEmpresaConnKey()
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        return session?.GetString("EmpresaConexion") ?? "LaColonialConnection";
    }

    // ── Conexión dinámica ──────────────────────────────────────────────────────

    /// <summary>
    /// Devuelve el connection string de la empresa activa.
    /// Siempre usa las credenciales de la aplicación (SIG/STARK) definidas en appsettings.
    /// El usuario logueado (CS_USER) es solo para autenticación en la app, no en Oracle.
    /// </summary>
    protected string GetOracleConnectionString()
    {
        var connKey = GetEmpresaConnKey();
        return _configuration.GetConnectionString(connKey) ?? _fallbackConnectionString;
    }

    // ── Apertura de conexión ──────────────────────────────────────────────────

    /// <summary>
    /// Abre la conexión Oracle con las credenciales de la aplicación.
    /// </summary>
    protected async Task<OracleConnection> AbrirConexionAsync()
    {
        var conn = new OracleConnection(GetOracleConnectionString());
        try
        {
            await conn.OpenAsync();
            return conn;
        }
        catch
        {
            await conn.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Devuelve el connection string con las credenciales BASE de la aplicación (no del usuario logueado).
    /// Usar para consultas SELECT de solo lectura donde el usuario Oracle puede tener VPD/RLS
    /// que filtraría sus propias filas (ej: listado de producción).
    /// Para DML (INSERT/UPDATE/DELETE) seguir usando GetOracleConnectionString().
    /// </summary>
    protected string GetBaseOracleConnectionString()
    {
        var connKey = GetEmpresaConnKey();
        return _configuration.GetConnectionString(connKey) ?? _fallbackConnectionString;
    }

    // ── Prefijo de esquema Oracle ──────────────────────────────────────────────
    // Uso en queries: $"{S}TABLA", $"{S}VISTA", $"{S}SECUENCIA.NEXTVAL"

    /// <summary>
    /// Prefijo del esquema Oracle según la empresa del usuario logueado.
    /// LaColonial → "SIG."   |   Arbona → "ARBONA."   |   Solsa → "SOLSA."
    /// </summary>
    protected string S => GetEmpresaConnKey() switch
    {
        "ArbonaConnection" => "ARBONA.",
        "SolsaConnection"  => "SOLSA.",
        _                  => "SIG."
    };

    // ── Mapeo de empresa → CodEmpresa para sistemas externos (Aquarius, etc.) ──
    // Centralizado aquí para que no esté en appsettings ni duplicado.
    // Al agregar una nueva empresa, solo se actualiza este diccionario.

    private static readonly Dictionary<string, string> _aquariusCodEmpresa = new()
    {
        { "LaColonialConnection", "0003" },
        { "ArbonaConnection",     "0001" },
        { "SolsaConnection",      "0002" },
    };

    /// <summary>
    /// Ejecuta una función Oracle y devuelve su resultado.
    /// Captura ORA-00942 (tabla/vista inexistente) registrando un warning y devolviendo
    /// el valor por defecto, para evitar que dashboards rompan cuando el módulo aún no está
    /// activado en el esquema. Todas las demás excepciones propagan normalmente.
    /// </summary>
    protected async Task<TResult> EjecutarConManejoAsync<TResult>(
        Func<Task<TResult>> operacion,
        TResult valorPorDefecto,
        string nombreVista,
        ILogger logger)
    {
        try
        {
            return await operacion();
        }
        catch (OracleException ex) when (ex.Number == 942)
        {
            logger.LogWarning(
                "[{Servicio}] {Vista} no existe en el esquema {Esquema}. Ejecute el script de activación del módulo.",
                GetType().Name, nombreVista, S);
            return valorPorDefecto;
        }
    }

    /// <summary>
    /// Sobrecarga para colecciones: devuelve lista vacía como valor por defecto.
    /// </summary>
    protected Task<IEnumerable<T>> EjecutarListaAsync<T>(
        Func<Task<IEnumerable<T>>> operacion,
        string nombreVista,
        ILogger logger)
        => EjecutarConManejoAsync(operacion, Enumerable.Empty<T>(), nombreVista, logger);

    /// <summary>
    /// Retorna el CodEmpresa de Aquarius según la clave de conexión.
    /// Centralizado para que sea el único lugar a modificar al agregar empresas.
    /// </summary>
    public static string GetCodEmpresaAquarius(string connKey) =>
        _aquariusCodEmpresa.TryGetValue(connKey, out var cod) ? cod : "0003";

    }
