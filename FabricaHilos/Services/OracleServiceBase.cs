using Oracle.ManagedDataAccess.Client;

namespace FabricaHilos.Services;

/// <summary>
/// Clase base para todos los servicios que ejecutan queries Oracle.
/// Centraliza:
///   - GetOracleConnectionString(): conexión dinámica según el usuario logueado.
///   - S (propiedad): prefijo de esquema Oracle según la empresa.
///       LaColonial → "SIG."
///       Arbona     → "ARBONA."
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

    protected string GetOracleConnectionString()
    {
        var session  = _httpContextAccessor.HttpContext?.Session;
        var connKey  = session?.GetString("EmpresaConexion") ?? "LaColonialConnection";
        var baseConn = _configuration.GetConnectionString(connKey) ?? _fallbackConnectionString;

        var oraUser = session?.GetString("OracleUser");
        var oraPass = session?.GetString("OraclePass");

        if (!string.IsNullOrEmpty(oraUser) && !string.IsNullOrEmpty(oraPass))
        {
            var csb = new OracleConnectionStringBuilder(baseConn)
            {
                UserID   = oraUser,
                Password = oraPass
            };
            return csb.ToString();
        }

        return baseConn;
    }

    // ── Prefijo de esquema Oracle ──────────────────────────────────────────────
    // Uso en queries: $"{S}TABLA", $"{S}VISTA", $"{S}SECUENCIA.NEXTVAL"

    /// <summary>
    /// Prefijo del esquema Oracle según la empresa del usuario logueado.
    /// LaColonial → "SIG."   |   Arbona → "ARBONA."
    /// </summary>
    protected string S => GetEmpresaConnKey() switch
    {
        "ArbonaConnection" => "ARBONA.",
        _                  => "SIG."
    };

    // ── Mapeo de empresa → CodEmpresa para sistemas externos (Aquarius, etc.) ──
    // Centralizado aquí para que no esté en appsettings ni duplicado.
    // Al agregar una nueva empresa, solo se actualiza este diccionario.

    private static readonly Dictionary<string, string> _aquariusCodEmpresa = new()
    {
        { "LaColonialConnection", "0003" },
        { "ArbonaConnection",     "0001" },
    };

    /// <summary>
    /// Retorna el CodEmpresa de Aquarius según la clave de conexión.
    /// Centralizado para que sea el único lugar a modificar al agregar empresas.
    /// </summary>
    public static string GetCodEmpresaAquarius(string connKey) =>
        _aquariusCodEmpresa.TryGetValue(connKey, out var cod) ? cod : "0003";

    /// <summary>
    /// CodEmpresa de Aquarius según la empresa activa del usuario en sesión.
    /// </summary>
    protected string CodEmpresaAquarius => GetCodEmpresaAquarius(GetEmpresaConnKey());
}
