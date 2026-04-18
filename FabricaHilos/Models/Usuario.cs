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
    }
}
