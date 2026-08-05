using FabricaHilos.Models.Contabilidad;

namespace FabricaHilos.Services.Contabilidad;

public interface IActivoFijoService
{
    // ── Listado ───────────────────────────────────────────────────────────────
    /// <param name="soloSistemas">
    /// true  → solo CLASE IN ('07','09') (equipos de computo + activos menores), sin importar el CCOSTO.
    /// false → excluye CLASE='07' (cualquier otro usuario), sin importar el CCOSTO.
    /// null  → sin filtro de clase.
    /// </param>
    Task<(IEnumerable<ActivoFijoDto> Items, int Total)> ObtenerActivosAsync(
        string? buscar, string? clase, string? estado, int page, int pageSize,
        bool? soloSistemas = null);

    /// Devuelve el C_COSTO (centro de costo) del usuario Oracle activo.
    Task<string?> ObtenerCcostoUsuarioAsync(string cUser);

    // ── Detalle ───────────────────────────────────────────────────────────────
    Task<ActivoFijoDto?> ObtenerActivoAsync(string clase, string codigo, int numero);

    // ── Actualización ─────────────────────────────────────────────────────────
    Task ActualizarActivoAsync(ActivoFijoDto dto, string usuario);
    Task ActualizarUsuarioAltaBajaAsync(string clase, string codigo, int numero, string tipo, string usuario);
    Task LimpiarUsuarioAltaBajaAsync(string clase, string codigo, int numero, string tipo);
    Task ActualizarObservacionesAsync(string clase, string codigo, int numero, string tipo, string obs, string usuario,
        string? estadoBaja = null, DateTime? fBaja = null, bool fBajaEnviada = false,
        string? cSestado = null, DateTime? fOpera = null, bool fOperaEnviada = false);

    // ── Referencias ───────────────────────────────────────────────────────────
    /// <param name="soloSistemas">Mismo criterio que ObtenerActivosAsync, para no ofrecer clases que el listado descartaria.</param>
    Task<IEnumerable<AfClaseDto>>              ObtenerClasesAsync(bool? soloSistemas = null);
    Task<Dictionary<string, string>>           ObtenerNombresProveedoresAsync(IEnumerable<string> codigos);
    Task<Dictionary<string, string>>           ObtenerDescripcionesCCostosAsync(IEnumerable<string> codigos);
    Task<string?>                              ObtenerNombreEmpleadoAsync(string codEmpleado);

    // ── Visado de Alta ────────────────────────────────────────────────────────
    /// <summary>Genera token, guarda en BD y devuelve el payload listo para enviar por email.</summary>
    Task<VisadoAltaEmailData?> PrepararEnvioVisadoAsync(
        string clase, string codigo, int numero, string baseUrl);

    /// <summary>Procesa la respuesta del visado (aprobación u observación) por token.</summary>
    Task<VisadoResultado> ProcesarVisadoAsync(
        string token, string accion, string? observacion, string ipRemota);
    Task<(FirmaAfDto? Alta, FirmaAfDto? Baja)> ObtenerFirmasAsync(
        string? userAlta, string? userBaja);

    Task<FirmaAfDto?> ObtenerFirmaJefaturaAsync(string? cCodigo);

    // -- Memorando -------------------------------------------------------------------
    Task<List<MemorandoItemDto>> ObtenerActivosParaMemoAsync(IEnumerable<(string Clase, string Codigo, int Numero)> claves);
    Task<FirmaAfDto?> ObtenerFirmaUsuarioAsync(string codigoUsuario);
}

