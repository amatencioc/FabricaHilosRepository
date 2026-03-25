using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace FabricaHilos.Helpers
{
    [SupportedOSPlatform("windows")]
    internal static class NetworkShareHelper
    {
        [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
        private static extern int WNetAddConnection2(
            ref NETRESOURCE lpNetResource,
            string? lpPassword,
            string? lpUserName,
            int dwFlags);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NETRESOURCE
        {
            public int dwScope;
            public int dwType;
            public int dwDisplayType;
            public int dwUsage;
            public string? lpLocalName;
            public string? lpRemoteName;
            public string? lpComment;
            public string? lpProvider;
        }

        private const int RESOURCETYPE_DISK           = 1;
        private const int CONNECT_TEMPORARY           = 4;
        private const int ERROR_ALREADY_ASSIGNED      = 85;
        private const int ERROR_SESSION_CONFLICT      = 1219;

        /// <summary>
        /// Establece una sesión autenticada hacia el share UNC antes de leer archivos.
        /// Es idempotente: si la sesión ya existe no hace nada.
        /// </summary>
        public static void Connect(string uncPath, string? username, string? password, string? domain)
        {
            if (string.IsNullOrEmpty(username)) return;

            var serverShare = ExtractServerShare(uncPath);
            if (serverShare == null) return;

            var nr = new NETRESOURCE
            {
                dwType       = RESOURCETYPE_DISK,
                lpRemoteName = serverShare
            };

            var user   = !string.IsNullOrEmpty(domain) ? $@"{domain}\{username}" : username;
            var result = WNetAddConnection2(ref nr, password, user, CONNECT_TEMPORARY);

            if (result != 0 && result != ERROR_ALREADY_ASSIGNED && result != ERROR_SESSION_CONFLICT)
                throw new InvalidOperationException(
                    $"No se pudo conectar al recurso '{serverShare}'. Código de error Win32: {result}");
        }

        private static string? ExtractServerShare(string uncPath)
        {
            if (!uncPath.StartsWith(@"\\", StringComparison.Ordinal)) return null;
            var parts = uncPath[2..].Split('\\');
            return parts.Length >= 2 ? $@"\\{parts[0]}\{parts[1]}" : null;
        }
    }
}
