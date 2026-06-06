using System.Text.Json.Serialization;

namespace FabricaHilos.Sire.Models;

/// <summary>
/// Respuesta del servicio 5.2 SIRE: consultar periodos habilitados.
/// Estructura anidada según manual v25 pág 25.
/// </summary>
public sealed class EjercicioPeriodosDto
{
    [JsonPropertyName("numEjercicio")]
    public string NumEjercicio { get; set; } = string.Empty;

    [JsonPropertyName("desEstado")]
    public string DesEstado { get; set; } = string.Empty;

    [JsonPropertyName("lisPeriodos")]
    public List<PeriodoDto> ListaPeriodos { get; set; } = new();
}

/// <summary>
/// Periodo tributario dentro de un ejercicio, con su estado.
/// </summary>
public sealed class PeriodoDto
{
    [JsonPropertyName("perTributario")]
    public string PerTributario { get; set; } = string.Empty;

    [JsonPropertyName("codEstado")]
    public string CodEstado { get; set; } = string.Empty;

    [JsonPropertyName("desEstado")]
    public string DesEstado { get; set; } = string.Empty;
}

/// <summary>
/// DTO simplificado para uso interno (dashboard) después de aplanar la estructura de SUNAT.
/// </summary>
public sealed class PropuestaDto
{
    public string Periodo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
}
