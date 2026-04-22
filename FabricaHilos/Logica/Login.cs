using System;
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
        private readonly string _conexion;
        private readonly ILogger _logger;
        private const int TimeoutSegundos = 8;

        public Login(IConfiguration configuration, ILogger logger = null)
        {
            // El UNION siempre se ejecuta contra LaColonialConnection porque
            // tiene acceso a ARBONA.CS_USER mediante database link.
            _conexion = configuration.GetConnectionString("LaColonialConnection");
            _logger = logger;
        }

        /// <summary>
        /// Busca el usuario en ambas empresas mediante UNION.
        /// El campo EMPRESA del resultado indica a qué base de datos pertenece.
        /// </summary>
        public async Task<Usuario> EncontrarUsuarioAsync(string usu, string psw)
        {
            var objeto = new Usuario();

            try
            {
                _logger?.LogInformation("🔍 Login Oracle multi-empresa — Usuario: {Usuario}", usu);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSegundos));
                using var oconexion = new OracleConnection(_conexion);

                await oconexion.OpenAsync(cts.Token);

                // UNION entre las dos empresas. El campo EMPRESA indica el origen.
                // Se filtra aquí por usuario y contraseña para no traer datos innecesarios.
                const string query = @"
                    SELECT c_user, c_codigo, c_nombre, c_costo, acceso_web, EMPRESA
                    FROM (
                        SELECT c_user, c_codigo, c_nombre, c_costo, acceso_web, 'COLONIAL' AS EMPRESA
                        FROM CS_USER
                        WHERE c_user = :puser AND psw_sig = :ppsw
                        UNION
                        SELECT c_user, c_codigo, c_nombre, c_costo, acceso_web, 'ARBONA' AS EMPRESA
                        FROM ARBONA.CS_USER
                        WHERE c_user = :puser AND psw_sig = :ppsw
                    )";

                using var cmd = new OracleCommand(query, oconexion);
                cmd.BindByName = true;
                cmd.Parameters.Add(new OracleParameter("puser", usu));
                cmd.Parameters.Add(new OracleParameter("ppsw",  psw));
                cmd.CommandType    = CommandType.Text;
                cmd.CommandTimeout = TimeoutSegundos;

                using var dr = await cmd.ExecuteReaderAsync(cts.Token);

                if (await dr.ReadAsync(cts.Token))
                {
                    objeto.c_user     = dr["c_user"]?.ToString();
                    objeto.c_codigo   = dr["c_codigo"]?.ToString();
                    objeto.c_nombre   = dr["c_nombre"]?.ToString();
                    objeto.c_costo    = dr["c_costo"]?.ToString();
                    objeto.acceso_web = dr["acceso_web"]?.ToString();
                    objeto.psw_sig    = psw;
                    objeto.Empresa    = dr["EMPRESA"]?.ToString();  // "COLONIAL" o "ARBONA"

                    _logger?.LogInformation(
                        "✅ Usuario encontrado: {CUser} — Empresa: {Empresa}",
                        objeto.c_user, objeto.Empresa);
                }
                else
                {
                    _logger?.LogWarning(
                        "❌ Usuario '{Usuario}' no encontrado o contraseña incorrecta en ninguna empresa.",
                        usu);
                }
            }
            catch (OperationCanceledException)
            {
                _logger?.LogError(
                    "⏱ Timeout ({Seg}s) al conectar con Oracle para autenticar usuario {Usuario}.",
                    TimeoutSegundos, usu);
                objeto = new Usuario();
            }
            catch (OracleException oex)
            {
                _logger?.LogError(
                    "❌ ERROR Oracle al autenticar {Usuario}: ORA-{Codigo} {Message}",
                    usu, oex.Number, oex.Message);
                objeto = new Usuario();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ ERROR inesperado al autenticar usuario {Usuario}.", usu);
                objeto = new Usuario();
            }

            return objeto;
        }

        /// <summary>
        /// Versión sincrónica mantenida por compatibilidad. Preferir EncontrarUsuarioAsync.
        /// </summary>
        public Usuario EncontrarUsuario(string usu, string psw)
            => EncontrarUsuarioAsync(usu, psw).GetAwaiter().GetResult();
    }
}
