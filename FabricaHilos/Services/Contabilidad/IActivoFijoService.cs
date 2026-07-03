using FabricaHilos.Models.Contabilidad;

namespace FabricaHilos.Services.Contabilidad;

public interface IActivoFijoService
{
    // ── Listado ───────────────────────────────────────────────────────────────
    Task<(IEnumerable<ActivoFijoDto> Items, int Total)> ObtenerActivosAsync(
        string? buscar, string? clase, string? estado, int page, int pageSize);

    // ── Detalle ───────────────────────────────────────────────────────────────
    Task<ActivoFijoDto?> ObtenerActivoAsync(string clase, string codigo, int numero);

    // ── Actualización ─────────────────────────────────────────────────────────
    Task ActualizarActivoAsync(ActivoFijoDto dto, string usuario);
    Task ActualizarUsuarioAltaBajaAsync(string clase, string codigo, int numero, string tipo, string usuario);

    // ── Referencias ───────────────────────────────────────────────────────────
    Task<IEnumerable<AfClaseDto>>              ObtenerClasesAsync();
    Task<Dictionary<string, string>>           ObtenerNombresProveedoresAsync(IEnumerable<string> codigos);
    Task<Dictionary<string, string>>           ObtenerDescripcionesCCostosAsync(IEnumerable<string> codigos);
    Task<string?>                              ObtenerNombreEmpleadoAsync(string codEmpleado);

    // ── Firmas para ficha impresa ─────────────────────────────────────────────
    Task<(FirmaAfDto? Alta, FirmaAfDto? Baja)> ObtenerFirmasAsync(
        string? userAlta, string? userBaja);

    // -- Memorando -------------------------------------------------------------------
    Task<List<MemorandoItemDto>> ObtenerActivosParaMemoAsync(IEnumerable<(string Clase, string Codigo, int Numero)> claves);
    Task<FirmaAfDto?> ObtenerFirmaUsuarioAsync(string codigoUsuario);
}

