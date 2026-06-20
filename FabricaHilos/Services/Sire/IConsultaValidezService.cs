namespace FabricaHilos.Services.Sire;

/// <summary>Resultado de validar un comprobante contra la API Consulta Integrada de SUNAT.</summary>
public sealed record ValidezResult(
    string EstadoCp,    // 0=NO EXISTE 1=ACEPTADO 2=ANULADO 3=AUTORIZADO 4=NO AUTORIZADO
    string EstadoRuc,   // 00=ACTIVO 01=BAJA PROVISIONAL 10=BAJA DEFINITIVA 11=BAJA OFICIO ...
    string CondDomiRuc  // 00=HABIDO 09=PENDIENTE 11=POR VERIFICAR 12=NO HABIDO 20=NO HALLADO
);

public interface IConsultaValidezService
{
    /// <summary>
    /// Valida un comprobante de pago en SUNAT (API Consulta Integrada de Validez).
    /// Reutiliza el token OAuth2 mientras no expire (3600 s).
    /// </summary>
    Task<ValidezResult?> ValidarAsync(
        string rucEmisor,
        string tipdoc,
        string serie,
        string numero,
        DateTime fechaEmision,
        decimal monto,
        CancellationToken ct = default);

    /// <summary>Invalida el token cacheado forzando renovación en la próxima llamada.</summary>
    void InvalidarToken();
}
