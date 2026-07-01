using FabricaHilos.Models.Capacitacion;

namespace FabricaHilos.Services.Capacitacion;

public interface IExamenService
{
    Task<CapExamen?>             GetExamenAsync(int idExamen);
    Task<(bool ok, string msg, long idIntento)> IniciarIntentoAsync(int idExamen, long idInscripcion, string codUsuario);
    Task<ExamenRendirVm?>        GetRendirVmAsync(long idIntento, int nroPregunta);
    Task<bool>                   GuardarRespuestaAsync(long idIntento, long idPregunta, string idOpcion);
    Task<bool>                   GuardarRespuestaTextoAsync(long idIntento, long idPregunta, string texto);
    Task<ExamenResultadoVm?>     ProcesarYCerrarAsync(long idIntento, string codUsuario);
    Task<ExamenResultadoVm?>     GetResultadoAsync(long idIntento, string codUsuario);
    Task<bool>                   ValidarTiempoAsync(long idIntento);          // anti-trampa server-side
    Task<List<CapIntentoExamen>> GetIntentosAsync(long idInscripcion);
}
