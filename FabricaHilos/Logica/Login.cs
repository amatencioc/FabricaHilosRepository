using System;
using System.Data;
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
        private readonly ILogger<Login> _logger;

        public Login(IConfiguration configuration, ILogger<Login> logger = null)
        {
            _conexion = configuration.GetConnectionString("OracleConnection");
            _logger = logger;
        }

        /// <summary>
        /// Busca y valida un usuario en la base de datos Oracle
        /// </summary>
        /// <param name="usu">Nombre de usuario</param>
        /// <param name="psw">Contraseña del usuario</param>
        /// <returns>Objeto Usuario si existe, o Usuario vacío si no se encuentra</returns>
        public Usuario EncontrarUsuario(string usu, string psw)
        {
            Usuario objeto = new Usuario();

            try
            {
                _logger?.LogInformation("═══════════════════════════════════════════");
                _logger?.LogInformation("🔍 INTENTANDO CONECTAR A ORACLE DATABASE");
                _logger?.LogInformation("═══════════════════════════════════════════");
                _logger?.LogInformation("👤 Usuario recibido: {Usuario}", usu);
                _logger?.LogInformation("🔑 Contraseña recibida: {Password}", new string('*', psw?.Length ?? 0));
                _logger?.LogInformation("🔌 Cadena de conexión: {Conexion}", _conexion?.Substring(0, Math.Min(50, _conexion?.Length ?? 0)) + "...");

                using (OracleConnection oconexion = new OracleConnection(_conexion))
                {
                    string query = "select c_user, psw_sig, c_costo from cs_user where c_user = :puser and psw_sig = :ppsw";

                    _logger?.LogInformation("📝 Query SQL: {Query}", query);

                    OracleCommand cmd = new OracleCommand(query, oconexion);
                    cmd.Parameters.Add(new OracleParameter("puser", usu));
                    cmd.Parameters.Add(new OracleParameter("ppsw", psw));
                    cmd.CommandType = CommandType.Text;

                    _logger?.LogInformation("🔄 Abriendo conexión a Oracle...");
                    oconexion.Open();
                    _logger?.LogInformation("✅ Conexión a Oracle abierta exitosamente");

                    using (OracleDataReader dr = cmd.ExecuteReader())
                    {
                        _logger?.LogInformation("🔍 Ejecutando query y leyendo resultados...");

                        if (dr.Read())
                        {
                            objeto.c_user = dr["c_user"]?.ToString();
                            objeto.psw_sig = dr["psw_sig"]?.ToString();
                            objeto.c_costo = dr["c_costo"]?.ToString();

                            _logger?.LogInformation("✅ USUARIO ENCONTRADO EN ORACLE");
                            _logger?.LogInformation("   - c_user: {CUser}", objeto.c_user);
                            _logger?.LogInformation("   - c_costo: {CCosto}", objeto.c_costo);
                        }
                        else
                        {
                            _logger?.LogWarning("❌ USUARIO NO ENCONTRADO EN ORACLE");
                            _logger?.LogWarning("   El usuario '{Usuario}' no existe o la contraseña es incorrecta", usu);
                        }
                    }

                    _logger?.LogInformation("🔒 Cerrando conexión a Oracle...");
                }

                _logger?.LogInformation("═══════════════════════════════════════════");
            }
            catch (OracleException oex)
            {
                _logger?.LogError("❌ ERROR DE ORACLE: {Message}", oex.Message);
                _logger?.LogError("   Código de error: {ErrorCode}", oex.Number);
                _logger?.LogError("   Detalles: {Details}", oex.ToString());
                objeto = new Usuario();
            }
            catch (Exception ex)
            {
                _logger?.LogError("❌ ERROR GENERAL AL CONECTAR CON ORACLE");
                _logger?.LogError("   Tipo: {Type}", ex.GetType().Name);
                _logger?.LogError("   Mensaje: {Message}", ex.Message);
                _logger?.LogError("   Stack Trace: {StackTrace}", ex.StackTrace);
                objeto = new Usuario();
            }

            return objeto;
        }
    }
}
