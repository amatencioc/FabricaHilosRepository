using System.Net;

namespace FabricaHilos.Services
{
    /// <summary>
    /// Determina si la petición actual proviene de la red interna o externa.
    /// Reutiliza la misma lógica de subnets de appsettings.json → RedInterna:Subnets.
    /// </summary>
    public interface IRedInternaService
    {
        bool EsRedInterna();
    }

    public class RedInternaService : IRedInternaService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly string[] _subnets;

        public RedInternaService(IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
        {
            _httpContextAccessor = httpContextAccessor;
            _subnets = configuration.GetSection("RedInterna:Subnets").Get<string[]>() ?? [];
        }

        public bool EsRedInterna()
        {
            var remoteIp = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress;
            if (remoteIp == null || IPAddress.IsLoopback(remoteIp)) return true;

            if (remoteIp.IsIPv4MappedToIPv6)
                remoteIp = remoteIp.MapToIPv4();

            foreach (var subnet in _subnets)
            {
                var partes = subnet.Split('/');
                if (partes.Length != 2) continue;
                if (!IPAddress.TryParse(partes[0], out var redBase)) continue;
                if (!int.TryParse(partes[1], out var prefixLen)) continue;

                var ipBytes = remoteIp.GetAddressBytes();
                var redBytes = redBase.GetAddressBytes();
                if (ipBytes.Length != redBytes.Length) continue;

                bool match = true;
                int fullBytes = prefixLen / 8;
                int bits = prefixLen % 8;
                for (int i = 0; i < fullBytes && match; i++)
                    match = ipBytes[i] == redBytes[i];
                if (match && bits > 0)
                {
                    int mask = 0xFF << (8 - bits);
                    match = (ipBytes[fullBytes] & mask) == (redBytes[fullBytes] & mask);
                }
                if (match) return true;
            }
            return false;
        }
    }
}
