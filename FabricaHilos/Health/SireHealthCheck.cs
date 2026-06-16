using FabricaHilos.Sire.Interfaces;
using FabricaHilos.Sire.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FabricaHilos.Health;

/// <summary>
/// Health check para monitorear el estado de la integración SUNAT SIRE.
/// Valida: autenticación, conectividad API, disponibilidad de servicios RVIE/RCE.
/// </summary>
public sealed class SireHealthCheck : IHealthCheck
{
    private readonly ISireAuthService _authService;
    private readonly ISireVentasService _ventasService;
    private readonly ISireComprasService _comprasService;
    private readonly ILogger<SireHealthCheck> _logger;

    public SireHealthCheck(
        ISireAuthService authService,
        ISireVentasService ventasService,
        ISireComprasService comprasService,
        ILogger<SireHealthCheck> logger)
    {
        _authService = authService;
        _ventasService = ventasService;
        _comprasService = comprasService;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>();
        var warnings = new List<string>();

        try
        {
            // 1. Validar autenticación OAuth2
            _logger.LogDebug("[SIRE-HEALTH] Validando autenticación SUNAT...");
            var token = await _authService.GetTokenAsync(cancellationToken);

            data["auth_ok"] = true;
            data["token_type"] = token.TokenType;
            data["token_expires_utc"] = token.ExpiraEnUtc.ToString("u");

            var minutosRestantes = (token.ExpiraEnUtc - DateTime.UtcNow).TotalMinutes;
            data["token_minutes_remaining"] = Math.Round(minutosRestantes, 1);

            // Advertencia si el token expira pronto (menos de 10 minutos)
            if (minutosRestantes < 10)
            {
                warnings.Add($"Token expira en {Math.Round(minutosRestantes, 1)} minutos");
            }

            // 2. Validar conectividad RVIE
            _logger.LogDebug("[SIRE-HEALTH] Validando disponibilidad RVIE...");
            try
            {
                var rvie = await _ventasService.ObtenerPeriodosAsync(cancellationToken);
                data["rvie_ok"] = true;
                data["rvie_periodos"] = rvie.Count;
            }
            catch (SireApiException ex)
            {
                _logger.LogWarning(ex, "[SIRE-HEALTH] Error al consultar RVIE: {Status}", ex.StatusCode);
                data["rvie_ok"] = false;
                data["rvie_error"] = $"{(int)(ex.StatusCode ?? 0)} {ex.Message}";

                // 401/403 indica problema de autorización (credenciales, scope, etc.)
                if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized || 
                    ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return HealthCheckResult.Degraded(
                        "SIRE: Autenticación OK pero acceso RVIE denegado. Verificar permisos de la aplicación en SUNAT.",
                        data: data);
                }

                warnings.Add($"RVIE no disponible: {ex.StatusCode ?? 0}");
            }

            // 3. Validar conectividad RCE
            _logger.LogDebug("[SIRE-HEALTH] Validando disponibilidad RCE...");
            try
            {
                var rce = await _comprasService.ObtenerPeriodosAsync(cancellationToken);
                data["rce_ok"] = true;
                data["rce_periodos"] = rce.Count;
            }
            catch (SireApiException ex)
            {
                _logger.LogWarning(ex, "[SIRE-HEALTH] Error al consultar RCE: {Status}", ex.StatusCode);
                data["rce_ok"] = false;
                data["rce_error"] = $"{(int)(ex.StatusCode ?? 0)} {ex.Message}";

                if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized || 
                    ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return HealthCheckResult.Degraded(
                        "SIRE: Autenticación OK pero acceso RCE denegado. Verificar permisos de la aplicación en SUNAT.",
                        data: data);
                }

                warnings.Add($"RCE no disponible: {ex.StatusCode}");
            }

            // Resultado final
            if (warnings.Count > 0)
            {
                data["warnings"] = string.Join("; ", warnings);
                return HealthCheckResult.Degraded($"SIRE parcialmente disponible: {string.Join(", ", warnings)}", data: data);
            }

            _logger.LogInformation("[SIRE-HEALTH] ✅ Todos los servicios SIRE operativos");
            return HealthCheckResult.Healthy("SIRE: Autenticación y servicios RVIE/RCE operativos", data: data);
        }
        catch (SireApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            // Error de autenticación (credenciales inválidas o expiradas)
            _logger.LogError(ex, "[SIRE-HEALTH] ❌ Error de autenticación SUNAT");
            data["auth_ok"] = false;
            data["auth_error"] = ex.Message;
            return HealthCheckResult.Unhealthy(
                "SIRE: Error de autenticación. Verificar ClientId/ClientSecret/UsuarioSOL/ClaveSol en appsettings.json",
                ex,
                data);
        }
        catch (HttpRequestException ex)
        {
            // Error de red/conectividad
            _logger.LogError(ex, "[SIRE-HEALTH] ❌ Error de conectividad SUNAT");
            data["connectivity_error"] = ex.Message;
            return HealthCheckResult.Unhealthy("SIRE: Error de conectividad con SUNAT", ex, data);
        }
        catch (Exception ex)
        {
            // Error inesperado
            _logger.LogError(ex, "[SIRE-HEALTH] ❌ Error inesperado en health check");
            return HealthCheckResult.Unhealthy("SIRE: Error inesperado en health check", ex, data);
        }
    }
}
