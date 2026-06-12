using FabricaHilos.Sire.Interfaces;
using Microsoft.Extensions.Logging;

namespace FabricaHilos.Services.Sire;

/// <summary>
/// Implementación de inicialización lazy para servicios SIRE.
/// Garantiza que los servicios SIRE se inicialicen solo cuando sea necesario (acceso a Contabilidad),
/// no al startup de la aplicación.
/// </summary>
public class LazySireInitializer : ILazySireInitializer
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LazySireInitializer> _logger;
    private readonly SemaphoreSlim _initializationSemaphore = new(1, 1);
    private readonly TaskCompletionSource _readyTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _isInitialized = false;

    public bool IsInitialized => _isInitialized;

    public LazySireInitializer(IServiceProvider serviceProvider, ILogger<LazySireInitializer> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InitializeAsync()
    {
        // Double-check locking para evitar reinicializaciones múltiples
        if (_isInitialized)
        {
            _logger.LogDebug("[SIRE-LAZY] Servicios SIRE ya inicializados, saltando reinicialización");
            return;
        }

        await _initializationSemaphore.WaitAsync();
        try
        {
            if (_isInitialized)
            {
                _logger.LogDebug("[SIRE-LAZY] Servicios SIRE ya inicializados (verificación post-lock)");
                return;
            }

            _logger.LogInformation("[SIRE-LAZY] Iniciando inicialización lazy de servicios SIRE...");

            // 1. Inicializar SireAuthService: obtener token SUNAT
            try
            {
                var authService = _serviceProvider.GetRequiredService<ISireAuthService>();
                _logger.LogDebug("[SIRE-LAZY] Obteniendo token de autenticación SUNAT...");
                await authService.GetTokenAsync();
                _logger.LogInformation("[SIRE-LAZY] ✓ Token SUNAT obtenido correctamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SIRE-LAZY] ✗ Error al autenticar con SUNAT");
                throw;
            }

            // 2. Validar que los servicios SIRE estén disponibles
            try
            {
                var ventasService = _serviceProvider.GetRequiredService<ISireVentasService>();
                var comprasService = _serviceProvider.GetRequiredService<ISireComprasService>();
                _logger.LogDebug("[SIRE-LAZY] Servicios SIRE (Ventas/Compras) validados correctamente");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[SIRE-LAZY] ⚠ Advertencia validando servicios SIRE (continuando)");
            }

            _isInitialized = true;
            // Desbloquear a todos los waiters (el worker y cualquier otro)
            _readyTcs.TrySetResult();
            _logger.LogInformation("[SIRE-LAZY] ✓ Inicialización lazy de SIRE completada exitosamente");
        }
        finally
        {
            _initializationSemaphore.Release();
        }
    }

    /// <summary>
    /// Espera sin polling hasta que InitializeAsync() complete.
    /// El worker llama esto antes de procesar jobs.
    /// </summary>
    public Task WaitForInitializationAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized) return Task.CompletedTask;
        return _readyTcs.Task.WaitAsync(cancellationToken);
    }
}
