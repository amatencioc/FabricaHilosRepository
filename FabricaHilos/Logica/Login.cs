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
    /// Clase de lógica para autenticación con Oracle Database
    /// </summary>
    public class Login
    {
        private readonly string _conexion;
        private readonly ILogger _logger;
        private const int TimeoutSegundos = 8; // Timeout para evitar bloqueos en red móvil lenta

        public Login(IConfiguration configuration, ILogger logger = null)
        {
            _conexion = configuration.GetConnectionString("OracleConnection");
            _logger = logger;
        }

        /// <summary>
        /// Busca y valida un usuario en la base de datos Oracle de forma asíncrona.
        /// </summary>
        public async Task<Usuario> EncontrarUsuarioAsync(string usu, string psw)
        {
            var objeto = new Usuario();

            try
            {
                _logger?.LogInformation("🔍 Login Oracle — Usuario: {Usuario}", usu);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSegundos));
                using var oconexion = new OracleConnection(_conexion);

                await oconexion.OpenAsync(cts.Token);

                // No se selecciona psw_sig para evitar que la contraseña circule innecesariamente
                const string query = @"
                    SELECT c_user, c_codigo, c_nombre, c_costo
                    FROM cs_user
                    WHERE c_user = :puser AND psw_sig = :ppsw";

                using var cmd = new OracleCommand(query, oconexion);
                cmd.Parameters.Add(new OracleParameter("puser", usu));
                cmd.Parameters.Add(new OracleParameter("ppsw", psw));
                cmd.CommandType  = CommandType.Text;
                cmd.CommandTimeout = TimeoutSegundos;

                using var dr = await cmd.ExecuteReaderAsync(cts.Token);

                if (await dr.ReadAsync(cts.Token))
                {
                    objeto.c_user    = dr["c_user"]?.ToString();
                    objeto.c_codigo  = dr["c_codigo"]?.ToString();
                    objeto.c_nombre  = dr["c_nombre"]?.ToString();
                    objeto.c_costo   = dr["c_costo"]?.ToString();
                    objeto.psw_sig   = psw; // conservar en memoria solo para el flujo de login

                    _logger?.LogInformation("✅ Usuario encontrado en CS_USER: {CUser}", objeto.c_user);
                }
                else
                {
                    _logger?.LogWarning("❌ Usuario '{Usuario}' no encontrado o contraseña incorrecta en CS_USER.", usu);
                }
            }
            catch (OperationCanceledException)
            {
                _logger?.LogError("⏱ Timeout ({Seg}s) al conectar con Oracle para autenticar usuario {Usuario}.", TimeoutSegundos, usu);
                objeto = new Usuario();
            }
            catch (OracleException oex)
            {
                _logger?.LogError("❌ ERROR Oracle al autenticar {Usuario}: ORA-{Codigo} {Message}", usu, oex.Number, oex.Message);
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
