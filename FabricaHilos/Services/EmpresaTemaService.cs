using FabricaHilos.Config;
using Microsoft.Extensions.Options;

namespace FabricaHilos.Services;

public interface IEmpresaTemaService
{
    EmpresaConfig GetTemaActual();
}

public class EmpresaTemaService : IEmpresaTemaService
{
    private readonly EmpresaTemaOptions _options;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public EmpresaTemaService(IOptions<EmpresaTemaOptions> options, IHttpContextAccessor httpContextAccessor)
    {
        _options = options.Value;
        _httpContextAccessor = httpContextAccessor;
    }

    public EmpresaConfig GetTemaActual()
    {
        // Leer la empresa desde la sesión del usuario logueado
        var session = _httpContextAccessor.HttpContext?.Session;
        var empresaConexion = session?.GetString("EmpresaConexion");

        // "ArbonaConnection" → "Arbona" | cualquier otra → "LaColonial"
        var empresaKey = empresaConexion == "ArbonaConnection" ? "Arbona" : _options.EmpresaActiva;

        if (_options.Empresas.TryGetValue(empresaKey, out var config))
            return config;

        // Fallback seguro: retornar la empresa activa por defecto
        return _options.Empresas[_options.EmpresaActiva];
    }
}
