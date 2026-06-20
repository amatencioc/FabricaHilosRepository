using FabricaHilos.Models.Produccion.Planeamiento;

namespace FabricaHilos.Services.Produccion.Planeamiento;

public interface IPlnPendientesService
{
    /// <summary>SP_PLN_FILTRO_TIPO: tipos H/G para combos de filtro.</summary>
    Task<IEnumerable<PlnFiltroTipo>> GetFiltroTipoAsync();

    /// <summary>SP_PLN_PEND_REVISADO: partidas pendientes de revisado (Martín).</summary>
    Task<IEnumerable<PlnPendienteRevisado>> GetPendientesRevisadoAsync(
        string tipo = "%", string asesor = "%", string cliente = "%");

    /// <summary>SP_PLN_PEND_EVAL_CALIDAD: partidas pendientes de evaluación de calidad (Ivon).</summary>
    Task<IEnumerable<PlnPendienteEvalCalidad>> GetPendientesEvalCalidadAsync(
        string tipo = "%", string asesor = "%", string cliente = "%");

    /// <summary>SP_PLN_PEND_ENCONADO: partidas aprobadas pendientes de enconado/devanado (Guevara).</summary>
    Task<IEnumerable<PlnPendienteEnconado>> GetPendientesEnconadoAsync(
        string tipo = "%", string asesor = "%", string cliente = "%");

    /// <summary>SP_PLN_PEND_TENIDO: partidas pendientes de teñido (Fredy/Malena).</summary>
    Task<IEnumerable<PlnPendienteTenido>> GetPendientesTenidoAsync(
        string tipo = "%", string asesor = "%", string cliente = "%");

    /// <summary>SP_PLN_PEND_SECADO: partidas terminadas en tintorería pendientes de secado (Freddy/Malena).</summary>
    Task<IEnumerable<PlnPendienteSecado>> GetPendientesSecadoAsync(
        string tipo = "%", string asesor = "%", string cliente = "%");

    /// <summary>SP_PLN_PEND_MADEJA: partidas programadas pendientes de acabado de madeja.</summary>
    Task<IEnumerable<PlnPendienteMadeja>> GetPendientesMadejaAsync(
        string tipo = "%", string asesor = "%", string cliente = "%");

    /// <summary>SP_PLN_PEND_PARTIDAS_DEF: partidas con evaluación de calidad pendiente de definición (Karen).</summary>
    Task<IEnumerable<PlnPendientePartidaDef>> GetPendientesPartidasDefAsync(
        string estEval = "%");
}
