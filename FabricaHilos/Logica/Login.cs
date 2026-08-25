using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using FabricaHilos.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FabricaHilos.Logica
{
    /// <summary>
    /// Autenticación multi-empresa contra Oracle.
    /// Ejecuta un UNION sobre CS_USER de LaColonial y ARBONA.CS_USER,
    /// filtra por usuario+contraseña y devuelve la empresa a la que pertenece.
    /// </summary>
    public class Login
    {
        private readonly string? _conexion;
        private readonly ILogger? _logger;
        private const int TimeoutSegundos = 8;

        public Login(IConfiguration configuration, ILogger? logger = null)
        {
            // El UNION siempre se ejecuta contra LaColonialConnection porque
            // tiene acceso a ARBONA.CS_USER mediante database link.
            _conexion = configuration.GetConnectionString("LaColonialConnection");
            _logger = logger;
        }

        /// <summary>
        /// Busca el usuario en las tres empresas de forma secuencial (COLONIAL → ARBONA → SOLSA).
        /// Se consulta cada empresa por separado para aislar fallos de esquema o enlace de BD.
        /// El campo EMPRESA del resultado indica a qué base de datos pertenece.
        /// </summary>
        public async Task<Usuario> EncontrarUsuarioAsync(string usu, string psw)
        {
            // Normalización defensiva: Oracle CS_USER siempre en mayúsculas, sin espacios
            usu = (usu ?? string.Empty).Trim().ToUpperInvariant();
            psw = (psw ?? string.Empty).Trim();

            _logger?.LogInformation("🔍 Login Oracle multi-empresa — Usuario: {Usuario}", usu);

            // Definición de cada empresa: (nombre, prefijo de tabla, alias)
            var empresas = new[]
            {
                (Nombre: "COLONIAL", Tabla: "CS_USER"),
                (Nombre: "ARBONA",   Tabla: "ARBONA.CS_USER"),
                (Nombre: "SOLSA",    Tabla: "SOLSA.CS_USER"),
            };

            using var oconexion = new OracleConnection(_conexion);
            try
            {
                using var ctsCon = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSegundos));
                await oconexion.OpenAsync(ctsCon.Token);
            }
            catch (OperationCanceledException)
            {
                _logger?.LogError("⏱ Timeout ({Seg}s) abriendo conexión Oracle para autenticar {Usuario}.", TimeoutSegundos, usu);
                return new Usuario();
            }
            catch (OracleException oex)
            {
                _logger?.LogError("❌ ERROR Oracle al abrir conexión para {Usuario}: ORA-{Codigo} {Message}", usu, oex.Number, oex.Message);
                return new Usuario();
            }

            foreach (var (nombre, tabla) in empresas)
            {
                try
                {
                    // Si existen filas duplicadas (mismo c_user / misma clave) en CS_USER,
                    // NO se debe descartar ninguna arbitrariamente: el antiguo ORDER BY
                    // acceso_web DESC + ROWNUM=1 perdía silenciosamente tokens de ACCESO_WEB
                    // cuando otra fila duplicada ordenaba alfabéticamente más alto
                    // (p.ej. "Seguridad" > "RhAutorizacionHorasAutorizar"). Ahora se traen
                    // TODAS las filas activas y se fusionan los tokens distintos de
                    // ACCESO_WEB de todas ellas.
                    var query = $@"
                        SELECT c_user, c_codigo, c_nombre, c_costo, acceso_web
                        FROM {tabla}
                        WHERE ESTADO <> '9'
                        AND c_user = :puser AND TRIM(psw_sig) = :ppsw";

                    using var cmd = new OracleCommand(query, oconexion);
                    cmd.BindByName     = true;
                    cmd.CommandType    = CommandType.Text;
                    cmd.CommandTimeout = TimeoutSegundos;
                    cmd.Parameters.Add(new OracleParameter("puser", usu));
                    cmd.Parameters.Add(new OracleParameter("ppsw",  psw));

                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSegundos));
                    using var dr  = await cmd.ExecuteReaderAsync(cts.Token);

                    Usuario? objeto = null;
                    var tokensFusionados = new List<string>();
                    var tokensVistos     = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    while (await dr.ReadAsync(cts.Token))
                    {
                        objeto ??= new Usuario
                        {
                            c_user   = dr["c_user"].ToString(),
                            c_codigo = dr["c_codigo"].ToString(),
                            c_nombre = dr["c_nombre"].ToString(),
                            c_costo  = dr["c_costo"].ToString(),
                            psw_sig  = psw,
                            Empresa  = nombre,
                        };

                        foreach (var token in SplitTokens(dr["acceso_web"].ToString()))
                        {
                            if (tokensVistos.Add(token))
                                tokensFusionados.Add(token);
                        }
                    }

                    if (objeto != null)
                    {
                        objeto.acceso_web = string.Join(",", tokensFusionados);
                        _logger?.LogInformation(
                            "✅ Usuario encontrado: {CUser} — Empresa: {Empresa}",
                            objeto.c_user, objeto.Empresa);
                        return objeto;
                    }

                    _logger?.LogDebug("ℹ️ Usuario '{Usuario}' no encontrado en {Empresa}.", usu, nombre);
                }
                catch (OperationCanceledException)
                {
                    _logger?.LogWarning("⏱ Timeout ({Seg}s) consultando {Empresa} para {Usuario}.", TimeoutSegundos, nombre, usu);
                }
                catch (OracleException oex)
                {
                    _logger?.LogWarning(
                        "⚠️ ERROR Oracle al consultar {Empresa} para {Usuario}: ORA-{Codigo} {Message}",
                        nombre, usu, oex.Number, oex.Message);
                }
            }

            _logger?.LogWarning("❌ Usuario '{Usuario}' no encontrado o contraseña incorrecta en ninguna empresa.", usu);
            return new Usuario();
        }

        /// <summary>
        /// Versión sincrónica mantenida por compatibilidad. Preferir EncontrarUsuarioAsync.
        /// </summary>
        public Usuario EncontrarUsuario(string usu, string psw)
            => EncontrarUsuarioAsync(usu, psw).GetAwaiter().GetResult();

        /// <summary>
        /// Separa un valor ACCESO_WEB por comas respetando corchetes de modificadores
        /// (ej: "Produccion[noNuevaOC],Sgc" -> ["Produccion[noNuevaOC]", "Sgc"]),
        /// igual que MenuService.ObtenerTokens, para poder fusionar tokens de filas
        /// duplicadas sin romper tokens con par\u00E1metros.
        /// </summary>
        private static IEnumerable<string> SplitTokens(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) yield break;

            var depth   = 0;
            var current = new System.Text.StringBuilder();
            foreach (var ch in raw)
            {
                if      (ch == '[') { depth++; current.Append(ch); }
                else if (ch == ']') { depth--; current.Append(ch); }
                else if (ch == ',' && depth == 0)
                {
                    var token = current.ToString().Trim();
                    if (token.Length > 0) yield return token;
                    current.Clear();
                }
                else current.Append(ch);
            }

            var last = current.ToString().Trim();
            if (last.Length > 0) yield return last;
        }
    }
}
