namespace FabricaHilos.LecturaCorreos.Services;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

/// <summary>
/// Rastrea fallos consecutivos por cuenta. Tras <see cref="UmbralFallos"/> fallos
/// seguidos abre el circuito y suspende la cuenta durante <see cref="TiempoSuspension"/>.
/// Se resetea automáticamente al expirar el tiempo o al registrar un éxito.
/// </summary>
public interface ICuentaCircuitBreaker
{
    /// <summary>Devuelve true si la cuenta está suspendida (circuito abierto).</summary>
    bool EstaSuspendida(string cuentaNombre);

    /// <summary>Debe llamarse cuando la lectura/conexión de una cuenta tiene éxito.</summary>
    void RegistrarExito(string cuentaNombre);

    /// <summary>Debe llamarse cuando la lectura/conexión de una cuenta falla.</summary>
    void RegistrarFallo(string cuentaNombre);
}

public sealed class CuentaCircuitBreaker : ICuentaCircuitBreaker
{
    private const int UmbralFallos = 5;
    private static readonly TimeSpan TiempoSuspension = TimeSpan.FromMinutes(30);

    private sealed record EstadoCuenta(int Fallos, DateTime? SuspendidaHasta);

    private readonly ConcurrentDictionary<string, EstadoCuenta> _estado = new();
    private readonly ILogger<CuentaCircuitBreaker>              _logger;

    public CuentaCircuitBreaker(ILogger<CuentaCircuitBreaker> logger) => _logger = logger;

    public bool EstaSuspendida(string cuentaNombre)
    {
        if (!_estado.TryGetValue(cuentaNombre, out var est) || est.SuspendidaHasta is null)
            return false;

        if (DateTime.UtcNow < est.SuspendidaHasta)
        {
            _logger.LogWarning(
                "⚡ Cuenta '{Cuenta}' suspendida por circuit breaker. Se reactiva en {Restante:F0} min.",
                cuentaNombre, (est.SuspendidaHasta.Value - DateTime.UtcNow).TotalMinutes);
            return true;
        }

        // Suspensión expirada → reset automático.
        _estado.TryUpdate(cuentaNombre, new EstadoCuenta(0, null), est);
        _logger.LogInformation(
            "⚡ Cuenta '{Cuenta}': suspensión expirada. Se reanuda el procesamiento.", cuentaNombre);
        return false;
    }

    public void RegistrarExito(string cuentaNombre) =>
        _estado[cuentaNombre] = new EstadoCuenta(0, null);

    public void RegistrarFallo(string cuentaNombre)
    {
        var nuevo = _estado.AddOrUpdate(
            cuentaNombre,
            _       => new EstadoCuenta(1, null),
            (_, ant) => ant with { Fallos = ant.Fallos + 1 });

        if (nuevo.Fallos >= UmbralFallos && nuevo.SuspendidaHasta is null)
        {
            var hasta = DateTime.UtcNow.Add(TiempoSuspension);
            // TryUpdate garantiza que solo el primer hilo en llegar aquí abre el circuito
            // y emite el log. Los demás hilos concurrentes verán un CAS fallido y no loguean.
            if (_estado.TryUpdate(cuentaNombre, nuevo with { SuspendidaHasta = hasta }, nuevo))
            {
                _logger.LogError(
                    "⚡ CIRCUIT BREAKER ABIERTO — cuenta '{Cuenta}' suspendida tras {N} fallos consecutivos. Se reactiva a las {Hasta:HH:mm:ss} UTC.",
                    cuentaNombre, UmbralFallos, hasta);
            }
        }
    }
}
