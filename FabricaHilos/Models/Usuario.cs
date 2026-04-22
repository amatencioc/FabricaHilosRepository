using System;

namespace FabricaHilos.Models
{
    /// <summary>
    /// Modelo de Usuario para autenticación con Oracle
    /// </summary>
    public class Usuario
    {
        public string c_user { get; set; }
        public string psw_sig { get; set; }
        public string c_codigo { get; set; }
        public string c_nombre { get; set; }
        public string c_costo { get; set; }
        public string acceso_web { get; set; }
        /// <summary>
        /// Empresa a la que pertenece el usuario: "COLONIAL" o "ARBONA".
        /// Determina qué connection string se usará en la sesión.
        /// </summary>
        public string Empresa { get; set; }
    }
}
