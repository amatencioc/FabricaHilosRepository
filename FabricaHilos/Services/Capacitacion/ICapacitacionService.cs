using FabricaHilos.Models.Capacitacion;

namespace FabricaHilos.Services.Capacitacion;

public interface ICapacitacionService
{
    // ── Catálogo ──────────────────────────────────────────────────────
    Task<List<CapCategoria>>  GetCategoriasAsync();
    Task<List<CapCurso>>      GetCatalogoAsync(string codUsuario, int? idCategoria = null,
                                               string? busqueda = null, string? nivel = null,
                                               bool soloObligatorios = false, bool soloPendientes = false,
                                               int? idCurso = null, int pagina = 1, int tamPag = 0,
                                               bool paraAdmin = false);
    Task<int>                 GetCatalogoTotalAsync(string codUsuario, int? idCategoria = null,
                                               string? busqueda = null, string? nivel = null,
                                               bool soloObligatorios = false, bool soloPendientes = false);
    Task<CapCurso?>           GetCursoDetalleAsync(int idCurso, string codUsuario, bool paraAdmin = false);

    // ── Visibilidad y alcance del curso (ver 07_CAP_VISIBILIDAD_CURSO.sql) ─
    Task<List<CapAreaOption>>    GetAreasAsync();
    Task<List<CapCursoArea>>     GetCursoAreasAsync(int idCurso);
    Task<List<CapCursoUsuario>>  GetCursoUsuariosAsync(int idCurso);
    Task<List<CapEmpleadoBusqueda>> BuscarEmpleadosAsync(string term, int take = 20);
    Task SetAlcanceCursoAsync(int idCurso, string visibilidad, string alcance,
                               IEnumerable<string> areas, IEnumerable<string> centrosCosto,
                               IEnumerable<string> usuarios, IEnumerable<string>? cargos = null);

    // ── Jerarquía Área → Centro de Costo (ver 12_CAP_JERARQUIA_CCOSTO.sql) ─
    Task<List<CapCcostoOption>>  GetCentrosCostoAsync(string? granCcosto = null);
    Task<List<CapCursoCcosto>>   GetCursoCcostoAsync(int idCurso);

    // ── Cargo (ver 15_CAP_CURSO_CARGO.sql) ───────────────────────────────
    Task<List<CapCargoOption>>   GetCargosAsync();
    Task<List<CapCursoCargo>>    GetCursoCargoAsync(int idCurso);

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
    Task<List<CapInscripcion>> GetInscripcionesAsync(int idCurso, string? granCcosto = null, string? codSupervisor = null, string? centroCosto = null);
    Task<List<CapInscripcion>> GetTodasInscripcionesAsync(int? idCategoria = null, string? granCcosto = null, string? codSupervisor = null, string? centroCosto = null);
    Task<bool>                 InscribirMasivoAsync(int idCurso, IEnumerable<string> usuarios, string inscritoPor, bool obligatorio);
    Task<List<CapSupervisorOption>>   GetSupervisoresAsync();
    Task<List<CapHeadcountDetalle>>  GetHeadcountJefaturasAsync();
}