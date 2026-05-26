// ============================================================
// AquariusPasswordHelper.cs
// Encoding/decoding del campo DES_PASSWORD de MAE_USUARIO
//
// ALGORITMO (aplicación Delphi legacy):
//   Estructura almacenada:
//     [salt:3 dígitos] + [bloque×2 (+ sep opcional)] + [2*salt+30:3 dígitos]
//
//   Cada carácter del password se codifica como:
//     encoded_val = ASCII(char) + salt + 15   →  3 dígitos decimales
//
//   Decodificación:
//     char = CHR(encoded_val - salt - 15)
//
//   Verificación de integridad:
//     suffix = 2 * salt + 30   (siempre)
//
// NOTA: el portal web puede enviar la clave en texto plano
//       directamente a PKG_AUTH_HE_SUPERVISOR.sp_login.
//       Oracle decodifica y compara internamente.
//       Este helper es útil para crear/modificar usuarios desde .NET.
// ============================================================

using System;
using System.Text;

namespace Aquarius.Auth
{
    public static class AquariusPasswordHelper
    {
        // -------------------------------------------------------
        // DECODIFICAR: hash almacenado → texto plano
        // Retorna null si el hash es inválido o está corrupto.
        // -------------------------------------------------------
        public static string? Decode(string stored)
        {
            if (string.IsNullOrEmpty(stored) || stored.Length < 9 || stored.Length % 3 != 0)
                return null;

            try
            {
                int salt   = int.Parse(stored.Substring(0, 3));
                int suffix = int.Parse(stored.Substring(stored.Length - 3, 3));

                // Verificación de integridad
                if (suffix != 2 * salt + 30)
                    return null;

                // Bloque central (sin prefix ni suffix)
                string central      = stored.Substring(3, stored.Length - 6);
                int    totalGroups  = central.Length / 3;

                // ¿Tiene separador central? → grupos impares
                //   Con sep: grupos = 2*N+1 → N=(grupos-1)/2
                //   Sin sep: grupos = 2*N   → N=grupos/2
                int n = (totalGroups % 2 == 1)
                    ? (totalGroups - 1) / 2
                    :  totalGroups      / 2;

                var sb = new StringBuilder(n);
                for (int i = 0; i < n; i++)
                {
                    int val = int.Parse(central.Substring(i * 3, 3));
                    sb.Append((char)(val - salt - 15));
                }
                return sb.ToString();
            }
            catch
            {
                return null;
            }
        }

        // -------------------------------------------------------
        // VERIFICAR: ¿la clave en texto plano coincide con el hash?
        // -------------------------------------------------------
        public static bool Verify(string plaintext, string stored)
        {
            if (plaintext == null) return false;
            string? decoded = Decode(stored);
            return decoded != null && decoded == plaintext;
        }

        // -------------------------------------------------------
        // CODIFICAR: texto plano → hash para INSERT/UPDATE
        //   salt: si se omite, se genera aleatoriamente (30-94).
        //         Pasar el mismo salt solo para pruebas deterministas.
        // -------------------------------------------------------
        public static string Encode(string plaintext, int salt = -1)
        {
            if (string.IsNullOrEmpty(plaintext))
                throw new ArgumentException("El password no puede estar vacío.");

            if (salt < 0)
                salt = new Random().Next(30, 95);   // rango observado en la BD

            var sb = new StringBuilder();

            // Prefix: salt en 3 dígitos
            sb.Append(salt.ToString("D3"));

            // Bloque codificado repetido DOS veces
            for (int rep = 0; rep < 2; rep++)
                foreach (char c in plaintext)
                    sb.Append((c + salt + 15).ToString("D3"));

            // Suffix: 2*salt + 30
            sb.Append((2 * salt + 30).ToString("D3"));

            return sb.ToString();
        }
    }
}

/* ============================================================
   EJEMPLOS DE USO
   ============================================================

   // Verificar login (solo si no se usa sp_login de Oracle)
   string stored = "036101100100108101100100108102";  // CCAL1
   bool ok = AquariusPasswordHelper.Verify("2119", stored);  // → true

   // Crear nuevo usuario en MAE_USUARIO
   string hash = AquariusPasswordHelper.Encode("1234");
   // INSERT INTO MAE_USUARIO (..., des_password) VALUES (..., :hash)

   // Decodificar (para auditoría/admin)
   string plain = AquariusPasswordHelper.Decode(stored);   // → "2119"

   ============================================================
   TABLA DE CONTRASEÑAS ACTUALES (07/05/2026)
   ============================================================
   Usuario       Nombre                  Clave
   -------       ------                  -----
   CCAL1         CONTROL CALIDAD         2119
   hilar1        JEFATURA PLANTA         1615
   rrhh7         ASIST SELEC             0615
   suptej        Sup. Tej ARB            3412
   JTEJARB1      JEFE TELAR ARB          5624
   rrhh1         JEFE RRHH ARBONA        1588
   rrhh3         PLANILLA ARBONA         19yo
   TINT2         SUPERV. TINTO           623785
   calidcol1     JEFE CALIDAD            545786
   coneracol1    SUPERV. CONERA          505095
   conticol1     SUPERV. CONTINUAS       356412
   financol1     JEFE ADMINISTRAC        963147
   labocol1      JEFE LABORATORIO        603335
   mant1         JEF MANTENIMIENTO       5PA454
   segucol1      JEFE SEGURIDAD          663145
   segucol2      SUPERV SEGURIDAD        456123
   calidcol2     JEFE CALIDAD TINTO      11864655
   dsolorz       DANIEL SOLORZANO        32851076
   flocacol      ENCARGADO FLOCA         28134382
   jalmint       JEFE INTERMEDIOS        41080855
   jalmqui       JEFE ALM QUIMICOS       76374041
   rrhh2         JEFE RRHH COLONIAL      citoamir
   rrhh4         SELECCION COLONIAL      57304390
   rrhh6         ASISTENTE ARBONA        ILUZGIAN
   sistemas2     COORD. SISTEMAS         emassist
   vigicol1      VIGILANCIA              19504331
   PREP1         PREPARATORIA            P1PR
   a             Administrador           v3C1
   TINT1         TINTCOLONIAL            FRE123
   retorcol1     ENCARG. RETORC          so1alo
   rrhh5         ASISTENTE COLONIAL      ial5colo
   sistemas1     JEFE DE SISTEMAS        mas1sist
   ============================================================ */
