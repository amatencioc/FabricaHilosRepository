using FabricaHilos.Models.Capacitacion;

namespace FabricaHilos.Services.Capacitacion;

public interface ICapacitacionService
{
    // ── Catálogo ──────────────────────────────────────────────────────
    Task<List<CapCategoria>>  GetCategoriasAsync();
    Task<List<CapCurso>>      GetCatalogoAsync(string codUsuario, int? idCategoria = null,
                                               string? busqueda = null, string? nivel = null,
                                               bool soloObligatorios = false, bool soloPendientes = false,
                                               int? idCurso = null, int pagina = 1, int tamPag = 0);
    Task<int>                 GetCatalogoTotalAsync(string codUsuario, int? idCategoria = null,
                                               string? busqueda = null, string? nivel = null,
                                               bool soloObligatorios = false, bool soloPendientes = false);
    Task<CapCurso?>           GetCursoDetalleAsync(int idCurso, string codUsuario);

    // ── Mi Panel ──────────────────────────────────────────────────────
    Task<List<CapCurso>>      GetMisCursosAsync(string codUsuario);
    Task<MiPanelVm>           GetMiPanelAsync(string codUsuario);

    // ── Player ────────────────────────────────────────────────────────
    Task<CursoPlayerVm?>      GetPlayerAsync(int idCurso, long idContenido, string codUsuario);
    Task<bool>                MarcarCompletadoAsync(long idInscripcion, long idContenido, int segReproducido);

    // ── Inscripción ───────────────────────────────────────────────────
    Task<(bool ok, string msg, long idInscripcion)> InscribirseAsync(int idCurso, string codUsuario);
    Task<bool>                ValidarRequisitoAsync(int idCurso, string codUsuario);
    Task<List<CapCurso>>      GetCursosDependientesAsync(int idCurso);

    // ── Admin ─────────────────────────────────────────────────────────
    Task<List<CapInscripcion>> GetInscripcionesAsync(int idCurso);
    Task<List<CapInscripcion>> GetTodasInscripcionesAsync();
    Task<bool>                 InscribirMasivoAsync(int idCurso, IEnumerable<string> usuarios, string inscritoPor, bool obligatorio);

    // ── Autorización LMS ──────────────────────────────────────────────
    /// <summary>
    /// Verifica si el usuario tiene rol administrador del módulo LMS (tabla CAP_ADMIN).
    /// </summary>
    Task<bool> IsCapAdminAsync(string codUsuario);
}
